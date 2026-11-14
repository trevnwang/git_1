using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MbdGitTool.Core.Contracts;
using MbdGitTool.Host.Bridge;
using MbdGitTool.Host.Services;
using Xunit;

namespace MbdGitTool.Host.Tests;

/// <summary>AC-OP-33 审计-lite：每次操作成功/失败写 JSONL，且不包含 token/output。</summary>
public sealed class OperationAuditServiceTests : IDisposable
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private readonly string _root = Path.Combine(Path.GetTempPath(), "mbd-audit-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Record_WritesJsonLineWithMetadataAndNoSecrets()
    {
        var service = new OperationAuditService(_root);

        service.Record(
            action: "PUSH",
            componentId: "CF100A_Demo",
            repoType: "IMPL",
            state: "AHEAD",
            preHead: "1111111111111111111111111111111111111111",
            postHead: "2222222222222222222222222222222222222222",
            ok: true,
            errorCode: null,
            durationMs: 123,
            actor: "tester",
            requestId: "req-1");

        var path = Path.Combine(_root, OperationAuditService.AuditFileName);
        Assert.True(File.Exists(path), "审计文件未创建");
        var line = Assert.Single(File.ReadAllLines(path));
        using var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;
        Assert.Equal("PUSH", root.GetProperty("action").GetString());
        Assert.Equal("CF100A_Demo", root.GetProperty("componentId").GetString());
        Assert.Equal("IMPL", root.GetProperty("repoType").GetString());
        Assert.Equal("AHEAD", root.GetProperty("state").GetString());
        Assert.True(root.GetProperty("ok").GetBoolean());
        Assert.Equal(123, root.GetProperty("durationMs").GetInt64());
        Assert.True(root.TryGetProperty("ts", out _));
        Assert.Equal("tester", root.GetProperty("actor").GetString());
        Assert.Equal("req-1", root.GetProperty("requestId").GetString());
        Assert.DoesNotContain("token", line, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Authorization", line, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("output", line, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Record_Failure_WritesErrorCodeAndCreatesDirectory()
    {
        var service = new OperationAuditService(_root);

        service.Record(
            action: "COMMIT",
            componentId: "CF100A_Demo",
            repoType: "SIL",
            state: "DIRTY",
            preHead: null,
            postHead: null,
            ok: false,
            errorCode: "E_NOTHING_TO_COMMIT",
            durationMs: 5);

        var line = Assert.Single(File.ReadAllLines(service.FilePath));
        using var doc = JsonDocument.Parse(line);
        Assert.False(doc.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("E_NOTHING_TO_COMMIT", doc.RootElement.GetProperty("errorCode").GetString());
    }

    [Fact]
    public void Record_ConcurrentAppends_WriteOneLinePerCall()
    {
        var service = new OperationAuditService(_root);

        Parallel.For(0, 32, index => service.Record(
            "PULL", "CF100A_Demo", "IMPL", "SYNCED", null, null, index % 2 == 0, "E_GIT_FAILED", index));

        var lines = File.ReadAllLines(service.FilePath);
        Assert.Equal(32, lines.Length);
        foreach (var line in lines)
        {
            using var doc = JsonDocument.Parse(line);
            Assert.Equal("PULL", doc.RootElement.GetProperty("action").GetString());
        }
    }

    [Fact]
    public async Task Router_RunRepoAction_Success_WritesOneAuditLineWithoutSecrets()
    {
        const string token = "ghp_audit_router_fake_token_0123456789";
        using var fixture = GitRemoteFixture.Create();
        var config = TestAudit.NewConfig(fixture.Root, token);
        using var snapshots = new SnapshotService(config);
        using var router = new HostCommandRouter(config, snapshots, new RecordingLauncher());
        Assert.True((await DispatchAsync(router, "getSnapshot", null)).Ok);

        var headBefore = GitRemoteFixture.Head(fixture.CloneB);
        File.WriteAllText(Path.Combine(fixture.CloneB, "audit-commit.txt"), "x");
        TestGit.Run(fixture.CloneB, "add", "audit-commit.txt");
        var result = await DispatchAsync(router, "runRepoAction", new
        {
            componentId = "CF100A_Demo",
            repoType = "IMPL",
            action = "COMMIT",
            args = new { message = "audit commit", confirm = true }
        });

        Assert.True(result.Ok, result.Error?.Code);
        var path = Path.Combine(config.AuditRoot!, OperationAuditService.AuditFileName);
        Assert.True(File.Exists(path), "HostCommandRouter 未写审计行");
        var line = Assert.Single(File.ReadAllLines(path));
        using var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;
        Assert.Equal("COMMIT", root.GetProperty("action").GetString());
        Assert.Equal("CF100A_Demo", root.GetProperty("componentId").GetString());
        Assert.Equal("IMPL", root.GetProperty("repoType").GetString());
        Assert.True(root.GetProperty("ok").GetBoolean());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("errorCode").ValueKind);
        Assert.Equal(headBefore, root.GetProperty("preHead").GetString());
        Assert.Equal(GitRemoteFixture.Head(fixture.CloneB), root.GetProperty("postHead").GetString());
        Assert.True(root.GetProperty("durationMs").GetInt64() >= 0);
        var ts = root.GetProperty("ts").GetString();
        Assert.True(
            DateTimeOffset.TryParse(
                ts,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsed),
            $"ts 不是 UTC ISO-8601：{ts}");
        Assert.Equal(TimeSpan.Zero, parsed.Offset);

        // 不写 token / base64 / 命令 output（FR-SAFE-03）。
        Assert.DoesNotContain(token, line, StringComparison.Ordinal);
        Assert.DoesNotContain(
            Convert.ToBase64String(Encoding.UTF8.GetBytes(token + ":")),
            line,
            StringComparison.Ordinal);
        Assert.DoesNotContain("output", line, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Router_RunRepoAction_Failure_WritesOneAuditLine()
    {
        using var fixture = GitRemoteFixture.Create();
        var config = TestAudit.NewConfig(fixture.Root);
        using var snapshots = new SnapshotService(config);
        using var router = new HostCommandRouter(config, snapshots, new RecordingLauncher());
        Assert.True((await DispatchAsync(router, "getSnapshot", null)).Ok);
        var headBefore = GitRemoteFixture.Head(fixture.CloneB);

        // 干净仓 COMMIT（已带 confirm）→ E_NOTHING_TO_COMMIT；失败同样在动作返回前写 1 行审计。
        var result = await DispatchAsync(router, "runRepoAction", new
        {
            componentId = "CF100A_Demo",
            repoType = "IMPL",
            action = "COMMIT",
            args = new { message = "nothing to commit", confirm = true }
        });

        Assert.False(result.Ok);
        Assert.Equal(BridgeErrorCodes.NothingToCommit, result.Error!.Code);
        var path = Path.Combine(config.AuditRoot!, OperationAuditService.AuditFileName);
        var line = Assert.Single(File.ReadAllLines(path));
        using var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;
        Assert.Equal("COMMIT", root.GetProperty("action").GetString());
        Assert.False(root.GetProperty("ok").GetBoolean());
        Assert.Equal(BridgeErrorCodes.NothingToCommit, root.GetProperty("errorCode").GetString());
        Assert.Equal(headBefore, root.GetProperty("preHead").GetString());
        Assert.Equal(headBefore, root.GetProperty("postHead").GetString());
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

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch
        {
            // 临时目录清理失败不影响测试结论。
        }
    }
}
