# 独立冻结复评报告 07 — SRS v0.2（红队 / 只读）

> **评审性质**：独立红队、只读。未修改 `requirements.md`、`v0.2/ac-catalog.md`、`v0.2/diagrams.md`、`v0.2/fixtures/*`、`freeze-checklist.md` 及任何既有评审文件；本文件 `reqs/review/07-freeze-review.md` 为本次唯一新建文件。
> **判定口径**：**阻断** = 不修则冻结包内部语义互相矛盾或验收不可执行，M0 会固化错误基线；**高** = 首版大概率返工/验收扯皮；**中** = 一致性/文档工程问题，可排 v1.1 但应在冻结前登记。
> **复评快照**（行号均以该快照为准，SHA256 见附表）：

| 对象 | 行数 | SHA256（前 16 位） | 最后写入 |
|---|---|---|---|
| `reqs/requirements.md`（SRS v0.2） | 1008 | `4673E403B5B2C41D` | 2026-11-14 00:35 |
| `reqs/v0.2/ac-catalog.md` | 1692 | `37EE25EB05197565` | 2026-11-14 00:36 |
| `reqs/v0.2/diagrams.md` | 1438 | `70989591353E474B` | 2026-11-14 00:36 |
| `reqs/v0.2/fixtures/state-matrix.ps1` | 1852 | `9800EC674FCBB3A2` | 2026-11-14 00:27 |
| `reqs/v0.2/fixtures/expected.json` | 182 | `54061A754F263282` | 2026-11-14 00:19 |
| `reqs/v0.2/fixtures/README.md` | 336 | `E41476EB0CA49BFE` | 2026-11-14 00:36 |
| `reqs/v0.2/freeze-checklist.md` | 92 | `5056C8C14C32C8B9` | 2026-11-14 00:35 |
| `reqs/review/06-redteam-srs.md` | 267 | `F90BF22F7A56594F` | 2026-11-14 00:01 |
| `reqs/review-report.md` | 143 | `BE6EC0F67DE1A6BE` | 2026-11-14 00:22 |

---

## 冻结复评结论

### ✗ 不可进入 M0（当前 SRS v0.2 冻结包不通过）

**理由（3 句）：**
1. **状态模型与批量安全两个基座仍有 4 处被冻结进 M0 的互相矛盾语义**：`BINDING_ERROR` 严重度自相矛盾（SRS L270 vs L288/L46；C-02）、聚合全序在 SRS 与 D3/夹具生成器之间相反（C-03）、SYNC 前置态在 §7.6 与 FR-OP-05/AC-OP-15/16/18 之间互斥（C-04）、AC 大量断言引用 SRS §8.1 不存在的字段/枚举（C-05）——按 `freeze-checklist.md` 自定门禁（“任何 P0 未关闭 / AC 不可执行 / 图集不一致 → 不得冻结”）任一即不通过。
2. **“权威 AC 目录”与 SRS v0.2 已发生分叉**：`ac-catalog.md` L8 仍声明 SRS“内容与 v0.1.1 一致”，但 SRS L26 已单方面修订 CI 契约（`componentId`/数字 `repoId`）、附录 D 与枚举；AC-CI-05（L1410）仍断言旧契约 `componentPrefix`/`repoId: string`，SRS §3.3/§13.4 与图集仍引用已作废的 AC-01…AC-27 旧编号（C-01/C-06）——冻结包内部两个“权威源”不能同时成立。
3. **夹具最小验证通过（exit 0，manifest 与 expected 对齐），但 test asset 本身把错误期望固化**：`state-matrix.ps1` L103-108 的 `AggregatePriority` 是 `BLOCKED_LOCAL=3 / MISSING=4`，与 SRS L272-273 相反（C-03）；因此当前包**不是“文档瑕疵”而是“实现/验收资产已按错误口径生成”**。建议做 1 轮 v0.2.1 修订（见“进入 M0 前必须修”Top 10）后重新冻结复评；若业务强制启动 M0，只能做“骨架 + Mock”，**不得以本 AC/图集/夹具作为验收基线**（与 06 红队结论一致）。

**问题计数**：阻断 6（C-01…C-06）、高 6（H-01…H-06）、中 15（M-01…M-15）。P0 抽查 26 条中 15 条可执行关闭、11 条部分关闭。

---

## P0 关闭抽查

> 抽查口径：从原 42 P0（RQ-01…08、G-P0-01…05、UX-01…10、P0-01…10、ARC-01…09）与 06 判定“假关闭/部分关闭”的 15 条中抽 **26 条**，覆盖状态模型、颜色、数据模型、批量安全、CI 预留、首启、AC、安全 8 个域；“关闭”= 在 SRS 正文有可判定规则且有对应新 AC；仅文档提到但验收资产仍不可执行的记为“部分关闭”。

