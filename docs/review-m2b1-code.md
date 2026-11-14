# M2b-1 代码对抗评审报告（提交 `3ea870e`）

| 项目 | 内容 |
|---|---|
| 评审对象 | 提交 `3ea870e`（`feat(m2b): job engine, batch operations and task center (M2b-1)`，2026-11-14 16:18:45 +0800） |
| 评审范围 | M2b-1：作业内核（job/job_item/JOB_UPDATED/取消/仅重试失败/持久化）+ 批量 PULL/FETCH/SYNC/CLONE/DELETE/RESTORE/CREATE/TAG + 任务中心；**只评审、不改代码** |
| 输入 | `src/MbdGitTool.Host/Services/{JobEngine,JobStore,BatchOperationService,OperationAuditService,RepoActionService}.cs`、`src/MbdGitTool.Host/Bridge/{HostCommandRouter,WebViewBridge}.cs`、`src/MbdGitTool.Host/MainWindow.xaml.cs`、`src/MbdGitTool.Core/Contracts/JobDto.cs`、`tests/MbdGitTool.Host.Tests/JobEngineTests.cs`、`docs/m2b-status.md`、SRS v0.3.1-r2 §8.1（`reqs/requirements.md:381-404`）/§9.7（`:507-528`）/§13.1.2（`:1114-1120`）、ADR D-025 §2.3/§4（`reqs/adr/D-025-m2a-m2b.md:83-95,150-152`）、AC 目录（`reqs/v0.2/ac-catalog.md:895-1020`） |
| 环境 | Windows 11 / git 2.56.1.windows.1 / dotnet SDK 10.0.404 / 工作区 `D:\Workspace\git_1`，`HEAD=3ea870e`、`git status --short` 空（本报告文件除外）；评审期间运行中 Host PID 45672（Debug 构建，未杀、未干扰） |
| 方法 | ① `git show 3ea870e` + 逐行静态核对（行号均指该提交）；② 全量测试实跑（Release 隔离输出）；③ 只读核对本机真实数据 `%LOCALAPPDATA%\MbdGitTool\{jobs\jobs.json,audit\operations.jsonl}`（16:16–16:18 的作业/审计行，时间早于被评提交，非并行评审产生）；④ 与 SRS/ADR/AC 目录逐条对照 |
| 边界 | **未修改任何源码/配置/真实仓库**；唯一产出为本文件。测试构建输出落 `src/**/bin/Release`、`obj`（gitignored）；未对真实 Gitea 发起任何写操作。注：评审期间另有并行 UI 评审在驱动真实 Host（16:23 之后的 job/audit 行不计入本报告证据） |

---

## 0. 结论（判决）

> **不建议打 tag（既不打 M2b-1 候选 tag，更不具备 M2b tag 条件）。**
> **汇总：P0 = 1，P1 = 7，P2 = 10。** 289/289 测试已复跑通过（Core 71 + Host 218，0 失败/0 跳过），但测试绿不等于 AC 绿：本轮 1 个 P0（批量破坏性操作绕过 §7.6 矩阵与预检）+ 7 个 P1 分别命中 M2b 出口 AC（AC-OP-15/16/21/26/27/28/29、AC-UI-10c）与 FR-ACT-05/06/FR-OP-08/10 的明文要求。
>
> 阻断口径：**P0-1 必须修**；P1-1/P1-2/P1-4/P1-5 直接影响 AC-UI-10c 与 AC-OP-26/27/28 的可验收性，属“修完再复评”项；其余 P1 可在 M2b-2 收口但与 M2b 出口绑定。`docs/m2b-status.md:3-4` 自述“M2b-1 候选、不宣称 M2b 完成”与实况基本一致，但 `:10-17` 的若干“已交付”表述过强（见 §4/§5/§7）。

| 汇总口径 | 结果 |
|---|---|
| 全量测试 | **289/289 通过**（Core 71 + Host 218，0 失败/0 跳过）；`TreatWarningsAsErrors=true` 构建成功（`Directory.Build.props:8,19`） |
| 真实环境实测 | 本机确有 16:16–16:18 的真实作业/审计数据（FETCH/CLONE/DELETE/RESTORE/SYNC 各 4/4、取消 12/12、重试 12/12），**但证据未随提交归档、不可复跑**（P2-1） |
| 新 P0 | **1**（P0-1） |
| 新 P1（阻断复评） | **7**（P1-1…P1-7） |
| 新 P2 | **10**（P2-1…P2-10） |
| 是否建议 tag | **否**。修复 P0-1，并至少闭环 P1-1/P1-2/P1-4 后复评；M2b-2（审计完整表/备份 ref 恢复/EMPTY 首推/离线/journal）未完成前不得写 M2b 完成 |

---

## 1. 评审基线与可复跑说明

1. **基准是提交对象**：本轮工作区干净，`HEAD=3ea870e`，以下所有 `文件:行号` 均对应该提交。
2. **全量测试实跑**（本机，未改动源码）：
   ```powershell
   dotnet test MbdGitTool.sln -c Release --nologo -v q
   # 已通过! - 失败: 0，通过: 71， 总计: 71  - MbdGitTool.Core.Tests.dll (net10.0)
   # 已通过! - 失败: 0，通过: 218，总计: 218 - MbdGitTool.Host.Tests.dll (net10.0)   （1 m 26 s）
   ```
   与 `docs/m2b-status.md:35` 的 289/289 一致；构建在 `TreatWarningsAsErrors=true` 下成功 ⇒ 0 warning 成立。
3. **真实数据旁证（只读）**：评审开始时本机 `jobs.json` 有 11 个作业，其中 **8 个创建于 16:16:47–16:18:18（早于提交 16:18:45）**：FETCH SUCCESS 4/4、CLONE SUCCESS 4/4、DELETE SUCCESS 4/4、RESTORE SUCCESS 4/4、SYNC SUCCESS 4/4、FETCH SUCCESS 1/1、FETCH CANCELED 12/12、FETCH SUCCESS 12/12（取消后重试）；其余 3 个为 16:23 后并行评审产生。评审时 `operations.jsonl` 127 行，含 `jobId/jobItemId/backupRef/actor/requestId`，无 token/Authorization。**说明真实场景确曾跑通**，但这些数据仅存于本机 `%LOCALAPPDATA%`、不随提交归档、不可在干净环境复现，不构成 AC 证据链（见 §7、P2-1）。
4. **并行声明**：另有并行评审在驱动 UI/真实 Host；16:23 后的新 job/audit 行不是本报告结论的依据。并行 UX 评审（review-m2b-ux）随后回传了同机真实 Host 的实测快照（§1.5），本报告将其作为**旁证**引用并标注来源，未独立复跑。

### 1.5 并行 UX 实测旁证（真实 Host，16:2x；来源 review-m2b-ux，截图 `artifacts/ui-review/m2b-ux-*.png`）

