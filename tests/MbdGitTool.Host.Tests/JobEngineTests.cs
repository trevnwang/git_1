using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MbdGitTool.Core.Contracts;
using MbdGitTool.Core.Domain;
using MbdGitTool.Core.Security;
using MbdGitTool.Host.Bridge;
using MbdGitTool.Host.Services;

namespace MbdGitTool.Host.Tests;

/// <summary>
/// M2b 作业内核 + 批量操作（SRS §9.7 FR-OP / §8.1 job/job_item / §13.1.2 AC-UI-10c）。
/// <para>全部使用临时 bare remote + 临时工作区（无网络/无真实 Gitea）：job 成功/PARTIAL/失败、取消、
/// 仅重试失败项、持久化重启 INTERRUPTED、SYNC 备份 ref、DELETE→RESTORE、CLONE（本地 file:// bare remote）、
/// CREATE/TAG、审计每 item 1 行含 jobId、token 不泄漏、路径逃逸拒绝、事件 JOB_UPDATED。</para>
/// </summary>
public sealed class JobEngineTests
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);
    private const string TestToken = "ghp_m2b_fake_token_0123456789abcdef";

    // ------------------------------------------------------------------ job 成功 / 查询 / 事件 / 审计

    [Fact]
    public async Task RunBatch_Item_ExposesExpectHead()
    {
        using var fixture = BatchWorkspace.Create("CF216A_Expect_IMPL");
        var config = TestAudit.NewConfig(fixture.Root);
        using var snapshots = new SnapshotService(config);
        using var router = new HostCommandRouter(config, snapshots, new RecordingLauncher());
        await DispatchAsync(router, "getSnapshot", null);
        var head = TestGit.Capture(fixture.Clones["CF216A_Expect_IMPL"], "rev-parse", "HEAD");

        var batch = await DispatchAsync(router, "runBatch", new
        {
            kind = "FETCH",
            componentIds = new[] { "CF216A_Expect" },
            repoTypes = new[] { "IMPL" }
        });
        var accepted = Assert.IsType<RunBatchResultDto>(Assert.IsType<CommandResult>(batch).Data);
        var job = await WaitJobAsync(router, accepted.JobId);
        var item = Assert.Single(job.Items);
        Assert.Equal(head, item.ExpectHead);
        Assert.Equal(item.PreHead, item.ExpectHead);

        var loaded = await DispatchAsync(router, "getJob", new { jobId = accepted.JobId });
        var loadedJob = Assert.IsType<JobDto>(Assert.IsType<CommandResult>(loaded).Data);
        Assert.Equal(head, Assert.Single(loadedJob.Items).ExpectHead);
    }
    [Fact]
    public async Task RunBatch_Fetch_Succeeds_EmitsEventsAndAuditsOneRowPerItem()
    {
        using var fixture = BatchWorkspace.Create("CF100A_Demo_Impl", "CF100A_Demo_SIL");
        var config = TestAudit.NewConfig(fixture.Root);
        using var snapshots = new SnapshotService(config);
        using var router = new HostCommandRouter(config, snapshots, new RecordingLauncher());
        await DispatchAsync(router, "getSnapshot", null);

        var events = new ConcurrentBag<object>();
        router.EventSink = envelope => events.Add(envelope);

        var batch = await DispatchAsync(
            router,
            "runBatch",
            new { kind = "FETCH", componentIds = new[] { "CF100A_Demo" }, repoTypes = new[] { "IMPL", "SIL" } });

        Assert.True(batch.Ok, Describe(batch));
        var accepted = Assert.IsType<RunBatchResultDto>(batch.Data);
        Assert.True(accepted.Accepted);
        Assert.Equal(2, accepted.Total);
        Assert.Equal(0, accepted.Skipped);
        Assert.False(string.IsNullOrWhiteSpace(accepted.JobId));

        var job = await WaitJobAsync(router, accepted.JobId);
        Assert.Equal("SUCCESS", job.State);
        Assert.Equal(2, job.Total);
        Assert.Equal(2, job.Ok);
        Assert.Equal(0, job.Failed);
        Assert.Equal(0, job.Skipped);
        Assert.False(job.Cancellable);
        Assert.NotNull(job.StartedAt);
        Assert.NotNull(job.FinishedAt);
        Assert.All(job.Items, item => Assert.Equal("OK", item.State));
        Assert.All(job.Items, item => Assert.NotNull(item.PostHead));

        // getJobs / getJob 契约。
        var jobsResult = await DispatchAsync(router, "getJobs", new { limit = 10 });
        Assert.True(jobsResult.Ok);
        var jobs = Assert.IsType<JobsDto>(jobsResult.Data);
        Assert.Contains(jobs.Jobs, entry => entry.Id == accepted.JobId);

        var single = await DispatchAsync(router, "getJob", new { jobId = accepted.JobId });
        Assert.True(single.Ok);
        Assert.Equal(accepted.JobId, Assert.IsType<JobDto>(single.Data).Id);

        // JOB_UPDATED 事件信封（AC-UI-10c② 固定形状）：
        // { event:"JOB_UPDATED", jobId, current:JobDto, data:JobDto, state, total, cancellable }。
        var envelopes = events
            .Select(envelope => JsonSerializer.Serialize(envelope, WebJson))
            .Select(json => JsonDocument.Parse(json))
            .ToList();
        try
        {
            Assert.NotEmpty(envelopes);
            Assert.All(envelopes, document =>
            {
                var root = document.RootElement;
                Assert.Equal("JOB_UPDATED", root.GetProperty("event").GetString());
                Assert.Equal(accepted.JobId, root.GetProperty("jobId").GetString());
                Assert.Equal(accepted.JobId, root.GetProperty("current").GetProperty("id").GetString());
                Assert.Equal(accepted.JobId, root.GetProperty("data").GetProperty("id").GetString());
                Assert.Equal(
                    root.GetProperty("current").GetProperty("state").GetString(),
                    root.GetProperty("data").GetProperty("state").GetString());
                Assert.Equal(
                    root.GetProperty("current").GetProperty("state").GetString(),
                    root.GetProperty("state").GetString());
                Assert.Equal(
                    root.GetProperty("current").GetProperty("total").GetInt32(),
                    root.GetProperty("total").GetInt32());
                Assert.Equal(
                    root.GetProperty("current").GetProperty("cancellable").GetBoolean(),
                    root.GetProperty("cancellable").GetBoolean());
            });
            Assert.Contains(envelopes, document =>
                document.RootElement.GetProperty("state").GetString() == "SUCCESS");
            Assert.All(envelopes, document =>
                Assert.False(document.RootElement.GetProperty("current").GetProperty("dryRun").GetBoolean()));
        }
        finally
        {
            envelopes.ForEach(document => document.Dispose());
        }

        // 审计：每 item 恰 1 行，均含 jobId/jobItemId/result，且无 token/Authorization。
        var lines = ReadAuditLines(config);
        Assert.Equal(2, lines.Count);
        var jobItemIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in lines)
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            Assert.Equal("FETCH", root.GetProperty("action").GetString());
            Assert.Equal(accepted.JobId, root.GetProperty("jobId").GetString());
            Assert.Equal("OK", root.GetProperty("result").GetString());
            Assert.True(root.GetProperty("ok").GetBoolean());
            Assert.True(jobItemIds.Add(root.GetProperty("jobItemId").GetString()!));
            Assert.DoesNotContain(TestToken, line, StringComparison.Ordinal);
            Assert.DoesNotContain("Authorization", line, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ------------------------------------------------------------------ PARTIAL / 仅重试失败项

    [Fact]
    public async Task RunBatch_Commit_Partial_Then_RetryFailedOnly()
    {
        using var fixture = BatchWorkspace.Create("CF100A_Demo_Impl", "CF100A_Demo_SIL");
        var config = TestAudit.NewConfig(fixture.Root);
        using var snapshots = new SnapshotService(config);
        using var router = new HostCommandRouter(config, snapshots, new RecordingLauncher());
        await DispatchAsync(router, "getSnapshot", null);

        // 只给 IMPL 暂存一个文件；SIL 干净 → E_NOTHING_TO_COMMIT → job PARTIAL。
        File.WriteAllText(Path.Combine(fixture.Clones["CF100A_Demo_Impl"], "impl-staged.txt"), "impl");
        TestGit.Run(fixture.Clones["CF100A_Demo_Impl"], "add", "impl-staged.txt");

        var batch = await DispatchAsync(
            router,
            "runBatch",
            new
            {
                kind = "COMMIT",
                componentIds = new[] { "CF100A_Demo" },
                repoTypes = new[] { "IMPL", "SIL" },
                args = new { message = "m2b partial", confirm = true }
            });
        Assert.True(batch.Ok, Describe(batch));
        var accepted = Assert.IsType<RunBatchResultDto>(batch.Data);

        var job = await WaitJobAsync(router, accepted.JobId);
        Assert.Equal("PARTIAL", job.State);
        Assert.Equal(1, job.Ok);
        Assert.Equal(1, job.Failed);
        var failedItem = Assert.Single(job.Items, item => item.State == "FAILED");
        Assert.Equal(BridgeErrorCodes.NothingToCommit, failedItem.ErrorCode);
        Assert.Equal("SIL", failedItem.RepoType);

        // 修正失败仓后可“仅重试失败项”：新 job 只包含失败的 SIL。
        File.WriteAllText(Path.Combine(fixture.Clones["CF100A_Demo_SIL"], "sil-staged.txt"), "sil");
        TestGit.Run(fixture.Clones["CF100A_Demo_SIL"], "add", "sil-staged.txt");

        var retry = await DispatchAsync(router, "retryFailed", new { jobId = accepted.JobId });
        Assert.True(retry.Ok, Describe(retry));
        var retryResult = Assert.IsType<RetryFailedResultDto>(retry.Data);
        Assert.True(retryResult.Accepted);
        Assert.Equal(1, retryResult.Retried);
        Assert.NotNull(retryResult.JobId);
        Assert.NotEqual(accepted.JobId, retryResult.JobId);

        var retryJob = await WaitJobAsync(router, retryResult.JobId!);
        Assert.Equal("SUCCESS", retryJob.State);
        var retryItem = Assert.Single(retryJob.Items);
        Assert.Equal("SIL", retryItem.RepoType);
        Assert.Equal("OK", retryItem.State);

        // 原 job 的成功仓不被重跑（IMPL HEAD 在重试 job 中不变）。
        Assert.DoesNotContain(retryJob.Items, item => item.RepoType == "IMPL");
    }

    [Fact]
    public async Task RunBatch_Commit_AllClean_IsFailed_And_RetryWithoutItemsRejected()
    {
        using var fixture = BatchWorkspace.Create("CF100A_Demo_Impl");
        var config = TestAudit.NewConfig(fixture.Root);
        using var snapshots = new SnapshotService(config);
        using var router = new HostCommandRouter(config, snapshots, new RecordingLauncher());
        await DispatchAsync(router, "getSnapshot", null);

        var batch = await DispatchAsync(
            router,
            "runBatch",
            new
            {
                kind = "COMMIT",
                componentIds = new[] { "CF100A_Demo" },
                args = new { message = "nothing", confirm = true }
            });
        var accepted = Assert.IsType<RunBatchResultDto>(Assert.IsType<CommandResult>(batch).Data);
        var job = await WaitJobAsync(router, accepted.JobId);
        Assert.Equal("FAILED", job.State);
        Assert.Equal(0, job.Ok);
        Assert.Equal(1, job.Failed);

        // “仅重试失败项”对 FAILED 有效（生成新 job）；作业运行中则拒绝。
        var retry = await DispatchAsync(router, "retryFailed", new { jobId = accepted.JobId });
        var retryResult = Assert.IsType<RetryFailedResultDto>(Assert.IsType<CommandResult>(retry).Data);
        Assert.True(retryResult.Accepted);
        Assert.Equal(1, retryResult.Retried);
        var retryJob = await WaitJobAsync(router, retryResult.JobId!);
        Assert.Equal("FAILED", retryJob.State);
    }

    // ------------------------------------------------------------------ 矩阵/前置 → SKIPPED（不失败整个 job）

    [Fact]
    public async Task RunBatch_Pull_MatrixBlocked_IsSkippedNotFailed()
    {
        using var fixture = BatchWorkspace.Create("CF100A_Demo_Impl");
        var config = TestAudit.NewConfig(fixture.Root);
        using var snapshots = new SnapshotService(config);
        using var router = new HostCommandRouter(config, snapshots, new RecordingLauncher());
        await DispatchAsync(router, "getSnapshot", null);

        // 修改 tracked 文件 → 扫描态 DIRTY（矩阵 DIRTY.PULL = N）。
        File.WriteAllText(Path.Combine(fixture.Clones["CF100A_Demo_Impl"], "README.md"), "dirty");
        var batch = await DispatchAsync(
            router,
            "runBatch",
            new { kind = "PULL", componentIds = new[] { "CF100A_Demo" }, repoTypes = new[] { "IMPL" } });
        var accepted = Assert.IsType<RunBatchResultDto>(Assert.IsType<CommandResult>(batch).Data);

        var job = await WaitJobAsync(router, accepted.JobId);
        Assert.Equal("SUCCESS", job.State);
        var item = Assert.Single(job.Items);
        Assert.Equal("SKIPPED", item.State);
        Assert.Equal(BridgeErrorCodes.ActionNotAllowed, item.ErrorCode);
        Assert.Contains("DIRTY", item.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------ 取消（PENDING / RUNNING）

    [Fact]
    public async Task Cancel_PendingJob_MarksJobAndItemsCanceled_WithoutExecuting()
    {
        using var server = new HangingHttpServer();
        using var slow = CreateSlowRepository(server, "CF100B_Slow");
        using var fixture = BatchWorkspace.Create("CF100A_Demo_Impl");
        // 把 slow 仓并入同一工作区（BatchWorkspace.Root 是同一个工作区根）。
        var slowTarget = Path.Combine(fixture.Root, "CF100B_Slow_Impl");
        CopyDirectory(slow.RepoPath, slowTarget);
        MoveOriginTo(slowTarget, $"http://127.0.0.1:{server.Port}/CF100B_Slow_Impl.git");

        var config = TestAudit.NewConfig(fixture.Root);
        using var snapshots = new SnapshotService(config);
        using var router = new HostCommandRouter(config, snapshots, new RecordingLauncher());
        await DispatchAsync(router, "getSnapshot", null);

        // job1 用慢仓占住唯一 worker（fetch 会挂起）。
        var first = await DispatchAsync(
            router,
            "runBatch",
            new { kind = "FETCH", componentIds = new[] { "CF100B_Slow" }, repoTypes = new[] { "IMPL" } });
        var firstJob = Assert.IsType<RunBatchResultDto>(Assert.IsType<CommandResult>(first).Data);
        await WaitUntilAsync(router, firstJob.JobId, job => job.Items.Any(item => item.State == "RUNNING"));

        // job2 仍在队列 PENDING：取消后立即 CANCELED 且不会执行。
        var second = await DispatchAsync(
            router,
            "runBatch",
            new { kind = "FETCH", componentIds = new[] { "CF100A_Demo" }, repoTypes = new[] { "IMPL" } });
        var secondJob = Assert.IsType<RunBatchResultDto>(Assert.IsType<CommandResult>(second).Data);

        var cancel = await DispatchAsync(router, "cancelJob", new { jobId = secondJob.JobId });
        Assert.True(cancel.Ok, Describe(cancel));
        var canceled = Assert.IsType<CancelJobResultDto>(cancel.Data);
        Assert.Equal("CANCELED", canceled.State);

        var secondState = Assert.IsType<JobDto>(
            Assert.IsType<CommandResult>(await DispatchAsync(router, "getJob", new { jobId = secondJob.JobId })).Data);
        Assert.Equal("CANCELED", secondState.State);
        Assert.True(secondState.CancelRequested);
        Assert.False(secondState.Cancellable);
        Assert.False(secondState.DryRun);
        var pendingItem = Assert.Single(secondState.Items);
        Assert.Equal("CANCELED", pendingItem.State);
        // 取消 ≠ 超时：未开始 item 不得写 E_TIMEOUT。
        Assert.Null(pendingItem.ErrorCode);
        Assert.Contains("未开始执行", pendingItem.Message, StringComparison.Ordinal);

        // 审计：PENDING 取消路径也必须为每 item 恰写 1 行（result=CANCELED、errorCode 为空）。
        var cancelAudit = ReadAuditLines(config)
            .Where(line => line.Contains("\"jobId\":\"" + secondJob.JobId + "\"", StringComparison.Ordinal))
            .ToList();
        Assert.Single(cancelAudit);
        using (var auditDocument = JsonDocument.Parse(cancelAudit[0]))
        {
            var auditRoot = auditDocument.RootElement;
            Assert.Equal(secondJob.JobId, auditRoot.GetProperty("jobId").GetString());
            Assert.Equal(pendingItem.ErrorCode, auditRoot.TryGetProperty("errorCode", out var code)
                && code.ValueKind != JsonValueKind.Null
                    ? code.GetString()
                    : null);
            Assert.Equal("CANCELED", auditRoot.GetProperty("result").GetString());
            Assert.False(string.IsNullOrWhiteSpace(auditRoot.GetProperty("jobItemId").GetString()));
        }

        // 只允许取消 PENDING/RUNNING：已完成作业再取消 → 稳定错误码（而不是静默成功）。
        var cancelAgain = await DispatchAsync(router, "cancelJob", new { jobId = secondJob.JobId });
        Assert.False(cancelAgain.Ok);
        Assert.Equal(BridgeErrorCodes.ActionNotAllowed, cancelAgain.Error!.Code);

        // 清理：取消慢 job，避免测试结束时遗留挂起 fetch。
        var cancelFirst = await DispatchAsync(router, "cancelJob", new { jobId = firstJob.JobId });
        Assert.True(cancelFirst.Ok);
        await WaitUntilAsync(router, firstJob.JobId, job => job.State == "CANCELED", timeoutMs: 20000);
    }

    [Fact]
    public async Task Cancel_RunningJob_CooperativelyCancelsGitProcess()
    {
        using var server = new HangingHttpServer();
        using var slow = CreateSlowRepository(server, "CF100B_Slow");
        var config = TestAudit.NewConfig(slow.WorkspaceRoot);
        using var snapshots = new SnapshotService(config);
        using var router = new HostCommandRouter(config, snapshots, new RecordingLauncher());
        await DispatchAsync(router, "getSnapshot", null);

        var batch = await DispatchAsync(
            router,
            "runBatch",
            new { kind = "FETCH", componentIds = new[] { "CF100B_Slow" }, repoTypes = new[] { "IMPL" } });
        var accepted = Assert.IsType<RunBatchResultDto>(Assert.IsType<CommandResult>(batch).Data);
        await WaitUntilAsync(router, accepted.JobId, job => job.Items.Any(item => item.State == "RUNNING"));

        // 确认 git 网络进程确实已连到挂起服务器，再取消（否则可能在进程连网前就被取消）。
        await WaitConditionAsync(() => server.Connections >= 1, timeoutMs: 15000);

        var stopwatch = Stopwatch.StartNew();
        var cancel = await DispatchAsync(router, "cancelJob", new { jobId = accepted.JobId });
        Assert.True(cancel.Ok, Describe(cancel));

        // 合作式取消：git fetch 120s 超时不应发生；取消后应快速进入 CANCELED。
        var job = await WaitUntilAsync(router, accepted.JobId, entry => entry.State == "CANCELED", timeoutMs: 20000);
        stopwatch.Stop();
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(15), $"取消耗时 {stopwatch.Elapsed}");
        Assert.False(job.Cancellable);
        Assert.True(job.CancelRequested);
        Assert.True(server.Connections >= 1, "未捕获到 git 网络请求");

        // 取消语义：RUNNING item 合作取消为 CANCELED，且 errorCode=null（取消不写 E_TIMEOUT）。
        var runningItem = Assert.Single(job.Items);
        Assert.Equal("CANCELED", runningItem.State);
        Assert.Null(runningItem.ErrorCode);
        Assert.Contains("取消", runningItem.Message, StringComparison.Ordinal);

        // 每个 item 终态恰 1 行审计（含取消路径）。
        var auditLines = ReadAuditLines(config);
        var cancelRow = Assert.Single(auditLines);
        using var cancelDocument = JsonDocument.Parse(cancelRow);
        Assert.Equal("CANCELED", cancelDocument.RootElement.GetProperty("result").GetString());
        Assert.False(
            cancelDocument.RootElement.TryGetProperty("errorCode", out var cancelCode)
            && cancelCode.ValueKind == JsonValueKind.String,
            "取消路径不得把 E_TIMEOUT 写进审计错误码");
        Assert.Equal(accepted.JobId, cancelDocument.RootElement.GetProperty("jobId").GetString());
    }

    // ------------------------------------------------------------------ 持久化：重启 INTERRUPTED / 200 上限

    [Fact]
    public void Restart_OnlyRunningBecomesInterrupted_PendingBecomesCanceled_WithoutReexecuting()
    {
        using var workspace = new TempWorkspace();
        var jobsRoot = TestAudit.NewJobsRoot();
        var auditRoot = TestAudit.NewRoot();
        var now = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);

        var store = new JobStore(jobsRoot);
        store.Save(new[]
        {
            new StoredJob(
                "job-running",
                "SYNC",
                "RUNNING",
                now,
                now,
                null,
                new[]
                {
                    new StoredJobItem("CF100A_Demo", "IMPL", "CF100A_Demo_Impl", "RUNNING", now, null, null, null, null, null, null),
                    new StoredJobItem("CF100A_Demo", "SIL", "CF100A_Demo_SIL", "PENDING", null, null, null, null, null, null, null)
                },
                new StoredJobArguments(null, null, null, true, false, false, false, false, false, null),
                "tester",
                "req-running"),
            new StoredJob(
                "job-pending",
                "FETCH",
                "PENDING",
                now,
                null,
                null,
                new[]
                {
                    new StoredJobItem("CF100A_Demo", "IMPL", null, "PENDING", null, null, null, null, null, null, null)
                },
                new StoredJobArguments(null, null, null, false, false, false, false, false, false, null),
                "tester",
                "req-pending")
        });

        var config = new AppConfig
        {
            WorkspaceRoot = workspace.Root,
            JobsRoot = jobsRoot,
            AuditRoot = auditRoot
        };

        using var snapshots = new SnapshotService(config);
        using var engine = new JobEngine(config, snapshots);
        var jobsResult = engine.GetJobs(new GetJobsParams(10));
        Assert.True(jobsResult.Ok, jobsResult.Error?.Code);
        var jobs = Assert.IsType<JobsDto>(jobsResult.Data).Jobs;

        // 曾执行中的作业 → INTERRUPTED；只有 RUNNING item 标 INTERRUPTED，未开始 PENDING item 标 CANCELED。
        var running = Assert.Single(jobs, job => job.Id == "job-running");
        Assert.Equal("INTERRUPTED", running.State);
        Assert.False(running.Cancellable);
        Assert.NotNull(running.FinishedAt);
        var interruptedItem = Assert.Single(running.Items, item => item.RepoType == "IMPL");
        Assert.Equal("INTERRUPTED", interruptedItem.State);
        Assert.Contains("进程重启", interruptedItem.Message, StringComparison.Ordinal);
        var neverStarted = Assert.Single(running.Items, item => item.RepoType == "SIL");
        Assert.Equal("CANCELED", neverStarted.State);
        Assert.Null(neverStarted.ErrorCode);
        Assert.Contains("进程重启", neverStarted.Message, StringComparison.Ordinal);

        // 纯排队未开始的作业 → CANCELED（不是 INTERRUPTED）。
        var pending = Assert.Single(jobs, job => job.Id == "job-pending");
        Assert.Equal("CANCELED", pending.State);
        Assert.True(pending.CancelRequested);
        var pendingItem = Assert.Single(pending.Items);
        Assert.Equal("CANCELED", pendingItem.State);
        Assert.Null(pendingItem.ErrorCode);

        // 审计：重启改写的 3 个 item 每个恰 1 行（RUNNING→INTERRUPTED / PENDING→CANCELED）。
        var auditLines = ReadAuditLines(config);
        Assert.Equal(3, auditLines.Count);
        var auditResults = auditLines
            .Select(line =>
            {
                using var document = JsonDocument.Parse(line);
                return (
                    JobId: document.RootElement.GetProperty("jobId").GetString(),
                    Result: document.RootElement.GetProperty("result").GetString(),
                    JobItemId: document.RootElement.GetProperty("jobItemId").GetString());
            })
            .ToList();
        Assert.Equal(2, auditResults.Count(entry => entry.JobId == "job-running"));
        Assert.Contains(auditResults, entry => entry.JobId == "job-running" && entry.Result == "INTERRUPTED");
        Assert.Contains(auditResults, entry => entry.JobId == "job-running" && entry.Result == "CANCELED");
        Assert.Contains(auditResults, entry => entry.JobId == "job-pending" && entry.Result == "CANCELED");
        Assert.Equal(3, auditResults.Select(entry => entry.JobItemId).Distinct(StringComparer.Ordinal).Count());

        // 磁盘上也已回写收敛状态（不重复执行）。
        var persisted = LoadStoredJobs(jobsRoot).ToList();
        var persistedRunning = Assert.Single(persisted, job => job.Id == "job-running");
        Assert.Equal("INTERRUPTED", persistedRunning.State);
        Assert.Contains(persistedRunning.Items, item => item.State == "CANCELED");
        Assert.Contains(persistedRunning.Items, item => item.State == "INTERRUPTED");
        Assert.Equal("CANCELED", Assert.Single(persisted, job => job.Id == "job-pending").State);

        // 再次“重启”加载：终态作业不再重写审计（每 item 仍恰 1 行）。
        using var snapshots2 = new SnapshotService(config);
        using var engine2 = new JobEngine(config, snapshots2);
        var again = Assert.IsType<JobsDto>(engine2.GetJobs(new GetJobsParams(10)).Data);
        Assert.Equal("INTERRUPTED", Assert.Single(again.Jobs, job => job.Id == "job-running").State);
        Assert.Equal("CANCELED", Assert.Single(again.Jobs, job => job.Id == "job-pending").State);
        Assert.Equal(3, ReadAuditLines(config).Count);
    }

    [Fact]
    public void JobStore_KeepsLatest200Jobs()
    {
        using var workspace = new TempWorkspace();
        var jobsRoot = TestAudit.NewJobsRoot();
        Directory.CreateDirectory(jobsRoot);
        var now = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);

        var storedJobs = Enumerable.Range(0, 205)
            .Select(index => new StoredJob(
                "job-" + index.ToString(CultureInfo.InvariantCulture),
                "FETCH",
                "SUCCESS",
                now,
                now,
                now,
                new[]
                {
                    new StoredJobItem("CF100A_Demo", "IMPL", null, "OK", now, now, null, null, null, null, null)
                },
                new StoredJobArguments(null, null, null, false, false, false, false, false, false, null),
                "tester",
                "req-" + index.ToString(CultureInfo.InvariantCulture)))
            .ToList();
        var json = JsonSerializer.Serialize(new { version = 1, jobs = storedJobs }, WebJson);
        File.WriteAllText(Path.Combine(jobsRoot, JobStore.FileName), json);

        var config = new AppConfig { WorkspaceRoot = workspace.Root, JobsRoot = jobsRoot, AuditRoot = TestAudit.NewRoot() };
        using var snapshots = new SnapshotService(config);
        using var engine = new JobEngine(config, snapshots);

        var result = engine.GetJobs(new GetJobsParams(500));
        Assert.Equal(BridgeErrorCodes.InvalidArgument, result.Error?.Code);

        var jobs = Assert.IsType<JobsDto>(Assert.IsType<CommandResult>(
            engine.GetJobs(new GetJobsParams(200))).Data).Jobs;
        Assert.Equal(200, jobs.Count);
        Assert.Equal("job-204", jobs[0].Id);   // 最新在前
        Assert.Equal("job-5", jobs[^1].Id);    // 最旧 5 条被淘汰
        Assert.Equal(BridgeErrorCodes.JobNotFound, engine.GetJob(new GetJobParams("job-0")).Error?.Code);
    }

    // ------------------------------------------------------------------ SYNC 备份 ref / 备份失败不 reset

    [Fact]
    public async Task RunBatch_Sync_DivergedWithTypedConfirm_CreatesBackupRefThenResetsToRemote()
    {
        using var fixture = GitRemoteFixture.Create();
        var config = TestAudit.NewConfig(fixture.Root);
        using var snapshots = new SnapshotService(config);
        using var router = new HostCommandRouter(config, snapshots, new RecordingLauncher());
        await DispatchAsync(router, "getSnapshot", null);

        // DIVERGED：本地未推送 commit + 远程前进 1 个提交（双方非祖先关系）。
        File.WriteAllText(Path.Combine(fixture.CloneB, "local-unpushed.txt"), "local");
        TestGit.CommitAll(fixture.CloneB, "local unpushed");
        var preHead = GitRemoteFixture.Head(fixture.CloneB);
        fixture.CommitAndPushFromA("remote advance");
        TestGit.Run(fixture.CloneB, "fetch", "-q", "origin");
        var remoteHead = GitRemoteFixture.Head(fixture.BarePath);

        // 作业开始时 read-through 刷新快照，保证矩阵判定为 DIVERGED。
        Assert.True((await DispatchAsync(router, "refresh", null)).Ok);
        var snapshot = Assert.IsType<SnapshotDto>(
            Assert.IsType<CommandResult>(await DispatchAsync(router, "getSnapshot", null)).Data);
        Assert.Equal("DIVERGED", Assert.Single(
            Assert.Single(snapshot.Components, entry => entry.ComponentId == "CF100A_Demo").Repos).State);

        var batch = await DispatchAsync(
            router,
            "runBatch",
            new
            {
                kind = "SYNC",
                componentIds = new[] { "CF100A_Demo" },
                repoTypes = new[] { "IMPL" },
                args = new { confirm = true, confirmText = "CF100A_Demo" }
            });
        var accepted = Assert.IsType<RunBatchResultDto>(Assert.IsType<CommandResult>(batch).Data);
        var job = await WaitJobAsync(router, accepted.JobId);
        Assert.Equal("SUCCESS", job.State);
        var item = Assert.Single(job.Items);
        Assert.Equal("OK", item.State);
        Assert.NotNull(item.BackupRef);
        Assert.True(item.BackupRef!.StartsWith(BatchOperationService.BackupRefPrefix, StringComparison.Ordinal), item.BackupRef);
        Assert.True(item.BackupRef!.EndsWith("/IMPL", StringComparison.Ordinal), item.BackupRef);

        // M2b-2b（ADR D-026 P1-2）：备份 ref 携带来源分支身份 <ts>/<branch>/<TYPE>。
        Assert.True(
            BackupRefService.TryParseBackupRef(
                item.BackupRef!,
                out _,
                out _,
                out var backupBranch,
                out var backupType,
                out var backupFormat),
            item.BackupRef);
        Assert.Equal("main", backupBranch);
        Assert.Equal("IMPL", backupType);
        Assert.Equal(BackupRefFormats.Branch, backupFormat);
        Assert.Contains("/main/IMPL", item.BackupRef!, StringComparison.Ordinal);

        // 备份 ref 指向 preHead（含本地未推送 commit）；HEAD 已 reset 到 origin/main。
        Assert.Equal(preHead, TestGit.Capture(fixture.CloneB, "rev-parse", item.BackupRef!));
        Assert.Equal(remoteHead, GitRemoteFixture.Head(fixture.CloneB));
        Assert.Equal(remoteHead, item.PostHead);

        // 审计行含 backupRef/jobId/result。
        var auditLine = Assert.Single(ReadAuditLines(config));
        using var document = JsonDocument.Parse(auditLine);
        Assert.Equal(item.BackupRef, document.RootElement.GetProperty("backupRef").GetString());
        Assert.Equal(accepted.JobId, document.RootElement.GetProperty("jobId").GetString());
        Assert.Equal("OK", document.RootElement.GetProperty("result").GetString());
    }

    [Fact]
    public async Task Sync_BackupRefCreationImpossible_FailsWithoutReset()
    {
        using var fixture = GitRemoteFixture.Create();
        var config = TestAudit.NewConfig(fixture.Root);
        using var snapshots = new SnapshotService(config);
        _ = snapshots;

        // cloneA 先把 orphan-sync 分支推到远端；cloneB 切到同名 unborn 分支（无 HEAD）：
        // 远端同名分支存在、但本地无 HEAD → 无法创建备份 ref，必须 FAILED 且绝不 reset。
        TestGit.Run(fixture.CloneA, "checkout", "-q", "-b", "orphan-sync");
        File.WriteAllText(Path.Combine(fixture.CloneA, "orphan.txt"), "orphan");
        TestGit.CommitAll(fixture.CloneA, "orphan commit");
        TestGit.Run(fixture.CloneA, "push", "-q", "-u", "origin", "orphan-sync");

        TestGit.Run(fixture.CloneB, "checkout", "-q", "--orphan", "orphan-sync");
        TestGit.Run(fixture.CloneB, "fetch", "-q", "origin");

        // 直接调用批量执行器（绕过矩阵预检，专测“备份 ref 失败 → item FAILED 且绝不 reset”的顺序保证）。
        var actions = new RepoActionService(config: config);
        var batch = new BatchOperationService(config, actions, paths: new PathPolicy(fixture.Root));
        var repoRef = MakeRepoRef("CF100A_Demo", "IMPL", fixture.CloneB, "SYNCED", headSha: null, branch: "orphan-sync");

        var result = await batch.SyncAsync(
            repoRef,
            "CF100A_Demo",
            "IMPL",
            new StoredJobArguments(null, null, null, true, false, false, false, false, false, null, "CF100A_Demo"),
            CancellationToken.None);

        Assert.Equal(JobItemStates.Failed, result.State);
        Assert.Equal(BridgeErrorCodes.GitFailed, result.ErrorCode);
        Assert.Contains("备份", result.Message, StringComparison.Ordinal);
        Assert.Null(result.BackupRef);

        // 备份失败 → 绝不 reset：HEAD 仍是 unborn（reset 会把工作区变成远端分支内容）。
        Assert.Throws<InvalidOperationException>(() =>
            TestGit.Capture(fixture.CloneB, "rev-parse", "--verify", "--quiet", "HEAD"));
    }

    // ------------------------------------------------------------------ DELETE → RESTORE

    [Fact]
    public async Task RunBatch_DeleteMovesToTrash_And_RestoreRoundTrips()
    {
        using var fixture = GitRemoteFixture.Create();
        var config = TestAudit.NewConfig(fixture.Root);
        using var snapshots = new SnapshotService(config);
        using var router = new HostCommandRouter(config, snapshots, new RecordingLauncher());
        await DispatchAsync(router, "getSnapshot", null);

        var originalHead = GitRemoteFixture.Head(fixture.CloneB);

        var deleteBatch = await DispatchAsync(
            router,
            "runBatch",
            new
            {
                kind = "DELETE",
                componentIds = new[] { "CF100A_Demo" },
                repoTypes = new[] { "IMPL" },
                args = new { confirm = true, confirmText = "CF100A_Demo" }
            });
        var deleteAccepted = Assert.IsType<RunBatchResultDto>(Assert.IsType<CommandResult>(deleteBatch).Data);
        var deleteJob = await WaitJobAsync(router, deleteAccepted.JobId);
        Assert.Equal("SUCCESS", deleteJob.State);
        var deleteItem = Assert.Single(deleteJob.Items);
        Assert.Equal("OK", deleteItem.State);
        Assert.False(Directory.Exists(fixture.CloneB), "DELETE 后工作区目录应已移走");
        Assert.NotNull(deleteItem.BackupRef);
        Assert.True(deleteItem.BackupRef!.StartsWith(".trash/", StringComparison.Ordinal), deleteItem.BackupRef);
        Assert.True(Directory.Exists(Path.Combine(fixture.Root, ".trash")));

        // 远端零变更（本地软删，不删远端）。
        Assert.True(Directory.Exists(fixture.BarePath));

        // RESTORE：目标目录已不存在，从 .trash 最近一份恢复。
        var restoreBatch = await DispatchAsync(
            router,
            "runBatch",
            new
            {
                kind = "RESTORE",
                componentIds = new[] { "CF100A_Demo" },
                repoTypes = new[] { "IMPL" },
                args = new { confirm = true }
            });
        var restoreAccepted = Assert.IsType<RunBatchResultDto>(Assert.IsType<CommandResult>(restoreBatch).Data);
        var restoreJob = await WaitJobAsync(router, restoreAccepted.JobId);
        Assert.Equal("SUCCESS", restoreJob.State);
        var restoreItem = Assert.Single(restoreJob.Items);
        Assert.Equal("OK", restoreItem.State);
        Assert.True(Directory.Exists(fixture.CloneB), "RESTORE 后目录应已回到工作区");
        Assert.Equal(originalHead, GitRemoteFixture.Head(fixture.CloneB));

        // 再次 RESTORE（目标已存在）→ SKIPPED（不覆盖）。
        var again = await DispatchAsync(
            router,
            "runBatch",
            new
            {
                kind = "RESTORE",
                componentIds = new[] { "CF100A_Demo" },
                repoTypes = new[] { "IMPL" },
                args = new { confirm = true }
            });
        var againAccepted = Assert.IsType<RunBatchResultDto>(Assert.IsType<CommandResult>(again).Data);
        var againJob = await WaitJobAsync(router, againAccepted.JobId);
        Assert.Equal("SKIPPED", Assert.Single(againJob.Items).State);
    }

    // ------------------------------------------------------------------ DELETE 预检四阻断 / 强确认

    [Fact]
    public async Task Delete_Preflight_DirtyUntrackedAheadStash_Rejected_WithoutMovingDirectory()
    {
        using var fixture = GitRemoteFixture.Create();
        var config = TestAudit.NewConfig(fixture.Root);
        using var snapshots = new SnapshotService(config);
        using var router = new HostCommandRouter(config, snapshots, new RecordingLauncher());
        await DispatchAsync(router, "getSnapshot", null);

        // ① untracked → 拒绝（提示先提交/stash；不提供 force 绕过）。
        File.WriteAllText(Path.Combine(fixture.CloneB, "scratch.txt"), "scratch");
        var untrackedJob = await RunBatchJobAsync(router, new
        {
            kind = "DELETE",
            componentIds = new[] { "CF100A_Demo" },
            repoTypes = new[] { "IMPL" },
            args = new { confirm = true, confirmText = "CF100A_Demo" }
        });
        Assert.Equal("FAILED", untrackedJob.State);
        var untrackedItem = Assert.Single(untrackedJob.Items);
        Assert.Equal("FAILED", untrackedItem.State);
        Assert.Equal(BridgeErrorCodes.DirtyWorktree, untrackedItem.ErrorCode);
        Assert.Contains("提交或 stash", untrackedItem.Message, StringComparison.Ordinal);
        Assert.True(Directory.Exists(fixture.CloneB));
        Assert.False(Directory.Exists(Path.Combine(fixture.Root, ".trash")));

        // ② tracked dirty → 拒绝。
        File.Delete(Path.Combine(fixture.CloneB, "scratch.txt"));
        File.WriteAllText(Path.Combine(fixture.CloneB, "README.md"), "dirty");
        var dirtyJob = await RunBatchJobAsync(router, new
        {
            kind = "DELETE",
            componentIds = new[] { "CF100A_Demo" },
            repoTypes = new[] { "IMPL" },
            args = new { confirm = true, confirmText = "CF100A_Demo" }
        });
        Assert.Equal(BridgeErrorCodes.DirtyWorktree, Assert.Single(dirtyJob.Items).ErrorCode);
        Assert.True(Directory.Exists(fixture.CloneB));

        // ③ stash 记录 → 拒绝。
        TestGit.Run(fixture.CloneB, "stash", "push", "-u", "-m", "pre-delete-stash");
        var stashJob = await RunBatchJobAsync(router, new
        {
            kind = "DELETE",
            componentIds = new[] { "CF100A_Demo" },
            repoTypes = new[] { "IMPL" },
            args = new { confirm = true, confirmText = "CF100A_Demo" }
        });
        var stashItem = Assert.Single(stashJob.Items);
        Assert.Equal("FAILED", stashItem.State);
        Assert.Equal(BridgeErrorCodes.ActionNotAllowed, stashItem.ErrorCode);
        Assert.Contains("stash", stashItem.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(Directory.Exists(fixture.CloneB));

        // ④ 未推送提交（AHEAD）→ 拒绝。
        TestGit.Run(fixture.CloneB, "stash", "drop", "-q");
        File.WriteAllText(Path.Combine(fixture.CloneB, "ahead.txt"), "ahead");
        TestGit.CommitAll(fixture.CloneB, "unpushed before delete");
        var aheadJob = await RunBatchJobAsync(router, new
        {
            kind = "DELETE",
            componentIds = new[] { "CF100A_Demo" },
            repoTypes = new[] { "IMPL" },
            args = new { confirm = true, confirmText = "CF100A_Demo" }
        });
        var aheadItem = Assert.Single(aheadJob.Items);
        Assert.Equal("FAILED", aheadItem.State);
        Assert.Equal(BridgeErrorCodes.ActionNotAllowed, aheadItem.ErrorCode);
        Assert.Contains("未推送", aheadItem.Message, StringComparison.Ordinal);
        Assert.True(Directory.Exists(fixture.CloneB));
        Assert.False(Directory.Exists(Path.Combine(fixture.Root, ".trash")));

        // 四类拒绝均写审计（每 item 恰 1 行）。
        Assert.Equal(4, ReadAuditLines(config).Count);
    }

    [Fact]
    public async Task Delete_RequiresTypedComponentId_IndependentFromConfirmFlag()
    {
        using var fixture = GitRemoteFixture.Create();
        var config = TestAudit.NewConfig(fixture.Root);
        using var snapshots = new SnapshotService(config);
        using var router = new HostCommandRouter(config, snapshots, new RecordingLauncher());
        await DispatchAsync(router, "getSnapshot", null);

        // 缺少 args.confirmText → 受理阶段直接拒绝（E_CONFIRM_REQUIRED，不产生 job）。
        var missing = await DispatchAsync(router, "runBatch", new
        {
            kind = "DELETE",
            componentIds = new[] { "CF100A_Demo" },
            repoTypes = new[] { "IMPL" },
            args = new { confirm = true }
        });
        Assert.False(missing.Ok);
        Assert.Equal(BridgeErrorCodes.ConfirmRequired, missing.Error!.Code);

        // 错误组件 ID（含大小写不匹配）→ 作业受理但逐仓 FAILED，目录保留、零写。
        foreach (var wrongText in new[] { "CF100A_Demo_IMPL", "cf100a_demo", "CF999_Other" })
        {
            var wrongJob = await RunBatchJobAsync(router, new
            {
                kind = "DELETE",
                componentIds = new[] { "CF100A_Demo" },
                repoTypes = new[] { "IMPL" },
                args = new { confirm = true, confirmText = wrongText }
            });
            var wrongItem = Assert.Single(wrongJob.Items);
            Assert.Equal("FAILED", wrongItem.State);
            Assert.Equal(BridgeErrorCodes.ConfirmRequired, wrongItem.ErrorCode);
            Assert.True(Directory.Exists(fixture.CloneB), "强确认失败时不得移动目录");
        }

        Assert.False(Directory.Exists(Path.Combine(fixture.Root, ".trash")));

        // 正确组件 ID（首尾空格忽略）→ OK 且只移到工作区 .trash。
        var okJob = await RunBatchJobAsync(router, new
        {
            kind = "DELETE",
            componentIds = new[] { "CF100A_Demo" },
            repoTypes = new[] { "IMPL" },
            args = new { confirm = true, confirmText = "  CF100A_Demo  " }
        });
        Assert.Equal("SUCCESS", okJob.State);
        var okItem = Assert.Single(okJob.Items);
        Assert.Equal("OK", okItem.State);
        Assert.False(Directory.Exists(fixture.CloneB));
        Assert.True(Directory.Exists(Path.Combine(fixture.Root, ".trash")));
    }

    // ------------------------------------------------------------------ SYNC 预检（矩阵/强确认）/ dryRun

    [Fact]
    public async Task Sync_Preflight_AheadBlocked_ZeroWriteAndAudited()
    {
        using var fixture = GitRemoteFixture.Create();
        var config = TestAudit.NewConfig(fixture.Root);
        using var snapshots = new SnapshotService(config);
        using var router = new HostCommandRouter(config, snapshots, new RecordingLauncher());
        await DispatchAsync(router, "getSnapshot", null);

        // AHEAD：本地未推送提交（§7.6 AHEAD.SYNC=❌）。
        File.WriteAllText(Path.Combine(fixture.CloneB, "ahead.txt"), "ahead");
        TestGit.CommitAll(fixture.CloneB, "ahead before sync");
        var headBefore = GitRemoteFixture.Head(fixture.CloneB);

        var job = await RunBatchJobAsync(router, new
        {
            kind = "SYNC",
            componentIds = new[] { "CF100A_Demo" },
            repoTypes = new[] { "IMPL" },
            args = new { confirm = true }
        });
        var item = Assert.Single(job.Items);
        Assert.Equal("SKIPPED", item.State);
        Assert.Equal(BridgeErrorCodes.ActionNotAllowed, item.ErrorCode);
        Assert.Contains("AHEAD", item.Message, StringComparison.OrdinalIgnoreCase);

        // 零写：HEAD 不变、无 refs/backup、无 stash、无 merge。
        Assert.Equal(headBefore, GitRemoteFixture.Head(fixture.CloneB));
        Assert.Equal(string.Empty, TestGit.Capture(fixture.CloneB, "for-each-ref", "--format=%(refname)", "refs/backup/"));
        Assert.Equal(string.Empty, TestGit.Capture(fixture.CloneB, "stash", "list"));

        var auditLine = Assert.Single(ReadAuditLines(config));
        using var auditDocument = JsonDocument.Parse(auditLine);
        Assert.Equal("SKIPPED", auditDocument.RootElement.GetProperty("result").GetString());
        Assert.Equal(job.Id, auditDocument.RootElement.GetProperty("jobId").GetString());
    }

    [Fact]
    public async Task Sync_Preflight_MatrixBlockedStates_AreSkippedWithoutWrite()
    {
        using var fixture = GitRemoteFixture.Create();
        var config = TestAudit.NewConfig(fixture.Root);
        var actions = new RepoActionService(config: config);
        var batch = new BatchOperationService(config, actions, paths: new PathPolicy(fixture.Root));
        var headBefore = GitRemoteFixture.Head(fixture.CloneB);

        // §7.6 SYNC 强覆盖列 ❌ 的状态：逐仓 SKIPPED（E_ACTION_NOT_ALLOWED）且零写（不 fetch/不建 ref）。
        var blockedStates = new[]
        {
            "IN_PROGRESS", "UNMERGED", "INVALID", "MISSING",
            "UNKNOWN", "EMPTY", "AHEAD", "NO_UPSTREAM", "DETACHED_TAG", "DETACHED_COMMIT"
        };
        foreach (var state in blockedStates)
        {
            var repoRef = MakeRepoRef("CF100A_Demo", "IMPL", fixture.CloneB, state, headBefore);
            var result = await batch.SyncAsync(
                repoRef,
                "CF100A_Demo",
                "IMPL",
                new StoredJobArguments(null, null, null, true, false, false, false, false, false, null),
                CancellationToken.None);

            Assert.Equal(JobItemStates.Skipped, result.State);
            Assert.Equal(BridgeErrorCodes.ActionNotAllowed, result.ErrorCode);
            Assert.Contains(state, result.Message, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Equal(headBefore, GitRemoteFixture.Head(fixture.CloneB));
        Assert.Equal(
            string.Empty,
            TestGit.Capture(fixture.CloneB, "for-each-ref", "--format=%(refname)", "refs/backup/"));
    }

    [Fact]
    public async Task Sync_Diverged_DefaultBlocked_NeedTypedComponentIdBeforeAnyWrite()
    {
        using var fixture = GitRemoteFixture.Create();
        var config = TestAudit.NewConfig(fixture.Root);
        using var snapshots = new SnapshotService(config);
        using var router = new HostCommandRouter(config, snapshots, new RecordingLauncher());
        await DispatchAsync(router, "getSnapshot", null);

        // 构造 DIVERGED：本地未推送 commit + 远程前进 1 个提交。
        File.WriteAllText(Path.Combine(fixture.CloneB, "local.txt"), "local");
        TestGit.CommitAll(fixture.CloneB, "local unpushed");
        var preHead = GitRemoteFixture.Head(fixture.CloneB);
        fixture.CommitAndPushFromA("remote advance");
        TestGit.Run(fixture.CloneB, "fetch", "-q", "origin");
        var remoteHead = GitRemoteFixture.Head(fixture.BarePath);
        Assert.True((await DispatchAsync(router, "refresh", null)).Ok);

        // ① 未输入 confirmText；② 错误/大小写不匹配 confirmText → 均默认阻断、零写。
        foreach (var confirmText in new string?[] { null, "CF100A_Demo_IMPL", "cf100a_demo" })
        {
            object args = confirmText is null
                ? new { confirm = true }
                : new { confirm = true, confirmText };
            var blockedJob = await RunBatchJobAsync(router, new
            {
                kind = "SYNC",
                componentIds = new[] { "CF100A_Demo" },
                repoTypes = new[] { "IMPL" },
                args
            });
            var blockedItem = Assert.Single(blockedJob.Items);
            Assert.Equal("SKIPPED", blockedItem.State);
            Assert.Equal(BridgeErrorCodes.ConfirmRequired, blockedItem.ErrorCode);
            Assert.Contains("组件 ID", blockedItem.Message, StringComparison.Ordinal);
            Assert.Equal(preHead, GitRemoteFixture.Head(fixture.CloneB));
            Assert.Equal(
                string.Empty,
                TestGit.Capture(fixture.CloneB, "for-each-ref", "--format=%(refname)", "refs/backup/"));
        }

        // ③ 正确组件 ID（首尾空格忽略）→ 备份 ref + reset 到远端。
        var okJob = await RunBatchJobAsync(router, new
        {
            kind = "SYNC",
            componentIds = new[] { "CF100A_Demo" },
            repoTypes = new[] { "IMPL" },
            args = new { confirm = true, confirmText = " CF100A_Demo " }
        });
        Assert.Equal("SUCCESS", okJob.State);
        var okItem = Assert.Single(okJob.Items);
        Assert.Equal("OK", okItem.State);
        Assert.NotNull(okItem.BackupRef);
        Assert.Equal(preHead, TestGit.Capture(fixture.CloneB, "rev-parse", okItem.BackupRef!));
        Assert.Equal(remoteHead, GitRemoteFixture.Head(fixture.CloneB));
    }

    [Fact]
    public async Task DryRun_Sync_IsPreviewOnly_ZeroWrite_AndJobStatePreview()
    {
        using var fixture = GitRemoteFixture.Create();
        var config = TestAudit.NewConfig(fixture.Root);
        using var snapshots = new SnapshotService(config);
        using var router = new HostCommandRouter(config, snapshots, new RecordingLauncher());
        await DispatchAsync(router, "getSnapshot", null);

        File.WriteAllText(Path.Combine(fixture.CloneB, "README.md"), "dirty-preview");
        var headBefore = GitRemoteFixture.Head(fixture.CloneB);

        // dryRun 无需 confirm：只输出预检。
        var batch = await DispatchAsync(router, "runBatch", new
        {
            kind = "SYNC",
            componentIds = new[] { "CF100A_Demo" },
            repoTypes = new[] { "IMPL" },
            args = new { dryRun = true }
        });
        Assert.True(batch.Ok, Describe(batch));
        var accepted = Assert.IsType<RunBatchResultDto>(Assert.IsType<CommandResult>(batch).Data);
        var job = await WaitJobAsync(router, accepted.JobId);

        // 预览作业：job/item 都是 PREVIEW（不写成 SUCCESS 0/1 误导）。
        Assert.Equal("PREVIEW", job.State);
        Assert.True(job.DryRun);
        Assert.False(job.Cancellable);
        Assert.Contains("预览", job.Message, StringComparison.Ordinal);
        var item = Assert.Single(job.Items);
        Assert.Equal("PREVIEW", item.State);
        Assert.True(item.Preview);
        Assert.Null(item.ErrorCode);
        Assert.Contains("DRY_RUN", item.Message, StringComparison.Ordinal);
        Assert.Contains("本地改动", item.Message, StringComparison.Ordinal);

        // 零写：HEAD 不变、无备份 ref、无 stash/reset、文件内容不变。
        Assert.Equal(headBefore, GitRemoteFixture.Head(fixture.CloneB));
        Assert.Equal("dirty-preview", File.ReadAllText(Path.Combine(fixture.CloneB, "README.md")));
        Assert.Equal(string.Empty, TestGit.Capture(fixture.CloneB, "for-each-ref", "--format=%(refname)", "refs/backup/"));
        Assert.Equal(string.Empty, TestGit.Capture(fixture.CloneB, "stash", "list"));

        // 审计：dryRun item 也是终态（PREVIEW），恰 1 行、errorCode=null。
        var auditLine = Assert.Single(ReadAuditLines(config));
        using var auditDocument = JsonDocument.Parse(auditLine);
        Assert.Equal("PREVIEW", auditDocument.RootElement.GetProperty("result").GetString());
        Assert.Equal(job.Id, auditDocument.RootElement.GetProperty("jobId").GetString());
    }

    [Fact]
    public async Task DryRun_Delete_IsPreviewOnly_DirectoryRetainedAndNoTrash()
    {
        using var fixture = GitRemoteFixture.Create();
        var config = TestAudit.NewConfig(fixture.Root);
        using var snapshots = new SnapshotService(config);
        using var router = new HostCommandRouter(config, snapshots, new RecordingLauncher());
        await DispatchAsync(router, "getSnapshot", null);

        var job = await RunBatchJobAsync(router, new
        {
            kind = "DELETE",
            componentIds = new[] { "CF100A_Demo" },
            repoTypes = new[] { "IMPL" },
            args = new { dryRun = true }
        });

        Assert.Equal("PREVIEW", job.State);
        Assert.True(job.DryRun);
        var item = Assert.Single(job.Items);
        Assert.Equal("PREVIEW", item.State);
        Assert.True(item.Preview);
        Assert.Contains(".trash", item.Message, StringComparison.Ordinal);
        Assert.Contains("组件 ID", item.Message, StringComparison.Ordinal);

        // 零写：目录仍在、无 .trash。
        Assert.True(Directory.Exists(fixture.CloneB));
        Assert.False(Directory.Exists(Path.Combine(fixture.Root, ".trash")));
        Assert.Single(ReadAuditLines(config));
    }

    // ------------------------------------------------------------------ 审计完整性 / per-repo 锁

    [Fact]
    public async Task Audit_EveryTerminalItem_ExactlyOneLine_IncludingPreSkipped()
    {
        using var fixture = BatchWorkspace.Create("CF100A_Demo_Impl");
        var config = TestAudit.NewConfig(fixture.Root);
        using var snapshots = new SnapshotService(config);
        using var router = new HostCommandRouter(config, snapshots, new RecordingLauncher());
        await DispatchAsync(router, "getSnapshot", null);

        // CF999_NotThere 不在快照 → 受理阶段预跳过（原先 0 行审计）；CF100A_Demo IMPL 正常执行 OK。
        var job = await RunBatchJobAsync(router, new
        {
            kind = "FETCH",
            componentIds = new[] { "CF100A_Demo", "CF999_NotThere" },
            repoTypes = new[] { "IMPL" }
        });
        Assert.Equal("SUCCESS", job.State);
        Assert.Equal(2, job.Total);
        Assert.Single(job.Items, item => item.State == "OK");
        Assert.Single(job.Items, item => item.State == "SKIPPED");

        var lines = ReadAuditLines(config);
        Assert.Equal(job.Total, lines.Count);
        var jobItemIds = new HashSet<string>(StringComparer.Ordinal);
        var results = new List<string>();
        foreach (var line in lines)
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            Assert.Equal(job.Id, root.GetProperty("jobId").GetString());
            Assert.True(jobItemIds.Add(root.GetProperty("jobItemId").GetString()!));
            Assert.Equal("FETCH", root.GetProperty("action").GetString());
            Assert.True(root.TryGetProperty("result", out var result));
            results.Add(result.GetString()!);
            Assert.True(root.TryGetProperty("errorCode", out _) || root.TryGetProperty("ok", out _));
        }

        Assert.Equal(2, jobItemIds.Count);
        Assert.Contains("OK", results);
        Assert.Contains("SKIPPED", results);
    }

    [Fact]
    public async Task RepoLock_IsSharedAndSerializesSameRepository()
    {
        using var workspace = new TempWorkspace();
        var repoPath = workspace.Dir("repo");
        var otherPath = workspace.Dir("other");

        // 同一 canonical 路径：第一把锁未释放前第二个必须排队（复用 RepoActionService 的静态锁）。
        using var first = await RepoActionService.AcquireRepoLockAsync(repoPath, CancellationToken.None);
        var secondTask = Task.Run(() => RepoActionService.AcquireRepoLockAsync(
            repoPath.TrimEnd(Path.DirectorySeparatorChar),
            CancellationToken.None));
        await Task.Delay(150);
        Assert.False(secondTask.IsCompleted, "同仓 per-repo 锁必须串行");

        // 异仓可并发。
        using var other = await RepoActionService
            .AcquireRepoLockAsync(otherPath, CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(5));

        first.Dispose();
        using var second = await secondTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    // ------------------------------------------------------------------ CLONE（本地 file:// bare remote）

    [Fact]
    public async Task RunBatch_Clone_MissingRepo_FromLocalBareRemote()
    {
        using var remotes = new TempWorkspace();
        var remoteRoot = Path.Combine(remotes.Root, "remote");
        var orgRoot = Path.Combine(remoteRoot, "RTCRepos");
        Directory.CreateDirectory(orgRoot);
        CreateSeedBareRemote(remotes.Root, orgRoot, "CF100A_Demo_Impl");

        using var workspace = new TempWorkspace();
        var config = CloneConfig(workspace.Root, remoteRoot, TestAudit.NewRoot(), TestAudit.NewJobsRoot());
        using var snapshots = new SnapshotService(config);
        using var router = new HostCommandRouter(config, snapshots, new RecordingLauncher());
        await DispatchAsync(router, "getSnapshot", null);

        var batch = await DispatchAsync(
            router,
            "runBatch",
            new { kind = "CLONE", componentIds = new[] { "CF100A_Demo" }, repoTypes = new[] { "IMPL" } });
        Assert.True(batch.Ok, Describe(batch));
        var accepted = Assert.IsType<RunBatchResultDto>(Assert.IsType<CommandResult>(batch).Data);
        var job = await WaitJobAsync(router, accepted.JobId);
        Assert.True(job.State == "SUCCESS", job.Items[0].Message);
        var item = Assert.Single(job.Items);
        Assert.Equal("OK", item.State);
        Assert.NotNull(item.PostHead);
        Assert.Equal("CF100A_Demo_Impl", item.RepoName);

        var clonedPath = Path.Combine(workspace.Root, "CF100A_Demo_Impl");
        Assert.True(Directory.Exists(clonedPath));
        Assert.Equal(item.PostHead, TestGit.Capture(clonedPath, "rev-parse", "HEAD"));

        // 重新扫描后工作台应能看到该组件/仓（CLONE 成功后失效快照）。
        var refreshed = await DispatchAsync(router, "refresh", null);
        Assert.True(refreshed.Ok);
        var snapshot = Assert.IsType<SnapshotDto>(
            Assert.IsType<CommandResult>(await DispatchAsync(router, "getSnapshot", null)).Data);
        var component = Assert.Single(snapshot.Components, entry => entry.ComponentId == "CF100A_Demo");
        var repo = Assert.Single(component.Repos, entry => entry.RepoType == "IMPL");
        Assert.True(repo.IsLocal);
    }

    [Fact]
    public async Task RunBatch_Clone_TokenWithNonHttpBase_FailsClosedWithoutLeak()
    {
        using var remotes = new TempWorkspace();
        var remoteRoot = Path.Combine(remotes.Root, "remote");
        var orgRoot = Path.Combine(remoteRoot, "RTCRepos");
        Directory.CreateDirectory(orgRoot);
        CreateSeedBareRemote(remotes.Root, orgRoot, "CF100A_Demo_Impl");

        using var workspace = new TempWorkspace();
        var config = CloneConfig(workspace.Root, remoteRoot, TestAudit.NewRoot(), TestAudit.NewJobsRoot())
            with
        {
            GiteaToken = TestToken
        };
        using var snapshots = new SnapshotService(config);
        using var router = new HostCommandRouter(config, snapshots, new RecordingLauncher());
        await DispatchAsync(router, "getSnapshot", null);

        var batch = await DispatchAsync(
            router,
            "runBatch",
            new { kind = "CLONE", componentIds = new[] { "CF100A_Demo" }, repoTypes = new[] { "IMPL" } });
        var accepted = Assert.IsType<RunBatchResultDto>(Assert.IsType<CommandResult>(batch).Data);
        var job = await WaitJobAsync(router, accepted.JobId);

        Assert.Equal("FAILED", job.State);
        Assert.Equal(BridgeErrorCodes.GiteaConfigInvalid, Assert.Single(job.Items).ErrorCode);
        Assert.False(Directory.Exists(Path.Combine(workspace.Root, "CF100A_Demo_Impl")));

        // token/Authorization 不进入 job 响应与审计。
        var jobJson = JsonSerializer.Serialize(
            Assert.IsType<JobDto>(Assert.IsType<CommandResult>(
                await DispatchAsync(router, "getJob", new { jobId = accepted.JobId })).Data),
            WebJson);
        Assert.DoesNotContain(TestToken, jobJson, StringComparison.Ordinal);
        Assert.DoesNotContain(
            Convert.ToBase64String(Encoding.UTF8.GetBytes(TestToken + ":")),
            jobJson,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Authorization", jobJson, StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------ CREATE / TAG

    [Fact]
    public async Task RunBatch_CreateBranchAndTag_PerRepo_ExistingSkipped()
    {
        using var fixture = BatchWorkspace.Create("CF100A_Demo_Impl", "CF100A_Demo_SIL");
        var config = TestAudit.NewConfig(fixture.Root);
        using var snapshots = new SnapshotService(config);
        using var router = new HostCommandRouter(config, snapshots, new RecordingLauncher());
        await DispatchAsync(router, "getSnapshot", null);

        var create = await DispatchAsync(
            router,
            "runBatch",
            new
            {
                kind = "CREATE",
                componentIds = new[] { "CF100A_Demo" },
                repoTypes = new[] { "IMPL", "SIL" },
                args = new { branch = "feature/m2b", confirm = true }
            });
        var createAccepted = Assert.IsType<RunBatchResultDto>(Assert.IsType<CommandResult>(create).Data);
        var createJob = await WaitJobAsync(router, createAccepted.JobId);
        Assert.Equal("SUCCESS", createJob.State);
        Assert.All(createJob.Items, item => Assert.Equal("OK", item.State));
        foreach (var clone in fixture.Clones.Values)
        {
            Assert.Equal(
                "feature/m2b",
                TestGit.Capture(clone, "rev-parse", "--abbrev-ref", "refs/heads/feature/m2b"));
            Assert.NotEmpty(TestGit.Capture(clone, "for-each-ref", "--format=%(refname)", "refs/baseline/create"));
        }

        // AC-OP-24 dry-run 字段：重名/远程存在/基线 HEAD/非法校验提示，零写。
        var preview = await DispatchAsync(router, "runBatch", new
        {
            kind = "CREATE",
            componentIds = new[] { "CF100A_Demo" },
            repoTypes = new[] { "IMPL" },
            args = new { branch = "feature/m2b", dryRun = true }
        });
        var previewAccepted = Assert.IsType<RunBatchResultDto>(Assert.IsType<CommandResult>(preview).Data);
        var previewJob = await WaitJobAsync(router, previewAccepted.JobId);
        var previewItem = Assert.Single(previewJob.Items);
        Assert.Equal("PREVIEW", previewItem.State);
        Assert.Contains("本地重名：是", previewItem.Message ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("基线 HEAD", previewItem.Message ?? string.Empty, StringComparison.Ordinal);

        // 已存在 → SKIPPED（不失败）。
        var createAgain = await DispatchAsync(
            router,
            "runBatch",
            new
            {
                kind = "CREATE",
                componentIds = new[] { "CF100A_Demo" },
                repoTypes = new[] { "IMPL", "SIL" },
                args = new { branch = "feature/m2b", confirm = true }
            });
        var againAccepted = Assert.IsType<RunBatchResultDto>(Assert.IsType<CommandResult>(createAgain).Data);
        var againJob = await WaitJobAsync(router, againAccepted.JobId);
        Assert.Equal("SUCCESS", againJob.State);
        Assert.All(againJob.Items, item => Assert.Equal("SKIPPED", item.State));

        // FR-OP-04 / AC-OP-11：TAG = 切 Tag（检出 detached），不是在当前 HEAD 打 tag。
        foreach (var clone in fixture.Clones.Values)
        {
            TestGit.Run(clone, "tag", "v1.0.0");
            TestGit.Run(clone, "push", "-q", "origin", "v1.0.0");
        }

        var tag = await DispatchAsync(
            router,
            "runBatch",
            new
            {
                kind = "TAG",
                componentIds = new[] { "CF100A_Demo" },
                repoTypes = new[] { "IMPL", "SIL" },
                args = new { tag = "v1.0.0", confirm = true }
            });
        var tagAccepted = Assert.IsType<RunBatchResultDto>(Assert.IsType<CommandResult>(tag).Data);
        var tagJob = await WaitJobAsync(router, tagAccepted.JobId);
        Assert.Equal("SUCCESS", tagJob.State);
        Assert.All(tagJob.Items, item => Assert.Equal("OK", item.State));
        foreach (var clone in fixture.Clones.Values)
        {
            var tagSha = TestGit.Capture(clone, "rev-parse", "refs/tags/v1.0.0");
            Assert.Equal(tagSha, TestGit.Capture(clone, "rev-parse", "HEAD"));
            Assert.Equal("HEAD", TestGit.Capture(clone, "rev-parse", "--abbrev-ref", "HEAD"));
        }

        // AC-OP-11/12/14：baseline_snapshot 由 TAG job 写 2 行（本用例选 2 仓），均为 tag_present=1。
        var baselineRows = new BaselineSnapshotService(config).List("CF100A_Demo");
        Assert.Equal(2, baselineRows.Count);
        Assert.All(baselineRows, row => Assert.True(row.TagPresent));
        Assert.All(baselineRows, row => Assert.False(string.IsNullOrWhiteSpace(row.CommitSha)));
    }

    [Fact]
    public async Task SyncPreview_Diverged_ShowsLossListFields()
    {
        using var fixture = BatchWorkspace.Create("CF205A_Loss_IMPL");
        var config = TestAudit.NewConfig(fixture.Root);
        var clone = fixture.Clones["CF205A_Loss_IMPL"];
        for (var i = 0; i < 2; i++)
        {
            File.WriteAllText(Path.Combine(clone, "local-" + i + ".txt"), "local");
            TestGit.CommitAll(clone, "local " + i);
        }
        File.WriteAllText(Path.Combine(clone, "untracked.txt"), "untracked");
        File.WriteAllText(Path.Combine(clone, "stash.txt"), "stash me");
        TestGit.Run(clone, "stash", "push", "-u", "-m", "test stash");
        File.WriteAllText(Path.Combine(clone, "untracked-after-stash.txt"), "untracked2");

        var head = TestGit.Capture(clone, "rev-parse", "HEAD");
        var repoRef = MakeRepoRef("CF205A_Loss", "IMPL", clone, "DIVERGED", head);
        var batch = new BatchOperationService(config, new RepoActionService(config: config), paths: new PathPolicy(fixture.Root));
        var arguments = new StoredJobArguments(null, null, null, false, true, false, false, false, false, null, "CF205A_Loss");
        var preview = await batch.PreviewAsync("SYNC", repoRef, "CF205A_Loss", "IMPL", arguments, CancellationToken.None);

        Assert.Equal(JobItemStates.Preview, preview.State);
        var message = preview.Message ?? string.Empty;
        Assert.Contains("未推送提交：2 个", message, StringComparison.Ordinal);
        Assert.Contains("untracked", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("untracked-after-stash.txt", message, StringComparison.Ordinal);
        Assert.Contains("stash：1 条", message, StringComparison.Ordinal);
        Assert.Contains("影响仓", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sync_Untracked_DefaultStashes_ExplicitCleanRequiresTypedConfirm()
    {
        using var fixture = BatchWorkspace.Create("CF206A_Untracked_IMPL");
        var config = TestAudit.NewConfig(fixture.Root);
        var clone = fixture.Clones["CF206A_Untracked_IMPL"];

        // 远端前进，使 SYNC 有实际 reset 动作。
        var seedRoot = Path.Combine(Path.GetTempPath(), "MbdGitToolTests", "untracked-seed-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(seedRoot);
        try
        {
            var seed = Path.Combine(seedRoot, "seed");
            TestGit.Run(seedRoot, "clone", "-q", fixture.Bares["CF206A_Untracked_IMPL"], seed);
            TestGit.ConfigureIdentity(seed);
            File.WriteAllText(Path.Combine(seed, "remote.txt"), "remote");
            TestGit.CommitAll(seed, "remote");
            TestGit.Run(seed, "push", "-q", "origin", "main");

            var repoRef = MakeRepoRef("CF206A_Untracked", "IMPL", clone, "BEHIND", TestGit.Capture(clone, "rev-parse", "HEAD"));
            var batch = new BatchOperationService(config, new RepoActionService(config: config), paths: new PathPolicy(fixture.Root));
            var untracked = Path.Combine(clone, "keep-untracked.txt");
            File.WriteAllText(untracked, "keep");

            // 默认：untracked 不删除（stash -u 保存）。
            var defaultArgs = new StoredJobArguments(null, null, null, true, false, false, false, false, false, null, "CF206A_Untracked");
            var defaultResult = await batch.SyncAsync(repoRef, "CF206A_Untracked", "IMPL", defaultArgs, CancellationToken.None);
            Assert.Equal(JobItemStates.Ok, defaultResult.State);
            Assert.False(File.Exists(untracked));

            // 无强确认的显式清理：SKIPPED 且文件保留。
            File.WriteAllText(untracked, "keep-again");
            var unconfirmed = new StoredJobArguments(null, null, null, true, false, false, false, false, false, null, null, CleanUntracked: true);
            var blocked = await batch.SyncAsync(
                MakeRepoRef("CF206A_Untracked", "IMPL", clone, "BEHIND", TestGit.Capture(clone, "rev-parse", "HEAD")),
                "CF206A_Untracked", "IMPL", unconfirmed, CancellationToken.None);
            Assert.Equal(JobItemStates.Skipped, blocked.State);
            Assert.Equal(BridgeErrorCodes.ConfirmRequired, blocked.ErrorCode);
            Assert.True(File.Exists(untracked));
        }
        finally
        {
            try { Directory.Delete(seedRoot, recursive: true); } catch { /* ignore */ }
        }
    }
    [Fact]
    public async Task Delete_FileLocked_WritesPendingAndRetrySucceeds()
    {
        using var fixture = BatchWorkspace.Create("CF207A_Pending_IMPL");
        var config = TestAudit.NewConfig(fixture.Root);
        var clone = fixture.Clones["CF207A_Pending_IMPL"];
        var head = TestGit.Capture(clone, "rev-parse", "HEAD");
        var repoRef = MakeRepoRef("CF207A_Pending", "IMPL", clone, "SYNCED", head);
        var batch = new BatchOperationService(config, new RepoActionService(config: config), paths: new PathPolicy(fixture.Root));
        var args = new StoredJobArguments(null, null, null, true, false, false, false, false, false, null, "CF207A_Pending");
        Environment.SetEnvironmentVariable("MBD_FAULT_INJECT", "DIR_MOVE_BUSY");
        BatchItemResult blocked;
        try
        {
            blocked = await batch.DeleteAsync(repoRef, "CF207A_Pending", "IMPL", args, CancellationToken.None);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MBD_FAULT_INJECT", null);
        }
        Assert.Equal(JobItemStates.Failed, blocked.State);
        Assert.Equal(BridgeErrorCodes.DeletePending, blocked.ErrorCode);
        Assert.True(Directory.Exists(clone));
        var pending = Path.Combine(fixture.Root, ".trash", ".pending");
        Assert.True(Directory.Exists(pending));
        Assert.NotEmpty(Directory.GetFiles(pending, "*-CF207A_Pending_IMPL.json"));

        var retry = await batch.DeleteAsync(repoRef, "CF207A_Pending", "IMPL", args, CancellationToken.None);
        Assert.Equal(JobItemStates.Ok, retry.State);
        Assert.False(Directory.Exists(clone));
        Assert.Empty(Directory.GetFiles(pending, "*-CF207A_Pending_IMPL.json"));
    }
    [Fact]
    public async Task RunBatch_Create_UndoRemovesCreatedBranches()
    {
        using var fixture = BatchWorkspace.Create("CF209A_Undo_IMPL", "CF209A_Undo_SIL");
        var config = TestAudit.NewConfig(fixture.Root);
        using var snapshots = new SnapshotService(config);
        using var router = new HostCommandRouter(config, snapshots, new RecordingLauncher());
        await DispatchAsync(router, "getSnapshot", null);

        var create = await DispatchAsync(router, "runBatch", new
        {
            kind = "CREATE",
            componentIds = new[] { "CF209A_Undo" },
            repoTypes = new[] { "IMPL", "SIL" },
            args = new { branch = "release/undo", confirm = true }
        });
        var accepted = Assert.IsType<RunBatchResultDto>(Assert.IsType<CommandResult>(create).Data);
        var job = await WaitJobAsync(router, accepted.JobId);
        Assert.Equal("SUCCESS", job.State);
        Assert.All(fixture.Clones.Values, clone => Assert.Equal("release/undo", TestGit.Capture(clone, "rev-parse", "--abbrev-ref", "refs/heads/release/undo")));
        Assert.All(job.Items, item => Assert.False(string.IsNullOrWhiteSpace(item.BaselineRef)));

        var undo = await DispatchAsync(router, "undoCreate", new { jobId = accepted.JobId });
        Assert.True(undo.Ok, undo.Error?.Code);
        Assert.All(fixture.Clones.Values, clone =>
        {
            var verify = TestGit.Capture(clone, "for-each-ref", "--format=%(refname)", "refs/heads/release/undo");
            Assert.Equal(string.Empty, verify);
        });
    }
    [Fact]
    public async Task RunBatch_Sync_RemoteBranchGone_RejectedZeroWrite()
    {
        using var fixture = BatchWorkspace.Create("CF211A_Gone_IMPL");
        var config = TestAudit.NewConfig(fixture.Root);
        using var snapshots = new SnapshotService(config);
        using var router = new HostCommandRouter(config, snapshots, new RecordingLauncher());
        await DispatchAsync(router, "getSnapshot", null);

        // 删除远端 main（模拟 UPSTREAM_GONE）。
        var bare = fixture.Bares["CF211A_Gone_IMPL"];
        TestGit.Run(bare, "update-ref", "-d", "refs/heads/main");
        var clone = fixture.Clones["CF211A_Gone_IMPL"];
        var head = TestGit.Capture(clone, "rev-parse", "HEAD");

        var batch = await DispatchAsync(router, "runBatch", new
        {
            kind = "SYNC",
            componentIds = new[] { "CF211A_Gone" },
            repoTypes = new[] { "IMPL" },
            args = new { confirm = true }
        });
        var accepted = Assert.IsType<RunBatchResultDto>(Assert.IsType<CommandResult>(batch).Data);
        var job = await WaitJobAsync(router, accepted.JobId);
        var item = Assert.Single(job.Items);
        Assert.Equal("FAILED", item.State);
        Assert.Equal(BridgeErrorCodes.RefNotFound, item.ErrorCode);
        Assert.Equal(head, TestGit.Capture(clone, "rev-parse", "HEAD"));
        Assert.Empty(TestGit.Capture(clone, "for-each-ref", "--format=%(refname)", "refs/backup/sync"));
    }
    [Fact]
    public async Task ComponentManagement_LocalDelete_RunsDeleteJobThenMarksDeleted()
    {
        using var fixture = BatchWorkspace.Create("CF210A_Del_IMPL");
        var config = TestAudit.NewConfig(fixture.Root);
        using var snapshots = new SnapshotService(config);
        using var router = new HostCommandRouter(config, snapshots, new RecordingLauncher());
        await DispatchAsync(router, "getSnapshot", null);

        var noConfirm = await DispatchAsync(router, "unbindComponent", new { componentId = "CF210A_Del", mode = "LOCAL_DELETE" });
        Assert.False(noConfirm.Ok);
        Assert.Equal(BridgeErrorCodes.ConfirmRequired, noConfirm.Error!.Code);

        var result = await DispatchAsync(router, "unbindComponent", new
        {
            componentId = "CF210A_Del",
            mode = "LOCAL_DELETE",
            confirmText = "CF210A_Del"
        });
        Assert.True(result.Ok, result.Error?.Code);
        Assert.False(Directory.Exists(Path.Combine(fixture.Root, "CF210A_Del_IMPL")));
        var management = await DispatchAsync(router, "getComponentManagement", null);
        var json = System.Text.Json.JsonSerializer.Serialize(management.Data, WebJson);
        Assert.Contains("\"isDeleted\":true", json.Replace(" ", string.Empty), StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public async Task ComponentManagement_StopManage_FiltersSnapshotAndListsState()
    {
        using var fixture = BatchWorkspace.Create("CF208A_Mgmt_IMPL");
        var config = TestAudit.NewConfig(fixture.Root);
        using var snapshots = new SnapshotService(config);
        using var router = new HostCommandRouter(config, snapshots, new RecordingLauncher());
        var before = Assert.IsType<SnapshotDto>(Assert.IsType<CommandResult>(
            await DispatchAsync(router, "getSnapshot", null)).Data);
        Assert.Contains(before.Components, component => component.ComponentId == "CF208A_Mgmt");

        var stop = await DispatchAsync(router, "unbindComponent", new { componentId = "CF208A_Mgmt", mode = "STOP_MANAGE" });
        Assert.True(stop.Ok, stop.Error?.Code);
        var after = Assert.IsType<SnapshotDto>(Assert.IsType<CommandResult>(
            await DispatchAsync(router, "getSnapshot", null)).Data);
        Assert.DoesNotContain(after.Components, component => component.ComponentId == "CF208A_Mgmt");
        Assert.DoesNotContain(after.Components.SelectMany(component => component.Repos), repo => repo.RepoType == "IMPL" && repo.LocalPath?.Contains("CF208A_Mgmt") == true);

        var list = await DispatchAsync(router, "getComponentManagement", null);
        Assert.True(list.Ok, list.Error?.Code);
        var json = System.Text.Json.JsonSerializer.Serialize(list.Data, WebJson);
        Assert.Contains("CF208A_Mgmt", json, StringComparison.Ordinal);
        Assert.Contains("\"isManaged\":false", json.Replace(" ", string.Empty), StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public async Task RunBatch_ExternalIndexLock_ReturnsBusyWithoutWriteOrDeletingLock()
    {
        using var fixture = BatchWorkspace.Create("CF204A_Lock_IMPL");
        var config = TestAudit.NewConfig(fixture.Root);
        using var snapshots = new SnapshotService(config);
        using var router = new HostCommandRouter(config, snapshots, new RecordingLauncher());
        await DispatchAsync(router, "getSnapshot", null);

        var clone = fixture.Clones.Values.First();
        var lockFile = Path.Combine(clone, ".git", "index.lock");
        File.WriteAllText(lockFile, "external git holds lock");
        var head = TestGit.Capture(clone, "rev-parse", "HEAD");

        var batch = await DispatchAsync(router, "runBatch", new
        {
            kind = "SYNC",
            componentIds = new[] { "CF204A_Lock" },
            repoTypes = new[] { "IMPL" },
            args = new { confirm = true }
        });
        var accepted = Assert.IsType<RunBatchResultDto>(Assert.IsType<CommandResult>(batch).Data);
        var job = await WaitJobAsync(router, accepted.JobId);
        var item = Assert.Single(job.Items);
        Assert.Equal("FAILED", item.State);
        Assert.Equal(BridgeErrorCodes.RepoBusy, item.ErrorCode);
        Assert.True(File.Exists(lockFile));
        Assert.Equal(head, TestGit.Capture(clone, "rev-parse", "HEAD"));
    }
    [Fact]
    public async Task RunBatch_Clone_FourRepos_AllSucceed_AndRerunIsIdempotent()
    {
        using var remotes = new TempWorkspace();
        var remoteRoot = Path.Combine(remotes.Root, "remote");
        var orgRoot = Path.Combine(remoteRoot, "RTCRepos");
        Directory.CreateDirectory(orgRoot);
        foreach (var type in new[] { "DESIGN", "IMPL", "MIL", "SIL" })
        {
            CreateSeedBareRemote(remotes.Root, orgRoot, "CF200A_Demo_" + type);
        }

        using var workspace = new TempWorkspace();
        var config = CloneConfig(workspace.Root, remoteRoot, TestAudit.NewRoot(), TestAudit.NewJobsRoot());
        using var snapshots = new SnapshotService(config);
        using var router = new HostCommandRouter(config, snapshots, new RecordingLauncher());
        await DispatchAsync(router, "getSnapshot", null);

        var first = await DispatchAsync(router, "runBatch", new
        {
            kind = "CLONE",
            componentIds = new[] { "CF200A_Demo" },
            repoTypes = new[] { "DESIGN", "IMPL", "MIL", "SIL" }
        });
        Assert.True(first.Ok, Describe(first));
        var firstAccepted = Assert.IsType<RunBatchResultDto>(Assert.IsType<CommandResult>(first).Data);
        var firstJob = await WaitJobAsync(router, firstAccepted.JobId);
        Assert.Equal("SUCCESS", firstJob.State);
        Assert.Equal(4, firstJob.Items.Count);
        Assert.All(firstJob.Items, item => Assert.Equal("OK", item.State));

        var gitDirs = new[] { "DESIGN", "IMPL", "MIL", "SIL" }
            .Select(type => Path.Combine(workspace.Root, "CF200A_Demo_" + type, ".git"))
            .ToList();
        Assert.All(gitDirs, dir => Assert.True(Directory.Exists(dir), dir));
        var before = gitDirs.ToDictionary(dir => dir, Directory.GetLastWriteTimeUtc);

        var second = await DispatchAsync(router, "runBatch", new
        {
            kind = "CLONE",
            componentIds = new[] { "CF200A_Demo" },
            repoTypes = new[] { "DESIGN", "IMPL", "MIL", "SIL" }
        });
        var secondAccepted = Assert.IsType<RunBatchResultDto>(Assert.IsType<CommandResult>(second).Data);
        var secondJob = await WaitJobAsync(router, secondAccepted.JobId);
        Assert.Equal("SUCCESS", secondJob.State);
        Assert.All(secondJob.Items, item => Assert.Equal("SKIPPED", item.State));
        Assert.All(gitDirs, dir => Assert.Equal(before[dir], Directory.GetLastWriteTimeUtc(dir)));
    }

    [Fact]
    public async Task RunBatch_Clone_PartialFailure_JobPartialAndRetryOnlyFailed()
    {
        using var remotes = new TempWorkspace();
        var remoteRoot = Path.Combine(remotes.Root, "remote");
        var orgRoot = Path.Combine(remoteRoot, "RTCRepos");
        Directory.CreateDirectory(orgRoot);
        CreateSeedBareRemote(remotes.Root, orgRoot, "CF201A_Partial_DESIGN");
        CreateSeedBareRemote(remotes.Root, orgRoot, "CF201A_Partial_IMPL");
        // MIL/SIL 故意缺失 → 2 成功 / 2 失败。

        using var workspace = new TempWorkspace();
        var config = CloneConfig(workspace.Root, remoteRoot, TestAudit.NewRoot(), TestAudit.NewJobsRoot());
        using var snapshots = new SnapshotService(config);
        using var router = new HostCommandRouter(config, snapshots, new RecordingLauncher());
        await DispatchAsync(router, "getSnapshot", null);

        var first = await DispatchAsync(router, "runBatch", new
        {
            kind = "CLONE",
            componentIds = new[] { "CF201A_Partial" },
            repoTypes = new[] { "DESIGN", "IMPL", "MIL", "SIL" }
        });
        var accepted = Assert.IsType<RunBatchResultDto>(Assert.IsType<CommandResult>(first).Data);
        var job = await WaitJobAsync(router, accepted.JobId);
        Assert.Equal("PARTIAL", job.State);
        Assert.Equal(2, job.Items.Count(item => item.State == "OK"));
        Assert.Equal(2, job.Items.Count(item => item.State == "FAILED"));
        Assert.All(job.Items.Where(item => item.State == "FAILED"), item =>
            Assert.Equal(BridgeErrorCodes.RemoteNotFound, item.ErrorCode));

        var retry = await DispatchAsync(router, "retryFailed", new { jobId = accepted.JobId });
        Assert.True(retry.Ok, retry.Error?.Code);
        var retryAccepted = Assert.IsType<RetryFailedResultDto>(Assert.IsType<CommandResult>(retry).Data);
        Assert.Equal(2, retryAccepted.Retried);
        var retryJob = await WaitJobAsync(router, retryAccepted.JobId!);
        Assert.Equal(2, retryJob.Items.Count);
        Assert.Equal(2, retryJob.Items.Count(item => item.State == "FAILED"));
    }
    [Fact]
    public async Task RunBatch_Pull_FastForward_NoMergeCommit()
    {
        using var fixture = BatchWorkspace.Create("CF202A_Pull_IMPL");
        var config = TestAudit.NewConfig(fixture.Root);
        using var snapshots = new SnapshotService(config);
        using var router = new HostCommandRouter(config, snapshots, new RecordingLauncher());
        await DispatchAsync(router, "getSnapshot", null);

        // 在远端 main 追加一个提交（通过临时 clone），让两个工作区处于 BEHIND。
        var seedRoot = Path.Combine(Path.GetTempPath(), "MbdGitToolTests", "pull-seed-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(seedRoot);
        try
        {
            var seed = Path.Combine(seedRoot, "seed");
            TestGit.Run(seedRoot, "clone", "-q", fixture.Bares["CF202A_Pull_IMPL"], seed);
            TestGit.ConfigureIdentity(seed);
            File.WriteAllText(Path.Combine(seed, "remote.txt"), "remote");
            TestGit.CommitAll(seed, "remote commit");
            TestGit.Run(seed, "push", "-q", "origin", "main");
            var remoteHead = TestGit.Capture(seed, "rev-parse", "HEAD");

            var batch = await DispatchAsync(router, "runBatch", new
            {
                kind = "PULL",
                componentIds = new[] { "CF202A_Pull" },
                repoTypes = new[] { "IMPL" }
            });
            var accepted = Assert.IsType<RunBatchResultDto>(Assert.IsType<CommandResult>(batch).Data);
            var job = await WaitJobAsync(router, accepted.JobId);
            Assert.Equal("SUCCESS", job.State);
            Assert.All(job.Items, item => Assert.Equal("OK", item.State));
            foreach (var clone in fixture.Clones.Values)
            {
                Assert.Equal(remoteHead, TestGit.Capture(clone, "rev-parse", "HEAD"));
                Assert.Empty(TestGit.Capture(clone, "rev-list", "--merges", "HEAD"));
            }
        }
        finally
        {
            try { Directory.Delete(seedRoot, recursive: true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public async Task RunBatch_Checkout_Branch_Succeeds_AndDirtyRepoBlocked()
    {
        using var fixture = BatchWorkspace.Create("CF203A_Co_IMPL", "CF203A_Co_SIL");
        var config = TestAudit.NewConfig(fixture.Root);
        using var snapshots = new SnapshotService(config);
        using var router = new HostCommandRouter(config, snapshots, new RecordingLauncher());
        await DispatchAsync(router, "getSnapshot", null);

        var seedRoot = Path.Combine(Path.GetTempPath(), "MbdGitToolTests", "co-seed-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(seedRoot);
        try
        {
            var seed = Path.Combine(seedRoot, "seed");
            TestGit.Run(seedRoot, "clone", "-q", fixture.Bares["CF203A_Co_IMPL"], seed);
            TestGit.ConfigureIdentity(seed);
            TestGit.Run(seed, "checkout", "-q", "-b", "release/1.0");
            File.WriteAllText(Path.Combine(seed, "release.txt"), "release");
            TestGit.CommitAll(seed, "release");
            TestGit.Run(seed, "push", "-q", "origin", "release/1.0");
            TestGit.Run(seed, "push", "-q", fixture.Bares["CF203A_Co_SIL"], "release/1.0:release/1.0");

            // 工作区先 fetch 出远端分支。
            foreach (var clone in fixture.Clones.Values)
            {
                TestGit.Run(clone, "fetch", "-q", "origin");
            }

            var batch = await DispatchAsync(router, "runBatch", new
            {
                kind = "CHKOUT",
                componentIds = new[] { "CF203A_Co" },
                repoTypes = new[] { "IMPL", "SIL" },
                args = new { branch = "release/1.0" }
            });
            var accepted = Assert.IsType<RunBatchResultDto>(Assert.IsType<CommandResult>(batch).Data);
            var job = await WaitJobAsync(router, accepted.JobId);
            Assert.True(job.State == "SUCCESS", string.Join(" | ", job.Items.Select(item => item.State + ":" + item.ErrorCode + ":" + item.Message)));
            Assert.All(job.Items, item => Assert.Equal("OK", item.State));
            Assert.All(fixture.Clones.Values, clone =>
                Assert.Equal("release/1.0", TestGit.Capture(clone, "rev-parse", "--abbrev-ref", "HEAD")));

            // DIRTY 仓切回 main 应被阻断（HEAD 不变）。
            var dirty = fixture.Clones.Values.First();
            File.WriteAllText(Path.Combine(dirty, "dirty.txt"), "dirty");
            var dirtyHead = TestGit.Capture(dirty, "rev-parse", "HEAD");
            var blocked = await DispatchAsync(router, "runBatch", new
            {
                kind = "CHKOUT",
                componentIds = new[] { "CF203A_Co" },
                repoTypes = new[] { "IMPL" },
                args = new { branch = "main" }
            });
            var blockedAccepted = Assert.IsType<RunBatchResultDto>(Assert.IsType<CommandResult>(blocked).Data);
            var blockedJob = await WaitJobAsync(router, blockedAccepted.JobId);
            var item = Assert.Single(blockedJob.Items);
            Assert.NotEqual("OK", item.State);
            Assert.Equal(dirtyHead, TestGit.Capture(dirty, "rev-parse", "HEAD"));
        }
        finally
        {
            try { Directory.Delete(seedRoot, recursive: true); } catch { /* ignore */ }
        }
    }
    [Fact]
    public async Task Repair_NonGitDirectory_BacksUpAndInitsFromRemote()
    {
        using var remotes = new TempWorkspace();
        var remoteRoot = Path.Combine(remotes.Root, "remote");
        var orgRoot = Path.Combine(remoteRoot, "RTCRepos");
        Directory.CreateDirectory(orgRoot);
        CreateSeedBareRemote(remotes.Root, orgRoot, "CF214A_Repair_IMPL");
        using var workspace = new TempWorkspace();
        var dir = Path.Combine(workspace.Root, "CF214A_Repair_IMPL");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "local-note.txt"), "keep me");
        var config = new AppConfig
        {
            WorkspaceRoot = workspace.Root,
            GiteaBaseUrl = "file:///" + remoteRoot.Replace('\\', '/').TrimStart('/'),
            GiteaOrg = "RTCRepos",
            AuditRoot = TestAudit.NewRoot(),
            JobsRoot = TestAudit.NewJobsRoot(),
            DatabasePath = TestAudit.NewDatabasePath(),
            ExportsRoot = TestAudit.NewExportsRoot(),
        };
        var repoRef = MakeRepoRef("CF214A_Repair", "IMPL", dir, "INVALID", null);
        var batch = new BatchOperationService(config, new RepoActionService(config: config), paths: new PathPolicy(workspace.Root));
        var args = new StoredJobArguments(null, null, null, true, false, false, false, false, false, null, "CF214A_Repair");
        var result = await batch.RepairAsync(repoRef, "CF214A_Repair", "IMPL", args, CancellationToken.None);
        Assert.True(result.State == JobItemStates.Ok, result.ErrorCode + ":" + result.Message);
        Assert.True(Directory.Exists(Path.Combine(dir, ".git")));
        var backups = Directory.GetFiles(Path.Combine(workspace.Root, ".trash"), "local-note.txt", SearchOption.AllDirectories);
        Assert.Single(backups);
    }

    [Fact]
    public async Task Reclone_CleanRepo_MovesToTrashAndClonesFresh()
    {
        using var remotes = new TempWorkspace();
        var remoteRoot = Path.Combine(remotes.Root, "remote");
        var orgRoot = Path.Combine(remoteRoot, "RTCRepos");
        Directory.CreateDirectory(orgRoot);
        CreateSeedBareRemote(remotes.Root, orgRoot, "CF215A_Reclone_IMPL");
        using var workspace = new TempWorkspace();
        var dir = Path.Combine(workspace.Root, "CF215A_Reclone_IMPL");
        TestGit.Run(workspace.Root, "clone", "-q", Path.Combine(orgRoot, "CF215A_Reclone_IMPL.git"), dir);
        TestGit.ConfigureIdentity(dir);
        var config = new AppConfig
        {
            WorkspaceRoot = workspace.Root,
            GiteaBaseUrl = "file:///" + remoteRoot.Replace('\\', '/').TrimStart('/'),
            GiteaOrg = "RTCRepos",
            AuditRoot = TestAudit.NewRoot(),
            JobsRoot = TestAudit.NewJobsRoot(),
            DatabasePath = TestAudit.NewDatabasePath(),
            ExportsRoot = TestAudit.NewExportsRoot(),
        };
        var repoRef = MakeRepoRef("CF215A_Reclone", "IMPL", dir, "INVALID", TestGit.Capture(dir, "rev-parse", "HEAD"));
        var batch = new BatchOperationService(config, new RepoActionService(config: config), paths: new PathPolicy(workspace.Root));
        var args = new StoredJobArguments(null, null, null, true, false, false, false, false, false, null, "CF215A_Reclone");
        var result = await batch.RecloneAsync(repoRef, "CF215A_Reclone", "IMPL", args, CancellationToken.None);
        Assert.True(result.State == JobItemStates.Ok, result.ErrorCode + ":" + result.Message);
        Assert.True(Directory.Exists(Path.Combine(dir, ".git")));
        Assert.NotEmpty(Directory.GetFiles(Path.Combine(workspace.Root, ".trash"), "README.md", SearchOption.AllDirectories));
    }
    [Fact]
    public async Task RunBatch_Tag_FourRepos_MixedWritesFourBaselineRows()
    {
        using var fixture = BatchWorkspace.Create("CF213A_Tag4_DESIGN", "CF213A_Tag4_IMPL", "CF213A_Tag4_MIL", "CF213A_Tag4_SIL");
        var config = TestAudit.NewConfig(fixture.Root);
        using var snapshots = new SnapshotService(config);
        using var router = new HostCommandRouter(config, snapshots, new RecordingLauncher());
        await DispatchAsync(router, "getSnapshot", null);

        foreach (var (name, clone) in fixture.Clones)
        {
            if (name.EndsWith("_SIL", StringComparison.Ordinal)) continue;
            TestGit.Run(clone, "tag", "-a", "v3.0.0", "-m", "annotated");
            TestGit.Run(clone, "push", "-q", "origin", "v3.0.0");
        }

        var result = await DispatchAsync(router, "runBatch", new
        {
            kind = "TAG",
            componentIds = new[] { "CF213A_Tag4" },
            repoTypes = new[] { "DESIGN", "IMPL", "MIL", "SIL" },
            args = new { tag = "v3.0.0", confirm = true }
        });
        var accepted = Assert.IsType<RunBatchResultDto>(Assert.IsType<CommandResult>(result).Data);
        var job = await WaitJobAsync(router, accepted.JobId);
        Assert.Equal(3, job.Items.Count(item => item.State == "OK"));
        Assert.Equal(1, job.Items.Count(item => item.State == "SKIPPED"));

        var rows = new BaselineSnapshotService(config).List("CF213A_Tag4");
        Assert.Equal(4, rows.Count);
        Assert.Equal(3, rows.Count(row => row.TagPresent));
        var missing = Assert.Single(rows, row => !row.TagPresent);
        Assert.Equal("SIL", missing.RepoType);
        Assert.Null(missing.CommitSha);
        Assert.All(rows.Where(row => row.TagPresent), row =>
        {
            Assert.False(string.IsNullOrWhiteSpace(row.CommitSha));
            Assert.False(string.IsNullOrWhiteSpace(row.TagObjectSha));
            Assert.NotEqual(row.CommitSha, row.TagObjectSha); // annotated tag object != commit
        });
    }
    [Fact]
    public async Task RunBatch_Tag_MissingRemoteTag_SkipsWithTagDrift_ZeroCheckout()
    {
        using var fixture = BatchWorkspace.Create("CF100A_Demo_Impl");
        var config = TestAudit.NewConfig(fixture.Root);
        using var snapshots = new SnapshotService(config);
        using var router = new HostCommandRouter(config, snapshots, new RecordingLauncher());
        await DispatchAsync(router, "getSnapshot", null);
        var before = fixture.Clones.Values.ToDictionary(path => path, path => TestGit.Capture(path, "rev-parse", "HEAD"));

        var result = await DispatchAsync(router, "runBatch", new
        {
            kind = "TAG",
            componentIds = new[] { "CF100A_Demo" },
            repoTypes = new[] { "IMPL" },
            args = new { tag = "missing-v9.9.9", confirm = true }
        });
        var accepted = Assert.IsType<RunBatchResultDto>(Assert.IsType<CommandResult>(result).Data);
        var job = await WaitJobAsync(router, accepted.JobId);
        Assert.All(job.Items, item => Assert.Equal("SKIPPED", item.State));
        Assert.All(job.Items, item => Assert.Equal(BridgeErrorCodes.RefNotFound, item.ErrorCode));
        Assert.Contains("TAG_DRIFT", job.Items[0].Message ?? string.Empty, StringComparison.Ordinal);
        var missingBaseline = new BaselineSnapshotService(config).List("CF100A_Demo");
        Assert.Contains(missingBaseline, row => row.Tag == "missing-v9.9.9" && !row.TagPresent && row.CommitSha is null);
        Assert.Equal(before[fixture.Clones.Values.First()], TestGit.Capture(fixture.Clones.Values.First(), "rev-parse", "HEAD"));
    }

    [Fact]
    public async Task RunBatch_Tag_AnnotatedTag_DereferencesToCommitAndDetaches()
    {
        using var fixture = BatchWorkspace.Create("CF100A_Demo_Impl");
        var config = TestAudit.NewConfig(fixture.Root);
        using var snapshots = new SnapshotService(config);
        using var router = new HostCommandRouter(config, snapshots, new RecordingLauncher());
        await DispatchAsync(router, "getSnapshot", null);
        foreach (var clonePath in fixture.Clones.Values)
        {
            TestGit.Run(clonePath, "tag", "-a", "v2.0.0", "-m", "annotated");
            TestGit.Run(clonePath, "push", "-q", "origin", "v2.0.0");
        }

        var result = await DispatchAsync(router, "runBatch", new
        {
            kind = "TAG",
            componentIds = new[] { "CF100A_Demo" },
            repoTypes = new[] { "IMPL" },
            args = new { tag = "v2.0.0", confirm = true }
        });
        var accepted = Assert.IsType<RunBatchResultDto>(Assert.IsType<CommandResult>(result).Data);
        var job = await WaitJobAsync(router, accepted.JobId);
        Assert.Equal("SUCCESS", job.State);
        var annotatedClone = fixture.Clones.Values.First();
        var commit = TestGit.Capture(annotatedClone, "rev-parse", "refs/tags/v2.0.0^{commit}");
        Assert.Equal(commit, TestGit.Capture(annotatedClone, "rev-parse", "HEAD"));
    }
    // ------------------------------------------------------------------ 参数/路径/令牌安全

    [Fact]
    public async Task RunBatch_ValidationErrors()
    {
        using var fixture = BatchWorkspace.Create("CF100A_Demo_Impl");
        var config = TestAudit.NewConfig(fixture.Root);
        using var snapshots = new SnapshotService(config);
        using var router = new HostCommandRouter(config, snapshots, new RecordingLauncher());
        await DispatchAsync(router, "getSnapshot", null);

        var cases = new (object Params, string ExpectedCode)[]
        {
            (new { kind = "RESET", componentIds = new[] { "CF100A_Demo" } }, BridgeErrorCodes.InvalidArgument),
            (new { kind = "FETCH", componentIds = Array.Empty<string>() }, BridgeErrorCodes.InvalidArgument),
            (new { kind = "FETCH", componentIds = new[] { "CF100A_Demo" }, repoTypes = new[] { "MAIN" } }, BridgeErrorCodes.InvalidArgument),
            (new { kind = "SYNC", componentIds = new[] { "CF100A_Demo" } }, BridgeErrorCodes.ConfirmRequired),
            (new { kind = "DELETE", componentIds = new[] { "CF100A_Demo" } }, BridgeErrorCodes.ConfirmRequired),
            // DELETE 的强确认（输入组件 ID）与 confirm=true 相互独立：缺 confirmText 同样拒绝。
            (new { kind = "DELETE", componentIds = new[] { "CF100A_Demo" }, args = new { confirm = true } }, BridgeErrorCodes.ConfirmRequired),
            (new { kind = "CREATE", componentIds = new[] { "CF100A_Demo" }, args = new { confirm = true } }, BridgeErrorCodes.InvalidArgument),
            (new { kind = "TAG", componentIds = new[] { "CF100A_Demo" }, args = new { confirm = true } }, BridgeErrorCodes.InvalidArgument),
            (new { kind = "COMMIT", componentIds = new[] { "CF100A_Demo" }, args = new { confirm = true } }, BridgeErrorCodes.InvalidArgument),
            (new { kind = "CHKOUT", componentIds = new[] { "CF100A_Demo" }, args = new { confirm = true } }, BridgeErrorCodes.InvalidArgument),
            (new { kind = "COMMIT", componentIds = new[] { "CF100A_Demo" }, args = new { message = "x" } }, BridgeErrorCodes.ConfirmRequired)
        };

        foreach (var (parameters, expectedCode) in cases)
        {
            var result = await DispatchAsync(router, "runBatch", parameters);
            Assert.False(result.Ok);
            Assert.Equal(expectedCode, result.Error!.Code);
        }

        var notFound = await DispatchAsync(router, "getJob", new { jobId = "missing-job" });
        Assert.Equal(BridgeErrorCodes.JobNotFound, notFound.Error!.Code);
        var cancelMissing = await DispatchAsync(router, "cancelJob", new { jobId = "missing-job" });
        Assert.Equal(BridgeErrorCodes.JobNotFound, cancelMissing.Error!.Code);
        var retryMissing = await DispatchAsync(router, "retryFailed", new { jobId = "missing-job" });
        Assert.Equal(BridgeErrorCodes.JobNotFound, retryMissing.Error!.Code);
        var badLimit = await DispatchAsync(router, "getJobs", new { limit = 0 });
        Assert.Equal(BridgeErrorCodes.InvalidArgument, badLimit.Error!.Code);
    }

    [Fact]
    public async Task RunBatch_Clone_PathEscapeComponentId_IsRejected()
    {
        using var remotes = new TempWorkspace();
        var remoteRoot = Path.Combine(remotes.Root, "remote");
        Directory.CreateDirectory(Path.Combine(remoteRoot, "RTCRepos"));
        using var workspace = new TempWorkspace();
        var config = CloneConfig(workspace.Root, remoteRoot, TestAudit.NewRoot(), TestAudit.NewJobsRoot());
        using var snapshots = new SnapshotService(config);
        using var router = new HostCommandRouter(config, snapshots, new RecordingLauncher());
        await DispatchAsync(router, "getSnapshot", null);

        var batch = await DispatchAsync(
            router,
            "runBatch",
            new
            {
                kind = "CLONE",
                componentIds = new[] { "..\\..\\evil" },
                repoTypes = new[] { "IMPL" }
            });
        var accepted = Assert.IsType<RunBatchResultDto>(Assert.IsType<CommandResult>(batch).Data);
        var job = await WaitJobAsync(router, accepted.JobId);

        Assert.Equal("FAILED", job.State);
        var item = Assert.Single(job.Items);
        Assert.Equal("FAILED", item.State);
        Assert.Equal(BridgeErrorCodes.PathInvalid, item.ErrorCode);
        // 工作区内外都不得出现逃逸目录。
        Assert.False(Directory.Exists(Path.Combine(workspace.Root, "evil_Impl")));
        Assert.False(Directory.Exists(Path.Combine(Directory.GetParent(workspace.Root)!.FullName, "evil_Impl")));
    }

    [Fact]
    public async Task RunBatch_TokenConfiguredWithoutGiteaConfig_FailsClosedAndAuditHasNoSecrets()
    {
        using var fixture = BatchWorkspace.Create("CF100A_Demo_Impl");
        var config = TestAudit.NewConfig(fixture.Root, TestToken);
        using var snapshots = new SnapshotService(config);
        using var router = new HostCommandRouter(config, snapshots, new RecordingLauncher());
        await DispatchAsync(router, "getSnapshot", null);

        var batch = await DispatchAsync(
            router,
            "runBatch",
            new { kind = "FETCH", componentIds = new[] { "CF100A_Demo" }, repoTypes = new[] { "IMPL" } });
        var accepted = Assert.IsType<RunBatchResultDto>(Assert.IsType<CommandResult>(batch).Data);
        var job = await WaitJobAsync(router, accepted.JobId);

        Assert.Equal("FAILED", job.State);
        Assert.Equal(BridgeErrorCodes.GiteaConfigInvalid, Assert.Single(job.Items).ErrorCode);

        var lines = ReadAuditLines(config);
        Assert.Single(lines);
        Assert.DoesNotContain(TestToken, lines[0], StringComparison.Ordinal);
        Assert.DoesNotContain(
            Convert.ToBase64String(Encoding.UTF8.GetBytes(TestToken + ":")),
            lines[0],
            StringComparison.Ordinal);
        Assert.DoesNotContain("Authorization", lines[0], StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>构造只读 RepoRef（直接驱动 BatchOperationService 的预检测试；不代表真实扫描结果）。</summary>
    private static RepoRef MakeRepoRef(
        string componentId,
        string repoType,
        string localPath,
        string state,
        string? headSha,
        string branch = "main")
    {
        var repo = new RepoDto(
            RepoType: repoType,
            State: state,
            SubType: null,
            Branch: branch,
            HeadSha: headSha,
            Ahead: 0,
            Behind: 0,
            DirtyCount: 0,
            UntrackedCount: 0,
            IsLocal: true,
            LocalPath: localPath,
            Flags: Array.Empty<string>(),
            RepoName: Path.GetFileName(localPath));
        var component = new ComponentDto(
            ComponentId: componentId,
            AggregateState: state,
            SeverityRank: 0,
            Branch: branch,
            Tag: null,
            MixedBranch: false,
            Flags: Array.Empty<string>(),
            Repos: new[] { repo });
        return new RepoRef(componentId, component, repo);
    }

    private static async Task<JobDto> RunBatchJobAsync(HostCommandRouter router, object parameters)
    {
        var result = await DispatchAsync(router, "runBatch", parameters);
        Assert.True(result.Ok, Describe(result));
        var accepted = Assert.IsType<RunBatchResultDto>(result.Data);
        return await WaitJobAsync(router, accepted.JobId);
    }

    private static async Task<JobDto> WaitJobAsync(
        HostCommandRouter router,
        string jobId,
        int timeoutMs = 60000) =>
        await WaitUntilAsync(router, jobId, IsTerminalJob, timeoutMs).ConfigureAwait(false);

    private static async Task<JobDto> WaitUntilAsync(
        HostCommandRouter router,
        string jobId,
        Func<JobDto, bool> predicate,
        int timeoutMs = 60000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        JobDto? last = null;
        while (DateTime.UtcNow < deadline)
        {
            var result = await DispatchAsync(router, "getJob", new { jobId });
            if (result.Ok)
            {
                last = Assert.IsType<JobDto>(result.Data);
                if (predicate(last))
                {
                    return last;
                }
            }

            await Task.Delay(25);
        }

        throw new TimeoutException(
            $"等待作业 {jobId} 超时；最后状态 {last?.State ?? "<none>"}，items="
            + string.Join(',', last?.Items.Select(item => item.State) ?? Array.Empty<string>()));
    }

    private static bool IsTerminalJob(JobDto job) =>
        job.State is "PARTIAL" or "SUCCESS" or "FAILED" or "CANCELED" or "INTERRUPTED" or "PREVIEW";

    private static async Task WaitConditionAsync(Func<bool> condition, int timeoutMs = 15000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(20);
        }

        throw new TimeoutException("等待条件超时");
    }

    private static Task<CommandResult> DispatchAsync(
        HostCommandRouter router,
        string method,
        object? parameters)
    {
        JsonElement? element = parameters is null
            ? null
            : JsonSerializer.SerializeToElement(parameters, WebJson);
        return router.DispatchAsync(method, element, CancellationToken.None);
    }

    private static List<string> ReadAuditLines(AppConfig config)
    {
        var path = Path.Combine(config.AuditRoot!, OperationAuditService.AuditFileName);
        if (!File.Exists(path))
        {
            return new List<string>();
        }

        return File.ReadAllLines(path).Where(line => line.Trim().Length > 0).ToList();
    }

    private static List<StoredJob> LoadStoredJobs(string jobsRoot) =>
        new JobStore(jobsRoot).Load().ToList();

    private static string Describe(CommandResult result) =>
        result.Ok ? "ok" : $"{result.Error?.Code}: {result.Error?.Message}";

    private static AppConfig CloneConfig(string workspaceRoot, string remoteRoot, string auditRoot, string jobsRoot) => new()
    {
        WorkspaceRoot = workspaceRoot,
        GiteaBaseUrl = "file:///" + remoteRoot.Replace('\\', '/').TrimStart('/'),
        GiteaOrg = "RTCRepos",
        AuditRoot = auditRoot,
        JobsRoot = jobsRoot
    };

    /// <summary>在 <paramref name="orgRoot"/> 下创建 bare remote 并种一个 main 提交。</summary>
    private static void CreateSeedBareRemote(string tempRoot, string orgRoot, string repoDirectoryName)
    {
        var barePath = Path.Combine(orgRoot, repoDirectoryName + ".git");
        TestGit.Run(orgRoot, "init", "--bare", "-q", "--initial-branch=main", barePath);
        var seed = Path.Combine(tempRoot, "seed-" + Guid.NewGuid().ToString("N"));
        TestGit.Run(tempRoot, "clone", "-q", barePath, seed);
        TestGit.ConfigureIdentity(seed);
        File.WriteAllText(Path.Combine(seed, "README.md"), "seed");
        TestGit.CommitAll(seed, "seed");
        TestGit.Run(seed, "push", "-q", "-u", "origin", "main");
        try
        {
            Directory.Delete(seed, recursive: true);
        }
        catch
        {
            // 临时目录清理失败不影响测试。
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: false);
        }

        foreach (var directory in Directory.EnumerateDirectories(source))
        {
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }
    }

    private static void MoveOriginTo(string repoPath, string url) =>
        TestGit.Run(repoPath, "remote", "set-url", "origin", url);

    private static SlowRepository CreateSlowRepository(HangingHttpServer server, string componentId)
    {
        var workspace = new TempWorkspace();
        var dirName = componentId + "_Impl";
        var repoPath = Path.Combine(workspace.Root, dirName);
        TestGit.InitRepo(repoPath);
        File.WriteAllText(Path.Combine(repoPath, "README.md"), "slow");
        TestGit.CommitAll(repoPath, "init");
        TestGit.Run(repoPath, "remote", "add", "origin", $"http://127.0.0.1:{server.Port}/{dirName}.git");
        return new SlowRepository(workspace, repoPath);
    }

    private sealed record SlowRepository(TempWorkspace Workspace, string RepoPath) : IDisposable
    {
        public string WorkspaceRoot => Workspace.Root;

        public void Dispose() => Workspace.Dispose();
    }

    /// <summary>
    /// 多仓临时工作区：<c>remote/&lt;repoDir&gt;.git</c>（bare）+ 工作区内每个仓的 clone（已配置身份）。
    /// 全部在 %TEMP% 下，Dispose 递归清理。
    /// </summary>
    private sealed class BatchWorkspace : IDisposable
    {
        private readonly TempWorkspace _workspace;
        private readonly TempWorkspace _remotes;

        private BatchWorkspace(
            TempWorkspace workspace,
            TempWorkspace remotes,
            Dictionary<string, string> bares,
            Dictionary<string, string> clones)
        {
            _workspace = workspace;
            _remotes = remotes;
            Bares = bares;
            Clones = clones;
        }

        public string Root => _workspace.Root;

        public IReadOnlyDictionary<string, string> Bares { get; }

        public IReadOnlyDictionary<string, string> Clones { get; }

        public static BatchWorkspace Create(params string[] repoDirectoryNames)
        {
            var remotes = new TempWorkspace();
            var workspace = new TempWorkspace();
            var remoteRoot = Path.Combine(remotes.Root, "remote");
            Directory.CreateDirectory(remoteRoot);

            var bares = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var clones = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in repoDirectoryNames)
            {
                var bare = Path.Combine(remoteRoot, name + ".git");
                TestGit.Run(remoteRoot, "init", "--bare", "-q", "--initial-branch=main", bare);

                var seed = Path.Combine(remotes.Root, "seed-" + Guid.NewGuid().ToString("N"));
                TestGit.Run(remotes.Root, "clone", "-q", bare, seed);
                TestGit.ConfigureIdentity(seed);
                File.WriteAllText(Path.Combine(seed, "README.md"), "seed:" + name);
                TestGit.CommitAll(seed, "seed");
                TestGit.Run(seed, "push", "-q", "-u", "origin", "main");

                var clone = Path.Combine(workspace.Root, name);
                TestGit.Run(workspace.Root, "clone", "-q", bare, clone);
                TestGit.ConfigureIdentity(clone);

                bares[name] = bare;
                clones[name] = clone;
            }

            return new BatchWorkspace(workspace, remotes, bares, clones);
        }

        public void Dispose()
        {
            _workspace.Dispose();
            _remotes.Dispose();
        }
    }

    /// <summary>
    /// 回环 TCP“挂起”服务器：接受连接、读取请求头后不响应（hold 住），用于验证 job 取消能杀掉挂起的 git 进程。
    /// </summary>
    private sealed class HangingHttpServer : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _lifetime = new();
        private readonly Task _acceptLoop;
        private int _connections;

        public HangingHttpServer()
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _acceptLoop = Task.Run(AcceptLoopAsync);
        }

        public int Port { get; }

        public int Connections => Volatile.Read(ref _connections);

        private async Task AcceptLoopAsync()
        {
            while (!_lifetime.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync(_lifetime.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (SocketException)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }

                Interlocked.Increment(ref _connections);
                _ = HoldClientAsync(client);
            }
        }

        private async Task HoldClientAsync(TcpClient client)
        {
            try
            {
                using (client)
                {
                    var stream = client.GetStream();
                    var buffer = new byte[16 * 1024];
                    // 读取请求头（不以 \r\n\r\n 结束也不返回响应，git 会一直等待）。
                    while (!_lifetime.IsCancellationRequested)
                    {
                        var read = await stream.ReadAsync(buffer, _lifetime.Token).ConfigureAwait(false);
                        if (read <= 0)
                        {
                            // 客户端已关闭（git 进程被取消杀掉）。
                            return;
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 服务器销毁。
            }
            catch
            {
                // 连接层错误不影响断言（git 进程可能已被取消）。
            }
        }

        public void Dispose()
        {
            _lifetime.Cancel();
            try
            {
                _listener.Stop();
            }
            catch
            {
                // 已停止。
            }

            try
            {
                _acceptLoop.Wait(TimeSpan.FromSeconds(2));
            }
            catch
            {
                // 取消等待超时。
            }

            _lifetime.Dispose();
        }
    }
}
