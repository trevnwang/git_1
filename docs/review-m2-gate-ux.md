# M2a Gate 复评（UX/需求）：提交 `1afa014` 闭环剩余 P0 + M2a/ADR 一致性

> 复评角色：独立 Gate 复评员（UX/需求）
> 复评对象：提交 `1afa014`（fix(m2): close security P0 and UX P0, add M2a/M2b ADR）
> 前置结论：`docs/review-m2-ux-fixcheck.md`（13:10–13:45）判定「M2 不可放行」，列出剩余 P0（P0-3 内容、P0-4/NP0-1、P0-5、P0-6、P0-7）与最小复评门禁 6 条。
> 硬性约束：本轮**只评审**，未修改任何代码/需求/构建产物/真实仓库，亦未执行任何真实写操作；仅新增本报告与 `artifacts/ui-review/gate/*`（gitignore）证据脚本/截图/JSON。
> 评审时间：2026-11-14 14:07–14:20（本地）。

---

## 0. 结论（放行判决）

| 维度 | 结论 |
|---|---|
| `review-m2-ux-fixcheck.md` 剩余 P0（5 组） | **行为层面 5/5 全部闭环**（P0-3 的「staged 文件列表」降级为 P1 残留，见 §2.1） |
| 本轮 6 项必须实测 | **5 PASS / 1 FAIL**（FAIL=需求/ADR 一致性，见 §4）；细化断言 **26 PASS / 8 FAIL**（行为面仅 2 条 FAIL：COMMIT 非「点击直出确认条」、AC-UI-10a 文案） |
| SRS v0.3.1 是否明确 M2a/M2b 出口 | ✅ 明确（§3.3 两行 + §9.16/§9.17 范围注 + 13 条 M2a 出口 AC 清单） |
| SRS v0.3.1 AC 是否可验收 | ❌ **不可**：§13.1.2 不存在；AC-UI-10a/10b/10c/15、AC-SAFE-14…17、AC-OP-33 在 SRS 只有引用、无 Given/When/Then；§13.4 追溯矩阵未按 changelog 更新 |
| M2a 出口是否覆盖本轮 UX 项 | ✅ 覆盖（确认条=AC-UI-10a/10b、焦点=AC-UI-13、tooltip/矩阵=AC-UI-08、CHKOUT=AC-UI-10、COMMIT 对话框=AC-UI-10/10a） |
| 是否发现「文档说通过但实测不过」 | ✅ **4 处**（AC-UI-08 单源矩阵/RequiresConfirm、AC-UI-08 tooltip 冻结格式、AC-UI-10a 点击即确认条+文案、AC-OP-33/AC-UI-15 未实现；另 ADR §5 三件证据全缺） |
| **是否建议 M2a 打 tag** | **❌ 不建议**。UX P0 已闭环，但 M2a 出口的 **5 项 Gate 级 P0** 未满足（§5），且 ADR 明文「`m2a-ac-report.md`/`m2-matrix.json`/`cdp-m2a-selfcheck.mjs` 缺一不得宣布 M2a 通过」 |
| 新 P0 | **5 项（Gate 级，均属 M2a 出口未满足，非 fixcheck UX 回归）**；新 P1 4 项；新 P2 3 项 |

> **一句话**：提交把「确认条/焦点/tooltip/CHKOUT/COMMIT 对话框」这一层 UX P0 实打实修好了（实测可复现、零真实写），但**还不足以给 M2a 打 tag**——矩阵单源与 Host `RequiresConfirm`、审计-lite、M2b 边界提示、状态栏模板、以及 SRS AC 定义/三件证据都不是 UX 措辞问题，而是 M2a 出口 AC 本身未交付。

---

## 1. 范围、环境与方法

### 1.1 实测环境（真实 Host + 真实 Gitea 夹具）

| 项 | 值 |
|---|---|
| Host | PID 9264，`src\MbdGitTool.Host\bin\Debug\net10.0-windows\MbdGitTool.Host.exe`，14:06:43 启动 |
| 页面 | `https://app.local/index.html`，CDP 9222，bundle `index-CZwlgGWA.js`（14:06:33 构建，含本提交前端改动） |
| 视口 | 1424×861（dpr=1） |
| 数据 | 真实 `RTCRepos`：4 组件 / 14 仓；本地 8 仓（`artifacts/real-test/workspace`） |
| 仓状态 | RTCT_Demo_SIL=`AHEAD`（staged 1/dirty 1/ahead 1）；RTCT_Demo_Design/Impl/MIL=`SYNCED`；RTCT_Synced_*= `SYNCED`（staged 0）；RTCT_Incomplete_*= `MISSING`；RTCT_RemoteOnly_*= `MISSING` |
| 交互方式 | CDP `Input.dispatchMouseEvent` / `Input.dispatchKeyEvent` 真实事件（非 JS click），选择器/bbox 断言 + 截图 |

### 1.2 零真实写保证（关键）

