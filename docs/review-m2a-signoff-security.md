# M2a 最终安全签收复评报告（提交 `fe98b95`）

| 项目 | 内容 |
|---|---|
| 复评对象 | 提交 `fe98b95`（`fix(m2): complete final gate evidence (write pre-read-through, audit actor/requestId, fixture parity, M2a CDP selfcheck 10/10)`，2026-11-14 15:25:21 +0800，2026-11-14 15:25 评审） |
| 本轮定位 | 对 `docs/review-m2a-final-security.md`（对象 `9d7afd0`，结论“不打”）中 **R-1/R-2/R-3 三项阻断项**及原 P0/P1 回退面的**最终签收复评**；按 lead 指定 6 项核对（read-through、审计 JSONL、N-1+矩阵夹具、全量测试、回归、签收结论）执行 |
| 输入 | `docs/review-m2a-final-security.md`、`reqs/requirements.md` §13.1.2（AC-SAFE-14…17/AC-OP-33）、`reqs/adr/D-025-m2a-m2b.md` §2.2/§3/§5、`docs/m2a-ac-report.md`、`docs/review-m2-gate-security.md`、`reqs/v0.2/fixtures/m2-matrix.json` |
| 环境 | Windows 11 / git 2.56.1.windows.1 / dotnet SDK 10.0.404 / 仓库 `D:\Workspace\git_1`，`HEAD=fe98b95`，评审开始时工作区干净（`git status` 空）；评审开始时运行中 Host PID 32464（15:24:45 启动，`bin\Debug\net10.0-windows\MbdGitTool.Host.exe` 构建于 15:22:09），评审期间该进程已不再运行（**非本评审所杀**） |
| 方法 | ① 严格以 **commit 对象**为准：`git archive fe98b95` 导出不可变树到 `%TEMP%\m2a-fe98b95-tree`；② 在同一导出树跑两轮全量测试（普通构建、`--artifacts-path` 隔离构建，均不触碰真实工作区）；③ 独立黑盒夹具（`%TEMP%\m2a-signoff-harness`，引用 **fe98b95 精确构建产物** `MbdGitTool.Host.dll`，真实 git + 隔离审计目录 + `GIT_TRACE2_EVENT` argv 审计，35 项断言）；④ 独立解析 Host `Matrix` 与 `m2-matrix.json` 逐格比对（14×5=70 格）；⑤ 真实 `%LOCALAPPDATA%\MbdGitTool\audit\operations.jsonl` 最近行/全文件核对；⑥ `git diff 9d7afd0 fe98b95` 逐行核对回退面 |
| 边界 | **只新增本报告，未修改任何源码/配置/测试/真实仓库**；全部构建与夹具位于 `%TEMP%`；未对真实 Gitea 发起任何写操作；真实审计文件在评审全程保持 **69 行不变**（两次全量测试与黑盒夹具均使用临时审计目录） |
| 日期 | 2026-11-14 |

---

## 0. 结论（Gate 判决）

**R-1 的 read-through 主项、R-2（审计 actor/requestId）、R-3（测试 hermetic）均已闭环；全量 271/271 通过、0 warning；N-1 与矩阵 14×5 对齐。但本次修复在桥层引入一处新的 CAS 回退：`runRepoAction` 写前 refresh 后直接用“重扫后的 HEAD”作为 CAS 基线，丢失了“用户请求所依据快照 HEAD”这一基线，导致 AC-SAFE-15 Then①（外部改 HEAD → 四动作均 `E_CONCURRENT_CHANGE`、远端 refs 零改动）在桥层实测不成立，且黑盒夹具实测一次 `PUSH` 把外部提交真的推到了远端。**

> **Gate 结论：提交 `fe98b95` 不建议打 `m2a` tag（不打）。**
> 阻断项：**P1-1** AC-SAFE-15 Then① 桥层 CAS 回退（实测 `PUSH` 执行成功并改变远端 refs；服务层 CAS 仍有效，问题在桥层基线选择），以及 `docs/m2a-ac-report.md:21` 对 CAS 的“四动作 E_CONCURRENT_CHANGE 零改动”陈述在桥层不成立（与本轮新 P1 同源）。
> R-1（read-through）/R-2/R-3 与本轮未回退的凭据作用域/ignored/ref/EMPTY.PUSH 不受该问题影响。

