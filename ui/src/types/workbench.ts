/**
 * 工作台 UI 视图模型类型（spec §9.2 / code-plan §5.3）。
 * 所有 DTO → VM 映射集中在 viewmodel.ts，组件只消费这里的类型。
 */

export type RepoType = 'DESIGN' | 'IMPL' | 'MIL' | 'SIL'
export type ViewMode = 'workbench' | 'folder'
export type CiState = 'PASS' | 'FAIL' | 'RUNNING' | 'QUEUED' | 'NONE' | 'UNKNOWN'
export type Tone =
  | 'success'
  | 'warning'
  | 'info'
  | 'purple'
  | 'cyan'
  | 'neutral'
  | 'danger'
  | 'hatched'
export type RepoAction = 'PULL' | 'FETCH' | 'CHKOUT' | 'COMMIT' | 'PUSH'
export type OperationState = 'idle' | 'running' | 'success' | 'error'
export type InfoTab = 'overview' | 'commits' | 'diff' | 'ci'
export type DrawerKind = 'tasks' | 'notifications'
export type ModalKind =
  | 'ci'
  | 'settings'
  | 'logs'
  | 'unbound'
  | 'components'
  | 'about'
  | 'shortcuts'

export const REPO_TYPES: readonly RepoType[] = ['DESIGN', 'IMPL', 'MIL', 'SIL']
export const REPO_ACTIONS: readonly RepoAction[] = ['PULL', 'FETCH', 'CHKOUT', 'COMMIT', 'PUSH']

export interface RepoVm {
  repoType: RepoType
  repoName: string
  state: string
  tone: Tone
  branch: string
  tag: string | null
  headSha: string | null
  ahead: number
  behind: number
  dirtyCount: number
  untrackedCount: number
  stagedCount: number
  stashCount: number
  isLocal: boolean
  localPath: string | null
  compareRef: string | null
  calcAt: string | null
  dataSource: string | null
  isStale: boolean
  errorCode: string | null
  flags: string[]
  ci: CiState
}

export interface ComponentVm {
  id: string
  state: string
  tone: Tone
  severityRank: number
  branch: string
  tag: string | null
  mixedBranch: boolean
  dirtyTotal: number
  untrackedTotal: number
  starred: boolean
  repos: RepoVm[]
  ci: CiState
  lastActivityAt: string | null
  incomplete: boolean
}

export type GitStatus =
  | 'NONE'
  | 'MODIFIED'
  | 'ADDED'
  | 'DELETED'
  | 'UNTRACKED'
  | 'CONFLICT'

export interface TreeNodeVm {
  path: string
  name: string
  kind: 'DIR' | 'FILE'
  hasChildren: boolean
  size: number | null
  mtime: string | null
  gitStatus: GitStatus
  submodule: boolean
  ignored: boolean
  error: string | null
  loaded: boolean
  children: TreeNodeVm[]
}

export interface CommitVm {
  sha: string
  shortSha: string
  message: string
  author: string
  at: string
  refs: string[]
}

export interface RepoDetailVm {
  repo: RepoVm | null
  commits: CommitVm[]
  commitTotal: number
  errorCode: string | null
}

export interface RepoTreePanelState {
  repoType: RepoType
  status: 'idle' | 'loading' | 'ready' | 'error' | 'unavailable'
  entries: TreeNodeVm[]
  byPath: Map<string, TreeNodeVm>
  expanded: Set<string>
  loadingPaths: Set<string>
  loadedPaths: Set<string>
  error: string
  errorCode: string
  truncated: boolean
  totalEntries: number
  scrollTop: number
  fallback: boolean
  at: number
}

export interface FolderTreeState {
  status: 'idle' | 'loading' | 'ready' | 'error' | 'unavailable'
  root: string
  entries: WorkspaceNodeVm[]
  byPath: Map<string, WorkspaceNodeVm>
  expanded: Set<string>
  loadingPaths: Set<string>
  error: string
  errorCode: string
  truncated: boolean
  totalEntries: number
  fallback: boolean
  at: number
}

