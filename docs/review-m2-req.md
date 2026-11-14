# M2 单仓写操作与 SRS/规格符合度对抗评审

> 评审日期：2026-11-14 13:09–13:35（本地）
> 评审对象：M2 单仓写操作（`PULL` / `FETCH` / `CHKOUT` / `COMMIT` / `PUSH`）的 Host 实现、UI 可用性矩阵、确认/前置/失败/回写链路，以及 M2 出口口径
> 需求输入：`reqs/specification.md` §3.3（M2 出口）、§7.5/§7.6（矩阵与新鲜度）、§9.16（FR-ACT）、§9.17（FR-INFO）、§11.0–§11.3、§13（AC）、附录 D（错误码）；`docs/ui-redesign-spec.md` §5/§6；`docs/review-m2-ux.md`（8 个 P0）；`docs/m2-ops-status.md`（实现/实测/剩余）
> 实现输入：`src/MbdGitTool.Host/Services/RepoActionService.cs`、`LocalScanner.cs`、`GitCli.cs`、`Bridge/HostCommandRouter.cs`、`Core/Contracts/*`、`ui/src/stores/workbench.ts`、`ui/src/viewmodel.ts`、`ui/src/components/workbench/{RepoActionsBar,InfoPanel}.vue`、`ui/src/components/overlays/{ActionDialog,ConfirmDialog,ComponentDrawer}.vue`、`ui/scripts/cdp-m2-selfcheck.mjs`
> 运行输入：真实 Host（PID 5348，CDP 9222，commit `06c6ea2`，真实 Gitea `RTCRepos` + `artifacts/real-test/workspace`）；本次仅做**只读 CDP 探测**（快照/DOM/按钮矩阵/对话框结构），**未执行任何写操作**
> 硬性约束：本轮只新建本报告，未改任何代码、构建产物与需求文档。

---

## 0. 结论（放行判决）

### 0.1 判决

| 维度 | 判定 | 摘要 |
|---|---|---|
| 单仓五键“能执行”主链路 | **基本可用** | FETCH/PULL(`--ff-only`)/CHKOUT/COMMIT(staged-only)/PUSH 真实 git 语义正确；staged-only 危险行为已修（`RepoActionService.cs:445-457`）；M1/M2 能力门禁正确；失败可读、动作后回写 ≤2s（`workbench.ts:1178-1182`）。 |
| FR-ACT（§9.16，7 条） | **1 条通过、2 条部分、4 条不通过** | FR-ACT-01 通过；FR-ACT-05/06 部分；FR-ACT-02/03/04/07 不通过。 |
| FR-INFO（§9.17，4 条） | **基本通过（P1/P2 级残留）** | 最近操作段已补（实测 `info-last-op` 存在）；STALE“数据 X 前”+斜纹、RepoType 显式字段等子项未达。 |
| §7.6 矩阵一致性（Host vs UI vs SRS） | **启用/禁用 12/14 行一致；1 行需求歧义（DIVERGED）、1 行口径未冻结（EMPTY）；1 格确认标志漂移（SYNCED CHKOUT）** | Host 矩阵 `RepoActionService.cs:49-66` 与 UI 矩阵 `viewmodel.ts:472-554` 是两份独立实现，SRS §7.6 末条明确“禁止另写一套判断”。 |
| M2 出口（§3.3 第 137 行 / §13） | **不满足** | 批量 7 操作、SYNC/Delete/备份/恢复、任务中心、审计、写前 journal、AC-OP-01…32/AC-REPO-01…11/AC-SAFE-01…04/AC-UI-08/10/11 均无交付或验收证据。 |
| 与 UX 评审 8 个 P0 闭环 | **已修 3、部分修 4、未修 1** | P0-1/P0-2/P0-8 已修；P0-3/P0-4/P0-5/P0-7 部分修；P0-6 未修。修复同时引入 1 个新偏差（PUSH 变为零确认，见 P0-01）。 |
| **M2 可放行** | **❌ 不可放行** | 单仓五键子集尚未达到 AC-UI-08/10 与 FR-ACT-02/03/04/05/06 的 P0 验收；SRS 定义的 M2 出口范围远未交付。 |

### 0.2 一句话结论

**“单仓五键可跑，但不可验收，更不可按 SRS 的 M2 放行。”** 当前实现证明的是“真实 git 语义 + staged-only 安全修复”可用；缺的是需求明确要求的确认条、单源矩阵/tooltip、写前 read-through+CAS、BUSY/取消/job/任务中心/审计，以及 M2 出口中的批量与备份恢复整块能力。

---

## 1. 方法与证据

### 1.1 证据分层

1. **条款核对**：逐条读 `requirements.md` §3.3/§7.5/§7.6/§9.7–§9.10/§9.16/§9.17/§11/§13/附录 D，以及 `ui-redesign-spec.md` §5/§6。
2. **代码核对**：Host `RepoActionService`/`HostCommandRouter`/`LocalScanner`/`GitCli` + UI store/viewmodel/组件，全部给出文件:行号。
3. **只读实测**（本次新增，2026-11-14 13:12–13:25，CDP 9222，页面 `https://app.local/index.html`，viewport 1424×861，Host `appVersion=0.1.0.0`）：读取真实快照（4 组件/14 仓，状态仅 `SYNCED`/`AHEAD`/`MISSING`）、`getComponentDetail` 的 Host 可用性、DOM 按钮矩阵/tooltip/信息面板/CHKOUT 对话框/焦点；**未点击任何执行性按钮、未做任何写操作**。
4. **既有证据复用**：`docs/review-m2-ux.md` 的真实 Host + 隔离 Mock 实测、截图；`docs/m2-ops-status.md` 的真实 Gitea 写操作实测（FETCH/COMMIT/PUSH/PULL/CHKOUT）。

### 1.2 本次只读实测关键值

| 项 | 实测值 |
|---|---|
| 真实快照 | 4 组件 / 14 仓；`RTCT_Demo`=AHEAD（SIL）、`RTCT_Synced`=SYNCED、`RTCT_Incomplete`=BINDING_ERROR、`RTCT_RemoteOnly`=MISSING |
| Host 能力 | `features.repoActions=true`；`appVersion=0.1.0.0`，WebView2 157.0.4341.46 |
| Host 可用性（`getComponentDetail`） | `RTCT_Synced` 四仓五键全部 `enabled=true/confirm=false`；`RTCT_Demo_SIL`（AHEAD）PULL/COMMIT `enabled=false`、CHKOUT `confirm=true`、FETCH/PUSH `enabled=true`；MISSING 五键全禁用 |
| UI 五键（选中 `RTCT_Demo_SIL`，AHEAD） | PULL 禁用（title `AHEAD：本地领先，需先推送`）、FETCH 启用、CHKOUT 启用、COMMIT 禁用（title **`当前仓库状态不允许该操作`**）、PUSH 启用；按钮实测 **96×30** |
| UI 五键（选中 `RTCT_Synced_Impl`，SYNCED） | 五键均启用；COMMIT 因 `stagedCount=0` 禁用（title `无已暂存改动：请先 git add 暂存后提交`）；CHKOUT title 含“执行前弹出确认/输入对话框” |
| 确认条 | `document.querySelector('[data-testid=confirm-bar]') === false`；全仓 grep 无 `confirm-bar`/`ConfirmBar` 组件 |
| CHKOUT 对话框 | `[data-testid=action-dialog]` 存在；建议源 `<datalist>` 仅 `["main"]`；无搜索框、无 `stash` 勾选、无远端 refs 列表 |
| MISSING 聚焦 | scope=`DESIGN`、aria=`单仓操作：Design`、五键 title 全为 `MISSING：仓库未克隆`；信息面板显示 `RTCT_Incomplete_Design`（P0-2 已修） |
| 组件抽屉 | 宽 **520px**（480–720 合规）；打开时焦点在抽屉内；**ESC 后 `document.activeElement=BODY`**（焦点未回触发控件） |
| 状态栏最近操作 | `就绪 · 无最近操作`（非 FR-OPS-05 的“无”） |
| 主界面文案 | 主界面 innerText 未出现“M1 只读/将在 M2 接入”（已清理） |

