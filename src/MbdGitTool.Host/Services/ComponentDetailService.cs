using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MbdGitTool.Core.Contracts;
using MbdGitTool.Core.Domain;

namespace MbdGitTool.Host.Services;

/// <summary>
/// getComponentDetail（code-plan §4.9）：组件级四仓 + action 可用性 + CI 占位汇总。
/// <para>只读 <see cref="SnapshotIndex"/>，不触发 git；找不到 → <c>E_NOT_FOUND</c>。</para>
/// </summary>
public sealed class ComponentDetailService
{
    private readonly Func<SnapshotIndex> _indexProvider;
    private readonly RepoActionService _actions;

    public ComponentDetailService(Func<SnapshotIndex> indexProvider, RepoActionService actions)
    {
        _indexProvider = indexProvider ?? throw new ArgumentNullException(nameof(indexProvider));
        _actions = actions ?? throw new ArgumentNullException(nameof(actions));
    }

    public CommandResult Get(GetComponentDetailParams parameters)
    {
        parameters ??= new GetComponentDetailParams();

        if (string.IsNullOrWhiteSpace(parameters.ComponentId))
        {
            return CommandResult.Failure(
                BridgeErrorCodes.InvalidArgument,
                "缺少必填参数 componentId",
                new Dictionary<string, object?> { ["field"] = "componentId" });
        }

        var index = _indexProvider();
        var snapshot = index.Snapshot;
        if (snapshot is null)
        {
            return CommandResult.Failure(
                BridgeErrorCodes.WorkspaceUnset,
                "工作区未就绪（快照尚未完成），请先刷新");
        }

        if (!index.TryGetComponent(parameters.ComponentId, out var component))
        {
            return CommandResult.Failure(
                BridgeErrorCodes.NotFound,
                $"未找到组件：{parameters.ComponentId}",
                new Dictionary<string, object?> { ["componentId"] = parameters.ComponentId });
        }

        var repos = new List<ComponentRepoDto>(component.Repos.Count);
        foreach (var repo in component.Repos)
        {
            var repoRef = new RepoRef(component.ComponentId, component, repo);
            repos.Add(new ComponentRepoDto(
                RepoType: repo.RepoType,
                RepoName: RepoResolver.RepoName(repoRef),
                State: repo.State,
                SubType: repo.SubType,
                Branch: repo.Branch,
                Tag: repo.Tag,
                HeadSha: repo.HeadSha,
                UpstreamRef: repo.UpstreamRef,
                CompareRef: repo.CompareRef ?? repo.UpstreamRef,
                Ahead: repo.Ahead,
                Behind: repo.Behind,
                DirtyCount: repo.DirtyCount,
                UntrackedCount: repo.UntrackedCount,
                StashCount: repo.StashCount,
                IsLocal: repo.IsLocal,
                LocalPath: repo.LocalPath,
                IsStale: repo.IsStale ?? false,
                CalcAt: repo.CalcAt,
                DataSource: repo.DataSource,
                ErrorCode: repo.ErrorCode,
                Flags: repo.Flags,
                Actions: _actions.GetAvailability(repoRef),
                Ci: null));
        }

        var detail = new ComponentDetailDto(
            ComponentId: component.ComponentId,
            AggregateState: component.AggregateState,
            SeverityRank: component.SeverityRank,
            Branch: component.Branch,
            Tag: component.Tag,
            MixedBranch: component.MixedBranch,
            Incomplete: component.Incomplete ?? component.Flags.Contains("incomplete"),
            BindingCause: component.BindingCause,
            Flags: component.Flags,
            WorkspaceRoot: snapshot.WorkspaceRoot,
            GeneratedAt: snapshot.GeneratedAt,
            Ci: new CiHintDto(Enabled: false, State: "NONE", Provider: "mock", FetchedAt: null),
            Repos: repos);

        return CommandResult.Success(detail);
    }
}
