namespace MbdGitTool.Core.Contracts;

/// <summary>getComponentDetail 响应（组件级：四仓 + action 可用性 + CI 汇总占位）。</summary>
public sealed record ComponentDetailDto(
    string ComponentId,
    string AggregateState,
    int SeverityRank,
    string? Branch,
    string? Tag,
    bool MixedBranch,
    bool? Incomplete,
    string? BindingCause,
    IReadOnlyList<string> Flags,
    string? WorkspaceRoot,
    string GeneratedAt,
    CiHintDto Ci,
    IReadOnlyList<ComponentRepoDto> Repos);

/// <summary>getComponentDetail 内的单仓行（RepoDto + RepoName/CompareRef 等 + actions）。</summary>
public sealed record ComponentRepoDto(
    string RepoType,
    string RepoName,
    string State,
    string? SubType,
    string? Branch,
    string? Tag,
    string? HeadSha,
    string? UpstreamRef,
    string? CompareRef,
    int Ahead,
    int Behind,
    int DirtyCount,
    int UntrackedCount,
    int StashCount,
    bool IsLocal,
    string? LocalPath,
    bool IsStale,
    string? CalcAt,
    string? DataSource,
    string? ErrorCode,
    IReadOnlyList<string> Flags,
    IReadOnlyList<RepoActionAvailabilityDto> Actions,
    CiHintDto? Ci);

/// <summary>单仓操作可用性（M1 enabled 恒 false，matrixAllowed 取 spec §6.2 矩阵）。</summary>
public sealed record RepoActionAvailabilityDto(
    string Action,
    bool Enabled,
    bool MatrixAllowed,
    string? ReasonCode,
    string? Reason,
    bool RequiresConfirm,
    string Stage);

/// <summary>getRepoDetail 响应（右栏“概览/最近提交”）。</summary>
public sealed record RepoDetailResponse(
    RepoDto Repo,
    ComponentDto Component,
    CommitsPageDto Commits);

/// <summary>提交分页（getCommits / getRepoDetail.commits）。</summary>
public sealed record CommitsPageDto(int Total, IReadOnlyList<CommitDto> Items);

/// <summary>getHostInfo 响应（版本/特性/限制；供 UI 能力探测与降级）。</summary>
public sealed record HostInfoDto(
    string AppVersion,
    string? WebView2Version,
    HostFeaturesDto Features,
    HostLimitsDto Limits);

public sealed record HostFeaturesDto(
    bool FileTree,
    bool WorkspaceTree,
    bool OpenPath,
    bool RepoActions,
    bool Ci,
    bool ComponentDetail);

public sealed record HostLimitsDto(
    int MaxDirectoryEntries,
    int MaxDepth,
    int MaxRequestBodyBytes,
    int MaxResponseBytes,
    int MaxCommitsPerPage);
