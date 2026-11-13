<script setup lang="ts">
import { computed } from 'vue'
import { useWorkbench } from '../../stores/workbench'

const w = useWorkbench()
const state = w.state

const checkedCount = computed(() => state.checkedIds.length)
const repoCount = computed(() => state.checkedIds.length * 4)
</script>

<template>
  <footer class="batch-bar" data-testid="batch-bar" aria-label="组件级批量操作">
    <template v-if="!state.selectMode">
      <button
        type="button"
        class="batch-btn primary"
        data-testid="batch-select-toggle"
        title="进入批量选择模式（对选中组件集合执行 SYNC / CREATE / Clone / Delete）"
        @click="w.toggleSelectMode()"
      >☑</button>
      <span class="hint">批量</span>
    </template>
    <template v-else>
      <span class="count" :title="`已选 ${checkedCount} 组件 / ${repoCount} 仓`">{{ checkedCount }}/{{ state.components.length }}</span>
      <div class="commands">
        <button type="button" class="batch-btn" title="SYNC（M2 接入）" @click="w.batchCommand('SYNC')">⟳</button>
        <button type="button" class="batch-btn" title="CREATE（M2 接入）" @click="w.batchCommand('CREATE')">＋</button>
        <button type="button" class="batch-btn" title="Clone 缺失仓（M2 接入）" @click="w.batchCommand('CLONE')">⤓</button>
        <button type="button" class="batch-btn danger" title="删除 / 恢复 / 纳入 / 移出（M2 接入）" @click="w.batchCommand('DELETE')">⋯</button>
      </div>
      <button type="button" class="batch-btn" title="全选筛选结果" @click="w.selectAllVisible()">全</button>
      <button type="button" class="close-btn" data-testid="batch-select-toggle" title="退出批量选择" @click="w.clearChecked()">✕</button>
    </template>
  </footer>
</template>

<style scoped>
.batch-bar {
  display: flex;
  align-items: center;
  gap: 4px;
  height: var(--layout-actions-h);
  padding: 0 6px;
  border: 1px solid var(--border);
  border-radius: var(--r-panel);
  background: var(--surface);
  overflow: hidden;
  white-space: nowrap;
}
.hint { font-size: 10px; color: var(--text-2); overflow: hidden; text-overflow: ellipsis; }
.count { font-size: 10px; font-weight: 600; white-space: nowrap; padding: 0 2px; color: var(--text-2); }
.commands { display: flex; flex: 0 0 auto; gap: 3px; }
.batch-btn {
  height: 26px;
  min-width: 26px;
  padding: 0 5px;
  border: 1px solid var(--border);
  border-radius: var(--r-btn);
  background: var(--surface);
  color: var(--text-2);
  font-size: 12px;
  font-weight: 600;
  cursor: pointer;
  white-space: nowrap;
}
.batch-btn:hover { background: var(--surface-2); color: var(--text); }
.batch-btn.primary { border-color: var(--st-info-bd); background: var(--st-info-bg); color: var(--st-info-fg); }
.batch-btn.danger { color: var(--st-danger-fg); border-color: var(--st-danger-bd); }
.close-btn {
  flex: 0 0 auto;
  width: 24px;
  height: 24px;
  border: 1px solid var(--border);
  border-radius: var(--r-btn);
  background: var(--surface);
  color: var(--text-2);
  cursor: pointer;
}
.close-btn:hover { color: var(--st-danger-fg); border-color: var(--st-danger-bd); }
@media (max-width: 1279px) {
  .hint { display: none; }
}
@media (prefers-reduced-motion: reduce) {
  .batch-btn, .close-btn { transition: none; }
}
</style>
