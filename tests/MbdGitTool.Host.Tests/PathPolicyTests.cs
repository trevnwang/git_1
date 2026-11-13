using System;
using System.Diagnostics;
using System.IO;
using MbdGitTool.Core.Contracts;
using MbdGitTool.Core.Security;

namespace MbdGitTool.Host.Tests;

public sealed class PathPolicyTests : IDisposable
{
    private readonly TempWorkspace _ws = new();
    private readonly PathPolicy _policy;

    public PathPolicyTests() => _policy = new PathPolicy(_ws.Root);

    public void Dispose() => _ws.Dispose();

    [Theory]
    [InlineData("../outside", BridgeErrorCodes.PathInvalid)]
    [InlineData("a/../../x", BridgeErrorCodes.PathInvalid)]
    [InlineData("a/..", BridgeErrorCodes.PathInvalid)]
    [InlineData("..", BridgeErrorCodes.PathInvalid)]
    [InlineData("a//b", BridgeErrorCodes.PathInvalid)]
    [InlineData("a/b/", BridgeErrorCodes.PathInvalid)]
    [InlineData("C:\\Windows", BridgeErrorCodes.PathOutsideRoot)]
    [InlineData("C:Windows", BridgeErrorCodes.PathInvalid)]
    [InlineData("\\\\server\\share", BridgeErrorCodes.PathOutsideRoot)]
    [InlineData("//server/share", BridgeErrorCodes.PathOutsideRoot)]
    [InlineData("/etc/passwd", BridgeErrorCodes.PathOutsideRoot)]
    [InlineData("a\u0000b", BridgeErrorCodes.PathInvalid)]
    [InlineData("file.txt:ads", BridgeErrorCodes.PathInvalid)]
    [InlineData("CON", BridgeErrorCodes.PathInvalid)]
    [InlineData("sub/NUL.txt", BridgeErrorCodes.PathInvalid)]
    [InlineData("COM1", BridgeErrorCodes.PathInvalid)]
    [InlineData("a<b", BridgeErrorCodes.PathInvalid)]
    [InlineData(".. ", BridgeErrorCodes.PathInvalid)]
    [InlineData("...", BridgeErrorCodes.PathInvalid)]
    [InlineData("a ", BridgeErrorCodes.PathInvalid)]
    [InlineData("a.", BridgeErrorCodes.PathInvalid)]
    [InlineData(".git/config", BridgeErrorCodes.GitInternal)]
    [InlineData("src/.git/HEAD", BridgeErrorCodes.GitInternal)]
    [InlineData(".trash/recover.txt", BridgeErrorCodes.PathInvalid)]
    public void TryResolveWorkspacePath_EscapeAndInvalid_Rejected(string relative, string expectedCode)
    {
        var ok = _policy.TryResolveWorkspacePath(relative, out var full, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
        Assert.Equal(expectedCode, error!.Code);
        Assert.Empty(full);
    }

    [Fact]
    public void TryResolveWorkspacePath_TooLong_Rejected()
    {
        var relative = new string('a', PathPolicy.MaxRelativePathLength + 1);

        var ok = _policy.TryResolveWorkspacePath(relative, out _, out var error);

        Assert.False(ok);
        Assert.Equal(BridgeErrorCodes.PathInvalid, error!.Code);
    }

    [Fact]
    public void TryResolveWorkspacePath_EmptyMeansRoot()
    {
        Assert.True(_policy.TryResolveWorkspacePath(null, out var nullPath, out var nullError));
        Assert.Equal(_policy.Root, nullPath);
        Assert.Null(nullError);

        Assert.True(_policy.TryResolveWorkspacePath(string.Empty, out var emptyPath, out _));
        Assert.Equal(_policy.Root, emptyPath);
    }

    [Theory]
    [InlineData("src/main.m")]
    [InlineData("src/控制 器/主 文件.m")]
    [InlineData("docs/readme.md")]
    [InlineData("SRC/Main.M")]
    public void TryResolveWorkspacePath_ValidRelative_ReturnsCanonicalInsideRoot(string relative)
    {
        var ok = _policy.TryResolveWorkspacePath(relative, out var full, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.StartsWith(
            _policy.Root + Path.DirectorySeparatorChar,
            full,
            StringComparison.OrdinalIgnoreCase);
        Assert.False(full.Contains("..", StringComparison.Ordinal));
    }

    [Fact]
    public void TryResolveRepoPath_RelativeInsideRepo_ReturnsCanonical()
    {
        var repo = _ws.Dir("CF100A_Demo_Impl");

        Assert.True(_policy.TryResolveRepoPath(repo, "src/main.m", out var full, out var error));
        Assert.Null(error);
        Assert.Equal(Path.Combine(repo, "src", "main.m"), full);
    }

    [Fact]
    public void TryResolveRepoPath_RepoOutsideWorkspace_Rejected()
    {
        using var other = new TempWorkspace();
        var outsideRepo = other.Dir("SomeRepo");

        var ok = _policy.TryResolveRepoPath(outsideRepo, "a.txt", out _, out var error);

        Assert.False(ok);
        Assert.Equal(BridgeErrorCodes.PathOutsideRoot, error!.Code);
    }

    [Fact]
    public void TryResolveRepoPath_EscapeFromRepo_Rejected()
    {
        var repo = _ws.Dir("CF100A_Demo_Impl");

        var ok = _policy.TryResolveRepoPath(repo, "../CF100A_Demo_Design/x", out _, out var error);

        Assert.False(ok);
        Assert.Equal(BridgeErrorCodes.PathInvalid, error!.Code);
    }

    [Fact]
    public void IsWithin_HandlesCaseAndSiblingPrefix()
    {
        var sub = _ws.Dir("Sub");

        Assert.True(_policy.IsWithin(_ws.Root, sub));
        Assert.True(_policy.IsWithin(_ws.Root, sub.ToUpperInvariant()));
        Assert.True(_policy.IsWithin(_ws.Root, _ws.Root));
        Assert.False(_policy.IsWithin(_ws.Root, _ws.Root + "Sibling"));
        Assert.False(_policy.IsWithin(_ws.Root, Path.Combine(Path.GetTempPath(), "elsewhere")));
    }

    [Fact]
    public void RelativeConversions_RoundTrip()
    {
        var file = _ws.CreateFile("src/控制 器/main.m");
        var repo = _ws.Dir("CF100A_Demo_Impl");

        Assert.Equal("src/控制 器/main.m", _policy.ToWorkspaceRelative(file));
        Assert.Equal(string.Empty, _policy.ToWorkspaceRelative(Path.Combine(Path.GetTempPath(), "nope")));

        var repoFile = _ws.CreateFile("CF100A_Demo_Impl/src/a.c");
        Assert.Equal("src/a.c", _policy.ToRepoRelative(repo, repoFile));
        Assert.Equal(string.Empty, _policy.ToRepoRelative(repo, file));
    }

    [Fact]
    public void CheckReparseChain_JunctionDetected()
    {
        var target = _ws.Dir("RealTarget");
        var link = _ws.PathFor("Link");
        if (!TryCreateJunction(link, target))
        {
            return; // 当前环境不允许创建 junction：跳过（策略另有 IsReparsePoint 属性判断）
        }

        var targetCheck = _policy.CheckReparseChain(link, includeTarget: true);
        Assert.True(targetCheck.HasReparse);

        var ancestorCheck = _policy.CheckReparseChain(link);
        Assert.False(ancestorCheck.HasReparse);

        var childCheck = _policy.CheckReparseChain(Path.Combine(link, "child.txt"));
        Assert.True(childCheck.HasReparse);
        Assert.Equal(link, childCheck.ReparsePath, ignoreCase: true);
    }

    private static bool TryCreateJunction(string linkPath, string targetPath)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            startInfo.ArgumentList.Add("/c");
            startInfo.ArgumentList.Add("mklink");
            startInfo.ArgumentList.Add("/J");
            startInfo.ArgumentList.Add(linkPath);
            startInfo.ArgumentList.Add(targetPath);

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return false;
            }

            process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 && Directory.Exists(linkPath);
        }
        catch
        {
            return false;
        }
    }
}
