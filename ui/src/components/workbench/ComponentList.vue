<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref } from 'vue'
import { useVirtualRows } from '../../composables/useVirtualRows'
import { useWorkbench } from '../../stores/workbench'
import { REPO_LABEL } from '../../viewmodel'
import { REPO_TYPES } from '../../types/workbench'
import type { RepoType } from '../../types/workbench'
import ComponentCard from './ComponentCard.vue'
import EmptyState from '../common/EmptyState.vue'

const w = useWorkbench()
const state = w.state
const scroller = ref<HTMLElement | null>(null)
const compact = ref(false)

const SORT_LABEL: Record<string, string> = {
  severity: '严重度',
  id: '组件 ID',
  activity: '最近活动',
}

const visible = computed(() => w.visibleComponents())
const activeFilters = computed(() => w.activeFilterCount())
const rowHeight = computed(() => (compact.value ? 68 : 84))
const rows = useVirtualRows(scroller, () => rowHeight.value, () => visible.value.length, 6)

const facetStates = computed(() => {
  const set = new Set<string>()
  for (const component of state.components) set.add(component.state)
  return [...set].sort()
})

const selectedIndex = computed(() =>
  visible.value.findIndex((component) => component.id === state.selectedComponentId),
)

function measure(): void {
  compact.value = window.innerHeight < 800
}

onMounted(() => {
  measure()
  window.addEventListener('resize', measure)
})

onBeforeUnmount(() => window.removeEventListener('resize', measure))

function cycleSort(): void {
  const order: ('severity' | 'id' | 'activity')[] = ['severity', 'id', 'activity']
  const at = order.indexOf(state.sortBy)
  w.setSort(order[(at + 1) % order.length])
}

function move(delta: number): void {
  if (visible.value.length === 0) return
  const base = selectedIndex.value < 0 ? 0 : selectedIndex.value
  const next = Math.max(0, Math.min(visible.value.length - 1, base + delta))
  const component = visible.value[next]
  void w.selectComponent(component.id)
  rows.scrollToIndex(next)
  void nextTick(() => {
    const node = document.querySelector<HTMLElement>(`[data-component-id="${component.id}"]`)
    node?.focus()
  })
}

function focusFirst(): void {
  if (visible.value.length === 0) return
  const component = visible.value[0]
  void w.selectComponent(component.id)
  void nextTick(() => document.querySelector<HTMLElement>(`[data-component-id="${component.id}"]`)?.focus())
}

function focusLast(): void {
  if (visible.value.length === 0) return
  const component = visible.value[visible.value.length - 1]
  void w.selectComponent(component.id)
  rows.scrollToIndex(visible.value.length - 1)
  void nextTick(() => document.querySelector<HTMLElement>(`[data-component-id="${component.id}"]`)?.focus())
}

function quick(componentId: string, command: 'SYNC' | 'CREATE'): void {
  if (state.selectedComponentId !== componentId) void w.selectComponent(componentId)
  w.batchCommand(command)
}

function selectRepoFacet(event: Event): void {
  const value = (event.target as HTMLSelectElement).value
  w.setFilterRepoType(value ? (value as RepoType) : null)
}
</script>

