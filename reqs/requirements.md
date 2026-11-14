# MBD 组件全生命周期 Git 工具 — 需求规格说明书（SRS v0.3 UI 重构修订）

> **本文档是需求的唯一维护源。** 原始 `MBD组件全生命周期Git工具.docx` 与 `software_workflow.drawio` 已冻结为证据归档（`reqs/archive/`），**不再修改**；后续所有需求变更只改本文件。
> 本文档由 6 路对抗式评审（需求质量 / Git 语义 / 架构可行性 / UI+CI / drawio 执行 / 红队复审）汇总重写并修订，评审证据见 `reqs/review/`，评审结论见 `reqs/review-report.md`；v0.2 冻结资产见 `reqs/v0.2/`；v0.2.1 按独立冻结复评 07（`reqs/review/07-freeze-review.md`）修订 SRS 侧阻断/高危项。
> **v0.3（2026-11-14）UI 基线变更**：用户提供 `docs/images/reference/main-ui-reference.png` 作为主界面唯一视觉基准，**旧 v0.2.1 §11（看板表格 + 右侧栏 + App Shell 左侧图标导航）与参考图冲突处一律以图为准**。§11 重构为「工作台 + 文件夹视图」；新增 FR-WS/FR-TREE/FR-FOLDER/FR-ACT/FR-CARD/FR-INFO 六组共 32 条 FR 与 AC-UI-01…AC-UI-14；10 个 CI 锚点全部给出新落点（10/10）。旧看板表格视图降级为 v1.1 备选（v1 入口不存在、不验收），旧 §11.1 导航与旧 §11.3 行布局**废弃**。

| 项目 | 内容 |
|---|---|
| 文档 ID | MBD-GIT-SRS |
| 版本 | **v0.3.1-r2（M2a/M2b 拆分修订 + M2a 出口 AC 定义/证据物补齐）** |
| 日期 | 2026-11-14 |
| 状态 | **v0.3.1 M2 阶段拆分（D-025）**：M2 拆为 **M2a 单仓安全交付** 与 **M2b 批量/作业/审计**，M2 整体出口 = M2a ∧ M2b。**M2a 当前状态 = 待复评（Gate 未通过，不可宣称已通过）**；M2b 未交付。v0.3 UI 基线（D-022）与 v0.2.1 其余冻结内容继续有效 |
| 替代 | v0.2.1（历史冻结证据见 `reqs/v0.2/`）；草案 docx（仅作历史证据） |
| 权威级别 | **本文件 > 任何口头/聊天记录 > 原 docx/drawio**；**UI 视觉冲突时：用户参考图 > 本文件 §11 > 旧 v0.2.1 §11** |

**需求状态图例**：`[已定]` 已决策可直接实现 · `[决策]` 本版决定的方案 · `[待确认]` 需用户拍板（附推荐默认） · `[设计建议]` 需求层不强制、由架构评审定稿 · `[v1]` 第一版范围 · `[v1.1]` 第二版 · `[v2]` 后续 · `[废弃]` 明确不做。
**与 P0/P1/P2 的关系**：P0/P1/P2 是交付优先级；`[v1]/[v1.1]/[v2]` 是版本归属；二者不是同一维度，FR 行内以 P 级为主，版本归属见各章说明。

---

## 0. 修订记录

| 版本 | 日期 | 变更 | 依据 |
|---|---|---|---|
| v0.1 | 2026-11-13 | 由草案 docx/drawio 重建：统一术语与状态模型、补齐数据/安全/验收/MVP 范围、CI 预留 10 锚点、修正 drawio 执行方案 | 5 路对抗评审 + 用户决策 D-001 |
| v0.1.1 | 2026-11-13 | 按红队复审修订：补 TTL/离线语义、7.4 聚合映射与兜底（15 聚合态）、7.6 状态×操作矩阵、11.2 全量色板（含深色）、8.1 表清单（16 表）、首启三阶段统一、AC 扩至 27 条+追溯矩阵、附录 C 状态矩阵/附录 D 设置与错误码、12.1 网络边界/恢复链路；关闭 OQ-02/03/04/08 等默认决策 | 红队复审 06 + 架构评审 03 |
| v0.2 | 2026-11-14 | 冻结候选：接入 `v0.2/ac-catalog.md`（162 AC/72 FR）、`v0.2/diagrams.md`（D1–D6）、`v0.2/fixtures/`（状态矩阵脚本）、`v0.2/freeze-checklist.md`；按红队 R-01…R-37 修订 SRS（严重度全序、写操作列权威化、TAG_DRIFT/LFS/NO_UPSTREAM 夹具裁决、CI 契约 componentId/数字 repoId、凭据口径、git 执行边界、枚举冻结、附录 D 补键） | AC 目录修订清单 + 图集/夹具核验 |
| v0.2.1 | 2026-11-14 | 按独立冻结复评 07 修订 SRS 侧阻断/高危项（AC/图集/夹具由并行角色回写）：C-02 `BINDING_ERROR` 按 `binding_cause` 拆分严重度（INVALID/AMBIGUOUS=1，仅缺仓=12）；C-04 §7.6 SYNC 强覆盖矩阵（DIRTY/DIVERGED/SYNCED）；C-05 §8.1 补 `job.skipped`、`job_item.started_at/finished_at`、`job_item.state=INTERRUPTED`、`sync_run.duration_ms`、`component.last_activity_at`，`job.state` 统一 `PARTIAL`，新增 §8.3 契约↔DDL 映射；H-01 远程未就绪中间态统一为 `UNKNOWN`+`incomplete=1`；H-02 冻结首扫进度阶段枚举与字段；H-03 新鲜度按表定义并补齐字段；H-04 hatched 对比度修正 + 对比度脚本入 M0 门禁；H-05 备份 ref 保留/清理/恢复；H-06 P2 功能 v1 仅保留入口不存在；C-06 里程碑出口与 §13.4 追溯矩阵改用 AC 目录新编号；M-09 凭据头统一 `Authorization: token <PAT>` | `reqs/review/07-freeze-review.md` §一致性缺陷 / §进入 M0 前必须修 |
| v0.3 | 2026-11-14 | **UI 以用户参考图为准重构**：重写 §11 主界面为工作台（菜单栏 / 标题区 / 左侧组件卡片列表 / 中间 2×2 四仓文件树 / 右侧选中仓信息面板 / 底部 PULL·FETCH·CHKOUT·COMMIT·PUSH 按钮条 / 状态栏），新增 §11.5 文件夹视图、§11.6 批量入口、§11.0.1 冲突裁决；旧看板表格 + 侧栏 + App Shell 左侧导航**废弃**，表格视图降级 v1.1（v1 入口不存在）；新增 §9.13–9.17 六组 32 条 FR 与 §13.1.1 的 AC-UI-01…AC-UI-14；10 个 CI 锚点全部给出新落点（10/10，CI 仍不参与聚合/卡片底色）；更新 §3 范围与 M0/M1/M2/M3 里程碑、§13 追溯矩阵、§10 性能/离线措辞、附录 D 设置键 | 用户参考图 `docs/images/reference/main-ui-reference.png` + `docs/ui-redesign-plan.md` + ADR D-022…D-024 |
| v0.3.1 | 2026-11-14 | **M2 拆分为 M2a 单仓安全交付 / M2b 批量·作业·审计（D-025）**：§3.3 M2 行拆为 M2a/M2b（出口标准与依赖）；§9.16/§9.17 标注 M2a 范围（取消/job/任务中心/仅重试失败项归 M2b）；§7.6 冻结按钮语义（DIVERGED Pull=❌、`—` 按禁用断言、EMPTY 首推归 M2b）；§13.1.1 修订 AC-UI-08/09/10 并新增 AC-UI-10a（COMMIT 确认条）/AC-UI-10b（PUSH 确认条）/AC-UI-10c（取消·job，M2b）/AC-UI-15（M2b 边界提示）；新增 §13.1.2 AC-SAFE-14（凭据作用域）/15（CAS）/16（ignored）/17（ref 校验）与 AC-OP-33（审计-lite）；§13.4 追溯矩阵与门禁更新。**当前状态：M2a 待复评，不写“已通过”** | `reqs/adr/D-025-m2a-m2b.md` + `docs/review-m2-req.md`（RQ-01…RQ-08/P0 清单）+ `docs/review-m2-code.md`（P0-1/P1-1/2/3）+ `docs/review-m2-ux-fixcheck.md`（NP0-1） |
| v0.3.1-r2 | 2026-11-14 | **M2a 出口 AC 定义与证据物补齐（Gate G-P0-4 回写；不放宽任何安全要求）**：新增 **§13.1.2**——AC-UI-10a/10b/10c/15、AC-SAFE-14…17、AC-OP-33 的完整 Given/When/Then 与可测断言、M2a 出口 13 条口径、单源矩阵夹具 `reqs/v0.2/fixtures/m2-matrix.json`（**Host 单测 + CDP 共用，禁止第三份手写期望**）、tooltip 冻结格式改 `原因 [E_*] → 替代动作`、COMMIT「对话框→确认条→执行」两步口径；§13.1.1 修正 AC-UI-08/09/10 拆分引用；§13.4 补 FR-ACT/FR-SAFE/FR-OPS 追溯行与 M2a 门禁注；新增 `docs/m2a-ac-report.md`（证据模板）与 `ui/scripts/cdp-m2a-selfcheck.mjs`（旧 `cdp-m2-selfcheck.mjs` 作废）。**状态仍为 M2a 待复评（修复中），不写“已通过”** | `docs/review-m2-gate-ux.md` §4.2/§4.4/G-P0-4 + `docs/review-m2-gate-security.md`（N-1/N-2）+ `reqs/adr/D-025-m2a-m2b.md` §2.2/§3/§5 |

**已确认决策（ADR）**

| ID | 决策 | 结论 | 来源 |
|---|---|---|---|
| D-001 | 组件唯一标识 | 仓库全名去掉末尾 `_Design/_Impl/_MIL/_SIL`（大小写不敏感）后的全名即组件 ID，如 `CF325F_ChanganSigProcgBascFct`；drawio 中“聚合成 CF325F”的写法**废弃** | 用户确认（A） |
| D-002 | CI 本期范围 | v1 只做 **UI 预留 + 数据契约 + Mock**，不接真实 CI API；CI 永不参与仓库主状态与卡片底色 | 用户要求 + 评审 |
| D-003 | 状态与颜色 | 废弃 docx 五状态/五色标与 drawio 六状态/另一套色标，改用本文件第 7 章状态机 + 第 11.2 节双通道色板 | 评审 RQ-02/RQ-03、UX-01/02 |
| D-004 | 文档权威 | 原 docx/drawio 降级为证据归档；本文件为唯一 SRS | 评审 RQ-09 |
| D-005 | 删除语义 | v1 删除只作用于**本地克隆目录**，禁止触达 Gitea 远端；默认软删除（`.trash`/回收站）可恢复 | 评审 RQ-08、G-P1-04 |
| D-006 | 凭据存储 | Token 禁止明文 JSON/写入 remote URL/URL query；**对外方案 = Windows Credential Manager**（DPAPI 加密文件作为等价实现细节）；token 只随 `Authorization: token <PAT>` header 发送（唯一写法，AC 按此断言） | 评审 RQ-08、G-P1-07、RT-18 |
| D-007 | 使用模式 | v1 单机单用户；同一数据目录单实例互斥 | 评审 RQ-10 |
| D-008 | 需求/方案分层 | 需求层只写可验收行为与约束；进程数、DB、技术栈等方案放第 12 章“设计建议” | 评审 RQ-05/RQ-18 |
| D-009 | 名称与术语 | 全文只用第 5 章术语表；“组件/Component”“仓库/Repo”“陈旧/STALE”不再混用 | 评审 RQ-17 |
| D-010 | 版本/基线 | 基线管理移入 v2；v1 仅支持 Tag 检出与基线快照记录（四仓同 Tag 判等） | 评审 G-P0-05、RQ-06 |
| D-011 | v1 架构（设计建议） | `Host` + `GitWorker` 两个 OS 进程（命名管道 JSON-RPC）；RemoteSync/LocalScan/StatusCalc 为 Host 内受监督 BackgroundService；SQLite WAL 单写者；聚合是 Host 内纯函数/视图 | 架构评审 ARC-01/03（推荐 Q1/Q3/Q4） |
| D-012 | 运行时 | .NET 10 LTS（.NET 8 已于 2026-11-10 终止支持）；WebView2 Evergreen + 离线安装器 | 架构评审 ARC-15 |
| D-013 | 本地目录布局 | 平铺单根：`<root>\<仓库全名>`，扫描非递归（深度 1）；根目录必须显式选择，禁止盘符根/用户目录根 | 用户确认（A） |
| D-014 | 比较基准 | 默认 `compare_ref = upstream`（已设置 upstream 时）否则 `refs/remotes/{remote}/{同名分支}`；四仓默认分支不同 → `MIXED_BRANCH`；不一致可逐组件覆盖（v2 落 UI） | 关闭 OQ-02（默认口径；如组织统一用 main/develop 可改） |
| D-015 | 缺仓策略 | 四仓齐全为正常；缺 1–3 仓 = `BINDING_ERROR`（`binding_cause=INCOMPLETE`、`component.incomplete=1`），组件仍可见、已有仓可操作；缺仓为**中性低严重度**（`severity_rank=12`），不参与“异常优先”的最高排序；含 INVALID/解析歧义/未绑定的 BINDING_ERROR 才按 `severity_rank=1` | 关闭 OQ-03（默认口径；若缺仓属常态需再调） |
| D-016 | 首启编排 | 三阶段：**本地发现 → 远程对账 → 状态计算**；远程阶段可跳过进入只读降级，远程就绪后自动重判本地仓（不得把“远程未就绪”误判为 INVALID/MISSING 并永久保留） | 关闭 P0-09/RT-14 |
| D-017 | 删除与恢复 | 仅本地、软删除 `.trash` 保留 **30 天**，提供恢复入口；备份 ref `refs/backup/*` 保留 ≥30 天（`backup.retentionDays` 可配，见附录 D）；扫描必须排除 `.trash` | 关闭 OQ-04 |
| D-018 | 兼容基线 | **Windows 11 x64 为基线**；Windows 10 22H2 仅 ESU 环境（需 OQ 确认是否支持） | 关闭 RT-22（默认口径） |
| D-019 | git 依赖 | 检测 Git for Windows ≥2.40（含 LFS/GCM）并引导安装；离线环境再评估捆绑 PortableGit（GPL 义务） | 关闭 OQ-14（默认口径） |
| D-020 | 组件严重度 | 采用 `severity_rank` 0–14（§7.4），与聚合优先级分离；工作台默认按 severity_rank 升序、再组件 ID 排序 | 关闭 OQ-21 |
| D-021 | DIRTY 口径 | 默认 `dirty.includeUntracked=false`：仅 tracked 改动升 DIRTY；untracked 用 flag 展示 | 关闭 OQ-22 |
| D-022 | UI 视觉基准 | **以用户参考图 `docs/images/reference/main-ui-reference.png` 为唯一视觉基准**；旧 SRS §11 与图冲突时**一律以图为准**。图是示意图，像素不精确：尺寸/比例按 §11.0.3 的区间口径，验收按 AC-UI-01 的 CDP/screenshot 断言，不要求像素级复刻 | 用户要求（2026-11-14） |
| D-023 | 旧看板处置 | 旧「看板表格 + 右侧详情栏 + App Shell 左侧图标导航」**废弃**为主界面；表格视图**降级 v1.1 备选**（v1 不提供入口、不验收表格）；工作台左侧「组件卡片列表」为唯一默认列表视图；主界面区域顺序以参考图为准 | 用户参考图 + `docs/ui-redesign-plan.md` |
| D-024 | 单仓操作阶段边界 | 底部 5 个单仓按钮（PULL/FETCH/CHKOUT/COMMIT/PUSH）阶段边界固定：**M0 禁用占位 → M1 只读、5 按钮均不可执行（含 FETCH）→ M2 按 §7.6 接入**。v1 不提供底部按钮条以外的单仓写入口（文件树/卡片右键只读项）；每个按钮必须可解释地禁用，禁止「可点但无效果」 | 用户参考图 + M1 只读闭环口径 |
| D-025 | **M2 阶段拆分（M2a/M2b）** | M2 拆为 **M2a 单仓安全交付**（五键 + 安全四件 + 确认条 + 焦点 + tooltip 三段式/单源矩阵 + 审计一条 + 真实数据；出口 AC 见 §13.1.1/§13.1.2）与 **M2b 批量·作业·审计**（批量写、job/JOB_UPDATED/取消/重试、备份/回滚、任务中心、审计完整表、写前 journal）。**M2 总出口 = M2a ∧ M2b**；M2b 未完成不阻塞 M2a 复评，但 M2a 须以「M2b 未接入」明确提示批量/任务中心入口；**M2a 不得含可点无效果或零确认写，不得以拆分降低任何安全要求**。完整决策与验收脚本见 `reqs/adr/D-025-m2a-m2b.md` | M2 三路对抗评审（`docs/review-m2-req.md` RQ-06 + §10.2、`docs/review-m2-code.md` P0-1、`docs/review-m2-ux-fixcheck.md` NP0-1） |

---

## 1. 背景与问题定义

业务以「组件」为单位管理 MBD（Model-Based Design）汽车嵌入式模型，但 Git 以 **4,000+ 个独立仓库**存储，二者天然割裂：

- 一个业务组件固定对应 4 个生命周期仓库：`Design` / `Impl` / `MIL` / `SIL`；
- 仓库命名形如 `CF325F_ChanganSigProcgBascFct_Design`，同一组件四仓共享前缀；
- 传统 Git 客户端只能按仓库操作，无法做组件级批量操作、版本联动、状态聚合；
- 开发者需要人工在多个仓库/网页间确认“我的组件现在是什么状态、CI 过没过、四仓版本是否对齐”。

**本工具解决的核心问题**：把 4,000 个仓库折叠成「组件」视角——**工作台聚合状态 + 组件级批量操作 + 单仓精细操作**，并以 Gitea 远程数据 + 本地真实 Git 状态双源兜底。

---

## 2. 产品定位、目标与非目标

**定位**：Windows 桌面客户端（WebView2 网页级界面），面向 MBD 组件开发的 **Git 聚合操作台**。

**v1 目标（可度量）**

1. 用户不需要打开 4 个 Git 客户端即可完成组件的 Clone / 拉取 / 切分支 / 切 Tag / 强制同步 / 删除；
2. 工作台在 **1440×900、100% 缩放下按 §11.0 的 7 个区域完整呈现**（菜单栏/标题区/左组件卡片列表/中 2×2 四仓文件树/右信息面板/底部按钮条/状态栏）且**无页面级横向溢出**；左侧组件列表默认排序 = `severity_rank` **升序**（数值越小=严重度越高）+ 组件 ID 升序；
3. 任何破坏性操作都有“将丢失什么”的预览、自动备份与可恢复路径；
4. CI 位置在 UI 与数据模型中**一次预留到位**，后续接入不改工作台布局/卡片/聚合语义；
5. 「工作台 ↔ 文件夹视图」一键切换（≤200ms）且两视图对同一组件/仓库**状态完全一致**（同一次快照，禁止各自计算）。

**非目标（v1 明确不做）**：见 3.2。

---

## 3. 范围与优先级

### 3.1 v1 In-Scope（MVP，必须交付）

| 域 | 内容 |
|---|---|
| 配置与认证 | Gitea 地址/组织/本地根目录/PAT/代理配置；连接测试；Token 过期重登 |
| 组件发现 | 远程仓识别、组件绑定、本地目录发现、未绑定仓库清单、组件缺仓标记 |
| 远程元数据 | 组织仓库清单、默认分支、分支列表、Tag 列表（含 tag→commit）同步；分页/限流/失败重试/覆盖率展示 |
| 本地状态 | 分支/detached/HEAD/dirty/untracked/upstream/ahead/behind/操作中间态扫描；新旧数据判定 |
| 状态聚合 | 单仓主状态 + 正交标记 + 组件聚合优先级；UNKNOWN/STALE 不许假绿 |
| 工作台 | **主界面以参考图为准（§11.0）**：菜单栏 + 标题区 + 左侧组件卡片列表 + 中间 2×2 四仓文件树 + 右侧选中仓信息面板 + 底部单仓按钮条 + 状态栏；搜索/筛选/排序/虚拟滚动/多选；异常优先 |
| 文件夹视图 | 工作区根 → 组件目录 → 四仓目录 → 文件目录树；展开/双击打开；按状态/RepoType/本地改动过滤；与工作台一键切换（§11.5） |
| 文件树 | 四仓只读文件树（懒加载/排序/展开/双击默认程序打开/状态标记/路径安全，只读无写入口）；单仓详情内文件 Tab 复用同一规则（§9.14） |
| 详情 | 组件详情抽屉（4 仓总览）、单仓详情抽屉（概览/历史/差异/文件/CI 占位）；双击组件卡片进入，不替换工作台布局 |
| 批量操作 | Clone（幂等/预检/部分失败）、Pull、Fetch、Checkout 分支、切 Tag（detached）、受控 SYNC、本地删除、CREATE 分支（dry-run）；入口=菜单栏操作菜单 + 组件卡片右键 + 卡片区顶部批量条（§11.6） |
| 单仓操作 | 底部按钮条 **PULL / FETCH / CHKOUT / COMMIT / PUSH**（作用于当前聚焦仓；前置/确认/失败/进行中规则见 §9.16；M1 只读禁用，M2 接入） |
| 任务与安全 | 任务中心（进度/取消/重试/逐仓结果）、破坏性确认、备份 ref、审计日志 |
| 首启 | 配置向导 + **三阶段首扫进度（`LOCAL_DISCOVERY` 本地发现 → `REMOTE_RECONCILE` 远程对账 → `STATUS_CALC` 状态计算，进度事件含 `{stage,total,current,cancellable}`，可跳过远程降级）** + 就绪门控与自动重判 |
| 运维 | 日志、诊断导出、单实例、DB 迁移、设置页、通知中心 |
| CI 预留 | 10 处 UI 锚点 + `ICiProvider` Mock + 数据契约 + feature flag（不接真实 API） |

### 3.2 v1 Out-of-Scope（必须显式标注，UI 可占位）

| 项 | 处理 | 目标版本 |
|---|---|---|
| 真实 CI API 接入/告警 | 仅占位 + Mock + 契约 | v2 |
| 基线管理（四仓同 Tag 冻结/漂移检测/快照管理 UI） | v1 仅记录快照与判等，不做管理界面 | v2 |
| 远端仓库创建/删除/改名 | 明确不做 | 不计划 |
| 跨组件“全组织”批量写操作 | v1 仅对筛选结果显式多选执行 | v2 |
| 多人共享同一台机器 / 多用户数据隔离 | 单机单用户 | 待定 |
| 自动更新 | 手动安装包升级 | v1.1 |
| SYNC 的全自动无人值守模式 | 必须人工确认 | 不计划 |
| SSH/OAuth/SSO 认证 | PAT only | v2 |
| 旧看板「表格视图」为主界面 | **降级 v1.1 备选**：v1 不提供入口、不验收；工作台组件卡片列表为唯一默认视图（D-023） | v1.1 |
| 文件树/文件夹视图的写操作（新建/重命名/删除/移动/拖放/上传/在树内编辑） | 明确不做：v1 文件树**严格只读**；外部编辑走系统默认程序/资源管理器 | 不计划 |
| 工作台多窗口/多标签页、可停靠面板 | 明确不做：单窗口、固定 7 区域；左右面板仅允许宽度拖拽 | 不计划 |

> **P2 口径（v0.2.1 冻结，H-06）**：P2 功能（FR-CFG-06 / FR-DETAIL-05 / FR-REPO-04 / FR-OPS-07）在 v1 **仅保留入口不存在/不可用**（不暴露可执行入口、不实现完整行为）；完整行为归 v1.1，M0 只冻结“入口不存在/不可用”断言（OQ-24）。**v0.3 补充（D-023）**：旧看板表格视图同按 P2 口径处理——v1 入口不存在、不验收；v1.1 如恢复须复用工作台同源数据与 §11.2 token，不得引入第二套状态计算。

### 3.3 里程碑（建议）

| 里程碑 | 交付 | 出口标准 |
|---|---|---|
| M0 骨架与冻结 | **新工作台骨架（参考图 7 区域：菜单栏/标题区/组件卡片列表/2×2 四仓文件树/右侧信息面板/底部按钮条/状态栏）+ 文件夹视图骨架 + 视图切换 + 设计 token（§11.2）+ 10 个 CI 占位锚点（新落点）+ Mock 数据 + 假 Gitea**；底部 5 按钮**禁用占位**（D-024）；状态矩阵夹具（附录 C）、AC 全量冻结、D1–D6 图集、测试资产（Gitea 单二进制 fixture/`IClock`/基准脚本） | 工作台骨架可跑；**CDP 布局断言：7 区域存在且顺序正确、1440/1280 无页面级横向溢出、CI 锚点 10/10 命中**；AC 追溯矩阵 100% 覆盖 FR（含 AC-UI-01…14）；夹具清单签收 |
| M1 只读闭环 | 配置/认证、远程同步、本地发现、状态计算、**工作台接真数据（组件卡片/四仓文件树/文件夹视图/右侧信息面板/状态栏）+ 详情抽屉 + 三阶段首扫与就绪重判**；底部 5 按钮**全部只读禁用**并提供禁用理由（D-024）；文件树/文件夹视图只读可展开、双击打开 | 附录 C 状态矩阵全绿；工作台首屏 P95 ≤1.5s（NFR-PERF-02/09）；**AC-UI-01/03/04/05/06/07/09/11/12/13/14 通过**（布局、文件树、文件夹视图、视图切换、M1 只读边界、信息面板、CI 锚点、二级入口、旧布局废弃） |
| M2a 单仓安全交付 | **底部单仓按钮条 PULL/FETCH/CHKOUT/COMMIT(staged-only)/PUSH 真实 Host 接入**（PULL `--ff-only`、COMMIT staged-only/message 1–200、PUSH 禁 force 且 `NO_UPSTREAM` 走“发布分支并建 upstream”确认；EMPTY 首推归 M2b，M2a 构建按空仓无提交禁用；CHKOUT 选择器来自 `remote_ref` + 搜索 + `stash -u` 选项）+ **安全四件**（凭据作用域、写前 read-through+CAS、ignored 文件保护、ref 校验）+ **COMMIT/PUSH ≤48px 确认条**（含 `Alt+5` 同路径）+ **焦点闭环** + **tooltip 三段式与 Host 单源 14 态矩阵** + **操作审计一条（audit-lite）** + **真实数据验收**；批量/任务中心入口明示“M2b 未接入” | **M2a 出口 AC 全绿（共 13 条）**：`AC-UI-08`（单源矩阵+tooltip）、`AC-UI-09`（阶段门禁）、`AC-UI-10`（前置/失败/进行中/回写）、`AC-UI-10a`（COMMIT 确认条）、`AC-UI-10b`（PUSH 确认条）、`AC-UI-11`、`AC-UI-13`（焦点复验）、`AC-UI-15`（M2b 边界）、`AC-SAFE-14/15/16/17`、`AC-OP-33`；评审 P0 闭环（`docs/review-m2-req.md` P0-01…P0-05/P0-08/P0-11、`docs/review-m2-code.md` P0-1/P1-1/P1-2/P1-3、`docs/review-m2-ux-fixcheck.md` NP0-1）；CDP+Host+fixture 可自动复跑；**不得声称 FR-ACT-05/06 全量通过**（取消/job/任务中心归 M2b） | 依赖 M1 出口通过；依赖 `reqs/adr/D-025-m2a-m2b.md` 冻结的口径与夹具 `reqs/v0.2/fixtures/m2-matrix.json` |
| M2b 批量/作业/审计 | 批量 Clone/Pull/Checkout/Tag/SYNC/Delete/Restore/Create + 受控 SYNC 备份 ref + `.trash` 恢复 + 任务中心（`job`/`JOB_UPDATED`/`jobId`/取消/仅重试失败项/持久化）+ 审计完整表（查询/导出/≥90 天）+ 写前 journal + 崩溃 reconcile + FR-REPO-05 其余修复类（Repair/Re-clone、detached 建分支、EMPTY 首推建 upstream）+ 离线降级 | 写操作 AC 通过（`AC-OP-01…AC-OP-32`、`AC-REPO-01…AC-REPO-11`、`AC-SAFE-01…AC-SAFE-13`、`AC-UI-10c`）；故障注入报告通过；14 态矩阵在 M2b 构建复验；**M2 总出口 = M2a ∧ M2b，缺一不得宣称 M2 交付** | 依赖 M2a 出口通过（矩阵单源/审计链路/错误码体系是 M2b 前置） |
| M3 收口 | 通知/日志/诊断/设置、性能与可用性、打包、验收 | **AC 基线冻结版本全绿**；P0 缺陷清零；D1–D6 与需求/代码追溯一致 |

