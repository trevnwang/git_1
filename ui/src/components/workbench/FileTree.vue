<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useVirtualRows } from '../../composables/useVirtualRows'
import { gitStatusLetter, gitStatusTone, fileExtension } from '../../viewmodel'
import type { RepoType, TreeNodeVm } from '../../types/workbench'

const props = withDefaults(
  defineProps<{
    entries: TreeNodeVm[]
    expanded: Set<string>
    loadingPaths: Set<string>
    selectedPath: string | null
    repoType: RepoType
    initialScrollTop?: number
    compact?: boolean
  }>(),
  { initialScrollTop: 0, compact: false },
)

const emit = defineEmits<{
  toggle: [path: string]
  select: [path: string | null]
  open: [node: TreeNodeVm]
  focusTree: []
  contextmenu: [event: MouseEvent, node: TreeNodeVm]
  scroll: [scrollTop: number]
}>()

interface Row {
  key: string
  node: TreeNodeVm | null
  depth: number
  synthetic: 'loading' | 'empty' | null
}

const scroller = ref<HTMLElement | null>(null)

const rows = computed<Row[]>(() => {
  const output: Row[] = []
  const walk = (nodes: TreeNodeVm[], depth: number) => {
    for (const node of nodes) {
      output.push({ key: node.path, node, depth, synthetic: null })
      if (node.kind === 'DIR' && props.expanded.has(node.path)) {
        if (props.loadingPaths.has(node.path)) {
          output.push({ key: `${node.path}::loading`, node: null, depth: depth + 1, synthetic: 'loading' })
        } else if (node.children.length === 0) {
          output.push({ key: `${node.path}::empty`, node: null, depth: depth + 1, synthetic: 'empty' })
        } else {
          walk(node.children, depth + 1)
        }
      }
    }
  }
  walk(props.entries, 0)
  return output
})

const rowHeight = computed(() => (props.compact ? 26 : 28))
const virtual = useVirtualRows(scroller, () => rowHeight.value, () => rows.value.length, 8)

const visibleRows = computed(() => rows.value.slice(virtual.start.value, virtual.end.value))
const selectedKey = computed(() => props.selectedPath)
const selectedRowIndex = computed(() => rows.value.findIndex((row) => row.node?.path === props.selectedPath))

function indent(depth: number): string {
  return `${Math.min(depth * 14, 112)}px`
}

function isExpanded(node: TreeNodeVm): boolean {
  return props.expanded.has(node.path)
}

function icon(node: TreeNodeVm): string {
  if (node.kind === 'DIR') return isExpanded(node) ? '▾' : '▸'
  const ext = fileExtension(node.name)
  if (['.m', '.c', '.h', '.cpp', '.hpp'].includes(ext)) return '•'
  if (['.md', '.txt'].includes(ext)) return '≡'
  if (['.json', '.xml', '.yaml', '.yml', '.ini'].includes(ext)) return '⚙'
  if (['.slx', '.mdl'].includes(ext)) return '◫'
  return '▣'
}

function onRowClick(row: Row): void {
  if (!row.node) return
  emit('focusTree')
  emit('select', row.node.path)
}

function onToggle(row: Row): void {
  if (!row.node || row.node.kind !== 'DIR') return
  emit('toggle', row.node.path)
}

function onOpen(row: Row): void {
  if (!row.node) return
  emit('focusTree')
  emit('select', row.node.path)
  emit('open', row.node)
}

function onContextMenu(event: MouseEvent, row: Row): void {
  if (!row.node) return
  event.preventDefault()
  emit('contextmenu', event, row.node)
}

function firstChildIndex(index: number): number {
  const row = rows.value[index]
  if (!row?.node) return index
  for (let cursor = index + 1; cursor < rows.value.length; cursor += 1) {
    if ((rows.value[cursor].depth ?? 0) <= row.depth) break
    if (rows.value[cursor].node) return cursor
  }
  return index
}

function parentIndex(index: number): number {
  const row = rows.value[index]
  if (!row) return index
  for (let cursor = index - 1; cursor >= 0; cursor -= 1) {
    if ((rows.value[cursor].depth ?? 0) < row.depth) return cursor
  }
  return index
}

function selectIndex(index: number): void {
  const row = rows.value[index]
  if (!row?.node) return
  emit('select', row.node.path)
}

function onKeydown(event: KeyboardEvent): void {
  const index = selectedRowIndex.value
  switch (event.key) {
    case 'ArrowDown':
      event.preventDefault()
      selectIndex(Math.min(rows.value.length - 1, index + 1))
      break
    case 'ArrowUp':
      event.preventDefault()
      selectIndex(Math.max(0, index - 1))
      break
    case 'ArrowRight': {
      event.preventDefault()
      const row = rows.value[index]
      if (!row?.node) return
      if (row.node.kind === 'DIR' && !isExpanded(row.node)) {
        emit('toggle', row.node.path)
      } else if (row.node.kind === 'DIR') {
        selectIndex(firstChildIndex(index))
      }
      break
    }
    case 'ArrowLeft': {
      event.preventDefault()
      const row = rows.value[index]
      if (!row?.node) return
      if (row.node.kind === 'DIR' && isExpanded(row.node)) {
        emit('toggle', row.node.path)
      } else {
        selectIndex(parentIndex(index))
      }
      break
    }
    case 'Home':
      event.preventDefault()
      selectIndex(0)
      break
    case 'End':
      event.preventDefault()
      selectIndex(rows.value.length - 1)
      break
    case 'Enter': {
      event.preventDefault()
      const row = rows.value[index]
      if (row?.node) {
        emit('open', row.node)
        emit('select', row.node.path)
      }
      break
    }
    case ' ': {
      event.preventDefault()
      const row = rows.value[index]
      if (row?.node?.kind === 'DIR') emit('toggle', row.node.path)
      break
    }
  }
}

