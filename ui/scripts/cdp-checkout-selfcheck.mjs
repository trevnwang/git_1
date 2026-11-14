#!/usr/bin/env node
/**
 * M2 P0-7 CHKOUT 选择器 CDP 自测（零依赖，Node ≥22 内置 WebSocket）。
 *
 * 验证：getRemoteRefs 接入（分组 本地/远程/Tag）、搜索（refName/shortSha）、点击填入 branch|tag、
 * 最多渲染 200、dirty 仓 stash -u 勾选与 args.stash=true、接口失败回退当前分支/Tag 建议且不报错。
 *
 * 用法：
 *   npm run build && npm run preview -- --port 4173
 *   msedge --headless=new --remote-debugging-port=9336 about:blank
 *   node scripts/cdp-checkout-selfcheck.mjs --cdp 9336 --url http://127.0.0.1:4173
 * 截图/报告默认写入 %TEMP%/dsh-checkout-selfcheck。
 */
import { mkdir, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

const args = process.argv.slice(2)
function arg(name, fallback) {
  const at = args.indexOf(`--${name}`)
  return at >= 0 && args[at + 1] ? args[at + 1] : fallback
}

const CDP = Number(arg('cdp', '9336'))
const URL = arg('url', 'http://127.0.0.1:4173')
const OUT = arg('out', join(tmpdir(), 'dsh-checkout-selfcheck'))
const WIDTH = Number(arg('width', '1440'))
const HEIGHT = Number(arg('height', '900'))

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms))