### 1.3 限制

- 为避免误写真实仓库，未执行 BUSY/CAS/审计/取消等失败路径的 Runtime 触发；这些项以代码核对 + Host 单测清单判定。
- 本次未复用 UX 评审的隔离 Mock 实例；14 态 UI 矩阵本轮**无**可复跑的 UI 证据（见 P0-12 与 §7.5）。

---

## 2. FR-ACT 逐条核对（SRS §9.16，`requirements.md:601-613`）

| FR | 优先级 | 结论 | 关键证据 | 类型 |
|---|---|---|---|---|
| FR-ACT-01 五键固定/顺序/未选中禁用/不换行 | P0 | **通过** | 五键顺序/英文/不换行实测（`RepoActionsBar.vue:47-63`；本次 1424 宽同排）；`NO_REPO_REASON`（`viewmodel.ts:678-690`）与 MISSING 提示已修。唯一子项：按钮高 30 < §11.0.2 F 的 32px（见 P1-07）。 | — |
| FR-ACT-02 可用性严格取 §7.6 + tooltip 原因/替代动作/错误码 + M1 边界 | P0 | **不通过** | ① UI 自写第二套矩阵（`viewmodel.ts:472-554`），未消费 Host `GetAvailability`（无桥方法，`HostCommandRouter.cs:207-335` 方法表）；② 禁用 title 仅“原因”，实测 `AHEAD` 的 COMMIT = **“当前仓库状态不允许该操作”**（无替代动作、无错误码）；③ 启用态 title 泄漏协议参数（Host reason `需二次确认（args.confirm=true）`，`RepoActionService.cs:100-104`）。M1 门禁本身通过（`workbench.ts:320-335`，UX 实测 M1 全禁用+0 写）。 | 实现缺失 |
| FR-ACT-03 前置：read-through+CAS；FETCH 离线禁用；PULL `--ff-only`；CHKOUT 选择器来自 `remote_ref` 含搜索；COMMIT staged-only/1–200/无 staged 禁用；PUSH upstream+非 detached | P0 | **不通过** | READ-THROUGH/CAS❌（`RunAsync` 直接读 `SnapshotIndex`，`RepoActionService.cs:153-161`；`preHead` 读完不比对，`:245`、`:281`）；离线 FETCH 禁用❌（`online` 恒 true，`workbench.ts:274`）；PULL ff-only✅（`:343-350`）；CHKOUT 选择器❌（`remote_ref` 桥不存在；UI 仅用 `[branch, tag, compareRef]` 生成 datalist，`workbench.ts:1119-1121`，实测仅 `["main"]`、无搜索）；COMMIT staged-only✅ + 无 staged 禁用✅ + message 1–200（UI `maxlength=200`✅，Host 上限 2000❌，`RepoActionService.cs:32-33`）；PUSH 不校验 upstream/detached，且 UI 从不传 `setUpstream`（`api.ts:211-217`、`workbench.ts:1070-1073`）。 | 实现缺失（含 1 项需求口径冲突，RQ-03） |
| FR-ACT-04 确认与安全：FETCH/PULL/CHKOUT 不弹确认；COMMIT/PUSH 在按钮条上方 ≤48px 确认条（“将提交 N 个文件/将推送 N 个提交（分支 → upstream）”），未确认时 0 写；破坏性覆盖不进按钮条 | P0 | **不通过** | ① 无确认条组件（实测 false，全仓 grep 0）；② PUSH **零确认直接执行**（`workbench.ts:1070-1073`；spec §6.4 口径与 SRS 冲突，见 RQ-02）；③ COMMIT 用模态输入框，内容行仍是“已跟踪/未跟踪”，无“将提交 N 个文件/staged 列表”（`workbench.ts:1097-1105`）、“无 bbox 不变/未确认 0 写”证据；④ FETCH/PULL 已免确认✅（P0-4 部分修）；⑤ 破坏性覆盖未进按钮条✅。 | 实现缺失 + 需求口径冲突 |
| FR-ACT-05 进行中/失败/取消：spinner+同仓串行+状态栏；错误码+中文+可重试；BUSY/CONCURRENT_CHANGE；合作式取消 | P0 | **部分通过** | spinner/同仓其余禁用/状态栏进行中✅（`workbench.ts:1190-1197`；UX 实测 `aria-busy`）；失败错误码+中文✅（`viewmodel.ts:591-662`）；“可重试”入口❌（无 Toast 动作、无任务中心）；BUSY❌（LocalScanner 已产 `busy` flag `LocalScanner.cs:189`，但 `RepoActionService` 从不消费，也不返回 `E_REPO_BUSY`，`BridgeErrors.cs:19` 成为死码）；CONCURRENT_CHANGE❌（`E_CONCURRENT_CHANGE` 仅存在于 UI 映射 `viewmodel.ts:639`，Host 无实现）；合作式取消❌（仅请求级 `CancellationToken`，无 UI 取消、无 job）。 | 实现缺失 |
| FR-ACT-06 完成后回写 ≤2s；状态栏“最近一次操作”模板；失败项任务中心仅重试；一次操作一条审计 | P1 | **部分通过** | 回写✅（`refreshAfterAction`，`workbench.ts:1178-1182`；UX 实测 730ms）；状态栏模板❌（`lastOpText()` 无“最近一次操作：”、无 componentId，成功无括注，实测 `就绪 · 无最近操作`/`✓ FETCH 成功 · HEAD…`，`workbench.ts:341-349`）；任务中心/仅重试❌（`DrawerHost.vue:39-45` 仍“M2 接入”占位）；审计❌（无 `operation_log`/DB，仅 `HostLog.Info` 两行，`RepoActionService.cs:275-285`）。 | 实现缺失 |
| FR-ACT-07 版本门禁：M0/M1/M2 三阶段；禁止可点无效果 | P0 | **不通过（M2 段）** | M1 段✅（5 键全禁用+理由+0 写，UX 实测）；M2 段要求“按 FR-ACT-02…06 全部接入”，其中 02/03/04/06 未达，故 M2 门禁声明不成立；静默失败未发现（失败均有 Toast/状态栏）。 | 实现缺失/出口口径 |

### 2.1 结论

