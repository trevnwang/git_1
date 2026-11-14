# M2b-2a 最终 Gate 复评报告（对象 `cbdfa78` + `b967d73`）

> 角色：独立最终 Gate（`gate-m2b2-final`，只读评审）。与并行 `gate-m2b2-code` / `gate-m2b2-ux` 相互独立。
> 结论：**建议打 `m2b-2a` 阶段 tag（受 ADR D-026 约束；仅阶段 tag，不代表 M2b/M2 完成）**。功能对象 `cbdfa78`+`b967d73` 的 P0 与编号 1–5 判据闭环；评审期间落库的 `e40cac4`（ADR D-026 延后 P1-2/P1-4、更正审计证据口径、更新并重跑 M2b-2 自检）已满足本报告 §7.1 的全部“转绿条件”。M2b-2b 未完成、M2b 不完整。

| 项目 | 内容 |
|---|---|
| 评审对象 | 功能对象 `cbdfa78`（`fix(m2b2a): restore untracked safety, branch/safety-ref hardening, audit isolation and retention floor`，2026-11-14 18:36:54 +0800）+ `b967d73`（`fix(m2b2a): audit backupRef column, restore clean-workspace warning, offline false-positive, EMPTY unborn guard`，18:52:01 +0800）；评审期间落库 `e40cac4`（`docs(m2b2a): ADR D-026 deferrals and corrected audit evidence wording`，19:05:32 +0800；**无 `src/`、`tests/`、`ui/src/` 变更**）。`HEAD=e40cac4264cf9e291580601d02d3c8c0fd30c819` |
| 输入 | `docs/review-m2b2-code.md`（P0=1/P1=5）、`docs/review-m2b2-ux.md`（P0=2/P1=8）、`docs/m2b-status.md`、SRS `reqs/requirements.md` §7.6/§9.7/附录 D、`reqs/v0.2/ac-catalog.md` AC-OP-30、ADR D-025、`reqs/v0.2/fixtures/m2-matrix.json`、M2b-1 Gate 报告口径 |
| 环境 | Windows 11 / .NET SDK 10.0.404 / git 2.59 / 工作区 `D:\Workspace\git_1`；真实数据 `%LOCALAPPDATA%\MbdGitTool`；评审期间真实 Host PID 54532（18:52:02 启动；Host dll 18:50:01=`cbdfa78`，UI bundle 18:49:55 已含 `b967d73` 文案）在约 19:00 前关闭（9222 拒绝连接；并行评审期间，关闭者未由本评审确认） |
| 方法 | ① `git show`/diff 静态核对（行号对应 `b967d73`）；② 全量 `dotnet test`（隔离 `--artifacts-path`，避免运行中 Host 锁 bin）；③ `--no-incremental` 洁净构建核 **0 warning/0 error**；④ `%TEMP%` 黑盒探针（临时 git 仓 + 引用 Host 项目）复验恢复矩阵/安全 ref/保留下限；⑤ 隔离 CDP 自检（独立 headless Edge + 注入测试桥 + 当前 `ui/dist`，不触真实 Host/真实仓库）；⑥ 只读核对真实 `operation_log`/JSONL/exports/真实导出文件 |
| 边界 | **未修改**任何源码/配置/`reqs`/UI 源码或真实仓库；唯一源码树新增为 `docs/review-m2b2-gate.md` 与 gitignored `artifacts/gate-m2b2-final/`；探针仓与自检临时产物均在 `%TEMP%`；真实库/JSONL/exports 全程只读（未清理/未标注污染行） |
| 证据 | `artifacts/gate-m2b2-final/`：`dotnet-test.log`、`build.log`、`restore-probe.log`、`m2b2-selfcheck-result.json`（基线旧脚本 52/58）、`m2b2-selfcheck-updated-script.log` + `…updated-result.json`（更新后 53/53）、`m2b2-selfcheck-history.txt`、`db-and-build-evidence.txt`、2 张 UI 截图（`ui-02-empty-push-disabled.png`、`ui-03-audit-backupref-column.png`） |

---

## 0. 结论（判决）

