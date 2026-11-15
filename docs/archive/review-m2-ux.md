# M2 单仓操作 UI 与需求符合度对抗评审

> 评审日期：2026-11-14 12:28–13:05（本地）
> 评审对象：M2 单仓操作（PULL / FETCH / CHKOUT / COMMIT / PUSH）工作台 UI + 组件详情抽屉 + 四仓树错误态 + 文件夹视图
> 需求输入：`reqs/specification.md` §7.6、§9.16（FR-ACT）、§9.17（FR-CARD/FR-INFO）、§11.0–§11.3；`docs/ui-redesign-spec.md` §5/§6；`docs/ui-redesign-plan.md` S6
> 代码输入：`ui/src/stores/workbench.ts`、`ui/src/viewmodel.ts`、`RepoActionsBar.vue`、`InfoPanel.vue`、`RepoTreePanel.vue`、`ActionDialog.vue`、`ConfirmDialog.vue`、`ComponentDrawer.vue`、`App.vue`、`ToastHost.vue`、`statusbar`；Host `RepoActionService.cs`（只读核对，不改代码）
> 运行输入：真实 Host（CDP 127.0.0.1:9222，RTCRepos + `artifacts/real-test/workspace`）；隔离 Mock 实例（`ui/dist` 静态服务于 5176 + Headless Edge CDP 9334 + 注入 `window.__DSH_TEST_BRIDGE__`，13 个单仓状态 × 14 组件）
> 截图输入：`artifacts/ui-review/90-m2-fetch.png` … `98-m2-final-workbench.png`、`m2-01…m2-14`、`m2-host-07`
> 方法：CDP `Runtime.evaluate` 读 DOM/bbox/computedStyle + `Input.dispatchMouseEvent/KeyEvent` 真实鼠标键盘 + 源码逐条核对；真实 Host 侧只做**只读操作**（选择组件/开对话框后取消/开抽屉），未执行任何写操作；Mock 侧用注入桥覆盖真实环境不可达的 13 状态矩阵
> 限制：评审期间另有并行 CDP 会话在同一页面操作（见 `docs/review-ui-v3.md` 同类现象）；为避免互相干扰，矩阵类断言全部在隔离实例完成，真实 Host 仅做 2–3 次复现。失败路径未在真实仓库触发（避免写操作），用注入桥 + Host 错误码静态核对替代。
> **本报告只新建本文件，未改任何代码/构建产物。**

---

## 0. 结论（Gate 判决）

| 维度 | 结论 |
|---|---|
| 真实 Host 主链路 | **基本可用**：SYNCED 仓五键全部可用；FETCH 免确认、成功 Toast 单 `✓`、含 HEAD 与 duration；状态栏 lastOp 更新；失败码+中文文案+通知中心可工作；动作后刷新链路存在（Mock 实测 730ms 触发 `refresh`+`getSnapshot`，真实 Host 状态计算时间与操作时间一致）。 |
| 状态矩阵 | **12/13 状态符合 §7.6**；`UNKNOWN` 状态错误禁用 FETCH（与 §7.6、spec §6.2、Host `GetAvailability` 均冲突）。 |
| 确认与前置 | **不符合 FR-ACT-03/04**：无 F 区 ≤48px 确认条；PULL 被要求免确认却弹确认；COMMIT 无 staged 仍启用且对话框无“N 个文件”；CHKOUT 数据不来自 remote_ref、无搜索、无 stash 选项；Host 的 COMMIT 实为 `git add -A` 后提交（比 UI 文案“提交已暂存改动”更危险）。 |
| 焦点与可访问性 | **不通过 AC-UI-13**：ConfirmDialog 打开后焦点仍在 body、Tab 直接逃逸到状态栏/菜单栏；ActionDialog/ConfirmDialog/ComponentDrawer 关闭后焦点落回 `BODY`，未回触发控件。 |
| 抽屉/树/文件夹 | 抽屉四仓表/最近提交/CI 占位/回退均可用，但宽度 460px < 480–720；MISSING 仓树空态被误判为“无法读取目录”且每面板重复 2 个“重试”；文件夹视图四仓 chip 正常。 |
| M1/M2 边界 | Host `features.repoActions=true` 时门禁正确启开；但“关于/设置/任务中心/菜单/Mock 错误文案”仍留 M1 只读与“M2 接入”字样，版本号自相矛盾（标题 v0.3.0 / 关于 0.1.0.0）。 |
| **能否提交** | **不能提交**。P0 共 **8 项**（含 1 项 Host 危险行为：COMMIT `git add -A`），需修复后复评；P1 8 项、P2 10 项。 |

---

## 1. 评审方法与证据

### 1.1 真实 Host 只读实测（CDP 9222）

| 步骤 | 结果 |
|---|---|
| 读快照 | 4 组件 / 14 仓；`RTCT_Incomplete`、`RTCT_RemoteOnly` 为 MISSING，`RTCT_Demo`、`RTCT_Synced` 为 SYNCED；`features.repoActions=true`、`appVersion=0.1.0.0` |
| 选中 `RTCT_Synced`（默认聚焦 IMPL） | 五键 `disabled=false`；`[data-testid=repo-actions][data-action-scope=IMPL]`，`aria-label="单仓操作：Impl"`；COMMIT 在“已跟踪 0 · 未跟踪 0”下仍可用 |
| COMMIT 对话框（取消） | `data-kind=commit`；正文“将提交 RTCT_Synced_Impl（IMPL）当前分支 main 的已暂存改动；提交信息必填（1–200 字符）”；信息行仅“仓库/分支/未提交 已跟踪 0 · 未跟踪 0”，**无“将提交 N 个文件”、无 staged 列表** |
| CHKOUT 对话框（取消） | Radio 分支/Tag + 必填输入可用；`#action-ref-suggestions` 仅 1 项 `["main"]`（无远端分支/Tag 列表、无搜索框），**无 stash -u 勾选** |
| PULL 确认（取消） | 存在 `[data-testid=confirm-dialog]`：`PULL · 快进拉取？将拉取 RTCT_Synced_Impl（IMPL）：当前未落后远程（behind 0），仍执行 --ff-only 拉取…` |
| PUSH 确认（取消） | 存在确认框：`PUSH · 推送当前分支？…分支 main · HEAD 88a9539 · ahead 0 / behind 0` |
| 取消后 | lastOp 不变（仍是上一次 FETCH 成功），`actionCalls` 0 次（Mock 侧验证），确认“取消=零写” |
| 选中 `RTCT_Incomplete`（MISSING） | `data-action-scope=""`、`aria-label="单仓操作：未选择"`、五键 title 全为“请先在文件树中选择仓库”；InfoPanel 显示“未选择仓库”空态；四仓树面板显示 `✕ 无法读取目录 E_REPO_NOT_LOCAL · 仓库未克隆（MISSING） 重试 重试` |
| 双击 `RTCT_Synced` 卡片 | 抽屉 `data-source=host`，宽 **460px**、四仓 4 行、最近提交 5 条、CI 占位；ESC 关闭后 `document.activeElement = BODY` |
| 关于弹窗（真实） | “版本 **0.1.0.0** · WebView2 157.0.4341.46 … **M1 只读：写操作（PULL/FETCH/CHKOUT/COMMIT/PUSH）在 M2 接入；文件树与文件夹视图只读。**”；标题区版本 `v0.3.0` |
| 文件夹视图（真实） | 4 组件行，每行 4 个 `[data-testid=folder-repo-chip]` = `D/I/M/S`，无截断/错误 |
| 1280×800（真实） | 五键同排 5×96×30，`scrollWidth==clientWidth==1280`，无页面级横滚 |

