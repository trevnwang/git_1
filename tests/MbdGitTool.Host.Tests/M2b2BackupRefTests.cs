using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MbdGitTool.Core.Contracts;
using MbdGitTool.Host.Services;
using Xunit;

namespace MbdGitTool.Host.Tests;

/// <summary>
/// M2b-2 备份 ref 列表/恢复/保留清理（SRS FR-OP-11、D-017/H-05、AC-OP-30）：
/// <c>for-each-ref refs/backup/sync</c>、恢复预检（ref 属于备份前缀且存在、脏工作区阻断、安全备份 ref）+
/// <c>reset --hard</c>、超期只删本地 ref 不触远端、<c>cleanupRetention</c> 端到端（.trash/备份 ref/审计）。
/// </summary>
public sealed class M2b2BackupRefTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    [Fact]
    public async Task GetBackupRefs_ReturnsRefShaCreatedAtAndRepoType_NewestFirst()
    {
        var repo = CreateRepoWithCommits(out var first, out var second);
        CreateBackupRef(repo, DaysAgo(5), "IMPL", first);
        CreateBackupRef(repo, DaysAgo(1), "IMPL", second);

        var result = await new BackupRefService().GetBackupRefsAsync(repo, "IMPL", CancellationToken.None);

        Assert.True(result.Ok, result.Error?.Code);
        var page = Assert.IsType<BackupRefsDto>(result.Data);
        Assert.Equal(2, page.Refs.Count);
        Assert.Contains(page.Refs, reference => reference.Sha == first);
        Assert.Contains(page.Refs, reference => reference.Sha == second);
        Assert.All(page.Refs, reference => Assert.Equal("IMPL", reference.RepoType));
        Assert.All(
            page.Refs,
            reference => Assert.Equal(
                TimeSpan.Zero,
                DateTimeOffset.Parse(reference.CreatedAt, CultureInfo.InvariantCulture).Offset));
        Assert.StartsWith("refs/backup/sync/", page.Refs[0].Ref, StringComparison.Ordinal);
        Assert.True(
            string.CompareOrdinal(page.Refs[0].Ref, page.Refs[1].Ref) > 0,
            "应按 ref（时间戳）倒序：新备份在前");

        var designOnly = await new BackupRefService().GetBackupRefsAsync(repo, "DESIGN", CancellationToken.None);
        Assert.True(designOnly.Ok);
        Assert.Empty(Assert.IsType<BackupRefsDto>(designOnly.Data).Refs);
    }

    [Fact]
    public async Task Restore_MovesHeadBackToBackupRef_AndSavesSafetyRef_Retryable()
    {
        var repo = CreateRepoWithCommits(out var first, out var second);
        var backupStamp = DaysAgo(1);
        CreateBackupRef(repo, backupStamp, "IMPL", first);

        var service = new BackupRefService();
        var (result, error) = await service.RestoreAsync(
            repo,
            "IMPL",
            $"refs/backup/sync/{backupStamp}/IMPL",
            CancellationToken.None);

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.True(result!.Restored);
        Assert.Equal(first, result.PostHead);
        Assert.Equal(second, result.PreHead);
        Assert.Equal(first, TestGit.Capture(repo, "rev-parse", "HEAD"));
        Assert.False(string.IsNullOrWhiteSpace(result.SafetyRef));
        Assert.StartsWith(BackupRefService.RestoreRefPrefix, result.SafetyRef, StringComparison.Ordinal);
        Assert.Equal(second, TestGit.Capture(repo, "rev-parse", result.SafetyRef!));

        // 失败可重试：同一 ref 再恢复一次仍成功（幂等），安全备份 ref 仍指向“恢复前 HEAD”。
        var (again, againError) = await service.RestoreAsync(
            repo,
            "IMPL",
            $"refs/backup/sync/{backupStamp}/IMPL",
            CancellationToken.None);
        Assert.Null(againError);
        Assert.True(again!.Restored);
        Assert.Equal(first, TestGit.Capture(repo, "rev-parse", "HEAD"));
        Assert.Equal(first, TestGit.Capture(repo, "rev-parse", again.SafetyRef!));
    }

    [Fact]
    public async Task Restore_RejectsNonBackupRefMissingRefAndTypeMismatch()
    {
        var repo = CreateRepoWithCommits(out var first, out _);
        var service = new BackupRefService();

        var (_, headsRefError) = await service.RestoreAsync(repo, "IMPL", "refs/heads/main", CancellationToken.None);
        Assert.Equal(BridgeErrorCodes.InvalidArgument, headsRefError!.Code);

        var (_, traversalError) = await service.RestoreAsync(
            repo, "IMPL", "refs/backup/sync/20260101000000/../../evil", CancellationToken.None);
        Assert.Equal(BridgeErrorCodes.InvalidArgument, traversalError!.Code);

        var (_, missingError) = await service.RestoreAsync(
            repo, "IMPL", $"refs/backup/sync/{DaysAgo(1)}/IMPL", CancellationToken.None);
        Assert.Equal(BridgeErrorCodes.RefNotFound, missingError!.Code);

        CreateBackupRef(repo, DaysAgo(1), "DESIGN", first);
        var (_, mismatchError) = await service.RestoreAsync(
            repo, "IMPL", $"refs/backup/sync/{DaysAgo(1)}/DESIGN", CancellationToken.None);
        Assert.Equal(BridgeErrorCodes.InvalidArgument, mismatchError!.Code);

        var (_, badTypeError) = await service.RestoreAsync(repo, "MAIN", "refs/backup/sync/20260101000000/IMPL",
            CancellationToken.None);
        Assert.Equal(BridgeErrorCodes.InvalidArgument, badTypeError!.Code);
    }

    [Fact]
    public async Task Restore_DirtyTrackedWorktree_BlockedWithoutChangingAnything()
    {
        var repo = CreateRepoWithCommits(out var first, out var second);
        var stamp = DaysAgo(1);
        CreateBackupRef(repo, stamp, "IMPL", first);
        var readme = Path.Combine(repo, "README.md");
        File.WriteAllText(readme, "local dirty change");

        var (result, error) = await new BackupRefService().RestoreAsync(
            repo, "IMPL", $"refs/backup/sync/{stamp}/IMPL", CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(BridgeErrorCodes.DirtyWorktree, error!.Code);
        Assert.False(error.Details!.ContainsKey("retryable")); // 脏工作区需先处理，不是简单重试
        Assert.Equal(second, TestGit.Capture(repo, "rev-parse", "HEAD"));
        Assert.Equal("local dirty change", File.ReadAllText(readme));
    }

    [Fact]
    public async Task Restore_UntrackedFile_IsBlockedAndPreserved()
    {
        var repo = CreateRepoWithCommits(out var first, out _);
        var stamp = DaysAgo(1);
        CreateBackupRef(repo, stamp, "IMPL", first);
        var untracked = Path.Combine(repo, "keep-me.txt");
        File.WriteAllText(untracked, "untracked");

        var (result, error) = await new BackupRefService().RestoreAsync(
            repo, "IMPL", $"refs/backup/sync/{stamp}/IMPL", CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(BridgeErrorCodes.WorktreeOverwrite, error!.Code);
        Assert.Equal("untracked", File.ReadAllText(untracked));
    }

    [Theory]
    [InlineData("refs/backup/sync/20260101000000/IMPL", true)]
    [InlineData("refs/backup/sync/20260101000000/impl", true)]
    [InlineData("refs/backup/sync/2026010100000/IMPL", false)] // stamp 非 14 位
    [InlineData("refs/backup/sync/abcdefghijklmn/IMPL", false)] // stamp 非数字
    [InlineData("refs/backup/sync/20260101000000/MAIN", false)] // 非四仓类型
    [InlineData("refs/backup/sync/20260101000000/IMPL/extra", false)]
    [InlineData("refs/backup/restore/20260101000000/IMPL", false)]
    [InlineData("refs/heads/main", false)]
    [InlineData("", false)]
    public void TryParseBackupRef_ValidatesPrefixStampAndRepoType(string value, bool expected)
    {
        Assert.Equal(expected, BackupRefService.TryParseBackupRef(value, out _, out _, out _));
    }

    [Fact]
    public async Task CleanupExpired_DeletesOnlyOldLocalRefs_NotRemote()
    {
        var config = TestAudit.NewConfig(_workspace.Root);
        var repo = CreateRepoWithCommits(out var first, out var second);
        var bareRemote = Path.Combine(_workspace.Root, "remote.git");
        TestGit.Run(_workspace.Root, "init", "--bare", "-q", bareRemote);
        TestGit.Run(repo, "remote", "add", "origin", bareRemote);
        TestGit.Run(repo, "push", "-q", "-u", "origin", "HEAD");

        var oldStamp = DaysAgo(31);
        var newStamp = DaysAgo(1);
        CreateBackupRef(repo, oldStamp, "IMPL", first);
        CreateBackupRef(repo, newStamp, "IMPL", second);

        var removed = await new BackupRefService(config).CleanupExpiredAsync(30, CancellationToken.None);

        Assert.Equal(1, removed);
        var refs = TestGit.Capture(repo, "for-each-ref", "--format=%(refname)", "refs/backup/");
        Assert.DoesNotContain(oldStamp, refs, StringComparison.Ordinal);
        Assert.Contains(newStamp, refs, StringComparison.Ordinal);

        // 远端（bare）不含 backup ref（清理只删本地；SYNC 备份 ref 从不上推）。
        var remoteRefs = TestGit.Capture(bareRemote, "for-each-ref", "--format=%(refname)", "refs/backup/");
        Assert.Equal(string.Empty, remoteRefs);
    }

    [Fact]
    public async Task RetentionService_CleansTrashBackupRefsAndAuditRows_ThenWritesAudit()
    {
        var config = TestAudit.NewConfig(_workspace.Root) with
        {
            BackupRetentionDays = 30,
            AuditRetentionDays = 90
        };
        var repo = CreateRepoWithCommits(out var first, out var second);
        CreateBackupRef(repo, DaysAgo(31), "IMPL", first);
        CreateBackupRef(repo, DaysAgo(1), "IMPL", second);

        var oldTrash = Path.Combine(_workspace.Root, BatchOperationService.TrashDirectoryName, DaysAgo(31));
        var newTrash = Path.Combine(_workspace.Root, BatchOperationService.TrashDirectoryName, DaysAgo(1));
        Directory.CreateDirectory(oldTrash);
        Directory.CreateDirectory(newTrash);
        File.WriteAllText(Path.Combine(oldTrash, "old.txt"), "old");
        File.WriteAllText(Path.Combine(newTrash, "new.txt"), "new");

        var audit = new OperationAuditService(config);
        audit.Database!.Insert(AuditEntry(DaysAgoUtc(91), "old-db"));
        audit.Record(action: "PUSH", componentId: "CF100A_Demo", repoType: "IMPL", state: "AHEAD",
            preHead: null, postHead: null, ok: true, errorCode: null, durationMs: 1, requestId: "recent-db");
        var oldLine = JsonSerializer.Serialize(new
        {
            ts = DaysAgoUtc(91),
            action = "PUSH",
            componentId = "CF100A_Demo",
            repoType = "IMPL",
            state = "AHEAD",
            preHead = (string?)null,
            postHead = (string?)null,
            ok = true,
            errorCode = (string?)null,
            durationMs = 1,
            actor = "tester",
            requestId = "old-jsonl",
            result = "OK"
        });
        File.AppendAllText(audit.FilePath, oldLine + Environment.NewLine);

        var retention = new RetentionService(config, audit, new BackupRefService(config));
        var summary = await retention.RunAsync(writeAudit: false);

        Assert.True(summary.WorkspaceAvailable);
        Assert.Equal(1, summary.TrashRemoved);
        Assert.Equal(1, summary.BackupRefsRemoved);
        Assert.Equal(1, summary.AuditRowsRemoved);
        Assert.Equal(1, summary.AuditJsonlRowsRemoved);
        Assert.False(Directory.Exists(oldTrash));
        Assert.True(Directory.Exists(newTrash));
        Assert.Equal(1, audit.Database!.Count());

        // 命令路径写审计：action=CLEANUP_RETENTION 恰 1 行（启动路径 writeAudit=false 不写）。
        await retention.RunAsync(writeAudit: true);
        var cleanupPage = audit.GetOperationLog(new GetOperationLogParams(Action: "CLEANUP_RETENTION"));
        Assert.True(cleanupPage.Ok, cleanupPage.Error?.Code);
        Assert.Single(Assert.IsType<OperationLogPageDto>(cleanupPage.Data).Items);
    }

    [Fact]
    public async Task RetentionService_WithoutWorkspace_OnlyCleansAuditAndReportsUnavailable()
    {
        var config = TestAudit.NewConfig();
        var audit = new OperationAuditService(config);
        audit.Database!.Insert(AuditEntry(DaysAgoUtc(100), "old"));
        var retention = new RetentionService(config, audit, new BackupRefService(config));

        var summary = await retention.RunAsync(writeAudit: false);

        Assert.False(summary.WorkspaceAvailable);
        Assert.Equal(0, summary.TrashRemoved);
        Assert.Equal(0, summary.BackupRefsRemoved);
        Assert.Equal(1, summary.AuditRowsRemoved);
    }

    private string CreateRepoWithCommits(out string first, out string second)
    {
        var repo = _workspace.Dir("CF100A_Demo_Impl");
        TestGit.InitRepo(repo);
        _workspace.CreateFile("CF100A_Demo_Impl/README.md", "v1");
        TestGit.CommitAll(repo, "first");
        first = TestGit.Capture(repo, "rev-parse", "HEAD");
        _workspace.CreateFile("CF100A_Demo_Impl/README.md", "v2");
        TestGit.CommitAll(repo, "second");
        second = TestGit.Capture(repo, "rev-parse", "HEAD");
        return repo;
    }

    private static void CreateBackupRef(string repo, string stamp, string repoType, string sha) =>
        TestGit.Run(repo, "update-ref", $"refs/backup/sync/{stamp}/{repoType}", sha);

    private static string DaysAgo(int days) =>
        DateTime.UtcNow.AddDays(-days).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);

    private static string DaysAgoUtc(int days) =>
        DateTime.UtcNow.AddDays(-days).ToString("O", CultureInfo.InvariantCulture);

    private static OperationLogEntry AuditEntry(string ts, string requestId) => new(
        Ts: ts,
        Actor: "tester",
        Action: "COMMIT",
        ComponentId: "CF100A_Demo",
        RepoType: "IMPL",
        TargetJson: null,
        BeforeJson: null,
        AfterJson: null,
        Result: "OK",
        ErrorCode: null,
        RequestId: requestId,
        JobId: null,
        JobItemId: null,
        BackupRef: null);

    public void Dispose() => _workspace.Dispose();
}
