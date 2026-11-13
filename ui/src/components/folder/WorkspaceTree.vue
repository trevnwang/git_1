<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { useVirtualRows } from '../../composables/useVirtualRows'
import { useWorkbench } from '../../stores/workbench'
import { formatAgo, REPO_LABEL } from '../../viewmodel'
import type { WorkspaceNodeVm } from '../../types/workbench'
import EmptyState from '../common/EmptyState.vue'
import StateChip from '../common/StateChip.vue'

const w = useWorkbench()
const state = w.state
const scroller = ref<HTMLElement | null>(null)
const filter = ref('')
const compact = ref(false)

interface WsRow {
  key: string
  node: WorkspaceNodeVm
  depth: number
  loading: boolean
  repo: boolean
}

function childrenOf(node: WorkspaceNodeVm): WorkspaceNodeVm[] {
  if (node.kind === 'COMPONENT') return node.repos
  return node.children
}

function nodeMatches(node: WorkspaceNodeVm, query: string): boolean {
  const text = `${node.label} ${node.path} ${node.state} ${node.repoType ?? ''}`.toLowerCase()
  return text.includes(query)
}

function buildRows(): WsRow[] {
  const query = filter.value.trim().toLowerCase()
  const kept = new Set<string>()

  const mark = (nodes: WorkspaceNodeVm[], depth: number): boolean => {
    let any = false
    for (const node of nodes) {
      const children = childrenOf(node)
      let childMatched = false
      if (node.kind === 'COMPONENT' || state.folder.expanded.has(node.key)) {
        childMatched = mark(children, depth + 1)
      }
      const self = !query || nodeMatches(node, query)
      const keep = self || childMatched
      if (keep) kept.add(node.key)
      any = any || keep
    }
    return any
  }
  mark(state.folder.entries, 0)

  const rows: WsRow[] = []
  const push = (nodes: WorkspaceNodeVm[], depth: number): void => {
    for (const node of nodes) {
      if (query && !kept.has(node.key)) continue
      const isRepo = node.kind === 'REPO'
      rows.push({
        key: node.key,
        node,
        depth,
        loading: state.folder.loadingPaths.has(node.key),
        repo: isRepo,
      })
      const expanded = state.folder.expanded.has(node.key)
      if (node.kind === 'COMPONENT' ? expanded : expanded && node.loaded) {
        if (!state.folder.loadingPaths.has(node.key)) push(childrenOf(node), depth + 1)
      }
    }
  }
  push(state.folder.entries, 0)
  return rows
}

const rows = computed<WsRow[]>(buildRows)
const rowHeight = computed(() => (compact.value ? 26 : 28))
const virtual = useVirtualRows(scroller, () => rowHeight.value, () => rows.value.length, 8)
const visibleRows = computed(() => rows.value.slice(virtual.start.value, virtual.end.value))

const selectedKey = computed(() => state.folderSelected?.key ?? '')
const selectedIndex = computed(() => rows.value.findIndex((row) => row.node.key === selectedKey.value))

function icon(node: WorkspaceNodeVm): string {
  if (node.kind === 'DIR' || node.kind === 'REPO' || node.kind === 'UNBOUND_DIR') {
    return state.folder.expanded.has(node.key) ? '▾' : '▸'
  }
  if (node.kind === 'COMPONENT') return state.folder.expanded.has(node.key) ? '▾' : '▸'
  if (node.kind === 'UNBOUND_GROUP') return '⚠'
  return '▣'
}

function canExpand(node: WorkspaceNodeVm): boolean {
  if (node.kind === 'COMPONENT') return node.repos.length > 0
  if (node.kind === 'FILE') return false
  if (node.kind === 'UNBOUND_GROUP') return true
  if (node.kind === 'REPO') return node.hasChildren
  return true
}

function displayState(node: WorkspaceNodeVm): string {
  if (node.kind === 'COMPONENT' || node.kind === 'REPO') return node.state
  return node.gitStatus !== 'NONE' ? node.gitStatus : ''
}

function repoLabel(node: WorkspaceNodeVm): string {
  return node.repoType ? REPO_LABEL[node.repoType] : '仓'
}

