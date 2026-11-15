# M2b-2a 最终独立签收报告（对象 `e40cac4`）

> 角色：最终独立签收（`signoff-m2b2`，只读评审；不改代码/需求/真实仓库）。
> 对象：`e40cac4264cf9e291580601d02d3c8c0fd30c819`（`docs(m2b2a): ADR D-026 deferrals and corrected audit evidence wording`，2026-11-14 19:05:32 +0800，父 `b967d73`）。
> 结论：**建议打 `m2b-2a` 阶段 tag**（阶段 checkpoint，不代表 M2b/M2 完成）；B1/B2/B4 闭环、B3 隔离自检闭环，**B3 的真实 Host 证据项未由本对象闭环**（已由并行提交 `9db68f9` 的 Gate 报告书面归入 M2b-2b）。该 tag 已于评审期间（19:31:55）由并行流程创建，本报告为**事后独立签收**而非 tag 前置审批。

| 项目 | 内容 |
|---|---|
| 评审对象 | `e40cac4`（diff：`reqs/adr/D-026-m2b2a-deferrals.md` +37、`docs/m2b-status.md` +3/−3、`ui/scripts/cdp-m2b2-selfcheck.mjs` +12/−37，另纳入 3 份并行 Gate 报告；**`src/`、`tests/`、`ui/src/` 零改动**，`git diff --stat b967d73..e40cac4 -- src tests ui/src` 为空） |
| 评审时 HEAD（动态） | 19:22 起 `e40cac4`；19:31:55 并行流程提交 `9db68f9`（`docs(m2b2a): final gate pass report and stage status`，仅改 `docs/review-m2b2-gate.md` 131 行 + `docs/m2b-status.md` 1 行）并创建 annotated tag `m2b-2a`（`725f48a` → `9db68f9`，`9db68f9` 的父为 `e40cac4`，故 tag 树含本对象全部功能与文档状态）。评审结束时 HEAD=`9db68f9` |
| 输入 | `docs/review-m2b2-gate.md`（初版判“不打”；19:31 修订为判“建议打”）、`docs/review-m2b2-code.md`/`review-m2b2-ux.md`、`reqs/adr/D-026-m2b2a-deferrals.md`、`docs/m2b-status.md`、SRS/AC（FR-OP-05/09/11、FR-SAFE-01、AC-OP-30）、HEAD 源码/tests/UI |
| 环境 | Windows 11 / .NET SDK 10.0.404 / git 2.59 / 工作区 `D:\Workspace\git_1`；真实数据 `%LOCALAPPDATA%\MbdGitTool`；**真实 Host 未运行**（评审结束前复核：`MbdGitTool*` 进程 0、`127.0.0.1:9222` 连接拒绝）；评审期间并行代理推进了 HEAD/tag |
| 方法 | ① `git show` 核对对象 diff、tag 指向与工作树；② 独立重跑隔离 `dotnet test` 与洁净构建（隔离 `--artifacts-path`，不写仓库 bin）；③ 独立重跑 `ui/scripts/cdp-m2b2-selfcheck.mjs`（脚本自带静态服务 + headless Edge + 注入测试桥，不触真实 Host/仓库）；④ 独立重跑离线“无误报”探针（基于并行 Gate 的 `%TEMP%` 注入桥副本，当前 `ui/dist`）；⑤ 只读查询真实 `operation_log`/JSONL/exports 并比对测试前后；⑥ 核对 ADR/Gate 报告/自检脚本/源码闭环与残留 |
| 边界 | **未修改**任何源码/配置/`reqs`/UI 源码/真实仓库/真实数据；新增仅本报告 + gitignored `artifacts/signoff-m2b2a/`；重跑自检覆盖了 gitignored `artifacts/m2b2-selfcheck/`（作者结果已另存 `artifacts/signoff-m2b2a/author-selfcheck-result.json`）；未启动/未驱动真实 Host，未创建/移动任何 tag |
| 证据 | `artifacts/signoff-m2b2a/`：`dotnet-test.log`、`build.log`、`build-release.log`、`selfcheck-rerun.log`、`selfcheck-rerun-result.json`、`author-selfcheck-result.json`、`selfcheck-local-noerror-rerun.log`、`db-evidence.txt`、`host-9222-probe.txt`；并行 Gate 证据 `artifacts/gate-m2b2-final/m2b2-selfcheck-updated-*.log/json`（19:23，53/53） |

