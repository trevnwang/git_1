namespace MbdGitTool.Core.Domain;

/// <summary>状态计算选项（对应附录 D 的 <c>dirty.includeUntracked</c> 等设置）。</summary>
public sealed record RepoStateOptions
{
    /// <summary>DIRTY 是否计入 untracked（D-021 默认 false）。</summary>
    public bool DirtyIncludeUntracked { get; init; }

    /// <summary>DIRTY 是否计入 stash（默认 false）。</summary>
    public bool DirtyIncludeStash { get; init; }

    public static RepoStateOptions Default { get; } = new();
}

/// <summary>单仓状态判定结果（纯函数输出：主状态 + NO_UPSTREAM 子型 + 正交标记 + 展示用计数）。</summary>
public sealed record RepoEvaluation(
    RepoState State,
    NoUpstreamSubType SubType,
    RepoFlags Flags,
    int Ahead,
    int Behind);

/// <summary>
/// 单仓主状态判定（SRS §7.2 的 14 态 + §7.3 标记）。
/// <para>严格按 7.2 优先级：UNKNOWN(0) → INVALID(1) → MISSING(2) → EMPTY(3) → UNMERGED(4) → IN_PROGRESS(5)</para>
/// <para>→ DETACHED_TAG(6) → DETACHED_COMMIT(7) → DIVERGED(8) → BEHIND(9) → AHEAD(10) → NO_UPSTREAM(11)</para>
/// <para>→ DIRTY(12) → SYNCED(13)；无法判定一律 UNKNOWN（禁止假绿）。</para>
/// <para>判定互斥穷尽：所有输入都有唯一落点，未命中组合落到 UNKNOWN。</para>
/// </summary>
public static class RepoStateEvaluator
{
    /// <summary>根据单仓事实判定主状态。纯函数，可表驱动测试。</summary>
    public static RepoEvaluation Evaluate(RepoFacts facts, RepoStateOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(facts);
        options ??= RepoStateOptions.Default;

        var flags = CollectFlags(facts);
        var ahead = facts.Ahead ?? 0;
        var behind = facts.Behind ?? 0;

        // §7.3 stale：主状态强制 UNKNOWN（UI 斜纹）。
        if (facts.Stale)
        {
            return Result(facts, RepoState.Unknown, NoUpstreamSubType.None, flags | RepoFlags.Stale);
        }

        // 扫描/git 调用失败：UNKNOWN + error（单个仓失败不中断整体）。
        if (facts.Error is not null)
        {
            return Result(facts, RepoState.Unknown, NoUpstreamSubType.None, flags);
        }

        // §7.2 优先级 2：目录不存在或为空 → MISSING。
        if (!facts.Exists)
        {
            return Result(facts, RepoState.Missing, NoUpstreamSubType.None, flags);
        }

        // §7.2 优先级 1：目录不是 git 仓 / top-level 不匹配 / origin 不匹配 → INVALID。
        if (!facts.IsValidRepo)
        {
            return Result(facts, RepoState.Invalid, NoUpstreamSubType.None, flags);
        }

        // §7.2 优先级 0 的远程事实：远程仓已删除时关系不可判 → UNKNOWN + remote_gone（附录 C F-REMOTE-GONE）。
        if (facts.RemoteGone)
        {
            return Result(facts, RepoState.Unknown, NoUpstreamSubType.None, flags);
        }

        // §7.2 优先级 3：HEAD unborn → EMPTY。
        if (facts.HeadKind == HeadKind.Unborn)
        {
            return Result(facts, RepoState.Empty, NoUpstreamSubType.None, flags);
        }

        // §7.2 优先级 4/5：index 冲突优先于进行中操作。
        if (facts.Unmerged)
        {
            return Result(facts, RepoState.Unmerged, NoUpstreamSubType.None, flags);
        }

        if (facts.InProgress)
        {
            return Result(facts, RepoState.InProgress, NoUpstreamSubType.None, flags);
        }

        // §7.2 优先级 6/7：detached HEAD（有 tag → DETACHED_TAG，无 tag → DETACHED_COMMIT）。
        if (facts.HeadKind == HeadKind.DetachedTag)
        {
            return Result(facts, RepoState.DetachedTag, NoUpstreamSubType.None, flags);
        }

        if (facts.HeadKind == HeadKind.DetachedCommit)
        {
            return Result(facts, RepoState.DetachedCommit, NoUpstreamSubType.None, flags);
        }

        // §7.2 优先级 8–10：关系类状态（需要 compare_ref 在 TTL 内）。
        var hasCompareRef = facts.UpstreamConfigured || facts.RemoteBranchExists;
        var relationKnown = facts.Ahead.HasValue && facts.Behind.HasValue;

        if (hasCompareRef && relationKnown)
        {
            if (ahead > 0 && behind > 0)
            {
                return Result(facts, RepoState.Diverged, NoUpstreamSubType.None, flags);
            }

            if (behind > 0)
            {
                return Result(facts, RepoState.Behind, NoUpstreamSubType.None, flags);
            }

            if (ahead > 0)
            {
                return Result(facts, RepoState.Ahead, NoUpstreamSubType.None, flags);
            }

            // 两侧无差异：有 upstream → SYNCED/DIRTY；无 upstream 但远程同名分支存在 → NO_UPSTREAM(UNSET_BUT_EXISTS)。
            if (facts.UpstreamConfigured)
            {
                var dirty = facts.DirtyTracked
                    || (options.DirtyIncludeUntracked && facts.UntrackedCount > 0)
                    || (options.DirtyIncludeStash && facts.Stashed);

                return Result(facts, dirty ? RepoState.Dirty : RepoState.Synced, NoUpstreamSubType.None, flags);
            }

            return Result(facts, RepoState.NoUpstream, NoUpstreamSubType.UnsetButExists, flags);
        }

        // §7.2 优先级 11：NO_UPSTREAM 三子型（本地事实可判定，不因关系数据缺失压成 UNKNOWN）。
        if (facts.UpstreamConfigured)
        {
            if (facts.UpstreamGone)
            {
                return Result(facts, RepoState.NoUpstream, NoUpstreamSubType.UpstreamGone, flags);
            }

            // 配置了 upstream 但 ref/计数不可用（如 shallow、对象缺失）→ 关系不可判。
            return Result(facts, RepoState.Unknown, NoUpstreamSubType.None, flags);
        }

        if (facts.RemoteBranchExists)
        {
            return Result(facts, RepoState.NoUpstream, NoUpstreamSubType.UnsetButExists, flags);
        }

        return Result(facts, RepoState.NoUpstream, NoUpstreamSubType.LocalOnly, flags);
    }

