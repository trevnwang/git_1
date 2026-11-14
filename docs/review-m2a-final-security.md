# M2a 最终安全 Gate 复评报告（提交 `9d7afd0`）

| 项目 | 内容 |
|---|---|
| 复评对象 | 提交 `9d7afd0`（`fix(m2): final gate fixes (read-through/CAS, EMPTY.PUSH, audit-lite, single-source matrix, M2b copy, status template)`，2026-11-14 15:08:24 +0800，含 N-1 修复、read-through、审计、EMPTY.PUSH、单源矩阵、265 测试） |
| 复评范围 | ① N-1（PULL 显式分支≠当前分支）；② AC-SAFE-15 read-through 写前重扫；③ AC-OP-33 审计-lite 字段/无 token/恰 1 行；④ 全量测试回归；⑤ 原 P0-1/P1-1/2/3/6 未回退；⑥ 新 P0/P1；UX Gate G-P0 中安全相关项（`review-m2-gate-ux.md` G-P0-1/G-P0-2） |
| 输入 | `docs/review-m2-gate-security.md`（上轮 6/6 + N-1）、`docs/m2a-ac-report.md`、`docs/review-m2-gate-ux.md`、`src/MbdGitTool.Host/Services/{RepoActionService,OperationAuditService,RemoteRefsService,AppConfig}.cs`、`src/MbdGitTool.Host/Bridge/HostCommandRouter.cs`、`tests/MbdGitTool.Host.Tests/*`、SRS v0.3.1-r2 §13.1.2（AC-SAFE-14…17、AC-OP-33）、ADR D-025 |
| 环境 | Windows 11 / git 2.56.1.windows.1 / dotnet SDK 10.0.404 / 仓库 `D:\Workspace\git_1`，`HEAD=9d7afd0`；评测时运行中 Host PID 34444（15:08:00 启动，`src\MbdGitTool.Host\bin\Debug\net10.0-windows\MbdGitTool.Host.exe`） |
| 方法 | ① `git show` 逐行代码/契约核对；② **`git archive 9d7afd0` 导出不可变树到 `%TEMP%` 后全量 `dotnet test`**；③ 独立服务级黑盒夹具（`%TEMP%\m2final-harness2`，引用 **9d7afd0 精确构建产物** 的 `MbdGitTool.Host.dll/Core.dll`，真实 git + 隔离审计目录 + `GIT_TRACE2_EVENT` 子进程 argv 审计，24 断言）；④ 真实 `%LOCALAPPDATA%\MbdGitTool\audit\operations.jsonl` 最近行核对；⑤ 夹具矩阵与 Host 代码逐格比对 |
| 边界 | **只新增本报告，未修改任何源码/配置/真实仓库**；未杀运行中 Host（用 `--artifacts-path` 隔离构建，规避 `bin` 文件锁）；未对真实 Gitea 发起任何写操作；全部夹具/构建在 `%TEMP%`；仅有的副作用见 §1.3 披露（被测提交的测试自身向真实审计文件追加了行） |
| 日期 | 2026-11-14 |

---

## 0. 结论（Gate 判决）

**N-1 已修复；CAS/ignored/ref/凭据作用域/stderr 摘要均未回退；全量 265/265 通过（0 跳过、0 警告）。但 AC-SAFE-15 的“写前 read-through 重扫”未实现，且 `docs/m2a-ac-report.md` 对此作了与代码不符的“✅”陈述；AC-OP-33 审计缺 `actor`/`request_id` 两个必需字段。M2a 出口 AC 不能全绿，Gate 不通过。**

> **Gate 结论：提交 `9d7afd0` 不建议打 `m2a` tag（不打）。**
> 阻断项（均须修，见 §7）：**R-1** AC-SAFE-15 read-through 未实现 + 验收报告不实；**R-2** AC-OP-33 缺 `actor`/`request_id`（且无 `request_id` 无法断言“恰 1 行/重启不重复”）；**R-3** Host 测试非 hermetic，会向真实审计文件写测试行（P1-8 回归，污染 AC-OP-33 证据）。
> N-1、AC-SAFE-14/16/17、原 P0/P1 不回退的结论不受上述影响；本轮未发现新 P0。

