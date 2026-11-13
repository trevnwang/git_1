# MBD 组件全生命周期 Git 工具 — 需求规格说明书（SRS v0.2.1 冻结候选修订）

> **本文档是需求的唯一维护源。** 原始 `MBD组件全生命周期Git工具.docx` 与 `software_workflow.drawio` 已冻结为证据归档（`reqs/archive/`），**不再修改**；后续所有需求变更只改本文件。
> 本文档由 6 路对抗式评审（需求质量 / Git 语义 / 架构可行性 / UI+CI / drawio 执行 / 红队复审）汇总重写并修订，评审证据见 `reqs/review/`，评审结论见 `reqs/review-report.md`；v0.2 冻结资产见 `reqs/v0.2/`；v0.2.1 按独立冻结复评 07（`reqs/review/07-freeze-review.md`）修订 SRS 侧阻断/高危项。

| 项目 | 内容 |
|---|---|
| 文档 ID | MBD-GIT-SRS |
| 版本 | **v0.2.1（冻结候选修订）** |
| 日期 | 2026-11-14 |
| 状态 | **Freeze Candidate v0.2.1**：第二轮冻结复评（`review/08`）判定“有条件进入 M0”；C1–C5 已关闭，C6（R-06 错误码全表）为 M0 出口条件 |
| 替代 | 草案 docx（仅作历史证据） |
| 权威级别 | **本文件 > 任何口头/聊天记录 > 原 docx/drawio** |

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
| D-020 | 组件严重度 | 采用 `severity_rank` 0–14（§7.4），与聚合优先级分离；看板默认按 severity_rank 升序、再组件 ID 排序 | 关闭 OQ-21 |
| D-021 | DIRTY 口径 | 默认 `dirty.includeUntracked=false`：仅 tracked 改动升 DIRTY；untracked 用 flag 展示 | 关闭 OQ-22 |

---

## 1. 背景与问题定义

业务以「组件」为单位管理 MBD（Model-Based Design）汽车嵌入式模型，但 Git 以 **4,000+ 个独立仓库**存储，二者天然割裂：

- 一个业务组件固定对应 4 个生命周期仓库：`Design` / `Impl` / `MIL` / `SIL`；
- 仓库命名形如 `CF325F_ChanganSigProcgBascFct_Design`，同一组件四仓共享前缀；
- 传统 Git 客户端只能按仓库操作，无法做组件级批量操作、版本联动、状态聚合；
- 开发者需要人工在多个仓库/网页间确认“我的组件现在是什么状态、CI 过没过、四仓版本是否对齐”。

**本工具解决的核心问题**：把 4,000 个仓库折叠成「组件」视角——**看板聚合状态 + 组件级批量操作 + 单仓精细操作**，并以 Gitea 远程数据 + 本地真实 Git 状态双源兜底。

---

## 2. 产品定位、目标与非目标

**定位**：Windows 桌面客户端（WebView2 网页级界面），面向 MBD 组件开发的 **Git 聚合操作台**。

**v1 目标（可度量）**

1. 用户不需要打开 4 个 Git 客户端即可完成组件的 Clone / 拉取 / 切分支 / 切 Tag / 强制同步 / 删除；
2. 看板在 1440×900、100% 缩放下完整展示组件行（4 仓状态 chip、聚合态、分支、CI 槽位、新鲜度）且无横向溢出；默认排序 = `severity_rank` **升序**（数值越小=严重度越高）+ 组件 ID 升序；
3. 任何破坏性操作都有“将丢失什么”的预览、自动备份与可恢复路径；
4. CI 位置在 UI 与数据模型中**一次预留到位**，后续接入不改导航/卡片/聚合语义。

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
| 看板 | 表格默认视图 + 卡片视图；搜索/筛选/排序/虚拟滚动/多选；异常优先 |
| 详情 | 组件详情（4 仓总览）、单仓详情（历史/差异/Tag 切换/CI 占位 Tab） |
| 批量操作 | Clone（幂等/预检/部分失败）、Pull、Fetch、Checkout 分支、切 Tag（detached）、受控 SYNC、本地删除、CREATE 分支（dry-run） |
| 单仓操作 | Pull / Fetch / Checkout / Push / Commit（最小化） |
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

> **P2 口径（v0.2.1 冻结，H-06）**：P2 功能（FR-CFG-06 / FR-DETAIL-05 / FR-REPO-04 / FR-OPS-07）在 v1 **仅保留入口不存在/不可用**（不暴露可执行入口、不实现完整行为）；完整行为归 v1.1，M0 只冻结“入口不存在/不可用”断言（OQ-24）。

### 3.3 里程碑（建议）

| 里程碑 | 交付 | 出口标准 |
|---|---|---|
| M0 骨架与冻结 | App Shell、导航、设计 token、CI 占位锚点、Mock 数据、假 Gitea；**状态矩阵夹具（附录 C）**、AC 全量冻结、D1–D6 图集、测试资产（Gitea 单二进制 fixture/`IClock`/基准脚本） | 页面骨架可跑、锚点快照通过、AC 追溯矩阵 100% 覆盖 FR、夹具清单签收 |
| M1 只读闭环 | 配置/认证、远程同步、本地发现、状态计算、看板/详情、三阶段首扫与就绪重判 | 附录 C 状态矩阵全绿；看板首屏 P95 ≤1.5s（NFR-PERF-02/09） |
| M2 写操作 | 批量 Clone/Pull/Checkout/Tag/SYNC/Delete/Restore/Create + 任务中心 + 备份/审计 | 写操作新 AC 通过（`AC-OP-01…AC-OP-32`、`AC-REPO-01…AC-REPO-11`、`AC-SAFE-01…AC-SAFE-04`）；故障注入通过 |
| M3 收口 | 通知/日志/诊断/设置、性能与可用性、打包、验收 | **AC 基线冻结版本全绿**；P0 缺陷清零；D1–D6 与需求/代码追溯一致 |

