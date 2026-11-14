/**
 * UI ↔ Host 只读数据通道（v3 工作台）。
 *
 * 契约（docs/ui-redesign-spec.md 附录 B.2 / docs/ui-redesign-code-plan.md §3）：
 *   UI  → Host：window.chrome.webview.postMessage({ id, method, params })
 *   Host → UI：{ id, ok: true, data } / { id, ok: false, code, error, details }
 * 兼容 M1 旧桥（getSnapshot/refresh 回 { id, ok, data } 或 { id, snapshot }）。
 *
 * 无 WebView2（浏览器 dev / CDP）时所有方法 reject，由 store 回退内置 Mock；
 * 真实 Host 可用时自动使用真实数据。测试可用 window.__DSH_TEST_BRIDGE__ 注入 Mock 桥。
 * DTO 字段以 camelCase 为准，解析时兼容 PascalCase（Host 序列化差异）。
 */

/**
 * 单仓动作可用性（M2a 单源矩阵：Host getComponentDetail.repos[].actions）。
 * UI 只消费该结构，不再本地维护任何状态×动作矩阵（AC-UI-08）。
 */
export interface RepoActionAvailabilityDto {
  action: string
  enabled: boolean
  matrixAllowed?: boolean | null
  /** Host 原因码（STATE_BLOCKED / CONFIRM_REQUIRED / AVAILABLE / E_*）。 */
  reasonCode?: string | null
  /** Host 给出的原因文案（禁用时用于 tooltip「原因：…」）。 */
  reason?: string | null
  /** Host 是否要求该动作二次确认（确认条/confirm 标志的唯一来源）。 */
  requiresConfirm?: boolean | null
  /** Host 阶段（M1/M2a/M2b）。 */
  stage?: string | null
}

export interface RepoDto {
  repoType: string
  repoName?: string
  state: string
  subType: string | null
  branch: string | null
  tag?: string | null
  headSha: string | null
  upstreamRef?: string | null
  compareRef?: string | null
  ahead: number
  behind: number
  dirtyCount: number
  untrackedCount: number
  stagedCount?: number
  stashCount?: number
  isLocal: boolean
  localPath: string | null
  isStale?: boolean | null
  calcAt?: string | null
  dataSource?: string | null
  errorCode?: string | null
  flags: string[]
  /** getComponentDetail.repos[].actions（Host 单源矩阵）；快照仓无该字段。 */
  actions?: RepoActionAvailabilityDto[] | null
  ci?: CiLatestDto | null
}

export interface CiLatestDto {
  repoId: number
  componentId: string
  repoType: 'DESIGN' | 'IMPL' | 'MIL' | 'SIL'
  branch: string
  commitHash: string
  state: 'PASS' | 'FAIL' | 'RUNNING' | 'QUEUED' | 'NONE' | 'UNKNOWN'
  pipelineId?: string
  buildNumber?: number
  failedStage?: string
  startedAt?: string
  finishedAt?: string
  durationMs?: number
  url?: string
  provider: string
  fetchedAt: string
}

export interface CiAggregateDto {
  componentId: string
  byState: Record<string, number>
  worstState: string
  lastFailed?: { repoType: string; buildNumber?: number; failedStage?: string; url?: string }
  dataFreshAt: string
}

export interface ComponentDto {
  componentId: string
  aggregateState: string
  severityRank: number
  branch: string | null
  tag: string | null
  mixedBranch: boolean
  flags: string[]
  repos: RepoDto[]
  incomplete?: boolean | null
  bindingCause?: string | null
  lastActivityAt?: string | null
  ciAggregate?: CiAggregateDto | null
}

export interface CountsDto {
  components: number
  repos: number
  missingRepos?: number
}

export interface FreshnessDto {
  localScanAt: string | null
  remoteFetchAt: string | null
  localStale: boolean
  remoteStale: boolean
}

export interface CapabilitiesDto {
  fileTree?: boolean
  workspaceTree?: boolean
  openPath?: boolean
  repoActions?: boolean
  ci?: boolean
}

export interface SnapshotDto {
  source: string
  workspaceRoot: string | null
  error: string | null
  generatedAt: string
  components: ComponentDto[]
  counts?: CountsDto | null
  freshness?: FreshnessDto | null
  owner?: string | null
  giteaBaseUrl?: string | null
  /** Host 能力开关（M2：repoActions=true）；UI 能力门禁的兜底来源（HostInfo 缺失时）。 */
  capabilities?: CapabilitiesDto | null
  /**
   * 远程阶段离线/不可达标记（M2b-2 Host 可选字段）。
   * 缺省时由 `source=local` + `error` / `freshness.remoteStale` 保守推断，绝不伪造“在线”。
   */
  offline?: boolean | null
  /** 远程阶段错误摘要（离线原因；服务端可选，不含 token）。 */
  remoteError?: string | null
}

export interface TreeNodeDto {
  path: string
  name: string
  kind: 'DIR' | 'FILE'
  hasChildren: boolean
  size?: number | null
  mtime?: string | null
  gitStatus?: string | null
  submodule?: boolean
  ignored?: boolean
  children?: TreeNodeDto[] | null
  error?: string | null
}

export interface RepoTreeDto {
  componentId: string
  repoType: string
  repoName?: string
  rootPath?: string
  path: string
  branch?: string | null
  headSha?: string | null
  state?: string
  dirtyCount?: number
  compareRef?: string | null
  fetchedAt?: string | null
  entries: TreeNodeDto[]
  truncated?: boolean
  totalEntries?: number
  error?: string | null
  errorCode?: string | null
}

export interface WorkspaceNodeDto {
  kind: string
  name?: string
  path?: string
  componentId?: string
  repoType?: string
  state?: string
  severityRank?: number
  dirtyCount?: number
  mixedBranch?: boolean
  branch?: string | null
  ahead?: number
  behind?: number
  count?: number
  repos?: WorkspaceNodeDto[]
  children?: WorkspaceNodeDto[]
  hasChildren?: boolean
  isLocal?: boolean
  gitStatus?: string | null
  size?: number | null
  mtime?: string | null
  submodule?: boolean
  ignored?: boolean
  error?: string | null
}

export interface WorkspaceTreeDto {
  root: string
  path: string
  generatedAt?: string
  readAt?: string
  entries: WorkspaceNodeDto[]
  truncated?: boolean
  totalEntries?: number
  error?: string | null
  errorCode?: string | null
}

export interface CommitDto {
  sha: string
  shortSha?: string
  message: string
  author: string
  at: string
  refs?: string[]
}

export interface RepoDetailDto {
  repo: RepoDto | null
  component?: ComponentDto | null
  commits: { total: number; items: CommitDto[] }
}

export interface OpenPathResultDto {
  opened: boolean
  mode: string
  path?: string
  errorCode?: string
  message?: string
}

/** runRepoAction 参数（契约：{ componentId, repoType, action, args }）。 */
export interface RepoActionArgs {
  branch?: string
  tag?: string
  message?: string
  /** 二次确认后重试时置 true（Host 返回 E_CONFIRM_REQUIRED 时）。 */
  confirm?: boolean
  /** COMMIT：显式勾选「暂存全部改动（git add -A）」时为 true（Host 要求同时 confirm=true）。 */
  stageAll?: boolean
  /** PUSH：NO_UPSTREAM 发布分支时置 true，Host 以 `git push -u origin HEAD` 建立 upstream。 */
  setUpstream?: boolean
  /** CHKOUT：工作区 dirty 时置 true，Host 先 `git stash push -u` 再检出（M2 P0-7）。 */
  stash?: boolean
}

/** runRepoAction 成功响应（M2：Host 返回执行结果；M1 仍拒绝 E_NOT_IMPLEMENTED）。 */
export interface RepoActionResultDto {
  jobId?: string
  accepted: boolean
  action?: string
  componentId?: string
  repoType?: string
  state?: string
  preHead?: string
  postHead?: string
  branch?: string
  output?: string
  durationMs?: number
  errorCode?: string
  message?: string
  /** CHKOUT 自动 stash 成功时的引用（如 stash@{0}），供 Toast 提示恢复入口。 */
  stashRef?: string
}

// ───────────────────────────── M2b 作业（Job）DTO ─────────────────────────────

/** 批量作业类型（runBatch.kind；Host 以自身能力为准，未知 kind 由 Host 返回错误码）。 */
export type JobKind =
  | 'SYNC'
  | 'CREATE'
  | 'CLONE'
  | 'DELETE'
  | 'RESTORE'
  | 'TAG'
  | 'PULL'
  | 'FETCH'
  | 'CHKOUT'
  | 'COMMIT'
  | 'PUSH'

/** job.state 合法值（契约 8 态；PREVIEW 仅用于 dryRun 预览作业“只预检、零写”）。 */
export type JobState =
  | 'PENDING'
  | 'RUNNING'
  | 'PARTIAL'
  | 'SUCCESS'
  | 'FAILED'
  | 'CANCELED'
  | 'INTERRUPTED'
  | 'PREVIEW'