/** 注入页面的 Mock Host 桥（M2 契约：getRemoteRefs + CHKOUT args.stash）。 */
function bridgeFactory() {
  const HEAD = 'abcdef1234567890abcdef1234567890abcdef12'
  const cfg = {
    features: { fileTree: true, workspaceTree: true, openPath: true, repoActions: true, ci: false, componentDetail: true, remoteRefs: true, repoActionStash: true },
    remoteRefsCalls: [],
    actionCalls: [],
    failRemoteRefs: false,
    largeRefs: false,
  }
  window.__CHKOUT_CFG__ = cfg

  const REFS = [
    { refName: 'refs/heads/main', sha: HEAD, shortSha: 'abcdef1', kind: 'LOCAL_BRANCH' },
    { refName: 'refs/heads/dev', sha: 'bbbbbbb1234567890abcdef1234567890abcdef12', shortSha: 'bbbbbbb', kind: 'LOCAL_BRANCH' },
    { refName: 'refs/heads/feature/foo', sha: 'feedfacecafe1234567890abcdef1234567890ab', shortSha: 'feedfac', kind: 'LOCAL_BRANCH' },
    { refName: 'refs/remotes/origin/main', sha: HEAD, shortSha: 'abcdef1', kind: 'REMOTE_BRANCH' },
    { refName: 'refs/remotes/origin/dev', sha: 'bbbbbbb1234567890abcdef1234567890abcdef12', shortSha: 'bbbbbbb', kind: 'REMOTE_BRANCH' },
    { refName: 'refs/remotes/origin/release/x', sha: 'ccccccc1234567890abcdef1234567890abcdef12', shortSha: 'ccccccc', kind: 'REMOTE_BRANCH' },
    { refName: 'refs/tags/v1.0.0', sha: '11111111234567890abcdef1234567890abcdef12', shortSha: '1111111', kind: 'TAG' },
    { refName: 'refs/tags/v1.2.0', sha: '22222222234567890abcdef1234567890abcdef12', shortSha: '2222222', kind: 'TAG' },
    { refName: 'refs/tags/release-2026', sha: '33333333234567890abcdef1234567890abcdef12', shortSha: '3333333', kind: 'TAG' },
  ]

  function repo(type, state, extra) {
    return Object.assign(
      {
        repoType: type,
        repoName: `Mock_${type}`,
        state,
        subType: null,
        branch: 'main',
        tag: null,
        headSha: HEAD,
        upstreamRef: 'refs/remotes/origin/main',
        compareRef: 'refs/remotes/origin/main',
        ahead: 0,
        behind: 0,
        dirtyCount: 0,
        untrackedCount: 0,
        stashCount: 0,
        isLocal: true,
        localPath: `D:\\Mock\\Repo_${type}`,
        isStale: false,
        calcAt: new Date().toISOString(),
        dataSource: 'local',
        errorCode: null,
        flags: [],
      },
      extra || {},
    )
  }

  function component(id, state, severityRank, repos) {
    return {
      componentId: id,
      aggregateState: state,
      severityRank,
      branch: 'main',
      tag: null,
      mixedBranch: false,
      flags: [],
      incomplete: false,
      lastActivityAt: new Date(Date.now() - severityRank * 60000).toISOString(),
      ciAggregate: { componentId: id, byState: { NONE: 4 }, worstState: 'NONE', dataFreshAt: new Date().toISOString() },
      repos,
    }
  }

  const clean = component('C1_CLEAN', 'SYNCED', 10, [
    repo('DESIGN', 'SYNCED'),
    repo('IMPL', 'SYNCED'),
    repo('MIL', 'SYNCED'),
    repo('SIL', 'SYNCED'),
  ])
  const dirty = component('C2_DIRTY', 'DIRTY', 20, [
    repo('DESIGN', 'DIRTY', { dirtyCount: 1 }),
    repo('IMPL', 'DIRTY', { dirtyCount: 2, untrackedCount: 1 }),
    repo('MIL', 'DIRTY', { dirtyCount: 1 }),
    repo('SIL', 'DIRTY', { untrackedCount: 2 }),
  ])
  const components = [clean, dirty]

  function findComponent(componentId) {
    return components.find((item) => item.componentId === componentId) || null
  }

  function findRepo(componentId, repoType) {
    const item = findComponent(componentId)
    return (item && item.repos.find((row) => row.repoType === repoType)) || null
  }

  function snapshot() {
    return {
      source: 'local',
      workspaceRoot: 'D:\\Mock\\Workspace',
      error: null,
      generatedAt: new Date().toISOString(),
      components,
      counts: { components: components.length, repos: 8, missingRepos: 0 },
      freshness: { localScanAt: new Date().toISOString(), remoteFetchAt: new Date().toISOString(), localStale: false, remoteStale: false },
    }
  }

  function remoteRefs(params) {
    cfg.remoteRefsCalls.push(params || {})
    if (cfg.failRemoteRefs) {
      return { ok: false, code: 'E_GIT_FAILED', error: 'Mock：读取 refs 失败（注入）' }
    }
    let refs = REFS
    if (cfg.largeRefs) {
      refs = [...REFS]
      for (let i = 1; i <= 250; i += 1) {
        refs.push({
          refName: `refs/heads/bulk-${String(i).padStart(3, '0')}`,
          sha: `${String(i).padStart(4, '0')}ffff1234567890abcdef1234567890abcdef12`,
          shortSha: `${String(i).padStart(4, '0')}fff`,
          kind: 'LOCAL_BRANCH',
        })
      }
    }
    return { refs, truncated: cfg.largeRefs, total: refs.length, generatedAt: new Date().toISOString() }
  }

  const bridge = {
    handle(request) {
      const method = request.method
      const params = request.params || {}
      if (method === 'getHostInfo') {
        return { appVersion: '0.3.0-m2-mock', webView2Version: 'mock', features: cfg.features, limits: { maxTreeEntries: 2000 } }
      }
      if (method === 'getSnapshot') return snapshot()
      if (method === 'refresh') return { refreshed: true }
      if (method === 'getRemoteRefs') return remoteRefs(params)
      if (method === 'getRepoTree') {
        return { componentId: params.componentId, repoType: params.repoType, repoName: `Mock_${params.repoType}`, rootPath: 'D:\\Mock', path: '', branch: 'main', headSha: HEAD, state: 'SYNCED', dirtyCount: 0, compareRef: 'refs/remotes/origin/main', fetchedAt: new Date().toISOString(), entries: [], truncated: false, totalEntries: 0, error: null, errorCode: null }
      }
      if (method === 'getWorkspaceTree') {
        return { root: 'D:\\Mock\\Workspace', path: '', generatedAt: new Date().toISOString(), readAt: new Date().toISOString(), entries: [], truncated: false, totalEntries: 0, error: null, errorCode: null }
      }
      if (method === 'getComponentDetail') return findComponent(params.componentId) || { ok: false, code: 'E_NOT_FOUND', error: 'Mock：组件不存在' }
      if (method === 'getRepoDetail') {
        return { repo: findRepo(params.componentId, params.repoType), component: findComponent(params.componentId), commits: { total: 0, items: [] } }
      }
      if (method === 'getCommits') return { total: 0, items: [] }
      if (method === 'openPath') return { opened: true, mode: params.mode || 'open', path: params.path }
      if (method === 'runRepoAction') {
        const action = params.action
        const actArgs = params.args || {}
        cfg.actionCalls.push({ action, componentId: params.componentId, repoType: params.repoType, args: actArgs })
        if (action === 'CHKOUT' && params.componentId === 'C2_DIRTY' && actArgs.stash !== true) {
          return { ok: false, code: 'E_DIRTY_WORKTREE', error: 'Mock：工作区存在未提交变更（含未跟踪文件），CHKOUT 前请先提交或 stash（或传 args.stash=true）' }
        }
        if (!actArgs.branch && !actArgs.tag) {
          return { ok: false, code: 'E_INVALID_ARGUMENT', error: 'Mock：branch/tag 二选一' }
        }
        return {
          accepted: true,
          action,
          componentId: params.componentId,
          repoType: params.repoType,
          state: 'SYNCED',
          preHead: HEAD,
          postHead: '1234567' + HEAD.slice(7),
          branch: actArgs.tag || actArgs.branch || 'main',
          stashRef: actArgs.stash === true ? 'stash@{0}' : null,
          output: 'mock output',
          durationMs: 42,
          message: 'ok',
        }
      }
      return { ok: false, code: 'E_UNKNOWN_METHOD', error: `Mock 未实现：${method}` }
    },
  }
  window.__DSH_TEST_BRIDGE__ = bridge
}

