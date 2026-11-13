using System;
using System.Collections.Generic;
using MbdGitTool.Core.Contracts;
using MbdGitTool.Core.Domain;

namespace MbdGitTool.Host.Services;

/// <summary>CI 状态 v1 Mock（code-plan §3.10）：<c>enabled=false</c>，全部 NONE；CI 永不参与聚合/卡片底色。</summary>
public sealed class CiStatusService
{
    private static readonly string[] CiStates = { "PASS", "FAIL", "RUNNING", "QUEUED", "NONE", "UNKNOWN" };

    private readonly Func<SnapshotIndex> _indexProvider;

    public CiStatusService(Func<SnapshotIndex> indexProvider) =>
        _indexProvider = indexProvider ?? throw new ArgumentNullException(nameof(indexProvider));

    public CommandResult Get(GetCiStatusParams parameters)
    {
        parameters ??= new GetCiStatusParams();
        var index = _indexProvider();
        var snapshot = index.Snapshot;
        if (snapshot is null)
        {
            return CommandResult.Failure(
                BridgeErrorCodes.WorkspaceUnset,
                "工作区未就绪（快照尚未完成），请先刷新");
        }

        ComponentDto? component = null;
        var componentId = parameters.ComponentId?.Trim();
        if (!string.IsNullOrEmpty(componentId))
        {
            if (!index.TryGetComponent(componentId, out var found))
            {
                return CommandResult.Failure(
                    BridgeErrorCodes.NotFound,
                    $"未找到组件：{componentId}",
                    new Dictionary<string, object?> { ["componentId"] = componentId });
            }

            component = found;
        }
        else if (index.Components.Count > 0)
        {
            component = index.Components[0];
        }

        var repoCount = component?.Repos.Count ?? 0;
        var byState = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var state in CiStates)
        {
            byState[state] = state == "NONE" ? repoCount : 0;
        }

        var aggregate = new ComponentCiAggregateDto(
            ComponentId: component?.ComponentId ?? string.Empty,
            ByState: byState,
            WorstState: "NONE",
            LastFailed: null,
            DataFreshAt: snapshot.GeneratedAt);

        return CommandResult.Success(new CiStatusDto(
            Enabled: false,
            Provider: "mock",
            FetchedAt: null,
            Latest: Array.Empty<CiLatestDto>(),
            Aggregate: aggregate));
    }
}
