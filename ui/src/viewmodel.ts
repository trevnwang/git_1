/**
 * DTO → VM 映射、状态 → tone/图标/文案、单仓动作可用性（Host actions 单源，spec §3.6 / §6.2）。
 * 组件模板禁止散落状态映射，统一走这里。
 */
import type {
  BackupRefDto,
  ComponentDto,
  CommitDto,
  JobDto,
  OperationLogItemDto,
  RepoActionAvailabilityDto,
  RepoDto,
  TreeNodeDto,
  WorkspaceNodeDto,
} from './api'
import { HostError, toErrorMessage } from './api'
import type {
  ActionAvailability,
  BackupRefVm,
  BatchKind,
  CommitVm,
  ComponentVm,
  GitStatus,
  HostActionAvailability,
  JobItemState,
  JobKind,
  JobState,
  JobVm,
  OperationLogItemVm,
  RepoAction,
  RepoDetailVm,
  RepoType,
  RepoVm,
  Tone,
  TreeNodeVm,
  WorkspaceNodeVm,
} from './types/workbench'
import { REPO_ACTIONS, REPO_TYPES } from './types/workbench'

interface StateMeta {
  tone: Tone
  icon: string
  label: string
}

/** 通道 A：组件聚合态 + 单仓主状态（SRS §7.4 / §7.2）。 */
export const STATE_META: Record<string, StateMeta> = {
  // 组件聚合态
  OP_PARTIAL_FAILED: { tone: 'danger', icon: '!', label: '部分失败' },
  BINDING_ERROR: { tone: 'danger', icon: '✕', label: '绑定异常' },
  BLOCKED_LOCAL: { tone: 'danger', icon: '✕', label: '阻塞' },
  TAG_DRIFT: { tone: 'warning', icon: '⚑', label: '基线不一致' },
  MIXED_BRANCH: { tone: 'cyan', icon: '⑂', label: '分支不一致' },
  NEW_BRANCH: { tone: 'cyan', icon: '⑂', label: '仅本地' },
  DETACHED: { tone: 'cyan', icon: '⌁', label: '游离' },
  EMPTY: { tone: 'neutral', icon: '∅', label: '空仓' },
  // 单仓主状态（与聚合态同名者复用）
  SYNCED: { tone: 'success', icon: '✓', label: '同步' },
  DIRTY: { tone: 'success', icon: '●', label: '未提交' },
  AHEAD: { tone: 'info', icon: '↑', label: '超前' },
  BEHIND: { tone: 'warning', icon: '↓', label: '落后' },
  DIVERGED: { tone: 'purple', icon: '⇄', label: '分叉' },
  DETACHED_TAG: { tone: 'cyan', icon: '⚑', label: '基线 tag' },
  DETACHED_COMMIT: { tone: 'cyan', icon: '⌁', label: '游离 HEAD' },
  NO_UPSTREAM: { tone: 'cyan', icon: '⑂', label: '仅本地' },
  UNMERGED: { tone: 'danger', icon: '✕', label: '冲突未合' },
  IN_PROGRESS: { tone: 'warning', icon: '◷', label: '操作中' },
  INVALID: { tone: 'danger', icon: '✕', label: '绑定异常' },
  MISSING: { tone: 'neutral', icon: '∅', label: '未克隆' },
  UNKNOWN: { tone: 'hatched', icon: '⏱', label: '未知' },
}

export function stateMeta(state: string): StateMeta {
  return STATE_META[state.toUpperCase()] ?? { tone: 'neutral', icon: '•', label: state || '未知' }
}

export function stateTone(state: string): Tone {
  return stateMeta(state).tone
}

export function stateLabel(state: string): string {
  const meta = stateMeta(state)
  return `${meta.icon} ${meta.label}`
}

export const REPO_LABEL: Record<RepoType, string> = {
  DESIGN: 'Design',
  IMPL: 'Impl',
  MIL: 'MIL',
  SIL: 'SIL',
}

export const REPO_SHORT: Record<RepoType, string> = {
  DESIGN: 'D',
  IMPL: 'I',
  MIL: 'M',
  SIL: 'S',
}

export const CI_LABEL: Record<string, string> = {
  PASS: 'CI 通过',
  FAIL: 'CI 失败',
  RUNNING: 'CI 运行中',
  QUEUED: 'CI 排队',
  NONE: 'CI 未接入',
  UNKNOWN: 'CI 未知',
}

export function ciLabel(state: string): string {
  return CI_LABEL[state] ?? 'CI 未接入'
}

export function emptyRepoVm(repoType: RepoType): RepoVm {
  return {
    repoType,
    repoName: '',
    state: 'MISSING',
    tone: 'neutral',
    branch: '—',
    tag: null,
    headSha: null,
    ahead: 0,
    behind: 0,
    dirtyCount: 0,
    untrackedCount: 0,
    stagedCount: 0,
    stashCount: 0,
    isLocal: false,
    localPath: null,
    compareRef: null,
    calcAt: null,
    dataSource: null,
    isStale: false,
    errorCode: null,
    flags: [],
    ci: 'NONE',
  }
}

const EMPTY_COMPONENT: ComponentVm = {
  id: '',
  state: 'UNKNOWN',
  tone: 'hatched',
  severityRank: 99,
  branch: '—',
  tag: null,
  mixedBranch: false,
  dirtyTotal: 0,
  untrackedTotal: 0,
  starred: false,
  repos: [],
  ci: 'NONE',
  lastActivityAt: null,
  incomplete: false,
}

export function emptyComponentVm(): ComponentVm {
  return { ...EMPTY_COMPONENT, repos: [] }
}