async function connect() {
  for (let attempt = 0; attempt < 40; attempt += 1) {
    try {
      const targets = await fetch(`http://127.0.0.1:${CDP}/json/list`).then((r) => r.json())
      const pages = targets.filter((t) => t.type === 'page' && t.webSocketDebuggerUrl)
      const page = pages.find((t) => /^http/.test(t.url ?? '')) ?? pages[0]
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
  if (msg.method === 'Runtime.exceptionThrown') exceptions.push(msg.params.exceptionDetails?.text ?? 'exception')
  if (msg.method === 'Runtime.consoleAPICalled' && msg.params.type === 'error') {
    consoleErrors.push((msg.params.args ?? []).map((a) => a.value ?? a.description ?? '').join(' '))
  }
  if (msg.method === 'Log.entryAdded' && msg.params.entry.level === 'error') {
    const entry = msg.params.entry
    consoleErrors.push(`${entry.url ?? entry.source ?? ''} ${entry.text}`.trim())
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
    await sleep(100)
  }
  return false
}

async function rect(selector) {
  return evaluate(`(() => {
    const el = document.querySelector(${JSON.stringify(selector)});
    if (!el) return null;
    const r = el.getBoundingClientRect();
    return { x: r.x, y: r.y, w: r.width, h: r.height };
  })()`)
}

async function click(selector) {
  const target = await rect(selector)
  if (!target) throw new Error(`元素不存在：${selector}`)
  const x = target.x + target.w / 2
  const y = target.y + target.h / 2
  await send('Input.dispatchMouseEvent', { type: 'mouseMoved', x, y, button: 'none', clickCount: 0 })
  await send('Input.dispatchMouseEvent', { type: 'mousePressed', x, y, button: 'left', clickCount: 1 })
  await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x, y, button: 'left', clickCount: 1 })
  await sleep(150)
}