### 1.2 隔离 Mock 矩阵（13 状态 × 5 键）

- 做法：Headless Edge（9334）承载静态 `ui/dist`（5176），`Page.addScriptToEvaluateOnNewDocument` 注入按 M2 Host 契约返回数据的假桥（`getSnapshot/getHostInfo/getRepoDetail/getRepoTree/getWorkspaceTree/getComponentDetail/runRepoAction`），`runRepoAction` 可注入成功、`E_CONFIRM_REQUIRED`（仅一次/始终）、`E_OFFLINE`、`E_NO_HOST`、`E_NOT_IMPLEMENTED` 与延迟；组件树覆盖 §7.2 的 13 个主状态。
- 该实例与真实 Host 页面完全隔离（独立 target），不写真实仓库。

### 1.3 证据选择器

`[data-testid=repo-actions]`（`data-action-scope`/`aria-busy`/`data-pending`）、`.action`、`[data-testid=action-*]`、`[data-testid=confirm-dialog]`、`[data-testid=action-dialog]`、`[data-testid=action-dialog-message]`、`[data-testid=action-dialog-ref]`、`[data-testid=checkout-kind-branch|tag]`、`[data-testid=component-drawer]`、`[data-testid=component-drawer-repo]`、`[data-testid=repo-tree-empty]`、`[data-testid=tree-error-banner]`、`[data-testid=tree-truncated]`、`[data-testid=tree-node-error]`、`[data-testid=workspace-node][data-kind=COMPONENT]`、`[data-testid=folder-repo-chip]`、`[data-testid=status-last-op]`、`[data-testid=status-notify]`、`[data-testid=toast]`、`[data-testid=info-panel]`。

---

## 2. 五键可用性矩阵

### 2.1 实测（隔离实例，逐一选中组件后读取 `.action[disabled]`）

> 期望值取自 SRS §7.6 末条映射（PULL→Pull(ff)、FETCH→Fetch、CHKOUT→Checkout 分支、COMMIT→Commit、PUSH→Push）与 spec §6.2。`✓`=enabled，`✗`=disabled。

| 主状态 | PULL | FETCH | CHKOUT | COMMIT | PUSH | 与期望 | 备注 |
|---|---|---|---|---|---|---|---|
| SYNCED | ✓ | ✓ | ✓ | ✓ | ✓ | ✅ | 无 staged 时 COMMIT 仍 ✓（P0-3） |
| DIRTY | ✗ | ✓ | ✓ | ✓ | ✓ | ✅ | 禁用理由“DIRTY：需先提交或 stash” |
| AHEAD | ✗ | ✓ | ✓ | ✗ | ✓ | ✅ | COMMIT 理由仅“当前仓库状态不允许该操作” |
| BEHIND | ✓ | ✓ | ✓ | ✗ | ✗ | ✅ | PUSH 理由“BEHIND：先 PULL” |
| DIVERGED | ✗ | ✓ | ✓ | ✗ | ✗ | ✅ | PULL/PUSH 理由可读 |
| DETACHED_TAG | ✗ | ✓ | ✓ | ✗ | ✗ | ✅ | “请先基于当前 HEAD 创建分支” |
| NO_UPSTREAM | ✗ | ✓ | ✗ | ✗ | ✓ | ✅ | PUSH 但无“发布分支建 upstream”文案/参数（P1） |
| EMPTY | ✗ | ✓ | ✓ | ✓ | ✓ | ✅ | PULL 理由通用 |
| UNMERGED | ✗ | ✓ | ✗ | ✗ | ✗ | ✅ | 理由可读 |
| IN_PROGRESS | ✗ | ✗ | ✗ | ✗ | ✗ | ✅ | 理由可读 |
| INVALID | ✗ | ✗ | ✗ | ✗ | ✗ | ✅ | 理由“INVALID：仓库绑定异常” |
| MISSING | ✗ | ✗ | ✗ | ✗ | ✗ | ✅（禁用） | 但**理由错误**为“请先在文件树中选择仓库”（P0-2） |
| UNKNOWN | ✗ | **✗** | ✗ | ✗ | ✗ | ❌ | **SRS §7.6 / spec §6.2 / Host 均为 FETCH=✓**（P0-1） |

### 2.2 P0-1：UNKNOWN 禁用 FETCH（唯一矩阵硬错误）

- 期望：`UNKNOWN` 行 Fetch=✅（§7.6 第 340 行），spec §6.2 同；Host `RepoActionService.cs:48-65` 矩阵 `["UNKNOWN"]="NYNNN"`（FETCH=Y）。
- 实测：`ui/src/viewmodel.ts:544-550` 将 UNKNOWN 五键全部置禁用，`actionAvailability` 的 `normalizedState` 分支（`viewmodel.ts:691-703`）一律返回“状态未知：数据过期，请先刷新/重试扫描”，连 FETCH 也禁。
- 影响：状态过期/远程数据陈旧时，用户唯一可用的恢复动作（FETCH 重新取远端 refs）被禁用，只能刷新快照；与 Host 能力否一致，形成“能恢复也要先刷新”的死循环观感。
- 同时违反 §7.6“按钮启用/禁用与 tooltip 由矩阵逐格生成，禁止另写一套判断”：UI 有一份矩阵（`viewmodel.ts:470-551`），Host 另有一份（`RepoActionService.cs:49-65`），本次 UNKNOWN 分叉即为该重复的直接后果。**修复：UI 消费 Host `GetAvailability`（需新增桥方法）或两处矩阵加契约测试对齐。**

