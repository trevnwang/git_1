# M2 单仓操作 UX 修复复评（P0 闭环 + 回归/新增问题）

> 复评日期：2026-11-14 13:10–13:45（本地）
> 复评对象：上一轮 `docs/review-m2-ux.md` 的 8 个 P0，在修复提交 `9614772`（+`06c6ea2` 截图）后的实际状态，及修复引入的回归/新问题。
> 只读输入：`docs/review-m2-ux.md`、`docs/m2-ops-status.md`、`docs/images/m2/{missing-focus,staged-commit,pull-direct,component-drawer}.png`；代码 `ui/src/{viewmodel.ts,stores/workbench.ts,api.ts}`、`ui/src/components/workbench/{InfoPanel,RepoTreePanel,RepoActionsBar}.vue`、`ui/src/components/overlays/{ActionDialog,ConfirmDialog,ComponentDrawer,ToastHost}.vue`、Host `RepoActionService.cs`/`HostCommandRouter.cs`/`LocalScanner.cs`（只读核对）。
> 运行输入：①真实 Host（PID 5348，`src/MbdGitTool.Host/bin/Debug/net10.0-windows/MbdGitTool.Host.exe`，12:56:58 启动，CDP 9222，`https://app.local/index.html`，真实 Gitea `RTCRepos` + `artifacts/real-test/workspace`，只读探测，未执行任何写操作）；②隔离 Mock（本机 headless Edge 9335 + 静态服务 `ui/dist`（12:52:13 构建，`index-DYFVNb00.js`）+ 注入 `window.__DSH_TEST_BRIDGE__` 的 17 组件/13 主状态 + staged/详情失败变体快照，34 项 CDP 断言）。
> 硬性约束：本轮**只评审、未改任何代码/构建产物/需求文档**；仅新增本报告与 `docs/images/m2/fixcheck-*.png` 证据截图。
> 并行说明：目录内另有并行需求侧评审 `docs/review-m2-req.md`；本报告独立完成（自建 Mock 矩阵与真实 Host 只读探测），未复用其结论。
> 证据脚本（gitignore 的 artifacts 下）：`artifacts/ui-review/fixcheck/{mock.mjs,probe-real.mjs,probe-real2.mjs,probe-real3.mjs,server.mjs}`、结果 `mock-result.json`/`real2.json`/`real3.json`、日志 `selfcheck-run.log`/`dotnet-test.log`。

---

## 0. 结论（放行判决）

| 维度 | 结论 |
|---|---|
| 上轮 8 个 P0 | **完全闭环 2 项**（P0-1 UNKNOWN/FETCH、P0-8 InfoPanel 最近操作）；**部分修复 4 项**（P0-2、P0-3、P0-4、P0-5）；**未修 2 项**（P0-6 tooltip 错误码、P0-7 CHKOUT 数据源/搜索/stash）。 |
| 新 P0 | **1 项**：`PUSH` 由“总确认”改为“零确认直接写远端”，且无 SRS 要求的 ≤48px 确认条/未确认 0 写证据（SRS FR-ACT-04 + AC-UI-10 未过，且未走 ADR/需求变更）。 |
| 修复引入的回归 | ① `ConfirmDialog` 焦点陷阱实现失效（danger 按钮选择器错误）→ 键盘 Tab 仍逃逸到状态栏（AC-UI-13 仍不通过）；② `ComponentDrawer` 关闭后焦点仍落 `BODY`；③ `PUSH` 免确认 + `Alt+5` 快捷键可无确认触发写远端；④ 自测脚本 `ui/scripts/cdp-m2-selfcheck.mjs` 未随行为更新，运行即崩（详见 §3.7）。 |
| 已确认修复 | UNKNOWN 允许 FETCH（UI+Host 对齐）；MISSING 聚焦语义/scope/aria/InfoPanel 已正确；COMMIT staged 门禁全状态生效、Host 只提交 index；PULL 免确认；ActionDialog 焦点陷阱+归还；抽屉宽度 520px；InfoPanel 最近操作字段；信息面板无横向溢出；成功 Toast 单 `✓`。 |
| **M2 可放行** | **❌ 不可放行**。见 §7：至少需再闭环 P0-4（含新 P0-1）、P0-5、P0-6、P0-7、P0-3 的对话框内容，并给出可复跑回归证据后才能复评。 |

---

## 1. 复评方法与证据分层

| 层 | 做法 | 证据 |
|---|---|---|
| A. 真实 Host 只读实测（CDP 9222） | 读快照/按钮矩阵/InfoPanel/树空态；选中 MISSING/SYNCED 组件；打开 COMMIT/CHKOUT 对话框后 ESC；双击卡片开抽屉后 ESC；打开帮助/关于、通知中心。**未点击任何执行性按钮**（PULL/PUSH/COMMIT/CHKOUT 均未在真实仓库执行）。 | `probe-real*.mjs`、`real2.json`/`real3.json`、`docs/images/m2/fixcheck-{missing-focus,drawer,about-version,notifications-mock}.png` |
| B. 隔离 Mock 全矩阵 | 自建 headless Edge（9335）+ `ui/dist` 静态服务（127.0.0.1:5199）+ 注入 M2 契约桥；17 个组件覆盖 13 主状态 + staged 变体 + 长字段 + `getRepoDetail` 失败注入；真实鼠标/键盘事件。 | `mock.mjs`、`mock-result.json`（34 项断言，23 通过 / 11 项为缺口证据）、`docs/images/m2/fixcheck-*.png` |
| C. 代码/契约核对 | UI 矩阵、按钮 title 生成、ConfirmDialog/ActionDialog/ComponentDrawer 焦点、Host 矩阵与 COMMIT 实现、桥方法清单。 | 文中文件:行号 |
| D. 既有证据复用 | 上轮报告截图、`docs/m2-ops-status.md` 的真实写操作结果、真实夹具仓库 git 历史。 | `staged-commit.png`、`pull-direct.png`、`RTCT_Demo_SIL` 的 `git show e67eb05` |

