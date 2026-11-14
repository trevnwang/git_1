/**
 * 内置 Mock 数据（无 Host / 浏览器 CDP / Host 方法未实现时回退）。
 * 覆盖 7 个组件（SYNCED/BEHIND/DIVERGED/DIRTY/DETACHED/MISSING/UNKNOWN）、
 * 4 仓文件树、仓库详情/提交、工作区树、CI 占位与 HostInfo。
 * 数据来源标注 source='mock'，UI 数据源胶囊常驻「Mock 数据」。
 */
import { HostError } from '../api'
import type {
  BatchArgs,
  CiStatusDto,
  CommitDto,
  ComponentDto,
  HostInfoDto,
  JobDto,
  JobItemDto,
  OpenPathResultDto,
  RepoDetailDto,
  RepoDto,
  RepoTreeDto,
  SnapshotDto,
  TreeNodeDto,
  WorkspaceNodeDto,
  WorkspaceTreeDto,
} from '../api'
import type { GitStatus, HostActionAvailability, RepoAction, RepoType } from '../types/workbench'
import { REPO_ACTIONS, REPO_TYPES } from '../types/workbench'

export const MOCK_WORKSPACE_ROOT = 'D:\\Work\\MBD'

interface MockRepo {
  type: RepoType
  state: string
  branch: string
  tag?: string | null
  ahead?: number
  behind?: number
  dirty?: number
  untracked?: number
  head?: string
  ci?: string
}

interface MockComponent {
  id: string
  state: string
  severity: number
  branch: string
  tag?: string | null
  mixed?: boolean
  dirtyTotal?: number
  repos: MockRepo[]
  lastActivityAt?: string
}

const HEAD_A = '3f2a9c1d4b5e6f7a8b9c0d1e2f3a4b5c6d7e8f90'
const HEAD_B = '7c2e9c1a5d8f3b0e6a4c2d9f1b7e3a5c8d0f2b46'
const HEAD_C = 'b41d08e7a2c5f9d3e6b0a8c4f1d7e9b5a3c6f024'
const HEAD_D = '9e8d7c6b5a4f3e2d1c0b9a8f7e6d5c4b3a2f1e0d'

const MOCK_COMPONENTS: MockComponent[] = [
  {
    id: 'CF325F_ChanganSigProcgBascFct',
    state: 'SYNCED',
    severity: 14,
    branch: 'main',
    tag: 'v1.2.0',
    repos: REPO_TYPES.map((type) => ({ type, state: 'SYNCED', branch: 'main', head: HEAD_A })),
  },
  {
    id: 'CF123A_ShortName',
    state: 'BEHIND',
    severity: 6,
    branch: 'main',
    tag: 'v1.1.3',
    repos: [
      { type: 'DESIGN', state: 'BEHIND', branch: 'main', behind: 2, head: HEAD_B },
      { type: 'IMPL', state: 'BEHIND', branch: 'main', behind: 3, head: HEAD_B },
      { type: 'MIL', state: 'SYNCED', branch: 'main', head: HEAD_B },
      { type: 'SIL', state: 'SYNCED', branch: 'main', head: HEAD_B },
    ],
  },
  {
    id: 'CF900A_TractionCtrl',
    state: 'DIVERGED',
    severity: 4,
    branch: 'feat-x',
    mixed: true,
    repos: [
      { type: 'DESIGN', state: 'DIVERGED', branch: 'feat-x', ahead: 2, behind: 1, head: HEAD_C },
      { type: 'IMPL', state: 'AHEAD', branch: 'feat-x', ahead: 1, head: HEAD_C },
      { type: 'MIL', state: 'SYNCED', branch: 'main', head: HEAD_C },
      { type: 'SIL', state: 'BEHIND', branch: 'main', behind: 2, head: HEAD_C },
    ],
  },
  {
    id: 'CF777B_BatteryMgmt',
    state: 'DIRTY',
    severity: 8,
    branch: 'dev',
    dirtyTotal: 3,
    repos: [
      { type: 'DESIGN', state: 'DIRTY', branch: 'dev', dirty: 1, head: HEAD_D },
      { type: 'IMPL', state: 'DIRTY', branch: 'dev', dirty: 2, untracked: 1, head: HEAD_D },
      { type: 'MIL', state: 'SYNCED', branch: 'dev', head: HEAD_D },
      { type: 'SIL', state: 'AHEAD', branch: 'dev', ahead: 2, head: HEAD_D },
    ],
  },
  {
    id: 'CF555C_Gateway',
    state: 'DETACHED',
    severity: 10,
    branch: '—',
    tag: 'v2.0.0',
    repos: [
      { type: 'DESIGN', state: 'DETACHED_TAG', branch: '—', tag: 'v2.0.0', head: HEAD_B },
      { type: 'IMPL', state: 'DETACHED_COMMIT', branch: '—', head: HEAD_B },
      { type: 'MIL', state: 'SYNCED', branch: 'main', head: HEAD_B },
      { type: 'SIL', state: 'SYNCED', branch: 'main', head: HEAD_B },
    ],
  },
  {
    id: 'CF111D_BrakeCtrl',
    state: 'MISSING',
    severity: 12,
    branch: '—',
    repos: [
      { type: 'DESIGN', state: 'MISSING', branch: '—' },
      { type: 'IMPL', state: 'MISSING', branch: '—' },
      { type: 'MIL', state: 'MISSING', branch: '—' },
      { type: 'SIL', state: 'MISSING', branch: '—' },
    ],
  },
  {
    id: 'CF001E_VehicleState',
    state: 'UNKNOWN',
    severity: 2,
    branch: 'main',
    repos: [
      { type: 'DESIGN', state: 'UNKNOWN', branch: 'main' },
      { type: 'IMPL', state: 'SYNCED', branch: 'main', head: HEAD_A },
      { type: 'MIL', state: 'SYNCED', branch: 'main', head: HEAD_A },
      { type: 'SIL', state: 'SYNCED', branch: 'main', head: HEAD_A },
    ],
  },
]

