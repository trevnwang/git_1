# SRS v0.3 新 UI 需求 ↔ 当前实现/运行行为 对抗式评审报告

| 项目 | 内容 |
|---|---|
| 评审对象 | `reqs/requirements.md` v0.3（§9.13–9.17、§11.0–11.7、§13.1.1、§13.4）+ `docs/ui-redesign-spec.md` + `docs/ui-redesign-plan.md` |
| 被核对实现 | `ui/src/components/**`、`ui/src/stores/workbench.ts`、`ui/src/viewmodel.ts`、`ui/src/api.ts`、`src/MbdGitTool.Host/Bridge/*`、`src/MbdGitTool.Host/Services/*`、`tests/MbdGitTool.Host.Tests/*`、`tests/MbdGitTool.Core.Tests/*` |
| 运行核对 | WebView2 CDP（9222）真机读取 DOM/bbox/computed style + `Input.dispatchMouseEvent`/`Input.dispatchKeyEvent` 真实事件；当前构建 `source=local+gitea`（M1 只读阶段，Host `features.repoActions=false`） |
| 截图证据 | `artifacts/ui-review/40-v3-workbench.png`、`41-v3-folder.png`、`50-v3-folder-expand.png`（均为 1424×861 窗内内容区，非 §11.0.3 要求的 1440×900 基线，见 §8 P2 与 §7-D6） |
| 评审方式 | 只读核对；本报告不改代码、不改需求。运行期只做 DOM 读取与鼠标键盘事件，不写仓库 |
| 日期 | 2026-11-14 |

---

## 1. 结论

**一句话：新 UI 的“骨架”基本落地，“需求承诺的行为”大面积未落地；当前不具备 M1 出口条件，更不具备进入 M2 条件。**

| 口径 | 结果 |
|---|---|
| 新 FR（§9.13–9.17 共 32 条）符合度 | **≈47%**（已实现 3 / 部分 26 / 未实现 3） |
| 含旧 v0.3 相关 FR（44 条）符合度 | **≈51%**（已实现 6 / 部分 33 / 未实现 5） |
| AC-UI-01…14 可全自动验收 | **3/14（21%）**；部分可执行 9 条；**完全不可判定 2 条（AC-UI-05、AC-UI-09）** |
| CI 10 锚点 | `data-testid` **10/10 存在**；**语义完全正确仅 6/10**（CI-4、CI-7 落点错误；CI-9、CI-10 只有“标记/文本”，无契约实现与聚合单测）；`ci-slot` 每卡 1 个（实测 4 个）与 spec“各恰好 1 个”冲突 |
| CI 是否参与聚合/卡片底色 | **不参与**（Host/Core 聚合函数无 CI 输入；卡片 tone 取自 `aggregateState`）——但“卡片底色恒中性”本身未实现，所以该结论只在“CI 不额外染色”意义上成立 |
| M1 只读边界 | **未实现**：实测选中 SYNCED 仓后 PULL/FETCH/CHKOUT/COMMIT/PUSH **全部 enabled 可点**，点击后 toast“M1 只读，M2 接入”，违反 D-024 / FR-ACT-02 / FR-ACT-07 / AC-UI-09 |
| 能否进下一阶段 | **否**。M1 出口要求的 AC-UI-01/03/04/05/06/07/09/11/12/13/14 中：AC-UI-05/09 不可判定、AC-UI-11 的抽屉断言未实现、AC-UI-02/06/08/12/13 存在明确失败项。建议先修 §8 的 P0（约 2–3 天），再复评一次 |

**三类问题必须区分处置（否则会“改实现改错方向”）：**

1. **实现没跟上需求**：M1 只读禁用、组件详情抽屉、COMMIT/PUSH 确认条、拖拽分栏持久化、`●/○` 文件标记、文件夹视图四仓 chip、Ctrl/Shift 多选、`详情` 按钮、facet 过滤、确认阈值、焦点陷阱/回落等 → 改实现。
2. **需求自相矛盾或与参考图/spec 冲突，导致实现“两头不是人”**：卡片底色中性 vs spec R-04 tint；F 区“通栏” vs 参考图/spec/实现“中+右两列”；批量条“C 区顶部浮层 ≤36px” vs spec/实现“左栏底部 52px”；`*` 变更后缀（§11.3 禁止 vs spec R-03 允许）；文件夹视图“C+D 合并 82%” vs spec/实现“保留左栏”；CI-4 “第 5 Tab” vs spec/实现“第 4 项”；CI-7 “标题区 B 筛选面板” vs spec/实现“C 区筛选 popover” → **先改 FR/spec 裁决，再改实现**（见 §9 Top10）。
3. **验收本身不可执行**：AC-UI-05/09/10 等缺夹具、缺注入点、缺阶段标识、缺进程计数与 shell 拦截手段 → 改 AC（补夹具/注入/阶段字段），否则永远无法签收。

---

## 2. 评审方法与证据边界

- **静态核对**：逐条读 SRS v0.3 的 32 条新 FR + §11 条款 + AC-UI-01…14 + §13.4 追溯矩阵，对照 UI 组件/store/viewmodel/api 与 Host 路由/服务/测试。
- **运行核对**（CDP，真实事件）：
  - 1440×900：7 区域 bbox、`documentElement.scrollWidth===clientWidth`、状态栏 segments、底部按钮 disabled/title、卡片 computed `background-color`、`data-testid` 计数、抽屉焦点、Tab 焦点轨迹。
  - 点击链：选卡片 → 读 E/F 联动；点 PULL/FETCH/CHKOUT/COMMIT/PUSH；双击卡片；打开 CI 弹窗/设置/通知抽屉/筛选 popover 逐个统计 10 锚点；切文件夹视图并展开组件/仓。
- **证据边界（会影响 AC 判定，必须在验收脚本设计时正视）**：
  1. 无 1,000/4,000 组件夹具，无 `__DSH_TEST_BRIDGE__` 注入入口（该钩子存在于 `ui/src/api.ts:275-305`，但应用启动时无注入时机/无夹具文件），**AC-UI-02/06/08 的大数据与 14 状态矩阵无法注入**；
  2. 无 CI FAIL/`ci.enabled=true` 注入点（UI 从不调用 `getCiStatus`，`ci.enabled` 无开关）→ **AC-UI-12 两个关键断言不可执行**；
  3. 无 shell 调用拦截/测试 launcher 的 E2E 通道（`OpenPathService` 的 `IProcessLauncher` 仅单测注入）→ **AC-UI-03“CDP 拦截 shell 调用并断言参数”不可执行**；
  4. 无阶段标识（无 `data-stage`/UI 未消费 `SnapshotDto.Capabilities`/`HostInfo.Features.repoActions`）→ **AC-UI-09“Given M1/M0/M2 构建”无法区分**；
  5. CDP `deviceScaleFactor` 只模拟 DPI，不改变 CSS 布局；WebView2 浏览器缩放（150%/200%）未通过 Host 暴露 → **AC-UI-01 的 150%/200% 断言当前不可执行**。

---

## 3. FR 逐条核对表

> 图例：✅已实现　🟡部分　❌未实现　⛔无法验收。证据列“文件:行”为静态证据；`运行时` 为本次 CDP 实测。未特别说明时 Host 服务/路由/单测均存在。

### 3.1 §9.13 工作台布局（FR-WS）

