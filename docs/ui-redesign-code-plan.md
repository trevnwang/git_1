# MBD Git Tool UI 重构（参考图工作台 + 文件夹视图）代码实施方案

> 文档性质：**代码差距 + Host API 契约 + Host/前端实现方案 + 风险/顺序 + 测试方案**；只定方案，不改代码。
> 视觉/交互唯一输入：`docs/images/reference/main-ui-reference.png` + **`docs/ui-redesign-spec.md`（spec-v3.0，S1 已产出，本文按其章节/数值对齐）**。
> 需求基线：`reqs/requirements.md` v0.2.1 §7.1–7.6、§9.6（FR-DETAIL）、§9.10（FR-OPS）、§11（信息架构/CI 锚点）、§12.1–12.2。
> 节奏基线：`docs/ui-redesign-plan.md` 的 S1–S7；本文把实现收敛为 4 个可执行阶段（§6.3），对齐 spec §11.3 的 S2/S3/S4/S6。
> 代码基准：2026-11-14 `D:\Workspace\git_1`（M1-a：本地扫描 + Gitea 只读清单 + WebView2 WebMessage 桥）。
>
> **命名对齐说明（重要）**：任务书建议的 `getComponentDetail` 与 spec-v3.0 的 `getRepoDetail/getCommits` 并存：前者做“组件级详情（四仓 + action 可用性 + CI 汇总）”，后者做“单仓详情 + 提交分页”。两者共享 Host 服务与 DTO，均写入本文契约（§0.6/§3）。错误码统一采用 spec 的 `E_*` 形式（Host 内部安全分类映射到 `E_*`，SRS 前缀放 `details.srsCode`）。

---

## 0. 结论摘要（评审 5 分钟版）

### 0.1 差距 Top5

| # | 差距 | 证据（当前代码） | 影响 |
|---|---|---|---|
| 1 | **Host 只有 2 个方法且无参数**：`getSnapshot/refresh`；spec 要求的 `getRepoTree/getWorkspaceTree/getRepoDetail/getCommits/openPath` 全无，`runRepoAction` 只有 M2 契约 | `src/MbdGitTool.Host/Bridge/WebViewBridge.cs:95-117`；`ui/src/api.ts:49` | 四仓树、右栏详情、文件夹视图、打开文件、底部按钮全部无数据源 |
| 2 | **契约缺 UI 所需字段**：无 `counts/freshness/capabilities`、无 `repoName/tag/compareRef/stashCount/calcAt/dataSource/isStale/errorCode/lastCommit/ci`、无树节点/详情/操作可用性 DTO | `src/MbdGitTool.Core/Contracts/SnapshotDto.cs:7-36` | spec §5 右栏、§4 树行 `gitStatus/ignored/submodule`、§6 矩阵无法落地 |
| 3 | **前端是 660 行单文件旧看板**：左 rail + 表格 + 旧右侧 340px；无菜单栏/标题工具栏/2×2 四树/BatchBar/InfoPanel 4 Tab/FolderView | `ui/src/App.vue` 全文件；`ui/src/styles/tokens.css:52-150` | 与 spec §2/§3/§4/§5/§6/§7 的布局、信息密度、交互全部不符 |
| 4 | **无路径安全与打开文件能力**：无 canonical/防穿越/reparse/`.git`/危险扩展名，无 `Process.Start` 封装 | 全仓无 `openPath` 相关实现 | FR-DETAIL-03、FR-SAFE-05 不可验收；直接实现极易越权 |
| 5 | **CI 10 锚点仍绑旧 DOM、E2E 资产未入库**：testid 在旧看板；CDP 脚本在 gitignored `artifacts/ui-review` | `ui/src/App.vue` testid；`docs/ui-review-m0.md` | 重写即丢 AC；没有可回归资产（spec §10.1/AC-UI-21） |

补充：批量入口（spec §3.5 四处落点）、文件夹视图（spec §7）、`gitStatus/ignored/submodule` 树字段、`getCommits` 分页、状态栏“最近操作”（spec §2.7）、Host 测试项目均缺失；`SnapshotDto` 仅有基础状态。

### 0.2 新增 Host API 方法清单（M1 只读）

| 来源 | 方法 | 说明 |
|---|---|---|
| 任务书要求 | `getComponentDetail` | 组件级：四仓 `RepoDto` + action 可用性 + CI 汇总（新增，补 spec 的组件粒度） |
| spec-v3.0 | `getRepoDetail` | 单仓：`{repo, component, commits:{total,items}}`（右栏“概览/最近提交”） |
| spec-v3.0 | `getCommits` | 提交分页 `{total, items}`（右栏“提交”Tab，虚拟滚动） |
| 任务书+spec | `getRepoTree` | 四仓文件树（`{componentId,repoType,path,depth}`；task 兼容 `repoId/repoPath` 别名） |
| 任务书+spec | `getWorkspaceTree` | 文件夹视图工作区树（根→组件组→仓→文件） |
| 任务书+spec | `openPath` | 白名单直开 / 危险扩展名确认 / 目录 resource reveal |
| 任务书+spec | `runRepoAction` | M1 返回 `E_NOT_IMPLEMENTED`；M2 冻结 `{jobId,accepted}` + `JOB_UPDATED` 事件 |
| spec（可选） | `getCiStatus` | v1 Mock（`ci.enabled=false`，全 `NONE`），供 CI-3/CI-4 空态 |
| 已有 | `getSnapshot` / `refresh` | 形状不变；`SnapshotDto` 追加可选 `Counts/Freshness/Capabilities` |
| 补充（可选） | `getHostInfo` | 版本/特性/限制；若采用 spec 的 `CapabilitiesDto` 则可省 |

### 0.3 文件树性能方案（一句话）

**一次只读一个目录 + 前端懒加载 + 固定行高虚拟滚动 + 30s（树）/5s（细节）缓存 + 单响应上限**：Host 用 `System.IO.Enumeration.FileSystemEnumerable` 单目录枚举（spec：单目录最多 **2000** 项、超限 `truncated:true`；单面板 DOM 行 <80；`getRepoTree` P95 ≤300ms/2000 项）；前端按 spec §4.5 扁平化可见节点，只渲染滚动窗口（行高 28 / overscan 6），展开按 `path` 请求并缓存 30s；4 个仓面板独立滚动/展开/缓存。

### 0.4 主要风险

路径逃逸/ADS/reparse/`.git`/危险扩展名（安全）、`openPath` 启动默认程序的 E2E 副作用、4000+ 组件全量快照经 WebMessage 的体积/首屏、4 树并发与快速切换的旧响应覆盖、卡片 tint 与 SRS 对比度门禁冲突、CI 锚点与旧 M1 行为（数据源/45s 超时/重试/Mock 回退）回归、spec 与任务书的方法命名差异。

### 0.5 建议阶段（4 个实现阶段）

**P1 契约 + 路径安全基座**（只动 Core/Host 骨架 + 测试，不动 UI）→ **P2 Host 数据层**（树/工作区树/详情/提交/打开/操作占位 + 缓存 + 路由；单测全绿）→ **P3 前端新工作台（Mock 驱动）**（组件拆分/布局/虚拟树/文件夹视图/浮层/CI 锚点；CDP 34 条）→ **P4 真实数据联调与加固**（接 Host、真实 Gitea/本地夹具、安全/性能/视觉评审、截图与文档收口）。

### 0.6 与 spec-v3.0 / 任务书的命名映射（实现前先确认）

| 任务书建议 | spec-v3.0 | 本方案落地 | 说明 |
|---|---|---|---|
| `getComponentDetail(componentId)` | 无同名（组件数据在快照，单仓用 `getRepoDetail`） | **新增** `getComponentDetail` | 组件级：4 仓 `RepoDto` + `actions[]` + `ci`；供左卡片选中后右栏“组件概览”与底部按钮可用性 |
| `getRepoDetail` | `getRepoDetail{componentId,repoType}` | **新增** | 单仓右栏 4 Tab 的“概览/最近提交” |
| `getCommits` | `getCommits{componentId,repoType,skip,take}` | **新增** | 提交 Tab 分页；M1 只读 `git log` |
| `getRepoTree(repoId\|repoPath, depth)` | `getRepoTree{componentId,repoType,path?,depth?}` | 以 spec 参数为主，**兼容** `repoId`/`repoPath` 别名；`path` 相对**仓根** | 响应采用 spec §4.4 的 `entries/TreeNode` 字段（`DIR/FILE`、`gitStatus/ignored/submodule`），**追加** `limit/cursor/nextCursor/readAt` 可选字段 |
| `getWorkspaceTree()` | `getWorkspaceTree{path?,depth?}` | 以 spec 为主 | 响应采用 spec §7.4 的 `COMPONENT/REPO/UNBOUND_GROUP/DIR/FILE` |
| `openPath(path)` | `openPath{componentId,repoType,path,mode,confirm?}` | 以 spec 为主（`componentId/repoType` 可选，用于仓内二次收口） | 响应 spec `{opened,mode,errorCode?,message?}` + 相对/绝对路径（诊断用） |
| `runRepoAction(repoId,action)` | `runRepoAction{componentId,repoType,action,args?}` | 以 spec 为主；M1 `E_NOT_IMPLEMENTED` | M2 返回 `{jobId,accepted}`；事件 `JOB_UPDATED` |
| — | 错误码 `E_*` | **采用** | Host 内部 PathPolicy 错误映射为 `E_PATH_INVALID`/`E_PATH_OUTSIDE_ROOT` 等，SRS 前缀放 `details.srsCode` |

> 该映射多出 `getComponentDetail`（纯读、复用 `getRepoDetail` 的组装逻辑），不破坏 spec；若 spec owner 要求严格收敛，可将其标记为 `features.componentDetail=false` 的可选方法，UI 退回“用 `getSnapshot.components + getRepoDetail`”组合。

---

## 1. 现状盘点与 spec/参考图解读

### 1.1 区域与尺寸（以 spec §1.3 / §2.2 / §2.4 为准）

| 区域 | 参考图比例 | 1440×900 实现 | 1280×800 实现 | 关键规格（spec） |
|---|---|---|---|---|
| 菜单栏 | 100% × 5.3% | 高 **30** | 30 | 6 个菜单；Alt 聚焦、方向键；无底纹、1px 下边框 |
| 标题区 | 100% × 8.4% | 高 **48** | 48 | 产品/版本/组织/计数/数据源/扫描 + 全局搜索 + `[工作台\|文件夹]` + 刷新/通知/任务/设置；一行不换行 |
| 主区横向 | 左 24.4% / 中 59.1% / 右 6.7% | 左 **281.6** / 中 **849** / 右 **253.4** | 左 **249.6** / 中 **749.8** / 右 **224.6** | `grid-template-columns: minmax(248px,20%) minmax(0,1fr) minmax(224px,18%)`；gap 12；padding 16/12 |
| 左栏 | 卡片列表 143px 宽 | 卡片 **76px** 定高三行 + 列表头 40 + **BatchBar 52** | 卡片 76 / BatchBar 52 | 卡片 L1 20px / L2 20px / L3 16px；虚拟滚动；批量条填参考图左栏下方空白 |
| 中区四树 | 2×2 各 167×112 | 每块 **418.5 × 347**（标题 32 + 树体 315） | 每块 **368.9 × 297** | 固定 4 面板（行优先 Design/Impl/MIL/SIL），缺仓保留空态；行高 **28**；无横向滚动 |
| 右侧信息 | 竖条 30px（几何示意） | **253.4** 宽 | **224.6** 宽 | CI 条 40 + 仓标题 36 + Tab 32 + 内容滚动；Tab：概览/提交/差异/CI |
| 底部按钮 | 树+信息列下方 387×20 | 行 2 **52**，`grid-column: 2 / -1` | 52 | 五键等宽 flex:1，min112/max220，gap12；**只作用于当前选中单仓** |
| 状态栏 | 100% × 6.9% | 高 **28** | 28 | 7 段：最近操作 + 新鲜度 + 进程健康 + 队列 + CI 队列 + 通知 + 网络 |
| 主区纵向 | 60.9% | 行 1 **706** + 行 2 **52**（Main 794，内容 770） | 行 1 **606** + 52（Main 694，内容 670） | 页面永不滚动；滚动只在列表/树/右栏内容区 |

> **参考图色块（#DBE8FB/#FEE6CE 等）是 drawio 示意图默认色，不是产品色**；实现一律 `tokens.css` 的 `--st-*` + 派生 `--tint-*`，禁止新增 hex（spec §1.3/§8.2）。

### 1.2 现有代码盘点

| 文件 | 行数 | 当前职责 | 复用/改造 |
|---|---:|---|---|
| `src/MbdGitTool.Core/Contracts/SnapshotDto.cs` | 36 | 快照（组件/四仓/状态/分支/head/ahead/behind/flags） | **追加可选字段**（spec 附录 B.1，零破坏） |
| `src/MbdGitTool.Core/Domain/RepoFacts.cs` | 138 | 扫描事实（HEAD/branch/upstream/ahead/behind/dirty/untracked/stash/unmerged/in-progress/shallow/LFS/busy/remote_gone/link） | 复用；新 DTO 字段来源 |
| `src/MbdGitTool.Core/Domain/ComponentAssembler.cs` | 359 | 事实→`ComponentDto/RepoDto` | 复用，`ToRepoDto` 填新增可选字段 |
| `src/MbdGitTool.Host/Services/LocalScanner.cs` | 672 | depth 1 扫描、git 只读、reparse 跳过、单仓失败隔离 | 复用；树服务不重扫 git（`gitStatus` 例外，见 §4.4） |
| `src/MbdGitTool.Host/Services/SnapshotService.cs` | 445 | 扫描 + Gitea → 快照；缓存/刷新互斥 | 加 `SnapshotIndex`（§4.2） |
| `src/MbdGitTool.Host/Services/GitCli.cs` | 143 | `Process`+`ArgumentList` 只读 git；20s 超时 | 复用于 `getCommits`/`gitStatus`；树枚举不走它 |
| `src/MbdGitTool.Host/Services/AppConfig.cs` | 122 | config + env；token 脱敏 | 复用；可选 `ui.*` 键（§4.4） |
| `src/MbdGitTool.Host/Services/HostLog.cs` | 111 | 滚动日志 + 凭据脱敏 | 复用；新 API 摘要日志 |
| `src/MbdGitTool.Host/Bridge/WebViewBridge.cs` | 167 | `getSnapshot/refresh`、app.local、camelCase | **改造成薄适配器 + `HostCommandRouter`** |
| `src/MbdGitTool.Host/MainWindow.xaml(.cs)` | 16/109 | WebView2 初始化/虚拟主机/预热 | 复用；加 `MinWidth=1024/MinHeight=720`（spec §2.6） |
| `ui/src/App.vue` | 660 | 单文件：标题/rail/看板/组件/CI/任务/通知/日志/设置 + Mock | **拆分重写**（§5.1） |
| `ui/src/api.ts` | 301 | 信封、2 方法、宽容 normalize | **重写 dispatcher**（§5.4），保留 `normalizeSnapshot` |
| `ui/src/styles/tokens.css` | 240 | §11.2 token + 旧 `.app` grid + 看板样式 | token 复用；新增 `--layout-*`、`--tint-*`（spec §9.4）；旧样式移 legacy |
| `tests/MbdGitTool.Core.Tests` | 4 类/71 测试 | 命名/状态/聚合 | 复用；新增 Host 测试项目（§7.1） |
| `reqs/v0.2/fixtures/state-matrix.ps1` | 1869 | 25 个离线 git 状态夹具（自校验） | 状态/矩阵/真实回归夹具（§7.4） |
| `artifacts/m1-fixture` / `artifacts/real-test/workspace` | — | 本地 2 组件/6 仓；RTCRepos 4 组件/14 仓 | E2E 真实环境（§7.4） |

### 1.3 旧功能保留清单（spec §11.1，禁止回退）

| 能力 | 旧位置 | 新落点 | 验收 |
|---|---|---|---|
| 数据源徽标（Mock/本地/本地+Gitea） | `App.vue:351` | TitleBar `snapshot-source` | Mock/真实各 1 图；`source=mock` 常驻不可隐藏 |
| 首屏 45s 超时 + 2 次重试 + Mock 回退 | `App.vue:80-299` | `composables/useHostSnapshot.ts` | 停 Host/删 config 仍渲染 Mock 且说明 |
| 刷新（re-scan） | `App.vue:264-289` | TitleBar/菜单刷新 + store `refreshAll` | 不闪白、保留选中、Toast 完成 |
| 工作区/快照时间/错误 | `App.vue:353-355` | TitleBar/StatusBar | 长路径省略 + title |
| 7 个功能入口（看板/组件/CI/任务/通知/日志/设置） | 左 rail | 菜单栏 + TitleBar 图标 + 状态栏段 + 浮层 | 全部可达（spec R-02） |
| CI 10 锚点 | 旧看板 testid | spec §8.1 新落点 | AC-UI-21 10/10 且各 1 个 |
| Toast | `App.vue:639-641` | `ToastHost`（堆叠/重试/错误码） | spec §6.5 |
| 深色主题 | tokens.css | 保留 | `prefers-color-scheme` 无白闪 |
| 无横向溢出 | 旧 `.app` | 新 AppShell | AC-UI-04/05 |