> **建议打 `m2b-2a` 阶段 tag（受 ADR D-026 约束；仅阶段 tag，不代表 M2b/M2 完成）。**
>
> 功能对象 `cbdfa78`+`b967d73` 的编号 1–5 判据均闭环：恢复对 tracked/untracked/ignored/“挡路”未跟踪文件全部阻断且文件与 HEAD 不变（黑盒探针 31/31）；真正 unborn 的 EMPTY 在 UI 禁用 PUSH 并给出 `E_REF_NOT_FOUND` 原因、有 HEAD 的 EMPTY 首推仍由 Host 矩阵单源允许；审计 SQLite 可查询/导出、测试前后真实库 **528→528 行（0 行差）**、`backupRef` 列已显示、审计保留下限 **≥90**（30/1→90 实测）；`source=local + 无显式错误` 不再误报离线、有显式错误才禁用 FETCH（双向动态验证）。全量测试 **337/337（Core 71 + Host 266，0 失败/0 跳过）**、洁净构建 **0 warning / 0 error**。
>
> **原判与改判（诚实记录）**：在 `b967d73` 基线本 Gate 原判“不打”，阻断项为本报告 §7.1 的 B1–B4。评审期间落库的 **`e40cac4`** 已逐项满足转绿条件（无 `src/tests/ui/src` 变更，故 337/337 与 0 warning 仍适用）：
> 1. **B1/B2（code P1-2/P1-4）**：`reqs/adr/D-026-m2b2a-deferrals.md` 以 **Accepted** 状态书面延后至 M2b-2b，并写明补偿条件（clean 门禁/安全 ref/JSONL 主链路/不伪装成功/仅阶段 tag）与 M2b-2b 必须完成的具体项；
> 2. **B3（自检）**：`ui/scripts/cdp-m2b2-selfcheck.mjs` 已把 EMPTY 断言改为“unborn 必须禁用 PUSH + `E_REF_NOT_FOUND` + 不出现确认条”，对当前 `ui/dist` 重跑 **53/53 PASS**（原 6 项红已消除）；
> 3. **B4（证据口径）**：`docs/m2b-status.md:54-55` 已更正为“历史 521 行含预隔离测试污染、不作为生产数据证据；测试隔离修复后不再新增真实库行”。
>
> **打 tag 的随附约束（必须写入 tag 说明）**：① 仅标 `m2b-2a` 阶段，P1-2（备份分支身份/恢复校验）与 P1-4（审计写失败降级/JSONL→DB 回填）**已知未闭合，必须在 M2b-2b 完成并复测全绿**；② 历史审计库污染清理/重建、`refs/backup/restore/*` 可见/可恢复、`cdp-m2b2-realhost-check.mjs` 以**新构建真实 Host** 重跑入库，均属 M2b-2b；③ **M2b-2b 未完成、M2b 与 M2 均不完整，任何“M2b 完成/M2 交付”声明不成立。**

| 判据 | 判定 | 一句话证据 |
|---|---|---|
| 1 恢复（code P0-1 / UX P0-1） | **PASS** | 探针 31/31：tracked→`E_DIRTY_WORKTREE`；untracked/ignored/“挡路”未跟踪→`E_WORKTREE_OVERWRITE`；文件原样、HEAD 不变；UI 确认文案披露 clean 要求；仓库内 tracked/untracked 测试存在且在 337 全量中运行 |
| 2 EMPTY（UX P0-2） | **PASS** | 真正 unborn：PUSH `data-enabled=false`、`code=E_REF_NOT_FOUND`、tooltip 含“先 COMMIT”、无确认条；有 HEAD 的 EMPTY 首推仍由 Host 矩阵单源允许（测试 green）；自检脚本已在 `e40cac4` 更新并重跑 53/53 |
| 3 审计（code P1-1/P1-3、UX P1-1） | **PASS** | 真实库只读可查（528 行）、真实导出 523 行含 `backup_ref`；测试前后真实库 **528→528**、JSONL 336→336、exports 11→11；UI 审计表含“备份”列并显示 ref；保留 30/1→90（探针）；证据口径已在 `e40cac4` 更正 |
| 4 离线（UX P1-5） | **PASS** | `source=local` 无显式错误 + `remoteStale=true`：无离线 banner、FETCH 未禁用；有显式远程错误：banner+`E_OFFLINE` 禁用 FETCH（动态双向） |
| 5 测试/构建 | **PASS（dotnet）/ 受限（selfcheck）** | `dotnet test` 337/337、0 warning；M2b-2 自检更新后 53/53 PASS；M2a/M2b-1 自检本轮无 Host 可实跑，以历史绿档（11/11、68/68）+ 本轮隔离回归抽样 + diff 范围判定无回退 |
| 6 结论 | **建议打 `m2b-2a` 阶段 tag** | P0 闭环 + 编号 1–5 判据 PASS + ADR D-026 书面延后 P1-2/P1-4 + 自检/证据更新；M2b-2b 未完成、M2b 不完整（约束见上） |

### 0.1 评审后处置复核（`e40cac4`，只读）

| 处置项 | 复核 | 判定 |
|---|---|---|
| ADR D-026 | 全文核对：仅用于 M2b-2a 阶段 tag；P1-2/P1-4 延后至 M2b-2b；补偿条件 1–4；M2b-2b 必须完成项（分支身份、审计 outbox/回填、安全 ref 可见性） | **符合本报告 §7.1 转绿条件** |
| 状态报告证据口径 | `docs/m2b-status.md` 更正“521 行真实”表述，声明历史行含预隔离测试污染、非生产证据 | **B4 闭环** |
| M2b-2 自检更新 | `cdp-m2b2-selfcheck.mjs`：删除“EMPTY 启用/确认条”旧断言，改为 `empty.unborn-push-disabled`、`empty.unborn-tooltip-e-ref-not-found`、`empty.unborn-no-confirm-bar`；本轮对当前 `ui/dist` 重跑 **53/53 PASS**（`artifacts/gate-m2b2-final/m2b2-selfcheck-updated-*.log/json`）；18:41 旧构建/旧脚本 57/57 的证据文件已被 `e40cac4` 后的新脚本运行覆盖，历史对照见 `…/m2b2-selfcheck-history.txt`（基线 `b967d73`+旧脚本为 52/58，见 `…result.json`） | **B3 的 M2b-2 自检闭环**；`cdp-m2b2-realhost-check.mjs` 仍为旧断言→随 M2b-2b 重跑（见 §8） |
| 源码/测试不变 | `git diff --stat b967d73..e40cac4 -- src tests ui/src` 为空 | 判据 1–5 结论不变 |

---

## 1. 判据 1：恢复安全（code P0-1 / UX P0-1）

### 1.1 Host 阻断矩阵（`src/MbdGitTool.Host/Services/BackupRefService.cs`）

