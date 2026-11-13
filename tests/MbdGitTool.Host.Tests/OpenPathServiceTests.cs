using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MbdGitTool.Core.Contracts;
using MbdGitTool.Core.Domain;
using MbdGitTool.Core.Security;
using MbdGitTool.Host.Services;

namespace MbdGitTool.Host.Tests;

public sealed class OpenPathServiceTests : IDisposable
{
    private readonly TempWorkspace _ws = new();
    private readonly RecordingLauncher _launcher = new();
    private readonly PathPolicy _paths;

    public OpenPathServiceTests() => _paths = new PathPolicy(_ws.Root);

    public void Dispose() => _ws.Dispose();

    [Fact]
    public async Task Open_SafeTextFile_UsesShellExecuteOnce()
    {
        _ws.CreateFile("docs/safe.md", "text");
        var service = CreateService();

        var result = await service.OpenAsync(
            new OpenPathParams { Path = "docs/safe.md", Mode = "open" },
            CancellationToken.None);

        Assert.True(result.Ok);
        var opened = Assert.IsType<OpenPathResultDto>(result.Data);
        Assert.True(opened.Opened);
        Assert.Equal("open", opened.Mode);
        Assert.Equal("docs/safe.md", opened.Path);
        Assert.Equal("SAFE_TEXT", opened.ExtensionClass);
        var startInfo = Assert.Single(_launcher.Calls);
        Assert.Equal(_ws.PathFor("docs/safe.md"), startInfo.FileName);
        Assert.True(startInfo.UseShellExecute);
    }

