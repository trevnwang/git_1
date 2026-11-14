#!/usr/bin/env node
/**
 * cdp-m2a-selfcheck.mjs — M2a 出口 AC 的 CDP 自检脚本（**断言清单占位 / 待实现**）
 * ---------------------------------------------------------------------------
 * 状态：PENDING（2026-11-14 v0.3.1-r2 只建清单，不实现、不运行、不伪造通过）。
 *
 * 目的：把 SRS v0.3.1-r2 §13.1.2 + ADR D-025 §5 要求的 CDP 断言固化成可复跑脚本，
 * 供 M2a 复评（Gate）与后续 CI 使用。实现后必须：
 *   1) 用 WebView2 CDP + Input.dispatchMouseEvent / Input.dispatchKeyEvent 真实事件
 *      （禁止 JS click / 程序化 DOM 调用，见 AC-UI 的驱动方式约定）；
 *   2) 逐格读取单源夹具 `reqs/v0.2/fixtures/m2-matrix.json`（禁止在本脚本内手写第三份矩阵）；
 *   3) 每条断言输出 {id, ac, ok, expected, actual}，任一失败 → 退出码 1；全部通过 → 退出码 0。
 *
 * 当前占位脚本行为：打印待实现清单后 **以退出码 2 结束**，因此 CI 不会把它误判为通过。
 * 旧脚本 `ui/scripts/cdp-m2-selfcheck.mjs` 已作废（仍含 PULL/PUSH 零确认旧期望），不得作为证据。
 *
 * 用法（实现后）：
 *   $env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS='--remote-debugging-port=9222'
 *   node ui/scripts/cdp-m2a-selfcheck.mjs            # 真实事件自检
 *   node ui/scripts/cdp-m2a-selfcheck.mjs --list     # 只打印断言清单（JSON）
 *   node ui/scripts/cdp-m2a-selfcheck.mjs --ac ui-10a # 只跑某组
 *
 * 证据：运行输出（stdout JSON + 截图路径）登记到 `docs/m2a-ac-report.md`；
 * 本脚本执行成功只代表 CDP 层断言通过，不代表 Host 安全 AC（SAFE-14…17）与真实写证据齐全。
 */

import { readFile } from 'node:fs/promises'
import { fileURLToPath } from 'node:url'
import path from 'node:path'

const HERE = path.dirname(fileURLToPath(import.meta.url))
const REPO_ROOT = path.resolve(HERE, '..', '..')
const MATRIX_PATH = path.join(REPO_ROOT, 'reqs', 'v0.2', 'fixtures', 'm2-matrix.json')
const CDP_PORT = Number(process.env.M2A_CDP_PORT ?? 9222)

/**
 * 待实现断言清单（占位）。每条 entry:
 *  - id: 与 docs/m2a-ac-report.md §2 的勾选分组对应
 *  - ac: SRS AC 编号；layer: host|cdp|e2e
 *  - selectors: 既有 data-testid（实现时如变更须同步 SRS/ADR/report）
 *  - assertions: 必须逐条断言；status 固定 PENDING 直到有人实现并勾选
 *  - evidence: 实现后应产出的证据形态
 */
