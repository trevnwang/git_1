# M2b-1 代码/安全 Gate 复评报告（提交 `9d16090`）

| 项目 | 内容 |
|---|---|
| 评审对象 | 提交 `9d16090`（`fix(m2b1): close review P0/P1 (...)`，2026-11-14 16:54:39 +0800，父提交 `3ea870e`） |
| 评审范围 | M2b-1：作业内核（job/job_item、取消、持久化/重启、审计、JOB_UPDATED、dryRun）+ 批量 SYNC/DELETE 安全门 + 批量入口/任务中心 UI 的代码与安全项；**只评审、不改代码/真实仓库** |
| 输入 | 上一轮 `docs/review-m2b1-code.md`（P0=1/P1=7）、`docs/review-m2b1-ux.md`（P0=2/P1=4）、`docs/m2b-status.md`、SRS §8.1/§9.7/§13.1.2、ADR D-025、AC 目录；被评代码 `src/MbdGitTool.Host/Services/{JobEngine,BatchOperationService,JobStore,OperationAuditService,RepoActionService}.cs`、`src/MbdGitTool.Host/{Bridge/*,MainWindow.xaml.cs}`、`src/MbdGitTool.Core/Contracts/JobDto.cs`、`tests/MbdGitTool.Host.Tests/JobEngineTests.cs`、`ui/src/{api.ts,stores/workbench.ts,viewmodel.ts,components/**}` |
| 环境 | Windows 11 / git 2.56.1 / .NET SDK 10.0.404 / 工作区 `D:\Workspace\git_1`，`HEAD=9d16090`、`git status --short` 空（本报告除外）；运行中真实 Host PID 38096（Debug，16:54:44 启动，CDP 9222，工作区 `artifacts\real-test\workspace`，4 组件/14 仓，RTCRepos） |
| 方法 | ① `git show 9d16090` 逐行静态核对（行号均指该提交）；② 全量 `dotnet test -c Release` 实跑；③ 运行中真实 Host + CDP 驱动 UI/桥做真实验证（只读 FETCH/dryRun，及被拒绝的破坏性调用；未执行会改变仓库 HEAD 的覆盖/DELETE/CLONE 写）；④ 隔离 `JobEngine` 探针复现“强杀重启”审计边界；⑤ 只读核对真实 `%LOCALAPPDATA%\MbdGitTool\{jobs\jobs.json,audit\operations.jsonl}` |
| 边界 | 未修改任何源码/配置/`reqs`/`ui` 源码；构建输出落 gitignored `bin/obj`；新增本报告与 gitignored `artifacts/m2b1-gate/` 截图；真实 Host 追加 8 个作业/42 行审计（见 §9，均为本轮实测产生）；测试工作区新增 1 个 `refs/backup/sync/...` ref（SYNC 正例证据）并已删除自建 untracked 探针文件 |

---

## 0. 结论（判决）

> **建议打 m2b-1 tag（仅代表 M2b-1 内部候选；不代表 M2b 完成）。**
> 上一轮 code P0-1 与 P1-1…P1-7、UX P0-1/P0-2/P1-1…P1-3 的**阻断项均已闭环**；本轮 **P0 = 0，P1 = 0**。全量测试 **298/298 通过（0 失败/0 跳过）**，Release 构建 **0 warning / 0 error**（`TreatWarningsAsErrors=true`），M2a 单仓行为无退化（真实 Host FETCH + 审计行形状复验）。真实 Host 上复验了取消/终态取消错误码、SYNC AHEAD 零写阻断、DELETE 脏仓阻断、强确认（缺 `confirmText` 受理前拒绝 / 错误 ID 逐仓 FAILED 目录保留）、SYNC 备份 ref 落库、dryRun PREVIEW 零写、事件新信封与 UI 展示。
>
> 但仍有 **9 项 P2 残留/契约偏差**（见 §11）：其中 **P2-R1（强杀重启后审计行原子性）** 是本轮唯一“判据字面上不完全成立”的点——常规/取消/预跳过/内部异常路径均已恰 1 行，但崩溃重启会把崩溃前已写审计的 `OK` item 再写 1 行（已用隔离探针复现）；其归口与 `docs/m2b-status.md:44` 声明的 M2b-2“写前 journal/崩溃 reconcile”一致，建议**不阻断 M2b-1 tag，但必须写入 tag 说明与 M2b-2 修复清单**。若 Gate 口径要求崩溃场景也严格“每 item 终态恰 1 行”，则 P2-R1 应升级为 P1（报告已给出复现与修法建议，供裁量）。

| 汇总口径 | 结果 |
|---|---|
| 全量测试 | **298/298 通过**（Core 71 + Host 227，0 失败/0 跳过，1m32s）；提交说明写的“297 tests”与实测 298 不一致（文案级 P3，不影响） |
| 构建 | **0 warning / 0 error**（`dotnet build -c Release`；`Directory.Build.props:8` `TreatWarningsAsErrors=true`） |
| P0 | **0**（上一轮 P0-1、UX P0-1/P0-2 全部闭环） |
| P1 | **0**（上一轮 7 个 code P1 + 4 个 UX P1 的阻断部分全部闭环，见 §10） |
| P2 | **9**（P2-R1…R9：审计崩溃原子性、事件节流、契约/AC 对齐、M2b-2 缺口与状态文档陈旧；见 §11） |
| 是否建议 tag | **建议打 `m2b-1` tag**；tag 说明须写明 M2b-2 未交付、且列出 §11 的 P2 已知项（尤其 R1/R3/R4/R8） |

---

## 1. 评审基线与可复跑说明

