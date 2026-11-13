<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import {
  hasHostBridge,
  normalizeSnapshot,
  request,
  toErrorMessage,
} from './api'
import type { ComponentDto, RepoDto, SnapshotDto } from './api'

type CiState = 'PASS' | 'FAIL' | 'RUNNING' | 'QUEUED' | 'NONE' | 'UNKNOWN'
type RepoType = 'Design' | 'Impl' | 'MIL' | 'SIL'
type View = 'board' | 'components' | 'ci' | 'tasks' | 'notifications' | 'logs' | 'settings'

type Repo = {
  type: RepoType; state: string; branch: string; tag?: string; dirty?: boolean
  headSha?: string; ahead?: number; behind?: number; untrackedCount?: number
}
type ComponentRow = {
  id: string; short: string; state: string; rail: string; repos: Repo[]
  branch: string; mixed?: boolean; tag?: string; ci: CiState; fresh: string; severity: number
}

const mockComponents: ComponentRow[] = [
  { id: 'CF325F_ChanganSigProcgBascFct', short: 'CF325F_ChanganSigProcgBascFct', state: 'SYNCED', rail: 'synced', severity: 14, branch: 'main', tag: 'v1.2.0', ci: 'PASS', fresh: '本地 4s 前 · 远程 2min 前',
    repos: [ { type: 'Design', state: 'SYNCED', branch: 'main' }, { type: 'Impl', state: 'SYNCED', branch: 'main' }, { type: 'MIL', state: 'SYNCED', branch: 'main' }, { type: 'SIL', state: 'SYNCED', branch: 'main' } ] },
  { id: 'CF123A_ShortName', short: 'CF123A_ShortName', state: 'BEHIND', rail: 'behind', severity: 6, branch: 'main', tag: 'v1.1.3', ci: 'FAIL', fresh: '本地 8s 前 · 远程 6min 前',
    repos: [ { type: 'Design', state: 'BEHIND', branch: 'main' }, { type: 'Impl', state: 'BEHIND', branch: 'main' }, { type: 'MIL', state: 'SYNCED', branch: 'main' }, { type: 'SIL', state: 'SYNCED', branch: 'main' } ] },
  { id: 'CF900A_TractionCtrl', short: 'CF900A_TractionCtrl', state: 'DIVERGED', rail: 'diverged', severity: 4, branch: 'feat-x', mixed: true, ci: 'RUNNING', fresh: '本地 2s 前 · 远程 1min 前',
    repos: [ { type: 'Design', state: 'DIVERGED', branch: 'feat-x' }, { type: 'Impl', state: 'AHEAD', branch: 'feat-x' }, { type: 'MIL', state: 'SYNCED', branch: 'main' }, { type: 'SIL', state: 'BEHIND', branch: 'main' } ] },
  { id: 'CF777B_BatteryMgmt', short: 'CF777B_BatteryMgmt', state: 'DIRTY', rail: 'dirty', severity: 8, branch: 'dev', ci: 'NONE', fresh: '本地 3s 前 · 远程 3min 前',
    repos: [ { type: 'Design', state: 'DIRTY', branch: 'dev', dirty: true }, { type: 'Impl', state: 'DIRTY', branch: 'dev', dirty: true }, { type: 'MIL', state: 'SYNCED', branch: 'dev' }, { type: 'SIL', state: 'AHEAD', branch: 'dev' } ] },
  { id: 'CF555C_Gateway', short: 'CF555C_Gateway', state: 'DETACHED', rail: 'detached', severity: 10, branch: '—', tag: 'v2.0.0', ci: 'UNKNOWN', fresh: '本地 6s 前 · 远程 8min 前',
    repos: [ { type: 'Design', state: 'DETACHED_TAG', branch: '—', tag: 'v2.0.0' }, { type: 'Impl', state: 'DETACHED_TAG', branch: '—', tag: 'v2.0.0' }, { type: 'MIL', state: 'DETACHED_TAG', branch: '—', tag: 'v2.0.0' }, { type: 'SIL', state: 'SYNCED', branch: 'main' } ] },
  { id: 'CF111D_BrakeCtrl', short: 'CF111D_BrakeCtrl', state: 'MISSING', rail: 'missing', severity: 12, branch: '—', ci: 'NONE', fresh: '本地 5s 前 · 远程 10min 前',
    repos: [ { type: 'Design', state: 'MISSING', branch: '—' }, { type: 'Impl', state: 'MISSING', branch: '—' }, { type: 'MIL', state: 'MISSING', branch: '—' }, { type: 'SIL', state: 'MISSING', branch: '—' } ] },
  { id: 'CF001E_VehicleState', short: 'CF001E_VehicleState', state: 'UNKNOWN', rail: 'unknown', severity: 2, branch: 'main', ci: 'QUEUED', fresh: '本地超时 · 远程 42min 前',
    repos: [ { type: 'Design', state: 'UNKNOWN', branch: 'main' }, { type: 'Impl', state: 'SYNCED', branch: 'main' }, { type: 'MIL', state: 'SYNCED', branch: 'main' }, { type: 'SIL', state: 'SYNCED', branch: 'main' } ] }
]

