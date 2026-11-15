# UI 视觉 / 交互对抗评审（v3 工作台）

> 评审日期：2026-11-14 03:00–03:10（本地）
> 视觉基准（唯一）：`docs/images/reference/main-ui-reference.png`（652×441，有效画布 585×379，drawio 示意）
> 规格输入：`docs/ui-redesign-spec.md` §1–§8、`docs/ui-redesign-plan.md`、`reqs/specification.md` §9.13–9.17 / §11.0–§11.7
> 评审对象：**运行中的 v3 工作台**（`https://app.local/index.html`，CDP 127.0.0.1:9222）
> 运行产物：`ui/dist` build = **02:55:11**；源码快照 = **~02:55:48**（`RepoActionsBar.vue` 在 build 之后被修改，见 P0-5）
> 视口实测：1440×900 / 1280×800，dpr=1；对比截图：`artifacts/ui-review/40-v3-workbench.png`、`41-v3-folder.png`、`45-v3-action-toast.png`、`48-v3-1280.png`、`50-v3-folder-expand.png`、`51-v3-folder-repo-select.png`（前六者实际为 1424×861，非 AC-29 基线尺寸）
> 方法：CDP `Runtime.evaluate` 读取 bbox/computedStyle + `Input.dispatchMouseEvent/KeyEvent` 真实交互 + 源码逐条核对；本报告**只新建本文件，未改任何代码/产物**。
> 偏差：评审期间另有 CDP 会话并发操作同一页面（发现外部注入的 `id:"zz"` 桥消息），关键项均 2–3 次复现后才记录。

---

## 0. 结论（Gate 判决）

| 维度 | 结论 |
|---|---|
| 与参考图**结构**对位 | **7/7 区域齐全、顺序正确**：A 菜单栏 → B 标题区 → C 组件卡片列表 / D 2×2 四仓树 / E 右信息面板 → F 五键按钮条 → G 状态栏；无左侧图标导航、无表格表头、无第三条全宽工具栏。 |
| 与参考图**几何**对位 | 1440：C=281.6、D=849（单面板 418.5×347、gap12）、E=253.4、F 起点=309.6（= Design 面板左缘）、按钮 213.3×40、状态栏 28；1280：249.6 / 749.8（368.9×297）/ 224.6 / 187.7×40；两档均 `document.scrollingElement.scrollWidth==innerWidth`。**几何合格**。 |
| 与参考图**视觉语言** | **部分不像**：底部按钮为 5 个 213×40 白底大按钮（参考图为小号绿色按钮，用户观感"过大/灰/无效感"）；卡片状态色 tint 实测接近白色（与参考图"卡片颜色由聚合状态决定"的意图不符）；右栏在 1280 下字段省略/换行、内容 666/496 首屏即滚动。 |
| 交互可用性 | 存在 **5 个 P0**（M1 按钮可点但必失败、批量勾选双触发失效、批量条重叠、文件夹视图展开仓无子项、源码与 dist 漂移），不满足 S5 出口"P0/P1 清零"。 |
| **能否进入下一阶段** | **不能**。最低返工：P0 清零 → 重建 `ui/dist` → 1440×900/1280×800 重截图 → 跑 §7 验收清单。 |

---

## 1. 实测事实（可作为修复前后对比基线）

### 1.1 1440×900 运行态（dist 02:55:11）

| 元素（选择器） | 实测值 |
|---|---|
| `[data-testid=component-list]` | x=16 y=90 **281.6×706**；`.list-head` 40；`.list-body` 664 |
| `[data-testid=repo-grid]` | x=309.6 y=90 **849×706**；4×`[data-testid=repo-tree-panel]` **418.5×347**（title 32 / body 312，gap 12） |
| `[data-testid=info-panel]` | x=1170.6 **253.4×706**；ci-strip 40 / repo-head 36 / tabs 32；`.info-body` **596 可见 / 656 内容（已滚动）** |
| `[data-testid=repo-actions]` | x=309.6 y=808 **1114.4×52**；每个 `.action` **213.3×40**，gap 12 |
| `[data-testid=statusbar]` | y=872 **1440×28**；7 段（首段 947.3 弹性） |
| 卡片 `[data-testid=component-card]` | 76×253.6，radius 8，border 1 #e2e5e9，rail 4（选中 5），ID 12px/600，chip 18px/10px |
| 面板标题 | 32px，`background: #f1f2f4`，`border-bottom: #e2e5e9`；节点行 28px/12px |
| 页面 | `overflow:hidden`，sw=sh=1440×900（无页面滚动） |

### 1.2 1280×800 运行态

- `component-list` 249.6×606、`repo-grid` 749.8（面板 368.9×297）、`info-panel` 224.6×606、`action` 187.7×40、titlebar 1280/1280 不溢出、statusbar 1280 不溢出；
- 但右栏 `.info-body` **666 内容 / 496 可见**；`对比基准 refs/remotes/origin/main` 被省略号截断、`↑0/↓0（相对 compare_ref）`、`已跟踪 0 · 未跟踪 0 · stash 0`、`CI 未接入（v1 占位）` 均回绕成 2 行；卡片 `.row2` 四仓章 200/200 未溢出。

### 1.3 颜色实测（关键视觉证据）