/** job_item.state（Host 契约：PENDING/RUNNING/OK/FAILED/SKIPPED/CANCELED/INTERRUPTED/PREVIEW；兼容 SUCCESS）。 */
export type JobItemState =
  | 'PENDING'
  | 'RUNNING'
  | 'OK'
  | 'SUCCESS'
  | 'FAILED'
  | 'SKIPPED'
  | 'CANCELED'
  | 'INTERRUPTED'
  | 'PREVIEW'

export interface JobItemDto {
  componentId: string
  repoType: string
  repoName?: string | null
  state: JobItemState
  startedAt?: string | null
  finishedAt?: string | null
  errorCode?: string | null
  message?: string | null
  preHead?: string | null
  postHead?: string | null
  /** SYNC 等备份类作业的恢复引用（refs/backup/sync/...）。 */
  backupRef?: string | null
  /** dryRun 预览项：只做只读预检、仓库零写（Host M2b-1 修复后新增）。 */
  preview?: boolean
}

export interface JobDto {
  id: string
  kind: JobKind
  state: JobState
  total: number
  ok: number
  failed: number
  skipped: number
  createdAt: string
  startedAt?: string | null
  finishedAt?: string | null
  cancellable: boolean
  items: JobItemDto[]
  /** dryRun 预览作业（只预检、零写；Host M2b-1 修复后新增字段）。 */
  dryRun?: boolean
  /** 已请求取消（合作式取消中）。 */
  cancelRequested?: boolean
  /** job 级说明（取消/内部原因等，可空）。 */
  message?: string | null
  /**
   * 部分快照（事件信封只带 jobId/state/total/cancellable，无完整 items）：
   * store 合并时保留已有 kind/items，避免明细被冲掉。正常 getJobs/getJob 响应不带该字段。
   */
  partial?: boolean
}

/** runBatch 参数（契约：{kind,componentIds?,repoTypes?,args?}）。 */
export interface RunBatchParams {
  kind: JobKind | string
  componentIds?: string[]
  repoTypes?: string[]
  args?: BatchArgs
}

/** 批量参数：branch/tag/confirm 与可选的 dryRun 预览。 */
export interface BatchArgs {
  branch?: string
  tag?: string
  message?: string
  backupRef?: string
  /** 仅预览，不写入（可选能力；Host 不支持时忽略）。 */
  dryRun?: boolean
  /** 二次确认后重试时置 true。 */
  confirm?: boolean
  [key: string]: unknown
}

/** runBatch 响应（契约：{jobId,accepted,total,skipped}）。 */
export interface RunBatchResultDto {
  jobId: string
  accepted: boolean
  total: number
  skipped: number
}

/** getJobs 响应（契约：{jobs}）。 */
export interface JobsDto {
  jobs: JobDto[]
}

// ───────────────────────────── M2b-2 操作审计 / 备份 ref ─────────────────────────────

/**
 * `getOperationLog` 审计行（契约）：
 * `{id,ts,actor,action,componentId,repoType,result,errorCode,requestId,jobId,jobItemId,backupRef,beforeHead,afterHead}`
 * 成功与失败各 1 行；`result` 取值 OK/FAILED/SKIPPED/CANCELED/INTERRUPTED（Host 追加时冻结）。
 */
export interface OperationLogItemDto {
  /** 行主键（Host 可能回 number 或 string，UI 统一转 string）。 */
  id: string
  ts: string
  actor: string
  action: string
  componentId: string | null
  repoType: string | null
  result: string
  errorCode: string | null
  requestId: string | null
  jobId: string | null
  jobItemId: string | null
  backupRef: string | null
  beforeHead: string | null
  afterHead: string | null
}

/** `getOperationLog` 参数（全部可选；分页 + 时间/动作/组件/结果过滤）。 */
export interface OperationLogParams {
  limit?: number
  offset?: number
  /** 时间下界（ISO-8601，含）。 */
  from?: string
  /** 时间上界（ISO-8601，含）。 */
  to?: string
  /** 动作过滤（如 PUSH/SYNC；Host 大小写不敏感）。 */
  action?: string
  componentId?: string
  /** 结果过滤（OK/FAILED/SKIPPED/CANCELED/INTERRUPTED）。 */
  result?: string
}

/** `getOperationLog` 响应（契约：{items,total}）。 */
export interface OperationLogPageDto {
  items: OperationLogItemDto[]
  total: number
}

/** `exportOperationLog` 参数（契约：{format:'csv'|'json'}）。 */
export interface ExportOperationLogParams {
  format: 'csv' | 'json'
}

/** `exportOperationLog` 响应（契约：{path,count}）。 */
export interface ExportOperationLogResultDto {
  path: string
  count: number
}

/** `getBackupRefs` 的单个备份 ref（契约：{ref,sha,createdAt,repoType}）。 */
export interface BackupRefDto {
  ref: string
  sha: string
  createdAt: string
  repoType: string
  /** M2b-2b：备份来源分支（旧格式为 null）。 */
  branch?: string | null
  /** M2b-2b：LEGACY | BRANCH。 */
  format?: string
}

/** `getBackupRefs` 参数（契约：{componentId,repoType?}）。 */
export interface BackupRefsParams {
  componentId: string
  repoType?: string
}

/** `getBackupRefs` 响应（契约：{refs:[…]}）。 */
export interface BackupRefsDto {
  refs: BackupRefDto[]
}

/** `restoreBackupRef` 参数（契约：{componentId,repoType,ref,confirm:true}）。 */
export interface RestoreBackupRefParams {
  componentId: string
  repoType: string
  ref: string
  confirm: true
}

/** `restoreBackupRef` 响应（契约 `{ok|restored,preHead,postHead,backupRef|ref}`；兼容 Host `{restored,ref,safetyRef,message}`）。 */
export interface RestoreBackupRefResultDto {
  ok: boolean
  preHead: string | null
  postHead: string | null
  backupRef: string | null
  /** 恢复前 HEAD 的安全备份 ref（Host M2b-2 `safetyRef`；空仓可空）。 */
  safetyRef?: string | null
  /** Host 返回的说明（可空）。 */
  message?: string | null
}

/** `cleanupRetention` 响应（可选能力：清理超过保留期的审计/备份记录）。 */
export interface CleanupRetentionResultDto {
  /** 被清理/回收的记录数（Host 未返回时 0）。 */
  removed: number
}

/** ref 种类（getRemoteRefs，M2 P0-7）。 */
export type RemoteRefKind = 'LOCAL_BRANCH' | 'REMOTE_BRANCH' | 'TAG'

export interface RemoteRefDto {
  /** 完整 ref 名，如 refs/heads/main、refs/remotes/origin/x、refs/tags/v1。 */
  refName: string
  sha: string
  /** 短 SHA（Host 未提供时由 sha 截取 7 位）。 */
  shortSha: string
  kind: RemoteRefKind
  objectType?: string
}

/** getRemoteRefs 响应（M2 P0-7）：本地/远程分支 + Tag，供 CHKOUT 选择器搜索。 */
export interface RemoteRefsDto {
  refs: RemoteRefDto[]
  truncated: boolean
  total: number
  generatedAt?: string
}

export interface CiStatusDto {
  enabled?: boolean
  provider?: string
  fetchedAt?: string | null
  latest: CiLatestDto[]
  aggregate: CiAggregateDto | null
}

export interface HostInfoDto {
  appVersion: string
  webView2Version: string
  features?: Record<string, boolean>
  limits?: Record<string, number>
}

export type RequestMethod =
  | 'getSnapshot'
  | 'refresh'
  | 'getComponentDetail'
  | 'getRepoDetail'
  | 'getCommits'
  | 'getRepoTree'
  | 'getWorkspaceTree'
  | 'openPath'
  | 'runRepoAction'
  | 'getRemoteRefs'
  | 'getCiStatus'
  | 'getHostInfo'
  | 'runBatch'
  | 'getJobs'
  | 'getJob'
  | 'cancelJob'
  | 'retryFailed'
  | 'getOperationLog'
  | 'exportOperationLog'
  | 'getBackupRefs'
  | 'restoreBackupRef'
  | 'cleanupRetention'

export const TIMEOUTS: Record<RequestMethod, number> = {
  getSnapshot: 45_000,
  refresh: 120_000,
  getComponentDetail: 45_000,
  getRepoDetail: 10_000,
  getCommits: 15_000,
  getRepoTree: 10_000,
  getWorkspaceTree: 10_000,
  openPath: 8_000,
  runRepoAction: 120_000,
  getRemoteRefs: 20_000,
  getCiStatus: 2_000,
  getHostInfo: 2_000,
  // 批量提交只返回 jobId（异步执行），取消/重试为短 RPC；列表读取供任务中心轮询兜底。
  runBatch: 30_000,
  getJobs: 5_000,
  getJob: 5_000,
  cancelJob: 10_000,
  retryFailed: 15_000,
  // M2b-2：审计查询/导出为只读 DB/文件操作；备份 ref 恢复是 git 写（备份 sha 读回）。
  getOperationLog: 15_000,
  exportOperationLog: 30_000,
  getBackupRefs: 10_000,
  restoreBackupRef: 120_000,
  cleanupRetention: 60_000,
}

/** 旧入口默认超时（与 M1 保持一致）。 */
export const DEFAULT_TIMEOUT_MS = 45_000

// ───────────────────────────── 公开类型 ─────────────────────────────

export class HostError extends Error {
  readonly code: string
  readonly details?: unknown
  readonly method?: RequestMethod