/** 程序化 click：用于被 Toast / 浮层遮挡的底部按钮，避免坐标点击被吞。 */
async function clickJs(selector) {
  const found = await evaluate(`(() => {
    const el = document.querySelector(${JSON.stringify(selector)});
    if (!el) return false;
    el.click();
    return true;
  })()`)
  if (!found) throw new Error(`元素不存在：${selector}`)
  await sleep(180)
}

/** 关闭遗留 Toast，避免其覆盖对话框底部按钮（toast pointer-events:auto）。 */
async function dismissToasts() {
  await evaluate(`[...document.querySelectorAll('[data-testid="toast"]')].forEach((el) => el.click())`)
  await sleep(120)
}

/** 选中组件并等待选中态生效。 */
async function selectComponent(componentId) {
  await clickJs(`[data-component-id="${componentId}"]`)
  await waitFor(
    `document.querySelector('[data-component-id="${componentId}"]')?.getAttribute('aria-selected') === 'true'`,
    5000,
  )
  await waitFor(`document.querySelector('[data-testid="action-chkout"]')?.disabled === false`, 10000)
}

async function key(keyName, code, vk) {
  await send('Input.dispatchKeyEvent', { type: 'keyDown', key: keyName, code, windowsVirtualKeyCode: vk, nativeVirtualKeyCode: vk })
  await send('Input.dispatchKeyEvent', { type: 'keyUp', key: keyName, code, windowsVirtualKeyCode: vk, nativeVirtualKeyCode: vk })
  await sleep(140)
}

async function setValue(selector, value) {
  await evaluate(`(() => {
    const el = document.querySelector(${JSON.stringify(selector)});
    if (!el) throw new Error('missing ${selector}');
    const proto = el instanceof HTMLTextAreaElement ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype;
    const setter = Object.getOwnPropertyDescriptor(proto, 'value').set;
    setter.call(el, ${JSON.stringify(value)});
    el.dispatchEvent(new Event('input', { bubbles: true }));
    el.focus();
  })()`)
  await sleep(140)
}

async function setChecked(selector, checked) {
  await evaluate(`(() => {
    const el = document.querySelector(${JSON.stringify(selector)});
    if (!el) throw new Error('missing ${selector}');
    const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'checked').set;
    setter.call(el, ${checked ? 'true' : 'false'});
    el.dispatchEvent(new Event('input', { bubbles: true }));
    el.dispatchEvent(new Event('change', { bubbles: true }));
  })()`)
  await sleep(120)
}

async function toastText() {
  return evaluate(`[...document.querySelectorAll('[data-testid="toast"]')].map((t) => t.textContent.trim()).join(' | ')`)
}

async function waitToast(needle, timeout = 6000) {
  return waitFor(
    `[...document.querySelectorAll('[data-testid="toast"]')].some((t) => t.textContent.includes(${JSON.stringify(needle)}))`,
    timeout,
  )
}

async function shot(name) {
  const result = await send('Page.captureScreenshot', { format: 'png' })
  await writeFile(join(OUT, `${name}.png`), Buffer.from(result.data, 'base64'))
  return `${name}.png`
}

await send('Page.enable')
await send('Runtime.enable')
await send('Log.enable')
await mkdir(OUT, { recursive: true })
await send('Emulation.setDeviceMetricsOverride', { width: WIDTH, height: HEIGHT, deviceScaleFactor: 1, mobile: false })
await send('Page.addScriptToEvaluateOnNewDocument', { source: `(${bridgeFactory.toString()})()` })
await send('Page.navigate', { url: URL })

const report = { url: URL, viewport: { width: WIDTH, height: HEIGHT }, out: OUT, checks: {}, screenshots: [] }
const check = (name, ok, detail) => {
  report.checks[name] = { ok: Boolean(ok), detail }
}

async function openCheckout() {
  await click('[data-testid="action-chkout"]')
  await waitFor(`Boolean(document.querySelector('[data-testid="action-dialog"]'))`, 5000)
  await waitFor(`document.querySelectorAll('[data-testid="checkout-ref-item"]').length > 0`, 8000)
}