export function mapRepo(dto: RepoDto, fallbackName: string): RepoVm {
  const repoType = (dto.repoType.toUpperCase() in REPO_LABEL
    ? dto.repoType.toUpperCase()
    : 'IMPL') as RepoType
  const state = (dto.state || 'UNKNOWN').toUpperCase()
  const repoName = dto.repoName ?? (dto.isLocal ? fallbackName : '')
  return {
    repoType,
    repoName,
    state,
    tone: stateTone(state),
    branch: dto.branch ?? '—',
    tag: dto.tag ?? null,
    headSha: dto.headSha ?? null,
    ahead: dto.ahead ?? 0,
    behind: dto.behind ?? 0,
    dirtyCount: dto.dirtyCount ?? 0,
    untrackedCount: dto.untrackedCount ?? 0,
  stagedCount: dto.stagedCount ?? 0,
    stashCount: dto.stashCount ?? 0,
    isLocal: Boolean(dto.isLocal),
    localPath: dto.localPath ?? null,
    compareRef: dto.compareRef ?? dto.upstreamRef ?? null,
    calcAt: dto.calcAt ?? null,
    dataSource: dto.dataSource ?? null,
    isStale: Boolean(dto.isStale),
    errorCode: dto.errorCode ?? null,
    flags: Array.isArray(dto.flags) ? dto.flags : [],
    ci: (dto.ci?.state ?? 'NONE') as RepoVm['ci'],
  }
}

export function mapComponent(dto: ComponentDto): ComponentVm {
  const state = (dto.aggregateState || 'UNKNOWN').toUpperCase()
  const repos = dto.repos.map((repo) =>
    mapRepo(repo, `${dto.componentId}_${repo.repoType}`),
  )
  // 契约只认 4 类；按 DESIGN/IMPL/MIL/SIL 补齐槽位（缺仓 = MISSING，不伪造数据）
  const byType = new Map(repos.map((repo) => [repo.repoType, repo]))
  const completeRepos = REPO_TYPES.map((type) => byType.get(type) ?? emptyRepoVm(type))
  const dirtyTotal = repos.reduce((sum, repo) => sum + repo.dirtyCount, 0)
  const untrackedTotal = repos.reduce((sum, repo) => sum + repo.untrackedCount, 0)
  const leadRepo = byType.get('IMPL') ?? repos.find((repo) => repo.state !== 'MISSING') ?? null
  return {
    id: dto.componentId,
    state,
    tone: stateTone(state),
    severityRank: dto.severityRank ?? 99,
    branch: dto.branch ?? leadRepo?.branch ?? '—',
    tag: dto.tag ?? leadRepo?.tag ?? null,
    mixedBranch: Boolean(dto.mixedBranch),
    dirtyTotal,
    untrackedTotal,
    starred: dirtyTotal + untrackedTotal > 0,
    repos: completeRepos,
    ci: (dto.ciAggregate?.worstState ?? 'NONE') as ComponentVm['ci'],
    lastActivityAt: dto.lastActivityAt ?? null,
    incomplete: Boolean(dto.incomplete),
  }
}

export function repoOf(component: ComponentVm, repoType: RepoType): RepoVm | null {
  return component.repos.find((repo) => repo.repoType === repoType) ?? null
}

/** 默认聚焦仓：IMPL 优先（且非 MISSING），否则第一个存在仓。 */
export function pickDefaultRepo(component: ComponentVm): RepoType {
  const impl = repoOf(component, 'IMPL')
  if (impl && impl.state !== 'MISSING') return 'IMPL'
  const first = component.repos.find((repo) => repo.state !== 'MISSING')
  return first?.repoType ?? 'DESIGN'
}

// ───────────────────────────── 时间 / 数字 / 路径 ─────────────────────────────

const pad = (value: number) => String(value).padStart(2, '0')

export function formatStamp(value: string | null | undefined): string {
  if (!value) return '—'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  return `${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}`
}

export function formatClock(value: string | null | undefined): string {
  if (!value) return '—'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  return `${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}`
}

export function formatAgo(value: string | null | undefined, now = Date.now()): string {
  if (!value) return '—'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  const delta = Math.max(0, now - date.getTime())
  const sec = Math.floor(delta / 1000)
  if (sec < 60) return `${sec}s 前`
  const min = Math.floor(sec / 60)
  if (min < 60) return `${min}min 前`
  const hour = Math.floor(min / 60)
  if (hour < 24) return `${hour}h 前`
  return `${Math.floor(hour / 24)}d 前`
}

export function shortSha(sha: string | null | undefined): string {
  if (!sha) return '—'
  return sha.slice(0, 7)
}

export function formatBytes(size: number | null | undefined): string {
  if (size === null || size === undefined) return '—'
  if (size < 1024) return `${size} B`
  if (size < 1024 * 1024) return `${(size / 1024).toFixed(1)} KB`
  return `${(size / 1024 / 1024).toFixed(1)} MB`
}

export function fileExtension(name: string): string {
  const at = name.lastIndexOf('.')
  if (at <= 0) return ''
  return name.slice(at).toLowerCase()
}

/** FR-DETAIL-03 白名单：可双击直开。 */
export const SAFE_EXTENSIONS = new Set([
  '.m',
  '.c',
  '.h',
  '.slx',
  '.mdl',
  '.json',
  '.xml',
  '.yaml',
  '.yml',
  '.txt',
  '.md',
  '.csv',
  '.hpp',
  '.cpp',
  '.log',
  '.ini',
  '.gitignore',
])

/** 危险扩展名：双击需二次确认（FR-DETAIL-03 / §4.6）。 */
export const DANGEROUS_EXTENSIONS = new Set([
  '.exe',
  '.bat',
  '.cmd',
  '.ps1',
  '.vbs',
  '.js',
  '.jse',
  '.wsf',
  '.msi',
  '.lnk',
  '.scr',
  '.reg',
  '.url',
  '.com',
  '.pif',
  '.cpl',
  '.jar',
  '.sh',
])

export function isSafeFile(name: string): boolean {
  return SAFE_EXTENSIONS.has(fileExtension(name))
}

export function isDangerousFile(name: string): boolean {
  return DANGEROUS_EXTENSIONS.has(fileExtension(name))
}

// ───────────────────────────── 树节点映射 ─────────────────────────────

const GIT_STATUSES: GitStatus[] = ['NONE', 'MODIFIED', 'ADDED', 'DELETED', 'UNTRACKED', 'CONFLICT']

function normalizeGitStatus(value: string | null | undefined): GitStatus {
  const upper = (value ?? 'NONE').toUpperCase() as GitStatus
  return GIT_STATUSES.includes(upper) ? upper : 'NONE'
}