  constructor(code: string, message: string, details?: unknown, method?: RequestMethod) {
    super(message)
    this.name = 'HostError'
    this.code = code
    this.details = details
    this.method = method
  }

  get retryable(): boolean {
    return ['E_TIMEOUT', 'E_INTERNAL', 'E_IO_ERROR', 'E_ACCESS_DENIED', 'E_GIT_FAILED'].includes(this.code)
  }
}

interface WebView2Bridge {
  postMessage(message: unknown): void
  addEventListener(type: 'message', listener: (event: MessageEvent<unknown>) => void): void
  removeEventListener(type: 'message', listener: (event: MessageEvent<unknown>) => void): void
}

type TestBridge = (request: { id: string; method: string; params?: unknown }) => unknown

declare global {
  interface Window {
    chrome?: { webview?: WebView2Bridge }
    __DSH_TEST_BRIDGE__?: TestBridge | { handle: TestBridge; hasMethod?: (m: string) => boolean }
    /** CDP 自测/演示用：置 true 时强制走内置 Mock（不触碰真实 Host 写接口）。 */
    __DSH_FORCE_MOCK__?: boolean
  }
}

function getBridge(): WebView2Bridge | null {
  if (typeof window === 'undefined') return null
  // 测试/演示钩子：仅 CDP 脚本会在页面脚本前注入，正常宿主内不存在。
  if (window.__DSH_FORCE_MOCK__ === true) return null
  const bridge = window.chrome?.webview
  if (!bridge || typeof bridge.postMessage !== 'function' || typeof bridge.addEventListener !== 'function') {
    return null
  }
  return bridge
}

/** 是否运行在 WebView2 宿主内或注入了测试桥（浏览器 dev 下为 false，store 据此回退 Mock）。 */
export function hasHostBridge(): boolean {
  return getBridge() !== null || getTestBridge() !== null
}

function getTestBridge(): TestBridge | null {
  if (typeof window === 'undefined') return null
  const injected = window.__DSH_TEST_BRIDGE__
  if (!injected) return null
  if (typeof injected === 'function') return injected
  if (typeof injected.handle === 'function') return injected.handle
  return null
}

/** 把任意异常转成可读文案（Toast 用）。 */
export function toErrorMessage(error: unknown): string {
  if (error instanceof HostError) {
    return error.code && !error.message.includes(error.code)
      ? `${error.code}：${error.message}`
      : error.message
  }
  if (error instanceof Error) return error.message
  if (typeof error === 'string' && error.length > 0) return error
  return '未知错误'
}

/** 判断是否「Host 尚未实现该方法」（M1 桥只有 getSnapshot/refresh）。 */
export function isMethodUnavailable(error: unknown): boolean {
  if (!(error instanceof HostError)) return false
  if (error.code === 'E_UNKNOWN_METHOD' || error.code === 'E_NOT_IMPLEMENTED') return true
  return /未知方法|未实现|not implemented|unknown method/i.test(error.message)
}

// ───────────────────────────── 请求通道（单 listener + pending Map） ─────────────────────────────

interface PendingCall {
  method: RequestMethod
  resolve: (value: unknown) => void
  reject: (error: Error) => void
  timer: ReturnType<typeof setTimeout>
}

const pending = new Map<string, PendingCall>()
let listening = false
let listenerBridge: WebView2Bridge | null = null

/** JOB_UPDATED 事件订阅者（Host → UI 推送；Mock 模式由 store 轮询兜底）。 */
const jobListeners = new Set<(job: JobDto) => void>()

function hasListeners(): boolean {
  return pending.size > 0 || jobListeners.size > 0
}

function attachListener(bridge: WebView2Bridge) {
  if (listening) return
  bridge.addEventListener('message', onBridgeMessage)
  listenerBridge = bridge
  listening = true
}

function detachListener() {
  if (!listening || !listenerBridge) return
  listenerBridge.removeEventListener('message', onBridgeMessage)
  listenerBridge = null
  listening = false
}

/** 无 pending 请求且无事件订阅者时才摘除单一 listener（事件订阅可长期持有）。 */
function detachIfIdle() {
  if (!hasListeners()) detachListener()
}

function onBridgeMessage(event: MessageEvent<unknown>) {
  const reply = asRecord(parseMaybeJson(event.data))
  if (!reply) return

  // Host 推送：新信封 `{event:"JOB_UPDATED", jobId, current, data, state,total,cancellable}`，
  // 兼容旧信封 `{event:"JOB_UPDATED", data:JobDto}`（无 id，不是请求响应）。
  const eventName = (readString(reply, 'event') ?? readString(reply, 'type') ?? '').toUpperCase()
  if (eventName === 'JOB_UPDATED' || eventName === 'JOB.UPDATED') {
    const job = normalizeJobUpdatedEvent(reply)
    if (job) dispatchJobUpdate(job)
    return
  }

  const replyId = reply['id']
  if (replyId !== undefined && replyId !== null && pending.has(String(replyId))) {
    settleCall(String(replyId), reply)
    return
  }

  // 兼容 Host 未回显 id：只接受快照形态，并交给最早等待 getSnapshot 的请求。
  if (replyId === undefined || replyId === null) {
    const payload = pickPayload(reply)
    if (!looksLikeSnapshot(payload)) return
    for (const [id, call] of pending) {
      if (call.method === 'getSnapshot' || call.method === 'refresh') {
        settleCall(id, reply)
        return
      }
    }
  }
}

function dispatchJobUpdate(job: JobDto): void {
  for (const listener of [...jobListeners]) {
    try {
      listener(job)
    } catch {
      // 订阅者异常不得影响桥接消息通道（其他请求/订阅仍需要继续工作）。
    }
  }
}

/**
 * 解析 `JOB_UPDATED` 事件（M2b-1 修：新信封 + 旧 data 形状兼容）。
 *
 * - 新信封（后端 M2b-1 修复后）：`{ event:"JOB_UPDATED", jobId, current:JobDto, data:JobDto,
 *   state, total, cancellable }`：`current` 与 `data` 均为当前 job 快照，优先 `current`。
 * - 旧信封：`{ event:"JOB_UPDATED", data:JobDto }`。
 * - 兜底：只有信封级字段（jobId + state/total/cancellable + items）时合成**部分快照**
 *   （`partial=true`），由 store 合并保留旧 items/kind，避免把已知明细冲掉。
 */
export function normalizeJobUpdatedEvent(reply: Record<string, unknown>): JobDto | null {
  for (const candidate of [reply['current'], reply['data'], reply['job'], reply['payload']]) {
    const record = asRecord(parseMaybeJson(candidate))
    if (!record) continue
    const payloadId = readString(record, 'id') ?? readString(record, 'jobId')
    const envelopeId = readString(reply, 'jobId') ?? readString(reply, 'id')
    const job = normalizeJob(payloadId || !envelopeId ? record : { ...record, id: envelopeId })
    if (job) return job
  }

  const jobId = readString(reply, 'jobId') ?? readString(reply, 'id')
  if (!jobId) return null
  // 只有确实带快照语义（state/items）时才合成，避免把进度数字误当状态。
  const hasState = prop(reply, 'state') !== undefined
  const hasItems = Array.isArray(prop(reply, 'items'))
  if (!hasState && !hasItems) return null
  const job = normalizeJob({
    id: jobId,
    kind: prop(reply, 'kind'),
    state: prop(reply, 'state'),
    total: prop(reply, 'total'),
    ok: prop(reply, 'ok'),
    failed: prop(reply, 'failed'),
    skipped: prop(reply, 'skipped'),
    createdAt: prop(reply, 'createdAt'),
    startedAt: prop(reply, 'startedAt'),
    finishedAt: prop(reply, 'finishedAt'),
    cancellable: prop(reply, 'cancellable'),
    items: prop(reply, 'items'),
    partial: true,
  })
  return job
}

/**
 * 订阅 Host `JOB_UPDATED` 事件（M2b 作业中心）。
 * 无 WebView2 宿主（浏览器 dev / CDP Mock）时返回空取消函数，由 store 轮询 Mock 兜底。
 */
export function onJobUpdated(listener: (job: JobDto) => void): () => void {
  const bridge = getBridge()
  if (!bridge) return () => undefined
  jobListeners.add(listener)
  attachListener(bridge)
  return () => {
    jobListeners.delete(listener)
    detachIfIdle()
  }
}

/** 是否具备 Host 作业事件通道（供 store 决定是否需要轮询兜底）。 */
export function hasJobEventBridge(): boolean {
  return getBridge() !== null
}

function settleCall(id: string, reply: Record<string, unknown>) {
  const call = pending.get(id)
  if (!call) return
  pending.delete(id)
  clearTimeout(call.timer)
  detachIfIdle()

  const ok = reply['ok']
  if (ok === false) {
    const code = readString(reply, 'code') ?? 'E_UNKNOWN'
    const message = readErrorText(reply) || `Host 拒绝了请求：${call.method}`
    call.reject(new HostError(code, message, reply['details'], call.method))
    return
  }
  call.resolve(pickPayload(reply))
}

function newId(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') return crypto.randomUUID()
  return `req-${Date.now()}-${Math.random().toString(16).slice(2)}`
}

