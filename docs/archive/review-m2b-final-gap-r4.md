# M2b Final 差距复核 r4（`spec-v0.4` + D-029 / HEAD `ec01cb9`）

| 项目 | 内容 |
|---|---|
| 对象 | `ec01cb95a24fd08491bc8054a81a247879cf9d41`（2026-11-15 07:2x，工作区干净）；对照 r3 `docs/review-m2b-final-gap-r3.md` 的 6 组 P1 复核；`m2b` tag 仍不存在 |
| 方法 | 只读 `git diff 6c1cd5b..HEAD`（19 文件 +452/−80）；全量 `dotnet test` 实跑；只读核对 manifest/result.json/AC 台账/故障报告/安全证据；未改代码、未起 Host、未打 tag |
| 本轮实测 | **423/423**（Core 71 + Host 352）、**0 warning**；manifest `commit=83477c1`、4 suite、34 条 AC 映射；`docs/evidence/security/` 三件套已提交 |

## 0. 判决

**P0 = 0；P1 仍未闭环（5 组）→ 尚不可进入 M2b final Gate。** DoD §16.9 #1（全 AC COVERED + 证据可定位且真实）与 #3（故障注入报告 PASS）仍不成立。

已闭环（不再列）：`AC-OP-18` cleanUntracked UI+测试、`AC-OP-19` local-only 拒绝+测试、`AC-OP-24` preview 字段+测试、`AC-OP-25` CREATE_UNDO job/retention/UI、`REPO-09/10` 修复+UI、`LOCAL_DELETE` partial 返回 `E_DELETE_PARTIAL`（`HostCommandRouter.cs:417-421`，测试 `JobEngineTests.cs:1836-1863`）、crash worker 按需构建（`M2bCrashDrillTests.cs:122-140`）、M2a result.json、G-WIRE clone+push 三件套、D-029 台账/规格一致性（OP-13 已 DEFERRED、FR-OP-04/§16.4/DoD 420 已注记）。

## 1. 仍未闭环的 P0/P1（文件/行号）

### P1-1 AC-OP-34 证据门禁不真实（manifest 无 evidence、result.json 未元数据化、AC→套件映射虚标）
- `run()` 未给 suite 写 `artifact`（`ui/scripts/run-selfchecks.mjs:41-46`），因此 manifest 的 suite/checks **无 evidence 字段**（`docs/evidence/m2b-manifest.json` suite 仅 name/result），AC-OP-34 ② 要求的 `target/url/reachable/commit/checks` 元数据补写循环 `if (!suite.artifact) continue`（`:78`）全部跳过——`artifacts/m2b1-fix-selfcheck`(17:08)、`m2b2-selfcheck`(19:05)、`m2b2-realhost`(20:40) 均未刷新；仅 m2a 自写 result 无元数据。
- `checks[].status` 恒为 `PASS`（非 `COVERED/PARTIAL/GAP`），且 `acMap`（`:34-40`）把台账 PARTIAL 的 AC 映射成 PASS：`AC-OP-26/27`→m2b1、`AC-OP-25/30/UI-10c`→m2b2（其 53 项检查无撤销/冲突预览）、`AC-OP-31/32/05/34`→realhost 19（19 项无组件管理/Repair）——这些套件实际未断言对应 AC；manifest `commit=83477c1` ≠ HEAD `ec01cb9`；台账测试基线 420 vs 实测 423（`docs/m2b-ac-op-gap.md:4`）。
- 最小修复：`run()` 写入 `artifact`/元数据、checks 按台账真实 status、acMap 只映射实际断言；重跑并提交 HEAD 版本 manifest。