| # | 观察 | 与本报告关系 |
|---|---|---|
| 1 | 实时取消 16 项 FETCH：job `7d6664a1…` state=CANCELED、ok=0/total=16/skipped=4；12 个 item 为 **CANCELED + `errorCode=E_TIMEOUT` + message“作业取消：未开始执行”**，4 个为 SKIPPED；取消请求 245ms 到达、**全部 item 实际未开始**；历史 job `dd46d919…` 12/12 同样全 CANCELED+E_TIMEOUT | 印证 §2.2/§2.3 的“未开始项被写成 CANCELED+E_TIMEOUT、取消≠超时”判定（P1-2）；DOM 行级 `0/16 跳过 4` 无“已取消 N”计数（UX） |
| 2 | reload 后历史仍含 `dd46…`（CANCELED 12/12）与 `2319…`（retry SUCCESS 12/12），未重复执行 | 印证 §3.2“重启不重复执行”（进程级）成立 |
| 3 | `JOB_UPDATED` payload 键 = `cancellable,createdAt,failed,finishedAt,id,items,kind,ok,skipped,startedAt,state,total`，**无 `jobId`/`current`**；取消后同一 job 先推 RUNNING+cancellable=false 再推 CANCELED | 独立印证 §6/P1-1 的契约缺失 |
| 4 | 实时批量 FETCH 16 仓时 `RTCT_RemoteOnly/IMPL` 一次 FAILED：`E_CREDENTIAL_SCOPE`“无法读取 origin push URL（未启动网络 git）”；**retry 同一仓成功** | 新增 P2-10（FETCH/SYNC 也被要求 push URL 作用域，过严/瞬时失败） |

**说明**：以上为并行评审的实测陈述与截图，不是本轮独立复跑；结论只采用其中与代码路径可互相印证的部分（1、3、4 均能在代码定位）。

---

## 2. 作业并发 / 取消 / 状态机（对应评审要求 1）

### 2.1 单 worker、逐仓串行

| 核对项 | 证据 | 判定 |
|---|---|---|
| 单一 worker 串行执行 job | `JobEngine.cs:119-121`（unbounded Channel，`SingleReader=true`）、`:358-384`（双层 `WaitToReadAsync`/`TryRead`，逐个 `await ExecuteJobAsync`） | 实现存在 |
| job 内 item 串行、同仓不并发 | 同上；同 job item 为 `foreach` 顺序 await（`:417-431`） | **PASS（同 job 内）** |
| 不同组件可并行（AC-OP-29③）/ 批量并发默认 2（FR-OP-01） | 只有 1 个 worker，全局串行：任一 job 执行期间其余 job 均 PENDING；无并发上限配置/并发语义 | **FAIL → P1-3** |
| `job_item.started_at/finished_at` 保序 | 时间列真实写入（`:515-521,567-582`，`JobStore` 持久化 `:188-199`） | PASS（仅单 worker 场景） |

### 2.2 cancel 后 git 进程是否真的被杀

| 核对项 | 证据 | 判定 |
|---|---|---|
| 取消链路 | `CancelJob`：PENDING 直接 CANCELED；RUNNING 置 `CancelRequested` + `Cts.Cancel()`（`JobEngine.cs:232-290`） | 实现存在 |
| 杀进程树 | `GitCli.RunAsync`：`WaitForExitAsync(token)` 取消 → `KillProcessTree(process.Kill(entireProcessTree:true))`（`GitCli.cs:115-137,142-155`） | **PASS** |
| 真实杀进程证据 | `JobEngineTests.cs:287-317`：本地回环“挂起 HTTP 服务器”上取消挂起 fetch，<15s 进入 CANCELED，断言服务端收到连接（证明 git 网络进程确已启动后被终止） | **PASS（行为）**；AC-OP-27① 的“≤2s”未断言（测试给 20s 超时/15s 上界，`docs/m2b-status.md:28` 的“12/12”无计时证据）→ P2-1 |
| 取消覆盖操作类型 | `ToDto` 的 `Cancellable = PENDING/RUNNING && !CancelRequested`（`:1689`），与 kind 无关；`CancelJob` 对任何 kind 生效 | **FAIL（FR-ACT-05：FETCH 短任务不提供取消、COMMIT 不可取消）→ P1-2** |

### 2.3 取消/完成竞态

| 场景 | 证据 | 判定 |
|---|---|---|
| cancel 落在最后一个 item 完成与 `FinalizeJob` 之间 | `CancelJob` 在锁内置 `CancelRequested=true`；`FinalizeJobUnsafe` 先判 `CancelRequested || 任意 canceled` 直接置 CANCELED（`JobEngine.cs:832-836`）——即使全部 item 已 OK，job 也会显示 CANCELED | **竞态缺陷 → P1-2** |
| 取消与 worker 收尾的重复回调 | `CancelJob` 在 CANCELED 时 `Task.Run(InvokeJobFinished)`（`:283-286`）；worker 的 `finally` 也调 `InvokeJobFinished`（`:483`）→ 同一 job 完成回调可能执行两次（当前仅失效缓存，无功能后果） | P2（并入 P2-3） |
| 取消失败项的错误码 | OCE 分支记 `BridgeErrorCodes.Timeout`（`JobEngine.cs:551-554`），未开始项亦记 `E_TIMEOUT`（`:788-794`）；PENDING 取消路径不写 `ErrorCode/Message`（`:797-805`）。并行实测（§1.5-1）：取消在 245ms 到达、全部 item 实际未开始，仍被写成 `CANCELED + E_TIMEOUT + 作业取消：未开始执行`；历史 job 12/12 同形 | **FAIL（取消≠超时、未开始项状态失真）→ P1-2** |

### 2.4 job 状态机合法性（SRS §8.1 冻结 CHECK 集）

SRS 冻结：`job.state ∈ QUEUED/RUNNING/SUCCEEDED/FAILED/CANCELLED/INTERRUPTED/PARTIAL`，`job_item.state ∈ QUEUED/RUNNING/OK/FAILED/BUSY/SKIPPED/CANCELLED/INTERRUPTED`（`reqs/requirements.md:393-394`；AC-UI-10c 亦用 `CANCELLED`，`:1119`）。

