<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted } from 'vue'
import { useWorkbench } from '../../stores/workbench'

const w = useWorkbench()
const state = w.state

const menu = computed(() => state.overlays.contextMenu)
const style = computed(() => {
  const ctx = menu.value
  if (!ctx) return {}
  const width = 260
  const height = Math.min(ctx.items.length * 28 + 12, window.innerHeight - 16)
  const left = Math.min(ctx.x, window.innerWidth - width - 8)
  const top = Math.min(ctx.y, window.innerHeight - height - 8)
  return { left: `${Math.max(8, left)}px`, top: `${Math.max(8, top)}px` }
})

function run(key: string): void {
  w.closeContextMenu()
  w.command(key)
}

function onPointerDown(event: MouseEvent): void {
  const target = event.target as HTMLElement | null
  if (!target?.closest('[data-testid="context-menu"]')) w.closeContextMenu()
}

function onKeydown(event: KeyboardEvent): void {
  if (event.key === 'Escape') {
    event.preventDefault()
    w.closeContextMenu()
  }
}

onMounted(() => {
  window.addEventListener('mousedown', onPointerDown)
  window.addEventListener('keydown', onKeydown)
})

onBeforeUnmount(() => {
  window.removeEventListener('mousedown', onPointerDown)
  window.removeEventListener('keydown', onKeydown)
})
</script>

<template>
  <div
    v-if="menu"
    class="context-menu"
    data-testid="context-menu"
    role="menu"
    :style="style"
    aria-label="上下文菜单"
  >
    <template v-for="item in menu.items" :key="item.key">
      <div v-if="item.separator" class="sep" role="separator" />
      <button
        v-else
        type="button"
        class="item"
        role="menuitem"
        :class="{ danger: item.danger }"
        :disabled="item.disabled"
        :title="item.title"
        @click="run(item.key)"
      >{{ item.label }}</button>
    </template>
  </div>
</template>

<style scoped>
.context-menu {
  position: fixed;
  z-index: var(--z-menu);
  width: 260px;
  padding: 4px;
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: var(--r-panel);
  box-shadow: 0 12px 28px rgb(16 24 40 / 22%);
}
.item {
  width: 100%;
  height: 28px;
  padding: 0 8px;
  border: 0;
  border-radius: 4px;
  background: transparent;
  color: var(--text);
  font-size: 12px;
  text-align: left;
  cursor: pointer;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.item:hover:not(:disabled) { background: var(--surface-2); }
.item:disabled { opacity: 0.5; cursor: default; }
.item.danger { color: var(--st-danger-fg); }
.sep { height: 1px; margin: 4px 6px; background: var(--border); }
</style>
