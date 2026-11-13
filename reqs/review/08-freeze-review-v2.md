# 独立冻结复评报告 08（第二轮 / 红队 / 只读）— SRS v0.2.1

> **评审性质**：独立红队、只读。未修改 `requirements.md`、`v0.2/ac-catalog.md`、`v0.2/diagrams.md`、`v0.2/fixtures/*`、`v0.2/freeze-checklist.md` 及任何既有评审文件；本次唯一新建文件 = 本文件。夹具运行仅写 `%TEMP%\MbdGitFixtures\*` 并在同一轮 `-Clean` 删除；未联网、未触碰真实仓库与用户全局 git 配置。
> **判定口径**：**阻断** = 冻结包内部语义互斥或 M0 验收不可执行；**高** = 首版大概率返工/验收扯皮，须冻结同批修；**中** = 一致性/文档工程问题，可在 M0 内或 v1.1 前登记；**低** = 措辞/工具化问题。
> **复评对象快照**（2026-11-14 01:01:31 复核，评审期间哈希未变）：

| 对象 | 行数 | SHA256 前 16 位 | mtime |
|---|---|---|---|
| `reqs/requirements.md`（SRS v0.2.1） | 1040 | `1A0C02AB8407D8A2` | 00:56:13 |
| `reqs/v0.2/ac-catalog.md`（AC v0.2.1） | 1714 | `11760D84709785DB` | 00:57:39 |
| `reqs/v0.2/diagrams.md`（D1–D6 v0.2.1） | 1470 | `7037A7788F6B347C` | 00:54:27 |
| `reqs/v0.2/fixtures/state-matrix.ps1` | 1869 | `15C88D0D4F3E261A` | 00:52:57 |
| `reqs/v0.2/fixtures/expected.json` | 190 | `657C3FE6AA4B17ED` | 00:51:39 |
| `reqs/v0.2/fixtures/README.md` | 376 | `3E1CDCCE6E95052E` | 00:53:39 |
| `reqs/v0.2/freeze-checklist.md` | 92 | `167C5B0079E843E3` | 00:58:16 |
| `reqs/review/07-freeze-review.md`（上轮） | 271 | `8222118284714E3F` | 00:44:45 |

---

## 最终冻结结论

### ☑ 有条件进入 M0（不建议“无条件签署冻结”；条件见“冻结建议”，均为行级/清单级修复）

**理由（3 句）：**
1. 上轮 6 个阻断已按“SRS 唯一语义源 + AC/图集/夹具回写”真正收敛：`binding_cause` 严重度拆分、聚合全序 `MISSING=3/BLOCKED_LOCAL=4`、§7.6 SYNC 三格、§8.1 补列/枚举、CI 契约与旧 AC 编号均可在多文件逐字对上；本次独立实跑 `F-MISSING+BLOCKED`（primary=MISSING/aggregate=MISSING）与 `F-SYNCED`（1/1）均 exit 0，`-Clean` exit 0，C-03 的“资产按错误口径生成”已被实测证伪。
2. 但冻结包仍有 **2 个高危新遗留**：`F-MISSING+BLOCKED` 只加进了脚本/expected/图集，**SRS 附录 C 仍 24 行、AC-STA-01 仍写“其余 23 个夹具”**（与交付的 25 个夹具计数冲突，AC-STA-01 按字面执行会漏测/误判）；D4 ER 声称“§8.1 全列”，实际缺 `job_item.message` 与 `sync_run.error` 两列（AC-OP-10/19、AC-SYNC-03/08 直接断言该两列），H-03 的验收口径“差集为空”未满足。
3. 另有 8 处中/低一致性问题（设置表重复键行、图集过时登记、freeze-checklist 版本与“25/25”证据混淆、§8.3 映射缺 `repoType` 等）与 1 个可接受的开放项 R-06（错误码全表本就是 M0 出口交付物）；这些均不推翻需求语义，但**在修完 N-01/N-02 并把 R-06 列入冻结记录条件之前，不应签署 freeze-checklist §6 与 ac-catalog §5.4 的“通过”**。

**计数**：C-01…C-06 = **6/6 已关闭**（C-03 遗留登记为 N-01，C-06 遗留登记为 N-06）；H-01…H-06 = **5/6 已关闭，H-03 部分关闭**（遗留 N-02）。新引入：**阻断 0、高 2（N-01/N-02）、中 5（N-03…N-07）、低 4（N-08…N-11）**。AC 抽查 15 条 = **13 条可执行、2 条条件可执行（AC-STA-01、AC-STA-12）**。夹具：**两次最小实跑 exit 0 + `-Clean` exit 0**；全量 25/25 **未能验证**（作者记录为 24 旧运行 + 2 次 `-Only`）。

---

## C-01…C-06 核对表