function indent(depth: number): string {
  return `${Math.min(12 + depth * 14, 120)}px`
}

function onToggle(row: WsRow): void {
  if (!canExpand(row.node)) return
  void w.toggleFolderNode(row.node)
}

function onSelect(row: WsRow): void {
  w.selectFolderNode(row.node)
}

function onOpen(row: WsRow): void {
  void w.openFolderNode(row.node)
}

function onArrowToggle(row: WsRow): void {
  if (!canExpand(row.node)) return
  void w.toggleFolderNode(row.node)
}

function focusRow(index: number): void {
  const row = rows.value[index]
  if (!row) return
  w.selectFolderNode(row.node)
  virtual.scrollToIndex(index)
}

function onKeydown(event: KeyboardEvent): void {
  const index = selectedIndex.value
  switch (event.key) {
    case 'ArrowDown':
      event.preventDefault()
      focusRow(Math.min(rows.value.length - 1, index + 1))
      break
    case 'ArrowUp':
      event.preventDefault()
      focusRow(Math.max(0, index - 1))
      break
    case 'ArrowRight': {
      event.preventDefault()
      const row = rows.value[index]
      if (!row) return
      if (canExpand(row.node) && !state.folder.expanded.has(row.node.key)) onArrowToggle(row)
      else focusRow(Math.min(rows.value.length - 1, index + 1))
      break
    }
    case 'ArrowLeft': {
      event.preventDefault()
      const row = rows.value[index]
      if (!row) return
      if (state.folder.expanded.has(row.node.key)) onArrowToggle(row)
      break
    }
    case 'Enter': {
      event.preventDefault()
      const row = rows.value[index]
      if (row) onOpen(row)
      break
    }
    case ' ': {
      event.preventDefault()
      const row = rows.value[index]
      if (row) onArrowToggle(row)
      break
    }
  }
}

function measure(): void {
  compact.value = window.innerHeight < 800
}

onMounted(() => {
  measure()
  window.addEventListener('resize', measure)
})

onBeforeUnmount(() => window.removeEventListener('resize', measure))
</script>