| P0 / 来源 | SRS 位置 | 是否关闭 | 证据 | 备注 |
|---|---|---|---|---|
| RQ-01 组件身份（01；原 42） | §6.1 L165-178、D-001 L32 | **关闭** | 去尾缀全名规则 + `X_SIL_Design` 例外判据；AC-SYNC-13 L330-336 断言不绑定 `X_SIL`、不静默丢 | 与 drawio `CF325F` 旧示例已切割 |
| RQ-02 状态聚合（01；06 假关闭） | §7.2 L222-243、§7.4 L263-290、§6.2 L187 | **部分关闭** | 15 聚合态 + UNKNOWN 兜底 + 14 主状态映射齐；INVALID 统一为 BINDING_ERROR | **C-02**：`BINDING_ERROR` severity_rank=1（L270）与“缺仓中性/低严重度”（L288、D-015 L46、6.2 L185）矛盾，原 P0-05 问题未真关闭 |
| RQ-03 / UX-01 颜色（01/04；06 假关闭） | §11.2 L596-646、§10.5 L571 | **部分关闭** | 20 类状态→颜色族/图标/文案表全量、深色 token、红色纪律、CI 通道 B 独立；对比度实测大多 >4.5 | **H-04**：`hatched` 前景 `#6B7280` 叠在 `#E5E7EB` 条纹上仅 **3.9:1**（`#F3F4F6` 上 4.39:1），违反自声明 ≥4.5:1（L645） |
| RQ-04 / P0-04 数据模型（01/05；06 假关闭） | §8.1 L339-362、§8.2 L364-371 | **关闭** | 16 表齐（含 `notification` v1 使用、`event_log` v2 空表）、主键/写者/`ci_status` 列齐、`repo` 双写者拆分说明 | 备注：§8 前言 L337 要求全表带 `observed_at/data_source/is_stale`，但 8.1 多数表未列（见 H-03/S3） |
| RQ-05 / P0-01 / ARC-02 进程（01/03/05） | §12.1 L703-719、D-011 L42 | **关闭** | 2 业务进程 + WebView2；Host 唯一 DB 写者/唯一 Gitea REST 客户端；GitWorker 唯一 git.exe 执行者；Job Object 监督/熔断/单实例 | 12.2 已无“Host↔git.exe”行（图集 S5 登记已过时，M-10） |
| RQ-06 / UX-06 / ARC-08 CI 契约（01/03/04；06 部分） | §9.11 L502-510、§11.4 L655-695 | **部分关闭（阻断）** | SRS 已改 `componentId`/数字 `repoId`、`ci_status` 补 `build_number/started_at/finished_at`、`ci_latest` 视图定义、notification/event_log 表 | **C-01**：AC-CI-05 L1410 仍断言 `componentPrefix`/`repoId:string`；D6 CI-8 L1225 仍“3 键”。SRS 单边修订、AC/图集未回写 |
| RQ-07 AC/追溯（01；06 假关闭） | §13 L777-851、ac-catalog §3 L1463-1542 | **关闭** | 本次脚本核验：**72/72 FR 在 AC 追溯矩阵、162/162 AC 在矩阵与定义双向闭合、无悬空**；P0 FR 均有失败/异常路径列 | 备注 **C-06**：SRS §3.3 L123/§13.4 L834-851 与图集仍用旧 AC-01…27（AC 目录 §1.2 已宣布旧号作废） |
| RQ-08 / UX-09 删除/恢复/安全（01/04；06 部分） | FR-OP-06 L459、FR-OP-11 L464、FR-SAFE-01 L481、§12.6 L758 | **部分关闭** | `.trash` 30 天统一（D-017 L48）、Restore FR/AC、删除仅本地、dry-run/输入组件 ID/审计、realpath 校验 | **H-05**：`refs/backup/*` 保留 ≥30 天仅在 ADR D-017 出现，无 FR/AC；AC-OP-22 只测 `.trash` 保留期 |
| G-P0-01 祖先判定（02） | §7.1 L205-220、FR-STA-03 L418、AC-STA-05 L487 | **关闭** | `compare_ref` D-014 + `merge-base --is-ancestor` 0/1/>1；Gitea compare 仅限已推送 commit 兜底 | AC-STA-05/06 可执行 |
| G-P0-02 状态枚举（02） | §7.2 L222-243、§7.3 L247-261 | **关闭** | 14 主状态互斥、11 flag、`CONFLICT→DIVERGED`、`NEW_BRANCH→NO_UPSTREAM` 三子型 | — |
| G-P0-03 比较基准（02；06 假关闭） | D-014 L45、§7.1 L205-208 | **关闭** | upstream/同名 remote 分支/缺失三来源冻结；OQ-02 关闭；离线可判定集写入 7.1/7.2 前置 | AC-STA-05 可判定 |
| G-P0-04 SYNC 语义（02；06 部分） | FR-OP-05 L458、§7.6 L310-331、附录 D L986 | **部分关闭（阻断）** | fetch 目标、备份 ref、stash -u、CAS、`reset --hard`、LFS pull、拒绝本地独有分支、`remote.prune=true` 均补齐 | **C-04**：§7.6 DIRTY/DIVERGED/SYNCED 行 SYNC=❌ 与 FR-OP-05/AC-OP-15/16/18 的执行预期互斥；detached 下 SYNC 目标仍未定义（R-25 L1600） |
| G-P0-05 基线 / TAG_DRIFT（02；06 假关闭） | §7.4 L275、§9.12 L514-517、附录 C L952 | **关闭** | 基线模式作废、TAG_DRIFT 无条件计算、`tag_present` 入 `baseline_snapshot`、`^{commit}` 解引用 | AC-STA-11/OP-12/14 可执行 |
| UX-03 缺失中性（04） | §6.2 L185、§11.2 L621/L643 | **关闭** | MISSING=neutral 虚线、红色纪律禁止把 MISSING/AHEAD/DIVERGED 标红 | — |
| UX-04 / UX-07 几何（04） | §11.3 L648-653 | **关闭** | 表格默认行 + 卡片 ≥320×104 + 左 4px 轨道；历史/差异/CI 拆到 P3 独立页 | — |
| UX-05 搜索/筛选/排序（04；06 假关闭） | FR-BOARD-02 L428、FR-BOARD-04 L430、AC-BOARD-04…07 | **部分关闭** | facet/搜索字段/排序键/阈值（>10 组件或 >40 仓）已定义 | **C-05**：AC-BOARD-07 L628 依赖 `recent_activity` 字段，SRS §8.1 `component` L345 无此列 |
| UX-08 / P0-09 首启三阶段（04/05；06 假关闭） | §3.1 L100、§11.1 L591、FR-OPS-08 L499、AC-11 L795 | **部分关闭** | 三阶段口径在 4 处统一、可跳过远程、远程就绪重判、AC-OPS-15/16 落地 | **H-01/H-02**：FR-OPS-08 出现未定义状态 `UNBOUND`（L499），阶段名/进度字段仅 AC 侧冻结（R-35 L1610） |
| UX-10 / P0-03 任务中心 / UI 数据流（04/05） | FR-OP-08 L461、§12.2 L721-729、D5/D6 | **关闭** | 任务中心队列/进度/取消/重试/持久化 + UI 仅经 Host 只读查询 ≤500 行 | 备注：D5 `RunJob kind=PREFLIGHT/DRYRUN/POST_SCAN` 不在 8.1 `job.kind` CHECK（M-02） |
| P0-02 聚合依赖边（05） | §12.1 L715、D3-c | **关闭** | 明确“聚合是 Host 内纯函数/SQL 视图”，无独立进程、无启动直连边 | 图集 D3-c 与 SRS §7.4 仍有顺序冲突（C-03），属新发现 |
| P0-05 聚合漏状态（05；06 RT-02） | §7.4 L263-290 | **部分关闭（阻断）** | 14 主状态全部有落点、兜底 UNKNOWN、缺失状态补全 | **C-02**：严重度与“异常优先”排序仍不可判定，缺仓常态会排到最高档之一 |
| P0-08 批量闭环 / 状态×操作（05；06 部分） | §7.6 L310-331、FR-OP L444-465、FR-REPO-05 L475 | **部分关闭（阻断）** | 7.6 矩阵、EMPTY 首推例外、Repair/set-upstream/detached 建分支、取消语义齐 | **C-04**：矩阵与 AC-OP-15/16/18 互斥；取消状态机仅在 FR 文字，无逐状态转移表 |
| P0-10 CI 数据流 / UI 预留（05；06 部分） | §9.11 L502-510、§11.4 L655-695 | **部分关闭（阻断）** | 10 锚点 + `CiLatest`/`ComponentCiAggregate`/`ICiProvider`/`CI_UPDATED` 契约 + `ci_status`/`ci_latest` + Mock/默认 false 齐 | **C-01**：AC-CI-05/D6 CI-8 仍按旧契约/旧键数（`componentPrefix`/3 键）；CI“不返工”验收不能通过 |
| ARC-03 / ARC-04 DB 单写者/选型（03） | §12.3 L731-734、§8.2 L364-371 | **关闭** | SQLite WAL、单写队列 200ms/500 行、`PRAGMA` 参数、迁移/VACUUM 备份、排除候选库理由 | — |
| ARC-05 数据模型（03；06 部分） | §8.1 L339-362、D4 L581-851 | **部分关闭** | 16 表 DDL 语义基线、写者/读者、新鲜度/禁止写回规则 | **H-03**：D4 ER 缺 `repo.renamed_from`、`component.is_managed`、`local_state.scan_error`、`baseline_snapshot.tag_object_sha`、`job.started_at`、`ci_status` 多列；D4-1“DDL 可生成”不成立 |
| ARC-06 / ARC-07 4000 仓/30s 扫描（03） | §7.5 L292-308、NFR L523-535 | **关闭** | 分层采集 10min/30min/每日、L1 15s 指纹、L2 仅变化仓、P95 指标、删除 120s 硬超时 | AC-SCAN-07/08/09 可执行 |
| ARC-09 安全基线（03） | §10.3 L547-554、§12.6 L752-761、FR-SAFE L477-486 | **关闭** | CM/DPAPI 口径、header-only、既有仓 token 扫描告警、诊断/转储脱敏、路径/保留名/大小写安全 | 备注 **M-09**：D-006 “Bearer/token”与 12.2/12.6/AC-CFG-04 “token”写法未统一 |

**抽查统计**：26 条样本 = **15 条可执行关闭、11 条部分关闭**；其中 6 条（RQ-02/P0-05、RQ-06/UX-06/P0-10/ARC-08、G-P0-04/P0-08）直接对应阻断项 C-01/C-02/C-04。结论：**“42 P0 全关闭”的自我声明不成立**；但多数为可 1 轮文档修订关闭的语义缺口，无需推翻重建。

---

## AC 可执行性抽查

> 追溯矩阵核验（脚本）：SRS §9 定义 **72 FR**、AC 目录定义 **162 AC**；矩阵 ↔ 定义双向闭合、无悬空、无重复编号；P0 FR 均有失败/异常路径列。此项为**通过**。
> 06 红队指定 6 个“不可判定”项落地情况：AC-04→AC-STA-01/02/03（已落地，夹具+expected+property-based）；AC-05→AC-STA-04/09/14（已落地，TTL 90/1200/25200s、离线子集、非假绿）；AC-10→AC-OP-11/12/14 + AC-STA-11（已落地，第 4 仓固定 SYNCED、`tag_present=0`）；AC-11→AC-OPS-15/16（已落地，但 `UNBOUND`/阶段枚举未定义）；AC-13→AC-BOARD-03/05/14 + AC-SCAN-08/09（已落地，两档数据集+§13.3 测量方法）；AC-14→AC-SAFE-12/13 + AC-CFG-06（已落地，header-only/既有 token 告警/不自动改）。**6/6 均有新 AC 承接**，但 AC-11 依赖 H-01/H-02 修完才可判定。
> 禁止词扫描（“正常/流畅/合理/友好/秒级/实时…”）：命中仅出现在目录自身的禁止词表（L23）、对 04/R-37 的引用说明（L111、L737、L1554、L1587）与自检项（L1665），**未发现把模糊词用作断言**——此项通过。

