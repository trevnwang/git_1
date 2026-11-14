using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MbdGitTool.Core.Contracts;
using MbdGitTool.Core.Domain;
using MbdGitTool.Host.Services;
using Xunit;

namespace MbdGitTool.Host.Tests;

/// <summary>
/// M2b-2 EMPTY 首推（FR-REPO-05 / D-025 §2.5④ / §7.6）：
/// M2b 矩阵恢复 <c>EMPTY.PUSH=Y</c>；首推走 <c>git push -u origin HEAD</c> 建立 upstream（setUpstream），
/// unborn HEAD 给出可读错误码且零远端写；NO_UPSTREAM 发布分支同样建立 upstream。
/// </summary>
public sealed class M2b2EmptyPushTests
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    [Fact]
    public void GetAvailability_EmptyState_PushEnabledAndRequiresConfirm()
    {
        var availability = new RepoActionService().GetAvailability(Repo("EMPTY", null));

        var push = availability.Single(item => item.Action == "PUSH");
        Assert.True(push.Enabled);
        Assert.True(push.MatrixAllowed);
        Assert.True(push.RequiresConfirm);
        Assert.Equal("CONFIRM_REQUIRED", push.ReasonCode);

        // COMMIT 在 EMPTY 仍允许（先有首个提交才可能首推）。
        Assert.True(availability.Single(item => item.Action == "COMMIT").Enabled);
    }

    [Fact]
    public async Task EmptyFirstPush_SetsUpstreamAndCreatesRemoteBranch()
    {
        using var workspace = new TempWorkspace();
        var bare = Path.Combine(workspace.Root, "remote", "CF100A_Demo_Impl.git");
        workspace.Dir("remote");
        TestGit.Run(workspace.Root, "init", "--bare", "-q", "--initial-branch=main", bare);

        var repo = workspace.Dir("CF100A_Demo_Impl");
        TestGit.InitRepo(repo);
        TestGit.Run(repo, "symbolic-ref", "HEAD", "refs/heads/main"); // 统一 main（与 origin/main 断言一致）
        workspace.CreateFile("CF100A_Demo_Impl/README.md", "first commit");
        TestGit.CommitAll(repo, "first commit");
        TestGit.Run(repo, "remote", "add", "origin", bare);
        var head = TestGit.Capture(repo, "rev-parse", "HEAD");

        var service = new RepoActionService();
        var index = IndexFor(repo, "EMPTY", head);
        var result = await service.RunAsync(
            Params("PUSH", new { confirm = true }),
            index,
            CancellationToken.None);

        Assert.True(result.Ok, Describe(result));
        var json = JsonSerializer.Serialize(result, WebJson);
        Assert.Contains("upstream", json, StringComparison.Ordinal);
        Assert.Contains("\"message\"", json, StringComparison.Ordinal);
        Assert.Equal("origin/main", TestGit.Capture(repo, "rev-parse", "--abbrev-ref", "@{upstream}"));
        Assert.Equal(head, TestGit.Capture(bare, "rev-parse", "refs/heads/main"));
    }

    [Fact]
    public async Task EmptyPush_UnbornHead_FailsWithReadableCodeAndNoRemoteWrite()
    {
        using var workspace = new TempWorkspace();
        var bare = Path.Combine(workspace.Root, "remote", "CF100A_Demo_Impl.git");
        workspace.Dir("remote");
        TestGit.Run(workspace.Root, "init", "--bare", "-q", bare);

        var repo = workspace.Dir("CF100A_Demo_Impl");
        TestGit.InitRepo(repo); // 无提交 = unborn HEAD
        TestGit.Run(repo, "remote", "add", "origin", bare);

        var service = new RepoActionService();
        // HeadSha 传空串 = 快照无 HEAD，CAS 接受 EMPTY。
        var index = IndexFor(repo, "EMPTY", string.Empty);
        var result = await service.RunAsync(
            Params("PUSH", new { confirm = true }),
            index,
            CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Equal(BridgeErrorCodes.RefNotFound, result.Error!.Code);
        Assert.True(result.Error.Details!.ContainsKey("requiresCommit"));
        Assert.Equal(string.Empty, TestGit.Capture(bare, "for-each-ref", "--format=%(refname)"));
    }

    [Fact]
    public async Task NoUpstreamPush_AlsoSetsUpstream()
    {
        using var workspace = new TempWorkspace();
        var bare = Path.Combine(workspace.Root, "remote", "CF100A_Demo_Impl.git");
        workspace.Dir("remote");
        TestGit.Run(workspace.Root, "init", "--bare", "-q", "--initial-branch=main", bare);

        var repo = workspace.Dir("CF100A_Demo_Impl");
        TestGit.InitRepo(repo);
        TestGit.Run(repo, "symbolic-ref", "HEAD", "refs/heads/main");
        workspace.CreateFile("CF100A_Demo_Impl/README.md", "local only");
        TestGit.CommitAll(repo, "local only");
        TestGit.Run(repo, "remote", "add", "origin", bare);
        var head = TestGit.Capture(repo, "rev-parse", "HEAD");

        var service = new RepoActionService();
        var index = IndexFor(repo, "NO_UPSTREAM", head);
        var result = await service.RunAsync(
            Params("PUSH", new { confirm = true }),
            index,
            CancellationToken.None);

        Assert.True(result.Ok, Describe(result));
        Assert.Equal("origin/main", TestGit.Capture(repo, "rev-parse", "--abbrev-ref", "@{upstream}"));
        Assert.Equal(head, TestGit.Capture(bare, "rev-parse", "refs/heads/main"));
    }

    private static RunRepoActionParams Params(string action, object? args = null)
    {
        IReadOnlyDictionary<string, JsonElement>? dictionary = null;
        if (args is not null)
        {
            dictionary = JsonSerializer.SerializeToElement(args, WebJson)
                .EnumerateObject()
                .ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal);
        }

        return new RunRepoActionParams
        {
            ComponentId = "CF100A_Demo",
            RepoType = "IMPL",
            Action = action,
            Args = dictionary
        };
    }

    private static SnapshotIndex IndexFor(string repoPath, string state, string? headSha) =>
        TestData.Index(
            Path.GetDirectoryName(repoPath) ?? repoPath,
            new ComponentSpec("CF100A_Demo", new[]
            {
                new RepoSpec(
                    "IMPL",
                    repoPath,
                    State: state,
                    Branch: "main",
                    HeadSha: headSha ?? string.Empty)
            }));

    private static RepoRef Repo(string state, string? headSha)
    {
        var repo = TestData.ToRepo(
            "CF100A_Demo",
            new RepoSpec("IMPL", @"C:\work\CF100A_Demo_Impl", State: state, HeadSha: headSha ?? string.Empty));
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

    private static string Describe(CommandResult result) =>
        result.Ok ? "ok" : $"{result.Error?.Code}: {result.Error?.Message}";
}