> **M2a/M2b 拆分口径（D-025，v0.3.1）**：
> 1. **M2 整体出口 = M2a 出口 ∧ M2b 出口**；M2a 通过 ≠ M2 交付完成，M3 收口依赖 M2b。M2a 当前状态 = **待复评（Gate 未通过）**，不得写“已通过”。
> 2. M2a 与 M2b 之间**不得用 Mock/单测数量替代真实证据**；M2a 验收须在真实 Host + 真实 Gitea（或单二进制夹具）上执行五键动作。
> 3. M2b 未完成**不阻塞** M2a 复评；但 M2a 构建必须把批量/任务中心入口明示为“M2b 未接入”（AC-UI-15），且不得因此移除任何安全项。
> 4. 详细决策、出口 AC 与逐项验收脚本见 `reqs/adr/D-025-m2a-m2b.md`；本表与 ADR 冲突时以 ADR 的最新决策为准，并回写本表。

---

## 4. 角色与核心用例

**角色**：组件开发者（v1 唯一角色）。假设其对本机有读写权限、持有 Gitea PAT。

| UC | 用例 | 频率 |
|---|---|---|
| UC-01 | 首次配置并完成首扫，进入工作台 | 一次/机器 |
| UC-02 | 日常巡检：查看组件状态、异常优先处理 | 每日多次 |
| UC-03 | 新组件接入：输入组件标识 → 四仓批量 Clone → 校验状态 | 每周 |
| UC-04 | 同步到某个版本：四仓批量切 Tag / 切分支 / 受控 SYNC | 每周 |
| UC-05 | 开发中：单仓 Pull/Push/Commit，外部 IDE 操作后工作台自动兜底识别 | 每日多次 |
| UC-06 | 清理：组件本地删除（可恢复） | 每月 |
| UC-07 | 断网/Token 过期/Gitea 不可达：降级只读，恢复后自动补齐 | 偶发 |
| UC-08 | 排障：查看任务失败明细、日志、导出诊断包 | 偶发 |
| UC-09 | 浏览：在工作台中间 2×2 四仓文件树中分层展开、双击打开文件/文件夹；切到文件夹视图按工作区目录巡览 | 每日多次 |
| UC-10 | 单仓操作（M2）：在文件树选中一个仓 → 底部 PULL/FETCH/CHKOUT/COMMIT/PUSH 中选一个执行并跟踪结果 | 每日多次 |

---

## 5. 术语表（唯一权威用词）

| 术语 | 英文/枚举 | 定义 | 禁用别名 |
|---|---|---|---|
| 组件 | Component | 一个业务组件，ID = 仓库名去掉生命周期后缀后的全名 | Component、模块、大仓 |
| 仓库 | Repo | 一个 Git 仓库，属于且仅属于一个组件的一个生命周期类型 | Repository、库 |
| 生命周期类型 | RepoType | `DESIGN` / `IMPL` / `MIL` / `SIL` 四类 | 阶段、用途 |
| 组件标识 | component_id | 见 6.1；UI 中文显示名默认等于 component_id | 前缀、聚合名 |
| 工作区根 | workspace_root | 用户配置的本地根目录；仓库克隆为其直接子目录 | 本地目录、根 |
| 主状态 | primary_state | 单仓的唯一主状态（见 7.2） | 状态色、健康度 |
| 正交标记 | flag | 不参与主状态优先级、但影响 UI 与阻断逻辑的布尔标记（见 7.3） | 子状态 |
| 组件状态 | component_state | 四仓主状态 + 组件级覆盖态的聚合结果（见 7.4） | 整体状态 |
| 数据新鲜度 | freshness | 上次成功扫描/抓取距今时间；超 TTL 标记 STALE | 实时、秒级 |
| 任务 | job | 一次用户发起的写操作，包含逐仓子任务 | 操作、命令 |
| 基线快照 | baseline_snapshot | 某 Tag 在四仓各自对应的 commit SHA 记录（v1 只记录不管理） | 版本 |
| 工作台 | Workbench | 参考图定义的主界面：菜单栏/标题区/组件卡片列表/2×2 四仓文件树/右侧信息面板/底部按钮条/状态栏（§11.0）；全文「看板」未特别说明时即指工作台的组件卡片列表区 | 主界面、Dashboard |
| 组件卡片 | Component Card | 工作台左列展示一个组件聚合状态的卡片（§11.3）；**不是**旧看板表格行 | 看板行 |
| 文件树 | File Tree | 工作台中间按仓库分格的只读目录树（§9.14）；四仓各一棵，2×2 排列 | 目录浏览器 |
| 文件夹视图 | Folder View | 以 workspace_root 为根、按组件/仓库/文件层级组织的只读树视图（§11.5），与工作台一键切换 | 工作区树 |
| 信息面板 | Info Panel | 工作台右侧窄栏（18%），显示当前聚焦仓的基本信息、构建/CI 占位与最近操作结果（§11.3） | 详情栏 |
| 表格视图 | Table View | 旧看板表格视图，**v1.1 备选、v1 不提供入口**（D-023） | 旧看板 |

---

## 6. 组件与仓库识别规则（核心，v1 必须严格实现）

### 6.1 组件标识 `[已定][决策 D-001]`

```
仓库全名 = <component_id>_<RepoType>
RepoType ∈ {Design, Impl, MIL, SIL}（匹配大小写不敏感）
```

- 规则：**从最后一个下划线切分**，只接受末尾为四类后缀之一、且**剩余前缀不以四类生命周期词结尾**的仓库；剩余部分即 `component_id`。
  - `CF123A_ShortName_Design` → component_id = `CF123A_ShortName`
  - `CF325F_ChanganSigProcgBascFct_Impl` → component_id = `CF325F_ChanganSigProcgBascFct`
  - `X_SIL_Design` → 前缀 `X_SIL` 以 `_SIL` 结尾，**不自动绑定**，进入未绑定清单（避免把 `X_SIL` 误当组件）
- 同 `component_id` 的 4 个仓库 = 一个组件的 4 仓；仓库名大小写必须按 Gitea 规范唯一。
- 若仓库名出现 `_Design2`、`_SIL_Design`、纯 `_design` 但大小写冲突等 → 不参与绑定，进入「未绑定仓库」清单并告警，**禁止静默丢弃**。
- 组件显示名 = `component_id`（v1 不做别名表）；仓库改名/迁移时 component_id 可能变化，必须生成 rename 迁移事件（见 FR-SYNC-05）。

### 6.2 绑定完整性

| 情况 | 组件状态标记 | 处理 |
|---|---|---|
| 远程恰好 4 仓且均可识别 | 正常 | 正常聚合 |
| 远程缺 1–3 仓 | `BINDING_ERROR`（`binding_cause=INCOMPLETE`，中性低严重度 `severity_rank=12`） | 仍显示组件与已有仓，缺失仓显示“缺失”；批量操作只作用于存在仓并明确提示 |
| 远程 >4 仓（重名/历史残留） | `BINDING_ERROR`（`binding_cause=AMBIGUOUS`，`severity_rank=1`） | 列出候选；v1 **不做收纳**（binding_override 表移 v2），交用户改名后重扫 |
| 本地目录不是 git 仓 / remote 不匹配 / 嵌套仓库 / 路径冲突 | `INVALID`（单仓）→ 计入组件 `BINDING_ERROR`（`binding_cause=INVALID`，`severity_rank=1`） | 该仓不参与正常状态计算，但**计入组件聚合**并在异常清单展示 + 修复动作（“不纳入聚合”的旧说法作废，与 7.4 一致） |
| 远程仓库已删除但本地仍在 | 标记 `REMOTE_GONE`（正交标记） | 禁止自动 push/覆盖，提示清理 |

> 草案“命名全局唯一，无需程序校验”被否决：客户端**必须**解析并暴露无法归类的仓库。

### 6.3 本地发现规则 `[已定][决策 D-013]`