### 2.3 M1 门禁（AC-UI-09，附带验证）

- `features.repoActions=false` 时：五键全部禁用，title 均为 `M1 只读：<ACTION> 需 Host 开启 features.repoActions 或 snapshot.capabilities.repoActions（M2 接入）`；点击禁用按钮 0 次 `runRepoAction` 调用、0 条 Toast → **M1 只读边界通过**。
- 切回 `features.repoActions=true`（重新拉 `getHostInfo`）后按钮按状态矩阵恢复 → 门禁逻辑正确（`workbench.ts:322-337`）。
- 但“关于”弹窗仍写“M1 只读：写操作…在 M2 接入”（真实 Host 实测），属于发布文案缺陷（P1-7）。

### 2.4 P0-2：MISSING 聚焦仓被当作“未选择”

- 代码根因：`focusedRepo()` 对 `MISSING` 直接 `return null`（`workbench.ts:300-306`），但 `focusedRepoType` 已被设为 DESIGN/IMPL；于是：
  - `RepoActionsBar`：`data-action-scope=""`，`aria-label="单仓操作：未选择"`，五键 title 统一 `请先在文件树中选择仓库`；
  - `InfoPanel`：`repo = state.detail?.repo ?? w.focusedRepo()`（`InfoPanel.vue:22`）→ 显示“未选择仓库”空态 + 一个“重试”按钮，标题却是 `RTCT_Incomplete_Design`；
  - 四仓树正确显示 MISSING（但归为“读取失败”，见 P1-5）。
- 违反 FR-ACT-01/02（禁用理由必须逐格给出“原因+替代动作+错误码”）、FR-INFO-02（不得显示上一个仓残留；未选中聚焦仓才给空态）、AC-UI-08/11。
- 1305 截图 `98-m2-final-workbench.png` 即为该状态的真实证据；Mock 侧同样复现（scope 空、aria 未选择）。

### 2.5 进行中 / 串行 / 快速连点（通过）

- 注入 700ms 延迟后采样（点击 FETCH 后 120ms）：`aria-busy=true`、`data-pending=FETCH`、按钮文本 `FETCH…`、`.spinner` 存在、其余四键 `disabled=true`（title“该仓有操作进行中”）、状态栏 `FETCH 执行中 · IMPL · 12:40:46`。
- 进行中再次点击 FETCH：`runRepoAction` 调用增量 = **1**（不重复执行）；成功 Toast 仅 1 条。
- 动作完成后：`refresh` 1 次 + `getSnapshot` 2 次（含首屏），Toast 出现耗时 223ms，刷新触发 730ms（≤2s，FR-ACT-06 通过）。

### 2.6 按钮布局/尺寸（P2）

- 1440/1280 实测：五键同排、顺序 `PULL>FETCH>CHKOUT>COMMIT>PUSH`、文本未本地化、无换行（1000px 亦不换行），F 区高 52px（≤64）。
- 但按钮实测 **96×30**（1280：96×30；1000：88×30）：高度 30 < SRS §11.0.2 F“按钮最小 88×32”与 §10.5“主按钮 ≥32px”；宽度也非 spec §6.1 的 `flex:1 min112/max220`（1440 应约 213px），呈现为居中一排小按钮，与参考图/规格的弹性布局不一致。证据：`RepoActionsBar.vue:77-99`。

---

## 3. 确认 / 输入

### 3.1 设计 vs 实现（关键冲突）

| 项 | SRS FR-ACT-04 / spec §6.3 | 实现 | 判定 |
|---|---|---|---|
| 确认形态 | COMMIT/PUSH 在按钮条上方显示 **≤48px 确认条**，不遮挡不移动五键 | 无任何 `confirm-bar` 元素；COMMIT 用 ActionDialog、PUSH/PULL 用 ConfirmDialog（模态） | ❌ P0-4 |
| FETCH | 免确认，直接执行 | 直接执行 | ✅ |
| PULL | “仅预检+入队（不弹确认）”；spec“常规操作免确认” | **弹确认框**（真实 Host 实测） | ❌ P0-4 |
| CHKOUT | 先弹分支/Tag 选择器（不弹确认） | 有选择器；但 Host 矩阵对 DIRTY/AHEAD/BEHIND/DIVERGED/DETACHED/EMPTY 的 CHKOUT 为 `C`，UI 提交不带 `confirm:true` → Host 返回 `E_CONFIRM_REQUIRED` → 再弹一次通用“需要二次确认” | ⚠️ 交互重复（P0-7） |
| PUSH | 仅“无 upstream 发布分支”需确认（spec §6.3/§6.4）；确认文案“分支 → upstream” | 所有 PUSH 都弹确认；文案显示 `refs/remotes/origin/main`，未写 `main → origin/main`，无“发布分支并建立 upstream” | ❌ P0-4 / P1-4 |
| 破坏性覆盖 | SYNC 强覆盖/Force/Reset 不进按钮条 | 按钮条只有 5 键 | ✅ |

### 3.2 PULL / PUSH 确认内容（内容本身合格）

- PULL body 含 behind 计数、`--ff-only` 说明与非快进指引，detail 为 `refs/remotes/origin/main`；PUSH body 含 ahead 计数与 `分支 main · HEAD 88a9539 · ahead 0 / behind 0`（真实 Host）。
- 问题在于“是否该弹”而不是“弹得对不对”。若产品决定保留模态确认，必须走 ADR 修订 FR-ACT-04，否则 M2 验收（确认条快照/按钮 bbox 不变）无法出证。

### 3.3 COMMIT（输入规则通过，前置/内容不通过）

- 通过：message 必填（trim 后 >0）、`maxlength=200`、计数器 `0/200`、空白串禁用提交、Ctrl+Enter 提交、提交值被 trim（实测 args.message=`fix: m2 review commit`）、初始焦点在 textarea、Tab 焦点陷阱完整（textarea→取消→提交→关闭→textarea…）。
- 不通过（P0-3）：
  1. **无 staged 仍可用**：SYNCED 仓 `未提交 已跟踪 0 · 未跟踪 0`，COMMIT enabled（真实 + Mock 均复现）；FR-ACT-03 明确“无 staged 时禁用”，AC-UI-10 为验收点；
  2. **不显示“将提交 N 个文件”/staged 文件列表**：DTO `RepoVm` 只有 `dirtyCount/untrackedCount`，无 staged 字段（`ui/src/types/workbench.ts:33-45`），UI 无数据可展示；
  3. **Host 实际行为与文案不符且更危险**：`RepoActionService.cs:450-451` 在 commit 前执行 `git add -A`，然后才 `git commit` → 会把**所有已跟踪改动 + untracked 文件**一起提交，而 UI 文案写“提交已暂存改动”。用户按“只提交 staged”的预期点击，可能提交未审阅/未跟踪文件。