---

## 0. 结论（判决）

> **建议打 `m2b-2a` 阶段 tag（仅阶段）。** 该 tag 已于 2026-11-14 19:31:55 由并行流程创建。
>
> **支持的证据**：B1/B2 由 ADR D-026 书面延后（Accepted，仅限 M2b-2a 阶段 tag，M2b-2b 必须闭合，P0 安全要求未降低）；B3 的 M2b-2 隔离自检已随 `e40cac4` 更新，本评审独立重跑 **53/53 PASS、0 console error**；B4 的“521 行真实”证据口径已更正；全量测试 **337/337（Core 71 + Host 266）**、Debug/Release 洁净构建 **0 warning / 0 error**；测试前后真实库 **528→528 行、JSONL 336→336、exports 11→11（0 增量）**；P0（tracked dirty / untracked / ignored 阻断、安全 ref、EMPTY unborn 禁用）在 `e40cac4` 代码与全量测试中仍闭环。
>
> **必须随 tag 记录的约束**：
> 1. 仅标 **M2b-2a 阶段**；**P1-2（备份 ref 分支身份/恢复校验）与 P1-4（审计写失败降级/回填）已知未闭合**，按 ADR D-026 必须在 M2b-2b 完成并复测全绿，不得带入 M2b/M2 完成声明。
> 2. **M2b-2b 未完成、M2b 与 M2 均不完整**，任何“M2b 完成/M2 交付”声明不成立。
> 3. **真实 Host 自检（`ui/scripts/cdp-m2b2-realhost-check.mjs`）未更新、未以新构建重跑**：本对象只完成了隔离自检更新；该证据项由并行提交 `9db68f9` 在 Gate 报告 §7.1/§8 中归入 M2b-2b 并声明不阻断阶段 tag，但 **ADR D-026 与现有 tag 说明均未记载该延后**，必须补记到 tag/发布说明（否则该缺口在审计责任上只落在一份被评审修改过的 Gate 报告里）。
> 4. 补记项还应含：`refs/backup/restore/*` 可见/可恢复/保留、历史审计污染清理/重建、M2b-2b 重跑 Gate。
>
> **诚实披露（过程）**：本评审开始（19:22）时 HEAD=`e40cac4`、无 `m2b-2a` tag；评审期间并行流程于 19:31 提交 `9db68f9`（把同一份 Gate 报告从“不打”修订为“建议打”）并打 tag。因此本报告无法声称“tag 是在本独立签收之后才打的”，只能给出**基于 `e40cac4` 证据 + 并行复核的独立事后签收意见**。
>
> **若按 Gate 初版原文（B3 含“两份脚本 + 真实 Host 重跑”）严格判定**：B3 的真实 Host 项未闭环，阶段 tag 依赖 9db68f9 对 B3 范围的书面修订；不认可该修订即应判“不打”。