---

## 4. 角色与核心用例

**角色**：组件开发者（v1 唯一角色）。假设其对本机有读写权限、持有 Gitea PAT。

| UC | 用例 | 频率 |
|---|---|---|
| UC-01 | 首次配置并完成首扫，进入看板 | 一次/机器 |
| UC-02 | 日常巡检：查看组件状态、异常优先处理 | 每日多次 |
| UC-03 | 新组件接入：输入组件标识 → 四仓批量 Clone → 校验状态 | 每周 |
| UC-04 | 同步到某个版本：四仓批量切 Tag / 切分支 / 受控 SYNC | 每周 |
| UC-05 | 开发中：单仓 Pull/Push/Commit，外部 IDE 操作后看板自动兜底识别 | 每日多次 |
| UC-06 | 清理：组件本地删除（可恢复） | 每月 |
| UC-07 | 断网/Token 过期/Gitea 不可达：降级只读，恢复后自动补齐 | 偶发 |
| UC-08 | 排障：查看任务失败明细、日志、导出诊断包 | 偶发 |

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

`组件状态 = max(组件覆盖态, 四仓主状态)`；任何一仓 UNKNOWN ⇒ 组件不得显示 SYNCED。**聚合优先级**决定卡片显示哪个组件态；**严重度/排序**独立决定看板“异常优先”排序（解决“缺失是中性却排在分叉前”的矛盾）。

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

- **两个维度分离**：`聚合优先级` 决定卡片显示哪个组件态（数值越小越优先）；`severity_rank` 决定看板默认排序（数值越小越靠前）。二者不是同一维度，禁止混用。聚合优先级与评审 02 表 C 对齐（MISSING 先于 BLOCKED_LOCAL；BLOCKED_LOCAL 覆盖 UNMERGED/IN_PROGRESS）。
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
| DIVERGED | ❌ | ✅ | ⚠️ 仅展示不自动 merge | ❌ | ❌ | ⚠️ | ✅ | ⚠️（默认阻断；备份 ref + 输入组件 ID 后允许） | ⚠️ | ✅ | ❌ |
| BEHIND | ❌ | ✅ | ✅ | ❌ | ❌ | ⚠️ | ✅ | ⚠️ | ⚠️ | ✅ | ❌ |
| AHEAD | ❌ | ✅ | ❌ | ✅ | ❌ | ⚠️ | ✅ | ❌ | ⚠️ | ✅ | ❌ |
| NO_UPSTREAM | ❌ | ✅ | ❌ | ✅ 发布分支 | ❌ | ❌ | ✅ | ❌ | ⚠️ | ✅ | ✅ |
| DIRTY | ❌ | ✅ | ❌ 先 commit/stash | ✅ | ✅ | ⚠️ 需 stash/确认 | ⚠️ | ⚠️（stash -u + 确认） | ⚠️ | ✅ | ❌ |
| SYNCED | ❌ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | —（无需；仅清理 untracked 需确认） | ⚠️ | ✅ | ❌ |

- 恢复类操作独立：`Restore`（从 `.trash` 或备份 ref）见 FR-OP-11，可从任务中心/组件详情进入。
- 取消语义：只有 Clone/Pull/Checkout/Tag/SYNC/Delete/Create 等长任务支持合作式取消；取消后逐仓状态以实际扫描为准，未开始的仓保持原状，已开始的仓记 `CANCELLED` 并触发重扫（不整体回滚）。
- **与 FR-OP-05 的冲突裁决（C-04）**：本矩阵与 FR-OP-05 冲突时**以本矩阵为准**；AC-OP-15/16/18 按本矩阵重写——DIRTY 需 `stash -u` + 确认、DIVERGED 默认阻断且“备份 ref + 输入组件 ID”后允许、SYNCED 仅“清理 untracked”需确认。

---

## 8. 数据模型（设计基线；字段名可调，语义不可丢）

> 基线 schema 采用架构评审 03 §C.3（SQLite DDL 草案），本章为语义约束。**DB 是缓存不是事实源**：磁盘与 Gitea 才是事实。**新鲜度列按表定义（H-03）：事实类表必须带等价新鲜度列**——`repo_status` 带 `data_source`/`is_stale`/`calc_at`；`remote_ref` 带 `observed_at`/`synced_at`；`local_state` 带 `scan_at`/`last_success_at`/`scan_error`；`ci_status` 带 `fetched_at`；`sync_run` 带 `started_at`/`finished_at`/`duration_ms`；其余表（`repo`/`component`/`job`/`job_item`/`operation_log`/`app_setting`/`unbound_repo`/`scan_issue`/`notification`/`event_log`/`baseline_snapshot`）按行内定义带更新时间或采集时间。库可整库重建。

### 8.1 表清单（废弃“极简五表”表述）