const mockNotifications = ref([
  { id: 1, type: '状态变化', level: 'warn', title: 'CF123A_ShortName 落后远程 2 个提交', detail: '3 个仓 BEHIND，建议执行同步（M0 Mock）', at: '2min 前', read: false },
  { id: 2, type: 'Token 过期', level: 'err', title: 'Gitea PAT 将在 3 天后过期', detail: '请到 设置 → 认证 更新 Token（M0 Mock）', at: '1h 前', read: false },
  { id: 3, type: '远程同步', level: 'info', title: '组织 MBD_Org 元数据同步完成', detail: '覆盖率 98.7%，部分仓失败可重试（M0 Mock）', at: '3h 前', read: true }
])
const mockLogs = [
  { level: 'INFO', at: '17:32:02', msg: 'Host started; webview2 initialized 157.0.4341.46' },
  { level: 'INFO', at: '17:32:03', msg: 'RemoteSync: org MBD_Org list watermark reached (M0 Mock)' },
  { level: 'WARN', at: '17:32:18', msg: 'LocalScan: safe.directory rejected for D:\\Work\\legacy\\X_Design (MOCK)' },
  { level: 'ERROR', at: '17:32:24', msg: 'SYNC-E002: 429 rate limited, retry in 30s (MOCK)' },
  { level: 'INFO', at: '17:32:30', msg: 'StatusCalc: 7 components / 28 repos, dirty-set 3 (MOCK)' },
  { level: 'DEBUG', at: '17:32:31', msg: 'job queue depth=0, db size=12.4MB (MOCK)' }
]
const logLevel = ref('全部')

const view = ref<View>('board')
const search = ref('')
const selectedId = ref(mockComponents[1].id)
const selectedRepo = ref<RepoType>('Impl')
const taskCount = ref(0)
const toast = ref('')
let toastTimer: ReturnType<typeof setTimeout> | undefined
const notify = (message: string) => {
  toast.value = message
  if (toastTimer) clearTimeout(toastTimer)
  toastTimer = setTimeout(() => { toast.value = '' }, 2400)
}

// —— Host 快照（M1）：成功则替换数据源；失败 / 无配置（source=mock）→ 保留 Mock ——
const components = ref<ComponentRow[]>(mockComponents)
const hostLinked = ref(false)
const dataSource = ref('mock')
const workspaceRoot = ref('')
const snapshotAt = ref('')
const snapshotError = ref('')
const snapshotEmpty = ref(false)
const fallbackReason = ref('')
const refreshing = ref(false)

// Host 首次 getSnapshot / refresh 会触发真实本地扫描（400 仓约 10–20s），8s 不够：
// 这两个入口用 45s 宽限并显示「扫描中」，超时保留 Mock 但持续提示 + 自动重试。
const SNAPSHOT_TIMEOUT_MS = 45_000
const SNAPSHOT_RETRY_DELAYS_MS = [5_000, 15_000]
const booting = ref(false)
const hostFailed = ref(false)
let retryIndex = 0
let snapshotRetryTimer: ReturnType<typeof setTimeout> | undefined

const sourceLabel = computed(() => {
  if (!hostLinked.value) return 'Mock 数据'
  switch (dataSource.value) {
    case 'mock': return 'Mock 数据'
    case 'local': return '本地扫描'
    case 'local+gitea': return '本地扫描 + Gitea'
    default: return `Host · ${dataSource.value || '未知数据源'}`
  }
})
const sourceTone = computed(() => {
  if (!hostLinked.value || dataSource.value === 'mock') return 'src-mock'
  return dataSource.value === 'local+gitea' ? 'src-gitea' : 'src-local'
})
const sourceTitle = computed(() => {
  if (!hostLinked.value) return fallbackReason.value || '未连接 Host，展示内置 Mock 数据'
  const lines = [`数据源：${dataSource.value || '未声明'}`]
  if (workspaceRoot.value) lines.push(`工作区：${workspaceRoot.value}`)
  if (snapshotAt.value) lines.push(`快照时间：${snapshotAt.value}`)
  if (snapshotError.value) lines.push(`错误：${snapshotError.value}`)
  if (fallbackReason.value) lines.push(fallbackReason.value)
  return lines.join('\n')
})
const snapshotFresh = computed(() => snapshotAt.value ? `Host 快照 · ${formatStamp(snapshotAt.value)}` : '')
const freshnessText = computed(() => hostLinked.value ? (snapshotFresh.value || 'Host 快照') : '刷新：4s 前（Mock）')
const titleFreshness = computed(() => hostLinked.value ? (snapshotFresh.value || 'Host 快照') : '数据新鲜度：本地 4s · 远程 2min（Mock）')
const scopeSummary = computed(() => {
  if (!hostLinked.value) return '组织 MBD_Org · 1,007 组件 / 4,028 仓（Mock）'
  if (snapshotEmpty.value) return 'Host 快照为空（0 组件）· 展示内置 Mock'
  const repoCount = components.value.reduce((sum, component) => sum + component.repos.length, 0)
  return `${components.value.length} 组件 / ${repoCount} 仓（Host 快照）`
})
const componentsNotice = computed(() => {
  if (snapshotEmpty.value) return 'Host 快照返回 0 个组件，当前展示内置 Mock 数据；点击“刷新”可重试。'
  if (hostFailed.value) return `${fallbackReason.value}；可点击“刷新”重试。`
  if (hostLinked.value && dataSource.value !== 'mock') {
    return `数据来自 Host 快照（${sourceLabel.value}）；写操作将在 M2 接入。点击行可回到看板并选中该组件。`
  }
  return 'M0：本页展示组件结构；真实 Gitea 数据与状态计算将在 M1 接入。点击行可回到看板并选中该组件。'
})

