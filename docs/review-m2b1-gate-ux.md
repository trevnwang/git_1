# M2b-1 UX Gate 复评（提交 `9d16090`，建议打 tag）

> 角色：独立 UX Gate 签收复评（gate-m2b1-ux2）。对象：`9d16090 fix(m2b1): close review P0/P1 …`（父 `3ea870e`，2026-11-14 16:54:39 +0800）。
> 环境：Win11 / 真实 Host PID 38096（Debug，16:54:44 启动）/ CDP 9222 / `https://app.local`。构建产物与提交一致：Host dll 16:53:05、UI bundle `index-Bi1V8Qq3.js` 16:53:02，均晚于对应源码最后修改（16:49:08/16:52:58）；真实快照 4 组件/14 仓。
> 方法：只读评审；真实 UI/CDP 实测 4 个脚本：提交内 `cdp-m2b1-fix-selfcheck.mjs`（68 项）+ 自写 `gate-dialogs`（39 项）+ `gate-event`（24 项）+ `cdp-m2a-selfcheck.mjs`（11 项）。真实写仅只读 FETCH 后取消；未改源码/需求/夹具/真实仓库。
> 证据：`artifacts/m2b1-gate-ux2/`（4 份 log/result + 8 张截图）；本报告为唯一源码树写盘。

## 0. 结论

**建议打 `m2b-1` tag**（仅代表 M2b-1 内部候选，不代表 M2b/M2 完成）。上一轮 UX P0-1/P0-2、P1-1/P1-2/P1-3 阻断项均已闭环；本轮专项实测 **P0=P1=0**，仅余 3 项 P2 记录项。

| 专项判据 | 判定 | 一句话证据 |
|---|---|---|
| 1 任务中心 + FETCH + 取消 + PREVIEW 徽标 | **PASS** | 真实 4 组件/16 仓 FETCH 行可见可取消；取消后 12×`CANCELED`（errorCode=null、`已取消（未开始）`），UI/JSON 均 `E_TIMEOUT=0`；历史 dryRun 作业显示「预览（dryRun）/预览完成/未执行写操作」 |
| 2 SYNC/DELETE 对话框安全门 | **PASS** | 多选拒绝；0 勾选禁用+提示；单组件标题/占位=组件 ID；错误 ID 拒绝；操作前后 jobs/audit/jobs.json bytes/HEAD/.trash **全不变**（0 写） |
| 3 焦点陷阱/ESC/事件信封 | **PASS** | 抽屉与对话框 Tab 不逃逸、ESC 焦点归还；真实 4 条 JOB_UPDATED 均含 `{event,jobId,current,data,state,total,cancellable}`，current/data id=jobId，终态 CANCELED、cancellable=false |
| 4 M2a selfcheck 不回退 | **PASS** | `cdp-m2a-selfcheck.mjs` **11/11** 首跑通过（Host requiresConfirm/tooltip/确认条/状态栏/审计 actor+requestId） |

## 1. 判据 1：任务中心（实测）
- 进行中：批量条→更多→FETCH→确认（4 组件/16 仓）后自动打开任务中心并切「进行中」段，行 `RUNNING 0/16` 带「取消」按钮（`real.running-visible`/`real.running-auto-focus` PASS）；另两轮独立实测行 id `578ebdb3…` 同现。
- 取消：点击取消→终态 `CANCELED`；jobs.json 该 job 12 项 `CANCELED`（`errorCode:null`，message「作业取消：未开始执行」），4 项为预存在 SKIP（`E_REPO_NOT_LOCAL/E_NOT_FOUND`，非取消产生）；UI 展开项显示「已取消（未开始）」、`E_TIMEOUT=0`；「已取消 12」计数可见。
- PREVIEW/dryRun 徽标：真实历史 job `5b3b76ae…`（FETCH，dryRun=true，16×PREVIEW，零写）显示「预览（dryRun）」徽标、状态「预览完成」、进度「未执行写操作」；注入 PREVIEW 快照同一断言通过。
- 事件驱动自动刷新：真实 FETCH 期间收到 2→4 条 JOB_UPDATED，`进行中 1` 计数/进度自动更新，终态后行移入历史段（无需手动刷新）。