- 建议：数据层补 `stagedCount/stagedFiles`（或 Host `GetAvailability` 返回 canCommit），UI 无 staged 禁用+展示 N；Host 去掉 `git add -A`（仅提交 index 中已有内容），除非产品明确改为“提交全部改动”并同步改文案与确认条。

### 3.4 CHKOUT（P0-7）

- 通过：分支/Tag Radio、必填输入、空值禁用、`maxlength=200`、ESC/取消不写。
- 不满足：
  - **数据不来自 remote_ref**：桥无 `getRemoteRefs/getBranches` 方法（`HostCommandRouter.cs` 方法清单无此方法）；UI 只用 `[repo.branch, repo.tag, compareRef 去前缀]` 生成 1–3 条 datalist 建议（`workbench.ts:1148-1151`），真实 Host 实测仅 `["main"]`；无搜索（spec §6.2/FR-ACT-03“含搜索”）；
  - **DIRTY 无 `stash -u` 勾选**：仅一行提示“若 Host 返回 E_DIRTY_WORKTREE，请先 COMMIT 或 stash 再检出”；spec §6.3 要求确认对话框内含 dirty 仓 `stash -u` 勾选 + 影响说明；
  - **二次确认冗余**：见 §3.1 CHKOUT 行（Host C 单元未由 UI 直接携带 confirm，导致“检出”后再来一次“CHKOUT 需要二次确认”）。

### 3.5 E_CONFIRM_REQUIRED 重试（通过，且只重试一次）

- 首次 `E_CONFIRM_REQUIRED` → 打开 danger 确认框；“确认并重试”后带 `args.confirm=true` 重试，调用共 2 次（Mock 记录 `[{args:{}},{args:{confirm:true}}]`）。
- 若重试后 Host 仍返回 `E_CONFIRM_REQUIRED`：错误 Toast“Host 仍要求确认，已停止自动重试”、状态栏错误码 `E_CONFIRM_REQUIRED`、通知中心 +1；调用数停在 2，无死循环。**符合“只重试一次”。**

### 3.6 ESC / 取消 / 焦点（P0-5）

| 场景 | 行为 | 判定 |
|---|---|---|
| ActionDialog ESC / 取消 | 关闭；无写调用；焦点落 `BODY` | 关闭✅ 焦点回落❌ |
| ConfirmDialog ESC / 取消 | 关闭；onCancel 回调执行（lastOp idle 恢复）；**焦点始终在 BODY，Tab 5 次全部逃逸到状态栏/菜单栏** | ❌ AC-UI-13（焦点陷阱、初始焦点） |
| ActionDialog Tab 陷阱 | 关闭按钮→textarea→取消→提交→关闭，循环不出对话框 | ✅ |
| ComponentDrawer Tab 陷阱 | 10 次 Tab 均在抽屉内 | ✅ |
| ComponentDrawer ESC | 关闭；焦点落 `BODY`（真实/隔离均复现） | ❌ AC-UI-13“焦点回落” |
| 全局 ESC | `App.vue:33-37` → `closeTopOverlay()`（`workbench.ts:1825-1861`），只关闭不恢复焦点 | 根因 |

---

## 4. 真实结果反馈

### 4.1 成功（通过）

- FETCH 实测：点击后 223ms 出现**单条**成功 Toast `✓FETCH 完成 · HEAD 1234567（42ms）`（真实截图 `90-m2-fetch.png` 为 `✓ FETCH 完成 · HEAD 85dced5（633ms）`）；无重复 ✓、无二次 Toast。
- statusbar：`✓ FETCH 成功 · 1234567 · IMPL · 12:38:27`；按钮 pending 期间 spinner/禁用/“FETCH…”。
- COMMIT/CHKOUT/PUSH 均有对应成功 Toast（`已提交 <sha>`、`已检出 <ref> · HEAD <sha>`、`PUSH 完成 · HEAD <sha>（<ms>）`），见 `91/95/96/97` 截图。
- 回写：Mock 730ms 内触发 `refresh` + `getSnapshot` + `getRepoDetail/getRepoTree`；真实 Host 在 12:31:58 FETCH 后状态计算时间同步为 12:31（≤2s 量级），FR-ACT-06 计时项通过。

### 4.2 失败（通过，缺重试/详情入口）

- 注入 `E_OFFLINE`：Toast `✕ FETCH 失败 · E_OFFLINE：网络不可达 · Mock：FETCH 失败（E_OFFLINE）。检查网络/代理后重试`；状态栏 `✕ FETCH 失败 · IMPL · E_OFFLINE`（`op-error` 红色）；通知中心未读数 3→4；按钮恢复可用。
- 注入 `E_CONFIRM_REQUIRED` 始终拒绝：Toast `✕ PUSH 失败 · E_CONFIRM_REQUIRED：Host 仍要求确认，已停止自动重试` + 通知 +1。
- 缺口（P1-3）：Toast 只有文本，无 spec §6.3 要求的“重试 / 详情”动作按钮；任务中心仍是空占位（见 §4.4），失败项无法“仅重试失败项”（FR-ACT-06/spec §6.4-6.5）。

### 4.3 刷新后看板/详情/树同步（通过）

- 动作后 `getRepoDetail`（15 次）、`getRepoTree`（55 次，含 4 面板 × 多次刷新）、`getSnapshot`（2 次）均被重新调用；真实截图 91→95 显示 PUSH 后卡片 SIL/IMPL 状态、InfoPanel、树徽标同步为“同步”。

### 4.4 状态栏 lastOp（P1-2，模板不符）

- FR-OPS-05 固定模板：`最近一次操作：<动作> <组件>/<RepoType> — <成功|失败(错误码)|进行中> · HH:mm`；未操作显示“无”。
- 实测（真实）：`✓ FETCH 成功 · 88a9539 · IMPL · 12:31:58`；未操作：`就绪 · 无最近操作`。缺“最近一次操作：”前缀、缺 componentId、成功括注不符合模板。
- 失败时“✕ FETCH 失败 · IMPL · E_OFFLINE · 12:38:58”把错误码放在尾部且缺 componentId；`workbench.ts:343-351` 为文案来源。

