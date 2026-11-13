#!/usr/bin/env node
/**
 * v3 工作台 CDP 自测（零依赖，Node ≥22 内置 WebSocket）。
 *
 * 前置：宿主页面已启动（`npm run dev` 或 `vite preview`）且 Chromium 内核（Edge/Chrome）
 * 以 --remote-debugging-port=<port> 打开该页面。
 *
 * 用法：
 *   node scripts/cdp-selfcheck.mjs --cdp 9333 --out "%TEMP%\mbd-ui-v3" --width 1440 --height 900
 *
 * 产出：控制台 JSON 摘要 + 截图（workbench-1440.png / workbench-1280.png / folder-1440.png / action-disabled.png）。
 * 注意：本脚本只做只读断言与真实鼠标/键盘事件（Input.dispatchMouseEvent / dispatchKeyEvent），不使用 element.click()。
 */
import { mkdir, writeFile } from 'node:fs/promises'
import { join } from 'node:path'

const args = process.argv.slice(2)
function arg(name, fallback) {
  const at = args.indexOf(`--${name}`)
  return at >= 0 && args[at + 1] ? args[at + 1] : fallback
}

const CDP = Number(arg('cdp', '9333'))
const OUT = arg('out', join(process.cwd(), 'ui-selfcheck'))
const WIDTH = Number(arg('width', '1440'))
const HEIGHT = Number(arg('height', '900'))

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms))

async function connect() {
  for (let attempt = 0; attempt < 40; attempt += 1) {
    try {
      const targets = await fetch(`http://127.0.0.1:${CDP}/json/list`).then((r) => r.json())
      const pages = targets.filter((t) => t.type === 'page' && t.webSocketDebuggerUrl && /^http/.test(t.url ?? ''))
      const page =
        pages.find((t) => /127\.0\.0\.1|localhost/.test(t.url) && /4173|5173/.test(t.url)) ??
        pages.find((t) => /127\.0\.0\.1|localhost/.test(t.url)) ??
        pages[0]
      if (page) {
        const ws = new WebSocket(page.webSocketDebuggerUrl)
        await new Promise((resolve, reject) => {
          ws.addEventListener('open', resolve, { once: true })
          ws.addEventListener('error', reject, { once: true })
        })
        return ws
      }
    } catch {
      /* retry */
    }
    await sleep(250)
  }
  throw new Error(`无法连接 CDP（127.0.0.1:${CDP}）`)
}

const ws = await connect()
let seq = 0
const pending = new Map()
const consoleErrors = []
const exceptions = []
ws.addEventListener('message', (event) => {
  const msg = JSON.parse(event.data)
  if (msg.id && pending.has(msg.id)) {
    pending.get(msg.id)(msg)
    pending.delete(msg.id)
    return
  }
  if (msg.method === 'Runtime.exceptionThrown') {
    exceptions.push(msg.params.exceptionDetails?.text ?? 'exception')
  }
  if (msg.method === 'Runtime.consoleAPICalled' && msg.params.type === 'error') {
    consoleErrors.push((msg.params.args ?? []).map((a) => a.value ?? a.description ?? '').join(' '))
  }
  if (msg.method === 'Log.entryAdded' && msg.params.entry.level === 'error') {
    consoleErrors.push(msg.params.entry.text)
  }
})

const send = (method, params = {}) =>
  new Promise((resolve, reject) => {
    const id = ++seq
    pending.set(id, (msg) => (msg.error ? reject(new Error(`${method}: ${msg.error.message}`)) : resolve(msg.result)))
    ws.send(JSON.stringify({ id, method, params }))
  })

const evaluate = async (expression) => {
  const result = await send('Runtime.evaluate', { expression, returnByValue: true, awaitPromise: true })
  if (result.exceptionDetails) throw new Error(JSON.stringify(result.exceptionDetails))
  return result.result?.value
}

async function waitFor(expression, timeout = 8000) {
  const started = Date.now()
  while (Date.now() - started < timeout) {
    if (await evaluate(expression)) return true
    await sleep(120)
  }
  return false
}

