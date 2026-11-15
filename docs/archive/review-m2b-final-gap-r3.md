# M2b Final 差距复核 r3（`spec-v0.4` + D-029 / HEAD `6c1cd5b`）

| 项目 | 内容 |
|---|---|
| 对象 | `6c1cd5be573294ae5b4ec805b06034426d96a7b2`（2026-11-15 07:0x，工作区干净）；基线 `spec-v0.4` + D-029（`reqs/adr/D-029-defer-tag-switch.md`，Accepted）；对照 r2 `docs/review-m2b-final-gap-r2.md` 的 6 组 P1 复核；`m2b` tag 仍不存在 |
| 方法 | 只读 `git diff b1e527a..HEAD`（27 文件 +837/−90）；全量 `dotnet test` 实跑；只读核对 `docs/evidence/m2b-manifest.json`、`artifacts/*/result.json`、故障注入报告、AC 台账、ui/scripts；未改代码、未起 Host、未打 tag |
| 本轮实测 | **420/420**（Core 71 + Host 349）、**0 warning**；`m2b1-fix-selfcheck` 68/68、`m2b2-selfcheck` 53/53、`m2b2-realhost` 19/19 均为 07:03 新结果；manifest commit=`f940375` |

## 0. 判决

**P0 = 0；P1 仍未闭环（6 组，明细 12 条）→ 尚不可进入 M2b final Gate**（DoD §16.9 #1「全 AC COVERED + 证据可定位」与 #3「故障注入报告 PASS」仍不成立，且 D-029 出口范围在台账/规格中不一致）。

已闭环（不再列）：CREATE_UNDO 已为 JobEngine job（任务中心/可取消/journal）+ baseline ref 30 天清理+测试；`expect_head/pre_head` 写前落库；REPO-09/10 后端+UI+单测；GIT_SLOW/DISK_WRITE_FAIL/CRASH_BEFORE·AFTER 真实进程注入；四个自检套件实际 PASS；D-029 已改 §3/§16.4/DoD 并禁用 UI 批量 Tag；`E_DELETE_PARTIAL` 错误码；420 单测全绿。

## 1. 仍未闭环的 P1（仅列剩余项，附文件/行号）

### P1-1 逐条证据 / AC 台账不可用（DoD #1、AC-OP-34）
- 证据：manifest `checks[]` 的 `ac` 填脚本名、`status=PASS`（规范要求 AC id 与 `COVERED/PARTIAL/GAP`），且 4 条 `evidence` 路径**全部不存在**（`docs/evidence/m2b-manifest.json:36-56`：`artifacts/cdp-*/*/result.json` 均 False；实际目录为 `artifacts/m2b1-fix-selfcheck|m2b2-selfcheck|m2b2-realhost`，M2a 脚本不写 result）；`commit=f940375` ≠ HEAD；AC 台账 `docs/m2b-ac-op-gap.md` 未更新：仍写“故障注入报告（未通过）/413 测试”（`:4`），无 `AC-OP-34/35`、`AC-UI-16` 行，`AC-OP-25` 标 COVERED 但缺口列仍写“UI 撤销入口/CDP”（`:32`），`AC-REPO-01…11=GAP`（`:40`）；新 UI（Repair/Reclone/撤销/待处理取消/upstream/detached）**无任何 CDP check**（`artifacts/m2b2-selfcheck/result.json` 53 项仍为旧能力）；crash drill 依赖**仓库内预构建 worker**（`tests/MbdGitTool.Host.Tests/M2bCrashDrillTests.cs:17,86` 读 `tests/…/bin/Debug/…dll`；隔离 `--artifacts-path` 不会生成该路径，本次因残留 dll 才通过）→ 证据不可复跑。
- 最小修复：manifest 按 AC id 写 `checks[{ac,status,evidence,artifact}]` 且 evidence 指真实存在文件；更新台账到 HEAD（含 AC-OP-34/35、UI-16、全部 PARTIAL→结论）；为新 UI 补 CDP/selfcheck；crash drill 改为构建时复制 worker 输出或 `ProjectReference` 定位。

### P1-2 故障注入报告仍未 PASS + AC-SAFE-05/06/10/11/12 PARTIAL（DoD #3）
- 证据：`docs/m2b-fault-injection-report.md:3` 仍写“本报告不是全绿结论”；AC-SAFE-04/05/06/07/08/09/10/11/12/13 仍 PARTIAL（`:30-34`）；剩余项含“生成安全证据三件套、按 AC-SAFE-01…13 逐条补证据、更新为 PASS”（`:38-43`）；`docs/evidence/security/{gwire.jsonl,argv.txt,remotes.txt}` 不存在（`docs/evidence` 仅 manifest）。
- 最小修复：用 G-WIRE 记录器覆盖产品 fetch/push/删除（不只 clone）并生成三件套+哨兵扫描；逐条 AC-SAFE 填期望/实测/证据；把报告结论更新为 PASS。

