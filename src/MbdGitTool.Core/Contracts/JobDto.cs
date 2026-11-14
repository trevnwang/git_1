namespace MbdGitTool.Core.Contracts;

/// <summary>
/// M2b 作业契约（SRS §8.1 <c>job</c>/<c>job_item</c>、§9.7 FR-OP-08、§13.1.2 AC-UI-10c）。
/// <para>所有状态一律 UPPER_SNAKE 字符串（与 SRS 枚举同名），camelCase 由 Host 序列化。</para>
/// <para>job.state：<c>PENDING/RUNNING/PARTIAL/SUCCESS/FAILED/CANCELED/INTERRUPTED/PREVIEW</c>
/// （取消枚举固定 <c>CANCELED</c>；<c>PREVIEW</c> 仅用于 <c>args.dryRun=true</c> 预览作业，表示“只预检、零写”）。</para>
/// <para>jobItem.state：<c>PENDING/RUNNING/OK/FAILED/SKIPPED/CANCELED/INTERRUPTED/PREVIEW</c>。</para>
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
    bool DryRun,
    bool CancelRequested,
    string? Message,
    IReadOnlyList<JobItemDto> Items);

/// <summary>
/// 作业逐仓结果（<c>job_item</c>）：componentId + repoType 唯一定位一个仓；
/// <c>preHead/postHead/backupRef</c> 供任务中心展示与回滚入口使用（不含 token/output）。
/// <para><c>preview=true</c> 仅出现在 dryRun 作业：该 item 只做只读预检，<c>state=PREVIEW</c>，仓库零写。</para>
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
    string? BackupRef,
    bool Preview = false,
  string? ExpectHead = null,
  string? BaselineRef = null);

/// <summary><c>getJobs</c> 响应：<c>{ jobs: JobDto[] }</c>（按创建时间倒序，最多 <c>limit</c> 条）。</summary>
public sealed record JobsDto(IReadOnlyList<JobDto> Jobs);

/// <summary>
/// <c>runBatch</c> 响应：<c>{ jobId, accepted, total, skipped }</c>。
/// <para><c>accepted</c> = 作业是否受理入队（与 <c>runRepoAction.accepted</c> 同为 boolean）；</para>
/// <para><c>total</c> = 展开后的 item 总数；<c>skipped</c> = 受理阶段即判定为 SKIPPED 的 item 数（如组件不在快照）。</para>
/// </summary>
public sealed record RunBatchResultDto(string JobId, bool Accepted, int Total, int Skipped);

/// <summary><c>cancelJob</c> 响应：<c>{ jobId, state }</c>（仅 PENDING/RUNNING 可取消；终态作业返回错误码）。</summary>
public sealed record CancelJobResultDto(string JobId, string State);

/// <summary>
/// <c>retryFailed</c> 响应：<c>{ jobId(new), accepted, retried }</c>。
/// <para>无可重试项（FAILED/INTERRUPTED/CANCELED）时 <c>JobId=null</c>、<c>Accepted=false</c>、<c>Retried=0</c>，不创建空作业。</para>
/// </summary>
public sealed record RetryFailedResultDto(string? JobId, bool Accepted, int Retried = 0);

/// <summary>
/// <c>JOB_UPDATED</c> 事件信封（AC-UI-10c②，Host→UI 单通道推送，无请求 id）。
/// <para>固定形状：<c>{ event:"JOB_UPDATED", jobId, current:JobDto, data:JobDto, state, total, cancellable }</c>；</para>
/// <para><c>current</c> 与 <c>data</c> 是同一份 job 快照（<c>data</c> 为 M2b 首版兼容键，新 UI 只依赖
/// <c>jobId</c>/<c>current</c>/<c>state</c>/<c>total</c>/<c>cancellable</c>）；取消后 <c>cancellable=false</c>、
/// <c>state</c> 固定 <c>CANCELED</c>（不是 <c>CANCELLED</c>）。</para>
/// </summary>
public sealed record JobUpdatedEventDto(
    string Event,
    string JobId,
    JobDto Current,
    JobDto Data,
    string State,
    int Total,
    bool Cancellable);
