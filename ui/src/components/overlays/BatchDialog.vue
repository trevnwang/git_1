<script setup lang="ts">
/**
 * M2b 批量参数对话框（复用 overlays 交互模式）：
 * - CREATE / TAG：输入分支/Tag 名称（必填，本地基础校验，最终由 Host 校验）；
 * - CLONE：预览缺失仓清单；
 * - 可选「仅预览（dryRun）」。
 * 状态由 store.overlays.batchDialog 驱动；只有点「确认」才调用 runBatch（未确认 = 0 写）。
 */
import { computed, nextTick, ref, watch } from 'vue'
import { useWorkbench } from '../../stores/workbench'
import { REPO_LABEL } from '../../viewmodel'

const w = useWorkbench()
const state = w.state

const dialog = computed(() => state.overlays.batchDialog ?? null)
const value = ref('')
const dryRun = ref(false)
const cleanUntracked = ref(false)
const root = ref<HTMLElement | null>(null)
const inputEl = ref<HTMLInputElement | null>(null)

let previousFocus: HTMLElement | null = null

watch(
  dialog,
  (next) => {
    if (!next) {
      previousFocus?.focus?.()
      previousFocus = null
      return
    }
    previousFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null
    value.value = next.initialValue ?? ''
    dryRun.value = false
  cleanUntracked.value = false
    void nextTick(() => {
      if (next.kind === 'clone') root.value?.querySelector<HTMLElement>('.btn.primary')?.focus()
      else inputEl.value?.focus()
    })
  },
  { immediate: true },
)

const trimmed = computed(() => value.value.trim())

