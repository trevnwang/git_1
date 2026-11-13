using System;
using System.Collections.Generic;
using System.IO;
using MbdGitTool.Core.Contracts;
using MbdGitTool.Core.Domain;

namespace MbdGitTool.Host.Services;

/// <summary>组件/仓参数解析与校验（componentId+repoType 唯一事实源；四仓固定顺序）。</summary>
public static class RepoResolver
{
    public static readonly IReadOnlyList<string> RepoTypes = new[] { "DESIGN", "IMPL", "MIL", "SIL" };

    /// <summary>repoType 规范化为 DESIGN/IMPL/MIL/SIL（大小写不敏感）；其他值返回 false。</summary>
    public static bool TryNormalizeRepoType(string? value, out string repoType)
    {
        repoType = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var upper = value.Trim().ToUpperInvariant();
        foreach (var known in RepoTypes)
        {
            if (known == upper)
            {
                repoType = known;
                return true;
            }
        }

        return false;
    }

    /// <summary>解析 componentId+repoType → 仓引用；失败返回可直接回给 UI 的错误。</summary>
    public static BridgeError? TryResolve(
        SnapshotIndex index,
        string? componentId,
        string? repoType,
        out RepoRef repo)
    {
        repo = null!;

        if (string.IsNullOrWhiteSpace(componentId))
        {
            return new BridgeError(
                BridgeErrorCodes.InvalidArgument,
                "缺少必填参数 componentId",
                new Dictionary<string, object?> { ["field"] = "componentId" });
        }

        if (!TryNormalizeRepoType(repoType, out var normalizedType))
        {
            return new BridgeError(
                BridgeErrorCodes.InvalidArgument,
                "参数 repoType 必须是 DESIGN/IMPL/MIL/SIL",
                new Dictionary<string, object?>
                {
                    ["field"] = "repoType",
                    ["repoType"] = repoType
                });
        }

        if (!index.TryGetRepo(componentId, normalizedType, out repo))
        {
            return new BridgeError(
                BridgeErrorCodes.NotFound,
                $"未找到仓库：{componentId.Trim()}_{normalizedType}",
                new Dictionary<string, object?>
                {
                    ["componentId"] = componentId,
                    ["repoType"] = normalizedType
                });
        }

        return null;
    }

    /// <summary>仓库展示名：优先快照 repoName，其次本地目录名，最后 componentId_Type。</summary>
    public static string RepoName(RepoRef repo)
    {
        if (!string.IsNullOrWhiteSpace(repo.Repo.RepoName))
        {
            return repo.Repo.RepoName;
        }

        if (!string.IsNullOrWhiteSpace(repo.Repo.LocalPath))
        {
            var name = Path.GetFileName(repo.Repo.LocalPath.TrimEnd('\\', '/'));
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
        }

        return repo.ComponentId + "_" + ToDisplayType(repo.Repo.RepoType);
    }

    private static string ToDisplayType(string repoType) => repoType.ToUpperInvariant() switch
    {
        "DESIGN" => "Design",
        "IMPL" => "Impl",
        "MIL" => "MIL",
        "SIL" => "SIL",
        _ => repoType
    };
}
