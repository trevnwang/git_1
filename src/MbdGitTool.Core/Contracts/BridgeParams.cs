using System.Text.Json;

namespace MbdGitTool.Core.Contracts;

/// <summary>
/// 桥请求信封（code-plan §3.3）。解析后 <see cref="Id"/> 原样回显；<see cref="Params"/> 由路由反序列化为具体参数记录。
/// </summary>
public sealed record BridgeRequest(string? Id, string? Method, JsonElement? Params);

public sealed record GetComponentDetailParams(string? ComponentId = null);

public sealed record GetRepoDetailParams(string? ComponentId = null, string? RepoType = null);

public sealed record GetCommitsParams(
    string? ComponentId = null,
    string? RepoType = null,
    int? Skip = null,
    int? Take = null);

/// <summary>getRepoTree 参数：spec 的 <c>path</c> 相对仓根；兼容任务书 <c>repoId</c>/<c>repoPath</c> 别名。</summary>
public sealed record GetRepoTreeParams(
    string? ComponentId = null,
    string? RepoType = null,
    string? RepoId = null,
    string? RepoPath = null,
    string? Path = null,
    string? RelativePath = null,
    int? Depth = null,
    int? Limit = null,
    string? Cursor = null,
    bool? IncludeGitStatus = null,
    bool? IncludeIgnored = null,
    bool? IncludeHidden = null);

/// <summary>getWorkspaceTree 参数：<c>path</c> 相对工作区根，空串 = 根。</summary>
public sealed record GetWorkspaceTreeParams(
    string? Path = null,
    int? Depth = null,
    int? Limit = null,
    string? Cursor = null,
    bool? IncludeHidden = null,
    bool? IncludeMissingRepos = null);

/// <summary>openPath 参数：<c>componentId+repoType</c> 可选（提供时做仓根二次收口）。</summary>
public sealed record OpenPathParams(
    string? Path = null,
    string? ComponentId = null,
    string? RepoType = null,
    string? Mode = null,
    bool? Confirm = null);

/// <summary>runRepoAction 参数（M1 只读：返回 E_NOT_IMPLEMENTED）。</summary>
public sealed record RunRepoActionParams(
    string? ComponentId = null,
    string? RepoType = null,
    string? Action = null,
    IReadOnlyDictionary<string, JsonElement>? Args = null);

public sealed record GetCiStatusParams(string? ComponentId = null);