| 项 | 判定 | 证据 | 遗留 |
|---|---|---|---|
| **C-01** CI 契约（`componentId`/数字 `repoId`/`ci_status` 列/`ci_latest`） | **已关闭** | `requirements.md` L675（CI-9 行）、L695–L721（`CiLatest` 契约：`repoId: number`、`componentId: string`、`repoType`、`commitHash` 等）、L379–397（§8.3 契约↔DDL 映射）、L1015（`ci.*` 4 键）；`ac-catalog.md` L1406–1412（AC-CI-05 断言 `componentId`+数字 `repoId`，R-16 已关闭）、L1438–1444（AC-CI-09 全列）、L1284–1290（AC-OPS-07 32 键）；`diagrams.md` L1251（CI-8 4 键）、L1252（CI-9 全列）。全仓扫描 `componentPrefix` 仅存在于只读评审 04/06，活跃资产 0 命中 | N-07：§8.3 映射表漏 `CiLatest.repoType`（可由 `repo.repo_type` 推导但“唯一判据”未写） |
| **C-02** `BINDING_ERROR` severity 拆分（`binding_cause`；缺仓=12） | **已关闭** | `requirements.md` L47（D-015）、L188–190（§6.2 三因）、L275（§7.4 severity 列：INVALID/AMBIGUOUS/未绑定=1、仅缺仓=12）、L293（`binding_cause` 冻结判别）；`ac-catalog.md` L339–345（AC-SYNC-14 参数化 1/12，不写死）、L578–584（AC-BOARD-01 两组 BINDING_ERROR 条件化断言，明确“不得使用 OQ-21 旧暂定表”）；`diagrams.md` L480（R1）、L564（R3-2） | 无（D3-c 节点只标 1，由 R3-2 补 12；建议 M0 内把节点文本改为“1/12 按 binding_cause”） |
| **C-03** 聚合序（SRS/D3/夹具三者一致：MISSING=3、BLOCKED_LOCAL=4） | **已关闭** | `requirements.md` L277–278（MISSING=3 / BLOCKED_LOCAL=4）、L290；`diagrams.md` L484–486（R3/R4 同序）、L472；`state-matrix.ps1` L105–121（`$script:AggregatePriority` MISSING=3/BLOCKED_LOCAL=4）、L376–379（`F-MISSING+BLOCKED` 定义）；`ac-catalog.md` L467–469（AC-STA-02 0–14 常量逐字）；本次实跑该夹具 `aggregate_candidates=BLOCKED_LOCAL,MISSING,SYNCED` → 选中 **MISSING**，`mismatches=0` | **N-01（高）**：夹具清单口径未统一——SRS 附录 C 无该夹具行、AC-STA-01 仍按 24 夹具 |
| **C-04** §7.6 SYNC 三格与 AC-OP-15/16/18 一致 | **已关闭** | `requirements.md` L328（DIVERGED=⚠️默认阻断+备份 ref+输入组件 ID）、L332（DIRTY=⚠️ stash -u+确认）、L333（SYNCED=—/仅清理 untracked 需确认）、L337（冲突裁决“以本矩阵为准”）；`ac-catalog.md` L571–573（AC-STA-15 三格逐字）、L894–900（AC-OP-15 DIRTY）、L902–908（AC-OP-16 DIVERGED）、L918–924（AC-OP-18 SYNCED）；`diagrams.md` L892（D5 SYNC 破坏性）、L1015（backup ref+stash -u+reset） | 无 |
| **C-05** 字段/枚举（`job.skipped`、`job_item` 时间列/`INTERRUPTED`、`job.state=PARTIAL`、`sync_run.duration_ms`、`component.last_activity_at`、§8.3 映射） | **已关闭** | `requirements.md` L357（`job.skipped` 整数、`PARTIAL` 统一）、L358（`job_item.state` 含 `INTERRUPTED`、`started_at/finished_at`）、L360（`sync_run.duration_ms`）、L351（`component.last_activity_at`）、L379–397（§8.3）；`ac-catalog.md` L915（`job.skipped=5`）、L987（`job_item.state=INTERRUPTED`）、L1011（`job_item.started_at/finished_at`）、L629（`last_activity_at`）、L296（`duration_ms`，明确旧 `duration` 不存在）、L206（AC-CFG-08 改为 `job.state=RUNNING` 且不引用暂停态）。全仓扫描 `PAUSED_AUTH`/`recent_activity`/`sync_run.duration`（非 `_ms`）= **0 命中** | 无（`job_item.message` 的 DDL 落点问题属图集侧 N-02，非 C-05） |
| **C-06** 版本/R 清单/旧 AC 编号（SRS §3.3/§13.4、图集、AC 目录统一新编号） | **已关闭（带文档残留）** | `requirements.md` L3/L9（v0.2.1）、L126–127（M2 出口新编号）、L807（新编号唯一权威、旧号仅 §13.1 历史映射）、L864–879（§13.4 全部域前缀）；`ac-catalog.md` L6–8（依据 v0.2.1）、L59–91（旧→新映射表）、L1593（R-01…R-38：37 关闭/仅 R-06 开放）；`diagrams.md` L4/L36（只用新编号）；`freeze-checklist.md` L10（v0.2.1） | **N-06（中）**：`freeze-checklist.md` L26/L27 证据列仍引旧 `AC-11/AC-12`、L21 写 `R-01…R-37`（实为 R-38）、L1/L3 标题仍写“SRS v0.2”；SRS §13.1 保留 27 条旧号 Given/When/Then 全表（已声明历史，但非纯映射表） |

