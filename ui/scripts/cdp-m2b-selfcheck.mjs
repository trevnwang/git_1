/**
 * M2b 任务中心 + 批量入口 CDP 自检（真实 WebView2 页面）。
 *
 * 覆盖：任务中心打开 / 进行中与历史分段 / 进度格式与推进 / 取消 / 逐仓 items
 * （状态、错误码、backupRef、preHead→postHead）/ 仅重试失败项 / 批量确认 0 写 /
 * 提交后 toast jobId 并自动打开任务中心 / CREATE 输入校验 / CLONE 缺失仓预览 /
 * DELETE 确认 /「关闭 Mock 时真实 Host 优先」（getJobs 走 Host，不伪造 Mock 作业）。
 *
 * 实现方式：用 `Page.addScriptToEvaluateOnNewDocument` 注入 `window.__DSH_FORCE_MOCK__=true`
 * 让同一 WebView2 页面走内置 Mock 演示作业（不触达 Host 写接口），最后移除注入脚本并 reload
 * 验证真实 Host 优先，页面最终停留在真实 Host 模式。
 *
 * 前置：Host 已启动并开启 CDP 9222（WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--remote-debugging-port=9222）。
 * 用法：node ui/scripts/cdp-m2b-selfcheck.mjs
 * 截图输出：artifacts/m2b-selfcheck/（gitignored 临时目录）
 */
import { mkdirSync, writeFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const here = dirname(fileURLToPath(import.meta.url))
const outDir = join(here, '..', '..', 'artifacts', 'm2b-selfcheck')
mkdirSync(outDir, { recursive: true })

const results = []
function check(id, ok, detail = '') {
  results.push({ id, ok: Boolean(ok), detail })
  console.log(`${ok ? 'PASS' : 'FAIL'} ${id}${detail ? ' :: ' + detail : ''}`)
}
const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms))

const targets = await fetch('http://127.0.0.1:9222/json/list').then((r) => r.json())
if (!targets.length) throw new Error('CDP 无页面：请确认 Host 已开启 --remote-debugging-port=9222')
const target = targets.find((item) => item.type === 'page') ?? targets[0]
const ws = new WebSocket(target.webSocketDebuggerUrl)
await new Promise((resolve, reject) => {
  ws.addEventListener('open', resolve)
  ws.addEventListener('error', reject)
})
let seq = 0
const pending = new Map()
ws.addEventListener('message', (event) => {
  const message = JSON.parse(event.data)
  if (message.id && pending.has(message.id)) {
    pending.get(message.id)(message)
    pending.delete(message.id)
  }
})
const send = (method, params = {}) =>
  new Promise((resolve) => {
    const id = ++seq
    pending.set(id, resolve)
    ws.send(JSON.stringify({ id, method, params }))
  })

async function evaluate(expression) {
  const response = await send('Runtime.evaluate', { expression, returnByValue: true, awaitPromise: true })
  const details = response.result?.exceptionDetails
  if (details) throw new Error(`evaluate 异常：${details.exception?.description ?? details.text}`)
  return response.result?.result?.value
}
async function waitFor(expression, timeoutMs = 12_000) {
  const deadline = Date.now() + timeoutMs
  while (Date.now() < deadline) {
    try {
      if (await evaluate(expression)) return true
    } catch {
      /* 页面 reload 期间执行上下文销毁，重试 */
    }
    await sleep(200)
  }
  return false
}
async function point(selector) {
  return evaluate(
    `(() => { const e = document.querySelector(${JSON.stringify(selector)}); if (!e) return null; const r = e.getBoundingClientRect(); return { x: r.x + r.width / 2, y: r.y + r.height / 2 }; })()`,
  )
}
async function click(selector) {
  const at = await point(selector)
  if (!at) throw new Error(`未找到可点击元素：${selector}`)
  await send('Input.dispatchMouseEvent', { type: 'mousePressed', x: at.x, y: at.y, button: 'left', clickCount: 1 })
  await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x: at.x, y: at.y, button: 'left', clickCount: 1 })
}
const text = (selector) =>
  evaluate(`document.querySelector(${JSON.stringify(selector)})?.innerText?.replace(/\\s+/g, ' ').trim() ?? ''`)
