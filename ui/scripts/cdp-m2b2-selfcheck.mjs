/**
 * M2b-2 前端 CDP 自检（隔离 Mock，不触达真实 Host / 真实仓库）：
 *  - 操作审计：列表字段、action/result 过滤、分页、CSV/JSON 导出、toast 路径 + 「打开所在目录」（openPath reveal）；
 *  - 备份 ref：按组件/仓加载（sha/createdAt）、选择后确认（覆盖提示）、确认后 restoreBackupRef、成功后刷新快照/详情、Host 拒绝不显示成功、无备份空态；
 *  - EMPTY 首推：PUSH 确认条「发布分支并建立 upstream」+ args.setUpstream=true；NO_UPSTREAM 文案不回退；Host 矩阵恢复后按钮启用；
 *  - 离线提示：source=local + 远程错误标记 → 顶部「离线/远程不可达」、FETCH 禁用且 tooltip 说明；
 *  - M2a/M2b-1 回归抽样：5 键 tooltip 冻结格式、确认条高度/bbox 不变/未确认 0 写、状态栏模板、任务中心空态。
 *
 * 前置：`npm --prefix ui run build`（本脚本自行启动静态服务 + Headless Edge + 注入测试桥）。
 * 用法：node ui/scripts/cdp-m2b2-selfcheck.mjs
 * 结果：artifacts/m2b2-selfcheck/（result.json + 截图）
 */
import { createServer } from 'node:http'
import { spawn, spawnSync } from 'node:child_process'
import { existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { dirname, extname, join, normalize, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { tmpdir } from 'node:os'

const here = dirname(fileURLToPath(import.meta.url))
const uiRoot = resolve(here, '..')
const distDir = join(uiRoot, 'dist')
const outDir = join(uiRoot, '..', 'artifacts', 'm2b2-selfcheck')
mkdirSync(outDir, { recursive: true })

if (!existsSync(join(distDir, 'index.html'))) {
  console.error('缺少 ui/dist：请先执行 npm --prefix ui run build')
  process.exit(2)
}

/** 结束本脚本启动的 Edge 进程树（Windows：taskkill /T /F；只杀父 PID 会留下子进程占用 CDP 端口）。 */
function killEdgeTree(pid) {
  if (!pid) return
  try {
    spawnSync('taskkill', ['/PID', String(pid), '/T', '/F'], { stdio: 'ignore', windowsHide: true })
  } catch {
    /* ignore */
  }
}

/** 清理上次异常退出的自检 Edge（commandline 含专用 profile 前缀，不影响其他浏览器/自动化实例）。 */
function cleanupStraySelfcheckEdges() {
  try {
    spawnSync(
      'powershell',
      [
        '-NoProfile',
        '-NonInteractive',
        '-Command',
        "Get-CimInstance Win32_Process -Filter \"Name='msedge.exe'\" | Where-Object { $_.CommandLine -match 'dsh-m2b2-edge' } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }",
      ],
      { stdio: 'ignore', windowsHide: true },
    )
  } catch {
    /* ignore */
  }
  spawnSync('cmd', ['/c', 'timeout', '/t', '1', '/nobreak'], { stdio: 'ignore', windowsHide: true })
}
cleanupStraySelfcheckEdges()

const results = []
function check(id, ok, detail = '') {
  const entry = { id, ok: Boolean(ok), detail: String(detail).slice(0, 500) }
  results.push(entry)
  console.log(`${entry.ok ? 'PASS' : 'FAIL'} ${id}${entry.detail ? ' :: ' + entry.detail : ''}`)
}
const sleep = (ms) => new Promise((resolvePromise) => setTimeout(resolvePromise, ms))

// ───────────────────────────── 静态服务（ui/dist） ─────────────────────────────
const MIME = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.svg': 'image/svg+xml',
  '.png': 'image/png',
  '.json': 'application/json; charset=utf-8',
}
let httpPort = 5177 + Math.floor(Math.random() * 200)
const httpBase = httpPort
for (let attempt = 0; attempt < 20; attempt += 1) {
  const port = httpBase + attempt
  const server = createServer((request, response) => {
    try {
      const rawPath = decodeURIComponent((request.url ?? '/').split('?')[0])
      const relative = rawPath === '/' ? 'index.html' : rawPath.replace(/^\/+/, '')
      const filePath = normalize(join(distDir, relative))
      if (!filePath.startsWith(distDir) || !existsSync(filePath)) {
        response.statusCode = 404
        response.end('not found')
        return
      }
      response.setHeader('Content-Type', MIME[extname(filePath)] ?? 'application/octet-stream')
      response.end(readFileSync(filePath))
    } catch {
      response.statusCode = 500
      response.end('error')
    }
  })
  try {
    await new Promise((resolvePromise, rejectPromise) => {
      server.once('error', rejectPromise)
      server.listen(port, '127.0.0.1', resolvePromise)
    })
    httpPort = port
    globalThis.__staticServer = server
    break
  } catch {
    /* 端口占用：换下一个 */
  }
}
if (!globalThis.__staticServer) throw new Error('无法启动静态服务')

// ───────────────────────────── Headless Edge + CDP ─────────────────────────────
const edgeCandidates = [
  'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe',
  'C:\\Program Files\\Microsoft\\Edge\\Application\\msedge.exe',
]
const edgePath = edgeCandidates.find((candidate) => existsSync(candidate))
if (!edgePath) throw new Error('未找到 msedge.exe')
const profileDir = join(tmpdir(), `dsh-m2b2-edge-${Date.now()}-${Math.random().toString(16).slice(2, 8)}`)
let cdpPort = 9400 + Math.floor(Math.random() * 400)
const cdpBase = cdpPort
let edgeProcess = null
for (let attempt = 0; attempt < 20; attempt += 1) {
  const port = cdpBase + attempt
  const child = spawn(
    edgePath,
    [
      '--headless=new',
      '--disable-gpu',
      '--no-first-run',
      '--no-default-browser-check',
      `--remote-debugging-port=${port}`,
      `--user-data-dir=${profileDir}`,
      '--window-size=1440,900',
      'about:blank',
    ],
    { stdio: 'ignore', windowsHide: true },
  )
  let ready = false
  for (let wait = 0; wait < 80; wait += 1) {
    try {
      const response = await fetch(`http://127.0.0.1:${port}/json/version`)
      if (response.ok) {
        ready = true
        break
      }
    } catch {
      /* 尚未就绪 */
    }
    await sleep(150)
  }
  if (ready) {
    cdpPort = port
    edgeProcess = child
    break
  }
  child.kill()
}
if (!edgeProcess) throw new Error('Edge CDP 未就绪')

// 异常退出时也回收 Edge / 静态服务 / 临时 profile，避免占用端口。
process.on('exit', () => {
  killEdgeTree(edgeProcess?.pid)
  cleanupStraySelfcheckEdges()
  try {
    globalThis.__staticServer?.close()
  } catch {
    /* ignore */
  }
})

const targets = await fetch(`http://127.0.0.1:${cdpPort}/json/list`).then((r) => r.json())
const target = targets.find((item) => item.type === 'page') ?? targets[0]
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
      /* 导航/重载期间上下文销毁 */
    }
    await sleep(120)
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

