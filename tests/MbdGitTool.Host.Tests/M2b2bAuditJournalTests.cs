using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using MbdGitTool.Core.Contracts;
using MbdGitTool.Core.Domain;
using MbdGitTool.Host.Bridge;
using MbdGitTool.Host.Services;
using Xunit;

namespace MbdGitTool.Host.Tests;

/// <summary>
/// M2b-2b：审计回退/回填/重建（FR-OP-09 / ADR D-026 P1-4）+ 写前 journal/崩溃 reconcile（FR-SAFE-04）。
/// <para>覆盖：SQLite 不可用 → fallback.jsonl → 重启回填并清空；DB 缺行从 JSONL 回填；查询/导出合并
/// pending fallback；repairOperationLog 重建计数；journal STARTED→DONE/FAILED 收口；模拟强杀（STARTED
/// 无终态）后 reconcile 的 NOT_APPLIED/UNKNOWN_APPLIED 两分支且仓库零改动；审计每动作恰 1 行
/// （RECONCILE 额外行允许、job 行不重复）。</para>
/// </summary>
public sealed class M2b2bAuditJournalTests
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    // ------------------------------------------------------------------ 审计回退/回填/重建

    [Fact]
    public void SqliteUnavailable_RecordsIntoFallback_ThenNextStartupBackfillsAndClears()
    {
        var config = TestAudit.NewConfig();
        CreateDatabaseWithUserVersion(config.DatabasePath!, 99);

        // 运行期：SQLite 不可用（user_version 高于本程序）→ JSONL 主链路 + fallback 回退队列。
        var first = new OperationAuditService(config);
        Assert.False(first.DatabaseAvailable);

        var unavailable = first.GetOperationLog(new GetOperationLogParams());
        Assert.False(unavailable.Ok);
        Assert.Equal(BridgeErrorCodes.Internal, unavailable.Error!.Code);
        Assert.Equal("unavailable", (string?)unavailable.Error.Details!["database"]);

        first.Record(
            action: "COMMIT",
            componentId: "CF100A_Demo",
            repoType: "IMPL",
            state: "DIRTY",
            preHead: "1111111111111111111111111111111111111111",
            postHead: "2222222222222222222222222222222222222222",
            ok: true,
            errorCode: null,
            durationMs: 3,
            requestId: "fallback-1");
        first.Record(
            action: "PUSH",
            componentId: "CF100A_Demo",
            repoType: "IMPL",
            state: "AHEAD",
            preHead: null,
            postHead: null,
            ok: false,
            errorCode: BridgeErrorCodes.PushRejected,
            durationMs: 4,
            requestId: "fallback-2");

        Assert.Equal(2, ReadJsonl(config).Count);
        Assert.Equal(2, first.PendingFallbackCount);
        Assert.Equal(2, first.ReadFallbackLines().Count(line => line.Trim().Length > 0));
        var degraded = first.GetOperationLog(new GetOperationLogParams());
        Assert.False(degraded.Ok);
        Assert.Equal(
            2,
            Convert.ToInt32(degraded.Error!.Details!["pendingFallback"], System.Globalization.CultureInfo.InvariantCulture));

        // 重启：DB 恢复可写 → 构造时回填 JSONL/fallback，成功后清空 fallback。
        CreateDatabaseWithUserVersion(config.DatabasePath!, 0);
        var second = new OperationAuditService(config);
        Assert.True(second.DatabaseAvailable);
        Assert.Equal(2, second.Database!.Count());
        Assert.DoesNotContain(second.ReadFallbackLines(), line => line.Trim().Length > 0);

        var page = Assert.IsType<OperationLogPageDto>(second.GetOperationLog(new GetOperationLogParams()).Data);
        Assert.Equal(2, page.Total);
        Assert.Contains(page.Items, item => item.RequestId == "fallback-1");
        Assert.Contains(page.Items, item => item.RequestId == "fallback-2" && item.Result == "FAILED");

        // 再次重启：稳定键去重，不产生重复行。
        var third = new OperationAuditService(config);
        Assert.Equal(2, third.Database!.Count());
    }

    [Fact]
    public void BackfillFromJsonl_RestoresRowsMissingFromSqlite_WithoutDuplicates()
    {
        var config = TestAudit.NewConfig();
        var first = new OperationAuditService(config);
        first.Record(action: "COMMIT", componentId: "C1", repoType: "IMPL", state: "DIRTY", preHead: null,
            postHead: null, ok: true, errorCode: null, durationMs: 1, requestId: "keep-1");
        first.Record(action: "PUSH", componentId: "C1", repoType: "IMPL", state: "AHEAD", preHead: null,
            postHead: null, ok: true, errorCode: null, durationMs: 1, requestId: "drop-1");
        Assert.Equal(2, first.Database!.Count());

        DeleteDatabaseRows(config.DatabasePath!, "delete from operation_log where request_id = 'drop-1';");
        Assert.Equal(1, first.Database!.Count());

        var second = new OperationAuditService(config);
        Assert.Equal(2, second.Database!.Count());
        var page = Assert.IsType<OperationLogPageDto>(second.GetOperationLog(new GetOperationLogParams()).Data);
        Assert.Equal(2, page.Total);
        Assert.Contains(page.Items, item => item.RequestId == "drop-1");

        var third = new OperationAuditService(config);
        Assert.Equal(2, third.Database!.Count());
    }

    [Fact]
    public void GetOperationLogAndExport_MergePendingFallbackRows()
    {
        var config = TestAudit.NewConfig();
        var service = new OperationAuditService(config);
        service.Record(action: "COMMIT", componentId: "CF100A_Demo", repoType: "IMPL", state: "DIRTY",
            preHead: null, postHead: null, ok: true, errorCode: null, durationMs: 1, requestId: "db-row");

        // 模拟“已写 JSONL，但 SQLite 写失败”尚未回填的 pending 行。
        AppendFallback(service, new
        {
            ts = DateTime.UtcNow.ToString("O"),
            action = "PUSH",
            componentId = "CF100A_Demo",
            repoType = "IMPL",
            state = "AHEAD",
            preHead = (string?)null,
            postHead = (string?)null,
            ok = false,
            errorCode = BridgeErrorCodes.PushRejected,
            durationMs = 2,
            actor = "tester",
            requestId = "fallback-row",
            result = "FAILED"
        });

        var page = Assert.IsType<OperationLogPageDto>(
            service.GetOperationLog(new GetOperationLogParams()).Data);
        Assert.Equal(2, page.Total);
        Assert.Contains(page.Items, item => item.RequestId == "fallback-row" && item.Result == "FAILED");

        var export = service.ExportOperationLog(new ExportOperationLogParams(Format: "csv"));
        Assert.True(export.Ok, export.Error?.Code);
        var exported = Assert.IsType<OperationLogExportDto>(export.Data);
        Assert.Equal(2, exported.Count);
        Assert.Contains("fallback-row", File.ReadAllText(exported.Path), StringComparison.Ordinal);
    }

    [Fact]
    public void RepairOperationLog_RequiresConfirm_ThenRebuildsTableFromJsonlAndWritesSelfAudit()
    {
        var config = TestAudit.NewConfig();
        var service = new OperationAuditService(config);
        service.Record(action: "COMMIT", componentId: "C1", repoType: "IMPL", state: "DIRTY", preHead: null,
            postHead: null, ok: true, errorCode: null, durationMs: 1, requestId: "r1");
        service.Record(action: "PUSH", componentId: "C1", repoType: "IMPL", state: "AHEAD", preHead: null,
            postHead: null, ok: true, errorCode: null, durationMs: 1, requestId: "r2");

        // 历史测试污染：DB 比 JSONL 多 2 行（无对应 JSONL 行）。
        InsertPollutedRow(service.Database!, "pollution-1");
        InsertPollutedRow(service.Database!, "pollution-2");
        Assert.Equal(4, service.Database!.Count());

        var rejected = service.RepairOperationLog(new RepairOperationLogParams(Confirm: false));
        Assert.False(rejected.Ok);
        Assert.Equal(BridgeErrorCodes.ConfirmRequired, rejected.Error!.Code);
        Assert.Equal(4, service.Database!.Count());

        var repaired = service.RepairOperationLog(new RepairOperationLogParams(Confirm: true));
        Assert.True(repaired.Ok, repaired.Error?.Code);
        var data = Assert.IsType<RepairOperationLogResultDto>(repaired.Data);
        Assert.Equal(4, data.Removed);
        Assert.Equal(2, data.Inserted);
        Assert.Equal(3, data.Total); // 2 条回填 + 1 条自身审计

        Assert.Equal(3, service.Database!.Count());
        var selfAudit = Assert.IsType<OperationLogPageDto>(
            service.GetOperationLog(new GetOperationLogParams(Action: OperationAuditService.RepairOperationLogAction)).Data);
        Assert.Equal(1, selfAudit.Total);
        Assert.DoesNotContain(service.ReadFallbackLines(), line => line.Trim().Length > 0);
        Assert.Equal(3, ReadJsonl(config).Count);
    }

    // ------------------------------------------------------------------ journal 收口

    [Fact]
    public void Journal_BeginCompleteFail_WritesExpectedStatuses_AndNoUnresolved()
    {
        var config = TestAudit.NewConfig();
        var journal = new OperationJournal(config);
        var localPath = Path.Combine(config.AuditRoot!, "repo");

        var done = journal.Begin(
            "COMMIT",
            "CF100A_Demo",
            "IMPL",
            "1111111111111111111111111111111111111111",
            localPath,
            jobId: "job-1",
            jobItemId: "job-1:1");
        Assert.NotNull(done);
        journal.Complete(done, "2222222222222222222222222222222222222222");

        var failed = journal.Begin("PUSH", "CF100A_Demo", "IMPL", "2222222222222222222222222222222222222222");
        Assert.NotNull(failed);
        journal.Fail(failed, BridgeErrorCodes.PushRejected);

        var lines = journal.ReadAll();
        Assert.Equal(4, lines.Count);
        Assert.Equal(
            new[] { OperationJournal.StatusStarted, OperationJournal.StatusDone, OperationJournal.StatusStarted, OperationJournal.StatusFailed },
            lines.Select(line => line.Status));
        Assert.Equal("job-1", lines[0].JobId);
        Assert.Equal("job-1:1", lines[0].JobItemId);
        Assert.Equal("2222222222222222222222222222222222222222", lines[1].PostHead);
        Assert.Equal(BridgeErrorCodes.PushRejected, lines[3].ErrorCode);
        Assert.Equal(0, journal.CountUnresolved());

        // JSONL 形状：id/ts/action/componentId/repoType/preHead/status（+可选 localPath/job 关联）。
        using var document = JsonDocument.Parse(File.ReadAllLines(journal.FilePath)[0]);
        var root = document.RootElement;
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("id").GetString()));
        Assert.True(root.GetProperty("ts").GetString()!.EndsWith('Z'));
        Assert.Equal("COMMIT", root.GetProperty("action").GetString());
        Assert.Equal("CF100A_Demo", root.GetProperty("componentId").GetString());
        Assert.Equal("IMPL", root.GetProperty("repoType").GetString());
        Assert.Equal(OperationJournal.StatusStarted, root.GetProperty("status").GetString());
        Assert.Equal("1111111111111111111111111111111111111111", root.GetProperty("preHead").GetString());
    }

    [Fact]
    public void Journal_ReadUnresolved_ReturnsOnlyStartedWithoutTerminal_WithJobAssociation()
    {
        var config = TestAudit.NewConfig();
        var journal = new OperationJournal(config);
        var started = journal.Begin(
            "SYNC",
            "CF100A_Demo",
            "IMPL",
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            Path.Combine(config.AuditRoot!, "repo"),
            jobId: "job-crash",
            jobItemId: "job-crash:1");
        var finished = journal.Begin("TAG", "CF100A_Demo", "SIL", "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");
        journal.Fail(finished, BridgeErrorCodes.GitFailed);

        var unresolved = Assert.Single(journal.ReadUnresolved());
        Assert.Equal(started!.Id, unresolved.Id);
        Assert.Equal("SYNC", unresolved.Action);
        Assert.Equal("job-crash", unresolved.JobId);
        Assert.Equal("job-crash:1", unresolved.JobItemId);
    }

    [Fact]
    public async Task SingleRepoAction_Success_WritesJournalStartedThenDone_AndAuditExactlyOneRow()
    {
        using var fixture = GitRemoteFixture.Create();
        var config = TestAudit.NewConfig(fixture.Root);
        var journal = new OperationJournal(config);
        var audit = new OperationAuditService(config);
        var actions = new RepoActionService(journal: journal);
        var head = GitRemoteFixture.Head(fixture.CloneB);
        var index = IndexFor(fixture.CloneB, state: "DIRTY");

        File.WriteAllText(Path.Combine(fixture.CloneB, "journal-commit.txt"), "x");
        TestGit.Run(fixture.CloneB, "add", "journal-commit.txt");

        var result = await actions.RunAsync(
            Params("COMMIT", new { message = "journal commit", confirm = true }),
            index,
            CancellationToken.None,
            execution => audit.Record(execution));

        Assert.True(result.Ok, result.Error?.Code);
        var lines = journal.ReadAll();
        Assert.Equal(2, lines.Count);
        Assert.Equal(OperationJournal.StatusStarted, lines[0].Status);
        Assert.Equal(OperationJournal.StatusDone, lines[1].Status);
        Assert.Equal("COMMIT", lines[1].Action);
        Assert.Equal(head, lines[0].PreHead);
        Assert.Equal(GitRemoteFixture.Head(fixture.CloneB), lines[1].PostHead);
        Assert.Equal(0, journal.CountUnresolved());

        // 审计仍恰 1 行（journal 不额外写审计行）。
        var auditLine = Assert.Single(ReadJsonl(config));
        using var document = JsonDocument.Parse(auditLine);
        Assert.Equal("COMMIT", document.RootElement.GetProperty("action").GetString());
        Assert.True(document.RootElement.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task SingleRepoAction_Failure_WritesJournalFailed_WithErrorCode()
    {
        using var fixture = GitRemoteFixture.Create();
        var config = TestAudit.NewConfig(fixture.Root);
        var journal = new OperationJournal(config);
        var actions = new RepoActionService(journal: journal);
        var index = IndexFor(fixture.CloneB);

        // 干净仓 COMMIT → E_NOTHING_TO_COMMIT（失败也收口，不留 STARTED）。
        var result = await actions.RunAsync(
            Params("COMMIT", new { message = "nothing", confirm = true }),
            index,
            CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Equal(BridgeErrorCodes.NothingToCommit, result.Error!.Code);
        var lines = journal.ReadAll();
        Assert.Equal(2, lines.Count);
        Assert.Equal(OperationJournal.StatusStarted, lines[0].Status);
        Assert.Equal(OperationJournal.StatusFailed, lines[1].Status);
        Assert.Equal(BridgeErrorCodes.NothingToCommit, lines[1].ErrorCode);
        Assert.Equal(0, journal.CountUnresolved());
    }

    [Fact]
    public async Task SingleRepoAction_Exception_WritesJournalFailed_AndRethrows()
    {
        using var fixture = GitRemoteFixture.Create();
        var config = TestAudit.NewConfig(fixture.Root);
        var journal = new OperationJournal(config);
        var audit = new OperationAuditService(config);
        var head = GitRemoteFixture.Head(fixture.CloneB);
        var actions = new RepoActionService(git: new ThrowingGitCli(head), journal: journal);

        await Assert.ThrowsAsync<InvalidOperationException>(() => actions.RunAsync(
            Params("FETCH"),
            IndexFor(fixture.CloneB),
            CancellationToken.None,
            execution => audit.Record(execution)));

        var lines = journal.ReadAll();
        Assert.Equal(2, lines.Count);
        Assert.Equal(OperationJournal.StatusStarted, lines[0].Status);
        Assert.Equal(OperationJournal.StatusFailed, lines[1].Status);
        Assert.Equal(BridgeErrorCodes.Internal, lines[1].ErrorCode);
        Assert.Equal(0, journal.CountUnresolved());
        var auditLine = Assert.Single(ReadJsonl(config));
        using var document = JsonDocument.Parse(auditLine);
        Assert.False(document.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal(BridgeErrorCodes.Internal, document.RootElement.GetProperty("errorCode").GetString());
    }

    // ------------------------------------------------------------------ 崩溃 reconcile（两分支）

    [Fact]
    public void Reconcile_HeadUnchanged_MarksNotApplied_WritesOneReconcileAudit_AndLeavesRepoUntouched()
    {
        using var fixture = GitRemoteFixture.Create();
        var config = TestAudit.NewConfig(fixture.Root);
        var journal = new OperationJournal(config);
        var audit = new OperationAuditService(config);
        var head = GitRemoteFixture.Head(fixture.CloneB);
        var statusBefore = TestGit.Capture(fixture.CloneB, "status", "--porcelain");

        // 模拟“git 写前 STARTED 已落盘，进程被杀后没有 DONE/FAILED”。
        journal.Begin("COMMIT", "CF100A_Demo", "IMPL", head, fixture.CloneB);
        Assert.Equal(1, journal.CountUnresolved());

        var report = new JournalReconciler(journal, audit).Reconcile();

        Assert.Equal(1, report.Total);
        Assert.Equal(1, report.NotApplied);
        Assert.Equal(0, report.UnknownApplied);
        Assert.Equal(JournalReconciler.NotApplied, report.Items[0].Status);
        Assert.Equal(0, journal.CountUnresolved());

        // reconcile 行：action=RECONCILE / result=NOT_APPLIED / preHead==postHead==当前 HEAD，且不猜成功。
        var auditLine = Assert.Single(ReadJsonl(config));
        using (var document = JsonDocument.Parse(auditLine))
        {
            var root = document.RootElement;
            Assert.Equal(JournalReconciler.ReconcileAction, root.GetProperty("action").GetString());
            Assert.Equal(JournalReconciler.NotApplied, root.GetProperty("result").GetString());
            Assert.True(root.GetProperty("ok").GetBoolean());
            Assert.Equal(head, root.GetProperty("preHead").GetString());
            Assert.Equal(head, root.GetProperty("postHead").GetString());
        }

        Assert.Equal(head, GitRemoteFixture.Head(fixture.CloneB));
        Assert.Equal(statusBefore, TestGit.Capture(fixture.CloneB, "status", "--porcelain"));
    }

    [Fact]
    public void Reconcile_HeadChanged_MarksUnknownApplied_DoesNotGuessSuccess_AndLeavesRepoUntouched()
    {
        using var fixture = GitRemoteFixture.Create();
        var config = TestAudit.NewConfig(fixture.Root);
        var journal = new OperationJournal(config);
        var audit = new OperationAuditService(config);
        var oldHead = GitRemoteFixture.Head(fixture.CloneB);
        journal.Begin("COMMIT", "CF100A_Demo", "IMPL", oldHead, fixture.CloneB);

        // 模拟崩溃前写操作其实已落地：HEAD 已变化（reconcile 只能判定 UNKNOWN_APPLIED）。
        File.WriteAllText(Path.Combine(fixture.CloneB, "crash-applied.txt"), "applied");
        TestGit.CommitAll(fixture.CloneB, "crash applied");
        var appliedHead = GitRemoteFixture.Head(fixture.CloneB);
        var statusBefore = TestGit.Capture(fixture.CloneB, "status", "--porcelain");

        var report = new JournalReconciler(journal, audit).Reconcile();

        Assert.Equal(1, report.Total);
        Assert.Equal(0, report.NotApplied);
        Assert.Equal(1, report.UnknownApplied);
        Assert.Equal(JournalReconciler.UnknownApplied, report.Items[0].Status);
        Assert.Equal(0, journal.CountUnresolved());

        var auditLine = Assert.Single(ReadJsonl(config));
        using (var document = JsonDocument.Parse(auditLine))
        {
            var root = document.RootElement;
            Assert.Equal(JournalReconciler.ReconcileAction, root.GetProperty("action").GetString());
            Assert.Equal(JournalReconciler.UnknownApplied, root.GetProperty("result").GetString());
            Assert.False(root.GetProperty("ok").GetBoolean()); // 不猜测成功
            Assert.Equal(BridgeErrorCodes.ReconcileUnknown, root.GetProperty("errorCode").GetString());
            Assert.Equal(oldHead, root.GetProperty("preHead").GetString());
            Assert.Equal(appliedHead, root.GetProperty("postHead").GetString());
        }

        // reconcile 零仓库改动：HEAD/工作区完全不变。
        Assert.Equal(appliedHead, GitRemoteFixture.Head(fixture.CloneB));
        Assert.Equal(statusBefore, TestGit.Capture(fixture.CloneB, "status", "--porcelain"));
    }

    [Fact]
    public async Task Router_StartupReconcile_WritesReconcileAudit_AndAnnotatesJobCenterItem()
    {
        using var fixture = GitRemoteFixture.Create();
        var config = TestAudit.NewConfig(fixture.Root);
        var head = GitRemoteFixture.Head(fixture.CloneB);
        var jobId = "job-crash-1";
        var jobItemId = jobId + ":1";

        // 模拟崩溃前 job 已持久化（job/item RUNNING，重启加载收敛为 INTERRUPTED）且 journal 留下 STARTED。
        var now = DateTime.UtcNow.ToString("O");
        new JobStore(config).Save(new[]
        {
            new StoredJob(
                jobId,
                "SYNC",
                "RUNNING",
                now,
                now,
                null,
                new[]
                {
                    new StoredJobItem(
                        "CF100A_Demo",
                        "IMPL",
                        "CF100A_Demo_Impl",
                        "RUNNING",
                        now,
                        null,
                        null,
                        null,
                        head,
                        null,
                        null)
                },
                new StoredJobArguments(null, null, null, false, false, false, false, false, false, null),
                "tester",
                "req-crash")
        });
        new OperationJournal(config).Begin("SYNC", "CF100A_Demo", "IMPL", head, fixture.CloneB, jobId, jobItemId);

        using var snapshots = new SnapshotService(config);
        using var router = new HostCommandRouter(config, snapshots, new RecordingLauncher());
        await router.StartupReconcile;

        var jobResult = await DispatchAsync(router, "getJob", new { jobId });
        Assert.True(jobResult.Ok, jobResult.Error?.Code);
        var job = Assert.IsType<JobDto>(jobResult.Data);
        var item = Assert.Single(job.Items);
        Assert.Equal("INTERRUPTED", item.State);
        Assert.Contains("journal reconcile", item.Message, StringComparison.Ordinal);
        Assert.Contains(JournalReconciler.NotApplied, item.Message, StringComparison.Ordinal);

        // RECONCILE 审计行含 job 关联（任务中心可检索）；原 job item 审计行仍恰 1 行、不重复。
        var reconcilePage = Assert.IsType<OperationLogPageDto>(
            (await DispatchAsync(router, "getOperationLog", new { action = JournalReconciler.ReconcileAction })).Data);
        Assert.Equal(1, reconcilePage.Total);
        var reconcileRow = Assert.Single(reconcilePage.Items);
        Assert.Equal(JournalReconciler.NotApplied, reconcileRow.Result);
        Assert.Equal(jobId, reconcileRow.JobId);
        Assert.Equal(jobItemId, reconcileRow.JobItemId);

        var allRows = Assert.IsType<OperationLogPageDto>(
            (await DispatchAsync(router, "getOperationLog", new { limit = 100 })).Data);
        var itemRows = allRows.Items.Where(row => row.JobItemId == jobItemId).ToList();
        Assert.Equal(2, itemRows.Count); // 1 条 job item 审计 + 1 条 reconcile
        Assert.Single(itemRows, row => row.Action != JournalReconciler.ReconcileAction);

        Assert.Equal(head, GitRemoteFixture.Head(fixture.CloneB));
    }

    // ------------------------------------------------------------------ job item 写前 journal

    [Fact]
    public async Task BatchRepoActionJobItem_JournalCarriesJobAssociation_AndAuditOneRow()
    {
        using var fixture = GitRemoteFixture.Create();
        var config = TestAudit.NewConfig(fixture.Root);
        using var snapshots = new SnapshotService(config);
        using var router = new HostCommandRouter(config, snapshots, new RecordingLauncher());
        Assert.True((await DispatchAsync(router, "getSnapshot", null)).Ok);

        // 单仓五键在批量 job 中执行：journal 由 RepoActionService 写，必须携带 jobId/jobItemId。
        var batch = await DispatchAsync(router, "runBatch", new
        {
            kind = "FETCH",
            componentIds = new[] { "CF100A_Demo" },
            repoTypes = new[] { "IMPL" }
        });
        Assert.True(batch.Ok, batch.Error?.Code);
        var accepted = Assert.IsType<RunBatchResultDto>(batch.Data);
        var job = await WaitJobAsync(router, accepted.JobId);
        Assert.Equal("SUCCESS", job.State);

        var journal = new OperationJournal(config);
        var lines = journal.ReadAll();
        Assert.Equal(2, lines.Count);
        Assert.Equal(OperationJournal.StatusStarted, lines[0].Status);
        Assert.Equal(OperationJournal.StatusDone, lines[1].Status);
        Assert.Equal("FETCH", lines[0].Action);
        Assert.Equal(accepted.JobId, lines[0].JobId);
        Assert.Equal(accepted.JobId + ":1", lines[0].JobItemId);
        Assert.Equal(0, journal.CountUnresolved());

        var auditLine = Assert.Single(ReadJsonl(config));
        using var document = JsonDocument.Parse(auditLine);
        Assert.Equal("FETCH", document.RootElement.GetProperty("action").GetString());
        Assert.Equal(accepted.JobId, document.RootElement.GetProperty("jobId").GetString());
        Assert.Equal(accepted.JobId + ":1", document.RootElement.GetProperty("jobItemId").GetString());
    }

    [Fact]
    public async Task BatchJob_Item_WritesJournalStartedThenDone_AndAuditOneRowPerItem()
    {
        using var fixture = GitRemoteFixture.Create();
        var config = TestAudit.NewConfig(fixture.Root);
        using var snapshots = new SnapshotService(config);
        using var router = new HostCommandRouter(config, snapshots, new RecordingLauncher());
        Assert.True((await DispatchAsync(router, "getSnapshot", null)).Ok);
        var head = GitRemoteFixture.Head(fixture.CloneB);

        var batch = await DispatchAsync(router, "runBatch", new
        {
            kind = "CREATE",
            componentIds = new[] { "CF100A_Demo" },
            repoTypes = new[] { "IMPL" },
            args = new { branch = "journal-branch", confirm = true }
        });
        Assert.True(batch.Ok, batch.Error?.Code);
        var accepted = Assert.IsType<RunBatchResultDto>(batch.Data);
        var job = await WaitJobAsync(router, accepted.JobId);
        Assert.Equal("SUCCESS", job.State);

        var journal = new OperationJournal(config);
        var lines = journal.ReadAll();
        Assert.Equal(2, lines.Count);
        Assert.Equal(OperationJournal.StatusStarted, lines[0].Status);
        Assert.Equal(OperationJournal.StatusDone, lines[1].Status);
        Assert.Equal("CREATE", lines[0].Action);
        Assert.Equal("CF100A_Demo", lines[0].ComponentId);
        Assert.Equal("IMPL", lines[0].RepoType);
        Assert.Equal(head, lines[0].PreHead);
        Assert.Equal(accepted.JobId, lines[0].JobId);
        Assert.Equal(accepted.JobId + ":1", lines[0].JobItemId);
        Assert.Equal(0, journal.CountUnresolved());

        // job 行审计仍恰 1 行（RECONCILE 无额外行：journal 已全部收口）。
        var auditLine = Assert.Single(ReadJsonl(config));
        using var document = JsonDocument.Parse(auditLine);
        Assert.Equal("CREATE", document.RootElement.GetProperty("action").GetString());
        Assert.Equal(accepted.JobId, document.RootElement.GetProperty("jobId").GetString());
        Assert.Equal(accepted.JobId + ":1", document.RootElement.GetProperty("jobItemId").GetString());
    }

    // ------------------------------------------------------------------ helpers

    private static RunRepoActionParams Params(string action, object? args = null)
    {
        IReadOnlyDictionary<string, JsonElement>? dictionary = null;
        if (args is not null)
        {
            dictionary = JsonSerializer.SerializeToElement(args, WebJson)
                .EnumerateObject()
                .ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal);
        }

        return new RunRepoActionParams
        {
            ComponentId = "CF100A_Demo",
            RepoType = "IMPL",
            Action = action,
            Args = dictionary
        };
    }

    private static SnapshotIndex IndexFor(string repoPath, string state = "SYNCED") =>
        TestData.Index(
            Path.GetDirectoryName(repoPath) ?? repoPath,
            new ComponentSpec("CF100A_Demo", new[]
            {
                new RepoSpec(
                    "IMPL",
                    repoPath,
                    State: state,
                    Branch: "main",
                    HeadSha: GitRemoteFixture.Head(repoPath))
            }));

    private static List<string> ReadJsonl(AppConfig config)
    {
        var path = Path.Combine(config.AuditRoot!, OperationAuditService.AuditFileName);
        return File.Exists(path)
            ? File.ReadAllLines(path).Where(line => line.Trim().Length > 0).ToList()
            : new List<string>();
    }

    private static void AppendFallback(OperationAuditService service, object line)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(service.FallbackFilePath)!);
        File.AppendAllText(
            service.FallbackFilePath,
            JsonSerializer.Serialize(line, WebJson) + Environment.NewLine);
    }

    private static void InsertPollutedRow(AuditDatabase database, string requestId)
    {
        Assert.True(database.Insert(new OperationLogEntry(
            Ts: DateTime.UtcNow.ToString("O"),
            Actor: "tester",
            Action: "FETCH",
            ComponentId: "C1",
            RepoType: "IMPL",
            TargetJson: null,
            BeforeJson: null,
            AfterJson: null,
            Result: "OK",
            ErrorCode: null,
            RequestId: requestId,
            JobId: null,
            JobItemId: null,
            BackupRef: null)));
    }

    private static void CreateDatabaseWithUserVersion(string databasePath, int userVersion)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        SqliteConnection.ClearAllPools();
        using (var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString()))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version = " + userVersion.ToString(System.Globalization.CultureInfo.InvariantCulture) + ";";
            command.ExecuteNonQuery();
        }

        SqliteConnection.ClearAllPools();
    }

    private static void DeleteDatabaseRows(string databasePath, string sql)
    {
        SqliteConnection.ClearAllPools();
        using (var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString()))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }

        SqliteConnection.ClearAllPools();
    }

    private static async Task<JobDto> WaitJobAsync(HostCommandRouter router, string jobId, int timeoutMs = 60000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        JobDto? last = null;
        while (DateTime.UtcNow < deadline)
        {
            var result = await DispatchAsync(router, "getJob", new { jobId });
            if (result.Ok)
            {
                last = Assert.IsType<JobDto>(result.Data);
                if (last.State is "PARTIAL" or "SUCCESS" or "FAILED" or "CANCELED" or "INTERRUPTED" or "PREVIEW")
                {
                    return last;
                }
            }

            await Task.Delay(25);
        }

        throw new TimeoutException($"等待作业 {jobId} 超时；最后状态 {last?.State ?? "<none>"}");
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

    /// <summary>故障注入：rev-parse 返回成功 HEAD，其余 git 调用抛异常（验证 journal 异常收口）。</summary>
    private sealed class ThrowingGitCli : GitCli
    {
        private readonly string _head;

        public ThrowingGitCli(string head) => _head = head;

        public override Task<GitResult> RunAsync(
            string workingDirectory,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken = default,
            TimeSpan? timeout = null,
            IReadOnlyDictionary<string, string>? extraEnvironment = null)
        {
            if (arguments.Count > 0 && arguments[0] == "rev-parse")
            {
                return Task.FromResult(new GitResult(0, _head, string.Empty));
            }

            throw new InvalidOperationException("simulated git failure");
        }
    }
}
