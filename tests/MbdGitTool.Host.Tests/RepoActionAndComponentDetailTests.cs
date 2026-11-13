using System;
using System.Collections.Generic;
using System.Linq;
using MbdGitTool.Core.Contracts;
using MbdGitTool.Core.Domain;
using MbdGitTool.Host.Services;

namespace MbdGitTool.Host.Tests;

public sealed class RepoActionServiceTests
{
    private static readonly string[] States =
    {
        "SYNCED", "DIRTY", "AHEAD", "BEHIND", "DIVERGED", "DETACHED_TAG", "DETACHED_COMMIT",
        "NO_UPSTREAM", "EMPTY", "UNMERGED", "IN_PROGRESS", "INVALID", "MISSING", "UNKNOWN"
    };

    // spec §6.2 矩阵（PULL/FETCH/CHKOUT/COMMIT/PUSH；Y=允许 N=禁止 C=允许但需确认）。
    private static readonly IReadOnlyDictionary<string, string> ExpectedMatrix =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["SYNCED"] = "YYYYY",
            ["DIRTY"] = "NYCYY",
            ["AHEAD"] = "NYCNY",
            ["BEHIND"] = "YYCNN",
            ["DIVERGED"] = "NYCNN",
            ["DETACHED_TAG"] = "NYCNN",
            ["DETACHED_COMMIT"] = "NYCNN",
            ["NO_UPSTREAM"] = "NYNNY",
            ["EMPTY"] = "NYCYY",
            ["UNMERGED"] = "NYNNN",
            ["IN_PROGRESS"] = "NNNNN",
            ["INVALID"] = "NNNNN",
            ["MISSING"] = "NNNNN",
            ["UNKNOWN"] = "NYNNN"
        };

    [Theory]
    [MemberData(nameof(StatesWithMatrix))]
    public void GetAvailability_MatchesSpecMatrix_AndM1AlwaysDisabled(string state, string expected)
    {
        var repo = Repo(state);
        var service = new RepoActionService();

        var availability = service.GetAvailability(repo);

        Assert.Equal(new[] { "PULL", "FETCH", "CHKOUT", "COMMIT", "PUSH" }, availability.Select(item => item.Action));
        var actual = string.Concat(availability.Select(item =>
            !item.MatrixAllowed ? "N" : item.RequiresConfirm ? "C" : "Y"));
        Assert.Equal(expected, actual);
        Assert.All(availability, item =>
        {
            Assert.False(item.Enabled);
            Assert.Equal("M2", item.Stage);
            Assert.False(string.IsNullOrWhiteSpace(item.Reason));
        });
    }

    public static IEnumerable<object[]> StatesWithMatrix() =>
        States.Select(state => new object[] { state, ExpectedMatrix[state] });

    [Fact]
    public void Run_UnknownAction_ReturnsInvalidArgument()
    {
        var service = new RepoActionService();
        var index = Index("SYNCED");

        var result = service.Run(
            new RunRepoActionParams { ComponentId = "CF100A_Demo", RepoType = "IMPL", Action = "RESET" },
            index);

        Assert.False(result.Ok);
        Assert.Equal(BridgeErrorCodes.InvalidArgument, result.Error!.Code);
    }

    [Fact]
    public void Run_UnknownRepo_ReturnsNotFound()
    {
        var service = new RepoActionService();

        var result = service.Run(
            new RunRepoActionParams { ComponentId = "NOPE", RepoType = "IMPL", Action = "PULL" },
            Index("SYNCED"));

        Assert.False(result.Ok);
        Assert.Equal(BridgeErrorCodes.NotFound, result.Error!.Code);
    }

    [Fact]
    public void Run_MatrixBlocked_ReturnsActionNotAllowedWithReason()
    {
        var service = new RepoActionService();

        var result = service.Run(
            new RunRepoActionParams { ComponentId = "CF100A_Demo", RepoType = "IMPL", Action = "PULL" },
            Index("DIRTY"));

        Assert.False(result.Ok);
        Assert.Equal(BridgeErrorCodes.ActionNotAllowed, result.Error!.Code);
        Assert.NotNull(result.Error.Details);
        Assert.True(result.Error.Details!.ContainsKey("reason"));
    }

    [Fact]
    public void Run_MatrixAllowed_ReturnsNotImplemented()
    {
        var service = new RepoActionService();

        var result = service.Run(
            new RunRepoActionParams { ComponentId = "CF100A_Demo", RepoType = "IMPL", Action = "PULL" },
            Index("SYNCED"));

        Assert.False(result.Ok);
        Assert.Equal(BridgeErrorCodes.NotImplemented, result.Error!.Code);
        Assert.Contains("M2", result.Error.Message);
        Assert.NotNull(result.Error.Details);
        Assert.Equal("M2", result.Error.Details!["writeStage"]);
    }

    private static RepoRef Repo(string state)
    {
        var repo = TestData.ToRepo("CF100A_Demo", new RepoSpec("IMPL", @"C:\work\CF100A_Demo_Impl", State: state));
        var component = new ComponentDto(
            "CF100A_Demo",
            "UNKNOWN",
            0,
            "main",
            null,
            false,
            Array.Empty<string>(),
            new[] { repo });
        return new RepoRef("CF100A_Demo", component, repo);
    }

    private static SnapshotIndex Index(string state) =>
        TestData.Index(
            @"C:\work",
            new ComponentSpec("CF100A_Demo", new[]
            {
                new RepoSpec("IMPL", @"C:\work\CF100A_Demo_Impl", State: state)
            }));
}