/** 基础 ref 名称校验（不替代 Host；仅用于禁用明显非法的提交）。 */
const valueValid = computed(() => {
  const text = trimmed.value
  if (!text || text.length > 200) return false
  if (/\s/.test(text)) return false
  if (text.startsWith('-') || text.startsWith('.') || text.endsWith('.') || text.endsWith('/')) return false
  if (text.includes('..')) return false
  if (/[~^:?*[\\]/.test(text)) return false
  return true
})

const needsValue = computed(
  () => dialog.value?.kind === 'branch' || dialog.value?.kind === 'tag' || dialog.value?.kind === 'chkout',
)
const canSubmit = computed(() => Boolean(dialog.value) && (!needsValue.value || valueValid.value))

const inputHint = computed(() => {
  const kind = dialog.value?.kind
  const action = dialog.value?.action
  const label = kind === 'tag' ? 'Tag' : kind === 'chkout' ? '目标分支/Tag' : '分支'
  if (action === 'DELETE' || action === 'SYNC') {
    if (!trimmed.value) return '必填：输入组件 ID 确认（区分大小写，须与组件 ID 完全一致）'
    return `将校验：${trimmed.value}（须与组件 ID 完全一致，Host 逐仓复核）`
  }
  if (kind === 'chkout') {
    if (!trimmed.value) return `必填：${label}（refs/tags/ 前缀按 Tag 处理）`
    if (valueValid.value) return `将检出：${trimmed.value}（工作区脏/ref 不存在时逐仓失败或跳过）`
    return `名称不合法：不能含空格或 ~^:?*[\\，不能以 - . 开头/以 . / 结尾，且不能包含 ..`
  }
  if (!trimmed.value) return `必填：${label}名称（1–200 字符）`
  if (valueValid.value) return `名称合法：${trimmed.value}（最终由 Host 校验冲突/权限）`
  return `名称不合法：不能含空格或 ~^:?*[\\，不能以 - . 开头/以 . / 结尾，且不能包含 ..`
})

function repoLabelOf(repoType: string): string {
  return ((REPO_LABEL as Record<string, string>)[repoType] ?? repoType) || '—'
}

function submit(): void {
  const current = dialog.value
  if (!current || !canSubmit.value) return
  w.submitBatchDialog({ value: needsValue.value ? trimmed.value : undefined, dryRun: dryRun.value, cleanUntracked: cleanUntracked.value })
}

function onKeydown(event: KeyboardEvent): void {
  if (event.key === 'Escape') {
    // 与 ConfirmDialog 一致：Esc 关闭并归还焦点；阻止冒泡，避免全局 Esc 继续关闭下层浮层。
    event.preventDefault()
    event.stopPropagation()
    w.closeBatchDialog()
    return
  }
  if (event.key === 'Enter' && needsValue.value) {
    event.preventDefault()
    submit()
  }
}

function trapTab(event: KeyboardEvent): void {
  const rootEl = root.value
  if (!rootEl) return
  const focusables = [...rootEl.querySelectorAll<HTMLElement>('button, input, [tabindex="0"]')].filter(
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
  <div v-if="dialog" class="overlay" @click.self="w.closeBatchDialog()" @keydown="onKeydown" @keydown.tab="trapTab">
    <section
      ref="root"
      class="batch-dialog"
      data-testid="batch-dialog"
      :data-batch-kind="dialog.kind"
      :data-batch-action="dialog.action"
      :data-dryrun="dryRun ? '1' : '0'"
      role="dialog"
      aria-modal="true"
      :aria-label="dialog.title"
    >
      <header class="dialog-head">
        <span class="dialog-title">{{ dialog.title }}</span>
        <span
          v-if="dialog.showDryRun && dryRun"
          class="dryrun-badge"
          data-testid="batch-dialog-dryrun-badge"
          title="dryRun 预览：仅预检，不执行任何写操作"
        >预览（dryRun）</span>
        <span class="spacer" />
        <button type="button" class="icon-btn" title="关闭（Esc）" aria-label="关闭" @click="w.closeBatchDialog()">✕</button>
      </header>

      <div class="dialog-body">
        <p class="dialog-text">{{ dialog.body }}</p>
        <p v-if="dialog.detail" class="dialog-detail mono">{{ dialog.detail }}</p>

        <!-- CLONE：缺失仓预览 -->
        <div v-if="dialog.kind === 'clone'" class="preview" data-testid="batch-dialog-preview">
          <div class="preview-head">
            <span>将 Clone 的缺失仓</span>
            <span class="preview-count">{{ dialog.preview?.length ?? 0 }}</span>
          </div>
          <ul class="preview-list">
            <li
              v-for="entry in dialog.preview ?? []"
              :key="`${entry.componentId}:${entry.repoType}`"
              class="preview-item"
              data-testid="batch-dialog-preview-item"
            >
              <span class="mono">{{ entry.componentId }}</span>
              <span class="preview-type">{{ repoLabelOf(entry.repoType) }}</span>
            </li>
          </ul>
        </div>

        <!-- CREATE / TAG：输入分支 / Tag 名称 -->
        <div v-else class="form">
          <label class="form-label" for="batch-dialog-input">
            {{ dialog.inputLabel ?? (dialog.kind === 'tag' ? 'Tag 名称' : '分支名') }}
            <span class="required" aria-hidden="true">*</span>
            <span class="hint">提交后由 Host 逐仓校验；名称冲突/脏仓将失败并可在任务中心重试</span>
          </label>
          <input
            id="batch-dialog-input"
            ref="inputEl"
            v-model="value"
            class="form-input"
            data-testid="batch-dialog-input"
            type="text"
            maxlength="200"
            autocomplete="off"
            spellcheck="false"
            :placeholder="dialog.inputPlaceholder"
            aria-required="true"
            @keydown="onKeydown"
          />
          <p class="input-hint" :class="{ invalid: trimmed.length > 0 && !valueValid }" data-testid="batch-dialog-input-hint">
            {{ inputHint }}
          </p>
        </div>

        <label v-if="dialog.showDryRun" class="dry-run" data-testid="batch-dialog-dryrun-label">
          <input v-model="dryRun" type="checkbox" data-testid="batch-dialog-dryrun" />
          <span>仅预览（dryRun，不执行任何写操作）</span>
        </label>
        <label v-if="dialog.showCleanUntracked" class="dry-run" data-testid="batch-dialog-clean-untracked-label">
  <input v-model="cleanUntracked" type="checkbox" data-testid="batch-dialog-clean-untracked" />
  <span>同时删除未跟踪文件（不可恢复；建议先勾选 dryRun 查看清单）</span>
</label><p v-if="dialog.showDryRun && dialog.dryRunHint" class="dry-run-hint" data-testid="batch-dialog-dryrun-hint">
          {{ dialog.dryRunHint }}
        </p>

        <p v-if="dialog.showDryRun && dryRun" class="dry-run-notice" data-testid="batch-dialog-dryrun-notice">
          预览（dryRun）已开启：确认后只提交 Host 预检作业，任务中心会标注「预览（dryRun）· 未执行写操作」。
        </p>
        <p v-else class="safe-hint">确认后提交到 Host 作业队列；执行进度、取消与逐仓结果见任务中心。</p>
      </div>

      <footer class="dialog-actions">
        <button type="button" class="btn" data-testid="batch-dialog-cancel" @click="w.closeBatchDialog()">取消</button>
        <button
          type="button"
          class="btn"
          :class="[dialog.danger ? 'danger' : 'primary', { disabled: !canSubmit }]"
          data-testid="batch-dialog-confirm"
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
.batch-dialog {
  width: min(500px, 94vw);
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
.dryrun-badge {
  font-size: 9px;
  font-weight: 700;
  padding: 1px 7px;
  border-radius: 999px;
  border: 1px solid var(--st-warning-bd);
  background: var(--st-warning-bg);
  color: var(--st-warning-fg);
  white-space: nowrap;
}
.spacer { flex: 1; }
.icon-btn { border: 0; background: transparent; color: var(--text-2); font-size: 13px; cursor: pointer; }
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
.preview { margin: 8px 0 4px; border: 1px solid var(--border); border-radius: var(--r-panel); background: var(--surface-2); overflow: hidden; }
.preview-head { display: flex; align-items: center; justify-content: space-between; padding: 6px 10px; font-size: 11px; font-weight: 650; border-bottom: 1px solid var(--border); }
.preview-count { font-variant-numeric: tabular-nums; color: var(--text-2); }
.preview-list { list-style: none; margin: 0; padding: 4px 0; max-height: 180px; overflow-y: auto; }
.preview-item { display: flex; align-items: center; justify-content: space-between; gap: 8px; padding: 2px 10px; font-size: 11px; }
.preview-type { color: var(--st-info-fg); font-size: 10px; white-space: nowrap; }
.form { display: flex; flex-direction: column; gap: 6px; margin-top: 8px; }
.form-label { font-size: 11px; font-weight: 600; color: var(--text); display: flex; flex-wrap: wrap; gap: 6px; align-items: baseline; }
.form-label .hint { font-weight: 400; color: var(--text-2); font-size: 10px; }
.required { color: var(--st-danger-fg); }
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
.form-input:focus { border-color: var(--focus); box-shadow: 0 0 0 2px color-mix(in srgb, var(--focus) 24%, transparent); }
.input-hint { margin: 0; font-size: 10px; line-height: 1.5; color: var(--text-2); }
.input-hint.invalid { color: var(--st-danger-fg); }
.dry-run { display: inline-flex; align-items: center; gap: 6px; margin-top: 10px; font-size: 12px; cursor: pointer; }
.dry-run input { accent-color: var(--focus); }
.dry-run-hint { margin: 4px 0 0; font-size: 10px; line-height: 1.5; color: var(--text-2); }
.dry-run-notice {
  margin: 10px 0 0;
  padding: 6px 8px;
  border: 1px dashed var(--st-warning-bd);
  border-radius: 4px;
  background: color-mix(in srgb, var(--st-warning-bg) 40%, var(--surface));
  color: var(--st-warning-fg);
  font-size: 10px;
  line-height: 1.6;
}
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
