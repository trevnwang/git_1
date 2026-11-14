<script setup lang="ts">
import { computed } from 'vue'
import type { ComponentVm } from '../../types/workbench'
import { REPO_LABEL } from '../../viewmodel'
import ChangeBadge from '../common/ChangeBadge.vue'
import CiBadge from '../common/CiBadge.vue'
import StateChip from '../common/StateChip.vue'

const props = withDefaults(
  defineProps<{
    component: ComponentVm
    selected: boolean
    checked: boolean
    selectMode: boolean
  }>(),
  { checked: false, selectMode: false },
)

const emit = defineEmits<{
  select: []
  detail: []
  check: []
  quick: [command: 'SYNC' | 'CREATE']
  contextmenu: [event: MouseEvent]
}>()

const dirtyRepoCount = computed(
  () => props.component.repos.filter((repo) => repo.dirtyCount + repo.untrackedCount > 0).length,
)
const title = computed(() =>
  [
    props.component.id,
    `聚合状态：${props.component.state}`,
    `分支：${props.component.branch}`,
    props.component.tag ? `Tag：${props.component.tag}` : '',
    `变更：已修改 ${props.component.dirtyTotal} / 未跟踪 ${props.component.untrackedTotal}`,
  ]
    .filter(Boolean)
    .join('\n'),
)

function onClick(): void {
  emit('select')
}

/** 双击卡片 = 打开组件详情抽屉（FR-CARD-02），不替换工作台。 */
function onDblClick(): void {
  emit('select')
  emit('detail')
}

function onCheck(event: Event): void {
  event.stopPropagation()
  emit('check')
}

function onQuick(event: MouseEvent, command: 'SYNC' | 'CREATE'): void {
  event.stopPropagation()
  emit('quick', command)
}

function onContextMenu(event: MouseEvent): void {
  event.preventDefault()
  emit('contextmenu', event)
}
</script>

<template>
  <article
    class="component-card"
    :class="[`tone-${component.tone}`, { selected, checked, 'select-mode': selectMode }]"
    role="option"
    :aria-selected="selected"
    :tabindex="-1"
    :data-component-id="component.id"
    data-testid="component-card"
    :title="title"
    @click="onClick"
    @dblclick.stop="onDblClick"
    @contextmenu="onContextMenu"
  >
    <span class="card-rail" aria-hidden="true" />
    <div class="row row1">
      <input
        v-if="selectMode"
        class="check"
        type="checkbox"
        :checked="checked"
        :aria-label="`选择 ${component.id}`"
        @click.stop
        @change="onCheck"
      />
      <span class="comp-id truncate">
        {{ component.id }}<span v-if="component.starred" class="star" title="存在未提交改动">*</span>
      </span>
      <CiBadge :state="component.ci" slot />
      <StateChip :state="component.state" />
    </div>
    <div class="row row2">
      <span
        v-for="repo in component.repos"
        :key="repo.repoType"
        class="repo-chip"
        :class="`tone-${repo.tone}`"
        data-testid="card-repo-chip"
        :data-repo-type="repo.repoType"
        :title="`${REPO_LABEL[repo.repoType]}：${repo.state}（${repo.branch}，${repo.dirtyCount + repo.untrackedCount} 变更）`"
      >
        <span class="chip-dot" aria-hidden="true" />{{ REPO_LABEL[repo.repoType] }}<span
          v-if="repo.dirtyCount + repo.untrackedCount > 0"
          class="star"
        >*</span>
      </span>
    </div>
    <div class="row row3">
      <span class="branch truncate" :title="`当前分支：${component.branch}`">{{ component.branch }}</span>
      <span v-if="component.mixedBranch" class="mixed" title="四仓分支不一致">⑂ 混合</span>
      <span v-if="component.tag" class="card-tag truncate" :title="`Tag：${component.tag}`">⚑ {{ component.tag }}</span>
      <span class="spacer" />
      <span class="quick" aria-hidden="true">
        <button type="button" tabindex="-1" title="SYNC：远程覆盖本地（先预检 + 备份 ref，需确认）→ 任务中心" @click="onQuick($event, 'SYNC')">SYNC</button>
        <button type="button" tabindex="-1" title="CREATE：四仓创建同名分支（需输入分支名）→ 任务中心" @click="onQuick($event, 'CREATE')">CREATE</button>
      </span>
      <ChangeBadge :dirty="component.dirtyTotal" :untracked="component.untrackedTotal" />
      <span v-if="dirtyRepoCount > 0" class="dirty-repos" :title="`${dirtyRepoCount} 个仓有未提交改动`" />
    </div>
  </article>
</template>

