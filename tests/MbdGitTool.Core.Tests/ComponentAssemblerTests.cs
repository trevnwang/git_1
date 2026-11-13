using MbdGitTool.Core.Contracts;
using MbdGitTool.Core.Domain;

namespace MbdGitTool.Core.Tests;

public class ComponentAssemblerTests
{
    private const string ComponentId = "CF325F_ChanganSigProcgBascFct";
    private const string Sha = "0123456789abcdef0123456789abcdef01234567";

    private static RepoFacts Synced(string branch = "main") => new()
    {
        HeadKind = HeadKind.Branch,
        Branch = branch,
        HeadSha = Sha,
        UpstreamConfigured = true,
        UpstreamRef = $"refs/remotes/origin/{branch}",
        Ahead = 0,
        Behind = 0
    };

    private static RepoInput Input(RepoType repoType, RepoFacts facts, string componentId = ComponentId) =>
        new(componentId, repoType, facts);

    private static ComponentDto Single(IEnumerable<RepoInput> inputs, ComponentAssemblyOptions? options = null) =>
        Assert.Single(ComponentAssembler.Assemble(inputs, options));

    private static List<RepoInput> AllSynced(string branch = "main") => new()
    {
        Input(RepoType.Design, Synced(branch)),
        Input(RepoType.Impl, Synced(branch)),
        Input(RepoType.Mil, Synced(branch)),
        Input(RepoType.Sil, Synced(branch))
    };

    [Fact]
    public void AllSynced_ReturnsSyncedWithRepoOrder()
    {
        var component = Single(AllSynced());

        Assert.Equal("SYNCED", component.AggregateState);
        Assert.Equal(14, component.SeverityRank);
        Assert.Equal("main", component.Branch);
        Assert.Null(component.Tag);
        Assert.False(component.MixedBranch);
        Assert.Empty(component.Flags);
        Assert.Equal(
            new[] { "DESIGN", "IMPL", "MIL", "SIL" },
            component.Repos.Select(r => r.RepoType));
        Assert.All(component.Repos, repo => Assert.Equal("SYNCED", repo.State));
        Assert.All(component.Repos, repo => Assert.True(repo.IsLocal));
    }

    [Fact]
    public void LocalMissingClone_IsMissingNotBindingError()
    {
        // 远程四仓齐全、本地 SIL 未克隆 → 单仓 MISSING，组件聚合 MISSING（§7.4 优先级 3）。
        var inputs = AllSynced();
        inputs[3] = Input(RepoType.Sil, RepoFacts.Missing(@"D:\ws\CF325F_ChanganSigProcgBascFct_SIL"));

        var component = Single(inputs, new ComponentAssemblyOptions { RemoteKnown = true });

        Assert.Equal("MISSING", component.AggregateState);
        Assert.Equal(12, component.SeverityRank);
        var sil = Assert.Single(component.Repos, r => r.RepoType == "SIL");
        Assert.Equal("MISSING", sil.State);
        Assert.False(sil.IsLocal);
    }

    [Fact]
    public void MissingBeatsBlockedLocal_OrderIsMissingThenBlocked()
    {
        // 附录 C F-MISSING+BLOCKED：MISSING=3 先于 BLOCKED_LOCAL=4。
        var inputs = new List<RepoInput>
        {
            Input(RepoType.Design, RepoFacts.Missing(@"D:\ws\CF325F_ChanganSigProcgBascFct_Design")),
            Input(RepoType.Impl, Synced() with { Unmerged = true }),
            Input(RepoType.Mil, Synced()),
            Input(RepoType.Sil, Synced())
        };

        var component = Single(inputs);

        Assert.Equal("MISSING", component.AggregateState);
        Assert.Equal(12, component.SeverityRank);
    }

    [Fact]
    public void BlockedLocal_CoversUnmergedAndInProgress()
    {
        var inputs = new List<RepoInput>
        {
            Input(RepoType.Design, Synced() with { Unmerged = true }),
            Input(RepoType.Impl, Synced() with { InProgress = true }),
            Input(RepoType.Mil, Synced()),
            Input(RepoType.Sil, Synced())
        };

        var component = Single(inputs);

        Assert.Equal("BLOCKED_LOCAL", component.AggregateState);
        Assert.Equal(3, component.SeverityRank);
        Assert.Equal("UNMERGED", component.Repos.Single(r => r.RepoType == "DESIGN").State);
        Assert.Equal("IN_PROGRESS", component.Repos.Single(r => r.RepoType == "IMPL").State);
    }

    [Fact]
    public void UnknownRepo_ShadowsLowerPriorities()
    {
        var unknown = new RepoFacts
        {
            HeadKind = HeadKind.Branch,
            Branch = "main",
            HeadSha = Sha,
            UpstreamConfigured = true,
            Ahead = null,
            Behind = null
        };

        var inputs = AllSynced();
        inputs[2] = Input(RepoType.Mil, unknown);

        var component = Single(inputs);

        Assert.Equal("UNKNOWN", component.AggregateState);
        Assert.Equal(2, component.SeverityRank);
        Assert.Equal("UNKNOWN", component.Repos.Single(r => r.RepoType == "MIL").State);
    }