- **布局**：平铺单根 `<workspace_root>\<仓库全名>`，扫描直接子目录（非递归，深度 1）；
- **根目录约束**：必须显式选择业务目录；**禁止**盘符根（`D:\`）、用户目录根（`C:\Users\xxx`）、Windows 目录等；多根目录为 v1.1 扩展（接口预留但 v1 只启用一个根）；
- 目录名必须等于仓库全名（Windows 不区分大小写）；目录内容校验：`git rev-parse --show-toplevel` 等于该目录，且 `origin` URL 规范化后 host/org/name 与 Gitea 同名仓一致；
- 只读探测用参数数组执行 `git -C <path> remote get-url origin`；不得因 `safe.directory` 静默放宽，需提示；
- 跳过 reparse point（junction/symlink/OneDrive 占位）与网络盘/UNC：**不纳管**（不进入组件），记 `scan_issue` 告警；若已纳管后路径变为不可写，则写操作拒绝、仅只读展示。不纳管 submodule（标 `submodule` flag，写操作拒绝）；
- 当前配置的根目录不合法/不可访问时，首扫进入降级态并提示，不静默扫描其他目录。

---

## 7. 状态模型（替代草案两套枚举）

### 7.1 数据权威与新鲜度（关键裁决）

- **比较基准 `compare_ref`（D-014）**：优先本地分支已配置的 `upstream`；否则 `refs/remotes/{remote}/{本地当前分支同名分支}`；四仓默认分支不同 → 组件 `MIXED_BRANCH`。7.2 中所有“ref”均指此 `compare_ref`，禁止使用未绑定的裸“ref”。
- **关系判定权威**：fetch 之后的 `compare_ref`（remote-tracking ref 对象）；**存在性/发现/元数据权威**：Gitea API（数字 `repo_id` 为主键，仓库名仅展示）；两者不一致 → 记 `DRIFT` 告警，不猜测。
- **TTL 表**（超期即对应数据不可信）：

  | 数据 | 采集频率 | TTL |
  |---|---|---|
  | 本地指纹/深扫 | 15s / 变化触发 | 90s |
  | remote-tracking ref（fetch） | 10min 巡检 / 写前强制 | 20min |
  | 远程元数据（清单/refs） | 10min / 30min / 每日全量 | 7h |
  | CI（v1 关闭） | — | 30min（v2） |

- **离线可判定状态集**（不依赖 fetch，可只读展示）：`INVALID`、`MISSING`、`EMPTY`、`UNMERGED`、`IN_PROGRESS`、`DETACHED_TAG`、`DETACHED_COMMIT` 的本地事实部分 → 照常判定；`BEHIND/AHEAD/DIVERGED/SYNCED` 等关系类状态在 `compare_ref` 超 TTL 或 fetch 失败时 → `UNKNOWN`，并显示“远程数据 X 前”；UI 必须区分“本地数据新鲜 / 远程数据过期”。
- 所有状态必须带 `calc_at`、`last_success_at`、`error_code`、`data_source`；禁止沿用旧值冒充当前状态。
- Gitea compare API 仅作兜底：仅当 commit 已存在于服务端、且本地对象缺失时使用；未推送 commit 禁止调用。

### 7.2 单仓主状态表（互斥，优先级 0 最高）

> 前置：**关系类状态**（BEHIND/AHEAD/DIVERGED/SYNCED 及 DIRTY 的关系部分）需 `compare_ref` 在 TTL 内且 fetch 成功，否则 `UNKNOWN`；**本地事实类状态**（INVALID/MISSING/EMPTY/UNMERGED/IN_PROGRESS/DETACHED_*）离线可判定，按本地事实给出，不得因离线一律压成 UNKNOWN。
>
> **远程未就绪（H-01）**：首扫 `REMOTE_RECONCILE` 未完成或远程阶段被跳过时，本地已识别仓**不得**判 `INVALID`/`MISSING`，正确态为 `UNKNOWN` 且 `component.incomplete=1`；远程就绪后自动重判（D-016）。

| 优先级 | 主状态 | 精确触发条件 | UI 文案 | 允许写操作 |
|---|---|---|---|---|
| 0 | `UNKNOWN` | fetch/scan 失败或超 TTL、git 不可用、对象库损坏、shallow 且无法判定且 API 兜底失败 | 状态未知（数据 X 前） | 见 7.6（仅重试扫描；Delete ⚠️） |
| 1 | `INVALID` | 目录非 git 仓、`--show-toplevel` 不匹配、origin URL 不匹配、路径大小写冲突、reparse point 指向外部 | 仓库绑定异常 | 见 7.6（Repair；Delete ⚠️） |
| 2 | `MISSING` | 组件目录不存在或为空 | 缺失（未克隆） | Clone |
| 3 | `EMPTY` | 仓库存在但 HEAD unborn（无首个 commit） | 空仓库 | 初始化/推送首个 commit |
| 4 | `UNMERGED` | `git ls-files -u` 非空（index 有未解决冲突） | 冲突未解决 | 禁用自动 SYNC/覆盖 |
| 5 | `IN_PROGRESS` | 存在 `MERGE_HEAD`/`rebase-merge`/`rebase-apply`/`CHERRY_PICK_HEAD`/`BISECT_LOG` | 合并/变基未完成 | 禁用写操作 |
| 6 | `DETACHED_TAG` | HEAD 分离且 `git tag --points-at HEAD` 非空 | 已检出基线 tag（只读态） | 允许建分支 |
| 7 | `DETACHED_COMMIT` | HEAD 分离但无 tag 指向 | 游离 commit | 允许建分支 |
| 8 | `DIVERGED` | `merge-base --is-ancestor HEAD ref` 与反向均为 1（双方各有新提交） | 本地与远程分叉 | 查看差异/建分支/手动处理 |
| 9 | `BEHIND` | 本地是远程祖先且 HEAD≠ref（`--is-ancestor HEAD ref` 返回 0） | 落后 N 个提交 | 受控 SYNC/Pull |
| 10 | `AHEAD` | 远程是本地祖先且 HEAD≠ref | 本地超前 N 个提交 | Push |
| 11 | `NO_UPSTREAM` | 分支无可用 upstream：`LOCAL_ONLY`（远程无同名分支）/ `UNSET_BUT_EXISTS`（远程有但未设 tracking）/ `UPSTREAM_GONE`（远程已删） | 仅本地分支 / 未跟踪 / 远程分支已删除 | 修复 tracking / 推送 |
| 12 | `DIRTY` | HEAD==upstream 且工作区有 tracked 改动或（按配置）untracked/stash | 已同步但有未提交改动 | Push 前需提交，SYNC 阻断 hard 覆盖 |
| 13 | `SYNCED` | 在分支、upstream 存在、HEAD==upstream tip、工作区干净 | 完全同步 | 正常操作 |

> `CONFLICT` 一词废弃：草案的“分叉”=`DIVERGED`；真正的 index 冲突=`UNMERGED`。`NEW_BRANCH` 拆为 `NO_UPSTREAM` 三子型。
>
> **“允许写操作”列只作摘要，权威见 7.6 状态×操作矩阵**；UNKNOWN/INVALID 下的 Delete 为 ⚠️（dry-run + 确认 + `.trash` 可恢复）。

### 7.3 正交标记（不参与主状态优先级）

| 标记 | 判定 | 作用 |
|---|---|---|
| `dirty_tracked` | `git --no-optional-locks diff-index --quiet HEAD --` 非 0 | 阻断覆盖；徽标 |
| `untracked` | `git ls-files --others --exclude-standard` 非空 | 提示；默认不清理 |
| `stashed` | `git stash list` 非空 | 提示 SYNC 不动 stash |
| `shallow` | `rev-parse --is-shallow-repository` | 关系判定降级/API 兜底 |
| `lfs_missing` | 含 LFS 但工作区为 pointer/对象缺失 | 红色告警：文件不可用 |
| `reparse_point` | junction/symlink/OneDrive 占位 | 阻止删除/写操作 |
| `stale` | 超 TTL 未成功扫描 | 主状态强制 UNKNOWN，UI 斜纹 |
| `busy` | `.git/index.lock` 存在或外部 git 正在操作 | 写操作返回 BUSY，**禁止删锁** |
| `remote_gone` | Gitea 已删除该仓 | 禁 push/覆盖，提示清理 |
| `tag_moved` | 本地/远程同名 tag 指向不同 commit | 基线完整性告警 |
| `mixed_branch`（组件级） | 四仓当前分支名不一致 | 卡片角标 + 四仓对照 |

### 7.4 组件聚合（单仓主状态 → 组件状态，全序 + 兜底）

`组件状态 = max(组件覆盖态, 四仓主状态)`；任何一仓 UNKNOWN ⇒ 组件不得显示 SYNCED。**聚合优先级**决定卡片显示哪个组件态；**严重度/排序**独立决定工作台“异常优先”排序（解决“缺失是中性却排在分叉前”的矛盾）。

| 聚合优先级 | 组件状态 | 触发（四仓映射） | 展示 | severity_rank |
|---|---|---|---|---|
| 0 | `OP_PARTIAL_FAILED` | 存在未完成/失败的批量 job（用户确认后清除） | 红「部分失败 n/4」+ 重试入口 | 0 |
| 1 | `BINDING_ERROR` | 仓数≠4（`binding_cause=INCOMPLETE`）/ 解析歧义或未绑定（`binding_cause=AMBIGUOUS`）/ 任一仓 INVALID（`binding_cause=INVALID`） | 红「绑定异常」+ 清单 | 1（含 INVALID/解析歧义/未绑定）/ **12（仅缺仓 `incomplete=1`，中性低）** |
| 2 | `UNKNOWN` | 任一仓 UNKNOWN/STALE/无法计算；**兜底默认** | 灰紫「部分状态未知」+ 哪些仓 | 2 |
| 3 | `MISSING` | 任一仓 MISSING | 灰「缺失 n/4」 | 12 |
| 4 | `BLOCKED_LOCAL` | 任一仓 UNMERGED / IN_PROGRESS | 红「本地冲突/操作未完成」 | 3 |
| 5 | `EMPTY` | 任一仓 EMPTY | 灰「空仓库」 | 13 |
| 6 | `TAG_DRIFT` | 四仓 tag 不一致 / 缺 tag / 与快照不符（**无条件计算**，不依赖“基线模式”） | 橙「基线不一致」+ 逐仓 tag | 5 |
| 7 | `DIVERGED` | 任一仓 DIVERGED | 橙「分叉」+ 需 Merge/Rebase | 4 |
| 8 | `BEHIND` | 任一仓 BEHIND | 黄「落后」 | 6 |
| 9 | `AHEAD` | 任一仓 AHEAD | 蓝「超前」 | 7 |
| 10 | `DETACHED` | 任一仓 DETACHED_TAG / DETACHED_COMMIT | 青「基线/游离检出」 | 10 |
| 11 | `MIXED_BRANCH` | 四仓分支名不一致且无更高态 | 青「分支不一致」 | 9 |
| 12 | `NEW_BRANCH` | 任一仓 NO_UPSTREAM（LOCAL_ONLY/UNSET_BUT_EXISTS/UPSTREAM_GONE） | 青「仅本地/未跟踪/远程已删」 | 11 |
| 13 | `DIRTY` | 任一仓 DIRTY | 绿+变更徽标 | 8 |
| 14 | `SYNCED` | 四仓均 SYNCED | 绿「同步」 | 14 |

- **两个维度分离**：`聚合优先级` 决定卡片显示哪个组件态（数值越小越优先）；`severity_rank` 决定工作台默认排序（数值越小越靠前）。二者不是同一维度，禁止混用。聚合优先级与评审 02 表 C 对齐（MISSING 先于 BLOCKED_LOCAL；BLOCKED_LOCAL 覆盖 UNMERGED/IN_PROGRESS）。
- 裁决：`BINDING_ERROR`(1) 与 `UNKNOWN`(2) 并存时显示 BINDING_ERROR，但必须同时列出 UNKNOWN 的仓。
- **映射完整性**：7.2 的 14 个主状态全部有落点（UNMERGED/IN_PROGRESS→BLOCKED_LOCAL；DETACHED_TAG/DETACHED_COMMIT→DETACHED；NO_UPSTREAM→NEW_BRANCH；EMPTY→EMPTY）；**任何未命中组合必须落到 UNKNOWN**，禁止空白/假绿。
- **`binding_cause`（§7.4 冻结判别字段）**：`INCOMPLETE`（仅缺 1–3 仓、`component.incomplete=1`）→ `severity_rank=12`（中性低，不参与“异常优先”最高级排序）；`AMBIGUOUS`（解析歧义/未绑定/>4 仓）与 `INVALID`（任一仓绑定异常）→ `severity_rank=1`（最高档）。MISSING/EMPTY/SYNCED 同为低严重度（12/13/14），避免 4,000 仓未接管的常态淹没真实故障（D-015）。
- 基线模式概念作废：`TAG_DRIFT` 无条件按四仓 tag 集合与快照计算（D-010）。
- CI 状态**不进入**本优先级（D-002）；dirty 作为主状态参与（影响能否安全写操作）。

### 7.5 刷新触发与新鲜度 SLO（分档，禁止再用“秒级”一词打包）

| 层 | 触发/频率 | 数据 | 时延目标（P95） |
|---|---|---|---|
| L0 本地发现 | 启动、每 10min、FileSystemWatcher、手动刷新 | 根目录下深度 1 的 `.git` 目录 | 400 目录 <100ms |
| L1 本地指纹 | 每 15s，只 `stat` 6 个文件（`.git/HEAD`、`index`、`refs`、`packed-refs`、`logs/HEAD`、`FETCH_HEAD` 的 mtime+size），不起进程 | 变更检测 | <300ms/400 仓 |
| L2 本地深扫 | 仅指纹变化的仓 + 热仓（当前查看） | `git --no-optional-locks status --porcelain=v2 --branch` + ahead/behind | 本地/外部 IDE 变更 ≤5s；外部 commit/checkout/fetch ≤10s |
| L3 远程清单水位 | 每 10min，`GET /orgs/{org}/repos?sort=updated&limit=50` 读到水位即停 | 仓库清单/updated_at | 远程 push/打 Tag ≤10min |
| L4 远程 refs | 每 30min 或按需，仅 updated_at 变化或本地已绑定仓 | branches/tags 最新 commit | ≤ fetch 周期 |
| L5 远程全量对账 | 每日 1 次（可配 6h），可续传 job，无 120s 硬超时 | 全组织 refs 对账 | P95 ≤5min |
| L6 CI | v1 关闭（占位） | — | v2 |
| 操作完成 | 写操作结束事件 | 受影响组件强制重扫 + dirty-set 重算 | ≤2s 回写 |
| 手动刷新 | 点击刷新当前视图 | 立即 fetch + 重扫 | 即时 |

- UI **必须**显示“数据 X 秒/分钟前”（按数据源分别显示本地扫描时间与远程 fetch 时间）；任一数据源超 TTL 或本轮失败 → 对应状态 `STALE/UNKNOWN`；
- 写操作前必须 read-through 重新读取真实状态（不接受 DB 缓存），并做 CAS（preflight HEAD == 执行时 HEAD）；
- 时间统一 UTC ISO-8601 落库，并记录单调时钟用于 stale 判定。

### 7.6 状态 × 操作可用性矩阵（v1）

> 图例：✅ 允许（仅预检）；⚠️ 允许但需 dry-run/备份/二次确认；— 无需执行（若有 untracked 清理需求按 ⚠️ 二次确认）；❌ 禁止（返回明确错误码）。**常规操作**（Fetch/Pull/Checkout/Push/Commit）只做预检；**破坏性操作**（SYNC 强覆盖/Delete/Reset/Force Push）必须 dry-run + 确认 + 备份。

| 主状态 | Clone/Repair | Fetch | Pull(ff) | Push | Commit | Checkout 分支 | 切 Tag | SYNC 强覆盖 | Delete | CreateBranch | SetUpstream |
|---|---|---|---|---|---|---|---|---|---|---|---|
| UNKNOWN | ⚠️ 重试扫描 | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ⚠️ | ❌ | ❌ |
| INVALID | ⚠️ Repair/Re-clone | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ⚠️ | ❌ | ❌ |
| MISSING | ✅ Clone | — | — | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| EMPTY | ✅ 初始化/首推 | ✅ | — | ✅ 首推建 upstream | ✅ | ⚠️ | ❌ | ❌ | ⚠️ | ✅ | ✅ |
| UNMERGED | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ⚠️ | ❌ | ❌ |
| IN_PROGRESS | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ⚠️ | ❌ | ❌ |
| DETACHED_TAG / DETACHED_COMMIT | ⚠️ | ✅ | ❌ | ❌ | ❌ | ⚠️ 需确认 | ✅ | ❌ | ⚠️ | ✅ 基于当前 HEAD | ❌ |
| DIVERGED | ❌ | ✅ | ❌ 禁止（不自动 merge；替代：手动 merge/rebase 后重扫） | ❌ | ❌ | ⚠️ | ✅ | ⚠️（默认阻断；备份 ref + 输入组件 ID 后允许） | ⚠️ | ✅ | ❌ |
| BEHIND | ❌ | ✅ | ✅ | ❌ | ❌ | ⚠️ | ✅ | ⚠️ | ⚠️ | ✅ | ❌ |
| AHEAD | ❌ | ✅ | ❌ | ✅ | ❌ | ⚠️ | ✅ | ❌ | ⚠️ | ✅ | ❌ |
| NO_UPSTREAM | ❌ | ✅ | ❌ | ✅ 发布分支 | ❌ | ❌ | ✅ | ❌ | ⚠️ | ✅ | ✅ |
| DIRTY | ❌ | ✅ | ❌ 先 commit/stash | ✅ | ✅ | ⚠️ 需 stash/确认 | ⚠️ | ⚠️（stash -u + 确认） | ⚠️ | ✅ | ❌ |
| SYNCED | ❌ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | —（无需；仅清理 untracked 需确认） | ⚠️ | ✅ | ❌ |

- 恢复类操作独立：`Restore`（从 `.trash` 或备份 ref）见 FR-OP-11，可从任务中心/组件详情进入。
- 取消语义：只有 Clone/Pull/Checkout/Tag/SYNC/Delete/Create 等长任务支持合作式取消；取消后逐仓状态以实际扫描为准，未开始的仓保持原状，已开始的仓记 `CANCELLED` 并触发重扫（不整体回滚）。
- **与 FR-OP-05 的冲突裁决（C-04）**：本矩阵与 FR-OP-05 冲突时**以本矩阵为准**；AC-OP-15/16/18 按本矩阵重写——DIRTY 需 `stash -u` + 确认、DIVERGED 默认阻断且“备份 ref + 输入组件 ID”后允许、SYNCED 仅“清理 untracked”需确认。
- **与底部单仓按钮的映射（v0.3）**：本矩阵是底部按钮条可用性唯一权威——`PULL`→Pull(ff) 列、`FETCH`→Fetch 列、`CHKOUT`→Checkout 分支 列（切 Tag 走 `CHKOUT` 的二级选择，按切 Tag 列）、`COMMIT`→Commit 列、`PUSH`→Push 列；按钮启用/禁用与 tooltip 由矩阵逐格生成，禁止另写一套判断（§9.16 FR-ACT-02）。
- **矩阵口径冻结（D-025，v0.3.1；AC-UI-08 单源夹具 `reqs/v0.2/fixtures/m2-matrix.json` 按此生成）**：
  1. `DIVERGED` 的 Pull(ff) 为 **❌ 禁用**（原“⚠ 仅展示不自动 merge”自相矛盾，按更严格语义冻结；tooltip 给出“手动 merge/rebase 后重扫”替代动作与错误码）；
  2. 矩阵中 `—`（无需执行）按 **禁用** 编码断言（`MISSING` 的 Fetch/Pull、`EMPTY` 的 Pull 等），不再按“可点但无效果”处理；
  3. `COMMIT`/`PUSH` 的确认标志固定为 **需确认（C）**（FR-ACT-04 全局要求，优先于本矩阵的 ⚠/✅ 单元格语义）；确认条断言见 AC-UI-10a/10b；`Alt+5` 与按钮同路径；
  4. `NO_UPSTREAM` 的 Push 允许但必须走“发布分支并建立 upstream”确认（`setUpstream=true`）；`EMPTY` 的 Push 在 **M2a 构建**按“空仓无提交”禁用并 tooltip 指向 M2b 首推（FR-REPO-05），M2b 构建恢复本矩阵；
  5. 同一状态同一动作在 Host、UI、测试夹具三处必须一致；出现第二套判断（如 UI 自写矩阵）即 AC-UI-08 不通过。

---

## 8. 数据模型（设计基线；字段名可调，语义不可丢）

> 基线 schema 采用架构评审 03 §C.3（SQLite DDL 草案），本章为语义约束。**DB 是缓存不是事实源**：磁盘与 Gitea 才是事实。**新鲜度列按表定义（H-03）：事实类表必须带等价新鲜度列**——`repo_status` 带 `data_source`/`is_stale`/`calc_at`；`remote_ref` 带 `observed_at`/`synced_at`；`local_state` 带 `scan_at`/`last_success_at`/`scan_error`；`ci_status` 带 `fetched_at`；`sync_run` 带 `started_at`/`finished_at`/`duration_ms`；其余表（`repo`/`component`/`job`/`job_item`/`operation_log`/`app_setting`/`unbound_repo`/`scan_issue`/`notification`/`event_log`/`baseline_snapshot`）按行内定义带更新时间或采集时间。库可整库重建。

### 8.1 表清单（废弃“极简五表”表述）

> **冻结说明**：字段名自 v0.2 起冻结（变更需 ADR）；**v1 建表 16 张**（含 3 张空表预留/治理表）。`repo` 的远程字段（full_name/remote_url/default_branch/archived）由 REMOTE_SYNC 写，本地字段（is_local/local_path）由 LOCAL_SCAN 写；如实现要求严格单写者，可拆 `repo`/`repo_local` 两表（语义不变）。字段清单自 v0.2.1 起与图集 D4 ER 对齐（`repo.renamed_from`、`component.is_managed`、`local_state.scan_error`、`baseline_snapshot.tag_object_sha`、`job.started_at`、`ci_status` 全列已在本表登记），D4 ER 缺失列由图集侧并行回写。

| 表 | 主键 | 关键字段 | 唯一写者 |
|---|---|---|---|
| `component` | component_id | display_name, local_root, aggregate_status, status_calc_at, incomplete, is_managed, is_deleted, last_activity_at（最近活动时间，供工作台“最近活动”排序）, timestamps | STATUS_CALC |
| `repo` | repo_id（Gitea 数字 id） | component_id FK, repo_type(CHECK DESIGN/IMPL/MIL/SIL/OTHER), full_name UK, remote_url, default_branch, archived, renamed_from, is_local, local_path, timestamps | REMOTE_SYNC（远程列）/ LOCAL_SCAN（本地列） |
| `remote_ref` | (repo_id, ref_type, ref_name) | ref_type(CHECK BRANCH/TAG), commit_hash, observed_at, synced_at, generation | REMOTE_SYNC |
| `local_state` | repo_id | head_type(CHECK BRANCH/DETACHED_TAG/DETACHED_COMMIT/UNBORN), head_ref, head_sha, upstream_ref, upstream_sha, ahead, behind, dirty_count, untracked_count, op_in_progress, fingerprint, scan_at, scan_error, last_success_at | LOCAL_SCAN |
| `repo_status` | repo_id | main_status, flags_json, error_code, data_source, is_stale, calc_at | STATUS_CALC |
| `ci_status`（v1 空表） | (repo_id, commit_sha) | provider, state, run_id, run_url, branch, build_number, failed_stage, started_at, finished_at, duration_ms, fetched_at, updated_at；`ci_latest` 视图 = 每 repo 最新 commit 对应行（v1 空） | 预留（v2 REMOTE_SYNC） |
| `job` | job_id | kind(CHECK CLONE/PULL/FETCH/CHECKOUT/TAG/SYNC/DELETE/CREATE/RESTORE/REPAIR/PUSH/COMMIT), target_json, state(CHECK QUEUED/RUNNING/SUCCEEDED/FAILED/CANCELLED/INTERRUPTED/PARTIAL；**部分成功统一用 `PARTIAL`**), total/succeeded/failed/**skipped（整数计数）**, cancel_requested, started_at, finished_at, timestamps | JOB_RUNNER |
| `job_item` | (job_id, repo_id) | state(CHECK QUEUED/RUNNING/OK/FAILED/BUSY/SKIPPED/CANCELLED/**INTERRUPTED**), attempt, pre_head, expect_head, post_head, backup_ref, message, error_code, started_at, finished_at, updated_at | JOB_RUNNER |
| `operation_log`（审计） | id | ts, actor, action(CHECK 同 job.kind 集), target_json, before_json, after_json, result, error_code, request_id | 各写者追加 |
| `sync_run` | run_id | kind(CHECK ORG_LIST/REFS/FULL_RECONCILE), started_at/finished_at, duration_ms, coverage, total/ok/failed, error | REMOTE_SYNC |
| `app_setting` | key | value（非敏感配置；凭据只存引用） | UI/ORCH |
| `unbound_repo` | repo_id | full_name, reason(CHECK DOCS/PATTERN/CASE_CONFLICT/UNKNOWN), discovered_at | REMOTE_SYNC |
| `scan_issue` | id | path, issue_type(NON_REPO/REMOTE_MISMATCH/NETWORK_DRIVE/REPARSE/SAFE_DIRECTORY/LONG_PATH/CASE_CONFLICT), detail, discovered_at | LOCAL_SCAN |
| `notification`（v1 使用） | id | type(CHECK STATE_CHANGED/JOB_PARTIAL/TOKEN_EXPIRED/SYNC_FAILED/CI_EVENT), payload_json, read_at, created_at | HOST |
| `event_log`（v2 预留空表） | id | source(WEBHOOK/POLL), payload_json, received_at | v2 |
| `baseline_snapshot`（v1 记录，v2 管理） | (component_id, tag, repo_type) | commit_sha, tag_object_sha, tag_present, captured_at | JOB_RUNNER |

> commit 历史**不落表**（git 对象库本职）：只存 HEAD/upstream sha + ahead/behind 计数，避免千万行膨胀。`tag_present` 用于表达“四仓中某仓缺该 tag”的快照语义。`repo.repo_type=OTHER` 仅用于“未绑定/无法归类仓库”的占位行，**不参与组件聚合**（不创建 component 绑定）。

### 8.2 约束

- 每张表：主键、必要索引、`created_at/updated_at`、大写枚举、`PRAGMA user_version` 迁移；
- 每字段**唯一写者**（表 8.1 最后一列），跨表读取只经仓储层；写命令经 `Channel` 单写线程批量提交（200ms/500 行），只写值变化的行；
- 连接参数：`journal_mode=WAL; synchronous=NORMAL; busy_timeout=5000; foreign_keys=ON`；生产 1 写连接 + 只读连接池；
- DB 固定放 `%LOCALAPPDATA%\MbdGitTool\`，**禁止网络盘/漫游目录**；启动 `quick_check`，损坏时重命名旧库→建新库→全量重扫；
- 迁移前 `VACUUM INTO` 备份，失败回滚；bundled SQLite ≥ 3.51.3（规避 WAL-reset 竞态缺陷），启动记录 `sqlite_version()`；
- 实际选型见 12.3。

### 8.3 CI 契约字段 ↔ DDL 列映射（M0 冻结）

> 解决 §11.4 `CiLatest` 契约字段名与 §8.1 `ci_status` 列名不一致（M-12/C-05）：**本映射表是“契约↔DDL 一致”的唯一判据**，实现与 AC-CI-09 均按此断言。

| 契约字段（§11.4） | DDL 落点（§8.1） | 映射规则 |
|---|---|---|
| `CiLatest.repoId: number` | `ci_status.repo_id` | 直接同值 |
| `CiLatest.componentId: string` | 无直接列 | 由 `ci_status.repo_id` 关联 `repo.component_id` 推导，**不得**在 `ci_status` 冗余存储 |
| `CiLatest.repoType` | 无直接列 | 由 `ci_status.repo_id` 关联 `repo.repo_type` 推导（DESIGN/IMPL/MIL/SIL），不冗余存储 |
| `CiLatest.commitHash` | `ci_status.commit_sha` | 直接同值（契约绑定 commit，不是“仓库最新”） |
| `CiLatest.state` | `ci_status.state` | 六态同名大写 `PASS/FAIL/RUNNING/QUEUED/NONE/UNKNOWN` |
| `CiLatest.pipelineId` | `ci_status.run_id` | 直接同值（字符串） |
| `CiLatest.buildNumber` | `ci_status.build_number` | 直接同值（整数） |
| `CiLatest.failedStage` | `ci_status.failed_stage` | 直接同值 |
| `CiLatest.startedAt` / `CiLatest.finishedAt` | `ci_status.started_at` / `finished_at` | ISO-8601 UTC |
| `CiLatest.durationMs` | `ci_status.duration_ms` | 整型毫秒 |
| `CiLatest.url` | `ci_status.run_url` | 直接同值 |
| `CiLatest.branch` / `CiLatest.provider` | `ci_status.branch` / `provider` | 直接同值 |
| `CiLatest.fetchedAt` | `ci_status.fetched_at` | 直接同值（UI 新鲜度来源） |
| `ComponentCiAggregate.byState` / `worstState` | 无表 | 由 `ci_latest` 视图按 `repo_id` 分组聚合，不落表 |

---

## 9. 功能需求（FR）

> 每条 FR 必须有可判定验收；`P0` = MVP 不可缺，`P1` = v1 内应完成，`P2` = 可排 v1.1。

### 9.1 配置与认证（FR-CFG）

| ID | 优先级 | 需求 | 验收 |
|---|---|---|---|
| FR-CFG-01 | P0 | 首次启动向导：Gitea BaseURL、组织名、认证方式（PAT）、**本地根目录（v1 恰好 1 个，多根 v1.1）**、代理；每步即时校验 | 非法 URL/Token/路径/根目录（盘符根/用户目录根）给出字段级错误；可后退；完成后写入配置 |
| FR-CFG-02 | P0 | “测试连接”返回认证结果、组织可见仓库数、权限范围；Token 权限不足时列出缺失权限 | Mock/真实 Gitea 两种模式均可验证 |
| FR-CFG-03 | P0 | Token 存 Windows Credential Manager；配置文件只存引用；日志/命令行/`.git/config` 不出现明文 Token | 全量日志扫描无 token；remote URL 无凭据 |
| FR-CFG-04 | P1 | Token 过期/401 时全局提示并在恢复后自动续跑；不清空已有数据 | 注入 401 后 UI 显示重登入口，重登后任务可重试 |
| FR-CFG-05 | P1 | 代理/自签证书配置（系统代理默认、手动覆盖、证书信任选项） | 内网自签 Gitea 可连通 |
| FR-CFG-06 | P2 | 配置热更新（组织/根目录变更需确认影响范围并重扫） | 变更后工作台计数正确 |

### 9.2 远程元数据同步（FR-SYNC）

| ID | 优先级 | 需求 | 验收 |
|---|---|---|---|
| FR-SYNC-01 | P0 | 同步组织仓库清单：`repo_id`、全名、默认分支、URL、是否空仓、创建/更新时间；分页拉全（Gitea 单页上限 50） | 4,000 仓清单覆盖率可计算并在 UI 展示 |
| FR-SYNC-02 | P0 | 对“本地已绑定 + 工作台可见/收藏”的仓库同步分支列表与 Tag 列表（含 tag→commit），不全量拉 4,000 仓的分支/Tag；**“关注”定义 = 本地已绑定 ∪ 当前视图可见 ∪ 用户收藏**（收藏为 v1.1，v1 前两者） | 请求量与时间在 NFR 预算内；覆盖率展示 |
| FR-SYNC-03 | P0 | 首启立即执行一次组织同步；之后每日全量 + 清单水位 10min + 变化仓 refs 30min；失败指数退避重试 ≤2，失败写日志且不阻塞其他职责 | 断网首启进入降级态并可重试；部分成功显示覆盖率与失败清单 |
| FR-SYNC-04 | P0 | 每轮同步记录 `sync_run`（总数/成功/失败/覆盖率/耗时）；覆盖率 <100% 时工作台标 STALE | 注入部分失败后工作台出现 stale 提示 |
| FR-SYNC-05 | P1 | 远程仓删除/改名：`archived`/`renamed_from` 字段标记并提示；改名生成组件 ID 迁移事件（旧 ID→新 ID，需用户确认），不静默删行；未绑定仓库进入清单 | 改名/删除场景用例；迁移后工作台无重复组件 |
| FR-SYNC-06 | P1 | 增量策略以**仓库 `updated_at` 水位**为主；ETag/If-None-Match **可用则用、不可用不依赖**；并发上限默认 6；429/403/5xx 退避+抖动 | 限流注入不雪崩；无 ETag 的 Gitea 版本仍能增量 |

### 9.3 本地扫描与发现（FR-SCAN）

| ID | 优先级 | 需求 | 验收 |
|---|---|---|---|
| FR-SCAN-01 | P0 | 遍历 workspace_root 的直接子目录，识别“git 仓 + origin 匹配 Gitea 同名仓”；非仓/不匹配目录跳过并记录 | 目录夹具下识别/跳过结果与预期一致 |
| FR-SCAN-02 | P0 | 采集单仓：HEAD 形态（分支/tag/游离/空）、分支名、HEAD SHA、upstream、ahead/behind、dirty(-tracked/untracked)、stash、unmerged、进行中操作、shallow、LFS 缺失 | 状态矩阵夹具 100% 覆盖 |
| FR-SCAN-03 | P0 | 扫描不得写仓库：使用 `git --no-optional-locks`、禁用 auto-gc/auto-maintenance；`.git/index.lock` 存在时标 BUSY 不重试 | 外部 git 并发时无锁冲突、无仓库改动（`git status` 前后一致） |
| FR-SCAN-04 | P0 | 分层扫描：L0 发现（10min/事件）、L1 指纹（15s，stat 6 文件，不起进程）、L2 深扫（仅指纹变化仓 + FileSystemWatcher 触发，8 路；热仓 5s/冷仓 6h）；记录 `last_success_at`/`fingerprint` | 400 仓指纹轮询 <300ms；变化 5% 时深扫 ≤4s；无全量 30s git 轮询 |
| FR-SCAN-05 | P0 | 关系判定前必须保证 remote-tracking ref 新鲜：远程清单水位 10min + refs 30min/按需 + 手动刷新 + 写操作前强制 fetch | 远程变更 ≤10min 可见；UI 分源显示数据时间 |
| FR-SCAN-06 | P1 | 长路径、大小写、reparse point、网络盘检测与告警；用户目录含中文/空格可正常扫描 | 兼容性夹具 |

### 9.4 状态计算（FR-STA）

| ID | 优先级 | 需求 | 验收 |
|---|---|---|---|
| FR-STA-01 | P0 | 实现 7.2 的 14 主状态 + 7.3 标记 + 7.4 聚合优先级（含 UNKNOWN 兜底）；判定必须穷尽互斥 | **附录 C 状态矩阵**逐条通过（14 主状态 × 关键 flag × 15 聚合态） |
| FR-STA-02 | P0 | 数据不可用一律 UNKNOWN/STALE；任何一仓 UNKNOWN 时组件不得显示 SYNCED | 注入扫描/远程失败后组件变 UNKNOWN，不显示绿 |
| FR-STA-03 | P0 | 关系判定用 `compare_ref`（D-014）+ `merge-base --is-ancestor`（区分退出码 0/1/>1）；Gitea compare 仅兜底且仅限已推送 commit | 单元测试覆盖 0/1/>1 与 compare_ref 三种来源；未推送 commit 不调用 API |
| FR-STA-04 | P0 | 状态记录带 `calc_at`/`data_source`/`error_code`/`is_stale`；过期数据不参与“绿色” | 查询接口返回新鲜度字段 |
| FR-STA-05 | P1 | 操作完成 ≤2s 回写；本地/外部 IDE 变更 ≤5s（FileSystemWatcher + 指纹兜底）；外部 commit/checkout/fetch ≤10s；远程变更 ≤10min | 计时用例覆盖四档；`IClock` 可注入 |
| FR-STA-06 | P1 | 组件四仓 Tag 一致性无条件计算（不依赖“基线模式”）：四仓 tag 集合不一致/缺 tag/与快照不符 → TAG_DRIFT（v1 只记录告警，不做管理 UI） | 四仓 Tag 不一致时展示 TAG_DRIFT；AC-STA-11 |

### 9.5 工作台组件列表与检索（FR-BOARD；原看板，v0.3 重写）

| ID | 优先级 | 需求 | 验收 |
|---|---|---|---|
| FR-BOARD-01 | P0 | **默认视图 = 工作台左侧组件卡片列表（参考图为准）**，不再有“表格默认/卡片可选”：每个组件一张卡片，字段与 §11.3 一致；默认排序 = `severity_rank` 升序（数值越小越严重）→ 组件 ID 升序；**旧看板表格视图降级 v1.1**，v1 不提供入口、不验收（D-023） | 卡片列表为唯一默认视图；默认排序断言；`table` 视图入口不存在（AC-UI-02/AC-UI-14） |
| FR-BOARD-02 | P0 | 工作台搜索与筛选：搜组件 ID/仓库全名/当前分支/Tag/HEAD 短 SHA；中文 IME 以 `compositionend` 触发（组合期不阻塞）；facet=聚合状态、单仓主状态、RepoType、分支、Tag、CI 状态（占位）、有未提交改动、有未推送提交；排序=严重度/组件 ID/最近活动；**搜索结果只过滤左侧卡片列表与文件夹视图组件节点，不改中间文件树当前选中** | 搜索/筛选/排序夹具与 AC；IME 用例；1,000 组件滚动 ≥58fps |
| FR-BOARD-03 | P0 | 工作台七态齐备：空/加载/错误可重试/离线降级/过期/部分失败/**权限或认证过期**；每区（卡片区/四仓树/信息面板）可独立显示态；每态均可重试 | 状态注入用例 + 截图（AC-UI-02/11） |
| FR-BOARD-04 | P1 | 组件级批量操作入口（§11.6）：对选中组件执行 Clone/Pull/Fetch/Checkout/Tag/SYNC/Delete/Restore/Create；**阈值 = 选中 >10 个组件或 >40 个仓**时二次确认；支持“全选筛选结果”；入口不得新增全宽工具栏或改变 7 区域布局 | 选择 11 个组件触发确认；任务中心可逐仓查看；布局 bbox 断言（AC-UI-02） |
| FR-BOARD-05 | P1 | 数据新鲜度展示（本地/远程分源）与手动刷新（标题区与状态栏各一处入口）；刷新中不得整表闪白；刷新保留展开状态与滚动位置 | 视觉回归 + 计时（AC-UI-01/06） |
| FR-BOARD-06 | P1 | 未绑定仓库入口（菜单栏/标题区入口 → 抽屉）：按原因分组、可重新解析、可忽略（需记录理由） | 未绑定清单抽屉存在；重新解析用例 |

### 9.6 组件/单仓详情（FR-DETAIL）

| ID | 优先级 | 需求 | 验收 |
|---|---|---|---|
| FR-DETAIL-01 | P0 | 组件详情：**双击工作台组件卡片打开右侧抽屉**（宽 480–720px，覆盖主区右侧、不替换工作台布局；ESC 关闭焦点回落）；内容=4 仓状态卡（分支/HEAD/upstream/ab/ahead/behind/dirty）、Tag 对照、未绑定告警、快捷操作 | 组件状态下钻到单仓 1 次点击可达；抽屉打开/关闭后主区 bbox 不变（AC-UI-11/13） |
| FR-DETAIL-02 | P0 | 单仓详情：从右侧信息面板“详情”或文件树仓库根节点进入抽屉；**Tab 顺序固定：概览 / 提交历史 / 差异 / 文件 / CI（占位，第 5 位）**；提交历史分页虚拟滚动；差异只读（二进制给不可预览提示） | 10,000 提交仓库：历史首屏 ≤500ms、滚动 ≥58fps、单帧 ≤33ms、提交序列与 `git log` 一致；diff 正确 |
| FR-DETAIL-03 | P1 | 单仓详情“文件”Tab 只读文件树**复用 FR-TREE-01/03/04 全部规则**（同一树组件、同一白名单与路径安全，禁止两套实现）；白名单 = `.m/.c/.h/.slx/.mdl/.json/.xml/.yaml/.yml/.txt/.md/.csv`；白名单外与危险扩展名（`.exe/.bat/.cmd/.ps1/.lnk/.msi/.scr`）一律二次确认；路径限定 workspace_root 内 | 路径穿越/危险扩展名/白名单用例；与工作台文件树规则一致性断言 |
| FR-DETAIL-04 | P1 | 四仓分支不一致时（卡片与信息面板）显示 Impl 分支为主 + “分支不一致”角标 + 四仓对照 | 角标必现；对照准确 |
| FR-DETAIL-05 | P2 | 右键菜单：在资源管理器打开、复制路径、复制 commit、用其他程序打开（v1.1；v1 仅保留“在资源管理器打开/复制路径”两个只读项的最小入口或均不提供，见 FR-TREE-04） | 交互用例 |

### 9.7 批量操作（FR-OP，核心）

> **两类操作模型**：
> - **常规操作**（Fetch/Pull/Checkout 分支/Push/Commit）：仅预检 → 入队 → 逐仓执行 → 重扫；无需确认。
> - **破坏性操作**（SYNC 强覆盖 / Delete / 强制覆盖 / v1.1 Reset）：预检 → dry-run 预览 → 二次确认 → 入队 → 逐仓执行 → 重扫 → 审计。
>
> 批量不整体回滚，必须报告部分成功；互斥 = Host 内 per-repo 队列 + GitWorker 每仓锁 + 外部 `.git/index.lock` 检测（BUSY）。

| ID | 优先级 | 需求 | 验收 |
|---|---|---|---|
| FR-OP-01 | P0 | 批量 Clone：预检磁盘空间/长路径/凭据/远程存在；目录已存在则 repair 不覆盖；并发默认 2；错误分类（401/403/404 不重试，网络退避 ≤2）；逐仓 journal | 4 仓 2 成功 2 失败时组件显示 OP_PARTIAL_FAILED + 明细；重跑幂等 |
| FR-OP-02 | P0 | 批量 Pull/Fetch：对选中组件/单仓；失败逐仓报告；禁止隐式 merge（Pull 默认 `--ff-only`，非快进转手动） | ff-only 失败提示明确；无隐式 merge commit |
| FR-OP-03 | P0 | 批量 Checkout 分支：校验 ref 合法性；dirty 仓阻断或 stash（用户选）；部分失败报告 | 5 种前置态用例 |
| FR-OP-04 | P0 | 切 Tag：四仓同一 Tag；Tag 不存在于远程则跳过并报告；检出为 detached；记录 `baseline_snapshot`（`^{commit}` 解引用）；Tag 被移动时 `fetch --force` 更新并告警；禁止 `--prune-tags` | 缺 Tag/移动 Tag/annotated Tag 用例 |
| FR-OP-05 | P0 | 受控 SYNC（“用远程覆盖本地”）：fetch 目标 `<remote>/<同名分支>`；先备份 `refs/backup/sync/<ts>/<branch>`；dirty 存在 `stash -u`；CAS 校验后 `reset --hard <remote-ref>`（禁止 `git pull`）；untracked 默认不删、需显式勾选且执行前 dry-run；LFS 仓随后 `lfs pull`；拒绝覆盖本地独有分支 | 未推送 commit 可从备份 ref 恢复；untracked 保留；部分失败报告 |
| FR-OP-06 | P0 | 本地删除：仅删本地；预检脏/未推送/进行中/stash → 默认阻断；软删除到 `<root>\.trash`（可恢复，**统一保留 30 天**，D-017）；realpath 校验防误删；占用文件失败保留 DELETE_PENDING | 恢复用例通过；远端零变更；`.trash` 保留期与清理策略可配置且默认 30 天 |
| FR-OP-07 | P0 | CREATE 分支：输入分支名 → `git check-ref-format` 校验 → dry-run 表（重名/远程已删/超前落后/非法/各仓状态/基线 ref）→ 逐仓创建；部分失败可“撤销已创建分支” | 1 仓重名时预览明确；撤销成功；创建基线 ref 记录在 job_item |
| FR-OP-08 | P0 | 任务中心：队列/进行中/历史；进度（组件/仓级）、合作式取消、失败重试（仅失败项）、逐仓结果与错误码；任务持久化，重启后不丢 | 杀进程重启后任务状态一致（不重复执行）；取消语义符合 7.6 |
| FR-OP-09 | P1 | 操作审计：谁/何时/对哪个组件/哪些仓/操作/前后 HEAD/备份 ref/结果；审计保留 ≥90 天 | `operation_log` 可查询导出；日志留存策略生效 |
| FR-OP-10 | P1 | 操作互斥：同组件写操作串行；检测外部 git（index.lock）→ BUSY，不删锁 | 并发用例无数据损坏 |
| FR-OP-11 | P0 | **恢复操作**：从 `.trash`（按删除记录）或 `refs/backup/*`（按组件/仓/备份时间）恢复；**备份 ref `refs/backup/sync/*` 保留 ≥30 天（`backup.retentionDays` 可配，见附录 D）+ 定期清理任务 + 任务中心可见/可恢复**（H-05）；恢复前展示将覆盖的当前状态与冲突，需确认；恢复过程写审计 | AC-OP-30：删除后可恢复；SYNC 后未推送 commit 可从备份 ref 恢复；保留期内备份 ref 可列出并恢复，超期由清理任务回收 |
| FR-OP-12 | P1 | **组件纳入/移出管理**：按组件 ID 纳入（校验四仓存在性、展示将 clone 的仓），移出=本地删除或仅停止管理（DB 行保留 archived）；操作入审计 | AC-OP-31/AC-OP-32：纳入/移出后工作台与 DB 状态正确 |

### 9.8 单仓操作（FR-REPO）

| ID | 优先级 | 需求 | 验收 |
|---|---|---|---|
| FR-REPO-01 | P0 | Fetch / Pull（ff-only）/ Checkout 分支或 Tag；操作前重新 scan；dirty 阻断规则同 9.7 | 与命令行结果一致 |
| FR-REPO-02 | P1 | Push：仅当前分支、有 upstream；无 upstream 提示“发布分支”；detached 禁止 Push 并提示建分支；分支保护拒绝时展示服务端消息。**例外：EMPTY 空仓首次推送由 FR-REPO-05 处理，不受“需 upstream”拦截** | 4 类场景用例 + EMPTY 首推用例 |
| FR-REPO-03 | P1 | Commit（最小化）：提交 staged 变更 + message；存在 unmerged 时禁止；不提供冲突解决/rebase 界面 | 提交后状态变 DIRTY/SYNCED 正确 |
| FR-REPO-04 | P2 | Reset（保留/混合/硬）——v1.1 再做，且必须先备份 ref | 备份可恢复 |
| FR-REPO-05 | P0 | **状态修复类操作**（对应 7.6）：Repair/Re-clone（INVALID）、set-upstream 三子型修复（NO_UPSTREAM）、基于 detached HEAD 建分支（DETACHED_*）、EMPTY 空仓首次推送（建 upstream）；均走预检+job+逐仓结果 | AC-REPO-08/09/10/11：四类修复各 1 个夹具，执行后状态转移正确 |

> **v0.3 UI 入口**：单仓操作在 v1 的唯一界面入口 = 工作台底部按钮条（§9.16 FR-ACT-01…07）；本节 FR-REPO 的行为与安全规则不变，按钮条只负责前置/确认/进行中/失败态呈现，映射与矩阵见 §7.6 末条。M1 阶段所有按钮禁用（D-024）。

### 9.9 破坏性操作与安全（FR-SAFE）

| ID | 优先级 | 需求 | 验收 |
|---|---|---|---|
| FR-SAFE-01 | P0 | 删除/SYNC/强制覆盖前展示“将丢失什么”（commit 列表、untracked 清单、stash、影响仓数），默认阻断，需显式确认；强制覆盖需输入组件 ID | dry-run 表字段断言；未确认时实际写入操作数=0 |
| FR-SAFE-02 | P0 | 删除仅本地；UI 固定文案“远程仓库不受影响” | 文案快照；Gitea API 无写请求（Mock 断言） |
| FR-SAFE-03 | P0 | Token/日志脱敏；配置加密；诊断包导出前自动脱敏；崩溃转储默认关闭或导出前脱敏 | 日志/诊断包/转储扫描无 token；脱敏规则单测 |
| FR-SAFE-04 | P0 | 写操作全部走写前 journal + 崩溃恢复 reconcile（pre/expect/post 推断，禁止猜测成功） | 杀进程重启后 reconcile 用例；不猜测成功 |
| FR-SAFE-05 | P1 | 危险扩展名与路径穿越防护；reparse point 禁止写 | 路径穿越/危险扩展名/reparse 用例 |
| FR-SAFE-06 | P0 | 启动时扫描既有仓库 `.git/config`/`insteadOf`/URL 中的凭据并告警（不自动改用户配置）；Token 仅随 `Authorization: token <PAT>` header，绝不进 URL/命令行 | 夹具仓库内嵌 token → 告警且 UI 脱敏展示；抓包/命令行无 token |

### 9.10 任务/通知/日志/设置（FR-OPS）

| ID | 优先级 | 需求 | 验收 |
|---|---|---|---|
| FR-OPS-01 | P0 | 应用内通知中心（**从菜单栏「帮助/通知」或标题区铃铛进入的右侧抽屉**，不占工作台区域）：状态恶化、批量完成/部分失败、Token 过期、远程同步失败；（CI 事件类目 v1 空占位）；通知持久化到 `notification` 表；“清除”=删除通知行（v1） | 通知触发/已读/清除用例；`notification` 表有记录；清除后列表为空 |
| FR-OPS-02 | P0 | 日志与诊断（**从菜单栏「工具 → 日志与诊断」进入的二级页面/抽屉**）：结构化滚动文件（**单文件 10MB × 保留 30 天**，审计日志保留 ≥90 天）、**级别过滤=显示 ≥所选级别**、搜索、暂停、一键脱敏诊断包；诊断包内容以 §12.5 清单为准（日志、脱敏配置、DB 快照、schema 版本、最近 job/sync_run、版本信息、转储清单） | 滚动/留存/脱敏/≥级别断言；诊断包含 §12.5 全部必需项 |
| FR-OPS-03 | P0 | 设置（**从菜单栏「设置」或标题区齿轮进入的抽屉/二级页面**）：通用/工作区/认证/操作与安全/CI 集成（禁用占位）/实验特性；设置键、默认值、校验、生效范围见 **附录 D**；**“实验特性”分区 v1 无可配置键（仅说明/开关占位）**；设置项不得改变工作台 7 区域结构 | 设置键表核对；非法值被拒绝并提示；布局 bbox 断言 |
| FR-OPS-04 | P0 | 单实例锁；双开提示不破坏数据；崩溃后 stale lock 可自愈（三条件任一：PID 不存在 / PID 非本应用 / 锁文件 mtime 超 10min 且无存活进程） | 双开用例 + 强杀后重启可自愈 |
| FR-OPS-05 | P1 | **状态栏（§11.0 区域 G）分两段**：左段固定“**最近一次操作**”文案（`最近一次操作：<动作> <组件>/<RepoType> — <成功|失败(错误码)|进行中> · HH:mm`，未操作时显示“无”，失败可展开最近 5 条）；右段 6 段：数据新鲜度 / 进程健康 / 操作队列 / CI 队列（v1 恒 0）/ 通知 / 网络状态。**进程健康 = Host + GitWorker 2 灯；各 BackgroundService（RemoteSync/LocalScan/StatusCalc/JOB_RUNNER）以子状态/悬浮明细呈现** | 布局快照；灯光数与映射断言；“最近一次操作”文案模板断言（AC-UI-01/10） |
| FR-OPS-06 | P1 | 数据库 schema 版本 + 迁移 + 迁移前自动备份 + 损坏检测与重建（重建只需重扫） | 迁移/回滚/损坏重建用例；`PRAGMA user_version` 断言 |
| FR-OPS-07 | P2 | 自动更新（v1.1） | 更新/回滚用例（v1.1） |
| FR-OPS-08 | P0 | **首启编排与就绪门控**：三阶段固定为 `LOCAL_DISCOVERY`（本地发现）→ `REMOTE_RECONCILE`（远程对账）→ `STATUS_CALC`（状态计算）；每阶段进度事件冻结为 `{stage,total,current,cancellable}`（§11.1）；远程阶段可跳过降级；远程就绪后自动重判本地仓；**远程未就绪时不得判 INVALID/MISSING，正确态为 `UNKNOWN` + `incomplete=1`（H-01）**。**统一 §3.1/§11.1/AC-OPS-15/16 的口径为三阶段** | AC-OPS-15/16：分阶段 progress 事件断言（`LOCAL_DISCOVERY→REMOTE_RECONCILE→STATUS_CALC` + `{stage,total,current,cancellable}`）；跳过远程后进入只读，远程恢复后组件由 `UNKNOWN`（`incomplete=1`）自动重判为真实聚合态 |
| FR-OPS-09 | P1 | 错误码表（前缀 CFG/SYNC/SCAN/STATE/OP/FS/NET/GIT/SEC）与用户可读文案；错误码稳定不随文案变化 | 错误码表核对；UI 展示错误码 + 文案 |

### 9.11 CI 预留（FR-CI，v1 只占位）

| ID | 优先级 | 需求 | 验收 |
|---|---|---|---|
| FR-CI-01 | P0 | 实现 **§11.4 的 10 处 CI 锚点（v0.3 新落点，编号 CI-1…CI-10 不变）**：菜单栏/标题区 CI 入口、组件卡片 CI 槽位、右侧信息面板 CI 汇总条、单仓详情 CI Tab、状态栏 CI 队列段、通知抽屉 CI 事件类目、标题区筛选面板 CI facet、设置 CI 分区、数据契约、聚合分离；**10/10 必须在 v1 有落点，不得因新工作台删减** | 锚点存在性快照测试 10/10（AC-UI-12）；位置变更需同步本表与 AC |
| FR-CI-02 | P0 | 数据契约 `CiLatest`/`ComponentCiAggregate`/`ICiProvider` + `CI_UPDATED` 事件；v1 由 Mock 提供，`ci.enabled=false` 默认 | 打开 feature flag 后锚点显示 Mock 数据且工作台布局/区域 bbox 不变 |
| FR-CI-03 | P0 | CI 独立通道：**永不参与**组件聚合与卡片底色（工作台组件卡片底色恒中性）；空/未知状态用中性样式，禁止红色表达“没数据” | 注入 CI FAIL 后卡片底色不变（视觉回归）；§7.4 聚合函数输入不含 CI（单测） |
| FR-CI-04 | P1 | 无 CI 时显示“CI 未接入”统一空态组件，禁止裸 TODO/白块 | 全站无裸 TODO |
| FR-CI-05 | P0 | `ci_status` 表 + `ci.*` 配置键 + 事件类型在 v1 建好（空数据） | DDL/迁移中包含 |

> 真实 CI 接入（v2）前置：确认 CI 系统（Gitea Actions / Jenkins / 其他）、查询接口、commit→run 映射、多 run 聚合规则。

### 9.12 基线（v2 占位，v1 仅记录）

- v1：切 Tag 时写 `baseline_snapshot`；四仓同 Tag 判等，不一致显示 `TAG_DRIFT`。
- v2：基线列表/冻结/漂移对比/导出/审计。需求 ID 预留 FR-BASE-001..00N。

### 9.13 工作台布局、区域与视图切换（FR-WS；v0.3 新增，参考图为准）

> 本节与 §11.0 逐条对应；「参考图」= `docs/images/reference/main-ui-reference.png`。图是示意图，尺寸/比例以 §11.0.3 的**区间口径**验收，不要求像素级复刻（D-022）。

| ID | 优先级 | 需求 | 验收 |
|---|---|---|---|
| FR-WS-01 | P0 | **主界面 7 区域固定且顺序不可交换（上→下、左→右）**：A 菜单栏（最上）→ B 标题区 → C 左侧组件卡片列表 / D 中间 2×2 四仓文件树 / E 右侧信息面板（C/D/E 同一水平行）→ F 底部单仓按钮条 → G 底部状态栏；任何区域不得被删除或并入其他区域；二级页面（任务/通知/日志/设置/CI）不得替换主界面后丢失区域布局 | CDP 截图 + bbox 断言：7 区域存在、相对顺序、D 为 2×2、C 在左/E 在右；AC-UI-01 |
| FR-WS-02 | P0 | **尺寸与缩放**：基线视口 1440×900（WebView 内容区，不含系统标题栏），最小 1280×800；宽度分配 C≈20%、D≈62%、E≈18%（C 240–360px、E 220–320px，可拖拽并持久化，D 自适应且 ≥560px）；高度分配：A ≤40px、B ≤64px、F ≤64px、G ≤32px、C/D/E 占剩余（≥600px）；100%–150% 缩放无页面级横向滚动，175%–200% 允许 D 区内部滚动但页面级 scrollWidth=clientWidth；面板内各自独立滚动，页面整体不横滚 | 1440/1280/150%/200% 截图与 `scrollWidth === clientWidth` 断言；拖拽宽度写入 `ui.panel.leftWidth/rightWidth`；AC-UI-01 |
| FR-WS-03 | P0 | **视图切换**：工作台 ↔ 文件夹视图（`Ctrl+1` / `Ctrl+2`，或标题区视图切换按钮 / 菜单栏「查看」）；切换 ≤200ms；切换保留 `selection`（组件+聚焦仓）与展开状态；两视图共用同一状态快照，禁止各自计算 | 切换计时与选择/展开状态保持断言；AC-UI-07 |
| FR-WS-04 | P0 | **二级入口全部经菜单栏或标题区进入，不设旧 App Shell 左侧图标导航**：任务中心 / 通知中心 / 日志与诊断 / 设置 / CI 流水线 / 未绑定仓库；抽屉打开不改变主区 bbox，ESC 关闭且焦点回到触发控件；入口可达率 6/6 | 6 个入口点击可达；抽屉前后 C/D/E bbox 不变；AC-UI-13 |
| FR-WS-05 | P1 | **键盘与可访问性**：Tab 顺序 A→C→D→E→F→G（跳过纯展示状态栏）；树内 ↑↓←→ 展开/移动、Enter 打开、空格选中；`F5` 刷新、`Ctrl+F` 搜索、`Ctrl+1/2` 视图、`Esc` 关闭抽屉；焦点环 2px 可见；状态变化 `aria-live` 播报 | 键盘用例；焦点环与 aria 断言；reduced-motion 下动效 ≤1ms |

### 9.14 四仓文件树（FR-TREE；v0.3 新增，工作台中间 D 区）

| ID | 优先级 | 需求 | 验收 |
|---|---|---|---|
| FR-TREE-01 | P0 | **读取目录**：按需懒加载（首次展开 ≤300ms；单目录 >500 子项分页/虚拟化，单次返回 ≤500 条）；默认展开仓库根 1 层；**排序 = 目录优先 → 名称升序（大小写不敏感）→ 扩展名**；默认隐藏 `.git` 与 `.trash`（`tree.hideDotGit=true` 可关）；不提供大小/时间列（v1.1 可选）；读取失败（权限/超长路径/不存在）返回统一错误码并可就地重试 | 目录 API 单测；排序夹具；2,000 子项目录计时；AC-UI-03 |
| FR-TREE-02 | P0 | **四仓 2×2 固定**：左上 Design / 右上 Impl / 左下 MIL / 右下 SIL（顺序不可变）；每格独立展开与独立滚动；展开状态按组件 LRU 缓存（≥100 组件）；切换组件后每格重置为「根展开」；缺失/未克隆/未绑定仓显示明确空态与原因（“未克隆/绑定异常/读取失败”），**禁止伪造树或显示他仓内容** | 2×2 顺序截图断言；组件切换后展开状态；缺仓空态；AC-UI-03 |
| FR-TREE-03 | P0 | **双击打开**：单击选中（同时把该仓设为「聚焦仓」，右侧信息面板与底部按钮条绑定同一聚焦仓）；双击文件用系统默认程序打开、双击文件夹用默认程序（资源管理器）打开；**仅白名单扩展名直开**（白名单同 FR-DETAIL-03），白名单外/危险扩展名一律二次确认；打开失败（无关联程序/被占用/超长路径/已删除）给出可读错误码与建议动作；双击不会修改任何文件 | 双击夹具；危险扩展名确认；错误码；AC-UI-03/04 |
| FR-TREE-04 | P0 | **只读与路径安全**：文件树 API 严格只读——不提供新建/重命名/删除/移动/拖放/上传/在树内编辑入口；所有路径请求先 canonical 解析，必须位于 `workspace_root` 之下且属于已绑定仓目录（拒绝 `..`、绝对路径、UNC/网络盘、reparse point 越界、保留名、尾随空格/点）；复核目录 realpath；拒绝时返回统一错误码且不泄露越界路径内容 | 路径穿越/绝对路径/UNC/reparse/保留名用例 100% 拒绝；树无写入口；AC-UI-04 |
| FR-TREE-05 | P1 | **节点状态标记**：每个仓库根节点显示该仓主状态徽标（§11.2 通道 A token，图标+文案+颜色），选中行高亮；已跟踪且未提交改动的文件行显示 `●`、untracked 显示 `○`（数据取自既有 `git status` 快照，**不得为文件树额外起 git 进程**）；该仓 STALE 时徽标灰化并显示“数据 X 前” | 状态矩阵夹具；进程计数断言（展开文件树不新增 git 进程）；AC-UI-05 |
| FR-TREE-06 | P1 | 文件树右键菜单仅只读项：在资源管理器中打开、复制路径、刷新该仓目录；“在资源管理器中打开”需二次确认路径后调用系统程序 | 右键菜单项断言（无写项）；AC-UI-04 |

### 9.15 文件夹视图（FR-FOLDER；v0.3 新增）

| ID | 优先级 | 需求 | 验收 |
|---|---|---|---|
| FR-FOLDER-01 | P0 | **结构**：`workspace_root` → 组件目录 → 四仓目录（Design/Impl/MIL/SIL）→ 文件目录树；最多 1 层组件目录、1 层仓库目录，文件层可无限展开；未克隆组件显示“未克隆”淡显节点（不可展开）；未纳管目录归入“未纳管”分组（只读展示，不参与聚合）；组件/仓库节点文案与工作台一致 | 树结构快照夹具；AC-UI-06 |
| FR-FOLDER-02 | P0 | **交互**：懒加载 + 展开/折叠；虚拟滚动（400 组件 / 1,600 仓规模 ≥58fps）；双击文件/文件夹同 FR-TREE-03；单击选中组件/仓库节点同步 `selection` 并可在切回工作台时定位 | 性能计时；选中同步断言；AC-UI-06 |
| FR-FOLDER-03 | P0 | **状态标记**：组件节点=聚合状态徽标 + 四仓 chip；仓库节点=主状态徽标；与工作台来自同一状态快照，同一夹具两视图状态必须逐字一致；STALE 显示数据时间与斜纹 | 同一夹具两视图状态一致断言；AC-UI-06 |
| FR-FOLDER-04 | P1 | **过滤**：按聚合状态/RepoType/是否有本地改动/未克隆过滤；搜索组件 ID/仓名/文件名（防抖 120ms，p95 ≤300ms）；文件夹视图筛选状态独立保存，不污染工作台筛选 | 过滤夹具；筛选状态隔离断言；AC-UI-06 |
| FR-FOLDER-05 | P0 | **与工作台切换**：`Ctrl+1` 工作台 / `Ctrl+2` 文件夹视图；切换 ≤200ms、保留选中与展开路径；**旧看板表格视图 v1 不提供入口**（D-023），不得以“文件夹视图=旧表格”混淆 | 切换断言；AC-UI-07/AC-UI-14 |

### 9.16 底部单仓操作按钮条（FR-ACT；v0.3 新增，工作台 F 区）

> 按钮条位于工作台底部、状态栏之上（参考图），**只作用于当前聚焦仓**；组件级批量操作走 §11.6 入口，二者不得混用同一按钮条。
> **M2a 范围（D-025）**：FR-ACT-01/02/03/04 全量属 M2a；FR-ACT-05 的“合作式取消 + job/jobId + job 状态持久化”与 FR-ACT-06 的“任务中心保留失败项/仅重试失败项 + 失败项明细”归 M2b；FR-ACT-07 的 M2a 段 = 按 FR-ACT-02…04 与 05/06 的 M2a 子集接入。**M2a 不得用“无 job”为由省略错误码、回写、同仓串行或静默失败处理**（详见 §9.16 末注与 `reqs/adr/D-025-m2a-m2b.md`）。

| ID | 优先级 | 需求 | 验收 |
|---|---|---|---|
| FR-ACT-01 | P0 | **按钮固定 5 个且文案/顺序与参考图一致**：`PULL` → `FETCH` → `CHKOUT` → `COMMIT` → `PUSH`（英文大写，不本地化）；未选中聚焦仓时 5 按钮全部禁用并显示提示“请先在文件树中选择仓库”；按钮条不因窗口宽度换行或隐藏 | 按钮存在/顺序/禁用态快照；未选中点击=0 次写操作；AC-UI-08 |
| FR-ACT-02 | P0 | **可用性严格取 §7.6 矩阵**（映射见 §7.6 末条）：PULL=Pull(ff)、FETCH=Fetch、CHKOUT=Checkout 分支（切 Tag 走 CHKOUT 二级选择、按切 Tag 列）、COMMIT=Commit、PUSH=Push；禁用时 tooltip 必须给出“原因 + 替代动作 + 错误码”；**M1 只读边界：M1 阶段 5 按钮全部禁用（含 FETCH）**，只读能力通过刷新/重扫/文件树查看体现（D-024） | 14 主状态 × 5 按钮矩阵逐格断言；M1 全禁用截图与理由断言；AC-UI-08/09 |
| FR-ACT-03 | P0 | **前置条件**：每次点击先 read-through 重扫（不接受 DB 缓存）并做 CAS；`FETCH` 需网络可达（离线禁用并说明）；`PULL` 仅 `--ff-only`（非快进给出手动处理指引）；`CHKOUT` 先弹分支/Tag 选择器（数据来自 `remote_ref`，含搜索）；`COMMIT` 仅提交 staged 变更、message 必填 1–200 字符、无 staged 时禁用、unmerged 时禁用；`PUSH` 需当前分支有 upstream 且非 detached（EMPTY 首推走 FR-REPO-05） | 各类前置夹具；无 staged 时 COMMIT 禁用；PULL 非快进明细；AC-UI-10 |
| FR-ACT-04 | P0 | **确认与安全**：`FETCH`/`PULL`/`CHKOUT` 仅预检+入队（不弹确认）；`COMMIT`/`PUSH` 为写操作，须在按钮条上方显示 ≤48px 的确认条——“将提交 N 个文件 / 将推送 N 个提交（分支 → upstream）”，确认后才执行；确认条不得遮挡或移动 5 个按钮；**破坏性覆盖（SYNC 强覆盖/Force Push/Reset）不进按钮条**，避免误触；`Alt+5` 等快捷键与按钮同路径 | 确认条快照；未确认时写操作数=0；按钮 bbox 不变；AC-UI-10a/10b |
| FR-ACT-05 | P0 | **进行中/失败**：执行中该按钮显示 spinner 且同仓其余按钮禁用（同仓操作串行）；状态栏左段实时显示进行中；失败在状态栏显示错误码+中文文案并可重试；`BUSY`（外部 git）/`CONCURRENT_CHANGE`（CAS）按错误码处理，**禁止删锁**；**合作式取消服从 §7.6**：`PULL` 等长任务支持取消；`FETCH` 为短网络任务不提供取消（超时中断）；`COMMIT` 不可取消；`PUSH` 在服务端接收前可中断、取消点后不可取消；取消后以实际扫描为准 | M2a：错误码/BUSY/CAS/进行中/失败可读；M2b：job 状态与取消语义断言；AC-UI-10（M2a）+ AC-UI-10c（M2b） |
| FR-ACT-06 | P1 | **完成后回写**：操作完成 ≤2s 刷新对应仓状态、四仓文件树标记、组件卡片与信息面板；状态栏左段更新为“最近一次操作”模板（FR-OPS-05）；失败项保留在任务中心可“仅重试失败项”；一次操作写一条审计（FR-OP-09） | M2a：≤2s 回写、状态栏模板、审计一条（AC-OP-33）；M2b：任务中心/仅重试失败项；AC-UI-10 + AC-OP-33 + AC-UI-10c |
| FR-ACT-07 | P0 | **版本门禁（禁止可点但无效果）**：M0=按钮存在、禁用、点击显示“M0 占位：单仓操作将在 M2 接入”说明；M1=只读、5 按钮均禁用并给出“M1 只读”理由；**M2a=按 FR-ACT-02…04 与 FR-ACT-05/06 的 M2a 子集接入（取消/job/任务中心除外）**；M2b=按 FR-ACT-02…06 全量接入；任何阶段点击按钮不得出现静默失败（无 toast/无状态变化） | 三阶段截图 + 点击断言；AC-UI-09 |

> **M2a/M2b 交付边界（D-025，v0.3.1）**：
> - **M2a 必须交付**：FR-ACT-01/02/03（离线 FETCH 子项除外，见下）/04 全量；FR-ACT-05 的 spinner、同仓串行、状态栏进行中、错误码+中文、`BUSY`/`CONCURRENT_CHANGE`、失败可读；FR-ACT-06 的 ≤2s 回写、状态栏“最近一次操作”模板、一次操作一条审计（AC-OP-33）；FR-ACT-07 的“M0/M1/M2a 门禁”。
> - **M2b 交付（M2a 只留“M2b 未接入”明确提示）**：FR-ACT-05 的合作式取消、job/jobId/job 持久化；FR-ACT-06 的任务中心失败项保留与“仅重试失败项”；批量入口（§11.6）与任务中心。
> - **M2a 的“可重试”口径** = 状态栏/Toast 展示错误码 + 用户修正后**重新预检并再次触发同一动作**（不产生 job，不做任务中心回执重试）；该口径必须在 M2a 验收报告中写明，不得写成“FR-ACT-05 全通过”。
> - **PUSH 确认条口径**：以本文件 FR-ACT-04 为准，**不采用** `docs/ui-redesign-spec.md` §6.4 的“常规 PUSH 免确认”；`Alt+5` 等全局快捷键与按钮同路径（先出确认条，未确认 0 写）。这是 M2a 的硬性安全要求，不得以“体验取舍”为由豁免。
> - **离线 FETCH 子项**（FR-ACT-03 的“FETCH 需网络可达，离线禁用并说明”）归 M2b；M2a 验收报告须列明该子项未覆盖，不得默认通过。
> - **M2b 未接入提示**：M2a 构建中批量/任务中心入口点击必须出现“M2b 未接入（批量/作业/审计）”明确文案且 Host 写调用数=0（AC-UI-15）；禁止可点无效果或伪装成功。

### 9.17 组件卡片与右侧信息面板（FR-CARD / FR-INFO；v0.3 新增）

| ID | 优先级 | 需求 | 验收 |
|---|---|---|---|
| FR-CARD-01 | P0 | **组件卡片内容**（左列 C 区，宽随栏宽 220–360px、高 ≥72px，**底色中性**）：组件 ID（超长省略 + tooltip 全名）、聚合状态徽标（图标+中文文案+通道 A 颜色）、四仓 chip `D/I/M/S`（各自主状态 + 缺失斜纹）、当前分支（`MIXED_BRANCH` 角标 + 四仓对照 tooltip）、CI 占位槽位（定高 16–20px，“CI 未接入”）、最近活动/新鲜度；卡片不整卡饱和色 | 卡片字段快照；底色中性断言；AC-UI-02/12 |
| FR-CARD-02 | P0 | **选中与联动**：单击卡片 = 选中组件并加载四仓文件树（默认聚焦第一个存在的仓）、右侧信息面板切换到该组件；`Ctrl/Shift` 多选 = 进入批量选择态（§11.6）；双击卡片 = 打开组件详情抽屉（不替换工作台）；选中态有 2px 焦点环与 `aria-selected`；**单一选中模型** `{componentId, focusedRepoType}` 为文件树/信息面板/按钮条唯一数据源 | 联动断言；单一选中模型（改选中后三处同步）；AC-UI-02/11 |
| FR-CARD-03 | P0 | **列表规模与检索**：1,000 组件虚拟滚动 ≥58fps、DOM 卡片节点 ≤120；默认排序 severity_rank 升序→组件 ID；搜索/筛选/排序见 FR-BOARD-02；4,000 组件压测档；卡片区独立滚动，纵向滚动条不挤压 D/E 区 | 性能计时；滚动条与 bbox 断言；AC-UI-02 |
| FR-CARD-04 | P0 | **批量入口（不破坏参考图布局）**：①菜单栏「操作」菜单（对当前选中集合生效，未选中时禁用）；②组件卡片右键菜单；③选中 ≥1 时卡片区顶部叠加显示批量条（高 ≤36px，浮层覆盖在卡片区顶部，**不新增全宽工具栏、不改变 C/D/E 区域 bbox**）；批量动作清单见 FR-BOARD-04；>10 组件或 >40 仓二次确认 | 三个入口可达；批量条出现时 C/D/E bbox 不变；AC-UI-02 |
| FR-CARD-05 | P1 | **卡片区七态 + 刷新**：空（“未发现组件，请检查配置/首扫”）、加载骨架、错误可重试、离线降级、过期（STALE 斜纹 + 数据时间）、部分失败（OP_PARTIAL_FAILED 徽标 + 重试）、权限/认证过期（重登入口）；刷新不得整区闪白，保留滚动位置 | 七态注入截图；刷新视觉回归；AC-UI-02 |
| FR-INFO-01 | P0 | **右侧信息面板 E 区（宽 220–320px，纵向滚动，字段不得横向溢出）**：当前聚焦仓 = 仓库全名、RepoType、主状态徽标 + 中文文案 + 数据时间、分支 / HEAD 短 SHA / upstream、ahead/behind/dirty 计数、最近一次操作结果、**CI 占位区（“CI 未接入”或 6 态 Mock）**、快捷按钮“详情 / 在资源管理器中打开”；窄栏内字段换行规则固定（标签列 ≤72px） | 字段快照；窄栏 1280/200% 不溢出；AC-UI-11 |
| FR-INFO-02 | P0 | **空态与一致性**：未选中聚焦仓时显示空态引导“从中间文件树选择仓库”（不得显示上一个仓的残留数据）；信息面板与底部按钮条必须绑定**同一**聚焦仓（同一时刻二者指向不一致 = 缺陷）；STALE 显示“数据 X 前”+ 斜纹 | 空态截图；切换仓后按钮条目标同步断言；AC-UI-11 |
| FR-INFO-03 | P1 | **构建状态区（CI 通道 B）**：v1 固定显示“CI 未接入”空态；`ci.enabled=true` 时显示 Mock 6 态（PASS/FAIL/RUNNING/QUEUED/NONE/UNKNOWN）与新鲜度；CI 区永不参与组件聚合与卡片底色（FR-CI-03） | CI Mock 开关截图；卡片底色不变；AC-UI-11/12 |
| FR-INFO-04 | P1 | **信息新鲜度**：面板字段刷新 ≤2s（本工具操作后）/ ≤10s（外部 IDE 变更）；过期字段灰化并保留最近成功时间；刷新时只更新字段值，不重建面板 | 计时与视觉回归；AC-UI-11 |

> **M2a 范围（D-025，v0.3.1）**：FR-INFO-01/02 与 FR-INFO-04 的“本工具操作后 ≤2s 刷新”属 **M2a 出口**（AC-UI-11）；FR-INFO-03（CI 占位）属 M1 已有能力；FR-INFO-04 的“外部 IDE ≤10s / STALE 灰化”复验归 M2b（依赖扫描周期实测）。FR-CARD-01/02 的卡片字段与选择联动为 M1 已交付能力；**FR-CARD-04 的批量入口在 M2a 构建只保留“M2b 未接入”明确提示（AC-UI-15）**，不得可点无效果、不得伪造成功、不得因 M2b 未交付而删除入口以外的主区布局（C/D/E bbox 不变）。组件详情抽屉的焦点归还（FR-WS-04/AC-UI-13）在 M2a 复验。

---

## 10. 非功能需求（NFR）

### 10.1 性能与容量

| ID | 指标 | 预算（测试机：8C/16G/SSD，写死） |
|---|---|---|
| NFR-PERF-01 | 容量基线 | 远程 4,000+ 仓库 / ≈1,000 组件；本地 100+ 组件 / 400+ 仓库；压测按 4× 余量（4,000 组件）验证 |
| NFR-PERF-02 | 工作台首屏可交互 | ≤1.5s（数据就绪后，7 区域全部可见且组件卡片/文件树根可操作）；冷启动到首屏 ≤5s（不含首扫） |
| NFR-PERF-03 | 滚动/渲染 | 组件卡片列表与文件树/文件夹视图滚动 ≥58fps 平均，单帧 ≤33ms；DOM 卡片节点 ≤120，总元素 ≤15,000；2×2 四仓树每格独立滚动 |
| NFR-PERF-04 | 搜索响应 | 防抖 120ms；输入到结果 p95 ≤300ms |
| NFR-PERF-05 | 本地扫描 | L1 指纹 400 仓 <300ms；L2 深扫仅变化仓（5% 变化时 ≤4s）；起 git 进程数限于变化仓 |
| NFR-PERF-06 | 远程同步 | 清单水位 80 请求 ≈2–8s（10min 周期）；refs 增量稳态 ≤20 请求；每日全量对账 4,000 仓 P95 ≤5min、可续传、覆盖率 100%；删除“120s 超时” |
| NFR-PERF-07 | 内存/存储 | Host 常驻 ≤300MB（不含 WebView2）；WebView2 另计 200–400MB；DB <100MB；空闲 CPU <2% |
| NFR-PERF-08 | 数据新鲜度 SLO | 本工具写操作 ≤2s；本地/IDE 变更 ≤5s；外部 commit/checkout/fetch ≤10s；远程 push/Tag ≤10min（水位）/≤6h（关水位）；UI 必须分源显示数据时间 |
| NFR-PERF-09 | 首启 | 400 仓已克隆场景 ≤30s 可用工作台（先本地后远程、渐进渲染；左右面板先出骨架、树懒加载）；查询一次 ≤500 行 + 键集分页 |

### 10.2 可靠性 / 离线

| ID | 需求 |
|---|---|
| NFR-REL-01 | 断网/远程不可达：工作台进入只读降级态，显示“数据截至 HH:mm”，本地扫描仍可用；恢复后自动续跑 |
| NFR-REL-02 | 任意后台职责失败不阻塞 UI；职责重启有上限（5 分钟 3 次失败 → 降级并在状态栏显示原因），禁止静默 |
| NFR-REL-03 | 崩溃恢复：任务 journal + 启动 reconcile；不猜测成功；部分失败必须用户可见 |
| NFR-REL-04 | 数据损坏：detect + 一键重建（重建 ≤5min/100 组件） |
| NFR-REL-05 | 磁盘不足预检：clone 前估算 `.git`+LFS 空间，不足阻断 |

### 10.3 安全

- Token 仅存 `Windows Credential Manager`（DPAPI 加密文件为等价实现细节）；进程内不落盘；日志/诊断/崩溃转储/命令行/`.git/config` 全链路脱敏；
- 启动扫描既有仓库 `.git/config`/`insteadOf`/remote URL 中的凭据并告警（不自动改用户配置）；
- `GIT_ASKPASS`/`credential.helper` 临时通道，remote URL 保存无凭据形式；Token 仅随 `Authorization: token <PAT>` header 发送；
- Loopback API（如采用）使用随机端口 + 每次启动随机 token + 仅 127.0.0.1 绑定 + CORS 白名单；
- 写操作审计；破坏性操作二次确认；删除仅本地并默认可恢复；
- TLS 校验默认开启；企业自签证书需显式配置信任；安装包/更新包签名校验（v1 至少内网分发校验）。

### 10.4 兼容性与运行环境

| ID | 要求 |
|---|---|
| NFR-COMPAT-01 | **Windows 11 x64 为基线**；Windows 10 22H2 仅 ESU 环境（是否支持待 OQ 确认，D-018） |
| NFR-COMPAT-02 | .NET 10 LTS（self-contained x64，.NET 8 已于 2026-11-10 EOL）；WebView2 Runtime 随安装包提供 Evergreen Standalone 离线安装器，启动检测缺失并引导 |
| NFR-COMPAT-03 | Git for Windows ≥ 2.40（含 Git LFS、GCM）；检测 `core.longpaths` 与 LongPathsEnabled；应用 manifest longPathAware |
| NFR-COMPAT-04 | 路径支持中文/空格；网络盘/OneDrive/UNC 允许只读展示、禁止写；reparse point 告警；DB 固定 `%LOCALAPPDATA%` 禁止网络盘/漫游 |
| NFR-COMPAT-05 | Git LFS：检测仓库是否使用 LFS；缺失 git-lfs 时阻断写操作并提示；clone 先 `GIT_LFS_SKIP_SMUDGE=1` 再后台 `lfs pull`；缺失对象标红 |
| NFR-COMPAT-06 | 显示器 100%–200% 缩放不产生页面级溢出（100%–150% 工作台 2×2 无横滚；175%–200% 允许中间 D 区内部横滚）；低端机/远程桌面自动关闭动效 |
| NFR-COMPAT-07 | v1 单用户单实例（per-user DB + 工作区锁）；RDP 多会话/共享工作区不支持写操作，检测后提示（D-007） |
| NFR-COMPAT-08 | 安装/更新：MSIX 用户级 + EV 签名 + 更新前等 job 空闲并备份 DB + 失败回滚；企业 SCCM 场景可另出 MSI（[待确认] OQ-13） |

### 10.5 可用性与可访问性

- 颜色只作辅助：状态必须“图标 + 中文文案 + 颜色”三重编码；对比度 ≥WCAG AA 4.5:1；
- 动效可验收：hover/press 120ms、展开/收起 240ms、页面切换 200ms、单次 ≤320ms；`prefers-reduced-motion: reduce` 下 ≤1ms；列表仅 1 种入场动效；
- 键盘可达、焦点可见（2px 焦点环）、`aria-live` 播报任务结果；点击目标 ≥24×24，主按钮 ≥32px；
- 删除“炫酷/丝滑/实时/秒级/高并发/性能极高”等不可测措辞。

### 10.6 可维护性与可测试性

- 需求追溯：**附录 C 状态矩阵 + 第 13 章 AC 追溯矩阵**（FR 分组→AC），每条 FR 有 ID；无 AC 的 FR 不得进入 M2；
- 测试资产（M0 交付物）：**Gitea 单二进制 fixture**（Windows 可跑，`gitea.exe web` + 内置 SQLite）+ **WireMock.NET 契约测试**；本地 git 状态矩阵夹具生成器（附录 C）；`IClock` 假时钟；BenchmarkDotNet 夜间基线；
- 端到端用例：首启三阶段/跳过降级、批量 Clone 部分失败、SYNC 保护与恢复、切 Tag 缺 Tag、删除恢复、断网降级、崩溃恢复、外部 git 并发；
- 日志/错误码表统一（附录 D；错误码前缀：CFG/SYNC/SCAN/STATE/OP/FS/NET/GIT/SEC）。

---

## 11. 界面信息架构（工作台 + 文件夹视图，v0.3 以参考图为准）

> **唯一视觉基准 = `docs/images/reference/main-ui-reference.png`（用户参考图）**。凡旧 v0.2.1 §11（看板表格 + 右侧详情栏 + App Shell 左侧图标导航）与参考图冲突，**一律以图为准**（D-022）；逐条裁决见 §11.0.1。工作台的「组件卡片列表」即全文所称“看板”；旧「表格视图」降级 v1.1（D-023）。本章条款以 **PC/截图可测**为写法目标：每区有尺寸区间、交互规则和对应 AC。

### 11.0 主界面工作台（参考图为准）

#### 11.0.1 冲突裁决：旧 §11 → v0.3 工作台（Top 裁决全表）

| # | 旧设计（v0.2.1，与图冲突即失效） | 参考图 / 用户要求 | 裁决 | 落地条款 |
|---|---|---|---|---|
| 1 | 看板以**表格为默认视图**，卡片视图可选 | 左侧为**组件卡片列表**（Component 1/2/N），中间为 2×2 四仓文件树 | **旧表格视图废弃为主界面，降级 v1.1**；组件卡片列表为 v1 唯一默认列表视图（v1 入口不存在） | D-023、FR-BOARD-01、§11.3 |
| 2 | **App Shell 左侧图标导航 8 项** + 全局工具栏 | 顶部**菜单栏 + 标题区**，无左侧图标导航 | **左侧图标导航废弃**；二级功能一律经菜单栏/标题区进入抽屉或页面 | §11.1、§11.6、FR-WS-04 |
| 3 | 主界面最右为**右侧详情栏 340px**（常驻详情列） | 最右为**窄栏**：选中仓基本信息 + 构建是否成功 | **详情列重写为窄信息面板**（18%，220–320px）；组件/单仓详情改从双击卡片/“详情”按钮打开**抽屉** | §11.3、FR-INFO、FR-DETAIL-01 |
| 4 | 文件树只在**单仓详情内**（FR-DETAIL-03），主界面无树 | 中间 **Design/Impl/MIL/SIL 四仓文件树 2×2**，树状分层、双击打开 | **文件树提升为主界面一级区域**（D 区）；单仓详情“文件”Tab 复用同一树组件 | §9.14、FR-TREE、FR-DETAIL-03 |
| 5 | 卡片视图“可选、宽 ≥320px、高 ≥104px”，左侧状态轨道 | 左列为**窄卡片列表**（宽度随栏约 220–360px） | **卡片为唯一主视图并按左列宽度自适应重排**（≥72px 高）；表格行布局废弃 | §11.3、FR-CARD-01 |
| 6 | 看板**顶部全宽批量操作条/工具栏** | 图无第三层全宽栏；批量操作须可达 | 批量入口 = **菜单栏「操作」+ 卡片右键 + 选中浮层批量条（≤36px，覆盖在 C 区顶部）**，不新增全宽行 | §11.3、§11.6、FR-CARD-04 |
| 7 | 状态栏 6 段（新鲜度/健康/队列/CI/通知/网络） | 状态栏显示**最近一次操作的状态** | 状态栏 = **左段“最近一次操作”+ 右段原 6 段**（左段必现） | FR-OPS-05、§11.0.2 G |
| 8 | 全局工具栏承载搜索/筛选/排序/视图/刷新/多选 | 菜单栏之下为**标题区域**（产品名/版本/组件数） | 搜索/筛选/视图切换/刷新/通知/设置**并入标题区 B**，不新增第三条全宽 Toolbar | §11.0.2 B、§11.0.4 |
| 9 | 看板行点击进入组件详情**页** | 双击卡片可进入详情（图未定义详情形态） | 组件/单仓详情采用**右侧抽屉**（宽 480–720px），不替换主界面、返回保留全部状态 | FR-DETAIL-01、§11.0.4 |
| 10 | CI-1 左侧导航 / CI-3 组件详情 / CI-4 单仓 Tab(第4位) / CI-7 看板筛选 | 新布局无左侧导航与看板表格 | **10 个 CI 锚点全部迁移新落点（10/10）**：菜单栏/标题区入口、右侧信息面板、单仓详情抽屉、标题区筛选面板；CI 仍不参与聚合与卡片底色 | §11.4、FR-CI-01 |

> 其余非冲突旧条款（状态色板 §11.2、CI 数据契约、首扫进度协议、七态）**继续有效**，仅补充新落点说明。

#### 11.0.2 区域定义（A–G；1440×900 基线，顺序不可交换）

| 区域 | 名称 | 位置/形状 | 内容（数据源） | 尺寸 / 滚动 / 缩放 | 关键交互 / 验收 |
|---|---|---|---|---|---|
| **A** | 菜单栏 | 最上，通栏，高 32px（≤40） | 菜单：文件（打开工作区/首扫/退出）、查看（工作台 `Ctrl+1` / 文件夹视图 `Ctrl+2` / 刷新 `F5` / CI 流水线）、操作（组件级批量，见 §11.6）、工具（任务中心/日志与诊断/诊断导出）、设置、帮助（快捷键/关于） | 不滚动；200% 下文字不裁切，菜单项超宽时菜单栏内部横滚 | 点击/Alt+字母展开；AC-UI-01/13 |
| **B** | 标题区 | A 之下，通栏，高 56px（≤64） | 产品名 + 版本号；组织 / 工作区根（截断 + tooltip）；组件计数（可见/总数）；**视图切换**（工作台/文件夹视图）；搜索框；筛选按钮（弹出面板，含 CI facet 占位）；刷新；通知铃铛（未读角标）；设置齿轮 | 固定不滚动；搜索框 240–480px；1280 宽时次要按钮可收进「更多」菜单，但**搜索与视图切换必须常显** | 搜索 IME 规则同 FR-BOARD-02；AC-UI-01/13 |
| **C** | 组件列表卡片区 | 主区左侧，宽 20%（288px@1440，min 240 / max 360，可拖拽） | 组件卡片列表（FR-CARD-01）；选中 ≥1 时顶部叠加批量条（≤36px） | 独立纵向滚动 + 虚拟滚动；卡片宽随栏宽 220–360；200% 下卡片内字段换行，不横向溢出 | 单击选中 / 双击详情 / `Ctrl`·`Shift` 多选 / 右键批量；AC-UI-02 |
| **D** | 四仓文件树区 | 主区中间，宽 62%（自适应，≥560），**2 列 × 2 行等分** | 固定顺序：Design（左上）/ Impl（右上）/ MIL（左下）/ SIL（右下）；每格一棵只读树（FR-TREE-02） | 每格独立纵/横滚动；格间距 8px；每格 ≥260×220；组件切换每格重置“根展开”；缺仓显示空态 | 单击设聚焦仓 / 双击打开 / 展开折叠；AC-UI-03/04/05 |
| **E** | 右侧信息面板 | 主区右侧，宽 18%（260px@1440，min 220 / max 320，可拖拽） | 当前**聚焦仓**的基本信息 + 构建状态（CI 占位）+ 最近一次操作 +「详情 / 在资源管理器中打开」（FR-INFO-01） | 独立纵向滚动；标签列 ≤72px；不允许横向溢出；窄栏内字段换行规则固定 | 绑定唯一聚焦仓；未选中显示空态；AC-UI-11 |
| **F** | 底部单仓按钮条 | 主区之下、状态栏之上，通栏，高 56px（≤64） | `PULL` `FETCH` `CHKOUT` `COMMIT` `PUSH` 5 按钮（FR-ACT-01）+ 位于按钮上方的确认条（≤48px，仅 COMMIT/PUSH 出现） | 固定不滚动；按钮不换行；按钮最小 88×32；1280 下仍 5 个同排 | 未选中全部禁用；进行中 spinner；AC-UI-08/09/10 |
| **G** | 状态栏 | 最下，通栏，高 28px（≤32） | 左段「最近一次操作：…」（FR-OPS-05 模板）；右段 6 段：数据新鲜度 / 进程健康 / 操作队列 / CI 队列（v1 恒 0）/ 通知 / 网络状态（可点开明细） | 固定；右段超宽按优先级折叠进「更多」；200% 不溢出 | 失败可展开最近 5 条；AC-UI-01 |
| **H** | 抽屉 / 二级页面 | 覆盖层或整页（不属于 A–G） | 组件详情 / 单仓详情 / 任务中心 / 通知中心 / 日志与诊断 / 设置 / CI 流水线 / 未绑定仓库（§11.1） | 抽屉宽 480–720px、overlay 不改变 A–G 的 bbox；整页返回保留工作台状态 | ESC 关闭 + 焦点回落；AC-UI-13 |

> **参考图对位**：A=菜单栏、B=标题区域、C=组件列表卡片、D=Design/Impl/MIL/SIL 仓库文件列表（2×2）、E=选中仓库基本信息与构建是否成功、F=PULL/FETCH/CHKOUT/COMMIT/PUSH、G=状态栏。任何实现不得新增“左侧图标导航”“第三条全宽工具栏”“表格表头”等图中不存在的常驻区域。

#### 11.0.3 尺寸、滚动与缩放（可测口径）

1. **视口**：基线 1440×900（WebView2 内容区，不含 Windows 标题栏）；最小 1280×800；≥1920 时 C/E 取 max 宽、D 吃余量。
2. **高度（基线）**：A 32px、B 56px、F 56px、G 28px，主行（C/D/E）= 900−32−56−56−28 = 728px（≥600 为底线）；A/B/F/G 超出上限（40/64/64/32）即判布局不通过。
3. **宽度（基线）**：外边距 12px、面板间隙 8px；C=20%（240–360）、D=62%（≥560、自适应）、E=18%（220–320）；三者合计 + 间隙 + 边距在 1440 下无页面级横滚。分隔条宽 8px，可拖拽 C/E 宽度，双击恢复默认；宽度持久化到 `ui.panel.leftWidth`/`ui.panel.rightWidth`（附录 D）。
4. **滚动**：页面级不滚动（`document.scrollingElement` 无纵向/横向溢出）；C、D 每格、E 各自**单一滚动容器**（禁止面板内嵌套双滚动条）；卡片列表与树均虚拟化。
5. **缩放**：100%–150% 下 2×2 保持且无页面级横滚；175%–200% 下允许 D 区内部横向滚动（页面级仍无溢出），A/B/F/G 关键文字（含 5 个按钮标签）**不得省略或裁切**；RDP/低端机自动关闭动效（§10.5）。
6. **主题/密度**：浅色 + 深色 token 换值不换名（§11.2）；`ui.density=compact/standard` 仅影响行高与间距，**不得改变 7 区域结构**。

#### 11.0.4 交互规则（工作台统一模型）

1. **唯一选中模型**：`selection = { componentId, focusedRepoType | null }`。C 区单击卡片设置 `componentId`（`focusedRepoType` 默认为第一个存在仓）；D 区单击树节点设置 `focusedRepoType`；E 区信息面板、F 区按钮条、右键菜单**全部读取同一 selection**；禁止任一处另存副本（不一致即缺陷，AC-UI-11）。
2. **双击语义表**：组件卡片→打开组件详情抽屉；文件树文件→系统默认程序打开；文件树文件夹→默认程序（资源管理器）打开；目录展开箭头→展开/折叠（不打开外部程序）；空白区→无动作。双击**不得写入/移动/删除**任何文件。
3. **展开与加载**：默认每格展开仓库根 1 层；展开状态按组件 LRU 缓存（≥100 组件）；刷新只更新数据不重建树（保留展开与滚动位置）；组件切换 ≤300ms 内先用缓存渲染。
4. **右键菜单**：C 区卡片=组件级批量菜单（§11.6）；D/E 区=只读菜单（打开/复制路径/在资源管理器中打开/刷新该仓目录）；**任何右键菜单不得出现写文件操作**。
5. **抽屉**：以 overlay 打开，不改变 A–G 的 bbox；ESC 关闭并把焦点还给触发控件；抽屉内焦点陷阱，Tab 不逃逸到主界面（AC-UI-13）。
6. **视图切换**：`Ctrl+1` 工作台 / `Ctrl+2` 文件夹视图（或标题区切换器/菜单栏「查看」）；切换 ≤200ms；保留 selection 与展开路径；**两视图共享同一状态快照**（禁止各自计算，AC-UI-06/07）。
7. **七态**：C、D（每格）、E 各自具备 空/加载/错误可重试/离线降级/过期/部分失败/权限或认证过期；错误态必须含错误码与「重试」按钮；刷新期间不闪白、不丢焦点（AC-UI-02/11）。
8. **动效**：hover/press 120ms、展开/收起 240ms、抽屉与视图切换 200ms、单次 ≤320ms；`prefers-reduced-motion: reduce` 下 ≤1ms；列表仅 1 种入场动效。
9. **键盘**：见 FR-WS-05；快捷键不得与中文 IME 组合态冲突（组合期不触发全局快捷键）。
10. **状态栏文案**：左段模板固定为 `最近一次操作：<PULL|FETCH|CHKOUT|COMMIT|PUSH> <componentId>/<RepoType> — <成功|失败(错误码)|进行中> · HH:mm`；从未操作显示“无”；失败可展开最近 5 条（不可只显示图标）。

### 11.1 页面清单与导航（v0.3 重写；旧 App Shell 左侧导航废弃）

| 层 | 页面/区域 | 说明 |
|---|---|---|
| L0 | 首次配置向导、首扫进度页（**`LOCAL_DISCOVERY`→`REMOTE_RECONCILE`→`STATUS_CALC`**，可跳过远程降级）、故障页 | 不进入主界面；完成后进入工作台（文件夹视图不是首屏） |
| L1 | **工作台（默认主界面，7 区域 A–G）**、**文件夹视图（与工作台同级，`Ctrl+2`）** | 常驻；两者共享 A/B/F/G 与同一状态快照；视图切换在标题区与菜单栏「查看」 |
| L2 | 抽屉/二级页面：组件详情、单仓详情、CI 流水线（占位页）、任务中心、通知中心、日志与诊断、设置、未绑定仓库 | **一律经菜单栏 A 或标题区 B 进入**；不占主界面区域；不设左侧图标导航（旧 8 项导航废弃） |
| L3 | 每区/每页七态：空/加载/错误可重试/离线降级/过期/部分失败/**权限或认证过期** | 必备；每态均可重试 |

> **首扫进度协议（M0 冻结，H-02；与 §3.1、§9.10 FR-OPS-08、AC-OPS-15/16 逐字一致）**：阶段枚举固定为 `LOCAL_DISCOVERY` / `REMOTE_RECONCILE` / `STATUS_CALC`，按此顺序推进；每个进度事件固定字段 `{stage, total, current, cancellable}`——`stage` 只取上述三值，`total`/`current` 为当前阶段仓（或步骤）总数/已完成数，`cancellable` 表示该阶段是否支持合作式取消；远程阶段可跳过并进入只读降级，远程就绪后自动重判。

### 11.2 状态色板（双通道，唯一权威；v1 含深色）

> 通道 A（仓库生命周期）用于子仓 chip、卡片左轨/徽标与聚合徽标；**卡片底色永远中性**，禁止整卡饱和色。通道 B（CI）独立徽标，永不染卡片底色。颜色只作辅助，必须“图标 + 中文文案 + 颜色”三重编码。
>
> **工作台应用映射（v0.3）**：组件卡片状态徽标 / 四仓 chip / 4px 状态轨道 = 通道 A；文件树仓库根徽标 = 通道 A；CI 槽位 / 信息面板构建状态 / 状态栏 CI 段 = 通道 B（圆点/pill，形状与通道 A 不同）；**卡片与信息面板底色恒中性**；**选中态只用 info 底色 + 2px 焦点环表达，禁止用状态色表达“选中”**；文件树选中行用 info 浅底。§11.2 全部 token 与对比度门禁对工作台/文件夹视图同样适用。

**颜色族 token（浅色 fg/bg/border；深色 fg/bg）**

| token 族 | 浅色 | 深色 | 语义 |
|---|---|---|---|
| success | `#0F7B4F` / `#E7F5EC` / `#9CCFB8` | `#3DD68C` / `#10301F` | 正常/同步 |
| warning | `#8A4B00` / `#FDF1DC` / `#E8BE84` | `#F5B544` / `#33240B` | 落后/警告/进行中 |
| info | `#1D4ED8` / `#E8EEFE` / `#AEC3F5` | `#7AA7FF` / `#0F1E3D` | 正常开发态/CI 运行 |
| purple | `#6D28D9` / `#F3EAFE` / `#C9AEF0` | `#C4A2FF` / `#221239` | 分叉 |
| cyan | `#0F5F73` / `#E3F4F7` / `#9ACFD9` | `#5CC8E0` / `#0B2A33` | 分支/基线/游离 |
| neutral | `#374151` / `#F1F2F4` 虚线 / `#9CA3AF` | `#A7ADB8` / `#1E2128` | 缺失/空/中性 |
| danger | `#B91C1C` / `#FDECEC` / `#EFB4B4` | `#F87171` / `#3B1111` | 阻断/失败 |
| hatched（未知） | `#4B5563` + 斜纹 `repeating-linear-gradient(135deg,#F3F4F6 0 6px,#E5E7EB 6px 12px)`（对 `#E5E7EB` 6.1:1） | `#B6BDC9` / `#1F2430`（深色 8.2:1） | 未知/过期/无数据 |

**状态 → 颜色族/图标/文案（覆盖全部单仓主状态与组件聚合态）**

| 状态（单仓/组件） | 颜色族 | 图标+文案 |
|---|---|---|
| SYNCED | success | ✓ 同步 |
| BEHIND | warning | ↓ 落后 N |
| AHEAD | info | ↑ 超前 N（正常开发态，禁止告警样式） |
| DIVERGED | purple | ⇄ 分叉（需 Merge/Rebase） |
| MISSING | neutral（虚线） | ∅ 缺失 n/4（中性，4,000 仓多数未克隆） |
| EMPTY | neutral | ∅ 空仓库 |
| DIRTY | success + 变更徽标 | ● 有未提交改动 |
| DETACHED_TAG | cyan | ⚑ 基线 tag |
| DETACHED_COMMIT | cyan | ⌁ 游离 commit |
| NO_UPSTREAM / NEW_BRANCH | cyan | ⑂ 仅本地/未跟踪/远程已删 |
| UNMERGED | danger | ✕ 冲突未解决 |
| IN_PROGRESS | warning | ⏳ 合并/变基未完成 |
| INVALID / BINDING_ERROR | danger | ! 仓库绑定异常 |
| BLOCKED_LOCAL | danger | ✕ 本地冲突/操作未完成 |
| TAG_DRIFT | warning | ⚑ 基线不一致 |
| MIXED_BRANCH | cyan | ⑂ 分支不一致 |
| OP_PARTIAL_FAILED | danger | ! 部分失败 n/4 |
| REMOTE_GONE（flag） | danger | ! 远程仓库已删除 |
| BUSY（flag） | neutral | ⏳ 外部 Git 正在操作 |
| lfs_missing（flag） | danger | ! LFS 对象缺失 |
| tag_moved（flag） | warning | ⚑ tag 被移动 |
| STALE/UNKNOWN | hatched | ⏱ 状态未知（数据 X 前） |

**通道 B（CI）**：`PASS`=success 圆点✓ · `FAIL`=danger 圆点✕ · `RUNNING`=info 半环◐ · `QUEUED`=neutral 空心○ · `NONE`=neutral ∅ · `UNKNOWN`=hatched ?。CI 形状与通道 A 不同（圆点/pill），位置固定，不参与卡片底色。

**颜色纪律（写进验收）**
1. 红色只用于“阻断/失败”：INVALID/BINDING_ERROR/BLOCKED_LOCAL/UNMERGED/OP_PARTIAL_FAILED/REMOTE_GONE/lfs_missing/CI FAIL/破坏性确认按钮；**不得**用于 AHEAD、DIVERGED（紫）、MISSING（灰）。
2. 状态色只能来自本节 token（`--st-*` / `--ci-*` CSS 变量）；组件内硬编码 hex 视为不通过。
3. 所有 fg/bg、fg/卡片底色组合对比度 ≥4.5:1（非文本 ≥3:1）；交付前自动扫描 + deuteranopia/protanopia 模拟；**对比度计算脚本纳入 M0 门禁**（对 §11.2 全部 token 组合逐一断言，不达标即冻结不通过，H-04）。
4. 深色主题为 v1 必做（token 换值不换名）；`prefers-color-scheme`、`prefers-contrast`、`prefers-reduced-motion`、`forced-colors` 均需适配。

### 11.3 组件卡片与信息面板布局（v0.3 重写；旧表格行布局废弃）

> 旧 v0.2.1 §11.3「表格行（默认）+ 卡片视图（可选，宽 ≥320px/高 ≥104px）」**整体废弃**（D-023，与参考图左列窄卡片列表冲突）。v1 只有组件卡片列表一种列表形态。

- **组件卡片（C 区，宽随栏 220–360px、高 ≥72px，底色中性）**，固定 3 行信息，顺序不得调换：
  1. 行 1：组件 ID（超长省略 + tooltip 全名）+ 聚合状态徽标（图标+中文+通道 A 颜色）；
  2. 行 2：四仓 chip `D` `I` `M` `S`（各自主状态；缺失/未克隆用 neutral 虚线斜纹），chip 之间可 hover 显示单仓文案；
  3. 行 3：当前分支（`MIXED_BRANCH` 时 Impl 为主 + “分支不一致”角标）+ 变更标记 `●N`（tracked，untracked 另用 `○`）+ **CI 占位槽位（定高 16–20px，右对齐，“CI 未接入”）** + 新鲜度。
- 卡片左侧 4px 状态轨道（通道 A）用于扫视，**不做整卡底色**；hover/press 120ms；选中 2px info 焦点环 + `aria-selected`；卡片内任何字段不得用 `*` 表示变更。
- **四仓分支不一致**（组件卡片与信息面板同规则）：Impl 分支为主 + “分支不一致”角标 + hover/详情内四仓对照（FR-DETAIL-04）。
- **信息面板（E 区，220–320px，纵向滚动）**分三段：
  1. 选中仓基本信息：仓库全名、RepoType、主状态徽标 + 中文文案 + 数据时间、分支 / HEAD 短 SHA / upstream、ahead/behind/dirty 计数；
  2. 构建状态：CI 汇总条（高 40px，四仓 CI 计数 + 最近失败仓；v1 显示“CI 未接入”空态）+ 通道 B 徽标；CI 永不改变面板底色；
  3. 最近一次操作结果（同状态栏模板的简版）+ 快捷入口「详情」「在资源管理器中打开」。
- **组件级批量条（C 区顶部浮层，高 ≤36px）**：选中 ≥1 时出现，左侧“已选 N 组件 / M 仓”+ 动作按钮组 + “清除选择”，右侧“全选筛选结果”；**它是浮层，不占 C/D/E 布局高度**（出现/消失时 C/D/E bbox 不变）；阈值与动作见 FR-BOARD-04/FR-CARD-04。
- **v1.1 若恢复表格视图**：必须复用本节卡片字段、§11.2 token 与同一状态快照；不得另起一套布局或第二套状态计算（D-023）。

### 11.4 CI 预留 10 锚点（v0.3 新落点；编号 CI-1…CI-10 稳定，10/10 必须命中）

| # | 新落点（v0.3 工作台） | 原落点（v0.2.1，已废弃/迁移） | v1 交付物 | 不返工理由 |
|---|---|---|---|---|
| CI-1 | 菜单栏「查看 → CI 流水线」（或标题区 CI 入口图标）打开的 CI 页面/抽屉 | 左侧导航“CI 流水线”入口（导航已废弃） | 路由/抽屉骨架 + 空态 + 实验标记 | 二级入口后加要改菜单与路由 |
| CI-2 | 组件卡片行 3 的 CI 徽标槽位（定高 16–20px，右对齐） | 组件行/卡片 CI 徽标槽位 | 固定槽位 + “CI 未接入”空态 | 卡片布局最高频返工点 |
| CI-3 | 右侧信息面板 E 区顶部 CI 汇总条（40px） | 组件详情 CI 汇总条 | 四仓 CI 计数 + 最近失败仓（空态） | 窄栏顶部后插会挤掉仓信息 |
| CI-4 | 单仓详情抽屉第 5 个 Tab「CI」（Tab 顺序固定：概览/历史/差异/文件/CI） | 单仓详情 CI Tab（第 4 位） | 空态 + 字段占位表 | Tab 顺序后加要改路由 |
| CI-5 | 状态栏 G 区右段「CI 队列」段 | 状态栏 CI 队列段 | 图标 + 计数（v1 恒 0） | 状态栏分段后加要重排 |
| CI-6 | 通知中心抽屉的「CI 事件」类目 | 通知中心 CI 事件类目 | 类型枚举 + 空分组 | 通知 schema 后加要迁移 |
| CI-7 | 标题区 B「筛选」弹出面板中的「CI 状态」facet | 看板筛选 CI facet（表格已废弃） | 禁用占位 + tooltip | 筛选分组是布局契约 |
| CI-8 | 设置（菜单栏 → 设置抽屉/页面）「CI 集成」分区 | 设置 CI 集成分区 | URL/Token/轮询/开关（disabled）+ 实验标记 | 配置项后加要改存储 |
| CI-9 | 数据契约 `CiLatest`（`componentId`/数字 `repoId`）/`ci_status`/`ci_latest` 视图/`CI_UPDATED`/`ICiProvider`（契约内容不变，见下） | 同左 | Mock 实现 | 数据层后加要改表/IPC |
| CI-10 | 聚合规则显式分离：组件状态只由仓库状态决定，CI 独立字段；**工作台卡片底色/聚合徽标/文件树徽标均不含 CI** | 同左 | 契约文档 + 单测 | 混入后再拆要重做整块 UI |

> **锚点纪律**：CI-1…CI-10 是 v1 交付物，**10/10 必须有落点**；实现可调整编号但必须在 §11.4 与 AC-UI-12 同步并保持 10 个落点；不得以“新工作台没有左侧导航/表格”为由删锚点；CI 永不参与聚合与卡片底色（D-002、FR-CI-03）。

**CI 数据契约（冻结）**

```ts
type CiState = 'PASS' | 'FAIL' | 'RUNNING' | 'QUEUED' | 'NONE' | 'UNKNOWN';
interface CiLatest {
  repoId: number; componentId: string; repoType: 'DESIGN'|'IMPL'|'MIL'|'SIL';
  branch: string; commitHash: string;            // 必须绑定 commit，不是“仓库最新”
  state: CiState; pipelineId?: string; buildNumber?: number;
  failedStage?: string; startedAt?: string; finishedAt?: string;
  durationMs?: number; url?: string; provider: string;   // 'mock' | ...
  fetchedAt: string;                              // UI 必须展示新鲜度
}
interface ComponentCiAggregate {
  componentId: string; byState: Record<CiState, number>;
  worstState: CiState;                            // FAIL > RUNNING > QUEUED > UNKNOWN > PASS > NONE
  lastFailed?: Pick<CiLatest,'repoType'|'buildNumber'|'failedStage'|'url'>;
  dataFreshAt: string;
}
interface ICiProvider {
  getLatest(componentId: string): Promise<CiLatest[]>;
  getAggregate(componentId: string): Promise<ComponentCiAggregate>;
  subscribe(cb: (e: {type:'CI_UPDATED'; payload: CiLatest}) => void): () => void; // v1 空实现
}
```

**CI 预留验收（可自动化）**：10 锚点全部存在（UI 快照断言，AC-UI-12）；CI 槽位定高不换行；注入 CI FAIL 后组件卡片/信息面板底色不变；`ci.enabled=true` 后仅换数据、10 锚点位置与布局 bbox 不变；无裸 TODO 文本。

### 11.5 文件夹视图（工作区树，与工作台同级；v0.3 新增）

1. **布局**：A（菜单栏）、B（标题区）、F（按钮条）、G（状态栏）与工作台相同；**主区 C+D 合并为一棵工作区树（约 82%，原左侧卡片区 + 中间四仓树区）**，右侧仍保留 **E 信息面板（18%）** 显示选中仓信息；F 按钮条对树中选中的仓库节点生效（与工作台同一 selection 模型）。
2. **树结构（自上而下）**：`workspace_root` → 组件目录（component_id）→ 四仓目录（Design/Impl/MIL/SIL）→ 文件目录树；组件层最多 1 层、仓库层最多 1 层，文件层可无限展开；**未克隆组件**显示“未克隆”淡显节点（不可展开）；**未纳管目录**归入“未纳管”分组（只读展示，不参与聚合与卡片）。
3. **数据与状态**：组件节点显示聚合状态徽标 + 四仓 chip；仓库节点显示主状态徽标；节点状态与工作台**来自同一状态快照**（同一次 `calc_at`），同一夹具两视图必须逐字一致；STALE 节点灰化 + 数据时间。
4. **展开/性能**：懒加载（展开 ≤300ms）、虚拟滚动（400 组件/1,600 仓 ≥58fps，NFR-PERF-03）；展开状态独立于工作台缓存，切换视图保留选中并定位节点。
5. **双击/只读**：双击文件/文件夹规则同 FR-TREE-03；路径安全同 FR-TREE-04；**文件夹视图无任何写入口**（无新建/重命名/删除/拖放）。
6. **过滤/搜索**：按聚合状态/RepoType/有本地改动/未克隆过滤；搜索组件 ID/仓名/文件名（防抖 120ms，p95 ≤300ms）；文件夹视图的筛选状态独立保存，不污染工作台筛选。
7. **切换**：`Ctrl+1` 工作台 / `Ctrl+2` 文件夹视图（或标题区切换器/菜单栏「查看」）；≤200ms；保留 selection 与展开路径；**旧看板表格视图 v1 不提供入口**（不得把“文件夹视图”当作旧表格视图的替代说法）。
8. **验收**：AC-UI-06（结构/状态一致/过滤/双击/性能）、AC-UI-07（切换）。

### 11.6 组件级批量操作入口与二级入口（不破坏参考图布局）

1. **批量操作入口（3 处，同一动作清单）**：
   - ①**菜单栏「操作」菜单**：对当前选中组件集合生效；未选中时整菜单禁用并给出“请先选择组件”；
   - ②**组件卡片右键菜单**：对右键卡片所在选择集生效；
   - ③**选中浮层批量条**（C 区顶部，高 ≤36px）：出现时不改变 C/D/E 的 bbox，不新增全宽工具栏；
   动作清单 = Clone / Pull / Fetch / Checkout 分支 / 切 Tag / 受控 SYNC / Delete / Restore / Create 分支（含 dry-run 预览与确认阈值 FR-BOARD-04）；批量执行可跳转任务中心查看逐仓结果。
2. **二级入口（统一经菜单栏/标题区，不占主界面区域）**：任务中心（菜单「工具」/状态栏队列段）、通知中心（标题区铃铛/菜单）、日志与诊断（菜单「工具」）、设置（菜单/标题区齿轮）、CI 流水线（菜单「查看」）、未绑定仓库（菜单「工具」/标题区异常计数）；均为抽屉或二级页面，打开不改变 A–G 的 bbox。
3. **快捷键表（工作台 + 文件夹视图通用）**：`F5` 刷新当前视图、`Ctrl+F` 聚焦搜索、`Ctrl+1` 工作台、`Ctrl+2` 文件夹视图、`Ctrl+Shift+F` 打开筛选面板、`Esc` 关闭抽屉/清除批量选择、`↑/↓` 组件卡片或树节点移动、`Enter` 打开（卡片=详情 / 文件=默认程序）、`空格` 选中。
4. **验收**：AC-UI-02（批量入口与布局不变）、AC-UI-13（二级入口可达/抽屉不改变主区）。

### 11.7 旧设计废弃/降级速查（回归断言，供 AC-UI-14）

| 旧 v0.2.1 条款 | v0.3 处置 | 回归断言（CDP/截图） |
|---|---|---|
| §11.1 App Shell 左侧图标导航（8 项） | **废弃** | 页面不存在左侧图标导航条；二级入口均在菜单栏/标题区 |
| §11.1 全局工具栏（搜索/筛选/排序/视图/刷新/多选） | **并入标题区 B** | 不存在第三条全宽工具栏；搜索/视图切换在 B 区 |
| §11.3 表格行默认视图 / 卡片视图可选 | **废弃/降级 v1.1** | 无表格表头/表格行；默认即组件卡片列表；表格视图入口不存在 |
| §11.3 卡片视图宽 ≥320px/高 ≥104px | **重写** | 卡片宽度随 C 栏 220–360 自适应、高 ≥72px |
| 旧右侧详情列 340px | **重写为信息面板 18%（220–320px）** | 主区右列宽在区间内；详情以抽屉打开 |
| 旧文件树仅在单仓详情内 | **迁移为主界面 D 区 2×2** | 工作台中间存在 4 棵固定顺序树；单仓详情文件 Tab 复用 |
| 旧 CI-1 左侧导航 / CI-3 组件详情 / CI-4 单仓 Tab(第4位) / CI-7 看板筛选 | **迁移新落点** | §11.4 表 10/10 命中（AC-UI-12） |
| 旧状态栏无“最近操作” | **扩展为左段最近操作 + 右段 6 段** | 状态栏左段文案模板存在 |

---

## 12. 架构方案（设计建议，非需求；按架构评审 03 推荐并入，D-011）

> 需求层只约束：UI 不阻塞、长任务可后台执行/可取消、崩溃可恢复、状态不撒谎、数据可迁移可重建。以下为实现基线，允许架构评审替换但需满足上述约束。

### 12.1 进程与职责（v1：2 个 OS 进程）

| 进程 | 职责 | 数据权限 |
|---|---|---|
| `MbdGitTool.Host.exe` | 生命周期/监督/调度（`RemoteSync`、`LocalScan`、`StatusCalc` 三个 `BackgroundService`）、**唯一 DB 写者**、**唯一持有凭据并发起 Gitea API 请求**、git 任务编排、日志/健康上报、托管 WebView2 窗口 | 唯一可写 DB；唯一可发 Gitea REST 请求 |
| `MbdGitTool.Git.exe` | 唯一执行 `git.exe` 的常驻 worker：clone/fetch/pull/checkout/tag/commit/push/reset/status/rev-list（**含网络型 git 命令**）；每仓互斥；进度回传；取消时杀整棵进程树 | **不碰 DB**；只接 job、回结构化事件 |
| `msedgewebview2.exe` | OS 管理的 WebView2 渲染进程 | 无业务权限 |

- **网络边界澄清（RT-07）**：“Host 唯一网络请求”仅指 **Gitea REST / CI API**；git 的 clone/fetch/pull 属 GitWorker 执行，凭据由 Host 经 `GIT_ASKPASS`/管道一次性下发，不落盘、不进命令行。
- **fetch → 状态刷新链路（必须实现）**：Host 下发 fetch job → GitWorker 执行并回传每个 ref 的 SHA/结果 → `RemoteSync` 在 Host 内写 `remote_ref`（`observed_at` 更新，`generation+1`）→ 触发 `StatusCalc` 重算受影响组件；GitWorker 永远不直接写库。
- **CAS 作用域（RT-08）**：写操作预检与执行时比较 `HEAD sha + index tree hash + 工作区 dirty 摘要` 三元组；任一变化 → `CONCURRENT_CHANGE` 失败，需重新预检。跨进程互斥 = Host 内 per-repo 队列 + GitWorker 每仓锁 + 外部 `.git/index.lock` 检测（检测到即 BUSY，**禁止删锁**）。

- **不设**聚合/对比/每类轮询进程：聚合是 Host 内纯函数/SQL 视图（400 行输入 → 100 行输出，µs 级）；对比/扫描是 Host 内 `BackgroundService`。
- **监督**：Host 为父进程，用 Job Object（`JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`）托管 GitWorker；重启退避 1s→2s→4s→…→60s，**5 分钟内失败 3 次熔断降级**并在状态栏显示原因；Host 崩溃写转储，下次启动 `PRAGMA quick_check` + 未完成 job 标 `INTERRUPTED`。
- **单实例**：命名 Mutex + 管道名（按 SID/AppGuid）；工作区 `.mbdgit.lock` 文件锁；双开提示并退出。
- **形态**：托盘应用、登录自启、关窗退托盘（`[待确认]` OQ-11：托盘/服务/仅前台）；**不做 Windows 服务**（Session 0 拿不到映射盘/DPAPI、无法弹 UI）。
- **退出**：停调度 → 取消 job → 杀进程树 → checkpoint DB。

### 12.2 通信

| 链路 | 方式 |
|---|---|
| UI ↔ Host | 同进程 WebView2 `PostWebMessageAsJson` / `WebMessageReceived`；消息 `{id,type,payload}`；事件 `job.progress / status.changed / health.changed`（节流 100ms） |
| Host ↔ GitWorker | 命名管道 `\\.\pipe\mbdgit-{SID}-{AppGuid}`（ACL 仅当前用户），长度前缀 JSON 或 `StreamJsonRpc`；`Hello{protocolVersion}` → `RunJob` → `Progress` → `Done`；控制调用 10s 超时；无进度 30s 判死并杀进程树；队列满回 BUSY |
| Host ↔ Gitea | HTTPS + `Authorization: token <PAT>`；并发闸门 6–8；单请求 15s；429/403/5xx 指数退避+抖动 |
| GitWorker ↔ git.exe | `Process` + `ArgumentList`（**禁止 shell 字符串拼接**）；`-C <path>` + `--` 分隔；`GIT_TERMINAL_PROMPT=0`；只读命令 `GIT_OPTIONAL_LOCKS=0`；只读 30s / 网络 10min；取消杀进程树。**Host 不直接持有 git 子进程** |
| 可选 dev 通道 | `127.0.0.1` 随机端口 + 启动令牌（仅测试/自动化，生产关闭） |

### 12.3 数据层

- **SQLite WAL + `Microsoft.Data.Sqlite` + Dapper + 手写迁移**；锁 bundled SQLite ≥ 3.51.3；单写者（详见第 8 章、评审 03 §C）。DB 放 `%LOCALAPPDATA%\MbdGitTool\`，禁止网络盘/漫游目录。
- 排除 LiteDB（跨进程弱）、DuckDB（单写者/偏 OLAP）、Realm（已弃用）、LMDB（无 SQL/迁移生态）、Postgres/嵌入式 SQL Server（部署过重）。草案“优于 SQLite”为伪需求，改为可验收选型约束：单文件、ACID、WAL 并发读、schema 版本化迁移、热备份、零服务进程、可重建。

### 12.4 采集策略（分层拉模式，替代 6h/120s + 30s 全量轮询）

- 层级与频率见 7.5：远程清单水位 10min（80 请求）→ 仅变化仓 refs（30min/按需）→ 每日全量对账可续传（P95 ≤5min，无 120s 硬超时）；每轮写 `sync_run`，覆盖率 <100% 标 stale。
- 增量抓手：仓库清单 `updated_at` 水位；**不假设**列表端点提供可用的 `ETag/If-None-Match` 语义（以每仓 `updated_at` 比较兜底）。
- 本地：L1 指纹 15s（stat 6 个文件）→ L2 仅指纹变化仓跑一次 `git --no-optional-locks status --porcelain=v2 --branch`（8 路）+ `FileSystemWatcher`；热仓 5s / 冷仓 6h。
- Webhook：v1 不依赖（桌面端通常收不到入站回调）；预留 `event_log`，v2 接入只加数据源不改采集主干。
- 团队规模：100 人 × 每 6h × 4000 仓 ≈ 18 req/s 持续，需与 Gitea 管理员协商配额/白名单（`[待确认]` OQ-17）。

### 12.5 技术栈与交付

- 运行时 **.NET 10 LTS** self-contained x64 + ReadyToRun；UI **WebView2 Evergreen** + 启动检测 + 随包离线 Standalone 安装器（常驻应用注意 Runtime 更新需重启）。
- 宿主 WPF；前端 TypeScript + Vue/React + Vite（前端栈为设计建议）。
- git：检测 Git for Windows ≥ 2.40（含 Git LFS、GCM）并引导安装（`[待确认]` OQ-16：检测 vs 捆绑 PortableGit）。
- 日志 Serilog：分级、滚动 10MB×30 天、审计独立 sink 保留 ≥90 天；健康快照协议 `health{name,state,lastSuccessAt,lastError,restarts,queueDepth}`（≤1Hz 推 UI）；一键脱敏诊断包（日志+配置+DB 快照+版本+最近 job）；托管/原生崩溃转储。
- 打包：MSIX 用户级 + EV 签名 + 更新前等 job 空闲并备份 DB + 失败回滚；企业 SCCM 可另出 MSI（`[待确认]` OQ-13）。

### 12.6 安全基线

- 凭据：**Windows Credential Manager 为对外方案**（DPAPI 加密文件作为等价内部实现细节），target `MbdGitTool/Gitea/{host}/{user}`；配置文件只存引用与尾 4 位；Token 只随 `Authorization: token <PAT>` header 发送（唯一写法）。
- Token 最小 scope：同步用 `read:repository`，push 用独立 `write:repository`（读写分离）；过期 → degraded + 重登。
- 传输：仅 HTTPS；企业代理/自签 CA；schannel 或按次 `-c http.sslCAInfo`；**禁止** `sslVerify=false` 兜底。
- 日志脱敏：`Authorization`、`token=`、`access_token=`、URL userinfo、`git remote -v` 输出、git stderr。
- 破坏性操作：dry-run → 影响清单 → 二次确认（输入组件 ID）→ 执行 → 审计；删除只本地且进 `.trash`（默认 30 天，可配）；覆盖前 read-through。
- 路径安全：canonical 校验拒绝 `..`、绝对路径、保留名（CON/PRN/AUX…）、尾随空格/点、大小写冲突；最终路径必须在根之下；拒绝 reparse 越界；240 字符预检 + longPathAware/core.longpaths。
- 权限：不以管理员运行；不改系统级 git config；`safe.directory` 只提示不放宽。
- 安装：代码签名与更新包校验；用户级安装。

### 12.7 数据落点

```
%LOCALAPPDATA%\MbdGitTool\        app.db / secrets.dat / config.json / logs\ / audit\ / dumps\ / diag\
<workspace_root>\<仓库全名>\      由工具克隆/管理；.mbdgit.workspace 标记文件
<workspace_root>\.trash\<timestamp>\   删除回收站（同卷 rename，秒级）
```

### 12.8 架构评审 16 项决策的处置

已按推荐并入本文：Q1 两进程（D-011）、Q3 SQLite、Q4 单写者、Q5 CI 关闭、Q7 取消 120s、Q8 新鲜度分档、Q9 凭据、Q10 token scope、Q12 删除、Q14 诊断、Q15 单用户。仍需用户确认：**Q2 常驻形态、Q6 远程 SLO、Q11 多根（与用户 D-013 单根冲突，以用户决策为准）、Q13 打包、Q16 git 依赖、Q17 Gitea 配额**（见 15 章）。

---

## 13. 验收标准与测试策略

> **权威 AC 目录 = `reqs/v0.2/ac-catalog.md`（v0.2 口径：162 条 AC，覆盖 72 条 FR，含 FR→AC 追溯矩阵与红队逐项处置；v0.3 另增 32 条 FR 与 §13.1.1 的 14 条 AC-UI，回写口径见 §13.4 末注）**。**自 v0.2.1 起，AC 目录新编号（`AC-<域>-<NN>`，如 `AC-OP-15`）为唯一权威；旧编号仅在 §13.1 作历史映射，不得再用于验收或里程碑出口引用**；两者冲突时以 AC 目录为准。执行图集见 `reqs/v0.2/diagrams.md`（D1–D6），状态矩阵夹具见 `reqs/v0.2/fixtures/`，冻结门禁见 `reqs/v0.2/freeze-checklist.md`。
> **v0.3 例外（UI 部分）**：v0.3 新增的 **AC-UI-01…AC-UI-15（§13.1.1）** 尚未回写进 `ac-catalog.md`；在回写完成前，**UI 布局/交互/文件树/文件夹视图/单仓按钮/CI 锚点以 §13.1.1 为权威**，其后恢复“ac-catalog 唯一权威”口径（回写责任与验收见 §13.4 末注）。
> **v0.3.1 例外（M2a/M2b 部分，D-025）**：新增/修订的 **AC-UI-10a/10b/10c/15、AC-SAFE-14…17、AC-OP-33** 的**权威定义统一在 §13.1.2**（§13.1.1 的 AC-UI-10 仅保留拆分引用）；该 9 条尚未回写 `ac-catalog.md`，回写完成前以 §13.1.2 为权威，回写时保持编号不变（AC-OP-33 为 AC 目录下一个未占用序号）。**M2a 出口以 §13.1.1 + §13.1.2 的 AC 子集为唯一依据，不得用“M2 全量 AC 未过”或“234 单测绿”替代。**

### 13.1 AC 清单（v0.1：27 条旧编号；红队复审后补；M0 冻结为 Given/When/Then 全量版）

> 〔**旧编号清单，仅作旧→新映射与历史追溯**；新编号唯一权威、旧号仅映射（映射见 `reqs/v0.2/ac-catalog.md` §1.4）。验收一律使用 ac-catalog 新编号。〕

| AC | Given/When/Then |
|---|---|
| AC-01 组件识别 | Given 远程存在 `CF325F_ChanganSigProcgBascFct_{Design,Impl,MIL,SIL}`，When 组织同步覆盖率 100%，Then 工作台出现 1 个组件且 component_id = `CF325F_ChanganSigProcgBascFct`，不出现 `CF325F` 组件 |
| AC-02 缺仓 | Given 组件只有 3 仓，Then 组件 `BINDING_ERROR` + 缺失仓可见，批量操作只作用于 3 仓并逐仓提示 |
| AC-03 未绑定 | Given 组织存在 `X_Docs`、`Y_Design2`、`X_SIL_Design`，Then 全部进入“未绑定仓库”清单，不静默丢弃、不影响工作台 |
| AC-04 状态矩阵 | Given 附录 C 状态矩阵夹具（14 主状态 × 关键 flag × 15 聚合态），When 扫描/聚合，Then 每个夹具命中唯一主状态与预期聚合态；任一步失败为 UNKNOWN 而非旧值 |
| AC-05 无假绿 | Given 远程不可达/超 TTL，Then 组件不得显示 SYNCED，显示 STALE/UNKNOWN 与数据时间；Given 仅离线本地事实，Then INVALID/MISSING/EMPTY/UNMERGED/IN_PROGRESS/DETACHED 可正常判定 |
| AC-06 关系判定 | Given `compare_ref` 三种来源（upstream / 同名 remote 分支 / 缺失），When 计算，Then 结果与 `merge-base --is-ancestor` 一致且区分退出码 0/1/>1；未推送 commit 不调用 compare API |
| AC-07 SYNC 保护 | Given 仓库有未推送 commit/dirty，When 执行 SYNC，Then 展示丢失清单、默认阻断、写入备份 ref/stash，确认后可从备份恢复 |
| AC-08 删除可恢复 | Given 组件存在未推送或 dirty，When 删除，Then 默认阻断；强制删除进入 `.trash` 且 30 天内可恢复；Gitea 远端零变更 |
| AC-09 部分失败 | Given 4 仓 2 成功 2 失败，Then 任务中心逐仓结果、组件 OP_PARTIAL_FAILED、可仅重试失败项；清除失败态需显式确认 |
| AC-10 切 Tag | Given 4 仓均存在、其中 3 仓有 Tag v1.0 且另 1 仓在默认分支（无该 tag），When 批量切 Tag v1.0，Then 3 仓 detached 检出并写 `baseline_snapshot`（含 tag_present），第 4 仓报告缺 tag 并跳过；组件 `TAG_DRIFT` |
| AC-11 首启三阶段 | Given 新机器，When 首次启动，Then 向导→三阶段进度（`LOCAL_DISCOVERY`→`REMOTE_RECONCILE`→`STATUS_CALC`，事件含 `{stage,total,current,cancellable}`）→ 可跳过远程进入只读降级；远程就绪后自动重判 |
| AC-12 CI 预留 | 按 **AC-UI-12（§13.1.1）**：10 锚点新落点 10/10 存在、卡片槽位定高不换行、CI FAIL 不染卡片底色、`ci.enabled` 开关仅换数据、无裸 TODO |
| AC-13 性能 | 按 13.3 测量方法与数据集：正常档 1,000 组件、压测档 4,000 组件，首屏 ≤1.5s、滚动 ≥58fps、搜索 p95 ≤300ms |
| AC-14 安全 | 工具自身产物（日志/诊断包/转储/命令行）扫描无 token；Gitea 请求仅 Authorization header；既有仓内嵌 token → 告警 + 脱敏展示 + 不自动改用户配置；删除/SYNC 场景 Mock 断言 Gitea 无写请求 |
| AC-15 单实例 | 双开第二个实例提示退出且不写数据；强杀后 stale lock 可自愈 |
| AC-16 状态×操作矩阵 | 7.6 矩阵抽样每行至少 1 个夹具；被禁止的操作返回明确错误码且仓库零改动 |
| AC-17 CREATE 分支 | 1 仓重名时 dry-run 明确；逐仓创建 + 部分失败可撤销；创建基线 ref 记录 |
| AC-18 修复类操作 | INVALID/NO_UPSTREAM/DETACHED/EMPTY 四类夹具各执行 Repair/set-upstream/建分支/首推，状态转移正确 |
| AC-19 恢复 | 删除后从 `.trash` 恢复；SYNC 后从 `refs/backup/*` 恢复未推送 commit；恢复前展示冲突并确认 |
| AC-20 纳入/移出管理 | 按组件 ID 纳入（校验四仓）→ 工作台出现；移出后 DB 状态（archived/删除）与工作台一致；均有审计 |
| AC-21 远程未就绪 | Given 跳过远程阶段，Then 本地仓不得被判为 INVALID/MISSING（正确态为 `UNKNOWN` + `incomplete=1`）；远程恢复后组件自动重判并更新 |
| AC-22 搜索/筛选/排序 | facet 枚举与排序键按 FR-BOARD-02；搜索 title/分支/Tag/SHA 命中正确；默认排序=严重度→组件 ID |
| AC-23 未绑定仓库入口 | 清单页/抽屉存在；按原因分组；可重新解析；忽略需记录理由 |
| AC-24 通知/设置/日志 | 通知持久化并已读/清除；设置键见附录 D 且非法值被拒；日志滚动/脱敏/诊断包可导出 |
| AC-25 权限/认证过期 | 401/403/Token 过期时显示权限或认证态并可重登；恢复后任务可续跑 |
| AC-26 外部 git 并发 | 外部持有 `.git/index.lock` 时工具写操作返回 BUSY、不删锁、仓库无改动 |
| AC-27 崩溃恢复 | 写操作中强制终止进程，重启后 journal reconcile 不猜测成功；重复执行幂等 |

### 13.1.1 v0.3 UI/交互 AC（新增，CDP/截图可测；工作台/文件夹视图/文件树/单仓按钮/CI 锚点）

> 本节 **AC-UI-01…AC-UI-14** 为 v0.3 §11 重构后的 UI 验收权威源；与 `reqs/v0.2/ac-catalog.md` 中 AC-BOARD/AC-DETAIL/AC-CI 的**布局与交互断言冲突时以本节为准**（ac-catalog 由并行角色按本节回写；回写完成前本节即 v0.3 UI 验收源）。驱动方式固定为 **WebView2 CDP**（`WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--remote-debugging-port=9222`）+ `Input.dispatchMouseEvent` / `Input.dispatchKeyEvent` 真实事件（不用 JS click）+ DOM bbox/`scrollWidth` 断言 + 截图归档 `docs/images/ui-v3/`。**D-025 新增/拆分的 AC-UI-10a/10b/10c/15 不在本表重复定义，统一见 §13.1.2（避免双源）；M2a 出口 AC = AC-UI-08/09/10/10a/10b/11/13/15 + §13.1.2 的 AC-SAFE-14…17 与 AC-OP-33；AC-UI-10c 属 M2b。**

| AC | Given/When/Then（可测） |
|---|---|
| AC-UI-01 工作台布局 | Given 1440×900、1280×800、150%、200% 视口，When 打开工作台，Then A–G 7 区域全部存在、顺序 A→B→(C\|D\|E)→F→G、C/D/E 同一行且 D 为 2×2；各区域高/宽落在 §11.0.3 区间；`document.scrollingElement.scrollWidth === clientWidth`（无页面级横滚）；键盘 Tab 顺序 A→C→D→E→F→G（跳过纯展示状态栏）、焦点环 2px 可见（FR-WS-05）；截图与参考图逐区域对比（位置/层级/内容类别）通过 |
| AC-UI-02 组件卡片列表 | Given 1,000 组件快照，When 渲染工作台，Then 左侧卡片列表为唯一默认列表视图（**无表格表头/表格行**）、卡片含 §11.3 三行字段、卡片底色中性、默认排序 = severity_rank 升序→组件 ID；滚动 10s ≥58fps；批量条出现时 C/D/E bbox 不变；菜单栏/右键/批量条 3 个批量入口可达；**卡片区七态（空/加载/错误可重试/离线/过期/部分失败/权限过期）注入后均可渲染并重试**（FR-CARD-05）；旧 AC-BOARD-01 的表格断言作废 |
| AC-UI-03 四仓文件树 | Given 选中一个 4 仓组件，When 展开/单击/双击，Then D 区为 Design(左上)/Impl(右上)/MIL(左下)/SIL(右下)；每格懒加载 ≤300ms、排序=目录优先→名称升序、默认根展开 1 层；单击设聚焦仓（E 面板与 F 按钮条同步）；双击文件调用系统默认程序（CDP 拦截 shell 调用并断言参数为 canonical 路径）、双击文件夹走资源管理器；缺仓格显示空态且不伪造树内容 |
| AC-UI-04 文件树只读与路径安全 | Given 构造 `..`、绝对路径、UNC、reparse 越界、保留名（CON/NUL）、尾随空格请求，When 调用文件树/打开文件 API，Then 全部拒绝并返回统一错误码且不泄露越界内容；Given 文件树 DOM，Then 不存在新建/重命名/删除/拖放/在树内编辑入口；右键菜单仅只读项 |
| AC-UI-05 文件树状态标记 | Given 附录 C 状态夹具，When 打开文件树，Then 仓库根徽标=该仓主状态（§11.2 token/图标/文案）、tracked 改动文件 `●`、untracked `○`、STALE 灰化+数据时间；展开文件树不新增 git 进程（进程计数断言） |
| AC-UI-06 文件夹视图 | Given 工作区根含 1 个未克隆组件 + 1 个未纳管目录，When 切换文件夹视图，Then 树层级为 根→组件→四仓→文件；未克隆淡显、未纳管入分组；组件/仓库节点状态与工作台同快照逐字一致；过滤与搜索生效；400 组件/1,600 仓滚动 ≥58fps |
| AC-UI-07 视图切换 | Given 工作台已选中组件 X 且展开某仓目录，When `Ctrl+2` 切到文件夹视图再 `Ctrl+1` 返回，Then 切换 ≤200ms、selection 与展开路径保留、两视图状态一致；**旧看板表格视图入口不存在**（D-023） |
| AC-UI-08 单仓按钮条 | Given 工作台且未选中聚焦仓，Then 5 按钮全部禁用并提示“请先在文件树中选择仓库”；Given 选中仓，Then 按 §7.6 逐格断言启用/禁用（14 主状态 × 5 按钮矩阵）；按钮顺序/文案固定 `PULL→FETCH→CHKOUT→COMMIT→PUSH`、不换行、1280 下不隐藏；**M2a 出口的逐格期望一律取单源夹具 `reqs/v0.2/fixtures/m2-matrix.json`（Host 单测与 CDP 共用，禁止第三份手写期望）**，Host `GetAvailability` 的 `enabled/requiresConfirm/reasonCode` 与 UI 逐格一致，禁用 tooltip 冻结格式见 §13.1.2 前言（`原因 [E_*] → 替代动作`） |
| AC-UI-09 M1 只读边界 | Given M1 构建，When 点击任一底部按钮，Then 5 按钮全部不可执行（含 FETCH）、只给出“M1 只读”理由，写操作数=0、仓库零改动；Given M0 构建，Then 按钮禁用 + 占位说明；Given **M2a 构建**，Then 按 FR-ACT-02…04 与 FR-ACT-05/06 的 **M2a 子集**逐项启用（取消/job/任务中心除外，归 M2b，见 §13.1.2 AC-UI-10c），且每次点击必须有可感知结果（执行/确认条/明确错误码/禁用原因/M2b 未接入提示），禁止“可点无效果”；Given M2b 构建，Then 按 FR-ACT-02…06 全量启用 |
| AC-UI-10 按钮前置/确认/失败 | Given 无 staged 的 DIRTY 仓，Then COMMIT 禁用并给原因；Given 非快进 PULL，Then 显示 ff-only 失败指引且无 merge commit；Given PUSH 无 upstream，Then 提示“发布分支”；Given COMMIT/PUSH，Then 确认条出现且未确认时写操作数=0；Given 外部 `index.lock`，Then 返回 BUSY、不删锁；Given 操作失败，Then 状态栏显示错误码+中文文案且可重试；取消/进行中语义符合 FR-ACT-05；完成后 ≤2s 回写并写审计。**v0.3.1 拆分（D-025）**：确认条细则拆为 **AC-UI-10a（COMMIT，对话框→确认条两步）/ AC-UI-10b（PUSH）**，取消/job 归 **AC-UI-10c（M2b）**；三者定义见 §13.1.2，本行只保留 M2a 通用前置/失败/回写断言 |
| AC-UI-11 右侧信息面板 | Given 选中仓，Then E 区显示 §11.3 三段字段（含主状态+数据时间、CI 空态）；未选中显示引导空态且无残留数据；同一时刻 E 与 F 指向同一聚焦仓；1280/200% 下窄栏不横向溢出、标签列 ≤72px；详情抽屉开/关后 A–G bbox 不变 |
| AC-UI-12 CI 10 锚点新落点 | Given v1 构建，Then §11.4 的 CI-1…CI-10 **全部存在（10/10）**；CI 槽位定高不换行；注入 CI FAIL 后组件卡片/信息面板底色与聚合徽标不变；`ci.enabled=true` 后仅数据变化、锚点位置与布局 bbox 不变；全站无裸 TODO |
| AC-UI-13 二级入口与抽屉 | Given 工作台，Then 任务/通知/日志/设置/CI/未绑定 6 个入口经菜单栏或标题区可达（6/6）；抽屉打开时 C/D/E bbox 不变、ESC 关闭且焦点回落、焦点陷阱生效；**无左侧图标导航、无第三条全宽工具栏** |
| AC-UI-14 旧布局废弃回归 | Given v1 构建，Then 页面不存在左侧图标导航、表格表头/表格行、340px 详情列；默认入口为工作台；§11.7 速查表逐项 CDP 断言通过；截图区域顺序与参考图一致 |

### 13.1.2 v0.3.1 M2a/M2b 新增与拆分 AC（AC-UI-10a/10b/10c/15、AC-SAFE-14…17、AC-OP-33；权威定义）

> 本小节是 D-025 冻结的 **M2a 出口 AC 中新增/拆分项的唯一权威定义**（2026-11-14 Gate G-P0-4 回写；此前本节缺失，导致 M2a 出口 8 条 AC 不可独立验收）。§13.1.1 的 AC-UI-10 行只保留“拆分引用”，不再重复定义；本节与 §13.1.1 冲突时以本节为准，二者均与 `reqs/adr/D-025-m2a-m2b.md` §2.2/§3/§5 一致，**不得以任何理由放宽安全要求**。
>
> **M2a 出口 13 条**（与 ADR §3 逐条对应）= §13.1.1 的 `AC-UI-08/09/10/11/13` + 本节的 `AC-UI-10a/10b/15`、`AC-SAFE-14/15/16/17`、`AC-OP-33`。**`AC-UI-10c` 属 M2b**：M2a 验收报告不得把 10c、`AC-OP-01…32`、`AC-REPO-01…11`、`AC-SAFE-01…13` 记为通过或“等价通过”。
>
> **单源矩阵夹具（v0.3.1 冻结）**：14 主状态 × 5 键的 `enabled/requiresConfirm/reasonCode` 一律取 `reqs/v0.2/fixtures/m2-matrix.json`（由 §7.6 与 D-025 冻结口径派生）；**Host 单测（`RepoActionAvailabilityMatrixTests` 等）与 CDP 脚本 `ui/scripts/cdp-m2a-selfcheck.mjs` 共用该夹具，禁止第三份手写期望**。`requiresConfirm` 规则（逐格冻结在夹具内）= **矩阵 C 单元 ∨ 动作 ∈ {COMMIT, PUSH}**；`reasonCode` ∈ `AVAILABLE / CONFIRM_REQUIRED / STATE_BLOCKED`。夹具的 M2a 构建口径含两处冻结覆盖：① `DIVERGED.PULL` 按禁用（原 ⚠ 作废）；② `EMPTY.PUSH` 按“空仓无提交”禁用且 tooltip 指向 M2b 首推（M2b 构建恢复矩阵）。
>
> **禁用 tooltip 冻结格式（v0.3.1 修订）**：固定 `原因 [E_*] → 替代动作`，例：`无已暂存改动：请先 git add 暂存后提交 [E_NOTHING_TO_COMMIT] → 先 git add 暂存改动`。旧的冒号三段式 `原因：…；替代：…；错误码：…` **作废**（避免 85 个按钮 title 过长）；错误码仍须为 Host `E_*`，附录 D 映射走 `details.srsCode`。CDP 断言正则：`/^.+ \[E_[A-Z_]+\] → .+$/`。
>
> **COMMIT 两步（v0.3.1 修订）**：AC-UI-10a 的点击路径固定为「**提交信息对话框 → 确认条 → 执行**」两步（旧“点击按钮直出确认条”字面作废），确认条文案以 `将提交 N 个文件` 为准；未确认 0 写、ESC 0 写、5 键 bbox 不变等安全不变量**不变**。
>
> **证据要求**：逐 AC 证据、复跑命令与“未通过/待验证”状态见 `docs/m2a-ac-report.md`；真实 Gitea 写操作证据不得用 Mock 回执替代（Mock 只允许用于注入 14 状态/错误态）。M2a 报告须按 ADR §2.2 末注写明 FR-ACT-05/06 仅 M2a 子集、离线 FETCH 子项与 FR-REPO-05 其余修复类归 M2b。

#### AC-UI-10a · COMMIT 确认条：对话框（提交信息）→ 确认条（将提交 N 个文件）→ 执行
- **覆盖 FR**：FR-ACT-04、FR-ACT-03、FR-ACT-06
- **测试层级/依赖**：UI/CDP（真实 Host 快照；`C-SYNTH`/附录 C `F-*` 夹具注入 `stagedCount=N≥1` 的状态；确认真实写回执另见 AC-UI-10 与 `docs/m2a-ac-report.md`）
- **Given**：选中一个矩阵允许 COMMIT 且有已暂存改动的仓（`stagedCount=N≥1`，如 SYNCED/DIRTY 夹具）；`runRepoAction` 桥调用计数清零；记录 5 键 `[data-testid=repo-actions]` 的 bbox。
- **When**：① CDP 真实鼠标点击 `[data-testid=action-commit]`；② 在 `[data-testid=action-dialog]` 的 `[data-testid=action-dialog-message]` 输入 `m2a-gate`（不勾选 `[data-testid=commit-stage-all]`）；③ 点击 `[data-testid=action-dialog-confirm]`（文案「下一步：确认提交」）；④ 在确认条上按 `Escape` 或点 `[data-testid=confirm-bar-cancel]`；⑤ 重走 ①→③ 后点 `[data-testid=confirm-bar-accept]`；⑥ 另起一轮勾选 `commit-stage-all` 后接受。
- **Then**：① 步骤①只出现 COMMIT **提交信息对话框**，**不得直接出现确认条、不得发起 `runRepoAction`（两步中的第一步）**；② 对话框正文/信息格含「已暂存 N 个文件 / 将提交 N 个文件」，与快照 `stagedCount` 一致；③ 步骤③后 `[data-testid=confirm-bar]` 可见、高度 ≤48px，`[data-testid=confirm-bar-text]` 匹配 `/将提交 \d+ 个文件/`（`\d+`=N；「已暂存」不得插入「N 个」与「文件」之间，可在「个文件」后以括注补充）；④ 步骤③后 5 键 bbox（x/y/w/h）与开条前逐像素一致，确认条不遮挡/不移动按钮；⑤ 步骤③后 `runRepoAction` 计数=0；步骤④后仍=0 且确认条消失、焦点回到 `[data-testid=action-commit]`；⑥ 步骤⑤后恰 1 次 `runRepoAction(COMMIT,{message:'m2a-gate'})`（未勾选时**无** `stageAll`），消息内容不含 token；⑦ 步骤⑥的 args 含 `stageAll:true,confirm:true`，且对话框 hint 明示将执行 `git add -A`（未勾选时不得出现 `-A` 行为）；⑧ 全程无 `[data-testid=confirm-dialog]` 打断、无 console error。
- **关联 NFR/OQ**：FR-ACT-04、FR-ACT-03；D-025 §2.5-②；Gate G-P1-1（旧字面/文案偏差）

#### AC-UI-10b · PUSH 确认条：零确认写禁止 + NO_UPSTREAM 发布分支
- **覆盖 FR**：FR-ACT-04、FR-ACT-03、FR-REPO-05（M2a 唯一子项）
- **测试层级/依赖**：UI/CDP（真实 Host 快照 + `NO_UPSTREAM`/`DETACHED_*`/`AHEAD` 夹具注入；桥拦截计数）
- **Given**：选中 `AHEAD`/`SYNCED` 仓（`ahead=N≥1`、upstream 可解析）；另有 `NO_UPSTREAM` 与 `DETACHED_TAG` 夹具；`runRepoAction` 计数清零；记录 5 键 bbox。
- **When**：① 点击 `[data-testid=action-push]`；② 未确认按 `Escape`；③ 重开条后点取消；④ 在非输入焦点按 `Alt+5`；⑤ 重开条后点 `[data-testid=confirm-bar-accept]`；⑥ `NO_UPSTREAM` 夹具重走 ①→⑤；⑦ DETACHED 夹具查看 `action-push` 禁用态与 title。
- **Then**：① 仅出现 `[data-testid=confirm-bar]`（无对话框/confirm-dialog），高度 ≤48px，文案匹配 `/将推送 \d+ 个提交（[^（）]+ → [^（）]+）/`；② 5 键 bbox 开条前后一致；③ 步骤①–④全程 `runRepoAction` 计数=0（**禁止零确认写**），ESC/取消后条消失、焦点回 `action-push`；④ `Alt+5` 与点击同路径（同样开条、未确认 0 写，**不得绕过确认条**）；⑤ 步骤⑤后恰 1 次 `runRepoAction(PUSH,{})`；⑥ `NO_UPSTREAM` 条含「发布分支」「建立 upstream（setUpstream=true）」文案，接受后 `args.setUpstream===true`；⑦ DETACHED PUSH `disabled=true` 且 title 匹配 `/^.+ \[E_[A-Z_]+\] → .+$/`；⑧ 确认条接受按钮可键盘操作（Tab/Enter），焦点陷阱不逃逸。
- **关联 NFR/OQ**：FR-ACT-04；D-025 §2.5-④；Gate G-P0-1/NP0-1

#### AC-UI-10c · 取消与 job（**M2b，不计 M2a 出口**）
- **覆盖 FR**：FR-ACT-05、FR-ACT-06
- **测试层级/依赖**：E2E/UI（M2b 构建；`G-E2E` 批量夹具 + `CLK`）
- **Given**：M2b 构建；批量 PULL 作业 4 仓（2 仓已开始、2 仓未开始）；任务中心可打开。
- **When**：① 任务中心点“取消”；② 取消结束后点“仅重试失败项”；③ 重启 Host 后查任务历史；④ 订阅 `JOB_UPDATED`。
- **Then**：① 返回 `jobId`；取消后 job 状态 `CANCELLED`，已开始仓记 `CANCELLED` 并触发重扫、未开始仓保持原状（**不整体回滚**），终态以实际扫描为准；② `JOB_UPDATED` 含 `{jobId,state,current,total,cancellable}` 且 `cancellable=false` 于取消后；③ 重启后 job 不重复执行、历史可查且状态一致；④ “仅重试失败项”只对失败/取消仓发新 item，不复跑成功仓；⑤ M2a 构建中本 AC **不验收**，验证方法仅需确认入口给出“M2b 未接入（批量/作业/审计）”提示（见 AC-UI-15）。
- **关联 NFR/OQ**：FR-ACT-05/06；ADR §2.3/§4（M2b 出口）

#### AC-UI-15 · M2b 边界：批量/任务中心入口明示“M2b 未接入（批量/作业/审计）”
- **覆盖 FR**：FR-ACT-07、FR-CARD-04、§11.6
- **测试层级/依赖**：UI/CDP（M2a 构建；真实 Host + 桥计数）
- **Given**：M2a 构建；批量条 `[data-testid=batch-bar]`、菜单批量项、任务中心 `[data-testid=title-tasks]`、组件卡片快速入口均可见；`runRepoAction`/`getJob`/`listJobs` 计数清零。
- **When**：① 依次点击批量条 `SYNC`/`CREATE`/`CLONE`/`DELETE`；② 菜单栏“批量”各项；③ 点 `[data-testid=title-tasks]` 打开任务中心，点“了解批量入口”与“取消 / 重试失败项”；④ 点卡片快速 `SYNC`/`CREATE`。
- **Then**：① 每次点击都出现**可见文案包含“M2b 未接入（批量/作业/审计）”**（括号内文字不可省略/改写）；② `runRepoAction`、`getJob`、`listJobs` 增量均为 0；③ 不得静默无响应（每次点击有 toast 或抽屉文案变化）；④ 不得伪造成功（不出现“已提交/已同步/已完成”等成功回执）；⑤ C/D/E 主区 bbox 不变（入口不改变布局）；⑥ 页面不存在“M2 接入”“M2 接入后”等旧措辞命中。
- **关联 NFR/OQ**：FR-ACT-07；ADR §2.2-8/§2.4-1；Gate G-P0-3

#### AC-SAFE-14 · 凭据作用域：URL 不匹配 → E_CREDENTIAL_SCOPE，配置不完整 → fail-closed
- **覆盖 FR**：FR-SAFE-06、FR-ACT-03
- **测试层级/依赖**：Host 单测/黑盒集成（临时 git 仓 + 回环 HTTP stub 抓包 + `GIT_TRACE2_EVENT` argv 审计）
- **Given**：token 哨兵 `ghp_CANARY_…` 与合法 `GiteaBaseUrl/GiteaOrg`；临时仓 `origin`/`remote.origin.pushurl` 分别构造为：① fetch host 不一致；② pushurl 指向其它 port/org；③ 配置缺 base 或 org + 有 token；④ 明文 http 未显式 `allowInsecureHttp`；⑤ fetch/push URL 与 base 完全一致（正例）。
- **When**：分别执行 FETCH/PULL/PUSH，并采集：①②的抓包请求数与 Host 实际启动的 git 子命令 argv；③④⑤的返回码与响应 JSON；⑤时检查发出的 `Authorization` 头与 URL 作用域。
- **Then**：① fetch or push URL 与 Gitea base（scheme/host/port/org 前缀）不一致 → `E_CREDENTIAL_SCOPE`，`details` 仅含 failedFields/scheme/host/port/路径前缀，**不含 URL/token**；抓包请求数=0，argv 中无 fetch/push（`rev-parse`/`remote` 之外不启动网络 git）；② pushurl 不一致同样 `E_CREDENTIAL_SCOPE` 且 0 请求；③ 配置缺/非法 + 有 token → `E_GITEA_CONFIG_INVALID`（fail-closed），0 网络请求；④ 明文 http 未显式允许 → `E_GITEA_INSECURE_HTTP`，0 网络请求；⑤ 仅 canonical base 收到 `Authorization: Basic base64(token:)`，且注入方式为 `http.<base>.extraHeader` URL 作用域（其它 host/org 收不到）；⑥ argv、Host 日志、响应 JSON、remote URL 中 token/base64 哨兵命中=0。
- **关联 NFR/OQ**：FR-SAFE-06、D-006、§10.3；ADR §2.2-1；Gate P0-1

#### AC-SAFE-15 · 写前 read-through 重扫 + HEAD CAS：E_CONCURRENT_CHANGE、零改动、scan_at 前进
- **覆盖 FR**：FR-SAFE-03、FR-ACT-03
- **测试层级/依赖**：Host 单测/黑盒集成（临时 git 仓；`CLK`/扫描时间戳可读）
- **Given**：宿主完成一次扫描形成快照（HEAD=`H0`、`scan_at=T0`）；外部进程在不通知 Host 的情况下将 HEAD 改为 `H1`（另备 dirty/upstream 变化样本）。
- **When**：对**旧快照**分别执行 PULL/CHKOUT/COMMIT/PUSH（`confirm=true`），并比对执行前后 `git rev-parse HEAD`/`git status --porcelain=v2`/分支/远端 refs 与 `local_state.scan_at`。
- **Then**：① 执行时 HEAD≠快照 HEAD → `E_CONCURRENT_CHANGE`，四动作逐项均拒绝；仓库 HEAD/分支/index/工作区/远端 refs **零改动**；② 动作前 **read-through 重扫**成立：`local_state.scan_at` 单调前进（`T1>T0`）且**矩阵判定基于动作前状态**（非 TTL 旧快照）；非 HEAD 变化（dirty/upstream/remote）同样在动作前被重算捕获并给出对应拒绝码，**不得仅以动作内部 git 报错兜底**（“安全失败”不能替代本断言）；③ 动作成功后快照失效，≤2s 内回写新状态；④ 快照 TTL 不作为门禁依据。
- **关联 NFR/OQ**：FR-SAFE-03、§7.5；ADR §2.2-2；Gate P1-2/N-2（实现偏差须在 `docs/m2a-ac-report.md` 显式记录，不得据此放宽本 AC）

#### AC-SAFE-16 · ignored 文件不被静默覆盖（CHKOUT/PULL）
- **覆盖 FR**：FR-SAFE-03、FR-REPO-01
- **测试层级/依赖**：Host 单测/黑盒集成（临时 git 仓；ignored 内容哨兵 `LOCAL-PRECIOUS`）
- **Given**：clean 仓 + `.gitignore` 忽略 `build.log`（内容 `LOCAL-PRECIOUS`）；目标分支/远端更新中含同路径 **tracked** 文件。
- **When**：① CHKOUT 到目标分支；② PULL（远端已含该 tracked 文件）；③ 采集实际 git argv。
- **Then**：① CHKOUT 命令必须含 `--no-overwrite-ignore`；PULL 必须为 `fetch` + `merge --ff-only --no-overwrite-ignore`（**不得**用不接受该开关的 `git pull`）；② git 拒绝覆盖时返回 `E_WORKTREE_OVERWRITE`（或等价明确错误码，不得静默成功），`build.log` 内容仍为 `LOCAL-PRECIOUS`、HEAD/分支未变、数据零丢失；③ 任何路径都不得出现 `-f`/`--force` 覆盖；④ 失败时状态栏/响应含可读原因，仓库零改动。
- **关联 NFR/OQ**：FR-SAFE-03；ADR §2.2-2③；Gate P1-1

#### AC-SAFE-17 · ref 校验：不存在 ref / 前缀注入 / PULL 显式分支 / detached PUSH / 无 force
- **覆盖 FR**：FR-ACT-03、FR-REPO-01、FR-SAFE-03
- **测试层级/依赖**：Host 单测/黑盒集成（临时 git 仓 + `GIT_TRACE2_EVENT`）
- **Given**：仓在 `main`，含 `README.md`（同名文件但无同名 ref）、tag `v1.0.0`、远端新分支 `feature`（`main` 为其祖先）、detached 夹具、可正常 push 的仓。
- **When**：① CHKOUT `branch="README.md"`；② CHKOUT `tag="v9.9.9"`；③ CHKOUT/PULL `branch="+main"`/`"-main"`；④ PULL `args.branch="feature"`（当前分支 `main`）；⑤ DETACHED 状态 PUSH；⑥ 正常 PUSH 并采集 argv。
- **Then**：① `E_REF_NOT_FOUND`（kind=branch），HEAD/分支不变（禁止“假成功”走路径模式）；② `E_REF_NOT_FOUND`（kind=tag）；③ `E_INVALID_ARGUMENT`；④ **拒绝**（`E_ACTION_NOT_ALLOWED` 或 `E_INVALID_ARGUMENT`，details 给 expected/current），当前分支**不得**被快进到 `origin/feature`，且拒绝发生在任何 git 网络命令之前（argv 无 fetch/push）；⑤ `E_ACTION_NOT_ALLOWED`，HEAD 不变；⑥ PUSH argv 无 `--force`/`-f`/`--mirror`/`--delete`/前导 `+`；⑦ 仅远端存在的分支不得静默 DWIM 成假成功：要么真实建本地跟踪分支（需 M2a 明确支持并记录），要么 `E_REF_NOT_FOUND` + UI 明确文案。
- **关联 NFR/OQ**：FR-REPO-01；ADR §2.2-2④；Gate P1-3/N-1（N-1 未修复前本 AC 判未通过）

#### AC-OP-33 · 审计-lite：每次单仓动作恰 1 行 operation_log
- **覆盖 FR**：FR-ACT-06、FR-OP-09（M2a 子集）
- **测试层级/依赖**：Host 集成/E2E（真实 Host + SQLite `operation_log`；成功与失败样本）
- **Given**：Host 数据目录可写（`%LOCALAPPDATA%\MbdGitTool\`，以实现落点为准）；分别准备：① 成功 COMMIT/PUSH；② 失败动作（`E_REPO_BUSY` 锁冲突、`E_CONCURRENT_CHANGE`、`E_REF_NOT_FOUND` 各 1）；记录动作前 `operation_log` 行数。
- **When**：逐个执行动作后查询 `select ts,actor,target_json,before_json,after_json,action,result,error_code,request_id from operation_log order by id desc limit 1`；同一 request_id 前后行数比对；重启 Host 后再次查询。
- **Then**：① 每个动作**恰写 1 行**（同一 `request_id` 不产生 0 行/多行；重试产生新行并独立审计）；② 最小语义字段齐全且可读：时间、actor、组件 ID、RepoType、动作、操作前 HEAD、操作后 HEAD、结果、错误码、request_id（若物理列采用 §8.1 的 `target_json/before_json/after_json`，必须能通过 SQL JSON 投影得到同名字段，语义不丢）；③ 成功与失败**都写**；失败时 `error_code` 非空、result 可区分成功/失败；④ 重启 Host 后审计行仍可查（持久化），同一 request_id 不重复；⑤ 审计不含 token/Authorization 明文；⑥ M2a 仅要求“一条最小列审计”，查询/导出/≥90 天保留归 M2b（不得以 M2b 能力缺失判 M2a 通过或失败混淆）。
- **关联 NFR/OQ**：FR-ACT-06、FR-OP-09；ADR §2.2-6；Gate G-P0-2

> **M2a 出口不覆盖（诚实口径，禁止写成通过）**：AC-UI-10c（取消/job/任务中心）、`AC-OP-01…32`、`AC-REPO-01…11`、`AC-SAFE-01…13`、故障注入全集、离线 FETCH 子项、FR-REPO-05 其余修复类、写前 journal/崩溃 reconcile —— 均属 M2b 出口。M2a 报告只能写“M2a 子集已验证 + 上述未覆盖”。

### 13.2 测试策略

1. **Gitea fixture**：Windows 可跑的 Gitea 单二进制（`gitea.exe web` + 内置 SQLite）做 E2E；**WireMock.NET** 做契约测试（分页/`x-total-count`/429/401/5xx/超时/半包/慢响应）；
2. **本地 git 夹具生成器**：覆盖附录 C 的状态矩阵（分支/tag/annotated tag/detached/dirty/untracked/unmerged/rebase 中/shallow/LFS/大小写/长路径/中文路径/index.lock/reparse point）；
3. **状态机单测**：主状态表逐条 + 聚合全序 + UNKNOWN 兜底 + 新鲜度；判定纯函数与“真值机”对拍 + property-based 随机提交图；
4. **时间与调度**：`IClock` 假时钟，断言 15s/10min/TTL/退避曲线；
5. **端到端**：9.7/9.8 每条操作 + 崩溃恢复（Windows 强制终止进程）+ 断网 + 部分失败 + 外部 git 并发；
6. **UI**：**CDP 真实事件驱动**（§13.1.1 AC-UI 全套：布局 bbox/顺序、卡片列表、2×2 文件树、文件夹视图、视图切换、底部按钮矩阵、信息面板、10 个 CI 锚点、6 个二级入口、旧布局废弃）；锚点存在性快照、视觉回归（**状态×CI 组合矩阵** ≥12 张，基于 11.2 全量色板 + 参考图对比截图）、虚拟滚动性能、reduced-motion/深色/高对比/200% 缩放；
7. **性能**：BenchmarkDotNet 夜间基线 + 合成数据集（附录 C），按 13.3 测量方法，门禁写入 CI；
8. **安全**：token 泄漏扫描、路径穿越、危险扩展名、凭据告警、转储脱敏。

### 13.3 性能测量方法与数据集（可复现）

| 指标 | 测量方法 | 数据集/环境 |
|---|---|---|
| 首屏/滚动/搜索 | WebView2 DevTools Performance 时间线 + 滚动 10s 采帧；`keyed diff` 断言；**工作台 7 区域布局用 DOM bbox + 截图对比参考图** | 4,000 组件合成数据（正常 1,000 组件 / 压测 4,000 组件两档） |
| 本地扫描 | `stopwatch` + 进程启动计数；断言不起 git 进程的轮询 | 400 个本地仓夹具（`git init` + 若干 commit） |
| 远程同步 | WireMock/Gitea 服务端计时 + 客户端 `sync_run` 记录 | 4,000 仓元数据（模拟分页 80 页）+ 429 注入 |
| DB 写入 | BenchmarkDotNet 批写基准 | 12 万 ref 行 + 400 状态行 |
| 测试机基线 | 8C/16G/SSD/Windows 11；报告注明杀软与磁盘型号 | CI runner 固定规格或人工基准机 |

### 13.4 AC 追溯矩阵（FR 分组 → 新 AC；M0 冻结；旧号仅映射）

| FR 分组 | 覆盖新 AC（权威：`ac-catalog.md` §3 FR→AC 矩阵 + 本文件 §13.1.1/§13.1.2） | 缺口处理 |
|---|---|---|
| FR-CFG | AC-CFG-01…AC-CFG-11 | — |
| FR-SYNC | AC-SYNC-01…AC-SYNC-14 | — |
| FR-SCAN | AC-SCAN-01…AC-SCAN-13 | — |
| FR-STA | AC-STA-01…AC-STA-15；跨域 AC-SCAN-09、AC-DETAIL-02、AC-OP-11/12 | — |
| FR-BOARD | AC-BOARD-01…AC-BOARD-16（**表格相关断言由 AC-UI-02 取代**）；跨域 AC-STA-08、**AC-UI-01/02/07** | 旧表格默认视图断言标记作废（D-023） |
| FR-DETAIL | AC-DETAIL-01…AC-DETAIL-09；跨域 AC-SAFE-10、**AC-UI-03/04/11/13** | 详情抽屉化由 AC-UI-11/13 覆盖 |
| FR-OP | AC-OP-01…AC-OP-32；跨域 AC-SYNC-05/14、AC-SCAN-06、AC-OPS-04 | — |
| FR-REPO | AC-REPO-01…AC-REPO-11；跨域 AC-OP-05、**AC-UI-08/10** | 底部按钮为单仓操作 v1 入口 |
| FR-SAFE | AC-SAFE-01…AC-SAFE-17（**v0.3.1 新增 AC-SAFE-14 凭据作用域 / 15 CAS / 16 ignored / 17 ref 校验，定义见 §13.1.2，M2a 出口**）；跨域 AC-OP-16/20/26、AC-CFG-06、AC-DETAIL-06/07、AC-OPS-06、**AC-UI-04** | M2a 出口=AC-SAFE-14…17；AC-SAFE-01…13 归 M2b 出口，不得混记 |
| FR-OPS | AC-OPS-01…AC-OPS-17；跨域 AC-SAFE-06、**AC-UI-13**（二级入口）、**AC-UI-01**（状态栏模板）、**AC-OP-33**（v0.3.1 新增审计-lite，定义见 §13.1.2，M2a 出口） | M2a 只要求“一次操作一条最小列审计”；查询/导出/90 天保留归 M2b |
| FR-CI | AC-CI-01…AC-CI-11；跨域 AC-DETAIL-03、AC-OPS-02、**AC-UI-12**（10 锚点新落点） | CI-1/3/4/7 位置迁移由 AC-UI-12 断言 |
| **FR-WS** | **AC-UI-01、AC-UI-05、AC-UI-07、AC-UI-13** | v0.3 新增；ac-catalog 回写待并行角色同步 |
| **FR-TREE** | **AC-UI-03、AC-UI-04、AC-UI-05** | 同上 |
| **FR-FOLDER** | **AC-UI-06、AC-UI-07** | 同上 |
| **FR-ACT** | **AC-UI-08、AC-UI-09、AC-UI-10、AC-UI-10a、AC-UI-10b、AC-UI-15**（v0.3.1 拆分/新增定义见 §13.1.2；AC-UI-10c 属 M2b） | M1 只读边界=AC-UI-09；M2a 写操作=AC-UI-08/10/10a/10b；M2b 边界=AC-UI-15；取消/job=AC-UI-10c |
| **FR-CARD** | **AC-UI-01、AC-UI-02、AC-UI-07** | 同上 |
| **FR-INFO** | **AC-UI-03、AC-UI-11、AC-UI-12** | 同上 |
| NFR | 性能：AC-BOARD-03/05/14、AC-SCAN-08/09、AC-SYNC-12、AC-DETAIL-04、**AC-UI-01/02/06**；安全：AC-SAFE-05/06/07/12/13、AC-CFG-06/07、**AC-UI-04**；可靠/并发：AC-OPS-09/10、AC-OP-29、AC-SAFE-08/09 | — |

> **v0.3 UI AC 回写说明**：`reqs/v0.2/ac-catalog.md` 当前仍为 v0.2 版本（未含 AC-UI-01…14 与 §13.1.2 的 9 条）。v0.3 的 UI 验收以 **§13.1.1 为权威源**、v0.3.1 新增/拆分项以 **§13.1.2 为权威源**；AC 目录、图集（D1–D6）与夹具由并行角色按本节回写后，§13.4 才恢复“ac-catalog 唯一权威”的口径。**回写完成前，M0/M1/M2 出口引用 AC-UI 编号须同时注记本文件 §13.1.1/§13.1.2。**
>
> **v0.3.1 M2a 门禁（D-025，Gate G-P0-4 回写）**：M2a 出口 = **13 条 AC 全绿 + 每条都有可复跑证据**：`AC-UI-08/09/10/10a/10b/11/13/15`（§13.1.1 + §13.1.2）+ `AC-SAFE-14/15/16/17` + `AC-OP-33`（§13.1.2）。证据物三件缺一不得宣布 M2a 通过：① `docs/m2a-ac-report.md`（逐 AC 证据 + 真实 Gitea 五键写 + 审计行查询）；② `reqs/v0.2/fixtures/m2-matrix.json`（14×5 单源，**Host 单测与 CDP 共用**，禁止第三份手写期望）；③ `ui/scripts/cdp-m2a-selfcheck.mjs`（旧 `cdp-m2-selfcheck.mjs` 作废，不得作为证据）。**M2a 通过 ≠ M2 交付完成**；M2b 未接入前只能写“M2a 通过、M2b 未接入”。

> **门禁**：M0 结束时上表所有“缺口处理”关闭并冻结 AC 版本；无 AC 的 FR 不得进入 M2；M3 以“AC 基线冻结版本全绿”为出口（不是“第 13 章全绿”的移动靶）。

---

## 14. 风险登记（Top）

| 风险 | 影响 | 缓解 |
|---|---|---|
| 4,000 仓 API 限额/耗时超预期 | 首启不可用 | 分页/条件请求/并发上限/部分成功 + 覆盖率提示；仅对本地仓拉分支/Tag |
| fetch 全量成本高 | 磁盘/网络/CPU | 分批巡检 + 可见优先 + 手动刷新 + 数据新鲜度透明 |
| Windows 长路径/LFS/大小写 | 克隆失败、文件不可用 | NFR-COMPAT-03/05；预检与错误码；CI 夹具覆盖 |
| 用户外部 git 并发 | 数据损坏 | BUSY 态 + 跨进程锁 + CAS + 禁止删锁 |
| 破坏性操作误伤 | 丢代码/失去信任 | 备份 ref + dry-run + 默认软删除 + 二次确认 |
| CI 接入返工 | 整块 UI 重做 | 10 锚点 + 契约 + 聚合分离（v1 验收）；v0.3 已按新工作台重新落点（§11.4） |
| **参考图仅为示意图（比例/像素/右侧窄栏宽度有歧义）** | UI 返工、验收扯皮 | D-022 明确“图为准但不像素级复刻”：尺寸用 §11.0.3 区间 + AC-UI-01 的 CDP bbox/截图对比断言；偏差走 ADR 记录而非口头 |
| **新工作台造成旧 AC/评审材料失配**（ac-catalog/diagrams/ui-review 仍描述旧看板） | 验收口径冲突、追溯断裂 | §13.1.1 暂为 v0.3 UI 权威源；ac-catalog/图集由并行角色回写；§13.4 与 §11.7 提供旧→新断言映射 |
| **文件树/文件夹视图在大目录或深目录下卡顿** | 体验差、看起来“假死” | 懒加载 + 虚拟滚动 + 单次 ≤500 子项 + 超时错误可重试；NFR-PERF-03/09 与 AC-UI-03/06 实测 |
| **底部按钮误触/silent failure** | 误写仓库、信任受损 | M1 只读全禁用（D-024）；M2 确认条 + §7.6 唯一映射 + 状态栏回执；禁止“可点无效果”（AC-UI-09/10） |
| 需求继续发散 | 范围失控 | 本文档 + Out-of-Scope 表 + 变更记录 |

---

## 15. 开放问题（按优先级）

**已关闭（按推荐并入本文）**：OQ-01 目录布局→D-013（用户确认 A）；OQ-02 比较基准→D-014；OQ-03 缺仓策略→D-015；OQ-04 删除保留期→D-017（30 天）；OQ-08 深色主题→v1 必做（11.2 已含深色 token）；OQ-10 架构→D-011；OQ-13 打包→MSI/MSIX 双产物（默认 MSIX 为主）；OQ-14 git 依赖→D-019（检测引导）。

| # | 问题 | 推荐默认 | 是否阻塞 |
|---|---|---|---|
| OQ-05 | 容量口径：1,000 组件/4,000 仓（按 4× 压测）是否准确？ | 是 | 否 |
| OQ-06 | 未来 CI 系统：Gitea Actions / Jenkins / 其他？ | 保持 provider 抽象，v2 再定 | 否（v2） |
| OQ-07 | 是否允许工具自动后台 fetch（网络流量）？ | 允许，默认 10min，可配/可关 | 否 |
| OQ-11 | 常驻形态：托盘随登录自启（关窗退托盘）/ Windows 服务 / 仅前台？ | 托盘 + 登录自启（架构评审 Q2） | 否 |
| OQ-12 | 远程新鲜度 SLO：清单水位 10min + refs 30min + 每日全量，是否接受？ | 接受（架构评审 Q6） | 否 |
| OQ-15 | 是否向 Gitea 管理员申请 API 配额/白名单（100 人 × 6h × 4000 仓 ≈ 18 req/s）？ | 申请（评审 Q17） | 否（影响现场体验） |
| OQ-16 | 是否允许“关闭 UI 后后台继续常驻同步”？ | 关窗退托盘仍可继续（与 OQ-11 联动） | 否 |
| OQ-17 | 是否接受“多根目录”作为 v1.1（当前单根平铺）？ | 接受，接口预留 | 否 |
| OQ-18 | RDP 多会话/共享工作区是否允许写操作？ | 禁止写，只读展示并提示 | 否 |
| OQ-19 | Windows 10 22H2 ESU 环境是否需要支持？ | v1 仅 Windows 11 x64（D-018） | 否 |
| OQ-20 | 四仓是否真的存在“长期缺仓”的组件？（影响缺仓中性严重度是否足够） | 缺仓视为 BINDING_ERROR（中性），若属常态再改为正常态 | 否 |
| OQ-21 | 组件状态严重度全序（`severity_rank`）是否按 §7.4 的 0–14 号执行？ | 按 §7.4 表（0 最高，14 最低） | 否（M0 冻结） |
| OQ-22 | untracked 文件是否计入 DIRTY（`dirty.includeUntracked`）？ | 默认 false（仅 tracked 改动算 DIRTY；untracked 用 flag） | 否（M0 冻结） |
| OQ-23 | 错误码全表（含可重试性/日志级别/用户文案）何时冻结？ | M0 冻结（附录 D 先给示例） | 否 |
| OQ-24 | P2 FR（FR-CFG-06 / FR-DETAIL-05 / FR-REPO-04 / FR-OPS-07）版本归属？ | 全部 v1.1（v1 只保留入口不存在/不可用；完整行为 v1.1，M0 不冻结） | 否 |
| OQ-25 | 旧看板「表格视图」是否保留为 v1.1 可选视图（默认工作台）？ | 保留为 v1.1 备选；v1 入口不存在、不验收（D-023） | 否（v1.1 再定） |
| OQ-26 | 参考图右侧窄栏为示意宽度，实现取 18%（220–320px）是否可接受？ | 接受；以 §11.0.3 区间 + AC-UI-01 断言为准，不追求像素复刻 | 否（D-022 已裁决） |
| OQ-27 | 文件夹视图是否保留右侧信息面板（18%）？ | 保留（A/B/F/G + 工作区树 + 右侧信息面板）；不合并为纯树 | 否（§11.5 已定） |
| OQ-28 | 文件树是否显示 `.git`/`.trash`/未纳管目录？ | 默认隐藏 `.git`/`.trash`；未纳管目录仅在文件夹视图“未纳管”分组显示 | 否（FR-TREE-01/FR-FOLDER-01） |

---

## 附录 A：草案缺陷 → 处置对照（摘要，详见 `reqs/review-report.md`）

| 草案问题 | 处置 |
|---|---|
| 组件标识矛盾（CF123A_ShortName vs CF325F） | 已决策 D-001：去后缀全名；drawio 示例作废 |
| 状态 5 vs 6、CONFLICT=分叉、NEW_BRANCH 判据不成立 | 第 7 章 14 主状态 + 标记 + 聚合全序；CONFLICT→DIVERGED，NEW_BRANCH→NO_UPSTREAM 三子型 |
| 两套颜色语义相反 | 第 11.2 双通道色板；整卡变色废弃 |
| “五表”只定义 3 表、无主外键/dirty/tag→commit | 第 8 章 16 表设计基线（评审 03 §C.3 + 补充） |
| 3 进程 vs 4 进程、聚合依赖边错误、无监管 | 第 12 章 2 进程 + Job Object 监督 + 单写者（D-011） |
| 祖先判定未 fetch 不可行、“纯内存比对” | 7.1 + FR-STA-03：fetch 权威 + merge-base；compare 兜底 |
| SYNC/切 Tag/删除/Clone 无安全语义 | 9.7 FR-OP 明确备份/软删除/保护/CAS/journal |
| “网页级炫酷/秒级感知”不可测 | 10.5 + 7.5 分档 SLO；本地 ≤5s / 外部 ≤10s / 远程 ≤10min |
| HLR 有基线/CI、LLR 无设计 | CI 预留（9.11）+ 基线 v2（9.12） |
| UI 直接沿用旧 SRS §11 看板布局（表格+侧栏+左侧导航） | **v0.3 以用户参考图为准重构**：工作台 7 区域 + 文件夹视图（§11.0/§11.5）；旧布局废弃/降级（§11.0.1/§11.7，D-022/D-023） |
| 文件树只在详情内、主界面无树 | **四仓 2×2 文件树提升为主界面 D 区**（§9.14）；单仓详情文件 Tab 复用同一树组件 |
| drawio 不可作为执行依据 | 图集拆分建议见 review-report；本文件第 11/12 章取代其执行语义 |

## 附录 B：资产索引

| 文件 | 用途 |
|---|---|
| `reqs/requirements.md` | **唯一维护源（本文件）** |
| `reqs/review-report.md` | 需求审核结论（评审者视角） |
| `reqs/review/01-requirements-critic.md` | 需求质量评审（27 发现） |
| `reqs/review/02-git-domain.md` | Git 语义评审（24 发现 + 修正状态机/伪代码） |
| `reqs/review/03-architecture.md` | 架构可行性评审（28 发现；推荐 Host+GitWorker / SQLite WAL / 分层采集） |
| `reqs/review/04-ux-ci.md` | UI/CI 预留评审（28 发现，10 锚点） |
| `reqs/review/05-drawio-execution.md` | drawio 执行方案评审（30 发现，图集拆分） |
| `reqs/review/06-redteam-srs.md` | SRS v0.1 红队复审（假关闭 P0 / AC 缺口 / Top 10） |
| `reqs/v0.2/ac-catalog.md` | **权威 AC 目录**（162 条 AC / 72 FR / 追溯矩阵 / 红队处置） |
| `reqs/v0.2/diagrams.md` | D1–D6 执行图集（Mermaid，含 CI 锚点与验收标准） |
| `reqs/v0.2/fixtures/` | 状态矩阵夹具生成脚本 + 期望清单 + 使用说明 |
| `reqs/v0.2/freeze-checklist.md` | v0.2.1 冻结检查清单与条件跟踪（C1–C6） |
| `reqs/review/07-freeze-review.md` | 第一轮冻结复评（✗ 不可进入 M0，C-01…C-06/H-01…H-06） |
| `reqs/review/08-freeze-review-v2.md` | 第二轮冻结复评（☑ 有条件进入 M0，C1–C6） |
| `reqs/archive/draft-docx-transcript.md` | 原 docx 忠实转写（冻结） |
| `reqs/archive/draft-drawio-transcript.md` | 原 drawio 忠实转写（冻结） |
| `reqs/MBD组件全生命周期Git工具.docx` / `reqs/software_workflow.drawio` | 原始证据（不再修改） |
| `docs/images/reference/main-ui-reference.png` | **v0.3 主界面唯一视觉基准（参考图）**：菜单栏/标题区/组件卡片/2×2 四仓文件树/右侧信息面板/底部 5 按钮/状态栏（D-022） |
| `docs/ui-redesign-plan.md` | UI 重构阶段计划与视觉基线（S1–S7；比例 20/62/18、1440×900 基线、1280×800 最小） |
| `docs/ui-review-m0.md` | M0 CDP 界面自检记录（旧布局证据；v0.3 工作台自检需另出记录） |

## 附录 C：状态矩阵夹具清单（M0 交付物，表驱动测试基线）

> 每行 = 一个夹具（`fixture_id`）；构造步骤用临时 git 仓库完成；`primary`/`aggregate` 为期望值。M0 冻结为可执行脚本（本地夹具生成器 + 断言），并补齐 flags 组合（dirty/untracked/stashed/shallow/lfs_missing/reparse/busy/remote_gone/tag_moved/mixed_branch）。

| fixture_id | 构造 | 期望 primary | 期望组件 aggregate | 关键 flag |
|---|---|---|---|---|
| F-SYNCED | 分支 + upstream 存在且 HEAD==upstream，工作区干净 | SYNCED | SYNCED | — |
| F-BEHIND | upstream 领先本地 2 个 commit | BEHIND | BEHIND | — |
| F-AHEAD | 本地领先 upstream 2 个 commit | AHEAD | AHEAD | — |
| F-DIVERGED | 双方各有 1 个新 commit | DIVERGED | DIVERGED | — |
| F-DIRTY | HEAD==upstream，tracked 文件改动 | DIRTY | DIRTY | dirty_tracked |
| F-NO_UPSTREAM-LOCAL | 本地新分支，远程无同名 | NO_UPSTREAM(LOCAL_ONLY) | NEW_BRANCH | — |
| F-NO_UPSTREAM-UNSET | 远程有同名分支但未设 tracking | NO_UPSTREAM(UNSET_BUT_EXISTS) | NEW_BRANCH | — |
| F-NO_UPSTREAM-GONE | upstream 分支已被删除（fetch --prune） | NO_UPSTREAM(UPSTREAM_GONE) | NEW_BRANCH | —（分支已删 ≠ 仓库已删） |
| F-DETACHED-TAG | `switch --detach v1.0` 且 tag 指向 HEAD | DETACHED_TAG | DETACHED | — |
| F-DETACHED-COMMIT | `switch --detach <sha>`（无 tag） | DETACHED_COMMIT | DETACHED | — |
| F-UNMERGED | 制造 index unmerged 条目 | UNMERGED | BLOCKED_LOCAL | — |
| F-IN_PROGRESS | 存在 `.git/MERGE_HEAD` | IN_PROGRESS | BLOCKED_LOCAL | — |
| F-EMPTY | `git init` 无 commit（unborn） | EMPTY | EMPTY | — |
| F-INVALID-NOTREPO | 目录存在但非 git 仓 | INVALID | BINDING_ERROR | — |
| F-INVALID-REMOTE | 是 git 仓但 origin 不等于期望仓 | INVALID | BINDING_ERROR | — |
| F-MISSING | 本地目录不存在 | MISSING | MISSING | — |
| F-MISSING+BLOCKED | Design 仓本地目录不存在（MISSING），Impl 仓存在 unmerged 冲突（UNMERGED），MIL/SIL 同步 | MISSING（Design）+ UNMERGED（Impl） | **MISSING**（MISSING=3 先于 BLOCKED_LOCAL=4） | — |
| F-UNKNOWN | fetch 失败 + TTL 过期 | UNKNOWN | UNKNOWN | stale |
| F-DETACHED+MIXED | 组件 2 仓 tag 检出、2 仓不同分支 | DETACHED_TAG×2 + SYNCED×2 | DETACHED | mixed_branch |
| F-TAG_DRIFT | 4 仓均存在；3 仓 detached 于 tag v1.0，第 4 仓在默认分支且状态固定为 SYNCED | DETACHED_TAG×3 + SYNCED×1 | TAG_DRIFT | —（`tag_present` 是 baseline_snapshot 字段，非 flag） |
| F-PARTIAL | 批量操作 2/4 失败 | — | OP_PARTIAL_FAILED | — |
| F-LFS-MISSING | LFS 仓库但对象缺失（pointer 文件） | SYNCED（本地事实）+ lfs_missing flag | **SYNCED**（flag 不改变聚合；UI 红色告警 + 阻断写） | lfs_missing |
| F-BUSY | 外部进程持有 `.git/index.lock` | 按实际状态 + flag | 按实际聚合 | busy |
| F-REMOTE-GONE | Gitea 已删仓、本地仍在 | UNKNOWN（远程删除→关系不可判） | UNKNOWN | remote_gone |
| F-TAG-MOVED | 本地/远程同名 tag 指向不同 commit | 按关系状态 + flag | TAG_DRIFT | tag_moved |

**聚合态覆盖（15 个组件聚合态 → fixture）**：OP_PARTIAL_FAILED←F-PARTIAL；BINDING_ERROR←F-INVALID-*；UNKNOWN←F-UNKNOWN；BLOCKED_LOCAL←F-UNMERGED/F-IN_PROGRESS；MISSING←F-MISSING；EMPTY←F-EMPTY；TAG_DRIFT←F-TAG_DRIFT/F-TAG-MOVED；DIVERGED←F-DIVERGED；BEHIND←F-BEHIND；AHEAD←F-AHEAD；DETACHED←F-DETACHED-*；MIXED_BRANCH←F-DETACHED+MIXED；NEW_BRANCH←F-NO_UPSTREAM-*；DIRTY←F-DIRTY；SYNCED←F-SYNCED；**兜底 UNKNOWN**←任意未命中组合（property-based 随机提交图断言）。

**离线子集**：F-NOTREPO/REMOTE/MISSING/EMPTY/UNMERGED/IN_PROGRESS/DETACHED-* 在断网下仍须得到对应状态（不得一律 UNKNOWN），用于 AC-STA-14。

> 夹具映射：`v0.2/fixtures/expected.json` 与本节逐条对应；`NO_UPSTREAM` 的 `expected_sub_type` 使用完整枚举名（`LOCAL_ONLY` / `UNSET_BUT_EXISTS` / `UPSTREAM_GONE`）。`tag_present`、`tag_moved` 为 baseline/flag 语义，`expected_flags` 仅收录 7.3 定义的 flag。

## 附录 D：设置项与错误码（M0 冻结）

**设置项（键 / 默认 / 范围 / 生效）**

| 键 | 默认 | 范围/说明 | 生效方式 |
|---|---|---|---|
| `workspace.root` | 必填 | v1 恰好 1 个；禁止盘符根/用户目录根 | 变更需确认+重扫 |
| `server.baseUrl` / `server.org` | 必填 | HTTPS | 变更需确认+重同步 |
| `auth.mode` | `pat` | v1 仅 PAT | 重登 |
| `proxy.mode` / `proxy.url` / `tls.extraCa` | 系统代理 / 空 | 手动/系统/关闭 | 重启或即时 |
| `git.path` | 自动检测 | Git ≥2.40 | 重启 |
| `scan.fingerprintIntervalSec` | 15 | 5–60 | 即时 |
| `scan.hotIntervalSec` / `scan.coldIntervalMin` | 5 / 360 | 1–30 / 30–1440 | 即时 |
| `remote.listWatermarkMin` / `remote.refreshMin` / `remote.fullSweepHours` | 10 / 30 / 24 | 1–60 / 5–120 / 6–168 | 即时 |
| `fetch.beforeWrite` | true | 破坏性操作前强制 fetch | 即时 |
| `ttl.localSec` / `ttl.fetchSec` / `ttl.remoteSec` | 90 / 1200 / 25200 | 按 7.1 | 即时 |
| `delete.retentionDays` | 30 | 7–90 | 即时 |
| `backup.retentionDays` | 30 | 7–90（备份 ref `refs/backup/sync/*` 保留天数，超期由清理任务回收，H-05） | 即时 |
| `log.keepDays` / `audit.keepDays` | 30 / 90 | 7–90 / 30–365 | 即时 |
| `ui.theme` / `ui.density` / `ui.reduceMotion` | system / standard / follow-system | light/dark/system；compact/standard；follow/reduce | 即时 |
| `ui.view` | `workbench` | `workbench`/`folder`：记忆上次视图；**首屏永远是工作台** | 即时 |
| `ui.panel.leftWidth` / `ui.panel.rightWidth` | 288 / 260 | 240–360 / 220–320；仅拖拽左右分隔条改变；双击分隔条恢复默认；不得改变 D 区 2×2 | 即时 |
| `tree.hideDotGit` / `tree.defaultExpandDepth` / `tree.pageSize` | true / 1 / 500 | 隐藏 `.git`/`.trash`；默认展开层数 1–2；单次目录返回子项 100–500（超出分页/虚拟化） | 即时 |
| `ci.enabled` / `ci.baseUrl` / `ci.token` / `ci.pollIntervalSec` | **false** / 空 / 空 / 60 | 禁用态占位（v1）；`ci.token` 只存凭据引用（与 12.6 同模型） | 重启 |
| `dirty.includeUntracked` | false | DIRTY 是否计入 untracked（OQ-22） | 即时 |
| `remote.prune` | true | fetch 时对 upstream 分支 `--prune`（用于 UPSTREAM_GONE 检测，R-26） | 即时 |
| `safety.requireTypedConfirm` | true | 破坏性操作输入组件 ID；精确匹配、区分大小写、忽略首尾空格 | 即时 |

**错误码（示例，M0 补全表；M0 需补“可重试性/日志级别/用户文案”三列）**

| 错误码 | 含义 | 用户动作 |
|---|---|---|
| `CFG-E001` | URL/路径非法 | 修正配置 |
| `CFG-E002` | Token 权限不足 | 重新生成最小 scope token |
| `SYNC-E001` | 401/403（认证/权限） | 重登/申请权限 |
| `SYNC-E002` | 429/网关限流 | 自动退避，展示重试倒计时 |
| `SYNC-E003` | 覆盖不足 100% | 工作台标 STALE，可见失败清单 |
| `SCAN-E001` | safe.directory 拒绝 | 提示而非放宽 |
| `SCAN-E002` | 网络盘/UNC 写禁止 | 只读展示 |
| `STATE-E001` | 数据超 TTL | 显示 STALE + 手动刷新 |
| `OP-E001` | BUSY（外部 git/index.lock） | 等待后重试，禁止删锁 |
| `OP-E002` | CONCURRENT_CHANGE（CAS 失败） | 重新预检 |
| `OP-E003` | 部分失败 | 逐仓明细 + 仅重试失败项 |
| `OP-E004` | 被状态矩阵禁止的操作 | 按 7.6 展示替代动作 |
| `FS-E001` | 路径过长/保留名/大小写冲突 | 修正路径 |
| `FS-E002` | 文件树路径越界/非法（`..`/绝对路径/UNC/reparse 越界/不属于已绑定仓） | 拒绝访问并刷新树；不泄露越界内容 |
| `FS-E003` | 打开文件/文件夹失败（无关联程序/被占用/已删除/长度超限） | 换用默认程序或资源管理器；可读文案 + 错误码 |
| `NET-E001` | 网络超时/断网 | 降级只读 |
| `GIT-E001` | 空仓库/unborn | 首推/初始化 |
| `SEC-E001` | 检测到既有凭据明文 | 告警并引导清理 |
