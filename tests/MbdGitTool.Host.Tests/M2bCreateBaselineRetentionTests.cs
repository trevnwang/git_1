using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MbdGitTool.Host.Services;
using Xunit;

namespace MbdGitTool.Host.Tests;

public sealed class M2bCreateBaselineRetentionTests
{
    [Fact]
    public async Task Retention_RemovesOldCreateBaselineRefs_KeepsFresh()
    {
        using var workspace = new TempWorkspace();
        var repo = Path.Combine(workspace.Root, "CF300_Ret_IMPL");
        Directory.CreateDirectory(repo);
        TestGit.Run(repo, "init", "-q", "--initial-branch=main");
        TestGit.ConfigureIdentity(repo);
        File.WriteAllText(Path.Combine(repo, "README.md"), "x");
        TestGit.Run(repo, "add", "README.md");
        TestGit.Run(repo, "commit", "-q", "-m", "base");
        var head = TestGit.Capture(repo, "rev-parse", "HEAD");
        var oldStamp = DateTime.UtcNow.AddDays(-40).ToString("yyyyMMddHHmmss");
        var newStamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var oldRef = $"refs/baseline/create/{oldStamp}/feature%2Fold/IMPL";
        var newRef = $"refs/baseline/create/{newStamp}/feature%2Fnew/IMPL";
        TestGit.Run(repo, "update-ref", oldRef, head);
        TestGit.Run(repo, "update-ref", newRef, head);

        var config = TestAudit.NewConfig(workspace.Root);
        var audit = new OperationAuditService(config);
        var retention = new RetentionService(config, audit, new BackupRefService(config));
        await retention.RunAsync(writeAudit: false, cancellationToken: CancellationToken.None);

        Assert.Equal(string.Empty, TestGit.Capture(repo, "for-each-ref", "--format=%(refname)", oldRef));
        Assert.Equal(newRef, TestGit.Capture(repo, "for-each-ref", "--format=%(refname)", newRef));
    }
}