- FR-ACT 的**“操作语义正确性”**（ff-only、staged-only、不加 `-f`、参数走 `ArgumentList`、token 脱敏）已达到较好水平（`RepoActionService.cs:303-501`、`:537-553`、`:621-678`）。
- FR-ACT 的**“交互契约与安全前置”**（确认条、tooltip 错误码、CHKOUT 数据源、read-through+CAS、BUSY、取消、job/审计）几乎整体缺失，且这正是 M2 出口 AC-UI-08/10 的断言面。

---

## 3. FR-INFO 逐条核对（SRS §9.17，`requirements.md:624-627`）

| FR | 优先级 | 结论 | 证据 |
|---|---|---|---|
| FR-INFO-01 E 区字段（全名/RepoType/状态+数据时间/分支/HEAD/upstream/ahead/behind/dirty/最近操作/CI 占位/快捷按钮；标签列 ≤72px） | P0 | **基本通过** | `InfoPanel.vue:91-238`；实测字段齐全（`info-repo-title`/`info-branch`/`info-head`/`info-ahead-behind`/`info-dirty`/`info-last-op`/`info-ci`，`已暂存/已跟踪/未跟踪/stash`，`状态计算 11-14 12:57 · local`）；标签列 52–64px（`InfoPanel.vue:372,403,435`）≤72 ✅；**RepoType 无显式字段**（仅藏在仓名后缀，P1-10）。 |
| FR-INFO-02 空态/同聚焦仓/STALE“数据 X 前”+斜纹 | P0 | **部分通过** | 空态与 E/F 同聚焦仓✅（`focusedRepo` 已区分 MISSING，`workbench.ts:300-304`；实测 MISSING 聚焦正确）；STALE 仅显示“⏱ 数据过期”，无“数据 X 前”与斜纹（`InfoPanel.vue:200`），P1-10。 |
| FR-INFO-03 CI 占位区/6 态 Mock/不参与聚合 | P1 | **通过** | `InfoPanel.vue:76-89,216-224,264-274`；CI 独立通道、`ci.enabled=false` 空态；卡片底色未染（`viewmodel.ts:198` 聚合输入不含 CI）。 |
| FR-INFO-04 刷新 ≤2s（本工具）/≤10s（外部 IDE）；过期灰化不重建面板 | P1 | **部分通过（不可判定）** | 本工具动作后刷新链路✅（同 FR-ACT-06）；外部 IDE ≤10s 与“仅更新字段不重建”本轮无实测证据，`LocalScanner` 轮询周期在后台服务（`docs/m2-ops-status.md` 未列）。 |

---

## 4. §7.6 状态×操作矩阵逐格核对（硬要求 2）

**方法**：SRS 权威列（`requirements.md:334-357`）；Host 编码（`RepoActionService.cs:49-66`，顺序 PULL/FETCH/CHKOUT/COMMIT/PUSH，Y/N/C）；UI 编码（`viewmodel.ts:472-554`，y/c/原因字符串）；另用本次 `getComponentDetail` 实测校验 Host `MatrixAllowed/RequiresConfirm` 与代码一致（抽样 SYNCED/AHEAD/MISSING 全部一致）。

> 编码：`Y`=启用免确认，`C`=启用需二次确认，`N`=禁用；SRS `—`=“无需执行”，`⚠`=允许但需 dry-run/备份/二次确认。

| 主状态 | SRS（Pull/Fetch/Chkout/Commit/Push） | Host | UI | 逐格偏差 |
|---|---|---|---|---|
| SYNCED | ✅ / ✅ / ✅ / ✅ / ✅ | YYYYY | y y **c** y y | **UI CHKOUT 标 `c`（SRS/Host 为免确认）**；启用/禁用一致 |
| DIRTY | ❌ / ✅ / ⚠ / ✅ / ✅ | NYCYY | 一致 | 无（原因文案两侧不同，见 FR-ACT-02） |
| AHEAD | ❌ / ✅ / ⚠ / ❌ / ✅ | NYCNY | 一致 | 无；UI COMMIT 原因退化（Host 有明确原因） |
| BEHIND | ✅ / ✅ / ⚠ / ❌ / ❌ | YYCNN | 一致 | 无 |
| DIVERGED | **⚠ / ✅ / ⚠ / ❌ / ❌** | NYCNN | 一致 | **需求歧义**：SRS 写“⚠ 仅展示不自动 merge”，无法映射按钮语义；Host/spec §6.2 取禁用，需 ADR（RQ-01） |
| DETACHED_TAG / DETACHED_COMMIT | ❌ / ✅ / ⚠ / ❌ / ❌ | NYCNN | 一致 | 无 |
| NO_UPSTREAM | ❌ / ✅ / ❌ / ❌ / ✅（发布分支） | NYNNY | 一致 | 启用一致，但“发布分支建 upstream”未实现（P1-03）；与 FR-ACT-03“PUSH 需 upstream”冲突（RQ-03） |
| EMPTY | **—** / ✅ / ⚠ / ✅ / ✅（首推建 upstream） | NYCYY | 一致 | 启用口径一致；SRS `—` 未在 AC 写死（RQ-01 同族）；首推建 upstream 未实现（P1-03） |
| UNMERGED | ❌ / ✅ / ❌ / ❌ / ❌ | NYNNN | 一致 | 无 |
| IN_PROGRESS | ❌ / ❌ / ❌ / ❌ / ❌ | NNNNN | 一致 | 无 |
| INVALID | ❌ / ❌ / ❌ / ❌ / ❌ | NNNNN | 一致 | 无 |
| MISSING | **—** / **—** / ❌ / ❌ / ❌ | NNNNN | 一致 | Fetch/Pull 的 SRS `—`（无需执行）按“禁用”实现，语义等价但 AC 未冻结（同 RQ-01 族）；MISSING 聚焦语义已修 |
| UNKNOWN | ❌ / ✅ / ❌ / ❌ / ❌ | NYNNN | 一致 | 无（P0-1 已修：`viewmodel.ts:546-553`） |

### 4.1 偏差与根因

1. **启用/禁用**：14 行中 11 行三源完全一致；`EMPTY`、`MISSING` 的 `—`（无需执行）与“禁用”语义等价但 AC 未写死；`DIVERGED` 因 SRS 单元格自相矛盾而无法判定。结论：**启用/禁用层面矩阵基本符合，无明确实现错误，但存在 2 处需求口径未冻结**（RQ-01）。
2. **确认标志**：UI 将 `SYNCED` 的 CHKOUT 标为 `c`，Host 为 `Y`，SRS 为 ✅——用户实际感受是“CHKOUT 一律弹选择框”，影响小，但违反 SRS §7.6 末条“按钮启用/禁用与 tooltip 由矩阵逐格生成，禁止另写一套判断”。
3. **双源结构**：`RepoActionService.Matrix` 与 `viewmodel.MATRIX` 各自维护，Host 已把 `ReasonCode/RequiresConfirm` 通过 `getComponentDetail` 暴露（`WorkbenchDto.cs:45-53`），但按钮条不消费；Host 侧 14 态单测的“期望矩阵”又是第三份拷贝（`tests/MbdGitTool.Host.Tests/RepoActionAndComponentDetailTests.cs:13-61`），不是从 SRS/AC 夹具生成。
4. **tooltip 内容**：同一禁用格 Host 文案与 UI 文案不一致（如 AHEAD COMMIT：Host `AHEAD：需先推送/手动处理`，UI `当前仓库状态不允许该操作`），且均无错误码——直接违反 FR-ACT-02。

