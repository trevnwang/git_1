# M2b-2b 代码/安全 Gate 报告（提交 `4d4a8b5`）

| 项目 | 内容 |
|---|---|
| 评审对象 | 提交 `4d4a8b5375d10a880e1529fc19d4c254fa79fbc9`（`feat(m2b2b): backup branch identity, audit backfill/repair, write-ahead journal and crash reconcile`，2026-11-14 20:44:07 +0800，父提交 `aac4baf`）；diff 25 文件 +2624/−275（Host 服务 13、Core 契约 5、测试 3、UI 4、docs 2，无 `reqs/` 变更） |
| 评审范围 | ①备份分支身份（`<ts>/<branch>/<TYPE>`、旧格式兼容、restore 跨分支零写、列表/UI、测试）；②审计回填/修复（fallback、启动回填、`repairOperationLog`、真实库 858→341、无泄漏、并发/降级、测试）；③journal/reconcile（写前 STARTED、DONE/FAILED、启动 NOT_APPLIED/UNKNOWN_APPLIED、只读、审计 1 行、异常/取消收口、测试）；④EMPTY 首推/离线/retention 不回退、全量测试与 M2a/M2b-1/M2b-2a 不退化；⑤P0/P1/P2 与 tag 结论。**只评审，不改代码/真实仓库** |
| 输入/判据 | SRS `reqs/specification.md`：FR-OP-09/11、**FR-SAFE-04（写操作全部走写前 journal + 崩溃 reconcile，pre/expect/post 推断、禁止猜测成功）**、NFR-REL-03；`reqs/specification.md（Annex A）`：AC-OP-30、AC-SAFE-08/09；`reqs/adr/D-026-m2b2a-deferrals.md`（M2b-2b 必须完成项 + 自动化验收 + 安全 ref 可见/可恢复/保留）；`reqs/adr/D-025`；`docs/m2b2b-status.md`、`docs/m2b-status.md`、上一轮 `docs/review-m2b2-gate-code.md` |
| 环境 | Windows 11 / .NET SDK 10.0.404 / git 2.56.1 / 工作区 `D:\Workspace\git_1`。评审开始时 `HEAD=4d4a8b5`、`git status` 干净，评审全程 HEAD 未变。评审末段（20:51–20:52）发现**并行工作流**在测试目录起草未提交文件（`tests/MbdGitTool.Host.Tests/M2b2bAuditJournalTests.cs` 13 个 Fact、`TestInfrastructure.cs` +7/−2），不属受评提交、未纳入本报告 369 基线，见 §8。真实 Host PID 48284（Debug，20:44:11 启动）由并行 UX 检查占用并锁定 `bin`，本报告**未启动/未关闭**该进程；全量测试改用 `--artifacts-path` 隔离输出绕开锁 |
| 方法 | ①`git show 4d4a8b5` 逐行静态核对（行号均对 HEAD）；②全量 `dotnet test` 实跑（隔离 artifacts）核 369/369、0 warning；③只读核对真实 `%LOCALAPPDATA%\MbdGitTool` 的 `db/mbd.db`（复制到 `%TEMP%` 用 sqlite 只读查询）、`audit/operations.jsonl`、`journal/operations.jsonl`、`exports/*`、`logs/host.log`；④真实工作区 `refs/backup/sync/*` 与 journal 配对；⑤`tests/**` 全量 grep 核对新增能力的自动化覆盖；⑥与 ADR/SRS/AC 逐条对照 |
| 边界 | 未修改任何源码/配置/`reqs`/`ui`/真实仓库/真实用户数据；本报告新增的唯一文件即本文档（并行工作流的未提交测试文件非本报告所为，见 §8）；构建产物与测试日志仅落 gitignored `artifacts/`；真实库仅在 `%TEMP%` 副本上查询；未执行 UI CDP 自检与“杀进程”真机 reconcile 演练（见 §6/§8） |

---

## 0. 结论（判决）

> **不建议在 `4d4a8b5` 打 `m2b-2b` tag。**（P0=0，P1=3）
>
> 三项 P1：①M2b-2b 新增的 journal/reconcile 与审计 fallback/回填/repair **零自动化用例**（ADR D-026 §4 明确要求“P1-2/P1-4 自动化用例全绿”，`docs/m2b2b-status.md:33` 的“单测覆盖 STARTED 重启两分支”与事实不符）；②ADR D-026 §3 明列的“安全 ref `refs/backup/restore/*` 在 UI/API 可见可恢复、按保留期清理”**未交付且未在状态报告挂账**（上一轮 Gate P1-5(b) 仍 OPEN）；③“修复历史测试污染”的**证据口径仍过度**：repair 只清理 DB-only 行，JSONL 来源的 64 行夹具/自检数据仍留在 341 基线内，`m2b2b-status.md:26/35` 称其为“真实 JSONL 340 行 / 已清理当前真实库”不成立（上一轮 P1-1(b) 未真正闭环）。
>
> 正向：备份分支身份 + restore 跨分支零写（代码/单测/真实 ref 三方一致）；审计 outbox/回填/repair 在真实环境可复现（`removed=858 inserted=340 total=341` 与日志逐字一致，启动回填 326 行）；写前 journal 在真实 SYNC 产生 4 组 STARTED→DONE；全量 **369/369**、0 warning/0 error；审计 JSONL/exports 无 token、无绝对路径泄漏。
>
> **M2b 完成仍需 AC-OP 全量逐条证据 + 真机“杀进程重启”reconcile 演练（当前真实库 `action=RECONCILE` 为 0 行，该路径未被真实触发过）；`m2b-2b` tag 只能代表 M2b-2b 阶段，不得表述 M2b/M2 完成。**