export interface WorkspaceNodeVm {
  key: string
  kind: 'ROOT' | 'COMPONENT' | 'REPO' | 'DIR' | 'FILE' | 'UNBOUND_GROUP' | 'UNBOUND_DIR'
  label: string
  path: string
  hasChildren: boolean
  componentId: string | null
  repoType: RepoType | null
  state: string
  tone: Tone
  branch: string | null
  ahead: number
  behind: number
  dirtyCount: number
  gitStatus: GitStatus
  size: number | null
  mtime: string | null
  count: number
  error: string | null
  repos: WorkspaceNodeVm[]
  loaded: boolean
  children: WorkspaceNodeVm[]
}

export interface CommitPage {
  total: number
  items: CommitVm[]
  errorCode: string | null
}

export interface ActionAvailability {
  enabled: boolean
  reason: string
  confirm: boolean
  /** 禁用原因对应的 Host 错误码（FR-ACT-02：禁用 tooltip 必须含错误码）。 */
  code?: string
  /** 替代动作（FR-ACT-02：禁用 tooltip 必须给出可执行的下一步）。 */
  alt?: string
}

/** Host getComponentDetail.repos[].actions 的单键可用性（M2a 单源矩阵）。 */
export interface HostActionAvailability {
  action: RepoAction
  enabled: boolean
  /** Host 原因码（STATE_BLOCKED / CONFIRM_REQUIRED / AVAILABLE / E_*）。 */
  reasonCode: string
  /** Host 原因文案（禁用时作为 tooltip「原因：…」）。 */
  reason: string
  /** 是否需二次确认（仅由 Host 输出，UI 不自行判定）。 */
  requiresConfirm: boolean
}

/** Host actions 读取状态：未就绪时五键禁用并提示「正在读取 Host 能力」。 */
export type HostActionsStatus = 'idle' | 'loading' | 'ready' | 'error'

/** Host actions 数据来源（Host getComponentDetail / Mock 测试矩阵）。 */
export type HostActionsSource = 'host' | 'mock'

export interface OperationVm {
  action: RepoAction | string
  /** 操作所属组件（FR-OPS-05：状态栏模板 `<componentId>/<RepoType>`）。 */
  componentId?: string
  /** 操作所属仓类型（状态栏模板 `<componentId>/<RepoType>` 的 RepoType 段）。 */
  repoType?: string
  scope: string
  state: OperationState
  at: string
  code?: string
  message?: string
}

export interface ToastVm {
  id: number
  text: string
  tone: 'info' | 'success' | 'warning' | 'error'
  ttl: number
  actionLabel?: string
  /** 操作按钮回调（如审计导出后的「打开所在目录」）。 */
  onAction?: () => void
}

export interface MenuItemVm {
  key: string
  label: string
  shortcut?: string
  disabled?: boolean
  danger?: boolean
  separator?: boolean
  stage?: string
  /** 悬停说明（禁用原因等）。 */
  title?: string
}

export interface ConfirmVm {
  title: string
  body: string
  detail?: string
  confirmLabel: string
  danger: boolean
  onConfirm: () => void
  /** 点“取消”/Esc/遮罩关闭时回调（可选）。 */
  onCancel?: () => void
}

/**
 * F 区按钮上方的内联确认条（FR-ACT-04 / AC-UI-10a/10b）。
 * COMMIT/PUSH 唯一写入口：未点「确认」不调用 runRepoAction；不遮挡/不移动 5 键。
 */
export interface ConfirmBarVm {
  action: 'COMMIT' | 'PUSH'
  /** 主文案：COMMIT「将提交 N 个文件（已暂存 M）」；PUSH「将推送 N 个提交（分支 → upstream）」。 */
  text: string
  /** 次要说明：NO_UPSTREAM 的发布分支、stageAll 影响等。 */
  detail?: string
  confirmLabel: string
  danger: boolean
  onAccept: () => void
  onCancel?: () => void
}

// ───────────────────────────── M2 单仓动作对话框 ─────────────────────────────

export type ActionDialogKind = 'commit' | 'checkout' | 'confirm'