| 判据 | 判定 | 一句话证据 |
|---|---|---|
| B1 code P1-2（分支身份） | **条件式转绿（ADR D-026 Accepted）** | ADR 明确仅阶段 tag、M2b-2b 必须完成（`<ts>/<branch>/<TYPE>` 迁移/列表/恢复校验）；代码仍为 `<ts>/<TYPE>`（P1-2 确未修，ADR 属真实延后） |
| B2 code P1-4（审计回填） | **条件式转绿（ADR D-026 Accepted）** | `AuditDatabase.Insert` 返回 false 仍被忽略、无 outbox/JSONL→DB 回填（确未修）；ADR §2 补偿条件（JSONL 始终写、不伪装成功）+ M2b-2b 必做项 |
| B3 自检/证据 | **隔离自检闭环；真实 Host 证据项未闭环（延后）** | 本评审独立重跑 `cdp-m2b2-selfcheck.mjs` **53/53、0 console error**；`realhost-check.mjs` 自 `1371f51` 未更新、仍断言旧 EMPTY/离线（`:147,:168`），最新真实 Host 证据 17:54 25/25 早于 `b967d73` |
| B4 审计证据口径 | **口径闭环（残留小项）** | `docs/m2b-status.md:54-55` 已更正“521 行含预隔离测试污染、非生产证据”；未写真实 528 行/来源细分（Gate 原文要求），且第 62 行仍有字面 `\n` 渲染缺陷 |
| P0 闭环 | **PASS** | 恢复 tracked/untracked/ignored 全阻断（`BackupRefService.cs:169-213`）、安全 ref 毫秒+GUID（`:215-245`）、EMPTY unborn 禁用 PUSH+`E_REF_NOT_FOUND`（`workbench.ts:530-539`）；对应测试在 337 全量中通过 |
| 全量测试/构建 | **PASS** | 337/337、0 failed/0 skipped、EXIT=0；Debug/Release `--no-incremental` 均 0 warning/0 error |
| 真实库隔离 | **PASS** | 测试+自检前后 528→528、max(id)/max(ts) 不变、JSONL 336→336、exports 11→11 |
| M2a/M2b-1 不回退 | **受限验证（未实跑真实 Host）** | 历史绿档 11/11（17:09）、68/68（17:08）；`e40cac4` 未改 UI/源码/测试；隔离自检回归抽样 `m2a.tooltip-format`、`no-upstream.confirm-bar`、`m2b1.task-center-empty`、`page.no-console-errors` 全 PASS |
| **总判决** | **建议打阶段 tag（已创建，附约束）** | 见 §0/§9；M2b-2b 未完成、M2b/M2 不完整 |

---

## 1. 对象与基线核对

- `git rev-parse HEAD`（评审开始）`=e40cac4264cf9e291580601d02d3c8c0fd30c819`；`git status` 干净（评审中并行代理短暂修改 `docs/review-m2b2-gate.md`，随后由 `9db68f9` 提交）。
- `git show --stat e40cac4`：`docs/m2b-status.md`（+3/−3）、`reqs/adr/D-026-m2b2a-deferrals.md`（新增 37 行）、`ui/scripts/cdp-m2b2-selfcheck.mjs`（+12/−37）、另纳入 `docs/review-m2b2-gate{,-code,-ux}.md` 三份并行 Gate 报告；共 6 文件 +792/−40。
- **功能零改动**：`git diff --stat b967d73..e40cac4 -- src tests ui/src` 为空；`git diff --stat e40cac4..9db68f9 -- src tests ui/src` 亦为空 → 上一轮功能评审（P0 闭环、337/337）对本对象仍成立。
- 并行提交 `9db68f9`（19:31:55）仅改两份文档（`+77/−56`）；annotated tag `m2b-2a`（`725f48a`）指向 `9db68f9`（其父 `e40cac4`），tag 说明仅写“P1-2/P1-4 deferred to M2b-2b per ADR D-026”，未写真实 Host 证据延后与 M2b-2b 清单（Gate 报告 §7.2 自己要求写入 tag 说明）——见 §8 补记项。
- `git tag -l`（评审结束）：`m0-baseline m1-baseline m2a m2b-1 m2b-2a ui-v3`；`m2b-2a` 于 19:31:55 创建。

## 2. B1/B2：ADR D-026 审查