### 4.5 通知中心（部分通过，P2 数据可信度）

- 失败动作 +1 未读、置顶可用（Mock 验证）。
- 但通知列表初始为**固定 Mock 种子数据**（`workbench.ts` 初始 `notifications` 含“（Mock）”字样），真实 Host 模式下同样显示（真实截图 90/91 铃铛角标 2）；M2 真失败通知会混在 Mock 条目里，削弱“真实结果反馈”可信度。
- 单仓操作**未进入任务中心**（无 jobId/任务条目；`DrawerHost.vue:39-45` 仍写“批量 … 将在 M2 接入后… 取消/重试失败项 M2 接入”）。

### 4.6 Toast 细节（P2）

- 成功：单 ✓ 不重复（通过）。
- 失败：`ToastHost.vue:20` 渲染图标 `✕`，而 store 文案又以 `✕ ` 开头（`workbench.ts:1294/1301`）→ 实测 DOM 为 `✕✕ FETCH 失败…`，双 ✕ 视觉重复（截图 `m2-07-dirty-error.png` 同）。

---

## 5. M1/M2 边界与版本文案（P1-7 / P2）

| 位置 | 实测文案 | 问题 |
|---|---|---|
| Host 能力门禁 | `features.repoActions=true` → 五键按矩阵启用；`false` → 全禁用+“M1 只读”理由；点击 0 写 | ✅ 符合 AC-UI-09 |
| 关于弹窗（真实） | `M1 只读：写操作（PULL/FETCH/CHKOUT/COMMIT/PUSH）在 M2 接入；文件树与文件夹视图只读。` | ❌ M2 构建仍宣称“M2 接入”，误导用户写操作未开放 |
| 关于弹窗版本 | `版本 0.1.0.0`；标题区 `v0.3.0`（`TitleBar.vue:36`） | ❌ 版本自相矛盾 |
| 设置 | `其余项 M1 接入`、`M1 接入真实目录扫描…`、`M1 接入真实连接测试…`、`M2 接入 dry-run/备份/恢复` | ❌ M1/M2 均已过阶段，文案未更新 |
| 任务中心 | `批量 Clone / Pull / 切 Tag / SYNC / Delete 将在 M2 接入后…`、`取消 / 重试失败项 M2 接入` | ❌ 并行批次文案过时，且与 M2 已交付单仓操作混淆 |
| 菜单「操作」 | `组件 SYNC…（M2）`、`组件 CREATE…（M2）`、`批量 Clone…（M2）`、`批量 Delete…（M2）` | ❌ M2 实交付为单仓五键，批量未交付 |
| 卡片/批量条 | `SYNC（M2 接入）`、`CREATE（M2 接入）`、`Clone 缺失仓（M2 接入）` | ❌ 同上 |
| 树空态按钮 | INVALID/EMPTY 的修复按钮点击只 `notify('…：M2 接入')`（`RepoTreePanel.vue:91`） | ⚠️ 应改“v1.1/M3 接入”或禁用并说明 |
| Mock / 无 Host | 拒绝且不伪造成功 ✅：`E_NO_HOST` / `E_NOT_IMPLEMENTED` Toast；但文案 `M1 只读 / M2 需 Host 支持`、`M1 为只读模式：… 将在 M2 启用`（`workbenchMock.ts:649`） | ❌ 过时；建议改“浏览器/Mock 预览不执行写操作” |

---

## 6. 组件详情抽屉（FR-DETAIL-01 / FR-CARD-02）

| 检查项 | 实测 | 判定 |
|---|---|---|
| 双击卡片打开 | 双击 `[data-component-id=…]` → `[data-testid=component-drawer]`；不替换工作台；右键“查看组件详情”亦可打开 | ✅ |
| 数据来源 | 真实 Host：`data-source=host`、`Host 详情`；`getComponentDetail` 注入失败时 `data-source=snapshot`、`⚠ 组件详情接口不可用（E_GIT_FAILED…），已回退快照` | ✅ 失败回退明确标注 |
| 四仓表 | 4 行 `component-drawer-repo`，列：仓/状态/分支/↑↓/变更/HEAD/CI | ✅ |
| 最近提交 | 5 行；标题“最近提交（聚焦仓 Impl）”，点击 SHA 可复制 | ✅（注意是聚焦仓而非四仓，标签已说明） |
| CI 占位 | “CI 未接入（v1 占位，独立通道）”，不染色 | ✅ |
| 关闭 | ✕/遮罩/ESC 均关闭 | ✅ |
| 焦点陷阱 | 10 次 Tab 全在抽屉内 | ✅ |
| ESC 焦点回落 | `document.activeElement = BODY`（真实与隔离均复现） | ❌ P0-5 |
| 宽度 | 实测 **460px**；FR-DETAIL-01/spec §11.0.2 H 要求 **480–720px**（`ComponentDrawer.vue:204 width:460px`） | ❌ P1-1 |
| 主区 bbox | 打开前后 `.main-area` 均 1440×794，不变 | ✅ |
| ARIA | `role=dialog`、`aria-modal=true`、`aria-label=组件详情抽屉` | ✅ |

> 附注：信息面板“详情”按钮（`[data-testid=info-detail]`）打开的是**组件详情抽屉**；FR-DETAIL-02 的“单仓详情抽屉（概览/历史/差异/文件/CI 5 Tab）”未在本次 M2 范围内评测，但 M2 尚未改变该现状，建议在 M2 出口说明中显式标注“v1.1/后续”。

---

## 7. 四仓树错误 / 截断 / 文件夹视图

