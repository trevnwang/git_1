<script setup lang="ts">
/**
 * 组件详情抽屉（FR-DETAIL-01 / FR-CARD-02）：双击组件卡片或右键「查看组件详情」打开。
 * 数据优先 api.getComponentDetail（Host 已实现），失败回退当前快照；来源在抽屉内明示。
 * M2b-2：新增「备份 ref」区，按组件/仓加载 refs（getBackupRefs），确认后 restoreBackupRef。
 * 关闭：右上 ✕ / 点击遮罩 / Esc（App 全局 closeTopOverlay）。
 */
import { computed, nextTick, ref, watch } from 'vue'
import { useWorkbench } from '../../stores/workbench'
import { formatAgo, formatStamp, REPO_LABEL, shortSha } from '../../viewmodel'
import { REPO_TYPES } from '../../types/workbench'
import type { BackupRefVm, RepoType } from '../../types/workbench'
import CiBadge from '../common/CiBadge.vue'
import StateChip from '../common/StateChip.vue'
import EmptyState from '../common/EmptyState.vue'

const w = useWorkbench()
const state = w.state
const closeBtn = ref<HTMLButtonElement | null>(null)
/** 打开前的触发控件；关闭后归还焦点（AC-UI-13）。原控件被移除时回退到触发卡片。 */
let previousFocus: HTMLElement | null = null
let lastOpenId = ''

const open = computed(() => state.overlays.componentDrawer)
const openId = computed(() => state.overlays.componentDrawer ?? '')
const detail = computed(() => state.componentDetail)
const component = computed(() => (detail.value.componentId === openId.value ? detail.value.component : null))
const sourceLabel = computed(() => (detail.value.source === 'host' ? 'Host 详情' : '快照回退'))
const sourceTone = computed(() => (detail.value.source === 'host' ? 'success' : 'warning'))
const focusedRepoLabel = computed(() => REPO_LABEL[state.focusedRepoType])
const recentCommits = computed(() => state.detail?.commits.slice(0, 5) ?? [])
const ciState = computed(() => component.value?.ci ?? 'NONE')

// ── M2b-2 备份 ref（按组件/仓） ──
const backup = computed(() => state.backupRefs)
const backupRepoType = computed<RepoType>(() => {
  const value = (backup.value.componentId === openId.value ? backup.value.repoType : state.focusedRepoType) as RepoType
  return (REPO_TYPES as readonly string[]).includes(value) ? value : 'IMPL'
})
const backupStatus = computed(() => backup.value.status)
const backupError = computed(() => backup.value.error)

function selectBackupRepo(type: RepoType): void {
  if (!openId.value) return
  void w.loadBackupRefs(openId.value, type)
}

function refreshBackupRefs(): void {
  if (!openId.value) return
  void w.loadBackupRefs(openId.value, backupRepoType.value)
}

function restoreBackupRef(ref: BackupRefVm): void {
  w.requestRestoreBackupRef(ref)
}

watch(open, (value) => {
  if (value) {
    lastOpenId = value
    previousFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null
    void nextTick(() => closeBtn.value?.focus())
    return
  }
  // 关闭后焦点归还触发控件：优先打开前元素；丢失时回退到该组件卡片。
  const target = previousFocus
  previousFocus = null
  void nextTick(() => {
    if (target && target.isConnected && target !== document.body) {
      target.focus()
      return
    }
    if (!lastOpenId) return
    const card = document.querySelector<HTMLElement>(
      `[data-testid="component-card"][data-component-id="${lastOpenId}"]`,
    )
    card?.focus()
  })
})

function copy(text: string | null | undefined, label: string): void {
  if (!text) return
  void w.copyText(text, label)
}