1. **基准是提交对象**：工作区干净、`HEAD=9d16090`，以下 `文件:行号` 均对应该提交；报告新增前仅本文件会出现在 `git status`。
2. **全量测试实跑**（本机，未改动源码；测试全部使用临时 `JobsRoot/AuditRoot`，不写真实 `%LOCALAPPDATA%`）：
   ```powershell
   dotnet test MbdGitTool.sln -c Release --nologo -v q
   # Core.Tests：已通过! - 失败: 0，通过: 71，已跳过: 0，总计: 71
   # Host.Tests：已通过! - 失败: 0，通过: 227，已跳过: 0，总计: 227（1 m 32 s）
   dotnet build MbdGitTool.sln -c Release --nologo    # 已成功生成。0 个警告 0 个错误
   ```
3. **真实环境实测**：驱动运行中 Host（PID 38096，真实快照 4 组件/14 仓，来源 RTCRepos）执行**只读**批量 FETCH/dryRun 与被拒绝的破坏性调用；新增作业/审计行见 §9，可只读复跑核对。
4. **并行声明**：评审期间无其他并行评审驱动该 Host；§9 的 8 个作业（`09:00–09:03Z`）全部由本轮 Gate 脚本产生，与作者 16:47 的自测作业可区分（作者作业为 `08:5xZ`）。
5. **隔离探针**：在 `%TEMP%\m2b1-gate-probe`（仓库外临时工程，`ProjectReference` 引用 Host/Core）复现强杀重启；不修改仓库源码，仅将构建产物写入 gitignored `src/**/bin|obj`。

---

## 2. 判据 1：P0 SYNC/DELETE 安全门（逐仓预检/矩阵、备份、强确认、四阻断、dryRun 零写）

### 2.1 Host 逐仓预检 + §7.6 矩阵（单源）

| 核对项 | 证据 | 判定 |
|---|---|---|
| SYNC 逐仓矩阵 | `BatchOperationService.cs:117-124` 先取 `NormalizeState`，`SyncMatrixAllowed`（`:1200-1245`）按 §7.6：仅 `SYNCED/BEHIND/DIRTY/DIVERGED` 允许，`AHEAD/UNMERGED/IN_PROGRESS/INVALID/MISSING/EMPTY/NO_UPSTREAM/DETACHED_*/UNKNOWN` 拒绝（SKIPPED + `E_ACTION_NOT_ALLOWED`） | **PASS** |
| SYNC DIVERGED 默认阻断 + 输入组件 ID | `:126-133` `requiresTypedConfirm = state==DIVERGED`，`MatchesTypedConfirm(confirmText, componentId)` 精确匹配/区分大小写/忽略首尾空格（`:1282-1284`） | **PASS**（单测；真实环境无 DIVERGED 夹具） |
| SYNC 外部改动 CAS | 执行前快照 HEAD vs 磁盘 HEAD（`:151-158`）；fetch 前后 HEAD 一致性（`:192-199`）；不一致→`E_CONCURRENT_CHANGE` 且不写 backup ref | **PASS**（代码路径确定，单测覆盖矩阵/零写） |
| DELETE 矩阵/忙碌/路径收口 | `:411-423` 矩阵（`DeleteMatrixAllowed` `:1248-1277`，`IN_PROGRESS/INVALID/MISSING/UNKNOWN` 拒绝）+ `IsBusyOrInProgress`；`:398-409` 只允许工作区直接子目录、拒绝 reparse；`:484-521` `.trash` 路径 canonical/`IsWithin`/reparse 校验后 `Directory.Move` | **PASS**（单测 + 真实拒绝） |
| DELETE 四阻断（dirty/untracked、AHEAD、stash、进行中） | `:431-445` `git status --porcelain` 非空→`E_DIRTY_WORKTREE`；`:451-465` `@{upstream}..HEAD` >0→拒绝、无 upstream→“无法确认已推送”拒绝；`:467-473` `refs/stash` 存在→拒绝；`:420-423` busy 拒绝；**无 force 绕过项** | **PASS**（单测 `:712-785`；真实脏仓拒绝见 §9-③） |
| DELETE 强确认（confirm + confirmText=componentId） | 受理层：`:1118-1146` 非 dryRun 时 DELETE 缺 `args.confirmText` → `E_CONFIRM_REQUIRED`（`requiresTypedConfirm=true`），且强写缺 `confirm=true` 先拒绝；逐仓层：`BatchOperationService.cs:389-396` `MatchesTypedConfirm` 失败→FAILED、目录不动 | **PASS**（单测 `:788-838`；真实缺 confirmText 受理前拒绝 + 错误 ID 逐仓 FAILED 且目录保留，见 §9-②） |
| SYNC 备份顺序 / 备份失败不 reset | `:201-223` `update-ref refs/backup/sync/<UTC>/<TYPE> <preHead>` 成功才继续；`:225-252` dirty 时 `stash -u` 成功才继续；`:254-261` 最后 `reset --hard origin/<branch>`；任一步失败即返回且不 reset。单测 `Sync_BackupRefCreationImpossible_FailsWithoutReset`（`JobEngineTests.cs:599-636`）以 unborn HEAD 证明“备份失败→HEAD 仍 unborn” | **PASS** |
| backupRef 落 job_item/审计 | 成功路径 `BatchOperationService.cs:263-267` 返回 `BackupRef`；JobEngine `:620,1058-1073` 落 item/审计。真实：`refs/backup/sync/20261114090055/IMPL` 文件内容=preHead，审计同作业行含该 `backupRef`（§9-④） | **PASS** |
| dryRun 零写 | `JobEngine.cs:692-700` dryRun 只走 `_preview`，在任何 `_batch.*`/`_actions.RunAsync` 之前返回；`BatchOperationService.PreviewAsync`（`:535-699`）只做 `status/rev-parse/rev-list` 只读探测（SYNC `:578-613`、DELETE `:615-658`、CLONE/RESTORE 预览不发网络/不移动） | **PASS**（单测 `:973-1050`；真实 HEAD/backup ref 前后一致，§9-⑤） |