| 输入 | 行为·证据 | 判定 |
|---|---|---|
| tracked dirty | `status --porcelain --untracked-files=no` 非空 → `E_DIRTY_WORKTREE`（`:169-190`）；测试 `M2b2BackupRefTests.cs:117-134` 断言错误码 + HEAD 不变 + 文件内容保留 | **PASS** |
| untracked（含与备份树冲突的“挡路”文件） | `status --porcelain --untracked-files=normal --ignored=matching` 非空 → `E_WORKTREE_OVERWRITE`（`:192-213`），发生在安全 ref 与 `reset --hard` **之前** | **PASS** |
| ignored | 同上（`--ignored=matching` 覆盖 `!!` 条目） | **PASS** |
| 干净工作区 | 先 `update-ref refs/backup/restore/<stamp>/<TYPE> <preHead>`（失败即拒绝 reset）、再 `reset --hard <ref>`（`:215-245`）；成功返回 `Restored=true` 并刷新快照 | **PASS** |

### 1.2 黑盒探针（本轮独立复验，`artifacts/gate-m2b2-final/restore-probe.log`，`PROBE_FAILURES=0`）

- tracked dirty：`E_DIRTY_WORKTREE`，HEAD 仍为 second，`a.txt` 保持本地改动（4 项）。
- untracked 非冲突文件：`E_WORKTREE_OVERWRITE`，HEAD 不变、文件保留、details 含 `untrackedFiles`（5 项）。
- **冲突场景**（backup 树含 `b.txt`、当前 HEAD 已删除、用户新建同名 untracked `b.txt=PRECIOUS-UNTRACKED`）：`E_WORKTREE_OVERWRITE`，文件未被覆盖、HEAD 不变（4 项）——正是上一轮 P0-1 的黑盒复现路径，本轮已阻断。
- ignored 文件（`.gitignore` 匹配的 `debug.log`）：`E_WORKTREE_OVERWRITE`，文件保留、HEAD 不变（5 项）。
- 干净恢复：成功后 HEAD=备份 SHA、`preHead=second`；安全 ref 形如 `refs/backup/restore/20261114110252226-b0b0fb67/IMPL`（**毫秒+8 位 GUID**，`:223`），指向恢复前 HEAD；同秒内第二次恢复生成**不同**安全 ref，不再互相覆盖（8 项）。

### 1.3 UI 确认文案（`ui/src/stores/workbench.ts:2889`）

> “……这会覆盖当前分支状态：恢复后本地 HEAD 指向备份 SHA，未包含在该备份中的未推送改动可能丢失。**恢复前工作区必须干净（无已跟踪改动、无未跟踪/忽略文件），否则 Host 会拒绝且不改动任何文件。**”

与 Host 策略一致；隔离自检 `backup.confirm-shows-overwrite` **PASS** 并回显上述全文。`ComponentDrawer.vue:258-265` 的“恢复…”按钮文案含“会覆盖当前分支状态，需确认”。

### 1.4 测试与判定

- 仓库内测试：`M2b2BackupRefTests.Restore_DirtyTrackedWorktree_BlockedWithoutChangingAnything`、`Restore_UntrackedFile_IsBlockedAndPreserved` 均存在，且在 337 全量中运行通过。
- **判定 PASS**。残留（不阻断本判据）：① 无 ignored/“挡路冲突”的**仓库内**回归测试（本轮以外部探针补证，属测试缺口）；② untracked 用例未断言 HEAD 不变（代码顺序上必然不变）；③ 阻断策略为“全量 untracked/ignored 一律拒绝”，只给数量不给文件名、无冲突预览/选择（AC-OP-30③④ 只满足“默认不覆盖”，不满足“展示将丢失什么”）；任何含 `bin/obj/node_modules` 等 ignored 产物的常见仓库将无法恢复（可用性风险，P2/P1-UX 残留）；④ 错误 details 中 `requiresCommit:false` 为复制残留字段。

---

## 2. 判据 2：EMPTY（UX P0-2）

### 2.1 真正 unborn（无 HEAD）

- 状态机：`src/MbdGitTool.Core/Domain/RepoStateEvaluator.cs:72-75`——“HEAD unborn → EMPTY”，即真实 `EMPTY` 只在无 HEAD 时产生。
- UI 唯一可用性来源：`workbench.ts:527-549`，在 Host 矩阵结果上叠加早退守卫（`:530-539`）：`state==='EMPTY' && !repo.headSha` → PUSH `enabled:false, code:'E_REF_NOT_FOUND', alt:'先 COMMIT…'`；`headSha` 映射为 `dto.headSha ?? null`（`viewmodel.ts:174`）。
- 动态实测（隔离自检，`artifacts/gate-m2b2-final/m2b2-selfcheck-current.log`）：
  - `empty.push-disabled-with-ref-not-found` **PASS**：`{"disabled":true,"enabled":"false","reasonCode":"E_REF_NOT_FOUND","requiresConfirm":"false","title":"空仓无提交：请先 COMMIT 生成首个提交后再 PUSH（首推将建立 upstream） [E_REF_NOT_FOUND] → 先 COMMIT（含 message）生成首个提交，再 PUSH 发布分支"}`；
  - 点击不再打开确认条（原“空仓首推”确认条不会出现，零写）；
  - 截图 `ui-02-empty-push-disabled.png`（PUSH 禁用、无确认条）。
- **判定 PASS**：真正 unborn 的 EMPTY 不再“可点必失败”。