async function waitRefsLoaded() {
  return waitFor(
    `(() => {
      const d = document.querySelector('[data-testid="action-dialog"]');
      if (!d || d.dataset.kind !== 'checkout') return false;
      return Boolean(document.querySelector('[data-testid="checkout-ref-groups"]')) &&
        !document.querySelector('[data-testid="checkout-ref-loading"]');
    })()`,
    8000,
  )
}

// 1) 启动 + 选中干净组件，打开 CHKOUT → 真实 refs 分组加载
const booted = await waitFor(`document.querySelectorAll('[data-testid="component-card"]').length === 2`, 15000)
check('boot', booted, { cards: await evaluate(`document.querySelectorAll('[data-testid="component-card"]').length`) })
await click('[data-component-id="C1_CLEAN"]')
const chkoutReady = await waitFor(`document.querySelector('[data-testid="action-chkout"]')?.disabled === false`, 10000)
check('chkoutEnabled', chkoutReady, await evaluate(`(() => { const el = document.querySelector('[data-testid="action-chkout"]'); return el ? { disabled: el.disabled, title: el.title } : null })()`))
await openCheckout()
await waitRefsLoaded()
const dialogShape = await evaluate(`(() => {
  const dialog = document.querySelector('[data-testid="action-dialog"]');
  const search = document.querySelector('[data-testid="checkout-ref-search"]');
  const items = [...document.querySelectorAll('[data-testid="checkout-ref-item"]')];
  return {
    exists: Boolean(dialog),
    kind: dialog?.dataset.kind,
    searchType: search?.getAttribute('type'),
    groups: ['LOCAL_BRANCH', 'REMOTE_BRANCH', 'TAG'].map((k) => ({
      key: k,
      count: document.querySelectorAll('[data-testid="checkout-ref-group-' + k + '"] [data-testid="checkout-ref-item"]').length,
    })),
    total: items.length,
    source: document.querySelector('[data-testid="checkout-ref-source-inline"]')?.textContent?.trim() || '',
    remoteCalls: window.__CHKOUT_CFG__.remoteRefsCalls.length,
    lastCall: window.__CHKOUT_CFG__.remoteRefsCalls.at(-1) || null,
  };
})()`)
check(
  'refsLoadedGrouped',
  dialogShape.exists &&
    dialogShape.kind === 'checkout' &&
    dialogShape.searchType === 'search' &&
    dialogShape.groups.find((g) => g.key === 'LOCAL_BRANCH')?.count === 3 &&
    dialogShape.groups.find((g) => g.key === 'REMOTE_BRANCH')?.count === 3 &&
    dialogShape.groups.find((g) => g.key === 'TAG')?.count === 3 &&
    dialogShape.remoteCalls === 1 &&
    dialogShape.lastCall?.componentId === 'C1_CLEAN' &&
    dialogShape.lastCall?.repoType === 'IMPL',
  dialogShape,
)
report.screenshots.push(await shot('chkout-01-refs-loaded'))

// 2) 搜索按 refName 过滤（dev → 本地 dev + 远程 origin/dev）
await setValue('[data-testid="checkout-ref-search"]', 'dev')
const filtered = await evaluate(`(() => {
  const items = [...document.querySelectorAll('[data-testid="checkout-ref-item"]')];
  return {
    count: items.length,
    names: items.map((el) => el.dataset.refName),
    groups: [...document.querySelectorAll('[data-testid="checkout-ref-groups"] section')].map((s) => s.dataset.testid),
  };
})()`)
check(
  'searchByRefName',
  filtered.count === 2 &&
    filtered.names.includes('refs/heads/dev') &&
    filtered.names.includes('refs/remotes/origin/dev') &&
    filtered.names.every((name) => name.toLowerCase().includes('dev')),
  filtered,
)
report.screenshots.push(await shot('chkout-02-search-dev'))

// 3) 搜索按 shortSha 过滤（feedfac → feature/foo）
await setValue('[data-testid="checkout-ref-search"]', 'feedfac')
const shaFiltered = await evaluate(`[...document.querySelectorAll('[data-testid="checkout-ref-item"]')].map((el) => ({ name: el.dataset.refName, kind: el.dataset.refKind }))`)
check('searchByShortSha', shaFiltered.length === 1 && shaFiltered[0].name === 'refs/heads/feature/foo', shaFiltered)

