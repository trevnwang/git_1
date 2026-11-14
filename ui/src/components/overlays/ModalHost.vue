<script setup lang="ts">
import { computed, nextTick, ref, watch } from 'vue'
import { useWorkbench } from '../../stores/workbench'
import { mockHostInfo } from '../../mocks/workbenchMock'
import { auditActionLabel, auditResultMeta, formatAgo, formatStamp, REPO_LABEL, shortSha } from '../../viewmodel'

const w = useWorkbench()
const state = w.state
const dialog = ref<HTMLElement | null>(null)
const logLevel = ref('全部')

const kind = computed(() => state.overlays.modal)
const hostInfo = computed(() => state.hostInfo ?? mockHostInfo())

// ── M2b-2 操作审计（「日志与诊断」内嵌） ──
const audit = computed(() => state.audit)
const auditSourceLabel = computed(() => (audit.value.source === 'mock' ? 'Mock 审计' : 'Host 审计'))
const auditStatusLabel = computed(() => {
  switch (audit.value.status) {
    case 'loading':
      return '正在读取 operation_log…'
    case 'ready':
      return `更新于 ${audit.value.at > 0 ? formatAgo(new Date(audit.value.at).toISOString(), state.now) : '—'}`
    case 'unavailable':
      return 'Host 未实现审计查询'
    case 'error':
      return '审计查询失败'
    default:
      return '尚未加载'
  }
})
const AUDIT_RESULT_OPTIONS = ['OK', 'FAILED', 'SKIPPED', 'CANCELED', 'INTERRUPTED'] as const

function repoTypeLabel(repoType: string): string {
  return ((REPO_LABEL as Record<string, string>)[repoType] ?? repoType) || '—'
}

function auditResult(result: string): { tone: string; label: string } {
  return auditResultMeta(result)
}

function onAuditActionInput(event: Event): void {
  w.setAuditActionFilter((event.target as HTMLInputElement).value)
}

function onAuditActionApply(event: Event): void {
  w.setAuditActionFilter((event.target as HTMLInputElement).value)
  w.applyAuditFilters()
}

function onAuditResultChange(event: Event): void {
  w.setAuditResultFilter((event.target as HTMLSelectElement).value)
  w.applyAuditFilters()
}

function exportAudit(format: 'csv' | 'json'): void {
  void w.exportAudit(format)
}
const title = computed(() => {
  switch (kind.value) {
    case 'ci':
      return 'CI 流水线'
    case 'settings':
      return '设置'
    case 'logs':
      return '日志与诊断'
    case 'unbound':
      return '未绑定仓库'
    case 'about':
      return '关于 MBD Git Tool'
    case 'shortcuts':
      return '快捷键'
    default:
      return ''
  }
})
const sizeClass = computed(() => {
  switch (kind.value) {
    case 'settings':
      return 'modal-md'
    case 'about':
    case 'shortcuts':
      return 'modal-sm'
    default:
      return 'modal-lg'
  }
})
const logs = [
  { level: 'INFO', at: '17:32:02', msg: 'Host started; webview2 initialized（Mock）' },
  { level: 'INFO', at: '17:32:03', msg: 'RemoteSync: org MBD_Org list watermark reached（Mock）' },
  { level: 'WARN', at: '17:32:18', msg: 'LocalScan: safe.directory rejected for D:\\Work\\legacy\\X_Design（Mock）' },
  { level: 'ERROR', at: '17:32:24', msg: 'SYNC-E002: 429 rate limited, retry in 30s（Mock）' },
  { level: 'INFO', at: '17:32:30', msg: 'StatusCalc: 7 components / 28 repos, dirty-set 3（Mock）' },
  { level: 'DEBUG', at: '17:32:31', msg: 'job queue depth=0, db size=12.4MB（Mock）' },
]
const filteredLogs = computed(() => (logLevel.value === '全部' ? logs : logs.filter((line) => line.level === logLevel.value)))

const settingsSections = [
  { key: 'general', label: '通用' },
  { key: 'workspace', label: '工作区' },
  { key: 'auth', label: '认证' },
  { key: 'safety', label: '操作与安全' },
  { key: 'ci', label: 'CI 集成' },
  { key: 'experimental', label: '实验特性' },
]

