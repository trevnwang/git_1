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
  toErrorMessage,
} from '../api'
import type { CommitDto, HostInfoDto, RepoTreeDto, SnapshotDto, WorkspaceTreeDto } from '../api'
import * as mock from '../mocks/workbenchMock'
import { REPO_ACTIONS } from '../types/workbench'
import {
  actionAvailability,
  emptyComponentVm,
  filterComponents,
  formatAgo,
  formatClock,
  isDangerousFile,
  isSafeFile,
  mapComponent,
  mapDetail,
  mapTreeNodes,
  mapWorkspaceEntries,
  pickDefaultRepo,
  repoOf,
  sortComponents,
  sortTreeNodes,
} from '../viewmodel'
import type {
  CommitVm,
  ComponentVm,
  ConfirmVm,
  DrawerKind,
  FilterState,
  FolderTreeState,
  InfoTab,
  ModalKind,
  OperationVm,
  RepoAction,
  RepoDetailVm,
  RepoTreePanelState,
  RepoType,
  RepoVm,
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
  items: { key: string; label: string; danger?: boolean; disabled?: boolean; separator?: boolean }[]
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
  online: boolean
  ignoreIgnored: boolean
  compact: boolean
  theme: 'system' | 'light' | 'dark'
  overlays: {
    drawer: DrawerKind | null
    modal: ModalKind | null
    filterOpen: boolean
    confirm: ConfirmVm | null
    contextMenu: ContextMenuState | null
  }
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
    fallback: false,
    at: 0,
  }
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
    lastOp: { action: '—', scope: '', state: 'idle', at: new Date().toISOString() },
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
  online: true,
  ignoreIgnored: false,
  compact: false,
  theme: 'system',
  overlays: {
    drawer: null,
    modal: null,
    filterOpen: false,
    confirm: null,
    contextMenu: null,
  },
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
  const repo = repoOf(component, state.focusedRepoType)
  if (!repo || repo.state === 'MISSING') return null
  return repo
}

function visibleComponents(): ComponentVm[] {
  return sortComponents(filterComponents(state.components, state.search, state.filter), state.sortBy)
}

function availability() {
  const hostReadOnly = state.hostLinked && state.hostInfo !== null && state.hostInfo.features?.repoActions !== true
  if (hostReadOnly) {
    const disabled = { ...{} } as Record<RepoAction, { enabled: false; reason: string; confirm: boolean }>
    for (const action of REPO_ACTIONS) {
      disabled[action] = { enabled: false, reason: `M1 只读：${action} 将在 M2 接入（需备份/确认）`, confirm: false }
    }
    return disabled
  }
  return actionAvailability(focusedRepo(), state.action.pending)
}

function unread(): number {
  return state.notifications.filter((item) => !item.read).length
}

function lastOpText(): string {
  const op = state.action.lastOp
  if (op.state === 'idle') return '就绪 · 无最近操作'
  const clock = formatClock(op.at)
  const scope = op.scope ? ` · ${op.scope}` : ''
  if (op.state === 'running') return `${op.action} 执行中${scope} · ${clock}`
  if (op.state === 'success') return `✓ ${op.action} 成功${scope} · ${clock}`
  return `✕ ${op.action} 失败${scope} · ${op.code ?? 'E_UNKNOWN'} · ${clock}`
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
}

function closeDrawer(): void {
  state.overlays.drawer = null
}

function openModal(kind: ModalKind): void {
  state.overlays.modal = kind
  if (kind === 'ci' && state.hostLinked && !state.mockFallback) void refreshCi()
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

function applySnapshot(snapshot: SnapshotDto, options: { mockFallback: boolean; reason?: string }): void {
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
  state.snapshotEmpty = components.length === 0
  state.hostFailed = false
  snapshotRetryIndex = 0
  if (snapshotRetryTimer !== undefined) {
    clearTimeout(snapshotRetryTimer)
    snapshotRetryTimer = undefined
  }

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
    return
  }
  notify('正在连接 Host 并扫描本地仓库…（首次可能 10–20s）')
  await loadInitialSnapshot()
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
  await Promise.all([loadDetail(), ...REPO_TYPES.map((type) => loadTree(type))])
  if (state.view === 'folder') void ensureFolderLoaded()
}

async function selectComponent(id: string): Promise<void> {
  if (!id || state.selectedComponentId === id) {
    if (id) await loadActive()
    return
  }
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
  panel.status = 'ready'
  panel.error = ''
  panel.errorCode = ''
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
    panel.error = repo?.state === 'MISSING' ? '仓库未克隆（MISSING）' : '该仓不在当前快照中'
    panel.errorCode = repo?.state === 'MISSING' ? 'E_REPO_NOT_LOCAL' : 'E_NOT_FOUND'
    return
  }
  if (repo.state === 'INVALID') {
    panel.status = 'unavailable'
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
    panel.error = toErrorMessage(error)
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
  state.focusedRepoType = repoType
  state.selectedPath = path
  if (changedRepo || path === null) void loadDetail()
}

