<script setup lang="ts">
/**
 * 任务中心 / 通知中心抽屉（D6 §7.5）。
 * 任务中心（M2b）：进行中/历史分段、逐 job 进度 `ok/total`、取消、逐仓 items
 * （状态/错误码/backupRef/preHead→postHead）、“仅重试失败项”、从备份 ref 恢复。
 * 数据来自 store：JOB_UPDATED 事件驱动 + getJobs 轮询兜底（Mock 模式为内置演示作业）。
 */
import { computed } from 'vue'
import { useWorkbench } from '../../stores/workbench'
import {
  REPO_LABEL,
  formatAgo,
  formatClock,
  formatDurationMs,
  jobItemStateMeta,
  jobKindLabel,
  jobProgressPercent,
  jobRetryableCount,
  jobStateMeta,
  shortSha,
} from '../../viewmodel'
import type { JobVm } from '../../types/workbench'
import EmptyState from '../common/EmptyState.vue'

const w = useWorkbench()
const state = w.state

const kind = computed(() => state.overlays.drawer)
const title = computed(() => (kind.value === 'tasks' ? '任务中心' : '通知中心'))

const ciNotifications = [
  { type: 'CI 失败', detail: '构建失败 / 失败阶段 / 构建号 / 链接（v1 空分组）' },
  { type: 'CI 通过', detail: '构建成功（v1 空分组）' },
  { type: 'CI 超时', detail: '构建超时（v1 空分组）' },
]

// ───────────────────────────── 任务中心 ─────────────────────────────

const activeJobs = computed(() => w.activeJobs())
const historyJobs = computed(() => w.historyJobs())
const shownJobs = computed<JobVm[]>(() => (state.tasksSegment === 'active' ? activeJobs.value : historyJobs.value))
const jobsSummary = computed(
  () => `进行中 ${activeJobs.value.length} · 历史 ${historyJobs.value.length} · 当前队列 ${state.taskCount}`,
)
const jobsSourceLabel = computed(() => (state.jobsSource === 'mock' ? 'Mock 作业' : 'Host 作业'))
const jobsUpdatedText = computed(() =>
  state.jobsAt > 0 ? `更新于 ${formatAgo(new Date(state.jobsAt).toISOString(), state.now)}` : '尚未加载',
)

function repoLabelOf(repoType: string): string {
  return ((REPO_LABEL as Record<string, string>)[repoType] ?? repoType) || '—'
}

function isExpanded(jobId: string): boolean {
  return state.expandedJobIds.includes(jobId)
}

function isCurrent(jobId: string): boolean {
  return state.currentJobId === jobId
}

function jobTimeText(job: JobVm): string {
  if (job.active) {
    return `开始 ${formatClock(job.startedAt ?? job.createdAt)} · 已运行 ${formatDurationMs(job.startedAt ?? job.createdAt, null)}`
  }
  const finished = job.finishedAt ?? job.createdAt
  return `结束 ${formatClock(finished)} · ${formatAgo(finished, state.now)} · 耗时 ${formatDurationMs(job.startedAt ?? job.createdAt, job.finishedAt)}`
}

function retryableCount(job: JobVm): number {
  return jobRetryableCount(job)
}

function onCancel(job: JobVm): void {
  void w.cancelJob(job.id)
}

function onRetry(job: JobVm): void {
  void w.retryFailed(job.id)
}
</script>