const scope = computed(() => (state.focusedRepoType ? state.focusedRepoType : 'IMPL'))

const anchors = [
  ['CI-1', '标题区 CI 入口 / 菜单栏 工具- CI 流水线', 'ci-nav'],
  ['CI-2', '组件卡 L1 右侧 18×18 槽位', 'ci-slot'],
  ['CI-3', '右栏顶部 40px 汇总条', 'ci-strip'],
  ['CI-4', '右栏 Tab 第 4 项', 'ci-tab'],
  ['CI-5', '状态栏 CI 队列段', 'ci-queue'],
  ['CI-6', '通知抽屉 CI 事件类目', 'ci-notif-category'],
  ['CI-7', '左栏筛选 popover CI facet', 'ci-facet'],
  ['CI-8', '设置弹窗 CI 集成分区', 'ci-settings'],
  ['CI-9', 'CI 弹窗契约块', 'ci-contract'],
  ['CI-10', '组件列表容器（data-ci-affects-base=false）', 'ci-card-base'],
]

function focusFirst(): void {
  void nextTick(() => {
    const node = dialog.value?.querySelector<HTMLElement>('button, input, select, [tabindex="0"]')
    node?.focus()
  })
}

function trapTab(event: KeyboardEvent): void {
  const root = dialog.value
  if (!root) return
  const focusables = [...root.querySelectorAll<HTMLElement>('button, input, select, textarea, [tabindex="0"]')].filter(
    (node) => !node.hasAttribute('disabled') && node.offsetParent !== null,
  )
  if (focusables.length === 0) return
  const first = focusables[0]
  const last = focusables[focusables.length - 1]
  if (event.shiftKey && document.activeElement === first) {
    event.preventDefault()
    last.focus()
  } else if (!event.shiftKey && document.activeElement === last) {
    event.preventDefault()
    first.focus()
  }
}

watch(kind, (value) => {
  if (value) focusFirst()
})
</script>

<template>
  <div v-if="kind" class="modal-overlay" @click.self="w.closeModal()" @keydown.tab="trapTab">
    <section
      ref="dialog"
      class="modal"
      :class="sizeClass"
      data-testid="modal"
      role="dialog"
      aria-modal="true"
      :aria-label="title"
    >
      <header class="modal-head">
        <span class="modal-title">{{ title }}</span>
        <span v-if="kind === 'ci'" class="experiment">实验</span>
        <span class="spacer" />
        <button type="button" class="icon-btn" title="关闭（Esc）" aria-label="关闭" @click="w.closeModal()">✕</button>
      </header>

      <!-- CI 流水线（CI-1 落点 / CI-9 契约） -->
      <div v-if="kind === 'ci'" class="modal-body" data-testid="ci-page">
        <nav class="breadcrumb" aria-label="面包屑">工具 / CI 流水线</nav>
        <p class="hint">
          当前版本不调用真实 CI API；结构按 SRS 预留，接入 v2 时仅替换数据源与开关，不改导航/卡片/聚合语义。
        </p>
        <div class="ci-empty">
          <div class="big">CI 流水线尚未接入</div>
          <div class="muted">该页面为 v1 预留（CI-1）；接入后显示按组件/仓库/commit 聚合的流水线列表。</div>
          <div class="muted mono">feature flag: ci.enabled = false</div>
        </div>
        <h4 class="section">数据契约（CI-9）</h4>
        <pre class="contract" data-testid="ci-contract">CiLatest { repoId: number; componentId: string; repoType: 'DESIGN'|'IMPL'|'MIL'|'SIL'; branch; commitHash;
  state: 'PASS'|'FAIL'|'RUNNING'|'QUEUED'|'NONE'|'UNKNOWN'; pipelineId?; buildNumber?; failedStage?;
  startedAt?; finishedAt?; durationMs?; url?; provider; fetchedAt }