async function rect(selector) {
  return evaluate(`(() => {
    const el = document.querySelector(${JSON.stringify(selector)});
    if (!el) return null;
    const r = el.getBoundingClientRect();
    return { x: r.x, y: r.y, w: r.width, h: r.height, bottom: r.bottom, right: r.right, left: r.left, top: r.top };
  })()`)
}

async function mouseClick(x, y, clickCount = 1) {
  await send('Input.dispatchMouseEvent', { type: 'mouseMoved', x, y, button: 'none', clickCount: 0 })
  await send('Input.dispatchMouseEvent', { type: 'mousePressed', x, y, button: 'left', clickCount })
  await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x, y, button: 'left', clickCount })
}

async function clickSelector(selector) {
  const started = Date.now()
  let target = null
  while (Date.now() - started < 6000) {
    target = await rect(selector)
    if (target) break
    await sleep(120)
  }
  if (!target) throw new Error(`元素不存在：${selector}`)
  await mouseClick(target.x + target.w / 2, target.y + target.h / 2)
  await sleep(120)
  return target
}

async function doubleClickSelector(selector) {
  const target = await rect(selector)
  if (!target) throw new Error(`元素不存在：${selector}`)
  const x = target.x + Math.min(target.w / 2, 80)
  const y = target.y + target.h / 2
  await mouseClick(x, y)
  await sleep(80)
  await mouseClick(x, y, 2)
  await sleep(200)
  return target
}

async function key(keyName, code, vk, modifiers = 0) {
  await send('Input.dispatchKeyEvent', { type: 'keyDown', key: keyName, code, windowsVirtualKeyCode: vk, nativeVirtualKeyCode: vk, modifiers })
  await send('Input.dispatchKeyEvent', { type: 'keyUp', key: keyName, code, windowsVirtualKeyCode: vk, nativeVirtualKeyCode: vk, modifiers })
  await sleep(120)
}

async function shot(name) {
  const result = await send('Page.captureScreenshot', { format: 'png' })
  await writeFile(join(OUT, `${name}.png`), Buffer.from(result.data, 'base64'))
  return join(OUT, `${name}.png`)
}

await send('Page.enable')
await send('Runtime.enable')
await send('Log.enable')
await mkdir(OUT, { recursive: true })

const report = { cdp: CDP, viewport: { width: WIDTH, height: HEIGHT }, checks: {}, anchors: {}, screenshots: [] }
const fail = (name, detail) => {
  report.checks[name] = { ok: false, detail }
}

// 等待应用就绪（Mock 数据 7 卡片）
await send('Emulation.setDeviceMetricsOverride', { width: WIDTH, height: HEIGHT, deviceScaleFactor: 1, mobile: false })
await send('Page.reload', { ignoreCache: true })
await sleep(600)
const ready = await waitFor(`document.querySelectorAll('[data-testid="component-card"]').length >= 6`)
report.checks.boot = { ok: ready, detail: ready ? '应用已渲染且卡片 ≥6' : '等待超时' }

