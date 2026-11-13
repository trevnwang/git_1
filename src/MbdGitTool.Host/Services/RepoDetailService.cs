using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MbdGitTool.Core.Contracts;
using MbdGitTool.Core.Domain;

namespace MbdGitTool.Host.Services;

/// <summary>
/// getRepoDetail（spec §5.2 / code-plan §4.6）：快照仓事实 + 本地路径 + 最近 5 条提交。
/// <para>MISSING/INVALID/无本地路径 → <c>commits={total:0,items:[]}</c>（不报错、不调 git）；git 失败 <c>E_GIT_FAILED</c>。</para>
/// </summary>
public sealed class RepoDetailService
{
    private readonly Func<SnapshotIndex> _indexProvider;
    private readonly CommitsService _commits;

    public RepoDetailService(Func<SnapshotIndex> indexProvider, CommitsService commits)
    {
        _indexProvider = indexProvider ?? throw new ArgumentNullException(nameof(indexProvider));
        _commits = commits ?? throw new ArgumentNullException(nameof(commits));
    }

    public async Task<CommandResult> GetAsync(
        GetRepoDetailParams parameters,
        CancellationToken cancellationToken)
    {
        parameters ??= new GetRepoDetailParams();
        var index = _indexProvider();
        if (index.Snapshot is null)
        {
            return CommandResult.Failure(
                BridgeErrorCodes.WorkspaceUnset,
                "工作区未就绪（快照尚未完成），请先刷新");
        }

        var resolveError = RepoResolver.TryResolve(index, parameters.ComponentId, parameters.RepoType, out var repo);
        if (resolveError is not null)
        {
            return CommandResult.Failure(resolveError);
        }

        var state = repo.Repo.State.ToUpperInvariant();
        var canReadGit = repo.Repo.IsLocal
            && !string.IsNullOrWhiteSpace(repo.Repo.LocalPath)
            && Directory.Exists(repo.Repo.LocalPath)
            && state is not ("INVALID" or "MISSING" or "EMPTY");

        CommitsPageDto commits;
        if (canReadGit)
        {
            var result = await _commits
                .GetAsync(repo.Repo.LocalPath!, 0, 5, cancellationToken)
                .ConfigureAwait(false);
            if (!result.Ok)
            {
                return result;
            }

            commits = (CommitsPageDto)result.Data!;
        }
        else
        {
            commits = new CommitsPageDto(0, Array.Empty<CommitDto>());
        }

        return CommandResult.Success(new RepoDetailResponse(repo.Repo, repo.Component, commits));
    }
}
