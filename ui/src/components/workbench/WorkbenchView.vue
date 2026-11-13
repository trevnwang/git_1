<script setup lang="ts">
import ComponentList from './ComponentList.vue'
import BatchBar from './BatchBar.vue'
import RepoGrid from './RepoGrid.vue'
import InfoPanel from './InfoPanel.vue'
import RepoActionsBar from './RepoActionsBar.vue'
</script>

<template>
  <div class="workbench view-root" data-view="workbench">
    <ComponentList class="area-list" />
    <BatchBar class="area-batch" />
    <RepoGrid class="area-grid" />
    <InfoPanel class="area-info" />
    <RepoActionsBar class="area-actions" />
  </div>
</template>

<style scoped>
.workbench {
  display: grid;
  grid-template-columns: var(--col-left) minmax(0, 1fr) var(--col-right);
  grid-template-rows: minmax(0, 1fr) var(--layout-actions-h);
  gap: var(--layout-gap);
  min-height: 0;
  min-width: 0;
  height: 100%;
}
.area-list { grid-column: 1; grid-row: 1; }
.area-batch { grid-column: 1; grid-row: 2; }
.area-grid { grid-column: 2; grid-row: 1; }
.area-info { grid-column: 3; grid-row: 1; }
.area-actions { grid-column: 2 / -1; grid-row: 2; }
</style>