| AC | 判定 | 缺口 |
|---|---|---|
| AC-CFG-01 | 可执行 | Given/When/Then 均落 DB/UI/错误码；字段级校验可自动化 |
| AC-CFG-07 | 可执行 | Windows 凭据 API 读取器可断言 target/尾 4 位/清除后 401 |
| AC-CFG-08 | **不可执行** | L205 断言 `job.state=PAUSED_AUTH`，SRS §8.1 L351 `job.state` CHECK 无此值；需改 SRS 枚举或 AC 改为“job 保持 RUNNING + `pause_reason=AUTH`” |
| AC-SYNC-01 | 可执行 | 80 页/`sync_run.coverage=1.0`/`component` 计数均落 DB |
| AC-SYNC-13 | 条件可执行 | L336 自认 `unbound_repo.reason` 取值集未冻结（R-13），“按 reason 分组数”无法定值 |
| AC-SCAN-05 | 可执行 | 前后 `status --porcelain=v2`/index mtime/无 gc 文件均可断言 |
| AC-STA-01 | 条件可执行 | L458 夹具含 F-PARTIAL（`expected_primary_state:null`），L460 断言“每夹具 main_status 与 expected.primary 完全相等”未写 null 例外；README L207-208 有说明但 AC 未写 |
| AC-STA-02 | **不可执行（口径冲突）** | L468 要求“严格按 §7.4 0–14 顺序”，但 D3-c L478-481/映射表 L547-549 与夹具脚本 L103-108 把 `BLOCKED_LOCAL` 排 3、`MISSING` 排 4，与 SRS L272-273 相反；且 15 组夹具无 MISSING+BLOCKED 组合，缺口不可见（C-03） |
| AC-STA-09 | 可执行 | L519-525 TTL 边界用 `CLK` 断言，阈值与附录 D 默认一致 |
| AC-STA-15 | **不可执行（AC 间互斥）** | L567-573 按 7.6 抽样：DIRTY/DIVERGED 行 SYNC=❌；但 AC-OP-15/16/18 期望 SYNC 成功，两者无法同时通过（C-04） |
| AC-BOARD-01 | **不可执行** | L582 排序依赖“`severity_rank` 暂定口径见 OQ-21”，OQ-21 暂定值（L1565：MISSING=8/EMPTY=9/DIRTY=10/DETACHED=11/MIXED=12/NEW=13）与 SRS D-020/§7.4（MISSING=12/EMPTY=13/DIRTY=8/DETACHED=10/MIXED=9/NEW=11）**不同**；且 BINDING_ERROR 中性矛盾（C-02）。断言顺序随口径二选一 |
| AC-BOARD-07 | **不可执行** | L628 Given 依赖 `recent_activity` 字段；SRS §8.1 `component` L345 无此列，DDL 无落点（C-05） |
| AC-DETAIL-04 | 可执行 | 10,000 提交夹具 + DevTools 采帧 + `git log` 序列对拍 |
| AC-DETAIL-09 | 条件可执行 | OQ-24 L889 规定 P2 “v1 只保留入口不存在”，但 L771-777 断言了完整右键菜单行为（v1.1），与“M0 只冻结入口不存在”范围不一致（H-06） |
| AC-OP-03 | **不可执行** | L802 断言 `job.state=PARTIAL_FAILED`；SRS L351 枚举为 `PARTIAL`（非 `PARTIAL_FAILED`），D5 L938/L1025 亦用 `PARTIAL_FAILED`（C-05） |
| AC-OP-15 | **不可执行** | L896 Given `A_Impl` 有 tracked 改动（DIRTY），L898 期望 SYNC 成功并 stash；§7.6 L327 DIRTY SYNC=❌（C-04） |
| AC-OP-16 | **不可执行** | L904 Given“远程已前进 1 提交（DIVERGED 前置）”、L906 期望确认后 SYNC 成功；§7.6 L323 DIVERGED SYNC=❌（C-04） |
| AC-OP-17 | **不可执行** | L914 断言 `job.succeeded=1/skipped=5`；SRS §8.1 `job` L351 无 `skipped` 列（C-05） |
| AC-OP-26 | **不可执行** | L986 断言“进行中 1 行标 `INTERRUPTED`”（`job_item`），但 `job_item.state` CHECK L352 无 `INTERRUPTED`；`INTERRUPTED` 在 `job.state` 上 |
| AC-OP-29 | **不可执行** | L1010 断言 `job_item` 的 `started_at/finished_at`；SRS `job_item` L352 无时间列（`job` 有） |
| AC-SAFE-13 | 可执行 | 抓包/argv/remote URL/哨兵扫描均可自动化 |
| AC-OPS-07 | **不可执行（版本落后）** | L1286 “共 28 键”、L1289 “附录 D 3 键（缺 `ci.token`）”；实测 SRS 附录 D 为 **31 键**且含 `ci.token`（L984）；AC-OPS-08 亦未覆盖 `dirty.includeUntracked/remote.prune/safety.requireTypedConfirm` |
| AC-OPS-15 | 条件可执行 | L1352 断言 `progress` 阶段名 `LOCAL_DISCOVERY/REMOTE_RECONCILE/STATUS_CALC`；SRS 只写中文阶段名+total/current/可取消（L499），阶段枚举未进 SRS（R-35，H-02） |
| AC-CI-05 | **不可执行（契约冲突）** | L1410 断言 `componentPrefix`、`repoId: string`；SRS §11.4 冻结契约为 `componentId: string`、`repoId: number`（L675）；AC 的 R-16 注（L1411）仍未回写（C-01） |
| AC-CI-09 | 条件可执行 | L1442 列出的 `ci_status` 列清单与 4 个 CI 设置键仍按旧版（漏 `build_number/started_at/finished_at` 列语义与 `ci.token`），断言“存在”虽不必然失败，但无法覆盖 SRS v0.2 冻结项 |

**AC 层结论**：**12/24 抽查不可执行，4 条条件可执行，8 条可执行**。主要不是“写得不细”，而是 **AC 目录按 v0.1.1 生成后未随 SRS v0.2 回写**（C-06），导致 AC 与 DDL/枚举/契约/图集互相打脸；这是本次冻结的**主要阻断来源**。

---

## 一致性缺陷