| 汇总口径 | 结果 |
|---|---|
| ① `runRepoAction` 写前 `RefreshAsync`（read-through），stale 快照不得复用 | **PASS**（静态 + 行为：外部 dirty 后 stale 缓存被重扫，动作被 DIRTY 矩阵拒绝、审计记 DIRTY、HEAD 零改动、无网络 git；`GeneratedAt`/`Freshness.LocalScanAt` 单调前进） |
| ② 审计 JSONL 含 `actor`/`requestId`、成功/失败各恰 1 行、无 token | **PASS**（新 schema 真实行 1 条含 `actor=Trevn`/`requestId`；路由测试成功/失败各恰 1 行；canary 无泄漏；测试 hermetic，真实文件零新增） |
| ③ N-1（PULL 显式 branch≠当前 → `E_INVALID_ARGUMENT`）与 `RepoActionAvailabilityMatrixTests` 夹具 14×5 | **PASS**（N-1 黑盒 9/9；矩阵独立比对 14 态×5 键 **0 mismatch**；单源测试普通构建通过） |
| ④ 全量测试 | **PASS（271/271、0 跳过、0 失败、0 warning）**；但 `--artifacts-path` 隔离构建下为 **270/271（1 失败：矩阵夹具找不到）**，属夹具路径解析缺陷（P2-1），非文件锁 |
| ⑤ 回归：凭据作用域 / ignored / ref / CAS / EMPTY.PUSH 不退化 | **FAIL（CAS 桥层回退）**：凭据作用域、ignored、ref、EMPTY.PUSH **PASS 未回退**；**CAS 在桥层退化**（服务层未退化），见 §5/§6 |
| 新 P0 | **0** |
| 新 P1（阻断出口） | **1**（P1-1 桥层 CAS 回退 + 验收报告陈述不符） |
| 新 P2 | **4**（P2-1…P2-4，§7） |
| 是否建议 M2a gate 通过 / 打 tag | **否（不打）**；修复 P1-1 并重跑桥级 CAS 证据/全量测试后复评 |

---

## 1. 验证方法与环境说明（可复跑）

### 1.1 以 commit 对象为准，工作区零改动
评审开始 `git status --short` 为空；评审前后 `HEAD=fe98b95503e5e4e5dba559b97056375920a0449d`。为排除构建产物/并行改动干扰，全部构建与测试在 `git archive fe98b95` 导出的不可变树 `%TEMP%\m2a-fe98b95-tree` 中执行（评审结束后该临时树仍保留）。评审仅新增本文件（工作区另有并行 UX 评审产生的未跟踪文件 `docs/review-m2a-signoff-ux.md`，非本评审产物）。

### 1.2 两次全量测试（均不锁运行中 Host）
```powershell
$tree = "$env:TEMP\m2a-fe98b95-tree"   # git archive fe98b95 导出
Set-Location $tree
# A. 普通构建（导出树不锁工作区 bin，可安全执行）
dotnet test MbdGitTool.sln -v minimal
# B. 隔离产物构建（复现“避锁”路线）
dotnet test MbdGitTool.sln --artifacts-path "$env:TEMP\m2a-final-artifacts" -v minimal
```

### 1.3 独立黑盒夹具（不复用仓库测试代码）
`%TEMP%\m2a-signoff-harness`：临时 bare remote + cloneA/cloneB，经**真实 `HostCommandRouter.DispatchAsync`** 驱动（覆盖 `runRepoAction` 桥层，而非仅服务层），审计目录注入 `%TEMP%`；`GIT_TRACE2_EVENT` 记录每次动作启动的全部 git 子进程 argv。结果 **35 断言：33 PASS / 2 FAIL**（2 FAIL 同一根因 = P1-1；服务层 CAS 对照断言 PASS）。
复跑：`cd %TEMP%\m2a-signoff-harness; dotnet run`。

### 1.4 真实审计文件（消费者数据）
基线 **69 行**（最后一条 07:25:02Z，新 schema）。评审两次全量测试 + 黑盒夹具后仍为 **69 行**（测试全部走 `TestAudit.NewConfig` 注入的 `%TEMP%` 目录），证明 R-3 已闭环；该文件 0 行非法 JSON、token/Authorization/base64 命中 0。

---

## 2. 核对项①：`runRepoAction` 写前 read-through —— **PASS**

### 2.1 静态核对
- `src/MbdGitTool.Host/Bridge/HostCommandRouter.cs:248-269`：`case "runRepoAction"` **先** `await _snapshots.RefreshAsync(cancellationToken)`（`:251`），**后**解析参数并通过 `_snapshots.Index`（`:261`）把**重扫后的 Index** 交给 `_actions.RunAsync`；审计回调在此处注入 `actor`/`requestId`（`:253-254, :263-268`）。
- `SnapshotService.RefreshAsync`（`SnapshotService.cs:92-104`）经 `_refreshGate` 串行化并 `BuildAndStoreAsync`（`:106-114`）原子替换 `_current`/`_index`；`runRepoAction` 不再走 `EnsureSnapshotAsync`（该 helper `:391-397` 仍服务只读方法）。
- 动作成功后 `_snapshots.Invalidate()` + `InvalidateCaches()`（`:272-276`）仍保留，用于下一次请求重建。
- 全仓 `RefreshAsync` 调用点 = `case "refresh"`（`:225-230`）+ `case "runRepoAction"`（`:251`）；没有“复用旧快照”的写前路径。

