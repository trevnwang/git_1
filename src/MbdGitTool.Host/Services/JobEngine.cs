using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using MbdGitTool.Core.Contracts;
using MbdGitTool.Core.Domain;
using MbdGitTool.Core.Security;

namespace MbdGitTool.Host.Services;

/// <summary>
/// M2b 作业内核（SRS §9.7 FR-OP-08、§9.16 FR-ACT-05/06、§13.1.2 AC-UI-10c）：
/// <para>后台队列（单 worker 串行执行 job；job 内 item 串行）。<b>互斥</b>：单 worker 保证 job 之间串行，
/// 但批量自定义 kind（SYNC/DELETE/CLONE/RESTORE/CREATE/TAG）逐仓执行前仍复用
/// <see cref="RepoActionService.AcquireRepoLockAsync"/> 的 per-repo 锁，与单仓 M2a 写操作互斥（同仓串行、异仓可并发）；</para>
/// <para>每个 job 独立 <see cref="CancellationTokenSource"/>，<c>cancelJob</c> 合作式取消当前 git 进程：
/// 只有 PENDING/RUNNING 可取消；未开始 item 记 <c>CANCELED</c> 且 <c>errorCode=null</c>（取消≠超时），
/// RUNNING item 随 CancellationToken 合作取消；</para>
/// <para><c>retryFailed</c> 用原 job 的 FAILED/INTERRUPTED/CANCELED items 创建新 job（不重跑成功仓）；</para>
/// <para>审计：每个 item 的每个终态（OK/FAILED/SKIPPED/CANCELED/INTERRUPTED/PREVIEW）<b>恰写 1 行</b>
/// （含预跳过、取消未开始、job 级内部异常、重启改写等路径），追加 jobId/jobItemId/result/backupRef；</para>
/// <para>持久化 <c>%LOCALAPPDATA%\MbdGitTool\jobs\jobs.json</c>（<see cref="AppConfig.JobsRoot"/> 可注入）；
/// 重启加载时 RUNNING item → <c>INTERRUPTED</c>、未开始 PENDING item → <c>CANCELED</c>（进程重启未执行），
/// 绝不重复执行；最多保留最近 200 个 job；</para>
/// <para>每次状态/逐仓进度变化通过 <see cref="EventSink"/> 推送
/// <c>{ event:"JOB_UPDATED", jobId, current:JobDto, data:JobDto, state, total, cancellable }</c>；
/// <c>args.dryRun=true</c> 的作业只输出预检（item/job state = <c>PREVIEW</c>），零仓库写。</para>
/// </summary>
public sealed class JobEngine : IDisposable
{
    /// <summary>任务历史上限（与 <see cref="JobStore.MaxJobs"/> 一致）。</summary>
    public const int MaxRetainedJobs = JobStore.MaxJobs;

    /// <summary>getJobs 默认返回条数。</summary>
    public const int DefaultGetJobsLimit = 50;

    /// <summary>单次 runBatch 组件数上限（防一次请求展开过多 item；>10 组件/&gt;40 仓的二次确认在 UI 侧）。</summary>
    public const int MaxBatchComponents = 200;

    /// <summary>单次 runBatch 的 item 上限（组件 × 仓类型）。</summary>
    public const int MaxBatchItems = 800;

    /// <summary>允许的批量 kind（SRS §8.1 job.kind CHECK 子集；REPAIR 等修复类不在本轮实现）。</summary>
    private static readonly string[] KindWhitelist =
    {
        "FETCH", "PULL", "CHKOUT", "COMMIT", "PUSH", "SYNC", "CLONE", "DELETE", "RESTORE", "CREATE", "TAG"
    };

    /// <summary>单仓五键（复用 <see cref="RepoActionService"/> 的矩阵/确认/工具链）。</summary>
    private static readonly string[] RepoActionKinds = { "FETCH", "PULL", "CHKOUT", "COMMIT", "PUSH" };

    /// <summary>必须 <c>args.confirm=true</c> 才执行的“强写”kind（批量入口 UI 二次确认 + Host 门禁双保险）。</summary>
    private static readonly string[] ConfirmRequiredKinds = { "COMMIT", "PUSH", "SYNC", "DELETE", "RESTORE", "CREATE", "TAG" };

    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private readonly AppConfig _config;
    private readonly SnapshotService _snapshots;
    private readonly RepoActionService _actions;
    private readonly OperationAuditService _audit;
    private readonly OperationJournal? _journal;
    private readonly GitCli _git;
    private readonly JobStore _store;
    private readonly BatchOperationService _batch;
    private readonly PathPolicy? _paths;
    private readonly BridgeError? _workspaceError;
    private readonly Action? _onJobFinished;
    private readonly Channel<JobRecord> _queue;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Task _worker;
    private readonly object _sync = new();
    private readonly List<JobRecord> _jobs = new();

    private bool _loaded;
    private bool _disposed;
    private int _trashCleanupStarted;

    /// <param name="config">应用配置（工作区/JobsRoot；审计与作业持久化落点）。</param>
    /// <param name="snapshots">快照服务（job 开始时 read-through 刷新，保证矩阵/路径基于动作前状态）。</param>
    /// <param name="git">git 调用封装（可空；测试可注入）。</param>
    /// <param name="actions">单仓动作服务（可空；默认按 config 创建）。</param>
    /// <param name="audit">审计服务（可空；默认按 config 创建）。</param>
    /// <param name="store">作业持久化（可空；默认按 config 创建）。</param>
    /// <param name="onJobFinished">作业结束回调（HostCommandRouter 用于失效派生缓存/刷新 CAS 基线）。</param>
    /// <param name="journal">
    /// M2b-2b 写前 journal（FR-SAFE-04；可空 = 不写 journal）。批量自定义 kind（SYNC/DELETE/CLONE/
    /// RESTORE/CREATE/TAG）在 git 写前由 JobEngine 写 STARTED/DONE/FAILED；单仓五键由
    /// <see cref="RepoActionService"/> 写（同一实例注入）。仅显式 <see cref="AppConfig.JournalRoot"/>
    /// 非空且未注入时按 config 创建，避免单测写真实用户目录。
    /// </param>
    public JobEngine(
        AppConfig config,
        SnapshotService snapshots,
        GitCli? git = null,
        RepoActionService? actions = null,
        OperationAuditService? audit = null,
        JobStore? store = null,
        Action? onJobFinished = null,
        OperationJournal? journal = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _snapshots = snapshots ?? throw new ArgumentNullException(nameof(snapshots));
        _git = git ?? new GitCli();
        _journal = journal
            ?? (string.IsNullOrWhiteSpace(config.JournalRoot) ? null : new OperationJournal(config));
        _actions = actions ?? new RepoActionService(config: config, journal: _journal);
        _audit = audit ?? new OperationAuditService(config);
        _store = store ?? new JobStore(config);
        _onJobFinished = onJobFinished;

        if (config.HasWorkspace)
        {
            try
            {
                _paths = new PathPolicy(config.WorkspaceRoot!);
            }
            catch (ArgumentException ex)
            {
                _workspaceError = new BridgeError(
                    BridgeErrorCodes.WorkspaceInvalid,
                    "工作区根目录非法：" + ex.Message);
            }
        }
        else
        {
            _workspaceError = new BridgeError(
                BridgeErrorCodes.WorkspaceUnset,
                "未配置工作区根目录（config.json 的 workspaceRoot）");
        }

        _batch = new BatchOperationService(config, _actions, _git, _paths);
        _queue = Channel.CreateUnbounded<JobRecord>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
        _worker = Task.Run(WorkerLoopAsync);
    }

    /// <summary>
    /// 作业事件推送出口（由 <c>HostCommandRouter.EventSink</c> 转发给 WebViewBridge）。
    /// 回调参数固定为 <see cref="JobUpdatedEventDto"/> 形状：
    /// <c>{ event:"JOB_UPDATED", jobId, current:JobDto, data:JobDto, state, total, cancellable }</c>；
    /// <c>current</c> 与 <c>data</c> 为同一份 job 快照；回调异常不影响作业。
    /// </summary>
    public Action<object>? EventSink { get; set; }

    /// <summary>已加载的作业数（诊断用；不含内存队列中的内部对象）。</summary>
    public int RetainedJobCount
    {
        get
        {
            lock (_sync)
            {
                return _jobs.Count;
            }
        }
    }

    // ------------------------------------------------------------------ 公共 API（Router 调用，均快速返回）

