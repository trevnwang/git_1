# MBD 组件全生命周期 Git 工具 v1.0

![MBD Git Tool](docs/logo/logo-wordmark.png)

面向汽车 MBD（Model-Based Design）嵌入式组件开发的 Git 聚合操作台：把 Gitea 上大量独立仓库折叠成「组件」视角，提供组件级状态看板、四仓批量操作、单仓精细操作、任务中心、操作审计与本地安全恢复。

**当前版本：v1.0.0**（Windows 桌面，WPF + WebView2 + Vue 3）

---

## 1. v1.0 发布包

| 项目 | 内容 |
|---|---|
| 安装包 | 仓库根目录 `MbdGitTool-Setup-1.0.0.exe` |
| 安装方式 | 双击安装；默认安装到 `C:\Program Files\MBD Git Tool` |
| 系统要求 | Windows 10/11 x64；需要 **Microsoft Edge WebView2 Runtime**（Win11 通常已内置，Win10 若缺失请安装 Evergreen Runtime） |
| 卸载 | Windows「应用和功能」→ MBD 组件全生命周期 Git 工具；或开始菜单卸载快捷方式 |
| 签名 | **未做代码签名**，Windows SmartScreen 可能提示“未知发布者”，选择“仍要运行”即可 |
| 安装包内容 | 自包含 .NET 10 运行时 + Host + 前端资源（无需另装 .NET） |
| 文件大小 | 46,546,076 字节 |
| SHA256 | `CE19C9C04318507C686C4850D9C58CFA9F923B5518D68C88B22C112F09FEA06D` |

首次启动会自动进入四步配置向导（工作区 → Gitea → Token → 确认），可测试连接并“保存并启动”；之后也可在菜单「设置」中修改。未配置时标题栏会显示“未配置 · 去设置”，不会把 Mock 数据当作真实数据。

---

## 2. 功能概览

- **组件看板**：组件卡片、四仓状态聚合、14 单仓状态、筛选/搜索/排序、文件夹视图。
- **四仓文件树**：Design/Impl/MIL/SIL 2×2、懒加载、只读路径安全、危险扩展名确认。
- **单仓操作**：FETCH / PULL（`--ff-only`）/ CHKOUT / COMMIT / PUSH，底部按钮条与确认条。
- **批量操作**：Clone / Pull / Fetch / Checkout / SYNC / Delete / Restore / CREATE；任务中心逐仓进度、取消、仅重试失败项、重启不重复执行。
- **安全**：写前 journal + 崩溃 reconcile、`.trash` 软删除（30 天）、`refs/backup/sync/*` 备份恢复、DELETE_PENDING、审计 ≥90 天、Token 使用 Windows DPAPI 加密存于 `%LOCALAPPDATA%\MbdGitTool\token.bin`，仅以 header 发送。
- **组件修复**：Repair / Re-clone、NO_UPSTREAM 三子型修复、detached HEAD 建分支、EMPTY 首推。
- **组件管理**：按组件 ID 纳入、停止管理、本地删除（强确认）、已移出视图、待处理删除。
- **审计与导出**：operation_log 查询/CSV/JSON 导出、JSONL 兼容、脱敏。
- **CI**：仅按 v1 设计预留 10 个锚点与数据契约，真实 CI 未接入。

---

## 3. v1.0 已知限制与待确认项（上线必读）

> v1.0 功能可用，但**尚未完成 M2b 最终 Gate 三方签字**。以下项请在正式推广前确认；不影响常规看板/单仓/批量操作，但影响“全 AC 签收”的口径。

### 3.1 明确未实现 / 暂缓

1. **批量切 Tag 暂不开放**（D-029）：各版本 Tag 的仓集合不一致（如 1.0 四仓、2.0 缺 MIL、3.0 缺 MIL/SIL），不按“四仓同一 Tag”实现；UI 入口已禁用，后端代码保留但不验收。
2. **真实 CI 未接入**：CI 10 锚点、`CiLatest`/`CiAggregate` 契约为占位；`ci.enabled=false`，显示“CI 未接入”。
3. **v1.1 功能**：Reset（保留/混合/硬）、自动更新、旧看板表格视图、CI 真实接入、Tag 管理。
4. **安装包未签名**：无代码签名证书，SmartScreen 可能告警。
5. **WebView2 Runtime 未随安装包捆绑**：需系统已安装 Evergreen Runtime。

### 3.2 部分实现 / 待补证据（M2b Gate 遗留）

以下项有实现/主路径测试，但 AC Then 的边界或证据仍未全部收口：

| 范围 | 待补 |
|---|---|
| AC-OP-26 | `CRASH_AT_ITEM_2` 作业级确定性终止用例 |
| AC-OP-27 | 严格取消 selfcheck（快速完成时当前会跳过历史断言） |
| AC-OP-30 | 恢复前的“目标已存在/远程已变/备份过期”冲突预览 |
| AC-OP-31 / 32 / AC-UI-16 | 组件纳入缺仓的 `BINDING_ERROR` 看板呈现、组件行写入、CDP 证据 |
| AC-SAFE-05 / 06 | 真实崩溃转储/诊断包演练 |
| AC-SAFE-10 / 11 | 危险扩展名/路径穿越的完整安全证据归档 |
| AC-OP-03 / 09 | Clone 部分失败“清除失败态”确认、Checkout 5 前置态逐用例 |
| AC-REPO-01…08 / 11 | 单仓修复类逐条 AC 证据（Repair/Re-clone/首推已有实现与部分测试） |
| AC-OP-34 / 35 | 证据台账逐条签字、Release 构建忽略注入验证 |

### 3.3 待确认