| FR | P | 结论 | 实现证据 | 缺口/运行证据 |
|---|---|---|---|---|
| FR-WS-01 7 区域固定顺序 | P0 | 🟡 | `ui/src/App.vue:77-90`、`WorkbenchView.vue:10-33`；`menubar/titlebar/component-list/repo-grid/info-panel/repo-actions/statusbar` 7 个 testid 全命中 | 实测 F 区 `x=309.6,w=1114.4`（1440 视口），**不是“通栏”**：C 列底部被 `BatchBar`（`x=16,w=281.6,h=52`）占据。参考图/spec §6.1/实现均把五键放在“中+右两列”，**是 SRS 文本与参考图冲突**（D-022 图为准 → 应改 FR 而非改实现） |
| FR-WS-02 尺寸/缩放/拖拽持久化 | P0 | 🟡 | bbox 达标：A30/B48/F52/G28，C281.6(240–360)/D849(≥560)/E253.4(220–320)，1440/1280 `scrollWidth=clientWidth` | **无分隔条拖拽、无 `ui.panel.leftWidth/rightWidth` 持久化**（`localStorage` 仅 `ui.v3.view` 一个键）；150%/200% 无验证手段；附录 D 的 `ui.panel.*`/`tree.*` 键实现中不存在 |
| FR-WS-03 视图切换 | P0 | 🟡 | `TitleBar.vue:71-88`、`App.vue:46-50`、`stores/workbench.ts:1251-1261`；两视图共用同 store 快照 | ≤200ms 无计量脚本；`selectComponent` 会把四仓树 `expanded` 清空（`stores/workbench.ts:593-595`），跨视图“保留展开路径”仅对“不换组件”成立 |
| FR-WS-04 二级入口（6/6） | P0 | 🟡 | 菜单栏/标题区入口齐全（`MenuBar.vue:15-85`、`TitleBar.vue:90-134`）；抽屉 overlay 不改变 A–G bbox | **焦点管理缺失**：打开通知抽屉后焦点仍在触发按钮（实测 `activeElement` 仍 `title-notifications`）；抽屉内连按 Tab 12 次全部逃逸到主界面；ESC 后焦点不回落。AC-UI-13 会失败 |
| FR-WS-05 键盘/可访问性 | P1 | 🟡 | `App.vue:26-61`（Esc/Ctrl+K/Ctrl+,/Ctrl+1/2/F5）、`FileTree.vue:132-189`（↑↓←→/Home/End/Enter/Space）、卡片列表 ↑↓ Home/End | **`Ctrl+F` 无实现**（仅 Ctrl+K）；`Ctrl+Shift+F` 无实现（SRS §11.6 要求）；实际 Tab 顺序 A→B→C→BatchBar→D→E→…，与 AC-UI-01 的 “A→C→D→E→F→G” 不符（B 区有大量交互控件） |

### 3.2 §9.14 四仓文件树（FR-TREE）

| FR | P | 结论 | 实现证据 | 缺口/运行证据 |
|---|---|---|---|---|
| FR-TREE-01 目录读取/排序/上限 | P0 | 🟡 | `Core/Files/FileTreeReader.cs`（目录优先+名称序、`.git/.trash` 隐藏、错误码降级）、`Host/Services/RepoTreeService.cs`（懒加载/30s 缓存/路径安全）、`FileTreeReaderTests.cs` 12 用例 | **单次上限 2000 ≠ 需求 ≤500**（`FileTreeReader.cs:20`、`RepoTreeService.cs:25`），无分页（`NextCursor: null`）、无 2,000 子项计时；`tree.hideDotGit` 不可配（硬编码）；扩展名次序仅 UI 端补（`viewmodel.ts:378-385`），Host 端无 |
| FR-TREE-02 2×2 固定/独立展开/LRU | P0 | 🟡 | `RepoGrid.vue` + `WorkbenchView.vue`，实测 DESIGN(左上 309.6,90)/IMPL(右上 740.1,90)/MIL(左下 309.6,449)/SIL(右下 740.1,449)，每格独立滚动；缺仓空态 `RepoTreePanel.vue:131-145` | **无“展开状态按组件 LRU 缓存（≥100 组件）”**：`selectComponent` 直接把 4 格重置为 `emptyTree`，滚动位置也归零；切换组件“先缓存渲染 ≤300ms”未实现 |
| FR-TREE-03 双击打开/白名单/错误码 | P0 | ✅ | `FileTree.vue:83-99,236`、`stores/workbench.ts:839-909`、`Host/Services/OpenPathService.cs`、`OpenPathServiceTests.cs`（白名单/危险扩展名/越界/子模块/junction 覆盖） | 单击设聚焦仓并同步 E/F 已实测（`data-action-scope=IMPL`）；“CDP 拦截 shell 调用”不可执行（见 §2 边界 3） |
| FR-TREE-04 只读/路径安全 | P0 | ✅ | 树无写入口；右键仅只读（`workbench.ts:1044-1055`）；`Core/Security/PathPolicy.cs` 拒绝 `..`/绝对/UNC/保留名/尾随空格/ADS/reparse，`PathPolicyTests.cs` 覆盖 | 与 §13.1.1“固定 CDP 驱动”矛盾：路径穿越只能 Host 单测（建议 AC 改为 CDP+单测双轨） |
| FR-TREE-05 节点状态标记 | P1 | 🟡 | 仓库根 `StateChip`（`RepoTreePanel.vue:112`）、`tree-node` 行内 git 状态字母（`FileTree.vue:257-263`） | **文件行用 `M/A/D/U` 字母，不是需求口径 `●/○`**；仓库根徽标 compact 只显示图标（无中文文案）；STALE 无“数据 X 前”/灰化；“展开不新增 git 进程”无计数断言 |
| FR-TREE-06 右键只读菜单 | P1 | 🟡 | `workbench.ts:1048-1054`：打开资源管理器/复制路径/复制文件名/刷新 | “在资源管理器中打开需二次确认路径”未实现（`tree.reveal` 直接 `confirm=true` 调 `openPath`） |

### 3.3 §9.15 文件夹视图（FR-FOLDER）

| FR | P | 结论 | 实现证据 | 缺口/运行证据 |
|---|---|---|---|---|
| FR-FOLDER-01 结构/未克隆/未纳管 | P0 | 🟡 | `Host/Services/WorkspaceTreeService.cs`（根层 COMPONENT→REPO 虚拟分组 + UNBOUND_GROUP 分组）、`WorkspaceTree.vue`、实测展开：COMPONENT→4×REPO | 未克隆组件**未淡显、未禁展开**（`.ws-row.missing` 只对 `state==='MISSING'`，组件态是 BINDING_ERROR/MISSING 不一）；当前夹具无“未纳管目录”，`hasUnboundGroup=false`，该分支**无法验收** |
| FR-FOLDER-02 交互/性能/同步 | P0 | 🟡 | 懒加载 + `useVirtualRows` + 双击（`WorkspaceTree.vue:115-131,269`）+ 单击同步 `selection`（`workbench.ts:1144-1183`） | 400 组件/1,600 仓 ≥58fps 无夹具无测量；切回工作台只恢复组件选中，不定位/滚动到节点 |
| FR-FOLDER-03 状态标记/同快照 | P0 | 🟡 | 仓节点 `StateChip` + 分支/ahead/behind（`WorkspaceTree.vue:285-291`）；两视图同 `state.components` | **组件节点“四仓 chip”不渲染**：`WorkspaceTree.vue:292-294` 的 `comp-meta` 被前面 `v-else-if`（COMPONENT 有 state 即命中 285 行）挡住，实测组件行只有 1 个状态 chip，无 4 个 mini-dot；STALE 数据时间/斜纹缺 |
| FR-FOLDER-04 过滤/搜索/隔离 | P1 | 🟡 | 树头本地文本过滤（`WorkspaceTree.vue:13,29-75,199-205`） | 无按聚合状态/RepoType/脏/未克隆 facet；无 120ms 防抖；**TitleBar 搜索不影响树**；筛选状态是组件内 `ref`，切视图即丢（无独立持久化） |
| FR-FOLDER-05 与工作台切换 | P0 | 🟡 | `TitleBar.vue:80-87`、`App.vue:46-50`、`workbench.ts:1251-1261`；旧表格入口不存在 | **文件夹视图仍保留左栏组件卡片列表**（实测 C 列 281.6px 仍在），与 SRS §11.5“主区 C+D 合并为工作区树（约 82%）”不符（与 spec §7.2/参考图一致，属需求冲突）；≤200ms 未测 |

### 3.4 §9.16 底部单仓按钮条（FR-ACT）