// ───────────────────────────── 测试数据 / 注入桥 ─────────────────────────────
const TEST = {
  snapshot: {
    source: 'local',
    workspaceRoot: 'D:\\Work\\MBD',
    error: 'Gitea 同步失败（模拟）：远程不可达 ECONNREFUSED',
    generatedAt: new Date().toISOString(),
    owner: 'MBD_Org',
    giteaBaseUrl: 'http://gitea.local',
    counts: { components: 3, repos: 12, missingRepos: 0 },
    freshness: {
      localScanAt: new Date(Date.now() - 8_000).toISOString(),
      remoteFetchAt: null,
      localStale: false,
      remoteStale: true,
    },
    capabilities: { fileTree: true, workspaceTree: true, openPath: true, repoActions: true, ci: false },
    components: [
      component('TEST_A_EMPTY', 'EMPTY', ['EMPTY', 'EMPTY', 'EMPTY', 'EMPTY']),
      component('TEST_B_NOUP', 'NO_UPSTREAM', ['SYNCED', 'NO_UPSTREAM', 'SYNCED', 'SYNCED']),
      component('TEST_C_BACKUP', 'SYNCED', ['SYNCED', 'SYNCED', 'SYNCED', 'SYNCED']),
    ],
  },
  audit: buildAuditRows(47),
  exportBase: 'C:\\Users\\Trevn\\AppData\\Local\\MbdGitTool\\exports\\operations-20261114-174500',
  backupRefs: {
    'TEST_C_BACKUP:IMPL': [
      {
        ref: 'refs/backup/sync/20261114173000/IMPL/main',
        sha: '7c2e9c1a5d8f3b0e6a4c2d9f1b7e3a5c8d0f2b46',
        createdAt: new Date(Date.now() - 3_600_000).toISOString(),
        repoType: 'IMPL',
      },
      {
        ref: 'refs/backup/sync/20261114162000/IMPL/main',
        sha: '3f2a9c1d4b5e6f7a8b9c0d1e2f3a4b5c6d7e8f90',
        createdAt: new Date(Date.now() - 7_200_000).toISOString(),
        repoType: 'IMPL',
      },
    ],
  },
  restoreModes: {
    'refs/backup/sync/20261114162000/IMPL/main': 'reject',
  },
  bridgeCalls: [],
  openCalls: [],
  exportMode: 'ok',
  restoreThrowMode: false,
}

function component(id, aggregateState, repoStates) {
  const repoTypes = ['DESIGN', 'IMPL', 'MIL', 'SIL']
  return {
    componentId: id,
    aggregateState,
    severityRank: 6,
    branch: 'main',
    tag: null,
    mixedBranch: false,
    flags: [],
    incomplete: false,
    lastActivityAt: new Date(Date.now() - 600_000).toISOString(),
    repos: repoTypes.map((repoType, index) => ({
      repoType,
      repoName: `${id}_${repoType}`,
      state: repoStates[index],
      subType: null,
      branch: 'main',
      tag: null,
      headSha: repoStates[index] === 'EMPTY' ? null : '3f2a9c1d4b5e6f7a8b9c0d1e2f3a4b5c6d7e8f90',
      upstreamRef: repoStates[index] === 'NO_UPSTREAM' ? null : 'refs/remotes/origin/main',
      compareRef: 'refs/remotes/origin/main',
      ahead: 0,
      behind: 0,
      dirtyCount: 0,
      untrackedCount: 0,
      stagedCount: 0,
      stashCount: 0,
      isLocal: true,
      localPath: `D:\\Work\\MBD\\${id}_${repoType}`,
      isStale: false,
      calcAt: new Date().toISOString(),
      dataSource: 'local',
      errorCode: null,
      flags: [],
      ci: null,
    })),
  }
}