const layout = await evaluate(`(() => {
  const q = (s) => document.querySelector(s);
  const r = (s) => { const el = q(s); if (!el) return null; const b = el.getBoundingClientRect(); return { w: b.width, h: b.height, x: b.x, y: b.y, bottom: b.bottom, top: b.top, left: b.left }; };
  const panels = [...document.querySelectorAll('[data-testid="repo-tree-panel"]')].map((el) => { const b = el.getBoundingClientRect(); return { w: b.width, h: b.height, top: b.top, left: b.left, type: el.dataset.repoType }; });
  const cards = [...document.querySelectorAll('[data-testid="component-card"]')];
  return {
    scrollWidth: document.scrollingElement.scrollWidth,
    clientWidth: document.scrollingElement.clientWidth,
    innerWidth: window.innerWidth,
    innerHeight: window.innerHeight,
    bodyOverflow: getComputedStyle(document.body).overflow,
    appOverflow: q('#app') ? getComputedStyle(q('#app')).overflow : null,
    appHeight: q('[data-testid="app-shell"]')?.getBoundingClientRect().height,
    menubar: r('[data-testid="menubar"]'),
    titlebar: r('[data-testid="titlebar"]'),
    componentList: r('[data-testid="component-list"]'),
    infoPanel: r('[data-testid="info-panel"]'),
    repoGrid: r('[data-testid="repo-grid"]'),
    repoActions: r('[data-testid="repo-actions"]'),
    statusbar: r('[data-testid="statusbar"]'),
    panels,
    cardCount: cards.length,
    cardChips: cards.map((c) => c.querySelectorAll('[data-testid="card-repo-chip"]').length),
    cardHeight: cards[0]?.getBoundingClientRect().height,
    selected: document.querySelectorAll('[data-testid="component-card"][aria-selected="true"]').length,
    treeNodes: [...document.querySelectorAll('[data-testid="repo-tree-panel"]')].map((p) => p.querySelectorAll('[data-testid="tree-node"]').length),
    ciSlots: document.querySelectorAll('[data-testid="ci-slot"]').length,
    dataSource: document.querySelector('[data-testid="snapshot-source"]')?.textContent?.trim(),
    componentCountText: document.querySelector('[data-testid="component-list-count"]')?.textContent?.trim(),
  };
})()`)
report.layout = layout

const near = (value, expected, tolerance) => Math.abs(value - expected) <= tolerance
if (ready) {
  report.checks.anchorsBase = {
    ok: ['menubar', 'titlebar', 'component-list', 'repo-grid', 'info-panel', 'repo-actions', 'statusbar', 'app-shell'].every((id) =>
      layout[id.replace(/-([a-z])/g, (_, c) => c.toUpperCase())] !== null,
    ),
    detail: '7 区域 + app-shell',
  }
  report.checks.width1440 = {
    ok:
      near(layout.componentList.w, 281.6, 6) &&
      near(layout.infoPanel.w, WIDTH === 1440 ? 253.4 : layout.infoPanel.w, 6) &&
      layout.panels.length === 4 &&
      layout.panels.every((p) => near(p.w, layout.panels[0].w, 2)),
    detail: { left: layout.componentList.w, right: layout.infoPanel.w, panels: layout.panels.map((p) => p.w) },
  }
  report.checks.verticalOrder = {
    ok:
      near(layout.menubar.top, 0, 1) &&
      near(layout.titlebar.top, layout.menubar.h, 2) &&
      near(layout.statusbar.bottom, HEIGHT, 2) &&
      near(layout.repoActions.bottom - layout.componentList.bottom, 64, 3) &&
      near(layout.repoActions.left, layout.repoGrid.left, 2),
    detail: {
      menubarTop: layout.menubar.top,
      titlebarTop: layout.titlebar.top,
      statusbarBottom: layout.statusbar.bottom,
      actionsBottom: layout.repoActions.bottom,
      listBottom: layout.componentList.bottom,
      actionsLeft: layout.repoActions.left,
      gridLeft: layout.repoGrid.left,
    },
  }
  report.checks.noHorizontalOverflow = {
    ok: layout.scrollWidth <= layout.innerWidth + 1 && layout.bodyOverflow === 'hidden' && layout.appOverflow === 'hidden',
    detail: { scrollWidth: layout.scrollWidth, innerWidth: layout.innerWidth, bodyOverflow: layout.bodyOverflow, appOverflow: layout.appOverflow },
  }
  report.checks.cards = {
    ok:
      layout.cardCount >= 6 &&
      layout.cardChips.every((count) => count === 4) &&
      layout.selected === 1 &&
      layout.ciSlots === layout.cardCount &&
      (layout.cardHeight >= 59 && layout.cardHeight <= 77),
    detail: { count: layout.cardCount, chips: layout.cardChips, selected: layout.selected, ciSlots: layout.ciSlots, height: layout.cardHeight },
  }
  report.checks.treeInitial = { ok: layout.treeNodes.length === 4 && layout.treeNodes.every((n) => n > 0), detail: layout.treeNodes }
}

