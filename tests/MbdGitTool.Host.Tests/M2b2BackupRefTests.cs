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
        Assert.All(page.Refs, reference => Assert.Null(reference.Branch));
        Assert.All(page.Refs, reference => Assert.Equal(BackupRefFormats.Legacy, reference.Format));
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
    public async Task GetBackupRefs_NewFormat_ReturnsBranchAndFormat_AndEncodesSlashes()
    {
        var repo = CreateRepoWithCommits(out var first, out var second);
        var newStamp = DaysAgo(1);
        var legacyStamp = DaysAgo(2);
        CreateNewBackupRef(repo, newStamp, "main", "IMPL", first);
        CreateNewBackupRef(repo, newStamp, "feature/x", "IMPL", second);
        CreateBackupRef(repo, legacyStamp, "IMPL", second);

        var result = await new BackupRefService().GetBackupRefsAsync(repo, "IMPL", CancellationToken.None);

        Assert.True(result.Ok, result.Error?.Code);
        var page = Assert.IsType<BackupRefsDto>(result.Data);
        Assert.Equal(3, page.Refs.Count);

        var mainRef = Assert.Single(page.Refs, reference => reference.Branch == "main");
        Assert.Equal(BackupRefFormats.Branch, mainRef.Format);
        Assert.Equal($"refs/backup/sync/{newStamp}/main/IMPL", mainRef.Ref);
        Assert.Equal(first, mainRef.Sha);
        Assert.Equal("IMPL", mainRef.RepoType);

        var slashRef = Assert.Single(page.Refs, reference => reference.Branch == "feature/x");
        Assert.Equal(BackupRefFormats.Branch, slashRef.Format);
        Assert.Contains("feature%2Fx", slashRef.Ref, StringComparison.Ordinal);
        Assert.Equal(second, slashRef.Sha);

        var legacyRef = Assert.Single(page.Refs, reference => reference.Format == BackupRefFormats.Legacy);
        Assert.Null(legacyRef.Branch);
        Assert.Equal($"refs/backup/sync/{legacyStamp}/IMPL", legacyRef.Ref);
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
    public async Task Restore_NewFormatSameBranch_Succeeds_AndReportsBackupBranch()
    {
        var repo = CreateRepoWithCommits(out var first, out var second);
        var current = CurrentBranch(repo);
        var stamp = DaysAgo(1);
        var backupRef = CreateNewBackupRef(repo, stamp, current, "IMPL", first);

        var (result, error) = await new BackupRefService().RestoreAsync(
            repo, "IMPL", backupRef, CancellationToken.None);

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.True(result!.Restored);
        Assert.Equal(first, result.PostHead);
        Assert.Equal(second, result.PreHead);
        Assert.Equal(first, TestGit.Capture(repo, "rev-parse", "HEAD"));
        Assert.Contains("备份来源分支：" + current, result.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("旧备份无分支身份", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Restore_NewFormatDifferentBranch_RejectedWithZeroWrites()
    {
        var repo = CreateRepoWithCommits(out var first, out var second);
        var current = CurrentBranch(repo);
        var other = current + "-old";
        TestGit.Run(repo, "branch", other); // 真实存在但非当前分支
        var stamp = DaysAgo(1);
        var backupRef = CreateNewBackupRef(repo, stamp, other, "IMPL", first);
        var refsBefore = TestGit.Capture(repo, "for-each-ref", "--format=%(refname)", "refs/backup/");

        var (result, error) = await new BackupRefService().RestoreAsync(
            repo, "IMPL", backupRef, CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(BridgeErrorCodes.ActionNotAllowed, error!.Code);
        Assert.Equal(other, error.Details!["expected"] as string);
        Assert.Equal(current, error.Details!["current"] as string);
        Assert.Equal(other, error.Details!["branch"] as string);

        // 零写：HEAD/分支/工作区/备份 ref 全部不变（未建安全备份 ref、未 reset）。
        Assert.Equal(second, TestGit.Capture(repo, "rev-parse", "HEAD"));
        Assert.Equal(current, CurrentBranch(repo));
        Assert.Equal(refsBefore, TestGit.Capture(repo, "for-each-ref", "--format=%(refname)", "refs/backup/"));
        Assert.Equal(string.Empty, TestGit.Capture(repo, "status", "--porcelain"));
    }

    [Fact]
    public async Task Restore_NewFormatBranchWithSlash_RoundTripsEncodedSegment()
    {
        var repo = CreateRepoWithCommits(out var first, out _);
        TestGit.Run(repo, "checkout", "-q", "-b", "feature/x");
        var stamp = DaysAgo(1);
        var backupRef = CreateNewBackupRef(repo, stamp, "feature/x", "IMPL", first);
        Assert.Contains("feature%2Fx", backupRef, StringComparison.Ordinal);

        var (result, error) = await new BackupRefService().RestoreAsync(
            repo, "IMPL", backupRef, CancellationToken.None);

        Assert.Null(error);
        Assert.True(result!.Restored);
        Assert.Equal(first, result.PostHead);
        Assert.Equal("feature/x", CurrentBranch(repo));
        Assert.Contains("备份来源分支：feature/x", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Restore_LegacyFormat_AllowedButMessageFlagsMissingBranchIdentity()
    {
        var repo = CreateRepoWithCommits(out var first, out _);
        var current = CurrentBranch(repo);
        var stamp = DaysAgo(1);
        CreateBackupRef(repo, stamp, "IMPL", first);

        var (result, error) = await new BackupRefService().RestoreAsync(
            repo, "IMPL", $"refs/backup/sync/{stamp}/IMPL", CancellationToken.None);

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.True(result!.Restored);
        Assert.Equal(first, result.PostHead);
        Assert.Contains("旧备份无分支身份，请确认当前分支", result.Message, StringComparison.Ordinal);
        Assert.Contains(current, result.Message, StringComparison.Ordinal);
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
    public async Task SafetyRef_IsListedAsSafety_AndRestorable()
    {
        var repo = CreateRepoWithCommits(out var first, out var second);
        var safety = $"refs/backup/restore/20260101000000-abcdef12/IMPL";
        TestGit.Run(repo, "update-ref", safety, first);

        var list = await new BackupRefService().GetBackupRefsAsync(repo, "IMPL", CancellationToken.None);
        Assert.True(list.Ok, list.Error?.Code);
        var refs = Assert.IsType<BackupRefsDto>(list.Data).Refs;
        var safetyRef = Assert.Single(refs, item => item.Ref == safety);
        Assert.Equal(BackupRefKinds.Safety, safetyRef.Kind);
        Assert.Null(safetyRef.Branch);

        var (result, error) = await new BackupRefService().RestoreAsync(
            repo, "IMPL", safety, CancellationToken.None);
        Assert.Null(error);
        Assert.True(result!.Restored);
        Assert.Equal(first, TestGit.Capture(repo, "rev-parse", "HEAD"));
        Assert.NotEqual(second, result.PostHead);
    }

    [Fact]
    public async Task Restore_WritesJournalStartedAndDone()
    {
        var repo = CreateRepoWithCommits(out var first, out var second);
        var config = TestAudit.NewConfig(_workspace.Root);
        var journal = new OperationJournal(config);
        var stamp = DaysAgo(1);
        CreateBackupRef(repo, stamp, "IMPL", first);

        var (result, error) = await new BackupRefService(config, journal: journal).RestoreAsync(
            repo, "IMPL", $"refs/backup/sync/{stamp}/IMPL", CancellationToken.None);

        Assert.Null(error);
        Assert.True(result!.Restored);
        var lines = File.ReadAllLines(journal.FilePath);
        Assert.Contains(lines, line => line.Contains("\"action\":\"RESTORE_BACKUP\"", StringComparison.Ordinal)
            && line.Contains("\"status\":\"STARTED\"", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("\"action\":\"RESTORE_BACKUP\"", StringComparison.Ordinal)
            && line.Contains("\"status\":\"DONE\"", StringComparison.Ordinal));
        Assert.Equal(second, TestGit.Capture(repo, "rev-parse", result.SafetyRef!));
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

    [Theory]
    [InlineData("refs/backup/sync/20260101000000/IMPL", true, null, BackupRefFormats.Legacy)]
    [InlineData("refs/backup/sync/20260101000000/impl", true, null, BackupRefFormats.Legacy)]
    [InlineData("refs/backup/sync/20260101000000/main/IMPL", true, "main", BackupRefFormats.Branch)]
    [InlineData("refs/backup/sync/20260101000000/DETACHED/IMPL", true, "DETACHED", BackupRefFormats.Branch)]
    [InlineData("refs/backup/sync/20260101000000/feature%2Fx/IMPL", true, "feature/x", BackupRefFormats.Branch)]
    [InlineData("refs/backup/sync/20260101000000/release%2F1.0/IMPL", true, "release/1.0", BackupRefFormats.Branch)]
    [InlineData("refs/backup/sync/20260101000000//IMPL", false, null, null)] // 空 branch 段
    [InlineData("refs/backup/sync/20260101000000/../IMPL", false, null, null)] // 相对段
    [InlineData("refs/backup/sync/20260101000000/main/IMPL/extra", false, null, null)] // 多余段
    [InlineData("refs/backup/sync/20260101000000/main", false, null, null)] // 缺 TYPE
    [InlineData("refs/backup/sync/20260101000000/main/MAIN", false, null, null)] // 非四仓类型
    [InlineData("refs/backup/sync/2026010100000/main/IMPL", false, null, null)] // stamp 非 14 位
    [InlineData("refs/backup/sync/20260101000000/feature%2fx/IMPL", false, null, null)] // 非规范小写转义
    [InlineData("refs/backup/sync/20260101000000/ma%20in/IMPL", false, null, null)] // 分支含空白
    [InlineData("refs/backup/restore/20260101000000/main/IMPL", false, null, null)] // 非 sync 前缀
    public void TryParseBackupRef_NewAndLegacyFormats_ReturnBranchAndFormat(
        string value,
        bool expectedOk,
        string? expectedBranch,
        string? expectedFormat)
    {
        var ok = BackupRefService.TryParseBackupRef(
            value,
            out var refText,
            out var stamp,
            out var branch,
            out var repoType,
            out var format);

        Assert.Equal(expectedOk, ok);
        if (!expectedOk)
        {
            Assert.Equal(string.Empty, refText);
            Assert.Equal(string.Empty, stamp);
            Assert.Null(branch);
            Assert.Equal(string.Empty, repoType);
            Assert.Equal(BackupRefFormats.Legacy, format);
            return;
        }

        Assert.Equal(value, refText);
        Assert.Equal("20260101000000", stamp);
        Assert.Equal(expectedBranch, branch);
        Assert.Equal(expectedFormat, format);
        Assert.Equal("IMPL", repoType);
        Assert.Equal(expectedBranch is null, format == BackupRefFormats.Legacy);
    }

    [Theory]
    [InlineData("main", "main")]
    [InlineData("feature/x", "feature%2Fx")]
    [InlineData("release/1.0", "release%2F1.0")]
    [InlineData("DETACHED", "DETACHED")]
    [InlineData(null, BackupRefService.DetachedBranchSegment)]
    [InlineData("", BackupRefService.DetachedBranchSegment)]
    [InlineData("has space", BackupRefService.DetachedBranchSegment)]
    [InlineData("bad..name", BackupRefService.DetachedBranchSegment)]
    [InlineData("-leading", BackupRefService.DetachedBranchSegment)]
    [InlineData("a=b", "a%3Db")]
    public void EncodeBranchSegment_EncodesBranchOrFallsBackToDetached(string? branch, string expected) =>
        Assert.Equal(expected, BackupRefService.EncodeBranchSegment(branch));

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
    public async Task CleanupExpired_RemovesOldRefsInBothFormats_KeepsRecentNewFormat()
    {
        var config = TestAudit.NewConfig(_workspace.Root);
        var repo = CreateRepoWithCommits(out var first, out var second);
        var oldStamp = DaysAgo(31);
        var recentStamp = DaysAgo(1);
        CreateBackupRef(repo, oldStamp, "IMPL", first); // 旧格式超期
        CreateNewBackupRef(repo, oldStamp, "main", "IMPL", second); // 新格式超期
        CreateNewBackupRef(repo, recentStamp, "feature/x", "IMPL", second); // 新格式保留期内

        var removed = await new BackupRefService(config).CleanupExpiredAsync(30, CancellationToken.None);

        Assert.Equal(2, removed);
        var refs = TestGit.Capture(repo, "for-each-ref", "--format=%(refname)", "refs/backup/");
        Assert.DoesNotContain(oldStamp, refs, StringComparison.Ordinal);
        Assert.Contains(recentStamp, refs, StringComparison.Ordinal);

        // 保留期内新格式仍可解析并经 getBackupRefs 返回 branch/format（保留期不变）。
        var listed = await new BackupRefService().GetBackupRefsAsync(repo, "IMPL", CancellationToken.None);
        Assert.True(listed.Ok, listed.Error?.Code);
        var remaining = Assert.Single(Assert.IsType<BackupRefsDto>(listed.Data).Refs);
        Assert.Equal("feature/x", remaining.Branch);
        Assert.Equal(BackupRefFormats.Branch, remaining.Format);
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

    /// <summary>M2b-2b 新格式：<c>refs/backup/sync/&lt;stamp&gt;/&lt;branch-encoded&gt;/&lt;TYPE&gt;</c>。</summary>
    private static string CreateNewBackupRef(string repo, string stamp, string branch, string repoType, string sha)
    {
        var encoded = BackupRefService.EncodeBranchSegment(branch);
        var refName = $"refs/backup/sync/{stamp}/{encoded}/{repoType}";
        TestGit.Run(repo, "update-ref", refName, sha);
        return refName;
    }

    private static string CurrentBranch(string repo) =>
        TestGit.Capture(repo, "rev-parse", "--abbrev-ref", "HEAD");

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