| ID | 严重级 | 位置 | 问题 | 影响 | 建议 |
|---|---|---|---|---|---|
| **C-01** | **阻断** | ac-catalog L1410-1411 vs requirements L670-692（§11.4）、SRS L26 | CI 数据契约两套：AC 断言 `componentPrefix`/`repoId:string`，SRS v0.2 已冻结 `componentId`/`repoId:number`；AC 仍把 R-16 标为“待修订”，SRS 修订记录却称已改 | M0 冻结 AC-CI-05/CI-09 与 SRS 不能同时满足；CI“不返工”承诺失效；M0 出口（CI 锚点/契约）无法判定 | 以 SRS L675 为准回写 AC-CI-05 ②③、AC-CI-09；AC 目录 L8 依据改为 v0.2；更新 R-16 状态 |
| **C-02** | **阻断** | requirements L270 vs L288、L46（D-015）、L185；ac-catalog L343；diagrams L557/L1306/L1410（S4） | `BINDING_ERROR` 的 `severity_rank=1` 与“缺仓为中性/低严重度、不参与异常优先最高排序”直接矛盾；AC-SYNC-14 要求“中性（不进入最高档）”，AC-BOARD-01 按 severity_rank 排序必把它排到最高档之一 | 原 P0-05（缺仓淹没真实故障）未真关闭；看板默认排序验收不可判定；D3-6“缺仓不排最前”验收不可能通过 | 拆分严重度：`severity_rank = 1`（含 INVALID/解析歧义）或 `12`（仅缺仓，`incomplete=1`）；在 §7.4 加 `binding_cause` 列并同步 AC-BOARD-01/AC-SYNC-14/D3-6 |
| **C-03** | **阻断** | requirements L272-273、L285；diagrams L478-481、L547-549；state-matrix.ps1 L103-108 | 聚合优先级顺序冲突：SRS 为 `MISSING=3 / BLOCKED_LOCAL=4`（并注明与评审 02 表 C 对齐），D3-c/L547-549 与夹具脚本为 `BLOCKED_LOCAL=3 / MISSING=4` | 同一“MISSING+UNMERGED”组合得到不同组件态；M0 图集与“状态矩阵夹具”资产按错误顺序生成；AC-STA-02 与资产冲突 | 统一以 SRS L272-273 为准，改 D3-c/D3-d 与脚本 L103-108；补一个“MISSING+UNMERGED”组合夹具 |
| **C-04** | **阻断** | requirements L323/L327/L328 vs L458（FR-OP-05）；ac-catalog L896/L898、L904/L906、L920-921 | SYNC 前置态互斥：§7.6 DIRTY/DIVERGED/SYNCED 的“SYNC 强覆盖”=❌（SYNCED=“无需”），但 FR-OP-05 规定 dirty 时 `stash -u` 后覆盖，AC-OP-15/16/18 全部期望 SYNC 成功 | AC-STA-15（M0 冻结）与 AC-OP-15/16/18 不能同时通过；M2 验收基线自相矛盾 | 7.6 三格改为：DIRTY=`⚠️ 需 stash -u + 确认`、DIVERGED=`⚠️ 默认阻断，备份 ref+输入组件 ID 后允许`、SYNCED=`⚠️ 仅清理 untracked 需确认`；或改写 AC-OP-15/16/18 的 Given |
| **C-05** | **阻断** | ac-catalog L914、L802、L986、L1010、L205、L794/L946、L295、L628；requirements L345/L351/L352/L354 | AC 断言引用 SRS §8.1 不存在字段/枚举：`job.skipped`、`job_item.started_at/finished_at`、`job_item.state=INTERRUPTED`、`job.state=PARTIAL_FAILED/BLOCKED/PAUSED_AUTH`、`sync_run.duration`、`component.recent_activity` | 至少 7 条 AC 不可执行或必失败；“M0 冻结后只增不改语义”会把错误断言锁死 | 要么在 8.1 补列/枚举（推荐补 `job.skipped` 计数、`job_item` 时间列、`sync_run.duration`、`component.last_activity_at`；`job.state` 统一 `PARTIAL`），要么按现有 DDL 改写 AC |
| **C-06** | **阻断** | ac-catalog L8、L1570-1612（R 清单）；requirements L26、L123、L834-851；diagrams L28-33、L1279-1340；freeze-checklist L37 | 冻结包版本口径分叉：AC 目录自称“SRS v0.2 内容与 v0.1.1 一致”；R-01…R-37 中多条已由 SRS v0.2 关闭（R-02/R-13/R-16/R-19/R-23/R-26 等）但 AC 仍按未关闭编写；SRS §3.3/§13.4 与图集仍用已作废旧编号 AC-01…AC-27 | 无法签“AC 全量冻结/追溯 100%”；M0/M2 出口引用不可解析的 AC ID；复评与冻结各说一套 | 以本次修订一次性回写 AC：更新 L8、逐条关闭 R 清单、旧 AC→新 AC 全量替换（按 ac-catalog §1.4 映射），并在 SRS §3.3/§13.4/图集 §1.2/§8.2 使用新编号 |
| **H-01** | 高 | requirements L499（FR-OPS-08）；ac-catalog L1360-1361、L1611（R-36） | `UNBOUND` 在 FR-OPS-08 验收与 AC-OPS-16 出现，但 §7.2/§7.4/附录 A 均无该状态，也未定义“远程未就绪”中间态 | AC-OPS-15/16（M0 冻结）断言“不得为 INVALID/MISSING”时无法指向合法期望值；实现可自选语义 | 改用 `UNKNOWN + incomplete=1`（推荐）或正式新增组件级中间态并同步 §7.4/附录 C/AC；删除 UNBOUND 一词 |
| **H-02** | 高 | requirements L499、§11.1 L591；ac-catalog L1352、L1610（R-35）；diagrams L1291 | 阶段枚举/进度协议只在 AC 与图集出现（`LOCAL_DISCOVERY/REMOTE_RECONCILE/STATUS_CALC` 与 `{stage,total,current,cancellable}`），SRS 只写中文阶段名 | M0 冻结 AC-OPS-15/16 的“严格阶段序列”无 SRS 依据；实现可另用命名导致 AC 失败 | 在 SRS §11.1 或 FR-OPS-08 冻结进度协议字段与枚举，并让 §3.1/§11.1/AC-11/AC-OPS-15/16 逐字一致 |
| **H-03** | 高 | requirements L337、L345-360；diagrams L589-743、L814-831 | §8 前言要求“所有行必须带 `observed_at/data_source/is_stale`”，但 §8.1 多数表未列；D4 ER 缺 SRS v0.2 已加字段（`repo.renamed_from`、`component.is_managed`、`local_state.scan_error`、`baseline_snapshot.tag_object_sha`、`job.started_at`、`ci_status` 多列） | D4-1“16 表 DDL 可生成”不成立；写者/新鲜度检查清单与 8.1 对不上；迁移脚本会漏列（图集 S3 L1409 已自认） | SRS 8.1 或补三列、或把前言改为“按表定义”；D4 ER/§5.3 补齐上述字段并更新“123 属性”计数 |
| **H-04** | 高 | requirements L611（hatched token）、L645 ③、L571（§10.5） | `hatched` 前景 `#6B7280` 在 `#E5E7EB` 条纹上实测 **3.9:1**（`#F3F4F6` 上 4.39:1），低于 SRS 自声明“全部组合 ≥4.5:1” | AC-BOARD-15 的对比度自动断言必然失败；a11y 声称不成立；UNKNOWN/STALE 状态可读性差 | 前景改 `#4B5563`（对 `#E5E7EB` 为 6.1:1）或改声明并把该组合排除（不推荐）；把对比度计算脚本纳入 M0 门禁 |
| **H-05** | 高 | requirements L48（D-017）、FR-OP-05 L458、FR-OP-11 L464、附录 D L967-988；ac-catalog L949-954 | D-017 承诺“备份 ref 保留 ≥30 天”，但无 FR 定义保留/清理、附录 D 无对应键；AC-OP-22 只覆盖 `.trash` 保留期；`refs/backup/*` 清理/可见/恢复入口未入 AC | “可恢复路径”中备份 ref 的一半不可验收；`.trash` 与备份 ref 生命周期不对称 | FR-OP-11 增补“备份 ref 保留 ≥30 天（可配）+ 清理任务 + 任务中心可见/恢复”；附录 D 加 `backup.retentionDays=30`；AC-OP-30 增补断言 |
| **H-06** | 高 | requirements OQ-24 L889；ac-catalog L216-222（AC-CFG-10）、L771-777（AC-DETAIL-09）、L1087-1093（AC-REPO-07 对照） | P2 FR 的“v1 只保留入口不存在”口径下，AC-CFG-10/AC-DETAIL-09 仍冻结完整功能断言（热更新/右键菜单），而 AC-REPO-07 正确只冻结“无入口” | M0 冻结范围被 P2 功能断言污染；v1.1 才做的功能被写进 v1 验收 | 按 AC-REPO-07 模式改写：v1 只断言“入口不存在/不可用”；完整行为移入 v1.1 AC 清单，M0 不冻结 |
| **M-01** | 中 | diagrams L279（D2）vs L175/L185（D1 §2.4）；requirements L343-360 | D2 在 `LOCAL_DISCOVERY` 阶段让 Host 写 `component`/`unbound_repo`；D1/8.1 规定 `component` 唯一写者 `STATUS_CALC`、`unbound_repo` 唯一写者 `REMOTE_SYNC` | 图集内部“唯一写者”不变量 I1/R7 被 D2 自身破坏；按图实现会出现双写者 | D2 改为：本地发现只写 `repo` 本地列/`local_state`/`scan_issue`；`unbound_repo` 在 REMOTE_RECONCILE 写；`component` 由 STATUS_CALC 写 |
| **M-02** | 中 | diagrams L858-869、L1365、L871、L1419；requirements L351 | D5 称“6 类操作”实列 8 行（Clone/Pull/Checkout/Tag/SYNC/Delete/Restore/Create）；图中 `RunJob kind=PREFLIGHT/DRYRUN/POST_SCAN` 不在 §8.1 `job.kind` CHECK 集 | D5 覆盖矩阵自述不一致；若按 kind 写 job 行会触发 CHECK 失败 | 改标题为“8 类操作”；PREFLIGHT/DRYRUN/POST_SCAN 标注为内部协议、不落 `job.kind`，或正式加入枚举并同步 AC |
| **M-03** | 中 | diagrams L1225（D6 7.7 CI-8）、L1236（D6 7.8）vs requirements L984、ac-catalog L1320 | D6 写 CI-8“3 键存在”，SRS 附录 D 为 4 键（含 `ci.token`）；D6 写“进程健康 2 灯”，AC-OPS-11 ③ 断言“4 灯” | 图集与 AC 对设置/状态栏验收不一致 | D6 改 4 键（或明确 `ci.token` 仅存凭据引用）；统一状态栏健康灯数量（建议 Host+GitWorker 2 灯 + 各 BackgroundService 子状态）并同步 AC-OPS-11 |
| **M-04** | 中 | ac-catalog L1286/L1289、L1291-1297 | AC-OPS-07“28 键/3 个 CI 键”落后于 SRS 附录 D（实测 31 键/4 个 CI 键）；AC-OPS-08 未覆盖 v0.2 新增键 | 设置页 100% 键映射验收按错误清单执行；新键（`dirty.includeUntracked`/`remote.prune`/`safety.requireTypedConfirm`/`ci.token`）无 AC | 以附录 D 31 键为唯一清单回写 AC-OPS-07/08，并要求“键→控件映射 31/31” |
| **M-05** | 中 | ac-catalog L556-557（AC-STA-13）、L930（AC-OP-19）、L1598/L1601（R-23/R-26） | AC 仍称“无 `dirty.includeUntracked` 配置键/R-23 未决”“fetch prune 策略缺失/R-26 未决”，但 SRS 附录 D L985-986 已有 `dirty.includeUntracked=false`、`remote.prune=true` | 已关闭项仍被登记为未决，冻结评审误判剩余风险 | 关闭 R-23/R-26 并删除对应“暂定口径”，AC-STA-13/OP-19 按附录 D 默认值断言 |
| **M-06** | 中 | ac-catalog L1532、L1542；requirements L497 | AC 矩阵把 `FR-OPS-06` 标 P0 并统计 P0=47；SRS 标 P1，实际 P0=46 | 冻结统计“72 FR/P0=47”与 SRS 不符；检查项 2.1 的口径错误 | 回写 AC 矩阵 P 列；统计改为 P0=46（或 SRS 若确要升 P0 则改 SRS 并注明） |
| **M-07** | 中 | requirements L954/L955/L957；expected.json L153-180；ac-catalog L460 | 附录 C 对 F-LFS-MISSING/F-BUSY/F-TAG-MOVED 的 primary 写“本地事实/按实际/按关系状态”，非确定值；expected.json 固定为 SYNCED；AC-STA-01 要求“完全相等” | 附录 C 作为 SRS 无法直接推出期望值；严格按附录 C 无法判定这三条 | 附录 C 三行改为确定值（SYNCED）+ flag 语义，与 expected.json 逐字一致；或 expected 注明“按实际状态”不可做精确断言（不推荐） |
| **M-08** | 中 | fixtures/README L317-334（§9）；expected.json L4 | README §9 仍列 5 条“SRS 不一致”，其中 1/2/3/5 已由 SRS v0.2 修复（附录 C L951-963），且 §9.4 对 §7.4 顺序的描述与现行 SRS 正好相反（L327-329）；expected.json `source` 仍写 v0.1.1 | 测试资产说明与 SRS 反向；新成员会按 README 理解出错误状态模型 | 重写 README §8/§9 为“v0.2 验证记录 + 已关闭项”；expected.json `source` 改 v0.2 并注明 M0 冻结 |
| **M-09** | 中 | requirements L37（D-006）、L727、L754；ac-catalog L173 | 凭据 header 方案三种写法：`Authorization: Bearer/token`（D-006）/`Authorization: token`（12.2/12.6）/AC-CFG-04 断言 `token <PAT>` | AC-CFG-04 可能因实现选择 `Bearer` 而失败；安全契约不唯一 | 统一为 `Authorization: token <PAT>`（Gitea 支持），D-006 删“Bearer/”或注明两者等价但 AC 只验 `token` |
| **M-10** | 中 | diagrams L1408（S2）、L1411（S5） | 图集把已修项仍登记为“SRS 不一致”：S2 称 `notification` 为 v1 空表（SRS L358 已“v1 使用”）；S5 称 §12.2 有 Host↔git.exe 行（现行 12.2 L726-728 只有 GitWorker↔git.exe） | 图集“不一致登记”失真，复评会重复排查已关闭问题 | 删除/更新 S2/S5 登记；保留真正未决的 S1/S3/S4（S1 也已由 SRS L228 引用 7.6 关闭） |
| **M-11** | 中 | ac-catalog L898（CAS 三元组）、L1230（GIT_ASKPASS）、L985（Job Object）；requirements L699-701（§12 设计建议非需求）、D-008 L39 | AC 把 §12“设计建议”当作验收对象（CAS 具体三元组、GIT_ASKPASS 通道、Job Object 杀进程树） | 违反需求/方案分层（D-008）；架构若替换实现（如不同凭据通道）会触发 AC 语义变更 | AC 只断言可观测行为（“外部改动导致操作失败且零改动”“命令行无凭据”）；实现机制放 §12 不进入 AC |
| **M-12** | 中 | requirements L674-681（`CiLatest`）vs L350（`ci_status`）；diagrams L1422 | CI 契约字段与 DDL 列无映射表（`pipelineId`↔`run_id`、`url`↔`run_url`、`commitHash`↔`commit_sha`、`componentId`↔经 `repo` 推导） | v2 接入或 DDL 验收时“字段一致”无法判定；CI-9 断言模糊 | 在 §8.1 加契约↔DDL 映射表（含 `pipelineId/run_id` 等），AC-CI-09 按映射断言 |
| **M-13** | 中 | requirements FR-OPS-02 L493、§12.5 L749；ac-catalog L1174（AC-SAFE-06）、L1277（AC-OPS-06） | 诊断包必需项两处分歧（FR-OPS-02 vs §12.5），AC 取并集 8 项（R-14 未关） | 诊断包验收清单不唯一；M3 会扯皮 | 统一为 §12.5 清单 8 项并在 FR-OPS-02 改成引用 §12.5；AC 只断言该清单 |
| **M-14** | 中 | diagrams L1262（R1）、L1347（C2） | 图集自定“黑名单词 grep 为空”的门禁，但正文 L64/L449/L837/L1262/L1347 自身包含 `进程1/表2/CONFLICT` 等词（元描述），无排除规则 | 按 C2 直接执行门禁会失败；M0 图集验收脚本不可用 | 明确排除元描述行/§9 差异表，或改为 AST 级校验（仅图节点文本） |
| **M-15** | 中 | ac-catalog L458-461；expected.json L146-152；README L207-208 | AC-STA-01 未写 F-PARTIAL 的 `expected_primary_state:null` 例外，“每夹具完全相等”字面不可执行 | 表驱动测试实现者需自行猜测 null 语义，可能误判 | AC-STA-01 增加一句：“F-PARTIAL 只断言组件聚合，主状态为 null 不参与相等断言” |