/** Mock 组件仓库的相对目录名（文件夹视图用）：`<componentId>_<Design|Impl|MIL|SIL>`。 */
export function mockRepoDir(componentId: string, type: RepoType): string {
  const suffix = type === 'DESIGN' ? 'Design' : type === 'IMPL' ? 'Impl' : type
  return `${componentId}_${suffix}`
}

function repoName(componentId: string, type: RepoType): string {
  return mockRepoDir(componentId, type)
}

function nowIso(): string {
  return new Date().toISOString()
}

function buildRepo(dto: MockRepositoryComponent, repo: MockRepo): RepoDto {
  const local = repo.state !== 'MISSING'
  return {
    repoType: repo.type,
    repoName: repoName(dto.id, repo.type),
    state: repo.state,
    subType: null,
    branch: repo.branch === '—' ? null : repo.branch,
    tag: repo.tag ?? null,
    headSha: repo.head ?? null,
    upstreamRef: repo.branch === '—' ? null : `refs/remotes/origin/${repo.branch}`,
    compareRef: repo.branch === '—' ? null : `refs/remotes/origin/${repo.branch}`,
    ahead: repo.ahead ?? 0,
    behind: repo.behind ?? 0,
    dirtyCount: repo.dirty ?? 0,
    untrackedCount: repo.untracked ?? 0,
    stashCount: 0,
    isLocal: local,
    localPath: local ? `${MOCK_WORKSPACE_ROOT}\\${repoName(dto.id, repo.type)}` : null,
    isStale: false,
    calcAt: nowIso(),
    dataSource: 'local',
    errorCode: null,
    flags: repo.dirty
      ? ['dirty_tracked']
      : repo.untracked
        ? ['untracked']
        : repo.state === 'UNKNOWN'
          ? ['calc_timeout']
          : [],
  }
}

type MockRepositoryComponent = MockComponent

function toComponentDto(component: MockComponent): ComponentDto {
  const repos = component.repos.map((repo) => buildRepo(component, repo))
  return {
    componentId: component.id,
    aggregateState: component.state,
    severityRank: component.severity,
    branch: component.branch,
    tag: component.tag ?? null,
    mixedBranch: Boolean(component.mixed),
    flags: component.repos.some((repo) => repo.state === 'MISSING') ? ['missing_repo'] : [],
    incomplete: component.repos.some((repo) => repo.state === 'MISSING'),
    lastActivityAt: component.lastActivityAt ?? new Date(Date.now() - component.severity * 60_000).toISOString(),
    ciAggregate: {
      componentId: component.id,
      byState: { PASS: 0, FAIL: 0, RUNNING: 0, QUEUED: 0, NONE: 4, UNKNOWN: 0 },
      worstState: 'NONE',
      dataFreshAt: nowIso(),
    },
    repos,
  }
}

/** 内置 7 组件快照（source=mock，浏览器/CDP 默认数据源）。 */
export function mockSnapshot(): SnapshotDto {
  const components = MOCK_COMPONENTS.map(toComponentDto)
  const repos = components.reduce((sum, component) => sum + component.repos.length, 0)
  return {
    source: 'mock',
    workspaceRoot: MOCK_WORKSPACE_ROOT,
    error: null,
    generatedAt: nowIso(),
    components,
    counts: { components: components.length, repos, missingRepos: 4 },
    freshness: {
      localScanAt: new Date(Date.now() - 12_000).toISOString(),
      remoteFetchAt: new Date(Date.now() - 180_000).toISOString(),
      localStale: false,
      remoteStale: false,
    },
  }
}

// ───────────────────────────── Mock 文件树 ─────────────────────────────

interface MockNodeDef {
  name: string
  kind: 'DIR' | 'FILE'
  status?: GitStatus
  size?: number
}

type TreeDef = Record<string, MockNodeDef[]>

const IMPL_TREE: TreeDef = {
  '': [
    { name: 'src', kind: 'DIR' },
    { name: 'tests', kind: 'DIR' },
    { name: 'docs', kind: 'DIR' },
    { name: 'tools', kind: 'DIR' },
    { name: '.gitignore', kind: 'FILE', size: 118 },
    { name: 'README.md', kind: 'FILE', size: 1834, status: 'MODIFIED' },
  ],
  src: [
    { name: 'app', kind: 'DIR' },
    { name: 'control', kind: 'DIR' },
    { name: 'init.m', kind: 'FILE', size: 4096 },
    { name: 'config.json', kind: 'FILE', size: 812, status: 'MODIFIED' },
  ],
  'src/app': [
    { name: 'main.m', kind: 'FILE', size: 20480, status: 'MODIFIED' },
    { name: 'plant.slx', kind: 'FILE', size: 155_648 },
    { name: 'notes.txt', kind: 'FILE', size: 320, status: 'UNTRACKED' },
  ],
  'src/control': [
    { name: 'trac.c', kind: 'FILE', size: 8192 },
    { name: 'trac.h', kind: 'FILE', size: 2048 },
    { name: 'controller.mdl', kind: 'FILE', size: 98_304 },
    { name: 'legacy_ctrl.m', kind: 'FILE', size: 5120, status: 'DELETED' },
  ],
  tests: [
    { name: 'test_main.m', kind: 'FILE', size: 6144, status: 'ADDED' },
    { name: 'fixtures', kind: 'DIR' },
  ],
  'tests/fixtures': [
    { name: 'case_01.json', kind: 'FILE', size: 1024 },
    { name: 'case_02.json', kind: 'FILE', size: 1200, status: 'MODIFIED' },
  ],
  docs: [
    { name: 'design.md', kind: 'FILE', size: 2400 },
    { name: 'api.yaml', kind: 'FILE', size: 3600 },
  ],
  tools: [
    { name: 'build.ps1', kind: 'FILE', size: 640 },
    { name: 'deploy.bat', kind: 'FILE', size: 220 },
    { name: 'very_long_file_name_for_ellipsis_and_overflow_testing_0123456789.m', kind: 'FILE', size: 900 },
  ],
}