export const PENDING_CHECKS = [
  {
    id: 'ui-08',
    ac: 'AC-UI-08',
    layer: 'cdp+host',
    title: '14 主状态 × 5 键逐格：disabled/requiresConfirm 与单源夹具一致；禁用 title 格式冻结',
    selectors: ['[data-testid=action-pull]', '[data-testid=action-fetch]', '[data-testid=action-chkout]', '[data-testid=action-commit]', '[data-testid=action-push]'],
    assertions: [
      '读取 reqs/v0.2/fixtures/m2-matrix.json（禁止手写矩阵）',
      'for state of fixture.states: 选中对应仓，逐动作 assert(button.disabled === !action.enabled)',
      'Host 层另由单测断言 GetAvailability 与夹具逐格相等（enabled/requiresConfirm/reasonCode）',
      'disabled 按钮 title 匹配 /^.+ \\[E_[A-Z_]+\\] → .+$/（v0.3.1-r2 冻结；旧冒号三段式作废）',
      'UI 源码不存在第二套矩阵（静态断言：viewmodel.ts 无 MATRIX）',
    ],
    evidence: ['Host 单测输出', 'CDP JSON（state×action×expected/actual）', '最长 title 样本'],
    status: 'PENDING',
  },
  {
    id: 'ui-09',
    ac: 'AC-UI-09',
    layer: 'cdp',
    title: 'M2a 阶段门禁：5 键逐一点击均有可感知结果，无“可点无效果”',
    selectors: ['[data-testid=repo-actions]', '[data-testid=toast]', '[data-testid=action-dialog]', '[data-testid=confirm-bar]'],
    assertions: [
      '对 5 键逐一点击（真实鼠标事件）',
      '每次要么发起 runRepoAction、要么出现 action-dialog/confirm-bar、要么有明确 toast/禁用原因（三者其一）',
      '无“点击后无任何可见变化且无 bridge 调用”的静默路径',
      'M1/M0 构建只读断言不回归（构建版本由 HostInfo 判定，非本脚本默认）',
    ],
    evidence: ['点击序列 JSON', '每步截图/文本'],
    status: 'PENDING',
  },
  {
    id: 'ui-10',
    ac: 'AC-UI-10',
    layer: 'cdp+e2e',
    title: '前置/失败/进行中/回写/状态栏模板',
    selectors: ['[data-testid=status-last-op]', '[data-testid=action-commit]', '[data-testid=action-pull]'],
    assertions: [
      '无 staged：COMMIT disabled 且 title 含“无已暂存改动”+ E_NOTHING_TO_COMMIT',
      '非快进 PULL 夹具：状态栏含错误码与“ff-only/手动”指引，无 merge commit',
      'index.lock 夹具：E_REPO_BUSY 且锁文件仍在',
      '失败后状态栏左段匹配 /最近一次操作：.+ — (成功|失败\\(E_[A-Z_]+\\))/（FR-OPS-05 模板）',
      '成功写后 performance.now() 计时 ≤2000ms 回写（真实写链路需 E2E 配合）',
    ],
    evidence: ['状态栏文本', '锁文件前后状态', '回写耗时 JSON'],
    status: 'PENDING',
  },
  {
    id: 'ui-10a',
    ac: 'AC-UI-10a',
    layer: 'cdp',
    title: 'COMMIT 两步：提交信息对话框 → 确认条（将提交 N 个文件）→ 执行',
    selectors: ['[data-testid=action-commit]', '[data-testid=action-dialog]', '[data-testid=action-dialog-message]', '[data-testid=action-dialog-confirm]', '[data-testid=commit-stage-all]', '[data-testid=confirm-bar]', '[data-testid=confirm-bar-text]', '[data-testid=confirm-bar-accept]'],
    assertions: [
      '步骤1 点击 action-commit：action-dialog 可见；confirm-bar 不可见；bridgeCalls(runRepoAction) 增量 0',
      '输入 message “m2a-gate” 后点 action-dialog-confirm：confirm-bar 可见',
      'confirm-bar-text 匹配 /将提交 \\d+ 个文件/（「已暂存」不得插在数字与“个文件”之间）',
      'confirm-bar 高度 ≤48px；repo-actions 5 键 bbox 与开条前逐像素一致',
      'Escape → 0 写且焦点回 action-commit；accept → runRepoAction(COMMIT) 恰 1 次',
      '未勾选 commit-stage-all 时 args 无 stageAll；勾选时 args.stageAll===true && confirm===true',
    ],
    evidence: ['两步截图', 'confirm-bar 文本/高度/bbox JSON', 'bridgeCalls 计数与 args'],
    status: 'PENDING',
  },
  {
    id: 'ui-10b',
    ac: 'AC-UI-10b',
    layer: 'cdp+e2e',
    title: 'PUSH 确认条：零确认写禁止 + Alt+5 同路径 + NO_UPSTREAM 发布分支',
    selectors: ['[data-testid=action-push]', '[data-testid=confirm-bar]', '[data-testid=confirm-bar-accept]'],
    assertions: [
      '点击 action-push 仅出现 confirm-bar；文本匹配 /将推送 \\d+ 个提交（.+ → .+）/；高度 ≤48px',
      '未确认/取消/ESC/Alt+5：bridgeCalls(runRepoAction) 增量 0（禁止零确认写）',
      'Alt+5 与按钮同路径（同样开条），不得绕过确认条',
      'accept 后恰 1 次 PUSH；NO_UPSTREAM 夹具 args.setUpstream===true 且文案含“发布分支/建立 upstream”',
      'DETACHED 夹具 action-push disabled 且 title 匹配冻结格式',
      '真实 E2E：远端 ref 前后变化与回执一致',
    ],
    evidence: ['confirm-bar 截图/文本', 'setUpstream args', 'git ls-remote 前后值'],
    status: 'PENDING',
  },
  {
    id: 'ui-11',
    ac: 'AC-UI-11',
    layer: 'cdp',
    title: '信息面板 E/F 同聚焦仓、空态、STALE、1280/200%、标签列宽',
    selectors: ['[data-testid=repo-actions]', '[data-testid=info-repo-title]', '[data-testid=info-branch]'],
    assertions: [
      'E 面板标题 === F 按钮条焦点仓（同快照）',
      '未选中：信息面板空态且无残留 info-branch',
      'STALE 夹具：显示“数据 X 前”与灰化',
      '视口 1280×800 与 200%：scrollWidth===clientWidth（无横向溢出）',
      '标签列宽 ≤72px',
    ],
    evidence: ['bbox JSON', '空态截图', 'STALE 截图'],
    status: 'PENDING',
  },
  {
    id: 'ui-13',
    ac: 'AC-UI-13',
    layer: 'cdp',
    title: '焦点闭环：确认条/对话框/抽屉初始焦点、陷阱、ESC 归还',
    selectors: ['[data-testid=confirm-bar]', '[data-testid=action-dialog]', '[data-testid=confirm-dialog]', '[data-testid=component-drawer]', '[data-testid=component-card]'],
    assertions: [
      '打开确认条/对话框/抽屉后 document.activeElement 在容器内',
      'Tab×N 不逃逸容器（焦点陷阱）',
      'Escape 关闭后焦点回到触发控件（按钮/卡片）',
      '键盘可完成确认/取消',
    ],
    evidence: ['activeElement 序列 JSON', '焦点截图'],
    status: 'PENDING',
  },
  {
    id: 'ui-15',
    ac: 'AC-UI-15',
    layer: 'cdp',
    title: 'M2b 边界：批量/任务中心入口明示“M2b 未接入（批量/作业/审计）”且 0 Host 写',
    selectors: ['[data-testid=batch-bar]', '[data-testid=title-tasks]', '[data-testid=drawer]', '[data-testid=component-card]'],
    assertions: [
      '点击批量条 SYNC/CREATE/CLONE/DELETE：文案包含“M2b 未接入（批量/作业/审计）”',
      '点击任务中心入口与“了解批量入口/取消·重试失败项”：同上文案',
      '每次点击 bridgeCalls(runRepoAction/getJob/listJobs) 增量均为 0',
      '无静默无响应、无“已提交/已同步/已完成”等伪成功回执',
      'C/D/E 主区 bbox 不变；全页无“M2 接入”旧措辞',
    ],
    evidence: ['点击序列文本/截图', 'bridge 计数 JSON', 'bbox 前后值'],
    status: 'PENDING',
  },
  {
    id: 'safe-cdp',
    ac: 'AC-SAFE-14/15/16/17（CDP 侧只做可观测面）',
    layer: 'cdp',
    title: '安全类 AC 的 UI 可观测断言（安全判定主体在 Host 单测/黑盒）',
    selectors: ['[data-testid=status-last-op]', '[data-testid=action-*]'],
    assertions: [
      '失败错误码（E_CREDENTIAL_SCOPE/E_CONCURRENT_CHANGE/E_WORKTREE_OVERWRITE/E_REF_NOT_FOUND）在状态栏/Toast 可读，禁止静默成功',
      'E_CREDENTIAL_SCOPE/E_CONCURRENT_CHANGE 后仓库状态回写未变（CDP 可读快照）',
      '本组不得替代 Host 层 7 场景/4 动作/ignored/ref 断言',
    ],
    evidence: ['状态栏文本', '错误码截图'],
    status: 'PENDING',
  },
  {
    id: 'op-33',
    ac: 'AC-OP-33',
    layer: 'e2e+host',
    title: '审计-lite：每次动作恰 1 行（CDP 只负责触发；查询在 Host/DB 层）',
    selectors: ['[data-testid=status-last-op]'],
    assertions: [
      '通过 UI 各触发 1 次成功 + 1 次失败动作',
      '动作后查询 operation_log：行数增量恰 1，最小列齐全（ts/actor/组件/RepoType/action/before_head/after_head/result/error_code/request_id）',
      '重启 Host 后可查、同一 request_id 不重复',
      '本脚本不直接查 DB 时，须输出触发时间与 request_id 供 Host 侧比对',
    ],
    evidence: ['触发时间/request_id JSON', 'operation_log 查询输出'],
    status: 'PENDING',
  },
]

