using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MbdGitTool.Core.Contracts;
using MbdGitTool.Core.Domain;
using MbdGitTool.Host.Services;
using Xunit;

namespace MbdGitTool.Host.Tests;

/// <summary>AC-UI-08 单源矩阵：Host GetAvailability 必须与 reqs/fixtures/m2-matrix.json 逐格一致。</summary>
public sealed class RepoActionAvailabilityMatrixTests
{
    private static readonly string[] Actions = { "PULL", "FETCH", "CHKOUT", "COMMIT", "PUSH" };

    [Fact]
    public void GetAvailability_MatchesSingleSourceFixture_ForAllStatesAndActions()
    {
        var fixturePath = FindFixture();
        Assert.True(File.Exists(fixturePath), $"夹具不存在：{fixturePath}");

        using var doc = JsonDocument.Parse(File.ReadAllText(fixturePath));
        Assert.Equal("aligned", doc.RootElement.GetProperty("status").GetString());
        var states = doc.RootElement.GetProperty("states").EnumerateArray().ToArray();
        Assert.Equal(14, states.Length);

        var service = new RepoActionService();
        foreach (var stateEntry in states)
        {
            var state = stateEntry.GetProperty("state").GetString()!;
            var actual = service.GetAvailability(Repo(state)).ToDictionary(item => item.Action);
            var expected = stateEntry.GetProperty("actions");

            foreach (var action in Actions)
            {
                var cell = expected.GetProperty(action);
                var item = actual[action];
                Assert.Equal(cell.GetProperty("enabled").GetBoolean(), item.Enabled);
                Assert.Equal(cell.GetProperty("requiresConfirm").GetBoolean(), item.RequiresConfirm);
                Assert.True(
                    string.Equals(cell.GetProperty("reasonCode").GetString(), item.ReasonCode, StringComparison.Ordinal),
                    $"{state}/{action} fixture={cell.GetProperty("reasonCode").GetString()} actual={item.ReasonCode} enabled={item.Enabled} confirm={item.RequiresConfirm}");
                Assert.True(item.MatrixAllowed == item.Enabled);
            }
        }
    }

    private static string FindFixture()
    {
        // 优先使用 csproj Content 复制到输出目录的夹具（普通/--artifacts-path 构建均可）。
        var copied = Path.Combine(AppContext.BaseDirectory, "fixtures", "m2-matrix.json");
        if (File.Exists(copied))
        {
            return copied;
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "reqs", "fixtures", "m2-matrix.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return Path.Combine("reqs", "fixtures", "m2-matrix.json");
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
}