**限制/缺口**
1. 为避免误写真实仓库，PULL/PUSH/COMMIT 的“点击即写”链路只在 Mock 验证；真实 Host 只验证“点击前状态”。真实写证据来自 `docs/m2-ops-status.md` 与夹具仓库历史（可复核）。
2. `dotnet test` 本轮**无法复跑**：运行中的 Host（PID 5348）锁定 `bin` 下的 `MbdGitTool.Core.dll`，构建报 `MSB3027/MSB3021`（见 `dotnet-test.log`）。Host 修复以源码 + 既有单测代码 + 真实提交产物三重佐证（§2.3）。
3. 项目自带自测脚本 `ui/scripts/cdp-m2-selfcheck.mjs` **已失效**（运行即崩，见 §3.7），无法作为回归证据；UI 目录无任何单元测试（`ui/package.json` 无 test script）。
4. 评审期间同一真实 Host 页面可能被其他评审会话操作；真实断言均以只读方式 2 次以上复现。

---

## 2. 8 个 P0 逐项复评

> 判定口径：**已修**=上轮缺陷行为不可复现且证据齐全；**部分修**=主路径修复但残留仍属 P0 的缺口；**未修**=行为基本未变。`证据` 均含选择器/实测值/行号。

### 2.0 P0 闭环表

| # | 上轮 P0 | 判定 | 关键证据 | 剩余差距是否仍 P0 |
|---|---|---|---|---|
| P0-1 | UNKNOWN 误禁 FETCH | ✅ **已修** | Mock 矩阵 `C_UNKNOWN`：`fetch.disabled=false`，title `拉取远程引用，不作合并 · Mock_IMPL（免确认，直接执行）`；其余四键禁用 title `状态未知：数据过期，请先 FETCH / 重试扫描`；17 组件×5 键矩阵全对（`mock-result.json#matrix16Components` 通过）；代码 `viewmodel.ts:546-553` 与 Host 矩阵 `RepoActionService.cs:65`（`UNKNOWN=NYNNN`）一致 | 否（矩阵双源为 P1，见 §4） |
| P0-2 | MISSING 被当“未选择” | 🟡 **部分修（核心已修）** | 真实：`[data-testid=repo-actions]` `data-action-scope="DESIGN"`、`aria-label="单仓操作：Design"`、五键 title 全为 `MISSING：仓库未克隆`；`info-repo-title=RTCT_Incomplete_Design`、不含“未选择仓库”空态。Mock 同（`matrix16Components/p0-2-*` 通过） | **核心否**；错误路径仍有残留（详情接口失败时 InfoPanel 显示上一仓，P1），树空态语义仍错（P1） |
| P0-3 | 无 staged 仍可 COMMIT；Host `git add -A` | 🟡 **部分修** | ✅ Host 只提交 index：`RepoActionService.cs:445-457`（`git diff --cached --quiet`，无 add）；真实夹具 `RTCT_Demo_SIL` 提交 `e67eb05` 仅含 `component.txt`（`git show --stat`）。✅ UI 门禁：真实 `RTCT_Synced_Impl`（stagedCount=0）COMMIT `disabled=true`，title `无已暂存改动：请先 git add 暂存后提交`；Mock `C_SYNCED/C_MIXED/C_EMPTY` 禁用、`C_STAGED/C_DIRTY/C_LONG` 启用（`matrix16Components` 通过） | **是**：COMMIT 对话框仍无“将提交 N 个文件”/staged 列表（见 §2.3），且 UI 无暂存入口/暂存文件列表，“禁用后无路可走”（P1 新增） |
| P0-4 | 无确认条；PULL 误确认；PUSH 常规也确认 | 🟡 **部分修 + 新 P0** | ✅ PULL 免确认：真实截图 `pull-direct.png`（单条 `✓ PULL 完成 · HEAD 037211d（492ms）`、无 confirm-dialog）；Mock `p0-4-pullDirectNoConfirm` 通过（`runRepoAction` 收到 `args={}`）。❌ 确认条：全仓 grep 无 `confirm-bar`/`ConfirmBar`；Mock/真实 `document.querySelector('[data-testid=confirm-bar]')===false`；❌ **PUSH 现为“零确认”**：Mock 点击 `action-push` 直接产生 `runRepoAction(PUSH,args={})` 调用（`p0-4-pushNoConfirmNoBar` 通过=缺陷证据）；`App.vue:58-62` 的 `Alt+5` 同样直接执行 | **是**：SRS FR-ACT-04（P0）要求 COMMIT/PUSH 有 ≤48px 确认条、“未确认时写操作数=0”；AC-UI-10 同。PUSH 零确认在 SRS 未改前为**新 P0 安全缺口** |
| P0-5 | 焦点陷阱/焦点归还 | 🟡 **部分修** | ✅ ActionDialog：真实 CHKOUT 打开 `focusInside=true`、active=`action-dialog-ref`；Tab×9 全在对话框；ESC 后 active=`action-chkout`（Mock 同）。✅ 抽屉 Tab 陷阱：Tab×12 全在抽屉。❌ ConfirmDialog：真实/ Mock 打开（`FETCH 需要二次确认`）后 `focusInside=false`、active 仍是 `action-fetch`，`.btn.primary` 不存在（按钮 class=`btn danger`）→ Tab 序列 `action-chkout→action-push→…→status-notify` 逃逸（`ConfirmDialog.vue:16`）。❌ 抽屉关闭后 `document.activeElement=BODY`（真实+Mock；`ComponentDrawer.vue:28-30` 只 focus 不记录/归还） | **是**：ConfirmDialog 与抽屉两项仍直接违反 AC-UI-13；修复实现存在但 ConfirmDialog 对当前唯一形态（danger）无效 |
| P0-6 | 禁用 tooltip 无错误码/替代动作 | ❌ **未修** | 85 个按钮 title 中 **0 个**含错误码（Mock 扫描：`titlesWithErrorCode=0`；`genericCount=7` 为 `当前仓库状态不允许该操作`）；真实 title 例：`AHEAD：本地领先，需先推送`、`MISSING：仓库未克隆`、`无已暂存改动：请先 git add 暂存后提交`——有原因无 `E_*`、无替代动作入口。Host 已在 `ComponentDetailDto` 暴露 `Actions{ReasonCode,Reason}`（`WorkbenchDto.cs:20-53`），UI 未消费（`api.ts` 无 availability 解析） | **是**（FR-ACT-02 P0；`docs/m2-ops-status.md` 自称“P1 待补”，但未走需求降级） |
| P0-7 | CHKOUT 无 remote_ref/搜索/stash | ❌ **未修** | 桥无 `getRemoteRefs/getBranches`：`api.ts:251-262` RequestMethod 11 项、`HostCommandRouter.cs` case 11 项均无；真实 CHKOUT 建议 `["main"]`、Mock `["main"]`；无 `input[type=search]`、无 `input[type=checkbox]`（`checkboxes=0`）；dirty 仓仅提示 `若 Host 返回 E_DIRTY_WORKTREE，请先 COMMIT 或 stash 再检出`（`workbench.ts:1122-1125`）。唯一改善：UI 提交带 `confirm:true`，Host `E_CONFIRM_REQUIRED` 二次弹窗消失（`workbench.ts:1150`） | **是**（FR-ACT-03 P0；需新增桥契约，非 UI 单侧可修） |
| P0-8 | InfoPanel 缺“最近操作” | ✅ **已修** | 真实：`[data-testid=info-panel] [data-testid=info-last-op]` 存在，值 `最近操作 就绪 · 无最近操作`；执行 FETCH 后 Mock 实测更新为 `最近操作 ✓ FETCH 成功 · 1234567 · IMPL · 13:17:34`（`p0-8-infoLastOpPresent` 通过；`InfoPanel.vue:194-197`） | 否（模板/格式问题降 P1-2） |