| token | 卡片 tint 实测（1440 浅色） | 与白底差异 |
|---|---|---|
| warning（RTCT_Demo） | `rgb(254,249,241)` / `#FEF9F1` | 极弱（肉眼近白） |
| danger（RTCT_Incomplete） | `rgb(254,245,245)` / `#FEF5F5` | 极弱 |
| neutral（RTCT_RemoteOnly） | `rgb(247,248,249)` / `#F7F8F9` | 与白几乎无差 |
| success（RTCT_Synced） | `rgb(242,250,245)` / `#F2FAF5` | 极弱 |
| success **selected** | `rgb(246,251,248)`（比未选中**更浅**） | 选中反而向白色混合 |

> 选中态实测比未选中更浅：`.component-card.selected{background: color-mix(in srgb, var(--card-tint) 72%, var(--surface))}`（ComponentCard.vue:149-153）把"已混过 55% 的 tint"再向 `--surface` 混，数学上只会变浅，与 spec §3.1"tint 加深到 70%"相反。

---

## 2. 与参考图逐区域对比

> 参考图是 drawio 示意图（色块为默认色板，spec §1.3 明确"不可照抄"），故判定分两层：**结构/比例** 与 **视觉语言/交互**。比例差异凡 spec 已裁决（R-06：左 20/中 62/右 18）不再重复计缺陷。

| # | 区域 | 参考图 / 规格 | 当前运行态实测 | 判定 | 具体差异（px / 色 / 类名） |
|---|---|---|---|---|---|
| A | 菜单栏 | 通栏、20px 示意、6 菜单；spec 30px/12px、Alt 聚焦 | 30px、#fff、1px `#e2e5e9`、6 项 12px、item 22px 高 | **像**（结构/比例） | 无 Alt+字母/方向键导航（spec §2.7）；`menu-actions` 项未与按钮可用性同步禁用 |
| B | 标题区 | 32px 示意、仅"标题区域"文字；spec 48px 一行 | 48px，14 个子元素：brand 90 / 版本 46 / 组织 52 / 计数 73 / 数据源 101 / 工作区路径 210 / 搜索 317 / 视图段 114 / 5 图标 28×5；1440 余量 spacer=169，1280 余量=44 | **结构像、密度高于参考图**（spec 授权合并工具栏） | 1280 下"次要信息不收进更多"（spec §2.7 允许隐藏组织/计数/路径）；`.ws` 固定 210px 长路径截断 |
| C | 左组件列表 | 24.4% 宽、卡片矩形列表；spec 20%（281.6） | 281.6×706；head 40；卡片 76、间距 8、radius 8、border 1px | **比例按 spec 收窄**（R-06），不算偏差 | 列表头"组件 4 / ⇅严重度 / ⏳筛选 / ☑"比参考图多 3 个控件（spec §3.2 要求） |
| C1 | 组件卡片 | 卡片按聚合状态着色、卡片列表 | 76px，3 行 20/20/16；rail 4px + 近白 tint；选中蓝框+内描边 | **结构像、状态色不像** | tint 实测见 §1.3；选中态双蓝线（1px border + inset 1px）且更浅；`*` 出现 3 处（ID、Impl\*、SIL\*）→ 视觉噪声（SRS FR-CARD-01 明确禁止字段用 `*`） |
| C2 | 左栏底部批量条 | 参考图该处为**空白**；spec §3.5 授权占用 | 52px 常驻：`☑ 选择` + 灰色常量文案"批量：SYNC / CREATE / Clone / Delete" | spec 合规；**观感像禁用命令** | 文案不可点、无 affordance；进入选择模式后内容溢出（见 P0-3） |
| D | 中间 2×2 四仓树 | 四块 167×112（占宽 28.5%）、硬边示意 | 四块 418.5×347、gap 12、radius 8、1px 边框、无阴影；顺序 DESIGN/IMPL/MIL/SIL | **像**（几何精确达标） | 面板内容仅 2 行文件（夹具），空白 >85%，与参考图"树块"的信息密度差距大（部分是夹具问题） |
| D1 | 面板标题 | 参考图为标题+说明文字；spec 32px：状态点+类型+仓名+`*`+状态 chip+CI 槽+刷新 | 32px，6 个元素，`background:#f1f2f4`；"● DESIGN RTCT_Demo_Design [✓] [∅虚框] ⟳" | **清晰但拥挤** | CI 空槽与状态 chip 同尺寸并排（18px vs 20px），在 368.9px@1280 时仓名先被省略；CI 虚线框 × 4 面板 + × 4 卡片 + 右栏 = 9 处虚线占位 |
| D2 | 树节点 | 树状分层、双击打开；spec 28px/12px/图标/状态字母 | 28px/12px；`.node-icon` 字符 `≡`(txt/md)/`•`(m/c/h)；选中 `info 40% + inset 2px 焦点线` | **节点清晰** | 文件名图标 `≡` 与参考图观感差异小；工作台无目录夹具 → 展开/层级未验证（需补夹具） |
| E | 右侧信息面板 | 参考图 6.7% 竖条（示意图，仅"选中子仓基本信息和构建是否成功"）；spec 18%/253.4、四 Tab | 253.4；CI 条 40 + 仓名 36 + Tabs 32 + 内容滚动；字段 10 行、commit 5 行 | **合规但过挤**（P1-3） | 1440 内容 656>596；1280 内容 666>496；`对比基准`/`本地路径` 省略；"超前/落后（相对 compare_ref）"行高 30.8 形成 2 行；commit 行 10px/36px 高 |
| F | 底部五键 | 5 个**绿色小按钮**（#D5E8D5，示意）55×16 占比 9.4%；spec 40px、flex1、min112/max220 | 5×**213.3×40**、#fff、1px 边框、12px/600、disabled `opacity:.45` | **不像**（P1-2）+ **行为缺陷**（P0-1） | 宽 14.8% vs 参考 9.4%（≈1.6×）；白底灰字+45% 透明 → "无效感"；参考图绿色可行动暗示丢失 |
| G | 状态栏 | 26px 示意、单段"最近一次操作状态"；spec 7 段 | 28px、7 段：最近操作 / 新鲜度 / Host·GitWorker / 队列 / CI 队列 / 🔔 / 在线 | **像** | 左段文案仍是"就绪 · 无最近操作"（SRS §11.0.2 G 模板为"最近一次操作：…"）；实测曾显示红字"✕ FETCH 失败 · IMPL · E_NOT_IMPLEMENTED"（P0-1 连带） |
| H | 文件夹视图（中区） | 新增视图（参考图无）；spec §7.2 标题 36/行 28/懒加载 | 标题 36、行 28、组件组展开 8 行、根/筛选/刷新齐全 | **结构合规、2 个交互缺陷** | 行首**重复箭头**（DOM 文本"▸▸RTCT_Demo"）；仓库节点展开**无子项**（P0-4）；切视图不高亮当前选中（P1-5） |
| I | 浮层/Toast | spec：CI 960×640、设置 760×560、Toast 置底居中 | CI/settings 尺寸达标，`modal-overlay rgba(10,12,16,.38)`；Toast 实测 y=771–808、宽 143–?，**底边=按钮顶 808（0px 间隙）** | 合规（P2 细节） | Toast `bottom: calc(28+52+12)=92` 恰好贴在按钮条顶；若要 12px 间隙应为 `104px`（ToastHost.vue:31） |
| J | 深色主题 | token 换值不换名 | `--surface:#1c1f26`；卡片 dark tint `rgb(41,34,23)/(45,23,26)/(29,32,39)`；按钮/面板/状态栏全部暗色 | **通过** | 未发现白底闪烁；卡片 tint 偏"脏"但文字对比可读；建议纳入 P2 对比度扫描 |

