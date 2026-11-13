<script setup lang="ts">
import { useWorkbench } from '../../stores/workbench'

const w = useWorkbench()
const state = w.state
</script>

<template>
  <div class="toast-host" data-testid="toast-host" aria-live="polite">
    <transition-group name="toast">
      <div
        v-for="toast in state.toasts"
        :key="toast.id"
        class="toast-item"
        :class="`tone-${toast.tone}`"
        :role="toast.tone === 'error' ? 'alert' : 'status'"
        data-testid="toast"
        @click="w.dismissToast(toast.id)"
      >
        <span class="toast-icon" aria-hidden="true">{{ toast.tone === 'success' ? '✓' : toast.tone === 'warning' ? '!' : toast.tone === 'error' ? '✕' : 'i' }}</span>
        <span class="toast-text">{{ toast.text }}</span>
      </div>
    </transition-group>
  </div>
</template>

<style scoped>
.toast-host {
  position: fixed;
  left: 50%;
  bottom: calc(var(--layout-status-h) + var(--layout-actions-h) + 12px);
  transform: translateX(-50%);
  z-index: var(--z-toast);
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 6px;
  pointer-events: none;
  max-width: min(480px, 84vw);
}
.toast-item {
  pointer-events: auto;
  display: flex;
  align-items: center;
  gap: 8px;
  max-width: 100%;
  padding: 9px 14px;
  border-radius: var(--r-panel);
  background: #14161a;
  color: #f3f4f6;
  font-size: 12px;
  line-height: 1.4;
  box-shadow: 0 8px 24px rgb(0 0 0 / 25%);
  cursor: pointer;
}
.toast-item.tone-success { background: var(--st-success-fg); color: #fff; }
.toast-item.tone-warning { background: var(--st-warning-bg); color: var(--st-warning-fg); border: 1px solid var(--st-warning-bd); }
.toast-item.tone-error { background: var(--st-danger-bg); color: var(--st-danger-fg); border: 1px solid var(--st-danger-bd); }
.toast-icon { font-weight: 700; }
.toast-text { overflow: hidden; text-overflow: ellipsis; }
.toast-enter-active, .toast-leave-active { transition: opacity 160ms var(--ease), transform 160ms var(--ease); }
.toast-enter-from, .toast-leave-to { opacity: 0; transform: translateY(6px); }
@media (prefers-reduced-motion: reduce) {
  .toast-enter-active, .toast-leave-active { transition: none; }
}
</style>