---

## 1. 基线与可复跑证据

1. **对象确定**：`git rev-parse HEAD` = `4d4a8b5…`，父 `aac4baf…`；评审开始时 `git status --porcelain` 为空。提交说明已自述“M2b tag pending AC-OP full evidence/reconcile drill; M2 not complete”，与当前 tag 现状一致：`git tag -l` 仅 `m0-baseline/m1-baseline/m2a/m2b-1/m2b-2a/ui-v3`，**无 `m2b-2b`**。
2. **全量测试（本报告实跑）**：运行中 Host（PID 48284）锁定 `src/MbdGitTool.Host/bin`，首次 `dotnet test` 因 MSB3027/MSB3021 文件锁失败（环境问题，非用例失败）；改用隔离输出后：
   ```powershell
   dotnet test MbdGitTool.sln -c Debug --nologo --artifacts-path artifacts/gate-test
   # 已通过! - 失败: 0，通过: 71，已跳过: 0，总计: 71  - Core.Tests
   # 已通过! - 失败: 0，通过: 298，已跳过: 0，总计: 298 - Host.Tests（2m38s）
   # 合计 369/369，0 warning / 0 error（log 中 warning/error 行数=0；目录级 TreatWarningsAsErrors=true）
   ```
   日志：`artifacts/gate-m2b2b-dotnet-test.log`（gitignored）。与 `docs/m2b2b-status.md:21` 的“369/369（Core 71 + Host 298）、0 warning”一致。
3. **真实数据只读基线（评审时刻）**：`db/mbd.db` `operation_log` **369** 行（`min ts=2026-11-14T06:36:14Z`、`max ts=12:39:03Z`）；`audit/operations.jsonl` **369** 行；两方稳定键（requestId+jobItemId+ts+action）**369 个、0 重复、0 损坏**；`fallback.jsonl` 不存在（无 pending）。即“repair 后 341 行 + repair 之后真实操作新增 28 行”。
4. **repair/回填真实证据**（`host.log` 原文）：
   - `12:37:23Z [INFO] 审计回填完成：…插入 326 行（跳过 10，回退剩余 0）`；
   - `12:37:50Z [WARN] repairOperationLog：removed=858 inserted=340 skipped=0 total=341 fallbackRemaining=0`；同秒 `api repairOperationLog ok=True`。
   与 `docs/m2b2b-status.md:12/26` 的“858→341（inserted=340）”**逐字一致**。
5. **真实 journal / 备份 ref**：`journal/operations.jsonl` 8 行 = 4 组 `SYNC` `STARTED`→`DONE`（`RTCT_RemoteOnly` 的 DESIGN/IMPL/MIL/SIL），含 `jobId/jobItemId/preHead/postHead/localPath`，与状态报告一致；真实工作区 4 仓存在 `refs/backup/sync/2026111412373x/main/<DESIGN|IMPL|MIL|SIL>` 新格式 + 若干旧格式 ref。真实库 `action=RECONCILE` **0 行**（启动 reconcile 未被真实触发，见 §4）。
6. **安全扫描**：`audit/operations.jsonl`、`exports/*`（14 个文件）对 `glpat/token/Authorization/extraHeader/password/Bearer` 命中 **0**，对 `D:\Workspace`/`LocalAppData` 等绝对路径命中 **0**；journal 文件仅含 8 条 `localPath`（见 §3-P2）。

---

## 2. 判据 1：备份分支身份 — **PASS**

