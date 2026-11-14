using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MbdGitTool.Core.Contracts;
using MbdGitTool.Core.Domain;
using MbdGitTool.Host.Services;

namespace MbdGitTool.Host.Tests;

/// <summary>
/// M2 单仓真实操作（本地 bare remote + 两个 clone，无网络/无 Gitea）：
/// FETCH / PULL(FF) / PULL 非快进 / CHKOUT / COMMIT / PUSH / 参数校验 / token 不泄漏。
/// 每个测试独立临时目录，Dispose 清理。
/// </summary>
public sealed class RepoActionGitTests
{
    private const string TestToken = "ghp_m2_fake_token_0123456789abcdef";

    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Fetch_Success_UpdatesRemoteTrackingWithoutMovingHead()
    {
        using var fixture = GitRemoteFixture.Create();
        fixture.CommitAndPushFromA("remote ahead");
        var service = new RepoActionService();
        var index = IndexFor(fixture.CloneB);
        var headBefore = GitRemoteFixture.Head(fixture.CloneB);

        var result = await service.RunAsync(Params("FETCH"), index, CancellationToken.None);

        Assert.True(result.Ok, Describe(result));
        var json = Serialize(result);
        Assert.Contains("\"accepted\":true", json, StringComparison.Ordinal);
        Assert.Contains("\"action\":\"FETCH\"", json, StringComparison.Ordinal);
        Assert.Equal(headBefore, GitRemoteFixture.Head(fixture.CloneB));
        Assert.Equal(
            GitRemoteFixture.Head(fixture.BarePath),
            TestGit.Capture(fixture.CloneB, "rev-parse", "refs/remotes/origin/main"));
    }

    [Fact]
    public async Task Pull_FastForward_SucceedsAndMovesHead()
    {
        using var fixture = GitRemoteFixture.Create();
        fixture.CommitAndPushFromA("remote ahead");
        var service = new RepoActionService();
        var index = IndexFor(fixture.CloneB);
        var remoteHead = GitRemoteFixture.Head(fixture.BarePath);

        var result = await service.RunAsync(Params("PULL"), index, CancellationToken.None);

        Assert.True(result.Ok, Describe(result));
        Assert.Equal(remoteHead, GitRemoteFixture.Head(fixture.CloneB));
        Assert.Equal("0", TestGit.Capture(fixture.CloneB, "rev-list", "--count", "--merges", "HEAD"));
    }