1. **Tag 切换策略**：若后续恢复，需要按版本/仓集合配置可切 Tag 的仓集合，而非固定四仓。
2. **生产 Gitea 兼容性**：当前在测试 Gitea（RTCRepos）验证；生产组织规模/权限/PAT scope 需现场确认。
3. **审计留存/备份策略**：默认审计 90 天、`.trash`/备份 ref 30 天，需按公司规范确认。
4. **安装目录/权限**：默认 Program Files 需要管理员权限；如需免管理员安装请提出。

---

## 4. 验证基线（v1.0 构建）

| 项目 | 结果 |
|---|---|
| 单元/集成测试 | **423/423 PASS**（Core + Host，0 warning） |
| UI CDP 自检 | M2a 11/11、M2b-1 68/68、M2b-2 53/53、realhost 19/19，四套全 PASS |
| 真实 Gitea | 测试环境 `RTCRepos` 四套 CDP 通过 |
| 故障注入 | GIT_NET_* / INDEX_LOCK / DIR_MOVE_BUSY / DISK_WRITE_FAIL / DISK_SPACE_LOW / GIT_SLOW / CRASH_BEFORE/AFTER 注入矩阵 PASS |
| 证据 | `docs/evidence/m2b-manifest.json`、`docs/evidence/security/*` |
| Gate | **M2b 最终 Gate 未签收**；README §3 列出剩余项 |

复跑命令：

```powershell
# 全量测试
dotnet test MbdGitTool.sln

# 启动 Host（UI/CDP 自检前置）
dotnet run --project src\MbdGitTool.Host

# 四套真实/Mock 自检（Host 已启动且 CDP 9222 可连）
node ui\scripts\run-selfchecks.mjs
```

---

## 5. 开发与构建

### 5.1 环境

- Windows 10/11 x64
- Visual Studio 2026（或 .NET SDK 10.0.x）
- Node.js 24.x（构建前端）
- Git for Windows
- NSIS 3（仅打包安装包需要，默认 `C:\Program Files (x86)\NSIS`）

### 5.2 编译/测试/运行

```powershell
npm --prefix ui install
npm --prefix ui run build
dotnet build MbdGitTool.sln -c Release
dotnet test  MbdGitTool.sln -c Release
dotnet run   --project src\MbdGitTool.Host
```

### 5.3 生成 v1.0 安装包

```powershell
# 1) Release 自包含发布（win-x64）
dotnet publish src\MbdGitTool.Host\MbdGitTool.Host.csproj `
  -c Release -r win-x64 --self-contained true `
  -o artifacts\release\win-x64 `
  -p:DebugType=None -p:DebugSymbols=false

# 2) NSIS 打包（根目录 installer.nsi）
& "C:\Program Files (x86)\NSIS\makensis.exe" installer.nsi
# 产物：MbdGitTool-Setup-1.0.0.exe
```

解决方案文件：`MbdGitTool.sln`（VS 2026 直接打开）。

---

## 6. 仓库结构

```
MbdGitTool.sln                 # VS 根解决方案
installer.nsi                  # NSIS v1.0 安装包脚本
README.md                      # 本文件（v1.0 发布说明）
reqs/
├─ SRS-v1.0.md                 # ★ v1.0 SRS：一条需求一个标题（需求号即标题）
├─ adr/                        # 架构决策记录（D-025/026/028/029）
├─ fixtures/                   # 状态矩阵夹具
└─ archive/                    # 历史 0.x 规格（只读归档）
docs/
├─ evidence/                   # v1.0 证据 manifest / G-WIRE 安全记录
├─ images/                     # 界面参考图与截图
├─ m2b-ac-op-gap.md            # AC 台账（现状/剩余证据）
├─ m2b-fault-injection-report.md
├─ dev-environment.md          # 开发环境
├─ testing-gitea-gate.md       # Gitea 测试门禁
├─ vs-workflow.md              # VS 构建/测试/发布
├─ real-gitea-test.md          # 真实 Gitea 操作记录
└─ archive/                    # 历史评审/阶段状态/设计稿（只读归档）
src/
├─ MbdGitTool.Core/            # 领域模型/状态机/契约
├─ MbdGitTool.Host/            # WPF + WebView2 宿主（Bridge/RPC/服务）
└─ MbdGitTool.Git/             # GitWorker 骨架
tests/
├─ MbdGitTool.Core.Tests/
├─ MbdGitTool.Host.Tests/
└─ MbdGitTool.CrashDrillWorker/ # 真实崩溃演练 worker
ui/                            # Vue 3 + TS + Vite
artifacts/                     # 构建/发布产物（gitignored）
```

---

## 7. 文档版本策略

- **v1.0 起不再维护 0.x 版本文档正文**；0.x 规格、评审、阶段状态全部归档到 `reqs/archive/`、`docs/archive/`，仅作历史证据。
- 需求唯一维护源：`reqs/SRS-v1.0.md`（一条需求一个标题）；验收台账：`docs/m2b-ac-op-gap.md`。
- 安装包版本与 README 版本一致：`v1.0.0`。
- 后续变更：新增版本时复制 SRS 并递增版本号，历史版本进入 archive。

---

## 8. 安全与数据

- Token 使用 Windows DPAPI（CurrentUser）加密存于 `%LOCALAPPDATA%\\MbdGitTool\\token.bin`；不写日志/命令行/remote URL，config.json 中 token 为 null。
- 删除仅本地 `.trash`（30 天）；SYNC 前写 `refs/backup/sync/*`；恢复前写 `refs/backup/restore/*`。
- 写操作前 journal + 崩溃 reconcile；不猜测成功。
- 审计 `operation_log`（SQLite + JSONL）默认保留 90 天。
- 真实数据目录：`%LOCALAPPDATA%\MbdGitTool\`（jobs/db/audit/journal）。