// 选中第二张卡（真实鼠标事件）
const cardSelectors = await evaluate(`[...document.querySelectorAll('[data-testid="component-card"]')].map((c) => c.dataset.componentId)`)
if (cardSelectors?.length >= 2) {
  await clickSelector('[data-testid="component-card"]:nth-of-type(2)')
  await sleep(400)
  const selected = await evaluate(`(() => {
    const card = document.querySelector('[data-testid="component-card"][aria-selected="true"]');
    return {
      id: card?.dataset.componentId ?? null,
      infoTitle: document.querySelector('[data-testid="info-repo-title"]')?.textContent?.trim() ?? '',
      treeTitles: [...document.querySelectorAll('[data-testid="repo-tree-title"]')].map((t) => t.textContent.trim()),
      actionScope: document.querySelector('[data-testid="repo-actions"]')?.dataset.actionScope ?? '',
    };
  })()`)
  report.interactions = { selected }
  report.checks.cardSelectSync = {
    ok:
      selected.id === cardSelectors[1] &&
      selected.infoTitle.includes(selected.id) &&
      selected.treeTitles.length === 4 &&
      selected.treeTitles.every((title) => title.includes(selected.id)),
    detail: selected,
  }
}

// 展开树根目录（第一棵树的第一个 tree-toggle）
const beforeExpand = await evaluate(`document.querySelectorAll('[data-testid="tree-node"]').length`)
await clickSelector('[data-testid="repo-tree-panel"] [data-testid="tree-toggle"]')
await sleep(400)
const afterExpand = await evaluate(`document.querySelectorAll('[data-testid="tree-node"]').length`)
report.checks.treeExpand = { ok: afterExpand > beforeExpand, detail: { before: beforeExpand, after: afterExpand } }

// 双击文件 → openPath（Mock）→ Toast
const fileRow = await evaluate(`(() => {
  const row = [...document.querySelectorAll('[data-testid="repo-tree-panel"] .tree-row')].find((r) => !r.querySelector('[data-testid="tree-toggle"]'));
  if (!row) return null;
  const b = row.getBoundingClientRect();
  return { path: row.dataset.path, x: b.x + Math.min(b.width / 2, 90), y: b.y + b.height / 2 };
})()`)
if (fileRow?.path) {
  await mouseClick(fileRow.x, fileRow.y)
  await sleep(60)
  await mouseClick(fileRow.x, fileRow.y, 2)
  await sleep(350)
  const toast = await evaluate(`document.querySelector('[data-testid="toast"]')?.textContent?.trim() ?? ''`)
  report.checks.doubleClickOpen = { ok: toast.length > 0, detail: { path: fileRow.path, toast } }
  await key('Escape', 'Escape', 27)
}

// 危险扩展名二次确认（双击 tools/build.ps1 → ConfirmDialog）
const toolsToggle = await evaluate(`(() => {
  const row = document.querySelector('[data-testid="repo-tree-panel"][data-repo-type="IMPL"] .tree-row[data-path="tools"]');
  const toggle = row?.querySelector('[data-testid="tree-toggle"]');
  if (!toggle) return null;
  const b = toggle.getBoundingClientRect();
  return { x: b.x + b.width / 2, y: b.y + b.height / 2 };
})()`)
if (toolsToggle) {
  await mouseClick(toolsToggle.x, toolsToggle.y)
  await sleep(350)
  const dangerRow = await evaluate(`(() => {
    const row = document.querySelector('[data-testid="repo-tree-panel"][data-repo-type="IMPL"] .tree-row[data-path="tools/build.ps1"]');
    if (!row) return null;
    const b = row.getBoundingClientRect();
    return { x: b.x + 120, y: b.y + b.height / 2 };
  })()`)
  if (dangerRow) {
    await mouseClick(dangerRow.x, dangerRow.y)
    await sleep(70)
    await mouseClick(dangerRow.x, dangerRow.y, 2)
    await sleep(350)
    const danger = await evaluate(`(() => {
      const dialog = document.querySelector('[data-testid="confirm-dialog"]');
      return { exists: Boolean(dialog), text: dialog?.textContent?.replace(/\\s+/g, ' ').trim().slice(0, 160) ?? '' };
    })()`)
    report.checks.dangerousConfirm = { ok: danger.exists && danger.text.includes('build.ps1'), detail: danger }
    report.screenshots.push(await shot('confirm-dangerous'))
    const cancel = await rect('[data-testid="confirm-dialog"] .btn')
    if (cancel) await mouseClick(cancel.x + cancel.w / 2, cancel.y + cancel.h / 2)
    await sleep(250)
  }
}