真实 Host 上采用**桥边界拦截**：在页面内替换 `window.chrome.webview.postMessage`，命中 `method==='runRepoAction'` 时**只记录 args、不下发 Host**，并用合成的 `{ok:false,code:'E_GATE_BLOCKED'}` 回执让 UI 闭环（因此可以安全点击「确认提交/确认推送/检出」捕获真实 args）。评审结束后复核夹具仓：

- `RTCT_Demo_SIL` 仍为 `M component.txt` + HEAD `e67eb05`（与评审前一致）；其余 7 仓 `git status` 干净、HEAD 与评审前一致；
- 未执行 PULL/FETCH/CHKOUT/COMMIT/PUSH 任何真实写。

### 1.3 证据分层

| 层 | 用途 | 证据 |
|---|---|---|
| A 真实 Host 只读 + 桥拦截 | 确认条（PUSH/Alt+5）、CHKOUT 真实 refs/搜索/选择/stash、tooltip、抽屉焦点、零写计数 | `artifacts/ui-review/gate/{lib.mjs, real-01-push.mjs, real-02-chkout.mjs, real-03-m2b.mjs}`、`real-01…07*.png` |
| B 桥注入 Mock（真实快照 + 定向 patch） | COMMIT 对话框/确认条/stageAll、PUSH `NO_UPSTREAM`、CHKOUT 三组 refs（本地/远程/Tag）、ConfirmDialog（danger）焦点 | `mock-bridge.js`、`mock-01-flow.mjs`、`mock-02-confirm-focus.mjs`、`mock-01…06*.png` |
| C 代码/契约只读核对 | 确认条/焦点/CHKOUT/审计/矩阵单源/状态栏模板 | 文中 `文件:行号` |
| D 需求/ADR 核对 | SRS v0.3.1、ADR D-025、fixcheck | 文中 `文件:行号` |

### 1.4 限制（不掩盖）

1. 真实夹具无 `NO_UPSTREAM` 仓、无 Tag、无「SYNCED+staged>0」仓、无危险扩展名文件 → 这些分支用**注入 Mock 桥**（真实 Host 快照 + 定向 patch，`getSnapshot` 等仍转发真实 Host）验证，未用 Mock 冒充真实写回执。
2. `ConfirmDialog` 在真实数据下无可达触发入口（唯一只读入口是危险扩展名打开）→ 用注入树（`run.bat`）+ 真实 Host 快照验证，触发链为真实 UI 事件。
3. `dotnet test` 无法复跑：运行中 Host 锁定 `bin`（与上一轮同因）；提交所称「262/262」**未独立复核**，且按 ADR §2.4-4 单测数量本就不能替代 AC 证据。
4. 未执行真实五键写操作（遵守「不得修改真实仓库」）；≤2s 回写、审计行、CAS/BUSY 属真实写链路，本轮不覆盖（ADR 要求另出 `m2a-ac-report.md`）。

---

## 2. `review-m2-ux-fixcheck.md` 剩余 P0 逐项复核

> 判定口径：以 fixcheck §7 最小复评门禁为准；行为可复现、且「未确认 0 写/焦点归还/内容齐全」有实测值即算闭环。