    /// <summary>
    /// <c>runBatch</c>：校验 kind/args/目标 → 展开 componentId × repoType → 入队后台作业（仅受理，不等待执行）。
    /// <para>作业开始执行时会 read-through 刷新快照；受理阶段不做任何 git 写。</para>
    /// </summary>
    public CommandResult RunBatch(RunBatchParams parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        EnsureLoaded();

        var validation = TryBuildBatchPlan(
            parameters,
            preSkipItems: true,
            out var kind,
            out var arguments,
            out var items,
            out var preSkipped);
        if (validation is not null)
        {
            return validation;
        }

        var job = CreateJob(kind, arguments, items, actor: Environment.UserName, requestId: Guid.NewGuid().ToString("N"));
        Enqueue(job);
        Publish(job);
        HostLog.Info(
            $"runBatch 受理：job={job.Id} kind={kind} items={job.Items.Count} skipped={preSkipped}");
        return CommandResult.Success(new RunBatchResultDto(job.Id, true, job.Items.Count, preSkipped));
    }

    /// <summary><c>getJobs</c>：返回最近作业（创建时间倒序，limit 默认 50 / 上限 200）。</summary>
    public CommandResult GetJobs(GetJobsParams parameters)
    {
        EnsureLoaded();
        var limit = parameters?.Limit ?? DefaultGetJobsLimit;
        if (limit < 1 || limit > MaxRetainedJobs)
        {
            return CommandResult.Failure(
                BridgeErrorCodes.InvalidArgument,
                $"参数 limit 必须在 1–{MaxRetainedJobs} 之间",
                new Dictionary<string, object?> { ["field"] = "limit", ["limit"] = parameters?.Limit });
        }

        List<JobDto> jobs;
        lock (_sync)
        {
            jobs = new List<JobDto>(Math.Min(limit, _jobs.Count));
            for (var index = _jobs.Count - 1; index >= 0 && jobs.Count < limit; index--)
            {
                jobs.Add(ToDto(_jobs[index]));
            }
        }

        return CommandResult.Success(new JobsDto(jobs));
    }

    /// <summary><c>getJob</c>：按 jobId 返回作业快照（含逐仓 item 状态/HEAD/backupRef）。</summary>
    public CommandResult GetJob(GetJobParams parameters)
    {
        EnsureLoaded();
        var jobId = parameters?.JobId?.Trim();
        if (string.IsNullOrEmpty(jobId))
        {
            return CommandResult.Failure(
                BridgeErrorCodes.InvalidArgument,
                "缺少必填参数 jobId",
                new Dictionary<string, object?> { ["field"] = "jobId" });
        }

        JobDto dto;
        lock (_sync)
        {
            var job = FindJobUnsafe(jobId);
            if (job is null)
            {
                return JobNotFound(jobId);
            }

            dto = ToDto(job);
        }

        return CommandResult.Success(dto);
    }

    /// <summary>
    /// <c>cancelJob</c>：只允许取消 PENDING/RUNNING 作业；已完成作业返回 <c>E_ACTION_NOT_ALLOWED</c>。
    /// <para>PENDING 立即标 <c>CANCELED</c>：全部未开始 item 记 <c>CANCELED</c> 且 <c>errorCode=null</c>
    /// （取消不等于超时，不写 <c>E_TIMEOUT</c>），逐 item 补写审计行；</para>
    /// <para>RUNNING：置 <c>cancelRequested</c> 并取消 token（GitCli 收到后杀进程树），worker 把当前 item 记
    /// <c>CANCELED</c>、未开始 item 记 <c>CANCELED</c>（errorCode=null），终态 job=CANCELED。</para>
    /// </summary>
    public CommandResult CancelJob(CancelJobParams parameters)
    {
        EnsureLoaded();
        var jobId = parameters?.JobId?.Trim();
        if (string.IsNullOrEmpty(jobId))
        {
            return CommandResult.Failure(
                BridgeErrorCodes.InvalidArgument,
                "缺少必填参数 jobId",
                new Dictionary<string, object?> { ["field"] = "jobId" });
        }

        JobRecord? job;
        CancelJobResultDto result;
        var jobFinalizedByCancel = false;
        lock (_sync)
        {
            job = FindJobUnsafe(jobId);
            if (job is null)
            {
                return JobNotFound(jobId);
            }

            if (job.State is not (JobStates.Pending or JobStates.Running))
            {
                return CommandResult.Failure(
                    BridgeErrorCodes.ActionNotAllowed,
                    $"作业已结束（state={job.State}），只有 PENDING/RUNNING 可取消",
                    new Dictionary<string, object?>
                    {
                        ["jobId"] = job.Id,
                        ["state"] = job.State,
                        ["cancellable"] = false
                    });
            }

            job.CancelRequested = true;
            if (job.State == JobStates.Pending)
            {
                job.State = JobStates.Canceled;
                job.FinishedAt = Now();
                CancelPendingItemsUnsafe(job, "作业取消：未开始执行");
                PersistUnsafe();

                // 每 item 终态恰 1 行审计（PENDING 取消路径原先漏写）。
                WriteMissingItemAuditsUnsafe(job);
                jobFinalizedByCancel = true;
            }
            else
            {
                // 合作式取消：GitCli 收到取消后杀进程树；OCE 由 worker 记 CANCELED。
                try
                {
                    job.Cts?.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // 作业刚结束：忽略。
                }
            }

            result = new CancelJobResultDto(job.Id, job.State);
        }

        Publish(job);
        if (jobFinalizedByCancel)
        {
            _ = Task.Run(() => InvokeJobFinished(), CancellationToken.None);
        }

        HostLog.Info($"cancelJob：job={job.Id} state={result.State}");
        return CommandResult.Success(result);
    }

    /// <summary>
    /// <c>retryFailed</c>：用原作业的 FAILED/INTERRUPTED/CANCELED items 创建新 job（同一 kind/args）。
    /// <para>原作业必须已结束；无可重试项时不创建空作业（jobId=null, accepted=false）。</para>
    /// </summary>
    public CommandResult RetryFailed(RetryFailedParams parameters)
    {
        EnsureLoaded();
        var jobId = parameters?.JobId?.Trim();
        if (string.IsNullOrEmpty(jobId))
        {
            return CommandResult.Failure(
                BridgeErrorCodes.InvalidArgument,
                "缺少必填参数 jobId",
                new Dictionary<string, object?> { ["field"] = "jobId" });
        }

        JobRecord? original;
        List<JobItemRecord> retryItems;
        lock (_sync)
        {
            original = FindJobUnsafe(jobId);
            if (original is null)
            {
                return JobNotFound(jobId);
            }

            if (original.State is JobStates.Pending or JobStates.Running)
            {
                return CommandResult.Failure(
                    BridgeErrorCodes.ActionNotAllowed,
                    "作业尚未结束，不能重试；请等待完成或先取消",
                    new Dictionary<string, object?> { ["jobId"] = original.Id, ["state"] = original.State });
            }

            retryItems = original.Items
                .Where(item => item.State is JobItemStates.Failed or JobItemStates.Interrupted or JobItemStates.Canceled)
                .Select((item, index) => new JobItemRecord
                {
                    Id = original.Id + ":retry:" + (index + 1).ToString(CultureInfo.InvariantCulture),
                    ComponentId = item.ComponentId,
                    RepoType = item.RepoType,
                    RepoName = item.RepoName,
                    State = JobItemStates.Pending
                })
                .ToList();
        }

        if (retryItems.Count == 0)
        {
            return CommandResult.Success(new RetryFailedResultDto(null, false, 0));
        }

        var job = CreateJob(
            original.Kind,
            original.Arguments,
            retryItems,
            actor: Environment.UserName,
            requestId: Guid.NewGuid().ToString("N"));
        Enqueue(job);
        Publish(job);
        HostLog.Info($"retryFailed：原 job={original.Id} → 新 job={job.Id} items={job.Items.Count}");
        return CommandResult.Success(new RetryFailedResultDto(job.Id, true, job.Items.Count));
    }

    // ------------------------------------------------------------------ worker / 执行