<template>
  <div v-if="kind" class="overlay" @click.self="w.closeDrawer()">
    <aside
      class="drawer"
      :class="{ 'drawer-wide': kind === 'tasks' }"
      data-testid="drawer"
      role="dialog"
      aria-modal="true"
      :aria-label="title"
    >
      <header class="drawer-head">
        <span class="title">{{ title }}</span>
        <span v-if="kind === 'tasks'" class="source-pill" data-testid="task-source">{{ jobsSourceLabel }}</span>
        <button type="button" class="icon-btn" autofocus title="关闭（Esc）" aria-label="关闭" @click="w.closeDrawer()">✕</button>
      </header>

      <!-- 任务中心（M2b） -->
      <div v-if="kind === 'tasks'" class="drawer-body" data-testid="task-center">
        <div class="task-toolbar">
          <span class="summary" data-testid="task-summary">{{ jobsSummary }}</span>
          <span class="spacer" />
          <button
            type="button"
            class="drawer-btn"
            data-testid="task-refresh"
            :aria-busy="state.jobsLoading"
            title="刷新任务列表（进行中作业自动轮询）"
            @click="w.refreshJobs(true)"
          >{{ state.jobsLoading ? '刷新中…' : '刷新' }}</button>
        </div>
        <div class="task-meta-row">
          <span class="task-updated" data-testid="task-updated">{{ jobsUpdatedText }}</span>
          <span class="task-source-note">{{ state.jobsSource === 'mock' ? 'Mock 演示作业：批量提交会生成新作业' : 'JOB_UPDATED 事件驱动 + 轮询兜底' }}</span>
        </div>

        <div class="task-tabs" role="tablist" aria-label="任务分段">
          <button
            type="button"
            role="tab"
            class="task-tab"
            :class="{ active: state.tasksSegment === 'active' }"
            :aria-selected="state.tasksSegment === 'active'"
            data-testid="task-segment-active"
            @click="w.setTasksSegment('active')"
          >进行中 <span class="tab-count">{{ activeJobs.length }}</span></button>
          <button
            type="button"
            role="tab"
            class="task-tab"
            :class="{ active: state.tasksSegment === 'history' }"
            :aria-selected="state.tasksSegment === 'history'"
            data-testid="task-segment-history"
            @click="w.setTasksSegment('history')"
          >历史 <span class="tab-count">{{ historyJobs.length }}</span></button>
        </div>

        <div v-if="state.jobsStatus === 'loading' && state.jobs.length === 0" class="task-loading" data-testid="task-loading">
          正在读取任务列表…
        </div>
        <div v-else-if="state.jobsStatus === 'error'" class="task-error" data-testid="task-error" role="alert">
          <div class="task-error-title">任务接口不可用</div>
          <div class="task-error-detail">{{ state.jobsError }}</div>
          <button type="button" class="drawer-btn" @click="w.refreshJobs(true)">重试</button>
        </div>
        <EmptyState
          v-else-if="shownJobs.length === 0"
          icon="◷"
          :title="state.tasksSegment === 'active' ? '暂无进行中的任务' : '暂无历史任务'"
          :desc="state.tasksSegment === 'active'
            ? '批量 SYNC / CREATE / Clone / Delete 提交后会在此显示逐仓进度、取消与失败重试。'
            : '已完成的批量作业会保留在此，可展开逐仓结果或仅重试失败项。'"
          data-testid="task-empty"
        />

        <ul v-else class="job-list" role="list" data-testid="task-list">
          <li
            v-for="job in shownJobs"
            :key="job.id"
            class="job-card"
            :class="{ current: isCurrent(job.id), active: job.active }"
            data-testid="task-job"
            :data-job-id="job.id"
            :data-job-kind="job.kind"
            :data-state="job.state"
            :data-active="job.active ? '1' : '0'"
            @click="w.selectJob(job.id)"
          >
            <div class="job-head">
              <span class="job-kind" data-testid="task-kind">{{ jobKindLabel(job.kind) }}</span>
              <span
                class="job-state"
                :class="`tone-${jobStateMeta(job.state).tone}`"
                data-testid="task-state"
              >{{ jobStateMeta(job.state).icon }} {{ jobStateMeta(job.state).label }}</span>
              <span class="spacer" />
              <span class="job-id mono" :title="job.id">{{ job.id }}</span>
              <button
                v-if="job.cancellable"
                type="button"
                class="drawer-btn danger"
                data-testid="task-cancel"
                title="合作式取消：已开始仓取消，未开始仓保持原状（不整体回滚）"
                @click.stop="onCancel(job)"
              >取消</button>
            </div>

            <div class="job-progress" :title="`成功 ${job.ok} · 失败 ${job.failed} · 跳过 ${job.skipped} / 共 ${job.total}`">
              <div class="progress-track">
                <span class="progress-ok" :style="{ width: `${jobProgressPercent(job)}%` }" />
              </div>
              <span class="progress-text" data-testid="task-progress">{{ job.ok }}/{{ job.total }}</span>
              <span v-if="job.failed > 0" class="failed-count" data-testid="task-failed-count">失败 {{ job.failed }}</span>
              <span v-if="job.skipped > 0" class="skipped-count">跳过 {{ job.skipped }}</span>
            </div>

            <div class="job-time" data-testid="task-time">{{ jobTimeText(job) }}</div>

            <div class="job-actions">
              <button
                type="button"
                class="task-link"
                data-testid="task-expand"
                :aria-expanded="isExpanded(job.id)"
                @click.stop="w.toggleJobExpanded(job.id)"
              >{{ isExpanded(job.id) ? '收起逐仓结果' : `展开逐仓结果（${job.items.length}）` }}</button>
              <button
                v-if="!job.active && retryableCount(job) > 0"
                type="button"
                class="task-link"
                data-testid="task-retry-failed"
                title="仅重试失败/取消/中断项，已成功仓不重跑"
                @click.stop="onRetry(job)"
              >仅重试失败项（{{ retryableCount(job) }}）</button>
            </div>

            <ul v-if="isExpanded(job.id)" class="job-items" data-testid="task-items">
              <li
                v-for="(item, index) in job.items"
                :key="`${item.componentId}:${item.repoType}:${index}`"
                class="job-item"
                data-testid="task-item"
                :data-item-state="item.state"
                :data-repo-type="item.repoType"
                :data-component-id="item.componentId"
              >
                <span class="item-dot" :class="`tone-${jobItemStateMeta(item.state).tone}`" aria-hidden="true" />
                <span class="item-repo" :title="`${item.componentId} / ${item.repoType}`">
                  {{ repoLabelOf(item.repoType) }} · <span class="mono">{{ item.componentId }}</span>
                </span>
                <span
                  class="item-state"
                  :class="`tone-${jobItemStateMeta(item.state).tone}`"
                  data-testid="task-item-state"
                >{{ jobItemStateMeta(item.state).icon }} {{ jobItemStateMeta(item.state).label }}</span>
                <span v-if="item.errorCode" class="item-error" :data-error-code="item.errorCode" data-testid="task-item-error">
                  <b class="mono">{{ item.errorCode }}</b>
                  <span v-if="item.message"> · {{ item.message }}</span>
                </span>
                <span v-else-if="item.message" class="item-message">{{ item.message }}</span>
                <span v-if="item.preHead || item.postHead" class="item-heads mono" data-testid="task-item-heads">
                  {{ shortSha(item.preHead) }} → {{ shortSha(item.postHead) }}
                </span>
                <span v-if="item.backupRef" class="item-backup" data-testid="task-item-backup" title="备份 ref（保留 30 天；供审计与后续恢复能力）">
                  <span class="mono" data-testid="task-backup-ref">{{ item.backupRef }}</span>
                </span>
                <span class="item-times">
                  {{ item.startedAt ? formatClock(item.startedAt) : '—' }} → {{ item.finishedAt ? formatClock(item.finishedAt) : (item.state === 'RUNNING' ? '进行中' : '—') }}
                </span>
              </li>
            </ul>
          </li>
        </ul>

        <p class="task-footnote">
          M2b：批量写操作经 Host 作业运行；取消为合作式（不整体回滚），终态以实际扫描为准；失败项可“仅重试失败项”。
        </p>
      </div>

      <!-- 通知中心 -->
      <div v-else class="drawer-body">
        <div class="drawer-toolbar">
          <span class="summary">{{ w.unread() }} 条未读 · 应用内通知（Mock）</span>
          <button type="button" class="drawer-btn" @click="w.markAllRead()">全部已读</button>
        </div>
        <div class="notif-list">
          <div v-for="item in state.notifications" :key="item.id" class="notif" :class="{ unread: !item.read }">
            <span class="dot" :class="item.level === 'err' ? 'err' : item.level === 'warn' ? 'warn' : ''" aria-hidden="true" />
            <div class="notif-body">
              <div class="notif-title">{{ item.title }} <span class="notif-tag">{{ item.type }}</span></div>
              <div class="notif-detail">{{ item.detail }}</div>
            </div>
            <span class="notif-at">{{ formatAgo(item.at, state.now) }}</span>
          </div>
        </div>

        <div class="section-title">CI 事件（CI-6）</div>
        <div class="notif ci-notif-category" data-testid="ci-notif-category">
          <span class="dot off" aria-hidden="true" />
          <div class="notif-body">
            <div class="notif-title">CI 事件类目 <span class="notif-tag">占位</span></div>
            <div class="notif-detail">CI 失败 / 通过 / 超时通知将在 v2 接入；当前为空分组。</div>
            <ul class="ci-types">
              <li v-for="entry in ciNotifications" :key="entry.type"><b>{{ entry.type }}</b>：{{ entry.detail }}</li>
            </ul>
          </div>
        </div>
      </div>
    </aside>
  </div>