| 核对项 | 证据（HEAD） | 判定 |
|---|---|---|
| 新格式 `<ts>/<branch>/<TYPE>` | `BatchOperationService.cs:210-227`：`BackupRefPrefix + stamp + "/" + EncodeBranchSegment(branch) + "/" + TYPE`；`EncodeBranchSegment` 用 `Uri.EscapeDataString`（`BackupRefService.cs:442-450`），`feature/x`→`feature%2Fx` | **PASS** |
| 旧格式兼容 | `TryParseBackupRef`（`BackupRefService.cs:352-426`）：2 段=LEGACY/Branch=null，3 段=BRANCH；严格校验 14 位 UTC stamp、TYPE、空段/多余段/相对段/控制字符/非规范转义均拒绝（`:371-425`） | **PASS** |
| 列表返回 branch/format | DTO 追加 `Branch`/`Format`（`BackupDto.cs:8-33`）；`GetBackupRefsAsync` 输出（`BackupRefService.cs:98-108`）；解析填充（`:562-574`） | **PASS** |
| restore 跨分支拒绝且**零写** | 顺序：ref 必须存在（`:174-184`）→ 读当前分支（`:186-204`）→ 不一致 `E_ACTION_NOT_ALLOWED`（details `expected/current/branch/format`）**在此之前无任何写**；之后才做 dirty/untracked 阻断（`:206-250`）、安全 ref（`:252-271`）、`reset --hard`（`:273-282`） | **PASS** |
| 旧格式 restore | 允许但响应 message 明确提示“旧备份无分支身份，请确认当前分支（<branch/detached>）”（`:294-308`） | **PASS** |
| 测试覆盖 | `M2b2BackupRefTests.cs`：新格式列表/`feature%2Fx`（`:56-86`）、同分支成功（`:124-143`）、**跨分支拒绝+零写断言**（`:145-170`：HEAD/分支/工作区/`refs/backup/*` 全不变）、slash 往返（`:172-189`）、LEGACY 提示（`:191-208`）、非法 ref/stamp/type/穿越（`:273-349`）、两种格式保留清理（`:378-402`）；`JobEngineTests.cs:585-599` 断言 SYNC 真实 ref 解析出 `main/IMPL/BRANCH`；`M2b2RouterApiTests.cs:181-229` 路由器级跨分支拒绝零写。上述用例均在本轮 369 全量中通过 | **PASS** |
| UI 显示 | `ui/src/types/workbench.ts:509-514` + `ui/src/viewmodel.ts:1167-1172` + `ui/src/api.ts:447-452`（`branch/format` 映射，`format` 缺省按 branch 推导）；`ui/src/components/overlays/ComponentDrawer.vue:256-257` 显示 `@branch`（title 含 format）或“旧格式”；realhost 脚本含 `real.backup-branch-or-legacy` 检查 | **PASS（无 UI 单测；仅真机脚本条件断言，见 §6-P2-8）** |
| 保留清理兼容新格式 | `CleanupExpiredAsync` 按 ref 的 stamp 判定、两种格式都删（`BackupRefService.cs:594-646`），单测 `M2b2BackupRefTests.cs:378-402` | **PASS** |

> 残留：分支身份取自快照 `repo.Repo.Branch`（`BatchOperationService.cs:140`），而写前 CAS 只比对 HEAD SHA；用户若在快照后切到“指向同一 SHA 的另一分支”，SYNC 会把旧分支名写进备份 ref 并 reset 当前分支指针（既有风险被新身份字段放大）→ §6-P2-6。

---

## 3. 判据 2：审计回填/修复 — **代码 PASS / 真实证据 PASS / 测试 FAIL（P1）**

