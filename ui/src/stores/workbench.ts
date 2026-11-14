/**
 * 工作台单例组合式 store（spec §9.3 / code-plan §5.3）。
 * 唯一选中事实源：selectedComponentId / focusedRepoType / selectedPath。
 * 树 / 详情 / 按钮 / 右栏全部读这里；Mock 与真实 Host 双路径在此切换。
 */
import { reactive } from 'vue'
import {
  api,
  hasHostBridge,
  HostError,
  isMethodUnavailable,
  onJobUpdated,
  toErrorMessage,
} from '../api'
import type { BatchArgs, CommitDto, HostInfoDto, JobDto, RepoActionArgs, RepoTreeDto, SnapshotDto, WorkspaceTreeDto } from '../api'
import * as mock from '../mocks/workbenchMock'
import { REPO_ACTIONS } from '../types/workbench'
import {
  BATCH_ACTIONS,
  HOST_ACTIONS_UNREADY_REASON,
  NO_REPO_REASON,
  actionErrorInfo,
  batchActionDef,
  batchActionLabel,
  emptyComponentVm,
  filterComponents,
  formatAgo,
  formatClock,
  hostAvailability,
  isDangerousFile,
  isSafeFile,
  jobIsDryRun,
  jobKindLabel,
  jobStateMeta,
  mapBackupRef,
  mapComponent,
  mapDetail,
  mapHostActions,
  mapJob,
  mapOperationLogItem,
  mapTreeNodes,
  mapWorkspaceEntries,
  pickDefaultRepo,
  repoOf,
  shortSha,
  sortComponents,
  sortTreeNodes,
} from '../viewmodel'
import type {
  ActionAvailability,
  ActionDialogPayload,
  ActionDialogRefsStatus,
  ActionDialogRefVm,
  ActionDialogVm,
  AuditFiltersVm,
  BackupRefVm,
  BatchDialogVm,
  BatchKind,
  CommitVm,
  ComponentDetailState,
  ComponentVm,
  ConfirmBarVm,
  ConfirmVm,
  DrawerKind,
  FilterState,
  FolderTreeState,
  HostActionAvailability,
  HostActionsSource,
  HostActionsStatus,
  InfoTab,
  JobVm,
  ModalKind,
  OfflineStateVm,
  OperationLogItemVm,
  OperationVm,
  RepoAction,
  RepoDetailVm,
  RepoTreePanelState,
  RepoType,
  RepoVm,
  TasksSegment,
  ToastVm,
  TreeNodeVm,
  ViewMode,
  WorkspaceNodeVm,
} from '../types/workbench'
import { REPO_TYPES } from '../types/workbench'

const VIEW_STORAGE_KEY = 'ui.v3.view'

interface NotificationVm {
  id: number
  type: string
  level: 'info' | 'warn' | 'err'
  title: string
  detail: string
  at: string
  read: boolean
}

interface ContextMenuState {
  x: number
  y: number
  items: { key: string; label: string; title?: string; danger?: boolean; disabled?: boolean; separator?: boolean }[]
}

interface WorkbenchState {
  booted: boolean
  booting: boolean
  refreshing: boolean
  hostLinked: boolean
  mockFallback: boolean
  source: string
  owner: string
  giteaBaseUrl: string
  workspaceRoot: string
  generatedAt: string
  snapshotError: string
  snapshotEmpty: boolean
  hostFailed: boolean
  fallbackReason: string
  counts: { components: number; repos: number; missingRepos: number }
  freshness: { localAt: string | null; remoteAt: string | null; localStale: boolean; remoteStale: boolean }
  /** SnapshotDto.capabilities（HostInfo 缺失时的能力兜底）。 */
  capabilities: { repoActions: boolean | null }
  /**
   * Host 单源矩阵缓存（AC-UI-08）：按 repoType 存 5 键可用性，
   * 数据来自 Host `getComponentDetail.repos[].actions`；Mock 模式由 mocks 的测试矩阵填充。
   */
  hostActions: Partial<Record<RepoType, Record<RepoAction, HostActionAvailability>>>
  /** Host actions 读取状态；非 ready 时五键禁用并提示「正在读取 Host 能力」。 */
  hostActionsStatus: HostActionsStatus
  /** Host actions 数据来源（host / mock）。 */
  hostActionsSource: HostActionsSource
  /** Host actions 读取失败原因（未就绪提示的诊断信息）。 */
  hostActionsError: string
  /** 当前 hostActions 所属组件（切换/刷新后重新加载）。 */
  hostActionsComponentId: string
  components: ComponentVm[]
  search: string
  sortBy: 'severity' | 'id' | 'activity'
  filter: FilterState
  selectedComponentId: string
  focusedRepoType: RepoType
  selectedPath: string | null
  view: ViewMode
  selectMode: boolean
  checkedIds: string[]
  trees: Record<RepoType, RepoTreePanelState>
  detail: RepoDetailVm | null
  detailStatus: 'idle' | 'loading' | 'ready' | 'error'
  detailError: string
  commits: { items: CommitVm[]; total: number; loading: boolean; error: string; loaded: boolean }
  tab: InfoTab
  folder: FolderTreeState
  folderSelected: WorkspaceNodeVm | null
  action: { pending: RepoAction | null; lastOp: OperationVm }
  notifications: NotificationVm[]
  ciQueue: number
  taskCount: number
  // ── M2b 任务中心 ──
  jobs: JobVm[]
  jobsStatus: 'idle' | 'loading' | 'ready' | 'error'
  /** 作业数据来源：Host 作业 API / 内置 Mock 演示。 */
  jobsSource: 'host' | 'mock'
  jobsError: string
  jobsLoading: boolean
  jobsAt: number
  /** 任务中心当前高亮作业（提交后自动定位）。 */
  currentJobId: string
  tasksSegment: TasksSegment
  /** 未查看的新进行中作业数（不在「进行中」分段时出现；切到进行中/关闭抽屉清零）。 */
  tasksUnreadActive: number
  expandedJobIds: string[]
  /** 批量提交进行中（防重复提交；不锁定单仓五键）。 */
  batchPending: boolean
  // ── M2b-2 操作审计（Host operation_log） ──
  audit: {
    status: 'idle' | 'loading' | 'ready' | 'error' | 'unavailable'
    source: HostActionsSource
    items: OperationLogItemVm[]
    total: number
    error: string
    at: number
    filters: AuditFiltersVm
    limit: number
    offset: number
    loading: boolean
    exporting: 'csv' | 'json' | null
    lastExport: { path: string; format: 'csv' | 'json'; count: number } | null
    exportError: string
  }
  // ── M2b-2 备份 ref 恢复 ──
  backupRefs: {
    status: 'idle' | 'loading' | 'ready' | 'error' | 'unavailable'
    componentId: string
    repoType: string
    refs: BackupRefVm[]
    error: string
    source: HostActionsSource
    at: number
  }
  backupRestore: { pending: string | null; error: string }
  /** M2b-2 离线判定（source=local 且远程错误/离线标记时 offline=true；不伪造在线）。 */
  remoteOffline: OfflineStateVm
  online: boolean
  ignoreIgnored: boolean
  compact: boolean
  theme: 'system' | 'light' | 'dark'
  overlays: {
    drawer: DrawerKind | null
    /** 组件详情抽屉：非 null 时为当前展示的组件 ID。 */
    componentDrawer: string | null
    modal: ModalKind | null
    filterOpen: boolean
    confirm: ConfirmVm | null
    /** F 区按钮上方的内联确认条（COMMIT/PUSH 唯一写入口，FR-ACT-04）。 */
    confirmBar: ConfirmBarVm | null
    /** M2 五键动作对话框（COMMIT 输入 / CHKOUT 选择 / 通用确认）。 */
    actionDialog: ActionDialogVm | null
    /** M2b 批量参数对话框（CREATE/TAG 输入 branch/tag；CLONE 预览缺失仓）。 */
    batchDialog: BatchDialogVm | null
    contextMenu: ContextMenuState | null
  }
  componentDetail: ComponentDetailState
  toasts: ToastVm[]
  hostInfo: HostInfoDto | null
  now: number
  menuOpen: string | null
}

function emptyTree(repoType: RepoType): RepoTreePanelState {
  return {
    repoType,
    status: 'idle',
    entries: [],
    byPath: new Map<string, TreeNodeVm>(),
    expanded: new Set<string>(),
    loadingPaths: new Set<string>(),
    loadedPaths: new Set<string>(),
    error: '',
    errorCode: '',
    truncated: false,
    totalEntries: 0,
    scrollTop: 0,
    fallback: false,
    at: 0,
  }
}

function emptyFolder(): FolderTreeState {
  return {
    status: 'idle',
    root: '',
    entries: [],
    byPath: new Map<string, WorkspaceNodeVm>(),
    expanded: new Set<string>(),
    loadingPaths: new Set<string>(),
    error: '',
    errorCode: '',
    truncated: false,
    totalEntries: 0,
    fallback: false,
    at: 0,
  }
}

function emptyComponentDetail(): ComponentDetailState {
  return { componentId: '', status: 'idle', component: null, source: 'snapshot', error: '' }
}

function readStoredView(): ViewMode {
  try {
    const value = localStorage.getItem(VIEW_STORAGE_KEY)
    return value === 'folder' ? 'folder' : 'workbench'
  } catch {
    return 'workbench'
  }
}

let toastSeq = 0
let notifySeq = 0
let epoch = 0
let snapshotRetryIndex = 0
let snapshotRetryTimer: ReturnType<typeof setTimeout> | undefined
/** CHKOUT refs 加载序号：关闭/重开对话框后丢弃过期响应（避免旧仓 refs 串台）。 */
let checkoutRefsSeq = 0
/** Host actions 加载序号：组件切换后丢弃过期响应（避免旧组件矩阵串台）。 */
let hostActionsSeq = 0
/** 备份 ref 加载序号：抽屉内切组件/切仓后丢弃过期响应（避免旧组件的 ref 串台）。 */
let backupRefsSeq = 0
/** 任务中心轮询定时器（Mock 推进作业 / Host 事件兜底轮询）。 */
let jobsClockTimer: ReturnType<typeof setInterval> | undefined
/** Job 事件订阅取消函数（真实 Host 推送）。 */
let jobsUnsubscribe: (() => void) | undefined
/** 作业终态后的快照回写（≤2s，合并多次终态事件）。 */
let jobSnapshotTimer: ReturnType<typeof setTimeout> | undefined
/**
 * dryRun 预览作业 id（P1-1）：提交时 args.dryRun=true 的作业在当前会话内直接标记；
 * 历史/重启后由 viewmodel.jobIsDryRun 的「全 SKIPPED + DRY_RUN 消息」识别兜底。
 */
const dryRunJobIds = new Set<string>()

const state = reactive<WorkbenchState>({
  booted: false,
  booting: false,
  refreshing: false,
  hostLinked: false,
  mockFallback: false,
  source: 'mock',
  owner: '',
  giteaBaseUrl: '',
  workspaceRoot: mock.MOCK_WORKSPACE_ROOT,
  generatedAt: '',
  snapshotError: '',
  snapshotEmpty: false,
  hostFailed: false,
  fallbackReason: '',
  counts: { components: 0, repos: 0, missingRepos: 0 },
  freshness: { localAt: null, remoteAt: null, localStale: false, remoteStale: false },
  capabilities: { repoActions: null },
  hostActions: {},
  hostActionsStatus: 'idle',
  hostActionsSource: 'host',
  hostActionsError: '',
  hostActionsComponentId: '',
  components: [],
  search: '',
  sortBy: 'severity',
  filter: { states: [], repoType: null, dirtyOnly: false, unpushedOnly: false },
  selectedComponentId: '',
  focusedRepoType: 'IMPL',
  selectedPath: null,
  view: readStoredView(),
  selectMode: false,
  checkedIds: [],
  trees: {
    DESIGN: emptyTree('DESIGN'),
    IMPL: emptyTree('IMPL'),
    MIL: emptyTree('MIL'),
    SIL: emptyTree('SIL'),
  },
  detail: null,
  detailStatus: 'idle',
  detailError: '',
  commits: { items: [], total: 0, loading: false, error: '', loaded: false },
  tab: 'overview',
  folder: emptyFolder(),
  folderSelected: null,
  action: {
    pending: null,
    lastOp: {
      action: '—',
      componentId: '',
      repoType: '',
      scope: '',
      state: 'idle',
      at: new Date().toISOString(),
    },
  },
  notifications: [
    {
      id: 1,
      type: '状态变化',
      level: 'warn',
      title: 'CF123A_ShortName 落后远程 2 个提交',
      detail: '3 个仓 BEHIND，建议执行同步（Mock）',
      at: new Date(Date.now() - 120_000).toISOString(),
      read: false,
    },
    {
      id: 2,
      type: 'Token 过期',
      level: 'err',
      title: 'Gitea PAT 将在 3 天后过期',
      detail: '请到 设置 → 认证 更新 Token（Mock）',
      at: new Date(Date.now() - 3_600_000).toISOString(),
      read: false,
    },
    {
      id: 3,
      type: '远程同步',
      level: 'info',
      title: '组织 MBD_Org 元数据同步完成',
      detail: '覆盖率 98.7%，部分仓失败可重试（Mock）',
      at: new Date(Date.now() - 10_800_000).toISOString(),
      read: true,
    },
  ],
  ciQueue: 0,
  taskCount: 0,
  jobs: [],
  jobsStatus: 'idle',
  jobsSource: 'mock',
  jobsError: '',
  jobsLoading: false,
  jobsAt: 0,
  currentJobId: '',
  tasksSegment: 'active',
  tasksUnreadActive: 0,
  expandedJobIds: [],
  batchPending: false,
  audit: {
    status: 'idle',
    source: 'host',
    items: [],
    total: 0,
    error: '',
    at: 0,
    filters: { action: '', result: '' },
    limit: 20,
    offset: 0,
    loading: false,
    exporting: null,
    lastExport: null,
    exportError: '',
  },
  backupRefs: {
    status: 'idle',
    componentId: '',
    repoType: '',
    refs: [],
    error: '',
    source: 'host',
    at: 0,
  },
  backupRestore: { pending: null, error: '' },
  remoteOffline: { offline: false, reason: '' },
  online: true,
  ignoreIgnored: false,
  compact: false,
  theme: 'system',
  overlays: {
    drawer: null,
    componentDrawer: null,
    modal: null,
    filterOpen: false,
    confirm: null,
    confirmBar: null,
    actionDialog: null,
    batchDialog: null,
    contextMenu: null,
  },
  componentDetail: emptyComponentDetail(),
  toasts: [],
  hostInfo: null,
  now: Date.now(),
  menuOpen: null,
})

// ───────────────────────────── 派生查询 ─────────────────────────────

function selectedComponent(): ComponentVm {
  return state.components.find((component) => component.id === state.selectedComponentId) ?? emptyComponentVm()
}

function focusedRepo(): RepoVm | null {
  const component = selectedComponent()
  if (!component.id) return null
  return repoOf(component, state.focusedRepoType)
}

function visibleComponents(): ComponentVm[] {
  return sortComponents(filterComponents(state.components, state.search, state.filter), state.sortBy)
}

function blockedAvailability(
  reasonFor: (action: RepoAction) => string,
  code = 'E_ACTION_NOT_ALLOWED',
  alt = '按 Host 返回的能力先修复仓库状态后重试',
): Record<RepoAction, ActionAvailability> {
  const result = {} as Record<RepoAction, ActionAvailability>
  for (const action of REPO_ACTIONS) result[action] = { enabled: false, reason: reasonFor(action), confirm: false, code, alt }
  return result
}

/**
 * 五键可用性（AC-UI-08 单源矩阵）：
 * 1) 未选中仓 / 有操作进行中 → 统一禁用理由；
 * 2) **唯一可用性来源** = Host `getComponentDetail.repos[].actions`（按 repoType 缓存在 state.hostActions）；
 * 3) Host actions 未就绪 → 五键禁用并提示「正在读取 Host 能力」；
 * 4) 仅在 Host actions 未就绪时保留 M1 只读能力门禁（features.repoActions=false 的旧 Host）。
 */
function baseAvailability(): Record<RepoAction, ActionAvailability> {
  const repo = focusedRepo()
  if (!repo) {
    return blockedAvailability(() => NO_REPO_REASON, 'E_ACTION_NOT_ALLOWED', '先在文件树/卡片中选择仓库')
  }
  if (state.action.pending) {
    return blockedAvailability(() => '该仓有操作进行中', 'E_BUSY', '等待当前操作完成后重试（同仓串行）')
  }
  const hostActions = state.hostActions[state.focusedRepoType]
  if (state.hostActionsStatus === 'ready' && hostActions) {
    return hostAvailability(hostActions, repo)
  }
  // Host actions 未就绪期间，旧 Host（features.repoActions=false）仍按 M1 只读给出理由。
  if (state.hostLinked) {
    const infoFlag = state.hostInfo ? state.hostInfo.features?.repoActions : undefined
    const snapshotFlag = state.capabilities.repoActions
    if (infoFlag !== undefined || snapshotFlag !== null) {
      const allowed = infoFlag === true || (infoFlag === undefined && snapshotFlag === true)
      if (!allowed) {
        return blockedAvailability(
          (action) => `M1 只读：${action} 需 Host 开启 features.repoActions 或 snapshot.capabilities.repoActions`,
          'E_NOT_IMPLEMENTED',
          '在支持写操作的 Host（M2a）中运行',
        )
      }
    }
  }
  const detail =
    state.hostActionsStatus === 'error' && state.hostActionsError
      ? `（读取失败：${state.hostActionsError}）`
      : ''
  return blockedAvailability(
    () => `${HOST_ACTIONS_UNREADY_REASON}${detail}`,
    'E_BUSY',
    '等待 Host 能力读取完成后重试',
  )
}

/**
 * M2b-2 离线降级（轻量）：快照 source=local 且存在远程错误/离线标记时，
 * FETCH 为网络操作，必须禁用并在 tooltip 说明“离线/远程不可达”；其余五键仍由 Host 矩阵单源决定。
 * 不伪造在线：离线判定来自快照/远程错误标记，不是本地假设。
 */