</template>

<style scoped>
.overlay {
  position: fixed;
  inset: 0;
  background: rgb(10 12 16 / 32%);
  z-index: var(--z-drawer);
  display: flex;
  justify-content: flex-end;
}
.drawer {
  width: 420px;
  max-width: min(92vw, 480px);
  height: 100%;
  display: flex;
  flex-direction: column;
  background: var(--surface);
  border-left: 1px solid var(--border);
  box-shadow: var(--shadow);
  animation: slide-in 200ms var(--ease);
}
.drawer-wide { width: 520px; max-width: min(94vw, 560px); }
@keyframes slide-in {
  from { transform: translateX(24px); opacity: 0; }
  to { transform: translateX(0); opacity: 1; }
}
.drawer-head {
  flex: 0 0 48px;
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 0 12px;
  border-bottom: 1px solid var(--border);
}
.drawer-head .title { flex: 1; font-size: 14px; font-weight: 650; }
.source-pill {
  padding: 1px 7px;
  border-radius: 999px;
  border: 1px solid var(--st-neutral-bd);
  color: var(--text-2);
  font-size: 10px;
}
.drawer-body { flex: 1; min-height: 0; overflow-y: auto; padding: 12px 14px 24px; display: flex; flex-direction: column; gap: 8px; }
.summary { font-size: 11px; color: var(--text-2); }
.drawer-toolbar { display: flex; align-items: center; justify-content: space-between; gap: 8px; }
.drawer-btn {
  height: 26px;
  padding: 0 8px;
  border: 1px solid var(--border);
  border-radius: var(--r-btn);
  background: var(--surface);
  color: var(--text);
  font-size: 11px;
  cursor: pointer;
  white-space: nowrap;
}
.drawer-btn:hover { background: var(--surface-2); }
.drawer-btn.danger { border-color: var(--st-danger-bd); color: var(--st-danger-fg); }
.spacer { flex: 1; }