<style scoped>
.component-card {
  --card-tint: var(--tint-neutral);
  position: relative;
  display: flex;
  flex-direction: column;
  justify-content: center;
  gap: 3px;
  height: var(--card-h);
  padding: var(--card-pad) 8px var(--card-pad) 12px;
  border: 1px solid var(--border);
  border-radius: var(--r-card);
  background: var(--surface);
  cursor: pointer;
  overflow: hidden;
  transition: background 120ms var(--ease), border-color 120ms var(--ease);
}
.tone-success { --card-tint: var(--tint-success); }
.tone-warning { --card-tint: var(--tint-warning); }
.tone-info { --card-tint: var(--tint-info); }
.tone-purple { --card-tint: var(--tint-purple); }
.tone-cyan { --card-tint: var(--tint-cyan); }
.tone-neutral { --card-tint: var(--tint-neutral); }
.tone-danger { --card-tint: var(--tint-danger); }
.tone-hatched { --card-tint: var(--tint-hatched); }
.component-card:hover { background: var(--surface-2); border-color: var(--rail); }
.component-card.selected {
  background: color-mix(in srgb, var(--st-info-bg) 38%, var(--surface));
  border-color: var(--focus);
  box-shadow: inset 0 0 0 1px var(--focus);
}
.component-card.checked { border-color: var(--st-info-bd); background: color-mix(in srgb, var(--st-info-bg) 24%, var(--surface)); }
.component-card:focus-visible { outline: 2px solid var(--focus); outline-offset: 2px; }
.card-rail {
  position: absolute;
  left: 0;
  top: 0;
  bottom: 0;
  width: 4px;
  background: var(--rail);
  border-radius: var(--r-card) 0 0 var(--r-card);
}
.tone-success .card-rail { background: var(--st-success-fg); }
.tone-warning .card-rail { background: var(--st-warning-fg); }
.tone-info .card-rail { background: var(--st-info-fg); }
.tone-purple .card-rail { background: var(--st-purple-fg); }
.tone-cyan .card-rail { background: var(--st-cyan-fg); }
.tone-neutral .card-rail { background: var(--st-neutral-bd); }
.tone-danger .card-rail { background: var(--st-danger-fg); }
.tone-hatched .card-rail { background: var(--st-unknown-fg); }
.selected .card-rail { width: 5px; }
.row { display: flex; align-items: center; gap: 6px; min-width: 0; }
.row1 { height: 20px; }
.row2 { height: 20px; gap: 4px; overflow: hidden; }
.row3 { height: 16px; gap: 5px; }
.comp-id { font-size: 12px; font-weight: 600; flex: 1; min-width: 0; }
.star { color: var(--st-warning-fg); font-weight: 700; }
.check { width: 14px; height: 14px; margin: 0; flex: 0 0 auto; accent-color: var(--focus); }
.repo-chip {
  display: inline-flex;
  align-items: center;
  gap: 3px;
  height: 18px;
  padding: 0 5px;
  border-radius: 5px;
  font-size: 10px;
  font-weight: 600;
  border: 1px solid transparent;
  white-space: nowrap;
  flex: 0 0 auto;
}
.repo-chip .chip-dot { width: 6px; height: 6px; border-radius: 50%; background: currentColor; }
.repo-chip.tone-success { color: var(--st-success-fg); background: var(--st-success-bg); border-color: var(--st-success-bd); }
.repo-chip.tone-warning { color: var(--st-warning-fg); background: var(--st-warning-bg); border-color: var(--st-warning-bd); }
.repo-chip.tone-info { color: var(--st-info-fg); background: var(--st-info-bg); border-color: var(--st-info-bd); }
.repo-chip.tone-purple { color: var(--st-purple-fg); background: var(--st-purple-bg); border-color: var(--st-purple-bd); }
.repo-chip.tone-cyan { color: var(--st-cyan-fg); background: var(--st-cyan-bg); border-color: var(--st-cyan-bd); }
.repo-chip.tone-danger { color: var(--st-danger-fg); background: var(--st-danger-bg); border-color: var(--st-danger-bd); }
.repo-chip.tone-neutral { color: var(--st-neutral-fg); background: var(--st-neutral-bg); border-style: dashed; border-color: var(--st-neutral-bd); }
.repo-chip.tone-hatched {
  color: var(--st-unknown-fg);
  background: repeating-linear-gradient(135deg, var(--surface-2) 0 5px, var(--border) 5px 10px);
}
.branch { font-size: 11px; color: var(--text-2); max-width: 90px; }
.mixed { font-size: 10px; color: var(--st-cyan-fg); white-space: nowrap; }
.card-tag { font-size: 10px; color: var(--text-2); max-width: 90px; }
.spacer { flex: 1; }
.quick { display: none; gap: 3px; }
.component-card:hover .quick { display: inline-flex; }
.quick button {
  height: 16px;
  padding: 0 5px;
  border: 1px solid var(--border);
  border-radius: 4px;
  background: var(--surface);
  color: var(--text-2);
  font-size: 9px;
  font-weight: 700;
  cursor: pointer;
}
.quick button:hover { background: var(--surface-2); color: var(--text); }
.dirty-repos { width: 5px; height: 5px; border-radius: 50%; background: var(--st-warning-fg); flex: 0 0 auto; }
@media (max-height: 799px) {
  .row3 { display: none; }
}
@media (prefers-reduced-motion: reduce) {
  .component-card { transition: none; }
}
</style>