const allToasts = () =>
  evaluate(
    `[...document.querySelectorAll('[data-testid=toast]')].map((n) => n.innerText.replace(/\\s+/g, ' ').trim()).join(' | ')`,
  )
const count = (selector) => evaluate(`document.querySelectorAll(${JSON.stringify(selector)}).length`)
async function screenshot(name) {
  const shot = await send('Page.captureScreenshot', { format: 'png' })
  const data = shot.result?.data
  if (!data) return
  writeFileSync(join(outDir, name), Buffer.from(data, 'base64'))
}
async function addScript(source) {
  const result = await send('Page.addScriptToEvaluateOnNewDocument', { source })
  return result.result?.identifier ?? ''
}
async function removeScript(identifier) {
  if (!identifier) return
  await send('Page.removeScriptToEvaluateOnNewDocument', { identifier })
}
async function reloadApp() {
  await send('Page.reload', { ignoreCache: true })
  const ready = await waitFor(`!!document.querySelector('[data-testid=component-list-count]')`, 20_000)
  await sleep(1_200)
  return ready
}
async function taskCounts() {
  const title = await evaluate(`document.querySelector('[data-testid=title-tasks]')?.title ?? ''`)
  const match = String(title).match(/进行中 (\d+) · 历史 (\d+)/)
  return match ? { active: Number(match[1]), history: Number(match[2]) } : { active: -1, history: -1 }
}
async function closeDrawer() {
  const at = await point('[data-testid=drawer] .drawer-head button')
  if (at) await click('[data-testid=drawer] .drawer-head button')
  await sleep(250)
}

// 注入脚本：强制 Mock + 包裹 chrome.webview.postMessage 统计 Host 方法调用（真实 Host 阶段用）。
const WRAP = `
  window.__dshCalls = window.__dshCalls || {};
  function __dshWrap() {
    try {
      var wv = window.chrome && window.chrome.webview;
      if (!wv || wv.__dshWrapped) return;
      var original = wv.postMessage.bind(wv);
      wv.postMessage = function (message) {
        try {
          var data = typeof message === 'string' ? JSON.parse(message) : message;
          if (data && data.method) window.__dshCalls[data.method] = (window.__dshCalls[data.method] || 0) + 1;
        } catch (e) {}
        return original(message);
      };
      wv.__dshWrapped = true;
    } catch (e) {}
  }
  __dshWrap();
  document.addEventListener('DOMContentLoaded', __dshWrap, { once: true });
`
const MOCK_SCRIPT = `window.__DSH_FORCE_MOCK__ = true;\n${WRAP}`
const COUNTER_SCRIPT = WRAP

await send('Page.enable')
await send('Runtime.enable')

const mockScriptId = await addScript(MOCK_SCRIPT)
const counterScriptId = await addScript(COUNTER_SCRIPT)

