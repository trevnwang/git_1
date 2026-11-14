<script setup lang="ts">
import { computed } from 'vue'
import { useWorkbench } from '../../stores/workbench'
import { M2B_BOUNDARY_TEXT, REPO_LABEL, repoOf } from '../../viewmodel'
import type { RepoType, TreeNodeVm } from '../../types/workbench'
import EmptyState from '../common/EmptyState.vue'
import CiBadge from '../common/CiBadge.vue'
import StateChip from '../common/StateChip.vue'
import FileTree from './FileTree.vue'

const props = defineProps<{ repoType: RepoType }>()

const w = useWorkbench()
const state = w.state

const component = computed(() => w.selectedComponent())
const repo = computed(() => repoOf(component.value, props.repoType))
const panel = computed(() => state.trees[props.repoType])
const focused = computed(() => state.focusedRepoType === props.repoType)
const fallbackName = computed(() => `${component.value.id}_${REPO_LABEL[props.repoType]}`)
const displayName = computed(() => repo.value?.repoName || fallbackName.value)
const hasChanges = computed(() => (repo.value?.dirtyCount ?? 0) + (repo.value?.untrackedCount ?? 0) > 0)

const emptyState = computed(() => {
  const tree = panel.value
  // 错误态优先：error/errorCode 必须自成一态，绝不显示成“空目录”。
  if (tree.error || tree.status === 'error') {
    return {
      icon: '✕',
      title: '无法读取目录',
      desc: `${tree.errorCode ? `${tree.errorCode} · ` : ''}${tree.error || '未知错误'}`,
      primary: '重试',
      primaryKey: 'retry',
    }
  }
  if (!repo.value || repo.value.state === 'MISSING') {
    return {
      icon: '∅',
      title: '未克隆',
      desc: `${displayName.value} 尚未克隆到本地工作区。`,
      primary: 'Clone',
      primaryKey: 'clone',
    }
  }
  if (repo.value.state === 'EMPTY') {
    return {
      icon: '∅',
      title: '空仓库（无首个提交）',
      desc: '仓库已初始化，但还没有首个提交。',
      primary: '初始化并首推…',
      primaryKey: 'init',
    }
  }
  if (repo.value.state === 'INVALID') {
    return {
      icon: '✕',
      title: '仓库绑定异常',
      desc: tree.error || '路径或命名不符合绑定规则。',
      primary: '修复绑定…',
      primaryKey: 'repair',
    }
  }
  if (repo.value.state === 'UNKNOWN' || tree.status === 'unavailable') {
    return {
      icon: '⏱',
      title: '目录不可用',
      desc: tree.error || '状态未知：数据过期或扫描尚未完成，请重试。',
      primary: '重试',
      primaryKey: 'retry',
    }
  }
  return {
    icon: '∅',
    title: '空目录',
    desc: '该仓没有可显示的文件（.git / .trash 不显示）。',
    primary: '刷新',
    primaryKey: 'retry',
  }
})

function onEmptyAction(): void {
  const key = emptyState.value.primaryKey
  if (key === 'clone') {
    w.batchCommand('CLONE')
    return
  }
  if (key === 'retry') {
    void w.loadTree(props.repoType, true)
    return
  }
  w.notify(`${emptyState.value.primary}：${M2B_BOUNDARY_TEXT}`, 'warning', 3000)
}

function onTreeSelect(path: string | null): void {
  w.selectTreeNode(props.repoType, path)
}

function onTreeOpen(node: TreeNodeVm): void {
  void w.openNode(node)
}

function onTreeContextMenu(event: MouseEvent, node: TreeNodeVm): void {
  w.treeContextMenu(event, props.repoType, node.path, node.name)
}
</script>

