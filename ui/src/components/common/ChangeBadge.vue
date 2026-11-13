<script setup lang="ts">
import { computed } from 'vue'

const props = defineProps<{ dirty: number; untracked: number }>()

const total = computed(() => props.dirty + props.untracked)
const text = computed(() => (total.value > 99 ? '99+' : String(total.value)))
const title = computed(() => `已修改 ${props.dirty} / 未跟踪 ${props.untracked}`)
</script>

<template>
  <span v-if="total > 0" class="change-badge" data-testid="card-change-badge" :title="title">
    <span class="dot" aria-hidden="true">●</span>{{ text }}
  </span>
</template>

<style scoped>
.change-badge {
  display: inline-flex;
  align-items: center;
  gap: 2px;
  height: 18px;
  padding: 0 6px;
  border: 1px solid var(--badge-change-bd);
  border-radius: 999px;
  background: var(--badge-change-bg);
  color: var(--badge-change-fg);
  font-size: 11px;
  font-weight: 600;
  font-variant-numeric: tabular-nums;
  white-space: nowrap;
}
.dot { font-size: 8px; line-height: 1; color: var(--st-warning-fg); }
</style>