### 2.2 UI 单组件限制 + 输入组件 ID 确认（读代码 + 真实 UI 实测）

| 核对项 | 证据 | 判定 |
|---|---|---|
| SYNC/DELETE 仅单组件 | `ui/src/stores/workbench.ts:2072-2106` `openDestructiveConfirm`：`componentIds.length !== 1` → toast「…仅支持单个组件…」并 return，不打开对话框；`requestBatch`（`:2266-2270`）把 SYNC/DELETE 都路由到该函数 | **PASS** |
| 输入组件 ID 强确认 | `:2080-2105` 对话框 `inputLabel:'输入组件 ID 确认'`、`initialValue:''`；提交时 `value !== componentId` → toast「确认失败：请输入完整组件 ID」，**不调用 runBatch**；`BatchDialog.vue:65-68,170-207` 提示“区分大小写，须与组件 ID 完全一致”；`:103-119` 抽屉/对话框焦点陷阱与 Esc 关闭 | **PASS** |
| 未确认 0 写 | 对话框仅在 `confirm` 点击后 `submitBatch`；`submitBatch`（`:2296-2302`）统一补 `args.confirm=true`；取消/关闭零调用 | **PASS**（真实：错误 ID 后作业数不变；见 §9-⑥） |
| 文案与 Host 行为一致 | SYNC 对话框明示“预检→备份 ref→reset”“脏仓/未推送/分叉仓将拒绝或跳过，不会整体回滚”；DELETE 明示“移到 .trash、不写远端、脏/未推送/stash 拒绝”（`:2083-2094`）——与 §2.1 的 Host 顺序/阻断一致，上一轮“将失败或跳过”与“stash+reset 覆盖”不一致已消除 | **PASS**（真实：多选 2 组件点 SYNC 被 toast 拦截、DELETE 同一分支未打开对话框；单组件弹出输入 ID 对话框；截图 `artifacts/m2b1-gate/02-sync-typed-confirm-dialog.png`、`03-delete-typed-confirm-dialog.png`） |

**本项小计：上一轮 P0-1 闭环。** 残留 P2-R6（SYNC 已建 ref 后 reset 失败时 backupRef 不落审计；dry-run 仍写 jobs.json/PREVIEW 审计行；AC-OP-16① 丢失清单仍只有计数）见 §11。

---

## 3. 判据 2：取消语义（PENDING→CANCELED/null、RUNNING 合作取消、终态错误码、实测新 job）

| 核对项 | 证据 | 判定 |
|---|---|---|
| 仅 PENDING/RUNNING 可取消，终态返回错误码 | `JobEngine.cs:266-277`：非 PENDING/RUNNING → `E_ACTION_NOT_ALLOWED`（details 含 jobId/state/cancellable=false） | **PASS** |
| PENDING 取消 | `:279-290` 置 `CancelRequested`、job=CANCELED、`CancelPendingItemsUnsafe`（`:864-873`）把 PENDING item 记 `CANCELED` 且 **`ErrorCode=null`**、message“作业取消：未开始执行”；随后 `WriteMissingItemAuditsUnsafe` 为每 item 补 1 行审计 | **PASS**（单测 `:259-332` 断言 CANCELED/null/1 行审计/重复取消=E_ACTION_NOT_ALLOWED） |
| RUNNING 合作取消 | `:291-302` `Cts.Cancel()`；`GitCli` 收到取消杀进程树（既有 M2a 能力）；worker OCE 路径 `:461-471` 把剩余 PENDING→CANCELED 后终态归并；当前 item 的 OCE 分支 `:593-600` 记 `CANCELED` + `errorCode=null`，不写 `E_TIMEOUT` | **PASS**（单测 `:335-382`：挂起 fetch 上取消→CANCELED、无 E_TIMEOUT、1 行审计） |
| 取消后终态/事件 | `FinalizeJobUnsafe:890-921` 有 CANCELED item→job=CANCELED、`CancelRequested=true`；`ToDto:1866` `Cancellable = (PENDING/RUNNING)&&!CancelRequested` | **PASS** |
| 真实新 job 实测 | 新 job `d82de6de…`（FETCH 16 项）：`runBatch accepted=true`→立即 `cancelJob` 返回 `{state:"RUNNING"}`→终态 `CANCELED`，12 项 `CANCELED` 全部 `errorCode=null` + message“作业取消：未开始执行”，4 项预跳过 `SKIPPED`（E_REPO_NOT_LOCAL/E_NOT_FOUND），`cancellable=false`、`cancelRequested=true`；终态再取消返回 `E_ACTION_NOT_ALLOWED`；审计 16 行=16 item（§9-①） | **PASS** |
| UI | 任务中心取消项展示「已取消（未开始）」、无 `E_TIMEOUT`；历史行 12 已取消/4 跳过；UI 对 `E_ACTION_NOT_ALLOWED` 有专门提示（`workbench.ts:2413-2414`） | **PASS**（截图 `artifacts/m2b1-gate/05-task-center-audit-jobs.png`） |

**本项小计：上一轮 P1-2（code）/P0-1（UX）闭环。** 按操作类型的取消可用性（FR-ACT-05/AC-OP-27⑥：FETCH 短任务不提供取消、COMMIT 不可取消）仍未实现 → P2-R5（本 Gate 清单未列为阻断）。

---

## 4. 判据 3：重启恢复（RUNNING→INTERRUPTED、PENDING→CANCELED、不重复执行）

