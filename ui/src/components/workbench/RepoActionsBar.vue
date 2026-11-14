<script setup lang="ts">
/**
 * F 区：底部单仓五键 + 按钮上方 ≤48px 内联确认条（FR-ACT-04 / AC-UI-10a/10b / AC-UI-13）。
 * - PULL/FETCH/CHKOUT 免确认；
 * - COMMIT/PUSH 必须先经确认条「确认」才调用 runRepoAction（未确认 0 写）；
 * - 确认条 position:absolute，不参与布局，5 键 bbox 与 F 区高度不变；
 * - 初始焦点在条内、Tab 陷阱、Esc/取消后焦点归还触发按钮。
 */
import { computed, nextTick, ref, watch } from 'vue'
import { useWorkbench } from '../../stores/workbench'
import { actionDisabledTitle, ACTION_TITLE, REPO_LABEL } from '../../viewmodel'
import { REPO_ACTIONS } from '../../types/workbench'

const w = useWorkbench()
const state = w.state

const repo = computed(() => w.focusedRepo())
const availability = computed(() => w.availability())
const scope = computed(() => (repo.value ? REPO_LABEL[repo.value.repoType] : '未选择'))
const scopeText = computed(() => (repo.value ? repo.value.repoType : ''))
const busy = computed(() => Boolean(state.action.pending))
const confirmBar = computed(() => state.overlays.confirmBar)

const barEl = ref<HTMLElement | null>(null)
const acceptEl = ref<HTMLButtonElement | null>(null)
let previousFocus: HTMLElement | null = null
/** 最近一次确认条动作；原触发控件已不在 DOM 时（如 COMMIT 从对话框进入确认条）回退到此按钮。 */
let lastBarAction: 'COMMIT' | 'PUSH' = 'COMMIT'

/** 确认条不参与布局；打开时把焦点移入条内，关闭时归还触发控件。 */
watch(confirmBar, (value) => {
  if (value) {
    lastBarAction = value.action
    previousFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null
    void nextTick(() => acceptEl.value?.focus())
    return
  }
  const target = previousFocus
  previousFocus = null
  void nextTick(() => {
    const active = document.activeElement
    // 关闭原因可能是“用户已切到别的控件”（如点了其他按钮/卡片）：此时不抢焦点。
    if (active && active !== document.body && active !== target) return
    if (target && target.isConnected && target !== document.body) {
      target.focus()
      return
    }
    document.querySelector<HTMLElement>(`[data-testid="action-${lastBarAction.toLowerCase()}"]`)?.focus()
  })
})

function label(action: string): string {
  return action
}

function title(action: (typeof REPO_ACTIONS)[number]): string {
  const cell = availability.value[action]
  if (cell.enabled) {
    const base = ACTION_TITLE[action]
    const scopeText = repo.value?.repoName || repo.value?.repoType || ''
    if (action === 'COMMIT' || action === 'PUSH') return `${base} · ${scopeText}（确认条确认后执行）`
    if (action === 'CHKOUT') return `${base} · ${scopeText}（执行前弹出分支/Tag 输入对话框）`
    return `${base} · ${scopeText}（免确认，直接执行）`
  }
  // 禁用 tooltip 冻结格式（v0.3.1-r2）：`原因 [E_*] → 替代动作`（FR-ACT-02 / AC-UI-08）。
  return actionDisabledTitle(cell.reason, cell.code, cell.alt)
}

function onClick(action: (typeof REPO_ACTIONS)[number]): void {
  void w.runAction(action)
}

function acceptBar(): void {
  if (busy.value) return
  w.acceptConfirmBar()
}

function cancelBar(): void {
  w.cancelConfirmBar()
}

