using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MbdGitTool.Core.Contracts;
using MbdGitTool.Core.Domain;

namespace MbdGitTool.Host.Services;

/// <summary>
/// 快照服务（SRS §12.1 LocalScan/StatusCalc 的 M1 只读实现）：
/// <c>LocalScanner</c> + 可选 <c>GiteaClient</c> → <see cref="SnapshotDto"/>。
/// <para>Source 取值：<c>mock</c>（未配置/根目录不可用）/ <c>local</c>（仅本地扫描）/ <c>local+gitea</c>（远程清单就绪）。</para>
/// <para>远程缺仓 → 组件 <c>BINDING_ERROR(INCOMPLETE)</c>；本地缺克隆 → 单仓 <c>MISSING</c>；</para>
/// <para>远程未就绪（H-01）时本地不完整组件不得判 INVALID/MISSING，统一 <c>UNKNOWN + incomplete</c>。</para>
/// </summary>
public sealed class SnapshotService : IDisposable
{
    private readonly AppConfig _config;
    private readonly LocalScanner _scanner;
    private readonly GiteaClient? _gitea;
    private readonly string? _giteaInitError;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    private SnapshotDto? _current;
    private bool _disposed;

    public SnapshotService(AppConfig config, LocalScanner? scanner = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _scanner = scanner ?? new LocalScanner();

        if (config.GiteaEnabled)
        {
            try
            {
                _gitea = new GiteaClient(config.GiteaBaseUrl!, config.GiteaOrg!, config.GiteaToken);
            }
            catch (ArgumentException ex)
            {
                // 配置非法不阻断启动：降级为 local，并给出可读原因（不含 token）。
                _giteaInitError = ex.Message;
                HostLog.Warn($"Gitea 客户端初始化失败：{ex.Message}");
            }
        }
    }

    public AppConfig Config => _config;

    /// <summary>最近一次快照（可能为 null，首次需 Refresh）。</summary>
    public SnapshotDto? Current => Volatile.Read(ref _current);

    /// <summary>读取当前快照；从未刷新过则立即刷新一次（互斥等待时复用已生成的快照，避免重复扫描）。</summary>
    public async Task<SnapshotDto> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var current = Current;
        if (current is not null)
        {
            return current;
        }

        await _refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // 等待期间预热/其他消息可能已完成刷新，直接复用避免二次全量扫描。
            return Current ?? await BuildAndStoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    /// <summary>重新扫描并返回新快照（并发调用串行化，避免重复扫描）。</summary>
    public async Task<SnapshotDto> RefreshAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await BuildAndStoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private async Task<SnapshotDto> BuildAndStoreAsync(CancellationToken cancellationToken)
    {
        var snapshot = await BuildAsync(cancellationToken).ConfigureAwait(false);
        Volatile.Write(ref _current, snapshot);
        HostLog.Info(
            $"快照完成：source={snapshot.Source} components={snapshot.Components.Count} error={snapshot.Error ?? "<none>"}");
        return snapshot;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _refreshGate.Dispose();
    }