| FR | P | 结论 | 实现证据 | 缺口/运行证据 |
|---|---|---|---|---|
| FR-ACT-01 5 键文案/顺序/禁用提示 | P0 | ✅ | `RepoActionsBar.vue:42-58` + `viewmodel.ts:552-569`；实测 5 键 `PULL→FETCH→CHKOUT→COMMIT→PUSH`，未选中聚焦仓时全部禁用且 `title="请先在文件树中选择仓库"`；1280 下 5 键同排不隐藏 | — |
| FR-ACT-02 可用性严格取 §7.6 | P0 | ❌ | `viewmodel.ts:469-550` 自写第二套矩阵；Host `RepoActionService.cs:19-36` 另有 spec §6.2 矩阵 | 实测 SYNCED 仓 **5 键全 enabled**（M1 应全禁用）；UNKNOWN-FETCH 在 §7.6/Host 为 ✅，UI 矩阵为 ❌；SYNCED-CHKOUT 在 §7.6/Host 为 ✅ 无需确认，UI 标“需二次确认”；**切 Tag 的 CHKOUT 二级列不存在**。同一矩阵三处口径（SRS/Host/UI），FR-ACT-02 明令“禁止另写一套判断” |
| FR-ACT-03 前置（read-through/CAS/选择器/staged/offline） | P0 | 🟡 | Host `RepoActionService.Run` 只做状态校验后返回 `E_NOT_IMPLEMENTED`（M2 预留） | 无 read-through 重扫/CAS；点 CHKOUT **无分支/Tag 选择器**（实测无 modal/dialog）；`RepoVm` 无 staged 字段 → “无 staged 时 COMMIT 禁用”**永远无法判定**；`state.online` 恒 true，离线不联动 |
| FR-ACT-04 确认条/未确认不写 | P0 | ❌ | 无确认条组件；`stores/workbench.ts:911-975` 点击即请求 | 实测 COMMIT/PUSH 点击直接发 `runRepoAction`，无 `confirm-dialog`；FR-ACT-04 的“确认条 ≤48px、不移动 5 键”完全未实现 |
| FR-ACT-05 进行中/失败/取消 | P0 | 🟡 | pending 时按钮 spinner + 同仓其余禁用（`RepoActionsBar.vue:47-57`、`viewmodel.ts:586-589`）；状态栏显示失败错误码（实测 `✕ PULL 失败 · IMPL · E_NOT_IMPLEMENTED · 02:58:47`） | `BUSY`（index.lock）/`CONCURRENT_CHANGE` 无专门处理；无合作式取消；无“取消后以实际扫描为准” |
| FR-ACT-06 完成后回写/状态栏模板/审计 | P1 | 🟡 | M1 无真实操作，回写不适用；`StatusBar.vue:32-35` | 状态栏左段**不是需求模板**：空闲为“就绪 · 无最近操作”（§11.0.4 要求“最近一次操作：…”、未操作为“无”）；失败文案缺“最近一次操作：”前缀；无审计、无任务中心“仅重试失败项” |
| FR-ACT-07 版本门禁 | P0 | ❌ | 无阶段标识；Host 已给出 `features.repoActions=false` 与 `RepoActionAvailabilityDto.Enabled=false`，**UI 完全未消费** | 实测 M1 按钮可点 + toast“M1 只读，M2 接入”，属于“可点但无效果（写操作数=0）”，正是 FR-ACT-07/D-024 禁止的形态；M0 占位文案也不存在 |

### 3.5 §9.17 组件卡片/信息面板（FR-CARD / FR-INFO）

| FR | P | 结论 | 实现证据 | 缺口/运行证据 |
|---|---|---|---|---|
| FR-CARD-01 卡片字段/中性底色 | P0 | 🟡 | `ComponentCard.vue:61-121`（ID/徽标/四仓 chip/分支/MIXED/变更徽标/CI 槽 18px）；实测卡高 76、宽随栏（≥220）、CI 槽 `ci-slot` 存在 | **底色是状态 tint**：computed bg = `color(srgb 0.9729 0.9605 0.9391)`（BEHIND）、`0.9957 0.9590 0.9590`（BINDING_ERROR）、`0.9482 0.9784 0.9590`（SYNCED），违反 FR-CARD-01/§11.2/AC-UI-02 的“底色中性”，但符合 spec 裁决 R-04（spec §3.6）；CI 槽在 L1 而 §11.3 规定 L3；无“最近活动/新鲜度”；卡内用 `*` 表变更（§11.3 明文禁止） |
| FR-CARD-02 选中/联动/双击抽屉/多选 | P0 | 🟡 | `ComponentList.vue:195-207`、`workbench.ts:581-602`；实测单击联动四仓树 + E 区；`aria-selected` 有 | **双击卡片不打开详情抽屉**（实测 drawer/modal=0，`ComponentCard.vue:72` 的 `@dblclick="onClick"` 只重复选中）；**Ctrl/Shift 多选未实现**（仅 checkbox 选择模式）；单选模型 `{componentId,focusedRepoType}` 已是唯一源 ✅ |
| FR-CARD-03 规模/虚拟滚动 | P0 | 🟡 | `useVirtualRows` 已用于卡片/四仓树/工作区树；卡片区独立滚动 | 无 1,000/4,000 夹具、无 fps 测量、无 DOM ≤120 断言（虚拟化实现存在但“未验收”） |
| FR-CARD-04 批量入口（3 处/浮层/阈值） | P0 | 🟡 | 菜单「操作」`MenuBar.vue:58-62`、卡片右键 `workbench.ts:1023-1042`、批量条 `BatchBar.vue`；实测批量条出现时 C/D/E bbox 不变 | **批量条是 C 列底部 52px 常驻布局行**，不是“C 区顶部浮层 ≤36px”（`BatchBar.vue:39-50`、`WorkbenchView.vue:30`）；未选中时“操作”菜单未禁用；**二次确认阈值只对 DELETE 生效**，且仓数=组件数×4 估算（`workbench.ts:1001-1019`）；另有卡片 hover SYNC/CREATE 第 4 入口（SRS §11.6 只列 3 处） |
| FR-CARD-05 卡片区七态 + 刷新 | P1 | 🟡 | 空态/刷新不清空（`workbench.ts:532-561` 保留旧快照）；树有骨架 | 卡片区无独立“错误可重试/离线降级/过期/部分失败/权限过期”渲染；无“七态注入”入口 |
| FR-INFO-01 E 区字段/快捷按钮 | P0 | 🟡 | `InfoPanel.vue:71-260`：仓库名/RepoType/状态+calcAt/分支/HEAD/compareRef/ab/dirty/最近提交/CI 占位；字段列 64px（≤72）；实测 1280 无横向溢出 | **缺“详情”快捷按钮**（只有 ↗ 在资源管理器打开）；CI 汇总条有计数但缺“最近失败仓”（空态可接受） |
| FR-INFO-02 空态/同聚焦仓/STALE | P0 | 🟡 | 未选中空态文案含“从中间文件树中选择仓库”（`InfoPanel.vue:140-148`）；E/F 同读 `state.focusedRepoType`（实测 scope 一致） | STALE 仅显示“⏱ 数据过期”，无“数据 X 前” |
| FR-INFO-03 CI 通道 B/6 态 Mock | P1 | 🟡 | 固定“CI 未接入”空态（`InfoPanel.vue:200-208,248-258`）；CI 不参与聚合/底色 ✅ | `ci.enabled=true` 的 6 态 Mock **无实现**（UI 不读 flag、不调 `getCiStatus`） |
| FR-INFO-04 信息新鲜度 | P1 | 🟡 | 面板随快照/切换仓刷新；仅局部字段更新 | 无“操作后 ≤2s / 外部变更 ≤10s”定向刷新（只有全量 refresh）；过期字段未灰化、无“最近成功时间”展示 |

### 3.6 旧编号但被 v0.3 改写的相关 FR

