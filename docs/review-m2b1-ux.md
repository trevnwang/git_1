# M2b-1 任务中心 / 批量入口 · UX 与需求对抗评审（对象 `3ea870e`）

> 评审对象：`3ea870e feat(m2b): job engine, batch operations and task center (M2b-1)`
> 评审类型：**对抗式 UX / 需求符合度评审**。只评审、不改代码、不改真实仓库：未修改任何源码/配置；本工作区新增文件仅本报告与并行代码评审文档，实测脚本/截图均在 gitignore 的 `artifacts/` 下。
> 输入：`docs/m2b-status.md`、SRS §9.16/§9.17/§11/§13（AC-UI-10c、AC-OP、AC-SAFE、AC-REPO、AC-UI-13）、ADR `D-025` §2.3/§2.4/§2.5/§4、前端 `DrawerHost.vue`/`BatchDialog.vue`/`BatchBar.vue`/`stores/workbench.ts`/`api.ts`。
> 实测环境（2026-11-14 16:16–16:30，Asia/Shanghai）：真实 Host（CDP `127.0.0.1:9222`，WebView2/Edge 157）+ 真实 RTCRepos（工作区 `D:\Workspace\git_1\artifacts\real-test\workspace`，4 组件 / 14 仓）+ 真实 Gitea（`MBD_GITEA_ALLOW_INSECURE_HTTP=true`）。
> 证据方式：CDP 真实鼠标/键盘事件 + DOM/bbox 断言 + 页面级故障注入（`window.__DSH_TEST_BRIDGE__`/`__DSH_FORCE_MOCK__`，仅运行期钩子，不落盘）+ `%LOCALAPPDATA%\MbdGitTool\jobs\jobs.json` 原始快照 + 官方 selfcheck 脚本复跑。真实写操作仅限 `dryRun` 与只读 `FETCH`；未对真实仓库做 SYNC/DELETE 等破坏性写。

## 0. 结论（先给答案）

| 问题 | 结论 |
|---|---|
| **M2b-1 可 tag / 需先修？** | **需先修（P0×2），修后可 tag M2b-1（内部候选）**。P0-1（取消语义/错误码）与 P0-2（任务中心分段被旧 `currentJobId` 劫持）都落在本批新增的“任务中心 + 取消”主链路上，且 P0-1 直接使 **AC-UI-10c ① 不可判通过**；建议同批修 P1-1（dryRun 伪装成功）与 P1-2（批量入口清单）。 |
| **M2b 是否完整？** | **仍不完整，且本批不应对外宣称 M2b/M2 完成**。ADR D-025 §2.3 的 7 项 M2b 交付中，第 3/4/5/6/7 项（备份 ref 恢复、审计完整表、journal/reconcile、EMPTY 首推、离线降级、backup ref 保留清理）仍缺或仅部分；`AC-OP-01…32`、`AC-REPO-01…11`、`AC-SAFE-01…13` 未提供逐 AC 证据与故障注入报告；M2b 出口 = 本报告修复项 ∧ 上述 AC 全绿。 |
| 缺陷计数 | **P0 = 2，P1 = 4，P2 = 5**（Top 8 见 §7） |

---

## 1. 任务中心（FR-OP-08、AC-UI-10c、§11.6-2、§11.0.4-5）

### 1.1 实测矩阵

真实任务历史：评审开始时 `getJobs` 返回 8 条真实 Host 作业（含 `cancel 12 项` 与 `retry 12/12` 两条），评审期间又产生真实 FETCH/CREATE-dryRun 作业；重启页面后历史 15 条持久可见。