---

## 5. M2 出口标准核查（SRS §3.3 第 137 行 / §13）

SRS M2 出口原文：批量 Clone/Pull/Checkout/Tag/SYNC/Delete/Restore/Create + 底部五键（前置/确认/失败/进行中/审计）+ 任务中心 + 备份/审计；写操作新 AC 通过（`AC-OP-01…32`、`AC-REPO-01…11`、`AC-SAFE-01…04`、`AC-UI-08/10/11`）；故障注入通过；状态×按钮矩阵 14 主状态全覆盖。

| 出口项 | 判定 | 证据 / 缺口 |
|---|---|---|
| 批量 Clone/Pull/Checkout/Tag/SYNC/Delete/Restore/Create | ❌ 未交付 | `workbench.ts:1303-1321` 仅 Toast“M2 接入”；树空态 Clone/Repair 也只 Toast（`RepoTreePanel.vue:83-91`） |
| 底部五键“前置” | ❌ | 无 read-through/CAS、无 BUSY、CHKOUT 无 remote_ref/搜索、FETCH 离线不禁用 |
| 底部五键“确认” | ❌ | 无 ≤48px 确认条；PUSH 零确认（P0-01） |
| 底部五键“失败” | ⚠ 部分 | 错误码+中文✅、spinner/回写✅；无重试入口、无任务中心明细、无 BUSY/CONCURRENT_CHANGE |
| 底部五键“进行中” | ✅ | spinner/同仓串行/状态栏（UX 实测 + `workbench.ts:1190-1197`） |
| 审计 | ❌ | 无 `operation_log`、无 DB；仅 `HostLog.Info`（`RepoActionService.cs:275-285`） |
| 任务中心 | ❌ | `DrawerHost.vue:38-45` 空占位；单仓操作不产生 job/jobId（Host 成功响应无 `jobId`，`RepoActionService.cs:287-300`；UI DTO 有可选字段但永远为空，`api.ts:220-234`） |
| 备份/恢复 | ❌ | 无 `refs/backup/*`、无 `.trash`（FR-OP-11 P0） |
| AC-OP-01…32 | ❌ 无证据 | 批量/SYNC/Delete/CREATE/Restore/任务中心/取消/审计相关 AC 全部无实现或无报告 |
| AC-REPO-01…11 | ⚠ 部分 | 01/02/03 的“操作前自动重扫”❌（AC-REPO-01④/02③）；04/06 的“写审计”❌；05（Push 四失败面/发布分支）❌；08–11（Repair/set-upstream/detached 建分支/EMPTY 首推）**完全未实现** |
| AC-SAFE-01…04 | ❌ | 依赖 SYNC/Delete，均未交付；无 dry-run、无“远程仓库不受影响”文案快照、无未确认 0 写断言 |
| AC-UI-08 | ❌ 无通过证据 | Host 单测覆盖 14 态 DTO；**UI 14 态逐格无可复跑证据**：真实环境仅 3 态；`ui/scripts/cdp-m2-selfcheck.mjs` 只覆盖 2 态（`cdp-m2-selfcheck.mjs:529-554`）且已过期（见 §7.1） |
| AC-UI-10 | ❌ | 无确认条、无 BUSY、无取消、无审计、无“未确认 0 写”证据；无 staged 禁用与 ff-only 指引子项✅ |
| AC-UI-11 | ⚠ 基本通过 | E/F 同聚焦仓、字段、抽屉 bbox 不变、抽屉宽 520px 合规（实测）；1280/200% 与 STALE 子项未复测 |
| 故障注入通过 | ❌ 无记录 | 无故障注入报告；`E_REPO_BUSY`/`E_CONCURRENT_CHANGE` 在 Host 不可产生 |
| 状态×按钮矩阵 14 主状态全覆盖 | ⚠ Host 层覆盖、UI 层未覆盖 | `RepoActionAndComponentDetailTests.cs:38-61` 覆盖 14 态（但期望表为第三份手写）；UI 层无 14 态断言 |

### 5.1 结论

- 若按 SRS 字面口径：**M2 出口 16 个核对项中仅 1 项通过（“进行中”）、4 项部分（“失败”、AC-REPO、AC-UI-11、矩阵覆盖）、11 项未满足**。
- 若产品实际想交付的是“M2a 单仓五键子集”，则**当前 SRS 未授权该拆分**（§3.3 M2 行未写 M2a/M2b），必须先走需求变更/ADR（RQ-06），否则无法给出可审计的放行结论。

---

## 6. 与 `docs/review-m2-ux.md` 8 个 P0 的闭环

| UX P0 | 状态 | 证据 | 新偏差/残留 |
|---|---|---|---|
| P0-1 UNKNOWN 误禁 FETCH | **已修（代码级）** | `viewmodel.ts:546-553` FETCH=y；Host `NYNNN`（`RepoActionService.cs:65`） | 无自动化回归：`cdp-m2-selfcheck.mjs` 未加 UNKNOWN 态；真实环境无 UNKNOWN 夹具 |
| P0-2 MISSING 被当“未选择” | **已修（实测）** | scope=`DESIGN`、aria=`单仓操作：Design`、五键 `MISSING：仓库未克隆`、E 区显示 MISSING 仓（`workbench.ts:300-304`、`InfoPanel.vue:22`） | 新：E 区“本地路径”显示不存在的目录（P2-05） |
| P0-3 无 staged 仍可 COMMIT / Host `add -A` | **已修主要部分** | Host 仅提交 index（`RepoActionService.cs:445-457`）；`stagedCount` 进快照（`SnapshotDto.cs:58`、`LocalScanner.cs:253`）；UI 无 staged 禁用（`viewmodel.ts:699-707`）；单测 `Commit_UnstagedChanges_ReturnsNothingToCommit` | 仍缺“将提交 N 个文件”/staged 列表（FR-ACT-04）；Host 消息上限 2000≠SRS 200 |
| P0-4 无确认条、PULL 反向确认、PUSH 常规确认 | **部分修（方向冲突）** | PULL 已免确认（`workbench.ts:1066-1068`，实测无 confirm）；但**改成了“PUSH 零确认”**且仍无确认条 | **新偏差**：SRS FR-ACT-04 要求 COMMIT/PUSH 确认条后才执行；本次修复遵循 spec §6.4“常规 PUSH 免确认”，二者矛盾（RQ-02）——需 ADR 明确以谁为准 |
| P0-5 焦点陷阱/焦点归还 | **部分修** | ActionDialog/ConfirmDialog 已补 initial focus/trap/restore（`ActionDialog.vue:20-41,77-93`；`ConfirmDialog.vue:10-51`；实测 CHKOUT 对话框 ESC 后 activeElement=`action-chkout`） | **ComponentDrawer 未修**：实测 ESC 后 activeElement=BODY（`ComponentDrawer.vue:5,29` 无 restore），AC-UI-13 仍不通过（M2 出口不含 AC-UI-13，但 FR-WS-04 仍为 P0） |
| P0-6 禁用 tooltip 无错误码/替代动作 | **未修** | 实测 COMMIT(AHEAD) title=`当前仓库状态不允许该操作`；UI 仅渲染 `cell.reason`（`RepoActionsBar.vue:20-30`）；Host `ReasonCode` 未消费 | 与 FR-ACT-02 同一条，属 M2 出口 AC-UI-08/10 范围 |
| P0-7 CHKOUT 无 remote_ref/搜索/stash；二次确认冗余 | **部分修** | 二次确认冗余已消除（UI 直接带 `confirm:true`，`workbench.ts:1150`）；但**数据源/搜索/stash 未修**：实测建议仅 `["main"]`、无 search、无 stash（`workbench.ts:1118-1153`、`ActionDialog.vue:146-176`），且无 `getRemoteRefs` 桥（`HostCommandRouter.cs:30-44,207-335`） | FR-ACT-03 P0 仍未达 |
| P0-8 InfoPanel 缺“最近一次操作” | **已修（实测）** | `info-last-op` 存在（`InfoPanel.vue:194-197`） | 文案沿用错误的 lastOp 模板（P1-01） |