| # | fixcheck 剩余 P0 | 本轮判定 | 实测证据（选择器/值/文件:行号） | 残留 |
|---|---|---|---|---|
| P0-3（内容项） | COMMIT 对话框缺「已暂存 N / 将提交 N」 | ✅ **核心闭环** | 注入 staged=3、SYNCED 后点击 `[data-testid=action-commit]`：对话框正文含「已暂存 3 个文件 / 将提交 3 个文件」，info-grid 同时显示 `已暂存 3 个文件`、`将提交 3 个文件`（`mock-01-commit-dialog.png`）；代码 `workbench.ts:1147-1185` | **staged 文件列表仍未提供**（spec §6.3 要求；fixcheck §7-5 亦列）→ P1，需产品裁决/补列表 |
| P0-4 + NP0-1 | 无 ≤48px 确认条；PUSH 零确认 | ✅ **闭环** | 真实 PUSH：`[data-testid=confirm-bar]` 高 **44px**、位于 5 键上方、文本「将推送 1 个提交（main → origin/main）」；F 区/5 键 bbox 与开条前**逐像素一致**（x=589.2/699.2/809.2/919.2/1029.2，y=780，96×30）；未确认 `runRepoAction` 调用 **0**；ESC/取消 0 写且焦点回 `action-push`；`Alt+5` 同样开条、0 写；接受后恰 1 次 `runRepoAction(PUSH,args={})`；`NO_UPSTREAM`（注入）文案含「发布分支…建立 upstream（setUpstream=true）」，接受后 `args.setUpstream===true`。代码 `RepoActionsBar.vue:114,189,31-55`、`workbench.ts:1130-1133,1208-1228` | 无（AC 字面/ Host 防御口径偏差另列 §4.7/§5） |
| P0-5 | ConfirmDialog/ComponentDrawer 焦点未闭环 | ✅ **闭环** | ConfirmDialog（danger，注入危险文件双击）：初始焦点=「仍然打开」且在 `[data-testid=confirm-dialog]` 内；Tab×5 逃逸 **0**；ESC 关闭、焦点回触发控件 `DIV.tree-scroller`（取消按钮同样归还）；抽屉（真实双击卡片）：初始焦点 `component-drawer-close`、Tab×8 逃逸 0、ESC 后 active=`ARTICLE[data-testid=component-card]`。代码 `ConfirmDialog.vue:15-26`、`ComponentDrawer.vue:31-51` | 无 |
| P0-6 | 禁用 tooltip 无错误码/替代动作 | ✅ **闭环（任务口径）** | 真实 MISSING：5 键 title=`MISSING：仓库未克隆 [E_REPO_NOT_LOCAL] → 先 Clone`；真实 SYNCED+staged0 COMMIT=`无已暂存改动… [E_NOTHING_TO_COMMIT] → 先 git add…`；AHEAD PULL/COMMIT、NO_UPSTREAM PULL/CHKOUT/COMMIT 均含 `[E_*] → 替代`；代码 `viewmodel.ts:472-494,712-722` | 与 ADR §5 AC-UI-08 冻结正则 `/原因：…；替代：…；错误码：E_…/` 不一致 → §4.7、P1-2 |
| P0-7 | CHKOUT 无 remote_ref/搜索/stash | ✅ **闭环** | 真实 Host `getRemoteRefs`：`共 3 个候选`，分组`本地分支 1 / 远程分支 2`（origin/HEAD、origin/main），`input[type=search][data-testid=checkout-ref-search]` 存在，输入 `mai` 过滤 3→2；选中 `refs/heads/main` → `args={branch:'main',stash:true,confirm:true}`；注入 6 refs 时三组齐全（`LOCAL_BRANCH 2 / REMOTE_BRANCH 2 / TAG 2`），搜索 `v1` → 仅 Tag，选 `refs/tags/v1.0.0` → `args={tag:'v1.0.0',confirm:true,stash:true}`；dirty 仓 `[data-testid=checkout-stash]` 勾选后 `args.stash===true`。代码 `workbench.ts:1253-1385`、`ActionDialog.vue:74-331`、Host `RemoteRefsService.cs`/`HostCommandRouter.cs:262` | refs 把 `origin/HEAD` 也列为可选「HEAD」→ P2 |

**小计：fixcheck 剩余 P0 行为层 5/5 闭环（P0-3 的 staged 文件列表残留 P1）。**

---

## 3. 6 项「必须实测」逐项结果

### 3.1 确认条（COMMIT/PUSH）

| 子项 | 判定 | 实测值/证据 |
|---|---|---|
| PUSH 点击仅出现 confirm-bar（无其他模态） | ✅ | 真实：点击 `[data-testid=action-push]` 后仅 `confirm-bar`，无 `action-dialog`/`confirm-dialog` |
| 确认条高度 ≤48px | ✅ | `getBoundingClientRect().height = 44.00` |
| 5 键 bbox 开条前后不变 | ✅ | 5×96×30 坐标完全一致；F 区 bbox `{x:306.39,y:769,w:1101.61,h:52}` 不变（`real-01-push-confirm-bar.png`） |
| 未确认前 `runRepoAction` 写调用=0 | ✅ | 桥拦截计数 `{intercepted:0, calls:[]}`；Alt+5、取消、ESC 均 0 |
| PUSH 文案含提交数与 `分支 → upstream` | ✅ | `将推送 1 个提交（main → origin/main）`；NO_UPSTREAM 注入：`将推送 2 个提交（main → origin/main）` + detail `发布分支…首次推送将建立 upstream（setUpstream=true）` |
| NO_UPSTREAM 传 `setUpstream:true` | ✅ | 接受后 `runRepoAction.params.args = {setUpstream:true}`（`mock-04-push-no-upstream.png`） |
| `Alt+5` 同路径、未确认 0 写 | ✅ | 真实 `Alt+5` → 条出现、`intercepted:0`；ESC 后 0 写 |
| 取消/ESC 关闭并归还焦点 | ✅ | 取消/ESC 后条消失、`activeElement=action-push`、0 写 |
| **COMMIT 点击直出 confirm-bar（AC-UI-10a 字面）** | ❌ | 实际先弹 COMMIT 输入对话框（`action-dialog`），需填 message + 点「下一步：确认提交」才出条（`mock-01-commit-dialog.png`） |
| COMMIT 确认条 ≤48px / bbox 不变 / 未确认 0 写 | ✅ | 44px；5 键 bbox 与开条前一致；`calls:[]`；ESC 后焦点回 `action-commit` |
| COMMIT 文案匹配 `/将提交 N 个文件/` | ❌ | 实际 `将提交 3 个已暂存文件`（多「已暂存」三字，AC-UI-10a 字面正则不匹配） |
| COMMIT 接受后 args 正确 | ✅ | 未勾选：`args={message:'gate commit B'}`；勾选：`args={message:'gate commit C',stageAll:true,confirm:true}` |