export function mapTreeNode(dto: TreeNodeDto): TreeNodeVm {
  const children = (dto.children ?? []).map(mapTreeNode)
  return {
    path: dto.path,
    name: dto.name,
    kind: dto.kind,
    hasChildren: dto.hasChildren || dto.kind === 'DIR',
    size: dto.size ?? null,
    mtime: dto.mtime ?? null,
    gitStatus: normalizeGitStatus(dto.gitStatus),
    submodule: Boolean(dto.submodule),
    ignored: Boolean(dto.ignored),
    error: dto.error ?? null,
    loaded: dto.children !== null && dto.children !== undefined,
    children,
  }
}

export function mapTreeNodes(dtos: TreeNodeDto[]): TreeNodeVm[] {
  return dtos.map(mapTreeNode)
}

export function gitStatusLetter(status: GitStatus): string {
  switch (status) {
    case 'MODIFIED':
      return 'M'
    case 'ADDED':
      return 'A'
    case 'DELETED':
      return 'D'
    case 'UNTRACKED':
      return 'U'
    case 'CONFLICT':
      return '!'
    default:
      return ''
  }
}

export function gitStatusTone(status: GitStatus): Tone {
  switch (status) {
    case 'MODIFIED':
      return 'warning'
    case 'ADDED':
    case 'UNTRACKED':
      return 'success'
    case 'DELETED':
    case 'CONFLICT':
      return 'danger'
    default:
      return 'neutral'
  }
}

/** 目录优先 → 名称升序（大小写不敏感）→ 扩展名（FR-TREE-01）。 */
export function sortTreeNodes(nodes: TreeNodeVm[]): TreeNodeVm[] {
  return [...nodes].sort((a, b) => {
    if (a.kind !== b.kind) return a.kind === 'DIR' ? -1 : 1
    const byName = a.name.localeCompare(b.name, 'zh-CN', { sensitivity: 'base' })
    if (byName !== 0) return byName
    return fileExtension(a.name).localeCompare(fileExtension(b.name))
  })
}

// ───────────────────────────── 文件夹视图节点映射 ─────────────────────────────

const WS_KIND_MAP: Record<string, WorkspaceNodeVm['kind']> = {
  COMPONENT: 'COMPONENT',
  REPO: 'REPO',
  DIR: 'DIR',
  FILE: 'FILE',
  UNBOUND_GROUP: 'UNBOUND_GROUP',
}

function mapWorkspaceNode(dto: WorkspaceNodeDto, depth = 0): WorkspaceNodeVm {
  const kind = WS_KIND_MAP[dto.kind] ?? (dto.kind === 'REPO' ? 'REPO' : 'DIR')
  const repos = (dto.repos ?? []).map((repo) =>
    mapWorkspaceNode(repo, depth + 1),
  )
  const children = (dto.children ?? []).map((child) =>
    mapWorkspaceNode(child, depth + 1),
  )
  const repoTypeRaw = (dto.repoType ?? '').toUpperCase()
  const repoType = (repoTypeRaw in REPO_LABEL ? repoTypeRaw : null) as RepoType | null
  const path = dto.path ?? dto.componentId ?? ''
  const label =
    dto.name ??
    (kind === 'COMPONENT'
      ? dto.componentId ?? ''
      : kind === 'UNBOUND_GROUP'
        ? `未纳管（${dto.count ?? children.length}）`
        : path.split('/').filter(Boolean).pop() ?? path)
  return {
    key: kind === 'COMPONENT' ? `component:${dto.componentId ?? path}` : path || label,
    kind,
    label,
    path,
    hasChildren:
      dto.hasChildren ??
      (kind === 'COMPONENT' || kind === 'UNBOUND_GROUP' ? repos.length + children.length > 0 : Boolean(dto.isLocal)),
    componentId: dto.componentId ?? null,
    repoType,
    state: (dto.state ?? '').toUpperCase(),
    tone: stateTone(dto.state ?? ''),
    branch: dto.branch ?? null,
    ahead: dto.ahead ?? 0,
    behind: dto.behind ?? 0,
    dirtyCount: dto.dirtyCount ?? 0,
    gitStatus: normalizeGitStatus(dto.gitStatus),
    size: dto.size ?? null,
    mtime: dto.mtime ?? null,
    count: dto.count ?? 0,
    error: dto.error ?? null,
    repos,
    loaded: (dto.children !== null && dto.children !== undefined) || repos.length > 0,
    children,
  }
}

export function mapWorkspaceEntries(dtos: WorkspaceNodeDto[]): WorkspaceNodeVm[] {
  const mapped = dtos.map((dto) => mapWorkspaceNode(dto))
  // 组件按 severityRank 升序 → ID；未纳管组固定在后
  return mapped.sort((a, b) => {
    if (a.kind === 'UNBOUND_GROUP' && b.kind !== 'UNBOUND_GROUP') return 1
    if (b.kind === 'UNBOUND_GROUP' && a.kind !== 'UNBOUND_GROUP') return -1
    if (a.kind === 'COMPONENT' && b.kind === 'COMPONENT') {
      const sa = STATE_META[a.state] ? 0 : 1
      const sb = STATE_META[b.state] ? 0 : 1
      if (sa !== sb) return sa - sb
      return a.label.localeCompare(b.label)
    }
    return 0
  })
}

export function flattenWorkspaceChildren(node: WorkspaceNodeVm): WorkspaceNodeVm[] {
  // 目录/仓展开返回物理条目：按 Host 返回顺序渲染（DIR 优先排序由 Host 完成，UI 兜底）
  return node.children
}

// ───────────────────────────── 单仓动作可用性（M2a：Host 单源矩阵） ─────────────────────────────

/**
 * AC-UI-08 单源矩阵：可用/禁用/确认标志**只**消费 Host `getComponentDetail.repos[].actions`
 * （`enabled` / `requiresConfirm` / `reasonCode` / `reason`），UI 不再持有「状态 × 动作」矩阵。
 * 本文件只负责把 Host actions 映射成按钮状态与 tooltip 文案（原因 / 替代动作 / 错误码）。
 */