// 双击目录 → reveal（不翻转展开态）
const docsBefore = await evaluate(`document.querySelectorAll('[data-testid="tree-node"]').length`)
const docsRow = await evaluate(`(() => {
  const row = document.querySelector('[data-testid="repo-tree-panel"][data-repo-type="IMPL"] .tree-row[data-path="docs"]');
  if (!row) return null;
  const b = row.getBoundingClientRect();
  return { x: b.x + 120, y: b.y + b.height / 2 };
})()`)
if (docsRow) {
  await mouseClick(docsRow.x, docsRow.y)
  await sleep(60)
  await mouseClick(docsRow.x, docsRow.y, 2)
  await sleep(350)
  const docsAfter = await evaluate(`document.querySelectorAll('[data-testid="tree-node"]').length`)
  const revealToast = await evaluate(`[...document.querySelectorAll('[data-testid="toast"]')].map((t) => t.textContent).join(' | ')`)
  report.checks.doubleClickDirReveal = {
    ok: docsAfter === docsBefore && /资源管理器/.test(revealToast),
    detail: { before: docsBefore, after: docsAfter, toast: revealToast },
  }
  await key('Escape', 'Escape', 27)
  await sleep(150)
}

// 底部按钮：可用 / 禁用 / M1 Toast
const actionState = await evaluate(`(() => ({
  pullDisabled: document.querySelector('[data-testid="action-pull"]')?.disabled ?? null,
  scope: document.querySelector('[data-testid="repo-actions"]')?.dataset.actionScope ?? '',
  titles: ['pull','fetch','chkout','commit','push'].map((a) => document.querySelector('[data-testid="action-' + a + '"]')?.title ?? ''),
}))()`)
report.checks.actionMatrix = { ok: actionState.pullDisabled !== null, detail: actionState }

if (actionState.pullDisabled === false || (await evaluate(`document.querySelector('[data-testid="action-fetch"]')?.disabled`)) === false) {
  await clickSelector('[data-testid="action-fetch"]')
  const m1ToastOk = await waitFor(
    `[...document.querySelectorAll('[data-testid="toast"]')].some((t) => t.textContent.includes('M1 只读'))`,
    5000,
  )
  const toast = await evaluate(`[...document.querySelectorAll('[data-testid="toast"]')].map((t) => t.textContent.trim()).join(' | ')`)
  const lastOp = await evaluate(`document.querySelector('[data-testid="status-last-op"]')?.textContent?.trim() ?? ''`)
  report.checks.m1ActionToast = { ok: m1ToastOk && lastOp.includes('E_NOT_IMPLEMENTED'), detail: { toast, lastOp } }
  await key('Escape', 'Escape', 27)
  await sleep(200)
}

// 10 个 CI 锚点（按 spec §8.1 打开对应浮层后断言，各恰好 1 个）
const anchorCounts = {}
const countAnchor = async (id) => {
  anchorCounts[id] = await evaluate(`document.querySelectorAll('[data-testid="${id}"]').length`)
}
await countAnchor('ci-nav')
await countAnchor('ci-slot')
await countAnchor('ci-strip')
await countAnchor('ci-tab')
await countAnchor('ci-queue')
await countAnchor('ci-card-base')

await clickSelector('[data-testid="ci-nav"]')
await sleep(300)
await countAnchor('ci-contract')
await countAnchor('ci-page')
report.checks.ciModal = { ok: (anchorCounts['ci-contract'] ?? 0) === 1, detail: anchorCounts }
report.screenshots.push(await shot('ci-modal'))
await key('Escape', 'Escape', 27)
await sleep(200)

await clickSelector('[data-testid="title-settings"]')
await sleep(300)
await countAnchor('ci-settings')
report.screenshots.push(await shot('settings-modal'))
await key('Escape', 'Escape', 27)
await sleep(200)

