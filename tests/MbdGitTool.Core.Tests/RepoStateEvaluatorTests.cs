using MbdGitTool.Core.Domain;

namespace MbdGitTool.Core.Tests;

public class RepoStateEvaluatorTests
{
    private const string Sha = "0123456789abcdef0123456789abcdef01234567";

    private static RepoFacts Branch(
        bool upstream = true,
        int? ahead = 0,
        int? behind = 0,
        bool remoteBranchExists = false,
        bool upstreamGone = false) => new()
    {
        HeadKind = HeadKind.Branch,
        Branch = "main",
        HeadSha = Sha,
        UpstreamConfigured = upstream,
        UpstreamRef = upstream ? "refs/remotes/origin/main" : null,
        RemoteBranchExists = remoteBranchExists,
        UpstreamGone = upstreamGone,
        Ahead = ahead,
        Behind = behind
    };

    private static RepoEvaluation Evaluate(RepoFacts facts, RepoStateOptions? options = null) =>
        RepoStateEvaluator.Evaluate(facts, options);

    [Fact]
    public void Synced_WhenHeadEqualsUpstreamAndClean()
    {
        var result = Evaluate(Branch());

        Assert.Equal(RepoState.Synced, result.State);
        Assert.Equal(NoUpstreamSubType.None, result.SubType);
        Assert.Equal(RepoFlags.None, result.Flags);
    }

    [Fact]
    public void Dirty_WhenHeadEqualsUpstreamButTrackedChanged()
    {
        var result = Evaluate(Branch() with { DirtyTracked = true, DirtyCount = 3 });

        Assert.Equal(RepoState.Dirty, result.State);
        Assert.True(result.Flags.HasFlag(RepoFlags.DirtyTracked));
    }

    [Fact]
    public void Ahead_WhenLocalHasNewCommits()
    {
        var result = Evaluate(Branch(ahead: 2, behind: 0));

        Assert.Equal(RepoState.Ahead, result.State);
        Assert.Equal(2, result.Ahead);
        Assert.Equal(0, result.Behind);
    }

    [Fact]
    public void Behind_WhenRemoteHasNewCommits()
    {
        var result = Evaluate(Branch(ahead: 0, behind: 3));

        Assert.Equal(RepoState.Behind, result.State);
        Assert.Equal(0, result.Ahead);
        Assert.Equal(3, result.Behind);
    }

    [Fact]
    public void Diverged_WhenBothSidesHaveNewCommits()
    {
        var result = Evaluate(Branch(ahead: 1, behind: 1));

        Assert.Equal(RepoState.Diverged, result.State);
    }

    [Fact]
    public void NoUpstream_LocalOnly_WhenNoUpstreamAndNoRemoteBranch()
    {
        var result = Evaluate(Branch(upstream: false, ahead: null, behind: null));

        Assert.Equal(RepoState.NoUpstream, result.State);
        Assert.Equal(NoUpstreamSubType.LocalOnly, result.SubType);
    }

    [Fact]
    public void NoUpstream_UnsetButExists_WhenRemoteBranchExistsButNoTracking()
    {
        var result = Evaluate(Branch(upstream: false, ahead: 0, behind: 0, remoteBranchExists: true));

        Assert.Equal(RepoState.NoUpstream, result.State);
        Assert.Equal(NoUpstreamSubType.UnsetButExists, result.SubType);
    }

    [Fact]
    public void NoUpstream_UpstreamGone_WhenTrackingRefDeleted()
    {
        var result = Evaluate(Branch(upstream: true, ahead: null, behind: null, upstreamGone: true));

        Assert.Equal(RepoState.NoUpstream, result.State);
        Assert.Equal(NoUpstreamSubType.UpstreamGone, result.SubType);
    }

    [Fact]
    public void DetachedTag_WhenDetachedHeadHasTag()
    {
        var facts = new RepoFacts
        {
            HeadKind = HeadKind.DetachedTag,
            HeadSha = Sha,
            TagsAtHead = new[] { "v1.0" }
        };

        var result = Evaluate(facts);

        Assert.Equal(RepoState.DetachedTag, result.State);
        Assert.Null(ComponentAssembler.WireSubType(result.SubType));
    }

    [Fact]
    public void DetachedCommit_WhenDetachedHeadHasNoTag()
    {
        var facts = new RepoFacts { HeadKind = HeadKind.DetachedCommit, HeadSha = Sha };

        Assert.Equal(RepoState.DetachedCommit, Evaluate(facts).State);
    }

    [Fact]
    public void Unmerged_TakesPriorityOverInProgress()
    {
        var facts = Branch() with { Unmerged = true, InProgress = true };

        Assert.Equal(RepoState.Unmerged, Evaluate(facts).State);
    }

    [Fact]
    public void InProgress_WhenMergeOrRebaseMarkerExists()
    {
        Assert.Equal(RepoState.InProgress, Evaluate(Branch() with { InProgress = true }).State);
    }