function availability(): Record<RepoAction, ActionAvailability> {
  const result = baseAvailability()
  const repo = focusedRepo()
  // FR-REPO-05：真正 unborn 的 EMPTY 仓没有可推送提交；矩阵虽允许首推，UI 必须禁用并给出原因。
  if (repo && repo.state === 'EMPTY' && !repo.headSha) {
    result.PUSH = {
      enabled: false,
      reason: '空仓无提交：请先 COMMIT 生成首个提交后再 PUSH（首推将建立 upstream）',
      confirm: false,
      code: 'E_REF_NOT_FOUND',
      alt: '先 COMMIT（含 message）生成首个提交，再 PUSH 发布分支',
    }
  }
  if (!repo || state.action.pending || !state.remoteOffline.offline) return result
  result.FETCH = {
    enabled: false,
    reason: `离线/远程不可达${state.remoteOffline.reason ? `：${state.remoteOffline.reason}` : ''}`,
    confirm: result.FETCH.confirm,
    code: 'E_OFFLINE',
    alt: '恢复网络 / Gitea 连接后点击刷新；离线期间不能执行 FETCH',
  }
  return result
}

function unread(): number {
  return state.notifications.filter((item) => !item.read).length
}

/**
 * FR-OPS-05 状态栏模板（M2a 出口）：
 * `最近一次操作：<ACTION> <ComponentId>/<RepoType> — 成功|失败(E_*) · HH:mm`
 * 进行中输出「进行中」，从未操作输出「无」。
 */
function lastOpText(): string {
  const op = state.action.lastOp
  if (op.state === 'idle') return '最近一次操作：无'
  const clock = formatClock(op.at)
  const minute = /^\d{2}:\d{2}/.test(clock) ? clock.slice(0, 5) : clock
  const componentId = op.componentId || '—'
  const repoType = op.repoType || op.scope || '—'
  const result =
    op.state === 'running' ? '进行中' : op.state === 'success' ? '成功' : `失败(${op.code ?? 'E_UNKNOWN'})`
  return `最近一次操作：${op.action} ${componentId}/${repoType} — ${result} · ${minute}`
}

function freshnessText(): string {
  const fresh = state.freshness
  if (!fresh.localAt && !fresh.remoteAt) return '数据新鲜度：—'
  const local = fresh.localAt ? `本地 ${formatAgo(fresh.localAt, state.now)}` : '本地 —'
  const remote = fresh.remoteAt ? `远程 ${formatAgo(fresh.remoteAt, state.now)}` : '远程 —'
  return `${local} · ${remote}`
}

function sourceLabel(): string {
  if (!state.hostLinked || state.mockFallback) return 'Mock 数据'
  switch (state.source) {
    case 'mock':
      return 'Mock 数据'
    case 'local':
      return '本地扫描'
    case 'local+gitea':
      return '本地扫描 + Gitea'
    default:
      return `Host · ${state.source || '未知数据源'}`
  }
}

function sourceTitle(): string {
  const lines = [`数据源：${state.source || '未声明'}`]
  if (state.workspaceRoot) lines.push(`工作区：${state.workspaceRoot}`)
  if (state.generatedAt) lines.push(`快照时间：${state.generatedAt}`)
  if (state.snapshotError) lines.push(`错误：${state.snapshotError}`)
  if (state.fallbackReason) lines.push(state.fallbackReason)
  return lines.join('\n')
}

function isMockMode(): boolean {
  return !hasHostBridge() || state.mockFallback
}

// ───────────────────────────── Toast / 通知 / 浮层 ─────────────────────────────

function notify(text: string, tone: ToastVm['tone'] = 'info', ttl = 2400): void {
  const toast: ToastVm = { id: ++toastSeq, text, tone, ttl }
  state.toasts.push(toast)
  if (state.toasts.length > 3) state.toasts.splice(0, state.toasts.length - 3)
  window.setTimeout(() => dismissToast(toast.id), ttl)
}

/** 带操作按钮的 Toast（M2b-2：审计导出后「打开所在目录」）；按钮点击后先关闭再执行回调。 */
function notifyAction(
  text: string,
  tone: ToastVm['tone'],
  ttl: number,
  actionLabel: string,
  onAction: () => void,
): void {
  const toast: ToastVm = { id: ++toastSeq, text, tone, ttl, actionLabel, onAction }
  state.toasts.push(toast)
  if (state.toasts.length > 3) state.toasts.splice(0, state.toasts.length - 3)
  window.setTimeout(() => dismissToast(toast.id), ttl)
}

function runToastAction(id: number): void {
  const toast = state.toasts.find((item) => item.id === id)
  if (!toast) return
  const action = toast.onAction
  dismissToast(id)
  action?.()
}

function dismissToast(id: number): void {
  const at = state.toasts.findIndex((toast) => toast.id === id)
  if (at >= 0) state.toasts.splice(at, 1)
}

function pushNotification(title: string, detail: string, level: NotificationVm['level'] = 'warn'): void {
  state.notifications.unshift({
    id: ++notifySeq,
    type: level === 'err' ? '操作失败' : '状态变化',
    level,
    title,
    detail,
    at: new Date().toISOString(),
    read: false,
  })
}

function markAllRead(): void {
  state.notifications.forEach((item) => {
    item.read = true
  })
}

function openDrawer(kind: DrawerKind): void {
  state.overlays.drawer = kind
  // 任务中心：打开即拉取一次列表；随后由 JOB_UPDATED 事件 + 轮询兜底保持最新。
  if (kind === 'tasks') void ensureJobsLoaded(true)
}

function closeDrawer(): void {
  state.overlays.drawer = null
}

function openModal(kind: ModalKind): void {
  state.overlays.modal = kind
  if (kind === 'ci' && state.hostLinked && !state.mockFallback) void refreshCi()
  // 「日志与诊断」内嵌操作审计：打开时按当前过滤条件重新拉取（Host 未实现时显示不可用态，不伪造数据）。
  if (kind === 'logs') void refreshOperationLog(true)
}

function closeModal(): void {
  state.overlays.modal = null
}

function openConfirm(confirm: ConfirmVm): void {
  state.overlays.confirm = confirm
}

function closeConfirm(): void {
  state.overlays.confirm = null
}

// ───────────────────────────── F 区确认条（FR-ACT-04 / AC-UI-10a/10b） ─────────────────────────────

/**
 * 打开 F 区按钮上方的 ≤48px 内联确认条。COMMIT/PUSH 的唯一写入口：
 * 只有点「确认」（acceptConfirmBar）才会调用 runRepoAction；取消/Esc/切换选中均为 0 写。
 */
function openConfirmBar(bar: ConfirmBarVm): void {
  closeContextMenu()
  state.overlays.confirmBar = bar
}

/** 静默关闭（切换仓库/选中、刷新等上下文失效时使用，不触发 onCancel 回执）。 */
function closeConfirmBar(): void {
  state.overlays.confirmBar = null
}

/** 用户点「取消」/Esc：关闭并执行取消回调（0 写）。 */
function cancelConfirmBar(): void {
  const bar = state.overlays.confirmBar
  state.overlays.confirmBar = null
  bar?.onCancel?.()
}

/** 用户点「确认」：先移除确认条，再执行写操作（其余路径不得绕过本函数）。 */
function acceptConfirmBar(): void {
  const bar = state.overlays.confirmBar
  if (!bar) return
  state.overlays.confirmBar = null
  bar.onAccept()
}

// ───────────────────────────── M2 动作对话框 / 组件详情抽屉 ─────────────────────────────

function openActionDialog(dialog: ActionDialogVm): void {
  closeConfirm()
  closeContextMenu()
  state.overlays.actionDialog = dialog
}

function closeActionDialog(): void {
  // 作废进行中的 CHKOUT refs 加载，避免关闭后旧响应回填。
  checkoutRefsSeq += 1
  state.overlays.actionDialog = null
}

/** 对话框提交后先关闭再执行，避免执行期间浮层遮挡状态。 */
function submitActionDialog(payload: ActionDialogPayload): void {
  const dialog = state.overlays.actionDialog
  if (!dialog) return
  state.overlays.actionDialog = null
  dialog.submit(payload)
}

function openComponentDrawer(componentId: string): void {
  if (!componentId) return
  closeContextMenu()
  closeConfirm()
  state.overlays.componentDrawer = componentId
  void loadComponentDetail(componentId)
  // M2b-2：抽屉内「备份 ref」区按当前组件默认聚焦仓加载（用户可在区内切换 D/I/M/S）。
  const component = state.components.find((item) => item.id === componentId)
  const repoType = component ? pickDefaultRepo(component) : state.focusedRepoType
  void loadBackupRefs(componentId, repoType)
}

function closeComponentDrawer(): void {
  state.overlays.componentDrawer = null
}

/**
 * 组件详情：优先 Host getComponentDetail（M1 已实现），任意失败回退当前快照组件；
 * 两种来源都在抽屉内标注，避免把回退数据伪装成 Host 实时详情。
 */
async function loadComponentDetail(componentId: string): Promise<void> {
  state.componentDetail = {
    componentId,
    status: 'loading',
    component: null,
    source: 'snapshot',
    error: '',
  }
  const applyFallback = (note: string): void => {
    if (state.overlays.componentDrawer !== componentId) return
    const component = state.components.find((item) => item.id === componentId) ?? null
    state.componentDetail = {
      componentId,
      status: component ? 'ready' : 'error',
      component,
      source: 'snapshot',
      error: component ? note : note || '快照中未找到该组件',
    }
  }
  if (isMockMode()) {
    applyFallback('Mock 模式：展示快照数据')
    return
  }
  try {
    const dto = await api.getComponentDetail({ componentId })
    if (state.overlays.componentDrawer !== componentId) return
    state.componentDetail = {
      componentId,
      status: 'ready',
      component: mapComponent(dto),
      source: 'host',
      error: '',
    }
  } catch (error) {
    applyFallback(`组件详情接口不可用（${toErrorMessage(error)}），已回退快照`)
  }
}

/**
 * AC-UI-08 单源矩阵加载：组件选择/切换/刷新后读取 Host
 * `getComponentDetail({componentId})` 的 `repos[].actions`，按 repoType 缓存到 `state.hostActions`。
 * - Mock/浏览器模式：由 `mocks/` 的测试矩阵生成（不进入运行逻辑判定）；
 * - Host 未就绪/读取失败：`hostActionsStatus` 非 ready，五键禁用并提示「正在读取 Host 能力」。
 */
async function loadHostActions(componentId = state.selectedComponentId, force = false): Promise<void> {
  if (!componentId) {
    hostActionsSeq += 1
    state.hostActions = {}
    state.hostActionsStatus = 'idle'
    state.hostActionsSource = 'host'
    state.hostActionsError = ''
    state.hostActionsComponentId = ''
    return
  }
  const sameTarget = state.hostActionsComponentId === componentId
  if (!force && sameTarget && (state.hostActionsStatus === 'loading' || state.hostActionsStatus === 'ready')) {
    return
  }
  const token = ++hostActionsSeq
  state.hostActionsComponentId = componentId
  state.hostActions = {}
  state.hostActionsStatus = 'loading'
  state.hostActionsError = ''

  if (isMockMode()) {
    const byType: Partial<Record<RepoType, Record<RepoAction, HostActionAvailability>>> = {}
    for (const type of REPO_TYPES) byType[type] = mock.mockHostActions(componentId, type)
    if (token !== hostActionsSeq) return
    state.hostActions = byType
    state.hostActionsSource = 'mock'
    state.hostActionsStatus = 'ready'
    return
  }

  try {
    const dto = await api.getComponentDetail({ componentId })
    if (token !== hostActionsSeq || state.selectedComponentId !== componentId) return
    const byType: Partial<Record<RepoType, Record<RepoAction, HostActionAvailability>>> = {}
    let readyRepos = 0
    for (const repo of dto.repos) {
      const type = (repo.repoType ?? '').toUpperCase()
      if (!(REPO_TYPES as readonly string[]).includes(type)) continue
      if (!Array.isArray(repo.actions) || repo.actions.length === 0) continue
      byType[type as RepoType] = mapHostActions(repo.actions)
      readyRepos += 1
    }
    state.hostActions = byType
    state.hostActionsSource = 'host'
    state.hostActionsStatus = readyRepos > 0 ? 'ready' : 'error'
    state.hostActionsError =
      readyRepos > 0 ? '' : 'Host getComponentDetail 未返回 repos[].actions（Host 未就绪）'
  } catch (error) {
    if (token !== hostActionsSeq || state.selectedComponentId !== componentId) return
    state.hostActions = {}
    state.hostActionsSource = 'host'
    state.hostActionsStatus = 'error'
    state.hostActionsError = toErrorMessage(error)
  }
}

function openContextMenu(x: number, y: number, items: ContextMenuState['items']): void {
  state.overlays.contextMenu = { x, y, items }
}

function closeContextMenu(): void {
  state.overlays.contextMenu = null
}

function openFilter(): void {
  state.overlays.filterOpen = true
}

function closeFilter(): void {
  state.overlays.filterOpen = false
}

function toggleTheme(): void {
  state.theme = state.theme === 'dark' ? 'light' : state.theme === 'light' ? 'system' : 'dark'
  const label = state.theme === 'dark' ? '深色' : state.theme === 'light' ? '浅色' : '跟随系统'
  notify(`主题：${label}`, 'info')
}

// ───────────────────────────── 快照 / 初始化 ─────────────────────────────

/** 离线判定用的错误码前缀/flag 关键词（Host 远程阶段失败时写入；不匹配路径/仓库名）。 */
const REMOTE_OFFLINE_ERROR = /^E_(OFFLINE|NETWORK|REMOTE|GITEA)/i
const REMOTE_OFFLINE_FLAG = /offline|remote_unreach|remote_fail|remote_error|remote_gone|remote_stale/i

/**
 * M2b-2 离线判定（轻量）：
 * - 只有 `source=local`（远程阶段不可用/未就绪）才进入候选；
 * - 远程错误/离线标记来源：Host 显式 `offline/remoteError`、snapshot.error、远程数据过期（remoteStale）、
 *   逐仓 E_OFFLINE/E_NETWORK/E_REMOTE* 或 remote_* flag；
 * - 命中即在顶部显示「离线/远程不可达」并禁用 FETCH；不命中时绝不显示为在线/离线任何一方之外的状态。
 */
function detectRemoteOffline(snapshot: SnapshotDto): OfflineStateVm {
  if (snapshot.source !== 'local') return { offline: false, reason: '' }
  const markers: string[] = []
  if (snapshot.offline === true) markers.push('Host 标记远程离线')
  if (snapshot.remoteError) markers.push(snapshot.remoteError)
  if (snapshot.error) markers.push(snapshot.error)
  // 仅 source=local + remoteStale 不能判定离线（未配置 Gitea 的正常本地模式）；必须有显式错误/标记。
  for (const component of snapshot.components) {
    for (const repo of component.repos) {
      if (repo.errorCode && REMOTE_OFFLINE_ERROR.test(repo.errorCode)) {
        markers.push(`${component.componentId}/${repo.repoType}：${repo.errorCode}`)
        break
      }
      const flag = repo.flags.find((item) => REMOTE_OFFLINE_FLAG.test(item))
      if (flag) {
        markers.push(`${component.componentId}/${repo.repoType}：${flag}`)
        break
      }
    }
    if (markers.length > 0) break
  }
  return { offline: markers.length > 0, reason: markers[0] ?? '' }
}

/** 仅应用快照数据（不动选中/不触发 loadActive）；供动作后定向刷新复用。 */
function applySnapshotHeader(snapshot: SnapshotDto, options: { mockFallback: boolean; reason?: string }): void {
  state.hostLinked = !options.mockFallback && hasHostBridge()
  state.mockFallback = options.mockFallback
  state.source = snapshot.source
  state.owner = snapshot.owner ?? state.owner
  state.giteaBaseUrl = snapshot.giteaBaseUrl ?? state.giteaBaseUrl
  state.workspaceRoot = snapshot.workspaceRoot ?? ''
  state.generatedAt = snapshot.generatedAt
  state.snapshotError = snapshot.error ?? ''
  state.fallbackReason = options.reason ?? ''
  const components = snapshot.components.map(mapComponent)
  state.snapshotEmpty = components.length === 0
  if (components.length > 0) {
    state.components = components
  } else if (state.components.length === 0) {
    state.components = [mapComponent(mock.mockSnapshot().components[0])]
  }
  const repos = state.components.reduce((sum, component) => sum + component.repos.length, 0)
  state.counts = {
    components: snapshot.counts?.components ?? state.components.length,
    repos: snapshot.counts?.repos ?? repos,
    missingRepos: snapshot.counts?.missingRepos ?? 0,
  }
  state.freshness = {
    localAt: snapshot.freshness?.localScanAt ?? snapshot.generatedAt,
    remoteAt: snapshot.freshness?.remoteFetchAt ?? snapshot.generatedAt,
    localStale: Boolean(snapshot.freshness?.localStale),
    remoteStale: Boolean(snapshot.freshness?.remoteStale),
  }
  state.capabilities = {
    repoActions: snapshot.capabilities?.repoActions ?? null,
  }
  state.remoteOffline = detectRemoteOffline(snapshot)
  state.online = !state.remoteOffline.offline
  state.snapshotEmpty = components.length === 0
  state.hostFailed = false
  snapshotRetryIndex = 0
  if (snapshotRetryTimer !== undefined) {
    clearTimeout(snapshotRetryTimer)
    snapshotRetryTimer = undefined
  }
}

function applySnapshot(snapshot: SnapshotDto, options: { mockFallback: boolean; reason?: string }): void {
  applySnapshotHeader(snapshot, options)

  const currentId = state.components.some((component) => component.id === state.selectedComponentId)
    ? state.selectedComponentId
    : state.components[0]?.id ?? ''
  if (currentId && currentId !== state.selectedComponentId) {
    void selectComponent(currentId)
  } else if (currentId) {
    const component = selectedComponent()
    if (!repoOf(component, state.focusedRepoType)) state.focusedRepoType = pickDefaultRepo(component)
    void loadActive()
  }
}