const DESIGN_TREE: TreeDef = {
  '': [
    { name: 'docs', kind: 'DIR' },
    { name: 'model', kind: 'DIR' },
    { name: 'src', kind: 'DIR' },
    { name: 'README.md', kind: 'FILE', size: 1500 },
    { name: 'requirements.md', kind: 'FILE', size: 4200, status: 'MODIFIED' },
  ],
  docs: [
    { name: 'design.md', kind: 'FILE', size: 3200 },
    { name: 'interface.md', kind: 'FILE', size: 2100 },
  ],
  model: [
    { name: 'plant.slx', kind: 'FILE', size: 128_000 },
    { name: 'controller.mdl', kind: 'FILE', size: 76_800 },
  ],
  src: [
    { name: 'requirements.yaml', kind: 'FILE', size: 1800 },
    { name: 'signals.csv', kind: 'FILE', size: 900 },
  ],
}

const MIL_TREE: TreeDef = {
  '': [
    { name: 'src', kind: 'DIR' },
    { name: 'tests', kind: 'DIR' },
    { name: 'README.md', kind: 'FILE', size: 980 },
  ],
  src: [
    { name: 'harness', kind: 'DIR' },
    { name: 'main.m', kind: 'FILE', size: 12_288 },
  ],
  'src/harness': [
    { name: 'io.c', kind: 'FILE', size: 4096 },
    { name: 'io.h', kind: 'FILE', size: 1024 },
  ],
  tests: [{ name: 'case_01.m', kind: 'FILE', size: 2048 }],
}

const SIL_TREE: TreeDef = {
  '': [
    { name: 'src', kind: 'DIR' },
    { name: 'docs', kind: 'DIR' },
    { name: 'README.md', kind: 'FILE', size: 900 },
    { name: '.gitignore', kind: 'FILE', size: 100 },
  ],
  src: [
    { name: 'sim', kind: 'DIR' },
    { name: 'main.m', kind: 'FILE', size: 9216 },
  ],
  'src/sim': [
    { name: 'plant.m', kind: 'FILE', size: 6144 },
    { name: 'scenario.json', kind: 'FILE', size: 1200, status: 'UNTRACKED' },
  ],
  docs: [{ name: 'validation.md', kind: 'FILE', size: 2400 }],
}

const TREE_BY_REPO: Record<RepoType, TreeDef> = {
  DESIGN: DESIGN_TREE,
  IMPL: IMPL_TREE,
  MIL: MIL_TREE,
  SIL: SIL_TREE,
}

function findMockComponent(componentId: string): MockComponent | undefined {
  return MOCK_COMPONENTS.find((component) => component.id === componentId)
}

function findMockRepo(componentId: string, type: RepoType): MockRepo | undefined {
  return findMockComponent(componentId)?.repos.find((repo) => repo.type === type)
}

/**
 * Mock 兜底解析：真实 Host 的组件 ID 不在内置 7 组件里时，按仓库类型给一个可浏览的
 * 合成仓（SYNCED/main），保证「Host 已连接但只读方法未实现」时文件树仍可展示 mock 内容。
 */
function resolveMockRepo(
  componentId: string,
  type: RepoType,
): { component: MockComponent; repo: MockRepo } {
  const foundRepo = findMockRepo(componentId, type)
  const foundComponent = findMockComponent(componentId)
  if (foundRepo && foundComponent) return { component: foundComponent, repo: foundRepo }
  const repo: MockRepo = { type, state: 'SYNCED', branch: 'main', head: HEAD_A }
  const component: MockComponent = {
    id: componentId,
    state: 'SYNCED',
    severity: 50,
    branch: 'main',
    repos: [{ type, state: 'SYNCED', branch: 'main', head: HEAD_A }],
  }
  return { component, repo }
}

function toMockTreeNodes(defs: MockNodeDef[], parentPath: string): TreeNodeDto[] {
  return defs.map((def) => ({
    path: parentPath ? `${parentPath}/${def.name}` : def.name,
    name: def.name,
    kind: def.kind,
    hasChildren: def.kind === 'DIR',
    size: def.kind === 'FILE' ? def.size ?? 1024 : null,
    mtime: nowIso(),
    gitStatus: def.status ?? 'NONE',
    submodule: false,
    ignored: false,
    children: null,
    error: null,
  }))
}