---

## H-01…H-06 核对表

| 项 | 判定 | 证据 | 遗留 |
|---|---|---|---|
| **H-01** `UNBOUND` → `UNKNOWN`+`incomplete=1` | **已关闭** | `requirements.md` L229（§7.2 前置：正确态 `UNKNOWN` 且 `component.incomplete=1`）、L525（FR-OPS-08 验收）；`ac-catalog.md` L1361（AC-OPS-16：不再使用未定义中间态命名）；`diagrams.md` L206/L304（D2）。大小写敏感扫描 `UNBOUND`（状态词）= **0 命中**（`unbound_repo` 为表名，无关） | 无 |
| **H-02** 进度协议枚举进 SRS | **已关闭** | `requirements.md` L101（§3.1 In-Scope 三阶段+`{stage,total,current,cancellable}`）、L525（FR-OPS-08）、L622（§11.1“M0 冻结”协议段，与 §3.1/§9.10/AC-OPS-15/16 逐字一致）；`ac-catalog.md` L1353/L1361；`diagrams.md` L209 | 无 |
| **H-03** §8.1/D4 字段补齐 | **部分关闭** | SRS 侧已补齐（`requirements.md` L343 改为“按表定义”、L351–366 全列、L379–397 §8.3）；D4 已补多数列（`diagrams.md` L603–608 `is_managed/last_activity_at`、L620 `renamed_from`、L649 `scan_error`、L685 `skipped`、L689 `started_at`、L695 `INTERRUPTED`、L723 `duration_ms`、L764 `tag_object_sha`、L662–677 `ci_status` 全列） | **N-02（高）**：D4 ER/§5.3 仍缺 `job_item.message`（SRS L358）与 `sync_run.error`（SRS L360）；“140 属性 = §8.1 全列”（L592/L781/L857/L1340）不成立 |
| **H-04** hatched 对比度 | **已关闭** | `requirements.md` L639 改为 `#4B5563` 叠 `#E5E7EB` 并自标 6.1:1；本次按 WCAG 公式复算 `#4B5563/#E5E7EB` = **6.1:1**、`#4B5563/#F3F4F6` = 6.87:1（旧 `#6B7280` 为 3.9:1）；`ac-catalog.md` L696（AC-BOARD-15 断言“不降低”）；`diagrams.md` L1223（UNKNOWN+hatched） | 无（全 token 组合扫描脚本仍须在 M0 门禁机执行——本次未运行） |
| **H-05** 备份 ref 生命周期 + `backup.retentionDays` | **已关闭** | `requirements.md` L49（D-017：`refs/backup/*` ≥30 天、键可配）、L490（FR-OP-11 增补保留/清理/任务中心可见/恢复，验收写明 AC-OP-30）、L1012（附录 D `backup.retentionDays=30`、7–90、即时）；`ac-catalog.md` L1014–1020（AC-OP-30 ⑥ 29 天保留/31 天回收/键名断言） | N-04：附录 D 该键重复两行（L1012、L1019） |
| **H-06** P2 范围 | **已关闭** | `requirements.md` L118（P2 口径段：v1 仅“入口不存在/不可用”，完整行为 v1.1）、L919（OQ-24）；`ac-catalog.md` L217–223（AC-CFG-10 v1 只断言入口不存在/只读）、L772–778（AC-DETAIL-09 同口径）、L1088–1094（AC-REPO-07 同模式）、L1340–1346（AC-OPS-14）、L1462–1480（§2.12 v1.1 AC 不冻结） | N-09：v1.1 草案复用 `AC-CFG-10/AC-DETAIL-09` 主编号加 `(v1.1)`，易被工具重复计数 |

---

## 剩余开放项与条件（含 R-06 判定）

**R 清单现状**：`ac-catalog.md` L1593 声明 R-01…R-38 中 **37 已关闭、仅 R-06 开放**；逐条核对 R-list（L1597–1634）与 SRS 位置一致（抽查 R-02/R-03/R-08/R-13/R-16/R-20/R-21/R-23/R-25/R-26/R-28/R-33/R-35/R-36/R-38 均有可命中的 SRS 落点）。**该声明属实**。

**R-06（附录 D 错误码表 v1 只给示例，M0 冻结全表）判定：可接受为条件（不阻断本轮冻结/进入 M0）**，理由：

