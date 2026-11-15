/**
 * M2b-1 UX 修复 CDP 自检（P0-2 任务中心分段 / P0-1 取消展示 / P1-1 dryRun 徽标 / P1-2 批量入口 /
 * P1-3 0 选择禁用 / 焦点陷阱 / JOB_UPDATED 新信封兼容）。
 *
 * 两段式：
 *  A. 强制 Mock（`window.__DSH_FORCE_MOCK__=true`，不触达真实 Host）：dryRun 提交/徽标、0 选择禁用、入口清单、取消展示。
 *  B. 真实 Host（当前有 job 历史）：历史 dryRun/取消展示、真实只读 FETCH 的进行中可见+取消+仅重试失败项、
 *     抽屉/批量对话框焦点、三入口动作清单一致、JOB_UPDATED 新信封（jobId+current）/旧 data 形状兼容与分段不劫持。
 *
 * 真实写操作仅：只读 `FETCH`（不修改工作区/本地分支）与 `CREATE dryRun`/取消（不写仓）。
 * 前置：Host 已启动并开启 CDP 9222；构建产物已复制到 Host 输出目录（ui/dist → bin/.../ui）。
 * 用法：node ui/scripts/cdp-m2b1-fix-selfcheck.mjs
 * 截图/结果：artifacts/m2b1-fix-selfcheck/
 */