/** 向 Host 发一条请求并等待同 id 的响应；无桥 / 超时 / 错误均 reject。 */
export function request<T, P = unknown>(
  method: RequestMethod,
  params?: P,
  timeoutMs: number = TIMEOUTS[method],
): Promise<T> {
  const testBridge = getTestBridge()
  if (testBridge) {
    const id = newId()
    return new Promise<T>((resolve, reject) => {
      let settled = false
      const timer = setTimeout(() => {
        if (settled) return
        settled = true
        reject(new HostError('E_TIMEOUT', `等待 Mock 桥响应超时（${method}）`, undefined, method))
      }, timeoutMs)
      Promise.resolve()
        .then(() => testBridge({ id, method, params }))
        .then((reply) => {
          if (settled) return
          settled = true
          clearTimeout(timer)
          const record = asRecord(parseMaybeJson(reply))
          if (record && record['ok'] === false) {
            reject(
              new HostError(
                readString(record, 'code') ?? 'E_UNKNOWN',
                readErrorText(record) || `Mock 桥拒绝：${method}`,
                record['details'],
                method,
              ),
            )
            return
          }
          resolve((record ? pickPayload(record) : reply) as T)
        })
        .catch((error: unknown) => {
          if (settled) return
          settled = true
          clearTimeout(timer)
          reject(error instanceof Error ? error : new Error(String(error)))
        })
    })
  }

  const bridge = getBridge()
  if (!bridge) {
    return Promise.reject(
      new HostError(
        'E_NO_HOST',
        '未检测到 WebView2 宿主（window.chrome.webview 不可用），无法连接 Host',
        undefined,
        method,
      ),
    )
  }

  const id = newId()
  attachListener(bridge)

  return new Promise<T>((resolve, reject) => {
    const timer = setTimeout(() => {
      if (!pending.has(id)) return
      pending.delete(id)
      detachIfIdle()
      reject(
        new HostError(
          'E_TIMEOUT',
          `等待 Host 响应超时（${method}，${timeoutMs}ms）；Host 可能未就绪`,
          undefined,
          method,
        ),
      )
    }, timeoutMs)

    pending.set(id, {
      method,
      resolve: (value) => resolve(value as T),
      reject,
      timer,
    })

    try {
      bridge.postMessage({ id, method, params: params ?? {} })
    } catch (error) {
      pending.delete(id)
      clearTimeout(timer)
      reject(new Error(`发送 Host 请求失败：${toErrorMessage(error)}`))
    }
  })
}

// ───────────────────────────── 规范化 ─────────────────────────────

const RESULT_PAYLOAD_KEYS = ['data', 'result', 'payload', 'snapshot'] as const

function asRecord(value: unknown): Record<string, unknown> | null {
  return typeof value === 'object' && value !== null ? (value as Record<string, unknown>) : null
}

function parseMaybeJson(value: unknown): unknown {
  if (typeof value !== 'string') return value
  const text = value.trim()
  if (!text.startsWith('{') && !text.startsWith('[')) return value
  try {
    return JSON.parse(text) as unknown
  } catch {
    return value
  }
}

function pickPayload(reply: Record<string, unknown>): unknown {
  for (const key of RESULT_PAYLOAD_KEYS) {
    const value = reply[key]
    if (value !== undefined && value !== null) return parseMaybeJson(value)
  }
  return reply
}

function looksLikeSnapshot(value: unknown): boolean {
  const record = asRecord(parseMaybeJson(value))
  return record !== null && Array.isArray(record['components'])
}

function readErrorText(reply: Record<string, unknown>): string {
  const payload = asRecord(pickPayload(reply))
  const candidates: unknown[] = [reply['error'], reply['message'], payload?.['error'], payload?.['message']]
  for (const candidate of candidates) {
    if (typeof candidate === 'string' && candidate.length > 0) return candidate
    const nested = asRecord(candidate)
    if (nested) {
      const text = readString(nested, 'message')
      if (text) return text
    }
  }
  return ''
}

// —— DTO 取值（容忍字段缺失/类型漂移；camelCase 优先，兼容 PascalCase） ——

function prop(record: Record<string, unknown>, key: string): unknown {
  if (key in record) return record[key]
  const pascal = key.charAt(0).toUpperCase() + key.slice(1)
  return record[pascal]
}

function readString(record: Record<string, unknown>, key: string): string | undefined {
  const value = prop(record, key)
  return typeof value === 'string' ? value : undefined
}

function readNullableString(record: Record<string, unknown>, key: string): string | null {
  return readString(record, key) ?? null
}

function readNumber(record: Record<string, unknown>, key: string, fallback = 0): number {
  const value = prop(record, key)
  if (typeof value === 'number' && Number.isFinite(value)) return value
  if (typeof value === 'string' && value.trim() !== '') {
    const parsed = Number(value)
    if (Number.isFinite(parsed)) return parsed
  }
  return fallback
}

function readBoolean(record: Record<string, unknown>, key: string): boolean {
  const value = prop(record, key)
  if (typeof value === 'boolean') return value
  return value === 1 || value === 'true'
}

function readStringArray(record: Record<string, unknown>, key: string): string[] {
  const value = prop(record, key)
  if (!Array.isArray(value)) return []
  return value.filter((item): item is string => typeof item === 'string')
}

function readArray(record: Record<string, unknown>, key: string): unknown[] | null {
  const value = prop(record, key)
  return Array.isArray(value) ? value : null
}

export function normalizeSnapshot(value: unknown): SnapshotDto | null {
  let record = asRecord(parseMaybeJson(value))
  if (!record) return null
  if (!Array.isArray(record['components'])) {
    const nested = asRecord(pickPayload(record))
    if (nested && nested !== record) record = nested
  }

  const source = (readString(record, 'source') ?? '').toLowerCase()
  const components = readArray(record, 'components')
  if (!source || !components) return null

  const countsRecord = asRecord(prop(record, 'counts'))
  const freshnessRecord = asRecord(prop(record, 'freshness'))
  const capabilitiesRecord = asRecord(prop(record, 'capabilities'))

  return {
    source,
    workspaceRoot: readNullableString(record, 'workspaceRoot'),
    error: readNullableString(record, 'error'),
    generatedAt: readString(record, 'generatedAt') ?? '',
    owner: readNullableString(record, 'owner'),
    giteaBaseUrl: readNullableString(record, 'giteaBaseUrl'),
    capabilities: capabilitiesRecord
      ? {
          fileTree: prop(capabilitiesRecord, 'fileTree') === undefined ? undefined : readBoolean(capabilitiesRecord, 'fileTree'),
          workspaceTree:
            prop(capabilitiesRecord, 'workspaceTree') === undefined ? undefined : readBoolean(capabilitiesRecord, 'workspaceTree'),
          openPath: prop(capabilitiesRecord, 'openPath') === undefined ? undefined : readBoolean(capabilitiesRecord, 'openPath'),
          repoActions:
            prop(capabilitiesRecord, 'repoActions') === undefined ? undefined : readBoolean(capabilitiesRecord, 'repoActions'),
          ci: prop(capabilitiesRecord, 'ci') === undefined ? undefined : readBoolean(capabilitiesRecord, 'ci'),
        }
      : null,
    // M2b-2（可选字段）：Host 未返回时为 null，由 store 结合 source/error/freshness 保守推断。
    offline: prop(record, 'offline') === undefined ? null : readBoolean(record, 'offline'),
    remoteError: readNullableString(record, 'remoteError') ?? readNullableString(record, 'remoteErrorCode'),
    counts: countsRecord
      ? {
          components: readNumber(countsRecord, 'components'),
          repos: readNumber(countsRecord, 'repos'),
          missingRepos: readNumber(countsRecord, 'missingRepos'),
        }
      : null,
    freshness: freshnessRecord
      ? {
          localScanAt: readNullableString(freshnessRecord, 'localScanAt'),
          remoteFetchAt: readNullableString(freshnessRecord, 'remoteFetchAt'),
          localStale: readBoolean(freshnessRecord, 'localStale'),
          remoteStale: readBoolean(freshnessRecord, 'remoteStale'),
        }
      : null,
    components: components
      .map(normalizeComponent)
      .filter((component): component is ComponentDto => component !== null),
  }
}

function normalizeComponent(value: unknown): ComponentDto | null {
  const record = asRecord(value)
  if (!record) return null
  const componentId = readString(record, 'componentId') ?? readString(record, 'id')
  if (!componentId) return null
  const ciAggregate = asRecord(prop(record, 'ciAggregate'))
  return {
    componentId,
    aggregateState: readString(record, 'aggregateState') ?? 'UNKNOWN',
    severityRank: readNumber(record, 'severityRank', 2),
    branch: readNullableString(record, 'branch'),
    tag: readNullableString(record, 'tag'),
    mixedBranch: readBoolean(record, 'mixedBranch'),
    flags: readStringArray(record, 'flags'),
    incomplete: readBoolean(record, 'incomplete'),
    bindingCause: readNullableString(record, 'bindingCause'),
    lastActivityAt: readNullableString(record, 'lastActivityAt'),
    ciAggregate: ciAggregate
      ? {
          componentId: readString(ciAggregate, 'componentId') ?? componentId,
          byState: (asRecord(prop(ciAggregate, 'byState')) as Record<string, number>) ?? {},
          worstState: readString(ciAggregate, 'worstState') ?? 'NONE',
          dataFreshAt: readString(ciAggregate, 'dataFreshAt') ?? '',
        }
      : null,
    repos: (readArray(record, 'repos') ?? [])
      .map(normalizeRepo)
      .filter((repo): repo is RepoDto => repo !== null),
  }
}