/** getRepoTree 的 Mock 实现：按（componentId, repoType, path）返回目录直接子项。 */
export function mockRepoTree(componentId: string, type: RepoType, path = ''): RepoTreeDto {
  const { repo } = resolveMockRepo(componentId, type)
  const state = repo.state
  if (state === 'MISSING') {
    throw new HostError('E_REPO_NOT_LOCAL', 'Mock：仓库未克隆（MISSING）')
  }
  const tree = TREE_BY_REPO[type]
  const defs = tree[path] ?? null
  if (!defs) {
    throw new HostError('E_PATH_INVALID', `Mock：路径不存在 ${path}`)
  }
  // 根路径为空串；子路径返回条目 path 为仓根相对路径（不带 repoDir 前缀）
  return {
    componentId,
    repoType: type,
    repoName: repoName(componentId, type),
    rootPath: `${MOCK_WORKSPACE_ROOT}\\${repoName(componentId, type)}`,
    path,
    branch: repo.branch === '—' ? null : repo.branch,
    headSha: repo.head ?? null,
    state,
    dirtyCount: repo.dirty ?? 0,
    compareRef: `refs/remotes/origin/${repo.branch}`,
    fetchedAt: nowIso(),
    entries: toMockTreeNodes(defs, path),
    truncated: false,
    totalEntries: defs.length,
    error: null,
  }
}

// ───────────────────────────── Mock 详情 / 提交 ─────────────────────────────

const COMMIT_MESSAGES = [
  'feat(control): 增加前馈补偿并更新标定参数',
  'fix(model): 修正 plant 模型在低速段的积分饱和',
  'docs: 补充接口约定与信号映射说明',
  'refactor(app): 收敛初始化流程，拆分配置加载',
  'test: 补充 MIL 用例 case_02 边界条件',
  'chore: 同步版本号与变更记录',
  'feat(sil): 场景 JSON 增加故障注入开关',
  'perf(control): 降低主循环抖动',
]

/** getRepoDetail 的 Mock 实现（含最近 5 条提交）。 */
export function mockRepoDetail(componentId: string, type: RepoType): RepoDetailDto {
  const { component, repo } = resolveMockRepo(componentId, type)
  const commits = mockCommits(componentId, type, 0, 5)
  return {
    repo: buildRepo(component, repo),
    component: toComponentDto(component),
    commits: { total: 128, items: commits.items },
  }
}

/** getCommits 的 Mock 实现。 */
export function mockCommits(
  componentId: string,
  type: RepoType,
  skip = 0,
  take = 100,
): { total: number; items: CommitDto[] } {
  const { repo } = resolveMockRepo(componentId, type)
  const total = 128
  const count = Math.max(0, Math.min(take, total - skip))
  const items: CommitDto[] = Array.from({ length: count }, (_, index) => {
    const ordinal = skip + index
    const message = COMMIT_MESSAGES[ordinal % COMMIT_MESSAGES.length]
    return {
      sha: `${(ordinal + 1).toString(16).padStart(2, '0')}e8d7c6b5a4f3e2d1c0b9a8f7e6d5c4b3a2f1e0d`,
      shortSha: `${(ordinal + 1).toString(16).padStart(2, '0')}e8d7c`,
      message,
      author: ordinal % 3 === 0 ? 'Trevn' : ordinal % 3 === 1 ? 'Li Wei' : 'Zhang Min',
      at: new Date(Date.now() - ordinal * 3_600_000).toISOString(),
      refs: ordinal === 0 && repo.branch !== '—' ? [`HEAD -> ${repo.branch}`, `origin/${repo.branch}`] : [],
    }
  })
  return { total, items }
}

// ───────────────────────────── Mock 工作区树（文件夹视图） ─────────────────────────────

function toWorkspaceRepoNode(component: MockComponent, repo: MockRepo): WorkspaceNodeDto {
  return {
    kind: 'REPO',
    repoType: repo.type,
    name: repoName(component.id, repo.type),
    path: repoName(component.id, repo.type),
    componentId: component.id,
    state: repo.state,
    branch: repo.branch === '—' ? null : repo.branch,
    ahead: repo.ahead ?? 0,
    behind: repo.behind ?? 0,
    dirtyCount: (repo.dirty ?? 0) + (repo.untracked ?? 0),
    isLocal: repo.state !== 'MISSING',
    hasChildren: repo.state !== 'MISSING',
  }
}

function toWorkspaceComponentNode(component: MockComponent): WorkspaceNodeDto {
  return {
    kind: 'COMPONENT',
    componentId: component.id,
    name: component.id,
    path: component.id,
    state: component.state,
    severityRank: component.severity,
    dirtyCount: component.repos.reduce((sum, repo) => sum + (repo.dirty ?? 0) + (repo.untracked ?? 0), 0),
    mixedBranch: Boolean(component.mixed),
    branch: component.branch,
    repos: component.repos.map((repo) => toWorkspaceRepoNode(component, repo)),
  }
}

const UNBOUND_DIRS = ['X_SIL_Design', 'X_Legacy_Impl', 'scratch']

function unboundGroup(): WorkspaceNodeDto {
  return {
    kind: 'UNBOUND_GROUP',
    name: '未纳管',
    path: '__unbound__',
    count: UNBOUND_DIRS.length,
    hasChildren: true,
    children: UNBOUND_DIRS.map((dir) => ({
      kind: 'DIR',
      name: dir,
      path: dir,
      hasChildren: true,
    })),
  }
}

