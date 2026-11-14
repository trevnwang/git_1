using System.Text.Json;

namespace MbdGitTool.Core.Contracts;

/// <summary>
/// 桥请求信封（code-plan §3.3）。解析后 <see cref="Id"/> 原样回显；<see cref="Params"/> 由路由反序列化为具体参数记录。
/// </summary>
public sealed record BridgeRequest(string? Id, string? Method, JsonElement? Params);

public sealed record GetComponentDetailParams(string? ComponentId = null);

/// <summary>M2b 组件纳入参数（AC-OP-31）。</summary>
public sealed record BindComponentParams(string? ComponentId = null, string? DisplayName = null);

/// <summary>M2b 组件移出参数（AC-OP-32）。</summary>
public sealed record UnbindComponentParams(string? ComponentId = null, string? Mode = null);

/// <summary>M2b CREATE 撤销参数（AC-OP-25）。</summary>
public sealed record UndoCreateParams(string? JobId = null);

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

/// <summary>getRemoteRefs 参数：按组件 + 仓类型解析本地仓（供 CHKOUT 选择器使用）。</summary>
public sealed record GetRemoteRefsParams(string? ComponentId = null, string? RepoType = null);

/// <summary>
/// runBatch 参数（M2b）：<c>{ kind, componentIds?, repoTypes?, args? }</c>。
/// <para><c>kind</c> ∈ FETCH/PULL/CHKOUT/COMMIT/PUSH/SYNC/CLONE/DELETE/RESTORE/CREATE/TAG；</para>
/// <para><c>repoTypes</c> 缺省 = DESIGN/IMPL/MIL/SIL 四仓；<c>componentIds</c> 必填（禁止隐式“全组织批量”）；</para>
/// <para><c>args</c>：branch/tag/message/confirm/dryRun/backup（另兼容 setUpstream/stash/stageAll/trashStamp）。</para>
/// </summary>
public sealed record RunBatchParams(
    string? Kind = null,
    IReadOnlyList<string>? ComponentIds = null,
    IReadOnlyList<string>? RepoTypes = null,
    IReadOnlyDictionary<string, JsonElement>? Args = null);

/// <summary>getJobs 参数：<c>{ limit? }</c>（默认 50，上限 200，创建时间倒序）。</summary>
public sealed record GetJobsParams(int? Limit = null);

/// <summary>getJob / cancelJob / retryFailed 参数：<c>{ jobId }</c>。</summary>
public sealed record GetJobParams(string? JobId = null);

public sealed record CancelJobParams(string? JobId = null);

public sealed record RetryFailedParams(string? JobId = null);

/// <summary>
/// M2b-2 <c>getOperationLog</c> 参数：<c>{ limit?, offset?, from?, to?, action?, componentId?, result? }</c>。
/// <para><c>from/to</c> 支持 UTC ISO-8601 日期或时间（日期粒度按整天闭区间）；<c>limit</c> 默认 100 / 上限 1000。</para>
/// </summary>
public sealed record GetOperationLogParams(
    int? Limit = null,
    int? Offset = null,
    string? From = null,
    string? To = null,
    string? Action = null,
    string? ComponentId = null,
    string? Result = null);

/// <summary>
/// M2b-2 <c>exportOperationLog</c> 参数：<c>{ format:'csv'|'json', from?, to? }</c>；
/// 导出文件固定写 <c>%LOCALAPPDATA%\MbdGitTool\exports\</c>（不接受调用方路径）。
/// </summary>
public sealed record ExportOperationLogParams(
    string? Format = null,
    string? From = null,
    string? To = null,
    string? Action = null,
    string? ComponentId = null,
    string? Result = null);

/// <summary>M2b-2 <c>getBackupRefs</c> 参数：按组件 + 可选仓类型列出本地 <c>refs/backup/sync/*</c>。</summary>
public sealed record GetBackupRefsParams(string? ComponentId = null, string? RepoType = null);

/// <summary>
/// M2b-2 <c>restoreBackupRef</c> 参数：<c>{ componentId, repoType, ref, confirm:true }</c>。
/// <para>强写门禁：必须 <c>confirm=true</c>（Host 侧 E_CONFIRM_REQUIRED 双保险）；<c>ref</c> 必须属于
/// <c>refs/backup/sync/</c> 且存在。</para>
/// </summary>
public sealed record RestoreBackupRefParams(
    string? ComponentId = null,
    string? RepoType = null,
    string? Ref = null,
    bool? Confirm = null);

/// <summary>M2b-2 <c>cleanupRetention</c> 参数（可空：按配置保留期清理本地 .trash / 备份 ref / 超期审计行）。</summary>
public sealed record CleanupRetentionParams();

/// <summary>
/// M2b-2b <c>repairOperationLog</c> 参数：<c>{ confirm:true }</c>。
/// <para>破坏性修复入口：删除并重建 <c>operation_log</c> 表，再从 JSONL/回退队列回填（用于清理历史测试污染）；
/// 必须显式 <c>confirm=true</c>（Host 侧 <c>E_CONFIRM_REQUIRED</c> 双保险）。</para>
/// </summary>
public sealed record RepairOperationLogParams(bool? Confirm = null);