**小计：完全闭环 2/8；部分修 4/8；未修 2/8；新 P0 1 项（PUSH 零确认）。**

### 2.1 P0-1（已修）补充：矩阵一致性只修了“格子”，没修“双源”

- UI 仍是第二套矩阵（`viewmodel.ts:472-554`），Host 仍是第一套（`RepoActionService.cs:49-66`），且 Host 的 `GetAvailability/ReasonCode` 仍未进按钮条（快照通道无 availability；`getComponentDetail` 内的 `Actions` 未被 UI 解析）。上轮修复建议的“UI 消费 Host 矩阵或加契约测试”**未做**：UI 无单测，自测脚本只覆盖 BEHIND/MISSING 两态且已失效。
- 因此 P0-1 的“格子”回归风险可控（本轮 17 组件全对），但结构性根因留给后续（P1，见 §4-N1）。

### 2.2 P0-2（部分修）：核心语义已对，缺两个边角

- **已修证据**：真实 MISSING 聚焦（`docs/images/m2/fixcheck-missing-focus.png`）：scope `DESIGN`、aria `单仓操作：Design`、五键 title `MISSING：仓库未克隆`、InfoPanel 标题 `RTCT_Incomplete_Design`、无“未选择仓库”空态；Mock `p0-2-missingScopeAria/p0-2-missingReasons/p0-2-missingInfoNotUnselected` 全通过。
- **残留 1（P1）**：`loadDetail()` 异常时 `state.detail` 不清空（`workbench.ts:905-909`），而 `InfoPanel.vue:22` 优先用 `state.detail?.repo`。Mock 注入 `getRepoDetail=E_GIT_FAILED` 后，聚焦 DESIGN 仓时 scope=`DESIGN` 但 InfoPanel 仍显示上一仓 `Mock_IMPL`（`mock-stale-detail.png`、`evidence.staleDetail={beforeSwitch:"Mock_IMPL",afterScope:"DESIGN",afterInfoTitle:"Mock_IMPL"}`）。Host `RepoDetailService` 在 git 失败时确实返回 `E_GIT_FAILED`，属可达路径；违反 FR-INFO-02“不得显示上一个仓残留”。
- **残留 2（P1）**：MISSING 仓树空态仍是“✕ 无法读取目录 / E_REPO_NOT_LOCAL · 仓库未克隆（MISSING）”，且唯一按钮“重试”是空操作（`loadTree` 对 MISSING 直接 return，`workbench.ts:764-772`），无 Clone 入口（`RepoTreePanel.vue:24-43,142-148`）。上轮 P1-5 只修了“重复两个重试”。

### 2.3 P0-3（部分修）：安全修复到位，但确认内容仍不满足 FR-ACT-04

- **Host 修复可复核**：`CommitAsync` 先 `git diff --cached --quiet`，`Success` 即 `E_NOTHING_TO_COMMIT`，从不 `git add -A`（`RepoActionService.cs:445-457`）；夹具仓 `RTCT_Demo_SIL` 的提交 `e67eb05`（说明 “fix: staged-only commit via UI”）`git show --stat` 仅 1 文件 `component.txt`；单测 `Commit_UnstagedChanges_ReturnsNothingToCommit`/`Commit_NothingToCommit_ReturnsNothingToCommit` 存在（`RepoActionTests`）。
- **门禁覆盖全部状态**：17 组件矩阵中 COMMIT 为 y 的状态（SYNCED/DIRTY/EMPTY）在 staged=0 时全部禁用，staged>0 的 `C_STAGED/C_DIRTY/C_LONG` 全部启用（`matrix16Components` 通过；`viewmodel.ts:699-707`）。
- **仍未 P0**：`openCommitDialog` 的 info 只有“仓库/分支/未提交 已跟踪 N · 未跟踪 M”，**没有“已暂存/将提交 N 个文件”或 staged 文件列表**（`workbench.ts:1097-1105`）。Mock 打开对话框实测：`C_STAGED`（staged=3）对话框 info=`["仓库Mock_IMPL","分支main","未提交已跟踪 2 · 未跟踪 1"]`、全文无“已暂存 3/将提交 3 个文件”（`p0-3-commitDialogShowsStagedCount` 失败，`fixcheck-commit-dialog-staged.png`）。这直接违反 FR-ACT-04 的“将提交 N 个文件”和 spec §6.3 的“staged 文件列表”；且“未提交 已跟踪 2 · 未跟踪 1”在 staged 提交语境下**容易被误读为提交范围**。
- **新增 P1**：UI 无任何暂存入口/暂存文件列表（全 DOM 按钮无“暂存/Stage”，`p0-3-stagingEntryExists` 失败）。禁用提示 `请先 git add 暂存后提交` 把用户导向 CLI，M2 的“UI 内提交闭环”不存在，需产品明确“外部暂存”口径或补入口。

