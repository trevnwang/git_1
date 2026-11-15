# M2b Final 差距复核 r2（冻结 `spec-v0.4` / HEAD `b1e527a`）

| 项目 | 内容 |
|---|---|
| 对象 | `b1e527a782daeba8d9bc34353f4857041f7194f7`（2026-11-15 03:16，工作区干净）；对照 r1 `docs/review-m2b-final-gap.md`（对象 `73d09ae`）列出的 6 组 P1 逐项复核；`m2b` tag 仍不存在 |
| 方法 | 只读 `git diff 73d09ae..HEAD`（18 文件 +724/−33）；全量 `dotnet test` 实跑；只读核对 `docs/evidence/m2b-manifest.json`、`artifacts/*/result.json`、真实库/日志；未改代码、未起 Host、未打 tag |
| 本轮实测 | **413/413**（Core 71 + Host 342）、**0 warning/0 error**；`artifacts/m2b2-realhost/result.json` **19/19 PASS**（Gitea 可达，L2）；m2b1 68/68、m2b2 53/53（03:11 生成）均 PASS；manifest 存在（commit=`e16043c`） |

## 0. 判决

**P0 = 0；P1 仍未全部闭环（6 组，明细 9 条）→ 尚不可进入 M2b final Gate。**

r1 中已闭环（不再列）：L2 realhost 证据（19/19 PASS）、证据 manifest 文件与 runner 生成、`GIT_NET_404/TIMEOUT` 专项、`COMPONENT_BIND`/`STOP_MANAGE` 审计、组件管理“已移出”筛选视图、DELETE_PENDING list/cancel/30 天 GC、`expect_head/baseline_ref` 字段暴露、G-WIRE REST/GIT 测试资产（部分）、413 单测全绿。

## 1. 仍未闭环的 P1（仅列剩余项，附证据行号）

### P1-1 证据门禁残余（DoD §16.9 #1/#5：证据可定位 + 签字）
- 证据：manifest 的 `checks` 为空、`commit` 非当前 HEAD（`docs/evidence/m2b-manifest.json:2,34`；规范要求 `{commit,generatedAt,suite:{…artifact},checks:[{ac,status,evidence}]}`，`specification.md:1543`）；AC 台账 `docs/m2b-ac-op-gap.md` 仍停在 1:24（HEAD 前），仍写“407 测试/`AC-REPO-01…11`=GAP/`m2b` 未完成”（`:4,:40`），且全文无 `AC-OP-34/35`；M2a 11/11 只有 manifest 一行 PASS、无 `result.json` 归档；无 `review-m2b-final-{req,code,ux}` 三方签字。
- 最小修复：重跑 runner 写入 HEAD commit + `checks[]`（逐 AC status/evidence/artifact），更新 AC 台账到 HEAD（含 AC-OP-34/35），归档 M2a 结果；三方 Gate 报告另行签署。

### P1-2 故障注入报告仍未 PASS + G-WIRE 证据三件套缺失（DoD #3；AC-OP-35、AC-SAFE-04/07/09/13）
- 证据：报告结论仍为“**尚未通过**”（`docs/m2b-fault-injection-report.md:38-43`）；`GIT_SLOW` PARTIAL（`:20`）、`DISK_WRITE_FAIL` GAP（`:21`）、`CRASH_BEFORE/AFTER_WRITE` 真实注入 GAP（`:22`）、真机 `UNKNOWN_APPLIED` 未演练（`:15`）、AC-SAFE-04/12/13 仍 PARTIAL（`:30,:34`）；`docs/evidence/security/` 三件套（`gwire.jsonl/argv.txt/remotes.txt`）不存在（规范 `specification.md:2805`）；G-WIRE-GIT 测试只是独立 `git clone` 走本地代理并断言 `GET`（`tests/M2b2bGWireGitTests.cs:176-185`），不经产品 fetch/push/DELETE、不记录/断言 Authorization 与 argv/remote；REST 桩只校验 URL 无 Authorization（`M2b2bComponentBindTests.cs:113-114`）。
- 最小修复：补 GIT_SLOW 与真实 CRASH_BEFORE/AFTER_WRITE/DISK_WRITE_FAIL 注入用例；让 G-WIRE-GIT 覆盖产品 fetch/push/删除零远端写并生成三件套（含哨兵扫描）；更新报告至 PASS。

### P1-3 AC-SAFE-08：pre/expect/post 写前未落库（FR-SAFE-04 P0）
- 证据：`ExpectHead` 仅在内存设置（`JobEngine.cs:740`），执行前**明确不落盘**（`:596-600` 注释“不在此处落盘”），首次持久化在 item 收口后（`:682`）；`PreHead` 同样只在收口时由 `result` 回填（`:670-676`）→ AC-SAFE-08 `:2759-2765` 的“执行前 pre_head 已落库/expect_head 记录/完成写 post_head”不成立；新测试只断言完成后 API 暴露 `ExpectHead`（`JobEngineTests.cs:35-60`）。
- 最小修复：item 进入 RUNNING 前持久化 pre/expect（或独立 journal→job_item 落库），收口写 post；补“执行前查询已落库”的 IT。