    private async Task<SnapshotDto> BuildAsync(CancellationToken cancellationToken)
    {
        if (!_config.HasWorkspace)
        {
            var reason = _config.LoadError ?? "未配置工作区根目录（config.json 的 workspaceRoot）";
            return BuildMock($"工作区未配置，当前为 Mock 数据：{reason}");
        }

        var root = _config.WorkspaceRoot!;
        if (!Directory.Exists(root))
        {
            return BuildMock($"工作区根目录不存在：{root}（当前为 Mock 数据）");
        }

        var rootError = ValidateRoot(root);
        if (rootError is not null)
        {
            return BuildMock($"{rootError}（当前为 Mock 数据）");
        }

        LocalScanResult local;
        try
        {
            local = await _scanner.ScanAsync(root, _config, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            HostLog.Error($"本地扫描失败：{ex.GetType().Name}");
            return CreateSnapshot(
                source: "local",
                workspaceRoot: root,
                error: $"本地扫描失败（{ex.GetType().Name}）：{ex.Message}",
                components: Array.Empty<ComponentDto>());
        }

        LogScanResult(local);

        if (_gitea is null)
        {
            // 未配置 Gitea 属正常“仅本地”模式（Source=local），不算降级错误；初始化失败才带 Error。
            return CreateSnapshot(
                source: "local",
                workspaceRoot: root,
                error: _giteaInitError,
                components: AssembleLocal(local));
        }

        try
        {
            var remoteRepos = await _gitea.GetOrgReposAsync(cancellationToken).ConfigureAwait(false);
            HostLog.Info($"Gitea 组织仓库清单同步完成：{remoteRepos.Count} 仓");
            return CreateSnapshot(
                source: "local+gitea",
                workspaceRoot: root,
                error: null,
                components: AssembleWithRemote(local, remoteRepos, root));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (GiteaApiException ex)
        {
            HostLog.Warn($"Gitea 只读同步失败：HTTP {ex.StatusCode}");
            return CreateSnapshot(
                source: "local",
                workspaceRoot: root,
                error: ex.Message,
                components: AssembleLocal(local));
        }
        catch (Exception ex)
        {
            HostLog.Warn($"Gitea 只读同步失败：{ex.GetType().Name}");
            return CreateSnapshot(
                source: "local",
                workspaceRoot: root,
                error: $"Gitea 同步失败（{ex.GetType().Name}）：{ex.Message}",
                components: AssembleLocal(local));
        }
    }

    private static void LogScanResult(LocalScanResult local)
    {
        HostLog.Info($"本地扫描完成：识别 {local.Repos.Count} 仓，问题 {local.Issues.Count} 条");
        foreach (var issue in local.Issues.Take(20))
        {
            HostLog.Warn($"扫描问题：{issue}");
        }

        if (local.Issues.Count > 20)
        {
            HostLog.Warn($"扫描问题其余 {local.Issues.Count - 20} 条已省略（写入诊断包时全量导出）");
        }
    }

    private static IReadOnlyList<ComponentDto> AssembleLocal(LocalScanResult local)
    {
        var inputs = local.Repos
            .Select(repo => new RepoInput(repo.ComponentId, repo.RepoType, repo.Facts))
            .ToList();

        // 远程未就绪：不判 MISSING/INVALID；不完整组件由 ComponentAssembler 按 H-01 记 UNKNOWN + incomplete。
        return ComponentAssembler.Assemble(
            inputs,
            new ComponentAssemblyOptions { RemoteKnown = false });
    }

    private static IReadOnlyList<ComponentDto> AssembleWithRemote(
        LocalScanResult local,
        IReadOnlyList<GiteaRepo> remoteRepos,
        string workspaceRoot)
    {
        var remoteGroups = new Dictionary<string, Dictionary<RepoType, List<GiteaRepo>>>(StringComparer.OrdinalIgnoreCase);
        var remoteNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var unboundRemote = 0;

        foreach (var repo in remoteRepos)
        {
            if (!RepoNameParser.TryParse(repo.Name, out var componentId, out var repoType))
            {
                // §6.1：解析失败必须暴露，禁止静默丢弃（M1 写日志；未绑定清单 UI 归 M1 前端/后续）。
                unboundRemote++;
                HostLog.Warn($"未绑定远程仓库（命名不符合 SRS §6.1）：{repo.FullName}");
                continue;
            }

            if (!remoteGroups.TryGetValue(componentId, out var byType))
            {
                byType = new Dictionary<RepoType, List<GiteaRepo>>();
                remoteGroups[componentId] = byType;
                remoteNames[componentId] = componentId;
            }

            if (!byType.TryGetValue(repoType, out var list))
            {
                list = new List<GiteaRepo>();
                byType[repoType] = list;
            }

            list.Add(repo);
        }

        if (unboundRemote > 0)
        {
            HostLog.Warn($"远程未绑定仓库共 {unboundRemote} 个（组件绑定只处理可解析仓库）");
        }

        var localGroups = new Dictionary<string, List<LocalRepoEntry>>(StringComparer.OrdinalIgnoreCase);
        foreach (var repo in local.Repos)
        {
            if (!localGroups.TryGetValue(repo.ComponentId, out var list))
            {
                list = new List<LocalRepoEntry>();
                localGroups[repo.ComponentId] = list;
            }

            list.Add(repo);
        }

        var componentKeys = new HashSet<string>(remoteGroups.Keys, StringComparer.OrdinalIgnoreCase);
        componentKeys.UnionWith(localGroups.Keys);

        var inputs = new List<RepoInput>();
        var ambiguous = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in componentKeys.OrderBy(k => k, StringComparer.Ordinal))
        {
            var componentId = remoteNames.TryGetValue(key, out var canonical)
                ? canonical
                : localGroups[key][0].ComponentId;

            var localByType = localGroups.TryGetValue(key, out var localList)
                ? localList.GroupBy(r => r.RepoType).ToDictionary(g => g.Key, g => g.First())
                : new Dictionary<RepoType, LocalRepoEntry>();

            if (!remoteGroups.TryGetValue(key, out var remoteByType))
            {
                // 远程清单里没有该组件任何仓：本地仓标 remote_gone（远程已删除），不得假绿。
                foreach (var localRepo in localList ?? new List<LocalRepoEntry>())
                {
                    inputs.Add(new RepoInput(
                        componentId,
                        localRepo.RepoType,
                        localRepo.Facts with { RemoteGone = true }));
                }

                continue;
            }

            foreach (var repoType in new[] { RepoType.Design, RepoType.Impl, RepoType.Mil, RepoType.Sil })
            {
                remoteByType.TryGetValue(repoType, out var remoteList);

                if (remoteList is { Count: > 1 })
                {
                    // 远程同组件同类型 >1 仓 → AMBIGUOUS（BINDING_ERROR, severity=1）。
                    ambiguous.Add(componentId);
                }

                if (remoteList is null || remoteList.Count == 0)
                {
                    if (localByType.TryGetValue(repoType, out var orphan))
                    {
                        inputs.Add(new RepoInput(componentId, repoType, orphan.Facts with { RemoteGone = true }));
                    }

                    continue;
                }

                if (localByType.TryGetValue(repoType, out var localRepo))
                {
                    inputs.Add(new RepoInput(componentId, repoType, localRepo.Facts));
                }
                else
                {
                    // 远程有仓、本地无克隆 → 单仓 MISSING（组件聚合按 §7.4 MISSING，不是缺仓 BINDING_ERROR）。
                    var expectedPath = Path.Combine(workspaceRoot, remoteList[0].Name);
                    inputs.Add(new RepoInput(componentId, repoType, RepoFacts.Missing(expectedPath)));
                }
            }
        }

        return ComponentAssembler.Assemble(
            inputs,
            new ComponentAssemblyOptions
            {
                RemoteKnown = true,
                AmbiguousComponentIds = ambiguous
            });
    }

    private static string? ValidateRoot(string root)
    {
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(root);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return $"工作区根目录路径非法：{root}";
        }

        var pathRoot = Path.GetPathRoot(fullPath);
        if (pathRoot is not null && PathEquals(fullPath, pathRoot))
        {
            return $"工作区根目录不能是盘符根（{fullPath}）";
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(userProfile) && PathEquals(fullPath, userProfile))
        {
            return $"工作区根目录不能是用户目录根（{fullPath}）";
        }

        return null;
    }

