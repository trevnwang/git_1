<script setup lang="ts">
import { computed, ref } from 'vue'
import { useWorkbench } from '../../stores/workbench'

const w = useWorkbench()
const state = w.state
const composing = ref(false)

const sourceTone = computed(() => {
  if (!state.hostLinked || state.mockFallback || state.source === 'mock') return 'src-mock'
  return state.source === 'local+gitea' ? 'src-gitea' : 'src-local'
})

const scanning = computed(() => state.booting || state.refreshing)
const componentCount = computed(() => state.counts.components.toLocaleString('en-US'))
const repoCount = computed(() => state.counts.repos.toLocaleString('en-US'))

function onInput(event: Event): void {
  if (composing.value) return
  w.setSearch((event.target as HTMLInputElement).value)
}

function onCompositionEnd(event: CompositionEvent): void {
  composing.value = false
  w.setSearch((event.target as HTMLInputElement).value)
}

function clearSearch(): void {
  w.setSearch('')
}
</script>

<template>
  <header class="title-bar" data-testid="titlebar">
    <span class="brand" data-testid="title-product">MBD Git Tool</span>
    <span class="pill version" data-testid="title-version">v1.0.0</span>
    <span class="meta org" :title="'组织 ' + (state.owner || '未配置')">{{ state.owner || '未配置' }}</span>
    <span class="meta scope" data-testid="title-scope" :title="`${componentCount} 组件 / ${repoCount} 仓`">
      {{ componentCount }} 组件 / {{ repoCount }} 仓
    </span>
    <span
      class="pill"
      :class="sourceTone"
      data-testid="snapshot-source"
      :title="w.sourceTitle()"
    >{{ w.sourceLabel() }}</span>
    <span v-if="scanning" class="pill src-local">扫描中…</span>
    <span v-if="state.workspaceRoot" class="meta ws truncate" :title="`工作区：${state.workspaceRoot}`">
      工作区 {{ state.workspaceRoot }}
    </span>
    <span v-if="state.mockFallback && state.hostFailed" class="pill src-warn" :title="state.fallbackReason">Mock 回退</span>
    <span v-if="state.snapshotError && state.source !== 'mock'" class="pill src-warn" :title="state.snapshotError">快照告警</span>
    <!-- M2b-2 离线提示（轻量）：仅当快照 source=local 且存在远程错误/离线标记；不伪造在线 -->
    <span
      v-if="state.remoteOffline.offline"
      class="pill offline-pill"
      data-testid="offline-banner"
      :title="`离线 / 远程不可达：${state.remoteOffline.reason} · FETCH 已禁用`"
    >⏻ 离线/远程不可达</span>

    <span class="spacer" />

    <label class="search">
      <span class="search-icon" aria-hidden="true">🔍</span>
      <input
        type="search"
        data-testid="title-search"
        :value="state.search"
        placeholder="搜索组件 / 仓库 / 分支 / Tag（Ctrl+K）"
        aria-label="搜索组件 / 仓库 / 分支 / Tag"
        @input="onInput"
        @compositionstart="composing = true"
        @compositionend="onCompositionEnd"
        @keydown.esc="clearSearch"
      />
    </label>

    <div class="seg" data-testid="view-switch" role="tablist" aria-label="视图切换">
      <button
        type="button"
        role="tab"
        data-testid="view-workbench"
        :aria-selected="state.view === 'workbench'"
        :class="{ active: state.view === 'workbench' }"
        @click="w.setView('workbench')"
      >工作台</button>
      <button
        type="button"
        role="tab"
        data-testid="view-folder"
        :aria-selected="state.view === 'folder'"
        :class="{ active: state.view === 'folder' }"
        @click="w.setView('folder')"
      >文件夹</button>
    </div>

    <button
      type="button"
      class="icon-btn ci-entry"
      data-testid="ci-nav"
      title="CI 流水线（CI-1：菜单栏 工具 → CI 流水线 / 标题区入口）"
      aria-label="打开 CI 流水线"
      @click="w.openModal('ci')"
    >CI</button>

    <button
      type="button"
      class="icon-btn"
      data-testid="title-refresh"
      :aria-busy="scanning"
      :title="scanning ? '正在扫描本地仓库…' : (state.hostLinked ? '从 Host 重新获取快照（大工作区约 10–20s）' : '重新生成 Mock 快照')"
      aria-label="刷新"
      @click="w.refresh()"
    >{{ scanning ? '◌' : '⟳' }}</button>

    <button
      type="button"
      class="icon-btn has-badge"
      data-testid="title-notifications"
      :title="`通知中心（${w.unread()} 条未读）`"
      aria-label="通知中心"
      @click="w.openDrawer('notifications')"
    >✉<span v-if="w.unread() > 0" class="badge">{{ w.unread() }}</span></button>

    <button
      type="button"
      class="icon-btn"
      data-testid="title-tasks"
      :title="`任务中心（进行中 ${state.taskCount} · 历史 ${state.jobs.length - state.taskCount}）`"
      aria-label="任务中心"
      @click="w.openTaskCenter()"
    >▤</button>

    <button
      type="button"
      class="icon-btn"
      data-testid="title-settings"
      title="设置（Ctrl+,）"
      aria-label="设置"
      @click="w.openModal('settings')"
    >⚙</button>
  </header>