### P1-2 故障注入报告仍不可标 PASS（DoD #3；AC-SAFE-05/06/10/11 仍 PARTIAL）
- `docs/m2b-fault-injection-report.md:3`“本报告不是全绿结论”；`:30` SAFE-04 仍称“缺 G-WIRE-GIT 记录”（三件套已归档）；`:31` SAFE-05/06 PARTIAL（真实转储/诊断包未演练）；`:32` SAFE-08/09 仍写“AFTER_WRITE/UNKNOWN_GAP”（crash drill 已完成，文字陈旧）；`:33` SAFE-10/11 PARTIAL；`:38-43` 结论仍要求“补证据/更新为 PASS 后才可作为 Gate 材料”。
- 最小修复：更新报告逐条期望/实测/证据（AFTER_WRITE 标 PASS、SAFE-04 引用三件套），补齐 SAFE-05/06/10/11 后把结论改为 PASS。

### P1-3 AC-OP-26/27/30 仍 PARTIAL（`docs/m2b-ac-op-gap.md:33,34,37` 自述）
- OP-26：无 `CRASH_AT_ITEM_2` 注入点/作业级用例（grep 全仓无；crash drill 仅 journal/reconcile 级，`M2bCrashDrillTests.cs`），不符合 `specification.md:2540-2543` 确定性终止。
- OP-27：selfcheck 真实取消在快速完成时跳过/允许 `SUCCESS`（`ui/scripts/cdp-m2b1-fix-selfcheck.mjs:669,704`），无 GIT_SLOW 作业级取消用例。
- OP-30：冲突预览 ③④（目标已存在/远程已变动/备份过期）未实现——`BackupRefService.cs:212,238,260` 仅分支身份/脏工作区/untracked，无目标存在/远程变动/过期校验。
- 最小修复：补真实进程 `CRASH_AT_ITEM_2` 作业级用例、严格取消断言、恢复冲突预览。

### P1-4 BINDING_ERROR/组件写入 + AC-OP-31/32/UI-16 仍 PARTIAL（`:38,39,42`）
- `ComponentManagementService.BindAsync`（`:34-57`）仍只写 management 表：不写 component 行、不标 `BINDING_ERROR`、无启动默认 `is_managed=1` 迁移（§16.1/AC-OP-31 ②⑤）；G-WIRE REST 测试仅直调 service，无 router 审计/CDP；OP-32/UI-16 仍缺 CDP/已移出证据（LOCAL_DELETE partial 语义已修）。
- 最小修复：bind 后建/刷 component 行、缺仓 `BINDING_ERROR`、启动迁移；补组件管理 CDP（纳入/停止/本地删除/已移出/pending）。

### P1-5 其余仍非 COVERED 的 AC（DoD #1 要求全 COVERED）
- 台账自述 PARTIAL：`AC-OP-03`（清除失败态确认，`:10`）、`AC-OP-09`（Checkout 5 前置态，`:16`）、`AC-REPO-01…08/11`（逐条证据，`:40`）、`AC-SAFE-05/06`（`:41`）、`AC-SAFE-10/11/12`（`:41`）、`AC-SAFE-13`（`:41`）、`AC-OP-35`（Release 忽略注入未测，`:42`）；`AC-OP-34`/`AC-UI-16` 同上。
- 最小修复：逐条补 AC 指定断言/证据（或 Gate 前以 ADR 明确不纳入，不得默示通过）。

## 2. 是否可进入 final Gate

**否。** 代码与 L1 证据已大幅收敛（423 全绿、G-WIRE 三件套、LOCAL_DELETE partial 修好、D-029 一致），但 **DoD #3 故障注入报告仍未 PASS**、**DoD #1 的 manifest/AC 台账逐条真实性不成立**（evidence 缺失/PASS 虚标），且 **OP-26/27/30、OP-31/32/UI-16 与 OP-03/09/REPO/SAFE-05/06/10-13/35 仍 PARTIAL**；现在送 Gate 必 FAIL。

建议顺序：①P1-1（manifest evidence/状态/映射与台账）与 P1-2（报告转 PASS）→ ②P1-3/P1-4（AC Then 实现与 CDP）→ ③P1-5（剩余逐条证据）→ 三方 final Gate。

## 3. 边界

只读复核：未改源码/配置/`reqs`/真实仓库，未起 Host/未跑 CDP、未打 tag；全量测试写 gitignored `artifacts/gate-r4`（`artifacts/gate-r4-test.log`）；唯一写文件为本报告（未跟踪）。