### P1-4 新写操作 AC Then 仍未闭合（AC-OP-05/11-14/23/25、AC-REPO-08/09/10、§16.2-16.5）
- **DELETE_PENDING（AC-OP-23 `:2512-2519`）**：list/cancel/GC 已补（`DeletePendingService.cs:25,51,65`；`RetentionService.cs:51`；Router `:476-486`），但**无启动扫描重建“待处理”视图、UI 无消费**（`ui/src/api.ts:558-559` 仅命令白名单，无调用点；`DeletePendingService` 仅 Router/Retention 两处构造）、审计 `result` 仍是 FAILED（`JobEngine.cs:1195` 起按 `result.State` 写，未映射 `DELETE_PENDING`）、无 `details.srsCode=FS-E001`。
- **CREATE undo（AC-OP-25 `:2528-2535`/§16.5 `:1499-1506`）**：`baseline_ref` 已持久化/暴露（`JobStore.cs:200-205`、`JobEngine.cs:1989-2017`），但撤销仍是 Router 直循环 + 手工 journal（`HostCommandRouter.cs:436-475`，`:462`），**不是 `CREATE_UNDO` job**（`JobEngine.cs:50-53` KindWhitelist 无该项）、无 `job_item` 逐仓态；`refs/baseline/create/*` 无 30 天保留（`RetentionService.cs:50-51` 仅 trash/pending/backup sync/audit）；UI 无撤销入口（`ui/src/api.ts:557` 白名单 only）。
- **baseline_snapshot（AC-OP-11…14/§16.4 `:1489-1497`）**：仍逐 item 即时 `Upsert`（`BatchOperationService.cs:1292,1312`），非“TAG job 收口一次写 4 行”，失败 job 会留半套；无 partial-failure 用例。
- **Repair/Reclone（AC-OP-05/AC-REPO-08 `:2668-2674`）**：后端/单测在（`BatchOperationService.cs:453,529`），但 UI 无入口（`ui/src` grep 无 REPAIR/RECLONE 调用）；失败回滚未清 `git init/fetch` 产生的 `.git`/半工作区（`:519`），RECLONE 半目录存在时不 move back（`:570`）；默认分支仍硬编码 `main`（`:497`）。
- **AC-REPO-09/10**：无 `--set-upstream-to`/清除 upstream 修复动作（仅 `push -u`：`RepoActionService.cs:861,892`）；DETACHED 建分支不检出（`CreateBranchAsync` 仍只 `git branch HEAD`）。
- 最小修复：pending 启动扫描+UI+audit result 映射；`CREATE_UNDO` 入 KindWhitelist 走 JobEngine+job_item；baseline ref 保留；TAG 收口写 4 行；Repair/Reclone UI+干净回滚+`origin/HEAD` 默认分支；补 REPO-09 三子型与 REPO-10 checkout。

### P1-5 组件管理残余（AC-OP-31/32、AC-UI-16、§16.1 `:1451-1462`）
- 证据：`COMPONENT_BIND`/STOP_MANAGE 审计已补（`HostCommandRouter.cs:365,431`），但 **LOCAL_DELETE 部分失败仍返回 `CommandResult.Success`**（`:416-419`，仅 `state=PARTIAL`），UI 仍无条件提示成功（`ui/src/stores/workbench.ts:2881-2888` 未改）；审计无 `details{mode/逐仓结果}`（`Record` 无 details 参数，`:416-423`）；bind 仍只写 management 表，不写/刷新 component 行、缺仓不标 `BINDING_ERROR`、无启动默认迁移写 `is_managed=1`（`ComponentManagementService.cs:36-80`；Router `:362-374` 仅审计+返回）；bind 无审计断言测试（`M2b2bComponentBindTests.cs` 仅直调 service）。
- 最小修复：LOCAL_DELETE partial 返回失败+items 并在 UI 列失败仓/重试；审计 details；bind 后触发刷新/写 component 行、缺仓 `BINDING_ERROR`、启动迁移；补 bind/审计/partial/UI 用例。

### P1-6 其余 PARTIAL AC 未变（严格 DoD 需全 COVERED）
- 证据（台账自述未变）：AC-OP-18（UI 无 untracked 显式删除勾选）、AC-OP-19（仅 remote-gone 子型）、AC-OP-24（dry-run 无“远程已删/超前落后/各仓状态”逐列断言与 E2E）、AC-OP-26/27（缺 `CRASH_AT_ITEM_2`/`GIT_SLOW` 确定性用例；`docs/m2b-fault-injection-report.md:20`）、AC-OP-30 ③④（缺目标已存在/远程已变动/ref 过期冲突预览与 `.trash` 记录选择）、AC-SAFE-05/06/10/11/12（报告 `:30-34` 仍 PARTIAL）；见 `docs/m2b-ac-op-gap.md:25,26,31`。
- 最小修复：逐条补 AC 指定断言/UI 入口/证据，或在 Gate 前以新 ADR 明确不纳入并同步 §16.9（不得默示）。

## 2. 是否可进入 final Gate

**否。** 已闭环项显著缩窄（L2 证据、413 测试、故障注入主体、组件审计等），但 DoD #1（全 AC COVERED + 证据可定位）与 #3（故障注入报告 PASS）仍明确不成立，另有 AC-SAFE-08 与多组 AC Then 未实现；现在送 Gate 必 FAIL。

建议顺序：①P1-2（报告转 PASS + G-WIRE 三件套）与 P1-3（SAFE-08 落库）→ ②P1-4（pending 展示/audit、CREATE_UNDO job、baseline 收口、Repair/Reclone、REPO-09/10）→ ③P1-5/P1-6 → ④更新台账/manifest 后交三方 final Gate。

## 3. 边界

只读复核：未改源码/配置/`reqs`/真实仓库，未起 Host/未跑 CDP、未打 tag；测试写入 gitignored `artifacts/gate-r2`（日志 `artifacts/gate-r2-test.log`）；真实库仅在 `%TEMP%` 副本查询；唯一写文件为本报告（未跟踪）。
