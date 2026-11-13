using MbdGitTool.Core.Domain;

namespace MbdGitTool.Core.Domain;

/// <summary>单仓快照（状态计算的输入；不可变）。</summary>
public sealed record RepoSnapshot(
    string RepoId,
    string ComponentId,
    RepoType RepoType,
    RepoState State,
    NoUpstreamSubType NoUpstream = NoUpstreamSubType.None,
    RepoFlags Flags = RepoFlags.None);

/// <summary>组件聚合输入：四仓（或实际存在的仓）+ 组件级事实。</summary>
public sealed record ComponentSnapshot(
    string ComponentId,
    IReadOnlyList<RepoSnapshot> Repos,
    BindingCause BindingCause = BindingCause.None,
    bool TagsConsistent = true,
    bool MixedBranch = false,
    bool OperationPartialFailed = false);

/// <summary>
/// 组件聚合（SRS §7.4 全序 + UNKNOWN 兜底 + BINDING_ERROR 成因分类）。
/// 纯函数：无 IO、无时钟、可表驱动测试。
/// </summary>
public static class ComponentAggregation
{
    /// <summary>按 §7.4 聚合优先级返回组件状态。</summary>
    public static ComponentState Evaluate(ComponentSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (snapshot.OperationPartialFailed)
            return ComponentState.OpPartialFailed;

        if (snapshot.BindingCause != BindingCause.None)
            return ComponentState.BindingError;

        var repos = snapshot.Repos;
        if (repos.Count == 0)
            return ComponentState.Unknown;

        // 任一仓 UNKNOWN/STALE ⇒ 组件不得 SYNCED；按优先级 2 返回。
        if (repos.Any(r => r.State == RepoState.Unknown || r.Flags.HasFlag(RepoFlags.Stale)))
            return ComponentState.Unknown;

        if (repos.Any(r => r.State == RepoState.Missing))
            return ComponentState.Missing;

        if (repos.Any(r => r.State is RepoState.Unmerged or RepoState.InProgress))
            return ComponentState.BlockedLocal;

        if (repos.Any(r => r.State == RepoState.Empty))
            return ComponentState.Empty;

        if (!snapshot.TagsConsistent)
            return ComponentState.TagDrift;

        if (repos.Any(r => r.State == RepoState.Diverged))
            return ComponentState.Diverged;

        if (repos.Any(r => r.State == RepoState.Behind))
            return ComponentState.Behind;

        if (repos.Any(r => r.State == RepoState.Ahead))
            return ComponentState.Ahead;

        if (repos.Any(r => r.State is RepoState.DetachedTag or RepoState.DetachedCommit))
            return ComponentState.Detached;

        if (snapshot.MixedBranch)
            return ComponentState.MixedBranch;

        if (repos.Any(r => r.State == RepoState.NoUpstream))
            return ComponentState.NewBranch;

        if (repos.Any(r => r.State == RepoState.Dirty))
            return ComponentState.Dirty;

        if (repos.All(r => r.State == RepoState.Synced))
            return ComponentState.Synced;

        // 未命中任何规则：必须落到 UNKNOWN，禁止空白/假绿（§7.4）。
        return ComponentState.Unknown;
    }

    /// <summary>
    /// severity_rank（SRS D-020/§7.4，数值越小越严重；用于看板默认排序）。
    /// BINDING_ERROR 按成因：INVALID/AMBIGUOUS=1，仅缺仓=12。
    /// </summary>
    public static int SeverityRank(ComponentSnapshot snapshot, ComponentState state)
    {
        if (state == ComponentState.BindingError)
            return snapshot.BindingCause == BindingCause.Incomplete ? 12 : 1;

        return state switch
        {
            ComponentState.OpPartialFailed => 0,
            ComponentState.Unknown => 2,
            ComponentState.BlockedLocal => 3,
            ComponentState.Diverged => 4,
            ComponentState.TagDrift => 5,
            ComponentState.Behind => 6,
            ComponentState.Ahead => 7,
            ComponentState.Dirty => 8,
            ComponentState.MixedBranch => 9,
            ComponentState.Detached => 10,
            ComponentState.NewBranch => 11,
            ComponentState.Missing => 12,
            ComponentState.Empty => 13,
            ComponentState.Synced => 14,
            _ => 2
        };
    }
}