| 核对项 | 证据 | 判定 |
|---|---|---|
| 加载收敛 | `JobEngine.cs:1706-1747`：`job.State∈{PENDING,RUNNING}` 或存在 PENDING/RUNNING item → `MarkInterruptedUnsafe`（`:940-970`）——RUNNING item→`INTERRUPTED`，PENDING item→`CANCELED`（`errorCode=null`，message=进程重启未执行）；job 曾有 RUNNING→`INTERRUPTED`，纯排队→`CANCELED`+`cancelRequested=true`；**加载只收敛、绝不重新入队**（无 Enqueue 调用） | **PASS** |
| 不重复执行 | 单测 `Restart_OnlyRunningBecomesInterrupted_PendingBecomesCanceled_WithoutReexecuting`（`JobEngineTests.cs:387-496`）：断言状态、审计 3 行、二次加载不重写审计、磁盘回写收敛状态 | **PASS** |
| 优雅关闭兜底 | `Dispose:1951-2001`：非终态 job 收敛 + 补审计 + 落盘；`ExecuteJobAsync:416-429` 对已被取消/已释放的 PENDING job 不执行 | **PASS** |
| 隔离引擎探针（模拟强杀留下的 jobs.json） | 作业 `RUNNING`（item1 `OK` 且崩溃前已写 1 行审计、item2 `RUNNING`）→ 新建引擎加载：job=`INTERRUPTED`、item1 保持 `OK`、item2=`INTERRUPTED`；**但审计行变为 item1 2 行（OK,OK）** → 见 P2-R1 | **PASS（状态语义）/ P2（审计原子性）** |
| 未开始项语义与 AC 字面 | AC-OP-26① 字面为“未开始 1 行**保持待执行**（PENDING）”；本实现按本轮 Gate 口径记 `CANCELED`（不误标 INTERRUPTED、不重复执行，且 UI 区分“已取消（未开始）”） | **按 Gate 口径 PASS**；AC 字面差异 → P2-R4 |

**本项小计：上一轮 P1-5 的阻断诉求（不得误标 INTERRUPTED/不得重复执行/不得固化错误语义）闭环；AC-OP-26① 的“保持待执行”字面口径待回写/裁决（P2-R4）。**

---

## 5. 判据 4：审计（每 item 终态恰 1 行含 jobId/jobItemId/backupRef/result；无 token）

| 核对项 | 证据 | 判定 |
|---|---|---|
| 每 item 恰 1 行（正常/失败/跳过） | `JobEngine.cs:1042-1080` 唯一写入点；`AuditWritten` 先置位再写（`:1049-1055`），失败不重写；`WriteMissingItemAuditsUnsafe:1017-1040` 覆盖预跳过/取消未开始/内部异常/重启改写路径；`FinalizeJobUnsafe` 末尾统一补漏 | **PASS** |
| 字段 | 行含 `ts/action/componentId/repoType/state/preHead/postHead/ok/errorCode/durationMs/actor/requestId/jobId/jobItemId/result/backupRef`（`OperationAuditService.cs:89-156,175-227`）；`jobId/jobItemId` 真实关联（`:1058-1073`） | **PASS** |
| 无 token | 审计只写元数据，不写 output/stderr（`OperationAuditService.cs:16-21`）；真实审计尾部扫描无 `ghp_/Bearer/Authorization`（§9） | **PASS** |
| 真实验证 | 8 个新作业逐条：16/16（取消）、16/16（dryRun FETCH）、4/4（dryRun SYNC）、1/1（DELETE 强确认失败）、1/1（SYNC 成功含 backupRef）、1/1（AHEAD 跳过）、1/1（DELETE 脏仓失败）、1/1（dryRun SYNC）——**每个 jobItemId 恰 1 行**（§9） | **PASS** |
| 崩溃重启原子性 | 隔离探针：崩溃前已写审计的 `OK` item 在重启补写时**再写 1 行**（`job-crash:1` rows=2 `[OK,OK]`）；反方向时间窗（状态先落盘、审计未落盘即崩溃）当前靠重启补写正确处理，但若补齐范围收窄会漏行。根因：`AuditWritten` 只在内存，`StoredJobItem`（`JobStore.cs:199-203`）未持久化该标记，`LoadUnsafe:1731` 对**所有**终态 item 调 `WriteMissingItemAuditsUnsafe` | **P2-R1**（M2b-2 journal/reconcile/审计表事务归口；若 Gate 要求崩溃场景也严格恰 1 行则升级 P1） |

**本项小计：上一轮 P1-4 的“缺行”三条路径闭环（单测 + 真实 Host 均 1 item=1 行）；崩溃/强杀原子性留 P2-R1。** 另：`requestId` 批量行=作业级 GUID、M2a 行=桥请求 ID 的语义差异仍在（旧 P2-4），属 M2b-2 审计表归口。

---

## 6. 判据 5：事件契约、UI 兼容、无洪泛/线程错误

