/**
 * M2b-2 前端真实 Host CDP 自检（前置：真实 Host 已启动并开启 CDP 9222，工作区为隔离副本）。
 *
 * 覆盖（只读 + 隔离副本内的 1 次备份 ref 恢复写）：
 *  - 离线提示（source=local + 远程标记）：顶部「离线/远程不可达」、状态栏离线、FETCH 禁用 tooltip；
 *  - EMPTY 首推：Host 矩阵恢复后 PUSH 按钮启用，确认条「发布分支并建立 upstream」+ setUpstream（取消 = 0 写）；
 *  - 操作审计：真实 operation_log 查询（空态/错误态）、CSV 导出真实 path、openPath 被路径策略拒绝时的兜底；
 *  - 备份 ref：真实 getBackupRefs 列表（refs/backup/sync/*）、确认后 restoreBackupRef 成功、恢复前安全备份；
 *  - 恢复后重新查询审计可见 RESTORE_BACKUP 行。
 *
 * 用法：node ui/scripts/cdp-m2b2-realhost-check.mjs [port=9222]
 * 结果：artifacts/m2b2-realhost/（result.json + 截图）
 */
import { mkdirSync, writeFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const here = dirname(fileURLToPath(import.meta.url))
const outDir = join(here, '..', '..', 'artifacts', 'm2b2-realhost')
mkdirSync(outDir, { recursive: true })
const port = Number(process.argv[2] ?? 9222)

const results = []
function check(id, ok, detail = '') {
  const entry = { id, ok: Boolean(ok), detail: String(detail).slice(0, 600) }
  results.push(entry)
  console.log(`${entry.ok ? 'PASS' : 'FAIL'} ${id}${entry.detail ? ' :: ' + entry.detail : ''}`)
}
const sleep = (ms) => new Promise((resolvePromise) => setTimeout(resolvePromise, ms))

const targets = await fetch(`http://127.0.0.1:${port}/json/list`).then((response) => response.json())
const target = targets.find((item) => item.type === 'page') ?? targets[0]
if (!target) throw new Error('CDP 无页面：请确认 Host 已开启 --remote-debugging-port=9222')
const ws = new WebSocket(target.webSocketDebuggerUrl)
await new Promise((resolvePromise, rejectPromise) => {
  ws.addEventListener('open', resolvePromise)
  ws.addEventListener('error', rejectPromise)
})
let seq = 0
const pending = new Map()
const pageErrors = []
ws.addEventListener('message', (event) => {
  const message = JSON.parse(event.data)
  if (message.method === 'Runtime.exceptionThrown') {
    pageErrors.push(message.params.exceptionDetails?.exception?.description ?? message.params.exceptionDetails?.text ?? '')
  }
  if (message.method === 'Runtime.consoleAPICalled' && message.params.type === 'error') {
    pageErrors.push(message.params.args.map((arg) => String(arg.value ?? arg.description ?? '')).join(' '))
  }
  if (message.id && pending.has(message.id)) {
    pending.get(message.id)(message)
    pending.delete(message.id)
  }
})
const send = (method, params = {}) =>
  new Promise((resolvePromise) => {
    const id = ++seq
    pending.set(id, resolvePromise)
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
      /* 页面导航/重载中 */
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
async function dblclick(selector) {
  const at = await point(selector)
  if (!at) throw new Error(`未找到可双击元素：${selector}`)
  await send('Input.dispatchMouseEvent', { type: 'mousePressed', x: at.x, y: at.y, button: 'left', clickCount: 1 })
  await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x: at.x, y: at.y, button: 'left', clickCount: 1 })
  await send('Input.dispatchMouseEvent', { type: 'mousePressed', x: at.x, y: at.y, button: 'left', clickCount: 2 })
  await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x: at.x, y: at.y, button: 'left', clickCount: 2 })
}
async function key(keyName, code, keyCode) {
  await send('Input.dispatchKeyEvent', { type: 'rawKeyDown', key: keyName, code, windowsVirtualKeyCode: keyCode })
  await send('Input.dispatchKeyEvent', { type: 'keyUp', key: keyName, code, windowsVirtualKeyCode: keyCode })
}
const escape = () => key('Escape', 'Escape', 27)
const enter = () => key('Enter', 'Enter', 13)
const text = (selector) =>
  evaluate(`document.querySelector(${JSON.stringify(selector)})?.innerText?.replace(/\\s+/g, ' ').trim() ?? ''`)