### 1.4 spec 已裁决的冲突（本文直接遵循，不再重复讨论）

spec §11.2 的 R-01～R-14 已裁决：默认工作台（R-01）、移除左导航（R-02）、`*`+`●N` 并存（R-03）、卡片 55% tint（R-04）、AHEAD=info/DIVERGED=purple/MISSING=neutral 等状态色按 SRS §11.2（R-05）、右栏 18%（R-06）、新增文件夹视图（R-07）、不设独立详情页（R-08）、五键严格单仓（R-09）、状态栏 7 段（R-10）、白名单安全保留（R-11）、CI-1 位移（R-12）、双击=打开/箭头=展开（R-13）、文件夹视图=候选 A（R-14）。
**本文只在 spec 未定处给实现裁决**：方法命名映射（§0.6）、`getComponentDetail` 增量、`getRepoTree` 的 `limit/cursor` 扩展、`gitStatus/ignored/submodule` 的 M1 实现深度、旧表格看板保留期限。

---

## 2. 代码差距清单（按文件/行为）

> P0 = 新 UI 不能开工/不能验收；P1 = 本次 UI 重构内完成；P2 = 可延后。阶段 P1–P4 见 §6.3。

### 2.1 Top5（同 §0.1，略）

### 2.2 详表

| ID | 优先级 | 文件/行为 | 现状 | 需要 | 阶段 |
|---|---|---|---|---|---|
| G-01 | P0 | `SnapshotDto.cs` | 无 counts/freshness/capabilities；`RepoDto` 无 repoName/tag/compareRef/stashCount/calcAt/dataSource/isStale/errorCode/lastCommit/ci | 按 spec 附录 B.1 **追加可选参数**（旧构造/旧 JSON 不受影响） | P1 |
| G-02 | P0 | `SnapshotDto.cs` 组件 | 无 `Incomplete/BindingCause/LastActivityAt/CiAggregate` | 追加可选参数（看板排序/卡片 CI/缺仓说明） | P1 |
| G-03 | P0 | `WebViewBridge.cs` | 只有 2 方法、无 `params`、错误为字符串 | `HostCommandRouter` + `params` + `E_*` 错误码 + 参数校验 | P1/P2 |
| G-04 | P0 | `WebViewBridge.cs` | 无每方法超时/取消/并发闸/响应大小限制 | 每请求 CTS、`SemaphoreSlim`、大小上限（spec 风险 1） | P2 |
| G-05 | P0 | Host 服务 | 无 `RepoTreeService/WorkspaceTreeService/RepoDetailService/CommitsService/OpenPathService/RepoActionService` | 按 §4 新建 | P2 |
| G-06 | P0 | Host 安全 | 无 canonical/防穿越/reparse/`.git`/危险扩展名/子模块 | `PathPolicy` + `OpenPathService` + 单测 | P1/P2 |
| G-07 | P0 | `SnapshotService.cs` | 无索引（按 componentId O(n)）、无 workspaceRoot 统一下发 | `SnapshotIndex`（FrozenDictionary） | P1 |
| G-08 | P1 | `AppConfig.cs` | 无树 limit/打开白名单/E2E 注入 | 可选 `ui.*`（安全默认值）+ 仅测试 env | P2 |
| G-09 | P0 | `tests/` | 只有 Core.Tests；Host FS/路径不可回归 | `tests/MbdGitTool.Host.Tests`（§7.1） | P1/P2 |
| G-10 | P0 | `App.vue` | 660 行单文件、旧 rail+表格+Mock | 拆为 spec §9.1 组件树 | P3 |
| G-11 | P0 | `api.ts` | 2 方法、无 params、无 code、无单 dispatcher | 重写（§5.4） | P3 |
| G-12 | P0 | `tokens.css` | 固定 `.app`（64px rail/48px 标题）；无 `--layout-*`/`--tint-*`/树行/面板 | 按 spec §9.4 追加；旧样式迁移/删除 | P3 |
| G-13 | P0 | 前端状态 | 无 store；选中/视图/树状态全在 App | spec §9.3 `stores/workbench.ts` + `viewmodel.ts` | P3 |
| G-14 | P0 | 四仓树 | 无树组件/懒加载/虚拟化/gitStatus 字母/空态 | `RepoGrid/RepoTreePanel/FileTree/VirtualList` | P3 |
| G-15 | P0 | 右栏 | 旧看板右侧 340px 详情，非 spec §5 四 Tab | `InfoPanel`（CI 条/仓标题/Tab/字段/提交） | P3 |
| G-16 | P0 | 底部操作栏/批量条 | 无 | `RepoActionsBar`（单仓五键）+ `BatchBar`（四处批量入口） | P3 |
| G-17 | P0 | 文件夹视图 | 不存在 | `FolderView/WorkspaceTree`（spec §7 候选 A） | P3 |
| G-18 | P0 | 打开文件/确认 | 无 | `openPath` + `ConfirmDialog`（白名单/危险扩展名/`.git`/reparse） | P3/P4 |
| G-19 | P0 | 菜单/标题/状态栏 | 旧 titlebar + rail，无菜单、无搜索/视图切换、状态栏 6 段 | `MenuBar/TitleBar/StatusBar`（spec §2.7/§6.5） | P3 |
| G-20 | P0 | CI 10 锚点 | 绑旧 DOM | spec §8.1 新落点；testid 不变；AC-UI-21 | P3 |
| G-21 | P1 | E2E 资产 | `artifacts/ui-review` gitignored | `ui/e2e/` 提交 + Mock bridge（spec §10.3） | P3/P4 |
| G-22 | P1 | Mock | 看板/通知/日志硬编码 | `ui/src/mocks/*`（组件/树/详情/七态/慢/失败/CI FAIL） | P3 |
| G-23 | P1 | 选中/刷新 | 刷新只重取快照；切换丢树状态 | 选中缓存/滚动/展开持久化（spec §4.1/§7.5） | P3/P4 |
| G-24 | P0 | 旧 M1 行为 | 数据源/45s/重试/Mock 回退/Toast | 迁到 store/composable（§2.3） | P3 |
| G-25 | P1 | 单仓操作 | 无按钮/矩阵/M2 契约 | `RepoActionsBar` + `getComponentDetail.actions` + `runRepoAction` | P2/P3 |
| G-26 | P1 | 提交/详情 | 无 `getRepoDetail/getCommits` | Host `CommitsService` + 右栏 Tab（spec §5.2） | P2/P3 |
| G-27 | P1 | 树字段 | 无 `gitStatus/ignored/submodule` | `FileTreeReader` 一次 `git status`/`.gitmodules`/`check-ignore` 映射（§4.4） | P2 |
| G-28 | P2 | 旧表格看板 | 旧看板是主界面 | `LegacyBoard` 作为“视图→表格视图”P2 备选（spec R-01） | P3/P4 |
| G-29 | P1 | 窗口尺寸/视图持久化 | 无 | `localStorage["ui.v3.view"]` + WPF `MinWidth/MinHeight`（spec §2.6） | P3 |
| G-30 | P1 | 可访问性 | 旧看板无 tree/listbox/menubar/focus trap | spec AC-UI-08/32 | P3 |

### 2.3 旧功能迁移清单

同 §1.3；补充代码级：`sourceLabel/sourceTone/sourceTitle`、`SNAPSHOT_TIMEOUT_MS=45_000`、`SNAPSHOT_RETRY_DELAYS_MS=[5000,15000]`、`normalizeSnapshot` 宽容解析、`HostLog` 脱敏规则全部保留。所有 Mock/回退逻辑从 `App.vue` 迁到 `mocks/` + `useHostSnapshot`，组件不直接读 Mock。

---

## 3. Host API 契约设计

### 3.1 信封、错误与兼容

继续 WebView2 同进程 WebMessage（不引入 HTTP 端口）；JSON camelCase（`JsonSerializerDefaults.Web`）。

**请求**

```json
{ "id": "uuid", "method": "getRepoTree", "params": { "componentId": "RTCT_Demo", "repoType": "IMPL", "path": "src", "depth": 1 } }
```

**成功**

```json
{ "id": "uuid", "ok": true, "data": { } }
```

**失败（兼容旧前端）**

```json
{
  "id": "uuid",
  "ok": false,
  "error": "路径越出工作区根",
  "code": "E_PATH_OUTSIDE_ROOT",
  "details": { "path": "../x", "srsCode": "FS_PATH_OUTSIDE_WORKSPACE" }
}
```

- `error` 保留人类可读字符串（旧 `api.ts` 读 `reply.error` 不破）；`code` 为 spec `E_*` 稳定错误码；`details` 可选（确认流程/诊断）。
- 请求体 ≤64KB；路径字符串 ≤1024；响应序列化 ≤2MB（spec 风险 1 的 WebMessage 体积上限；超过返回 `E_RESPONSE_TOO_LARGE` 并让 UI 降 `limit`/分页）。
- 允许 `params` 省略（`getSnapshot/refresh` 等）；未知字段忽略（向前兼容）。
- `id` 缺失/非字符串 → `E_INVALID_REQUEST`，但 `getSnapshot/refresh` 继续按旧行为处理（旧 UI 兼容）。

### 3.2 方法总表

| method | params | 返回 `data` | Host 超时 | 缓存/并发 | 阶段 |
|---|---|---|---|---:|---|
| `getSnapshot` | 无 | `SnapshotDto`（含可选 counts/freshness/capabilities） | 45s | SnapshotService 互斥 | 已有/扩展 |
| `refresh` | `{}` | `SnapshotDto` | 120s | 串行；完成后清树/详情缓存 | 已有/扩展 |
| `getComponentDetail` | `{componentId}` | `ComponentDetailDto` | 45s | 读快照索引 | P2（任务书增量） |
| `getRepoDetail` | `{componentId, repoType}` | `{repo, component, commits:{total,items}}` | 10s | 快照 + `git log -n5` | P2 |
| `getCommits` | `{componentId, repoType, skip, take}` | `{total, items}` | 15s | `git log` 分页；按仓+skip 缓存 10s | P2 |
| `getRepoTree` | `{componentId, repoType, path?, depth?, limit?, cursor?, includeIgnored?, includeGitStatus?}`（兼容 `repoId`/`repoPath`） | §3.6 | 10s | 目录页缓存 30s；IO 闸 4 | P2 |
| `getWorkspaceTree` | `{path?, depth?, includeMissingRepos?}` | §3.7 | 10s | 缓存 30s | P2 |
| `openPath` | `{componentId?, repoType?, path, mode?: "open"\|"reveal", confirm?}` | §3.8 | 8s | 无缓存；启动前全量校验 | P2 |
| `runRepoAction` | `{componentId, repoType, action, args?}` | M1：`ok:false,E_NOT_IMPLEMENTED`；M2：`{jobId,accepted}` | 5s | M1 无副作用 | P2 契约 |
| `getCiStatus` | `{componentId}` | `{latest:[], aggregate}`（Mock，全 NONE） | 2s | 启动期缓存 | P2 可选 |
| `getHostInfo` | 无 | `{appVersion, webView2Version, features, limits}` | 2s | 启动期一次 | 可选 |

**参数语义（重要）**

- `getRepoTree.path`：**相对仓根**（spec §4.4），`""` 或省略 = 仓根；`/` 分隔；Host 内部再叠加仓根得到绝对路径。
- 兼容别名：`repoId="{componentId}_{REPO_TYPE}"` 或 `repoPath="<workspace 相对>"`（任务书要求）；三者同时出现时优先级 `componentId+repoType` > `repoId` > `repoPath`。
- `depth`：`getRepoTree` 默认 1（0..2 夹紧）；`getWorkspaceTree` 默认 1（1..3）；响应 `depth` 语义：0=只要条目元数据；1=目录+直接子项；2=再展开一层。
- `limit`：默认 2000（spec 大目录上限），夹紧 1..2000；`cursor/nextCursor` 为**可选扩展**（spec 未要求“加载更多”，M1 UI 用 2000 截断警告；保留分页供后续大目录）。
- `includeGitStatus`：默认 true；关闭可省一次 `git status`。
- `includeIgnored`：默认 false；开启才做 `git check-ignore`（见 §4.4.4）。

### 3.3 参数 DTO（C# 记录）

```csharp
public sealed record BridgeRequest(string? Id, string? Method, JsonElement? Params);

public sealed record GetComponentDetailParams(string ComponentId);
public sealed record GetRepoDetailParams(string ComponentId, string RepoType);
public sealed record GetCommitsParams(string ComponentId, string RepoType, int Skip, int Take);

public sealed record GetRepoTreeParams(
    string? ComponentId, string? RepoType,
    string? RepoId, string? RepoPath,          // 任务书兼容别名
    string? Path, string? RelativePath,        // spec=path；relativePath 兼容
    int? Depth, int? Limit, string? Cursor,
    bool? IncludeGitStatus, bool? IncludeIgnored, bool? IncludeHidden);

public sealed record GetWorkspaceTreeParams(
    string? Path, int? Depth, int? Limit, string? Cursor,
    bool? IncludeHidden, bool? IncludeMissingRepos);

public sealed record OpenPathParams(string Path, string? ComponentId, string? RepoType,
    string? Mode, bool? Confirm);

public sealed record RunRepoActionParams(string ComponentId, string RepoType, string Action,
    IReadOnlyDictionary<string, JsonElement>? Args);
```

反序列化失败/必填缺失 → `E_INVALID_ARGUMENT`（`details.field`）。

### 3.4 `getComponentDetail`

组件级详情（任务书要求；供左卡片选中后右栏组件概览、四仓 action 可用性）。

**请求**

```json
{ "id": "1", "method": "getComponentDetail", "params": { "componentId": "RTCT_Demo" } }
```

**响应（节选）**

```json
{
  "id": "1", "ok": true,
  "data": {
    "componentId": "RTCT_Demo",
    "aggregateState": "BEHIND",
    "severityRank": 6,
    "branch": "main",
    "tag": null,
    "mixedBranch": false,
    "incomplete": false,
    "flags": [],
    "workspaceRoot": "D:\\Work\\MBD",
    "generatedAt": "2026-11-14T02:20:44.123Z",
    "ci": { "enabled": false, "state": "NONE", "provider": "mock", "fetchedAt": null },
    "repos": [
      {
        "repoType": "DESIGN",
        "repoName": "RTCT_Demo_Design",
        "state": "SYNCED",
        "subType": null,
        "branch": "main",
        "tag": null,
        "headSha": "3f9a1c2…",
        "upstreamRef": "refs/remotes/origin/main",
        "compareRef": "refs/remotes/origin/main",
        "ahead": 0, "behind": 0,
        "dirtyCount": 0, "untrackedCount": 0, "stashCount": 0,
        "isLocal": true, "localPath": "D:\\Work\\MBD\\RTCT_Demo_Design",
        "isStale": false, "calcAt": "2026-11-14T02:20:44Z", "dataSource": "local+gitea",
        "errorCode": null, "flags": [],
        "actions": [
          { "action": "PULL", "enabled": false, "matrixAllowed": true, "reasonCode": "M1_READ_ONLY", "reason": "M1 只读，M2 启用", "requiresConfirm": false, "stage": "M2" },
          { "action": "FETCH", "enabled": false, "matrixAllowed": true, "reasonCode": "M1_READ_ONLY", "reason": "M1 只读，M2 启用", "requiresConfirm": false, "stage": "M2" },
          { "action": "CHKOUT", "enabled": false, "matrixAllowed": true, "reasonCode": "M1_READ_ONLY", "reason": "M1 只读，M2 启用", "requiresConfirm": false, "stage": "M2" },
          { "action": "COMMIT", "enabled": false, "matrixAllowed": true, "reasonCode": "M1_READ_ONLY", "reason": "M1 只读，M2 启用", "requiresConfirm": false, "stage": "M2" },
          { "action": "PUSH", "enabled": false, "matrixAllowed": true, "reasonCode": "M1_READ_ONLY", "reason": "M1 只读，M2 启用", "requiresConfirm": false, "stage": "M2" }
        ]
      },
      { "repoType": "IMPL", "repoName": "RTCT_Demo_Impl", "state": "AHEAD", "branch": "main", "ahead": 1, "behind": 0, "isLocal": true, "localPath": "D:\\Work\\MBD\\RTCT_Demo_Impl", "flags": [], "actions": [] },
      { "repoType": "MIL", "repoName": "RTCT_Demo_MIL", "state": "BEHIND", "branch": "main", "ahead": 0, "behind": 1, "isLocal": true, "localPath": "D:\\Work\\MBD\\RTCT_Demo_MIL", "flags": [], "actions": [] },
      { "repoType": "SIL", "repoName": "RTCT_Demo_SIL", "state": "DIRTY", "branch": "main", "ahead": 0, "behind": 0, "dirtyCount": 1, "isLocal": true, "localPath": "D:\\Work\\MBD\\RTCT_Demo_SIL", "flags": ["dirty_tracked"], "actions": [] }
    ]
  }
}
```

