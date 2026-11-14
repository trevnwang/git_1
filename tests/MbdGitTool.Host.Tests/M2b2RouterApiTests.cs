using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MbdGitTool.Core.Contracts;
using MbdGitTool.Host.Bridge;
using MbdGitTool.Host.Services;
using Xunit;

namespace MbdGitTool.Host.Tests;

/// <summary>
/// M2b-2 路由 API 端到端（真实本地 bare remote，无网络/Gitea）：
/// <c>getOperationLog</c> / <c>exportOperationLog</c> / <c>getBackupRefs</c> / <c>restoreBackupRef</c> /
/// <c>cleanupRetention</c>；含 RESTORE_BACKUP 审计、确认门禁、参数校验与 token 不泄漏。
/// </summary>
public sealed class M2b2RouterApiTests
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GetOperationLog_AfterRepoAction_ReturnsPersistedRowWithJsonProjection()
    {
        using var fixture = GitRemoteFixture.Create();
        var config = TestAudit.NewConfig(fixture.Root);
        using var snapshots = new SnapshotService(config);
        using var router = new HostCommandRouter(config, snapshots, new RecordingLauncher());
        await router.StartupRetention;
        Assert.True((await DispatchAsync(router, "getSnapshot", null)).Ok);

        File.WriteAllText(Path.Combine(fixture.CloneB, "audit-m2b2.txt"), "x");
        TestGit.Run(fixture.CloneB, "add", "audit-m2b2.txt");
        var commit = await DispatchAsync(router, "runRepoAction", new
        {
            componentId = "CF100A_Demo",
            repoType = "IMPL",
            action = "COMMIT",
            args = new { message = "m2b2 audit", confirm = true }
        });
        Assert.True(commit.Ok, commit.Error?.Code);

        var result = await DispatchAsync(router, "getOperationLog", new
        {
            componentId = "CF100A_Demo",
            action = "COMMIT",
            limit = 10
        });

        Assert.True(result.Ok, result.Error?.Code);
        var page = Assert.IsType<OperationLogPageDto>(result.Data);
        Assert.True(page.Total >= 1);
        var item = Assert.Single(page.Items, entry => entry.Action == "COMMIT");
        Assert.Equal("CF100A_Demo", item.ComponentId);
        Assert.Equal("IMPL", item.RepoType);
        Assert.Equal("OK", item.Result);
        Assert.False(string.IsNullOrWhiteSpace(item.RequestId));
        Assert.NotNull(item.Target);
        Assert.Equal("CF100A_Demo", item.Target!.Value.GetProperty("componentId").GetString());

        // from 未来时间 → 0 行（时间过滤生效）。
        var future = await DispatchAsync(router, "getOperationLog", new
        {
            from = DateTime.UtcNow.AddHours(1).ToString("O", CultureInfo.InvariantCulture)
        });
        Assert.True(future.Ok, future.Error?.Code);
        Assert.Equal(0, Assert.IsType<OperationLogPageDto>(future.Data).Total);
    }

    [Fact]
    public async Task ExportOperationLog_Csv_WritesToConfiguredExportsRoot_WithoutSecrets()
    {
        const string token = "ghp_m2b2_export_fake_token_0123456789";
        using var fixture = GitRemoteFixture.Create();
        var config = TestAudit.NewConfig(fixture.Root, token);
        using var snapshots = new SnapshotService(config);
        using var router = new HostCommandRouter(config, snapshots, new RecordingLauncher());
        await router.StartupRetention;
        Assert.True((await DispatchAsync(router, "getSnapshot", null)).Ok);

        File.WriteAllText(Path.Combine(fixture.CloneB, "export.txt"), "x");
        TestGit.Run(fixture.CloneB, "add", "export.txt");
        var commit = await DispatchAsync(router, "runRepoAction", new
        {
            componentId = "CF100A_Demo",
            repoType = "IMPL",
            action = "COMMIT",
            args = new { message = "m2b2 export", confirm = true }
        });
        Assert.True(commit.Ok, commit.Error?.Code);

        var exported = await DispatchAsync(router, "exportOperationLog", new { format = "csv" });

        Assert.True(exported.Ok, exported.Error?.Code);
        var data = Assert.IsType<OperationLogExportDto>(exported.Data);
        Assert.True(data.Count >= 1);
        Assert.True(File.Exists(data.Path));
        Assert.StartsWith(Path.GetFullPath(config.ExportsRoot!), data.Path, StringComparison.OrdinalIgnoreCase);
        var text = File.ReadAllText(data.Path);
        Assert.Contains("target_json", text, StringComparison.Ordinal);
        Assert.DoesNotContain(token, text, StringComparison.Ordinal);
        Assert.DoesNotContain("Authorization", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("output", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetBackupRefsAndRestore_AuditsRestoreBackup_AndRequiresConfirm()
    {
        using var fixture = GitRemoteFixture.Create();
        var config = TestAudit.NewConfig(fixture.Root);
        using var snapshots = new SnapshotService(config);
        using var router = new HostCommandRouter(config, snapshots, new RecordingLauncher());
        await router.StartupRetention;
        Assert.True((await DispatchAsync(router, "getSnapshot", null)).Ok);

        var backupTarget = GitRemoteFixture.Head(fixture.CloneB);
        File.WriteAllText(Path.Combine(fixture.CloneB, "restore-local.txt"), "local");
        TestGit.CommitAll(fixture.CloneB, "local ahead for restore");
        var preRestoreHead = GitRemoteFixture.Head(fixture.CloneB);
        var stamp = DateTime.UtcNow.AddDays(-1).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        var backupRef = $"refs/backup/sync/{stamp}/IMPL";
        TestGit.Run(fixture.CloneB, "update-ref", backupRef, backupTarget);
        Assert.True((await DispatchAsync(router, "getSnapshot", null)).Ok);

        var list = await DispatchAsync(router, "getBackupRefs", new
        {
            componentId = "CF100A_Demo",
            repoType = "IMPL"
        });
        Assert.True(list.Ok, list.Error?.Code);
        var refs = Assert.IsType<BackupRefsDto>(list.Data).Refs;
        var found = Assert.Single(refs, reference => reference.Ref == backupRef);
        Assert.Equal(backupTarget, found.Sha);
        Assert.Equal("IMPL", found.RepoType);
        Assert.Null(found.Branch); // 旧格式：无分支身份
        Assert.Equal(BackupRefFormats.Legacy, found.Format);

        var noConfirm = await DispatchAsync(router, "restoreBackupRef", new
        {
            componentId = "CF100A_Demo",
            repoType = "IMPL",
            @ref = backupRef
        });
        Assert.False(noConfirm.Ok);
        Assert.Equal(BridgeErrorCodes.ConfirmRequired, noConfirm.Error!.Code);
        Assert.Equal(preRestoreHead, GitRemoteFixture.Head(fixture.CloneB));

        var restored = await DispatchAsync(router, "restoreBackupRef", new
        {
            componentId = "CF100A_Demo",
            repoType = "IMPL",
            @ref = backupRef,
            confirm = true
        });
        Assert.True(restored.Ok, restored.Error?.Code);
        var restoredData = Assert.IsType<RestoreBackupRefResultDto>(restored.Data);
        Assert.True(restoredData.Restored);
        Assert.Equal(backupTarget, GitRemoteFixture.Head(fixture.CloneB));
        Assert.Equal(preRestoreHead, restoredData.PreHead);
        Assert.Equal(backupTarget, restoredData.PostHead);
        Assert.NotNull(restoredData.SafetyRef);
        Assert.Equal(preRestoreHead, TestGit.Capture(fixture.CloneB, "rev-parse", restoredData.SafetyRef!));
        Assert.Contains("旧备份无分支身份，请确认当前分支", restoredData.Message, StringComparison.Ordinal);

        var audit = await DispatchAsync(router, "getOperationLog", new
        {
            componentId = "CF100A_Demo",
            action = "RESTORE_BACKUP"
        });
        Assert.True(audit.Ok, audit.Error?.Code);
        var page = Assert.IsType<OperationLogPageDto>(audit.Data);
        var row = Assert.Single(page.Items);
        Assert.Equal("OK", row.Result);
        Assert.Equal(backupRef, row.BackupRef);
        Assert.Equal(preRestoreHead, row.Before!.Value.GetProperty("head").GetString());
        Assert.Equal(backupTarget, row.After!.Value.GetProperty("head").GetString());
    }

    [Fact]
    public async Task GetBackupRefsAndRestore_NewFormatBranch_ReturnsBranchAndBlocksCrossBranchRestore()
    {
        using var fixture = GitRemoteFixture.Create();
        var config = TestAudit.NewConfig(fixture.Root);
        using var snapshots = new SnapshotService(config);
        using var router = new HostCommandRouter(config, snapshots, new RecordingLauncher());
        await router.StartupRetention;
        Assert.True((await DispatchAsync(router, "getSnapshot", null)).Ok);

        var head = GitRemoteFixture.Head(fixture.CloneB);
        var current = GitRemoteFixture.Branch(fixture.CloneB);
        var stamp = DateTime.UtcNow.AddDays(-1).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        var sameBranchRef = $"refs/backup/sync/{stamp}/{Uri.EscapeDataString(current)}/IMPL";
        TestGit.Run(fixture.CloneB, "update-ref", sameBranchRef, head);
        var otherBranchRef = $"refs/backup/sync/{stamp}/other-branch/IMPL";
        TestGit.Run(fixture.CloneB, "update-ref", otherBranchRef, head);

        var list = await DispatchAsync(router, "getBackupRefs", new
        {
            componentId = "CF100A_Demo",
            repoType = "IMPL"
        });
        Assert.True(list.Ok, list.Error?.Code);
        var refs = Assert.IsType<BackupRefsDto>(list.Data).Refs;
        var sameBranch = Assert.Single(refs, reference => reference.Ref == sameBranchRef);
        Assert.Equal(current, sameBranch.Branch);
        Assert.Equal(BackupRefFormats.Branch, sameBranch.Format);
        var otherBranch = Assert.Single(refs, reference => reference.Ref == otherBranchRef);
        Assert.Equal("other-branch", otherBranch.Branch);

        // 跨分支恢复：E_ACTION_NOT_ALLOWED 且零写（HEAD/备份 ref 不变、无私自创建安全备份 ref）。
        var refsBefore = TestGit.Capture(fixture.CloneB, "for-each-ref", "--format=%(refname)", "refs/backup/");
        var blocked = await DispatchAsync(router, "restoreBackupRef", new
        {
            componentId = "CF100A_Demo",
            repoType = "IMPL",
            @ref = otherBranchRef,
            confirm = true
        });
        Assert.False(blocked.Ok);
        Assert.Equal(BridgeErrorCodes.ActionNotAllowed, blocked.Error!.Code);
        Assert.Equal("other-branch", blocked.Error.Details!["expected"] as string);
        Assert.Equal(current, blocked.Error.Details!["current"] as string);
        Assert.Equal(head, GitRemoteFixture.Head(fixture.CloneB));
        Assert.Equal(refsBefore, TestGit.Capture(fixture.CloneB, "for-each-ref", "--format=%(refname)", "refs/backup/"));
        Assert.Equal(string.Empty, TestGit.Capture(fixture.CloneB, "status", "--porcelain"));
    }

    [Fact]
    public async Task RestoreBackupRef_InvalidRefAndBadArgs_AreRejected()
    {
        using var fixture = GitRemoteFixture.Create();
        var config = TestAudit.NewConfig(fixture.Root);
        using var snapshots = new SnapshotService(config);
        using var router = new HostCommandRouter(config, snapshots, new RecordingLauncher());
        await router.StartupRetention;
        Assert.True((await DispatchAsync(router, "getSnapshot", null)).Ok);

        var nonBackup = await DispatchAsync(router, "restoreBackupRef", new
        {
            componentId = "CF100A_Demo",
            repoType = "IMPL",
            @ref = "refs/heads/main",
            confirm = true
        });
        Assert.False(nonBackup.Ok);
        Assert.Equal(BridgeErrorCodes.InvalidArgument, nonBackup.Error!.Code);

        var missing = await DispatchAsync(router, "restoreBackupRef", new
        {
            componentId = "CF100A_Demo",
            repoType = "IMPL",
            @ref = "refs/backup/sync/20260101000000/IMPL",
            confirm = true
        });
        Assert.False(missing.Ok);
        Assert.Equal(BridgeErrorCodes.RefNotFound, missing.Error!.Code);

        var badRepoType = await DispatchAsync(router, "getBackupRefs", new
        {
            componentId = "CF100A_Demo",
            repoType = "MAIN"
        });
        Assert.False(badRepoType.Ok);
        Assert.Equal(BridgeErrorCodes.InvalidArgument, badRepoType.Error!.Code);

        var missingComponent = await DispatchAsync(router, "getBackupRefs", new { });
        Assert.False(missingComponent.Ok);
        Assert.Equal(BridgeErrorCodes.InvalidArgument, missingComponent.Error!.Code);
    }

    [Fact]
    public async Task CleanupRetention_Command_CleansLocalTrashBackupRefAndAuditRows()
    {
        using var fixture = GitRemoteFixture.Create();
        var config = TestAudit.NewConfig(fixture.Root);
        using var snapshots = new SnapshotService(config);
        using var router = new HostCommandRouter(config, snapshots, new RecordingLauncher());
        await router.StartupRetention; // 启动清理完成后再布置夹具，避免被启动任务先删掉

        var head = GitRemoteFixture.Head(fixture.CloneB);
        var oldStamp = DateTime.UtcNow.AddDays(-31).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        TestGit.Run(fixture.CloneB, "update-ref", $"refs/backup/sync/{oldStamp}/IMPL", head);
        var oldTrash = Path.Combine(fixture.Root, ".trash", oldStamp);
        Directory.CreateDirectory(oldTrash);
        File.WriteAllText(Path.Combine(oldTrash, "x.txt"), "x");
        new OperationAuditService(config).Database!.Insert(new OperationLogEntry(
            Ts: DateTime.UtcNow.AddDays(-91).ToString("O", CultureInfo.InvariantCulture),
            Actor: "tester",
            Action: "COMMIT",
            ComponentId: "CF100A_Demo",
            RepoType: "IMPL",
            TargetJson: null,
            BeforeJson: null,
            AfterJson: null,
            Result: "OK",
            ErrorCode: null,
            RequestId: "old-row",
            JobId: null,
            JobItemId: null,
            BackupRef: null));

        var result = await DispatchAsync(router, "cleanupRetention", null);

        Assert.True(result.Ok, result.Error?.Code);
        var summary = Assert.IsType<RetentionCleanupDto>(result.Data);
        Assert.True(summary.WorkspaceAvailable);
        Assert.Equal(1, summary.TrashRemoved);
        Assert.Equal(1, summary.BackupRefsRemoved);
        Assert.Equal(1, summary.AuditRowsRemoved);
        Assert.False(Directory.Exists(oldTrash));
        Assert.DoesNotContain(
            oldStamp,
            TestGit.Capture(fixture.CloneB, "for-each-ref", "--format=%(refname)", "refs/backup/sync/"),
            StringComparison.Ordinal);

        // 命令清理写审计（action=CLEANUP_RETENTION）。
        var audit = await DispatchAsync(router, "getOperationLog", new { action = "CLEANUP_RETENTION" });
        Assert.True(audit.Ok, audit.Error?.Code);
        Assert.Single(Assert.IsType<OperationLogPageDto>(audit.Data).Items);
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
}