function trapTab(event: KeyboardEvent): void {
  const rootEl = document.querySelector<HTMLElement>('[data-testid="component-drawer"]')
  if (!rootEl) return
  const focusables = [...rootEl.querySelectorAll<HTMLElement>('button, [tabindex="0"]')].filter(
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
</script>

<template>
  <div v-if="openId" class="overlay" @click.self="w.closeComponentDrawer()" @keydown.tab="trapTab">
    <aside
      class="component-drawer"
      data-testid="component-drawer"
      :data-component-id="openId"
      :data-source="detail.source"
      role="dialog"
      aria-modal="true"
      aria-label="组件详情抽屉"
    >
      <header class="drawer-head">
        <span class="title">组件详情</span>
        <span class="source-badge" :class="`tone-${sourceTone}`" data-testid="component-drawer-source">{{ sourceLabel }}</span>
        <span class="spacer" />
        <button
          ref="closeBtn"
          type="button"
          class="icon-btn"
          title="关闭（Esc）"
          aria-label="关闭组件详情"
          data-testid="component-drawer-close"
          @click="w.closeComponentDrawer()"
        >✕</button>
      </header>

      <div class="drawer-body">
        <div v-if="detail.status === 'loading'" class="loading">正在加载组件详情…</div>

        <EmptyState
          v-else-if="detail.status === 'error' || !component"
          icon="✕"
          title="无法读取组件详情"
          :desc="detail.error || '快照中未找到该组件；可重试或先刷新快照。'"
        >
          <button type="button" class="drawer-btn" @click="w.loadComponentDetail(openId)">重试</button>
          <button type="button" class="drawer-btn" @click="w.refresh()">刷新快照</button>
        </EmptyState>

        <template v-else>
          <p v-if="detail.error" class="fallback-note" data-testid="component-drawer-note">⚠ {{ detail.error }}</p>

          <section class="section">
            <div class="section-title">组件概览</div>
            <div class="field" data-testid="component-drawer-id">
              <span class="field-label">组件 ID</span>
              <span class="field-value mono truncate" :title="component.id">{{ component.id }}</span>
              <button type="button" class="copy-btn" title="复制组件 ID" @click="copy(component.id, '组件 ID')">⧉</button>
            </div>
            <div class="field">
              <span class="field-label">聚合状态</span>
              <span class="field-value"><StateChip :state="component.state" /></span>
            </div>
            <div class="field">
              <span class="field-label">分支</span>
              <span class="field-value mono truncate" :title="component.branch">{{ component.branch }}</span>
              <button type="button" class="copy-btn" title="复制分支" @click="copy(component.branch, '分支')">⧉</button>
            </div>
            <div class="field">
              <span class="field-label">Tag</span>
              <span class="field-value mono truncate" :title="component.tag ?? '—'">{{ component.tag ?? '—' }}</span>
            </div>
            <div class="field">
              <span class="field-label">四仓一致性</span>
              <span class="field-value" :class="{ warning: component.mixedBranch }">
                {{ component.mixedBranch ? '⑂ 分支不一致（MIXED_BRANCH）' : '一致' }}
              </span>
            </div>
            <div class="field">
              <span class="field-label">最近活动</span>
              <span class="field-value">{{ formatStamp(component.lastActivityAt) }} · {{ formatAgo(component.lastActivityAt, state.now) }}</span>
            </div>
          </section>

          <section class="section">
            <div class="section-title">四仓状态 <span class="hint">（D / I / M / S）</span></div>
            <div class="repo-table" data-testid="component-drawer-repos">
              <div class="repo-row repo-head-row" aria-hidden="true">
                <span>仓</span><span>状态</span><span>分支</span><span>↑/↓</span><span>变更</span><span>HEAD</span><span>CI</span>
              </div>
              <div
                v-for="repo in component.repos"
                :key="repo.repoType"
                class="repo-row"
                data-testid="component-drawer-repo"
                :data-repo-type="repo.repoType"
                :title="`${REPO_LABEL[repo.repoType]}：${repo.state}${repo.errorCode ? `（${repo.errorCode}）` : ''}`"
              >
                <span class="repo-type" :class="`tone-${repo.tone}`">{{ REPO_LABEL[repo.repoType] }}</span>
                <span class="repo-state"><StateChip :state="repo.state" compact /></span>
                <span class="mono truncate" :title="repo.branch">{{ repo.branch }}</span>
                <span class="mono">{{ repo.ahead }}/{{ repo.behind }}</span>
                <span class="mono" :class="{ warning: repo.dirtyCount + repo.untrackedCount > 0 }">
                  {{ repo.dirtyCount + repo.untrackedCount }}
                </span>
                <span class="mono truncate" :title="repo.headSha ?? '—'">{{ shortSha(repo.headSha) }}</span>
                <span class="ci-cell"><CiBadge :state="repo.ci" :with-label="false" /></span>
              </div>
            </div>
          </section>

          <section class="section">
            <div class="section-title">
              备份 ref
              <span class="hint">（SYNC 备份，可恢复到此处；保留 30 天）</span>
            </div>
            <div class="backup-toolbar">
              <div class="repo-tabs" role="tablist" aria-label="备份 ref 仓类型">
                <button
                  v-for="type in REPO_TYPES"
                  :key="type"
                  type="button"
                  role="tab"
                  class="repo-tab"
                  :class="{ active: backupRepoType === type }"
                  :aria-selected="backupRepoType === type"
                  :data-repo-type="type"
                  data-testid="backup-repo-tab"
                  @click="selectBackupRepo(type)"
                >{{ REPO_LABEL[type] }}</button>
              </div>
              <span class="spacer" />
              <button
                type="button"
                class="drawer-btn"
                data-testid="backup-refresh"
                :aria-busy="backupStatus === 'loading'"
                title="重新读取该仓的备份 ref（Host getBackupRefs）"
                @click="refreshBackupRefs"
              >{{ backupStatus === 'loading' ? '读取中…' : '刷新' }}</button>
            </div>

            <div v-if="backupStatus === 'loading' && backup.refs.length === 0" class="muted small" data-testid="backup-loading">
              正在读取备份 ref…
            </div>
            <div v-else-if="backupStatus === 'unavailable' || backupStatus === 'error'" class="backup-error" data-testid="backup-error" role="alert">
              <span>{{ backupError }}</span>
              <button type="button" class="drawer-btn" @click="refreshBackupRefs">重试</button>
            </div>
            <div v-else-if="backup.refs.length === 0" class="backup-empty" data-testid="backup-empty">
              该仓暂无可恢复的备份 ref（SYNC 成功后会产生 <span class="mono">refs/backup/sync/*</span> 备份）。
            </div>
            <ul v-else class="backup-list" data-testid="backup-list">
              <li
                v-for="item in backup.refs"
                :key="`${item.repoType}:${item.ref}`"
                class="backup-row"
                data-testid="backup-ref"
                :data-ref="item.ref"
                :data-repo-type="item.repoType"
              >
                <span class="mono truncate backup-ref-name" :title="item.ref">{{ item.ref }}</span>
                <span v-if="item.kind === 'SAFETY'" class="backup-branch safety" title="恢复前自动安全备份，可恢复到此处">安全备份</span>
                <span v-if="item.branch" class="backup-branch" :title="`来源分支：${item.branch}（${item.format}）`">@{{ item.branch }}</span>
                <span v-else class="backup-branch legacy" title="旧备份无分支身份；恢复前请确认当前分支">旧格式</span>
                <span class="mono backup-sha" :title="item.sha">{{ shortSha(item.sha) }}</span>
                <span class="muted backup-at" :title="item.createdAt">{{ formatStamp(item.createdAt) }}</span>
                <button
                  type="button"
                  class="drawer-btn backup-restore"
                  data-testid="backup-restore"
                  :disabled="Boolean(state.backupRestore.pending)"
                  :title="`从 ${item.ref} 恢复（会覆盖当前分支状态，需确认）`"
                  @click="restoreBackupRef(item)"
                >{{ state.backupRestore.pending === item.ref ? '恢复中…' : '恢复…' }}</button>
              </li>
            </ul>
            <p v-if="state.backupRestore.error" class="backup-error" data-testid="backup-restore-error" role="alert">
              {{ state.backupRestore.error }}
            </p>
            <p class="hint backup-note">
              恢复属覆盖性写操作：确认后由 Host 执行 <span class="mono">restoreBackupRef</span>，成功后刷新快照与详情；Host 拒绝时不显示为成功。
            </p>
          </section>

          <section class="section">
            <div class="section-title">
              最近提交
              <span class="hint">（聚焦仓 {{ focusedRepoLabel }}）</span>
            </div>
            <div v-if="recentCommits.length === 0" class="muted small">该仓暂无提交（或尚未加载）。</div>
            <div v-else class="commit-list" data-testid="component-drawer-commits">
              <div v-for="commit in recentCommits" :key="commit.sha" class="commit-row">
                <span class="commit-sha mono" :title="commit.sha">{{ commit.shortSha }}</span>
                <span class="commit-msg truncate" :title="commit.message">{{ commit.message }}</span>
                <span class="commit-meta">{{ commit.author }} · {{ formatAgo(commit.at, state.now) }}</span>
              </div>
            </div>
          </section>

          <section class="section">
            <div class="section-title">CI</div>
            <div class="field" data-testid="component-drawer-ci">
              <span class="field-label">CI 状态</span>
              <span class="field-value"><CiBadge :state="ciState" /> <span class="muted">CI 未接入（v1 占位，独立通道）</span></span>
            </div>
            <div class="field">
              <span class="field-label">本地构建</span>
              <span class="field-value muted">未接入</span>
            </div>
          </section>
        </template>
      </div>

      <footer class="drawer-foot">
        <span class="muted small">详情来源：{{ sourceLabel }} · Esc 关闭</span>
        <button type="button" class="drawer-btn" @click="w.closeComponentDrawer()">关闭</button>
      </footer>
    </aside>
  </div>
</template>

<style scoped>
.overlay {
  position: fixed;
  inset: 0;
  z-index: var(--z-drawer);
  display: flex;
  justify-content: flex-end;
  background: rgb(10 12 16 / 32%);
}
.component-drawer {
  width: 520px;
  max-width: min(94vw, 720px);
  height: 100%;
  display: flex;
  flex-direction: column;
  background: var(--surface);
  border-left: 1px solid var(--border);
  box-shadow: var(--shadow);
  animation: slide-in 200ms var(--ease);
}
@keyframes slide-in {
  from { transform: translateX(24px); opacity: 0; }
  to { transform: translateX(0); opacity: 1; }
}
.drawer-head {
  flex: 0 0 48px;
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 0 12px;
  border-bottom: 1px solid var(--border);
  background: var(--surface-2);
}
.title { font-size: 14px; font-weight: 650; }
.source-badge {
  font-size: 10px;
  border: 1px solid var(--border);
  border-radius: 999px;
  padding: 0 6px;
  color: var(--text-2);
}
.source-badge.tone-success { color: var(--st-success-fg); border-color: var(--st-success-bd); background: var(--st-success-bg); }
.source-badge.tone-warning { color: var(--st-warning-fg); border-color: var(--st-warning-bd); background: var(--st-warning-bg); }
.spacer { flex: 1; }
.icon-btn { border: 0; background: transparent; color: var(--text-2); font-size: 13px; cursor: pointer; }
.icon-btn:hover { color: var(--text); }
.drawer-body { flex: 1; min-height: 0; overflow-y: auto; overscroll-behavior: contain; padding: 10px 14px 18px; }
.loading { padding: 24px; text-align: center; color: var(--text-2); font-size: 12px; }
.fallback-note {
  margin: 0 0 8px;
  padding: 6px 8px;
  border: 1px dashed var(--st-warning-bd);
  border-radius: var(--r-panel);
  background: var(--st-warning-bg);
  color: var(--st-warning-fg);
  font-size: 10px;
  line-height: 1.6;
  overflow-wrap: anywhere;
}
.section { margin-bottom: 10px; }
.section-title {
  display: flex;
  align-items: baseline;
  gap: 6px;
  margin: 6px 0 6px;
  font-size: 11px;
  font-weight: 650;
  color: var(--text-2);
  text-transform: uppercase;
  letter-spacing: 0.3px;
}
.section-title .hint { font-weight: 400; text-transform: none; letter-spacing: 0; font-size: 10px; }
.field {
  display: grid;
  grid-template-columns: minmax(62px, 72px) minmax(0, 1fr) auto;
  gap: 6px;
  align-items: center;
  min-height: 26px;
  font-size: 11px;
  line-height: 1.4;
}
.field-label { color: var(--text-2); }
.field-value { min-width: 0; overflow-wrap: anywhere; }
.field-value.warning { color: var(--st-warning-fg); }
.copy-btn { border: 0; background: transparent; color: var(--text-2); cursor: pointer; font-size: 11px; padding: 0 2px; }
.copy-btn:hover { color: var(--focus); }
.repo-table { border: 1px solid var(--border); border-radius: var(--r-panel); overflow: hidden; }
.repo-row {
  display: grid;
  grid-template-columns: 48px 26px minmax(0, 1fr) 34px 32px 58px 20px;
  gap: 4px;
  align-items: center;
  min-height: 26px;
  padding: 0 8px;
  border-bottom: 1px solid var(--border);
  font-size: 10px;
}
.repo-row:last-child { border-bottom: 0; }
.repo-head-row { background: var(--surface-2); color: var(--text-2); font-weight: 600; }
.repo-type { font-weight: 700; }
.repo-type.tone-success { color: var(--st-success-fg); }
.repo-type.tone-warning { color: var(--st-warning-fg); }
.repo-type.tone-info { color: var(--st-info-fg); }
.repo-type.tone-purple { color: var(--st-purple-fg); }
.repo-type.tone-cyan { color: var(--st-cyan-fg); }
.repo-type.tone-danger { color: var(--st-danger-fg); }
.repo-type.tone-neutral { color: var(--st-neutral-fg); }
.repo-type.tone-hatched { color: var(--st-unknown-fg); }
.repo-state { display: inline-flex; }
.warning { color: var(--st-warning-fg); }
.ci-cell { display: inline-flex; }
/* ── M2b-2 备份 ref ── */
.backup-toolbar { display: flex; align-items: center; gap: 6px; margin-bottom: 6px; flex-wrap: wrap; }
.repo-tabs { display: inline-flex; border: 1px solid var(--border); border-radius: var(--r-btn); overflow: hidden; }
.repo-tab {
  height: 24px;
  padding: 0 8px;
  border: 0;
  border-right: 1px solid var(--border);
  background: transparent;
  color: var(--text-2);
  font-size: 10px;
  cursor: pointer;
}
.repo-tab:last-child { border-right: 0; }
.repo-tab.active { background: var(--surface-2); color: var(--text); font-weight: 600; }
.backup-empty {
  padding: 12px;
  border: 1px dashed var(--border);
  border-radius: var(--r-panel);
  color: var(--text-2);
  font-size: 10px;
  line-height: 1.6;
  text-align: center;
}
.backup-error {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
  margin: 4px 0;
  padding: 6px 8px;
  border: 1px solid var(--st-danger-bd);
  border-radius: var(--r-panel);
  background: var(--st-danger-bg);
  color: var(--st-danger-fg);
  font-size: 10px;
  line-height: 1.5;
  overflow-wrap: anywhere;
}
.backup-list { list-style: none; margin: 0; padding: 0; border: 1px solid var(--border); border-radius: var(--r-panel); overflow: hidden; }
.backup-row {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 54px 68px auto;
  gap: 6px;
  align-items: center;
  min-height: 28px;
  padding: 2px 6px;
  border-bottom: 1px solid var(--border);
  font-size: 10px;
}
.backup-row:last-child { border-bottom: 0; }
.backup-ref-name { color: var(--text-2); }
.backup-branch { font-size: 10px; color: var(--st-info-fg); max-width: 86px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.backup-branch.legacy { color: var(--text-2); }
.backup-sha { color: var(--focus); }
.backup-at { white-space: nowrap; }
.backup-restore { height: 22px; padding: 0 6px; font-size: 10px; }
.backup-note { margin: 6px 0 0; }
.commit-list { display: flex; flex-direction: column; gap: 2px; }
.commit-row {
  display: grid;
  grid-template-columns: 52px minmax(0, 1fr);
  column-gap: 6px;
  padding: 3px 0;
  border-bottom: 1px dashed var(--border);
  font-size: 11px;
}
.commit-row:last-child { border-bottom: 0; }
.commit-sha { color: var(--focus); }
.commit-msg { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.commit-meta { grid-column: 2; color: var(--text-2); font-size: 10px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.drawer-foot {
  flex: 0 0 38px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  padding: 0 12px;
  border-top: 1px solid var(--border);
  background: var(--surface-2);
}
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
.muted { color: var(--text-2); }
.small { font-size: 10px; }
.mono { font-family: ui-monospace, Consolas, monospace; }
.truncate { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
@media (prefers-reduced-motion: reduce) {
  .component-drawer { animation: none; }
}
</style>