| 核对项 | 证据 | 判定 |
|---|---|---|
| 固定信封 | `JobDto.cs:72-79` `JobUpdatedEventDto(Event, JobId, Current, Data, State, Total, Cancellable)`；`Publish:1914-1921` 填 `event:"JOB_UPDATED"`、`jobId=dto.Id`、`current=data=dto`、`state/total/cancellable`；`WebViewBridge` 用 `JsonSerializerDefaults.Web`（camelCase） | **PASS** |
| 单测 | `JobEngineTests.cs:81-118` 逐键断言（event/jobId/current.id/data.id/state/total/cancellable）；`:110-111` 含终态 SUCCESS；`:112-113` 非 dryRun | **PASS** |
| 真实事件 | 取消作业捕获 4 条 `JOB_UPDATED`，键=`[cancellable,current,data,event,jobId,state,total]`，`current.id==data.id==jobId`，终态 `state=CANCELED` 且 `cancellable=false`；页面 0 console error | **PASS** |
| 线程安全 | 后台 worker→`MainWindow.xaml.cs:74-90` `Dispatcher.HasShutdownStarted` 检查 + `Dispatcher.BeginInvoke`→`WebViewBridge.PostEvent`；序列化/超长/桥销毁只告警丢弃（`WebViewBridge.cs:77-103`） | **PASS** |
| UI 兼容（新旧形状/部分快照） | `api.ts` `normalizeJobUpdatedEvent`：优先 `current`，兼容旧 `{event,data}`，无完整 items 时合成 `partial=true`；`workbench.ts:1765-1784` `mergeJob` 对 partial 保留旧 items/kind；`:1838-1856` 按 id 合并；`CANCELLED→CANCELED` 兼容（`api.ts` `normalizeJobState`）；P0-2 根因 `syncCurrentJobSegment` 已移除，改为 `noteNewActiveJobs` 只加未读角标（`:1862-1867`） | **PASS**（真实任务中心显示新 job；作者 16:47 自测 68/68 亦含 event.old-data-envelope 项） |
| 无洪泛/节流 | **未加节流**：每次状态/进度都 Publish 全量 items（`ExecuteItemAsync:550,634`、`ExecuteJobAsync:438`、`Enqueue:176`、`FinalizeJob:883`）≈`2N+3` 条；真实 16 项 dryRun 实测 **35 条（=2N+3）/357,462 字节（单条 max 12,438B）**；16 项取消 4 条/单条 max 9,657B。按 800 项上限外推约 1,603 条、天然 O(N²) 字节量级，UI 主线程逐条 parse/map/sort | **PASS（无失控/无重复循环/无线程错误）；洪泛风险残留 P2-R2**（架构约束 `reqs/v0.2/diagrams.md:167` ≤10Hz/UI 100ms 节流未落实，属上轮 P2-3 未清） |

---

## 7. 判据 6：dryRun 语义（PREVIEW 终态/item.preview/message；不是 SUCCESS）

| 核对项 | 证据 | 判定 |
|---|---|---|
| job 终态 PREVIEW | `FinalizeJobUnsafe:895-898`：`args.dryRun` → `job.State=PREVIEW`（优先于 SUCCESS/PARTIAL/FAILED 归并）；`JobMessage:1874-1879` 输出“预览（DRY_RUN）：仅输出逐仓预检结果，未执行、未写入任何仓库/ref（N 项）” | **PASS** |
| item 状态/标记/message | `BatchOperationService.PreviewResult:791-792` 返回 `PREVIEW` + 只读预检 message；`JobEngine.cs:621` `item.Preview = state==PREVIEW`；`JobDto`/`JobItemDto` 增加 `DryRun/Preview/Message/CancelRequested` 字段并持久化（`JobStore.cs:171-203`） | **PASS** |
| 不是 SUCCESS | 单测 `DryRun_Sync_IsPreviewOnly_ZeroWrite_AndJobStatePreview`（`:973-1019`）、`DryRun_Delete_IsPreviewOnly_DirectoryRetainedAndNoTrash`（`:1022-1050`）断言 `PREVIEW`、`dryRun=true`、`preview=true`、message 含 DRY_RUN；真实：`944d1109`（SYNC 4 项）、`b4ff58c7`（SYNC 1 项）、`5b3b76ae`（FETCH 16 项）均 `state=PREVIEW`、item 全 `PREVIEW`+`preview=true`，UI 显示“预览（dryRun）· 预览完成 · 未执行写操作” | **PASS** |
| 真实零写 | dryRun SYNC 前后同仓 HEAD 不变（`88a9539… → 88a9539…`）、`.git/refs/backup` 前后均 0 项；隔离单测另断言 backup ref/stash 无变化 | **PASS** |
| 预览质量 | `PreviewAsync` 已按 kind 探测矩阵/未克隆/unborn/upstream/dirty/untracked/unpushed/stash/.trash 目标/凭据；但 AC-OP-16① 的“未推送 commit 列表（短 SHA/作者/message）、untracked 路径清单、stash 条数、影响仓数”仍只有计数 | **PASS（本 Gate 口径）/ P2-R6（AC 明细）** |

---

## 8. 判据 7：回归（298/298、0 warning、M2a 不退化）

| 核对项 | 证据 | 判定 |
|---|---|---|
| 全量测试 | `dotnet test -c Release`：Core 71/71 + Host 227/227 = **298/298**，0 失败/0 跳过；测试隔离 `%TEMP%` 的 jobs/audit，未污染真实数据 | **PASS** |
| 0 warning | `dotnet build -c Release`：**0 个警告 0 个错误**（`TreatWarningsAsErrors=true` 在 `Directory.Build.props:8` 全局生效） | **PASS** |
| M2a 单仓不退化 | 全量 Host 测试含 `RepoAction*`/`HostCommandRouter`/`OperationAudit*`/矩阵夹具等 M2a 套件且全绿；真实 Host `runRepoAction(FETCH, RTCT_Synced/IMPL)` 返回 `accepted=true`、`E_*` 为空、pre/post HEAD 一致；审计产生 **M2a 行形状**（无 jobId，含 requestId/actor/result）（§9-⑦） | **PASS** |
| 既有 M2a 安全项 | 未改 Matrix/凭据/路径/确认条主路径；`RepoActionService` 仅把 per-repo 锁抽成 `AcquireRepoLockAsync` 并复用（`:98-127`、`:368-380`），语义不变（单测 `RepoLock_IsSharedAndSerializesSameRepository:1097-1118`） | **PASS** |

---

## 9. 真实环境实测记录（本轮独立复跑，2026-11-14 17:00–17:04 +08 / 09:00–09:03Z）

真实 Host：PID 38096，Debug，CDP 9222；工作区 `D:\Workspace\git_1\artifacts\real-test\workspace`（4 组件/14 仓，RTCRepos）。全部真实写操作仅限**只读 FETCH/dryRun 与被 Host 安全门拒绝的破坏性调用**；唯一成功的 SYNC 落在 `SYNCED` 仓（reset 为 no-op，仅新增备份 ref 作为正例证据）；未执行会改变 HEAD 的 SYNC 覆盖/DELETE/CLONE/CREATE/TAG 写。