| 核对项 | 证据（HEAD） | 判定 |
|---|---|---|
| SQLite 失败 → fallback.jsonl | `Record` 双写后检查 `Insert` 返回值，失败且 `allowFallback` 时写 `fallback.jsonl`（`OperationAuditService.cs:298-309,340-369`）；`AuditDatabase.Insert` 对 READONLY/CORRUPT/CANTOPEN/NOTADB 置 `_available=false` 运行期降级，busy/locked 不降级（`AuditDatabase.cs:276-303`） | **PASS** |
| 启动回填（JSONL+fallback→DB） | 构造器即调用（`OperationAuditService.cs:150-161` → `:415-509`）：稳定键 `requestId+jobItemId+ts+action`（`AuditDatabase.cs:484-490`）去重；主 JSONL 先、fallback 后；成功清空 fallback、失败保留并 `Succeeded=false`；`HostLog` 记录 inserted/skipped/remaining | **PASS** |
| repairOperationLog 重建 | 必须 `confirm=true`（`:568-580`），`DROP+CREATE` 事务（`AuditDatabase.cs:494-560`），再从 JSONL/fallback 回填，最后写 1 行 `REPAIR_OPERATION_LOG`（`allowFallback:false`，`:655-669`），返回 `{removed,inserted,total}`；路由器 120s 超时（`HostCommandRouter.cs:56,495-504`） | **PASS** |
| 查询/导出不隐藏 DB 缺行 | DB 可用但 fallback 有 pending 时，`getOperationLog` 合并回退队列（`:904-946`，负 id 表示未入 DB）、导出同样合并（`:1004-1038`）；DB 不可用返回 `E_INTERNAL` 且 details 带 `database/pendingFallback`（`:1540-1547`） | **PASS** |
| 真实库 858→341 | `host.log`：`removed=858 inserted=340 skipped=0 total=341 fallbackRemaining=0`；当前库=341+28=369，与 JSONL 369 一致、0 重复（§1.3/1.4） | **PASS** |
| 无 token/路径泄漏 | 审计只写元数据（action/component/head/result/errorCode/requestId/jobId/jobItemId/backupRef，`OperationAuditService.cs:246-296`）；真实 JSONL/exports 扫描 0 token、0 绝对路径（§1.6） | **PASS**（journal 的 `localPath` 例外，本地文件，§6-P2-3） |
| 并发/失败降级 | 进程内按文件路径静态锁串行 JSONL/fallback 追加（`:62-67,1095-1125`）；DB 每操作短连接、WAL/busy_timeout；硬失败降级、查询合并、错误显式化。**但**repair 与并发 Record 无互斥、DROP 前无备份、fallback 不参与 retention（§6-P2-4/P2-5） | **PASS（含 P2 残留）** |
| **测试覆盖** | `tests/**` 全量 grep `(?i)journal\|repair\|backfill\|fallback\|reconcile` 仅 1 条无关命中（`M2b2AuditDatabaseTests.cs:40` 的 `PRAGMA journal_mode`）；`QueryStableKeys/RecreateOperationLog/BackfillFromJsonlAndFallback/RepairOperationLog/fallback.jsonl/pendingFallback` **零测试**；本轮 +32 用例全部来自备份 ref（M2b2BackupRefTests 新增）与 router 分支断言 | **FAIL → P1-1** |
| 污染清理的真实性 | repair 删除了 518 行 DB-only 污染（858−340），方向正确；但回填的 340 行 JSONL 本身含 64 行历史夹具/自检数据（`CF100A_Demo` 38 行 + `MOCK_CF1001_ChanganSigProcgBascFct` 26 行，ts 06:36–07:13Z、actor/requestId 均 NULL），repair 无法清理“JSONL 有对应行”的污染；`m2b2b-status.md:26` 称“回填真实 JSONL 340 行”、`:35` 称“repair 已清理当前真实库”不准确 | **FAIL → P1-3** |

---

## 4. 判据 3：journal / reconcile — **代码 PASS / 测试与真机演练 FAIL（P1）**

| 核对项 | 证据（HEAD） | 判定 |
|---|---|---|
| 写前 STARTED | 单仓五键：`RepoActionService.cs:452-462` 在 switch 执行 git 前 `_journal.Begin(action, component, repoType, preHead, localPath, jobId, jobItemId)`；批量 item：`JobEngine.cs:760-770` 在 `_batch.*` 前 `Begin(kind,…,job.Id,item.Id)`（dryRun 不写，`:744-752`）；重复 journal 已避免（`RepoActionKinds` 分支走 RepoActionService，`:754-758`） | **PASS** |
| DONE/FAILED 收口 | 成功 `Complete(postHead)`（`RepoActionService.cs:507-510`；`JobEngine.cs:793-796`）；失败 `Fail(errorCode)`（`:500`；`:797-800`）；**取消**（`OperationCanceledException`）与**未捕获异常**均 `Fail` 后原样抛出（`RepoActionService.cs:531-542`；`JobEngine.cs:809-818`） | **PASS** |
| 旁路语义（不阻塞动作） | journal 写入失败仅 WARN 返回 null/false，动作继续（`OperationJournal.cs:231-272`）；字段 `Sanitize`≤512 + 可选键 null 不输出（`:298-314,367-389,395-445`） | **PASS** |
| 启动 reconcile 判定 | `HostCommandRouter.cs:146-170` 后台任务 `StartupReconcile`；`JournalReconciler.Reconcile`（`:101-160`）只扫“STARTED 无 DONE/FAILED/RECONCILED”（`OperationJournal.cs:168-205`），逐条对比当前 HEAD 与 preHead：相等→`NOT_APPLIED`，不等/读不到→`UNKNOWN_APPLIED`（`JournalReconciler.cs:162-203`），**从不产出“成功”** | **PASS** |
| 只读 | reconcile 只执行 `git rev-parse HEAD`（`JournalReconciler.cs:249-255`），失败/目录不存在返回 null；写操作仅是 journal RECONCILED、审计行、任务中心 message（均在应用数据目录），**不修改仓库/ref** | **PASS** |
| 审计恰 1 行 | 每条未收口条目调用一次 `_audit.Record(action=RECONCILE, requestId=entry.Id, jobId/jobItemId, result=NOT_APPLIED/UNKNOWN_APPLIED)`（`:175-189`），随后 journal 追加 RECONCILED（`:191`），下次启动不重复判定 | **PASS** |
| job 关联回写 | `JobEngine.AnnotateReconcile`（`:399-436`）仅改对应 item.message（“可安全重试/请人工核对”）、持久化并 JOB_UPDATED，不改终态/不重跑 | **PASS** |
| 真实环境证据 | 真实 journal 4 组 STARTED→DONE 成对（§1.5）；但真实库/任务中心**从未出现 `RECONCILE` 审计行（0 行）**，说明启动 reconcile 分支在真实运行中未被触发；状态报告也自述“尚未做杀进程在写中真实演练”（`m2b2b-status.md:33`） | **PARTIAL（无真机证据）** |
| **测试覆盖** | 全仓 `tests/**` 无 `OperationJournal`/`JournalReconciler`/`NOT_APPLIED`/`UNKNOWN_APPLIED`/`AnnotateReconcile`/`StartupReconcile` 任何引用；状态报告 `:33`“单测覆盖 STARTED 重启两分支”**不实** | **FAIL → P1-1** |
| 语义边界 | 只比对本地 HEAD：PUSH/EMPTY 首推/FETCH 崩溃后 HEAD 未变 → `NOT_APPLIED`，但远端可能已应用；DELETE（preHead 非空、目录已移走）/CLONE（preHead=null、半克隆）会落入 UNKNOWN 或 NOT_APPLIED 的误导标签（§6-P2-1） | **P2** |

