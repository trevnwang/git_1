using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MbdGitTool.Core.Contracts;
using MbdGitTool.Host.Bridge;
using MbdGitTool.Host.Services;
using Xunit.Abstractions;

namespace MbdGitTool.Host.Tests;

/// <summary>
/// 真实工作区烟测（artifacts/real-test/workspace 或 artifacts/m1-fixture/workspace）：
/// 走真实 SnapshotService + HostCommandRouter，确认新 API 能返回树/详情/提交数据。
/// <para>夹具不存在时该用例直接返回（不作为失败），避免 clean clone 误报。</para>
/// </summary>
public sealed class RealWorkspaceSmokeTests
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private readonly ITestOutputHelper _output;

    public RealWorkspaceSmokeTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task RealWorkspace_NewApis_ReturnTreeData()
    {
        var repositoryRoot = FindRepositoryRoot();
        if (repositoryRoot is null)
        {
            _output.WriteLine("未找到仓库根，跳过烟测");
            return;
        }

        var workspace = Path.Combine(repositoryRoot, "artifacts", "real-test", "workspace");
        if (!Directory.Exists(workspace))
        {
            workspace = Path.Combine(repositoryRoot, "artifacts", "m1-fixture", "workspace");
        }

        if (!Directory.Exists(workspace))
        {
            _output.WriteLine("未找到真实工作区夹具，跳过烟测");
            return;
        }

        _output.WriteLine("workspace=" + workspace);
        var config = new AppConfig { WorkspaceRoot = workspace };
        using var snapshots = new SnapshotService(config);
        var launcher = new RecordingLauncher();
        using var router = new HostCommandRouter(config, snapshots, launcher);

        // 1) 快照
        var snapshotResult = await DispatchAsync(router, "getSnapshot", null);
        Assert.True(snapshotResult.Ok, snapshotResult.Error?.Code);
        var snapshot = Assert.IsType<SnapshotDto>(snapshotResult.Data);
        _output.WriteLine($"getSnapshot source={snapshot.Source} components={snapshot.Components.Count}");
        Assert.NotEmpty(snapshot.Components);
        Assert.Equal("local", snapshot.Source);

        // 2) 工作区树根层（组件虚拟组 + 未纳管组）
        var workspaceTreeResult = await DispatchAsync(router, "getWorkspaceTree", new { path = string.Empty });
        Assert.True(workspaceTreeResult.Ok, workspaceTreeResult.Error?.Code);
        var workspaceTree = Assert.IsType<WorkspaceTreeDto>(workspaceTreeResult.Data);
        Assert.NotEmpty(workspaceTree.Entries);
        _output.WriteLine(
            $"getWorkspaceTree entries={workspaceTree.Entries.Count} "
            + $"kinds={string.Join(',', workspaceTree.Entries.Select(entry => entry.Kind))}");

        // 3) 四仓文件树（选一个本地 IMPL 仓）
        var component = snapshot.Components.FirstOrDefault(candidate =>
            candidate.Repos.Any(repo => repo.RepoType == "IMPL"
                && repo.IsLocal
                && !string.IsNullOrWhiteSpace(repo.LocalPath)));
        if (component is null)
        {
            _output.WriteLine("真实工作区无本地 IMPL 仓，跳过树/提交烟测");
            return;
        }

        var treeResult = await DispatchAsync(
            router,
            "getRepoTree",
            new { componentId = component.ComponentId, repoType = "IMPL", path = string.Empty });
        Assert.True(treeResult.Ok, treeResult.Error?.Code);
        var tree = Assert.IsType<RepoTreeDto>(treeResult.Data);
        Assert.NotEmpty(tree.Entries);
        _output.WriteLine(
            $"getRepoTree {component.ComponentId}_IMPL entries={tree.Entries.Count} "
            + $"total={tree.TotalEntries} truncated={tree.Truncated} state={tree.State}");
        _output.WriteLine(
            "repoTree names=" + string.Join(',', tree.Entries.Take(8).Select(entry => entry.Name)));

        // 4) 单仓详情（repo + component + 最近提交）
        var detailResult = await DispatchAsync(
            router,
            "getRepoDetail",
            new { componentId = component.ComponentId, repoType = "IMPL" });
        Assert.True(detailResult.Ok, detailResult.Error?.Code);
        var detail = Assert.IsType<RepoDetailResponse>(detailResult.Data);
        _output.WriteLine(
            $"getRepoDetail repo={detail.Repo.RepoType} commits.total={detail.Commits.Total} "
            + $"items={detail.Commits.Items.Count}");

        // 5) 提交分页
        var commitsResult = await DispatchAsync(
            router,
            "getCommits",
            new { componentId = component.ComponentId, repoType = "IMPL", skip = 0, take = 5 });
        Assert.True(commitsResult.Ok, commitsResult.Error?.Code);
        var commits = Assert.IsType<CommitsPageDto>(commitsResult.Data);
        Assert.NotEmpty(commits.Items);
        _output.WriteLine(
            $"getCommits total={commits.Total} first={commits.Items[0].ShortSha} {commits.Items[0].Message}");

        // 6) 组件详情 + CI 占位 + HostInfo
        var componentDetailResult = await DispatchAsync(
            router,
            "getComponentDetail",
            new { componentId = component.ComponentId });
        Assert.True(componentDetailResult.Ok, componentDetailResult.Error?.Code);
        var componentDetail = Assert.IsType<ComponentDetailDto>(componentDetailResult.Data);
        _output.WriteLine(
            $"getComponentDetail repos={componentDetail.Repos.Count} "
            + $"actions={componentDetail.Repos[0].Actions.Count} ci.enabled={componentDetail.Ci.Enabled}");

        var ciResult = await DispatchAsync(router, "getCiStatus", new { componentId = component.ComponentId });
        Assert.True(ciResult.Ok, ciResult.Error?.Code);
        Assert.IsType<CiStatusDto>(ciResult.Data);

        var hostInfoResult = await DispatchAsync(router, "getHostInfo", null);
        Assert.True(hostInfoResult.Ok, hostInfoResult.Error?.Code);
        var hostInfo = Assert.IsType<HostInfoDto>(hostInfoResult.Data);
        _output.WriteLine(
            $"getHostInfo version={hostInfo.AppVersion} maxEntries={hostInfo.Limits.MaxDirectoryEntries}");

        // 7) openPath（RecordingLauncher：验证路由/校验/扩展名分级，不真正启动程序）
        var firstFile = tree.Entries.FirstOrDefault(entry => entry.Kind == "FILE");
        if (firstFile is not null)
        {
            var openResult = await DispatchAsync(
                router,
                "openPath",
                new
                {
                    componentId = component.ComponentId,
                    repoType = "IMPL",
                    path = firstFile.Name,
                    mode = "reveal"
                });
            Assert.True(openResult.Ok, openResult.Error?.Code);
            var opened = Assert.IsType<OpenPathResultDto>(openResult.Data);
            Assert.True(opened.Opened);
            Assert.Single(launcher.Calls);
            _output.WriteLine(
                $"openPath mode={opened.Mode} class={opened.ExtensionClass} path={opened.Path} "
                + $"launcher={Path.GetFileName(launcher.Calls[0].FileName)}");
        }

        // 7) getRemoteRefs（P0-7 只读桥：本地分支/Tag 列表；对真实工作区安全）
        var refsResult = await DispatchAsync(
            router,
            "getRemoteRefs",
            new { componentId = component.ComponentId, repoType = "IMPL" });
        Assert.True(refsResult.Ok, refsResult.Error?.Code);
        var remoteRefs = Assert.IsType<RemoteRefsDto>(refsResult.Data);
        Assert.Contains(remoteRefs.Refs, item => item.Kind == "LOCAL_BRANCH");
        _output.WriteLine(
            $"getRemoteRefs total={remoteRefs.Total} truncated={remoteRefs.Truncated} kinds="
            + string.Join(',', remoteRefs.Refs.Select(item => item.Kind).Distinct()));

        // 8) runRepoAction：P1-8 hermetic——真实工作区写操作（PULL/可能访问真实 Gitea）默认不做，
        //    仅当显式设置 MBD_RUN_REAL_WRITE_TESTS=true 时才执行；常规 dotnet test 只读。
        if (!string.Equals(
                Environment.GetEnvironmentVariable("MBD_RUN_REAL_WRITE_TESTS"),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            _output.WriteLine("MBD_RUN_REAL_WRITE_TESTS != true：跳过真实工作区写操作（PULL）烟测");
            return;
        }

        var actionResult = await DispatchAsync(
            router,
            "runRepoAction",
            new { componentId = component.ComponentId, repoType = "IMPL", action = "PULL" });
        Assert.True(
            actionResult.Ok
            || actionResult.Error?.Code == BridgeErrorCodes.ActionNotAllowed
            || actionResult.Error?.Code == BridgeErrorCodes.GitNonFastForward
            || actionResult.Error?.Code == BridgeErrorCodes.WorktreeOverwrite
            || actionResult.Error?.Code == BridgeErrorCodes.ConcurrentChange,
            actionResult.Error?.Code);
        _output.WriteLine($"runRepoAction ok={actionResult.Ok} code={actionResult.Error?.Code}");
    }

    private static Task<CommandResult> DispatchAsync(
        HostCommandRouter router,
        string method,
        object? parameters)
    {
        JsonElement? element = parameters is null ? null : JsonSerializer.SerializeToElement(parameters, WebJson);
        return router.DispatchAsync(method, element, CancellationToken.None);
    }

    private static string? FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MbdGitTool.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