### 3.2 COMMIT 对话框

| 子项 | 判定 | 实测值/证据 |
|---|---|---|
| 显示「已暂存 N / 将提交 N」 | ✅ | staged=3：正文「已暂存 3 个文件 / 将提交 3 个文件」；`未提交 已跟踪 1 · 未跟踪 0` |
| 「暂存全部改动（git add -A）」checkbox | ✅ | `[data-testid=commit-stage-all]` 存在、`checked=false`，hint「未勾选：另有未跟踪/已修改 1 处不会进入本次提交」 |
| 仅显式勾选时 `stageAll=true` | ✅ | 未勾选 accept → args **无** `stageAll`；勾选后 hint 变「已勾选：将先 git add -A，提交全部约 4 个文件」，accept → `stageAll:true` |
| staged=0 时 COMMIT 禁用并给原因 | ✅ | 真实 RTCT_Synced_Impl：`disabled=true`，title `无已暂存改动：请先 git add 暂存后提交 [E_NOTHING_TO_COMMIT] → …` |
| 真实仓验证限制 | — | 真实 8 仓无「SYNCED+staged>0」，故 COMPLET 对话框用注入 staged=3 验证（AHEAD 仓按 §7.6 COMMIT 禁用） |

### 3.3 焦点闭环

| 子项 | 判定 | 实测值/证据 |
|---|---|---|
| ConfirmDialog（danger）初始焦点在框内 | ✅ | `activeElement` = 按钮「仍然打开」，`.closest('[data-testid=confirm-dialog]')` 非空；role=`alertdialog`、`aria-modal=true`、`.btn.danger` 存在 |
| Tab 不逃逸 | ✅ | Tab×5 全部 `inConfirmDialog=true`，逃逸 0 |
| ESC 关闭并归还触发控件 | ✅ | ESC/取消后 `activeElement` = 关闭前的 `DIV.tree-scroller`（危险文件树触发器），0 写 |
| ComponentDrawer 关闭焦点归触发控件 | ✅ | 真实双击卡片开抽屉：初始 `component-drawer-close`，Tab×8 逃逸 0；ESC 后 `activeElement=ARTICLE[data-testid=component-card]`（该组件卡片） |
| 确认条焦点 | ✅ | 开条后焦点=`confirm-bar-accept`；Tab 只在「取消/确认」间循环；ESC 回 `action-push` |

### 3.4 tooltip

| 子项 | 判定 | 实测值/证据 |
|---|---|---|
| 禁用按钮 title 含 `E_*` | ✅ | MISSING 5 键 `[E_REPO_NOT_LOCAL]`；SYNCED 无 staged COMMIT `[E_NOTHING_TO_COMMIT]`；AHEAD PULL/COMMIT `[E_ACTION_NOT_ALLOWED]`；NO_UPSTREAM 注入 PULL/CHKOUT/COMMIT 均带码；按钮另有 `data-reason-code` 属性 |
| 含替代动作 | ✅ | `→ 先 Clone / → 先 PUSH 发布分支并建立 upstream / → 先 git add…` 等 |
| 与 ADR AC-UI-08 冻结格式一致 | ❌ | ADR 断言 `/原因：.+；替代：.+；错误码：E_[A-Z_]+/`；实测格式 `原因 [E_*] → 替代`，**0 匹配**（`real-06-missing-tooltips.png`） |

### 3.5 CHKOUT

| 子项 | 判定 | 实测值/证据 |
|---|---|---|
| refs 列表（本地/远程/Tag） | ✅ | 真实：本地分支 1（main）、远程分支 2（origin/HEAD、origin/main）；注入：三组 2/2/2（含 Tag v1.0.0/v0.9.0）；`checkout-ref-group-LOCAL_BRANCH/REMOTE_BRANCH/TAG` |
| 搜索过滤 | ✅ | `input[type=search][data-testid=checkout-ref-search]`；真实输入 `mai` 3→2、注入输入 `v1` 6→1（仅 TAG 组，source「共 1 个候选」） |
| 选择后 args branch 正确 | ✅ | 真实选 `refs/heads/main` → `args={branch:'main',confirm:true,stash:true}` |
| 选择后 args tag 正确 | ✅ | 注入选 `refs/tags/v1.0.0` → `radio tag=true`、输入框 `v1.0.0`、`args={tag:'v1.0.0',…}` |
| dirty 时 stash checkbox → args.stash=true | ✅ | 真实 RTCT_Demo_SIL（dirty=1）：`[data-testid=checkout-stash]` 勾选→`args.stash=true`（`real-05-chkout-dirty-stash.png`） |
| 取消/ESC 0 写 + 焦点归还 | ✅ | ESC 后 active=`action-chkout`，`calls:[]`；Tab×10 逃逸 0；初始焦点=`checkout-ref-search` |

