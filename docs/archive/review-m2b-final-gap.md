# M2b Final 差距审计（冻结规格 `spec-v0.4` 对照，HEAD `73d09ae`）

| 项目 | 内容 |
|---|---|
| 审计对象 | 最新 HEAD `73d09ae013ea3b655a481527f102f7a67c504892`（2026-11-15 01:27，工作区干净）；冻结规格 tag `spec-v0.4`（`reqs/specification.md`，3289 行）；阶段 tag 已存在：`m2a/m2b-1/m2b-2a/m2b-2b`，**无 `m2b`** |
| 判据 | §16.9 DoD（`specification.md:1536-1550`）：①AC-OP-01…35、AC-REPO-01…11、AC-SAFE-01…13、AC-UI-10c、AC-UI-16 全部 **COVERED**（PARTIAL/GAP 不计）；②14 态×5 键矩阵在 M2b 构建复验；③`docs/m2b-fault-injection-report.md` 通过、无 P0/P1 安全缺陷；④单测全绿 + L1 必跑 + 至少一份 L2/hermetic 真实 Gitea 证据（SKIP 不计）；⑤需求/代码/UX 三方独立 Gate 签字 + 证据 manifest `docs/evidence/m2b-manifest.json`（`:1543`） |
| 方法 | 只读：`git show/log` 逐文件核对；全量 `dotnet test` 实跑（隔离 artifacts）；真实 `%LOCALAPPDATA%\MbdGitTool` 只读查询（DB 复制到 `%TEMP%`）；`docs/`、`artifacts/`、`ui/scripts`、`host.log` 证据核对；UI/测试 grep 核 AC Then |
| 边界 | 未改代码/配置/`reqs`/真实仓库；未起 Host、未跑 CDP/真机检查（真实 Host PID 80028 由并行流程占用）；未打 tag；唯一写文件为本报告 |

---

## 0. 结论（先答 lead 问题）

**剩余 P0 = 0；P1 = 6 组（明细 15 条）。当前不可进入 M2b final Gate——按 §16.9 DoD 现在送 Gate 必然 FAIL。**

唯一整体绿项：`dotnet test` **407/407**（Core 71 + Host 336）、**0 warning/0 error**（`artifacts/gate-final-test.log`）；14 态矩阵单测在套件内通过。但 DoD 还要求「全 AC COVERED + L2 真实证据 + 故障注入报告 PASS + 三方签字」，这四项均未满足（详见 §2/§3）。

| # | lead 关注项 | 现状（HEAD） | 判定 |
|---|---|---|---|
| 1 | DELETE_PENDING | 后端写 pending/重试成功有测试（`BatchOperationService.cs:754-780,1407-1446`），但**无取消 API/UI、无启动扫描重建、无 30 天 GC、审计 result 仍为 FAILED**（`JobEngine.cs:1195-1235`），§16.2 `:1464-1472` 未全满足 | **P1-4** |
| 2 | Repair/Reclone | 后端+单测存在（`BatchOperationService.cs:452-577`），但**UI 无入口、失败回滚不干净、REPAIR 硬编码 main、无 job 级/失败用例** | **P1-4** |
| 3 | CREATE undo | 仅 Router 直执行+regex 从 item.Message 抽 baseline（`HostCommandRouter.cs:428-456`），**无 `job_item.baseline_ref` 列、非 CREATE_UNDO job/journal、无 baseline ref 保留、UI 无撤销入口** | **P1-4** |
| 4 | 组件管理 | bind/unbind/store/UI/菜单已接（`ComponentManagementService.cs:36-93`、`ModalHost.vue:412-431`、`73d09ae`），但**COMPONENT_BIND 审计缺失、STOP_MANAGE 无审计、LOCAL_DELETE 部分失败仍返回 Success、无 BINDING_ERROR/默认迁移/“已移出”看板视图、bind 零单测** | **P1-5** |
| 5 | baseline_snapshot | schema v2 表+4 行 happy-path 测试（`AuditDatabase.cs:90,691`；`JobEngineTests.cs:1839-1900`），但**逐 item 即时 Upsert（`:1290,1310`）非“TAG job 收口一次写 4 行”，失败 job 会留半套；真实库 0 行** | **P1-4** |
| 6 | 故障注入 | GIT_NET_AUTH_401/DISK_SPACE_LOW/DIR_MOVE_BUSY/INDEX_LOCK 已实现且有绿测；**CRASH_BEFORE/AFTER_WRITE、CRASH_AT_ITEM_2、DISK_WRITE_FAIL、GIT_NET_404/TIMEOUT、GIT_SLOW 专项缺失；报告自述未通过**（`docs/m2b-fault-injection-report.md:9-24,36-43`） | **P1-2** |
| 7 | G-WIRE 缺失 | 全仓无 WireMock/`G-WIRE-REST/G-WIRE-GIT`；`docs/evidence/` 不存在；AC-SAFE-13 三件套 `gwire.jsonl/argv.txt/remotes.txt` 未生成（`specification.md:2799-2806,1527-1528`） | **P1-2** |
| 8 | 真机 reconcile | `host.log` 有真机 NOT_APPLIED/UNKNOWN_APPLIED 演练记录（11-14T17:04–17:28Z），审计仅存 id=452 NOT_APPLIED；但**未归档 manifest/可复跑脚本，repair 已清掉多数 RECONCILE 行，且 AC-SAFE-09 指定的 CRASH_AFTER_WRITE 注入未实现** | **P1-1/P1-2** |
| 9 | M2b1 real 自检失败 | 最新提交 `73d09ae` 自述“**M2b1 real cancel checks fail in current env (to investigate)**”，无结果文件/无修复；`artifacts/m2b1-fix-selfcheck/result.json` 是 21:43 旧 Mock 68/68，非 HEAD 真实证据 | **P1-1（AC-UI-10c）** |