    private static bool PathEquals(string left, string right) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(left),
            Path.TrimEndingDirectorySeparator(right),
            StringComparison.OrdinalIgnoreCase);

    private static SnapshotDto CreateSnapshot(
        string source,
        string? workspaceRoot,
        string? error,
        IReadOnlyList<ComponentDto> components) =>
        new(
            Source: source,
            WorkspaceRoot: workspaceRoot,
            Error: error,
            GeneratedAt: DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            Components: components);

    /// <summary>未配置/根目录不可用时的 Mock 快照（Source=mock + Error 说明），保证 UI 七态可渲染。</summary>
    private static SnapshotDto BuildMock(string error)
    {
        var inputs = new List<RepoInput>();

        static RepoFacts BranchFacts(string branch, int ahead = 0, int behind = 0, bool dirty = false) => new()
        {
            HeadKind = HeadKind.Branch,
            Branch = branch,
            HeadSha = new string('0', 40),
            UpstreamConfigured = true,
            UpstreamRef = $"refs/remotes/origin/{branch}",
            Ahead = ahead,
            Behind = behind,
            DirtyTracked = dirty,
            DirtyCount = dirty ? 2 : 0
        };

        static RepoFacts DetachedTagFacts(string tag) => new()
        {
            HeadKind = HeadKind.DetachedTag,
            HeadSha = new string('1', 40),
            TagsAtHead = new[] { tag },
            LocalTags = new[] { tag }
        };

        // 1) 同步 + 脏 + 落后 → BEHIND
        inputs.Add(new RepoInput("MOCK_CF1001_ChanganSigProcgBascFct", RepoType.Design, BranchFacts("main")));
        inputs.Add(new RepoInput("MOCK_CF1001_ChanganSigProcgBascFct", RepoType.Impl, BranchFacts("main", dirty: true)));
        inputs.Add(new RepoInput("MOCK_CF1001_ChanganSigProcgBascFct", RepoType.Mil, BranchFacts("main", behind: 2)));
        inputs.Add(new RepoInput("MOCK_CF1001_ChanganSigProcgBascFct", RepoType.Sil, BranchFacts("main")));

        // 2) 本地缺 SIL 克隆 → MISSING
        inputs.Add(new RepoInput("MOCK_CF1002_AebDemoComponent", RepoType.Design, BranchFacts("develop")));
        inputs.Add(new RepoInput("MOCK_CF1002_AebDemoComponent", RepoType.Impl, BranchFacts("develop")));
        inputs.Add(new RepoInput("MOCK_CF1002_AebDemoComponent", RepoType.Mil, BranchFacts("develop")));
        inputs.Add(new RepoInput(
            "MOCK_CF1002_AebDemoComponent",
            RepoType.Sil,
            RepoFacts.Missing(@"C:\Mock\workspace\MOCK_CF1002_AebDemoComponent_SIL")));

        // 3) 四仓同一 tag 检出 → DETACHED
        foreach (var repoType in new[] { RepoType.Design, RepoType.Impl, RepoType.Mil, RepoType.Sil })
        {
            inputs.Add(new RepoInput("MOCK_CF1003_VehicleDynamics", repoType, DetachedTagFacts("v1.0")));
        }

        var components = ComponentAssembler.Assemble(
            inputs,
            new ComponentAssemblyOptions { RemoteKnown = true });

        return CreateSnapshot("mock", workspaceRoot: null, error: error, components: components);
    }
}