ComponentCiAggregate { componentId; byState: Record&lt;CiState, number&gt;; worstState; lastFailed?; dataFreshAt }
ICiProvider { getLatest(componentId); getAggregate(componentId); subscribe('CI_UPDATED') }  // v1 空实现</pre>
        <h4 class="section">10 个 CI 锚点新落点</h4>
        <table class="anchor-table">
          <thead><tr><th>#</th><th>新落点</th><th>data-testid</th></tr></thead>
          <tbody>
            <tr v-for="row in anchors" :key="row[0]">
              <td class="mono">{{ row[0] }}</td>
              <td>{{ row[1] }}</td>
              <td class="mono">{{ row[2] }}</td>
            </tr>
          </tbody>
        </table>
        <p class="hint">CI 独立通道：永不参与组件聚合、卡片底色与严重度排序（CI-10）。</p>
      </div>

      <!-- 设置 -->
      <div v-else-if="kind === 'settings'" class="modal-body settings-body">
        <div class="settings-layout">
          <nav class="settings-nav" aria-label="设置分区">
            <a v-for="section in settingsSections" :key="section.key" :href="`#settings-${section.key}`">{{ section.label }}</a>
          </nav>
          <div class="settings-sections">
            <section id="settings-general" class="settings-section">
              <h3>通用</h3>
              <div class="field"><label>主题</label><select disabled><option>跟随系统（v1）</option></select></div>
              <div class="field"><label>界面密度</label><select disabled><option>标准</option></select></div>
              <div class="field"><label>语言</label><select disabled><option>简体中文</option></select></div>
              <p class="hint">主题 token 已生效（跟随系统浅/深色）；其余项 M1 接入。</p>
            </section>
            <section id="settings-workspace" class="settings-section">
              <h3>工作区</h3>
              <div class="field"><label>本地根目录</label><input disabled :value="state.workspaceRoot || '（Host 未返回工作区根目录）'" /></div>
              <div class="field"><label>扫描频率</label><input disabled value="指纹 15s / 深扫变化触发" /></div>
              <p class="hint">M1 已接入真实目录扫描；首扫三阶段进度在 M2/M3 收口。</p>
            </section>
            <section id="settings-auth" class="settings-section">
              <h3>认证</h3>
              <div class="field"><label>Gitea 地址</label><input disabled :value="state.giteaBaseUrl || '（Host 未返回）'" /></div>
              <div class="field"><label>组织</label><input disabled :value="state.owner || '（未配置）'" /></div>
              <div class="field"><label>Token 状态</label><input disabled value="已注入（环境变量/凭据管理器；值不展示）" /></div>
              <p class="hint">M2 已启用单仓写操作（PULL/FETCH/CHKOUT/COMMIT/PUSH）；Token 仅经环境变量/凭据引用，不落配置文件。</p>
            </section>
            <section id="settings-safety" class="settings-section">
              <h3>操作与安全</h3>
              <div class="field"><label>删除保留期</label><input disabled value="30 天（.trash 可恢复）" /></div>
              <div class="field"><label>备份 ref 保留</label><input disabled value="30 天（refs/backup/*）" /></div>
              <div class="field"><label>强制覆盖确认</label><input type="checkbox" disabled checked /></div>
              <p class="hint">dry-run / 备份 / 恢复：由 Host 作业与任务中心提供（备份 ref 保留 30 天）。</p>
            </section>
            <section id="settings-ci" class="settings-section" data-testid="ci-settings">
              <h3>CI 集成 <span class="experiment">实验</span></h3>
              <p class="hint">v1 禁用；接入 v2 后启用。Token 仅存 Windows 凭据管理器，不落配置文件。</p>
              <div class="field"><label>CI 类型</label><select disabled><option>未接入（v1）</option></select></div>
              <div class="field"><label>Base URL</label><input disabled placeholder="https://ci.example.com" /></div>
              <div class="field"><label>Token</label><input disabled placeholder="凭据管理器引用" /></div>
              <div class="field"><label>轮询间隔（秒）</label><input disabled value="60" /></div>
              <div class="field"><label>启用 CI</label><input type="checkbox" disabled /></div>
              <p class="hint">CI 独立通道：永不参与组件聚合与卡片底色；空/未知使用中性样式（CI-10）。</p>
            </section>
            <section id="settings-experimental" class="settings-section">
              <h3>实验特性</h3>
              <p class="hint">v1 暂无可配置实验项（对应 SRS 附录 D：实验特性分区无可配置键）。</p>
            </section>
          </div>
        </div>
      </div>

      <!-- 日志与诊断（含 M2b-2 操作审计） -->
      <div v-else-if="kind === 'logs'" class="modal-body" data-testid="logs-modal">
        <div class="toolbar">
          <select v-model="logLevel" aria-label="日志级别过滤">
            <option>全部</option><option>INFO</option><option>WARN</option><option>ERROR</option><option>DEBUG</option>
          </select>
          <button type="button" class="drawer-btn" @click="w.notify('导出脱敏诊断包将在 M3 接入', 'info')">导出诊断包（M3）</button>
          <span class="muted">保留：运行日志 30 天 / 审计 90 天</span>
        </div>
        <div class="log-list" role="log" aria-label="结构化日志">
          <div v-for="(line, index) in filteredLogs" :key="index" class="log-line">
            <span class="log-at">{{ line.at }}</span>
            <span class="log-level" :class="line.level.toLowerCase()">{{ line.level }}</span>
            <span class="log-msg">{{ line.msg }}</span>
          </div>
        </div>

        <!-- 操作审计（M2b-2：Host operation_log 查询/过滤/分页/导出；不伪造数据） -->
        <section class="audit-section" data-testid="audit-section" aria-label="操作审计">
          <div class="audit-head">
            <h4 class="section">操作审计 <span class="hint">（谁 / 何时 / 组件 / 仓 / 前后 HEAD / 结果 / 错误码 / job）</span></h4>
            <span class="spacer" />
            <span class="audit-source" data-testid="audit-source">{{ auditSourceLabel }}</span>
            <span class="audit-status" data-testid="audit-status">{{ auditStatusLabel }}</span>
          </div>
          <div class="audit-toolbar">
            <input
              class="audit-input"
              data-testid="audit-filter-action"
              type="text"
              placeholder="动作过滤（如 PUSH / SYNC）"
              aria-label="按动作过滤审计"
              :value="audit.filters.action"
              @input="onAuditActionInput"
              @change="onAuditActionApply"
              @keydown.enter="onAuditActionApply"
            />
            <select
              class="audit-select"
              data-testid="audit-filter-result"
              aria-label="按结果过滤审计"
              :value="audit.filters.result"
              @change="onAuditResultChange"
            >
              <option value="">全部结果</option>
              <option v-for="option in AUDIT_RESULT_OPTIONS" :key="option" :value="option">{{ option }}</option>
            </select>
            <button type="button" class="drawer-btn" data-testid="audit-refresh" :aria-busy="audit.loading" @click="w.refreshOperationLog(true)">
              {{ audit.loading ? '刷新中…' : '刷新' }}
            </button>
            <span class="spacer" />
            <button
              type="button"
              class="drawer-btn"
              data-testid="audit-export-csv"
              :disabled="Boolean(audit.exporting)"
              :aria-busy="audit.exporting === 'csv'"
              title="导出 CSV（Host exportOperationLog）"
              @click="exportAudit('csv')"
            >{{ audit.exporting === 'csv' ? '导出中…' : '导出 CSV' }}</button>
            <button
              type="button"
              class="drawer-btn"
              data-testid="audit-export-json"
              :disabled="Boolean(audit.exporting)"
              :aria-busy="audit.exporting === 'json'"
              title="导出 JSON（Host exportOperationLog）"
              @click="exportAudit('json')"
            >{{ audit.exporting === 'json' ? '导出中…' : '导出 JSON' }}</button>
          </div>

          <p v-if="audit.lastExport" class="audit-export-note" data-testid="audit-export-note">
            已导出 {{ audit.lastExport.format.toUpperCase() }}：
            <span class="mono" data-testid="audit-export-path">{{ audit.lastExport.path }}</span>
            （{{ audit.lastExport.count }} 条）
            <button type="button" class="drawer-btn" data-testid="audit-open-dir" @click="w.openAuditExportDir()">打开所在目录</button>
          </p>
          <p v-if="audit.exportError" class="audit-error" data-testid="audit-export-error" role="alert">{{ audit.exportError }}</p>

          <div v-if="audit.status === 'loading' && audit.items.length === 0" class="audit-state" data-testid="audit-loading">
            正在读取操作审计…
          </div>
          <div v-else-if="audit.status === 'unavailable' || audit.status === 'error'" class="audit-error-box" data-testid="audit-error" role="alert">
            <div class="audit-error-title">{{ audit.status === 'unavailable' ? '审计查询不可用' : '审计查询失败' }}</div>
            <div class="audit-error-detail">{{ audit.error }}</div>
            <button type="button" class="drawer-btn" @click="w.refreshOperationLog(true)">重试</button>
          </div>
          <div v-else-if="audit.items.length === 0" class="audit-state" data-testid="audit-empty">
            没有匹配的操作审计记录（成功/失败动作各写 1 行；可清空过滤条件或调整时间范围后刷新）。
          </div>
          <div v-else class="audit-table-wrap">
            <table class="audit-table" data-testid="audit-table">
              <thead>
                <tr>
                  <th>时间</th><th>actor</th><th>动作</th><th>组件</th><th>仓</th><th>结果</th><th>错误码</th><th>HEAD（前→后）</th><th>job</th><th>备份</th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="item in audit.items"
                  :key="item.id"
                  data-testid="audit-row"
                  :data-action="item.action"
                  :data-result="item.result"
                  :data-component-id="item.componentId"
                  :data-error-code="item.errorCode ?? ''"
                  :data-job-id="item.jobId ?? ''"
                >
                  <td class="mono nowrap" data-testid="audit-ts" :title="item.ts">{{ formatStamp(item.ts) }}</td>
                  <td class="truncate" :title="item.actor">{{ item.actor || '—' }}</td>
                  <td class="nowrap" data-testid="audit-action" :title="auditActionLabel(item.action)">{{ item.action || '—' }}</td>
                  <td class="mono truncate" :title="item.componentId">{{ item.componentId || '—' }}</td>
                  <td class="nowrap">{{ item.repoType ? repoTypeLabel(item.repoType) : '—' }}</td>
                  <td class="nowrap">
                    <span class="audit-result" :class="`tone-${auditResult(item.result).tone}`" data-testid="audit-result">
                      {{ auditResult(item.result).tone === 'success' ? '✓' : auditResult(item.result).tone === 'danger' ? '✕' : '•' }}
                      {{ auditResult(item.result).label }}
                    </span>
                  </td>
                  <td class="mono nowrap" data-testid="audit-error-code">{{ item.errorCode ?? '—' }}</td>
                  <td class="mono nowrap" data-testid="audit-heads" :title="`${item.beforeHead ?? '—'} → ${item.afterHead ?? '—'}`">
                    {{ shortSha(item.beforeHead) }} → {{ shortSha(item.afterHead) }}
                  </td>
                  <td class="mono truncate" :title="item.jobId ?? ''" data-testid="audit-job">{{ item.jobId || '—' }}</td>
                  <td class="mono truncate" :title="item.backupRef ?? ''" data-testid="audit-backup-ref">{{ item.backupRef || '—' }}</td>
                </tr>
              </tbody>
            </table>
          </div>

          <div class="audit-pager">
            <span class="audit-range" data-testid="audit-range">{{ w.auditRangeText() }}</span>
            <span class="spacer" />
            <button type="button" class="drawer-btn" data-testid="audit-prev" :disabled="!w.auditHasPrev() || audit.loading" @click="w.auditPrevPage()">上一页</button>
            <span class="audit-page" data-testid="audit-page">第 {{ w.auditPageIndex() + 1 }} / {{ w.auditPageCount() }} 页</span>
            <button type="button" class="drawer-btn" data-testid="audit-next" :disabled="!w.auditHasNext() || audit.loading" @click="w.auditNextPage()">下一页</button>
          </div>
          <p class="hint">数据来源：Host <span class="mono">operation_log</span>（成功与失败各 1 行；批量作业行含 jobId/jobItemId/backupRef）。Host 未就绪时显示不可用态，不展示伪造审计。</p>
        </section>
      </div>

      <!-- 未绑定仓库 -->
      <div v-else-if="kind === 'unbound'" class="modal-body">
        <div class="ci-empty">
          <div class="big">未绑定仓库（0）</div>
          <div class="muted">按原因分组：命名不符合 RepoNameParser / 路径不存在 / 未纳入管理。M1 只读展示，重新解析与忽略由 Host 后续版本开放。</div>
          <button type="button" class="drawer-btn" @click="w.refresh()">重新对账</button>
        </div>
        <h4 class="section">分组字段（契约占位）</h4>
        <div class="kv"><span>原因</span><span class="muted">—</span></div>
        <div class="kv"><span>路径</span><span class="muted">—</span></div>
        <div class="kv"><span>重新解析 / 忽略（写理由）</span><span class="muted">—（Host 未开放）</span></div>
      </div>

      <!-- 关于 -->
      <div v-else-if="kind === 'about'" class="modal-body">
        <div class="about">
          <div class="about-brand">MBD Git Tool</div>
          <div class="muted">版本 {{ hostInfo.appVersion }} · WebView2 {{ hostInfo.webView2Version }}</div>
          <div class="kv"><span>数据源</span><span>{{ state.source }}（{{ w.sourceLabel() }}）</span></div>
          <div class="kv"><span>工作区</span><span class="mono">{{ state.workspaceRoot || '—' }}</span></div>
          <div class="kv"><span>组件 / 仓</span><span>{{ state.counts.components }} / {{ state.counts.repos }}</span></div>
          <div class="kv"><span>当前聚焦仓</span><span class="mono">{{ scope }}</span></div>
          <p class="hint">M2：单仓写操作（PULL/FETCH/CHKOUT/COMMIT/PUSH）已启用；文件树/文件夹视图只读；批量写操作与 CI 在 M3/v2 接入。</p>
        </div>
      </div>

      <!-- 快捷键 -->
      <div v-else class="modal-body">
        <table class="anchor-table">
          <thead><tr><th>快捷键</th><th>动作</th></tr></thead>
          <tbody>
            <tr><td class="mono">Ctrl+K</td><td>聚焦搜索</td></tr>
            <tr><td class="mono">Ctrl+1 / Ctrl+2</td><td>工作台 / 文件夹视图</td></tr>
            <tr><td class="mono">F5</td><td>刷新当前视图</td></tr>
            <tr><td class="mono">Alt+1…Alt+5</td><td>PULL / FETCH / CHKOUT / COMMIT / PUSH</td></tr>
            <tr><td class="mono">↑ / ↓</td><td>组件卡片或树节点移动</td></tr>
            <tr><td class="mono">Enter</td><td>打开（卡片=详情 / 文件=默认程序）</td></tr>
            <tr><td class="mono">Space</td><td>树节点展开/折叠</td></tr>
            <tr><td class="mono">Esc</td><td>关闭浮层 / 清除批量选择</td></tr>
          </tbody>
        </table>
      </div>
    </section>
  </div>