function applyMockSnapshot(reason: string): void {
  const snapshot = mock.mockSnapshot()
  applySnapshot(
    {
      source: snapshot.source,
      workspaceRoot: snapshot.workspaceRoot,
      error: snapshot.error,
      generatedAt: snapshot.generatedAt,
      components: snapshot.components,
      counts: snapshot.counts ?? null,
      freshness: snapshot.freshness ?? null,
    },
    { mockFallback: true, reason },
  )
}

async function loadInitialSnapshot(): Promise<void> {
  if (state.booting) return
  state.booting = true
  try {
    const snapshot = await api.getSnapshot()
    applySnapshot(snapshot, { mockFallback: snapshot.source === 'mock' })
    if (snapshot.source === 'mock') {
      notify('Host 未配置工作区（source=mock），展示内置 Mock 数据', 'warning', 4000)
    } else {
      notify(`已连接 Host · ${sourceLabel()}`, 'success')
    }
  } catch (error) {
    const reason = toErrorMessage(error)
    state.hostFailed = true
    applyMockSnapshot(`读取 Host 失败：${reason}（当前展示内置 Mock 数据）`)
    if (snapshotRetryIndex < 2) {
      const delay = snapshotRetryIndex === 0 ? 5_000 : 15_000
      snapshotRetryIndex += 1
      notify(`Host 快照尚未就绪（${reason}），${Math.round(delay / 1000)}s 后自动重试`, 'warning', 5000)
      if (snapshotRetryTimer !== undefined) clearTimeout(snapshotRetryTimer)
      snapshotRetryTimer = setTimeout(() => {
        snapshotRetryTimer = undefined
        void loadInitialSnapshot()
      }, delay)
    } else {
      notify(`读取 Host 快照失败：${reason}，已回退 Mock；可点击刷新重试`, 'error', 6000)
    }
  } finally {
    state.booting = false
    state.booted = true
  }
}

async function init(): Promise<void> {
  if (state.booted || state.booting) return
  if (!hasHostBridge()) {
    applyMockSnapshot('当前不在 WebView2 宿主中（浏览器 dev / CDP），展示内置 Mock 数据')
    state.booted = true
    state.booting = false
    await initJobs()
    return
  }
  notify('正在连接 Host 并扫描本地仓库…（首次可能 10–20s）')
  await loadInitialSnapshot()
  await initJobs()
}

async function refresh(): Promise<void> {
  if (state.refreshing || state.booting) return
  state.refreshing = true
  const keepId = state.selectedComponentId
  const keepRepo = state.focusedRepoType
  if (isMockMode()) {
    await delay(600)
    applyMockSnapshot('Mock：已重新生成快照')
    notify('已刷新（Mock 数据）', 'success')
    state.refreshing = false
    void refreshJobs(true)
    return
  }
  notify('正在重新扫描本地仓库…（大工作区约 10–20s）')
  try {
    await api.refresh()
    const snapshot = await api.getSnapshot()
    applySnapshot(snapshot, { mockFallback: snapshot.source === 'mock' })
    if (keepId) {
      state.selectedComponentId = keepId
      state.focusedRepoType = keepRepo
    }
    resetCaches()
    // 手动刷新：强制重新读取 Host 单源矩阵（Host 可能已随扫描更新）。
    void loadHostActions(state.selectedComponentId, true)
    await loadActive()
    notify(`已刷新 · ${sourceLabel()}`, 'success')
  } catch (error) {
    notify(`刷新失败：${toErrorMessage(error)}`, 'error', 6000)
  } finally {
    state.refreshing = false
  }
}

function resetCaches(): void {
  for (const type of REPO_TYPES) {
    state.trees[type] = emptyTree(type)
  }
  state.detail = null
  state.detailStatus = 'idle'
  state.commits = { items: [], total: 0, loading: false, error: '', loaded: false }
  state.folder = emptyFolder()
}

async function loadActive(): Promise<void> {
  state.selectedPath = null
  state.folderSelected = null
  state.tab = 'overview'
  // 组件选择/刷新后重新加载 Host 单源矩阵（失败不影响详情/文件树加载）。
  void loadHostActions(state.selectedComponentId)
  await Promise.all([loadDetail(), ...REPO_TYPES.map((type) => loadTree(type))])
  if (state.view === 'folder') void ensureFolderLoaded()
}

async function selectComponent(id: string): Promise<void> {
  if (!id || state.selectedComponentId === id) {
    if (id) await loadActive()
    return
  }
  // 选中变化后旧确认条的作用域已失效：静默作废（0 写）。
  if (state.overlays.confirmBar) closeConfirmBar()
  epoch += 1
  const currentEpoch = epoch
  state.selectedComponentId = id
  const component = selectedComponent()
  state.focusedRepoType = pickDefaultRepo(component)
  state.selectedPath = null
  state.folderSelected = null
  for (const type of REPO_TYPES) {
    state.trees[type] = emptyTree(type)
  }
  state.detail = null
  state.detailStatus = 'loading'
  state.commits = { items: [], total: 0, loading: false, error: '', loaded: false }
  state.tab = 'overview'
  if (currentEpoch !== epoch) return
  // 组件切换：重新加载 Host actions（旧矩阵立即作废，避免沿用上一组件能力）。
  void loadHostActions(id)
  await Promise.all([loadDetail(), ...REPO_TYPES.map((type) => loadTree(type))])
}

// ───────────────────────────── 四仓树 ─────────────────────────────

function registerTreeNodes(panel: RepoTreePanelState, nodes: TreeNodeVm[]): void {
  for (const node of nodes) {
    panel.byPath.set(node.path, node)
    if (node.children.length > 0) registerTreeNodes(panel, node.children)
  }
}

function applyTree(panel: RepoTreePanelState, dto: RepoTreeDto, fallback: boolean): void {
  panel.entries = sortTreeNodes(mapTreeNodes(dto.entries))
  panel.byPath = new Map<string, TreeNodeVm>()
  panel.expanded = new Set<string>()
  panel.loadedPaths = new Set<string>()
  panel.loadingPaths = new Set<string>()
  registerTreeNodes(panel, panel.entries)
  const treeError = dto.error ?? ''
  // Host 可在“部分目录不可读”时返回 error + entries：有数据时保留列表并用横幅提示；
  // 无数据时进入 error 空态（展示 errorCode），绝不伪装成“空目录”。
  panel.status = treeError && panel.entries.length === 0 ? 'error' : 'ready'
  panel.error = treeError
  panel.errorCode = treeError ? dto.errorCode ?? 'E_TREE_READ_FAILED' : ''
  panel.truncated = Boolean(dto.truncated)
  panel.totalEntries = dto.totalEntries ?? panel.entries.length
  panel.fallback = fallback
  panel.at = Date.now()
}

async function loadTree(repoType: RepoType, force = false): Promise<void> {
  const panel = state.trees[repoType]
  const component = selectedComponent()
  if (!component.id) return
  const repo = repoOf(component, repoType)
  if (!repo || repo.state === 'MISSING' || !repo.isLocal) {
    panel.status = 'unavailable'
    panel.entries = []
    panel.byPath = new Map()
    panel.truncated = false
    panel.totalEntries = 0
    panel.error = repo?.state === 'MISSING' ? '仓库未克隆（MISSING）' : '该仓不在当前快照中'
    panel.errorCode = repo?.state === 'MISSING' ? 'E_REPO_NOT_LOCAL' : 'E_NOT_FOUND'
    return
  }
  if (repo.state === 'INVALID') {
    panel.status = 'unavailable'
    panel.truncated = false
    panel.totalEntries = 0
    panel.error = '仓库绑定异常（INVALID）'
    panel.errorCode = 'E_BINDING_ERROR'
    return
  }
  if (panel.status === 'loading') return
  if (!force && panel.status === 'ready' && Date.now() - panel.at < 30_000) return
  panel.status = 'loading'
  panel.error = ''
  const currentEpoch = epoch
  try {
    let dto: RepoTreeDto
    let fallback = false
    if (isMockMode()) {
      dto = mock.mockRepoTree(component.id, repoType, '')
      fallback = false
    } else {
      try {
        dto = await api.getRepoTree({ componentId: component.id, repoType, path: '', depth: 1 })
      } catch (error) {
        if (!isMethodUnavailable(error)) throw error
        dto = mock.mockRepoTree(component.id, repoType, '')
        fallback = true
      }
    }
    if (currentEpoch !== epoch) return
    applyTree(panel, dto, fallback)
  } catch (error) {
    if (currentEpoch !== epoch) return
    panel.status = 'error'
    panel.error = error instanceof HostError ? error.message : toErrorMessage(error)
    panel.errorCode = error instanceof HostError ? error.code : 'E_IO_ERROR'
  }
}

async function toggleTreePath(repoType: RepoType, path: string): Promise<void> {
  const panel = state.trees[repoType]
  if (panel.expanded.has(path)) {
    panel.expanded.delete(path)
    return
  }
  const node = panel.byPath.get(path)
  if (!node || node.kind !== 'DIR' || !node.hasChildren) {
    panel.expanded.add(path)
    return
  }
  if (!node.loaded) {
    if (panel.loadingPaths.has(path)) return
    panel.loadingPaths.add(path)
    try {
      const component = selectedComponent()
      let dto: RepoTreeDto
      if (isMockMode()) {
        dto = mock.mockRepoTree(component.id, repoType, path)
      } else {
        try {
          dto = await api.getRepoTree({ componentId: component.id, repoType, path, depth: 1 })
        } catch (error) {
          if (!isMethodUnavailable(error)) throw error
          dto = mock.mockRepoTree(component.id, repoType, path)
          panel.fallback = true
        }
      }
      node.children = sortTreeNodes(mapTreeNodes(dto.entries))
      node.loaded = true
      registerTreeNodes(panel, node.children)
      panel.loadedPaths.add(path)
      panel.truncated = panel.truncated || Boolean(dto.truncated)
    } catch (error) {
      node.error = toErrorMessage(error)
      notify(`无法读取目录：${toErrorMessage(error)}`, 'error', 6000)
      return
    } finally {
      panel.loadingPaths.delete(path)
    }
  }
  panel.expanded.add(path)
}

function selectTreeNode(repoType: RepoType, path: string | null): void {
  const changedRepo = repoType !== state.focusedRepoType
  if (changedRepo && state.overlays.confirmBar) closeConfirmBar()
  state.focusedRepoType = repoType
  state.selectedPath = path
  if (changedRepo || path === null) void loadDetail()
}

function focusRepo(repoType: RepoType): void {
  if (state.focusedRepoType === repoType) return
  // 聚焦仓变化后旧确认条作用域失效：静默作废（0 写）。
  if (state.overlays.confirmBar) closeConfirmBar()
  state.focusedRepoType = repoType
  state.selectedPath = null
  void loadDetail()
}

function setTreeScroll(repoType: RepoType, scrollTop: number): void {
  state.trees[repoType].scrollTop = scrollTop
}

// ───────────────────────────── 详情 / 提交 ─────────────────────────────

async function loadDetail(): Promise<void> {
  const component = selectedComponent()
  const repo = focusedRepo()
  if (!component.id || !repo) {
    state.detail = null
    state.detailStatus = 'idle'
    return
  }
  state.detailStatus = 'loading'
  const currentEpoch = epoch
  const repoType = state.focusedRepoType
  try {
    let detail: RepoDetailVm
    if (isMockMode()) {
      const dto = mock.mockRepoDetail(component.id, repoType)
      detail = mapDetail(dto, repo.repoName)
    } else {
      try {
        const dto = await api.getRepoDetail({ componentId: component.id, repoType })
        detail = mapDetail(dto, repo.repoName)
      } catch (error) {
        if (!isMethodUnavailable(error)) throw error
        detail = mapDetail(mock.mockRepoDetail(component.id, repoType), repo.repoName)
      }
    }
    if (currentEpoch !== epoch) return
    state.detail = detail
    state.detailStatus = 'ready'
    state.detailError = ''
  } catch (error) {
    if (currentEpoch !== epoch) return
    state.detailStatus = 'error'
    state.detailError = toErrorMessage(error)
  }
}

function setTab(tab: InfoTab): void {
  state.tab = tab
  if (tab === 'commits' && !state.commits.loaded) void loadCommits(true)
}

async function loadCommits(reset = false): Promise<void> {
  const component = selectedComponent()
  const repo = focusedRepo()
  if (!component.id || !repo || state.commits.loading) return
  if (reset) state.commits = { items: [], total: 0, loading: true, error: '', loaded: false }
  else state.commits.loading = true
  try {
    let page: { total: number; items: CommitDto[] }
    if (isMockMode()) {
      page = mock.mockCommits(component.id, state.focusedRepoType, state.commits.items.length, 100)
    } else {
      try {
        page = await api.getCommits({
          componentId: component.id,
          repoType: state.focusedRepoType,
          skip: state.commits.items.length,
          take: 100,
        })
      } catch (error) {
        if (!isMethodUnavailable(error)) throw error
        page = mock.mockCommits(component.id, state.focusedRepoType, state.commits.items.length, 100)
      }
    }
    state.commits.total = page.total
    state.commits.items = state.commits.items.concat(
      page.items.map((item) => ({
        sha: item.sha,
        shortSha: item.shortSha ?? item.sha.slice(0, 7),
        message: item.message,
        author: item.author,
        at: item.at,
        refs: item.refs ?? [],
      })),
    )
    state.commits.loaded = true
    state.commits.error = ''
  } catch (error) {
    state.commits.error = toErrorMessage(error)
  } finally {
    state.commits.loading = false
  }
}

async function refreshCi(): Promise<void> {
  try {
    state.hostInfo = await api.getHostInfo()
  } catch {
    state.hostInfo = mock.mockHostInfo()
  }
}

// ───────────────────────────── 打开文件 / 单仓动作 ─────────────────────────────

/** openPath 的 path 语义：repo=相对仓根（工作台树）；workspace=相对工作区根（文件夹视图）。 */
type PathScope = 'repo' | 'workspace'

function dangerousConfirm(path: string, name: string, mode: 'open' | 'reveal', scope: 'repo' | 'workspace' = 'repo'): void {
  openConfirm({
    title: `打开 ${name}？`,
    body: `${name} 可能包含可执行脚本或未知格式，用默认程序打开存在风险。`,
    detail: path,
    confirmLabel: '仍然打开',
    danger: true,
    onConfirm: () => {
      closeConfirm()
      void doOpenPath(path, name, mode, true, scope)
    },
  })
}

async function openNode(node: Pick<TreeNodeVm, 'path' | 'name' | 'kind'>): Promise<void> {
  const mode: 'open' | 'reveal' = node.kind === 'DIR' ? 'reveal' : 'open'
  if (node.kind === 'FILE' && isDangerousFile(node.name)) {
    dangerousConfirm(node.path, node.name, mode)
    return
  }
  await doOpenPath(node.path, node.name, mode, false)
}

async function doOpenPath(
  path: string,
  name: string,
  mode: 'open' | 'reveal',
  confirm: boolean,
  scope: PathScope = 'repo',
): Promise<void> {
  const repo = focusedRepo()
  const component = selectedComponent()
  if (mode === 'open' && !isSafeFile(name) && !confirm) {
    dangerousConfirm(path, name, mode, scope)
    return
  }
  try {
    if (isMockMode()) {
      const result = mock.mockOpenPath(path, mode)
      notify(result.message ?? `已打开：${path}`, 'success')
      state.action.lastOp = {
        action: mode === 'open' ? 'OPEN' : 'REVEAL',
        componentId: component.id,
        repoType: repo?.repoType ?? state.focusedRepoType,
        scope: repo?.repoType ?? state.focusedRepoType,
        state: 'success',
        at: new Date().toISOString(),
      }
      return
    }
    const result = await api.openPath({
      ...(scope === 'repo' ? { componentId: component.id, repoType: repo?.repoType } : {}),
      path,
      mode,
      confirm,
    })
    if (result.opened) {
      notify(mode === 'reveal' ? `已打开所在目录：${path}` : `已打开：${path}`, 'success')
    } else {
      notify(`无法打开：${result.errorCode ?? 'E_LAUNCH_FAILED'} ${result.message ?? ''}`, 'error', 6000)
    }
  } catch (error) {
    if (error instanceof HostError && error.code === 'E_CONFIRM_REQUIRED') {
      dangerousConfirm(path, name, mode, scope)
      return
    }
    if (isMethodUnavailable(error)) {
      notify(`Mock：${mode === 'reveal' ? '已打开所在目录' : '已打开'} ${path}（Host 未实现 openPath）`, 'info', 4000)
      return
    }
    notify(`无法打开：${toErrorMessage(error)}`, 'error', 6000)
  }
}

async function runAction(action: RepoAction): Promise<void> {
  if (state.action.pending) return
  if (state.overlays.actionDialog || state.overlays.confirm) return
  // 已打开的确认条视为未确认：切换动作前先作废（0 写），避免旧仓/旧动作的确认被误用。
  if (state.overlays.confirmBar) closeConfirmBar()
  const repo = focusedRepo()
  if (!repo) {
    notify('请先在文件树中选择仓库', 'warning')
    return
  }
  const cell = availability()[action]
  if (!cell.enabled) {
    notify(`${cell.reason}${cell.code ? ` [${cell.code}]` : ''}${cell.alt ? ` → ${cell.alt}` : ''}`, 'warning', 4000)
    return
  }
  const target: ActionTarget = { componentId: selectedComponent().id, repoType: repo.repoType }
  switch (action) {
    case 'COMMIT':
      // 第一步：输入提交信息 + 暂存范围；第二步（提交对话框确认后）才是 F 区确认条，确认后才写。
      openCommitDialog(target, repo)
      return
    case 'CHKOUT':
      // spec §6.3：CHKOUT 只弹分支/Tag 选择器，不走确认条（免确认）。
      openCheckoutDialog(target, repo)
      return
    case 'PULL':
      // spec §6.3：常规 PULL = 预检 + --ff-only，不弹确认。
      void executeRepoAction('PULL', {}, false, target)
      return
    case 'PUSH':
      // FR-ACT-04 / D-025②：PUSH 是写远端操作，必须先经 F 区确认条，禁止零确认。
      openPushConfirmBar(target, repo)
      return
    default:
      // FETCH 为短网络只读探测，按 spec §6.4 免确认直接执行。
      void executeRepoAction('FETCH', {}, false, target)
  }
}

