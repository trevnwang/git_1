using System;
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

public sealed class WorkspaceTreeServiceTests : IDisposable
{
    private readonly TempWorkspace _ws = new();

    public void Dispose() => _ws.Dispose();

    [Fact]
    public async Task GetTree_Root_GroupsComponentsAndUnboundGroup()
    {
        var (service, _) = CreateService();
        var tree = await GetOkAsync(service, new GetWorkspaceTreeParams { Path = string.Empty });

        var component = Assert.IsType<WorkspaceNodeDto>(tree.Entries[0]);
        Assert.Equal("COMPONENT", component.Kind);
        Assert.Equal("CF100A_Demo", component.ComponentId);
        Assert.NotNull(component.Repos);
        Assert.Equal(
            new[] { "DESIGN", "IMPL", "MIL", "SIL" },
            component.Repos!.Select(repo => repo.RepoType));
        Assert.Equal("REPO", component.Repos![0].Kind);
        Assert.True(component.Repos[0].IsLocal);
        Assert.False(component.Repos[2].IsLocal);
        Assert.False(component.Repos[2].HasChildren);

        var unbound = Assert.Single(tree.Entries, entry => entry.Kind == "UNBOUND_GROUP");
        Assert.Equal("未纳管", unbound.Name);
        Assert.Equal(1, unbound.Count);
        Assert.Equal("X_SIL_Design", Assert.Single(unbound.Children!).Name);

        Assert.Contains(tree.Entries, entry => entry.Kind == "FILE" && entry.Name == "notes.txt");
    }

    [Fact]
    public async Task GetTree_ExpandNestedRepoDirectory_KeepsWorkspaceRelativePaths()
    {
        var (service, _) = CreateService();

        var tree = await GetOkAsync(service, new GetWorkspaceTreeParams { Path = "CF100A_Demo_Impl/src" });

        Assert.Equal("CF100A_Demo_Impl/src", tree.Path);
        var main = Assert.Single(tree.Entries);
        Assert.Equal("main.m", main.Name);
        Assert.Equal("CF100A_Demo_Impl/src/main.m", main.Path);
    }

    [Fact]
    public async Task GetTree_ExpandRepo_ReturnsWorkspaceRelativePaths()
    {
        var (service, _) = CreateService();

        var tree = await GetOkAsync(service, new GetWorkspaceTreeParams { Path = "CF100A_Demo_Impl" });

        Assert.Equal("CF100A_Demo_Impl", tree.Path);
        var readme = Assert.Single(tree.Entries, entry => entry.Name == "README.md");
        Assert.Equal("CF100A_Demo_Impl/README.md", readme.Path);
        Assert.Equal("FILE", readme.Kind);
        var src = Assert.Single(tree.Entries, entry => entry.Name == "src");
        Assert.Equal("CF100A_Demo_Impl/src", src.Path);
        Assert.Equal("DIR", src.Kind);
    }

