<script setup lang="ts">
import { computed } from 'vue'
import { useWorkbench } from '../../stores/workbench'
import {
  fileExtension,
  formatAgo,
  formatBytes,
  formatStamp,
  isSafeFile,
  REPO_LABEL,
  shortSha,
} from '../../viewmodel'
import type { InfoTab, WorkspaceNodeVm } from '../../types/workbench'
import CiBadge from '../common/CiBadge.vue'
import EmptyState from '../common/EmptyState.vue'
import StateChip from '../common/StateChip.vue'

const w = useWorkbench()
const state = w.state

const component = computed(() => w.selectedComponent())
const repo = computed(() => state.detail?.repo ?? w.focusedRepo())
const displayName = computed(() => repo.value?.repoName || (component.value.id ? `${component.value.id}_${REPO_LABEL[state.focusedRepoType]}` : '未选择仓库'))
const hasChanges = computed(() => (repo.value?.dirtyCount ?? 0) + (repo.value?.untrackedCount ?? 0) > 0)
const commitsPreview = computed(() => state.detail?.commits.slice(0, 5) ?? [])
const commitTotal = computed(() => state.detail?.commitTotal ?? 0)

const ciCounts = computed(() => {
  const counts: Record<string, number> = { PASS: 0, FAIL: 0, RUNNING: 0, QUEUED: 0, NONE: 0, UNKNOWN: 0 }
  for (const item of component.value.repos) counts[item.ci] = (counts[item.ci] ?? 0) + 1
  return counts
})

const fileNode = computed<WorkspaceNodeVm | null>(() => {
  const node = state.folderSelected
  if (!node || node.kind !== 'FILE') return null
  return node
})
const fileRepo = computed(() => {
  if (!fileNode.value?.path) return repo.value
  const segment = fileNode.value.path.split('/')[0]
  for (const item of component.value.repos) {
    if (segment === item.repoName) return item
  }
  return repo.value
})

const tabs: { key: InfoTab; label: string }[] = [
  { key: 'overview', label: '概览' },
  { key: 'commits', label: '提交' },
  { key: 'diff', label: '差异' },
  { key: 'ci', label: 'CI' },
]

function copy(text: string | null | undefined, label: string): void {
  if (!text) return
  void w.copyText(text, label)
}

function openCurrent(): void {
  w.openInExplorer()
}

function openComponentDetail(): void {
  if (component.value.id) w.openComponentDrawer(component.value.id)
}

function openFile(): void {
  const node = fileNode.value
  if (node) void w.openFolderNode(node)
}
</script>

