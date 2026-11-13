using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MbdGitTool.Core.Contracts;
using MbdGitTool.Core.Domain;
using MbdGitTool.Core.Files;
using MbdGitTool.Core.Security;

namespace MbdGitTool.Host.Services;

/// <summary>
/// getWorkspaceTree（spec §7.4 / code-plan §4.5）：文件夹视图。
/// <list type="bullet">
/// <item>根层：快照组件虚拟组（COMPONENT → 四仓 REPO，含 MISSING）+ 根目录一次枚举得到“未纳管”组与根文件，不枚举仓内部；</item>
/// <item>展开：仓/目录懒加载（DIR/FILE 条目，path 相对工作区根，可直接作为下一次请求的 path）；30s 缓存；</item>
/// <item>安全：所有 path 经 <see cref="PathPolicy"/>；reparse 不递归；gitStatus/submodule 仅对已绑定仓注解。</item>
/// </list>
/// </summary>
public sealed class WorkspaceTreeService
{
    public const int DefaultLimit = 2000;

    private readonly Func<SnapshotIndex> _indexProvider;
    private readonly PathPolicy _paths;
    private readonly FileTreeReader _reader;
    private readonly GitStatusIndex _annotations;
    private readonly TtlCache<CachedPage> _cache = new(TimeSpan.FromSeconds(30), 256);

    public WorkspaceTreeService(
        Func<SnapshotIndex> indexProvider,
        PathPolicy paths,
        FileTreeReader reader,
        GitStatusIndex annotations)
    {
        _indexProvider = indexProvider ?? throw new ArgumentNullException(nameof(indexProvider));
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _annotations = annotations ?? throw new ArgumentNullException(nameof(annotations));
    }

    public async Task<CommandResult> GetTreeAsync(
        GetWorkspaceTreeParams parameters,
        CancellationToken cancellationToken)
    {
        parameters ??= new GetWorkspaceTreeParams();
        var index = _indexProvider();
        var snapshot = index.Snapshot;
        if (snapshot is null)
        {
            return CommandResult.Failure(
                BridgeErrorCodes.WorkspaceUnset,
                "工作区未就绪（快照尚未完成），请先刷新");
        }

        if (!Directory.Exists(_paths.Root))
        {
            return CommandResult.Failure(
                BridgeErrorCodes.WorkspaceInvalid,
                "工作区根目录不存在或不可读",
                new Dictionary<string, object?> { ["root"] = _paths.Root });
        }

        var relativePath = PathPolicy.NormalizeRelative(parameters.Path);
        var includeHidden = parameters.IncludeHidden ?? true;
        var includeMissingRepos = parameters.IncludeMissingRepos ?? true;
        var limit = Math.Clamp(parameters.Limit ?? DefaultLimit, 1, DefaultLimit);

        var stopwatch = Stopwatch.StartNew();
        CachedPage page;
        if (relativePath.Length == 0)
        {
            var rootKey = string.Create(
                CultureInfo.InvariantCulture,
                $"wsroot|{snapshot.GeneratedAt}|{includeHidden}|{includeMissingRepos}|{limit}");
            if (!_cache.TryGet(rootKey, out page!))
            {
                page = BuildRoot(index, snapshot, includeHidden, includeMissingRepos, limit, cancellationToken);
                _cache.Set(rootKey, page);
            }
        }
        else
        {
            var expansionKey = string.Create(
                CultureInfo.InvariantCulture,
                $"wspage|{snapshot.GeneratedAt}|{relativePath}|{includeHidden}|{limit}");
            if (!_cache.TryGet(expansionKey, out page!))
            {
                var expansion = await BuildExpansionAsync(
                    index,
                    snapshot,
                    relativePath,
                    includeHidden,
                    limit,
                    cancellationToken).ConfigureAwait(false);
                if (expansion.Error is not null)
                {
                    return CommandResult.Failure(expansion.Error);
                }

                page = expansion.Page!;
                _cache.Set(expansionKey, page);
            }
        }

        stopwatch.Stop();
        if (page.ErrorCode is not null)
        {
            HostLog.Warn($"getWorkspaceTree 降级：path={relativePath} code={page.ErrorCode}");
        }

        var tree = new WorkspaceTreeDto(
            Root: _paths.Root,
            Path: relativePath,
            GeneratedAt: snapshot.GeneratedAt,
            ReadAt: DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            Entries: page.Entries,
            Truncated: page.Truncated,
            Error: page.ErrorCode);

        HostLog.Info(
            $"api getWorkspaceTree path={relativePath} entries={page.Entries.Count} "
            + $"truncated={page.Truncated} ms={stopwatch.ElapsedMilliseconds}");
        return CommandResult.Success(tree);
    }

    /// <summary>清空缓存（refresh 后）。</summary>
    public void InvalidateCaches() => _cache.InvalidateAll();

