<script setup lang="ts">
import { onMounted } from 'vue'
import { useWorkbench } from '../../stores/workbench'
import ComponentList from '../workbench/ComponentList.vue'
import BatchBar from '../workbench/BatchBar.vue'
import InfoPanel from '../workbench/InfoPanel.vue'
import RepoActionsBar from '../workbench/RepoActionsBar.vue'
import WorkspaceTree from './WorkspaceTree.vue'

const w = useWorkbench()

onMounted(() => {
  void w.ensureFolderLoaded()
})
</script>

<template>
  <div class="folder-view view-root" data-testid="folder-view" data-view="folder">
    <ComponentList class="area-list" />
    <BatchBar class="area-batch" />
    <WorkspaceTree class="area-tree" />
    <InfoPanel class="area-info" />
    <RepoActionsBar class="area-actions" />
  </div>
</template>

<style scoped>
.folder-view {
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
.area-tree { grid-column: 2; grid-row: 1; }
.area-info { grid-column: 3; grid-row: 1; }
.area-actions { grid-column: 2 / -1; grid-row: 2; }
</style>
