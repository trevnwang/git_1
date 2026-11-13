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
}

export interface OperationVm {
  action: RepoAction | string
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
}

export interface MenuItemVm {
  key: string
  label: string
  shortcut?: string
  disabled?: boolean
  danger?: boolean
  separator?: boolean
  stage?: string
}

export interface ConfirmVm {
  title: string
  body: string
  detail?: string
  confirmLabel: string
  danger: boolean
  onConfirm: () => void
}

export interface FilterState {
  states: string[]
  repoType: RepoType | null
  dirtyOnly: boolean
  unpushedOnly: boolean
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