function focusRepo(repoType: RepoType): void {
  if (state.focusedRepoType === repoType) return
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
  const repo = focusedRepo()
  if (!repo) {
    notify('请先在文件树中选择仓库', 'warning')
    return
  }
  const availabilityMap = availability()
  const cell = availabilityMap[action]
  if (!cell.enabled) {
    notify(cell.reason, 'warning', 4000)
    return
  }
  state.action.pending = action
  state.action.lastOp = {
    action,
    scope: repo.repoType,
    state: 'running',
    at: new Date().toISOString(),
  }
  const component = selectedComponent()
  try {
    await delay(360)
    if (isMockMode()) {
      // Mock 桥与 M1 后端一致：返回 E_NOT_IMPLEMENTED
      mock.mockRunRepoAction(action)
    }
    const result = await api.runRepoAction({
      componentId: component.id,
      repoType: repo.repoType,
      action,
    })
    if (!result.accepted) {
      throw new HostError(result.errorCode ?? 'E_ACTION_NOT_ALLOWED', result.message ?? '操作被拒绝')
    }
    state.action.lastOp = {
      action,
      scope: repo.repoType,
      state: 'success',
      at: new Date().toISOString(),
      message: result.jobId,
    }
    notify(`✓ ${action} 已受理（M2 job ${result.jobId ?? '—'}）`, 'success')
    void loadTree(state.focusedRepoType, true)
    void loadDetail()
  } catch (error) {
    const code = error instanceof HostError ? error.code : 'E_UNKNOWN'
    state.action.lastOp = {
      action,
      scope: repo.repoType,
      state: 'error',
      at: new Date().toISOString(),
      code,
    }
    if (code === 'E_NOT_IMPLEMENTED' || isMethodUnavailable(error)) {
      notify('M1 只读，M2 接入', 'warning', 3600)
      pushNotification(`${action} 未执行`, `M1 只读：${action} 将在 M2 启用（${repo.repoType}）`, 'warn')
    } else {
      notify(`操作失败：${toErrorMessage(error)}`, 'error', 6000)
      pushNotification(`${action} 失败`, toErrorMessage(error), 'err')
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

function batchCommand(command: string): void {
  const count = state.checkedIds.length || 1
  const repoCount = state.checkedIds.length * 4 || 4
  if ((count > 10 || repoCount > 40) && command === 'DELETE') {
    openConfirm({
      title: `批量 ${command} 确认`,
      body: `将对 ${count} 个组件 / ${repoCount} 个仓执行 ${command}；远程仓库不受影响，本地文件进入 .trash 可恢复。`,
      confirmLabel: '确认执行',
      danger: true,
      onConfirm: () => {
        closeConfirm()
        notify(`批量 ${command}：M2 接入`, 'warning', 3600)
      },
    })
    return
  }
  void count
  notify(`批量 ${command}（${state.checkedIds.length || '当前'} 个组件）：M2 接入`, 'warning', 3600)
}

// ───────────────────────────── 右键菜单 ─────────────────────────────

function cardContextMenu(event: MouseEvent, componentId: string): void {
  if (state.selectedComponentId !== componentId) void selectComponent(componentId)
  const component = selectedComponent()
  const hasMissing = component.repos.some((repo) => repo.state === 'MISSING')
  openContextMenu(event.clientX, event.clientY, [
    { key: 'card.openWorkbench', label: '在工作台打开' },
    { key: 'card.locateFolder', label: '在文件夹视图定位' },
    { key: '-', label: '', separator: true },
    { key: 'component.SYNC', label: 'SYNC…（四仓，远程覆盖本地）', danger: true },
    { key: 'component.CREATE', label: 'CREATE…（四仓建同名分支）' },
    { key: 'component.CLONE', label: 'Clone 缺失仓…', disabled: !hasMissing },
    { key: 'component.DELETE', label: 'Delete…（仅本地，软删到 .trash）', danger: true },
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
    state.folder.status = 'ready'
    state.folder.fallback = fallback
    state.folder.at = Date.now()
  } catch (error) {
    state.folder.status = 'error'
    state.folder.error = toErrorMessage(error)
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
      notify('打开工作区：由 Host 配置（M2 接入文件选择器）', 'info', 3200)
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
      notify('紧凑密度将在 M2 接入', 'info')
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
      batchCommand('SYNC')
      break
    case 'component.CREATE':
      batchCommand('CREATE')
      break
    case 'component.CLONE':
      batchCommand('CLONE')
      break
    case 'component.DELETE':
      batchCommand('DELETE')
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
  if (state.overlays.confirm) {
    closeConfirm()
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
  openContextMenu: typeof openContextMenu
  closeContextMenu: typeof closeContextMenu
  openFilter: typeof openFilter
  closeFilter: typeof closeFilter
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
  batchCommand: typeof batchCommand
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
  openContextMenu,
  closeContextMenu,
  openFilter,
  closeFilter,
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
  batchCommand,
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