| ADR 要求 | 核对结果 | 判定 |
|---|---|---|
| 明确性 | `reqs/adr/D-026-m2b2a-deferrals.md` 编号/日期/状态/关联/决策齐全；状态 **Accepted（仅用于 M2b-2a 阶段 tag）**，声明“不改变 M2b-2b/M2 出口要求” | PASS |
| 仅限 M2b-2a 阶段 tag | §2.3“M2b-2a tag 只标注 M2b-2a；M2b-2b 完成前不得发布 M2b/M2 完成声明”；§4 “tag 名 `m2b-2a`” | PASS |
| M2b-2b 必须完成 | §3 列明：ref 命名 `<ts>/<branch>/<TYPE>` + 旧 ref 迁移/兼容 + `getBackupRefs` 返回 branch/kind + restore 校验来源分支（不一致阻断/二次确认）；审计 outbox/回填 + 查询合并 + 历史污染清理/重建入口；安全 ref 可见/可恢复/保留；完成后重跑 M2b-2 Gate 才可打 `m2b-2b` | PASS |
| 未降低 P0 安全要求 | §2 补偿条件：恢复前 clean 门禁（tracked/untracked/ignored 全阻断）、安全 ref（毫秒+GUID 唯一）、JSONL 始终写、UI/API 不伪装成功；ADR 未改写任何 P0 策略，代码亦未在 `e40cac4` 变动 | PASS |
| 延后是真实（非“假修复”） | P1-2：`BatchOperationService.cs:210-217` 仍建 `refs/backup/sync/<stamp>/<TYPE>`，`BackupRefService` 解析仍严格两段；P1-4：`AuditDatabase.Insert` 失败仅 `HostLog.Warn` 返回 false（`:272-276`），`OperationAuditService.cs:233` 忽略返回值，全仓无 outbox/回填 | PASS（与 ADR 描述一致） |

**P0 复核（`e40cac4` 时点）**：`BackupRefService.cs:169-190` tracked dirty→`E_DIRTY_WORKTREE`；`:192-213` untracked/ignored→`E_WORKTREE_OVERWRITE`（发生在安全 ref/reset 之前）；`:215-245` 安全 ref `refs/backup/restore/<毫秒+GUID>/<TYPE>` 先 `update-ref` 再 `reset --hard`；`ui/src/stores/workbench.ts:530-539` 对 `EMPTY && !headSha` 禁用 PUSH 并给 `E_REF_NOT_FOUND`。对应用例 `M2b2BackupRefTests.Restore_DirtyTrackedWorktree_BlockedWithoutChangingAnything(:118)`、`Restore_UntrackedFile_IsBlockedAndPreserved(:137)`、`Restore_MovesHeadBackToBackupRef_AndSavesSafetyRef_Retryable(:55)`、`M2b2EmptyPushTests.EmptyPush_UnbornHead_FailsWithReadableCodeAndNoRemoteWrite(:71)` 均在 337 全量中通过。

## 3. B3：自检脚本与运行证据

### 3.1 `cdp-m2b2-selfcheck.mjs`（本评审独立重跑）

- 本对象更新：EMPTY 段由“PUSH 启用 + 确认条”7 项旧断言改为 3 项新断言（`empty.unborn-push-disabled`、`empty.unborn-tooltip-e-ref-not-found`、`empty.unborn-no-confirm-bar`，脚本 `:539-551`），全脚本共 **53** 项。
- 独立重跑（2026-11-14 19:26，当前 `ui/dist`=b967d73 构建，脚本自带 headless Edge+注入桥，ports 5287/9642）：
  - **53/53 PASS，failed=[]，EXIT=0，耗时 29.1s**（`artifacts/signoff-m2b2a/selfcheck-rerun.log` / `selfcheck-rerun-result.json`）；
  - `empty.unborn-push-disabled` detail：`{"disabled":true,"enabled":"false","title":"空仓无提交：请先 COMMIT… [E_REF_NOT_FOUND] → …"}`；`empty.unborn-no-confirm-bar` PASS（零写）；
  - **`page.no-console-errors` PASS（0 console error / 0 未捕获异常）**；
  - M2a/M2b-1 回归抽样 PASS：`m2a.tooltip-format`、`no-upstream.confirm-bar`（含 `setUpstream=true`）、`m2b1.task-center-empty`；
  - 并行 Gate 于 19:23 的独立重跑亦为 53/53（`artifacts/gate-m2b2-final/m2b2-selfcheck-updated-result.json`），与本评审一致。
