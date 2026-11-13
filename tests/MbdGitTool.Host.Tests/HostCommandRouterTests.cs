using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MbdGitTool.Core.Contracts;
using MbdGitTool.Host.Bridge;
using MbdGitTool.Host.Services;

namespace MbdGitTool.Host.Tests;

public sealed class HostCommandRouterTests
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Dispatch_UnknownMethod_ReturnsUnknownMethod()
    {
        using var router = CreateMockRouter();

        var result = await CallAsync(router, "getNothing", null);

        Assert.False(result.Ok);
        Assert.Equal(BridgeErrorCodes.UnknownMethod, result.Error!.Code);
    }

    [Fact]
    public async Task Dispatch_GetSnapshot_MockSourceAndOptionalFields()
    {
        using var router = CreateMockRouter();

        var result = await CallAsync(router, "getSnapshot", null);

        Assert.True(result.Ok);
        var snapshot = Assert.IsType<SnapshotDto>(result.Data);
        Assert.Equal("mock", snapshot.Source);
        Assert.NotEmpty(snapshot.Components);
        Assert.NotNull(snapshot.Counts);
        Assert.NotNull(snapshot.Freshness);
        Assert.NotNull(snapshot.Capabilities);
        Assert.False(snapshot.Capabilities!.RepoActions);
    }

    [Fact]
    public async Task Dispatch_GetHostInfo_ReturnsFeaturesAndLimits()
    {
        using var router = CreateMockRouter();

        var result = await CallAsync(router, "getHostInfo", null);

        Assert.True(result.Ok);
        var info = Assert.IsType<HostInfoDto>(result.Data);
        Assert.True(info.Features.FileTree);
        Assert.True(info.Features.WorkspaceTree);
        Assert.True(info.Features.OpenPath);
        Assert.False(info.Features.RepoActions);
        Assert.Equal(2000, info.Limits.MaxDirectoryEntries);
        Assert.Equal(200, info.Limits.MaxCommitsPerPage);
    }

    [Fact]
    public async Task Dispatch_GetComponentDetail_MockComponent_ReturnsFourReposAndActions()
    {
        using var router = CreateMockRouter();
        await CallAsync(router, "getSnapshot", null);

        var result = await CallAsync(
            router,
            "getComponentDetail",
            new { componentId = "MOCK_CF1001_ChanganSigProcgBascFct" });

        Assert.True(result.Ok);
        var detail = Assert.IsType<ComponentDetailDto>(result.Data);
        Assert.Equal(4, detail.Repos.Count);
        Assert.All(detail.Repos, repo => Assert.Equal(5, repo.Actions.Count));
        Assert.False(detail.Ci.Enabled);
    }

    [Fact]
    public async Task Dispatch_GetComponentDetail_ParamValidation()
    {
        using var router = CreateMockRouter();
        await CallAsync(router, "getSnapshot", null);

        var missing = await CallAsync(router, "getComponentDetail", new { });
        Assert.Equal(BridgeErrorCodes.InvalidArgument, missing.Error!.Code);

        var wrongType = await CallAsync(router, "getComponentDetail", new { componentId = 123 });
        Assert.Equal(BridgeErrorCodes.InvalidArgument, wrongType.Error!.Code);

        var unknown = await CallAsync(router, "getComponentDetail", new { componentId = "NOPE" });
        Assert.Equal(BridgeErrorCodes.NotFound, unknown.Error!.Code);
    }

    [Fact]
    public async Task Dispatch_GetCiStatus_ReturnsMockNoneAggregate()
    {
        using var router = CreateMockRouter();
        await CallAsync(router, "getSnapshot", null);

        var result = await CallAsync(
            router,
            "getCiStatus",
            new { componentId = "MOCK_CF1001_ChanganSigProcgBascFct" });

        Assert.True(result.Ok);
        var status = Assert.IsType<CiStatusDto>(result.Data);
        Assert.False(status.Enabled);
        Assert.Empty(status.Latest);
        Assert.Equal("NONE", status.Aggregate.WorstState);
        Assert.Equal(4, status.Aggregate.ByState["NONE"]);
    }

    [Fact]
    public async Task Dispatch_RunRepoAction_IsReadOnly()
    {
        using var router = CreateMockRouter();
        await CallAsync(router, "getSnapshot", null);

        var result = await CallAsync(
            router,
            "runRepoAction",
            new { componentId = "MOCK_CF1001_ChanganSigProcgBascFct", repoType = "DESIGN", action = "PULL" });

        Assert.False(result.Ok);
        Assert.Contains(
            result.Error!.Code,
            new[] { BridgeErrorCodes.NotImplemented, BridgeErrorCodes.ActionNotAllowed });

        var badAction = await CallAsync(
            router,
            "runRepoAction",
            new { componentId = "MOCK_CF1001_ChanganSigProcgBascFct", repoType = "DESIGN", action = "RESET" });
        Assert.Equal(BridgeErrorCodes.InvalidArgument, badAction.Error!.Code);
    }

    [Fact]
    public async Task Dispatch_WithoutWorkspace_TreeAndOpenReturnWorkspaceUnset()
    {
        using var router = CreateMockRouter();
        await CallAsync(router, "getSnapshot", null);

        foreach (var method in new[] { "getRepoTree", "getWorkspaceTree", "openPath" })
        {
            var result = await CallAsync(
                router,
                method,
                method == "openPath" ? new { path = "a.txt" } : new { });
            Assert.False(result.Ok);
            Assert.Equal(BridgeErrorCodes.WorkspaceUnset, result.Error!.Code);
        }
    }

    [Fact]
    public async Task Dispatch_CancelledToken_ReturnsTimeoutCode()
    {
        using var router = CreateMockRouter();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        var result = await router.DispatchAsync("getSnapshot", null, cancelled.Token);

        Assert.False(result.Ok);
        Assert.Equal(BridgeErrorCodes.Timeout, result.Error!.Code);
    }

    [Fact]
    public async Task Dispatch_SerializedData_IsCamelCase()
    {
        using var router = CreateMockRouter();
        await CallAsync(router, "getSnapshot", null);
        var result = await CallAsync(
            router,
            "getComponentDetail",
            new { componentId = "MOCK_CF1001_ChanganSigProcgBascFct" });

        var json = JsonSerializer.Serialize(result.Data, WebJson);

        Assert.Contains("\"componentId\"", json, StringComparison.Ordinal);
        Assert.Contains("\"aggregateState\"", json, StringComparison.Ordinal);
        Assert.Contains("\"repos\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"ComponentId\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Dispatch_WithWorkspace_EndToEnd_TreeDetailAndOpen()
    {
        using var ws = new TempWorkspace();
        var impl = ws.Dir("CF100A_Demo_Impl");
        ws.CreateFile("CF100A_Demo_Impl/README.md", "readme");
        ws.Dir("misc");

        var launcher = new RecordingLauncher();
        using var router = CreateWorkspaceRouter(ws.Root, launcher);

        var snapshot = await CallAsync(router, "getSnapshot", null);
        Assert.True(snapshot.Ok);
        Assert.Equal("local", Assert.IsType<SnapshotDto>(snapshot.Data).Source);

        var workspaceTree = await CallAsync(router, "getWorkspaceTree", new { path = string.Empty });
        Assert.True(workspaceTree.Ok);
        var wsTree = Assert.IsType<WorkspaceTreeDto>(workspaceTree.Data);
        Assert.Contains(wsTree.Entries, entry => entry.Kind == "COMPONENT");
        Assert.Contains(wsTree.Entries, entry => entry.Kind == "UNBOUND_GROUP");

        var repoTree = await CallAsync(
            router,
            "getRepoTree",
            new { componentId = "CF100A_Demo", repoType = "IMPL", path = string.Empty });
        Assert.True(repoTree.Ok);
        var tree = Assert.IsType<RepoTreeDto>(repoTree.Data);
        Assert.Equal(impl, tree.RootPath);
        Assert.Contains(tree.Entries, entry => entry.Name == "README.md");

        var detail = await CallAsync(
            router,
            "getRepoDetail",
            new { componentId = "CF100A_Demo", repoType = "IMPL" });
        Assert.True(detail.Ok);
        Assert.Empty(Assert.IsType<RepoDetailResponse>(detail.Data).Commits.Items);

        var commits = await CallAsync(
            router,
            "getCommits",
            new { componentId = "CF100A_Demo", repoType = "IMPL", skip = 0, take = 5 });
        Assert.False(commits.Ok);
        Assert.Equal(BridgeErrorCodes.GitFailed, commits.Error!.Code);

        var opened = await CallAsync(
            router,
            "openPath",
            new
            {
                componentId = "CF100A_Demo",
                repoType = "IMPL",
                path = "README.md",
                mode = "open"
            });
        Assert.True(opened.Ok);
        Assert.Single(launcher.Calls);
    }

    [Fact]
    public async Task Dispatch_WithWorkspace_ParamValidation()
    {
        using var ws = new TempWorkspace();
        ws.Dir("CF100A_Demo_Impl");
        using var router = CreateWorkspaceRouter(ws.Root, new RecordingLauncher());
        await CallAsync(router, "getSnapshot", null);

        var missingRepoType = await CallAsync(
            router,
            "getRepoDetail",
            new { componentId = "CF100A_Demo" });
        Assert.Equal(BridgeErrorCodes.InvalidArgument, missingRepoType.Error!.Code);

        var badRepoType = await CallAsync(
            router,
            "getCommits",
            new { componentId = "CF100A_Demo", repoType = "MAIN", skip = 0, take = 5 });
        Assert.Equal(BridgeErrorCodes.InvalidArgument, badRepoType.Error!.Code);

        var badComponent = await CallAsync(
            router,
            "getRepoTree",
            new { repoType = "IMPL" });
        Assert.Equal(BridgeErrorCodes.InvalidArgument, badComponent.Error!.Code);

        var badParams = await CallAsync(router, "getRepoTree", "not-an-object");
        Assert.Equal(BridgeErrorCodes.InvalidArgument, badParams.Error!.Code);
    }

    private static HostCommandRouter CreateMockRouter() =>
        new(new AppConfig(), new SnapshotService(new AppConfig()));

    private static HostCommandRouter CreateWorkspaceRouter(string workspaceRoot, RecordingLauncher launcher)
    {
        var config = new AppConfig { WorkspaceRoot = workspaceRoot };
        return new HostCommandRouter(config, new SnapshotService(config), launcher);
    }

    private static async Task<CommandResult> CallAsync(
        HostCommandRouter router,
        string method,
        object? parameters)
    {
        JsonElement? element = parameters is null
            ? null
            : JsonSerializer.SerializeToElement(parameters, WebJson);
        return await router.DispatchAsync(method, element, CancellationToken.None);
    }
}
