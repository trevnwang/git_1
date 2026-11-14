<script setup lang="ts">
/**
 * 组件详情抽屉（FR-DETAIL-01 / FR-CARD-02）：双击组件卡片或右键「查看组件详情」打开。
 * 数据优先 api.getComponentDetail（Host 已实现），失败回退当前快照；来源在抽屉内明示。
 * 关闭：右上 ✕ / 点击遮罩 / Esc（App 全局 closeTopOverlay）。
 */
import { computed, nextTick, ref, watch } from 'vue'
import { useWorkbench } from '../../stores/workbench'
import { formatAgo, formatStamp, REPO_LABEL, shortSha } from '../../viewmodel'
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