### 3.6 需求/ADR 一致性

见 §4；判定 **FAIL**（出口明确但 AC 不可验收，且 5 项出口 AC 未满足）。

**6 项汇总：5 PASS / 1 FAIL（需求/ADR）；细化断言 26 PASS / 8 FAIL。**

---

## 4. SRS v0.3.1 / ADR D-025 一致性核对

### 4.1 出口是否明确

- ✅ SRS §3.3 `reqs/specification.md:139` 已拆 M2a/M2b：M2a 出口 = 13 条 AC（AC-UI-08/09/10/10a/10b/11/13/15 + AC-SAFE-14…17 + AC-OP-33），M2b 出口另列；§3.3 注（`:143-147`）明确「M2 总出口 = M2a ∧ M2b」「M2a 当前=待复评，不得写已通过」。
- ✅ §9.16 M2a 范围注（`:619,631-637`）：确认条/焦点/tooltip/M2b 边界/状态栏模板/审计一条均纳入 M2a；§9.17 M2a 范围（`:653`）。
- ✅ ADR D-025 §2.2 九项 M2a 交付、§3 出口 AC 表、§2.6 债务表齐全，与 SRS 口径一致。
- ✅ 未发现任何文档提前声称「M2a 已通过」；`docs/m2-ops-status.md:3-5` 仍写 Gate 不通过（诚实状态）。

### 4.2 AC 是否可验收（FAIL）

| 缺口 | 证据 |
|---|---|
| **§13.1.2 不存在** | `grep -n "^### 13\." requirements.md` 仅有 13.1/13.1.1/13.2/13.3/13.4；SRS `:1026` 与 ADR §3/§6 引用的「§13.1.2 的 AC-SAFE-14…17、AC-OP-33」**全文无定义**（AC-SAFE-17 在 requirements.md 命中 0 次） |
| AC-UI-10a/10b/10c/15 无 Given/When/Then | §13.1.1 表（`:1066-1081`）止于 AC-UI-14；AC-UI-10a/10b/10c/15 仅出现在 changelog/§3.3/§9.16/§13.1.1 前言等引用处（分别命中 5/5/5/7 次，均非定义行） |
| §13.4 追溯矩阵未更新 | changelog（`:30`）声称「§13.4 追溯矩阵与门禁更新」，但 FR-ACT 行（`:1122`）仍只有 AC-UI-08/09/10，FR-SAFE 行（`:1116`）仍只有 AC-SAFE-01…13；无 AC-UI-10a/10b/15、AC-SAFE-14…17、AC-OP-33 |
| 三件证据缺失（ADR 硬门槛） | ADR `:182`「产出 `docs/m2a-ac-report.md`、`reqs/fixtures/m2-matrix.json`、新 CDP 脚本；三者缺一不得宣布 M2a 通过」；实际三者**均不存在**（旧 `ui/scripts/cdp-m2-selfcheck.mjs` 未更新，仍为 `9614772` 提交版本；本提交只新增 `cdp-checkout-selfcheck.mjs`） |

> 结论：M2a 出口 13 条中，8 条在 SRS 侧无法独立验收；ADR 虽给了条件，但缺少 ADR 自定的执行物 → **当前不具备打 tag 的可审计条件**。

### 4.3 M2a 出口是否覆盖本轮 UX 项

✅ 覆盖：确认条=AC-UI-10a/10b；焦点=AC-UI-13；tooltip+矩阵单源=AC-UI-08；CHKOUT refs/搜索/stash=AC-UI-10；COMMIT staged 内容=AC-UI-10/10a。**问题不在「没覆盖」，而在「覆盖的 AC 未全绿/未定义」。**

### 4.4 矩阵单源 + Host `RequiresConfirm`（FAIL，Gate-P0）

三层证据均不满足 ADR AC-UI-08：

1. **UI 仍持第二套矩阵**：`ui/src/viewmodel.ts:502` `const MATRIX`、`:725 actionAvailability()` 本地逐格计算；`ui/src/api.ts:280 RequestMethod` 无 `getAvailability`，`HostCommandRouter.cs:218-348` 亦无该 case；UI 也未解析 `getComponentDetail` 的 `repos[].actions`。
2. **Host `RequiresConfirm` 与冻结口径冲突**：真实 Host `getComponentDetail(RTCT_Demo).repos[0].actions` 实测 SYNCED 五键全部 `requiresConfirm:false`（含 COMMIT/PUSH）；而 SRS §7.6 冻结（`:370`）要求「COMMIT/PUSH 确认标志固定 C」，ADR AC-UI-08 Host 断言（`:168`）要求 `RequiresConfirm = (cell==⚠) ∨ action∈{COMMIT,PUSH}`。Host 实现只取 `cell=='C'`（`RepoActionService.cs:113`），矩阵 SYNCED=`YYYYY`（`:63`），因此 COMMIT/PUSH 永远 false。
3. **单源夹具缺失**：`reqs/fixtures/m2-matrix.json` 不存在 → AC-UI-08 的「夹具驱动断言」无据可依。