// 4) 点击本地分支项 → 填入 branch
await click('[data-testid="checkout-ref-item"][data-ref-name="refs/heads/feature/foo"]')
const pickedLocal = await evaluate(`(() => ({
  value: document.querySelector('[data-testid="action-dialog-ref"]')?.value,
  branchChecked: document.querySelector('[data-testid="checkout-kind-branch"]')?.checked,
  picked: document.querySelector('[data-testid="checkout-ref-item"][data-ref-name="refs/heads/feature/foo"]')?.getAttribute('aria-selected'),
}))()`)
check('pickLocalBranch', pickedLocal.value === 'feature/foo' && pickedLocal.branchChecked === true && pickedLocal.picked === 'true', pickedLocal)

// 5) 搜索并点击远程分支项 → 填入去掉 origin/ 的 branch
await setValue('[data-testid="checkout-ref-search"]', 'origin/dev')
await click('[data-testid="checkout-ref-item"][data-ref-name="refs/remotes/origin/dev"]')
const pickedRemote = await evaluate(`(() => ({
  value: document.querySelector('[data-testid="action-dialog-ref"]')?.value,
  kind: document.querySelector('[data-testid="checkout-ref-item"][data-ref-name="refs/remotes/origin/dev"]')?.dataset.refKind,
  branchChecked: document.querySelector('[data-testid="checkout-kind-branch"]')?.checked,
}))()`)
check('pickRemoteBranch', pickedRemote.value === 'dev' && pickedRemote.kind === 'REMOTE_BRANCH' && pickedRemote.branchChecked === true, pickedRemote)

// 6) 搜索并点击 Tag → 切换 tag + 填入
await setValue('[data-testid="checkout-ref-search"]', 'v1.2')
await click('[data-testid="checkout-ref-item"][data-ref-name="refs/tags/v1.2.0"]')
const pickedTag = await evaluate(`(() => ({
  value: document.querySelector('[data-testid="action-dialog-ref"]')?.value,
  tagChecked: document.querySelector('[data-testid="checkout-kind-tag"]')?.checked,
}))()`)
check('pickTag', pickedTag.value === 'v1.2.0' && pickedTag.tagChecked === true, pickedTag)
report.screenshots.push(await shot('chkout-03-picked-tag'))

// 7) 提交干净仓：args = { tag, confirm:true }，无 branch、无 stash
await click('[data-testid="action-dialog-confirm"]')
const cleanToast = await waitToast('已检出', 8000)
const cleanCall = await evaluate(`window.__CHKOUT_CFG__.actionCalls.filter((c) => c.action === 'CHKOUT').at(-1) || null`)
check(
  'submitTagArgs',
  cleanToast &&
    cleanCall?.args.tag === 'v1.2.0' &&
    cleanCall?.args.confirm === true &&
    cleanCall?.args.branch === undefined &&
    cleanCall?.args.stash === undefined,
  { cleanToast, cleanCall, toast: await toastText() },
)
report.screenshots.push(await shot('chkout-04-checkout-success'))

// 8) dirty 仓：显示 stash 勾选；未勾选提交 → E_DIRTY_WORKTREE 错误提示
await click('[data-component-id="C2_DIRTY"]')
await waitFor(`document.querySelector('[data-testid="action-chkout"]')?.disabled === false`, 10000)
await openCheckout()
await waitRefsLoaded()
const dirtyShape = await evaluate(`(() => {
  const stash = document.querySelector('[data-testid="checkout-stash"]');
  return {
    stashExists: Boolean(stash),
    stashChecked: stash?.checked,
    hint: document.querySelector('[data-testid="checkout-stash-hint"]')?.textContent?.replace(/\\s+/g, ' ').trim() || '',
    body: document.querySelector('[data-testid="action-dialog"] .dialog-text')?.textContent?.replace(/\\s+/g, ' ').trim() || '',
  };
})()`)
check('dirtyStashCheckbox', dirtyShape.stashExists && dirtyShape.stashChecked === false && dirtyShape.hint.includes('E_DIRTY_WORKTREE'), dirtyShape)
report.screenshots.push(await shot('chkout-05-dirty-dialog'))