### 2.4 P0-4（部分修 + 新 P0）：确认形态只完成了“减法”

- **PULL**：真实截图 `pull-direct.png` 单条 `✓ PULL 完成 · HEAD 037211d（492ms）`、无确认框；Mock `runRepoAction(PULL,args={})`。符合 SRS“PULL 仅预检+入队（不弹确认）”，**不构成回归**。
- **确认条仍未实现**：全仓无 `confirm-bar` 组件；F 区（`[data-testid=repo-actions]`）高 52px，按钮上方无任何确认条；AC-UI-10 的“Given COMMIT/PUSH，Then 确认条出现且未确认时写操作数=0”无法出证。
- **新 P0：PUSH 零确认**。修复删除了 PUSH 的模态确认（`workbench.ts:1070-1073` 直接 `executeRepoAction('PUSH',{},false,target)`），Mock 点击即产生写调用；`App.vue:58-62` 的 `Alt+5` 同理。规格冲突如下（必须裁决，不能默认）：
  - SRS `requirements.md:610` FR-ACT-04（P0）：“COMMIT/PUSH …须在按钮条上方显示 ≤48px 的确认条…确认后才执行”；AC-UI-10 同。
  - spec `ui-redesign-spec.md:563,578` §6.3/§6.4：“常规操作免确认；CHKOUT dirty、COMMIT、发布分支例外”。
  - 修复选择了 spec 口径，但**未提交 ADR、未回写 SRS/AC**（`docs/` 无 ADR 变更记录），因此按冻结的 SRS，PUSH 从“有确认”退化为“零确认”属于**验收面回归**；且“无 upstream/发布分支”确认分支实际不可达（Host PUSH 单元从不为 C，UI 也从不传 `setUpstream`，`api.ts:211-217`）。建议：要么实现确认条，要么走 ADR 修订 FR-ACT-04/AC-UI-10 并明文接受“PUSH 免确认”。

### 2.5 P0-5（部分修）：ActionDialog 已修，ConfirmDialog 修复失效、抽屉未归还

| 场景 | 实测（真实/Mock） | 判定 |
|---|---|---|
| ActionDialog（CHKOUT）初始焦点 | active=`action-dialog-ref`，`focusInside=true` | ✅ |
| ActionDialog Tab×9 | 全部在 `[data-testid=action-dialog]` 内 | ✅（`ActionDialog.vue:77-97`） |
| ActionDialog ESC 后 | active=`action-chkout` / `action-commit`（回触发按钮） | ✅ |
| ConfirmDialog 初始焦点 | **不在对话框**：active=`action-fetch`；`primaryExists=false, dangerExists=true`（`autofocus` 动态节点无效） | ❌ |
| ConfirmDialog Tab×6 | `action-chkout → action-push → … → status-notify`（3 次逃逸后再进入对话框） | ❌ |
| ConfirmDialog ESC/取消 | 可关闭，且 focus 回 `action-fetch`（`previousFocus` 生效） | ✅ |
| ComponentDrawer 打开焦点 | `component-drawer-close`，Tab×12 不逃逸 | ✅ |
| ComponentDrawer ESC/✕ 关闭后 | `document.activeElement = BODY`（真实 520px 抽屉同） | ❌ |

- **根因（可直接修）**：`ConfirmDialog.vue:16` `dialogEl.querySelector('.confirm-actions .btn.primary')?.focus()`，而模板 `:class="danger ? 'danger' : 'primary'"`（`:76-79`）——当前所有 `openConfirm` 调用都传 `danger:true`（危险文件、`E_CONFIRM_REQUIRED`、批量 DELETE），因此**没有任何一次能命中初始焦点**；焦点不在 section 时，绑在 section 上的 `@keydown` 收不到 Tab，陷阱形同虚设。
- **抽屉根因**：`ComponentDrawer.vue:28-30` 仅 `watch(open)→closeBtn.focus()`，未记录 `previousFocus`、关闭时无 `focus()` 归还（ActionDialog/ConfirmDialog 反而都有）。

### 2.6 P0-6（未修）与 P0-7（未修）

- P0-6：见 2.0。补充可落地路径：Host `getComponentDetail` 已在返回 `Actions[{Action,Enabled,MatrixAllowed,ReasonCode,Reason,RequiresConfirm}]`（`WorkbenchDto.cs:20-53`），但快照通道没有，UI 也未解析；至少可先把 `ReasonCode` 映射成 `E_ACTION_NOT_ALLOWED`/`E_CONFIRM_REQUIRED` 等错误码并拼接替代动作文案。
- P0-7：本轮实测再次确认真实 Host `#action-ref-suggestions` 仅 `["main"]`（`fixcheck-checkout-dialog.png`），没有搜索与 stash 勾选；桥契约缺 `getRemoteRefs`，Host 侧也没有对应 CommandRouter case，因此**不是 UI 一处能闭环**，必须在契约层排期。

---

## 3. 回归与新增问题核查（按任务要求逐项）

### 3.1 PULL/PUSH 免确认是否违反安全/误操作风险

- PULL：`--ff-only` 且 SRS 明确免确认 → **通过，不视为回归**（真实 F 区点击直接执行，动作后回写正常）。
- PUSH：**回归/新 P0**。理由：① 远端写操作从“模态确认”变为“单击即推”；② `Alt+5` 全局快捷键同样无确认（`App.vue:58-62`），误触概率高于按钮；③ 无确认条、无“分支 → upstream / 将推送 N 个提交”文案；④ `NO_UPSTREAM` 首推也不建 upstream（UI 从不传 `setUpstream`，Host 虽支持 `-u`），首推后状态仍显示“仅本地”，用户可能重复推送。建议按 §2.4 的 ADR 路径二选一。

### 3.2 COMMIT staged 门禁是否覆盖所有状态

