<script setup lang="ts">
import RepoTreePanel from './RepoTreePanel.vue'
import { REPO_TYPES } from '../../types/workbench'
</script>

<template>
  <div class="repo-grid" data-testid="repo-grid" aria-label="四仓文件树（Design / Impl / MIL / SIL）">
    <RepoTreePanel v-for="type in REPO_TYPES" :key="type" :repo-type="type" />
  </div>
</template>

<style scoped>
.repo-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  grid-template-rows: repeat(2, minmax(0, 1fr));
  gap: var(--layout-gap);
  min-width: 0;
  min-height: 0;
}
</style>