function physicalEntries(
  repoDir: string,
  relPath: string,
  defs: MockNodeDef[],
): WorkspaceNodeDto[] {
  return defs.map((def) => ({
    kind: def.kind,
    name: def.name,
    path: relPath ? `${repoDir}/${relPath}/${def.name}` : `${repoDir}/${def.name}`,
    hasChildren: def.kind === 'DIR',
    size: def.kind === 'FILE' ? def.size ?? 1024 : null,
    mtime: nowIso(),
    gitStatus: def.status ?? 'NONE',
    ignored: false,
    submodule: false,
  }))
}

function parseRepoDir(path: string): { componentId: string; type: RepoType; rel: string } | null {
  const segments = path.split('/')
  const first = segments[0]
  const rel = segments.slice(1).join('/')
  for (const type of REPO_TYPES) {
    const suffix = `_${type === 'DESIGN' ? 'Design' : type === 'IMPL' ? 'Impl' : type}`
    if (first.endsWith(suffix)) {
      return { componentId: first.slice(0, -suffix.length), type, rel }
    }
  }
  return null
}

/** getWorkspaceTree 的 Mock 实现（根层=组件分组；展开仓/目录=物理条目）。 */
export function mockWorkspaceTree(path = ''): WorkspaceTreeDto {
  const base = {
    root: MOCK_WORKSPACE_ROOT,
    path,
    generatedAt: nowIso(),
    readAt: nowIso(),
    truncated: false,
    error: null,
  }
  if (!path) {
    return {
      ...base,
      entries: [...MOCK_COMPONENTS.map(toWorkspaceComponentNode), unboundGroup()],
    }
  }
  const parsed = parseRepoDir(path)
  if (parsed) {
    const component = findMockComponent(parsed.componentId)
    if (!component) throw new HostError('E_NOT_FOUND', `Mock：组件不存在 ${parsed.componentId}`)
    const repo = findMockRepo(parsed.componentId, parsed.type)
    if (!repo || repo.state === 'MISSING') {
      throw new HostError('E_REPO_NOT_LOCAL', 'Mock：仓库未克隆（MISSING）')
    }
    const tree = TREE_BY_REPO[parsed.type]
    const defs = tree[parsed.rel] ?? tree[''] ?? []
    return { ...base, entries: physicalEntries(path.split('/')[0], parsed.rel, defs) }
  }
  if (path === '__unbound__') {
    return { ...base, entries: unboundGroup().children ?? [] }
  }
  if (UNBOUND_DIRS.includes(path) || path.includes('/')) {
    const defs: MockNodeDef[] =
      path.endsWith('Design')
        ? DESIGN_TREE['']
        : path.endsWith('Impl')
          ? IMPL_TREE['']
          : [{ name: 'README.md', kind: 'FILE', size: 320 }]
    return { ...base, entries: physicalEntries(path, '', defs) }
  }
  throw new HostError('E_PATH_OUTSIDE_ROOT', `Mock：路径不在工作区内 ${path}`)
}

// ───────────────────────────── Mock CI / HostInfo / 动作 ─────────────────────────────

/** getCiStatus 的 Mock 实现：ci.enabled=false，全 NONE（CI-10：不参与聚合）。 */
export function mockCiStatus(componentId: string): CiStatusDto {
  return {
    enabled: false,
    provider: 'mock',
    fetchedAt: null,
    latest: [],
    aggregate: {
      componentId,
      byState: { PASS: 0, FAIL: 0, RUNNING: 0, QUEUED: 0, NONE: 4, UNKNOWN: 0 },
      worstState: 'NONE',
      dataFreshAt: nowIso(),
    },
  }
}

export function mockHostInfo(): HostInfoDto {
  return {
    appVersion: '0.3.0',
    webView2Version: 'Mock（浏览器）',
    features: { fileTree: true, workspaceTree: true, openPath: true, repoActions: false, ci: false },
    limits: { maxTreeEntries: 2000, snapshotTimeoutMs: 45_000 },
  }
}

// ───────────────────── Mock 单源矩阵（仅 Mock/CDP 自测；运行逻辑只消费 state.hostActions） ─────────────────────

/**
 * 与单源夹具 `reqs/v0.2/fixtures/m2-matrix.json`（M2a 构建）同构的 14 态 × 5 键编码
 * （PULL/FETCH/CHKOUT/COMMIT/PUSH）：Y=允许 / C=允许但需确认 / N=禁止。
 * M2a 冻结覆盖：EMPTY.PUSH 禁用（空仓无提交，首推归 M2b）。
 * 仅在无 Host（浏览器 dev / CDP）时由 store 载入 `state.hostActions`，**不得**被运行逻辑直接引用。
 */
const MOCK_HOST_ACTIONS_FIXTURE: Record<string, string> = {
  SYNCED: 'YYYYY',
  DIRTY: 'NYCYY',
  AHEAD: 'NYCNY',
  BEHIND: 'YYCNN',
  DIVERGED: 'NYCNN',
  DETACHED_TAG: 'NYCNN',
  DETACHED_COMMIT: 'NYCNN',
  NO_UPSTREAM: 'NYNNY',
  EMPTY: 'NYCYN',
  UNMERGED: 'NYNNN',
  IN_PROGRESS: 'NNNNN',
  INVALID: 'NNNNN',
  MISSING: 'NNNNN',
  UNKNOWN: 'NYNNN',
}

