/**
 * 统一自检 runner（换机器/CI 友好）：
 *  - 永远运行 Mock 自检（不依赖真实 Gitea）；
 *  - 真实 Gitea 自检先探测，不可达则 SKIP（exit 0），可达才运行；
 *  - 任何被运行脚本失败才以非 0 退出。
 *
 * 前置：Host 已启动并开启 CDP 9222（脚本自身不启动 Host，避免端口冲突）。
 * 用法：node ui/scripts/run-selfchecks.mjs
 * 环境：MBD_GITEA_URL / MBD_GITEA_PROBE_URL 指向测试 Gitea。
 */
import { spawnSync } from 'node:child_process'
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

const failures = []
function run(script, required) {
  const result = spawnSync(process.execPath, [join(here, script)], { stdio: 'inherit' })
  if (result.status !== 0 && required) failures.push(script)
}

console.log('=== Mock / Host-independent selfchecks ===')
for (const script of mockScripts) run(script, true)

const probe = await probeGitea()
console.log(`=== Real Gitea selfchecks (${giteaProbeUrl()}) ===`)
if (probe.reachable) {
  for (const script of realScripts) run(script, true)
} else {
  console.log(
    JSON.stringify(
      {
        skipped: true,
        reason: 'Gitea unreachable; real-Gitea selfchecks skipped (not a failure)',
        url: probe.url,
        error: probe.error ?? '',
        hint: '设置 MBD_GITEA_URL/MBD_GITEA_PROBE_URL 后重跑',
      },
      null,
      2,
    ),
  )
}

if (failures.length > 0) {
  console.error(`FAILED: ${failures.join(', ')}`)
  process.exit(1)
}
console.log('ALL_RUNNABLE_SELFCHECKS_PASSED')