| # | 检查项 | 结果 | 实测证据 |
|---|---|---|---|
| 1 | 入口可达（菜单「工具→任务中心」/标题区 `title-tasks`/状态栏「队列」） | ✅ | 标题区按钮点击后 `[data-testid=task-center]` 出现；状态栏「队列 N」可点开 |
| 2 | 进行中 / 历史分段 | ✅ | 分段计数 `进行中 0 · 历史 8` → 提交真实作业后 `进行中 1 · 历史 8`；两段独立计数 |
| 3 | 逐 job 行字段（jobId/ok/total/state/耗时） | ✅ | `批量 FETCH ✓成功 23197d69… 12/12 结束 16:18:28 · 耗时 10.0s`；`批量 SYNC ✓成功 bc155ace… 4/4`；`⊘已取消 dd46d919… 0/12`；jobId 完整 32 hex（视觉 110px 省略，title 全量） |
| 4 | 展开逐仓结果 | ✅ | 12 项 item 全部渲染：状态 icon+文案、`errorCode · message`、`preHead → postHead`、`backupRef`、起止时间；CLONE/SYNC/FETCH 均验证 |
| 5 | `backupRef` 展示 | ✅ | SYNC 4 项显示 `refs/backup/sync/20261114081755/DESIGN` … `181758/SIL`，并有“备份 ref（保留 30 天）”title |
| 6 | 取消按钮（进行中） | ✅（语义见 P0-1） | 真实 FETCH 16 仓：`进行中` 行出现「取消」；点击后 245ms 内 `CANCELED`，取消按钮消失，状态栏队列 1→0，Toast「已请求取消 …（合作式；终态以实际扫描为准）」 |
| 7 | 仅重试失败项 | ✅ | 取消作业行出现「仅重试失败项（7）」；点击后新 job `ee0b31…` 仅 7 项，最终 `SUCCESS 7/7`；成功 5 项与跳过 4 项未重跑（不重跑成功仓 = AC-UI-10c ④ 通过） |
| 8 | 刷新按钮 | ✅ | 「刷新」触发 getJobs；`更新于 0s 前` 正常；失败注入后「重试」可恢复 |
| 9 | 事件驱动（JOB_UPDATED 自动更新） | ✅（字段见 P2-2） | 真实 FETCH 期间 5.6s 收到 13 条 JOB_UPDATED；不点刷新，行进度自动 `0/16→1/16→3/16→4/16`，标题 `进行中 1`、状态栏队列同步；取消后事件 `cancellable=false` 再 `state=CANCELED`；终态后桥调用序列含 `refresh/getSnapshot/getRepoDetail`（≤2s 回写） |
| 10 | 空态（进行中 / 历史） | ✅ | 进行中：`◷ 暂无进行中的任务`（描述写“批量 SYNC / CREATE / Clone / Delete 提交后会在此显示…”，未含 TAG/RESTORE，见 P2-4）；历史：注入空列表后 `◷ 暂无历史任务` 正常渲染 |
| 11 | 错误态 | ✅（呈现方式见 P2-3） | 注入 `getJobs` 故障：`任务接口不可用 / E_INTERNAL：注入故障：任务接口不可用 / 重试`；**注入瞬间已加载的 17 行历史被替换为 0 行**（错误块独占），恢复钩子后刷新即恢复 17 行 |
| 12 | 抽屉不改变 A–G bbox（AC-UI-13） | ✅ | 打开前后逐像素一致：C `(16,90,278,667)`、D `(306,90,839,667)`、E `(1157,90,251,667)`、F `(306,769,1102,52)`、G `(0,833,1424,28)` |
| 13 | 重启后历史可查、不重复执行（AC-UI-10c ③） | ✅/部分 | `Page.reload` 后来源仍为「本地扫描 + Gitea」，历史 15 条含 `dd46…（CANCELED 12/12）`；未观察到重启后重复入队。**“崩溃中 job → INTERRUPTED”未做故障注入**（仅源码确认 `JobEngine.cs:1549-1570`），该子项记为未验证 |
| 14 | 历史分页/检索 | ⚠️ | 仅 `getJobs(50)` 拉取最近 50 条；无分页、无搜索、无“失败项筛选”（见 P2-3） |

### 1.2 关键问题

#### P0-2 任务中心分段被旧 `currentJobId` 劫持：进行中作业不可见/不可取消
- **现象（可稳定复现）**：只要此前点过「取消」或「仅重试失败项」（store 会设置 `state.currentJobId`），之后任何一次作业列表合并都会执行 `syncCurrentJobSegment()`，把 `tasksSegment` 强制改成“旧 currentJob 所在的分段”。实测：先取消过 1 个作业（`currentJobId` 指向已取消 job），再在「进行中」分段提交真实 FETCH：
  - `activeSelected=false`、`activeRows=0`、`historyRows=16`；
  - 顶部摘要仍显示 `进行中 1 · 当前队列 1`，但列表切到「历史」，**正在运行的作业行不可见，因此无法在任务中心取消/观察**；
  - 截图 `artifacts/ui-review/m2b-ux-segment-bug.png`（`进行中 1` 徽标 + 历史 tab 下划线 + 16 行历史）。
- **根因**：`ui/src/stores/workbench.ts:1822-1829` `syncCurrentJobSegment()` 在每次 `applyJobs/applyJobUpdate` 末尾无条件执行（调用点 1799、1819），语义应为“用户主动选中某 job 时同步”，而非“每次列表刷新都重置分段”。
- **影响**：AC-UI-10c ① 的“任务中心可打开、进行中可取消”在主用户路径（第二次及以后使用任务中心）失效；并行提交多作业时更易出现“看得到计数、看不到作业”。
- **建议**：`syncCurrentJobSegment` 仅在 `openTaskCenter(jobId)`/`selectJob(jobId)`/取消/重试的显式调用点执行；列表合并只更新计数与行状态，不强制分段；新作业出现但当前分段不含它时，在分段 tab 上给出“新作业 N”提示而非静默跳转。

#### P2-3 错误态整块替换已加载列表 / 无分页、无失败项筛选
- `DrawerHost.vue:139-155` 的 `v-if loading → v-else-if error → v-else-if empty → v-else list` 顺序导致：**抽屉打开期间 1.5s 轮询任一瞬时失败，已加载的作业列表被整块替换为错误块**（实测：17 行历史 → 错误块生效时 0 行；错误块本身含错误码+「重试」，点重试可恢复 17 行）。建议保留过期列表 + 顶部告警条。
- 历史仅最近 50 条（Host 保留 200），无分页/搜索；无“仅看失败项/取消项”筛选；“展开逐仓结果”展开后不折叠失败统计（可接受）。

---

## 2. 批量入口（§11.6、FR-CARD-04、D-025 §2.3-1）

### 2.1 入口矩阵（实测）

| 入口 | 实测动作清单 | 结果 |
|---|---|---|
| ① 菜单栏「操作(O)」 | SYNC / CREATE / Clone / **TAG** / **RESTORE** / Delete（6 项，另有单仓 5 键） | ✅ 6 项均可达；TAG 弹参数对话框、RESTORE 弹确认框 |
| ② 组件卡片右键菜单 | SYNC / CREATE / Clone / **TAG** / **RESTORE** / Delete（+ 详情/定位/复制等只读项） | ✅（源码 `workbench.ts:2291-2312`；本次实测菜单入口，右键为同一 `command()` 派发） |
| ③ C 区顶部批量条 `[data-testid=batch-bar]` | 仅 SYNC / CREATE / CLONE / DELETE（4 个纯图标按钮 ⟳ ＋ ⤓ ⋯ + 全选 + 清除） | ⚠️ 与 ①/② 动作清单不一致；**无 TAG / RESTORE**；**批量 PULL / FETCH / CHKOUT / COMMIT / PUSH 在三个入口都没有**（后端 `KindWhitelist` 已支持 11 种 kind） |
| ④ 组件卡片 hover 快速按钮 | SYNC / CREATE | ✅ 存在（`tabindex=-1`、容器 `aria-hidden`，键盘不可达 → P2-1） |
| ⑤ 组件卡片勾选（批量选择模式） | 全选/清除/计数 N/M | ✅ 全选后 `4/4`、卡片 checked=4 |