| 汇总口径 | 结果 |
|---|---|
| N-1（PULL 显式分支≠当前分支） | **PASS**（修复 + 测试通过 + 独立夹具 9/9 断言；测试 Given 偏弱，见 P2-1） |
| AC-SAFE-15 read-through（写前重扫 / `scan_at` 前进 / 矩阵基于动作前状态） | **FAIL**（`runRepoAction` 只 `EnsureSnapshotAsync` 不 refresh；行为实测 stale DIRTY 仍执行 PULL；报告不实） |
| AC-OP-33 审计-lite | **FAIL（部分）**：成功/失败各 1 行、无 token、JSONL 持久化 **PASS**；`actor`/`request_id` 缺失 **FAIL**；测试非 hermetic（R-3） |
| 全量测试 | **265/265 通过**（Core 71 + Host 194；0 失败/0 跳过）；`TreatWarningsAsErrors=true` 构建成功 ⇒ **0 警告** |
| 原 P0/P1 是否回退 | **无回退**：P0-1、P1-1、P1-2(CAS 部分)、P1-3、P1-6 均 PASS（§5） |
| 新 P0 | **0** |
| 新 P1（阻断出口） | **2**（R-1、R-2）＋**1 项测试/证据链 P1**（R-3） |
| 新 P2 | 4（P2-1…P2-4，§6） |
| 是否建议 M2a gate 通过 / 打 tag | **否（不打）**；修复 R-1/R-2/R-3 并产出干净提交后复评 |

---

## 1. 验证方法与环境说明（可复跑）