    [Fact]
    public void Empty_WhenHeadUnborn()
    {
        var facts = new RepoFacts { HeadKind = HeadKind.Unborn, Branch = "main", HeadSha = null };

        Assert.Equal(RepoState.Empty, Evaluate(facts).State);
    }

    [Fact]
    public void Invalid_WhenDirectoryIsNotGitRepo()
    {
        var facts = new RepoFacts { IsValidRepo = false, LocalPath = @"D:\ws\X_Design" };

        Assert.Equal(RepoState.Invalid, Evaluate(facts).State);
    }

    [Fact]
    public void Missing_WhenDirectoryDoesNotExist()
    {
        Assert.Equal(RepoState.Missing, Evaluate(RepoFacts.Missing(@"D:\ws\X_Design")).State);
    }

    [Fact]
    public void Unknown_WhenRelationCannotBeDetermined()
    {
        // 配置了 upstream 但 ahead/behind 不可用（fetch 失败/TTL 过期）→ 关系类不可判 → UNKNOWN（禁止假绿）。
        var result = Evaluate(Branch(upstream: true, ahead: null, behind: null));

        Assert.Equal(RepoState.Unknown, result.State);
        Assert.Equal(0, result.Ahead);
        Assert.Equal(0, result.Behind);
    }

    [Fact]
    public void Unknown_WhenStaleFlagForcesUnknown()
    {
        var result = Evaluate(Branch() with { Stale = true });

        Assert.Equal(RepoState.Unknown, result.State);
        Assert.True(result.Flags.HasFlag(RepoFlags.Stale));
    }

    [Fact]
    public void Unknown_WhenScanFailed()
    {
        var result = Evaluate(RepoFacts.Failed("git status 失败"));

        Assert.Equal(RepoState.Unknown, result.State);
    }

    [Fact]
    public void Unknown_WhenRemoteGone()
    {
        // 附录 C F-REMOTE-GONE：远程删除 → 关系不可判，主状态 UNKNOWN + remote_gone flag。
        var result = Evaluate(Branch() with { RemoteGone = true });

        Assert.Equal(RepoState.Unknown, result.State);
        Assert.True(result.Flags.HasFlag(RepoFlags.RemoteGone));
    }

    [Fact]
    public void Flags_AreCollectedWithoutChangingPrimaryState()
    {
        var facts = Branch() with
        {
            UntrackedCount = 2,
            Stashed = true,
            Shallow = true,
            LfsMissing = true,
            Busy = true,
            TagMoved = true,
            IsReparsePoint = true,
            IsSubmodule = true
        };

        var result = Evaluate(facts);

        Assert.Equal(RepoState.Synced, result.State);
        Assert.True(result.Flags.HasFlag(RepoFlags.Untracked));
        Assert.True(result.Flags.HasFlag(RepoFlags.Stashed));
        Assert.True(result.Flags.HasFlag(RepoFlags.Shallow));
        Assert.True(result.Flags.HasFlag(RepoFlags.LfsMissing));
        Assert.True(result.Flags.HasFlag(RepoFlags.Busy));
        Assert.True(result.Flags.HasFlag(RepoFlags.TagMoved));
        Assert.True(result.Flags.HasFlag(RepoFlags.ReparsePoint));
        Assert.True(result.Flags.HasFlag(RepoFlags.Submodule));
    }

    [Fact]
    public void FlagsToWireNames_UsesSrsSnakeCaseInFixedOrder()
    {
        var names = (RepoFlags.Untracked | RepoFlags.DirtyTracked | RepoFlags.Busy).ToWireNames();

        Assert.Equal(new[] { "dirty_tracked", "untracked", "busy" }, names);
        Assert.Empty(RepoFlags.None.ToWireNames());
    }

    [Fact]
    public void DirtyIncludeUntrackedOption_ChangesDirtyClassification()
    {
        var facts = Branch() with { UntrackedCount = 1 };

        Assert.Equal(RepoState.Synced, Evaluate(facts).State);
        Assert.Equal(RepoState.Dirty, Evaluate(facts, new RepoStateOptions { DirtyIncludeUntracked = true }).State);
    }

    [Fact]
    public void WireNames_AreSrsUpperSnake()
    {
        Assert.Equal("SYNCED", ComponentAssembler.WireRepoState(RepoState.Synced));
        Assert.Equal("NO_UPSTREAM", ComponentAssembler.WireRepoState(RepoState.NoUpstream));
        Assert.Equal("IN_PROGRESS", ComponentAssembler.WireRepoState(RepoState.InProgress));
        Assert.Equal("LOCAL_ONLY", ComponentAssembler.WireSubType(NoUpstreamSubType.LocalOnly));
        Assert.Equal("UNSET_BUT_EXISTS", ComponentAssembler.WireSubType(NoUpstreamSubType.UnsetButExists));
        Assert.Equal("UPSTREAM_GONE", ComponentAssembler.WireSubType(NoUpstreamSubType.UpstreamGone));
        Assert.Equal("DESIGN", ComponentAssembler.WireRepoType(RepoType.Design));
        Assert.Equal("MIL", ComponentAssembler.WireRepoType(RepoType.Mil));
    }
}
