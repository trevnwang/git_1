<script setup lang="ts">
/**
 * 组件级批量操作条（FR-BOARD-04 / M2b / P1-2）：
 * - 常驻按钮：SYNC / CREATE / Clone / Delete；「更多」菜单补齐 PULL / FETCH / CHKOUT / TAG / RESTORE，
 *   未实现项（COMMIT/PUSH）显式禁用；
 * - 动作清单、文案与可用性来自 viewmodel.BATCH_ACTIONS + store.batchActionState（与卡片右键/菜单栏单源）；
 * - 点击先走确认/参数对话框（overlays），确认后才 runBatch（未确认 = 0 写）；
 * - P1-3：选择模式 0 勾选时命令禁用并提示「先勾选组件」，不隐式作用于当前组件。
 */
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { useWorkbench } from '../../stores/workbench'
import { BATCH_PRIMARY_ACTIONS, BATCH_SECONDARY_ACTIONS } from '../../viewmodel'
import type { BatchActionDef } from '../../viewmodel'

const w = useWorkbench()
const state = w.state

const checkedCount = computed(() => w.batchSelectionCount())
const blocked = computed(() => w.batchCommandsBlocked())
const moreOpen = ref(false)
const moreRoot = ref<HTMLElement | null>(null)

function run(action: BatchActionDef): void {
  moreOpen.value = false
  w.requestBatch(action.kind)
}

function actionTitle(action: BatchActionDef): string {
  const availability = w.batchActionState(action.kind)
  return availability.enabled ? action.title : availability.title
}

function toggleMore(): void {
  // 即使 0 勾选也展开菜单：让用户看到完整动作清单（各项禁用并给出原因），
  // 主按钮点击时再由 store 给出「先勾选组件」提示。
  moreOpen.value = !moreOpen.value
}

function onDocumentPointerDown(event: MouseEvent): void {
  const target = event.target as Node | null
  if (!moreRoot.value || !target || !moreRoot.value.contains(target)) moreOpen.value = false
}

onMounted(() => document.addEventListener('mousedown', onDocumentPointerDown))
onBeforeUnmount(() => document.removeEventListener('mousedown', onDocumentPointerDown))
</script>

<template>
  <footer class="batch-bar" data-testid="batch-bar" aria-label="组件级批量操作">
    <template v-if="!state.selectMode">
      <button
        type="button"
        class="batch-btn primary"
        data-testid="batch-select-toggle"
        title="进入批量选择模式（勾选组件后对选中集合执行 SYNC / CREATE / Clone / PULL / FETCH / CHKOUT / Tag / Restore / Delete，经任务中心执行）"
        aria-label="进入批量选择模式"
        @click="w.toggleSelectMode()"
      >☑</button>
      <span class="hint">批量</span>
    </template>
    <template v-else>
      <span
        class="count"
        :class="{ blocked }"
        :title="`已选 ${checkedCount} / ${state.components.length} 个组件；批量命令只对已勾选组件生效（未勾选不会作用于当前组件）`"
        data-testid="batch-count"
      >{{ checkedCount }}/{{ state.components.length }}</span>
      <span
        v-if="blocked"
        class="blocked-flag"
        data-testid="batch-blocked-hint"
        title="先勾选组件：批量命令只对已勾选组件生效"
        aria-label="先勾选组件"
        role="img"
      >⚠</span>
      <div class="commands">
        <button
          v-for="action in BATCH_PRIMARY_ACTIONS"
          :key="action.kind"
          type="button"
          class="batch-btn"
          :class="{ danger: action.danger, blocked: blocked }"
          :data-testid="`batch-${action.kind.toLowerCase()}`"
          :disabled="state.batchPending"
          :aria-disabled="blocked || state.batchPending"
          :aria-label="action.label"
          :title="actionTitle(action)"
          @click="run(action)"
        >{{ action.icon }}</button>
        <div ref="moreRoot" class="more-root">
          <button
            type="button"
            class="batch-btn more-btn"
            :class="{ blocked }"
            data-testid="batch-more"
            :disabled="state.batchPending"
            :aria-disabled="blocked || state.batchPending"
            aria-haspopup="menu"
            :aria-expanded="moreOpen"
            aria-label="更多批量操作"
            title="更多批量操作：PULL / FETCH / CHKOUT / Tag / Restore（COMMIT/PUSH 未接入，显式禁用）"
            @click.stop="toggleMore"
          >▾</button>
          <ul v-if="moreOpen" class="more-menu" role="menu" data-testid="batch-more-menu" aria-label="更多批量操作">
            <li v-for="action in BATCH_SECONDARY_ACTIONS" :key="action.kind" role="none">
              <button
                type="button"
                role="menuitem"
                class="more-item"
                :class="{ danger: action.danger }"
                :data-testid="`batch-more-${action.kind.toLowerCase()}`"
                :disabled="!w.batchActionState(action.kind).enabled"
                :aria-disabled="!w.batchActionState(action.kind).enabled"
                :title="actionTitle(action)"
                @click="run(action)"
              >
                <span class="more-icon" aria-hidden="true">{{ action.icon }}</span>
                <span class="more-label">{{ action.label }}</span>
              </button>
            </li>
          </ul>
        </div>
      </div>
      <button type="button" class="batch-btn" data-testid="batch-select-all" title="全选筛选结果" @click="w.selectAllVisible()">全</button>
      <button type="button" class="close-btn" data-testid="batch-select-toggle" title="退出批量选择" @click="w.clearChecked()">✕</button>
    </template>
  </footer>
</template>

<style scoped>
.batch-bar {
  position: relative;
  display: flex;
  align-items: center;
  gap: 4px;
  height: var(--layout-actions-h);
  padding: 0 6px;
  border: 1px solid var(--border);
  border-radius: var(--r-panel);
  background: var(--surface);
  overflow: visible;
  white-space: nowrap;
}
.hint { font-size: 10px; color: var(--text-2); overflow: hidden; text-overflow: ellipsis; }
.count { font-size: 10px; font-weight: 600; white-space: nowrap; padding: 0 2px; color: var(--text-2); flex: 0 0 auto; }
.count.blocked { color: var(--st-warning-fg); }
.blocked-flag {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 16px;
  height: 16px;
  flex: 0 0 auto;
  font-size: 11px;
  color: var(--st-warning-fg);
  border: 1px solid var(--st-warning-bd);
  background: var(--st-warning-bg);
  border-radius: 999px;
  cursor: help;
}
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
.batch-btn.blocked { opacity: 0.55; }
.batch-btn.blocked:hover:not(:disabled) { color: var(--st-warning-fg); border-color: var(--st-warning-bd); }
.more-root { position: relative; flex: 0 0 auto; }
.more-btn { font-size: 11px; padding: 0 4px; }
.more-menu {
  position: absolute;
  bottom: calc(100% + 6px);
  right: 0;
  z-index: var(--z-menu);
  min-width: 190px;
  margin: 0;
  padding: 4px;
  list-style: none;
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: var(--r-panel);
  box-shadow: 0 -8px 24px rgb(16 24 40 / 18%);
}
.more-item {
  width: 100%;
  height: 26px;
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 0 8px;
  border: 0;
  border-radius: 4px;
  background: transparent;
  color: var(--text);
  font-size: 11px;
  cursor: pointer;
  text-align: left;
  white-space: nowrap;
}
.more-item:hover:not(:disabled) { background: var(--surface-2); }
.more-item:disabled { opacity: 0.45; cursor: not-allowed; }
.more-item.danger { color: var(--st-danger-fg); }
.more-icon { width: 12px; text-align: center; color: var(--text-2); }
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
