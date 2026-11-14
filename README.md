# MBD 组件全生命周期 Git 工具

面向汽车 MBD（Model-Based Design）嵌入式组件开发的 Git 聚合操作台：把 Gitea 上 4,000+ 个独立仓库折叠成「组件」视角，提供组件级批量操作、状态聚合看板与单仓精细操作。CI 后续接入，v1 已在界面与数据契约中预留。

## 仓库结构（Visual Studio 风格）

```
MbdGitTool.sln                 # ★ Visual Studio 2026 根解决方案（src/tests 解决方案文件夹）
global.json                    # .NET SDK 10.0.100（rollForward=latestFeature）
Directory.Build.props          # 公共构建约定（Nullable / Analyzers / TreatWarningsAsErrors / win-x64）
Directory.Packages.props       # 中央包版本管理
.editorconfig  .vsconfig  .gitignore
src/
├─ MbdGitTool.Core/            # 领域模型：14 单仓状态 / 15 组件聚合态 / 聚合纯函数
├─ MbdGitTool.Host/            # WPF + WebView2 宿主（app.manifest：longPathAware + PerMonitorV2）
└─ MbdGitTool.Git/             # GitWorker 进程骨架（M1 实现命名管道 JSON-RPC）
tests/
└─ MbdGitTool.Core.Tests/      # xUnit（10 个聚合/severity 单测）
ui/                            # Vue 3 + TS + Vite：App Shell / 看板 Mock / CI 10 锚点 / 设计 token
docs/
├─ dev-environment.md          # 本机工具链体检与复现命令
├─ vs-workflow.md              # VS 打开/编译/测试/发布命令与验证记录
├─ ui-review-m0.md             # M0 界面自检（CDP 驱动、截图、修复记录）\n├─ m1-status.md                # M1-a：本地扫描 + Gitea 只读同步 + 真实看板（含验证/偏差）\n├─ real-gitea-test.md          # 真实 Gitea（RTCRepos）端到端 + 鼠标键盘操作测试
├─ ui-redesign-spec.md         # v3 工作台 UI 规格（参考图为准）
├─ ui-redesign-code-plan.md    # v3 API/文件树/路径安全实现方案
├─ ui-v3-status.md             # UI v3 阶段报告、评审 P0 修复与剩余项
├─ m2-ops-status.md            # M2 单仓真实写操作、staged-only 提交、评审修复与实测
├─ review-ui-v3.md / review-req-v3.md / review-code-v3.md   # 三路对抗评审
└─ m0-status.md                # M0 完成状态、运行方式、CI 锚点映射
artifacts/                     # 发布产物（gitignored）：publish/ + zip
reqs/                          # 需求工程（唯一需求源 + 评审证据 + 冻结资产）
├─ requirements.md             # ★ SRS v0.2.1（唯一维护源，冻结候选）
├─ review-report.md            # 需求审核报告
├─ review/01..08-*.md          # 6 路对抗评审 + 2 轮冻结复评
├─ v0.2/                       # AC 目录（162 条）/ D1–D6 图集 / 25 夹具 / 冻结清单
├─ archive/                    # docx/drawio 忠实转写（冻结）
└─ _tools/                     # 转写脚本与 dump
```

## 快速开始（VS 2026）

```powershell
# 1) 前端（首次/前端变更后）
npm --prefix ui install
npm --prefix ui run build

# 2) 打开解决方案（双击 MbdGitTool.sln 亦可）
& "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\devenv.exe" "D:\Workspace\git_1\MbdGitTool.sln"

# 3) 命令行编译 / 测试 / 运行
dotnet build MbdGitTool.sln
dotnet test  MbdGitTool.sln
dotnet run   --project src\MbdGitTool.Host
```

完整 VS 命令（devenv.com / MSBuild / vstest / Publish）见 [vs-workflow.md](docs/vs-workflow.md)。

## 维护约定

1. **需求只维护 `reqs/requirements.md`**；docx / drawio / archive 已冻结为证据，任何需求变更只改 md 并写修订记录。
2. 需求状态图例：`[已定]/[决策]/[待确认]/[设计建议]/[v1]/[v1.1]/[v2]/[废弃]`；P0/P1/P2 与版本归属是两个维度。
3. 已决策项见 SRS 第 0 章 ADR；未决项见第 15 章（OQ-xx）。
4. 验收以 `reqs/v0.2/ac-catalog.md` 为权威；执行图以 `v0.2/diagrams.md` 为准；测试夹具用 `v0.2/fixtures/`。
5. 代码改动遵循 SRS 冻结状态机/枚举/表名；实现机制（进程/IPC/库选型）以 SRS 第 12 章设计建议为准。

## UI v3 工作台（2026-11-14）

- 主界面按用户参考图重构：菜单栏/标题区/左组件卡片/中 2×2 四仓文件树/右信息面板/底部 5 键/状态栏；
- 新增文件夹视图（工作区树）、四仓文件树懒加载、右侧仓详情/最近提交、CI 10 锚点新落点；
- 接真实 RTCRepos + 本地工作区；216 单测通过；三路对抗评审 P0 已修（详见 `docs/ui-v3-status.md`）；
- 当前 M1 只读：底部写操作按钮统一禁用并提示 M2 接入。

## 当前状态（2026-11-14）

- **需求**：草案经 6 路对抗式评审判定不可直接开发（167 条发现）；已重建 SRS **v0.2.1**，第二轮冻结复评判定 **☑ 有条件进入 M0**（C1–C5 已关闭，C6 错误码全表为 M0 出口条件）。
- **M0 骨架已完成**：根级 VS 解决方案（.NET 10.0.404 固定）、领域聚合纯函数 + 10 个单测、WPF+WebView2 宿主、Vue App Shell（看板 Mock / 10 个 CI 锚点 / 状态栏）、VS 编译+测试+发布链路。
- **验证**：`devenv.com` 4/4 成功；VS MSBuild 0 warning/0 error；vstest 10/10；Release 发布产物 `navigation success=True`；zip 位于 `artifacts/`。
- **M1-a 已完成**：本地扫描 + Gitea 只读同步 + 真实状态计算 + 看板接真数据（71 测试、CDP 真机验证通过），详见 [m1-status.md](docs/m1-status.md)。\n- **下一步 M1-b**：远程 refs/fetch、三阶段首扫进度、GitWorker 管道、未绑定仓 UI、Gitea 退避。
- 待用户确认：容量口径、常驻形态、远程 SLO、Gitea 配额等，见 `requirements.md` 第 15 章（均非阻塞）。

## 提交约定（Conventional Commits）

- 每完成一个里程碑/可交付阶段提交一笔：`feat(m1): ...`、`fix(ui): ...`、`docs(reqs): ...`、`chore: ...`；
- 提交信息正文写：改了什么 / 为什么 / 验证方式（build/test/手工验证）；
- 里程碑提交同时打 tag：`m0-baseline`、`m1-baseline`…，便于回滚；
- 需求变更单独提交（`docs(reqs): ...`），与代码提交分离；
- `artifacts/`、`bin/obj`、`node_modules`、`ui/dist`、`.vs/` 不入库。