| FR | P | 结论 | 实现证据 | 缺口/运行证据 |
|---|---|---|---|---|
| FR-BOARD-01 默认卡片列表/排序 | P0 | ✅ | 唯一默认列表为卡片；无表格入口；默认排序 severity→ID（实测顺序 6→12→12→14） | — |
| FR-BOARD-02 搜索/筛选/排序 | P0 | 🟡 | `TitleBar.vue:56-69`（IME composition/`Ctrl+K`）、`viewmodel.ts:625-674`（搜 ID/仓名/分支/Tag/短 SHA、排序三档） | facet 缺“分支/Tag”；过滤面板在 C 区而非 §11.0.2 B 区（与 CI-7 冲突，见 §6）；结果不影响文件树 ✅ |
| FR-BOARD-03 七态（每区独立） | P0 | 🟡 | 部分空态/加载/错误 | 七态未齐（同 FR-CARD-05） |
| FR-BOARD-04 组件级批量（阈值/全选） | P1 | 🟡 | 动作清单/全选/入口存在 | 超阈值二次确认仅 DELETE；跨仓/任务中心逐仓结果无 |
| FR-DETAIL-01 组件详情抽屉 | P0 | ❌ | Host `getComponentDetail` 已实现（`ComponentDetailService.cs`、`WorkbenchDto.cs:4-53`、含每仓 action availability），**UI 无 API 封装、无组件、无入口** | 双击卡片不打开抽屉；`api.ts` 无 `getComponentDetail`；`types/workbench.ts:21-22` 的 `DrawerKind` 只有 tasks/notifications |
| FR-DETAIL-02 单仓详情 Tab 顺序（CI 第 5） | P0 | 🟡 | 用 E 区 4 Tab 代替抽屉（`InfoPanel.vue:48-53`）：概览/提交/差异/CI | 缺“文件”Tab；CI 为第 4 项（与 FR-DETAIL-02/§11.4 CI-4 的“第 5 位”不符，但符合旧 AC-DETAIL-03，见 §7）；提交分页 100/页 ✅ |
| FR-OPS-05 状态栏两段 | P1 | 🟡 | `StatusBar.vue:31-66`；实测 7 段 `last-op,freshness,health,queue,ci,notify,network`；进程健康 2 灯 ✅ | 左段文案非模板；失败“展开最近 5 条”未实现 |
| FR-CI-01 10 锚点 | P0 | 🟡 | 10 个 testid 均存在（见 §5） | CI-4/CI-7 落点错误；CI-9/CI-10 仅标记/文本；UI 未接 `getCiStatus` |
| FR-CI-02 契约 + `CI_UPDATED` + flag | P0 | 🟡 | `api.ts:39-63,204-210` 有 CiLatest/CiAggregate DTO；Host `CiDto.cs`；`CiStatusService` enabled=false | 无 `ICiProvider`/`CI_UPDATED` 实现、无 flag 开关、UI 从不调用 CI 接口；`ci.*` 配置键无存储 |
| FR-CI-03 CI 不参与聚合/底色 | P0 | ✅ | Core 聚合函数无 CI 输入（`ComponentAggregation`/`ComponentAssembler` 无 CI 引用）；卡片 tone 取 `component.state` | “注入 CI FAIL 后底色不变”不可执行（无注入点）；且卡片底色本身非中性 |
| FR-CI-04 统一空态/无裸 TODO | P1 | ✅ | 统一“CI 未接入”空态；`ui/src` grep 无 TODO/FIXME | — |
| FR-CI-05 `ci_status` 表 + `ci.*` 键 | P0 | ❌ | M1 无 DB 层；Core/Host 无 `ci_status`/`ci_latest` | v1 建表要求未落地 |

**FR 统计**：44 条中 ✅6 / 🟡33 / ❌5；新 32 条中 ✅3（TREE-03、TREE-04、ACT-01）/ 🟡26 / ❌3（ACT-02、ACT-04、ACT-07）。

---

## 4. AC-UI-01…14 可执行性

> 判定口径：**可自动** = 现有资产（真实 Host + CDP + 现有重放脚本）即可稳定断言；**部分** = 断言中至少一项缺夹具/注入点/计量手段或断言口径自相矛盾；**不可判定** = 无实现或无机制，当前构建上无法给出通过/不通过。