| # | 作业（前 8 位） | 场景/操作 | 结果（job/item 明细） | 审计行 |
|---|---|---|---|---|
| ① | `d82de6de` | FETCH 4 组件 16 项，RUNNING 时 cancel | `CANCELED`；12×CANCELED（`errorCode=null`，message“作业取消：未开始执行”）、4×SKIPPED；`cancellable=false`、`cancelRequested=true`；终态再 cancel→`E_ACTION_NOT_ALLOWED` | 16 行=16 item（12 CANCELED + 4 SKIPPED） |
| ② | `8768ff71` | DELETE `RTCT_Synced/IMPL`，`confirm=true` + 错误 `confirmText` | `FAILED`；item `FAILED/E_CONFIRM_REQUIRED`；目录保留（HEAD 前后 `88a9539…` 不变）；同组件缺 `confirmText` 的调用在**受理前**被 `E_CONFIRM_REQUIRED` 拒绝且不产生 job | 1 行（FAILED） |
| ③ | `eb3259ad` | DELETE，工作区写入 untracked 探针文件 | `FAILED`；item `E_DIRTY_WORKTREE`“工作区存在未提交改动或未跟踪文件”；目录保留；探针文件已删除 | 1 行（FAILED） |
| ④ | `e4b91d33`/`94360f27` | SYNC `RTCT_Demo/IMPL`（SYNCED，正例）/ `RTCT_Demo/SIL`（AHEAD，负例） | 正例 `SUCCESS`，backupRef=`refs/backup/sync/20261114090055/IMPL`（文件内容=preHead `85dced5…`，`git show-ref` 可见）；负例 `SKIPPED/E_ACTION_NOT_ALLOWED`“AHEAD…SYNC 会丢弃”，HEAD 前后 `e67eb05…` 不变、`.git/refs/backup` 前后 0→0 | 1 行 OK（含 backupRef）+ 1 行 SKIPPED |
| ⑤ | `944d1109`/`b4ff58c7`/`5b3b76ae` | dryRun：SYNC（4/1 项）、FETCH（16 项） | job/item 全 `PREVIEW`、`dryRun=true`、`preview=true`、message 含 `DRY_RUN`；真实工作区 HEAD 与 `.git/refs/backup` 前后不变 | 4+1+16 行，全 PREVIEW |
| ⑥ | UI | 多选 2 组件点 SYNC/DELETE | SYNC 实测 toast「…仅支持单个组件…」且不打开对话框（DELETE 走同一 `openDestructiveConfirm` 分支，同样不打开）；单组件弹出“输入组件 ID 确认”对话框；输入错误 ID 点确认→toast「确认失败：请输入完整组件 ID「RTCT_Demo」」，作业数前后不变（27→27）；DELETE 对话框同样要求组件 ID（未提交） | — |
| ⑦ | M2a | 单仓 `runRepoAction(FETCH, RTCT_Synced/IMPL)` | `accepted=true`、pre/post HEAD 一致、无错误码；审计为 M2a 行形状（无 jobId、含 requestId/actor/result=OK） | 1 行 |
| ⑧ | 事件 | ①与⑤期间捕获 `JOB_UPDATED` | 键固定 `[cancellable,current,data,event,jobId,state,total]`；`current.id=data.id=jobId`；终态取消 `cancellable=false`；16 项 dryRun 共 35 条、357,462B（max 12,438B）；页面 0 console error | — |

审计汇总：真实 `operations.jsonl` 由 223 行增至 **265 行**（本轮新增 42 行=8 作业 41 行 + M2a 1 行）；逐 `jobItemId` 去重后与 item 数一一对应（见 §9 表）；尾部无 token/Authorization 命中。
截图（gitignored）：`artifacts/m2b1-gate/02-sync-typed-confirm-dialog.png`、`03-delete-typed-confirm-dialog.png`、`04-task-center-history.png`、`05-task-center-audit-jobs.png`。

**隔离强杀重启探针**（`%TEMP%\m2b1-gate-probe`，宿主外临时工程；仅证明“状态语义 + 审计边界”）：
```
jobs.json: job-crash(FETCH, RUNNING) items=[IMPL:OK(崩溃前已写1行审计), SIL:RUNNING]
加载后:   jobState=INTERRUPTED, IMPL=OK, SIL=INTERRUPTED
审计:     job-crash:1 rows=2 [OK,OK]  ← P2-R1（重复）
          job-crash:2 rows=1 [INTERRUPTED]
```

---

## 10. 上一轮 P0/P1 闭环矩阵

### 10.1 代码评审（`docs/review-m2b1-code.md`）