> 影响：AC-UI-08 判 **FAIL**；同时意味着「直接调用桥/脚本可绕过确认条写」的防御层缺失（UI 层确认条仍有效，故 fixcheck NP0-1 行为闭环不受影响）。

### 4.5 审计-lite（FAIL，Gate-P0）

- ADR §2.2-6/§3-13 要求 M2a 每次动作恰写 1 行 `operation_log`（`ts/actor/componentId/repoType/action/before_head/after_head/result/error_code/request_id`，成功/失败都写，重启可查）。
- 实测/静态：全仓（排除 bin/obj/node_modules/artifacts）`operation_log|OperationLog|audit|审计` **0 命中**；Host 无 SQLite 审计表/写入路径 → **AC-OP-33 未交付**。

### 4.6 M2b 边界提示（FAIL，Gate-P0）

- AC-UI-15 要求 M2a 构建中批量/任务中心入口点击显示「**M2b 未接入（批量/作业/审计）**」且写调用=0。
- 实测真实 Host：任务中心抽屉文案为「批量 Clone / Pull / 切 Tag / SYNC / Delete 将在 **M2 接入**后…」「取消 / 重试失败项 **M2 接入**」；全页无「M2b 未接入」。
- 代码：`DrawerHost.vue:39-45`、`BatchBar.vue:27-30`、`MenuBar.vue:60-61`、`ComponentCard.vue:121-122` 仍写「M2 接入」。
- 注：入口可点且不触发 Host 写（点了只弹 toast），但**文案不满足 AC-UI-15**，且与 v0.3.1「M2b 拆分」自相矛盾。

### 4.7 「文档说通过但实测不过」偏差清单

| # | 文档断言 | 实测 | 差异性质 |
|---|---|---|---|
| D-1 | ADR AC-UI-08 Host 行：`RequiresConfirm = (cell==⚠) ∨ action∈{COMMIT,PUSH}`；「UI 源码中不存在第二套矩阵（viewmodel.ts 无 MATRIX）」 | Host COMMIT/PUSH `requiresConfirm=false`；`viewmodel.ts:502 MATRIX` 仍在 | 实现未达 AC（Gate-P0，§4.4） |
| D-2 | ADR AC-UI-08 tooltip 断言 `/原因：.+；替代：.+；错误码：E_[A-Z_]+/` | 实际 `原因 [E_*] → 替代`，0 匹配 | 文案/AC 冻结字符串不一致（P1） |
| D-3 | ADR §5 AC-UI-10a：`click(action-commit)` → `visible(confirm-bar)`；文案 `/将提交 2 个文件/` | 先弹 ActionDialog；文案「将提交 N 个已暂存文件」 | 步骤/正则不一致（P1；安全不变量仍满足） |
| D-4 | changelog 声称 §13.4 已更新；SRS 前言称 M2a 出口 AC 见 §13.1.1/§13.1.2 | §13.1.2 不存在；10a/10b/15/SAFE-14…17/OP-33 无定义；§13.4 未更新 | 需求回写未完成（Gate-P0，§4.2） |
| D-5 | ADR §3 要求 AC-OP-33、AC-UI-15 | 未实现 | Gate-P0（§4.5/§4.6） |
| D-6 | SRS §9.16 M2a 必交付含「状态栏『最近一次操作』模板」 | 实测 `✓ PUSH 成功 · e67eb05 · SIL · 14:10:15` / `✕ PUSH 失败 · SIL · E_GATE_BLOCKED · 14:10:15`，无「最近一次操作：」前缀、无组件 ID、无「— 成功/失败(码)」；代码 `workbench.ts:359-367` | FR-ACT-06 M2a 子集未交付（Gate-P0） |
| D-7 | fixcheck P0-3 最小门禁含「+ staged 列表」 | 已有 N 计数，无文件列表 | P1 残留 |
| D-8 | commit message「tests 262/262」 | 无法复跑（Host 锁 bin），仓库无运行证据 | 证据链（P1） |

---

## 5. 新缺陷清单与修复建议

### 5.1 新 P0（5 项，均为 M2a 出口未满足；非本次 UX 修复引入）