/** CHKOUT 选择器候选 ref（M2 P0-7：来自 Host getRemoteRefs，无 Host 时回退当前分支/Tag）。 */
export interface ActionDialogRefVm {
  /** 完整 ref 名（如 refs/heads/main、refs/remotes/origin/x、refs/tags/v1）。 */
  refName: string
  /** 短 SHA（搜索与展示）。 */
  shortSha: string
  kind: 'LOCAL_BRANCH' | 'REMOTE_BRANCH' | 'TAG'
}

/** CHKOUT refs 加载状态：loading / ready（Host）/ fallback（Mock 或接口不可用）/ error。 */
export type ActionDialogRefsStatus = 'loading' | 'ready' | 'fallback' | 'error'

export interface ActionDialogPayload {
  message?: string
  refKind?: 'branch' | 'tag'
  refValue?: string
  /** COMMIT：显式勾选「暂存全部改动（git add -A）」时才为 true。 */
  stageAll?: boolean
  /** CHKOUT：勾选「检出前自动 stash（-u）」时为 true（Host 先 stash -u 再检出）。 */
  stash?: boolean
  /** CHKOUT：提交校验失败重开对话框时保留搜索词。 */
  search?: string
}

export interface ActionDialogVm {
  kind: ActionDialogKind
  action: RepoAction
  title: string
  body: string
  detail?: string
  confirmLabel: string
  danger: boolean
  /** 只读摘要行（PULL 的 behind / PUSH 的 branch+ahead 等）。 */
  info?: { label: string; value: string; tone?: 'muted' | 'warning' | 'danger' }[]
  initialMessage?: string
  initialRefKind?: 'branch' | 'tag'
  initialRefValue?: string
  /** CHKOUT 的候选 ref（Host getRemoteRefs；空/不可用时为当前分支/Tag 建议）。 */
  refs?: ActionDialogRefVm[]
  /** CHKOUT refs 加载状态（loading 时对话框显示骨架文案）。 */
  refsStatus?: ActionDialogRefsStatus
  /** CHKOUT refs 加载失败/回退原因（内联展示，不打断操作）。 */
  refsError?: string
  /** CHKOUT refs 是否被 Host 截断（提示用搜索缩小范围）。 */
  refsTruncated?: boolean
  /** CHKOUT 重开时恢复的搜索词。 */
  initialSearch?: string
  /** CHKOUT：工作区是否有未提交改动（dirty/untracked），决定是否显示 stash 勾选。 */
  showStash?: boolean
  dirtyTotal?: number
  /** COMMIT：已暂存/未提交计数（用于「已暂存 N / 将提交 N」与 stageAll 影响说明）。 */
  stagedCount?: number
  dirtyCount?: number
  untrackedCount?: number
  /** COMMIT：是否显示「暂存全部改动（git add -A）」checkbox。 */
  showStageAll?: boolean
  submit: (payload: ActionDialogPayload) => void
}

/** 组件详情抽屉的数据（优先 Host getComponentDetail，失败回退快照）。 */
export interface ComponentDetailState {
  componentId: string
  status: 'idle' | 'loading' | 'ready' | 'error'
  component: ComponentVm | null
  source: 'host' | 'snapshot'
  error: string
}

export interface FilterState {
  states: string[]
  repoType: RepoType | null
  dirtyOnly: boolean
  unpushedOnly: boolean
}

// ───────────────────────────── M2b 任务中心 / 批量入口 ─────────────────────────────

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

export type JobState =
  | 'PENDING'
  | 'RUNNING'
  | 'PARTIAL'
  | 'SUCCESS'
  | 'FAILED'
  | 'CANCELED'
  | 'INTERRUPTED'
  | 'PREVIEW'

export type JobItemState =
  | 'PENDING'
  | 'RUNNING'
  /** Host 契约用 OK 表示单仓成功（兼容历史 SUCCESS）。 */
  | 'OK'
  | 'SUCCESS'
  | 'FAILED'
  | 'SKIPPED'
  | 'CANCELED'
  | 'INTERRUPTED'
  /** dryRun 预览项：只做只读预检、仓库零写。 */
  | 'PREVIEW'