function buildAuditRows(total) {
  const actions = ['PUSH', 'COMMIT', 'FETCH', 'SYNC', 'CLONE', 'RESTORE', 'DELETE', 'PULL', 'CHKOUT', 'TAG', 'CREATE']
  const results = ['OK', 'FAILED', 'SKIPPED', 'CANCELED']
  const components = ['TEST_A_EMPTY', 'TEST_B_NOUP', 'TEST_C_BACKUP']
  const repoTypes = ['DESIGN', 'IMPL', 'MIL', 'SIL']
  const heads = [
    '3f2a9c1d4b5e6f7a8b9c0d1e2f3a4b5c6d7e8f90',
    '7c2e9c1a5d8f3b0e6a4c2d9f1b7e3a5c8d0f2b46',
  ]
  const rows = []
  for (let index = 0; index < total; index += 1) {
    const result = index % 6 === 0 ? 'FAILED' : index % 13 === 0 ? 'SKIPPED' : index % 17 === 0 ? 'CANCELED' : 'OK'
    rows.push({
      id: `op-${String(total - index).padStart(4, '0')}`,
      ts: new Date(Date.now() - index * 61_000).toISOString(),
      actor: 'MBD\\trevn',
      action: actions[index % actions.length],
      componentId: components[index % components.length],
      repoType: repoTypes[index % repoTypes.length],
      // Host M2b-2 实际契约：target/before/after 为 JSON 投影（无扁平 beforeHead/afterHead）。
      target: { componentId: components[index % components.length], repoType: repoTypes[index % repoTypes.length] },
      before: { head: heads[index % 2], state: 'SYNCED' },
      after: result === 'FAILED' ? {} : { head: heads[(index + 1) % 2] },
      result,
      errorCode: result === 'FAILED' ? (index % 2 === 0 ? 'E_OFFLINE' : 'E_DIRTY_WORKTREE') : null,
      requestId: `req-${9000 + index}`,
      jobId: index % 4 === 0 ? `job-test-${8000 + (index % 3)}` : null,
      jobItemId: index % 4 === 0 ? `item-${index}` : null,
      backupRef: actions[index % actions.length] === 'SYNC' && result === 'OK' ? `refs/backup/sync/x/IMPL/main` : null,
    })
  }
  return rows
}

