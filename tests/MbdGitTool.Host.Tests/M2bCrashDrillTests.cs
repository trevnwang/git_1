using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using MbdGitTool.Host.Services;
using Xunit;

namespace MbdGitTool.Host.Tests;

public sealed class M2bCrashDrillTests
{
    [Fact]
    public async Task RealProcessCrashAfterWrite_ReconcileMarksUnknownApplied()
    {
        var repoRoot = FindRepoRoot();
        var workerDll = Path.Combine(repoRoot, "tests", "MbdGitTool.CrashDrillWorker", "bin", "Debug", "net10.0-windows", "MbdGitTool.CrashDrillWorker.dll");
        Assert.True(File.Exists(workerDll), $"worker 不存在：{workerDll}");

        using var workspace = new TempWorkspace();
        var repo = Path.Combine(workspace.Root, "repo");
        Directory.CreateDirectory(repo);
        TestGit.Run(repo, "init", "-q", "--initial-branch=main");
        TestGit.ConfigureIdentity(repo);
        File.WriteAllText(Path.Combine(repo, "README.md"), "base");
        TestGit.Run(repo, "add", "README.md");
        TestGit.Run(repo, "commit", "-q", "-m", "base");
        var preHead = TestGit.Capture(repo, "rev-parse", "HEAD");

        var journalDir = Path.Combine(workspace.Root, "journal");
        var marker = Path.Combine(workspace.Root, "written.marker");
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = workspace.Root,
        };
        start.ArgumentList.Add(workerDll);
        start.ArgumentList.Add(journalDir);
        start.ArgumentList.Add(repo);
        start.ArgumentList.Add(preHead);
        start.ArgumentList.Add(marker);
        using var process = Process.Start(start)!;
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (!File.Exists(marker) && DateTime.UtcNow < deadline && !process.HasExited)
        {
            await Task.Delay(200);
        }
        Assert.True(File.Exists(marker), "worker 未完成 git 写");
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
        }
        using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
        {
            await process.WaitForExitAsync(cts.Token);
        }
        Assert.NotEqual(preHead, TestGit.Capture(repo, "rev-parse", "HEAD"));

        var journal = new OperationJournal(journalDir);
        Assert.Equal(1, journal.CountUnresolved());
        var config = TestAudit.NewConfig();
        var audit = new OperationAuditService(config);
        var report = new JournalReconciler(journal, audit).Reconcile();
        Assert.Equal(1, report.Total);
        Assert.Equal(1, report.UnknownApplied);
        Assert.Equal(0, report.NotApplied);
        Assert.Equal(JournalReconciler.UnknownApplied, report.Items[0].Status);
        Assert.Equal(0, journal.CountUnresolved());

        // 真实写入后的 HEAD 不被 reconcile 修改；审计写 UNKNOWN_APPLIED 不猜成功。
        var after = TestGit.Capture(repo, "rev-parse", "HEAD");
        Assert.NotEqual(preHead, after);
        var auditLine = Directory.EnumerateFiles(config.AuditRoot!, "*.jsonl", SearchOption.AllDirectories)
            .SelectMany(File.ReadAllLines)
            .Last(line => line.Contains("\"action\":\"RECONCILE\"", StringComparison.Ordinal));
        using var doc = JsonDocument.Parse(auditLine);
        Assert.Equal(JournalReconciler.UnknownApplied, doc.RootElement.GetProperty("result").GetString());
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "MbdGitTool.sln"))) return current.FullName;
            current = current.Parent;
        }
        throw new InvalidOperationException("找不到仓库根（MbdGitTool.sln）");
    }
}