// ───────────────────────────── M2 五键执行 ─────────────────────────────

interface ActionTarget {
  componentId: string
  repoType: RepoType
}

function openCommitDialog(target: ActionTarget, repo: RepoVm): void {
  const staged = Math.max(0, repo.stagedCount)
  const unstaged = Math.max(0, repo.dirtyCount) + Math.max(0, repo.untrackedCount)
  openActionDialog({
    kind: 'commit',
    action: 'COMMIT',
    title: 'COMMIT · 提交已暂存改动',
    body: `将提交 ${repo.repoName || repo.repoType}（${repo.repoType}）当前分支 ${repo.branch} 的已暂存改动：已暂存 ${staged} 个文件 / 将提交 ${staged} 个文件；提交信息必填（1–200 字符）。默认只提交已暂存内容，不自动 git add -A。`,
    detail: repo.localPath ?? undefined,
    confirmLabel: '下一步：确认提交',
    danger: false,
    initialMessage: '',
    stagedCount: staged,
    dirtyCount: Math.max(0, repo.dirtyCount),
    untrackedCount: Math.max(0, repo.untrackedCount),
    showStageAll: true,
    info: [
      { label: '仓库', value: repo.repoName || repo.repoType },
      { label: '分支', value: repo.branch },
      { label: '已暂存', value: `${staged} 个文件`, tone: staged > 0 ? 'muted' : 'warning' },
      { label: '将提交', value: `${staged} 个文件`, tone: staged > 0 ? 'muted' : 'warning' },
      {
        label: '未提交',
        value: `已跟踪 ${repo.dirtyCount} · 未跟踪 ${repo.untrackedCount}`,
        tone: unstaged > 0 ? 'warning' : 'muted',
      },
    ],
    submit: (payload) => {
      const message = (payload.message ?? '').trim()
      if (!message) {
        notify('提交信息不能为空', 'warning')
        openCommitDialog(target, repo)
        return
      }
      // 提交对话框只负责收集参数；真正的写操作必须再经 F 区确认条（0 写直到 accept）。
      openCommitConfirmBar(target, repo, message, payload.stageAll === true)
    },
  })
}

/** COMMIT 的 F 区确认条：文案「将提交 N 个文件（已暂存 M）」，accept 后才执行写操作。 */
function openCommitConfirmBar(target: ActionTarget, repo: RepoVm, message: string, stageAll: boolean): void {
  const staged = Math.max(0, repo.stagedCount)
  const extra = Math.max(0, repo.dirtyCount) + Math.max(0, repo.untrackedCount)
  const willCommit = stageAll ? staged + extra : staged
  const label = repo.branch && repo.branch !== '—' ? repo.branch : 'HEAD'
  openConfirmBar({
    action: 'COMMIT',
    text: `将提交 ${willCommit} 个文件（已暂存 ${staged}）`,
    detail: stageAll
      ? `已勾选「暂存全部改动（git add -A）」：将在提交前暂存剩余 ${extra} 处改动，然后提交全部 ${willCommit} 个文件（分支 ${label}）`
      : `分支 ${label} · 仅提交已暂存内容，不自动 git add -A`,
    confirmLabel: '确认提交',
    danger: false,
    onAccept: () => {
      const args: RepoActionArgs = stageAll ? { message, stageAll: true, confirm: true } : { message, confirm: true }
      void executeRepoAction('COMMIT', args, true, target)
    },
  })
}

/**
 * PUSH 的 F 区确认条：常规「将推送 N 个提交（分支 → upstream）」；
 * NO_UPSTREAM 走「发布分支并建立 upstream」；M2b-2 EMPTY 首推同样走发布分支 + setUpstream=true
 * （Host 矩阵恢复后按钮才可用；UI 不做本地可用性冻结）。
 */