1. 错误码表本身就是 **M0 出口交付物**而非 M1/M2 需求：SRS OQ-23（L918）写明“M0 冻结（附录 D 先给示例）”，附录 D 表头 L1021 写明“M0 补全表；需补可重试性/日志级别/用户文案三列”，AC §5.3 第 13 条（L1697）要求“R-06 须在 M0 冻结同批回写 AC”。把它当“阻断冻结”会与既定里程碑定义冲突。
2. 现有 AC 对 R-06 的依赖是**弱依赖**：AC-OPS-17（L1364–1370）只断言“码值稳定+前缀合法+UI 同码+四列在 M0 冻结”；AC-OP-04（L811）、AC-OP-23（L963）、AC-SAFE-13（L1232）均只断言前缀/码表一致性；当前 16 个示例码（`CFG/SYNC/SCAN/STATE/OP/FS/NET/GIT/SEC` 前缀）已覆盖全部错误路径类 AC 的引用。不构成语义矛盾，只是“表未补全”。
3. 但必须列为**冻结记录条件（非可选）**：M0 首轮交付内产出四列全表并回写 AC-OPS-17/OP-04/OP-23/SAFE-13；否则 AC-OPS-17 的 Then①“每个错误码均有码值/文案/可重试性/日志级别”在 M0 出口不可验收，构成“AC 不可执行”。

**其他仍开放但非阻断项（已登记为 N-xx，见下节）**：N-01/N-02（高，须冻结同批修）、N-03/N-07/N-08（中/低，M0 内修）、N-04/N-05/N-06（中，签署前修）。

---

## AC 可执行性抽查（15 条）

> 判定口径：Then 是否落在 SRS 现存表/字段/枚举/错误码/夹具上，且同一事实在 SRS/D3/夹具间无互斥定义。“部分上一轮判不可执行”的 12 条已全部复读原文。

| # | AC | 判定 | 证据/理由 |
|---|---|---|---|
| 1 | AC-STA-15（状态×操作抽样） | **可执行** | `ac-catalog.md` L568–574：Given 列 13 行矩阵（DETACHED 合并），Then 明确“SYNC 三格与 AC-OP-15/16/18 无互斥”，与 SRS L321–333 逐格一致；被拒操作断言 `OP-E004`+零改动 |
| 2 | AC-OP-15（SYNC BEHIND/DIRTY） | **可执行** | L894–900：BEHIND 可成功、DIRTY 走 `stash -u`+确认后成功；与 SRS L332 一致；备份 ref 断言落在 SRS L458/L490 |
| 3 | AC-OP-16（SYNC DIVERGED） | **可执行** | L902–908：默认阻断、dry-run 字段逐项、输入组件 ID 后允许且可从 `refs/backup/*` 恢复；与 SRS L328/L337 一致 |
| 4 | AC-OP-17（SYNC 跳过表） | **可执行** | L910–916：`job.skipped=5`（SRS L357 已有）、`job_item.state=SKIPPED`（SRS L358 合法）、DETACHED SYNC=❌（SRS L327）；上一轮“job 无 skipped 列”已关闭 |
| 5 | AC-OP-26（任务中心/重启） | **可执行** | L982–988：`job.state=INTERRUPTED` 与 `job_item.state=INTERRUPTED` 均为 SRS L357/L358 合法值；持久化/不重复执行可断言 |
| 6 | AC-OP-29（互斥/时间列） | **可执行** | L1006–1012：`job_item.started_at/finished_at` 已为 SRS L358 列；`OP-E001`+锁保留可断言 |
| 7 | AC-OPS-07（设置键 32） | **可执行** | L1284–1290：本次从 SRS 附录 D 提取唯一键 = **32**（`SETTINGS unique=32`），与 AC 列表 32/32 一致；重复行 N-04 不破坏键集合 |
| 8 | AC-CI-05（CI 契约） | **可执行** | L1406–1412：`componentId: string`/`repoId: number`/6 态枚举/`ICiProvider` 三方法，与 SRS L695–721 逐字；上一轮 `componentPrefix` 冲突已关闭 |
| 9 | AC-BOARD-01（默认排序） | **可执行** | L578–584：按 `severity_rank` 升序+组件 ID；仅缺仓（INCOMPLETE→12）与缺仓+INVALID（→1）两组条件化断言，与 SRS L275/L293 一致 |
| 10 | AC-BOARD-07（排序三键） | **可执行** | L626–632：`component.last_activity_at` 已在 SRS L351；空值回退 `status_calc_at`；三键升降序可断言 |
| 11 | AC-CFG-08（401 重登续跑） | **可执行** | L201–207：明确“job.state 保持 RUNNING（合法值）；不引用暂停态/暂停原因列”，与 SRS L357 一致；上一轮 `PAUSED_AUTH` 冲突已关闭 |
| 12 | AC-STA-01（附录 C 主状态） | **条件可执行（计数冲突）** | L456–462：Given 列 24 个夹具（无 `F-MISSING+BLOCKED`）、⑤ 写“其余 **23** 个夹具主状态逐条相等”；而 M0 实交付 manifest 为 **25** 个（`expected.json` L126–132、脚本 L376–379）。按字面执行会与 manifest 声明数冲突（N-01） |
| 13 | AC-SYNC-14（缺仓严重度） | **可执行** | L339–345：`binding_cause=INCOMPLETE→severity=12`、`INVALID→1` 参数化断言；`component.incomplete=1`、`job.target_json` 只含存在仓，均可落 SRS L188/L275 与 DB |
| 14 | AC-OP-30（删除/备份恢复） | **可执行** | L1014–1020：`.trash` 还原、`refs/backup/*` 恢复、29 天保留/31 天回收、`backup.retentionDays` 均在 SRS L49/L490/L1012 有定义 |
| 15 | AC-STA-12（MIXED_BRANCH） | **条件可执行（DB 落点缺失）** | L544–549：① 要求“`component` 级 `mixed_branch` 标记存在（`flags_json` 或等价字段）”，但 SRS §8.1 `component` 无 `flags_json` 列（L351），D4 `component` 也无该列（`diagrams.md` L596–609）——只有 `repo_status.flags_json`；`aggregate_status`/UI `data-mixed` 可断言，“组件级标记”落点需实现自定（N-03） |