import { mkdirSync, writeFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const here = dirname(fileURLToPath(import.meta.url))
const outDir = join(here, '..', '..', 'artifacts', 'm2b1-fix-selfcheck')
mkdirSync(outDir, { recursive: true })

const results = []
function check(id, ok, detail = '') {
  const entry = { id, ok: Boolean(ok), detail: String(detail).slice(0, 400) }
  results.push(entry)
  console.log(`${entry.ok ? 'PASS' : 'FAIL'} ${id}${detail ? ' :: ' + entry.detail : ''}`)
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
const pageErrors = []
ws.addEventListener('message', (event) => {
  const message = JSON.parse(event.data)
  if (message.method === 'Runtime.exceptionThrown') {
    pageErrors.push(message.params.exceptionDetails?.exception?.description ?? message.params.exceptionDetails?.text)
  }
  if (message.method === 'Runtime.consoleAPICalled' && message.params.type === 'error') {
    pageErrors.push(message.params.args.map((arg) => arg.value ?? arg.description).join(' '))
  }
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
async function waitFor(expression, timeoutMs = 15_000) {
  const deadline = Date.now() + timeoutMs
  while (Date.now() < deadline) {
    try {
      if (await evaluate(expression)) return true
    } catch {
      /* reload 期间执行上下文销毁，重试 */
    }
    await sleep(150)
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
async function key(keyName, code, keyCode, modifiers = 0) {
  await send('Input.dispatchKeyEvent', { type: 'rawKeyDown', key: keyName, code, windowsVirtualKeyCode: keyCode, modifiers })
  await send('Input.dispatchKeyEvent', { type: 'keyUp', key: keyName, code, windowsVirtualKeyCode: keyCode, modifiers })
}
const tab = (shift = false) => key('Tab', 'Tab', 9, shift ? 8 : 0)
const escape = () => key('Escape', 'Escape', 27)
const text = (selector) =>
  evaluate(`document.querySelector(${JSON.stringify(selector)})?.innerText?.replace(/\\s+/g, ' ').trim() ?? ''`)
const count = (selector) => evaluate(`document.querySelectorAll(${JSON.stringify(selector)}).length`)
const allToasts = () =>
  evaluate(
    `[...document.querySelectorAll('[data-testid=toast]')].map((n) => n.innerText.replace(/\\s+/g, ' ').trim()).join(' | ')`,
  )
async function screenshot(name) {
  const shot = await send('Page.captureScreenshot', { format: 'png' })
  if (shot.result?.data) writeFileSync(join(outDir, name), Buffer.from(shot.result.data, 'base64'))
}
async function addScript(source) {
  const result = await send('Page.addScriptToEvaluateOnNewDocument', { source })
  return result.result?.identifier ?? ''
}
async function removeScript(identifier) {
  if (identifier) await send('Page.removeScriptToEvaluateOnNewDocument', { identifier })
}
async function reloadApp() {
  await send('Page.reload', { ignoreCache: true })
  const ready = await waitFor(`!!document.querySelector('[data-testid=component-list-count]')`, 25_000)
  await sleep(1_200)
  return ready
}
async function openTaskCenter() {
  await click('[data-testid=title-tasks]')
  return waitFor(`!!document.querySelector('[data-testid=task-center]')`, 5_000)
}
async function closeDrawerByEsc() {
  await escape()
  await sleep(350)
}
async function segmentActive() {
  return evaluate(`document.querySelector('[data-testid=task-segment-active]')?.getAttribute('aria-selected') === 'true'`)
}
async function jobIds(selector = '[data-testid=task-job]') {
  return evaluate(`[...document.querySelectorAll(${JSON.stringify(selector)})].map((n) => n.getAttribute('data-job-id'))`)
}
/** 确保处于批量选择模式且已全选（Esc 可能清空选择模式）。 */
async function ensureSelectionAll() {
  if (!(await evaluate(`!!document.querySelector('[data-testid=batch-select-all]')`))) {
    await click('[data-testid=batch-select-toggle]')
    await sleep(250)
  }
  await click('[data-testid=batch-select-all]')
  await sleep(250)
}
/** 确保指定 job 已展开逐仓结果（focusJob 会自动展开，避免重复点击反而收起）。 */
async function ensureJobExpanded(jobId) {
  const expanded = await evaluate(
    `document.querySelector('[data-testid=task-job][data-job-id="${jobId}"] [data-testid=task-expand]')?.getAttribute('aria-expanded')`,
  )
  if (expanded !== 'true') {
    await click(`[data-testid=task-job][data-job-id="${jobId}"] [data-testid=task-expand]`)
    await sleep(300)
  }
}

/** 轮询直到 predicate 返回真值（Host 模式每 1.5s 会 getJobs 覆盖列表，注入的假作业可能被清掉）。 */
async function pollUntil(predicate, attempts = 20, gapMs = 120) {
  for (let i = 0; i < attempts; i += 1) {
    const value = await predicate()
    if (value) return value
    await sleep(gapMs)
  }
  return null
}
/** 页内注入 JOB_UPDATED 事件（不写 Host），可选随后点选该行以展开明细。 */
async function injectJobUpdate(envelope, selectJobId = '') {
  await evaluate(
    `(async () => {
      window.__dshCaptured.forEach((listener) => listener({ data: JSON.stringify(${JSON.stringify(envelope)}) }));
      await new Promise((resolve) => setTimeout(resolve, 40));
      ${selectJobId ? `document.querySelector('[data-testid=task-job][data-job-id="${selectJobId}"]')?.click();` : ''}
    })()`,
  )
}

// 注入脚本：强制 Mock + 捕获 JOB_UPDATED 监听器（页内注入事件，不写 Host）。
const CAPTURE_SCRIPT = `
window.__dshCaptured = [];
(function () {
  function wrap() {
    try {
      var wv = window.chrome && window.chrome.webview;
      if (!wv || wv.__dshCapWrapped) return;
      var original = wv.addEventListener.bind(wv);
      wv.addEventListener = function (type, listener) {
        if (type === 'message') window.__dshCaptured.push(listener);
        return original(type, listener);
      };
      wv.__dshCapWrapped = true;
    } catch (e) {}
  }
  wrap();
  document.addEventListener('DOMContentLoaded', wrap, { once: true });
})();
`
const MOCK_SCRIPT = `window.__DSH_FORCE_MOCK__ = true;\n${CAPTURE_SCRIPT}`

await send('Page.enable')
await send('Runtime.enable')

const mockScriptId = await addScript(MOCK_SCRIPT)
const captureScriptId = await addScript(CAPTURE_SCRIPT)

try {
  // ═════════════════ A. 强制 Mock：dryRun 提交/徽标、0 选择禁用、入口清单、取消展示 ═════════════════
  const mockBooted = await reloadApp()
  check('mock.boot', mockBooted)
  check('mock.source', (await text('[data-testid=snapshot-source]')).includes('Mock'))
  await click('[data-testid=batch-select-toggle]')
  await sleep(250)
  await click('[data-testid=batch-select-all]')
  await sleep(250)
  const mockChecked = await text('[data-testid=batch-count]')
  check('batch.select-all', /^\d+\/\d+$/.test(mockChecked) && !mockChecked.startsWith('0/'), mockChecked)

  // 1) 批量入口：更多菜单清单（PULL/FETCH/CHKOUT/TAG/RESTORE + COMMIT/PUSH 显式禁用）
  await click('[data-testid=batch-more]')
  await sleep(300)
  const moreItems = await evaluate(`[...document.querySelectorAll('[data-testid=batch-more-menu] button')].map((n) => ({
    testid: n.getAttribute('data-testid'), label: n.innerText.replace(/\\s+/g, ' ').trim(), disabled: n.disabled,
  }))`)
  check(
    'batch.more-kinds',
    ['batch-more-pull', 'batch-more-fetch', 'batch-more-chkout', 'batch-more-restore'].every((id) =>
    moreItems.some((item) => item.testid === id && !item.disabled),
  ) && !moreItems.some((item) => item.testid === 'batch-more-tag' && !item.disabled),
    JSON.stringify(moreItems.map((item) => item.testid)),
  )
  check(
    'batch.more-unimplemented-disabled',
    moreItems.filter((item) => item.testid === 'batch-more-commit' || item.testid === 'batch-more-push').every((item) => item.disabled),
    JSON.stringify(moreItems.filter((item) => /commit|push/.test(item.testid))),
  )
  await screenshot('01-batch-more-menu.png')
  await click('[data-testid=batch-more]')
  await sleep(200)

  // 2) dryRun 预览：对话框徽标 + 提交 Toast + 任务中心“未执行写操作”（不写仓）
  await click('[data-testid=batch-create]')
  await waitFor(`!!document.querySelector('[data-testid=batch-dialog]')`, 4_000)
  await click('[data-testid=batch-dialog-input]')
  await send('Input.insertText', { text: 'review/m2b1-fix-dryrun' })
  await click('[data-testid=batch-dialog-dryrun]')
  await sleep(250)
  const dryBadge = await text('[data-testid=batch-dialog-dryrun-badge]')
  const dryNotice = await text('[data-testid=batch-dialog-dryrun-notice]')
  check('dryrun.dialog-badge', /预览（dryRun）/.test(dryBadge), dryBadge)
  check('dryrun.dialog-notice', /未执行写操作/.test(dryNotice), dryNotice)
  await screenshot('02-batch-dryrun-dialog.png')
  await click('[data-testid=batch-dialog-confirm]')
  await waitFor(`!!document.querySelector('[data-testid=task-center]')`, 6_000)
  await sleep(500)
  const dryToast = await allToasts()
  check('dryrun.submit-toast', /dryRun/.test(dryToast) && /未执行任何写操作/.test(dryToast), dryToast.slice(-220))
  const dryjob = await evaluate(`(() => {
    const row = [...document.querySelectorAll('[data-testid=task-job][data-dryrun="1"]')].pop();
    if (!row) return null;
    return {
      id: row.getAttribute('data-job-id'),
      badge: row.querySelector('[data-testid=task-dryrun-badge]')?.innerText ?? '',
      state: row.querySelector('[data-testid=task-state]')?.innerText ?? '',
      progress: row.querySelector('[data-testid=task-progress]')?.innerText ?? '',
      note: row.querySelector('[data-testid=task-dryrun-note]')?.innerText ?? '',
    };
  })()`)
  check('dryrun.job-badge', Boolean(dryjob && /预览（dryRun）/.test(dryjob.badge)), JSON.stringify(dryjob))
  check('dryrun.job-state', Boolean(dryjob && dryjob.state.includes('预览完成')), dryjob?.state)
  check('dryrun.job-progress', Boolean(dryjob && dryjob.progress === '未执行写操作'), dryjob?.progress)
  check('dryrun.job-note', Boolean(dryjob && /未执行任何写操作/.test(dryjob.note)), dryjob?.note)
  await screenshot('03-task-dryrun.png')

  // 3) 取消展示：CANCELED 项无 E_TIMEOUT，提示“已取消（未开始）”
  await click('[data-testid=task-segment-active]')
  await sleep(300)
  const runningId = (await jobIds('[data-testid=task-job][data-active="1"]'))[0] ?? ''
  if (runningId) {
    await click(`[data-testid=task-job][data-job-id="${runningId}"] [data-testid=task-cancel]`)
    await sleep(900)
  }
  check('cancel.running-visible', Boolean(runningId), runningId)
  check(
    'cancel.state',
    (await evaluate(`document.querySelector('[data-testid=task-job][data-job-id="${runningId}"]')?.getAttribute('data-state')`)) === 'CANCELED',
  )
  await ensureJobExpanded(runningId)
  const mockTimeoutErrors = await evaluate(
    `[...document.querySelectorAll('[data-testid=task-job][data-job-id="${runningId}"] [data-testid=task-item-error]')].filter((n) => n.getAttribute('data-error-code') === 'E_TIMEOUT').length`,
  )
  check('cancel.no-e-timeout', mockTimeoutErrors === 0, `E_TIMEOUT=${mockTimeoutErrors}`)
  check(
    'cancel.item-note',
    (await evaluate(`document.querySelectorAll('[data-testid=task-job][data-job-id="${runningId}"] [data-testid=task-item-canceled]').length`)) > 0,
  )
  await screenshot('04-task-canceled.png')
  await closeDrawerByEsc()

  // 4) 0 勾选：批量命令禁用并提示，不隐式作用于当前组件
  await evaluate(`document.querySelector('[data-testid=batch-select-toggle].close-btn')?.click()`)
  await sleep(250)
  await click('[data-testid=batch-select-toggle]')
  await sleep(300)
  check(
    'selection.zero-hint',
    /先勾选组件/.test(
      (await evaluate(`document.querySelector('[data-testid=batch-blocked-hint]')?.getAttribute('title') ?? ''`)) || '',
    ),
  )
  await click('[data-testid=batch-sync]')
  await sleep(400)
  const zeroToast = await allToasts()
  check('selection.zero-toast', /先勾选组件/.test(zeroToast), zeroToast.slice(-160))
  check('selection.zero-no-confirm', (await count('[data-testid=confirm-dialog]')) === 0)
  await click('[data-testid=batch-more]')
  await sleep(300)
  const blockedMore = await evaluate(
    `[...document.querySelectorAll('[data-testid=batch-more-menu] button')].every((n) => n.disabled)`,
  )
  check('selection.zero-more-disabled', blockedMore)
  await escape()
  await sleep(200)
  await evaluate(`document.querySelector('[data-testid=batch-select-toggle].close-btn')?.click()`)

  // ═════════════════ B. 真实 Host：历史 dryRun/取消、真实 FETCH 进行中+取消+重试、焦点、入口一致、事件信封 ═════════════════
  await removeScript(mockScriptId)
  const realBooted = await reloadApp()
  check('real.boot', realBooted)
  check('real.not-forced-mock', (await evaluate('!window.__DSH_FORCE_MOCK__')))
  check('event.listeners-captured', (await evaluate('window.__dshCaptured.length')) > 0)

  // 5) 真实历史：dryRun 徽标 + 取消项不显示 E_TIMEOUT
  await openTaskCenter()
  check('real.source', (await text('[data-testid=task-source]')).includes('Host'), await text('[data-testid=task-source]'))
  await click('[data-testid=task-segment-history]')
  await sleep(600)
  const historySummary = await text('[data-testid=task-summary]')
  check('task.history-present', (await count('[data-testid=task-job]')) > 0, historySummary)
  const realDry = await evaluate(`(() => {
    const row = document.querySelector('[data-testid=task-job][data-dryrun="1"]');
    if (!row) return null;
    return {
      id: row.getAttribute('data-job-id'),
      badge: row.querySelector('[data-testid=task-dryrun-badge]')?.innerText ?? '',
      progress: row.querySelector('[data-testid=task-progress]')?.innerText ?? '',
      note: row.querySelector('[data-testid=task-dryrun-note]')?.innerText ?? '',
    };
  })()`)
  check('task.history-dryrun-badge', !realDry || /预览（dryRun）/.test(realDry.badge), JSON.stringify(realDry))
  check('task.history-dryrun-no-success-0', !realDry || !/^\d+\/\d+$/.test(realDry.progress), realDry?.progress)
  const realCanceledId = await evaluate(
    `document.querySelector('[data-testid=task-job][data-state="CANCELED"]')?.getAttribute('data-job-id') ?? ''`,
  )
  if (realCanceledId) {
    await ensureJobExpanded(realCanceledId)
  }
  const realTimeoutErrors = realCanceledId
    ? await evaluate(
        `[...document.querySelectorAll('[data-testid=task-job][data-job-id="${realCanceledId}"] [data-testid=task-item-error]')].filter((n) => n.getAttribute('data-error-code') === 'E_TIMEOUT').length`,
      )
    : -1
  check('task.history-cancel-no-e-timeout', realTimeoutErrors === 0, `E_TIMEOUT=${realTimeoutErrors}`)
  check(
    'task.history-cancel-item-note',
    realCanceledId
      ? (await evaluate(
          `[...document.querySelectorAll('[data-testid=task-job][data-job-id="${realCanceledId}"] [data-testid=task-item-canceled]')].every((n) => /已取消（未开始）/.test(n.innerText))`,
        ))
      : false,
  )
  await screenshot('05-real-history-dryrun-canceled.png')

  // 6) 焦点：抽屉内焦点 + Tab 不逃逸 + ESC 归还触发控件
  await closeDrawerByEsc()
  await click('[data-testid=title-tasks]')
  await waitFor(`!!document.querySelector('[data-testid=drawer]')`, 5_000)
  await sleep(400)
  check('focus.drawer-initial', await evaluate(`document.querySelector('[data-testid=drawer]')?.contains(document.activeElement)`))
  let escaped = false
  for (let i = 0; i < 14; i += 1) {
    await tab()
    if (!(await evaluate(`document.querySelector('[data-testid=drawer]')?.contains(document.activeElement)`))) {
      escaped = true
      break
    }
  }
  check('focus.drawer-tab-trap', !escaped, await evaluate(`document.activeElement?.getAttribute('data-testid') ?? document.activeElement?.tagName`))
  await tab(true)
  check('focus.drawer-shift-tab', await evaluate(`document.querySelector('[data-testid=drawer]')?.contains(document.activeElement)`))
  await escape()
  await sleep(350)
  check(
    'focus.drawer-esc-return',
    (await evaluate(`!document.querySelector('[data-testid=drawer]')`)) &&
      (await evaluate(`document.activeElement?.getAttribute('data-testid')`)) === 'title-tasks',
    await evaluate(`document.activeElement?.getAttribute('data-testid') ?? document.activeElement?.tagName`),
  )

  // 7) 批量对话框焦点（ConfirmDialog 模式）：焦点在对话框内 + Tab 不逃逸 + ESC 归还
  await click('[data-testid=batch-select-toggle]')
  await sleep(250)
  await click('[data-testid=batch-select-all]')
  await sleep(250)
  await click('[data-testid=batch-create]')
  await waitFor(`!!document.querySelector('[data-testid=batch-dialog]')`, 4_000)
  await sleep(300)
  check('focus.dialog-initial', await evaluate(`document.querySelector('[data-testid=batch-dialog]')?.contains(document.activeElement)`))
  let dialogEscaped = false
  for (let i = 0; i < 8; i += 1) {
    await tab()
    if (!(await evaluate(`document.querySelector('[data-testid=batch-dialog]')?.contains(document.activeElement)`))) {
      dialogEscaped = true
      break
    }
  }
  check('focus.dialog-tab-trap', !dialogEscaped, await evaluate(`document.activeElement?.tagName`))
  await escape()
  await sleep(350)
  check(
    'focus.dialog-esc-return',
    (await evaluate(`!document.querySelector('[data-testid=batch-dialog]')`)) &&
      (await evaluate(`document.activeElement?.getAttribute('data-testid')`)) === 'batch-create',
    await evaluate(`document.activeElement?.getAttribute('data-testid') ?? document.activeElement?.tagName`),
  )

  // 8) 三处入口动作清单一致 + 未实现项禁用
  await click('[data-testid=batch-more]')
  await sleep(300)
  const barKinds = await evaluate(
    `[...document.querySelectorAll('[data-testid=batch-more-menu] button')].map((n) => (n.querySelector('.more-label')?.innerText ?? n.innerText).replace(/\\s+/g, ' ').replace(/…$/, '').trim())`,
  )
  await escape()
  await sleep(200)
  await click('[data-testid=menu-actions]')
  await sleep(250)
  const menuKinds = await evaluate(
    `[...document.querySelectorAll('[data-testid^="menu-item-component."]')].map((n) => n.innerText.replace(/\\s+/g, ' ').replace(/…$/, '').trim())`,
  )
  const menuDisabled = await evaluate(
    `[...document.querySelectorAll('[data-testid^="menu-item-component."]')].filter((n) => n.disabled).map((n) => n.getAttribute('data-testid'))`,
  )
  await escape()
  await sleep(200)
  await evaluate(`(() => { const card = document.querySelector('[data-testid=component-card]'); card?.dispatchEvent(new MouseEvent('contextmenu', { bubbles: true, clientX: 200, clientY: 220 })); })()`)
  await sleep(300)
  const ctxKinds = await evaluate(
    `[...document.querySelectorAll('[data-testid=context-menu] button')].map((n) => n.innerText.replace(/\\s+/g, ' ').replace(/…$/, '').trim()).filter((s) => /批量|Clone|Tag|Restore/.test(s))`,
  )
  const ctxDisabled = await evaluate(
    `[...document.querySelectorAll('[data-testid=context-menu] button')].filter((n) => n.disabled).map((n) => n.innerText.trim())`,
  )
  await escape()
  await sleep(200)
  check('entry.bar-kinds', barKinds.length === 7, JSON.stringify(barKinds))
  check('entry.menu-covers-bar', barKinds.every((label) => menuKinds.includes(label)), JSON.stringify(menuKinds))
  check('entry.ctx-covers-bar', barKinds.every((label) => ctxKinds.includes(label)), JSON.stringify(ctxKinds))
  check(
    'entry.unimplemented-disabled',
    /COMMIT|PUSH/.test(menuDisabled.join(',')) && ctxDisabled.some((label) => /COMMIT|PUSH/.test(label)),
    JSON.stringify({ menuDisabled, ctxDisabled }),
  )
  check('entry.ctx-count', ctxKinds.length >= 11, JSON.stringify(ctxKinds))

  // 9) JOB_UPDATED 新信封（jobId + current:JobDto）兼容 + 旧 data 形状 + 分段不被旧 currentJobId 劫持（P0-2）
  await openTaskCenter()
  await click('[data-testid=task-segment-history]')
  await sleep(500)
  const firstHistoryId = (await jobIds())[0] ?? ''
  if (firstHistoryId) await click(`[data-testid=task-job][data-job-id="${firstHistoryId}"]`)
  await sleep(300)
  check('segment.history-before', (await segmentActive()) === false)
  const fakeJob = {
    id: 'job-evt-selfcheck-1',
    kind: 'FETCH',
    state: 'RUNNING',
    total: 4,
    ok: 1,
    failed: 0,
    skipped: 0,
    createdAt: new Date().toISOString(),
    startedAt: new Date().toISOString(),
    finishedAt: null,
    cancellable: true,
    items: [
      { componentId: 'RTCT_EVT', repoType: 'DESIGN', state: 'OK', errorCode: null, message: null },
      { componentId: 'RTCT_EVT', repoType: 'IMPL', state: 'RUNNING', errorCode: null, message: null },
      { componentId: 'RTCT_EVT', repoType: 'MIL', state: 'PENDING', errorCode: null, message: null },
      { componentId: 'RTCT_EVT', repoType: 'SIL', state: 'PENDING', errorCode: null, message: null },
    ],
  }
  const staleData = { ...fakeJob, ok: 3, items: fakeJob.items.map((item) => ({ ...item, state: 'OK' })) }
  const runningEnvelope = {
    event: 'JOB_UPDATED',
    jobId: fakeJob.id,
    current: fakeJob,
    data: staleData,
    state: 'RUNNING',
    total: 4,
    cancellable: true,
  }
  const newEnvelopeState = await pollUntil(async () => {
    await injectJobUpdate(runningEnvelope)
    await sleep(110)
    const snapshot = await evaluate(`({
      summary: document.querySelector('[data-testid=task-summary]')?.innerText ?? '',
      oldSegment: document.querySelector('[data-testid=task-segment-history]')?.getAttribute('aria-selected'),
      unread: document.querySelector('[data-testid=task-new-active]')?.innerText ?? '',
      activeRows: document.querySelectorAll('[data-testid=task-job][data-active="1"]').length,
    })`)
    return /进行中 1/.test(snapshot.summary) ? snapshot : null
  })
  check('event.new-envelope', Boolean(newEnvelopeState), JSON.stringify(newEnvelopeState))
  check('event.prefer-current', true, '若误用陈旧 data（ok=3）进度会是 3/4；current 提供 1/4')
  check('p0-2.segment-not-hijacked', newEnvelopeState?.oldSegment === 'true' && newEnvelopeState?.activeRows === 0, JSON.stringify(newEnvelopeState))
  check('p0-2.unread-hint', /\d+ 新/.test(newEnvelopeState?.unread ?? ''), newEnvelopeState?.unread)
  await click('[data-testid=task-segment-active]')
  await sleep(200)
  const activeVisible = await pollUntil(async () => {
    await injectJobUpdate(runningEnvelope)
    await sleep(90)
    return evaluate(`(() => {
      const row = document.querySelector('[data-testid=task-job][data-job-id="job-evt-selfcheck-1"]');
      if (!row) return null;
      return { active: row.getAttribute('data-active'), progress: row.querySelector('[data-testid=task-progress]')?.innerText, cancel: !!row.querySelector('[data-testid=task-cancel]') };
    })()`)
  })
  check('p0-2.active-visible', Boolean(activeVisible && activeVisible.active === '1' && activeVisible.cancel), JSON.stringify(activeVisible))
  check('p0-2.progress-from-current', activeVisible?.progress === '1/4', activeVisible?.progress)
  await screenshot('06-task-active-visible.png')
  const canceledJob = {
    ...fakeJob,
    state: 'CANCELED',
    cancellable: false,
    finishedAt: new Date().toISOString(),
    items: fakeJob.items.map((item) => ({ ...item, state: 'CANCELED', errorCode: null, message: '作业取消：未开始执行' })),
  }
  await click('[data-testid=task-segment-history]')
  await sleep(200)
  const cancelledRowId = fakeJob.id
  const oldDataState = await pollUntil(async () => {
    // 旧 data 形状（无 jobId/current）；注入后点选行展开逐仓结果。
    await injectJobUpdate({ event: 'JOB_UPDATED', data: canceledJob }, cancelledRowId)
    await sleep(90)
    return evaluate(`(() => {
      const row = document.querySelector('[data-testid=task-job][data-job-id="${cancelledRowId}"]');
      if (!row) return null;
      return {
        state: row.getAttribute('data-state'),
        cancelNote: [...row.querySelectorAll('[data-testid=task-item-canceled]')].map((n) => n.innerText),
        timeoutErrors: [...row.querySelectorAll('[data-testid=task-item-error]')].filter((n) => n.getAttribute('data-error-code') === 'E_TIMEOUT').length,
      };
    })()`)
  })
  check('event.old-data-envelope', oldDataState?.state === 'CANCELED', JSON.stringify(oldDataState))
  check('event.canceled-no-timeout', oldDataState?.timeoutErrors === 0)
  check(
    'event.canceled-notes',
    (oldDataState?.cancelNote?.length ?? 0) > 0 && oldDataState.cancelNote.every((note) => /已取消（未开始）/.test(note)),
    JSON.stringify(oldDataState?.cancelNote?.slice(0, 2)),
  )

  // 9b) 新后端契约：job.state=PREVIEW + dryRun=true + item.state=PREVIEW（只读预检、零写）
  const previewJob = {
    id: 'job-evt-preview-1',
    kind: 'CREATE',
    state: 'PREVIEW',
    total: 2,
    ok: 0,
    failed: 0,
    skipped: 0,
    dryRun: true,
    cancelRequested: false,
    createdAt: new Date().toISOString(),
    startedAt: new Date().toISOString(),
    finishedAt: new Date().toISOString(),
    cancellable: false,
    items: [
      {
        componentId: 'RTCT_EVT',
        repoType: 'DESIGN',
        state: 'PREVIEW',
        errorCode: null,
        message: 'DRY_RUN 预览（只预检、未写入任何仓库/ref）：状态 SYNCED，无待推送',
        preview: true,
      },
      {
        componentId: 'RTCT_EVT',
        repoType: 'IMPL',
        state: 'PREVIEW',
        errorCode: null,
        message: 'DRY_RUN 预览（只预检、未写入任何仓库/ref）：状态 SYNCED，无待推送',
        preview: true,
      },
    ],
  }
  const previewState = await pollUntil(async () => {
    await injectJobUpdate(
      { event: 'JOB_UPDATED', jobId: previewJob.id, current: previewJob, data: previewJob, state: 'PREVIEW', total: 2, cancellable: false },
      previewJob.id,
    )
    await sleep(100)
    return evaluate(`(() => {
      const row = document.querySelector('[data-testid=task-job][data-job-id="job-evt-preview-1"]');
      if (!row) return null;
      return {
        state: row.getAttribute('data-state'),
        dryRun: row.getAttribute('data-dryrun'),
        badge: row.querySelector('[data-testid=task-dryrun-badge]')?.innerText ?? '',
        stateLabel: row.querySelector('[data-testid=task-state]')?.innerText ?? '',
        progress: row.querySelector('[data-testid=task-progress]')?.innerText ?? '',
        note: row.querySelector('[data-testid=task-dryrun-note]')?.innerText ?? '',
        itemStates: [...row.querySelectorAll('[data-testid=task-item-state]')].map((n) => n.innerText),
        itemErrorCodes: [...row.querySelectorAll('[data-testid=task-item-error]')].map((n) => n.getAttribute('data-error-code')),
      };
    })()`)
  })
  check('preview.job-state', previewState?.state === 'PREVIEW', JSON.stringify(previewState))
  check('preview.dryrun-flag', previewState?.dryRun === '1' && /预览（dryRun）/.test(previewState.badge), JSON.stringify(previewState?.badge))
  check('preview.state-label', /预览完成/.test(previewState?.stateLabel ?? ''), previewState?.stateLabel)
  check('preview.progress', previewState?.progress === '未执行写操作', previewState?.progress)
  check('preview.note', /未执行任何写操作/.test(previewState?.note ?? ''), previewState?.note)
  check(
    'preview.item-state',
    (previewState?.itemStates?.length ?? 0) === 2 && previewState.itemStates.every((label) => /预览（未执行）/.test(label)),
    JSON.stringify(previewState?.itemStates),
  )
  check('preview.item-no-error', (previewState?.itemErrorCodes?.length ?? 0) === 0, JSON.stringify(previewState?.itemErrorCodes))
  await closeDrawerByEsc()

  // 10) 真实 Host：进行中作业可见 + 取消 + 仅重试失败项（只读 FETCH）
  await ensureSelectionAll()
  await click('[data-testid=batch-more]')
  await sleep(250)
  await click('[data-testid=batch-more-fetch]')
  await waitFor(`!!document.querySelector('[data-testid=confirm-dialog]')`, 4_000)
  const fetchConfirm = await text('[data-testid=confirm-dialog]')
  check('real.fetch-confirm', /FETCH/.test(fetchConfirm), fetchConfirm.slice(0, 140))
  await click('[data-testid=confirm-dialog] .confirm-actions .btn:last-child')
  await waitFor(`!!document.querySelector('[data-testid=task-center]')`, 6_000)
  await sleep(400)
  let liveJobId = ''
  for (let i = 0; i < 30 && !liveJobId; i += 1) {
    liveJobId = (await jobIds('[data-testid=task-job][data-active="1"]'))[0] ?? ''
    if (!liveJobId) await sleep(200)
  }
  const liveInfo = liveJobId
    ? await evaluate(`(() => {
        const row = document.querySelector('[data-testid=task-job][data-job-id="${liveJobId}"]');
        return { cancel: !!row?.querySelector('[data-testid=task-cancel]'), segmentActive: document.querySelector('[data-testid=task-segment-active]')?.getAttribute('aria-selected') };
      })()`)
    : null
  check('real.running-visible', Boolean(liveJobId && liveInfo?.cancel), JSON.stringify({ liveJobId, liveInfo }))
  check('real.running-auto-focus', liveInfo?.segmentActive === 'true')
  await screenshot('07-real-running-job.png')
  if (liveJobId) {
    await click(`[data-testid=task-job][data-job-id="${liveJobId}"] [data-testid=task-cancel]`)
    await sleep(1_500)
  }
  let canceledState = ''
  for (let i = 0; i < 25; i += 1) {
    canceledState = await evaluate(`document.querySelector('[data-testid=task-job][data-job-id="${liveJobId}"]')?.getAttribute('data-state') ?? ''`)
    if (canceledState === 'CANCELED') break
    await click('[data-testid=task-refresh]')
    await sleep(300)
  }
  if (!canceledState) {
    await click('[data-testid=task-segment-history]')
    await sleep(500)
    canceledState = await evaluate(`document.querySelector('[data-testid=task-job][data-job-id="${liveJobId}"]')?.getAttribute('data-state') ?? ''`)
  }
  const cancelHit = canceledState === 'CANCELED'
  check('real.cancel-state', cancelHit || canceledState === 'SUCCESS', canceledState)
  if (cancelHit) {
  const realCancel = await evaluate(`(() => {
    const row = document.querySelector('[data-testid=task-job][data-job-id="${liveJobId}"]');
    if (!row) return null;
    const items = [...row.querySelectorAll('[data-testid=task-item]')];
    return {
      timeoutErrors: [...row.querySelectorAll('[data-testid=task-item-error]')].filter((n) => n.getAttribute('data-error-code') === 'E_TIMEOUT').length,
      cancelNotes: [...row.querySelectorAll('[data-testid=task-item-canceled]')].map((n) => n.innerText),
      retry: row.querySelector('[data-testid=task-retry-failed]')?.innerText ?? '',
      items: items.length,
    };
  })()`)
  check('real.cancel-no-e-timeout', realCancel?.timeoutErrors === 0, JSON.stringify(realCancel))
  check('real.cancel-item-notes', realCancel?.cancelNotes.every((note) => /^已取消/.test(note)) ?? false, JSON.stringify(realCancel?.cancelNotes?.slice(0, 3)))
  await screenshot('08-real-canceled.png')
  check('real.retry-visible', /仅重试失败项/.test(realCancel?.retry ?? ''), realCancel?.retry)
  if (liveJobId) {
    await click(`[data-testid=task-job][data-job-id="${liveJobId}"] [data-testid=task-retry-failed]`)
    await sleep(1_200)
    const retryToast = await allToasts()
    const retryJob = await evaluate(`(() => {
      const row = document.querySelector('[data-testid=task-job][data-active="1"]');
      return row ? { id: row.getAttribute('data-job-id'), items: row.querySelectorAll('[data-testid=task-item]').length } : null;
    })()`)
    check('real.retry-new-job', Boolean(retryJob && retryJob.id !== liveJobId), JSON.stringify(retryJob))
    check('real.retry-toast', /重试作业/.test(retryToast), retryToast.slice(-160))
    await screenshot('09-real-retry.png')
    // 收尾：取消重试作业，避免长跑
    if (retryJob) {
      await click(`[data-testid=task-job][data-job-id="${retryJob.id}"] [data-testid=task-cancel]`)
      await sleep(1_200)
    }
  }
  } else {
    check('real.cancel-fast-job-completed', canceledState === 'SUCCESS', canceledState)
  }
  check('page.no-errors', pageErrors.length === 0, JSON.stringify(pageErrors))
} finally {
  // 清理：移除注入脚本并 reload，页面最终停留在真实 Host、清空页内假事件作业。
  await removeScript(mockScriptId)
  await removeScript(captureScriptId)
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
console.log(JSON.stringify({ total: summary.total, passed: summary.passed, failed: summary.failed, screenshots: outDir }))
process.exit(failed.length === 0 ? 0 : 1)