function openPushConfirmBar(target: ActionTarget, repo: RepoVm): void {
  const stateCode = repo.state.toUpperCase()
  const noUpstream = stateCode === 'NO_UPSTREAM'
  const empty = stateCode === 'EMPTY'
  const releaseBranch = noUpstream || empty
  const label = repo.branch && repo.branch !== '—' ? repo.branch : 'HEAD'
  const upstreamRaw = (repo.compareRef ?? '').replace(/^refs\/remotes\//, '').trim()
  const upstream = upstreamRaw && upstreamRaw !== label ? upstreamRaw : `origin/${label}`
  const commits = Math.max(0, repo.ahead)
  openConfirmBar({
    action: 'PUSH',
    text: empty
      ? `发布分支并建立 upstream（${label} → ${upstream}）`
      : `将推送 ${commits} 个提交（${label} → ${upstream}）`,
    detail: empty
      ? `空仓首推：首次推送将建立 upstream（setUpstream=true）；${label} → ${upstream}`
      : noUpstream
        ? `发布分支：${label} → ${upstream}；首次推送将建立 upstream（setUpstream=true）`
        : undefined,
    confirmLabel: '确认推送',
    danger: true,
    onAccept: () => {
      const args: RepoActionArgs = releaseBranch ? { setUpstream: true, confirm: true } : { confirm: true }
      void executeRepoAction('PUSH', args, true, target)
    },
  })
}

/** CHKOUT 对话框重开（校验失败）时保留已加载的 refs 与搜索词。 */
interface CheckoutDialogSeed {
  refs?: ActionDialogRefVm[]
  refsStatus?: ActionDialogRefsStatus
  refsError?: string
  refsTruncated?: boolean
  search?: string
  refKind?: 'branch' | 'tag'
  refValue?: string
}

function dedupeRefs(refs: ActionDialogRefVm[]): ActionDialogRefVm[] {
  const seen = new Set<string>()
  const out: ActionDialogRefVm[] = []
  for (const ref of refs) {
    const key = `${ref.kind}:${ref.refName}`
    if (seen.has(key)) continue
    seen.add(key)
    out.push(ref)
  }
  return out
}

/** 无 Host / Mock / getRemoteRefs 不可用时的回退：当前分支 + compareRef（远程）+ 当前 Tag 建议。 */
function checkoutFallbackRefs(repo: RepoVm): ActionDialogRefVm[] {
  const head = repo.headSha && repo.headSha !== '—' ? shortSha(repo.headSha) : ''
  const refs: ActionDialogRefVm[] = []
  const branch = (repo.branch ?? '').trim()
  if (branch && branch !== '—') {
    refs.push({ refName: `refs/heads/${branch}`, shortSha: head, kind: 'LOCAL_BRANCH' })
  }
  const compare = (repo.compareRef ?? '').trim()
  if (compare.startsWith('refs/remotes/')) {
    refs.push({ refName: compare, shortSha: head, kind: 'REMOTE_BRANCH' })
  } else if (compare.startsWith('refs/heads/')) {
    refs.push({ refName: compare, shortSha: head, kind: 'LOCAL_BRANCH' })
  }
  const tag = (repo.tag ?? '').trim()
  if (tag && tag !== '—') {
    refs.push({ refName: `refs/tags/${tag}`, shortSha: head, kind: 'TAG' })
  }
  return dedupeRefs(refs)
}

/**
 * CHKOUT 打开时按当前组件/仓加载 Host getRemoteRefs（M2 P0-7）。
 * loading → ready；Host 不可用/失败/Mock → 回退当前分支/Tag 建议（内联提示，不打断）。
 */
async function loadCheckoutRefs(target: ActionTarget, repo: RepoVm, token: number): Promise<void> {
  const apply = (patch: Partial<ActionDialogVm>): void => {
    if (token !== checkoutRefsSeq) return
    const current = state.overlays.actionDialog
    if (!current || current.kind !== 'checkout') return
    Object.assign(current, patch)
  }
  const fallback = checkoutFallbackRefs(repo)
  if (isMockMode()) {
    apply({ refs: fallback, refsStatus: 'fallback', refsError: '', refsTruncated: false })
    return
  }
  if (state.hostInfo?.features?.remoteRefs === false) {
    apply({
      refs: fallback,
      refsStatus: 'fallback',
      refsError: '当前 Host 未开启 getRemoteRefs，已回退为当前分支/Tag 建议',
      refsTruncated: false,
    })
    return
  }
  try {
    const dto = await api.getRemoteRefs({ componentId: target.componentId, repoType: target.repoType })
    const refs = dedupeRefs(
      dto.refs.map((ref) => ({ refName: ref.refName, shortSha: ref.shortSha, kind: ref.kind })),
    )
    if (refs.length === 0) {
      apply({ refs: fallback, refsStatus: 'fallback', refsError: '未读取到分支/Tag，已回退为当前分支/Tag 建议', refsTruncated: false })
      return
    }
    apply({ refs, refsStatus: 'ready', refsError: '', refsTruncated: dto.truncated })
  } catch (error) {
    // 契约未实现/超时/无 Host：回退建议，不弹错误 Toast（对话框内联说明即可）。
    apply({
      refs: fallback,
      refsStatus: 'fallback',
      refsError: `读取分支/Tag 失败（${toErrorMessage(error)}），已回退为当前分支/Tag 建议`,
      refsTruncated: false,
    })
  }
}

function openCheckoutDialog(target: ActionTarget, repo: RepoVm, seed?: CheckoutDialogSeed): void {
  const dirtyTotal = Math.max(0, repo.dirtyCount) + Math.max(0, repo.untrackedCount)
  const dirty = dirtyTotal > 0
  const head = repo.headSha && repo.headSha !== '—' ? shortSha(repo.headSha) : '—'
  const refs: ActionDialogRefVm[] = seed?.refs ?? (seed ? [] : checkoutFallbackRefs(repo))
  const dialog: ActionDialogVm = {
    kind: 'checkout',
    action: 'CHKOUT',
    title: 'CHKOUT · 检出分支 / Tag',
    body: dirty
      ? `将 ${repo.repoName || repo.repoType}（${repo.repoType}）从 ${repo.branch} 检出到指定分支或 Tag。工作区有 ${dirtyTotal} 处未提交改动：勾选「检出前自动 stash（-u）」后由 Host 先 stash 再检出，否则 Host 将返回 E_DIRTY_WORKTREE。`
      : `将 ${repo.repoName || repo.repoType}（${repo.repoType}）从 ${repo.branch} 检出到指定分支或 Tag。`,
    detail: dirty
      ? `工作区 dirty：${dirtyTotal} 处改动（含未跟踪）将在检出前自动 git stash push -u 保存`
      : repo.localPath ?? undefined,
    confirmLabel: '检出',
    danger: false,
    initialRefKind: seed?.refKind ?? (repo.tag && repo.branch === '—' ? 'tag' : 'branch'),
    initialRefValue: seed?.refValue ?? '',
    initialSearch: seed?.search ?? '',
    refs,
    refsStatus: seed?.refsStatus ?? (seed ? 'ready' : 'loading'),
    refsError: seed?.refsError ?? '',
    refsTruncated: seed?.refsTruncated ?? false,
    showStash: dirty,
    dirtyTotal,
    info: [
      { label: '仓库', value: repo.repoName || repo.repoType },
      { label: '当前分支', value: repo.branch },
      { label: '当前 HEAD', value: head },
      ...(dirty
        ? [{ label: '工作区', value: `${dirtyTotal} 处未提交改动`, tone: 'warning' as const }]
        : []),
    ],
    submit: (payload) => {
      const value = (payload.refValue ?? '').trim()
      if (!value) {
        notify('请填写或选择分支 / Tag 名称', 'warning')
        // 校验失败重开：保留已加载 refs 与搜索词，避免重新请求。
        openCheckoutDialog(target, repo, {
          refs: dialog.refs ?? [],
          refsStatus: dialog.refsStatus ?? 'ready',
          refsError: dialog.refsError,
          refsTruncated: dialog.refsTruncated,
          search: payload.search,
          refKind: payload.refKind,
          refValue: payload.refValue,
        })
        return
      }
      const isTag = payload.refKind === 'tag'
      const args: RepoActionArgs = isTag ? { tag: value, confirm: true } : { branch: value, confirm: true }
      if (payload.stash === true) args.stash = true
      void executeRepoAction('CHKOUT', args, true, target)
    },
  }
  const token = ++checkoutRefsSeq
  openActionDialog(dialog)
  if (seed) return
  void loadCheckoutRefs(target, repo, token)
}

/** 动作成功后定向刷新快照（refresh 容错 + getSnapshot），不重建选中。 */
async function refreshSnapshot(): Promise<void> {
  if (isMockMode()) {
    applyMockSnapshot('Mock：动作后刷新')
    return
  }
  try {
    try {
      await api.refresh()
    } catch {
      // refresh 只回 ack 或 M1 未实现均可容忍；后续 getSnapshot 才是事实源。
    }
    const snapshot = await api.getSnapshot()
    applySnapshotHeader(snapshot, { mockFallback: snapshot.source === 'mock' })
    const component = selectedComponent()
    if (component.id && !repoOf(component, state.focusedRepoType)) {
      state.focusedRepoType = pickDefaultRepo(component)
    }
  } catch (error) {
    notify(`操作已完成，但状态刷新失败：${toErrorMessage(error)}（请手动刷新）`, 'warning', 5000)
  }
}

/** 成功后刷新 snapshot + 详情/四仓树（FR-ACT-06：≤2s 回写）。 */
async function refreshAfterAction(): Promise<void> {
  await refreshSnapshot()
  // 动作后 Host 矩阵可能随仓状态变化：强制重新读取（AC-UI-08 单源）。
  void loadHostActions(state.selectedComponentId, true)
  await loadActive()
}

async function executeRepoAction(
  action: RepoAction,
  args: RepoActionArgs,
  confirmed: boolean,
  target: ActionTarget,
): Promise<void> {
  if (state.action.pending) return
  state.action.pending = action
  state.action.lastOp = {
    action,
    componentId: target.componentId,
    repoType: target.repoType,
    scope: target.repoType,
    state: 'running',
    at: new Date().toISOString(),
  }
  const startedAt = Date.now()
  try {
    if (isMockMode()) {
      await delay(360)
      // 与 M1 Host 契约一致：Mock 仍返回 E_NOT_IMPLEMENTED，绝不伪造成功。
      mock.mockRunRepoAction(action)
    }
    const result = await api.runRepoAction({
      componentId: target.componentId,
      repoType: target.repoType,
      action,
      args,
    })
    if (!result.accepted) {
      throw new HostError(result.errorCode ?? 'E_ACTION_NOT_ALLOWED', result.message ?? '操作被拒绝')
    }
    const short = shortSha(result.postHead ?? result.preHead ?? null)
    const headText = short === '—' ? '' : short
    state.action.lastOp = {
      action,
      componentId: target.componentId,
      repoType: target.repoType,
      scope: target.repoType,
      state: 'success',
      at: new Date().toISOString(),
      message: headText || result.message,
    }
    if (action === 'COMMIT') {
      notify(headText ? `已提交 ${headText}` : '已提交（Host 未返回 SHA）', 'success', 2800)
    } else if (action === 'CHKOUT') {
      const ref = result.branch || args.tag || args.branch || ''
      const stashNote =
        args.stash === true
          ? result.stashRef
            ? ` · 已自动 stash：${result.stashRef}`
            : ' · 已自动 stash 工作区改动'
          : ''
      notify(`已检出 ${ref}${headText ? ` · HEAD ${headText}` : ''}${stashNote}`, 'success', 3200)
    } else {
      const duration = result.durationMs ?? Date.now() - startedAt
      notify(`${action} 完成${headText ? ` · HEAD ${headText}` : ''}（${Math.round(duration)}ms）`, 'success', 2800)
    }
    // 动作本身已结束：先释放按钮（刷新在后台继续），避免大工作区全量重扫期间锁死五键。
    state.action.pending = null
    await refreshAfterAction()
  } catch (error) {
    const info = actionErrorInfo(error)
    if (info.code === 'E_CONFIRM_REQUIRED' && !confirmed) {
      // Host 要求二次确认：只重试一次（confirmed=true），再次要求确认即停止，避免无限循环。
      state.action.lastOp = {
        action,
        componentId: target.componentId,
        repoType: target.repoType,
        scope: target.repoType,
        state: 'idle',
        at: new Date().toISOString(),
      }
      openConfirm({
        title: `${action} 需要二次确认`,
        body: info.detail || info.message,
        detail: info.hint,
        confirmLabel: '确认并重试',
        danger: true,
        onCancel: () => {
          state.action.lastOp = {
            action,
            componentId: target.componentId,
            repoType: target.repoType,
            scope: target.repoType,
            state: 'idle',
            at: new Date().toISOString(),
          }
        },
        onConfirm: () => {
          closeConfirm()
          void executeRepoAction(action, { ...args, confirm: true }, true, target)
        },
      })
      return
    }
    state.action.lastOp = {
      action,
      componentId: target.componentId,
      repoType: target.repoType,
      scope: target.repoType,
      state: 'error',
      at: new Date().toISOString(),
      code: info.code,
    }
    if (info.code === 'E_CONFIRM_REQUIRED') {
      notify(`✕ ${action} 失败 · E_CONFIRM_REQUIRED：Host 仍要求确认，已停止自动重试`, 'error', 6000)
      pushNotification(`${action} 失败`, 'E_CONFIRM_REQUIRED：二次确认后仍被拒绝，请检查仓库状态', 'err')
    } else if (info.code === 'E_NOT_IMPLEMENTED' || info.code === 'E_NO_HOST') {
      notify(`${info.code}：${info.message} · ${info.hint}`, 'warning', 5200)
      pushNotification(`${action} 未执行`, `${info.code}：${info.message}（${info.hint}）`, 'warn')
    } else {
      const detailText = info.detail ? ` · ${info.detail}` : ''
      notify(`✕ ${action} 失败 · ${info.code}：${info.message}${detailText}${info.hint ? `。${info.hint}` : ''}`, 'error', 6500)
      pushNotification(`${action} 失败`, `${info.code}：${info.message}${detailText}`, 'err')
    }
  } finally {
    state.action.pending = null
  }
}

// ───────────────────────────── 批量 ─────────────────────────────

function toggleSelectMode(): void {
  state.selectMode = !state.selectMode
  if (!state.selectMode) state.checkedIds = []
}

function toggleChecked(id: string): void {
  const at = state.checkedIds.indexOf(id)
  if (at >= 0) state.checkedIds.splice(at, 1)
  else state.checkedIds.push(id)
  if (state.checkedIds.length > 0) state.selectMode = true
}

function clearChecked(): void {
  state.checkedIds = []
  state.selectMode = false
}

function selectAllVisible(): void {
  state.selectMode = true
  state.checkedIds = visibleComponents().map((component) => component.id)
}

// ───────────────────────────── M2b 任务中心（Job） ─────────────────────────────

function shortJobId(jobId: string): string {
  return jobId.length > 14 ? `${jobId.slice(0, 14)}…` : jobId
}

function updateTaskCount(): void {
  state.taskCount = state.jobs.filter((job) => job.active).length
}

function sortJobs(jobs: JobVm[]): JobVm[] {
  return [...jobs].sort((a, b) => {
    if (a.active !== b.active) return a.active ? -1 : 1
    const aTime = a.active ? a.createdAt : (a.finishedAt ?? a.createdAt)
    const bTime = b.active ? b.createdAt : (b.finishedAt ?? b.createdAt)
    return (bTime || '').localeCompare(aTime || '')
  })
}

/** 合并同一 job 的增量更新：进度计数单调不减，终止态不被迟到的进行中事件覆盖。 */
function mergeJob(prev: JobVm, next: JobVm): JobVm {
  if (prev.finishedAt && !next.finishedAt) return prev
  const merged: JobVm = {
    ...next,
    ok: Math.max(prev.ok, next.ok),
    failed: Math.max(prev.failed, next.failed),
    skipped: Math.max(prev.skipped, next.skipped),
    total: Math.max(prev.total, next.total),
  }
  // 事件信封可能只带状态/计数（partial）：保留上一份明细与不可变字段，避免把 items 冲成空。
  if (next.partial) {
    if (merged.items.length === 0) merged.items = prev.items
    merged.kind = next.kind || prev.kind
    merged.createdAt = next.createdAt || prev.createdAt
  }
  if (dryRunJobIds.has(merged.id)) merged.dryRun = true
  // dryRun 是 job 不可变属性：后续快照缺字段/旧 Host 不带 dryRun 时不得丢标记。
  if (prev.dryRun) merged.dryRun = true
  return merged
}

/** 作业终态：通知中心 +1、可感知 Toast、并触发快照回写（卡片/状态栏 ≤2s）。 */
function notifyJobTerminal(job: JobVm): void {
  const meta = jobStateMeta(job.state)
  const dryRun = jobIsDryRun(job)
  const detail = dryRun
    ? `dryRun 预览：未执行任何写操作（预检 ${job.total} 仓，跳过 ${job.skipped}）`
    : `成功 ${job.ok} · 失败 ${job.failed} · 跳过 ${job.skipped} · 已取消 ${jobCanceledCountOf(job)}`
  const title = dryRun ? `${jobKindLabel(job.kind)} · 预览完成（dryRun）` : `${jobKindLabel(job.kind)} · ${meta.label}`
  const level = dryRun || job.state === 'SUCCESS' ? 'info' : job.state === 'PARTIAL' ? 'warn' : 'err'
  pushNotification(title, detail, level)
  if (state.overlays.drawer !== 'tasks') {
    const tone = dryRun || job.state === 'SUCCESS' ? 'success' : job.state === 'PARTIAL' ? 'warning' : 'error'
    notify(`${title}（${detail}）`, tone, 4_200)
  }
  scheduleJobSnapshotRefresh()
}

function jobCanceledCountOf(job: JobVm): number {
  return job.items.filter((item) => item.state === 'CANCELED').length
}

/** 作业终态后合并刷新快照（避免 PARTIAL 多项连续完成时重复全量刷新）。 */
function scheduleJobSnapshotRefresh(): void {
  if (jobSnapshotTimer !== undefined) clearTimeout(jobSnapshotTimer)
  jobSnapshotTimer = setTimeout(() => {
    jobSnapshotTimer = undefined
    void refreshSnapshot().then(() => loadDetail())
  }, 450)
}

/** 应用整份作业列表（Mock 轮询 / Host 列表刷新），并检测 active → 终态迁移。 */
function applyJobs(nextJobs: JobVm[]): void {
  const previous = new Map(state.jobs.map((job) => [job.id, job]))
  const added: JobVm[] = []
  const merged = nextJobs.map((next) => {
    const prev = previous.get(next.id)
    const job = prev ? mergeJob(prev, next) : next
    if (!prev) {
      if (dryRunJobIds.has(job.id)) job.dryRun = true
      added.push(job)
    }
    if (prev && prev.active && !job.active) notifyJobTerminal(job)
    return job
  })
  state.jobs = sortJobs(merged)
  state.jobsAt = Date.now()
  updateTaskCount()
  // P0-2 修复：列表合并只更新计数/行状态，**不**按旧 currentJobId 强制切换分段。
  noteNewActiveJobs(added)
}

/** 单个 JOB_UPDATED 事件：按 id 合并（事件可能乱序/重复）。 */
function applyJobUpdate(dto: JobDto): void {
  const next = mapJob(dto)
  const prev = state.jobs.find((job) => job.id === next.id)
  if (prev) {
    const merged = mergeJob(prev, next)
    state.jobs = sortJobs(state.jobs.map((job) => (job.id === next.id ? merged : job)))
    if (prev.active && !merged.active) notifyJobTerminal(merged)
  } else {
    if (dryRunJobIds.has(next.id)) next.dryRun = true
    state.jobs = sortJobs([...state.jobs, next])
    if (!next.active) notifyJobTerminal(next)
    else noteNewActiveJobs([next])
  }
  state.jobsSource = isMockMode() ? 'mock' : 'host'
  state.jobsStatus = 'ready'
  state.jobsError = ''
  state.jobsAt = Date.now()
  updateTaskCount()
}

/**
 * 新出现的进行中作业：若用户当前不在「进行中」分段，只累计未读提示（tab 角标），
 * 不静默抢走当前分段（P0-2 的根因修复）。
 */
function noteNewActiveJobs(added: JobVm[]): void {
  const active = added.filter((job) => job.active)
  if (active.length === 0) return
  if (state.tasksSegment === 'active' && state.overlays.drawer === 'tasks') return
  state.tasksUnreadActive += active.length
}

/** 显式定位某个 job（提交/取消/重试/点选）：同步高亮、分段与展开。 */
function focusJob(jobId: string): void {
  if (!jobId) return
  state.currentJobId = jobId
  const job = state.jobs.find((item) => item.id === jobId)
  if (job) state.tasksSegment = job.active ? 'active' : 'history'
  if (!state.expandedJobIds.includes(jobId)) state.expandedJobIds.push(jobId)
  if (state.tasksSegment === 'active') state.tasksUnreadActive = 0
}

function activeJobs(): JobVm[] {
  return state.jobs.filter((job) => job.active)
}

function historyJobs(): JobVm[] {
  return state.jobs.filter((job) => !job.active)
}

function currentJob(): JobVm | null {
  return state.jobs.find((job) => job.id === state.currentJobId) ?? null
}

function setTasksSegment(segment: TasksSegment): void {
  state.tasksSegment = segment
  if (segment === 'active') state.tasksUnreadActive = 0
}

function selectJob(jobId: string): void {
  focusJob(jobId)
}

function toggleJobExpanded(jobId: string): void {
  const at = state.expandedJobIds.indexOf(jobId)
  if (at >= 0) state.expandedJobIds.splice(at, 1)
  else state.expandedJobIds.push(jobId)
}

async function refreshJobs(silent = false): Promise<void> {
  if (state.jobsLoading) return
  state.jobsLoading = true
  if (!silent && state.jobsStatus !== 'ready') state.jobsStatus = 'loading'
  try {
    if (isMockMode()) {
      mock.tickMockJobs()
      applyJobs(mock.mockJobs().map(mapJob))
      state.jobsSource = 'mock'
      state.jobsStatus = 'ready'
      state.jobsError = ''
      return
    }
    const jobs = (await api.getJobs(50)).map(mapJob)
    applyJobs(jobs)
    state.jobsSource = 'host'
    state.jobsStatus = 'ready'
    state.jobsError = ''
  } catch (error) {
    state.jobsSource = 'host'
    state.jobsStatus = 'error'
    state.jobsError = isMethodUnavailable(error)
      ? `Host 任务接口未就绪（getJobs/runBatch 未实现）：${toErrorMessage(error)}`
      : toErrorMessage(error)
  } finally {
    state.jobsLoading = false
    updateTaskCount()
  }
}

function ensureJobsLoaded(force = false): void {
  if (state.jobsLoading) return
  if (!force && (state.jobsStatus === 'loading' || state.jobsStatus === 'ready')) return
  void refreshJobs(force)
}

/**
 * 任务中心时钟：Mock 模式推进演示作业进度；Host 模式仅在「有进行中作业或抽屉打开」时
 * 以 getJobs 轮询兜底（JOB_UPDATED 为主通道，事件丢失时仍可收敛）。
 */
function startJobsClock(): void {
  if (jobsClockTimer !== undefined) return
  jobsClockTimer = window.setInterval(() => {
    if (isMockMode()) {
      mock.tickMockJobs()
      applyJobs(mock.mockJobs().map(mapJob))
      state.jobsSource = 'mock'
      state.jobsStatus = 'ready'
      state.jobsError = ''
      return
    }
    if (state.jobs.some((job) => job.active) || (state.overlays.drawer === 'tasks' && state.jobsStatus !== 'error')) {
      void refreshJobs(true)
    }
  }, 1_500)
}

/** 初始化作业中心（Mock 演示 / Host 订阅 + 首次拉取）。 */
async function initJobs(): Promise<void> {
  if (state.jobsStatus === 'ready' || state.jobsStatus === 'loading') return
  if (isMockMode()) {
    mock.tickMockJobs()
    applyJobs(mock.mockJobs().map(mapJob))
    state.jobsSource = 'mock'
    state.jobsStatus = 'ready'
    state.jobsError = ''
    startJobsClock()
    return
  }
  if (!jobsUnsubscribe) jobsUnsubscribe = onJobUpdated((job) => applyJobUpdate(job))
  await refreshJobs(true)
  startJobsClock()
}

function openTaskCenter(jobId?: string): void {
  if (jobId) focusJob(jobId)
  else if (state.tasksSegment === 'active') state.tasksUnreadActive = 0
  openDrawer('tasks')
  void ensureJobsLoaded(true)
}

// ───────────────────────────── M2b 批量入口 ─────────────────────────────

/** 有效的已勾选组件（过滤掉已不在快照里的 id）。 */
function checkedComponentIds(): string[] {
  return state.checkedIds.filter((id) => state.components.some((component) => component.id === id))
}

/**
 * 批量目标（P1-3 修复）：
 * - 批量选择模式：**只**取勾选集合；0 勾选 = 空集合（不再隐式回退到当前选中组件）。
 * - 非选择模式（菜单/右键/卡片快捷入口）：显式作用于当前选中组件，确认对话框逐条列出目标。
 */
function batchTargetComponentIds(): string[] {
  if (state.selectMode) return checkedComponentIds()
  return state.selectedComponentId ? [state.selectedComponentId] : []
}

/** 选择模式下 0 勾选：三处批量入口统一禁用并提示「先勾选组件」。 */
function batchCommandsBlocked(): boolean {
  return state.selectMode && checkedComponentIds().length === 0
}

function batchSelectionCount(): number {
  return checkedComponentIds().length
}

function batchCommandsHint(): string {
  return state.selectMode ? '先勾选组件（批量命令只对已勾选组件生效）' : '请先选择组件'
}

/** 三处入口统一的批量动作可用性（P1-2：未实现项显式禁用 + 原因）。 */
function batchActionState(kind: BatchKind): { enabled: boolean; title: string; blocked: boolean } {
  const def = batchActionDef(kind)
  if (def && !def.implemented) {
    return { enabled: false, blocked: false, title: def.title }
  }
  if (batchCommandsBlocked()) {
    return { enabled: false, blocked: true, title: `${batchCommandsHint()} [${batchActionLabel(kind)} 未提交]` }
  }
  return { enabled: true, blocked: false, title: def?.title ?? batchActionLabel(kind) }
}

function componentIdsText(componentIds: string[]): string {
  if (componentIds.length <= 3) return componentIds.join('、')
  return `${componentIds.slice(0, 2).join('、')} 等 ${componentIds.length} 个组件`
}

function commonBranchOf(componentIds: string[]): string {
  const branches = componentIds.map(
    (id) => state.components.find((component) => component.id === id)?.branch ?? '',
  )
  const first = branches[0] ?? ''
  if (!first || first === '—') return ''
  return branches.every((branch) => branch === first) ? first : ''
}

function commonTagOf(componentIds: string[]): string {
  const tags = componentIds.map(
    (id) => state.components.find((component) => component.id === id)?.tag ?? '',
  )
  const first = tags[0] ?? ''
  if (!first) return ''
  return tags.every((tag) => tag === first) ? first : ''
}

function openBatchDialog(dialog: BatchDialogVm): void {
  closeConfirm()
  closeContextMenu()
  state.overlays.batchDialog = dialog
}

function closeBatchDialog(): void {
  state.overlays.batchDialog = null
}

function submitBatchDialog(payload: { value?: string; dryRun?: boolean }): void {
  const dialog = state.overlays.batchDialog
  if (!dialog) return
  state.overlays.batchDialog = null
  dialog.submit(payload)
}

/** 需要二次确认的批量动作（不含需要参数输入/预览的 CREATE/TAG/CHKOUT/CLONE）。 */
type BatchConfirmKind = 'SYNC' | 'DELETE' | 'PULL' | 'FETCH'

/** SYNC/DELETE 强写：仅允许单个组件，要求输入组件 ID 二次确认（Host 校验 confirmText）。 */
function openDestructiveConfirm(kind: 'SYNC' | 'DELETE', componentIds: string[]): void {
  if (componentIds.length !== 1) {
    notify(`${kind} 仅支持单个组件：请只勾选 1 个组件后再执行（强覆盖/软删需输入组件 ID 确认）`, 'warning', 5_000)
    return
  }
  const componentId = componentIds[0]
  const isSync = kind === 'SYNC'
  openBatchDialog({
    kind: 'branch',
    action: kind,
    title: isSync ? `SYNC 受控覆盖 · ${componentId}` : `Delete 软删除 · ${componentId}`,
    body: isSync
      ? '将对该组件四仓执行：预检 → 创建 refs/backup/sync 备份 → reset --hard origin/<branch>。脏仓/未推送/分叉仓将拒绝或跳过；不会整体回滚。请输入组件 ID 确认。'
      : '将把该组件四仓本地目录移到工作区 .trash（保留 30 天，可 RESTORE），不写远端。脏/有未推送/有 stash 的仓将被拒绝；请输入组件 ID 确认。',
    detail: `组件：${componentId} · 四仓（Design/Impl/MIL/SIL）`,
    confirmLabel: isSync ? '确认 SYNC' : '确认软删除',
    danger: true,
    inputLabel: '输入组件 ID 确认',
    inputPlaceholder: componentId,
    initialValue: '',
    showDryRun: true,
    dryRunHint: '勾选后仅逐仓预检，不创建备份 ref、不 reset、不移动目录',
    submit: (payload) => {
      const value = (payload.value ?? '').trim()
      if (value !== componentId) {
        notify(`确认失败：请输入完整组件 ID「${componentId}」`, 'warning', 4_000)
        return
      }
      const args: BatchArgs = { confirmText: value }
      if (payload.dryRun === true) args.dryRun = true
      void submitBatch(kind, { componentIds, args })
    },
  })
}

function openBatchConfirm(kind: BatchConfirmKind, componentIds: string[]): void {
  const count = componentIds.length
  const repoCount = count * 4
  const copy: Record<BatchConfirmKind, { title: string; body: string; confirmLabel: string; danger: boolean }> = {
    SYNC: {
      title: `批量 SYNC ${count} 个组件？`,
      body: `将对 ${count} 个组件的 ${repoCount} 个仓执行受控 SYNC（远程覆盖本地）。Host 会先预检并创建备份 ref；脏仓/未推送仓将失败或跳过，不整体回滚。逐仓结果提交后可在任务中心查看。`,
      confirmLabel: '确认 SYNC',
      danger: true,
    },
    DELETE: {
      title: `批量 Delete ${count} 个组件？`,
      body: `将把 ${count} 个组件的本地仓软删除到工作区 .trash（保留 30 天，可恢复），不写远端；失败项可在任务中心“仅重试失败项”。`,
      confirmLabel: '确认软删除',
      danger: true,
    },
    PULL: {
      title: `批量 PULL ${count} 个组件？`,
      body: `将对 ${count} 个组件的 ${repoCount} 个仓执行 PULL（拉取 origin 并快进合并）。本地有未提交改动或已分叉的仓将失败/跳过，不整体回滚；不自动 stash。逐仓结果提交后可在任务中心查看。`,
      confirmLabel: '确认 PULL',
      danger: true,
    },
    FETCH: {
      title: `批量 FETCH ${count} 个组件？`,
      body: `将对 ${count} 个组件的 ${repoCount} 个仓执行 FETCH（只读：仅更新 origin/* 远程引用，不修改工作区与本地分支）。逐仓结果提交后可在任务中心查看。`,
      confirmLabel: '确认 FETCH',
      danger: false,
    },
  }
  const entry = copy[kind]
  openConfirm({
    title: entry.title,
    body: entry.body,
    detail: `组件：${componentIdsText(componentIds)}`,
    confirmLabel: entry.confirmLabel,
    danger: entry.danger,
    onConfirm: () => {
      closeConfirm()
      void submitBatch(kind, { componentIds })
    },
  })
}

function openClonePreview(componentIds: string[]): void {
  const missing: { componentId: string; repoType: string; label: string }[] = []
  for (const componentId of componentIds) {
    const component = state.components.find((item) => item.id === componentId)
    if (!component) continue
    for (const repo of component.repos) {
      if (repo.state !== 'MISSING') continue
      missing.push({ componentId, repoType: repo.repoType, label: `${componentId} / ${repo.repoType}` })
    }
  }
  if (missing.length === 0) {
    notify('所选组件无缺失仓，无需 Clone', 'info', 4_000)
    return
  }
  openBatchDialog({
    kind: 'clone',
    action: 'CLONE',
    title: `批量 Clone 缺失仓（${missing.length} 个）`,
    body: `将克隆 ${missing.length} 个缺失仓到本地工作区；已存在的仓不会重复克隆。提交后在任务中心逐仓显示进度与失败原因。`,
    detail: `组件 ${componentIds.length} 个 · 缺失仓 ${missing.length} 个`,
    confirmLabel: '确认 Clone',
    danger: false,
    showDryRun: true,
    dryRunHint: '勾选后仅预览将克隆的仓库，不写本地',
    preview: missing,
    submit: (payload) => {
      const args: BatchArgs = {}
      if (payload.dryRun === true) args.dryRun = true
      void submitBatch('CLONE', {
        componentIds,
        repoTypes: [...new Set(missing.map((item) => item.repoType))],
        args,
      })
    },
  })
}

function openBatchInput(kind: 'CREATE' | 'TAG' | 'CHKOUT', componentIds: string[]): void {
  const isTag = kind === 'TAG'
  const isChkout = kind === 'CHKOUT'
  openBatchDialog({
    kind: isTag ? 'tag' : isChkout ? 'chkout' : 'branch',
    action: kind,
    title: isTag
      ? `批量打 Tag（${componentIds.length} 个组件）`
      : isChkout
        ? `批量 CHKOUT（${componentIds.length} 个组件）`
        : `批量 CREATE 分支（${componentIds.length} 个组件）`,
    body: isTag
      ? `将在所选组件的各仓创建/切换到同名 Tag。名称由 Host 校验；同名 Tag 已存在或工作区脏的仓将失败。`
      : isChkout
        ? `将在所选组件的各仓检出指定分支/Tag（refs/tags/ 前缀按 Tag 处理）。工作区脏或 ref 不存在的仓将失败/跳过，不整体回滚。`
        : `将在所选组件的各仓创建同名分支（已存在则切换）。名称由 Host 校验；工作区脏或游离 HEAD 的仓将失败。`,
    detail: `组件：${componentIdsText(componentIds)}`,
    confirmLabel: isTag ? '确认打 Tag' : isChkout ? '确认 CHKOUT' : '确认 CREATE',
    danger: false,
    inputLabel: isTag ? 'Tag 名称' : isChkout ? '目标分支/Tag' : '分支名',
    inputPlaceholder: isTag ? '例如 v1.2.0' : isChkout ? '例如 main 或 refs/tags/v1.2.0' : '例如 release/1.2',
    initialValue: isTag ? commonTagOf(componentIds) : isChkout ? commonBranchOf(componentIds) : commonBranchOf(componentIds),
    showDryRun: true,
    dryRunHint: isTag || !isChkout ? '勾选后仅校验名称并预览，不写本地' : '勾选后仅预检 ref 与仓库状态，不检出',
    submit: (payload) => {
      const value = (payload.value ?? '').trim()
      if (!value) {
        notify(isTag ? '请填写 Tag 名称' : isChkout ? '请填写目标分支/Tag' : '请填写分支名', 'warning')
        return
      }
      let args: BatchArgs
      if (isTag) args = { tag: value }
      else if (isChkout) {
        const tagRef = /^refs\/tags\/(.+)$/.exec(value)
        args = tagRef ? { tag: tagRef[1] } : { branch: value }
      } else args = { branch: value }
      if (payload.dryRun === true) args.dryRun = true
      void submitBatch(kind, { componentIds, args })
    },
  })
}

/** 仅当 Host 明确未实现作业接口时阻止提交；瞬时失败（超时等）仍允许用户重试。 */
function jobsApiUnavailable(): boolean {
  if (isMockMode() || state.jobsStatus !== 'error') return false
  return /未实现|E_UNKNOWN_METHOD|E_NOT_IMPLEMENTED/.test(state.jobsError)
}

/**
 * 批量入口（BatchBar / 卡片右键 / 菜单）：先按 kind 走确认/参数对话框，
 * 用户确认后才调用 runBatch（未确认 = 0 写）。
 */
function requestBatch(kind: BatchKind): void {
  // 未实现项（批量 COMMIT/PUSH）在三处入口显式禁用；万一路径触达也明确提示，不静默失败。
  const def = batchActionDef(kind)
  if (def && !def.implemented) {
    notify(`${def.label}：${def.title}`, 'warning', 5_000)
    return
  }
  if (state.batchPending) {
    notify('已有批量提交进行中，请稍候', 'warning')
    return
  }
  if (jobsApiUnavailable()) {
    notify(`无法提交${jobKindLabel(kind)}：${state.jobsError}`, 'warning', 6_000)
    return
  }
  const componentIds = batchTargetComponentIds()
  if (componentIds.length === 0) {
    // P1-3：选择模式 0 勾选时禁用并提示，不隐式回退到当前组件。
    notify(
      state.selectMode
        ? `先勾选组件：${batchActionLabel(kind)}未提交（批量选择模式下只对已勾选组件生效）`
        : '请先选择组件',
      'warning',
    )
    return
  }
  switch (kind) {
    case 'SYNC':
    case 'DELETE':
      openDestructiveConfirm(kind, componentIds)
      return
    case 'PULL':
    case 'FETCH':
      openBatchConfirm(kind, componentIds)
      return
    case 'CLONE':
      openClonePreview(componentIds)
      return
    case 'CREATE':
    case 'TAG':
    case 'CHKOUT':
      openBatchInput(kind, componentIds)
      return
    case 'RESTORE':
      openRestoreConfirm(componentIds)
      return
    default:
      notify(`${batchActionLabel(kind)}：Host 未声明支持该作业类型`, 'warning', 4_200)
  }
}

/** 真正的 Host/Mock 写入口：仅在确认/参数对话框提交后被调用。 */
async function submitBatch(
  kind: BatchKind,
  options: { componentIds: string[]; repoTypes?: string[]; args?: BatchArgs },
): Promise<void> {
  if (state.batchPending) return
  state.batchPending = true
  const args: BatchArgs = { ...(options.args ?? {}) }
  const dryRun = args.dryRun === true
  // 契约：COMMIT/PUSH/SYNC/DELETE/RESTORE/CREATE/TAG 属强写 kind，Host 要求 args.confirm=true；
  // 到达 submitBatch 说明用户已通过确认/参数对话框确认，故统一携带 confirm。
  if (['COMMIT', 'PUSH', 'SYNC', 'DELETE', 'RESTORE', 'CREATE', 'TAG'].includes(kind)) args.confirm = true
  const submitToast = (jobId: string, total: number, skipped = 0): void => {
    const skippedText = skipped > 0 ? ` · 跳过 ${skipped}` : ''
    if (dryRun) {
      notify(
        `已提交${jobKindLabel(kind)}（dryRun 预览，未执行任何写操作） · job ${shortJobId(jobId)}（${total} 仓预检${skippedText}）`,
        'info',
        5_000,
      )
    } else {
      notify(`已提交${jobKindLabel(kind)} · job ${shortJobId(jobId)}（${total} 仓${skippedText}）`, 'success', 4_200)
    }
  }
  try {
    if (isMockMode()) {
      const result = mock.mockRunBatch(kind, options.componentIds, options.repoTypes, args)
      if (dryRun && result.jobId) dryRunJobIds.add(result.jobId)
      applyJobs(mock.mockJobs().map(mapJob))
      state.jobsSource = 'mock'
      state.jobsStatus = 'ready'
      state.jobsError = ''
      submitToast(result.jobId, result.total, result.skipped)
      openTaskCenter(result.jobId)
      return
    }
    const result = await api.runBatch({
      kind,
      componentIds: options.componentIds,
      repoTypes: options.repoTypes,
      args,
    })
    if (dryRun && result.jobId) dryRunJobIds.add(result.jobId)
    await refreshJobs(true)
    if (!result.accepted) {
      notify(
        `${batchActionLabel(kind)}未被接受（accepted=false）${result.skipped > 0 ? ` · 跳过 ${result.skipped}` : ''}`,
        'warning',
        5_000,
      )
      return
    }
    if (result.jobId) {
      submitToast(result.jobId, result.total)
      openTaskCenter(result.jobId)
    } else {
      notify(`${batchActionLabel(kind)}已提交（Host 未返回 jobId）`, 'info', 4_200)
      openDrawer('tasks')
    }
  } catch (error) {
    if (error instanceof HostError && error.code === 'E_CONFIRM_REQUIRED' && args.confirm !== true) {
      openConfirm({
        title: `${batchActionLabel(kind)}需要二次确认`,
        body: toErrorMessage(error),
        confirmLabel: '确认并提交',
        danger: true,
        onConfirm: () => {
          closeConfirm()
          void submitBatch(kind, { ...options, args: { ...args, confirm: true } })
        },
      })
      return
    }
    const info = actionErrorInfo(error)
    if (isMethodUnavailable(error)) {
      notify(`Host 未实现 runBatch（M2b 后端未就绪）：${info.message}`, 'warning', 6_000)
    } else {
      notify(`${batchActionLabel(kind)}提交失败 · ${info.code}：${info.message}`, 'error', 6_500)
    }
    pushNotification(`${batchActionLabel(kind)}提交失败`, `${info.code}：${info.message}`, 'err')
  } finally {
    state.batchPending = false
  }
}

/** 任务中心：取消作业（合作式；已开始仓取消、未开始仓保持原状，不整体回滚）。 */
async function cancelJob(jobId: string): Promise<void> {
  if (!jobId) return
  try {
    if (isMockMode()) {
      const job = mock.mockCancelJob(jobId)
      if (job) applyJobUpdate(job)
      focusJob(jobId)
      notify(`已请求取消 ${shortJobId(jobId)}：已开始仓取消、未开始仓保持原状（合作式，不整体回滚；以实际扫描为准）`, 'warning', 4_200)
      return
    }
    // Host 契约：cancelJob 回 { jobId, state }（仅状态，不是完整 job 快照）。
    // 不能用它覆盖 items；先做状态补丁，再拉取完整列表（含逐仓 CANCELED/RUNNING 明细）。
    const returned = await api.cancelJob(jobId)
    const prev = state.jobs.find((job) => job.id === jobId)
    const terminalStates: JobVm['state'][] = ['PARTIAL', 'SUCCESS', 'FAILED', 'CANCELED', 'INTERRUPTED']
    if (returned && returned.items.length > 0) {
      applyJobUpdate(returned)
    } else if (returned && prev && terminalStates.includes(returned.state)) {
      const patched: JobVm = {
        ...prev,
        state: returned.state,
        cancellable: false,
        active: false,
        finishedAt: new Date().toISOString(),
      }
      state.jobs = sortJobs(state.jobs.map((job) => (job.id === jobId ? patched : job)))
      if (prev.active) notifyJobTerminal(patched)
      updateTaskCount()
    }
    await refreshJobs(true)
    focusJob(jobId)
    notify(`已请求取消 ${shortJobId(jobId)}：已开始仓取消、未开始仓保持原状（合作式，不整体回滚；终态以实际扫描为准）`, 'info', 3_800)
  } catch (error) {
    if (isMethodUnavailable(error)) notify('Host 未实现 cancelJob（M2b 后端未就绪）', 'warning', 5_000)
    else if (error instanceof HostError && error.code === 'E_JOB_NOT_FOUND')
      notify(`取消失败：任务不存在或已被淘汰（${shortJobId(jobId)}）`, 'warning', 4_000)
    else if (error instanceof HostError && error.code === 'E_ACTION_NOT_ALLOWED')
      notify(`取消失败：作业已结束，只有排队/进行中的作业可取消（${shortJobId(jobId)}）`, 'warning', 4_500)
    else notify(`取消失败：${toErrorMessage(error)}`, 'error', 5_000)
  }
}

/** 任务中心：仅重试失败/取消/中断项（成功仓不重跑）。 */
async function retryFailed(jobId: string): Promise<void> {
  if (!jobId) return
  try {
    if (isMockMode()) {
      const result = mock.mockRetryFailed(jobId)
      if (!result) {
        notify('该作业没有可重试的失败/取消项', 'warning', 4_000)
        return
      }
      applyJobs(mock.mockJobs().map(mapJob))
      focusJob(result.jobId)
      notify(`已提交重试作业 · job ${shortJobId(result.jobId)}（仅 ${result.total} 个失败/取消项）`, 'success', 4_200)
      return
    }
    const result = await api.retryFailed(jobId)
    await refreshJobs(true)
    if (result.accepted && result.jobId) {
      focusJob(result.jobId)
      notify(`已提交重试作业 · job ${shortJobId(result.jobId)}（仅 ${result.total || '—'} 项）`, 'success', 4_200)
    } else {
      // 契约：无可重试项时 { jobId:null, accepted:false, retried:0 }，不创建空作业。
      notify('该作业没有可重试的失败/取消项', 'warning', 4_000)
    }
  } catch (error) {
    if (isMethodUnavailable(error)) notify('Host 未实现 retryFailed（M2b 后端未就绪）', 'warning', 5_000)
    else if (error instanceof HostError && error.code === 'E_JOB_NOT_FOUND')
      notify(`重试失败：任务不存在或已被淘汰（${shortJobId(jobId)}）`, 'warning', 4_000)
    else notify(`重试失败项失败：${toErrorMessage(error)}`, 'error', 5_000)
  }
}

/**
 * 批量 RESTORE（从工作区 .trash 恢复）：Host 已实现 kind=RESTORE（按仓名查找最近 .trash 备份，
 * 目标已存在则 SKIPPED，不覆盖；写远端=0）。属破坏性操作，必须先确认。
 * 备份 ref 仅在任务中心展示（Host 当前未提供按 ref 恢复的参数/列表接口，不伪造恢复入口）。
 */
function openRestoreConfirm(componentIds: string[]): void {
  openConfirm({
    title: `从 .trash 恢复 ${componentIds.length} 个组件的缺失仓？`,
    body: `Host 将在工作区 .trash 中按仓名查找最近备份并恢复；目标路径已存在的仓跳过（不覆盖，需先 Delete）。不写远端；逐仓结果提交后可在任务中心查看。`,
    detail: `组件：${componentIdsText(componentIds)}`,
    confirmLabel: '确认恢复',
    danger: true,
    onConfirm: () => {
      closeConfirm()
      void submitBatch('RESTORE', { componentIds })
    },
  })
}

// ───────────────────────────── M2b-2 操作审计 ─────────────────────────────

/** 审计页数（total/limit；至少 1 页便于显示“第 x 页”）。 */
function auditPageCount(): number {
  return Math.max(1, Math.ceil(state.audit.total / Math.max(1, state.audit.limit)))
}

function auditPageIndex(): number {
  return Math.floor(state.audit.offset / Math.max(1, state.audit.limit))
}

function auditHasPrev(): boolean {
  return state.audit.offset > 0
}

function auditHasNext(): boolean {
  return state.audit.offset + state.audit.limit < state.audit.total
}

function auditRangeText(): string {
  if (state.audit.total === 0) return '0 条'
  const start = state.audit.offset + 1
  const end = Math.min(state.audit.total, state.audit.offset + state.audit.limit)
  return `第 ${start}–${end} / 共 ${state.audit.total} 条`
}

/**
 * 拉取操作审计（M2b-2）：
 * - Host：`getOperationLog`（只读）；未实现/失败时进入 unavailable/error 空态，**不展示伪造行**；
 * - Mock：内置演示审计（页面标注 Mock）。
 */
async function refreshOperationLog(reset = false): Promise<void> {
  if (state.audit.loading) return
  if (reset) state.audit.offset = 0
  state.audit.loading = true
  state.audit.status = 'loading'
  state.audit.error = ''
  try {
    const params = {
      limit: state.audit.limit,
      offset: state.audit.offset,
      action: state.audit.filters.action.trim() || undefined,
      result: state.audit.filters.result.trim() || undefined,
    }
    if (isMockMode()) {
      const page = mock.mockOperationLog(params)
      state.audit.items = page.items.map(mapOperationLogItem)
      state.audit.total = page.total
      state.audit.source = 'mock'
      state.audit.status = 'ready'
    } else {
      const page = await api.getOperationLog(params)
      state.audit.items = page.items.map(mapOperationLogItem)
      state.audit.total = page.total
      state.audit.source = 'host'
      state.audit.status = 'ready'
    }
    state.audit.at = Date.now()
  } catch (error) {
    state.audit.items = []
    state.audit.total = 0
    state.audit.source = isMockMode() ? 'mock' : 'host'
    state.audit.status = isMethodUnavailable(error) ? 'unavailable' : 'error'
    state.audit.error = isMethodUnavailable(error)
      ? `Host 未实现 getOperationLog（M2b-2 后端未就绪）：${toErrorMessage(error)}`
      : toErrorMessage(error)
  } finally {
    state.audit.loading = false
  }
}

function ensureOperationLogLoaded(force = false): void {
  if (!force && (state.audit.status === 'loading' || state.audit.status === 'ready')) return
  void refreshOperationLog(!force)
}

function setAuditActionFilter(value: string): void {
  state.audit.filters.action = value
}

function setAuditResultFilter(value: string): void {
  state.audit.filters.result = value
}

/** 过滤条件变更（输入框 change / 下拉 change）：回到第 1 页并重新查询。 */
function applyAuditFilters(): void {
  void refreshOperationLog(true)
}

function setAuditPage(pageIndex: number): void {
  const clamped = Math.max(0, Math.min(auditPageCount() - 1, pageIndex))
  const offset = clamped * state.audit.limit
  if (offset === state.audit.offset) return
  state.audit.offset = offset
  void refreshOperationLog()
}

function auditPrevPage(): void {
  if (auditHasPrev()) setAuditPage(auditPageIndex() - 1)
}

function auditNextPage(): void {
  if (auditHasNext()) setAuditPage(auditPageIndex() + 1)
}

/** 导出审计（csv/json）：成功后 Toast 显示真实 path 并提供「打开所在目录」；失败不伪造成功。 */
async function exportAudit(format: 'csv' | 'json'): Promise<void> {
  if (state.audit.exporting) return
  state.audit.exporting = format
  state.audit.exportError = ''
  try {
    const result = isMockMode()
      ? mock.mockExportOperationLog(format)
      : await api.exportOperationLog(format, {
          action: state.audit.filters.action || undefined,
          result: state.audit.filters.result || undefined,
        })
    state.audit.lastExport = { path: result.path, format, count: result.count }
    notifyAction(
      `已导出操作审计 ${format.toUpperCase()}：${result.path}（${result.count} 条）`,
      'success',
      10_000,
      '打开所在目录',
      () => {
        void openAuditExportDir()
      },
    )
    pushNotification('操作审计已导出', `${format.toUpperCase()} · ${result.path}（${result.count} 条）`, 'info')
  } catch (error) {
    state.audit.exportError = isMethodUnavailable(error)
      ? `Host 未实现 exportOperationLog（M2b-2 后端未就绪）：${toErrorMessage(error)}`
      : toErrorMessage(error)
    if (isMethodUnavailable(error)) {
      notify(`导出失败：Host 未实现 exportOperationLog（M2b-2 后端未就绪）`, 'warning', 6_000)
    } else {
      notify(`导出操作审计失败：${toErrorMessage(error)}`, 'error', 6_500)
    }
  } finally {
    state.audit.exporting = null
  }
}

/** 「打开所在目录」：用 Host openPath(reveal)；绝对路径被策略拒绝时明确告知（不假装成功）。 */
async function openAuditExportDir(): Promise<void> {
  const last = state.audit.lastExport
  if (!last?.path) {
    notify('尚未导出操作审计，没有可打开的目录', 'info', 3_200)
    return
  }
  if (isMockMode()) {
    mock.mockOpenPath(last.path, 'reveal')
    notify(`已打开所在目录（Mock）：${last.path}`, 'info', 4_000)
    return
  }
  try {
    const result = await api.openPath({ path: last.path, mode: 'reveal' })
    if (result.opened) notify(`已在资源管理器中打开：${last.path}`, 'success', 4_200)
    else notify(`无法打开所在目录：${result.errorCode ?? 'E_LAUNCH_FAILED'} ${result.message ?? ''}`, 'error', 6_000)
  } catch (error) {
    if (error instanceof HostError && error.code === 'E_CONFIRM_REQUIRED') {
      try {
        const retry = await api.openPath({ path: last.path, mode: 'reveal', confirm: true })
        if (retry.opened) {
          notify(`已在资源管理器中打开：${last.path}`, 'success', 4_200)
          return
        }
      } catch (retryError) {
        await fallbackCopyExportPath(last.path, toErrorMessage(retryError))
        return
      }
    }
    // Host openPath 仅允许工作区根内路径（导出在 %LOCALAPPDATA% 时会被拒）：明确原因并复制路径，
    // 绝不假装“已打开”。
    const code = error instanceof HostError ? error.code : ''
    if (code === 'E_PATH_OUTSIDE_ROOT' || code === 'E_PATH_INVALID' || code === 'E_INVALID_ARGUMENT') {
      await fallbackCopyExportPath(last.path, toErrorMessage(error))
      return
    }
    notify(`无法打开所在目录：${toErrorMessage(error)}（路径：${last.path}）`, 'error', 7_000)
  }
}

/** 导出目录无法被 openPath 打开时的兜底：复制绝对路径并说明原因（不伪造打开成功）。 */
async function fallbackCopyExportPath(path: string, reason: string): Promise<void> {
  try {
    if (navigator.clipboard?.writeText) await navigator.clipboard.writeText(path)
  } catch {
    /* 剪贴板不可用时仍给出路径 */
  }
  notify(`无法直接打开所在目录（${reason}）：导出路径已复制 · ${path}`, 'warning', 8_000)
  pushNotification('导出目录未打开', `${reason} · 路径：${path}`, 'warn')
}

// ───────────────────────────── M2b-2 备份 ref 恢复 ─────────────────────────────

/**
 * 按组件 + 仓类型加载可恢复备份 ref（Host `getBackupRefs`）。
 * 无备份为空态；接口不可用/失败为 unavailable/error 空态，绝不伪造 ref。
 */
async function loadBackupRefs(componentId: string, repoType?: RepoType): Promise<void> {
  if (!componentId) {
    backupRefsSeq += 1
    state.backupRefs = { status: 'idle', componentId: '', repoType: '', refs: [], error: '', source: 'host', at: 0 }
    return
  }
  const token = ++backupRefsSeq
  const type = repoType ?? state.focusedRepoType
  state.backupRefs = {
    status: 'loading',
    componentId,
    repoType: type,
    refs: [],
    error: '',
    source: isMockMode() ? 'mock' : 'host',
    at: 0,
  }
  try {
    if (isMockMode()) {
      const dto = mock.mockBackupRefs(componentId, type)
      if (token !== backupRefsSeq) return
      state.backupRefs.refs = dto.refs.map(mapBackupRef)
      state.backupRefs.source = 'mock'
      state.backupRefs.status = 'ready'
    } else {
      const dto = await api.getBackupRefs({ componentId, repoType: type })
      if (token !== backupRefsSeq) return
      state.backupRefs.refs = dto.refs.map(mapBackupRef)
      state.backupRefs.source = 'host'
      state.backupRefs.status = 'ready'
    }
    state.backupRefs.at = Date.now()
  } catch (error) {
    if (token !== backupRefsSeq) return
    state.backupRefs.refs = []
    state.backupRefs.status = isMethodUnavailable(error) ? 'unavailable' : 'error'
    state.backupRefs.error = isMethodUnavailable(error)
      ? `Host 未实现 getBackupRefs（M2b-2 后端未就绪）：${toErrorMessage(error)}`
      : toErrorMessage(error)
  }
}

/** 选择备份 ref 后先弹确认（显示将覆盖当前状态），确认后才调用 Host 恢复。 */
function requestRestoreBackupRef(ref: BackupRefVm): void {
  const componentId = state.backupRefs.componentId
  const repoType = state.backupRefs.repoType
  if (!componentId || !repoType || !ref.ref) return
  if (state.backupRestore.pending) {
    notify('已有备份恢复进行中，请稍候', 'warning')
    return
  }
  const component = state.components.find((item) => item.id === componentId)
  const repo = component ? repoOf(component, repoType as RepoType) : null
  const repoLabel = repoType
  openConfirm({
    title: '从备份 ref 恢复？',
    body: `将把 ${componentId}/${repoLabel} 从当前状态恢复到备份 ref ${ref.ref}（${shortSha(ref.sha)}）。这会覆盖当前分支状态：恢复后本地 HEAD 指向备份 SHA，未包含在该备份中的未推送改动可能丢失。恢复前工作区必须干净（无已跟踪改动、无未跟踪/忽略文件），否则 Host 会拒绝且不改动任何文件。`,
    detail: `备份 ref：${ref.ref}\n备份 SHA：${ref.sha}\n本次操作会真实写入本地仓库（不写远端）。`,
    confirmLabel: '确认恢复',
    danger: true,
    onConfirm: () => {
      closeConfirm()
      void executeRestoreBackupRef(componentId, repoType, ref.ref, repo?.headSha ?? null)
    },
  })
}

/** 真正的恢复写入口：仅在确认对话框 onConfirm 后调用；Host 拒绝（ok=false/异常）不得显示成功。 */
async function executeRestoreBackupRef(
  componentId: string,
  repoType: string,
  ref: string,
  currentHead: string | null,
): Promise<void> {
  if (state.backupRestore.pending) return
  state.backupRestore.pending = ref
  state.backupRestore.error = ''
  try {
    const result = isMockMode()
      ? mock.mockRestoreBackupRef(componentId, repoType, ref)
      : await api.restoreBackupRef({ componentId, repoType, ref, confirm: true })
    if (!result.ok) {
      // Host 明确拒绝：不刷新为成功态，给出错误并保留当前状态。
      state.backupRestore.error = 'Host 拒绝 restoreBackupRef（ok=false），仓库未被修改或恢复失败'
      notify(`✕ 恢复失败：Host 拒绝恢复 ${componentId}/${repoType}（未显示成功）`, 'error', 7_000)
      pushNotification('备份恢复被拒绝', `${componentId}/${repoType} · ${ref}：Host 返回 ok=false`, 'err')
      return
    }
    const pre = result.preHead ?? currentHead
    const post = result.postHead
    const safety = result.safetyRef ? ` · 恢复前 HEAD 已备份：${result.safetyRef}` : ''
    notify(
      `✓ 已从备份 ref 恢复 ${componentId}/${repoType}：${shortSha(pre)} → ${shortSha(post)}（${result.backupRef ?? ref}）${safety}`,
      'success',
      8_000,
    )
    pushNotification(
      '备份 ref 已恢复',
      `${componentId}/${repoType} · ${ref} · HEAD ${shortSha(pre)} → ${shortSha(post)}${safety}`,
      'info',
    )
    // 成功后刷新快照 / 详情 / 备份 ref 列表（真实回写 ≤2s 口径）。
    await refreshSnapshot()
    await loadActive()
    if (state.overlays.componentDrawer === componentId) await loadComponentDetail(componentId)
    await loadBackupRefs(componentId, repoType as RepoType)
  } catch (error) {
    state.backupRestore.error = toErrorMessage(error)
    if (isMethodUnavailable(error)) {
      notify('恢复未执行：Host 未实现 restoreBackupRef（M2b-2 后端未就绪）', 'warning', 6_000)
    } else {
      const info = actionErrorInfo(error)
      notify(
        `✕ 恢复失败 · ${info.code}：${info.message}${info.detail ? ` · ${info.detail}` : ''}${info.hint ? `。${info.hint}` : ''}`,
        'error',
        7_500,
      )
      pushNotification('备份恢复失败', `${info.code}：${info.message}`, 'err')
    }
  } finally {
    state.backupRestore.pending = null
  }
}

// ───────────────────────────── 右键菜单 ─────────────────────────────

function cardContextMenu(event: MouseEvent, componentId: string): void {
  if (state.selectedComponentId !== componentId) void selectComponent(componentId)
  // P1-2/P1-3：批量项与 BatchBar/菜单栏同一清单；选择模式 0 勾选时统一禁用并给出原因。
  const batchItems = BATCH_ACTIONS.map((action) => {
    const availability = batchActionState(action.kind)
    return {
      key: `component.${action.kind}`,
      label: `${action.label}…`,
      title: availability.title,
      disabled: !availability.enabled,
      danger: action.danger,
    }
  })
  openContextMenu(event.clientX, event.clientY, [
    { key: 'card.openWorkbench', label: '在工作台打开' },
    { key: 'card.detail', label: '查看组件详情（抽屉）' },
    { key: 'card.locateFolder', label: '在文件夹视图定位' },
    { key: '-', label: '', separator: true },
    ...batchItems,
    { key: '-', label: '', separator: true },
    { key: 'copy.componentId', label: '复制组件 ID' },
    { key: 'copy.componentPath', label: '复制组件路径' },
    { key: 'copy.branch', label: '复制分支名' },
    { key: '-', label: '', separator: true },
    { key: 'reveal.component', label: '在资源管理器中打开组件目录' },
  ])
}

function treeContextMenu(event: MouseEvent, repoType: RepoType, path: string, name: string): void {
  closeContextMenu()
  state.focusedRepoType = repoType
  state.selectedPath = path
  openContextMenu(event.clientX, event.clientY, [
    { key: `tree.reveal:${repoType}:${path}`, label: '在资源管理器中打开' },
    { key: `tree.copyPath:${path}`, label: '复制路径' },
    { key: `tree.copyName:${name}`, label: '复制文件名' },
    { key: '-', label: '', separator: true },
    { key: `tree.refresh:${repoType}`, label: '刷新该仓目录' },
  ])
}

// ───────────────────────────── 文件夹视图 ─────────────────────────────

function workspaceNodeToRepo(node: WorkspaceNodeVm): RepoVm | null {
  if (!node.repoType) return null
  return {
    repoType: node.repoType,
    repoName: node.label,
    state: node.state || 'UNKNOWN',
    tone: node.tone,
    branch: node.branch ?? '—',
    tag: null,
    headSha: null,
    ahead: node.ahead,
    behind: node.behind,
    dirtyCount: node.dirtyCount,
    untrackedCount: 0,
    stagedCount: 0,
    stashCount: 0,
    isLocal: node.hasChildren,
    localPath: node.path ? `${state.workspaceRoot}\\${node.path}` : null,
    compareRef: node.branch ? `refs/remotes/origin/${node.branch}` : null,
    calcAt: null,
    dataSource: null,
    isStale: false,
    errorCode: null,
    flags: [],
    ci: 'NONE',
  }
}

async function ensureFolderLoaded(force = false): Promise<void> {
  if (state.folder.status === 'loading') return
  if (!force && state.folder.status === 'ready' && Date.now() - state.folder.at < 30_000) return
  state.folder.status = 'loading'
  state.folder.error = ''
  try {
    let dto: WorkspaceTreeDto
    let fallback = false
    if (isMockMode()) {
      dto = mock.mockWorkspaceTree('')
      fallback = false
    } else {
      try {
        dto = await api.getWorkspaceTree({ path: '', depth: 1 })
      } catch (error) {
        if (!isMethodUnavailable(error)) throw error
        dto = mock.mockWorkspaceTree('')
        fallback = true
      }
    }
    state.folder.root = dto.root || state.workspaceRoot
    state.folder.entries = mapWorkspaceEntries(dto.entries)
    state.folder.byPath = new Map<string, WorkspaceNodeVm>()
    state.folder.expanded = new Set<string>()
    state.folder.loadingPaths = new Set<string>()
    for (const node of state.folder.entries) {
      state.folder.byPath.set(node.key, node)
      for (const repo of node.repos) state.folder.byPath.set(repo.key, repo)
    }
    const treeError = dto.error ?? ''
    // 与四仓树同口径：有错误且无条目 → error 空态；有数据 → 保留列表 + error/truncated 横幅。
    state.folder.status = treeError && state.folder.entries.length === 0 ? 'error' : 'ready'
    state.folder.error = treeError
    state.folder.errorCode = treeError ? dto.errorCode ?? 'E_TREE_READ_FAILED' : ''
    state.folder.truncated = Boolean(dto.truncated)
    state.folder.totalEntries = dto.totalEntries ?? state.folder.entries.length
    state.folder.fallback = fallback
    state.folder.at = Date.now()
  } catch (error) {
    state.folder.status = 'error'
    state.folder.error = error instanceof HostError ? error.message : toErrorMessage(error)
    state.folder.errorCode = error instanceof HostError ? error.code : 'E_IO_ERROR'
  }
}

async function refreshFolder(): Promise<void> {
  state.folder = emptyFolder()
  await ensureFolderLoaded(true)
  notify('文件夹视图已刷新', 'success')
}

/** 文件夹视图物理节点不带 componentId/repoType：按路径首段（仓目录名）反查所属仓。 */
function resolveRepoFromPath(path: string): { componentId: string; repoType: RepoType } | null {
  const segment = path.split('/')[0]?.toLowerCase()
  if (!segment) return null
  for (const component of state.components) {
    for (const repo of component.repos) {
      const name = (repo.repoName || `${component.id}_${repo.repoType}`).toLowerCase()
      if (name === segment) return { componentId: component.id, repoType: repo.repoType }
    }
  }
  return null
}

function selectFolderNode(node: WorkspaceNodeVm): void {
  state.folderSelected = node
  state.selectedPath = node.kind === 'FILE' ? node.path : null
  if (node.kind === 'COMPONENT' && node.componentId) {
    void selectComponent(node.componentId)
    return
  }
  if (node.kind === 'REPO' && node.repoType) {
    if (node.componentId && node.componentId !== state.selectedComponentId) {
      void selectComponent(node.componentId).then(() => {
        state.folderSelected = node
        state.focusedRepoType = node.repoType as RepoType
        state.detail = null
        state.detailStatus = 'loading'
        void loadDetail()
      })
      return
    }
    state.focusedRepoType = node.repoType
    state.detail = null
    state.detailStatus = 'loading'
    void loadDetail()
    return
  }
  // 文件/目录：在文件夹视图中按祖先仓目录反推所属仓（底部按钮 / 右栏绑定同一聚焦仓）
  const owner = resolveRepoFromPath(node.path)
  if (owner) {
    if (owner.componentId !== state.selectedComponentId) {
      void selectComponent(owner.componentId).then(() => {
        state.folderSelected = node
        state.selectedPath = node.path
        state.focusedRepoType = owner.repoType
        void loadDetail()
      })
      return
    }
    state.focusedRepoType = owner.repoType
  }
  void loadDetail()
}

async function toggleFolderNode(node: WorkspaceNodeVm): Promise<void> {
  if (state.folder.expanded.has(node.key)) {
    state.folder.expanded.delete(node.key)
    return
  }
  if (node.kind === 'COMPONENT') {
    if (node.repos.length === 0) return
    state.folder.expanded.add(node.key)
    return
  }
  if (!node.hasChildren) return
  if (!node.loaded) {
    if (state.folder.loadingPaths.has(node.key)) return
    state.folder.loadingPaths.add(node.key)
    try {
      let dto: WorkspaceTreeDto
      if (isMockMode()) {
        dto = mock.mockWorkspaceTree(node.path)
      } else {
        try {
          dto = await api.getWorkspaceTree({ path: node.path, depth: 1 })
        } catch (error) {
          if (!isMethodUnavailable(error)) throw error
          dto = mock.mockWorkspaceTree(node.path)
          state.folder.fallback = true
        }
      }
      node.children = mapWorkspaceEntries(dto.entries)
      for (const child of node.children) {
        state.folder.byPath.set(child.key, child)
        for (const repo of child.repos) state.folder.byPath.set(repo.key, repo)
      }
      node.loaded = true
    } catch (error) {
      node.error = toErrorMessage(error)
      notify(`无法展开：${toErrorMessage(error)}`, 'error', 5000)
      return
    } finally {
      state.folder.loadingPaths.delete(node.key)
    }
  }
  state.folder.expanded.add(node.key)
}

async function openFolderNode(node: WorkspaceNodeVm): Promise<void> {
  if (node.kind === 'FILE') {
    const owner = resolveRepoFromPath(node.path)
    if (owner) state.focusedRepoType = owner.repoType
    if (node.path && isDangerousFile(node.label)) {
      dangerousConfirm(node.path, node.label, 'open', 'workspace')
      return
    }
    await doOpenPath(node.path, node.label, 'open', false, 'workspace')
    return
  }
  if (node.kind === 'REPO' || node.kind === 'DIR' || node.kind === 'UNBOUND_DIR') {
    await doOpenPath(node.path, node.label, 'reveal', false, 'workspace')
  }
}

function setWorkspaceFilter(value: string): void {
  state.search = value
}

// ───────────────────────────── 视图 / 筛选 / 排序 ─────────────────────────────

function setView(view: ViewMode): void {
  if (state.view === view) return
  state.view = view
  try {
    localStorage.setItem(VIEW_STORAGE_KEY, view)
  } catch {
    /* localStorage 不可用时忽略 */
  }
  if (view === 'folder') void ensureFolderLoaded()
  state.overlays.filterOpen = false
}

function setSearch(value: string): void {
  state.search = value
}

function setSort(sortBy: 'severity' | 'id' | 'activity'): void {
  state.sortBy = sortBy
}

function toggleFilterState(value: string): void {
  const at = state.filter.states.indexOf(value)
  if (at >= 0) state.filter.states.splice(at, 1)
  else state.filter.states.push(value)
}

function setFilterRepoType(value: RepoType | null): void {
  state.filter.repoType = value
}

function clearFilter(): void {
  state.filter = { states: [], repoType: null, dirtyOnly: false, unpushedOnly: false }
}

function activeFilterCount(): number {
  return (
    state.filter.states.length +
    (state.filter.repoType ? 1 : 0) +
    (state.filter.dirtyOnly ? 1 : 0) +
    (state.filter.unpushedOnly ? 1 : 0)
  )
}

// ───────────────────────────── 复制 / 菜单命令 ─────────────────────────────

async function copyText(text: string, label = '内容'): Promise<void> {
  try {
    if (navigator.clipboard && navigator.clipboard.writeText) {
      await navigator.clipboard.writeText(text)
    } else {
      const area = document.createElement('textarea')
      area.value = text
      area.style.position = 'fixed'
      area.style.opacity = '0'
      document.body.appendChild(area)
      area.select()
      document.execCommand('copy')
      document.body.removeChild(area)
    }
    notify(`已复制${label}：${text.length > 40 ? `${text.slice(0, 40)}…` : text}`, 'success', 1800)
  } catch {
    notify(`复制失败（浏览器权限）：${text}`, 'warning', 4000)
  }
}

function workspaceRelativePath(absolute: string | null | undefined): string | null {
  if (!absolute || !state.workspaceRoot) return null
  const root = state.workspaceRoot.replace(/\\/g, '/').replace(/\/+$/, '')
  const target = absolute.replace(/\\/g, '/')
  if (target.toLowerCase() === root.toLowerCase()) return ''
  if (!target.toLowerCase().startsWith(`${root.toLowerCase()}/`)) return null
  return target.slice(root.length + 1)
}

function openInExplorer(): void {
  const repo = focusedRepo()
  const component = selectedComponent()
  const absolute =
    repo?.localPath ?? component.repos.find((item) => item.localPath)?.localPath ?? state.workspaceRoot
  const relative = workspaceRelativePath(absolute)
  if (!absolute || relative === null) {
    notify('没有可打开的本路径', 'warning')
    return
  }
  if (isMockMode()) {
    notify(`在资源管理器中打开（Mock）：${absolute}`, 'info', 3600)
    return
  }
  // Host openPath 只接受工作区相对路径；不带 componentId/repoType 时按工作区根解析
  const path = relative || '.'
  void api
    .openPath({ path, mode: 'reveal' })
    .then(() => notify(`已在资源管理器中打开：${absolute}`, 'success'))
    .catch((error: unknown) => notify(`无法打开：${toErrorMessage(error)}`, 'error', 5000))
}

function command(key: string): void {
  state.menuOpen = null
  switch (key) {
    case 'file.openWorkspace':
      notify('打开工作区：由 Host 配置（文件选择器将在后续版本接入）', 'info', 3200)
      break
    case 'file.rescan':
      void refresh()
      break
    case 'file.auth':
      openModal('settings')
      break
    case 'file.exit':
      notify('退出由 Host 窗口处理（Web 预览不可用）', 'info')
      break
    case 'edit.search':
      focusSearch()
      break
    case 'edit.copyPath':
      void copyText(focusedRepo()?.localPath ?? state.workspaceRoot, '路径')
      break
    case 'edit.selectAll':
      selectAllVisible()
      break
    case 'view.workbench':
      setView('workbench')
      break
    case 'view.folder':
      setView('folder')
      break
    case 'view.ignored':
      state.ignoreIgnored = !state.ignoreIgnored
      notify(state.ignoreIgnored ? '显示已忽略文件' : '隐藏已忽略文件', 'info')
      break
    case 'view.compact':
      notify('紧凑密度将在后续版本接入', 'info')
      break
    case 'view.theme':
      toggleTheme()
      break
    case 'actions.PULL':
    case 'actions.FETCH':
    case 'actions.CHKOUT':
    case 'actions.COMMIT':
    case 'actions.PUSH':
      void runAction(key.slice('actions.'.length) as RepoAction)
      break
    case 'component.SYNC':
      requestBatch('SYNC')
      break
    case 'component.CREATE':
      requestBatch('CREATE')
      break
    case 'component.CLONE':
      requestBatch('CLONE')
      break
    case 'component.PULL':
      requestBatch('PULL')
      break
    case 'component.FETCH':
      requestBatch('FETCH')
      break
    case 'component.CHKOUT':
      requestBatch('CHKOUT')
      break
    case 'component.TAG':
      requestBatch('TAG')
      break
    case 'component.RESTORE':
      requestBatch('RESTORE')
      break
    case 'component.DELETE':
      requestBatch('DELETE')
      break
    case 'component.COMMIT':
      requestBatch('COMMIT')
      break
    case 'component.PUSH':
      requestBatch('PUSH')
      break
    case 'tools.ci':
      openModal('ci')
      break
    case 'tools.unbound':
      openModal('unbound')
      break
    case 'tools.logs':
      openModal('logs')
      break
    case 'tools.tasks':
      openDrawer('tasks')
      break
    case 'tools.notifications':
      openDrawer('notifications')
      break
    case 'tools.settings':
      openModal('settings')
      break
    case 'help.about':
      openModal('about')
      break
    case 'help.shortcuts':
      openModal('shortcuts')
      break
    case 'help.update':
      notify('检查更新将在 v1.1 提供', 'info')
      break
    default:
      if (key.startsWith('card.')) {
        if (key === 'card.openWorkbench') setView('workbench')
        else if (key === 'card.detail') openComponentDrawer(state.selectedComponentId)
        else if (key === 'card.locateFolder') {
          setView('folder')
          const node = state.folder.byPath.get(`component:${state.selectedComponentId}`)
          if (node) state.folder.expanded.add(node.key)
        }
        return
      }
      if (key.startsWith('copy.')) {
        if (key === 'copy.componentId') void copyText(state.selectedComponentId, '组件 ID')
        else if (key === 'copy.componentPath') void copyText(state.workspaceRoot, '工作区路径')
        else if (key === 'copy.branch') void copyText(selectedComponent().branch, '分支')
        return
      }
      if (key.startsWith('reveal.component')) {
        openInExplorer()
        return
      }
      if (key.startsWith('tree.reveal')) {
        const [, repoType, ...rest] = key.split(':')
        void doOpenPath(rest.join(':'), rest.join(':').split('/').pop() ?? '', 'reveal', true)
        if (repoType) state.focusedRepoType = repoType as RepoType
        return
      }
      if (key.startsWith('tree.copyPath:')) {
        void copyText(key.slice('tree.copyPath:'.length), '路径')
        return
      }
      if (key.startsWith('tree.copyName:')) {
        void copyText(key.slice('tree.copyName:'.length), '文件名')
        return
      }
      if (key.startsWith('tree.refresh:')) {
        const repoType = key.slice('tree.refresh:'.length) as RepoType
        if (repoType in state.trees) void loadTree(repoType, true)
        return
      }
      if (key === 'close') closeContextMenu()
      else notify(`命令尚未接入：${key}`, 'info')
  }
}

function focusSearch(): void {
  const input = document.querySelector<HTMLInputElement>('[data-testid="title-search"]')
  if (input) {
    input.focus()
    input.select()
  }
}

function closeTopOverlay(): void {
  if (state.overlays.contextMenu) {
    closeContextMenu()
    return
  }
  if (state.overlays.confirmBar) {
    // Esc/全局关闭确认条 = 取消，0 写；焦点由 RepoActionsBar 归还触发按钮。
    cancelConfirmBar()
    return
  }
  if (state.overlays.actionDialog) {
    closeActionDialog()
    return
  }
  if (state.overlays.batchDialog) {
    closeBatchDialog()
    return
  }
  if (state.overlays.confirm) {
    const cancel = state.overlays.confirm.onCancel
    closeConfirm()
    cancel?.()
    return
  }
  if (state.overlays.filterOpen) {
    closeFilter()
    return
  }
  if (state.overlays.modal) {
    closeModal()
    return
  }
  if (state.overlays.componentDrawer) {
    closeComponentDrawer()
    return
  }
  if (state.overlays.drawer) {
    closeDrawer()
    return
  }
  if (state.menuOpen) {
    state.menuOpen = null
    return
  }
  if (state.selectMode) clearChecked()
}

function tickNow(): void {
  state.now = Date.now()
}

async function loadHostInfo(): Promise<void> {
  try {
    state.hostInfo = await api.getHostInfo()
  } catch {
    state.hostInfo = mock.mockHostInfo()
  }
}

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => window.setTimeout(resolve, ms))
}