**规则**

- 数据只读 `SnapshotService.Index`（不触发 git）；`Current==null` 时先 `await GetSnapshotAsync()`（可能 10–20s，故超时 45s）。
- 找不到 → `E_NOT_FOUND`（`details.componentId`）。
- `actions` 由 `RepoActionService.GetAvailability` 按 SRS §7.6 + spec §6.2 给 `matrixAllowed`；M1 `enabled=false, reasonCode=M1_READ_ONLY`；状态阻断时 `matrixAllowed=false` + 具体 `reason`（spec §6.2 文案表）。
- `ci` 占位（`enabled=false,state=NONE`），只用于 CI-3/CI-4 空态，不参与聚合/底色（CI-10）。

### 3.5 `getRepoDetail` / `getCommits`

**`getRepoDetail` 请求**

```json
{ "id": "2", "method": "getRepoDetail", "params": { "componentId": "RTCT_Demo", "repoType": "IMPL" } }
```

**响应**

```json
{
  "id": "2", "ok": true,
  "data": {
    "repo": { "repoType": "IMPL", "repoName": "RTCT_Demo_Impl", "state": "AHEAD", "branch": "main", "tag": null,
      "headSha": "7c2e9…", "compareRef": "refs/remotes/origin/main", "ahead": 1, "behind": 0,
      "dirtyCount": 0, "untrackedCount": 0, "stashCount": 0, "isLocal": true,
      "localPath": "D:\\Work\\MBD\\RTCT_Demo_Impl", "isStale": false, "calcAt": "2026-11-14T02:20:44Z",
      "dataSource": "local+gitea", "errorCode": null, "flags": [], "ci": null },
    "component": { "componentId": "RTCT_Demo", "aggregateState": "BEHIND", "severityRank": 6, "branch": "main", "tag": null, "mixedBranch": false, "flags": [], "repos": [] },
    "commits": {
      "total": 128,
      "items": [
        { "sha": "7c2e9…", "shortSha": "7c2e9c1", "message": "feat: …", "author": "Trevn", "at": "2026-11-13T09:12:00Z", "refs": ["HEAD -> main"] }
      ]
    }
  }
}
```

- `commits.items` 为最近 5 条（右栏概览）；`total` 用 `git rev-list --count HEAD`（只读，1 次）；`getCommits` 的 `skip/take` 分页（take 夹紧 1..200，spec 提交 Tab 100/页）。
- 仅当 `isLocal && isValid` 才调 git；MISSING/INVALID → `commits={total:0,items:[]}` + `errorCode`（不报错）；git 失败 → `E_GIT_FAILED`（`details` 含 stderr 首行，`HostLog.Redact`）。
- 超时 10s/15s；`GitCli` 固定 `--no-optional-locks`。

### 3.6 `getRepoTree`

**请求（初始）**

```json
{ "id": "3", "method": "getRepoTree",
  "params": { "componentId": "RTCT_Demo", "repoType": "IMPL", "path": "", "depth": 1 } }
```

**响应（spec §4.4 形状 + 本方案扩展字段）**

```json
{
  "id": "3", "ok": true,
  "data": {
    "componentId": "RTCT_Demo",
    "repoType": "IMPL",
    "repoName": "RTCT_Demo_Impl",
    "rootPath": "D:\\Work\\MBD\\RTCT_Demo_Impl",
    "path": "",
    "branch": "main",
    "headSha": "7c2e9c1…",
    "state": "AHEAD",
    "dirtyCount": 0,
    "compareRef": "refs/remotes/origin/main",
    "fetchedAt": "2026-11-14T02:20:44Z",
    "readAt": "2026-11-14T02:21:05.456Z",
    "entries": [
      { "path": "src", "name": "src", "kind": "DIR", "hasChildren": true,
        "size": null, "mtime": "2026-11-13T10:00:00Z", "gitStatus": "NONE",
        "submodule": false, "ignored": false, "children": null, "error": null },
      { "path": "README.md", "name": "README.md", "kind": "FILE", "hasChildren": false,
        "size": 1834, "mtime": "2026-11-13T10:01:02Z", "gitStatus": "MODIFIED",
        "submodule": false, "ignored": false, "children": null, "error": null },
      { "path": ".gitignore", "name": ".gitignore", "kind": "FILE", "hasChildren": false,
        "size": 120, "mtime": "2026-11-13T10:01:02Z", "gitStatus": "NONE",
        "submodule": false, "ignored": false, "children": null, "error": null }
    ],
    "truncated": false,
    "totalEntries": 128,
    "error": null,
    "limit": 2000, "nextCursor": null
  }
}
```

**展开请求**

```json
{ "id": "4", "method": "getRepoTree",
  "params": { "componentId": "RTCT_Demo", "repoType": "IMPL", "path": "src/control", "depth": 1 } }
```

`entries` 为 `path` 目录的直接子项（spec：`path` 相对仓根，根为空串）。`depth=2` 时子目录的 `children` 内联；UI 只用 `depth=1`。

**规则**

- `kind` 固定 `"DIR"|"FILE"`；`hasChildren` 对目录恒 true（未检查也给 true，UI 点击后空目录显示“（空）”），文件 false。
- `gitStatus`：`NONE/MODIFIED/ADDED/DELETED/UNTRACKED/CONFLICT`；由一次 `git status --porcelain=v2 -z --untracked-files=all` 的路径→状态映射得到（§4.4.3）；未纳入 git（目录/repo 外）→ `NONE`；`includeGitStatus=false` 时全部 `NONE`。
- `ignored`：默认 false；`includeIgnored=true` 时才做批探测；`.git`、`.trash` 永不出现。
- `submodule`：目录节点若在仓 `.gitmodules` 中登记 → true（读一次 `.gitmodules`）；打开/深层展开按 spec `E_SUBMODULE` 处理。
- 大目录：>2000 项 → `truncated=true`，`totalEntries` 为实际扫描数（受 20000 保护），UI 显示 `tree-truncated` 警告行“仅显示前 2000 项（打开文件夹查看全部）”（spec §4.5）。
- 空目录：`entries=[]`，UI 行内“（空）”；缺仓：`state=MISSING`/`isLocal=false` → `ok:true` + `entries=[]` + `error="E_REPO_NOT_LOCAL"`？**推荐**：`E_REPO_NOT_LOCAL` 作为 `ok:false` 错误，由 `RepoTreePanel` 的空态分支处理（spec §4.3 MISSING 空态）；树面板已从快照知道 state，不必发请求。实现上路由层直接返回 `ok:false,E_REPO_NOT_LOCAL` 更明确。
- 非仓/INVALID：`ok:true` 返回目录内容（只读），但 `state=INVALID`，UI 顶部告警 + 空态按钮“修复绑定”。
- 权限不足：该目录 `entries=[]` + `error="E_ACCESS_DENIED"`（节点级），不拖垮整树。
- 路径安全：`path` 只允许相对仓根；Host 解析后同时满足“在仓根内 + 在工作区根内”；`E_PATH_INVALID/E_PATH_OUTSIDE_ROOT/E_REPARSE_POINT`。

### 3.7 `getWorkspaceTree`

**初始请求**

```json
{ "id": "5", "method": "getWorkspaceTree", "params": { "path": "", "depth": 1 } }
```

**响应（spec §7.4 形状）**

```json
{
  "id": "5", "ok": true,
  "data": {
    "root": "D:\\Work\\MBD",
    "path": "",
    "generatedAt": "2026-11-14T02:20:44Z",
    "readAt": "2026-11-14T02:21:30.000Z",
    "entries": [
      {
        "kind": "COMPONENT",
        "componentId": "RTCT_Demo",
        "state": "BEHIND",
        "severityRank": 6,
        "dirtyCount": 3,
        "mixedBranch": false,
        "repos": [
          { "kind": "REPO", "repoType": "DESIGN", "name": "RTCT_Demo_Design", "path": "RTCT_Demo_Design", "state": "SYNCED", "branch": "main", "ahead": 0, "behind": 0, "dirtyCount": 0, "hasChildren": true },
          { "kind": "REPO", "repoType": "IMPL", "name": "RTCT_Demo_Impl", "path": "RTCT_Demo_Impl", "state": "AHEAD", "branch": "main", "ahead": 1, "behind": 0, "dirtyCount": 0, "hasChildren": true },
          { "kind": "REPO", "repoType": "MIL", "name": "RTCT_Demo_MIL", "path": "RTCT_Demo_MIL", "state": "BEHIND", "branch": "main", "ahead": 0, "behind": 1, "dirtyCount": 0, "hasChildren": true },
          { "kind": "REPO", "repoType": "SIL", "name": "RTCT_Demo_SIL", "path": "RTCT_Demo_SIL", "state": "DIRTY", "branch": "main", "ahead": 0, "behind": 0, "dirtyCount": 1, "hasChildren": true }
        ]
      },
      {
        "kind": "COMPONENT", "componentId": "RTCT_RemoteOnly", "state": "MISSING", "severityRank": 12, "dirtyCount": 0, "mixedBranch": false,
        "repos": [ { "kind": "REPO", "repoType": "DESIGN", "name": "RTCT_RemoteOnly_Design", "path": "RTCT_RemoteOnly_Design", "state": "MISSING", "isLocal": false, "hasChildren": false } ]
      },
      {
        "kind": "UNBOUND_GROUP", "name": "未纳管", "count": 2,
        "children": [ { "kind": "DIR", "name": "X_SIL_Design", "path": "X_SIL_Design", "hasChildren": true } ]
      }
    ],
    "truncated": false,
    "error": null
  }
}
```

**展开请求**

```json
{ "id": "6", "method": "getWorkspaceTree", "params": { "path": "RTCT_Demo_Impl/src", "depth": 1 } }
```

- 展开仓/目录返回与 `getRepoTree` 同构的 `DIR/FILE` 条目（`path/name/kind/hasChildren/size/mtime/gitStatus/submodule/ignored/children/error`）；
- 组件组为**虚拟节点**（Host 用 `RepoNameParser` 分组，UI 不解析目录名，spec R-14/§7.3）；展开组件组零请求（快照数据已内联），展开仓/目录才请求；
- 缺失仓也返回 REPO 节点（`isLocal=false`，`hasChildren=false`），保证四仓完整性可见；
- 未纳管组：根下不符合命名/非仓目录，`UNBOUND_GROUP` + `DIR` 子项；`groupByComponent` 不提供（spec 候选 A 固定分组）；
- 缓存 30s；根层不枚举 400 个仓库内容（只做快照映射 + 根目录一次枚举识别未纳管）。

### 3.8 `openPath`

**请求**

```json
{ "id": "7", "method": "openPath",
  "params": { "componentId": "RTCT_Demo", "repoType": "IMPL", "path": "tools/build.bat", "mode": "open" } }
```

**成功**

```json
{ "id": "7", "ok": true,
  "data": { "opened": true, "mode": "open", "path": "tools/build.bat",
            "absolutePath": "D:\\Work\\MBD\\RTCT_Demo_Impl\\tools\\build.bat",
            "extension": ".bat", "extensionClass": "DANGEROUS", "isReparsePoint": false } }
```

**危险扩展名未确认**

```json
{ "id": "8", "ok": false, "code": "E_CONFIRM_REQUIRED",
  "error": "文件类型 .bat 可能执行脚本，需确认后才能用默认程序打开",
  "details": { "path": "tools/build.bat", "extension": ".bat", "extensionClass": "DANGEROUS" } }
```

确认后重发 `"confirm": true`。

**模式与扩展名分级**

| mode | 行为 |
|---|---|
| `open` | 文件：默认程序；目录：资源管理器 |
| `reveal` | 资源管理器定位（`explorer.exe /select,"<path>"`；目录直接打开） |

| class | 扩展名 | 未确认 `open` |
|---|---|---|
| `DIRECTORY` | 目录 | 允许 |
| `SAFE_TEXT` | `.m .c .h .slx .mdl .json .xml .yaml .yml .txt .md .csv`（FR-DETAIL-03 白名单；可登记扩展如 `.hpp/.cpp/.log/.ini/.gitignore`） | 允许 |
| `DANGEROUS` | `.exe .bat .cmd .ps1 .vbs .js .jse .wsf .msi .lnk .scr .reg .url .com .pif .cpl .jar .sh` | `E_CONFIRM_REQUIRED` |
| `OTHER` | 其余（图片/模型/未知） | `E_CONFIRM_REQUIRED` |

- 目录双击 = `mode:"reveal"`（spec §4.2/AC-UI-11：打开资源管理器且**不翻转展开态**）；
- 文件双击：白名单 `mode:"open"`；危险/未知先确认（spec §4.2/AC-UI-10）；
- 失败统一 `E_*`，UI Toast + 状态栏“最近操作”记录（spec §4.6）。

### 3.9 `runRepoAction`（M1 契约冻结，M2 实现）

**M1 请求/响应**

```json
{ "id": "9", "method": "runRepoAction",
  "params": { "componentId": "RTCT_Demo", "repoType": "IMPL", "action": "PULL" } }
```

```json
{ "id": "9", "ok": false, "code": "E_NOT_IMPLEMENTED",
  "error": "M1 为只读模式：PULL 将在 M2 启用（需预检、dry-run/备份、逐仓结果与任务中心）",
  "details": { "action": "PULL", "repoType": "IMPL", "matrixAllowed": true, "writeStage": "M2" } }
```

**M2 成功（冻结）**

```json
{ "id": "10", "ok": true,
  "data": { "jobId": "e6f1…", "accepted": true, "repoType": "IMPL", "action": "PULL",
            "state": "QUEUED", "preflight": { "expectedHead": "7c2e9…", "isDirty": false, "requiresConfirm": false } } }
```

**M2 事件（预留）**：`JOB_UPDATED {jobId, repoType, phase, percent, message}` 通过 `PostWebMessageAsJson` 推送；前端 `addEventListener` 消费（spec 附录 B.2）。

**校验顺序**：action ∈ `PULL/FETCH/CHKOUT/COMMIT/PUSH`（M2 另含 SYNC/CREATE/...）→ 仓存在 → spec §6.2 矩阵（`E_ACTION_NOT_ALLOWED`，`details.reason` 具体文案）→ M1 `E_NOT_IMPLEMENTED` / M2 入队。

### 3.10 `getCiStatus`（Mock，可选）

```json
{ "id": "11", "ok": true,
  "data": { "enabled": false, "provider": "mock", "fetchedAt": null,
            "latest": [], "aggregate": { "componentId": "RTCT_Demo",
              "byState": { "PASS": 0, "FAIL": 0, "RUNNING": 0, "QUEUED": 0, "NONE": 4, "UNKNOWN": 0 },
              "worstState": "NONE", "dataFreshAt": "2026-11-14T02:20:44Z" } } }
```

`ci.enabled=false` 时 `E_CI_DISABLED` 或直接返回 NONE 空数据（推荐后者，UI 不额外分支）；CI 永不参与聚合/底色（CI-10）。

### 3.11 错误码表

