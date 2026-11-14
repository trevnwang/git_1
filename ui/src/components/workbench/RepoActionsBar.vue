<script setup lang="ts">
import { computed } from 'vue'
import { useWorkbench } from '../../stores/workbench'
import { ACTION_TITLE, REPO_LABEL } from '../../viewmodel'
import { REPO_ACTIONS } from '../../types/workbench'

const w = useWorkbench()
const state = w.state

const repo = computed(() => w.focusedRepo())
const availability = computed(() => w.availability())
const scope = computed(() => (repo.value ? REPO_LABEL[repo.value.repoType] : '未选择'))
const scopeText = computed(() => (repo.value ? repo.value.repoType : ''))
const busy = computed(() => Boolean(state.action.pending))

function label(action: string): string {
  return action
}

function title(action: (typeof REPO_ACTIONS)[number]): string {
  const cell = availability.value[action]
  if (cell.enabled) {
    const base = ACTION_TITLE[action]
    const scope = repo.value?.repoName || repo.value?.repoType || ''
    if (cell.confirm) return `${base} · ${scope}（执行前弹出确认/输入对话框）`
    if (action === 'FETCH') return `${base} · ${scope}（免确认，直接执行）`
    return `${base} · ${scope}`
  }
  return cell.reason
}

function onClick(action: (typeof REPO_ACTIONS)[number]): void {
  void w.runAction(action)
}
</script>

<template>
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
</template>

<style scoped>
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