await evaluate(`(() => {
  const previous = window.__CHKOUT_CFG__.actionCalls.length;
  window.__CHKOUT_DIRTY_MARK__ = previous;
})()`)
await click('[data-testid="checkout-ref-item"][data-ref-name="refs/heads/dev"]').catch(async () => {
  // 列表可能已按上一步 search 过滤（重开时 search 为空，应存在 dev）
  await setValue('[data-testid="checkout-ref-search"]', 'dev')
  await click('[data-testid="checkout-ref-item"][data-ref-name="refs/heads/dev"]')
})
await click('[data-testid="action-dialog-confirm"]')
const dirtyErrorToast = await waitToast('E_DIRTY_WORKTREE', 8000)
const dirtyErrorCall = await evaluate(`window.__CHKOUT_CFG__.actionCalls.filter((c) => c.action === 'CHKOUT').at(-1) || null`)
check(
  'dirtyWithoutStashRejected',
  dirtyErrorToast && dirtyErrorCall?.args.branch === 'dev' && dirtyErrorCall?.args.confirm === true && dirtyErrorCall?.args.stash === undefined,
  { dirtyErrorToast, dirtyErrorCall, toast: await toastText() },
)
report.screenshots.push(await shot('chkout-06-dirty-error'))

// 9) dirty 仓：勾选 stash → args = { branch, confirm:true, stash:true }；Toast 含自动 stash
await openCheckout()
await waitRefsLoaded()
await click('[data-testid="checkout-ref-item"][data-ref-name="refs/heads/dev"]').catch(async () => {
  await setValue('[data-testid="checkout-ref-search"]', 'dev')
  await click('[data-testid="checkout-ref-item"][data-ref-name="refs/heads/dev"]')
})
await setChecked('[data-testid="checkout-stash"]', true)
const stashHintChecked = await evaluate(`document.querySelector('[data-testid="checkout-stash-hint"]')?.textContent?.replace(/\\s+/g, ' ').trim() || ''`)
report.screenshots.push(await shot('chkout-07-stash-checked'))
await click('[data-testid="action-dialog-confirm"]')
const stashToast = await waitToast('自动 stash', 8000)
const stashCall = await evaluate(`window.__CHKOUT_CFG__.actionCalls.filter((c) => c.action === 'CHKOUT').at(-1) || null`)
check(
  'dirtyWithStashArgs',
  stashCall?.args.branch === 'dev' && stashCall?.args.confirm === true && stashCall?.args.stash === true && stashToast,
  { stashCall, stashToast, stashHintChecked, toast: await toastText() },
)
report.screenshots.push(await shot('chkout-08-stash-success'))

// 10) getRemoteRefs 失败 → 回退当前分支/Tag 建议，不报错误 Toast，仍可手动提交
await evaluate(`window.__CHKOUT_CFG__.failRemoteRefs = true`)
await click('[data-component-id="C1_CLEAN"]')
await waitFor(`document.querySelector('[data-testid="action-chkout"]')?.disabled === false`, 10000)
const toastCountBefore = await evaluate(`document.querySelectorAll('[data-testid="toast"]').length`)
await openCheckout()
await waitRefsLoaded()
const fallbackShape = await evaluate(`(() => ({
  source: document.querySelector('[data-testid="checkout-ref-source-inline"]')?.textContent?.replace(/\\s+/g, ' ').trim() || '',
  names: [...document.querySelectorAll('[data-testid="checkout-ref-item"]')].map((el) => el.dataset.refName),
  value: document.querySelector('[data-testid="action-dialog-ref"]')?.value || '',
  toasts: [...document.querySelectorAll('[data-testid="toast"]')].map((t) => t.textContent.trim()),
}))()`)
check(
  'remoteRefsErrorFallback',
  fallbackShape.source.includes('回退') &&
    fallbackShape.names.includes('refs/heads/main') &&
    !fallbackShape.toasts.some((text) => text.includes('getRemoteRefs') || text.includes('读取分支/Tag 失败：')),
  { fallbackShape, toastCountBefore },
)
report.screenshots.push(await shot('chkout-09-refs-fallback'))

