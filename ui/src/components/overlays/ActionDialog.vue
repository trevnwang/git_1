<script setup lang="ts">
/**
 * M2 单仓动作对话框：COMMIT（提交信息必填）/ CHKOUT（Radio 分支/Tag + 必填）/ 通用确认。
 * 状态由 store.overlays.actionDialog 驱动；提交后由 store 关闭并执行。
 */
import { computed, nextTick, ref, watch } from 'vue'
import { useWorkbench } from '../../stores/workbench'

const w = useWorkbench()
const state = w.state

const dialog = computed(() => state.overlays.actionDialog)
const message = ref('')
const refKind = ref<'branch' | 'tag'>('branch')
const refValue = ref('')
const root = ref<HTMLElement | null>(null)
const messageEl = ref<HTMLTextAreaElement | null>(null)
const refEl = ref<HTMLInputElement | null>(null)

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
    void nextTick(() => {
      if (value.kind === 'commit') messageEl.value?.focus()
      else if (value.kind === 'checkout') refEl.value?.focus()
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

function submit(): void {
  const value = dialog.value
  if (!value || !canSubmit.value) return
  if (value.kind === 'commit') w.submitActionDialog({ message: message.value.trim() })
  else if (value.kind === 'checkout') w.submitActionDialog({ refKind: refKind.value, refValue: refValue.value.trim() })
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
        </div>

        <!-- CHKOUT：Radio 分支/Tag + 必填输入 -->
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
          </div>
          <label class="form-label" for="action-dialog-ref">
            {{ refKind === 'tag' ? 'Tag 名称' : '分支名' }} <span class="required" aria-hidden="true">*</span>
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
            <option v-for="item in dialog.suggestions ?? []" :key="item" :value="item" />
          </datalist>
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
.radios { display: flex; gap: 14px; align-items: center; }
.radio { display: inline-flex; align-items: center; gap: 4px; font-size: 12px; cursor: pointer; }
.radio input { accent-color: var(--focus); }
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