| 核对项 | 证据 | 判定 |
|---|---|---|
| 实际取值 | `JobStates`：`PENDING/RUNNING/PARTIAL/SUCCESS/FAILED/CANCELED/INTERRUPTED`；`JobItemStates`：`PENDING/RUNNING/OK/FAILED/SKIPPED/CANCELED/INTERRUPTED`（`BatchOperationService.cs:944-966`）；DTO 文档注释同样如此（`JobDto.cs:6-7`） | **PENDING≠QUEUED、SUCCESS≠SUCCEEDED、CANCELED≠CANCELLED、缺 BUSY → P1-1** |
| 合法迁移 | PENDING→RUNNING（`:405`）、RUNNING/PENDING→CANCELED（`:254-277,832-837`）、RUNNING→INTERRUPTED（`:859-874`、加载 `:1555-1561`）、RUNNING→PARTIAL/SUCCESS/FAILED（`:839-846`） | 迁移图本身合法；但**枚举字面不符冻结契约** |
| `PARTIAL` 语义 | `failed>0 && ok>0` → PARTIAL；`failed>0 && ok==0` → FAILED（`:839-846`） | PASS |
| `cancel_requested` | SRS `job.cancel_requested`（`:393`）、AC-OP-27⑤ 要求 `job.cancel_requested=1`；`JobDto` 无该字段（`JobDto.cs:9-21`），`StoredJob` 不持久化（`JobStore.cs:175-185`），加载后恒 false | **不可断言 → P1-1** |
| “全跳过”job 状态 | 预跳过/矩阵跳过使 `failed==0` → `SUCCESS`（`:839-845`），即使 `ok=0`；测试把“全 SKIPPED 也 SUCCESS”固化（`JobEngineTests.cs:229-233`） | **假绿风险 → P2-5** |
| 状态上限/200 淘汰 | `PruneJobsUnsafe` 只淘汰终态（`:1483-1495`），`JobStore.Trim` 保留最新 200（`JobStore.cs:138-146`） | 见 §3.4 |

### 2.5 retryFailed 是否只重试失败/中断项、不重复成功项

| 核对项 | 证据 | 判定 |
|---|---|---|
| 过滤集合 | 仅 `FAILED/INTERRUPTED/CANCELED` 生成新 item（`JobEngine.cs:326-336`）；成功/跳过不复制；无重试项返回 `jobId=null, accepted=false`（`:339-342`） | **PASS**（含 CANCELED 与 AC-UI-10c④“只对失败/取消仓发新 item”一致） |
| 原 job 未结束拒绝 | PENDING/RUNNING 拒绝重试（`:318-324`） | PASS |
| 真实数据 | `jobs.json`：CANCELED 12/12 之后 1s 内出现重试 SUCCESS 12/12（08:18:18Z） | PASS（旁证） |
| 持久化后重试 | `Arguments` 随 `StoredJob` 持久化（`JobStore.cs:175-212`），INTERRUPTED 作业可重试 | PASS |
| 防御性 | `RetryFailed` 直接信任 `jobs.json` 里的 `Kind/Arguments`，不重新过 `KindWhitelist`/`ValidateKindArguments`/confirm 门禁（`:296-354`；对照 `RunBatch` `:947-1089`） | P2（并入 P2-2/P2-6） |

---

## 3. 持久化 / 崩溃（对应评审要求 2）

### 3.1 jobs.json 是否原子写

| 核对项 | 证据 | 判定 |
|---|---|---|
| 原子性 | 临时文件 `jobs.json.<guid>.tmp` + `File.Move(overwrite:true)`（`JobStore.cs:116-136`）；失败保留旧文件并清理 tmp | **PASS（进程级）** |
| 落盘强度 | 无 `Flush(true)`/目录同步；停电/蓝屏可能丢最后一次写（非损坏） | P2-2 |
| 损坏处理 | JSON 损坏/IO 失败 → `Load()` 返回空表并仅 `HostLog.Warn`（`JobStore.cs:87-95`）；随后任意新 job 落盘会以空历史覆盖原文件（`JobEngine.cs:1572-1575` 只在有 job 时 persist，但新 job 的 `Enqueue→PersistUnsafe` 会覆盖） | P2-2 |
| 写失败可见性 | `Save` 失败仅告警（`JobStore.cs:126-134`）；`PersistUnsafe` 失败仅告警（`JobEngine.cs:1578-1597`）；UI/任务中心无任何“持久化失败”提示 | P2-2 |
| 每 item 全量重写 | `ExecuteItemAsync` 每个 item 结束都 `PersistUnsafe()`（`JobEngine.cs:567-582`）：最坏 800 item × 最近 200 job 全量序列化；同时 `:520-522` 注释声称“不在此处落盘，避免每个 item 重写整份 jobs.json”，与实现矛盾 | P2-2 |
| 并发读写 | 同一进程内 JobStore 静态文件锁 + 引擎 `_sync` 串行（`JobStore.cs:39-43`、`JobEngine.cs:68,1515-1528`） | PASS（进程内） |
| 跨进程并发 | 仅进程内锁；Host 未见单实例实现（全仓无 Mutex/单实例代码）；双开 Host 会竞写 jobs.json/audit（last-writer-wins） | **P2-2** |

### 3.2 重启 RUNNING→INTERRUPTED、不重复执行

| 核对项 | 证据 | 判定 |
|---|---|---|
| 加载即标 INTERRUPTED、绝不再入队 | `JobEngine.cs:1554-1561`、`:848-874`；测试 `JobEngineTests.cs:322-393` 断言无审计写入/磁盘回写 INTERRUPTED | **PASS（不重复执行）** |
| 进程内退出兜底 | `Dispose()` 对非终态 job 标 INTERRUPTED 后落盘（`:1742-1790`） | PASS |
| 并行实测旁证（§1.5-2） | reload 后历史仍含 CANCELED 12/12 与 retry SUCCESS 12/12，未重复执行 | PASS（进程级） |
| **未开始项状态** | `MarkInterruptedUnsafe` 把 `PENDING` 与 `RUNNING` 的 item 一并标 `INTERRUPTED`（`:868-873`）；测试断言 PENDING 作业/项也变 INTERRUPTED（`JobEngineTests.cs:382-384`）。而 AC-OP-26① 明文要求“未开始 1 行**保持待执行**”（`ac-catalog.md:988`） | **FAIL → P1-5**（测试把错误行为固化） |
| 真实强杀重启 E2E | 无；仅“伪造 jobs.json → 新建引擎”单测 | 测试缺口（P2-1） |

### 3.3 200 上限

| 核对项 | 证据 | 判定 |
|---|---|---|
| 保留最近 200 | `JobStore.MaxJobs=200`（`:24`）、`Trim`（`:138-146`）；测试 205→200、最旧 5 被淘汰、`E_JOB_NOT_FOUND`（`JobEngineTests.cs:396-435`） | **PASS** |
| 非终态堆积 | `PruneJobsUnsafe` 找不到终态即停止淘汰（`JobEngine.cs:1483-1495`），但落盘被 `JobStore.Trim` 截为最新 200 → 内存可 >200 而非终态最旧 job 只存在于内存、不再落盘（重启丢失） | P2-2（边界） |

### 3.4 路径/权限失败

