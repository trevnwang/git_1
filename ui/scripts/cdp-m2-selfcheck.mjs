#!/usr/bin/env node
/**
 * M2 单仓按钮 / 组件详情抽屉 / 树错误态 CDP 自测（零依赖，Node ≥22 内置 WebSocket）。
 *
 * 做法：连上已用 --remote-debugging-port 打开的 Chromium 内核，向页面注入
 * window.__DSH_TEST_BRIDGE__（按 task M2 Host 契约返回数据），再导航到 Vite dev server。
 * 覆盖：能力门禁（features.repoActions false→true）、状态矩阵、COMMIT/CHKOUT 输入框、
 * PULL/PUSH 确认、FETCH 直执行、E_CONFIRM_REQUIRED 单次重试、DIRTY/TIMEOUT 等错误码、
 * 组件详情抽屉（Host + 快照回退）、四仓树 error/truncated、文件夹视图 error/truncated 与 D/I/M/S chips。
 *
 * 用法：node scripts/cdp-m2-selfcheck.mjs --cdp 9333 --url http://127.0.0.1:5173 --out artifacts/ui-review
 */
import { mkdir, writeFile } from 'node:fs/promises'
import { join } from 'node:path'

const args = process.argv.slice(2)
function arg(name, fallback) {
  const at = args.indexOf(`--${name}`)
  return at >= 0 && args[at + 1] ? args[at + 1] : fallback
}

const CDP = Number(arg('cdp', '9333'))
const URL = arg('url', 'http://127.0.0.1:5173')
const OUT = arg('out', join(process.cwd(), '..', 'artifacts', 'ui-review'))
const WIDTH = Number(arg('width', '1440'))
const HEIGHT = Number(arg('height', '900'))

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms))

