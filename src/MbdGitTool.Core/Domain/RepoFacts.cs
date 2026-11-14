namespace MbdGitTool.Core.Domain;

/// <summary>HEAD 形态（SRS §7.2 / §8.1 <c>local_state.head_type</c>）。</summary>
public enum HeadKind
{
    Branch = 0,
    DetachedTag = 1,
    DetachedCommit = 2,
    Unborn = 3
}

/// <summary>
/// 单仓事实（只读扫描结果）。
/// <para>职责边界：<see cref="RepoStateEvaluator"/> 只根据事实判定状态，不做任何 IO；</para>
/// <para>关系类字段 <see cref="Ahead"/>/<see cref="Behind"/> 为 <c>null</c> 表示 compare_ref 不可用或关系不可判定（SRS §7.1）。</para>
/// </summary>
public sealed record RepoFacts
{
    /// <summary>本地目录（缺失仓为期望路径或 null）。</summary>
    public string? LocalPath { get; init; }

    /// <summary>本地目录是否存在（MISSING 判定）。</summary>
    public bool Exists { get; init; } = true;

    /// <summary>是否为合法 git 仓：--show-toplevel 匹配、origin 存在且与 Gitea 同名仓一致（INVALID 判定）。</summary>
    public bool IsValidRepo { get; init; } = true;

    public HeadKind HeadKind { get; init; } = HeadKind.Branch;

    /// <summary>当前分支名；detached/unborn 时可能为 null 或 unborn 分支名。</summary>
    public string? Branch { get; init; }

    public string? HeadSha { get; init; }

    /// <summary>已配置 upstream 的完整 ref（如 refs/remotes/origin/main）；未配置为 null。</summary>
    public string? UpstreamRef { get; init; }

    /// <summary>本地分支是否配置了 upstream（tracking）。</summary>
    public bool UpstreamConfigured { get; init; }

    /// <summary>远程存在与本地当前分支同名的分支（用于 NO_UPSTREAM 子型判定）。</summary>
    public bool RemoteBranchExists { get; init; }

    /// <summary>已配置 upstream 但远程跟踪分支已不存在（UPSTREAM_GONE）。</summary>
    public bool UpstreamGone { get; init; }

    /// <summary>本地领先 compare_ref 的提交数；null = 关系不可判定。</summary>
    public int? Ahead { get; init; }

    /// <summary>本地落后 compare_ref 的提交数；null = 关系不可判定。</summary>
    public int? Behind { get; init; }

    /// <summary>tracked 文件改动（工作区或 index），SRS §7.3 <c>dirty_tracked</c>。</summary>
    public bool DirtyTracked { get; init; }

    public int DirtyCount { get; init; }

    public int UntrackedCount { get; init; }

    public int StagedCount { get; init; }

    public bool Stashed { get; init; }

    /// <summary>index 存在未解决冲突（git ls-files -u 非空）。</summary>
    public bool Unmerged { get; init; }

    /// <summary>存在 MERGE_HEAD / rebase-merge / rebase-apply / CHERRY_PICK_HEAD / BISECT_LOG 等标记。</summary>
    public bool InProgress { get; init; }

    public bool Shallow { get; init; }

    public bool LfsMissing { get; init; }

    /// <summary><c>.git/index.lock</c> 存在或外部 git 正在操作（写操作 BUSY；扫描本身不判失败）。</summary>
    public bool Busy { get; init; }

    /// <summary>Gitea 已删除该仓（远程清单里不存在该类型）。</summary>
    public bool RemoteGone { get; init; }

    /// <summary>本地/远程同名 tag 指向不同 commit（M1 未 fetch 时无法判定，保留字段）。</summary>
    public bool TagMoved { get; init; }

    /// <summary>指向 HEAD 的 tag 名（detached 时用于区分 DETACHED_TAG / DETACHED_COMMIT）。</summary>
    public IReadOnlyList<string> TagsAtHead { get; init; } = Array.Empty<string>();

    /// <summary>本地全部 tag 名（组件级 TAG_DRIFT 判定用，SRS §7.4/FR-STA-06）。</summary>
    public IReadOnlyList<string> LocalTags { get; init; } = Array.Empty<string>();

    public bool IsReparsePoint { get; init; }

    public bool IsSubmodule { get; init; }

    /// <summary>数据超 TTL 或本轮扫描失败（主状态强制 UNKNOWN，SRS §7.3）。</summary>
    public bool Stale { get; init; }

    /// <summary>扫描失败原因（非 null 时主状态按 UNKNOWN 处理，单个仓失败不中断整体）。</summary>
    public string? Error { get; init; }

    /// <summary>本地目录存在的“缺失仓”事实（远程有仓、本地未克隆）。</summary>
    public static RepoFacts Missing(string? localPath = null) =>
        new() { Exists = false, IsValidRepo = false, LocalPath = localPath };

    /// <summary>扫描失败事实（主状态 UNKNOWN）。</summary>
    public static RepoFacts Failed(string error, string? localPath = null) =>
        new() { LocalPath = localPath, IsValidRepo = false, Error = error };
}

/// <summary>SRS §7.3 正交标记 → UI/JSON 用的 snake_case 名称（固定顺序）。</summary>
public static class RepoFlagsExtensions
{
    private static readonly (RepoFlags Flag, string WireName)[] WireOrder =
    {
        (RepoFlags.DirtyTracked, "dirty_tracked"),
        (RepoFlags.Untracked, "untracked"),
        (RepoFlags.Stashed, "stashed"),
        (RepoFlags.Shallow, "shallow"),
        (RepoFlags.LfsMissing, "lfs_missing"),
        (RepoFlags.ReparsePoint, "reparse_point"),
        (RepoFlags.Stale, "stale"),
        (RepoFlags.Busy, "busy"),
        (RepoFlags.RemoteGone, "remote_gone"),
        (RepoFlags.TagMoved, "tag_moved"),
        (RepoFlags.Submodule, "submodule")
    };

    /// <summary>把标记位转换为 SRS 枚举名的有序列表。</summary>
    public static IReadOnlyList<string> ToWireNames(this RepoFlags flags)
    {
        var names = new List<string>();
        foreach (var (flag, wireName) in WireOrder)
        {
            if (flags.HasFlag(flag))
            {
                names.Add(wireName);
            }
        }

        return names;
    }
}