<template>
  <aside class="info-panel w-panel" data-testid="info-panel" aria-label="选中仓库信息面板">
    <div
      class="ci-strip"
      data-testid="ci-strip"
      title="CI 汇总：独立通道，v1 未接入（不参与组件聚合与卡片底色）"
    >
      <span class="ci-label">CI 汇总</span>
      <span class="ci-counts" aria-hidden="true">
        <span class="ci-count"><span class="ci-dot pass" />{{ ciCounts.PASS }}</span>
        <span class="ci-count"><span class="ci-dot fail" />{{ ciCounts.FAIL }}</span>
        <span class="ci-count"><span class="ci-dot running" />{{ ciCounts.RUNNING }}</span>
        <span class="ci-count"><span class="ci-dot queued" />{{ ciCounts.QUEUED }}</span>
      </span>
      <span class="ci-hint">未接入</span>
    </div>

    <div class="repo-head">
      <span v-if="repo" class="state-dot" :class="`tone-${repo.tone}`" aria-hidden="true" />
      <span class="repo-name truncate" data-testid="info-repo-title" :title="displayName">
        {{ displayName }}<span v-if="hasChanges" class="star">*</span>
      </span>
      <StateChip v-if="repo" :state="repo.state" compact />
      <button
        type="button"
        class="icon-btn"
        title="查看组件详情（抽屉）"
        aria-label="查看组件详情"
        data-testid="info-detail"
        @click="openComponentDetail"
      >ⓘ</button>
      <button
        type="button"
        class="icon-btn"
        title="在资源管理器中打开"
        aria-label="在资源管理器中打开"
        @click="openCurrent"
      >↗</button>
    </div>

    <div v-if="!fileNode" class="tabs" role="tablist" aria-label="仓库详情 Tab">
      <button
        v-for="tab in tabs"
        :key="tab.key"
        type="button"
        role="tab"
        class="tab"
        data-testid="info-tab"
        :data-tab="tab.key"
        :data-ci-tab="tab.key === 'ci' ? '' : undefined"
        :aria-selected="state.tab === tab.key"
        :class="{ active: state.tab === tab.key }"
        @click="w.setTab(tab.key)"
      >
        <span v-if="tab.key === 'ci'" data-testid="ci-tab" class="ci-tab-label">CI</span>
        <template v-else>{{ tab.label }}</template>
      </button>
    </div>

    <!-- 文件信息（文件夹视图） -->
    <div v-if="fileNode" class="info-body" data-mode="file">
      <div class="section-title">文件信息</div>
      <div class="field"><span class="field-label">文件名</span><span class="field-value truncate" :title="fileNode.label">{{ fileNode.label }}</span></div>
      <div class="field"><span class="field-label">相对路径</span><span class="field-value mono truncate" :title="fileNode.path">{{ fileNode.path }}</span></div>
      <div class="field"><span class="field-label">扩展名</span><span class="field-value">{{ fileExtension(fileNode.label) || '（无）' }}</span></div>
      <div class="field"><span class="field-label">大小</span><span class="field-value">{{ formatBytes(fileNode.size) }}</span></div>
      <div class="field"><span class="field-label">修改时间</span><span class="field-value">{{ formatStamp(fileNode.mtime) }}</span></div>
      <div class="field"><span class="field-label">Git 状态</span><span class="field-value">{{ fileNode.gitStatus }}</span></div>
      <div class="field"><span class="field-label">打开白名单</span><span class="field-value">{{ isSafeFile(fileNode.label) ? '是（可直接打开）' : '否（需二次确认）' }}</span></div>
      <div class="field"><span class="field-label">所属仓</span><span class="field-value truncate">{{ fileRepo?.repoName ?? '—' }}</span></div>
      <div class="file-actions">
        <button type="button" class="action-btn primary" @click="openFile">用默认程序打开</button>
        <button type="button" class="action-btn" @click="openCurrent">打开所在目录</button>
        <button type="button" class="action-btn" @click="copy(fileNode.path, '路径')">复制路径</button>
      </div>
    </div>

    <!-- 未选择仓 -->
    <div v-else-if="!repo" class="info-body">
      <EmptyState
        icon="⇢"
        title="未选择仓库"
        desc="从中间文件树中选择仓库，或在上方切换 Tab 查看详情。"
      >
        <button type="button" class="action-btn" @click="w.selectComponent(component.id)">重试</button>
      </EmptyState>
    </div>

    <!-- 仓库详情 -->
    <div v-else class="info-body" :data-mode="state.tab">
      <template v-if="state.tab === 'overview'">
        <div class="section-title">基本信息</div>
        <div class="field" data-testid="info-branch">
          <span class="field-label">分支</span>
          <span class="field-value mono truncate" :title="repo.branch">{{ repo.branch }}</span>
          <button type="button" class="copy-btn" title="复制分支" @click="copy(repo.branch, '分支')">⧉</button>
        </div>
        <div v-if="repo.tag" class="field">
          <span class="field-label">Tag</span>
          <span class="field-value mono truncate" :title="repo.tag">{{ repo.tag }}</span>
        </div>
        <div class="field" data-testid="info-head">
          <span class="field-label">HEAD</span>
          <span class="field-value mono truncate" :title="repo.headSha ?? '—'">{{ shortSha(repo.headSha) }}</span>
          <button type="button" class="copy-btn" title="复制完整 SHA" @click="copy(repo.headSha, 'HEAD')">⧉</button>
        </div>
        <div class="field">
          <span class="field-label">对比基准</span>
          <span class="field-value mono truncate" :title="repo.compareRef ?? '未配置 upstream'">{{ repo.compareRef ?? '未配置 upstream' }}</span>
        </div>
        <div class="field" data-testid="info-ahead-behind">
          <span class="field-label">超前/落后</span>
          <span class="field-value">↑ {{ repo.ahead }} / ↓ {{ repo.behind }}<span class="muted">（相对 compare_ref）</span></span>
        </div>
        <div class="field" data-testid="info-dirty">
          <span class="field-label">未提交</span>
          <span class="field-value">
            已暂存 {{ repo.stagedCount }} · 已跟踪 {{ repo.dirtyCount }} · 未跟踪 {{ repo.untrackedCount }} · stash {{ repo.stashCount }}
          </span>
        </div>
        <div class="field" data-testid="info-last-op">
          <span class="field-label">最近操作</span>
          <span class="field-value truncate" :title="w.lastOpText()">{{ w.lastOpText() }}</span>
        </div>
        <div class="field">
          <span class="field-label">状态</span>
          <span class="field-value"><StateChip :state="repo.state" /> <span v-if="repo.isStale" class="muted">⏱ 数据过期</span></span>
        </div>
        <div v-if="repo.flags.length > 0" class="field">
          <span class="field-label">标记</span>
          <span class="field-value flags"><span v-for="flag in repo.flags" :key="flag" class="flag">{{ flag }}</span></span>
        </div>
        <div class="field">
          <span class="field-label">本地路径</span>
          <span class="field-value mono truncate" :title="repo.localPath ?? '—'">{{ repo.localPath ?? '（MISSING）' }}</span>
          <button type="button" class="copy-btn" title="复制路径" @click="copy(repo.localPath, '路径')">⧉</button>
        </div>
        <div class="field">
          <span class="field-label">状态计算</span>
          <span class="field-value muted">{{ formatStamp(repo.calcAt) }} · {{ repo.dataSource ?? 'local' }}</span>
        </div>

        <div class="section-title">构建 / CI</div>
        <div class="field" data-testid="info-ci">
          <span class="field-label">CI 状态</span>
          <span class="field-value"><CiBadge :state="repo.ci" /> <span class="muted">（v1 占位）</span></span>
        </div>
        <div class="field">
          <span class="field-label">本地构建</span>
          <span class="field-value muted">未接入</span>
        </div>

        <div class="section-title">
          最近提交
          <button type="button" class="link-btn" @click="w.setTab('commits')">查看全部 →</button>
        </div>
        <div class="commit-mini" data-testid="info-last-commit">
          <div v-if="commitsPreview.length === 0" class="muted small">该仓暂无提交（或未克隆）。</div>
          <div v-for="commit in commitsPreview" :key="commit.sha" class="commit-row">
            <span class="commit-sha mono" :title="commit.sha">{{ commit.shortSha }}</span>
            <span class="commit-msg truncate" :title="commit.message">{{ commit.message }}</span>
            <span class="commit-meta">{{ commit.author }} · {{ formatAgo(commit.at, state.now) }}</span>
          </div>
          <div v-if="commitTotal > 0" class="muted small">共 {{ commitTotal }} 个提交</div>
        </div>
      </template>

      <template v-else-if="state.tab === 'commits'">
        <div class="section-title">提交历史（{{ state.commits.total || commitTotal }}）</div>
        <div v-if="state.commits.loading && state.commits.items.length === 0" class="muted small">加载中…</div>
        <div v-else-if="state.commits.error" class="muted small">读取失败：{{ state.commits.error }}</div>
        <div v-else class="commit-list">
          <div v-for="commit in state.commits.items" :key="commit.sha" class="commit-row">
            <span class="commit-sha mono" :title="commit.sha" @click="copy(commit.sha, 'SHA')">{{ commit.shortSha }}</span>
            <span class="commit-msg truncate" :title="commit.message">{{ commit.message }}</span>
            <span class="commit-meta">{{ commit.author }} · {{ formatAgo(commit.at, state.now) }}</span>
          </div>
          <div v-if="state.commits.items.length < state.commits.total" class="load-more">
            <button type="button" class="action-btn" :disabled="state.commits.loading" @click="w.loadCommits(false)">
              {{ state.commits.loading ? '加载中…' : '加载更多' }}
            </button>
          </div>
          <div v-if="state.commits.items.length === 0" class="muted small">暂无提交记录。</div>
        </div>
      </template>

      <template v-else-if="state.tab === 'diff'">
        <EmptyState icon="±" title="差异只读视图将在后续版本接入" desc="契约字段占位：files / chunks / addition / deletion；本期不展示裸 TODO。" />
      </template>

      <template v-else>
        <div class="section-title">CI 详情（v1 占位）</div>
        <div class="muted small">CI 未接入（v1 占位）· feature flag: ci.enabled = false</div>
        <div class="contract-table" aria-label="CiLatest 字段占位">
          <div v-for="field in ['repoId', 'componentId', 'repoType', 'branch', 'commitHash', 'state', 'failedStage', 'durationMs', 'url', 'provider', 'fetchedAt']" :key="field" class="contract-row">
            <span class="contract-key mono">{{ field }}</span>
            <span class="contract-value muted">—</span>
          </div>
        </div>
        <div class="muted small">CI 独立通道：永不参与组件聚合、卡片底色与严重度排序（CI-10）。</div>
      </template>
    </div>
  </aside>