    private CachedPage BuildRoot(
        SnapshotIndex index,
        SnapshotDto snapshot,
        bool includeHidden,
        bool includeMissingRepos,
        int limit,
        CancellationToken cancellationToken)
    {
        var entries = new List<WorkspaceNodeDto>();
        var knownRepoPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var component in index.Components)
        {
            var repoNodes = new List<WorkspaceNodeDto>();
            var dirtyCount = 0;

            foreach (var repo in component.Repos)
            {
                if (!includeMissingRepos && !repo.IsLocal)
                {
                    continue;
                }

                dirtyCount += repo.DirtyCount;
                var repoName = RepoName(component.ComponentId, repo);
                var repoPath = RepoWorkspaceRelative(repo, repoName);
                knownRepoPaths.Add(repoPath);
                repoNodes.Add(new WorkspaceNodeDto(
                    Kind: "REPO",
                    Name: repoName,
                    Path: repoPath,
                    HasChildren: repo.IsLocal,
                    ComponentId: component.ComponentId,
                    RepoType: repo.RepoType,
                    State: repo.State,
                    Branch: repo.Branch,
                    Ahead: repo.Ahead,
                    Behind: repo.Behind,
                    DirtyCount: repo.DirtyCount,
                    IsLocal: repo.IsLocal));
            }

            entries.Add(new WorkspaceNodeDto(
                Kind: "COMPONENT",
                Name: component.ComponentId,
                HasChildren: true,
                ComponentId: component.ComponentId,
                State: component.AggregateState,
                SeverityRank: component.SeverityRank,
                DirtyCount: dirtyCount,
                MixedBranch: component.MixedBranch,
                Repos: repoNodes));
        }

        var rootPage = _reader.Read(
            _paths.Root,
            string.Empty,
            new TreeReadOptions(
                Depth: 1,
                Limit: limit,
                IncludeGitStatus: false,
                IncludeIgnored: false,
                IncludeHidden: includeHidden),
            cancellationToken);

        var unbound = new List<WorkspaceNodeDto>();
        var rootFiles = new List<WorkspaceNodeDto>();
        foreach (var node in rootPage.Entries)
        {
            if (string.Equals(node.Kind, "DIR", StringComparison.Ordinal))
            {
                if (knownRepoPaths.Contains(node.Path))
                {
                    continue;
                }

                unbound.Add(new WorkspaceNodeDto(
                    Kind: "DIR",
                    Name: node.Name,
                    Path: node.Path,
                    HasChildren: true,
                    IsReparsePoint: node.IsReparsePoint));
            }
            else
            {
                rootFiles.Add(ToWorkspaceNode(node));
            }
        }

        if (unbound.Count > 0)
        {
            entries.Add(new WorkspaceNodeDto(
                Kind: "UNBOUND_GROUP",
                Name: "未纳管",
                HasChildren: true,
                Count: unbound.Count,
                Children: unbound));
        }

        entries.AddRange(rootFiles);