---

## 3. 用户视角走查（"像不像参考图"）

| 走查点 | 判定 | 说明 |
|---|---|---|
| 组件卡片像不像参考图的卡片列表 | **半像** | 卡片高度 76、圆角 8、1px 边框、4px 状态轨、选中/状态 chip 都有；但"卡片底色由状态决定"的参考图意图几乎看不出来（tint 近白），且一张卡上出现 `*` ×3 + CI 虚框 + 状态 chip，信息噪声大于参考图的"一卡一块色"。 |
| 2×2 树面板标题与文件节点是否清晰 | **清晰但空** | 标题"● DESIGN 仓名 [状态] [CI] ⟳"层级可读；节点 12px/28px、具备类型图标与 git 字母通道；但夹具每仓仅 2 个文件、无目录，参考图那种"分层树"几乎显示不出来，且 4 面板同时存在 8 个空占位（CI 虚框）削弱"工作区主角"感。 |
| 右侧信息是否过挤 | **过挤** | 1440 已需滚动（656/596）；1280 更甚（666/496），字段值省略/换行、最近提交 10px；参考图右栏只是"选中子仓基本信息+构建是否成功"的窄条，当前塞入 4 Tab+10 字段+5 提交。 |
| 底部 5 个按钮像不像参考图的绿色按钮 | **不像** | 白底、213×40、灰字；启用态仅 100% 不透明，禁用态 45%，主次只靠这一点区分；参考图 5 个小绿钮的"可行动"暗示在当前实现里丢失。 |
| 状态栏信息是否合理 | **基本合理** | 7 段与 SRS 对齐、最左"最近操作"满足参考图；但默认文案与 SRS 模板不同，且测试中"失败"被写入状态栏后无清除路径（复评需复位夹具）。 |

---

## 4. 交互缺陷实测