**闭环统计：已修 3（P0-1/2/8）、部分修 4（P0-3/4/5/7）、未修 1（P0-6）。**

---

## 7. AC 可验收性（硬要求 5）

### 7.1 现状：AC-UI-08/10/11 目前**不可自动验收**

1. **AC 权威分层未回写**：`requirements.md:998-1000` 明确 AC-UI-01…14 尚未回写 `reqs/specification.md（Annex A）`；M2 出口引用 AC-UI-08/10/11 必须同时注记 §13.1.1。
2. **唯一提交的 UI 自测脚本已过期**：
   - Mock 仓库样例无 `stagedCount`（`ui/scripts/cdp-m2-selfcheck.mjs:46-71`）→ 当前 UI 会禁用 COMMIT，脚本第 4 步（`:555-591`）将失败；
   - 脚本仍期望 PULL 弹 `confirm-dialog`（`:596-608`）、PUSH 弹 `confirm-dialog`（`:617-629`），与当前“免确认直接执行”实现相反；
   - 矩阵只覆盖 2 个状态（BEHIND/MISSING，`:529-554`），未覆盖 14 态；未断言确认条、tooltip 错误码、BUSY、取消、审计。
   - 结论：**仓库内不存在能绿跑并证明 AC-UI-08/10 的资产**；`docs/m2-ops-status.md` 的“234/234 单测”不等价于 AC。
3. **真实环境状态覆盖不足**：真实 Host 快照仅 `SYNCED/AHEAD/MISSING`（本次实测），无法覆盖 DIRTY/BEHIND/DIVERGED/DETACHED/NO_UPSTREAM/EMPTY/UNMERGED/IN_PROGRESS/INVALID/UNKNOWN。
4. **矩阵双源**：即使 UI 断言全绿，也只是证明 UI 的第二套矩阵自洽；SRS §7.6 要求由同一权威矩阵生成。

### 7.2 建议 Host / 单测断言（可直接落地）

| 断言 | 方式 | 期望 |
|---|---|---|
| 14 态×5 键矩阵 | 表驱动测试，**期望表从新增 `reqs/fixtures/m2-matrix.json`（由 SRS §7.6 生成）读取**，不手写第三份 | Host `GetAvailability` 编码 == 夹具；`Enabled == MatrixAllowed`；`RequiresConfirm == (cell==C)` |
| 禁用 tooltip 三段式 | 单测序列化 `RepoActionAvailabilityDto` + UI 快照 | 每格 `Reason` 匹配 `原因：.+；替代：.+；错误码：E_[A-Z_]+` |
| read-through + CAS | 临时仓：scan → 外部改 HEAD → `RunRepoAction` | 返回 `E_CONCURRENT_CHANGE`，仓库零改动；且 action 前 `scan_at` 前进 |
| BUSY | 临时仓创建 `.git/index.lock` 后 `FETCH`/`COMMIT` | 返回 `E_REPO_BUSY`；`.git/index.lock` 仍存在（不删锁） |
| 离线 FETCH | 注入网络不可达（或 git 包装器失败） | UI 侧 FETCH 禁用并说明；直接调用 Host 返回 `NET-E001`/`E_OFFLINE` 语义映射 |
| COMMIT staged-only | staged 1 + unstaged 1 | `git show --stat` 仅 1 文件；message 201 字符 → `E_INVALID_ARGUMENT`（与 FR-ACT-03 的 1–200 一致） |
| PUSH 发布分支 | NO_UPSTREAM / EMPTY 夹具 | `setUpstream=true` 且 `git push -u` 后 `upstream_ref` 非空、状态重算；无 upstream 时先提示“发布分支” |
| 取消 | 长 PULL（注入延迟）+ CancellationToken | job 记 `CANCELLED`；未开始步骤保持原状；触发重扫 |
| 审计 | 每次动作后查 `operation_log` | `ts/actor/action/target/before/after/result/error_code/request_id` 齐全（当前无 DB，测试将红） |
| 写前 journal | 强杀进程 + 重启 reconcile | 不猜测成功；`INTERRUPTED` 可见（FR-SAFE-04） |

### 7.3 建议 CDP 断言（AC-UI-08/10/11）

```js
// 1) 14 态矩阵：mock 桥候选 14 个组件（每组件 4 仓同态），或 Host 夹具工作区；逐态逐键断言
for (const state of STATES) {
  selectComponent(`C_${state}`);
  for (const [action, expected] of Object.entries(SRS_MATRIX[state])) {
    const b = document.querySelector(`[data-testid=action-${action.toLowerCase()}]`);
    assert(b.disabled === !expected.enabled, `${state}/${action}`);
    if (!expected.enabled) assert(/原因：.+；替代：.+；错误码：E_[A-Z_]+/.test(b.title), b.title);
  }
}

// 2) 确认条（FR-ACT-04）：未确认 0 写 + F 区 bbox 不变
const before = await calls(); const f0 = bbox('[data-testid=repo-actions]');
click('[data-testid=action-push]');
assert(await visible('[data-testid=confirm-bar]'));
assert((await calls()).length === before.length);
assert(text('[data-testid=confirm-bar]').match(/将推送 \d+ 个提交（[^ ]+ → [^ ]+）/));
assert(JSON.stringify(bbox('[data-testid=repo-actions]')) === JSON.stringify(f0));
click('[data-testid=confirm-bar-confirm]');
assert((await calls()).length === before.length + 1);

// 3) PULL 免确认
click('[data-testid=action-pull]'); await sleep(200);
assert(!(await exists('[data-testid=confirm-dialog],[data-testid=confirm-bar]')));

// 4) CHKOUT 数据源/搜索/stash
openChkout();
assert(options().length === fixture.remoteRefs.length);
assert(await exists('[data-testid=checkout-search]'));
assert(dirtyState && await exists('[data-testid=checkout-stash]'));

// 5) COMMIT staged 文案
assert(text(dialogOrBar).includes('将提交 2 个文件'));  // stagedCount=2 fixtures

// 6) BUSY / CAS 错误态
injectError('FETCH', 'E_REPO_BUSY');
assert(statusBar().includes('E_REPO_BUSY') && statusBar().includes('外部'));
assert(await exists('[data-testid=toast-retry]'));

// 7) 状态栏模板（FR-OPS-05/§11.0.4-10）
assert(/^最近一次操作：(PULL|FETCH|CHKOUT|COMMIT|PUSH) [^ ]+\/[^ ]+ — (成功|失败\(E_[A-Z_]+\)|进行中) · \d{2}:\d{2}$/.test(lastOpText()));

// 8) AC-UI-11：E/F 同聚焦仓、无残留、1280/200%、标签列 ≤72px
assert(scopeOf('[data-testid=repo-actions]') === titleOf('[data-testid=info-repo-title]'));
for (const vp of [[1440,900],[1280,800]]) { setViewport(vp); assert(scrollWidth===clientWidth); }
```