function onBarKeydown(event: KeyboardEvent): void {
  if (event.key === 'Escape') {
    event.preventDefault()
    event.stopPropagation()
    cancelBar()
    return
  }
  if (event.key !== 'Tab') return
  const nodes = [...(barEl.value?.querySelectorAll<HTMLButtonElement>('button') ?? [])].filter(
    (node) => !node.disabled,
  )
  if (nodes.length === 0) return
  const first = nodes[0]
  const last = nodes[nodes.length - 1]
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
  <div class="actions-area" data-testid="repo-actions-area">
    <!-- F 区按钮上方 ≤48px 内联确认条（COMMIT/PUSH 唯一写入口） -->
    <section
      v-if="confirmBar"
      ref="barEl"
      class="confirm-bar"
      :class="{ danger: confirmBar.danger }"
      data-testid="confirm-bar"
      :data-confirm-action="confirmBar.action"
      role="alertdialog"
      aria-modal="false"
      :aria-label="confirmBar.text"
      @keydown="onBarKeydown"
    >
      <span class="confirm-bar-icon" aria-hidden="true">⚠</span>
      <span class="confirm-bar-text" data-testid="confirm-bar-text" :title="confirmBar.text">{{ confirmBar.text }}</span>
      <span v-if="confirmBar.detail" class="confirm-bar-detail" :title="confirmBar.detail">{{ confirmBar.detail }}</span>
      <span class="spacer" />
      <button type="button" class="bar-btn" data-testid="confirm-bar-cancel" :disabled="busy" @click="cancelBar">
        取消
      </button>
      <button
        ref="acceptEl"
        type="button"
        class="bar-btn"
        :class="confirmBar.danger ? 'danger' : 'primary'"
        data-testid="confirm-bar-accept"
        :disabled="busy"
        @click="acceptBar"
      >{{ busy ? '执行中…' : confirmBar.confirmLabel }}</button>
    </section>

    <div
      class="repo-actions"
      data-testid="repo-actions"
      :data-action-scope="scopeText"
      :aria-label="`单仓操作：${scope}`"
      :aria-busy="busy"
      :data-pending="state.action.pending ?? ''"
      role="toolbar"
    >
      <button
        v-for="action in REPO_ACTIONS"
        :key="action"
        type="button"
        class="action"
        :class="{ pending: state.action.pending === action }"
        :data-testid="`action-${action.toLowerCase()}`"
        :data-action="action"
        :data-reason-code="availability[action].code ?? ''"
        :data-requires-confirm="availability[action].confirm ? 'true' : 'false'"
        :data-enabled="availability[action].enabled ? 'true' : 'false'"
        :disabled="!availability[action].enabled"
        :aria-disabled="!availability[action].enabled"
        :aria-busy="state.action.pending === action"
        :title="title(action)"
        @click="onClick(action)"
      >
        <span v-if="state.action.pending === action" class="spinner" aria-hidden="true" />
        <span>{{ state.action.pending === action ? `${label(action)}…` : label(action) }}</span>
      </button>
    </div>
  </div>
</template>

<style scoped>
/* 根节点仍占原 F 区网格行（52px）：确认条绝对定位在其上方，不参与布局。 */
.actions-area {
  position: relative;
  display: flex;
  flex-direction: column;
  justify-content: flex-end;
  min-width: 0;
  height: 100%;
}
.confirm-bar {
  position: absolute;
  left: 0;
  right: 0;
  bottom: 100%;
  z-index: 6;
  display: flex;
  align-items: center;
  gap: 8px;
  height: 44px;
  min-width: 0;
  padding: 0 12px;
  border: 1px solid var(--st-warning-bd);
  border-bottom: 0;
  border-radius: var(--r-panel) var(--r-panel) 0 0;
  background: var(--st-warning-bg);
  color: var(--st-warning-fg);
  box-shadow: 0 -8px 20px rgb(0 0 0 / 12%);
  box-sizing: border-box;
}
.confirm-bar.danger {
  border-color: var(--st-danger-bd);
  background: var(--st-danger-bg);
  color: var(--st-danger-fg);
}
.confirm-bar-icon { flex: 0 0 auto; font-size: 13px; }
.confirm-bar-text {
  flex: 0 1 auto;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 12px;
  font-weight: 650;
}
.confirm-bar-detail {
  flex: 1 1 auto;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 10px;
  opacity: 0.9;
}
.spacer { flex: 0 0 4px; }
.bar-btn {
  flex: 0 0 auto;
  height: 28px;
  padding: 0 12px;
  border: 1px solid var(--border);
  border-radius: var(--r-btn);
  background: var(--surface);
  color: var(--text);
  font-size: 12px;
  cursor: pointer;
}
.bar-btn:hover:not(:disabled) { background: var(--surface-2); }
.bar-btn.primary { border-color: var(--st-info-bd); background: var(--st-info-bg); color: var(--st-info-fg); }
.bar-btn.danger { border-color: var(--st-danger-bd); background: var(--st-danger-fg); color: var(--surface); }
.bar-btn:disabled { opacity: 0.6; cursor: not-allowed; }
.repo-actions {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 14px;
  height: var(--layout-actions-h);
  min-width: 0;
  padding: 0 18px;
}
.action {
  flex: 0 0 auto;
  min-width: 96px;
  max-width: 132px;
  height: 30px;
  padding: 0 18px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  border: 1px solid var(--st-success-bd);
  border-radius: var(--r-btn);
  background: var(--st-success-bg);
  color: var(--st-success-fg);
  font-size: 12px;
  font-weight: 600;
  letter-spacing: 0.3px;
  cursor: pointer;
  transition: background 120ms var(--ease), border-color 120ms var(--ease);
}
.action:hover:not(:disabled) { background: color-mix(in srgb, var(--st-success-bg) 70%, var(--st-success-bd)); }
.action:active:not(:disabled) { background: color-mix(in srgb, var(--surface-2) 70%, var(--rail)); transform: translateY(1px); }
.action:disabled { opacity: 0.62; cursor: not-allowed; }
.action.pending { border-color: var(--st-info-bd); background: var(--st-info-bg); color: var(--st-info-fg); }
.spinner {
  width: 12px;
  height: 12px;
  border: 2px solid color-mix(in srgb, var(--st-info-fg) 30%, transparent);
  border-top-color: var(--st-info-fg);
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}
@keyframes spin {
  to { transform: rotate(360deg); }
}
@media (max-width: 1279px) {
  .action { min-width: 88px; font-size: 11px; }
}
@media (prefers-reduced-motion: reduce) {
  .action { transition: none; }
  .spinner { animation: none; }
}
</style>