- **覆盖**：17 组件 × 5 键矩阵全对（`matrix16Components` 通过）；COMMIT 为 y 的 SYNCED/EMPTY/DIRTY 在 staged=0 时禁用（含补槽位/合成 MISSING 仓），staged>0 时启用；pending 时其余键禁用逻辑未变。
- **未覆盖**：门禁只读快照（无 FR-ACT-03 的“点击前 read-through 重扫 + CAS”）；外部 `git add` 后不刷新仍禁用 COMMIT，需用户先手动刷新（P1）；对话框不显示 N（§2.3）；`RepoActionService` 从不返回 `E_REPO_BUSY`（`BridgeErrors.cs:19` 成死码），UI 映射表也没有 `E_REPO_BUSY`（只有 `E_BUSY`，`viewmodel.ts:634`），`E_CONCURRENT_CHANGE` Host 无实现（P1-6 未修）。

### 3.3 MISSING 聚焦后按钮 tooltip/scope/aria

- scope/aria/InfoPanel：**正确**（见 2.2）。
- tooltip：语义正确但**无错误码/替代动作**（`MISSING：仓库未克隆`，应为 `MISSING：仓库未克隆（E_REPO_NOT_LOCAL），先 Clone`）；属 P0-6 未修。
- 树：MISSING 仍被渲染成“错误态”，按钮空操作（见 2.2 残留 2）。

### 3.4 焦点陷阱：ConfirmDialog / ActionDialog / ComponentDrawer / Esc / Tab / 关闭后

见 §2.5 表。汇总结论：ActionDialog ✅；ConfirmDialog ❌（初始焦点+Tab 陷阱）；ComponentDrawer 陷阱 ✅ 但关闭后焦点 ❌（`BODY`）。**AC-UI-13 仍不通过。** 补充：`ModalHost.vue:76-102` 已有 `focusFirst/trapTab`，但 `DrawerHost.vue`（通知中心/任务中心）无陷阱/归还（未纳入本轮 P0，P2 提示）。

### 3.5 Toast 单 ✓ / 双 ✕

- 成功：真实 `✓ PULL 完成 · HEAD 037211d（492ms）`、Mock `checkMarks=1`（`toastSingleCheckMark` 通过）。
- 失败：**仍双 ✕**。Mock 实测 DOM `toast-icon=✕` + `toast-text="✕ FETCH 失败 · E_OFFLINE…"`，textContent=`✕✕ FETCH 失败…`（`p1-8-errorToastSingleCross` 失败；`fixcheck-stale-detail.png` 截图可复现）。P1-8 未修。

### 3.6 信息面板字段溢出

- 无横向溢出：Mock 1440（panel 253 / body sw=241=cw）、1000（panel 208 / 196=196）、真实会话 1424×861（panel 251 / body 239=239）；所有 `.field` 子元素未越界（`infoPanelNoHorizontalOverflow`、`infoPanelFieldsContained` 通过）。长分支名/长路径按 `.truncate`+`title` 截断，合规。
- 可读性残留（P2）：“最近操作”值在 239px 栏宽下只显示约 14 字（`✓ COMMIT 成功 · e67eb05 ...`，完整值仅 title）；“未提交 已暂存 0 · 已跟踪 0 · 未跟踪 0 · stash 0”在窄栏折行；MISSING/UNKNOWN 仓仍显示全零“未提交”、本地路径等字段（易误读为可操作）。

### 3.7 新发现的其他回归/证据链问题

1. **自测脚本失效（P1）**：`ui/scripts/cdp-m2-selfcheck.mjs` 在本轮 Mock 实例上运行即崩：
   `Error: missing [data-testid="action-dialog-message"]` at `setValue`（脚本 `:573`）——因为脚本的假桥未提供 `stagedCount`，COMMIT 现在禁用、对话框打不开；即使修复此处，脚本 `:596-629` 仍断言“PULL/PUSH 弹确认对话框”，与修复后行为相反。该脚本**不能作为 P0-4/P0-3 的回归证据**，且只覆盖 BEHIND/MISSING 两态。
2. **版本号仍自相矛盾（P1-7 残留）**：真实标题 `v0.3.0`，关于弹窗 `版本 0.1.0.0`（`fixcheck-about-version.png`、`ModalHost.vue:242` 取 `hostInfo.appVersion`）。关于正文已更新为“M2 已启用…批量与 CI 在 M3/v2 接入”（上轮 P1-7 的关于项✅），但设置页仍有“其余项 M1 接入”“M1 已接入真实目录扫描”，任务中心/菜单/批量条仍是“M2 接入”占位。
3. **状态栏模板未变（P1-2）**：真实 `就绪 · 无最近操作`、`✓ FETCH 成功 · 1234567 · IMPL · 13:17:43`；无“最近一次操作：”前缀、无 componentId、成功无括注（`workbench.ts:341-349`）。
4. **真实模式仍显示 Mock 通知（P2）**：通知中心顶部“2 条未读 · 应用内通知（Mock）”，条目均带“（Mock）”（`fixcheck-notifications-mock.png`，`mockCount=4`）。
5. **`online` 恒 true（P2）**：`workbench.ts:274`，状态栏“在线”无信息量，FETCH 离线不禁用（FR-ACT-03 子项）。
6. **CHKOUT 确认标志漂移（P2）**：UI 矩阵把 SYNCED 的 CHKOUT 标为 `c`（`viewmodel.ts:473`），Host/SRS 为免确认 `Y`；按钮 title 因此写“执行前弹出确认/输入对话框”，但实际只弹选择器（不弹确认）。违反 §7.6 末条“禁止另写一套判断”的口径，属矩阵双源的一部分。
7. **Host COMMIT message 上限 2000 ≠ SRS 200（P2）**：UI `maxlength=200` 兜底，但契约层不一致（`RepoActionService.cs:32-33` 注释自认 200 为 UI 约束）。

---

## 4. 新缺陷清单

### 新 P0（1 项）