    private static RepoEvaluation Result(
        RepoFacts facts,
        RepoState state,
        NoUpstreamSubType subType,
        RepoFlags flags) =>
        new(state, subType, flags, facts.Ahead ?? 0, facts.Behind ?? 0);

    private static RepoFlags CollectFlags(RepoFacts facts)
    {
        var flags = RepoFlags.None;

        if (facts.DirtyTracked || facts.DirtyCount > 0)
        {
            flags |= RepoFlags.DirtyTracked;
        }

        if (facts.UntrackedCount > 0)
        {
            flags |= RepoFlags.Untracked;
        }

        if (facts.Stashed)
        {
            flags |= RepoFlags.Stashed;
        }

        if (facts.Shallow)
        {
            flags |= RepoFlags.Shallow;
        }

        if (facts.LfsMissing)
        {
            flags |= RepoFlags.LfsMissing;
        }

        if (facts.Busy)
        {
            flags |= RepoFlags.Busy;
        }

        if (facts.RemoteGone)
        {
            flags |= RepoFlags.RemoteGone;
        }

        if (facts.TagMoved)
        {
            flags |= RepoFlags.TagMoved;
        }

        if (facts.Stale)
        {
            flags |= RepoFlags.Stale;
        }

        if (facts.IsReparsePoint)
        {
            flags |= RepoFlags.ReparsePoint;
        }

        if (facts.IsSubmodule)
        {
            flags |= RepoFlags.Submodule;
        }

        return flags;
    }
}