// 回退形态仍可手动输入 tag 并提交
await click('[data-testid="checkout-kind-tag"]')
await setValue('[data-testid="action-dialog-ref"]', 'v9.9.9')
await click('[data-testid="action-dialog-confirm"]')
const fallbackToast = await waitToast('已检出', 8000)
const fallbackCall = await evaluate(`window.__CHKOUT_CFG__.actionCalls.filter((c) => c.action === 'CHKOUT').at(-1) || null`)
check('fallbackManualSubmit', fallbackToast && fallbackCall?.args.tag === 'v9.9.9' && fallbackCall?.args.confirm === true, { fallbackToast, fallbackCall })

// 11) 最多渲染 200 条（259 条匹配 → 200 条 + 截断提示）
await evaluate(`(() => { window.__CHKOUT_CFG__.failRemoteRefs = false; window.__CHKOUT_CFG__.largeRefs = true })()`)
await click('[data-component-id="C1_CLEAN"]')
await waitFor(`document.querySelector('[data-testid="action-chkout"]')?.disabled === false`, 10000)
await openCheckout()
await waitRefsLoaded()
const capShape = await evaluate(`(() => ({
  items: document.querySelectorAll('[data-testid="checkout-ref-item"]').length,
  note: document.querySelector('[data-testid="checkout-ref-truncated"]')?.textContent?.replace(/\\s+/g, ' ').trim() || '',
  source: document.querySelector('[data-testid="checkout-ref-source-inline"]')?.textContent?.replace(/\\s+/g, ' ').trim() || '',
}))()`)
check('renderCap200', capShape.items === 200 && capShape.note.includes('仅渲染前 200'), capShape)
report.screenshots.push(await shot('chkout-10-render-cap'))
await click('[data-testid="action-dialog-cancel"]')

// 12) 搜索无结果空态
await evaluate(`window.__CHKOUT_CFG__.largeRefs = false`)
await click('[data-component-id="C1_CLEAN"]')
await waitFor(`document.querySelector('[data-testid="action-chkout"]')?.disabled === false`, 10000)
await openCheckout()
await waitRefsLoaded()
await setValue('[data-testid="checkout-ref-search"]', 'no-such-ref-xyz')
const emptyShape = await evaluate(`(() => ({
  empty: document.querySelector('[data-testid="checkout-ref-empty"]')?.textContent?.replace(/\\s+/g, ' ').trim() || '',
  items: document.querySelectorAll('[data-testid="checkout-ref-item"]').length,
  confirmDisabled: document.querySelector('[data-testid="action-dialog-confirm"]')?.disabled,
}))()`)
check('searchEmptyState', emptyShape.items === 0 && emptyShape.empty.includes('没有匹配') && emptyShape.confirmDisabled === true, emptyShape)
report.screenshots.push(await shot('chkout-11-search-empty'))
await key('Escape', 'Escape', 27)
const dialogClosed = await evaluate(`!document.querySelector('[data-testid="action-dialog"]')`)
check('escClose', dialogClosed, { dialogClosed })

report.consoleErrors = consoleErrors
report.exceptions = exceptions
const failed = Object.entries(report.checks).filter(([, value]) => !value.ok)
report.summary = {
  total: Object.keys(report.checks).length,
  passed: Object.keys(report.checks).length - failed.length,
  failed: failed.map(([name]) => name),
  consoleErrors,
  exceptions,
}
await writeFile(join(OUT, 'report.json'), JSON.stringify(report, null, 2))
console.log(JSON.stringify(report.summary, null, 2))
console.log(`报告与截图：${OUT}`)
ws.close()
process.exit(failed.length === 0 && exceptions.length === 0 ? 0 : 1)
