using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MbdGitTool.Core.Contracts;
using MbdGitTool.Core.Domain;
using MbdGitTool.Core.Files;
using MbdGitTool.Core.Security;
using MbdGitTool.Host.Services;

namespace MbdGitTool.Host.Tests;

public sealed class RepoTreeServiceTests : IDisposable
{
    private readonly TempWorkspace _ws = new();

    public void Dispose() => _ws.Dispose();

    [Fact]
    public async Task GetTree_Root_ReturnsSortedEntriesAndSnapshotFacts()
    {
        var impl = _ws.Dir("CF100A_Demo_Impl");
        _ws.CreateFile("CF100A_Demo_Impl/src/main.m", "x");
        _ws.CreateFile("CF100A_Demo_Impl/README.md", "readme");
        _ws.CreateFile("CF100A_Demo_Impl/.gitignore", "bin/");
        var service = CreateService(impl);

        var tree = await GetOkAsync(service, new GetRepoTreeParams
        {
            ComponentId = "CF100A_Demo",
            RepoType = "IMPL"
        });

        Assert.Equal("CF100A_Demo", tree.ComponentId);
        Assert.Equal("IMPL", tree.RepoType);
        Assert.Equal("CF100A_Demo_Impl", tree.RepoName);
        Assert.Equal(impl, tree.RootPath);
        Assert.Equal(string.Empty, tree.Path);
        Assert.Equal("SYNCED", tree.State);
        Assert.Equal(2000, tree.Limit);
        Assert.False(tree.Truncated);
        Assert.Equal(new[] { "src", ".gitignore", "README.md" }, tree.Entries.Select(entry => entry.Name));
        Assert.All(tree.Entries, entry => Assert.Equal("NONE", entry.GitStatus));
    }

    [Fact]
    public async Task GetTree_SubDirectory_ReturnsRelativePaths()
    {
        var impl = _ws.Dir("CF100A_Demo_Impl");
        _ws.CreateFile("CF100A_Demo_Impl/src/control/main.m", "x");
        var service = CreateService(impl);

        var tree = await GetOkAsync(service, new GetRepoTreeParams
        {
            ComponentId = "CF100A_Demo",
            RepoType = "IMPL",
            Path = "src"
        });

        Assert.Equal("src", tree.Path);
        var node = Assert.Single(tree.Entries);
        Assert.Equal("control", node.Name);
        Assert.Equal("src/control", node.Path);
        Assert.Equal("DIR", node.Kind);
    }

    [Fact]
    public async Task GetTree_Limit_TruncatesButReportsTotal()
    {
        var impl = _ws.Dir("CF100A_Demo_Impl");
        _ws.CreateFile("CF100A_Demo_Impl/a.txt");
        _ws.CreateFile("CF100A_Demo_Impl/b.txt");
        _ws.CreateFile("CF100A_Demo_Impl/c.txt");
        var service = CreateService(impl);

        var tree = await GetOkAsync(service, new GetRepoTreeParams
        {
            ComponentId = "CF100A_Demo",
            RepoType = "IMPL",
            Limit = 2
        });

        Assert.True(tree.Truncated);
        Assert.Equal(3, tree.TotalEntries);
        Assert.Equal(2, tree.Entries.Count);
        Assert.Equal(2, tree.Limit);
    }

