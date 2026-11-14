<script setup lang="ts">
/**
 * M2 单仓动作对话框：COMMIT（提交信息必填）/ CHKOUT（ref 搜索选择 + 分支/Tag 手动输入 + dirty 自动 stash）/
 * 通用确认。状态由 store.overlays.actionDialog 驱动；提交后由 store 关闭并执行。
 */
import { computed, nextTick, ref, watch } from 'vue'
import { useWorkbench } from '../../stores/workbench'
import type { ActionDialogRefVm } from '../../types/workbench'

const w = useWorkbench()
const state = w.state

/** CHKOUT 选择器单次最多渲染的 ref 条数（P0-7）。 */
const MAX_REF_RENDER = 200

const dialog = computed(() => state.overlays.actionDialog)
const message = ref('')
const refKind = ref<'branch' | 'tag'>('branch')
const refValue = ref('')
const stageAll = ref(false)
const stash = ref(false)
const search = ref('')
const root = ref<HTMLElement | null>(null)
const messageEl = ref<HTMLTextAreaElement | null>(null)
const refEl = ref<HTMLInputElement | null>(null)
const searchEl = ref<HTMLInputElement | null>(null)

let previousFocus: HTMLElement | null = null

watch(
  dialog,
  (value) => {
    if (!value) {
      previousFocus?.focus?.()
      previousFocus = null
      return
    }
    previousFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null
    message.value = value.initialMessage ?? ''
    refKind.value = value.initialRefKind ?? 'branch'
    refValue.value = value.initialRefValue ?? ''
    search.value = value.initialSearch ?? ''
    // 「暂存全部改动」/「检出前自动 stash」默认不勾选：只有用户显式勾选才传对应 args。
    stageAll.value = false
    stash.value = false
    void nextTick(() => {
      if (value.kind === 'commit') messageEl.value?.focus()
      else if (value.kind === 'checkout') (searchEl.value ?? refEl.value)?.focus()
      else root.value?.querySelector<HTMLElement>('.btn.primary')?.focus()
    })
  },
  { immediate: true },
)

const messageOk = computed(() => {
  const length = message.value.trim().length
  return length > 0 && length <= 200
})
const refOk = computed(() => refValue.value.trim().length > 0)
const canSubmit = computed(() => {
  if (!dialog.value) return false
  if (dialog.value.kind === 'commit') return messageOk.value
  if (dialog.value.kind === 'checkout') return refOk.value
  return true
})
const charCount = computed(() => message.value.trim().length)
const refPlaceholder = computed(() =>
  refKind.value === 'tag' ? '输入 Tag 名称，例如 v1.2.0' : '输入分支名，例如 main 或 feat/x',
)
const messagePlaceholder = computed(() =>
  dialog.value?.action === 'COMMIT' ? 'fix: ...' : '请输入提交信息',
)

// ── CHKOUT ref 搜索与分组（本地分支 / 远程分支 / Tag，最多渲染 200 条） ──

