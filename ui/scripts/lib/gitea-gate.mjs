/**
 * 真实 Gitea 测试门禁（M2b CI/本地测试通用）。
 *
 * 目的：`192.168.50.20:3000` 只是当前开发机的测试环境；换机器时该地址可能不可达。
 * 所有依赖真实 Gitea 的测试/自检必须先探测，不可达时**跳过（exit 0）**而不是失败。
 *
 * 约定：
 *  - 探测地址优先级：`MBD_GITEA_URL` > `MBD_GITEA_PROBE_URL` > 默认 `http://192.168.50.20:3000`
 *  - 探测端点：`<base>/api/v1/version`（Gitea 公共只读接口，不需要 token）
 *  - 单次超时默认 2500ms，不重试（测试启动不应被离线拖住）
 */
import { mkdirSync, writeFileSync } from 'node:fs'
import { dirname, join } from 'node:path'

export const DEFAULT_GITEA_URL = 'http://192.168.50.20:3000'

export function giteaProbeUrl() {
  return (
    process.env.MBD_GITEA_PROBE_URL ||
    process.env.MBD_GITEA_URL ||
    DEFAULT_GITEA_URL
  ).replace(/\/+$/, '')
}

/** 探测 Gitea 是否可达；任何网络/HTTP 错误都视为不可达。 */
export async function probeGitea(url = giteaProbeUrl(), timeoutMs = 2500) {
  const controller = new AbortController()
  const timer = setTimeout(() => controller.abort(), timeoutMs)
  try {
    const response = await fetch(`${url}/api/v1/version`, {
      method: 'GET',
      signal: controller.signal,
      headers: { Accept: 'application/json' },
    })
    return { reachable: response.ok, url, status: response.status }
  } catch (error) {
    return { reachable: false, url, status: 0, error: String(error?.message ?? error) }
  } finally {
    clearTimeout(timer)
  }
}

/**
 * 真实 Gitea 测试入口调用：不可达时打印 SKIP、写 result.json、以 0 退出。
 * @param {string} name 测试/脚本名
 * @param {string} outDir 结果目录（可为空）
 */
export async function requireGiteaOrSkip(name, outDir = '') {
  const result = await probeGitea()
  if (result.reachable) {
    console.log(`GITEA_READY ${name} :: ${result.url} (HTTP ${result.status})`)
    return result
  }

  const payload = {
    skipped: true,
    reason: 'Gitea unreachable; real-Gitea test skipped (not a failure)',
    target: name,
    url: result.url,
    status: result.status,
    error: result.error ?? '',
    hint: '设置 MBD_GITEA_URL/MBD_GITEA_PROBE_URL 指向可用测试 Gitea 后重跑',
  }
  if (outDir) {
    mkdirSync(outDir, { recursive: true })
    writeFileSync(join(outDir, 'result.json'), JSON.stringify(payload, null, 2))
  }
  console.log(JSON.stringify(payload, null, 2))
  process.exit(0)
}

/** 仅探测并返回布尔，供 runner 聚合多个脚本。 */
export async function isGiteaReachable() {
  return (await probeGitea()).reachable
}

/** 需要调用方自建结果目录时复用：确保目录存在。 */
export function ensureDir(dir) {
  mkdirSync(dir, { recursive: true })
  return dir
}

/** 脚本文件所在目录的父级 artifacts 路径辅助（保持各脚本行为一致）。 */
export function artifactDir(scriptDir, name) {
  return join(dirname(scriptDir), '..', '..', 'artifacts', name)
}