| 上轮 ID | 缺陷 | 本轮判定 | 依据 |
|---|---|---|---|
| **P0-1** | SYNC/DELETE 绕过 §7.6 矩阵与预检/强确认 | **已闭环** | §2：矩阵/typedConfirm/dirty/AHEAD/stash/备份顺序/零写全部实现并（真实/单测）验证；残留 P2-R6 |
| P1-1 | 状态字面/`JOB_UPDATED` 缺 `jobId/current`/无 `cancel_requested` | **阻断部分已闭环**：事件信封+`cancel_requested`+`DryRun/Preview/Message` 已交付；状态字面（PENDING/SUCCESS/CANCELED + 新 PREVIEW）仍与 SRS §8.1 不同 → **P2-R3** | `JobDto.cs`、`JobEngine.cs:1837-1871,1896-1928`、单测 `:81-118` |
| P1-2 | 取消语义（E_TIMEOUT/竞态/未开始项失真/审计缺行） | **已闭环**（本 Gate 判据）；按 kind 取消可用性未实现 → **P2-R5** | §3；单测 `:259-382`；真实 `d82de6de` |
| P1-3 | 并发/互斥 | **互斥已闭环**（per-repo 锁共享，兼容单仓 M2a）；全局单 worker/并发 2 未实现 → **P2-R7** | `RepoActionService.cs:98-127`；`JobEngine.cs:563-581` |
| P1-4 | 审计每 item 恰 1 行（缺行三路径） | **已闭环**：预跳过/取消未开始/内部异常/重启改写均补行，真实 16/16、1/1 等 | §5；`JobEngine.cs:1017-1080` |
| P1-5 | 重启把 PENDING item 标 INTERRUPTED | **已闭环**（改为 CANCELED + null error + 不重复执行）；AC-OP-26①“保持待执行”字面差异 → **P2-R4** | §4；单测 `:387-496` |
| P1-6 | dryRun 非真预检 | **阻断部分已闭环**：PREVIEW/只读探测/零写；AC-OP-16① 明细清单仍有缺口 → **P2-R6** | §7；`BatchOperationService.cs:535-699` |
| P1-7 | 批量常规入口缺失/单仓 job 化 | **批量入口已闭环**（UI 11 kinds 一致，COMMIT/PUSH 显式标注 M2b-2 禁用）；单仓 job 化仍属 M2b-2（M2b 整体不完整，tag 只代表 M2b-1） | `viewmodel.ts:772-857`、`BatchBar.vue:76-125`、`MenuBar.vue:89-105` |

### 10.2 UX 评审（`docs/review-m2b1-ux.md`）

| 上轮 ID | 缺陷 | 本轮判定 | 依据 |
|---|---|---|---|
| P0-1 | 取消未开始项 = `CANCELED + E_TIMEOUT` | **已闭环** | 单测 + 真实 `d82de6de`：CANCELED/null，UI“已取消（未开始）”、无 E_TIMEOUT |
| P0-2 | 任务中心分段被旧 `currentJobId` 劫持 | **已闭环** | `syncCurrentJobSegment` 已移除；`applyJobs/applyJobUpdate` 只 `noteNewActiveJobs` 加未读角标，显式 `focusJob` 才切段（`workbench.ts:1833-1834,1862-1877`）；作者 16:47 自测 `p0-2.*` 项通过 |
| P1-1 | dryRun 显示「✓ 成功 0/16」 | **已闭环** | PREVIEW 状态/徽标/“未执行写操作”/说明文案；真实 UI 行 `data-state=PREVIEW,data-dryrun=1` |
| P1-2 | 批量动作清单三处不一致、5 种无入口 | **已闭环** | `BATCH_ACTIONS` 单一清单 + 三入口共用；PULL/FETCH/CHKOUT/TAG/RESTORE 可用，COMMIT/PUSH 显式禁用并标“M2b-2 未接入” |
| P1-3 | 选择模式 `0/N` 作用目标不一致 | **已闭环** | `batchTargetComponentIds` 只取勾选集合；0 勾选禁用+hint（`:1999-2015,2255-2265`） |
| P1-4 | M2b-2 六项能力缺口 | **仍开放（已知，非 M2b-1 阻断）** | `docs/m2b-status.md:39-46`；见 P2-R9 |

> 说明：作者 16:47 的自测 `artifacts/m2b1-fix-selfcheck/result.json` 共 68/68（含 `p0-2.segment-not-hijacked`/`p0-2.unread-hint`/`p0-2.active-visible`/`p0-2.progress-from-current`、`event.*`、`real.cancel-*`、`preview.*`）；本报告对其中状态/事件/取消/预览/强确认关键项做了独立复跑（§9）。

---

## 11. 本轮残留 P2（不阻断 M2b-1 tag；建议写入 tag 说明/M2b-2 清单）