| # | 操作（CDP） | 期望（spec/SRS） | 实测 | 结论 |
|---|---|---|---|---|
| I1 | 点 `[data-testid=action-fetch]`（AHEAD 状态，enabled） | M1 只读：按钮应 disabled，或点击不得产生"失败" | 360ms 后 Toast `! M1 只读，M2 接入`，状态栏 `✕ FETCH 失败 · IMPL · E_NOT_IMPLEMENTED`；按钮再次恢复 enabled | **P0**（可点但必失败，FR-ACT-02/07） |
| I2 | 选择模式点卡片 checkbox ×2 | "已选 2 / 4"，批量条计数 2 | DOM checkbox `checked=true`，批量条仍"已选 0 组件 · 0 仓"（`@click`+`@change` 各触发一次 `onCheck`，双 toggle 抵消） | **P0**（批量入口不可用） |
| I3 | 进入选择模式 | 批量条单行不溢出、控件不重叠 | 280px 内：SYNC x=100 / CREATE 145 / Clone 200 / ⋯ 245 / 全选结果 **x=187 w=56（与 Clone 重叠）** / ✕ x=249–273（与 ⋯ 重叠）；`scrollWidth=clientWidth`（overflow hidden 被裁） | **P0**（选择模式 UI 破版） |
| I4 | 文件夹视图展开仓库节点（3/3 复现） | 懒加载文件行；空目录显示"（空）"；加载显示"加载中…" | 箭头 ▾ 翻转、行数不变（8→8）、无 loading/空态/Toast；根响应中 REPO `children:null`，但 UI 未发后续加载请求或无渲染 | **P0**（FR-FOLDER-02 P0） |
| I5 | 卡片单击 | 选中组件→4 面板+右栏+五键同一 selection（默认 IMPL） | 正常：4 面板标题切换（RTCT_Incomplete_*）、右栏切换；MISSING 组件时右栏标题仍显示 `_Design` 而正文"未选择仓库"、五键 title "请先在文件树中选择仓库" | 主流程 ✓；MISSING 语义 P1-6 |
| I6 | 树节点单击/双击 | 单击=聚焦仓+选中文件；双击=openPath | 单击：树行 selected、聚焦仓切换、右栏 HEAD 更新；双击：Toast `✓ 已打开：component.txt`、无页面导航 | ✓；但右栏**不显示节点/文件信息**（spec §4.2），P1-9 |
| I7 | 工作台 ⇄ 文件夹 | 保留 selection/展开，≤200ms | 状态保留（scope=IMPL）；但切到文件夹视图**无任何选中行**（`workspace-node.selected` 不存在）；文件夹选中仓后切回工作台卡片同步正常 | P1-5（FR-WS-03） |
| I8 | 搜索 | Ctrl+K 聚焦、输入过滤、Esc 清空 | "Synced"→组件 1、卡片 1，Esc 清空回 4 | ✓（快捷键文案 Ctrl+K，SRS 写 Ctrl+F，P2） |
| I9 | 右键卡片 | 组件级菜单，含 SYNC/CREATE/Clone/Delete/复制/资源管理器，Esc 关闭 | 260px、10 项、2 个 danger 项、Esc 关闭 | ✓（spec 要求 240px，P2 宽度） |
| I10 | 键盘卡片导航 | ↑↓ 移动、焦点环 2px、`aria-selected` | ArrowDown 后 `document.activeElement`=ARTICLE.component-card，`outline:2px solid #2563eb offset 2px`，`aria-selected=true` | ✓（AC-UI-08） |
| I11 | Tab 顺序 | A→C→D→E→F→G | 实测：菜单 → 搜索 → 视图段/图标 → C 列表头 → 列表容器 → 批量条 → D 面板刷新/树容器 → E 头部/Tabs → …（卡片不单独 Tab，由容器+方向键，符合 listbox 模式） | 基本 ✓；文件夹树缺 `aria-activedescendant`、行不可聚焦（P2 可访问性） |
| I12 | 1280 适配 | 无页面横向溢出；5 键同排；右栏不溢出 | 页面 sw=1280 ✓；按钮 187.7×40 同排 ✓；右栏 3 字段回绕 + 2 字段省略（有 title） | ✓ 几何 / P1 密度 |
| I13 | 深色主题 | token 换值、无白闪 | `--surface:#1c1f26`，卡片 tint/按钮/面板均跟随；无异常 | ✓ |
| I14 | Toast 位置 | 按钮条上方 12px | 实测 Toast 底边 = 按钮条顶 808（0px 间隙） | P2 |
| I15 | Alt 菜单/菜单项禁用同步 | Alt+字母开菜单、↑↓ 选择；禁用项不可点 | 无 Alt 处理；"单仓 PULL"菜单项在按钮禁用时仍可点（点击走 runAction → 仅 Toast 警告） | P2（违反 spec §2.7） |

---

## 5. P0 / P1 / P2 清单（含证据与定位）

### P0（阻断，必须清零）