export const HOST_ACTIONS_UNREADY_REASON = '正在读取 Host 能力'

/** Host actions 读取状态文案（供状态说明/调试；不参与可用性判定）。 */
export const HOST_ACTIONS_STATUS_LABEL: Record<'idle' | 'loading' | 'ready' | 'error', string> = {
  idle: '—',
  loading: HOST_ACTIONS_UNREADY_REASON,
  ready: 'Host 能力已就绪',
  error: 'Host 能力读取失败',
}

/** Host ReasonCode → UI 错误码（未知原因码回退到 E_ACTION_NOT_ALLOWED）。 */
const REASON_CODE_TO_ERROR: Record<string, string> = {
  STATE_BLOCKED: 'E_ACTION_NOT_ALLOWED',
  CONFIRM_REQUIRED: 'E_CONFIRM_REQUIRED',
  AVAILABLE: '',
}

/**
 * 仅用于禁用 tooltip 文案：原因关键词 → 错误码 + 替代动作。
 * 不参与 enable/confirm 判定（那是 Host actions 的唯一职责）。
 */
const REASON_HINTS: { match: RegExp; code: string; alt: string }[] = [
  { match: /未克隆|MISSING/, code: 'E_REPO_NOT_LOCAL', alt: '先 Clone' },
  { match: /DIRTY|需先提交或 stash/, code: 'E_DIRTY_WORKTREE', alt: '先 COMMIT 或 stash 后再 PULL' },
  { match: /NO_UPSTREAM|未配置 upstream/, code: 'E_ACTION_NOT_ALLOWED', alt: '先 PUSH 发布分支并建立 upstream' },
  { match: /AHEAD|需先推送/, code: 'E_ACTION_NOT_ALLOWED', alt: '先 PUSH 本地提交' },
  { match: /BEHIND|先 PULL/, code: 'E_ACTION_NOT_ALLOWED', alt: '先 PULL 快进后再试' },
  { match: /DIVERGED|Merge\/Rebase/, code: 'E_ACTION_NOT_ALLOWED', alt: '先手动 Merge/Rebase 后重扫' },
  { match: /DETACHED|游离 HEAD/, code: 'E_ACTION_NOT_ALLOWED', alt: '基于当前 HEAD 创建分支' },
  { match: /UNMERGED|未解决冲突/, code: 'E_ACTION_NOT_ALLOWED', alt: '先解决冲突并 COMMIT' },
  { match: /IN_PROGRESS|变基未完成|操作未完成/, code: 'E_BUSY', alt: '等待 git 操作完成后重试' },
  { match: /INVALID|绑定异常/, code: 'E_ACTION_NOT_ALLOWED', alt: '修复仓库绑定后重试' },
  { match: /无已暂存改动|已暂存为 0/, code: 'E_NOTHING_TO_COMMIT', alt: '先 git add 暂存改动（已暂存后可勾选「暂存全部改动」一并提交）' },
  { match: /UNKNOWN|关系数据过期|状态未知|数据过期/, code: 'E_ACTION_NOT_ALLOWED', alt: '先 FETCH 或重试扫描' },
]

/** 通用禁用原因的替代动作按动作细化（仅文案，不改变 Host 的 enable/confirm）。 */
const NA_ALT_BY_ACTION: Record<RepoAction, string> = {
  PULL: '先处理本地/远程差异后重试',
  FETCH: '先 FETCH 刷新远程引用',
  CHKOUT: '先解决当前状态后再 CHKOUT',
  COMMIT: '先处理工作区状态后再 COMMIT',
  PUSH: '先 PULL 或解决冲突后再 PUSH',
}

function reasonHint(
  reason: string,
  reasonCode: string | undefined,
  action: RepoAction,
): { code: string; alt: string } {
  // M2b-2：EMPTY 首推建 upstream 已由 Host 矩阵恢复（enabled 单源），UI 不再本地冻结。
  for (const hint of REASON_HINTS) {
    if (hint.match.test(reason)) return { code: hint.code, alt: hint.alt }
  }
  const rawCode = (reasonCode ?? '').trim().toUpperCase()
  const mapped = REASON_CODE_TO_ERROR[rawCode] ?? (rawCode.startsWith('E_') ? rawCode : 'E_ACTION_NOT_ALLOWED')
  return { code: mapped || 'E_ACTION_NOT_ALLOWED', alt: NA_ALT_BY_ACTION[action] }
}

/** getComponentDetail.repos[].actions → 按五键索引的 Host 可用性（缺失动作保持禁用）。 */
export function mapHostActions(
  actions: RepoActionAvailabilityDto[] | null | undefined,
): Record<RepoAction, HostActionAvailability> {
  const list = actions ?? []
  const result = {} as Record<RepoAction, HostActionAvailability>
  for (const action of REPO_ACTIONS) {
    const dto = list.find((item) => (item.action ?? '').toUpperCase() === action)
    result[action] = {
      action,
      enabled: Boolean(dto?.enabled),
      reasonCode: dto?.reasonCode ?? (dto?.enabled ? 'AVAILABLE' : 'STATE_BLOCKED'),
      reason: dto?.reason ?? '',
      requiresConfirm: Boolean(dto?.requiresConfirm),
    }
  }
  return result
}

/**
 * Host actions → UI 五键可用性。
 * - `enabled` / `confirm` 完全取自 Host（唯一权威，AC-UI-08；禁用单元的 requiresConfirm 同样透传）；
 * - 禁用时补「原因 [E_*] → 替代动作」（FR-ACT-02 v0.3.1-r2 冻结格式）；
 * - COMMIT 的 staged 前置（FR-ACT-03）在 Host 矩阵之外按快照计数补充（不改变 Host 可用性语义）。
 */