</template>

<style scoped>
.title-bar {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 0 12px;
  height: var(--layout-title-h);
  background: var(--surface);
  border-bottom: 1px solid var(--border);
  white-space: nowrap;
  overflow: hidden;
  position: relative;
  z-index: var(--z-sticky);
}
.brand { font-size: 14px; font-weight: 650; letter-spacing: 0.2px; }
.pill {
  border: 1px solid var(--border);
  border-radius: 999px;
  padding: 2px 8px;
  font-size: 11px;
  color: var(--text-2);
  white-space: nowrap;
}
.pill.version { border-color: var(--st-info-bd); color: var(--st-info-fg); background: var(--st-info-bg); }
.src-mock { color: var(--st-neutral-fg); background: var(--st-neutral-bg); border-color: var(--st-neutral-bd); }
.src-local { color: var(--st-info-fg); background: var(--st-info-bg); border-color: var(--st-info-bd); }
.src-gitea { color: var(--st-success-fg); background: var(--st-success-bg); border-color: var(--st-success-bd); }
.src-warn { color: var(--st-warning-fg); background: var(--st-warning-bg); border-color: var(--st-warning-bd); }
.offline-pill {
  color: var(--st-warning-fg);
  background: var(--st-warning-bg);
  border-color: var(--st-warning-bd);
  font-weight: 600;
}
.meta { color: var(--text-2); font-size: 12px; white-space: nowrap; }
.scope { font-variant-numeric: tabular-nums; }
.org { max-width: 120px; overflow: hidden; text-overflow: ellipsis; }
.ws { max-width: 210px; font-family: ui-monospace, Consolas, monospace; }
.spacer { flex: 1; min-width: 8px; }
.search {
  display: flex;
  align-items: center;
  gap: 6px;
  width: clamp(200px, 22vw, 320px);
  height: var(--field-h);
  padding: 0 8px;
  border: 1px solid var(--border);
  border-radius: var(--r-btn);
  background: var(--surface);
}
.search:focus-within { border-color: var(--focus); box-shadow: 0 0 0 2px rgb(37 99 235 / 20%); }
.search-icon { font-size: 11px; opacity: 0.7; }
.search input {
  flex: 1;
  min-width: 0;
  height: 100%;
  border: 0;
  background: transparent;
  color: var(--text);
  font: inherit;
  outline: none;
}
.search input::placeholder { color: var(--text-2); }
.seg { display: inline-flex; border: 1px solid var(--border); border-radius: var(--r-btn); overflow: hidden; height: var(--field-h); }
.seg button {
  border: 0;
  padding: 0 10px;
  background: transparent;
  color: var(--text-2);
  font-size: 12px;
  cursor: pointer;
}
.seg button.active { background: var(--surface-2); color: var(--text); font-weight: 600; }
.icon-btn { width: 28px; height: 28px; font-size: 13px; }
.ci-entry { font-size: 10px; font-weight: 700; letter-spacing: 0.2px; }
.has-badge { position: relative; }
.badge {
  position: absolute;
  top: -3px;
  right: -3px;
  min-width: 14px;
  height: 14px;
  padding: 0 3px;
  border-radius: 7px;
  background: var(--st-info-fg);
  color: #fff;
  font-size: 9px;
  display: grid;
  place-items: center;
}
@media (max-width: 1279px) {
  .org, .scope, .ws { display: none; }
}
@media (max-width: 1099px) {
  .pill.version { display: none; }
  .search { width: 180px; }
}
</style>