| code | 语义 | 可重试 | UI 行为 | SRS 前缀（details.srsCode） |
|---|---|---|---|---|
| `E_INVALID_REQUEST` | 非 JSON/超大/缺 id 类型 | 否 | 错误 Toast | BRIDGE |
| `E_INVALID_ARGUMENT` | params 类型/范围非法 | 否 | Toast + 控制台 | BRIDGE |
| `E_UNKNOWN_METHOD` | 未知方法 | 否 | 版本不匹配提示 | BRIDGE |
| `E_TIMEOUT` | Host 侧超时 | 是 | 节点/面板重试，不清空已加载 | BRIDGE |
| `E_INTERNAL` | 未分类异常 | 是（一次） | 错误态 | BRIDGE |
| `E_RESPONSE_TOO_LARGE` | 响应 >2MB | 是（降 limit） | 自动降 limit 重试 | BRIDGE |
| `E_WORKSPACE_UNSET` | 未配置/工作区不可用 | 否 | 空态 + “打开设置” | CFG |
| `E_WORKSPACE_INVALID` | 根目录非法（盘符根等） | 否 | 空态 + 设置 | CFG |
| `E_NOT_FOUND` | 组件/仓不存在（快照过期） | 是（刷新） | 清选中/错误态 | STATE |
| `E_REPO_NOT_LOCAL` | 缺失仓（未克隆） | 否 | MISSING 空态 + Clone | STATE |
| `E_REPO_BUSY` | index.lock/外部 git | 是 | BUSY 提示（M2 写） | GIT |
| `E_PATH_INVALID` | 空/非法字符/保留设备名/ADS/超长 | 否 | Toast | FS |
| `E_PATH_OUTSIDE_ROOT` | canonical 后越出工作区/仓根 | 否 | Toast + WARN 日志 | SEC |
| `E_REPARSE_POINT` | reparse 遍历/展开被阻止 | 否 | 节点“链接”不可展开 | FS |
| `E_GIT_INTERNAL` | `.git` 内部路径被禁止 | 否 | Toast | SEC |
| `E_ACCESS_DENIED` | 权限不足 | 是 | 目录内联 warning | FS |
| `E_IO_ERROR` | 共享冲突/设备移除 | 是 | 内联错误 | FS |
| `E_TOO_MANY_ENTRIES` | 目录超过保护上限（20000） | 否 | 截断警告（spec §4.5） | FS |
| `E_CONFIRM_REQUIRED` | 危险扩展名/需确认 | 否（需确认） | 确认对话框后 `confirm:true` | SEC |
| `E_DANGEROUS_EXT` | 被策略禁止的扩展名（如 `.git` 内可执行） | 否 | Toast | SEC |
| `E_SUBMODULE` | 子模块内部禁止打开 | 否 | 提示“未纳管 submodule” | SEC |
| `E_LAUNCH_FAILED` | ShellExecute 失败 | 可重试一次 | Toast + 建议 reveal | SEC |
| `E_GIT_FAILED` | git 只读命令失败 | 是 | 错误态 + stderr 摘要 | GIT |
| `E_ACTION_NOT_ALLOWED` | 矩阵禁止（UNMERGED 等） | 否 | 禁用 + 原因 | OP |
| `E_NOT_IMPLEMENTED` | M1 只读 | 否 | “M1 只读，M2 启用” | OP |
| `E_CI_DISABLED` | CI 未接入 | 否 | 空态（一般不用，返回 NONE 数据） | CI |

错误码稳定、文案可改；`HostLog` 只记 code + 相对路径。

### 3.12 超时、并发、取消

| 环节 | 约定 |
|---|---|
| Host 每请求 | `CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token)` + `CancelAfter(methodTimeout)`；超时 `E_TIMEOUT`；桥销毁静默 |
| 方法超时 | snapshot 45s、refresh 120s、`getComponentDetail` 45s、`getRepoDetail` 10s、`getCommits` 15s、`getRepoTree` 10s、`getWorkspaceTree` 10s、`openPath` 8s、`runRepoAction` 5s |
| UI 每请求 | Host 超时 + 5s 余量；树/详情超时在节点/面板级重试，不清空已加载数据 |
| 并发闸 | `SemaphoreSlim(4)` 包 FS 枚举/详情 git；`getSnapshot/refresh` 走 SnapshotService 自身互斥 |
| 去重 | Host 不做 id 去重；UI `pending Map` + `loadingPaths` 防重复展开；`getRepoTree` 在途同 key 合并（可选） |
| 取消 | WebMessage 无客户端 cancel；UI epoch 丢弃过期响应；Host 桥 Dispose 取消全部 |
| UI 线程 | 解析/校验/回写在 UI 线程；枚举/排序/git/Process.Start 在 `Task.Run` |

### 3.13 安全边界（逐条实现 + 单测）

1. **来源**：只接受 `app.local`（保留 `IsAppLocalSource`）。
2. **路径**：UI 只传相对路径；绝对路径/UNC/盘符直接 `E_PATH_OUTSIDE_ROOT`；`E_PATH_INVALID` 检查 `\0`、`:`（ADS/盘符）、`< > " | ? *`、段首尾空格/点、保留设备名（`CON/PRN/AUX/NUL/COM1-9/LPT1-9`）。
3. **canonical**：`Path.GetFullPath(Path.Combine(root, relative))` 后必须 `== root` 或 `StartsWith(root + '\\', OrdinalIgnoreCase)`；`Path.GetRelativePath` 不得 `..`。
4. **仓根二次收口**：`getRepoTree/openPath` 解析出的绝对路径必须还在该仓 `localPath` 内（防 `path="../OtherRepo"`）。
5. **reparse**：枚举展示但不递归（`⟲` 标记）；`openPath` 允许只读打开（NFR-COMPAT-04 网络盘/OneDrive 只读），响应带 `isReparsePoint`；写操作（M2）拒绝。链接链逐级检查，reparse 祖先 → 遍历阻止。
6. **`.git`/`.trash`**：树隐藏；`openPath` 含 `.git` 段 → `E_GIT_INTERNAL`；`.trash` 仅恢复流程可见。
7. **大小/预算**：单目录 2000 项、单请求节点 ≤2000、`depth≤2`、响应 ≤2MB、**M1 不读文件内容**（无 `getFileContent`）。
8. **打开**：`Process.Start(new ProcessStartInfo { FileName = canonical, UseShellExecute = true })`；reveal 用 `explorer.exe` + `/select,"<canonical>"`；禁止字符串拼 `Arguments`（统一 `ArgumentList`/已验证 explorer 参数）；路径重申不含 `"`；失败 `E_LAUNCH_FAILED`。
9. **日志**：`HostLog.Info($"openPath mode=… path=<relative>")`；token/URL userinfo 脱敏（已有）；错误只回相对路径。
10. **测试注入**：仅 `MBD_E2E_OPEN_DRY_RUN=1` 时 `ShellProcessLauncher` 记录不启动（CDP E2E）；生产默认执行；单测用注入 fake，不依赖 env。
11. **M1 只读**：新增 API 不写文件/不调 git 写/不改配置；`runRepoAction` 恒 `E_NOT_IMPLEMENTED`。

### 3.14 `SnapshotDto` 增量（按 spec 附录 B.1，保持兼容）

```csharp
public sealed record SnapshotDto(
    string Source, string? WorkspaceRoot, string? Error, string GeneratedAt,
    IReadOnlyList<ComponentDto> Components,
    CountsDto? Counts = null, FreshnessDto? Freshness = null, CapabilitiesDto? Capabilities = null);

public sealed record ComponentDto(
    string ComponentId, string AggregateState, int SeverityRank, string? Branch, string? Tag,
    bool MixedBranch, IReadOnlyList<string> Flags, IReadOnlyList<RepoDto> Repos,
    bool? Incomplete = null, string? BindingCause = null, string? LastActivityAt = null,
    ComponentCiAggregateDto? CiAggregate = null);

public sealed record RepoDto(
    string RepoType, string State, string? SubType, string? Branch, string? HeadSha,
    int Ahead, int Behind, int DirtyCount, int UntrackedCount, bool IsLocal,
    string? LocalPath, IReadOnlyList<string> Flags,
    string? RepoName = null, string? Tag = null, string? UpstreamRef = null, string? CompareRef = null,
    int StashCount = 0, string? CalcAt = null, string? DataSource = null, bool? IsStale = null,
    string? ErrorCode = null, LastCommitDto? LastCommit = null, CiLatestDto? Ci = null);

public sealed record CountsDto(int Components, int Repos, int MissingRepos);
public sealed record FreshnessDto(string? LocalScanAt, string? RemoteFetchAt, bool LocalStale, bool RemoteStale);
public sealed record CapabilitiesDto(bool FileTree, bool WorkspaceTree, bool OpenPath, bool RepoActions, bool Ci);
public sealed record LastCommitDto(string Sha, string ShortSha, string Message, string Author, string At, IReadOnlyList<string> Refs);
```

- 追加**可选参数**：旧构造调用（`ComponentAssembler` 两处）源码兼容；旧 JSON 反序列化兼容；旧前端忽略新字段（spec 已确认）。
- `ComponentAssembler.ToRepoDto` 用 `RepoFacts` 填充 `RepoName/Tag/UpstreamRef/CompareRef/StashCount/CalcAt/DataSource/IsStale/ErrorCode`；`LastCommit/Ci` 由 Host 服务后补（P2，`getRepoDetail` 内）。
- 不做本方案早期设想的 `RepoId/HeadKind/IsValidRepo/ScanError` 位置字段：`state=INVALID` 可推导、`ErrorCode` 替代 `ScanError`、`repoId` 由 `getRepoTree` 的 `componentId+repoType` 表达；避免与 spec 冻结字段冲突。
- 新增 `Contracts/WorkbenchDto.cs`（`ComponentDetailDto/RepoActionAvailabilityDto/CiHintDto`）、`TreeDto.cs`（`RepoTreeDto/WorkspaceTreeDto/TreeNodeDto/OpenPathResultDto`）、`BridgeErrors.cs`；`SnapshotDto` 只按上面追加。

### 3.15 契约冻结与 golden

- 把 §3.4–§3.9 示例存 `tests/contracts/bridge/*.json`（时间/SHA 用占位符），Host/前端各跑“键名集合”比对，防 camelCase/字段漂移；
- 旧 `getSnapshot/refresh` 形状不变；新方法不破坏旧 CDP；
- 变更流程：字段新增允许（同 PR 更新 golden + 本文 §3）；删除/改名禁止。

---

## 4. Host 实现方案

### 4.1 新增/修改文件

```text
src/MbdGitTool.Core/
├─ Contracts/SnapshotDto.cs            # 修改：按 spec B.1 追加可选参数
├─ Contracts/WorkbenchDto.cs           # 新增：ComponentDetailDto/RepoDetailDto/RepoActionAvailabilityDto/CiHintDto/HostInfoDto
├─ Contracts/TreeDto.cs                # 新增：TreeNodeDto/RepoTreeDto/WorkspaceTreeDto/WorkspaceNodeDto/OpenPathResultDto
├─ Contracts/BridgeErrors.cs           # 新增：BridgeErrorCodes(E_*) + BridgeError
└─ Domain/ComponentAssembler.cs        # 修改：ToRepoDto 填新增可选字段

src/MbdGitTool.Host/
├─ Bridge/WebViewBridge.cs             # 修改：薄适配器（来源校验/解析/回写/超时）
├─ Bridge/HostCommandRouter.cs         # 新增：路由/参数/错误映射/并发闸（可单测）
├─ Bridge/BridgeParams.cs              # 新增：参数记录
├─ Services/SnapshotIndex.cs           # 新增：FrozenDictionary 查询
├─ Services/SnapshotService.cs         # 修改：暴露 Index；refresh 后清缓存钩子
├─ Services/PathPolicy.cs              # 新增：canonical/防穿越/黑名单/reparse
├─ Services/PathUtil.cs                # 新增：长路径 \\?\、相对路径规范
├─ Services/RepoTreeService.cs         # 新增：getRepoTree（FileTreeReader + GitStatusIndex + 缓存）
├─ Services/FileTreeReader.cs          # 新增：单目录枚举/排序/2000 截断/节点映射
├─ Services/GitStatusIndex.cs          # 新增：一次 git status 路径→状态映射（缓存 30s）
├─ Services/WorkspaceTreeService.cs    # 新增：getWorkspaceTree（组件虚拟组/未纳管组/懒加载）
├─ Services/RepoDetailService.cs       # 新增：getRepoDetail（repo/component/commits 前 5）
├─ CommitsService.cs                   # 新增：getCommits（git log 分页）
├─ Services/OpenPathService.cs         # 新增：扩展名分级/IProcessLauncher/ShellExecute
├─ Services/ShellProcessLauncher.cs    # 新增：IProcessLauncher 实现（E2E dry-run）
├─ Services/RepoActionService.cs       # 新增：§6.2 矩阵 + M1 E_NOT_IMPLEMENTED
├─ Services/ComponentDetailService.cs  # 新增：getComponentDetail
├─ Services/TreeCache.cs               # 新增：TTL 30s + 容量上限
├─ Services/HostInfoProvider.cs        # 新增：可选
└─ Services/AppConfig.cs               # 可选：ui.* 限制/白名单

tests/MbdGitTool.Host.Tests/           # 新增（§7.1）
tests/contracts/bridge/*.json          # 新增
```

### 4.2 `SnapshotIndex` 与 `SnapshotService`

```csharp
public sealed class SnapshotIndex
{
    public static SnapshotIndex Empty { get; }
    public SnapshotDto? Snapshot { get; }
    public string? WorkspaceRoot { get; }
    public string? GeneratedAt { get; }
    public IReadOnlyList<ComponentDto> Components { get; }

    public static SnapshotIndex Build(SnapshotDto snapshot);
    public bool TryGetComponent(string componentId, out ComponentDto component);
    public bool TryGetRepo(string componentId, string repoType, out RepoRef repo);
    public bool TryGetRepoByPath(string canonicalLocalPath, out RepoRef repo);
}

public sealed record RepoRef(string ComponentId, ComponentDto Component, RepoDto Repo);
```

- `SnapshotService.BuildAndStore` 同步写 `_current` 与 `_index`（`Volatile.Write`）；
- `ComponentAssembler` 保持不变，只在 `ToRepoDto` 填新字段；
- 组件查找 O(1)；仓按 `componentId|repoType` 和 `LocalPath`（`Path.GetFullPath` + `TrimEndingDirectorySeparator`，OrdinalIgnoreCase）双索引；
- `refresh` 后由 Router 调 `TreeCache.InvalidateAll()` 与 `GitStatusIndex.InvalidateAll()`。

### 4.3 `PathPolicy`（先写 + 先测）

```csharp
public sealed class PathPolicy
{
    public PathPolicy(string workspaceRoot);           // 规范化；非法启动期报错
    public string Root { get; }
    public bool TryResolveWorkspacePath(string? relative, out string full, out BridgeError? error);
    public bool TryResolveRepoPath(string repoRoot, string? relativeToRepo, out string full, out BridgeError? error);
    public bool IsWithin(string parentCanonical, string candidateCanonical);
    public ReparseCheck CheckReparseChain(string target);
    public string ToWorkspaceRelative(string fullPath);   // / 分隔
    public string ToRepoRelative(string repoRoot, string fullPath);
}
```

`TryResolveWorkspacePath` 伪代码（`TryResolveRepoPath` 相同逻辑，parent=repoRoot 且额外保证在 workspace root 内）：

```csharp
if (string.IsNullOrEmpty(relative)) { full = parent; return true; }
if (relative.Length > 1024) return Fail(E_PATH_INVALID);
if (relative.Contains('\0')) return Fail(E_PATH_INVALID);
if (Path.IsPathFullyQualified(relative) || relative.StartsWith(@"\\") || relative.StartsWith("//"))
    return Fail(E_PATH_OUTSIDE_ROOT);
foreach (var seg in relative.Split('\\', '/'))
{
    if (seg is "" or "." or "..") return Fail(E_PATH_INVALID);
    if (seg.Contains(':')) return Fail(E_PATH_INVALID);
    if (seg.IndexOfAny(new[] { '<','>','"','|','?','*' }) >= 0) return Fail(E_PATH_INVALID);
    if (IsReservedDeviceName(seg)) return Fail(E_PATH_INVALID);
    if (seg != seg.Trim() || seg.EndsWith('.')) return Fail(E_PATH_INVALID);
}
var combined = Path.GetFullPath(Path.Combine(parent, relative));
if (!IsWithin(parent, combined) && !PathEquals(parent, combined)) return Fail(E_PATH_OUTSIDE_ROOT);
if (GetSegments(combined).Any(s => s.Equals(".git", OrdinalIgnoreCase))) return Fail(E_GIT_INTERNAL);
full = combined; return true;
```

- `IsWithin` 用 `Path.GetRelativePath`，非 `..` 开头且非 rooted；
- reparse 链逐级 `File.GetAttributes`，记录第一处；树枚举不递归、openPath 标注；
- 网络盘/UNC 根允许只读，`getHostInfo.features` 标注，M2 写拒绝。

### 4.4 `RepoTreeService` + `FileTreeReader` + `GitStatusIndex`

#### 4.4.1 类