| AC | 判定 | 阻断原因（证据） | 建议改写（可执行口径） |
|---|---|---|---|
| AC-UI-01 布局 | ⚠ 部分 | 7 区域/顺序/D 2×2/区域尺寸/`scrollWidth` 均可测；但：①“Tab 顺序 A→C→D→E→F→G”与 B 区（search/view/CI/refresh/notify/tasks/settings）实际可聚焦控件冲突（实测 Tab 先经过 B 区）；②150%/200% 缩放无注入手段（CDP dsf 不改变 CSS 布局）；③“截图与参考图逐区域对比通过”无阈值基线 | 拆为 AC-UI-01a（7 区域 bbox/顺序/D 2×2/尺寸区间/无页面横滚）、01b（Tab 顺序 A→B→C→(D 内可聚焦容器)→E→F，并跳过静态状态栏）、01c（150%/200% 用 WebView2 `ZoomFactor` 由 Host 暴露测试开关，或明确“由 M0 手工截图验收”）。删除/阈值化“与参考图对比” |
| AC-UI-02 卡片列表 | ⚠ 部分 | ①“1,000 组件快照”无夹具/注入入口；②“卡片底色中性”**当前实测失败**（tint）；③“七态注入”无机制；④“滚动 10s ≥58fps”无 trace 脚本；⑤批次条 bbox 可测（实测 C/D/E 不变） | 先裁决底色（§9-#1）。夹具：提供 `fixtures/components-1000.json` + `window.__DSH_TEST_BRIDGE__` 或 `?fixture=` 注入；七态用 query 参数 `?state=offline|error|stale...` 注入；fps 给出 `Tracing.start` + 10s 采帧脚本。 |
| AC-UI-03 四仓树 | ⚠ 部分 | 2×2 顺序/懒加载/排序/根展开/单击同步/缺仓空态可测；**“CDP 拦截 shell 调用并断言参数为 canonical 路径”不可行**（Win32 ShellExecute，CDP 无法拦截） | 改为：①Host 侧断言（已有 `HostLog.Info("openPath mode=… class=… path=…")`，可用日志契约断言）；或②注入 `__DSH_TEST_BRIDGE__` 断言请求 `{componentId,repoType,path,mode}` 与 canonical 相对路径；或③开放 `openPath` 测试 hook（`--dry-run`） |
| AC-UI-04 只读/路径安全 | ✅ 可自动 | DOM 无写入口/右键只读可 CDP 断言；路径穿越由 `PathPolicyTests`/`OpenPathServiceTests` 覆盖（建议明确为“CDP + Host 单测双轨”，不要硬绑“纯 CDP”） | 补一句“路径安全以 Host 单测 + 1 条 CDP 端到端（请求 `../` 返回 `E_PATH_OUTSIDE_ROOT`）为准” |
| AC-UI-05 文件树状态标记 | ⛔ 不可判定 | ①无附录 C 14 状态夹具注入手段（真实工作区只覆盖 5 态左右，实测 `treeStatusMarks=[]` 无脏文件）；②`●/○` 与实现字母 `M/A/D/U` 不一致（先裁决）；③“进程计数断言”无工具（无 Host 侧 git 进程计数 API/日志字段） | 补：夹具注入 + 明确标记字符（改实现为 `●/○` 或改 AC 为 “M/A/D/U 字母 + 颜色 + title”）；进程计数改为读 Host 日志中 `git` 启动计数或 `GetProcessesByName("git")` 采样（给出脚本） |
| AC-UI-06 文件夹视图 | ⚠ 部分 | 结构/两视图一致性/双击可测；缺“1 未克隆组件 + 1 未纳管目录”夹具（实测 `hasUnboundGroup=false`）；400/1600 fps 无数据；过滤缺 facet 实现 | 增加 fixtures/workspace-unbound；把性能档单列为 AC-UI-06b（400/1600 合成数据 + 帧率脚本）；过滤按 FR-FOLDER-04 逐 facet 断言 |
| AC-UI-07 视图切换 | ✅ 可自动 | selection/展开/两视图一致可断言；≤200ms 可写 Performance 计时 | 补计时方法：`performance.now()` 包住 `setView` + 两帧 `requestAnimationFrame`；展开路径断言改为“切换前展开的组件+仓路径集合在切换后仍在” |
| AC-UI-08 按钮条 | ⚠ 部分 | 未选中禁用/提示/顺序/1280 不换行可测（实测通过）；**14 主状态 × 5 按钮矩阵无夹具**，且 UI 矩阵与 §7.6/Host 已不一致（UNKNOWN/FETCH、SYNCED/CHKOUT） | 夹具：14 个状态组件（附录 C 已定义）+ 注入；断言对象改为 Host 下发的 availability（单一来源）；补“切 Tag 走 CHKOUT 二级选择”用例 |
| AC-UI-09 M1 只读边界 | ⛔ 不可判定 | 无阶段标识：单构建无法区分 M0/M1/M2；且**当前实测 M1 按钮可点**（若承认当前就是 M1，则**直接失败**） | 补 `SnapshotDto.Capabilities.repoActions` 或 `data-stage="M0|M1|M2"`；AC 改为“当 capabilities.repoActions=false 时 5 键全 disabled 且 title 含‘M1 只读’”；M0 用同一字段 false + 占位文案断言 |
| AC-UI-10 前置/确认/失败 | ⚠ 部分 | 状态栏错误码可测；但：①“无 staged 的 DIRTY 仓”无 staged 字段（DTO 缺）→ 不可判定；②BUSY 注入无手段；③≤2s 回写+审计未实现（M2）；④取消语义无实现 | 契约追加 `stagedCount`/`unmerged`；注入 `index.lock` 夹具 + 断言 `E_BUSY`且锁仍在；回写/审计以 M2 实现后补；取消语义逐键给出可观测状态（`CANCELLED`） |
| AC-UI-11 右侧信息面板 | ⚠ 部分 | 字段/空态/同聚焦仓/1280 不溢出可测；**“详情抽屉开/关后 A–G bbox 不变”抽屉不存在** → 不可判定；200% 无手段 | 拆：AC-UI-11a（面板字段/空态/一致性/1280 无溢出）；11b（抽屉存在 + 打开前后 bbox；与 FR-CARD-02/FR-DETAIL-01 同批实现后启用） |
| AC-UI-12 CI 10 锚点 | ⚠ 部分 | 10 个 testid“存在”需按 SRS 未写的顺序打开 4 个浮层（实测：筛选/CI/设置/通知逐个打开才齐）；**`ci-slot` 实测 4 个**（每卡 1 个，与 spec“各恰好 1 个”冲突）；“ci.enabled=true/注入 CI FAIL”无实现无注入 | 改写为：“依次打开 筛选 popover→CI 弹窗→设置→通知抽屉，10 个 testid 各≥1 且 `ci-nav/ci-strip/ci-tab/ci-queue/ci-facet/ci-settings/ci-contract/ci-notif-category/ci-card-base` 唯一；`ci-slot` 每张可见卡 1 个”；另加前置“Host 暴露 `ci.enabled` 测试开关 + Mock FAIL 注入”后才有颜色断言 |
| AC-UI-13 二级入口/抽屉 | ✅ 可自动（但**当前会失败**） | 6/6 入口可达 ✅；**焦点陷阱/焦点回落实测失败**（抽屉内 Tab 12 次全逃逸；ESC 后焦点不回落）；“无左导航/无第三条工具栏”✅ | 断言“抽屉打开后 activeElement 在 drawer 内；Tab 循环不出 drawer；ESC 后 activeElement 为触发控件” → 需先实现后再验收 |
| AC-UI-14 旧布局废弃 | ✅ 可自动（需澄清 1 点） | 无左导航/无表格/无 340px 详情列/§11.7 逐项可测；**“默认入口为工作台”与视图持久化冲突**：`localStorage["ui.v3.view"]` 存在时重载会进文件夹视图（实测键存在） | 改为“清除 `ui.v3.view` 后首次进入为工作台；有持久化值时恢复上次视图（与 spec AC-UI-34 一致）”；§11.7 中“状态栏左段文案模板存在”当前失败，应挂到 FR-OPS-05/AC-UI-01 |

**AC 小结**：全自动 3 条（04/07/13）、部分 9 条（01/02/03/06/08/10/11/12/14）、完全不可判定 2 条（05/09）。**AC-UI-09 与 AC-UI-05 属于“结构性缺陷”，不是补脚本能解决的**——必须补阶段标识与夹具/注入。

---

## 5. CI 10 锚点核对（`data-testid` + 语义）

| # | testid | 存在 | 实际落点（代码/运行时） | 语义是否符合 §11.4 | 结论 |
|---|---|---|---|---|---|
| CI-1 | `ci-nav` | ✅ | `TitleBar.vue:90-97` 标题区 CI 按钮 → `openModal('ci')` 打开 960×640 弹窗（骨架/空态/“实验”/面包屑/契约表） | ✅ 符合（§11.4 允许“菜单栏或标题区入口”；注：菜单「视图」内没有“CI 流水线”，在「工具」里，见 §7-D7） | 语义正确 |
| CI-2 | `ci-slot` | ✅ | `CiBadge.vue:19-29` + `ComponentCard.vue:89`，18×18 定高、虚线空态、不换行 | ✅ 符合 CI-2；但**每张卡片 1 个**（实测 4 个），与 spec §8.1/§10.1“各恰好 1 个”自相矛盾 | 语义正确（spec 口径需改） |
| CI-3 | `ci-strip` | ✅ | `InfoPanel.vue:72-85`，40px 顶部汇总条：PASS/FAIL/RUNNING/QUEUED 计数 + “未接入” | ✅ 基本符合（缺“最近失败仓”字段展示，v1 空态可接受） | 语义正确（可补最近失败仓占位） |
| CI-4 | `ci-tab` | ✅ | `InfoPanel.vue:48-53,116`：E 区 4 Tab 的**第 4 项**「CI」；无“文件”Tab | ❌ 不符：SRS §11.4 CI-4 = **单仓详情抽屉第 5 个 Tab**（概览/历史/差异/文件/CI），当前既无抽屉、又缺文件 Tab、序号为 4。该落点符合 spec §8.1/旧 AC-DETAIL-03 | **语义错误**（需求/spec 先裁决：改 FR 承认 E 区 4 Tab，或改实现补抽屉 5 Tab） |
| CI-5 | `ci-queue` | ✅ | `StatusBar.vue:44-52`，状态栏第 5 段（last-op 之后第 5 段），显示 0 | ✅ 符合 CI-5 | 语义正确 |
| CI-6 | `ci-notif-category` | ✅ | `DrawerHost.vue:64-74`，通知抽屉独立“CI 事件”分组 + 类型枚举 + 空分组 | ✅ 符合 CI-6 | 语义正确 |
| CI-7 | `ci-facet` | ✅ | `ComponentList.vue:150-160`：**C 区卡片列表头部筛选 popover** 内的禁用“CI 状态”select | ❌ 不符 SRS §11.4 CI-7 = **标题区 B「筛选」弹出面板**；B 区当前没有筛选按钮（FR-BOARD-02/§11.0.2 B 要求）。该落点符合 spec §8.1“左栏筛选 popover” | **语义/落点错误**（同 CI-4，需先裁决） |
| CI-8 | `ci-settings` | ✅ | `ModalHost.vue` 设置弹窗**第 5 分区**“CI 集成”：CI 类型/Base URL/Token/轮询/启用开关全部 disabled + “实验”徽标 | ✅ 符合 CI-8 | 语义正确 |
| CI-9 | `ci-contract` | ✅ | `ModalHost.vue:136-140` CI 弹窗内 `<pre>` 契约文本（CiLatest/ComponentCiAggregate/ICiProvider/CI_UPDATED） | 🟡 部分：CI-9 要求 `ci_status`/`ci_latest` 视图 + `ICiProvider` + `CI_UPDATED` **契约落物**；实际只有文本 + `api.ts` 两个 TS 接口，无 provider 实现/事件/表（FR-CI-05 未实现） | 部分（契约“露脸”但无实现） |
| CI-10 | `ci-card-base` | ✅ | `ComponentList.vue:167-182` 卡片列表容器 `data-ci-affects-base="false"` | 🟡 部分：CI 确实不进聚合/底色（Core 无 CI；卡片 tone 取 aggregateState），但 CI-10 要求“契约文档 + **单测**”；`tests/MbdGitTool.Core.Tests` 无 CI 相关单测，且“注入 CI FAIL 后底色不变”无注入点 | 部分（属性存在，证据链不足） |