export function normalizeRepo(value: unknown): RepoDto | null {
  const record = asRecord(value)
  if (!record) return null
  const repoType = readString(record, 'repoType')
  if (!repoType) return null
  return {
    repoType: repoType.toUpperCase(),
    repoName: readNullableString(record, 'repoName') ?? undefined,
    state: readString(record, 'state') ?? 'UNKNOWN',
    subType: readNullableString(record, 'subType'),
    branch: readNullableString(record, 'branch'),
    tag: readNullableString(record, 'tag'),
    headSha: readNullableString(record, 'headSha'),
    upstreamRef: readNullableString(record, 'upstreamRef'),
    compareRef: readNullableString(record, 'compareRef'),
    ahead: readNumber(record, 'ahead'),
    behind: readNumber(record, 'behind'),
    dirtyCount: readNumber(record, 'dirtyCount'),
    untrackedCount: readNumber(record, 'untrackedCount'),
    stagedCount: readNumber(record, 'stagedCount'),
    stashCount: readNumber(record, 'stashCount'),
    isLocal: readBoolean(record, 'isLocal'),
    localPath: readNullableString(record, 'localPath'),
    isStale: readBoolean(record, 'isStale'),
    calcAt: readNullableString(record, 'calcAt'),
    dataSource: readNullableString(record, 'dataSource'),
    errorCode: readNullableString(record, 'errorCode'),
    flags: readStringArray(record, 'flags'),
    actions: (readArray(record, 'actions') ?? [])
      .map(normalizeRepoActionAvailability)
      .filter((item): item is RepoActionAvailabilityDto => item !== null),
  }
}

/** normalize getComponentDetail.repos[].actions（Host 单源矩阵，PascalCase 兼容）。 */
export function normalizeRepoActionAvailability(value: unknown): RepoActionAvailabilityDto | null {
  const record = asRecord(value)
  if (!record) return null
  const action = readString(record, 'action')
  if (!action) return null
  return {
    action: action.toUpperCase(),
    enabled: readBoolean(record, 'enabled'),
    matrixAllowed:
      prop(record, 'matrixAllowed') === undefined ? undefined : readBoolean(record, 'matrixAllowed'),
    reasonCode: readNullableString(record, 'reasonCode'),
    reason: readNullableString(record, 'reason'),
    requiresConfirm:
      prop(record, 'requiresConfirm') === undefined ? undefined : readBoolean(record, 'requiresConfirm'),
    stage: readNullableString(record, 'stage'),
  }
}

function normalizeTreeNode(value: unknown): TreeNodeDto | null {
  const record = asRecord(value)
  if (!record) return null
  const path = readString(record, 'path')
  const name = readString(record, 'name')
  if (!path || !name) return null
  const kind = (readString(record, 'kind') ?? 'FILE').toUpperCase() === 'DIR' ? 'DIR' : 'FILE'
  const children = readArray(record, 'children')
  return {
    path,
    name,
    kind,
    hasChildren: readBoolean(record, 'hasChildren') || kind === 'DIR',
    size: typeof prop(record, 'size') === 'number' ? readNumber(record, 'size') : null,
    mtime: readNullableString(record, 'mtime'),
    gitStatus: readNullableString(record, 'gitStatus'),
    submodule: readBoolean(record, 'submodule'),
    ignored: readBoolean(record, 'ignored'),
    children: children
      ? children.map(normalizeTreeNode).filter((node): node is TreeNodeDto => node !== null)
      : null,
    error: readNullableString(record, 'error'),
  }
}

export function normalizeRepoTree(value: unknown): RepoTreeDto {
  const record = asRecord(parseMaybeJson(value))
  if (!record) throw new HostError('E_INVALID_RESPONSE', 'getRepoTree 响应不是对象')
  const entries = readArray(record, 'entries')
  if (!entries) throw new HostError('E_INVALID_RESPONSE', 'getRepoTree 响应缺少 entries')
  return {
    componentId: readString(record, 'componentId') ?? '',
    repoType: (readString(record, 'repoType') ?? '').toUpperCase(),
    repoName: readString(record, 'repoName'),
    rootPath: readString(record, 'rootPath'),
    path: readString(record, 'path') ?? '',
    branch: readNullableString(record, 'branch'),
    headSha: readNullableString(record, 'headSha'),
    state: readString(record, 'state'),
    dirtyCount: readNumber(record, 'dirtyCount'),
    compareRef: readNullableString(record, 'compareRef'),
    fetchedAt: readNullableString(record, 'fetchedAt'),
    entries: entries.map(normalizeTreeNode).filter((node): node is TreeNodeDto => node !== null),
    truncated: readBoolean(record, 'truncated'),
    totalEntries: readNumber(record, 'totalEntries'),
    error: readNullableString(record, 'error'),
    errorCode: readNullableString(record, 'errorCode'),
  }
}

function normalizeWorkspaceNode(value: unknown): WorkspaceNodeDto | null {
  const record = asRecord(value)
  if (!record) return null
  const kind = (readString(record, 'kind') ?? '').toUpperCase()
  if (!kind) return null
  const repos = readArray(record, 'repos') ?? []
  const children = readArray(record, 'children')
  const path = readString(record, 'path') ?? readString(record, 'componentId') ?? ''
  return {
    kind,
    name: readString(record, 'name'),
    path,
    componentId: readString(record, 'componentId'),
    repoType: readString(record, 'repoType'),
    state: readString(record, 'state'),
    severityRank: readNumber(record, 'severityRank'),
    dirtyCount: readNumber(record, 'dirtyCount'),
    mixedBranch: readBoolean(record, 'mixedBranch'),
    branch: readNullableString(record, 'branch'),
    ahead: readNumber(record, 'ahead'),
    behind: readNumber(record, 'behind'),
    count: readNumber(record, 'count'),
    isLocal: prop(record, 'isLocal') === undefined ? undefined : readBoolean(record, 'isLocal'),
    hasChildren: prop(record, 'hasChildren') === undefined ? undefined : readBoolean(record, 'hasChildren'),
    gitStatus: readNullableString(record, 'gitStatus'),
    size: typeof prop(record, 'size') === 'number' ? readNumber(record, 'size') : null,
    mtime: readNullableString(record, 'mtime'),
    submodule: readBoolean(record, 'submodule'),
    ignored: readBoolean(record, 'ignored'),
    error: readNullableString(record, 'error'),
    repos: repos
      .map(normalizeWorkspaceNode)
      .filter((node): node is WorkspaceNodeDto => node !== null),
    children: children == null
      ? undefined
      : children
          .map(normalizeWorkspaceNode)
          .filter((node): node is WorkspaceNodeDto => node !== null),
  }
}

export function normalizeWorkspaceTree(value: unknown): WorkspaceTreeDto {
  const record = asRecord(parseMaybeJson(value))
  if (!record) throw new HostError('E_INVALID_RESPONSE', 'getWorkspaceTree 响应不是对象')
  const entries = readArray(record, 'entries')
  if (!entries) throw new HostError('E_INVALID_RESPONSE', 'getWorkspaceTree 响应缺少 entries')
  return {
    root: readString(record, 'root') ?? '',
    path: readString(record, 'path') ?? '',
    generatedAt: readString(record, 'generatedAt'),
    readAt: readString(record, 'readAt'),
    truncated: readBoolean(record, 'truncated'),
    totalEntries: prop(record, 'totalEntries') === undefined ? undefined : readNumber(record, 'totalEntries'),
    error: readNullableString(record, 'error'),
    errorCode: readNullableString(record, 'errorCode'),
    entries: entries
      .map(normalizeWorkspaceNode)
      .filter((node): node is WorkspaceNodeDto => node !== null),
  }
}

function normalizeCommit(value: unknown): CommitDto | null {
  const record = asRecord(value)
  if (!record) return null
  const sha = readString(record, 'sha')
  if (!sha) return null
  return {
    sha,
    shortSha: readString(record, 'shortSha'),
    message: readString(record, 'message') ?? '',
    author: readString(record, 'author') ?? '',
    at: readString(record, 'at') ?? '',
    refs: readStringArray(record, 'refs'),
  }
}

export function normalizeCommits(value: unknown): { total: number; items: CommitDto[] } {
  const record = asRecord(parseMaybeJson(value))
  if (!record) throw new HostError('E_INVALID_RESPONSE', 'getCommits 响应不是对象')
  return {
    total: readNumber(record, 'total'),
    items: (readArray(record, 'items') ?? [])
      .map(normalizeCommit)
      .filter((commit): commit is CommitDto => commit !== null),
  }
}