    [Fact]
    public async Task GetTree_ExpandUnboundDirectory_ReturnsEntries()
    {
        var (service, _) = CreateService();

        var tree = await GetOkAsync(service, new GetWorkspaceTreeParams { Path = "X_SIL_Design" });

        Assert.Equal("X_SIL_Design", tree.Path);
        var sub = Assert.Single(tree.Entries);
        Assert.Equal("X_SIL_Design/sub", sub.Path);
        Assert.Equal("DIR", sub.Kind);
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("C:\\Windows")]
    [InlineData("\\\\server\\share")]
    [InlineData("/etc/passwd")]
    [InlineData(".git/config")]
    public async Task GetTree_PathEscapeOrGitInternal_Rejected(string path)
    {
        var (service, _) = CreateService();

        var result = await service.GetTreeAsync(
            new GetWorkspaceTreeParams { Path = path },
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
    public async Task GetTree_NonexistentDirectory_ReturnsPathInvalid()
    {
        var (service, _) = CreateService();

        var result = await service.GetTreeAsync(
            new GetWorkspaceTreeParams { Path = "does-not-exist" },
            CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Equal(BridgeErrorCodes.PathInvalid, result.Error!.Code);
    }

    [Fact]
    public async Task GetTree_NoSnapshot_ReturnsWorkspaceUnset()
    {
        var paths = new PathPolicy(_ws.Root);
        var service = new WorkspaceTreeService(
            () => SnapshotIndex.Empty,
            paths,
            new FileTreeReader(),
            new GitStatusIndex());

        var result = await service.GetTreeAsync(new GetWorkspaceTreeParams { Path = string.Empty }, CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Equal(BridgeErrorCodes.WorkspaceUnset, result.Error!.Code);
    }

    [Fact]
    public async Task GetTree_IncludeMissingReposFalse_SkipsMissingRepos()
    {
        var (service, _) = CreateService();

        var tree = await GetOkAsync(service, new GetWorkspaceTreeParams
        {
            Path = string.Empty,
            IncludeMissingRepos = false
        });

        var component = tree.Entries[0];
        Assert.Equal(2, component.Repos!.Count);
        Assert.All(component.Repos!, repo => Assert.True(repo.IsLocal));
    }

    [Fact]
    public async Task GetTree_ExpansionInsideRepo_AnnotatesGitStatus()
    {
        var (service, _) = CreateService(initGit: true);

        var tree = await GetOkAsync(service, new GetWorkspaceTreeParams { Path = "CF100A_Demo_Impl" });
        var readme = Assert.Single(tree.Entries, entry => entry.Name == "README.md");
        Assert.Equal("MODIFIED", readme.GitStatus);
    }

    [Fact]
    public async Task GetTree_RootDoesNotEnumerateRepos()
    {
        var (service, _) = CreateService();

        var tree = await GetOkAsync(service, new GetWorkspaceTreeParams { Path = string.Empty });

        var component = tree.Entries[0];
        Assert.All(component.Repos!, repo => Assert.Null(repo.Children));
        Assert.DoesNotContain(tree.Entries, entry => entry.Kind == "DIR" && entry.Name == "CF100A_Demo_Impl");
    }

    private (WorkspaceTreeService Service, SnapshotIndex Index) CreateService(bool initGit = false)
    {
        var impl = _ws.Dir("CF100A_Demo_Impl");
        _ws.CreateFile("CF100A_Demo_Impl/README.md", "readme");
        _ws.CreateFile("CF100A_Demo_Impl/src/main.m", "x");

        if (initGit)
        {
            TestGit.InitRepo(impl);
            _ws.CreateFile("CF100A_Demo_Impl/README.md", "v1");
            TestGit.CommitAll(impl, "init");
            _ws.CreateFile("CF100A_Demo_Impl/README.md", "v2");
        }

        var design = _ws.Dir("CF100A_Demo_Design");
        _ws.CreateFile("CF100A_Demo_Design/README.md", "d");
        _ws.Dir("X_SIL_Design", "sub");
        _ws.CreateFile("notes.txt", "notes");

        var index = TestData.Index(
            _ws.Root,
            new ComponentSpec("CF100A_Demo", new[]
            {
                new RepoSpec("DESIGN", design),
                new RepoSpec("IMPL", impl),
                new RepoSpec("MIL", _ws.PathFor("CF100A_Demo_MIL"), IsLocal: false, State: "MISSING"),
                new RepoSpec("SIL", _ws.PathFor("CF100A_Demo_SIL"), IsLocal: false, State: "MISSING")
            }));

        var service = new WorkspaceTreeService(
            () => index,
            new PathPolicy(_ws.Root),
            new FileTreeReader(),
            new GitStatusIndex());

        return (service, index);
    }

    private static async Task<WorkspaceTreeDto> GetOkAsync(
        WorkspaceTreeService service,
        GetWorkspaceTreeParams parameters)
    {
        var result = await service.GetTreeAsync(parameters, CancellationToken.None);
        Assert.True(result.Ok, result.Error?.Code + ": " + result.Error?.Message);
        return Assert.IsType<WorkspaceTreeDto>(result.Data);
    }
}