export function hostAvailability(
  hostActions: Record<RepoAction, HostActionAvailability> | null | undefined,
  repo: RepoVm,
): Record<RepoAction, ActionAvailability> {
  const result = {} as Record<RepoAction, ActionAvailability>
  for (const action of REPO_ACTIONS) {
    const host = hostActions?.[action]
    if (!host) {
      result[action] = {
        enabled: false,
        reason: HOST_ACTIONS_UNREADY_REASON,
        confirm: false,
        code: 'E_BUSY',
        alt: '等待 Host 能力读取完成后重试',
      }
      continue
    }
    const requiresConfirm = Boolean(host.requiresConfirm)
    if (!host.enabled) {
      const reason = host.reason.trim() || '当前仓库状态不允许该操作'
      const hint = reasonHint(reason, host.reasonCode, action)
      result[action] = { enabled: false, reason, confirm: requiresConfirm, code: hint.code, alt: hint.alt }
      continue
    }
    // FR-ACT-03：COMMIT 仅在有已暂存改动时可用（staged 计数来自快照，Host 只给状态矩阵）。
    if (action === 'COMMIT' && repo.stagedCount <= 0) {
      result[action] = {
        enabled: false,
        reason: '无已暂存改动：请先 git add 暂存后提交',
        confirm: requiresConfirm,
        code: 'E_NOTHING_TO_COMMIT',
        alt: '先 git add 暂存改动（已暂存后可勾选「暂存全部改动」一并提交）',
      }
      continue
    }
    result[action] = { enabled: true, reason: '', confirm: requiresConfirm }
  }
  return result
}


export const ACTION_LABEL: Record<RepoAction, string> = {
  PULL: 'PULL',
  FETCH: 'FETCH',
  CHKOUT: 'CHKOUT',
  COMMIT: 'COMMIT',
  PUSH: 'PUSH',
}

export const ACTION_TITLE: Record<RepoAction, string> = {
  PULL: '快进拉取（--ff-only）',
  FETCH: '拉取远程引用，不作合并',
  CHKOUT: '检出分支 / Tag',
  COMMIT: '提交已暂存改动',
  PUSH: '推送当前分支',
}

// ───────────────────────────── runRepoAction 错误码映射（M2 契约） ─────────────────────────────

export interface ActionErrorInfo {
  code: string
  /** 中文标题（错误类别）。 */
  message: string
  /** 建议的下一步（显示在 Toast 第二段）。 */
  hint: string
  /** Host 返回的原始明细（可能为空）。 */
  detail: string
  retryable: boolean
}

interface ActionErrorMeta {
  message: string
  hint: string
  retryable?: boolean
}

const ACTION_ERROR_MAP: Record<string, ActionErrorMeta> = {
  E_CONFIRM_REQUIRED: {
    message: '该操作需要二次确认',
    hint: '确认后将携带 confirm=true 重试（只重试一次）',
  },
  E_DIRTY_WORKTREE: {
    message: '工作区存在未提交改动',
    hint: '请先 COMMIT 或 stash 后再执行',
  },
  E_NOTHING_TO_COMMIT: {
    message: '没有可提交的改动（staged 为空）',
    hint: '先暂存改动，再执行 COMMIT',
  },
  E_GIT_NON_FF: {
    message: '非快进合并被拒绝',
    hint: '请先 FETCH，再手动 Merge/Rebase；PULL 仅支持 --ff-only',
  },
  E_GIT_FAILED: {
    message: 'Git 执行失败',
    hint: '查看 Host 日志后重试（仓库未做破坏性改动）',
    retryable: true,
  },
  E_INVALID_ARGUMENT: {
    message: '参数无效',
    hint: '请检查分支/Tag 名称或提交信息后重试',
  },
  E_TIMEOUT: {
    message: '等待 Host 超时，结果未知',
    hint: '请先刷新查看仓库实际状态，再决定是否重试',
    retryable: true,
  },
  E_NOT_IMPLEMENTED: {
    message: 'M1 只读 / M2 需 Host 支持',
    hint: '当前构建未开放写操作（Host 返回 E_NOT_IMPLEMENTED）',
  },
  E_NO_HOST: {
    message: '未连接 Host',
    hint: '请通过桌面宿主运行，浏览器 Mock 不执行写操作',
  },
  E_ACTION_NOT_ALLOWED: {
    message: '当前仓库状态不允许该操作',
    hint: '请按状态矩阵先修复仓库状态（如先提交/先 PULL）',
  },
  E_BUSY: {
    message: '仓库被其他 git 进程占用',
    hint: '关闭外部 git 操作后重试；禁止删除 .git/index.lock',
    retryable: true,
  },
  E_CONCURRENT_CHANGE: {
    message: '仓库状态在预检后发生变化',
    hint: '请刷新后重试',
    retryable: true,
  },
  E_OFFLINE: {
    message: '网络不可达',
    hint: '检查网络/代理后重试',
    retryable: true,
  },
  E_REPO_NOT_LOCAL: {
    message: '仓库未克隆到本地',
    hint: '请先 Clone 该仓',
  },
  E_ACCESS_DENIED: {
    message: '访问被拒绝',
    hint: '检查路径/文件权限或以合适身份运行',
  },
  E_INTERNAL: {
    message: 'Host 内部错误',
    hint: '请重试；若持续失败请导出诊断日志',
    retryable: true,
  },
}

/** 把 runRepoAction 的任意异常映射为「错误码 + 中文消息 + 下一步」；未知码保留原样。 */
export function actionErrorInfo(error: unknown): ActionErrorInfo {
  const code = error instanceof HostError && error.code ? error.code : 'E_UNKNOWN'
  const raw = error instanceof HostError ? error.message : toErrorMessage(error)
  const meta = ACTION_ERROR_MAP[code]
  return {
    code,
    message: meta?.message ?? (raw || '操作失败'),
    hint: meta?.hint ?? '',
    detail: raw && raw !== meta?.message ? raw : '',
    retryable: meta?.retryable ?? false,
  }
}

/** 无聚焦仓 / 仓缺失时的统一禁用原因（FR-ACT-01 / AC-UI-08）。 */
export const NO_REPO_REASON = '请先在文件树中选择仓库'

/**
 * 禁用 tooltip 冻结格式（v0.3.1-r2）：`原因 [E_*] → 替代动作`。
 * 例：`无已暂存改动：请先 git add 暂存后提交 [E_NOTHING_TO_COMMIT] → 先 git add 暂存改动`。
 */