> **冻结说明**：字段名自 v0.2 起冻结（变更需 ADR）；**v1 建表 16 张**（含 3 张空表预留/治理表）。`repo` 的远程字段（full_name/remote_url/default_branch/archived）由 REMOTE_SYNC 写，本地字段（is_local/local_path）由 LOCAL_SCAN 写；如实现要求严格单写者，可拆 `repo`/`repo_local` 两表（语义不变）。字段清单自 v0.2.1 起与图集 D4 ER 对齐（`repo.renamed_from`、`component.is_managed`、`local_state.scan_error`、`baseline_snapshot.tag_object_sha`、`job.started_at`、`ci_status` 全列已在本表登记），D4 ER 缺失列由图集侧并行回写。

| 表 | 主键 | 关键字段 | 唯一写者 |
|---|---|---|---|
| `component` | component_id | display_name, local_root, aggregate_status, status_calc_at, incomplete, is_managed, is_deleted, last_activity_at（最近活动时间，供看板“最近活动”排序）, timestamps | STATUS_CALC |
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
| FR-CFG-06 | P2 | 配置热更新（组织/根目录变更需确认影响范围并重扫） | 变更后看板计数正确 |

### 9.2 远程元数据同步（FR-SYNC）

| ID | 优先级 | 需求 | 验收 |
|---|---|---|---|
| FR-SYNC-01 | P0 | 同步组织仓库清单：`repo_id`、全名、默认分支、URL、是否空仓、创建/更新时间；分页拉全（Gitea 单页上限 50） | 4,000 仓清单覆盖率可计算并在 UI 展示 |
| FR-SYNC-02 | P0 | 对“本地已绑定 + 看板可见/收藏”的仓库同步分支列表与 Tag 列表（含 tag→commit），不全量拉 4,000 仓的分支/Tag；**“关注”定义 = 本地已绑定 ∪ 当前视图可见 ∪ 用户收藏**（收藏为 v1.1，v1 前两者） | 请求量与时间在 NFR 预算内；覆盖率展示 |
| FR-SYNC-03 | P0 | 首启立即执行一次组织同步；之后每日全量 + 清单水位 10min + 变化仓 refs 30min；失败指数退避重试 ≤2，失败写日志且不阻塞其他职责 | 断网首启进入降级态并可重试；部分成功显示覆盖率与失败清单 |
| FR-SYNC-04 | P0 | 每轮同步记录 `sync_run`（总数/成功/失败/覆盖率/耗时）；覆盖率 <100% 时看板标 STALE | 注入部分失败后看板出现 stale 提示 |
| FR-SYNC-05 | P1 | 远程仓删除/改名：`archived`/`renamed_from` 字段标记并提示；改名生成组件 ID 迁移事件（旧 ID→新 ID，需用户确认），不静默删行；未绑定仓库进入清单 | 改名/删除场景用例；迁移后看板无重复组件 |
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

### 9.5 看板与检索（FR-BOARD）