### 7.4 建议 E2E / 夹具断言

- 真实 Gitea fixture：AC-REPO-01…06、AC-OP-29（外部 `index.lock`）、AC-OP-26/27（强杀 + 取消）、AC-REPO-11（EMPTY 首推建 upstream）。
- 状态矩阵夹具：复用 `reqs/fixtures/`，新增 14 态 + 每态 5 键期望；UI 与 Host 共用同一 JSON（单源）。
- 故障注入报告：至少覆盖 `E_REPO_BUSY`、`E_CONCURRENT_CHANGE`、`E_GIT_NON_FF`、`E_DIRTY_WORKTREE`、`E_NOTHING_TO_COMMIT`、网络失败、服务端保护分支。

### 7.5 14 态注入与“单源矩阵”根治方案

1. Host 新增只读桥方法 `getRepoActionAvailability { componentId, repoType }`（或把 availability 并入 `getSnapshot.repo.actions`），**UI 按钮条/ tooltip 只消费该返回值**；删除 `viewmodel.MATRIX`。
2. Host 侧 `GetAvailability` 的输入表从 **SRS §7.6 生成的 JSON 夹具**加载（或编译期表 + 测试从夹具反查），禁止第三份手写期望。
3. 矩阵夹具加入 `RequiresConfirm`、`ReasonCode`、`Reason`（原因/替代/错误码三段），同时供 AC-CDP 与单测使用。

---

## 8. 需求缺失 / 矛盾清单（与实现缺失严格分开）

| ID | 类型 | 内容 | 影响 |
|---|---|---|---|
| RQ-01 | **需求矛盾** | §7.6 `DIVERGED` 行 Pull(ff) 单元格为“⚠ 仅展示不自动 merge”：⚠ 在 §7.6 图例中=“允许但需 dry-run/备份/二次确认”，但文字又说“仅展示不自动 merge”，无法映射为按钮启用/禁用；UI/Host/spec §6.2 均取禁用，但无 ADR | AC-UI-08 该格不可判定；按 SRS 字面实现可能被要求“可点” |
| RQ-02 | **规格冲突** | SRS FR-ACT-04（`requirements.md:610`）要求 COMMIT/PUSH 出现 ≤48px 确认条且“确认后才执行”；spec §6.3/§6.4（`ui-redesign-spec.md:563,578`）写“COMMIT 消息框、PUSH 常规免确认”。两份规格对 PUSH 是否确认直接相反；本次实现选择了 spec（零确认） | 实现不可同时满足两份规格；FR-ACT-04/AC-UI-10 判定取决于裁决 |
| RQ-03 | **需求矛盾** | FR-ACT-03（P0）写“PUSH 需当前分支有 upstream”；FR-REPO-02/§7.6/spec 写 NO_UPSTREAM、EMPTY 允许 Push（发布分支/首推建 upstream）。UI 未定义“发布分支并建立 upstream”的确认入口与 `setUpstream` 参数契约 | NO_UPSTREAM/EMPTY 的 PUSH 验收无一致口径 |
| RQ-04 | **需求缺失** | FR-ACT-05/FR-OP-08 未定义单仓五键与任务中心/job 的关系（是否必须入 job、是否可取消、`jobId` 是否强制返回）；spec §6.3 要求 `runRepoAction` 返回 `jobId`，SRS 无对应契约 | 取消/重试/审计无法写可执行 AC |
| RQ-05 | **需求缺失** | FR-ACT-02 要求 tooltip“错误码”，但未规定错误码体系归属：SRS 附录 D 为 `CFG/SYNC/SCAN/STATE/OP/FS/NET/GIT/SEC-E0xx` 前缀，spec 附录 B/`BridgeErrors.cs` 为 `E_*`；`BridgeErrors` 注释称放 `details.srsCode`，实现却写 `sdkCode`（`RepoActionService.cs:366,594,605`） | AC 无法断言“错误码”字符串 |
| RQ-06 | **需求治理缺失** | M2 出口（`requirements.md:137`）把批量 7 操作+任务中心+备份/审计与“五键接入”捆在同一里程碑，未授权 M2a/M2b 拆分；当前实现只交付五键 | 出口完全不可达；放行判决只能为“否” |
| RQ-07 | **需求可验收性缺失** | AC-UI-08 要求“14 主状态×5 按钮逐格断言”，但未规定：14 态从哪里注入（真实 Host 只有 3 态）、断言数据源（UI 自写矩阵还是 Host 矩阵）、确认标志是否属于断言面 | AC 不可执行/可自证；这是 P0-12 的根因 |
| RQ-08 | **需求边界未定** | FR-INFO-01 要求“仓库全名、RepoType”，但未定义 MISSING（无仓）时全名来源；实现以 `<componentId>_<Type>` 合成并显示“本地路径”，是否算伪造/误导无判据 | FR-INFO-02/AC-UI-11 边界模糊 |

---

## 9. 缺口清单（P0 / P1 / P2）与 Top10

### 9.1 P0（12 项，阻断 M2 放行）