const bridgeSource = `
window.__M2B2_TEST__ = ${JSON.stringify(TEST)};
(() => {
  const test = window.__M2B2_TEST__;
  const MATRIX = {
    SYNCED: 'YYYYY', DIRTY: 'NYCYY', AHEAD: 'NYCNY', BEHIND: 'YYCNN', DIVERGED: 'NYCNN',
    DETACHED_TAG: 'NYCNN', DETACHED_COMMIT: 'NYCNN', NO_UPSTREAM: 'NYNNY', EMPTY: 'NYCYY',
    UNMERGED: 'NYNNN', IN_PROGRESS: 'NNNNN', INVALID: 'NNNNN', MISSING: 'NNNNN', UNKNOWN: 'NYNNN'
  };
  const ACTIONS = ['PULL', 'FETCH', 'CHKOUT', 'COMMIT', 'PUSH'];
  function actionsFor(state) {
    const row = MATRIX[String(state || 'UNKNOWN').toUpperCase()] || 'NNNNN';
    return ACTIONS.map((action, index) => {
      const cell = row[index] || 'N';
      const enabled = cell === 'Y' || cell === 'C';
      const requiresConfirm = cell === 'C' || action === 'COMMIT' || action === 'PUSH';
      return {
        action,
        enabled,
        requiresConfirm,
        reasonCode: enabled ? (requiresConfirm ? 'CONFIRM_REQUIRED' : 'AVAILABLE') : 'STATE_BLOCKED',
        reason: enabled ? (requiresConfirm ? '该操作会改变工作区/HEAD，需二次确认' : '可直接执行') : '当前状态 ' + state + ' 不允许 ' + action
      };
    });
  }
  function ok(data) { return { ok: true, data }; }
  function findRepo(componentId, repoType) {
    const component = test.snapshot.components.find((item) => item.componentId === componentId);
    if (!component) return null;
    const repo = component.repos.find((item) => item.repoType === repoType);
    return repo ? { component, repo } : null;
  }
  function handle(request) {
    const method = request.method;
    const params = request.params || {};
    test.bridgeCalls.push({ method, params });
    switch (method) {
      case 'getSnapshot':
        return ok(test.snapshot);
      case 'refresh':
        return ok(test.snapshot);
      case 'getHostInfo':
        return ok({ appVersion: '0.3.0', webView2Version: 'Mock', features: { fileTree: true, workspaceTree: true, openPath: true, repoActions: true, remoteRefs: true, ci: false }, limits: {} });
      case 'getComponentDetail': {
        const component = test.snapshot.components.find((item) => item.componentId === params.componentId);
        if (!component) return { ok: false, code: 'E_NOT_FOUND', error: 'component not found' };
        return ok({
          ...component,
          repos: component.repos.map((repo) => ({ ...repo, actions: actionsFor(repo.state) }))
        });
      }
      case 'getRepoDetail': {
        const found = findRepo(params.componentId, params.repoType);
        if (!found) return { ok: false, code: 'E_NOT_FOUND', error: 'repo not found' };
        return ok({ repo: found.repo, component: found.component, commits: { total: 0, items: [] } });
      }
      case 'getRepoTree':
        return ok({ componentId: params.componentId, repoType: params.repoType, path: '', entries: [], truncated: false, totalEntries: 0 });
      case 'getWorkspaceTree':
        return ok({ root: test.snapshot.workspaceRoot, path: '', entries: [], truncated: false, totalEntries: 0 });
      case 'getCommits':
        return ok({ total: 0, items: [] });
      case 'getRemoteRefs':
        return ok({ refs: [], truncated: false, total: 0 });
      case 'getJobs':
        return ok({ jobs: [] });
      case 'getOperationLog': {
        let rows = test.audit.slice();
        const action = String(params.action || '').toUpperCase();
        const result = String(params.result || '').toUpperCase();
        if (action) rows = rows.filter((row) => row.action.includes(action));
        if (result) rows = rows.filter((row) => row.result === result);
        const offset = Math.max(0, params.offset || 0);
        const limit = Math.max(1, params.limit || 20);
        return ok({ items: rows.slice(offset, offset + limit), total: rows.length });
      }
      case 'exportOperationLog': {
        if (test.exportMode === 'unavailable') {
          return { ok: false, code: 'E_NOT_IMPLEMENTED', error: 'exportOperationLog 未实现（模拟）' };
        }
        const path = test.exportBase + (params.format === 'json' ? '.json' : '.csv');
        return ok({ path, count: test.audit.length });
      }
      case 'getBackupRefs': {
        const key = params.componentId + ':' + String(params.repoType || '').toUpperCase();
        return ok({ refs: test.backupRefs[key] || [] });
      }
      case 'restoreBackupRef': {
        if (test.restoreThrowMode) {
          return { ok: false, code: 'E_ACTION_NOT_ALLOWED', error: 'Host 拒绝恢复（模拟异常）' };
        }
        const mode = test.restoreModes[params.ref] || 'ok';
        if (mode === 'reject') {
          return ok({ restored: false, ref: params.ref, safetyRef: null, preHead: null, postHead: null, message: 'Host 拒绝恢复（模拟）' });
        }
        return ok({
          restored: true,
          ref: params.ref,
          safetyRef: 'refs/backup/restore/20261114174500/IMPL',
          preHead: '3f2a9c1d4b5e6f7a8b9c0d1e2f3a4b5c6d7e8f90',
          postHead: '7c2e9c1a5d8f3b0e6a4c2d9f1b7e3a5c8d0f2b46',
          message: '已从备份 ref 恢复（reset --hard）'
        });
      }
      case 'openPath':
        test.openCalls.push(params);
        return ok({ opened: true, mode: params.mode || 'open', path: params.path });
      case 'runRepoAction':
        return ok({
          jobId: 'job-test-' + Date.now(),
          accepted: true,
          action: params.action,
          componentId: params.componentId,
          repoType: params.repoType,
          state: 'OK',
          preHead: '3f2a9c1d4b5e6f7a8b9c0d1e2f3a4b5c6d7e8f90',
          postHead: '7c2e9c1a5d8f3b0e6a4c2d9f1b7e3a5c8d0f2b46'
        });
      default:
        return { ok: false, code: 'E_UNKNOWN_METHOD', error: 'unknown method ' + method };
    }
  }
  window.__DSH_TEST_BRIDGE__ = handle;
})();
`

await send('Page.enable')
await send('Runtime.enable')
const bridgeScript = await send('Page.addScriptToEvaluateOnNewDocument', { source: bridgeSource })
await send('Page.navigate', { url: `http://127.0.0.1:${httpPort}/index.html` })
const booted = await waitFor(`!!document.querySelector('[data-testid=component-list-count]')`, 25_000)
check('boot.ready', booted)
check('boot.host-mode', await evaluate(`Boolean(window.__DSH_TEST_BRIDGE__) && !window.__DSH_FORCE_MOCK__`))
await sleep(800)

const bridgeCalls = () => evaluate(`window.__M2B2_TEST__.bridgeCalls.slice()`)
async function selectCard(componentId) {
  await evaluate(`document.querySelector('[data-testid=component-card][data-component-id="${componentId}"]')?.scrollIntoView({block:'center'})`)
  await sleep(150)
  await click(`[data-testid=component-card][data-component-id="${componentId}"]`)
  await sleep(700)
}

// ── 1) 离线提示 + FETCH 禁用（M2b-2） ──
const offlineText = await text('[data-testid=offline-banner]')
check('offline.banner', /离线/.test(offlineText) && /远程不可达/.test(offlineText), offlineText)
check('offline.statusbar', /离线/.test(await text('[data-testid=status-network]')))
await selectCard('TEST_A_EMPTY')
const fetchState = await evaluate(`(() => {
  const n = document.querySelector('[data-testid=action-fetch]');
  return n ? { disabled: n.disabled, title: n.title, enabled: n.getAttribute('data-enabled'), code: n.getAttribute('data-reason-code') } : null;
})()`)
check('offline.fetch-disabled', fetchState?.disabled === true, JSON.stringify(fetchState))
check(
  'offline.fetch-tooltip',
  /离线|远程不可达/.test(fetchState?.title ?? '') && /E_OFFLINE/.test(fetchState?.title ?? ''),
  fetchState?.title ?? '',
)
await screenshot('01-offline-banner-fetch-disabled.png')