export function actionDisabledTitle(reason: string, code: string | undefined, alt: string | undefined): string {
  const reasonText = reason || '当前仓库状态不允许该操作'
  const altText = alt || '按 Host 返回的能力先修复仓库状态后重试'
  const codeText = code && /^E_[A-Z0-9_]+$/.test(code) ? code : 'E_ACTION_NOT_ALLOWED'
  return `${reasonText} [${codeText}] → ${altText}`
}

/**
 * 批量入口动作单源（P1-2）：BatchBar / 组件卡片右键菜单 / 菜单栏「操作」三处
 * 使用同一清单与文案，可用性由 store.batchActionState() 统一计算。
 *
 * `implemented=false` 的项（批量 COMMIT/PUSH：Host 已支持 kind，但 UI 尚无
 * message 输入/推送确认流程）在三处入口一律显式禁用，不静默隐藏。
 */
export interface BatchActionDef {
  kind: BatchKind
  /** 三处入口统一文案（批量条按钮用 aria-label，菜单追加 …）。 */
  label: string
  /** 批量条主按钮图标（仅 primary 项使用）。 */
  icon: string
  /** 悬停说明（动作语义/安全提示）。 */
  title: string
  danger?: boolean
  implemented: boolean
  /** true = 批量条常驻主按钮；false = 收进「更多」菜单。 */
  primary?: boolean
}

export const BATCH_ACTIONS: readonly BatchActionDef[] = [
  {
    kind: 'SYNC',
    label: '批量 SYNC',
    icon: '⟳',
    title: 'SYNC：四仓受控同步（远程覆盖本地；先预检 + 备份 ref，需确认）→ 任务中心',
    danger: true,
    implemented: true,
    primary: true,
  },
  {
    kind: 'CREATE',
    label: '批量 CREATE',
    icon: '＋',
    title: 'CREATE：四仓创建同名分支（需输入分支名；可选 dryRun）→ 任务中心',
    implemented: true,
    primary: true,
  },
  {
    kind: 'CLONE',
    label: '批量 Clone',
    icon: '⤓',
    title: 'Clone 缺失仓（预览缺失清单，可选 dryRun）→ 任务中心',
    implemented: true,
    primary: true,
  },
  {
    kind: 'DELETE',
    label: '批量 Delete',
    icon: '⋯',
    title: 'Delete：仅本地软删到 .trash（保留 30 天，需确认）→ 任务中心',
    danger: true,
    implemented: true,
    primary: true,
  },
  {
    kind: 'PULL',
    label: '批量 PULL',
    icon: '↓',
    title: 'PULL：四仓拉取并快进合并（本地有改动/分叉则失败，需确认）→ 任务中心',
    implemented: true,
  },
  {
    kind: 'FETCH',
    label: '批量 FETCH',
    icon: '⇣',
    title: 'FETCH：四仓只读拉取远端引用（不修改工作区，需确认）→ 任务中心',
    implemented: true,
  },
  {
    kind: 'CHKOUT',
    label: '批量 CHKOUT',
    icon: '⌁',
    title: 'CHKOUT：四仓检出指定分支/Tag（需输入 ref，可选 dryRun）→ 任务中心',
    implemented: true,
  },
  {
    kind: 'TAG',
    label: '批量 Tag',
    icon: '⚑',
    title: 'Tag：四仓打同名 Tag（需输入 Tag 名称；可选 dryRun）→ 任务中心',
    implemented: true,
  },
  {
    kind: 'RESTORE',
    label: '批量 Restore',
    icon: '↺',
    title: 'Restore：从工作区 .trash 恢复缺失仓（目标已存在则跳过，需确认）→ 任务中心',
    danger: true,
    implemented: true,
  },
  {
    kind: 'COMMIT',
    label: '批量 COMMIT（M2b-2）',
    icon: '✓',
    title: 'M2b-2 未接入：Host 已支持 COMMIT kind，UI 的提交说明输入/确认流程尚未实现',
    implemented: false,
  },
  {
    kind: 'PUSH',
    label: '批量 PUSH（M2b-2）',
    icon: '↑',
    title: 'M2b-2 未接入：Host 已支持 PUSH kind，UI 的推送确认流程尚未实现',
    implemented: false,
  },
]

export const BATCH_PRIMARY_ACTIONS = BATCH_ACTIONS.filter((action) => action.primary)
export const BATCH_SECONDARY_ACTIONS = BATCH_ACTIONS.filter((action) => !action.primary)

/** 由 kind 取统一动作定义（菜单/右键/批量条共用）。 */
export function batchActionDef(kind: string): BatchActionDef | undefined {
  return BATCH_ACTIONS.find((action) => action.kind === kind)
}

export function batchActionLabel(kind: string): string {
  return batchActionDef(kind)?.label ?? `批量 ${kind}`
}

export function componentSearchHit(
  component: ComponentVm,
  query: string,
): boolean {
  const q = query.trim().toLowerCase()
  if (!q) return true
  if (component.id.toLowerCase().includes(q)) return true
  if (component.branch.toLowerCase().includes(q)) return true
  if ((component.tag ?? '').toLowerCase().includes(q)) return true
  return component.repos.some(
    (repo) =>
      repo.branch.toLowerCase().includes(q) ||
      repo.repoName.toLowerCase().includes(q) ||
      (repo.headSha ?? '').toLowerCase().startsWith(q) ||
      repo.repoType.toLowerCase().includes(q),
  )
}

export function filterComponents(
  components: ComponentVm[],
  query: string,
  filter: { states: string[]; repoType: RepoType | null; dirtyOnly: boolean; unpushedOnly: boolean },
): ComponentVm[] {
  return components.filter((component) => {
    if (!componentSearchHit(component, query)) return false
    if (filter.states.length > 0 && !filter.states.includes(component.state)) return false
    if (filter.repoType) {
      const repo = repoOf(component, filter.repoType)
      if (!repo || repo.state === 'MISSING') return false
    }
    if (filter.dirtyOnly && component.dirtyTotal + component.untrackedTotal === 0) return false
    if (filter.unpushedOnly && !component.repos.some((repo) => repo.ahead > 0)) return false
    return true
  })
}