/** 注入页面的 Mock Host 桥：严格模拟 M2 契约（runRepoAction 成功/错误、能力开关、详情回退）。 */
function bridgeFactory() {
  const HEAD = 'abcdef1234567890abcdef1234567890abcdef12'
  const cfg = {
    features: { fileTree: true, workspaceTree: true, openPath: true, repoActions: false, ci: false, componentDetail: true },
    failComponentDetail: false,
    workspaceError: false,
    workspaceTruncated: true,
    confirmRequired: {},
    failAction: {},
    actionCalls: [],
    snapshotCalls: 0,
    hostInfoCalls: 0,
  }
  window.__M2_CFG__ = cfg

  function repo(type, state, extra) {
    const base = {
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
    }
    return Object.assign(base, extra || {})
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
      incomplete: state === 'MISSING',
      lastActivityAt: new Date(Date.now() - severityRank * 60000).toISOString(),
      ciAggregate: { componentId: id, byState: { NONE: 4 }, worstState: 'NONE', dataFreshAt: new Date().toISOString() },
      repos: repos.map((item) => repo(item[0], item[1], item[2])),
    }
  }

  const components = [
    component('C1_SYNCED', 'SYNCED', 10, [
      ['DESIGN', 'SYNCED'],
      ['IMPL', 'SYNCED'],
      ['MIL', 'SYNCED'],
      ['SIL', 'SYNCED'],
    ]),
    component('C2_BEHIND', 'BEHIND', 20, [
      ['DESIGN', 'BEHIND', { behind: 2 }],
      ['IMPL', 'BEHIND', { behind: 3 }],
      ['MIL', 'SYNCED'],
      ['SIL', 'SYNCED'],
    ]),
    component('C3_DIRTY', 'DIRTY', 30, [
      ['DESIGN', 'DIRTY', { dirtyCount: 1 }],
      ['IMPL', 'DIRTY', { dirtyCount: 2, untrackedCount: 1 }],
      ['MIL', 'SYNCED'],
      ['SIL', 'AHEAD', { ahead: 2 }],
    ]),
    component('C4_MISSING', 'MISSING', 40, [
      ['DESIGN', 'MISSING', { isLocal: false, localPath: null }],
      ['IMPL', 'MISSING', { isLocal: false, localPath: null }],
      ['MIL', 'MISSING', { isLocal: false, localPath: null }],
      ['SIL', 'MISSING', { isLocal: false, localPath: null }],
    ]),
  ]

  const commits = (componentId, type) =>
    Array.from({ length: 5 }, (_, index) => ({
      sha: `${(index + 1).toString(16).padStart(2, '0')}c0ffee1234567890abcdef1234567890abcdef1`,
      shortSha: `${(index + 1).toString(16).padStart(2, '0')}c0ffee`,
      message: index === 0 ? 'fix: mock commit for M2 UI selfcheck' : `chore: mock commit ${index}`,
      author: 'M2 Tester',
      at: new Date(Date.now() - index * 3600000).toISOString(),
      refs: index === 0 ? ['HEAD -> main'] : [],
    }))

  function snapshot() {
    cfg.snapshotCalls += 1
    return {
      source: 'local',
      workspaceRoot: 'D:\\Mock\\Workspace',
      error: null,
      generatedAt: new Date().toISOString(),
      components,
      counts: { components: components.length, repos: components.length * 4, missingRepos: 4 },
      freshness: {
        localScanAt: new Date().toISOString(),
        remoteFetchAt: new Date().toISOString(),
        localStale: false,
        remoteStale: false,
      },
    }
  }

  function findComponent(componentId) {
    return components.find((item) => item.componentId === componentId) || null
  }

  function findRepo(componentId, repoType) {
    const component = findComponent(componentId)
    return (component && component.repos.find((item) => item.repoType === repoType)) || null
  }

  function treePayload(componentId, repoType) {
    const repoRow = findRepo(componentId, repoType)
    const base = {
      componentId,
      repoType,
      repoName: (repoRow && repoRow.repoName) || `Mock_${repoType}`,
      rootPath: `D:\\Mock\\${repoType}`,
      path: '',
      branch: 'main',
      headSha: HEAD,
      state: (repoRow && repoRow.state) || 'SYNCED',
      dirtyCount: (repoRow && repoRow.dirtyCount) || 0,
      compareRef: 'refs/remotes/origin/main',
      fetchedAt: new Date().toISOString(),
      entries: [],
      truncated: false,
      totalEntries: 0,
      error: null,
      errorCode: null,
    }
    if (componentId === 'C2_BEHIND' && repoType === 'IMPL') {
      base.error = '读取目录失败：拒绝访问（Mock 注入）'
      base.errorCode = 'E_ACCESS_DENIED'
      return base
    }
    if (componentId === 'C2_BEHIND' && repoType === 'MIL') {
      base.entries = [
        { path: 'src', name: 'src', kind: 'DIR', hasChildren: true, size: null, mtime: new Date().toISOString(), gitStatus: 'NONE', submodule: false, ignored: false, children: null, error: null },
      ]
      base.truncated = true
      base.totalEntries = 2000
      return base
    }
    base.entries = [
      { path: 'src', name: 'src', kind: 'DIR', hasChildren: true, size: null, mtime: new Date().toISOString(), gitStatus: 'NONE', submodule: false, ignored: false, children: null, error: null },
      { path: 'README.md', name: 'README.md', kind: 'FILE', hasChildren: false, size: 1024, mtime: new Date().toISOString(), gitStatus: 'MODIFIED', submodule: false, ignored: false, children: null, error: null },
    ]
    if (componentId === 'C1_SYNCED' && repoType === 'DESIGN') {
      // 目录级错误：节点 error 必须在 FileTree 内以错误标记呈现（不是静默空目录）。
      base.entries.push({
        path: 'locked',
        name: 'locked',
        kind: 'DIR',
        hasChildren: true,
        size: null,
        mtime: new Date().toISOString(),
        gitStatus: 'NONE',
        submodule: false,
        ignored: false,
        children: null,
        error: 'E_ACCESS_DENIED 子目录不可读（Mock 注入）',
      })
    }
    base.totalEntries = base.entries.length
    return base
  }

  function workspacePayload() {
    const entries = components.map((item) => ({
      kind: 'COMPONENT',
      name: item.componentId,
      path: item.componentId,
      componentId: item.componentId,
      state: item.aggregateState,
      severityRank: item.severityRank,
      branch: 'main',
      dirtyCount: item.componentId === 'C3_DIRTY' ? 3 : 0,
      mixedBranch: false,
      repos: item.repos.map((row) => ({
        kind: 'REPO',
        name: row.repoName,
        path: `${row.repoName}`,
        componentId: item.componentId,
        repoType: row.repoType,
        state: row.state,
        branch: 'main',
        hasChildren: row.isLocal,
        isLocal: row.isLocal,
      })),
    }))
    if (cfg.workspaceError) {
      return {
        root: 'D:\\Mock\\Workspace',
        path: '',
        generatedAt: new Date().toISOString(),
        readAt: new Date().toISOString(),
        entries: [],
        truncated: false,
        totalEntries: 0,
        error: '读取工作区失败：拒绝访问（Mock 注入）',
        errorCode: 'E_ACCESS_DENIED',
      }
    }
    return {
      root: 'D:\\Mock\\Workspace',
      path: '',
      generatedAt: new Date().toISOString(),
      readAt: new Date().toISOString(),
      entries,
      truncated: cfg.workspaceTruncated,
      totalEntries: 42,
      error: null,
      errorCode: null,
    }
  }

  const bridge = {
    handle(request) {
      const method = request.method
      const params = request.params || {}
      if (method === 'getHostInfo') {
        cfg.hostInfoCalls += 1
        return {
          appVersion: '0.3.0-m2-mock',
          webView2Version: 'mock',
          features: cfg.features,
          limits: { maxTreeEntries: 2000 },
        }
      }
      if (method === 'getSnapshot') return snapshot()
      if (method === 'refresh') {
        cfg.snapshotCalls += 1
        return { refreshed: true }
      }
      if (method === 'getRepoTree') return treePayload(params.componentId, params.repoType)
      if (method === 'getWorkspaceTree') return workspacePayload()
      if (method === 'getRepoDetail') {
        return {
          repo: findRepo(params.componentId, params.repoType),
          component: findComponent(params.componentId),
          commits: { total: 5, items: commits(params.componentId, params.repoType) },
        }
      }
      if (method === 'getCommits') {
        return { total: 5, items: commits(params.componentId, params.repoType) }
      }
      if (method === 'getComponentDetail') {
        if (cfg.failComponentDetail) {
          return { ok: false, code: 'E_GIT_FAILED', error: 'Mock：组件详情接口失败（注入）' }
        }
        const component = findComponent(params.componentId)
        if (!component) return { ok: false, code: 'E_NOT_FOUND', error: 'Mock：组件不存在' }
        return component
      }
      if (method === 'openPath') {
        return { opened: true, mode: params.mode || 'open', path: params.path, errorCode: null, message: 'Mock 已打开' }
      }
      if (method === 'runRepoAction') {
        const action = params.action
        const args = params.args || {}
        cfg.actionCalls.push({ action, componentId: params.componentId, repoType: params.repoType, args })
        if (cfg.confirmRequired[action] && !args.confirm) {
          return { ok: false, code: 'E_CONFIRM_REQUIRED', error: 'Mock：该操作需要二次确认', details: { action } }
        }
        if (cfg.failAction[action]) {
          return { ok: false, code: cfg.failAction[action], error: `Mock：${action} 失败（${cfg.failAction[action]}）` }
        }
        if (action === 'CHKOUT' && params.componentId === 'C3_DIRTY') {
          return { ok: false, code: 'E_DIRTY_WORKTREE', error: 'Mock：工作区有未提交改动，无法检出' }
        }
        if (action === 'COMMIT' && !args.message) {
          return { ok: false, code: 'E_INVALID_ARGUMENT', error: 'Mock：提交信息不能为空' }
        }
        return {
          accepted: true,
          action,
          componentId: params.componentId,
          repoType: params.repoType,
          state: 'SYNCED',
          preHead: HEAD,
          postHead: '1234567' + HEAD.slice(7),
          branch: args.branch || args.tag || 'main',
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
  if (msg.method === 'Runtime.exceptionThrown') {
    exceptions.push(msg.params.exceptionDetails?.text ?? 'exception')
  }
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
  await sleep(140)
}

async function doubleClick(selector) {
  const target = await rect(selector)
  if (!target) throw new Error(`元素不存在：${selector}`)
  const x = target.x + Math.min(target.w / 2, 80)
  const y = target.y + target.h / 2
  await send('Input.dispatchMouseEvent', { type: 'mouseMoved', x, y, button: 'none', clickCount: 0 })
  await send('Input.dispatchMouseEvent', { type: 'mousePressed', x, y, button: 'left', clickCount: 1 })
  await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x, y, button: 'left', clickCount: 1 })
  await sleep(60)
  await send('Input.dispatchMouseEvent', { type: 'mousePressed', x, y, button: 'left', clickCount: 2 })
  await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x, y, button: 'left', clickCount: 2 })
  await sleep(240)
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
  await sleep(120)
}