### 1.1 关键前提：评审基准是 commit 对象，不是当时工作区
首次全量测试后核查发现工作区被**并行进程**持续修改（非本评审）：`src/MbdGitTool.Host/Services/RepoActionService.cs` 与 `tests/MbdGitTool.Host.Tests/*` 共 8 个文件在 15:09:59–15:17 期间出现未提交改动（如给 `TestAudit` 测试 helper、`RunRepoActionParams?` 可空标注等），这些**不在 `9d7afd0` 内**。为保证结论只针对被评审提交，本报告：
1. 用 `git archive 9d7afd0` 把**提交对象内容**导出到 `%TEMP%\m2-9d7afd0-tree-*`（工作区零改动），在该不可变树上完成权威全量测试；
2. 独立黑盒夹具引用**该导出树构建的 DLL**（`%TEMP%\m2final-artifacts-exact-*\bin\MbdGitTool.Host\debug\`），不引用工作区/测试代码；
3. 工作区未提交改动仅作为“并行修复活动”的事实记录，不纳入本报告的通过/失败判定。

> 打 tag 约束：`m2a` 必须打在**干净工作区**的、含全部修复的提交上；若并行修复（审计测试隔离等）要进入 tag，须形成新提交并重新复评。

### 1.2 全量测试：隔离构建，不打断运行中 Host
运行中 Host 锁定了 `src\MbdGitTool.Host\bin\Debug\...\MbdGitTool.Core.dll`，直接 `dotnet test` 会 MSB3027/3021。为不中断并行 UI 复验，**未杀 Host**，改用 `--artifacts-path` 隔离构建/输出：

```powershell
# 在 9d7afd0 导出树中执行（工作区零改动）
dotnet test MbdGitTool.sln --artifacts-path "$env:TEMP\m2final-artifacts-exact-<guid>" -v minimal
```

关键输出（两轮：工作区当前态、9d7afd0 导出态结果一致）：

```text
已通过! - 失败: 0，通过: 71，已跳过: 0，总计: 71  - MbdGitTool.Core.Tests.dll (net10.0)
已通过! - 失败: 0，通过: 194，已跳过: 0，总计: 194 - MbdGitTool.Host.Tests.dll (net10.0)  (1 m 30 s)
```

即 **265/265**；日志无 `warning`/`MSB3027`/`MSB3021`，且 `Directory.Build.props` 设 `TreatWarningsAsErrors=true`，构建成功 ⇒ **0 警告**。Host 194 = 上轮 191 + 本提交新增 3（`OperationAuditServiceTests` 2 + `Pull_ExplicitBranchDifferentFromCurrent_IsRejectedWithoutChanges` 1），与“265 测试”口径一致。

### 1.3 独立黑盒夹具（不复用仓库测试代码）
`%TEMP%\m2final-harness2`：临时 bare remote + cloneA/cloneB，经**真实 `HostCommandRouter.DispatchAsync`** 驱动；审计目录注入 `%TEMP%`；`GIT_TRACE2_EVENT` 记录每次动作启动的全部 git 子进程 argv；结果 24 断言 **22 PASS / 2 FAIL**（FAIL 恰为 R-1、R-2 对应断言）。复跑：`cd %TEMP%\m2final-harness2; dotnet run --no-build`。

**副作用披露（诚实记录）**：本评审两次全量测试（工作区态 + 9d7afd0 导出态）期间，被测提交的 `HostCommandRouterTests`/`RepoActionSecurityTests` 因未注入 `AuditRoot`，向真实 `%LOCALAPPDATA%\MbdGitTool\audit\operations.jsonl` 追加了约 15 行测试夹具记录（`CF100A_Demo`/`MOCK_*`，时间 07:10–07:13Z，均为本评审测试运行产生；无 token）。这不是评审伪造，而是 R-3 的直接证据；未清理真实审计文件（避免二次篡改），建议修复测试隔离后由维护者按需清理。

---

## 2. N-1：PULL 显式 branch ≠ 当前分支 —— **PASS（已关闭）**

### 2.1 实现核对
`RepoActionService.PullAsync`（`RepoActionService.cs:482-529`）：先 `ReadCurrentBranchAsync`（`:1040-1057`），`arguments.Branch` 非空且与当前分支不相等 → `E_INVALID_ARGUMENT`，details 给 `field=branch`、`expected=当前分支`、`current=请求值`、`sdkCode=GIT_PULL_BRANCH_MISMATCH`；该分支在任何 `git fetch/merge` **之前**返回；detached/无法解析同样 `E_INVALID_ARGUMENT`。

### 2.2 测试
`tests/MbdGitTool.Host.Tests/RepoActionGitTests.cs:84-103` `Pull_ExplicitBranchDifferentFromCurrent_IsRejectedWithoutChanges`：断言 `ok=false`、`E_INVALID_ARGUMENT`、HEAD/分支零变化；随全量 265 通过。

### 2.3 独立黑盒实测（9d7afd0 精确产物；`feature` 真实存在且 `main` 为其祖先，复现上轮 N-1 的危险 Given）

| 断言 | 实测 |
|---|---|
| PULL `args.branch="feature"`（当前 `main`）被拒 | `ok=false code=E_INVALID_ARGUMENT` |
| details expected/current | `expected=main current=feature` |
| HEAD / 分支零变化 | `before=50a71d4b… after=50a71d4b…`，`branch=main` |
| 拒绝先于任何 git 网络命令 | `GIT_TRACE2_EVENT`：networkGitCount=**0**（无 fetch/push/pull/ls-remote） |
| 恰 1 行审计（失败样本） | 审计 +1；`action=PULL ok=false errorCode=E_INVALID_ARGUMENT preHead=postHead=当前 HEAD` |

**P2-1（非阻断）**：仓库内回归测试未按 AC-SAFE-17④ 的 Given 造出“真实远端 `feature` 分支（main 为其祖先）”，也未断言“argv 无 fetch”；它对旧实现仍能通过错误码（旧实现 `fetch origin feature` 失败为 `E_GIT_FAILED`）区分，但**不能独立证明“当前分支不会被 FF 到 origin/feature”与“无网络 git”**。本轮已用独立夹具补齐证据，建议把该测试 Given 强化为真实 feature 分支 + trace2/argv 断言。

**N-1 判定：PASS（AC-SAFE-17 的 PULL 子项关闭）。**

---

## 3. AC-SAFE-15：写前 read-through 重扫 —— **FAIL（阻断）**

AC-SAFE-15（`reqs/requirements.md:1138-1144`）Then② 明文要求：**动作前 read-through 重扫成立：`local_state.scan_at` 单调前进（T1>T0）且矩阵判定基于动作前状态（非 TTL 旧快照）；非 HEAD 变化（dirty/upstream/remote）同样在动作前被重算捕获并给出对应拒绝码，不得仅以动作内部 git 报错兜底**；并注明“实现偏差须在 `docs/m2a-ac-report.md` 显式记录，**不得据此放宽本 AC**”。

### 3.1 静态核对：`runRepoAction` 没有写前 refresh
- `HostCommandRouter.cs:248-271`：`case "runRepoAction"` 仅 `await EnsureSnapshotAsync(...)`（`:250`）→ `_actions.RunAsync(parsed, _snapshots.Index, ...)`（`:255-261`）。
- `EnsureSnapshotAsync`（`:383-389`）：`if (_snapshots.Current is null) await GetSnapshotAsync(...)` ——**快照非空即直接复用旧快照**。
- `SnapshotService.RefreshAsync`（`:92-104`）全仓唯一调用点是 `case "refresh"`（`HostCommandRouter.cs:225-230`）；`runRepoAction` 不调用。
- 只有“写成功后失效快照”（`HostCommandRouter.cs:263-268` → `SnapshotService.cs:64-68 Invalidate`），这是**动作后回写**，不是动作前重扫。
- UI 侧同样只在动作**后**刷新：`ui/src/stores/workbench.ts:1535-1541 refreshAfterAction`、`:1603`；`executeRepoAction`（`:1543-1571`）在 `api.runRepoAction` 前不 refresh。
- 快照契约只有 `Freshness.LocalScanAt`（`SnapshotService.cs:413`，等于 `GeneratedAt`），没有独立可单调前进的 `local_state.scan_at`。

### 3.2 行为实测（独立夹具，9d7afd0 精确产物）

| 步骤 | 实测 | 结论 |
|---|---|---|
| 扫描得到快照 T0 | `GeneratedAt=2026-11-14T07:15:54.1131801Z` | — |
| 外部进程改本地 tracked 文件（HEAD 不变）；另一独立 Host 扫描 | fresh 扫描状态=**DIRTY**（矩阵 `DIRTY.PULL=N`） | 动作前真实状态已变为 DIRTY |
| 动作前再 `getSnapshot` | `GeneratedAt` 与 T0 **完全相同**（未重扫，T1=T0） | read-through 不存在 |
| 对 stale 快照 `runRepoAction(PULL)` | **`ok=true`（执行成功）** | AC 期望拒绝，实测放行 |
| 该动作审计行 | `state="SYNCED"`（旧快照态），而动作前真实态=DIRTY | 矩阵/审计均基于旧状态 |
| 外部改 HEAD（非 dirty）+ stale 快照四动作 | PULL/CHKOUT/COMMIT/PUSH 全部 `E_CONCURRENT_CHANGE`，HEAD 零改动 | Then① PASS（CAS 仍生效） |

即：**Then①（HEAD CAS）PASS；Then②（read-through/scan_at/矩阵基于动作前状态）FAIL**，且“非 HEAD 变化由动作内部 git 报错兜底”的替代路径也不成立（本例动作直接成功而非“安全失败”）。

影响：在快照 TTL/未刷新窗口内，状态矩阵可被旧状态绕过（例：真实 `DIRTY` 时执行本应禁用的 PULL；`NO_UPSTREAM` 等状态同理）。由于 PULL 仍是 `fetch+merge --ff-only --no-overwrite-ignore`，未发现数据丢失路径，但这是 SRS 冻结的 M2a 出口 AC，且“审计/矩阵所记状态与动作前真实状态不一致”，不能以“无破坏”降级。

### 3.3 验收报告与提交信息陈述与代码不符（证据链问题）
- `docs/m2a-ac-report.md:21`：“AC-SAFE-15 … ✅ `runRepoAction` 先 `_snapshots.RefreshAsync`（写前重扫）”——**代码中不存在该调用**（见 §3.1）。
- 提交信息“write pre-read-through refresh before action”——同上不成立。
- 该“文档说通过但实测不过”与上轮 Gate 已指出过的模式相同，属 Gate 级证据完整性问题（R-1 的一部分）；AC-SAFE-15 未全绿，M2a 13 条出口 AC 不可能全绿。

**修复建议**：`runRepoAction` 在动作前 `await _snapshots.RefreshAsync(...)`（或等价“锁内重扫 + 原子替换 Index”），用**重扫后的状态**过矩阵并记录新的 `scan_at`；CAS 语义保留为“请求快照 HEAD vs 重扫 HEAD / 执行时 HEAD”；新增测试：stale 快照 + 外部 dirty/upstream 变化 → 相应矩阵拒绝码（不得动作执行）、`scan_at` 单调前进、拒绝时 argv 无网络命令；同时改正 `docs/m2a-ac-report.md`。

**AC-SAFE-15 判定：FAIL（阻断 M2a 出口）。**

---

## 4. AC-OP-33：审计-lite —— **FAIL（部分，字段缺失；其余 PASS）**

AC-OP-33（`reqs/requirements.md:1162-1168`）：每次动作**恰 1 行**；最小语义字段含 **`ts/actor/componentId/repoType/action/before_head/after_head/result/error_code/request_id`**；成功与失败都写；重启可查、同一 `request_id` 不产生 0/多行；不含 token。

### 4.1 实现与字段核对
- 落点：`%LOCALAPPDATA%\MbdGitTool\audit\operations.jsonl`（`OperationAuditService.DefaultFilePath`）；JSONL audit-lite。SRS 允许“以实现落点为准”，M2b 再做完整 `operation_log` 查询/导出/90 天保留 —— 落点形态可接受。
- 写入时机：`RepoActionService.RunAsync`（`RepoActionService.cs:166-208`）在成功/失败/异常后**恰回调一次** `onCompleted` → `HostCommandRouter.cs:255-261` 写审计；失败早退路径也回调。
- 字段实测（真实文件 + 夹具）：`ts,action,componentId,repoType,state,preHead,postHead,ok,errorCode,durationMs`，全部为单行合法 JSON。

| 必需字段 | 实际 | 判定 |
|---|---|---|
| `ts` | `ts` | ✅ |
| `actor` | **缺失** | ❌ |
| `componentId` / `repoType` / `action` | 同名 | ✅ |
| `before_head` / `after_head` | `preHead` / `postHead`（语义等价） | ✅ |
| `result` / `error_code` | `ok` / `errorCode`（语义等价） | ✅ |
| `request_id` | **缺失** | ❌ |

**R-2（P1，阻断）**：缺 `actor`、`request_id` → AC-OP-33 Then② 不满足；且没有 `request_id`，Then①“同一 request_id 不产生 0 行/多行”、Then④“重启后同一 request_id 不重复”**无法断言**。须补字段（M2a 单机可令 `actor` 为固定本地用户标识；`request_id` 每次动作生成/透传），并补“同一 request_id 恰 1 行”的路由层集成测试。

### 4.2 成功/失败都写、无 token、恰 1 行
- 真实文件（基线 53 行）中真实 Gitea 动作证据：
  - 失败：`{"action":"COMMIT","componentId":"RTCT_Demo","repoType":"DESIGN","state":"DIRTY","preHead":"037211dd…","postHead":"037211dd…","ok":false,"errorCode":"E_CONFIRM_REQUIRED"}`（06:49:19Z）
  - 成功：COMMIT `preHead=037211dd… → postHead=73cee5b… ok=true`（06:50:09Z）、PUSH `73cee5b… ok=true`（06:50:14Z）、FETCH `ok=true`（07:08:07Z）——与 `m2a-ac-report` 的 73cee5b 真实写证据一致。
  - 无 token/Authorization/base64 命中=0；53 行全部为合法 JSON。
- 夹具：单路由器 7 次 `runRepoAction`（成功/失败/矩阵拒绝/CAS/超时类）→ 审计文件**恰 7 行**，逐次 +1；token 哨兵 `ghp_CANARY_…` 在审计文件与响应 JSON 中命中=0（含 fail-closed 的 `E_GITEA_CONFIG_INVALID` 失败样本）。
- 单测：`OperationAuditServiceTests.cs` 2 例覆盖字段/失败码/目录创建/无 token；**但没有 Host 路由级“每动作恰 1 行/同 request_id 不重复”测试**（属 R-2 修复内容）。

### 4.3 R-3：审计测试非 hermetic，写真实用户审计文件（P1-8 回归）
- `9d7afd0` 的 Host 测试配置未注入 `AppConfig.AuditRoot`：`HostCommandRouterTests.cs:142`、`RepoActionSecurityTests.cs:395`、`:538`（`new AppConfig { WorkspaceRoot = … }`）；只要测试触发 `runRepoAction`，`OperationAuditService` 就落到真实 `%LOCALAPPDATA%\MbdGitTool\audit\operations.jsonl`。
- 实测：本评审两次全量测试使该真实文件 53 → 68 行（+15），新增行全部为测试夹具（`CF100A_Demo` 随机 SHA、`MOCK_CF1001_…` 的 PULL/RESET），时间与测试运行对齐；真实 Gitea 行未被覆盖。
- 影响：① 违反“测试 hermetic / 不改真实用户数据”的既有 P1-8 结论；② 污染 AC-OP-33 的“真实审计证据”文件（需靠时间/组件名区分，易被误用）；③ 每次 CI/本地跑测试都会增长真实审计。
- 备注：评测时**工作区并行改动**已在 `TestInfrastructure.cs` 增加 `TestAudit` helper 并改相关测试（未提交、不在 `9d7afd0`），说明团队已识别该问题；但该修复必须进入被 tag 的提交并重跑 265，且需清理/标注已产生的测试行。
- 建议：所有构造 `AppConfig` 的 Host 测试统一注入 `%TEMP%\…\audit\<guid>`；补一条“动作后真实 audit 目录零新增”的测试；`OperationAuditServiceTests` 之外补路由级审计集成测试。

**AC-OP-33 判定：FAIL（部分）**——成功/失败各 1 行、无 token、持久化 PASS；最小字段（actor/request_id）与“恰 1 行”可断言性 FAIL。

---

## 5. 原 P0/P1 未回退复核 —— **PASS（无回退）**

本提交对 `RepoActionService.cs` 的改动仅：N-1 分支一致校验、审计回调/字段、COMMIT/PUSH 冻结 `RequiresConfirm`、`EMPTY.PUSH` 禁用；凭据作用域/ignored/ref/CAS/摘要代码未被改写（`git diff 1afa014 9d7afd0` 逐段核对）。全量 265 通过 + 独立夹具复验：

| 原项 | 结论 | 本轮证据 |
|---|---|---|
| P0-1 凭据作用域 fail-closed/URL 作用域 header | **PASS（未回退）** | `RepoActionService.cs:795-935` 未改；9 个 `Token_*` 测试（`Token_FetchUrlOutsideOrg…`、`Token_PushUrlMismatch…`、`Token_MismatchedOriginScope…`、`Token_ScopedUrlHeader_IsSentOnlyToMatchingCanonicalBase` 等）随 265 通过；夹具 token 哨兵不进审计/响应 |
| P1-1 ignored 文件不被覆盖 | **PASS（未回退）** | `checkout --no-overwrite-ignore`（`:655,672`）、`merge --ff-only --no-overwrite-ignore`（`:548`）未改；`Checkout_IgnoredFileWouldBeOverwritten…`、`Pull_IgnoredFileWouldBeOverwritten…` 通过 |
| P1-2 HEAD CAS（E_CONCURRENT_CHANGE、零改动） | **PASS（未回退）** | `CheckSnapshotHead`（`:1059-1079`）未改；夹具外部改 HEAD 后四动作全 `E_CONCURRENT_CHANGE`、HEAD 零改动。**注意**：其“read-through”子项属 AC-SAFE-15②，仍 FAIL（§3） |
| P1-3 ref 校验（不存在 ref 不假成功） | **PASS（未回退）** | `rev-parse --verify`（`:644-669`）、分支名 `-`/`+` 拒绝（`:1502-1538`）未改；`Checkout_NonExistentBranch/Tag…` 通过 |
| P1-6 stderr 摘要（关键行而非首行） | **PASS（未回退）** | `SummarizeGitError`（`:1217-1249`）、`IsPushRejected`（`:1205-1212`）未改；`Push_Rejected_ReturnsPushRejectedWithKeyStderrLine`、`PushRejected_SummaryContainsRejectedReasonNotOnlyFirstLine` 通过 |
| 新增防御：COMMIT/PUSH 确认冻结、EMPTY.PUSH 禁用 | **PASS** | `RepoActionService.cs:74`（`EMPTY="NYCYN"`）、`:119-123`（`RequiresConfirm = cell=='C' ∨ COMMIT/PUSH`）、`:287-300`（未 `confirm=true` → `E_CONFIRM_REQUIRED`）；`CommitAndPush_WithoutConfirm_AreRejectedByFrozenConfirmFlag`、`GetAvailability_Synced_CommitAndPushRequireConfirm…` 通过。**这补上了“绕过 UI 直接调桥写”的防御层**（G-P0-1 安全相关部分） |
| 单源矩阵夹具 ↔ Host 代码 | 代码/夹具逐格一致（14 态×5 键，`enabled/requiresConfirm` 比对 mismatches=0）；但见 P2-3 | `reqs/v0.2/fixtures/m2-matrix.json` 与 `RepoActionService.Matrix` 比对脚本 |

---

## 6. 新发现问题（按严重度）

### R-1【P1，阻断 AC-SAFE-15 / 证据不实】写前 read-through 未实现，验收报告称已实现
- 位置：`HostCommandRouter.cs:248-261`（仅 `EnsureSnapshotAsync`）、`:383-389`、`SnapshotService.cs:92-104`；`docs/m2a-ac-report.md:21`。
- 实测：§3.2；stale 快照 + 外部 DIRTY → PULL `ok=true`；动作前 `GeneratedAt` 不变；审计记录旧 `SYNCED` 态。
- 性质：M2a 出口 AC 未绿 + “文档说通过但实测不过”的证据完整性问题；SRS 明言不得以“报告记录偏差”放宽本 AC。
- 修复：动作前强制 `RefreshAsync` 并用新状态过矩阵；保留 CAS；补 `scan_at` 单调测试与“非 HEAD 变化矩阵拒绝”测试；改正报告。

### R-2【P1，阻断 AC-OP-33】审计缺 `actor`/`request_id`，且无“同一 request_id 恰 1 行”验证
- 位置：`OperationAuditService.cs:79-104`（序列化字段表）、`AppConfig.cs:32-37`。
- 实测：§4.1/§4.2。
- 修复：补两字段（`actor` 可为单机本地标识；`request_id` 每动作唯一并贯穿结果/错误 details 可选）；补路由级成功/失败/异常恰 1 行、同 request_id 不重复测试；重启后仍可查已有 JSONL 持久化。

### R-3【P1，测试/证据链】Host 测试非 hermetic：向真实用户审计文件写测试行（P1-8 回归）
- 位置：`tests/MbdGitTool.Host.Tests/HostCommandRouterTests.cs:142`、`RepoActionSecurityTests.cs:395,538` 等未注入 `AuditRoot`。
- 实测：§4.3（53→68 行，+15 行测试夹具记录，无 token）。
- 修复：测试统一注入临时 `AuditRoot`（工作区已有未提交的 `TestAudit` helper 可复用，但必须提交）；CI 断言真实审计目录零新增；清理/标注污染行。

### P2-1【P2】N-1 回归测试 Given 偏弱
`RepoActionGitTests.cs:84-103` 未创建真实 `feature` 分支、未断言拒绝前无 fetch/argv；建议按 AC-SAFE-17④ 的 Given/Then 强化（本轮独立夹具已覆盖行为）。

### P2-2【P2】（安全相关旁证）AC-UI-08 单源矩阵的“Host 单测与 CDP 共用夹具”未落实、夹具状态自相矛盾
- `tests/MbdGitTool.Host.Tests/RepoActionAndComponentDetailTests.cs:19-36` 仍手写 `ExpectedMatrix`（第三份期望）；`reqs/v0.2/fixtures/m2-matrix.json` 仅被 `ui/scripts/cdp-m2a-selfcheck.mjs` 读取。
- 夹具 `status` 仍为 `SINGLE_SOURCE_PENDING_CODE_ALIGNMENT`，`freezeNotes` 仍写“截至 1afa014 Host 仍 requiresConfirm=false、UI 仍持第二套矩阵、EMPTY.PUSH 仍启用；对齐完成并复跑后才可改状态”——与本提交实际状态矛盾（`reqs/v0.2/fixtures/m2-matrix.json:16` 与 freezeNotes）。
- 归 UX Gate 最终裁决；本报告仅记录安全相关部分（Host `RequiresConfirm` 已正确，防绕过确认的 Host 侧门禁已生效）。

### P2-3【P2】验收报告/提交信息的小偏差
`docs/m2a-ac-report.md:3` 写“候选代码：工作区最新（待提交）”（实际已有提交）；`:24` 称“真实动作证据见文件末尾两行”（真实文件尾部是 FETCH，真实 COMMIT/PUSH 在其前 2–3 行，且随后会被测试行追加）；`:32` 的 265 已由本轮独立复跑证实（这部分属实）。

### P2-4【P2/P3】审计记录“非法 action”行与细节
`RunAsync` 在 action 校验前初始化审计，因此 `action="RESET"` 等非法调用也会落一行 `action=RESET, state=UNKNOWN`（真实文件可见）。属“每次 runRepoAction 调用一行”的实现取舍，无安全影响；M2b 做完整审计表时建议统一 action 枚举/标记 invalid。

---

## 7. Gate 结论、必须修项与 tag 指引

### 7.1 结论
**M2a Gate 不通过；提交 `9d7afd0` 不建议打 `m2a` tag。**
- `AC-SAFE-15`（read-through ②）未绿且报告不实（R-1）；`AC-OP-33` 字段不全（R-2）；测试非 hermetic 污染真实审计（R-3）。
- M2a 出口要求“13 条 AC 全绿 + 每条可复跑证据”（`reqs/requirements.md:1218`，ADR §5 三件证据缺一不可），当前不满足；按 D-025，**不得**打 `m2-ops` 或宣称“M2 交付完成”。
- 本报告不覆盖 AC-UI-08/09/10/10a/10b/11/13/15 的 UI/CDP 行为面（由并行 UX Gate 负责），但 §6 P2-2 的矩阵单源问题会同时影响 AC-UI-08 证据。

### 7.2 必须修项（按优先级）
1. **R-1**：实现 `runRepoAction` 写前 read-through（refresh 快照 + 新状态过矩阵 + `scan_at` 单调 + 保留 CAS）；补“stale 快照 + 非 HEAD 变化 → 矩阵拒绝、拒绝不启网络 git、scan_at 前进”的 Host 测试；改正 `docs/m2a-ac-report.md` 与提交信息中的不实陈述。
2. **R-2**：审计补 `actor`/`request_id`；补“每动作恰 1 行（成功/失败/异常）、同一 request_id 不重复、重启可查”的路由级测试；复核字段可读性（中文/UTF-8）。
3. **R-3**：所有 Host 测试注入临时 `AuditRoot`；CI/本地跑测试不得改动真实 `%LOCALAPPDATA%\MbdGitTool\audit`；清理或标注已产生的测试行。
4. 建议同时处理 **P2-1**（N-1 测试 Given）、**P2-2**（Host 单测读取 `m2-matrix.json`、更新夹具 status）、**P2-3**（报告小偏差）。

### 7.3 修复后的 tag 口径
- 形成**新提交**（不要在工作区脏状态下打 tag；评测时工作区有并行未提交修改，且不属于 `9d7afd0`），重新跑全量（≥265）与独立安全夹具；
- tag 建议 `m2a`（仅 M2a 单仓安全交付），**不得**打 `m2-ops`；
- 三件证据（`docs/m2a-ac-report.md`、`reqs/v0.2/fixtures/m2-matrix.json`、`ui/scripts/cdp-m2a-selfcheck.mjs`）须与实际代码一致，并由独立 Gate 复评签字后才能宣布“M2a 通过”。

---

## 8. 关键复跑命令

```powershell
# 0) 目标提交（工作区可能有并行改动；评审须以提交对象为准）
git -C D:\Workspace\git_1 rev-parse 9d7afd0