| 检查项 | 实测 | 判定 |
|---|---|---|
| 目录读取错误 | `getRepoTree` 注入 `E_ACCESS_DENIED` → 面板显示“✕ 无法读取目录 / E_ACCESS_DENIED · 读取目录失败…”，不是“空目录”（`RepoTreePanel.vue:150-159`） | ✅ |
| 部分目录错误 | 有 entries + error 时保留列表并显示 `[data-testid=tree-error-banner]` | ✅ |
| 节点级错误 | 目录节点显示 `!` 标记，title 含错误码（`tree-node-error`） | ✅ |
| 截断 | `truncated=true` → “已截断：仅显示前 1 项（共 2000 项，打开文件夹查看全部）” | ✅ |
| MISSING 空态 | 显示“✕ **无法读取目录** / E_REPO_NOT_LOCAL · 仓库未克隆（MISSING）”，与“未克隆”空态语义混淆；且每个面板 2 个“重试”按钮（实测一屏 8 个） | ❌ P1-5 |
| 文件夹组件四仓 chip | 真实：4 组件 × 4 chip `D/I/M/S`；Mock：14 组件 × 4 chip，颜色取通道 A token | ✅ |
| 文件夹截断 | `workspace-truncated`：“已截断：仅显示前 14 项（共 42 项）” | ✅ |
| 文件夹错误 | 注入 `E_ACCESS_DENIED` → “✕ 无法读取工作区 / E_ACCESS_DENIED …重试” | ✅ |
| 视图切换 | 工作台↔文件夹切换后 selection 保留（返回后 InfoPanel 仍是原仓） | ✅ |

MISSING 误判根因：`loadTree` 为 MISSING 设置 `status='unavailable'` 且 `error='仓库未克隆（MISSING）'`（`workbench.ts:761-775`），而 `RepoTreePanel.vue:24-35` 的 `emptyState` **先判 error/错误态**，于是 MISSING 落入“无法读取目录”分支。重复按钮来自 `RepoTreePanel.vue:145-146`：EmptyState 内容里既有 primary（key=retry 时文案“重试”）又固定追加一个“重试”。

---

## 8. 需求 / AC 对照

| 条款 | 结论 | 证据 |
|---|---|---|
| FR-ACT-01 五键/顺序/不换行 | **部分通过**：5 键顺序/英文/不换行（1440/1280/1000）✅；“未选中”提示错位（MISSING 时误用）❌；按钮 88×30 高度不足 | §2.6、§2.4 |
| FR-ACT-02 矩阵 + tooltip + M1 边界 | **不通过**：12/13 状态对；UNKNOWN FETCH 错；tooltip 无错误码、多个通用文案；M1 全禁用 ✅ | §2.1/2.2/2.3 |
| FR-ACT-03 前置 | **不通过**：PULL ff-only ✅；CHKOUT 无 remote_ref/搜索 ❌；COMMIT 无 staged 禁用 ❌；PUSH upstream/发布分支 ❌；FETCH 离线不禁用 ❌；CAS 未实现（无 E_CONCURRENT_CHANGE）❌ | §3.3/3.4、§5 |
| FR-ACT-04 确认与安全 | **不通过**：无确认条；PULL 弹确认；PUSH 常规也确认；COMMIT 无“N 个文件”；破坏性覆盖未进按钮条 ✅ | §3.1/3.2 |
| FR-ACT-05 进行中/失败/取消 | **部分通过**：spinner/串行/状态栏进行中/错误码+通知 ✅；BUSY（E_BUSY≠Host E_REPO_BUSY）/CAS ❌；合作式取消完全未提供（PULL/PUSH 同步阻塞）❌；失败可重试入口 ❌ | §2.5/4.2 |
| FR-ACT-06 完成后回写 | **部分通过**：≤2s 回写 ✅；状态栏模板 ❌；任务中心/仅重试失败项 ❌；审计 UI 不可见（后端另评） | §4.3/4.4 |
| FR-ACT-07 版本门禁 | **通过**：M1=只读全禁用+理由、点击 0 写；任何阶段无静默失败（失败均有 Toast/状态栏） | §2.3 |
| AC-UI-08 | **不通过**（UNKNOWN FETCH；MISSING 语义） | 本报告 §2 |
| AC-UI-09 | **通过**（M1 边界） | §2.3 |
| AC-UI-10 | **不通过**（无 staged COMMIT 未禁用；确认条缺失；PUSH 无 upstream 无“发布分支”提示；BUSY 未按错误码处理；失败/回写子项通过） | §3/§4 |
| AC-UI-11 | **部分通过**：E/F 同聚焦仓 ✅、抽屉 bbox 不变 ✅；抽屉宽度 ❌；ESC 焦点回落 ❌ | §3.6/§6 |
| AC-UI-13 | **不通过**（抽屉 ESC 焦点不回落；ConfirmDialog 无陷阱；6 入口本次未全量复测） | §3.6 |
| FR-OPS-05 | **不通过**（lastOp 模板） | §4.4 |
| FR-INFO-01 | **不通过**（E 区缺“最近一次操作结果”段；其余字段/CI 占位/快捷入口 ✅） | §6/§4.4 |
| FR-INFO-02 | **不通过**（MISSING 下显示“未选择仓库”空态） | §2.4 |
| FR-DETAIL-01 | **不通过**（宽度 460<480；ESC 焦点）其余内容/回退 ✅ | §6 |
| FR-TREE-02 | **部分通过**（2×2/顺序/缺仓原因显示 ✅；MISSING 空态语义错误、重复按钮 ❌） | §7 |
| FR-FOLDER-03 | **通过**（组件节点四仓 chip，真实 4/4） | §7 |
| UI plan S6 出口（前置校验/失败提示/不误伤） | **未达到**：COMMIT 可能误提交全部改动（Host `git add -A`）；确认形态与矩阵文案未闭环 | 本报告 §3.3 |

---

## 9. 缺陷清单

### P0（8 项，阻断 M2 验收）