</template>

<style scoped>
.info-panel { min-width: 0; min-height: 0; }
.ci-strip {
  flex: 0 0 40px;
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 0 8px;
  border-bottom: 1px solid var(--border);
  background: var(--surface-2);
  font-size: 11px;
  white-space: nowrap;
  overflow: hidden;
}
.ci-label { font-weight: 600; }
.ci-counts { display: flex; gap: 5px; }
.ci-count { display: inline-flex; align-items: center; gap: 2px; color: var(--text-2); font-variant-numeric: tabular-nums; }
.ci-dot { width: 7px; height: 7px; border-radius: 50%; border: 1px solid var(--st-neutral-bd); }
.ci-dot.pass { background: var(--ci-pass); border-color: var(--ci-pass); }
.ci-dot.fail { background: var(--ci-fail); border-color: var(--ci-fail); }
.ci-dot.running { background: var(--ci-running); border-color: var(--ci-running); }
.ci-dot.queued { background: transparent; }
.ci-hint { margin-left: auto; color: var(--text-2); }
.repo-head {
  flex: 0 0 36px;
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 0 6px 0 8px;
  border-bottom: 1px solid var(--border);
  white-space: nowrap;
  overflow: hidden;
}
.state-dot { width: 8px; height: 8px; border-radius: 50%; flex: 0 0 8px; background: var(--rail); }
.state-dot.tone-success { background: var(--st-success-fg); }
.state-dot.tone-warning { background: var(--st-warning-fg); }
.state-dot.tone-info { background: var(--st-info-fg); }
.state-dot.tone-purple { background: var(--st-purple-fg); }
.state-dot.tone-cyan { background: var(--st-cyan-fg); }
.state-dot.tone-danger { background: var(--st-danger-fg); }
.state-dot.tone-neutral { background: var(--st-neutral-bd); }
.state-dot.tone-hatched { background: var(--st-unknown-fg); }
.repo-name { font-size: 12px; font-weight: 600; flex: 1; min-width: 0; }
.star { color: var(--st-warning-fg); }
.tabs {
  margin: 0;
  flex: 0 0 32px;
  display: flex;
  gap: 2px;
  padding: 0 6px;
  border-bottom: 1px solid var(--border);
  background: var(--surface);
  align-items: center;
}
.tab {
  flex: 1 1 0;
  height: 24px;
  border: 1px solid transparent;
  border-radius: 4px;
  background: transparent;
  color: var(--text-2);
  font-size: 11px;
  cursor: pointer;
  white-space: nowrap;
  overflow: hidden;
}
.tab:hover { background: var(--surface-2); color: var(--text); }
.tab.active { background: var(--surface-2); border-color: var(--border); color: var(--text); font-weight: 600; }
.ci-tab-label { display: block; }
.info-body {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  overflow-x: hidden;
  overscroll-behavior: contain;
  padding: 8px 10px 14px;
  scrollbar-gutter: stable;
}
.section-title {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 6px;
  margin: 8px 0 6px;
  font-size: 11px;
  font-weight: 650;
  color: var(--text-2);
  text-transform: uppercase;
  letter-spacing: 0.3px;
}
.section-title:first-child { margin-top: 2px; }
.field {
  display: grid;
  grid-template-columns: minmax(52px, 64px) minmax(0, 1fr) auto;
  gap: 6px;
  align-items: center;
  min-height: 26px;
  font-size: 11px;
  line-height: 1.4;
}
.field-label { color: var(--text-2); }
.field-value { min-width: 0; overflow-wrap: anywhere; }
.copy-btn, .link-btn {
  border: 0;
  background: transparent;
  color: var(--text-2);
  cursor: pointer;
  font-size: 11px;
  padding: 0 2px;
}
.copy-btn:hover, .link-btn:hover { color: var(--focus); }
.link-btn { font-size: 10px; text-transform: none; letter-spacing: 0; }
.flags { display: flex; flex-wrap: wrap; gap: 3px; }
.flag {
  padding: 0 4px;
  border: 1px solid var(--border);
  border-radius: 4px;
  font-size: 10px;
  color: var(--text-2);
  background: var(--surface-2);
}
.commit-mini, .commit-list { display: flex; flex-direction: column; gap: 2px; }
.commit-row {
  display: grid;
  grid-template-columns: 52px minmax(0, 1fr);
  column-gap: 6px;
  padding: 3px 0;
  border-bottom: 1px dashed var(--border);
  font-size: 11px;
}
.commit-row:last-child { border-bottom: 0; }
.commit-sha { color: var(--focus); cursor: pointer; }
.commit-msg { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.commit-meta { grid-column: 2; color: var(--text-2); font-size: 10px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.muted { color: var(--text-2); }
.small { font-size: 10px; }
.load-more { display: flex; justify-content: center; padding: 6px 0; }
.file-actions { display: flex; flex-wrap: wrap; gap: 6px; margin-top: 10px; }
.action-btn {
  height: 26px;
  padding: 0 8px;
  border: 1px solid var(--border);
  border-radius: var(--r-btn);
  background: var(--surface);
  color: var(--text);
  font-size: 11px;
  cursor: pointer;
}
.action-btn:hover { background: var(--surface-2); }
.action-btn.primary { border-color: var(--st-info-bd); background: var(--st-info-bg); color: var(--st-info-fg); }
.contract-table { margin: 8px 0; border: 1px solid var(--border); border-radius: var(--r-panel); overflow: hidden; }
.contract-row { display: grid; grid-template-columns: 120px minmax(0, 1fr); border-bottom: 1px solid var(--border); }
.contract-row:last-child { border-bottom: 0; }
.contract-key { padding: 4px 8px; background: var(--surface-2); font-size: 10px; color: var(--text-2); }
.contract-value { padding: 4px 8px; font-size: 10px; }
@media (max-width: 1279px) {
  .field { grid-template-columns: 52px minmax(0, 1fr) auto; }
}
</style>