### 2.2 行为实测（黑盒夹具，fe98b95 精确产物）

| 断言 | 实测 |
|---|---|
| 动作前宿主缓存快照为旧态 | 外部已改 tracked 文件（HEAD 不变）后，`getSnapshot` 仍返回 `SYNCED`、`GeneratedAt=2026-11-14T07:31:04.497Z`（未重扫，符合缓存语义） |
| 动作前真实状态 | 独立 Host 重新扫描 = **DIRTY** |
| 对该 stale 快照执行 PULL | `ok=false, code=E_ACTION_NOT_ALLOWED, details.state=DIRTY`（**拒绝**，DIRTY.PULL=N）——旧实现此处会 `ok=true` |
| 零改动 / 无网络 git | HEAD 前后同为 `459bca4c…`；`GIT_TRACE2_EVENT` 中 fetch/push/pull/ls-remote 子进程数 = **0** |
| 审计记录动作前真实态 | 新增行 `state="DIRTY"`、`action="PULL"`、`ok=false` |
| `scan_at` 前进 | 动作前快照 `GeneratedAt`/`Freshness.LocalScanAt`: `07:31:02.964Z → 07:31:06.182Z`（单调前进） |
| 动作后快照反映真实态 | 动作后 `getSnapshot` = `DIRTY` |

> 说明：AC-SAFE-15 用 `local_state.scan_at` 表述；M2a 快照契约中等价字段为 `Freshness.LocalScanAt`（`SnapshotDto.cs:62-66`，值 = `GeneratedAt`），实测在写前重扫后单调前进。未新增独立 `scan_at` 字段本身不构成阻断，但仓库内无“单调前进”自动断言（P2-4）。

**核对项①判定：PASS（R-1 的 read-through 主项已闭环）。**

---

## 3. 核对项②：审计 JSONL `actor`/`requestId`、成功/失败各 1 行、无 token —— **PASS**

### 3.1 实现与字段
- `OperationAuditService.cs:96-112`：每条审计行序列化 `ts/action/componentId/repoType/state/preHead/postHead/ok/errorCode/durationMs/actor/requestId`，`actor`/`requestId` 经 `Sanitize`（`:143-152`，≤256 字符）。
- `HostCommandRouter.cs:253-254`：`requestId = Guid.NewGuid().ToString("N")`、`actor = Environment.UserName`；`:263-268` 在回调中注入后 `_audit.Record(execution)`；`RunAsync`（`RepoActionService.cs:166-208`）在成功/失败/异常后**恰回调一次**。
- 并发追加：`OperationAuditService` 进程内按文件互斥 + `FileShare.ReadWrite` 单次写（`:114-129`）。

### 3.2 测试与真实行
- 单测：`OperationAuditServiceTests.cs:23-58` 断言 `actor`/`requestId` 落盘；`:82` 并发 32 次恰 32 行；`:99-151` 路由级成功 COMMIT **恰 1 行**且不含 token/base64/output；`:153-190` 路由级失败 COMMIT **恰 1 行**且 `errorCode/preHead/postHead` 正确。
- 真实文件最新行（**新 schema 证据**）：
  ```text
  {"ts":"2026-11-14T07:25:02.2413433Z","action":"FETCH","componentId":"RTCT_Demo","repoType":"DESIGN",
   "state":"SYNCED","preHead":"73cee5be…","postHead":"73cee5be…","ok":true,"errorCode":null,
   "durationMs":621,"actor":"Trevn","requestId":"3fc8d23e2c1e419e8c79243e1cd2f1f9"}
  ```
  （运行中 Host 构建于 15:22:09、动作发生于 15:25:02，源文件 mtime 15:20–15:21，与该构建一致；`actor/requestId` 由真实进程写入。）
- 全文件：69 行、0 行非法 JSON、`ghp_/Authorization/Basic /token` 命中 **0**；`actor` 非空行 = 1（新 schema），其余 68 行为修复前旧 schema（无 actor/requestId，属历史行）。
- 黑盒夹具：6 次 `runRepoAction`（成功/矩阵拒绝/N-1 失败/失败 fail-closed）→ 审计恰 6 行、逐次 +1；`actor` 全部非空、`requestId` 6 个全不同；`ghp_CANARY_…` 与 base64 在审计文件与响应 JSON 命中 0（含 `E_GITEA_CONFIG_INVALID` fail-closed 失败样本）。
- **Hermetic（R-3 闭环）**：所有构造 `HostCommandRouter` 的测试均注入临时 `AuditRoot`（`TestInfrastructure.cs:318-334` 的 `TestAudit`；`HostCommandRouterTests.cs:142,315,321`、`RepoActionSecurityTests.cs:395,538`、`RealWorkspaceSmokeTests.cs:51`）。两轮全量测试与夹具运行后，真实审计文件保持 69 行（未污染）。