// 状态字符串 → CSS class（聚合态 / 单仓态统一映射；未命中一律小写，与 tokens.css 对齐）
const STATE_TONES: Record<string, string> = {
  // §7.4 组件聚合态
  OP_PARTIAL_FAILED: 'partial',
  BINDING_ERROR: 'binding',
  TAG_DRIFT: 'tagdrift',
  BLOCKED_LOCAL: 'blocked',
  MIXED_BRANCH: 'mixed',
  NEW_BRANCH: 'newbranch',
  DETACHED: 'detached',
  EMPTY: 'empty',
  // §7.2 单仓主状态的特殊落点
  DETACHED_TAG: 'detached',
  DETACHED_COMMIT: 'detached',
  UNMERGED: 'blocked',
  IN_PROGRESS: 'blocked',
  NO_UPSTREAM: 'newbranch',
  INVALID: 'binding',
}
const REPO_TYPE_LABELS: Record<string, RepoType> = { DESIGN: 'Design', IMPL: 'Impl', MIL: 'MIL', SIL: 'SIL' }

function chipClass(state: string) {
  return STATE_TONES[state] ?? (state.toLowerCase() || 'unknown')
}
function formatStamp(value: string): string {
  if (!value) return '—'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}`
}
function repoHasChanges(repo: RepoDto): boolean {
  const flags = repo.flags.map(flag => flag.toUpperCase())
  return repo.dirtyCount > 0 || repo.untrackedCount > 0 || flags.some(flag => flag.includes('DIRTY') || flag.includes('UNTRACKED'))
}
function mapRepo(repo: RepoDto): Repo | null {
  const type = REPO_TYPE_LABELS[repo.repoType.toUpperCase()]
  if (!type) return null // 契约只有 4 类；其它类型不纳入组件仓 chip（未绑定仓走独立清单）
  return {
    type,
    state: repo.state,
    branch: repo.branch ?? '—',
    dirty: repoHasChanges(repo),
    headSha: repo.headSha ?? undefined,
    ahead: repo.ahead,
    behind: repo.behind,
    untrackedCount: repo.untrackedCount,
  }
}
function mapComponent(component: ComponentDto, generatedAt: string): ComponentRow {
  const state = component.aggregateState || 'UNKNOWN'
  return {
    id: component.componentId,
    short: component.componentId,
    state,
    rail: chipClass(state),
    repos: component.repos.map(mapRepo).filter((repo): repo is Repo => repo !== null),
    branch: component.branch ?? '—',
    mixed: component.mixedBranch || undefined,
    tag: component.tag ?? undefined,
    ci: 'NONE', // CI 独立通道：v1 统一 NONE，永不参与组件聚合（D-002 / CI-10）
    fresh: `Host 快照 · ${formatStamp(generatedAt)}`,
    severity: component.severityRank,
  }
}
function applySnapshot(snapshot: SnapshotDto): string {
  hostLinked.value = true
  dataSource.value = snapshot.source
  workspaceRoot.value = snapshot.workspaceRoot ?? ''
  snapshotAt.value = snapshot.generatedAt
  snapshotError.value = snapshot.error ?? ''
  fallbackReason.value = ''
  const next = snapshot.components.map(component => mapComponent(component, snapshot.generatedAt))
  if (next.length === 0) {
    // 空快照不整页闪空：保留内置 Mock，并明确告知
    snapshotEmpty.value = true
    return snapshot.error && snapshot.source !== 'mock'
      ? `Host 快照告警：${snapshot.error}`
      : 'Host 快照返回 0 个组件，暂保留内置 Mock 展示'
  }
  snapshotEmpty.value = false
  components.value = next
  if (!next.some(row => row.id === selectedId.value)) selectedId.value = next[0].id
  const current = next.find(row => row.id === selectedId.value) ?? next[0]
  if (!current.repos.some(repo => repo.type === selectedRepo.value) && current.repos.length > 0) {
    selectedRepo.value = current.repos[0].type
  }
  return snapshot.error && snapshot.source !== 'mock' ? `Host 快照告警：${snapshot.error}` : ''
}

function applyInitialSnapshot(snapshot: SnapshotDto) {
  retryIndex = 0
  hostFailed.value = false
  const notice = applySnapshot(snapshot)
  if (snapshot.source === 'mock') {
    fallbackReason.value = `Host 未配置工作区（source=mock）${snapshot.error ? `：${snapshot.error}` : ''}，展示 Mock 数据`
    notify(snapshot.error ? `Host 数据源为 Mock：${snapshot.error}` : 'Host 未配置工作区（source=mock），展示 Mock 数据')
  } else if (notice) {
    notify(notice)
  } else {
    notify(`已连接 Host · ${sourceLabel.value}`)
  }
}

function handleInitialFailure(error: unknown) {
  const reason = toErrorMessage(error)
  hostFailed.value = true
  fallbackReason.value = `读取 Host 快照失败：${reason}（当前展示内置 Mock 数据）`
  if (retryIndex < SNAPSHOT_RETRY_DELAYS_MS.length) {
    const delay = SNAPSHOT_RETRY_DELAYS_MS[retryIndex]
    retryIndex += 1
    notify(`Host 快照尚未就绪（${reason}），${Math.round(delay / 1000)}s 后自动重试`)
    snapshotRetryTimer = setTimeout(() => { void loadInitialSnapshot() }, delay)
  } else {
    notify(`读取 Host 快照失败：${reason}，已回退 Mock；可点击“刷新”重试`)
  }
}

async function loadInitialSnapshot() {
  if (booting.value) return
  booting.value = true
  try {
    const snapshot = normalizeSnapshot(await request<unknown>('getSnapshot', SNAPSHOT_TIMEOUT_MS))
    if (!snapshot) {
      handleInitialFailure(new Error('Host 返回的快照格式无法识别'))
      return
    }
    applyInitialSnapshot(snapshot)
  } catch (error) {
    handleInitialFailure(error)
  } finally {
    booting.value = false
  }
}

async function refreshNow() {
  if (refreshing.value || booting.value) return
  if (snapshotRetryTimer !== undefined) {
    clearTimeout(snapshotRetryTimer)
    snapshotRetryTimer = undefined
  }
  retryIndex = 0
  refreshing.value = true
  notify('正在重新扫描本地仓库…（大工作区约 10–20s）')
  try {
    const reply = normalizeSnapshot(await request<unknown>('refresh', SNAPSHOT_TIMEOUT_MS))
    // refresh 也可能只回 ack（无快照载荷），此时再取一次 getSnapshot
    const snapshot = reply ?? normalizeSnapshot(await request<unknown>('getSnapshot', SNAPSHOT_TIMEOUT_MS))
    if (snapshot) {
      hostFailed.value = false
      const notice = applySnapshot(snapshot)
      notify(notice || `已刷新 · ${sourceLabel.value}`)
    } else {
      notify('刷新已提交，但 Host 未返回可识别快照')
    }
  } catch (error) {
    notify(`刷新失败：${toErrorMessage(error)}`)
  } finally {
    refreshing.value = false
  }
}

onMounted(async () => {
  if (!hasHostBridge()) {
    fallbackReason.value = '当前不在 WebView2 宿主中（浏览器 dev），展示内置 Mock 数据'
    notify(fallbackReason.value)
    return
  }
  notify('正在连接 Host 并扫描本地仓库…（首次可能 10–20s）')
  await loadInitialSnapshot()
})

onBeforeUnmount(() => {
  if (snapshotRetryTimer !== undefined) clearTimeout(snapshotRetryTimer)
})

const unread = computed(() => mockNotifications.value.filter(n => !n.read).length)
const filteredLogs = computed(() => logLevel.value === '全部' ? mockLogs : mockLogs.filter(l => l.level === logLevel.value))
const selected = computed<ComponentRow>(() => components.value.find(c => c.id === selectedId.value) ?? components.value.at(0) ?? mockComponents[0])
const rows = computed(() => {
  const q = search.value.trim().toLowerCase()
  const list = q
    ? components.value.filter(c => c.id.toLowerCase().includes(q) || c.repos.some(r => r.branch.toLowerCase().includes(q)))
    : components.value
  return [...list].sort((a, b) => a.severity - b.severity || a.id.localeCompare(b.id))
})
const selectedRepoRow = computed<Repo | undefined>(() => selected.value.repos.find(r => r.type === selectedRepo.value))

const settingTab = ref('general')
const settings = [
  { key: 'general', label: '通用' },
  { key: 'workspace', label: '工作区' },
  { key: 'auth', label: '认证' },
  { key: 'safety', label: '操作与安全' },
  { key: 'ci', label: 'CI 集成' },
  { key: 'experimental', label: '实验特性' }
]

const navItems: { key: View; label: string; icon: string; badge?: 'unread' }[] = [
  { key: 'board', label: '看板', icon: '▦' },
  { key: 'components', label: '组件', icon: '◫' },
  { key: 'ci', label: 'CI', icon: '⇄' },
  { key: 'tasks', label: '任务', icon: '⇅' },
  { key: 'notifications', label: '通知', icon: '✉', badge: 'unread' },
  { key: 'logs', label: '日志', icon: '≣' },
  { key: 'settings', label: '设置', icon: '⚙' }
]

function ciLabel(ci: CiState) {
  return ({ PASS: 'CI 通过', FAIL: 'CI 失败', RUNNING: 'CI 运行中', QUEUED: 'CI 排队', NONE: '无 CI', UNKNOWN: 'CI 未知' } as Record<CiState, string>)[ci]
}
function ciDot(ci: CiState) { return ci.toLowerCase() }
function markAllRead() { mockNotifications.value.forEach(n => { n.read = true }) }
function openComponent(id: string) { selectedId.value = id; view.value = 'board' }
</script>

<template>
  <div class="app">
    <header class="titlebar">
      <span class="brand">MBD Git Tool</span>
      <span class="pill">v0.2.1 · M1</span>
      <span v-if="booting" class="pill src-local">扫描中…</span>
      <span class="pill" :class="sourceTone" data-testid="snapshot-source" :title="sourceTitle">{{ sourceLabel }}</span>
      <span class="meta">{{ scopeSummary }}</span>
      <span v-if="workspaceRoot" class="meta ws" :title="workspaceRoot">工作区：{{ workspaceRoot }}</span>
      <span v-if="snapshotError && dataSource !== 'mock'" class="pill src-warn" :title="snapshotError">快照告警</span>
      <span class="spacer" />
      <span class="meta">{{ titleFreshness }}</span>
    </header>

    <nav class="rail" aria-label="主导航">
      <button v-for="item in navItems" :key="item.key"
              :data-testid="item.key === 'ci' ? 'ci-nav' : undefined"
              :class="{ active: view === item.key }"
              :title="item.label"
              @click="view = item.key">
        <span class="ico">{{ item.icon }}</span>{{ item.label }}
        <span v-if="item.badge === 'unread' && unread > 0" class="badge">{{ unread }}</span>
      </button>
    </nav>

    <main class="main">
      <!-- 看板 -->
      <template v-if="view === 'board'">
        <div class="page-head">
          <h1>组件看板</h1>
          <span class="sub">异常优先排序 · {{ components.length }} 个组件 · {{ sourceLabel }}</span>
        </div>

        <div v-if="booting" class="notice">正在连接 Host 并扫描本地仓库…（首次约 10–20s，超大工作区更久）</div>
        <div v-else-if="hostFailed" class="notice notice-warn">{{ fallbackReason }}；将自动重试，也可点击“刷新”。</div>
        <div v-if="snapshotEmpty" class="notice">Host 快照返回 0 个组件（工作区可能为空或首扫未完成），当前展示内置 Mock 数据；点击“刷新”可重试。</div>

        <div class="toolbar">
          <input type="search" v-model="search" placeholder="搜索组件 / 仓库 / 分支 / Tag（Ctrl+K）" />
          <select aria-label="聚合状态" @change="notify('筛选将在 M1 接入真实数据后启用')"><option>全部状态</option><option>异常优先</option></select>
          <select aria-label="仓类型" @change="notify('筛选将在 M1 接入真实数据后启用')"><option>全部仓类型</option><option>Design</option><option>Impl</option><option>MIL</option><option>SIL</option></select>
          <select data-testid="ci-facet" disabled title="CI 集成将在后续版本开放"><option>CI 状态（即将支持）</option></select>
          <div class="seg"><button class="active">表格</button><button @click="notify('卡片视图将在 M1 接入')">卡片</button></div>
          <button class="btn" data-testid="snapshot-refresh" :disabled="refreshing || booting"
                  :title="booting ? '正在扫描本地仓库…' : (hostLinked ? '从 Host 重新获取快照（触发本地重扫，大工作区约 10–20s）' : '未连接 Host（当前为 Mock 数据）')"
                  @click="refreshNow">{{ booting ? '扫描中…' : refreshing ? '刷新中…' : '刷新' }}</button>
          <span class="fresh">{{ freshnessText }}</span>
        </div>

        <div class="grid2">
          <div>
            <table class="table" data-testid="ci-card-base" data-ci-affects-base="false">
              <thead>
                <tr>
                  <th style="width:28px"><input type="checkbox" aria-label="全选" @change="notify('多选批量操作将在 M2 接入')" /></th>
                  <th>组件</th><th>聚合状态</th><th>生命周期仓库</th><th>分支 / Tag</th><th>CI</th><th>数据新鲜度</th><th>操作</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="c in rows" :key="c.id" @click="selectedId = c.id">
                  <td><input type="checkbox" :aria-label="`选择 ${c.id}`" @click.stop /></td>
                  <td>
                    <div class="comp">
                      <span class="railbar" :class="c.rail" />
                      <div>
                        <div class="name">{{ c.short }}</div>
                        <div class="dir">{{ c.id === selectedId ? '选中' : '组件' }} · {{ c.repos.length }}/4 仓</div>
                      </div>
                    </div>
                  </td>
                  <td><span class="chip t" :class="chipClass(c.state)">{{ c.state }}</span></td>
                  <td>
                    <div class="chips">
                      <span v-for="r in c.repos" :key="r.type" class="chip" :class="chipClass(r.state)" :title="`${r.type}: ${r.state}（${r.branch}）`">
                        {{ r.type[0] }}<template v-if="r.dirty">●</template>
                      </span>
                    </div>
                  </td>
                  <td>
                    <div>{{ c.branch }} <span v-if="c.mixed" class="chip mixed" title="四仓分支不一致">⑂ 不一致</span></div>
                    <div class="dir" v-if="c.tag">Tag {{ c.tag }}</div>
                  </td>
                  <td class="ci-slot" data-testid="ci-slot">
                    <span class="ci-badge"><span class="ci-dot" :class="ciDot(c.ci)" />{{ ciLabel(c.ci) }}</span>
                  </td>
                  <td class="fresh">{{ c.fresh }}</td>
                  <td>
                    <div class="actions">
                      <button @click.stop="notify('M0 骨架：SYNC 将在 M1/M2 接入（需 dry-run + 备份 ref）')" title="SYNC 到远程最新（M1/M2 接入）">同步</button>
                      <button @click.stop="notify('M0 骨架：创建分支 / 删除 等操作将在 M2 接入')" title="更多：创建分支 / 删除（M2 接入）">⋯</button>
                    </div>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>

          <aside class="panel">
            <h2>组件详情 · {{ selected.short }}</h2>
            <div class="ci-strip" data-testid="ci-strip">
              <span class="label">CI 汇总（占位）</span>
              <span class="ci-badge"><span class="ci-dot pass" />1 通过</span>
              <span class="ci-badge"><span class="ci-dot fail" />1 失败</span>
              <span class="ci-badge"><span class="ci-dot running" />1 运行中</span>
              <span class="fresh">CI 未接入（v1 占位）</span>
            </div>
            <div class="tabs">
              <button v-for="t in (['Design','Impl','MIL','SIL'] as RepoType[])" :key="t" :class="{ active: selectedRepo === t }" @click="selectedRepo = t">{{ t }}</button>
              <button data-testid="ci-tab" title="CI 集成将在后续版本开放" @click="notify('单仓 CI Tab 为 v1 占位，M3 后接入')">CI</button>
            </div>
            <div v-if="selectedRepoRow" class="panel" style="box-shadow:none">
              <div class="field"><label>主状态</label><div><span class="chip t" :class="chipClass(selectedRepoRow.state)">{{ selectedRepoRow.state }}</span></div></div>
              <div class="field"><label>当前分支</label><div>{{ selectedRepoRow.branch }}</div></div>
              <div class="field"><label>HEAD</label><div class="dir">{{ selectedRepoRow.headSha ? selectedRepoRow.headSha.slice(0, 7) : '—' }}</div></div>
              <div class="field"><label>未提交改动</label><div>{{ selectedRepoRow.dirty ? '有（●）' : '无' }}<span v-if="selectedRepoRow.untrackedCount" class="dir"> · 未跟踪 {{ selectedRepoRow.untrackedCount }}</span></div></div>
              <div v-if="selectedRepoRow.ahead !== undefined || selectedRepoRow.behind !== undefined" class="field">
                <label>ahead / behind</label>
                <div class="dir">↑ {{ selectedRepoRow.ahead ?? 0 }} / ↓ {{ selectedRepoRow.behind ?? 0 }}（相对 compare_ref）</div>
              </div>
            </div>
            <div v-else class="panel" style="box-shadow:none">
              <div class="hint">该生命周期仓库未出现在 Host 快照中（缺仓 / 未扫描），无法展示单仓详情。</div>
            </div>
            <div class="panel" style="box-shadow:none;margin-top:12px" data-testid="ci-notif-category">
              <h2>通知</h2>
              <div class="hint">v1 通知中心；CI 事件类目为空占位（CI 未接入）。</div>
            </div>
          </aside>
        </div>
      </template>

      <!-- 组件列表 -->
      <template v-else-if="view === 'components'">
        <div class="page-head"><h1>组件列表</h1><span class="sub">组件视角聚合 · {{ components.length }} 个组件（{{ hostLinked ? sourceLabel : '内置 Mock' }}）</span></div>
        <div class="notice">{{ componentsNotice }}</div>
        <table class="table">
          <thead><tr><th>组件 ID</th><th>聚合状态</th><th>4 仓</th><th>分支 / Tag</th><th>CI</th><th>新鲜度</th></tr></thead>
          <tbody>
            <tr v-for="c in components" :key="c.id" style="cursor:pointer" @click="openComponent(c.id)">
              <td><div class="comp"><span class="railbar" :class="c.rail" /><span class="name">{{ c.short }}</span></div></td>
              <td><span class="chip t" :class="chipClass(c.state)">{{ c.state }}</span></td>
              <td><div class="chips"><span v-for="r in c.repos" :key="r.type" class="chip" :class="chipClass(r.state)">{{ r.type[0] }}</span></div></td>
              <td>{{ c.branch }}<span v-if="c.tag" class="dir"> · Tag {{ c.tag }}</span></td>
              <td><span class="ci-badge"><span class="ci-dot" :class="ciDot(c.ci)" />{{ ciLabel(c.ci) }}</span></td>
              <td class="fresh">{{ c.fresh }}</td>
            </tr>
          </tbody>
        </table>
      </template>

      <!-- 任务中心 -->
      <template v-else-if="view === 'tasks'">
        <div class="page-head"><h1>任务中心</h1><span class="sub">批量操作队列 · 当前 {{ taskCount }} 个任务</span></div>
        <div class="notice">M0：任务队列/进度/取消/重试在 M2 接入；此处先展示结构与空态。</div>
        <div class="empty-page">
          <div class="empty-icon">◷</div>
          <div class="empty-title">暂无进行中的任务</div>
          <div class="empty-desc">批量 Clone / Pull / 切 Tag / SYNC / 删除 将在 M2 接入后在此显示逐仓进度与失败明细。</div>
        </div>
      </template>

      <!-- 通知中心 -->
      <template v-else-if="view === 'notifications'">
        <div class="page-head">
          <h1>通知中心</h1>
          <span class="sub">{{ unread }} 条未读 · 应用内通知（M0 Mock）</span>
          <span class="spacer" />
          <button class="btn" @click="markAllRead">全部已读</button>
        </div>
        <div class="notice">M0：通知为 Mock；真实通知（状态恶化 / Token 过期 / 批量完成 / CI 事件）在 M1/M2 接入。</div>
        <div class="notif-list">
          <div v-for="n in mockNotifications" :key="n.id" class="notif" :class="{ unread: !n.read }">
            <span class="dot" :class="n.level === 'err' ? 'err' : n.level === 'warn' ? 'warn' : ''" />
            <div class="notif-body">
              <div class="notif-title">{{ n.title }} <span class="tag">{{ n.type }}</span></div>
              <div class="notif-detail">{{ n.detail }}</div>
            </div>
            <span class="fresh">{{ n.at }}</span>
          </div>
          <div class="notif" data-testid="ci-notif-category">
            <span class="dot off" />
            <div class="notif-body">
              <div class="notif-title">CI 事件 <span class="tag">占位</span></div>
              <div class="notif-detail">CI 失败 / 通过 / 超时通知将在 v2 接入；此处为类目占位。</div>
            </div>
            <span class="fresh">—</span>
          </div>
        </div>
      </template>

      <!-- 日志与诊断 -->
      <template v-else-if="view === 'logs'">
        <div class="page-head"><h1>日志与诊断</h1><span class="sub">结构化日志（M0 Mock）</span></div>
        <div class="toolbar">
          <select v-model="logLevel" aria-label="级别过滤"><option>全部</option><option>INFO</option><option>WARN</option><option>ERROR</option><option>DEBUG</option></select>
          <button @click="notify('导出脱敏诊断包将在 M3 接入')">导出诊断包（M3）</button>
          <span class="fresh">保留：运行日志 30 天 / 审计 90 天</span>
        </div>
        <div class="log-list">
          <div v-for="(l, i) in filteredLogs" :key="i" class="log-line">
            <span class="log-at">{{ l.at }}</span>
            <span class="log-level" :class="l.level.toLowerCase()">{{ l.level }}</span>
            <span class="log-msg">{{ l.msg }}</span>
          </div>
        </div>
      </template>

      <!-- CI 流水线 -->
      <template v-else-if="view === 'ci'">
        <div class="page-head"><h1>CI 流水线</h1><span class="sub">v1 占位页 · 数据契约与锚点已冻结</span></div>
        <div class="panel" style="margin-bottom:12px">
          <h2>CI 集成（v1 仅占位）</h2>
          <p class="hint">当前版本不调用真实 CI API；以下结构已按 SRS 预留，接入 v2 时仅替换数据源与开关，不改导航/卡片/聚合语义。</p>
          <div class="ci-contract" data-testid="ci-contract">CiLatest { repoId: number; componentId: string; repoType: 'DESIGN'|'IMPL'|'MIL'|'SIL'; branch; commitHash; state: PASS|FAIL|RUNNING|QUEUED|NONE|UNKNOWN; provider; fetchedAt }
ICiProvider { getLatest(componentId); getAggregate(componentId); subscribe(CI_UPDATED) }</div>
        </div>
        <div class="ci-page-empty">
          <div>
            <div class="big">CI 流水线尚未接入</div>
            <div>该页面为 v1 预留（CI-1）；接入后显示按组件/仓库/commit 聚合的流水线列表。</div>
            <div style="margin-top:8px"><code>feature flag: ci.enabled = false</code></div>
          </div>
        </div>
      </template>

      <!-- 设置 -->
      <template v-else>
        <div class="page-head"><h1>设置</h1><span class="sub">v1 设置页骨架（M0 Mock，CI 分区为禁用占位）</span></div>
        <div class="settings">
          <div class="list">
            <button v-for="s in settings" :key="s.key" :class="{ active: settingTab === s.key }" @click="settingTab = s.key">{{ s.label }}</button>
          </div>

          <div v-if="settingTab === 'general'" class="panel">
            <h2>通用</h2>
            <div class="field"><label>主题</label><select disabled><option>跟随系统</option></select></div>
            <div class="field"><label>界面密度</label><select disabled><option>标准</option></select></div>
            <div class="field"><label>语言</label><select disabled><option>简体中文</option></select></div>
            <div class="hint">M0：主题 token 已生效（跟随系统浅/深色）；其余项 M1 接入。</div>
          </div>

          <div v-else-if="settingTab === 'workspace'" class="panel">
            <h2>工作区</h2>
            <div class="field"><label>本地根目录</label><input disabled :value="workspaceRoot || '（Host 未返回工作区根目录）'" /></div>
            <div class="field"><label>扫描频率</label><input disabled value="指纹 15s / 深扫变化触发" /></div>
            <div class="hint">M1 接入真实目录扫描与首扫三阶段进度。</div>
          </div>

          <div v-else-if="settingTab === 'auth'" class="panel">
            <h2>认证</h2>
            <div class="field"><label>Gitea 地址</label><input disabled value="https://gitea.example.com（示例）" /></div>
            <div class="field"><label>组织</label><input disabled value="MBD_Org" /></div>
            <div class="field"><label>Token 状态</label><input disabled value="已保存（Windows 凭据管理器 / MOCK）" /></div>
            <div class="hint">M1 接入真实连接测试与 PAT 校验；Token 不落配置文件。</div>
          </div>

          <div v-else-if="settingTab === 'safety'" class="panel">
            <h2>操作与安全</h2>
            <div class="field"><label>删除保留期</label><input disabled value="30 天（.trash 可恢复）" /></div>
            <div class="field"><label>备份 ref 保留</label><input disabled value="30 天（refs/backup/*）" /></div>
            <div class="field"><label>强制覆盖确认</label><input type="checkbox" disabled checked /></div>
            <div class="hint">M2 接入 dry-run / 备份 / 恢复。</div>
          </div>

          <div v-else-if="settingTab === 'ci'" class="panel" data-testid="ci-settings">
            <h2>CI 集成</h2>
            <p class="hint">v1 禁用；接入 v2 后启用。Token 仅存 Windows 凭据管理器，不落配置文件。</p>
            <div class="field"><label>CI 类型</label><select disabled><option>未接入（v1）</option></select></div>
            <div class="field"><label>Base URL</label><input disabled placeholder="https://ci.example.com" /></div>
            <div class="field"><label>Token</label><input disabled placeholder="凭据管理器引用" /></div>
            <div class="field"><label>轮询间隔（秒）</label><input disabled value="60" /></div>
            <div class="field"><label>启用 CI</label><input type="checkbox" disabled /></div>
            <div class="notice" data-testid="ci-contract">CI 独立通道：永不参与组件聚合与卡片底色；空/未知使用中性样式。契约与 DDL 映射见 SRS §8.3 / §11.4。</div>
          </div>

          <div v-else class="panel">
            <h2>实验特性</h2>
            <div class="hint">v1 暂无可配置实验项（对应 SRS 附录 D：实验特性分区无可配置键）。</div>
          </div>
        </div>
      </template>
    </main>

    <footer class="statusbar">
      <span class="seg"><span class="dot" :class="hostLinked ? '' : 'off'" />进程健康 Host/GitWorker</span>
      <span class="seg"><span class="dot warn" />RemoteSync 同步中</span>
      <span class="seg">{{ hostLinked ? (snapshotFresh || 'Host 快照') : '本地 4s 前 · 远程 2min 前（Mock）' }}</span>
      <span class="spacer" />
      <span class="seg">任务队列 {{ taskCount }}</span>
      <span class="seg" data-testid="ci-queue"><span class="ci-dot none" />CI 队列 0</span>
      <span class="seg">通知 {{ unread }}</span>
      <span class="seg"><span class="dot" />在线</span>
    </footer>

    <transition name="toast">
      <div v-if="toast" class="toast" role="status">{{ toast }}</div>
    </transition>
  </div>
</template>

<style scoped>
.titlebar .ws {
  max-width: 320px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.titlebar .meta { white-space: nowrap; }
.pill.src-mock { color: var(--st-neutral-fg); background: var(--st-neutral-bg); border-color: var(--st-neutral-bd); }
.pill.src-local { color: var(--st-info-fg); background: var(--st-info-bg); border-color: var(--st-info-bd); }
.pill.src-gitea { color: var(--st-success-fg); background: var(--st-success-bg); border-color: var(--st-success-bd); }
.pill.src-warn { color: var(--st-warning-fg); background: var(--st-warning-bg); border-color: var(--st-warning-bd); }
.notice.notice-warn { border-color: var(--st-warning-bd); background: var(--st-warning-bg); color: var(--st-warning-fg); }
.toolbar button:disabled { opacity: 0.55; cursor: default; }
.tabs button:disabled { opacity: 0.55; cursor: default; }
</style>