---

## 图集核验（D1–D6）

> **总体**：6 张图全部存在、结构完整；共 **10 个 Mermaid 块**（D1/D4/D6 各 1，D2/D3/D5 各 2，与 C1 L1346 自述一致）；每图 **6/6/6/6/6/7 = 37 条**可检查验收（§8.2 L1275-1340）；§9.1 对原 drawio 的 **24 条缺陷**逐条给处置，原图 5 类缺陷（进程划分、表归属、状态枚举、首启时序、操作闭环）均有对应图；旧词（`进程1/表2/CONFLICT/MISS/CF325F`）只出现在“差异说明/门禁规则”元描述行，未出现在图的节点/边中（静态扫描 L1-1355 验证）——**这部分质量高，可作为 M0 图集骨架**。逐图核验：

| 图 | 一致项 | 缺口 |
|---|---|---|
| **D1** 系统上下文与进程拓扑 | 2 业务进程 + WebView2；I1–I5 五条不变量与 §12.1/§12.2 一致；§2.4 恰列 16 表且写者/读者与 §8.1 对应；进程卡 7 行含周期/超时/重试/失败动作/心跳（L159-169）；L1-6 验收与 §7.5/§12.2 数值可对 | 无阻断缺口。备注：§2.4 `notification` 行仍标“见 S2”，S2 已被 SRS v0.2 修复（M-10）；`ci_status` 在 D1 仅“v1 空表/v2 写者”，与 SRS L350 一致 |
| **D2** 启停/异常生命周期 | 单入口顺序、三阶段首启、跳过远程降级、远程就绪重判、401/403/429/断网/迁移失败/崩溃/熔断/退出清理 X1–X15 全部画出（L208-365）；监督状态机 4 态 9 迁移（L332-343）；与 D-016/FR-OPS-08 一致 | **M-01 写者冲突**（L279 在发现阶段写 `component`/`unbound_repo`）；**H-01/H-02**（`UNBOUND` 与阶段枚举未在 SRS 定义）；S2/S5 登记过时（M-10）；验收引用旧 AC-11/21/25/27（C-06） |
| **D3** 单仓状态机 + 聚合 | 14 主状态逐字与 §7.2 一致、每个转移带事件+guard；11 flag 表与 §7.3 一致；15 聚合态 + 兜底 UNKNOWN；R3-1/2/3 三条不变量清晰（L554-558） | **C-03 聚合优先级顺序相反**（L478-481/L547-549）；**C-02/S4 severity 验收（D3-6）不可通过**（L1306、L1410）；D3-d 映射表 L547-549 写 `MISSING 优先级 4` 与 SRS L272 冲突；验收引用旧 AC-04/05/06/10/16/18/21/22 |
| **D4** 数据流与 ER | 16 表齐全、PK/FK/关系 10 条；§5.3 写者/读者/新鲜度表 16 行；禁止路径（对比结果不得写 local_state/remote_ref）明确（L837）；UI 查询预算 ≤500 行/键集分页（L839-850） | **H-03 字段缺失**：ER 缺 `repo.renamed_from`、`component.is_managed`、`local_state.scan_error`、`baseline_snapshot.tag_object_sha`、`job.started_at`、`ci_status` 多列；§5.3 声称有 `observed_at` 等三列但 SRS 8.1 未列（S3，L1409）；通知写者标“生产”与 8.1 一致，但“见 S2”注过时 |
| **D5** 批量操作闭环 | 常规/破坏性两类时序、预检→dry-run→确认→journal/备份→逐仓→部分失败→重扫→审计；BUSY/CAS/取消/失败 7 条规则（L1040-1050）；与原 drawio 的“无批量闭环/无 dry-run”缺陷对应 | **M-02**：“6 类操作”实列 8 行 + `RunJob kind` 枚举未冻结；**C-04**：D5 L989 画了“dirty→stash -u→reset”路径，与 §7.6 DIRTY SYNC=❌ 冲突（图内与 SRS 冲突）；验收引用旧 AC-02/07/08/09/10/16/17/18/19/20/26/27 |
| **D6** UI 线框与数据绑定 | L0/L1/L2 页面拓扑、P1–P8、七态、状态栏 6 段、看板绑定表 17 行、CI-1…CI-10 锚点表（L1214-1229）完整；CI 空态/不染卡片底色/不参与聚合可检查；线框几何 ≥320×104 与 §11.3 一致 | **M-03**：CI-8“3 键”vs SRS 4 键；状态栏“2 灯”vs AC-OPS-11“4 灯”；验收引用旧 AC-12/13/22/23/24/25（C-06）；D6 §7.6 绑定“排序严重度←§7.4 排序列”依赖 C-02 的 severity 口径才能执行 |