**统计**：13 条可执行、2 条条件可执行。上一轮点名不可执行的 AC-STA-15/OP-15/16/17/26/29/OPS-07/CI-05/BOARD-01/07/CFG-08 已 **12/12 全部转为可执行**；AC-STA-01 因夹具计数口径成为新的条件项。

**追溯闭合（脚本核验）**：SRS §9 定义 FR = 72（P0=46/P1=22/P2=4）、AC 目录定义 AC = 162（域分布 CFG11/SYNC14/SCAN13/STA15/BOARD16/DETAIL9/OP32/REPO11/SAFE13/OPS17/CI11）；矩阵 72 行 × 162 唯一 AC，**FR↔矩阵、AC 定义↔矩阵双向差集为空、无重复、无悬空、P 级零不一致**。

---

## 新引入矛盾

| ID | 严重级 | 位置 | 问题 | 建议 |
|---|---|---|---|---|
| **N-01** | **高** | `requirements.md` 附录 C L958–993（24 行夹具表）；`ac-catalog.md` L456–462（AC-STA-01）、L376（AC-SCAN-04“24 个”）；`expected.json` L126–132；`state-matrix.ps1` L376–379；`README.md` L15/L261；`freeze-checklist.md` L66 | C-03 要求的“顺序锁定组合夹具”（`F-MISSING+BLOCKED`）只进了脚本/expected/图集/README，**SRS 附录 C（M0 交付基线）未增行、AC-STA-01 Given 未列、⑤“其余 23 个”与 25 个 manifest 冲突**，AC-SCAN-04 亦写 24 个 | SRS 附录 C 加 `F-MISSING+BLOCKED` 行（Design=MISSING/Impl=UNMERGED，期望 aggregate=MISSING）；AC-STA-01 Given 增列该夹具、⑤“23→24”；AC-SCAN-04“24→25”；全仓统一“25 个夹具” |
| **N-02** | **高** | `diagrams.md` L692–705（ER `job_item`）、L718–728（ER `sync_run`）、L781/L857/L1340（“全列/140 属性”声明）、L847/L849（§5.3 行）；SRS L358/L360；`ac-catalog.md` L859/L931（`job_item.message`）、L256/L296（`sync_run.error`） | D4 声称“SRS v0.2.1 §8.1 全列全部入图 / DDL 可生成”，但 ER 与 §5.3 **缺 `job_item.message` 与 `sync_run.error`**；按 D4 生成的 DDL 会让 AC-OP-10⑤/AC-OP-19⑤/AC-SYNC-03①/AC-SYNC-08② 失败（H-03 验收“差集为空”不成立） | ER 两实体各补一列（`message`、`error`），属性计数 140→142 并同步 L45/L592/L857/L1340；或明确“这两列不在 D4 ER、以 SRS 8.1 为准”并修正“全列/DDL 可生成”措辞（不推荐） |
| **N-03** | 中 | `ac-catalog.md` L544–549（AC-STA-12①）；SRS L266（§7.3 `mixed_branch` 组件级 flag）、L351（`component` 字段无 `flags_json`）；`diagrams.md` L596–609（ER `component`） | 组件级 `mixed_branch` flag 在 SRS §7.3 有定义，但 §8.1/ER 无存储列；AC 用“`flags_json` 或等价字段”兜底，违反“Then 必须落在 §8.1 表/字段”的 AC 规则，实现各自发明落点 | 二选一：① `component` 增 `flags_json`（或 `mixed_branch` 布尔列）并同步 D4/AC；② AC-STA-12 改为只断言 `aggregate_status=MIXED_BRANCH`+UI `data-mixed`（推荐，flag 作为派生值不落库） |
| **N-04** | 中 | `requirements.md` L1012 与 L1019 | 附录 D 设置表 `backup.retentionDays` **重复两行**（同一键、同默认 30，说明文字不同：L1019 写“≥30 可配”与 L1012 的“7–90”范围口径略异）；唯一键仍 32，故 AC-OPS-07 可执行，但设置表出现重复定义 | 删除 L1019 重复行（保留 L1012 的 7–90+清理说明），并在 L1019 处只保留一条来源注释；M0 前用脚本校验“键唯一” |
| **N-05** | 中 | `diagrams.md` L1444（§9.3 剩余限制①）、L1448（§9.4 建议 1）、L1268（§7.8 注）；`ac-catalog.md` L1321（AC-OPS-11③） | 图集仍登记“AC-OPS-11 仍写进程健康 **4 灯**、尚未回写”，但 AC-OPS-11 已改为“**恰 2 灯**=Host+GitWorker+BackgroundService 子状态”；图集自身也写 2 灯——**图集的“剩余限制”已过期且自相矛盾** | 删除 L1444 ① 与 L1448 “AC-OPS-11 改 2 灯”条；L1268“与 AC-OPS-11③ 的 4 项 health 快照”改为“Host/GitWorker/… 的 health 快照条目（灯=进程，子状态=服务）” |
| **N-06** | 中 | `freeze-checklist.md` L1/L3（标题“SRS v0.2”）、L12（“夹具 25 个，25/25”）、L21（`R-01…R-37`、`v0.2 修订记录`）、L26（旧 `AC-11`）、L27（旧 `AC-12`）、L67（“24/24 通过 … 341.64s”）；`README.md` L328/L344–345（明确该 24/24 是 v0.2 旧顺序历史运行） | 签收文档存在三处失真：① 版本标题仍 v0.2；② 把“v0.2 旧口径 24 全量 + 新增组合夹具 1 个 `-Only` 通过”表述为“25/25”，而 §4.5 仍记“24/24”，全量 25 在修正顺序下**未重跑**；③ 证据列引用已作废旧 AC 编号；R 清单数写 37（实为 38） | 标题/用途改 v0.2.1；R-01…R-38；`AC-11/AC-12`→`AC-OPS-15/16`、`AC-CI-01…11`；明确“全量 25 未在 v0.2.1 口径下重跑”或补跑后写入；§4.1/4.2 状态改为“见条件”而非 ☑（至少在签字前） |
| **N-07** | 中 | `requirements.md` L379–397（§8.3）vs L702–721（`CiLatest` 含 `repoType`）；`ac-catalog.md` L1409–1411（AC-CI-05 含 `repoType`，AC-CI-09 声称“按 §8.3 映射表断言”） | §8.3 被称为“契约↔DDL 一致的唯一判据”，但 **未映射 `CiLatest.repoType`**（`ci_status` 无该列，须由 `repo.repo_type` 推导）；AC-CI-05/09 按“映射表”断言时该字段无判据 | §8.3 加一行：`CiLatest.repoType` → `repo.repo_type`（经 `ci_status.repo_id` 关联推导，不冗余存储）；AC-CI-09 增对应断言 |
| **N-08** | 低 | SRS L2/L77/L453（“默认排序=严重度**降序**”→“组件 ID 升序”）vs D-020 L52、§7.4 L290、AC-BOARD-01 L583（“`severity_rank` **升序**，数值小排前”）、D6 L1229 | “严重度降序”与“severity_rank 升序”并存；虽然语义上可解释为“最严重优先=rank 升序”，但两处直接措辞相反，易被实现按字面做成 rank 降序 | 全文统一为“按 `severity_rank` **升序**（数值越小=严重度越高）+ 组件 ID 升序”；若保留“严重度降序”必须括注等价关系 |
| **N-09** | 低 | `ac-catalog.md` §2.12 L1462–1480（`AC-CFG-10(v1.1)`、`AC-DETAIL-09(v1.1)`）vs §2.1 L217 / §2.6 L772（同主编号） | v1.1 草案 AC 复用主编号加 `(v1.1)` 后缀（文档已声明不计入 162），但编号正则/门禁脚本会数出 164 条定义/162 唯一，与“162 条冻结基线”“无重复编号”检查口径冲突 | v1.1 AC 用 `AC-CFG-12(v1.1)`/`AC-DETAIL-10(v1.1)` 等未占用序号（或统一加 `V11-` 前缀），由 §1.2 变更规则承接 |
| **N-10** | 低 | SRS L352（`repo.repo_type` CHECK 含 `OTHER`）vs L154（术语表 RepoType 仅 4 类）、`ac-catalog.md` L621–623（AC-BOARD-06 facet 4 类）、L1411（契约 `repoType` 4 值） | `OTHER` 是 §8.1 合法枚举值，但术语表/契约/筛选 facet 均未列，也没有 AC 断言其出现条件；实现可能写出契约不接受的值 | 明确 `OTHER` 仅用于“未绑定/无法归类仓的占位行”并补一句口径；或从 CHECK 删除（若不会存在） |
| **N-11** | 低 | `diagrams.md` §5.3 L838–855（16 表“关键列/新鲜度列”均列 `observed_at/data_source/is_stale`）vs SRS L343（“其余表按行内定义带更新时间或采集时间”） | D4 对 `app_setting/notification/operation_log` 等非事实表也硬列三列新鲜度字段，而 SRS 改为“按表定义”；D4-3 的“或等价列”可解释，但表格字面与 SRS 口径不一致，DDL 生成可能多出三列 | §5.3 对非事实表改为“`created_at/updated_at` 或行内定义（SRS §8.1）”，仅在事实类表保留三列；或 SRS 明确“允许统一附加三列” |

