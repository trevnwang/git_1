# M1 状态：本地扫描 + Gitea 只读同步 + 真实看板（M1-a）

> 日期：2026-11-14　范围：SRS v0.2.1 §9.2（FR-SYNC 清单部分）、§9.3（FR-SCAN）、§9.4（FR-STA）、§12.1–12.2（桥接）。
> 结论：**M1-a 完成并通过端到端验证**；M1-b（远程 refs/fetch、三阶段首扫、GitWorker 管道）待续。

## 1. 已实现

| 能力 | 说明 |
|---|---|
| 组件/仓库识别 | `RepoNameParser`：去 `_Design/_Impl/_MIL/_SIL` 尾缀（大小写不敏感）；`X_SIL_Design` 等异常不绑定 |
| 单仓状态 | `RepoFacts` + `RepoStateEvaluator`：14 态、UNMERGED>IN_PROGRESS、NO_UPSTREAM 三子型、stale/扫描失败/remote_gone→UNKNOWN |
| 组件聚合 | `ComponentAssembler`：§7.4 全序、severity_rank、binding_cause（INVALID=1 / INCOMPLETE=12）、mixed_branch、tag 一致性 |
| 本地扫描 | `LocalScanner`：遍历 workspace 根直接子目录；`git --no-optional-locks status --porcelain=v2 --branch`、upstream、ahead/behind、unmerged、merge/rebase 中间态、shallow、stash、index.lock；单仓失败隔离 |
| Gitea 只读同步 | `GiteaClient`：`GET /api/v1/orgs/{org}/repos?page&limit=50` 分页；`Authorization: token`；401/403/429 分类异常；token 全链路不落日志 |
| 合并语义 | 远程缺仓 → `BINDING_ERROR(INCOMPLETE)`；远程有/本地无 → 单仓 `MISSING`；本地有/远程无 → `remote_gone`；本地不完整且远程未就绪 → `UNKNOWN + incomplete`（H-01） |
| Host↔UI 桥 | WebView2 WebMessage：`{id,method:"getSnapshot"|"refresh"}` → `{id,ok,data}`；仅接受 `app.local`；camelCase |
| 配置 | `%LOCALAPPDATA%\MbdGitTool\config.json`（workspaceRoot / gitea.*）；环境变量覆盖 `MBD_WORKSPACE_ROOT/MBD_GITEA_URL/MBD_GITEA_ORG/MBD_GITEA_TOKEN`；token 脱敏 |
| UI 真实数据 | 数据源徽标（Mock/本地扫描/本地扫描+Gitea）、工作区路径、扫描中提示、45s 超时、失败自动重试 2 次 + Mock 回退、刷新按钮、Toast |

## 2. 文件地图

```
src/MbdGitTool.Core/
├─ Contracts/SnapshotDto.cs          # UI↔Host 冻结契约
└─ Domain/
   ├─ RepoNameParser.cs              # 命名解析
   ├─ RepoFacts.cs                   # 单仓事实
   ├─ RepoStateEvaluator.cs          # 14 态判定
   └─ ComponentAssembler.cs          # 组件聚合/DTO

src/MbdGitTool.Host/
├─ Bridge/WebViewBridge.cs           # WebMessage 请求/响应
├─ Services/AppConfig.cs             # 配置加载（token 脱敏）
├─ Services/GitCli.cs                # git 参数数组调用
├─ Services/LocalScanner.cs          # 本地扫描
├─ Services/GiteaClient.cs           # Gitea 只读分页
├─ Services/SnapshotService.cs       # 组装 + 缓存 + 刷新互斥
└─ MainWindow.xaml.cs                # 配置 → WebView2 → 桥 + 预热
```

## 3. 验证证据

| 验证 | 结果 |
|---|---|
| VS MSBuild | 0 warning / 0 error |
| vstest | **71/71 通过**（新增解析/状态/聚合测试） |
| 本地扫描（CDP 真机） | workspace 夹具：2 组件 / 6 仓；识别 CF100A（4 仓，BEHIND）、CF200B（2/4，UNKNOWN+incomplete）；`X_SIL_Design`、`not-a-repo` 未绑定并记日志 |
| 本地+Gitea（假 Gitea API） | 3 组件：CF100A **BEHIND**（MIL 落后）、CF200B **BINDING_ERROR**（远程缺 MIL/SIL）、CF300C **MISSING**（远程独有、本地未克隆）；数据源 `本地扫描 + Gitea` |
| 刷新 | 点击刷新 → `刷新中…` → 完成 Toast「已刷新 · 本地扫描 + Gitea」，行数稳定 |
| 控制台 | 0 error / 0 exception；1440×900 无横向溢出 |
| 真实 Gitea | RTCRepos 15 仓 + 3 轮真实提交；工具接入后 4 组件/14 仓，Demo=BEHIND、Synced=SYNCED、RemoteOnly=MISSING、Incomplete=BINDING_ERROR；详见 [real-gitea-test.md](real-gitea-test.md) |\n| 鼠标/键盘 | CDP Input 事件：搜索输入/清空、点行、切 Tab、点导航、点设置、点刷新全部通过；0 console error |\n| 截图 | ![M1 看板](images/m1/board-real.png) · [组件列表](images/m1/components-real.png) · [真实 Gitea 测试](real-gitea-test.md) |

## 4. 如何本地运行

```powershell
cd D:\Workspace\git_1
npm --prefix ui run build
dotnet build MbdGitTool.sln

# 仅本地扫描
$env:MBD_WORKSPACE_ROOT = 'D:\Work\MBD'; dotnet run --project src\MbdGitTool.Host

# 带 Gitea（token 建议从环境变量注入，勿落盘/日志）
$env:MBD_GITEA_URL = 'https://gitea.example.com'
$env:MBD_GITEA_ORG = 'MBD_Org'
$env:MBD_GITEA_TOKEN = '<PAT>'
```

## 5. 已知偏差 / M1-b 待办

1. **Git 执行位置**：当前由 Host 直接调用 `git`（`GitCli`），尚未走 SRS §12.1 的 Host→GitWorker 命名管道；M1-b 需迁移。
2. **远程 refs/fetch**：当前只同步组织仓库清单；分支/Tag 当前值、fetch 巡检、remote-tracking 新鲜度未实现 → BEHIND/AHEAD 依赖本地已 fetch 的 ref。
3. **首扫三阶段进度**：当前只有“扫描中…”状态，无 `LOCAL_DISCOVERY/REMOTE_RECONCILE/STATUS_CALC` 进度协议。
4. **未绑定仓 UI**：目前仅日志告警，未列入 UI 清单（FR-BOARD-06）。
5. **tag_moved / LFS 缺失检测**：未实现（flags 预留）。
6. **Gitea 退避重试/ETag**：仅分类异常，无退避；M1-b 补 429/5xx 退避与水位增量。
7. **性能**：400 仓首扫约 10–20s（串行 git 子进程），M1-b 做并发/分层扫描。