| ID | 现象 | 证据 | 定位 | 修复要点 |
|---|---|---|---|---|
| **P0-1** | M1 阶段底部五键按状态矩阵启用（AHEAD 下 FETCH/CHKOUT/PUSH 可点），点击后伪造 360ms pending 再抛 `E_NOT_IMPLEMENTED`，Toast"! M1 只读，M2 接入"、状态栏写红字"✕ FETCH 失败"，用户看到"失败"而非"未启用" | live `x-toast-fetch.png`（1440×900，按钮区 y=808–860，Toast y=771–808，状态栏左侧红字）；`45-v3-action-toast.png` 五键灰态但点击无任何反馈（disabled 静默） | `stores/workbench.ts:911-975 runAction`；`stores/workbench.ts:288-290 availability`；`common/../RepoActionsBar.vue:42-58` | availability 首行加 M1 门禁：`state.capabilities.repoActions !== true` → 五键全部 disabled，title="M1 只读：单仓操作将在 M2 接入（原因）"；`runAction` 不得写 `lastOp=error`；移除 `delay(360)` 伪 pending。 |
| **P0-2** | 批量选择 checkbox 失效：勾选后视觉选中但"已选 0"，批量按钮对空集生效 | live `x-select-mode.png`（1440×900，前两卡 checkbox 勾选、批量条"已选 0/4"） | `workbench/ComponentCard.vue:77-85`：`@click="onCheck"` 与 `@change="onCheck"` 同时触发 → 两次 `toggleChecked` 抵消 | checkbox 只保留 `@change`（或 `@click.stop` 且不 emit change）；补 `:checked` 受控单测；`aria-checked` 与实际一致。 |
| **P0-3** | 选择模式批量条内容溢出重叠，且与组件列表底部 `.select-hint` 重复两套"全选结果/清除" | live `x-select-mode.png`（批量条 y=808–860，宽 281.6）；实测子项 x：SYNC 100 / CREATE 145 / Clone 200 / ⋯ 245 / 全选结果 187–243 / ✕ 249–273 → 两处重叠；`batch-bar` 280/280 overflow hidden；另有 `ComponentList.vue:212-216` 同一批控件 | `workbench/BatchBar.vue:24-34,38-51`；`workbench/ComponentList.vue:212-216,320-330` | 选择模式只保留一行：`已选 N · SYNC · CREATE · ⋯`（Clone/Delete/恢复收进 ⋯，全选结果收进 ⋯ 或列表头）；删除 `ComponentList` 的 `.select-hint`；改 `flex:1 1 auto; min-width:0` + `text-overflow`，并加 CDP 断言（见 A4）。 |
| **P0-4** | 文件夹视图展开仓库节点：箭头 ▾ 翻转但列表无任何变化（无子文件、无"加载中"、无"（空）"、无错误 Toast），3/3 复现；参考图/规格要求"工作区根→组件→仓→文件" | live `z2-folder-repoexpand.png`、`z2-folder-repoexpand2.png`（1440×900，折叠区 x=309.6…；行数保持 8）；`50/51-v3-folder-*` 只验证到组件层，未验证到文件层 | `folder/WorkspaceTree.vue:56-75 buildRows`；`viewmodel.ts:437 loaded` 启发式（`children!==null` 即视为已加载）；`stores/workbench.ts:1185-1227 toggleFolderNode` | REPO 节点初始 `loaded:false` 必须可靠；展开时若无子项渲染"（空）"、加载中渲染"加载中…"；对 Host `getWorkspaceTree{path:"RTCT_Demo_Design"}` 做接口验证（应 entries=2）；补 AC-UI-14 文件层断言。 |
| **P0-5** | **源码与运行产物不一致**：`ui/dist`（02:55:11）底部按钮为 40px/112–220 白底；`ui/src/components/workbench/RepoActionsBar.vue`（02:55:48）已改为 30px/96–132 **绿色**；所有现有截图与 CDP 验收均跑旧产物，修复后未重建、未重截 | dist `index-BLxl6unv.css`：`.action{min-width:112px;max-width:220px;height:var(--btn-h);...background:var(--surface);flex:1 1 0}`；源码 `RepoActionsBar.vue:72-94`：`min-width:96px;max-width:132px;height:30px;background:var(--st-success-bg)`；文件 mtime 02:55:48 > build 02:55:11；live computed 高度 40/min112 | `ui/dist`、`artifacts/ui-review/4*.png`（1424×861，非 1440×900） | 先冻结样式决策（见文档冲突 D2），再 `npm run build`，重跑 1440×900/1280×800 CDP 截图到 `docs/images/ui-v3/`，以新基线验收；禁止用旧截图判定"已修复"。 |

### P1（下一阶段前应修）