> `ui/src/viewmodel.ts:751-761` 的 `BATCH_COMMANDS`（9 种动作）**未被任何组件引用**，属历史遗留死代码，进一步说明“动作清单单源”未落地。

### 2.2 确认/参数对话框与「未确认 0 写」

| 动作 | 交互 | 实测 |
|---|---|---|
| SYNC | 确认框：`批量 SYNC N 个组件？…受控 SYNC（远程覆盖本地）…不整体回滚` | ✅ 文案完整；`Escape` 取消后 `runBatch` 桥调用计数 = 0（AC-UI-10c/FR-ACT-04 零写） |
| DELETE | 确认框：`批量 Delete N 个组件？…软删除到 .trash（保留 30 天，可恢复），不写远端` | ✅ 同上，0 写 |
| CLONE | 预览缺失仓清单（本次真实环境无缺失仓 → Toast「所选组件无缺失仓，无需 Clone」）；mock selfcheck 环境 4 个缺失仓预览 ✅ | ✅ 预览/空提示均有可感知结果 |
| CREATE | 参数对话框：分支名必填（空 → 确认禁用；非法字符 → 红字 hint；合法 → 可确认） | ✅ 实测 `review/ux-dryrun` 合法；确认前 0 写 |
| TAG | 参数对话框：`批量打 Tag（N 个组件）`、Tag 名称必填、dryRun 勾选项 | ✅ 实测弹窗、Escape 关闭、0 写 |
| RESTORE | 确认框：`.trash` 按仓名找最近备份、目标存在则跳过、不写远端 | ✅ 实测弹窗、Escape 关闭、0 写 |
| dryRun | CLONE/CREATE/TAG 对话框有「仅预览（dryRun，不执行任何写操作）」勾选 | ✅ 勾选后 `args.dryRun=true` 下发（见 2.3），Host 逐仓 `SKIPPED + DRY_RUN…`，`getRemoteRefs` 验证未建分支 |

### 2.3 CREATE dryRun 真实提交（只写 job、不写仓）

- 对话框确认后桥调用：`runBatch {kind:"CREATE", componentIds:[4 个], args:{branch:"review/ux-dryrun", dryRun:true, confirm:true}}` → Toast `✓ 已提交批量 CREATE · job efdaefa5…（16 仓）`，**自动打开任务中心**（`openTaskCenter(jobId)`）。
- 任务中心行：`批量 CREATE ✓ 成功 efdaefa5… 0/16 跳过 16 结束 … 耗时 1.5s`；展开 items 全部 `↷ 跳过 / DRY_RUN：已预检通过，将执行 CREATE（未执行）`。
- 事后查询 `RTCT_Demo/DESIGN` 分支：仅 `refs/heads/main`、`origin/main`，**未创建 `review/ux-dryrun`** → dryRun 零写成立。
- **但出现 P1-1**：预览作业被标成绿色「✓ 成功」，见 §6。

### 2.4 选择语义（P1-3）

- 批量条进入选择模式显示 `0/4`（已选 0）；此时点 SYNC，确认框显示 **「批量 SYNC 1 个组件？ 将对 1 个组件的 4 个仓执行… 组件：RTCT_Demo」** —— 即 `0/4` 的显式计数与“实际作用于当前选中组件”不一致（`workbench.ts:1944-1949` 的 fallback：`selectMode && checked>0 ? checked : [selectedComponentId]`）。
- 目标在确认框内有明示、破坏性动作必须确认，故不构成零确认写；但对 DELETE/SYNC 这类强写，`0/N` 计数极易让用户误判“未选任何组件 = 安全”，建议：选择模式下 0 勾选时禁用批量动作并提示“请先勾选组件”，或把计数改为“已选 0（将作用于当前组件 RTCT_Demo）”。
- 「未选择组件」的提示分支（Toast「请先选择组件」）在组件卡恒有默认选中时几乎不可达，实际守卫依赖上述 fallback。

---

## 3. 取消 / 重试（AC-UI-10c 逐条判定）

### 3.1 真实结果在 UI 的展示（核对 `docs/m2b-status.md` §2 声明）

| 声明 | UI 核对（真实 jobs.json + 历史面板） | 判定 |
|---|---|---|
| 取消 12 项 FETCH → `CANCELED 12/12，0 ok/0 failed` | 历史行 `批量 FETCH ⊘已取消 dd46d919… 0/12 结束 16:18:18 · 耗时 438ms`；展开 12 项均为 `⊘ 已取消 / E_TIMEOUT · 作业取消：未开始执行`；「仅重试失败项（12）」 | ✅ 数量一致；**但错误码错误（P0-1）** |
| retryFailed → 新 job `SUCCESS 12/12（retried=12）` | 历史行 `批量 FETCH ✓成功 23197d69… 12/12`；12 项 `✓ 成功 / FETCH 完成 / pre→post / 起止时间` | ✅ |
| JOB_UPDATED 自动更新（未手动刷新） | 真实 FETCH 期间自动进度 0→4；终态自动通知（任务中心打开时只进通知中心） | ✅ |
| 提交后自动打开任务中心 | CREATE dryRun 实测自动打开并聚焦新 job | ✅ |

