<script setup lang="ts">
import { computed } from 'vue'
import { useWorkbench } from '../../stores/workbench'
import { formatAgo, M2B_BOUNDARY_TEXT } from '../../viewmodel'

const w = useWorkbench()
const state = w.state

const kind = computed(() => state.overlays.drawer)
const title = computed(() => (kind.value === 'tasks' ? '任务中心' : '通知中心'))

const ciNotifications = [
  { type: 'CI 失败', detail: '构建失败 / 失败阶段 / 构建号 / 链接（v1 空分组）' },
  { type: 'CI 通过', detail: '构建成功（v1 空分组）' },
  { type: 'CI 超时', detail: '构建超时（v1 空分组）' },
]
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
        <button type="button" class="icon-btn" autofocus title="关闭（Esc）" aria-label="关闭" @click="w.closeDrawer()">✕</button>
      </header>

      <div v-if="kind === 'tasks'" class="drawer-body">
        <div class="summary">进行中 0 · 历史 0 · 当前队列 {{ state.taskCount }}</div>
        <div class="empty">
          <div class="empty-icon" aria-hidden="true">◷</div>
          <div class="empty-title">暂无进行中的任务</div>
          <div class="empty-desc">批量 Clone / Pull / 切 Tag / SYNC / Delete：{{ M2B_BOUNDARY_TEXT }}，接入后在此显示逐仓进度与失败明细。</div>
          <button type="button" class="drawer-btn" data-testid="drawer-batch-entry" @click="w.notify(`任务批量提交：${M2B_BOUNDARY_TEXT}`, 'warning')">了解批量入口</button>
        </div>
        <div class="section-title">任务队列字段（契约占位）</div>
        <div class="kv"><span>jobId</span><span class="muted">—</span></div>
        <div class="kv"><span>逐仓进度</span><span class="muted">—</span></div>
        <div class="kv"><span>取消 / 重试失败项</span><span class="muted">{{ M2B_BOUNDARY_TEXT }}</span></div>
      </div>

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
.drawer-wide { width: 480px; }
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
}
.drawer-btn:hover { background: var(--surface-2); }
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
.empty-icon { font-size: 30px; color: var(--st-neutral-bd); }
.empty-title { font-size: 14px; font-weight: 600; color: var(--text); }
.empty-desc { max-width: 340px; line-height: 1.7; font-size: 11px; }
.section-title {
  margin-top: 6px;
  font-size: 11px;
  font-weight: 650;
  color: var(--text-2);
  text-transform: uppercase;
  letter-spacing: 0.3px;
}
.kv { display: flex; justify-content: space-between; font-size: 11px; padding: 5px 0; border-bottom: 1px dashed var(--border); }
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
@media (prefers-reduced-motion: reduce) {
  .drawer { animation: none; }
}
</style>