| ID | 问题 | 证据 | 建议 |
|---|---|---|---|
| NP0-1 | **PUSH 零确认直接写远端**（无 ≤48px 确认条、无“分支 → upstream/将推送 N 个提交”文案、无发布分支提示；`Alt+5` 亦可触发），SRS FR-ACT-04 + AC-UI-10 未过且无 ADR 修订 | §2.4；`workbench.ts:1070-1073`；`App.vue:58-62`；Mock `p0-4-pushNoConfirmNoBar`/`p0-4-confirmBarAbsent`；全仓无 `confirm-bar` | 二选一：①实现 F 区上方 ≤48px 确认条（COMMIT 显示 N 文件、PUSH 显示 `分支 → upstream`，未确认 0 写、按钮 bbox 不变）；②提交 ADR 明确“常规 PUSH 免确认”并回写 FR-ACT-04/AC-UI-10（含 `Alt+5` 风险说明）；若采用②，NO_UPSTREAM 仍必须确认“发布分支并建 upstream” |

### 新 P1（影响可用性/验收）

| ID | 问题 | 证据 |
|---|---|---|
| N1-1 | ConfirmDialog 焦点陷阱修复失效（danger 选择器），键盘用户打开二次确认时焦点仍在背景按钮、Tab 逃逸 | §2.5；`ConfirmDialog.vue:16,76-79`；`fixcheck-confirm-dialog-focus.png` |
| N1-2 | ComponentDrawer 关闭后焦点落 `BODY`（无 previousFocus 归还） | §2.5；`ComponentDrawer.vue:28-30` |
| N1-3 | COMMIT 对话框缺“已暂存 N / 将提交 N 个文件 / staged 文件列表”；UI 无暂存入口或 staged 列表 → staged 门禁后无法在应用内完成提交闭环 | §2.3；`workbench.ts:1097-1105`；`fixcheck-commit-dialog-staged.png` |
| N1-4 | `getRepoDetail` 失败时 InfoPanel 显示上一仓（scope 与内容不一致，FR-INFO-02 残留） | §2.2；`workbench.ts:905-909`；`InfoPanel.vue:22`；`fixcheck-stale-detail.png` |
| N1-5 | MISSING 树空态仍“无法读取目录”，唯一按钮“重试”是空操作、无 Clone 入口 | §2.2；`RepoTreePanel.vue:24-43,142-148`；`workbench.ts:764-772` |
| N1-6 | 矩阵/文案仍双源：Host `Actions/ReasonCode` 未进按钮条、UI 无契约测试；自测脚本 `cdp-m2-selfcheck.mjs` 失效（`selfcheck-run.log`），无法作回归证据 | §3.7；`viewmodel.ts:472-554`、`WorkbenchDto.cs:20-53`、`ui/scripts/cdp-m2-selfcheck.mjs:573,596-629` |
| N1-7 | 状态栏 lastOp 模板未按 FR-OPS-05（P1-2 未修）；错误 Toast 双 ✕（P1-8 未修）；失败 Toast 无“重试/详情”、单仓操作未进任务中心（P1-3 未修）；PUSH 不建 upstream（P1-4 未修）；`E_REPO_BUSY` 未映射/`online` 恒真/无 read-through+CAS（P1-6 未修）；版本号与阶段文案残留（P1-7 部分修） | §3.7；`workbench.ts:341-349,634`、`ToastHost.vue:20`、`ModalHost.vue:167-247` |
| N1-8 | 版本号矛盾：标题 `v0.3.0` vs 关于/Host `0.1.0.0` | 真实 `real3.json`；`fixcheck-about-version.png` |

### 新 P2（10 项）

1. InfoPanel“最近操作”值截断过早（239px 栏 ~14 字），完整信息只能 hover title。
2. MISSING/UNKNOWN 仓在 InfoPanel 显示“已暂存 0 · 已跟踪 0 …”、本地路径（易误读为存在/可操作）。
3. 五键仍 96×30（< SRS 88×32）、F 区 52px（≤64 合规）——上轮 P2-1 未变（真实 `real3.json`）。
4. 通知中心真实模式仍有 4 条 Mock 种子（`fixcheck-notifications-mock.png`）。
5. `online` 恒 true、状态栏“在线”无信息量（`workbench.ts:274`）。
6. CHKOUT 按钮 title 宣称“确认/输入对话框”但 SRS 要求 CHKOUT 不弹确认（矩阵 `c` 漂移，见 §3.7-6）。
7. `ConfirmDialog` 的 `autofocus` 属性对动态插入节点无效（应改显式 focus）。
8. `DrawerHost`（通知/任务中心）无焦点陷阱/归还（`ModalHost` 已有）。
9. UI 错误码表与 Host 不一致：缺 `E_REPO_BUSY/E_GIT_INTERNAL/E_IO_ERROR/E_LAUNCH_FAILED` 的文案/提示（`viewmodel.ts:591-662` vs `BridgeErrors.cs`）。
10. Host COMMIT message 上限 2000 vs SRS 200（`RepoActionService.cs:32-33`），仅靠 UI `maxlength` 兜底。

---

## 5. 上轮 P1 中影响可用项的复评

| 上轮 P1 | 本轮状态 | 证据 |
|---|---|---|
| P1-1 抽屉宽度 460<480 | ✅ **已修**（520px，480–720 合规；真实+Mock） | `ComponentDrawer.vue`；`real2.json#drawer.open.width=520` |
| P1-2 状态栏 lastOp 模板 | ❌ 未修 | §3.7-3 |
| P1-3 失败 Toast 无重试/详情、单仓不进任务中心、无取消 | ❌ 未修 | `ToastHost.vue` 全文件无动作；`DrawerHost.vue:39-45` 仍“M2 接入” |
| P1-4 PUSH 不建 upstream | ❌ 未修（UI `RepoActionArgs` 无 `setUpstream`） | `api.ts:211-217`；`workbench.ts:1070-1073`；Host 虽支持 `-u`（`RepoActionService.cs:475-481`） |
| P1-5 MISSING 树空态语义 + 重复重试 | 🟡 部分修：重复“重试”已修（每面板 1 个）；“无法读取目录”标签未改、按钮空操作、无 Clone | §2.2 残留 2；`repo-tree-empty` 实测 |
| P1-6 离线/`E_BUSY`/CAS | ❌ 未修（`online` 恒真、Host 无 `E_CONCURRENT_CHANGE`、UI 无 `E_REPO_BUSY` 映射、无写前 read-through） | §3.2/§3.7 |
| P1-7 M1/M2 文案与版本 | 🟡 部分修：关于正文已改“M2 已启用…M3/v2 接入”；标题/关于版本仍冲突；设置/任务中心/菜单/树空态仍“M1 接入/M2 接入” | 真实关于弹窗；`ModalHost.vue:167-247`；`DrawerHost.vue:39-45` |
| P1-8 错误 Toast 双 ✕ | ❌ 未修 | §3.5 |

