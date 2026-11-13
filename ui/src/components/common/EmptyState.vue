<script setup lang="ts">
withDefaults(defineProps<{ icon?: string; title: string; desc?: string }>(), {
  icon: '∅',
  desc: '',
})
</script>

<template>
  <div class="empty-state">
    <div class="empty-icon" aria-hidden="true">{{ icon }}</div>
    <div class="empty-title">{{ title }}</div>
    <div v-if="desc" class="empty-desc">{{ desc }}</div>
    <div v-if="$slots.default" class="empty-actions"><slot /></div>
  </div>
</template>

<style scoped>
.empty-state {
  min-height: 160px;
  height: 100%;
  display: grid;
  place-content: center;
  justify-items: center;
  gap: 6px;
  padding: 16px;
  text-align: center;
  color: var(--text-2);
}
.empty-icon { font-size: 26px; color: var(--st-neutral-bd); line-height: 1; }
.empty-title { font-size: 12px; font-weight: 600; color: var(--text); }
.empty-desc { font-size: 11px; max-width: 240px; line-height: 1.6; overflow-wrap: anywhere; }
.empty-actions { display: flex; gap: 6px; margin-top: 4px; flex-wrap: wrap; justify-content: center; }
</style>