| ID | 问题 | 证据 | 修复 |
|---|---|---|---|
| **G-P0-1** | **AC-UI-08 单源矩阵不成立**：UI 仍自算矩阵；Host `RequiresConfirm` 对 COMMIT/PUSH 恒 false（与 §7.6 冻结 C、ADR Host 断言冲突）；`m2-matrix.json` 缺失 | §4.4；`viewmodel.ts:502,725`；`RepoActionService.cs:63,113`；真实 `getComponentDetail.repos[].actions`；`reqs/fixtures/m2-matrix.json` 不存在 | ① Host `GetAvailability` 对 COMMIT/PUSH 置 `RequiresConfirm=true`（或按 ADR 断言生成）；② 新增 `getAvailability` 桥或 UI 解析 `getComponentDetail.repos[].actions`，**删除** `viewmodel.ts MATRIX`；③ 生成 14×5 单源夹具，Host 单测+CDP 共用 |
| **G-P0-2** | **AC-OP-33 审计-lite 未实现** | §4.5：全仓 0 处 `operation_log/audit` | `RepoActionService` 每次动作（成功/失败）写 1 行 `operation_log` 最小列，补 Host 测试与 `docs/m2a-ac-report.md` 审计证据 |
| **G-P0-3** | **AC-UI-15 M2b 边界提示未实施** | §4.6 实测任务中心/批量条/菜单/卡片仍写「M2 接入」 | 统一改「M2b 未接入（批量/作业/审计）」，点击给明确提示且 Host 写调用=0，补 CDP 断言 |
| **G-P0-4** | **M2a 出口 AC 不可验收/证据链缺失**：SRS §13.1.2 缺失、10a/10b/10c/15/SAFE-14…17/OP-33 无 Given/When/Then、§13.4 未更新；ADR §5 三件证据全缺 | §4.2；ADR `:182` | 回写 SRS §13.1.1/§13.1.2 与 §13.4；产出 `docs/m2a-ac-report.md`、`reqs/fixtures/m2-matrix.json`、`ui/scripts/cdp-m2a-selfcheck.mjs`（旧脚本作废） |
| **G-P0-5** | **FR-ACT-06 状态栏「最近一次操作」模板未交付**（fixcheck P1-2 未修，ADR 列为 M2a 必交付） | §4.7 D-6；`workbench.ts:359-367`；真实状态栏 | 按 FR-OPS-05 模板输出 `最近一次操作：<动作> <组件>/<RepoType> — <成功\|失败(E_*)> · HH:mm`，补 CDP 正则断言 |

### 5.2 新 P1（4 项）

| ID | 问题 | 修复 |
|---|---|---|
| **G-P1-1** | AC-UI-10a 字面偏差：COMMIT 必须先经输入对话框；确认条文案「将提交 N 个已暂存文件」不匹配 `/将提交 N 个文件/` | 回写 AC-UI-10a（明确「对话框→确认条」两步、正则改为 `将提交 \d+ 个文件`）或改 bar 文案为「将提交 N 个文件（已暂存 M）」 |
| **G-P1-2** | AC-UI-08 tooltip 冻结格式偏差（`原因 [E_*] → 替代` vs `原因：…；替代：…；错误码：E_…`） | 二选一：统一文案格式，或回写 ADR/SRS 冻结字符串（建议后者，现文案更紧凑，避免把 85 个按钮 title 拉长） |
| **G-P1-3** | COMMIT 对话框无 staged 文件列表（spec §6.3/fixcheck §7-5） | 对话框补「已暂存文件列表」（或明确产品裁决「仅外部 git add」，并在 UI 写明） |
| **G-P1-4** | 错误 Toast 双 `✕` 残留（fixcheck P1-8 未修）：实测 `✕ ✕ PUSH 失败…`；`ToastHost.vue:20` 图标 + 文案自带 `✕` | 错误 Toast 文案去掉前导 `✕`（图标负责） |

### 5.3 新 P2（3 项）

1. CHKOUT refs 把 `refs/remotes/origin/HEAD` 列为可选「HEAD」（可选后会被当作分支名 `HEAD`），建议过滤 symbolic HEAD。
2. 五键仍 96×30（宽 ≥88、高 <32，SRS/spec 尺寸细则未对齐）；F 区 52px（≤48 是指确认条，F 区 ≤64 合规）。
3. 旧自测脚本 `ui/scripts/cdp-m2-selfcheck.mjs` 未随行为更新（仍断言 PULL/PUSH 确认对话框等），且未替换为 `cdp-m2a-selfcheck.mjs`（并入 G-P0-4）。

---

## 6. Gate 结论与打 tag 前置条件

### 6.1 结论

**不建议 M2a 打 tag（也不建议在任何文档写「M2a 已通过」）。**

理由（按权重）：
1. **5 项 M2a 出口 AC 未满足**：AC-UI-08（单源矩阵+RequiresConfirm）、AC-OP-33（审计）、AC-UI-15（M2b 边界）、FR-ACT-06 状态栏模板、以及 AC 定义/证据链缺失导致出口**不可验收**。
2. ADR §5 明文「三件证据缺一不得宣布 M2a 通过」，当前三件全缺。
3. 虽然本轮 UX 修复质量可复现（确认条/焦点/tooltip/CHKOUT/COMMIT 对话框均已闭环，零真实写验证），但**不能以 UX 子集通过替代 M2a 出口**（ADR §2.4-4）。

### 6.2 打 tag 的最小前置（建议）