# 1) 不可变树 + 全量测试（隔离产物，不锁运行中 Host；0 失败/0 跳过/0 警告）
$d="$env:TEMP\m2-9d7afd0-tree"; git archive --format=zip 9d7afd0 -o "$env:TEMP\m2-9d7afd0.zip"
Expand-Archive "$env:TEMP\m2-9d7afd0.zip" $d -Force
Set-Location $d; dotnet test MbdGitTool.sln --artifacts-path "$env:TEMP\m2final-artifacts-exact" -v minimal

# 2) 独立安全夹具（24 断言；2 FAIL = R-1/R-2）
cd "$env:TEMP\m2final-harness2"; dotnet run --no-build

# 3) 真实审计文件核对（成功/失败/字段/无 token）
Get-Content "$env:LOCALAPPDATA\MbdGitTool\audit\operations.jsonl" -Tail 20
#   缺 actor/request_id；含 RTCT_Demo 真实 COMMIT/PUSH 与 E_CONFIRM_REQUIRED 失败样本

# 4) N-1 最小复现（危险 Given）：远端建 feature（main 为其祖先）后
#    PULL args:{branch:"feature"} → 期望 E_INVALID_ARGUMENT、HEAD/分支零变化、无 fetch argv
```

---

## 9. 附：证据摘录

**全量测试（9d7afd0 导出树）**

```text
已通过! - 失败: 0，通过: 71，已跳过: 0，总计: 71  - MbdGitTool.Core.Tests.dll
已通过! - 失败: 0，通过: 194，已跳过: 0，总计: 194 - MbdGitTool.Host.Tests.dll
```

**独立夹具（9d7afd0 精确 DLL；22 PASS / 2 FAIL）**

```text
[PASS] N1.rejected: ok=False code=E_INVALID_ARGUMENT
[PASS] N1.details_expected_current: expected=main current=feature
[PASS] N1.head_zero_change / N1.branch_zero_change
[PASS] N1.no_network_git_before_reject: networkGitCount=0
[PASS] N1.audit_plus_one / N1.audit_failure_fields
[FAIL] S15.stale_matrix_should_block_pull_on_dirty:
       期望=拒绝(DIRTY.PULL=N)；实测 ok=True