---

## 6. 需求 / AC 对照（本轮复评更新）

| 条款 | 结论 | 依据 |
|---|---|---|
| FR-ACT-01 五键/顺序/未选中禁用 | **通过（尺寸子项 P2）** | 真实 5 键 96×30 同排；MISSING 未选中语义已修 |
| FR-ACT-02 矩阵 + tooltip 原因/替代/错误码 | **不通过** | 矩阵启用/禁用全对；tooltip 0 错误码、7 条通用文案、双源未收敛（P0-6 未修） |
| FR-ACT-03 前置 | **部分**：PULL ff-only ✅、COMMIT staged-only/无 staged 禁用 ✅；CHKOUT 无 `remote_ref`/搜索/stash ❌；PUSH upstream/发布分支 ❌；离线 FETCH ❌；read-through+CAS ❌ | §2.0、§3.2 |
| FR-ACT-04 确认与安全 | **不通过** | 无确认条；PUSH 零确认（新 P0）；COMMIT 无 N 文件；破坏性覆盖未进按钮条 ✅ |
| FR-ACT-05 进行中/失败 | **部分**：spinner/串行/错误码+中文 ✅；BUSY/CAS/取消/失败重试入口 ❌ | 上轮+本轮代码核对 |
| FR-ACT-06 回写/状态栏/任务中心 | **部分**：回写 ✅；状态栏模板 ❌；任务中心/仅重试 ❌ | §3.7-3 |
| FR-ACT-07 版本门禁 | **M2 段不成立**（依赖 02/03/04/06） | 上表 |
| AC-UI-08 | **部分**：UNKNOWN/MISSING 已修；tooltip/矩阵双源未闭环 | §2.1/2.2 |
| AC-UI-10 | **不通过**：无确认条、无“未确认 0 写”证据（PUSH 直接写）、COMMIT 无 N | §2.4/§2.3 |
| AC-UI-11 | **部分**：E/F 同聚焦仓 ✅、抽屉 bbox 不变 ✅、宽 520 ✅；抽屉 ESC 焦点 ❌ | §2.5 |
| AC-UI-13 | **不通过**：ConfirmDialog 无初始焦点/陷阱；抽屉关闭焦点落 BODY | §2.5 |
| FR-INFO-01 | **通过**（11 字段齐全、label ≤72px；格式/可读性 P2） | 真实 InfoPanel；§2.0-P0-8 |
| FR-INFO-02 | **部分**：MISSING 空态已修；详情失败残留上一仓、STALE 文案不足 | §2.2/§5 |
| FR-OPS-05 | **不通过**（模板未改） | §3.7-3 |
| FR-DETAIL-01 | **通过**（宽 520、内容/回退 ✅）；焦点 P0-5 未闭环 | §2.5/§5 |

---

## 7. 放行结论

**M2 不可放行。** 依据：

1. 上轮 8 个 P0 仅 2 项完全闭环，2 项（P0-6/P0-7）完全未修，且修复未提交需求降级/ADR 就直接把 P0 降级为 P1（`docs/m2-ops-status.md` §5），不符合冻结 SRS（v0.2.1）的变更流程。
2. 确认/安全面（FR-ACT-04、AC-UI-10）仍不成立，且新引入 **PUSH 零确认**（含 `Alt+5`）——这是 M2 最需要防的“误写远端”风险。
3. 可访问性面（AC-UI-13）仍不通过：ConfirmDialog 的焦点修复对当前唯一形态（danger）无效，抽屉关闭不归还焦点。
4. 回归证据链不能自证：`cdp-m2-selfcheck.mjs` 已与实现相反且运行即崩，UI 无单测，`dotnet test` 因运行中 Host 锁文件无法复跑。

**建议的最小复评门禁（全部满足后再复评）**
1. **P0-4/NP0-1**：实现 ≤48px 确认条（COMMIT：将提交 N 个文件；PUSH：分支 → upstream；未确认 0 写、按钮 bbox 不变），或提交 ADR 修订 FR-ACT-04/AC-UI-10 并同步 AC；`Alt+5` 纳入同一策略。
2. **P0-5**：ConfirmDialog 初始焦点改 `.confirm-actions .btn`（或最后按钮）；ComponentDrawer/ActionDialog 统一“记录 trigger → 关闭归还”；补 CDP 断言（activeElement 在 `[role=alertdialog|dialog]` 内、关闭后回 `data-testid`）。
3. **P0-6**：按钮 tooltip 生成器输出“原因 + 替代动作 + 错误码”；优先消费 Host `ComponentDetailDto.Actions.ReasonCode`（契约已存在）或新增 `getAvailability` 桥。
4. **P0-7**：新增 `getRemoteRefs` 桥契约（Host `git for-each-ref`）+ 搜索选择器；dirty 仓 `stash -u` 勾选与影响说明。
5. **P0-3 内容项**：COMMIT 对话框显示“已暂存 N / 将提交 N 个文件 + staged 列表”；产品裁决“UI 内是否提供暂存入口”，否则在 UI/文档显式说明“需外部 git add”。
6. **证据链**：更新 `cdp-m2-selfcheck.mjs` 覆盖 14 态矩阵、确认条、焦点、staged 门禁、MISSING 语义，并在 CI 中运行；补 UI 契约测试（矩阵/错误码文案）。

---

## 8. 复测建议（可直接补入自测脚本）

