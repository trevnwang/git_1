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
/// getRepoTree（code-plan §4.4）：按 componentId+repoType（兼容 repoId/repoPath 别名）找到本地仓根，
/// 单目录懒加载 + 目录优先排序 + 2000 截断 + 30s 页缓存 + gitStatus/submodule 注解；错误降级不拖垮整树。
/// <para><b>安全</b>：path 相对仓根；canonical 后必须仍在仓根内且仓根在工作区根内（<see cref="PathPolicy"/>）。</para>
/// </summary>
public sealed class RepoTreeService
{
    public const int DefaultDepth = 1;
    public const int MaxDepth = 2;
    public const int DefaultLimit = 2000;

    private readonly Func<SnapshotIndex> _indexProvider;
    private readonly PathPolicy _paths;
    private readonly FileTreeReader _reader;
    private readonly GitStatusIndex _annotations;
    private readonly TtlCache<DirectoryPage> _pageCache = new(TimeSpan.FromSeconds(30), 512);

    public RepoTreeService(
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

    public async Task<CommandResult> GetTreeAsync(GetRepoTreeParams parameters, CancellationToken cancellationToken)
    {
        parameters ??= new GetRepoTreeParams();
        var index = _indexProvider();
        var snapshot = index.Snapshot;
        if (snapshot is null)
        {
            return CommandResult.Failure(
                BridgeErrorCodes.WorkspaceUnset,
                "工作区未就绪（快照尚未完成），请先刷新");
        }

        var resolveError = TryResolveRepo(index, parameters, out var repo);
        if (resolveError is not null)
        {
            return CommandResult.Failure(resolveError);
        }

        if (!repo.Repo.IsLocal || string.IsNullOrWhiteSpace(repo.Repo.LocalPath))
        {
            return CommandResult.Failure(
                BridgeErrorCodes.RepoNotLocal,
                $"仓库未克隆：{RepoResolver.RepoName(repo)}",
                new Dictionary<string, object?>
                {
                    ["repoType"] = repo.Repo.RepoType,
                    ["componentId"] = repo.ComponentId,
                    ["expectedPath"] = repo.Repo.LocalPath
                });
        }

        var repoRoot = PathPolicy.TryCanonicalize(repo.Repo.LocalPath);
        if (repoRoot is null || !_paths.IsWithin(_paths.Root, repoRoot))
        {
            return CommandResult.Failure(
                BridgeErrorCodes.PathOutsideRoot,
                "仓库路径越出工作区根",
                new Dictionary<string, object?> { ["repoType"] = repo.Repo.RepoType });
        }

        if (!Directory.Exists(repoRoot))
        {
            return CommandResult.Failure(
                BridgeErrorCodes.RepoNotLocal,
                $"仓库本地目录不存在：{RepoResolver.RepoName(repo)}",
                new Dictionary<string, object?> { ["repoType"] = repo.Repo.RepoType });
        }

        var relativePath = PathPolicy.NormalizeRelative(parameters.Path ?? parameters.RelativePath);
        if (!_paths.TryResolveRepoPath(repoRoot, relativePath, out var absoluteDirectory, out var pathError))
        {
            return CommandResult.Failure(pathError!);
        }

        var depth = Math.Clamp(parameters.Depth ?? DefaultDepth, 0, MaxDepth);
        var limit = Math.Clamp(parameters.Limit ?? DefaultLimit, 1, DefaultLimit);
        var includeHidden = parameters.IncludeHidden ?? true;
        var includeGitStatus = parameters.IncludeGitStatus ?? true;
        var includeIgnored = parameters.IncludeIgnored ?? false;

        var submodules = await _annotations.GetSubmodulesAsync(repoRoot, cancellationToken).ConfigureAwait(false);
        if (relativePath.Length > 0 && IsInsideSubmodule(submodules, relativePath))
        {
            return CommandResult.Failure(
                BridgeErrorCodes.Submodule,
                "submodule 内部禁止展开（未纳管）",
                new Dictionary<string, object?> { ["path"] = relativePath });
        }

        if (relativePath.Length > 0)
        {
            var ancestorReparse = _paths.CheckReparseChain(absoluteDirectory);
            if (ancestorReparse.HasReparse || PathPolicy.IsReparsePoint(absoluteDirectory))
            {
                return CommandResult.Failure(
                    BridgeErrorCodes.ReparsePoint,
                    "reparse point/junction 不允许展开（树中仅展示）",
                    new Dictionary<string, object?> { ["path"] = relativePath });
            }
        }

        var state = repo.Repo.State.ToUpperInvariant();
        var cacheKey = string.Create(
            CultureInfo.InvariantCulture,
            $"{snapshot.GeneratedAt}|{repo.ComponentId}|{repo.Repo.RepoType}|{relativePath}|{depth}|{limit}|{includeHidden}|{includeGitStatus}|{includeIgnored}|{state}");

        var stopwatch = Stopwatch.StartNew();
        DirectoryPage page;
        if (!_pageCache.TryGet(cacheKey, out page!))
        {
            IReadOnlyDictionary<string, string> statuses = new Dictionary<string, string>(StringComparer.Ordinal);
            if (includeGitStatus && !string.Equals(state, "INVALID", StringComparison.Ordinal))
            {
                statuses = await _annotations.GetStatusAsync(repoRoot, cancellationToken).ConfigureAwait(false);
            }

            var annotation = new TreeAnnotationProvider(
                GitStatus: path => statuses.TryGetValue(path, out var status) ? status : GitStatusIndex.StatusNone,
                Submodule: path => submodules.Contains(path));

            page = _reader.Read(
                absoluteDirectory,
                relativePath,
                new TreeReadOptions(
                    Depth: depth,
                    Limit: limit,
                    IncludeGitStatus: includeGitStatus,
                    IncludeIgnored: includeIgnored,
                    IncludeHidden: includeHidden),
                cancellationToken,
                annotation);

            if (includeIgnored && page.Entries.Count > 0)
            {
                var files = page.Entries
                    .Where(entry => string.Equals(entry.Kind, "FILE", StringComparison.Ordinal))
                    .Select(entry => entry.Path)
                    .ToList();
                if (files.Count > 0)
                {
                    var ignored = await _annotations
                        .CheckIgnoredAsync(repoRoot, files, cancellationToken)
                        .ConfigureAwait(false);
                    if (ignored.Count > 0)
                    {
                        page = page with { Entries = ApplyIgnored(page.Entries, ignored) };
                    }
                }
            }

            _pageCache.Set(cacheKey, page);
        }

        stopwatch.Stop();
        var errorCode = page.Warnings.Count > 0 ? page.Warnings[0] : null;
        if (errorCode is not null)
        {
            HostLog.Warn($"getRepoTree 降级：{repo.ComponentId}_{repo.Repo.RepoType} path={relativePath} code={errorCode}");
        }

        var tree = new RepoTreeDto(
            ComponentId: repo.ComponentId,
            RepoType: repo.Repo.RepoType,
            RepoName: RepoResolver.RepoName(repo),
            RootPath: repoRoot,
            Path: relativePath,
            Branch: repo.Repo.Branch,
            HeadSha: repo.Repo.HeadSha,
            State: repo.Repo.State,
            DirtyCount: repo.Repo.DirtyCount,
            CompareRef: repo.Repo.CompareRef ?? repo.Repo.UpstreamRef,
            FetchedAt: snapshot.GeneratedAt,
            ReadAt: DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            Entries: page.Entries,
            Truncated: page.Truncated,
            TotalEntries: page.TotalEntries,
            Error: errorCode,
            Limit: limit,
            NextCursor: null);

        HostLog.Info(
            $"api getRepoTree repo={repo.ComponentId}_{repo.Repo.RepoType} path={relativePath} entries={page.Entries.Count} "
            + $"truncated={page.Truncated} ms={stopwatch.ElapsedMilliseconds}");
        return CommandResult.Success(tree);
    }

    /// <summary>清空页缓存（refresh 后）。</summary>
    public void InvalidateCaches() => _pageCache.InvalidateAll();

    private BridgeError? TryResolveRepo(SnapshotIndex index, GetRepoTreeParams parameters, out RepoRef repo)
    {
        if (!string.IsNullOrWhiteSpace(parameters.ComponentId) || !string.IsNullOrWhiteSpace(parameters.RepoType))
        {
            return RepoResolver.TryResolve(index, parameters.ComponentId, parameters.RepoType, out repo);
        }

        if (!string.IsNullOrWhiteSpace(parameters.RepoId))
        {
            var repoId = parameters.RepoId.Trim();
            var separator = repoId.LastIndexOf('_');
            if (separator <= 0 || separator == repoId.Length - 1)
            {
                repo = null!;
                return new BridgeError(
                    BridgeErrorCodes.InvalidArgument,
                    "参数 repoId 必须形如 {componentId}_{REPO_TYPE}",
                    new Dictionary<string, object?> { ["field"] = "repoId" });
            }

            return RepoResolver.TryResolve(index, repoId[..separator], repoId[(separator + 1)..], out repo);
        }

        if (!string.IsNullOrWhiteSpace(parameters.RepoPath))
        {
            if (!_paths.TryResolveWorkspacePath(parameters.RepoPath, out var absolutePath, out var pathError))
            {
                repo = null!;
                return pathError;
            }

            if (!index.TryGetRepoByPath(absolutePath, out repo))
            {
                return new BridgeError(
                    BridgeErrorCodes.NotFound,
                    "repoPath 未匹配快照中的本地仓",
                    new Dictionary<string, object?> { ["repoPath"] = parameters.RepoPath });
            }

            return null;
        }

        repo = null!;
        return new BridgeError(
            BridgeErrorCodes.InvalidArgument,
            "缺少参数：componentId+repoType（或 repoId/repoPath 别名）",
            new Dictionary<string, object?> { ["field"] = "componentId" });
    }

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

    private static IReadOnlyList<TreeNodeDto> ApplyIgnored(
        IReadOnlyList<TreeNodeDto> entries,
        IReadOnlySet<string> ignored)
    {
        var result = new List<TreeNodeDto>(entries.Count);
        foreach (var entry in entries)
        {
            var updated = ignored.Contains(entry.Path) ? entry with { Ignored = true } : entry;
            if (updated.Children is { Count: > 0 })
            {
                updated = updated with { Children = ApplyIgnored(updated.Children, ignored) };
            }

            result.Add(updated);
        }

        return result;
    }
}