export function normalizeRepoDetail(value: unknown): RepoDetailDto {
  const record = asRecord(parseMaybeJson(value))
  if (!record) throw new HostError('E_INVALID_RESPONSE', 'getRepoDetail 响应不是对象')
  const repo = normalizeRepo(prop(record, 'repo'))
  const commitRecord = asRecord(prop(record, 'commits'))
  return {
    repo,
    component: normalizeComponent(prop(record, 'component')),
    commits: {
      total: commitRecord ? readNumber(commitRecord, 'total') : 0,
      items: commitRecord
        ? (readArray(commitRecord, 'items') ?? [])
            .map(normalizeCommit)
            .filter((commit): commit is CommitDto => commit !== null)
        : [],
    },
  }
}

export function normalizeOpenPath(value: unknown): OpenPathResultDto {
  const record = asRecord(parseMaybeJson(value))
  if (!record) return { opened: false, mode: 'open' }
  return {
    opened: readBoolean(record, 'opened'),
    mode: readString(record, 'mode') ?? 'open',
    path: readString(record, 'path'),
    errorCode: readString(record, 'errorCode'),
    message: readString(record, 'message'),
  }
}

export function normalizeRepoActionResult(value: unknown): RepoActionResultDto {
  const record = asRecord(parseMaybeJson(value))
  if (!record) return { accepted: false }
  return {
    jobId: readString(record, 'jobId'),
    accepted: readBoolean(record, 'accepted'),
    action: readString(record, 'action')?.toUpperCase(),
    componentId: readString(record, 'componentId'),
    repoType: readString(record, 'repoType')?.toUpperCase(),
    state: readString(record, 'state'),
    preHead: readString(record, 'preHead'),
    postHead: readString(record, 'postHead'),
    branch: readString(record, 'branch'),
    output: readString(record, 'output'),
    durationMs: prop(record, 'durationMs') === undefined ? undefined : readNumber(record, 'durationMs'),
    // Host 错误码字段既可能是 errorCode，也可能是桥接层的 code；两者都保留以便完整映射。
    errorCode: readString(record, 'errorCode') ?? readString(record, 'code'),
    message: readString(record, 'message'),
    stashRef: readString(record, 'stashRef'),
  }
}

// ── M2b 作业 DTO 规范化（容忍 PascalCase、字段缺失与计数漂移） ──

const JOB_STATES: readonly JobState[] = [
  'PENDING',
  'RUNNING',
  'PARTIAL',
  'SUCCESS',
  'FAILED',
  'CANCELED',
  'INTERRUPTED',
  'PREVIEW',
]
const JOB_ITEM_STATES: readonly JobItemState[] = [
  'PENDING',
  'RUNNING',
  'OK',
  'SUCCESS',
  'FAILED',
  'SKIPPED',
  'CANCELED',
  'INTERRUPTED',
  'PREVIEW',
]

function normalizeJobState(value: unknown): JobState {
  const upper = typeof value === 'string' ? value.toUpperCase() : ''
  // 兼容旧拼写 CANCELLED（SRS/AC 字面）：UI 内部状态只用 CANCELED。
  if (upper === 'CANCELLED') return 'CANCELED'
  return (JOB_STATES as readonly string[]).includes(upper) ? (upper as JobState) : 'PENDING'
}

function normalizeJobItemState(value: unknown): JobItemState {
  const upper = typeof value === 'string' ? value.toUpperCase() : ''
  if (upper === 'CANCELLED') return 'CANCELED'
  return (JOB_ITEM_STATES as readonly string[]).includes(upper) ? (upper as JobItemState) : 'PENDING'
}

function normalizeJobItem(value: unknown): JobItemDto | null {
  const record = asRecord(parseMaybeJson(value))
  if (!record) return null
  const componentId = readString(record, 'componentId') ?? ''
  const repoType = (readString(record, 'repoType') ?? '').toUpperCase()
  if (!componentId && !repoType) return null
  return {
    componentId,
    repoType,
    repoName: readNullableString(record, 'repoName'),
    state: normalizeJobItemState(prop(record, 'state')),
    startedAt: readNullableString(record, 'startedAt'),
    finishedAt: readNullableString(record, 'finishedAt'),
    errorCode: readNullableString(record, 'errorCode'),
    message: readNullableString(record, 'message'),
    preHead: readNullableString(record, 'preHead'),
    postHead: readNullableString(record, 'postHead'),
    backupRef: readNullableString(record, 'backupRef'),
    preview: readBoolean(record, 'preview') || normalizeJobItemState(prop(record, 'state')) === 'PREVIEW',
  }
}

/** normalize Job：兼容直接 job、`{job:{…}}` 与 `{data:{…}}` 包装；计数缺失时按 items 兜底。 */
export function normalizeJob(value: unknown): JobDto | null {
  let record = asRecord(parseMaybeJson(value))
  if (!record) return null
  if (!record['id'] && !record['jobId']) {
    const nested = asRecord(parseMaybeJson(record['job'] ?? record['data']))
    if (nested && nested !== record) record = nested
  }
  const id = readString(record, 'id') ?? readString(record, 'jobId')
  if (!id) return null
  const items = (readArray(record, 'items') ?? [])
    .map(normalizeJobItem)
    .filter((item): item is JobItemDto => item !== null)
  const state = normalizeJobState(prop(record, 'state'))
  const okFallback = items.filter((item) => item.state === 'OK' || item.state === 'SUCCESS').length
  const failedFallback = items.filter((item) => item.state === 'FAILED').length
  const skippedFallback = items.filter((item) => item.state === 'SKIPPED').length
  const cancellableRaw = prop(record, 'cancellable')
  const cancellable =
    cancellableRaw === undefined
      ? state === 'PENDING' || state === 'RUNNING'
      : readBoolean(record, 'cancellable')
  return {
    id,
    kind: ((readString(record, 'kind') ?? '').toUpperCase() || 'SYNC') as JobKind,
    state,
    total: Math.max(0, readNumber(record, 'total', items.length)),
    ok: Math.max(0, readNumber(record, 'ok', okFallback)),
    failed: Math.max(0, readNumber(record, 'failed', failedFallback)),
    skipped: Math.max(0, readNumber(record, 'skipped', skippedFallback)),
    createdAt: readString(record, 'createdAt') ?? '',
    startedAt: readNullableString(record, 'startedAt'),
    finishedAt: readNullableString(record, 'finishedAt'),
    cancellable,
    items,
    dryRun: readBoolean(record, 'dryRun') || state === 'PREVIEW' || undefined,
    cancelRequested: readBoolean(record, 'cancelRequested') || undefined,
    message: readNullableString(record, 'message'),
    partial: record['partial'] === true ? true : undefined,
  }
}

/** normalize getJobs：契约 `{jobs}`，兼容裸数组与 `{items}`。 */
export function normalizeJobs(value: unknown): JobsDto {
  const record = asRecord(parseMaybeJson(value))
  const raw = Array.isArray(record)
    ? record
    : (readArray(record ?? {}, 'jobs') ?? readArray(record ?? {}, 'items') ?? [])
  const jobs: JobDto[] = []
  for (const item of raw) {
    const job = normalizeJob(item)
    if (job) jobs.push(job)
  }
  return { jobs }
}

/** normalize runBatch / retryFailed 响应（契约 `{jobId,accepted,total,skipped}`）。 */
export function normalizeRunBatchResult(value: unknown): RunBatchResultDto {
  const record = asRecord(parseMaybeJson(value)) ?? {}
  const jobId = readString(record, 'jobId') ?? readString(record, 'id') ?? ''
  return {
    jobId,
    accepted: prop(record, 'accepted') === undefined ? Boolean(jobId) : readBoolean(record, 'accepted'),
    total: Math.max(0, readNumber(record, 'total', readNumber(record, 'retried'))),
    skipped: Math.max(0, readNumber(record, 'skipped')),
  }
}

// ── M2b-2 操作审计 / 备份 ref 规范化（容忍 PascalCase、id 类型漂移与包装层） ──

function readIdString(record: Record<string, unknown>, key: string): string {
  const value = prop(record, key)
  if (typeof value === 'string') return value
  if (typeof value === 'number' && Number.isFinite(value)) return String(value)
  return ''
}

/** normalize `getOperationLog` 单行（契约 13 字段；兼容 Host M2b-2 的 target/before/after JSON 投影）。 */
export function normalizeOperationLogItem(value: unknown): OperationLogItemDto | null {
  const record = asRecord(parseMaybeJson(value))
  if (!record) return null
  const id = readIdString(record, 'id')
  const ts = readString(record, 'ts') ?? ''
  if (!id && !ts) return null
  const target = asRecord(parseMaybeJson(prop(record, 'target')))
  const before = asRecord(parseMaybeJson(prop(record, 'before')))
  const after = asRecord(parseMaybeJson(prop(record, 'after')))
  return {
    id: id || ts,
    ts,
    actor: readString(record, 'actor') ?? '',
    action: (readString(record, 'action') ?? '').toUpperCase(),
    componentId: readNullableString(record, 'componentId') ?? (target ? readNullableString(target, 'componentId') : null),
    repoType:
      readString(record, 'repoType')?.toUpperCase() ?? (target ? readString(target, 'repoType')?.toUpperCase() : null) ?? null,
    result: (readString(record, 'result') ?? (readBoolean(record, 'ok') ? 'OK' : 'FAILED')).toUpperCase(),
    errorCode: readNullableString(record, 'errorCode'),
    requestId: readNullableString(record, 'requestId'),
    jobId: readNullableString(record, 'jobId'),
    jobItemId: readNullableString(record, 'jobItemId'),
    backupRef: readNullableString(record, 'backupRef'),
    beforeHead:
      readNullableString(record, 'beforeHead') ??
      readNullableString(record, 'preHead') ??
      (before ? readNullableString(before, 'head') : null),
    afterHead:
      readNullableString(record, 'afterHead') ??
      readNullableString(record, 'postHead') ??
      (after ? readNullableString(after, 'head') : null),
  }
}