await clickSelector('[data-testid="title-notifications"]')
await sleep(300)
await countAnchor('ci-notif-category')
report.screenshots.push(await shot('notifications-drawer'))
await key('Escape', 'Escape', 27)
await sleep(200)

await clickSelector('[data-testid="component-filter"]')
await sleep(300)
await countAnchor('ci-facet')
report.screenshots.push(await shot('filter-popover'))
await key('Escape', 'Escape', 27)
await sleep(200)

report.anchors = anchorCounts
const expectedAnchors = ['ci-nav', 'ci-strip', 'ci-tab', 'ci-queue', 'ci-notif-category', 'ci-facet', 'ci-settings', 'ci-contract', 'ci-card-base']
const cardCountNow = await evaluate(`document.querySelectorAll('[data-testid="component-card"]').length`)
report.checks.ciAnchors10 = {
  // CI-2（ci-slot）落在每张组件卡 L1，故按卡片数出现；其余 9 个锚点 DOM 唯一（spec §8.1）
  ok:
    expectedAnchors.every((id) => anchorCounts[id] === 1) &&
    anchorCounts['ci-slot'] === cardCountNow &&
    cardCountNow >= 6,
  detail: anchorCounts,
}

// 1280×800 基线
await send('Emulation.setDeviceMetricsOverride', { width: 1280, height: 800, deviceScaleFactor: 1, mobile: false })
await sleep(400)
const layout1280 = await evaluate(`(() => {
  const r = (s) => { const el = document.querySelector(s); if (!el) return null; const b = el.getBoundingClientRect(); return { w: b.width, h: b.height, bottom: b.bottom }; };
  const panels = [...document.querySelectorAll('[data-testid="repo-tree-panel"]')].map((el) => el.getBoundingClientRect().width);
  return { componentList: r('[data-testid="component-list"]'), infoPanel: r('[data-testid="info-panel"]'), repoActions: r('[data-testid="repo-actions"]'), panels, scrollWidth: document.scrollingElement.scrollWidth, innerWidth: window.innerWidth };
})()`)
report.layout1280 = layout1280
report.checks.width1280 = {
  ok:
    near(layout1280.componentList.w, 249.6, 6) &&
    near(layout1280.infoPanel.w, 224.6, 6) &&
    near(layout1280.panels[0], 368.9, 3) &&
    layout1280.scrollWidth <= layout1280.innerWidth + 1,
  detail: layout1280,
}
report.screenshots.push(await shot('workbench-1280'))

// 回到 1440 截图基线
await send('Emulation.setDeviceMetricsOverride', { width: 1440, height: 900, deviceScaleFactor: 1, mobile: false })
await sleep(400)
report.screenshots.push(await shot('workbench-1440'))

// 文件夹视图
await clickSelector('[data-testid="view-folder"]')
await sleep(600)
const folder = await evaluate(`(() => ({
  folderView: Boolean(document.querySelector('[data-testid="folder-view"]')),
  workspaceTree: Boolean(document.querySelector('[data-testid="workspace-tree"]')),
  repoGrid: Boolean(document.querySelector('[data-testid="repo-grid"]')),
  nodes: document.querySelectorAll('[data-testid="workspace-node"]').length,
  kinds: [...new Set([...document.querySelectorAll('[data-testid="workspace-node"]')].map((n) => n.dataset.kind))],
  componentList: Boolean(document.querySelector('[data-testid="component-list"]')),
  infoPanel: Boolean(document.querySelector('[data-testid="info-panel"]')),
}))()`)
report.checks.folderView = {
  ok: folder.folderView && folder.workspaceTree && !folder.repoGrid && folder.nodes > 0 && folder.kinds.includes('COMPONENT') && folder.componentList && folder.infoPanel,
  detail: folder,
}
const workspaceBefore = folder.nodes
await clickSelector('[data-testid="workspace-toggle"]')
await sleep(400)
const workspaceAfter = await evaluate(`document.querySelectorAll('[data-testid="workspace-node"]').length`)
report.checks.folderExpand = { ok: workspaceAfter > workspaceBefore, detail: { before: workspaceBefore, after: workspaceAfter } }
report.screenshots.push(await shot('folder-1440'))
await clickSelector('[data-testid="view-workbench"]')
await sleep(400)