1. 完成 G-P0-1…G-P0-5；其中 AC-UI-08 必须看到「UI 无第二套矩阵 + Host RequiresConfirm 正确 + 单源夹具三处一致」的可复跑证据。
2. 回写 SRS §13.1.1/§13.1.2/§13.4，产出 `docs/m2a-ac-report.md`（逐 AC 证据，含真实 Host 五键写操作 + 审计行查询）、`reqs/fixtures/m2-matrix.json`、`ui/scripts/cdp-m2a-selfcheck.mjs` 并在 CI/本机复跑。
3. 处理 G-P1-1/G-P1-2 的「AC 字面 vs 实现」二选一裁决（推荐改 AC 文案，不动已实测有效的实现）。
4. 真实夹具补一次 `NO_UPSTREAM`/Tag/SYNCED+staged 写操作 E2E（本轮按约束未执行），并在验收报告注明。
5. `docs/review-m2-ux-fixcheck.md` 的 P1-2/P1-8 等既有 P1 不阻塞 M2a 打 tag，但建议随 G-P0-5/G-P1-4 一并收口。

---

## 附录 A：关键实测值（可直接复跑）

| 项 | 值 |
|---|---|
| Host | PID 9264 / bundle `index-CZwlgGWA.js` / 视口 1424×861 / consoleErrors=0 |
| PUSH 确认条 | `h=44`、y=725、bottom=769（5 键 top=769，无重叠）、text=`将推送 1 个提交（main → origin/main）`；F 区 `1101.6×52`；5 键 `96×30 @ y=780` 开条前后一致 |
| 零写计数 | 开条/Alt+5/取消/ESC 全部 `runRepoAction calls=[]`；接受后 PUSH 1 次 `args={}`；NO_UPSTREAM 1 次 `args={setUpstream:true}` |
| 焦点 | 开条→`confirm-bar-accept`；ESC→`action-push`；ConfirmDialog→「仍然打开」；Tab×5 逃逸 0；ESC→`DIV.tree-scroller`；抽屉 ESC→`ARTICLE[data-testid=component-card]` |
| COMMIT | 对话框「已暂存 3 / 将提交 3」；checkbox 默认 false；未勾选 `args={message}`；勾选 `args={message,stageAll:true,confirm:true}`；确认条 44px、文案「将提交 3 个已暂存文件」 |
| tooltip | MISSING：`MISSING：仓库未克隆 [E_REPO_NOT_LOCAL] → 先 Clone`；NO_UPSTREAM PULL：`… [E_ACTION_NOT_ALLOWED] → 先 PUSH 发布分支并建立 upstream`；ADR 冻结正则匹配数=0 |
| CHKOUT | 真实 refs 3（本地 1/远程 2，无 Tag）；搜索 `mai`→2；选 main→`args.branch=main`；stash→`args.stash=true`；注入 6 refs 三组齐全，选 Tag→`args.tag=v1.0.0` |
| Host getComponentDetail | `repos[0].actions` 五键 `requiresConfirm:false`（SYNCED）；无 `getAvailability` 桥 |
| 审计 | `operation_log|audit` 源码命中 0 |
| M2b 提示 | 真实任务中心：「…将在 M2 接入后…」「取消 / 重试失败项 M2 接入」；全页「M2b 未接入」=0 |
| 真实仓零改动 | RTCT_Demo_SIL `M component.txt` + `e67eb05`；其余 7 仓 HEAD/status 与评审前一致 |

## 附录 B：证据文件与脚本（`artifacts/ui-review/gate/`，gitignore）

- 脚本：`lib.mjs`（CDP 真实事件封装）、`real-01-push.mjs`、`real-02-chkout.mjs`、`real-03-m2b.mjs`、`mock-bridge.js`、`mock-01-flow.mjs`、`mock-02-confirm-focus.mjs`、`probe1…5.mjs`
- 截图：`real-01-push-confirm-bar.png`、`real-03-alt5-bar.png`、`real-04-chkout-refs.png`、`real-05-chkout-dirty-stash.png`、`real-06-missing-tooltips.png`、`real-07-drawer.png`、`mock-01-commit-dialog.png`、`mock-02-commit-confirm-bar.png`、`mock-03-commit-stageall.png`、`mock-04-push-no-upstream.png`、`mock-05-chkout-refs.png`、`mock-06-confirm-dialog-danger.png`
- 原始 JSON：realtime 断言输出见各脚本 stdout（本轮报告数值均取自该输出，可复跑）

## 附录 C：未覆盖/待裁决

1. 真实五键写链路（≤2s 回写、审计行、CAS/BUSY、NO_UPSTREAM 真机推送）本轮按「不得修改真实仓库」未执行，需 `m2a-ac-report.md` 补齐。
2. `docs/review-m2-req.md` 的 P0-01…05/08/11 与 `docs/review-m2-code.md` 的 P0-1/P1-1/2/3 的闭环状态不在本报告范围（另有并行评审）。
3. AC-UI-10a/10b 的「对话框两步 vs 直出确认条」属产品/AC 口径裁决，本报告给出两侧证据，不代为改需求。