### 2.2 有 HEAD 的 EMPTY 首推仍可用（Host 矩阵单源）

- Host 矩阵未改：`RepoActionService.cs:74` `EMPTY="NYCYY"`（PUSH 允许）；`GetAvailability` 由矩阵派生 `Enabled=MatrixAllowed`、PUSH `RequiresConfirm=true`（`:131-180`）。
- 单源夹具：`reqs/v0.2/fixtures/m2-matrix.json` EMPTY.PUSH `{enabled:true, requiresConfirm:true, reasonCode:CONFIRM_REQUIRED}`；`RepoActionAvailabilityMatrixTests` 逐格断言通过。
- 行为测试：`M2b2EmptyPushTests.GetAvailability_EmptyState_PushEnabledAndRequiresConfirm`（矩阵单源）、`EmptyFirstPush_SetsUpstreamAndCreatesRemoteBranch`（预置 HEAD 的 EMPTY → `push -u origin HEAD` 建立 upstream、bare 远端 main=HEAD）、`EmptyPush_UnbornHead_FailsWithReadableCodeAndNoRemoteWrite`、`NoUpstreamPush_AlsoSetsUpstream`，337 全量中通过。
- UI 守卫只在 `!headSha` 生效，若快照为 EMPTY 且带 HEAD（矩阵单源口径）则 PUSH 仍按 Host 矩阵启用。
- **判定 PASS**（Host 矩阵单源；真实状态机下 EMPTY 恒 unborn，故真实 Host 无法演示“成功首推”，需先 COMMIT——与 SRS §7.6/D-025 的“EMPTY 直接首推”规格冲突仍未裁决，见 §8）。

### 2.3 自检（原为过时断言，已在 `e40cac4` 更新并重跑）

- 基线 `b967d73` 时 `ui/scripts/cdp-m2b2-selfcheck.mjs`（测试桥，隔离）为“18:41 旧构建 57/57、当前构建 52/58”，6 项失败均为过时断言（仍期望 unborn EMPTY 的 PUSH 启用 + 确认条）。
- `e40cac4` 已将该段改为：`empty.unborn-push-disabled`、`empty.unborn-tooltip-e-ref-not-found`、`empty.unborn-no-confirm-bar`；本轮对当前 `ui/dist` 重跑 **53/53 PASS**（`artifacts/gate-m2b2-final/m2b2-selfcheck-updated-script.log` 与 `…updated-result.json`；历史对照见 `…/m2b2-selfcheck-history.txt`）。
- 仍过时：`ui/scripts/cdp-m2b2-realhost-check.mjs` 仍断言 `real.empty-push-enabled` 与“仅本地=离线/`real.offline-banner`”，与 `b967d73` 的两处修复相反——按 ADR D-026 与 §8，该真实 Host 证据随 M2b-2b 以新构建重跑入库（不阻断 M2b-2a 阶段 tag，但必须写入 tag 说明）。
- **判定：功能 PASS；M2b-2 隔离自检已更新并全绿；真实 Host 自检待 M2b-2b。**

---

## 3. 判据 3：审计（code P1-1/P1-3、UX P1-1）

### 3.1 SQLite 可查询 / 可导出

- 真实库只读核对（`db-and-build-evidence.txt`）：`count=528, maxid=528`；按动作 `CLEANUP_RETENTION 107 / COMMIT 92 / DELETE 70 / FETCH 68 / SYNC 58 / … / RESTORE_BACKUP 7`；有 `backup_ref` 的行 31。
- 真实导出（Host `exportOperationLog`，18:43 落盘）：CSV 523 数据行、表头含 `backup_ref/job_item_id`；JSON 523 项、字段含 `backupRef`（`%LOCALAPPDATA%\MbdGitTool\exports\operation-log-20261114104311-3d09fc28.csv` / `…104312-a8f04acc.json`）。
- 单测：`M2b2AuditDatabaseTests`（查询/过滤/分页/CSV/JSON/降级/重启）、`M2b2RouterApiTests`（路由/审计恰 1 行）在 337 全量中通过；`M2b2BackupRefTests.RetentionService_*` 覆盖清理与审计行。
- **判定 PASS**。

### 3.2 测试隔离：真实库行数差 = 0

- 机制：`tests/MbdGitTool.Host.Tests/TestInfrastructure.cs:322-349` `TestAudit.NewConfig()` 为每次测试注入临时 `AuditRoot/DatabasePath/ExportsRoot/JobsRoot`（`%TEMP%\MbdGitToolTests\…`）；全部 Host 测试均经该工厂构造（grep 无 `new OperationAuditService()` 默认真实路径的用例；`new AppConfig` 的少数用例不构造审计服务或另带 `AuditRoot`）。
- 实测窗口（全量 `dotnet test` 前后，19:00 复核）：

| 指标 | 测试前（18:55:14） | 测试后（18:59:03 / 19:00:20） | 差 |
|---|---|---|---|
| 真实库 `operation_log` 行数 / max(id) | 528 / 528 | **528 / 528** | **0** |
| 真实 `operations.jsonl` 行数 | 336 | 336 | 0 |
| 真实 `exports/` 文件数 | 11 | 11 | 0 |
| 真实库 `max(ts)` | 10:45:14.78Z | 10:45:14.78Z | 不变 |