function parseArgs(argv) {
  const out = { list: false, ac: null }
  for (let i = 2; i < argv.length; i += 1) {
    const a = argv[i]
    if (a === '--list') out.list = true
    else if (a === '--ac') out.ac = argv[++i] ?? null
  }
  return out
}

function printChecklist(checks) {
  const lines = []
  lines.push('M2a CDP 自检 · 断言清单（PENDING，占位脚本，不代表通过）')
  lines.push(`CDP 端口: ${CDP_PORT} · 单源夹具: ${MATRIX_PATH}`)
  for (const c of checks) {
    lines.push('')
    lines.push(`[${c.status}] ${c.id} · ${c.ac} · ${c.title}`)
    for (const s of c.selectors) lines.push(`  selector: ${s}`)
    for (const a of c.assertions) lines.push(`  assert: ${a}`)
    lines.push(`  evidence: ${c.evidence.join(' / ')}`)
  }
  console.error(lines.join('\n'))
}

async function main() {
  const args = parseArgs(process.argv)
  let checks = PENDING_CHECKS
  if (args.ac) checks = checks.filter((c) => c.ac.includes(args.ac) || c.id === args.ac)

  if (args.list) {
    console.log(JSON.stringify({ status: 'PENDING', script: 'cdp-m2a-selfcheck.mjs', checks }, null, 2))
    process.exit(0)
  }

  // 占位阶段仅校验夹具存在与可解析（不连 CDP、不点真实界面、不写任何数据）。
  let fixture = null
  try {
    fixture = JSON.parse(await readFile(MATRIX_PATH, 'utf8'))
  } catch (error) {
    console.error(`[FAIL] 无法读取单源夹具 ${MATRIX_PATH}: ${error.message}`)
    process.exit(1)
  }
  if (!Array.isArray(fixture.states) || fixture.states.length !== 14) {
    console.error(`[FAIL] 单源夹具应为 14 个主状态，实际 ${fixture.states?.length ?? 0}`)
    process.exit(1)
  }

  printChecklist(checks)
  console.error('\n[PENDING] 本脚本为 v0.3.1-r2 断言清单占位：CDP/Host 断言尚未实现，不得作为 M2a 通过证据。')
  console.error('          实现后：全部通过 exit 0；任一失败 exit 1；环境不可用 exit 3。')
  process.exit(2)
}

const isMain = process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)
if (isMain) {
  await main()
}