async function text(selector) {
  return evaluate(`document.querySelector(${JSON.stringify(selector)})?.textContent?.replace(/\\s+/g, ' ').trim() ?? ''`)
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

async function clickable(selector) {
  return evaluate(`(() => {
    const el = document.querySelector(${JSON.stringify(selector)});
    return el ? { disabled: Boolean(el.disabled), title: el.getAttribute('title') || '' } : null;
  })()`)
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

const report = { url: URL, viewport: { width: WIDTH, height: HEIGHT }, checks: {}, screenshots: [] }
const check = (name, ok, detail) => {
  report.checks[name] = { ok: Boolean(ok), detail }
}

// 1) 启动 + Host 能力 false → 五键全部禁用且给 M1 理由
const booted = await waitFor(`document.querySelectorAll('[data-testid="component-card"]').length === 4`, 15000)
check('boot', booted, { cards: await evaluate(`document.querySelectorAll('[data-testid="component-card"]').length`) })
const m1Blocked = await waitFor(
  `(() => {
    const buttons = [...document.querySelectorAll('[data-testid="repo-actions"] .action')];
    return buttons.length === 5 && buttons.every((b) => b.disabled) &&
      [...document.querySelectorAll('[data-testid="repo-actions"] .action')].every((b) => (b.title || '').includes('M1 只读'));
  })()`,
  8000,
)
check('capabilityDisabledM1', m1Blocked, await evaluate(`(() => ({
  disabled: [...document.querySelectorAll('[data-testid="repo-actions"] .action')].map((b) => b.disabled),
  titles: [...document.querySelectorAll('[data-testid="repo-actions"] .action')].map((b) => b.title),
}))()`))
report.screenshots.push(await shot('m2-01-capability-disabled'))

// 2) Host 能力 true（触发 getHostInfo 刷新）→ 按矩阵启用
await evaluate(`window.__M2_CFG__.features.repoActions = true`)
await click('[data-testid="ci-nav"]')
await sleep(350)
await key('Escape', 'Escape', 27)
const enabled = await waitFor(
  `document.querySelector('[data-testid="action-pull"]')?.disabled === false`,
  8000,
)
check('capabilityEnabled', enabled, await evaluate(`(() => ({
  features: window.__M2_CFG__.features.repoActions,
  disabled: [...document.querySelectorAll('[data-testid="repo-actions"] .action')].map((b) => b.disabled),
}))()`))

// 3) 状态矩阵：BEHIND / MISSING
await click('[data-component-id="C2_BEHIND"]')
await sleep(500)
const behindMatrix = await evaluate(`(() => {
  const state = {};
  for (const action of ['pull','fetch','chkout','commit','push']) {
    const el = document.querySelector('[data-testid="action-' + action + '"]');
    state[action] = { disabled: el?.disabled, title: el?.title || '' };
  }
  return state;
})()`)
check(
  'matrixBehind',
  behindMatrix.pull?.disabled === false &&
    behindMatrix.fetch?.disabled === false &&
    behindMatrix.chkout?.disabled === false &&
    behindMatrix.commit?.disabled === true &&
    behindMatrix.push?.disabled === true &&
    behindMatrix.push?.title.includes('BEHIND'),
  behindMatrix,
)
await click('[data-component-id="C4_MISSING"]')
await sleep(400)
const missingMatrix = await evaluate(`[...document.querySelectorAll('[data-testid="repo-actions"] .action')].map((b) => ({ disabled: b.disabled, title: b.title }))`)
check('matrixMissing', missingMatrix.every((item) => item.disabled && item.title.length > 0), missingMatrix)

// 4) COMMIT：输入框必填 → 执行成功 Toast「已提交 <shortSha>」+ 状态栏 success + 刷新快照
await click('[data-component-id="C1_SYNCED"]')
await sleep(400)
const snapshotBefore = await evaluate(`window.__M2_CFG__.snapshotCalls`)
await click('[data-testid="action-commit"]')
await waitFor(`Boolean(document.querySelector('[data-testid="action-dialog"]'))`, 4000)
const commitDialog = await evaluate(`(() => {
  const dialog = document.querySelector('[data-testid="action-dialog"]');
  const confirm = document.querySelector('[data-testid="action-dialog-confirm"]');
  return {
    exists: Boolean(dialog),
    kind: dialog?.dataset.kind,
    hasTextarea: Boolean(document.querySelector('[data-testid="action-dialog-message"]')),
    confirmDisabled: confirm?.disabled,
    title: dialog?.getAttribute('aria-label') || '',
  };
})()`)
report.screenshots.push(await shot('m2-02-commit-dialog'))
await setValue('[data-testid="action-dialog-message"]', 'fix: mock commit from M2 UI selfcheck')
const commitCanSubmit = await evaluate(`document.querySelector('[data-testid="action-dialog-confirm"]')?.disabled === false`)
await click('[data-testid="action-dialog-confirm"]')
const commitToast = await waitToast('已提交', 6000)
const commitCalls = await evaluate(`window.__M2_CFG__.actionCalls.filter((c) => c.action === 'COMMIT')`)
const lastOpAfterCommit = await text('[data-testid="status-last-op"]')
check(
  'commitFlow',
  commitDialog.exists &&
    commitDialog.kind === 'commit' &&
    commitDialog.hasTextarea &&
    commitDialog.confirmDisabled === true &&
    commitCanSubmit &&
    commitToast &&
    commitCalls.length === 1 &&
    commitCalls[0].args.message === 'fix: mock commit from M2 UI selfcheck' &&
    lastOpAfterCommit.includes('✓ COMMIT 成功'),
  { commitDialog, commitCanSubmit, commitToast, commitCalls, lastOpAfterCommit },
)
report.screenshots.push(await shot('m2-03-commit-success'))
const snapshotAfterCommit = await evaluate(`window.__M2_CFG__.snapshotCalls`)
check('refreshAfterAction', snapshotAfterCommit > snapshotBefore, { before: snapshotBefore, after: snapshotAfterCommit })

// 5) PULL：确认对话框显示 behind → 成功 Toast 带 postHead 短 sha
await click('[data-component-id="C2_BEHIND"]')
await sleep(400)
await click('[data-testid="action-pull"]')
await waitFor(`Boolean(document.querySelector('[data-testid="confirm-dialog"]'))`, 4000)
const pullDialog = await evaluate(`(() => {
  const dialog = document.querySelector('[data-testid="confirm-dialog"]');
  return { exists: Boolean(dialog), text: dialog?.textContent?.replace(/\\s+/g, ' ').trim() || '' };
})()`)
report.screenshots.push(await shot('m2-04-pull-confirm'))
await click('[data-testid="confirm-dialog"] .btn.primary')
const pullToast = await waitToast('PULL 完成', 6000)
check('pullConfirmFlow', pullDialog.exists && pullDialog.text.includes('落后') && pullToast, { pullDialog, pullToast })

// 6) FETCH：无确认直接执行
await click('[data-testid="action-fetch"]')
await sleep(120)
const fetchDialogDuring = await evaluate(`Boolean(document.querySelector('[data-testid="action-dialog"]') || document.querySelector('[data-testid="confirm-dialog"]'))`)
const fetchToast = await waitToast('FETCH 完成', 6000)
check('fetchDirect', fetchDialogDuring === false && fetchToast, { fetchDialogDuring, fetchToast })

// 7) PUSH：确认对话框含分支/ahead → 成功
await click('[data-component-id="C1_SYNCED"]')
await sleep(400)
await click('[data-testid="action-push"]')
await waitFor(`Boolean(document.querySelector('[data-testid="confirm-dialog"]'))`, 4000)
const pushDialog = await evaluate(`(() => {
  const dialog = document.querySelector('[data-testid="confirm-dialog"]');
  return { exists: Boolean(dialog), text: dialog?.textContent?.replace(/\\s+/g, ' ').trim() || '' };
})()`)
report.screenshots.push(await shot('m2-05-push-confirm'))
await click('[data-testid="confirm-dialog"] .btn.primary')
const pushToast = await waitToast('PUSH 完成', 6000)
check('pushConfirmFlow', pushDialog.exists && pushDialog.text.includes('分支') && pushToast, { pushDialog, pushToast })

// 8) CHKOUT：Radio Tag + 输入 → 成功；DIRTY 错误提示先提交/stash
await click('[data-testid="action-chkout"]')
await waitFor(`Boolean(document.querySelector('[data-testid="action-dialog"]'))`, 4000)
await click('[data-testid="checkout-kind-tag"]')
await setValue('[data-testid="action-dialog-ref"]', 'v1.2.0')
report.screenshots.push(await shot('m2-06-checkout-dialog'))
await click('[data-testid="action-dialog-confirm"]')
const checkoutToast = await waitToast('已检出', 6000)
const checkoutCall = await evaluate(`window.__M2_CFG__.actionCalls.filter((c) => c.action === 'CHKOUT').at(-1) ?? null`)
check('checkoutSuccess', checkoutToast && checkoutCall?.args.tag === 'v1.2.0', { checkoutToast, checkoutCall })

await click('[data-component-id="C3_DIRTY"]')
await sleep(400)
await click('[data-testid="action-chkout"]')
await waitFor(`Boolean(document.querySelector('[data-testid="action-dialog"]'))`, 4000)
await setValue('[data-testid="action-dialog-ref"]', 'dev')
await click('[data-testid="action-dialog-confirm"]')
const dirtyToast = await waitToast('E_DIRTY_WORKTREE', 7000)
const dirtyLastOp = await text('[data-testid="status-last-op"]')
check(
  'checkoutDirtyError',
  dirtyToast && (await toastText()).includes('提交') && dirtyLastOp.includes('E_DIRTY_WORKTREE'),
  { dirtyToast, toast: await toastText(), dirtyLastOp },
)
report.screenshots.push(await shot('m2-07-dirty-error'))

// 9) E_CONFIRM_REQUIRED：二次确认后带 confirm=true 重试（且只重试一次）
await evaluate(`window.__M2_CFG__.confirmRequired.PUSH = true`)
await click('[data-component-id="C1_SYNCED"]')
await sleep(400)
await click('[data-testid="action-push"]')
await waitFor(`Boolean(document.querySelector('[data-testid="confirm-dialog"]'))`, 4000)
await click('[data-testid="confirm-dialog"] .btn.primary')
const confirmAgain = await waitFor(`(() => {
  const dialog = document.querySelector('[data-testid="confirm-dialog"]');
  return Boolean(dialog && dialog.textContent.includes('二次确认'));
})()`, 6000)
report.screenshots.push(await shot('m2-08-confirm-required'))
await click('[data-testid="confirm-dialog"] .btn.danger')
const confirmRetryToast = await waitToast('PUSH 完成', 6000)
const pushCalls = await evaluate(`window.__M2_CFG__.actionCalls.filter((c) => c.action === 'PUSH')`)
check(
  'confirmRequiredRetry',
  confirmAgain && confirmRetryToast && pushCalls.length >= 2 && pushCalls.at(-1).args.confirm === true,
  { confirmAgain, confirmRetryToast, pushCalls: pushCalls.slice(-2) },
)
await evaluate(`window.__M2_CFG__.confirmRequired.PUSH = false`)

// 10) 组件详情抽屉：Host 详情 → 关闭（ESC）；Host 失败 → 快照回退
await doubleClick('[data-component-id="C1_SYNCED"]')
const drawerOpen = await waitFor(`Boolean(document.querySelector('[data-testid="component-drawer"]'))`, 5000)
await sleep(300)
const drawerHost = await evaluate(`(() => {
  const drawer = document.querySelector('[data-testid="component-drawer"]');
  return {
    source: drawer?.dataset.source || '',
    hasId: drawer?.textContent.includes('C1_SYNCED') || false,
    repoRows: drawer?.querySelectorAll('[data-testid="component-drawer-repo"]').length || 0,
    hasCi: drawer?.textContent.includes('CI 未接入') || false,
    text: drawer?.textContent?.replace(/\\s+/g, ' ').trim().slice(0, 180) || '',
  };
})()`)
report.screenshots.push(await shot('m2-09-component-drawer-host'))
await key('Escape', 'Escape', 27)
await sleep(250)
const drawerClosed = await evaluate(`!document.querySelector('[data-testid="component-drawer"]')`)
check(
  'componentDrawerHost',
  drawerOpen && drawerHost.source === 'host' && drawerHost.hasId && drawerHost.repoRows === 4 && drawerHost.hasCi && drawerClosed,
  { drawerHost, drawerClosed },
)

await evaluate(`window.__M2_CFG__.failComponentDetail = true`)
await doubleClick('[data-component-id="C1_SYNCED"]')
await waitFor(`Boolean(document.querySelector('[data-testid="component-drawer"]'))`, 5000)
await sleep(300)
const drawerFallback = await evaluate(`(() => {
  const drawer = document.querySelector('[data-testid="component-drawer"]');
  return { source: drawer?.dataset.source || '', note: drawer?.querySelector('[data-testid="component-drawer-note"]')?.textContent.trim() || '' };
})()`)
report.screenshots.push(await shot('m2-10-component-drawer-fallback'))
await click('[data-testid="component-drawer-close"]')
await sleep(200)
const drawerClosedByButton = await evaluate(`!document.querySelector('[data-testid="component-drawer"]')`)
await evaluate(`window.__M2_CFG__.failComponentDetail = false`)
check(
  'componentDrawerFallback',
  drawerFallback.source === 'snapshot' && drawerFallback.note.includes('回退快照') && drawerClosedByButton,
  { drawerFallback, drawerClosedByButton },
)

// 10b) 右键菜单「查看组件详情」同样打开抽屉
const cardRect = await rect('[data-component-id="C1_SYNCED"]')
const cx = cardRect.x + 60
const cy = cardRect.y + 20
await send('Input.dispatchMouseEvent', { type: 'mousePressed', x: cx, y: cy, button: 'right', clickCount: 1 })
await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x: cx, y: cy, button: 'right', clickCount: 1 })
const contextShown = await waitFor(`Boolean(document.querySelector('[data-testid="context-menu"]'))`, 4000)
const detailItem = await evaluate(`(() => {
  const menu = document.querySelector('[data-testid="context-menu"]');
  const button = [...(menu?.querySelectorAll('button') ?? [])].find((item) => item.textContent.includes('查看组件详情'));
  if (!button) return null;
  const r = button.getBoundingClientRect();
  return { x: r.x + r.width / 2, y: r.y + r.height / 2, label: button.textContent.trim() };
})()`)
if (detailItem) {
  await send('Input.dispatchMouseEvent', { type: 'mouseMoved', x: detailItem.x, y: detailItem.y, button: 'none', clickCount: 0 })
  await send('Input.dispatchMouseEvent', { type: 'mousePressed', x: detailItem.x, y: detailItem.y, button: 'left', clickCount: 1 })
  await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x: detailItem.x, y: detailItem.y, button: 'left', clickCount: 1 })
}
const contextDrawer = await waitFor(`Boolean(document.querySelector('[data-testid="component-drawer"]'))`, 4000)
await key('Escape', 'Escape', 27)
await sleep(200)
const contextDrawerClosed = await evaluate(`!document.querySelector('[data-testid="component-drawer"]')`)
check('componentDrawerContextMenu', contextShown && Boolean(detailItem) && contextDrawer && contextDrawerClosed, {
  contextShown,
  detailItem,
  contextDrawer,
  contextDrawerClosed,
})

