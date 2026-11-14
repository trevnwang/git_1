using System.Text.Json;

namespace MbdGitTool.Core.Contracts;

/// <summary>
/// M2b-2 审计查询契约（SRS §8.1 <c>operation_log</c> / §9.7 FR-OP-09 / AC-OP-33）。
/// <para>每行 = 一次单仓动作或一个 job item 的终态；<c>ts</c> 为 UTC ISO-8601。</para>
/// <para><c>target</c>/<c>before</c>/<c>after</c> 是 <c>target_json/before_json/after_json</c> 的 JSON 投影：
/// <c>target.componentId/repoType</c>、<c>before.head/state</c>、<c>after.head</c>；UI 也可直接用扁平列。</para>
/// </summary>
public sealed record OperationLogItemDto(
    long Id,
    string Ts,
    string? Actor,
    string Action,
    string? ComponentId,
    string? RepoType,
    JsonElement? Target,
    JsonElement? Before,
    JsonElement? After,
    string Result,
    string? ErrorCode,
    string? RequestId,
    string? JobId,
    string? JobItemId,
    string? BackupRef);

/// <summary><c>getOperationLog</c> 响应：<c>{ items, total }</c>（<c>total</c> = 过滤条件下的总行数，与分页无关）。</summary>
public sealed record OperationLogPageDto(IReadOnlyList<OperationLogItemDto> Items, int Total);

/// <summary>
/// <c>exportOperationLog</c> 响应：<c>{ path, count }</c>。
/// <para><c>path</c> 为绝对路径（位于 <c>%LOCALAPPDATA%\MbdGitTool\exports\</c>），供 UI 打开所在目录；
/// 导出内容仅审计元数据，**不含 token/Authorization/命令 output**。</para>
/// </summary>
public sealed record OperationLogExportDto(string Path, int Count);

/// <summary>
/// <c>repairOperationLog</c> 响应（M2b-2b / ADR D-026 P1-4）：重建 <c>operation_log</c> 并回填后的计数。
/// <para><c>removed</c> = 重建前旧表行数（含历史测试污染）；<c>inserted</c> = 从 JSONL/回退队列回填的行数；
/// <c>total</c> = 回填后 <c>operation_log</c> 行数（含本操作自身 1 条审计行）。</para>
/// </summary>
public sealed record RepairOperationLogResultDto(int Removed, int Inserted, int Total);

/// <summary>
/// 审计启动回填结果（M2b-2b / ADR D-026 P1-4）：把主 JSONL/回退队列中 SQLite 缺失行按稳定键回填后的计数。
/// <para><c>Inserted</c> = 本次新插入行数；<c>Skipped</c> = DB 已存在稳定键/重复行数；
/// <c>FallbackRemaining</c> = 回填后仍留在 <c>fallback.jsonl</c> 的行数（0 = 已清空）；
/// <c>Succeeded</c> = DB 本次是否可写（false 时保留回退队列，下次启动重试，不伪装成功）。</para>
/// </summary>
public sealed record AuditBackfillResult(int Inserted, int Skipped, int FallbackRemaining, bool Succeeded);

/// <summary>
/// <c>cleanupRetention</c> 响应：本地保留清理结果（只删本地 <c>.trash</c> 目录 / <c>refs/backup/sync/*</c> ref /
/// 超期审计行；**不触碰远端**）。
/// </summary>
public sealed record RetentionCleanupDto(
    int TrashRemoved,
    int BackupRefsRemoved,
    int AuditRowsRemoved,
    int AuditJsonlRowsRemoved,
    bool WorkspaceAvailable);