/** 列表展示名：去掉 refs/heads|refs/tags|refs/remotes 前缀。 */
function refDisplayName(item: ActionDialogRefVm): string {
  if (item.kind === 'TAG') return item.refName.replace(/^refs\/tags\//, '')
  if (item.kind === 'REMOTE_BRANCH') return item.refName.replace(/^refs\/remotes\//, '')
  return item.refName.replace(/^refs\/heads\//, '')
}

/** 填入输入框的值：远端分支去掉 refs/remotes/<remote>/ 前缀（Host 按本地分支名校验）。 */
function refValueOf(item: ActionDialogRefVm): string {
  if (item.kind === 'TAG') return item.refName.replace(/^refs\/tags\//, '')
  if (item.kind === 'REMOTE_BRANCH') return item.refName.replace(/^refs\/remotes\/[^/]+\//, '')
  return item.refName.replace(/^refs\/heads\//, '')
}

function refMatches(item: ActionDialogRefVm): boolean {
  const query = search.value.trim().toLowerCase()
  if (!query) return true
  return (
    item.refName.toLowerCase().includes(query) ||
    item.shortSha.toLowerCase().includes(query)
  )
}

const matchedRefs = computed(() => (dialog.value?.refs ?? []).filter(refMatches))

const refGroups = computed(() => {
  const limited = matchedRefs.value.slice(0, MAX_REF_RENDER)
  const groups = (
    [
      { key: 'LOCAL_BRANCH', label: '本地分支', items: limited.filter((item) => item.kind === 'LOCAL_BRANCH') },
      { key: 'REMOTE_BRANCH', label: '远程分支', items: limited.filter((item) => item.kind === 'REMOTE_BRANCH') },
      { key: 'TAG', label: 'Tag', items: limited.filter((item) => item.kind === 'TAG') },
    ] as { key: ActionDialogRefVm['kind']; label: string; items: ActionDialogRefVm[] }[]
  ).filter((group) => group.items.length > 0)
  return { total: matchedRefs.value.length, rendered: limited.length, groups }
})

/** datalist：手动输入时的完整候选（去重，最多 200）。 */
const refSuggestions = computed(() => {
  const seen = new Set<string>()
  const out: string[] = []
  for (const item of matchedRefs.value.slice(0, MAX_REF_RENDER)) {
    const value = refValueOf(item)
    const key = `${item.kind === 'TAG' ? 'tag' : 'branch'}:${value}`
    if (!value || seen.has(key)) continue
    seen.add(key)
    out.push(value)
  }
  return out
})

function isPicked(item: ActionDialogRefVm): boolean {
  return refValue.value.trim() === refValueOf(item) && (item.kind === 'TAG') === (refKind.value === 'tag')
}

/** 点击列表项：自动切换 分支/Tag 并填入输入框（仍可手动修改）。 */
function pickRef(item: ActionDialogRefVm): void {
  refKind.value = item.kind === 'TAG' ? 'tag' : 'branch'
  refValue.value = refValueOf(item)
  void nextTick(() => refEl.value?.focus())
}

const refSourceText = computed(() => {
  const value = dialog.value
  if (!value || value.kind !== 'checkout') return ''
  const count = matchedRefs.value.length
  if (value.refsStatus === 'loading') return '正在读取分支 / Tag…'
  if (value.refsStatus === 'error') return value.refsError || '读取分支 / Tag 失败，请手动输入'
  if (value.refsStatus === 'fallback') {
    return value.refsError || 'Mock / 无 Host：已回退为当前分支 + Tag 建议'
  }
  return `共 ${count} 个候选${value.refsTruncated ? '（Host 已截断，请用搜索缩小范围）' : ''}`
})

function submit(): void {
  const value = dialog.value
  if (!value || !canSubmit.value) return
  if (value.kind === 'commit') w.submitActionDialog({ message: message.value.trim(), stageAll: stageAll.value })
  else if (value.kind === 'checkout')
    w.submitActionDialog({
      refKind: refKind.value,
      refValue: refValue.value.trim(),
      stash: stash.value,
      search: search.value,
    })
  else w.submitActionDialog({})
}

function onMessageKeydown(event: KeyboardEvent): void {
  if ((event.ctrlKey || event.metaKey) && event.key === 'Enter') {
    event.preventDefault()
    submit()
  }
}

function trapTab(event: KeyboardEvent): void {
  const rootEl = root.value
  if (!rootEl) return
  const focusables = [...rootEl.querySelectorAll<HTMLElement>('button, input, textarea, [tabindex="0"]')].filter(
    (node) => !node.hasAttribute('disabled') && node.offsetParent !== null,
  )
  if (focusables.length === 0) return
  const first = focusables[0]
  const last = focusables[focusables.length - 1]
  if (event.shiftKey && document.activeElement === first) {
    event.preventDefault()
    last.focus()
  } else if (!event.shiftKey && document.activeElement === last) {
    event.preventDefault()
    first.focus()
  }
}
</script>

<template>
  <div v-if="dialog" class="overlay" @click.self="w.closeActionDialog()" @keydown.tab="trapTab">
    <section
      ref="root"
      class="action-dialog"
      data-testid="action-dialog"
      :data-action="dialog.action"
      :data-kind="dialog.kind"
      role="dialog"
      aria-modal="true"
      :aria-label="dialog.title"
    >
      <header class="dialog-head">
        <span class="dialog-title">{{ dialog.title }}</span>
        <span class="spacer" />
        <button type="button" class="icon-btn" title="关闭（Esc）" aria-label="关闭" @click="w.closeActionDialog()">✕</button>
      </header>

      <div class="dialog-body">
        <p class="dialog-text">{{ dialog.body }}</p>
        <p v-if="dialog.detail" class="dialog-detail mono">{{ dialog.detail }}</p>

        <div v-if="dialog.info && dialog.info.length > 0" class="info-grid" data-testid="action-dialog-info">
          <div v-for="row in dialog.info" :key="row.label" class="info-row">
            <span class="info-label">{{ row.label }}</span>
            <span class="info-value" :class="row.tone ? `tone-${row.tone}` : ''">{{ row.value }}</span>
          </div>
        </div>

        <!-- COMMIT：提交信息必填（多行，支持 Ctrl+Enter 提交） -->
        <div v-if="dialog.kind === 'commit'" class="form">
          <label class="form-label" for="action-dialog-message">
            提交信息 <span class="required" aria-hidden="true">*</span>
            <span class="hint">必填 1–200 字符，建议使用 conventional 前缀（如 fix: …）</span>
          </label>
          <textarea
            id="action-dialog-message"
            ref="messageEl"
            v-model="message"
            class="form-textarea"
            data-testid="action-dialog-message"
            rows="3"
            maxlength="200"
            :placeholder="messagePlaceholder"
            aria-required="true"
            @keydown="onMessageKeydown"
          />
          <div class="counter" :class="{ invalid: !messageOk }">{{ charCount }} / 200</div>
          <!-- FR-ACT-03/04：仅显式勾选才传 args.stageAll=true；默认 staged-only（Host 不再自动 add -A）。 -->
          <label v-if="dialog.showStageAll" class="stage-all" data-testid="commit-stage-all-label">
            <input
              v-model="stageAll"
              type="checkbox"
              data-testid="commit-stage-all"
              :aria-describedby="'commit-stage-all-hint'"
            />
            <span class="stage-all-text">暂存全部改动（git add -A）</span>
          </label>
          <p id="commit-stage-all-hint" class="stage-all-hint" data-testid="commit-stage-all-hint">
            默认只提交已暂存内容（已暂存 {{ dialog.stagedCount ?? 0 }} 个文件）；
            {{ stageAll
              ? `已勾选：将先 git add -A，提交全部约 ${(dialog.stagedCount ?? 0) + (dialog.dirtyCount ?? 0) + (dialog.untrackedCount ?? 0)} 个文件`
              : `未勾选：另有未跟踪/已修改 ${(dialog.dirtyCount ?? 0) + (dialog.untrackedCount ?? 0)} 处不会进入本次提交` }}
          </p>
        </div>

        <!-- CHKOUT：ref 搜索选择（本地/远程分支 + Tag）+ 手动输入 + dirty 自动 stash -->
        <div v-else-if="dialog.kind === 'checkout'" class="form">
          <div class="radios" role="radiogroup" aria-label="检出类型">
            <label class="radio">
              <input v-model="refKind" type="radio" value="branch" data-testid="checkout-kind-branch" />
              <span>分支</span>
            </label>
            <label class="radio">
              <input v-model="refKind" type="radio" value="tag" data-testid="checkout-kind-tag" />
              <span>Tag</span>
            </label>
            <span class="spacer" />
            <span class="ref-source-inline" data-testid="checkout-ref-source-inline">{{ refSourceText }}</span>
          </div>

          <input
            ref="searchEl"
            v-model="search"
            class="form-input ref-search"
            data-testid="checkout-ref-search"
            type="search"
            autocomplete="off"
            spellcheck="false"
            placeholder="搜索分支 / Tag / SHA…"
            aria-label="搜索分支或 Tag"
          />

          <div class="ref-groups" data-testid="checkout-ref-groups">
            <p v-if="dialog.refsStatus === 'loading'" class="ref-empty" data-testid="checkout-ref-loading">
              正在读取分支 / Tag…
            </p>
            <p
              v-else-if="refGroups.total === 0"
              class="ref-empty"
              data-testid="checkout-ref-empty"
            >
              {{ search.trim() ? `没有匹配「${search.trim()}」的分支 / Tag` : '暂无可选分支 / Tag，请手动输入' }}
            </p>
            <template v-else>
              <section
                v-for="group in refGroups.groups"
                :key="group.key"
                class="ref-group"
                :data-testid="`checkout-ref-group-${group.key}`"
              >
                <header class="ref-group-head">
                  <span class="ref-group-label">{{ group.label }}</span>
                  <span class="ref-count">{{ group.items.length }}</span>
                  <span v-if="group.key === 'REMOTE_BRANCH'" class="ref-tip">需本地已有同名分支</span>
                </header>
                <ul class="ref-list" role="listbox" :aria-label="group.label">
                  <li v-for="item in group.items" :key="item.refName">
                    <button
                      type="button"
                      class="ref-item"
                      :class="{ picked: isPicked(item) }"
                      role="option"
                      :aria-selected="isPicked(item)"
                      data-testid="checkout-ref-item"
                      :data-ref-name="item.refName"
                      :data-ref-kind="item.kind"
                      @click="pickRef(item)"
                    >
                      <span class="ref-name" :title="item.refName">{{ refDisplayName(item) }}</span>
                      <span class="ref-sha mono">{{ item.shortSha }}</span>
                    </button>
                  </li>
                </ul>
              </section>
            </template>
            <p
              v-if="dialog.refsStatus !== 'loading' && refGroups.total > refGroups.rendered"
              class="ref-trunc-note"
              data-testid="checkout-ref-truncated"
            >
              匹配 {{ refGroups.total }} 条，仅渲染前 {{ refGroups.rendered }} 条，请用搜索缩小范围
            </p>
          </div>

          <label class="form-label" for="action-dialog-ref">
            {{ refKind === 'tag' ? 'Tag 名称' : '分支名' }} <span class="required" aria-hidden="true">*</span>
            <span class="hint">可直接输入，或点击上方列表项填入</span>
          </label>
          <input
            id="action-dialog-ref"
            ref="refEl"
            v-model="refValue"
            class="form-input"
            data-testid="action-dialog-ref"
            type="text"
            maxlength="200"
            list="action-ref-suggestions"
            :placeholder="refPlaceholder"
            aria-required="true"
          />
          <datalist id="action-ref-suggestions">
            <option v-for="item in refSuggestions" :key="item" :value="item" />
          </datalist>

          <template v-if="dialog.showStash">
            <label class="stage-all" data-testid="checkout-stash-label">
              <input v-model="stash" type="checkbox" data-testid="checkout-stash" />
              <span class="stage-all-text">检出前自动 stash（-u）</span>
            </label>
            <p class="stage-all-hint" data-testid="checkout-stash-hint">
              {{ stash
                ? `已勾选：Host 将先 git stash push -u 保存 ${dialog.dirtyTotal ?? 0} 处改动（含未跟踪文件）再检出，可用 git stash pop 恢复`
                : `未勾选：工作区有 ${dialog.dirtyTotal ?? 0} 处改动时 Host 返回 E_DIRTY_WORKTREE，检出会被拒绝` }}
            </p>
          </template>
        </div>

        <p v-if="dialog.kind !== 'confirm'" class="safe-hint">Host 会先做预检；失败时返回错误码，不会静默失败。</p>
      </div>

      <footer class="dialog-actions">
        <button type="button" class="btn" data-testid="action-dialog-cancel" @click="w.closeActionDialog()">取消</button>
        <button
          type="button"
          class="btn"
          :class="[dialog.danger ? 'danger' : 'primary', { disabled: !canSubmit }]"
          data-testid="action-dialog-confirm"
          :disabled="!canSubmit"
          @click="submit"
        >{{ dialog.confirmLabel }}</button>
      </footer>
    </section>
  </div>
</template>

<style scoped>
.overlay {
  position: fixed;
  inset: 0;
  z-index: var(--z-modal);
  display: grid;
  place-items: center;
  padding: 16px;
  background: rgb(10 12 16 / 40%);
}
.action-dialog {
  width: min(520px, 94vw);
  max-height: 90vh;
  display: flex;
  flex-direction: column;
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: var(--r-panel);
  box-shadow: 0 20px 48px rgb(0 0 0 / 28%);
  overflow: hidden;
}
.dialog-head {
  flex: 0 0 42px;
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 0 12px;
  border-bottom: 1px solid var(--border);
}
.dialog-title { font-size: 13px; font-weight: 650; }
.spacer { flex: 1; }
.icon-btn {
  border: 0;
  background: transparent;
  color: var(--text-2);
  font-size: 13px;
  cursor: pointer;
}
.icon-btn:hover { color: var(--text); }
.dialog-body { flex: 1; min-height: 0; overflow-y: auto; padding: 12px 14px 14px; }
.dialog-text { margin: 0 0 6px; font-size: 12px; color: var(--text); line-height: 1.65; }
.dialog-detail {
  margin: 0 0 10px;
  padding: 6px 8px;
  border-radius: 4px;
  background: var(--surface-2);
  color: var(--text-2);
  font-size: 11px;
  line-height: 1.6;
  overflow-wrap: anywhere;
}
.info-grid {
  display: flex;
  flex-direction: column;
  gap: 2px;
  margin: 0 0 10px;
  padding: 6px 8px;
  border: 1px solid var(--border);
  border-radius: var(--r-panel);
  background: var(--surface-2);
}
.info-row { display: grid; grid-template-columns: 76px minmax(0, 1fr); gap: 8px; font-size: 11px; line-height: 1.7; }
.info-label { color: var(--text-2); }
.info-value { min-width: 0; overflow-wrap: anywhere; }
.info-value.tone-warning { color: var(--st-warning-fg); }
.info-value.tone-danger { color: var(--st-danger-fg); }
.info-value.tone-muted { color: var(--text-2); }
.form { display: flex; flex-direction: column; gap: 6px; margin-top: 8px; }
.form-label { font-size: 11px; font-weight: 600; color: var(--text); display: flex; flex-wrap: wrap; gap: 6px; align-items: baseline; }
.form-label .hint { font-weight: 400; color: var(--text-2); font-size: 10px; }
.required { color: var(--st-danger-fg); }
.form-textarea,
.form-input {
  width: 100%;
  border: 1px solid var(--border);
  border-radius: var(--r-btn);
  background: var(--surface);
  color: var(--text);
  font: inherit;
  font-size: 12px;
  padding: 7px 8px;
  outline: none;
  box-sizing: border-box;
}
.form-textarea { resize: vertical; min-height: 66px; line-height: 1.5; }
.form-textarea:focus,
.form-input:focus { border-color: var(--focus); box-shadow: 0 0 0 2px color-mix(in srgb, var(--focus) 24%, transparent); }
.counter { align-self: flex-end; font-size: 10px; color: var(--text-2); font-variant-numeric: tabular-nums; }
.counter.invalid { color: var(--st-danger-fg); }
.stage-all {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  margin-top: 2px;
  font-size: 12px;
  cursor: pointer;
}
.stage-all input { accent-color: var(--focus); }
.stage-all-text { font-weight: 600; }
.stage-all-hint {
  margin: 0;
  font-size: 10px;
  line-height: 1.5;
  color: var(--text-2);
}
.radios { display: flex; gap: 14px; align-items: center; }
.radio { display: inline-flex; align-items: center; gap: 4px; font-size: 12px; cursor: pointer; }
.radio input { accent-color: var(--focus); }
.ref-source-inline { font-size: 10px; color: var(--text-2); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.ref-search { margin-top: 2px; }
.ref-groups {
  display: flex;
  flex-direction: column;
  gap: 6px;
  max-height: 216px;
  overflow-y: auto;
  padding: 6px;
  border: 1px solid var(--border);
  border-radius: var(--r-panel);
  background: var(--surface-2);
}
.ref-group { display: flex; flex-direction: column; gap: 2px; }
.ref-group-head {
  display: flex;
  align-items: baseline;
  gap: 6px;
  padding: 2px 4px;
  font-size: 10px;
  font-weight: 650;
  color: var(--text-2);
  text-transform: none;
}
.ref-group-label { color: var(--text); }
.ref-count {
  font-variant-numeric: tabular-nums;
  padding: 0 5px;
  border-radius: 8px;
  background: var(--surface);
  border: 1px solid var(--border);
  font-size: 9px;
}
.ref-tip { margin-left: auto; font-weight: 400; color: var(--st-warning-fg); }
.ref-list { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 1px; }
.ref-item {
  width: 100%;
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 3px 6px;
  border: 1px solid transparent;
  border-radius: var(--r-btn);
  background: transparent;
  color: var(--text);
  font: inherit;
  font-size: 11px;
  text-align: left;
  cursor: pointer;
}
.ref-item:hover { background: var(--surface); border-color: var(--border); }
.ref-item.picked {
  border-color: var(--st-info-bd);
  background: var(--st-info-bg);
  color: var(--st-info-fg);
}
.ref-item:focus-visible { outline: 2px solid var(--focus); outline-offset: -1px; }
.ref-name { flex: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.ref-sha { flex: 0 0 auto; font-size: 10px; color: var(--text-2); }
.ref-empty { margin: 0; padding: 6px 4px; font-size: 11px; color: var(--text-2); }
.ref-trunc-note { margin: 2px 0 0; padding: 0 4px; font-size: 10px; color: var(--st-warning-fg); }
.safe-hint { margin: 10px 0 0; font-size: 10px; color: var(--text-2); }
.dialog-actions {
  flex: 0 0 auto;
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  padding: 10px 14px;
  border-top: 1px solid var(--border);
  background: var(--surface-2);
}
.btn {
  height: 30px;
  padding: 0 12px;
  border: 1px solid var(--border);
  border-radius: var(--r-btn);
  background: var(--surface);
  color: var(--text);
  font-size: 12px;
  cursor: pointer;
}
.btn:hover:not(:disabled) { background: var(--surface-2); }
.btn.primary { border-color: var(--st-info-bd); background: var(--st-info-bg); color: var(--st-info-fg); }
.btn.danger { border-color: var(--st-danger-bd); background: var(--st-danger-bg); color: var(--st-danger-fg); }
.btn:disabled { opacity: 0.5; cursor: not-allowed; }
.mono { font-family: ui-monospace, Consolas, monospace; }
</style>