- **性质说明**：该脚本是**隔离 Mock 自检**（`ui/scripts/cdp-m2b2-selfcheck.mjs:1-11`），不连接真实 Host/CDP 9222；本评审开始时真实 Host 已不在运行（见 §3.3），因此“真实 Host 下 53/53”实际指**隔离桥下 53/53**，不能表述为真实 Host E2E。

### 3.2 离线“无误报”

- 仓库内 `cdp-m2b2-selfcheck.mjs` 未新增“本地无显式错误=在线”断言（其夹具仍带显式远程错误，只覆盖“有错才离线”方向）。
- 本评审独立重跑并行 Gate 的 `%TEMP%` 注入桥副本（`snapshot.error=null` + `remoteStale=true`，当前 `ui/dist`）：`local-no-error.no-false-offline` PASS、`local-no-error.fetch-not-disabled` PASS、`local-no-error.statusbar-online` PASS、`page.no-console-errors` PASS（`artifacts/signoff-m2b2a/selfcheck-local-noerror-rerun.log`；该副本中 4 项 `offline.*` 与 6 项旧 EMPTY 断言按设计失败，恰好证明夹具已改为“无错”）。
- 判定：**功能 PASS（双向）**；但“离线无误报”尚未进入仓内自检脚本，属证据/脚本缺口，非功能缺陷。

### 3.3 `cdp-m2b2-realhost-check.mjs`（真实 Host）——未闭环

- 该文件自 `1371f51`（17:53）后**未再修改**（`git log -- ui/scripts/cdp-m2b2-realhost-check.mjs` 仅 1 条），仍断言 `real.empty-push-enabled`（`:168`）与 `real.offline-banner`（`:147`），与 `b967d73` 的“unborn 禁用 PUSH / 仅显式错误才离线”修复相反。
- 最新真实 Host 证据：`artifacts/m2b2-realhost/result.json` **25/25，17:54:09**（早于 `b967d73` 18:52，非“本提交新构建”）。
- 评审时真实 Host 不可用：`MbdGitTool*` 进程 0；`http://127.0.0.1:9222/json/version` → `ECONNREFUSED`（`artifacts/signoff-m2b2a/host-9222-probe.txt`）。按只读边界**未启动新 Host**，故无法实跑；且即使有 Host，旧脚本断言也已过时，必须先更新脚本再重跑。
- 判定：**B3 的“真实 Host 重跑入库”未完成**；并行提交 `9db68f9` 的 Gate 报告 §7.1/§8 已将其书面归入 M2b-2b 并声明“不阻断阶段 tag”，但 ADR D-026 与 tag 说明均未记载（见 §0 约束 3、§8）。

### 3.4 M2a / M2b-1 selfcheck 不回退

- `cdp-m2a-selfcheck.mjs` **11/11**（`artifacts/m2b1-gate-ux2/selfcheck-m2a.log`，17:09）；`cdp-m2b1-fix-selfcheck.mjs` **68/68**（`artifacts/m2b1-fix-selfcheck/result.json`，17:08）——历史绿档。
- 本轮**无法实跑**：两脚本均硬编码连接 `127.0.0.1:9222`（M2a 还需 `chrome.webview` 真实桥），Host 未运行；只读评审未启动 Host。
- 替代证据：① `e40cac4` 对 `ui/src`、源码、测试零改动（§1）；② 隔离自检回归抽样（§3.1）全 PASS；③ 并行 Gate 亦以“历史绿档 + 隔离抽样 + diff 范围”判定无回退。
- 判定：**无回退证据，属受限验证**（与上一轮 Gate §5.3 同级限制），已在 tag 约束中披露。

## 4. B4：`docs/m2b-status.md` 证据口径