// ── 2) EMPTY 首推确认条（M2a/M2b 回归） ──
const actionBoxesBefore = await evaluate(
  `[...document.querySelectorAll('[data-testid^=action-]')].map((b) => { const r = b.getBoundingClientRect(); return [r.x, r.y, r.width, r.height]; })`,
)
const lastOpBefore = await text('[data-testid=status-last-op]')
const pushReady = await waitFor(
  `document.querySelector('[data-testid=action-push]')?.getAttribute('data-enabled') === 'true'`,
  8_000,
)
check('empty.push-enabled-by-host-matrix', pushReady)
await click('[data-testid=action-push]')
await sleep(450)
let emptyBar = await evaluate(
  `(() => { const b = document.querySelector('[data-testid=confirm-bar]'); if (!b) return null; const r = b.getBoundingClientRect(); return { h: r.height, text: b.innerText.replace(/\\s+/g, ' ').trim(), action: b.getAttribute('data-confirm-action') }; })()`,
)
if (!emptyBar) {
  // 一次重试：避免 Host actions 重载窗口内点击落空（不改变断言目标）。
  await click('[data-testid=action-push]')
  await sleep(500)
  emptyBar = await evaluate(
    `(() => { const b = document.querySelector('[data-testid=confirm-bar]'); if (!b) return null; const r = b.getBoundingClientRect(); return { h: r.height, text: b.innerText.replace(/\\s+/g, ' ').trim(), action: b.getAttribute('data-confirm-action') }; })()`,
  )
}
check('empty.confirm-bar-open', Boolean(emptyBar), emptyBar ? emptyBar.text.slice(0, 140) : 'no bar')
check('empty.confirm-bar-branch-upstream', /发布分支并建立 upstream/.test(emptyBar?.text ?? ''), emptyBar?.text ?? '')
check('empty.confirm-bar-setupstream', /setUpstream=true/.test(emptyBar?.text ?? ''), emptyBar?.text ?? '')
check('empty.confirm-bar-height', (emptyBar?.h ?? 999) <= 48, `h=${emptyBar?.h}`)
const actionBoxesAfter = await evaluate(
  `[...document.querySelectorAll('[data-testid^=action-]')].map((b) => { const r = b.getBoundingClientRect(); return [r.x, r.y, r.width, r.height]; })`,
)
check('empty.confirm-bar-bbox-stable', JSON.stringify(actionBoxesBefore) === JSON.stringify(actionBoxesAfter))
check('empty.no-write-before-accept', lastOpBefore === (await text('[data-testid=status-last-op]')))
await screenshot('02-empty-push-confirm-bar.png')
if (emptyBar) {
  await click('[data-testid=confirm-bar-cancel]')
  await sleep(250)
}

// ── 3) NO_UPSTREAM 保真（不破坏） ──
await selectCard('TEST_B_NOUP')
await waitFor(`document.querySelector('[data-testid=action-push]')?.getAttribute('data-enabled') === 'true'`, 8_000)
await click('[data-testid=action-push]')
await sleep(400)
let noupBar = await text('[data-testid=confirm-bar]')
if (!noupBar) {
  await click('[data-testid=action-push]')
  await sleep(450)
  noupBar = await text('[data-testid=confirm-bar]')
}
check('no-upstream.confirm-bar', /将推送/.test(noupBar) && /发布分支/.test(noupBar) && /setUpstream=true/.test(noupBar), noupBar.slice(0, 160))
if (noupBar) {
  await click('[data-testid=confirm-bar-cancel]')
  await sleep(200)
}

// ── 4) 5 键 tooltip 冻结格式（M2a 回归） ──
const actionTitles = await evaluate(
  `[...document.querySelectorAll('[data-testid^=action-]')].map((b) => ({ title: b.title, enabled: b.getAttribute('data-enabled') }))`,
)
const disabledTitles = actionTitles.filter((item) => item.enabled === 'false').map((item) => item.title)
const tooltipRegex = /^.+ \[E_[A-Z_]+\] → .+$/
check(
  'm2a.tooltip-format',
  disabledTitles.length > 0 && disabledTitles.every((title) => tooltipRegex.test(title)),
  disabledTitles[0] ?? 'no disabled title',
)

// ── 5) 操作审计（「日志与诊断」内嵌） ──
await click('[data-testid=menu-tools]')
await sleep(200)
await click('[data-testid="menu-item-tools.logs"]')
await sleep(300)
const auditReady = await waitFor(`document.querySelector('[data-testid=audit-status]')?.innerText.includes('更新于')`, 10_000)
check('audit.open-and-loaded', auditReady && (await count('[data-testid=audit-section]')) === 1)
check('audit.row-count-page1', (await count('[data-testid=audit-row]')) === 20, `rows=${await count('[data-testid=audit-row]')}`)
check('audit.range-total', /共 47 条/.test(await text('[data-testid=audit-range]')), await text('[data-testid=audit-range]'))
const firstRow = await evaluate(
  `(() => { const r = document.querySelector('[data-testid=audit-row]'); if (!r) return null; return { action: r.getAttribute('data-action'), result: r.getAttribute('data-result'), componentId: r.getAttribute('data-component-id'), ts: r.querySelector('[data-testid=audit-ts]')?.innerText, actor: r.children[1]?.innerText, job: r.querySelector('[data-testid=audit-job]')?.innerText, heads: r.querySelector('[data-testid=audit-heads]')?.innerText }; })()`,
)
check('audit.row-fields', Boolean(firstRow?.ts && firstRow?.actor && firstRow?.action && firstRow?.componentId), JSON.stringify(firstRow))
// Host 实际契约的 before/after 是 JSON 投影：UI 必须能解析出 HEAD（不能显示 “— → —”）
check('audit.row-heads-from-json-projection', /[0-9a-f]{7}/.test(firstRow?.heads?.split('→')[0] ?? ''), firstRow?.heads ?? '')
const initialCalls = (await bridgeCalls()).filter((call) => call.method === 'getOperationLog').length
check('audit.host-bridge-used', initialCalls >= 1, `getOperationLog calls=${initialCalls}`)
await screenshot('03-audit-list.png')