export function sortComponents(
  components: ComponentVm[],
  sortBy: 'severity' | 'id' | 'activity',
): ComponentVm[] {
  const list = [...components]
  if (sortBy === 'id') {
    list.sort((a, b) => a.id.localeCompare(b.id))
  } else if (sortBy === 'activity') {
    list.sort((a, b) => (b.lastActivityAt ?? '').localeCompare(a.lastActivityAt ?? ''))
  } else {
    list.sort((a, b) => a.severityRank - b.severityRank || a.id.localeCompare(b.id))
  }
  return list
}

export function mapDetail(
  dto: {
    repo: RepoDto | null
    commits: { total: number; items: CommitDto[] }
  },
  fallbackName: string,
): RepoDetailVm {
  return {
    repo: dto.repo ? mapRepo(dto.repo, fallbackName) : null,
    commitTotal: dto.commits.total,
    commits: dto.commits.items.map(mapCommit),
    errorCode: null,
  }
}

export function mapCommit(dto: CommitDto): CommitVm {
  return {
    sha: dto.sha,
    shortSha: dto.shortSha ?? dto.sha.slice(0, 7),
    message: dto.message,
    author: dto.author,
    at: dto.at,
    refs: dto.refs ?? [],
  }
}

// ───────────────────────────── M2b 任务中心（Job DTO → VM） ─────────────────────────────

interface JobStateMeta {
  tone: Tone
  icon: string
  label: string
}

const JOB_STATE_META: Record<JobState, JobStateMeta> = {
  PENDING: { tone: 'neutral', icon: '◷', label: '排队' },
  RUNNING: { tone: 'info', icon: '◷', label: '进行中' },
  PARTIAL: { tone: 'warning', icon: '!', label: '部分失败' },
  SUCCESS: { tone: 'success', icon: '✓', label: '成功' },
  FAILED: { tone: 'danger', icon: '✕', label: '失败' },
  CANCELED: { tone: 'neutral', icon: '⊘', label: '已取消' },
  INTERRUPTED: { tone: 'danger', icon: '⚠', label: '已中断' },
  PREVIEW: { tone: 'info', icon: '◷', label: '预览完成' },
}

const JOB_ITEM_META: Record<JobItemState, JobStateMeta> = {
  PENDING: { tone: 'neutral', icon: '◷', label: '待执行' },
  RUNNING: { tone: 'info', icon: '◷', label: '进行中' },
  OK: { tone: 'success', icon: '✓', label: '成功' },
  SUCCESS: { tone: 'success', icon: '✓', label: '成功' },
  FAILED: { tone: 'danger', icon: '✕', label: '失败' },
  SKIPPED: { tone: 'warning', icon: '↷', label: '跳过' },
  CANCELED: { tone: 'neutral', icon: '⊘', label: '已取消' },
  INTERRUPTED: { tone: 'danger', icon: '⚠', label: '已中断' },
  PREVIEW: { tone: 'warning', icon: '◷', label: '预览（未执行）' },
}

export function jobStateMeta(state: string): JobStateMeta {
  return JOB_STATE_META[(state || '').toUpperCase() as JobState] ?? { tone: 'neutral', icon: '•', label: state || '未知' }
}

export function jobItemStateMeta(state: string): JobStateMeta {
  return (
    JOB_ITEM_META[(state || '').toUpperCase() as JobItemState] ?? { tone: 'neutral', icon: '•', label: state || '未知' }
  )
}

/** 批量作业类型中文标签（任务中心行首）。 */
export const JOB_KIND_LABEL: Record<string, string> = {
  SYNC: '批量 SYNC',
  CREATE: '批量 CREATE',
  CLONE: '批量 Clone',
  DELETE: '批量删除',
  TAG: '批量 Tag',
  RESTORE: '恢复备份',
  PULL: '批量 PULL',
  FETCH: '批量 FETCH',
  CHKOUT: '批量 CHKOUT',
  COMMIT: '批量 COMMIT',
  PUSH: '批量 PUSH',
}

export function jobKindLabel(kind: string): string {
  return JOB_KIND_LABEL[(kind || '').toUpperCase()] ?? (kind || '作业')
}

export function mapJobItem(dto: JobDto['items'][number]): JobVm['items'][number] {
  return {
    componentId: dto.componentId,
    repoType: (dto.repoType || '').toUpperCase(),
    repoName: dto.repoName ?? '',
    state: (dto.state || 'PENDING').toUpperCase() as JobItemState,
    startedAt: dto.startedAt ?? null,
    finishedAt: dto.finishedAt ?? null,
    errorCode: dto.errorCode ?? null,
    message: dto.message ?? null,
    preHead: dto.preHead ?? null,
    postHead: dto.postHead ?? null,
    backupRef: dto.backupRef ?? null,
    preview: dto.preview === true,
  }
}

export function mapJob(dto: JobDto): JobVm {
  const items = (dto.items ?? []).map(mapJobItem)
  const state = (dto.state || 'PENDING').toUpperCase() as JobState
  return {
    id: dto.id,
    kind: (dto.kind || '').toUpperCase() as JobKind,
    state,
    total: dto.total ?? items.length,
    ok: dto.ok ?? 0,
    failed: dto.failed ?? 0,
    skipped: dto.skipped ?? 0,
    createdAt: dto.createdAt ?? '',
    startedAt: dto.startedAt ?? null,
    finishedAt: dto.finishedAt ?? null,
    cancellable: Boolean(dto.cancellable),
    items,
    active: state === 'PENDING' || state === 'RUNNING',
    dryRun: dto.dryRun === true ? true : undefined,
    partial: dto.partial === true,
  }
}

/**
 * dryRun 预览作业识别（P1-1）：提交时 args.dryRun=true 由 store 记录 jobId；
 * 历史/重启后无 args 可用，按「逐仓全部 SKIPPED 且消息含 DRY_RUN/预览」识别。
 * 仅用于展示（徽标/文案），不影响执行语义。
 */
const DRY_RUN_MESSAGE = /(DRY[_-]?RUN|dryRun|仅预览|未执行任何写操作)/i

