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
  | 'getCiStatus'
  | 'getHostInfo'

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
  getCiStatus: 2_000,
  getHostInfo: 2_000,
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
  }
}

function getBridge(): WebView2Bridge | null {
  if (typeof window === 'undefined') return null
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

function onBridgeMessage(event: MessageEvent<unknown>) {
  const reply = asRecord(parseMaybeJson(event.data))
  if (!reply) return

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

function settleCall(id: string, reply: Record<string, unknown>) {
  const call = pending.get(id)
  if (!call) return
  pending.delete(id)
  clearTimeout(call.timer)
  if (pending.size === 0) detachListener()

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
      if (pending.size === 0) detachListener()
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
  async getCiStatus(componentId: string): Promise<CiStatusDto> {
    return normalizeCiStatus(await request<unknown>('getCiStatus', { componentId }))
  },
  async getHostInfo(): Promise<HostInfoDto> {
    return normalizeHostInfo(await request<unknown>('getHostInfo'))
  },
}

function normalizeSnapshotRequired(value: unknown): SnapshotDto {
  const snapshot = normalizeSnapshot(value)
  if (!snapshot) throw new HostError('E_INVALID_RESPONSE', 'Host 返回的快照格式无法识别')
  return snapshot
}