    [Fact]
    public void InvalidRepo_ProducesBindingErrorWithHighestSeverity()
    {
        var invalid = new RepoFacts { IsValidRepo = false, LocalPath = @"D:\ws\bad" };
        var inputs = AllSynced();
        inputs[1] = Input(RepoType.Impl, invalid);

        var component = Single(inputs);

        Assert.Equal("BINDING_ERROR", component.AggregateState);
        Assert.Equal(1, component.SeverityRank);
        Assert.Equal("INVALID", component.Repos.Single(r => r.RepoType == "IMPL").State);
    }

    [Fact]
    public void RemoteIncomplete_IsBindingErrorWithNeutralSeverity()
    {
        // 远程缺 1 仓 → BINDING_ERROR(binding_cause=INCOMPLETE)，severity_rank=12（D-015 中性低）。
        var inputs = new List<RepoInput>
        {
            Input(RepoType.Design, Synced()),
            Input(RepoType.Impl, Synced()),
            Input(RepoType.Mil, Synced())
        };

        var component = Single(inputs, new ComponentAssemblyOptions { RemoteKnown = true });

        Assert.Equal("BINDING_ERROR", component.AggregateState);
        Assert.Equal(12, component.SeverityRank);
        Assert.Equal(3, component.Repos.Count);
    }

    [Fact]
    public void RemoteNotReady_LocalIncomplete_IsUnknownWithIncompleteFlag()
    {
        // H-01 / AC-21：远程未就绪时本地不完整组件不得判 MISSING/INVALID → UNKNOWN + incomplete。
        var inputs = new List<RepoInput>
        {
            Input(RepoType.Design, Synced()),
            Input(RepoType.Impl, Synced())
        };

        var component = Single(inputs, new ComponentAssemblyOptions { RemoteKnown = false });

        Assert.Equal("UNKNOWN", component.AggregateState);
        Assert.Equal(2, component.SeverityRank);
        Assert.Contains("incomplete", component.Flags, StringComparer.Ordinal);
    }

    [Fact]
    public void AmbiguousComponents_AreBindingErrorWithHighestSeverity()
    {
        var options = new ComponentAssemblyOptions
        {
            RemoteKnown = true,
            AmbiguousComponentIds = new HashSet<string>(new[] { ComponentId }, StringComparer.OrdinalIgnoreCase)
        };

        var component = Single(AllSynced(), options);

        Assert.Equal("BINDING_ERROR", component.AggregateState);
        Assert.Equal(1, component.SeverityRank);
    }

    [Fact]
    public void DuplicateRepoType_IsTreatAsAmbiguousBindingError()
    {
        var inputs = AllSynced();
        inputs.Add(Input(RepoType.Design, Synced()));

        var component = Single(inputs, new ComponentAssemblyOptions { RemoteKnown = true });

        Assert.Equal("BINDING_ERROR", component.AggregateState);
        Assert.Equal(1, component.SeverityRank);
    }

    [Fact]
    public void MixedBranch_IsDetectedWithComponentFlag()
    {
        var inputs = new List<RepoInput>
        {
            Input(RepoType.Design, Synced("main")),
            Input(RepoType.Impl, Synced("main")),
            Input(RepoType.Mil, Synced("develop")),
            Input(RepoType.Sil, Synced("main"))
        };

        var component = Single(inputs);

        Assert.Equal("MIXED_BRANCH", component.AggregateState);
        Assert.Equal(9, component.SeverityRank);
        Assert.True(component.MixedBranch);
        Assert.Contains("mixed_branch", component.Flags, StringComparer.Ordinal);
        Assert.Equal("main", component.Branch); // Impl 优先（SRS §11.3）
    }

    [Fact]
    public void TagDrift_WhenLocalTagSetsDiffer()
    {
        static RepoFacts DetachedAtTag(string tag) => new()
        {
            HeadKind = HeadKind.DetachedTag,
            HeadSha = Sha,
            TagsAtHead = new[] { tag },
            LocalTags = new[] { tag }
        };

        var inputs = new List<RepoInput>
        {
            Input(RepoType.Design, DetachedAtTag("v1.0")),
            Input(RepoType.Impl, DetachedAtTag("v1.0")),
            Input(RepoType.Mil, DetachedAtTag("v1.0")),
            Input(RepoType.Sil, Synced()) // 无 tag → 四仓 tag 集合不一致
        };

        var component = Single(inputs);

        Assert.Equal("TAG_DRIFT", component.AggregateState);
        Assert.Equal(5, component.SeverityRank);
        Assert.Null(component.Tag); // 四仓 tag 不一致时不由组件级 Tag 展示，逐仓展示
        Assert.All(component.Repos.Take(3), repo => Assert.Equal("DETACHED_TAG", repo.State));
        Assert.Equal("SYNCED", component.Repos.Single(r => r.RepoType == "SIL").State);
    }