```csharp
public sealed class RepoTreeService
{
    public RepoTreeService(PathPolicy paths, SnapshotService snapshots, FileTreeReader reader,
                           GitStatusIndex gitStatus, TreeCache cache);
    public CommandResult GetTree(GetRepoTreeParams p, CancellationToken ct);
}

public sealed class FileTreeReader
{
    public DirectoryPage Read(string absoluteDir, string repoRoot, string repoRelativeDir,
                              TreeReadOptions options, CancellationToken ct);
}
public sealed record TreeReadOptions(int Depth = 1, int Limit = 2000, bool IncludeGitStatus = true,
                                     bool IncludeIgnored = false, bool IncludeHidden = false);
public sealed record DirectoryPage(IReadOnlyList<TreeNodeDto> Entries, bool Truncated, int TotalEntries,
                                   IReadOnlyList<string> Warnings);
```

#### 4.4.2 单目录枚举

```csharp
var entries = new List<RawEntry>(Math.Min(limit + 1, 2048));
var opts = new EnumerationOptions {
    RecurseSubdirectories = false,
    IgnoreInaccessible = true,
    AttributesToSkip = 0,
    ReturnSpecialDirectories = false,
    MatchType = MatchType.Simple
};
var enumerable = new FileSystemEnumerable<RawEntry>(
    PathUtil.EnsureExtendedPath(absDir),
    (ref FileSystemEntry e) => RawEntry.From(ref e, absDir, repoRoot),
    opts)
{
    ShouldIncludePredicate = (ref FileSystemEntry e) => ShouldInclude(ref e, options)
};
foreach (var entry in enumerable)
{
    ct.ThrowIfCancellationRequested();
    entries.Add(entry);
    if (entries.Count >= MaxScanEntries) break;     // 20000 保护
}
entries.Sort(RawEntryComparer.Instance);            // DIR first → name OrdinalIgnoreCase → Ordinal
var page = entries.Take(options.Limit).ToList();
var truncated = entries.Count > options.Limit;
```

- 一次枚举拿 `FileName/Length/LastWriteTimeUtc/Attributes/IsDirectory`（不逐项 `FileInfo`）；
- `ShouldInclude`：排除 `.git`（任意层，spec §4.6）、`.trash`、`..`；`includeHidden=false` 时排除 Hidden/System/`.` 开头；`includeHidden=true` 仍排除 `.git/.trash`；
- 目录先、文件后；名称忽略大小写 + Original 次序稳定；
- `hasChildren`：目录 true（未检查也给 true），文件 false；`children=null`；
- `kind`：`"DIR"|"FILE"`；
- 大目录 >2000：`truncated=true`、`totalEntries=entries.Count`（若触 20000 保护则 `E_TOO_MANY_ENTRIES` warning）；
- 空目录：`entries=[]`（UI 行内“（空）”）；权限不足：`Inaccessible` 由 `IgnoreInaccessible` 跳过，单节点 `error` 可选；
- 长路径：`PathUtil.EnsureExtendedPath`（>260 加 `\\?\`），输出 `path` 用未加前缀的仓相对路径；
- reparse：`Attributes & ReparsePoint` → `ignored?` 不递归，`hasChildren` 仍 true 但 UI 点击展开时返回 `E_REPARSE_POINT` 或空（推荐空 + 标记）。

#### 4.4.3 `gitStatus`（M1 实现，性能可控）

- 每个选中仓一次 `git --no-optional-locks -C <repo> status --porcelain=v2 -z --untracked-files=all`（GitCli，10s 超时），解析：
  - `1 <XY> <sub> <mH> <mI> <mW> <hH> <hI> <path>` → `MODIFIED`（X/Y 含 `A`→`ADDED`、`D`→`DELETED`、`U`→`CONFLICT`）；
  - `2 <XY> … <path>\0<origPath>` → rename → `MODIFIED`；
  - `u <XY> … <path>` → `CONFLICT`；
  - `? <path>` → `UNTRACKED`；
  - `! <path>` → `ignored`（仅当 `--ignored` 时；默认不传）。
- 结果 `FrozenDictionary<string,string>`（仓相对 `/` 路径），`GitStatusIndex` 按 `repoPath` 缓存 30s；`getRepoTree` 只查表，不逐目录起 git；
- 目录节点 `gitStatus="NONE"`；文件节点查表未命中 → `NONE`；
- 4 仓首次加载各 1 次 git（并发 4，`GitCli` 20s 超时足够）；大仓 10 万文件 `status` 可能 1–3s → 首次树请求超时 10s 足够；若超时，返回 `gitStatus=NONE` + warning，不阻塞树；
- `includeGitStatus=false` 时跳过，全部 `NONE`（M1 默认 true，spec 期望字母）。

#### 4.4.4 `ignored` 与 `submodule`

- `ignored`：默认 false；`includeIgnored=true` 时对当前目录做 `git check-ignore --stdin -z`（一次进程，2s 超时），失败全部 false；结果按 `(repo, dir, 5s)` 缓存；`includeHidden=true` 才显示被忽略项（spec “显示已忽略文件”）。
- `submodule`：读仓根 `.gitmodules`（一次，缓存 30s），`git config -f .gitmodules --get-regexp path` 或解析 `[submodule "..."] path = ...`；命中目录 → true；打开/展开内部 → `E_SUBMODULE`（spec B.2）；无 `.gitmodules` → false。

#### 4.4.5 节点 DTO

```csharp
TreeNodeDto(
  Path: repoRelativePathSlash,   // 相对仓根；根请求 path=""
  Name: fileName,
  Kind: "DIR"|"FILE",
  HasChildren: bool,
  Size: long?,
  Mtime: DateTimeOffset?,
  GitStatus: "NONE"|"MODIFIED"|"ADDED"|"DELETED"|"UNTRACKED"|"CONFLICT",
  Submodule: bool,
  Ignored: bool,
  Children: null,                // depth=1 时子目录 null；depth=2 内联
  Error: null);
```

### 4.5 `WorkspaceTreeService`

```csharp
public sealed class WorkspaceTreeService
{
    public WorkspaceTreeService(SnapshotService snapshots, PathPolicy paths,
                                FileTreeReader reader, TreeCache cache);
    public CommandResult GetTree(GetWorkspaceTreeParams p, CancellationToken ct);
}
```

**初始（`path=""`）**

1. 从 `SnapshotIndex` 按 `componentId` 分组，`RepoType` 顺序 DESIGN/IMPL/MIL/SIL；
2. `COMPONENT` 节点：`componentId/state/severityRank/dirtyCount(Σ)/mixedBranch/repos[]`；`repos[]` 为 `REPO` 节点（含 MISSING，`isLocal=false`、`hasChildren=false`、`path`=期望目录名）；
3. 根目录一次枚举（`FileTreeReader` limit 2000）识别：
   - 命中快照 repo.localPath 的目录 → 已包含在组件组，不重复；
   - 不符合命名/非仓目录 → 汇总 `UNBOUND_GROUP{name:"未纳管",count,children:[DIR...]}`；
   - 根下文件 → `FILE` 节点；
4. 安全：所有 `path` 由 `PathPolicy` 校验；reparse 不递归；节点数 ≤2000 截断；**不枚举 400 个仓库内部**；
5. 缓存 30s（key 含 `generatedAt`）。

**懒加载（`path!=""`）**

- `PathPolicy.TryResolveWorkspacePath(path)` → 若等于某仓根 → 校验后在仓内用 `FileTreeReader`；否则按普通目录；
- 返回 `DIR/FILE` 条目（同 `getRepoTree` 节点）；`E_REPARSE_POINT`/`E_ACCESS_DENIED` 节点级；
- 展开组件组不发请求（UI 用内联 `repos`）；展开仓/目录才请求（spec AC-UI-14）。

### 4.6 `RepoDetailService` / `CommitsService`

- `getRepoDetail`：`SnapshotIndex` 取 `component/repo` + `GitCli` 跑一次 `git log -n 5 --format=...` + `git rev-list --count HEAD`（可合并：`git log -n 6` 第 6 条判断是否有更多，减少一次进程；total 用 `rev-list --count` 更准）；
- `getCommits`：`git log --skip=<skip> --max-count=<take> --format=%H%x1f%h%x1f%s%x1f%an%x1f%aI%x1f%D`；`total` 用 `git rev-list --count HEAD`（同一缓存 10s）；
- 只读、`GIT_TERMINAL_PROMPT=0`、`GIT_OPTIONAL_LOCKS=0`、超时 10/15s、失败 `E_GIT_FAILED`（stderr 首行脱敏）；
- `isLocal=false`/`INVALID` → `commits={total:0,items:[]}` + `errorCode`，不调 git；
- `take` 夹紧 1..200（spec 100/页）。

### 4.7 `OpenPathService`

```csharp
public interface IProcessLauncher { bool TryStart(ProcessStartInfo startInfo, out string? error); }
public sealed class OpenPathService
{
    public OpenPathService(PathPolicy paths, SnapshotService snapshots, IProcessLauncher? launcher = null);
    public CommandResult Open(OpenPathParams p, CancellationToken ct);
}
```

流程：

1. 若带 `componentId/repoType`：从索引取仓根；`PathPolicy.TryResolveRepoPath(repoRoot, p.Path)`；否则 `TryResolveWorkspacePath`；
2. 存在性检查；`File.GetAttributes` 取 `Directory/ReparsePoint`；`.git` 段 → `E_GIT_INTERNAL`；submodule → `E_SUBMODULE`；
3. 扩展名分级（§3.8）；未确认的危险/未知 → `E_CONFIRM_REQUIRED`；目录直接允许；
4. `mode=open`：`FileName=full, UseShellExecute=true`；`mode=reveal`：`explorer.exe` + `"/select,\"<full>\""`（断言无 `"`）；
5. `IProcessLauncher`（`MBD_E2E_OPEN_DRY_RUN=1` 记录不启动）；失败 `E_LAUNCH_FAILED`；
6. 写日志（相对路径），返回 `{opened, mode, path, absolutePath, extension, extensionClass, isReparsePoint}`。

### 4.8 `RepoActionService`

```csharp
public sealed class RepoActionService
{
    public RepoActionAvailabilityDto GetAvailability(RepoRef repo);
    public CommandResult Run(RunRepoActionParams p, SnapshotIndex index);
}
```

- 矩阵按 SRS §7.6 + spec §6.2 表（13 状态 × 5 键）实现为静态表；`enabled` M1 恒 false；`matrixAllowed` 真实；`reason` 用 spec §6.2 的禁用原因文案；
- M1 `Run` 校验后可返回 `E_NOT_IMPLEMENTED`（`details.matrixAllowed`）；非本仓/未知 action → `E_ACTION_NOT_ALLOWED`/`E_INVALID_ARGUMENT`；
- 不写文件、不调 git 写；M2 替换为 job 入队 + `JOB_UPDATED`。

### 4.9 `ComponentDetailService`

- 输入 `componentId` + `SnapshotIndex`；组装 `ComponentDetailDto`（四仓顺序 DESIGN/IMPL/MIL/SIL、actions、CI hint、workspaceRoot/generatedAt）；
- 复用 `RepoActionService.GetAvailability`；`getRepoDetail` 复用同一 `RepoDto` 映射；
- 找不到 → `E_NOT_FOUND`。

### 4.10 `HostCommandRouter` + `WebViewBridge`

**Router**

```csharp
public sealed class HostCommandRouter : IDisposable
{
    public async Task<CommandResult> DispatchAsync(string? method, JsonElement? parameters, CancellationToken ct);
    public void InvalidateCaches(); // refresh 后
}
public sealed record CommandResult(bool Ok, object? Data, BridgeError? Error);
public sealed record BridgeError(string Code, string Message, object? Details = null);
```

```csharp
using var linked = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, ct);
linked.CancelAfter(TimeoutPolicy(method));
try {
  switch (method) {
    case "getSnapshot": return Ok(await _snapshots.GetSnapshotAsync(linked.Token));
    case "refresh": { var s = await _snapshots.RefreshAsync(linked.Token); InvalidateCaches(); return Ok(s); }
    case "getComponentDetail": return await RunFs(() => _componentDetail.Build(Deserialize<GetComponentDetailParams>(parameters)));
    case "getRepoDetail": return await RunFs(() => _repoDetail.Build(Deserialize<GetRepoDetailParams>(parameters), linked.Token));
    case "getCommits": return await RunFs(() => _commits.Get(Deserialize<GetCommitsParams>(parameters), linked.Token));
    case "getRepoTree": return await RunFs(() => _repoTree.GetTree(Deserialize<GetRepoTreeParams>(parameters), linked.Token));
    case "getWorkspaceTree": return await RunFs(() => _workspace.GetTree(Deserialize<GetWorkspaceTreeParams>(parameters), linked.Token));
    case "openPath": return await RunFs(() => _open.Open(Deserialize<OpenPathParams>(parameters), linked.Token));
    case "runRepoAction": return _actions.Run(Deserialize<RunRepoActionParams>(parameters), _snapshots.Index);
    case "getCiStatus": return Ok(_ciMock.Get(Deserialize<...>));
    case "getHostInfo": return Ok(_hostInfo.Get());
    default: return Fail(E_UNKNOWN_METHOD, ...);
  }
}
catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { return CancelledSilently(); }
catch (OperationCanceledException) { return Fail(E_TIMEOUT, ...); }
catch (CommandException ex) { return Fail(ex.Error); }
catch (Exception ex) { HostLog.Error(...); return Fail(E_INTERNAL, ...); }
```

**WebViewBridge 改造要点**

- 保留 `IsAppLocalSource`、`JsonDocument` 解析、`id` 回显、`PostWebMessageAsJson`；
- 解析 `params` 后交 Router；错误信封加 `code/details`（`error` 字符串保留）；
- 请求体上限 64KB；响应 >2MB → `E_RESPONSE_TOO_LARGE`；
- 并发 `_ = HandleMessageAsync(e)` 不阻塞 UI；`Dispose` 取消 `_lifetime`；
- 回写必须 UI 线程（`ConfigureAwait(true)`）。

### 4.11 缓存

| 缓存 | 内容 | TTL | 容量 | 失效 |
|---|---|---|---|---|
| `TreeCache`（repo） | 目录页节点 + truncated/total | 30s（spec §4.4） | 512 页 | `refresh` 全清；key 含 `generatedAt` |
| `TreeCache`（workspace） | 根层组件组 + 已展开目录页 | 30s | 256 | 同上 |
| `GitStatusIndex` | 仓 → path→status | 30s | 16 仓 | 刷新/切仓可保留 |
| `GitIgnore` | `(repo,dir)` ignored 集合 | 5s | 256 | 同上 |
| `CommitsCache` | 仓+skip+take | 10s | 64 | 刷新后清 |
| `RepoDetail`（前 5 提交） | 仓 | 10s | 64 | 同上 |

只缓存 FS/git 结果；仓库状态每次从 `SnapshotIndex` 合并（避免树新、状态旧）。

### 4.12 线程与桥异步

同 §3.12：UI 线程解析/回写；`Task.Run` 枚举/git/Process.Start；`SemaphoreSlim(4)`；取消 token 每项检查；`HostLog` 摘要（method/ms/entries/truncated）。

### 4.13 性能预算（spec §4.5/§12 + 本方案）

| 场景 | 目标 |
|---|---|
| `getRepoTree` 2000 项（本地 SSD，热缓存） | P95 ≤300ms（spec） |
| 4 仓首次加载并发 | 总耗时 ≤1.5s（含 4 次 git status，大仓 ≤3s + warning） |
| `getWorkspaceTree` 根层（400 仓/100 组件） | ≤300ms（不枚举仓内容） |
| 大目录 5000/10000 项 | 截断 2000 + 虚拟滚动；首帧 ≤1.5s；滚动 ≥55fps（spec） |
| 单响应 | 常规 ≤300KB；硬上限 2MB |
| 4000+ 组件快照 | 1.5–3MB（spec 风险 1）；实测 >200ms 或 >5MB 时启用 `getComponentPage` 分页（契约预留，不改 UI） |
| 前端 DOM | 单树 <80 行；页面元素 ≤15,000（SRS NFR-PERF-03） |

测量：`HostLog.Info("api ... ms=... entries=... truncated=...")`；CDP `Performance`/帧计时 + AC-UI-28。

### 4.14 日志与审计

- 所有新 API 摘要日志；`openPath` 记录 mode/class/相对路径；
- 错误 code + 相对路径；不打印绝对路径/token/文件内容；
- dry-run 日志明确标记，E2E 断言用。

---

## 5. 前端架构（按 spec §9）

### 5.1 组件树与文件落点（spec §9.1 为准）

