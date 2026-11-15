> 归档说明：本文为 v0.4.1 历史规格（v0.4 冻结 + D-029 Tag 暂定），不再作为 v1.0 需求正文；文中 docs/... 链接为当时路径，相关文档现归档于 docs/archive/。当前需求见 eqs/SRS-v1.0.md。

# MBD 组件全生命周期 Git 工具 — 规格说明书（Specification v0.4.1（Tag 暂定））

> **单一需求源。** 本文件合并原 SRS 与 AC Catalog；所有实现、测试、评审、验收引用本文件。`reqs/adr/*` 是阶段性决策记录，不是需求源。
> 评审规则：每次需求评审**只评审本文件**；评审通过并冻结后才进入开发。M2b 增量已作为本文件 §16（M2b Delta）待评审；通过后冻结，不另建需求文件。

---
> **原始设计来源（已并入本文件，不再单独维护）：** 旧 SRS v0.3.1-r2 正文与 AC Catalog 已合并到本文件；历史 docx/drawio、评审、v0.2 资产按要求删除。
> **v0.3（2026-11-14）UI 基线变更**：用户提供 `docs/images/reference/main-ui-reference.png` 作为主界面唯一视觉基准，**旧 v0.2.1 §11（看板表格 + 右侧栏 + App Shell 左侧图标导航）与参考图冲突处一律以图为准**。§11 重构为「工作台 + 文件夹视图」；新增 FR-WS/FR-TREE/FR-FOLDER/FR-ACT/FR-CARD/FR-INFO 六组共 32 条 FR 与 AC-UI-01…AC-UI-14；10 个 CI 锚点全部给出新落点（10/10）。旧看板表格视图降级为 v1.1 备选（v1 入口不存在、不验收），旧 §11.1 导航与旧 §11.3 行布局**废弃**。

| 项目 | 内容 |
|---|---|
| 文档 ID | MBD-GIT-SRS |
| 版本 | **v0.4.1（2026-11-15，Tag 暂定修订；v0.4 冻结基线 + D-029）** |
| 日期 | 2026-11-14 |
| 状态 | **v0.4 已冻结**；v0.4.1 按用户决策将批量切 Tag 暂定（D-029），不参与 M2b 验收。M2 拆分（D-025）继续有效：M2a 已独立签收，M2b 未完成；M2 总出口 = M2a ∧ M2b。评审通过并冻结后进入 M2b 增量开发 |
| 替代 | v0.3.1-r2；历史原稿已删除 |
| 权威级别 | **本文件 > 任何口头/聊天记录 > ADR 阶段说明**；**UI 视觉冲突时：用户参考图 > 本文件 §11** |

**需求状态图例**：`[已定]` 已决策可直接实现 · `[决策]` 本版决定的方案 · `[待确认]` 需用户拍板（附推荐默认） · `[设计建议]` 需求层不强制、由架构评审定稿 · `[v1]` 第一版范围 · `[v1.1]` 第二版 · `[v2]` 后续 · `[废弃]` 明确不做。
**与 P0/P1/P2 的关系**：P0/P1/P2 是交付优先级；`[v1]/[v1.1]/[v2]` 是版本归属；二者不是同一维度，FR 行内以 P 级为主，版本归属见各章说明。

---

## 0. 修订记录

| 版本 | 日期 | 变更 | 依据 |
|---|---|---|---|
| v0.1 | 2026-11-13 | 由草案 docx/drawio 重建：统一术语与状态模型、补齐数据/安全/验收/MVP 范围、CI 预留 10 锚点、修正 drawio 执行方案 | 5 路对抗评审 + 用户决策 D-001 |
| v0.1.1 | 2026-11-13 | 按红队复审修订：补 TTL/离线语义、7.4 聚合映射与兜底（15 聚合态）、7.6 状态×操作矩阵、11.2 全量色板（含深色）、8.1 表清单（16 表）、首启三阶段统一、AC 扩至 27 条+追溯矩阵、附录 C 状态矩阵/附录 D 设置与错误码、12.1 网络边界/恢复链路；关闭 OQ-02/03/04/08 等默认决策 | 红队复审 06 + 架构评审 03 |
| v0.2 | 2026-11-14 | 冻结候选：接入 `Annex A`（188 AC/104 FR）、`本文件 §7.2/§7.4/§8.1`、`reqs/fixtures/`（m2-matrix）、`本文件 §5.4`；按红队 R-01…R-37 修订 SRS（严重度全序、写操作列权威化、TAG_DRIFT/LFS/NO_UPSTREAM 夹具裁决、CI 契约 componentId/数字 repoId、凭据口径、git 执行边界、枚举冻结、附录 D 补键） | AC 目录修订清单 + 图集/夹具核验 |
| v0.2.1 | 2026-11-14 | 按独立冻结复评 07 修订 SRS 侧阻断/高危项（AC/图集/夹具由并行角色回写）：C-02 `BINDING_ERROR` 按 `binding_cause` 拆分严重度（INVALID/AMBIGUOUS=1，仅缺仓=12）；C-04 §7.6 SYNC 强覆盖矩阵（DIRTY/DIVERGED/SYNCED）；C-05 §8.1 补 `job.skipped`、`job_item.started_at/finished_at`、`job_item.state=INTERRUPTED`、`sync_run.duration_ms`、`component.last_activity_at`，`job.state` 统一 `PARTIAL`，新增 §8.3 契约↔DDL 映射；H-01 远程未就绪中间态统一为 `UNKNOWN`+`incomplete=1`；H-02 冻结首扫进度阶段枚举与字段；H-03 新鲜度按表定义并补齐字段；H-04 hatched 对比度修正 + 对比度脚本入 M0 门禁；H-05 备份 ref 保留/清理/恢复；H-06 P2 功能 v1 仅保留入口不存在；C-06 里程碑出口与 §13.4 追溯矩阵改用 AC 目录新编号；M-09 凭据头统一 `Authorization: token <PAT>` | `历史评审（已删除）07-freeze-review.md` §一致性缺陷 / §进入 M0 前必须修 |
| v0.3 | 2026-11-14 | **UI 以用户参考图为准重构**：重写 §11 主界面为工作台（菜单栏 / 标题区 / 左侧组件卡片列表 / 中间 2×2 四仓文件树 / 右侧选中仓信息面板 / 底部 PULL·FETCH·CHKOUT·COMMIT·PUSH 按钮条 / 状态栏），新增 §11.5 文件夹视图、§11.6 批量入口、§11.0.1 冲突裁决；旧看板表格 + 侧栏 + App Shell 左侧导航**废弃**，表格视图降级 v1.1（v1 入口不存在）；新增 §9.13–9.17 六组 32 条 FR 与 §13.1.1 的 AC-UI-01…AC-UI-14；10 个 CI 锚点全部给出新落点（10/10，CI 仍不参与聚合/卡片底色）；更新 §3 范围与 M0/M1/M2/M3 里程碑、§13 追溯矩阵、§10 性能/离线措辞、附录 D 设置键 | 用户参考图 `docs/images/reference/main-ui-reference.png` + `docs/ui-redesign-plan.md` + ADR D-022…D-024 |
| v0.3.1 | 2026-11-14 | **M2 拆分为 M2a 单仓安全交付 / M2b 批量·作业·审计（D-025）**：§3.3 M2 行拆为 M2a/M2b（出口标准与依赖）；§9.16/§9.17 标注 M2a 范围（取消/job/任务中心/仅重试失败项归 M2b）；§7.6 冻结按钮语义（DIVERGED Pull=❌、`—` 按禁用断言、EMPTY 首推归 M2b）；§13.1.1 修订 AC-UI-08/09/10 并新增 AC-UI-10a（COMMIT 确认条）/AC-UI-10b（PUSH 确认条）/AC-UI-10c（取消·job，M2b）/AC-UI-15（M2b 边界提示）；新增 §13.1.2 AC-SAFE-14（凭据作用域）/15（CAS）/16（ignored）/17（ref 校验）与 AC-OP-33（审计-lite）；§13.4 追溯矩阵与门禁更新。**当前状态：M2a 待复评，不写“已通过”** | `reqs/adr/D-025-m2a-m2b.md` + `docs/review-m2-req.md`（RQ-01…RQ-08/P0 清单）+ `docs/review-m2-code.md`（P0-1/P1-1/2/3）+ `docs/review-m2-ux-fixcheck.md`（NP0-1） |
| v0.3.1-r2 | 2026-11-14 | **M2a 出口 AC 定义与证据物补齐（Gate G-P0-4 回写；不放宽任何安全要求）**：新增 **§13.1.2**——AC-UI-10a/10b/10c/15、AC-SAFE-14…17、AC-OP-33 的完整 Given/When/Then 与可测断言、M2a 出口 13 条口径、单源矩阵夹具 `reqs/fixtures/m2-matrix.json`（**Host 单测 + CDP 共用，禁止第三份手写期望**）、tooltip 冻结格式改 `原因 [E_*] → 替代动作`、COMMIT「对话框→确认条→执行」两步口径；§13.1.1 修正 AC-UI-08/09/10 拆分引用；§13.4 补 FR-ACT/FR-SAFE/FR-OPS 追溯行与 M2a 门禁注；新增 `docs/m2a-ac-report.md`（证据模板）与 `ui/scripts/cdp-m2a-selfcheck.mjs`（旧 `cdp-m2-selfcheck.mjs` 作废）。**状态仍为 M2a 待复评（修复中），不写“已通过”** | `docs/review-m2-gate-ux.md` §4.2/§4.4/G-P0-4 + `docs/review-m2-gate-security.md`（N-1/N-2）+ `reqs/adr/D-025-m2a-m2b.md` §2.2/§3/§5 |
| v0.4.1 | 2026-11-15 | **切 Tag 暂定（D-029）**：AC-OP-11…14 与 Tag baseline 不参与 M2b 验收/实现计划；已有后端保留、UI 禁用；M2b 出口改为 AC-OP-01…10、15…35 | 用户决策：各版本 Tag 仓集合不同 |\n| v0.4（frozen） | 2026-11-15 | **需求单源化**：SRS + AC Catalog + 追溯/错误码附录合并为 specification.md；删除历史 review/archive/_tools/v0.2/docx/drawio；新增 §16 M2b 增量（组件管理/DELETE_PENDING/Repair/Re-clone/baseline/CREATE 撤销/故障注入/测试门禁/DoD）；按三路评审 P0 修订 | 用户要求：单文件评审；docs/review-spec-*.md |

**已确认决策（ADR）**

| ID | 决策 | 结论 | 来源 |
|---|---|---|---|
| D-001 | 组件唯一标识 | 仓库全名去掉末尾 `_Design/_Impl/_MIL/_SIL`（大小写不敏感）后的全名即组件 ID，如 `CF325F_ChanganSigProcgBascFct`；drawio 中“聚合成 CF325F”的写法**废弃** | 用户确认（A） |
| D-002 | CI 本期范围 | v1 只做 **UI 预留 + 数据契约 + Mock**，不接真实 CI API；CI 永不参与仓库主状态与卡片底色 | 用户要求 + 评审 |
| D-003 | 状态与颜色 | 废弃 docx 五状态/五色标与 drawio 六状态/另一套色标，改用本文件第 7 章状态机 + 第 11.2 节双通道色板 | 评审 RQ-02/RQ-03、UX-01/02 |
| D-004 | 文档权威 | 历史原稿（已删除） 降级为证据归档；本文件为唯一 SRS | 评审 RQ-09 |
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
| 批量操作 | Clone（幂等/预检/部分失败）、Pull、Fetch、Checkout 分支、切 Tag（detached）、受控 SYNC、本地删除、CREATE 分支（dry-run）；入口=菜单栏操作菜单 + 组件卡片右键 + 卡片区顶部批量条（§11.6）+ 组件纳入/移出（AC-UI-16，“已移出”视图） |
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
| M0 骨架与冻结 | **新工作台骨架（参考图 7 区域：菜单栏/标题区/组件卡片列表/2×2 四仓文件树/右侧信息面板/底部按钮条/状态栏）+ 文件夹视图骨架 + 视图切换 + 设计 token（§11.2）+ 10 个 CI 占位锚点（新落点）+ Mock 数据 + 假 Gitea**；底部 5 按钮**禁用占位**（D-024）；状态矩阵夹具（附录 C）、AC 全量冻结、测试资产（图集语义已并入 §7/§8/§11）（Gitea 单二进制 fixture/`IClock`/基准脚本） | 工作台骨架可跑；**CDP 布局断言：7 区域存在且顺序正确、1440/1280 无页面级横向溢出、CI 锚点 10/10 命中**；AC 追溯矩阵 100% 覆盖 FR（含 AC-UI-01…14）；夹具清单签收 |
| M1 只读闭环 | 配置/认证、远程同步、本地发现、状态计算、**工作台接真数据（组件卡片/四仓文件树/文件夹视图/右侧信息面板/状态栏）+ 详情抽屉 + 三阶段首扫与就绪重判**；底部 5 按钮**全部只读禁用**并提供禁用理由（D-024）；文件树/文件夹视图只读可展开、双击打开 | 附录 C 状态矩阵全绿；工作台首屏 P95 ≤1.5s（NFR-PERF-02/09）；**AC-UI-01/03/04/05/06/07/09/11/12/13/14 通过**（布局、文件树、文件夹视图、视图切换、M1 只读边界、信息面板、CI 锚点、二级入口、旧布局废弃） |
| M2a 单仓安全交付 | **底部单仓按钮条 PULL/FETCH/CHKOUT/COMMIT(staged-only)/PUSH 真实 Host 接入**（PULL `--ff-only`、COMMIT staged-only/message 1–200、PUSH 禁 force 且 `NO_UPSTREAM` 走“发布分支并建 upstream”确认；EMPTY 首推归 M2b，M2a 构建按空仓无提交禁用；CHKOUT 选择器来自 `remote_ref` + 搜索 + `stash -u` 选项）+ **安全四件**（凭据作用域、写前 read-through+CAS、ignored 文件保护、ref 校验）+ **COMMIT/PUSH ≤48px 确认条**（含 `Alt+5` 同路径）+ **焦点闭环** + **tooltip 三段式与 Host 单源 14 态矩阵** + **操作审计一条（audit-lite）** + **真实数据验收**；批量/任务中心入口明示“M2b 未接入” | **M2a 出口 AC 全绿（共 13 条）**：`AC-UI-08`（单源矩阵+tooltip）、`AC-UI-09`（阶段门禁）、`AC-UI-10`（前置/失败/进行中/回写）、`AC-UI-10a`（COMMIT 确认条）、`AC-UI-10b`（PUSH 确认条）、`AC-UI-11`、`AC-UI-13`（焦点复验）、`AC-UI-15`（M2b 边界）、`AC-SAFE-14/15/16/17`、`AC-OP-33`；评审 P0 闭环（`docs/review-m2-req.md` P0-01…P0-05/P0-08/P0-11、`docs/review-m2-code.md` P0-1/P1-1/P1-2/P1-3、`docs/review-m2-ux-fixcheck.md` NP0-1）；CDP+Host+fixture 可自动复跑；**不得声称 FR-ACT-05/06 全量通过**（取消/job/任务中心归 M2b） | 依赖 M1 出口通过；依赖 `reqs/adr/D-025-m2a-m2b.md` 冻结的口径与夹具 `reqs/fixtures/m2-matrix.json` |
| M2b 批量/作业/审计 | 批量 Clone/Pull/Checkout/Tag/SYNC/Delete/Restore/Create + 受控 SYNC 备份 ref + `.trash` 恢复 + 任务中心（`job`/`JOB_UPDATED`/`jobId`/取消/仅重试失败项/持久化）+ 审计完整表（查询/导出/≥90 天）+ 写前 journal + 崩溃 reconcile + FR-REPO-05 其余修复类（Repair/Re-clone、detached 建分支、EMPTY 首推建 upstream）+ 离线降级 | 写操作 AC 通过（`AC-OP-01…10、AC-OP-15…35`、`AC-REPO-01…AC-REPO-11`、`AC-SAFE-01…AC-SAFE-13`、`AC-UI-10c`、`AC-UI-16`；`AC-OP-11…14` 按 D-029 暂定不参与）；故障注入报告通过；14 态矩阵在 M2b 构建复验；**M2 总出口 = M2a ∧ M2b，缺一不得宣称 M2 交付** | 依赖 M2a 出口通过（矩阵单源/审计链路/错误码体系是 M2b 前置） |
| M3 收口 | 通知/日志/诊断/设置、性能与可用性、打包、验收 | **AC 基线冻结版本全绿**；P0 缺陷清零；§7/§8/§11 与需求/代码追溯一致 |

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
- **矩阵口径冻结（D-025，v0.3.1；AC-UI-08 单源夹具 `reqs/fixtures/m2-matrix.json` 按此生成）**：
  1. `DIVERGED` 的 Pull(ff) 为 **❌ 禁用**（原“⚠ 仅展示不自动 merge”自相矛盾，按更严格语义冻结；tooltip 给出“手动 merge/rebase 后重扫”替代动作与错误码）；
  2. 矩阵中 `—`（无需执行）按 **禁用** 编码断言（`MISSING` 的 Fetch/Pull、`EMPTY` 的 Pull 等），不再按“可点但无效果”处理；
  3. `COMMIT`/`PUSH` 的确认标志固定为 **需确认（C）**（FR-ACT-04 全局要求，优先于本矩阵的 ⚠/✅ 单元格语义）；确认条断言见 AC-UI-10a/10b；`Alt+5` 与按钮同路径；
  4. `NO_UPSTREAM` 的 Push 允许但必须走“发布分支并建立 upstream”确认（`setUpstream=true`）；`EMPTY` 的 Push 在 **M2a 构建**按“空仓无提交”禁用并 tooltip 指向 M2b 首推（FR-REPO-05），M2b 构建恢复本矩阵；
  5. 同一状态同一动作在 Host、UI、测试夹具三处必须一致；出现第二套判断（如 UI 自写矩阵）即 AC-UI-08 不通过。

---

## 8. 数据模型（设计基线；字段名可调，语义不可丢）

> 基线 schema 采用架构评审 03 §C.3（SQLite DDL 草案），本章为语义约束。**DB 是缓存不是事实源**：磁盘与 Gitea 才是事实。**新鲜度列按表定义（H-03）：事实类表必须带等价新鲜度列**——`repo_status` 带 `data_source`/`is_stale`/`calc_at`；`remote_ref` 带 `observed_at`/`synced_at`；`local_state` 带 `scan_at`/`last_success_at`/`scan_error`；`ci_status` 带 `fetched_at`；`sync_run` 带 `started_at`/`finished_at`/`duration_ms`；其余表（`repo`/`component`/`job`/`job_item`/`operation_log`/`app_setting`/`unbound_repo`/`scan_issue`/`notification`/`event_log`/`baseline_snapshot`）按行内定义带更新时间或采集时间。库可整库重建。

### 8.1 表清单（废弃“极简五表”表述）

> **冻结说明**：字段名自 v0.2 起冻结（变更需 ADR）；**v1 建表 16 张**（含 3 张空表预留/治理表）。`repo` 的远程字段（full_name/remote_url/default_branch/archived）由 REMOTE_SYNC 写，本地字段（is_local/local_path）由 LOCAL_SCAN 单写（管理操作只触发重扫，不直接写 is_local）；如实现要求严格单写者，可拆 `repo`/`repo_local` 两表（语义不变）。字段清单自 v0.2.1 起与历史图集（已并入正文） 对齐（`repo.renamed_from`、`component.is_managed`、`local_state.scan_error`、`baseline_snapshot.tag_object_sha`、`job.started_at`、`ci_status` 全列已在本表登记），D4 ER 缺失列由已并入本表。

| 表 | 主键 | 关键字段 | 唯一写者 |
|---|---|---|---|
| `component` | component_id | display_name, local_root, aggregate_status, status_calc_at, incomplete, is_managed, is_deleted, last_activity_at（最近活动时间，供工作台“最近活动”排序）, timestamps | STATUS_CALC（业务列）/ COMPONENT_MGMT（is_managed/is_deleted） |
| `repo` | repo_id（Gitea 数字 id） | component_id FK, repo_type(CHECK DESIGN/IMPL/MIL/SIL/OTHER), full_name UK, remote_url, default_branch, archived, renamed_from, is_local, local_path, timestamps | REMOTE_SYNC（远程列）/ LOCAL_SCAN（is_local/local_path 单写） |
| `remote_ref` | (repo_id, ref_type, ref_name) | ref_type(CHECK BRANCH/TAG), commit_hash, observed_at, synced_at, generation | REMOTE_SYNC |
| `local_state` | repo_id | head_type(CHECK BRANCH/DETACHED_TAG/DETACHED_COMMIT/UNBORN), head_ref, head_sha, upstream_ref, upstream_sha, ahead, behind, dirty_count, untracked_count, op_in_progress, fingerprint, scan_at, scan_error, last_success_at | LOCAL_SCAN |
| `repo_status` | repo_id | main_status, flags_json, error_code, data_source, is_stale, calc_at | STATUS_CALC |
| `ci_status`（v1 空表） | (repo_id, commit_sha) | provider, state, run_id, run_url, branch, build_number, failed_stage, started_at, finished_at, duration_ms, fetched_at, updated_at；`ci_latest` 视图 = 每 repo 最新 commit 对应行（v1 空） | 预留（v2 REMOTE_SYNC） |
| `job` | job_id | kind(CHECK CLONE/PULL/FETCH/CHECKOUT/TAG/SYNC/DELETE/CREATE/RESTORE/REPAIR/RECLONE/CREATE_UNDO/PUSH/COMMIT), target_json, state(CHECK QUEUED/RUNNING/SUCCEEDED/FAILED/CANCELLED/INTERRUPTED/PARTIAL；**部分成功统一用 `PARTIAL`**), total/succeeded/failed/**skipped（整数计数）**, cancel_requested, started_at, finished_at, timestamps | JOB_RUNNER |
| `job_item` | (job_id, repo_id) | state(CHECK QUEUED/RUNNING/OK/FAILED/BUSY/SKIPPED/CANCELLED/**INTERRUPTED**), attempt, pre_head, expect_head, post_head, backup_ref, baseline_ref, reconcile_status(CHECK NOT_APPLIED/UNKNOWN_APPLIED，可空), message, error_code, started_at, finished_at, updated_at | JOB_RUNNER |
| `operation_log`（审计） | id | ts, actor, action(CHECK = job.kind ∪ {COMPONENT_BIND, COMPONENT_UNBIND, CREATE_UNDO, RECONCILE}，D-028), target_json, before_json, after_json, result, error_code, request_id | 各写者追加 |
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
| FR-OP-04 | P0 | **暂定（D-029）：不参与 M2b 验收**。切 Tag：四仓同一 Tag；Tag 不存在于远程则跳过并报告；检出为 detached；记录 `baseline_snapshot`（`^{commit}` 解引用）；Tag 被移动时 `fetch --force` 更新并告警；禁止 `--prune-tags` | 恢复时需按版本/仓集合重新定义，缺 Tag/移动 Tag/annotated Tag 用例 |
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
| FR-WS-04 | P0 | **二级入口全部经菜单栏或标题区进入，不设旧 App Shell 左侧图标导航**：任务中心 / 通知中心 / 日志与诊断 / 设置 / CI 流水线 / 未绑定仓库；抽屉打开不改变主区 bbox，ESC 关闭且焦点回到触发控件；入口可达率 7/7 | 7 个入口点击可达；抽屉前后 C/D/E bbox 不变；AC-UI-13 |
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
- 测试资产（M0 交付物）：**Gitea 单二进制 fixture**（Windows 可跑，`gitea.exe web` + 内置 SQLite）+ **WireMock.NET 契约测试**；本地 git 状态矩阵夹具（`reqs/fixtures/m2-matrix.json`）（附录 C）；`IClock` 假时钟；BenchmarkDotNet 夜间基线；
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
| L2 | 抽屉/二级页面：组件详情、单仓详情、CI 流水线（占位页）、任务中心、通知中心、日志与诊断、设置、未绑定仓库、组件管理/“已移出”视图 | **一律经菜单栏 A 或标题区 B 进入**；不占主界面区域；不设左侧图标导航（旧 8 项导航废弃） |
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
   动作清单 = Clone / Pull / Fetch / Checkout 分支 / 切 Tag / 受控 SYNC / Delete / Restore / Create 分支 / 纳入组件 / 移出组件（含 dry-run 预览与确认阈值 FR-BOARD-04）；批量执行可跳转任务中心查看逐仓结果。
2. **二级入口（统一经菜单栏/标题区，不占主界面区域）**：组件管理（纳入/移出、已移出视图）、任务中心（菜单「工具」/状态栏队列段）、通知中心（标题区铃铛/菜单）、日志与诊断（菜单「工具」）、设置（菜单/标题区齿轮）、CI 流水线（菜单「查看」）、未绑定仓库（菜单「工具」/标题区异常计数）；均为抽屉或二级页面，打开不改变 A–G 的 bbox。
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

> **唯一权威 AC 目录 = 本文件 Annex A。** 自 v0.4 起，SRS 正文、AC 目录、追溯矩阵、错误码附录合并为本文件；`§13.1.1`（AC-UI-01…16）与 `§13.1.2`（AC-UI-10a/b/c/15、AC-UI-16、AC-SAFE-14…17、AC-OP-33/34/35）是 Annex A 的组成部分，不再要求“回写外部文件”。Annex A 采用 `AC-<域>-<NN>` 主编号；`§13.1` 旧编号仅作历史映射，不得用于验收。
> 状态矩阵单源夹具：`reqs/fixtures/m2-matrix.json`（Host 单测与 CDP 共用；禁止第三份手写期望）。测试/证据资产由 `tests/MbdGitTool.Host.Tests` 与 `ui/scripts` 实现；不再依赖已删除的 `reqs/` 图集或 freeze-checklist。

### 13.1 AC 清单（v0.1：27 条旧编号；红队复审后补；M0 冻结为 Given/When/Then 全量版）

> 〔**旧编号清单，仅作旧→新映射与历史追溯**；新编号唯一权威、旧号仅映射（映射见 Annex A §1.4）。验收一律使用 Annex A 新编号。〕

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

> 本节 **AC-UI-01…AC-UI-16** 为 UI 验收权威源（含组件管理入口）；与 `本文件 Annex A` 中 AC-BOARD/AC-DETAIL/AC-CI 的**布局与交互断言冲突时以本节为准**（Annex A 已含本表；本节与 Annex A 冲突时以本节 UI 语义 + Annex A 安全断言为准）。驱动方式固定为 **WebView2 CDP**（`WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--remote-debugging-port=9222`）+ `Input.dispatchMouseEvent` / `Input.dispatchKeyEvent` 真实事件（不用 JS click）+ DOM bbox/`scrollWidth` 断言 + 截图归档 `docs/images/ui-v3/`。**D-025 新增/拆分的 AC-UI-10a/10b/10c/15 不在本表重复定义，统一见 §13.1.2（避免双源）；M2a 出口 AC = AC-UI-08/09/10/10a/10b/11/13/15 + §13.1.2 的 AC-SAFE-14…17 与 AC-OP-33；AC-UI-10c 属 M2b。**

| AC | Given/When/Then（可测） |
|---|---|
| AC-UI-01 工作台布局 | Given 1440×900、1280×800、150%、200% 视口，When 打开工作台，Then A–G 7 区域全部存在、顺序 A→B→(C\|D\|E)→F→G、C/D/E 同一行且 D 为 2×2；各区域高/宽落在 §11.0.3 区间；`document.scrollingElement.scrollWidth === clientWidth`（无页面级横滚）；键盘 Tab 顺序 A→C→D→E→F→G（跳过纯展示状态栏）、焦点环 2px 可见（FR-WS-05）；截图与参考图逐区域对比（位置/层级/内容类别）通过 |
| AC-UI-02 组件卡片列表 | Given 1,000 组件快照，When 渲染工作台，Then 左侧卡片列表为唯一默认列表视图（**无表格表头/表格行**）、卡片含 §11.3 三行字段、卡片底色中性、默认排序 = severity_rank 升序→组件 ID；滚动 10s ≥58fps；批量条出现时 C/D/E bbox 不变；菜单栏/右键/批量条 3 个批量入口可达；**卡片区七态（空/加载/错误可重试/离线/过期/部分失败/权限过期）注入后均可渲染并重试**（FR-CARD-05）；旧 AC-BOARD-01 的表格断言作废 |
| AC-UI-03 四仓文件树 | Given 选中一个 4 仓组件，When 展开/单击/双击，Then D 区为 Design(左上)/Impl(右上)/MIL(左下)/SIL(右下)；每格懒加载 ≤300ms、排序=目录优先→名称升序、默认根展开 1 层；单击设聚焦仓（E 面板与 F 按钮条同步）；双击文件调用系统默认程序（CDP 拦截 shell 调用并断言参数为 canonical 路径）、双击文件夹走资源管理器；缺仓格显示空态且不伪造树内容 |
| AC-UI-04 文件树只读与路径安全 | Given 构造 `..`、绝对路径、UNC、reparse 越界、保留名（CON/NUL）、尾随空格请求，When 调用文件树/打开文件 API，Then 全部拒绝并返回统一错误码且不泄露越界内容；Given 文件树 DOM，Then 不存在新建/重命名/删除/拖放/在树内编辑入口；右键菜单仅只读项 |
| AC-UI-05 文件树状态标记 | Given 附录 C 状态夹具，When 打开文件树，Then 仓库根徽标=该仓主状态（§11.2 token/图标/文案）、tracked 改动文件 `●`、untracked `○`、STALE 灰化+数据时间；展开文件树不新增 git 进程（进程计数断言） |
| AC-UI-06 文件夹视图 | Given 工作区根含 1 个未克隆组件 + 1 个未纳管目录，When 切换文件夹视图，Then 树层级为 根→组件→四仓→文件；未克隆淡显、未纳管入分组；组件/仓库节点状态与工作台同快照逐字一致；过滤与搜索生效；400 组件/1,600 仓滚动 ≥58fps |
| AC-UI-07 视图切换 | Given 工作台已选中组件 X 且展开某仓目录，When `Ctrl+2` 切到文件夹视图再 `Ctrl+1` 返回，Then 切换 ≤200ms、selection 与展开路径保留、两视图状态一致；**旧看板表格视图入口不存在**（D-023） |
| AC-UI-08 单仓按钮条 | Given 工作台且未选中聚焦仓，Then 5 按钮全部禁用并提示“请先在文件树中选择仓库”；Given 选中仓，Then 按 §7.6 逐格断言启用/禁用（14 主状态 × 5 按钮矩阵）；按钮顺序/文案固定 `PULL→FETCH→CHKOUT→COMMIT→PUSH`、不换行、1280 下不隐藏；**M2a 出口的逐格期望一律取单源夹具 `reqs/fixtures/m2-matrix.json`（Host 单测与 CDP 共用，禁止第三份手写期望）**，Host `GetAvailability` 的 `enabled/requiresConfirm/reasonCode` 与 UI 逐格一致，禁用 tooltip 冻结格式见 §13.1.2 前言（`原因 [E_*] → 替代动作`） |
| AC-UI-09 M1 只读边界 | Given M1 构建，When 点击任一底部按钮，Then 5 按钮全部不可执行（含 FETCH）、只给出“M1 只读”理由，写操作数=0、仓库零改动；Given M0 构建，Then 按钮禁用 + 占位说明；Given **M2a 构建**，Then 按 FR-ACT-02…04 与 FR-ACT-05/06 的 **M2a 子集**逐项启用（取消/job/任务中心除外，归 M2b，见 §13.1.2 AC-UI-10c），且每次点击必须有可感知结果（执行/确认条/明确错误码/禁用原因/M2b 未接入提示），禁止“可点无效果”；Given M2b 构建，Then 按 FR-ACT-02…06 全量启用 |
| AC-UI-10 按钮前置/确认/失败 | Given 无 staged 的 DIRTY 仓，Then COMMIT 禁用并给原因；Given 非快进 PULL，Then 显示 ff-only 失败指引且无 merge commit；Given PUSH 无 upstream，Then 提示“发布分支”；Given COMMIT/PUSH，Then 确认条出现且未确认时写操作数=0；Given 外部 `index.lock`，Then 返回 BUSY、不删锁；Given 操作失败，Then 状态栏显示错误码+中文文案且可重试；取消/进行中语义符合 FR-ACT-05；完成后 ≤2s 回写并写审计。**v0.3.1 拆分（D-025）**：确认条细则拆为 **AC-UI-10a（COMMIT，对话框→确认条两步）/ AC-UI-10b（PUSH）**，取消/job 归 **AC-UI-10c（M2b）**；三者定义见 §13.1.2，本行只保留 M2a 通用前置/失败/回写断言 |
| AC-UI-11 右侧信息面板 | Given 选中仓，Then E 区显示 §11.3 三段字段（含主状态+数据时间、CI 空态）；未选中显示引导空态且无残留数据；同一时刻 E 与 F 指向同一聚焦仓；1280/200% 下窄栏不横向溢出、标签列 ≤72px；详情抽屉开/关后 A–G bbox 不变 |
| AC-UI-12 CI 10 锚点新落点 | Given v1 构建，Then §11.4 的 CI-1…CI-10 **全部存在（10/10）**；CI 槽位定高不换行；注入 CI FAIL 后组件卡片/信息面板底色与聚合徽标不变；`ci.enabled=true` 后仅数据变化、锚点位置与布局 bbox 不变；全站无裸 TODO |
| AC-UI-13 二级入口与抽屉 | Given 工作台，Then 任务/通知/日志/设置/CI/未绑定/组件管理 7 个入口经菜单栏或标题区可达（7/7）；抽屉打开时 C/D/E bbox 不变、ESC 关闭且焦点回落、焦点陷阱生效；**无左侧图标导航、无第三条全宽工具栏** |
| AC-UI-14 旧布局废弃回归 | Given v1 构建，Then 页面不存在左侧图标导航、表格表头/表格行、340px 详情列；默认入口为工作台；§11.7 速查表逐项 CDP 断言通过；截图区域顺序与参考图一致 |

### 13.1.2 v0.3.1 M2a/M2b 新增与拆分 AC（AC-UI-10a/10b/10c/15、AC-SAFE-14…17、AC-OP-33；权威定义）

> 本小节是 D-025 冻结的 **M2a 出口 AC 中新增/拆分项的唯一权威定义**（2026-11-14 Gate G-P0-4 回写；此前本节缺失，导致 M2a 出口 8 条 AC 不可独立验收）。§13.1.1 的 AC-UI-10 行只保留“拆分引用”，不再重复定义；本节与 §13.1.1 冲突时以本节为准，二者均与 `reqs/adr/D-025-m2a-m2b.md` §2.2/§3/§5 一致，**不得以任何理由放宽安全要求**。
>
> **M2a 出口 13 条**（与 ADR §3 逐条对应）= §13.1.1 的 `AC-UI-08/09/10/11/13` + 本节的 `AC-UI-10a/10b/15`、`AC-SAFE-14/15/16/17`、`AC-OP-33`。**`AC-UI-10c` 属 M2b**：M2a 验收报告不得把 10c、`AC-OP-01…32`、`AC-REPO-01…11`、`AC-SAFE-01…13` 记为通过或“等价通过”。
>
> **单源矩阵夹具（v0.3.1 冻结）**：14 主状态 × 5 键的 `enabled/requiresConfirm/reasonCode` 一律取 `reqs/fixtures/m2-matrix.json`（由 §7.6 与 D-025 冻结口径派生）；**Host 单测（`RepoActionAvailabilityMatrixTests` 等）与 CDP 脚本 `ui/scripts/cdp-m2a-selfcheck.mjs` 共用该夹具，禁止第三份手写期望**。`requiresConfirm` 规则（逐格冻结在夹具内）= **矩阵 C 单元 ∨ 动作 ∈ {COMMIT, PUSH}**；`reasonCode` ∈ `AVAILABLE / CONFIRM_REQUIRED / STATE_BLOCKED`。夹具的 M2a 构建口径含两处冻结覆盖：① `DIVERGED.PULL` 按禁用（原 ⚠ 作废）；② `EMPTY.PUSH` 按“空仓无提交”禁用且 tooltip 指向 M2b 首推（M2b 构建恢复矩阵）。
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
- **Then**：① CHKOUT 命令必须含 `--no-overwrite-ignore`；PULL 必须为 `fetch` + `merge --ff-only --no-overwrite-ignore`（**不得**用不接受该开关的 `git pull`）；② git 拒绝覆盖时返回 `E_WORKTREE_OVERWRITE`（（固定 `E_WORKTREE_OVERWRITE`），不得静默成功），`build.log` 内容仍为 `LOCAL-PRECIOUS`、HEAD/分支未变、数据零丢失；③ 任何路径都不得出现 `-f`/`--force` 覆盖；④ 失败时状态栏/响应含可读原因，仓库零改动。
- **关联 NFR/OQ**：FR-SAFE-03；ADR §2.2-2③；Gate P1-1

#### AC-SAFE-17 · ref 校验：不存在 ref / 前缀注入 / PULL 显式分支 / detached PUSH / 无 force
- **覆盖 FR**：FR-ACT-03、FR-REPO-01、FR-SAFE-03
- **测试层级/依赖**：Host 单测/黑盒集成（临时 git 仓 + `GIT_TRACE2_EVENT`）
- **Given**：仓在 `main`，含 `README.md`（同名文件但无同名 ref）、tag `v1.0.0`、远端新分支 `feature`（`main` 为其祖先）、detached 夹具、可正常 push 的仓。
- **When**：① CHKOUT `branch="README.md"`；② CHKOUT `tag="v9.9.9"`；③ CHKOUT/PULL `branch="+main"`/`"-main"`；④ PULL `args.branch="feature"`（当前分支 `main`）；⑤ DETACHED 状态 PUSH；⑥ 正常 PUSH 并采集 argv。
- **Then**：① `E_REF_NOT_FOUND`（kind=branch），HEAD/分支不变（禁止“假成功”走路径模式）；② `E_REF_NOT_FOUND`（kind=tag）；③ `E_INVALID_ARGUMENT`；④ **拒绝**（`E_ACTION_NOT_ALLOWED`，details 给 expected/current），当前分支**不得**被快进到 `origin/feature`，且拒绝发生在任何 git 网络命令之前（argv 无 fetch/push）；⑤ `E_ACTION_NOT_ALLOWED`，HEAD 不变；⑥ PUSH argv 无 `--force`/`-f`/`--mirror`/`--delete`/前导 `+`；⑦ 仅远端存在的分支必须真实建本地跟踪分支并记录（AC-OP-09 已支持）；若建分支失败则 `E_REF_NOT_FOUND` + UI 明确文案，禁止静默 DWIM 假成功。
- **关联 NFR/OQ**：FR-REPO-01；ADR §2.2-2④；Gate P1-3/N-1（N-1 未修复前本 AC 判未通过）

#### AC-UI-16 · 组件纳入/移出入口

- **覆盖 FR**：FR-OP-12、§16.1
- **测试层级/依赖**：VIS + IT（工作台/已移出视图）
- **Given**：工作台有已纳管组件 A，另有未纳管远程组件 NEWC。
- **When**：① 打开“纳入组件”输入 NEWC；② 打开 A 的“移出组件”。
- **Then**：① 纳入入口可见、弹层显示四仓 full_name/缺仓、确认后看板出现 NEWC；② 移出弹层显示“停止管理/本地删除”差异、影响仓和 `.trash` 提示，默认不执行；③ 停止管理后默认视图隐藏、进入“已移出”视图可见、可重新纳入；④ 本地删除需输入组件 ID 强确认，失败时组件不置已移出；⑤ 两操作均可在审计中查到 `COMPONENT_BIND/UNBIND`，远端零写。
- **关联 NFR/OQ**：FR-OP-12、§16.1。
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

| FR 分组 | 覆盖新 AC（权威：`Annex A` §3 FR→AC 矩阵 + 本文件 §13.1.1/§13.1.2） | 缺口处理 |
|---|---|---|
| FR-CFG | AC-CFG-01…AC-CFG-11 | — |
| FR-SYNC | AC-SYNC-01…AC-SYNC-14 | — |
| FR-SCAN | AC-SCAN-01…AC-SCAN-13 | — |
| FR-STA | AC-STA-01…AC-STA-15；跨域 AC-SCAN-09、AC-DETAIL-02、AC-OP-11/12 | — |
| FR-BOARD | AC-BOARD-01…AC-BOARD-16（**表格相关断言由 AC-UI-02 取代**）；跨域 AC-STA-08、**AC-UI-01/02/07** | 旧表格默认视图断言标记作废（D-023） |
| FR-DETAIL | AC-DETAIL-01…AC-DETAIL-09；跨域 AC-SAFE-10、**AC-UI-03/04/11/13** | 详情抽屉化由 AC-UI-11/13 覆盖 |
| FR-OP | AC-OP-01…10、AC-OP-15…35；跨域 AC-SYNC-05/14、AC-SCAN-06、AC-OPS-04（AC-OP-11…14 按 D-029 暂定） | — |
| FR-REPO | AC-REPO-01…AC-REPO-11；跨域 AC-OP-05、**AC-UI-08/10** | 底部按钮为单仓操作 v1 入口 |
| FR-SAFE | AC-SAFE-01…AC-SAFE-17（**v0.3.1 新增 AC-SAFE-14 凭据作用域 / 15 CAS / 16 ignored / 17 ref 校验，定义见 §13.1.2，M2a 出口**）；跨域 AC-OP-16/20/26、AC-CFG-06、AC-DETAIL-06/07、AC-OPS-06、**AC-UI-04** | M2a 出口=AC-SAFE-14…17；AC-SAFE-01…13 归 M2b 出口，不得混记 |
| FR-OPS | AC-OPS-01…AC-OPS-17；跨域 AC-SAFE-06、**AC-UI-13**（二级入口）、**AC-UI-01**（状态栏模板）、**AC-OP-33**（v0.3.1 新增审计-lite，定义见 §13.1.2，M2a 出口） | M2a 只要求“一次操作一条最小列审计”；查询/导出/90 天保留归 M2b |
| FR-CI | AC-CI-01…AC-CI-11；跨域 AC-DETAIL-03、AC-OPS-02、**AC-UI-12**（10 锚点新落点） | CI-1/3/4/7 位置迁移由 AC-UI-12 断言 |
| **FR-WS** | **AC-UI-01、AC-UI-05、AC-UI-07、AC-UI-13、AC-UI-16** | v0.3 新增；Annex A 已含 |
| **FR-TREE** | **AC-UI-03、AC-UI-04、AC-UI-05** | 同上 |
| **FR-FOLDER** | **AC-UI-06、AC-UI-07** | 同上 |
| **FR-ACT** | **AC-UI-08、AC-UI-09、AC-UI-10、AC-UI-10a、AC-UI-10b、AC-UI-15**（v0.3.1 拆分/新增定义见 §13.1.2；AC-UI-10c 属 M2b） | M1 只读边界=AC-UI-09；M2a 写操作=AC-UI-08/10/10a/10b；M2b 边界=AC-UI-15；取消/job=AC-UI-10c |
| **FR-CARD** | **AC-UI-01、AC-UI-02、AC-UI-07** | 同上 |
| **FR-INFO** | **AC-UI-03、AC-UI-11、AC-UI-12** | 同上 |
| NFR | 性能：AC-BOARD-03/05/14、AC-SCAN-08/09、AC-SYNC-12、AC-DETAIL-04、**AC-UI-01/02/06**；安全：AC-SAFE-05/06/07/12/13、AC-CFG-06/07、**AC-UI-04**；可靠/并发：AC-OPS-09/10、AC-OP-29、AC-SAFE-08/09 | — |

> **v0.4 单源化说明**：本文件 Annex A 是唯一 AC 目录；§13.1.1/§13.1.2 已并入 Annex A，不再存在“待回写外部 AC 目录”。M0/M1/M2 出口引用 AC-UI 编号时直接引用 Annex A。
>
> **v0.3.1 M2a 门禁（D-025，Gate G-P0-4 回写）**：M2a 出口 = **13 条 AC 全绿 + 每条都有可复跑证据**：`AC-UI-08/09/10/10a/10b/11/13/15`（§13.1.1 + §13.1.2）+ `AC-SAFE-14/15/16/17` + `AC-OP-33`（§13.1.2）。证据物三件缺一不得宣布 M2a 通过：① `docs/m2a-ac-report.md`（逐 AC 证据 + 真实 Gitea 五键写 + 审计行查询）；② `reqs/fixtures/m2-matrix.json`（14×5 单源，**Host 单测与 CDP 共用**，禁止第三份手写期望）；③ `ui/scripts/cdp-m2a-selfcheck.mjs`（旧 `cdp-m2-selfcheck.mjs` 作废，不得作为证据）。**M2a 通过 ≠ M2 交付完成**；M2b 未接入前只能写“M2a 通过、M2b 未接入”。

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
| **新工作台造成旧 AC/评审材料失配**（历史材料已删除；Annex A/§11.7 已合并） | 验收口径冲突、追溯断裂 | §13.1.1 暂为 v0.3 UI 权威源；Annex A/§11.7 提供旧→新断言映射 |
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
| OQ-23 | 错误码全表（含可重试性/日志级别/用户文案）何时冻结？ | **已关闭**：v0.4 附录 D 冻结 Host 码与 srsCode 映射 | 否 |
| OQ-24 | P2 FR（FR-CFG-06 / FR-DETAIL-05 / FR-REPO-04 / FR-OPS-07）版本归属？ | 全部 v1.1（v1 只保留入口不存在/不可用；完整行为 v1.1，M0 不冻结） | 否 |
| OQ-25 | 旧看板「表格视图」是否保留为 v1.1 可选视图（默认工作台）？ | 保留为 v1.1 备选；v1 入口不存在、不验收（D-023） | 否（v1.1 再定） |
| OQ-26 | 参考图右侧窄栏为示意宽度，实现取 18%（220–320px）是否可接受？ | 接受；以 §11.0.3 区间 + AC-UI-01 断言为准，不追求像素复刻 | 否（D-022 已裁决） |
| OQ-27 | 文件夹视图是否保留右侧信息面板（18%）？ | 保留（A/B/F/G + 工作区树 + 右侧信息面板）；不合并为纯树 | 否（§11.5 已定） |
| OQ-28 | 文件树是否显示 `.git`/`.trash`/未纳管目录？ | 默认隐藏 `.git`/`.trash`；未纳管目录仅在文件夹视图“未纳管”分组显示 | 否（FR-TREE-01/FR-FOLDER-01） |

---

## 附录 A：草案缺陷 → 处置对照（摘要，详见 历史评审（已删除））

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
| drawio 不可作为执行依据 | 图集拆分建议见历史评审（已删除）；本文件第 11/12 章取代其执行语义 |

## 附录 B：资产索引

| 文件 | 用途 |
|---|---|
| `reqs/specification.md` | **唯一维护源（本文件）** |
| 历史评审（已删除） | 需求审核结论（评审者视角） |
| `历史评审（已删除）01-requirements-critic.md` | 需求质量评审（27 发现） |
| `历史评审（已删除）02-git-domain.md` | Git 语义评审（24 发现 + 修正状态机/伪代码） |
| `历史评审（已删除）03-architecture.md` | 架构可行性评审（28 发现；推荐 Host+GitWorker / SQLite WAL / 分层采集） |
| `历史评审（已删除）04-ux-ci.md` | UI/CI 预留评审（28 发现，10 锚点） |
| `历史评审（已删除）05-drawio-execution.md` | drawio 执行方案评审（30 发现，图集拆分） |
| `历史评审（已删除）06-redteam-srs.md` | SRS v0.1 红队复审（假关闭 P0 / AC 缺口 / Top 10） |
| `本文件 Annex A` | **权威 AC 目录**（188 条 AC / 104 FR / 追溯矩阵 / 红队处置） |
| `本文件 §7.2/§7.4/§8.1（图集语义已并入正文）` | 不再维护独立 D1–D6 文件；执行流程/状态/CI 锚点以正文为准 |
| `reqs/fixtures/m2-matrix.json` | 14×5 单源矩阵夹具（Host 单测 + CDP 共用）；其余状态夹具由测试代码按附录 C 构造 |
| `本文件 §5.4` | v0.2.1 冻结检查清单与条件跟踪（C1–C6） |
| `历史评审（已删除）07-freeze-review.md` | 第一轮冻结复评（✗ 不可进入 M0，C-01…C-06/H-01…H-06） |
| `历史评审（已删除）08-freeze-review-v2.md` | 第二轮冻结复评（☑ 有条件进入 M0，C1–C6） |
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

> 夹具映射：附录 C 状态由测试按本节构造（不再维护 测试内构造的状态夹具 生成器）；`NO_UPSTREAM` 的 `expected_sub_type` 使用完整枚举名（`LOCAL_ONLY` / `UNSET_BUT_EXISTS` / `UPSTREAM_GONE`）。`tag_present`、`tag_moved` 为 baseline/flag 语义，`expected_flags` 仅收录 7.3 定义的 flag。

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

**错误码（v0.4 冻结：srsCode 表 + Host 映射表）**

> 下表为 `details.srsCode` 的稳定错误码；Host 对外码与重试/日志级别见 D.2。R-06 于 v0.4 关闭。

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
| `E_DELETE_PENDING` | 删除因文件占用未完成，已保留 pending 记录（Host 码；`details.srsCode=FS-E001`） | 解除占用后重试/取消 pending |
| `E_AUTH_FAILED` | Gitea 401/403（Host 码；`details.srsCode=NET-E001`） | 重新认证 |
| `E_REMOTE_NOT_FOUND` | Gitea 404/仓库不存在（Host 码；`details.srsCode=NET-E001`） | 确认 org/repo |
| `E_NETWORK` | 网络不可达/超时（Host 码；`details.srsCode=NET-E001`） | 检查网络后重试（≤2 次） |
| `E_DISK_FULL` | 磁盘空间不足（Host 码；`details.srsCode=FS-E001`） | 释放空间 |
| `E_PATH_TOO_LONG` | 目标路径超长（Host 码；`details.srsCode=FS-E001`） | 缩短工作区路径 |
| `FS-E002` | 文件树路径越界/非法（`..`/绝对路径/UNC/reparse 越界/不属于已绑定仓） | 拒绝访问并刷新树；不泄露越界内容 |
| `FS-E003` | 打开文件/文件夹失败（无关联程序/被占用/已删除/长度超限） | 换用默认程序或资源管理器；可读文案 + 错误码 |
| `NET-E001` | 网络超时/断网 | 降级只读 |
| `GIT-E001` | 空仓库/unborn | 首推/初始化 |
| `SEC-E001` | 检测到既有凭据明文 | 告警并引导清理 |

### D.2 Host 错误码映射（v0.4 冻结）

| Host 码 | srsCode | 可重试 | 日志级别 | 用户动作 |
|---|---|---|---|---|
| E_ACTION_NOT_ALLOWED | OP-E004 | 否 | WARN | 按 §7.6 替代动作 |
| E_CONCURRENT_CHANGE | OP-E002 | 是 | WARN | 重新预检 |
| E_REPO_BUSY | OP-E001 | 是 | WARN | 等待外部 git 结束，禁止删锁 |
| E_CREDENTIAL_SCOPE / E_GITEA_CONFIG_INVALID / E_GITEA_INSECURE_HTTP | CFG-E001/E002 | 否 | ERROR | 修正配置/凭据 |
| E_AUTH_FAILED | NET-E001 | 否 | WARN | 重新认证 |
| E_REMOTE_NOT_FOUND | NET-E001 | 否 | WARN | 确认 org/repo |
| E_NETWORK | NET-E001 | 是（≤2） | WARN | 检查网络后重试 |
| E_DISK_FULL | FS-E001 | 否 | ERROR | 释放空间 |
| E_PATH_TOO_LONG | FS-E001 | 否 | ERROR | 缩短路径 |
| E_DELETE_PENDING | FS-E001 | 是 | WARN | 解除占用后重试/取消 pending |
| E_WORKTREE_OVERWRITE | FS-E001 | 否 | WARN | 先处理本地文件 |
| E_REF_NOT_FOUND | GIT-E001 | 否 | WARN | 先提交/确认 ref |
| E_NOTHING_TO_COMMIT | OP-E004 | 否 | INFO | 先 add 暂存 |
| E_INVALID_ARGUMENT | OP-E004 | 否 | WARN | 修正参数 |
| E_GIT_NON_FF | GIT-E001 | 否 | WARN | 手动 merge/rebase 后重试 |
| E_REPAIR_FAILED | FS-E001 | 否 | ERROR | 按 diff 修复/改用 Re-clone |
| E_RECLONE_FAILED | NET-E001 | 是 | ERROR | 检查远程/清理后重试 |
---

---

## 16. M2b 增量与完成定义（v0.4 冻结）

> 本章只补充实现 M2b 时发现的规格缺口，**不降低**第 1–15 章与 Annex A 的任何安全/验收要求。评审通过并冻结后才可开发；实现中发现新歧义须走本文件修订，不得直接改代码。
> 本章与正文冲突时，安全/范围硬约束以正文为准。新增行为必须映射到 Annex A 的 AC；无法映射的，在冻结前新增同域下一个未占用 AC 编号（禁止改已冻结 AC 的 Then）。
> 本版已按三路需求评审 P0 修订；仍待复评。

## 16.1 组件纳入 / 移出（FR-OP-12 / AC-OP-31、AC-OP-32）

- **模型维度**：`component.is_managed/is_deleted` 是**管理状态**，不是 §7.4 的 15 个聚合状态之一；看板“已移出”是视图过滤，不是新增聚合态。纳管组件四仓缺失时仍按 MISSING/BINDING_ERROR 计算并显示。
- **默认与迁移**：升级/全新安装时，**已由扫描发现的四仓组件默认 `is_managed=1,is_deleted=0`**（不隐藏既有工作区）；只有用户显式移出才置 0/1。迁移在启动扫描事务内完成。
- **唯一写者**：`is_managed/is_deleted` 由新增 `COMPONENT_MGMT` 写者写（ADR 修订 §8.1）；`component` 其余列仍由 STATUS_CALC 写，`repo` 远程列仍由 REMOTE_SYNC、本地列仍由 LOCAL_SCAN 写。写者变更见 ADR D-028。
- **纳入**：`bindComponent { componentId, displayName? }` → 远程只读校验四仓（full_name 清单、缺仓清单）；远程不可用 fail-closed、不写库；缺仓允许纳入但标 BINDING_ERROR（D-015）；确认后写 management 状态并审计 `COMPONENT_BIND`，**不直接改 repo 远程/本地字段**。
- **移出**：`unbindComponent { componentId, mode }`：
  - `STOP_MANAGE`：`is_managed=0`；本地目录/repo 行保留；默认视图过滤，“已移出”视图可见；可重新纳入。
  - `LOCAL_DELETE`：**必须复用 DELETE job 语义**（`confirm=true` + `confirmText=componentId` + 四阻断预检 + `.trash`），仅当四仓全部删除成功或已不存在才置 `is_deleted=1`；`is_local` 由下一次 LOCAL_SCAN 置 0，管理 API 不直接写；任一仓失败 → 整体 `PARTIAL`，不将组件置已移出，列出失败仓并允许重试；不提供“强制删除”。
  - 两种方式均审计 `COMPONENT_UNBIND`（details 含 mode/逐仓结果），远端零写。
- **UI**：工作台提供“纳入组件”“移出组件”入口；纳入弹层显示四仓 full_name/缺仓；移出确认必须展示“停止管理 vs 本地删除”差异、影响仓与 `.trash` 备份说明；入口与“已移出”视图按 AC-UI-16 验收。
- **验收**：AC-OP-31/32 原断言 + 默认迁移（既有组件仍显示）+ 部分失败 PARTIAL + 重新纳入恢复。

## 16.2 DELETE_PENDING（FR-OP-06 / AC-OP-23）

- **触发**：`Directory.Move` 到 `.trash` 因文件占用/权限等可恢复错误失败。
- **载体**：只使用文件记录 `<workspace>/.trash/.pending/<stamp>-<repoName>.json`，字段 `{componentId,repoType,repoName,localPath,errorCode,createdAt}`；**不在 DB 新增 pending 表/列**。启动扫描读取 pending 文件重建“待处理”视图；扫描排除 `.trash` 本身，但读取 `.pending` 记录。
- **状态/错误码**：`job_item.state=FAILED`、`errorCode=E_DELETE_PENDING`（新增 Host 码，附录 D `details.srsCode=FS-E001`）；任务中心显示“待处理（文件占用）”，**DELETE_PENDING 不是 §7.2 主状态/§7.4 聚合态**。
- **重试安全**：必须重新扫描并 canonicalize 路径、重跑 DELETE 全部四阻断（dirty/未推送/in-progress/stash）、校验路径仍在工作区且非 reparse；任一变化 → 拒绝并保留记录。禁止按旧 JSON 的 localPath 直接移动。
- **取消/GC**：用户可显式取消 pending（删除记录，目录不动）；pending 记录保留 30 天，超期由 retention 清理只删记录不删目录。
- **审计**：action=`DELETE`；result=`DELETE_PENDING`/OK/FAILED；不删除锁文件、不递归强删。
- **验收**：AC-OP-23 按本口径重写：占用文件→目录/数据不变+pending 记录+job_item FAILED/E_DELETE_PENDING；解除占用后“仅重试失败项”成功；取消/超期行为可断言。

## 16.3 Repair / Re-clone（FR-REPO-05 / AC-OP-05、AC-REPO-08）

- **job kind**：新增 `REPAIR`、`RECLONE` 到 §8.1 `job.kind` 合法集（ADR D-028），逐仓执行；UI 入口在组件抽屉/任务中心。
- **REPAIR（非 git 目录）零改动约束**：在目标目录内先创建临时 git 元数据（`git init` + origin + fetch + 按远端默认分支 checkout 到临时索引/工作区）无法保证“零改动”，因此规定：
  - REPAIR 对非 git 目录执行时，先把原目录**全部内容**移到 `.trash/<stamp>/<repoName>.repair-backup`（保留 30 天，类型 `repair-backup`），确认框列出将移走的文件清单/数量；再 `git init`/fetch/checkout；成功后备份按 `.trash` 保留期清理，不立即删除；文件移走失败则零改动。
  - 任一步失败 → 回滚到原目录（move back），失败结果 `FAILED`，原文件 hash 不变；
  - 目标目录非空且含不可安全保留内容 → 默认拒绝，提示改用 RECLONE。
- **RECLONE（origin 不匹配/远程不可用）**：
  - 前置：远程可达、凭据作用域通过、原目录通过 clean/未推送/untracked/stash 预检（否则拒绝）；
  - 先把原目录移动到 `.trash/<stamp>/<repoName>`（stamp 唯一，冲突则加后缀），再 clone；clone 失败 → 自动 move back 并 FAILED；
  - 成功后由下一次 LOCAL_SCAN 置 `is_local=1` 并重算状态；`.trash` 备份保留 30 天。
- **确认**：REPAIR/RECLONE 均需确认；RECLONE 需输入组件 ID 强确认并展示 `.trash` 备份路径。
- **审计**：action=`REPAIR`/`RECLONE`，含 pre/post HEAD 与结果；失败零改动（按上述回滚）。
- **验收**：AC-OP-05 目标存在不覆盖/转 Repair 提示；AC-REPO-08 两场景 + 失败回滚。

## 16.4 Tag 与 baseline_snapshot（暂定：D-029，不参与 M2b 验收）

- TAG = 检出同一 Tag，规则见 AC-OP-11…14。
- **写入规则（唯一口径）**：每次 TAG job 对组件固定写 **4 行** `baseline_snapshot`：
  - 命中仓：`tag_present=1`、`commit_sha=rev-parse <tag>^{commit}`、`tag_object_sha=rev-parse <tag>`（轻量 tag 与 commit 相同；annotated 为 tag 对象 SHA）、`captured_at`；
  - 缺 Tag 仓：`tag_present=0`、`commit_sha=NULL`、`tag_object_sha=NULL`、`captured_at`。
- **主键/历史**：沿用 §8.1 PK `(component_id, tag, repo_type)`；重复 TAG/移动 tag **覆盖同键行**，历史由 `operation_log` 保留（action=TAG，before/after 含旧/新 commit）。AC-OP-13④ 的“不丢历史行”按此解释，不再要求 baseline 表多版本行。
- 四仓全部 present → 组件不漂移；任一缺 → `TAG_DRIFT`。baseline 写入仅在 TAG job 收口时按逐仓结果一次性写 4 行（失败 job 不写半套；job_item 失败但仍可写 `tag_present=0` 的完整 4 行，按 job 收口事务）。
- **验收（暂定，D-029）**：AC-OP-11/12/13/14 不参与 M2b 验收/实现计划；如未来恢复，按本口径修订，四仓混合与 tag 移动用例必须断言 4 行/覆盖/operation_log 历史。

## 16.5 CREATE 撤销（FR-OP-07 / AC-OP-25）

- **基线持久化**：创建成功时写 `refs/baseline/create/<ts>/<branch-encoded>/<TYPE>`，并在 `job_item` 记录 `baseline_ref`（§8.1 增列，ADR D-028）。
- **API**：`undoCreate { jobId }` 创建 `CREATE_UNDO` job（逐仓执行，写 journal/审计），仅撤销该 job 成功创建的分支；逐仓校验：分支仍存在、指向 baseline_ref、未被当前检出、无新提交；满足则 `git branch -D`（仅本地）；任一仓不满足 → SKIPPED + 原因；部分成功逐仓报告，不整体回滚。
- **写前 journal**：`CREATE_UNDO` 作为 job kind 走 JOB_RUNNER，写 STARTED/DONE/FAILED 与 `job_item`，不依赖不存在的 journal 表。
- **审计**：action=`CREATE_UNDO`；不删除远端分支；失败零改动。
- **保留/清理**：`refs/baseline/create/*` 保留 30 天，由 retention 清理；job 历史保留 200 条。
- **验收**：AC-OP-25 增加“撤销成功/已切走拒绝/有新提交拒绝/partial”。

## 16.6 故障注入 / G-WIRE（AC-SAFE）

- **启用约束**：仅 `MBD_FAULT_INJECT` 环境变量 + **Debug/Test 构建符号**同时满足时启用；Release/生产忽略并日志告警。配置为 JSON，含 `point/action/delayMs/errorCode`。
- **注入点（固定枚举）**：`GIT_NET_AUTH_401`、`GIT_NET_404`、`GIT_NET_TIMEOUT`、`INDEX_LOCK`、`DIR_MOVE_BUSY`、`DISK_WRITE_FAIL`、`DISK_SPACE_LOW`、`GIT_SLOW`、`CRASH_BEFORE_WRITE`、`CRASH_AFTER_WRITE`、`CRASH_AT_ITEM_2`。AC-SAFE-05/10/11 等非注入类明确标 N/A。
- **G-WIRE**：外部 HTTP 代理或本地 Gitea 服务端日志记录 `{method,url,status}`；**不记录 Authorization header 值**；结合 `Get-CimInstance Win32_Process` 命令行快照与 remote URL 检查，覆盖 AC-SAFE-13。
- **确定性时序**：取消/崩溃类 AC（OP-26/27、SAFE-09）使用 `CRASH_AT_ITEM_2`/`INDEX_LOCK` 等注入点，不以墙钟“≤2s”作为唯一断言；UI 响应时间改为注入命中后状态机断言。
- **报告**：`docs/m2b-fault-injection-report.md` 逐 AC-SAFE-01…13 给注入点/期望/实测/证据；无注入点的标 N/A 并说明理由。

## 16.7 测试环境门禁与证据分级

- 真实 Gitea 测试默认地址**只写入** `docs/testing-gitea-gate.md`，本需求不硬编码内网 IP；探测规则：`MBD_GITEA_URL`/`MBD_GITEA_PROBE_URL`；GET `<base>/api/v1/version` + `GET /api/v1/user`（带 token，校验身份）+ `GET /api/v1/orgs/{org}/repos?limit=1`（校验 org/权限），超时 2.5s；三项全 2xx 才算 L2 可达。禁止条件恒真/空断言；runner 必须记录每项 HTTP status。
- **两级证据**：
  - L1 本地/CI 必跑：单元、本地 bare remote、Mock CDP、单二进制 Gitea/WireMock 夹具（§13.2 已要求）——**不允许 SKIP**；
  - L2 共享测试实例：可达才跑；不可达 SKIP，写 `artifacts/<name>/result.json` `{skipped:true,url,reason,commit}`，**SKIP 不计入 DoD 的真实证据**。
- M2b final 的真实证据必须至少一次 L2 运行记录（或 L1 hermetic Gitea）并归档 manifest；仅 SKIP 不能声明 AC COVERED。
- 单元测试不得依赖固定 IP；`run-selfchecks.mjs` 汇总时必须区分 PASS/SKIP/FAIL，SKIP 单独列出。

### 16.7.1 测试资产代号（冻结定义）

- `G-E2E`：本地 hermetic Gitea/WireMock + 临时工作区 E2E 夹具；测试内构造，不依赖固定 IP/共享实例。
- `G-WIRE-REST`：REST 契约桩；`G-WIRE-GIT`：git smart-HTTP 代理；两者都只记 `{method,url,status}`，不记 Authorization 值。
- `F-*`（如 `F-DIRTY`、`F-DIVERGED`、`F-TAG_DRIFT`）：附录 C 定义的状态夹具，由测试在临时目录按附录 C 构造；**不再依赖已删除的生成脚本**。
- `C-SYNTH`：合成配置/组件数据夹具，由测试代码生成。
- `CLK`：可注入时钟（仅用于取消/超时状态机测试）；不以墙钟作为唯一断言。
## 16.8 I18N（本轮不做）

- 作为 M3 候选独立 `FR-I18N` 立项，输入见 `docs/i18n-feasibility-input.md`；不纳入 M2b 出口，不因 I18N 调整阻塞 M2b 功能收尾。

## 16.9 M2b 完成定义（DoD）

**状态定义（可判定）**：
- `COVERED`：该 AC 有可复跑证据（自动化或记录在案的真实运行），且证据文件/命令/结果在 `docs/m2b-ac-op-gap.md` 或证据 manifest 中可定位；
- `PARTIAL`：功能存在但 AC 的任一 Then 未满足/未验证；
- `GAP`：未实现或无证据。

**证据 manifest 固定路径/schema**：`docs/evidence/m2b-manifest.json`，字段 `{commit,generatedAt,suite:{name,command,startedAt,finishedAt,result:PASS|FAIL|SKIP,artifact},checks:[{ac,status:COVERED|PARTIAL|GAP,evidence}]}`；CI/本机 runner 生成。

**DoD（全部满足才可打 `m2b` / 宣称 M2 成立）**：
1. Annex A 中 AC-OP-01…10、AC-OP-15…35、AC-REPO-01…11、AC-SAFE-01…13、AC-UI-10c、AC-UI-16 全部 `COVERED`（`PARTIAL`/`GAP` 不计；`AC-OP-11…14` 按 D-029 暂定不参与）；新增 §16 行为对应 AC（AC-OP-31/32、AC-OP-23、AC-OP-25、AC-OP-05/AC-REPO-08、AC-OP-34/35）同；
2. 14 态×5 键矩阵在 M2b 构建复验：消费者=`RepoActionAvailabilityMatrixTests` + `ui/scripts/cdp-m2a-selfcheck.mjs`；命令=`vstest ... --Tests:RepoActionAvailabilityMatrix` + `node ui/scripts/cdp-m2a-selfcheck.mjs`；`reqs/fixtures/m2-matrix.json` 与 Host/UI 一致；
3. `docs/m2b-fault-injection-report.md` 通过（§16.6），无 P0/P1 安全缺陷；
4. 测试：单元/集成全绿（当前 420/420 基线；以最新台账/HEAD 为准）；L1 证据必跑；至少一份 L2/hermetic 真实 Gitea 证据归档；SKIP 不计 COVERED；
5. 独立 Gate（需求/代码/UX）签字：报告文件 + 结论 + 日期 + commit；M2 = M2a ∧ M2b；D-025 §4 的 M2b 出口按 D-028 增补 AC-OP-34/35（ADR 记录，不降低原 AC）。
# Annex A — 验收标准目录（AC Catalog，normative）

> 本附录由原 AC Catalog 合并而来，含 AC-OP-01…35、AC-REPO-01…11、AC-SAFE、AC-UI 等逐条 Given/When/Then；与正文冲突时以正文中的安全/范围硬约束为准。


| 项 | 内容 |
|---|---|
| 文档 ID | MBD-GIT-AC |
| 版本 | **v0.2.1（AC 全量，待冻结；按 07 独立冻结复评回写 SRS v0.2.1 口径）** |
| 日期 | 2026-11-14 |
| 依据 | 本文件 SRS 正文（v0.4-draft 合并稿）（本版按 `历史冻结评审（已删除）` 回写：`BINDING_ERROR` 严重度按 `binding_cause` 拆分 / SYNC 前置态 / 聚合顺序 / CI 契约 `componentId`+数字 `repoId` / 首启中间态 `UNKNOWN+incomplete=1` / §8.1 补列与枚举；14 主状态 §7.2 / 11 flags §7.3 / 15 聚合态 §7.4 / 状态×操作 §7.6 / 16 表 §8.1 / §8.3 契约↔DDL 映射 / FR §9.1–9.11 / NFR §10 / 色板 §11.2 / CI 锚点 §11.4 / 旧 AC §13.1（仅映射） / 测量方法 §13.3 / 附录 C 夹具 / 附录 D 设置与错误码） |
| 强制输入 | `历史红队评审（已删除）` 第 93–115 行（逐 AC 可测试性问题）— 本目录 §1.5 逐条给出处置；`历史 UX/CI 评审（已删除）` §5.4 CI“不返工”验收 10 条；`历史架构评审（已删除）` §H 测试资产 |
| 文档性质 | **本文件正文的验收附录**（已合并，不再是独立派生文件）；与正文冲突时以正文安全/范围条款为准 |
| 边界 | 只写“可执行的验收断言”，**不做实现设计**：不选库、不写代码、不定进程/IPC/DDL 物理实现；SRS 已冻结的枚举/命名/表名/错误码/状态名/文案一律照抄 |
| 规模 | 覆盖 **104 条 FR（9.1–9.11 全部）**，共 **188 条 AC**（见 §3 末尾统计；FR 优先级 P0=46 / P1=22 / P2=4）；其中 P0 FR 均含成功路径 + 关键失败/异常路径；P2 功能的 v1.1 完整行为单列 §2.12（不冻结、不计入 188 条） |
| 约定 | 与 SRS 不一致处一律显式标 **〔SRS 需修订 R-xx〕**（R 清单与状态见 §4.2），不自行发明枚举/命名；R 清单逐条标“已关闭（SRS v0.2.1 位置）/ 如再开放须 ADR” |

---

## 1. 使用说明与 AC 编号规则

### 1.1 使用说明

1. **本目录是 M0 冻结的验收基线**：M0 出口要求“每条 FR ≥1 条 AC、AC 可自动化判定、追溯矩阵 100% 覆盖”（SRS §3.3、§13.4、§10.6）。
2. 每条 AC 的 **Then 必须落在可程序读取的断言源**上，只允许以下 6 类：① SQLite 查询结果（表名/字段名严格按 SRS §8.1）；② Host API/IPC 响应或事件（§12.2 消息契约仅作观测载体，不作验收对象）；③ UI DOM 属性（`data-testid`/`data-state`/`aria-*`/可见文案，文案严格按 §11.2）；④ 日志与错误码（附录 D，前缀与码值照抄）；⑤ 文件系统状态（路径、文件存在性、mtime、`.trash` 目录结构）；⑥ git 命令输出（`git rev-parse`/`for-each-ref`/`status --porcelain=v2`/`cat-file` 等）。
3. **禁止词**（出现即视为该 AC 未通过冻结评审）：正常、流畅、合理、友好、炫酷、丝滑、秒级、实时、尽量、适当、大致、良好、稳定（作为形容词）、性能高、快速（无数字）。
4. 所有时间断言必须用 `IClock` 假时钟或 SRS §13.3 的测量方法（DevTools Performance / BenchmarkDotNet / `sync_run` 计时 / 进程计数），不得用 `sleep` 墙钟。
5. 每条 AC 的 Given 必须引用**夹具 ID**：附录 C `F-*`（本地 git 夹具，由测试按附录 C 在临时目录构造）或本目录 §1.6 定义的 `G-WIRE`/`G-E2E`/`C-SYNTH`/`CLK`。
6. 测试层级四选一：**单测 / 集成 / E2E / UI 快照 / 性能**（可叠加，如“集成+UI 快照”）。
7. M0 冻结后 **只允许“只增不改语义”**：新增 AC 用同域下一个未占用序号；修改已冻结 AC 的 Then 必须走 SRS 变更记录 + ADR（见 §5.4 变更规则）。

### 1.2 AC 编号规则

```
AC-<域>-<NN>[.<子字母>]
```

- `<域>` 取值固定 12 个（含 UI）：`CFG` / `SYNC` / `SCAN` / `STA` / `BOARD` / `DETAIL` / `OP` / `REPO` / `SAFE` / `OPS` / `CI` / `UI`。
- `<NN>` 为**域内两位流水号**（01–99），按首次冻结时分配；**与 FR 编号无关**，因为一条 AC 可覆盖多条 FR（如状态矩阵同时覆盖 FR-STA-01/02）。
- `<子字母>` 仅用于同一 AC 下必须分别判定、且共享同一 Given 的原子场景（如 `.a`/`.b`），子项单独计入 AC 总数统计；**本版 188 条冻结 AC（174 个冻结 `#### AC-*` + §13.1.1 表内 14 条 UI；另有 2 条 v1.1 不计）；AC-UI-10a/10b/10c 使用子字母**（留给 M1/M2 只增不改时拆分子场景）。
- 每条 AC 必须显式写 **覆盖 FR**；一个 FR 可被多条 AC 覆盖；**禁止**出现无 FR 归属的 AC、禁止跨域编号、禁止复用已作废编号（作废保留占位并标 `[作废]`）。
- 旧编号 `AC-01…AC-27` 不再使用，仅保留映射（§1.4）。
- **v1.1 草案 AC**：`AC-<域>-<NN>(v1.1)` 使用域内下一个未占用序号（当前 `AC-CFG-12(v1.1)`、`AC-DETAIL-10(v1.1)`），**不复用冻结主编号、不计入 188 条冻结基线统计**，M0/M1/M2 不验收；v1.1 立项时转为正式编号。

### 1.3 单条 AC 模板（固定 6 字段）

```md
#### AC-<域>-<NN> · <一句话标题>
- **覆盖 FR**：<FR-ID（可多条）>
- **测试层级/依赖**：<层级>（<Fake Gitea / WireMock / 真 Gitea / 本地 git 夹具 / IClock / BenchmarkDotNet>）
- **Given**：<前置数据夹具 ID / 环境 / 版本化数据集>
- **When**：<精确操作：命令 / 点击 / 注入事件，含参数>
- **Then**：<可观测断言：DB 字段 / API 响应 / UI 元素与文案 / 日志错误码 / 文件系统状态>
- **关联 NFR/OQ**：<NFR-ID / OQ-ID，无则写 —>
```

字段纪律：
- **Given** 必须能由脚本一键盘起（测试夹具 + `G-WIRE-REST`/`G-WIRE-GIT` + `IClock` 固定时刻）；不得出现“用户手工准备”的数据。
- **When** 必须写清入口（页面/菜单/API）+ 参数值；批量操作写清选中集合与期望 `job` 行。
- **Then** 逐条断言用分号分隔，每条断言必须含“期望值”；涉及列表断言必须给**行数/顺序/键集**。
- **关联 NFR/OQ** 只填 SRS 已存在 ID；OQ 未决时在 §4.1 给出本 AC 采用的暂定口径。

### 1.4 旧 AC-01…AC-27 → 新 AC 映射表（旧编号已作废，仅映射）

> **旧编号已作废，本表仅作映射**：旧 AC 仅为节选（红队原文按当时 65 条 FR 计，直接覆盖约 20 条），映射后旧编号不再引用；同一旧 AC 被拆到多条新 AC。SRS v0.2.1 §3.3/§13.4 已改用本目录新编号（`AC-<域>-<NN>`），旧号仅存于 SRS §13.1 历史映射表；若其他资产仍出现 `AC-01…AC-27`，一律按本表 + §3 追溯矩阵解析为现行编号。M0/M1/M2/M3 出口只允许引用现行编号。本目录正文出现的“旧 AC-xx”“红队 06 AC-xx”均为历史引用，不解析为现行 AC ID。

| 旧 AC | 旧标题 | 新 AC（主） | 补充/拆分 |
|---|---|---|---|
| AC-01 | 组件识别 | AC-SYNC-01 | AC-SCAN-01、AC-SYNC-14、AC-BOARD-01 |
| AC-02 | 缺仓 | AC-SYNC-14 | AC-BOARD-06（筛选“缺仓”） |
| AC-03 | 未绑定 | AC-SYNC-13 | AC-BOARD-13、AC-SCAN-01 |
| AC-04 | 状态矩阵 | AC-STA-01、AC-STA-02 | AC-STA-03、AC-STA-14 |
| AC-05 | 无假绿 | AC-STA-04、AC-STA-09 | AC-STA-08、AC-STA-14、AC-BOARD-12 |
| AC-06 | 关系判定 | AC-STA-05 | AC-STA-06、AC-SCAN-11 |
| AC-07 | SYNC 保护 | AC-OP-15、AC-OP-16 | AC-OP-17、AC-OP-18、AC-SAFE-01/02 |
| AC-08 | 删除可恢复 | AC-OP-20、AC-OP-21 | AC-OP-22、AC-SAFE-04 |
| AC-09 | 部分失败 | AC-OP-03 | AC-OP-04、AC-OP-08、AC-OP-26 |
| AC-10 | 切 Tag | AC-OP-11、AC-OP-12 | AC-OP-13、AC-OP-14、AC-STA-11 |
| AC-11 | 首启三阶段 | AC-OPS-15 | AC-OPS-16 |
| AC-12 | CI 预留 | AC-CI-01…AC-CI-11 | — |
| AC-13 | 性能 | AC-BOARD-03、AC-BOARD-05、AC-BOARD-14 | AC-SCAN-08、AC-SCAN-09、AC-SYNC-12、AC-DETAIL-04 |
| AC-14 | 安全 | AC-SAFE-13、AC-SAFE-12 | AC-SAFE-05/06/07、AC-CFG-06/07 |
| AC-15 | 单实例 | AC-OPS-09 | AC-OPS-10 |
| AC-16 | 状态×操作矩阵 | AC-STA-15 | AC-OP-02、AC-OP-17 |
| AC-17 | CREATE 分支 | AC-OP-24、AC-OP-25 | — |
| AC-18 | 修复类操作 | AC-REPO-08/09/10/11 | AC-OP-05 |
| AC-19 | 恢复 | AC-OP-30 | AC-OP-20、AC-OP-16 |
| AC-20 | 纳入/移出 | AC-OP-31、AC-OP-32 | — |
| AC-21 | 远程未就绪 | AC-OPS-16 | AC-OPS-15、AC-STA-04 |
| AC-22 | 搜索/筛选/排序 | AC-BOARD-04、AC-BOARD-06、AC-BOARD-07 | AC-BOARD-05 |
| AC-23 | 未绑定入口 | AC-BOARD-13 | AC-SYNC-13 |
| AC-24 | 通知/设置/日志 | AC-OPS-01…AC-OPS-08、AC-OPS-17 | AC-OPS-03…AC-OPS-06 |
| AC-25 | 权限/认证过期 | AC-CFG-08 | AC-BOARD-08、AC-SYNC-03 |
| AC-26 | 外部 git 并发 | AC-OP-29 | AC-SCAN-06 |
| AC-27 | 崩溃恢复 | AC-SAFE-08、AC-SAFE-09 | AC-OP-26、AC-OPS-12 |

### 1.5 红队 06 §93–115「可测试性问题」逐条处置索引

> 红队判定 ❌ = 原 AC 不可判定；⚠ = 条件可判定；✅ = 可判定。本表证明每个不可判定项已在 §2 落地为可执行断言。表中 `AC-01…AC-13` 为 **06 红队条目编号**（非本目录现行 ID），映射见 §1.4。

| 红队条目 | 不可判定原因（06 原文要点） | 本目录处置 |
|---|---|---|
| AC-01 组件识别 ✅ | 未定义“同步完成”判据与夹具组织 | **AC-SYNC-01**：`sync_run.coverage=1.0` 且 `repo` 计数=4,000 作为“同步完成”门控；`G-WIRE` 80 页夹具 + `component_id` 精确断言（不出现 `CF325F`） |
| AC-02 缺仓 ⚠ | OQ-03 未决；“提示”形式/位置未定义；批量作用范围未列 | **AC-SYNC-14**：组件 `aggregate_status=BINDING_ERROR`、缺失仓 chip 文案“缺失”、`job.target_json` 只含 3 个存在仓；OQ-03 暂定口径见 §4.1 |
| AC-03 未绑定 ⚠ | `_SIL_Design` 例外/大小写冲突/空仓未覆盖；入口未定义 | **AC-SYNC-13**（`_SIL_Design`/`Y_Design2`/`X_Docs` 全进 `unbound_repo`）；**AC-BOARD-13**（入口=P1 看板“未绑定仓库”抽屉） |
| AC-04 状态矩阵 ❌ | 无夹具清单/预期映射；未覆盖 flags 与 15 聚合态；矩阵非交付物 | **AC-STA-01/02/03**：以附录 C 25 个 `F-*` 为可执行清单，测试内构造的状态夹具 表驱动 `primary`/`aggregate`/`flags_json` 三断言；property-based 随机提交图断言“恰好 1 主状态 + 必然落 15 态之一 + 未命中落 UNKNOWN” |
| AC-05 无假绿 ❌ | “超 TTL”无值；STALE 非组件态；BINDING_ERROR 遮蔽 UNKNOWN 未裁决；时间格式未定义 | **AC-STA-09**（TTL=90s/1200s/25200s，来源 §7.1）；**AC-STA-04**（任一仓 UNKNOWN ⇒ 组件非 SYNCED）；**AC-STA-08**（时间格式=本地化 `数据 X 前`，源字段 `local_state.last_success_at`/`remote_ref.observed_at`）；遮蔽裁决见 AC-STA-04（R-15 已由 SRS v0.2 §7.4 关闭） |
| AC-06 关系判定 ✅ | 未写明“与 fetch+merge-base 一致”的基准方法 | **AC-STA-05**：先 `fetch` 再对 `compare_ref` 跑 `merge-base --is-ancestor` 的 0/1/>1 三退出码对拍；“未推送不调用 API”由 **AC-STA-06** 用 `G-WIRE` 请求记录断言 |
| AC-07 SYNC 保护 ⚠ | “丢失清单”字段未定义；恢复无 FR/入口；备份/stash 顺序未写 | **AC-OP-16**：预检表行字段 `未推送 commit 列表`/`untracked 清单`/`stash 条数`/`影响仓数` 逐字段断言；恢复入口=任务中心 `refs/backup/sync/<ts>/<branch>`（**AC-OP-30**）；顺序=fetch→backup ref→stash→reset，且外部改动时失败且零改动（**AC-OP-15**） |
| AC-08 删除可恢复 ⚠ | `.trash`/回收站二义；保留期冲突；恢复入口未定义；远端零变更无检查方法 | 统一 `.trash`+30 天（D-017）；**AC-OP-20** 断言 `.trash/<ts>/<full_name>` 目录；**AC-OP-30** 恢复；**AC-SAFE-04** 用 Mock 断言 Gitea 请求记录**无任何写方法** |
| AC-09 部分失败 ✅ | OP_PARTIAL_FAILED 清除条件/入口未定义；重试失败项契约未写 | **AC-OP-03**：`job.succeeded=2/failed=2` → `aggregate_status=OP_PARTIAL_FAILED`；清除需“确认清除”动作并写 `operation_log`；**AC-OP-26** 断言“仅重试失败项”生成的 `job.target_json` 仅含 2 个失败 `repo_id` |
| AC-10 切 Tag ❌ | TAG_DRIFT 依赖未定义基线模式；7.4 无 DETACHED 映射；“1 仓报告缺失”歧义 | 基线模式已作废（D-010）→ **AC-STA-11** 断言“无条件计算”；**AC-OP-12** 明确夹具口径：4 仓均存在、第 4 仓 `main_status=SYNCED` 且无 v1.0 tag → 期望 `TAG_DRIFT`；**AC-OP-14** 断言 `tag_present=0` 行 |
| AC-11 首启 ❌ | 双阶段 vs 三阶段；阶段名/total 来源未定义；跳过降级后看板态未定义 | 统一三阶段（D-016）；**AC-OPS-15** 断言 `progress` 事件序列 `LOCAL_DISCOVERY→REMOTE_RECONCILE→STATUS_CALC` + 跳过远程后看板为只读降级 + 数据截至时间；**AC-OPS-16** 断言每阶段 `{stage,total,current,cancellable}` |
| AC-12 CI 预留 ⚠ | 5 条验收未编号；契约与 DDL 不一致；无 CI 新鲜度锚点 | **AC-CI-01…AC-CI-11**（含 §11.4 的 5 条 + 04 §5.4 的 10 条映射）；新鲜度=**AC-CI-11**；DDL 对齐=**AC-CI-09**（已按 SRS v0.2 契约回写，R-16 关闭） |
| AC-13 性能 ❌ | 无测量方法/工具/门禁；4,000 组件口径与 NFR 冲突 | 全部引用 §13.3：**AC-BOARD-14**（首屏 LCP≤1.5s、冷启动≤5s，两档：正常 1,000 组件/4,000 仓、压测 4,000 组件/16,000 仓）；**AC-BOARD-03**（滚动 10s 采帧 ≥58fps、单帧 ≤33ms、DOM≤120/≤15,000）；**AC-BOARD-05**（防抖 120ms、p95≤300ms）；固定 8C/16G/SSD/Win11 并记录杀软/磁盘型号 |
| AC-14 安全 ❌ | “全量”范围未定义；既有仓 `.git/config` token 不可能清零；诊断包/转储未纳入 | **AC-SAFE-13**（header-only：WireMock 断言仅在 `Authorization: token …` 头、命令行/remote URL 无凭据）；**AC-SAFE-12**（既有仓内嵌 token → `SEC-E001` 告警 + UI 脱敏展示 + **不自动改**）；**AC-SAFE-06/07**（诊断包/转储脱敏）；扫描规则=**AC-SAFE-05** 五类正则样本 |
| AC-15 单实例 ✅ | 崩溃后 stale lock 未覆盖 | **AC-OPS-09**（双开）+ **AC-OPS-10**（PID/时间/进程存活三条件自愈） |

> 06 第 115 行“其他可测试性缺陷”处置：①“kill -9”改为 **Windows 强制终止进程**（验收只断言重启后不猜测成功；具体终止机制不属验收，见 AC-SAFE-09）；②视觉回归“≥12 张”改为 **AC-BOARD-15** 的“6 仓状态 × 6 CI 状态关键组合 ≥12 张 + 15 聚合态全覆盖”；③深色主题=**AC-BOARD-15** 的 `prefers-color-scheme: dark` 快照；④性能测试机=§13.3 写死并在报告注明杀软/磁盘；⑤Fake Gitea 选型冻结为 **`G-WIRE`（WireMock.NET 契约）+ `G-E2E`（真 Gitea 单二进制）** 双资产，见 §1.6。

### 1.6 测试依赖代号（本目录定义，映射 SRS §10.6/§13.2 与 03 §H）

| 代号 | 资产 | 说明 |
|---|---|---|
| `G-WIRE`（总称） | REST/GIT 双资产 | 引用时必须写子资产：`G-WIRE-REST`=REST 契约桩；`G-WIRE-GIT`=git smart-HTTP 代理 |\n| `G-WIRE-REST` | WireMock.NET REST 契约桩 | 回放 Gitea REST：分页（`Link`/`x-total-count`）、401/403/404/429/5xx、超时、慢响应；记录方法/URL/status（不记录 Authorization 值） |\n| `G-WIRE-GIT` | 本地 HTTP 代理 | 覆盖 git smart-HTTP（GET/POST）；记录 `{method,url,status}`（POST 也记录）；用于“远端零写/无 force push”断言，不记录 Authorization 值 |
| `G-E2E` | Gitea 单二进制 fixture | `gitea.exe web` + 内置 SQLite；组织 `MBD`；两档数据：E2E-NORMAL（=1,000 组件 / 4,000 仓）与 E2E-STRESS（=4,000 组件 / 16,000 仓）；可打 Tag/推 commit/删仓/改名 |
| `F-*` | 本地 git 夹具（附录 C） | 由测试按附录 C 在临时目录构造；期望 `primary`/`aggregate`/`flags_json` 由测试断言；覆盖 14 主状态 + 11 flags + 15 聚合态 |
| `C-SYNTH` | 合成数据集生成器 | 版本化生成 4,000 组件/16,000 仓元数据 + 400 本地仓（§13.3），每次测量报告生成器版本 |
| `CLK` | `IClock` 假时钟 | 可手动推进；用于 15s/10min/TTL/退避曲线断言 |
| `BENCH` | BenchmarkDotNet 夜间基线门禁 | DB 批写、扫描、聚合纯函数；门禁写入 CI |

环境基线（所有 E2E/性能 AC 共用）：Windows 11 x64、8C/16G/SSD、1440×900/100%、WebView2 Evergreen、.NET 10、Git for Windows ≥2.40、DB 在 `%LOCALAPPDATA%\MbdGitTool\`。

### 1.7 引用与标注约定

- 枚举/状态名/表名/字段名/错误码/设置键/UI 文案：**照抄 SRS**（如 `DETACHED_TAG`、`repo_status.main_status`、`OP-E002`、`delete.retentionDays`、`远程仓库不受影响`）。
- 凡 SRS 未定义而验收必须用到的取值（如 `job_item.state` 取值集），本目录暂用括注名并标 **〔SRS 需修订 R-xx〕**；M0 冻结时以修订后的 SRS 为准同步替换。
- `〔SRS 需修订 R-xx〕` 汇总见 §4.2；未决产品口径（OQ）见 §4.1。
- **需求/方案分层（D-008）**：§12 为设计建议，**不进入验收对象**。AC 只断言可观测行为（DB 查询/UI DOM/FS/git 输出/日志错误码）；CAS 三元组的具体形式、`GIT_ASKPASS` 通道、Job Object 杀进程树等实现机制仅作说明，不写进 Then；实现替换不得改变 AC 语义。

---

## 2. 全量 AC 目录

> 每条 FR 至少 1 条 AC；P0 FR（SRS §9 优先级列）均覆盖成功 + 关键失败/异常路径。测试层级缩写：单测=UT、集成=IT、E2E、UI 快照=VIS、性能=PERF。

### 2.1 配置与认证（FR-CFG）

#### AC-CFG-01 · 向导三必填项的成功路径与字段级校验
- **覆盖 FR**：FR-CFG-01
- **测试层级/依赖**：IT + VIS（`G-WIRE`、`CLK`）
- **Given**：全新 `%LOCALAPPDATA%\MbdGitTool`（无 app.db/config.json）；`G-WIRE` 返回 `GET /api/v1/version` 与 `GET /api/v1/user` 的 200；`CLK=2026-11-14T00:00:00Z`。
- **When**：启动应用 → 向导第 1 步输入 `server.baseUrl=https://gitea.mbd.local`、`server.org=MBD`；第 2 步输入 PAT 并点“测试连接”；第 3 步选择 `workspace.root=D:\MBD\Workspace`，点“完成”。
- **Then**：① 每步“下一步/完成”仅在当前步校验通过后 `enabled`；② 输入 `http://gitea.mbd.local`（非 HTTPS）→ 字段下方 `data-error-code="CFG-E001"` 且“下一步”保持 `disabled`；③ 空 org → `CFG-E001` + 提示含“组织”；④ 完成时 `config.json` 与 `app_setting` 写入 `server.baseUrl`/`server.org`/`workspace.root` 三键；⑤ 无明文 token（见 AC-CFG-06）。
- **关联 NFR/OQ**：NFR-COMPAT-02/03；OQ-19（Win10 不计入）

#### AC-CFG-02 · 根目录专项校验：盘符根/用户目录根/系统目录/不存在目录/多根
- **覆盖 FR**：FR-CFG-01、FR-CFG-06（单根约束，D-013）
- **测试层级/依赖**：IT（本地 FS 夹具）
- **Given**：可写测试目录 `T=`（含中文/空格路径 `D:\MBD 测试\工作区`）、当前用户目录 `C:\Users\<me>`、系统目录 `C:\Windows`；`G-WIRE` 在线。
- **When**：向导第 3 步依次填写下列 5 个值并尝试“完成”：`D:\`、`C:\Users\<me>`、`C:\Windows`、`E:\NotExist`、`D:\MBD 测试\工作区`。
- **Then**：① 前 4 个均被拒：`CFG-E001` + 字段级文案（`data-error-code="CFG-E001"`），且 `app_setting.workspace.root` 未写入；② 第 5 个通过并写入；③ 再次打开设置页，`workspace.root` 控件为**单选**（`data-multi-root="false"`），UI 无“添加第二个根”入口（多根=v1.1，OQ-17 暂定口径）；④ 中文/空格路径可完成，`repo.local_path` 保留原始大小写与空格。
- **关联 NFR/OQ**：NFR-COMPAT-04；OQ-17

#### AC-CFG-03 · 向导可后退且不丢已填值
- **覆盖 FR**：FR-CFG-01
- **测试层级/依赖**：IT（`G-WIRE`）
- **Given**：向导处于第 3 步且前两步已通过校验。
- **When**：点“上一步”两次→在第 1 步修改 `server.org=MBD2`→点“下一步”两次回到第 3 步。
- **Then**：① 第 2 步 PAT 输入框为空（**不回填**任何凭据字符，`type=password` 且 value 为空）；② 第 3 步 `workspace.root` 保留原值；③ 步骤条 `data-step` 依次为 3→2→1→2→3；④ 第 1 步 org 已更新为 `MBD2` 且再测试连接时请求 URL 为 `/api/v1/orgs/MBD2/repos`。
- **关联 NFR/OQ**：NFR-COMPAT-02；—

#### AC-CFG-04 · “测试连接”成功：认证结果/组织可见仓数/权限范围
- **覆盖 FR**：FR-CFG-02
- **测试层级/依赖**：IT（`G-WIRE` 双模式：Mock 与真 Gitea `G-E2E`）
- **Given**：`G-WIRE` 预置：`GET /api/v1/user` → `{login:"dev01"}`；`GET /api/v1/orgs/MBD/repos?limit=50` → 200 且 `x-total-count=4000`；token scope=`read:repository, write:repository`。
- **When**：向导第 2 步点“测试连接”。
- **Then**：① 请求头含 `Authorization: token <PAT>`（`G-WIRE` 请求记录断言，大小写不敏感前缀）；② UI 展示三项且 `data-testid="conn-auth"` 文案含 `dev01`、`data-testid="conn-repo-count"`=`4000`、`data-testid="conn-scope"` 含 `read:repository` 与 `write:repository`；③ 组件状态 `data-state="success"`；④ 同一用例在 `G-E2E` 真实模式下得到相同三项（HTTP 200 + 组织计数一致）。
- **关联 NFR/OQ**：NFR-REL-01；OQ-15（配额不改变本 AC）

#### AC-CFG-05 · “测试连接”失败：401/403 与缺失权限清单
- **覆盖 FR**：FR-CFG-02、FR-CFG-04
- **测试层级/依赖**：IT（`G-WIRE`）
- **Given**：`G-WIRE` 两套 stub：① `GET /api/v1/user` → 401；② 200 但 token scope 仅 `read:user`（无 `read:repository`），`/orgs/MBD/repos` → 403。
- **When**：分别点“测试连接”。
- **Then**：① 401 场景：`data-error-code="E_AUTH_FAILED"`（`details.srsCode=NET-E001`），UI 含“重新生成最小 scope Token”入口；`app_setting` 不写认证状态；② 403 场景：`E_AUTH_FAILED`（`details.srsCode=NET-E001`）+ 缺失权限清单**逐项列出** `read:repository`、`write:repository`（缺失项数=2）；③ 两场景均**不**弹出成功态、不进入下一步；④ 错误码与文案分离：文案本地化字符串变化后错误码不变（`data-error-code` 仍 `E_AUTH_FAILED`）。
- **关联 NFR/OQ**：NFR-REL-02；—

#### AC-CFG-06 · Token 明文零泄漏（日志/配置/命令行/remote URL/诊断包）
- **覆盖 FR**：FR-CFG-03、FR-SAFE-03、FR-SAFE-06
- **测试层级/依赖**：IT + 安全扫描（`G-WIRE`、真 git 夹具）
- **Given**：全流程跑通 1 次配置 + 1 次 clone（token=`ghp_CANARY_9f8e7d6c5b4a`，唯一哨兵字符串）；开启 Verbose 日志。
- **When**：执行 AC-CFG-01、AC-OP-01 后，对下列产物做全量正则扫描（哨兵 + `token=`/`access_token=`/`Authorization` 值/userinfo）：`%LOCALAPPDATA%\MbdGitTool\**`（config.json/日志/审计/诊断包）、克隆仓 `.git/config`、进程命令行快照（`Get-CimInstance Win32_Process` 采集全部命令行）、网络抓包（仅 `G-WIRE` 记录的头）。
- **Then**：① `config.json` 无哨兵、无 `token` 明文字段，只含引用键与尾 4 位（`…b4a` 之外不出现完整值）；② 日志/审计/诊断包中哨兵出现次数=0（正则扫描输出空）；③ 克隆仓 `.git/config` 的 `[remote "origin"] url` 不含 userinfo 与 token；④ 进程命令行不含哨兵；⑤ 抓包记录中哨兵只出现在 `Authorization` 请求头值；进程 argv、落盘的环境变量/脚本、日志与 `.git/config` 中哨兵出现次数=0（凭据通道实现机制不属验收，见 D-008）。
- **关联 NFR/OQ**：NFR-COMPAT-07、SRS §10.3/D-006；—

#### AC-CFG-07 · 凭据存储契约：只存引用，可撤回
- **覆盖 FR**：FR-CFG-03
- **测试层级/依赖**：IT（Windows 凭据 API 读取器）
- **Given**：已完成 AC-CFG-01 配置，PAT=`ghp_CANARY_9f8e7d6c5b4a`。
- **When**：① 用 Windows Credential Manager 查询 target 形如 `MbdGitTool/Gitea/{host}/{user}` 的条目；② 在设置页点“退出登录/清除凭据”；③ 再次查看配置与凭据。
- **Then**：① 凭据条目存在且值为 PAT（仅测试进程可读）；② `config.json` 仅存引用（target 字符串）+ 尾 4 位；③ 清除后凭据条目不存在，`app_setting` 认证态=`signed_out`，后续请求返回 `SYNC-E001`（401）且 UI 出现重登入口。
- **关联 NFR/OQ**：D-006；—

#### AC-CFG-08 · 401 全局提示 + 重登后自动续跑 + 数据不清空
- **覆盖 FR**：FR-CFG-04
- **测试层级/依赖**：IT + E2E（`G-WIRE` 401 注入、`G-E2E`）
- **Given**：看板已加载 1,000 组件（`repo` 1,000 行，`component` 1,000 行）；`G-WIRE` 令全部 API 返回 401（`SYNC-E001`）。
- **When**：触发一次手动刷新（看板“刷新”按钮）→ 观察状态栏与看板 → 点“重新登录”并输入有效 PAT → 点“重试”。
- **Then**：① 全局横幅出现，`data-error-code="SYNC-E001"`，含“重新登录”按钮；② 看板数据行数不变（1,000 行）、不清空、不显示空态；③ 进行中的 job 的 `job.state` 保持 `RUNNING`（SRS §8.1 合法值；不得置 FAILED/CANCELLED，`job`/`job_item` 行不删除、进度不清零），任务中心该 job 显示“等待认证”且进度不前进（SRS v0.2.1 未新增暂停态或暂停原因列，故不引用不存在的枚举/字段）；④ 重登后同一 job 自动续跑并 `job.state` 终态为成功类，`repo`/`component` 行数与刷新前一致。
- **关联 NFR/OQ**：NFR-REL-01/02；—

#### AC-CFG-09 · 代理与自签证书连通；`sslVerify=false` 被拒
- **覆盖 FR**：FR-CFG-05
- **测试层级/依赖**：IT（`G-WIRE` + 自签证书本地 Gitea 端点）
- **Given**：内网自签 Gitea 端点 `https://gitea.lab.local`（自签 CA `lab-ca.pem`）；代理 `http://proxy.lab:8080` 可转发。
- **When**：① `proxy.mode=manual`、`proxy.url=http://proxy.lab:8080` → 测试连接；② `tls.extraCa=lab-ca.pem` → 测试连接；③ 尝试保存 `sslVerify=false` 或 `git config http.sslVerify=false`。
- **Then**：① 经代理请求成功（`G-WIRE` 记录 `Via`/代理地址），连接测试 200；② 配置 `tls.extraCa` 后 200；③ ③ 被拒：`CFG-E001`（或 `FS-E001` 若为文件问题）+ UI 提示“仅支持显式信任自定义 CA，不支持关闭校验”；④ 应用 `git config --global --get http.sslVerify` 前后均为未设置（工具不改系统级配置）。
- **关联 NFR/OQ**：SRS §12.6（禁止 sslVerify=false 兜底）、NFR-COMPAT-02；—

#### AC-CFG-10 · 配置热更新（组织/根目录变更）：v1 只断言入口不存在/不可用
- **覆盖 FR**：FR-CFG-06（P2，v1.1；v1 只冻结“入口不存在/不可用”，见 §4.1 OQ-24）
- **测试层级/依赖**：IT + VIS（`G-WIRE`、本地 FS 夹具）
- **Given**：已加载组织 `MBD`（1,000 组件）与根 `D:\MBD\Workspace`（100 个本地仓）。
- **When**：① 打开设置页的通用/工作区分区；② 尝试修改 `server.org` 或 `workspace.root`；③ 经 IPC 请求改写 `app_setting` 上述两键；④ 直接改 DB 后重启。
- **Then**：① v1 无“热更新/重同步/重扫”确认对话框与变更生效路径（DOM 无该弹层、IPC 无对应命令，不得出现死按钮）；② 设置页 `server.org`/`workspace.root` 控件为只读或不可见（`data-readonly="true"` 或不存在），并给出 tooltip“v1 仅支持首次配置”；③ 经 IPC 改写被拒（`CFG-E001`），`app_setting` 旧值不变；④ DB 直改后下次启动检测到配置漂移并提示重新配置，不得静默按新组织/新根重扫；⑤ 上述操作后 `component`/`repo` 行数不变、不产生 `sync_run`。
- **关联 NFR/OQ**：OQ-24（P2 版本归属）；完整行为见 §2.12 v1.1 AC（不冻结）

#### AC-CFG-11 · 向导门控：未通过“测试连接”不可完成配置
- **覆盖 FR**：FR-CFG-01、FR-CFG-02
- **测试层级/依赖**：IT（`G-WIRE`）
- **Given**：向导填写合法 BaseURL/org/PAT 但从未点“测试连接”（或测试连接返回 401）。
- **When**：直接点第 3 步“完成”。
- **Then**：① “完成”按钮 `disabled` 且 `aria-disabled=true`；② 提示“请先通过连接测试”（`data-testid="conn-required"`）；③ `app_setting` 未写入、不产生 `sync_run`、看板不加载；④ 补点测试连接成功后按钮 `enabled`，完成后进入首扫三阶段（见 AC-OPS-15）。
- **关联 NFR/OQ**：NFR-COMPAT-02；—

### 2.2 远程元数据同步（FR-SYNC）

#### AC-SYNC-01 · 4,000 仓清单分页拉全、覆盖率 100% 并在 UI 可见
- **覆盖 FR**：FR-SYNC-01、FR-SYNC-03、FR-SYNC-04
- **测试层级/依赖**：IT + E2E（`G-WIRE` 80 页夹具 / `G-E2E` E2E-NORMAL）
- **Given**：`G-WIRE` 提供 4,000 个仓库分页响应（每页 50，`x-total-count=4000`，`Link rel="next"` 链共 80 页），默认分支各不同（含 `main`/`develop`/`master` 混合）；`CLK` 固定。
- **When**：完成 AC-CFG-01 后触发首启同步（或点“立即同步”），等待同步终态。
- **Then**：① 请求页数=80、无重复页（`G-WIRE` 记录去重后 page=1..80）；② `repo` 表行数=4000，`repo_id` 无重复；③ `sync_run` 最近一行 `total=4000`、`ok=4000`、`failed=0`、`coverage=1.0`；④ 看板/状态栏显示“覆盖率 100%”且 `data-testid="sync-coverage"`=`100%`；⑤ 组件数=`component` 行数（按 D-001 规则计算，示例数据集 1,000）。
- **关联 NFR/OQ**：NFR-PERF-06、FR-SYNC-01；OQ-05

#### AC-SYNC-02 · 清单字段落库完整性与空仓标记
- **覆盖 FR**：FR-SYNC-01
- **测试层级/依赖**：IT（`G-WIRE` 单页夹具）
- **Given**：`G-WIRE` 返回 3 仓：`A_Design`（`empty=false`、`default_branch=main`、`archived=false`）、`A_Impl`（`empty=true`、`default_branch=develop`）、`A_Docs`（无生命周期后缀）。
- **When**：执行一次远程清单同步。
- **Then**：① `repo` 行含 `full_name`/`remote_url`/`default_branch`/`archived`/`is_local=0` 且 `repo_id` 等于 Gitea 数字 id；② `A_Impl` 的 `repo` 行标记为空仓（`empty` 语义落到 `local_state`/扫描后 `main_status=EMPTY`，依据 §7.2）；③ `A_Docs` 不进入 `repo`（不参与绑定）而进入 `unbound_repo`；④ 观察时间字段写入（`observed_at`）且非空。
- **关联 NFR/OQ**：FR-SYNC-01；—

#### AC-SYNC-03 · 清单部分失败：覆盖率 <100%、STALE 与失败清单
- **覆盖 FR**：FR-SYNC-01、FR-SYNC-04
- **测试层级/依赖**：IT（`G-WIRE` 第 3 页返回 500；第 4 页起 200）
- **Given**：4,000 仓分页夹具，注入第 3 页 500（重试后仍失败）。
- **When**：执行同步并在弹层查看失败清单。
- **Then**：① `sync_run` 行 `total=4000`、`ok<4000`、`failed>0`、`coverage<1.0`，`error` 含 `SYNC-E003`；② 看板顶部出现 `stale` 提示（`data-stale="true"`），文案含“数据截至 HH:mm”；③ 失败清单可展开，逐条显示页码/仓库/错误码；④ 其余成功页的 `repo` 行保留、不被清空；⑤ 依赖覆盖率的状态标记 STALE（见 AC-STA-09）。
- **关联 NFR/OQ**：SYNC-E003、NFR-REL-01；—

#### AC-SYNC-04 · “关注”范围 = 本地已绑定 ∪ 当前视图可见，不全量拉 4,000 仓分支/Tag
- **覆盖 FR**：FR-SYNC-02
- **测试层级/依赖**：IT（`G-WIRE` 记录请求集合）
- **Given**：4,000 仓清单已同步；本地已绑定 100 仓；当前看板视图可见 50 组件（200 仓）；其余 3,700 仓未绑定不可见。
- **When**：触发 L4 refs 同步（30min 周期或按需）。
- **Then**：① 分支/Tag 请求的 `repo_id` 集合 = 本地已绑定 100 仓 ∪ 可见 50 组件对应 200 仓（去重 ≤300）；② 未绑定且不可见的仓（≥3,400 个）**零请求**；③ `remote_ref` 行只覆盖上述集合；④ UI 覆盖率展示区分“清单覆盖率 100% / refs 覆盖率 n%”（两字段独立）；⑤ **失败路径**：令其中 20 仓 refs 请求返回 500 → refs 覆盖率=(280/300) 且失败仓清单可见，依赖这些仓的关系类状态标 `UNKNOWN`/`STALE`，其余 280 仓 refs 请求成功且 `remote_ref` 行数=280；不回退为“全量拉 4,000 仓”。
- **关联 NFR/OQ**：FR-SYNC-02、NFR-PERF-06；OQ-15

#### AC-SYNC-05 · tag→commit 与分支 head 落 `remote_ref`（含 annotated tag 解引用）
- **覆盖 FR**：FR-SYNC-02、FR-OP-04
- **测试层级/依赖**：IT（`G-E2E` 真 Gitea；annotated tag 夹具）
- **Given**：`G-E2E` 仓 `A_Design`：分支 `main` head=`c1`；轻量 tag `v1.0`→`c2`；annotated tag `v2.0`（tag 对象）指向 `c3`。
- **When**：同步该仓 refs。
- **Then**：① `remote_ref` 行：`(repo_id,'BRANCH','main').commit_hash=c1`；`(repo_id,'TAG','v1.0').commit_hash=c2`；`(repo_id,'TAG','v2.0').commit_hash=c3`（**解引用到 commit**，非 tag 对象 sha）；② 每行 `observed_at` 非空、`synced_at` 非空；③ `ref_type` 仅取 SRS v0.2.1 §8.1 CHECK 集 `BRANCH`/`TAG`（冻结，R-21 关闭）。
- **关联 NFR/OQ**：FR-SYNC-02、§9.7 FR-OP-04；R-21 已关闭（SRS v0.2.1 §8.1 `ref_type CHECK BRANCH/TAG`）

#### AC-SYNC-06 · 首启立即同步 + 三层周期编排（10min 水位 / 30min refs / 每日全量）
- **覆盖 FR**：FR-SYNC-03
- **测试层级/依赖**：IT（`G-WIRE`、`CLK`）
- **Given**：`CLK=2026-11-14T00:00:00Z`；已完成配置。
- **When**：启动应用（首启）→ 连续推进 `CLK`：+10min、+20min、+30min、+24h。
- **Then**：① 首启后立即产生 1 次清单同步（`sync_run` 行 +1，`kind` 取 SRS §8.1 `sync_run.kind`：ORG_LIST/REFS/FULL_RECONCILE）；② `CLK+10min` 触发清单水位同步（请求 `sort=updated&limit=50`，读到水位即停），不触发全量 refs；③ `CLK+30min` 触发变化仓 refs；④ `CLK+24h` 触发全量对账（`sync_run.total=4000`）；⑤ 各职责失败不阻塞其他职责（注入清单失败后 refs 仍运行）。
- **关联 NFR/OQ**：FR-SYNC-03、NFR-PERF-06；OQ-12

#### AC-SYNC-07 · 断网首启降级：退避重试 ≤2、恢复后自动续跑
- **覆盖 FR**：FR-SYNC-03
- **测试层级/依赖**：IT + E2E（`G-WIRE` 断网模拟 / 停 `G-E2E`）
- **Given**：首启时网络不可达（全部请求超时）。
- **When**：观察重试曲线与 UI → 恢复网络 → 点“重试同步”。
- **Then**：① 同一同步轮失败重试次数 ≤2（`G-WIRE` 记录尝试次数=3 含首次，或实现计数断言 ≤2 重试）；② UI 进入离线降级态：`data-degraded="offline"`、`NET-E001`、显示“数据截至 HH:mm”、本地扫描仍运行；③ 恢复后自动续跑，`sync_run.coverage=1.0`；④ 降级期间 `repo` 不被清空、组件不显示 SYNCED（见 AC-STA-04）。
- **关联 NFR/OQ**：NFR-REL-01、NET-E001；—

#### AC-SYNC-08 · `sync_run` 记录字段与覆盖率计算正确
- **覆盖 FR**：FR-SYNC-04
- **测试层级/依赖**：IT（`G-WIRE` 3 仓，1 仓 500）
- **Given**：清单 3 仓，第 2 仓详情请求 500（重试后失败）。
- **When**：完成同步。
- **Then**：① `sync_run` 行 `total=3`、`ok=2`、`failed=1`、`coverage=0.666…`（按 ok/total，保留定义）；② `started_at`/`finished_at` UTC ISO-8601、`duration_ms` ≥0（SRS v0.2.1 补列；旧 `duration` 字段不存在）；③ `error` 为空或含第 2 仓错误；④ 同一轮失败重试不新增 `sync_run` 行（仍为 1 行）。
- **关联 NFR/OQ**：SYNC-E003；—

#### AC-SYNC-09 · 远程改名 → `renamed_from` + 组件 ID 迁移事件（需用户确认，不静默删行）
- **覆盖 FR**：FR-SYNC-05
- **测试层级/依赖**：IT（`G-E2E` 改名 + `G-WIRE` 同名夹具）
- **Given**：`G-E2E` 已有组件 `A`（4 仓 `A_Design`…）；把 `A_Design` 改名为 `B_Design`（其余 3 仓仍 `A_*`）。
- **When**：执行一次清单同步。
- **Then**：① `repo` 行中该仓 `repo_id` 不变、`full_name=B_Design`、`renamed_from=A_Design`；② 生成组件 ID 迁移事件/待确认项，UI 列出旧 ID→新 ID（`data-testid="rename-event"`）；③ 用户点“暂不迁移”时 `component` 行保留旧 ID 且 `is_deleted=0`，无重复组件；④ 点“确认迁移”后 `component` 行数不变（旧 ID 不并存两个），`operation_log` 记迁移；⑤ 全过程无静默删行（前后 `component` 行数差=0）。
- **关联 NFR/OQ**：FR-SYNC-05；R-08 已关闭（SRS v0.2 §8.1 `repo.renamed_from` 已列）

#### AC-SYNC-10 · 远程删除 → `archived` 标记 + `remote_gone` flag + 禁 push
- **覆盖 FR**：FR-SYNC-05
- **测试层级/依赖**：IT + E2E（`G-E2E` 删仓，本地仍有克隆）
- **Given**：本地已克隆 `A_SIL`；在 `G-E2E` 删除该仓。
- **When**：执行一次清单同步 → 打开组件详情 → 尝试对该仓执行 Push。
- **Then**：① `repo.archived=1`（或删除态标记，按 §8.1 `archived`），`repo_status.flags_json` 含 `remote_gone`；② 组件详情该仓显示“远程仓库已删除”告警（`data-flag="remote_gone"`）；③ Push 被禁止并返回明确错误码（`E_ACTION_NOT_ALLOWED`，`details.srsCode=OP-E004`），仓库零改动（`git rev-parse HEAD` 前后一致）；④ 提示清理入口存在。
- **关联 NFR/OQ**：§7.3 `remote_gone`；—

#### AC-SYNC-11 · 增量策略：`updated_at` 水位为主；ETag 可用则用、不可用不依赖
- **覆盖 FR**：FR-SYNC-06
- **测试层级/依赖**：IT（`G-WIRE` 两套：有/无 ETag）
- **Given**：`G-WIRE-A` 支持 `ETag/If-None-Match`（304 响应）；`G-WIRE-B` 不返回 ETag。
- **When**：.a 对 A 连续同步两次（第二次数据未变）；.b 对 B 连续同步两次。
- **Then**：.a ① 第二次请求携带 `If-None-Match`；② 304 时 `repo` 行 `updated_at`/`observed_at` 按水位语义处理且不重复写 refs；③ `sync_run` 行 `coverage=1.0`、`failed=0`。  .b ④ 第二次请求不带条件头，仅按 `updated_at` 水位判断变化仓，**仍成功完成增量**（无 304 也不报错）；⑤ 两模式增量后的 `remote_ref` 结果一致。
- **关联 NFR/OQ**：FR-SYNC-06、§12.4；—

#### AC-SYNC-12 · 429/403/5xx 退避 + 抖动，并发上限默认 6，不雪崩
- **覆盖 FR**：FR-SYNC-06
- **测试层级/依赖**：IT（`G-WIRE` 限流注入、`CLK`）
- **Given**：`G-WIRE` 对前 20 个请求返回 429（带 `Retry-After: 2`），其余 200；`CLK` 可控。
- **When**：执行 80 页清单同步。
- **Then**：① 记录 429 后请求间隔按退避序列递增（1s→2s→4s…上限符合实现曲线，断言单调不减且含抖动：同轮重试间隔不完全相同）；② 并发请求峰值 ≤6（`G-WIRE` 记录并发数）；③ `sync_run` 最终 `coverage=1.0`，无请求风暴（总请求数 ≤ 基线请求数×2；基线=同数据集无 429 时的 80 页请求数）；④ 若 `Retry-After` 存在则等待 ≥2s（`CLK` 断言）。
- **关联 NFR/OQ**：SYNC-E002、NFR-PERF-06；OQ-15

#### AC-SYNC-13 · 未绑定仓库落库与原因分组（含 `_SIL_Design` 例外）
- **覆盖 FR**：FR-SYNC-05、SRS §6.1
- **测试层级/依赖**：IT（`G-WIRE` 夹具 6 仓）
- **Given**：远程存在：`X_Docs`（无生命周期后缀）、`Y_Design2`（非法后缀）、`X_SIL_Design`（前缀以生命周期词结尾）、`Z_Design` 与 `z_design`（大小写冲突）、`OK_Impl`（合法）。
- **When**：执行清单同步。
- **Then**：① `unbound_repo` 行数=5（`OK_Impl` 不进入），每条 `full_name` 与夹具一致；② `reason` ∈ `{DOCS,PATTERN,CASE_CONFLICT,UNKNOWN}`（SRS v0.2.1 §8.1 CHECK），逐仓断言 `X_Docs→DOCS`、`Y_Design2→PATTERN`、`X_SIL_Design→PATTERN`、`Z_Design`/`z_design→CASE_CONFLICT`；UI `data-testid="unbound-group"` 恰 3 组（DOCS/PATTERN/CASE_CONFLICT）；③ `X_SIL_Design` **不**被绑定为组件 `X_SIL`（`component` 中不存在该行）；④ 大小写冲突两条均列出且不静默丢弃；⑤ 看板组件数不受未绑定仓影响（只有 `OK_Impl` 所在组件可能缺仓→BINDING_ERROR）。
- **关联 NFR/OQ**：SRS §6.1；R-38 已关闭（SRS v0.2.1 §8.1 `unbound_repo.reason CHECK DOCS/PATTERN/CASE_CONFLICT/UNKNOWN`）

#### AC-SYNC-14 · 远程缺 1–3 仓：`BINDING_ERROR` + `binding_cause=INCOMPLETE`（severity=12）+ 缺失仓可见 + 批量只作用存在仓
- **覆盖 FR**：FR-SYNC-01、SRS §6.2/§7.4、FR-OP-01
- **测试层级/依赖**：IT + UI 快照（`G-WIRE`）
- **Given**：远程仅 `CF325F_ChanganSigProcgBascFct_{Design,Impl,MIL}` 三仓（无 SIL）；本地三仓已克隆；另设对照组件一组：缺 1 仓且其第 4 仓为 `INVALID`（`F-INVALID-REMOTE`）。
- **When**：同步后打开看板行与组件详情 → 选中该组件执行“批量 Fetch”；对照组件做同样操作。
- **Then**：① 仅缺仓组件：`aggregate_status=BINDING_ERROR`、`binding_cause=INCOMPLETE`、`severity_rank=12`（中性低：不进入异常优先最高档，见 AC-BOARD-01；可观测落点=`component.incomplete=1` 且无 INVALID 仓）；② 缺仓+INVALID 对照组件：`binding_cause=INVALID`、`severity_rank=1`（两组参数化断言，**不得写死单一 rank**；`binding_cause∈{INVALID,AMBIGUOUS}`（未绑定/解析歧义归 AMBIGUOUS）→1，`INCOMPLETE`→12；可观测落点=存在 `repo_status.main_status=INVALID` 的仓且 `component.incomplete` 不因缺仓置 1）；③ 卡片文案“绑定异常”+ 缺失仓 chip 显示“缺失”（`data-state="MISSING"`、`data-missing="1"`）；④ 批量 Fetch 的 `job.target_json` 仅含 3 个存在仓 `repo_id`，入队前提示“缺失 1 仓，仅对 3 仓执行”；⑤ 任务结果 3 行 `job_item`，无失败；组件仍可操作其余 3 仓（MISSING 仓不阻塞）。
- **关联 NFR/OQ**：D-015、OQ-20；C-02（SRS v0.2.1 拆分 `binding_cause` 严重度）

### 2.3 本地扫描与发现（FR-SCAN）

#### AC-SCAN-01 · 深度 1 平铺发现：识别/跳过结果与预期一致
- **覆盖 FR**：FR-SCAN-01
- **测试层级/依赖**：IT（本地 FS 夹具 `F-SYNCED` 等）
- **Given**：`workspace_root` 下构造：`A_Design/`（有效 git 仓，origin 匹配）、`A_Impl/`（有效仓）、`A_MIL/`（有效仓）、`A_SIL/`（有效仓）、`Docs/`（普通目录）、`B_Design/`（空目录）、`A_Design/sub_repo/`（嵌套仓，深度 2）、`.trash/20261113/A_Design/`（回收站）。
- **When**：执行 L0 本地发现（启动/手动刷新）。
- **Then**：① 识别为本地仓的条目=`A_Design/A_Impl/A_MIL/A_SIL` 4 条，`repo.is_local=1` 且 `local_path` 为 `<root>\<full_name>`；② 非仓目录 `Docs`/`B_Design` 跳过并写入 `scan_issue`（`issue_type=NON_REPO`）；③ 深度 2 的 `sub_repo` **不**被发现（非递归）；④ `.trash` 下目录**不**被发现（D-017）；⑤ 扫描不产生任何仓库写入（见 AC-SCAN-05）。
- **关联 NFR/OQ**：D-013；—

#### AC-SCAN-02 · 有效仓判定：`rev-parse --show-toplevel` + origin 规范化匹配
- **覆盖 FR**：FR-SCAN-01
- **测试层级/依赖**：IT（`F-INVALID-REMOTE`、`F-INVALID-NOTREPO`、`G-WIRE`）
- **Given**：`C_Design/`（是 git 仓但 origin=`https://other.host/other/C_Design.git`）、`D_Design/`（是 git 仓但 origin 指向同名文档仓 `D_Docs`）、`E_Design/`（有效）。
- **When**：执行发现 + 单仓深扫。
- **Then**：① `C_Design`/`D_Design` 判 `INVALID`，`scan_issue.issue_type=REMOTE_MISMATCH`，`scan_issue.path` 指向目录；② 有效仓 `E_Design` 判 `SYNCED`（`F-SYNCED` 语义）；③ `INVALID` 仓在组件聚合中计入 `BINDING_ERROR`（见 AC-STA-02），不被静默排除；④ `git -C <path> remote get-url origin` 使用参数数组执行（进程命令行不含 shell 拼接；测试断言 argv 为 `git`、`-C`、`<path>`、`remote`、`get-url`、`origin`）。
- **关联 NFR/OQ**：SRS §6.3；R-22 已关闭（SRS v0.2 附录 D `SCAN-E001` 已定义，本 AC 断言该码）

#### AC-SCAN-03 · `scan_issue` 七类问题落库与告警
- **覆盖 FR**：FR-SCAN-01、FR-SCAN-06
- **测试层级/依赖**：IT（本地 FS 夹具：非仓/网络盘/长路径/大小写冲突/junction）
- **Given**：`workspace_root` 下构造 7 类问题各 1：`NON_REPO`（普通目录）、`REMOTE_MISMATCH`（见 AC-SCAN-02）、`NETWORK_DRIVE`（映射为 UNC 的目录）、`REPARSE`（junction 指向外部）、`SAFE_DIRECTORY`（属主不同触发 git 拒绝）、`LONG_PATH`（>260 字符路径）、`CASE_CONFLICT`（`X_Design` 与 `x_design` 并存）。
- **When**：执行 L0+L2 扫描。
- **Then**：① `scan_issue` 行数=7，`issue_type` 覆盖全部 7 个枚举值且与目录一一对应；② `detail` 非空；③ UI 异常清单可按 `issue_type` 分组、每类计数正确；④ 长路径/大小写冲突/网络盘/reparse 均给出“仅只读/禁止写”提示（见 AC-SCAN-12）；⑤ 扫描不因单个问题中断（其余有效仓 `is_local=1` 且 `main_status` 与 测试内构造的状态夹具 一致）。
- **关联 NFR/OQ**：NFR-COMPAT-04、SCAN-E002；R-10 已关闭（SRS v0.2.1 §6.3 统一“不纳管+`scan_issue` 告警；已纳管后不可写→只读展示”）

#### AC-SCAN-04 · 单仓采集字段全覆盖（附录 C 表驱动）
- **覆盖 FR**：FR-SCAN-02
- **测试层级/依赖**：IT（附录 C 全部 `F-*` 夹具，测试内构造的状态夹具）
- **Given**：测试按附录 C 构造的 **25 个 `F-*` 夹具**（覆盖 14 主状态 + flags + `F-MISSING+BLOCKED` 组合，N-01）。
- **When**：对每个夹具执行一次 L2 深扫。
- **Then**：逐夹具断言 `local_state` 字段：① `head_type`/`head_ref`/`head_sha` 与 测试内构造的状态夹具 一致（分支/tag/游离/空仓四形态）；② `upstream_ref`/`ahead`/`behind` 一致；③ `dirty_count`/`untracked_count` 一致；④ `op_in_progress`（MERGE_HEAD/rebase-merge/rebase-apply/CHERRY_PICK_HEAD/BISECT_LOG）一致；⑤ `fingerprint`/`scan_at`/`last_success_at` 非空；⑥ flags（`shallow`/`lfs_missing`/`stashed`/`busy`）与 测试内构造的状态夹具 的 `flags_json` 相等。**夹具缺失或期望缺失视为未通过**。
- **失败路径补充（同 AC）**：⑦ 对某仓注入 `git` 命令失败（模拟 git 不可用/对象损坏）→ `local_state.scan_error` 写入非空错误码（`GIT-*` 前缀）、`last_success_at` **不**前进、`repo_status.error_code` 非空且主状态按 §7.2 优先级 0 为 `UNKNOWN`；其余仓不受影响（逐仓隔离），失败仓不得沿用旧状态冒充新鲜。
- **关联 NFR/OQ**：FR-SCAN-02、附录 C；R-20 已关闭（SRS v0.2 §8.1 `local_state.head_type` CHECK：BRANCH/DETACHED_TAG/DETACHED_COMMIT/UNBORN）

#### AC-SCAN-05 · 只读扫描零写入：仓库前后状态一致、索引不被触碰
- **覆盖 FR**：FR-SCAN-03
- **测试层级/依赖**：IT（本地 git 夹具、进程监控）
- **Given**：`F-DIRTY`、`F-UNMERGED` 两仓；预采集：`git status --porcelain=v2`、`.git/index` mtime/size、`.git/HEAD` mtime、`git for-each-ref` 输出。
- **When**：执行 L1 指纹 + L2 深扫（含 FileSystemWatcher 触发）。
- **Then**：① 扫描后 `git status --porcelain=v2` 输出与扫描前**逐字节相同**；② `.git/index` mtime/size 不变（未被刷新）；③ `git for-each-ref` 前后相同；④ 扫描进程 argv 含 `--no-optional-locks`（或等价 `GIT_OPTIONAL_LOCKS=0`），且未触发 `gc`/`maintenance`（无 `gc.log`/`gc.pid` 产生）；⑤ 仓库内无新增/修改文件。
- **关联 NFR/OQ**：SRS §7.2/§9.3；—

#### AC-SCAN-06 · `.git/index.lock` 存在 → `busy` flag + BUSY 错误码，不删锁不重试
- **覆盖 FR**：FR-SCAN-03、FR-OP-10
- **测试层级/依赖**：IT（`F-BUSY` 夹具）
- **Given**：`F-BUSY` 仓存在外部持有的 `.git/index.lock`（测试进程创建并保持打开），仓可被只读 git 命令访问。
- **When**：① 执行 L2 扫描；② 对同一仓发起 Fetch 与 Commit。
- **Then**：① 扫描完成且 `repo_status.flags_json` 含 `busy`，锁文件仍存在（测试清理前断言 `Test-Path`=True）；② Fetch/Commit 返回 `OP-E001`、`job_item.state=FAILED`（SRS §8.1 合法值）、仓库零改动；③ 工具**不删除**该锁（前后文件 hash 一致）；④ UI chip 显示“外部 Git 正在操作”（`data-flag="busy"`）；⑤ 不自动重试（`job_item.attempt=1`）。
- **关联 NFR/OQ**：OP-E001、NFR-COMPAT-07；—

#### AC-SCAN-07 · 分层调度：L0/L1/L2 频率与“L1 不起 git 进程”
- **覆盖 FR**：FR-SCAN-04
- **测试层级/依赖**：IT（`CLK`、进程启动计数）
- **Given**：400 个本地仓夹具（`F-SYNCED` 批量生成）；`CLK` 固定；埋点统计 `git.exe` 进程启动次数。
- **When**：推进 `CLK` 15s×4、10min、6h。
- **Then**：① L1 每 15s 一轮，每轮只 `stat` 6 个文件（`.git/HEAD`、`index`、`refs`、`packed-refs`、`logs/HEAD`、`FETCH_HEAD`）——断言 `git.exe` 启动次数=0；② L0 每 10min 触发一次目录发现；③ L2 仅对指纹变化仓 + 热仓启动 git 进程（进程数 ≤ 变化仓数×1，8 路并发上限且并发峰值 ≤8）；④ 无任何“全量 30s git 轮询”（15s 时刻点 git 进程数=0）；⑤ `local_state.fingerprint`/`last_success_at` 每轮更新；⑥ **失败路径**：令 2 个变化仓的 L2 深扫失败 → 这两仓 `scan_error` 写入、状态 `UNKNOWN`，其余 398 仓照常完成（不因单仓失败中断整轮），下一轮仍会重试这 2 仓（失败仓不被永久跳过）。
- **关联 NFR/OQ**：FR-SCAN-04、NFR-PERF-05；—

#### AC-SCAN-08 · L1 指纹性能：400 仓 <300ms（进程数=0）
- **覆盖 FR**：FR-SCAN-04
- **测试层级/依赖**：PERF（400 仓夹具、BenchmarkDotNet/Stopwatch）
- **Given**：400 个本地仓（`F-SYNCED` 批量生成，SSD、杀软状态记录在报告）；预热 3 轮。
- **When**：执行 10 轮 L1 指纹轮询取 P95。
- **Then**：① 单轮 P95 <300ms；② 每轮 `git.exe` 启动计数=0；③ 报告记录测试机规格、杀软、磁盘型号（§13.3）；④ 结果入 `BENCH` 夜间基线，超预算即门禁失败。
- **关联 NFR/OQ**：NFR-PERF-05；—

#### AC-SCAN-09 · L2 时效：本地/IDE 变更 ≤5s、外部 commit/checkout/fetch ≤10s
- **覆盖 FR**：FR-SCAN-04、FR-STA-05
- **测试层级/依赖**：IT（`CLK` + 真实 git 操作）
- **Given**：热仓 `A_Design`（当前详情页打开）；`CLK` 按真实时间推进（用于触发 5s/10s 窗口）。
- **When**：① 外部编辑一个 tracked 文件并保存；② 外部执行 `git commit`；③ 外部执行 `git checkout -b feat`；④ 外部执行 `git fetch`。
- **Then**：① 文件变更后 ≤5s 内 `local_state.dirty_count` 与 `repo_status.main_status`（→`DIRTY`）更新，UI `data-testid="freshness-local"` 时间前进；② commit 后 ≤10s 内 `head_sha` 更新；③ checkout 后 ≤10s 内 `head_ref` 更新；④ fetch 后 ≤10s 内 `behind/ahead` 与 `remote_ref` 更新；⑤ 全部更新仅针对变化仓（未变化仓 `scan_at` 不变）。
- **关联 NFR/OQ**：NFR-PERF-08、FR-STA-05；—

#### AC-SCAN-10 · 热仓/冷仓分层频率（5s / 360min）
- **覆盖 FR**：FR-SCAN-04
- **测试层级/依赖**：IT（`CLK`）
- **Given**：1 个热仓（当前详情页）、1 个冷仓（无界面引用）；`scan.hotIntervalSec=5`、`scan.coldIntervalMin=360`。
- **When**：推进 `CLK` 5s→6s→360min。
- **Then**：① 热仓每 5s 一轮 L2；② 冷仓在 360min 前不触发 L2（git 进程数=0），360min 时触发 1 次；③ 设置项修改（`scan.hotIntervalSec=1`）即时生效（下一轮按新值）；④ 冷仓被用户打开后升级为热仓，下一轮起按 5s 节奏。
- **关联 NFR/OQ**：FR-SCAN-04、附录 D；—

#### AC-SCAN-11 · 关系判定前新鲜度门控 + 写前强制 fetch + UI 分源时间
- **覆盖 FR**：FR-SCAN-05
- **测试层级/依赖**：IT（`G-WIRE`、`CLK`）
- **Given**：本地 `A_Design` 上次 fetch 于 `T0`；`ttl.fetchSec=1200`。
- **When**：① `CLK=T0+19min` 时打开看板；② 对 `A_Design` 发起 SYNC（破坏性，`fetch.beforeWrite=true`）；③ 令 `CLK=T0+21min` 再打开。
- **Then**：① 19min 内关系类状态与 `repo_status` 计算值一致且非 `UNKNOWN`，UI 显示“远程数据 19 分钟前”；② 写前强制 fetch 发生时 `G-WIRE`/真 git 记录一次 fetch，成功后 `remote_ref.observed_at` 更新，后续覆盖以新 HEAD 为基准（外部改动导致操作失败且零改动，见 AC-OP-15）；③ 21min 时关系类状态（BEHIND/AHEAD/DIVERGED/SYNCED）变 `UNKNOWN`（`STATE-E001`），本地事实类状态保持，UI 分别显示“本地数据 X 前 / 远程数据 21 分钟前（已过期）”；④ **失败路径**：写前强制 fetch 失败（断网，`NET-E001`）时，破坏性写操作**被阻断**（不入队、无备份 ref、仓库零改动），UI 提示“远程数据不可用，无法安全执行”，不得跳过 fetch 直接执行覆盖。
- **关联 NFR/OQ**：FR-SCAN-05、NFR-PERF-08、STATE-E001；—

#### AC-SCAN-12 · 长路径/中文空格/大小写冲突/reparse/网络盘检测与告警
- **覆盖 FR**：FR-SCAN-06
- **测试层级/依赖**：IT（本地 FS 夹具、`G-E2E` 长路径仓）
- **Given**：① 中文+空格路径 `D:\MBD 测试\工作区\A_Design`；② 总路径 >260 字符的仓；③ `X_Design`/`x_design` 同目录并存；④ junction 指向外部目录；⑤ UNC 路径根。
- **When**：执行 L0+L2 扫描并尝试对各类仓发起写操作（Fetch/Commit）。
- **Then**：① 中文/空格路径被识别为本地仓（`is_local=1`）且 `main_status=SYNCED`；② 长路径仓：`LongPathsEnabled`/`core.longpaths` 检测结果写入状态栏/诊断，超限写操作返回 `FS-E001`；③ 大小写冲突：两目录均入 `scan_issue(CASE_CONFLICT)`，不静默合并；④ reparse/网络盘/UNC 目标**不纳管**（不进入 `component`，无 `repo.is_local=1`）：记 `scan_issue(REPARSE)`/`scan_issue(NETWORK_DRIVE)` 告警并给只读提示（`SCAN-E002`）；⑤ 若已纳管仓的路径随后变为不可写（网络盘断开/reparse 变更）：写操作被拒（`SCAN-E002`/`OP-E004`）、仅只读展示且仓库零改动；⑥ 已纳管仓如仍置 `reparse_point` flag，则删除入口禁用。
- **关联 NFR/OQ**：NFR-COMPAT-03/04、FS-E001、SCAN-E002；R-10 已关闭（SRS v0.2.1 §6.3）

#### AC-SCAN-13 · submodule 不纳管、仅标记（SRS v0.2.1 §6.3 已冻结）
- **覆盖 FR**：FR-SCAN-06（SRS §6.3“不纳管 submodule（只标记）”）
- **测试层级/依赖**：IT（含 submodule 的夹具）
- **Given**：`A_Design` 内含 1 个 submodule（`.gitmodules` + 子目录）。
- **When**：执行 L2 扫描 → 打开单仓详情。
- **Then**：① 扫描**不**把 submodule 目录当独立仓发现（`repo` 行数不增加）；② 单仓详情/文件树对 submodule 显示“未纳管（submodule）”标记 `data-submodule="true"`（SRS v0.2.1 §6.3 冻结 `submodule` 标记；若 §7.3 后续补列则同时断言 `flags_json` 含 `submodule`）；③ 对 submodule 路径的写操作被拒（`OP-E004`）；④ 主仓状态不受 submodule 内部变更影响（改 submodule 内文件后主仓 `dirty_count` 不含该路径）。
- **关联 NFR/OQ**：SRS §6.3；R-10 已关闭（不纳管 submodule、标 `submodule` 标记、写操作拒绝）

### 2.4 状态计算（FR-STA）

#### AC-STA-01 · 附录 C 状态矩阵：14 主状态逐夹具唯一命中
- **覆盖 FR**：FR-STA-01、FR-STA-02
- **测试层级/依赖**：UT 表驱动（附录 C 全部 `F-*`，测试内构造的状态夹具）
- **Given**：附录 C 的 25 个 `F-*` 夹具（由测试在临时目录构造，不依赖 `m2-matrix.json`）；`m2-matrix.json` 只负责 14×5 按钮矩阵，不含 `F-*`。
- **When**：对每个夹具执行“扫描→状态计算”纯函数调用。
- **Then**：① 每夹具 `repo_status.main_status` 与测试期望 `primary` **完全相等**（14 个状态值全部至少命中 1 次；F-PARTIAL 例外见 ⑤）；② 每夹具 `main_status` 唯一（不得同时命中两态）；③ 任一步骤失败（如 git 不可用）时结果为 `UNKNOWN` 而非沿用旧值；④ 夹具执行数=25（附录 C 行数），缺失视为未通过（红队 06 AC-04 要求）；⑤ **F-PARTIAL null 例外（M-15）**：该夹具 `expected_primary_state=null`，**不参与主状态相等断言**，只断言组件聚合 `aggregate_status=OP_PARTIAL_FAILED`；其余 24 个夹具主状态逐条相等（本 AC 夹具总数 25 = 附录 C 25 行；`F-MISSING+BLOCKED` 逐仓主状态为 MISSING/UNMERGED，聚合断言见 AC-STA-02）。
- **关联 NFR/OQ**：FR-STA-01、附录 C；R-03/R-04 已关闭（SRS v0.2.1 附录 C：`F-TAG_DRIFT` 第 4 仓固定 `SYNCED`；`F-LFS-MISSING` 定死 SYNCED+flag）

#### AC-STA-02 · 附录 C 聚合矩阵：15 聚合态逐夹具 + 未命中兜底 UNKNOWN（SRS v0.2.1 顺序）
- **覆盖 FR**：FR-STA-01、SRS §7.4
- **测试层级/依赖**：UT 表驱动（附录 C 聚合映射节）
- **Given**：按附录 C“聚合态覆盖”表构造的 15 组夹具（按 SRS v0.2.1 §7.4 聚合优先级 0–14 排列，`MISSING=3` 先于 `BLOCKED_LOCAL=4`）：`OP_PARTIAL_FAILED←F-PARTIAL`(0)、`BINDING_ERROR←F-INVALID-*`(1)、`UNKNOWN←F-UNKNOWN`(2)、`MISSING←F-MISSING`(3)、`BLOCKED_LOCAL←F-UNMERGED/F-IN_PROGRESS`(4)、`EMPTY←F-EMPTY`(5)、`TAG_DRIFT←F-TAG_DRIFT/F-TAG-MOVED`(6)、`DIVERGED←F-DIVERGED`(7)、`BEHIND←F-BEHIND`(8)、`AHEAD←F-AHEAD`(9)、`DETACHED←F-DETACHED-*`(10)、`MIXED_BRANCH←F-DETACHED+MIXED`(11)、`NEW_BRANCH←F-NO_UPSTREAM-*`(12)、`DIRTY←F-DIRTY`(13)、`SYNCED←F-SYNCED`(14)。
- **When**：对每组夹具的四仓主状态执行聚合纯函数；另跑 v0.2.1 新增的 `F-MISSING+BLOCKED` 组合夹具（Design=MISSING/Impl=UNMERGED）。
- **Then**：① `component.aggregate_status` 与期望聚合态**完全相等**（15 个聚合态全部命中）；② 聚合优先级严格按 SRS v0.2.1 §7.4 的 0–14 顺序：`OP_PARTIAL_FAILED=0 / BINDING_ERROR=1 / UNKNOWN=2 / MISSING=3 / BLOCKED_LOCAL=4 / EMPTY=5 / TAG_DRIFT=6 / DIVERGED=7 / BEHIND=8 / AHEAD=9 / DETACHED=10 / MIXED_BRANCH=11 / NEW_BRANCH=12 / DIRTY=13 / SYNCED=14`；`MISSING+UNMERGED` 组合必须得 `MISSING`（不得得 `BLOCKED_LOCAL`）；同时存在 DIVERGED+BEHIND 时为 `DIVERGED`；③ 构造一个“未命中任何显式行”的组合 → 结果必须为 `UNKNOWN`，不得为空白/`SYNCED`；④ 任一仓 `INVALID` ⇒ `BINDING_ERROR`，任一仓 `UNKNOWN/STALE` ⇒ 组件非 `SYNCED`；⑤ **口径唯一性（与图集/夹具已对齐）**：本 AC 的 0–14 常量必须与 测试构造的 D3 步骤、`tests/MbdGitTool.Host.Tests` 夹具 的 `AggregatePriority` 一致；任一资产仍为 `BLOCKED_LOCAL=3 / MISSING=4` 即判本 AC 失败并登记为资产未回写（C-03）。
- **关联 NFR/OQ**：FR-STA-01/02；OQ-20；C-03（SRS v0.2.1 已回写，图集/夹具同序）

#### AC-STA-03 · 主状态互斥穷尽（property-based 随机提交图）
- **覆盖 FR**：FR-STA-01
- **测试层级/依赖**：UT + property-based（随机提交图生成器）
- **Given**：随机生成 ≥500 组提交图（分支数 1–5、提交数 0–50、随机 detached/tag/dirty/upstream 缺失/远程删除），每组落成真实 git 仓。
- **When**：对每组执行扫描 + 状态判定纯函数。
- **Then**：① 每组结果恰好 1 个主状态，且 ∈ 14 个合法枚举；② 每组聚合态 ∈ 15 个合法枚举；③ 不存在“空白/None/其他字符串”；④ 同一输入两次判定结果一致（幂等）；⑤ 失败样本最小化后写入报告并作为回归夹具。
- **关联 NFR/OQ**：FR-STA-01；—

#### AC-STA-04 · 假绿防护：任一仓 UNKNOWN/STALE ⇒ 组件不得 SYNCED
- **覆盖 FR**：FR-STA-02
- **测试层级/依赖**：IT（`F-UNKNOWN` + 注入失败）
- **Given**：四仓中 3 仓 `SYNCED`、第 4 仓注入 fetch 失败并超 TTL → 该仓 `UNKNOWN`（`stale` flag）。
- **When**：执行聚合。
- **Then**：① `component.aggregate_status=UNKNOWN`，UI 显示“部分状态未知”+ 指明 1 个仓（`data-unknown-repos="SIL"`）；② 卡片**不**显示绿色/SYNCED（`data-aggregate`≠`SYNCED`）；③ 若第 4 仓同时 `INVALID`（另一组）：按 §7.4 优先级 `BINDING_ERROR`(1) 先于 `UNKNOWN`(2)，结果为 `BINDING_ERROR` 且同时列出 UNKNOWN 仓（SRS v0.2 §7.4 已裁决，R-15 关闭）；④ 数据行不因 UNKNOWN 被隐藏或清空。
- **关联 NFR/OQ**：FR-STA-02、STATE-E001；R-15 已关闭（SRS v0.2 §7.4 裁决 BINDING_ERROR 遮蔽 UNKNOWN）

#### AC-STA-05 · `compare_ref` 三来源 + `merge-base --is-ancestor` 退出码 0/1/>1
- **覆盖 FR**：FR-STA-03
- **测试层级/依赖**：UT + IT（本地 git 夹具、`G-WIRE` 记录 compare 调用）
- **Given**：三组夹具：① 已设 upstream 的分支（`F-BEHIND`）；② 无 upstream 但远程存在同名分支（`F-NO_UPSTREAM-UNSET`）；③ 无 upstream 且远程无同名分支（`F-NO_UPSTREAM-LOCAL`）。
- **When**：先 `git fetch`，再按 D-014 解析 `compare_ref` 并执行 `merge-base --is-ancestor`。
- **Then**：① 来源①`compare_ref=<local_branch>@{upstream}`；来源②`refs/remotes/<remote>/<local_branch>`；来源③无 `compare_ref` → 主状态 `NO_UPSTREAM`；② 退出码 0 ⇒ BEHIND/AHEAD 按方向判定；退出码 1 ⇒ DIVERGED；退出码 >1（如 ref 不存在）⇒ 回退/`UNKNOWN` 并记录错误码，**不得**猜测为 DIVERGED；③ 判定结果与“直接命令行跑 git 命令的真值机”逐用例一致（对拍 ≥20 例）；④ 退出码 >1 的样本有专门单测。
- **关联 NFR/OQ**：FR-STA-03、D-014；—

#### AC-STA-06 · Gitea compare 仅兜底：限已推送 commit，未推送不调用 API
- **覆盖 FR**：FR-STA-03
- **测试层级/依赖**：IT（`G-WIRE` 请求记录 + 本地仓）
- **Given**：① 本地存在未推送 commit（`F-AHEAD`）；② 本地对象库缺失某个已存在于服务端的 commit（shallow 夹具）。
- **When**：执行关系判定。
- **Then**：① 场景① `G-WIRE` 记录中 `/compare` 端点调用次数=0；② 场景② 调用 `/compare` 1 次，结果用于兜底且 `data_source` 标记 compare 来源（SRS §7.1）；③ compare 返回 404/500 时回退 `UNKNOWN` 并写 `error_code`；④ 任何情况下不为未推送 commit 生成“远程关系”。
- **关联 NFR/OQ**：FR-STA-03、§7.1；—

#### AC-STA-07 · `repo_status` 字段完整性（新鲜度四元组）
- **覆盖 FR**：FR-STA-04
- **测试层级/依赖**：IT（`F-SYNCED`、`F-UNKNOWN`、`CLK`）
- **Given**：`F-SYNCED`（成功）与 `F-UNKNOWN`（fetch 失败 + TTL 过期）两仓。
- **When**：分别计算状态后查询 `repo_status`。
- **Then**：① 两行均含 `calc_at`（UTC ISO-8601）、`last_success_at`、`error_code`、`data_source`、`is_stale` 且非空（`error_code` 成功时为 NULL/空、失败时为附录 D 错误码）；② `F-UNKNOWN` 行 `is_stale=1`；③ 查询 API 返回上述字段（响应 JSON key 与表字段同名）；④ 旧值不被沿用：把 fetch 改为失败后 `main_status=UNKNOWN`，`calc_at` 前进。
- **关联 NFR/OQ**：FR-STA-04；—

#### AC-STA-08 · 新鲜度展示：本地/远程分源 + 统一格式 + 过期态
- **覆盖 FR**：FR-STA-04、FR-BOARD-05
- **测试层级/依赖**：IT + VIS（`CLK`）
- **Given**：本地扫描于 `T0`、远程 fetch 于 `T1`、`CLK` 当前为 `T2`。
- **When**：打开看板与组件详情。
- **Then**：① 三处（状态栏/看板行/组件详情）均分源显示“本地数据 X 前 / 远程数据 Y 前”，`data-testid="freshness-local"`/`"freshness-remote"`；② 格式统一为“数据 X 秒前/分钟前”（<60s 用秒、≥60s 用分钟，取整规则固定）；③ 任一源超 TTL ⇒ 对应区 `data-stale="true"` + 斜纹 hatched token；④ 不带时间戳的裸“已同步”文案不得出现（扫描全站文案断言）。
- **关联 NFR/OQ**：NFR-PERF-08、STATE-E001；—

#### AC-STA-09 · TTL 表与超期降级：90s / 1200s / 25200s
- **覆盖 FR**：FR-STA-02、FR-STA-04、SRS §7.1
- **测试层级/依赖**：IT + UT（`CLK`、`F-*`）
- **Given**：`ttl.localSec=90`、`ttl.fetchSec=1200`、`ttl.remoteSec=25200`（附录 D 默认值）。
- **When**：推进 `CLK` 至各 TTL 阈值前 1s / 阈值 / 阈值后 1s，分别触发状态计算。
- **Then**：① 本地指纹 TTL 90s：超期时依赖本地新鲜度的 `is_stale=1`；② fetch TTL 1200s：超期时关系类状态（BEHIND/AHEAD/DIVERGED/SYNCED）→ `UNKNOWN`，错误码 `STATE-E001`；③ 远程元数据 TTL 25200s：超期时元数据区 STALE 且看板标“远程数据已过期”；④ 阈值前 1s 不降级（边界断言）；⑤ 每类超期都有对应 UI 提示，不出现静默假绿。
- **关联 NFR/OQ**：NFR-PERF-08、STATE-E001；—

#### AC-STA-10 · 写操作完成 ≤2s 回写受影响组件
- **覆盖 FR**：FR-STA-05
- **测试层级/依赖**：IT（`G-E2E`、`CLK`）
- **Given**：`A_Design` 处于 `BEHIND`（远程领先 2 提交）；执行一次批量 Pull 成功。
- **When**：记录 Pull `job_item` 完成时刻 `T`，轮询 `component.status_calc_at`/`repo_status.calc_at` 与 UI。
- **Then**：① `T+2s` 内 `repo_status.main_status` 变为 `SYNCED` 且 `calc_at ≥ T`；② UI 该行 `data-aggregate` 同步更新（不整表重渲染，见 AC-BOARD-12）；③ 受影响组件的 dirty-set 重算（其它组件 `calc_at` 不变）；④ 超 2s 视为未通过（P95 断言）。
- **关联 NFR/OQ**：NFR-PERF-08、FR-STA-05；—

#### AC-STA-11 · `TAG_DRIFT` 无条件计算（无“基线模式”）三种触发
- **覆盖 FR**：FR-STA-06、SRS §7.4
- **测试层级/依赖**：UT + IT（`F-TAG_DRIFT`、`F-TAG-MOVED`、`G-E2E`）
- **Given**：① 四仓中 3 仓 HEAD 为 tag `v1.0`、第 4 仓在 `main` 分支且无 `v1.0` tag（`F-TAG_DRIFT`）；② `F-TAG-MOVED`（本地/远程同名 tag 指向不同 commit）；③ `baseline_snapshot` 记录后某仓 tag 被移动。
- **When**：执行组件聚合（不开启任何“基线模式”开关，SRS 已作废该概念）。
- **Then**：① 三组均得 `aggregate_status=TAG_DRIFT`；② 组件卡片文案“基线不一致”（§7.4/§11.2），并逐仓展示 tag 值（缺 tag 仓显示空/`tag_present=0`）；③ 场景② `repo_status.flags_json` 含 `tag_moved`；④ 无任何 UI/配置项名为“基线模式”（全站文案扫描断言）。
- **关联 NFR/OQ**：FR-STA-06、D-010；R-03 已关闭（SRS v0.2.1 附录 C `F-TAG_DRIFT` 第 4 仓固定 `SYNCED`）

#### AC-STA-12 · 组件级 `MIXED_BRANCH` 判定与展示
- **覆盖 FR**：FR-STA-01、SRS §7.3/§7.4
- **测试层级/依赖**：UT + VIS（`F-DETACHED+MIXED`、`G-E2E`）
- **Given**：四仓分别处于 `main`、`develop`、`main`、`feature/x` 分支且无更高聚合态。
- **When**：执行聚合。
- **Then**：① `component.aggregate_status=MIXED_BRANCH`（组件级 `mixed_branch` 为 §7.3 派生标记：SRS §8.1 `component` 无 `flags_json` 列，本 AC **不要求存储落点**，N-03）；② 组件卡片显示“分支不一致”角标（`data-mixed="true"`）；③ 主行分支显示 Impl 仓分支 + 角标（与 AC-DETAIL-08 一致）；④ 若任一仓为 `DETACHED_TAG`，因 `DETACHED`(10) 优先于 `MIXED_BRANCH`(11)，聚合为 `DETACHED`（按 §7.4）。
- **关联 NFR/OQ**：SRS §7.3/§7.4；—

#### AC-STA-13 · `DIRTY` 主状态与 `dirty_tracked`/`untracked`/`stashed` flags
- **覆盖 FR**：FR-STA-01、SRS §7.2/§7.3
- **测试层级/依赖**：UT + IT（`F-DIRTY`、`F-SYNCED` + 参数化）
- **Given**：4 组夹具：① HEAD==upstream + tracked 改动（`F-DIRTY`）；② 仅 untracked 文件；③ 有 stash；④ 干净 `F-SYNCED`。
- **When**：执行状态判定。
- **Then**：① 组①`main_status=DIRTY`、`flags_json` 含 `dirty_tracked`；② 组②仅 untracked：默认 `dirty.includeUntracked=false`（SRS v0.2 附录 D/D-021）→ 断言 `main_status=SYNCED` + `untracked` flag；参数化把该键改为 `true` → 同一夹具 `main_status=DIRTY`；③ 组③ `stashed` flag 存在且不影响主状态；④ 组④`main_status=SYNCED` 且 flags 为空；⑤ `dirty_count`/`untracked_count` 数值与夹具一致。
- **关联 NFR/OQ**：SRS §7.2/§7.3、附录 D `dirty.includeUntracked`、D-021；R-23 已关闭

#### AC-STA-14 · 离线可判定状态集：断网下 7 类本地事实照常判定
- **覆盖 FR**：FR-STA-02、SRS §7.1
- **测试层级/依赖**：IT（断网模拟 + 附录 C 离线子集）
- **Given**：断开全部网络（`G-WIRE`/`G-E2E` 不可达）且 fetch TTL 过期。
- **When**：对 `F-INVALID-NOTREPO`、`F-INVALID-REMOTE`、`F-MISSING`、`F-EMPTY`、`F-UNMERGED`、`F-IN_PROGRESS`、`F-DETACHED-TAG`、`F-DETACHED-COMMIT` 执行本地扫描与状态判定。
- **Then**：① 8 个夹具的主状态分别与附录 C 期望一致（**不**被压成 UNKNOWN）；② 关系类夹具（`F-SYNCED`/`F-BEHIND`/`F-AHEAD`/`F-DIVERGED`）在离线时 → `UNKNOWN` 且 `is_stale=1`；③ UI 显示“远程数据 X 前（离线）”，本地事实状态照常展示；④ 不出现全表灰/全 UNKNOWN（与 NFR-REL-01 一致）。
- **关联 NFR/OQ**：NFR-REL-01、SRS §7.1；—

#### AC-STA-15 · 状态×操作矩阵抽样：禁止操作返回明确错误码且仓库零改动
- **覆盖 FR**：FR-STA-01、SRS §7.6
- **测试层级/依赖**：IT（附录 C 夹具 × 7.6 矩阵抽样）
- **Given**：SRS v0.2.1 §7.6 矩阵 13 行（DETACHED_TAG/DETACHED_COMMIT 合并 1 行）每行至少 1 个夹具（如 UNKNOWN→`F-UNKNOWN`、INVALID→`F-INVALID-REMOTE`、MISSING→`F-MISSING`、EMPTY→`F-EMPTY`、UNMERGED→`F-UNMERGED`、IN_PROGRESS→`F-IN_PROGRESS`、DETACHED→`F-DETACHED-COMMIT`、DIVERGED→`F-DIVERGED`、BEHIND→`F-BEHIND`、AHEAD→`F-AHEAD`、NO_UPSTREAM→`F-NO_UPSTREAM-*`、DIRTY→`F-DIRTY`、SYNCED→`F-SYNCED`）；SRS v0.2.1 的 SYNC 强覆盖列已改为：DIRTY=`⚠️ stash -u + 确认`、DIVERGED=`⚠️ 默认阻断，备份 ref + 输入组件 ID 后允许`、SYNCED=`—（无需；仅清理 untracked 需确认）`。
- **When**：对每夹具按矩阵逐操作发起：Fetch/Pull(ff)/Push/Commit/Checkout 分支/切 Tag/SYNC 强覆盖/Delete/CreateBranch/SetUpstream；SYNC 列按上述 ⚠️/— 逐格抽样。
- **Then**：① 矩阵标 ❌ 的操作全部被拒绝，返回 `OP-E004`，UI 给出替代动作（如“仅修复/先提交”）；② 被拒操作后仓库零改动（`HEAD`、`git status`、`.git` 目录树 hash 前后一致），无 `job` 落库或 `job.state=CANCELLED`（不重复）；③ 标 ✅/⚠️ 的操作至少各成功 1 例，且 SYNC 三格与 AC-OP-15/16/18 无互斥：DIRTY 夹具 stash -u+确认后 SYNC 成功、DIVERGED 夹具备份 ref+输入组件 ID 后 SYNC 成功、SYNCED 夹具 SYNC 无需确认且为 no-op（仅勾选清理 untracked 才需确认）；④ 抽样覆盖矩阵 13 行 × ≥4 列，缺行视为未通过。
- **关联 NFR/OQ**：SRS §7.6、OP-E004；C-04（v0.2.1 已回写；不得再按 DIRTY/DIVERGED/SYNCED=❌ 断言）

### 2.5 看板与检索（FR-BOARD）

#### AC-BOARD-01 · 表格默认视图：列齐全 + 默认排序（`severity_rank` 升序→组件 ID 升序）
- **覆盖 FR**：FR-BOARD-01
- **测试层级/依赖**：IT + VIS（`C-SYNTH` 或 `G-E2E` E2E-NORMAL）
- **Given**：1,000 组件数据，含 15 类聚合态各若干、3 个组件 `severity_rank` 相同；另含两组 `BINDING_ERROR`：仅缺仓（`binding_cause=INCOMPLETE`→`severity_rank=12`）与缺仓+INVALID（`binding_cause=INVALID`→`severity_rank=1`）；当前视图=表格。
- **When**：打开 P1 看板（默认排序）。
- **Then**：① 行内列齐全且顺序固定：多选框、组件 ID+变更徽标、聚合状态徽标、`[D][I][M][S]` 四仓 chip、分支（含不一致角标）、Tag、CI 槽位、新鲜度、操作菜单（`data-col` 顺序断言）；② 默认排序=按 SRS D-020/§7.4 的 `severity_rank` **升序**（数值小=严重度高，排前）→ `component_id` 升序：整表顺序与“`severity_rank` 升序 + `component_id` 升序”独立计算结果逐行相同，**不得使用 OQ-21 旧暂定表**；③ **`BINDING_ERROR` 条件化断言（C-02）**：仅缺仓组件（`binding_cause=INCOMPLETE`，`component.incomplete=1`）必须排在 `severity_rank=12` 的位置，不得排到最高档；缺仓+INVALID 组件（`binding_cause=INVALID`，存在 `repo_status.main_status=INVALID` 仓）按 `severity_rank=1` 排在最前档（同一 `BINDING_ERROR` 不得写死单一 rank）；④ 同 `severity_rank` 行按 `component_id` 升序；⑤ 卡片视图与表格数据行数/组件 ID 集合一致。
- **关联 NFR/OQ**：FR-BOARD-01；D-020/§7.4（R-02 已关闭）；C-02（`binding_cause` 1/12 双档）

#### AC-BOARD-02 · 卡片视图：数据一致 + 几何约束（≥320×104、4px 左轨）
- **覆盖 FR**：FR-BOARD-01、SRS §11.3
- **测试层级/依赖**：VIS + IT
- **Given**：同 AC-BOARD-01 数据集。
- **When**：切换视图为卡片；在 1440×900/100% 下测量。
- **Then**：① 卡片视图组件 ID 集合、聚合态、四仓 chip 状态与表格视图逐项一致；② 卡片宽 ≥320px、高 ≥104px（渲染盒测量）；③ 左轨宽 4px 且颜色=对应状态 token；④ 卡片底色为中性 token（非状态饱和色）；⑤ 卡内仅 1 个 CI 槽位（与 AC-CI-02 一致）。
- **关联 NFR/OQ**：SRS §11.3；—

#### AC-BOARD-03 · 4,000 组件虚拟滚动：≥58fps、单帧 ≤33ms、DOM 节点预算
- **覆盖 FR**：FR-BOARD-01、FR-BOARD-02
- **测试层级/依赖**：PERF（`C-SYNTH` E2E-STRESS、WebView2 DevTools）
- **Given**：`C-SYNTH` 生成 4,000 组件/16,000 仓（生成器版本记录在报告）；1440×900/100%。
- **When**：按 §13.3 用 DevTools Performance 时间线滚动 10s 采帧；同时统计 DOM。
- **Then**：① 平均帧率 ≥58fps；② 单帧 ≤33ms（不允许出现 >33ms 的帧）；③ DOM 卡片节点 ≤120、总元素 ≤15,000；④ 仅可视 + ≤8 屏 overscan 被渲染（虚拟滚动断言）；⑤ 滚动后滚动锚点与选中项保持（不跳动）；⑥ 报告含测试机规格/杀软/磁盘型号。
- **关联 NFR/OQ**：NFR-PERF-03（旧 AC-13 已作废，仅映射见 §1.4）；OQ-05

#### AC-BOARD-04 · 全局搜索：5 字段命中与匹配规则
- **覆盖 FR**：FR-BOARD-02
- **测试层级/依赖**：IT + UT（搜索服务）
- **Given**：数据集含：组件 `CF325F_ChanganSigProcgBascFct`（分支 `feat/abc`、tag `v1.0`、HEAD `a1b2c3d4`）、仓库 `CF325F_ChanganSigProcgBascFct_Design`。
- **When**：依次搜索：`CF325F`、`ChanganSig`、`CF325F_..._Design`（全名）、`feat/abc`、`v1.0`、`a1b2c3d`（HEAD 短 SHA 前缀）。
- **Then**：① 6 次搜索均命中同一组组件（结果集一致，行数=1）；② 搜索大小写不敏感；③ 搜索**不**匹配变更徽标字符 `●` 与 `*`（搜索 `*` 结果=0，避免通配符冲突）；④ 搜索字段不包含未同步仓的分支/Tag（只覆盖“关注”范围，与 AC-SYNC-04 一致）；⑤ 命中高亮节点存在（`data-match`）。
- **关联 NFR/OQ**：FR-BOARD-02、NFR-PERF-04；—

#### AC-BOARD-05 · 搜索防抖 120ms 与输入到结果 P95 ≤300ms
- **覆盖 FR**：FR-BOARD-02、FR-BOARD-05
- **测试层级/依赖**：PERF（`C-SYNTH`、DevTools）
- **Given**：4,000 组件数据集；输入法中文候选场景（`compositionstart/compositionend`）。
- **When**：连续键入 10 个字符（间隔 50ms）；以及单次输入后测 20 轮取 P95。
- **Then**：① 仅触发 1 次查询（防抖 120ms：`G-WIRE`/查询日志计数=1；输入间隔 <120ms 时查询不在每次击键触发）；② 输入到结果渲染 P95 ≤300ms；③ 中文 IME 组合期间不触发查询（`compositionend` 后触发 1 次）；④ 查询过程不阻塞输入（无 >100ms 长任务）。
- **关联 NFR/OQ**：NFR-PERF-04；R-29 已关闭（SRS v0.2.1 FR-BOARD-02：IME 以 `compositionend` 触发）

#### AC-BOARD-06 · facet 枚举全集与组合筛选
- **覆盖 FR**：FR-BOARD-02
- **测试层级/依赖**：IT + VIS
- **Given**：数据集覆盖 15 聚合态、14 单仓主状态、4 种 RepoType、多分支/多 Tag、CI 占位、dirty/未推送。
- **When**：逐个打开筛选 facet 并断言选项；再做 2 个 facet 组合筛选。
- **Then**：① facet 共 9 组且枚举与 SRS 一致：聚合状态（15 值）、单仓主状态（14 值）、RepoType（`DESIGN/IMPL/MIL/SIL`）、分支、Tag、CI 状态（占位、禁用态）、有未提交改动、有未推送提交（共 8 个可筛组 + 1 个 CI 占位，若实现分组合并则断言键名与 SRS 一一对应）；② 选项计数之和=对应全量计数；③ 组合筛选结果=交集；④ CI facet 为禁用占位 + tooltip“即将上线”（第 7 锚点，见 AC-CI-01）；⑤ 清空筛选恢复全量行数。
- **关联 NFR/OQ**：FR-BOARD-02、CI-7；—

#### AC-BOARD-07 · 排序三键（严重度/组件 ID/最近活动）与升降序
- **覆盖 FR**：FR-BOARD-02
- **测试层级/依赖**：IT + UT（排序服务）
- **Given**：1,000 组件，`component.last_activity_at` 由夹具指定（10 个不同时间；SRS v0.2.1 补列；字段为空时按 ③ 回退）。
- **When**：依次切换排序：严重度↓、组件 ID↑↓、最近活动↓↑。
- **Then**：① 每种排序的整表顺序与独立计算期望逐行一致（“最近活动”排序键=`component.last_activity_at` 降序，为空的行回退 `status_calc_at`）；② 升降序切换后首末行互换；③ 排序键在 URL/状态中保留（刷新后保持）；④ 非法排序键回退默认（严重度↓）并记录日志；⑤ 排序不改变筛选结果集（行数不变）。
- **关联 NFR/OQ**：FR-BOARD-02、§7.4/D-020；C-05（`component.last_activity_at`，SRS v0.2.1 补列）

#### AC-BOARD-08 · 七态齐备（空/加载/错误可重试/离线降级/过期/部分失败/权限过期）
- **覆盖 FR**：FR-BOARD-03
- **测试层级/依赖**：IT + VIS（注入）
- **Given**：分别注入 7 种条件：无数据、请求中、API 500、断网、数据超 TTL、`F-PARTIAL` 部分失败、401（认证过期）。
- **When**：逐一打开 P1 看板。
- **Then**：① 每态有唯一可识别空/错态组件（`data-page-state` 取 `empty/loading/error/offline/stale/partial/permission`）；② 每态均有“重试/重登/刷新”可点击入口且点击后状态推进；③ 错误态显示错误码（附录 D，如 `NET-E001`/`SYNC-E001`）与文案；④ 权限过期态提供“重新登录”入口（与 AC-CFG-08 共用）；⑤ 七态截图入视觉基线。
- **关联 NFR/OQ**：FR-BOARD-03、NFR-REL-01；—

#### AC-BOARD-09 · 批量操作条阈值：>10 组件或 >40 仓触发二次确认
- **覆盖 FR**：FR-BOARD-04
- **测试层级/依赖**：IT + VIS
- **Given**：看板有 ≥12 个可操作组件、对应 ≥48 仓。
- **When**：① 选中 10 个组件（=40 仓）→ 点“批量 Fetch”；② 选中 11 个组件（=44 仓）→ 点“批量 Fetch”。
- **Then**：① 场景① 无二次确认（直接入队，仅预检）；② 场景② 弹出二次确认，文案含“11 个组件 / 44 个仓”，需点“确认执行”才入队；③ 取消确认时 `job` 行数不增；④ 边界值 10 与 40 均为“不触发”（阈值严格 >10 且 >40，按 SRS 原文）；⑤ 确认后 `job.target_json` 与选择集合一致。
- **关联 NFR/OQ**：FR-BOARD-04；—

#### AC-BOARD-10 · 多选与“全选筛选结果”
- **覆盖 FR**：FR-BOARD-04
- **测试层级/依赖**：IT + VIS
- **Given**：筛选“聚合状态=BEHIND”命中 37 个组件。
- **When**：① 手动多选 3 个；② 点“全选筛选结果”；③ 清空筛选。
- **Then**：① 手动多选计数显示 `3`；② “全选筛选结果”后计数=`37`（跨虚拟滚动全量选中，不仅可视行），`data-selected-count="37"`；③ 清空筛选后选择集保持 37（或按实现显式提示“已选 37”）；④ 取消全选后计数=0；⑤ 选中状态在滚动中保持。
- **关联 NFR/OQ**：FR-BOARD-04；—

#### AC-BOARD-11 · 批量操作条操作集合与逐仓进任务中心
- **覆盖 FR**：FR-BOARD-04
- **测试层级/依赖**：IT
- **Given**：选中 2 个组件（8 仓）。
- **When**：打开批量操作条，枚举可见操作；执行其中 1 个（Fetch）。
- **Then**：① 操作条含 9 项且与 SRS 一致：Clone/Pull/Fetch/Checkout/Tag/SYNC/Delete/Restore/Create；② 与状态矩阵不兼容的项为禁用态并给 tooltip（如对缺失组件禁用 SYNC）；③ 执行后跳转/打开任务中心对应 `job`，逐仓 8 行可见（组件/仓级进度）；④ 任务中心返回看板后选中集保留。
- **关联 NFR/OQ**：FR-BOARD-04；—

#### AC-BOARD-12 · 数据新鲜度分源展示 + 手动刷新不整表闪白
- **覆盖 FR**：FR-BOARD-05、SRS §7.5
- **测试层级/依赖**：VIS + PERF
- **Given**：4,000 组件看板已加载，记录当前滚动位置与选中行；`CLK` 固定。
- **When**：点“刷新当前视图”（触发 fetch + 重扫）。
- **Then**：① 刷新中保留旧数据行，行上出现行内加载态（skeleton/半透明），不出现整表白屏/清空（截图无空白帧）；② 刷新后仅变化行更新（keyed diff：未变化行 DOM 节点身份不变）；③ 滚动位置与选中项保持；④ `data-testid="freshness-local"`/`"freshness-remote"` 时间前进；⑤ 刷新失败时保留旧数据 + 错误 Toast + 错误码，不整表报错。
- **关联 NFR/OQ**：NFR-PERF-08、FR-BOARD-05；—

#### AC-BOARD-13 · 未绑定仓库入口：原因分组/重新解析/忽略需记录理由
- **覆盖 FR**：FR-BOARD-06
- **测试层级/依赖**：IT + VIS
- **Given**：`unbound_repo` 有 5 条（原因见 AC-SYNC-13）。
- **When**：打开“未绑定仓库”抽屉/异常清单页；对 1 条点“重新解析”；对 1 条点“忽略”并输入理由。
- **Then**：① 入口存在于 P1 页面（`data-testid="unbound-entry"`），抽屉按 `reason` 分组且恰 3 组（DOCS/PATTERN/CASE_CONFLICT，与 AC-SYNC-13 一致）；② “重新解析”重新执行绑定规则，成功后该仓移出清单（行数 5→4），失败保持并更新 `discovered_at`；③ “忽略”必须填写理由（空理由时确认按钮 `disabled`），忽略后该条不再显示但 `unbound_repo` 行保留忽略记录；④ 操作写审计；⑤ 清单不影响看板组件数。
- **关联 NFR/OQ**：FR-BOARD-06（旧 AC-23 仅映射见 §1.4）；R-38 已关闭（SRS v0.2.1 §8.1 `reason` CHECK 四值）

#### AC-BOARD-14 · 首屏可交互 ≤1.5s、冷启动到首屏 ≤5s（两档数据集）
- **覆盖 FR**：FR-BOARD-01、FR-BOARD-05
- **测试层级/依赖**：PERF（`C-SYNTH`、DevTools Performance）
- **Given**：两档数据集：正常档 1,000 组件/4,000 仓、压测档 4,000 组件/16,000 仓；数据已就绪（不含首扫）；冷启动场景=进程冷启动、DB 已存在。
- **When**：按 §13.3 测量 LCP/首屏可交互时刻；重复 5 次取 P95。
- **Then**：① 两档“数据就绪后首屏可交互”均 ≤1.5s；② 冷启动到首屏 ≤5s（不含首扫）；③ 报告记录数据集生成器版本、测试机 8C/16G/SSD/Win11、杀软/磁盘型号；④ 结果入 `BENCH` 夜间门禁；⑤ 超预算即失败，不允许用“压测档豁免”掩盖。
- **关联 NFR/OQ**：NFR-PERF-02/09（旧 AC-13 已作废，仅映射见 §1.4）；OQ-05

#### AC-BOARD-15 · 状态三重编码 + token 扫描 + 对比度 + 深色主题
- **覆盖 FR**：FR-BOARD-01、SRS §10.5/§11.2
- **测试层级/依赖**：VIS + UT（token 静态扫描 + 对比度计算）
- **Given**：15 聚合态 × 6 关键 CI 态组合矩阵（≥12 张快照）、浅色/深色两主题、`prefers-reduced-motion: reduce`、`prefers-contrast`、`forced-colors`。
- **When**：渲染状态矩阵页并跑：① token 扫描（组件样式禁硬编码 hex，只允许 `--st-*`/`--ci-*`）；② 对比度计算（fg/bg、fg/卡片底）；③ 三重编码检查（图标+中文文案+颜色）；④ 深色/减少动效/高对比模式快照。
- **Then**：① 组件内无硬编码 hex（扫描命中=0）；② 全部组合对比度 ≥4.5:1（非文本 ≥3:1）；③ 每个状态均有图标+中文文案+颜色三通道（缺一即失败）；④ 红色只出现在 SRS §11.2 允许清单（INVALID/BINDING_ERROR/BLOCKED_LOCAL/UNMERGED/OP_PARTIAL_FAILED/REMOTE_GONE/lfs_missing/CI FAIL/破坏性确认按钮），AHEAD/DIVERGED/MISSING 不得为红；⑤ 深色模式 token 换值不换名、快照通过；`reduce-motion` 下动效 ≤1ms（无位移动画）；⑥ 视觉基线 ≥12 张且覆盖 15 聚合态。
- **关联 NFR/OQ**：SRS §10.5、§11.2；R-24 已关闭（§11.2 `DIRTY=success+变更徽标`）；hatched 对比度（H-04）由 SRS v0.2.1 修正，本 AC 断言不降低

#### AC-BOARD-16 · 可访问性：键盘可达、焦点可见、aria-live、点击目标
- **覆盖 FR**：FR-BOARD-01、SRS §10.5
- **测试层级/依赖**：VIS + IT（axe 规则 + 键盘脚本）
- **Given**：看板 + 组件详情 + 任务中心。
- **When**：仅键盘操作（Tab/Shift+Tab/Enter/Space/Esc）；运行 axe 规则集。
- **Then**：① 全部交互元素可 Tab 到达（无 tabindex 陷阱）；② 焦点环 2px 可见（截图断言）；③ 任务完成/失败通过 `aria-live` 播报（DOM 断言 `role=status`/`aria-live=polite` 文案变化）；④ 点击目标 ≥24×24、主按钮 ≥32px（尺寸断言）；⑤ axe 严重级违规=0；⑥ `Esc` 从详情返回看板且焦点回到原行。
- **关联 NFR/OQ**：SRS §10.5；—

### 2.6 组件/单仓详情（FR-DETAIL）

#### AC-DETAIL-01 · 组件详情四仓状态卡字段 + 一次点击可达
- **覆盖 FR**：FR-DETAIL-01
- **测试层级/依赖**：IT + VIS（`G-E2E`）
- **Given**：看板中存在组件 `A`（4 仓 SYNCED/BEHIND/DIRTY/MISSING 混合）。
- **When**：在看板行点击组件 ID。
- **Then**：① 一次点击进入 P2 组件详情（路由/URL 变化，无二次确认）；② 4 张仓状态卡字段齐全：分支、HEAD（短 SHA+全文 tooltip）、upstream、ahead/behind、dirty（`dirty_count`/`untracked_count`）；③ 数据与看板 chip/DB `local_state` 一致；④ `Esc`/面包屑返回看板并保持原滚动位置与选中行；⑤ **失败路径**：令详情数据源查询失败 → 页面显示错误态（`data-page-state="error"`）+ 错误码 + “重试”按钮，旧数据不清空不白屏；重试成功后 `data-page-state` 回到 `ready` 且 4 张卡数据与 DB `local_state` 一致。
- **关联 NFR/OQ**：FR-DETAIL-01；—

#### AC-DETAIL-02 · 组件详情：Tag 对照 + 未绑定告警 + 快捷操作
- **覆盖 FR**：FR-DETAIL-01、FR-STA-06
- **测试层级/依赖**：IT + VIS（`F-TAG_DRIFT`）
- **Given**：`F-TAG_DRIFT` 组件（3 仓 tag `v1.0`、1 仓缺 tag），且该组件有 1 条未绑定仓（`A_Docs` 不进绑定，用组件外部未绑定清单交叉）。
- **When**：打开组件详情。
- **Then**：① Tag 对照表 4 行，逐仓显示 tag 与 commit，缺 tag 仓显示“无”（`data-tag-present="0"`）；② 顶部出现“基线不一致”提示（与 `TAG_DRIFT` 一致）；③ 未绑定告警区存在并列出相关未绑定仓（`data-testid="unbound-alert"`）；④ 快捷操作含 Swap 到详情/任务中心（SYNC/Tag/Create 入口存在且按状态矩阵禁用规则生效）；⑤ 所有数据与 DB 一致（`baseline_snapshot` 行、`remote_ref` tag）。
- **关联 NFR/OQ**：FR-STA-06、D-010；—

#### AC-DETAIL-03 · 单仓详情四 Tab 顺序固定（概览/历史/差异/CI 第 4 位）
- **覆盖 FR**：FR-DETAIL-02、FR-CI-01
- **测试层级/依赖**：VIS + IT
- **Given**：仓 `A_Design`。
- **When**：打开 P3 单仓详情；记录 Tab 顺序与路由。
- **Then**：① Tab 顺序固定为 概览→提交历史→差异→CI（CI 恰为第 4 位），`role=tab` 的 `aria-posinset` 与顺序一致；② 每个 Tab 有独立路由参数（深链可直达，刷新后保持当前 Tab）；③ CI Tab 为占位但可渲染空态（见 AC-CI-08/AC-CI-11）；④ 未接入 CI 时 Tab 顺序不重排（快照断言）；⑤ **失败路径**：深链到不存在的 commit/分支参数 → 显示“内容不存在/已变更”错误态 + 返回入口，不崩溃、不空白。
- **关联 NFR/OQ**：CI-4、SRS §11.4；—

#### AC-DETAIL-04 · 提交历史 1 万提交：虚拟滚动 ≥58fps、首屏 ≤500ms
- **覆盖 FR**：FR-DETAIL-02
- **测试层级/依赖**：PERF（1 万提交夹具、DevTools）
- **Given**：仓含 10,000 个提交（脚本生成）；默认 50 条/页。
- **When**：打开提交历史 Tab；滚动 10s 采帧；测量首屏渲染。
- **Then**：① 首屏（首批 50 条）渲染 ≤500ms；② 滚动平均 ≥58fps、单帧 ≤33ms；③ 分页/滚动加载不重复/不丢行（提交 SHA 序列与 `git log --format=%H` 前 N 条一致）；④ 行内字段：短 SHA、作者、时间、message 首行；⑤ 二进制/超大 commit 不导致卡死（夹具含 1 个 5MB diff 提交）。
- **关联 NFR/OQ**：NFR-PERF-03；R-37 已关闭（SRS v0.2.1 FR-DETAIL-02 已同口径量化：首屏 ≤500ms/≥58fps/单帧 ≤33ms；旧 AC-13 仅映射见 §1.4）

#### AC-DETAIL-05 · 差异只读 + 二进制不可预览提示
- **覆盖 FR**：FR-DETAIL-02
- **测试层级/依赖**：IT + VIS
- **Given**：① 文本文件修改的 commit；② 二进制文件（`.slx`/`.mat`）变更的 commit。
- **When**：打开差异 Tab 并选择两个 commit 对比。
- **Then**：① 文本差异逐行显示、新增/删除着色为 token（非硬编码）；② 二进制文件显示“不可预览”提示（`data-binary="true"`），不尝试渲染乱码；③ 差异区域只读（无编辑控件）；④ 大 diff（>5MB）显示截断提示并提供“在外部程序打开”入口；⑤ 差异内容与 `git diff` 命令输出一致（抽查 3 个 hunk）。
- **关联 NFR/OQ**：FR-DETAIL-02；—

#### AC-DETAIL-06 · 文件树：单击选中/双击白名单/危险扩展名二次确认
- **覆盖 FR**：FR-DETAIL-03、FR-SAFE-05
- **测试层级/依赖**：IT + 安全用例（`G-E2E`）
- **Given**：仓内含 `model.slx`、`readme.txt`、`build.bat`、`tool.ps1`、`setup.exe`、`shortcut.lnk`。
- **When**：① 单击 `readme.txt`；② 双击 `readme.txt`；③ 双击 `build.bat`；④ 双击 `setup.exe`。
- **Then**：① 单击仅选中（`aria-selected=true`），不打开；② 双击 `.txt` 直接打开（白名单内）；③ 双击 `.bat`/`.ps1`/`.exe`/`.lnk`/`.msi`/`.scr` 弹二次确认，点“取消”不启动进程（进程监控无新进程）；④ 确认后由系统默认程序打开，进程命令行不含路径拼接注入；⑤ 打开动作写审计（`action` 取 SRS §8.1 `operation_log.action`，同 `job.kind` 集）。
- **关联 NFR/OQ**：FR-SAFE-05、SRS §12.6；—

#### AC-DETAIL-07 · 路径穿越与 root 限定拒绝（`FS-E001`）
- **覆盖 FR**：FR-DETAIL-03、FR-SAFE-05
- **测试层级/依赖**：IT（安全用例）
- **Given**：文件树节点 + 构造恶意输入：`..\..\Windows\System32\cmd.exe`、绝对路径 `C:\Windows\notepad.exe`、UNC 路径 `\\host\share\x`、reparse 指向 root 外。
- **When**：通过 IPC 请求“打开文件/读取文件”传入上述路径。
- **Then**：① 全部被拒绝并返回 `FS-E001`；② 不发生任何文件读取/进程启动；③ 日志记录拒绝事件（不含敏感路径明文泄漏到普通日志，仅结构字段）；④ reparse 越界同样拒绝；⑤ root 内合法文件读取成功（返回内容与磁盘文件逐字节一致）。
- **关联 NFR/OQ**：FR-SAFE-05、FS-E001；—

#### AC-DETAIL-08 · 四仓分支不一致：Impl 为主 + 角标 + 四仓对照
- **覆盖 FR**：FR-DETAIL-04
- **测试层级/依赖**：VIS + IT（`G-E2E` 四仓不同分支）
- **Given**：四仓分支 `main`/`dev_feature`/`main`/`feature/x`。
- **When**：打开看板行与组件详情。
- **Then**：① 看板主行分支显示 Impl 仓分支（`dev_feature`）；② “分支不一致”角标必现（`data-mixed="true"`）；③ hover/详情展示四仓分支对照表，4 行且值准确；④ 提供“对齐分支”动作入口并跳批量 Checkout（参数预填 4 仓）；⑤ 组件聚合态为 `MIXED_BRANCH`（无更高态时）。
- **关联 NFR/OQ**：SRS §11.3、FR-BOARD-01；—

#### AC-DETAIL-09 · 右键菜单（P2）：v1 只断言入口不存在/不可用
- **覆盖 FR**：FR-DETAIL-05（P2，v1.1；v1 只冻结“入口不存在”，见 §4.1 OQ-24）
- **测试层级/依赖**：IT + VIS
- **Given**：v1 构建；文件树节点、提交行、组件行均可见。
- **When**：对上述三类目标分别右键、按 Shift+F10、并检索命令面板。
- **Then**：① v1 无右键上下文菜单（DOM 无 contextmenu 菜单节点、无相关 IPC 命令类型、命令面板无“复制路径/复制 commit/在资源管理器打开”项），不得出现死按钮或空菜单；② 浏览器默认右键菜单被抑制时不出现“功能未实现”类裸提示（避免把 P2 半成品暴露给用户）；③ 本 AC 在 M0 仅冻结 ①/②；④ 完整四动作行为见 §2.12 v1.1 AC（不冻结）。
- **关联 NFR/OQ**：OQ-24；§2.12 v1.1 AC（不冻结）

### 2.7 批量操作（FR-OP）

#### AC-OP-01 · 批量 Clone 成功路径（4/4）与幂等重跑
- **覆盖 FR**：FR-OP-01
- **测试层级/依赖**：E2E（`G-E2E` + 本地 FS）
- **Given**：组织 `MBD` 存在 4 仓 `CF325F_ChanganSigProcgBascFct_{Design,Impl,MIL,SIL}`；`workspace_root` 为空；磁盘余量充足；PAT 有效。
- **When**：看板选中组件 → 批量操作条点击 “Clone” → 弹层显示将 clone 的 4 仓 → 确认入队。
- **Then**：① `job` 行 `kind=CLONE`（SRS §8.1 `job.kind` 合法值），`job_item` 4 行，`job.succeeded=4`/`failed=0`；② 4 个目录 `<root>\<full_name>` 存在且为 git 仓，`origin` URL 无凭据；③ `repo.is_local=1` 且 `local_path` 相符；④ 首扫后 4 仓 `main_status=SYNCED`，组件 `aggregate_status=SYNCED`；⑤ 重复执行同一操作：4 仓已存在 → 不重新 clone，`job_item` 记“已存在/跳过”（`state=SKIPPED`，SRS §8.1 合法值），磁盘上 `.git` 修改时间不变（幂等）。
- **关联 NFR/OQ**：NFR-REL-05、FR-OP-01；—

#### AC-OP-02 · Clone 预检：磁盘/长路径/凭据/远程存在，按仓阻断并汇总
- **覆盖 FR**：FR-OP-01、NFR-REL-05
- **测试层级/依赖**：IT（`G-WIRE-REST`/`G-E2E`、`DISK_SPACE_LOW` 注入）
- **Given**：4 组条件：① 磁盘剩余 < 单仓体积×2；② 目标路径超 240 字符；③ PAT 无效（401）；④ 远程某仓 404。
- **When**：分别发起批量 Clone（预检阶段）。
- **Then**：① **按仓隔离**：预检失败的仓不入队/不创建目录，其余仓继续；整体结果为 `PARTIAL` 而非“任一失败即全部阻断”（本 AC 的“阻断”只针对失败仓自身）；② 场景① 磁盘不足：该仓预检失败，错误码 `E_DISK_FULL`（`details.srsCode=FS-E001`），无目录创建；③ 场景② 超长路径：`E_PATH_TOO_LONG`（`details.srsCode=FS-E001`），提示含路径与字符数；④ 场景③ PAT 401：`E_AUTH_FAILED`（`details.srsCode=NET-E001`），该仓不入队，不产生半成品目录，UI 提供重登入口；⑤ 场景④ 远程 404：`E_REMOTE_NOT_FOUND`（`details.srsCode=NET-E001`），列出 404 仓，其余仓继续；⑥ 预检表每仓一行（可 clone/失败原因），行数=4；磁盘余量通过 `MBD_FAULT_INJECT=DISK_SPACE_LOW`（预检注入点，固定返回 E_DISK_FULL）模拟，不用真实占满磁盘。
- **关联 NFR/OQ**：NFR-REL-05、NFR-COMPAT-03；—

#### AC-OP-03 · Clone 部分失败（2/4）：`OP_PARTIAL_FAILED` + 明细 + 仅重试失败项 + 清除需确认
- **覆盖 FR**：FR-OP-01、FR-OP-08
- **测试层级/依赖**：E2E（`G-E2E` 2 仓 500/403 注入）
- **Given**：4 仓中 `MIL`/`SIL` 在 clone 时被注入 403/网络错误，`Design`/`Impl` 成功。
- **When**：执行批量 Clone → 打开任务中心 → 点“仅重试失败项” → 再点“清除失败态”。
- **Then**：① `job.succeeded=2`、`failed=2`、`job.state=PARTIAL`（SRS §8.1 CHECK 合法值；不得使用其他 job 态名）；② 组件 `aggregate_status=OP_PARTIAL_FAILED`，卡片文案“部分失败 2/4”；③ 任务中心逐仓 4 行，失败行含错误码（附录 D）与可读文案；④ 重试 job 的 `target_json` 仅含 2 个失败仓 `repo_id`；⑤ “清除失败态”需显式确认（弹确认框），确认后 `aggregate_status` 按真实状态重算并写 `operation_log`；⑥ 重跑幂等（已成功 2 仓不再 clone）。
- **关联 NFR/OQ**：OP-E003、FR-OP-08（旧 AC-09 仅映射见 §1.4）；—

#### AC-OP-04 · Clone 错误分类：401/403/404 不重试，网络错误退避 ≤2
- **覆盖 FR**：FR-OP-01
- **测试层级/依赖**：IT（`G-E2E`/WireMock 代理注入）
- **Given**：4 个远程仓分别注入 401、403、404、瞬时网络错误（首次失败、第二次成功）。
- **When**：执行批量 Clone。
- **Then**：① 401/403 仓 `job_item.attempt=1`（不重试），错误码 `E_AUTH_FAILED`（`details.srsCode=NET-E001`）；404 仓 `E_REMOTE_NOT_FOUND`（`details.srsCode=NET-E001`）；错误码表 v0.4 已冻结，R-06 关闭；② 网络错误仓 `attempt=2`（≤2 次重试）后成功；③ 无“无限重试”（attempt 上限断言）；④ 任务中心展示每仓分类错误码与文案（非技术堆栈裸露）；⑤ 失败仓不创建残留目录（预检未通过即不 clone）。
- **关联 NFR/OQ**：FR-OP-01；〔R-06 已关闭：v0.4 附录 D 冻结 `E_AUTH_FAILED`/`E_REMOTE_NOT_FOUND` 并映射 srsCode〕

#### AC-OP-05 · Clone 目标目录已存在：repair 不覆盖

- **覆盖 FR**：FR-OP-01、FR-REPO-05
- **测试层级/依赖**：IT（`F-INVALID-NOTREPO`、`F-INVALID-REMOTE`）
- **Given**：`<root>\A_MIL` 已存在但为非 git 目录；`<root>\A_SIL` 已存在但 origin 不匹配。
- **When**：对组件 A 执行批量 Clone。
- **Then**：① 两目录均**不被覆盖/删除**；`job_item` 记“已存在→转 Repair”；`job_item.state=SKIPPED` 且 message 含“转 Repair”；② UI 显示“转修复流程”并提供 Repair/Re-clone 入口（AC-REPO-08）；③ 原有目录内文件 hash 前后不变；④ 其余 2 仓 clone 成功；⑤ 不产生重复目录（`A_MIL_1`）；⑥ Repair/Re-clone 的零改动/回滚规则按 §16.3。
- **关联 NFR/OQ**：FR-OP-01、FR-REPO-05、§16.3。
#### AC-OP-06 · 批量 Pull（`--ff-only`）成功且无隐式 merge commit
- **覆盖 FR**：FR-OP-02
- **测试层级/依赖**：E2E（`G-E2E` + `F-BEHIND`）
- **Given**：4 仓均为 `BEHIND`（远程领先 2 提交）；预记录各仓 `git rev-list --merges HEAD` 数量。
- **When**：批量 Pull（ff-only）。
- **Then**：① 4 仓 `main_status=SYNCED`；② `git rev-list --merges HEAD` 数量与 Pull 前相同（无新增 merge commit）；③ `job.succeeded=4`；④ 每组 `job_item.pre_head/post_head` 记录且 `post_head` 为远程 tip 的快进结果；⑤ 组件 `aggregate_status=SYNCED`。
- **关联 NFR/OQ**：FR-OP-02；—

#### AC-OP-07 · Pull 非快进被拒绝并转手动提示
- **覆盖 FR**：FR-OP-02
- **测试层级/依赖**：IT（`F-DIVERGED`）
- **Given**：4 仓中 1 仓为 `F-DIVERGED`。
- **When**：批量 Pull。
- **Then**：① 该仓 Pull 被拒，错误码 `E_GIT_NON_FF`（`details.srsCode=GIT-E001`），UI 提示“非快进，请手动处理（查看差异/建分支）”；② 该仓 HEAD 不变（`pre_head==post_head`）；③ 其余 3 仓成功；④ `job.succeeded=3/failed=1`、组件 `OP_PARTIAL_FAILED`；⑤ 无 merge/rebase 中间态残留（无 `MERGE_HEAD`）。
- **关联 NFR/OQ**：FR-OP-02；—

#### AC-OP-08 · 批量 Fetch 成功/失败逐仓报告
- **覆盖 FR**：FR-OP-02
- **测试层级/依赖**：IT（`G-E2E` 部分仓 500）
- **Given**：4 仓中 1 仓 fetch 注入 500，其余 3 仓成功。
- **When**：批量 Fetch。
- **Then**：① `job.succeeded=3/failed=1`；② 失败仓错误码前缀 `GIT`/`NET` + 文案；③ 成功仓 `remote_ref.observed_at` 更新；④ Fetch 不改变工作区（`git status` 前后相同、无 merge）；⑤ 任务中心逐仓可见结果。
- **关联 NFR/OQ**：FR-OP-02；—

#### AC-OP-09 · 批量 Checkout 分支：5 种前置态
- **覆盖 FR**：FR-OP-03
- **测试层级/依赖**：E2E（`F-SYNCED`/`F-BEHIND`/`F-DIRTY`/`F-DETACHED-COMMIT`/`F-UNMERGED`）
- **Given**：5 仓分别为 `SYNCED`、`BEHIND`、`DIRTY`、`DETACHED_COMMIT`、`UNMERGED`；目标分支 `release/1.0` 均存在。
- **When**：批量 Checkout 到 `release/1.0`。
- **Then**：① `SYNCED`/`BEHIND` 仓成功（`head_ref=release/1.0`），BEHIND 仓提示落后；② `DIRTY` 仓阻断（除非用户选 stash，见 AC-OP-10），HEAD 不变；③ `DETACHED_COMMIT` 仓按 7.6 允许但需确认 → 确认后成功；④ `UNMERGED` 仓被拒 `OP-E004`；⑤ `job` 逐仓结果与 7.6 一致；⑥ 分支名非法（`bad..name`）在入队前被 `check-ref-format` 拒绝。
- **关联 NFR/OQ**：FR-OP-03、SRS §7.6；—

#### AC-OP-10 · Checkout 遇 dirty：阻断或按用户选择 stash
- **覆盖 FR**：FR-OP-03、SRS §7.6
- **测试层级/依赖**：IT（`F-DIRTY`）
- **Given**：`F-DIRTY` 仓（1 个 tracked 改动 + 1 个 untracked）。
- **When**：① 预检后选“阻断”（默认）；② 重新发起并选“stash 后切换”；③ 再用 `git stash list` 核对。
- **Then**：① 默认阻断：仓 HEAD 与文件内容不变、无 stash 产生；② stash 选项：切换成功，`stash list` 新增 1 条，切换后工作区干净；③ untracked 文件按选择处理（默认保留）；④ UI 明确显示 stash 名称与恢复入口；⑤ 结果写 `job_item.message` 与审计。
- **关联 NFR/OQ**：FR-OP-03；—

#### AC-OP-11 · 切 Tag：四仓同 Tag 成功、detached 检出、写 `baseline_snapshot` 〔暂定：D-029，不参与 M2b 验收/实现计划〕
- **覆盖 FR**：FR-OP-04、FR-STA-06
- **测试层级/依赖**：E2E（`G-E2E` 四仓均打 `v1.0`）
- **Given**：4 仓均有轻量 tag `v1.0`（指向各自 commit `cD/cI/cM/cS`）。
- **When**：对组件执行“切 Tag → v1.0”。
- **Then**：① 4 仓 `HEAD` 分离（`git status` 显示 detached）且 `git tag --points-at HEAD` 含 `v1.0`；② `repo_status.main_status=DETACHED_TAG`（4 仓）；③ `baseline_snapshot` 写入 4 行 `(component_id,'v1.0',repo_type)`，`commit_sha` 分别为 `cD/cI/cM/cS`、`tag_present=1`；④ 组件 `aggregate_status=DETACHED`（按 §7.4，无 TAG_DRIFT 时）；⑤ 任务中心逐仓结果与 tag 一致。
- **关联 NFR/OQ**：FR-OP-04（旧 AC-10 仅映射见 §1.4）；—

#### AC-OP-12 · 切 Tag 缺 Tag：3 仓检出 + 1 仓跳过报告 + `TAG_DRIFT` 〔暂定：D-029，不参与 M2b 验收/实现计划〕
- **覆盖 FR**：FR-OP-04、FR-STA-06
- **测试层级/依赖**：E2E（`F-TAG_DRIFT` 口径）
- **Given**：4 仓均存在；仅 3 仓（Design/Impl/MIL）有 tag `v1.0`；SIL 仓在默认分支上、无 `v1.0`（`main_status=SYNCED`）。
- **When**：执行“切 Tag → v1.0”。
- **Then**：① 3 仓 `DETACHED_TAG`，1 仓（SIL）不做 detached 检出、`job_item` 记“缺 Tag 跳过”（`state=SKIPPED`，SRS §8.1 合法值，不置失败也不置成功检出）；② 组件 `aggregate_status=TAG_DRIFT`（TAG_DRIFT 优先于 SYNCED；附录 C `F-TAG_DRIFT` 已冻结第 4 仓为 `SYNCED` 而非 MISSING，R-03 关闭）；③ `baseline_snapshot` **固定写 4 行**：SIL 行 `tag_present=0`、`commit_sha=NULL`、`tag_object_sha=NULL`；其余 3 行 `tag_present=1`；写入在 TAG job 收口事务中完成；④ 组件详情逐仓展示 tag 对照，缺 tag 仓显示“无”；⑤ 不产生远端 tag 创建/推送。
- **关联 NFR/OQ**：FR-OP-04、FR-STA-06；R-03 已关闭（附录 C `F-TAG_DRIFT` 第 4 仓固定 SYNCED）

#### AC-OP-13 · Tag 被移动：`fetch --force` 更新 + `tag_moved` 告警 + 禁 `--prune-tags` 〔暂定：D-029，不参与 M2b 验收/实现计划〕
- **覆盖 FR**：FR-OP-04
- **测试层级/依赖**：E2E（`F-TAG-MOVED`、`G-E2E` 移动 tag）
- **Given**：本地 tag `v1.0`→`c1`；远程同名 tag 被删除重建指向 `c2`。
- **When**：执行切 Tag `v1.0`（或手动刷新）。
- **Then**：① 工具以 `fetch --force`（禁止 `--prune-tags`）更新本地 tag 到 `c2`（断言 `git rev-parse v1.0`=`c2`）；② `repo_status.flags_json` 含 `tag_moved`，UI 警告“tag 被移动”；③ 组件 `aggregate_status=TAG_DRIFT`；④ `baseline_snapshot` 按 §8.1 PK `(component_id,tag,repo_type)` **覆盖同键行为新 commit**；旧值必须记录在 `operation_log`（action=TAG，before/after 含旧/新 SHA），不要求 baseline 表保留多版本行；⑤ 其他 tag 未被 prune（本地多余 tag 仍在）。
- **关联 NFR/OQ**：FR-OP-04、§7.3；—

#### AC-OP-14 · annotated tag `^{commit}` 解引用与 `tag_present` 〔暂定：D-029，不参与 M2b 验收/实现计划〕
- **覆盖 FR**：FR-OP-04
- **测试层级/依赖**：UT + E2E（annotated tag 夹具）
- **Given**：annotated tag `v2.0`（tag 对象 sha ≠ commit sha `c3`）；轻量 tag `v1.0`→`c2`。
- **When**：切 Tag 并写快照。
- **Then**：① `baseline_snapshot.commit_sha` 均为 **commit sha**（`v2.0`→`c3`，不是 tag 对象 sha）；② 解引用使用 `^{commit}`（断言 git 命令输出与表值一致）；③ 检出后 `git tag --points-at HEAD` 正确；④ `tag_present=1`；⑤ `baseline_snapshot.tag_object_sha` 与 `commit_sha` 区分（SRS v0.2 §8.1 已补该列，可另存 tag 对象 sha；R-08 关闭）。
- **关联 NFR/OQ**：FR-OP-04、§8.1；—

#### AC-OP-15 · 受控 SYNC 成功（BEHIND）与 DIRTY 前置态：备份 ref→stash -u→确认→`reset --hard`→LFS pull
- **覆盖 FR**：FR-OP-05、SRS §7.6
- **测试层级/依赖**：E2E（`G-E2E` + `F-BEHIND`/`F-DIRTY`）+ UI 快照
- **Given**：`A_Design` 本地落后远程 3 提交且无本地独有提交（`BEHIND`）；`A_Impl` 有 1 个 tracked 改动（`DIRTY`）且无未推送 commit；远程分支 `main` 已 fetch 且 `remote_ref` 新鲜。
- **When**：对组件执行 SYNC 强覆盖（dry-run→确认→入队）：① `A_Design`（BEHIND）；② `A_Impl`（DIRTY），确认框勾选 stash 并输入确认。
- **Then**：① BEHIND 仓按可观测顺序完成：备份 ref `refs/backup/sync/<ts>/<branch>` 写入且 `job_item.backup_ref` 落库 → `reset --hard <remote-ref>` → LFS 仓随后 `lfs pull`；SYNC 后 HEAD=远程 tip、`main_status=SYNCED`，`git rev-parse <backup_ref>`=SYNC 前 HEAD，备份 ref 在 `git for-each-ref refs/backup/sync/` 可见，且未产生 merge commit（禁止 `git pull` 语义以“无 merge commit + HEAD=远程 tip”观测）；② DIRTY 仓：§7.6 DIRTY=`⚠️ stash -u + 确认`——未确认时零写入；确认后 `stash -u` 产生 1 条 stash（`git stash list` 可观测，含 untracked），SYNC 成功且 `main_status=SYNCED`，备份 ref 与 stash 均可恢复；③ **外部改动导致操作失败且零改动**：dry-run 期间由外部进程改动 HEAD 或 index → 入队执行失败并返回 `OP-E002`，仓库 `HEAD`/`git status`/`.git` 树 hash 与失败前一致（CAS 具体三元组属实现机制，不属验收，见 D-008）；④ 每仓 `job_item` 与 `operation_log` 可查；⑤ 任一仓失败不回滚另一仓（部分失败逐仓报告）。
- **关联 NFR/OQ**：FR-OP-05、§7.6、D-008；C-04（DIRTY=⚠️ stash -u+确认后成功）

#### AC-OP-16 · SYNC 保护（DIVERGED）：未推送 commit 丢失清单 + 默认阻断 + 备份 ref + 输入组件 ID 后成功且可恢复
- **覆盖 FR**：FR-OP-05、FR-SAFE-01、SRS §7.6
- **测试层级/依赖**：E2E（`F-DIVERGED` + `F-DIRTY`）
- **Given**：`A_Design` 主状态=`DIVERGED`（§7.2 优先级 8：本地有 2 个未推送 commit，同时远程分支前进 1 个提交，双方非祖先关系），另有 1 个 untracked 文件 + 1 条 stash。
- **When**：发起 SYNC → 查看 dry-run 预览 → 不做确认 → 再确认执行并输入正确组件 ID。
- **Then**：① dry-run 表逐字段断言：未推送 commit 列表（2 条，含短 SHA/作者/message）、untracked 清单（1 个路径）、stash 条数=1、影响仓数=4、将覆盖的仓；② 未点确认前**实际写入操作数=0**（`job` 不入队、无 ref 写入、无文件变化）；③ **默认阻断**：DIVERGED 下 SYNC 不自动合并，UI 提示“将丢失”与“可恢复路径”，确认需 `safety.requireTypedConfirm`（输入组件 ID 正确后才 enabled）；④ 确认后备份 ref `refs/backup/sync/<ts>/<branch>` 与 stash 写入，SYNC 完成后可从 `refs/backup/sync/*` 恢复那 2 个 commit（见 AC-OP-30），`main_status=SYNCED`（§7.6 DIVERGED=`⚠️ 默认阻断，备份 ref + 输入组件 ID 后允许`）；⑤ untracked 默认保留（见 AC-OP-18）。
- **关联 NFR/OQ**：FR-SAFE-01、§7.6；C-04（DIVERGED=⚠️ 默认阻断+备份 ref+输入 ID 后成功）

#### AC-OP-17 · SYNC 跳过表：UNMERGED/IN_PROGRESS/INVALID/EMPTY/DETACHED 逐仓跳过并报错码
- **覆盖 FR**：FR-OP-05、SRS §7.6
- **测试层级/依赖**：IT（`F-UNMERGED`、`F-IN_PROGRESS`、`F-INVALID-REMOTE`、`F-EMPTY`、`F-DETACHED-COMMIT`）
- **Given**：5 仓分别处于上述状态，1 仓 `F-BEHIND` 可同步。
- **When**：对组件执行 SYNC（含 dry-run 与确认）。
- **Then**：① 5 仓在预检表中逐行标记“跳过+原因”，SYNC 不执行 `reset --hard`（`.git` 对象/索引 hash 不变）；② 被跳过仓返回 `OP-E004`；③ 仅 `F-BEHIND` 仓完成 SYNC；④ `job.succeeded=1`、`failed=0`、`job.skipped=5`（SRS v0.2.1 §8.1 已补整数计数列），且被跳过 5 仓逐行 `job_item.state=SKIPPED`（SRS §8.1 合法值）；组件 `OP_PARTIAL_FAILED`（skipped>0 时），清除需显式确认并写审计；⑤ DETACHED 仓：§7.6 中 SYNC=❌，跳过并提示“先建分支”（R-25 已关闭），不做目标分支猜测。
- **关联 NFR/OQ**：FR-OP-05、SRS §7.6；R-25 已关闭（§7.6 DETACHED SYNC=❌）

#### AC-OP-18 · SYNC 的 untracked（SYNCED 前置态）：默认保留且无需确认；显式勾选删除前必须 dry-run + 确认
- **覆盖 FR**：FR-OP-05、SRS §7.6
- **测试层级/依赖**：IT（`F-DIRTY` + untracked 夹具）
- **Given**：仓内有 3 个 untracked 文件与 1 个 untracked 目录，主状态=`SYNCED`（默认 `dirty.includeUntracked=false`，仅 untracked 不升 DIRTY）。
- **When**：① 默认 SYNC；② 勾选“删除 untracked”再 SYNC（先看 dry-run）。
- **Then**：① 默认：SYNC 属“无需确认”路径（§7.6 SYNCED=`—（无需；仅清理 untracked 需确认）`，不弹确认框、不要求输入组件 ID），4 项 untracked 全部保留，`flags_json` 含 `untracked`；② 勾选“删除 untracked”属需确认路径：dry-run 列出将被删除的 4 个路径；③ 未确认时文件仍在、无写入；④ 确认后 4 项被删除且可从备份/stash 恢复策略中得到说明（若不可恢复必须在确认框明示“不可恢复”）；⑤ 两种路径均写审计。
- **关联 NFR/OQ**：FR-OP-05、§7.6、附录 D `dirty.includeUntracked`；C-04（SYNCED=无需，仅清理 untracked 需确认）

#### AC-OP-19 · SYNC 拒绝覆盖本地独有分支 + `UPSTREAM_GONE` 场景
- **覆盖 FR**：FR-OP-05
- **测试层级/依赖**：IT（`F-NO_UPSTREAM-GONE`、本地独有分支夹具）
- **Given**：① 本地分支 `feat/local-only` 无远程同名分支；② `F-NO_UPSTREAM-GONE`（upstream 对应远程分支已删，需 `fetch --prune` 才能检测到）。
- **When**：对含上述分支的仓执行 SYNC 与 Fetch。
- **Then**：① `feat/local-only` 不被覆盖/删除，SYNC 明确拒绝并提示“本地独有分支”；② 场景② fetch 后 `NO_UPSTREAM(UPSTREAM_GONE)` 可被判定（默认 `remote.prune=true`，SRS v0.2 附录 D 已定，R-26 关闭），主状态=NO_UPSTREAM、聚合=NEW_BRANCH；③ SYNC 对 UPSTREAM_GONE 仓跳过；④ 远程删除的分支不在本地被静默删除；⑤ 结果入 `job_item.message`。
- **关联 NFR/OQ**：FR-OP-05、§7.2、附录 D `remote.prune`；R-26 已关闭

#### AC-OP-20 · 本地删除：软删除到 `.trash`、30 天可恢复、realpath 校验
- **覆盖 FR**：FR-OP-06、FR-SAFE-02
- **测试层级/依赖**：E2E（本地仓夹具 + FS）
- **Given**：`F-SYNCED` 与 `F-DIRTY` 两仓位于 `<root>`。
- **When**：① 删除 `F-SYNCED`（干净仓）；② 删除 `F-DIRTY`（默认应阻断）；③ 强制删除 `F-DIRTY`。
- **Then**：① 场景①：`<root>\<full_name>` 不存在，`<root>\.trash\<ts>\<full_name>` 存在且为完整 git 仓（`git log` 可读）；② 场景②被阻断，目录仍在，提示“有未提交改动/未推送提交”；③ 场景③强制后进入 `.trash`，`operation_log` 记录删除（含 `before_json` 含 HEAD/分支）；④ realpath 校验：删除目标必须在 `<root>` 下（构造 `..`/junction 越界拒绝 `FS-E001`）；⑤ 删除**不触碰** Gitea 远端（见 AC-SAFE-04）。
- **关联 NFR/OQ**：FR-OP-06、D-005/D-017；—

#### AC-OP-21 · 删除预检四项阻断（脏/未推送/进行中/stash）
- **覆盖 FR**：FR-OP-06
- **测试层级/依赖**：IT（`F-DIRTY`、`F-AHEAD`、`F-IN_PROGRESS` + stash 夹具）
- **Given**：4 仓分别为：dirty、AHEAD（未推送）、IN_PROGRESS、有 stash。
- **When**：分别发起删除。
- **Then**：① 4 组均默认阻断（`job` 不入队、不产生 `job` 行；SRS §8.1 `job.state` 无 BLOCKED 枚举），目录均保留；② 阻断弹层列出具体原因与“仍要删除”风险说明；③ 勾选“我已理解”并输入组件 ID 后才可强制；④ 强制删除后 4 目录进 `.trash`；⑤ 仅删除本地，远端无变化。
- **关联 NFR/OQ**：FR-OP-06、FR-SAFE-01；—

#### AC-OP-22 · `.trash` 保留期可配（7–90，默认 30）+ 清理任务 + 扫描排除
- **覆盖 FR**：FR-OP-06、D-017
- **测试层级/依赖**：IT（`CLK`、FS）
- **Given**：`.trash` 内含 3 个不同日期的删除记录（今天、T-29d、T-31d）；`delete.retentionDays=30`。
- **When**：推进 `CLK` 触发清理任务（及设置改为 7 后重跑）。
- **Then**：① 默认 30 天：T-31d 记录被清理、今天与 T-29d 保留；② 设置改为 7 后，T-29d 也被清理；③ 非法值（0/91）被拒（设置校验，见 AC-OPS-08）；④ 清理写审计；⑤ 扫描器**不**扫描 `.trash` 内目录（AC-SCAN-01 ⑤）；⑥ `.trash` 体积/条数可在设置或诊断中查看。
- **关联 NFR/OQ**：FR-OP-06、附录 D；—

#### AC-OP-23 · 占用文件导致的删除失败 → `DELETE_PENDING` 保留

- **覆盖 FR**：FR-OP-06、FR-SAFE-02
- **测试层级/依赖**：IT（Windows 文件占用注入 `MBD_FAULT_INJECT=DIR_MOVE_BUSY`）
- **Given**：`A_MIL` 四阻断预检通过，但目录内某文件被外部进程占用，`Directory.Move` 到 `.trash` 失败。
- **When**：执行 DELETE。
- **Then**：① 目录**留在原处**、文件 hash 不变、不递归强删、不删除占用锁；② 生成 `<workspace>/.trash/.pending/<stamp>-<repoName>.json`，字段 `{componentId,repoType,repoName,localPath,errorCode,createdAt}`；③ `job_item.state=FAILED`、`errorCode=E_DELETE_PENDING`（`details.srsCode=FS-E001`）；任务中心显示“待处理（文件占用）”；④ `DELETE_PENDING` 不是 §7.2 主状态/§7.4 聚合态；⑤ 文件占用解除后任务中心“仅重试失败项”只重试 pending 项；重试必须重新 canonicalize 路径、重跑四阻断与工作区边界校验，通过后成功移入 `.trash` 并删除 pending 记录；⑥ 用户可显式取消 pending（删除记录，目录不动）；⑦ 审计 action=DELETE，result=DELETE_PENDING/OK/FAILED 可查。
- **关联 NFR/OQ**：FR-OP-06、AC-OP-22；`E_DELETE_PENDING` 见附录 D。
#### AC-OP-24 · CREATE 分支 dry-run 表字段 + `check-ref-format` 非法输入
- **覆盖 FR**：FR-OP-07
- **测试层级/依赖**：IT（`G-E2E` 四仓）
- **Given**：四仓存在；目标分支名候选：`release/1.0`（合法且不存在）、`bad..name`（非法）、`main`（已存在）、`remote-only`（远程已删分支名）。
- **When**：分别输入 4 个名字并进入 dry-run 预览。
- **Then**：① 非法名 `bad..name` 在输入框即时被 `check-ref-format` 拒绝（按钮禁用 + `FS-E001` 类提示），不发请求；② `main` 行标“已存在/重名”，`release/1.0` 行标可创建；③ dry-run 表字段逐列断言：重名/远程已删/超前落后/非法/各仓状态/基线 ref；④ `remote-only` 行标“远程已删”并给处置选择；⑤ 表行数=4 仓（每仓一行）。
- **关联 NFR/OQ**：FR-OP-07（旧 AC-17 仅映射见 §1.4）；—

#### AC-OP-25 · CREATE 逐仓创建 + 1 仓重名预览 + 撤销已创建 + 基线 ref 记录

- **覆盖 FR**：FR-OP-07
- **测试层级/依赖**：E2E（`G-E2E` + 1 仓预置同名分支）
- **Given**：3 仓无 `release/1.0`，1 仓（MIL）已有同名分支。
- **When**：dry-run 后创建；再调用 `undoCreate { jobId }`。
- **Then**：① dry-run 中 MIL 行高亮“重名”，其余行显示远程存在性/基线 HEAD/非法名提示；② 创建后 3 仓存在 `release/1.0`，`job_item.baseline_ref=refs/baseline/create/<ts>/<branch>/<TYPE>`，MIL 原 HEAD 不变；③ 撤销：仅当分支仍指向 baseline_ref、未被检出、无新提交时删除；满足的仓 `git branch -D`，不满足的仓 SKIPPED+原因；部分成功逐仓报告；④ 撤销写 journal STARTED/DONE/FAILED 与审计 `CREATE_UNDO`，不删远端；⑤ 基线 ref 保留 30 天，job 历史 200 条；⑥ 已切走/有新提交/分支不存在均不可撤销且零改动。
- **关联 NFR/OQ**：FR-OP-07、§16.5；—

#### AC-OP-26 · 任务中心：队列/进度/持久化/失败重试/重启不重复执行

- **覆盖 FR**：FR-OP-08
- **测试层级/依赖**：IT（`MBD_FAULT_INJECT=CRASH_AT_ITEM_2` 确定性终止；任务 DB/JSON）
- **Given**：16 项批量 FETCH，用故障注入在第 2 项执行前终止 Host 进程（不靠墙钟/人工时机）。
- **When**：重启 Host，打开任务中心。
- **Then**：① 重启后 job 状态为 `INTERRUPTED`，job_item 为 `INTERRUPTED`；未开始 item 保持 `QUEUED` 且不重复执行；② 已成功项保持 OK，当前项记 `INTERRUPTED`；③ 失败项可“仅重试失败项”，新 job 的 target 只含失败 repo_id；④ 任务中心逐项显示状态/错误码；⑤ 不产生重复目录/重复 ref；⑥ 无需等待真实 kill，注入点固定即可复现。
- **关联 NFR/OQ**：FR-OP-08、AC-OP-03、FR-SAFE-04；确定性注入见 §16.6。
#### AC-OP-27 · 取消语义：合作式取消、`CANCELLED` 逐仓、未开始保持原状、触发重扫

- **覆盖 FR**：FR-OP-08、SRS §7.6
- **测试层级/依赖**：IT（`MBD_FAULT_INJECT=GIT_SLOW` + `CRASH_AT_ITEM_2`；状态机断言，不以墙钟为唯一断言）
- **Given**：16 项批量 FETCH 正在执行，第 1 项完成、第 2 项已开始（`GIT_SLOW` 注入使其可取消）、其余 QUEUED。
- **When**：调用 `cancelJob`。
- **Then**：① job 进入 CANCELLED；第 1 项 OK 保留；第 2 项合作式取消后 `CANCELLED`；未开始项记 `QUEUED`（不执行任何 git 写），job_item 枚举固定为 QUEUED/CANCELLED；② 取消不整体回滚已完成项；③ 取消后触发重扫并更新看板；④ 不支持取消的 job kind（纯只读/已结束）返回 `E_ACTION_NOT_ALLOWED`；⑤ UI 在注入命中后的下一个 `JOB_UPDATED` 事件内更新（状态机断言，不写“≤2s”死断言）。
- **关联 NFR/OQ**：FR-OP-08、AC-UI-10c、§7.6。
#### AC-OP-28 · 操作审计：字段齐全、可查询导出、保留 ≥90 天
- **覆盖 FR**：FR-OP-09
- **测试层级/依赖**：IT（`CLK`、DB 查询）
- **Given**：执行 1 次 SYNC、1 次删除、1 次恢复。
- **When**：查询 `operation_log` 并按时间导出。
- **Then**：① 每次写操作产生记录，字段齐全：`ts`（UTC ISO-8601）、`actor`、`action`、`target_json`（组件/仓集合）、`before_json`/`after_json`（HEAD/分支）、`result`、`error_code`（空或码）、`request_id`；② 审计可查询、可导出（CSV/JSON 至少 1 种），导出文件脱敏；③ 保留策略 `audit.keepDays=90` 生效：`CLK` 推进 91 天后旧记录被清理；④ 审计 sink 与普通日志分离；⑤ 恢复/取消等操作同样入审计。
- **关联 NFR/OQ**：FR-OP-09、NFR-REL-03；—

#### AC-OP-29 · 操作互斥：同组件串行、外部 `index.lock` → BUSY
- **覆盖 FR**：FR-OP-10
- **测试层级/依赖**：IT + E2E
- **Given**：对同一组件同时发起 2 个写操作（Pull 与 Checkout）。
- **When**：并发提交两个 job → 再对 1 仓制造外部 `index.lock` 后发起 Commit。
- **Then**：① 同组件两 job 串行执行（`job_item.started_at`/`finished_at` 为 SRS v0.2.1 补列；第二 job 的 `started_at` ≥ 第一的 `finished_at`），无交错写入；② 外部锁场景返回 `OP-E001`、`job_item.state=FAILED`、仓库零改动、锁文件保留；③ 不同组件可并行（断言可并行，受并发上限约束）；④ 无数据损坏（DB `quick_check`=ok、仓库 `git fsck` 无新增错误）；⑤ 不删锁、不重试。
- **关联 NFR/OQ**：FR-OP-10、OP-E001（旧 AC-26 仅映射见 §1.4）；R-20 已关闭（v0.2.1 补 `job_item` 时间列）

#### AC-OP-30 · 恢复：`.trash` 还原 + `refs/backup/*` 恢复 + 冲突预览 + 审计
- **覆盖 FR**：FR-OP-11、FR-OP-06、SRS §7.6
- **测试层级/依赖**：E2E（AC-OP-20/16 产物）
- **Given**：① 被删除到 `.trash/<ts>/A_Design`；② SYNC 产生 `refs/backup/sync/<ts>/main`（含 2 个未推送 commit）。
- **When**：① 任务中心→恢复→选择 `.trash` 记录→确认；② 组件详情→恢复→选择备份 ref→确认。
- **Then**：① 场景① 目录回到 `<root>\A_Design`，`git log` 与删除前一致，`.trash` 中该记录被移除，`operation_log` 记恢复；② 场景② 恢复后本地分支包含那 2 个 commit（`git rev-parse` 对比备份 ref）；③ 恢复前展示冲突：目标已存在/远程已变动/备份 ref 已过期均提示并需确认；④ 冲突时默认不覆盖、可取消；⑤ 恢复不写远端（Gitea 零写请求）；⑥ **备份 ref 生命周期（D-017/H-05）**：SYNC 产生的 `refs/backup/sync/*` 在保留期内不被清理（默认 30 天：`CLK` 推进 29 天仍存在、推进 31 天由清理任务回收），任务中心可见且可恢复；保留期键=`backup.retentionDays`（SRS v0.2.1 附录 D：默认 30、范围 7–90、即时生效）。
- **关联 NFR/OQ**：FR-OP-11、D-017（旧 AC-19 仅映射见 §1.4）；H-05（备份 ref 保留 ≥30 天）

#### AC-OP-31 · 组件纳入：按组件 ID 校验四仓并展示将 clone 的仓

- **覆盖 FR**：FR-OP-12
- **测试层级/依赖**：E2E（`G-E2E`）
- **Given**：组织有组件 `NEWC` 四仓，本地未克隆；另有既有扫描组件 `OLD`（用来验证默认迁移）。
- **When**：在“纳入组件”输入 `NEWC` → 校验 → 确认纳入；再检查 `OLD`。
- **Then**：① 校验显示 4 仓齐全及将 clone 的仓清单（4 行 full_name）；② 纳入后看板出现组件（计数 +1），`component` 行写入，`is_managed=1/is_deleted=0`；③ 既有扫描组件 `OLD` 升级/启动后默认 `is_managed=1`，不被隐藏；④ 输入不存在 ID → 明确“远程不存在”，不写库；⑤ 缺仓 → 列出缺失仓并标 `BINDING_ERROR`，仍允许纳入（D-015）；⑥ 远程不可用时 fail-closed，不写库；⑦ 审计 `COMPONENT_BIND`，远端零写。
- **关联 NFR/OQ**：FR-OP-12、§16.1；R-27 已关闭。
#### AC-OP-32 · 组件移出：本地删除 vs 仅停止管理，看板/DB 一致 + 审计

- **覆盖 FR**：FR-OP-12
- **测试层级/依赖**：E2E（真实 DELETE job 语义）
- **Given**：组件 `A`（4 仓已克隆、clean、无未推送）。
- **When**：① 仅停止管理；② 重新纳入后选择本地删除（输入组件 ID 强确认）；③ 再模拟一仓删除失败。
- **Then**：① 停止管理：`is_managed=0`，`component`/`repo` 行保留，本地目录保留，默认视图过滤、“已移出”可见，重新纳入恢复；② 本地删除：复用 DELETE job（confirm+typed componentId+四阻断+`.trash`），仅四仓全部成功/已不存在时置 `is_deleted=1`；`repo.is_local` 由下一次扫描置 0；目录进 `.trash`，看板默认隐藏；③ 一仓失败 → 整体 `PARTIAL`，组件**不**置已移出，列出失败仓并可重试；④ 两模式均写审计 `COMPONENT_UNBIND`（mode/逐仓结果），远端零变更；⑤ 不提供强制删除；⑥ 重新纳入恢复显示且 DB 一致。
- **关联 NFR/OQ**：FR-OP-12、§16.1；R-27 已关闭。
#### AC-OP-34 · M2b 证据门禁：SKIP 不计 COVERED、真实证据可追溯

- **覆盖 FR**：FR-OP-08、FR-OPS-09、§16.7
- **测试层级/依赖**：过程/证据审计（测试 runner + 证据 manifest）
- **Given**：M2b 候选构建；可运行 L1 本地/CI 套件；L2 共享 Gitea 可能不可达。
- **When**：运行 `ui/scripts/run-selfchecks.mjs` 与 `dotnet test`，生成证据 manifest。
- **Then**：① runner 输出区分 PASS/FAIL/SKIP；SKIP 单独计数且**不得**写成 PASS；② `result.json` 至少含 `{target,url,reachable,skipped,commit,timestamp,checks}`；③ DoD 统计只把 PASS 计入 COVERED；④ 至少一份 L2 或 hermetic Gitea 运行记录归档（命令、时间、commit、输出摘要）；⑤ 只有 SKIP 时不得标记 M2b final。
- **关联 NFR/OQ**：§16.7、§16.9。

#### AC-OP-35 · 故障注入可复现且安全降级

- **覆盖 FR**：FR-SAFE-01…04、§16.6
- **测试层级/依赖**：IT（`MBD_FAULT_INJECT` 注入矩阵 + 审计/仓库快照）
- **Given**：Debug/Test 构建，注入点枚举：`GIT_NET_AUTH_401/GIT_NET_404/GIT_NET_TIMEOUT/INDEX_LOCK/DIR_MOVE_BUSY/DISK_WRITE_FAIL/DISK_SPACE_LOW/GIT_SLOW/CRASH_BEFORE_WRITE/CRASH_AFTER_WRITE/CRASH_AT_ITEM_2`。
- **When**：逐点注入并执行对应操作；Release 构建重复一次。
- **Then**：① 每个注入点产生确定性错误码/中断，不产生半写/误删/误推；② 仓库/DB/refs 与失败前一致（除显式 pending/journal 记录）；③ Release 忽略注入并正常执行（日志告警）；④ 报告 `docs/m2b-fault-injection-report.md` 逐点记录期望/实测/证据；⑤ AC-SAFE-05/10/11 等非注入类标 N/A 并说明。
- **关联 NFR/OQ**：§16.6、§16.9。
### 2.8 单仓操作（FR-REPO）

#### AC-REPO-01 · 单仓 Fetch：结果与命令行一致
- **覆盖 FR**：FR-REPO-01
- **测试层级/依赖**：IT（`G-E2E` + `F-BEHIND`）
- **Given**：`A_Design` 远程领先 2 提交、本地 `remote_ref` 旧。
- **When**：单仓详情点“Fetch”。
- **Then**：① `remote_ref` 更新到新 tip（`observed_at` 前进）；② 与手工 `git fetch` 后 `git rev-parse origin/main` 结果一致；③ 不改变本地 HEAD 与工作区（`git status` 前后相同）；④ 操作前自动重扫（`scan_at` 前进）；⑤ 结果入任务中心/状态栏（单仓操作即时反馈）。
- **关联 NFR/OQ**：FR-REPO-01、NFR-PERF-08；—

#### AC-REPO-02 · 单仓 Pull（ff-only）：成功后无 merge，非快进拒绝
- **覆盖 FR**：FR-REPO-01
- **测试层级/依赖**：IT（`F-BEHIND`、`F-DIVERGED`）
- **Given**：`F-BEHIND`（可快进）与 `F-DIVERGED`（非快进）两仓。
- **When**：分别点“Pull”。
- **Then**：① 快进仓：HEAD 前进到远程 tip、`main_status=SYNCED`、`git rev-list --merges` 不增、无 merge commit；② 分叉仓：被拒并提示“非快进，请手动处理”，HEAD 不变、无 `MERGE_HEAD`；③ 两场景操作前均自动重扫；④ 失败时错误码与文案可见。
- **关联 NFR/OQ**：FR-REPO-01；—

#### AC-REPO-03 · 单仓 Checkout 分支/Tag：dirty 规则一致
- **覆盖 FR**：FR-REPO-01
- **测试层级/依赖**：IT（`F-DIRTY`、`F-SYNCED`）
- **Given**：干净仓、dirty 仓各 1。
- **When**：① 干净仓 Checkout 分支 `dev`；② 干净仓 Checkout tag `v1.0`；③ dirty 仓 Checkout 分支。
- **Then**：① 分支切换成功 `head_ref=dev`；② tag 检出为 detached 且 `main_status=DETACHED_TAG`；③ dirty 仓按 7.6 阻断（或需 stash 确认，与 AC-OP-10 同规则）；④ 与 §7.6 矩阵一致；⑤ 失败仓库零改动。
- **关联 NFR/OQ**：FR-REPO-01、SRS §7.6；—

#### AC-REPO-04 · Push 成功（有 upstream）
- **覆盖 FR**：FR-REPO-02
- **测试层级/依赖**：E2E（`G-E2E` + `F-AHEAD`）
- **Given**：`F-AHEAD` 仓（上游分支存在、本地领先 2 提交、有 `write:repository` 权限）。
- **When**：单仓详情点“Push”。
- **Then**：① Push 成功，远程分支 tip=本地 HEAD；② 本地 `main_status=SYNCED`（重扫后）；③ 组件 `aggregate_status` 按新状态重算；④ 任务/操作记录写审计（`action` 含 push）；⑤ 只推当前分支（其他分支远端不变）。
- **关联 NFR/OQ**：FR-REPO-02；—

#### AC-REPO-05 · Push 四类失败面：无 upstream / detached / 分支保护 / 网络
- **覆盖 FR**：FR-REPO-02
- **测试层级/依赖**：E2E（`G-E2E` 保护分支 + 本地夹具）
- **Given**：① 无 upstream 的分支；② detached HEAD 仓；③ 远程受保护分支（服务端拒绝）；④ 断网。
- **When**：分别点“Push”。
- **Then**：① 无 upstream：提示“发布分支”，提供 set-upstream 引导（FR-REPO-05），不静默推送；② detached：禁止 Push 并提示“基于当前 HEAD 建分支”（FR-REPO-05）；③ 保护分支：展示**服务端原文消息**（断言 UI 含服务端返回字符串），不吞错；④ 断网：`NET-E001`，本地零改动；⑤ 四场景均不产生 force push。
- **关联 NFR/OQ**：FR-REPO-02；—

#### AC-REPO-06 · Commit（最小化）：提交 staged 变更；unmerged 时禁止
- **覆盖 FR**：FR-REPO-03
- **测试层级/依赖**：IT（`F-DIRTY`、`F-UNMERGED`）
- **Given**：① 仓有 1 个已 staged 文件 + 1 个 unstaged 文件；② `F-UNMERGED` 仓。
- **When**：① 输入 message 点 Commit；② 对 unmerged 仓点 Commit。
- **Then**：① 场景① 仅 staged 文件被提交（`git show --stat` 只含 1 文件），成功后 `main_status` 转为 `AHEAD`（测试不执行 push）；② message 与输入一致；③ 场景② 被拒并提示“存在未解决冲突”，`git status` 不变；④ 不提供冲突解决/rebase UI（页面无相关控件断言）；⑤ 提交写审计。
- **关联 NFR/OQ**：FR-REPO-03；—

#### AC-REPO-07 · Reset（v1.1）：v1 不暴露入口
- **覆盖 FR**：FR-REPO-04（P2，v1.1）
- **测试层级/依赖**：VIS + IT
- **Given**：v1 构建。
- **When**：在单仓详情与命令面板搜索“Reset”。
- **Then**：① v1 无 Reset 入口/命令（DOM 与 IPC 均无 `RESET` 类型），不得出现死按钮；② 若通过实验开关暴露，必须先备份 ref 再执行（v1.1 验收口径：备份 ref 存在且可恢复，`refs/backup/reset/<ts>/<branch>`）；③ 本 AC 在 M0 仅冻结 ①，② 随 v1.1 冻结。
- **关联 NFR/OQ**：FR-REPO-04；§4.1 OQ-24（版本归属）

#### AC-REPO-08 · INVALID 修复：Repair / Re-clone
- **覆盖 FR**：FR-REPO-05、SRS §7.6
- **测试层级/依赖**：IT（`F-INVALID-NOTREPO`、`F-INVALID-REMOTE`）
- **Given**：① 目录非 git 仓（`F-INVALID-NOTREPO`）；② 是 git 仓但 origin 不匹配（`F-INVALID-REMOTE`）。
- **When**：分别执行 Repair（①）/ Re-clone（②，含备份现有目录到 `.trash`）。
- **Then**：① 场景① Repair 后目录成为合法仓且 origin 匹配，`main_status` 脱离 INVALID（→ SYNCED/EMPTY 等真实态）；② 场景② Re-clone 先备份原目录进 `.trash` 再 clone，成功后由下一次 LOCAL_SCAN 置 `is_local=1`、`main_status` 正确；③ 两场景均走预检+job+逐仓结果；④ 组件 `BINDING_ERROR` 在修复后按真实状态重算（若四仓齐则脱离 BINDING_ERROR）；⑤ 审计记录 repair/re-clone；⑥ **失败路径**：REPAIR/RECLONE 任一步失败 → 目录/HEAD/远程零改动（按 §16.3 回滚），`job_item=FAILED` + `E_REPAIR_FAILED`/`E_RECLONE_FAILED`（附录 D D.2）；REPAIR 备份保留在 `.trash` 30 天，失败后可人工恢复；⑦ Re-clone 前必须通过 clean/未推送/untracked/stash 预检，否则拒绝。
- **关联 NFR/OQ**：FR-REPO-05（旧 AC-18 仅映射见 §1.4）；—

#### AC-REPO-09 · `NO_UPSTREAM` 三子型修复：set-upstream / 发布分支
- **覆盖 FR**：FR-REPO-05
- **测试层级/依赖**：E2E（`F-NO_UPSTREAM-LOCAL/UNSET/GONE`）
- **Given**：三仓分别处于 `LOCAL_ONLY`（远程无同名分支）、`UNSET_BUT_EXISTS`（远程有但未设 tracking）、`UPSTREAM_GONE`（远程已删）。
- **When**：分别执行修复动作。
- **Then**：① `LOCAL_ONLY`：引导“发布分支”（push -u 建 upstream），成功后 `main_status` 重新计算；② `UNSET_BUT_EXISTS`：`git branch --set-upstream-to` 成功，`upstream_ref` 写入，主状态变为关系态（BEHIND/AHEAD/SYNCED）；③ `UPSTREAM_GONE`：引导改名/新建远程分支或清除 upstream，处理后不再显示该子型；④ 三场景均走预检+job+逐仓结果；⑤ 修复失败时仓库零改动。
- **关联 NFR/OQ**：FR-REPO-05、§7.2 NO_UPSTREAM 三子型；—

#### AC-REPO-10 · DETACHED（tag/commit）基于当前 HEAD 建分支
- **覆盖 FR**：FR-REPO-05
- **测试层级/依赖**：IT（`F-DETACHED-TAG`、`F-DETACHED-COMMIT`）
- **Given**：两仓分别 detached 于 tag 与游离 commit；当前 HEAD `h1`。
- **When**：执行“基于当前 HEAD 建分支”（分支名 `baseline/v1.0`）→ 再切回原分支。
- **Then**：① 新分支 `baseline/v1.0` 指向 `h1`（`git rev-parse` 断言）；② 检出新分支后 `head_type`=分支、`main_status` 重新计算（如 `AHEAD`/`NO_UPSTREAM`）；③ 分支名合法性校验同 AC-OP-24；④ 失败时 HEAD 仍为 detached、零改动；⑤ 审计记录。
- **关联 NFR/OQ**：FR-REPO-05（旧 AC-18 仅映射见 §1.4）；—

#### AC-REPO-11 · `EMPTY` 空仓首次推送并建 upstream
- **覆盖 FR**：FR-REPO-05、SRS §7.6
- **测试层级/依赖**：E2E（`F-EMPTY` + `G-E2E` 空仓）
- **Given**：`F-EMPTY`（unborn HEAD）仓，本地有首个 commit 待推。
- **When**：点“初始化/推送首个 commit”。
- **Then**：① 首个 commit 推送成功并建立 upstream（`upstream_ref` 非空）；② `main_status` 由 `EMPTY` 变为 `SYNCED`；③ 远程分支出现且 tip 一致；④ 该操作不被 FR-REPO-02“需有 upstream”误拦（SRS v0.2 FR-REPO-02 已加 EMPTY 首推例外，R-05 关闭）；⑤ 审计记录。
- **关联 NFR/OQ**：FR-REPO-05、GIT-E001；R-05 已关闭（SRS v0.2 FR-REPO-02 例外）

### 2.9 破坏性操作与安全（FR-SAFE）

#### AC-SAFE-01 · dry-run“将丢失什么”字段完整
- **覆盖 FR**：FR-SAFE-01
- **测试层级/依赖**：VIS + IT（`F-AHEAD`+`F-DIRTY`+untracked/stash）
- **Given**：待 SYNC 的组件含 3 个未推送 commit、4 个 untracked、2 条 stash；待删除组件含 dirty 仓。
- **When**：分别打开 SYNC dry-run 与 Delete 预检。
- **Then**：① 预览表逐字段断言：commit 列表（短 SHA+作者+message，条数=3）、untracked 清单（路径数=4）、stash 条数=2、影响仓数、将被覆盖的分支/tag；② 删除预检列出 dirty 仓数与未推送数（与夹具一致）；③ 预览为只读表（无编辑控件）；④ 表数据来自写前 read-through（非 DB 缓存）：在预览打开后外部修改仓库，点“刷新预览”数据变化；⑤ 默认动作=阻断（确认按钮非默认聚焦）。
- **关联 NFR/OQ**：FR-SAFE-01（旧 AC-07 仅映射见 §1.4）；—

#### AC-SAFE-02 · 未确认时写入操作数=0（默认阻断）
- **覆盖 FR**：FR-SAFE-01、SRS §7.6
- **测试层级/依赖**：IT（DB+FS 快照）
- **Given**：预览已展示，记录 DB 全表行数与仓库 `.git`/工作区 hash 快照。
- **When**：① 点“取消”；② 直接关闭弹层；③ 按 Esc。
- **Then**：① 三种退出方式后 `job`/`job_item` 行数不增；② 仓库 `.git` 与工作区快照不变；③ 无 `refs/backup/*` 产生；④ 无审计“已执行”记录（最多记录“取消”）；⑤ 再打开预览结果仍为新快照。
- **关联 NFR/OQ**：FR-SAFE-01；—

#### AC-SAFE-03 · 强制覆盖需输入组件 ID（`safety.requireTypedConfirm=true`）
- **覆盖 FR**：FR-SAFE-01
- **测试层级/依赖**：IT + VIS
- **Given**：`safety.requireTypedConfirm=true`；组件 `CF325F_ChanganSigProcgBascFct` 的 SYNC 强覆盖预览。
- **When**：① 不输入直接点“确认”；② 输入错误 ID；③ 输入正确 ID。
- **Then**：① ①② 确认按钮 `disabled`/点击无效，无任何写入；② ③ 才入队执行；③ 输入框匹配规则（SRS v0.2.1 附录 D 冻结）：**精确匹配、区分大小写、忽略首尾空格**——`cf325f_…`（小写）不通过、` CF325F_ChanganSigProcgBascFct `（首尾空格）通过、多/少字符不通过；④ 设置改为 `false` 后不再要求输入但仍有二次确认；⑤ 确认操作写审计（含确认方式）。
- **关联 NFR/OQ**：FR-SAFE-01、附录 D `safety.requireTypedConfirm`；R-28 已关闭（SRS v0.2.1：精确匹配/区分大小写/忽略首尾空格）

#### AC-SAFE-04 · 删除仅本地：固定文案 + 远端 refs 零变更
- **覆盖 FR**：FR-SAFE-02
- **测试层级/依赖**：IT（`G-WIRE-GIT` 代理 + 远端 refs 快照）
- **Given**：git 网络由 `G-WIRE-GIT` 代理记录；执行 AC-OP-21 的强制删除；记录远端 refs/reflog 快照。
- **When**：删除确认弹层与执行全过程。
- **Then**：① 弹层固定文案含“远程仓库不受影响”（文案快照逐字断言）；② 远端 refs/reflog 与删除前逐字节一致；`G-WIRE-GIT` 记录中无 push/无 force（git smart-HTTP 的 POST 读不视为远端写，以 refs 不变为准）；③ 远端仓仍存在（`G-E2E` 侧断言）；④ 本地目录进 `.trash`；⑤ 审计只记本地删除。
- **关联 NFR/OQ**：FR-SAFE-02、D-005（旧 AC-08 仅映射见 §1.4）；—

#### AC-SAFE-05 · 日志脱敏规则单测（五类模式）
- **覆盖 FR**：FR-SAFE-03
- **测试层级/依赖**：UT（脱敏函数）+ IT
- **Given**：样本串：`Authorization: Bearer abc123`、`Authorization: token abc123`、URL `https://user:pass@gitea/x`、`?token=abc123&x=1`、`access_token=abc123`、git stderr 含 `remote: invalid token abc123`、`git remote -v` 输出含凭据 URL。
- **When**：经脱敏函数处理后写日志。
- **Then**：① 五类模式全部被替换（输出不含 `abc123`/`pass`，保留结构占位如 `[REDACTED]`）；② 非敏感字段（URL 主机、用户名、repo 名）保留；③ 脱敏函数幂等（二次处理结果不变）；④ 日志落盘文件扫描无 `abc123`；⑤ 单测覆盖正/反例各 ≥1。
- **关联 NFR/OQ**：FR-SAFE-03、SRS §10.3；—

#### AC-SAFE-06 · 诊断包：必需项齐全 + 导出前自动脱敏
- **覆盖 FR**：FR-SAFE-03、FR-OPS-02
- **测试层级/依赖**：IT（导出包解压检查）
- **Given**：已产生日志、1 次同步、1 次任务、DB 有数据；token 哨兵 `ghp_CANARY_9f8e7d6c5b4a`。
- **When**：一键导出诊断包。
- **Then**：① 包内必需项：脱敏配置、进程状态、最近任务（≥50 条）、DB 版本（`PRAGMA user_version` + `sqlite_version()`）、转储清单、日志、`VACUUM INTO` DB 快照、`git --version`、OS/WebView2 版本（SRS v0.2 FR-OPS-02 已改为引用 §12.5 清单，R-14 关闭）；② 全包扫描哨兵出现次数=0；③ 包可完整解压且文件非空；④ 导出动作写审计；⑤ 导出路径用户可选、默认 `diag\`。
- **关联 NFR/OQ**：FR-SAFE-03、FR-OPS-02；R-14 已关闭（FR-OPS-02 引用 §12.5）

#### AC-SAFE-07 · 崩溃转储：默认关闭或导出前脱敏

- **覆盖 FR**：FR-SAFE-03、SRS §10.3
- **测试层级/依赖**：IT（`MBD_FAULT_INJECT=CRASH_BEFORE_WRITE` 确定性崩溃 + 转储扫描）
- **Given**：生产默认配置；`dumps` 根固定为 `%LOCALAPPDATA%\MbdGitTool\dumps\`（不得落在工作区/仓库）。
- **When**：触发一次确定性崩溃（注入点），检查转储策略/内容。
- **Then**：① **v1 唯一口径：默认关闭**（环境变量/构建开关控制，不新增设置键）；关闭时 `dumps\` 不生成新文件；② 测试显式开启：仅保留最近 **8** 个转储且总大小 ≤256MB；③ 转储/诊断包内明文 token 哨兵扫描=0；④ 诊断包“转储清单”与实际文件一致；⑤ 路径断言：所有转储均在上述根目录内；⑥ 设置页显示“默认关闭/上限”文案。两分支不再并存，按配置断言。
- **关联 NFR/OQ**：FR-SAFE-03、SRS §10.3；—

#### AC-SAFE-08 · 写前 journal：pre/expect/post 三元组落 `job_item`
- **覆盖 FR**：FR-SAFE-04
- **测试层级/依赖**：IT（DB 查询 + 写操作）
- **Given**：对 1 仓执行 Pull（快进场景）。
- **When**：执行前/中/后查询 `job_item`。
- **Then**：① 写操作**开始前** `job_item.pre_head` 已落库（与执行时 HEAD 一致）；② 执行前记录 `expect_head`（如 CAS 期望值）；③ 完成后 `post_head` 落库；④ 三者与 git 命令输出一致；⑤ 若执行中 HEAD 被外部改变，操作失败 `OP-E002`，`job_item.state=FAILED` 且不写“成功”（只断言“外部改动→失败+零改动”，CAS 具体三元组不属验收）。
- **关联 NFR/OQ**：FR-SAFE-04、SRS §7.5 写前 read-through；—

#### AC-SAFE-09 · 崩溃后 reconcile：不猜测成功

- **覆盖 FR**：FR-SAFE-04、NFR-REL-03
- **测试层级/依赖**：IT（`MBD_FAULT_INJECT=CRASH_AT_ITEM_2` 确定性终止；journal/审计/任务态）
- **Given**：批量 SYNC 执行到第 2 仓时，由注入点在“写前 STARTED 已落盘、git 写尚未发生”处终止进程；不依赖墙钟或人工时机。
- **When**：重启应用触发启动 reconcile。
- **Then**：① 第 1 仓（已完成）`job_item.state=OK` 且 `post_head` 有值；② 第 2 仓 reconcile 判定：HEAD==preHead → `NOT_APPLIED`；HEAD 变化 → `UNKNOWN_APPLIED`；**不得标成功**；job=`INTERRUPTED`，item=`INTERRUPTED`；③ 未开始仓保持原状；④ reconcile 只读、不修改仓库；写 1 行 `action=RECONCILE` 审计；⑤ 用户可见“上次任务被中断”通知并可重试失败项。注入点 `CRASH_AFTER_WRITE` 另测 UNKNOWN_APPLIED。
- **关联 NFR/OQ**：FR-SAFE-04、NFR-REL-03；R-20 已关闭（v0.2.1 补 `job_item` 中断态/时间列）
#### AC-SAFE-10 · 危险扩展名二次确认（与文件树联动）
- **覆盖 FR**：FR-SAFE-05、FR-DETAIL-03
- **测试层级/依赖**：IT + 安全用例
- **Given**：文件树含 `.exe/.bat/.cmd/.ps1/.lnk/.msi/.scr` 各 1。
- **When**：逐个双击。
- **Then**：① 7 类全部弹二次确认（`data-danger-ext` 标识扩展名）；② 点“取消”后进程监控无新进程、无新增审计“已打开”；③ 点“确认”后由系统默认打开且审计记录；④ SRS v0.2.1 白名单内的 12 类（`.m/.c/.h/.slx/.mdl/.json/.xml/.yaml/.yml/.txt/.md/.csv`）双击直开、不弹确认；⑤ 白名单外非危险扩展名与危险 7 类（`.exe/.bat/.cmd/.ps1/.lnk/.msi/.scr`）一律二次确认（与 FR-DETAIL-03 逐项一致，R-30 关闭）。
- **关联 NFR/OQ**：FR-SAFE-05；R-30 已关闭（SRS v0.2.1 FR-DETAIL-03 已列白名单 12 类）

#### AC-SAFE-11 · 路径穿越与 reparse 越界拒绝
- **覆盖 FR**：FR-SAFE-05
- **测试层级/依赖**：IT（恶意路径夹具）
- **Given**：恶意输入集：`..\..\x`、`C:\Windows\x`、`\\host\share\x`、保留名 `CON`、尾随点/空格名、大小写冲突路径、junction 指向 root 外。
- **When**：通过 删除/打开/写入 接口传入。
- **Then**：① 全部拒绝并返回 `FS-E001`；② 无文件创建/删除/读取发生（FS 快照不变）；③ 日志记录拒绝且不泄漏完整敏感路径；④ root 内合法路径的读/打开操作成功（返回成功态与文件内容）；⑤ reparse 越界时 `reparse_point` flag 与拒绝码同时出现。
- **关联 NFR/OQ**：FR-SAFE-05、FS-E001；—

#### AC-SAFE-12 · 启动扫描既有仓凭据 → `SEC-E001` 告警 + 脱敏展示 + 不自动改配置
- **覆盖 FR**：FR-SAFE-06
- **测试层级/依赖**：IT（内嵌 token 的夹具仓）
- **Given**：既有仓 `.git/config` 的 origin URL 含 `https://user:ghp_LEAK_123456@host/repo.git`；另一仓含 `insteadOf` 明文凭据；再一仓 clean。
- **When**：启动/首次扫描既有仓库。
- **Then**：① 两个问题仓触发告警，`data-error-code="SEC-E001"`，UI 列表显示仓库名与脱敏后的 URL（`ghp_****3456` 形式）；② **不自动修改**用户 `.git/config`（文件 hash 前后一致）；③ clean 仓无告警；④ 提供“查看处理指引”入口（不提供一键改配置）；⑤ 告警进入通知中心并持久化（FR-OPS-01）。
- **关联 NFR/OQ**：FR-SAFE-06、SEC-E001（旧 AC-14 仅映射见 §1.4）；R-11 已关闭

#### AC-SAFE-13 · Token header-only：抓包/命令行/remote URL 均无凭据

- **覆盖 FR**：FR-SAFE-06、D-006
- **测试层级/依赖**：IT（G-WIRE 代理 + 进程命令行快照 + remote URL）
- **Given**：token=`ghp_CANARY_9f8e7d6c5b4a`；执行 clone/fetch/push。
- **When**：采集：① G-WIRE 代理记录的 Gitea HTTP 请求（字段 `method,url,status`，**不记 Authorization 值**）；② `Get-CimInstance Win32_Process` 全量命令行快照；③ 所有 `git remote -v`/`.git/config`。
- **Then**：① 认证成功（G-WIRE 记录 2xx 且 token 以 header 形式送达的**布尔**记录，不落值）；② 命令行/环境变量落盘/remote URL/`.git/config` 中哨兵出现次数=0；③ 无 `?token=`/`access_token=` 出现在 URL；④ 无 `sslVerify=false`；⑤ 证据产物固定为 `docs/evidence/security/gwire.jsonl` + `docs/evidence/security/argv.txt` + `docs/evidence/security/remotes.txt`，CI 可复跑；不要求网络抓包工具。
- **关联 NFR/OQ**：FR-SAFE-06、D-006（旧 AC-14 仅映射见 §1.4）；SRS v0.2 附录 D 已明确 `ci.token` 只存凭据引用
### 2.10 任务/通知/日志/设置（FR-OPS）

#### AC-OPS-01 · 通知四类触发 + `notification` 持久化 + 已读/清除
- **覆盖 FR**：FR-OPS-01
- **测试层级/依赖**：IT（`G-E2E` 注入 + DB）
- **Given**：分别注入 4 类事件：① 组件状态恶化（SYNCED→DIVERGED）；② 批量完成（成功/部分失败各 1）；③ Token 过期（401）；④ 远程同步失败（覆盖不足）。
- **When**：逐一触发后打开通知抽屉。
- **Then**：① 4 类各产生 ≥1 条 `notification` 行，`type` 取 SRS §8.1 CHECK 集（STATE_CHANGED/JOB_PARTIAL/TOKEN_EXPIRED/SYNC_FAILED/CI_EVENT）、`payload_json` 含上下文（组件/仓/错误码）；② 通知中心计数=未读条数；③ 点“已读”后 `read_at` 非空、计数减 1；④ 点“清除”后对应 `notification` 行被**删除**（SRS v0.2.1 FR-OPS-01：清除=删除通知行），列表为空；⑤ 重启后未读通知仍存在（持久化）；⑥ **失败路径**：注入 `notification` 写库失败 → 降级为 UI Toast + 日志错误码，不阻断触发它的主流程（如任务完成仍写 `job.state` 终态并触发受影响仓重扫），且失败本身写审计。
- **关联 NFR/OQ**：FR-OPS-01；R-20/R-31 已关闭（清除=删除通知行）

#### AC-OPS-02 · 通知中心 CI 事件类目：类型枚举存在 + 空分组（CI-6 锚点）
- **覆盖 FR**：FR-OPS-01、FR-CI-01
- **测试层级/依赖**：VIS + IT
- **Given**：`ci.enabled=false`。
- **When**：打开通知中心。
- **Then**：① 通知中心存在 “CI” 类目/分组（`data-ci-category` 存在），显示空态“CI 未接入”（不得空白/裸 TODO）；② IPC 事件契约中 `CI_UPDATED` 类型已注册（即使 v1 不发送）；③ 其它 4 类通知不受影响；④ 快照测试通过。
- **关联 NFR/OQ**：CI-6、FR-CI-02；—

#### AC-OPS-03 · 日志：10MB×30 天滚动 + 结构化字段 + 级别
- **覆盖 FR**：FR-OPS-02
- **测试层级/依赖**：IT（`CLK` + 写入压测）
- **Given**：日志目录初始为空；`log.keepDays=30`。
- **When**：① 写入 >25MB 日志（脚本快速灌入）；② 推进 `CLK` 31 天；③ 检查结构化字段。
- **Then**：① 单文件滚动上限 10MB（`logs\host-*.log` 每文件 ≤10MB，滚动产生多个文件）；② 31 天后超过保留期的文件被清理；③ 每条结构化日志含 `job_id`/`repo_id`/`component_id`/`request_id`/`duration_ms`/`http_status`/`exit_code` 中适用字段（抽样断言）；④ 级别枚举 `Verbose/Debug/Information/Warning/Error/Fatal` 可按级过滤；⑤ 审计日志不在普通滚动目录（独立 sink）；⑥ **失败路径**：日志目录不可写（只读目录注入）→ 应用不崩溃、状态栏/日志页提示 `FS-E001` 降级、主功能继续可用，并在恢复可写后自动续写。
- **关联 NFR/OQ**：FR-OPS-02、SRS §12.5；—

#### AC-OPS-04 · 审计日志独立 sink，保留 ≥90 天
- **覆盖 FR**：FR-OPS-02、FR-OP-09
- **测试层级/依赖**：IT（`CLK`）
- **Given**：`audit.keepDays=90`；已产生 100 条审计。
- **When**：推进 `CLK` 至 91 天；检查普通日志目录与审计目录。
- **Then**：① 审计文件位于独立 `audit\` 目录，不与普通日志混写；② 90 天内记录保留、超出被清理；③ 审计记录不可被普通日志清理任务删除；④ 审计文件同样脱敏（哨兵扫描=0）；⑤ 90 天阈值可配（30–365）且生效。
- **关联 NFR/OQ**：FR-OPS-02、附录 D；—

#### AC-OPS-05 · 日志级别过滤/搜索/暂停
- **覆盖 FR**：FR-OPS-02
- **测试层级/依赖**：IT + VIS（P7 日志页）
- **Given**：日志含 6 个级别各 ≥2 条，含关键字 `FAILED_SYNC`。
- **When**：① 选级别 `Warning`；② 搜索 `FAILED_SYNC`；③ 点“暂停”后再产生新日志。
- **Then**：① 级别过滤=显示 ≥所选级别（SRS v0.2.1 FR-OPS-02）：选 `Warning` 时显示 Warning/Error/Fatal，不显示 Information 及以下；② 搜索命中行数=关键字实际条数且高亮；③ 暂停后新日志不刷新显示（计数提示“有 N 条新日志”），恢复后补齐；④ 日志内容只读；⑤ 支持复制选中行（不含脱敏前内容）。
- **关联 NFR/OQ**：FR-OPS-02；R-32 已关闭（SRS v0.2.1：显示 ≥所选级别）

#### AC-OPS-06 · 一键诊断包：内容清单与导出流程
- **覆盖 FR**：FR-OPS-02、FR-SAFE-03
- **测试层级/依赖**：IT（与 AC-SAFE-06 共用产物）
- **Given**：运行 ≥10 分钟后导出。
- **When**：点“导出诊断包”→选择路径→导出。
- **Then**：① 导出进度可见、完成后提示路径；② 包内至少含 8 项：配置（脱敏）、进程状态、最近任务、DB 版本、日志、转储清单、DB 快照、环境版本（以 §12.5 清单为准，R-14 关闭）；③ 包大小 ∈[1KB, 500MB]（超出上限报错并给错误码）；④ 导出失败给出错误码与原因（磁盘/权限）；⑤ 导出路径越界（网络盘/系统目录）被拒 `FS-E001`。
- **关联 NFR/OQ**：FR-OPS-02；R-14 已关闭（FR-OPS-02 引用 §12.5）

#### AC-OPS-07 · 设置页六分区 + 附录 D 设置键逐项核对（v0.2.1：38 键）
- **覆盖 FR**：FR-OPS-03
- **测试层级/依赖**：IT + VIS
- **Given**：附录 D 全量设置键表（以 SRS v0.2.1 附录 D 为唯一清单；实测 **38 键**：`workspace.root`、`server.baseUrl`、`server.org`、`auth.mode`、`proxy.mode`、`proxy.url`、`tls.extraCa`、`git.path`、`scan.fingerprintIntervalSec`、`scan.hotIntervalSec`、`scan.coldIntervalMin`、`remote.listWatermarkMin`、`remote.refreshMin`、`remote.fullSweepHours`、`fetch.beforeWrite`、`ttl.localSec`、`ttl.fetchSec`、`ttl.remoteSec`、`delete.retentionDays`、`backup.retentionDays`、`log.keepDays`、`audit.keepDays`、`ui.theme`、`ui.density`、`ui.reduceMotion`、`ci.enabled`、`ci.baseUrl`、`ci.token`、`ci.pollIntervalSec`、`dirty.includeUntracked`、`remote.prune`、`safety.requireTypedConfirm`）。
- **When**：打开 P8 设置页，逐分区核对键。
- **Then**：① 分区固定六组：通用/工作区/认证/操作与安全/CI 集成（禁用占位）/实验特性（v1 无可配置键，仅说明/开关占位，R-18 关闭）；② 附录 D 每个键均可在 UI 找到对应控件（键→控件映射表 100% 命中，**38/38**；含 `backup.retentionDays`、`ci.token`、`dirty.includeUntracked`、`remote.prune`、`safety.requireTypedConfirm`）；③ `ci.enabled/ci.baseUrl/ci.token/ci.pollIntervalSec` 在 CI 分区且为禁用态 + 实验标记（`ci.token` 控件只显示“已存凭据引用/尾 4 位”）；④ 每控件显示当前值与默认值（重置入口存在），缺键即失败（不得出现附录 D 无定义的设置项）；⑤ 快照通过。
- **关联 NFR/OQ**：FR-OPS-03、附录 D；R-13/R-18 已关闭（CI 4 键齐；实验特性 v1 无键）

#### AC-OPS-08 · 设置校验与生效范围（即时/重启）逐键（v0.2.1：38 键全覆盖）
- **覆盖 FR**：FR-OPS-03
- **测试层级/依赖**：IT（`CLK`）
- **Given**：附录 D 38 键逐键非法/边界样本（如 `delete.retentionDays=0`/`91`、`backup.retentionDays=0`/`91`、`scan.fingerprintIntervalSec=4`、`server.baseUrl=http://…`、`ui.theme=blue`、`dirty.includeUntracked=not-bool`、`remote.prune=not-bool`、`safety.requireTypedConfirm=not-bool`、`ci.token` 超长/含空白）。
- **When**：逐键保存非法值；再逐键保存合法值并观察生效时机；对 v0.2 新增键 `dirty.includeUntracked`/`remote.prune`/`safety.requireTypedConfirm`/`ci.token` 各跑 1 组行为断言。
- **Then**：① 非法值被拒并给字段级提示（`CFG-E001`/`FS-E001` 等），`app_setting` 保持旧值；② 标注“即时”的键（扫描/远程/TTL/删除保留/备份保留/日志/UI 主题/`dirty.includeUntracked`/`remote.prune`/`safety.requireTypedConfirm`）保存后下一轮/立即生效（用 `CLK` 或下一轮扫描断言）；③ 标注“重启”的键（`git.path`/`ci.*`/代理）重启后生效；④ 改变 `workspace.root`/`server.org` 在 v1 不生效（入口只读，见 AC-CFG-10）；⑤ 键名与附录 D 完全一致（无大小写/拼写差异），键数=38。
- **关联 NFR/OQ**：FR-OPS-03、附录 D；M-04/M-05（R-23/R-26 已关闭）

#### AC-OPS-09 · 单实例：双开提示退出且不写数据
- **覆盖 FR**：FR-OPS-04
- **测试层级/依赖**：IT + E2E（进程启动）
- **Given**：实例 A 已运行并持有单实例锁（命名 Mutex + 工作区 `.mbdgit.lock`）。
- **When**：启动实例 B。
- **Then**：① 实例 B 显示“已有实例在运行”提示后退出（进程退出码可断言）；② A 不受影响；③ B 不写 DB、不写工作区（DB `-wal` 大小/行数不变）；④ 双开检测覆盖 Mutex 与工作区锁两种路径（分别构造）；⑤ 日志记录双开事件（脱敏）。
- **关联 NFR/OQ**：FR-OPS-04（旧 AC-15 仅映射见 §1.4）；—

#### AC-OPS-10 · stale lock 自愈（PID/时间/进程存活三条件）
- **覆盖 FR**：FR-OPS-04
- **测试层级/依赖**：IT（`CLK` + 残留锁文件）
- **Given**：构造 3 种 stale lock（SRS v0.2.1 FR-OPS-04 三条件）：① PID 不存在；② PID 存在但进程非本应用；③ 锁文件 mtime 超 10min（`CLK` 推进 >10min）且无存活进程；另 1 种：PID 存活且确为上一实例残留（非 stale）。
- **When**：启动应用。
- **Then**：① 前 3 种均自愈（锁被清除且应用进入主界面 `data-ready=true`），写审计/日志说明；② 第 4 种按“单实例占用”处理，不误删活锁；③ 自愈不破坏 DB（`quick_check`=ok）；④ 强杀（TerminateProcess）后立即重启可自愈（真实场景用例）；⑤ 自愈判定字段（PID/时间/存活）可从日志断言。
- **关联 NFR/OQ**：FR-OPS-04（旧 AC-15 仅映射见 §1.4）；R-33 已关闭（SRS v0.2.1 FR-OPS-04 三条件含 mtime>10min）

#### AC-OPS-11 · 状态栏 6 段数据绑定
- **覆盖 FR**：FR-OPS-05
- **测试层级/依赖**：VIS + IT
- **Given**：应用已运行 ≥1 分钟 + 注入 1 次同步失败 + 1 个进行中任务 + 1 条未读通知。
- **When**：观察状态栏。
- **Then**：① 6 段固定顺序：数据新鲜度 / 进程健康 / 操作队列 / CI 队列（v1 恒 0）/ 通知 / 网络；② 各段数据与后端一致（新鲜度时间、队列数、通知计数、网络状态、CI 计数=0）；③ 进程健康恰 **2 灯**=Host + GitWorker（SRS v0.2.1 FR-OPS-05）；各 `BackgroundService`（RemoteSync/LocalScan/StatusCalc/JOB_RUNNER）以子状态/悬浮明细呈现（`health{name,state,…}`），不得把子状态升为独立灯；④ 点击各段跳转对应页面（通知/任务/CI）；⑤ 布局快照 1280/1440/1920 三档不换行。
- **关联 NFR/OQ**：FR-OPS-05、SRS §12.5 健康快照；R-34 已关闭（SRS v0.2.1 FR-OPS-05：2 灯 + `BackgroundService` 子状态）

#### AC-OPS-12 · DB schema `user_version` + 迁移前 `VACUUM INTO` 备份 + 失败回滚
- **覆盖 FR**：FR-OPS-06
- **测试层级/依赖**：IT（旧版本 DB 夹具）
- **Given**：v1 旧 schema DB（`user_version=1`）与目标版本 DB。
- **When**：① 启动执行迁移；② 注入迁移脚本失败。
- **Then**：① 迁移成功：`PRAGMA user_version`=目标值、表结构符合 §8.1（16 表存在，含 `ci_status`/`notification`/`event_log` 空表）；② 迁移前生成 `VACUUM INTO` 备份文件（断言文件存在且可打开）；③ 注入失败时回滚到旧版本（`user_version` 不变、数据行数不变、应用可用或明确提示）；④ 备份文件路径在 `%LOCALAPPDATA%\MbdGitTool\`；⑤ 启动记录 `sqlite_version()` ≥3.51.3。
- **关联 NFR/OQ**：FR-OPS-06、SRS §8.2；—

#### AC-OPS-13 · DB 损坏：`quick_check` 失败 → 重建（≤5min/100 组件）
- **覆盖 FR**：FR-OPS-06、NFR-REL-04
- **测试层级/依赖**：IT（损坏库夹具 + 计时）
- **Given**：人为破坏的 app.db（页损坏）；100 个本地仓夹具。
- **When**：启动应用触发 `PRAGMA quick_check`。
- **Then**：① 检测失败后重命名旧库（保留，不直接删除）→ 建新库 → 全量重扫；② 重建完成后看板可用（100 组件，`repo` 行数=本地仓数）；③ 重建总耗时 ≤5min（P95，按 §13.3 计时）；④ 旧库文件保留在 `MbdGitTool\` 下可导出；⑤ 全过程 UI 有进度提示，不静默。
- **关联 NFR/OQ**：FR-OPS-06、NFR-REL-04；—

#### AC-OPS-14 · 自动更新（v1.1，版本外）
- **覆盖 FR**：FR-OPS-07（P2）
- **测试层级/依赖**：IT（签名包夹具）
- **Given**：v1 构建。
- **When**：检查更新入口/设置项。
- **Then**：① v1 无自动更新入口（不得出现死按钮），设置页无“自动更新”可选项；② v1.1 验收口径（后续冻结）：更新前等 job 空闲并备份 DB、失败可回滚、包签名校验；③ 本 AC 在 M0 仅冻结 ①。
- **关联 NFR/OQ**：FR-OPS-07、NFR-COMPAT-08；§4.1 OQ-24

#### AC-OPS-15 · 首启三阶段：本地发现→远程对账→状态计算（含跳过远程降级）
- **覆盖 FR**：FR-OPS-08、SRS §3.1/§11.1、D-016
- **测试层级/依赖**：E2E（新机器 + `G-E2E` + `CLK`）
- **Given**：全新数据目录；`workspace_root` 下 400 个本地仓夹具；`G-E2E` 在线。
- **When**：首次启动 → 向导完成 → 观察进度页。
- **Then**：① `progress` 事件序列严格为 `LOCAL_DISCOVERY → REMOTE_RECONCILE → STATUS_CALC`（SRS v0.2.1 §11.1 冻结的阶段枚举，逐字一致），不出现“双阶段”旧口径；② 每阶段 `progress` 事件含 `{stage,total,current,cancellable}` 四字段（SRS v0.2.1 冻结），`current` 单调递增至 total；③ 全部完成后看板可用（400 本地仓、组件计数正确）；④ 中途点“跳过远程”后进入只读降级（`data-degraded` 标记、显示“数据截至 HH:mm”），本地仓**不被**永久误判 INVALID/MISSING；⑤ 远程恢复后自动执行重判（见 AC-OPS-16）；⑥ 首启 400 仓场景 ≤30s 可用看板（NFR-PERF-09）。
- **关联 NFR/OQ**：FR-OPS-08（旧 AC-11 仅映射见 §1.4）、NFR-PERF-09；R-35 已关闭（SRS v0.2.1 §11.1 冻结进度协议）

#### AC-OPS-16 · 首扫进度协议 + 远程就绪自动重判（不永久误判）
- **覆盖 FR**：FR-OPS-08
- **测试层级/依赖**：E2E（`G-WIRE` 延迟响应 + `CLK`）
- **Given**：本地 400 仓已克隆；远程阶段被跳过（`G-WIRE` 不可达）；此时观察组件状态。
- **When**：① 跳过远程后打开看板；② 恢复 `G-WIRE` 并触发重连/自动重试。
- **Then**：① 跳过阶段后看板显示只读降级 + “远程未就绪”，本地仓状态按**本地事实**判定（INVALID/MISSING 不被误判：本地存在的仓不得显示 MISSING）；② 未绑定/待对账仓显示 `UNKNOWN` 且 `component.incomplete=1`（SRS v0.2.1 口径，不再使用未定义的中间态命名），不得显示 `INVALID`；③ 远程就绪后自动触发重判：受影响组件 `aggregate_status` 由 UNKNOWN 更新为真实态（断言至少 1 个组件由 UNKNOWN→SYNCED/BEHIND），`incomplete` 清零；④ 重判不需用户操作；⑤ 重判后“远程未就绪”标记消失；⑥ 进度事件含 `{stage,total,current,cancellable}` 四字段且取消可用。
- **关联 NFR/OQ**：FR-OPS-08、D-016（旧 AC-21 仅映射见 §1.4）；R-36 已关闭（SRS v0.2.1：UNKNOWN+incomplete=1）

#### AC-OPS-17 · 错误码表核对：稳定、可读文案、UI 展示错误码+文案
- **覆盖 FR**：FR-OPS-09
- **测试层级/依赖**：UT + IT
- **Given**：附录 D 的 srsCode 表 + D.2 Host 映射表（v0.4 冻结）。
- **When**：① 逐码触发或单测注入；② 修改某码的用户文案。
- **Then**：① 每个错误码均有：码值、用户可读文案、可重试性、日志级别（四列已冻结：码值/文案/可重试性/日志级别，见 D.2）；② UI 展示格式固定为“错误码 + 文案”（`data-error-code` + 文案节点）；③ 文案变化后码值不变（回归断言）；④ 未知错误兜底码存在且前缀合法；⑤ 任务中心/日志/诊断包三处引用同一码表。
- **关联 NFR/OQ**：FR-OPS-09、附录 D；R-06 已关闭。

### 2.11 CI 预留（FR-CI，v1 只占位）

#### AC-CI-01 · 10 锚点存在性（UI 快照）
- **覆盖 FR**：FR-CI-01、SRS §11.4、04 §5.4-1
- **测试层级/依赖**：VIS + IT
- **Given**：v1 构建（`ci.enabled=false`）。
- **When**：遍历应用并断言 10 个锚点。
- **Then**：① CI-1 左侧导航“CI 流水线”入口存在（路由可跳转、页面骨架 + 空态 + 实验标记）；② CI-2 组件行/卡片 CI 徽标槽位存在且定高 16–20px；③ CI-3 组件详情 CI 汇总条 40px 存在；④ CI-4 单仓详情第 4 个 Tab 存在；⑤ CI-5 状态栏 CI 队列段存在且计数=0；⑥ CI-6 通知中心 CI 类目存在（见 AC-OPS-02）；⑦ CI-7 看板筛选 CI facet 存在且禁用；⑧ CI-8 设置“CI 集成”分区存在且 disabled；⑨ CI-9 数据契约（`ci_status` 表/`ci_latest` 视图/`CI_UPDATED`/`ICiProvider`）存在；⑩ CI-10 聚合分离（见 AC-CI-04）。10/10 缺一即失败。
- **关联 NFR/OQ**：FR-CI-01、04 §5.4-1；—

#### AC-CI-02 · CI 槽位定高：1280/1440/1920 三档不换行、不遮挡、卡片高度变化 0px
- **覆盖 FR**：FR-CI-01、04 §5.4-2
- **测试层级/依赖**：VIS（三档分辨率）
- **Given**：同一组件数据在 1280×800、1440×900、1920×1080 三档。
- **When**：渲染并测量 CI 槽位与行/卡片几何。
- **Then**：① 槽位高度 ∈[16,20]px 且三档一致；② 槽位不换行、不与其他信息位遮挡（元素边界不重叠）；③ 卡片/行高度在 CI 数据有/无时变化=0px；④ 每行/卡片恰有 1 个 CI 槽位；⑤ 三档快照通过。
- **关联 NFR/OQ**：CI-2、04 §5.4-2；—

#### AC-CI-03 · 注入 CI FAIL 后卡片底色不变（中性）
- **覆盖 FR**：FR-CI-03
- **测试层级/依赖**：VIS（`ci.enabled=true` + Mock FAIL 注入）
- **Given**：同一组件分别注入 `CiState=PASS`、`FAIL`、`NONE`、`UNKNOWN`。
- **When**：截图对比卡片底色与聚合徽标。
- **Then**：① 4 种注入下卡片底色（计算样式 background-color）=中性 token 且完全相同；② 聚合徽标/左轨颜色完全不变（不因 CI 变化）；③ CI 徽标本身按通道 B 变化（FAIL 红点/`data-ci-state="FAIL"`）；④ 与 SRS §11.2 通道 B 颜色一致。
- **关联 NFR/OQ**：FR-CI-03、CI-10（旧 AC-12 仅映射见 §1.4）；—

#### AC-CI-04 · CI 不参与组件聚合：注入 FAIL 后 `aggregate_status` 不变
- **覆盖 FR**：FR-CI-03、SRS §7.4/D-002
- **测试层级/依赖**：UT + IT
- **Given**：组件四仓均 `SYNCED` → 聚合 `SYNCED`；注入四仓 CI 均 `FAIL`。
- **When**：重新计算聚合。
- **Then**：① `component.aggregate_status` 仍为 `SYNCED`；② 排序位置不变（严重度不变）；③ CI 聚合字段（`byState`/`worstState`）独立存在且 `worstState=FAIL`；④ 单测断言聚合纯函数输入不含 CI 字段（契约隔离）；⑤ 任何 CI 状态组合下聚合结果与“仅仓库状态”计算完全一致。
- **关联 NFR/OQ**：FR-CI-03、D-002、CI-10；—

#### AC-CI-05 · 数据契约：`CiLatest`/`ComponentCiAggregate`/`ICiProvider` 字段与枚举冻结（SRS v0.2 契约）
- **覆盖 FR**：FR-CI-02
- **测试层级/依赖**：UT（契约 schema 校验）
- **Given**：SRS §11.4 冻结契约（SRS v0.2 修订后原文，字段名照抄；契约↔DDL 映射以 SRS §8.3 为唯一判据，AC-CI-09 按此断言）。
- **When**：对 Mock 实现产物做 JSON Schema 校验。
- **Then**：① `CiState` 枚举恰为 `PASS|FAIL|RUNNING|QUEUED|NONE|UNKNOWN` 6 值；② `CiLatest` 含全部字段：`repoId: number`（Gitea 数字 id）、`componentId: string`、`repoType`（`DESIGN|IMPL|MIL|SIL`）、`branch`、`commitHash`（必须绑定 commit）、`state`、可选 `pipelineId`/`buildNumber`/`failedStage`/`startedAt`/`finishedAt`/`durationMs`/`url`、`provider`、`fetchedAt`；**字段名必须为 `componentId` 与数字 `repoId`（不得使用 D-009 禁用别名，也不得用字符串型 repo id）**；③ `ComponentCiAggregate` 含 `componentId`、`byState`（6 键全）、`worstState`、`lastFailed`、`dataFreshAt`；④ `worstState` 优先级为 `FAIL > RUNNING > QUEUED > UNKNOWN > PASS > NONE`（单测排序断言）；⑤ `ICiProvider` 三方法签名存在（`getLatest(componentId)`/`getAggregate(componentId)`/`subscribe`），v1 `subscribe` 为空实现。
- **关联 NFR/OQ**：FR-CI-02、SRS §11.4；R-16 已关闭（SRS v0.2 §11.4 冻结 `componentId`/数字 `repoId`；`ci_status` 列见 AC-CI-09）

#### AC-CI-06 · `CI_UPDATED` 事件：类型注册 + 处理分支 + Mock 触发
- **覆盖 FR**：FR-CI-02
- **测试层级/依赖**：IT（事件注入）
- **Given**：`ci.enabled=true`，Mock provider 可手动发事件。
- **When**：通过 Mock 触发 `CI_UPDATED`（payload 为 1 条 `CiLatest`）。
- **Then**：① IPC 契约中存在 `CI_UPDATED` 类型定义与处理分支（代码/契约断言）；② 收到事件后对应槽位/徽标更新为 payload 的 `state`（DOM 断言）；③ 未知字段/未知 `state` 时使用 `UNKNOWN` 兜底且不崩溃；④ v1 `ci.enabled=false` 时事件不发送（subscribe 为空实现，`getLatest` 返回空数组）；⑤ 事件节流/去重不丢失最终态（连发 3 条取最后一条）。
- **关联 NFR/OQ**：FR-CI-02、04 §5.4-7；—

#### AC-CI-07 · `ci.enabled` 默认 false；开 true 后仅换数据不改结构
- **覆盖 FR**：FR-CI-02、04 §5.4-6
- **测试层级/依赖**：IT + VIS
- **Given**：全新安装。
- **When**：① 读取 `ci.enabled`；② 改为 true 并重启，注入 Mock 数据；③ 对比开/关两态 DOM 结构。
- **Then**：① 默认值=`false`（`app_setting` 断言）；② 开 true 后 CI-2/3/4/5/7 显示 Mock 数据（非空态）；③ 组件结构不变：CI 槽位/汇总条/Tab/状态栏/筛选的 DOM 路径、尺寸、位置与关闭态一致（仅内容变化）；④ 无需改组件代码/重建前端（配置驱动）；⑤ 关闭后恢复空态且无残留 Mock 数据。
- **关联 NFR/OQ**：FR-CI-02、附录 D `ci.enabled`；—

#### AC-CI-08 · 无 CI 时统一 `CiSlot` 空态，禁止裸 TODO/白块
- **覆盖 FR**：FR-CI-04
- **测试层级/依赖**：VIS + 文案扫描
- **Given**：`ci.enabled=false` 全站页面。
- **When**：扫描全站 DOM 文本与截图。
- **Then**：① 全站无裸文本 `TODO`/`敬请期待`/`coming soon`（正则扫描命中=0）；② 所有 CI 位置使用统一 `CiSlot` 空态组件（`data-testid="ci-slot"` + `data-state="NONE"`），文案统一“CI 未接入”/“CI 集成将在后续版本开放”；③ 空态有固定尺寸、骨架不塌陷、tooltip 完整、`aria-label` 存在；④ 5 处以上位置（行/详情/Tab/状态栏/通知）空态一致；⑤ 视觉快照通过。
- **关联 NFR/OQ**：FR-CI-04、04 §5.4-9；—

#### AC-CI-09 · `ci_status` 表 + `ci_latest` 视图 + `ci.*` 配置键在 v1 建好（空数据，SRS v0.2 契约）
- **覆盖 FR**：FR-CI-05、SRS §8.1/§11.4
- **测试层级/依赖**：IT（DDL 检查 + DB 查询）
- **Given**：v1 全新 DB。
- **When**：检查 schema 与数据。
- **Then**：① `ci_status` 表存在，主键 `(repo_id, commit_sha)`，含 SRS v0.2 §8.1 全列：`provider/state/run_id/run_url/branch/build_number/failed_stage/started_at/finished_at/duration_ms/fetched_at/updated_at`（含 v0.2 补的 `build_number/started_at/finished_at`），v1 行数=0；② `ci_latest` 视图存在且可查询（每 `repo_id` 取最新 `commit_sha` 对应行），结果为空集不报错；视图列与契约 `CiLatest` 按 SRS §8.3 映射表断言（`pipelineId↔run_id`、`url↔run_url`、`commitHash↔commit_sha`、`componentId` 经 `repo.component_id` 推导、**`repoType` 由 `ci_status.repo_id → repo.repo_type` 推导（DESIGN/IMPL/MIL/SIL，不冗余存储）**、`byState/worstState` 由视图分组聚合不落表，M-12/N-07）；③ `app_setting` 含 `ci.enabled=false`、`ci.baseUrl=''`、`ci.token`（只存凭据引用，空）、`ci.pollIntervalSec=60` 四键；④ `event_log` 空表存在；⑤ 迁移脚本包含上述对象（M0 冻结）。
- **关联 NFR/OQ**：FR-CI-05、SRS §8.1/§11.4；R-16/R-19 已关闭（旧 AC-12 仅映射见 §1.4）

#### AC-CI-10 · CI 六态视觉（通道 B）与 `worstState` 聚合展示
- **覆盖 FR**：FR-CI-02、SRS §11.2 通道 B
- **测试层级/依赖**：VIS（6 态 × 关键位置快照）
- **Given**：`ci.enabled=true`，分别注入 6 种 `CiState`；另一组注入四仓混合状态。
- **When**：渲染看板/组件详情/单仓 Tab/状态栏/通知。
- **Then**：① 六态视觉与 SRS 通道 B 一致：`PASS`=success 圆点✓、`FAIL`=danger 圆点✕、`RUNNING`=info 半环◐、`QUEUED`=neutral 空心○、`NONE`=neutral ∅、`UNKNOWN`=hatched ?；② 形状/位置与通道 A 明确不同（圆点/pill，不染卡片底色）；③ 四仓混合时 `worstState` 按 `FAIL>RUNNING>QUEUED>UNKNOWN>PASS>NONE` 展示且与契约一致；④ 视觉基线包含 6 态 × ≥2 位置 ≥12 张；⑤ 状态色只来自 `--ci-*` token（无硬编码 hex）。
- **关联 NFR/OQ**：FR-CI-02、SRS §11.2；—

#### AC-CI-11 · CI 数据新鲜度 `fetchedAt` 展示锚点
- **覆盖 FR**：FR-CI-02、SRS §11.4 契约（“UI 必须展示新鲜度”）
- **测试层级/依赖**：VIS + IT
- **Given**：Mock 数据 `fetchedAt=T0`，当前 `CLK=T0+45min`。
- **When**：查看组件行/组件详情汇总条/单仓 CI Tab。
- **Then**：① 3 处均显示 CI 数据时间（“CI 数据 X 分钟前”或 tooltip，`data-testid="ci-freshness"`）；② 超过 §7.1 CI TTL 30min（v2 值，v1 占位）时显示过期态而非伪造当前；③ 无 CI 数据时显示“无 CI”而非“0 秒前”；④ 展示格式与本地/远程新鲜度统一；⑤ 快照通过。
- **关联 NFR/OQ**：FR-CI-02、SRS §11.4、§7.1；—

### 2.12 v1.1 AC（不冻结，不计入 188 条冻结基线）

> 口径（SRS §15 OQ-24 / D-008）：P2 功能（FR-CFG-06 配置热更新、FR-DETAIL-05 右键菜单）在 v1 只冻结“入口不存在/不可用”（AC-CFG-10、AC-DETAIL-09 ①）。以下完整行为为 v1.1 草案，**M0/M1/M2 不验收、不冻结**；编号使用域内**下一个未占用序号**并以 `(v1.1)` 后缀标识（当前 `AC-CFG-12(v1.1)`、`AC-DETAIL-10(v1.1)`），**不占用 188 条冻结编号、不计入统计**（N-09）；v1.1 立项时按 §5.4 变更规则转为正式编号。

#### AC-CFG-12(v1.1) · 配置热更新完整行为（不冻结）
- **覆盖 FR**：FR-CFG-06（P2）
- **测试层级/依赖**：IT（`G-WIRE`、本地 FS 夹具）
- **Given**：已加载组织 `MBD`（1,000 组件）与根 `D:\MBD\Workspace`（100 个本地仓）。
- **When**：设置页把 `server.org` 改为 `MBD2`→确认对话框点“确定并重同步”；再把 `workspace.root` 改为空目录 `D:\MBD\Empty`→确认“重扫”。
- **Then**：① 确认对话框列出影响：组件数 1,000→（MBD2 实际数）、本地仓 100→0；② 确认后 `component` 行数=新组织组件数、`repo` 中 `is_local=1` 行数=0；③ 未确认点“取消”时配置不变、DB 行数不变；④ 变更写 `operation_log.action` 并更新 `app_setting`。
- **关联 NFR/OQ**：OQ-05、OQ-24；v1.1 冻结

#### AC-DETAIL-10(v1.1) · 右键菜单四动作完整行为（不冻结）
- **覆盖 FR**：FR-DETAIL-05（P2）
- **测试层级/依赖**：IT + VIS
- **Given**：文件树节点、提交行、组件行。
- **When**：右键打开上下文菜单。
- **Then**：① 菜单含 4 项：在资源管理器打开、复制路径、复制 commit、用其他程序打开；② 复制动作写入剪贴板并断言内容（路径/完整 SHA）；③ “在资源管理器打开”启动 `explorer.exe` 且路径限定 root 内（越界拒绝 `FS-E001`）；④ 键盘上下文菜单键（Shift+F10）等价可用。
- **关联 NFR/OQ**：OQ-24；v1.1 冻结

---

## 3. FR→AC 追溯矩阵

> 统计：**104 条 FR（9.1–9.11 全量）/ 188 条 AC**；每条 FR ≥1 条 AC，P0 FR 均含成功 + 失败/异常路径。里程碑按 SRS §3.3：M0=骨架与冻结（含 CI 锚点/契约/状态矩阵/夹具/AC 冻结）、M1=只读闭环、M2=写操作、M3=收口；`v1.1` 表示版本外（v1 只冻结“不暴露入口”断言）。

| FR ID | P | AC IDs | 成功路径 | 失败/异常路径 | 测试层级 | 里程碑 |
|---|---|---|---|---|---|---|
| FR-CFG-01 | P0 | AC-CFG-01, AC-CFG-02, AC-CFG-03, AC-CFG-11 | ✓ | ✓ | IT+VIS | M1 |
| FR-CFG-02 | P0 | AC-CFG-04, AC-CFG-05, AC-CFG-11 | ✓ | ✓ | IT（Mock/真 Gitea） | M1 |
| FR-CFG-03 | P0 | AC-CFG-06, AC-CFG-07 | ✓ | ✓ | IT+安全扫描 | M1 |
| FR-CFG-04 | P1 | AC-CFG-05, AC-CFG-08 | ✓ | ✓ | IT+E2E | M1 |
| FR-CFG-05 | P1 | AC-CFG-09 | ✓ | ✓ | IT | M1 |
| FR-CFG-06 | P2 | AC-CFG-02, AC-CFG-10 | ✓ | ✓ | IT | v1.1 |
| FR-SYNC-01 | P0 | AC-SYNC-01, AC-SYNC-02, AC-SYNC-03, AC-SYNC-14 | ✓ | ✓ | IT+E2E | M1 |
| FR-SYNC-02 | P0 | AC-SYNC-04, AC-SYNC-05 | ✓ | ✓（AC-SYNC-04 ⑤ refs 部分失败） | IT | M1 |
| FR-SYNC-03 | P0 | AC-SYNC-01, AC-SYNC-06, AC-SYNC-07 | ✓ | ✓ | IT | M1 |
| FR-SYNC-04 | P0 | AC-SYNC-01, AC-SYNC-03, AC-SYNC-08 | ✓ | ✓ | IT | M1 |
| FR-SYNC-05 | P1 | AC-SYNC-09, AC-SYNC-10, AC-SYNC-13 | ✓ | ✓ | IT+E2E | M1 |
| FR-SYNC-06 | P1 | AC-SYNC-11, AC-SYNC-12 | ✓ | ✓ | IT | M1 |
| FR-SCAN-01 | P0 | AC-SCAN-01, AC-SCAN-02, AC-SCAN-03 | ✓ | ✓ | IT | M1 |
| FR-SCAN-02 | P0 | AC-SCAN-04 | ✓ | ✓（AC-SCAN-04 ⑦ 单仓扫描失败） | IT（表驱动） | M1 |
| FR-SCAN-03 | P0 | AC-SCAN-05, AC-SCAN-06 | ✓ | ✓ | IT | M1 |
| FR-SCAN-04 | P0 | AC-SCAN-07, AC-SCAN-08, AC-SCAN-09, AC-SCAN-10 | ✓ | ✓（AC-SCAN-07 ⑥ 深扫失败隔离） | IT+PERF | M1 |
| FR-SCAN-05 | P0 | AC-SCAN-11 | ✓ | ✓ | IT | M1 |
| FR-SCAN-06 | P1 | AC-SCAN-03, AC-SCAN-12, AC-SCAN-13 | ✓ | ✓ | IT | M1 |
| FR-STA-01 | P0 | AC-STA-01, AC-STA-02, AC-STA-03, AC-STA-12, AC-STA-13, AC-STA-15 | ✓ | ✓ | UT+IT | M1 |
| FR-STA-02 | P0 | AC-STA-01, AC-STA-04, AC-STA-09, AC-STA-14 | ✓ | ✓ | UT+IT | M1 |
| FR-STA-03 | P0 | AC-STA-05, AC-STA-06 | ✓ | ✓ | UT+IT | M1 |
| FR-STA-04 | P0 | AC-STA-07, AC-STA-08, AC-STA-09 | ✓ | ✓（AC-STA-07 ④ fetch 失败 error_code） | IT | M1 |
| FR-STA-05 | P1 | AC-SCAN-09, AC-STA-10 | ✓ | — | IT+PERF | M1 |
| FR-STA-06 | P1 | AC-STA-11, AC-DETAIL-02, AC-OP-11, AC-OP-12 | ✓ | ✓ | UT+IT | M1 |
| FR-BOARD-01 | P0 | AC-BOARD-01, AC-BOARD-02, AC-BOARD-03, AC-BOARD-14, AC-BOARD-15, AC-BOARD-16 | ✓ | ✓*（七态异常由 AC-BOARD-08 覆盖） | IT+VIS+PERF | M1 |
| FR-BOARD-02 | P0 | AC-BOARD-03, AC-BOARD-04, AC-BOARD-05, AC-BOARD-06, AC-BOARD-07 | ✓ | ✓ | IT+PERF | M1 |
| FR-BOARD-03 | P0 | AC-BOARD-08 | ✓ | ✓ | IT+VIS | M1 |
| FR-BOARD-04 | P1 | AC-BOARD-09, AC-BOARD-10, AC-BOARD-11 | ✓ | ✓ | IT+VIS | M2 |
| FR-BOARD-05 | P1 | AC-STA-08, AC-BOARD-05, AC-BOARD-12, AC-BOARD-14 | ✓ | — | VIS+PERF | M1 |
| FR-BOARD-06 | P1 | AC-BOARD-13 | ✓ | ✓ | IT+VIS | M1 |
| FR-DETAIL-01 | P0 | AC-DETAIL-01, AC-DETAIL-02 | ✓ | ✓（AC-DETAIL-01 ⑤ 数据源失败） | IT+VIS | M1 |
| FR-DETAIL-02 | P0 | AC-DETAIL-03, AC-DETAIL-04, AC-DETAIL-05 | ✓ | ✓（AC-DETAIL-03 ⑤ 非法深链参数） | IT+VIS+PERF | M1 |
| FR-DETAIL-03 | P1 | AC-DETAIL-06, AC-DETAIL-07, AC-SAFE-10 | ✓ | ✓ | IT+安全 | M1 |
| FR-DETAIL-04 | P1 | AC-DETAIL-08 | ✓ | — | VIS+IT | M1 |
| FR-DETAIL-05 | P2 | AC-DETAIL-09 | ✓ | — | IT+VIS | v1.1 |
| FR-OP-01 | P0 | AC-SYNC-14, AC-OP-01, AC-OP-02, AC-OP-03, AC-OP-04, AC-OP-05 | ✓ | ✓ | E2E+IT | M2 |
| FR-OP-02 | P0 | AC-OP-06, AC-OP-07, AC-OP-08 | ✓ | ✓ | E2E+IT | M2 |
| FR-OP-03 | P0 | AC-OP-09, AC-OP-10 | ✓ | ✓ | E2E+IT | M2 |
| FR-OP-04 | P0 | AC-SYNC-05；AC-OP-11…14（D-029 暂定，不参与 M2b） | ✓ | ✓ | E2E+UT | M2 |
| FR-OP-05 | P0 | AC-OP-15, AC-OP-16, AC-OP-17, AC-OP-18, AC-OP-19 | ✓ | ✓ | E2E+IT | M2 |
| FR-OP-06 | P0 | AC-OP-20, AC-OP-21, AC-OP-22, AC-OP-23, AC-OP-30 | ✓ | ✓ | E2E+IT | M2 |
| FR-OP-07 | P0 | AC-OP-24, AC-OP-25 | ✓ | ✓ | E2E+IT | M2 |
| FR-OP-08 | P0 | AC-OP-03, AC-OP-26, AC-OP-27 | ✓ | ✓ | E2E | M2 |
| FR-OP-09 | P1 | AC-OP-28, AC-OPS-04 | ✓ | — | IT | M2 |
| FR-OP-10 | P1 | AC-SCAN-06, AC-OP-29 | ✓ | ✓ | IT+E2E | M2 |
| FR-OP-11 | P0 | AC-OP-30 | ✓ | ✓ | E2E | M2 |
| FR-OP-12 | P1 | AC-OP-31, AC-OP-32 | ✓ | ✓ | E2E | M2 |
| FR-REPO-01 | P0 | AC-REPO-01, AC-REPO-02, AC-REPO-03 | ✓ | ✓ | IT | M2 |
| FR-REPO-02 | P1 | AC-REPO-04, AC-REPO-05 | ✓ | ✓ | E2E | M2 |
| FR-REPO-03 | P1 | AC-REPO-06 | ✓ | ✓ | IT | M2 |
| FR-REPO-04 | P2 | AC-REPO-07 | ✓ | — | VIS+IT | v1.1 |
| FR-REPO-05 | P0 | AC-OP-05, AC-REPO-08, AC-REPO-09, AC-REPO-10, AC-REPO-11 | ✓ | ✓ | E2E+IT | M2 |
| FR-SAFE-01 | P0 | AC-OP-16, AC-SAFE-01, AC-SAFE-02, AC-SAFE-03 | ✓ | ✓ | VIS+IT | M2 |
| FR-SAFE-02 | P0 | AC-OP-20, AC-SAFE-04 | ✓ | ✓ | IT | M2 |
| FR-SAFE-03 | P0 | AC-CFG-06, AC-SAFE-05, AC-SAFE-06, AC-SAFE-07, AC-OPS-06 | ✓ | ✓ | UT+IT+安全 | M3 |
| FR-SAFE-04 | P0 | AC-OP-26, AC-SAFE-08, AC-SAFE-09 | ✓ | ✓ | E2E | M2 |
| FR-SAFE-05 | P1 | AC-DETAIL-06, AC-DETAIL-07, AC-SAFE-10, AC-SAFE-11 | ✓ | ✓ | IT+安全 | M1 |
| FR-SAFE-06 | P0 | AC-CFG-06, AC-SAFE-12, AC-SAFE-13 | ✓ | ✓ | IT+安全 | M1 |
| FR-OPS-01 | P0 | AC-OPS-01, AC-OPS-02 | ✓ | ✓（AC-OPS-01 ⑥ 写库失败降级） | IT+VIS | M3 |
| FR-OPS-02 | P0 | AC-SAFE-06, AC-OPS-03, AC-OPS-04, AC-OPS-05, AC-OPS-06 | ✓ | ✓ | IT | M3 |
| FR-OPS-03 | P0 | AC-OPS-07, AC-OPS-08 | ✓ | ✓ | IT+VIS | M3 |
| FR-OPS-04 | P0 | AC-OPS-09, AC-OPS-10 | ✓ | ✓ | IT+E2E | M1 |
| FR-OPS-05 | P1 | AC-OPS-11 | ✓ | — | VIS+IT | M3 |
| FR-OPS-06 | P1 | AC-OPS-12, AC-OPS-13 | ✓ | ✓ | IT | M1（迁移）/M3（重建） |
| FR-OPS-07 | P2 | AC-OPS-14 | ✓ | — | IT | v1.1 |
| FR-OPS-08 | P0 | AC-OPS-15, AC-OPS-16 | ✓ | ✓ | E2E | M1 |
| FR-OPS-09 | P1 | AC-OPS-17 | ✓ | ✓ | UT+IT | M0（码表冻结）/M1 |
| FR-CI-01 | P0 | AC-DETAIL-03, AC-OPS-02, AC-CI-01, AC-CI-02 | ✓ | ✓*（锚点缺失即失败；空态由 AC-CI-08 覆盖） | VIS+IT | M0 |
| FR-CI-02 | P0 | AC-CI-05, AC-CI-06, AC-CI-07, AC-CI-10, AC-CI-11 | ✓ | ✓ | UT+IT+VIS | M0 |
| FR-CI-03 | P0 | AC-CI-03, AC-CI-04 | ✓ | ✓*（AC-CI-03 注入 CI FAIL 本身即异常注入） | VIS+UT | M0 |
| FR-CI-04 | P1 | AC-CI-08 | ✓ | — | VIS | M0 |
| FR-CI-05 | P0 | AC-CI-09 | ✓ | ✓*（AC-CI-09 空集查询不报错） | IT（DDL） | M0 |

**统计**：FR 总数 = 104（P0 = 46 / P1 = 22 / P2 = 4，以 SRS §9 优先级列为准；FR-OPS-06 为 P1）；AC 总数 = 188（含 AC-UI-10a/b/c 子字母；§2.12 v1.1 草案 AC 不计入）；按域：CFG 11 / SYNC 14 / SCAN 13 / STA 15 / BOARD 16 / DETAIL 9 / OP 35 / REPO 11 / SAFE 17 / OPS 17 / CI 11 / UI 19。**P0 FR 的成功 + 失败/异常双路径**：本表“失败/异常路径”列为 ✓ 者均在该 FR 的主 AC 内直接断言异常分支；标 `✓*` 者为“异常面由同一 P0 域内相邻 AC 覆盖”（如展示类 FR-BOARD-01 的七态异常由 AC-BOARD-08 覆盖、FR-SYNC-02 的 refs 失败由 AC-SYNC-04/12 覆盖、契约类 FR-CI-05 的空集查询不报错由 AC-CI-09 覆盖），冻结评审需逐条确认 `✓*` 的覆盖关系成立。

---

## 4. 未覆盖项与产品决策依赖

### 4.1 产品决策依赖（OQ）与 AC 暂定口径

> 以下 OQ 不阻塞 M0 冻结（SRS §15 判定“是否阻塞=否”者），但 AC 必须先有可执行口径；本列为本目录采用的暂定口径，M0 拍板后同步替换 AC 中的对应断言。

| OQ | 问题 | 本目录 AC 暂定口径 | 影响的 AC |
|---|---|---|---|
| OQ-05 | 容量口径 1,000 组件/4,000 仓 vs 4× 压测 | 分两档：正常档 1,000 组件/4,000 仓；压测档 4,000 组件/16,000 仓，性能预算不豁免 | AC-BOARD-03, AC-BOARD-14, AC-SYNC-01 |
| OQ-06 | 未来 CI 系统 | 只冻结 `provider='mock'`，不冻结真实 provider 名 | AC-CI-05/06/07 |
| OQ-07 | 是否允许后台自动 fetch | 允许、默认开；fetch 周期取附录 D `remote.refreshMin`（SRS v0.2 已定义，R-26 关闭），可关 | AC-SCAN-11, AC-OP-15 |
| OQ-11/16 | 常驻形态/托盘 | 验收只断言“进程存活 + 单实例 + 通知可达”，不冻结托盘/服务形态；形态变更不影响 AC | AC-OPS-09/10 |
| OQ-12 | 远程 SLO（10min/30min/每日） | 按 7.5 分档验收（≤10min 可见、TTL 20min、每日全量 P95≤5min） | AC-SYNC-06, AC-SCAN-11 |
| OQ-13 | 打包 MSIX vs MSI | v1 以 MSIX 为主，签名校验归 M3；AC 不绑定安装器形态 | —（NFR-COMPAT-08 不在本目录 AC 范围） |
| OQ-15 | Gitea 配额/限流 | 验收用 WireMock 注入 429；现场真实配额不计入 AC | AC-SYNC-12 |
| OQ-17 | 多根目录 | v1 恰好 1 个根；多根入口不存在（AC-CFG-02 ③） | AC-CFG-02 |
| OQ-18 | RDP/共享工作区写操作 | 暂定“禁止写、只读展示并提示”；写入返回 `OP-E004`/`SCAN-E002` | AC-SCAN-12, AC-STA-15 |
| OQ-19 | Win10 22H2 ESU | 验收环境固定 Windows 11 x64；Win10 不计入 AC | 全部 E2E/PERF AC |
| OQ-20 | 缺仓是否长期常态 | 按 D-015：缺仓=`BINDING_ERROR` 中性、不参与异常优先最高档 | AC-SYNC-14, AC-BOARD-01 |
| OQ-21 | §7.4 严重度列全序 | **已关闭（D-020 + SRS v0.2 §7.4）**：`severity_rank` 0–14 与聚合优先级分离，看板按 `severity_rank` 升序 + 组件 ID 排序；`BINDING_ERROR` 再按 `binding_cause` 分 1（INVALID/AMBIGUOUS，含未绑定）/12（INCOMPLETE，仅缺仓） | AC-BOARD-01/07, AC-SYNC-14, AC-STA-04 |
| OQ-22 | `untracked` 是否升 `DIRTY` 主状态 | **已关闭（D-021 + 附录 D `dirty.includeUntracked=false`）**：仅 tracked 改动升 DIRTY；改 true 时 untracked 也升 DIRTY（AC-STA-13 参数化断言） | AC-STA-13 |
| **OQ-23**（本目录新提） | 错误码全表（附录 D 仅示例 16 个）与四列（码/文案/可重试性/日志级别） | M0 冻结全表；AC 只断言“码值稳定 + 前缀合法 + UI 同码”，具体新码 M0 补 | AC-OPS-17, AC-OP-04, AC-OP-23, AC-SAFE-13 |
| OQ-24 | P2 FR 的版本归属（FR-CFG-06/DETAIL-05/REPO-04/OPS-07） | v1 只冻结“入口不存在/不可用”（AC-CFG-10/AC-DETAIL-09 已按此重写）；完整行为单列 §2.12 v1.1 AC（不冻结） | AC-CFG-10, AC-DETAIL-09, AC-REPO-07, AC-OPS-14 |

### 4.2 SRS 需修订清单（本目录发现，逐条给出 AC 暂定口径）

> 规则：本目录**不自行发明枚举/命名**；凡 SRS 缺失/矛盾而 AC 必须依赖的，标注 R-xx 并在下表说明暂定口径与建议修订位置。**状态列**按 SRS v0.2.1 逐条核对：已关闭项标 SRS 位置，仍开放项列为 M0 前必修。R-01…R-38 中 **38 条已关闭、R-06 于 v0.4 关闭**（M0 出口条件：附录 D 错误码全表冻结）。夹具总数：**25**（附录 C 25 行，含 v0.2.1 新增 `F-MISSING+BLOCKED` 组合夹具；`F-PARTIAL` 主状态为 null，只断言聚合）。M0 冻结前需由 SRS 修订并回写本目录。

| ID | 位置 | 问题 | AC 暂定口径 / 修订建议 | 状态（SRS v0.2.1） |
|---|---|---|---|---|
| R-01 | §7.2 vs §7.6 | UNKNOWN/INVALID 行 §7.2 的允许写操作与 §7.6 不一致 | 以 §7.6 矩阵为准 | **已关闭**（SRS v0.2.1 §7.2 UNKNOWN/INVALID 行已改为“见 7.6”） |
| R-02 | §7.4 严重度列 | 原严重度非全序，默认排序不可判定 | `severity_rank` 0–14（D-020）；`BINDING_ERROR` 按 `binding_cause` 分 1/12 | **已关闭**（SRS v0.2.1 §7.4 + D-020：`severity_rank` 0–14；`binding_cause` INCOMPLETE=12 / 其余=1） |
| R-03 | 附录 C `F-TAG_DRIFT` | 第 4 仓主状态未定死 | AC-OP-12/AC-STA-01 按附录 C 断言第 4 仓 `SYNCED` | **已关闭**（SRS v0.2.1 附录 C `F-TAG_DRIFT`：第 4 仓固定 `SYNCED`） |
| R-04 | 附录 C `F-LFS-MISSING` | 原写 `DIRTY?`，非确定值 | 主状态 `SYNCED` + `lfs_missing` flag，聚合 SYNCED | **已关闭**（SRS v0.2.1 附录 C `F-LFS-MISSING` 行） |
| R-05 | FR-REPO-02 vs §7.6 | EMPTY 首推与“需 upstream”冲突 | AC-REPO-11 断言不受拦截 | **已关闭**（SRS v0.2.1 FR-REPO-02 已加 EMPTY 例外） |
| R-06 | 附录 D 错误码表 | 仅 16 示例、无“可重试性/日志级别”列 | AC 只断言码前缀/码值稳定/UI 同码；M0 冻结全表 | **已关闭（v0.4）**：附录 D 已补 Host 码与 srsCode 映射；E_DELETE_PENDING/E_AUTH_FAILED/E_REMOTE_NOT_FOUND/E_NETWORK/E_DISK_FULL/E_PATH_TOO_LONG 已登记 |
| R-07 | 附录 C `F-NO_UPSTREAM-GONE` | flag 写 `remote_gone?` | 不置 remote_gone，仅主状态 NO_UPSTREAM | **已关闭**（SRS v0.2.1 附录 C `F-NO_UPSTREAM-GONE` 行） |
| R-08 | §8.1 字段缺失 | `repo.renamed_from`、`baseline_snapshot.tag_object_sha`、停止管理字段 | AC-SYNC-09/AC-OP-14/AC-OP-32 已按新字段断言 | **已关闭**（SRS v0.2.1 §8.1 `component.is_managed`/`repo.renamed_from`/`baseline_snapshot.tag_object_sha`） |
| R-09 | §10.3 vs §12.6 | 凭据口径不一 | CM 为对外方案、DPAPI 为实现细节 | **已关闭**（SRS v0.2.1 §10.3/§12.6 已统一） |
| R-10 | §6.3/§7.3/NFR | 网络盘/reparse/submodule 多口径并存 | AC-SCAN-12/13 按“不纳管+`scan_issue` 告警；已纳管后不可写→只读展示；submodule 标记+写拒绝”断言 | **已关闭**（SRS v0.2.1 §6.3 已统一口径） |
| R-11 | §13.1 AC-14 | 既有仓内嵌 token 不可能清零 | 告警+脱敏+不自动改 | **已关闭**（SRS v0.2.1 §13.1 AC-14） |
| R-12 | §13.1 AC-13 vs §13.3 | 容量口径不一 | 两档数据集 | **已关闭**（SRS v0.2.1 §13.1 AC-13 已分两档） |
| R-13 | §11.4 CI-8 vs 附录 D | 缺 `ci.token` | 附录 D 4 键 | **已关闭**（SRS v0.2.1 附录 D `ci.*` 4 键含 `ci.token`） |
| R-14 | FR-OPS-02 vs §12.5 | 诊断包清单不一 | 以 §12.5 为准 | **已关闭**（SRS v0.2.1 FR-OPS-02 引用 §12.5） |
| R-15 | §7.4 优先级 1 vs 2 | BINDING_ERROR/UNKNOWN 遮蔽未裁决 | 加裁决句 | **已关闭**（SRS v0.2.1 §7.4 BINDING_ERROR/UNKNOWN 裁决句） |
| R-16 | §11.4 契约 | 旧字段名与 §8.1 不一致、字符串 repo id、`ci_status` 缺列 | AC-CI-05/AC-CI-09 按 SRS v0.2 回写 | **已关闭**（SRS v0.2.1 §11.4：`componentId`/数字 `repoId`；§8.1 `ci_status`；§8.3 映射表） |
| R-17 | §11.4 CI 契约 | 聚合契约旧字段名、无 repo 级主键、undefined 与 NONE 未区分 | 字段改 `componentId`；`ci_status` PK `(repo_id,commit_sha)`；无数据=NONE | **已关闭**（SRS v0.2.1 §11.4/§8.1） |
| R-18 | §9.10 设置页 | “实验特性”分区无附录 D 键 | AC-OPS-07 断言该分区存在但 v1 无可配置键 | **已关闭**（SRS v0.2.1 FR-OPS-03：实验特性 v1 无键，仅说明/占位） |
| R-19 | §8.1 `ci_status` | 缺 `build_number/started_at/finished_at` | AC-CI-09 按新列断言 | **已关闭**（SRS v0.2.1 §8.1 `ci_status` 全列含 `build_number/started_at/finished_at`） |
| R-20 | §8.1 多个枚举字段 | 无取值集 | 按 §8.1 CHECK 断言 | **已关闭**（SRS v0.2.1 §8.1：全部 CHECK 集已冻结，并补 `job.skipped`、`job_item.started_at/finished_at`、`job_item.state=INTERRUPTED`、`sync_run.duration_ms`、`component.last_activity_at`） |
| R-21 | §8.1 `remote_ref.ref_type` | 无 CHECK/枚举 | AC-SYNC-05 断言 `BRANCH`/`TAG` | **已关闭**（SRS v0.2.1 §8.1 `ref_type CHECK BRANCH/TAG`） |
| R-22 | §6.3 `safe.directory` | 无错误码落点 | 附录 D `SCAN-E001` | **已关闭**（SRS v0.2.1 附录 D `SCAN-E001`） |
| R-23 | §7.2 DIRTY 条件 | 无 `dirty.includeUntracked` 键 | 默认 false；AC-STA-13 参数化 | **已关闭**（SRS v0.2.1 附录 D `dirty.includeUntracked` + D-021） |
| R-24 | §11.2/§10.5 | DIRTY token/对比度未冻结 | `DIRTY=success+变更徽标` | **已关闭**（SRS v0.2.1 §11.2 `DIRTY=success+变更徽标`）；hatched 对比度 H-04 已由 v0.2.1 修正（`#4B5563`） |
| R-25 | FR-OP-05 vs §7.6 | detached 下 SYNC 目标未定义 | §7.6 DETACHED SYNC=❌，AC-OP-17 跳过 | **已关闭**（SRS v0.2.1 §7.6 DETACHED 行 SYNC=❌） |
| R-26 | FR-SYNC-05/SCAN-05 | fetch prune 策略缺失 | `remote.prune=true` | **已关闭**（SRS v0.2.1 附录 D `remote.prune=true`） |
| R-27 | FR-OP-12 vs §8.1 | component 无停止管理字段 | `is_managed/is_deleted` | **已关闭**（SRS v0.2.1 §8.1 `component.is_managed/is_deleted`） |
| R-28 | `safety.requireTypedConfirm` | 输入组件 ID 的大小写/空格未定 | AC-SAFE-03 按冻结规则断言 | **已关闭**（SRS v0.2.1 附录 D：精确匹配、区分大小写、忽略首尾空格） |
| R-29 | FR-BOARD-02 | 中文 IME 规则未写入 | AC-BOARD-05 断言 `compositionend` 触发 | **已关闭**（SRS v0.2.1 FR-BOARD-02：IME 以 `compositionend` 触发） |
| R-30 | FR-DETAIL-03 | 白名单扩展名未列举 | AC-SAFE-10 按完整白名单断言 | **已关闭**（SRS v0.2.1 FR-DETAIL-03：白名单 12 类已列） |
| R-31 | FR-OPS-01 | 通知“清除”语义未定 | AC-OPS-01 ④ 断言删除行 | **已关闭**（SRS v0.2.1 FR-OPS-01：清除=删除通知行） |
| R-32 | FR-OPS-02 | 日志级别过滤语义未定 | AC-OPS-05 ① 断言“≥所选级别” | **已关闭**（SRS v0.2.1 FR-OPS-02：显示 ≥所选级别） |
| R-33 | FR-OPS-04 | stale lock 阈值无数值 | AC-OPS-10 断言三条件（含 mtime>10min） | **已关闭**（SRS v0.2.1 FR-OPS-04：PID 不存在 / PID 非本应用 / mtime>10min 且无存活进程） |
| R-34 | FR-OPS-05 | 进程健康灯映射未定 | AC-OPS-11 断言 Host+GitWorker 2 灯 + 子状态 | **已关闭**（SRS v0.2.1 FR-OPS-05：2 灯 + `BackgroundService` 子状态） |
| R-35 | §3.1/§11.1 | 进度协议字段未定义 | 三阶段 + `{stage,total,current,cancellable}` | **已关闭**（SRS v0.2.1 §11.1 首扫进度协议：阶段枚举 + `{stage,total,current,cancellable}`） |
| R-36 | FR-OPS-08/D-016 | 远程未就绪中间态命名未定 | `UNKNOWN` + `incomplete=1` | **已关闭**（SRS v0.2.1 FR-OPS-08：`UNKNOWN`+`incomplete=1`，不得 INVALID/MISSING） |
| R-37 | FR-DETAIL-02 | “滚动流畅”不可判定 | AC-DETAIL-04 按量化指标断言 | **已关闭**（SRS v0.2.1 FR-DETAIL-02：首屏 ≤500ms/≥58fps/单帧 ≤33ms） |
| R-38 | §8.1 `unbound_repo.reason` | 取值集未定义 | AC-SYNC-13/AC-BOARD-13 按 `DOCS/PATTERN/CASE_CONFLICT/UNKNOWN` 断言 | **已关闭**（SRS v0.2.1 §8.1 `reason CHECK DOCS/PATTERN/CASE_CONFLICT/UNKNOWN`） |

### 4.3 本目录不覆盖项（版本外/非 FR 范围）

| 项 | 原因 | 处理 |
|---|---|---|
| 真实 CI API 接入/告警（v2） | SRS §3.2 Out-of-Scope；v1 只有 Mock/契约 | 仅冻结 AC-CI-* 的 v1 占位与契约；v2 另立 AC |
| 基线管理 UI（v2：列表/冻结/漂移对比/导出） | §9.12 v2 占位 | v1 只覆盖 `baseline_snapshot` 记录与 `TAG_DRIFT`（AC-OP-11/12/14、AC-STA-11） |
| 远端仓库创建/删除/改名（明确不做） | §3.2 | 不设 AC；AC-SYNC-10 只断言“远端已删的本地提示”，不提供远端操作 |
| SSH/OAuth/SSO 认证（v2） | §3.2 | 不设 AC；FR-CFG 只验 PAT |
| 跨组件“全组织”批量写（v2） | §3.2 | 不设 AC；FR-BOARD-04 只验“筛选结果显式多选” |
| 多根目录（v1.1） | D-013/OQ-17 | AC-CFG-02 只断言单根；多根入口不存在 |
| Windows 10 22H2 ESU | D-018/OQ-19 | 验收环境固定 Win11 x64 |
| RDP 多会话/共享工作区写操作 | NFR-COMPAT-07/OQ-18 | 只读提示 + 写拒绝（AC-SCAN-12） |
| 自动更新（v1.1） | FR-OPS-07 P2 | AC-OPS-14 只冻结“v1 无入口” |
| 安装包签名/分发校验 | NFR-COMPAT-08，归 M3 打包验收（非 FR） | 本目录不设 AC，由发布验收承接 |
| 托盘/登录自启形态 | OQ-11 待确认（设计层） | 不设 AC（§4.1 OQ-11 口径） |

---

## 5. AC 冻结门禁

### 5.1 M0 必须冻结的 AC（不允许 M1/M2 再补语义）

M0 = 骨架与冻结（SRS §3.3）。以下 AC 的 Given/When/Then/预期值/夹具/错误码**全部冻结**，M0 退出时须全部可执行（可通过 Mock/夹具跑通或确定性失败）：

| 类别 | 必须冻结的 AC | 理由 |
|---|---|---|
| 状态与聚合（最高优先级） | AC-STA-01…AC-STA-15 | 状态矩阵是全部 UI/写操作的判定基座；附录 C 夹具同批冻结 |
| 配置与认证契约 | AC-CFG-01…AC-CFG-11 | 后续所有 E2E 的前置条件；Token 安全面（CFG-06/07）必须早冻结 |
| 远程同步契约 | AC-SYNC-01…AC-SYNC-14 | 清单/覆盖率/错误码/改名语义是数据模型输入 |
| 本地扫描契约 | AC-SCAN-01…AC-SCAN-13 | 只读零写入、BUSY、分层频率是正确性底线 |
| 状态×操作矩阵 | AC-STA-15、AC-OP-02、AC-OP-17 | §7.6 是写操作放行/禁止的唯一依据 |
| 破坏性安全基线 | AC-SAFE-01…AC-SAFE-13（SAFE-05/06/07 允许 M3 补测试细节，但断言语义 M0 冻结） | 数据丢失风险；审计/journal/备份语义必须先行 |
| CI 预留 | AC-CI-01…AC-CI-11 | 10 锚点/契约/DDL/色板通道 B 是 M0 交付物（SRS §3.3 M0 出口） |
| 首启三阶段 | AC-OPS-15、AC-OPS-16 | D-016 的门控与重判是 M1 出口前提 |
| 性能与 a11y 基线 | AC-BOARD-03、AC-BOARD-05、AC-BOARD-14、AC-BOARD-15、AC-SCAN-08、AC-SCAN-09 | 预算口径/测量方法/数据集生成器需在 M0 冻结，M1 才有可比基线 |
| 错误码与设置键 | AC-OPS-07、AC-OPS-08、AC-OPS-17 | 附录 D 的键/码是跨模块契约 |
| 单实例/DB 基线 | AC-OPS-09、AC-OPS-10、AC-OPS-12 | 数据目录/锁/schema 版本在 M0 定型 |
| 详情/看板骨架断言 | AC-BOARD-01、AC-BOARD-08、AC-DETAIL-03、AC-DETAIL-06、AC-DETAIL-07 | 路由/Tab 顺序/安全边界后加成本高（CI-10 同理） |

### 5.2 M1/M2 允许补充的 AC 边界（只增不改语义）

| 里程碑 | 允许补充 | 禁止改动 |
|---|---|---|
| M1（只读闭环） | ① 视觉回归快照张数扩展（≥12 → 全覆盖 15 聚合态 × 深色）；② 只读交互细节断言（hover/tooltip/键盘顺序）细化；③ 新增夹具变体（同一状态的不同构造）作为**新 AC 编号** | 不得修改已冻结 AC 的 Then 期望值/枚举/错误码；不得降低性能/安全/状态断言 |
| M2（写操作） | ① 故障注入矩阵扩展（更多 4xx/5xx 组合、更多部分失败组合）作为新 AC；② E2E 脚本细化（步骤拆分不改变断言）；③ 任务中心/取消/重试的补充用例 | 不得改动 §7.6 矩阵、错误码映射、备份/stash/CAS 顺序、`.trash` 保留期 |
| M3（收口） | ① 日志/诊断/通知/设置的数据绑定细节；② 安装/发布验收（非 FR） | 不得改动 AC 的判定语义；P0 AC 不得以“收口期优化”为由降级 |

### 5.3 冻结检查清单（M0 签收用）

- [ ] 1. **FR 覆盖**：104/104 FR 均有 ≥1 条 AC，追溯矩阵无悬空 FR（§3）。
- [ ] 2. **P0 路径**：每条 P0 FR 的 AC 同时覆盖成功路径与关键失败/异常路径（§3“失败/异常路径”列）。
- [ ] 3. **可自动化**：全部 Then 落在 6 类观测源（DB/IPC/UI DOM/日志错误码/FS/git 输出），无“正常/流畅/合理/友好”等词（§1.1 禁止词扫描命中=0）。
- [ ] 4. **夹具可执行**：附录 C 全部 `F-*` 由 测试夹具构造产出，测试内构造的状态夹具 与 AC-STA-01/02 期望逐条一致（红队 06 AC-04 关闭）。
- [ ] 5. **不可判定项关闭**：§1.5 表 15 行（红队 93–115 行）逐条有 AC 编号且 AC 可执行；“其他可测试性缺陷”5 项已处置。
- [ ] 6. **状态模型闭合**：14 主状态/11 flags/15 聚合态/UNKNOWN 兜底/§7.6 矩阵全部有 AC（AC-STA-01/02/03/15）。
- [ ] 7. **TTL/离线**：TTL 数值（90/1200/25200s）与离线可判定状态集有 AC（AC-STA-09/14），且 §7.1 表与附录 D 默认值一致。
- [ ] 8. **首启三阶段**：阶段名/进度字段/跳过降级/自动重判有 AC（AC-OPS-15/16），且与 §3.1/§11.1 口径一致（三阶段）。
- [ ] 9. **安全**：header-only/既有仓 token 扫描/转储与诊断包脱敏/Gitea 无写请求均有 AC（AC-SAFE-04/05/06/07/12/13、AC-CFG-06/07）。
- [ ] 10. **Tag/TAG_DRIFT**：无“基线模式”，无条件计算有 AC（AC-STA-11、AC-OP-11/12/13/14）。
- [ ] 11. **CI 预留**：10 锚点/契约/DDL/底色不变/无裸 TODO/新鲜度均有 AC（AC-CI-01…AC-CI-11）。
- [ ] 12. **性能**：两档数据集、测量方法（§13.3）、门禁（BENCH）写入 AC（AC-BOARD-03/05/14、AC-SCAN-08/09、AC-SYNC-12）。
- [ ] 13. **SRS 修订闭环**：§4.2 R-01…R-38 已逐条标状态（38 已关闭：**M0 出口条件**，附录 D 错误码全表冻结时回写 AC）；§4.1 OQ-21/OQ-22 已关闭，OQ-23/OQ-24 仍为 M0/版本归属决策。
- [ ] 14. **版本外项**：v1.1 项（FR-CFG-06/DETAIL-05/REPO-04/OPS-07）的 AC 只冻结“入口不存在”，完整行为移入 §2.12 v1.1 AC（不冻结），不阻塞 M0。
- [ ] 15. **编号与映射**：新编号规则生效、旧 AC-01…27 映射表存在（§1.2/§1.4），无重复/空缺编号。
- [ ] 16. **签收**：产品/QA/架构三方确认；冻结记录（版本号、日期、文件哈希）写入本文件末尾。

### 5.4 冻结记录（v0.4）

| 项 | 内容 |
|---|---|
| 冻结版本 | **本文件 v0.4**（M2b 增量需求冻结） |
| 文件哈希（SHA256） | `C15E4944D88A95687F9128EAF616610E57E730ECF1B2BE41A99109967C19E68B`（评审基线 commit `cc86f52`） |
| 冻结日期 | 2026-11-15 |
| 签收人 | 需求评审：`review-spec-r5-complete` PASS；可测试性：`review-spec-r4-testability` PASS；架构/安全：`review-spec-r4-arch` PASS；产品/QA 由用户按本文件确认 |
| 结论 | ☑ 通过（v0.4 冻结，允许进入 M2b 开发） |
| 变更规则 | 冻结后只增不改语义；改 Then 期望值/枚举/错误码 → 先改 SRS（修订记录 + ADR）→ 再改 AC 并升版本号 |

> v0.4 起不再使用外部 freeze-checklist；本文件 §5.3 + Annex A 即冻结门禁。冻结后只增不改语义；变更须新 ADR + 升版本。


---