| ID | 问题 | 证据/影响 | 建议 |
|---|---|---|---|
| **P2-R1** | 强杀/崩溃重启后审计不是严格“每 item 恰 1 行”：崩溃前已写审计的 `OK` item 会在加载补写时**重复 1 行**；另一时间窗（状态已落盘、审计未落盘）依赖补写，若收窄会漏行 | 隔离探针 `job-crash:1 rows=2 [OK,OK]`；根因 `AuditWritten` 仅内存、`StoredJobItem` 未持久化、`LoadUnsafe:1731` 对全部终态 item 补写（`JobEngine.cs:1017-1040,1706-1747`；`JobStore.cs:199-203`） | M2b-2：持久化 `auditWritten`/幂等键，或审计表唯一约束（`(job_id,job_item_id,result)`）+ journal/reconcile；若 Gate 要求崩溃场景同样严格则升 P1 并先修（改动小） |
| **P2-R2** | 事件无节流/合并，全量 items 快照 `2N+3` 条且总量 O(N²) | 真实 16 项=35 条/357KB；800 项上限外推 ~1,603 条；`JobEngine.cs:550,634,438,176,883,1896-1928`；UI 主线程逐条处理（`workbench.ts:1838-1856`） | M2b-2 或 M2b-1 后补：合并/100ms 节流/增量 item 事件；对齐 `diagrams.md:167` |
| **P2-R3** | 状态字面与 SRS §8.1 冻结 CHECK 不一致：实现 `PENDING/SUCCESS/CANCELED`（并新增 `PREVIEW`），SRS/AC 为 `QUEUED/SUCCEEDED/CANCELLED`（`requirements.md:393-394`、`:1119`）；`JobDto.cs:5` 注释“与 SRS 枚举同名”不成立（UI 已双向兼容 `CANCELLED→CANCELED`，`api.ts`） | 契约/AC 字面断言不可直接复用；M2b-2 SQLite CHECK 前需裁决 | 回写 SRS/AC 或改枚举（二选一）；在 `docs/m2b-status.md` 记为契约偏差 |
| **P2-R4** | AC-OP-26① 字面“未开始 1 行保持待执行（PENDING）” vs 实现“PENDING→CANCELED”；AC-OP-26③“继续/重试失败项入口”尚无 resume | 单测/探针；`ac-catalog.md:988` | 回写 AC 或 M2b-2 提供 resume；本轮 Gate 口径已按 CANCELED 通过 |
| **P2-R5** | FR-ACT-05/AC-OP-27⑥ 按操作类型的取消可用性未实现：`Cancellable` 与 kind 无关（`JobEngine.cs:1866`），FETCH 短任务/COMMIT 不可取消规则缺失 | `requirements.md:628`、`ac-catalog.md:996` | M2b-2/AC 对齐：按 kind 禁用取消并给提示 |
| **P2-R6** | ① SYNC 已建 backup ref 后 reset/stash 失败 → 返回结果与审计 `backupRef=null`（ref 失关联，旧 P2-4）；② dryRun 仍写 `jobs.json` + `PREVIEW` 审计行（AC-OP-16②“未确认前写入操作数=0”口径需澄清）；③ AC-OP-16① 丢失清单仅计数（无 commit 短 SHA/作者/message、untracked 路径、stash 条数、影响仓数） | `BatchOperationService.cs:209-215,254-261`；`JobEngine.cs:596-600` | M2b-2 backupRef 回填/审计口径/Git 明细预检 |
| **P2-R7** | 仍单 worker 全局串行（不同组件并行/并发 2 未实现，AC-OP-29③/FR-OP-01）；本轮仅补 per-repo 锁共享 | `JobEngine.cs:126-128,383-409` | 至少在 `docs/m2b-status.md` 登记偏离；并发留 M2b-2 |
| **P2-R8** | `docs/m2b-status.md` 未随 `9d16090` 更新：仍写“7 态”“PENDING/RUNNING→INTERRUPTED”“事件 `{event,data}`”“BatchBar 4 键”“289/289” | `docs/m2b-status.md:10-16,35,43` | tag 前更新状态文档（8 态含 PREVIEW、新事件、11 kinds、298/298、P1 闭环与 P2 残留） |
| **P2-R9** | M2b 整体未交付（tag 只代表 M2b-1）：审计完整表/查询导出/90 天、backup ref 恢复、EMPTY 首推、离线降级、journal/reconcile、单仓 job 化、DELETE_PENDING 等 | `docs/m2b-status.md:39-46`；ADR D-025 §2.3 | 不得宣称 M2b 完成；M2b-2 另出复评 |

**测试缺口（不单列 ID）**：未执行真实“强杀 Host→重启”E2E（避免中断运行中 Host，已用隔离 JobEngine 探针替代）；真实环境无 DIVERGED 夹具（DIVERGED typedConfirm 仅单测）；stash 阻断仅单测。这些在上一轮已列为 P2-1，本轮维持。

---

## 12. 复跑命令（供后续 Gate 复核）

```powershell
# 1) 全量测试与 0 warning
dotnet test MbdGitTool.sln -c Release --nologo -v q
dotnet build MbdGitTool.sln -c Release --nologo

# 2) 真实 Host（需先启动 Host 并开启 CDP 9222）
#    - 提交内自测脚本：node ui/scripts/cdp-m2b1-fix-selfcheck.mjs（Mock + 真实 Host 两段）
#    - 本报告 §9 的临时 CDP 探针（只读 Host 行为 + UI 强确认/事件/任务中心）位于 %TEMP%\m2b1-gate-probe
#    - 只读核对真实作业/审计：
$j = Get-Content "$env:LOCALAPPDATA\MbdGitTool\jobs\jobs.json" -Raw | ConvertFrom-Json
$j.jobs | Where-Object { $_.id -in @('d82de6de3b22478dbc49684c5535220e','e4b91d33076e4456bc89087cf776bd24') } | Format-List
Select-String -Path "$env:LOCALAPPDATA\MbdGitTool\audit\operations.jsonl" -Pattern 'd82de6de3b22478dbc49684c5535220e'

# 3) 崩溃重启审计边界复现（隔离探针，仓库外）：构造 jobs.json（RUNNING + 已 OK item 预置 1 行审计）→ 新建 JobEngine 加载 → 统计 jobItemId 行数
```

---

## 13. 边界与副作用披露

- 本报告只新增 `docs/review-m2b1-gate-code.md`；未修改源码、配置、`reqs/`、`ui/` 源码，未改真实用户仓库。
- 全量测试使用 Release，构建输出写 `src/**/bin|obj`（gitignored）；测试使用临时 jobs/audit，不写真实 `%LOCALAPPDATA%`。
- 真实 Host 实测（§9）：追加 8 个作业与 42 行审计（jobs 25→33；audit 223→265），均为本轮 Gate 脚本产生、append-only；在真实测试工作区 `artifacts\real-test\workspace\RTCT_Demo_Impl` 新增 1 个 `refs/backup/sync/20261114090055/IMPL`（SYNC 正例证据，保留）；曾在 `RTCT_Synced_Impl` 创建并已删除 1 个 untracked 探针文件；**未执行会改变 HEAD 的 SYNC 覆盖、也未执行 DELETE/CLONE/CREATE/TAG 写**（唯一成功 SYNC 落在 `SYNCED` 仓，reset 为 no-op，仅新增上述备份 ref）。
- 隔离探针工程位于 `%TEMP%\m2b1-gate-probe`（仓库外），通过 `ProjectReference` 复用 Host/Core 构建产物；截图目录 `artifacts/m2b1-gate/` 为 gitignored。
- 评审结束时 `git status --short` 仅显示本报告未跟踪，其余为空、`HEAD=9d16090`；运行中 Host（PID 38096）未被终止/重启。