本次**实时**复验（真实 Host）：提交 FETCH 16 仓 → 245ms 时 UI 点「取消」→ `CANCELED`、`0/16`、`跳过 4`、`仅重试失败项（12）`、状态栏队列 1→0；随后点重试 → 新 job `7/7 成功`（1 个真实失败 + 6 个取消项被重试，成功/跳过项未重试）。另一次真实运行中 1 个 item 真实失败（`E_CREDENTIAL_SCOPE · 无法读取 origin push URL`），作业因取消请求终结为 `CANCELED` 而非 `PARTIAL`（见 §9 观察项，供安全评审判断）。

### 3.2 AC-UI-10c 逐条

| AC 子项 | 判定 | 说明/证据 |
|---|---|---|
| ① 返回 `jobId`；取消后 job 状态 `CANCELLED`；已开始仓 `CANCELLED` 并触发重扫；**未开始仓保持原状**；不整体回滚；终态以实际扫描为准 | ❌ **未通过** | 取消确实返回 `jobId`、job 终态 `CANCELED`、不整体回滚、终态后触发快照刷新（`refresh/getSnapshot/getRepoDetail`）；但**未开始仓被写成 `CANCELED` + `errorCode=E_TIMEOUT`**（`JobEngine.cs:786-795 CancelRemainingItemsUnsafe`），违反“未开始仓保持原状”（SRS §7.6 原文亦然）；且 `E_TIMEOUT` 在 UI 上被渲染成“超时”错误，语义撒谎；“已开始仓触发重扫”仅由终态回写间接体现，未做逐仓重扫断言 |
| ② `JOB_UPDATED` 含 `{jobId,state,current,total,cancellable}` 且取消后 `cancellable=false` | ⚠️ **部分** | 事件确实到达且 `cancellable` 请求取消后由 true→false→终态 false（实测）；但载荷键为 `id/ok/…`（完整 `JobDto`），**无 `jobId`、无 `current`**（见 P2-2），AC 字面断言不可直接套用 |
| ③ 重启 Host 后历史可查、不重复执行、状态一致 | ✅/部分 | 页面重启后历史 15 条、`dd46… CANCELED` 可查、未重复入队；**崩溃中 job 的 `INTERRUPTED` 未做故障注入**（源码 `JobEngine.cs:1549-1570` 将 PENDING/RUNNING 标 INTERRUPTED 不重跑） |
| ④ 仅重试失败项只对失败/取消仓发新 item，不复跑成功仓 | ✅ | 实测：原 job `5 ok + 1 failed + 4 skipped + 6 canceled` → 重试新 job 恰 7 项（1 failed + 6 canceled），最终 7/7 成功；`retryFailed` 源码只取 FAILED/INTERRUPTED/CANCELED（`JobEngine.cs:327`） |

---

## 4. M2b-2 缺口（对照 ADR D-025 §2.3 与 SRS P0/P1）

> 本节为“M2b 仍不完整”的证据；这些是 M2b **出口**缺口，按 D-025 §2.4-3 不阻塞 M2b-1 内部候选，但对外只能写“M2a/M2b-1 + M2b 未完成”。

| ADR §2.3 交付项 | SRS 锚点 | 现状（代码/实测） | 判定 |
|---|---|---|---|
| 1. 批量 Clone/Pull/Checkout/Tag/SYNC/Delete/Restore/Create（含备份 ref、dry-run、二次确认） | FR-OP-01…12、§11.6、FR-CARD-04 | 后端 `KindWhitelist` 11 种全部支持；**UI 仅可达 6 种**（4+2），批量 PULL/FETCH/CHKOUT/COMMIT/PUSH 无任何 UI 入口；`BATCH_COMMANDS` 死代码；dryRun/确认已实现 | ❌ 部分（P1-2） |
| 2. job 模型：`job`/`job_item`/`JOB_UPDATED`/`jobId`/任务中心/取消/仅重试失败项/持久化不重复执行 | FR-OP-08、AC-UI-10c | 7 态 job/item、JSON 持久化（最近 200）、事件、取消、重试、历史均可实测；取消语义与事件字段有缺陷（P0-1/P2-2） | ⚠️ 基本交付，取消子项 P0 |
| 3. 备份/回滚：`refs/backup/*`（≥30 天、`backup.retentionDays`）、`.trash` 30 天、**Restore 入口与冲突预览** | FR-REPO、AC-OP、FR-OP-11 | `refs/backup/sync/<UTC>/<TYPE>` 已创建并在任务中心展示；`.trash` RESTORE 有入口（菜单/右键）且不覆盖已有目标；**无“按 backupRef 恢复”参数/入口/冲突预览**（`StoredJobArguments` 仅有 `TrashStamp`，无 `backupRef`）；`.trash` 30 天清理存在（`BatchOperationService.cs:607`，进程内首次加载 job 列表时触发 `JobEngine.cs:1723 CleanupTrashOnce`）；**backup ref 无保留/清理、无 `backup.retentionDays` 读取** | ❌ 部分（P1-4） |
| 4. 审计完整表：`operation_log` 全字段/查询/导出/≥90 天、批量与 job 关联 | FR-OP-09、AC-OP-01…32 | 仅 JSONL audit-lite（`%LOCALAPPDATA%\MbdGitTool\audit\operations.jsonl`），每 item 一行含 jobId/jobItemId/result/backupRef（实测可见 actor/requestId）；**无 SQLite `operation_log` 表、无查询/导出 API/UI、无 90 天保留策略**；「日志与诊断」弹窗是静态 Mock 文本并写有“保留：运行日志 30 天 / 审计 90 天”（对未实现能力的静态声明，易被误读） | ❌ 未交付（P1-4） |
| 5. 写前 journal + 崩溃 reconcile（FR-SAFE-04） | FR-SAFE-04、NFR-REL-03、AC-27 | 仅 job 级“启动把 PENDING/RUNNING 标 INTERRUPTED、不重复执行”；**无写前 journal / 逐仓操作 reconcile** | ❌ 未交付（P1-4） |
| 6. FR-REPO-05 其余修复类：Repair/Re-clone、detached 建分支、**EMPTY 首推建 upstream** | FR-REPO-05、§7.6、D-025 §2.5④ | Repair/Re-clone、detached 建分支未见入口；`RepoActionService.cs:74` 仍按 M2a 口径把 `EMPTY.PUSH` 标禁用（注释“归 M2b”），**M2b 构建未按 D-025 §2.5④ 恢复矩阵** | ❌ 未交付（P1-4） |
| 7. 离线降级与 FR-ACT-03 离线子项、状态栏 6 段完整口径 | FR-ACT-03、NFR-REL-01 | `workbench.ts:354` `online: true` 硬编码；无离线检测、无 FETCH 离线禁用/说明；状态栏 6 段存在（新鲜度/健康/队列/CI/通知/网络），网络段恒“在线” | ❌ 未交付（P1-4） |