</template>

<style scoped>
.modal-overlay {
  position: fixed;
  inset: 0;
  z-index: var(--z-modal);
  display: grid;
  place-items: center;
  padding: 16px;
  background: rgb(10 12 16 / 38%);
}
.modal {
  display: flex;
  flex-direction: column;
  max-width: 96vw;
  max-height: 92vh;
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: var(--r-panel);
  box-shadow: 0 20px 48px rgb(0 0 0 / 28%);
  overflow: hidden;
}
.modal-lg { width: 960px; height: min(90vh, 640px); }
.modal-md { width: 760px; height: min(90vh, 560px); }
.modal-sm { width: 560px; height: min(90vh, 480px); }
.modal-head {
  flex: 0 0 44px;
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 0 12px;
  border-bottom: 1px solid var(--border);
}
.modal-title { font-size: 14px; font-weight: 650; }
.experiment {
  font-size: 10px;
  color: var(--st-warning-fg);
  border: 1px solid var(--st-warning-bd);
  background: var(--st-warning-bg);
  border-radius: 999px;
  padding: 0 6px;
}
.spacer { flex: 1; }
.modal-body { flex: 1; min-height: 0; overflow-y: auto; padding: 12px 16px 20px; }
.breadcrumb { font-size: 11px; color: var(--text-2); margin-bottom: 8px; }
.hint { color: var(--text-2); font-size: 11px; line-height: 1.7; margin: 6px 0 10px; }
.section { margin: 14px 0 6px; font-size: 12px; }
.ci-empty {
  min-height: 210px;
  display: grid;
  place-content: center;
  justify-items: center;
  gap: 6px;
  text-align: center;
  border: 1px dashed var(--border);
  border-radius: var(--r-panel);
  color: var(--text-2);
  padding: 16px;
}
.ci-empty .big { font-size: 16px; color: var(--text); font-weight: 600; }
.contract {
  margin: 0;
  padding: 10px 12px;
  border: 1px solid var(--border);
  border-radius: var(--r-panel);
  background: var(--surface-2);
  color: var(--text-2);
  font-family: ui-monospace, Consolas, monospace;
  font-size: 10px;
  line-height: 1.6;
  overflow-x: auto;
  white-space: pre;
}
.anchor-table { width: 100%; border-collapse: collapse; font-size: 11px; }
.anchor-table th, .anchor-table td { text-align: left; padding: 5px 8px; border-bottom: 1px solid var(--border); vertical-align: top; }
.anchor-table th { color: var(--text-2); font-weight: 600; background: var(--surface-2); }
.settings-layout { display: grid; grid-template-columns: 150px minmax(0, 1fr); gap: 16px; }
.settings-nav { display: flex; flex-direction: column; gap: 2px; position: sticky; top: 0; align-self: start; }
.settings-nav a {
  padding: 6px 8px;
  border-radius: var(--r-btn);
  color: var(--text-2);
  font-size: 12px;
  text-decoration: none;
}
.settings-nav a:hover { background: var(--surface-2); color: var(--text); }
.settings-sections { display: flex; flex-direction: column; gap: 14px; min-width: 0; }
.settings-section {
  border: 1px solid var(--border);
  border-radius: var(--r-panel);
  padding: 10px 12px;
}
.settings-section h3 { margin: 0 0 8px; font-size: 13px; display: flex; align-items: center; gap: 6px; }
.field { display: grid; grid-template-columns: 104px minmax(0, 1fr); gap: 8px; align-items: center; margin-bottom: 6px; }
.field label { color: var(--text-2); font-size: 11px; }
.field input, .field select {
  height: 28px;
  border: 1px solid var(--border);
  border-radius: var(--r-btn);
  background: var(--surface);
  color: var(--text);
  font: inherit;
  font-size: 11px;
  padding: 0 8px;
}
.field input[disabled], .field select[disabled] { opacity: 0.55; }
.toolbar { display: flex; align-items: center; gap: 8px; margin-bottom: 10px; flex-wrap: wrap; }
.toolbar select { height: 26px; border: 1px solid var(--border); border-radius: var(--r-btn); background: var(--surface); color: var(--text); font-size: 11px; }
.drawer-btn {
  height: 26px;
  padding: 0 8px;
  border: 1px solid var(--border);
  border-radius: var(--r-btn);
  background: var(--surface);
  color: var(--text);
  font-size: 11px;
  cursor: pointer;
}
.drawer-btn:hover { background: var(--surface-2); }
.log-list {
  border: 1px solid var(--border);
  border-radius: var(--r-panel);
  background: var(--surface);
  font-family: ui-monospace, Consolas, monospace;
  font-size: 11px;
  overflow: hidden;
}
.log-line { display: grid; grid-template-columns: 68px 54px 1fr; gap: 10px; padding: 5px 10px; border-bottom: 1px solid var(--border); }
.log-line:last-child { border-bottom: 0; }
.log-at { color: var(--text-2); }
.log-level { font-weight: 700; }
.log-level.info { color: var(--st-info-fg); }
.log-level.warn { color: var(--st-warning-fg); }
.log-level.error { color: var(--st-danger-fg); }
.log-level.debug { color: var(--text-2); }
.log-msg { word-break: break-word; }
.kv { display: flex; justify-content: space-between; gap: 12px; font-size: 11px; padding: 5px 0; border-bottom: 1px dashed var(--border); }
.about { display: flex; flex-direction: column; gap: 2px; }
.about-brand { font-size: 18px; font-weight: 650; margin-bottom: 2px; }
.muted { color: var(--text-2); }
.mono { font-family: ui-monospace, Consolas, monospace; }
/* ── M2b-2 操作审计 ── */
.audit-section {
  margin-top: 14px;
  padding-top: 10px;
  border-top: 1px solid var(--border);
}
.audit-head { display: flex; align-items: baseline; gap: 8px; flex-wrap: wrap; }
.audit-head .section { margin: 0; }
.audit-source {
  font-size: 10px;
  border: 1px solid var(--border);
  border-radius: 999px;
  padding: 0 6px;
  color: var(--text-2);
}
.audit-status { font-size: 10px; color: var(--text-2); }
.audit-toolbar { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; margin: 8px 0; }
.audit-input {
  width: 200px;
  height: 26px;
  padding: 0 8px;
  border: 1px solid var(--border);
  border-radius: var(--r-btn);
  background: var(--surface);
  color: var(--text);
  font: inherit;
  font-size: 11px;
}
.audit-select {
  height: 26px;
  border: 1px solid var(--border);
  border-radius: var(--r-btn);
  background: var(--surface);
  color: var(--text);
  font-size: 11px;
}
.audit-export-note {
  display: flex;
  align-items: center;
  gap: 6px;
  flex-wrap: wrap;
  margin: 4px 0 8px;
  padding: 5px 8px;
  border: 1px dashed var(--st-info-bd);
  border-radius: var(--r-panel);
  background: var(--st-info-bg);
  color: var(--st-info-fg);
  font-size: 10px;
  overflow-wrap: anywhere;
}
.audit-error { color: var(--st-danger-fg); font-size: 10px; margin: 4px 0; }
.audit-state {
  min-height: 76px;
  display: grid;
  place-content: center;
  justify-items: center;
  gap: 4px;
  border: 1px dashed var(--border);
  border-radius: var(--r-panel);
  color: var(--text-2);
  font-size: 11px;
  padding: 12px;
  text-align: center;
}
.audit-error-box {
  display: flex;
  flex-direction: column;
  gap: 6px;
  align-items: flex-start;
  padding: 10px 12px;
  border: 1px solid var(--st-danger-bd);
  border-radius: var(--r-panel);
  background: var(--st-danger-bg);
  color: var(--st-danger-fg);
  font-size: 11px;
}
.audit-error-title { font-weight: 650; }
.audit-error-detail { overflow-wrap: anywhere; }
.audit-table-wrap { border: 1px solid var(--border); border-radius: var(--r-panel); overflow: auto; max-height: 260px; }
.audit-table { width: 100%; border-collapse: collapse; font-size: 10px; }
.audit-table th, .audit-table td { text-align: left; padding: 5px 8px; border-bottom: 1px solid var(--border); vertical-align: top; }
.audit-table th { position: sticky; top: 0; background: var(--surface-2); color: var(--text-2); font-weight: 600; z-index: 1; }
.audit-table tbody tr:last-child td { border-bottom: 0; }
.audit-table .nowrap { white-space: nowrap; }
.audit-table .truncate { max-width: 150px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.audit-result { white-space: nowrap; font-weight: 600; }
.audit-result.tone-success { color: var(--st-success-fg); }
.audit-result.tone-danger { color: var(--st-danger-fg); }
.audit-result.tone-warning { color: var(--st-warning-fg); }
.audit-result.tone-info { color: var(--st-info-fg); }
.audit-result.tone-neutral { color: var(--st-neutral-fg); }
.audit-pager { display: flex; align-items: center; gap: 8px; margin-top: 8px; flex-wrap: wrap; }
.audit-range { font-size: 10px; color: var(--text-2); }
.audit-page { font-size: 10px; color: var(--text-2); }
</style>
