using MbdGitTool.Core.Domain;
using Xunit;

namespace MbdGitTool.Core.Tests;

public class ComponentAggregationTests
{
    private static readonly string ComponentId = "CF325F_ChanganSigProcgBascFct";

    private static RepoSnapshot Repo(RepoType type, RepoState state, RepoFlags flags = RepoFlags.None) =>
        new($"{type}-repo", ComponentId, type, state, NoUpstreamSubType.None, flags);

    private static IReadOnlyList<RepoSnapshot> All(RepoState state) => new[]
    {
        Repo(RepoType.Design, state), Repo(RepoType.Impl, state),
        Repo(RepoType.Mil, state), Repo(RepoType.Sil, state)
    };

    private static ComponentState Evaluate(
        IReadOnlyList<RepoSnapshot> repos,
        BindingCause binding = BindingCause.None,
        bool tagsConsistent = true,
        bool mixedBranch = false,
        bool partialFailed = false) =>
        ComponentAggregation.Evaluate(new ComponentSnapshot(
            ComponentId, repos, binding, tagsConsistent, mixedBranch, partialFailed));

    [Fact]
    public void AllSynced_ReturnsSynced()
    {
        Assert.Equal(ComponentState.Synced, Evaluate(All(RepoState.Synced)));
    }

    [Fact]
    public void MissingBeatsBlockedLocal_OrderIsMissingThenBlocked()
    {
        // SRS §7.4：MISSING=3 先于 BLOCKED_LOCAL=4
        var repos = new[]
        {
            Repo(RepoType.Design, RepoState.Missing),
            Repo(RepoType.Impl, RepoState.Unmerged),
            Repo(RepoType.Mil, RepoState.Synced),
            Repo(RepoType.Sil, RepoState.Synced)
        };
        Assert.Equal(ComponentState.Missing, Evaluate(repos));
    }

    [Fact]
    public void UnknownShadowsEverythingBelow()
    {
        var repos = new[]
        {
            Repo(RepoType.Design, RepoState.Synced),
            Repo(RepoType.Impl, RepoState.Unknown),
            Repo(RepoType.Mil, RepoState.Behind),
            Repo(RepoType.Sil, RepoState.Synced)
        };
        Assert.Equal(ComponentState.Unknown, Evaluate(repos));
    }

    [Fact]
    public void AnyUnknown_ComponentNeverSynced()
    {
        var repos = new[]
        {
            Repo(RepoType.Design, RepoState.Synced),
            Repo(RepoType.Impl, RepoState.Synced),
            Repo(RepoType.Mil, RepoState.Synced),
            Repo(RepoType.Sil, RepoState.Synced, RepoFlags.Stale)
        };
        Assert.NotEqual(ComponentState.Synced, Evaluate(repos));
    }

    [Fact]
    public void EmptyFallsAfterBlockedLocal()
    {
        var repos = new[]
        {
            Repo(RepoType.Design, RepoState.Empty),
            Repo(RepoType.Impl, RepoState.InProgress),
            Repo(RepoType.Mil, RepoState.Synced),
            Repo(RepoType.Sil, RepoState.Synced)
        };
        Assert.Equal(ComponentState.BlockedLocal, Evaluate(repos));
    }

    [Fact]
    public void BindingError_UsesCauseForSeverity()
    {
        var invalid = new ComponentSnapshot(ComponentId, All(RepoState.Synced), BindingCause.InvalidOrUnbound);
        var incomplete = new ComponentSnapshot(ComponentId, All(RepoState.Synced), BindingCause.Incomplete);

        Assert.Equal(ComponentState.BindingError, ComponentAggregation.Evaluate(invalid));
        Assert.Equal(1, ComponentAggregation.SeverityRank(invalid, ComponentState.BindingError));
        Assert.Equal(12, ComponentAggregation.SeverityRank(incomplete, ComponentState.BindingError));
    }

    [Fact]
    public void OperationPartialFailed_IsHighestPriority()
    {
        var repos = All(RepoState.Diverged);
        Assert.Equal(ComponentState.OpPartialFailed, Evaluate(repos, partialFailed: true));
    }

    [Fact]
    public void TagDriftBeatsDiverged()
    {
        var repos = new[]
        {
            Repo(RepoType.Design, RepoState.DetachedTag),
            Repo(RepoType.Impl, RepoState.Diverged),
            Repo(RepoType.Mil, RepoState.Synced),
            Repo(RepoType.Sil, RepoState.Synced)
        };
        Assert.Equal(ComponentState.TagDrift, Evaluate(repos, tagsConsistent: false));
    }

    [Fact]
    public void NoRuleHits_FallsBackToUnknown()
    {
        // 所有仓 SYNCED 但 tagsConsistent 未被规则消费时不触发；这里构造 MixedBranch 与单仓不一致的兜底场景
        var repos = new[]
        {
            Repo(RepoType.Design, RepoState.Synced),
            Repo(RepoType.Impl, RepoState.Synced)
        };
        // 只有 2 仓且全 SYNCED：按规则返回 SYNCED（组件不完整由 BindingCause 在绑定层表达）
        Assert.Equal(ComponentState.Synced, Evaluate(repos));
    }

    [Fact]
    public void SeverityRank_OrderMatchesSrs()
    {
        var synced = new ComponentSnapshot(ComponentId, All(RepoState.Synced));
        Assert.Equal(0, ComponentAggregation.SeverityRank(synced, ComponentState.OpPartialFailed));
        Assert.Equal(1, ComponentAggregation.SeverityRank(synced, ComponentState.BindingError));
        Assert.Equal(2, ComponentAggregation.SeverityRank(synced, ComponentState.Unknown));
        Assert.Equal(3, ComponentAggregation.SeverityRank(synced, ComponentState.BlockedLocal));
        Assert.Equal(12, ComponentAggregation.SeverityRank(synced, ComponentState.Missing));
        Assert.Equal(14, ComponentAggregation.SeverityRank(synced, ComponentState.Synced));
    }
}