**SRS P0/P1 口径对照**：D-025 §4 的 M2b 出口 AC 为 `AC-OP-01…32`、`AC-REPO-01…11`、`AC-SAFE-01…13`、`AC-UI-10c`、故障注入报告、14 态矩阵 M2b 复验。本批仅交付其中 `AC-UI-10c` 的主体（含 2 个 P0 缺陷）与批量能力子集；**未附逐 AC 证据、无故障注入报告、无 14 态矩阵 M2b 复验记录**，故 M2b 出口不可判定。

---

## 5. M2a 行为回归（确认条 / tooltip / 矩阵单源 / 状态栏）

官方脚本复跑（日志：`artifacts/ui-review/m2b-review-m2a-selfcheck.log`）：

| 断言 | 结果 |
|---|---|
| `ui-08` Host 单源 COMMIT/PUSH `requiresConfirm=true` | ✅ |
| `ui-08.tooltip-format` 5 键 tooltip 匹配 `原因 [E_*] → 替代动作` | ✅（首轮 10/11，`ui-15.batch-entry-feedback` 曾因页面残留状态失败，重跑 11/11；见 P2-5） |
| `ui-10b` PUSH 确认条：出现/高度 44px/5 键 bbox 不变/未确认 0 写/ESC 焦点归还 | ✅（实测 `将推送 0 个提交（main → origin/main）`，44px，bbox 前后一致，`focusReturn=action-push`，`runRepoAction` 计数 0） |
| `ui-10.status-template` 状态栏 `最近一次操作：` 模板 | ✅（本次实测仍为“无”，有操作时由 M2a 逻辑更新） |
| `ui-15.no-old-boundary-copy` 无“M2b 未接入”旧文案 | ✅ |
| 矩阵单源：UI `data-enabled/requires-confirm/reason-code` 与 Host `getComponentDetail.actions` 逐格一致 | ✅（RTCT_Demo/IMPL：PULL/FETCH/CHKOUT enabled，COMMIT disabled `E_NOTHING_TO_COMMIT`，PUSH enabled；requiresConfirm COMMIT/PUSH=true，与 Host 一致；MISSING 仓 5 键全部禁用且 tooltip 合规） |
| M2a 单仓五键/确认条/焦点/审计链 | 未发现回归；M2b 新增 overlay/BatchBar 未破坏 F 区与状态栏布局（bbox 不变） |

> 说明：单仓 `PUSH` 在 SYNCED/AHEAD 下允许且“将推送 0 个提交”属 M2a 既有行为（本次非回归项）；M2a 的“一键写”仍由确认条唯一入口，实测未发现零确认写。

---

## 6. 缺陷清单（P0 / P1 / P2）

### P0（Must fix before tag）

**P0-1 · 取消未开始项被写成 `CANCELED` + `E_TIMEOUT`（AC-UI-10c ① 不通过，语义撒谎）**
- 证据：真实历史 job `dd46d919…` 12/12 item = `CANCELED + E_TIMEOUT + 作业取消：未开始执行`；实时取消 job `7d6664a1…` 12 个未开始 item 同样 `CANCELED + E_TIMEOUT`；UI 渲染为红色 `E_TIMEOUT · 作业取消：未开始执行`（截图 `m2b-ux-task-canceled.png`）。
- 位置：`src/MbdGitTool.Host/Services/JobEngine.cs:786-795`（`CancelRemainingItemsUnsafe` 把 PENDING 全部置 CANCELED 并赋 `BridgeErrorCodes.Timeout`）。
- 影响：违反 AC-UI-10c ①“未开始仓保持原状”与 SRS §7.6；用户把“主动取消”误读为“超时失败”，并会触发错误的重试预期；`E_TIMEOUT` 会被 UI 错误映射（viewmodel E_TIMEOUT → “等待 Host 超时，结果未知”）。
- 建议：未开始 item 保持 PENDING（job 终态 CANCELED，UI 对 PENDING 显示“未执行/已取消（未开始）”）；取消不使用 `E_TIMEOUT`（用 `E_CANCELLED`/不置 errorCode，或 `OP-E00x`）；若产品坚持“未开始也标 CANCELED”，需先改 SRS §7.6/AC-UI-10c 并说明“保持原状”的口径，否则按 AC 修。

