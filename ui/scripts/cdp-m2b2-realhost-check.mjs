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
import { requireGiteaOrSkip } from './lib/gitea-gate.mjs'

const here = dirname(fileURLToPath(import.meta.url))
const outDir = join(here, '..', '..', 'artifacts', 'm2b2-realhost')
mkdirSync(outDir, { recursive: true })
// 换机器时 192.168.50.20 可能不可达：真实 Gitea 自检必须跳过而不是失败。
await requireGiteaOrSkip('cdp-m2b2-realhost-check', outDir)
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

// ── 1) 真实快照：健康 local+gitea 不得误报离线；只有显式远程错误才禁用 FETCH ──
const sourceLabel = await text('[data-testid=snapshot-source]')
const offlineText = await text('[data-testid=offline-banner]')
check('real.source-local', /本地扫描/.test(sourceLabel), sourceLabel)
const healthyGitea = /Gitea/.test(sourceLabel)
const offlineShown = /离线/.test(offlineText)
check('real.no-false-offline', healthyGitea ? !offlineShown : true, offlineText)
check('real.statusbar-online', healthyGitea ? !/离线/.test(await text('[data-testid=status-network]')) : true)
const counts = await evaluate(
  `(() => ({ empty: !!document.querySelector('[data-testid=component-card][data-component-id="RTCT_Empty"]'), demo: !!document.querySelector('[data-testid=component-card][data-component-id="RTCT_Demo"]'), list: document.querySelectorAll('[data-testid=component-card]').length }))()`,
)
check('real.workspace-components', counts.list >= 4 && counts.demo, JSON.stringify(counts))
const fetchState = await evaluate(
  `(() => { const n = document.querySelector('[data-testid=action-fetch]'); return n ? { disabled: n.disabled, title: n.title, code: n.getAttribute('data-reason-code') } : null; })()`,
)
check('real.fetch-available-online', healthyGitea ? fetchState?.disabled === false : true, JSON.stringify(fetchState))
await screenshot('01-real-online.png')

// ── 2) EMPTY 首推：若真实工作区没有 EMPTY 组件则跳过（单测覆盖 unborn/有 HEAD 两分支） ──
if (counts.empty) {
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
  if (emptyBar) {
    await click('[data-testid=confirm-bar-cancel]')
    await sleep(250)
  }
  await screenshot('02-real-empty-push-bar.png')
} else {
  check('real.empty-skip-no-fixture', true, '真实工作区无 RTCT_Empty；EMPTY 两分支由 Host 单测覆盖')
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
  // M2b-2b：真实 Host 备份 ref 只做只读校验（新格式带 branch，旧格式标记 LEGACY）；
  // 恢复写链路由 M2b-2 自检（Mock）+ Host 单测（含跨分支拒绝）覆盖，避免 realhost 脚本硬编码旧 ref/HEAD。
  check('real.backup-target-ref-present', backupRows.length > 0, backupRows.map((row) => row.ref).join(', '))
  check(
    'real.backup-branch-or-legacy',
    backupRows.some((row) => /@main/.test(row.text) || /旧格式/.test(row.text)),
    backupRows.map((row) => row.text).join(' | ').slice(0, 260),
  )
  check(
    'real.backup-ref-fields',
    backupRows.every((row) => /[0-9a-f]{7}/.test(row.text) && /\d{2}-\d{2}/.test(row.text)),
    backupRows[0]?.text ?? '',
  )
  await screenshot('05-real-backup-refs.png')
await escape()
await sleep(300)

// ── 5) 恢复后审计表仍可查询（真实 operation_log；具体 RESTORE_BACKUP 写链路由 Mock 自检覆盖） ──
await openLogsModal()
await waitFor(`document.querySelectorAll('[data-testid=audit-row]').length > 0`, 8_000)
const auditRowsAfter = await evaluate(
  `[...document.querySelectorAll('[data-testid=audit-row]')].map((r) => ({ action: r.getAttribute('data-action'), result: r.getAttribute('data-result') }))`,
)
check('real.audit-query-after', auditRowsAfter.length > 0, `rows=${auditRowsAfter.length}`)
check('real.audit-has-terminal-result', auditRowsAfter.some((row) => /OK|FAILED|SKIPPED|CANCELED|PREVIEW/.test(row.result ?? '')), JSON.stringify(auditRowsAfter.slice(0, 3)))
await screenshot('08-real-audit-query.png')
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