<template>
  <section class="component-list w-panel" data-testid="component-list" aria-label="组件卡片列表">
    <header class="list-head">
      <span class="count" data-testid="component-list-count">组件 {{ visible.length }}</span>
      <span class="spacer" />
      <button
        type="button"
        class="head-btn"
        data-testid="component-sort"
        :title="`排序：${SORT_LABEL[state.sortBy]}（点击切换：严重度 → 组件 ID → 最近活动）`"
        @click="cycleSort"
      >⇅ {{ SORT_LABEL[state.sortBy] }}</button>
      <button
        type="button"
        class="head-btn"
        data-testid="component-filter"
        :class="{ active: activeFilters > 0 || state.overlays.filterOpen }"
        :title="`筛选（聚合状态 / 仓类型 / 改动 / 未推送），已启用 ${activeFilters} 项`"
        :aria-expanded="state.overlays.filterOpen"
        @click="state.overlays.filterOpen ? w.closeFilter() : w.openFilter()"
      >⏳ 筛选<span v-if="activeFilters > 0" class="filter-count">{{ activeFilters }}</span></button>
      <button
        type="button"
        class="head-btn"
        data-testid="component-select-toggle"
        :class="{ active: state.selectMode }"
        :aria-pressed="state.selectMode"
        title="批量选择模式"
        @click="w.toggleSelectMode()"
      >☑</button>
    </header>

    <div v-if="state.overlays.filterOpen" class="filter-pop" role="dialog" aria-label="筛选面板">
      <div class="filter-row">
        <span class="filter-label">聚合状态</span>
        <div class="facet-chips">
          <button
            v-for="facet in facetStates"
            :key="facet"
            type="button"
            class="facet-chip"
            :class="{ on: state.filter.states.includes(facet) }"
            @click="w.toggleFilterState(facet)"
          >{{ facet }}</button>
        </div>
      </div>
      <div class="filter-row">
        <span class="filter-label">仓类型</span>
        <select :value="state.filter.repoType ?? ''" aria-label="仓类型筛选" @change="selectRepoFacet">
          <option value="">全部仓类型</option>
          <option v-for="type in REPO_TYPES" :key="type" :value="type">{{ REPO_LABEL[type] }}</option>
        </select>
      </div>
      <div class="filter-row">
        <label class="check-line"><input v-model="state.filter.dirtyOnly" type="checkbox" /> 仅看有未提交改动</label>
        <label class="check-line"><input v-model="state.filter.unpushedOnly" type="checkbox" /> 仅看有未推送提交</label>
      </div>
      <div class="filter-row">
        <span class="filter-label">CI 状态</span>
        <select
          data-testid="ci-facet"
          disabled
          title="CI 集成将在后续版本开放（CI-7 禁用占位）"
          aria-label="CI 状态筛选（即将支持）"
        >
          <option>CI 状态（即将支持）</option>
        </select>
      </div>
      <div class="filter-actions">
        <button type="button" class="head-btn" @click="w.clearFilter()">清除筛选</button>
        <button type="button" class="head-btn" @click="w.closeFilter()">关闭</button>
      </div>
    </div>

    <div
      ref="scroller"
      class="list-body"
      data-testid="ci-card-base"
      data-ci-affects-base="false"
      role="listbox"
      aria-label="组件列表（CI 不影响卡片底色）"
      tabindex="0"
      :aria-activedescendant="selectedIndex >= 0 ? `card-${visible[selectedIndex]?.id}` : undefined"
      @scroll="rows.onScroll"
      @keydown.down.prevent="move(1)"
      @keydown.up.prevent="move(-1)"
      @keydown.home.prevent="focusFirst"
      @keydown.end.prevent="focusLast"
      @keydown.enter.prevent="focusFirst"
    >
      <div v-if="state.booted && visible.length === 0" class="list-empty">
        <EmptyState
          icon="∅"
          title="未匹配到组件"
          :desc="state.search || activeFilters ? '当前搜索/筛选条件下没有组件，试试清除筛选。' : '未发现组件，请检查工作区配置或重新扫描。'"
        >
          <button type="button" class="head-btn" @click="w.clearFilter(); w.setSearch('')">清除筛选</button>
          <button type="button" class="head-btn" @click="w.refresh()">重新扫描</button>
        </EmptyState>
      </div>
      <div v-else class="list-inner" :style="{ height: `${rows.totalHeight.value}px` }">
        <div class="list-window" :style="{ transform: `translateY(${rows.offsetY.value}px)` }">
          <ComponentCard
            v-for="component in visible.slice(rows.start.value, rows.end.value)"
            :key="component.id"
            :id="`card-${component.id}`"
            :component="component"
            :selected="component.id === state.selectedComponentId"
            :checked="state.checkedIds.includes(component.id)"
            :select-mode="state.selectMode"
            @select="w.selectComponent(component.id)"
            @detail="w.openComponentDrawer(component.id)"
            @check="w.toggleChecked(component.id)"
            @quick="quick(component.id, $event)"
            @contextmenu="w.cardContextMenu($event, component.id)"
          />
        </div>
      </div>
    </div>

    <footer v-if="state.selectMode" class="select-hint">
      <span>已选 {{ state.checkedIds.length }} / {{ visible.length }}</span>
      <button type="button" class="head-btn" @click="w.selectAllVisible()">全选结果</button>
      <button type="button" class="head-btn" @click="w.clearChecked()">✕ 清除</button>
    </footer>
  </section>