const count = (selector) => evaluate(`document.querySelectorAll(${JSON.stringify(selector)}).length`)
const allToasts = () =>
  evaluate(`[...document.querySelectorAll('[data-testid=toast]')].map((n) => n.innerText.replace(/\\s+/g, ' ').trim()).join(' | ')`)
async function screenshot(name) {
  const shot = await send('Page.captureScreenshot', { format: 'png' })
  if (shot.result?.data) writeFileSync(join(outDir, name), Buffer.from(shot.result.data, 'base64'))
}
async function reload() {
  await send('Page.reload', { ignoreCache: true })
  const ready = await waitFor(`!!document.querySelector('[data-testid=component-list-count]')`, 30_000)
  await sleep(1_500)
  return ready
}
async function selectCard(componentId) {
  const selector = `[data-testid=component-card][data-component-id="${componentId}"]`
  await evaluate(`document.querySelector(${JSON.stringify(selector)})?.scrollIntoView({ block: 'center' })`)
  await sleep(200)
  await click(selector)
  await sleep(900)
}
async function openLogsModal() {
  await click('[data-testid=menu-tools]')
  await sleep(250)
  await click('[data-testid="menu-item-tools.logs"]')
  await sleep(400)
}

await send('Page.enable')
await send('Runtime.enable')
const booted = await reload()
check('real.boot', booted)

// 强制重新扫描（隔离副本在 Host 启动后追加了 EMPTY 夹具与备份 ref）。
await click('[data-testid=title-refresh]')
await sleep(4_000)
await waitFor(`!!document.querySelector('[data-testid=component-card]')`, 20_000)

// ── 1) 真实快照 + 离线提示 + FETCH 禁用 ──
const sourceLabel = await text('[data-testid=snapshot-source]')
const offlineText = await text('[data-testid=offline-banner]')
check('real.source-local', /本地扫描/.test(sourceLabel), sourceLabel)
check('real.offline-banner', /离线/.test(offlineText) && /远程不可达/.test(offlineText), offlineText)
check('real.statusbar-offline', /离线/.test(await text('[data-testid=status-network]')))
const counts = await evaluate(
  `(() => ({ empty: !!document.querySelector('[data-testid=component-card][data-component-id="RTCT_Empty"]'), demo: !!document.querySelector('[data-testid=component-card][data-component-id="RTCT_Demo"]'), list: document.querySelectorAll('[data-testid=component-card]').length }))()`,
)
check('real.workspace-components', counts.list >= 4 && counts.empty && counts.demo, JSON.stringify(counts))
const fetchState = await evaluate(
  `(() => { const n = document.querySelector('[data-testid=action-fetch]'); return n ? { disabled: n.disabled, title: n.title, code: n.getAttribute('data-reason-code') } : null; })()`,
)
check('real.fetch-offline-disabled', fetchState?.disabled === true && /E_OFFLINE/.test(fetchState?.title ?? ''), JSON.stringify(fetchState))
await screenshot('01-real-offline.png')

// ── 2) EMPTY 首推确认条（Host 矩阵恢复；取消 = 0 写） ──
await selectCard('RTCT_Empty')
const emptyPushReady = await waitFor(
  `document.querySelector('[data-testid=action-push]')?.getAttribute('data-enabled') === 'true'`,
  12_000,
)
const emptyPush = await evaluate(
  `(() => { const n = document.querySelector('[data-testid=action-push]'); return n ? { enabled: n.getAttribute('data-enabled'), disabled: n.disabled, title: n.title } : null; })()`,
)
check('real.empty-push-enabled', emptyPushReady && emptyPush?.enabled === 'true' && emptyPush?.disabled === false, JSON.stringify(emptyPush))
await click('[data-testid=action-push]')
await sleep(450)
const emptyBar = await text('[data-testid=confirm-bar]')
check('real.empty-confirm-branch-upstream', /发布分支并建立 upstream/.test(emptyBar) && /setUpstream=true/.test(emptyBar), emptyBar.slice(0, 180))
check('real.empty-confirm-no-write-before-accept', (await text('[data-testid=status-last-op]')).includes('无'), await text('[data-testid=status-last-op]'))
await screenshot('02-real-empty-push-bar.png')
if (emptyBar) {
  await click('[data-testid=confirm-bar-cancel]')
  await sleep(250)
}