**P0-2 · 任务中心分段被旧 `currentJobId` 劫持：进行中作业不可见/不可取消**
- 证据：`m2b-ux-segment-bug.png`；复现步骤与 DOM 见 §1.2。复位条件：此前任一取消/重试操作。
- 位置：`ui/src/stores/workbench.ts:1799/1819/1822-1829`。
- 影响：AC-UI-10c ①② 主链路（进行中可见、可取消）在第二次使用任务中心后失效；`进行中 N` 与列表内容矛盾。
- 建议：只在显式 `openTaskCenter(jobId)/selectJob` 时同步分段；新作业事件只更新计数并在 tab 上加未读提示。

### P1（建议与 P0 同批修；不修则 tag 说明中必须列为已知缺陷）

**P1-1 · dryRun 预览作业在任务中心显示「✓ 成功 0/16」，Toast 也不标注 dryRun（历史记录“伪装成功”）**
- 证据：job `efdaefa5…`（CREATE dryRun，16/16 SKIPPED）行 = `批量 CREATE ✓ 成功 0/16 跳过 16`，进度条因 skipped 计入已满，而文本 0/16；Toast `✓ 已提交批量 CREATE …（16 仓）` 未提“预览”。事后 `getRemoteRefs` 证明未建分支（功能安全正确）。
- 影响：与 D-025 §2.4-1“不得可点无效果/伪装成功”同族；用户或审计读者会把“预览成功”读成“已创建分支”；进度条/文本/状态三方矛盾。
- 建议：job `args.dryRun=true` 时 kind 显示“批量 CREATE（预览）”、状态显示“预览完成”/“已跳过 N”、进度条只按已执行项；Toast 加“（dryRun 预览，未写入）”。

**P1-2 · 批量入口动作清单三处不一致，且 5 种后端批量动作无 UI 入口**
- 证据：`BatchBar.vue:36-67` 仅 SYNC/CREATE/CLONE/DELETE；菜单/右键 6 项（含 TAG/RESTORE）；`viewmodel.ts:751` `BATCH_COMMANDS` 9 项未被引用；后端 `KindWhitelist` 11 种（含 PULL/FETCH/CHKOUT/COMMIT/PUSH）。
- 影响：FR §11.6 “3 处入口同一动作清单、动作清单=Clone/Pull/Fetch/Checkout/切 Tag/SYNC/Delete/Restore/Create”未满足；批量 PULL/CHKOUT 等能力不可用；状态文档 §4-3 称“RESTORE/TAG 仅后端 API”与实现不符（UI 可达，文档过时）。
- 建议：BatchBar 补齐 TAG/RESTORE 与批量 PULL/FETCH/CHKOUT（至少菜单可达并标注“M2b-2 未接入”若不做），用 `BATCH_COMMANDS` 单源渲染三入口；更新文档。

**P1-3 · 选择模式 `0/N` 与实际作用目标不一致（强写入口语义陷阱）**
- 证据：批量条 `0/4` 时点 SYNC → 确认框“批量 SYNC 1 个组件？……组件：RTCT_Demo”。
- 影响：SYNC/DELETE 等强写可能作用于用户以为“未选中”的组件；虽有确认框兜底，仍属高误操作风险。
- 建议：选择模式 0 勾选时禁用动作并提示；或计数与目标文案统一（“将作用于当前组件 RTCT_Demo”）；`batchTargetComponentIds` 的 fallback 需产品裁决并写进 SRS。

**P1-4 · M2b-2 六项能力缺口（备份 ref 恢复 / 审计完整表 / journal+reconcile / EMPTY 首推 / 离线降级 / backup ref 保留清理）**
- 证据：§4 表；`StoredJobArguments` 无 backupRef 恢复参数；无 SQLite `operation_log`；`RepoActionService.cs:74` EMPTY.PUSH 仍禁用；`workbench.ts:354` `online:true`；无写前 journal；`.trash` 清理有、backup ref 无。
- 影响：M2b 出口不可判定（`AC-OP-01…32`、`AC-REPO-01…11`、`AC-SAFE-01…13`、FR-SAFE-04、FR-REPO-05、FR-ACT-03 离线子项）；不得对外宣称 M2b/M2 完成。
- 建议：按 D-025 §2.3 逐项排 M2b-2；本批在 README/状态文档保持“M2b 未完成”声明（`docs/m2b-status.md` 已诚实列出，需同步修正“RESTORE/TAG 仅后端 API”的不实描述）。

### P2（修复/记录即可）

**P2-1 · 任务中心抽屉无焦点陷阱/ESC 焦点回落；批量条纯图标无 aria 名称；卡片快速按钮键盘不可达**
- 证据：抽屉内 24 个可聚焦元素，但 Tab 10 次全部逃逸到主界面（`title-settings`/`component-sort`/…）；ESC 关闭后 `document.activeElement` 为 DIV（非触发按钮 `title-tasks`）；`BatchBar` 4 按钮 `aria-label=null`（文本仅 ⟳/＋/⤓/⋯，DELETE 用 ⋯）；卡片 SYNC/CREATE 按钮 `tabindex=-1`、容器 `aria-hidden`。
- 关联 AC-UI-13/§11.0.4-5（“抽屉内焦点陷阱、ESC 焦点回落”对抽屉通用）。建议抽屉加 focus trap + 记录触发元素；批量按钮加可见短文案或 `aria-label`（DELETE 建议“删除”文字/危险色）。

