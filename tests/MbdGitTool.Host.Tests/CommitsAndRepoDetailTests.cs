using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MbdGitTool.Core.Contracts;
using MbdGitTool.Core.Domain;
using MbdGitTool.Host.Services;

namespace MbdGitTool.Host.Tests;

public sealed class CommitsAndRepoDetailTests : IDisposable
{
    private readonly TempWorkspace _ws = new();

    public void Dispose() => _ws.Dispose();

    [Fact]
    public async Task GetCommits_ReturnsNewestFirstAndTotal()
    {
        var repo = CreateRepoWithCommits(3);
        var service = new CommitsService();

        var result = await service.GetAsync(repo, 0, 5, CancellationToken.None);

        Assert.True(result.Ok);
        var page = Assert.IsType<CommitsPageDto>(result.Data);
        Assert.Equal(3, page.Total);
        Assert.Equal(3, page.Items.Count);
        Assert.Equal("commit 3", page.Items[0].Message);
        Assert.Equal("commit 1", page.Items[2].Message);
        Assert.All(page.Items, item =>
        {
            Assert.Equal(40, item.Sha.Length);
            Assert.Equal(7, item.ShortSha.Length);
            Assert.Equal("Test", item.Author);
            Assert.False(string.IsNullOrWhiteSpace(item.At));
        });
    }

    [Fact]
    public async Task GetCommits_SkipAndTake_PaginateWithoutOverlap()
    {
        var repo = CreateRepoWithCommits(3);
        var service = new CommitsService();

        var first = (CommitsPageDto)(await service.GetAsync(repo, 0, 1, CancellationToken.None)).Data!;
        var second = (CommitsPageDto)(await service.GetAsync(repo, 1, 1, CancellationToken.None)).Data!;

        Assert.Equal("commit 3", first.Items.Single().Message);
        Assert.Equal("commit 2", second.Items.Single().Message);
        Assert.Equal(3, first.Total);
        Assert.Equal(3, second.Total);
    }

    [Fact]
    public async Task GetCommits_UnbornRepository_ReturnsEmptyPage()
    {
        var repo = _ws.Dir("empty-repo");
        TestGit.InitRepo(repo);
        var service = new CommitsService();

        var result = await service.GetAsync(repo, 0, 5, CancellationToken.None);

        Assert.True(result.Ok);
        var page = Assert.IsType<CommitsPageDto>(result.Data);
        Assert.Equal(0, page.Total);
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task GetCommits_NonGitDirectory_ReturnsGitFailed()
    {
        var plain = _ws.Dir("plain");
        var service = new CommitsService();

        var result = await service.GetAsync(plain, 0, 5, CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Equal(BridgeErrorCodes.GitFailed, result.Error!.Code);
    }

    [Fact]
    public async Task GetCommits_MissingDirectory_ReturnsRepoNotLocal()
    {
        var service = new CommitsService();

        var result = await service.GetAsync(_ws.PathFor("missing"), 0, 5, CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Equal(BridgeErrorCodes.RepoNotLocal, result.Error!.Code);
    }

    [Fact]
    public async Task RepoDetail_ReturnsRepoComponentAndRecentCommits()
    {
        var repo = CreateRepoWithCommits(3);
        var index = SnapshotIndex.Build(TestData.Snapshot(
            _ws.Root,
            new ComponentSpec("CF100A_Demo", new[] { new RepoSpec("IMPL", repo) })));
        var service = new RepoDetailService(() => index, new CommitsService());

        var result = await service.GetAsync(
            new GetRepoDetailParams("CF100A_Demo", "IMPL"),
            CancellationToken.None);

        Assert.True(result.Ok);
        var detail = Assert.IsType<RepoDetailResponse>(result.Data);
        Assert.Equal("IMPL", detail.Repo.RepoType);
        Assert.Equal("CF100A_Demo", detail.Component.ComponentId);
        Assert.Equal(3, detail.Commits.Total);
        Assert.Equal(3, detail.Commits.Items.Count);
    }

    [Fact]
    public async Task RepoDetail_MissingRepo_ReturnsEmptyCommitsWithoutGit()
    {
        var index = TestData.Index(
            _ws.Root,
            new ComponentSpec("CF100A_Demo", new[]
            {
                new RepoSpec("IMPL", _ws.PathFor("CF100A_Demo_Impl"), IsLocal: false, State: "MISSING")
            }));
        var service = new RepoDetailService(() => index, new CommitsService());

        var result = await service.GetAsync(
            new GetRepoDetailParams("CF100A_Demo", "IMPL"),
            CancellationToken.None);

        Assert.True(result.Ok);
        var detail = Assert.IsType<RepoDetailResponse>(result.Data);
        Assert.Equal(0, detail.Commits.Total);
        Assert.Empty(detail.Commits.Items);
    }

    [Fact]
    public async Task RepoDetail_UnknownRepo_ReturnsNotFound()
    {
        var index = TestData.Index(
            _ws.Root,
            new ComponentSpec("CF100A_Demo", new[] { new RepoSpec("IMPL", _ws.PathFor("CF100A_Demo_Impl")) }));
        var service = new RepoDetailService(() => index, new CommitsService());

        var result = await service.GetAsync(
            new GetRepoDetailParams("NOPE", "IMPL"),
            CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Equal(BridgeErrorCodes.NotFound, result.Error!.Code);
    }

    private string CreateRepoWithCommits(int count)
    {
        var repo = _ws.Dir("CF100A_Demo_Impl");
        TestGit.InitRepo(repo);
        for (var index = 1; index <= count; index++)
        {
            _ws.CreateFile("CF100A_Demo_Impl/file.txt", "content " + index);
            TestGit.CommitAll(repo, "commit " + index);
        }

        return repo;
    }
}