// ── 3) 真实 operation_log 查询（初始空态/就绪，不伪造行） ──
await openLogsModal()
const auditReady = await waitFor(
  `(() => { const s = document.querySelector('[data-testid=audit-status]')?.innerText ?? ''; return s.includes('更新于') || s.includes('失败') || s.includes('不可用'); })()`,
  12_000,
)
const auditStatus = await text('[data-testid=audit-status]')
const auditError = await text('[data-testid=audit-error]')
check('real.audit-query-ready', auditReady && !/失败|不可用/.test(auditStatus), `${auditStatus} ${auditError}`.trim())
const auditRowsBefore = await count('[data-testid=audit-row]')
const auditEmptyBefore = await count('[data-testid=audit-empty]')
check('real.audit-has-rows-or-empty', auditRowsBefore > 0 || auditEmptyBefore === 1, `rows=${auditRowsBefore} empty=${auditEmptyBefore}`)

// 导出 CSV → 真实 path；openPath 路径策略拒绝时必须兜底（不伪造打开成功）
await evaluate(`[...document.querySelectorAll('[data-testid=toast]')].forEach((node) => node.click())`)
await click('[data-testid=audit-export-csv]')
await sleep(900)
const exportPath = await text('[data-testid=audit-export-path]')
const exportToast = await allToasts()
check(
  'real.audit-export-csv',
  /MbdGitTool\\exports/i.test(exportPath) && exportPath.endsWith('.csv') && exportToast.includes(exportPath),
  `${exportPath} | ${exportToast.slice(0, 160)}`,
)
await screenshot('03-real-audit-export.png')
await evaluate(`[...document.querySelectorAll('[data-testid=toast]')].forEach((node) => node.click())`)
await sleep(300)
await click('[data-testid=audit-open-dir]')
await sleep(900)
const openDirToast = await allToasts()
check(
  'real.audit-open-dir-no-fake-success',
  !/已在资源管理器中打开/.test(openDirToast) && /无法直接打开所在目录|已复制|路径/.test(openDirToast),
  openDirToast.slice(0, 220),
)
check('real.audit-open-dir-path-copied', /已复制|路径/.test(openDirToast), openDirToast.slice(0, 220))
await screenshot('04-real-audit-open-dir-fallback.png')
await escape()
await sleep(300)

// ── 4) 备份 ref：真实 getBackupRefs → 确认 → restoreBackupRef（隔离副本写） ──
await selectCard('RTCT_Demo')
await dblclick('[data-testid=component-card][data-component-id="RTCT_Demo"]')
await sleep(1_000)
const backupReady = await waitFor(
  `document.querySelectorAll('[data-testid=backup-ref]').length > 0 || !!document.querySelector('[data-testid=backup-empty]')`,
  10_000,
)
const backupRows = await evaluate(
  `[...document.querySelectorAll('[data-testid=backup-ref]')].map((r) => ({ ref: r.getAttribute('data-ref'), text: r.innerText.replace(/\\s+/g, ' ').trim() }))`,
)
check('real.backup-refs-loaded', backupReady && backupRows.length >= 1, JSON.stringify(backupRows))
const targetRow = backupRows.find((row) => row.ref.includes('20261114170000'))
check('real.backup-target-ref-present', Boolean(targetRow), backupRows.map((row) => row.ref).join(', '))
await screenshot('05-real-backup-refs.png')

