using System;
using System.IO;
using System.Text.Json;
using MbdGitTool.Host.Services;
using Xunit;

namespace MbdGitTool.Host.Tests;

/// <summary>AC-OP-33 审计-lite：每次操作成功/失败写 JSONL，且不包含 token/output。</summary>
public sealed class OperationAuditServiceTests : IDisposable
{
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
            durationMs: 123);

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