/** Host `RepoActionService.DisabledReason` 的 Mock 同构（仅用于 tooltip 文案自测）。 */
function mockDisabledReason(state: string, action: RepoAction): string {
  switch (`${state}/${action}`) {
    case 'DIRTY/PULL':
      return 'DIRTY：需先提交或 stash'
    case 'DIVERGED/PULL':
      return 'DIVERGED：需手动 Merge/Rebase'
    case 'UNKNOWN/PULL':
      return 'UNKNOWN：关系数据过期，先 FETCH'
    case 'AHEAD/PULL':
      return 'AHEAD：需先推送或手动处理'
    case 'NO_UPSTREAM/PULL':
      return 'NO_UPSTREAM：未配置 upstream'
    case 'EMPTY/PULL':
      return 'EMPTY：无首个提交'
    case 'UNMERGED/PULL':
      return 'UNMERGED：存在未解决冲突'
    case 'AHEAD/COMMIT':
      return 'AHEAD：需先推送/手动处理'
    case 'BEHIND/COMMIT':
      return 'BEHIND：先 PULL'
    case 'UNMERGED/COMMIT':
      return 'UNMERGED：存在未解决冲突'
    case 'IN_PROGRESS/COMMIT':
      return 'IN_PROGRESS：合并/变基未完成'
    case 'BEHIND/PUSH':
      return 'BEHIND：先 PULL'
    case 'DETACHED_TAG/PUSH':
    case 'DETACHED_COMMIT/PUSH':
      return 'DETACHED：请先基于当前 HEAD 创建分支'
    case 'UNMERGED/PUSH':
      return 'UNMERGED：存在未解决冲突'
    case 'IN_PROGRESS/FETCH':
      return 'IN_PROGRESS：操作未完成'
    case 'INVALID/FETCH':
      return 'INVALID：仓库绑定异常'
    case 'MISSING/CHKOUT':
      return 'MISSING：仓库未克隆'
    case 'IN_PROGRESS/CHKOUT':
      return 'IN_PROGRESS：操作未完成'
    default:
      return `当前状态 ${state} 不允许 ${action}（spec §6.2）`
  }
}

/**
 * Mock 的 Host actions（与 `getComponentDetail.repos[].actions` 同构），用于浏览器 dev / CDP 自测。
 * 真实 Host 下 UI 只消费 Host 返回值；本函数不得进入运行逻辑的可用性判定。
 */
export function mockHostActionsForState(stateRaw: string): Record<RepoAction, HostActionAvailability> {
  const state = (stateRaw || 'UNKNOWN').toUpperCase()
  const row = MOCK_HOST_ACTIONS_FIXTURE[state] ?? 'NNNNN'
  const result = {} as Record<RepoAction, HostActionAvailability>
  REPO_ACTIONS.forEach((action, index) => {
    const cell = row[index] ?? 'N'
    const enabled = cell === 'Y' || cell === 'C'
    // v0.3.1-r2 冻结：RequiresConfirm = (cell == C) ∨ action ∈ {COMMIT, PUSH}（禁用单元同样透传）。
    const requiresConfirm = cell === 'C' || action === 'COMMIT' || action === 'PUSH'
    result[action] = {
      action,
      enabled,
      reasonCode: enabled ? (requiresConfirm ? 'CONFIRM_REQUIRED' : 'AVAILABLE') : 'STATE_BLOCKED',
      reason: enabled
        ? requiresConfirm
          ? '该操作会改变工作区/HEAD，需二次确认（args.confirm=true）'
          : 'M2 已启用：可直接执行'
        : mockDisabledReason(state, action),
      requiresConfirm,
    }
  })
  return result
}

/** 按组件 + 仓类型生成 Mock Host actions（供 store 在 Mock 模式下填充 state.hostActions）。 */
export function mockHostActions(componentId: string, type: RepoType): Record<RepoAction, HostActionAvailability> {
  const { repo } = resolveMockRepo(componentId, type)
  return mockHostActionsForState(repo.state)
}

/** openPath 的 Mock 实现：仅返回成功结果，不启动外部程序。 */
export function mockOpenPath(path: string, mode: 'open' | 'reveal'): OpenPathResultDto {
  return {
    opened: true,
    mode,
    path,
    message: mode === 'reveal' ? `已在资源管理器中定位（Mock）：${path}` : `已用默认程序打开（Mock）：${path}`,
  }
}

/** runRepoAction 的 Mock 实现：M1 只读，返回 E_NOT_IMPLEMENTED（与 Host 契约一致）。 */
export function mockRunRepoAction(action: string): never {
  throw new HostError(
    'E_NOT_IMPLEMENTED',
    `M1 为只读模式：${action} 将在 M2 启用（需预检、dry-run/备份、逐仓结果与任务中心）`,
    { action, writeStage: 'M2' },
  )
}

// ───────────────────────────── M2b Mock 任务中心 / 批量作业 ─────────────────────────────

/**
 * 内置 2 个演示作业（进行中 CLONE / 部分失败 SYNC），并按轮询推进进度。
 * 仅在 Mock 模式（无 Host / CDP 强制 Mock）使用；真实 Host 下 store 走 Host 作业 API。
 */
let mockJobSeq = 0
let mockJobsCache: JobDto[] | null = null
/** 演示用「停在进行中」预算：种子作业完成指定项数后保留 1 个 RUNNING 项，便于演示取消。 */
const mockStallBudget = new Map<string, number>()

function isoAgo(ms: number): string {
  return new Date(Date.now() - ms).toISOString()
}

function cloneJobs(): JobDto[] {
  return JSON.parse(JSON.stringify(mockJobsCache ?? [])) as JobDto[]
}

function findMockJob(jobId: string): JobDto | undefined {
  return ensureMockJobs().find((job) => job.id === jobId)
}

