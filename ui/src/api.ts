/**
 * UI ↔ Host 只读数据通道（WebView2 WebMessage，M1）。
 *
 * 约定：
 *   UI  → Host：window.chrome.webview.postMessage({ id, method })
 *   Host → UI：window.chrome.webview.postMessage({ id, ok: true, result: SnapshotDto })
 *              （同时兼容 { id, type, payload } / { id, snapshot } / 直接回快照等形态）
 *
 * 无 WebView2（浏览器 dev / 单测）或 Host 超时/报错 → reject，
 * 由 App.vue 捕获后回退到内置 Mock 数据，不打断页面。
 *
 * DTO 形状以 src/MbdGitTool.Core/Contracts/SnapshotDto.cs 为准（Host 侧 camelCase 序列化）。
 */

export interface RepoDto {
  repoType: string
  state: string
  subType: string | null
  branch: string | null
  headSha: string | null
  ahead: number
  behind: number
  dirtyCount: number
  untrackedCount: number
  isLocal: boolean
  localPath: string | null
  flags: string[]
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
}

export interface SnapshotDto {
  source: string
  workspaceRoot: string | null
  error: string | null
  generatedAt: string
  components: ComponentDto[]
}

export type RequestMethod = 'getSnapshot' | 'refresh'

export const DEFAULT_TIMEOUT_MS = 30000

interface WebView2Bridge {
  postMessage(message: unknown): void
  addEventListener(type: 'message', listener: (event: MessageEvent<unknown>) => void): void
  removeEventListener(type: 'message', listener: (event: MessageEvent<unknown>) => void): void
}

declare global {
  interface Window {
    chrome?: { webview?: WebView2Bridge }
  }
}

function getBridge(): WebView2Bridge | null {
  if (typeof window === 'undefined') return null
  const bridge = window.chrome?.webview
  if (!bridge || typeof bridge.postMessage !== 'function' || typeof bridge.addEventListener !== 'function') return null
  return bridge
}

/** 是否运行在 WebView2 宿主内（浏览器 dev 下为 false，App 据此回退 Mock）。 */
export function hasHostBridge(): boolean {
  return getBridge() !== null
}

/** 把任意异常转成可读文案（Toast 用）。 */
export function toErrorMessage(error: unknown): string {
  if (error instanceof Error) return error.message
  if (typeof error === 'string' && error.length > 0) return error
  return '未知错误'
}

/**
 * 向 Host 发一条请求并等待同 id 的响应。
 * - 无 WebView2 宿主 → reject（可读文案）
 * - 超时（默认 8000ms）→ reject（可读文案）
 * - Host 返回 ok=false / error → reject（Host 文案）
 */
export function request<T>(method: RequestMethod, timeoutMs = DEFAULT_TIMEOUT_MS): Promise<T> {
  const bridge = getBridge()
  if (!bridge) {
    return Promise.reject(new Error('未检测到 WebView2 宿主（window.chrome.webview 不可用），无法连接 Host'))
  }

  const id = crypto.randomUUID()

  return new Promise<T>((resolve, reject) => {
    let settled = false
    let timer: ReturnType<typeof setTimeout> | undefined

    const cleanup = () => {
      bridge.removeEventListener('message', onMessage)
      if (timer !== undefined) clearTimeout(timer)
    }
    const settle = (action: () => void) => {
      if (settled) return
      settled = true
      cleanup()
      action()
    }

    const onMessage = (event: MessageEvent<unknown>) => {
      const reply = asRecord(parseMaybeJson(event.data))
      if (!reply) return

      const replyId = reply['id']
      const hasId = replyId !== undefined && replyId !== null
      if (hasId && String(replyId) !== id) return // 其它请求的响应

      const payload = pickPayload(reply)
      // 兼容 Host 未回显 id 的情况：只接受“快照形态”的载荷，避免误吞其它事件。
      if (!hasId && !looksLikeSnapshot(payload)) return

      if (isErrorReply(reply, payload)) {
        settle(() => reject(new Error(readErrorText(reply) || `Host 拒绝了请求：${method}`)))
        return
      }
      settle(() => resolve(payload as T))
    }

    timer = setTimeout(() => {
      settle(() => reject(new Error(`等待 Host 响应超时（${method}，${timeoutMs}ms）；Host 可能未就绪`)))
    }, timeoutMs)

    bridge.addEventListener('message', onMessage)
    try {
      bridge.postMessage({ id, method })
    } catch (error) {
      settle(() => reject(new Error(`发送 Host 请求失败：${toErrorMessage(error)}`)))
    }
  })
}