// 10c) FileTree 节点级错误标记（目录 error 不静默）
await click('[data-component-id="C1_SYNCED"]')
const fileTreeErrorShown = await waitFor(
  `Boolean(document.querySelector('[data-testid="repo-tree-panel"][data-repo-type="DESIGN"] [data-testid="tree-node-error"]'))`,
  6000,
)
const fileTreeError = await evaluate(`(() => {
  const marker = document.querySelector('[data-testid="repo-tree-panel"][data-repo-type="DESIGN"] [data-testid="tree-node-error"]');
  return { exists: Boolean(marker), text: marker?.textContent?.trim() || '', title: marker?.getAttribute('title') || '' };
})()`)
check(
  'fileTreeNodeError',
  fileTreeErrorShown && fileTreeError.text === '!' && fileTreeError.title.includes('E_ACCESS_DENIED'),
  fileTreeError,
)

// 11) 四仓树 error/errorCode 空态（不是“空目录”）+ truncated 横幅
await click('[data-component-id="C2_BEHIND"]')
await sleep(700)
const treeError = await evaluate(`(() => {
  const panel = document.querySelector('[data-testid="repo-tree-panel"][data-repo-type="IMPL"]');
  const empty = panel?.querySelector('[data-testid="repo-tree-empty"]');
  return {
    exists: Boolean(empty),
    text: empty?.textContent?.replace(/\\s+/g, ' ').trim() || '',
    isBlankDir: Boolean(empty && empty.textContent.includes('空目录')),
  };
})()`)
const truncated = await evaluate(`(() => {
  const panel = document.querySelector('[data-testid="repo-tree-panel"][data-repo-type="MIL"]');
  const banner = panel?.querySelector('[data-testid="tree-truncated"]');
  return { exists: Boolean(banner), text: banner?.textContent?.replace(/\\s+/g, ' ').trim() || '' };
})()`)
report.screenshots.push(await shot('m2-11-tree-error-truncated'))
check(
  'treeErrorEmptyState',
  treeError.exists && treeError.text.includes('E_ACCESS_DENIED') && treeError.text.includes('无法读取目录') && treeError.isBlankDir === false,
  treeError,
)
check('treeTruncatedBanner', truncated.exists && truncated.text.includes('已截断'), truncated)