- `:54` 已改为“**更正：早期证据中的‘521 行真实’包含预隔离测试行，不作为生产数据证据；测试隔离修复后不再新增真实库行。**”；`:55` 改为“导出实测可用（历史行含测试污染，口径已更正）”。不再以“521 行真实可查/真实 521 行导出成功”作为验收证据 → **口径更正闭环**。
- 本评审实测真实库 `count=528, max(id)=528, max(ts)=2026-11-14T10:45:14.78Z`（按动作 `CLEANUP_RETENTION 107 / … / RESTORE_BACKUP 7`），JSONL 336、exports 11；测试与自检期间 **0 增量**（§6）。
- 残留（非阻断）：
  1. 未按 Gate B4 原文写出**真实行数与来源细分**（528 行中 fixture/test-like 370、历史 `CLEANUP_RETENTION` 107、真实 Host ≈7）及处置方式（保留并标注/清理）——仅声明“含预隔离测试行”，信息密度不足；
  2. 第 33、62 行存在字面 `\n`（`## 3. …\n\n| 评审 |`、`…（分支身份、审计回填）。\n> 待 M2b-2b…`），Markdown 渲染为一行，且第 62 行为 `e40cac4` 引入；属文档格式缺陷，建议随 M2b-2b 修；
  3. `docs/images/m2b/audit-log.png` 仍为污染期截图（引用需注明，既有 P2）。

## 5. 全量测试与构建（本评审独立实跑）

```
dotnet test MbdGitTool.sln --artifacts-path %TEMP%\mbd-signoff-artifacts -v minimal
# Core.Tests ：失败 0，通过 71，跳过 0，总计 71
# Host.Tests ：失败 0，通过 266，跳过 0，总计 266（1m47s）
# EXIT=0  => 337/337
dotnet build MbdGitTool.sln --artifacts-path %TEMP%\mbd-signoff-build    --no-incremental -v minimal  # Debug
dotnet build MbdGitTool.sln -c Release --artifacts-path %TEMP%\mbd-signoff-build-rel --no-incremental -v minimal
# 两者均：已成功生成。0 个警告 / 0 个错误（TreatWarningsAsErrors=true）
```

日志：`artifacts/signoff-m2b2a/{dotnet-test.log,build.log,build-release.log}`。说明：使用隔离 `--artifacts-path`，未写仓库 `bin/obj`，不受并行进程锁 DLL 影响。

## 6. 真实库测试隔离（0 增量）

机制：`tests/MbdGitTool.Host.Tests/TestInfrastructure.cs:332-349` `TestAudit.NewConfig` 为每次测试注入 `%TEMP%\MbdGitToolTests\{audit,db,exports,jobs}\<guid>`（`AuditRoot/DatabasePath/ExportsRoot/JobsRoot`）；本评审的隔离自检/探针均使用注入桥，不写真实库/JSONL/exports。

| 指标 | 测试前（19:23） | 测试+自检后（19:26） | 差 |
|---|---|---|---|
| 真实库 `operation_log` 行数 / max(id) | 528 / 528 | **528 / 528** | **0** |
| 真实库 `max(ts)` | 10:45:14.78Z | 10:45:14.78Z | 不变 |
| 真实 `audit/operations.jsonl` 行数 | 336 | **336** | **0** |
| 真实 `exports/` 文件数 | 11 | **11** | **0** |

证据：`artifacts/signoff-m2b2a/db-evidence.txt`。旁注（既有 P2-11）：`HostLog` 未隔离，本轮全量测试仍写真实 `logs/host.log`（7,313,707 B→7,481,421 B，≈+167 KB）；审计库/JSONL/exports 不受影响（0 增量），但该测试副作用应随 M2b-2b 收口。

## 7. 与 Gate 初版“转绿条件”逐项对照