    [Fact]
    public async Task Open_DangerousExtensionWithoutConfirm_ReturnsConfirmRequiredAndDoesNotLaunch()
    {
        _ws.CreateFile("tools/build.bat", "echo");
        var service = CreateService();

        var result = await service.OpenAsync(
            new OpenPathParams { Path = "tools/build.bat", Mode = "open" },
            CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Equal(BridgeErrorCodes.ConfirmRequired, result.Error!.Code);
        Assert.Empty(_launcher.Calls);
        Assert.Equal("DANGEROUS", result.Error.Details!["extensionClass"]);
    }

    [Fact]
    public async Task Open_DangerousExtensionWithConfirm_Launches()
    {
        _ws.CreateFile("tools/build.ps1", "echo");
        var service = CreateService();

        var result = await service.OpenAsync(
            new OpenPathParams { Path = "tools/build.ps1", Mode = "open", Confirm = true },
            CancellationToken.None);

        Assert.True(result.Ok);
        Assert.Single(_launcher.Calls);
    }

    [Fact]
    public async Task Open_UnknownExtension_RequiresConfirm()
    {
        _ws.CreateFile("data/model.sldprt", "blob");
        var service = CreateService();

        var result = await service.OpenAsync(
            new OpenPathParams { Path = "data/model.sldprt", Mode = "open" },
            CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Equal(BridgeErrorCodes.ConfirmRequired, result.Error!.Code);
        Assert.Empty(_launcher.Calls);
    }

    [Fact]
    public async Task Open_DirectoryReveal_UsesExplorer()
    {
        _ws.Dir("folder");
        var service = CreateService();

        var result = await service.OpenAsync(
            new OpenPathParams { Path = "folder", Mode = "reveal" },
            CancellationToken.None);

        Assert.True(result.Ok);
        var startInfo = Assert.Single(_launcher.Calls);
        Assert.Equal("explorer.exe", startInfo.FileName);
        Assert.True(startInfo.UseShellExecute);
        Assert.Equal("DIRECTORY", Assert.IsType<OpenPathResultDto>(result.Data).ExtensionClass);
    }

    [Fact]
    public async Task Open_FileReveal_SelectsInExplorer()
    {
        _ws.CreateFile("docs/a.md", "x");
        var service = CreateService();

        var result = await service.OpenAsync(
            new OpenPathParams { Path = "docs/a.md", Mode = "reveal" },
            CancellationToken.None);

        Assert.True(result.Ok);
        var startInfo = Assert.Single(_launcher.Calls);
        Assert.Equal("explorer.exe", startInfo.FileName);
        Assert.Contains("/select,", startInfo.ArgumentList.ToArray()[0], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("../escape.md")]
    [InlineData("C:\\Windows\\notepad.exe")]
    [InlineData("\\\\server\\share\\x.txt")]
    [InlineData("/etc/passwd")]
    [InlineData(".git/config")]
    public async Task Open_OutsideRootOrGitInternal_Rejected(string path)
    {
        _ws.CreateFile("docs/a.md", "x");
        var service = CreateService();

        var result = await service.OpenAsync(
            new OpenPathParams { Path = path, Mode = "open", Confirm = true },
            CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Empty(_launcher.Calls);
        Assert.Contains(
            result.Error!.Code,
            new[]
            {
                BridgeErrorCodes.PathInvalid,
                BridgeErrorCodes.PathOutsideRoot,
                BridgeErrorCodes.GitInternal
            });
    }

    [Fact]
    public async Task Open_NonexistentPath_ReturnsPathInvalid()
    {
        var service = CreateService();

        var result = await service.OpenAsync(
            new OpenPathParams { Path = "docs/nope.md", Mode = "open" },
            CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Equal(BridgeErrorCodes.PathInvalid, result.Error!.Code);
        Assert.Empty(_launcher.Calls);
    }

    [Fact]
    public async Task Open_LauncherFailure_ReturnsLaunchFailed()
    {
        _ws.CreateFile("docs/a.md", "x");
        _launcher.Succeed = false;
        var service = CreateService();

        var result = await service.OpenAsync(
            new OpenPathParams { Path = "docs/a.md", Mode = "open" },
            CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Equal(BridgeErrorCodes.LaunchFailed, result.Error!.Code);
    }

    [Fact]
    public async Task Open_WithRepoScope_EnforcesRepoContainment()
    {
        var impl = _ws.Dir("CF100A_Demo_Impl");
        _ws.CreateFile("CF100A_Demo_Impl/README.md", "x");
        _ws.CreateFile("outside.txt", "x");
        var service = CreateService(impl);

        var crossRepo = await service.OpenAsync(
            new OpenPathParams
            {
                ComponentId = "CF100A_Demo",
                RepoType = "IMPL",
                Path = "../outside.txt",
                Mode = "open"
            },
            CancellationToken.None);
        Assert.False(crossRepo.Ok);
        Assert.Empty(_launcher.Calls);

        var inside = await service.OpenAsync(
            new OpenPathParams
            {
                ComponentId = "CF100A_Demo",
                RepoType = "IMPL",
                Path = "README.md",
                Mode = "open"
            },
            CancellationToken.None);
        Assert.True(inside.Ok);
        Assert.Single(_launcher.Calls);
        Assert.Equal("README.md", Assert.IsType<OpenPathResultDto>(inside.Data).Path);
    }

    [Fact]
    public async Task Open_SubmodulePath_Rejected()
    {
        var impl = _ws.Dir("CF100A_Demo_Impl");
        _ws.CreateFile("CF100A_Demo_Impl/.gitmodules", "[submodule \"third\"]\n\tpath = third\n");
        _ws.CreateFile("CF100A_Demo_Impl/third/file.txt", "x");
        var service = CreateService(impl);

        var result = await service.OpenAsync(
            new OpenPathParams
            {
                ComponentId = "CF100A_Demo",
                RepoType = "IMPL",
                Path = "third/file.txt",
                Mode = "open"
            },
            CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Equal(BridgeErrorCodes.Submodule, result.Error!.Code);
        Assert.Empty(_launcher.Calls);
    }

    [Fact]
    public async Task Open_AncestorReparsePoint_Rejected()
    {
        _ws.Dir("real-target");
        _ws.CreateFile("real-target/x.md", "x");
        var link = _ws.PathFor("link");
        if (!TryCreateJunction(link, _ws.PathFor("real-target")))
        {
            return;
        }

        var service = CreateService();
        var result = await service.OpenAsync(
            new OpenPathParams { Path = "link/x.md", Mode = "open" },
            CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Equal(BridgeErrorCodes.ReparsePoint, result.Error!.Code);
        Assert.Empty(_launcher.Calls);
    }

    [Fact]
    public void ShellProcessLauncher_DryRun_DoesNotLaunch()
    {
        var launcher = new ShellProcessLauncher();
        var previous = Environment.GetEnvironmentVariable("MBD_E2E_OPEN_DRY_RUN");
        Environment.SetEnvironmentVariable("MBD_E2E_OPEN_DRY_RUN", "1");
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _ws.PathFor("dry-run.txt"),
                UseShellExecute = true
            };

            Assert.True(launcher.TryStart(startInfo, out var error));
            Assert.Null(error);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MBD_E2E_OPEN_DRY_RUN", previous);
        }
    }

    private OpenPathService CreateService(string? repoRoot = null)
    {
        var repos = new[]
        {
            repoRoot is null
                ? new RepoSpec("IMPL", _ws.PathFor("CF100A_Demo_Impl"), IsLocal: false, State: "MISSING")
                : new RepoSpec("IMPL", repoRoot)
        };

        var index = TestData.Index(_ws.Root, new ComponentSpec("CF100A_Demo", repos));
        return new OpenPathService(_paths, () => index, new GitStatusIndex(), _launcher);
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