| 核对项 | 证据 | 判定 |
|---|---|---|
| JobsRoot 默认 `%LOCALAPPDATA%\MbdGitTool\jobs`（可注入） | `JobStore.cs:26-30,45-58`、`AppConfig.cs:39-43` | PASS |
| 目录不可写/路径非法 | `Directory.CreateDirectory`/写入异常被捕获仅告警（`JobStore.cs:121-134`）；`GetFullPath` 异常在构造时抛出（`JobStore.cs:55`，由 `JobEngine` 构造 → 若 JobsRoot 非法可影响引擎构造，未做 fail-soft 兜底） | P2-2 |
| 网络盘/漫游目录 | 未按 SRS §8.2 禁止网络盘（该约束针对 DB，jobs.json 未校验） | P2（低） |

---

## 4. 批量安全（对应评审要求 3）

### 4.1 componentIds 显式、路径逃逸

| 核对项 | 证据 | 判定 |
|---|---|---|
| 显式 componentIds、禁止隐式全量 | 空/缺省/空串/上限（200 组件、800 item）逐项拒绝（`JobEngine.cs:995-1085`） | **PASS** |
| componentIds 去重、repoTypes 白名单 | `:1003-1019`、`:1134-1162` | PASS |
| 路径逃逸 | 批量路径全部经 `PathPolicy` 收口（`BatchOperationService.cs:95-105,249-254,332-379,426-442`）；CLONE 目标名 `IsSafeDirectoryName`（`:691-720`）；单测 `..\..\evil` 被拒（`JobEngineTests.cs:795-826`） | **PASS** |
| DELETE 目标限工作区直接子目录 + reparse 检查 | `BatchOperationService.cs:338-349` | PASS |

### 4.2 DELETE 是否只 `.trash`、禁止直接删/跨 workspace

| 核对项 | 证据 | 判定 |
|---|---|---|
| 仅 `Directory.Move` 到 `<root>\.trash\<UTC stamp>\<repoName>`，不改远端 | `BatchOperationService.cs:364-405`；单测远端 bare 仍存在（`JobEngineTests.cs:560-561`） | **PASS** |
| 禁止直接递归删除 | 同卷 `Directory.Move`；仅跨卷回退“复制成功后删源”（`:907-935`），不删工作区外 | PASS（回退路径非原子，失败时源保留：`:392-400`） |
| 30 天保留清理 | `CleanupTrash` 只删 `.trash` 下 14 位时间戳目录（`:611-666`）；启动时执行一次（`JobEngine.cs:1723-1738`）；测试未覆盖 | PASS/测试缺口 |
| **预检四阻断（脏/未推送/进行中/stash）** | FR-OP-06 P0、AC-OP-21 要求默认阻断；实现只查 `IN_PROGRESS`/`busy` 快照标记（`BatchOperationService.cs:107-110,351-354`），**不检查 dirty/AHEAD/stash**，直接把仓库移入 `.trash`；无 dry-run 丢失清单 | **FAIL → P0-1** |
| DELETE 失败占用 → DELETE_PENDING | 未实现（AC-OP-23），失败统一 `E_IO_ERROR` | P2（M2b-2） |

### 4.3 RESTORE 冲突

| 核对项 | 证据 | 判定 |
|---|---|---|
| 目标已存在 → SKIPPED（不覆盖） | `BatchOperationService.cs:437-442`；单测 `JobEngineTests.cs:582-595` | PASS（安全优先） |
| 冲突预览/确认（AC-OP-30③④） | UI 仅通用确认框（`workbench.ts:2275-2287`）；无“将覆盖的当前状态/远端已变动/备份过期”预览；`args.trashStamp` 无 UI 入口 | P2-8 |
| 指定 stamp / 最近一份 | 指定 stamp 校验 14 位数字（`:446-463`）；缺省按 stamp 倒序取最近（`:464-480`） | PASS |
| 备份 ref 恢复（`refs/backup/*`） | 未实现（`docs/m2b-status.md:42` 承认）；SYNC 的 `backupRef` 只展示不可恢复 | P2-8（M2b-2） |

### 4.4 SYNC 备份失败不 reset

| 核对项 | 证据 | 判定 |
|---|---|---|
| 顺序 = fetch → remote ref 校验 → 备份 ref → stash -u → reset --hard（禁止 `git pull`） | `BatchOperationService.cs:134-215` | **PASS（顺序/语义）** |
| 备份失败/无 HEAD/remote 分支不存在 → 不 reset | `:143-177` 提前返回；单测 unborn 分支场景 HEAD 仍 unborn（`JobEngineTests.cs:485-525`） | **PASS** |
| 备份 ref 命名/内容 | `refs/backup/sync/<UTC yyyyMMddHHmmss>/<REPO_TYPE>`（`:164-177`）；单测 `git rev-parse` = preHead（`:470-476`） | PASS（stamp 秒级，同秒同类型重跑会覆盖同一 ref → P2 低） |
| **§7.6 矩阵门禁** | SYNC 在 `JobEngine` 中直接走 `_batch.SyncAsync`（`JobEngine.cs:630-646`），**从不调用 `_actions.GetAvailability`**；`BatchOperationService` 全文件无矩阵/状态判断（grep 0 命中）。§7.6：AHEAD SYNC=❌（`requirements.md:359`）、DIVERGED SYNC=⚠ 默认阻断+备份 ref+输入组件 ID（`:357`） | **FAIL → P0-1** |
| **外部改动 CAS（AC-OP-15③）** | SYNC 读到 `preHead` 后直接建备份 ref 并 reset（`:156-215`），不比较 job 开始快照 HEAD/不检测执行期外部改动；按实现语义静态推断：外部提交会被备份 ref 捕获但操作仍返回 OK，而非 `OP-E002` 零改动（本轮未单独实测，代码路径确定） | FAIL（AC-OP-15③）→ P0-1（并入） |
| 失败/取消时 backupRef 丢失 | `reset` 失败走 `MapGitFailure`（`:209-215`），返回的 `BatchItemResult` 无 `BackupRef`；OCE 由 JobEngine 记 CANCELED，同样丢 ref（`JobEngine.cs:549-555`）→ 审计/job_item `backupRef=null`，已创建的备份 ref 失去关联 | P2-4 |

### 4.5 CLONE 凭据作用域 / token 泄漏