</template>

<style scoped>
.component-list {
  position: relative;
  min-width: 0;
  min-height: 0;
}
.list-head {
  flex: 0 0 40px;
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 0 8px;
  border-bottom: 1px solid var(--border);
  background: var(--surface-2);
  overflow: hidden;
}
.count { font-size: 12px; font-weight: 650; white-space: nowrap; }
.spacer { flex: 1; }
.head-btn {
  height: 24px;
  padding: 0 6px;
  border: 1px solid var(--border);
  border-radius: var(--r-btn);
  background: var(--surface);
  color: var(--text-2);
  font-size: 11px;
  cursor: pointer;
  white-space: nowrap;
}
.head-btn:hover { background: var(--surface-2); color: var(--text); }
.head-btn.active { border-color: var(--st-info-bd); background: var(--st-info-bg); color: var(--st-info-fg); }
.filter-count {
  display: inline-grid;
  place-items: center;
  min-width: 14px;
  height: 14px;
  margin-left: 3px;
  padding: 0 3px;
  border-radius: 7px;
  background: var(--st-info-fg);
  color: #fff;
  font-size: 9px;
}
.filter-pop {
  position: absolute;
  top: 40px;
  right: 4px;
  z-index: var(--z-popover);
  width: min(300px, calc(100% - 8px));
  padding: 10px;
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: var(--r-panel);
  box-shadow: 0 8px 24px rgb(16 24 40 / 18%);
  display: flex;
  flex-direction: column;
  gap: 8px;
}
.filter-row { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; }
.filter-label { font-size: 11px; color: var(--text-2); min-width: 52px; }
.facet-chips { display: flex; flex-wrap: wrap; gap: 4px; }
.facet-chip {
  height: 20px;
  padding: 0 6px;
  border: 1px solid var(--border);
  border-radius: var(--r-chip);
  background: var(--surface);
  color: var(--text-2);
  font-size: 10px;
  cursor: pointer;
}
.facet-chip.on { border-color: var(--st-info-bd); background: var(--st-info-bg); color: var(--st-info-fg); }
.filter-row select {
  height: 26px;
  border: 1px solid var(--border);
  border-radius: var(--r-btn);
  background: var(--surface);
  color: var(--text);
  font: inherit;
  font-size: 11px;
  padding: 0 6px;
}
.filter-row select:disabled { opacity: 0.55; }
.check-line { font-size: 11px; color: var(--text-2); display: inline-flex; align-items: center; gap: 4px; }
.filter-actions { display: flex; justify-content: flex-end; gap: 6px; }
.list-body {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  overflow-x: hidden;
  overscroll-behavior: contain;
  padding: 8px;
  scrollbar-gutter: stable;
  outline: none;
}
.list-body:focus-visible { box-shadow: inset 0 0 0 2px var(--focus); }
.list-inner { position: relative; }
.list-window { display: flex; flex-direction: column; gap: 0; }
.list-window :deep(.component-card) { margin-bottom: 8px; }
.list-empty { height: 100%; }
.select-hint {
  flex: 0 0 auto;
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 4px 8px;
  border-top: 1px solid var(--border);
  background: var(--surface-2);
  font-size: 11px;
  color: var(--text-2);
}
</style>