```text
ui/src/
├─ App.vue                              # AppShell 根（≤120 行）：组装 + 全局 store + 浮层宿主
├─ main.ts
├─ api.ts                               # dispatcher + 新方法 + HostError + normalize*
├─ viewmodel.ts                         # DTO→VM、状态→tone/图标/文案 单表
├─ stores/workbench.ts                  # 单例 reactive store（spec §9.3）
├─ types/workbench.ts                   # Tree/Detail/Open/Action VM 类型
├─ composables/
│  ├─ useHostSnapshot.ts                # 首屏 45s + 重试 + Mock 回退（迁移自 App.vue）
│  ├─ useTreeState.ts                   # 每树懒加载/展开/缓存
│  ├─ useVirtualRows.ts                 # 行高 28/26 虚拟列表
│  ├─ useTreeKeyboard.ts                # role=tree 键盘
│  ├─ useToast.ts / useDialogs.ts
├─ components/
│  ├─ shell/MenuBar.vue
│  ├─ shell/TitleBar.vue
│  ├─ shell/StatusBar.vue
│  ├─ workbench/WorkbenchView.vue
│  ├─ workbench/ComponentList.vue
│  ├─ workbench/ComponentCard.vue
│  ├─ workbench/RepoGrid.vue
│  ├─ workbench/RepoTreePanel.vue
│  ├─ workbench/FileTree.vue            # 树渲染 + 虚拟滚动（工作台/文件夹复用）
│  ├─ workbench/InfoPanel.vue
│  ├─ workbench/RepoActionsBar.vue
│  ├─ workbench/BatchBar.vue
│  ├─ folder/FolderView.vue
│  ├─ folder/WorkspaceTree.vue
│  ├─ common/{StateChip,CiBadge,ChangeBadge,TreeIcon,EmptyState,LoadingState,ErrorState,VirtualList}.vue
│  ├─ components/AppMenu.vue / ContextMenu.vue / Popover.vue
│  └─ overlays/{DrawerHost,ModalHost,ToastHost,ConfirmDialog}.vue
│     └─ pages/{CiPage,TasksPage,NotificationsPage,LogsPage,SettingsPage,LegacyBoard}.vue
├─ mocks/                               # 组件/树/详情/七态/慢/失败/CI FAIL/文件夹树
└─ styles/tokens.css                    # 保留 --st-*；追加 --layout-*/--tint-*（spec §9.4）
```

**原则**：容器组件只布局/接线；展示组件纯 props/emits；全部 Host 调用经 `api.ts` + store；`tone/图标/文案` 只在 `viewmodel.ts` 一处映射（spec §9.3）。

### 5.2 AppShell 布局与 CSS（spec §2.2/§9.4 数值）

```css
/* tokens.css 追加（spec §9.4） */
:root{
  --layout-menu-h:30px; --layout-title-h:48px; --layout-actions-h:52px; --layout-status-h:28px;
  --layout-pad-x:16px; --layout-pad-y:12px; --layout-gap:12px;
  --col-left:minmax(248px,20%); --col-right:minmax(224px,18%);
  --card-h:76px; --card-h-compact:60px; --card-pad:7px;
  --tree-row-h:28px; --tree-indent:14px; --tree-indent-max:112px; --tree-head-h:32px;
  --btn-h:40px; --field-h:30px; --chip-h:20px;
  --r-panel:8px; --r-card:8px; --r-chip:6px; --r-btn:6px;
  --tint-success: color-mix(in srgb, var(--st-success-bg) 55%, var(--surface));
  /* … warning/info/purple/cyan/neutral/danger 同理（禁止新增 hex） */
}

/* AppShell */
.app-shell{ height:100%; display:grid;
  grid-template-rows: var(--layout-menu-h) var(--layout-title-h) minmax(0,1fr) var(--layout-status-h);
  background:var(--bg); color:var(--text); overflow:hidden; }
.main{ min-height:0; padding:var(--layout-pad-y) var(--layout-pad-x); overflow:hidden; }

/* 工作台：行1 内容（706/606），行2 底部按钮（52） */
.workbench{ height:100%; display:grid;
  grid-template-columns: var(--col-left) minmax(0,1fr) var(--col-right);
  grid-template-rows: minmax(0,1fr) var(--layout-actions-h);
  gap: var(--layout-gap); min-height:0; }
.workbench .component-list{ grid-column:1; grid-row:1; }
.workbench .batch-bar{ grid-column:1; grid-row:2; }
.workbench .repo-grid{ grid-column:2; grid-row:1; display:grid;
  grid-template-columns:repeat(2,minmax(0,1fr)); grid-template-rows:repeat(2,minmax(0,1fr));
  gap:var(--layout-gap); min-height:0; }
.workbench .info-panel{ grid-column:3; grid-row:1; }
.workbench .repo-actions{ grid-column:2 / -1; grid-row:2; }   /* spec：只占中+右 */

.panel{ display:flex; flex-direction:column; min-width:0; min-height:0; overflow:hidden;
  background:var(--surface); border:1px solid var(--border); border-radius:var(--r-panel); }
.panel__header{ flex:0 0 var(--tree-head-h); display:flex; align-items:center; gap:8px;
  padding:0 10px; border-bottom:1px solid var(--border); background:var(--surface-2);
  font-size:11px; font-weight:700; white-space:nowrap; overflow:hidden; }
.panel__body{ flex:1; min-height:0; overflow:auto; overscroll-behavior:contain;
  scrollbar-gutter:stable; }

/* 文件夹视图：共用左/右/底部，只换中区 */
.folder-view{ height:100%; display:grid;
  grid-template-columns: var(--col-left) minmax(0,1fr) var(--col-right);
  grid-template-rows:minmax(0,1fr) var(--layout-actions-h); gap:var(--layout-gap); }
.folder-view .workspace-tree{ grid-column:2; grid-row:1; }
.folder-view .repo-actions{ grid-column:2 / -1; grid-row:2; }

/* 紧凑档（spec §2.4/§9.4） */
@media (max-height:799px){
  :root{ --layout-menu-h:26px; --layout-title-h:44px; --layout-actions-h:44px;
    --layout-status-h:24px; --layout-pad-y:8px; --card-h:60px; --tree-row-h:26px;
    --tree-head-h:30px; --btn-h:36px; }
}
@media (max-width:1279px){
  :root{ --layout-pad-x:8px; --layout-gap:8px; --col-left:232px; --col-right:208px; }
}
```

- 页面级 `overflow:hidden`；滚动只在列表、树、右栏内容、抽屉/弹窗（spec §2.5）；
- 1440 验收：左 281.6±6 / 右 253.4±6 / 树 418.5±2；1280：249.6/224.6/368.9（AC-UI-02/04）；
- 卡片 76px 三行（L1 20 / L2 20 / L3 16，padding 7，间距 3）；紧凑 60 隐藏 L3；
- 树行 28（紧凑 26）；缩进 14/层，>8 层每层 +4，上限 112；无横向滚动，名称省略 + title；
- 状态栏 7 段（最近操作弹性 + 6 段）；底部按钮 40 高、flex:1、min112/max220、gap12。

### 5.3 状态模型（spec §9.3 + 本方案并发细节）

```ts
export interface TreePanelState {
  status: 'idle'|'loading'|'ready'|'error'
  repoType: RepoType
  entries: TreeNodeVm[]
  byPath: Map<string, TreeNodeVm>          // path → 节点（扁平索引）
  expanded: Set<string>                    // data-path
  loading: Set<string>
  error: string | null
  errorCode: string | null
  truncated: boolean
  totalEntries: number
  scrollTop: number
  epoch: number
  at: number
}

export interface WorkbenchState {
  snapshot: SnapshotDto | null
  source: string
  scanning: boolean
  snapshotError: string
  selectedComponentId: string
  selectedRepoType: RepoType
  selectedPath: string | null
  view: 'workbench' | 'folder'
  selectMode: boolean
  checkedIds: Set<string>
  componentDetail: ComponentDetailDto | null
  componentDetailStatus: 'idle'|'loading'|'ready'|'error'
  trees: Record<RepoType, TreePanelState>
  repoDetailCache: Map<string, { data: RepoDetailDto; at: number }>
  folder: { status; root: WorkspaceNodeVm[]; expanded: Set<string>; loading: Set<string>; error: string|null; epoch: number }
  action: { pending: Action | null; scope: RepoType | null; lastOp: OperationVm }
  overlays: { drawer: 'tasks'|'notifications'|null; modal: 'settings'|'ci'|'logs'|'confirm'|null; confirm: ConfirmVm|null; toasts: ToastVm[] }
  epoch: number
}
```

关键 action（与 spec 行为一致）：

```ts
async function selectComponent(id: string) {
  const epoch = ++state.epoch
  state.selectedComponentId = id
  state.selectedRepoType = pickDefaultRepo(id)        // IMPL 优先，否则第一个存在仓
  state.componentDetailStatus = 'loading'
  resetTrees()
  try {
    const detail = await api.request('getComponentDetail', { componentId: id }, 45_000)
    if (epoch !== state.epoch) return
    state.componentDetail = detail; state.componentDetailStatus = 'ready'
    await Promise.all(REPO_TYPES.map(t => loadTree(t, epoch)))   // 4 并发；每树独立懒加载
    if (epoch === state.epoch) focusRepo(state.selectedRepoType)
  } catch (e) { /* 错误态，保留旧列表；Toast */ }
}

async function loadTree(repoType: RepoType, epoch: number) {
  const t = state.trees[repoType]
  if (cacheFresh(t)) return
  t.status = 'loading'; t.epoch = epoch
  try {
    const res = await api.request('getRepoTree',
      { componentId: state.selectedComponentId, repoType, path: '', depth: 1 }, 10_000)
    if (epoch !== state.epoch || t.epoch !== epoch) return
    t.entries = res.entries; t.truncated = res.truncated; t.totalEntries = res.totalEntries
    t.status = 'ready'; t.at = Date.now()
  } catch (e) { if (epoch === state.epoch) { t.status = 'error'; t.error = ...; } }
}

async function toggleNode(repoType: RepoType, path: string) {
  const t = state.trees[repoType]
  if (t.expanded.has(path)) { t.expanded.delete(path); return }
  const node = t.byPath.get(path)
  if (!node?.hasChildren) return
  if (!node.children) {
    if (t.loading.has(path)) return
    t.loading.add(path)
    try {
      const res = await api.request('getRepoTree',
        { componentId: state.selectedComponentId, repoType, path, depth: 1 }, 10_000)
      if (t.epoch !== state.epoch) return
      insertChildren(t, path, res.entries)   // 更新 children + byPath
    } finally { t.loading.delete(path) }
  }
  t.expanded.add(path)
}
```

- epoch 规则：组件切换 `state.epoch++`；每树自己 `epoch`；响应落地前双重校验；
- `refreshAll()`：`refresh` → 替换快照 → 清树/详情缓存 → 重载当前组件（保留 selected/滚动/展开交集）；
- `selectedComponentId/selectedRepoType/selectedPath` 是唯一选中事实源（spec §9.3），组件不各自维护；
- 树缓存 `treeCache: Map<`${componentId}/${repoType}`, …>`，滚动位置按组件+仓保留（spec §4.1 最近 20 组 LRU）。

### 5.4 API 层（`api.ts`）

```ts
export type RequestMethod =
  | 'getSnapshot' | 'refresh'
  | 'getComponentDetail' | 'getRepoDetail' | 'getCommits'
  | 'getRepoTree' | 'getWorkspaceTree'
  | 'openPath' | 'runRepoAction' | 'getCiStatus' | 'getHostInfo'

const TIMEOUTS: Record<RequestMethod, number> = {
  getSnapshot: 45_000, refresh: 120_000,
  getComponentDetail: 45_000, getRepoDetail: 10_000, getCommits: 15_000,
  getRepoTree: 10_000, getWorkspaceTree: 10_000,
  openPath: 8_000, runRepoAction: 5_000, getCiStatus: 2_000, getHostInfo: 2_000,
}

// 单 listener + Map<id, pending>（替代旧“每请求 addEventListener”）
function onReply(ev: MessageEvent<unknown>) {
  const reply = asRecord(parseMaybeJson(ev.data)); if (!reply) return
  const id = String(reply['id'] ?? ''); const p = pending.get(id); if (!p) return
  pending.delete(id); clearTimeout(p.timer)
  if (reply['ok'] === false) p.reject(new HostError(String(reply['code'] ?? 'E_UNKNOWN'),
    String(reply['error'] ?? 'Host 拒绝了请求'), reply['details'], p.method))
  else p.resolve(pickData(reply))
}

export class HostError extends Error {
  constructor(public code: string, message: string, public details?: unknown, public method?: RequestMethod) { super(message) }
  get retryable() { return ['E_TIMEOUT','E_INTERNAL','E_IO_ERROR','E_ACCESS_DENIED','E_GIT_FAILED'].includes(this.code) }
}

export function request<T, P = undefined>(method: RequestMethod, params?: P, timeoutMs = TIMEOUTS[method]): Promise<T>
```

- 旧 `normalizeSnapshot/normalizeRepo` 宽容解析保留（旧 Host 兼容）；新增 `normalizeComponentDetail/normalizeRepoDetail/normalizeRepoTree/normalizeWorkspaceTree/normalizeOpenResult/normalizeCommits`（必填校验，缺失 → `E_INVALID_RESPONSE`）；
- `hasHostBridge()/toErrorMessage()` 签名保留；`request` 的 3 个旧调用点改造时统一 `request('getSnapshot', undefined, 45_000)`；
- Mock 桥（P3）：`window.__DSH_TEST_BRIDGE__`/`Runtime.addBinding` 按 spec §10.3 注入可控应答（慢/失败/七态/CI FAIL），真实 Host 另跑一组。

### 5.5 树虚拟化（spec §4.5）

```ts
interface VisibleRow { node: TreeNodeVm; depth: number; path: string; index: number }

function flatten(entries: TreeNodeVm[], expanded: Set<string>, byPath: Map<string, TreeNodeVm>): VisibleRow[] {
  const rows: VisibleRow[] = []
  const walk = (nodes: TreeNodeVm[], depth: number) => {
    for (const node of nodes) {
      rows.push({ node, depth, path: node.path, index: rows.length })
      if (node.kind === 'DIR' && expanded.has(node.path)) {
        const children = byPath.get(node.path)?.children ?? []
        walk(children, depth + 1)
      }
    }
  }
  walk(entries, 0)
  return rows
}

const ROW_H = computed(() => compact.value ? 26 : 28)
const start = computed(() => max(0, floor(scrollTop / ROW_H) - 6))
const end = computed(() => min(rows.length, ceil((scrollTop + viewportH) / ROW_H) + 6))
```

- `VirtualList.vue`：外层 `overflow:auto`，内层总高 `rows.length * ROW_H`，窗口 `translateY(start * ROW_H)`；只渲染 `start..end`；
- 单面板 DOM 行 <80（spec AC-UI-28）；`scrollToIndex` 键盘用；展开/收起只改数组，不重建树；
- 大目录：Host 2000 截断 + `tree-truncated` 警告行（spec AC-UI-28）；不再逐屏请求；
- 深缩进：`padding-left = min(14 * depth, 112)`；名称 `text-overflow:ellipsis`，`title` 全路径。

### 5.6 交互契约（spec §3.3/§4.2/§6）

| 操作 | 行为 |
|---|---|
| 单击组件卡片 | 选中 → 4 树/右栏切换；默认仓 IMPL（无则第一个存在仓），树焦点落 IMPL；`aria-selected=true` |
| 双击组件卡片 | 与单击同义（不打开外部程序，spec §3.3） |
| 单击树节点 | 仅选中（文件/目录），右栏切“节点信息”；不改仓上下文（除非节点属另一面板=切焦点） |
| 单击箭头 / `Enter`（目录） / `Space` | 展开/收起（懒加载）；`ArrowRight/Left` 同 |
| 双击文件 | 白名单 → `openPath(mode:'open')`；危险/未知 → `ConfirmDialog` → `confirm:true` 重发（AC-UI-10） |
| 双击目录 | `openPath(mode:'reveal')`（资源管理器），**不翻转展开态**（AC-UI-11/R-13） |
| 右键树节点 | 菜单：在资源管理器中打开 / 复制路径 / 打开所在目录 / 复制文件名 |
| 右键组件卡片 | SYNC/CREATE/Clone/Delete/复制组件 ID/在资源管理器中打开（spec §3.4；M2 命令禁用+原因） |
| 底部 PULL/FETCH/CHKOUT/COMMIT/PUSH | `runRepoAction`；M1 收到 `E_NOT_IMPLEMENTED` → Toast/对话框“M1 只读，M2 启用”；矩阵禁用时 `title` 给具体原因（spec §6.2） |
| 批量条/卡片 hover/右键/菜单“操作” | 4 个批量入口（spec §3.5）；M1 菜单项禁用 + M2 说明；>10 组件/40 仓二次确认（M2） |
| 刷新（F5/标题栏/菜单） | `refresh` → 不闪白、保留选中/滚动；完成后重载当前组件（spec AC-UI-25） |
| 视图切换 | 标题栏 `[工作台\|文件夹]` + 菜单 + `Ctrl+1/2`；保留选中/滚动；`localStorage["ui.v3.view"]`（AC-UI-34） |
| 搜索 | 标题栏搜索（Ctrl+K）：组件 ID/仓库/分支/Tag/短 SHA；IME `compositionend` 触发（AC-UI-24） |