// 12) 文件夹视图：组件行四仓 chips（D/I/M/S + 色）+ truncated + error
await click('[data-testid="view-folder"]')
const folderReady = await waitFor(`document.querySelectorAll('[data-testid="workspace-node"][data-kind="COMPONENT"]').length >= 4`, 8000)
await sleep(400)
const folderChips = await evaluate(`(() => {
  const row = document.querySelector('[data-testid="workspace-node"][data-kind="COMPONENT"]');
  const chips = row ? [...row.querySelectorAll('[data-testid="folder-repo-chip"]')] : [];
  return {
    rows: document.querySelectorAll('[data-testid="workspace-node"][data-kind="COMPONENT"]').length,
    chips: chips.length,
    letters: chips.map((c) => c.textContent.trim()),
    colors: chips.map((c) => getComputedStyle(c).color),
    truncated: document.querySelector('[data-testid="workspace-truncated"]')?.textContent?.replace(/\\s+/g, ' ').trim() || '',
  };
})()`)
report.screenshots.push(await shot('m2-12-folder-chips'))
const chipLettersOk = ['D', 'I', 'M', 'S'].every((letter) => folderChips.letters.includes(letter))
const chipColorsOk = folderChips.colors.length === 4 && folderChips.colors.every((color) => color && color !== 'rgb(0, 0, 0)')
check(
  'folderComponentChips',
  folderReady && folderChips.rows >= 4 && folderChips.chips === 4 && chipLettersOk && chipColorsOk,
  folderChips,
)
check('folderTruncated', folderChips.truncated.includes('已截断'), folderChips.truncated)

