namespace MbdGitTool.Core.Contracts;

/// <summary>
/// 文件树节点（spec §4.4 / code-plan §4.4.5）。
/// <para><c>Path</c> 为 posix（<c>/</c>）分隔：getRepoTree 相对仓根；getWorkspaceTree 相对工作区根。</para>
/// </summary>
public sealed record TreeNodeDto(
    string Path,
    string Name,
    string Kind,                 // "DIR" | "FILE"
    bool HasChildren,
    long? Size,
    DateTimeOffset? Mtime,
    string GitStatus,            // NONE/MODIFIED/ADDED/DELETED/UNTRACKED/CONFLICT
    bool Submodule,
    bool Ignored,
    IReadOnlyList<TreeNodeDto>? Children,
    string? Error,
    bool? IsReparsePoint = null);

/// <summary>四仓文件树响应（spec §4.4 + code-plan §3.6）。</summary>
public sealed record RepoTreeDto(
    string ComponentId,
    string RepoType,
    string RepoName,
    string RootPath,
    string Path,
    string? Branch,
    string? HeadSha,
    string State,
    int DirtyCount,
    string? CompareRef,
    string? FetchedAt,
    string ReadAt,
    IReadOnlyList<TreeNodeDto> Entries,
    bool Truncated,
    int TotalEntries,
    string? Error,
    int Limit,
    string? NextCursor);

/// <summary>
/// 文件夹视图节点（spec §7.4）：<c>COMPONENT</c>/<c>REPO</c>/<c>UNBOUND_GROUP</c> 为组织节点，
/// <c>DIR</c>/<c>FILE</c> 为物理条目（字段与 <see cref="TreeNodeDto"/> 对齐）。
/// </summary>
public sealed record WorkspaceNodeDto(
    string Kind,
    string? Name = null,
    string? Path = null,
    bool? HasChildren = null,
    string? ComponentId = null,
    string? State = null,
    int? SeverityRank = null,
    int? DirtyCount = null,
    bool? MixedBranch = null,
    IReadOnlyList<WorkspaceNodeDto>? Repos = null,
    int? Count = null,
    IReadOnlyList<WorkspaceNodeDto>? Children = null,
    string? RepoType = null,
    bool? IsLocal = null,
    string? Branch = null,
    int? Ahead = null,
    int? Behind = null,
    long? Size = null,
    DateTimeOffset? Mtime = null,
    string? GitStatus = null,
    bool? Submodule = null,
    bool? Ignored = null,
    bool? IsReparsePoint = null,
    string? Error = null);

/// <summary>工作区树响应（spec §7.4 / code-plan §3.7）。</summary>
public sealed record WorkspaceTreeDto(
    string Root,
    string Path,
    string GeneratedAt,
    string ReadAt,
    IReadOnlyList<WorkspaceNodeDto> Entries,
    bool Truncated,
    string? Error);

/// <summary>openPath 结果（spec §3.8）。</summary>
public sealed record OpenPathResultDto(
    bool Opened,
    string Mode,
    string Path,
    string AbsolutePath,
    string? Extension,
    string ExtensionClass,       // DIRECTORY/SAFE_TEXT/DANGEROUS/OTHER
    bool IsReparsePoint);