**核对项②判定：PASS。** 残余（非阻断）：真实文件缺少“新 schema 的失败行”样本（历史失败行均为旧 schema）；`requestId` 为 camelCase（AC 写作 `request_id`，与 `preHead/before_head` 等一样按语义等价接受，建议文档写明别名）；历史 64 行测试污染行未清理/标注（P3）。

---

## 4. 核对项③：N-1 与矩阵夹具 14×5 对齐 —— **PASS**

### 4.1 N-1（PULL 显式 branch ≠ 当前分支 → `E_INVALID_ARGUMENT`）
- 实现未变（`RepoActionService.PullAsync` 在任何 fetch/merge 之前读当前分支并比较，details 给 `expected/current/sdkCode=GIT_PULL_BRANCH_MISMATCH`）。
- 回归测试已按上轮 P2-1 强化：`tests/MbdGitTool.Host.Tests/RepoActionGitTests.cs:85-114` 真实创建远端 `feature`（`main` 为其祖先、feature 领先）并断言：`ok=false`、`E_INVALID_ARGUMENT`、`details.field="branch"`、HEAD/分支零改动、**HEAD ≠ feature tip**。
- 黑盒夹具 9/9 PASS：拒绝码、`expected=main/current=feature`、HEAD/分支零改动、网络 git argv=0、审计 +1（失败行 preHead==postHead）且带 `actor/requestId`。

### 4.2 矩阵夹具 14×5 与 Host 代码对齐
- 独立脚本（不经仓库测试代码）解析 `src/MbdGitTool.Host/Services/RepoActionService.cs:63-80` 的 `Matrix` 与 `reqs/v0.2/fixtures/m2-matrix.json`，逐格计算 `enabled=cell∈{Y,C}`、`requiresConfirm=(cell=='C' ∨ action∈{COMMIT,PUSH})`、`reasonCode=STATE_BLOCKED/CONFIRM_REQUIRED/AVAILABLE`：
  ```text
  states_host=14 states_fixture=14 cells=70 mismatches=0
  host_only=[] fixture_only=[]
  ```
- 新增 `tests/MbdGitTool.Host.Tests/RepoActionAvailabilityMatrixTests.cs` 从夹具文件读 14 态×5 键，断言 `status=="aligned"`、`Enabled/RequiresConfirm/ReasonCode/MatrixAllowed` 逐格一致；**普通构建下 PASS**（随 271/271）。
- 覆盖 M2a 冻结口径：`DIVERGED.PULL=disabled`、`EMPTY.PUSH=disabled`、COMMIT/PUSH 恒 `requiresConfirm=true` 均与代码一致；`CommitAndPush_WithoutConfirm_AreRejectedByFrozenConfirmFlag`（`RepoActionGitTests.cs:116-137`）通过。

**核对项③判定：PASS。**（矩阵单源测试在 `--artifacts-path` 构建下存在路径缺陷，见 P2-1；夹具 `freezeNotes` 内部矛盾 + 仓库仍存第三份手写矩阵，见 P2-2。）

---

## 5. 核对项④：全量测试 —— **PASS（附 1 项 P2 证据链缺陷）**

### 5.1 权威结果（fe98b95 导出树，普通构建）
```text
已通过! - 失败: 0，通过: 71，已跳过: 0，总计: 71  - MbdGitTool.Core.Tests.dll (net10.0)
已通过! - 失败: 0，通过: 200，已跳过: 0，总计: 200 - MbdGitTool.Host.Tests.dll (net10.0)   (1 m 20 s)
```
即 **271/271**（Core 71 + Host 200）；0 失败、0 跳过；构建输出 `warning/MSB3027/MSB3021` 命中 **0**，且 `Directory.Build.props` 设 `TreatWarningsAsErrors=true`，构建成功 ⇒ **0 警告**。与 `docs/m2a-ac-report.md:32` 的 271/271 口径一致（Host 200 = 上轮 194 + 本轮新增 6）。

### 5.2 “避锁”路线下的缺陷（P2-1，非文件锁）
用 `--artifacts-path` 隔离产物（任务书建议的避锁方式）重跑：
```text
已通过! - 通过:   71，总计:   71  - MbdGitTool.Core.Tests.dll
失败!  - 失败:    1，通过:  199，总计:  200 - MbdGitTool.Host.Tests.dll
  RepoActionAvailabilityMatrixTests.GetAvailability_MatchesSingleSourceFixture_ForAllStatesAndActions
  错误消息: 夹具不存在：reqs\v0.2\fixtures\m2-matrix.json
```
根因：`RepoActionAvailabilityMatrixTests.FindFixture()`（`:48-63`）从 `AppContext.BaseDirectory` 逐级向上找 `reqs/v0.2/fixtures/m2-matrix.json`；`--artifacts-path` 把 bin 放到 `%TEMP%` 后向上找不到仓库根目录，于是失败。**这不是文件锁，也不是产品代码失败**，但意味着“271/271”在“不杀 Host 的避锁路线”下不可复现（要么杀 Host 用普通构建，要么修夹具发现逻辑，见 P2-1）。