        var errorCode = rootPage.Warnings.Count > 0 ? rootPage.Warnings[0] : null;
        return new CachedPage(entries, rootPage.Truncated, errorCode);
    }

    private async Task<ExpansionResult> BuildExpansionAsync(
        SnapshotIndex index,
        SnapshotDto snapshot,
        string relativePath,
        bool includeHidden,
        int limit,
        CancellationToken cancellationToken)
    {
        if (!_paths.TryResolveWorkspacePath(relativePath, out var absolutePath, out var pathError))
        {
            return new ExpansionResult(null, pathError);
        }

        if (!Directory.Exists(absolutePath))
        {
            if (File.Exists(absolutePath))
            {
                return new ExpansionResult(
                    null,
                    new BridgeError(
                        BridgeErrorCodes.InvalidArgument,
                        "只能展开目录节点",
                        new Dictionary<string, object?> { ["path"] = relativePath }));
            }

            return new ExpansionResult(
                null,
                new BridgeError(
                    BridgeErrorCodes.PathInvalid,
                    "目录不存在",
                    new Dictionary<string, object?> { ["path"] = relativePath }));
        }

        if (PathPolicy.IsReparsePoint(absolutePath) || _paths.CheckReparseChain(absolutePath).HasReparse)
        {
            return new ExpansionResult(
                new CachedPage(Array.Empty<WorkspaceNodeDto>(), false, BridgeErrorCodes.ReparsePoint),
                null);
        }

        // 识别所属仓（仓根或仓内目录）：用于 gitStatus/submodule 注解与 submodule 拒绝。
        RepoRef? owner = null;
        var repoRoot = string.Empty;
        var repoRelative = string.Empty;
        if (index.TryGetRepoByPath(absolutePath, out var repoRef))
        {
            owner = repoRef;
            repoRoot = absolutePath;
        }
        else
        {
            foreach (var component in snapshot.Components)
            {
                foreach (var repo in component.Repos)
                {
                    if (!repo.IsLocal || string.IsNullOrWhiteSpace(repo.LocalPath))
                    {
                        continue;
                    }

                    var candidateRoot = PathPolicy.TryCanonicalize(repo.LocalPath);
                    if (candidateRoot is null || !_paths.IsWithin(candidateRoot, absolutePath))
                    {
                        continue;
                    }

                    owner = new RepoRef(component.ComponentId, component, repo);
                    repoRoot = candidateRoot;
                    repoRelative = _paths.ToRepoRelative(candidateRoot, absolutePath);
                    break;
                }

                if (owner is not null)
                {
                    break;
                }
            }
        }

        IReadOnlySet<string> submodules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        IReadOnlyDictionary<string, string> statuses = new Dictionary<string, string>(StringComparer.Ordinal);
        if (owner is not null)
        {
            submodules = await _annotations.GetSubmodulesAsync(repoRoot, cancellationToken).ConfigureAwait(false);
            if (repoRelative.Length > 0 && IsInsideSubmodule(submodules, repoRelative))
            {
                return new ExpansionResult(
                    null,
                    new BridgeError(
                        BridgeErrorCodes.Submodule,
                        "submodule 内部禁止展开（未纳管）",
                        new Dictionary<string, object?> { ["path"] = relativePath }));
            }

            if (!string.Equals(owner.Repo.State, "INVALID", StringComparison.OrdinalIgnoreCase))
            {
                statuses = await _annotations.GetStatusAsync(repoRoot, cancellationToken).ConfigureAwait(false);
            }
        }

        // 节点 path 直接使用工作区相对路径（UI 用同一 path 继续懒加载）。
        var annotation = new TreeAnnotationProvider(
            GitStatus: nodePath => owner is null
                ? GitStatusIndex.StatusNone
                : statuses.TryGetValue(ToRepoRelative(nodePath, relativePath, repoRelative), out var status)
                    ? status
                    : GitStatusIndex.StatusNone,
            Submodule: nodePath => submodules.Contains(ToRepoRelative(nodePath, relativePath, repoRelative)));

        var repoTree = _reader.Read(
            absolutePath,
            relativePath,
            new TreeReadOptions(
                Depth: 1,
                Limit: limit,
                IncludeGitStatus: owner is not null,
                IncludeIgnored: false,
                IncludeHidden: includeHidden),
            cancellationToken,
            annotation);

        var entries = new List<WorkspaceNodeDto>(repoTree.Entries.Count);
        foreach (var node in repoTree.Entries)
        {
            entries.Add(ToWorkspaceNode(node));
        }

        var errorCode = repoTree.Warnings.Count > 0 ? repoTree.Warnings[0] : null;
        return new ExpansionResult(new CachedPage(entries, repoTree.Truncated, errorCode), null);
    }

    private static string ToRepoRelative(string nodePath, string parentRelative, string parentRepoRelative)
    {
        // nodePath 以工作区相对路径给出；父目录的仓相对路径 + 节点名 = 节点仓相对路径。
        if (parentRepoRelative.Length == 0)
        {
            return nodePath.Length > parentRelative.Length + 1
                ? nodePath[(parentRelative.Length + 1)..]
                : nodePath;
        }

        return nodePath.Length > parentRelative.Length + 1
            ? parentRepoRelative + "/" + nodePath[(parentRelative.Length + 1)..]
            : parentRepoRelative;
    }

    private static WorkspaceNodeDto ToWorkspaceNode(TreeNodeDto node) => new(
        Kind: node.Kind,
        Name: node.Name,
        Path: node.Path,
        HasChildren: node.HasChildren,
        Size: node.Size,
        Mtime: node.Mtime,
        GitStatus: node.GitStatus,
        Submodule: node.Submodule,
        Ignored: node.Ignored,
        IsReparsePoint: node.IsReparsePoint,
        Error: node.Error,
        Children: node.Children?.Select(ToWorkspaceNode).ToList());

    private static bool IsInsideSubmodule(IReadOnlySet<string> submodules, string relativePath)
    {
        foreach (var submodule in submodules)
        {
            if (relativePath.Equals(submodule, StringComparison.OrdinalIgnoreCase)
                || relativePath.StartsWith(submodule + "/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private string RepoWorkspaceRelative(RepoDto repo, string fallbackName)
    {
        if (!string.IsNullOrWhiteSpace(repo.LocalPath))
        {
            var relative = _paths.ToWorkspaceRelative(repo.LocalPath);
            if (relative.Length > 0)
            {
                return relative;
            }
        }

        return fallbackName;
    }

    private static string RepoName(string componentId, RepoDto repo)
    {
        if (!string.IsNullOrWhiteSpace(repo.RepoName))
        {
            return repo.RepoName;
        }

        if (!string.IsNullOrWhiteSpace(repo.LocalPath))
        {
            var name = Path.GetFileName(repo.LocalPath.TrimEnd('\\', '/'));
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
        }

        return componentId + "_" + repo.RepoType;
    }

    private sealed record CachedPage(IReadOnlyList<WorkspaceNodeDto> Entries, bool Truncated, string? ErrorCode);

    private sealed record ExpansionResult(CachedPage? Page, BridgeError? Error);
}