| ID | 优先级 | 需求 | 验收 |
|---|---|---|---|
| FR-BOARD-01 | P0 | 组件列表默认表格视图（默认排序 = `severity_rank` 升序（数值越小越严重）→ 组件 ID 升序），可切卡片视图；列：组件、聚合状态、4 仓状态、分支、Tag（可选）、CI 槽位、最后活动/新鲜度 | 两种视图数据一致；默认排序断言 |
| FR-BOARD-02 | P0 | 全局搜索与筛选：搜索字段=组件 ID/仓库全名/当前分支/Tag/HEAD 短 SHA；中文 IME 以 `compositionend` 触发查询（组合期不阻塞）；facet=聚合状态、单仓主状态、RepoType、分支、Tag、CI 状态（占位）、有未提交改动、有未推送提交；排序=严重度/组件 ID/最近活动；虚拟滚动 | 搜索/筛选/排序夹具与 AC；IME 用例；4,000 组件滚动 ≥58fps |
| FR-BOARD-03 | P0 | 七态齐备：空/加载/错误可重试/离线降级/过期/部分失败/**权限或认证过期**；每页均可重试 | 状态注入用例 + 截图 |
| FR-BOARD-04 | P1 | 批量操作条：对选中组件执行 Clone/Pull/Fetch/Checkout/Tag/SYNC/Delete/Restore/Create；**阈值 = 选中 >10 个组件或 >40 个仓**时二次确认；支持“全选筛选结果” | 选择 11 个组件触发确认；任务中心可逐仓查看 |
| FR-BOARD-05 | P1 | 数据新鲜度展示（本地/远程分源）与手动刷新；刷新中不得整表闪白 | 视觉回归 + 计时 |
| FR-BOARD-06 | P1 | 未绑定仓库入口（异常清单页/抽屉）：按原因分组、可重新解析、可忽略（需记录理由） | 未绑定清单页存在；重新解析用例 |

### 9.6 组件/单仓详情（FR-DETAIL）

| ID | 优先级 | 需求 | 验收 |
|---|---|---|---|
| FR-DETAIL-01 | P0 | 组件详情：4 仓状态卡（分支/HEAD/upstream/ab/ahead/behind/dirty）、Tag 对照、未绑定告警、快捷操作 | 组件状态下钻到单仓 1 次点击可达 |
| FR-DETAIL-02 | P0 | 单仓详情：概览/提交历史/差异/CI（占位）四 Tab；提交历史分页虚拟滚动；差异只读（二进制给不可预览提示） | 10,000 提交仓库：历史首屏 ≤500ms、滚动 ≥58fps、单帧 ≤33ms、提交序列与 `git log` 一致；diff 正确 |
| FR-DETAIL-03 | P1 | 文件树（只读）双击打开：单击选中、双击**仅白名单扩展名**直开（白名单 = 常见文本/模型/工程文件：`.m/.c/.h/.slx/.mdl/.json/.xml/.yaml/.yml/.txt/.md/.csv`）；白名单外与危险扩展名（.exe/.bat/.cmd/.ps1/.lnk/.msi/.scr）一律二次确认；路径限定 workspace_root 内 | 路径穿越/危险扩展名/白名单用例 |
| FR-DETAIL-04 | P1 | 四仓分支不一致时显示 Impl 分支为主 + “分支不一致”角标 + 四仓对照 | 角标必现；对照准确 |
| FR-DETAIL-05 | P2 | 右键菜单：在资源管理器打开、复制路径、复制 commit、用其他程序打开 | 交互用例 |

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
| FR-OP-12 | P1 | **组件纳入/移出管理**：按组件 ID 纳入（校验四仓存在性、展示将 clone 的仓），移出=本地删除或仅停止管理（DB 行保留 archived）；操作入审计 | AC-OP-31/AC-OP-32：纳入/移出后看板与 DB 状态正确 |

### 9.8 单仓操作（FR-REPO）

| ID | 优先级 | 需求 | 验收 |
|---|---|---|---|
| FR-REPO-01 | P0 | Fetch / Pull（ff-only）/ Checkout 分支或 Tag；操作前重新 scan；dirty 阻断规则同 9.7 | 与命令行结果一致 |
| FR-REPO-02 | P1 | Push：仅当前分支、有 upstream；无 upstream 提示“发布分支”；detached 禁止 Push 并提示建分支；分支保护拒绝时展示服务端消息。**例外：EMPTY 空仓首次推送由 FR-REPO-05 处理，不受“需 upstream”拦截** | 4 类场景用例 + EMPTY 首推用例 |
| FR-REPO-03 | P1 | Commit（最小化）：提交 staged 变更 + message；存在 unmerged 时禁止；不提供冲突解决/rebase 界面 | 提交后状态变 DIRTY/SYNCED 正确 |
| FR-REPO-04 | P2 | Reset（保留/混合/硬）——v1.1 再做，且必须先备份 ref | 备份可恢复 |
| FR-REPO-05 | P0 | **状态修复类操作**（对应 7.6）：Repair/Re-clone（INVALID）、set-upstream 三子型修复（NO_UPSTREAM）、基于 detached HEAD 建分支（DETACHED_*）、EMPTY 空仓首次推送（建 upstream）；均走预检+job+逐仓结果 | AC-REPO-08/09/10/11：四类修复各 1 个夹具，执行后状态转移正确 |

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
| FR-OPS-01 | P0 | 应用内通知中心：状态恶化、批量完成/部分失败、Token 过期、远程同步失败；（CI 事件类目 v1 空占位）；通知持久化到 `notification` 表；“清除”=删除通知行（v1） | 通知触发/已读/清除用例；`notification` 表有记录；清除后列表为空 |
| FR-OPS-02 | P0 | 日志：结构化滚动文件（**单文件 10MB × 保留 30 天**，审计日志保留 ≥90 天）、**级别过滤=显示 ≥所选级别**、搜索、暂停、一键脱敏诊断包；诊断包内容以 §12.5 清单为准（日志、脱敏配置、DB 快照、schema 版本、最近 job/sync_run、版本信息、转储清单） | 滚动/留存/脱敏/≥级别断言；诊断包含 §12.5 全部必需项 |
| FR-OPS-03 | P0 | 设置页：通用/工作区/认证/操作与安全/CI 集成（禁用占位）/实验特性；设置键、默认值、校验、生效范围见 **附录 D**；**“实验特性”分区 v1 无可配置键（仅说明/开关占位）** | 设置键表核对；非法值被拒绝并提示 |
| FR-OPS-04 | P0 | 单实例锁；双开提示不破坏数据；崩溃后 stale lock 可自愈（三条件任一：PID 不存在 / PID 非本应用 / 锁文件 mtime 超 10min 且无存活进程） | 双开用例 + 强杀后重启可自愈 |
| FR-OPS-05 | P1 | 状态栏 6 段：数据新鲜度 / 进程健康 / 操作队列 / CI 队列（v1 恒 0）/ 通知 / 网络状态。**进程健康 = Host + GitWorker 2 灯；各 BackgroundService（RemoteSync/LocalScan/StatusCalc/JOB_RUNNER）以子状态/悬浮明细呈现** | 布局快照；灯光数与映射断言 |
| FR-OPS-06 | P1 | 数据库 schema 版本 + 迁移 + 迁移前自动备份 + 损坏检测与重建（重建只需重扫） | 迁移/回滚/损坏重建用例；`PRAGMA user_version` 断言 |
| FR-OPS-07 | P2 | 自动更新（v1.1） | 更新/回滚用例（v1.1） |
| FR-OPS-08 | P0 | **首启编排与就绪门控**：三阶段固定为 `LOCAL_DISCOVERY`（本地发现）→ `REMOTE_RECONCILE`（远程对账）→ `STATUS_CALC`（状态计算）；每阶段进度事件冻结为 `{stage,total,current,cancellable}`（§11.1）；远程阶段可跳过降级；远程就绪后自动重判本地仓；**远程未就绪时不得判 INVALID/MISSING，正确态为 `UNKNOWN` + `incomplete=1`（H-01）**。**统一 §3.1/§11.1/AC-OPS-15/16 的口径为三阶段** | AC-OPS-15/16：分阶段 progress 事件断言（`LOCAL_DISCOVERY→REMOTE_RECONCILE→STATUS_CALC` + `{stage,total,current,cancellable}`）；跳过远程后进入只读，远程恢复后组件由 `UNKNOWN`（`incomplete=1`）自动重判为真实聚合态 |
| FR-OPS-09 | P1 | 错误码表（前缀 CFG/SYNC/SCAN/STATE/OP/FS/NET/GIT/SEC）与用户可读文案；错误码稳定不随文案变化 | 错误码表核对；UI 展示错误码 + 文案 |

### 9.11 CI 预留（FR-CI，v1 只占位）

| ID | 优先级 | 需求 | 验收 |
|---|---|---|---|
| FR-CI-01 | P0 | 实现 11.4 的 10 处 CI 锚点（导航/卡片/组件详情/单仓 Tab/状态栏/通知类目/筛选 facet/设置/数据契约/聚合分离） | 锚点存在性快照测试通过 |
| FR-CI-02 | P0 | 数据契约 `CiLatest`/`ComponentCiAggregate`/`ICiProvider` + `CI_UPDATED` 事件；v1 由 Mock 提供，`ci.enabled=false` 默认 | 打开 feature flag 后锚点显示 Mock 数据，结构不变 |
| FR-CI-03 | P0 | CI 独立通道：**永不参与**组件聚合与卡片底色；空/未知状态用中性样式，禁止红色表达“没数据” | 注入 CI FAIL 后卡片底色不变（视觉回归） |
| FR-CI-04 | P1 | 无 CI 时显示“CI 未接入”统一空态组件，禁止裸 TODO/白块 | 全站无裸 TODO |
| FR-CI-05 | P0 | `ci_status` 表 + `ci.*` 配置键 + 事件类型在 v1 建好（空数据） | DDL/迁移中包含 |

> 真实 CI 接入（v2）前置：确认 CI 系统（Gitea Actions / Jenkins / 其他）、查询接口、commit→run 映射、多 run 聚合规则。

### 9.12 基线（v2 占位，v1 仅记录）

- v1：切 Tag 时写 `baseline_snapshot`；四仓同 Tag 判等，不一致显示 `TAG_DRIFT`。
- v2：基线列表/冻结/漂移对比/导出/审计。需求 ID 预留 FR-BASE-001..00N。

---

## 10. 非功能需求（NFR）

### 10.1 性能与容量

| ID | 指标 | 预算（测试机：8C/16G/SSD，写死） |
|---|---|---|
| NFR-PERF-01 | 容量基线 | 远程 4,000+ 仓库 / ≈1,000 组件；本地 100+ 组件 / 400+ 仓库；压测按 4× 余量（4,000 组件）验证 |
| NFR-PERF-02 | 看板首屏可交互 | ≤1.5s（数据就绪后）；冷启动到首屏 ≤5s（不含首扫） |
| NFR-PERF-03 | 滚动 | ≥58fps 平均，单帧 ≤33ms；DOM 卡片节点 ≤120，总元素 ≤15,000 |
| NFR-PERF-04 | 搜索响应 | 防抖 120ms；输入到结果 p95 ≤300ms |
| NFR-PERF-05 | 本地扫描 | L1 指纹 400 仓 <300ms；L2 深扫仅变化仓（5% 变化时 ≤4s）；起 git 进程数限于变化仓 |
| NFR-PERF-06 | 远程同步 | 清单水位 80 请求 ≈2–8s（10min 周期）；refs 增量稳态 ≤20 请求；每日全量对账 4,000 仓 P95 ≤5min、可续传、覆盖率 100%；删除“120s 超时” |
| NFR-PERF-07 | 内存/存储 | Host 常驻 ≤300MB（不含 WebView2）；WebView2 另计 200–400MB；DB <100MB；空闲 CPU <2% |
| NFR-PERF-08 | 数据新鲜度 SLO | 本工具写操作 ≤2s；本地/IDE 变更 ≤5s；外部 commit/checkout/fetch ≤10s；远程 push/Tag ≤10min（水位）/≤6h（关水位）；UI 必须分源显示数据时间 |
| NFR-PERF-09 | 首启 | 400 仓已克隆场景 ≤30s 可用看板（先本地后远程、渐进渲染）；查询一次 ≤500 行 + 键集分页 |

### 10.2 可靠性 / 离线

| ID | 需求 |
|---|---|
| NFR-REL-01 | 断网/远程不可达：看板进入只读降级态，显示“数据截至 HH:mm”，本地扫描仍可用；恢复后自动续跑 |
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
| NFR-COMPAT-06 | 显示器 100%–200% 缩放不溢出；低端机/远程桌面自动关闭动效 |
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

## 11. 界面信息架构（含 CI 预留）

### 11.1 页面清单（App Shell + 页面）

| 层 | 页面/区域 | 说明 |
|---|---|---|
| L0 | 登录/首次配置向导、首扫进度页（**`LOCAL_DISCOVERY`→`REMOTE_RECONCILE`→`STATUS_CALC`**，可跳过远程降级）、故障页 | 不进入全局导航 |
| L1 | App Shell：自定义标题栏（产品名/版本/组织/环境）、左侧图标导航、全局工具栏（搜索/筛选/排序/视图/刷新/多选）、内容区、状态栏 | 常驻 |
| L2 | P1 看板（表格默认/卡片可选）、P2 组件详情、P3 单仓详情、P4 CI 流水线（占位页）、P5 任务中心、P6 通知抽屉、P7 日志与诊断、P8 设置 | 导航 8 项 |
| L3 | 每页七态：空/加载/错误可重试/离线降级/过期/部分失败/**权限或认证过期** | 必备 |

> **首扫进度协议（M0 冻结，H-02；与 §3.1、§9.10 FR-OPS-08、AC-OPS-15/16 逐字一致）**：阶段枚举固定为 `LOCAL_DISCOVERY` / `REMOTE_RECONCILE` / `STATUS_CALC`，按此顺序推进；每个进度事件固定字段 `{stage, total, current, cancellable}`——`stage` 只取上述三值，`total`/`current` 为当前阶段仓（或步骤）总数/已完成数，`cancellable` 表示该阶段是否支持合作式取消；远程阶段可跳过并进入只读降级，远程就绪后自动重判。

### 11.2 状态色板（双通道，唯一权威；v1 含深色）

> 通道 A（仓库生命周期）用于子仓 chip、卡片左轨与聚合徽标；**卡片底色永远中性**，禁止整卡饱和色。通道 B（CI）独立徽标，永不染卡片底色。颜色只作辅助，必须“图标 + 中文文案 + 颜色”三重编码。

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

### 11.3 组件卡片/行布局（修正 drawio 120px 不可用）

- 表格行（默认）：`[多选框] [组件 ID + 变更徽标] [聚合状态徽标] [D][I][M][S] 四 chip [分支（含不一致角标）] [Tag] [CI 槽位] [新鲜度] [⋯操作菜单]`；
- 卡片视图（可选）：宽 ≥320px，高 ≥104px；左 4px 状态轨道 + 顶部组件名 + 四仓 chip + 分支行 + 底部 SYNC/CREATE 收进 `⋯`；
- 变更标记：固定位置徽标 `●N`（不用 `*` 字符，避免与搜索/通配符冲突），tooltip 分类（已修改/新增/未跟踪）；
- 四仓分支不一致：Impl 为主 + “分支不一致”角标 + hover 四仓对照。

### 11.4 CI 预留 10 锚点（v1 交付物 = 占位 + Mock + 契约）

| # | 位置 | v1 交付物 | 不返工理由 |
|---|---|---|---|
| CI-1 | 左侧导航“CI 流水线”入口 | 路由 + 页面骨架 + 空态 + 实验标记 | 后加导航要改路由/面包屑 |
| CI-2 | 组件行/卡片 CI 徽标槽位（定高 16–20px） | 固定槽位 + “无 CI”空态 | 卡片布局最高频返工点 |
| CI-3 | 组件详情 CI 汇总条（40px） | 四仓 CI 计数 + 最近失败仓 | 头部后插会挤掉分支信息 |
| CI-4 | 单仓详情 CI Tab（第 4 位） | 空态 + 字段占位表 | Tab 顺序后加要改路由 |
| CI-5 | 状态栏 CI 队列段 | 图标 + 计数（v1 恒 0） | 状态栏分段后加要重排 |
| CI-6 | 通知中心 CI 事件类目 | 类型枚举 + 空分组 | 通知 schema 后加要迁移 |
| CI-7 | 看板筛选 “CI 状态” facet | 禁用占位 + tooltip | 筛选分组是布局契约 |
| CI-8 | 设置 “CI 集成”分区 | URL/Token/轮询/开关（disabled）+ 实验标记 | 配置项后加要改存储 |
| CI-9 | 数据契约 `CiLatest`（`componentId`/数字 `repoId`）`ci_status`/`ci_latest` 视图/`CI_UPDATED`/`ICiProvider` | Mock 实现 | 数据层后加要改表/IPC |
| CI-10 | 聚合规则显式分离：组件状态只由仓库状态决定，CI 独立字段 | 契约文档 + 单测 | 混入后再拆要重做整块 UI |

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

**CI 预留验收（可自动化）**：10 锚点全部存在（UI 快照断言）；CI 槽位定高不换行；注入 CI FAIL 后卡片底色不变；`ci.enabled=true` 后仅换数据不改结构；无裸 TODO 文本。

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

> **权威 AC 目录 = `reqs/v0.2/ac-catalog.md`（162 条 AC，覆盖 72 条 FR，含 FR→AC 追溯矩阵与红队逐项处置）**。**自 v0.2.1 起，AC 目录新编号（`AC-<域>-<NN>`，如 `AC-OP-15`）为唯一权威；旧编号仅在 §13.1 作历史映射，不得再用于验收或里程碑出口引用**；两者冲突时以 AC 目录为准。执行图集见 `reqs/v0.2/diagrams.md`（D1–D6），状态矩阵夹具见 `reqs/v0.2/fixtures/`，冻结门禁见 `reqs/v0.2/freeze-checklist.md`。

### 13.1 AC 清单（v0.1：27 条旧编号；红队复审后补；M0 冻结为 Given/When/Then 全量版）

> 〔**旧编号清单，仅作旧→新映射与历史追溯**；新编号唯一权威、旧号仅映射（映射见 `reqs/v0.2/ac-catalog.md` §1.4）。验收一律使用 ac-catalog 新编号。〕

| AC | Given/When/Then |
|---|---|
| AC-01 组件识别 | Given 远程存在 `CF325F_ChanganSigProcgBascFct_{Design,Impl,MIL,SIL}`，When 组织同步覆盖率 100%，Then 看板出现 1 个组件且 component_id = `CF325F_ChanganSigProcgBascFct`，不出现 `CF325F` 组件 |
| AC-02 缺仓 | Given 组件只有 3 仓，Then 组件 `BINDING_ERROR` + 缺失仓可见，批量操作只作用于 3 仓并逐仓提示 |
| AC-03 未绑定 | Given 组织存在 `X_Docs`、`Y_Design2`、`X_SIL_Design`，Then 全部进入“未绑定仓库”清单，不静默丢弃、不影响看板 |
| AC-04 状态矩阵 | Given 附录 C 状态矩阵夹具（14 主状态 × 关键 flag × 15 聚合态），When 扫描/聚合，Then 每个夹具命中唯一主状态与预期聚合态；任一步失败为 UNKNOWN 而非旧值 |
| AC-05 无假绿 | Given 远程不可达/超 TTL，Then 组件不得显示 SYNCED，显示 STALE/UNKNOWN 与数据时间；Given 仅离线本地事实，Then INVALID/MISSING/EMPTY/UNMERGED/IN_PROGRESS/DETACHED 可正常判定 |
| AC-06 关系判定 | Given `compare_ref` 三种来源（upstream / 同名 remote 分支 / 缺失），When 计算，Then 结果与 `merge-base --is-ancestor` 一致且区分退出码 0/1/>1；未推送 commit 不调用 compare API |
| AC-07 SYNC 保护 | Given 仓库有未推送 commit/dirty，When 执行 SYNC，Then 展示丢失清单、默认阻断、写入备份 ref/stash，确认后可从备份恢复 |
| AC-08 删除可恢复 | Given 组件存在未推送或 dirty，When 删除，Then 默认阻断；强制删除进入 `.trash` 且 30 天内可恢复；Gitea 远端零变更 |
| AC-09 部分失败 | Given 4 仓 2 成功 2 失败，Then 任务中心逐仓结果、组件 OP_PARTIAL_FAILED、可仅重试失败项；清除失败态需显式确认 |
| AC-10 切 Tag | Given 4 仓均存在、其中 3 仓有 Tag v1.0 且另 1 仓在默认分支（无该 tag），When 批量切 Tag v1.0，Then 3 仓 detached 检出并写 `baseline_snapshot`（含 tag_present），第 4 仓报告缺 tag 并跳过；组件 `TAG_DRIFT` |
| AC-11 首启三阶段 | Given 新机器，When 首次启动，Then 向导→三阶段进度（`LOCAL_DISCOVERY`→`REMOTE_RECONCILE`→`STATUS_CALC`，事件含 `{stage,total,current,cancellable}`）→ 可跳过远程进入只读降级；远程就绪后自动重判 |
| AC-12 CI 预留 | 11.4 的 5 条 CI 验收（锚点存在/卡片槽位/底色不变/Mock 开关/无裸 TODO） |
| AC-13 性能 | 按 13.3 测量方法与数据集：正常档 1,000 组件、压测档 4,000 组件，首屏 ≤1.5s、滚动 ≥58fps、搜索 p95 ≤300ms |
| AC-14 安全 | 工具自身产物（日志/诊断包/转储/命令行）扫描无 token；Gitea 请求仅 Authorization header；既有仓内嵌 token → 告警 + 脱敏展示 + 不自动改用户配置；删除/SYNC 场景 Mock 断言 Gitea 无写请求 |
| AC-15 单实例 | 双开第二个实例提示退出且不写数据；强杀后 stale lock 可自愈 |
| AC-16 状态×操作矩阵 | 7.6 矩阵抽样每行至少 1 个夹具；被禁止的操作返回明确错误码且仓库零改动 |
| AC-17 CREATE 分支 | 1 仓重名时 dry-run 明确；逐仓创建 + 部分失败可撤销；创建基线 ref 记录 |
| AC-18 修复类操作 | INVALID/NO_UPSTREAM/DETACHED/EMPTY 四类夹具各执行 Repair/set-upstream/建分支/首推，状态转移正确 |
| AC-19 恢复 | 删除后从 `.trash` 恢复；SYNC 后从 `refs/backup/*` 恢复未推送 commit；恢复前展示冲突并确认 |
| AC-20 纳入/移出管理 | 按组件 ID 纳入（校验四仓）→ 看板出现；移出后 DB 状态（archived/删除）与看板一致；均有审计 |
| AC-21 远程未就绪 | Given 跳过远程阶段，Then 本地仓不得被判为 INVALID/MISSING（正确态为 `UNKNOWN` + `incomplete=1`）；远程恢复后组件自动重判并更新 |
| AC-22 搜索/筛选/排序 | facet 枚举与排序键按 FR-BOARD-02；搜索 title/分支/Tag/SHA 命中正确；默认排序=严重度→组件 ID |
| AC-23 未绑定仓库入口 | 清单页/抽屉存在；按原因分组；可重新解析；忽略需记录理由 |
| AC-24 通知/设置/日志 | 通知持久化并已读/清除；设置键见附录 D 且非法值被拒；日志滚动/脱敏/诊断包可导出 |
| AC-25 权限/认证过期 | 401/403/Token 过期时显示权限或认证态并可重登；恢复后任务可续跑 |
| AC-26 外部 git 并发 | 外部持有 `.git/index.lock` 时工具写操作返回 BUSY、不删锁、仓库无改动 |
| AC-27 崩溃恢复 | 写操作中强制终止进程，重启后 journal reconcile 不猜测成功；重复执行幂等 |

### 13.2 测试策略

1. **Gitea fixture**：Windows 可跑的 Gitea 单二进制（`gitea.exe web` + 内置 SQLite）做 E2E；**WireMock.NET** 做契约测试（分页/`x-total-count`/429/401/5xx/超时/半包/慢响应）；
2. **本地 git 夹具生成器**：覆盖附录 C 的状态矩阵（分支/tag/annotated tag/detached/dirty/untracked/unmerged/rebase 中/shallow/LFS/大小写/长路径/中文路径/index.lock/reparse point）；
3. **状态机单测**：主状态表逐条 + 聚合全序 + UNKNOWN 兜底 + 新鲜度；判定纯函数与“真值机”对拍 + property-based 随机提交图；
4. **时间与调度**：`IClock` 假时钟，断言 15s/10min/TTL/退避曲线；
5. **端到端**：9.7/9.8 每条操作 + 崩溃恢复（Windows 强制终止进程）+ 断网 + 部分失败 + 外部 git 并发；
6. **UI**：锚点存在性快照、视觉回归（**状态×CI 组合矩阵** ≥12 张，基于 11.2 全量色板）、虚拟滚动性能、reduced-motion/深色/高对比/200% 缩放；
7. **性能**：BenchmarkDotNet 夜间基线 + 合成数据集（附录 C），按 13.3 测量方法，门禁写入 CI；
8. **安全**：token 泄漏扫描、路径穿越、危险扩展名、凭据告警、转储脱敏。

### 13.3 性能测量方法与数据集（可复现）

| 指标 | 测量方法 | 数据集/环境 |
|---|---|---|
| 首屏/滚动/搜索 | WebView2 DevTools Performance 时间线 + 滚动 10s 采帧；`keyed diff` 断言 | 4,000 组件合成数据（正常 1,000 组件 / 压测 4,000 组件两档） |
| 本地扫描 | `stopwatch` + 进程启动计数；断言不起 git 进程的轮询 | 400 个本地仓夹具（`git init` + 若干 commit） |
| 远程同步 | WireMock/Gitea 服务端计时 + 客户端 `sync_run` 记录 | 4,000 仓元数据（模拟分页 80 页）+ 429 注入 |
| DB 写入 | BenchmarkDotNet 批写基准 | 12 万 ref 行 + 400 状态行 |
| 测试机基线 | 8C/16G/SSD/Windows 11；报告注明杀软与磁盘型号 | CI runner 固定规格或人工基准机 |

### 13.4 AC 追溯矩阵（FR 分组 → 新 AC；M0 冻结；旧号仅映射）

| FR 分组 | 覆盖新 AC（权威：`ac-catalog.md` §3 FR→AC 矩阵） | 缺口处理 |
|---|---|---|
| FR-CFG | AC-CFG-01…AC-CFG-11 | — |
| FR-SYNC | AC-SYNC-01…AC-SYNC-14 | — |
| FR-SCAN | AC-SCAN-01…AC-SCAN-13 | — |
| FR-STA | AC-STA-01…AC-STA-15；跨域 AC-SCAN-09、AC-DETAIL-02、AC-OP-11/12 | — |
| FR-BOARD | AC-BOARD-01…AC-BOARD-16；跨域 AC-STA-08 | — |
| FR-DETAIL | AC-DETAIL-01…AC-DETAIL-09；跨域 AC-SAFE-10 | — |
| FR-OP | AC-OP-01…AC-OP-32；跨域 AC-SYNC-05/14、AC-SCAN-06、AC-OPS-04 | — |
| FR-REPO | AC-REPO-01…AC-REPO-11；跨域 AC-OP-05 | — |
| FR-SAFE | AC-SAFE-01…AC-SAFE-13；跨域 AC-OP-16/20/26、AC-CFG-06、AC-DETAIL-06/07、AC-OPS-06 | — |
| FR-OPS | AC-OPS-01…AC-OPS-17；跨域 AC-SAFE-06 | — |
| FR-CI | AC-CI-01…AC-CI-11；跨域 AC-DETAIL-03、AC-OPS-02 | — |
| NFR | 性能：AC-BOARD-03/05/14、AC-SCAN-08/09、AC-SYNC-12、AC-DETAIL-04；安全：AC-SAFE-05/06/07/12/13、AC-CFG-06/07；可靠/并发：AC-OPS-09/10、AC-OP-29、AC-SAFE-08/09 | — |

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
| CI 接入返工 | 整块 UI 重做 | 10 锚点 + 契约 + 聚合分离（v1 验收） |
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
| `SYNC-E003` | 覆盖不足 100% | 看板标 STALE，可见失败清单 |
| `SCAN-E001` | safe.directory 拒绝 | 提示而非放宽 |
| `SCAN-E002` | 网络盘/UNC 写禁止 | 只读展示 |
| `STATE-E001` | 数据超 TTL | 显示 STALE + 手动刷新 |
| `OP-E001` | BUSY（外部 git/index.lock） | 等待后重试，禁止删锁 |
| `OP-E002` | CONCURRENT_CHANGE（CAS 失败） | 重新预检 |
| `OP-E003` | 部分失败 | 逐仓明细 + 仅重试失败项 |
| `OP-E004` | 被状态矩阵禁止的操作 | 按 7.6 展示替代动作 |
| `FS-E001` | 路径过长/保留名/大小写冲突 | 修正路径 |
| `NET-E001` | 网络超时/断网 | 降级只读 |
| `GIT-E001` | 空仓库/unborn | 首推/初始化 |
| `SEC-E001` | 检测到既有凭据明文 | 告警并引导清理 |
