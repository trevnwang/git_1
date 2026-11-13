using System;
using System.Collections.Generic;
using System.IO;
using MbdGitTool.Core.Contracts;

namespace MbdGitTool.Core.Domain;

/// <summary>快照中的一个仓引用（组件 DTO + 仓 DTO）。</summary>
public sealed record RepoRef(string ComponentId, ComponentDto Component, RepoDto Repo);

/// <summary>
/// 快照索引（code-plan §4.2）：组件按 ID、仓按 <c>componentId|repoType</c> 与 canonical localPath 双索引，
/// 供 getRepoTree/getRepoDetail/getCommits/getComponentDetail/openPath 只读复用，避免 O(n) 查找。
/// </summary>
public sealed class SnapshotIndex
{
    public static SnapshotIndex Empty { get; } = new(null);

    private readonly Dictionary<string, ComponentDto> _components;
    private readonly Dictionary<string, RepoRef> _reposByKey;
    private readonly Dictionary<string, RepoRef> _reposByPath;

    private SnapshotIndex(SnapshotDto? snapshot)
    {
        Snapshot = snapshot;
        _components = new Dictionary<string, ComponentDto>(StringComparer.OrdinalIgnoreCase);
        _reposByKey = new Dictionary<string, RepoRef>(StringComparer.OrdinalIgnoreCase);
        _reposByPath = new Dictionary<string, RepoRef>(StringComparer.OrdinalIgnoreCase);

        if (snapshot is null)
        {
            return;
        }

        foreach (var component in snapshot.Components)
        {
            _components[component.ComponentId] = component;

            foreach (var repo in component.Repos)
            {
                var repoRef = new RepoRef(component.ComponentId, component, repo);
                _reposByKey[Key(component.ComponentId, repo.RepoType)] = repoRef;

                var pathKey = NormalizePath(repo.LocalPath);
                if (pathKey is not null)
                {
                    _reposByPath[pathKey] = repoRef;
                }
            }
        }
    }

    public SnapshotDto? Snapshot { get; }

    public string? WorkspaceRoot => Snapshot?.WorkspaceRoot;

    public string? GeneratedAt => Snapshot?.GeneratedAt;

    public IReadOnlyList<ComponentDto> Components => Snapshot?.Components ?? Array.Empty<ComponentDto>();

    public static SnapshotIndex Build(SnapshotDto? snapshot) => snapshot is null ? Empty : new SnapshotIndex(snapshot);

    public bool TryGetComponent(string? componentId, out ComponentDto component)
    {
        if (!string.IsNullOrWhiteSpace(componentId) && _components.TryGetValue(componentId, out var found))
        {
            component = found;
            return true;
        }

        component = null!;
        return false;
    }

    public bool TryGetRepo(string? componentId, string? repoType, out RepoRef repo)
    {
        if (!string.IsNullOrWhiteSpace(componentId)
            && !string.IsNullOrWhiteSpace(repoType)
            && _reposByKey.TryGetValue(Key(componentId, repoType), out var found))
        {
            repo = found;
            return true;
        }

        repo = null!;
        return false;
    }

    public bool TryGetRepoByPath(string? localPath, out RepoRef repo)
    {
        var pathKey = NormalizePath(localPath);
        if (pathKey is not null && _reposByPath.TryGetValue(pathKey, out var found))
        {
            repo = found;
            return true;
        }

        repo = null!;
        return false;
    }

    private static string Key(string componentId, string repoType) =>
        componentId.Trim() + "|" + repoType.Trim().ToUpperInvariant();

    private static string? NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path.Trim()));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or IOException)
        {
            return null;
        }
    }
}