---

## 夹具运行核验

**环境**：Windows + PowerShell 7.6.7 + Git for Windows **2.56.1** + git-lfs **3.8.1**（均从脚本输出实测）；未联网；脚本把 `GIT_CONFIG_GLOBAL/SYSTEM` 隔离在输出根内（脚本 L33 说明、README §1），未触碰用户配置与真实仓库。

**实际执行（本次复评实跑，2026-11-14 01:00–01:03）**：

```powershell
# 1) 任务要求的顺序锁定夹具（临时目录，不联网）
pwsh -NoProfile -File D:\Workspace\git_1\reqs\v0.2\fixtures\state-matrix.ps1 `
     -Root "$env:TEMP\MbdGitFixtures\freeze-v2-20261114-010000" -Only 'F-MISSING+BLOCKED'
# 输出：expected.json 一致性检查通过（25 个 fixture）
#       [ OK ] F-MISSING+BLOCKED 自校验通过（primary=MISSING / aggregate=MISSING，10.53s）
#       Design=MISSING  Impl=UNMERGED  MIL=SYNCED  SIL=SYNCED
#       summary{fixture_total:1,passed:1,failed_build:0,failed_self_check:0,skipped:0}；EXITCODE=0，11.52s
#       self_check.ok=true；derived_component_aggregate=MISSING；
#       aggregate_candidates=BLOCKED_LOCAL,MISSING,SYNCED；mismatches=0
#       manifest SHA256 前 16 位=EE049A29C3492E9A（16502 bytes）