**图集结论**：**修掉原 drawio 5 类缺陷的目标基本达成（可判“是”）**；但作为 M0 交付物，D3/D4/D5/D6 与 SRS 的 4 处矛盾（C-02/C-03/C-04/H-03）以及旧 AC 编号（C-06）使其**不能通过“图集与 SRS 一致”的验收**。图集验收标准本身大多可自动/半自动检查（37 条 + R1–R10 + C1–C7），但 C2 门禁与自身元描述冲突（M-14）。

---

## 夹具运行核验

**环境**：Windows + PowerShell `7.6.7` + `git version 2.56.1.windows.1` + `git-lfs/3.8.1`；未联网，未触碰真实仓库/用户全局 git 配置（脚本 L1433-1434 将 `GIT_CONFIG_GLOBAL/SYSTEM` 隔离到输出根目录内）。

**实际执行命令与结果**（本次复评实跑，2026-11-14）：

```powershell
# 1) 最小生成（指定临时目录，避免默认时间戳目录）
pwsh -NoProfile -File D:\Workspace\git_1\reqs\v0.2\fixtures\state-matrix.ps1 `
     -Root "$env:TEMP\MbdGitFixtures\freeze-check" -Only F-SYNCED
# 输出：git 2.56.1 / git-lfs 3.8.1 → expected.json 一致性检查通过（24 个 fixture）
#       [ OK ] F-SYNCED 自校验通过（primary=SYNCED / aggregate=SYNCED，12.78s）
#       fixture 总数 1 / 通过 1 / 失败 0 / 跳过 0；EXITCODE=0；墙钟 13.87s

# 2) 清理
pwsh -NoProfile -File D:\Workspace\git_1\reqs\v0.2\fixtures\state-matrix.ps1 `
     -Root "$env:TEMP\MbdGitFixtures\freeze-check" -Clean
# 输出：[ OK ] 已删除 …\freeze-check；CLEAN_EXIT=0；DIR_EXISTS=False
```

**exit code / manifest / expected 对齐**：
- `-Only F-SYNCED` **exit 0**；`manifest.json.summary = {fixture_total:1, passed:1, failed_build:0, failed_self_check:0, skipped:0, elapsed_seconds:13.33}`。
- F-SYNCED 记录：`expected_primary_state=SYNCED`、`expected_component_aggregate=SYNCED`、`expected_repos` 四仓均 SYNCED、`expected_flags=[]`；`self_check.ok=true`、`mismatches=0`、`derived_component_aggregate=SYNCED`；四仓 `repos[].self_check.ok=true`。与 `expected.json` F-SYNCED 条目（L13-19）逐项一致。
- 脚本启动时先做 `expected.json` ↔ 内部定义一致性门禁，输出“通过（24 个 fixture）”，说明 24 条期望静态副本与生成器定义在本次运行中一致。
- `-Clean` 仅删除带标记文件的目录，**exit 0** 且目录已删除；未残留。

**限制与“未能验证”**：
1. **未跑全量 24 夹具**（README L290 估 5–6 分钟；本次按任务要求只跑最小 `-Only F-SYNCED`）。因此“24/24 全绿”只采信作者记录（README L295-313、freeze-checklist §4.5），**本报告未能独立验证全量**。
2. 未构造/未验证发现层夹具（README L281-282 自认无“仓数≠4/解析歧义/大小写冲突/reparse”夹具），与 AC-STA-02 的 BINDING_ERROR 覆盖存在缺口。
3. 未运行 Mermaid 渲染器（环境无 `mermaid-cli`），图集仅做结构与文本一致性静态核验。
4. 未运行真实 Gitea/WireMock/G-E2E、未执行任何被测代码；所有“AC 可执行性”判断为**静态可达性**（断言源/字段/枚举是否存在于 SRS）而非运行结果。
5. **静态发现（重要）**：`state-matrix.ps1` L103-108 `$script:AggregatePriority` 为 `OP_PARTIAL_FAILED=0, BINDING_ERROR=1, UNKNOWN=2, BLOCKED_LOCAL=3, MISSING=4, EMPTY=5, …`，与 SRS §7.4 L272-273（`MISSING=3, BLOCKED_LOCAL=4`）相反，与 D3-c L478-481 一致——即**夹具生成器与图集互相一致、但与 SRS 相反**（C-03）。本次 F-SYNCED 不触发该组合，故运行仍 green，属于“夹具覆盖不足掩盖的口径错误”。
6. `.trash`/备份 ref/长路径/中文路径/大小写等未被本脚本构造（README L285-287），相关 AC 仍需工作区级夹具。

---

## 进入 M0 前必须修（Top 10，按阻断/高）