    /// <summary>
    /// 启动 journal reconcile 结果回写任务中心（FR-SAFE-04；仅在 journal 条目关联 jobId/jobItemId 时）：
    /// 更新对应 item 的 message（不改变终态、不重跑、不修改仓库），落盘并推送 JOB_UPDATED。
    /// </summary>
    public void AnnotateReconcile(string? jobId, string? jobItemId, string? status)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            return;
        }

        var detail = string.Equals(status, JournalReconciler.NotApplied, StringComparison.Ordinal)
            ? "NOT_APPLIED（写前 HEAD 未变化，可安全重试）"
            : "UNKNOWN_APPLIED（写后 HEAD 已变化，请人工核对）";

        EnsureLoaded();
        JobRecord? job;
        lock (_sync)
        {
            job = FindJobUnsafe(jobId!);
            if (job is null)
            {
                return;
            }

            var item = job.Items.FirstOrDefault(entry =>
                string.Equals(entry.Id, jobItemId, StringComparison.Ordinal));
            if (item is null)
            {
                return;
            }

            var prefix = string.IsNullOrWhiteSpace(item.Message) ? string.Empty : item.Message + "；";
            item.Message = Truncate(prefix + "journal reconcile: " + detail, 500);
            PersistUnsafe();
        }

        Publish(job);
    }

    private async Task WorkerLoopAsync()
    {
        try
        {
            while (await _queue.Reader.WaitToReadAsync(_lifetime.Token).ConfigureAwait(false))
            {
                while (_queue.Reader.TryRead(out var job))
                {
                    if (_lifetime.IsCancellationRequested)
                    {
                        MarkInterruptedOnShutdown(job);
                        continue;
                    }

                    await ExecuteJobAsync(job).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 正常退出（宿主关闭）。
        }
        catch (Exception ex)
        {
            HostLog.Error($"作业 worker 异常退出：{ex.GetType().Name}");
        }
    }

    private async Task ExecuteJobAsync(JobRecord job)
    {
        CancellationTokenSource? jobCts = null;
        try
        {
            lock (_sync)
            {
                if (job.State is not JobStates.Pending || _disposed)
                {
                    // 已被 cancelJob 标 CANCELED / 宿主关闭：不执行（绝不重复执行）。
                    if (job.State is JobStates.Pending)
                    {
                        MarkInterruptedUnsafe(job, "宿主关闭：未开始执行", "宿主关闭：执行中断");
                        PersistUnsafe();
                        WriteMissingItemAuditsUnsafe(job);
                    }

                    return;
                }

                job.State = JobStates.Running;
                job.StartedAt = Now();
                jobCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
                job.Cts = jobCts;
                PersistUnsafe();
            }

            Publish(job);

            // read-through：作业开始前刷新快照（矩阵/本地路径/HEAD 基于动作前状态；避免 TTL 旧快照）。
            var index = await RefreshIndexAsync(jobCts.Token).ConfigureAwait(false);

            foreach (var item in job.Items)
            {
                if (jobCts.Token.IsCancellationRequested)
                {
                    CancelRemainingItems(job);
                    break;
                }

                if (item.State is not JobItemStates.Pending)
                {
                    continue;
                }

                await ExecuteItemAsync(job, item, index, jobCts.Token).ConfigureAwait(false);
            }

            FinalizeJob(job);
        }
        catch (OperationCanceledException)
        {
            lock (_sync)
            {
                CancelRemainingItemsUnsafe(job, "作业取消：未开始执行");
                FinalizeJobUnsafe(job);
                PersistUnsafe();
            }

            Publish(job);
        }
        catch (Exception ex)
        {
            HostLog.Error($"作业执行异常：job={job.Id} {ex.GetType().Name}");
            lock (_sync)
            {
                foreach (var item in job.Items.Where(entry => entry.State is JobItemStates.Pending or JobItemStates.Running))
                {
                    item.State = JobItemStates.Failed;
                    item.ErrorCode = BridgeErrorCodes.Internal;
                    item.Message = "内部错误（" + ex.GetType().Name + "）";
                    item.FinishedAt ??= Now();
                }

                job.CancelRequested = false;
                FinalizeJobUnsafe(job);
                PersistUnsafe();
            }

            Publish(job);
        }
        finally
        {
            lock (_sync)
            {
                job.Cts = null;
            }

            jobCts?.Dispose();
            try
            {
                _snapshots.Invalidate();
            }
            catch (ObjectDisposedException)
            {
                // 快照服务已销毁（宿主关闭）：忽略。
            }

            InvokeJobFinished();
        }
    }

    private async Task<SnapshotIndex> RefreshIndexAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _snapshots.RefreshAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // 扫描失败不终止作业：沿用最近一次索引，逐仓解析失败记 SKIPPED。
            HostLog.Warn($"作业前刷新快照失败（{ex.GetType().Name}）：沿用最近索引");
        }

        return _snapshots.Index;
    }

    private async Task ExecuteItemAsync(
        JobRecord job,
        JobItemRecord item,
        SnapshotIndex index,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var repoState = "UNKNOWN";

        lock (_sync)
        {
            item.State = JobItemStates.Running;
            item.StartedAt = Now();

            // 不在此处落盘：即使进程崩溃，下次加载会把 RUNNING item 标 INTERRUPTED（未开始 PENDING 标 CANCELED，
            // 不重复执行），状态语义不变；避免大批量作业每个 item 都重写整份 jobs.json。
        }

        Publish(job);

        index.TryGetRepo(item.ComponentId, item.RepoType, out var repo);
        BatchItemResult result;
        try
        {
            RepoActionExecution? execution = null;
            var lockPath = ResolveRepoLockPath(job, item, repo);
            if (lockPath is null || job.Arguments.DryRun)
            {
                (result, execution, repoState) = await ExecuteByKindAsync(
                        job,
                        item,
                        repo,
                        index,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                // P1-3：批量自定义 kind 与单仓 M2a 写操作复用同一把 per-repo 锁（同仓串行、异仓并发）。
                using var lease = await RepoActionService
                    .AcquireRepoLockAsync(lockPath, cancellationToken)
                    .ConfigureAwait(false);
                (result, execution, repoState) = await ExecuteByKindAsync(
                        job,
                        item,
                        repo,
                        index,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            if (execution is not null)
            {
                // 以真实 git 执行摘要为准（pre/post HEAD 含失败时兜底）。
                result = result with
                {
                    PreHead = execution.PreHead ?? result.PreHead,
                    PostHead = execution.PostHead ?? result.PostHead
                };
            }
        }
        catch (OperationCanceledException)
        {
            // 取消 ≠ 失败/超时：item 记 CANCELED 且 errorCode=null（不写 E_TIMEOUT）。
            result = new BatchItemResult(
                JobItemStates.Canceled,
                null,
                "已取消（当前 git 进程已终止）");
        }
        catch (Exception ex)
        {
            HostLog.Error($"job item 执行异常：{ex.GetType().Name}");
            result = new BatchItemResult(
                JobItemStates.Failed,
                BridgeErrorCodes.Internal,
                "内部错误（" + ex.GetType().Name + "）");
        }

        stopwatch.Stop();

        lock (_sync)
        {
            item.State = result.State;
            item.FinishedAt = Now();
            item.ErrorCode = Truncate(result.ErrorCode, 128);
            item.Message = Truncate(result.Message, 500);
            item.PreHead = result.PreHead;
            item.PostHead = result.PostHead;
            item.BackupRef = Truncate(result.BackupRef, 256);
            item.Preview = result.State == JobItemStates.Preview;
            item.RepoState = repoState;
            if (result.State == JobItemStates.Canceled)
            {
                job.CancelRequested = true;
            }

            PersistUnsafe();

            // 已执行 item 的终态审计只在这里写 1 行（AuditWritten 保证不重复）。
            WriteItemAuditUnsafe(job, item, repoState, result, stopwatch.ElapsedMilliseconds);
        }

        Publish(job);

        if (result.State == JobItemStates.Canceled)
        {
            CancelRemainingItems(job);
            Publish(job);
        }

        HostLog.Info(
            $"job item：job={job.Id} {item.ComponentId}/{item.RepoType} kind={job.Kind} "
            + $"state={result.State} code={result.ErrorCode ?? "<none>"} ms={stopwatch.ElapsedMilliseconds}");
    }

    /// <summary>
    /// item 执行时使用的 per-repo 锁路径：单仓五键由 <see cref="RepoActionService.RunAsync"/> 内部加同一把锁，
    /// 这里不重复获取（避免自锁）；其余批量 kind 返回 canonical localPath / 目标路径。
    /// </summary>
    private string? ResolveRepoLockPath(JobRecord job, JobItemRecord item, RepoRef? repo)
    {
        if (RepoActionKinds.Contains(job.Kind, StringComparer.Ordinal))
        {
            return null;
        }

        if (repo is not null && repo.Repo.IsLocal && !string.IsNullOrWhiteSpace(repo.Repo.LocalPath))
        {
            return PathPolicy.TryCanonicalize(repo.Repo.LocalPath);
        }

        if (job.Kind is "CLONE" or "RESTORE" && _paths is not null)
        {
            var name = BatchOperationService.RepoNameFor(repo, item.ComponentId, item.RepoType);
            return _paths.TryResolveWorkspacePath(name, out var full, out _) ? full : null;
        }

        return null;
    }

    private async Task<(BatchItemResult Result, RepoActionExecution? Execution, string RepoState)> ExecuteByKindAsync(
        JobRecord job,
        JobItemRecord item,
        RepoRef? repo,
        SnapshotIndex index,
        CancellationToken cancellationToken)
    {
        var repoState = string.IsNullOrWhiteSpace(repo?.Repo.State) ? "UNKNOWN" : repo!.Repo.State;
        var arguments = job.Arguments;

        // 通用前置：仓库解析（CLONE/RESTORE 允许缺失仓，用推导名继续）。
        if (repo is not null)
        {
            item.RepoName = BatchOperationService.RepoNameFor(repo, item.ComponentId, item.RepoType);
        }
        else if (job.Kind is "CLONE" or "RESTORE")
        {
            item.RepoName = BatchOperationService.RepoNameFor(null, item.ComponentId, item.RepoType);
        }

        if (arguments.DryRun)
        {
            // dryRun：只做逐仓只读预检（状态矩阵/本地/分支/upstream/dirty/untracked/unpushed/stash），
            // 不 fetch / 不建 ref / 不 stash / 不 reset / 不移动目录；item 记 PREVIEW。
            var preview = await _batch
                .PreviewAsync(job.Kind, repo, item.ComponentId, item.RepoType, arguments, cancellationToken)
                .ConfigureAwait(false);
            return (preview, null, repoState);
        }

        if (RepoActionKinds.Contains(job.Kind, StringComparer.Ordinal))
        {
            return await ExecuteRepoActionKindAsync(job, item, repo, index, cancellationToken)
                .ConfigureAwait(false);
        }

        // FR-SAFE-04 / ADR D-026 M2b-2b：批量自定义 kind（SYNC/CLONE/DELETE/RESTORE/CREATE/TAG）在
        // git 写前由 JobEngine 落 journal STARTED（含 preHead/localPath/job 关联）；成功 DONE、失败/取消/
        // 异常 FAILED。单仓五键由 RepoActionService 写，避免重复。
        var journalEntry = _journal?.Begin(
            job.Kind,
            item.ComponentId,
            item.RepoType,
            await ReadJournalHeadAsync(repo, cancellationToken).ConfigureAwait(false),
            ResolveJournalPath(repo, item, job),
            job.Id,
            item.Id);

        try
        {
            var result = job.Kind switch
            {
                "SYNC" => await _batch.SyncAsync(repo, item.ComponentId, item.RepoType, arguments, cancellationToken)
                    .ConfigureAwait(false),
                "CLONE" => await _batch.CloneAsync(repo, item.ComponentId, item.RepoType, cancellationToken)
                    .ConfigureAwait(false),
                "DELETE" => await _batch.DeleteAsync(repo, item.ComponentId, item.RepoType, arguments, cancellationToken)
                    .ConfigureAwait(false),
                "RESTORE" => await _batch.RestoreAsync(repo, item.ComponentId, item.RepoType, arguments, cancellationToken)
                    .ConfigureAwait(false),
                "CREATE" => await _batch.CreateBranchAsync(repo, item.RepoType, arguments, cancellationToken)
                    .ConfigureAwait(false),
                "TAG" => await _batch.TagAsync(repo, item.RepoType, arguments, cancellationToken).ConfigureAwait(false),
                _ => new BatchItemResult(
                    JobItemStates.Failed,
                    BridgeErrorCodes.InvalidArgument,
                    $"不支持的 kind：{job.Kind}")
            };

            if (result.State == JobItemStates.Ok)
            {
                _journal?.Complete(journalEntry, result.PostHead);
            }
            else
            {
                _journal?.Fail(journalEntry, result.ErrorCode ?? result.State);
            }

            if (result.State == JobItemStates.Ok && result.PostHead is null)
            {
                item.RepoName ??= BatchOperationService.RepoNameFor(repo, item.ComponentId, item.RepoType);
            }

            return (result, null, repoState);
        }
        catch (OperationCanceledException)
        {
            _journal?.Fail(journalEntry, BridgeErrorCodes.Timeout);
            throw;
        }
        catch
        {
            _journal?.Fail(journalEntry, BridgeErrorCodes.Internal);
            throw;
        }
    }

    /// <summary>批次 kind 的写前 HEAD（只读；仓不存在/未克隆返回 null = 无 HEAD 可比较）。</summary>
    private async Task<string?> ReadJournalHeadAsync(RepoRef? repo, CancellationToken cancellationToken)
    {
        if (repo is null || !repo.Repo.IsLocal || string.IsNullOrWhiteSpace(repo.Repo.LocalPath))
        {
            return null;
        }

        try
        {
            var result = await _git
                .RunAsync(repo.Repo.LocalPath, new[] { "rev-parse", "HEAD" }, cancellationToken)
                .ConfigureAwait(false);
            if (!result.Success)
            {
                return null; // EMPTY（unborn HEAD）等：preHead 允许为空。
            }

            var head = result.TrimmedStdout;
            return head.Length is > 0 and <= 64 ? head : null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or InvalidOperationException)
        {
            HostLog.Warn($"job item 写前 HEAD 读取失败（{ex.GetType().Name}）；journal preHead=null");
            return null;
        }
    }

    /// <summary>批次 kind 的 journal 目标路径（日志/回退用；CLONE/RESTORE 目标可尚未存在）。</summary>
    private string? ResolveJournalPath(RepoRef? repo, JobItemRecord item, JobRecord job)
    {
        if (repo is not null && repo.Repo.IsLocal && !string.IsNullOrWhiteSpace(repo.Repo.LocalPath))
        {
            return repo.Repo.LocalPath;
        }

        if (job.Kind is "CLONE" or "RESTORE" && _paths is not null)
        {
            var name = BatchOperationService.RepoNameFor(repo, item.ComponentId, item.RepoType);
            return _paths.TryResolveWorkspacePath(name, out var full, out _) ? full : null;
        }

        return null;
    }

    private async Task<(BatchItemResult Result, RepoActionExecution? Execution, string RepoState)>
        ExecuteRepoActionKindAsync(
            JobRecord job,
            JobItemRecord item,
            RepoRef? repo,
            SnapshotIndex index,
            CancellationToken cancellationToken)
    {
        if (repo is null)
        {
            return (
                new BatchItemResult(
                    JobItemStates.Skipped,
                    BridgeErrorCodes.NotFound,
                    "仓库不在当前快照，跳过 " + job.Kind),
                null,
                "UNKNOWN");
        }

        var repoState = string.IsNullOrWhiteSpace(repo.Repo.State) ? "UNKNOWN" : repo.Repo.State;
        if (!repo.Repo.IsLocal || string.IsNullOrWhiteSpace(repo.Repo.LocalPath))
        {
            return (
                new BatchItemResult(
                    JobItemStates.Skipped,
                    BridgeErrorCodes.RepoNotLocal,
                    $"仓库未克隆：{BatchOperationService.RepoNameFor(repo, item.ComponentId, item.RepoType)}，跳过 {job.Kind}"),
                null,
                repoState);
        }

        // §7.6 矩阵单源：不允许的 cell 记 SKIPPED + 原因（不失败整个 job）。
        var availability = _actions.GetAvailability(repo).FirstOrDefault(entry => entry.Action == job.Kind);
        if (availability is null || !availability.MatrixAllowed)
        {
            return (
                new BatchItemResult(
                    JobItemStates.Skipped,
                    BridgeErrorCodes.ActionNotAllowed,
                    availability?.Reason ?? $"当前状态 {repoState} 不允许 {job.Kind}"),
                null,
                repoState);
        }

        var parameters = new RunRepoActionParams
        {
            ComponentId = item.ComponentId,
            RepoType = item.RepoType,
            Action = job.Kind,
            Args = BuildRepoActionArgs(job.Arguments)
        };

        RepoActionExecution? execution = null;
        var commandResult = await _actions
            .RunAsync(
                parameters,
                index,
                cancellationToken,
                captured => execution = captured,
                jobId: job.Id,
                jobItemId: item.Id)
            .ConfigureAwait(false);

        if (commandResult.Ok)
        {
            return (
                new BatchItemResult(
                    JobItemStates.Ok,
                    null,
                    $"{job.Kind} 完成",
                    execution?.PreHead,
                    execution?.PostHead,
                    null),
                execution,
                repoState);
        }

        return (
            new BatchItemResult(
                JobItemStates.Failed,
                commandResult.Error?.Code ?? BridgeErrorCodes.GitFailed,
                commandResult.Error?.Message ?? (job.Kind + " 失败"),
                execution?.PreHead,
                execution?.PostHead,
                null),
            execution,
            repoState);
    }

    /// <summary>dry-run 预检（只读；不执行任何 git 写/网络操作）。</summary>
    private static string? PreviewDryRun(string kind, RepoRef? repo, string repoType)
    {
        if (repo is null && kind is not ("CLONE" or "RESTORE"))
        {
            return "DRY_RUN：仓库不在当前快照，实际执行将 SKIPPED";
        }

        if (kind is "FETCH" or "PULL" or "CHKOUT" or "COMMIT" or "PUSH" or "SYNC" or "DELETE" or "CREATE" or "TAG")
        {
            if (repo is not null && !repo.Repo.IsLocal)
            {
                return $"DRY_RUN：仓库未克隆（{BatchOperationService.RepoNameFor(repo, repo.ComponentId, repoType)}），"
                    + "实际执行将 SKIPPED";
            }
        }

        if (kind is "SYNC" or "DELETE" or "CREATE" or "TAG"
            && repo is not null
            && string.Equals(repo.Repo.State, "IN_PROGRESS", StringComparison.OrdinalIgnoreCase))
        {
            return "DRY_RUN：仓库存在进行中操作，实际执行将 SKIPPED";
        }

        return null;
    }

    // ------------------------------------------------------------------ 状态推进

    private void CancelRemainingItems(JobRecord job)
    {
        lock (_sync)
        {
            CancelRemainingItemsUnsafe(job, "作业取消：未开始执行");
            PersistUnsafe();
        }
    }

    /// <summary>取消未开始（PENDING）item：记 CANCELED 且 errorCode=null（不写 E_TIMEOUT，取消≠超时）。</summary>
    private static void CancelRemainingItemsUnsafe(JobRecord job, string message) =>
        CancelPendingItemsUnsafe(job, message);

    private static void CancelPendingItemsUnsafe(JobRecord job, string message)
    {
        foreach (var item in job.Items.Where(entry => entry.State == JobItemStates.Pending))
        {
            item.State = JobItemStates.Canceled;
            item.ErrorCode = null;
            item.Message = message;
            item.FinishedAt = Now();
        }
    }

    private void FinalizeJob(JobRecord job)
    {
        lock (_sync)
        {
            FinalizeJobUnsafe(job);
            PersistUnsafe();
        }

        Publish(job);
    }

    /// <summary>
    /// 终态归并（幂等）：dryRun → PREVIEW；有 INTERRUPTED item → INTERRUPTED；有 CANCELED item → CANCELED；
    /// 否则按 ok/failed 计 SUCCESS/PARTIAL/FAILED。无论走哪条路径，都为尚未写审计的终态 item 补写恰好 1 行。
    /// </summary>
    private void FinalizeJobUnsafe(JobRecord job)
    {
        if (!IsTerminal(job.State))
        {
            job.FinishedAt = Now();
            if (job.Arguments.DryRun)
            {
                job.State = JobStates.Preview;
            }
            else if (job.Items.Any(item => item.State == JobItemStates.Interrupted))
            {
                job.State = JobStates.Interrupted;
            }
            else if (job.Items.Any(item => item.State == JobItemStates.Canceled))
            {
                job.State = JobStates.Canceled;
                job.CancelRequested = true;
            }
            else
            {
                var ok = job.Items.Count(item => item.State == JobItemStates.Ok);
                var failed = job.Items.Count(item => item.State == JobItemStates.Failed);
                job.State = failed == 0
                    ? JobStates.Success
                    : ok > 0
                        ? JobStates.Partial
                        : JobStates.Failed;
            }
        }

        WriteMissingItemAuditsUnsafe(job);
    }

    private void MarkInterruptedOnShutdown(JobRecord job)
    {
        lock (_sync)
        {
            MarkInterruptedUnsafe(job, "宿主关闭：未开始执行", "宿主关闭：执行中断");
            PersistUnsafe();
            WriteMissingItemAuditsUnsafe(job);
        }

        Publish(job);
    }

    /// <summary>
    /// 重启/关闭语义（AC-OP-26① 修正）：只有真正执行中的 RUNNING item 标 INTERRUPTED；
    /// 未开始的 PENDING item 记 CANCELED（reason=进程重启/关闭未执行），绝不误标 INTERRUPTED 或重复执行；
    /// job：曾 RUNNING（或有 RUNNING item）→ INTERRUPTED，纯排队未开始 → CANCELED。
    /// </summary>
    private static void MarkInterruptedUnsafe(JobRecord job, string pendingMessage, string runningMessage)
    {
        if (!IsTerminal(job.State))
        {
            var wasRunning = job.State == JobStates.Running
                || job.Items.Any(item => item.State == JobItemStates.Running);
            job.State = wasRunning ? JobStates.Interrupted : JobStates.Canceled;
            job.FinishedAt ??= Now();
            if (job.State == JobStates.Canceled)
            {
                job.CancelRequested = true;
            }
        }

        foreach (var item in job.Items)
        {
            if (item.State == JobItemStates.Running)
            {
                item.State = JobItemStates.Interrupted;
                item.Message = runningMessage;
                item.FinishedAt ??= Now();
            }
            else if (item.State == JobItemStates.Pending)
            {
                item.State = JobItemStates.Canceled;
                item.ErrorCode = null;
                item.Message = pendingMessage;
                item.FinishedAt ??= Now();
            }
        }
    }

    private static bool IsTerminal(string state) => state is
        JobStates.Partial or JobStates.Success or JobStates.Failed or JobStates.Canceled
        or JobStates.Interrupted or JobStates.Preview;

    private static bool IsItemTerminal(string state) => state is
        JobItemStates.Ok or JobItemStates.Failed or JobItemStates.Skipped
        or JobItemStates.Canceled or JobItemStates.Interrupted or JobItemStates.Preview;

    // ------------------------------------------------------------------ 失效处理 / 快照

    private SnapshotIndex RefreshIndexSafe()
    {
        try
        {
            return _snapshots.Index;
        }
        catch (ObjectDisposedException)
        {
            return SnapshotIndex.Empty;
        }
    }

    private void InvokeJobFinished()
    {
        if (_onJobFinished is null)
        {
            return;
        }

        try
        {
            _onJobFinished();
        }
        catch (Exception ex)
        {
            HostLog.Warn($"作业完成回调异常：{ex.GetType().Name}");
        }
    }

    // ------------------------------------------------------------------ 审计

    /// <summary>
    /// 为尚未写审计的终态 item 补写恰好 1 行（覆盖：受理阶段预跳过、取消未开始、job 级内部异常、
    /// 重启改写等“未进入 ExecuteItemAsync”的路径）。<c>AuditWritten</c> 标记保证每 item 恰 1 行。
    /// </summary>
    private void WriteMissingItemAuditsUnsafe(JobRecord job)
    {
        foreach (var item in job.Items)
        {
            if (item.AuditWritten || !IsItemTerminal(item.State))
            {
                continue;
            }

            var result = new BatchItemResult(
                item.State,
                item.ErrorCode,
                item.Message,
                item.PreHead,
                item.PostHead,
                item.BackupRef);
            WriteItemAuditUnsafe(
                job,
                item,
                string.IsNullOrWhiteSpace(item.RepoState) ? "UNKNOWN" : item.RepoState!,
                result,
                durationMs: 0);
        }
    }

    private void WriteItemAuditUnsafe(
        JobRecord job,
        JobItemRecord item,
        string repoState,
        BatchItemResult result,
        long durationMs)
    {
        if (item.AuditWritten)
        {
            return;
        }

        // 先置位再写：即使审计 IO 失败也不重复写（“恰 1 行”由 Result/JobItemId 唯一性保证）。
        item.AuditWritten = true;
        try
        {
            _audit.Record(
                action: job.Kind,
                componentId: item.ComponentId,
                repoType: item.RepoType,
                state: repoState,
                preHead: result.PreHead,
                postHead: result.PostHead,
                ok: result.State == JobItemStates.Ok,
                errorCode: result.ErrorCode,
                durationMs: durationMs,
                actor: job.Actor,
                requestId: job.RequestId,
                jobId: job.Id,
                jobItemId: item.Id,
                result: result.State,
                backupRef: result.BackupRef);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // 审计是旁路能力：失败不影响作业（OperationAuditService 内部已兜底，这里只防意外）。
            HostLog.Warn($"作业审计写入失败（{ex.GetType().Name}）");
        }
    }

    // ------------------------------------------------------------------ 参数/计划校验

    private CommandResult? TryBuildBatchPlan(
        RunBatchParams parameters,
        bool preSkipItems,
        out string kind,
        out StoredJobArguments arguments,
        out List<JobItemRecord> items,
        out int preSkipped)
    {
        kind = string.Empty;
        arguments = DefaultArguments();
        items = new List<JobItemRecord>();
        preSkipped = 0;

        var kindText = parameters.Kind?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(kindText) || !KindWhitelist.Contains(kindText, StringComparer.Ordinal))
        {
            return CommandResult.Failure(
                BridgeErrorCodes.InvalidArgument,
                "参数 kind 必须是 " + string.Join('/', KindWhitelist),
                new Dictionary<string, object?> { ["field"] = "kind", ["kind"] = parameters.Kind });
        }

        var argumentError = TryParseArguments(parameters.Args, out arguments);
        if (argumentError is not null)
        {
            return CommandResult.Failure(argumentError);
        }

        var requiredArgumentError = ValidateKindArguments(kindText, arguments);
        if (requiredArgumentError is not null)
        {
            return CommandResult.Failure(requiredArgumentError);
        }

        if (!arguments.DryRun
            && ConfirmRequiredKinds.Contains(kindText, StringComparer.Ordinal)
            && !arguments.Confirm)
        {
            return CommandResult.Failure(
                BridgeErrorCodes.ConfirmRequired,
                $"{kindText} 属强写操作：批量入口二次确认后必须在 args 传入 confirm=true 才执行",
                new Dictionary<string, object?>
                {
                    ["kind"] = kindText,
                    ["requiresConfirm"] = true
                });
        }

        // DELETE 的第二重确认独立于 confirm：必须携带 args.confirmText（UI 输入组件 ID）。
        if (!arguments.DryRun
            && kindText == "DELETE"
            && string.IsNullOrWhiteSpace(arguments.ConfirmText))
        {
            return CommandResult.Failure(
                BridgeErrorCodes.ConfirmRequired,
                "DELETE 需强确认：args.confirmText 必须等于要删除的组件 ID（UI 输入组件 ID；精确匹配、区分大小写）",
                new Dictionary<string, object?>
                {
                    ["kind"] = kindText,
                    ["requiresConfirm"] = true,
                    ["requiresTypedConfirm"] = true
                });
        }

        if (parameters.ComponentIds is null || parameters.ComponentIds.Count == 0)
        {
            return CommandResult.Failure(
                BridgeErrorCodes.InvalidArgument,
                "参数 componentIds 不能为空（批量只对显式选择集合生效，禁止隐式全量）",
                new Dictionary<string, object?> { ["field"] = "componentIds" });
        }

        var componentIds = new List<string>();
        foreach (var componentId in parameters.ComponentIds)
        {
            var trimmed = componentId?.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                return CommandResult.Failure(
                    BridgeErrorCodes.InvalidArgument,
                    "componentIds 不能包含空值",
                    new Dictionary<string, object?> { ["field"] = "componentIds" });
            }

            if (!componentIds.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
            {
                componentIds.Add(trimmed);
            }
        }

        if (componentIds.Count > MaxBatchComponents)
        {
            return CommandResult.Failure(
                BridgeErrorCodes.InvalidArgument,
                $"单次批量组件数超过上限（{MaxBatchComponents}）",
                new Dictionary<string, object?> { ["field"] = "componentIds", ["count"] = componentIds.Count });
        }

        var repoTypes = ResolveRepoTypes(parameters.RepoTypes, out var repoTypeError);
        if (repoTypeError is not null)
        {
            return CommandResult.Failure(repoTypeError);
        }

        var index = RefreshIndexSafe();
        var hasSnapshot = index.Snapshot is not null;
        var sequence = 0;
        foreach (var componentId in componentIds)
        {
            foreach (var repoType in repoTypes)
            {
                sequence++;
                var item = new JobItemRecord
                {
                    Id = string.Empty,
                    ComponentId = componentId,
                    RepoType = repoType,
                    State = JobItemStates.Pending
                };
                item.Id = "pending:" + sequence.ToString(CultureInfo.InvariantCulture);

                if (preSkipItems && !arguments.DryRun && hasSnapshot)
                {
                    var found = index.TryGetRepo(componentId, repoType, out var repo);
                    if (found)
                    {
                        item.RepoState = repo.Repo.State;
                    }

                    if (found && TryPreviewSkip(kindText, repo, componentId, repoType, out var skipCode, out var skipMessage))
                    {
                        item.State = JobItemStates.Skipped;
                        item.ErrorCode = skipCode;
                        item.Message = skipMessage;
                        item.RepoName = BatchOperationService.RepoNameFor(repo, componentId, repoType);
                        item.FinishedAt = Now();
                        preSkipped++;
                    }
                    else if (!found && kindText is not ("CLONE" or "RESTORE"))
                    {
                        // 组件/仓不在当前快照：执行阶段同样会 SKIPPED（预跳过不改变“不失败整个 job”语义）。
                        item.State = JobItemStates.Skipped;
                        item.ErrorCode = BridgeErrorCodes.NotFound;
                        item.Message = $"组件/仓不在当前快照：{componentId}_{repoType}，跳过 {kindText}";
                        item.FinishedAt = Now();
                        preSkipped++;
                    }
                }

                items.Add(item);
            }
        }

        if (items.Count > MaxBatchItems)
        {
            return CommandResult.Failure(
                BridgeErrorCodes.InvalidArgument,
                $"单次批量 item 数超过上限（{MaxBatchItems}）",
                new Dictionary<string, object?> { ["field"] = "repoTypes", ["count"] = items.Count });
        }

        kind = kindText;
        return null;
    }

    /// <summary>
    /// 受理阶段的最佳努力预跳过（快照已知时）：CLONE 已存在 / 未克隆仓执行本地动作 / 组件不在快照。
    /// 执行阶段仍会重新判定；<c>accepted=true</c> 不回滚这些预跳过。
    /// </summary>
    private static bool TryPreviewSkip(
        string kind,
        RepoRef repo,
        string componentId,
        string repoType,
        out string? skipCode,
        out string? skipMessage)
    {
        skipCode = null;
        skipMessage = null;
        var repoName = BatchOperationService.RepoNameFor(repo, componentId, repoType);

        if (kind == "CLONE")
        {
            if (repo.Repo.IsLocal)
            {
                skipCode = BridgeErrorCodes.ActionNotAllowed;
                skipMessage = $"目标已存在（{repoName}），跳过 CLONE";
                return true;
            }

            return false;
        }

        if (kind == "RESTORE")
        {
            return false;
        }

        if (!repo.Repo.IsLocal)
        {
            skipCode = BridgeErrorCodes.RepoNotLocal;
            skipMessage = $"仓库未克隆：{repoName}，跳过 {kind}";
            return true;
        }

        return false;
    }

    private static List<string> ResolveRepoTypes(IReadOnlyList<string>? requested, out BridgeError? error)
    {
        error = null;
        var result = new List<string>(4);
        if (requested is null || requested.Count == 0)
        {
            result.AddRange(RepoResolver.RepoTypes);
            return result;
        }

        foreach (var repoType in requested)
        {
            if (!RepoResolver.TryNormalizeRepoType(repoType, out var normalized))
            {
                error = new BridgeError(
                    BridgeErrorCodes.InvalidArgument,
                    "参数 repoTypes 只能包含 DESIGN/IMPL/MIL/SIL",
                    new Dictionary<string, object?> { ["field"] = "repoTypes", ["repoType"] = repoType });
                return result;
            }

            if (!result.Contains(normalized, StringComparer.Ordinal))
            {
                result.Add(normalized);
            }
        }

        return result;
    }

    private static BridgeError? ValidateKindArguments(string kind, StoredJobArguments arguments)
    {
        switch (kind)
        {
            case "CHKOUT":
                if (arguments.Branch is not null && arguments.Tag is not null)
                {
                    return InvalidArgument("CHKOUT 的 branch 与 tag 只能二选一", "branch");
                }

                if (arguments.Branch is null && arguments.Tag is null)
                {
                    return InvalidArgument("CHKOUT 需要 branch 或 tag 参数", "branch");
                }

                break;

            case "COMMIT":
                if (string.IsNullOrWhiteSpace(arguments.Message))
                {
                    return InvalidArgument("COMMIT 需要非空 message 参数", "message");
                }

                break;

            case "CREATE":
                if (arguments.Branch is null)
                {
                    return InvalidArgument("CREATE 需要 branch 参数", "branch");
                }

                break;

            case "TAG":
                if (arguments.Tag is null)
                {
                    return InvalidArgument("TAG 需要 tag 参数", "tag");
                }

                break;

            case "RESTORE":
                if (arguments.TrashStamp is not null && !IsTrashStamp(arguments.TrashStamp))
                {
                    return InvalidArgument("args.trashStamp 必须是 UTC yyyyMMddHHmmss（14 位数字）", "trashStamp");
                }

                break;
        }

        if (arguments.Branch is not null)
        {
            var branchError = RepoActionService.ValidateRefArgument(arguments.Branch, "branch");
            if (branchError is not null)
            {
                return branchError.Error;
            }
        }

        if (arguments.Tag is not null)
        {
            var tagError = RepoActionService.ValidateRefArgument(arguments.Tag, "tag");
            if (tagError is not null)
            {
                return tagError.Error;
            }
        }

        if (arguments.Message is not null && arguments.Message.Length > 2000)
        {
            return InvalidArgument("参数 message 超长（≤2000 字符）", "message");
        }

        if (arguments.ConfirmText is not null && arguments.ConfirmText.Length > 256)
        {
            return InvalidArgument("参数 confirmText 超长（≤256 字符）", "confirmText");
        }

        return null;
    }

    private static BridgeError InvalidArgument(string message, string field) =>
        new(BridgeErrorCodes.InvalidArgument, message, new Dictionary<string, object?> { ["field"] = field });

    private static bool IsTrashStamp(string value) =>
        value.Length == 14 && value.All(character => character is >= '0' and <= '9');

    private static StoredJobArguments DefaultArguments() =>
        new(null, null, null, false, false, false, false, false, false, null, null);

    private static BridgeError? TryParseArguments(
        IReadOnlyDictionary<string, JsonElement>? raw,
        out StoredJobArguments arguments)
    {
        arguments = DefaultArguments();
        if (raw is null || raw.Count == 0)
        {
            return null;
        }

        var error = ReadString(raw, "branch", out var branch);
        if (error is not null)
        {
            return error;
        }

        error = ReadString(raw, "tag", out var tag);
        if (error is not null)
        {
            return error;
        }

        error = ReadString(raw, "message", out var message);
        if (error is not null)
        {
            return error;
        }

        error = ReadString(raw, "trashStamp", out var trashStamp);
        if (error is not null)
        {
            return error;
        }

        error = ReadString(raw, "confirmText", out var confirmText);
        if (error is not null)
        {
            return error;
        }

        error = ReadBool(raw, "confirm", out var confirm);
        if (error is not null)
        {
            return error;
        }

        error = ReadBool(raw, "dryRun", out var dryRun);
        if (error is not null)
        {
            return error;
        }

        error = ReadBool(raw, "backup", out var backup);
        if (error is not null)
        {
            return error;
        }

        error = ReadBool(raw, "setUpstream", out var setUpstream);
        if (error is not null)
        {
            return error;
        }

        error = ReadBool(raw, "stash", out var stash);
        if (error is not null)
        {
            return error;
        }

        error = ReadBool(raw, "stageAll", out var stageAll);
        if (error is not null)
        {
            return error;
        }

        arguments = new StoredJobArguments(
            branch,
            tag,
            message,
            confirm,
            dryRun,
            backup,
            setUpstream,
            stash,
            stageAll,
            trashStamp,
            confirmText);
        return null;
    }

    private static BridgeError? ReadString(
        IReadOnlyDictionary<string, JsonElement> raw,
        string field,
        out string? value)
    {
        value = null;
        if (!TryGetElement(raw, field, out var element) || element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (element.ValueKind != JsonValueKind.String)
        {
            return InvalidArgument($"参数 {field} 必须是字符串", field);
        }

        var text = element.GetString()?.Trim();
        value = string.IsNullOrEmpty(text) ? null : text;
        return null;
    }

    private static BridgeError? ReadBool(
        IReadOnlyDictionary<string, JsonElement> raw,
        string field,
        out bool value)
    {
        value = false;
        if (!TryGetElement(raw, field, out var element) || element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (element.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            value = element.GetBoolean();
            return null;
        }

        return InvalidArgument($"参数 {field} 必须是布尔值", field);
    }

    private static bool TryGetElement(
        IReadOnlyDictionary<string, JsonElement> raw,
        string field,
        out JsonElement element)
    {
        if (raw.TryGetValue(field, out element))
        {
            return true;
        }

        foreach (var pair in raw)
        {
            if (pair.Key.Equals(field, StringComparison.OrdinalIgnoreCase))
            {
                element = pair.Value;
                return true;
            }
        }

        element = default;
        return false;
    }

    private static IReadOnlyDictionary<string, JsonElement>? BuildRepoActionArgs(StoredJobArguments arguments)
    {
        var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        void Add(string name, object value) => values[name] = JsonSerializer.SerializeToElement(value, WebJson);

        if (arguments.Confirm)
        {
            Add("confirm", true);
        }

        if (arguments.Branch is not null)
        {
            Add("branch", arguments.Branch);
        }

        if (arguments.Tag is not null)
        {
            Add("tag", arguments.Tag);
        }

        if (arguments.Message is not null)
        {
            Add("message", arguments.Message);
        }

        if (arguments.SetUpstream)
        {
            Add("setUpstream", true);
        }

        if (arguments.Stash)
        {
            Add("stash", true);
        }

        if (arguments.StageAll)
        {
            Add("stageAll", true);
        }

        return values;
    }

    // ------------------------------------------------------------------ 作业持久化/创建/查询

    private JobRecord CreateJob(
        string kind,
        StoredJobArguments arguments,
        List<JobItemRecord> items,
        string? actor,
        string? requestId)
    {
        var job = new JobRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            Kind = kind,
            Arguments = arguments,
            Actor = actor,
            RequestId = requestId,
            State = JobStates.Pending,
            CreatedAt = Now()
        };

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            item.Id = job.Id + ":" + (index + 1).ToString(CultureInfo.InvariantCulture);
            item.RepoName ??= BatchOperationService.RepoNameFor(null, item.ComponentId, item.RepoType);
            job.Items.Add(item);
        }

        return job;
    }

    private void Enqueue(JobRecord job)
    {
        lock (_sync)
        {
            _jobs.Add(job);
            PruneJobsUnsafe();
            PersistUnsafe();
        }

        _queue.Writer.TryWrite(job);
    }

    private void PruneJobsUnsafe()
    {
        while (_jobs.Count > MaxRetainedJobs)
        {
            var removableIndex = _jobs.FindIndex(job => IsTerminal(job.State));
            if (removableIndex < 0)
            {
                return;
            }

            _jobs.RemoveAt(removableIndex);
        }
    }

    private JobRecord? FindJobUnsafe(string jobId)
    {
        for (var index = _jobs.Count - 1; index >= 0; index--)
        {
            if (string.Equals(_jobs[index].Id, jobId, StringComparison.Ordinal))
            {
                return _jobs[index];
            }
        }

        return null;
    }

    private CommandResult JobNotFound(string jobId) => CommandResult.Failure(
        BridgeErrorCodes.JobNotFound,
        "作业不存在或已被历史淘汰：" + jobId,
        new Dictionary<string, object?> { ["jobId"] = jobId });

    private void EnsureLoaded()
    {
        var loadedNow = false;
        lock (_sync)
        {
            if (_loaded)
            {
                return;
            }

            _loaded = true;
            loadedNow = true;
            LoadUnsafe();
        }

        if (loadedNow)
        {
            CleanupTrashOnce();
        }
    }

    private void LoadUnsafe()
    {
        IReadOnlyList<StoredJob> stored;
        try
        {
            stored = _store.Load();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            HostLog.Warn($"作业历史加载失败（{ex.GetType().Name}）");
            return;
        }

        var interrupted = 0;
        foreach (var entry in stored)
        {
            var job = FromStored(entry);

            // “重启不重复执行”（AC-OP-26/P1-5）：RUNNING item → INTERRUPTED；未开始 PENDING item → CANCELED
            // （reason=进程重启未执行），绝不重新入队、也不把未开始项误标 INTERRUPTED。
            if (job.State is JobStates.Pending or JobStates.Running || job.Items.Any(item =>
                    item.State is JobItemStates.Pending or JobItemStates.Running))
            {
                MarkInterruptedUnsafe(job, "进程重启：未开始执行（不重复执行）", "进程重启：执行中断（不重复执行）");
                interrupted++;
                WriteMissingItemAuditsUnsafe(job);
            }

            _jobs.Add(job);
        }

        PruneJobsUnsafe();
        if (interrupted > 0)
        {
            HostLog.Warn($"作业历史加载：{interrupted} 个未完成作业已收敛（RUNNING→INTERRUPTED、PENDING→CANCELED，重启不重复执行）");
        }

        if (_jobs.Count > 0)
        {
            PersistUnsafe();
        }
    }

    private void PersistUnsafe()
    {
        try
        {
            var stored = new List<StoredJob>(_jobs.Count);
            foreach (var job in _jobs)
            {
                stored.Add(ToStored(job));
            }

            _store.Save(stored);
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException)
        {
            HostLog.Warn($"作业历史持久化失败（{ex.GetType().Name}）");
        }
    }

    private static StoredJob ToStored(JobRecord job) => new(
        job.Id,
        job.Kind,
        job.State,
        job.CreatedAt,
        job.StartedAt,
        job.FinishedAt,
        job.Items.Select(item => new StoredJobItem(
            item.ComponentId,
            item.RepoType,
            item.RepoName,
            item.State,
            item.StartedAt,
            item.FinishedAt,
            item.ErrorCode,
            item.Message,
            item.PreHead,
            item.PostHead,
            item.BackupRef,
            item.Preview)).ToList(),
        job.Arguments,
        job.Actor,
        job.RequestId,
        job.Arguments.DryRun,
        job.CancelRequested);

    private static JobRecord FromStored(StoredJob stored)
    {
        var job = new JobRecord
        {
            Id = stored.Id,
            Kind = stored.Kind,
            State = string.IsNullOrWhiteSpace(stored.State) ? JobStates.Interrupted : stored.State,
            CreatedAt = stored.CreatedAt,
            StartedAt = stored.StartedAt,
            FinishedAt = stored.FinishedAt,
            Arguments = stored.Arguments ?? DefaultArguments(),
            Actor = stored.Actor,
            RequestId = stored.RequestId,
            CancelRequested = stored.CancelRequested
        };

        var index = 0;
        foreach (var item in stored.Items ?? Array.Empty<StoredJobItem>())
        {
            index++;
            job.Items.Add(new JobItemRecord
            {
                Id = job.Id + ":" + index.ToString(CultureInfo.InvariantCulture),
                ComponentId = item.ComponentId,
                RepoType = item.RepoType,
                RepoName = item.RepoName,
                State = string.IsNullOrWhiteSpace(item.State) ? JobItemStates.Interrupted : item.State,
                StartedAt = item.StartedAt,
                FinishedAt = item.FinishedAt,
                ErrorCode = item.ErrorCode,
                Message = item.Message,
                PreHead = item.PreHead,
                PostHead = item.PostHead,
                BackupRef = item.BackupRef,
                Preview = item.Preview || string.Equals(item.State, JobItemStates.Preview, StringComparison.Ordinal)
            });
        }

        return job;
    }

    private static JobDto ToDto(JobRecord job)
    {
        var items = job.Items
            .Select(item => new JobItemDto(
                item.ComponentId,
                item.RepoType,
                item.RepoName,
                item.State,
                item.StartedAt,
                item.FinishedAt,
                item.ErrorCode,
                item.Message,
                item.PreHead,
                item.PostHead,
                item.BackupRef,
                item.Preview || string.Equals(item.State, JobItemStates.Preview, StringComparison.Ordinal)))
            .ToList();

        return new JobDto(
            job.Id,
            job.Kind,
            job.State,
            items.Count,
            items.Count(item => item.State == JobItemStates.Ok),
            items.Count(item => item.State == JobItemStates.Failed),
            items.Count(item => item.State == JobItemStates.Skipped),
            job.CreatedAt,
            job.StartedAt,
            job.FinishedAt,
            job.State is JobStates.Pending or JobStates.Running && !job.CancelRequested,
            job.Arguments.DryRun,
            job.CancelRequested,
            JobMessage(job),
            items);
    }

    /// <summary>作业级文案（dryRun=预览、取消/中断说明；正常作业为 null 由 UI 自行组合逐仓文案）。</summary>
    private static string? JobMessage(JobRecord job)
    {
        if (job.Arguments.DryRun)
        {
            return $"预览（DRY_RUN）：仅输出逐仓预检结果，未执行、未写入任何仓库/ref（{job.Items.Count} 项）";
        }

        if (job.State == JobStates.Canceled)
        {
            return "作业已取消：未开始项已取消（errorCode=null）；已开始项按合作式取消终止";
        }

        if (job.State == JobStates.Interrupted)
        {
            return "作业已中断：进程重启/宿主关闭导致执行中断，重启后不重复执行";
        }

        return null;
    }

    // ------------------------------------------------------------------ 事件推送

    private void Publish(JobRecord job)
    {
        var sink = EventSink;
        if (sink is null)
        {
            return;
        }

        JobDto dto;
        lock (_sync)
        {
            dto = ToDto(job);
        }

        try
        {
            // 固定形状（AC-UI-10c②）：{ event, jobId, current, data, state, total, cancellable }；
            // current 与 data 同为 job 快照（data 为 M2b 首版兼容键）。
            sink(new JobUpdatedEventDto(
                Event: "JOB_UPDATED",
                JobId: dto.Id,
                Current: dto,
                Data: dto,
                State: dto.State,
                Total: dto.Total,
                Cancellable: dto.Cancellable));
        }
        catch (Exception ex)
        {
            // UI 已关闭/回调异常不影响作业执行。
            HostLog.Warn($"JOB_UPDATED 推送失败：{ex.GetType().Name}");
        }
    }

    // ------------------------------------------------------------------ .trash 清理

    private void CleanupTrashOnce()
    {
        if (Interlocked.Exchange(ref _trashCleanupStarted, 1) != 0)
        {
            return;
        }

        try
        {
            _batch.CleanupTrash();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            HostLog.Warn($"清理 .trash 失败（{ex.GetType().Name}）");
        }
    }

    // ------------------------------------------------------------------ 生命周期

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            try
            {
                _lifetime.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // 已取消/销毁。
            }

            // 进程退出兜底：内存中的未完成作业收敛（RUNNING→INTERRUPTED、未开始→CANCELED）并落盘，
            // 下次启动不会再执行；逐 item 补写审计行。
            foreach (var job in _jobs.Where(entry => !IsTerminal(entry.State)))
            {
                MarkInterruptedUnsafe(job, "宿主关闭：未开始执行", "宿主关闭：执行中断");
                WriteMissingItemAuditsUnsafe(job);
            }

            try
            {
                PersistUnsafe();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                HostLog.Warn($"作业历史关闭落盘失败（{ex.GetType().Name}）");
            }

            _queue.Writer.TryComplete();
        }

        // 给 worker 少量时间观察取消退出（不阻塞过久；git 进程随 CancellationToken 被杀）。
        try
        {
            _worker.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // worker 已因取消/Socket 关闭退出。
        }

        // 不 Dispose _lifetime：worker 可能仍持有取消注册；进程/对象回收时自然释放，避免竞态。
    }

    private static string Now() => DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value.Length <= max ? value : value[..(max - 1)] + "…";
    }

    // ------------------------------------------------------------------ 内部模型

    private sealed class JobRecord
    {
        public required string Id { get; init; }

        public required string Kind { get; init; }

        public required string State { get; set; }

        public required string CreatedAt { get; init; }

        public string? StartedAt { get; set; }

        public string? FinishedAt { get; set; }

        public bool CancelRequested { get; set; }

        public required StoredJobArguments Arguments { get; init; }

        public string? Actor { get; init; }

        public string? RequestId { get; init; }

        public List<JobItemRecord> Items { get; } = new();

        public CancellationTokenSource? Cts { get; set; }
    }

    private sealed class JobItemRecord
    {
        public required string Id { get; set; }

        public required string ComponentId { get; init; }

        public required string RepoType { get; init; }

        public string? RepoName { get; set; }

        public required string State { get; set; }

        public string? StartedAt { get; set; }

        public string? FinishedAt { get; set; }

        public string? ErrorCode { get; set; }

        public string? Message { get; set; }

        public string? PreHead { get; set; }

        public string? PostHead { get; set; }

        public string? BackupRef { get; set; }

        /// <summary>dryRun 预检 item 标记（state=PREVIEW 时为 true）。</summary>
        public bool Preview { get; set; }

        /// <summary>动作前仓库状态快照（审计 <c>state</c> 列；未执行路径可能为 UNKNOWN）。</summary>
        public string? RepoState { get; set; }

        /// <summary>审计行已写标记（每 item 每个终态恰 1 行）。</summary>
        public bool AuditWritten { get; set; }
    }
}