export interface WorkbenchStore {
  state: WorkbenchState
  init: typeof init
  refresh: typeof refresh
  selectComponent: typeof selectComponent
  loadTree: typeof loadTree
  toggleTreePath: typeof toggleTreePath
  selectTreeNode: typeof selectTreeNode
  focusRepo: typeof focusRepo
  setTreeScroll: typeof setTreeScroll
  loadDetail: typeof loadDetail
  loadCommits: typeof loadCommits
  setTab: typeof setTab
  runAction: typeof runAction
  openNode: typeof openNode
  doOpenPath: typeof doOpenPath
  selectedComponent: typeof selectedComponent
  focusedRepo: typeof focusedRepo
  visibleComponents: typeof visibleComponents
  availability: typeof availability
  unread: typeof unread
  lastOpText: typeof lastOpText
  freshnessText: typeof freshnessText
  sourceLabel: typeof sourceLabel
  sourceTitle: typeof sourceTitle
  isMockMode: typeof isMockMode
  notify: typeof notify
  dismissToast: typeof dismissToast
  markAllRead: typeof markAllRead
  pushNotification: typeof pushNotification
  openDrawer: typeof openDrawer
  closeDrawer: typeof closeDrawer
  openModal: typeof openModal
  closeModal: typeof closeModal
  openConfirm: typeof openConfirm
  closeConfirm: typeof closeConfirm
  openConfirmBar: typeof openConfirmBar
  closeConfirmBar: typeof closeConfirmBar
  cancelConfirmBar: typeof cancelConfirmBar
  acceptConfirmBar: typeof acceptConfirmBar
  openContextMenu: typeof openContextMenu
  closeContextMenu: typeof closeContextMenu
  openFilter: typeof openFilter
  closeFilter: typeof closeFilter
  openActionDialog: typeof openActionDialog
  closeActionDialog: typeof closeActionDialog
  submitActionDialog: typeof submitActionDialog
  openComponentDrawer: typeof openComponentDrawer
  closeComponentDrawer: typeof closeComponentDrawer
  loadComponentDetail: typeof loadComponentDetail
  loadHostActions: typeof loadHostActions
  refreshSnapshot: typeof refreshSnapshot
  toggleTheme: typeof toggleTheme
  cardContextMenu: typeof cardContextMenu
  treeContextMenu: typeof treeContextMenu
  ensureFolderLoaded: typeof ensureFolderLoaded
  refreshFolder: typeof refreshFolder
  toggleFolderNode: typeof toggleFolderNode
  selectFolderNode: typeof selectFolderNode
  openFolderNode: typeof openFolderNode
  workspaceNodeToRepo: typeof workspaceNodeToRepo
  toggleSelectMode: typeof toggleSelectMode
  toggleChecked: typeof toggleChecked
  clearChecked: typeof clearChecked
  selectAllVisible: typeof selectAllVisible
  // M2b 任务中心 / 批量入口
  initJobs: typeof initJobs
  refreshJobs: typeof refreshJobs
  ensureJobsLoaded: typeof ensureJobsLoaded
  activeJobs: typeof activeJobs
  historyJobs: typeof historyJobs
  currentJob: typeof currentJob
  setTasksSegment: typeof setTasksSegment
  selectJob: typeof selectJob
  toggleJobExpanded: typeof toggleJobExpanded
  openTaskCenter: typeof openTaskCenter
  cancelJob: typeof cancelJob
  retryFailed: typeof retryFailed
  // M2b-2 操作审计 / 备份 ref 恢复 / Toast 操作
  refreshOperationLog: typeof refreshOperationLog
  ensureOperationLogLoaded: typeof ensureOperationLogLoaded
  setAuditActionFilter: typeof setAuditActionFilter
  setAuditResultFilter: typeof setAuditResultFilter
  applyAuditFilters: typeof applyAuditFilters
  setAuditPage: typeof setAuditPage
  auditPrevPage: typeof auditPrevPage
  auditNextPage: typeof auditNextPage
  auditPageCount: typeof auditPageCount
  auditPageIndex: typeof auditPageIndex
  auditHasPrev: typeof auditHasPrev
  auditHasNext: typeof auditHasNext
  auditRangeText: typeof auditRangeText
  exportAudit: typeof exportAudit
  openAuditExportDir: typeof openAuditExportDir
  loadBackupRefs: typeof loadBackupRefs
  requestRestoreBackupRef: typeof requestRestoreBackupRef
  executeRestoreBackupRef: typeof executeRestoreBackupRef
  notifyAction: typeof notifyAction
  runToastAction: typeof runToastAction
  requestBatch: typeof requestBatch
  submitBatch: typeof submitBatch
  batchActionState: typeof batchActionState
  batchCommandsBlocked: typeof batchCommandsBlocked
  batchSelectionCount: typeof batchSelectionCount
  batchCommandsHint: typeof batchCommandsHint
  openBatchDialog: typeof openBatchDialog
  closeBatchDialog: typeof closeBatchDialog
  submitBatchDialog: typeof submitBatchDialog
  setView: typeof setView
  setSearch: typeof setSearch
  setSort: typeof setSort
  setWorkspaceFilter: typeof setWorkspaceFilter
  toggleFilterState: typeof toggleFilterState
  setFilterRepoType: typeof setFilterRepoType
  clearFilter: typeof clearFilter
  activeFilterCount: typeof activeFilterCount
  copyText: typeof copyText
  openInExplorer: typeof openInExplorer
  command: typeof command
  focusSearch: typeof focusSearch
  closeTopOverlay: typeof closeTopOverlay
  tickNow: typeof tickNow
  loadHostInfo: typeof loadHostInfo
}