// MISSING 组件 → 空态 + 五键禁用
const missingCard = await evaluate(`(() => {
  const id = ${JSON.stringify('CF111D_BrakeCtrl')};
  const card = document.querySelector('[data-component-id="' + id + '"]');
  if (!card) return null;
  const b = card.getBoundingClientRect();
  return { x: b.x + b.width / 2, y: b.y + b.height / 2 };
})()`)
if (missingCard) {
  await mouseClick(missingCard.x, missingCard.y)
  await sleep(500)
  const missing = await evaluate(`(() => ({
    emptyPanels: document.querySelectorAll('[data-testid="repo-tree-panel"] [data-testid="repo-tree-empty"]').length,
    disabled: ['pull','fetch','chkout','commit','push'].map((a) => document.querySelector('[data-testid="action-' + a + '"]')?.disabled),
    titles: ['pull','fetch','chkout','commit','push'].map((a) => document.querySelector('[data-testid="action-' + a + '"]')?.title ?? ''),
  }))()`)
  report.checks.missingState = {
    ok: missing.emptyPanels === 4 && missing.disabled.every((value) => value === true) && missing.titles.every((title) => title.length > 0),
    detail: missing,
  }
  report.screenshots.push(await shot('action-disabled'))
}

// 深色主题 + 主题 token 生效
await send('Emulation.setEmulatedMedia', { features: [{ name: 'prefers-color-scheme', value: 'dark' }] })
await sleep(300)
const dark = await evaluate(`(() => ({ bodyBg: getComputedStyle(document.body).backgroundColor, surface: getComputedStyle(document.documentElement).getPropertyValue('--surface').trim(), cardBg: getComputedStyle(document.querySelector('[data-testid="component-card"]')).backgroundColor }))()`)
report.checks.darkTheme = { ok: dark.bodyBg !== 'rgb(246, 247, 249)', detail: dark }
report.screenshots.push(await shot('workbench-dark'))
await send('Emulation.setEmulatedMedia', { features: [] })

// 键盘：Ctrl+K 聚焦搜索
await key('k', 'KeyK', 75, 2)
const focusedSearch = await evaluate(`document.activeElement?.getAttribute('data-testid') ?? document.activeElement?.tagName`)
report.checks.ctrlK = { ok: focusedSearch === 'title-search', detail: focusedSearch }

// 搜索过滤
await evaluate(`(() => {
  const input = document.querySelector('[data-testid="title-search"]');
  const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set;
  setter.call(input, 'CF111D');
  input.dispatchEvent(new Event('input', { bubbles: true }));
})()`)
await sleep(400)
const filtered = await evaluate(`({ cards: document.querySelectorAll('[data-testid="component-card"]').length, countText: document.querySelector('[data-testid="component-list-count"]')?.textContent?.trim() })`)
report.checks.searchFilter = { ok: filtered.cards === 1, detail: filtered }
await evaluate(`(() => {
  const input = document.querySelector('[data-testid="title-search"]');
  const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set;
  setter.call(input, '');
  input.dispatchEvent(new Event('input', { bubbles: true }));
})()`)
await sleep(300)

// 控制台错误
report.consoleErrors = consoleErrors
report.exceptions = exceptions
report.checks.consoleClean = { ok: exceptions.length === 0 && consoleErrors.length === 0, detail: { exceptions, consoleErrors } }