function seedMockJobs(): JobDto[] {
  const runningId = 'job-mock-9001'
  const runningStarted = isoAgo(14_000)
  const running: JobDto = {
    id: runningId,
    kind: 'CLONE',
    state: 'RUNNING',
    total: 4,
    ok: 2,
    failed: 0,
    skipped: 0,
    createdAt: runningStarted,
    startedAt: runningStarted,
    finishedAt: null,
    cancellable: true,
    items: [
      {
        componentId: 'CF111D_BrakeCtrl',
        repoType: 'DESIGN',
        repoName: repoName('CF111D_BrakeCtrl', 'DESIGN'),
        state: 'OK',
        startedAt: isoAgo(13_000),
        finishedAt: isoAgo(9_000),
        preHead: null,
        postHead: HEAD_A,
        errorCode: null,
        message: 'Clone 完成（Mock）',
        backupRef: null,
      },
      {
        componentId: 'CF111D_BrakeCtrl',
        repoType: 'IMPL',
        repoName: repoName('CF111D_BrakeCtrl', 'IMPL'),
        state: 'OK',
        startedAt: isoAgo(9_000),
        finishedAt: isoAgo(5_000),
        preHead: null,
        postHead: HEAD_B,
        errorCode: null,
        message: 'Clone 完成（Mock）',
        backupRef: null,
      },
      {
        componentId: 'CF111D_BrakeCtrl',
        repoType: 'MIL',
        repoName: repoName('CF111D_BrakeCtrl', 'MIL'),
        state: 'RUNNING',
        startedAt: isoAgo(2_000),
        finishedAt: null,
        preHead: null,
        postHead: null,
        errorCode: null,
        message: '正在 Clone（Mock）',
        backupRef: null,
      },
      {
        componentId: 'CF111D_BrakeCtrl',
        repoType: 'SIL',
        repoName: repoName('CF111D_BrakeCtrl', 'SIL'),
        state: 'PENDING',
        startedAt: null,
        finishedAt: null,
        preHead: null,
        postHead: null,
        errorCode: null,
        message: null,
        backupRef: null,
      },
    ],
  }
  mockStallBudget.set(runningId, 1)

  const syncStarted = isoAgo(8 * 60_000)
  const syncFinished = isoAgo(7.5 * 60_000)
  const backupAt = new Date(Date.now() - 8 * 60_000).toISOString().replace(/[:.]/g, '').slice(0, 15)
  const partial: JobDto = {
    id: 'job-mock-9002',
    kind: 'SYNC',
    state: 'PARTIAL',
    total: 4,
    ok: 2,
    failed: 2,
    skipped: 0,
    createdAt: syncStarted,
    startedAt: syncStarted,
    finishedAt: syncFinished,
    cancellable: false,
    items: [
      {
        componentId: 'CF123A_ShortName',
        repoType: 'DESIGN',
        repoName: repoName('CF123A_ShortName', 'DESIGN'),
        state: 'OK',
        startedAt: syncStarted,
        finishedAt: isoAgo(7 * 60_000),
        preHead: HEAD_B,
        postHead: HEAD_A,
        errorCode: null,
        message: 'SYNC 完成：已备份并硬重置到远程（Mock）',
        backupRef: `refs/backup/sync/${backupAt}/DESIGN/main`,
      },
      {
        componentId: 'CF123A_ShortName',
        repoType: 'IMPL',
        repoName: repoName('CF123A_ShortName', 'IMPL'),
        state: 'OK',
        startedAt: syncStarted,
        finishedAt: isoAgo(6.8 * 60_000),
        preHead: HEAD_C,
        postHead: HEAD_B,
        errorCode: null,
        message: 'SYNC 完成：已备份并硬重置到远程（Mock）',
        backupRef: `refs/backup/sync/${backupAt}/IMPL/main`,
      },
      {
        componentId: 'CF123A_ShortName',
        repoType: 'MIL',
        repoName: repoName('CF123A_ShortName', 'MIL'),
        state: 'FAILED',
        startedAt: isoAgo(6.8 * 60_000),
        finishedAt: isoAgo(6.6 * 60_000),
        preHead: null,
        postHead: null,
        errorCode: 'E_DIRTY_WORKTREE',
        message: '工作区存在未提交改动，已跳过（未做破坏性改动）',
        backupRef: null,
      },
      {
        componentId: 'CF123A_ShortName',
        repoType: 'SIL',
        repoName: repoName('CF123A_ShortName', 'SIL'),
        state: 'FAILED',
        startedAt: isoAgo(6.6 * 60_000),
        finishedAt: isoAgo(6.4 * 60_000),
        preHead: null,
        postHead: null,
        errorCode: 'E_OFFLINE',
        message: '网络不可达，fetch 超时（attempt=2）',
        backupRef: null,
      },
    ],
  }
  return [running, partial]
}

function ensureMockJobs(): JobDto[] {
  if (!mockJobsCache) mockJobsCache = seedMockJobs()
  return mockJobsCache
}

/** 读取全部 Mock 作业（深拷贝，避免组件直接改缓存）。 */
export function mockJobs(): JobDto[] {
  ensureMockJobs()
  return cloneJobs()
}

function nextHead(): string {
  return `${Math.random().toString(16).slice(2, 10)}${Date.now().toString(16)}`.padEnd(40, '0').slice(0, 40)
}

