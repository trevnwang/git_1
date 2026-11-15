# M0 骨架状态（完成于 2026-11-14）

> 依据：SRS v0.2.1 §3.3 M0（App Shell、导航、设计 token、CI 占位锚点、Mock 数据、测试基建）。
> **M0 只做骨架 + Mock + 测试基建，不实现 Git 状态计算/SYNC/CI 真实接入。**

## 1. 已完成

| M0 交付 | 实现 | 证据 |
|---|---|---|
| SDK 固定 | 仓库根 `global.json` → .NET **10.0.404**（`rollForward=latestFeature`） | `dotnet --version` = 10.0.404 |
| 解决方案骨架 | 根目录 `MbdGitTool.sln`（src/tests 解决方案文件夹）：Core / Host / Git / Core.Tests | 4 项目构建 0 warning 0 error |
| 领域模型骨架 | 14 单仓状态、15 组件聚合态、`RepoFlags`、`BindingCause`、`ComponentAggregation`（§7.4 全序 + UNKNOWN 兜底 + severity） | 10 个 xUnit 单测全绿 |
| WPF + WebView2 宿主 | `MbdGitTool.Host`：启动 WebView2、映射 `ui/` 虚拟主机、加载失败兜底页、M0 日志 | 隐藏启动 8s 存活；`navigation completed: success=True` |
| 前端 App Shell | Vue 3 + TS + Vite 8：左侧导航、标题栏、看板表格/详情、设置页、状态栏 | `npm run build` 成功，产物拷入 Host 输出 |
| 设计 token | `ui/src/styles/tokens.css`：SRS §11.2 双通道色板 + 深色 + reduced-motion | 与 SRS 色值一致 |
| 看板 Mock | 7 个示例组件，覆盖 SYNCED/BEHIND/DIVERGED/DIRTY/DETACHED/MISSING/UNKNOWN + CI 六态 | 页面可渲染 |
| CI 10 锚点 | 见 §2 的 `data-testid` 映射 | 前端源码可 grep |
| GitWorker 骨架 | `MbdGitTool.Git` 控制台进程占位（协议 M1 实现） | 构建通过 |

## 2. CI 锚点（SRS §11.4 / AC-CI-01 对应）

| 锚点 | 位置 | `data-testid` / 标识 |
|---|---|---|
| CI-1 | 左侧导航“CI 流水线” | `ci-nav` |
| CI-2 | 组件行 CI 槽位 | `ci-slot` |
| CI-3 | 组件详情 CI 汇总条 | `ci-strip` |
| CI-4 | 单仓详情 CI Tab | `ci-tab` |
| CI-5 | 状态栏 CI 队列 | `ci-queue` |
| CI-6 | 通知中心 CI 类目 | `ci-notif-category` |
| CI-7 | 看板 CI 筛选 facet（禁用占位） | `ci-facet` |
| CI-8 | 设置页 CI 分区（禁用占位） | `ci-settings` |
| CI-9 | CI 契约（页面/设置内文本） | `ci-contract` |
| CI-10 | 卡片底色不受 CI 影响 | `ci-card-base` + `data-ci-affects-base="false"` |

## 3. 运行

```powershell
# 1) 构建前端（首次/前端变更后）
cd ui
npm install        # 首次
npm run build

# 2) 构建并运行桌面端
cd D:\Workspace\git_1
dotnet build MbdGitTool.sln
dotnet run --project src\MbdGitTool.Host

# 3) 测试
dotnet test MbdGitTool.sln
```

Host 启动日志：`%LOCALAPPDATA%\MbdGitTool\logs\host-m0.log`（用于轻量排障）。
若前端产物缺失，窗口会显示 fallback 页并提示先 `npm run build`。

## 4. 本次验证记录

| 验证 | 结果 |
|---|---|
| `dotnet build MbdGitTool.sln` | 0 warning / 0 error |
| `dotnet test MbdGitTool.sln` | 10/10 通过 |
| `npm --prefix ui run build` | 成功（Vite 8.3.8） |
| Host 隐藏启动（Start-Process + 8s） | 进程存活；日志 `webview2 initialized: 157.0.4341.46`、`navigation completed: success=True` |
| VS IDE 编译（devenv.com） | `4 成功 / 0 失败`（6.9s） |
| VS vstest.console | 10/10 通过（0.84s） |
| VS MSBuild Publish（Host+Git, Release/win-x64） | exit 0；`artifacts/publish/`；发布版 Host 隐藏启动 `navigation success=True` |
| Release 压缩包 | `artifacts/MbdGitTool-0.1.0-m0-win-x64.zip`（约 0.7MB） |

## 5. 未做（按计划留给后续）

- M1：Gitea 同步、本地扫描、真实状态计算、看板接真数据、首启三阶段；
- M1+：GitWorker 命名管道 JSON-RPC、SQLite WAL、任务中心、首启向导；
- M2+：批量写操作（Clone/SYNC/Tag/Delete/Restore）、备份 ref/journal；
- M3：MSIX 打包、性能门禁、C6（错误码全表）/ R-06。

## 6. UI 自检与可用性补齐（CDP 驱动）

2026-11-14 两轮 CDP 自检：第 1 轮修复 4 处布局问题；第 2 轮按用户反馈补齐全部导航入口（看板/组件/CI/任务/通知/日志/设置）、设置 6 分区、通知已读、日志过滤、行内操作 Toast 反馈，占位页明确标注 M1/M2。结果与截图见 [ui-review-m0.md](ui-review-m0.md)。

## 7. VS 工作流

标准 VS 开发/编译/测试/发布命令见 [vs-workflow.md](vs-workflow.md)；根目录 `MbdGitTool.sln` 可直接双击打开。

## 8. 注意

- `ui/dist` 与 `node_modules` 已加入 `.gitignore`；CI/Host 构建前必须先 `npm run build`。
- 本机 SDK 10.0.404 已固定；CI 需安装 .NET 10 SDK 并保持 `global.json` 一致。