| Gate 初版转绿条件（`e40cac4` 前） | `e40cac4` 处置 | 本评审判定 |
|---|---|---|
| B1 P1-2 修复或 ADR 书面延后 | ADR D-026 Accepted，仅阶段 tag，M2b-2b 必须完成 | **满足（书面延后）** |
| B2 P1-4 修复或 ADR 书面延后 | 同 ADR D-026（补偿条件 + M2b-2b outbox/回填） | **满足（书面延后）** |
| B3 更新 `cdp-m2b2-selfcheck.mjs`（EMPTY 禁用 + `E_REF_NOT_FOUND`） | 已改 3 项新断言，独立重跑 53/53、0 console error | **满足（隔离自检）** |
| B3 更新 `cdp-m2b2-realhost-check.mjs`（本地无错=在线、EMPTY 禁用）+ 新构建真实 Host 重跑入库 | **未做**（脚本自 17:53 未变；无新构建真实 Host 证据） | **不满足**（由 9db68f9 Gate 报告延后 M2b-2b 并声明不阻断阶段 tag；ADR/tag 说明未记载） |
| B4 更正 `m2b-status.md:54-55` 证据口径 | “521 行含预隔离测试污染、非生产证据” | **满足（口径）；残留见 §4** |
| M2b-2b 未完成、M2b/M2 不完整声明 | ADR §2.3/§4、status、Gate §8 均有 | **满足**（tag 说明亦须写） |

## 8. 残留与必须补记项（不阻断阶段 tag，但须记录）

1. **真实 Host 自检证据**：更新 `cdp-m2b2-realhost-check.mjs`（EMPTY unborn 禁用 + `E_REF_NOT_FOUND`、本地无错=在线、真实恢复/审计）并以新构建真实 Host 重跑入库；最迟在 M2b-2b 完成，**建议在 tag/发布说明或 ADR 增补中明确写出**（当前仅 Gate 报告 §7.1/§8 有）。
2. **tag 说明不完整**：`m2b-2a` 注释仅写 P1-2/P1-4 延后，未写“真实 Host 证据延后 M2b-2b、M2b-2b 未完成、M2b/M2 不完整、安全 ref 可见性/审计污染清理归 M2b-2b”；Gate §7.2 自己要求写入 tag 说明。
3. B4 残留：status 未写真实 528 行/来源细分与处置；第 33/62 行字面 `\n`。
4. 既有 P2（不影响本阶段）：untracked/ignored 全量阻断无文件名/冲突预览（AC-OP-30③④ 不足）；`HostLog` 未隔离；审计表 `jobItemId/requestId` 未展示、hint 过度声明；`createdAt` 语义；离线本地扫描失败仍称“远程不可达”、PULL 未联动、mock/local-only 显示“在线”；`delete.retentionDays` 未实现。
5. `cbdfa78` 提交说明与 diff 不完全一致（过程项，Gate 已记录）：`branch … hardening` 无对应 diff；`ConfigureTestIsolation` 为无调用方死代码（实际隔离来自父提交 `TestAudit.NewConfig`）。

## 9. M2b-2b 未完成 / M2b / M2 不完整（tag 说明必写）

- **ADR D-026 延后项（必须完成）**：P1-2 备份 ref 分支身份（`refs/backup/sync/<ts>/<branch>/<TYPE>`、旧 ref 迁移、`getBackupRefs` branch/kind、restore 校验来源分支）+ P1-4 审计 outbox/JSONL→DB 回填、运行期降级/健康度、历史污染清理/重建入口。
- 写前 journal + 崩溃 reconcile（FR-SAFE-04 P0）：全仓无实现（仅 SQLite WAL）。
- 离线降级完整行为（分区七态、PULL 联动、恢复通知、远程数据时点区分）。
- 备份恢复完整语义：`createdAt`=ref 创建时间、冲突/状态预览、`refs/backup/restore/*` 可见/可恢复/保留策略、`.trash` 记录选择（AC-OP-30①）。
- 审计健壮性：运行期降级、失败补偿/对账、回填、导出与过滤对齐、CSV 注入/大文件治理。
- FR-OP-12 组件纳入/移出（AC-OP-31/32）；AC-OP-01…32 / AC-REPO / AC-SAFE 逐条证据 + 故障注入报告（无 `docs/m2b2-ac-report.md`）。
- 真实 Host CDP 自检（`cdp-m2b2-realhost-check.mjs`）以新构建重跑并入库。
- FR-REPO-05 其余修复类（Repair/Re-clone、detached 建分支）与 SRS §7.6/D-025 的 EMPTY 首推规格冲突裁决。
- 完成后重跑 M2b-2 Gate；M2b 总出口 = M2b-2a ∧ M2b-2b ∧ M2b-1。