// action 过滤（真实键盘输入 + Enter）
await click('[data-testid=audit-filter-action]')
await send('Input.insertText', { text: 'PUSH' })
await enter()
await sleep(600)
const filterState = await evaluate(
  `(() => { const rows = [...document.querySelectorAll('[data-testid=audit-row]')]; return { count: rows.length, allPush: rows.every((r) => r.getAttribute('data-action') === 'PUSH'), range: document.querySelector('[data-testid=audit-range]')?.innerText }; })()`,
)
const lastAuditCall = (await bridgeCalls()).filter((call) => call.method === 'getOperationLog').slice(-1)[0]
check('audit.filter-action', filterState.allPush && filterState.count > 0, JSON.stringify(filterState) + ' params=' + JSON.stringify(lastAuditCall?.params))
check('audit.filter-params-host', lastAuditCall?.params?.action === 'PUSH', JSON.stringify(lastAuditCall?.params))
await screenshot('04-audit-filter-action.png')

// result 过滤（select change）
await evaluate(`(() => { const el = document.querySelector('[data-testid=audit-filter-result]'); el.value = 'FAILED'; el.dispatchEvent(new Event('change')); })()`)
await sleep(600)
const resultFilter = await evaluate(
  `(() => { const rows = [...document.querySelectorAll('[data-testid=audit-row]')]; return { count: rows.length, allFailed: rows.every((r) => r.getAttribute('data-result') === 'FAILED'), total: document.querySelector('[data-testid=audit-range]')?.innerText }; })()`,
)
check('audit.filter-result', resultFilter.count > 0 && resultFilter.allFailed, JSON.stringify(resultFilter))
// 清空过滤，回到第 1 页
await evaluate(`(() => { const el = document.querySelector('[data-testid=audit-filter-action]'); el.value = ''; el.dispatchEvent(new Event('input')); el.dispatchEvent(new Event('change')); })()`)
await evaluate(`(() => { const el = document.querySelector('[data-testid=audit-filter-result]'); el.value = ''; el.dispatchEvent(new Event('change')); })()`)
await click('[data-testid=audit-refresh]')
await sleep(700)

