<script setup lang="ts">
import { computed } from 'vue'
import { ciLabel } from '../../viewmodel'

const props = withDefaults(
  defineProps<{ state: string; slot?: boolean; withLabel?: boolean; title?: string }>(),
  { slot: false, withLabel: true, title: '' },
)

const normalized = computed(() => (props.state || 'NONE').toUpperCase())
const dotClass = computed(() => normalized.value.toLowerCase())
const label = computed(() => ciLabel(normalized.value))
const titleText = computed(
  () => props.title || `${label.value} · CI 独立通道（不参与组件聚合 / 卡片底色）`,
)
</script>

<template>
  <span
    class="ci-badge"
    :class="{ 'ci-slot-box': slot }"
    :data-testid="slot ? 'ci-slot' : undefined"
    :data-ci-state="normalized"
    :title="titleText"
  >
    <span class="ci-dot" :class="dotClass" aria-hidden="true" />
    <span v-if="withLabel && !slot" class="ci-label">{{ label }}</span>
    <span v-if="slot" class="sr-only">{{ label }}</span>
  </span>
</template>

<style scoped>
.ci-badge {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  font-size: 11px;
  color: var(--text-2);
  white-space: nowrap;
}
.ci-slot-box {
  width: 18px;
  height: 18px;
  flex: 0 0 18px;
  justify-content: center;
  border: 1px dashed var(--st-neutral-bd);
  border-radius: 4px;
  background: transparent;
}
.ci-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: var(--ci-idle);
  flex: 0 0 auto;
}
.ci-dot.pass { background: var(--ci-pass); }
.ci-dot.fail { background: var(--ci-fail); }
.ci-dot.running { background: var(--ci-running); }
.ci-dot.queued { border: 1px solid var(--st-neutral-bd); background: transparent; }
.ci-dot.none { background: transparent; border: 1px dashed var(--st-neutral-bd); }
.ci-dot.unknown { background: var(--ci-unknown); }
.ci-label { overflow: hidden; text-overflow: ellipsis; }
.sr-only {
  position: absolute;
  width: 1px;
  height: 1px;
  padding: 0;
  margin: -1px;
  overflow: hidden;
  clip: rect(0 0 0 0);
  white-space: nowrap;
  border: 0;
}
</style>