---

## 1. 基线证据（本轮实测）

1. HEAD `73d09ae`、工作区干净；tag 列表无 `m2b`。`git log m2b-2b..HEAD` 共 23 提交，主体为 M2b 增量（DELETE_PENDING/Repair/Reclone/组件管理/CREATE undo/baseline/注入）与规格冻结。
2. 全量测试：`dotnet test MbdGitTool.sln -c Debug --artifacts-path artifacts/gate-final` → **407/407 通过（0 失败/0 跳过）、0 warning/0 error**。
3. 真实库（只读副本）：`operation_log` **468** 行、`user_version=2`；`RECONCILE` 仅 **id=452 NOT_APPLIED**；`baseline_snapshot`=**0 行**、`component_management`=**0 行**；`REPAIR/RECLONE/CREATE_UNDO/COMPONENT_BIND/COMPONENT_UNBIND` 审计 **0 行**（新功能无任何真实环境证据）。
4. 证据资产：`docs/evidence/` **不存在**；`artifacts/m2b2-realhost/result.json` = `{skipped:true, reason:"Gitea unreachable"}`（**SKIP 不计 COVERED**）；`artifacts/m2b2-selfcheck/result.json` 53/53 为 Mock CDP，且 check 列表**不含**组件管理/Repair/undo/pending（均为离线/EMPTY/审计/备份/任务中心等旧能力）；无 `review-m2b-final-*` 三方签字报告；`docs/m2b-ac-op-gap.md` 仍写“407 测试/AC-REPO-01…11 GAP”，**未更新到 HEAD、且全文无 AC-OP-34/35/manifest 章节**。

---

## 2. DoD 逐项判定（§16.9）

| DoD | 要求 | 当前证据 | 判定 |
|---|---|---|---|
| 1 | 全部 AC COVERED + evidence manifest | 无 `docs/evidence/m2b-manifest.json`；`docs/m2b-ac-op-gap.md` 台账过期（未含 AC-OP-34/35、把已实现的 REPAIR/RECLONE 写成 GAP）；多条 AC 仅单测无 AC 指定断言 | **FAIL** |
| 2 | 14 态矩阵 M2b 复验 | `RepoActionAvailabilityMatrixTests` 在 407 绿；`cdp-m2a-selfcheck.mjs` 存在，M2a 11/11 仅提交说明，无归档结果 | **PARTIAL** |
| 3 | 故障注入报告通过、无 P0/P1 安全缺陷 | 报告 `docs/m2b-fault-injection-report.md` 自述“尚未通过”，列 3 个 GAP、7 个 PARTIAL | **FAIL** |
| 4 | 单测全绿 + L1 必跑 + 至少一份 L2/hermetic 真实 Gitea 证据（SKIP 不计） | 单测 407/407 绿；但 realhost=**SKIP**、无 hermetic Gitea（G-E2E）/WireMock、无证据 manifest；M2b1 real cancel 失败未查清 | **FAIL** |
| 5 | 需求/代码/UX 三方独立 Gate 签字 | 无 `review-m2b-final-*` 报告/签字；本轮仅本差距审计 | **FAIL** |