// 分页
const page1Range = await text('[data-testid=audit-range]')
await click('[data-testid=audit-next]')
await sleep(600)
const page2 = await evaluate(
  `(() => ({ range: document.querySelector('[data-testid=audit-range]')?.innerText, page: document.querySelector('[data-testid=audit-page]')?.innerText, rows: document.querySelectorAll('[data-testid=audit-row]').length }))()`,
)
check('audit.pagination', /第 21–40/.test(page2.range ?? '') && /第 2 \//.test(page2.page ?? ''), JSON.stringify({ page1Range, ...page2 }))

// 导出 CSV → toast 路径 + 打开所在目录（openPath reveal）
await click('[data-testid=audit-export-csv]')
await sleep(700)
const exportToast = await allToasts()
const exportPath = await text('[data-testid=audit-export-path]')
const csvCall = (await bridgeCalls()).filter((call) => call.method === 'exportOperationLog').slice(-1)[0]
check('audit.export-csv', /CSV/.test(exportToast) && exportPath.endsWith('.csv'), `${exportToast.slice(0, 170)} path=${exportPath}`)
check('audit.export-params-csv', csvCall?.params?.format === 'csv', JSON.stringify(csvCall?.params))
await screenshot('05-audit-export-csv.png')
await click('[data-testid=toast-action]')
await sleep(500)
const openCalls = await evaluate(`window.__M2B2_TEST__.openCalls.slice()`)
check(
  'audit.open-export-dir',
  openCalls.some((call) => call.mode === 'reveal' && String(call.path).endsWith('.csv')),
  JSON.stringify(openCalls.slice(-1)),
)
// 导出 JSON
await click('[data-testid=audit-export-json]')
await sleep(700)
const jsonCall = (await bridgeCalls()).filter((call) => call.method === 'exportOperationLog').slice(-1)[0]
check('audit.export-json', jsonCall?.params?.format === 'json' && (await text('[data-testid=audit-export-path]')).endsWith('.json'), JSON.stringify(jsonCall?.params))
// Host export 未实现：不得伪造成功
await evaluate(`window.__M2B2_TEST__.exportMode = 'unavailable'`)
await click('[data-testid=audit-export-csv]')
await sleep(600)
const exportError = await text('[data-testid=audit-export-error]')
check('audit.export-unavailable-no-fake-success', /未实现|失败/.test(exportError), exportError.slice(0, 180))
await evaluate(`window.__M2B2_TEST__.exportMode = 'ok'`)
await screenshot('06-audit-export-json.png')
await escape()
await sleep(300)

// ── 6) 备份 ref 恢复（组件详情抽屉） ──
await selectCard('TEST_C_BACKUP')
await dblclick('[data-testid=component-card][data-component-id="TEST_C_BACKUP"]')
await sleep(800)
check('backup.drawer-open', (await count('[data-testid=component-drawer]')) === 1)
const backupLoadingOrReady = await waitFor(`document.querySelectorAll('[data-testid=backup-ref]').length >= 1 || !!document.querySelector('[data-testid=backup-empty]')`, 8_000)
const backupRows = await evaluate(
  `[...document.querySelectorAll('[data-testid=backup-ref]')].map((r) => ({ ref: r.getAttribute('data-ref'), text: r.innerText.replace(/\\s+/g, ' ').trim() }))`,
)
check('backup.refs-loaded', backupLoadingOrReady && backupRows.length === 2, JSON.stringify(backupRows))
check(
  'backup.ref-fields',
  backupRows.every((row) => row.text.length > 20) && /refs\/backup\/sync\//.test(backupRows[0]?.ref ?? ''),
  backupRows[0]?.text ?? '',
)
const backupCall = (await bridgeCalls()).filter((call) => call.method === 'getBackupRefs').slice(-1)[0]
check('backup.host-bridge-params', backupCall?.params?.componentId === 'TEST_C_BACKUP' && backupCall?.params?.repoType === 'IMPL', JSON.stringify(backupCall?.params))
await screenshot('07-backup-refs.png')

// 选择 ref → 确认（显示将覆盖当前状态），未确认不发 restore
const restoreCallsBefore = (await bridgeCalls()).filter((call) => call.method === 'restoreBackupRef').length
await click('[data-testid=backup-restore]')
await sleep(350)
const confirmText = await text('[data-testid=confirm-dialog]')
check('backup.confirm-shows-overwrite', /覆盖当前分支状态/.test(confirmText) && /refs\/backup\/sync\//.test(confirmText), confirmText.slice(0, 200))
check(
  'backup.no-restore-before-confirm',
  (await bridgeCalls()).filter((call) => call.method === 'restoreBackupRef').length === restoreCallsBefore,
)
await screenshot('08-backup-restore-confirm.png')
await click('[data-testid=confirm-dialog] .confirm-actions .btn:last-child')
await sleep(1_200)
const successToast = await allToasts()
const restoreCalls = (await bridgeCalls()).filter((call) => call.method === 'restoreBackupRef')
check('backup.restore-success-toast', /已从备份 ref 恢复/.test(successToast), successToast.slice(0, 200))
check('backup.restore-safety-ref-shown', /恢复前 HEAD 已备份/.test(successToast), successToast.slice(0, 240))
check('backup.restore-host-confirm', restoreCalls.slice(-1)[0]?.params?.confirm === true, JSON.stringify(restoreCalls.slice(-1)?.params))
const snapshotCount = (await bridgeCalls()).filter((call) => call.method === 'getSnapshot').length
check('backup.refresh-after-restore', snapshotCount >= 2, `getSnapshot calls=${snapshotCount}`)
await screenshot('09-backup-restore-success.png')

// 清掉旧 toast，避免把上一次成功 toast 误当成本次结果。
const successToastCount = () =>
  evaluate(`[...document.querySelectorAll('[data-testid=toast]')].filter((n) => /已从备份 ref 恢复/.test(n.innerText)).length`)
const successToastsBefore = await successToastCount()
await evaluate(`[...document.querySelectorAll('[data-testid=toast]')].forEach((node) => node.click())`)
await sleep(300)

// Host 拒绝（ok=false）：不得显示成功
const restoreRefsAgain = await evaluate(
  `[...document.querySelectorAll('[data-testid=backup-ref]')].map((r) => r.getAttribute('data-ref'))`,
)
const rejectRef = restoreRefsAgain.find((ref) => String(ref).includes('20261114162000'))
check('backup.reject-ref-present', Boolean(rejectRef), String(rejectRef))
await evaluate(
  `(() => { const rows = [...document.querySelectorAll('[data-testid=backup-ref]')]; const row = rows.find((r) => r.getAttribute('data-ref') === ${JSON.stringify('refs/backup/sync/20261114162000/IMPL/main')}); row?.querySelector('[data-testid=backup-restore]')?.scrollIntoView({block:'center'}); })()`,
)
await sleep(150)
await evaluate(
  `(() => { const rows = [...document.querySelectorAll('[data-testid=backup-ref]')]; const row = rows.find((r) => r.getAttribute('data-ref') === ${JSON.stringify('refs/backup/sync/20261114162000/IMPL/main')}); const btn = row?.querySelector('[data-testid=backup-restore]'); if (btn) btn.click(); })()`,
)
await sleep(350)
const rejectConfirm = await count('[data-testid=confirm-dialog]')
check('backup.reject-confirm-open', rejectConfirm === 1, `dialog=${rejectConfirm}`)
await click('[data-testid=confirm-dialog] .confirm-actions .btn:last-child')
await sleep(900)
const toastTexts = await evaluate(
  `[...document.querySelectorAll('[data-testid=toast]')].map((n) => n.innerText.replace(/\\s+/g, ' ').trim())`,
)
const lastToast = toastTexts.slice(-1)[0] ?? ''
check('backup.reject-no-fake-success', /恢复失败|Host 拒绝/.test(lastToast) && !/已从备份 ref 恢复/.test(lastToast), lastToast)
check('backup.reject-no-new-success-toast', (await successToastCount()) <= successToastsBefore)
check('backup.reject-error-visible', /Host 拒绝|ok=false/.test(await text('[data-testid=backup-restore-error]')))
await screenshot('10-backup-restore-rejected.png')

// 无备份空态：切到 DESIGN（bridge 无该仓 refs）
await evaluate(
  `document.querySelector('[data-testid=backup-repo-tab][data-repo-type=DESIGN]')?.click()`,
)
await sleep(600)
check('backup.empty-state', (await count('[data-testid=backup-empty]')) === 1, await text('[data-testid=backup-empty]'))
await screenshot('11-backup-empty.png')
await escape()
await sleep(250)

// ── 7) M2b-1 回归抽样：任务中心空态 ──
await click('[data-testid=title-tasks]')
await sleep(600)
check('m2b1.task-center-empty', (await count('[data-testid=task-center]')) === 1 && (await count('[data-testid=task-empty]')) === 1, await text('[data-testid=task-empty]'))
await escape()
await sleep(250)

// ── 8) Mock 兼容（无 Host / 浏览器 dev 路径）：内置 Mock 审计与备份 ref，不依赖测试桥 ──
await send('Page.removeScriptToEvaluateOnNewDocument', { identifier: bridgeScript.result?.identifier })
await send('Page.addScriptToEvaluateOnNewDocument', { source: 'window.__DSH_FORCE_MOCK__ = true;' })
await send('Page.reload', { ignoreCache: true })
const mockBooted = await waitFor(`!!document.querySelector('[data-testid=component-list-count]')`, 20_000)
await sleep(900)
check('mock.boot', mockBooted && (await text('[data-testid=snapshot-source]')).includes('Mock'), await text('[data-testid=snapshot-source]'))
check('mock.no-offline-banner', (await count('[data-testid=offline-banner]')) === 0)
await click('[data-testid=menu-tools]')
await sleep(200)
await click('[data-testid="menu-item-tools.logs"]')
await waitFor(`document.querySelectorAll('[data-testid=audit-row]').length > 0`, 8_000)
check('mock.audit-source', (await text('[data-testid=audit-source]')) === 'Mock 审计', await text('[data-testid=audit-source]'))
check('mock.audit-rows', (await count('[data-testid=audit-row]')) === 20, `rows=${await count('[data-testid=audit-row]')}`)
check('mock.audit-total', /共 57 条/.test(await text('[data-testid=audit-range]')), await text('[data-testid=audit-range]'))
await click('[data-testid=audit-export-csv]')
await sleep(500)
const mockExport = await text('[data-testid=audit-export-path]')
check('mock.audit-export', mockExport.endsWith('.csv') && (await allToasts()).includes(mockExport), mockExport)
await escape()
await sleep(250)
// Mock 备份 ref：有 SYNC 备份的组件可见；无备份组件为空态
await selectCard('CF123A_ShortName')
await dblclick('[data-testid=component-card][data-component-id="CF123A_ShortName"]')
await sleep(700)
await waitFor(`document.querySelectorAll('[data-testid=backup-ref]').length > 0`, 6_000)
check('mock.backup-refs', (await count('[data-testid=backup-ref]')) >= 1, `rows=${await count('[data-testid=backup-ref]')}`)
const mockSuccessBefore = await successToastCount()
await click('[data-testid=backup-restore]')
await sleep(300)
await click('[data-testid=confirm-dialog] .confirm-actions .btn:last-child')
await sleep(1_000)
check('mock.backup-restore-success', (await successToastCount()) > mockSuccessBefore, (await allToasts()).slice(0, 200))
await evaluate(`[...document.querySelectorAll('[data-testid=toast]')].forEach((node) => node.click())`)
await escape()
await sleep(250)
await selectCard('CF900A_TractionCtrl')
await dblclick('[data-testid=component-card][data-component-id="CF900A_TractionCtrl"]')
await sleep(700)
await waitFor(`!!document.querySelector('[data-testid=backup-empty]') || document.querySelectorAll('[data-testid=backup-ref]').length > 0`, 6_000)
check('mock.backup-empty', (await count('[data-testid=backup-empty]')) === 1, await text('[data-testid=backup-empty]'))
await escape()
await sleep(250)

// ── 9) 页面错误检查 ──
check('page.no-console-errors', pageErrors.length === 0, pageErrors.slice(0, 3).join(' | '))

// ── 收尾 ──
const failed = results.filter((item) => !item.ok)
const summary = {
  total: results.length,
  passed: results.length - failed.length,
  failed: failed.map((item) => item.id),
  httpPort,
  cdpPort,
  checks: results,
}
writeFileSync(join(outDir, 'result.json'), JSON.stringify(summary, null, 2))
console.log(JSON.stringify({ total: summary.total, passed: summary.passed, failed: summary.failed }, null, 2))
try {
  ws.close()
} catch {
  /* ignore */
}
killEdgeTree(edgeProcess?.pid)
cleanupStraySelfcheckEdges()
try {
  globalThis.__staticServer?.close()
} catch {
  /* ignore */
}
try {
  rmSync(profileDir, { recursive: true, force: true })
} catch {
  /* ignore */
}
process.exit(failed.length === 0 ? 0 : 1)
