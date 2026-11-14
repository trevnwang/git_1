<script setup lang="ts">
/**
 * 组件级批量操作条（FR-BOARD-04 / M2b）：SYNC / CREATE / Clone / Delete
 * 点击先走确认/参数对话框（overlays），确认后才 runBatch；提交后打开任务中心。
 */
import { computed } from 'vue'
import { useWorkbench } from '../../stores/workbench'
import type { BatchKind } from '../../types/workbench'

const w = useWorkbench()
const state = w.state

const checkedCount = computed(() => state.checkedIds.length)
const repoCount = computed(() => state.checkedIds.length * 4)

function run(kind: BatchKind): void {
  w.requestBatch(kind)
}
</script>

<template>
  <footer class="batch-bar" data-testid="batch-bar" aria-label="组件级批量操作">
    <template v-if="!state.selectMode">
      <button
        type="button"
        class="batch-btn primary"
        data-testid="batch-select-toggle"
        title="进入批量选择模式（对选中组件集合执行 SYNC / CREATE / Clone / Delete，经任务中心执行）"
        @click="w.toggleSelectMode()"
      >☑</button>
      <span class="hint">批量</span>
    </template>
    <template v-else>
      <span class="count" :title="`已选 ${checkedCount} 组件 / ${repoCount} 仓（未勾选时对当前选中组件执行）`">{{ checkedCount }}/{{ state.components.length }}</span>
      <div class="commands">
        <button
          type="button"
          class="batch-btn"
          data-testid="batch-sync"
          :disabled="state.batchPending"
          title="SYNC：远程覆盖本地（先预检 + 备份 ref，需确认）→ 任务中心"
          @click="run('SYNC')"
        >⟳</button>
        <button
          type="button"
          class="batch-btn"
          data-testid="batch-create"
          :disabled="state.batchPending"
          title="CREATE：四仓创建同名分支（需输入分支名）→ 任务中心"
          @click="run('CREATE')"
        >＋</button>
        <button
          type="button"
          class="batch-btn"
          data-testid="batch-clone"
          :disabled="state.batchPending"
          title="Clone 缺失仓（预览缺失清单，可选 dryRun）→ 任务中心"
          @click="run('CLONE')"
        >⤓</button>
        <button
          type="button"
          class="batch-btn danger"
          data-testid="batch-delete"
          :disabled="state.batchPending"
          title="Delete：仅本地软删到 .trash（保留 30 天，需确认）→ 任务中心"
          @click="run('DELETE')"
        >⋯</button>
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
.batch-btn:hover:not(:disabled) { background: var(--surface-2); color: var(--text); }
.batch-btn:disabled { opacity: 0.5; cursor: not-allowed; }
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
