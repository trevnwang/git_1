using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MbdGitTool.Core.Contracts;
using MbdGitTool.Core.Domain;
using MbdGitTool.Host.Services;

namespace MbdGitTool.Host.Tests;

/// <summary>临时工作区夹具（每个测试独立目录，Dispose 递归删除）。</summary>
public sealed class TempWorkspace : IDisposable
{
    public TempWorkspace()
    {
        Root = Path.Combine(Path.GetTempPath(), "MbdGitToolTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public string PathFor(string relative) => Path.Combine(Root, relative.Replace('/', '\\'));

    public string Dir(params string[] parts)
    {
        var path = Path.Combine(new[] { Root }.Concat(parts).ToArray());
        Directory.CreateDirectory(path);
        return path;
    }

    public string CreateFile(string relative, string content = "x", DateTime? mtimeUtc = null)
    {
        var path = PathFor(relative);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        System.IO.File.WriteAllText(path, content);
        if (mtimeUtc is not null)
        {
            System.IO.File.SetLastWriteTimeUtc(path, mtimeUtc.Value);
        }

        return path;
    }

    public void Dispose()
    {
        try
        {
            var prefix = Path.Combine(Path.GetTempPath(), "MbdGitToolTests");
            if (Root.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
        catch
        {
            // 测试清理失败不影响断言。
        }
    }
}

internal sealed record RepoSpec(
    string RepoType,
    string? LocalPath = null,
    bool IsLocal = true,
    string State = "SYNCED",
    int Ahead = 0,
    int Behind = 0,
    int DirtyCount = 0,
    string? RepoName = null,
    string? Branch = "main");

internal sealed record ComponentSpec(
    string ComponentId,
    IReadOnlyList<RepoSpec> Repos,
    string State = "SYNCED",
    int SeverityRank = 0,
    bool MixedBranch = false);

/// <summary>构造只读快照/索引，避免测试依赖真实扫描（真实扫描另见 HostCommandRouter 集成与烟测）。</summary>
internal static class TestData
{
    public static SnapshotDto Snapshot(string workspaceRoot, params ComponentSpec[] components) => new(
        Source: "local",
        WorkspaceRoot: workspaceRoot,
        Error: null,
        GeneratedAt: DateTime.UtcNow.ToString("O"),
        Components: components.Select(ToComponent).ToList(),
        Counts: new CountsDto(components.Length, components.Sum(c => c.Repos.Count), 0),
        Freshness: new FreshnessDto(DateTime.UtcNow.ToString("O"), null, false, true),
        Capabilities: new CapabilitiesDto(true, true, true, false, false));

    public static SnapshotIndex Index(string workspaceRoot, params ComponentSpec[] components) =>
        SnapshotIndex.Build(Snapshot(workspaceRoot, components));

    public static RepoDto ToRepo(string componentId, RepoSpec spec) => new(
        RepoType: spec.RepoType,
        State: spec.State,
        SubType: null,
        Branch: spec.Branch,
        HeadSha: new string('a', 40),
        Ahead: spec.Ahead,
        Behind: spec.Behind,
        DirtyCount: spec.DirtyCount,
        UntrackedCount: 0,
        IsLocal: spec.IsLocal,
        LocalPath: spec.LocalPath,
        Flags: Array.Empty<string>(),
        RepoName: spec.RepoName ?? (spec.LocalPath is null ? $"{componentId}_{spec.RepoType}" : Path.GetFileName(spec.LocalPath)),
        CompareRef: "refs/remotes/origin/main",
        CalcAt: DateTime.UtcNow.ToString("O"),
        DataSource: "local",
        IsStale: false);

    private static ComponentDto ToComponent(ComponentSpec spec) => new(
        ComponentId: spec.ComponentId,
        AggregateState: spec.State,
        SeverityRank: spec.SeverityRank,
        Branch: "main",
        Tag: null,
        MixedBranch: spec.MixedBranch,
        Flags: Array.Empty<string>(),
        Repos: spec.Repos.Select(repo => ToRepo(spec.ComponentId, repo)).ToList());
}

/// <summary>真实 git 夹具命令（本地只读测试用）。</summary>
internal static class TestGit
{
    public static void InitRepo(string path)
    {
        Directory.CreateDirectory(path);
        Run(path, "init", "-q");
        Run(path, "config", "user.email", "test@example.com");
        Run(path, "config", "user.name", "Test");
        Run(path, "config", "commit.gpgsign", "false");
        Run(path, "config", "core.autocrlf", "false");
    }

    public static void CommitAll(string path, string message)
    {
        Run(path, "add", "-A");
        Run(path, "commit", "-q", "-m", message, "--no-gpg-sign");
    }

    public static void Run(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        startInfo.ArgumentList.Add("-C");
        startInfo.ArgumentList.Add(workingDirectory);
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        startInfo.Environment["GIT_AUTHOR_NAME"] = "Test";
        startInfo.Environment["GIT_AUTHOR_EMAIL"] = "test@example.com";
        startInfo.Environment["GIT_COMMITTER_NAME"] = "Test";
        startInfo.Environment["GIT_COMMITTER_EMAIL"] = "test@example.com";

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("无法启动 git");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"git {string.Join(' ', arguments)} 失败（exit={process.ExitCode}）：{stdout}{stderr}");
        }
    }
}

/// <summary>记录调用的启动器（测试绝不真正打开文件）。</summary>
internal sealed class RecordingLauncher : IProcessLauncher
{
    public List<ProcessStartInfo> Calls { get; } = new();

    public bool Succeed { get; set; } = true;

    public bool TryStart(ProcessStartInfo startInfo, out string? error)
    {
        Calls.Add(startInfo);
        error = Succeed ? null : "模拟启动失败";
        return Succeed;
    }
}
