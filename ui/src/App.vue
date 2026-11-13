<script setup lang="ts">
import { computed, ref } from 'vue'

type RepoState = 'SYNCED' | 'BEHIND' | 'AHEAD' | 'DIVERGED' | 'MISSING' | 'EMPTY' | 'DIRTY' | 'DETACHED_TAG' | 'NO_UPSTREAM' | 'UNKNOWN' | 'UNMERGED' | 'IN_PROGRESS' | 'INVALID'
type CiState = 'PASS' | 'FAIL' | 'RUNNING' | 'QUEUED' | 'NONE' | 'UNKNOWN'
type RepoType = 'Design' | 'Impl' | 'MIL' | 'SIL'
type View = 'board' | 'components' | 'ci' | 'tasks' | 'notifications' | 'logs' | 'settings'

type Repo = { type: RepoType; state: RepoState; branch: string; tag?: string; dirty?: boolean }
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

const unread = computed(() => mockNotifications.value.filter(n => !n.read).length)
const filteredLogs = computed(() => logLevel.value === '全部' ? mockLogs : mockLogs.filter(l => l.level === logLevel.value))
const selected = computed(() => mockComponents.find(c => c.id === selectedId.value) ?? mockComponents[0])
const rows = computed(() => {
  const q = search.value.trim().toLowerCase()
  const list = q
    ? mockComponents.filter(c => c.id.toLowerCase().includes(q) || c.repos.some(r => r.branch.toLowerCase().includes(q)))
    : mockComponents
  return [...list].sort((a, b) => a.severity - b.severity || a.id.localeCompare(b.id))
})
const selectedRepoRow = computed(() => selected.value.repos.find(r => r.type === selectedRepo.value)!)

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

function chipClass(state: string) { return state.toLowerCase() }
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
      <span class="pill">v0.2.1 · M0</span>
      <span class="meta">组织 MBD_Org · 1,007 组件 / 4,028 仓</span>
      <span class="spacer" />
      <span class="meta">数据新鲜度：本地 4s · 远程 2min</span>
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
          <span class="sub">异常优先排序 · 共 7 个示例组件（M0 Mock）</span>
        </div>

        <div class="toolbar">
          <input type="search" v-model="search" placeholder="搜索组件 / 仓库 / 分支 / Tag（Ctrl+K）" />
          <select aria-label="聚合状态" @change="notify('筛选将在 M1 接入真实数据后启用')"><option>全部状态</option><option>异常优先</option></select>
          <select aria-label="仓类型" @change="notify('筛选将在 M1 接入真实数据后启用')"><option>全部仓类型</option><option>Design</option><option>Impl</option><option>MIL</option><option>SIL</option></select>
          <select data-testid="ci-facet" disabled title="CI 集成将在后续版本开放"><option>CI 状态（即将支持）</option></select>
          <div class="seg"><button class="active">表格</button><button @click="notify('卡片视图将在 M1 接入')">卡片</button></div>
          <span class="fresh">刷新：4s 前</span>
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
            <div class="panel" style="box-shadow:none">
              <div class="field"><label>主状态</label><div><span class="chip t" :class="chipClass(selectedRepoRow.state)">{{ selectedRepoRow.state }}</span></div></div>
              <div class="field"><label>当前分支</label><div>{{ selectedRepoRow.branch }}</div></div>
              <div class="field"><label>HEAD</label><div class="dir">3f9a2c1（示例）</div></div>
              <div class="field"><label>未提交改动</label><div>{{ selectedRepoRow.dirty ? '有（●）' : '无' }}</div></div>
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
        <div class="page-head"><h1>组件列表</h1><span class="sub">组件视角聚合 · {{ mockComponents.length }} 个 Mock 组件</span></div>
        <div class="notice">M0：本页展示组件结构；真实 Gitea 数据与状态计算将在 M1 接入。点击行可回到看板并选中该组件。</div>
        <table class="table">
          <thead><tr><th>组件 ID</th><th>聚合状态</th><th>4 仓</th><th>分支 / Tag</th><th>CI</th><th>新鲜度</th></tr></thead>
          <tbody>
            <tr v-for="c in mockComponents" :key="c.id" style="cursor:pointer" @click="openComponent(c.id)">
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
            <div class="field"><label>本地根目录</label><input disabled value="D:\\Work\\MBD（示例）" /></div>
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
      <span class="seg"><span class="dot" />进程健康 Host/GitWorker</span>
      <span class="seg"><span class="dot warn" />RemoteSync 同步中</span>
      <span class="seg">本地 4s 前 · 远程 2min 前</span>
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