await evaluate(`window.__M2_CFG__.workspaceError = true`)
await click('[aria-label="刷新工作区树"]')
const folderError = await waitFor(`(() => {
  const tree = document.querySelector('[data-testid="workspace-tree"]');
  return Boolean(tree && tree.textContent.includes('E_ACCESS_DENIED') && tree.textContent.includes('无法读取工作区'));
})()`, 6000)
report.screenshots.push(await shot('m2-13-folder-error'))
check('folderErrorState', folderError, await evaluate(`document.querySelector('[data-testid="workspace-tree"]')?.textContent?.replace(/\\s+/g, ' ').trim().slice(0, 160) ?? ''`))
await evaluate(`window.__M2_CFG__.workspaceError = false`)

// 13) 控制台无 error / 无未捕获异常
check('consoleClean', exceptions.length === 0 && consoleErrors.length === 0, { exceptions, consoleErrors })

// 回到工作台留最终截图
await click('[data-testid="view-workbench"]')
await sleep(500)
report.screenshots.push(await shot('m2-14-workbench-final'))

const failed = Object.entries(report.checks).filter(([, value]) => !value.ok).map(([key]) => key)
report.summary = { total: Object.keys(report.checks).length, passed: Object.keys(report.checks).length - failed.length, failed }
console.log(JSON.stringify(report, null, 2))
ws.close()
if (failed.length > 0) process.exitCode = 1