---

## 5. 判据 4：EMPTY 首推 / 离线 / retention / 全量测试 / 不退化

| 核对项 | 证据 | 判定 |
|---|---|---|
| EMPTY 首推建 upstream | `RepoActionService.cs:839-874`：`EMPTY/NO_UPSTREAM/setUpstream` → `push -u origin HEAD`；unborn HEAD 在启动网络 git 前返回 `E_REF_NOT_FOUND + requiresCommit`（`:844-867`）；UI unborn 禁用（`ui/src/stores/workbench.ts:530-539`）；测试 `M2b2EmptyPushTests.cs` 4 例（unborn 零远端写/首推 upstream/NO_UPSTREAM）在本轮全量通过 | **PASS（未退化）** |
| 离线不误报 | 离线判定仍为“source=local 且远程错误/离线标记”（`workbench.ts:888-919`），健康 `local+gitea` 不误报；本提交未改该逻辑；realhost 脚本含 `real.no-false-offline`/`real.fetch-available-online`（但为条件短路检查，§6-P2-8） | **PASS（代码级，未复跑 UI 自检）** |
| retention 不回退 | `CleanupExpiredAsync` 只删本地、保留下限 `Math.Clamp(7,90)`（`BackupRefService.cs:318-343`）；审计保留仍由 `CleanupExpired` 用配置值（`OperationAuditService.cs:1077-1091`）；上一轮已闭环的 `.trash 30 天/backup refs 30 天/审计 ≥90 天` 测试在 369 全量通过 | **PASS**（残留：回填×保留竞态 §6-P2-4） |
| 全量测试 / 构建 | **369/369**（Core 71 + Host 298，0 失败/0 跳过），**0 warning/0 error**（§1.2，日志可复跑） | **PASS** |
| M2a/M2b-1/M2b-2a 不退化 | 本提交对既有路径为附加式改动（RepoActionService 只加 journal+try/catch、JobEngine 只加 journal/anotate 与可选 ctor 参数、`GitCli` 仅 `sealed→class`/`virtual`、契约只追加可选字段）；全量套件含 M2a/M2b-1/M2b-2a 全部既有用例且全绿；M2b-2a 旧格式 ref/恢复/清理均有兼容断言（§2） | **PASS（套件级）** |

---

## 6. P0 / P1 / P2

### 6.1 P0（阻断数据丢失/安全）= **0**

未发现新增的数据丢失或凭据泄漏路径：restore 跨分支/脏工作区均在写前零写拒绝；审计查询/导出/回填无 token；journal 只写元数据到应用数据目录。

### 6.2 P1（tag 前置，3 项）

