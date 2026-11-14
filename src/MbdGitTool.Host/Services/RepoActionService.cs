using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MbdGitTool.Core.Contracts;
using MbdGitTool.Core.Domain;
using MbdGitTool.Core.Security;

namespace MbdGitTool.Host.Services;

/// <summary>
/// 单仓操作（spec §6.2 矩阵 / code-plan §3.9 / SRS §7.6/§9.16 FR-ACT）。
/// <para><b>M2</b>：<c>GetAvailability</c> 仍是矩阵唯一事实源（Enabled = MatrixAllowed，Stage=M2）；</para>
/// <para><c>RunAsync</c> 校验（action → 仓 → 矩阵 → 确认 → 本地仓 → 凭据作用域 → CAS）后执行真实 git 单仓操作：
/// FETCH / PULL (fetch+merge --ff-only) / CHKOUT / COMMIT / PUSH；同仓串行、异仓并发。</para>
/// <para><b>安全（M2 P0/P1 收口）</b>：所有 git 参数走 <c>ArgumentList</c>；PAT 仅在网络动作、
/// 且 fetch/push URL 与 <c>AppConfig.GiteaBaseUrl</c>（scheme+host+port）及 <c>/&lt;org&gt;/</c> 路径完全匹配时，
/// 以 URL 作用域 <c>http.&lt;base&gt;.extraHeader</c> 环境注入；配置非法/作用域不匹配一律 fail-closed（不启动网络 git）；
/// 不进入参数/日志/响应/错误 details（输出统一脱敏 + 截断）。http base 需显式 <c>allowInsecureHttp</c>。</para>
/// </summary>
public sealed class RepoActionService
{
    private static readonly string[] Actions = { "PULL", "FETCH", "CHKOUT", "COMMIT", "PUSH" };

    /// <summary>网络类动作（需要 PAT 注入与 URL 作用域校验）。</summary>
    private static readonly string[] NetworkActions = { "FETCH", "PULL", "PUSH" };

    /// <summary>写类动作（需要快照 HEAD CAS 校验）。</summary>
    private static readonly string[] WriteActions = { "PULL", "CHKOUT", "COMMIT", "PUSH" };

    /// <summary>响应 output / 错误 stderr 摘要最大字符数（spec 风险预算）。</summary>
    private const int MaxOutputChars = 2000;

    /// <summary>COMMIT message 最大字符数（任务书 M2 口径；SRS FR-ACT-03 的 200 为 UI 约束）。</summary>
    private const int MaxCommitMessageChars = 2000;

    /// <summary>提交信息展示摘要最大字符数。</summary>
    private const int MaxErrorSummaryChars = 500;

    /// <summary>错误摘要保留的最大 stderr 行数（P1-6：看最后 20 行 + 关键行）。</summary>
    private const int MaxSummaryLines = 20;

    /// <summary>CHKOUT 自动 stash 的固定 message（便于用户按 message 找回）。</summary>
    public const string AutoStashMessage = "MBD: auto stash before CHKOUT";

    private const string Stage = "M2";

    // 写操作超时：fetch/pull/push 120s；commit/checkout 60s；只读探测沿用 GitCli 默认 20s。
    private static readonly TimeSpan NetworkTimeout = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan WriteTimeout = TimeSpan.FromSeconds(60);

    // 同一仓操作串行（跨服务实例共享）；不同仓各自一把锁，可并发。
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> RepoLocks =
        new(StringComparer.OrdinalIgnoreCase);

