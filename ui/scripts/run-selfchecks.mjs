/**
 * 统一自检 runner（换机器/CI 友好）：
 *  - 永远运行 Mock 自检（不依赖真实 Gitea）；
 *  - 真实 Gitea 自检先探测，不可达则 SKIP（exit 0），可达才运行；
 *  - 任何被运行脚本失败才以非 0 退出；
 *  - 结果写入 docs/evidence/m2b-manifest.json（冻结规格 §16.9 证据 manifest）。
 *
 * 前置：Host 已启动并开启 CDP 9222（脚本自身不启动 Host，避免端口冲突）。
 * 用法：node ui/scripts/run-selfchecks.mjs
 * 环境：MBD_GITEA_URL / MBD_GITEA_PROBE_URL 指向测试 Gitea。
 */
import { spawnSync } from 'node:child_process'
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { execFileSync } from 'node:child_process'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { probeGitea, giteaProbeUrl } from './lib/gitea-gate.mjs'

const here = dirname(fileURLToPath(import.meta.url))
const mockScripts = [
  'cdp-m2a-selfcheck.mjs',
  'cdp-m2b1-fix-selfcheck.mjs',
  'cdp-m2b2-selfcheck.mjs',
]
const realScripts = ['cdp-m2b2-realhost-check.mjs']

const suites = []
const artifactMap = {
  'cdp-m2a-selfcheck.mjs': 'artifacts/m2a-selfcheck/result.json',
  'cdp-m2b1-fix-selfcheck.mjs': 'artifacts/m2b1-fix-selfcheck/result.json',
  'cdp-m2b2-selfcheck.mjs': 'artifacts/m2b2-selfcheck/result.json',
  'cdp-m2b2-realhost-check.mjs': 'artifacts/m2b2-realhost/result.json',
}
const acMap = {
  'cdp-m2a-selfcheck.mjs': ['AC-OP-06', 'AC-OP-07', 'AC-OP-08', 'AC-OP-09', 'AC-OP-10', 'AC-OP-15', 'AC-OP-16', 'AC-OP-17', 'AC-OP-20', 'AC-OP-21', 'AC-OP-22', 'AC-OP-28', 'AC-OP-29', 'AC-OP-33'],
  'cdp-m2b1-fix-selfcheck.mjs': ['AC-OP-03', 'AC-OP-24', 'AC-OP-26', 'AC-OP-27', 'AC-OP-34', 'AC-OP-35'],
  'cdp-m2b2-selfcheck.mjs': ['AC-OP-22', 'AC-OP-23', 'AC-OP-25', 'AC-OP-28', 'AC-OP-30', 'AC-UI-10c'],
  'cdp-m2b2-realhost-check.mjs': ['AC-OP-01', 'AC-OP-02', 'AC-OP-04', 'AC-OP-05', 'AC-OP-31', 'AC-OP-32', 'AC-OP-34', 'AC-SAFE-13'],
}
const failures = []
function run(script, required) {
  const startedAt = new Date().toISOString()
  const result = spawnSync(process.execPath, [join(here, script)], { stdio: 'inherit' })
  const status = result.status === 0 ? 'PASS' : 'FAIL'
  suites.push({ name: script, command: `node ui/scripts/${script}`, startedAt, finishedAt: new Date().toISOString(), result: status, artifact: artifactMap[script] })
  if (result.status !== 0 && required) failures.push(script)
}

console.log('=== Mock / Host-independent selfchecks ===')
for (const script of mockScripts) run(script, true)

const probe = await probeGitea()
console.log(`=== Real Gitea selfchecks (${giteaProbeUrl()}) ===`)
if (probe.reachable) {
  for (const script of realScripts) run(script, true)
} else {
  const skipped = {
    skipped: true,
    reason: 'Gitea unreachable; real-Gitea selfchecks skipped (not a failure)',
    url: probe.url,
    error: probe.error ?? '',
    hint: '设置 MBD_GITEA_URL/MBD_GITEA_PROBE_URL 后重跑',
  }
  for (const script of realScripts) {
    suites.push({ name: script, result: 'SKIP', artifact: skipped })
  }
  console.log(JSON.stringify(skipped, null, 2))
}

let commit = ''
try {
  commit = execFileSync('git', ['rev-parse', '--short', 'HEAD'], { cwd: join(here, '..', '..'), encoding: 'utf8' }).trim()
} catch {
  commit = 'unknown'
}
// AC-OP-34：每个 result.json 补齐 target/url/reachable/skipped/commit/timestamp/checks。
for (const suite of suites) {
  if (!suite.artifact) continue
  try {
    const absolute = join(here, '..', '..', suite.artifact)
    const existing = existsSync(absolute) ? JSON.parse(readFileSync(absolute, 'utf8')) : {}
    const augmented = {
      target: suite.name,
      url: giteaProbeUrl(),
      reachable: probe.reachable,
      skipped: suite.result === 'SKIP',
      commit,
      timestamp: new Date().toISOString(),
      checks: existing.checks ?? [],
      total: existing.total ?? null,
      passed: existing.passed ?? null,
      failed: existing.failed ?? [],
    }
    writeFileSync(absolute, JSON.stringify(augmented, null, 2))
  } catch {
    /* ignore */
  }
}
const manifest = {
  commit,
  generatedAt: new Date().toISOString(),
  suite: suites,
  checks: suites.flatMap((suite) => {
    let detail = null
    try {
      const absolute = join(here, '..', '..', suite.artifact ?? '')
      if (suite.artifact && existsSync(absolute)) detail = JSON.parse(readFileSync(absolute, 'utf8'))
    } catch {
      /* ignore */
    }
    const checks = Array.isArray(detail?.checks) ? detail.checks : []
    if (checks.length === 0) return [{ suite: suite.name, status: suite.result, evidence: suite.artifact }]
    return checks.map((check) => ({
      suite: suite.name,
      check: check.id,
      status: check.ok === true ? 'PASS' : 'FAIL',
      evidence: suite.artifact,
    }))
  }),  note: 'SKIP 不计入 COVERED；AC 逐条证据见 docs/m2b-ac-op-gap.md',
}
const evidenceDir = join(here, '..', '..', 'docs', 'evidence')
mkdirSync(evidenceDir, { recursive: true })
writeFileSync(join(evidenceDir, 'm2b-manifest.json'), JSON.stringify(manifest, null, 2))

if (failures.length > 0) {
  console.error(`FAILED: ${failures.join(', ')}`)
  process.exit(1)
}
console.log('ALL_RUNNABLE_SELFCHECKS_PASSED')