| 编号 | 问题 | 证据 | 影响 / 建议 |
|---|---|---|---|
| **P1-1** | M2b-2b 新增的 journal/reconcile 与审计 outbox/回填/repair **零自动化用例**；状态报告“单测覆盖 STARTED 重启两分支”不实 | `tests/**` grep 无 `JournalReconciler/OperationJournal/NOT_APPLIED/UNKNOWN_APPLIED/BackfillFromJsonlAndFallback/RepairOperationLog/QueryStableKeys/RecreateOperationLog/fallback` 引用；本轮 +32 用例全部是备份 ref/router 断言（`git diff aac4baf..4d4a8b5 --numstat tests/`：+15/+221/+52）；`docs/m2b2b-status.md:33`；ADR D-026 §4 `:36`“P1-2/P1-4 自动化用例全绿” | FR-SAFE-04（P0 需求）与 P1-4 的“可验证性”未成立。建议补：journal 写前/收口/写入失败旁路、reconcile 两分支 + 审计 1 行 + 只读、fallback 触发/合并查询、启动回填去重、repair confirm/重建/计数 的单测或 IT。**注：评审末段并行工作流已在工作区起草覆盖上述项的 `M2b2bAuditJournalTests.cs`（未提交，见 §8）；若提交并全量复跑通过 + 更正 status 失实表述，本项可降级/关闭——结论仅对 `4d4a8b5` 成立** |
| **P1-2** | ADR D-026 §3“安全 ref `refs/backup/restore/*` 在 UI/API 可见可恢复、按保留期清理”**未交付、未挂账** | `BackupRefService` 列表只扫 `SyncRefPrefix`（`:32,532-578`）、restore 白名单拒绝 restore 前缀（`:145-152`）、`CleanupExpiredAsync` 只扫 sync（`:594-646`）；UI 仅恢复成功 toast 显示 safetyRef（`ui/src/stores/workbench.ts:2923`）；`docs/m2b2b-status.md` 能力表与残留表均未列该项；上一轮 `docs/review-m2b2-gate-code.md:124-128` 判 P1-5(b) OPEN | ADR 明列 M2b-2b 必须完成项。建议：`getBackupRefs`（或新增 `getRestorePoints`）列出 restore ref + 允许恢复 + 纳入保留清理，并补回归测试；如产品决定再次延后，需新 ADR 书面挂账，不能默示 |
| **P1-3** | “清理历史测试污染/真实 JSONL 340 行”证据口径仍过度；repair 无法清理 JSONL 来源污染 | 真实库回填的 340 行中 `CF100A_Demo`=38、`MOCK_CF1001_ChanganSigProcBascFct`=26（ts 06:36–07:13Z、actor/requestId 为 NULL），合计 64 行（≈19%）为隔离前测试/自检数据；`docs/m2b2b-status.md:26`“回填真实 JSONL 340 行”、`:35`“repair 已清理当前真实库”；上一轮 P1-1(b) 未闭环 | 证据可信度问题（与上一轮同类 P1）。建议：修正 status 措辞为“340 行可回填 JSONL（含 64 行历史夹具/自检行）”；如要真正清理需离线删改 JSONL 或新增按 action/actor 白名单清理入口（须审计/可回滚） |

### 6.3 P2（Top8 + 其余，不改变“不打 tag”判定）

| 编号 | 问题 | 证据 | 建议 |
|---|---|---|---|
| **P2-1** | reconcile 只比对本地 HEAD，对“不改 HEAD 的写操作”判定语义不成立 | `JournalReconciler.cs:162-203`：PUSH/FETCH/EMPTY 首推崩溃后 HEAD==preHead→`NOT_APPLIED`（远端可能已应用，重试多数无害但提示误导）；DELETE 成功但目录已移走（preHead 非空、HEAD=null）→UNKNOWN；CLONE 半克隆（preHead=null、无 HEAD）→NOT_APPLIED（重试会因目录存在失败） | 对 action 分类：非 HEAD 变更动作输出“未知/需人工核对”或专门文案；journal 记录 expect/post 期望（FR-SAFE-04 的 pre/expect/post 推断） |
| **P2-2** | 写操作 journal 覆盖缺口（FR-SAFE-04“写操作全部”） | `restoreBackupRef` 的 `reset --hard` 直接由 `HostCommandRouter.cs:517-595` 调 `BackupRefService.RestoreAsync`，无 journal；`cleanupRetention`（删 ref/文件/审计行）也无 journal；grep `_journal` 仅 RepoActionService/JobEngine/HostCommandRouter/JournalReconciler | 为 restore/cleanup 加 STARTED/DONE/FAILED（restore 已有安全 ref，可复用 reconcile 判定） |
| **P2-3** | journal 生命周期：无保留/压缩/上限；启动全量读；含绝对 `localPath`；无 fsync | `OperationJournal.cs:105-127`（`localPath`）、`:168-205`（启动全量解析）、无 retention 集成（`CleanupExpired` 不处理 journal）；真实 journal 8 行含 `D:\Workspace\git_1\artifacts\…` 绝对路径（本地文件，未进入 API/审计/导出） | 加 journal 保留/大小上限与轮转；路径保持仅本地（当前 OK）；可选 fsync（杀进程场景非必需） |
| **P2-4** | 启动回填 × retention 竞态：超期审计行可能被“回填复活” | `CleanupExpired`（`:1077-1091`）先删 DB 行（`DeleteOlderThan`）再 prune JSONL；若 JSONL prune 失败（锁/IO）返回 0，下次启动构造器 backfill 会把仍留在 JSONL 的超期行重新插入 DB；`fallback.jsonl` 不参与 retention，同样可被回填复活；每次启动重复全量扫描 | 回填以“min(JSONL, DB 保留期)”为界，或 prune 失败即跳过回填并告警；fallback 纳入 retention |
| **P2-5** | `repairOperationLog` 破坏性/并发：DROP 前无备份/导出；与并发 `Record` 无互斥；insert 失败不回退、`Count()` 失败时仍返回 success | `OperationAuditService.cs:568-681`：无 `VACUUM INTO`/导出；无全局写锁；JSONL 写失败过（仅 DB 有）的行会被永久删除；`:615-619` 主 JSONL 回填 insert 失败不计数、不保留（仅能靠下次启动再回填），`:640-648` fallback 失败行才保留；`:671-675` `total<0 → total=inserted` 后仍返回成功 | repair 前导出/备份、在审计写锁下执行、失败即返回错误并回滚/保留 fallback |
| **P2-6** | 备份分支身份来源为快照 `repo.Repo.Branch`，CAS 只比对 HEAD SHA | `BatchOperationService.cs:140`（branch 来自快照）+ `:151-158`（CAS 仅 SHA）；同 SHA 切分支后 SYNC 会把旧分支名写入 ref 并 reset 当前分支指针 | 写 ref 前 fresh `rev-parse --abbrev-ref HEAD` 并与快照 branch 不一致即零写拒绝 |
| **P2-7** | 新 action 未纳入 UI 枚举/中文标签；`@DETACHED` 占位显示为真实分支；`format` 英文枚举直出 | UI 审计 action 枚举无 `RECONCILE/REPAIR_OPERATION_LOG`（既有漂移扩大）；`ComponentDrawer.vue:256` title 直接显示 `BRANCH/LEGACY`；`EncodeBranchSegment("DETACHED")` 与占位同形（`M2b2BackupRefTests.cs:341`，生产 SYNC 已拒绝 detached，故实际不可达） | 补 action 中文标签与 `DETACHED` 展示文案；占位加区分前缀（如 `%2DDETACHED`） |
| **P2-8** | realhost 19/19 脚本已被改写为“只读/条件短路”检查，且结果未入库，不能作为 M2b-2b 写路径/journal/reconcile 证据 | `ui/scripts/cdp-m2b2-realhost-check.mjs` diff：删除真实 restore 写链路与 `RESTORE_BACKUP` 审计断言、`healthyGitea ? … : true`、`real.empty-skip-no-fixture` 恒真；`artifacts/` 全部 gitignored，未见 19/19 结果文件；真实库 `RECONCILE`=0 行、真实 journal 只有正常配对 | 证据入库（非 gitignore 的 Gate 报告附件）并保留写链路断言；真机 reconcile 演练单独出证据（见 §7） |