| # | 严重级 | 必修项 | 验收（修完如何证明） |
|---|---|---|---|
| 1 | **阻断** | **回写 AC 目录到 SRS v0.2**：AC-CI-05 改 `componentId`/数字 `repoId`；AC-OPS-07 改 31 键/4 CI 键；关闭 R-02/R-13/R-16/R-19/R-23/R-26；AC 目录 L8 依据改 v0.2；AC-STA-13/OP-19/CI-09 按附录 D 新键/新列重写（C-01/C-06/M-04/M-05） | 重新生成“SRS↔AC”差异报告为 0；AC-CI-05 断言与 SRS L675 逐字一致；AC 目录 L8 不再出现 v0.1.1 |
| 2 | **阻断** | **拆分 `BINDING_ERROR` 严重度**：`binding_cause=INVALID/AMBIGUOUS` 用 `severity_rank=1`；`binding_cause=INCOMPLETE`（仅缺仓）用 `severity_rank=12`；同步 §7.4 表格/ L288 措辞/ AC-BOARD-01/AC-SYNC-14/D3-6（C-02） | AC-SYNC-14“中性”与 AC-BOARD-01 排序在同一 severity 口径下同时通过；补“缺仓 + 真实故障”混合排序夹具 |
| 3 | **阻断** | **统一聚合优先级**：以 SRS L272-273（`MISSING=3 / BLOCKED_LOCAL=4`）为准，修 D3-c L478-481、D3-d L547-549 与 `state-matrix.ps1` L103-108；补 `MISSING+UNMERGED` 组合夹具（C-03） | 三处常量一致；新增组合夹具的 `expected_component_aggregate` 由 SRS 表可推导 |
| 4 | **阻断** | **修 SYNC 前置态矩阵**：§7.6 DIRTY=`⚠️ stash -u+确认`、DIVERGED=`⚠️ 默认阻断+备份 ref+输入 ID`、SYNCED=`⚠️ 仅清理 untracked 需确认`；同步 AC-OP-15/16/18 的 Given/Then 与 AC-STA-15 抽样（C-04） | AC-STA-15 与 AC-OP-15/16/18 无互斥；7.6 每格可在 AC 中找到至少 1 条对应用例 |
| 5 | **阻断** | **AC 字段/枚举对齐 DDL**：`job.state` 统一 `PARTIAL`（AC-OP-03/D5）；`job` 增 `skipped` 计数或 AC-OP-17 改断言 `job_item.state=SKIPPED`；`job_item.state` 增 `INTERRUPTED` 或 AC-OP-26 改断言 `job.state=INTERRUPTED`；`job_item` 增 `started_at/finished_at` 或 AC-OP-29 改查 `job`；`sync_run.duration` 增列或删断言；`component.last_activity_at` 增列或 AC-BOARD-07 改排序键；`job.state=BLOCKED/PAUSED_AUTH` 改为合法枚举或改为“无 job 行/暂停原因字段”（C-05） | 专项脚本：AC 中出现的 `job.*`/`job_item.*`/`sync_run.*`/`component.*` 字段名 ⊆ SRS §8.1 字段清单，枚举值 ⊆ CHECK 集；0 例外 |
| 6 | **阻断** | **统一 AC 编号与追踪**：SRS §3.3 L123、§13.4 L834-851、diagrams §1.2 L28-33 与 §8.2 L1279-1340、freeze-checklist §2.5 全部改新 AC ID；建立“旧 AC-01…27 → 新 AC”现行映射的单一来源（C-06） | 全仓 grep `AC-0[1-9]`/`AC-2[0-7]` 在 07 报告之外只剩映射表；M0/M1/M2/M3 出口引用的 AC ID 均可在 ac-catalog 定义中命中 |
| 7 | 高 | **定义首启中间态与进度协议**：FR-OPS-08 中 `UNBOUND` 改为 `UNKNOWN + incomplete=1`（或正式加入枚举）；把阶段枚举 `LOCAL_DISCOVERY/REMOTE_RECONCILE/STATUS_CALC` 与 `{stage,total,current,cancellable}` 写入 SRS §11.1/FR-OPS-08，并同步 §3.1/§11.1/AC-11/AC-OPS-15/16（H-01/H-02） | SRS 内 `UNBOUND` 出现次数=0 或已入 §7.2/§7.4；阶段名/字段在 SRS 与 AC 中逐字一致 |
| 8 | 高 | **SRS 8.1 ↔ D4 字段对齐**：补 `repo.renamed_from`、`component.is_managed`、`local_state.scan_error`、`baseline_snapshot.tag_object_sha`、`job.started_at`、`ci_status` 全列；§8 前言“全表三列”与 8.1 对齐；D4 ER/§5.3 同步；修 D2 L279 写者（M-01） | D4 与 8.1 字段清单集合差集为空；D2 图中 LOCAL_DISCOVERY 不写 `component`/`unbound_repo` |
| 9 | 高 | **颜色对比度与 CI 设置项**：`hatched` 前景改 `#4B5563`（或等效 ≥4.5:1 token）；D6 CI-8 改 4 键；统一状态栏健康灯 2/4 灯口径并同步 AC-OPS-11（H-04/M-03） | 对比度自动校验全 token 组合 ≥4.5:1；D6/AC-OPS-07/AC-OPS-11 键数与灯数一致 |
| 10 | 高 | **补备份 ref 生命周期 + 收敛 P2 范围**：FR-OP-11 增补 `refs/backup/*` 保留 ≥30 天/清理/恢复可见，附录 D 加键，AC-OP-30 增断言；AC-CFG-10/AC-DETAIL-09 按 AC-REPO-07 模式改“v1 入口不存在”，完整行为移 v1.1 AC（H-05/H-06） | 备份 ref 有 FR+AC+设置键；v1 冻结的 P2 AC 只断言入口不存在 |

**建议放行方式**：以上 1–6（阻断）全部关闭 + 7–10 中至少 7/9/10 关闭后，重新做一次“冻结包一致性复评”（同一套哈希+同一脚本核验）；在此之前 **不得签署 `freeze-checklist.md` §5.4**，`ac-catalog.md` §5.4 的冻结记录也保持“待填”。

---

## 建议的 v0.2.1 修订（可直接替换的措辞/表格行）

> 原则：以 SRS 为唯一语义源，AC/图集/夹具回写 SRS；不新增枚举（除非在 §7.2/§7.4 正式登记）。下列“位置”为当前快照行号。

1. **requirements.md L270（§7.4 BINDING_ERROR 行）**
   - 现：`| 1 | BINDING_ERROR | 仓数≠4 / 解析歧义 / 未绑定 / 任一仓 INVALID | 红「绑定异常」+ 清单 | 1 |`
   - 改为：`| 1 | BINDING_ERROR | 仓数≠4 / 解析歧义 / 未绑定 / 任一仓 INVALID；缺仓时 binding_cause=INCOMPLETE | 红「绑定异常」+ 清单 | 1（INVALID/解析歧义）/ 12（仅缺仓，中性） |`
   - 同步 L288 改为：`缺仓（binding_cause=INCOMPLETE）severity_rank=12，不参与“异常优先”最高级排序；含 INVALID 或解析歧义的 BINDING_ERROR 才按 1 排序（D-015）。`