## 2. 判据 2：批量 SYNC/DELETE 对话框（实测）
- 多组件拒绝：选 4/4 后点 SYNC、DELETE，均 toast「仅支持单个组件…」，不打开对话框（dialog=0）。
- 0 勾选禁用：0/4 时显示 `⚠ 先勾选组件`（title 含「先勾选组件」）；5 个常驻按钮 `aria-disabled=true`；「更多」菜单 7 项均原生 `disabled`；点 SYNC 仅出「先勾选组件：批量 SYNC未提交」toast，无对话框。
- 单组件确认：1/4（RTCT_Demo）点 SYNC → 标题「SYNC 受控覆盖 · RTCT_Demo」、输入占位=组件 ID、空值「确认」禁用、hint「须与组件 ID 完全一致」、提供 dryRun；DELETE 同构（「Delete 软删除 · RTCT_Demo」）。
- 错误 ID：输入 `WRONG_COMPONENT_ID` 点确认 → toast「确认失败：请输入完整组件 ID「RTCT_Demo」」，不产生作业；DELETE/正确 ID+取消/Esc 同样 0 作业；Esc 后焦点回触发按钮 `batch-delete`。
- 未确认 0 写（强证据）：操作前后 jobs 35→35、audit 294→294 行、jobs.json 140990→140990 bytes、`.trash` 列表与 RTCT_Demo 四仓 HEAD 全同。

## 3. 判据 3：焦点陷阱 / ESC / 事件信封（实测）
- 抽屉：打开后焦点在抽屉内，14×Tab 与 Shift+Tab 均不逃逸，ESC 关闭且焦点归还 `title-tasks`（`focus.drawer-*` 4 项 PASS）。
- 批量对话框：初始焦点在对话框内，8×Tab 不逃逸，ESC 关闭且焦点回 `batch-create`/`batch-delete`（`focus.dialog-*` 3 项 PASS）。
- 事件信封：页面启动前包装 `chrome.webview.addEventListener` 捕获真实批量 FETCH 原始帧，4 条事件全部满足 `event==="JOB_UPDATED"` 且同时含 `jobId/current/data/state/total/cancellable`；`current.id==data.id==jobId`；`current.state==state`（PENDING→RUNNING→RUNNING→CANCELED）；`cancellable` true→false；items 无 `E_TIMEOUT`（只有预存在 `E_REPO_NOT_LOCAL/E_NOT_FOUND`）。
- 兼容：新旧信封（含旧 `data` 形状、仅信封级字段）均能合并；陈旧 `currentJobId` 不再劫持分段，历史段停留且 tab 显示「● 1 新」（`event.new-envelope`/`p0-2.*` PASS）。

## 4. 判据 4：M2a 不回退（实测）
- `cdp-m2a-selfcheck.mjs`：**11/11**（Host COMMIT/PUSH `requiresConfirm=true`、tooltip 紧凑格式、确认条 h=44≤48/bbox 稳定/未确认 0 写、状态栏模板、审计 `actor`+`requestId`）。
- 提交内 `cdp-m2b1-fix-selfcheck.mjs`：**68/68**（批量入口 11 种三处一致、COMMIT/PUSH 显式禁用、dryRun 徽标、0 选择、取消/重试、焦点、事件）。

## 5. 残留 P2（不阻断 tag）

- 0 勾选时 5 个主按钮用 `aria-disabled` 而非原生 `disabled`（点击=提示；菜单项为原生 disabled）。符合「禁用并提示」意图，但严格无障碍断言需注意。
- 错误组件 ID 点确认后对话框先关闭再出错误 toast，需重新打开（小不便）。
- 上一轮 P2（错误态整块替换列表/无分页筛选/kind 文案中英混用）未在本批修复范围，随 M2b-2 处理。

## 6. M2b-2 未交付（tag 说明必写）

审计完整表/查询导出/90 天、backupRef 恢复入口、RESTORE/TAG/CREATE UI 入口、EMPTY 首推建 upstream、离线降级、写前 journal+崩溃 reconcile、AC-OP/AC-REPO/AC-SAFE 逐条证据——均未交付（`docs/m2b-status.md` §4）。

## 7. 边界与污染声明

- 全程只读：未修改源码/需求/夹具/真实仓库，未执行任何 SYNC/DELETE/CLONE/RESTORE 真实写；真实 Host 新增作业/审计仅来自实测 FETCH+取消（jobs 33→37、audit 265→326，均为 Host 正常持久化）。
- 构建基线：Host dll 与 UI bundle 时间戳均晚于被评提交源码，页面/Selfcheck 证据对应 `9d16090`；评审期间无其他进程操作该 Host。

## 8. 复跑命令

`node ui/scripts/cdp-m2b1-fix-selfcheck.mjs`（68/68）、`node ui/scripts/cdp-m2a-selfcheck.mjs`（11/11）、`node artifacts/m2b1-gate-ux2/gate-dialogs.mjs`（39/39）、`node artifacts/m2b1-gate-ux2/gate-event.mjs`（24/24）