export interface JobItemVm {
  componentId: string
  repoType: string
  repoName: string
  state: JobItemState
  startedAt: string | null
  finishedAt: string | null
  errorCode: string | null
  message: string | null
  preHead: string | null
  postHead: string | null
  backupRef: string | null
  /** dryRun 预览项（只读预检、零写）。 */
  preview?: boolean
}

export interface JobVm {
  id: string
  kind: JobKind
  state: JobState
  total: number
  ok: number
  failed: number
  skipped: number
  createdAt: string
  startedAt: string | null
  finishedAt: string | null
  cancellable: boolean
  items: JobItemVm[]
  /** 进行中（PENDING/RUNNING）：任务中心按此分段，数量同步到状态栏队列。 */
  active: boolean
  /**
   * dryRun 预览作业（未执行任何写操作）：来自提交时 args.dryRun=true 的本地记录，
   * 或由「全部 SKIPPED + DRY_RUN 消息」识别（历史/重启后仍可识别）。
   */
  dryRun?: boolean
  /** 事件信封合成的部分快照（无完整 items/kind）：合并时保留上一份明细。 */
  partial?: boolean
}

/** 任务中心分段：进行中 / 历史。 */
export type TasksSegment = 'active' | 'history'

/** 批量入口动作（runBatch.kind 的 UI 子集；完整清单见 viewmodel.BATCH_ACTIONS）。 */
export type BatchKind =
  | 'SYNC'
  | 'CREATE'
  | 'CLONE'
  | 'DELETE'
  | 'TAG'
  | 'RESTORE'
  | 'PULL'
  | 'FETCH'
  | 'CHKOUT'
  | 'COMMIT'
  | 'PUSH'

/** 批量参数对话框（CREATE/TAG/CHKOUT 输入 branch/tag；CLONE 预览缺失仓；可选 dryRun）。 */
export interface BatchDialogVm {
  kind: 'branch' | 'tag' | 'clone' | 'chkout'
  action: BatchKind
  title: string
  body: string
  detail?: string
  confirmLabel: string
  danger: boolean
  inputLabel?: string
  inputPlaceholder?: string
  initialValue?: string
  /** 允许「仅预览（dryRun）」勾选（Host 不支持时忽略）。 */
  showDryRun?: boolean
  dryRunHint?: string
  /** CLONE 预览：缺失仓列表（componentId + repoType）。 */
  preview?: { componentId: string; repoType: string; label: string }[]
  submit: (payload: { value?: string; dryRun?: boolean }) => void
}

export interface BatchTargetsVm {
  componentIds: string[]
  repoTypes?: string[]
}

export interface HostStatusVm {
  linked: boolean
  scanning: boolean
  failed: boolean
  fallbackReason: string
  mockFallback: boolean
}

export interface FreshnessVm {
  localAt: string | null
  remoteAt: string | null
  localStale: boolean
  remoteStale: boolean
}

// ───────────────────────────── M2b-2 操作审计 / 备份 ref / 离线 ─────────────────────────────

/** 审计行（GET operationLog 的 UI 视图模型；字段与 Host 契约一一对应）。 */
export interface OperationLogItemVm {
  id: string
  ts: string
  actor: string
  action: string
  componentId: string
  repoType: string
  result: string
  errorCode: string | null
  requestId: string | null
  jobId: string | null
  jobItemId: string | null
  backupRef: string | null
  beforeHead: string | null
  afterHead: string | null
}

/** 审计查询过滤条件（UI 只暴露 action/result；时间/组件由调用方可选传入）。 */
export interface AuditFiltersVm {
  action: string
  result: string
}

/** 可恢复备份 ref（getBackupRefs 的 UI 视图模型）。 */
export interface BackupRefVm {
  ref: string
  sha: string
  createdAt: string
  repoType: string
  branch: string | null
  format: string
  kind: string
}

/** 离线判定结果（source=local 且存在远程错误/离线标记时 offline=true）。 */
export interface OfflineStateVm {
  offline: boolean
  /** 离线原因（Host error/远程错误/远程数据过期；用于顶部提示与 tooltip）。 */
  reason: string
}