/** normalize `getOperationLog` 响应（契约 `{items,total}`；兼容裸数组/数据包装）。 */
export function normalizeOperationLogPage(value: unknown): OperationLogPageDto {
  let record = asRecord(parseMaybeJson(value))
  if (!record) throw new HostError('E_INVALID_RESPONSE', 'getOperationLog 响应不是对象')
  if (!Array.isArray(record['items']) && !Array.isArray(record)) {
    const nested = asRecord(pickPayload(record))
    if (nested && Array.isArray(nested['items'])) record = nested
  }
  const raw = Array.isArray(record) ? record : readArray(record, 'items')
  if (!raw) throw new HostError('E_INVALID_RESPONSE', 'getOperationLog 响应缺少 items')
  const items: OperationLogItemDto[] = []
  for (const entry of raw) {
    const item = normalizeOperationLogItem(entry)
    if (item) items.push(item)
  }
  return {
    items,
    total: Math.max(0, readNumber(record as Record<string, unknown>, 'total', items.length)),
  }
}

/** normalize `exportOperationLog` 响应（契约 `{path,count}`；缺 path 视为失败，不伪造成功路径）。 */
export function normalizeExportOperationLogResult(value: unknown): ExportOperationLogResultDto {
  const record = asRecord(parseMaybeJson(value)) ?? {}
  const path = readString(record, 'path') ?? ''
  if (!path) throw new HostError('E_INVALID_RESPONSE', 'exportOperationLog 响应缺少 path')
  return { path, count: Math.max(0, readNumber(record, 'count')) }
}

/** normalize `getBackupRefs` 响应（契约 `{refs:[{ref,sha,createdAt,repoType}]}`）。 */
export function normalizeBackupRefs(value: unknown): BackupRefsDto {
  const record = asRecord(parseMaybeJson(value))
  if (!record) throw new HostError('E_INVALID_RESPONSE', 'getBackupRefs 响应不是对象')
  const rawRefs = readArray(record, 'refs') ?? readArray(record, 'items')
  if (!rawRefs) throw new HostError('E_INVALID_RESPONSE', 'getBackupRefs 响应缺少 refs')
  const refs: BackupRefDto[] = []
  for (const entry of rawRefs) {
    const refRecord = asRecord(entry)
    if (!refRecord) continue
    const ref = readString(refRecord, 'ref') ?? readString(refRecord, 'refName') ?? readString(refRecord, 'name')
    const sha = readString(refRecord, 'sha') ?? readString(refRecord, 'objectSha') ?? ''
    if (!ref || !sha) continue
    refs.push({
      ref,
      sha,
      createdAt: readString(refRecord, 'createdAt') ?? readString(refRecord, 'at') ?? '',
      repoType: (readString(refRecord, 'repoType') ?? '').toUpperCase(),
      branch: readNullableString(refRecord, 'branch'),
      format: readString(refRecord, 'format') ?? (readString(refRecord, 'branch') ? 'BRANCH' : 'LEGACY'),
    })
  }
  return { refs }
}

/**
 * normalize `restoreBackupRef` 响应。
 * <para>兼容两代契约：任务书 `{ok,preHead,postHead,backupRef}` 与 Host M2b-2 实现
 * `{restored,ref,safetyRef,preHead,postHead,message}`（`ok` 只取明确 true/restored=true）。</para>
 */
export function normalizeRestoreBackupRefResult(value: unknown): RestoreBackupRefResultDto {
  const record = asRecord(parseMaybeJson(value)) ?? {}
  const okRaw = prop(record, 'ok')
  const restoredRaw = prop(record, 'restored')
  const ok = okRaw === undefined && restoredRaw === undefined ? false : readBoolean(record, 'ok') || readBoolean(record, 'restored')
  return {
    ok,
    preHead: readNullableString(record, 'preHead'),
    postHead: readNullableString(record, 'postHead'),
    backupRef: readNullableString(record, 'backupRef') ?? readNullableString(record, 'ref'),
    safetyRef: readNullableString(record, 'safetyRef'),
    message: readNullableString(record, 'message'),
  }
}

/** normalize `cleanupRetention` 响应（M2b-2 Host 返回分项计数；兼容 removed/deleted 简写）。 */
export function normalizeCleanupRetentionResult(value: unknown): CleanupRetentionResultDto {
  const record = asRecord(parseMaybeJson(value)) ?? {}
  const sum =
    readNumber(record, 'trashRemoved') +
    readNumber(record, 'backupRefsRemoved') +
    readNumber(record, 'auditRowsRemoved') +
    readNumber(record, 'auditJsonlRowsRemoved')
  const removed = sum > 0 ? sum : readNumber(record, 'removed', readNumber(record, 'deleted', readNumber(record, 'cleanupCount')))
  return { removed: Math.max(0, removed) }
}

const REMOTE_REF_KINDS: readonly RemoteRefKind[] = ['LOCAL_BRANCH', 'REMOTE_BRANCH', 'TAG']

/** 由 refName 前缀推断 kind（Host 字段缺失/大小写漂移时兜底）。 */
function inferRemoteRefKind(refName: string): RemoteRefKind | null {
  if (refName.startsWith('refs/heads/')) return 'LOCAL_BRANCH'
  if (refName.startsWith('refs/remotes/')) return 'REMOTE_BRANCH'
  if (refName.startsWith('refs/tags/')) return 'TAG'
  return null
}

function shortShaOf(sha: string): string {
  return sha.length > 7 ? sha.slice(0, 7) : sha
}

/** normalize getRemoteRefs：容忍 refs/items 字段名、PascalCase、kind 缺省（按 refName 前缀推断）。 */
export function normalizeRemoteRefs(value: unknown): RemoteRefsDto {
  const record = asRecord(parseMaybeJson(value))
  if (!record) throw new HostError('E_INVALID_RESPONSE', 'getRemoteRefs 响应不是对象')
  const rawRefs = readArray(record, 'refs') ?? readArray(record, 'items')
  if (!rawRefs) throw new HostError('E_INVALID_RESPONSE', 'getRemoteRefs 响应缺少 refs')
  const refs: RemoteRefDto[] = []
  for (const item of rawRefs) {
    const refRecord = asRecord(item)
    if (!refRecord) continue
    const refName = readString(refRecord, 'refName') ?? readString(refRecord, 'name')
    const sha = readString(refRecord, 'sha') ?? readString(refRecord, 'objectSha') ?? ''
    if (!refName || !sha) continue
    const kindRaw = (readString(refRecord, 'kind') ?? '').toUpperCase()
    const kind = REMOTE_REF_KINDS.includes(kindRaw as RemoteRefKind)
      ? (kindRaw as RemoteRefKind)
      : inferRemoteRefKind(refName)
    if (!kind) continue
    refs.push({
      refName,
      sha,
      shortSha: readString(refRecord, 'shortSha') ?? shortShaOf(sha),
      kind,
      objectType: readString(refRecord, 'objectType'),
    })
  }
  return {
    refs,
    truncated: readBoolean(record, 'truncated'),
    total: prop(record, 'total') === undefined ? refs.length : readNumber(record, 'total', refs.length),
    generatedAt: readString(record, 'generatedAt'),
  }
}

export function normalizeCiStatus(value: unknown): CiStatusDto {
  const record = asRecord(parseMaybeJson(value)) ?? {}
  const aggregateRecord = asRecord(prop(record, 'aggregate'))
  const byState = aggregateRecord ? asRecord(prop(aggregateRecord, 'byState')) : null
  const byStateOut: Record<string, number> = {}
  if (byState) {
    for (const key of Object.keys(byState)) byStateOut[key] = readNumber(byState, key)
  }
  return {
    enabled: readBoolean(record, 'enabled'),
    provider: readString(record, 'provider'),
    fetchedAt: readNullableString(record, 'fetchedAt'),
    latest: (readArray(record, 'latest') ?? []) as CiLatestDto[],
    aggregate: aggregateRecord
      ? {
          componentId: readString(aggregateRecord, 'componentId') ?? '',
          byState: byStateOut,
          worstState: readString(aggregateRecord, 'worstState') ?? 'NONE',
          dataFreshAt: readString(aggregateRecord, 'dataFreshAt') ?? '',
        }
      : null,
  }
}

export function normalizeHostInfo(value: unknown): HostInfoDto {
  const record = asRecord(parseMaybeJson(value)) ?? {}
  const features = asRecord(prop(record, 'features'))
  const limits = asRecord(prop(record, 'limits'))
  const featuresOut: Record<string, boolean> = {}
  if (features) {
    for (const key of Object.keys(features)) featuresOut[key] = readBoolean(features, key)
  }
  const limitsOut: Record<string, number> = {}
  if (limits) {
    for (const key of Object.keys(limits)) limitsOut[key] = readNumber(limits, key)
  }
  return {
    appVersion: readString(record, 'appVersion') ?? '0.3.0',
    webView2Version: readString(record, 'webView2Version') ?? '—',
    features: featuresOut,
    limits: limitsOut,
  }
}

