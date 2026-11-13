<script setup lang="ts">
import { computed } from 'vue'
import { useWorkbench } from '../../stores/workbench'

const w = useWorkbench()
const state = w.state

const healthTitle = computed(() => {
  const lines = [
    `Host：${state.hostLinked ? '已连接' : '未连接（Mock 模式）'}`,
    `GitWorker：${state.action.pending ? '执行中' : '空闲'}`,
    'RemoteSync：等待下次同步',
    'LocalScan：指纹 15s / 深扫变化触发',
    'StatusCalc：按快照时间',
    'JobRunner：队列 ' + state.taskCount,
  ]
  return lines.join('\n')
})

const freshnessTitle = computed(() => {
  const fresh = state.freshness
  const lines = [
    `本地扫描：${fresh.localAt ?? '—'}${fresh.localStale ? '（过期）' : ''}`,
    `远程拉取：${fresh.remoteAt ?? '—'}${fresh.remoteStale ? '（过期）' : ''}`,
  ]
  return lines.join('\n')
})
</script>

<template>
  <footer class="status-bar" data-testid="statusbar" role="status" aria-label="状态栏">
    <span class="seg last-op" data-seg="last-op" :data-testid="'status-last-op'" :class="`op-${state.action.lastOp.state}`" :title="w.lastOpText()">
      <span class="dot" :class="state.action.lastOp.state === 'error' ? 'err' : state.action.lastOp.state === 'running' ? 'warn' : state.action.lastOp.state === 'success' ? '' : 'off'" aria-hidden="true" />
      <span class="text">{{ w.lastOpText() }}</span>
    </span>
    <span class="seg" data-seg="freshness" data-testid="status-freshness" :class="{ stale: state.freshness.localStale || state.freshness.remoteStale }" :title="freshnessTitle">
      <span aria-hidden="true">{{ state.freshness.localStale || state.freshness.remoteStale ? '⏱' : '◷' }}</span>{{ w.freshnessText() }}
    </span>
    <span class="seg" data-seg="health" data-testid="status-health" :title="healthTitle">
      <span class="dot" :class="state.hostLinked ? '' : 'off'" aria-hidden="true" />Host
      <span class="dot" :class="state.action.pending ? 'warn' : ''" aria-hidden="true" />GitWorker
    </span>
    <span class="seg" data-seg="queue" data-testid="status-queue" title="操作队列 / 任务中心">队列 {{ state.taskCount }}</span>
    <button
      type="button"
      class="seg seg-btn"
      data-seg="ci"
      title="CI 队列（v1 恒 0）；点击打开 CI 流水线"
      @click="w.openModal('ci')"
    >
      <span class="ci-dot" aria-hidden="true" />CI 队列 <span data-testid="ci-queue">{{ state.ciQueue }}</span>
    </button>
    <button
      type="button"
      class="seg seg-btn"
      data-seg="notify"
      data-testid="status-notify"
      :title="`通知 ${w.unread()} 条未读`"
      @click="w.openDrawer('notifications')"
    >
      🔔 {{ w.unread() }}
    </button>
    <span class="seg" data-seg="network" data-testid="status-network" :class="{ offline: !state.online }">
      <span class="dot" :class="state.online ? '' : 'off'" aria-hidden="true" />{{ state.online ? '在线' : '离线' }}
    </span>
  </footer>
</template>

<style scoped>
.status-bar {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 0 12px;
  height: var(--layout-status-h);
  border-top: 1px solid var(--border);
  background: var(--surface);
  color: var(--text-2);
  font-size: 11px;
  white-space: nowrap;
  overflow: hidden;
}
.seg {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  min-width: 0;
}
.last-op {
  flex: 1;
  overflow: hidden;
}
.last-op .text { overflow: hidden; text-overflow: ellipsis; }
.op-error { color: var(--st-danger-fg); }
.op-running { color: var(--st-warning-fg); }
.op-success { color: var(--st-success-fg); }
.seg-btn {
  border: 0;
  padding: 0;
  background: transparent;
  color: inherit;
  font: inherit;
  cursor: pointer;
}
.seg-btn:hover { color: var(--text); text-decoration: underline; }
.dot { width: 7px; height: 7px; border-radius: 50%; background: var(--st-success-fg); flex: 0 0 auto; }
.dot.warn { background: var(--st-warning-fg); }
.dot.err { background: var(--st-danger-fg); }
.dot.off { background: var(--st-neutral-bd); }
.ci-dot {
  width: 7px;
  height: 7px;
  border-radius: 50%;
  border: 1px dashed var(--st-neutral-bd);
  background: transparent;
}
.stale { color: var(--st-warning-fg); }
.offline { color: var(--st-warning-fg); }
@media (max-width: 1099px) {
  .status-bar [data-seg="queue"], .status-bar [data-seg="network"] { display: none; }
}
</style>