1. 矩阵扩展为 14 主状态 × 5 键，断言 UNKNOWN FETCH 启用、其余禁用；同时断言“禁用 title 含 `E_*` 与替代动作”。
2. 确认条：点击 COMMIT/PUSH 后 `[data-testid=confirm-bar]` 出现；未点确认前 `runRepoAction` 调用数 = 0；F 区五键 bbox 不变。
3. 焦点：ConfirmDialog 打开后 `document.activeElement` 在 `[data-testid=confirm-dialog]` 内；Tab×6 不逃逸；ESC/取消后回触发按钮；ComponentDrawer 同断言。
4. COMMIT：`stagedCount=3` 时对话框文本含“将提交 3 个文件”/“已暂存 3”；`stagedCount=0` 时 COMMIT 禁用且 title 含暂存语义+错误码。
5. MISSING：E 区不含“未选择仓库”、树空态标题为“未克隆”且按钮为“Clone”；`repo-tree-empty button` = 1（已满足）。
6. 树/详情失败：注入 `getRepoDetail=E_GIT_FAILED` 后，InfoPanel 不得显示上一仓（必须显示空态/错误态）。
7. Toast：成功单 `✓`；错误 textContent 不含第二个 `✕`；失败 Toast 含“重试/详情”（若 M2 承诺）。
8. 状态栏：正则 `^最近一次操作：.* (PULL|FETCH|CHKOUT|COMMIT|PUSH) .+/.+ — (成功|失败\(E_[A-Z_]+\)|进行中) · \d{2}:\d{2}$`；“无操作”显示“无”。
9. 版本/文案：真实 Host 页面全文不得出现“M1 只读/将在 M2 接入”；标题与关于版本一致。
10. 建议把 Mock 桥（17 组件快照 + `detailFail/confirmRequired/failAction` 注入）沉淀进 `ui/scripts`，替换现有失效脚本。

---

## 附录 A：本轮关键实测值（可直接复核）

| 项 | 值 |
|---|---|
| 真实 Host | commit `06c6ea2`；PID 5348；4 组件/14 仓；`features.repoActions=true`；`appVersion=0.1.0.0`；标题 `v0.3.0`；consoleErrors=0 |
| 真实 MISSING（RTCT_Incomplete） | scope=`DESIGN`；aria=`单仓操作：Design`；五键 disabled，title=`MISSING：仓库未克隆`；InfoPanel=`RTCT_Incomplete_Design`；树 4 面板各 1 个“重试”，文案=`✕无法读取目录 E_REPO_NOT_LOCAL · 仓库未克隆（MISSING）` |
| 真实 SYNCED（RTCT_Synced_Impl） | PULL/FETCH/CHKOUT/PUSH enabled，COMMIT disabled（`无已暂存改动：请先 git add 暂存后提交`）；title 无错误码 |
| 真实 CHKOUT 对话框 | active=`action-dialog-ref`；suggestions=`["main"]`；无 search/checkbox；Tab×8 全在框内；ESC 后 active=`action-chkout` |
| 真实抽屉 | 520px、4 行仓表、`data-source=host`、打开 focus 在关闭按钮、Tab×12 不逃逸；ESC 后 active=`BODY` |
| 真实关于/通知 | 关于 `版本 0.1.0.0`；通知中心 `应用内通知（Mock）`，4 条均带“（Mock）” |
| 真实按钮 | F 区 52px；5×96×30；顺序 PULL/FETCH/CHKOUT/COMMIT/PUSH |
| Mock 矩阵 | 17 组件 × 5 键全对（含 13 主状态 + staged 变体）；`titlesWithErrorCode=0/85`，`generic=7` |
| Mock COMMIT | staged=3 时对话框 info=`仓库/分支/未提交 已跟踪 2 · 未跟踪 1`，无“已暂存 3/将提交 3 个文件”；staged=0 时 3 个状态全部禁用 |
| Mock 确认/焦点 | ConfirmDialog：`focusInside=false`、`primaryExists=false`、`dangerExists=true`，Tab 逃逸到 `status-notify`；ActionDialog：Tab×9 全内、ESC 回 trigger |
| Mock PULL/PUSH | PULL/PUSH 点击各 1 次 `runRepoAction`（PUSH `args={}`）；无 confirm-dialog、无确认条；成功 Toast 单 ✓ |
| Mock 详情失败 | 聚焦 DESIGN 后 scope=`DESIGN`，InfoPanel 标题仍 `Mock_IMPL`（上一仓残留） |
| Mock 溢出 | 1440：panel 253 / body 241=241；1000：panel 208 / 196=196；无越界字段 |
| 自测脚本 | `cdp-m2-selfcheck.mjs` 运行崩于 `:573 missing action-dialog-message`（staged 门禁导致） |
| dotnet test | 无法复跑：`MSB3027/MSB3021`（Host 5348 锁定 `bin/.../MbdGitTool.Core.dll`） |

## 附录 B：证据文件索引

- 隔离 Mock：`artifacts/ui-review/fixcheck/mock.mjs`、`mock-result.json`（34 checks / 23 pass / 11 gap）
- 真实 Host 只读：`probe-real.mjs`、`probe-real2.mjs`、`probe-real3.mjs`、`real2.json`、`real3.json`
- 失效自测：`selfcheck-run.log`（崩溃栈）；`dotnet-test.log`（构建被锁）
- 截图（`docs/images/m2/`）：`fixcheck-missing-focus.png`、`fixcheck-commit-dialog-staged.png`、`fixcheck-confirm-dialog-focus.png`、`fixcheck-checkout-dialog.png`、`fixcheck-drawer.png`、`fixcheck-about-version.png`、`fixcheck-notifications-mock.png`、`fixcheck-stale-detail.png`
- 原修复截图（对照）：`docs/images/m2/{missing-focus,staged-commit,pull-direct,component-drawer}.png`

## 附录 C：本报告未覆盖/待裁决

1. PUSH 是否必须确认：SRS FR-ACT-04 与 spec §6.3/§6.4 冲突（RQ-02），需产品 ADR；本轮按冻结 SRS 判 NP0-1。
2. `DIVERGED` 的 PULL 单元格在 SRS 中为“⚠ 仅展示不自动 merge”，UI/Host 均取禁用——需求歧义，未在 UX 层判缺陷（同并行评审 RQ-01）。
3. 真实验证未执行写操作：PUSH/COMMIT 真实链路证据依赖 `docs/m2-ops-status.md` 与夹具 git 历史（`e67eb05`），未在评审中重新执行；建议在隔离夹具仓补一次 E2E。