export const workbench: WorkbenchStore = {
  state,
  init,
  refresh,
  selectComponent,
  loadTree,
  toggleTreePath,
  selectTreeNode,
  focusRepo,
  setTreeScroll,
  loadDetail,
  loadCommits,
  setTab,
  runAction,
  openNode,
  doOpenPath,
  selectedComponent,
  focusedRepo,
  visibleComponents,
  availability,
  unread,
  lastOpText,
  freshnessText,
  sourceLabel,
  sourceTitle,
  isMockMode,
  notify,
  dismissToast,
  markAllRead,
  pushNotification,
  openDrawer,
  closeDrawer,
  openModal,
  closeModal,
  openConfirm,
  closeConfirm,
  openConfirmBar,
  closeConfirmBar,
  cancelConfirmBar,
  acceptConfirmBar,
  openContextMenu,
  closeContextMenu,
  openFilter,
  closeFilter,
  openActionDialog,
  closeActionDialog,
  submitActionDialog,
  openComponentDrawer,
  closeComponentDrawer,
  loadComponentDetail,
  loadHostActions,
  refreshSnapshot,
  toggleTheme,
  cardContextMenu,
  treeContextMenu,
  ensureFolderLoaded,
  refreshFolder,
  toggleFolderNode,
  selectFolderNode,
  openFolderNode,
  workspaceNodeToRepo,
  toggleSelectMode,
  toggleChecked,
  clearChecked,
  selectAllVisible,
  initJobs,
  refreshJobs,
  ensureJobsLoaded,
  activeJobs,
  historyJobs,
  currentJob,
  setTasksSegment,
  selectJob,
  toggleJobExpanded,
  openTaskCenter,
  cancelJob,
  retryFailed,
  refreshOperationLog,
  ensureOperationLogLoaded,
  setAuditActionFilter,
  setAuditResultFilter,
  applyAuditFilters,
  setAuditPage,
  auditPrevPage,
  auditNextPage,
  auditPageCount,
  auditPageIndex,
  auditHasPrev,
  auditHasNext,
  auditRangeText,
  exportAudit,
  openAuditExportDir,
  loadBackupRefs,
  requestRestoreBackupRef,
  executeRestoreBackupRef,
  notifyAction,
  runToastAction,
  requestBatch,
  submitBatch,
  batchActionState,
  batchCommandsBlocked,
  batchSelectionCount,
  batchCommandsHint,
  openBatchDialog,
  closeBatchDialog,
  submitBatchDialog,
  setView,
  setSearch,
  setSort,
  setWorkspaceFilter,
  toggleFilterState,
  setFilterRepoType,
  clearFilter,
  activeFilterCount,
  copyText,
  openInExplorer,
  command,
  focusSearch,
  closeTopOverlay,
  tickNow,
  loadHostInfo,
}

export function useWorkbench(): WorkbenchStore {
  return workbench
}

// 供测试/调试读取 Mock workspaceRoot
export const mockWorkspaceRoot = mock.MOCK_WORKSPACE_ROOT
