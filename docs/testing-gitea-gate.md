# 测试环境 Gitea 门禁（M2b）

`http://192.168.50.20:3000` 只是当前开发机的**测试环境**。换电脑/CI 时该地址可能不可达，因此：

## 规则

1. **依赖真实 Gitea 的测试/自检必须先探测，不可达就 SKIP（exit 0），不得失败。**
2. 探测地址（按优先级）：
   - `MBD_GITEA_PROBE_URL`（仅探测用）
   - `MBD_GITEA_URL`
   - 默认 `http://192.168.50.20:3000`
3. 探测端点：`GET <base>/api/v1/version`，超时 2500ms；不重试。
4. 单元测试（`tests/`）默认使用本地 bare remote/临时目录，不依赖真实 Gitea；只有真实 Host CDP 自检和人工真实环境验证受门禁影响。
5. 跳过时写 `artifacts/<name>/result.json`：`{skipped:true, reason, url, hint}`，并在控制台打印。

## 使用

```powershell
# 1) 单元测试永远可跑
dotnet test MbdGitTool.sln

# 2) 统一自检（Mock 必跑；真实 Gitea 自动探测）
#    前置：Host 已启动且 CDP 9222 可用
node ui/scripts/run-selfchecks.mjs

# 3) 指定其它 Gitea 测试环境
$env:MBD_GITEA_URL='http://192.168.50.20:3000'
$env:MBD_GITEA_PROBE_URL='http://192.168.50.20:3000'
node ui/scripts/run-selfchecks.mjs
```

## 实现

- `ui/scripts/lib/gitea-gate.mjs`：`probeGitea` / `requireGiteaOrSkip`。
- `ui/scripts/cdp-m2b2-realhost-check.mjs`：启动即 `requireGiteaOrSkip(...)`，不可达直接 SKIP。
- `ui/scripts/run-selfchecks.mjs`：聚合 Mock + 真实自检。