| ID | 现象 | 证据/定位 | 修复要点 |
|---|---|---|---|
| P1-1 | 卡片状态 tint 近白（§1.3），"卡片颜色由聚合状态决定"不可读；选中态反而更浅 | live tint 值；`ComponentCard.vue:124-155`；截图 40 左栏 | 若保留 tint：基准改 100% `--st-*-bg` → `color-mix(--st-*-bg 100%, --surface)` 或 70% 与 bd；选中用加深方向（如 `color-mix(--card-tint 70%, --st-info-bg)`）；若按 SRS §11.3"不做整卡底色"则改中性+强化 4px 轨，**由需求方裁决**（见 D1）。 |
| P1-2 | 底部按钮不像参考图绿色按钮：213×40 白底、启用/禁用仅透明度差；源码绿版又只有 30px/96–132 | 截图 40/41/45/50/51 底部；`RepoActionsBar.vue`（源码 vs dist）；live 测量 | 统一目标：高 40（spec），取 `--st-success-bg/fg/bd`（参考图绿色意图）或 spec 中性；`max-width` 降到 160–180、整组居中/左对齐，禁用保留灰底+title；必做重建（P0-5）。 |
| P1-3 | 右栏过挤：1440 656/596、1280 666/496；1280 字段省略/2 行回绕 | live；截图 48（1280 设置弹窗下方右栏仍是截断 `/refs/remotes/origi…`） | 1280 右栏 `grid-template-columns: 56px 1fr`（spec §2.3 已给）；`对比基准` 显示短形式 `origin/main` + title 全量；`超前/落后` 去掉"（相对 compare_ref）"移入 title；commit 行 30px；或 `--col-right` 1280 提到 232–240。 |
| P1-4 | 文件夹树行首重复箭头（toggle 与 icon 同字符） | live DOM 文本"▸▸RTCT_Demo / ▸▸Design…"；`folder/WorkspaceTree.vue:85-92,281-282` | `icon()` 对 COMPONENT/REPO/DIR 返回空（由 `.ws-toggle` 单独承担），或 icon 改"▣"；工作台 FileTree 的 DIR 同样问题（`workbench/FileTree.vue:301-319`）。 |
| P1-5 | 视图切换不高亮当前选中组件；文件夹选中→工作台不定位树节点 | live 切换实测（选中 RTCT_Synced → folder 无 selectedWs）；`stores/workbench.ts:1251-1261 setView` | `setView('folder')` 时按 `selectedComponentId` 映射 `folderSelected` 并展开祖先组；切回 workbench 时按 `focusedRepoType/selectedPath` 定位卡片与树滚动。 |
| P1-6 | 禁用原因不具体：COMMIT"AHEAD"显示"当前仓库状态不允许该操作"（无原因/替代/错误码）；MISSING 组件五键 title 统一"请先在文件树中选择仓库"与实际"已选中但未克隆"矛盾 | live `btnAhead`、`afterCardClick`；`viewmodel.ts` MATRIX 的 `NA` 常量；`stores/workbench.ts:914-917` | MATRIX 内 `NA` 按状态细化（COMMIT+AHEAD="AHEAD：本地领先，需先推送"；CHKOUT+MISSING="MISSING：仓库未克隆"等）；`runAction` 对 null repo 区分"未选/未克隆/绑定异常"。 |
| P1-7 | 常驻批量条文案"批量：SYNC / CREATE / Clone / Delete"为不可点灰字，观感=禁用命令；进入选择模式又与列表底栏重复 | 截图 40/41 底部左；`BatchBar.vue:14-23`；`ComponentList.vue:212-216` | 常驻态改为"选择后可批量 SYNC/CREATE/Clone/Delete"（或按钮组 disabled + tooltip）；选择模式合并为一套控价（见 P0-3）。 |
| P1-8 | CI 空槽视觉噪声：卡片 4 + 面板 4 + 信息条共 9 处虚线框/虚线圈（#9CA3AF 虚线），读作"坏图/占位"；参考图无 CI | 截图 40/41（每卡右侧虚框、每面板标题虚框、CI 汇总 4 虚圈） | `CiBadge.vue:41-49`：空态边框改 `var(--border)`、dot none 用 10px `∅` 字符或缩小到 14px；CI 汇总条 dot 改实心中性；保持 CI-2/10 锚点位置不变。 |
| P1-9 | 工作台单击树节点右栏不切"节点信息"（spec §4.2 要求）；仅树内高亮 | live `afterTreeSingleClick`（info 仍 data-mode=overview，仅 HEAD 变化）；`InfoPanel.vue:34-38` 仅 folderSelected 生效 | InfoPanel 增加 `state.selectedPath` 分支，渲染文件名/相对路径/git 字母/所属仓（复用 §5.5 文件信息块的精简版）。 |
| P1-10 | 菜单无 Alt+字母/方向键；菜单项未与 `availability()` 同步 disabled（按钮禁用时"单仓 PULL"仍可点，点击只 Toast 警告） | live Tab 序列；`MenuBar.vue:15-85,97-101`；`stores/workbench.ts:288-290` | 菜单项 `disabled` 绑定 availability/选中状态；加 Alt+字母与 ↑↓/Enter 键盘导航（spec §2.7、SRS §11.0.2 A）。 |

### P2（打磨/一致性）

| ID | 现象 | 定位 |
|---|---|---|
| P2-1 | Toast 底边贴按钮条顶（0px，spec 意图 12px） | `overlays/ToastHost.vue:29-31` → bottom 改 `calc(var(--layout-status-h) + var(--layout-actions-h) + 24px)` 或 +12 |
| P2-2 | 卡片 `*` 后缀 3 处（ID、Impl\*、SIL\*）与 SRS FR-CARD-01"不得用 `*`"冲突；视觉噪声 | `ComponentCard.vue:87,102-105`；spec R-03 允许两者并存 → 需裁决 |
| P2-3 | 1280 标题区仍显示组织/计数/长路径（断点 1279），spacer 仅 44px | `shell/TitleBar.vue:222-224` 断点改 `@media (max-width: 1300px)` 或按元素优先级隐藏 |
| P2-4 | 状态栏文案"就绪 · 无最近操作" vs SRS 模板"最近一次操作：…/无" | `stores/workbench.ts:296-304`；`shell/StatusBar.vue:32` |
| P2-5 | `ci-slot` 出现 4 次，但 spec §8.1/AC-UI-21 要求 10 锚点"各恰好 1 个"（文档/注册表冲突） | `CiBadge.vue:22`；`spec §8.1`；AC 脚本需按"每卡 1 个"更新或注册表改 `ci-slot` 为容器级 |
| P2-6 | CI-2 槽位在卡片 L1，SRS §11.3/§11.4 写"行 3"，spec §3.1 写 L1 → 文档冲突 | `ComponentCard.vue:76-91`；需裁决 |
| P2-7 | 2×2 树面板夹具仅 2 文件/仓，评审无法验证"分层树/目录展开/大目录" | 夹具 `artifacts/real-test/workspace`；建议给评审准备 ≥3 层/≥8 节点/含目录夹具 |
| P2-8 | 快捷键 Ctrl+K（impl/spec）vs Ctrl+F（SRS §11.6） | `TitleBar.vue:62` placeholder、`App.vue:36-40` |
| P2-9 | 截图基线不合格：40/41/45/46/47/50/51 为 1424×861，AC-29 要求 1440×900/1280×800；`docs/images/ui-v3/` 不存在 | `artifacts/ui-review/*`；建议 P0-5 重建时一并修正 |
| P2-10 | 文件夹树行不可聚焦、`ws-scroller` 无 `aria-activedescendant`；键盘仅靠全局 keydown | `folder/WorkspaceTree.vue:237-246` |