| ID | 缺口 | 条款/证据 | 类型 |
|---|---|---|---|
| P0-01 | 无 ≤48px 确认条；PUSH 零确认直接执行；COMMIT 无“将提交 N 个文件” | FR-ACT-04；实测 `confirm-bar=false`、`workbench.ts:1097-1105,1070-1073` | 实现缺失（含 RQ-02 冲突） |
| P0-02 | 禁用 tooltip 缺“原因+替代动作+错误码”，存在通用文案；UI 未消费 Host `ReasonCode` | FR-ACT-02；实测 AHEAD COMMIT title；`viewmodel.ts:710,714`；`RepoActionService.cs:93-109` | 实现缺失 |
| P0-03 | CHKOUT 选择器不来自 `remote_ref`、无搜索、无 `stash -u` 选项；无 `getRemoteRefs` 桥 | FR-ACT-03、FR-OP-03、AC-OP-10；实测建议 `["main"]`；`workbench.ts:1118-1153`；`HostCommandRouter.cs:207-335` | 实现缺失 |
| P0-04 | 写前无 read-through 重扫、无 CAS（preflight HEAD==执行时 HEAD）；矩阵基于缓存快照 | FR-ACT-03、§7.5（`requirements.md:331`）；`RepoActionService.cs:153-161,245,281` | 实现缺失 |
| P0-05 | BUSY / CONCURRENT_CHANGE 未实现：`busy` flag 不消费、无 `E_REPO_BUSY`/`E_CONCURRENT_CHANGE` 产生路径 | FR-ACT-05、FR-OP-10、AC-OP-29；`LocalScanner.cs:189`；`BridgeErrors.cs:19`；`RepoActionService` 全文 | 实现缺失 |
| P0-06 | 无合作式取消、无 job/jobId、无任务中心记录与“仅重试失败项” | FR-ACT-05/06、FR-OP-08、AC-UI-10；`workbench.ts:1184-1277`；`DrawerHost.vue:38-45`；Host 成功响应无 jobId（`RepoActionService.cs:287-300`） | 实现缺失 |
| P0-07 | 无写前 journal + 崩溃 reconcile + 任务持久化（因无 DB） | FR-SAFE-04、FR-OP-08；`src/` 无 SQLite/journal/operation_log（全仓检索为空，仅 `MbdGitTool.Git/Program.cs:8` 注释） | 实现缺失 |
| P0-08 | 无审计（operation_log / 前后 HEAD / actor / request_id） | FR-OP-09、FR-ACT-06、AC-REPO-04④/06⑤；`RepoActionService.cs:275-285` 仅 HostLog | 实现缺失 |
| P0-09 | FR-REPO-05 修复类操作（Repair/Re-clone、set-upstream、detached 建分支、EMPTY 首推建 upstream）未实现；UI 入口只 Toast“M2 接入”；UI 从不传 `setUpstream` | FR-REPO-05、AC-REPO-08..11；`RepoTreePanel.vue:83-91`；`api.ts:211-217` | 实现缺失 |
| P0-10 | M2 批量写操作（Clone/Pull/Checkout/Tag/SYNC/Delete/Restore/Create）与备份/恢复整块未交付 | SRS §3.3 M2（`requirements.md:137`）、FR-OP-01…12；`workbench.ts:1303-1321` | 实现缺失 |
| P0-11 | 矩阵双源且无单源契约；UI 14 态逐格无验收证据；提交的 CDP selfcheck 已过期（跑必红） | SRS §7.6 末条（`:357`）；`viewmodel.ts:472-554`；`RepoActionService.cs:49-66`；`ui/scripts/cdp-m2-selfcheck.mjs:46-71,596-629` | 实现+验收缺失 |
| P0-12 | M2 出口 AC（AC-OP-01…32、AC-REPO-01…11、AC-SAFE-01…04、AC-UI-08/10/11）无执行/证据，无故障注入报告 | SRS §3.3/§13；`docs/m2-ops-status.md` 仅“234/234 单测” | 验收缺失 |

### 9.2 P1（10 项，应在 M2/M3 修复但不单独阻断）

| ID | 缺口 | 条款/证据 |
|---|---|---|
| P1-01 | 状态栏“最近一次操作”模板不符（缺前缀、componentId、括注；未操作非“无”） | FR-OPS-05、§11.0.4-10；`workbench.ts:341-349`；实测 `就绪 · 无最近操作` |
| P1-02 | 失败 Toast 无“重试/详情”；Toast 双 ✕；任务中心不可达 | spec §6.3/6.5；`ToastHost.vue:20`、`workbench.ts:1271` |
| P1-03 | NO_UPSTREAM/EMPTY 的 PUSH 不建 upstream、无“发布分支”提示（UI 不传 `setUpstream`） | FR-REPO-02、AC-REPO-05/09/11；`workbench.ts:1070-1073`、`RepoActionService.cs:475-483` |
| P1-04 | Host COMMIT message 上限 2000 ≠ SRS 1–200 | FR-ACT-03；`RepoActionService.cs:32-33,750-757` |
| P1-05 | 错误码体系未对齐附录 D/FR-OPS-09；实现用 `sdkCode` 而非注释承诺的 `srsCode` | FR-OPS-09；`BridgeErrors.cs:4-5`；`RepoActionService.cs:366,594,605` |
| P1-06 | 组件详情抽屉 ESC 后焦点未归还触发控件 | FR-WS-04、AC-UI-13；本次实测 BODY；`ComponentDrawer.vue:29`（仅入向 focus） |
| P1-07 | 五键实测 96×30：高度 < 32（SRS §11.0.2 F“按钮最小 88×32”；§10.5 主按钮 ≥32px；spec §6.1 高 40） | `RepoActionsBar.vue:77-99`；本次实测 |
| P1-08 | 版本/阶段文案不一致：标题 v0.3.0 vs Host `appVersion=0.1.0.0`；任务中心/菜单仍写“M2 接入” | 实测；`TitleBar.vue:36`；`DrawerHost.vue:39-45`；`MenuBar.vue:58-61` |
| P1-09 | 离线降级未落地：`state.online` 恒 true，FETCH 不因离线禁用 | FR-ACT-03、NFR-REL-01；`workbench.ts:121,274` |
| P1-10 | 信息面板细节：STALE 无“数据 X 前”+斜纹；RepoType 无显式字段；MISSING 显示不存在的本地路径 | FR-INFO-01/02；`InfoPanel.vue:200,208`；实测 |

### 9.3 P2（8 项）

1. 启用态/确认态文案泄漏协议参数（Host reason 里的 `args.confirm=true`，`RepoActionService.cs:103`）。
2. 通知中心初始 Mock 种子数据在真实模式仍显示（UX P2-4）。
3. 状态栏“在线”恒真，网络段无信息量（UX P2-10）。
4. 信息面板空态“重试”按钮语义弱/可能与自动选中冲突（`InfoPanel.vue:158`）。
5. 死代码 `workspaceNodeToRepo`（`workbench.ts:1362-1387`）与“SYNC/CREATE（M2 接入）”卡片占位文案。
6. AC-UI 未回写 `ac-catalog`，AC 权威源暂分叉（§13.4 末注）。
7. Toast 成功文案 `✓FETCH` 空格/图标间距（UX P2-2）。
8. 200%/1280 布局与焦点环未在本轮复测（既有 UX 已测部分通过）。

### 9.4 Top10（按优先级）