- 旁注：`OperationAuditService.ConfigureTestIsolation`（`:62-64`，`cbdfa78` 新增）**无任何调用方**（死代码）；实际隔离来自父提交 `1371f51` 的 `TestAudit.NewConfig`。`HostLog` 仍写真实 `host.log`（`cbdfa78` 未隔离，测试期 7.0→7.3 MB），属既有 P2-11，不影响“真实库 0 行差”。
- **判定 PASS**。

### 3.3 审计表 `backupRef` 列（UX P1-1）

- `ui/src/components/overlays/ModalHost.vue:347` 表头追加“备份”，`:377` 每行渲染 `item.backupRef`（`data-testid="audit-backup-ref"`，`title` 全 ref）；构建产物 `ui/dist`/Host bin 内 bundle 已含该 testid（18:49:55）。
- 隔离自检：`audit.*` 全部 PASS，截图 `ui-03-audit-backupref-column.png` 可见「备份」列与 `refs/backup/sync/x/IMPL/main`。
- 残留：表格仍不展示 `jobItemId/requestId`，但底部 hint 仍称“批量作业行含 jobId/jobItemId/backupRef”（`:390`）——列已补一半、hint 仍过度声明（P2，非本判据要求）。
- **判定 PASS**（本判据只要求 `backupRef` 列）。

### 3.4 审计保留 ≥90

- `AppConfig.cs:79,152`：`EffectiveAuditRetentionDays = ClampDays(…, min:90, max:3650)`、`Load` 同样 `min:90`；生产路径 `OperationAuditService(:72)` 取 `EffectiveAuditRetentionDays`，`CleanupExpired` 由其算 cutoff（`:387-401`）。
- 探针实测：`default=90`、配置 30→**90**、配置 1→**90**、3650→3650、3651→3650（`restore-probe.log`）。
- 残留：无“配置 30 被夹到 90”的仓库内回归测试（`M2b2AuditDatabaseTests.cs:387-388` 只覆盖 0→90 与 99999→3650）；`OperationAuditService` 内部构造参数仍 `Math.Clamp(…,1,3650)`（`:137`，非用户配置路径）；附录 D `delete.retentionDays`（.trash 7–90 可配）仍未实现（既有 P2/FR-OP-06）。
- **判定 PASS（代码/实测）；测试缺口记录。**

### 3.5 P1-1 证据口径（已更正；历史污染清理归 M2b-2b）

- 基线 `b967d73` 时 `docs/m2b-status.md:54-55` 仍写“521 行真实可查 / 真实 521 行导出成功”；实际真实库 **528 行**，其中 fixture/test-like 组件行 370、历史测试期 `CLEANUP_RETENTION` 约 104 行（与 `review-m2b2-code.md` §2.2 的约 518 行测试污染同源，之后又追加 5 行真实 Host `RESTORE_BACKUP`）。
- `e40cac4` 已更正为“历史 521 行含预隔离测试污染、不作为生产数据证据；隔离修复后不再新增真实库行”，与 §3.2 的 528→528 实测一致。
- 历史污染行本身仍未清理/重建（ADR D-026 明确归 M2b-2b：可审计的清理/重建入口）。
- **P1-1：隔离根因 + 证据口径已闭环；历史污染清理归 M2b-2b。**

---

## 4. 判据 4：离线（UX P1-5）

### 4.1 判定逻辑（`ui/src/stores/workbench.ts:891-920`）

- 仅 `snapshot.source === 'local'` 进入候选；标记来源只保留**显式**项：Host `offline`、`remoteError`、`snapshot.error`、逐仓 `E_OFFLINE/E_NETWORK/E_REMOTE*/E_GITEA*`、`remote_*` flag；`freshness.remoteStale` **不再**作为离线标记（`:904` 注释）。
- `availability()` 仅在 `state.remoteOffline.offline` 时把 FETCH 覆写为禁用（`code:E_OFFLINE`，`:540-548`），其余五键仍由 Host 矩阵单源。

### 4.2 动态双向验证（隔离自检，当前 `ui/dist`）

- 有显式远程错误（`snapshot.error='Gitea 同步失败（模拟）…'`，`source=local`）：`offline.banner`、`offline.statusbar`、`offline.fetch-disabled`（`E_OFFLINE`）、`offline.fetch-tooltip` 全 **PASS**。
- 无显式错误（把同一夹具的 `snapshot.error` 置 null、`remoteStale=true`、`source=local`）：`local-no-error.no-false-offline` PASS（无“离线/远程不可达”banner）、`local-no-error.fetch-not-disabled` PASS（FETCH 可用）、`status-network=在线`；同一运行中旧的 `offline.*` 断言按预期失败——恰好证明“仅本地正常模式不再误报离线”。
- **判定 PASS**。

### 4.3 残留（不阻断本判据）

- 本地扫描失败（`snapshot.error` 为本地错误）仍会被标为“离线/远程不可达”（显式错误触发，语义仍混淆本地/远程，P2）。
- PULL（fetch+merge，同为网络操作）不随离线禁用；卡片/四仓树无分区离线态（七态未做）。
- `source=mock` / 仅本地模式状态栏显示“在线”（未区分“未配置远程/远程阶段失败/断网”）；首帧默认在线（P2）。
- `detectRemoteOffline` 上方 docstring 仍写“远程数据过期（remoteStale）”为来源（`:894`），与实现不符（注释陈旧）。

---

## 5. 判据 5：全量测试、构建与 selfcheck 不回退

### 5.1 全量测试（PASS）