> **M2b 不完整、M2 不完整**；本签收只支持“M2b-2a **阶段** tag”，不支持任何 M2b/M2 完成声明。

## 10. 边界与副作用披露（诚实记录）

- **未修改**任何源码、配置、`reqs`、UI 源码、真实仓库或真实用户数据；唯一源码树新增为本报告与 `artifacts/signoff-m2b2a/`（gitignored）。
- 本评审重跑 `cdp-m2b2-selfcheck.mjs` 覆盖了 gitignored `artifacts/m2b2-selfcheck/`（result.json + 截图）；作者原始结果已另存 `artifacts/signoff-m2b2a/author-selfcheck-result.json`（53/53，ports 5265/9437）。
- 全量测试/构建输出落 `%TEMP%` 的 `--artifacts-path`（不写仓库 `bin/obj`）；未杀任何进程（自检脚本仅清理自己以 `dsh-m2b2-edge` 前缀启动的 Edge）。
- 真实 `%LOCALAPPDATA%\MbdGitTool\db\mbd.db`、`audit/operations.jsonl`、`exports/`：**只读**；前后 **528→528、max(id)/max(ts) 不变、JSONL 336→336、exports 11→11**。未清理/未修改历史污染行。真实 `logs/host.log` 因 `HostLog` 未隔离被全量测试追加 ≈167 KB（既有 P2-11，见 §6）。
- 未启动/未驱动真实 Host；`127.0.0.1:9222` 未监听，故 `cdp-m2a-selfcheck.mjs` / `cdp-m2b1-fix-selfcheck.mjs` / `cdp-m2b2-realhost-check.mjs` 均无法实跑（已按受限验证披露）。
- **评审期间并行流程推进了仓库**：19:31:55 提交 `9db68f9`（改 Gate 报告结论为“建议打”、status 改为“通过（阶段 tag）”）并创建 annotated tag `m2b-2a`（→`9db68f9`）；该操作非本评审所为，本报告已将其作为对象外上下文核对。
- 本报告为事后独立签收，不能替代 tag 前置审批；如需正式流程，应以本报告 §0 约束补记 tag/发布说明。

## 11. 复跑命令（本报告口径）

```powershell
# 0) 基线
git -C D:\Workspace\git_1 rev-parse HEAD          # e40cac4（评审开始）/ 9db68f9（评审结束，含 m2b-2a 文档）
git -C D:\Workspace\git_1 tag -l | Select-String m2b-2a

# 1) 全量测试（隔离输出）
dotnet test MbdGitTool.sln --artifacts-path "$env:TEMP\mbd-signoff-artifacts" -v minimal
# 期望 Core 71 + Host 266 = 337/337，EXIT=0

# 2) 洁净构建 0 warning
dotnet build MbdGitTool.sln --artifacts-path "$env:TEMP\mbd-signoff-build" --no-incremental -v minimal
dotnet build MbdGitTool.sln -c Release --artifacts-path "$env:TEMP\mbd-signoff-build-rel" --no-incremental -v minimal

# 3) M2b-2 隔离自检（更新后脚本；输出 artifacts/m2b2-selfcheck/）
node ui/scripts/cdp-m2b2-selfcheck.mjs            # 期望 53/53、0 console error

# 4) 离线“无误报”探针（并行 Gate 的 %TEMP% 注入桥副本；当前 ui/dist）
node "$env:TEMP\m2b2-selfcheck-local.mjs"         # 期望 local-no-error.* 3 项 PASS

# 5) 真实库只读复核（本报告脚本见 artifacts/signoff-m2b2a/db-evidence.txt）
python - <<'PY'
import sqlite3, os
base=os.path.join(os.environ['LOCALAPPDATA'],'MbdGitTool')
con=sqlite3.connect('file:'+os.path.join(base,'db','mbd.db')+'?mode=ro', uri=True)
print(con.execute('select count(*), max(id), max(ts) from operation_log').fetchone())
PY
```