<template>
  <section class="workspace-tree w-panel" data-testid="workspace-tree" aria-label="文件夹视图（工作区目录树）">
    <header class="ws-head">
      <span class="ws-title">工作区</span>
      <span class="ws-root truncate mono" :title="state.folder.root || state.workspaceRoot">{{ state.folder.root || state.workspaceRoot || '（未配置工作区）' }}</span>
      <span class="ws-count muted">{{ rows.length }} 节点</span>
      <input
        v-model="filter"
        class="ws-filter"
        type="search"
        placeholder="筛选已加载节点…"
        aria-label="在文件夹视图中筛选"
      />
      <button
        type="button"
        class="icon-btn"
        title="刷新工作区树"
        aria-label="刷新工作区树"
        :aria-busy="state.folder.status === 'loading'"
        @click="w.refreshFolder()"
      >⟳</button>
    </header>

    <div class="ws-body">
      <div v-if="state.folder.status === 'loading' && state.folder.entries.length === 0" class="ws-skeleton">
        <div v-for="index in 10" :key="index" class="skeleton-row" :style="{ width: `${40 + ((index * 13) % 55)}%` }" />
      </div>
      <EmptyState
        v-else-if="state.folder.status === 'error'"
        icon="✕"
        title="无法读取工作区"
        :desc="`${state.folder.errorCode} ${state.folder.error}`"
      >
        <button type="button" class="ws-btn" @click="w.refreshFolder()">重试</button>
      </EmptyState>
      <EmptyState
        v-else-if="state.folder.entries.length === 0"
        icon="∅"
        title="工作区为空"
        desc="未发现组件目录；请检查工作区配置或重新扫描。"
      >
        <button type="button" class="ws-btn" @click="w.refreshFolder()">重新扫描</button>
        <button type="button" class="ws-btn" @click="w.openModal('settings')">打开设置</button>
      </EmptyState>
      <div
        v-else
        ref="scroller"
        class="ws-scroller"
        role="tree"
        aria-label="工作区目录树"
        tabindex="0"
        @scroll="virtual.onScroll"
        @keydown="onKeydown"
      >
        <div class="ws-inner" :style="{ height: `${virtual.totalHeight.value}px` }">
          <div class="ws-window" :style="{ transform: `translateY(${virtual.offsetY.value}px)` }">
            <div
              v-for="row in visibleRows"
              :key="row.key"
              class="ws-row"
              :class="{
                selected: row.node.key === selectedKey,
                file: row.node.kind === 'FILE',
                repo: row.repo,
                missing: row.node.state === 'MISSING',
                error: Boolean(row.node.error),
              }"
              :style="{ height: `${rowHeight}px`, paddingLeft: indent(row.depth) }"
              role="treeitem"
              :aria-level="row.depth + 1"
              :aria-selected="row.node.key === selectedKey"
              :aria-expanded="canExpand(row.node) ? state.folder.expanded.has(row.node.key) : undefined"
              data-testid="workspace-node"
              :data-kind="row.node.kind"
              :data-path="row.node.path"
              @click="onSelect(row)"
              @dblclick.stop="onOpen(row)"
            >
              <button
                v-if="canExpand(row.node)"
                type="button"
                class="ws-toggle"
                :aria-expanded="state.folder.expanded.has(row.node.key)"
                :aria-label="`${state.folder.expanded.has(row.node.key) ? '折叠' : '展开'} ${row.node.label}`"
                data-testid="workspace-toggle"
                @click.stop="onToggle(row)"
              >{{ state.folder.expanded.has(row.node.key) ? '▾' : '▸' }}</button>
              <span v-else class="ws-toggle placeholder" aria-hidden="true" />
              <span class="ws-icon" aria-hidden="true">{{ icon(row.node) }}</span>
              <span v-if="row.repo" class="repo-type">{{ REPO_LABEL[row.node.repoType ?? 'IMPL'] }}</span>
              <span class="ws-label truncate" :title="row.node.path || row.node.label">{{ row.node.label }}</span>
              <span v-if="row.loading" class="loading">加载中…</span>
              <span v-else-if="row.node.state && (row.node.kind === 'COMPONENT' || row.node.kind === 'REPO')" class="ws-state">
                <StateChip :state="row.node.state" compact />
                <span v-if="row.node.kind === 'REPO' && row.node.state === 'MISSING'" class="missing-mark">未克隆</span>
                <span v-else-if="row.node.kind === 'REPO'" class="repo-meta muted">
                  {{ row.node.branch ?? '—' }}<template v-if="row.node.ahead"> ↑{{ row.node.ahead }}</template><template v-if="row.node.behind"> ↓{{ row.node.behind }}</template>
                </span>
              </span>
              <span v-else-if="row.node.kind === 'COMPONENT'" class="comp-meta">
                <span v-for="item in row.node.repos" :key="item.key" class="mini-dot" :class="`tone-${item.tone}`" :title="`${repoLabel(item)}：${item.state}`" />
              </span>
              <span v-else-if="displayState(row.node)" class="git-letter">{{ displayState(row.node).slice(0, 1) }}</span>
              <span v-if="row.node.kind === 'FILE'" class="muted file-time">{{ formatAgo(row.node.mtime, state.now) }}</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  </section>
</template>