**核对项④判定：PASS（271/271、0 warning；`--artifacts-path` 路线 270/271 的缺陷记 P2-1）。**

---

## 6. 核对项⑤：回归（凭据作用域 / ignored / ref / CAS / EMPTY.PUSH）—— **FAIL（CAS 桥层回退，P1-1）**

### 6.1 静态回退面（`git diff 9d7afd0 fe98b95 -- src/`）
仅 3 个源文件、26 行新增：`HostCommandRouter.cs`（写前 refresh + audit 注入）、`OperationAuditService.cs`（字段序列化）、`RepoActionService.cs`（`RunCoreAsync` 参数可空标注 + `RepoActionExecution.Actor/RequestId`）。**凭据作用域、ignored `--no-overwrite-ignore`、ref 校验、EMPTY.PUSH 矩阵/禁用的代码路径未被改写**；测试侧只做了 `TestAudit` 注入与 N-1 夹具强化。

| 回退项 | 结论 | 证据 |
|---|---|---|
| 凭据作用域（P0-1） | **PASS（未回退）** | 7 个 `Token_*` 用例（URL 不匹配/0 网络/fail-closed/http/URL 作用域 header/日志不泄漏/argv）全部通过；黑盒 `token+缺 base` → `E_GITEA_CONFIG_INVALID`、0 网络、审计与响应无 canary/base64；代码未改 |
| ignored 保护（P1-1） | **PASS（未回退）** | `Checkout_IgnoredFileWouldBeOverwritten_IsRejectedAndFileKept`、`Pull_IgnoredFileWouldBeOverwritten_IsRejectedAndFileKept` 通过；`--no-overwrite-ignore` 代码未改 |
| ref 校验（P1-3） | **PASS（未回退）** | 不存在 branch/tag `E_REF_NOT_FOUND`、`+/-` 前缀拒绝、N-1 显式分支拒绝、detached PUSH 拒绝、无 force 用例通过；代码未改 |
| EMPTY.PUSH（D-025） | **PASS（未回退）** | `Matrix["EMPTY"]="NYCYN"` 与夹具 `EMPTY.PUSH.enabled=false` 一致（14×5 比对 0 mismatch）；`requiresConfirm` 冻结用例通过 |
| **CAS（P1-2）** | **FAIL（桥层回退）** | 见 §6.2：服务层 stale Index 仍 `E_CONCURRENT_CHANGE`；但**桥层 `runRepoAction` 写前 refresh + 用重扫 HEAD 作基线**，导致“用户请求所依据快照 HEAD ≠ 执行时 HEAD”这一并发变化不再被 CAS 捕获 |

### 6.2 P1-1：AC-SAFE-15 Then① 桥层实测失败（新问题）

**复现场景（黑盒夹具，fe98b95 精确产物；与 AC-SAFE-15 Given/When 一致）**：
1. 宿主扫描得到快照 `H0=459bca4c…`、`state=SYNCED`；
2. 外部进程 commit 改 HEAD 为 `H1=89f0e5be…`（`state=AHEAD`）；
3. 经 `HostCommandRouter` 对旧快照分别执行 PULL / CHKOUT(main, confirm=true) / COMMIT(confirm=true) / PUSH(confirm=true)。

**实测**：
```text
CAS detail: snapshotState=SYNCED snapshotHead=459bca4c externalHead=89f0e5be
            results=[PULL:E_ACTION_NOT_ALLOWED, CHKOUT:OK, COMMIT:E_ACTION_NOT_ALLOWED, PUSH:OK]
FAIL CAS.all_four_concurrent_change   （AC 期望四个动作全 E_CONCURRENT_CHANGE）
FAIL CAS.remote_refs_zero_change      （remote before=459bca4c after=89f0e5be）
PASS CAS.local_head_zero_change
PASS CAS.service_layer_still_detects_stale_head
     （对照：直接用 stale Index 调 RepoActionService.RunAsync → ok=False code=E_CONCURRENT_CHANGE）
```

