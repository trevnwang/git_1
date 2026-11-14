namespace MbdGitTool.Core.Contracts;

/// <summary>
/// M2b 作业契约（SRS §8.1 <c>job</c>/<c>job_item</c>、§9.7 FR-OP-08、§13.1.2 AC-UI-10c）。
/// <para>所有状态一律 UPPER_SNAKE 字符串（与 SRS 枚举同名），camelCase 由 Host 序列化。</para>
/// <para>job.state：<c>PENDING/RUNNING/PARTIAL/SUCCESS/FAILED/CANCELED/INTERRUPTED</c>（部分成功统一 PARTIAL）。</para>
/// <para>jobItem.state：<c>PENDING/RUNNING/OK/FAILED/SKIPPED/CANCELED/INTERRUPTED</c>。</para>
/// </summary>
public sealed record JobDto(
    string Id,
    string Kind,
    string State,
    int Total,
    int Ok,
    int Failed,
    int Skipped,
    string CreatedAt,
    string? StartedAt,
    string? FinishedAt,
    bool Cancellable,
    IReadOnlyList<JobItemDto> Items);

/// <summary>
/// 作业逐仓结果（<c>job_item</c>）：componentId + repoType 唯一定位一个仓；
/// <c>preHead/postHead/backupRef</c> 供任务中心展示与回滚入口使用（不含 token/output）。
/// </summary>
public sealed record JobItemDto(
    string ComponentId,
    string RepoType,
    string? RepoName,
    string State,
    string? StartedAt,
    string? FinishedAt,
    string? ErrorCode,
    string? Message,
    string? PreHead,
    string? PostHead,
    string? BackupRef);

/// <summary><c>getJobs</c> 响应：<c>{ jobs: JobDto[] }</c>（按创建时间倒序，最多 <c>limit</c> 条）。</summary>
public sealed record JobsDto(IReadOnlyList<JobDto> Jobs);

/// <summary>
/// <c>runBatch</c> 响应：<c>{ jobId, accepted, total, skipped }</c>。
/// <para><c>accepted</c> = 作业是否受理入队（与 <c>runRepoAction.accepted</c> 同为 boolean）；</para>
/// <para><c>total</c> = 展开后的 item 总数；<c>skipped</c> = 受理阶段即判定为 SKIPPED 的 item 数（如组件不在快照）。</para>
/// </summary>
public sealed record RunBatchResultDto(string JobId, bool Accepted, int Total, int Skipped);

/// <summary><c>cancelJob</c> 响应：<c>{ jobId, state }</c>（终态作业幂等返回当前状态）。</summary>
public sealed record CancelJobResultDto(string JobId, string State);

/// <summary>
/// <c>retryFailed</c> 响应：<c>{ jobId(new), accepted, retried }</c>。
/// <para>无可重试项（FAILED/INTERRUPTED/CANCELED）时 <c>JobId=null</c>、<c>Accepted=false</c>、<c>Retried=0</c>，不创建空作业。</para>
/// </summary>
public sealed record RetryFailedResultDto(string? JobId, bool Accepted, int Retried = 0);