# 2) 清理（-Clean 只删带 .mbd-fixtures-root.json 标记的目录）
pwsh -NoProfile -File ...\state-matrix.ps1 -Root "<同一目录>" -Clean
# 输出：[ OK ] 已删除 …\freeze-v2-20261114-010000；CLEAN_EXIT=0；DIR_EXISTS=False

# 3) 补充最小验证（对照 README §8.1 第 1 行记录）
pwsh -NoProfile -File ...\state-matrix.ps1 -Root "$env:TEMP\MbdGitFixtures\freeze-v2-synced-010237" -Only F-SYNCED
# [ OK ] F-SYNCED 自校验通过（primary=SYNCED / aggregate=SYNCED，11.93s）；EXITCODE=0；summary=1/1
# 随后 -Clean：CLEAN_EXIT=0；DIR_EXISTS=False
```

**结论**：① 脚本启动的 `expected.json ↔ 脚本内定义` 一致性门禁报 **25 个 fixture** 通过（说明 expected 静态副本已含第 25 条 `F-MISSING+BLOCKED`）；② 聚合序实证正确：候选集合同时含 `BLOCKED_LOCAL` 与 `MISSING` 时选中 **MISSING**（MISSING=3 先于 BLOCKED_LOCAL=4），与 SRS §7.4 L277–278、D3-c L484–486 一致；③ 脚本与 expected、README、freeze-checklist 在“25 个夹具”上一致，唯 SRS 附录 C/AC-STA-01 落后（N-01）。

**限制与“未能验证”**：
1. **未跑全量 25 夹具**（README 估 5–6 分钟；任务只要求最小验证）：因此“全量 25/25 在 v0.2.1 新顺序下全绿”**未能验证**；`freeze-checklist.md` L12 的“25/25”与 L67 的“24/24”证据不等价（README L328/L344 明确 24/24 是 v0.2 旧顺序历史运行）。
2. 未运行 Mermaid 渲染器（环境无 `mermaid-cli`）；图集仅做文本/结构一致性静态核验，C1 渲染门禁未能验证。
3. 未实现/运行任何被测代码、Mock/WireMock/Gitea 或 UI；所有“AC 可执行性”为静态可达性判断（SRS 字段/枚举/夹具是否存在），不是执行结果。
4. 未验证 LFS 跳过路径、`-Only 'F-TAG*'` 等次路径、长/中文路径与 discovery 层夹具（README §7 自认未构造，未独立验证）。
5. 未运行全 token 对比度自动脚本（仅按公式复算 hatched 一处）。

---

## 冻结建议

**建议：可以有条件签署“冻结”（进入 M0），但不得无条件签；条件 C1–C5 在签署前关闭，C6 作为 M0 出口条件写入冻结记录。**

| 条件 | 内容 | Owner | 截止 | 验收证据 |
|---|---|---|---|---|
| **C1（对应 N-01，高）** | 统一夹具清单为 25：SRS 附录 C 增 `F-MISSING+BLOCKED` 行；AC-STA-01 Given 增列并将“其余 23”改“其余 24”；AC-SCAN-04“24 个”改“25 个” | SRS owner + AC owner | **冻结签署前** | 三文件 grep `F-MISSING+BLOCKED` 命中；`-Only F-MISSING+BLOCKED` exit 0（本次已复现） |
| **C2（对应 N-02，高）** | D4 ER/§5.3 补 `job_item.message`、`sync_run.error`；属性计数 140→142；修正“全列/DDL 可生成”措辞 | 图集 owner | **M0 DDL 基线冻结前**（建议同批于签署前） | ER 字段集 vs SRS §8.1 差集为空（专项脚本）；AC-OP-10/19、AC-SYNC-03/08 的列断言可落 DDL |
| **C3（对应 N-04/N-05，中）** | 删除附录 D `backup.retentionDays` 重复行；删除图集 §9.3/§9.4 过时的“AC-OPS-11 仍 4 灯”登记并修正 L1268 措辞 | SRS owner + 图集 owner | **冻结签署前** | 附录 D 键唯一性脚本通过（32/32）；图集 grep “4 灯”=0（除历史说明） |
| **C4（对应 N-06，中）** | freeze-checklist 标题/版本改 v0.2.1；R-01…R-38；旧 AC-11/AC-12→新编号；§4.5 如实记录“全量 25 未在 v0.2.1 口径重跑”或补跑后更新；签字前把 §4.1/4.2 由 ☑ 改为“待复评确认” | 冻结/PM owner | **签署前** | checklist diff；若补跑全量：manifest `fixture_total=25,passed=25` + 新哈希 |
| **C5（对应 N-03/N-07 及 N-08–N-11，中/低）** | AC-STA-12 组件级 `mixed_branch` 落点二选一；§8.3 补 `repoType` 映射；统一“severity_rank 升序”措辞；v1.1 AC 改用未占用编号；明确 `repo_type=OTHER` 口径；修正 D4 §5.3 非事实表新鲜度列表述 | AC/SRS/图集 owner | **M0 内、M1 入口前** | 相关 AC 原文复核 + 映射表完整性脚本 |
| **C6（R-06）** | 附录 D 错误码四列全表（码值/文案/可重试性/日志级别）M0 补全，并回写 AC-OPS-17、AC-OP-04、AC-OP-23、AC-SAFE-13 | SRS owner + AC owner | **M0 出口（AC 基线冻结前）** | 错误码表行数=全量、四列非空；AC-OPS-17 Then 可执行；冻结记录写明“R-06 为 M0 出口条件” |

**签收口径**：`freeze-checklist.md` L14 的“有条件通过”与 `ac-catalog.md` §5.4（L1702–1711）在 C1–C5 关闭后可签；`freeze-checklist.md` §6 三方签收须等 C1–C5 证据附上。C6 允许在 M0 出口前闭环（不阻塞“进入 M0”），但必须在冻结记录中显式列为条件，否则 AC-OPS-17 在 M0 出口不可判定。

**M0 放行边界（若业务方要求“立即进 M0”）**：允许做 App Shell/导航/设计 token/CI 占位/Mock/Gitea 单二进制/测试基建；**状态计算、聚合、SYNC、CI 契约四类实现必须以本报告修正后的 SRS §7.4/§7.6/§8.1 为准**，不得以 SRS 附录 C 现 24 行（缺 N-01 第 25 行）或 D4 现 ER（缺 N-02 两列）作为验收基线。

---

## 证据快照

| 文件 | 行数 | SHA256 前 16 位 | 用途 |
|---|---|---|---|
| `reqs/requirements.md` | 1040 | `1A0C02AB8407D8A2` | SRS v0.2.1（C/H 修补载体） |
| `reqs/v0.2/ac-catalog.md` | 1714 | `11760D84709785DB` | AC v0.2.1（162 AC/72 FR/R 清单） |
| `reqs/v0.2/diagrams.md` | 1470 | `7037A7788F6B347C` | D1–D6 v0.2.1 |
| `reqs/v0.2/fixtures/state-matrix.ps1` | 1869 | `15C88D0D4F3E261A` | 25 夹具生成器（本次实跑 exit 0） |
| `reqs/v0.2/fixtures/expected.json` | 190 | `657C3FE6AA4B17ED` | 25 条期望（含第 25 条组合夹具） |
| `reqs/v0.2/fixtures/README.md` | 376 | `3E1CDCCE6E95052E` | 夹具说明/验证记录/剩余限制 |
| `reqs/v0.2/freeze-checklist.md` | 92 | `167C5B0079E843E3` | 冻结自评（仍为“有条件通过”，§5.4 待执行） |
| `reqs/review/07-freeze-review.md` | 271 | `8222118284714E3F` | 上一轮问题清单（C-01…C-06/H-01…H-06） |
| 本次报告（唯一新建） | — | — | `reqs/review/08-freeze-review-v2.md` |

**评审方法可复核**：① 全文通读 SRS 1040 行、AC 1714 行、图集 1470 行、夹具脚本关键段/README/expected 全文；② 脚本化核验：FR/AC 计数与双向追溯闭合（72/162、0 悬空）、附录 D 键集合（唯一 32 键、重复 `backup.retentionDays`）、§7.3 flag=11、§7.4 聚合态=15、设置键 vs AC-OPS-07、AC 字段 vs SRS §8.1、旧词/旧 AC/`componentPrefix`/`PAUSED_AUTH`/`recent_activity` 扫描、WCAG 对比度复算；③ 夹具两次最小实跑 + `-Clean`；④ 人工红队判定（“同一事实是否在 SRS/AC/图集/夹具存在两个互斥定义”“AC 的 Then 是否落在真实存在的表/字段/枚举”）。