    // spec §6.2 矩阵编码（顺序 PULL/FETCH/CHKOUT/COMMIT/PUSH）：Y=允许 N=禁止 C=允许但需确认。
    private static readonly IReadOnlyDictionary<string, string> Matrix =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["SYNCED"] = "YYYYY",
            ["DIRTY"] = "NYCYY",
            ["AHEAD"] = "NYCNY",
            ["BEHIND"] = "YYCNN",
            ["DIVERGED"] = "NYCNN",
            ["DETACHED_TAG"] = "NYCNN",
            ["DETACHED_COMMIT"] = "NYCNN",
            ["NO_UPSTREAM"] = "NYNNY",
            ["EMPTY"] = "NYCYY", // M2b（FR-REPO-05 / D-025 §2.5④）：恢复 EMPTY.PUSH=Y（首次推送建 upstream）
            ["UNMERGED"] = "NYNNN",
            ["IN_PROGRESS"] = "NNNNN",
            ["INVALID"] = "NNNNN",
            ["MISSING"] = "NNNNN",
            ["UNKNOWN"] = "NYNNN"
        };

    private readonly GitCli _git;
    private readonly OperationJournal? _journal;
    private readonly string? _giteaToken;
    private readonly string? _giteaBaseUrl;
    private readonly string? _giteaOrg;
    private readonly bool _allowInsecureHttp;

    /// <param name="git">git 调用封装（可空，默认 <see cref="GitCli"/>）。</param>
    /// <param name="giteaToken">Gitea PAT（可空；为 null 时使用 <paramref name="config"/> 中的 token）。</param>
    /// <param name="config">应用配置（提供 Gitea baseUrl/org、token 与 allowInsecureHttp）。</param>
    /// <param name="journal">
    /// M2b-2b 写前 journal（FR-SAFE-04；可空 = 不做 journal，仅供不接 Host 的独立测试/安全用例）。
    /// 生产由 <c>HostCommandRouter</c> 注入；git 写前写 STARTED，成功写 DONE，失败/取消/异常写 FAILED。
    /// </param>
    public RepoActionService(
        GitCli? git = null,
        string? giteaToken = null,
        AppConfig? config = null,
        OperationJournal? journal = null)
    {
        _git = git ?? new GitCli();
        _journal = journal;
        var token = giteaToken ?? config?.GiteaToken;
        _giteaToken = string.IsNullOrWhiteSpace(token) ? null : token.Trim();
        _giteaBaseUrl = config?.GiteaBaseUrl;
        _giteaOrg = config?.GiteaOrg;
        _allowInsecureHttp = config?.AllowInsecureHttp ?? false;
    }

    /// <summary>
    /// 批量作业与单仓动作共享的 per-repo 互斥锁（P1-3 收口）：键 = canonical localPath，与
    /// <see cref="RunAsync"/> 使用同一静态 <see cref="RepoLocks"/> 字典；同仓串行、异仓可并发。
    /// 批量 SYNC/DELETE/CLONE/RESTORE/CREATE/TAG 在 <c>JobEngine</c> 逐仓执行前调用本方法，
    /// 避免与单仓 M2a 写操作并发写同一仓。
    /// </summary>
    public static async Task<IDisposable> AcquireRepoLockAsync(string? localPath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localPath);
        var key = PathPolicy.TryCanonicalize(localPath) ?? localPath.Trim();
        var gate = RepoLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new RepoLockLease(gate);
    }

    private sealed class RepoLockLease : IDisposable
    {
        private SemaphoreSlim? _gate;

        public RepoLockLease(SemaphoreSlim gate) => _gate = gate;

        public void Dispose()
        {
            var gate = Interlocked.Exchange(ref _gate, null);
            gate?.Release();
        }
    }


    /// <summary>
    /// 按仓状态给出五键可用性（固定顺序 PULL/FETCH/CHKOUT/COMMIT/PUSH；Enabled=MatrixAllowed）。
    /// <para><b>RequiresConfirm</b>（ADR AC-UI-08 / SRS §7.6 冻结）：COMMIT/PUSH 恒为 <c>true</c>（与矩阵 cell 无关），
    /// 其余动作取矩阵 <c>C</c>。<c>RunAsync</c> 据此强制 <c>args.confirm=true</c>（FR-ACT-04），
    /// 防止绕过 UI 确认条直接调用桥/Win32 写。</para>
    /// </summary>
    public IReadOnlyList<RepoActionAvailabilityDto> GetAvailability(RepoRef repo)
    {
        var state = NormalizeState(repo.Repo.State);
        var row = Matrix.TryGetValue(state, out var encoded) ? encoded : "NNNNN";
        var result = new List<RepoActionAvailabilityDto>(Actions.Length);

        for (var index = 0; index < Actions.Length; index++)
        {
            var action = Actions[index];
            var cell = index < row.Length ? row[index] : 'N';
            var matrixAllowed = cell is 'Y' or 'C';

            // ADR AC-UI-08：RequiresConfirm = (cell == ⚠/C) ∨ action ∈ {COMMIT, PUSH}。
            // SRS §7.6 冻结 C：COMMIT/PUSH 确认标志固定，不随矩阵行变化（即使矩阵 cell 为 Y）。
            var requiresConfirm = cell == 'C'
                || action.Equals("COMMIT", StringComparison.Ordinal)
                || action.Equals("PUSH", StringComparison.Ordinal);

            string reasonCode;
            string reason;
            if (!matrixAllowed)
            {
                reasonCode = "STATE_BLOCKED";
                reason = DisabledReason(state, action);
            }
            else if (requiresConfirm)
            {
                reasonCode = "CONFIRM_REQUIRED";
                reason = "该操作会改变工作区/HEAD，需二次确认（args.confirm=true）";
            }
            else
            {
                reasonCode = "AVAILABLE";
                reason = "M2 已启用：可直接执行";
            }

            result.Add(new RepoActionAvailabilityDto(
                Action: action,
                Enabled: matrixAllowed, // M2：能力开关 RepoActions=true，按矩阵启用（D-024）
                MatrixAllowed: matrixAllowed,
                ReasonCode: reasonCode,
                Reason: reason,
                RequiresConfirm: requiresConfirm,
                Stage: Stage));
        }

        return result;
    }

    /// <summary>同步包装（兼容旧调用/单测）；消息循环请用 <see cref="RunAsync"/>。</summary>
    public CommandResult Run(RunRepoActionParams parameters, SnapshotIndex index) =>
        RunAsync(parameters, index, CancellationToken.None).GetAwaiter().GetResult();

    /// <summary>
    /// 执行单仓操作（真实 git；不抛非取消异常：全部映射为 <see cref="CommandResult"/>）。
    /// <para><paramref name="onCompleted"/>：执行结束（成功或失败/异常）时回调一次审计摘要
    /// （含 action/componentId/repoType/state/preHead/postHead/ok/errorCode/durationMs；不含 token/output），
    /// 供 <c>HostCommandRouter</c> 在返回前写审计-lite（G-P0-2/AC-OP-33）。</para>
    /// <para><paramref name="jobId"/>/<paramref name="jobItemId"/>：批量作业复用单仓动作时的 job 关联，
    /// 写进 journal 供崩溃 reconcile 回写任务中心（单仓 M2a 调用为 null）。</para>
    /// </summary>
    public async Task<CommandResult> RunAsync(
        RunRepoActionParams parameters,
        SnapshotIndex index,
        CancellationToken cancellationToken = default,
        Action<RepoActionExecution>? onCompleted = null,
        string? jobId = null,
        string? jobItemId = null)
    {
        var execution = new RepoActionExecution
        {
            Action = string.IsNullOrWhiteSpace(parameters?.Action)
                ? parameters?.Action
                : parameters.Action.Trim().ToUpperInvariant(),
            ComponentId = parameters?.ComponentId,
            RepoType = parameters?.RepoType
        };

        var stopwatch = Stopwatch.StartNew();
        CommandResult result;
        try
        {
            result = await RunCoreAsync(
                    parameters!,
                    index,
                    cancellationToken,
                    execution,
                    jobId,
                    jobItemId)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // 取消/内部异常同样落审计（失败也记），随后保持原异常语义。
            stopwatch.Stop();
            execution.DurationMs = (long)stopwatch.ElapsedMilliseconds;
            execution.Ok = false;
            execution.ErrorCode = ex is OperationCanceledException
                ? BridgeErrorCodes.Timeout
                : BridgeErrorCodes.Internal;
            execution.PostHead ??= execution.PreHead;
            NotifyCompleted(onCompleted, execution);
            throw;
        }

        stopwatch.Stop();
        execution.DurationMs = (long)stopwatch.ElapsedMilliseconds;
        execution.Ok = result.Ok;
        execution.ErrorCode = result.Error?.Code;
        execution.PostHead ??= execution.PreHead;
        NotifyCompleted(onCompleted, execution);
        return result;
    }

    /// <summary>审计回调隔离：回调自身异常不得改变动作结果。</summary>
    private static void NotifyCompleted(Action<RepoActionExecution>? onCompleted, RepoActionExecution execution)
    {
        if (onCompleted is null)
        {
            return;
        }

        try
        {
            onCompleted(execution);
        }
        catch
        {
            // 审计是旁路能力：回调失败不影响动作返回。
        }
    }

    private async Task<CommandResult> RunCoreAsync(
        RunRepoActionParams? parameters,
        SnapshotIndex index,
        CancellationToken cancellationToken,
        RepoActionExecution execution,
        string? jobId,
        string? jobItemId)
    {
        if (parameters is null)
        {
            return CommandResult.Failure(BridgeErrorCodes.InvalidArgument, "缺少参数");
        }

        // 1) action 白名单。
        var action = parameters.Action?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(action) || !Actions.Contains(action, StringComparer.Ordinal))
        {
            return CommandResult.Failure(
                BridgeErrorCodes.InvalidArgument,
                "参数 action 必须是 PULL/FETCH/CHKOUT/COMMIT/PUSH",
                new Dictionary<string, object?>
                {
                    ["field"] = "action",
                    ["action"] = parameters.Action
                });
        }

        // 2) 仓解析。
        var resolveError = RepoResolver.TryResolve(index, parameters.ComponentId, parameters.RepoType, out var repo);
        if (resolveError is not null)
        {
            return CommandResult.Failure(resolveError);
        }

        // 审计摘要（G-P0-2）：快照态/HEAD 作为失败早退时的兜底值；ExecuteAsync 会用真实 HEAD 覆盖。
        execution.State = NormalizeState(repo.Repo.State);
        execution.PreHead = string.IsNullOrWhiteSpace(repo.Repo.HeadSha) ? null : repo.Repo.HeadSha.Trim();

        // 3) §7.6 矩阵（唯一事实源）。
        var availability = GetAvailability(repo).First(item => item.Action == action);
        if (!availability.MatrixAllowed)
        {
            return CommandResult.Failure(
                BridgeErrorCodes.ActionNotAllowed,
                availability.Reason ?? $"当前状态 {repo.Repo.State} 不允许 {action}",
                new Dictionary<string, object?>
                {
                    ["action"] = action,
                    ["repoType"] = repo.Repo.RepoType,
                    ["state"] = NormalizeState(repo.Repo.State),
                    ["reason"] = availability.Reason
                });
        }

        // 4) args 解析/校验。
        var argumentError = TryParseArguments(parameters.Args, action, out var arguments);
        if (argumentError is not null)
        {
            return argumentError;
        }

        // 5) 需确认的 cell：未带 confirm=true 一律不写（FR-ACT-04）。
        if (availability.RequiresConfirm && !arguments.Confirm)
        {
            return CommandResult.Failure(
                BridgeErrorCodes.ConfirmRequired,
                $"{action} 需要二次确认：请在 args 中传入 confirm=true 后重试",
                new Dictionary<string, object?>
                {
                    ["action"] = action,
                    ["repoType"] = repo.Repo.RepoType,
                    ["state"] = NormalizeState(repo.Repo.State),
                    ["requiresConfirm"] = true
                });
        }

        // 6) COMMIT 的 stageAll 是显式“暂存全部改动”能力，必须与 confirm=true 同时出现（默认 staged-only）。
        if (action == "COMMIT" && arguments.StageAll && !arguments.Confirm)
        {
            return CommandResult.Failure(
                BridgeErrorCodes.ConfirmRequired,
                "COMMIT 的 stageAll=true 会暂存全部改动（含未跟踪文件），需同时传入 confirm=true",
                new Dictionary<string, object?>
                {
                    ["action"] = "COMMIT",
                    ["requiresConfirm"] = true,
                    ["stageAll"] = true
                });
        }

        // 7) 本地仓前提（快照可能过期，必须在磁盘/Git 上再收口一次）。
        if (!repo.Repo.IsLocal || string.IsNullOrWhiteSpace(repo.Repo.LocalPath))
        {
            return CommandResult.Failure(
                BridgeErrorCodes.RepoNotLocal,
                $"仓库未克隆：{RepoResolver.RepoName(repo)}",
                new Dictionary<string, object?> { ["repoType"] = repo.Repo.RepoType });
        }

        var localPath = PathPolicy.TryCanonicalize(repo.Repo.LocalPath);
        if (localPath is null || !Directory.Exists(localPath))
        {
            return CommandResult.Failure(
                BridgeErrorCodes.RepoNotLocal,
                "本地仓库路径不存在或不可用（请刷新后重试）",
                new Dictionary<string, object?> { ["repoType"] = repo.Repo.RepoType });
        }

        if (!IsGitRepository(localPath))
        {
            return CommandResult.Failure(
                BridgeErrorCodes.RepoNotLocal,
                "本地路径不是 git 仓库（请刷新/修复后重试）",
                new Dictionary<string, object?> { ["repoType"] = repo.Repo.RepoType });
        }

        // 8) 同仓串行：per-repo 信号量（静态字典，跨实例共享；批量作业复用同一把锁）。
        using var lease = await AcquireRepoLockAsync(localPath, cancellationToken).ConfigureAwait(false);
        return await ExecuteAsync(
                action,
                repo,
                localPath,
                arguments,
                execution,
                cancellationToken,
                jobId,
                jobItemId)
            .ConfigureAwait(false);
    }

    private async Task<CommandResult> ExecuteAsync(
        string action,
        RepoRef repo,
        string localPath,
        ActionArguments arguments,
        RepoActionExecution execution,
        CancellationToken cancellationToken,
        string? jobId,
        string? jobItemId)
    {
        var stopwatch = Stopwatch.StartNew();
        var preHead = await ReadHeadAsync(localPath, cancellationToken).ConfigureAwait(false);
        execution.PreHead = preHead;

        // M2 P0-1：网络动作先做凭据作用域校验（锁内、执行 git 前）；不匹配/配置非法一律不启动网络 git。
        IReadOnlyDictionary<string, string>? credentialEnvironment = null;
        if (IsNetworkAction(action))
        {
            var (scopeFailure, environment) =
                await PrepareCredentialEnvironmentAsync(localPath, action, cancellationToken).ConfigureAwait(false);
            if (scopeFailure is not null)
            {
                HostLog.Warn(
                    $"runRepoAction {action} {repo.ComponentId}/{repo.Repo.RepoType} ok=false "
                    + $"code={scopeFailure.Error?.Code ?? "<none>"} ms={stopwatch.ElapsedMilliseconds} (credential scope)");
                return scopeFailure;
            }

            credentialEnvironment = environment;
        }

        // M2 P1-2：写动作执行前做 CAS（快照 HEAD vs 当前 HEAD），外部改动一律拒绝且零改动。
        if (IsWriteAction(action))
        {
            var casFailure = CheckSnapshotHead(action, repo, preHead);
            if (casFailure is not null)
            {
                HostLog.Warn(
                    $"runRepoAction {action} {repo.ComponentId}/{repo.Repo.RepoType} ok=false "
                    + $"code={casFailure.Error?.Code ?? "<none>"} ms={stopwatch.ElapsedMilliseconds} (cas)");
                return casFailure;
            }
        }

        // FR-SAFE-04 / ADR D-026 M2b-2b：真实 git 写前先落 journal STARTED（含 preHead/localPath/job 关联），
        // 成功落 DONE（postHead）、失败落 FAILED（errorCode）、取消/异常也收口；进程被杀时留下 STARTED
        // 由启动 JournalReconciler 按当前 HEAD 判定 NOT_APPLIED/UNKNOWN_APPLIED（不猜成功）。
        var journalEntry = _journal?.Begin(
            action,
            repo.ComponentId,
            repo.Repo.RepoType,
            preHead,
            localPath,
            jobId,
            jobItemId);

        try
        {
            ActionOutcome outcome;
            switch (action)
            {
                case "FETCH":
                    outcome = await FetchAsync(localPath, credentialEnvironment, cancellationToken)
                        .ConfigureAwait(false);
                    break;
                case "PULL":
                    outcome = await PullAsync(localPath, arguments, credentialEnvironment, cancellationToken)
                        .ConfigureAwait(false);
                    break;
                case "CHKOUT":
                    outcome = await CheckoutAsync(localPath, arguments, cancellationToken).ConfigureAwait(false);
                    break;
                case "COMMIT":
                    outcome = await CommitAsync(localPath, arguments, cancellationToken).ConfigureAwait(false);
                    break;
                case "PUSH":
                    outcome = await PushAsync(localPath, repo, arguments, credentialEnvironment, cancellationToken)
                        .ConfigureAwait(false);
                    break;
                default:
                    _journal?.Fail(journalEntry, BridgeErrorCodes.InvalidArgument);
                    return CommandResult.Failure(
                        BridgeErrorCodes.InvalidArgument,
                        $"不支持的 action：{action}",
                        new Dictionary<string, object?> { ["field"] = "action", ["action"] = action });
            }

            stopwatch.Stop();
            if (outcome.Failure is not null)
            {
                // 审计：失败动作理论上零 HEAD 改动；仍读取真实 HEAD 以记录 postHead（失败也记）。
                execution.PostHead = await ReadHeadAsync(localPath, cancellationToken).ConfigureAwait(false) ?? preHead;
                _journal?.Fail(journalEntry, outcome.Failure.Error?.Code ?? BridgeErrorCodes.GitFailed);
                HostLog.Warn(
                    $"runRepoAction {action} {repo.ComponentId}/{repo.Repo.RepoType} ok=false "
                    + $"code={outcome.Failure.Error?.Code ?? "<none>"} ms={stopwatch.ElapsedMilliseconds}");
                return outcome.Failure;
            }

            var postHead = await ReadHeadAsync(localPath, cancellationToken).ConfigureAwait(false) ?? preHead;
            var branch = await ReadCurrentBranchAsync(localPath, cancellationToken).ConfigureAwait(false);
            execution.PostHead = postHead;
            _journal?.Complete(journalEntry, postHead);

            HostLog.Info(
                $"runRepoAction {action} {repo.ComponentId}/{repo.Repo.RepoType} ok=true ms={stopwatch.ElapsedMilliseconds}");

            return CommandResult.Success(new
            {
                accepted = true,
                action,
                componentId = repo.ComponentId,
                repoType = repo.Repo.RepoType,
                state = NormalizeState(repo.Repo.State),
                preHead,
                postHead,
                branch,
                stashRef = outcome.StashRef,
                output = outcome.Output,
                durationMs = (long)stopwatch.ElapsedMilliseconds,
                message = outcome.Message
            });
        }
        catch (OperationCanceledException)
        {
            // 取消 ≠ 成功：journal 收口 FAILED（不猜成功），异常语义保持原样。
            _journal?.Fail(journalEntry, BridgeErrorCodes.Timeout);
            throw;
        }
        catch
        {
            // 未捕获异常同样收口（不留下无解释的 STARTED）；由 RunAsync 外层映射 E_INTERNAL。
            _journal?.Fail(journalEntry, BridgeErrorCodes.Internal);
            throw;
        }
    }

    private async Task<ActionOutcome> FetchAsync(
        string localPath,
        IReadOnlyDictionary<string, string>? credentialEnvironment,
        CancellationToken cancellationToken)
    {
        var result = await _git
            .RunAsync(
                localPath,
                new[] { "fetch", "--prune", "origin" },
                cancellationToken,
                NetworkTimeout,
                credentialEnvironment)
            .ConfigureAwait(false);

        if (!result.Success)
        {
            return new ActionOutcome(MapGitFailure("FETCH", result, cancellationToken), string.Empty, string.Empty);
        }

        return new ActionOutcome(null, FormatOutput(result), "FETCH 完成");
    }

    private async Task<ActionOutcome> PullAsync(
        string localPath,
        ActionArguments arguments,
        IReadOnlyDictionary<string, string>? credentialEnvironment,
        CancellationToken cancellationToken)
    {
        // AC-SAFE-17 / FR-ACT-03：PULL 只允许当前分支；显式 args.branch 必须等于当前 HEAD 分支，
        // 否则拒绝且不启动任何网络 git（防止把当前分支 FF 到其它远端分支 tip）。
        var branch = arguments.Branch;
        var currentBranch = await ReadCurrentBranchAsync(localPath, cancellationToken).ConfigureAwait(false);
        if (branch is null)
        {
            // 未指定 branch = 当前分支；detached HEAD 无法推断，要求显式参数。
            if (currentBranch is null)
            {
                return new ActionOutcome(
                    InvalidArgument("PULL 需要 branch 参数（当前 HEAD 处于 detached 状态或无法解析当前分支）", "branch"),
                    string.Empty,
                    string.Empty);
            }

            branch = currentBranch;
        }
        else if (currentBranch is null)
        {
            return new ActionOutcome(
                InvalidArgument(
                    "PULL 显式 branch 需要当前 HEAD 处于分支状态（detached HEAD 无法校验分支一致）；请先检出分支",
                    "branch"),
                string.Empty,
                string.Empty);
        }
        else if (!string.Equals(branch, currentBranch, StringComparison.Ordinal))
        {
            return new ActionOutcome(
                CommandResult.Failure(
                    BridgeErrorCodes.InvalidArgument,
                    $"PULL 只允许拉取当前分支：当前分支 {currentBranch}，请求 branch={branch}；已拒绝且 HEAD 不变",
                    new Dictionary<string, object?>
                    {
                        ["field"] = "branch",
                        ["expected"] = currentBranch,
                        ["current"] = branch,
                        ["sdkCode"] = "GIT_PULL_BRANCH_MISMATCH"
                    }),
                string.Empty,
                string.Empty);
        }

        // M2 P1-1：不再用 git pull（pull 无法传 --no-overwrite-ignore），改为 fetch + merge --ff-only。
        var fetch = await _git
            .RunAsync(
                localPath,
                new[] { "fetch", "origin", branch },
                cancellationToken,
                NetworkTimeout,
                credentialEnvironment)
            .ConfigureAwait(false);
        if (!fetch.Success)
        {
            return new ActionOutcome(MapGitFailure("PULL", fetch, cancellationToken), string.Empty, string.Empty);
        }

        var merge = await _git
            .RunAsync(
                localPath,
                new[] { "merge", "--ff-only", "--no-overwrite-ignore", "origin/" + branch },
                cancellationToken,
                WriteTimeout)
            .ConfigureAwait(false);
        if (!merge.Success)
        {
            if (IsNonFastForward(merge) || IsNonFastForward(fetch))
            {
                return new ActionOutcome(
                    FailWithSummary(
                        BridgeErrorCodes.GitNonFastForward,
                        "PULL 失败：无法快进（--ff-only），请手动 Merge/Rebase 后重试",
                        "PULL",
                        merge,
                        new Dictionary<string, object?>
                        {
                            ["branch"] = branch,
                            ["sdkCode"] = "GIT_NON_FAST_FORWARD"
                        }),
                    string.Empty,
                    string.Empty);
            }

            if (IsOverwriteRejection(merge))
            {
                return new ActionOutcome(OverwriteFailure("PULL", merge), string.Empty, string.Empty);
            }

            return new ActionOutcome(MapGitFailure("PULL", merge, cancellationToken), string.Empty, string.Empty);
        }

        var output = Truncate(RedactSecrets(Combine(
            Combine(fetch.Stdout, fetch.Stderr),
            Combine(merge.Stdout, merge.Stderr))));
        return new ActionOutcome(null, output, $"PULL 完成（{branch}）");
    }

    private async Task<ActionOutcome> CheckoutAsync(
        string localPath,
        ActionArguments arguments,
        CancellationToken cancellationToken)
    {
        // CHKOUT 前必须工作区 clean（含 untracked）：否则拒绝，避免覆盖本地改动。
        // 例外：显式 args.stash=true 时先把工作区改动（含未跟踪）stash -u 保存，再检出。
        var status = await _git
            .RunAsync(localPath, new[] { "status", "--porcelain" }, cancellationToken)
            .ConfigureAwait(false);
        if (!status.Success)
        {
            return new ActionOutcome(MapGitFailure("CHKOUT", status, cancellationToken), string.Empty, string.Empty);
        }

        var dirty = status.Stdout.Trim();
        string? stashRef = null;
        if (dirty.Length > 0)
        {
            if (!arguments.Stash)
            {
                var dirtyCount = dirty.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
                return new ActionOutcome(
                    CommandResult.Failure(
                        BridgeErrorCodes.DirtyWorktree,
                        "工作区存在未提交变更（含未跟踪文件），CHKOUT 前请先提交或 stash（或传 args.stash=true）",
                        new Dictionary<string, object?>
                        {
                            ["action"] = "CHKOUT",
                            ["dirtyCount"] = dirtyCount,
                            ["sdkCode"] = "GIT_DIRTY_WORKTREE"
                        }),
                    string.Empty,
                    string.Empty);
            }

            var stash = await _git
                .RunAsync(
                    localPath,
                    new[] { "stash", "push", "-u", "-m", AutoStashMessage },
                    cancellationToken,
                    WriteTimeout)
                .ConfigureAwait(false);
            if (!stash.Success)
            {
                return new ActionOutcome(MapGitFailure("CHKOUT", stash, cancellationToken), string.Empty, string.Empty);
            }

            var stashId = await _git
                .RunAsync(localPath, new[] { "rev-parse", "--verify", "refs/stash" }, cancellationToken)
                .ConfigureAwait(false);
            stashRef = stashId.Success && stashId.TrimmedStdout.Length > 0 ? "stash@{0}" : null;
        }

        // M2 P1-3：先校验 ref 真实存在（refs/heads|refs/tags^{commit}），避免 checkout 进入路径模式的假成功。
        string[] argumentsForGit;
        string message;
        if (arguments.Tag is not null)
        {
            var verify = await _git
                .RunAsync(
                    localPath,
                    new[] { "rev-parse", "--verify", "--quiet", "refs/tags/" + arguments.Tag + "^{commit}" },
                    cancellationToken)
                .ConfigureAwait(false);
            if (!verify.Success || verify.TrimmedStdout.Length == 0)
            {
                return new ActionOutcome(RefNotFound("tag", arguments.Tag), string.Empty, string.Empty);
            }

            argumentsForGit = new[] { "checkout", "--no-overwrite-ignore", "--detach", arguments.Tag };
            message = $"CHKOUT 完成（tag {arguments.Tag}）";
        }
        else
        {
            var branch = arguments.Branch!;
            var verify = await _git
                .RunAsync(
                    localPath,
                    new[] { "rev-parse", "--verify", "--quiet", "refs/heads/" + branch + "^{commit}" },
                    cancellationToken)
                .ConfigureAwait(false);
            if (!verify.Success || verify.TrimmedStdout.Length == 0)
            {
                return new ActionOutcome(RefNotFound("branch", branch), string.Empty, string.Empty);
            }

            argumentsForGit = new[] { "checkout", "--no-overwrite-ignore", branch };
            message = $"CHKOUT 完成（分支 {branch}）";
        }

        var result = await _git
            .RunAsync(localPath, argumentsForGit, cancellationToken, WriteTimeout)
            .ConfigureAwait(false);
        if (!result.Success)
        {
            // M2 P1-1：ignored/untracked 文件会被覆盖时 git 拒绝；明确提示先处理，绝不静默覆盖。
            if (IsOverwriteRejection(result))
            {
                return new ActionOutcome(OverwriteFailure("CHKOUT", result), string.Empty, string.Empty);
            }

            return new ActionOutcome(MapGitFailure("CHKOUT", result, cancellationToken), string.Empty, string.Empty);
        }

        return new ActionOutcome(null, FormatOutput(result), message, stashRef);
    }

    private async Task<ActionOutcome> CommitAsync(
        string localPath,
        ActionArguments arguments,
        CancellationToken cancellationToken)
    {
        var message = arguments.Message!;

        var status = await _git
            .RunAsync(localPath, new[] { "status", "--porcelain" }, cancellationToken)
            .ConfigureAwait(false);
        if (!status.Success)
        {
            return new ActionOutcome(MapGitFailure("COMMIT", status, cancellationToken), string.Empty, string.Empty);
        }

        // 显式“暂存全部改动”（args.stageAll=true 且 confirm=true）：先 git add -A，再走 staged-only 提交。
        if (arguments.StageAll)
        {
            var add = await _git
                .RunAsync(localPath, new[] { "add", "-A" }, cancellationToken, WriteTimeout)
                .ConfigureAwait(false);
            if (!add.Success)
            {
                return new ActionOutcome(MapGitFailure("COMMIT", add, cancellationToken), string.Empty, string.Empty);
            }
        }

        // FR-ACT-03：默认只提交 index 中已暂存内容，不自动 add（避免误提交未审阅/未跟踪文件）。
        var staged = await _git
            .RunAsync(localPath, new[] { "diff", "--cached", "--quiet" }, cancellationToken)
            .ConfigureAwait(false);
        if (staged.Success)
        {
            return new ActionOutcome(NothingToCommit(), string.Empty, string.Empty);
        }

        if (staged.ExitCode != 1)
        {
            return new ActionOutcome(MapGitFailure("COMMIT", staged, cancellationToken), string.Empty, string.Empty);
        }

        var commit = await _git
            .RunAsync(localPath, new[] { "commit", "-m", message }, cancellationToken, WriteTimeout)
            .ConfigureAwait(false);
        if (!commit.Success)
        {
            return new ActionOutcome(MapGitFailure("COMMIT", commit, cancellationToken), string.Empty, string.Empty);
        }

        return new ActionOutcome(null, FormatOutput(commit), "COMMIT 完成");
    }

    private async Task<ActionOutcome> PushAsync(
        string localPath,
        RepoRef repo,
        ActionArguments arguments,
        IReadOnlyDictionary<string, string>? credentialEnvironment,
        CancellationToken cancellationToken)
    {
        var state = NormalizeState(repo.Repo.State);

        // FR-REPO-05 / D-025 §2.5④：EMPTY 首推 + NO_UPSTREAM 发布分支都必须建立 upstream
        // （git push -u origin HEAD）；UI 确认条传入 setUpstream=true，后端对这两态再兜底一次。
        var firstPush = state == "EMPTY";
        var setUpstream = arguments.SetUpstream || firstPush || state == "NO_UPSTREAM";

        if (firstPush)
        {
            // E7 已证明 unborn HEAD 直接 push 必失败（src refspec HEAD does not match any）：
            // 在启动网络 git 前给出可读错误码，避免“假成功/难懂 stderr”。
            var headCheck = await _git
                .RunAsync(localPath, new[] { "rev-parse", "--verify", "--quiet", "HEAD" }, cancellationToken)
                .ConfigureAwait(false);
            if (!headCheck.Success || headCheck.TrimmedStdout.Length == 0)
            {
                return new ActionOutcome(
                    CommandResult.Failure(
                        BridgeErrorCodes.RefNotFound,
                        "EMPTY 空仓还没有首个提交（unborn HEAD），无法首推；请先在按钮条 COMMIT 生成首个提交后重试"
                        + "（E7：unborn HEAD 直接 push 必失败）",
                        new Dictionary<string, object?>
                        {
                            ["action"] = "PUSH",
                            ["state"] = "EMPTY",
                            ["requiresCommit"] = true
                        }),
                    string.Empty,
                    string.Empty);
            }
        }

        // 禁止 force：只允许 origin HEAD（首次推送/发布分支附加 -u 建立 upstream）。
        var pushArguments = new List<string> { "push" };
        if (setUpstream)
        {
            pushArguments.Add("-u");
        }

        pushArguments.Add("origin");
        pushArguments.Add("HEAD");

        var result = await _git
            .RunAsync(
                localPath,
                pushArguments,
                cancellationToken,
                NetworkTimeout,
                credentialEnvironment)
            .ConfigureAwait(false);

        if (!result.Success)
        {
            if (IsPushRejected(result))
            {
                return new ActionOutcome(
                    FailWithSummary(
                        BridgeErrorCodes.PushRejected,
                        "PUSH 被远端拒绝（非快进/受保护分支等），请 FETCH/PULL 后重试；远端未被强制覆盖",
                        "PUSH",
                        result,
                        new Dictionary<string, object?>
                        {
                            ["sdkCode"] = "GIT_PUSH_REJECTED",
                            ["hint"] = "先 git pull --ff-only 合并远端提交，再重新 PUSH"
                        }),
                    string.Empty,
                    string.Empty);
            }

            return new ActionOutcome(MapGitFailure("PUSH", result, cancellationToken), string.Empty, string.Empty);
        }

        return new ActionOutcome(
            null,
            FormatOutput(result),
            setUpstream ? "PUSH 完成（已建立 upstream；首次推送/发布分支）" : "PUSH 完成");
    }

    /// <summary>M2 P0-1：凭据注入前的 fail-closed 作用域校验（锁内执行，只读、不注凭据）。</summary>
    /// <remarks>
    /// M2b：网络类批量作业（SYNC 等）复用同一套校验（origin fetch/pushurl 与 Gitea base 作用域一致），
    /// 因此由 <c>private</c> 提升为 <c>public</c>；调用方不得记录返回的环境变量值（含 token）。
    /// </remarks>
    public async Task<(CommandResult? Failure, IReadOnlyDictionary<string, string>? Environment)>
        PrepareCredentialEnvironmentAsync(
            string localPath,
            string action,
            CancellationToken cancellationToken)
    {
        var token = _giteaToken;
        if (token is null)
        {
            // 无 token：公开仓/系统凭据助手路径，不注入、不校验（无凭据可外发）。
            return (null, null);
        }

        var configurationFailure = ValidateCredentialConfiguration(action, out var baseUri);
        if (configurationFailure is not null)
        {
            return (configurationFailure, null);
        }

        // 2) 读取 fetch URL 与 pushurl（只读，不注凭据）。
        var fetchUrl = await _git
            .RunAsync(localPath, new[] { "remote", "get-url", "origin" }, cancellationToken)
            .ConfigureAwait(false);
        var pushUrl = await _git
            .RunAsync(localPath, new[] { "remote", "get-url", "--push", "origin" }, cancellationToken)
            .ConfigureAwait(false);

        if (!fetchUrl.Success || fetchUrl.TrimmedStdout.Length == 0)
        {
            return (CredentialScopeFailure(
                action,
                baseUri!,
                _giteaOrg!.Trim(),
                fetchUrl: null,
                pushUrl: pushUrl.Success ? pushUrl.TrimmedStdout : null,
                failedFields: new[] { "fetchUrl" },
                reason: "无法读取 origin fetch URL"), null);
        }

        if (!pushUrl.Success || pushUrl.TrimmedStdout.Length == 0)
        {
            return (CredentialScopeFailure(
                action,
                baseUri!,
                _giteaOrg!.Trim(),
                fetchUrl: fetchUrl.TrimmedStdout,
                pushUrl: null,
                failedFields: new[] { "pushUrl" },
                reason: "无法读取 origin push URL"), null);
        }

        var org = _giteaOrg!.Trim();
        var fetchCheck = CheckUrlScope(fetchUrl.TrimmedStdout, baseUri!, org);
        var pushCheck = CheckUrlScope(pushUrl.TrimmedStdout, baseUri!, org);
        if (!fetchCheck.Matches || !pushCheck.Matches)
        {
            var failed = new List<string>();
            failed.AddRange(fetchCheck.FailedFields.Select(field => "fetch." + field));
            failed.AddRange(pushCheck.FailedFields.Select(field => "push." + field));

            return (CredentialScopeFailure(
                action,
                baseUri!,
                org,
                fetchUrl: fetchUrl.TrimmedStdout,
                pushUrl: pushUrl.TrimmedStdout,
                failedFields: failed,
                reason: "origin fetch URL / pushurl 与 Gitea baseUrl（scheme+host+port）或 /<org>/ 路径不一致"), null);
        }

        return (null, BuildCredentialEnvironment(baseUri!, token));
    }

    /// <summary>
    /// M2b：CLONE 前构造 PAT 注入环境（无本地仓、无 origin 可比对，只做配置完整性/作用域 base 校验）。
    /// <para>返回非 null 表示 fail-closed（<c>E_GITEA_CONFIG_INVALID</c> / <c>E_GITEA_INSECURE_HTTP</c>），
    /// 调用方不得启动网络 git；返回 null 时 <paramref name="environment"/> 可能为 null（无 token）或仅含
    /// URL 作用域的 <c>http.&lt;base&gt;.extraHeader</c>（绝不进入 argv/日志/响应）。</para>
    /// </summary>
    public CommandResult? TryBuildCloneCredentialEnvironment(
        out IReadOnlyDictionary<string, string>? environment)
    {
        environment = null;
        var token = _giteaToken;
        if (token is null)
        {
            return null;
        }

        var configurationFailure = ValidateCredentialConfiguration("CLONE", out var baseUri);
        if (configurationFailure is not null)
        {
            return configurationFailure;
        }

        environment = BuildCredentialEnvironment(baseUri!, token);
        return null;
    }

    /// <summary>
    /// 凭据配置完整性/作用域校验（fail-closed）：token 非空时必须配置合法的 Gitea baseUrl（http/https，无 userinfo）
    /// 与 org；明文 http 需显式 allowInsecureHttp。
    /// </summary>
    private CommandResult? ValidateCredentialConfiguration(string action, out Uri? baseUri)
    {
        baseUri = null;

        // 1) 配置完整性：token 非空但 Gitea base/org 缺失或非法 → fail closed（绝不退化为“公开仓继续注入”）。
        var invalidFields = new List<string>();
        if (string.IsNullOrWhiteSpace(_giteaBaseUrl))
        {
            invalidFields.Add("giteaBaseUrl");
        }

        if (string.IsNullOrWhiteSpace(_giteaOrg))
        {
            invalidFields.Add("giteaOrg");
        }

        if (invalidFields.Count > 0)
        {
            return GiteaConfigInvalid(action, invalidFields, "未配置 Gitea baseUrl/org，禁止携带 PAT 执行网络 git 操作");
        }

        if (!Uri.TryCreate(_giteaBaseUrl!.Trim(), UriKind.Absolute, out var parsedBaseUri)
            || (parsedBaseUri.Scheme != Uri.UriSchemeHttp && parsedBaseUri.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrEmpty(parsedBaseUri.Host))
        {
            return GiteaConfigInvalid(
                action,
                new[] { "giteaBaseUrl" },
                "Gitea baseUrl 非法（需 http(s)://host[:port]），禁止携带 PAT 执行网络 git 操作");
        }

        if (!string.IsNullOrEmpty(parsedBaseUri.UserInfo))
        {
            return GiteaConfigInvalid(
                action,
                new[] { "giteaBaseUrl" },
                "Gitea baseUrl 不允许包含 userinfo（凭据），禁止携带 PAT 执行网络 git 操作");
        }

        var org = _giteaOrg!.Trim();
        if (org.Length == 0
            || org.Contains('/')
            || org.Contains('\\')
            || org.Contains("..", StringComparison.Ordinal)
            || org.Any(char.IsWhiteSpace)
            || org.Any(char.IsControl))
        {
            return GiteaConfigInvalid(
                action,
                new[] { "giteaOrg" },
                "Gitea org 非法，禁止携带 PAT 执行网络 git 操作");
        }

        if (parsedBaseUri.Scheme == Uri.UriSchemeHttp && !_allowInsecureHttp)
        {
            return CommandResult.Failure(
                BridgeErrorCodes.GiteaInsecureHttp,
                "Gitea baseUrl 为明文 http，默认禁止注入 PAT（Authorization: Basic 可被同网段读取/重放）；"
                + "确需内网 http 请显式设置 config.json gitea.allowInsecureHttp=true 或环境变量 MBD_GITEA_ALLOW_INSECURE_HTTP=true",
                new Dictionary<string, object?>
                {
                    ["action"] = action,
                    ["scheme"] = parsedBaseUri.Scheme,
                    ["host"] = parsedBaseUri.Host,
                    ["port"] = parsedBaseUri.Port,
                    ["sdkCode"] = "GITEA_INSECURE_HTTP"
                });
        }

        baseUri = parsedBaseUri;
        return null;
    }

    /// <summary>URL 作用域 header（仅发往 canonical base 的请求携带 Authorization；值不进日志/响应）。</summary>
    private static IReadOnlyDictionary<string, string> BuildCredentialEnvironment(Uri baseUri, string token)
    {
        var canonicalBase = baseUri.GetLeftPart(UriPartial.Path).TrimEnd('/');
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes(token + ":"));
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["GIT_CONFIG_COUNT"] = "1",
            ["GIT_CONFIG_KEY_0"] = "http." + canonicalBase + "/.extraHeader",
            ["GIT_CONFIG_VALUE_0"] = "Authorization: Basic " + basic
        };
    }

    /// <summary>M2b：批量 CREATE/TAG 的 ref 名校验（与单仓 CHKOUT 同一套规则，返回 null 表示合法）。</summary>
    public static CommandResult? ValidateRefArgument(string value, string field) => ValidateRefName(value, field);

    private static UrlScopeCheck CheckUrlScope(string rawUrl, Uri baseUri, string org)
    {
        var failed = new List<string>();
        if (!Uri.TryCreate(rawUrl.Trim(), UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host))
        {
            return new UrlScopeCheck(false, null, null, null, false, new[] { "url" });
        }

        if (!uri.Scheme.Equals(baseUri.Scheme, StringComparison.OrdinalIgnoreCase))
        {
            failed.Add("scheme");
        }

        if (!uri.Host.Equals(baseUri.Host, StringComparison.OrdinalIgnoreCase))
        {
            failed.Add("host");
        }

        if (uri.Port != baseUri.Port)
        {
            failed.Add("port");
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            failed.Add("userinfo");
        }

        var basePath = baseUri.AbsolutePath.TrimEnd('/');
        var expectedPrefix = basePath + "/" + org + "/";
        var actualPath = uri.AbsolutePath;
        if (!actualPath.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase)
            || actualPath.Length <= expectedPrefix.Length)
        {
            failed.Add("path");
        }

        return new UrlScopeCheck(
            failed.Count == 0,
            uri.Scheme,
            uri.Host,
            uri.Port,
            string.IsNullOrEmpty(uri.UserInfo),
            failed);
    }

    private CommandResult GiteaConfigInvalid(string action, IReadOnlyList<string> fields, string message) =>
        CommandResult.Failure(
            BridgeErrorCodes.GiteaConfigInvalid,
            message,
            new Dictionary<string, object?>
            {
                ["action"] = action,
                ["fields"] = fields,
                ["sdkCode"] = "GITEA_CONFIG_INVALID"
            });

    private CommandResult CredentialScopeFailure(
        string action,
        Uri baseUri,
        string org,
        string? fetchUrl,
        string? pushUrl,
        IReadOnlyList<string> failedFields,
        string reason)
    {
        var basePath = baseUri.AbsolutePath.TrimEnd('/');

        // 仅返回 scheme/host/port/路径前缀等非敏感匹配字段；绝不回显 URL（可能内嵌旧凭据）。
        var details = new Dictionary<string, object?>
        {
            ["action"] = action,
            ["reason"] = reason,
            ["expectedScheme"] = baseUri.Scheme,
            ["expectedHost"] = baseUri.Host,
            ["expectedPort"] = baseUri.Port,
            ["expectedPathPrefix"] = basePath + "/" + org + "/",
            ["fetchUrlPresent"] = !string.IsNullOrEmpty(fetchUrl),
            ["pushUrlPresent"] = !string.IsNullOrEmpty(pushUrl),
            ["failedFields"] = failedFields,
            ["sdkCode"] = "GIT_CREDENTIAL_SCOPE"
        };

        return CommandResult.Failure(
            BridgeErrorCodes.CredentialScope,
            "拒绝注入 PAT：" + reason + "（未启动网络 git）",
            details);
    }

    private async Task<string?> ReadHeadAsync(string localPath, CancellationToken cancellationToken)
    {
        var result = await _git
            .RunAsync(localPath, new[] { "rev-parse", "HEAD" }, cancellationToken)
            .ConfigureAwait(false);
        if (!result.Success)
        {
            return null; // EMPTY（unborn HEAD）等：preHead/postHead 允许为空。
        }

        var head = result.TrimmedStdout;
        return head.Length is > 0 and <= 64 ? head : null;
    }

    private async Task<string?> ReadCurrentBranchAsync(string localPath, CancellationToken cancellationToken)
    {
        var result = await _git
            .RunAsync(localPath, new[] { "rev-parse", "--abbrev-ref", "HEAD" }, cancellationToken)
            .ConfigureAwait(false);
        if (!result.Success)
        {
            return null;
        }

        var branch = result.TrimmedStdout;
        if (branch.Length == 0 || branch.Equals("HEAD", StringComparison.Ordinal))
        {
            return null; // detached HEAD
        }

        return branch;
    }

    /// <summary>M2 P1-2：快照 HEAD 与当前 HEAD 的 CAS 校验（不一致 → 零改动拒绝）。</summary>
    private static CommandResult? CheckSnapshotHead(string action, RepoRef repo, string? currentHead)
    {
        var snapshotHead = string.IsNullOrWhiteSpace(repo.Repo.HeadSha) ? null : repo.Repo.HeadSha.Trim();
        if (string.Equals(snapshotHead ?? string.Empty, currentHead ?? string.Empty, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return CommandResult.Failure(
            BridgeErrorCodes.ConcurrentChange,
            "仓库 HEAD 已变化（快照过期/外部操作），已拒绝执行且未做任何修改；请刷新后重试",
            new Dictionary<string, object?>
            {
                ["action"] = action,
                ["state"] = NormalizeState(repo.Repo.State),
                ["snapshotHead"] = snapshotHead,
                ["currentHead"] = currentHead,
                ["sdkCode"] = "GIT_CONCURRENT_CHANGE"
            });
    }

    private CommandResult MapGitFailure(string action, GitResult result, CancellationToken cancellationToken)
    {
        if (result.Canceled)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            return CommandResult.Failure(
                BridgeErrorCodes.GitFailed,
                $"{action} 已取消",
                new Dictionary<string, object?>
                {
                    ["action"] = action,
                    ["sdkCode"] = "GIT_CANCELLED"
                });
        }

        if (result.TimedOut)
        {
            return CommandResult.Failure(
                BridgeErrorCodes.Timeout,
                $"{action} 执行超时（git 进程已终止）",
                new Dictionary<string, object?>
                {
                    ["action"] = action,
                    ["sdkCode"] = "GIT_TIMEOUT"
                });
        }

        return FailWithSummary(
            BridgeErrorCodes.GitFailed,
            $"{action} 失败（exit={result.ExitCode}）",
            action,
            result,
            new Dictionary<string, object?> { ["sdkCode"] = "GIT_FAILED" });
    }

    private CommandResult FailWithSummary(
        string code,
        string message,
        string action,
        GitResult result,
        IReadOnlyDictionary<string, object?>? extraDetails = null)
    {
        var summary = SummarizeGitError(result);
        var details = new Dictionary<string, object?>
        {
            ["action"] = action,
            ["exitCode"] = result.ExitCode,
            ["stderr"] = summary
        };
        if (extraDetails is not null)
        {
            foreach (var pair in extraDetails)
            {
                details[pair.Key] = pair.Value;
            }
        }

        return CommandResult.Failure(code, $"{message}：{summary}", details);
    }

    private CommandResult OverwriteFailure(string action, GitResult result) =>
        FailWithSummary(
            BridgeErrorCodes.WorktreeOverwrite,
            $"{action} 被拒绝：存在会被覆盖的本地文件（含 .gitignore 忽略文件）",
            action,
            result,
            new Dictionary<string, object?>
            {
                ["sdkCode"] = "GIT_WORKTREE_OVERWRITE",
                ["hint"] = "请先备份/移走或提交这些本地文件，再重试（Host 不会静默覆盖）"
            });

    private CommandResult NothingToCommit() => CommandResult.Failure(
        BridgeErrorCodes.NothingToCommit,
        "没有可提交的变更",
        new Dictionary<string, object?>
        {
            ["action"] = "COMMIT",
            ["sdkCode"] = "GIT_NOTHING_TO_COMMIT"
        });

    private static CommandResult InvalidArgument(string message, string field) => CommandResult.Failure(
        BridgeErrorCodes.InvalidArgument,
        message,
        new Dictionary<string, object?> { ["field"] = field });

    private static CommandResult RefNotFound(string kind, string value) => CommandResult.Failure(
        BridgeErrorCodes.RefNotFound,
        $"{kind} 不存在：{value}（未检出，HEAD 保持不变）",
        new Dictionary<string, object?>
        {
            ["field"] = kind,
            ["kind"] = kind,
            ["value"] = value,
            ["sdkCode"] = "GIT_REF_NOT_FOUND"
        });

    private static bool IsNetworkAction(string action) =>
        NetworkActions.Contains(action, StringComparer.Ordinal);

    private static bool IsWriteAction(string action) =>
        WriteActions.Contains(action, StringComparer.Ordinal);

    private static bool IsNonFastForward(GitResult result)
    {
        var text = (result.Stderr + "\n" + result.Stdout).ToLowerInvariant();
        return text.Contains("not possible to fast-forward", StringComparison.Ordinal)
            || text.Contains("non-fast-forward", StringComparison.Ordinal)
            || text.Contains("not a fast forward", StringComparison.Ordinal);
    }

    /// <summary>P1-1：git 因“本地文件会被覆盖”拒绝（ignored/untracked 冲突）。</summary>
    private static bool IsOverwriteRejection(GitResult result)
    {
        var text = (result.Stderr + "\n" + result.Stdout).ToLowerInvariant();
        return text.Contains("would be overwritten", StringComparison.Ordinal)
            || text.Contains("please move or remove them", StringComparison.Ordinal)
            || text.Contains("overwrite ignored", StringComparison.Ordinal);
    }

    private static bool IsPushRejected(GitResult result)
    {
        var text = (result.Stderr + "\n" + result.Stdout).ToLowerInvariant();
        return text.Contains("[rejected]", StringComparison.Ordinal)
            || text.Contains("non-fast-forward", StringComparison.Ordinal)
            || text.Contains("fetch first", StringComparison.Ordinal)
            || text.Contains("protected branch", StringComparison.Ordinal);
    }

    private string FormatOutput(GitResult result) =>
        Truncate(RedactSecrets(Combine(result.Stdout, result.Stderr)));

    private string SummarizeGitError(GitResult result)
    {
        var text = Combine(result.Stderr, result.Stdout);
        if (text.Length == 0)
        {
            return "（无 stderr 输出）";
        }

        var lines = text
            .Split('\n')
            .Select(line => line.TrimEnd())
            .Where(line => line.Trim().Length > 0)
            .ToList();
        if (lines.Count == 0)
        {
            return "（无 stderr 输出）";
        }

        // P1-6：优先保留关键行（fatal/error/rejected 等），再补充最后 ≤20 行；不再只看首行。
        var selected = new List<string>();
        foreach (var line in lines.Where(IsKeyErrorLine).TakeLast(8))
        {
            AddIfMissing(selected, line);
        }

        foreach (var line in lines.TakeLast(MaxSummaryLines))
        {
            AddIfMissing(selected, line);
        }

        var summary = RedactSecrets(string.Join("\n", selected));
        return summary.Length <= MaxErrorSummaryChars ? summary : summary[..(MaxErrorSummaryChars - 1)] + "…";
    }

    private static bool IsKeyErrorLine(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.StartsWith("fatal:", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("error:", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("! [rejected]", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("[rejected]", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("hint:", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("remote:", StringComparison.OrdinalIgnoreCase);
    }

    private static void AddIfMissing(List<string> lines, string line)
    {
        if (!lines.Contains(line, StringComparer.Ordinal))
        {
            lines.Add(line);
        }
    }

    private static string Combine(string stdout, string stderr)
    {
        var left = NormalizeNewLines(stdout).Trim();
        var right = NormalizeNewLines(stderr).Trim();
        if (left.Length == 0)
        {
            return right;
        }

        return right.Length == 0 ? left : left + "\n" + right;
    }

    private static string NormalizeNewLines(string? text) =>
        string.IsNullOrEmpty(text) ? string.Empty : text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private static string Truncate(string text) =>
        text.Length <= MaxOutputChars ? text : text[..(MaxOutputChars - 1)] + "…";

    /// <summary>token/Authorization/URL userinfo 脱敏（响应、日志、错误 details 统一走这里）。</summary>
    private string RedactSecrets(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var redacted = text;
        var token = _giteaToken;
        if (token is not null)
        {
            redacted = redacted.Replace(token, "***", StringComparison.Ordinal);
            var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes(token + ":"));
            redacted = redacted.Replace(basic, "***", StringComparison.Ordinal);
        }

        return HostLog.Redact(redacted);
    }

    private static bool IsGitRepository(string localPath)
    {
        if (Directory.Exists(Path.Combine(localPath, ".git")))
        {
            return true;
        }

        // worktree/submodule 场景 .git 为文件（gitdir: ...）。
        return File.Exists(Path.Combine(localPath, ".git"));
    }

    private static CommandResult? TryParseArguments(
        IReadOnlyDictionary<string, JsonElement>? raw,
        string action,
        out ActionArguments arguments)
    {
        arguments = new ActionArguments(null, null, null, false, false, false, false);
        if (raw is null || raw.Count == 0)
        {
            return ValidateRequiredArguments(action, arguments);
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

        error = ReadBool(raw, "confirm", out var confirm);
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

        if (branch is not null)
        {
            var branchError = ValidateRefName(branch, "branch");
            if (branchError is not null)
            {
                return branchError;
            }
        }

        if (tag is not null)
        {
            var tagError = ValidateRefName(tag, "tag");
            if (tagError is not null)
            {
                return tagError;
            }
        }

        if (message is not null)
        {
            message = message.Trim();
            if (message.Length > MaxCommitMessageChars)
            {
                return InvalidArgument($"参数 message 超长（≤{MaxCommitMessageChars} 字符）", "message");
            }
        }

        arguments = new ActionArguments(branch, tag, message, confirm, setUpstream, stash, stageAll);
        return ValidateRequiredArguments(action, arguments);
    }

    private static CommandResult? ValidateRequiredArguments(string action, ActionArguments arguments)
    {
        switch (action)
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

                return null;

            case "COMMIT":
                if (string.IsNullOrWhiteSpace(arguments.Message))
                {
                    return InvalidArgument("COMMIT 需要非空 message 参数", "message");
                }

                return null;

            default:
                return null;
        }
    }

    private static CommandResult? ReadString(
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

        var text = element.GetString();
        if (text is not null)
        {
            text = text.Trim();
        }

        value = string.IsNullOrEmpty(text) ? null : text;
        return null;
    }

    private static CommandResult? ReadBool(
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

    /// <summary>保守的 ref 名校验（只做安全收口；git 仍是最终裁判）。拒绝控制字符/空白/危险符号/选项注入。</summary>
    private static CommandResult? ValidateRefName(string value, string field)
    {
        if (value.Length > 255)
        {
            return InvalidArgument($"参数 {field} 超长（≤255 字符）", field);
        }

        if (value[0] is '-' or '+')
        {
            return InvalidArgument($"参数 {field} 不能以 '-' 或 '+' 开头", field);
        }

        foreach (var ch in value)
        {
            if (char.IsWhiteSpace(ch) || char.IsControl(ch))
            {
                return InvalidArgument($"参数 {field} 不能包含空白/控制字符", field);
            }

            if (ch is '~' or '^' or ':' or '?' or '*' or '[' or '\\')
            {
                return InvalidArgument($"参数 {field} 包含非法字符 {ch}", field);
            }
        }

        if (value.Contains("..", StringComparison.Ordinal)
            || value.Contains("@{", StringComparison.Ordinal)
            || value.EndsWith('.')
            || value.EndsWith('/')
            || value.EndsWith(".lock", StringComparison.OrdinalIgnoreCase))
        {
            return InvalidArgument($"参数 {field} 不是合法的分支/Tag 名", field);
        }

        return null;
    }

    private static string NormalizeState(string? state) =>
        string.IsNullOrWhiteSpace(state) ? "UNKNOWN" : state.Trim().ToUpperInvariant();

    private static string DisabledReason(string state, string action) => (state, action) switch
    {
        ("DIRTY", "PULL") => "DIRTY：需先提交或 stash",
        ("DIVERGED", "PULL") => "DIVERGED：需手动 Merge/Rebase",
        ("UNKNOWN", "PULL") => "UNKNOWN：关系数据过期，先 FETCH",
        ("AHEAD", "PULL") => "AHEAD：需先推送或手动处理",
        ("NO_UPSTREAM", "PULL") => "NO_UPSTREAM：未配置 upstream",
        ("EMPTY", "PULL") => "EMPTY：无首个提交",
        ("UNMERGED", "PULL") => "UNMERGED：存在未解决冲突",
        ("AHEAD", "COMMIT") => "AHEAD：需先推送/手动处理",
        ("BEHIND", "COMMIT") => "BEHIND：先 PULL",
        ("UNMERGED", "COMMIT") => "UNMERGED：存在未解决冲突",
        ("IN_PROGRESS", "COMMIT") => "IN_PROGRESS：合并/变基未完成",
        ("BEHIND", "PUSH") => "BEHIND：先 PULL",
        ("DETACHED_TAG", "PUSH") => "DETACHED：请先基于当前 HEAD 创建分支",
        ("DETACHED_COMMIT", "PUSH") => "DETACHED：请先基于当前 HEAD 创建分支",
        ("UNMERGED", "PUSH") => "UNMERGED：存在未解决冲突",
        ("IN_PROGRESS", "FETCH") => "IN_PROGRESS：操作未完成",
        ("INVALID", "FETCH") => "INVALID：仓库绑定异常",
        ("MISSING", "CHKOUT") => "MISSING：仓库未克隆",
        ("IN_PROGRESS", "CHKOUT") => "IN_PROGRESS：操作未完成",
        _ => $"当前状态 {state} 不允许 {action}（spec §6.2）"
    };

    /// <summary>解析后的 args（branch/tag/message/confirm/setUpstream/stash/stageAll）。</summary>
    private sealed record ActionArguments(
        string? Branch,
        string? Tag,
        string? Message,
        bool Confirm,
        bool SetUpstream,
        bool Stash,
        bool StageAll);

    /// <summary>动作执行结果（Failure 非 null 即失败；StashRef 仅 CHKOUT 自动 stash 时非空）。</summary>
    private sealed record ActionOutcome(
        CommandResult? Failure,
        string Output,
        string Message,
        string? StashRef = null);

    /// <summary>origin URL 与 Gitea base 的作用域比对结果（用于可诊断的 E_CREDENTIAL_SCOPE details）。</summary>
    private sealed record UrlScopeCheck(
        bool Matches,
        string? Scheme,
        string? Host,
        int? Port,
        bool NoUserInfo,
        IReadOnlyList<string> FailedFields);
}

/// <summary>
/// 一次 <c>runRepoAction</c> 的执行审计摘要（G-P0-2/AC-OP-33）：成功/失败/异常都会产生；
/// 仅包含可审计元数据，<b>不含 token / 命令 output / stderr</b>。
/// </summary>
public sealed class RepoActionExecution
{
    public string? Action { get; set; }

    public string? ComponentId { get; set; }

    public string? RepoType { get; set; }

    public string State { get; set; } = "UNKNOWN";

    public string? PreHead { get; set; }

    public string? PostHead { get; set; }

    public bool Ok { get; set; }

    public string? ErrorCode { get; set; }

    public long DurationMs { get; set; }

    /// <summary>操作者（审计 operation_log.actor）。</summary>
    public string? Actor { get; set; }

    /// <summary>请求关联 ID（审计 operation_log.request_id）。</summary>
    public string? RequestId { get; set; }

    /// <summary>M2b：作业 ID（批量作业内单仓动作的审计关联；单仓动作/路径为 null）。</summary>
    public string? JobId { get; set; }

    /// <summary>M2b：作业 item ID（审计 job_item 关联；单仓动作/路径为 null）。</summary>
    public string? JobItemId { get; set; }

    /// <summary>M2b：审计结果名（OK/FAILED/SKIPPED/CANCELED/INTERRUPTED）；null 时由 ok 推导。</summary>
    public string? Result { get; set; }

    /// <summary>M2b：SYNC 等操作的备份 ref（审计可追溯；无则为 null）。</summary>
    public string? BackupRef { get; set; }
}