// 右键菜单（真实右键事件）
const ctxTarget = await rect('[data-testid="component-card"]')
if (ctxTarget) {
  const cx = ctxTarget.x + 60
  const cy = ctxTarget.y + 30
  await send('Input.dispatchMouseEvent', { type: 'mousePressed', x: cx, y: cy, button: 'right', clickCount: 1 })
  await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x: cx, y: cy, button: 'right', clickCount: 1 })
  await sleep(300)
  const ctx = await evaluate(`(() => {
    const menu = document.querySelector('[data-testid="context-menu"]');
    if (!menu) return { count: 0 };
    const labels = [...menu.querySelectorAll('button')].map((b) => b.textContent.trim());
    const danger = [...menu.querySelectorAll('button.danger')].map((b) => getComputedStyle(b).color);
    return { count: 1, labels, danger };
  })()`)
  report.checks.contextMenu = {
    ok:
      ctx.count === 1 &&
      ['SYNC', 'CREATE', 'Clone', 'Delete', '复制组件 ID', '在资源管理器中打开'].every((needle) =>
        ctx.labels.some((label) => label.includes(needle)),
      ) &&
      (ctx.danger ?? []).length > 0,
    detail: ctx,
  }
  report.screenshots.push(await shot('context-menu'))
  await key('Escape', 'Escape', 27)
  await sleep(200)
}

// 批量选择模式（左栏底部批量条）
await clickSelector('[data-testid="batch-select-toggle"]')
await sleep(300)
const batch = await evaluate(`({
  checkboxes: document.querySelectorAll('[data-testid="component-card"] .check').length,
  hint: document.querySelector('.select-hint')?.textContent?.replace(/\\s+/g, ' ').trim() ?? '',
  bar: document.querySelector('[data-testid="batch-bar"]')?.textContent?.replace(/\\s+/g, ' ').trim() ?? '',
})`)
report.checks.batchMode = { ok: batch.checkboxes >= 6 && batch.hint.includes('已选'), detail: batch }
await clickSelector('[data-testid="batch-select-toggle"]')
await sleep(250)

// 键盘导航（列表 role=listbox）
await evaluate(`document.querySelector('[data-testid="ci-card-base"]').focus()`)
await key('ArrowDown', 'ArrowDown', 40)
await sleep(300)
const keyboard = await evaluate(`(() => ({
  selected: document.querySelector('[data-testid="component-card"][aria-selected="true"]')?.dataset.componentId ?? null,
  active: document.activeElement?.getAttribute('data-component-id') ?? document.activeElement?.tagName,
}))()`)
report.checks.keyboardNav = { ok: Boolean(keyboard.selected), detail: keyboard }

// Mock 模式下不应出现「Host 方法缺失」回退标签
const fallbackTags = await evaluate(`document.querySelectorAll('[data-testid="repo-tree-panel"] .fallback').length`)
report.checks.noMockFallbackTag = { ok: fallbackTags === 0, detail: { fallbackTags } }

// 1024×720 紧凑档：无横向溢出、区域可达
await send('Emulation.setDeviceMetricsOverride', { width: 1024, height: 720, deviceScaleFactor: 1, mobile: false })
await sleep(400)
const compact = await evaluate(`(() => {
  const r = (s) => { const el = document.querySelector(s); if (!el) return null; const b = el.getBoundingClientRect(); return { w: +b.width.toFixed(1), h: +b.height.toFixed(1) }; };
  return {
    scrollWidth: document.scrollingElement.scrollWidth,
    innerWidth,
    componentList: r('[data-testid="component-list"]'),
    infoPanel: r('[data-testid="info-panel"]'),
    repoActions: r('[data-testid="repo-actions"]'),
    cards: document.querySelectorAll('[data-testid="component-card"]').length,
  };
})()`)
report.checks.compact1024 = {
  ok: compact.scrollWidth <= compact.innerWidth + 1 && compact.componentList.w >= 200 && compact.infoPanel.w >= 190 && compact.cards >= 6,
  detail: compact,
}
report.screenshots.push(await shot('workbench-1024'))
await send('Emulation.setDeviceMetricsOverride', { width: 1440, height: 900, deviceScaleFactor: 1, mobile: false })
await sleep(300)

const checks = report.checks
const failed = Object.entries(checks).filter(([, value]) => !value.ok).map(([key]) => key)
report.summary = { total: Object.keys(checks).length, passed: Object.keys(checks).length - failed.length, failed }

console.log(JSON.stringify(report, null, 2))
ws.close()
if (failed.length > 0) process.exitCode = 1