| 核对项 | 证据 | 判定 |
|---|---|---|
| PAT 仅经 URL 作用域 `http.<base>/.extraHeader` 注入，不进 argv | `RepoActionService.cs:97,879-897,976-987`；`GitCli` 经 `startInfo.Environment` 注入（`GitCli.cs:87-94`）；参数走 `ArgumentList`（`:63-80`） | **PASS（有 token 时）** |
| fail-closed | 有 token 且 baseUrl/org 缺失、非法、userinfo、http 未显式允许 → 拒绝且不启动网络 git（`RepoActionService.cs:903-974`）；单测 token+file:// 配置 FAILED 且不泄漏（`JobEngineTests.cs:642-683`） | PASS |
| 泄漏面 | job/审计/日志无 token/base64/Authorization（单测 `JobEngineTests.cs:672-682,828-855`；真实 audit 127 行扫描 0 命中）；git 子进程可继承 `GIT_CONFIG_VALUE_0`（既有 M2a P1-5 风险，非本提交引入） | PASS（本提交路径） |
| **无 token 但 baseUrl 含 userinfo** | `TryBuildCloneCredentialEnvironment` 在 `token==null` 时**直接返回 null、跳过 ValidateCredentialConfiguration**（`RepoActionService.cs:882-887`）；`CloneAsync` 用 `baseUrl` 原样拼 URL 进 argv（`BatchOperationService.cs:263-288`）→ 配置里的凭据会进入命令行（进程列表可见），违反 FR-SAFE-06“绝不进 URL/命令行”；日志/UI 侧有 `HostLog.Redact` 的 URL userinfo 脱敏兜底（`HostLog.cs:59-70,117-119`） | **P2-7（配置相关、失败开放）** |

### 4.6 dryRun 零写 / 强写 confirm

| 核对项 | 证据 | 判定 |
|---|---|---|
| dryRun 零仓库写 | dry-run 分支在任何 `_batch.*`/`_actions.RunAsync` 之前返回 SKIPPED（`JobEngine.cs:618-628`）；仅做快照读（`SnapshotService` 本地扫描+Gitea GET，无 git 写） | **PASS（仓库零写）** |
| dry-run 仍写 jobs.json + 审计行 | Enqueue 落盘；dry-run item 产生审计行（`result=SKIPPED, ok=false, errorCode=null`；真实 audit 中已见 CREATE dry-run 行） | P2-4（预览污染 `operation_log`，AC-OP-16②“未确认前写入操作数=0”需澄清是否含审计） |
| dryRun 是否真预检 | `PreviewDryRun` 只查“仓在/已克隆/IN_PROGRESS”，不检查 COMMIT staged、CHKOUT ref 存在、CLONE URL/磁盘空间、SYNC 丢失清单，却回“已预检通过”（`JobEngine.cs:748-773`） | **FAIL → P1-6** |
| 强写 confirm | Host 门禁：COMMIT/PUSH/SYNC/DELETE/RESTORE/CREATE/TAG 无 `args.confirm=true` 一律 `E_CONFIRM_REQUIRED`（`JobEngine.cs:52,981-993`；单测 `:756-793`）；UI 确认后才提交并统一补 `confirm`（`workbench.ts:2127-2130`） | **PASS（门禁）** |
| 但 confirm 为“通用确认” | 无 `safety.requireTypedConfirm`（输入组件 ID）对照；DIVERGED 强覆盖、>10 组件/>40 仓均无第二重确认（`JobEngine.cs:36-37` 注释声称“二次确认在 UI 侧”，UI 无实现） | P0-1 / P2-9 |

---

## 5. 审计（对应评审要求 4）

### 5.1 每 item 恰 1 行、字段齐全、无 token、失败也写

| 核对项 | 证据 | 判定 |
|---|---|---|
| 已执行 item 恰 1 行 | `ExecuteItemAsync` 末尾 `WriteItemAudit` 一次（`JobEngine.cs:584,912-943`）；单测 FETCH 2 item = 2 行（`JobEngineTests.cs:100-115`）；SYNC 1 行含 backupRef（`:478-482`） | **PASS（已执行路径）** |
| 字段 | 行含 `ts/action/componentId/repoType/state/preHead/postHead/ok/errorCode/durationMs/actor/requestId/result/jobId/jobItemId/backupRef`（`OperationAuditService.cs:89-156,175-227`）；真实 audit 核对一致 | PASS（job 行） |
| 失败也写 | item FAILED 会写（`JobEngineTests.cs:847-855`）；**但**：① 受理阶段预跳过 item（矩阵不允许、未克隆、组件不在快照）从不进入 `ExecuteItemAsync`（`JobEngine.cs:1052-1073`）→ 0 行；② 取消时未开始 item 被 `CancelRemainingItemsUnsafe` 改写（`:786-805`）→ 0 行；③ job 级内部异常路径把 PENDING/RUNNING 项标 FAILED（`:446-465`）→ 0 行 | **FAIL → P1-4** |
| 无 token | 审计只写元数据，不写 output/stderr（`OperationAuditService.cs:16-21,106-156`）；单测/真实数据扫描 0 命中 | PASS |
| 与 M2a 行兼容 | 新增 `result` 字段恒输出（`:123,213-214`），旧 M2a 行无该字段 → 同一 JSONL 混有两代形状（真实文件当前 58 行含 result、69 行不含）；文档注释“M2a 单仓行不出现这几个可选键”只对 jobId/jobItemId/backupRef 成立，`result` 对所有行输出 | P2-4 / 需补兼容性断言 |
| `requestId` 语义 | 批量行 `requestId` = 作业级 GUID（`JobEngine.cs:165,348,933`），非桥请求 ID；M2a 行为桥请求 ID（`HostCommandRouter.cs:290-291`）→ 同一列两种语义 | P2-4 |

### 5.2 审计与 job 的关联

- `jobId/jobItemId` 真实写出行（`JobEngine.cs:933-935`），可用 `getJob` 反查；但 `operation_log` 的 `target_json/before_json/after_json`（AC-OP-28①）尚未实现，JSONL 也不是 SQLite `operation_log` 表（`docs/m2b-status.md:41` 承认归 M2b-2）→ M2b 出口未满足（P2-6 记录，非 M2b-1 隐瞒）。

---

## 6. 事件（对应评审要求 5）