---

## 6. Top 10 可直接执行修复（按顺序）

1. **P0-1 修 `availability()`/`runAction()`**：在 `stores/workbench.ts:288` 的返回上根据 `state.capabilities.repoActions !== true` 短路为全禁用；删除伪 pending 与 `E_NOT_IMPLEMENTED` 的失败 lastOp；五键 title 统一"M1 只读：单仓操作将在 M2 接入"。
2. **P0-2 修 checkbox 双触发**：`ComponentCard.vue:77-85` 只保留 `@change="onCheck"`；`w.toggleChecked` 保持单次；补 CDP 断言"点击后已选=1"。
3. **P0-3 重排批量条**：`BatchBar.vue` 选择模式改为 `已选 N · SYNC · CREATE · ⋯` 单行（Clone/Delete/全选/清除进 ⋯ 菜单）；删除 `ComponentList.vue:212-216` 的 `.select-hint`；容器加 `min-width:0; overflow:hidden` 并给按钮 `flex:0 0 auto`。
4. **P0-4 修文件夹树仓展开**：`viewmodel.ts:437` 对 REPO/DIR 改为 `loaded=false`（只有 children 非空才算已加载）；`WorkspaceTree.vue:buildRows` 增加 loading/`（空）` 行；`toggleFolderNode` 加成功/失败 Toast 与"重试"；验证 Host `getWorkspaceTree path=RTCT_Demo_Design entries=2`。
5. **P0-5 重建产物并重截基线**：确认按钮最终样式后 `npm run build`，用 CDP `Emulation.setDeviceMetricsOverride{1440,900,1}` 重截 40/41/45/48/50/51 到 `docs/images/ui-v3/`，替换旧 1424×861 图。
6. **P1-2/P0-5 统一按钮视觉（源码）**：`RepoActionsBar.vue`：`height:40px; flex:1 1 0; min-width:112px; max-width:180px; gap:12px; background:var(--st-success-bg); color:var(--st-success-fg); border-color:var(--st-success-bd)`；禁用 `opacity:.45` + `background:var(--surface-2)`；保证五键等宽居中且不换行。
7. **P1-1 修 tint/选中**：`ComponentCard.vue:149-153` 选中背景改为 `color-mix(in srgb, var(--card-tint) 70%, var(--st-info-bg))`（变深）；tint 基准提高（或按 SRS 改中性+强化轨道，需裁决后执行）；补对比度断言。
8. **P1-3 右栏适配 1280**：`InfoPanel.vue:354-363` 在 `@media (max-width:1279px)` 或容器宽度 <240px 时两行布局（label 上/值下）；`对比基准` 显示 `origin/main`；commit-row 30px；`.info-body` 加 `scrollbar-gutter: stable`（已有）并在 1280 验证无截断字段。
9. **P1-4/P1-5 文件夹树可读性与选中映射**：`WorkspaceTree.icon()` 不再返回 ▸/▾（避免重复）；`stores/workbench.ts:setView('folder')` 按 `selectedComponentId` 定位并展开组件组，切回时按 `focusedRepoType` 恢复。
10. **P1-10/P2-8 菜单与快捷键**：`MenuBar.vue` 菜单项 `disabled` 接 `availability()`；加 Alt+F/V/O/T 开菜单与 ↑↓/Enter；`App.vue` 搜索快捷键同时支持 Ctrl+F（SRS）与 Ctrl+K（spec），UI 文案统一为 `Ctrl+F`。

---

## 7. 复评验收清单（CDP 可断言）

> 每个断言给出选择器与期望值；修复后由 Host 运行 `Emulation.setDeviceMetricsOverride` 到 1440×900 / 1280×800（dpr=1）执行。