    [Fact]
    public void TagDrift_WhenTagMovedFlagPresent()
    {
        var inputs = AllSynced();
        inputs[0] = Input(RepoType.Design, Synced() with { TagMoved = true });

        var component = Single(inputs);

        Assert.Equal("TAG_DRIFT", component.AggregateState);
        Assert.Contains("tag_moved", component.Flags, StringComparer.Ordinal);
        Assert.Contains("tag_moved", component.Repos.Single(r => r.RepoType == "DESIGN").Flags, StringComparer.Ordinal);
    }

    [Fact]
    public void NoUpstream_MapsToNewBranchWithSubType()
    {
        var noUpstream = new RepoFacts
        {
            HeadKind = HeadKind.Branch,
            Branch = "main", // 四仓分支名一致，仅无 upstream（F-NO_UPSTREAM-LOCAL）
            HeadSha = Sha,
            UpstreamConfigured = false,
            Ahead = null,
            Behind = null
        };

        var inputs = AllSynced();
        inputs[3] = Input(RepoType.Sil, noUpstream);

        var component = Single(inputs);

        Assert.Equal("NEW_BRANCH", component.AggregateState);
        Assert.Equal(11, component.SeverityRank);
        var sil = component.Repos.Single(r => r.RepoType == "SIL");
        Assert.Equal("NO_UPSTREAM", sil.State);
        Assert.Equal("LOCAL_ONLY", sil.SubType);
    }

    [Fact]
    public void Detached_IsDetectedForAllReposAtSameTag()
    {
        static RepoFacts Detached(string tag) => new()
        {
            HeadKind = HeadKind.DetachedTag,
            HeadSha = Sha,
            TagsAtHead = new[] { tag },
            LocalTags = new[] { tag }
        };

        var inputs = new List<RepoInput>
        {
            Input(RepoType.Design, Detached("v2.1")),
            Input(RepoType.Impl, Detached("v2.1")),
            Input(RepoType.Mil, Detached("v2.1")),
            Input(RepoType.Sil, Detached("v2.1"))
        };

        var component = Single(inputs);

        Assert.Equal("DETACHED", component.AggregateState);
        Assert.Equal(10, component.SeverityRank);
        Assert.Equal("v2.1", component.Tag);
    }

    [Fact]
    public void EmptyGroup_ReturnsBindingError()
    {
        var component = ComponentAssembler.AssembleComponent(ComponentId, Array.Empty<RepoInput>());

        Assert.Equal("BINDING_ERROR", component.AggregateState);
        Assert.Equal(1, component.SeverityRank);
        Assert.Empty(component.Repos);
    }

    [Fact]
    public void Components_AreSortedBySeverityThenComponentId()
    {
        var behind = "AAA_Behind";
        var synced = "ZZZ_Synced";
        var inputs = new List<RepoInput>
        {
            new(behind, RepoType.Design, Synced() with { Behind = 2 }),
            new(behind, RepoType.Impl, Synced() with { Behind = 2 }),
            new(behind, RepoType.Mil, Synced() with { Behind = 2 }),
            new(behind, RepoType.Sil, Synced() with { Behind = 2 }),
            new(synced, RepoType.Design, Synced()),
            new(synced, RepoType.Impl, Synced()),
            new(synced, RepoType.Mil, Synced()),
            new(synced, RepoType.Sil, Synced())
        };

        var components = ComponentAssembler.Assemble(inputs);

        Assert.Equal(2, components.Count);
        Assert.Equal(behind, components[0].ComponentId); // BEHIND severity=6 先于 SYNCED=14
        Assert.Equal("BEHIND", components[0].AggregateState);
        Assert.Equal("SYNCED", components[1].AggregateState);
    }

    [Fact]
    public void ComponentWireNames_CoverAllAggregateStates()
    {
        Assert.Equal("OP_PARTIAL_FAILED", ComponentAssembler.WireComponentState(ComponentState.OpPartialFailed));
        Assert.Equal("BINDING_ERROR", ComponentAssembler.WireComponentState(ComponentState.BindingError));
        Assert.Equal("BLOCKED_LOCAL", ComponentAssembler.WireComponentState(ComponentState.BlockedLocal));
        Assert.Equal("TAG_DRIFT", ComponentAssembler.WireComponentState(ComponentState.TagDrift));
        Assert.Equal("MIXED_BRANCH", ComponentAssembler.WireComponentState(ComponentState.MixedBranch));
        Assert.Equal("NEW_BRANCH", ComponentAssembler.WireComponentState(ComponentState.NewBranch));
        Assert.Equal("DETACHED", ComponentAssembler.WireComponentState(ComponentState.Detached));
        Assert.Equal("EMPTY", ComponentAssembler.WireComponentState(ComponentState.Empty));
        Assert.Equal("SYNCED", ComponentAssembler.WireComponentState(ComponentState.Synced));
    }
}