### P1-3 AC-OP-26/27/30 的 `COVERED` 不成立（AC Then 未满足/证据弱化）
- AC-OP-26（`:2537-2544`，依赖 `CRASH_AT_ITEM_2` 确定性终止）：无该注入点、无用例（grep 无 `CRASH_AT_ITEM_2`；crash drill 是 journal/reconcile 级，不执行 JobEngine）；台账 `:33` 仍标 COVERED。
- AC-OP-27（`:2545-2552`）：自检 `real.cancel-state` 被放宽为 `cancelHit || SUCCESS`（`ui/scripts/cdp-m2b1-fix-selfcheck.mjs:669,704`），无法证明取消语义。
- AC-OP-30（`:2569-2575`）③④“目标已存在/远程已变动/备份 ref 过期”冲突预览未实现：`BackupRefService` 恢复仅校验分支身份/脏工作区/untracked（`:212,238,260`），无冲突预览代码；台账 `:37` 标 COVERED。
- 最小修复：补 `CRASH_AT_ITEM_2`（真实进程 Kill 在第 2 项）作业级用例；恢复取消严格断言或补 GIT_SLOW 作业级取消用例；实现恢复前冲突预览与 `.trash` 记录选择后改台账。

### P1-4 LOCAL_DELETE / BINDING_ERROR 仍未闭环（AC-OP-31/32、AC-UI-16）
- LOCAL_DELETE 部分失败仍返回 `CommandResult.Success`（`HostCommandRouter.cs:417-419` 仅 state=PARTIAL），UI 恒提示“已本地删除”（`ui/src/stores/workbench.ts:2942`）；审计无 `details{mode/逐仓结果}`；AC-OP-32 `:2585-2592` ③④未满足。
- bind 仍只写 management 表（`ComponentManagementService.cs:34-57`），不写/刷新 component 行、缺仓不标 `BINDING_ERROR`、无启动默认 `is_managed=1` 迁移（§16.1 `:1451-1462`；AC-OP-31 `:2577-2584` ②⑤）。
- 最小修复：partial 返回失败+逐仓 items 并 UI 展示失败仓/重试；审计 details；bind 触发扫描/写 component 行、缺仓 `BINDING_ERROR`、启动迁移；补 bind/审计/partial CDP。

### P1-5 AC-OP-18/19/24 仍 PARTIAL（台账自述）
- 证据：`docs/m2b-ac-op-gap.md:25`（OP-18 缺 UI untracked 显式删除勾选，UI grep 无 `cleanUntracked`）、`:26`（OP-19 仅 remote-gone 子型）、`:31`（OP-24 dry-run 缺“远程已删/超前落后/各仓状态”逐列断言与 E2E）。
- 最小修复：逐条补 AC 指定 UI/断言/证据后再改状态。

### P1-6 D-029 暂定后出口范围/证据不一致（Gate 口径不可复现）
- 证据：台账 `AC-OP-13` 仍 `PARTIAL` 而非 `DEFERRED`（`:20`）；“仍需完成”仍列 Tag 11…14 与已完成的 AC-OP-01…05/16/18/19/23/24/25/29/31/32/REPO-01…11/故障报告（`:44-50`）；规格 §16.4 验收句仍要求 AC-OP-11/12/13/14（`specification.md:1497`）；FR-OP-04 需求/追溯行仍列 AC-OP-11…14 且无 D-029 注（`:524,:3101`）；DoD 基线“397/397”（`:1549`）与实跑 420、台账 413 三处不一致。
- 最小修复：台账 OP-13 改 DEFERRED 并重写“仍需完成”；§16.4/FR-OP-04/追溯矩阵标注 D-029；统一测试基线与 manifest commit 到 HEAD。

## 2. 是否可进入 final Gate

**否。** 代码侧已明显收敛（420 全绿、CREATE_UNDO/REPO-09/10/真实 crash drill 等已交付），但 DoD #1 的逐条证据/台账仍不可用、#3 故障注入报告仍非 PASS，且 AC-OP-26/27/30 存在错误 COVERED、组件管理与 OP-18/19/24 仍有 Then 缺口、D-029 出口范围未在台账/规格一致收口——现在送 Gate 必 FAIL。

建议顺序：①P1-1（manifest 映射/台账/新 UI CDP/crash drill 可复跑）→ ②P1-2（报告 PASS+三件套）→ ③P1-3/P1-4（Then 实现与证据）→ ④P1-5/P1-6（台账与 D-029 范围一致）→ 三方 final Gate。

## 3. 边界

只读复核：未改源码/配置/`reqs`/真实仓库，未起 Host/未跑 CDP、未打 tag；全量测试写 gitignored `artifacts/gate-r3`（日志 `artifacts/gate-r3-test.log`）；真实库未改；唯一写文件为本报告（未跟踪）。
