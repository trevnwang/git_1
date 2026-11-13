<script setup lang="ts">
import { computed } from 'vue'
import { stateLabel, stateMeta } from '../../viewmodel'

const props = withDefaults(defineProps<{ state: string; compact?: boolean; showIcon?: boolean }>(), {
  compact: false,
  showIcon: true,
})

const meta = computed(() => stateMeta(props.state))
const tone = computed(() => meta.value.tone)
</script>

<template>
  <span
    class="state-chip"
    :class="[`tone-${tone}`, { compact }]"
    :title="stateLabel(state)"
    :aria-label="stateLabel(state)"
    role="img"
  >
    <span v-if="showIcon" class="ico" aria-hidden="true">{{ meta.icon }}</span>
    <span v-if="!compact" class="txt">{{ meta.label }}</span>
  </span>
</template>

<style scoped>
.state-chip {
  display: inline-flex;
  align-items: center;
  gap: 3px;
  height: var(--chip-h);
  max-width: 100%;
  padding: 0 6px;
  border: 1px solid transparent;
  border-radius: var(--r-chip);
  font-size: 11px;
  font-weight: 600;
  line-height: 1;
  white-space: nowrap;
  overflow: hidden;
}
.state-chip.compact {
  width: var(--chip-h);
  padding: 0;
  justify-content: center;
}
.ico { font-size: 10px; line-height: 1; }
.txt { overflow: hidden; text-overflow: ellipsis; }
.tone-success { color: var(--st-success-fg); background: var(--st-success-bg); border-color: var(--st-success-bd); }
.tone-warning { color: var(--st-warning-fg); background: var(--st-warning-bg); border-color: var(--st-warning-bd); }
.tone-info { color: var(--st-info-fg); background: var(--st-info-bg); border-color: var(--st-info-bd); }
.tone-purple { color: var(--st-purple-fg); background: var(--st-purple-bg); border-color: var(--st-purple-bd); }
.tone-cyan { color: var(--st-cyan-fg); background: var(--st-cyan-bg); border-color: var(--st-cyan-bd); }
.tone-neutral {
  color: var(--st-neutral-fg);
  background: var(--st-neutral-bg);
  border-style: dashed;
  border-color: var(--st-neutral-bd);
}
.tone-danger { color: var(--st-danger-fg); background: var(--st-danger-bg); border-color: var(--st-danger-bd); }
.tone-hatched {
  color: var(--st-unknown-fg);
  background: repeating-linear-gradient(135deg, var(--surface-2) 0 6px, var(--border) 6px 12px);
  border-color: var(--st-neutral-bd);
}
</style>