| ID | 断言 | 期望 |
|---|---|---|
| A1 | `component-list` / `info-panel` / `repo-tree-panel`×4 / `repo-actions` bbox | 281.6±1 / 253.4±1 / 418.5×347±1 / 起点 x=309.6±1；1440 与 1280（249.6/224.6/368.9/187.7）两档均满足；`scrollWidth==innerWidth` |
| A2 | M1 门禁 | `[...document.querySelectorAll('[data-testid=repo-actions] .action')].every(b=>b.disabled)` 为 true；title 均含"M1 只读"或具体原因+替代；点击后 `status-last-op` 不出现"失败"、桥 0 次 `runRepoAction` |
| A3 | 批量勾选 | 点击 3 个 `.component-card .check` 后 `batch-bar` 文本含"已选 3"，checkbox `checked` 与 `aria-checked` 一致，无重复 toggle |
| A4 | 批量条不溢出 | `batch-bar.scrollWidth <= clientWidth+1`；子按钮两两 `rect` 不相交；每个按钮 `rect.right <= bar.rect.right` |
| A5 | 文件夹树懒加载 | 展开 REPO 节点后 `workspace-node` 数量增加 ≥2 且新增行 `data-kind=FILE`；空目录出现"（空）"；失败出现含错误码的 Toast/重试 |
| A6 | 产物一致 | `ui/dist` 构建时间 > 源码修改时间；截图存在 `docs/images/ui-v3/workbench-1440.png`、`folder-1440.png`、`workbench-1280.png` 且尺寸分别为 1440×900、1280×800；`Runtime.exceptionThrown`/console error = 0 |
| A7 | 卡片状态色 | 选中卡背景亮度 < 未选中卡（或与 token 计算值一致）；警告/危险/成功/中性四卡 `backgroundColor` 两两不同且与 `--surface` 差值 ≥ 设定阈值（建议 ΔR+ΔG+ΔB ≥ 12） |
| A8 | 按钮几何 | 五键 `height=40±1`、等宽（max-min≤1）、不换行、`aria-label` 含"单仓操作：IMPL"；禁用 `opacity≈.45` 且 `disabled` |
| A9 | 右栏 1280 无横向溢出 | 每个 `.field-value`：`scrollWidth<=clientWidth` 或（有 `title` 且 `clientWidth>0`）；`.field` 高度 ≤ 2 行；`.info-body.scrollHeight-clientHeight` 记录基线不回退 |
| A10 | 文件夹树无重复箭头 | 每个 `workspace-node` 的 `textContent` 不匹配 `/^[▸▾]\s*[▸▾]/`；切视图后 `workspace-node.selected[data-path=RTCT_Synced]` 存在；切回 `component-card[aria-selected=true][data-component-id=RTCT_Synced]` 存在 |
| A11 | 键盘/焦点 | ArrowDown 后 `document.activeElement` 为 `article.component-card`，`getComputedStyle(outline-width)=2px`、`outline-offset=2px`；Tab 顺序含 `menubar→titlebar→component-list→...→repo-actions→statusbar` 关键锚点 |
| A12 | Toast 与浮层 | Toast 底边 = 按钮条顶 −12±1；ESC 关闭 modal 后 `document.querySelector('[data-testid=modal]')===null` 且焦点回到触发按钮；CI/设置/通知/筛选打开后 10 个 CI testid 命中 |
| A13 | 深色主题 | `Emulation.setEmulatedMedia{prefers-color-scheme:dark}` 后 `--surface=#1c1f26`；卡片/按钮/面板无白底；console error=0 |
| A14 | 截图对比 | 1440/1280 截图与参考图区域比例：C 20%±1、D 58.9%±1、E 17.6%±1；F 行起点 = D 左缘 ±2；G 高 28；A/B/F/G 高不超过 40/64/64/32 |

---

## 8. 需裁决的文档冲突（转需求/规格）

| # | 冲突点 | A 方 | B 方 | 当前实现 |
|---|---|---|---|---|
| D1 | 卡片底色 | SRS §11.2"卡片底色永远中性" / §11.3"不做整卡底色" | spec §3.6/§11.2 R-04"允许 55% 状态 tint + 4px 轨" | 55% tint（近白） |
| D2 | 底部按钮色 | 参考图 `#D5E8D5` 绿色 | spec §1.3"drawio 色不可照抄"+§6.3 中性边框按钮 | 源码绿 30px（未构建）/ 运行白 40px |
| D3 | 间距/边距 | SRS §11.0.3 外边距 12 / 面板间隙 8 | spec §2.4 边距 16 / 间隙 12 | 16 / 12（按 spec） |
| D4 | 搜索快捷键 | SRS §11.6 `Ctrl+F` | spec §2.7/§3.2 `Ctrl+K` | Ctrl+K |
| D5 | CI-2 槽位位置 | SRS §11.3/§11.4"行 3" | spec §3.1"L1 右侧" | L1 |
| D6 | CI 锚点唯一性 | spec §8.1/AC-21"10 个 testid 各恰好 1 个" | CI-2 语义为"每卡一个槽位" | `ci-slot`=4 |
| D7 | M1 按钮策略 | SRS FR-ACT-02 D-024"M1 五键全禁用（含 FETCH）" | spec §6.2 v1 可用性矩阵（按状态启用） | 按状态启用 → P0-1 |
| D8 | 变更标记 | SRS FR-CARD-01"卡片内不得用 `*`" | spec R-03"`*` 后缀 + `●N` 并存" | 两者都有 |

---

## 9. 局限与说明

0. **证据索引**：本报告引用的 given 截图均位于 `artifacts/ui-review/`；本次 live 复测截图/JSON 位于 `C:\Users\Trevn\AppData\Local\Temp\ui-audit\`（`w-1440-workbench.png`、`x-toast-fetch.png`、`x-select-mode.png`、`z2-folder-repoexpand.png`、`x-dark.png`、`y-1280-workbench.png` 及 `result*.json`，不在仓库内、不入库）。
1. **并发评审干扰**：评审期间同一页面有其它 CDP 会话操作/注入桥消息（观察到 `id:"zz"`、`path:"no-such-dir"` 的注入响应），交互类结论均做 2–3 次复现；纯几何/样式数据不受影响。
2. **截图-源码漂移**：`RepoActionsBar.vue` 在最近一次 `vite build` 之后被修改（P0-5），本报告运行态结论基于 dist 02:55:11；修复后必须以新构建复评。
3. **夹具稀疏**：`artifacts/real-test/workspace` 每仓仅 2 个文件、无子目录，2×2 树的信息密度、目录折叠/双击、大目录/长路径均无法在此夹具上验证；这些项在 AC 中仍需独立夹具（P2-7）。
4. 本报告不修改任何代码/产物/既有文档；建议的修复由实现方执行后按 §7 复评。