if (targetRow) {
  await evaluate(
    `(() => { const rows = [...document.querySelectorAll('[data-testid=backup-ref]')]; const row = rows.find((r) => r.getAttribute('data-ref') === ${JSON.stringify(targetRow.ref)}); row?.scrollIntoView({ block: 'center' }); })()`,
  )
  await sleep(200)
  await evaluate(
    `(() => { const rows = [...document.querySelectorAll('[data-testid=backup-ref]')]; const row = rows.find((r) => r.getAttribute('data-ref') === ${JSON.stringify(targetRow.ref)}); row?.querySelector('[data-testid=backup-restore]')?.click(); })()`,
  )
  await sleep(450)
  const confirmText = await text('[data-testid=confirm-dialog]')
  const headBeforeConfirm = await evaluate(
    `(() => { const row = document.querySelector('[data-testid=component-drawer-repo][data-repo-type="IMPL"]'); return row ? row.innerText.replace(/\\s+/g, ' ').trim() : ''; })()`,
  )
  check(
    'real.backup-confirm-overwrite',
    /覆盖当前分支状态/.test(confirmText) && confirmText.includes('refs/backup/sync/20261114170000/IMPL'),
    confirmText.slice(0, 220),
  )
  check('real.backup-no-restore-before-confirm', /85dced5/.test(headBeforeConfirm), headBeforeConfirm.slice(0, 160))
  await screenshot('06-real-backup-confirm.png')
  await click('[data-testid=confirm-dialog] .confirm-actions .btn:last-child')
  // 真实 Host restore 会执行 refresh(getSnapshot) + 安全备份 ref + reset；给足 20s 等待结果。
  await waitFor(`/已从备份 ref 恢复/.test([...document.querySelectorAll('[data-testid=toast]')].map((n) => n.innerText).join(' '))`, 20_000)
  await sleep(600)
  const restoreToasts = await allToasts()
  check('real.backup-restore-success', /已从备份 ref 恢复/.test(restoreToasts), restoreToasts.slice(0, 260))
  check('real.backup-safety-ref', /恢复前 HEAD 已备份/.test(restoreToasts), restoreToasts.slice(0, 260))
  await waitFor(`/e0bd905/.test(document.querySelector('[data-testid=component-drawer-repo][data-repo-type="IMPL"]')?.innerText ?? '')`, 12_000)
  await screenshot('07-real-backup-restored.png')
  // 恢复后详情 HEAD 回写（真实 refreshSnapshot + loadComponentDetail）
  const headText = await evaluate(
    `(() => { const row = document.querySelector('[data-testid=component-drawer-repo][data-repo-type="IMPL"]'); return row ? row.innerText.replace(/\\s+/g, ' ').trim() : ''; })()`,
  )
  check('real.backup-head-updated', /e0bd905/.test(headText) && !/85dced5/.test(headText), headText.slice(0, 200))
} else {
  check('real.backup-confirm-overwrite', false, 'target ref missing')
  check('real.backup-no-restore-before-confirm', false, 'target ref missing')
  check('real.backup-restore-success', false, 'target ref missing')
  check('real.backup-safety-ref', false, 'target ref missing')
  check('real.backup-head-updated', false, 'target ref missing')
}
await escape()
await sleep(300)

// ── 5) 恢复后重查审计：RESTORE_BACKUP 行可见（真实写审计 → 真实查询） ──
await openLogsModal()
await waitFor(`document.querySelectorAll('[data-testid=audit-row]').length > 0`, 8_000)
const auditRowsAfter = await evaluate(
  `[...document.querySelectorAll('[data-testid=audit-row]')].map((r) => ({ action: r.getAttribute('data-action'), result: r.getAttribute('data-result'), ts: r.querySelector('[data-testid=audit-ts]')?.innerText, heads: r.querySelector('[data-testid=audit-heads]')?.innerText }))`,
)
const restoreRow = auditRowsAfter.find((row) => row.action === 'RESTORE_BACKUP')
check('real.audit-restore-row', Boolean(restoreRow), JSON.stringify(auditRowsAfter.slice(0, 3)))
check('real.audit-restore-row-result', restoreRow?.result === 'OK', restoreRow?.result ?? '')
check('real.audit-restore-row-heads', /[0-9a-f]{7}/.test(restoreRow?.heads ?? ''), restoreRow?.heads ?? '')
await screenshot('08-real-audit-restore-row.png')
await escape()
await sleep(250)

// ── 6) 页面错误 ──
check('real.page-no-console-errors', pageErrors.length === 0, pageErrors.slice(0, 3).join(' | '))

const failed = results.filter((item) => !item.ok)
writeFileSync(
  join(outDir, 'result.json'),
  JSON.stringify({ total: results.length, passed: results.length - failed.length, failed: failed.map((item) => item.id), checks: results }, null, 2),
)
console.log(JSON.stringify({ total: results.length, passed: results.length - failed.length, failed: failed.map((item) => item.id) }, null, 2))
try {
  ws.close()
} catch {
  /* ignore */
}
process.exit(failed.length === 0 ? 0 : 1)