> 其余（未入 Top8）：`JournalReconciler` 先写 RECONCILE 审计再标记 RECONCILED，若审计写失败则缺口不重试；reconcile 不重扫仓状态（AC-SAFE-09 ④“main_status 与真实一致”未由该路径提供）；`M2a/M2b-1/M2b-2 自检 53/53/11/11/68/68` 为本轮未复跑的 UI/真机证据（UX Gate 范围），本轮仅以 369 单测 + 真实数据核验。

---

## 7. 结论与 tag 建议

**建议：不打 `m2b-2b` tag（`git tag -l` 当前无该 tag，保持现状）。**

- **P0 已清零**；备份分支身份、审计 outbox/回填/repair、写前 journal 的主体实现质量与真实证据（858→341、326 行回填、4 组 STARTED/DONE、新格式真实 ref）均成立，且 369/369、0 warning 可复跑。
- **但 P1 未清零，且命中 ADR D-026 明文验收条件**：
  1. ADR §4 要求“P1-2/P1-4 自动化用例全绿”，而 P1-4 的 fallback/回填/repair 与 FR-SAFE-04 的 journal/reconcile **完全无自动化用例**；状态报告存在不实表述（P1-1）。
  2. ADR §3 要求安全 ref 可见/可恢复/按保留期清理，本提交未做亦未挂账（P1-2）。
  3. 审计“历史测试污染清理”证据口径仍过度，上一轮 P1-1(b) 未真正闭环（P1-3）。
- **若产品裁量坚持先打阶段 tag（不建议）**：必须在 tag 说明中逐条列出 P1-1/P1-2/P1-3 未闭合、说明 `m2b-2b` 仅代表阶段、并声明 M2b/M2 未完成；同时建议先完成 P1-1（补测试，改动量小、风险低）与 P1-3（改证据措辞）。
- **M2b 完成（本 tag 之外）仍缺**：AC-OP-01…32 全量逐条 Given/When/Then 证据与故障注入报告；**真机“杀进程在写中重启”reconcile 演练**（当前 `action=RECONCILE` 真实行数=0）；离线降级完整语义；AC-SAFE-08/09 E2E。`m2b-2b` tag 只代表 M2b-2b 阶段，**不得表述 M2b/M2 完成**（提交说明与状态报告已如此自述，一致）。

