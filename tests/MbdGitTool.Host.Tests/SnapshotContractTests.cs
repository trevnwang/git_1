using System;
using System.Linq;
using System.Text.Json;
using MbdGitTool.Core.Contracts;
using MbdGitTool.Core.Domain;

namespace MbdGitTool.Host.Tests;

public sealed class SnapshotContractTests
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private const string LegacySnapshotJson = """
    {
      "source": "local",
      "workspaceRoot": "D:\\Work\\MBD",
      "error": null,
      "generatedAt": "2026-11-14T02:20:44.123Z",
      "components": [
        {
          "componentId": "CF100A_Demo",
          "aggregateState": "SYNCED",
          "severityRank": 14,
          "branch": "main",
          "tag": null,
          "mixedBranch": false,
          "flags": [],
          "repos": [
            {
              "repoType": "IMPL",
              "state": "AHEAD",
              "subType": null,
              "branch": "main",
              "headSha": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
              "ahead": 1,
              "behind": 0,
              "dirtyCount": 0,
              "untrackedCount": 0,
              "isLocal": true,
              "localPath": "D:\\Work\\MBD\\CF100A_Demo_Impl",
              "flags": []
            }
          ]
        }
      ]
    }
    """;

    [Fact]
    public void LegacyJsonWithoutNewFields_Deserializes()
    {
        var snapshot = JsonSerializer.Deserialize<SnapshotDto>(LegacySnapshotJson, WebJson);

        Assert.NotNull(snapshot);
        Assert.Equal("local", snapshot!.Source);
        Assert.Null(snapshot.Counts);
        Assert.Null(snapshot.Freshness);
        Assert.Null(snapshot.Capabilities);
        var repo = Assert.Single(snapshot.Components).Repos.Single();
        Assert.Equal("AHEAD", repo.State);
        Assert.Null(repo.RepoName);
        Assert.Null(repo.CompareRef);
        Assert.Null(repo.LastCommit);
        Assert.Null(repo.Ci);
    }

    [Fact]
    public void OldPositionalConstruction_StillCompilesAndSerializes()
    {
        var snapshot = new SnapshotDto(
            "local",
            null,
            null,
            "2026-11-14T02:20:44Z",
            Array.Empty<ComponentDto>());

        var json = JsonSerializer.Serialize(snapshot, WebJson);

        Assert.Contains("\"source\":\"local\"", json, StringComparison.Ordinal);
        Assert.Contains("\"components\":[]", json, StringComparison.Ordinal);
    }

    [Fact]
    public void NewOptionalFields_SerializeCamelCase()
    {
        var repo = new RepoDto(
            "IMPL", "SYNCED", null, "main", new string('a', 40), 0, 0, 0, 0, true,
            @"D:\Work\MBD\CF100A_Demo_Impl", Array.Empty<string>(),
            RepoName: "CF100A_Demo_Impl",
            Tag: "v1.0.0",
            UpstreamRef: "refs/remotes/origin/main",
            CompareRef: "refs/remotes/origin/main",
            StashCount: 1,
            CalcAt: "2026-11-14T02:20:44Z",
            DataSource: "local+gitea",
            IsStale: false,
            ErrorCode: null,
            LastCommit: new CommitDto("sha", "short", "msg", "author", "at", Array.Empty<string>()));

        var json = JsonSerializer.Serialize(repo, WebJson);

        foreach (var key in new[]
                 {
                     "repoName", "tag", "upstreamRef", "compareRef", "stashCount", "calcAt",
                     "dataSource", "isStale", "errorCode", "lastCommit"
                 })
        {
            Assert.Contains("\"" + key + "\"", json, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ComponentAssembler_FillsNewOptionalRepoFields()
    {
        var facts = new RepoFacts
        {
            LocalPath = @"C:\Work\MBD\CF100A_Demo_Impl",
            HeadKind = HeadKind.Branch,
            Branch = "main",
            HeadSha = new string('a', 40),
            UpstreamConfigured = true,
            UpstreamRef = "refs/remotes/origin/main",
            Ahead = 0,
            Behind = 0
        };

        var components = ComponentAssembler.Assemble(new[]
        {
            new RepoInput("CF100A_Demo", RepoType.Impl, facts)
        });

        var repo = Assert.Single(Assert.Single(components).Repos);
        Assert.Equal("CF100A_Demo_Impl", repo.RepoName);
        Assert.Equal("refs/remotes/origin/main", repo.CompareRef);
        Assert.Equal("refs/remotes/origin/main", repo.UpstreamRef);
        Assert.Equal("local", repo.DataSource);
        Assert.False(repo.IsStale);
        Assert.NotNull(repo.CalcAt);
    }

    [Fact]
    public void SnapshotIndex_FindsComponentRepoAndLocalPath()
    {
        var snapshot = TestData.Snapshot(
            @"D:\Work\MBD",
            new ComponentSpec("CF100A_Demo", new[]
            {
                new RepoSpec("DESIGN", @"D:\Work\MBD\CF100A_Demo_Design"),
                new RepoSpec("IMPL", @"D:\Work\MBD\CF100A_Demo_Impl")
            }));
        var index = SnapshotIndex.Build(snapshot);

        Assert.Equal(@"D:\Work\MBD", index.WorkspaceRoot);
        Assert.True(index.TryGetComponent("cf100a_demo", out var component));
        Assert.Equal("CF100A_Demo", component.ComponentId);

        Assert.True(index.TryGetRepo("CF100A_Demo", "impl", out var repo));
        Assert.Equal("IMPL", repo.Repo.RepoType);

        Assert.True(index.TryGetRepoByPath(@"d:\work\mbd\cf100a_demo_impl\", out var byPath));
        Assert.Equal("IMPL", byPath.Repo.RepoType);

        Assert.False(index.TryGetRepoByPath(null, out _));
        Assert.False(index.TryGetComponent("NOPE", out _));
        Assert.False(SnapshotIndex.Empty.TryGetRepo("A", "IMPL", out _));
    }
}