**CI 结论**：`data-testid` 10/10 存在，但严格语义 **6/10 正确**（CI-1/2/3/5/6/8）、2 个部分（CI-9/CI-10）、2 个错误（CI-4/CI-7）；`ci-slot` 的“唯一性”与 spec 自相矛盾。**CI 未参与聚合/卡片底色成立**（聚合函数无 CI 输入、卡片 tone 取自聚合态），但该“不参与”目前只能静态证明，不能按 AC-UI-12 动态注入证明。

---

## 6. 专项结论（任务要求第 3 条）

### 6.1 文件夹视图
- 结构：`WorkspaceTree` 根→COMPONENT→REPO→文件可展开（实测：4 COMPONENT，展开后 4 REPO），Host `WorkspaceTreeService` 提供 UNBOUND_GROUP 与 MISSING 仓节点 → **主链路可用**。
- 与需求偏差：①左栏卡片列表仍保留（SRS §11.5 要求 C+D 合并 ≈82%）；②未克隆组件未淡显/未禁展开；③组件节点四仓 chip 不渲染（模板分支不可达）；④无状态/RepoType/脏/未克隆 facet，搜索只过滤树内名称文本且不影响 titlebar 搜索；⑤无 400/1600 性能验收。

### 6.2 四仓文件树
- 2×2 固定顺序、独立滚动、缺仓空态、懒加载、排序、双击打开/右键只读、路径安全（Host 单测）**均成立**。
- 不达标：单次 2000 条（应 ≤500 且分页）、无 LRU 展开缓存、`●/○` 标记未按口径、STALE 时间/灰化缺失、`tree.hideDotGit` 不可配。

### 6.3 底部单仓按钮 / M1-M2 边界
- 5 键存在、顺序/文案/禁用提示对；**M1 只读边界完全未实现**：Host 已返回 `Enabled=false`（`RepoActionService.cs:72`）与 `features.repoActions=false`，但 UI 用 `viewmodel.ts` 自算矩阵并允许点击，点击后经 Host `E_NOT_IMPLEMENTED` 才转 toast → 典型“可点但无效果”，违反 D-024/FR-ACT-02/07/AC-UI-09。
- **M2 写操作 UI（确认条/选择器/进行中/取消/回写/审计）整体缺失**，与 FR-ACT-03/04/05/06 有 6 项硬缺口；其中“COMMIT 无 staged 禁用”受数据模型限制（DTO 无 staged 字段），即使到 M2 也需先改契约。

### 6.4 右侧信息面板
- 字段与三段结构基本到位（概览/提交/差异/CI 4 Tab），标签列 64px（≤72）、1280 无横向溢出；空态/同聚焦仓一致。
- 缺“详情”快捷按钮（只有资源管理器按钮）、STALE“数据 X 前”、CI 6 态 Mock、≤2s/10s 定向刷新；详情抽屉不存在导致 FR-DETAIL-01/AC-UI-11 抽屉断言悬空。

### 6.5 搜索/排序/筛选
- 搜索：组件 ID/仓库名/分支/Tag/HEAD 短 SHA ✅，IME `compositionend` ✅，`Ctrl+K` ✅，结果不影响文件树 ✅；**`Ctrl+F`（SRS 指定的搜索快捷键）缺失**。
- 排序：严重度/ID/最近活动三档 ✅，默认 severity→ID ✅。
- 筛选：只有 聚合状态/RepoType/脏/未推送 4 组，缺 **分支/Tag** 两组（FR-BOARD-02）；CI facet 位于 C 区（见 CI-7）；文件夹视图无 facet 过滤、无独立持久化筛选。

### 6.6 批量入口
- 3 个入口可达 ✅；菜单「操作」未选中时未禁用；“选中 ≥1 时出现”的浮层批量条不存在（常驻底部 52px）；>10 组件/>40 仓二次确认只对 DELETE、且仓数为估算（×4）；卡片 hover SYNC/CREATE 是 SRS 未列的第四入口。

---

## 7. 文档一致性

> 结论：**v0.3 修订记录本身基本自洽，但“旧 v0.2 AC 仍具权威”造成双源冲突，spec 与 SRS 同为“权威”且互相打架，追溯矩阵有错映射与覆盖缺口。**

| # | 级别 | 问题 | 证据 | 影响 |
|---|---|---|---|---|
| D1 | **P0** | 旧 v0.2 AC 仍被 §13 声明为唯一权威，且与新 UI 直接冲突：`AC-BOARD-01` 要求**表格默认视图**、`AC-BOARD-02` 要求卡片**宽≥320/高≥104/底色中性**、`AC-DETAIL-03` 要求 Tab **CI 第 4 位**、`AC-CI-01` 要求 **CI-1 左侧导航入口** | `reqs/v0.2/ac-catalog.md:579,592,730,1375`；§13 开头“AC 目录唯一权威…冲突时以 AC 目录为准”；§13.4 只口头说“表格相关断言由 AC-UI-02 取代” | 验收时按 ac-catalog 会判当前实现失败（无表格/无左导航/卡片 76px），按 §13.1.1 则判通过——**双源冲突，冻结门禁无法签收** |
| D2 | **P0** | 两套 `AC-UI-*` 编号同号异物：spec §10.2 定义 **AC-UI-01…34**（如 AC-UI-21=CI 锚点、AC-UI-22=CI FAIL 底色），SRS §13.1.1 定义 **AC-UI-01…14**（AC-UI-12=CI 锚点、AC-UI-04=路径安全…）；spec §11.2 R-07/§11.3 还要求 SRS 补 `AC-FS-01..04` | `docs/ui-redesign-spec.md:883-920,973`；`reqs/requirements.md:1035-1054` | 同一个“AC-UI-12”指两张不同的验收用例，M0/M1 出口引用会歧义 |
| D3 | **P0** | spec 明确保留与 SRS v0.3 冲突的裁决：R-04“卡片允许 55% 状态色 tint”、R-03“`*` 后缀与 `●N` 并存”、R-07“SRS 无文件夹视图需补 FR-FS”、§3.5“批量入口 4 处”、§8.1 “CI-4 第 4 项/CI-7 左栏 popover”；SRS v0.3 对应条款为“卡片底色中性/§11.3 禁止 `*`/FR-FOLDER-05 已有/§11.6 三入口/§11.4 CI-4 第 5 位、CI-7 标题区 B” | spec §11.2 R-03/R-04、§3.5、§8.1、§11.3；SRS §11.2/§11.3/§11.4/§11.6 | 实现按 spec 做 → 与 SRS 逐条冲突；本报告 §3 的 CARD-01/CARD-04/CI-4/CI-7/FOLDER-05 均源于此 |
| D4 | **P1** | SRS 内部自相矛盾：①FR-WS-01/§11.0.2 说 F 区“通栏”，但参考图（D-022 图为准）与 spec §6.1/实现均为“中+右两列”；②§11.3/FR-CARD-04 说批量条为“C 区顶部浮层 ≤36px”，spec §3.5/实现为“左栏底部 52px 常驻”；③FR-DETAIL-02 与 §11.4 CI-4 说 CI 第 5 Tab、§11.0.1 #10 旧表说第 4 位 | SRS:574,727,834,1041,1044；spec:525-527,300-307 | 实现无论怎么做都违反其中一处；AC-UI-01/02 的 bbox 断言也随之歧义 |
| D5 | **P1** | §13.4 追溯矩阵错映射/漏映射：`FR-WS → AC-UI-01/05/07/13`（AC-UI-05 属文件树）、`FR-CARD → AC-UI-01/02/07`（双击抽屉/多选/批量阈值无 AC）、`FR-INFO → AC-UI-03/11/12`（AC-UI-03 属文件树）；`FR-WS-05` 键盘/可访问性、`FR-CARD-04` 阈值、`FR-FOLDER-04` 筛选隔离、`FR-INFO-04` 时延均无 AC | SRS:1092-1097、§3.3 M0 出口“AC 追溯矩阵 100% 覆盖 FR” | 矩阵声称 100% 覆盖实际不成立；“无 AC 的 FR 不得进 M2”门禁失效 |
| D6 | **P1** | 验收口径与实现/工具能力矛盾：§13.1.1 固定“CDP 驱动 + 真实事件 + 截图归档 `docs/images/ui-v3/`”；实际上：`docs/images/ui-v3/` **不存在**（截图在 `artifacts/ui-review/` 且为 1424×861）；shell 调用拦截、150%/200% 缩放、CI 注入均无 CDP 手段 | §13.1.1 首段；`Test-Path docs/images/ui-v3` = False；截图尺寸 1424×861 | AC 无法按写定方法执行 |
| D7 | P2 | 菜单栏内容与 §11.0.2 A 不符：A 要求“文件（打开工作区/首扫/退出）、查看（工作台/文件夹/刷新/CI 流水线）、操作、工具（任务中心/日志与诊断/诊断导出）、设置、帮助”；实现为 文件（打开工作区/重新扫描/认证/退出）、编辑、视图（无 CI）、操作、工具（CI 在工具、设置也在工具）、帮助；无顶级“设置”菜单、无“诊断导出” | `MenuBar.vue:15-85` vs SRS:722 | 菜单契约偏差；CI-1 可在标题区兜底，但“查看→CI 流水线”不存在 |
| D8 | P2 | 附录 D 设置键（`ui.panel.leftWidth/rightWidth`、`tree.hideDotGit/pageSize/defaultExpandDepth`、`ci.enabled`…）与实现零对应；`tree.pageSize` 100–500 与 Host `DefaultLimit=2000` 冲突 | SRS:1253-1255；`FileTreeReader.cs:20`/`RepoTreeService.cs:25`；`localStorage` 仅 `ui.v3.view` | 设置类 FR/AC 无法验收 |
| D9 | P2 | §13 顶部仍写“权威 AC 目录 162 AC / 72 FR”，而 v0.3 实际 72+32=104 FR 且 AC-UI 未回写；“回写完成前 M0/M1 出口引用 AC-UI 须注记 §13.1.1”未被执行（§3.3 已直接引用） | SRS:998-1000,1100,135-137 | 冻结文档版本不自洽 |