```
dotnet test MbdGitTool.sln --artifacts-path %TEMP%\mbd-gate-artifacts -v minimal
# Core.Tests ：已通过! 失败:0，通过:71，已跳过:0，总计:71
# Host.Tests ：已通过! 失败:0，通过:266，已跳过:0，总计:266（3 m 08 s）
# EXIT=0  => 合计 337/337
```

日志：`artifacts/gate-m2b2-final/dotnet-test.log`。

### 5.2 洁净构建（PASS）

```
dotnet build MbdGitTool.sln --artifacts-path %TEMP%\mbd-gate-build-a --no-incremental -v minimal
# 已成功生成。0 个警告 / 0 个错误（TreatWarningsAsErrors=true）
```

日志：`artifacts/gate-m2b2-final/build.log`。说明：直接构建仓库 `bin` 会因运行中 Host 锁定 DLL 出现 `MSB3026/MSB3027`（本轮首次尝试即如此，见 `dotnet-test-first-attempt.log`）；改用隔离 `--artifacts-path` 后 0 warning。

### 5.3 M2a / M2b-1 selfcheck 不回退（受限验证）

- 历史绿档：`cdp-m2a-selfcheck.mjs` **11/11**（`artifacts/m2b1-gate-ux2/selfcheck-m2a.log`，17:09）；`cdp-m2b1-fix-selfcheck.mjs` **68/68**（`artifacts/m2b1-fix-selfcheck/result.json`，17:08）。
- 本轮 **无法实跑**：评审期间唯一真实 Host（PID 54532）在约 19:00 前关闭，`127.0.0.1:9222` 拒绝连接；只读边界下未自行启动新 Host（会写真实 `jobs.json`/审计状态且需 Gitea token）。重跑 M2b-1 自检还会因其真实只读 `FETCH` 向真实库写审计行，破坏“评测前后真实库行差=0”的判据窗口。
- 替代证据：① `b967d73` diff 仅触及 **审计表新增列**、**EMPTY unborn 守卫**、**离线标记移除**、**恢复确认文案**，不触及 M2a/M2b-1 自检断言的单源 action/tooltip/确认条/状态栏/批量入口/焦点/事件路径；② 本轮隔离自检（当前构建）中 M2a/M2b-1 回归抽样 **PASS**：`m2a.tooltip-format`、`no-upstream.confirm-bar`（含 `setUpstream=true`）、`empty.no-write-before-accept`、`m2b1.task-center-empty`、`page.no-console-errors`。
- **判定：无回退证据，但属“受限验证”**（见 §7.3 同类证据缺口）。

### 5.4 M2b-2a 自有 UI 自检（基线红 -> `e40cac4` 更新后全绿）

| 时间 | 脚本/构建 | 结果 |
|---|---|---|
| 18:41（旧基线） | 旧脚本 + `b967d73` 前构建 | 57/57（证据文件后被 `e40cac4` 后运行覆盖，见 `…/m2b2-selfcheck-history.txt`） |
| 本轮（基线 `b967d73`） | 旧脚本 + 当前 `ui/dist` | 52/58（6 项 `empty.*` 过时断言，`m2b2-selfcheck-result.json`） |
| 本轮（`e40cac4`） | 新脚本 + 当前 `ui/dist` | **53/53 PASS**（`m2b2-selfcheck-updated-result.json`） |

结论：基线红是**脚本未随 UI 修复更新**而非功能回退；`e40cac4` 更新后已全绿，可作为 M2b-2a 阶段 tag 的自检证据。`cdp-m2b2-realhost-check.mjs` 仍待 M2b-2b 以新构建真实 Host 重跑。

---

## 6. P0/P1 状态矩阵（对照两轮评审）

| 来源 | 编号 | 事项 | 状态 | 证据 |
|---|---|---|---|---|
| code | P0-1 | restore 静默丢 untracked | **CLOSED** | §1（Host 阻断 + UI 文案 + 探针 31/31） |
| code | P1-1 | 真实审计库/导出被测试污染并当“真实 521 行”证据 | **CLOSED**（隔离已修 + `e40cac4` 更正口径；历史行清理归 M2b-2b） | §3.2 §3.5 |
| code | P1-2 | 备份 ref 丢失分支身份；恢复不校验当前分支 | **OPEN → ADR D-026 阶段延后（M2b-2b 必须闭合）** | 代码未变（`<ts>/<TYPE>`、restore 不校验分支）；`reqs/adr/D-026-m2b2a-deferrals.md` Accepted（仅阶段 tag） |
| code | P1-3 | 审计保留可 <90 | **CLOSED**（代码+探针；测试缺口） | §3.4 |
| code | P1-4 | SQLite 写失败静默/不降级/无回填 | **OPEN → ADR D-026 阶段延后（M2b-2b 必须闭合）** | 代码未变；ADR D-026 补偿条件 + M2b-2b 必须项（outbox/回填） |
| code | P1-5 | 安全 ref 秒级覆盖 + 产品内不可见/不可恢复 | **PARTIAL** | 唯一性已修（毫秒+GUID，探针）；`getBackupRefs` 仍只列 `refs/backup/sync/*`、restore 白名单仍拒 `refs/backup/restore/*`（`M2b2BackupRefTests.cs:160` 断言 false）、无保留策略（ADR D-026 归 M2b-2b） |
| UX | P0-1 | 恢复确认未披露真实破坏面 | **CLOSED** | §1.3 |
| UX | P0-2 | EMPTY 启用但必失败 | **CLOSED（功能+自检）** | §2；`e40cac4` 更新自检后 53/53 |
| UX | P1-1 | 审计表缺 backupRef 列 | **CLOSED（列）** | §3.3；jobItemId/requestId 仍未展示、hint 过度声明（P2） |
| UX | P1-2 | “真实 521 行”证据不成立 | **CLOSED（口径更正）** | §3.5；历史污染清理归 M2b-2b |
| UX | P1-3 | `createdAt` 用 commit committerdate、非备份时间；ref 无分支/时间格式 | **OPEN** | `BackupRefService.cs:367` `%(committerdate:iso-strict)` |
| UX | P1-4 | 恢复确认缺冲突/当前状态预览；dirty 在 accept 后暴露 | **PARTIAL** | 文案已披露 clean 要求；仍无预览/点击前禁用 |
| UX | P1-5 | 离线误报（本地正常模式/本地扫描失败）、mock 伪在线、仅 FETCH 降级 | **PARTIAL** | §4：误报已修（有显式错误才离线）；本地失败语义/ PULL/分区态/mock 在线仍残留 |
| UX | P1-6 | 审计过滤维度不足、导出不受过滤约束 | **OPEN** | 未在本批改动内 |
| UX | P1-7/P1-8 | M2b-2b 缺口与 AC 全量证据 | **OPEN（按指示，M2b-2b 范围）** | 全仓无 journal/reconcile；无 `m2b2-ac-report.md`；AC-OP-31/32 未实现 |