**确认对话框**（危险扩展名）：

```
标题：打开 .ps1 文件？
正文：build.ps1 是可执行脚本，用默认程序打开可能执行代码。
路径：RTCT_Demo_Impl/tools/build.ps1
[取消] [仍然打开]
```

### 5.7 文件夹视图（spec §7，候选 A）

- 中区由 2×2 换成单棵 `WorkspaceTree`，左栏/右栏/底部按钮/BatchBar/标题/状态栏全部复用；
- 节点：`COMPONENT`（虚拟组）→ `REPO`（真实目录 + 快照状态）→ `DIR/FILE`；`UNBOUND_GROUP`（未纳管）；
- 展开组件组零请求（快照内联 `repos`）；展开仓/目录 `getWorkspaceTree({path})` 懒加载 + 30s 缓存（AC-UI-14）；
- 单击组件→右栏组件概览 + 左栏同步选中；单击仓→右栏 4 Tab + 底部五键可用；单击文件→右栏文件信息（文件名/相对路径/扩展名/大小/修改时间/git 状态/白名单/所属仓 + 打开/打开目录/复制）；
- 双击：仓/目录 `reveal`；文件白名单 `open`/危险确认；
- 空态：未配置工作区 → “未配置工作区 + 打开设置”；空工作区 → “工作区为空（无组件目录）+ 重新扫描”；根不可读 → 错误码 + 重试（spec §7.3）；
- 选中同步：节点携带 `componentId/repoType`（Host 返回），底部按钮按祖先仓启用；切回工作台恢复选中（spec §7.5）。

### 5.8 可访问性与键盘（spec AC-UI-08/32）

| 区域 | ARIA/交互 |
|---|---|
| 菜单栏 | `role=menubar`/`menuitem`；Alt 聚焦；←/→/↑/↓/Enter/Esc |
| 组件列表 | `role=listbox`；卡片 `role=option` `aria-selected`；↑/↓ 移动、Enter 选中；focus ring ≥2px |
| 树 | 容器 `role=tree` + `aria-label`；行 `role=treeitem` `aria-level` `aria-expanded`（目录）`aria-selected`；`aria-activedescendant`；↑/↓/←/→/Home/End/Enter/Space/Esc；虚拟滚动保证活动行已渲染 |
| 操作栏 | 原生 `button`；禁用 `aria-disabled=true`（可聚焦）+ 原因 `title`；`aria-busy` 进行中；五键等宽 |
| 状态栏/Toast | `role=status`（最近操作/复制成功）、错误 `role=alert`；Toast 最多 3 条堆叠 |
| 浮层 | `role=dialog`/`aria-modal`；焦点陷阱；Esc 关闭；关闭后焦点回触发元素；底层 `inert/aria-hidden` + 滚动锁定 |
| 焦点/动效 | `:focus-visible{outline:2px solid var(--focus); outline-offset:2px}`；120ms hover/240ms 展开/200ms 视图切换；`prefers-reduced-motion` 关闭；`forced-colors` 保留边框/图标 |
| 颜色 | 状态必须“图标+中文+颜色”；CI 形状独立；token 对比度 ≥4.5:1（AC-UI-23/32） |

### 5.9 响应式与 1280/1440/DPI（spec §2.6/§9.5）

- 基线：1440×900（A）、1280×800（B），dpr=1；`Emulation.setDeviceMetricsOverride` 驱动（AC-UI-02/04/31）；
- 1280：左 249.6 / 中 749.8（树 368.9）/ 右 224.6；右栏字段标签列 56；
- 1024–1279：紧凑档（padding 8/gap 8/卡片 60/树行 26/状态栏折叠图标），只保证不溢出、入口可达；
- <1024：单列降级（左栏抽屉/右栏底部抽屉），不进入截图验收；WPF `MinWidth=1024/MinHeight=720`；
- 125%/150%：CSS px 随 WebView2 DPR，不做 CSS zoom/手工系数；125% 触发紧凑档；验收断言无横向溢出（AC-UI-31）；
- 页面级永不滚动；`document.scrollingElement.scrollWidth <= innerWidth+1`（AC-UI-04/05/27）。

### 5.10 CI 10 锚点新落点（spec §8.1，testid 不变）

| # | 新落点 | v1 交付物 | testid |
|---|---|---|---|
| CI-1 | 菜单栏 工具→CI 流水线；状态栏 CI 队列段点击 | CI 弹窗（960×640）骨架 + 空态 + 实验标记 + 面包屑 | `ci-nav` |
| CI-2 | 组件卡 L1 右侧固定 18×18 | 固定槽位 + `∅ 无 CI` 中性空态 | `ci-slot` |
| CI-3 | 右栏顶部 40px | 四仓计数 0/0/0/0 + 空最近失败 + “未接入” | `ci-strip` |
| CI-4 | 右栏 tablist 第 4 项 | 空态 + `CiLatest` 字段占位表 | `ci-tab` |
| CI-5 | 状态栏第 5 段 | 图标 + `CI 0`（v1 恒 0） | `ci-queue` |
| CI-6 | 通知抽屉 CI 事件类目 | 类型枚举 + 空分组 | `ci-notif-category` |
| CI-7 | 左栏筛选 popover | 禁用项 + tooltip “即将支持” | `ci-facet` |
| CI-8 | 设置弹窗第 5 分区 | URL/Token/轮询/开关 disabled + 实验标记 | `ci-settings` |
| CI-9 | CI 弹窗契约块 + `api.ts` 类型 | `CiLatest/ComponentCiAggregate/ICiProvider` + `CI_UPDATED` 空实现 | `ci-contract` |
| CI-10 | 组件列表容器 + 单测 | `data-ci-affects-base="false"`；注入 CI FAIL 卡片底色不变 | `ci-card-base` |

> 10 个 testid 在 DOM 中各恰好 1 个（CI-9/CI-8/CI-6 打开对应浮层后断言，spec AC-UI-21）。CI 形状（圆点/半环/空心）与通道 A chip 形状不同，CI 永不参与卡片底色/聚合/排序。

### 5.11 data-testid 与旧页面迁移

- 完整注册表按 spec §10.1（`menubar/titlebar/.../ci-*` 共 60+ 项）；实现时逐项落 DOM，供 CDP 34 条 AC 使用；
- 旧 7 页降级为浮层页：CI 弹窗、任务抽屉 480、通知抽屉 420、日志弹窗 960×640、设置弹窗 760×560、未绑定 800×560；内容/testid 保留（spec §8.3/§11.1）；
- 看板表格保留 `LegacyBoard` 作为“视图→表格视图（P2 备选）”，默认不暴露（spec R-01）。

---

## 6. 风险与实施顺序

### 6.1 风险表

| # | 风险 | 概率/影响 | 缓解 |
|---|---|---|---|
| R-1 | 4000+ 组件全量快照（1.5–3MB）经 WebMessage + 前端虚拟化 | 中/高 | spec 风险 1：v1 全量 + 虚拟列表；实测 >200ms 或 >5MB 启用 `getComponentPage` 分页（契约预留，不改 UI） |
| R-2 | 大目录/深路径/长路径/网络盘导致树卡顿 | 中/高 | 单目录 2000 截断 + 虚拟滚动 + 30s 缓存 + `Task.Run`；网络盘超时降级只读 |
| R-3 | 路径逃逸（`..`/绝对/UNC/ADS/保留名）与 reparse | 中/高 | `PathPolicy` 黑名单 + canonical + 仓根二次收口；**测试先行**；安全评审签字 |
| R-4 | `openPath` 启动默认程序的安全/测试副作用 | 中/高 | 白名单 + 危险扩展名确认 + `.git/.trash/submodule` 禁止；`MBD_E2E_OPEN_DRY_RUN` 仅测试 |
| R-5 | 4 树并发 + 快速切组件旧响应覆盖 | 高/中 | 全局/树 epoch；`loading` 去重；响应双校验；缓存按 `generatedAt` |
| R-6 | 卡片 55% tint 与 SRS 对比度门禁冲突 | 中/中 | 只由 token 派生；门禁不过降 40%（spec R-04/AC-32）；保留“中性卡片”样式类兜底 |
| R-7 | CI 10 锚点丢失/重复 | 中/高 | testid 注册表 + AC-UI-21 唯一性；§5.10 逐项 |
| R-8 | `gitStatus` 一次状态可能慢（大仓 1–3s） | 中/中 | 每仓一次 + 缓存 30s + 10s 超时降级 NONE + warning；4 仓并发 |
| R-9 | `SnapshotDto` 追加字段破坏旧构造/旧 JSON | 低/中 | 全部可选参数；golden 契约测试；旧 71 测试回归 |
| R-10 | 旧 M1 行为（数据源/45s/重试/Mock 回退/Toast）在新 UI 丢失 | 中/中 | §1.3 清单逐项 CDP 回归；P4 门禁 |
| R-11 | 批量/单仓共用任务队列导致按钮锁死 | 中/中 | M1 不实现写；M2 单一 job 状态源 + 同仓互斥/异仓并行 + 明确原因（spec 风险 6） |
| R-12 | 文件夹虚拟组件组与实际扁平目录不一致 | 中/中 | 分组在 Host（`RepoNameParser` 唯一权威）+ `UNBOUND_GROUP` 兜底；UI 不解析目录名 |
| R-13 | DPI 125% 触发紧凑档/单列降级 | 中/中 | 验收固定 dpr=1 的 1440/1280；AC-UI-31 单独验证；单列只保证可达 |
| R-14 | spec/任务书方法命名差异引发返工 | 中/中 | §0.6 映射 + `features.componentDetail` 开关；契约 golden |
| R-15 | `getCommits` 10,000 提交性能（spec 首屏 ≤500ms） | 中/中 | `git log -n 5` 概览 + `getCommits` 100/页；`GIT_OPTIONAL_LOCKS=0`；缓存 10s；虚拟列表 |

### 6.2 并行性与依赖

**可并行**

- A（Host）：`PathPolicy` + `FileTreeReader` + `RepoTreeService` + `WorkspaceTreeService` + 单测；
- B（Host）：`SnapshotDto` 增量 + `SnapshotIndex` + `ComponentDetailService` + `RepoDetailService/CommitsService` + `OpenPathService` + `RepoActionService`；
- C（前端）：AppShell/组件拆分 + Mock 树/详情 + 虚拟树 + 文件夹视图 + 浮层（不依赖 Host，P3 可先跑）；
- D（测试）：CDP 客户端 + MockBridge + AC-UI 脚本（依赖 testid，可先写框架）；
- E（文档）：SRS delta/AC 更新/截图归档。

**必须串行**

1. 契约冻结（§3 + spec B.1）→ 前端 `api.ts`/`viewmodel.ts` 类型最终化；
2. `PathPolicy` + 单测 → `OpenPathService` 接 UI；
3. Host API 全通 → P4 真实联调；
4. CI 锚点/ testid 注册表确认 → P3 验收；
5. 安全评审（path/open）→ 合并前。

### 6.3 四阶段 + 验收（对齐 spec §11.3）

| 阶段 | 内容 | 出口验收 | 映射 |
|---|---|---|---|
| **P1 契约与安全基座** | §3 契约 + spec B.1 `SnapshotDto` 增量；`BridgeErrors`（`E_*`）；`PathPolicy`+`PathUtil`；golden JSON；Host 测试项目骨架（路径/契约先行） | `dotnet test` 全绿；契约评审通过；路径逃逸/黑名单单测覆盖；**不改 UI、不改现有 API 行为；旧 71 测试绿** | S2 前置 |
| **P2 Host 数据层** | `SnapshotIndex`；`RepoTreeService/FileTreeReader/GitStatusIndex`；`WorkspaceTreeService`；`RepoDetailService/CommitsService`；`OpenPathService`；`RepoActionService`；`ComponentDetailService`；`TreeCache`；`HostCommandRouter` + `WebViewBridge` 改造；`getCiStatus` Mock | `dotnet build` 0 warning；`dotnet test` 全绿（Host 新增 ≥35：路径/深目录/中文空格/长路径/大目录/gitStatus/分页/reparse/submodule/open 确认）；`getRepoTree` P95 ≤300ms/2000；**无 UI 改动** | S2 |
| **P3 前端新工作台（Mock 驱动）** | spec §9.1 组件树；AppShell 布局；菜单栏/TitleBar/状态栏；组件卡片+BatchBar；四仓树+虚拟滚动；InfoPanel 4 Tab；底部五键；文件夹视图；浮层（CI/设置/通知/任务/日志/确认）；七态 Mock；testid 注册表 | 1440×900/1280×800 无溢出；AC-UI-01..34 全过（Mock 桥）；CI 锚点 10/10；0 console error；截图 4 张归档 | S3 |
| **P4 真实数据联调与加固** | 接 Host 全部只读 API；保留 M1 回退/重试/刷新；真实 Gitea + 本地夹具回归；安全/性能/视觉评审修复；文档/AC/截图收口 | 真实 RTCRepos 4 组件/14 仓渲染；本地夹具 2/6；双击打开（dry-run）+ 危险扩展名确认；刷新保留选中；真实环境 0 console error；P0/P1=0；测试全绿 | S4 + S5 |

> S6（单仓写操作）不在本期实现：`runRepoAction` 契约已冻结，M2 替换 Mock 为真实 job + `JOB_UPDATED`，不改布局/testid（spec §11.3）。

---

## 7. 测试方案

### 7.1 Host 单测/集成测试

**新项目** `tests/MbdGitTool.Host.Tests`：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="coverlet.collector" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\MbdGitTool.Host\MbdGitTool.Host.csproj" />
    <ProjectReference Include="..\..\src\MbdGitTool.Core\MbdGitTool.Core.csproj" />
  </ItemGroup>
  <ItemGroup><Using Include="Xunit" /></ItemGroup>
