namespace MbdGitTool.Core.Contracts;

/// <summary>CI 单次运行契约（spec 附录 B.1，冻结；v1 由 Mock 提供，enabled=false）。</summary>
public sealed record CiLatestDto(
    int RepoId,
    string ComponentId,
    string RepoType,
    string Branch,
    string CommitHash,
    string State,                // PASS/FAIL/RUNNING/QUEUED/NONE/UNKNOWN
    string? PipelineId,
    int? BuildNumber,
    string? FailedStage,
    string? StartedAt,
    string? FinishedAt,
    long? DurationMs,
    string? Url,
    string Provider,
    string FetchedAt);

/// <summary>CI 失败摘要（ComponentCiAggregate.lastFailed）。</summary>
public sealed record CiFailureDto(
    string RepoType,
    int? BuildNumber,
    string? FailedStage,
    string? Url);

/// <summary>组件级 CI 聚合（CI 独立通道：永不参与组件聚合/卡片底色/排序）。</summary>
public sealed record ComponentCiAggregateDto(
    string ComponentId,
    IReadOnlyDictionary<string, int> ByState,
    string WorstState,
    CiFailureDto? LastFailed,
    string DataFreshAt);

/// <summary>CI 提示（v1 Mock：enabled=false，state=NONE）。</summary>
public sealed record CiHintDto(bool Enabled, string State, string Provider, string? FetchedAt);

/// <summary>getCiStatus 响应（spec §3.10）。</summary>
public sealed record CiStatusDto(
    bool Enabled,
    string Provider,
    string? FetchedAt,
    IReadOnlyList<CiLatestDto> Latest,
    ComponentCiAggregateDto Aggregate);