| ID | 问题 | 位置 | 修复要点 |
|---|---|---|---|
| P0-1 | UNKNOWN 状态错误禁用 FETCH（与 §7.6/spec §6.2/Host 矩阵冲突，断恢复路径） | `viewmodel.ts:544-550,691-703`；Host `RepoActionService.cs:61` | UNKNOWN 允许 FETCH；UI/Host 矩阵单一来源或契约测试 |
| P0-2 | MISSING 聚焦仓显示“未选择仓库/单仓操作：未选择/请先在文件树中选择仓库” | `workbench.ts:300-306`；`InfoPanel.vue:22,152-160`；`RepoActionsBar.vue:12-13,42` | 区分“无选择”与“MISSING”；禁用理由“MISSING：仓库未克隆，先 Clone”；E 区显示 MISSING 信息 |
| P0-3 | 无 staged 仍可 COMMIT、无“提交 N 个文件”、Host `git add -A` 会把全部改动提交 | `viewmodel.ts:471-478`；`RepoVm` 无 staged 字段；Host `RepoActionService.cs:450-451` | DTO 加 stagedCount；无 staged 禁用并展示 N；Host 去掉 add -A 或改为显式“提交全部改动”确认 |
| P0-4 | 无 F 区 ≤48px 确认条；PULL 弹确认；PUSH 常规也确认 | `workbench.ts:1068-1102`；无 confirm-bar 组件 | 实现确认条；PULL 免确认；PUSH 仅无 upstream 发布分支确认；否则 ADR 修订 SRS |
| P0-5 | ConfirmDialog 无初始焦点/焦点陷阱；对话框/抽屉 ESC 后焦点落 BODY | `ConfirmDialog.vue:14-41`；`workbench.ts:1825-1861`；`ComponentDrawer.vue:28-30` | 打开即 focus 进对话框并陷阱；关闭时 restore 到 `document.activeElement` 记录 |
| P0-6 | 禁用 tooltip 无错误码/替代动作，多个通用文案 | `viewmodel.ts:470-551`；实测 disabledTitles | 每格输出“原因+替代动作+错误码”（如 `AHEAD：请先 PUSH；E_ACTION_NOT_ALLOWED`） |
| P0-7 | CHKOUT 选择器不来自 remote_ref、无搜索、无 stash -u；Host C 单元触发二次确认 | `workbench.ts:1148-1181`；Host 矩阵 CHKOUT=C；无 getRemoteRefs API | 新增远端 refs 契约+搜索；dirty 仓 stash 勾选；UI 提交带 confirm 消除二次弹窗 |
| P0-8 | InfoPanel 缺“最近一次操作结果”段（FR-INFO-01 P0） | `InfoPanel.vue` 全文无 lastOp | E 区第三段展示最近操作简版+失败码 |

### P1（8 项）

| ID | 问题 | 证据 |
|---|---|---|
| P1-1 | 组件详情抽屉宽 460px < 480–720（真实/Mock 均 460） | `ComponentDrawer.vue:204`；真实 bbox |
| P1-2 | 状态栏 lastOp 模板不符 FR-OPS-05（缺前缀/componentId，未操作非“无”） | 真实 `✓ FETCH 成功 · 88a9539 · IMPL · 12:31:58`；`workbench.ts:343-351` |
| P1-3 | 失败 Toast 无“重试/详情”；单仓操作未进任务中心，无逐仓结果/取消；FR-ACT-05 合作式取消未实现 | `ToastHost.vue` 全文件；`DrawerHost.vue:38-45`；无 job 桥方法 |
| P1-4 | PUSH 发布分支不建 upstream：UI 从不传 `setUpstream`（args={}），Host `push origin HEAD` | `workbench.ts:1099`；Host `RepoActionService.cs:488-496` |
| P1-5 | MISSING 树空态被显示为“无法读取目录”；每面板重复 2 个“重试” | `RepoTreePanel.vue:24-35,145-146`；`workbench.ts:761-783` |
| P1-6 | 离线降级名存实亡：`state.online` 恒 true，FETCH 不因离线禁用；UI `E_BUSY` 与 Host `E_REPO_BUSY` 不一致；CAS/`E_CONCURRENT_CHANGE` 无 Host 实现 | `workbench.ts:121,274`；`viewmodel.ts:631`；`BridgeErrors.cs:19` |
| P1-7 | M1/M2 文案与版本遗留（关于“M1 只读…在 M2 接入”、标题 v0.3.0 vs 关于 0.1.0.0、设置“M1 接入”、任务中心“M2 接入后”、菜单“（M2）”） | 真实关于弹窗；`ModalHost.vue:167-187,229-247`；`DrawerHost.vue:39-45`；`MenuBar.vue:58-61` |
| P1-8 | 错误 Toast 双 ✕（icon ✕ + 文案前缀 ✕） | `ToastHost.vue:20`；`workbench.ts:1294,1301`；DOM 实测 |

### P2（10 项）

1. 五键 96×30：高度 <32、宽度非 `flex:1 min112/max220`（§11.0.2 F / §10.5 / spec §6.1）。
2. 成功 Toast `✓FETCH` 图标与文本紧贴缺空格；失败 Toast 已双 ✕。
3. PUSH 确认 detail 用 `refs/remotes/origin/main`，未写 `main → origin/main`。
4. 通知中心初始含固定 Mock 假通知（真实模式仍显示，铃铛角标 2）。
5. 真实模式 InfoPanel “未提交”行标签语义：已跟踪/未跟踪/stash 与“staged”无关，易被误读为可提交。
6. 信息面板“未选择仓库”空态的“重试”按钮实际无效（`InfoPanel.vue:158`）。
7. 无选中态在正常流程不可达（自动选中首组件），FR-ACT-01 的“未选中提示”只会在 MISSING 时误现。
8. 树 INVALID/EMPTY 修复按钮点击仅 Toast “M2 接入”，阶段文案不实（应 v1.1/M3）。
9. 组件详情抽屉“最近提交”实际绑定聚焦仓（标题已注明），四仓对照信息仍缺。
10. 状态栏“在线”恒真（`online:true` 从不更新），网络段无信息量。

---

## 10. Top10 修复建议（按优先级）

1. **修矩阵一致性**（P0-1）：`viewmodel.ts` MATRIX 的 UNKNOWN 行 FETCH 改 `y`，移除 `normalizedState` 一刀切；新增契约单测；中期改为消费 Host `GetAvailability`。
2. **修 MISSING 语义**（P0-2）：`focusedRepo()` 区分 MISSING 与 null；按钮条 scope/aria 用实际聚焦仓；E 区显示 MISSING 卡（含 Clone 入口）；禁用理由改 `MISSING：仓库未克隆（E_REPO_NOT_LOCAL），先 Clone`。
3. **修 COMMIT 前置与安全**（P0-3）：Snapshot/DTO 增 `stagedCount/stagedFiles`；无 staged 禁用 COMMIT 并在确认条/对话框显示“将提交 N 个文件”；Host 取消 `git add -A`（或改为显式“全部改动”确认与文案）。
4. **确认形态对齐 FR-ACT-04**（P0-4）：F 区按钮上方实现 ≤48px 确认条（COMMIT 显示文件数、PUSH 显示 `分支 → upstream`/发布分支）；PULL 免确认；PUSH 仅发布分支确认；若坚持模态需先改 SRS/AC。
5. **修焦点管理**（P0-5）：ConfirmDialog 打开 focus 首按钮并加 `trapTab`；所有 overlay 打开时记录 trigger，`closeTopOverlay` 关闭后 `focus()` 回 trigger；抽屉/ActionDialog 同规则；补 CDP 断言（activeElement 在 `[role=dialog]` 内、关闭后回 `[data-testid=…]`）。
6. **tooltip 生成器**（P0-6）：每格 `原因 + 替代动作 + 错误码`，删除“当前仓库状态不允许该操作”这类通用文案；补 UNKNOWN/MISSING 专属文案。
7. **CHKOUT 选择器**（P0-7）：新增 `getRemoteRefs`（远端 branch/tag + 搜索，契约见 spec §4.4）；dirty 仓加 `stash -u` 勾选与影响说明；UI 在用户“检出”确认后带 `confirm:true`，避免第二次通用确认。
8. **信息面板/状态栏回执**（P0-8、P1-2）：E 区补“最近一次操作结果”段；状态栏严格按 FR-OPS-05 模板，未操作显示“无”。
9. **抽屉宽度与焦点回归**（P1-1、P0-5）：`width: clamp(480px, 40vw, 720px)`；补 ESC 焦点回 trigger 断言。
10. **M1/M2 文案与版本文案清理**（P1-7）：删除“M1 只读/M2 接入”过期文案；统一版本来源（标题与关于取同一 `appVersion`/产品版本）；任务中心/菜单/批量条阶段标签改为真实状态或“未交付”。