</Project>
```

**临时目录夹具**

```csharp
public sealed class TempWorkspace : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "MbdGitToolTests", Guid.NewGuid().ToString("N"));
    public TempWorkspace() => Directory.CreateDirectory(Root);

    public string Dir(params string[] parts)
    {
        var p = Path.Combine(new[] { Root }.Concat(parts).ToArray());
        Directory.CreateDirectory(p); return p;
    }

    public string File(string relative, string content = "x", DateTime? mtimeUtc = null)
    {
        var p = Path.Combine(Root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(p)!);
        System.IO.File.WriteAllText(p, content);
        if (mtimeUtc is not null) System.IO.File.SetLastWriteTimeUtc(p, mtimeUtc.Value);
        return p;
    }

    public void Dispose()
    {
        try
        {
            if (Root.StartsWith(Path.Combine(Path.GetTempPath(), "MbdGitToolTests"), StringComparison.OrdinalIgnoreCase))
                Directory.Delete(Root, recursive: true);
        }
        catch { }
    }
}
```

**用例清单**

| 测试类 | 用例 | 断言 |
|---|---|---|
| `PathPolicyTests` | `../outside`、`a/../../x`、`C:\Windows`、`\\server\share`、`/etc/passwd`、`a\0b`、`file.txt:ads`、`CON`、`sub/NUL.txt`、`a<b`、`.. `、`...`、绝对/UNC/盘符、中文/空格合法、大小写、>1024 | `E_PATH_INVALID/E_PATH_OUTSIDE_ROOT` 正确；合法路径 canonical/relative 稳定 |
| `FileTreeReaderTests` | 排序（DIR 先/忽略大小写）、隐藏过滤、`.git/.trash` 永不出现、size/mtime、depth=0/1/2、2000 截断 + `totalEntries`、5000/10000 文件完整性、20000 保护、无权限不炸、reparse 不递归、中文/空格/长路径、空目录 | 条目/顺序/`truncated`/`totalEntries`/`kind` |
| `GitStatusIndexTests` | 临时真实 git 仓（`git init` + 文件改动/新增/删除/未跟踪） | `MODIFIED/ADDED/DELETED/UNTRACKED` 映射；无变更全 `NONE`；非仓返回空；缓存命中 |
| `RepoTreeServiceTests` | 根/子目录、缺仓 `E_REPO_NOT_LOCAL`、INVALID 目录可读、路径 `../` 拒绝、仓根二次收口、limit 截断 | `data` 形状/错误码/entries |
| `WorkspaceTreeServiceTests` | 组件分组（4 仓顺序）/缺仓 `isLocal=false`/未纳管组/根文件/懒加载/`path` 越界/reparse | `kind`/`repos`/`UNBOUND_GROUP` |
| `RepoDetailServiceTests` / `CommitsServiceTests` | 真实临时 git 仓 3 提交；`getRepoDetail` 前 5 + total；`getCommits` skip/take 无重无漏；非仓/缺失 → 空 + errorCode | 字段/分页/错误码 |
| `OpenPathServiceTests` | SAFE_TEXT → launcher 1 次（FileName=canonical、UseShellExecute）；DANGEROUS 未确认 → `E_CONFIRM_REQUIRED` 且 0 次；确认 → 1 次；目录 open/reveal；root 外/`.git`/ADS/submodule → 拒绝；launcher 失败 → `E_LAUNCH_FAILED`；env dry-run 不启动 | 注入 `RecordingLauncher`；**测试绝不真正打开文件** |
| `RepoActionServiceTests` | 13 状态 × 5 键矩阵逐格；M1 全部 `E_NOT_IMPLEMENTED`；未知 action/仓 → 错误码；reason 文案非空 | spec §6.2 表 |
| `ComponentDetailServiceTests` | 四仓顺序/actions 5 个/CI hint/缺仓 | DTO |
| `HostCommandRouterTests` | 未知方法、params 类型错、缺参、component 不存在、超时（注入 10ms policy）、信封 camelCase、错误 `error`+`code`+`details`、响应超限 | 不抛异常/错误码 |
| `SnapshotDtoCompatTests` | 旧 JSON（无新字段）反序列化；新字段 camelCase；旧构造调用可编译；71 旧测试绿 | 兼容 |
| `SnapshotIndexTests` | 组件/仓/本地路径索引、大小写/尾斜杠、缺失路径不抛 | 查找 |

**集成（真实 git 夹具，nightly）**：用 `state-matrix.ps1 -Only F-SYNCED` 生成 4 仓组件 → `SnapshotService` + `getRepoTree/getRepoDetail/getComponentDetail/runRepoAction` 全链路；断言树根 4 仓、gitStatus 字母、矩阵与状态一致。

### 7.2 契约测试

- `tests/contracts/bridge/*.json`：`getSnapshot/refresh/getComponentDetail/getRepoDetail/getCommits/getRepoTree/getWorkspaceTree/openPath(E_CONFIRM_REQUIRED)/runRepoAction(E_NOT_IMPLEMENTED)`；
- Host 序列化样例 → 递归比较“键名集合/嵌套键集合”（不比较值/SHA/时间）；
- 前端 `normalize*` 吃同一 golden → 断言 VM；
- 任一字段改名/删除即失败；新增字段允许但同 PR 更新 golden + 本文 §3。

### 7.3 前端 CDP 用例（spec §10.2 AC-UI-01..34）

**执行约定（spec §10.3）**

- 启动：`$env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS='--remote-debugging-port=9222'`；`dotnet run --project src\MbdGitTool.Host`；Node 脚本连 `http://127.0.0.1:9222/json`；
- 视口：`Emulation.setDeviceMetricsOverride { width:1440,height:900,deviceScaleFactor:1 }`（1280×800 同理）；截图前 `Page.bringToFront` + 2 帧；
- 交互：一律 `Input.dispatchMouseEvent`（双击 = 两次 click 间隔 80ms）/`Input.dispatchKeyEvent`，**禁止 `element.click()`**；
- Mock 桥：`window.__DSH_TEST_BRIDGE__`/`Runtime.addBinding` 注入慢动作/失败/七态/CI FAIL；真实 Host 另跑一组；
- 产物：截图 `docs/images/ui-v3/`；报告 `artifacts/ui-v3/report.json`（gitignored）。

**34 条 AC 分组（逐条断言细节以 spec §10.2 为准）**

| 组 | AC | 本方案补充执行点 |
|---|---|---|
| 布局 | 01–05 | 1440/1280 尺寸/顺序/gap/padding、页面不滚动、内部滚动 |
| 左栏 | 06–08, 24 | 卡片 4 仓章/`*`/`●N`、选中、键盘、搜索 IME |
| 树 | 09–12, 27–28 | 展开/折叠、双击文件/目录、MISSING 空态、长路径、5000 项截断/DOM<80 |
| 文件夹视图 | 13–14 | 切换、`workspace-node[data-kind=COMPONENT]`、组件组零请求、仓只请求 1 次、缓存 |
| 底部/批量 | 15–20 | scope、13 状态矩阵、进行中/失败、批量确认、右键菜单 |
| CI/状态色 | 21–23 | 10 锚点各 1 个、CI FAIL 不改卡片底色、token 无硬编码 hex |
| 刷新/状态栏 | 25–26 | 不闪白、7 段/最近操作 |
| 截图/控制台/DPI/深色/七态/持久化 | 29–34 | 错误 0、125% 无溢出、深色/focus trap/Esc、七态、`localStorage` 视图 |

**截图归档**：`workbench-1440.png`、`workbench-1280.png`、`folder-1440.png`、`action-disabled.png`（AC-UI-29）。

### 7.4 真实 Gitea/本地夹具回归

| 环境 | 数据 | 断言 |
|---|---|---|
| Mock（无 workspace） | `source=mock` | 工作台渲染、数据源胶囊不可隐藏、七态可用、0 error |
| 本地 `artifacts/m1-fixture/workspace` | 2 组件/6 仓（含 2/4 缺失） | 卡片 2；缺仓面板 MISSING 空态；SYNCED 仓展开文件；`gitStatus` 字母；矩阵一致 |
| 状态矩阵 `state-matrix.ps1 -Only F-SYNCED,F-DIRTY,F-UNMERGED,F-MISSING,F-BUSY,F-REMOTE-GONE` | 真实 git 状态 | 右栏 flags/矩阵；`F-UNMERGED` PULL/COMMIT `E_ACTION_NOT_ALLOWED`；`F-MISSING` 空态；`F-BUSY` 原因 |
| 真实 Gitea `RTCRepos`（`http://192.168.50.20:3000`） | 4 组件/14 仓 | `source=local+gitea`；RTCT_Demo 四仓树与状态；双击文件（dry-run）；0 console error；token 不出现在日志/响应 |
| 文件夹视图 | 上述本地/真实工作区 | 组件虚拟组/未纳管组/仓展开/双击 reveal；切换保留选中 |

> 真实 Gitea 注入方式沿用 `docs/real-gitea-test.md`（token 仅环境变量，不落仓库/日志）。

### 7.5 验收命令与门禁

```powershell
# 编译/单测（P1 起）
dotnet build MbdGitTool.sln
dotnet test  MbdGitTool.sln

# 前端类型检查/构建（P3 起）
npm --prefix ui run build

# 真实 Host + CDP（P3/P4）
$env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS='--remote-debugging-port=9222'
$env:MBD_WORKSPACE_ROOT='D:\Workspace\git_1\artifacts\real-test\workspace'
$env:MBD_E2E_OPEN_DRY_RUN='1'
dotnet run --project src\MbdGitTool.Host
node ui/e2e/run-ac.mjs --port=9222

# 状态夹具（nightly / P4）
pwsh -NoProfile -File reqs/v0.2/fixtures/state-matrix.ps1 -Root $env:TEMP\MbdGitFixtures\ui-v3 -Only F-SYNCED,F-DIRTY,F-UNMERGED,F-MISSING
```

门禁：`dotnet build` 0 warning（`TreatWarningsAsErrors`）；`dotnet test` 全绿；`npm run build`（vue-tsc）通过；AC-UI 34/34；0 console error/exception；1440/1280 无溢出；安全用例全过；契约 golden 未漂移；截图归档；本文 §3/§5 变更同步。

---

## 8. 假设与待裁决

1. **spec-v3.0 是 UI 唯一输入**（S1 交付物，冻结候选）；本文按其 §1.3/§2/§4/§5/§6/§7/§8/§9/§10 对齐。若 spec 后续修订，以 spec 为准并回写本文。
2. **方法命名映射（§0.6）**：`getComponentDetail` 为任务书要求的增量方法，与 spec 的 `getRepoDetail/getCommits` 并存；若 spec owner 要求严格收敛，可用 `features.componentDetail=false` 关闭并由 UI 用 `getSnapshot.components + getRepoDetail` 组合。
3. **`getRepoTree` 的 `limit/cursor`** 是 spec 2000 截断之外的扩展（为后续大目录“加载更多”预留）；M1 UI 不使用 `cursor`，只用 `truncated` 警告行。
4. **`gitStatus/ignored/submodule` 的 M1 深度**：`gitStatus` 每仓一次 `git status`（默认开）；`ignored` 默认关、开启时批探测；`submodule` 读 `.gitmodules`（best-effort）。若性能评审认为 4 仓 `git status` 超出预算，可对超大仓降级为 `NONE` + 面板警告。
5. **`getRepoDetail/getCommits` 走只读 `git log`**（M1 允许只读 git；与 spec “只读接口”一致）；若要求完全零 git 子进程，则 `commits` 需 M2 由 GitWorker 提供，届时接口形状不变。
6. **旧表格看板保留期限**：P3 保留 `LegacyBoard` 过渡，S5 评审后决定删除或永久作为“视图→表格视图”（spec R-01 是 P2 备选）。
7. **窗口尺寸/视图持久化**：`localStorage["ui.v3.view"]` + WPF `MinWidth/MinHeight`；窗口几何保存 M1 可选，不阻塞验收。
8. **`getCiStatus` 为 Mock**；CI v2 接入时只换数据源不改 UI 结构（CI-9 契约冻结）。

---

## 附录 A：实施时文件变更清单（本文件不执行）

**新增**

- `src/MbdGitTool.Core/Contracts/{WorkbenchDto,TreeDto,BridgeErrors}.cs`
- `src/MbdGitTool.Host/Bridge/{HostCommandRouter,BridgeParams}.cs`
- `src/MbdGitTool.Host/Services/{SnapshotIndex,PathPolicy,PathUtil,RepoTreeService,FileTreeReader,GitStatusIndex,WorkspaceTreeService,RepoDetailService,CommitsService,OpenPathService,ShellProcessLauncher,RepoActionService,ComponentDetailService,TreeCache,HostInfoProvider}.cs`
- `tests/MbdGitTool.Host.Tests/*`、`tests/contracts/bridge/*.json`
- `ui/src/{viewmodel.ts,stores/workbench.ts,types/workbench.ts,composables/*,components/**,mocks/**}`
- `ui/e2e/{cdp-client.mjs,run-ac.mjs,mock-bridge.mjs}`
- `docs/ui-redesign-code-plan.md`（本文）

**修改**

- `src/MbdGitTool.Core/Contracts/SnapshotDto.cs`（spec B.1 可选参数）
- `src/MbdGitTool.Core/Domain/ComponentAssembler.cs`（`ToRepoDto` 填新字段）
- `src/MbdGitTool.Host/Services/SnapshotService.cs`（`SnapshotIndex`/`WorkspaceRoot`/刷新钩子）
- `src/MbdGitTool.Host/Bridge/WebViewBridge.cs`（薄适配器 + `params`/`code`）
- `src/MbdGitTool.Host/Services/AppConfig.cs`（可选 `ui.*`/白名单）
- `src/MbdGitTool.Host/MainWindow.xaml(.cs)`（`MinWidth/MinHeight`；可选 E2E 能力开关）
- `ui/src/App.vue`（拆为 AppShell）、`ui/src/api.ts`（dispatcher）、`ui/src/styles/tokens.css`（`--layout-*`/`--tint-*`；旧 `.app` 样式迁移）
- `ui/package.json`（`e2e` script，无新增运行时依赖）
- `MbdGitTool.sln`（Host 测试项目）
- `reqs/requirements.md`（SRS delta：FR-UI/FR-FS/AC-UI，spec §11.2 已列，S1 产出）

**不修改**

- `ui/src/main.ts`；`LocalScanner/GitCli/GiteaClient` 只读扫描逻辑（`getCommits` 复用 `GitCli` 但不改其接口）；`app.manifest`（已有 longPathAware）。

## 附录 B：关键类/方法签名

```csharp
// Contracts
public sealed record ComponentDetailDto(string ComponentId, string AggregateState, int SeverityRank,
    string? Branch, string? Tag, bool MixedBranch, bool? Incomplete, IReadOnlyList<string> Flags,
    string? WorkspaceRoot, string GeneratedAt, CiHintDto Ci, IReadOnlyList<RepoDetailDto> Repos);

public sealed record RepoDetailDto(string RepoType, string RepoName, string State, string? SubType,
    string? Branch, string? Tag, string? HeadSha, string? UpstreamRef, string? CompareRef,
    int Ahead, int Behind, int DirtyCount, int UntrackedCount, int StashCount,
    bool IsLocal, string? LocalPath, bool IsStale, string? CalcAt, string? DataSource,
    string? ErrorCode, IReadOnlyList<string> Flags, IReadOnlyList<RepoActionAvailabilityDto> Actions);

public sealed record RepoActionAvailabilityDto(string Action, bool Enabled, bool MatrixAllowed,
    string? ReasonCode, string? Reason, bool RequiresConfirm, string Stage);

public sealed record CiHintDto(bool Enabled, string State, string Provider, string? FetchedAt);

public sealed record TreeNodeDto(string Path, string Name, string Kind, bool HasChildren,
    long? Size, DateTimeOffset? Mtime, string GitStatus, bool Submodule, bool Ignored,
    IReadOnlyList<TreeNodeDto>? Children, string? Error);

public sealed record RepoTreeDto(string ComponentId, string RepoType, string RepoName, string RootPath,
    string Path, string? Branch, string? HeadSha, string State, int DirtyCount, string? CompareRef,
    string? FetchedAt, string ReadAt, IReadOnlyList<TreeNodeDto> Entries, bool Truncated,
    int TotalEntries, string? Error, int Limit, string? NextCursor);

public sealed record WorkspaceTreeDto(string Root, string Path, string GeneratedAt, string ReadAt,
    IReadOnlyList<WorkspaceNodeDto> Entries, bool Truncated, string? Error);

public sealed record CommitDto(string Sha, string ShortSha, string Message, string Author,
    string At, IReadOnlyList<string> Refs);

// Host
public sealed class HostCommandRouter { public Task<CommandResult> DispatchAsync(string? method, JsonElement? parameters, CancellationToken ct); }
public sealed class PathPolicy { public bool TryResolveWorkspacePath(string? relative, out string full, out BridgeError? error); public bool TryResolveRepoPath(string repoRoot, string? relativeToRepo, out string full, out BridgeError? error); }
public sealed class RepoTreeService { public CommandResult GetTree(GetRepoTreeParams p, CancellationToken ct); }
public sealed class WorkspaceTreeService { public CommandResult GetTree(GetWorkspaceTreeParams p, CancellationToken ct); }
public sealed class RepoDetailService { public CommandResult Get(GetRepoDetailParams p, CancellationToken ct); }
public sealed class CommitsService { public CommandResult Get(GetCommitsParams p, CancellationToken ct); }
public sealed class OpenPathService { public CommandResult Open(OpenPathParams p, CancellationToken ct); }
public sealed class RepoActionService { public RepoActionAvailabilityDto GetAvailability(RepoRef repo); public CommandResult Run(RunRepoActionParams p, SnapshotIndex index); }
```

## 附录 C：验收锚点对照

- 完整 `data-testid` 注册表：spec §10.1（60+ 项），实现逐项落 DOM；
- 完整 AC：spec §10.2 AC-UI-01..34；本文 §7.3 给执行补充与截图归档；
- CI 10 锚点：spec §8.1 与本文 §5.10（testid 不变、各 1 个）；
- Host 契约：本文 §3.4–§3.9 + `tests/contracts/bridge/*.json`；
- 安全：本文 §3.13 + `PathPolicyTests/OpenPathServiceTests`；
- 阶段：本文 §6.3 与 spec §11.3 的 S2/S3/S4/S6 对齐。

---

*本文只新增，不改代码；实施按 §6.3 的 P1→P4 顺序，每阶段以 §7.5 门禁 + spec AC-UI 验收。*
