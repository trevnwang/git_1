<script setup lang="ts">
import { useWorkbench } from '../../stores/workbench'

const w = useWorkbench()
const state = w.state
</script>

<template>
  <div v-if="state.overlays.confirm" class="overlay" @click.self="w.closeConfirm()">
    <section
      class="confirm"
      data-testid="confirm-dialog"
      role="alertdialog"
      aria-modal="true"
      :aria-label="state.overlays.confirm.title"
    >
      <header class="confirm-head">
        <span class="confirm-title">{{ state.overlays.confirm.title }}</span>
      </header>
      <div class="confirm-body">
        <p class="confirm-text">{{ state.overlays.confirm.body }}</p>
        <p v-if="state.overlays.confirm.detail" class="confirm-detail mono">{{ state.overlays.confirm.detail }}</p>
      </div>
      <footer class="confirm-actions">
        <button type="button" class="btn" autofocus @click="w.closeConfirm()">取消</button>
        <button
          type="button"
          class="btn"
          :class="state.overlays.confirm.danger ? 'danger' : 'primary'"
          @click="state.overlays.confirm.onConfirm()"
        >{{ state.overlays.confirm.confirmLabel }}</button>
      </footer>
    </section>
  </div>
</template>

<style scoped>
.overlay {
  position: fixed;
  inset: 0;
  z-index: var(--z-modal);
  display: grid;
  place-items: center;
  padding: 16px;
  background: rgb(10 12 16 / 40%);
}
.confirm {
  width: min(480px, 92vw);
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: var(--r-panel);
  box-shadow: 0 20px 48px rgb(0 0 0 / 28%);
  overflow: hidden;
}
.confirm-head { padding: 12px 14px 0; }
.confirm-title { font-size: 14px; font-weight: 650; }
.confirm-body { padding: 8px 14px 14px; }
.confirm-text { margin: 0 0 6px; font-size: 12px; color: var(--text); line-height: 1.6; }
.confirm-detail {
  margin: 0;
  font-size: 11px;
  color: var(--text-2);
  word-break: break-all;
  background: var(--surface-2);
  border-radius: 4px;
  padding: 6px 8px;
}
.confirm-actions {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  padding: 10px 14px;
  border-top: 1px solid var(--border);
  background: var(--surface-2);
}
.btn {
  height: 30px;
  padding: 0 12px;
  border: 1px solid var(--border);
  border-radius: var(--r-btn);
  background: var(--surface);
  color: var(--text);
  font-size: 12px;
  cursor: pointer;
}
.btn:hover { background: var(--surface-2); }
.btn.primary { border-color: var(--st-info-bd); background: var(--st-info-bg); color: var(--st-info-fg); }
.btn.danger { border-color: var(--st-danger-bd); background: var(--st-danger-bg); color: var(--st-danger-fg); }
.mono { font-family: ui-monospace, Consolas, monospace; }
</style>