---

## 7. 阻断项处置与 tag 随附约束

### 7.1 原阻断项处置（基线 `b967d73` → 评审后 `e40cac4`）

| 原阻断项 | 处置 | 判定 |
|---|---|---|
| **B1 code P1-2**（备份 ref 分支身份/恢复不校验当前分支） | `reqs/adr/D-026-m2b2a-deferrals.md` **Accepted**：仅用于 M2b-2a 阶段 tag 的书面延后；补偿条件 1–4；M2b-2b 必须完成 `<ts>/<branch>/<TYPE>`、旧 ref 迁移、列表/恢复展示分支、恢复校验分支 | **条件式转绿（延后已授权）** |
| **B2 code P1-4**（SQLite 写失败静默/不降级/无回填） | 同 ADR D-026：延后至 M2b-2b（outbox/回填、查询合并回退、修复历史污染重建入口）；补偿条件“JSONL 始终写、不伪装成功”已由代码/§3.2 验证 | **条件式转绿（延后已授权）** |
| **B3 M2b-2 UI 自检** | `e40cac4` 更新 `cdp-m2b2-selfcheck.mjs`（EMPTY 改为禁用 + `E_REF_NOT_FOUND` + 无确认条）；本轮对当前构建重跑 **53/53 PASS** | **闭环（M2b-2 隔离自检）**；`cdp-m2b2-realhost-check.mjs` 待 M2b-2b 重跑 |
| **B4 P1-1 证据口径** | `docs/m2b-status.md:54-55` 已更正为“历史行含预隔离测试污染、非生产证据；隔离修复后不再新增”；与 528→528 实测一致 | **口径闭环**；历史行清理归 M2b-2b |

### 7.2 `m2b-2a` 阶段 tag 必须随附的约束（写入 tag 说明）

1. 仅标注 **M2b-2a 阶段**；**P1-2、P1-4 已知未闭合**（ADR D-026），必须在 M2b-2b 完成并复测全绿，不得带入 M2b/M2 完成声明。
2. M2b-2b 必须完成：备份 ref 分支身份/恢复校验、审计 outbox/JSONL→DB 回填、`refs/backup/restore/*` 可见/可恢复/保留、历史审计污染清理/重建、`cdp-m2b2-realhost-check.mjs` 新构建真实 Host 重跑入库。
3. **M2b-2b 未完成、M2b 与 M2 不完整**；本 tag 不支持任何“M2b/M2 完成”声明。

### 7.3 非阻断残留（M2b-2b / P2，不阻断阶段 tag）

- code P1-5 的“安全 ref 可见/可恢复/保留策略”（唯一性已修）。
- fixed-path 全量阻断 untracked/ignored 的可用性（无文件名/预览/例外/`git stash -u` 替代）；无 ignored/冲突的仓库内回归测试；untracked 用例未断言 HEAD。
- 审计未知配置 30→90 的回归测试；`OperationAuditService` 内部 clamp 1 的旁路；`delete.retentionDays` 未实现。
- 审计表 jobItemId/requestId 未展示、hint 过度声明；`createdAt` 语义；恢复无 preflight 预览。
- 离线：本地扫描失败仍称“远程不可达”、PULL 未联动、无分区七态、mock/local-only 显示“在线”、docstring 陈旧。
- `cbdfa78` 提交说明与 diff 不完全一致（过程项）：`audit test isolation override to temp DB/exports` 实际只新增了**无调用方**的 `OperationAuditService.ConfigureTestIsolation`（隔离来自父提交 `1371f51` 的 `TestAudit.NewConfig`）；`M2b matrix fixture normalized (EMPTY.PUSH enabled…)` 在该提交中**未改夹具**（父提交 `1371f51` 已是 `EMPTY.PUSH=Y`）；`branch … hardening` 无对应 diff。
- `docs/images/m2b/audit-log.png` 仍为测试污染期截图（如引用需注明）。
- `HostLog` 未隔离（测试写真实 `host.log`，7.0→7.3 MB）。

---

## 8. M2b-2b 未完成（tag 说明必写）