---

## 8. 缺口与优先级

### P0（阻断 M1 出口/合同违约，必须先修）
1. **M1 只读边界未实现**：按钮全 enabled 可点（实测），Host 已提供 `Enabled=false`/`features.repoActions=false` 未被消费（FR-ACT-02/07、AC-UI-09、D-024）。
2. **卡片底色非中性**：状态 tint 实测（FR-CARD-01/§11.2/§11.3/AC-UI-02 vs spec R-04）——先裁决需求，再改实现/AC。
3. **组件详情抽屉缺失**：双击不打开、无 API 封装、无组件（FR-CARD-02/FR-DETAIL-01/AC-UI-11 抽屉断言）；Host `getComponentDetail` 已就绪，纯 UI 缺口。
4. **AC-UI-09 不可判定**：无 build 阶段标识；需 `capabilities.repoActions`/`data-stage`（AC 结构性缺陷）。
5. **COMMIT/PUSH 无确认条**、CHKOUT 无选择器（FR-ACT-03/04；M2 前置，但 M1 阶段按钮应禁用，见 #1）。
6. **旧 AC 双源冲突**：ac-catalog v0.2 的 AC-BOARD-01/02、AC-DETAIL-03、AC-CI-01 仍“权威”并与 v0.3 冲突（D1），M0/M1/M2 出口无法签收。
7. **两套 AC-UI 编号**（D2）与 **spec↔SRS 语义冲突**（D3）必须先出裁决记录，否则返工。
8. **F 区“通栏”与参考图冲突**（D4-①），AC-UI-01 布局断言口径不定。
9. **批量条位置/高度与需求冲突**（D4-②），AC-UI-02 bbox 断言口径不定。
10. **UI/Host 双矩阵不一致**（FR-ACT-02 明令单一来源；UNKNOWN/FETCH、SYNCED/CHKOUT 已实测/静态分歧）。

### P1（M1 可修，影响验收与体验）
- 拖拽分栏 + `ui.panel.*` 持久化缺失（FR-WS-02/附录 D）。
- 焦点管理：抽屉无焦点陷阱、ESC 不回落（FR-WS-04/AC-UI-13 实测失败）。
- 状态栏“最近一次操作”文案模板不符（FR-OPS-05/§11.0.4）。
- `Ctrl+F`/`Ctrl+Shift+F` 缺失；AC-UI-01 Tab 顺序口径需改（FR-WS-05）。
- 文件树单次 ≤500/分页、`tree.hideDotGit` 可配（FR-TREE-01/附录 D）。
- 文件状态标记 `●/○`（FR-TREE-05/AC-UI-05）。
- 文件夹视图组件节点四仓 chip、未克隆淡显、facet 过滤/独立筛选、左栏布局裁决（FR-FOLDER-01/03/04/05）。
- E 区“详情”快捷按钮、STALE“数据 X 前”（FR-INFO-01/02）。
- 卡片：`*` 变更后缀裁决、CI 槽行位、最近活动/新鲜度、双击抽屉、Ctrl/Shift 多选（FR-CARD-01/02）。
- 批量：>10 组件/>40 仓二次确认全覆盖、菜单未选中禁用、4 入口 vs 3 入口裁决（FR-CARD-04/FR-BOARD-04）。
- CI：CI-4/CI-7 落点裁决、`ci.enabled` 开关 + Mock 注入、`ICiProvider`/`CI_UPDATED`、`ci_status` 表（FR-CI-01/02/05、AC-UI-12）。
- 七态注入（卡片区错误/离线/过期/部分失败/权限过期）（FR-CARD-05/FR-BOARD-03）。
- 夹具与计量资产：1,000/4,000 组件、14 状态、未纳管目录、大目录/长路径、fps/耗时脚本（AC-UI-02/05/06/08）。
- 截图归档目录 `docs/images/ui-v3/` 与 1440×900 基线（D6）。

### P2（收口/文档卫生）
- 卡片 CI 槽在 L1（§11.3 要 L3）、compact StateChip 无中文文案。
- 树右键“打开资源管理器”二次确认缺失（FR-TREE-06）。
- 菜单结构对齐 §11.0.2 A（D7）、无“诊断导出”入口。
- 视图持久化 vs AC-UI-14“默认入口工作台”冲突（AC 需澄清）。
- 无裸 TODO ✅；控制台 error=0 ✅（本机实测）；深色/200%/对比度门禁脚本缺失。
- 阶段截图与运行证据未归档到 SRS 指定位置。

---

## 9. Top 10 可执行修复（改 FR/AC 还是改实现）