<style scoped>
.workspace-tree { min-width: 0; min-height: 0; height: 100%; }
.ws-head {
  flex: 0 0 36px;
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 0 8px;
  border-bottom: 1px solid var(--border);
  background: var(--surface-2);
  white-space: nowrap;
  overflow: hidden;
}
.ws-title { font-size: 11px; font-weight: 700; }
.ws-root { font-size: 11px; color: var(--text-2); max-width: 40%; }
.ws-count { font-size: 10px; }
.ws-filter {
  margin-left: auto;
  width: clamp(120px, 16vw, 200px);
  height: 24px;
  border: 1px solid var(--border);
  border-radius: var(--r-btn);
  background: var(--surface);
  color: var(--text);
  font: inherit;
  font-size: 11px;
  padding: 0 6px;
  outline: none;
}
.ws-filter:focus { border-color: var(--focus); }
.ws-body { flex: 1; min-height: 0; display: flex; flex-direction: column; }
.ws-scroller { flex: 1; min-height: 0; overflow-y: auto; overflow-x: hidden; overscroll-behavior: contain; outline: none; }
.ws-scroller:focus-visible { box-shadow: inset 0 0 0 2px var(--focus); }
.ws-inner { position: relative; }
.ws-window { position: absolute; left: 0; right: 0; top: 0; }
.ws-row {
  display: flex;
  align-items: center;
  gap: 4px;
  padding-right: 8px;
  font-size: 12px;
  white-space: nowrap;
  cursor: default;
  color: var(--text);
}
.ws-row:hover { background: color-mix(in srgb, var(--surface-2) 55%, transparent); }
.ws-row.selected { background: color-mix(in srgb, var(--st-info-bg) 40%, transparent); box-shadow: inset 2px 0 0 var(--focus); }
.ws-row.missing { opacity: 0.6; }
.ws-toggle {
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
.ws-toggle:hover { background: var(--surface-2); }
.ws-toggle.placeholder { cursor: default; }
.ws-icon { width: 14px; flex: 0 0 14px; text-align: center; color: var(--text-2); font-size: 11px; }
.repo-type {
  flex: 0 0 auto;
  font-size: 9px;
  font-weight: 700;
  letter-spacing: 0.2px;
  color: var(--st-info-fg);
  background: var(--st-info-bg);
  border: 1px solid var(--st-info-bd);
  border-radius: 4px;
  padding: 0 3px;
}
.ws-label { flex: 1; min-width: 0; }
.file .ws-label { color: var(--text-2); }
.ws-state { display: inline-flex; align-items: center; gap: 4px; flex: 0 0 auto; }
.ws-state .loading { font-size: 10px; color: var(--text-2); }
.repo-meta { font-size: 10px; font-family: ui-monospace, Consolas, monospace; }
.missing-mark { font-size: 10px; color: var(--st-neutral-fg); border: 1px dashed var(--st-neutral-bd); border-radius: 4px; padding: 0 3px; }
.comp-meta { display: inline-flex; gap: 3px; flex: 0 0 auto; }
.mini-dot { width: 7px; height: 7px; border-radius: 50%; background: var(--rail); }
.mini-dot.tone-success { background: var(--st-success-fg); }
.mini-dot.tone-warning { background: var(--st-warning-fg); }
.mini-dot.tone-info { background: var(--st-info-fg); }
.mini-dot.tone-purple { background: var(--st-purple-fg); }
.mini-dot.tone-cyan { background: var(--st-cyan-fg); }
.mini-dot.tone-danger { background: var(--st-danger-fg); }
.mini-dot.tone-neutral { background: var(--st-neutral-bd); }
.mini-dot.tone-hatched { background: var(--st-unknown-fg); }
.git-letter { font-family: ui-monospace, Consolas, monospace; font-size: 10px; font-weight: 700; color: var(--st-warning-fg); }
.file-time { font-size: 9px; }
.loading { font-size: 10px; color: var(--text-2); }
.ws-skeleton { padding: 8px 10px; display: flex; flex-direction: column; gap: 8px; }
.skeleton-row {
  height: 12px;
  border-radius: 4px;
  background: linear-gradient(90deg, var(--surface-2) 25%, var(--border) 50%, var(--surface-2) 75%);
  background-size: 200% 100%;
  animation: shimmer 1.4s infinite linear;
}
@keyframes shimmer {
  from { background-position: 200% 0; }
  to { background-position: -200% 0; }
}
.ws-btn {
  height: 24px;
  padding: 0 8px;
  border: 1px solid var(--border);
  border-radius: var(--r-btn);
  background: var(--surface);
  color: var(--text);
  font-size: 11px;
  cursor: pointer;
}
.ws-btn:hover { background: var(--surface-2); }
.error { color: var(--st-danger-fg); }
@media (prefers-reduced-motion: reduce) {
  .skeleton-row { animation: none; }
}
</style>