- **ADR D-026 延后项（必须完成）**：P1-2 备份 ref 分支身份/恢复校验（`<ts>/<branch>/<TYPE>`、旧 ref 迁移、列表/恢复展示分支、不一致阻断或二次确认）；P1-4 审计 outbox/JSONL→DB 回填、运行期降级/健康度、历史测试污染清理/重建入口。
- 写前 journal + 崩溃 reconcile（FR-SAFE-04 P0）：全仓无实现（仅 SQLite `journal_mode=WAL`）。
- 离线降级完整行为（分区七态、PULL 联动、恢复通知、远程数据时点区分）。
- 备份恢复完整语义：`createdAt` 为 ref 创建时间、冲突/状态预览、安全 ref 可见可恢复、保留策略。
- 审计健壮性：导出与过滤对齐、CSV 注入防御加固、大文件/OOM 治理。
- FR-OP-12 组件纳入/移出（AC-OP-31/32）、`.trash` 记录选择（AC-OP-30①）。
- AC-OP-01…32 / AC-REPO / AC-SAFE 逐条证据 + 故障注入报告（无 `docs/m2b2-ac-report.md`）。
- 真实 Host CDP 自检（`cdp-m2b2-realhost-check.mjs`）以**新构建**重跑并入库。
- FR-REPO-05 其余修复类（Repair/Re-clone、detached 建分支）与 SRS §7.6/D-025 的 EMPTY 首推规格冲突裁决。

> **M2b 不完整、M2 不完整**；本 Gate 只支持“M2b-2a **阶段** tag”，不支持 M2b/M2 完成声明。

---

## 9. 边界与副作用披露（诚实记录）

- **未修改**任何源码、配置、`reqs`、UI 源码或真实仓库；唯一源码树写盘为 `docs/review-m2b2-gate.md`（新增）与 gitignored `artifacts/gate-m2b2-final/`（日志/截图/证据）。
- 全量测试与构建输出落 `%TEMP%` 的 `--artifacts-path`（不写仓库 `bin/obj`；首次尝试因运行中 Host 锁 DLL 失败，未产生测试写入）。
- 真实 `%LOCALAPPDATA%\MbdGitTool\db\mbd.db`、`audit/operations.jsonl`、`exports/`：**只读**；本轮测试/探针/自检前后 **528→528 行、max(id)=528 不变、JSONL 336→336、exports 11→11**。未清理/未修改历史污染行。
- 黑暗面探针（untracked 冲突/ignored 等）仅在 `%TEMP%\gate-restore-probe-*` 临时仓执行并删除，未触碰真实仓库；探针通过 ProjectReference 引用 Host 项目、构建到 `%TEMP%\gate-probe-artifacts`（不写仓库 bin、不被运行中 Host 的 DLL 锁阻断）。
- 隔离 CDP 自检自行启动 headless Edge（独立临时 profile/端口）与静态服务，未触达真实 Host/真实仓库；截图/结果落 `%TEMP%` 后复制入 `artifacts/gate-m2b2-final/`。
- 评审期间并行存在其他 Gate/实现代理（共享真实 `%TEMP%`、`artifacts/` 与真实 Host）；真实 Host 于约 19:00 前关闭（并行评审期间，关闭者未确认），导致 M2a/M2b-1 自检无法在本轮实跑（已披露为受限验证）。
- 评审期间落库的 `e40cac4`（ADR D-026 / 状态口径 / 自检脚本）由并行流程编辑并提交，本报告仅**只读复核**（§0.1），未参与其编辑；本报告文件亦被 `e40cac4` 一并纳入提交，评审后的修订为工作树修改。
- `artifacts/gate-m2b2-final/` 为 gitignored 证据目录；如需入库请另行处理。

---

## 10. 复跑命令（本报告口径）

```powershell
# 0) 基线（只读）
git -C D:\Workspace\git_1 rev-parse HEAD          # 期望 e40cac4（功能对象 b967d73）
& "$env:TEMP\mbd-review-query\bin\Release\net11.0\query.exe"

# 1) 全量测试（隔离输出，避免运行中 Host 锁 bin）
dotnet test MbdGitTool.sln --artifacts-path "$env:TEMP\mbd-gate-artifacts" -v minimal
# 期望 Core 71 + Host 266 = 337/337，EXIT=0

# 2) 洁净构建 0 warning
dotnet build MbdGitTool.sln --artifacts-path "$env:TEMP\mbd-gate-build-a" --no-incremental -v minimal

# 3) 恢复安全黑盒探针（临时仓，%TEMP%，引用 Host 项目；输出见 artifacts/gate-m2b2-final/restore-probe.log）
#    源码：%TEMP%\gate-restore-probe\Program.cs；运行：
$exe = Get-ChildItem "$env:TEMP\gate-probe-artifacts" -Recurse -Filter probe.exe | Select-Object -First 1
& $exe.FullName

# 4) M2b-2 隔离 UI 自检（e40cac4 更新后脚本；输出 artifacts/m2b2-selfcheck/）
node ui/scripts/cdp-m2b2-selfcheck.mjs     # 期望 53/53 PASS
# 基线对照（旧脚本/旧断言，只读）：node "$env:TEMP\m2b2-selfcheck-gate.mjs"  # 58 项中 52 PASS、6 项过时 EMPTY 断言

# 5) 恢复后真实库零写入复核（只读）
python "$env:TEMP\gate-m2b2-query.py"      # count,maxid 应与测试前一致
```