**复评转正的最小前置（建议顺序）**：①补 P1-1 自动化用例（journal/reconcile + 审计 fallback/回填/repair）并修正 `m2b2b-status.md:33`；②交付或新 ADR 挂账 P1-2；③修正 P1-3 证据口径（或提供可审计的 JSONL 污染清理）；④建议一并处理 P2-1/P2-2/P2-5；⑤重跑 369 全量 + 真机 reconcile 演练并入库证据。

---

## 8. 边界与副作用披露（诚实记录）

- **未修改**任何源码、配置、`reqs/`、`ui/` 或真实仓库/真实用户数据；未执行 git add/commit/checkout；唯一由本报告新增的文件为 `docs/review-m2b2b-gate-code.md`（未跟踪）。评审全程 `HEAD=4d4a8b5`。**评审末段（20:51–20:52）并行工作流**在测试目录生成了未提交变更（`?? tests/MbdGitTool.Host.Tests/M2b2bAuditJournalTests.cs`、` M tests/MbdGitTool.Host.Tests/TestInfrastructure.cs`），非本报告所为，也不属于受评提交；本报告 §1.2 的 369/369 实测构建发生在这些文件落盘之前（`artifacts/gate-test/bin/MbdGitTool.Host.Tests/debug/MbdGitTool.Host.Tests.dll` mtime≈20:45:21；并行文件 mtime 20:51:19/20:52:52），故 369/369 是 `4d4a8b5` 干净工作树的真实结果，未被并行用例污染。
- **全量测试**：因真实 Host PID 48284 占用 `src/MbdGitTool.Host/bin`，首次直接 `dotnet test` 因文件锁失败（MSB3027/MSB3021，环境问题）；改用 `--artifacts-path artifacts/gate-test` 完成 369/369。构建产物/日志仅落 gitignored `artifacts/`（`artifacts/gate-test/**`、`artifacts/gate-m2b2b-dotnet-test.log`）。
- **真实数据只读**：`mbd.db` 复制到 `%TEMP%\gate-dbcheck` 后用 Python `sqlite3` 只读查询；JSONL/exports/journal/host.log 仅读取/计数；未清理/未修改任何真实数据；未 kill/驱动真实 Host，未启动替代服务器。
- **未执行**：UI CDP 自检、realhost 脚本复跑、真实“杀进程”reconcile 演练、`M2b-2 53/53` 与 `M2a/M2b-1` 自检复跑——这些属 UX Gate 或后续 AC-OP 证据范围，本报告对相关声明只做“是否与代码/磁盘证据一致”的核对。
- **临时产物**：`%TEMP%\gate-dbcheck\*`（只读查询脚本与 DB 副本）在仓库外，供复跑。

---

## 9. 附：关键复跑命令

```powershell
# 1) 全量测试（隔离 artifacts，避免运行中 Host 锁 bin）。本报告实测 369/369、0 warning。
dotnet test MbdGitTool.sln -c Debug --nologo --artifacts-path artifacts/gate-test

# 2) 真实审计库/JSONL 只读核对（本报告实测：DB=369、JSONL=369、unique keys=369、0 dup）
#    先将 %LOCALAPPDATA%\MbdGitTool\db\mbd.db* 复制到 %TEMP% 后用 sqlite3 只读查询
(Get-Content "$env:LOCALAPPDATA\MbdGitTool\audit\operations.jsonl" | Measure-Object -Line).Lines
Select-String -Path "$env:LOCALAPPDATA\MbdGitTool\logs\host.log" -Pattern 'repairOperationLog|审计回填' | ForEach-Object Line
#   -> repairOperationLog：removed=858 inserted=340 skipped=0 total=341 fallbackRemaining=0
#   -> 审计回填完成：…插入 326 行（跳过 10，回退剩余 0）

# 3) 真实 journal / 备份 ref（本报告实测：4 组 STARTED→DONE；4 仓新格式 ref）
Get-Content "$env:LOCALAPPDATA\MbdGitTool\journal\operations.jsonl"
Get-ChildItem artifacts/real-test/workspace -Directory | ForEach-Object {
  git -C $_.FullName for-each-ref --format='%(refname)' refs/backup/
}

# 4) 新增能力测试覆盖核对（本报告结论：journal/reconcile/回填/repair 零引用）
Get-ChildItem tests -Recurse -Filter *.cs | Select-String -Pattern '(?i)journal|repair|backfill|fallback|reconcile'

# 5) 关键代码锚点
git show 4d4a8b5 --stat
git show 4d4a8b5 -- src/MbdGitTool.Host/Services/OperationAuditService.cs   # 回填/repair
git show 4d4a8b5 -- src/MbdGitTool.Host/Services/JournalReconciler.cs        # reconcile（新增）
git show 4d4a8b5 -- src/MbdGitTool.Host/Services/BackupRefService.cs         # branch 身份/零写
```