/** 把 Host 回包规范化为 SnapshotDto；形状不符返回 null（调用方回退 Mock）。 */
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

  return {
    source,
    workspaceRoot: readNullableString(record, 'workspaceRoot'),
    error: readNullableString(record, 'error'),
    generatedAt: readString(record, 'generatedAt') ?? '',
    components: components
      .map(normalizeComponent)
      .filter((component): component is ComponentDto => component !== null),
  }
}

const RESULT_KEYS = ['result', 'payload', 'snapshot', 'data'] as const

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
  for (const key of RESULT_KEYS) {
    const value = reply[key]
    if (value !== undefined && value !== null) return parseMaybeJson(value)
  }
  return reply
}

function looksLikeSnapshot(value: unknown): boolean {
  const record = asRecord(parseMaybeJson(value))
  return record !== null && Array.isArray(record['components'])
}

function isErrorReply(reply: Record<string, unknown>, payload: unknown): boolean {
  if (reply['ok'] === false) return true
  const payloadRecord = asRecord(payload)
  if (payloadRecord?.['ok'] === false) return true
  if (looksLikeSnapshot(payload)) return false
  const hasResult = RESULT_KEYS.some((key) => reply[key] !== undefined && reply[key] !== null)
  if (hasResult) return false
  return typeof reply['error'] === 'string' && reply['error'].length > 0
}

function readErrorText(reply: Record<string, unknown>): string {
  const payload = asRecord(pickPayload(reply))
  const candidates: unknown[] = [reply['error'], reply['message'], payload?.['error'], payload?.['message']]
  for (const candidate of candidates) {
    if (typeof candidate === 'string' && candidate.length > 0) return candidate
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

function normalizeComponent(value: unknown): ComponentDto | null {
  const record = asRecord(value)
  if (!record) return null
  const componentId = readString(record, 'componentId') ?? readString(record, 'id')
  if (!componentId) return null
  return {
    componentId,
    aggregateState: readString(record, 'aggregateState') ?? 'UNKNOWN',
    severityRank: readNumber(record, 'severityRank', 2),
    branch: readNullableString(record, 'branch'),
    tag: readNullableString(record, 'tag'),
    mixedBranch: readBoolean(record, 'mixedBranch'),
    flags: readStringArray(record, 'flags'),
    repos: (readArray(record, 'repos') ?? [])
      .map(normalizeRepo)
      .filter((repo): repo is RepoDto => repo !== null),
  }
}

function normalizeRepo(value: unknown): RepoDto | null {
  const record = asRecord(value)
  if (!record) return null
  const repoType = readString(record, 'repoType')
  if (!repoType) return null
  return {
    repoType: repoType.toUpperCase(),
    state: readString(record, 'state') ?? 'UNKNOWN',
    subType: readNullableString(record, 'subType'),
    branch: readNullableString(record, 'branch'),
    headSha: readNullableString(record, 'headSha'),
    ahead: readNumber(record, 'ahead'),
    behind: readNumber(record, 'behind'),
    dirtyCount: readNumber(record, 'dirtyCount'),
    untrackedCount: readNumber(record, 'untrackedCount'),
    isLocal: readBoolean(record, 'isLocal'),
    localPath: readNullableString(record, 'localPath'),
    flags: readStringArray(record, 'flags'),
  }
}