**判定**：AC-SAFE-15 Then① 明文要求“执行时 HEAD≠快照 HEAD → 四动作逐项 `E_CONCURRENT_CHANGE`；仓库 HEAD/分支/index/工作区/**远端 refs 零改动**”，ADR §5（`reqs/adr/D-025-m2a-m2b.md:177`）冻结的 Host 断言同样是“扫描 → 外部 commit 改 HEAD → `RunRepoAction` → `E_CONCURRENT_CHANGE`”。本轮实测：PULL/COMMIT 被矩阵挡下（错误码为 `E_ACTION_NOT_ALLOWED`，非 CAS），**CHKOUT 与 PUSH 直接执行**，`PUSH` 把外部提交推到了远端，**远端 refs 发生变化**。这不是“安全失败”，而是桥层不再以用户所见快照为 CAS 基线。

**根因（静态定位）**：`HostCommandRouter.cs:248-269` 在写前 `RefreshAsync` 后把 `_snapshots.Index`（含 `H1`）传给 `RepoActionService.RunAsync`；`ExecuteAsync` 的 CAS（`RepoActionService.cs:385-396`、`CheckSnapshotHead:1060-1078`）比较“快照 HEAD（此刻已是重扫后的 `H1`）”与“执行时 HEAD（`H1`）”，恒相等。R-1 修复建议中“**CAS 语义保留为‘请求快照 HEAD vs 重扫 HEAD / 执行时 HEAD’**”只落了后半句，**没有保留请求/用户快照（refresh 前的 `H0`）作为 CAS 基线**，因此 refresh 把“旧快照 → 新 HEAD”的变化吞掉了。

**影响**：外部工具（终端 git / IDE）在用户点击动作与 Host 执行之间改变 HEAD 时，PUSH 会推送用户未在 UI 中看到的提交、COMMIT 会提交到用户未看到的新 HEAD（本次 `COMMIT` 因 AHEAD 矩阵先被拒，属巧合）；确认条给出的“将推送 N 个提交”与实际推送数量不一致。单机单用户下仍需 CAS（SRS 冻结、不得放宽）。

**修复建议（P1-1）**：
- 在 `runRepoAction` 中：refresh **之前**先捕获请求时刻的目标仓快照 HEAD（`_snapshots.Index` 中的 `repo.Repo.HeadSha`，或 `_snapshots.Current`）；refresh 后若该仓 HEAD 变化 → **先返回 `E_CONCURRENT_CHANGE`（零改动、审计 1 行、无网络 git）**；未变化才进入矩阵/动作；服务层 CAS 继续承担 refresh 与执行之间的窗口。
- 补桥级回归测试（AC-SAFE-15 Then①）：scan → 外部 commit → `runRepoAction` × 4 → 全 `E_CONCURRENT_CHANGE`，本地/远端 refs 零改动，审计每动作 1 行；与现有 `CasMismatch_WriteAction_IsRejectedWithoutChanges`（服务层）分层并存。
- 复核 `docs/m2a-ac-report.md:21` 的 CAS 陈述，按桥层实测更新。

---

## 7. 新发现问题（按严重度）

### P1-1【P1，阻断 AC-SAFE-15 / 证据陈述不符】桥层 CAS 基线丢失，外部改 HEAD 后 PUSH 可继续写远端
- 位置：`HostCommandRouter.cs:248-269`（写前 refresh 后直接用新 Index）、`RepoActionService.cs:385-396, 1060-1078`；陈述：`docs/m2a-ac-report.md:21`。
- 实测：§6.2（`PUSH:OK`、远端 refs `459bca4c→89f0e5be`；四动作无 `E_CONCURRENT_CHANGE`）。
- 性质：M2a 出口 AC 未全绿 + 验收报告对 CAS 的陈述在桥层不成立（服务层单测 PASS 不能替代桥级断言）。

### P2-1【P2，测试/证据链】矩阵单源测试在 `--artifacts-path` 构建下失败（270/271）
- 位置：`tests/MbdGitTool.Host.Tests/RepoActionAvailabilityMatrixTests.cs:48-63`（从 `AppContext.BaseDirectory` 上溯找夹具）。
- 实测：§5.2。
- 修复：把 `reqs/v0.2/fixtures/m2-matrix.json` 作为 `Content` 复制到测试输出（或允许 `MBD_REPO_ROOT`/`git rev-parse --show-toplevel` 兜底），使普通构建与 `--artifacts-path` 构建都能读同一夹具；然后两路线各复跑 271。

### P2-2【P2，单源矩阵不自洽】夹具 `status=aligned` 但 `freezeNotes` 仍写“状态为 SINGLE_SOURCE_PENDING_CODE_ALIGNMENT”；仓库仍存第三份手写矩阵
- `reqs/v0.2/fixtures/m2-matrix.json:16` 与 `:24` 自相矛盾（测试只断言 `status`，无法发现）；`RepoActionAndComponentDetailTests.cs:18-36` 仍有 `ExpectedMatrix` 手写期望，违反 ADR §5“Host 单测与 CDP 共用，禁止第三份手写期望”。
- 修复：更新 `freezeNotes`；删除/改为从夹具派生第三份矩阵（或显式记录其为过渡）。

### P2-3【P2，验收报告不一致】`docs/m2a-ac-report.md` 三处与代码/事实不符或残缺
- `:21` CAS 陈述（并入 P1-1，须改为桥层实测口径）；
- `:24` AC-OP-33 字段清单仍为旧 10 字段，**未列 `actor/requestId`**（代码已实现）；
- `:36` 存在字面量 `\n\n`（Markdown 未换行，整段落成一行）；`:30` “尾行”摘录已过期（当前尾行为 07:25 新 schema FETCH）。

### P2-4【P2，测试覆盖缺口】路由级审计字段与 scan 单调性无自动断言
- `Router_RunRepoAction_Success_WritesOneAuditLineWithoutSecrets` 未断言路由注入的 `actor/requestId` 非空（只测了直调 `Record` 的重载）；也无“同一 request_id 恰 1 行/重启后可查”的路由级用例（行为已由新 schema 真实行 + 黑盒夹具证明）。
- 无自动测试断言“写前重扫后 `Freshness.LocalScanAt` 单调前进”（黑盒夹具实测前进，但仓库测试未覆盖）。
- 建议补断言，使 AC-SAFE-15②/AC-OP-33 的“可复跑证据”落在仓库测试内。

### P3（记录，不阻断）
- 真实审计文件仍残留 64 行历史测试污染行（`MOCK_*`/`CF100A_Demo`，07:10–07:13Z，无 token）；R-3 已不再新增，但未按上轮建议清理/标注。
- `actor/requestId` 为 camelCase（AC 写 `request_id`，语义等价可按既有别名口径接受，建议在验收报告注明）。
- `runRepoAction` 现在每次动作前全量重扫工作区；真实 4 仓环境实测 ~3s 级（M2a 可接受），仓数增长时需复核 AC-UI-10 的 ≤2s 回写口径（性能，非安全阻断）。

---

## 8. 与上一轮阻断项（R-1/R-2/R-3）的闭环核对

| 上轮项 | 状态 | 说明 |
|---|---|---|
| R-1 写前 read-through 未实现 + 报告不实 | **部分闭环（主项 PASS）** | refresh 已实现且行为实测成立（§2）；但同一修复未保留“请求快照 HEAD” CAS 基线，引入 P1-1（§6.2），且报告 CAS 陈述仍不实 |
| R-2 审计缺 `actor`/`request_id` | **闭环** | 字段落盘（真实行/单测/夹具）、失败也写、无 token、恰 1 行（§3） |
| R-3 审计测试非 hermetic | **闭环** | 全部 Host 路由测试注入临时 `AuditRoot`；两轮全量测试真实文件零新增（69→69） |
| 上轮 P2-1 N-1 测试 Given 偏弱 | **闭环** | 真实远端 feature 分支 + `field=branch`/HEAD≠feature tip 断言（§4.1） |
| 上轮 P2-2 矩阵单源 | **部分闭环** | 新增夹具单源测试且普通构建 PASS；`freezeNotes` 矛盾与第三份手写矩阵仍在（P2-2） |

---

## 9. Gate 结论、必须修项与 tag 指引

### 9.1 结论
**M2a Gate 不通过；提交 `fe98b95` 不建议打 `m2a` tag。**
- 阻断：**P1-1**——AC-SAFE-15 Then① 桥层 CAS 回退（实测 PUSH 改变远端 refs），并导致 `docs/m2a-ac-report.md:21` 陈述不实；按 SRS“实现偏差不得据此放宽本 AC”与 D-025“M2a 出口 13 条 AC 全绿”，不能签收。
- 无新 P0。R-1（read-through 主项）、R-2、R-3 已闭环；凭据作用域/ignored/ref/EMPTY.PUSH 未见回退。

### 9.2 必须修项（按优先级）
1. **P1-1**：`runRepoAction` 保留 refresh 前的请求快照 HEAD 作为 CAS 基线；refresh 后发现 HEAD 变化 → `E_CONCURRENT_CHANGE` 零改动（在任何矩阵动作/网络 git 之前）；补桥级四动作回归测试（本地/远端 refs 零改动）+ 更新 `m2a-ac-report` CAS 口径。
2. **P2-1**：修矩阵夹具发现路径，使普通构建与 `--artifacts-path` 都是 271/271。
3. **P2-2/P2-3**：修夹具 `freezeNotes` 与单源矩阵；更新 `m2a-ac-report` 字段清单/格式/过期摘录。
4. 建议同时补 P2-4 路由级 `actor/requestId` 与 scan 单调性断言；清理/标注历史测试污染审计行（P3）。

### 9.3 修复后的 tag 口径
- 形成**新提交**（工作区须干净；当前工作区仅有并行评审文档未跟踪，不含修复）；重跑普通 + `--artifacts-path` 两路线全量（≥271、0 warning）与桥级安全夹具；
- 产出更新后的 `docs/m2a-ac-report.md`（CAS 桥层实测证据 + 审计新字段真实成功/失败行）与一致的 `m2-matrix.json`；
- 独立 Gate 复评签字后，才可对**该复评通过的提交**打 `m2a` tag；`m2-ops` 仍不得打。

---

## 10. 复跑命令

```powershell
# 0) 目标提交与不可变树（工作区零改动）
git -C D:\Workspace\git_1 rev-parse fe98b95
$tree="$env:TEMP\m2a-fe98b95-tree"
git archive --format=zip fe98b95 -o "$env:TEMP\m2a-fe98b95.zip"
Expand-Archive "$env:TEMP\m2a-fe98b95.zip" $tree -Force

# 1) 普通构建全量（权威 271/271；导出树不锁工作区 bin）
Set-Location $tree; dotnet test MbdGitTool.sln -v minimal

# 2) 避锁路线（当前为 270/271：矩阵夹具找不到，P2-1）
dotnet test MbdGitTool.sln --artifacts-path "$env:TEMP\m2a-final-artifacts" -v minimal

# 3) 独立黑盒夹具（35 断言；2 FAIL = P1-1）
cd "$env:TEMP\m2a-signoff-harness"; dotnet run

# 4) 真实审计行核对（新 schema 行应含 actor/requestId；无 token）
Get-Content "$env:LOCALAPPDATA\MbdGitTool\audit\operations.jsonl" -Tail 3

# 5) 矩阵 14×5 独立比对（不经仓库测试代码）
#    解析 RepoActionService.cs Matrix 与 m2-matrix.json，逐格 enabled/requiresConfirm/reasonCode
```

---

## 11. 附：关键证据摘录

**黑盒夹具（fe98b95 精确产物；35 断言，2 FAIL 同源 P1-1）**
```text
PASS SNAP.initial / N1.*（9 项，含 details expected=main current=feature、no_network_git=0、audit_plus_one、audit_actor_requestid）
PASS S15.cached_snapshot_is_stale_before_action / actual_pre_action_state_is_dirty
PASS S15.stale_matrix_should_block_pull_on_dirty（E_ACTION_NOT_ALLOWED, details.state=DIRTY）
PASS S15.no_network_git_on_refusal / audit_state_is_pre_action_reality
PASS S15.pre_action_rescan_happened / scan_timestamp_advanced / local_scan_at_advanced
     GeneratedAt 2026-11-14T07:31:02.9647342Z -> 2026-11-14T07:31:06.1823493Z
PASS CAS.service_layer_still_detects_stale_head（直调 stale Index → E_CONCURRENT_CHANGE）
FAIL CAS.all_four_concurrent_change
     results=[PULL:E_ACTION_NOT_ALLOWED, CHKOUT:OK, COMMIT:E_ACTION_NOT_ALLOWED, PUSH:OK]
FAIL CAS.remote_refs_zero_change（remote before=459bca4c after=89f0e5be）
PASS AUDIT.exactly_one_line / no_token_in_file / no_token_in_response / actor_requestid_on_failure
PASS AUDIT.all_lines_have_min_fields（6 行）/ request_ids_unique（6 distinct）/ no_actor_empty
----SUMMARY---- TOTAL=35 FAIL=2 [CAS.all_four_concurrent_change,CAS.remote_refs_zero_change]
```

**全量测试（普通构建，fe98b95 导出树）**
```text
已通过! - 失败: 0，通过:  71，已跳过: 0，总计:  71  - MbdGitTool.Core.Tests.dll (net10.0)
已通过! - 失败: 0，通过: 200，已跳过: 0，总计: 200 - MbdGitTool.Host.Tests.dll (net10.0)
warning/MSB3027/MSB3021 命中 = 0；TreatWarningsAsErrors=true
```

**真实审计（`%LOCALAPPDATA%\MbdGitTool\audit\operations.jsonl`，评审基线/结束均为 69 行）**
```text
最新行（新 schema）：
{"ts":"2026-11-14T07:25:02.2413433Z","action":"FETCH","componentId":"RTCT_Demo","repoType":"DESIGN",
 "state":"SYNCED","preHead":"73cee5be…","postHead":"73cee5be…","ok":true,"errorCode":null,
 "durationMs":621,"actor":"Trevn","requestId":"3fc8d23e2c1e419e8c79243e1cd2f1f9"}
全文件：69 行 / 非法 JSON=0 / token 模式命中=0；新 schema 1 行，旧 schema 68 行（历史）
```

**矩阵独立比对**
```text
states_host=14 states_fixture=14 cells=70 mismatches=0
host_only=[] fixture_only=[]
```

> 本报告只新增本文件；未修改任何源码、配置、测试、真实仓库或运行中 Host。工作区干净；两轮构建/测试与全部夹具在 `%TEMP%`（`m2a-9d7afd0…` 为上一轮、`m2a-fe98b95-tree`、`m2a-final-artifacts`、`m2a-signoff-harness*`）；真实用户审计文件未被本次评审改动（69 行不变）。
