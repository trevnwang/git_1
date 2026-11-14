/**
 * M2a 单源/确认条/状态栏/tooltip/M2b/审计 CDP 自检（只读；confirm-bar 仅打开后取消，零写）。
 * 前置：Host 已启动并开启 CDP 9222；真实/Mock 均可。
 * 用法：node ui/scripts/cdp-m2a-selfcheck.mjs
 */
import { readFileSync, existsSync, mkdirSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const results = [];
function check(id, ok, detail = '') {
  results.push({ id, ok, detail });
  console.log(`${ok ? 'PASS' : 'FAIL'} ${id}${detail ? ' :: ' + detail : ''}`);
}
const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

const targets = await fetch('http://127.0.0.1:9222/json/list').then((r) => r.json());
if (!targets.length) throw new Error('CDP 无页面');
const ws = new WebSocket(targets[0].webSocketDebuggerUrl);
await new Promise((resolve, reject) => {
  ws.addEventListener('open', resolve);
  ws.addEventListener('error', reject);
});
let seq = 0;
const pending = new Map();
ws.addEventListener('message', (event) => {
  const message = JSON.parse(event.data);
  if (message.id && pending.has(message.id)) {
    pending.get(message.id)(message);
    pending.delete(message.id);
  }
});
const send = (method, params = {}) =>
  new Promise((resolve) => {
    const id = ++seq;
    pending.set(id, resolve);
    ws.send(JSON.stringify({ id, method, params }));
  });
const evaluate = async (expression) => {
  const response = await send('Runtime.evaluate', {
    expression,
    returnByValue: true,
    awaitPromise: true,
  });
  return response.result?.result?.value;
};
const click = async (selector) => {
  const point = await evaluate(`(() => { const e = document.querySelector(${JSON.stringify(selector)}); if (!e) return null; const r = e.getBoundingClientRect(); return { x: r.x + r.width / 2, y: r.y + r.height / 2 }; })()`);
  if (!point) return false;
  await send('Input.dispatchMouseEvent', { type: 'mousePressed', x: point.x, y: point.y, button: 'left', clickCount: 1 });
  await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x: point.x, y: point.y, button: 'left', clickCount: 1 });
  return true;
};

await send('Runtime.enable');
await sleep(400);

// 1) Host 单源 actions：COMMIT/PUSH requiresConfirm=true
const componentId = await evaluate(`document.querySelector('[data-testid=component-card]')?.getAttribute('data-component-id') || [...document.querySelectorAll('[data-testid=component-card]')][0]?.querySelector('.comp-id')?.textContent?.trim() || ''`);
const detail = await evaluate(`new Promise((resolve) => { const id = crypto.randomUUID(); const h = (e) => { const d = typeof e.data === 'string' ? JSON.parse(e.data) : e.data; if (!d || String(d.id) !== id) return; chrome.webview.removeEventListener('message', h); resolve(d); }; chrome.webview.addEventListener('message', h); chrome.webview.postMessage({ id, method: 'getComponentDetail', params: { componentId: ${JSON.stringify('RTCT_Demo')} } }); })`);
const repos = detail?.data?.repos ?? [];
const actions = repos[0]?.actions ?? [];
const commit = actions.find((a) => a.action === 'COMMIT');
const push = actions.find((a) => a.action === 'PUSH');
check('ui-08.host-confirm-commit', commit?.requiresConfirm === true, `commit=${commit?.requiresConfirm}`);
check('ui-08.host-confirm-push', push?.requiresConfirm === true, `push=${push?.requiresConfirm}`);

// 2) tooltip 紧凑格式（选 MISSING 组件）
await evaluate(`[...document.querySelectorAll('[data-testid=component-card]')].find(x => x.textContent.includes('RTCT_Incomplete'))?.click()`);
await sleep(600);
const tips = await evaluate(`[...document.querySelectorAll('[data-testid^=action-]')].map(b => b.title)`);
const compact = /^.+ \[E_[A-Z_]+\] → .+$/;
check('ui-08.tooltip-format', tips.length === 5 && tips.every((t) => compact.test(t)), tips[0] ?? '');