---

## 3. 剩余 P1 阻断明细（证据 + 最小修复）

### P1-1 证据门禁：L2/真实证据、manifest、台账、M2b1 real 取消失败（DoD 1/4/5，AC-OP-34、AC-UI-10c）
- 证据：`artifacts/m2b2-realhost/result.json` `skipped:true`（Gitea 不可达，只有 SKIP）；无 `docs/evidence/m2b-manifest.json`（`specification.md:1543`）；`ui/scripts/run-selfchecks.mjs:20-46` 只在控制台打印 SKIP/ALL_PASSED，不写 manifest、不区分/汇总 PASS·SKIP·FAIL，`probeGitea` 失败不影响退出码；提交 `73d09ae` 自述 M2b1 real cancel checks fail；无 final Gate 签字。
- 最小修复：①起 hermetic Gitea 或 WireMock（G-E2E）跑 `cdp-m2b2-realhost-check.mjs`；②在 `run-selfchecks.mjs` 生成 §16.9 schema 的 `docs/evidence/m2b-manifest.json`（commit/时间/PASS·SKIP·FAIL/checks）；③定位并修复 M2b1 real cancel 失败（AC-UI-10c/AC-OP-27）并归档 result；④把 `docs/m2b-ac-op-gap.md` 更新到 HEAD（含 AC-OP-34/35）。

### P1-2 故障注入 / G-WIRE 未闭合（DoD 3；AC-OP-26/27/35、AC-SAFE-04/07/09/13）
- 证据：已实现且有绿测的仅 `GIT_NET_AUTH_401`（`M2b2bClonePreflightTests.cs:59`）、`DISK_SPACE_LOW`（`BatchOperationService.cs:375-380`、测试 `:30`）、`DIR_MOVE_BUSY`（`:754-760`）、`INDEX_LOCK`（`JobEngine.cs:745-753`）；`GitCli.cs:64-86` 只注入 401/404/TIMEOUT/SLOW 文本，404/TIMEOUT/SLOW **无专项用例**；`CRASH_BEFORE_WRITE/CRASH_AFTER_WRITE/CRASH_AT_ITEM_2/DISK_WRITE_FAIL` 未实现；`#if DEBUG` 在 Release 直接不生效但**无告警日志**（AC-OP-35 ③）；全仓无 WireMock/G-WIRE，`docs/evidence/security/*` 不存在。报告第 9–24 行逐点标 PARTIAL/GAP、第 36–43 行自述未通过。
- 最小修复：实现缺失注入点（CRASH_* 用子进程自终止或注入点挂起+外部杀、DISK_WRITE_FAIL 在写审计/ref 时抛错）＋为 404/TIMEOUT/SLOW 补确定性用例；Release 分支加告警日志；实现最小 G-WIRE-REST/GIT 记录器并生成 `gwire.jsonl/argv.txt/remotes.txt`；重写报告至 PASS。

### P1-3 AC-SAFE-08：`job_item` pre/expect/post 三元组缺失（FR-SAFE-04 P0）
- 证据：`StoredJobItem` 只有 `PreHead/PostHead`（`JobStore.cs:191-206`，无 `ExpectHead`），且 `PreHead` 仅在 item 收口时从 `BatchItemResult` 回填（`JobEngine.cs:1195-1235`），**写入前未落库**；AC-SAFE-08（`specification.md:2759-2765`）①执行前 `pre_head` 已落库、②`expect_head`、③完成后 `post_head` 均无法断言（现以 JSONL journal 替代，不是 AC 指定载体）。
- 最小修复：item 入队/执行前持久化 `pre_head`+`expect_head`（JobStore 增列/字段并在状态落盘时写），收口写 `post_head`；补 Pull 场景 IT 断言三者与 git 输出一致。