    [Fact]
    public async Task Pull_NonFastForward_ReturnsNonFfAndDoesNotMerge()
    {
        using var fixture = GitRemoteFixture.Create();
        // B 本地提交（未推送）→ A 再推一条 → B 分叉。
        File.WriteAllText(Path.Combine(fixture.CloneB, "local.txt"), "local only");
        TestGit.CommitAll(fixture.CloneB, "local only commit");
        fixture.CommitAndPushFromA("remote diverged");
        var service = new RepoActionService();
        var index = IndexFor(fixture.CloneB);
        var headBefore = GitRemoteFixture.Head(fixture.CloneB);

        var result = await service.RunAsync(Params("PULL"), index, CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Equal(BridgeErrorCodes.GitNonFastForward, result.Error!.Code);
        Assert.Equal(headBefore, GitRemoteFixture.Head(fixture.CloneB));
        Assert.Equal("0", TestGit.Capture(fixture.CloneB, "rev-list", "--count", "--merges", "HEAD"));
        Assert.Contains("branch", result.Error.Details!.Keys);
    }

    [Fact]
    public async Task Pull_ExplicitBranchDifferentFromCurrent_IsRejectedWithoutChanges()
    {
        using var fixture = GitRemoteFixture.Create();

        // 远端 feature 领先 main（main 是其祖先）：旧实现会 fetch origin feature 后把当前 main 快进到 origin/feature。
        TestGit.Run(fixture.CloneA, "checkout", "-q", "-b", "feature");
        File.WriteAllText(Path.Combine(fixture.CloneA, "feature-ahead.txt"), "feature ahead");
        TestGit.CommitAll(fixture.CloneA, "feature ahead of main");
        TestGit.Run(fixture.CloneA, "push", "-q", "origin", "feature");
        TestGit.Run(fixture.CloneA, "checkout", "-q", "main");
        var featureHead = TestGit.Capture(fixture.BarePath, "rev-parse", "refs/heads/feature");

        var service = new RepoActionService();
        var index = IndexFor(fixture.CloneB);
        var headBefore = GitRemoteFixture.Head(fixture.CloneB);
        var branchBefore = GitRemoteFixture.Branch(fixture.CloneB);

        // AC-SAFE-17：PULL 只允许当前分支，显式 branch 不同必须拒绝且零改动（不得 FF 到其它远端分支）。
        var result = await service.RunAsync(
            Params("PULL", new { branch = "feature" }),
            index,
            CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Equal(BridgeErrorCodes.InvalidArgument, result.Error!.Code);
        Assert.Equal("branch", result.Error.Details!["field"]);
        Assert.Equal(headBefore, GitRemoteFixture.Head(fixture.CloneB));
        Assert.Equal(branchBefore, GitRemoteFixture.Branch(fixture.CloneB));
        Assert.NotEqual(featureHead, GitRemoteFixture.Head(fixture.CloneB));
    }

    [Fact]
    public async Task CommitAndPush_WithoutConfirm_AreRejectedByFrozenConfirmFlag()
    {
        using var fixture = GitRemoteFixture.Create();
        File.WriteAllText(Path.Combine(fixture.CloneB, "needs-confirm.txt"), "x");
        TestGit.Run(fixture.CloneB, "add", "needs-confirm.txt");
        var service = new RepoActionService();
        var index = IndexFor(fixture.CloneB);
        var headBefore = GitRemoteFixture.Head(fixture.CloneB);

        // ADR AC-UI-08 / SRS §7.6 冻结：COMMIT/PUSH 恒 RequiresConfirm（即使矩阵 cell=Y）。
        var commit = await service.RunAsync(
            Params("COMMIT", new { message = "no confirm" }), index, CancellationToken.None);
        Assert.False(commit.Ok);
        Assert.Equal(BridgeErrorCodes.ConfirmRequired, commit.Error!.Code);
        Assert.Equal(headBefore, GitRemoteFixture.Head(fixture.CloneB));

        var push = await service.RunAsync(Params("PUSH"), index, CancellationToken.None);
        Assert.False(push.Ok);
        Assert.Equal(BridgeErrorCodes.ConfirmRequired, push.Error!.Code);
        Assert.Equal(headBefore, GitRemoteFixture.Head(fixture.CloneB));
    }

    [Fact]
    public async Task Commit_WithMessage_SucceedsAndRenamesHead()
    {
        using var fixture = GitRemoteFixture.Create();
        File.WriteAllText(Path.Combine(fixture.CloneB, "work.txt"), "work");
        // FR-ACT-03：Host 只提交 index 中已暂存内容，测试需显式 git add。
        TestGit.Run(fixture.CloneB, "add", "work.txt");
        var service = new RepoActionService();
        var index = IndexFor(fixture.CloneB);
        var headBefore = GitRemoteFixture.Head(fixture.CloneB);

        var result = await service.RunAsync(
            Params("COMMIT", new { message = "feat: m2 commit", confirm = true }),
            index,
            CancellationToken.None);

        Assert.True(result.Ok, Describe(result));
        var headAfter = GitRemoteFixture.Head(fixture.CloneB);
        Assert.NotEqual(headBefore, headAfter);
        Assert.Equal("feat: m2 commit", TestGit.Capture(fixture.CloneB, "log", "-1", "--pretty=%s"));
        Assert.Contains("\"postHead\":" + JsonSerializer.Serialize(headAfter, WebJson), Serialize(result), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Commit_UnstagedChanges_ReturnsNothingToCommit()
    {
        using var fixture = GitRemoteFixture.Create();
        File.WriteAllText(Path.Combine(fixture.CloneB, "unstaged.txt"), "draft");
        var service = new RepoActionService();
        var index = IndexFor(fixture.CloneB);
        var headBefore = GitRemoteFixture.Head(fixture.CloneB);

        var result = await service.RunAsync(
            Params("COMMIT", new { message = "should not commit", confirm = true }),
            index,
            CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Equal(BridgeErrorCodes.NothingToCommit, result.Error!.Code);
        Assert.Equal(headBefore, GitRemoteFixture.Head(fixture.CloneB));
    }

    [Fact]
    public async Task Commit_NothingToCommit_ReturnsNothingToCommit()
    {
        using var fixture = GitRemoteFixture.Create();
        var service = new RepoActionService();
        var index = IndexFor(fixture.CloneB);

        var result = await service.RunAsync(
            Params("COMMIT", new { message = "noop", confirm = true }),
            index,
            CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Equal(BridgeErrorCodes.NothingToCommit, result.Error!.Code);
    }

    [Fact]
    public async Task Commit_MessageMissingOrTooLong_ReturnsInvalidArgument()
    {
        using var fixture = GitRemoteFixture.Create();
        var service = new RepoActionService();
        var index = IndexFor(fixture.CloneB);

        var missing = await service.RunAsync(Params("COMMIT"), index, CancellationToken.None);
        Assert.Equal(BridgeErrorCodes.InvalidArgument, missing.Error!.Code);
        Assert.Equal("message", missing.Error.Details!["field"]);

        var blank = await service.RunAsync(
            Params("COMMIT", new { message = "   ", confirm = true }), index, CancellationToken.None);
        Assert.Equal(BridgeErrorCodes.InvalidArgument, blank.Error!.Code);

        var tooLong = await service.RunAsync(
            Params("COMMIT", new { message = new string('x', 2001) }), index, CancellationToken.None);
        Assert.Equal(BridgeErrorCodes.InvalidArgument, tooLong.Error!.Code);
        Assert.Equal("message", tooLong.Error.Details!["field"]);
    }

    [Fact]
    public async Task Checkout_RequiresConfirm_WhenMatrixCellIsC()
    {
        using var fixture = GitRemoteFixture.Create();
        var service = new RepoActionService();
        // DIRTY 行 CHKOUT 为 C（需二次确认）：未带 confirm 必须 E_CONFIRM_REQUIRED 且不写。
        var index = IndexFor(fixture.CloneB, state: "DIRTY");
        var headBefore = GitRemoteFixture.Head(fixture.CloneB);
        var branchBefore = GitRemoteFixture.Branch(fixture.CloneB);

        var result = await service.RunAsync(
            Params("CHKOUT", new { branch = "main" }),
            index,
            CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Equal(BridgeErrorCodes.ConfirmRequired, result.Error!.Code);
        Assert.Equal(headBefore, GitRemoteFixture.Head(fixture.CloneB));
        Assert.Equal(branchBefore, GitRemoteFixture.Branch(fixture.CloneB));
    }

    [Fact]
    public async Task Checkout_Branch_SucceedsAndMovesHead()
    {
        using var fixture = GitRemoteFixture.Create();
        TestGit.Run(fixture.CloneB, "branch", "feature");
        var service = new RepoActionService();
        var index = IndexFor(fixture.CloneB);

        var result = await service.RunAsync(
            Params("CHKOUT", new { branch = "feature" }),
            index,
            CancellationToken.None);

        Assert.True(result.Ok, Describe(result));
        Assert.Equal("feature", GitRemoteFixture.Branch(fixture.CloneB));
        Assert.Contains("\"branch\":\"feature\"", Serialize(result), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Checkout_DirtyWorktree_IsBlocked()
    {
        using var fixture = GitRemoteFixture.Create();
        TestGit.Run(fixture.CloneB, "branch", "feature");
        File.WriteAllText(Path.Combine(fixture.CloneB, "dirty.txt"), "dirty");
        var service = new RepoActionService();
        var index = IndexFor(fixture.CloneB);
        var branchBefore = GitRemoteFixture.Branch(fixture.CloneB);

        var result = await service.RunAsync(
            Params("CHKOUT", new { branch = "feature" }),
            index,
            CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Equal(BridgeErrorCodes.DirtyWorktree, result.Error!.Code);
        Assert.Equal(branchBefore, GitRemoteFixture.Branch(fixture.CloneB));
        Assert.True(result.Error.Details!.ContainsKey("dirtyCount"));
    }

    [Fact]
    public async Task Checkout_MissingBranchAndTag_ReturnsInvalidArgument()
    {
        using var fixture = GitRemoteFixture.Create();
        var service = new RepoActionService();
        var index = IndexFor(fixture.CloneB);

        var result = await service.RunAsync(Params("CHKOUT"), index, CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Equal(BridgeErrorCodes.InvalidArgument, result.Error!.Code);
        Assert.Equal("branch", result.Error.Details!["field"]);
    }

    [Fact]
    public async Task Push_Success_UpdatesBareRemote()
    {
        using var fixture = GitRemoteFixture.Create();
        File.WriteAllText(Path.Combine(fixture.CloneB, "push.txt"), "push");
        TestGit.Run(fixture.CloneB, "add", "push.txt");
        var service = new RepoActionService();
        var index = IndexFor(fixture.CloneB);
        var commit = await service.RunAsync(
            Params("COMMIT", new { message = "commit for push", confirm = true }),
            index,
            CancellationToken.None);
        Assert.True(commit.Ok, Describe(commit));
        var head = GitRemoteFixture.Head(fixture.CloneB);

        // P1-2 CAS：COMMIT 后快照过期，PUSH 前必须用新 HEAD 重建索引（等价 UI 动作后 refresh）。
        index = IndexFor(fixture.CloneB);
        var result = await service.RunAsync(Params("PUSH", new { confirm = true }), index, CancellationToken.None);

        Assert.True(result.Ok, Describe(result));
        Assert.Equal(head, GitRemoteFixture.Head(fixture.BarePath));
    }

    [Fact]
    public async Task Push_Rejected_ReturnsPushRejectedWithKeyStderrLine()
    {
        using var fixture = GitRemoteFixture.Create();
        // B 本地提交，A 再推 → B push 被非快进拒绝。
        File.WriteAllText(Path.Combine(fixture.CloneB, "local.txt"), "local only");
        TestGit.CommitAll(fixture.CloneB, "local only commit");
        fixture.CommitAndPushFromA("remote diverged");
        var service = new RepoActionService();
        var index = IndexFor(fixture.CloneB);

        var result = await service.RunAsync(Params("PUSH", new { confirm = true }), index, CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Equal(BridgeErrorCodes.PushRejected, result.Error!.Code);
        Assert.Contains("exitCode", result.Error.Details!.Keys);
        // P1-6：摘要不能只有首行 "To <url>"，必须包含 rejected/non-fast-forward 关键行。
        var stderr = (string)result.Error.Details!["stderr"]!;
        Assert.True(
            stderr.Contains("rejected", StringComparison.OrdinalIgnoreCase)
            || stderr.Contains("fast-forward", StringComparison.OrdinalIgnoreCase),
            stderr);
    }

    [Fact]
    public async Task Token_ConfiguredWithoutGiteaConfig_FailsClosedAndDoesNotLeak()
    {
        using var fixture = GitRemoteFixture.Create();
        var service = new RepoActionService(giteaToken: TestToken);
        var index = IndexFor(fixture.CloneB);
        var remoteHead = GitRemoteFixture.Head(fixture.BarePath);

        var fetch = await service.RunAsync(Params("FETCH"), index, CancellationToken.None);
        Assert.False(fetch.Ok);
        Assert.Equal(BridgeErrorCodes.GiteaConfigInvalid, fetch.Error!.Code);
        AssertNoToken(Serialize(fetch));
        Assert.Equal(remoteHead, GitRemoteFixture.Head(fixture.BarePath));

        // origin 指向不存在的仓库：仍应在配置 fail-closed 阶段拒绝（不启动网络 git）。
        TestGit.Run(
            fixture.CloneB,
            "remote",
            "set-url",
            "origin",
            Path.Combine(fixture.Root, "missing-remote.git"));
        var failed = await service.RunAsync(Params("FETCH"), index, CancellationToken.None);
        Assert.False(failed.Ok);
        Assert.Equal(BridgeErrorCodes.GiteaConfigInvalid, failed.Error!.Code);
        AssertNoToken(Serialize(failed));
    }

    [Fact]
    public async Task Token_MismatchedOriginScope_ReturnsCredentialScopeWithoutLeak()
    {
        using var fixture = GitRemoteFixture.Create();
        var config = new AppConfig
        {
            GiteaBaseUrl = "https://gitea.example.test",
            GiteaOrg = "RTCRepos",
            GiteaToken = TestToken
        };
        var service = new RepoActionService(config: config);
        var index = IndexFor(fixture.CloneB);
        var remoteHead = GitRemoteFixture.Head(fixture.BarePath);

        var fetch = await service.RunAsync(Params("FETCH"), index, CancellationToken.None);

        Assert.False(fetch.Ok);
        Assert.Equal(BridgeErrorCodes.CredentialScope, fetch.Error!.Code);
        Assert.Contains("failedFields", fetch.Error.Details!.Keys);
        AssertNoToken(Serialize(fetch));
        Assert.Equal(remoteHead, GitRemoteFixture.Head(fixture.BarePath));
    }

    [Fact]
    public async Task Run_InvalidArgs_ReturnInvalidArgument()
    {
        using var fixture = GitRemoteFixture.Create();
        var service = new RepoActionService();
        var index = IndexFor(fixture.CloneB);

        var cases = new (string Action, object? Args, string Field)[]
        {
            ("CHKOUT", new { branch = "a b" }, "branch"),
            ("CHKOUT", new { branch = "--force" }, "branch"),
            ("CHKOUT", new { branch = "x..y" }, "branch"),
            ("CHKOUT", new { branch = "main", tag = "v1" }, "branch"),
            ("FETCH", new { confirm = "yes" }, "confirm"),
            ("FETCH", new { branch = 123 }, "branch"),
            ("PULL", new { setUpstream = "true" }, "setUpstream")
        };

        foreach (var (action, args, field) in cases)
        {
            var result = await service.RunAsync(Params(action, args), index, CancellationToken.None);
            Assert.False(result.Ok);
            Assert.Equal(BridgeErrorCodes.InvalidArgument, result.Error!.Code);
            Assert.Equal(field, result.Error.Details!["field"]);
        }
    }

    [Fact]
    public async Task Run_NonGitDirectory_ReturnsRepoNotLocal()
    {
        using var workspace = new TempWorkspace();
        var directory = workspace.Dir("CF100A_Demo_Impl");
        var service = new RepoActionService();
        var index = TestData.Index(
            workspace.Root,
            new ComponentSpec("CF100A_Demo", new[]
            {
                new RepoSpec("IMPL", directory)
            }));

        var result = await service.RunAsync(Params("FETCH"), index, CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Equal(BridgeErrorCodes.RepoNotLocal, result.Error!.Code);
    }

    [Fact]
    public async Task SameRepo_SameGitActions_AreSerialized()
    {
        using var fixture = GitRemoteFixture.Create();
        var serviceA = new RepoActionService();
        var serviceB = new RepoActionService();
        var index = IndexFor(fixture.CloneB);

        // 两个独立服务实例并发对同一仓 COMMIT：per-repo 信号量保证串行（一成一败/均无异常）。
        File.WriteAllText(Path.Combine(fixture.CloneB, "race-a.txt"), "a");
        File.WriteAllText(Path.Combine(fixture.CloneB, "race-b.txt"), "b");
        TestGit.Run(fixture.CloneB, "add", "race-a.txt", "race-b.txt");
        var first = serviceA.RunAsync(
            Params("COMMIT", new { message = "race a", confirm = true }), index, CancellationToken.None);
        var second = serviceB.RunAsync(
            Params("COMMIT", new { message = "race b", confirm = true }), index, CancellationToken.None);
        var results = await Task.WhenAll(first, second);

        // 至少成功一次；不得抛异常、不得出现 E_GIT_FAILED 竞态。
        Assert.Contains(results, result => result.Ok);
        Assert.DoesNotContain(results, result => result.Error?.Code == BridgeErrorCodes.GitFailed);
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

    private static SnapshotIndex IndexFor(string repoPath, string state = "SYNCED", string? headSha = null) =>
        TestData.Index(
            Path.GetDirectoryName(repoPath) ?? repoPath,
            new ComponentSpec("CF100A_Demo", new[]
            {
                new RepoSpec(
                    "IMPL",
                    repoPath,
                    State: state,
                    Branch: "main",
                    HeadSha: headSha ?? GitRemoteFixture.Head(repoPath))
            }));

    private static string Serialize(CommandResult result) => JsonSerializer.Serialize(result, WebJson);

    private static string Describe(CommandResult result) =>
        result.Ok ? "ok" : $"{result.Error?.Code}: {result.Error?.Message}";

    private static void AssertNoToken(string json)
    {
        Assert.DoesNotContain(TestToken, json, StringComparison.Ordinal);
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes(TestToken + ":"));
        Assert.DoesNotContain(basic, json, StringComparison.Ordinal);
        Assert.DoesNotContain("Authorization", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("extraHeader", json, StringComparison.OrdinalIgnoreCase);
    }
}