// 3) 确认条：PUSH 打开后取消；高度 ≤48、5 键 bbox 不变、0 写（status 不变）
await evaluate(`[...document.querySelectorAll('[data-testid=component-card]')].find(x => x.textContent.includes('RTCT_Demo'))?.click()`);
await sleep(600);
const before = await evaluate(`[...document.querySelectorAll('[data-testid^=action-]')].map(b => { const r = b.getBoundingClientRect(); return [r.x, r.y, r.width, r.height]; })`);
const statusBefore = await evaluate(`document.querySelector('[data-testid=status-last-op]')?.textContent?.trim() || ''`);
const opened = await click('[data-testid=action-push]');
await sleep(400);
const bar = await evaluate(`(() => { const b = document.querySelector('[data-testid=confirm-bar]'); if (!b) return null; const r = b.getBoundingClientRect(); return { h: r.height, text: b.innerText.replace(/\\s+/g, ' ').trim() }; })()`);
const after = await evaluate(`[...document.querySelectorAll('[data-testid^=action-]')].map(b => { const r = b.getBoundingClientRect(); return [r.x, r.y, r.width, r.height]; })`);
await click('[data-testid=confirm-bar-cancel]');
await sleep(300);
const statusAfter = await evaluate(`document.querySelector('[data-testid=status-last-op]')?.textContent?.trim() || ''`);
check('ui-10b.confirm-bar', Boolean(opened && bar), bar?.text ?? 'no bar');
check('ui-10b.bar-height', Boolean(bar && bar.h <= 48), `h=${bar?.h}`);
check('ui-10b.bbox-stable', JSON.stringify(before) === JSON.stringify(after));
check('ui-10b.no-write-before-accept', statusBefore === statusAfter, `${statusBefore} => ${statusAfter}`);

// 4) 状态栏模板
const statusRegex = /^最近一次操作：/;
check('ui-10.status-template', statusRegex.test(statusAfter), statusAfter);

// 5) M2b 批量入口：点击 SYNC 必须有可感知结果（确认对话框 / 任务接口未就绪提示），未确认 0 写
await evaluate(`document.querySelector('[data-testid=batch-select-toggle]')?.click()`);
await sleep(300);
await click('[data-testid=batch-sync]');
await sleep(700);
const batchConfirmText = await evaluate(`document.querySelector('[data-testid=confirm-dialog]')?.innerText?.replace(/\\s+/g, ' ').trim() || ''`);
const batchToastText = await evaluate(`[...document.querySelectorAll('[data-testid=toast]')].map((n) => n.textContent).join(' | ')`);
const batchFeedback = batchConfirmText || batchToastText;
check(
  'ui-15.batch-entry-feedback',
  /SYNC|任务接口|Host 任务/.test(batchFeedback),
  batchFeedback.slice(0, 140),
);
check('ui-15.no-old-boundary-copy', !batchFeedback.includes('M2b 未接入'), batchFeedback.slice(0, 140));
if (batchConfirmText) await click('[data-testid=confirm-dialog] .confirm-actions .btn:first-child');
await sleep(200);
await evaluate(`document.querySelector('.batch-bar .close-btn')?.click()`);

// 6) 审计 JSONL：actor/requestId
const auditPath = join(process.env.LOCALAPPDATA ?? '', 'MbdGitTool', 'audit', 'operations.jsonl');
let auditOk = false;
let auditDetail = auditPath;
try {
  if (existsSync(auditPath)) {
    const lines = readFileSync(auditPath, 'utf8').trim().split('\n');
    const last = JSON.parse(lines[lines.length - 1]);
    auditOk = typeof last.actor === 'string' && typeof last.requestId === 'string';
    auditDetail = `actor=${last.actor} requestId=${last.requestId}`;
  }
} catch (error) {
  auditDetail = String(error);
}
check('op-33.audit-actor-requestid', auditOk, auditDetail);

ws.close();
const failed = results.filter((r) => !r.ok);
const outDir = join(dirname(fileURLToPath(import.meta.url)), '..', '..', 'artifacts', 'm2a-selfcheck');
mkdirSync(outDir, { recursive: true });
writeFileSync(join(outDir, 'result.json'), JSON.stringify({ total: results.length, passed: results.length - failed.length, failed: failed.map((r) => r.id), checks: results }, null, 2));
console.log(JSON.stringify({ total: results.length, passed: results.length - failed.length, failed: failed.map((r) => r.id) }, null, 2));
process.exit(failed.length === 0 ? 0 : 1);