/** 每个轮询 tick 推进一次 Mock 作业进度（单调不减；演示作业到达预定点后停在 RUNNING）。 */
export function tickMockJobs(): void {
  for (const job of ensureMockJobs()) {
    if (job.state === 'PENDING') {
      job.state = 'RUNNING'
      job.startedAt = job.startedAt ?? new Date().toISOString()
    }
    if (job.state !== 'RUNNING') continue
    const budget = mockStallBudget.get(job.id)
    if (budget !== undefined && budget <= 0) continue
    const running = job.items.find((item) => item.state === 'RUNNING')
    if (running) {
      running.state = 'OK'
      running.finishedAt = new Date().toISOString()
      running.postHead = running.postHead ?? nextHead()
      running.message = running.message ?? '完成（Mock）'
      job.ok += 1
      if (budget !== undefined) mockStallBudget.set(job.id, budget - 1)
    }
    const next = job.items.find((item) => item.state === 'PENDING')
    if (next) {
      next.state = 'RUNNING'
      next.startedAt = new Date().toISOString()
      continue
    }
    if (!job.items.some((item) => item.state === 'RUNNING')) {
      job.state = job.failed > 0 ? 'PARTIAL' : 'SUCCESS'
      job.finishedAt = new Date().toISOString()
      job.cancellable = false
    }
  }
}

export interface MockBatchResult {
  jobId: string
  accepted: boolean
  total: number
  skipped: number
}

/** runBatch 的 Mock 实现：生成一个 RUNNING 作业（dryRun 则为 SUCCESS + 全部 SKIPPED）。 */
export function mockRunBatch(
  kind: string,
  componentIds: string[],
  repoTypes: string[] | undefined,
  args: BatchArgs = {},
): MockBatchResult {
  const types = (repoTypes && repoTypes.length > 0 ? repoTypes : REPO_TYPES) as readonly string[]
  const targets: { componentId: string; repoType: string }[] = []
  for (const componentId of componentIds) {
    for (const repoType of types) targets.push({ componentId, repoType: repoType.toUpperCase() })
  }
  const limited = targets.slice(0, 40)
  const dryRun = args.dryRun === true
  const now = new Date().toISOString()
  const id = `job-mock-${++mockJobSeq + 9100}`
  const refName = args.branch ?? args.tag ?? ''
  const items: JobItemDto[] = limited.map((target) => ({
    componentId: target.componentId,
    repoType: target.repoType,
    repoName: repoName(target.componentId, target.repoType as RepoType),
    state: dryRun ? 'SKIPPED' : 'PENDING',
    startedAt: dryRun ? now : null,
    finishedAt: dryRun ? now : null,
    preHead: null,
    postHead: null,
    errorCode: null,
    message: dryRun ? 'dryRun：仅预览，未执行任何写操作（Mock）' : `${kind} 已入队（Mock）`,
    backupRef: null,
  }))
  const job: JobDto = {
    id,
    kind: kind.toUpperCase() as JobDto['kind'],
    state: dryRun ? 'SUCCESS' : 'RUNNING',
    total: items.length,
    ok: 0,
    failed: 0,
    skipped: dryRun ? items.length : 0,
    createdAt: now,
    startedAt: dryRun ? now : null,
    finishedAt: dryRun ? now : null,
    cancellable: !dryRun,
    items,
  }
  if (!dryRun && refName) {
    // 分支/Tag 名称仅用于演示提示；不伪造 git 状态。
    job.items.forEach((item) => {
      item.message = `${kind} ${refName} 已入队（Mock）`
    })
  }
  if (!dryRun) mockJobsCache = [job, ...ensureMockJobs()]
  else mockJobsCache = [job, ...ensureMockJobs()]
  return { jobId: id, accepted: true, total: items.length, skipped: job.skipped }
}

/** cancelJob 的 Mock 实现：合作式取消（已开始项 CANCELED，未开始项保持 PENDING）。 */
export function mockCancelJob(jobId: string): JobDto | null {
  const job = findMockJob(jobId)
  if (!job) return null
  if (!job.cancellable || (job.state !== 'RUNNING' && job.state !== 'PENDING')) {
    return JSON.parse(JSON.stringify(job)) as JobDto
  }
  job.state = 'CANCELED'
  job.cancellable = false
  job.finishedAt = new Date().toISOString()
  mockStallBudget.delete(job.id)
  for (const item of job.items) {
    if (item.state === 'RUNNING') {
      item.state = 'CANCELED'
      item.finishedAt = new Date().toISOString()
      item.message = '已取消（合作式取消，未整体回滚；Mock）'
    }
  }
  return JSON.parse(JSON.stringify(job)) as JobDto
}

/** retryFailed 的 Mock 实现：仅把失败/取消/中断项作为新作业重跑。 */
export function mockRetryFailed(jobId: string): MockBatchResult | null {
  const source = findMockJob(jobId)
  if (!source) return null
  const retryItems = source.items.filter(
    (item) => item.state === 'FAILED' || item.state === 'CANCELED' || item.state === 'INTERRUPTED',
  )
  if (retryItems.length === 0) return null
  const now = new Date().toISOString()
  const id = `job-mock-retry-${++mockJobSeq + 9200}`
  const job: JobDto = {
    id,
    kind: source.kind,
    state: 'PENDING',
    total: retryItems.length,
    ok: 0,
    failed: 0,
    skipped: 0,
    createdAt: now,
    startedAt: null,
    finishedAt: null,
    cancellable: true,
    items: retryItems.map((item) => ({
      ...item,
      state: 'PENDING',
      startedAt: null,
      finishedAt: null,
      errorCode: null,
      preHead: null,
      postHead: null,
      message: '仅重试失败项：已重新入队（Mock）',
    })),
  }
  mockJobsCache = [job, ...ensureMockJobs()]
  return { jobId: id, accepted: true, total: job.total, skipped: 0 }
}