2. **requirements.md L323/L327/L328（§7.6 SYNC 强覆盖列）**：DIVERGED 格 → `⚠️ 默认阻断，备份 ref + 输入组件 ID 后允许`；DIRTY 格 → `⚠️ 需 stash -u 并确认`；SYNCED 格 → `⚠️ 仅清理 untracked 需确认`。同步 AC-OP-15/16/18 的 Given（去掉“DIVERGED 前置”或改为“待确认后允许”）。
3. **requirements.md L611（hatched token）**：`#6B7280` → `#4B5563`（对 `#E5E7EB` 对比度 6.1:1，满足 L645/L571 的 4.5:1）。
4. **requirements.md L499（FR-OPS-08 验收）**：`远程恢复后组件由 UNBOUND/UNKNOWN 自动重判` → `远程恢复后组件由 UNKNOWN（incomplete=1）自动重判为真实聚合态`；并在 11.1 增一句：`首扫进度事件 = {stage: LOCAL_DISCOVERY|REMOTE_RECONCILE|STATUS_CALC, total, current, cancellable}（M0 冻结）`。
5. **requirements.md L123（M2 出口）与 L834-851（§13.4）**：删除 AC-01…27 旧编号，改为 `AC-OP-01/02/03/06/07/08/09/11/12/15/16/17/20/21/22/23/24/25/26/27/28/29/30/31/32` 等新 ID；§13.4 表头改为“FR 分组 → 新 AC（见 ac-catalog §3）”。
6. **requirements.md FR-OP-11 L464 增补**：`备份 ref refs/backup/sync/* 保留 ≥30 天（可配），提供任务中心可见/恢复/清理；清理策略见附录 D backup.retentionDays`；附录 D L981 后加 `| backup.retentionDays | 30 | 7–90 | 即时 |`。
7. **AC 目录 L1410（AC-CI-05 ②③）**：`repoId`→`repoId: number`；`componentPrefix`→`componentId: string`；删除“repoId:string 与 §8.1 不一致”的 R-16 说明并标“已关闭（SRS v0.2 §11.4）”。
8. **AC 目录 L1286/L1289（AC-OPS-07）**：`共 28 键`→`共 31 键`；`ci.enabled/ci.baseUrl/ci.pollIntervalSec`→`ci.enabled/ci.baseUrl/ci.token/ci.pollIntervalSec`；删除“缺 ci.token”注。
9. **AC 目录 L914（AC-OP-17 ④）**：`job.succeeded=1/skipped=5` → `job.succeeded=1、failed=0；job_item 5 行 state=SKIPPED（SRS §8.1 job_item.state 合法值）`。
10. **AC 目录 L802（AC-OP-03 ①）**：`job.state=PARTIAL_FAILED` → `job.state=PARTIAL`（SRS L351 CHECK）；D5 L938/L1025 同步改 `PARTIAL`。
11. **AC 目录 L986（AC-OP-26 ①）**：`进行中 1 行标 INTERRUPTED（SRS §12.1 用词）` → `job.state=INTERRUPTED；该 job_item.state=FAILED（不猜测成功）`。
12. **AC 目录 L1010（AC-OP-29 ①）与 SRS L352**：`job_item` 时间断言改为 `job.started_at/finished_at`；或在 SRS 8.1 `job_item` 增 `started_at/finished_at` 两列并同步 D4。
13. **AC 目录 L628（AC-BOARD-07）**：`recent_activity 字段由夹具指定` → `component.last_activity_at/status_calc_at`；或在 SRS 8.1 `component` 增 `last_activity_at` 并同步 D4/D6/AC。
14. **AC 目录 L582（AC-BOARD-01 ②）**：删除“（severity_rank 暂定口径见 §4.1 OQ-21）”，改为“按 SRS D-020/§7.4 的 severity_rank 升序（0 最高）”；删除 §4.1 OQ-21 或改为“已由 D-020 关闭”。
15. **图集 L478-481 与 L547-549、夹具脚本 L103-108**：`BLOCKED_LOCAL=4, MISSING=3`（与 SRS L272-273 一致）；D3-c 的 Q3/Q4 顺序互换，D3-d L547-549 同步；`state-matrix.ps1` L103-108 同步并重跑 `-Only F-SYNCED`+新增组合夹具验证。
16. **图集 D4 ER（L589-743）**：补 `repo.renamed_from`、`component.is_managed`、`local_state.scan_error`、`baseline_snapshot.tag_object_sha`、`job.started_at`、`ci_status.build_number/started_at/finished_at/duration_ms/failed_stage/run_url`；D2 L279 改为 `写 repo 本地列、local_state、scan_issue`。
17. **图集 D6 L1225/L1236**：CI-8 改“`ci.enabled=false / ci.baseUrl / ci.token / ci.pollIntervalSec` 4 键”；状态栏进程健康统一为“Host+GitWorker 2 灯 + BackgroundService 子状态”，同步 AC-OPS-11 ③；删除 §9.3 S2/S5（已过时）。
18. **fixtures/README.md L317-334 与 expected.json L4**：§9 改为“v0.2 已修复的 5 条 + 残留说明”；`source` 改 `requirements.md v0.2`；并在 README 增加“本生成器聚合优先级以 SRS §7.4 为准，修改需同步 L103-108”告警。
19. **附录 C L954/L955/L957**：F-LFS-MISSING/F-BUSY/F-TAG-MOVED 的 primary 改为确定值（`SYNCED`）+ flag 语义，与 expected.json 逐字一致；AC-STA-01 增加 F-PARTIAL null 例外（L460 后）。
20. **D-006 L37**：`Authorization: Bearer/token` → `Authorization: token <PAT>`（与 12.2 L727/12.6 L754/AC-CFG-04 L173 统一；Gitea 支持 token 方案）。

---

## 附：证据与复评方法

### A. 复评方法（可复核）
1. **全文逐节通读**：`requirements.md`（1008 行）§0–§15 + 附录 A–D；`ac-catalog.md`（1692 行）§1–§5；`diagrams.md`（1438 行）§1–§9；`fixtures/`（脚本 1852 行 + README 336 行 + expected 182 行）；`freeze-checklist.md`、`06-redteam-srs.md`、`review-report.md` 全文。
2. **脚本化一致性核验**（只读）：
   - FR/AC 计数与追溯闭合：SRS 定义 FR=72（P0=46/P1=22/P2=4）；AC 定义=162、唯一编号 162；矩阵→定义、定义→矩阵双向差集为空；每条 FR 均被 ≥1 AC 覆盖。
   - 附录 C 24 行 ↔ `expected.json` 24 条逐条比对（3 条主状态非确定值，见 M-07）。
   - 聚合优先级常量比对：SRS §7.4 L272-273 vs diagrams L478-481/L547-549 vs `state-matrix.ps1` L103-108。
   - 设置键计数：解析 SRS 附录 D → **31 键**（含 `ci.token`），与 AC-OPS-07“28 键/3 CI 键”冲突。
   - AC 字段/枚举存在性：对 AC 中出现的 `job.*`/`job_item.*`/`sync_run.*`/`component.*` 字段与 SRS §8.1 比对（C-05/M-07）。
   - 对比度计算：按 WCAG 2.1 相对亮度公式计算 SRS §11.2 八族浅/深 token；`hatched` 前景 `#6B7280` 在 `#E5E7EB` 上 **3.9:1**、在 `#F3F4F6` 上 **4.39:1**；其余浅色族 4.71–9.2、深色族 5.96–8.28。
   - 图集静态核验：Mermaid 块计数=10；§8.2 验收条数 6/6/6/6/6/7=37；对 L1-1355 扫描旧词（`进程1/表1/表2/表3/CF325F/CONFLICT/MISS/双阶段`），命中仅元描述行（L64/L449/L837/L1262/L1347）。
3. **夹具实跑**：见“夹具运行核验”（`-Only F-SYNCED` exit 0；`-Clean` exit 0）。
4. **人工红队判断**：以“AC 的 Then 是否落在真实存在的表/字段/枚举/错误码/夹具上”为可执行性标准；以“同一事实是否在 SRS/AC/图集/夹具中存在两个互斥定义”为冻结阻断标准。

### B. “未能验证”清单（明确声明，不猜测）
- 未运行全量 24 夹具（仅最小 F-SYNCED），因此“24/24 全绿”仅为作者记录，本报告未独立复现（`freeze-checklist.md` §4.5 数据可作参考）。
- 未在 Windows 目标机上运行 AC 自动化（无被测程序/无 M0 代码）；AC 判定为静态可执行性，不是运行结果。
- 未运行真实 Gitea、WireMock.NET、`G-E2E`，未验证 80 页分页/429/401/403/慢响应等契约注入。
- 未运行 Mermaid 渲染器（环境无 `mermaid-cli`）；图集“可渲染”仅静态结构检查。
- 颜色对比度仅按 token 值计算，未渲染真实组件/考虑字体字重与阴影；`hatched` 为最明确的一处不达标。
- 未验证 `-Only 'F-TAG*'`、`-SkipLfs`、Gitea/DB 模拟注入等次路径（任务只要求最小验证）。

### C. 结论对冻结签收的影响
- `freeze-checklist.md` 当前自评“有条件通过”（L12-14）**不成立**；其 §1.2/§1.5/§1.6/§1.8 的 ☑ 与本报告 C-01/C-02/C-05/H-05 冲突，应改为 ☐ 并记录复评证据。
- `ac-catalog.md` §5.4 冻结记录、`freeze-checklist.md` §6 签收表应保持空白，待 v0.2.1 + 复评通过后再签。
- 若业务方强行进入 M0：允许范围仅限“App Shell/导航/设计 token/CI 占位锚点/Mock 数据/假 Gitea/测试基建骨架”，且不得以本报告之外的口头解释替代 AC；**状态模型、聚合、SYNC、CI 契约四类实现不得以当前 v0.2 为验收依据**（与 06 红队第 14 行结论一致）。