**P2-2 · 契约/命名漂移：job 状态与 JOB_UPDATED 字段与 SRS/AC 字面不一致**
- SRS §8.1/AC-UI-10c 用 `CANCELLED/SUCCEEDED/QUEUED`；实现与 DTO 用 `CANCELED/SUCCESS/PENDING/PARTIAL`；AC-UI-10c ② 要求事件 `{jobId,state,current,total,cancellable}`，实测载荷为完整 `JobDto`（`id/ok/failed/skipped/…`，无 `current`）。
- 影响：AC 自动化断言不可直接复用、跨版本客户端/审计对不上。建议：以 D-025/§8.1 更新为唯一枚举并同步 AC 字面，或补 `jobId/current` 兼容字段；在 `docs/m2b-status.md` 明示口径变更。

**P2-3 · 错误态整块替换列表；历史无分页/搜索/失败项筛选；job 行缺“已取消 N”计数与失败筛选**
- 证据：§1.2；`0/16 跳过 4` 对 12 个已取消项无计数（只有“仅重试失败项（12）”侧面体现）；进度条把 skipped 计入百分比导致“0/16 但进度条已满”。建议：列表保留 + 顶部错误条；取消计数独立展示；行内失败/取消筛选；`getJobs` 分页。

**P2-4 · 文案/一致性**
- 批量 kind 标签中英混用（`批量 SYNC`/`批量 CREATE`/`批量 Clone`/`批量删除`/`批量 Tag`/`恢复备份`）；空态描述仅列 SYNC/CREATE/Clone/Delete（未含 TAG/RESTORE）；「日志与诊断」为静态 Mock 日志且静态声明“审计 90 天”（未实现）；`BatchDialog` 的 dryRun 提示“仅校验名称并预览”与 Host 实际（逐仓预检 + SKIPPED）语义略有偏差（可接受但应统一）。

**P2-5 · 门禁脚本可复跑性**
- `cdp-m2a-selfcheck.mjs` 首跑 10/11（`ui-15.batch-entry-feedback` 因页面残留状态失败），重跑 11/11（日志见附录）；`cdp-m2b-selfcheck.mjs` 36/36 但 30+ 项在强制 Mock 下完成，真实 Host 段仅断言 `getJobs`/来源，**真实取消/重试证据不在脚本内**。建议脚本前置 `Page.reload` + 固定状态，并把本报告的真实 Host 取消/重试断言并入 m2b selfcheck；`docs/m2b-status.md` 的 “289/289 单测”未在本评审复跑，不计入证据。

---

## 7. Top 8（按修复优先级）

1. **P0-2** 任务中心分段被旧 `currentJobId` 劫持 → 进行中作业不可见/不可取消（`workbench.ts:1822-1829`）。
2. **P0-1** 取消未开始项 = `CANCELED + E_TIMEOUT`，违反 AC-UI-10c ①/§7.6（`JobEngine.cs:786-795`）。
3. **P1-1** dryRun 预览作业显示「✓ 成功 0/16」且无预览标识（历史记录伪装成功）。
4. **P1-2** 批量入口清单不一致（BatchBar 缺 TAG/RESTORE；批量 PULL/FETCH/CHKOUT/COMMIT/PUSH 无 UI；`BATCH_COMMANDS` 死代码）。
5. **P1-3** 选择模式 `0/N` 仍作用于当前选中组件（SYNC/DELETE 语义陷阱）。
6. **P1-4** M2b-2 六项能力缺口（备份 ref 恢复、审计完整表、journal/reconcile、EMPTY 首推、离线降级、backup ref 保留）。
7. **P2-1** 任务中心抽屉无焦点陷阱/焦点回落；批量条纯图标无 `aria-label`、卡片快速入口键盘不可达。
8. **P2-2** 契约命名/事件字段漂移（`CANCELED/SUCCESS`、JOB_UPDATED 无 `jobId/current`），AC 字面断言不可复用。

---

## 8. 验收判定与 Tag 建议

1. **任务中心核心能力（本批判定 = 部分通过）**：分段/历史/逐仓展开/backupRef/取消/仅重试失败项/刷新/事件驱动/空态/错误态/重启持久化均可实测；**但 AC-UI-10c ① 不通过（P0-1），分段劫持使进行中作业在常见二次使用路径不可取消（P0-2）**。
2. **批量入口（部分通过）**：确认/参数对话框、dryRun、未确认 0 写、提交后自动打开任务中心均成立；动作清单不完整且三入口不一致（P1-2）、选择语义有陷阱（P1-3）、dryRun 结果展示撒谎（P1-1）。
3. **结论**：
   - **M2b-1 当前状态：需先修，不建议直接 tag**；修 P0-1/P0-2（建议连同 P1-1/P1-2）后可 tag `M2b-1`（内部候选，tag message 必须写明“M2b-2 缺口未完成、AC-OP/AC-REPO/AC-SAFE 未验收”）。
   - **M2b 仍不完整**：ADR §2.3 的 7 项中 5 项缺/部分（见 §4），M2b 出口 AC 无证据，**不得宣称 M2b 或 M2 完成**；`m2-ops` tag 维持撤销。
   - M2a 未回退（官方 selfcheck 11/11、确认条/tooltip/矩阵单源/状态栏实测通过），M2b 新增能力按“M2b-1 候选”发布是诚实的。

---

## 9. 未验证项 / 观察项（供后续与并行评审）