---

## 11. 复测建议（可直接补入 `ui/scripts/cdp-m2-selfcheck.mjs`）

1. 矩阵断言扩展为 14 状态 × 5 键（现脚本仅 BEHIND/MISSING 两态）；UNKNOWN 期望 FETCH=可用。
2. 新增 ConfirmDialog 断言：打开后 `document.activeElement` 在 `[data-testid=confirm-dialog]` 内；Tab×N 不逃逸；ESC 后 activeElement 回触发控件。
3. 新增抽屉宽度断言 `480 <= rect.width <= 720` 与 ESC 焦点回落。
4. 新增 COMMIT 前置断言：`stagedCount=0` 时 `action-commit.disabled===true` 且 title 含“staged/暂存”原因；对话框文本包含“将提交 N 个文件”。
5. 新增 PULL 无确认断言（`action-pull` 点击后无 `[data-testid=confirm-dialog]` 且 `runRepoAction` 被调用）；PUSH 仅无 upstream 出现确认。
6. 新增 CHKOUT 数据源断言：`#action-ref-suggestions option` 数量=远端 refs 数（按夹具），且存在搜索输入；DIRTY 仓对话框含 stash 选项。
7. 新增失败 Toast 双 ✕ 回归断言（`toast .toast-text` 不以 ✕ 开头）与 `重试/详情` 动作存在性。
8. 新增 statusbar 模板正则：`^最近一次操作：.* (PULL|FETCH|CHKOUT|COMMIT|PUSH) .+/.+ — .+ · \d{2}:\d{2}$`。
9. 新增 MISSING 语义断言：E 区不含“未选择仓库”、按钮 scope 非空、树空态标题为“未克隆”、`repo-tree-empty button` 数量=1。
10. 新增 M2 文案扫描：真实 Host 页面全文不得出现“M1 只读”“M1 接入”“将在 M2 接入”（允许出现在开发 mock 脚本中）。

---

## 附录 A：关键实测值

| 项 | 值 |
|---|---|
| 真实 Host | 4 组件 / 14 仓；`features.repoActions=true`；`appVersion=0.1.0.0`；标题 v0.3.0 |
| 真实 SYNCED 五键 | `[PULL,FETCH,CHKOUT,COMMIT,PUSH] = [false,false,false,false,false]`（即全部 enabled） |
| 真实 MISSING 五键 | 全部 disabled，title `请先在文件树中选择仓库`；scope `""`；aria `单仓操作：未选择` |
| 真实 drawer | 460×861，4 行仓表，5 提交，CI 占位，`source=host`；ESC 后 activeElement=BODY |
| 真实 PULL 确认 | `PULL · 快进拉取？… behind 0 … --ff-only … refs/remotes/origin/main`（取消=0 写） |
| 真实 PUSH 确认 | `PUSH · 推送当前分支？… 分支 main · HEAD 88a9539 · ahead 0 / behind 0`（取消=0 写） |
| 真实 CHKOUT 建议 | `["main"]` |
| 真实文件夹 chips | 4 组件 × `D/I/M/S`；无截断/错误 |
| Mock FETCH | 223ms 出单条 `✓FETCH 完成 · HEAD 1234567（42ms）`；刷新 730ms；pending 时其余 4 键禁用、状态栏“执行中” |
| Mock 连点 | 700ms 延迟下连点 2 次 → `runRepoAction` 增量 1 |
| Mock E_CONFIRM | 首次+带 confirm 重试=2 次；始终拒绝时停在 2 次并报错 |
| Mock 失败 | `E_OFFLINE` Toast/状态栏/通知 +1；`E_NO_HOST`/`E_NOT_IMPLEMENTED` 均拒绝、无假成功 |
| Mock 抽屉 | 460×900；4 仓行/5 提交/CI；焦点陷阱通过；fallback `快照回退`+错误注记 |
| Mock 树 | error 空态含 `E_ACCESS_DENIED` 且非“空目录”；truncated `共 2000 项`；节点 `!` 含错误码 |
| Mock 文件夹 | 14 行 × 4 chip；`已截断：前 14 / 共 42`；工作区错误 `E_ACCESS_DENIED` |
| 布局 | F 区 52px；按钮 96×30（1440/1280）、88×30（1000）；1440/1280/1000 `scrollWidth==clientWidth` |
| 版本 | 真实 title `v0.3.0`；关于 `0.1.0.0` |

## 附录 B：未覆盖 / 待裁决

1. 真实 Host 的失败路径（E_NOTHING_TO_COMMIT / E_BUSY / E_CONCURRENT_CHANGE）未在真实仓库触发（避免写操作），仅做静态映射核对与注入桥验证；建议在隔离夹具仓补充真实 E2E。
2. 合作式取消（FR-ACT-05）与任务中心（FR-ACT-06/spec §6.4-6.5）当前无桥方法/jobId，属于**契约级缺口**，需要产品裁决 M2 是否降级并在 AC 中标注。
3. 6 个二级入口/抽屉的 AC-UI-13 全量键盘复测未在本轮完成（仅抽屉焦点部分），建议与抽屉焦点修复一并回归。
4. 并发 CDP 会话（另一评审者）存在，本轮真实 Host 断言均 2 次复现且隔离实例覆盖矩阵；若需单人复跑，请先确认 9222 无其他自动化在操作。