// ───────────────────────────── 方法封装（供 store 调用） ─────────────────────────────

export interface RepoTreeParams {
  componentId: string
  repoType: string
  path?: string
  depth?: number
}

export interface RepoDetailParams {
  componentId: string
  repoType: string
}

export interface CommitsParams {
  componentId: string
  repoType: string
  skip?: number
  take?: number
}

export interface OpenPathParams {
  componentId?: string
  repoType?: string
  path: string
  mode?: 'open' | 'reveal'
  confirm?: boolean
}

export interface RunRepoActionParams {
  componentId: string
  repoType: string
  action: string
  args?: RepoActionArgs
}

export interface ComponentDetailParams {
  componentId: string
}

export interface WorkspaceTreeParams {
  path?: string
  depth?: number
}

/** getRemoteRefs 参数（M2 P0-7）：按组件 + 仓类型定位本地仓。 */
export interface RemoteRefsParams {
  componentId: string
  repoType: string
}

export const api = {
  async getSnapshot(): Promise<SnapshotDto> {
    return normalizeSnapshotRequired(await request<unknown>('getSnapshot'))
  },
  async refresh(): Promise<SnapshotDto | null> {
    // refresh 可能只回 ack（无快照载荷），此时返回 null，由调用方再取 getSnapshot。
    return normalizeSnapshot(await request<unknown>('refresh'))
  },
  async getRepoTree(params: RepoTreeParams): Promise<RepoTreeDto> {
    return normalizeRepoTree(
      await request<unknown>('getRepoTree', { ...params, path: params.path ?? '', depth: params.depth ?? 1 }),
    )
  },
  async getWorkspaceTree(params: WorkspaceTreeParams = {}): Promise<WorkspaceTreeDto> {
    return normalizeWorkspaceTree(
      await request<unknown>('getWorkspaceTree', { path: params.path ?? '', depth: params.depth ?? 1 }),
    )
  },
  async getRepoDetail(params: RepoDetailParams): Promise<RepoDetailDto> {
    return normalizeRepoDetail(await request<unknown>('getRepoDetail', params))
  },
  async getCommits(params: CommitsParams): Promise<{ total: number; items: CommitDto[] }> {
    return normalizeCommits(
      await request<unknown>('getCommits', {
        componentId: params.componentId,
        repoType: params.repoType,
        skip: params.skip ?? 0,
        take: params.take ?? 100,
      }),
    )
  },
  async openPath(params: OpenPathParams): Promise<OpenPathResultDto> {
    return normalizeOpenPath(await request<unknown>('openPath', params))
  },
  async getComponentDetail(params: ComponentDetailParams): Promise<ComponentDto> {
    const component = normalizeComponent(await request<unknown>('getComponentDetail', { componentId: params.componentId }))
    if (!component) throw new HostError('E_INVALID_RESPONSE', 'getComponentDetail 响应无法识别')
    return component
  },
  async runRepoAction(params: RunRepoActionParams): Promise<RepoActionResultDto> {
    // 契约严格按 { componentId, repoType, action, args } 透传；args 始终携带（缺省空对象）。
    return normalizeRepoActionResult(
      await request<unknown>('runRepoAction', {
        componentId: params.componentId,
        repoType: params.repoType,
        action: params.action,
        args: params.args ?? {},
      }),
    )
  },
  /** M2 P0-7：CHKOUT 选择器数据源（本地/远程分支 + Tag，只读）。 */
  async getRemoteRefs(params: RemoteRefsParams): Promise<RemoteRefsDto> {
    return normalizeRemoteRefs(
      await request<unknown>('getRemoteRefs', {
        componentId: params.componentId,
        repoType: params.repoType,
      }),
    )
  },
  async getCiStatus(componentId: string): Promise<CiStatusDto> {
    return normalizeCiStatus(await request<unknown>('getCiStatus', { componentId }))
  },
  async getHostInfo(): Promise<HostInfoDto> {
    return normalizeHostInfo(await request<unknown>('getHostInfo'))
  },
  // ── M2b 批量作业（Host 并行实现；UI 只读消费，Mock 模式由 store 走内置演示作业） ──
  /** 提交批量作业；Host 只回 jobId + 计数，逐仓进度通过 JOB_UPDATED / getJob(s) 获取。 */
  async runBatch(params: RunBatchParams): Promise<RunBatchResultDto> {
    const payload: Record<string, unknown> = { kind: params.kind, args: params.args ?? {} }
    if (params.componentIds && params.componentIds.length > 0) payload.componentIds = params.componentIds
    if (params.repoTypes && params.repoTypes.length > 0) payload.repoTypes = params.repoTypes
    return normalizeRunBatchResult(await request<unknown>('runBatch', payload))
  },
  /** 最近作业列表（进行中 + 历史；limit 默认 50）。 */
  async getJobs(limit = 50): Promise<JobDto[]> {
    return normalizeJobs(await request<unknown>('getJobs', { limit })).jobs
  },
  async getJob(jobId: string): Promise<JobDto> {
    const job = normalizeJob(await request<unknown>('getJob', { jobId }))
    if (!job) throw new HostError('E_INVALID_RESPONSE', 'getJob 响应无法识别（缺少 jobId/state）')
    return job
  },
  /** 合作式取消；Host 可回最新 job（也允许只回 ack，此时返回 null 由调用方刷新列表）。 */
  async cancelJob(jobId: string): Promise<JobDto | null> {
    const raw = await request<unknown>('cancelJob', { jobId })
    const record = asRecord(parseMaybeJson(raw))
    if (!record) return null
    // 只回 { jobId, accepted } 的 ack 不是 job 快照：不得当作 Job 合并（避免把状态覆盖回 PENDING）。
    const hasJobShape = prop(record, 'state') !== undefined || Array.isArray(prop(record, 'items'))
    if (!hasJobShape) return null
    return normalizeJob(record)
  },
  /** 仅重试失败/取消项；返回新 jobId 与计数。 */
  async retryFailed(jobId: string): Promise<RunBatchResultDto> {
    return normalizeRunBatchResult(await request<unknown>('retryFailed', { jobId }))
  },
  // ── M2b-2 操作审计（Host operation_log；只读查询/导出，绝不伪造审计行） ──
  /** 分页查询操作审计（契约 `{limit?,offset?,from?,to?,action?,componentId?,result?}` → `{items,total}`）。 */
  async getOperationLog(params: OperationLogParams = {}): Promise<OperationLogPageDto> {
    const payload: Record<string, unknown> = {
      limit: params.limit ?? 20,
      offset: params.offset ?? 0,
    }
    if (params.from) payload.from = params.from
    if (params.to) payload.to = params.to
    // Host operation_log 以大写存储 action/result 并做等值过滤：统一大写后再传（避免整词大小写失配）。
    if (params.action) payload.action = params.action.trim().toUpperCase()
    if (params.componentId) payload.componentId = params.componentId
    if (params.result) payload.result = params.result.trim().toUpperCase()
    return normalizeOperationLogPage(await request<unknown>('getOperationLog', payload))
  },
  /** 导出操作审计（契约 `{format:'csv'|'json'}` → `{path,count}`；path 必填，否则视为失败）。 */
  async exportOperationLog(format: 'csv' | 'json'): Promise<ExportOperationLogResultDto> {
    return normalizeExportOperationLogResult(await request<unknown>('exportOperationLog', { format }))
  },
  // ── M2b-2 备份 ref 恢复（按组件/仓列出 SYNC 备份 ref，确认后恢复） ──
  /** 按组件（可选仓类型）读取可恢复的备份 ref（契约 `{componentId,repoType?}` → `{refs}`）。 */
  async getBackupRefs(params: BackupRefsParams): Promise<BackupRefsDto> {
    const payload: Record<string, unknown> = { componentId: params.componentId }
    if (params.repoType) payload.repoType = params.repoType
    return normalizeBackupRefs(await request<unknown>('getBackupRefs', payload))
  },
  /** 恢复到指定备份 ref（契约 `{componentId,repoType,ref,confirm:true}` → `{ok,preHead,postHead,backupRef}`）。 */
  async restoreBackupRef(params: RestoreBackupRefParams): Promise<RestoreBackupRefResultDto> {
    return normalizeRestoreBackupRefResult(
      await request<unknown>('restoreBackupRef', {
        componentId: params.componentId,
        repoType: params.repoType,
        ref: params.ref,
        confirm: true,
      }),
    )
  },
  /** 可选能力：按保留期清理审计/备份记录（Host 未实现时由调用方降级提示）。 */
  async cleanupRetention(): Promise<CleanupRetentionResultDto> {
    return normalizeCleanupRetentionResult(await request<unknown>('cleanupRetention', {}))
  },
}

function normalizeSnapshotRequired(value: unknown): SnapshotDto {
  const snapshot = normalizeSnapshot(value)
  if (!snapshot) throw new HostError('E_INVALID_RESPONSE', 'Host 返回的快照格式无法识别')
  return snapshot
}