1. **补确认条**：COMMIT/PUSH 使用 ≤48px 内联确认条（N 文件/N 提交、分支→upstream），未确认 0 写，F 区 bbox 不变（FR-ACT-04）。
2. **写前 read-through + CAS + BUSY**：action 前重扫并比对 expect HEAD、消费 `busy` flag、返回 `E_REPO_BUSY`/`E_CONCURRENT_CHANGE`、禁止删锁（FR-ACT-03/05、§7.5、AC-OP-29）。
3. **CHKOUT 数据与安全**：新增 `getRemoteRefs`（branches/tags + 搜索），对话框加 `stash -u` 选项与影响说明（FR-ACT-03、FR-OP-03）。
4. **tooltip 三段式**：按钮条改消费 Host `GetAvailability`，每格输出“原因+替代动作+错误码”，删除通用文案与双矩阵（FR-ACT-02、§7.6）。
5. **单仓操作 job 化**：返回 jobId、任务中心逐仓/单仓记录、取消语义、仅重试失败项（FR-ACT-05/06、FR-OP-08）。
6. **审计 + 写前 journal + reconcile**：落 `operation_log`（前后 HEAD、备份 ref、结果）与崩溃恢复（FR-OP-09、FR-SAFE-04）。
7. **FR-REPO-05 修复类**：Repair/Re-clone、set-upstream、detached 建分支、EMPTY 首推 `-u`（AC-REPO-08..11）。
8. **M2 批量与备份恢复**：Clone/Pull/Checkout/Tag/SYNC/Delete/Restore/Create + `.trash`/`refs/backup/*`（SRS §3.3 M2）。
9. **矩阵单源 + 14 态 UI 验收**：删除 UI `MATRIX`、从 SRS 夹具生成；更新/重写 `cdp-m2-selfcheck.mjs`，14 态×5 键逐格断言（AC-UI-08）。
10. **出口口径与证据**：M2a/M2b 拆分走 ADR，回写 AC，执行 AC-OP/REPO/SAFE/UI-08/10/11 与故障注入，产出验收报告（§13.4）。

---

## 10. 放行结论与条件

### 10.1 正式结论

**M2（按 SRS §3.3/§13 定义）不可放行。** 单仓五键的“真实 git 执行 + staged-only + M1/M2 门禁 + 失败可读 + 回写”质量明显优于 M1，但 M2 出口的 16 个核对项中仅“进行中”1 项通过、4 项部分、11 项未满足；确认/前置/审计/任务中心/批量/备份/AC 证据均不满足；且 FR-ACT 作为 P0 仍有 4 条不通过。任何“M2 已交付”的对外表述都会与 SRS 相悖。

### 10.2 若产品希望“M2a 单仓子集”条件放行，最小闭环

必须先做 **需求变更（ADR + SRS §3.3 拆分 M2a/M2b + AC 回写）**，且完成以下最小集：

1. P0-01 确认条 + PUSH 确认（或 ADR 明确 PUSH 免确认并同步改 FR-ACT-04/AC-UI-10）；
2. P0-02 tooltip 三段式 + 矩阵单源（P0-11 的前半）；
3. P0-03 CHKOUT `remote_ref` + 搜索 + `stash -u`；
4. P0-04 read-through + CAS；P0-05 BUSY/CONCURRENT_CHANGE；
5. P0-06 job/任务中心/取消/重试；P0-08 审计（P0-07 journal 可列 M2b，但必须在 ADR 写明降级与风险）；
6. AC-UI-08/10 的 14 态 CDP 自动化 + 故障注入报告；AC-REPO-01④/02③（重扫）与 04④/06⑤（审计）必须转绿。

在上述 6 条完成前，**不应对内宣布 M2 完成，也不应进入 M3 收口**。

### 10.3 复评入口

修复后以本报告 §7.2/§7.3 断言清单复测，并另行产出：`docs/m2-ac-report.md`（逐 AC 证据）+ 14 态矩阵夹具 + 更新后的 `ui/scripts/cdp-m2-selfcheck.mjs`。

---

## 附录 A：本次实测原始值（节选）

```text
Host: PID 5348, CDP 9222, commit 06c6ea2, viewport 1424x861
hostInfo: appVersion=0.1.0.0, features.repoActions=true, webView2=157.0.4341.46
snapshot: components=4, repos=14, missing=6, source=local+gitea
states: RTCT_Demo=AHEAD(SIL), RTCT_Synced=SYNCED, RTCT_Incomplete=BINDING_ERROR, RTCT_RemoteOnly=MISSING
Host availability (RTCT_Synced.*): PULL/FETCH/CHKOUT/COMMIT/PUSH enabled=true, confirm=false
Host availability (RTCT_Demo.SIL=AHEAD): PULL=false, FETCH=true, CHKOUT=true(confirm=true), COMMIT=false, PUSH=true
Host availability (MISSING.*): 五键 enabled=false, rc=STATE_BLOCKED
UI RTCT_Demo.SIL buttons: PULL d=true title="AHEAD：本地领先，需先推送"; FETCH d=false;
  CHKOUT d=false; COMMIT d=true title="当前仓库状态不允许该操作"; PUSH d=false; size=96x30
UI RTCT_Synced_Impl: COMMIT d=true title="无已暂存改动：请先 git add 暂存后提交"
confirm-bar: false (querySelector) ; 全仓 grep confirm-bar/ConfirmBar = 0
CHKOUT dialog: suggestions=["main"], inputs=radio branch/tag + text, search=false, stash=false
MISSING focus: scope=DESIGN aria="单仓操作：Design" title="MISSING：仓库未克隆"
drawer: 520px, focus in drawer; ESC -> activeElement=BODY
status-last-op: "就绪 · 无最近操作"; info-last-op: "最近操作就绪 · 无最近操作"
body text: 无 "M1 只读" / "将在 M2 接入"
```

## 附录 B：关键行号索引

- SRS：M2 出口 `requirements.md:137`；单仓入口 `:107`；§7.5 写前 read-through/CAS `:331`；§7.6 矩阵 `:334-357`（末条单源 `:357`）；FR-OP-08/09/10/11 `:508-511`；FR-REPO `:514-524`；FR-SAFE-04 `:533`；FR-ACT-01…07 `:607-613`；FR-INFO-01…04 `:624-627`；§11.0.2 F/E `:726-727`；§11.0.4-10 `:753`；AC-UI-08/10/11 `:1048-1051`；AC 追溯 `:1079-1100`；附录 D 错误码 `:1260-1281`。
- spec：§6.1 `:525-528`；§6.2 `:530-555`；§6.3 `:557-570`；§6.5 `:584-588`；§5.1–5.3 `:443-501`。
- Host：矩阵 `RepoActionService.cs:49-66`；确认门 `:183-196`；staged-only commit `:445-457`；push `:470-501`；失败映射 `:555-597`；禁用原因 `:903-925`；路由方法表 `HostCommandRouter.cs:30-44,207-335`；错误码 `BridgeErrors.cs:4-38`；busy 采集 `LocalScanner.cs:182-194`；staged 采集 `:253`；超时/取消 `GitCli.cs:109-124`。
- UI：矩阵 `viewmodel.ts:472-554`；可用性 `:681-720`；staged 门 `:699-707`；错误映射 `:591-662`；五键执行 `workbench.ts:1045-1078`；COMMIT/CHKOUT 对话框 `:1087-1153`；执行/确认重试 `:1184-1277`；状态栏 `:341-349`；能力门禁 `:320-335`；焦点 `ComponentDrawer.vue:29`、`App.vue:35`；按钮样式 `RepoActionsBar.vue:77-99`；面板 `InfoPanel.vue:75-238`。
- UX：8 P0 `review-m2-ux.md:289-300`；矩阵实测 `:65-79`；P0-1 `:81-86`；P0-3 `:134-141`；P0-4 `:118-127`；P0-5 `:156-166`；P0-7 `:143-149`。
- 状态/实现报告：`m2-ops-status.md:6-20,35-46,55-61`。
- 自测脚本：`ui/scripts/cdp-m2-selfcheck.mjs:46-71,529-554,555-591,596-629`。