| 核对项 | 证据 | 判定 |
|---|---|---|
| 线程安全（UI Dispatcher） | `MainWindow.xaml.cs:73-91`：`EventSink` 内 `Dispatcher.HasShutdownStarted` 检查 + `Dispatcher.BeginInvoke`；`WebViewBridge.PostEvent` 要求 UI 线程（`WebViewBridge.cs:69-101`），序列化/超长/销毁均只告警丢弃 | **PASS** |
| 消息形状 | 固定 `{event:"JOB_UPDATED", data:JobDto}`（`JobEngine.cs:1695-1719,1860-1863`；单测 `JobEngineTests.cs:79-94`）；但 AC-UI-10c② 要求 `{jobId,state,current,total,cancellable}`——实际是 `data.id`、无 `current`（进度需 `ok/failed/skipped` 推导），`data.cancel_requested` 也缺；并行实测（§1.5-3）payload 键实为 `cancellable,createdAt,failed,finishedAt,id,items,kind,ok,skipped,startedAt,state,total`，独立确认无 `jobId/current` | **FAIL（契约）→ P1-1**；形状本身 PASS |
| 洪水控制 | 每条状态变化都 Publish：受理 1 + 开始 1 + 每 item 2 条（开始/结束）+ 取消剩余 1 + 终态 1 ≈ **2N+3 条**，每条携带**全量 items**；无节流/合并/序列号（`JobEngine.cs:524,585,812,1695-1719`）；800 item 上限（`:40`）下约 1603 条、单条最大含 800 个 item。对照架构约束“`job.progress` 事件 ≤10Hz、UI 节流 100ms”（`reqs/v0.2/diagrams.md:167`） | P2-3 |
| 上限/断连 | 单条 >2MB 丢弃（`WebViewBridge.cs:94`、`HostCommandRouter.cs:26`）；断连丢弃后 UI 靠 1.5s 轮询兜底（`workbench.ts:1894-1913`） | PASS（有兜底），但无“丢事件的最终一致性”断言 |
| UI 轮询是否重复更新 | 终态通知有防重（`workbench.ts:1793,1809`）；但 `mergeJob` 只对计数取 max、状态/items 用 `{...next}` 覆盖（`:1753-1761`），迟到的轮询响应可把 item 状态从 OK 回退为 RUNNING（显示回退/闪烁），非终态重复通知风险低 | P2-3 |
| 伪造事件风险 | 仅 Host→UI 单通道；UI 接受 `event/type` + `data/job/payload` 多形（`api.ts:553-583`），来源校验仅用于 Host 侧入站（`WebViewBridge.cs:264-274`）；页面内脚本可伪造 JOB_UPDATED 触发通知/刷新，但 UI 包为本地可信资源，风险低 | PASS（低风险，记录） |

---

## 7. 测试缺口与真实环境证据（对应评审要求 6）

### 7.1 已有测试（17 个 JobEngine Fact，全部通过）

覆盖：FETCH 成功/事件/审计每 item 1 行（`JobEngineTests.cs:33-116`）、COMMIT PARTIAL+retry（`:120-174`）、全失败 retry（`:176-207`）、矩阵跳过（`:211-234`）、PENDING/RUNNING 取消+杀 git（`:238-317`）、重启 INTERRUPTED（`:322-393`）、200 上限（`:395-435`）、SYNC 备份 ref/备份失败不 reset（`:439-525`）、DELETE→RESTORE（`:529-596`）、CLONE（`:600-640`）、token fail-closed（`:642-683`）、CREATE/TAG（`:686-751`）、参数/路径/令牌（`:755-855`）。

### 7.2 缺口（按 AC 归口）

| 缺口 | 对应 AC/要求 | 级别 |
|---|---|---|
| 真实强杀 Host→重启 E2E（AC-OP-26 明确“杀进程重启”） | AC-OP-26；FR-SAFE-04 | P2-1 |
| DELETE 预检四阻断（dirty/AHEAD/IN_PROGRESS/stash） | AC-OP-21 | P0-1（无测试且无实现） |
| SYNC AHEAD/DIVERGED 阻断/强确认、外部改动 `OP-E002` 零改动 | AC-OP-15③/AC-OP-16 | P0-1 |
| 取消 ≤2s、取消后 `job.cancel_requested=1`、四类操作取消可用性 | AC-OP-27①⑤⑥ | P1-2 |
| 预跳过/取消未开始/内部异常 item 的审计行数 | AC-OP-28、AC-OP-33 | P1-4 |
| dryRun 零写（含“无 ref/无文件变化/无 job 入队”断言） | AC-OP-16② | P1-6 |
| 事件节流/洪泛/断线补发 | 架构约束 `diagrams.md:167` | P2-3 |
| FETCH/SYNC 被要求 push URL 作用域而失败（真实 Host 一次 `E_CREDENTIAL_SCOPE`“无法读取 origin push URL”，retry 成功） | FR-ACT-03/FR-OP-02、AC-SAFE-14 子项（过严/瞬时） | P2-10 |
| CLONE 401/403/404 不重试、网络退避 ≤2、并发 2、磁盘/长路径预检 | FR-OP-01 | P2-7 |
| `job_item.attempt/expect_head`、`job.target_json`、`operation_log` 查询/导出/90 天 | AC-OP-28、§8.1 | P2-6（M2b-2） |
| 覆盖“同组件两 job 串行 + 不同组件并行” | AC-OP-29①③ | P1-3 |

### 7.3 真实环境证据评价

- **确有实测数据**（本机 `jobs.json` 8 个提交前作业，见 §1.3；`operations.jsonl` 含 CLONE/DELETE/RESTORE/SYNC 的 backupRef 与 jobId）；`docs/images/m2b/` 的 FETCH/CLONE 截图也来自真实 RTCRepos（组织 RTCRepos、14 仓、Gitea 来源）。
- **但不可复跑/不可审计**：证据不随提交入库，无环境记录（git/Gitea 版本、工作区、机器）、无原始命令/输出、无 AC 逐条断言；“取消 12/12 / 重试 12/12 / SYNC 备份 ref”仅见文档表格（`docs/m2b-status.md:28-29`）与截图，无计时/HEAD 比对等可断言证据；`ui/scripts/cdp-m2b-selfcheck.mjs` 明确注入 `__DSH_FORCE_MOCK__=true` 用 Mock 演示作业（`:9-11,123-130`），**不能**作为真实 Host 写证据。
- **判定**：真实环境“已实测”可信度中等（有旁证），但按 ADR §5“不得以 Mock/单测数量替代 AC 证据”与 M2b 出口“故障注入报告”要求，当前证据链不足 → P2-1；不将其升为 P1（M2b-1 自述候选，且本机确有真实数据）。

---

## 8. 缺陷清单（P0/P1/P2）

### 8.1 P0（1）

**P0-1 批量破坏性操作（SYNC/DELETE）绕过 §7.6 矩阵与 FR-SAFE-01/FR-OP-06 预检/强确认**
- 证据：`JobEngine.cs:630-646` 对 SYNC/CLONE/DELETE/RESTORE/CREATE/TAG 直接调用 `_batch.*`，**不进矩阵判定**（矩阵只在 `RepoActionKinds` 分支 `:630-634`）；`BatchOperationService.SyncAsync` 仅查 `IN_PROGRESS/busy`（`:107-110`）后即 stash -u + `reset --hard`（`:189-215`）；`DeleteAsync` 仅查 busy（`:351-354`）后即 `Directory.Move`（`:387-405`）——**不检查 dirty/AHEAD/stash**；`PreviewDryRun` 只有泛化文案（`JobEngine.cs:748-773`），无“未推送 commit 清单/untracked 清单/stash 条数/影响仓数”（AC-OP-16① 逐字段）；UI 确认文案还声称“脏仓/未推送仓将失败或跳过”（`workbench.ts:1996-1998`），与 Host 实际“stash+reset 覆盖”不符，且 DIVERGED 未要求输入组件 ID（AC-OP-16③）。
- 影响：§7.6 冻结 `AHEAD.SYNC=❌`（`requirements.md:359`）、`DIVERGED.SYNC=⚠ 默认阻断`（`:357`）；FR-OP-06 P0 四阻断（`reqs/requirements.md:522`）；AC-OP-15③/16/20/21 无法通过。虽 `.trash`/backup ref 使数据可恢复，但“默认阻断 + 丢失清单 + 输入组件 ID 强确认”三层保护全部缺失。
- 修复建议：SYNC/DELETE 进入共同 preflight（矩阵、dirty/AHEAD/stash、丢失清单 dry-run），DIVERGED/AHEAD 默认阻断 + typedConfirm；Host 侧二次确认参数（组件 ID）独立于 `confirm`；UI 文案与行为对齐，禁止宣称“将失败/跳过”。