### P1-4 新写操作 AC Then 未闭环（AC-OP-05/11-14/23/25、AC-REPO-08/09/10、§16.2-16.5）
- **AC-OP-23（§16.2 `:1464-1472`）**：pending JSON 字段齐全（`BatchOperationService.cs:1418-1434`）且重试成功单测有；但无 `.pending` 启动扫描重建、无显式取消命令/UI、无 30 天 GC（`CleanupTrash` 只枚举 14 位时间戳目录，`.pending` 永不清理）、审计 result=item.State（FAILED，非 DELETE_PENDING）、无 `details.srsCode=FS-E001`；UI 无“待处理（文件占用）”文案/取消入口。最小修复：新增 `listDeletePending/cancelDeletePending`+UI 按钮、启动扫描读 `.pending`、Retention 增 pending 30 天清理、审计 result 映射 + srsCode。
- **AC-OP-25（§16.5 `:1499-1506`）**：`baseline_ref` 未持久化到 `job_item`（`JobStore.cs:191` 无该列），undo 靠 `item.Message` 正则抽 ref（`HostCommandRouter.cs:444`）；undo 不是 `CREATE_UNDO` job、不写 journal（Router 直循环 `:428-456`）；`refs/baseline/create/*` 不在 RetentionService 清理范围；UI 无“撤销创建”入口（`ui/src/api.ts:557` 只有命令白名单，无调用点）。最小修复：job_item 增 `baseline_ref`、`CREATE_UNDO` 入 `KindWhitelist` 走 JobEngine+journal、Retention 清 baseline ref、补 UI 撤销入口与 partial/拒绝用例。
- **AC-OP-11/12/13/14（§16.4 `:1489-1497`）**：`baseline_snapshot` 在逐 item 内即时 `Upsert`（`BatchOperationService.cs:1290,1310`），不是“TAG job 收口一次写 4 行”；失败 job 会留下半套 rows（`ResolvedLocalRepo` 失败/checkout 失败的 item 连 `tag_present=0` 行都没有）。最小修复：把 baseline 写入移到 TAG job 收口（按 4 仓结果一次事务写 4 行，含 skipped/missing=0），补“混合失败仍 4 行”用例。
- **AC-OP-05 / AC-REPO-08（§16.3 `:1474-1487`）**：REPAIR/RECLONE 后端/单测在（`BatchOperationService.cs:452-577`；`JobEngineTests.cs:1764-1830`），但 UI 无入口（`§16.3` 要求组件抽屉/任务中心）；REPAIR 失败回滚只搬回原文件、**不清 `git init/fetch` 产生的 `.git` 与半工作区**（`:504-519`），RECLONE 失败若 clone 留下半目录则因 `Directory.Exists(path)` 不 move back（`:565-576`），不满足“失败零改动”；REPAIR 默认分支硬编码 `main`（`:496`）；无 job 级/失败回滚/E_REPAIR/RECLONE 测试。最小修复：回滚前删除新建 `.git`/半目录再 move back；用 `origin/HEAD` 取默认分支；补 UI 入口与失败注入用例。
- **AC-REPO-09/10**：NO_UPSTREAM 仅 `push -u` 发布分支（`RepoActionService.cs:858-932`），无 `--set-upstream-to`（UNSET_BUT_EXISTS）与清除/重建 upstream（UPSTREAM_GONE）修复动作；DETACHED 建分支只有 `git branch <name> HEAD`（`BatchOperationService.cs:1189-1233`），**不检出**新分支且 UI 文案称“已存在则切换”与实际 SKIP 不符（AC-REPO-10 ② 要求检出）。最小修复：补两类 upstream 修复动作 + DETACHED 建分支后 checkout，补三子型/两 detached 用例。