function onScroll(): void {
  virtual.onScroll()
  emit('scroll', scroller.value?.scrollTop ?? 0)
}

onMounted(() => {
  if (scroller.value && props.initialScrollTop > 0) {
    scroller.value.scrollTop = props.initialScrollTop
    virtual.onScroll()
  }
})
</script>

<template>
  <div class="file-tree" role="tree" :aria-label="`${repoType} 文件树`">
    <div
      ref="scroller"
      class="tree-scroller"
      tabindex="0"
      :aria-activedescendant="selectedRowIndex >= 0 ? `tree-active-${repoType}` : undefined"
      @scroll="onScroll"
      @keydown="onKeydown"
    >
      <div class="tree-inner" :style="{ height: `${virtual.totalHeight.value}px` }">
        <div class="tree-window" :style="{ transform: `translateY(${virtual.offsetY.value}px)` }">
          <div
            v-for="row in visibleRows"
            :key="row.key"
            class="tree-row"
            :class="{
              dir: row.node?.kind === 'DIR',
              selected: row.node?.path === selectedKey,
              ignored: row.node?.ignored,
              synthetic: Boolean(row.synthetic),
              error: Boolean(row.node?.error),
            }"
            :style="{ height: `${rowHeight}px`, paddingLeft: indent(row.depth) }"
            role="treeitem"
            :aria-level="row.depth + 1"
            :aria-selected="row.node?.path === selectedKey"
            :aria-expanded="row.node?.kind === 'DIR' ? isExpanded(row.node) : undefined"
            :data-path="row.node?.path"
            :id="row.node?.path === selectedKey ? `tree-active-${repoType}` : undefined"
            data-testid="tree-node"
            @click="onRowClick(row)"
            @dblclick.stop="onOpen(row)"
            @contextmenu="onContextMenu($event, row)"
          >
            <template v-if="row.synthetic">
              <span class="synthetic-text" :class="{ error: row.synthetic === 'empty' }">{{
                row.synthetic === 'loading' ? '加载中…' : '（空目录）'
              }}</span>
            </template>
            <template v-else-if="row.node">
              <button
                v-if="row.node.kind === 'DIR'"
                type="button"
                class="tree-toggle"
                data-testid="tree-toggle"
                :aria-expanded="isExpanded(row.node)"
                :aria-label="`${isExpanded(row.node) ? '折叠' : '展开'} ${row.node.name}`"
                @click.stop="onToggle(row)"
              >{{ isExpanded(row.node) ? '▾' : '▸' }}</button>
              <span v-else class="tree-toggle placeholder" aria-hidden="true" />
              <span class="node-icon" aria-hidden="true">{{ icon(row.node) }}</span>
              <span class="node-name" :title="row.node.path">
                {{ row.node.name }}<span v-if="row.node.submodule" class="submodule" title="未纳管（submodule）">⟲</span>
              </span>
              <span
                v-if="row.node.gitStatus !== 'NONE'"
                class="git-status"
                :class="`tone-${gitStatusTone(row.node.gitStatus)}`"
                :title="row.node.gitStatus"
              >{{ gitStatusLetter(row.node.gitStatus) }}</span>
              <span
                v-else-if="row.node.error"
                class="git-status tone-danger"
                data-testid="tree-node-error"
                :title="`目录读取错误：${row.node.error}`"
              >!</span>
            </template>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.file-tree { height: 100%; min-height: 0; display: flex; flex-direction: column; }
.tree-scroller {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  overflow-x: hidden;
  overscroll-behavior: contain;
  outline: none;
}
.tree-scroller:focus-visible { box-shadow: inset 0 0 0 2px var(--focus); }
.tree-inner { position: relative; }
.tree-window { position: absolute; left: 0; right: 0; top: 0; }
.tree-row {
  display: flex;
  align-items: center;
  gap: 4px;
  padding-right: 6px;
  font-size: 12px;
  color: var(--text);
  cursor: default;
  white-space: nowrap;
  border: 0;
}
.tree-row:hover { background: color-mix(in srgb, var(--surface-2) 55%, transparent); }
.tree-row.selected { background: color-mix(in srgb, var(--st-info-bg) 40%, transparent); box-shadow: inset 2px 0 0 var(--focus); }
.tree-row.ignored { opacity: 0.6; }
.tree-row.synthetic { color: var(--text-2); font-size: 11px; }
.tree-row.error { color: var(--st-danger-fg); }
.tree-toggle {
  width: 16px;
  height: 16px;
  flex: 0 0 16px;
  display: inline-grid;
  place-items: center;
  padding: 0;
  border: 0;
  border-radius: 3px;
  background: transparent;
  color: var(--text-2);
  font-size: 9px;
  cursor: pointer;
}
.tree-toggle:hover { background: var(--surface-2); color: var(--text); }
.tree-toggle.placeholder { cursor: default; }
.node-icon { width: 14px; flex: 0 0 14px; text-align: center; color: var(--text-2); font-size: 11px; }
.node-name { flex: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.dir .node-name { font-weight: 600; }
.submodule { margin-left: 3px; color: var(--st-warning-fg); }
.git-status { flex: 0 0 16px; text-align: right; font-family: ui-monospace, Consolas, monospace; font-size: 10px; font-weight: 700; }
.tone-warning { color: var(--st-warning-fg); }
.tone-success { color: var(--st-success-fg); }
.tone-danger { color: var(--st-danger-fg); }
.synthetic-text { color: var(--text-2); font-size: 11px; }
.synthetic-text.error { color: var(--st-danger-fg); }
</style>
