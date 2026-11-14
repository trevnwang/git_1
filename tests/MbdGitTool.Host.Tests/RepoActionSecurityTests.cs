using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MbdGitTool.Core.Contracts;
using MbdGitTool.Core.Domain;
using MbdGitTool.Host.Bridge;
using MbdGitTool.Host.Services;

namespace MbdGitTool.Host.Tests;

/// <summary>
/// M2 P0/P1 安全负向测试：
/// PAT 作用域/fail-closed（P0-1）、ignored 覆盖（P1-1）、CHKOUT ref 存在性（P1-3）、CAS（P1-2）、
/// stderr 摘要（P1-6）、stash/stageAll、token 不泄漏、getRemoteRefs（P0-7）。
/// <para>全部使用临时 bare remote/临时仓 + 回环 TCP 抓包，不访问真实 Gitea/真实工作区。</para>
/// </summary>
public sealed class RepoActionSecurityTests
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private const string TestToken = "ghp_security_fake_token_0123456789abcdef";

    // ---------------------------------------------------------------- P0-1 凭据作用域

    [Fact]
    public async Task Token_ScopedUrlHeader_IsSentOnlyToMatchingCanonicalBase()
    {
        using var server = new LoopbackHttpCapture();
        using var repo = TemporaryGitRepo.Create();
        var origin = $"http://127.0.0.1:{server.Port}/RTCRepos/CF100A_Demo_Impl.git";
        TestGit.Run(repo.RepoPath, "remote", "add", "origin", origin);
        var config = new AppConfig
        {
            GiteaBaseUrl = $"http://127.0.0.1:{server.Port}",
            GiteaOrg = "RTCRepos",
            GiteaToken = TestToken,
            AllowInsecureHttp = true
        };
        var service = new RepoActionService(config: config);
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes(TestToken + ":"));

        var result = await service.RunAsync(Params("FETCH"), IndexFor(repo.RepoPath), CancellationToken.None);

        // git 收到 404 后失败；关键断言是 header 已按 URL 作用域注入且只出现在网络请求里。
        Assert.False(result.Ok);
        Assert.True(server.WaitForRequest(TimeSpan.FromSeconds(10)), "未捕获到 git HTTP 请求");
        var captured = server.RequestsText;
        Assert.Contains("Authorization: Basic " + basic, captured, StringComparison.Ordinal);
        Assert.DoesNotContain(TestToken, captured, StringComparison.Ordinal);
        AssertNoSecrets(Serialize(result), basic);
    }

    [Fact]
    public async Task Token_InsecureHttpWithoutExplicitAllow_IsRejectedBeforeNetwork()
    {
        using var server = new LoopbackHttpCapture();
        using var repo = TemporaryGitRepo.Create();
        TestGit.Run(
            repo.RepoPath,
            "remote",
            "add",
            "origin",
            $"http://127.0.0.1:{server.Port}/RTCRepos/CF100A_Demo_Impl.git");
        var config = new AppConfig
        {
            GiteaBaseUrl = $"http://127.0.0.1:{server.Port}",
            GiteaOrg = "RTCRepos",
            GiteaToken = TestToken,
            AllowInsecureHttp = false
        };
        var service = new RepoActionService(config: config);

        var result = await service.RunAsync(Params("FETCH"), IndexFor(repo.RepoPath), CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Equal(BridgeErrorCodes.GiteaInsecureHttp, result.Error!.Code);
        await Task.Delay(250);
        Assert.Equal(0, server.RequestCount);
        AssertNoSecrets(Serialize(result));
    }

    [Fact]
    public async Task Token_OriginHostMismatch_IsRejectedBeforeNetwork()
    {
        using var server = new LoopbackHttpCapture();
        using var repo = TemporaryGitRepo.Create();
        // host 字符串与配置不一致（localhost vs 127.0.0.1）：必须拒绝且不发起网络请求。
        TestGit.Run(
            repo.RepoPath,
            "remote",
            "add",
            "origin",
            $"http://localhost:{server.Port}/RTCRepos/CF100A_Demo_Impl.git");
        var config = new AppConfig
        {
            GiteaBaseUrl = $"http://127.0.0.1:{server.Port}",
            GiteaOrg = "RTCRepos",
            GiteaToken = TestToken,
            AllowInsecureHttp = true
        };
        var service = new RepoActionService(config: config);

        var result = await service.RunAsync(Params("FETCH"), IndexFor(repo.RepoPath), CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Equal(BridgeErrorCodes.CredentialScope, result.Error!.Code);
        Assert.Contains("fetch.host", FailedFields(result), StringComparer.Ordinal);
        await Task.Delay(250);
        Assert.Equal(0, server.RequestCount);
        AssertNoSecrets(Serialize(result));
    }

    [Fact]
    public async Task Token_FetchUrlOutsideOrg_IsRejectedBeforeNetwork()
    {
        using var server = new LoopbackHttpCapture();
        using var repo = TemporaryGitRepo.Create();
        TestGit.Run(
            repo.RepoPath,
            "remote",
            "add",
            "origin",
            $"http://127.0.0.1:{server.Port}/OtherOrg/CF100A_Demo_Impl.git");
        var config = new AppConfig
        {
            GiteaBaseUrl = $"http://127.0.0.1:{server.Port}",
            GiteaOrg = "RTCRepos",
            GiteaToken = TestToken,
            AllowInsecureHttp = true
        };
        var service = new RepoActionService(config: config);

        var result = await service.RunAsync(Params("FETCH"), IndexFor(repo.RepoPath), CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Equal(BridgeErrorCodes.CredentialScope, result.Error!.Code);
        Assert.Contains("fetch.path", FailedFields(result), StringComparer.Ordinal);
        await Task.Delay(250);
        Assert.Equal(0, server.RequestCount);
    }

    [Fact]
    public async Task Token_PushUrlMismatch_IsRejectedBeforeNetwork()
    {
        using var server = new LoopbackHttpCapture();
        using var repo = TemporaryGitRepo.Create();
        var fetchUrl = $"http://127.0.0.1:{server.Port}/RTCRepos/CF100A_Demo_Impl.git";
        TestGit.Run(repo.RepoPath, "remote", "add", "origin", fetchUrl);
        TestGit.Run(
            repo.RepoPath,
            "remote",
            "set-url",
            "--add",
            "--push",
            "origin",
            $"http://127.0.0.1:{server.Port}/EvilOrg/CF100A_Demo_Impl.git");
        var config = new AppConfig
        {
            GiteaBaseUrl = $"http://127.0.0.1:{server.Port}",
            GiteaOrg = "RTCRepos",
            GiteaToken = TestToken,
            AllowInsecureHttp = true
        };
        var service = new RepoActionService(config: config);

        var result = await service.RunAsync(Params("PUSH", new { confirm = true }), IndexFor(repo.RepoPath), CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Equal(BridgeErrorCodes.CredentialScope, result.Error!.Code);
        Assert.Contains("push.path", FailedFields(result), StringComparer.Ordinal);
        await Task.Delay(250);
        Assert.Equal(0, server.RequestCount);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("http://gitea.example", null)]
    [InlineData(null, "RTCRepos")]
    [InlineData("not-a-url", "RTCRepos")]
    [InlineData("ftp://gitea.example", "RTCRepos")]
    public async Task Token_MissingOrInvalidGiteaConfig_FailsClosedBeforeGit(string? baseUrl, string? org)
    {
        using var fixture = GitRemoteFixture.Create();
        var config = new AppConfig
        {
            GiteaBaseUrl = baseUrl,
            GiteaOrg = org,
            GiteaToken = TestToken,
            AllowInsecureHttp = true
        };
        var service = new RepoActionService(config: config);
        var headBefore = GitRemoteFixture.Head(fixture.CloneB);

        foreach (var action in new[] { "FETCH", "PULL", "PUSH" })
        {
            var args = action == "PUSH" ? (object)new { confirm = true } : null;
            var result = await service.RunAsync(Params(action, args), IndexFor(fixture.CloneB), CancellationToken.None);

            Assert.False(result.Ok);
            Assert.Equal(BridgeErrorCodes.GiteaConfigInvalid, result.Error!.Code);
            AssertNoSecrets(Serialize(result));
        }

        Assert.Equal(headBefore, GitRemoteFixture.Head(fixture.CloneB));
    }

    [Fact]
    public async Task Token_NotLeakedInHostLog()
    {
        using var fixture = GitRemoteFixture.Create();
        var token = "ghp_log_canary_" + Guid.NewGuid().ToString("N");
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes(token + ":"));
        var service = new RepoActionService(giteaToken: token);

        var result = await service.RunAsync(
            Params("FETCH"),
            IndexFor(fixture.CloneB),
            CancellationToken.None);
        Assert.False(result.Ok);
        Assert.Equal(BridgeErrorCodes.GiteaConfigInvalid, result.Error!.Code);

        var redacted = HostLog.Redact("Authorization: Basic " + basic);
        Assert.DoesNotContain(basic, redacted, StringComparison.Ordinal);
        Assert.DoesNotContain(token, redacted, StringComparison.Ordinal);

        if (File.Exists(HostLog.LogFilePath))
        {
            var log = File.ReadAllText(HostLog.LogFilePath);
            Assert.DoesNotContain(token, log, StringComparison.Ordinal);
            Assert.DoesNotContain(basic, log, StringComparison.Ordinal);
        }
    }

    // ---------------------------------------------------------------- P1-1 ignored 覆盖

    [Fact]
    public async Task Checkout_IgnoredFileWouldBeOverwritten_IsRejectedAndFileKept()
    {
        using var fixture = GitRemoteFixture.Create();
        File.WriteAllText(Path.Combine(fixture.CloneA, ".gitignore"), "*.log\n");
        TestGit.CommitAll(fixture.CloneA, "ignore logs");
        TestGit.Run(fixture.CloneA, "push", "-q", "origin", "main");
        TestGit.Run(fixture.CloneB, "pull", "-q", "--ff-only", "origin", "main");

        TestGit.Run(fixture.CloneA, "checkout", "-q", "-b", "other");
        File.WriteAllText(Path.Combine(fixture.CloneA, "build.log"), "REMOTE-LOG");
        TestGit.Run(fixture.CloneA, "add", "-f", "build.log");
        TestGit.CommitAll(fixture.CloneA, "track build log");
        TestGit.Run(fixture.CloneA, "push", "-q", "-u", "origin", "other");

        // cloneB 需要本地的 other 分支（CHKOUT 只接受 refs/heads/*），且工作区出现同路径 ignored 文件。
        TestGit.Run(fixture.CloneB, "fetch", "-q", "origin");
        TestGit.Run(fixture.CloneB, "branch", "other", "origin/other");

        var localLog = Path.Combine(fixture.CloneB, "build.log");
        File.WriteAllText(localLog, "LOCAL-PRECIOUS");
        var headBefore = GitRemoteFixture.Head(fixture.CloneB);
        var branchBefore = GitRemoteFixture.Branch(fixture.CloneB);
        var service = new RepoActionService();

        var result = await service.RunAsync(
            Params("CHKOUT", new { branch = "other" }),
            IndexFor(fixture.CloneB),
            CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Equal(BridgeErrorCodes.WorktreeOverwrite, result.Error!.Code);
        Assert.Equal("LOCAL-PRECIOUS", File.ReadAllText(localLog));
        Assert.Equal(headBefore, GitRemoteFixture.Head(fixture.CloneB));
        Assert.Equal(branchBefore, GitRemoteFixture.Branch(fixture.CloneB));
        Assert.Contains("hint", result.Error.Details!.Keys);
    }

    [Fact]
    public async Task Pull_IgnoredFileWouldBeOverwritten_IsRejectedAndFileKept()
    {
        using var fixture = GitRemoteFixture.Create();
        File.WriteAllText(Path.Combine(fixture.CloneA, ".gitignore"), "*.log\n");
        TestGit.CommitAll(fixture.CloneA, "ignore logs");
        TestGit.Run(fixture.CloneA, "push", "-q", "origin", "main");
        TestGit.Run(fixture.CloneB, "pull", "-q", "--ff-only", "origin", "main");

        File.WriteAllText(Path.Combine(fixture.CloneA, "build.log"), "REMOTE-LOG");
        TestGit.Run(fixture.CloneA, "add", "-f", "build.log");
        TestGit.CommitAll(fixture.CloneA, "track build log on main");
        TestGit.Run(fixture.CloneA, "push", "-q", "origin", "main");

        var localLog = Path.Combine(fixture.CloneB, "build.log");
        File.WriteAllText(localLog, "LOCAL-PRECIOUS");
        var headBefore = GitRemoteFixture.Head(fixture.CloneB);
        var service = new RepoActionService();

        var result = await service.RunAsync(Params("PULL"), IndexFor(fixture.CloneB), CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Equal(BridgeErrorCodes.WorktreeOverwrite, result.Error!.Code);
        Assert.Equal("LOCAL-PRECIOUS", File.ReadAllText(localLog));
        Assert.Equal(headBefore, GitRemoteFixture.Head(fixture.CloneB));
    }

    // ---------------------------------------------------------------- P1-3 ref 存在性

    [Fact]
    public async Task Checkout_NonExistentBranch_ReturnsRefNotFoundWithoutMovingHead()
    {
        using var fixture = GitRemoteFixture.Create();
        // 同名文件存在（README.md），旧实现会进入 checkout 路径模式并假成功。
        Assert.True(File.Exists(Path.Combine(fixture.CloneB, "README.md")));
        var headBefore = GitRemoteFixture.Head(fixture.CloneB);
        var branchBefore = GitRemoteFixture.Branch(fixture.CloneB);
        var service = new RepoActionService();

        var result = await service.RunAsync(
            Params("CHKOUT", new { branch = "README.md" }),
            IndexFor(fixture.CloneB),
            CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Equal(BridgeErrorCodes.RefNotFound, result.Error!.Code);
        Assert.Equal("branch", result.Error.Details!["kind"]);
        Assert.Equal(headBefore, GitRemoteFixture.Head(fixture.CloneB));
        Assert.Equal(branchBefore, GitRemoteFixture.Branch(fixture.CloneB));
    }

    [Fact]
    public async Task Checkout_NonExistentTag_ReturnsRefNotFoundWithoutDetaching()
    {
        using var fixture = GitRemoteFixture.Create();
        var headBefore = GitRemoteFixture.Head(fixture.CloneB);
        var branchBefore = GitRemoteFixture.Branch(fixture.CloneB);
        var service = new RepoActionService();

        var result = await service.RunAsync(
            Params("CHKOUT", new { tag = "v9.9.9" }),
            IndexFor(fixture.CloneB),
            CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Equal(BridgeErrorCodes.RefNotFound, result.Error!.Code);
        Assert.Equal("tag", result.Error.Details!["kind"]);
        Assert.Equal(headBefore, GitRemoteFixture.Head(fixture.CloneB));
        Assert.Equal(branchBefore, GitRemoteFixture.Branch(fixture.CloneB));
    }

    // ---------------------------------------------------------------- P1-2 CAS / TOCTOU

    [Theory]
    [InlineData("CHKOUT")]
    [InlineData("PULL")]
    [InlineData("COMMIT")]
    [InlineData("PUSH")]
    public async Task CasMismatch_WriteAction_IsRejectedWithoutChanges(string action)
    {
        using var fixture = GitRemoteFixture.Create();
        TestGit.Run(fixture.CloneB, "branch", "feature");
        var staleIndex = IndexFor(fixture.CloneB);
        var headBefore = GitRemoteFixture.Head(fixture.CloneB);

        // 外部改动 HEAD（模拟其它工具 rebase/commit）→ 快照过期。
        File.WriteAllText(Path.Combine(fixture.CloneB, "external.txt"), "external");
        TestGit.CommitAll(fixture.CloneB, "external change");
        var headAfterExternal = GitRemoteFixture.Head(fixture.CloneB);
        var branchBefore = GitRemoteFixture.Branch(fixture.CloneB);
        var service = new RepoActionService();

        object? args = action switch
        {
            "CHKOUT" => new { branch = "feature" },
            "COMMIT" => new { message = "cas should block", confirm = true },
            "PUSH" => new { confirm = true },
            _ => null
        };
        var result = await service.RunAsync(Params(action, args), staleIndex, CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Equal(BridgeErrorCodes.ConcurrentChange, result.Error!.Code);
        Assert.Contains("snapshotHead", result.Error.Details!.Keys);
        Assert.Equal(headAfterExternal, GitRemoteFixture.Head(fixture.CloneB));
        Assert.Equal(branchBefore, GitRemoteFixture.Branch(fixture.CloneB));
        Assert.True(headBefore != headAfterExternal);
    }

    [Fact]
    public async Task Router_AfterSuccessfulWrite_RefreshesSnapshotSoNextActionPassesCas()
    {
        using var fixture = GitRemoteFixture.Create();
        var config = TestAudit.NewConfig(fixture.Root);
        using var snapshots = new SnapshotService(config);
        using var router = new HostCommandRouter(config, snapshots, new RecordingLauncher());
        var snapshot = await DispatchAsync(router, "getSnapshot", null);
        Assert.True(snapshot.Ok, snapshot.Error?.Code);
        var index = (SnapshotDto)snapshot.Data!;
        var headBefore = index.Components
            .SelectMany(component => component.Repos)
            .First(repo => repo.RepoType == "IMPL" && repo.IsLocal)
            .HeadSha;

        File.WriteAllText(Path.Combine(fixture.CloneB, "first.txt"), "1");
        TestGit.Run(fixture.CloneB, "add", "first.txt");
        var first = await DispatchAsync(
            router,
            "runRepoAction",
            new { componentId = "CF100A_Demo", repoType = "IMPL", action = "COMMIT", args = new { message = "first", confirm = true } });
        Assert.True(first.Ok, first.Error?.Code);
        var headAfterFirst = GitRemoteFixture.Head(fixture.CloneB);
        Assert.NotEqual(headBefore, headAfterFirst);

        // 不显式调用 refresh：Host 在写成功后失效快照，下一次 getSnapshot/runRepoAction 自动重建（CAS 拿到新 HEAD）。
        // 第一次 COMMIT 后仓状态变为 AHEAD（COMMIT 被矩阵禁止、PUSH 允许），用 PUSH 验证新快照/新 HEAD 已生效。
        var second = await DispatchAsync(
            router,
            "runRepoAction",
            new { componentId = "CF100A_Demo", repoType = "IMPL", action = "PUSH", args = new { confirm = true } });
        Assert.True(second.Ok, second.Error?.Code);
        Assert.Equal(headAfterFirst, GitRemoteFixture.Head(fixture.BarePath));

        var refreshed = await DispatchAsync(router, "getSnapshot", null);
        Assert.True(refreshed.Ok, refreshed.Error?.Code);
        var refreshedHead = ((SnapshotDto)refreshed.Data!).Components
            .SelectMany(component => component.Repos)
            .First(repo => repo.RepoType == "IMPL" && repo.IsLocal)
            .HeadSha;
        Assert.Equal(GitRemoteFixture.Head(fixture.CloneB), refreshedHead);
    }

    // ---------------------------------------------------------------- P1-6 stderr 摘要

    [Fact]
    public async Task PushRejected_SummaryContainsRejectedReasonNotOnlyFirstLine()
    {
        using var fixture = GitRemoteFixture.Create();
        File.WriteAllText(Path.Combine(fixture.CloneB, "local.txt"), "local only");
        TestGit.CommitAll(fixture.CloneB, "local only commit");
        fixture.CommitAndPushFromA("remote diverged");
        var service = new RepoActionService();

        var result = await service.RunAsync(Params("PUSH", new { confirm = true }), IndexFor(fixture.CloneB), CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Equal(BridgeErrorCodes.PushRejected, result.Error!.Code);
        var summary = (string)result.Error.Details!["stderr"]!;
        Assert.True(
            summary.Contains("rejected", StringComparison.OrdinalIgnoreCase)
            || summary.Contains("fast-forward", StringComparison.OrdinalIgnoreCase),
            summary);
    }

    // ---------------------------------------------------------------- CHKOUT stash / COMMIT stageAll

    [Fact]
    public async Task Checkout_WithStash_SavesDirtyWorktreeAndReturnsStashRef()
    {
        using var fixture = GitRemoteFixture.Create();
        TestGit.Run(fixture.CloneB, "branch", "feature");
        File.WriteAllText(Path.Combine(fixture.CloneB, "dirty-untracked.txt"), "keep me");
        var service = new RepoActionService();

        var result = await service.RunAsync(
            Params("CHKOUT", new { branch = "feature", confirm = true, stash = true }),
            IndexFor(fixture.CloneB, state: "DIRTY"),
            CancellationToken.None);

        Assert.True(result.Ok, Describe(result));
        Assert.Equal("feature", GitRemoteFixture.Branch(fixture.CloneB));
        var json = Serialize(result);
        Assert.Contains("\"stashRef\":\"stash@{0}\"", json, StringComparison.Ordinal);
        Assert.Contains(
            RepoActionService.AutoStashMessage,
            TestGit.Capture(fixture.CloneB, "stash", "list"),
            StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(fixture.CloneB, "dirty-untracked.txt")));
    }

    [Fact]
    public async Task Commit_StageAll_RequiresConfirmAndStagesAllWhenConfirmed()
    {
        using var fixture = GitRemoteFixture.Create();
        File.WriteAllText(Path.Combine(fixture.CloneB, "untracked.txt"), "new file");
        var headBefore = GitRemoteFixture.Head(fixture.CloneB);
        var service = new RepoActionService();
        var index = IndexFor(fixture.CloneB);

        var blocked = await service.RunAsync(
            Params("COMMIT", new { message = "stage all", stageAll = true }),
            index,
            CancellationToken.None);
        Assert.False(blocked.Ok);
        Assert.Equal(BridgeErrorCodes.ConfirmRequired, blocked.Error!.Code);
        Assert.Equal(headBefore, GitRemoteFixture.Head(fixture.CloneB));

        var confirmed = await service.RunAsync(
            Params("COMMIT", new { message = "stage all", stageAll = true, confirm = true }),
            index,
            CancellationToken.None);
        Assert.True(confirmed.Ok, Describe(confirmed));
        Assert.Contains(
            "untracked.txt",
            TestGit.Capture(fixture.CloneB, "show", "--name-only", "--pretty=", "HEAD"),
            StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- P0-7 getRemoteRefs

    [Fact]
    public async Task RemoteRefs_Service_ReturnsLocalRemoteBranchesAndTags()
    {
        using var fixture = GitRemoteFixture.Create();
        TestGit.Run(fixture.CloneB, "branch", "feature");
        TestGit.Run(fixture.CloneB, "tag", "v1.0");
        var service = new RemoteRefsService();

        var result = await service.GetAsync(fixture.CloneB, CancellationToken.None);

        Assert.True(result.Ok, Describe(result));
        var dto = Assert.IsType<RemoteRefsDto>(result.Data);
        Assert.False(dto.Truncated);
        Assert.Contains(dto.Refs, item => item.RefName == "refs/heads/main" && item.Kind == "LOCAL_BRANCH");
        Assert.Contains(dto.Refs, item => item.RefName == "refs/heads/feature" && item.Kind == "LOCAL_BRANCH");
        Assert.Contains(dto.Refs, item => item.RefName == "refs/tags/v1.0" && item.Kind == "TAG");
        Assert.Contains(
            dto.Refs,
            item => item.RefName == "refs/remotes/origin/main" && item.Kind == "REMOTE_BRANCH");
        Assert.All(dto.Refs, item => Assert.NotEmpty(item.Sha));
    }

    [Fact]
    public async Task RemoteRefs_Router_ReturnsReadOnlyRefList()
    {
        using var fixture = GitRemoteFixture.Create();
        var config = TestAudit.NewConfig(fixture.Root);
        using var snapshots = new SnapshotService(config);
        using var router = new HostCommandRouter(config, snapshots, new RecordingLauncher());
        Assert.True((await DispatchAsync(router, "getSnapshot", null)).Ok);

        var result = await DispatchAsync(
            router,
            "getRemoteRefs",
            new { componentId = "CF100A_Demo", repoType = "IMPL" });

        Assert.True(result.Ok, result.Error?.Code);
        var dto = Assert.IsType<RemoteRefsDto>(result.Data);
        Assert.Contains(dto.Refs, item => item.RefName == "refs/heads/main");
    }

    [Fact]
    public async Task RemoteRefs_TruncatesAt500AndCachesFor30s()
    {
        using var fixture = GitRemoteFixture.Create();
        var head = GitRemoteFixture.Head(fixture.CloneB);
        var input = new StringBuilder();
        for (var index = 1; index <= 505; index++)
        {
            input.Append("create refs/tags/bulk-").Append(index.ToString("D3")).Append(' ').Append(head).Append('\n');
        }

        TestGit.RunWithInput(fixture.CloneB, input.ToString(), "update-ref", "--stdin");

        var clock = new ManualTimeProvider { Now = DateTimeOffset.UtcNow };
        var service = new RemoteRefsService(timeProvider: clock);
        var first = Assert.IsType<RemoteRefsDto>(
            (await service.GetAsync(fixture.CloneB, CancellationToken.None)).Data);
        Assert.True(first.Truncated);
        Assert.Equal(RemoteRefsService.MaxRefs, first.Refs.Count);
        Assert.Equal(RemoteRefsService.MaxRefs + 1, first.Total);

        // 30s 内新增的 branch 不应出现（缓存命中）。
        TestGit.Run(fixture.CloneB, "branch", "after-cache");
        var cached = Assert.IsType<RemoteRefsDto>(
            (await service.GetAsync(fixture.CloneB, CancellationToken.None)).Data);
        Assert.DoesNotContain(cached.Refs, item => item.RefName == "refs/heads/after-cache");

        clock.Now = clock.Now.Add(RemoteRefsService.CacheTtl + TimeSpan.FromSeconds(1));
        var refreshed = Assert.IsType<RemoteRefsDto>(
            (await service.GetAsync(fixture.CloneB, CancellationToken.None)).Data);
        Assert.Contains(refreshed.Refs, item => item.RefName == "refs/heads/after-cache");
    }

    // ---------------------------------------------------------------- AppConfig 显式 http

    [Fact]
    public void AppConfig_AllowInsecureHttp_FromFile_DefaultsFalse()
    {
        using var workspace = new TempWorkspace();
        var path = workspace.CreateFile(
            "config.json",
            """
            {"gitea":{"baseUrl":"http://192.168.50.20:3000","org":"RTCRepos","token":"fake","allowInsecureHttp":true}}
            """);

        var config = AppConfig.Load(path);

        Assert.True(config.AllowInsecureHttp);
        Assert.Equal("RTCRepos", config.GiteaOrg);
        Assert.DoesNotContain("fake", config.ToString(), StringComparison.Ordinal);
        Assert.False(new AppConfig().AllowInsecureHttp);
    }

    // ---------------------------------------------------------------- helpers

    private sealed record TemporaryGitRepo(string RepoPath) : IDisposable
    {
        public static TemporaryGitRepo Create()
        {
            var workspace = new TempWorkspace();
            var path = workspace.Dir("CF100A_Demo_Impl");
            TestGit.InitRepo(path);
            File.WriteAllText(System.IO.Path.Combine(path, "README.md"), "readme");
            TestGit.CommitAll(path, "init");
            return new TemporaryGitRepo(path);
        }

        public void Dispose()
        {
            var parent = Directory.GetParent(RepoPath);
            if (parent is not null)
            {
                try
                {
                    Directory.Delete(parent.FullName, recursive: true);
                }
                catch
                {
                    // 清理失败不影响断言。
                }
            }
        }
    }

    /// <summary>回环 TCP HTTP 抓包（避免 HttpListener 的 URL ACL 依赖）：记录请求头并回 404。</summary>
    private sealed class LoopbackHttpCapture : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cancellation = new();
        private readonly ConcurrentQueue<string> _requests = new();
        private readonly Task _acceptLoop;

        public LoopbackHttpCapture()
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _acceptLoop = AcceptLoopAsync(_cancellation.Token);
        }

        public int Port { get; }

        public int RequestCount => _requests.Count;

        public string RequestsText => string.Join("\n---\n", _requests);

        public bool WaitForRequest(TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (_requests.Count > 0)
                {
                    return true;
                }

                Thread.Sleep(25);
            }

            return _requests.Count > 0;
        }

        private async Task AcceptLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (SocketException)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }

                _ = HandleClientAsync(client);
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            try
            {
                using (client)
                {
                    var stream = client.GetStream();
                    var buffer = new byte[16 * 1024];
                    var received = new MemoryStream();
                    while (received.Length < 16 * 1024)
                    {
                        var read = await stream.ReadAsync(buffer, CancellationToken.None).ConfigureAwait(false);
                        if (read <= 0)
                        {
                            break;
                        }

                        received.Write(buffer, 0, read);
                        var text = Encoding.ASCII.GetString(received.ToArray());
                        if (text.Contains("\r\n\r\n", StringComparison.Ordinal))
                        {
                            break;
                        }
                    }

                    _requests.Enqueue(Encoding.ASCII.GetString(received.ToArray()));
                    var response = Encoding.ASCII.GetBytes(
                        "HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
                    await stream.WriteAsync(response, CancellationToken.None).ConfigureAwait(false);
                    await stream.FlushAsync(CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch
            {
                // 抓包失败不影响测试断言（连接关闭/取消均可能发生）。
            }
        }

        public void Dispose()
        {
            _cancellation.Cancel();
            try
            {
                _listener.Stop();
            }
            catch
            {
                // 已停止。
            }

            try
            {
                _acceptLoop.Wait(TimeSpan.FromSeconds(2));
            }
            catch
            {
                // 取消等待超时可忽略。
            }

            _cancellation.Dispose();
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow() => Now;
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

    private static IReadOnlyList<string> FailedFields(CommandResult result) =>
        Assert.IsAssignableFrom<IEnumerable<string>>(result.Error!.Details!["failedFields"]).ToList();

    private static string Serialize(CommandResult result) => JsonSerializer.Serialize(result, WebJson);

    private static string Describe(CommandResult result) =>
        result.Ok ? "ok" : $"{result.Error?.Code}: {result.Error?.Message}";

    private static void AssertNoSecrets(string json, string? base64Token = null)
    {
        Assert.DoesNotContain(TestToken, json, StringComparison.Ordinal);
        var basic = base64Token ?? Convert.ToBase64String(Encoding.UTF8.GetBytes(TestToken + ":"));
        Assert.DoesNotContain(basic, json, StringComparison.Ordinal);
        // 错误文案可出现 header 名（提示风险），但绝不能出现注入的配置键/值。
        Assert.DoesNotContain("extraHeader", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GIT_CONFIG_VALUE", json, StringComparison.OrdinalIgnoreCase);
    }

    private static Task<CommandResult> DispatchAsync(
        HostCommandRouter router,
        string method,
        object? parameters)
    {
        JsonElement? element = parameters is null
            ? null
            : JsonSerializer.SerializeToElement(parameters, WebJson);
        return router.DispatchAsync(method, element, CancellationToken.None);
    }
}