### P1-5 组件管理（AC-OP-31/32、AC-UI-16、§16.1 `:1451-1462`）
- 证据：bind/unbind/store/UI/菜单已接（`ComponentManagementService.cs:36-93`；`HostCommandRouter.cs:354-460`；`ModalHost.vue:412-431`；`73d09ae` 只补菜单 `tools.components`）。但：**无 `COMPONENT_BIND` 审计**（grep 全仓仅 LOCAL_DELETE 两处 UNBIND，`HostCommandRouter.cs:410,417`）；STOP_MANAGE 分支无审计（`:419-425`），AC-OP-32 ④/AC-UI-16 ⑤ 不成立；bind 只写 management 表，不写 component 行、不触发看板刷新、缺仓不标 `BINDING_ERROR`、无启动迁移写 `is_managed=1`（AC-OP-31 ②③⑤）；LOCAL_DELETE 部分失败仍 `CommandResult.Success`（`:396-418`）且 UI 恒 toast“已本地删除”（`ui/src/stores/workbench.ts:2881-2888`），无逐仓失败/重试提示；无“已移出”看板视图（`ApplyManagementFilter:1054-1068` 永久过滤，仅管理弹层可见）；`bindComponent` 零单测（tests 仅 unbind 两例，`JobEngineTests.cs:1489-1536`）。
- 最小修复：补 COMPONENT_BIND/STOP_MANAGE 审计（details 含 mode/逐仓结果）；bind 后写/刷新 component 行并标 BINDING_ERROR、启动迁移写默认 is_managed=1；LOCAL_DELETE 失败返回 FAILED+items 并 UI 显示失败仓/重试；加“已移出”视图/重新纳入入口与 bind/partial/UI 用例。

### P1-6 其余 PARTIAL AC（严格 DoD 要求全 COVERED）
- AC-OP-18（untracked 显式删除缺 UI 勾选）、AC-OP-19（仅 remote-gone 用例，缺本地独有/UPSTREAM_GONE 两子型）、AC-OP-24（dry-run 表缺“远程已删/超前落后/各仓状态/基线 ref”逐列断言与 E2E；`BatchDialog.vue:44-55` 仅基础名称校验）、AC-OP-26/27（缺 CRASH_AT_ITEM_2/GIT_SLOW 确定性用例，仍为模拟重启/通用取消）、AC-OP-30 ③④（备份恢复缺“目标已存在/远程已变动/ref 过期”冲突预览；`.trash` 缺记录选择）、AC-SAFE-05/06/07/10/11/12（安全报告的 PARTIAL：日志脱敏/诊断包/转储/危险扩展名/穿越/既有 token 未按 Then 全量断言）、AC-UI-10c（real cancel 失败，见 P1-1）。
- 最小修复：逐条补齐 AC 指定断言/UI 入口/证据，或在最终 Gate 前用新 ADR 明确不纳入并同步 §16.9（不得默示通过）。

---

## 4. 进入 final Gate 的前置（建议顺序）

1. **先闭环 P1-1+P1-2**（证据门禁与故障注入/G-WIRE）：DoD 3/4 是硬门槛，缺 L2/G-WIRE 时其余全绿也必 FAIL。
2. 再修 **P1-3（SAFE-08 job_item 三元组）** 与 **P1-4**（DELETE_PENDING 取消/GC、CREATE undo job+baseline_ref+保留、baseline 收口 4 行、Repair/Reclone 回滚/UI、REPO-09/10）。
3. 然后 **P1-5**（组件管理审计/看板/partial/已移出视图）与 **P1-6** 台账补证；同步更新 `docs/m2b-ac-op-gap.md` 与 manifest。
4. 最后按 §16.9 ⑤ 出需求/代码/UX 三方 final Gate 报告；M2 = M2a ∧ M2b，`m2b` tag 才可打。

---

## 5. 边界与副作用

- 只读审计：未改任何源码/配置/`reqs`/`ui`/真实仓库；未启动/关闭 Host（PID 80028 由并行流程占用）、未跑 CDP/真机检查；未打 tag。
- 全量测试用隔离 `artifacts/gate-final`（gitignored），日志 `artifacts/gate-final-test.log`；真实库仅复制到 `%TEMP%` 后只读查询。
- 新增文件仅本报告 `docs/review-m2b-final-gap.md`（未跟踪）；`docs/m2b-ac-op-gap.md`/`docs/m2b-fault-injection-report.md` 为并行流程产物，本报告仅引用其内容并指出与 HEAD 的偏差。