<template>
  <section
    class="repo-tree-panel w-panel"
    :class="{ focused }"
    data-testid="repo-tree-panel"
    :data-repo-type="repoType"
    :aria-label="`${REPO_LABEL[repoType]} 仓库文件树`"
    @click="w.focusRepo(repoType)"
  >
    <header class="panel-title">
      <span class="state-dot" :class="`tone-${repo?.tone ?? 'neutral'}`" aria-hidden="true" />
      <span class="repo-type">{{ repoType }}</span>
      <span class="repo-name truncate" data-testid="repo-tree-title" :title="`${displayName}（${repo?.state ?? '未绑定'}）`">
        {{ displayName }}<span v-if="hasChanges" class="star">*</span>
      </span>
      <span v-if="panel.fallback" class="fallback" title="Host 未实现该方法，当前展示内置 Mock 文件树">Mock</span>
      <StateChip v-if="repo" :state="repo.state" compact />
      <CiBadge :state="repo?.ci ?? 'NONE'" :with-label="false" />
      <span class="spacer" />
      <button
        type="button"
        class="icon-btn"
        :aria-busy="panel.status === 'loading'"
        :title="`刷新 ${repoType} 目录`"
        :aria-label="`刷新 ${repoType} 目录`"
        @click.stop="w.loadTree(repoType, true)"
      >⟳</button>
    </header>

    <div class="panel-body">
      <template v-if="panel.status === 'loading' && panel.entries.length === 0">
        <div class="skeleton" aria-label="加载中">
          <div v-for="index in 8" :key="index" class="skeleton-row" :style="{ width: `${45 + ((index * 11) % 50)}%` }" />
        </div>
      </template>
      <template v-else-if="panel.status === 'unavailable' || (panel.status === 'ready' && panel.entries.length === 0)">
        <div data-testid="repo-tree-empty">
          <EmptyState :icon="emptyState.icon" :title="emptyState.title" :desc="emptyState.desc">
            <button type="button" class="empty-btn primary" @click.stop="onEmptyAction">{{ emptyState.primary }}</button>
            <button v-if="emptyState.primaryKey !== 'retry'" type="button" class="empty-btn" @click.stop="w.loadTree(repoType, true)">重试</button>
          </EmptyState>
        </div>
      </template>
      <template v-else-if="panel.status === 'error'">
        <div data-testid="repo-tree-empty">
          <EmptyState
            icon="✕"
            title="无法读取目录"
            :desc="`${panel.errorCode ? `${panel.errorCode} · ` : ''}${panel.error || '未知错误'}`"
          >
            <button type="button" class="empty-btn primary" @click.stop="w.loadTree(repoType, true)">重试</button>
          </EmptyState>
        </div>
      </template>
      <template v-else>
        <div v-if="panel.error" class="tree-error-banner" data-testid="tree-error-banner">
          {{ panel.errorCode }} · {{ panel.error }}
        </div>
        <FileTree
          :entries="panel.entries"
          :expanded="panel.expanded"
          :loading-paths="panel.loadingPaths"
          :selected-path="focused ? state.selectedPath : null"
          :repo-type="repoType"
          :initial-scroll-top="panel.scrollTop"
          @toggle="w.toggleTreePath(repoType, $event)"
          @select="onTreeSelect"
          @open="onTreeOpen"
          @focus-tree="w.focusRepo(repoType)"
          @contextmenu="onTreeContextMenu"
          @scroll="w.setTreeScroll(repoType, $event)"
        />
        <div v-if="panel.truncated" class="truncated" data-testid="tree-truncated" title="目录条目超过 Host 上限，已截断">
          已截断：仅显示前 {{ panel.entries.length }} 项<template v-if="panel.totalEntries > panel.entries.length">（共 {{ panel.totalEntries }} 项，打开文件夹查看全部）</template>
        </div>
      </template>
    </div>
  </section>
</template>

<style scoped>
.repo-tree-panel {
  min-width: 0;
  min-height: 0;
  border-top: 2px solid transparent;
}
.repo-tree-panel.focused { border-top-color: var(--focus); }
.panel-title {
  flex: 0 0 var(--tree-head-h);
  display: flex;
  align-items: center;
  gap: 5px;
  padding: 0 6px 0 8px;
  border-bottom: 1px solid var(--border);
  background: var(--surface-2);
  white-space: nowrap;
  overflow: hidden;
}
.focused .panel-title { border-bottom-color: var(--focus); }
.state-dot { width: 8px; height: 8px; border-radius: 50%; flex: 0 0 8px; background: var(--rail); }
.state-dot.tone-success { background: var(--st-success-fg); }
.state-dot.tone-warning { background: var(--st-warning-fg); }
.state-dot.tone-info { background: var(--st-info-fg); }
.state-dot.tone-purple { background: var(--st-purple-fg); }
.state-dot.tone-cyan { background: var(--st-cyan-fg); }
.state-dot.tone-danger { background: var(--st-danger-fg); }
.state-dot.tone-neutral { background: var(--st-neutral-bd); }
.state-dot.tone-hatched { background: var(--st-unknown-fg); }
.repo-type { font-size: 11px; font-weight: 700; letter-spacing: 0.2px; flex: 0 0 auto; }
.repo-name { font-size: 12px; flex: 1; min-width: 0; }
.star { color: var(--st-warning-fg); font-weight: 700; }
.fallback {
  flex: 0 0 auto;
  font-size: 9px;
  color: var(--st-warning-fg);
  border: 1px dashed var(--st-warning-bd);
  border-radius: 4px;
  padding: 0 3px;
}
.spacer { flex: 1; }
.panel-body { flex: 1; min-height: 0; display: flex; flex-direction: column; overflow: hidden; }
.skeleton { padding: 8px 10px; display: flex; flex-direction: column; gap: 8px; }
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
.truncated {
  flex: 0 0 28px;
  display: flex;
  align-items: center;
  padding: 0 10px;
  border-top: 1px solid var(--st-warning-bd);
  background: var(--st-warning-bg);
  color: var(--st-warning-fg);
  font-size: 11px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.tree-error-banner {
  flex: 0 0 auto;
  padding: 4px 10px;
  border-bottom: 1px solid var(--st-danger-bd);
  background: var(--st-danger-bg);
  color: var(--st-danger-fg);
  font-size: 10px;
  line-height: 1.5;
  overflow-wrap: anywhere;
}
.empty-btn {
  height: 24px;
  padding: 0 8px;
  border: 1px solid var(--border);
  border-radius: var(--r-btn);
  background: var(--surface);
  color: var(--text);
  font-size: 11px;
  cursor: pointer;
}
.empty-btn.primary { border-color: var(--st-info-bd); background: var(--st-info-bg); color: var(--st-info-fg); }
@media (prefers-reduced-motion: reduce) {
  .skeleton-row { animation: none; }
}
</style>