[PASS] S15.no_pre_action_rescan: snapshotT0 == snapshotNow（未重扫）
[PASS] S15.actual_pre_action_state_is_dirty: freshScanState=DIRTY
[PASS] S15.audit_records_stale_state: audit.state=SYNCED（实际动作前=DIRTY）
[PASS] CAS.four_actions_rejected: PULL/CHKOUT/COMMIT/PUSH 全 E_CONCURRENT_CHANGE
[PASS] PULL.explicit_current_branch_ff: ok=True（N-1 修复未误伤正常 PULL）
[PASS] AUDIT.exactly_one_line / AUDIT.no_token_in_file / AUDIT.no_token_in_response
[FAIL] AUDIT.required_fields_complete: missing=[actor,request_id]
```

**真实审计（%LOCALAPPDATA%，真实 Gitea 样本，评测基线）**

```text
{"ts":"2026-11-14T06:49:19Z","action":"COMMIT","componentId":"RTCT_Demo","repoType":"DESIGN","state":"DIRTY","preHead":"037211dd…","postHead":"037211dd…","ok":false,"errorCode":"E_CONFIRM_REQUIRED",…}
{"ts":"2026-11-14T06:50:09Z","action":"COMMIT","componentId":"RTCT_Demo","repoType":"DESIGN","state":"DIRTY","preHead":"037211dd…","postHead":"73cee5b…","ok":true,"errorCode":null,…}
{"ts":"2026-11-14T06:50:14Z","action":"PUSH","componentId":"RTCT_Demo","repoType":"DESIGN","state":"AHEAD","preHead":"73cee5b…","postHead":"73cee5b…","ok":true,…}
{"ts":"2026-11-14T07:08:07Z","action":"FETCH","componentId":"RTCT_Demo",…,"ok":true,…}
# token/Authorization/ghp_/Basic 命中=0；字段=ts,action,componentId,repoType,state,preHead,postHead,ok,errorCode,durationMs（缺 actor/request_id）
```

**单源矩阵比对**：`m2-matrix.json` 14 态 × 5 键 与 `RepoActionService.Matrix` 逐格 `enabled/requiresConfirm` 比对 **mismatches=0**。

> 本报告只新增本文件；未修改任何源码、配置、真实仓库或运行中 Host。工作区在评测期间存在**并行进程**的未提交修改（8 个文件，非本评审产生），评审结论以 `9d7afd0` 提交对象为准。所有构建/夹具证据位于 `%TEMP%`（`m2-9d7afd0-tree-*`、`m2final-artifacts-*`、`m2final-harness2`、`m2final-harness-exact.out`）。