try {
  // ═════════ A. 强制 Mock：任务中心演示 + 批量入口全链路（0 Host 写） ═════════
  const booted = await reloadApp()
  check('m2b.mock-boot', booted, booted ? '' : '页面未在 20s 内就绪')
  check('m2b.mock-source', (await text('[data-testid=snapshot-source]')).includes('Mock'))
  const queueInitial = Number((await text('[data-testid=status-queue]')).replace(/\D/g, ''))
  check('m2b.queue-running', queueInitial >= 1, `队列=${queueInitial}`)

  // 1) 任务中心打开 + 两段（进行中/历史）
  await click('[data-testid=title-tasks]')
  const drawerOpen = await waitFor(`!!document.querySelector('[data-testid=task-center]')`, 5_000)
  check('task.center-open', drawerOpen)
  await sleep(500)
  check('task.source-mock', (await text('[data-testid=task-source]')).includes('Mock'))
  const seedActive = await count('[data-testid=task-job][data-active="1"]')
  const seedCounts = await taskCounts()
  check(
    'task.seed-two-jobs',
    seedActive >= 1 && seedCounts.history >= 1,
    `进行中=${seedActive} · title(${JSON.stringify(seedCounts)})`,
  )
  const progressSeed = await text('[data-testid=task-job][data-active="1"] [data-testid=task-progress]')
  check('task.progress-format', /^\d+\/\d+$/.test(progressSeed), progressSeed)

  // 2) 展开逐仓 items
  await click('[data-testid=task-job][data-active="1"] [data-testid=task-expand]')
  await sleep(250)
  const seedItems = await count('[data-testid=task-job][data-active="1"] [data-testid=task-item]')
  check('task.items-visible', seedItems >= 4, `items=${seedItems}`)
  await screenshot('01-task-center-running.png')

  // 3) 取消进行中作业（合作式；已开始仓取消，未开始仓保持）
  const runningJobId = await evaluate(
    `document.querySelector('[data-testid=task-job][data-active="1"]')?.getAttribute('data-job-id') ?? ''`,
  )
  await click(`[data-testid=task-job][data-job-id="${runningJobId}"] [data-testid=task-cancel]`)
  await sleep(800)
  const canceledState = await evaluate(
    `document.querySelector('[data-testid=task-job][data-job-id="${runningJobId}"]')?.getAttribute('data-state') ?? ''`,
  )
  check('task.cancel', canceledState === 'CANCELED', `state=${canceledState}`)
  const queueAfterCancel = Number((await text('[data-testid=status-queue]')).replace(/\D/g, ''))
  check('task.queue-updated', queueAfterCancel < queueInitial, `${queueInitial} => ${queueAfterCancel}`)
  await screenshot('02-task-cancelled.png')

  // 4) 历史分段：部分失败逐仓明细（错误码 / backupRef / preHead→postHead）
  await click('[data-testid=task-segment-history]')
  await sleep(300)
  const partialJobId = await evaluate(
    `document.querySelector('[data-testid=task-job][data-state="PARTIAL"]')?.getAttribute('data-job-id') ?? ''`,
  )
  check('task.partial-history', Boolean(partialJobId), partialJobId)
  await click(`[data-testid=task-job][data-job-id="${partialJobId}"] [data-testid=task-expand]`)
  await sleep(300)
  const errorCodes = await evaluate(
    `[...document.querySelectorAll('[data-testid=task-job][data-job-id="${partialJobId}"] [data-testid=task-item-error]')].map((n) => n.getAttribute('data-error-code'))`,
  )
  check(
    'task.item-error-codes',
    Array.isArray(errorCodes) && errorCodes.includes('E_DIRTY_WORKTREE') && errorCodes.includes('E_OFFLINE'),
    JSON.stringify(errorCodes),
  )
  const backupRef = await text(`[data-testid=task-job][data-job-id="${partialJobId}"] [data-testid=task-backup-ref]`)
  check('task.backup-ref', backupRef.startsWith('refs/backup/'), backupRef)
  const heads = await text(`[data-testid=task-job][data-job-id="${partialJobId}"] [data-testid=task-item-heads]`)
  check('task.heads', heads.includes('→'), heads)
  await screenshot('03-task-partial-history.png')

  // 5) 仅重试失败项（新作业只含失败/取消项）
  const countsBeforeRetry = await taskCounts()
  await click(`[data-testid=task-job][data-job-id="${partialJobId}"] [data-testid=task-retry-failed]`)
  await sleep(900)
  const retryToast = await allToasts()
  check('task.retry-toast', retryToast.includes('重试作业'), retryToast.slice(-140))
  const retryJob = await evaluate(
    `(() => { const row = document.querySelector('[data-testid=task-job][data-active="1"]'); if (!row) return null; return { id: row.getAttribute('data-job-id'), state: row.getAttribute('data-state'), items: row.querySelectorAll('[data-testid=task-item]').length }; })()`,
  )
  check('task.retry-new-job', Boolean(retryJob?.id), JSON.stringify(retryJob))
  const countsAfterRetry = await taskCounts()
  check(
    'task.retry-count',
    countsAfterRetry.active + countsAfterRetry.history === countsBeforeRetry.active + countsBeforeRetry.history + 1,
    `${JSON.stringify(countsBeforeRetry)} => ${JSON.stringify(countsAfterRetry)}`,
  )
  await screenshot('04-task-retry-failed.png')

  // 6) 批量 SYNC：确认对话框 + 未确认 0 写
  await closeDrawer()
  const countsBeforeBatch = await taskCounts()
  await click('[data-testid=batch-select-toggle]')
  await sleep(250)
  await evaluate(`[...document.querySelectorAll('.batch-bar button')].find((b) => b.title === '全选筛选结果')?.click()`)
  await sleep(250)
  await click('[data-testid=batch-sync]')
  const syncDialogOpen = await waitFor(`!!document.querySelector('[data-testid=confirm-dialog]')`, 4_000)
  const syncDialogText = await text('[data-testid=confirm-dialog]')
  check('batch.sync-confirm', syncDialogOpen && syncDialogText.includes('SYNC'), syncDialogText.slice(0, 140))
  await screenshot('05-batch-sync-confirm.png')
  const countsDuringConfirm = await taskCounts()
  check(
    'batch.no-write-before-confirm',
    countsDuringConfirm.active === countsBeforeBatch.active && countsDuringConfirm.history === countsBeforeBatch.history,
    `${JSON.stringify(countsBeforeBatch)} => ${JSON.stringify(countsDuringConfirm)}`,
  )
  const hostCallsBefore = await evaluate(`JSON.stringify(window.__dshCalls ?? {})`)
  check('batch.no-host-write-before-confirm', !String(hostCallsBefore).includes('runBatch'), hostCallsBefore)
  await click('[data-testid=confirm-dialog] .confirm-actions .btn:first-child')
  await sleep(300)
  const countsAfterCancel = await taskCounts()
  check(
    'batch.cancel-no-write',
    countsAfterCancel.active === countsBeforeBatch.active && countsAfterCancel.history === countsBeforeBatch.history,
    JSON.stringify(countsAfterCancel),
  )

  // 7) 确认提交 → toast jobId + 自动打开任务中心 + 生成作业
  await click('[data-testid=batch-sync]')
  await waitFor(`!!document.querySelector('[data-testid=confirm-dialog]')`, 4_000)
  await click('[data-testid=confirm-dialog] .confirm-actions .btn:last-child')
  await sleep(1_000)
  const submitToast = await allToasts()
  check(
    'batch.submit-toast-jobid',
    submitToast.includes('已提交批量 SYNC') && submitToast.includes('job'),
    submitToast.slice(-160),
  )
  check('batch.taskcenter-opened', await evaluate(`!!document.querySelector('[data-testid=task-center]')`))
  const countsAfterSubmit = await taskCounts()
  check(
    'batch.submit-creates-job',
    countsAfterSubmit.active + countsAfterSubmit.history ===
      countsBeforeBatch.active + countsBeforeBatch.history + 1,
    `${JSON.stringify(countsBeforeBatch)} => ${JSON.stringify(countsAfterSubmit)}`,
  )
  await screenshot('06-batch-submitted-taskcenter.png')

  // 8) 进度推进（自动刷新/事件驱动）
  const progressA = await text('[data-testid=task-job][data-active="1"] [data-testid=task-progress]')
  await sleep(2_800)
  const progressB = await text('[data-testid=task-job][data-active="1"] [data-testid=task-progress]')
  const done = (value) => Number(String(value).split('/')[0] ?? 0)
  check('task.progress-advances', done(progressB) > done(progressA), `${progressA} -> ${progressB}`)

  // 9) CREATE 输入校验（未输入不可提交；取消 0 写）
  await closeDrawer()
  const countsBeforeCreate = await taskCounts()
  await click('[data-testid=batch-create]')
  await waitFor(`!!document.querySelector('[data-testid=batch-dialog]')`, 4_000)
  const createDisabled = await evaluate(`document.querySelector('[data-testid=batch-dialog-confirm]')?.disabled === true`)
  check('batch.create-input-required', createDisabled)
  await click('[data-testid=batch-dialog-input]')
  await send('Input.insertText', { text: 'release/1.2' })
  await sleep(300)
  const createEnabled = await evaluate(`document.querySelector('[data-testid=batch-dialog-confirm]')?.disabled === false`)
  check('batch.create-input-valid', createEnabled)
  await screenshot('07-batch-create-dialog.png')
  await click('[data-testid=batch-dialog-cancel]')
  await sleep(300)
  const countsAfterCreateCancel = await taskCounts()
  check(
    'batch.create-cancel-no-write',
    countsAfterCreateCancel.active + countsAfterCreateCancel.history ===
      countsBeforeCreate.active + countsBeforeCreate.history,
    JSON.stringify(countsAfterCreateCancel),
  )

  // 10) CLONE 预览缺失仓 + DELETE 确认（均不提交）
  await click('[data-testid=batch-clone]')
  await waitFor(`!!document.querySelector('[data-testid=batch-dialog-preview]')`, 4_000)
  const clonePreview = await count('[data-testid=batch-dialog-preview-item]')
  check('batch.clone-preview', clonePreview >= 1, `缺失仓=${clonePreview}`)
  await screenshot('08-batch-clone-preview.png')
  await click('[data-testid=batch-dialog-cancel]')
  await sleep(250)
  await click('[data-testid=batch-delete]')
  await waitFor(`!!document.querySelector('[data-testid=confirm-dialog]')`, 4_000)
  const deleteDialogText = await text('[data-testid=confirm-dialog]')
  check('batch.delete-confirm', deleteDialogText.includes('Delete'), deleteDialogText.slice(0, 110))
  await click('[data-testid=confirm-dialog] .confirm-actions .btn:first-child')
  await sleep(250)
  await evaluate(`document.querySelector('.batch-bar .close-btn')?.click()`)

  // 11) RESTORE（从 .trash）确认入口：菜单触发确认，未确认 0 写
  const countsBeforeRestore = await taskCounts()
  await click('[data-testid=menu-actions]')
  await sleep(200)
  await click('[data-testid="menu-item-component.RESTORE"]')
  await waitFor(`!!document.querySelector('[data-testid=confirm-dialog]')`, 4_000)
  const restoreDialogText = await text('[data-testid=confirm-dialog]')
  check('batch.restore-confirm', restoreDialogText.includes('恢复'), restoreDialogText.slice(0, 110))
  await click('[data-testid=confirm-dialog] .confirm-actions .btn:first-child')
  await sleep(250)
  const countsAfterRestoreCancel = await taskCounts()
  check(
    'batch.restore-cancel-no-write',
    countsAfterRestoreCancel.active + countsAfterRestoreCancel.history ===
      countsBeforeRestore.active + countsBeforeRestore.history,
    JSON.stringify(countsAfterRestoreCancel),
  )

  // ═════════ B. 移除强制 Mock：真实 Host 优先（不伪造 Mock 作业） ═════════
  await removeScript(mockScriptId)
  const realBooted = await reloadApp()
  check('m2b.real-host-boot', realBooted)
  await click('[data-testid=title-tasks]')
  await waitFor(`!!document.querySelector('[data-testid=task-center]')`, 5_000)
  await sleep(1_200)
  const hostSource = await text('[data-testid=task-source]')
  check('m2b.host-source', hostSource.includes('Host'), hostSource)
  const hostCalls = await evaluate(`JSON.stringify(window.__dshCalls ?? {})`)
  check('m2b.host-getjobs-called', String(hostCalls).includes('getJobs'), hostCalls)
  const hostJobRows = await count('[data-testid=task-job]')
  const hostError = await text('[data-testid=task-error]')
  check('m2b.host-no-fake-mock-jobs', hostError ? hostJobRows === 0 : true, `rows=${hostJobRows} error=${hostError.slice(0, 80)}`)
  await screenshot('09-real-host-taskcenter.png')
  await closeDrawer()
} finally {
  // 清理注入脚本并 reload，页面最终停留在真实 Host 模式。
  await removeScript(mockScriptId)
  await removeScript(counterScriptId)
  try {
    await send('Page.reload', { ignoreCache: true })
    await sleep(1_500)
  } catch {
    /* 清理失败不改变断言结果 */
  }
  ws.close()
}

const failed = results.filter((item) => !item.ok)
const summary = {
  total: results.length,
  passed: results.length - failed.length,
  failed: failed.map((item) => item.id),
  screenshots: outDir,
  checks: results,
}
writeFileSync(join(outDir, 'result.json'), JSON.stringify(summary, null, 2))
console.log(JSON.stringify({ total: summary.total, passed: summary.passed, failed: summary.failed, screenshots: outDir }, null, 2))
process.exit(failed.length === 0 ? 0 : 1)