    [Fact]
    public async Task GetTree_MissingRepo_ReturnsRepoNotLocal()
    {
        var service = CreateService(_ws.Dir("CF100A_Demo_Impl"), new RepoSpec(
            "MIL",
            _ws.PathFor("CF100A_Demo_MIL"),
            IsLocal: false,
            State: "MISSING"));

        var result = await service.GetTreeAsync(
            new GetRepoTreeParams { ComponentId = "CF100A_Demo", RepoType = "MIL" },
            CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Equal(BridgeErrorCodes.RepoNotLocal, result.Error!.Code);
    }

    [Fact]
    public async Task GetTree_UnknownRepo_ReturnsNotFound()
    {
        var service = CreateService(_ws.Dir("CF100A_Demo_Impl"));

        var result = await service.GetTreeAsync(
            new GetRepoTreeParams { ComponentId = "NOPE", RepoType = "IMPL" },
            CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Equal(BridgeErrorCodes.NotFound, result.Error!.Code);
    }

    [Fact]
    public async Task GetTree_MissingParameters_ReturnsInvalidArgument()
    {
        var service = CreateService(_ws.Dir("CF100A_Demo_Impl"));

        var result = await service.GetTreeAsync(new GetRepoTreeParams(), CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Equal(BridgeErrorCodes.InvalidArgument, result.Error!.Code);

        var badType = await service.GetTreeAsync(
            new GetRepoTreeParams { ComponentId = "CF100A_Demo", RepoType = "MAIN" },
            CancellationToken.None);
        Assert.False(badType.Ok);
        Assert.Equal(BridgeErrorCodes.InvalidArgument, badType.Error!.Code);
    }

    [Theory]
    [InlineData("../CF100A_Demo_Design")]
    [InlineData("a/../../x")]
    [InlineData("C:\\Windows")]
    [InlineData("\\\\server\\share")]
    [InlineData("/etc/passwd")]
    [InlineData(".git/config")]
    public async Task GetTree_PathEscape_Rejected(string path)
    {
        var service = CreateService(_ws.Dir("CF100A_Demo_Impl"));

        var result = await service.GetTreeAsync(
            new GetRepoTreeParams { ComponentId = "CF100A_Demo", RepoType = "IMPL", Path = path },
            CancellationToken.None);

        Assert.False(result.Ok);
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
    public async Task GetTree_RepoIdAndRepoPathAliases_Work()
    {
        var impl = _ws.Dir("CF100A_Demo_Impl");
        _ws.CreateFile("CF100A_Demo_Impl/README.md");
        var service = CreateService(impl);

        var byRepoId = await GetOkAsync(service, new GetRepoTreeParams { RepoId = "CF100A_Demo_IMPL" });
        Assert.Equal("IMPL", byRepoId.RepoType);

        var byRepoPath = await GetOkAsync(service, new GetRepoTreeParams { RepoPath = "CF100A_Demo_Impl" });
        Assert.Equal(impl, byRepoPath.RootPath);
    }

    [Theory]
    [InlineData("../CF100A_Demo_Impl")]
    [InlineData("C:\\Windows")]
    public async Task GetTree_RepoPathAliasEscape_Rejected(string repoPath)
    {
        var service = CreateService(_ws.Dir("CF100A_Demo_Impl"));

        var result = await service.GetTreeAsync(
            new GetRepoTreeParams { RepoPath = repoPath },
            CancellationToken.None);

        Assert.False(result.Ok);
    }

    [Fact]
    public async Task GetTree_GitStatus_AnnotatesModifiedAndUntrackedFiles()
    {
        var impl = _ws.Dir("CF100A_Demo_Impl");
        TestGit.InitRepo(impl);
        _ws.CreateFile("CF100A_Demo_Impl/README.md", "v1");
        TestGit.CommitAll(impl, "init");
        _ws.CreateFile("CF100A_Demo_Impl/README.md", "v2");
        _ws.CreateFile("CF100A_Demo_Impl/untracked.txt", "new");
        var service = CreateService(impl);

        var tree = await GetOkAsync(service, new GetRepoTreeParams
        {
            ComponentId = "CF100A_Demo",
            RepoType = "IMPL"
        });

        Assert.Equal(
            "MODIFIED",
            Assert.Single(tree.Entries, entry => entry.Name == "README.md").GitStatus);
        Assert.Equal(
            "UNTRACKED",
            Assert.Single(tree.Entries, entry => entry.Name == "untracked.txt").GitStatus);
    }

    [Fact]
    public async Task GetTree_IncludeGitStatusFalse_AllNone()
    {
        var impl = _ws.Dir("CF100A_Demo_Impl");
        TestGit.InitRepo(impl);
        _ws.CreateFile("CF100A_Demo_Impl/README.md", "v1");
        TestGit.CommitAll(impl, "init");
        _ws.CreateFile("CF100A_Demo_Impl/README.md", "v2");
        var service = CreateService(impl);

        var tree = await GetOkAsync(service, new GetRepoTreeParams
        {
            ComponentId = "CF100A_Demo",
            RepoType = "IMPL",
            IncludeGitStatus = false
        });

        Assert.All(tree.Entries, entry => Assert.Equal("NONE", entry.GitStatus));
    }

    [Fact]
    public async Task GetTree_SubmodulePath_ReturnsSubmoduleError()
    {
        var impl = _ws.Dir("CF100A_Demo_Impl");
        _ws.CreateFile("CF100A_Demo_Impl/.gitmodules", "[submodule \"third\"]\n\tpath = third\n");
        _ws.CreateFile("CF100A_Demo_Impl/third/file.txt", "x");
        var service = CreateService(impl);

        var root = await GetOkAsync(service, new GetRepoTreeParams
        {
            ComponentId = "CF100A_Demo",
            RepoType = "IMPL"
        });
        Assert.True(Assert.Single(root.Entries, entry => entry.Name == "third").Submodule);

        var result = await service.GetTreeAsync(
            new GetRepoTreeParams { ComponentId = "CF100A_Demo", RepoType = "IMPL", Path = "third" },
            CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Equal(BridgeErrorCodes.Submodule, result.Error!.Code);
    }

    [Fact]
    public async Task GetTree_ReparsePointExpansion_Rejected()
    {
        var impl = _ws.Dir("CF100A_Demo_Impl");
        var target = _ws.Dir("outside-target");
        var link = _ws.PathFor("CF100A_Demo_Impl/link");
        if (!TryCreateJunction(link, target))
        {
            return;
        }

        var service = CreateService(impl);
        var result = await service.GetTreeAsync(
            new GetRepoTreeParams { ComponentId = "CF100A_Demo", RepoType = "IMPL", Path = "link" },
            CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Equal(BridgeErrorCodes.ReparsePoint, result.Error!.Code);
    }

    private RepoTreeService CreateService(string repoRoot, params RepoSpec[] overrides)
    {
        var repos = new[]
        {
            new RepoSpec("DESIGN", _ws.PathFor("CF100A_Demo_Design"), IsLocal: false, State: "MISSING"),
            new RepoSpec("IMPL", repoRoot),
            new RepoSpec("MIL", _ws.PathFor("CF100A_Demo_MIL"), IsLocal: false, State: "MISSING"),
            new RepoSpec("SIL", _ws.PathFor("CF100A_Demo_SIL"), IsLocal: false, State: "MISSING")
        }.ToList();

        foreach (var over in overrides)
        {
            var index = repos.FindIndex(repo => repo.RepoType == over.RepoType);
            if (index >= 0)
            {
                repos[index] = over;
            }
        }

        var index2 = TestData.Index(_ws.Root, new ComponentSpec("CF100A_Demo", repos));
        var paths = new PathPolicy(_ws.Root);
        return new RepoTreeService(() => index2, paths, new FileTreeReader(), new GitStatusIndex());
    }

    private static async Task<RepoTreeDto> GetOkAsync(RepoTreeService service, GetRepoTreeParams parameters)
    {
        var result = await service.GetTreeAsync(parameters, CancellationToken.None);
        Assert.True(result.Ok, result.Error?.Code + ": " + result.Error?.Message);
        return Assert.IsType<RepoTreeDto>(result.Data);
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