- **未验证**：>10 组件/ >40 仓二次确认阈值（环境仅 4 组件，UI 对强写统一确认，阈值文案未实现专门断言）；1280×800/200% 缩放下任务中心与批量条布局；崩溃中 job 的 `INTERRUPTED` 故障注入；“已开始仓取消后逐仓重扫”的逐仓断言；`289/289` 单测未复跑（本评审范围外）。
- **观察（交安全/代码评审）**：真实批量 FETCH 中 `RTCT_RemoteOnly/IMPL` 曾失败 `E_CREDENTIAL_SCOPE · 拒绝注入 PAT：无法读取 origin push URL`，随后同一仓 retry 成功（7/7）。FETCH 因 push URL 不可读而失败可能过严，且该失败使作业最终显示 `CANCELED` 而非 `PARTIAL`（取消请求覆盖了真实失败状态），建议代码评审评估是否符合 AC-SAFE-14 意图。
- **评审口径**：本次仅在真实 Host 上执行 `dryRun` 与只读 `FETCH`；未执行 SYNC/DELETE/CLONE 等真实写（这些已有 `docs/m2b-status.md` 的历史真实证据，按原文引用但未复跑）。

---

## 附录 A · 复跑命令与证据文件

```powershell
# 官方门禁脚本（真实 Host + CDP 9222）
node ui/scripts/cdp-m2a-selfcheck.mjs   # 11/11 PASS（日志 artifacts/ui-review/m2b-review-m2a-selfcheck.log）
node ui/scripts/cdp-m2b-selfcheck.mjs   # 36/36 PASS（日志 artifacts/ui-review/m2b-review-m2b-selfcheck.log；30+ 项在 Mock 下）
# 本报告实测脚本（只读/ dryRun，不改源码）
node artifacts/ui-review/m2b-review-ux-a.mjs   # 任务中心只读 DOM、空态/错误态注入、bbox
node artifacts/ui-review/m2b-review-ux-batchprobe.mjs
node artifacts/ui-review/m2b-review-ux-c.mjs   # 真实 FETCH + JOB_UPDATED + UI 取消/重试
node artifacts/ui-review/m2b-review-ux-e.mjs   # CREATE dryRun 真实提交 + 分支零写验证
node artifacts/ui-review/m2b-review-ux-h.mjs   # 进行中行 + UI 取消 + 分段 bug
node artifacts/ui-review/m2b-review-ux-i.mjs   # 重启持久化 + 实时取消/重试 + 分段 bug 复现
node artifacts/ui-review/probe-a11y.mjs        # 抽屉焦点陷阱/焦点回落
```

**截图证据**（`artifacts/ui-review/`）：
`m2b-ux-task-history-a.png`（真实历史 8 条，含取消/retry 12）、`m2b-ux-task-expanded-a.png`（逐仓展开，backupRef/错误码）、`m2b-ux-task-empty-a.png`（历史空态）、`m2b-ux-task-error-a.png`（错误态）、`m2b-ux-task-running.png`（进行中 + 取消按钮）、`m2b-ux-task-canceled.png`（取消后 12×CANCELED/E_TIMEOUT）、`m2b-ux-task-canceled-expanded.png`、`m2b-ux-segment-bug.png`（进行中 1 但列表切历史）、`m2b-ux-create-dryrun-dialog.png`、`m2b-ux-dryrun-job.png`（dryRun 作业“成功 0/16”）、`m2b-ux-push-confirmbar.png`（M2a 确认条回归）。
另有输入证据 `docs/images/m2b/{task-history,clone-dialog,clone-job}.png` 与 `artifacts/m2b-selfcheck/01..09-*.png`、`result.json`。

## 附录 B · 关键代码位置索引

| 问题 | 位置 |
|---|---|
| 取消未开始项置 CANCELED+E_TIMEOUT | `src/MbdGitTool.Host/Services/JobEngine.cs:786-795` |
| 分段被 currentJobId 劫持 | `ui/src/stores/workbench.ts:1799,1819,1822-1829` |
| dryRun 结果/进度/状态展示 | `ui/src/viewmodel.ts:911-942`；`ui/src/components/overlays/DrawerHost.vue:183-216` |
| 批量条 4 键 / 无 aria | `ui/src/components/workbench/BatchBar.vue:36-68` |
| 三入口动作清单不一致/死代码 | `ui/src/components/shell/MenuBar.vue:57-63`；`ui/src/stores/workbench.ts:2291-2312,2625-2689`；`ui/src/viewmodel.ts:751-761` |
| `0/N` fallback | `ui/src/stores/workbench.ts:1944-1949` |
| 错误态整块替换 | `ui/src/components/overlays/DrawerHost.vue:139-155` |
| 抽屉无焦点陷阱 | `ui/src/components/overlays/DrawerHost.vue:84-100,506-509`（对比 `BatchDialog.vue` 有 `trapTab`） |
| 审计仅 JSONL | `src/MbdGitTool.Host/Services/OperationAuditService.cs:23-230`；UI `ModalHost.vue:42-50,207-214` |
| EMPTY.PUSH 仍禁用 | `src/MbdGitTool.Host/Services/RepoActionService.cs:74` |
| 离线恒在线 | `ui/src/stores/workbench.ts:354` |
| `.trash` 清理 / backup ref 无清理 | `JobEngine.cs:1723`、`BatchOperationService.cs:607-662`；`StoredJobArguments`（`JobStore.cs:202-212`）无 backupRef/保留策略 |
| job 启动 INTERRUPTED | `JobEngine.cs:1549-1570` |
```