export function jobIsDryRun(job: JobVm): boolean {
  // Host M2b-1 修复后直接提供 dryRun / state=PREVIEW；历史数据走「全 SKIPPED + DRY_RUN 消息」兜底。
  if (job.dryRun || job.state === 'PREVIEW') return true
  if (job.items.length === 0) return false
  if (job.items.some((item) => item.state === 'PREVIEW')) return true
  if (job.items.some((item) => item.state !== 'SKIPPED')) return false
  return job.items.some((item) => DRY_RUN_MESSAGE.test(item.message ?? ''))
}

/** 逐仓已取消计数（job 行展示“已取消 N”，替代对 skipped 的误读）。 */
export function jobCanceledCount(job: JobVm): number {
  return job.items.filter((item) => item.state === 'CANCELED').length
}

/**
 * 逐仓错误码展示（P0-1 前端侧）：取消不是错误。
 * - CANCELED 项不展示错误码（旧 Host 数据把未开始项写成 CANCELED+E_TIMEOUT，一律不展示，避免“超时”误导）；
 * - 其余状态照常展示 errorCode。
 */
export function jobItemVisibleErrorCode(item: JobVm['items'][number]): string | null {
  if (item.state === 'CANCELED') return null
  return item.errorCode ?? null
}

/** 取消项说明：明确“已取消（未开始）”，不透传 E_TIMEOUT/超时语义。 */
export function jobItemCancelNote(item: JobVm['items'][number]): string {
  if (item.state !== 'CANCELED') return ''
  const message = (item.message ?? '').trim()
  if (!message) return '已取消（未开始）'
  if (/E_TIMEOUT|超时|timeout/i.test(message)) return '已取消（未开始）'
  if (/未开始|未执行/.test(message)) return '已取消（未开始）'
  return `已取消 · ${message}`
}

/** 进度百分比（ok+failed+skipped 已完成项；0 total 时为 0）。 */
export function jobProgressPercent(job: JobVm): number {
  if (job.total <= 0) return 0
  const done = job.ok + job.failed + job.skipped
  return Math.max(0, Math.min(100, Math.round((done / job.total) * 100)))
}

/** 「仅重试失败项」可见条件：终态且存在失败/取消/中断的逐仓项。 */
export function jobRetryableCount(job: JobVm): number {
  return job.items.filter((item) => item.state === 'FAILED' || item.state === 'CANCELED' || item.state === 'INTERRUPTED')
    .length
}

// ───────────────────────────── M2b-2 操作审计 / 备份 ref ─────────────────────────────

/** 审计 result → tone/中文标签（Host 值 OK/FAILED/SKIPPED/CANCELED/INTERRUPTED；未知保留原样）。 */
export const AUDIT_RESULT_META: Record<string, { tone: Tone; label: string }> = {
  OK: { tone: 'success', label: '成功' },
  SUCCESS: { tone: 'success', label: '成功' },
  FAILED: { tone: 'danger', label: '失败' },
  SKIPPED: { tone: 'warning', label: '跳过' },
  CANCELED: { tone: 'neutral', label: '已取消' },
  CANCELLED: { tone: 'neutral', label: '已取消' },
  INTERRUPTED: { tone: 'danger', label: '已中断' },
  PREVIEW: { tone: 'info', label: '预览' },
}

export function auditResultMeta(result: string): { tone: Tone; label: string } {
  const key = (result || '').toUpperCase()
  return AUDIT_RESULT_META[key] ?? { tone: 'neutral', label: result || '—' }
}

/** 审计动作中文标签（与 job.kind 同集；未知动作保留原样大写）。 */
export const AUDIT_ACTION_LABEL: Record<string, string> = {
  SYNC: 'SYNC 受控同步',
  CREATE: 'CREATE 建分支',
  CLONE: 'Clone',
  DELETE: 'Delete 软删',
  RESTORE: 'Restore 恢复',
  TAG: 'Tag',
  PULL: 'PULL',
  FETCH: 'FETCH',
  CHKOUT: 'CHKOUT',
  COMMIT: 'COMMIT',
  PUSH: 'PUSH',
  OPEN: '打开文件',
  REVEAL: '打开目录',
}

export function auditActionLabel(action: string): string {
  const key = (action || '').toUpperCase()
  return AUDIT_ACTION_LABEL[key] ?? (key || '—')
}

/** DTO → 审计 VM（字段缺失保持空串/null，不伪造行）。 */
export function mapOperationLogItem(dto: OperationLogItemDto): OperationLogItemVm {
  return {
    id: dto.id,
    ts: dto.ts,
    actor: dto.actor,
    action: (dto.action || '').toUpperCase(),
    componentId: dto.componentId ?? '',
    repoType: (dto.repoType ?? '').toUpperCase(),
    result: (dto.result || '').toUpperCase(),
    errorCode: dto.errorCode,
    requestId: dto.requestId,
    jobId: dto.jobId,
    jobItemId: dto.jobItemId,
    backupRef: dto.backupRef,
    beforeHead: dto.beforeHead,
    afterHead: dto.afterHead,
  }
}

/** DTO → 备份 ref VM（sha 短显示由组件截取；此处保留完整 sha）。 */
export function mapBackupRef(dto: BackupRefDto): BackupRefVm {
  return {
    ref: dto.ref,
    sha: dto.sha,
    createdAt: dto.createdAt,
    repoType: (dto.repoType || '').toUpperCase(),
    branch: dto.branch ?? null,
    format: dto.format ?? (dto.branch ? 'BRANCH' : 'LEGACY'),
  }
}

export function formatDurationMs(startAt: string | null, endAt: string | null): string {
  if (!startAt) return '—'
  const start = new Date(startAt).getTime()
  const end = endAt ? new Date(endAt).getTime() : Date.now()
  if (Number.isNaN(start) || Number.isNaN(end) || end < start) return '—'
  const delta = end - start
  if (delta < 1000) return `${delta}ms`
  if (delta < 60_000) return `${(delta / 1000).toFixed(1)}s`
  return `${Math.floor(delta / 60_000)}m${Math.round((delta % 60_000) / 1000)}s`
}