### 8.2 P1（7）

| ID | 缺陷 | 证据 | 影响 |
|---|---|---|---|
| **P1-1** | job/job_item 状态字面不符合 SRS §8.1 冻结 CHECK；`JOB_UPDATED` 缺 `jobId/current`；无 `cancel_requested` | `BatchOperationService.cs:944-966`（PENDING/SUCCESS/CANCELED）；`JobDto.cs:6-21`；`JobEngine.cs:1661-1691`（DTO 用 `id/ok`，无 current/cancel_requested）；`requirements.md:393-394,1119`；AC-OP-27⑤ `ac-catalog.md:996`；并行实测 payload 键（§1.5-3） | AC-UI-10c②/AC-OP-27⑤ 按字面不可断言；未来 SQLite CHECK 迁移将冲突 |
| **P1-2** | 取消语义不符 FR-ACT-05：FETCH/COMMIT 等一律可取消；cancel/complete 竞态可把全 OK job 置 CANCELED；取消记 `E_TIMEOUT`；PENDING/未开始项被写成 CANCELED+E_TIMEOUT、无独立取消码；PENDING 取消项无错误码；取消未开始项不写审计 | `JobEngine.cs:1689`（Cancellable 与 kind 无关）、`:832-836`（CancelRequested 优先终态）、`:551-554,788-794`（E_TIMEOUT）、`:797-805`（无 code/message）、`:786-805`（无审计）；FR-ACT-05 `requirements.md:628`；并行实测（§1.5-1）12/16 未开始项全 CANCELED+E_TIMEOUT | 可能中断 COMMIT 等不可取消操作；取消报告语义错乱、未开始项状态失真 |
| **P1-3** | 并发/互斥：单 worker 全局串行 → 不同组件不能并行（AC-OP-29③/FR-OP-01 并发 2）；批量自定义 kind 不共享 `RepoActionService.RepoLocks`，可与 `runRepoAction` 在同仓并发写 | `JobEngine.cs:119-121,358-384`；`RepoActionService.cs:58-60,342-353`（单仓锁）；`BatchOperationService.cs:135,170,192,209`（直接 `_git.RunAsync`，无 RepoLocks）；`ac-catalog.md:1012` | FR-OP-10/AC-OP-29 不通过；单仓 PULL 与批量 SYNC 同仓并发存在 index/工作区竞争风险 |
| **P1-4** | 审计“每 item 恰 1 行/失败也写”不成立：预跳过项、取消未开始项、job 级内部异常 item 均 0 行；`requestId` 批量行语义与 M2a 不同；dry-run 预览行污染 operation_log | `JobEngine.cs:1052-1073`（预跳过不入执行）、`:446-465`（异常标 FAILED 无审计）、`:786-805`、`:584`（唯一审计点）；`OperationAuditService.cs:123`（result 恒输出）；`JobEngineTests.cs:847-855` | AC-OP-28/AC-OP-33 的“恰 1 行”无法通过；审计-状态不一致 |
| **P1-5** | 重启把未开始 PENDING item 标 INTERRUPTED，违反 AC-OP-26①“未开始保持待执行”；测试固化该行为 | `JobEngine.cs:848-874,1554-1561`；`JobEngineTests.cs:322-393,377-384`；`ac-catalog.md:988` | AC-OP-26 不通过；任务中心无法区分“未开始”与“中断” |
| **P1-6** | dryRun 不是真预检：不检查 COMMIT staged/CHKOUT ref/CLONE 磁盘与 URL/SYNC 丢失清单，仍返回“已预检通过”；UI SYNC/DELETE 无 dryRun 选项；AC-OP-16① 逐字段清单不存在 | `JobEngine.cs:618-628,748-773`；`workbench.ts:1991-2007`；`ac-catalog.md:908` | 破坏性操作预览失真，用户无法判断将丢失什么 |
| **P1-7** | 批量常规操作（PULL/FETCH/CHKOUT/COMMIT/PUSH）无 UI 入口（`BatchKind` 仅 6 种）；单仓五键未纳入 job（无 jobId/取消/任务中心），FR-ACT-05/06 的 M2b 全量未达成 | `ui/src/types/workbench.ts:400`；`BatchBar.vue`（仅 4 键）；`workbench.ts:2100-2117`（default 报“不支持”）；`HostCommandRouter.cs:287-368`（单仓动作不建 job）；`D-025:72-79,87-95` | AC-UI-10c 的 “批量 PULL 4 仓” Given 无法经 UI 构造；M2b 出口 FR-ACT-05/06 缺口 |

### 8.3 P2（10）

