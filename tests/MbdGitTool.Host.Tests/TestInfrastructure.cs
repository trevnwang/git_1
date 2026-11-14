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
    string? Branch = "main",
    string? HeadSha = null);

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
        Capabilities: new CapabilitiesDto(true, true, true, true, false));

    public static SnapshotIndex Index(string workspaceRoot, params ComponentSpec[] components) =>
        SnapshotIndex.Build(Snapshot(workspaceRoot, components));

    public static RepoDto ToRepo(string componentId, RepoSpec spec) => new(
        RepoType: spec.RepoType,
        State: spec.State,
        SubType: null,
        Branch: spec.Branch,
        HeadSha: spec.HeadSha ?? new string('a', 40),
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

/// <summary>真实 git 夹具命令（本地只读/写测试用）。</summary>
internal static class TestGit
{
    public static void InitRepo(string path)
    {
        Directory.CreateDirectory(path);
        Run(path, "init", "-q");
        ConfigureIdentity(path);
    }

    /// <summary>本地提交身份/避免 GPG 交互（写操作测试必需）。</summary>
    public static void ConfigureIdentity(string path)
    {
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

    /// <summary>执行 git 并返回 stdout（trim；失败抛异常）。</summary>
    public static string Capture(string workingDirectory, params string[] arguments)
    {
        var startInfo = BuildStartInfo(workingDirectory, arguments);
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

        return stdout.Trim();
    }

    public static void Run(string workingDirectory, params string[] arguments)
    {
        var startInfo = BuildStartInfo(workingDirectory, arguments);

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

    /// <summary>执行 git 并写入 stdin（如 <c>update-ref --stdin</c> 批量造 ref）。</summary>
    public static void RunWithInput(string workingDirectory, string input, params string[] arguments)
    {
        var startInfo = BuildStartInfo(workingDirectory, arguments);
        startInfo.RedirectStandardInput = true;

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("无法启动 git");
        process.StandardInput.Write(input);
        process.StandardInput.Close();
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"git {string.Join(' ', arguments)} 失败（exit={process.ExitCode}）：{stdout}{stderr}");
        }
    }

    private static ProcessStartInfo BuildStartInfo(string workingDirectory, string[] arguments)
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
        return startInfo;
    }
}

/// <summary>
/// 本地 bare remote + 两个 clone 夹具（无网络/无 Gitea）：
/// <c>ws/remote/CF100A_Demo_Impl.git</c>（bare）、<c>ws/cloneA</c>（写入方）、<c>ws/CF100A_Demo_Impl</c>（读取方，可被工作区扫描识别）。
/// </summary>
internal sealed class GitRemoteFixture : IDisposable
{
    private readonly TempWorkspace _workspace;

    private GitRemoteFixture(TempWorkspace workspace, string barePath, string cloneA, string cloneB)
    {
        _workspace = workspace;
        BarePath = barePath;
        CloneA = cloneA;
        CloneB = cloneB;
    }

    public string Root => _workspace.Root;

    public string BarePath { get; }

    public string CloneA { get; }

    public string CloneB { get; }

    public static GitRemoteFixture Create(string repoDirectoryName = "CF100A_Demo_Impl")
    {
        var workspace = new TempWorkspace();
        workspace.Dir("remote");
        workspace.Dir("cloneA");

        var barePath = workspace.PathFor(Path.Combine("remote", repoDirectoryName + ".git"));
        TestGit.Run(workspace.Root, "init", "--bare", "-q", "--initial-branch=main", barePath);

        var cloneA = workspace.PathFor("cloneA");
        TestGit.Run(workspace.Root, "clone", "-q", barePath, cloneA);
        TestGit.ConfigureIdentity(cloneA);
        System.IO.File.WriteAllText(Path.Combine(cloneA, "README.md"), "init");
        TestGit.CommitAll(cloneA, "init");
        TestGit.Run(cloneA, "push", "-q", "-u", "origin", "main");

        var cloneB = workspace.Dir(repoDirectoryName);
        TestGit.Run(workspace.Root, "clone", "-q", barePath, cloneB);
        TestGit.ConfigureIdentity(cloneB);

        return new GitRemoteFixture(workspace, barePath, cloneA, cloneB);
    }

    /// <summary>cloneA 写入并 push（推进 origin/main）。</summary>
    public void CommitAndPushFromA(string message)
    {
        System.IO.File.WriteAllText(Path.Combine(CloneA, message.Replace(' ', '_') + ".txt"), message);
        TestGit.CommitAll(CloneA, message);
        TestGit.Run(CloneA, "push", "-q", "origin", "main");
    }

    public static string Head(string path) => TestGit.Capture(path, "rev-parse", "HEAD");

    public static string Branch(string path) => TestGit.Capture(path, "rev-parse", "--abbrev-ref", "HEAD");

    public void Dispose() => _workspace.Dispose();
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

/// <summary>
/// 隔离的审计-lite 目录（<c>%TEMP%\MbdGitToolTests\audit\&lt;guid&gt;</c>）：
/// 避免 Host 单测把 JSONL 写进真实 <c>%LOCALAPPDATA%\MbdGitTool\audit</c>。
/// </summary>
internal static class TestAudit
{
    public static string NewRoot() =>
        Path.Combine(Path.GetTempPath(), "MbdGitToolTests", "audit", Guid.NewGuid().ToString("N"));

    /// <summary>带临时 <see cref="AppConfig.AuditRoot"/> 的配置（供 HostCommandRouter/SnapshotService 共用）。</summary>
    public static AppConfig NewConfig(string? workspaceRoot = null, string? giteaToken = null) => new()
    {
        WorkspaceRoot = workspaceRoot,
        GiteaToken = giteaToken,
        AuditRoot = NewRoot()
    };
}