public sealed class ComponentDetailServiceTests
{
    [Fact]
    public void Get_ReturnsFourReposWithActionsAndCiHint()
    {
        var index = TestData.Index(
            @"C:\work",
            new ComponentSpec("CF100A_Demo", new[]
            {
                new RepoSpec("DESIGN", @"C:\work\CF100A_Demo_Design"),
                new RepoSpec("IMPL", @"C:\work\CF100A_Demo_Impl", State: "DIRTY", DirtyCount: 2),
                new RepoSpec("MIL", @"C:\work\CF100A_Demo_MIL", IsLocal: false, State: "MISSING"),
                new RepoSpec("SIL", @"C:\work\CF100A_Demo_SIL", State: "AHEAD", Ahead: 1)
            }));

        var service = new ComponentDetailService(() => index, new RepoActionService());
        var result = service.Get(new GetComponentDetailParams("CF100A_Demo"));

        Assert.True(result.Ok);
        var detail = Assert.IsType<ComponentDetailDto>(result.Data);
        Assert.Equal("CF100A_Demo", detail.ComponentId);
        Assert.Equal(4, detail.Repos.Count);
        Assert.Equal(new[] { "DESIGN", "IMPL", "MIL", "SIL" }, detail.Repos.Select(repo => repo.RepoType));
        Assert.All(detail.Repos, repo => Assert.Equal(5, repo.Actions.Count));
        Assert.All(detail.Repos, repo => Assert.All(repo.Actions, action => Assert.False(action.Enabled)));
        Assert.Equal(2, detail.Repos[1].DirtyCount);
        Assert.False(detail.Repos[2].IsLocal);
        Assert.Equal(1, detail.Repos[3].Ahead);
        Assert.False(detail.Ci.Enabled);
        Assert.Equal("NONE", detail.Ci.State);
        Assert.Equal(@"C:\work", detail.WorkspaceRoot);
    }

    [Fact]
    public void Get_MissingComponentId_ReturnsInvalidArgument()
    {
        var service = new ComponentDetailService(() => SnapshotIndex.Empty, new RepoActionService());

        var result = service.Get(new GetComponentDetailParams());

        Assert.False(result.Ok);
        Assert.Equal(BridgeErrorCodes.InvalidArgument, result.Error!.Code);
    }

    [Fact]
    public void Get_UnknownComponent_ReturnsNotFound()
    {
        var index = TestData.Index(
            @"C:\work",
            new ComponentSpec("CF100A_Demo", new[] { new RepoSpec("IMPL", @"C:\work\CF100A_Demo_Impl") }));
        var service = new ComponentDetailService(() => index, new RepoActionService());

        var result = service.Get(new GetComponentDetailParams("NOPE"));

        Assert.False(result.Ok);
        Assert.Equal(BridgeErrorCodes.NotFound, result.Error!.Code);
    }
}
