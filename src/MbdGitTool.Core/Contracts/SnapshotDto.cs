namespace MbdGitTool.Core.Contracts;

/// <summary>
/// UI ↔ Host 数据契约（M1）。JSON 由 Host 以 camelCase 序列化后经 WebView2 WebMessage 下发。
/// 所有状态/枚举一律用 SRS 的 UPPER_SNAKE 字符串，避免前后端枚举序列化不一致。
/// <para>v3 追加字段全部为可选参数：旧构造调用源码兼容、旧 JSON 反序列化兼容、旧前端忽略新字段（spec 附录 B.1）。</para>
/// </summary>
public sealed record SnapshotDto(
    string Source,                 // "mock" | "local" | "local+gitea"
    string? WorkspaceRoot,
    string? Error,
    string GeneratedAt,            // ISO-8601 UTC
    IReadOnlyList<ComponentDto> Components,
    CountsDto? Counts = null,
    FreshnessDto? Freshness = null,
    CapabilitiesDto? Capabilities = null,
    string? Owner = null,           // Gitea 组织/所有者（标题栏显示，v3）
    string? GiteaBaseUrl = null);   // 设置页认证展示（v3）

public sealed record ComponentDto(
    string ComponentId,
    string AggregateState,         // SRS §7.4（OP_PARTIAL_FAILED/BINDING_ERROR/.../SYNCED）
    int SeverityRank,              // D-020（数值越小越严重）
    string? Branch,                // 聚合展示分支（Impl 优先；不一致时由 mixedBranch 标记）
    string? Tag,
    bool MixedBranch,
    IReadOnlyList<string> Flags,
    IReadOnlyList<RepoDto> Repos,
    bool? Incomplete = null,
    string? BindingCause = null,
    string? LastActivityAt = null,
    ComponentCiAggregateDto? CiAggregate = null);

public sealed record RepoDto(
    string RepoType,               // DESIGN/IMPL/MIL/SIL
    string State,                  // SRS §7.2
    string? SubType,               // NO_UPSTREAM 子型：LOCAL_ONLY/UNSET_BUT_EXISTS/UPSTREAM_GONE
    string? Branch,
    string? HeadSha,
    int Ahead,
    int Behind,
    int DirtyCount,
    int UntrackedCount,
    bool IsLocal,
    string? LocalPath,
    IReadOnlyList<string> Flags,
    string? RepoName = null,
    string? Tag = null,
    string? UpstreamRef = null,
    string? CompareRef = null,
    int StashCount = 0,
    string? CalcAt = null,
    string? DataSource = null,
    bool? IsStale = null,
    string? ErrorCode = null,
    CommitDto? LastCommit = null,
    CiLatestDto? Ci = null,
    int StagedCount = 0);

public sealed record CountsDto(int Components, int Repos, int MissingRepos);

public sealed record FreshnessDto(
    string? LocalScanAt,
    string? RemoteFetchAt,
    bool LocalStale,
    bool RemoteStale);

public sealed record CapabilitiesDto(
    bool FileTree,
    bool WorkspaceTree,
    bool OpenPath,
    bool RepoActions,
    bool Ci);