| ID | 缺陷 | 证据 |
|---|---|---|
| **P2-1** | 真实环境证据未归档/不可复跑；缺关键 E2E（真实强杀重启、DELETE 预检、SYNC AHEAD/DIVERGED、取消≤2s、dryRun 零写、事件洪泛）；CDP 脚本走 Mock | `docs/m2b-status.md:19-31,33-37`；`ui/scripts/cdp-m2b-selfcheck.mjs:9-11,123-130`；本报告 §7 |
| **P2-2** | JobStore 可靠性：无 fsync；损坏静默空表后可能被新 job 覆盖；持久化失败仅日志不可见；每 item 全量重写（与 `:520-522` 注释矛盾）；>200 非终态时内存/磁盘不一致；跨进程无锁且单实例未实现 | `JobStore.cs:67-146`；`JobEngine.cs:567-582,1483-1495,1578-1597`；全仓无单实例代码 |
| **P2-3** | 事件无节流/合并/序列号（约 2N+3 条、每条全量 items）；断连仅靠 1.5s 轮询；UI `mergeJob` 计数取 max 但 items 整体覆盖，迟到轮询可回退 item 状态 | `JobEngine.cs:1695-1719`；`WebViewBridge.cs:94`；`diagrams.md:167`；`workbench.ts:1753-1761,1894-1913` |
| **P2-4** | SYNC 失败/取消时已创建 backupRef 不落到 job_item/审计（reset 失败/OCE 路径 `BackupRef=null`）；dry-run 产生审计行；`result` 字段对所有行输出、M2a 行形状变化无兼容性断言 | `BatchOperationService.cs:209-215`；`JobEngine.cs:549-555`；`OperationAuditService.cs:123,213-226`；真实 audit 58/127 行含 result |
| **P2-5** | 全 SKIPPED（含组件不在快照）job 记 SUCCESS（ok=0）属假绿；SKIPPED 项携带 `E_NOT_FOUND` 但 job “成功” | `JobEngine.cs:839-845,1052-1073`；`JobEngineTests.cs:229-233` |
| **P2-6** | 持久化缺 SRS 列：`cancel_requested/attempt/expect_head/target_json/job_item.repo_id`；jobs.json 非 §8.1 DDL，§8.2“单写线程/事务”不满足（M2b-2 迁移） | `JobStore.cs:175-212`；`requirements.md:393-395,406-413`；`docs/m2b-status.md:41` |
| **P2-7** | CLONE 未实现 FR-OP-01 预检/并发 2/401-403-404 不重试+网络退避≤2；目标目录名推导与 AC-OP-20 `<full_name>` 措辞未对齐；无 token + baseUrl 含 userinfo 时 URL 凭据进 argv（失败开放） | `BatchOperationService.cs:230-304`（无 retry/预检）、`:263-288`；`RepoActionService.cs:882-887`；`ac-catalog.md:788` |
| **P2-8** | RESTORE 冲突仅 SKIPPED、无预览/确认；无 `refs/backup/*` 恢复入口；RESTORE 依赖本地目录名与推导名一致（重命名后找不到）；DELETE 占用失败无 DELETE_PENDING | `BatchOperationService.cs:437-442,444-510,611-666`；`ac-catalog.md:1015-1020`；`docs/m2b-status.md:42` |
| **P2-9** | >10 组件/>40 仓二次确认缺失（`JobEngine.cs:36-37` 注释称在 UI 侧，UI 无实现）；`BATCH_COMMANDS` 死代码；首次 `EnsureLoaded` 的磁盘清理在 UI 线程同步执行 | `JobEngine.cs:36-37`；`viewmodel.ts:751-760`（无引用）；`JobEngine.cs:1515-1534,1723-1738` |
| **P2-10** | FETCH/SYNC（只读网络动作）也被强制校验 **push URL** 作用域：pushurl 读不到/不匹配即 `E_CREDENTIAL_SCOPE`，真实 Host 批量 FETCH 曾因此单仓 FAILED、retry 又成功（瞬时/过严导致误失败，属 fail-closed 但可用性问题） | `RepoActionService.cs:819-868`（`PrepareCredentialEnvironmentAsync` 对 fetch+push 均 `remote get-url --push` 校验，FETCH 路径同用）；`BatchOperationService.cs:123-132`（SYNC 复用）；并行实测（§1.5-4）。非 token 泄漏，安全方向正确 |

---

## 9. Top 8（修复优先级）

1. **P0-1** SYNC/DELETE 破坏性 gate：矩阵 + dirty/AHEAD/stash 预检 + 丢失清单 dry-run + 输入组件 ID 强确认（含 AC-OP-15③ 外部改动 `OP-E002`）。
2. **P1-1** 状态枚举/契约：QUEUED/SUCCEEDED/CANCELLED、`cancel_requested`、`JOB_UPDATED {jobId,current,total,cancellable}`。
3. **P1-4** 审计完整性：预跳过/取消未开始/内部异常三路径补行，`requestId/jobId` 语义统一，dry-run 不写“已执行”语义行。
4. **P1-2** 取消语义：按 FR-ACT-05 限定可取消类型、消 cancel/complete 竞态、取消专用错误码、≤2s 断言。
5. **P1-5** 重启语义：未开始 item 保持 PENDING（AC-OP-26①），只把 RUNNING 标 INTERRUPTED。
6. **P1-3** 并发/互斥：至少“不同组件并发上限 2”或显式记录偏离；所有批量写路径共享 per-repo 锁（含 SYNC/DELETE 与单仓五键互斥）。
7. **P1-7** 批量入口与单仓 job 化：批量 PULL/FETCH/CHKOUT/COMMIT/PUSH 的 UI 入口 + 单仓五键 jobId/取消/任务中心（AC-UI-10c 可构造）。
8. **P1-6** dryRun 真预检 + UI 确认文案与 Host 行为一致（消除“将失败/跳过”错误承诺）。

> P2-1/P2-2/P2-4（证据链、持久化可靠性、审计/backupRef 关联）建议随 P1 同批修，避免 M2b-2 迁移时返工。

---

## 10. 复跑与验收建议

```powershell
# 1) 全量测试（本轮已跑，289/289）
dotnet test MbdGitTool.sln -c Release --nologo -v q

# 2) M2b-1 修复后必须新增/通过的断言（建议）
#    - SYNC: AHEAD/DIVERGED 默认阻断；DIVERGED 无 typedConfirm 零写；外部改 HEAD → E_CONCURRENT_CHANGE/OP-E002 且零改动
#    - DELETE: dirty/AHEAD/IN_PROGRESS/stash 四类默认阻断，强制删除走丢失清单 + 强确认
#    - 取消: FETCH/COMMIT 不可取消；RUNNING 取消 ≤2s；job.cancel_requested=1；未开始项状态不变
#    - 重启: 真实 force-kill → 未开始项 PENDING、进行项 INTERRUPTED、不重跑已成功项
#    - 审计: 每个 item（含预跳过/取消未开始）恰 1 行；jobId/jobItemId/backupRef/actor/requestId；无 token
#    - AC-UI-10c 事件信封: {jobId,state,current,total,cancellable}，取消后 cancellable=false
```

**签收建议**：修 P0-1 + P1-1/P1-2/P1-4/P1-5，并把 P1-3/P1-7 的真实偏离写入 `docs/m2b-status.md`（不得再写“单 worker 逐仓串行”为无代价设计），M2b-2 完成审计表/备份恢复/EMPTY/离线/journal 后另出复评。当前提交**不建议打 tag**。

---

## 11. 边界与副作用披露

- 本报告只新增 `docs/review-m2b1-code.md`；未修改源码、配置、`reqs/`、`ui/`、真实仓库，未对真实 Gitea 发起写操作。
- 全量测试使用 Release 配置，构建输出写入 `src/**/bin/Release`、`obj`（gitignored），未触碰运行中 Host（PID 45672，Debug 构建）与并行评审。
- 只读查看了 `%LOCALAPPDATA%\MbdGitTool\jobs\jobs.json`、`audit\operations.jsonl`（16:16–16:18 的提交前数据与 16:23 后并行评审数据）；未修改/清理这些文件。