| # | 动作 | 类型 | 具体做法 | 关联 |
|---|---|---|---|---|
| 1 | 裁决“卡片底色” | **改 FR+AC**（推荐） | 以参考图为准（D-022）允许“≤55% 状态色混合 + 4px 轨道”，把 §11.2/§11.3/FR-CARD-01 的“底色中性/禁止整卡饱和色”改为“底色必须中性基准 + 状态 tint ≤55% 且过 4.5:1 门禁”；同步 AC-UI-02 的“底色中性”断言与 spec R-04 | FR-CARD-01、§11.2、AC-UI-02、spec R-04 |
| 2 | 落地 M1 只读边界 | **改实现** | `SnapshotDto` 暴露 `Capabilities` → `api.ts` 透传 → store 在 `capabilities.repoActions=false` 时把 5 键强制 disabled 且 title 用 Host 的 `Reason`；`HostInfo.features.repoActions` 兜底；删/收拢 `viewmodel.MATRIX`，改为消费 Host `getComponentDetail.actions` 单一来源 | FR-ACT-02/07、AC-UI-09、D-024 |
| 3 | 修 F 区“通栏” | **改 FR** | 按参考图与 spec §6.1，把 FR-WS-01/§11.0.2 F 行改为“位于中+右两列下方，左列底部为批量条”，并同步 AC-UI-01 的 bbox 口径（F 左边缘=中区左边缘 ±2） | FR-WS-01、§11.0.2、AC-UI-01 |
| 4 | 统一批量条规格 | **改 FR**（推荐，spec/实现已一致） | 承认“左栏底部 52px 批量条”，删除“C 区顶部浮层 ≤36px”表述；补充“入口常驻、选中后变命令组”，AC-UI-02 断言改为“批量条出现前后 C/D/E bbox 不变 + 不新增全宽工具栏” | FR-CARD-04、§11.3、AC-UI-02 |
| 5 | 补组件详情抽屉 | **改实现** | 新增 `api.getComponentDetail` + `ComponentDetailDrawer.vue` + `DrawerKind='componentDetail'`，双击卡片/`详情`按钮打开；复用 Host 已实现的四仓+actions+CI hint；补 ESC 焦点回落与焦点陷阱 | FR-CARD-02、FR-DETAIL-01、AC-UI-11/13 |
| 6 | 修 AC 可执行性（阶段/夹具/注入） | **改 AC** | AC-UI-09 增加 `data-stage`/capabilities 前置；AC-UI-05/06/08 增加“附录 C 状态夹具 + Mock 桥注入”的前置与夹具清单；AC-UI-03 把“CDP 拦截 shell”改为“Host 日志/测试 launcher 断言”；AC-UI-01 删/拆 150–200% 与截图对比；AC-UI-12 明确“逐浮层打开”的断言序列 | AC-UI-01/03/05/06/08/09/12 |
| 7 | 回写/作废旧 AC | **改文档** | 在 ac-catalog 中标注 AC-BOARD-01/02、AC-DETAIL-03、AC-CI-01 被 v0.3 取代（表格默认、卡片尺寸/中性、CI 第 4 位、左导航入口），并同步 §13.4 映射（修 D5 的三个错映射 + 补 FR-WS-05/CARD-04/FOLDER-04/INFO-04 的 AC） | D1/D5、§13.4、M0 冻结 |
| 8 | 统一 CI 锚点编号与落点 | **改 FR+实现** | 先裁决 CI-4/CI-7：保留 E 区 4 Tab + C 区 facet（改 FR/§11.4），或补抽屉 5 Tab + B 区筛选按钮（改实现）；同时把 spec §8.1/§10.1 的“各恰好 1 个”改为“ci-slot 每卡 1 个、其余 1 个” | FR-CI-01、§11.4、AC-UI-12、spec §8.1/§10.1 |
| 9 | 绑定单一可用性矩阵 | **改实现** | 删除 UI 自算矩阵，完全使用 Host `RepoActionAvailabilityDto`（含 `MatrixAllowed`/`RequiresConfirm`/`Reason`/`Stage`）；M2 时 UI 只负责前置/确认/进行中呈现；顺带修 UNKNOWN-FETCH 与 SYNCED-CHKOUT 偏差 | FR-ACT-02/03、§7.6、AC-UI-08 |
| 10 | 补“写操作前确认”骨架 | **改实现**（M2 前置） | 在按钮条上方加确认条组件（≤48px，仅 COMMIT/PUSH），未确认不发 `runRepoAction`；CHKOUT 弹分支/Tag 选择器（数据 `remote_ref`）；DTO 增 `stagedCount`/`unmerged` 供 COMMIT 禁用判定 | FR-ACT-03/04、AC-UI-10 |

> 若只能做 3 件事：**#2（M1 只读边界）→ #7（旧 AC 作废/回写）→ #6（AC 可执行化）**。三者完成前，M1 出口即使“看起来能跑”也无法合规签收。

---

## 附录 A：运行实测快照（CDP，1440×900，2026-11-14）

```
viewport: w=1440 h=900 sw=1440 cw=1440 sh=900（无页面级横/纵溢出）
menubar      x=0     y=0    w=1440 h=30
titlebar     x=0     y=30   w=1440 h=48
componentList x=16   y=90   w=281.6 h=706
repoPanels   DESIGN(309.6,90,418.5,347) IMPL(740.1,90,418.5,347)
             MIL(309.6,449,418.5,347)   SIL(740.1,449,418.5,347)
infoPanel    x=1170.6 y=90  w=253.4 h=706
repoActions  x=309.6  y=808 w=1114.4 h=52   ← 非“通栏”（C 列底部被 BatchBar 占据）
batchBar     x=16     y=808 w=281.6  h=52   ← 常驻布局行，非“C 区顶部浮层 ≤36px”
statusbar    x=0      y=872 w=1440  h=28；segments=last-op,freshness,health,queue,ci,notify,network
卡片 computed bg：BEHIND=color(srgb .9729 .9605 .9391) / BINDING_ERROR=color(srgb .9957 .9590 .9590)
                 / SYNCED=color(srgb .9482 .9784 .9590)  ← 状态 tint，非中性
选择 SYNCED 仓后：action-pull/fetch/chkout/commit/push 全部 disabled=false（M1 应全禁用）
点击 PULL → toast「M1 只读，M2 接入」；status-last-op「✕ PULL 失败 · IMPL · E_NOT_IMPLEMENTED · 02:58:47」
双击卡片 → drawer=0 modal=0（无详情抽屉）
Ctrl+F → 焦点不变（仅 Ctrl+K 生效）；Ctrl+Shift+F → 筛选未打开
通知抽屉：打开后 activeElement=title-notifications；抽屉内 Tab×12 全逃逸；ESC 后焦点不回落
CI 锚点（逐个浮层）：筛选 popover → ci-facet=1；CI 弹窗 → ci-contract=1；设置 → ci-settings=1；
                     通知抽屉 → ci-notif-category=1；ci-slot 恒=4（每卡 1 个）；ci-nav/strip/tab/queue/card-base 恒=1
console error = 0
```

## 附录 B：核对文件索引

- 需求：`reqs/requirements.md`（§9.13–9.17 = L568–627；§11.0–11.7 = L695–915；AC-UI = L1039–1054；§13.4 = L1077–1102；附录 D = L1251–1255）
- 设计：`docs/ui-redesign-spec.md`（§3.1–3.6 L239–326；§6 L521–588；§7 L592–680；§8.1 L686–702；§10.1–10.2 L878–920；§11.2 裁决 L954–973）
- 实现：`ui/src/components/workbench/*`、`ui/src/components/folder/*`、`ui/src/components/shell/*`、`ui/src/components/overlays/*`、`ui/src/stores/workbench.ts`、`ui/src/viewmodel.ts`、`ui/src/api.ts`、`ui/src/types/workbench.ts`
- Host：`src/MbdGitTool.Host/Bridge/HostCommandRouter.cs`、`src/MbdGitTool.Host/Services/{RepoTreeService,WorkspaceTreeService,OpenPathService,RepoActionService,ComponentDetailService,CiStatusService}.cs`、`src/MbdGitTool.Core/{Files/FileTreeReader,Security/PathPolicy,Contracts/*}.cs`
- 测试：`tests/MbdGitTool.Host.Tests/*`（12 个测试文件，覆盖 Host API/路径安全/openPath/文件树/提交/组件详情/矩阵）
- 旧 AC：`reqs/v0.2/ac-catalog.md`（AC-BOARD-01 L579、AC-BOARD-02 L592、AC-DETAIL-03 L730、AC-CI-01 L1375、FR→AC 矩阵 L1515）
- 截图/脚本：`artifacts/ui-review/40-v3-workbench.png`、`41-v3-folder.png`、`50-v3-folder-expand.png`、`drive-v3-func.mjs`、`test-folder.mjs`