/* ── 任务中心 ── */
.task-toolbar { display: flex; align-items: center; gap: 8px; }
.task-meta-row { display: flex; align-items: center; justify-content: space-between; gap: 8px; font-size: 10px; color: var(--text-2); }
.task-source-note { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.task-tabs { display: flex; gap: 4px; border-bottom: 1px solid var(--border); }
.task-tab {
  height: 28px;
  padding: 0 10px;
  border: 0;
  border-bottom: 2px solid transparent;
  background: transparent;
  color: var(--text-2);
  font: inherit;
  font-size: 12px;
  cursor: pointer;
}
.task-tab.active { color: var(--text); border-bottom-color: var(--focus); font-weight: 650; }
.tab-count {
  margin-left: 4px;
  padding: 0 5px;
  border-radius: 8px;
  background: var(--surface-2);
  border: 1px solid var(--border);
  font-size: 9px;
  font-variant-numeric: tabular-nums;
}
.task-loading { padding: 24px 0; text-align: center; font-size: 12px; color: var(--text-2); }
.task-error {
  display: flex;
  flex-direction: column;
  gap: 6px;
  padding: 10px 12px;
  border: 1px solid var(--st-danger-bd);
  border-radius: var(--r-panel);
  background: color-mix(in srgb, var(--st-danger-bg) 45%, var(--surface));
  font-size: 11px;
}
.task-error-title { font-weight: 650; color: var(--st-danger-fg); }
.task-error-detail { color: var(--text-2); line-height: 1.6; overflow-wrap: anywhere; }
.job-list { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 8px; }
.job-card {
  display: flex;
  flex-direction: column;
  gap: 5px;
  padding: 9px 11px;
  border: 1px solid var(--border);
  border-radius: var(--r-panel);
  background: var(--surface);
  cursor: pointer;
}
.job-card.active { border-left: 3px solid var(--st-info-fg); }
.job-card.current { border-color: var(--focus); box-shadow: inset 0 0 0 1px var(--focus); }
.job-head { display: flex; align-items: center; gap: 7px; min-width: 0; }
.job-kind { font-size: 12px; font-weight: 650; white-space: nowrap; }
.job-state { font-size: 10px; font-weight: 600; padding: 0 6px; border-radius: 999px; white-space: nowrap; }
.job-id { font-size: 9px; color: var(--text-2); max-width: 110px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.job-progress { display: flex; align-items: center; gap: 6px; }
.progress-track { flex: 1; height: 5px; border-radius: 3px; background: var(--surface-2); overflow: hidden; }
.progress-ok { display: block; height: 100%; background: var(--st-info-fg); transition: width 240ms var(--ease); }
.progress-text { font-size: 11px; font-weight: 650; font-variant-numeric: tabular-nums; white-space: nowrap; }
.failed-count { font-size: 10px; color: var(--st-danger-fg); white-space: nowrap; }
.skipped-count { font-size: 10px; color: var(--st-warning-fg); white-space: nowrap; }
.job-time { font-size: 10px; color: var(--text-2); }
.job-actions { display: flex; gap: 10px; flex-wrap: wrap; }
.task-link {
  border: 0;
  background: transparent;
  color: var(--st-info-fg);
  font: inherit;
  font-size: 10px;
  padding: 0;
  cursor: pointer;
  text-decoration: underline;
  text-underline-offset: 2px;
}
.task-link:hover { color: var(--text); }
.job-items { list-style: none; margin: 3px 0 0; padding: 6px 0 0; border-top: 1px dashed var(--border); display: flex; flex-direction: column; gap: 4px; }
.job-item { display: flex; flex-wrap: wrap; align-items: center; gap: 6px; font-size: 10px; line-height: 1.5; }
.item-dot { width: 6px; height: 6px; border-radius: 50%; background: var(--st-neutral-bd); flex: 0 0 auto; }
.item-repo { font-weight: 600; }
.item-state { padding: 0 5px; border-radius: 999px; border: 1px solid transparent; white-space: nowrap; }
.item-error { color: var(--st-danger-fg); flex: 1 1 100%; padding-left: 12px; overflow-wrap: anywhere; }
.item-message { color: var(--text-2); flex: 1 1 100%; padding-left: 12px; overflow-wrap: anywhere; }
.item-heads { color: var(--text-2); }
.item-backup { display: inline-flex; align-items: center; gap: 6px; flex: 1 1 100%; padding-left: 12px; color: var(--st-warning-fg); overflow-wrap: anywhere; }
.item-times { margin-left: auto; color: var(--text-2); white-space: nowrap; }
.task-footnote { margin: 4px 0 0; font-size: 10px; line-height: 1.6; color: var(--text-2); }

/* 状态 tone（与 StateChip 一致） */
.tone-success { color: var(--st-success-fg); background: var(--st-success-bg); border-color: var(--st-success-bd); }
.tone-warning { color: var(--st-warning-fg); background: var(--st-warning-bg); border-color: var(--st-warning-bd); }
.tone-info { color: var(--st-info-fg); background: var(--st-info-bg); border-color: var(--st-info-bd); }
.tone-danger { color: var(--st-danger-fg); background: var(--st-danger-bg); border-color: var(--st-danger-bd); }
.tone-neutral { color: var(--st-neutral-fg); background: var(--st-neutral-bg); border-color: var(--st-neutral-bd); }
.tone-purple { color: var(--st-purple-fg); background: var(--st-purple-bg); border-color: var(--st-purple-bd); }
.tone-cyan { color: var(--st-cyan-fg); background: var(--st-cyan-bg); border-color: var(--st-cyan-bd); }
.tone-hatched { color: var(--st-unknown-fg); background: var(--st-neutral-bg); border-color: var(--st-neutral-bd); }
.item-dot.tone-success { background: var(--st-success-fg); }
.item-dot.tone-warning { background: var(--st-warning-fg); }
.item-dot.tone-info { background: var(--st-info-fg); }
.item-dot.tone-danger { background: var(--st-danger-fg); }

/* ── 通知中心 ── */
.empty {
  min-height: 240px;
  display: grid;
  place-content: center;
  justify-items: center;
  gap: 6px;
  text-align: center;
  color: var(--text-2);
  border: 1px dashed var(--border);
  border-radius: var(--r-panel);
  padding: 16px;
}
.section-title {
  margin-top: 6px;
  font-size: 11px;
  font-weight: 650;
  color: var(--text-2);
  text-transform: uppercase;
  letter-spacing: 0.3px;
}
.notif-list { display: flex; flex-direction: column; gap: 8px; }
.notif {
  display: flex;
  gap: 10px;
  align-items: flex-start;
  padding: 10px 12px;
  border: 1px solid var(--border);
  border-radius: var(--r-panel);
  background: var(--surface);
}
.notif.unread { border-left: 3px solid var(--st-info-fg); background: color-mix(in srgb, var(--st-info-bg) 45%, var(--surface)); }
.dot { width: 8px; height: 8px; border-radius: 50%; background: var(--st-success-fg); margin-top: 5px; flex: 0 0 auto; }
.dot.warn { background: var(--st-warning-fg); }
.dot.err { background: var(--st-danger-fg); }
.dot.off { background: var(--st-neutral-bd); }
.notif-body { flex: 1; min-width: 0; }
.notif-title { font-size: 12px; font-weight: 600; }
.notif-detail { font-size: 11px; color: var(--text-2); }
.notif-at { font-size: 10px; color: var(--text-2); white-space: nowrap; }
.notif-tag {
  margin-left: 6px;
  padding: 0 6px;
  border-radius: 999px;
  font-size: 9px;
  font-weight: 500;
  color: var(--text-2);
  border: 1px solid var(--border);
}
.ci-types { margin: 6px 0 0; padding-left: 16px; font-size: 10px; color: var(--text-2); line-height: 1.7; }
.mono { font-family: ui-monospace, Consolas, monospace; }
@media (prefers-reduced-motion: reduce) {
  .drawer { animation: none; }
  .progress-ok { transition: none; }
}
</style>
