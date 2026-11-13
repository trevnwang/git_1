# MBD Git Tool v3 主界面 UI/UX 规格（工作台）

> 状态：设计冻结候选（S1 交付物，可直接驱动 S2/S3 实现）
> 日期：2026-11-14　版本：spec-v3.0
> 唯一视觉基准：`docs/images/reference/main-ui-reference.png`（652×441 PNG，图内画布 585×379，源自 drawio 490×319 的 1.19× 导出）
> 需求基线：`reqs/requirements.md` SRS v0.2.1 §7 / §9.5–9.8 / §11；现状锚点：`docs/ui-review-m0.md`；节奏：`docs/ui-redesign-plan.md`
> 现有实现：`ui/src/App.vue`（单文件 7 视图 + 左图标导航）、`ui/src/api.ts`、`ui/src/styles/tokens.css`、`src/MbdGitTool.Core/Contracts/SnapshotDto.cs`、`src/MbdGitTool.Host/Bridge/WebViewBridge.cs`
> 交付约束：本规格**只新增本文件**，不改动任何其他文件；实现阶段再动代码。

---

## 0. 结论摘要（实现者先看这 10 条）

1. 主界面改为**单页工作台**：菜单栏 30 / 标题区 48 / 工作区（左组件卡片 20% + 中 2×2 四仓树 62% + 右信息面板 18%）/ 底部单仓按钮 52 / 状态栏 28；页面本身永不滚动。
2. 菜单栏承接原左侧图标导航的全部 7 个入口；标题区合并 SRS §11.1 的全局工具栏（搜索 / 视图切换 / 刷新 / 通知 / 任务 / 设置）。
3. 左栏卡片为**三行 76px 定高**：组件 ID + 聚合状态 + CI 槽位 / Design·Impl·MIL·SIL 四仓章（含 `*` 变更后缀）/ 分支 + Tag + `●N` 变更徽标。
4. 卡片按聚合状态着**低饱和底色**（参考图文字 3.2 的意图）+ 4px 左轨；颜色语义以 SRS §11.2 token 为准（裁决 R-04）。
5. 中区固定 4 块树面板（Design/Impl/MIL/SIL，2×2 行优先），**缺仓也保留面板**显示空态，保证几何稳定；标题 = 仓名 + `*` + 状态 + CI 槽位。
6. 右栏顺序：40px 组件 CI 汇总条（CI-3）→ 36px 选中仓标题 → 32px 四 Tab（概览/提交/差异/CI，CI-4）→ 可滚动内容；"构建是否成功"只在 CI 通道表达。
7. 底部 PULL/FETCH/CHKOUT/COMMIT/PUSH 只作用于**当前选中单仓**，五键等宽铺满中+右两列（参考图几何），禁用原因写进 `title` 与状态栏。
8. 批量操作入口放 4 处，**不新增任何工具栏**：左栏底部 52px 批量条（复用参考图左栏下方的空白）、卡片 hover SYNC/CREATE、右键菜单、菜单栏「操作」。
9. **文件夹视图 = 候选 A：工作区目录树**（根 → 组件虚拟组 → 仓目录 → 文件），与工作台共用左栏、右栏、底部按钮与批量条，只替换中区；候选 B（四仓合并树）降级为工作台的 P2 布局选项，不作为文件夹视图。
10. 新增 Host 只读接口 5 个：`getRepoTree` / `getWorkspaceTree` / `getRepoDetail` / `getCommits` / `openPath`；另 `getCiStatus` 为 Mock、`runRepoAction` 为 M2 预留；`SnapshotDto` 仅**追加可选字段**，旧 UI / 旧测试零改动可编译。

---

## 1. 视觉基准解读

### 1.1 参考图实测几何（像素）

参考图为 drawio 示意图导出（652×441），有效画布 bbox = `(35,42)-(619,421)` = **585×379**；图内文字框与实测像素为 1.19× 关系。顶部"主界面示意图"字样与左上角小图标是**图注/示意图窗口标记，不属于 UI**；虚线是示意图分隔线，实现一律改为 1px 实线 `var(--border)` + 12px 留白，虚线仅保留给 MISSING/EMPTY 空态。

| 区域 | 实测 bbox (x1,y1,x2,y2) | 尺寸 | 占画布宽 × 高 | 图面备注 |
|---|---|---|---|---|
| 菜单栏 | 35,42 → 619,61 | 585×20 | 100% × 5.3% | 登录认证/配置参数，存本地 json |
| 标题区域 | 35,64 → 619,95 | 585×32 | 100% × 8.4% | Title、版本、总组件数、总仓数 |
| 组件列表卡片 | 35,117 → 178,347 | 143×231 | 24.4% × 60.9% | Component 1…N；卡片内 4 仓状态、分支、SYNC/CREATE |
| Design 树 | 214,117 → 381,229 | 167×112 | 28.5% × 29.6% | 树状，可分层打开，双击默认程序打开 |
| Impl 树 | 392,117 → 559,229 | 167×112 | 28.5% × 29.6% | 同上 |
| MIL 树 | 214,233 → 381,345 | 167×112 | 28.5% × 29.6% | 同上 |
| SIL 树 | 392,233 → 559,345 | 167×112 | 28.5% × 29.6% | 同上 |
| 右侧信息 | 571,120 → 610,335 | 39×215 | 6.7% × 56.7% | 选中子仓基本信息 + 提交历史 + 滞后超前 + build 是否失败（图内竖排文字） |
| 按钮行 | 214,359 → 601,379 | 387×20 | 66.2% × 5.3% | PULL/FETCH/CHKOUT/COMMIT/PUSH，5 键等距铺满，只在树+信息列下方 |
| 状态栏 | 35,395 → 619,421 | 585×26 | 100% × 6.9% | 最近一次操作的状态 + 进程健康 |

关键比例事实：**左 24.4% / 中 59.1% / 右 6.7%**（横向），左栏下缘与树区下缘齐平，按钮行**从 Design 树左边缘开始**、贯穿到右侧信息面板右边缘；左栏下方（按钮行左侧）是空白，这正是批量条的落点（§3.6）。

### 1.2 逐区域解读

| 区域 | 图面意图 | 视觉层级 | 实现裁决 |
|---|---|---|---|
| 菜单栏 | 全局命令：登录/认证、配置、视图；配置保存为本地 json | L0 常驻 chrome，最低视觉重量：无底纹、1px 下边框、13px 文字 | 中性 `--surface` 底；**不照抄参考图浅蓝底**（蓝在 SRS 是 info 状态色，不能当装饰）；下拉菜单 220px 起，快捷键右对齐 |
| 标题区域 | 产品身份 + 规模 + 操作入口；参考图只有文字 | L1：14px 产品名 650 + 11px 胶囊 + 12px 元信息 | 合并 SRS 全局工具栏：左"产品名/版本/组织/组件数/仓数/数据源/扫描"、中"全局搜索"、右"视图切换/刷新/通知/任务/设置"；48px 高，一行不换行，溢出用省略 + title |
| 左组件列表 | 全部组件及其 4 仓状态；卡片本身按聚合状态着色；dirty 用 `*`；显示当前分支（不一致显示 Impl 分支）；卡片提供 SYNC/CREATE | L1 导航 + 状态总览：卡片左轨 4px 是最强状态信号，底色是弱提示 | 卡片 76px 三行；左轨 + 55% 状态色底（裁决 R-04）；置顶列表头 40px（计数/排序/筛选/多选）；底部批量条 52px；列表纵向虚拟滚动 |
| 中间 2×2 文件树 | 四仓平铺对照，任何一仓可独立浏览；双击打开 | L2 主工作区：四块等权、等尺寸，标题条是每块的唯一状态来源 | 4 块等宽等高（行优先 Design/Impl/MIL/SIL）；标题 32px；面板 1px 边框 + 8px 圆角、**无阴影**（参考图是扁平硬边）；每块独立滚动、独立展开态、独立缓存 |
| 右侧信息面板 | 选中子仓的基本信息、提交历史、滞后/超前、远程 build 是否失败 | L3 详情：字号 11–12，标签弱化、值可复制 | 参考图 30×181px 竖条是**几何示意，不是可用尺寸**（30px 只能排 9 个竖排汉字）；落地为 253px（1440）/224px（1280），内容按 §5 分四 Tab；滚动区域仅内容区 |
| 底部按钮栏 | 单仓操作（明确"不同时对四个子仓库发起"）；只在选中仓上生效 | L2 动作：40px 高按钮、等宽、与左栏批量条同高对齐 | 五键 `flex:1`、min 112 / max 220，行内 gap 12；只占中+右两列；按钮常态边框、hover 提亮、禁用降透明度 + title 原因、进行中 spinner + `aria-busy` |
| 状态栏 | 最近一次操作状态 + 后台进程健康 | L0 chrome，11px，信息密度最高但最不抢眼 | 28px；左起第一段恒为"最近操作"（参考图唯一明确要求），其后数据新鲜度/进程健康/队列/CI 队列/通知/网络（SRS FR-OPS-05 的 6 段全部保留） |

### 1.3 参考图 → v3 实现的几何映射（含不可照抄处）

| 区域 | 参考图比例 | v3 实现（1440×900） | 差异理由（以图为准的部分 / 必须修正的部分） |
|---|---|---|---|
| 横向左栏 | 24.4% | **20.0%（281.6px）** | 保持"左栏明显窄于中区"的结构；参考图 143px 卡片放不下 ID+状态+CI+4 仓章，按 plan 既有 20% 裁决 |
| 横向中区 | 59.1% | **58.9%（849px）+ 12px 内缝** | 2×2 等分 418.5px，几何意图完全保留 |
| 横向右栏 | 6.7% | **17.6%（253.4px）** | 唯一明确的"比例修正"：竖条无法承载 FR-DETAIL-01/02；差额全部从右栏取得，左/中比例与图一致；plan 已裁决 20/62/18 |
| 纵向菜单/标题 | 5.3% / 8.4% | 30px / 48px（3.3% / 5.3%） | 真实菜单项需 30px 命中区、标题区需容纳 30px 搜索框；参考图为示意图比例 |
| 纵向主体/按钮/状态 | 60.9% / 5.3% / 6.9% | 706px / 52px / 28px | 按钮行 52 = 40px 按钮 + 12px；状态栏 28 是 11px 字号下限 |
| 按钮行起点 | Design 树左缘 | `grid-column: 2 / -1` | 完全照图：左栏行高延伸到底部批量条，按钮行不覆盖左栏 |
| 卡片列表下缘 | 与树区下缘齐平 | 行 1 底 = 树区底 | 完全照图；行 2 左侧空位改为批量条 |
| 面板圆角/描边 | 示意图直角虚线 | 8px 圆角 + 1px 实线 + 无阴影 | 示意图线条非视觉规格；扁平硬边 + token 边框与参考图"块面分割"一致 |

**不可照抄项（图面示意色）**：参考图填充色（菜单 #DBE8FB、标题/状态栏 #FEE6CE、卡片 #FECDC7/#E1D5E6、按钮 #D5E8D5）是 drawio 默认色板，用来区分框体，不是产品色板。v3 采用：中性面（surface/surface-2）+ 1px 边框 + 状态 token 表达状态。参考图**文字规则**（3.1–3.6）与 SRS 状态色冲突时，按用户硬要求"保留状态色"执行 SRS §11.2（详见 §11.2 裁决 R-04/R-05）。

### 1.4 配色意图与 token 落地

- 唯一颜色来源：`ui/src/styles/tokens.css` 的 `--st-*`（通道 A：仓库生命周期）与新增 `--ci-*`/`--tint-*`（由现有 token 派生，**不新增 hex**）。
- 三重编码：颜色 + 图标/形状 + 中文文案，三者缺一不可（SRS §11.2 颜色纪律）。
- 红色只用于阻断/失败（BINDING_ERROR/BLOCKED_LOCAL/UNMERGED/OP_PARTIAL_FAILED/REMOTE_GONE/lfs_missing/CI FAIL/破坏性确认）；AHEAD 用 info、DIVERGED 用 purple、MISSING/EMPTY 用 neutral 虚线、UNKNOWN 用 hatched。
- 卡片底色 = `color-mix(in srgb, var(--st-<tone>-bg) 55%, var(--surface))`（裁决 R-04）；文字仍用 `--text`，状态 chip 用完整 token；CI 永不参与底色（CI-10）。
- 深色主题：token 换值不换名；`@media (prefers-color-scheme: dark)` 已存在，tint 公式自动跟随；`prefers-reduced-motion` 下关闭所有过渡；`forced-colors` 下保留边框/图标。

---

## 2. 新版界面规格

### 2.1 区域树、尺寸与层级

```
AppShell (100vw × 100vh, overflow:hidden)
├─ MenuBar            高 30  L0
├─ TitleBar           高 48  L1
├─ Main (overflow:hidden, padding 12 16)
│  ├─ Workbench（默认视图）
│  │  ├─ ComponentList   col1 row1  20% 宽 × 1fr
│  │  ├─ BatchBar        col1 row2  20% 宽 × 52
│  │  ├─ RepoGrid        col2 row1  62% 宽 × 1fr
│  │  │  └─ RepoTreePanel ×4（2×2 行优先）
│  │  ├─ RepoActionsBar  col2/4 row2（跨中+右）52 高
│  │  └─ InfoPanel       col3 row1  18% 宽 × 1fr
│  └─ FolderView（视图模式 2）
│     ├─ ComponentList   col1 row1
│     ├─ BatchBar        col1 row2
│     ├─ WorkspaceTree   col2 row1（单树，替换 RepoGrid）
│     ├─ RepoActionsBar  col2/4 row2（选中节点可解析为仓时可用）
│     └─ InfoPanel       col3 row1
├─ StatusBar          高 28  L0
└─ OverlayHost
   ├─ Dropdown/Popover   z=100/110  L4
   ├─ ContextMenu        z=110
   ├─ Drawer（右滑）      z=200
   ├─ Modal（居中）       z=210
   └─ Toast              z=300
```

层级规则：L0 菜单/状态栏（chrome，不与内容抢注意力）；L1 标题区 + 左栏导航；L2 四仓树 + 按钮栏（主工作区）；L3 右栏详情；L4 浮层。同一时刻只有一个主视图（工作台/文件夹），浮层不改变底层状态（底层 `aria-hidden="true"` + 滚动锁定）。

### 2.2 1440×900 基线线框（CSS px，dpr=1，CDP viewport A）

```
┌──────────────────────────────────────────────────────────────────────────────────┐ y=0
│ 文件(F)  编辑(E)  视图(V)  操作(O)  工具(T)  帮助(H)                        30px  │ MenuBar
├──────────────────────────────────────────────────────────────────────────────────┤ y=30
│ MBD Git Tool  v0.3 │ MBD_Org │ 1,007 组件 / 4,028 仓 │ ●本地+Gitea   ←可伸缩→   │
│ [🔍 搜索组件/仓库/分支/Tag (Ctrl+K) 280]  [工作台|文件夹]  [刷新] [🔔3] [▤] [⚙]  48px │ TitleBar
├─pad16─┬──────────────────────────────┬──────────────────────────────────┬───────┤ y=78
│       │ 组件 1,007      [排序][筛选][☑] │           中区 849             │ 右 253 │
│  左   │ ┌──────────────────────────┐ │ ┌────────────────────────────┐ ┌──────────────┐ │
│  282  │ │ CF325F_ChanganSigPr…*    │ │ │ ● Design  CF…_Design*  SYNCED│ │ CI 汇总 40px │ │
│       │ │ [∅CI] [▣ 同步(绿)]        │ │ │ 树体 315px（虚拟滚动）       │ │ 仓标题  36   │ │
│       │ │ Design* Impl* MIL SIL    │ │ ├────────────────────────────┤ │ 概览|提交|差异|CI │
│       │ │ main · ⚑v1.2.0     ●3    │ │ │ ● Impl    CF…_Impl      DIRTY│ │ 内容 scroll  │ │
│       │ │ ── 卡片 76px ──          │ │ ├────────────────────────────┤ │              │ │
│       │ │ …（列表纵向滚动）         │ │ │ ● MIL     CF…_MIL     BEHIND │ │ 253×706      │ │
│       │ │                          │ │ ├────────────────────────────┤ │              │ │
│       │ │ 组件列表 body 666px       │ │ │ ● SIL     CF…_SIL    MISSING │ │              │ │
│       │ └──────────────────────────┘ │ └────────────────────────────┘ │              │ │
│       │ ┌─ 批量条 52px ────────────┐ │ 4 面板各 418.5 × 347（行 1 = 706，内缝 12）   │ │
│       │ │[☑选择] SYNC CREATE …     │ │ ┌────────────────────────────────────────┐ │ │
│       │ └──────────────────────────┘ │ │ [PULL] [FETCH] [CHKOUT] [COMMIT] [PUSH] 52 │ │
├───────┴──────────────────────────────┴─┴────────────────────────────────────────┴─┤ y=872
│ 最近操作：PULL 成功 · Impl · 12:03:04 | 本地 12s · 远程 3min | ●Host ●GitWorker | … 28px │ StatusBar
└──────────────────────────────────────────────────────────────────────────────────┘ y=900
```

精确数值（1440×900）：视口 1440，左右 padding 16 → 内容宽 **1408**；列 `20% / 1fr / 18%` + 2×12 gap → 左 **281.6**、中 **849**（树单列 (849-12)/2 = **418.5**）、右 **253.4**；纵向 30+48 → Main 高 **794**，上下 padding 12 → 内容高 **770**，行 1 = 770-12-52 = **706**（树面板 347，含标题 32 → 树体 315），行 2 = **52**，状态栏 28。

### 2.3 1280×800 基线线框（CDP viewport B）

```
┌──────────────────────────────────────────────────────────────────────────┐
│ 菜单栏 30px                                                              │
├──────────────────────────────────────────────────────────────────────────┤
│ 标题区 48px  MBD Git Tool v0.3 │ 1,007/4,028 │ ●本地+Gitea  [🔍200] [工作台|文件夹] … │
├─ 左 249.6 ─┬──────────── 中 749.8 ────────────┬─ 右 224.6 ─┬ 行1=606
│ 组件头 40   │ ┌──── 面板 368.9×297 ────┐ ┌──── 面板 ────┐ │ CI 条 40   │
│ 卡片 76px   │ │ Design（树体 265）      │ │ Impl          │ │ 仓标题 36  │
│ 列表体 566  │ ├─────────────────────────┤ ├───────────────┤ │ Tabs 32    │
│             │ │ MIL                     │ │ SIL           │ │ 内容滚动   │
│ 批量条 52   │ └─────────────────────────┘ └───────────────┘ │            │
├─────────────┴──────────────────────────┴─────────────────┴─ 行2=52 ────┤
│ [PULL] [FETCH] [CHKOUT] [COMMIT] [PUSH]（只跨中+右列，各约 188×40）           │
├──────────────────────────────────────────────────────────────────────────┤
│ 状态栏 28px  最近操作 | 本地/远程新鲜度 | ●Host ●GitWorker | 队列 | CI 0 | 🔔 │
└──────────────────────────────────────────────────────────────────────────┘
```

精确数值（1280×800）：内容宽 1248；左 249.6 / 中 749.8（树列 368.9）/ 右 224.6；Main 694；内容 670；行 1 = 606（树面板 297，树体 265）；行 2 = 52；状态栏 28。右栏信息面板在 1280 下为 224.6，字段标签列缩至 56px，值列 140px。

### 2.4 尺寸 / 间距 / 排版 token 表

| 类别 | 标准（≥1280×800） | 紧凑（1024–1279 或高 720–799） |
|---|---|---|
| 菜单栏 / 标题区 / 按钮行 / 状态栏 | 30 / 48 / 52 / 28 | 26 / 44 / 44 / 24 |
| 页面左右 padding / 上下 padding | 16 / 12 | 8 / 8 |
| 列间距 / 树间距 / 卡片间距 | 12 / 12 / 8 | 8 / 8 / 6 |
| 左栏 / 右栏 | `minmax(248px,20%)` / `minmax(224px,18%)` | 232 / 208 |
| 组件卡片高 / 树行高 / 面板圆角 | 76 / 28 / 8 | 60（两行）/ 26 / 6 |
| 按钮高 / 输入高 / chip 高 | 40 / 30 / 20 | 36 / 28 / 18 |
| 字号：产品名 / 卡片 ID / 树 / 正文 / 状态栏 | 14·600 / 12·600 / 12 / 13 / 11 | 13·600 / 11·600 / 11 / 12 / 10 |
| 间距刻度 | 4 / 8 / 12 / 16 / 24 | 4 / 8 / 12 |
| 边框 / 阴影 | 1px `--border`；仅浮层有 `--shadow` | 同左 |
| 树缩进 / 最大缩进 | 14/层 / 112（超出后每层 +4） | 12/层 / 96 |

排版：字体沿用 `Segoe UI` + `Microsoft YaHei UI`（tokens.css 已定义）；数字/路径/短 SHA 用 `ui-monospace, Consolas`；所有长文本单行省略 + `title` 全量。

### 2.5 滚动与溢出（硬规则）

| 容器 | overflow | 说明 |
|---|---|---|
| `html, body, #app, .app-shell` | `hidden` | 页面级永不滚动；`document.scrollingElement.scrollWidth <= innerWidth` 是验收项 |
| `ComponentList` 列表体 | `auto`（仅纵向） | 滚动条宽 8，虚拟滚动；`overscroll-behavior: contain` |
| `FileTree` 树体（每面板） | `auto`（纵向） | **禁止横向滚动条**：名称省略 + `title`，深缩进封顶 112px |
| `InfoPanel` 内容区 | `auto`（纵向） | 头部（CI 条/仓标题/Tab）`position: sticky`，滚动只在内容区 |
| `BatchBar` / `RepoActionsBar` / `MenuBar` / `TitleBar` / `StatusBar` | `hidden` | 固定，不随内容滚动 |
| `WorkspaceTree`（文件夹视图） | `auto`（纵向） | 同树体规则；根路径超长时中间省略 |
| Drawer / Modal 内容 | `auto` | 浮层内部滚动，页面滚动锁定 |

### 2.6 最小宽度、断点与 DPI / 缩放策略

- **验收基线**：1440×900（A）与 1280×800（B），dpr=1；截图基线存 `docs/images/ui-v3/`。
- **设计最小**：1280×800；**运行硬最小**：1024×720（`MainWindow` `MinWidth=1024`、`MinHeight=720`，单位 DIP）。1024–1279 只保证"可用 + 不溢出"，不作为截图基线。
- 断点：`@media (max-width:1279px)` 紧凑横向；`@media (max-height:799px)` 紧凑纵向；两者可叠加。`@media (min-width:1920px)` 左右栏封顶 320px，中区吸收剩余空间（2×2 面板变大，不出现超宽空列）。
- **DPI**：WPF/WebView2 均为 PerMonitorV2 DPI 感知；所有尺寸用 CSS px，由 WebView2 按 `devicePixelRatio` 自动缩放，**不做手工乘系数、不设 `--force-device-scale-factor`、不做 CSS `zoom`**。以 1440×900 物理窗口为例，125%/150%/175%/200% 下 CSS 视口约为 1152×720 / 960×600 / 823×514 / 720×450，因此：
  - 125% → 1024–1279 紧凑档生效；≤150% 时实际 CSS 宽度可能 <1024，此时**仅显示单列降级**（左栏折叠为抽拉，中区全宽，右栏变为底部抽屉），该形态不进入验收，但不得出现横向溢出或内容被裁切不可达。
  - 应用内不提供独立缩放控件；系统"放大文本"通过 CSS 字号 token 跟随 `prefers-contrast`/系统缩放，不做字体重排。
- **窗口尺寸记忆**：保存上次窗口尺寸/位置/最大化/当前视图（`ui.v3.window`、`ui.v3.view`），恢复时若小于最小尺寸则夹到 1280×800。

### 2.7 菜单栏 / 标题栏 / 状态栏的内容规格

菜单栏（30px，高亮当前打开的浮层；Alt 聚焦，方向键切换）：

| 菜单 | 项（含禁用项） |
|---|---|
| 文件(F) | 打开工作区…、重新扫描(F5)、认证与凭据…、退出 |
| 编辑(E) | 搜索(Ctrl+K)、复制所选路径、全选筛选结果(Ctrl+A) |
| 视图(V) | 工作台(Ctrl+1)、文件夹视图(Ctrl+2)、显示已忽略文件、紧凑密度、主题（跟随系统/浅色/深色） |
| 操作(O) | 单仓 PULL/FETCH/CHKOUT/COMMIT/PUSH（与底部同源，禁用态同步，快捷键 Alt+1…5）…、组件 SYNC…、组件 CREATE…、批量 Clone…、批量 Delete…、恢复…、任务中心 |
| 工具(T) | **CI 流水线（CI-1）**、未绑定仓库、日志与诊断、设置(Ctrl+,) |
| 帮助(H) | 关于、快捷键、检查更新（v1.1，禁用占位） |

标题栏（48px，一行，`flex-wrap:nowrap`，任何元素不换行；窄屏按"省略文本 → 隐藏组织 → 隐藏计数（保留 title）"降级）：
`[产品名 14/650] [版本胶囊 v0.3] [组织 MBD_Org] [1,007 组件 / 4,028 仓] [数据源胶囊 ●本地+Gitea] [扫描中胶囊] ←spacer→ [🔍 搜索 240–320] [工作台|文件夹 seg] [刷新] [🔔 通知 (badge)] [▤ 任务] [⚙ 设置]`。
组件数/仓数取 `snapshot.counts`，禁止写死；`source=mock` 时数据源胶囊常驻"Mock 数据"且不可隐藏。

状态栏（28px，11px，左起）：
1. **最近操作**（弹性，`flex:1`，省略）：`✓ PULL 成功 · Impl · 12:03:04` / `✕ COMMIT 失败 · E_GIT_… · 12:04:10` / `就绪`；
2. 数据新鲜度：`本地 12s · 远程 3min`（分源，任一超 TTL 变 warning 色并加 `⏱`）；
3. 进程健康：`●Host ●GitWorker`（2 灯，hover 明细列出 RemoteSync/LocalScan/StatusCalc/JobRunner 子状态，FR-OPS-05）；
4. 操作队列 `队列 2`；
5. **CI 队列（CI-5）**：`CI 0`，v1 恒 0，可点击打开 CI 页；
6. 通知 `🔔 3`，可点击打开通知抽屉；
7. 网络 `在线/离线`（离线时 warning 色，远程状态强制 UNKNOWN 提示）。

---

## 3. 组件卡片列表

### 3.1 卡片信息层级（三行 76px，标准档）

```
┌─────────────────────────────────────────────────────────────┐
│▌ CF325F_ChanganSigProcgBascFct*        [∅CI] [⇄ 分叉]        │  L1 20px：ID* + CI 槽位(18×18) + 聚合状态 chip
│▌ Design*  Impl*  MIL  SIL                                    │  L2 20px：4 仓章（状态色文字+8px 圆点，dirty 加 *）
│▌ main  ⑂混合  ⚑ v1.2.0                              ●3      │  L3 16px：分支(Impl 优先) + Tag + 变更徽标
└─────────────────────────────────────────────────────────────┘
  ▏4px 左轨 = 聚合状态 tone（唯一整卡状态色通道之二，之二为 55% tint 底）
```

| 元素 | 数据来源（现有 / 新增） | 规则 |
|---|---|---|
| 组件 ID | `component.componentId` | 单行省略，`title` 全名；**任意仓有未提交改动则尾部加 `*`**（参考图 3.3；tooltip `"3 处未提交改动"`） |
| CI 槽位 | `component.ciAggregate?.worstState`（新增，v1 恒 `NONE`） | 固定 18×18，不换行；`NONE/UNKNOWN` 用中性虚线/斜纹，**不参与卡片底色与排序**（CI-2/CI-10） |
| 聚合状态 chip | `component.aggregateState` + §11.2 映射 | 图标 + 中文短文案（如 `⇄ 分叉`、`↓ 落后`、`∅ 缺失 1/4`、`✕ 绑定异常`），颜色取完整 token |
| 4 仓章 | `component.repos[]` 按 `repoType` 补齐 DESIGN/IMPL/MIL/SIL 四槽 | 每章：`Design`/`Impl`/`MIL`/`SIL` + 8px 状态圆点；仓不存在=虚线空心；dirty 加 `*`；`title="Design：BEHIND（main，3 变更）"`；`data-repo-type="DESIGN"` |
| 分支 | `component.branch`（Impl 优先；缺 Impl 取第一个仓） | 单行省略；`component.mixedBranch=true` 显示 `⑂混合` 角标，`title` 列四仓分支对照 |
| Tag | `component.tag`（无则整段不渲染） | `⚑ v1.2.0`；TAG_DRIFT 时 `⚑ 基线不一致` warning 色 |
| 变更徽标 | Σ`repo.dirtyCount + repo.untrackedCount`（前端聚合） | `●3` 右对齐；>99 显示 `99+`；title 分类"已修改 2 / 未跟踪 1"；**不用 `*` 计数**（`*` 只做后缀，见裁决 R-03） |
| 选中态 | — | 4px 左轨加宽为 5px + 2px `--focus` 内描边 + tint 加深到 70%；`aria-selected="true"` |

卡片高度固定 76，行高 20/20/16，上下 padding 7，行间 3；紧凑档 60（隐藏 L3，Tag/分支并入 L2 尾部），列表体 padding 8，卡片间距 8。

### 3.2 排序 / 筛选 / 搜索（列表头 40px）

`组件 {n}  [⇅ 排序] [⏳ 筛选] [☑ 选择]`，右侧可显示当前筛选 chip（有筛选时追加一行 24px chip 行，列表体相应缩短）。

- 默认排序 = `severityRank` 升序 → 组件 ID 升序（FR-BOARD-01 语义保留，布局改为卡片）。
- 排序可选：严重度 / 组件 ID / 最近活动（`component.lastActivityAt`，缺省用 `snapshot.generatedAt`）。
- 筛选 facet：聚合状态（多选）、单仓主状态（多选）、RepoType、分支、Tag、有未提交改动、有未推送提交、**CI 状态（禁用占位，CI-7，tooltip「CI 集成将在后续版本开放」）**；筛选按钮显示激活数量。
- 搜索在标题栏（全局），匹配组件 ID / 仓库名 / 分支 / Tag / HEAD 短 SHA；中文 IME 用 `compositionend` 触发，组合期不查询；`Ctrl+K` 聚焦、`Esc` 清空。
- 4000+ 组件：列表**必须虚拟滚动**（行高 76/60 固定，overscan 6）；`filteredRows` 只存 id 数组，卡片组件按需渲染。

### 3.3 交互态

| 态 | 表现 |
|---|---|
| hover | 底色在 tint 上叠 4% surface-2；L3 右侧浮现快捷动作 `[SYNC]` `[CREATE]`（各 24×20，绝对定位于卡片右下，不改变行高；分支文本相应省略，不引发面板回流） |
| focus-visible | 2px `--focus` 外描边 offset 2（键盘可达，Tab 顺序：列表头 → 卡片 → 按钮） |
| 单击 | 选中组件 → 4 树面板与右栏全部切到该组件；默认选中仓 = `IMPL`（无 Impl 时取第一个可用仓），树焦点框落到 Impl 面板 |
| 双击 | 与单击同义（不打开外部程序） |
| 多选 | 选择模式下列表头变 `已选 n / 1,007  [全选结果] [清除] [✕]`；卡片左侧 18px checkbox；选中卡加 `--st-info-bd` 边框 + info tint 24%；`Ctrl+单击` 追加，`Shift+单击` 区间 |
| 右键 | 见 3.4；右键不改变当前选中（除非点在未选中卡上则先选中） |
| 空/加载/错误 | 空：`未匹配到组件` + 清除筛选；加载：8 个 76px 骨架卡；错误：inline 重试条（不清空已有列表，FR-BOARD-05 "不整表闪白"）；过期：列表头出现 `⏱ 数据过期` pill，卡片状态斜纹由 token 承担 |

### 3.4 右键菜单（`ContextMenu`，240px）

| 分组 | 项 | 启用条件 |
|---|---|---|
| 打开 | 在工作台打开 / 在文件夹视图定位 | 恒可用 |
| 写操作 | SYNC…（四仓，远程覆盖本地） | 组件完整且非 BLOCKED_LOCAL；需 dry-run + 二次确认 |
| | CREATE…（四仓建同名分支） | 组件完整；先展示 dry-run 预览表 |
| | Clone 缺失仓… | 存在 MISSING/UNBOUND 仓 |
| | Delete…（仅本地，软删到 `.trash`） | 恒显示；执行前输入组件 ID + 确认；文案固定"远程仓库不受影响" |
| 维护 | 修复绑定…/纳入管理/移出管理 | INVALID、未绑定、已纳入场景 |
| 复制 | 复制组件 ID / 复制路径 / 复制分支 | 恒可用 |
| 系统 | 在资源管理器中打开组件目录 | 组件目录存在 |

菜单项 32px 高，快捷键右对齐；危险项 `--st-danger-fg`；`Esc`/点击外部关闭；键盘 ↑↓/Enter 可达。

### 3.5 批量操作入口（4 个落点，零新增工具栏）

1. **左栏底部批量条（主入口，52px，与底部按钮行同高）**：`[☑ 选择]` 常驻；进入选择模式后变为 `已选 3 · SYNC · CREATE · Clone · ⋯`（Delete/恢复/纳入/移出收进 `⋯`），右侧 `✕` 退出。正好填满参考图左栏下方的空白，**不破坏参考图任何区域**。
2. **卡片 hover 快捷**：`SYNC` `CREATE` 两个小按钮，浮层出现，不占固定布局。
3. **卡片右键菜单**：全量命令（含 Delete/Clone/修复）。
4. **菜单栏「操作」**：键盘与发现性入口，与批量条同源命令、同一启用矩阵。

批量语义：默认只对**选中组件集合**执行（不是当前组件）；`全选筛选结果` 支持；**选中 >10 个组件或 >40 个仓时必须二次确认**（FR-BOARD-04），确认框展示 组件数/仓数/操作/将丢失内容摘要；提交后进入任务中心（逐仓进度、取消、重试失败项）；破坏性操作额外要求输入组件 ID（单个时）或固定文案确认（批量时）。

### 3.6 卡片状态色映射（通道 A，与 §11.2 一致）

| 聚合状态 | tone | 图标+短文案 | 卡片 tint | 左轨 |
|---|---|---|---|---|
| OP_PARTIAL_FAILED / BINDING_ERROR / BLOCKED_LOCAL | danger | `! 部分失败` / `✕ 绑定异常` / `✕ 阻塞` | danger 55% | danger |
| UNKNOWN | hatched | `⏱ 未知`（斜纹） | neutral 55% | neutral |
| MISSING / EMPTY | neutral（虚线） | `∅ 缺失 n/4` / `∅ 空仓` | neutral 40% | neutral |
| TAG_DRIFT | warning | `⚑ 基线不一致` | warning 55% | warning |
| DIVERGED | purple | `⇄ 分叉` | purple 55% | purple |
| BEHIND | warning | `↓ 落后 n` | warning 55% | warning |
| AHEAD | info | `↑ 超前 n` | info 55% | info |
| DETACHED | cyan | `⚑ 基线 tag` / `⌁ 游离` | cyan 55% | cyan |
| MIXED_BRANCH | cyan | `⑂ 分支不一致` | cyan 45% | cyan |
| NEW_BRANCH | cyan | `⑂ 仅本地` | cyan 45% | cyan |
| DIRTY | success | `● 未提交` | success 45% | success |
| SYNCED | success | `✓ 同步` | success 40% | success |

> tint 强度是唯一允许的"整卡着色"，且必须通过 4.5:1 对比度门禁；不达标时自动降为 40% 混合（实现期由 token 计算，禁止硬编码 hex）。

---

## 4. 四仓文件树（中区 2×2）

### 4.1 面板结构与标题

```
┌──────────────────────────────────────────────────────────┐ 418.5 × 347 (1440)
│ ● Design  CF325F_ChanganSigProcgBascFct_Design*  [SYNCED] [∅CI] [⟳] │ 32px 标题
├──────────────────────────────────────────────────────────┤
│ ▾ src                                                    │ 28px/行
│   ▾ control                                              │
│     • main.m                                   M         │
│   • README.md                                            │
│ ▸ docs                                                   │
│ …（独立纵向滚动，无横向滚动条）                             │
└──────────────────────────────────────────────────────────┘
```

- 四面板固定顺序 **Design / Impl / MIL / SIL**（行优先：左→右、上→下），始终渲染 4 块；某仓不在快照中时该面板显示空态（§4.3），**不得删除面板导致布局跳动**。
- 标题 32px：状态圆点(8) + 仓类型（DESIGN/IMPL/MIL/SIL，11px/700）+ 仓名（12px，省略）+ **`*` 变更后缀** + 状态 chip（20px）+ CI 槽位（CI-2 同款，18×18）+ 刷新按钮（22×22，hover 显示）；`title` = 仓全名 + 状态文案；`data-testid="repo-tree-panel"` `data-repo-type="DESIGN"`。
- 焦点面板：2px `--focus` 顶边（`border-top`）+ 标题底边加粗；点击面板任意节点即获得焦点，并成为右栏与底部按钮的作用对象。默认焦点 = Impl（无 Impl 则第一个存在仓）。
- 每面板独立维护 `expandedPaths` / `loadingPaths` / 滚动位置；切换组件后保留滚动位置到 `Map<componentId:repoType, scrollTop>`（最近 20 组，LRU）。

### 4.2 节点渲染与交互

- 行高 28（紧凑 26），结构：缩进 + 展开箭头(16) + 类型图标(16) + 名称(12px，省略) + git 状态字母(右对齐，18px)。
- 类型图标：目录 `▸/▾`；文件按扩展名分组（`.m/.c/.h` 代码、`.md/.txt` 文档、`.json/.xml/.yaml/.yml` 配置、`.slx/.mdl` 模型、二进制/未知），**不引入图标库**，用 CSS/字符实现。
- 目录展开/折叠：单击箭头（16×16 命中区可放宽到 20）；`ArrowRight/ArrowLeft`；`Enter` 在目录上=展开（与双击打开文件夹区分）。展开时懒加载（`getRepoTree(path)`），加载中插入 28px `加载中…` 占位行。
- 单击节点：仅选中（文件=选中文件、目录=选中目录），右栏切到"节点信息"；**不改变已选仓上下文**（除非节点属于另一面板——面板点击本身已切换焦点）。
- 双击文件：按 FR-DETAIL-03 —— 白名单扩展名（`.m .c .h .slx .mdl .json .xml .yaml .yml .txt .md .csv`）→ `openPath(mode:"open")` 用默认程序打开；白名单外/危险扩展名（`.exe .bat .cmd .ps1 .lnk .msi .scr`）→ 弹确认框（列出完整路径/扩展名/风险），确认后调用（Host 二次校验）；路径必须位于 `workspaceRoot` 内。
- 双击目录：`openPath(mode:"reveal")` 用资源管理器打开（参考图 4 明确"双击可用默认程序打开文件或者文件夹"）；不翻转展开态（避免与单击箭头冲突）。
- `Enter` = 双击语义；`Space` = 展开/折叠；`↑↓` 移动选中；`Home/End` 首尾；`F2` 无；右键菜单：在资源管理器中打开、复制路径、打开所在目录、复制文件名。
- 选中行：`--st-info-bg` 40% 底 + 2px 左 `--focus`；hover：`--surface-2` 55%；`聚焦但未选中` 无底色。

### 4.3 空仓 / MISSING / UNKNOWN / INVALID / 错误态

| 面板态 | 触发 | 树体内容（居中 EmptyState，高≥160） | 主按钮 |
|---|---|---|---|
| MISSING | `repo.state=MISSING` 或快照无该仓且远程存在 | `∅ 未克隆` + 仓全名 + 期望路径（可选） | `Clone`（组件级，提交后进任务中心） |
| EMPTY | `EMPTY` | `∅ 空仓库（无首个提交）` + 说明 | `初始化并首推…`（M2 占位，toast 说明） |
| UNKNOWN | `UNKNOWN` / `isStale` | `⏱ 状态未知（数据 3min 前）` + 原因 `errorCode` | `重试扫描` |
| INVALID | `INVALID` | `✕ 仓库绑定异常` + 路径 + 原因 | `修复绑定…` |
| 缺仓（4 槽中无此类型） | 组件仓数 <4 且远程无该仓 | `∅ 预留槽位（组件仓数 2/4）` + `BINDING_ERROR(INCOMPLETE)` 说明 | `重新对账` |
| 树加载失败 | `getRepoTree` 报错 | `无法读取目录` + errorCode + 路径 | `重试` |
| 空目录 | 目录存在但无子项 | 行内 `（空）` 占位（灰色，不可交互） | — |

### 4.4 树数据接口（Host 只读，新增）

请求（沿用现有桥 `{id, method, params}` 信封，camelCase）：

```json
// UI → Host: getRepoTree
{ "id": "uuid", "method": "getRepoTree",
  "params": { "componentId": "CF325F_ChanganSigProcgBascFct", "repoType": "IMPL", "path": "src/control", "depth": 1 } }
```

```json
// Host → UI
{ "id": "uuid", "ok": true, "data": {
  "componentId": "CF325F_ChanganSigProcgBascFct",
  "repoType": "IMPL",
  "repoName": "CF325F_ChanganSigProcgBascFct_Impl",
  "rootPath": "D:\\Work\\MBD\\CF325F_ChanganSigProcgBascFct_Impl",
  "path": "src/control",
  "branch": "main",
  "headSha": "3f2a9c1d4b5e6f7a8b9c0d1e2f3a4b5c6d7e8f90",
  "state": "DIRTY",
  "dirtyCount": 3,
  "compareRef": "refs/remotes/origin/main",
  "fetchedAt": "2026-11-14T02:20:44Z",
  "entries": [
    { "path": "src/control/main.m", "name": "main.m", "kind": "FILE", "hasChildren": false,
      "size": 20480, "mtime": "2026-11-13T09:12:00Z", "gitStatus": "MODIFIED",
      "submodule": false, "ignored": false, "children": null, "error": null }
  ],
  "truncated": false,
  "totalEntries": 128,
  "error": null
} }
```

| 字段 | 类型 | UI 用途 |
|---|---|---|
| `path` | string | 相对仓根、`/` 分隔、根为空串；拼接/去重/展开集合的键 |
| `name` | string | 显示名（不参与排序键以外的解析） |
| `kind` | `"DIR"` / `"FILE"` | 图标、双击语义、展开能力 |
| `hasChildren` | bool | 是否显示箭头；根目录也为 true |
| `children` | `TreeNode[]?` | `depth>=1` 时内联；`null`=未加载（点击再请求） |
| `size` / `mtime` | number / ISO string | 详情；`DIR` 可为 null |
| `gitStatus` | `"NONE"\|"MODIFIED"\|"ADDED"\|"DELETED"\|"UNTRACKED"\|"CONFLICT"` | 行尾字母 M/A/D/U/!/…，颜色走 token |
| `submodule` | bool | 显示"未纳管（submodule）"标记（AC-DETAIL-06） |
| `ignored` | bool | "显示已忽略文件"关闭时过滤；开启时灰显 |
| `truncated` / `totalEntries` | bool / int | 大目录截断提示行 |
| `error` | string? | 单节点读取失败（不拖垮整树） |

**缓存与失效**：按 `componentId/repoType/path` 缓存 30s；组件/仓选中变化不重复请求已缓存路径；`refresh`、任何写操作完成、FileSystemWatcher 事件（M2）→ 清空对应仓缓存。

### 4.5 长路径 / 大目录 / 虚拟滚动

- 大目录：单目录最多返回 **2000** 项 + `truncated:true`；树尾追加 28px 警告行"仅显示前 2000 项（打开文件夹查看全部）"；用户可点"在资源管理器中打开"。
- 长路径：名称 `text-overflow:ellipsis`，`title` 放全相对路径；缩进 14px/层，超过 8 层改为每层 +4px（上限 112px）；面板内不出现横向滚动条。
- 虚拟滚动：把"展开中的可见节点"扁平化为数组 `visibleRows`，只渲染 `[scrollTop/28 - 6, scrollTop/28 + 视口行数 + 6]`；单面板 DOM 行数 < 80；`scrollToIndex` 供键盘使用；展开/折叠只改动数组与高度，不重建整个树。
- 性能预算：单面板 10,000 节点展开 + 滚动 ≥55fps；`getRepoTree` 单次 P95 ≤300ms（2000 项）；超过 1s 显示骨架行而不是阻塞。

### 4.6 打开文件的安全规则（与 SRS FR-SAFE-05 / FR-DETAIL-03 对齐）

1. UI 只传**相对路径**；Host 侧 `Path.GetFullPath` + 归一化后必须 `StartsWith(workspaceRoot + separator)`，拒绝 `..`、绝对路径、UNC、盘符根、大小写冲突。
2. 解析 reparse point/junction/symlink 后仍须在根内；reparse 节点在树中打 `⟲` 标记，双击仅"打开所在目录"，不默认直开。
3. 扩展名白名单直开；危险扩展名/无扩展名/可执行 → UI 确认框（显示路径、扩展名、来源仓）→ 携带 `confirm:true` 调用；Host 仍校验，缺失即 `E_CONFIRM_REQUIRED`。
4. `.git` 目录不显示、不可打开；`.trash` 默认隐藏（"显示已忽略文件"也不显示，仅恢复流程可见）。
5. 失败统一 Toast：`无法打开：<错误码> <中文文案>`，同时状态栏最近操作记录错误。

---

## 5. 右侧信息面板

### 5.1 结构与尺寸

```
┌──────────────────────────────┐ 253.4 (1440) / 224.6 (1280) / 208 (紧凑)
│ CI 汇总  ●0 ✕0 ◐0 ○0  未接入 │ 40px 组件级（CI-3，跨四仓）
├──────────────────────────────┤
│ Impl  CF…_Impl*   [DIRTY]    │ 36px 选中仓标题（可复制路径图标）
├──────────────────────────────┤
│ [概览] [提交] [差异] [CI]    │ 32px tablist（CI-4 = 第 4 位）
├──────────────────────────────┤
│ 分支        main             │ ↑ 内容区唯一滚动容器
│ Tag         v1.2.0           │ 字段行 26–30px
│ HEAD        3f2a9c1 (复制)    │
│ 对比基准    refs/remotes/origin/main │
│ 超前/落后   ↑2 / ↓3          │
│ 未提交      已跟踪 3 · 未跟踪 1 · stash 0 │
│ 状态        [DIRTY] [shallow] │
│ 本地路径    D:\Work\… (复制)  │
│ 状态计算    12:02:31 · local  │
│ 最近提交    5 行（短sha·标题·作者·相对时间）│
│ …（概览底部"查看全部"→提交 Tab）│
└──────────────────────────────┘
```

- 宽度：`clamp(224px, 18%, 320px)`；最小 224，低于 224 时字段行改两行布局（标签上、值下）。
- 头部（CI 条 + 仓标题 + Tabs）`position: sticky; top:0`，内容区 `overflow:auto`；面板整体不滚动。
- 所有可复制值（HEAD/路径/组件 ID/分支）hover 出现复制图标，点击 Toast `已复制`。
- 空态：未选中仓/组件缺仓 → `未选择仓库` + 说明 + 重试/Clone；选中文件（文件夹视图）→ 文件信息视图（见 5.4）。

### 5.2 四个 Tab 的内容规格

| Tab | 内容 | 数据接口 | 本期状态 |
|---|---|---|---|
| 概览 | 上表字段 + flags 章 + 最近提交 5 条 | `getRepoDetail`（含 `repo` + `commits.items` 前 5） | 本期实现 |
| 提交 | 虚拟列表（行高 30，分页 100，`loadMore`），列：短 SHA / 标题 / 作者 / 相对时间 / refs 章；点击复制 SHA；右键菜单（复制 SHA/复制标题） | `getCommits(skip,take)` | 本期实现；10,000 提交首屏 ≤500ms |
| 差异 | 空态："差异只读视图将在后续版本接入" + 契约字段占位（files/chunks/addition/deletion）；不出现裸 TODO | —（P1 后续） | 占位 |
| CI | 空态 + 字段占位表（`CiLatest` 全字段，值 `—`）+ "CI 未接入（v1 占位）" + `ci.enabled=false` | Mock `getCiStatus`/快照 `repo.ci` | 占位（CI-4） |

### 5.3 字段与数据来源

| 字段 | 来源 | 展示规则 |
|---|---|---|
| 仓全名 | `repo.repoName`（新增；缺省用 `componentId_Type`） | 单行省略 + title |
| 主状态 | `repo.state` + §11.2 | chip（图标+中文） |
| 当前分支 / Tag | `repo.branch` / `repo.tag`（新增） | DETACHED_TAG 显示 `⚑ tag`；无分支显示 `—` |
| HEAD | `repo.headSha` | 短 7 位等宽，`title` 全 SHA + 复制 |
| 对比基准 | `repo.compareRef`（新增） | `refs/remotes/origin/main`；缺省显示"未配置 upstream" |
| ahead / behind | `repo.ahead/behind` | `↑2 / ↓3`；关系类状态 UNKNOWN 时显示 `—` 并注明"远程数据过期" |
| dirty | `repo.dirtyCount/untrackedCount`（+`stashCount` 新增） | `已跟踪 3 · 未跟踪 1 · stash 0`；为 0 显示"无" |
| 正交标记 | `repo.flags[]` | chip：dirty/untracked/stashed/shallow/lfs_missing/reparse/busy/remote_gone/tag_moved；红色仅阻断类 |
| 本地路径 | `repo.localPath` | 单行省略 + 复制；`isLocal=false` 显示"MISSING" |
| 新鲜度 | `repo.calcAt` / `repo.dataSource` / `repo.isStale`（新增） | `12:02:31 · local`；stale 加 `⏱` warning |
| 错误码 | `repo.errorCode`（新增） | `E_GIT_…` + 中文文案（FR-OPS-09） |
| 最近提交 | `repo.lastCommit`（新增）或 `getRepoDetail.commits` | 5 行；空仓/缺失显示空态 |
| 组件 CI 汇总 | `component.ciAggregate`（新增，v1 Mock） | CI-3 条：`●0 ✕0 ◐0 ○0` + 最近失败仓 + "未接入"；**不染色** |
| 构建是否成功 | 由 CI 状态映射（见 5.4） | 仅在 CI Tab / CI 条；不得影响卡片与仓库状态 |

### 5.4 构建 / CI 状态（占位，独立通道）

| CI 状态 | 文案（右侧面板） | 视觉 |
|---|---|---|
| PASS | `构建成功`（Mock 时才可能出现，v1 默认 NONE） | success 圆点（CI 通道） |
| FAIL | `构建失败` + 最近失败阶段/构建号 | danger 圆点（CI 通道） |
| RUNNING | `构建进行中` + 开始时间 | info 半环 + 进行中 |
| QUEUED | `构建排队中` | neutral 空心圆 |
| NONE | `CI 未接入（v1 占位）` | neutral 虚线 ∅ |
| UNKNOWN | `构建结果未知` | hatched ? |

规则：CI 仅用圆点/pill 形状表达，**永不作为卡片底色、不进入组件聚合、不参与严重度排序**（CI-10）；`ci.enabled=false` 时全部为 `NONE`；"本地构建"v1 无数据源，统一显示 `本地构建：未接入`（与 CI 区分两行）。

### 5.5 选中文件信息（文件夹视图复用）

字段：文件名 / 相对路径 / 扩展名 / 大小（人类可读）/ 修改时间 / git 状态 / 是否在打开白名单 / 所属仓（可点击跳回工作台选中该仓）；按钮：`用默认程序打开`（白名单）、`打开所在目录`、`复制路径`。

---

## 6. 底部按钮栏

### 6.1 布局与作用域

- 位置：Main 行 2，`grid-column: 2 / -1`（中区+右栏下方），高 52，顶部无边框（与树区留 12 缝），按钮 40px 高。
- 五键顺序固定：**PULL / FETCH / CHKOUT / COMMIT / PUSH**；`flex:1 1 0; min-width:112px; max-width:220px; gap:12px`；1440 下每键约 213px，1280 下约 188px。
- **作用域永远是"当前选中的单个仓"**（用户硬要求：不同时对四仓发起）。按钮组带 `aria-label="单仓操作：Impl"`；选中仓切换即时更新 `data-action-scope`；未选中任何仓 → 五键全部禁用，`title="未选择仓库"`。
- 组件级操作不在这一行：见 §3.5 的 4 个批量入口。

### 6.2 可用性矩阵（v1 实现表，源自 SRS §7.6）

`✓=可用　✗=禁用　⚠=可用但需二次确认`

| 单仓主状态 | PULL(ff-only) | FETCH | CHKOUT(分支/Tag) | COMMIT | PUSH |
|---|---|---|---|---|---|
| SYNCED | ✓ | ✓ | ✓ | ✓ | ✓ |
| DIRTY | ✗（先提交/stash） | ✓ | ⚠（stash 或确认） | ✓ | ✓ |
| AHEAD | ✗（需先推送/手动） | ✓ | ⚠ | ✗ | ✓ |
| BEHIND | ✓ | ✓ | ⚠ | ✗ | ✗ |
| DIVERGED | ✗（需手动 merge/rebase） | ✓ | ⚠ | ✗ | ✗ |
| DETACHED_TAG / DETACHED_COMMIT | ✗ | ✓ | ⚠（需确认） | ✗ | ✗ |
| NO_UPSTREAM | ✗ | ✓ | ✗ | ✗ | ✓（发布分支） |
| EMPTY | ✗ | ✓ | ⚠ | ✓ | ✓（首推建 upstream） |
| UNMERGED | ✗ | ✓ | ✗ | ✗ | ✗ |
| IN_PROGRESS | ✗ | ✗ | ✗ | ✗ | ✗ |
| INVALID | ✗ | ✗ | ✗ | ✗ | ✗ |
| MISSING | ✗（面板显示 Clone） | ✗ | ✗ | ✗ | ✗ |
| UNKNOWN | ✗ | ✓ | ✗ | ✗ | ✗ |

禁用时 `title` / 状态栏给出**具体原因文案**（不写"不可用"）：
- PULL：`DIRTY：需先提交或 stash`；`DIVERGED：需手动 Merge/Rebase`；`UNKNOWN：关系数据过期，先 FETCH`
- PUSH：`DETACHED：请先基于当前 HEAD 创建分支`；`BEHIND：先 PULL`；`UNMERGED：存在未解决冲突`
- COMMIT：`UNMERGED：存在未解决冲突`；`IN_PROGRESS：合并/变基未完成`
- CHKOUT：`IN_PROGRESS：操作未完成`；`MISSING：仓库未克隆`
- FETCH：`INVALID：仓库绑定异常`；`IN_PROGRESS：操作未完成`

### 6.3 状态机（禁用 / 进行中 / 成功 / 失败）

| 态 | 表现 | 数据 |
|---|---|---|
| 空闲可用 | 边框按钮，hover `--surface-2` | 由矩阵计算 |
| 空闲禁用 | `opacity:.45`、`cursor:not-allowed`、`aria-disabled=true`、title 原因 | 由矩阵计算 |
| 需确认 | 点击打开确认对话框（CHKOUT dirty、COMMIT 消息框、PUSH 无 upstream 的"发布分支"提示） | 本地判定 |
| 进行中 | 该键 spinner + `PULL…`；其余 4 键禁用；按钮组 `aria-busy=true`；状态栏"PULL 执行中 · Impl · 12:03:04" | `runRepoAction` 返回 `jobId` |
| 成功 | Toast `✓ PULL 成功`（2.4s）+ 状态栏最近操作 + ≤2s 内刷新该仓状态（优先 `refresh` 单仓，退化为 `getSnapshot`） | `JOB_UPDATED`/轮询 |
| 失败 | Toast（6s，含错误码、`重试`、`详情`）+ 状态栏 danger 文案 + 通知中心 +1 + 任务中心记录；**按钮恢复可用**（不锁死） | error code |
| 超时 | Toast `等待 Host 超时`，状态栏警示，不猜测成功（SRS FR-SAFE-04） | 45s 上限（沿用现有 SNAPSHOT_TIMEOUT_MS） |

- 确认对话框：COMMIT 弹窗含 `staged 文件列表`（只读）+ 提交信息输入（必填，≥1 字符）+ `提交`（危险色仅当有 unmerged 时禁用）；CHKOUT 弹窗含 分支/Tag 搜索选择 + dirty 仓的 `stash -u` 勾选 + 影响说明；PUSH 到无 upstream 时提示"将发布分支并建立 upstream"。
- 同一仓写操作串行；与该仓批量任务互斥（批量进行中时五键禁用，title `该组件有批量任务进行中`）；不同仓可并行（队列在任务中心展示）。

### 6.4 与组件级批量操作的关系

| 维度 | 底部五键（单仓） | 组件/批量操作 |
|---|---|---|
| 作用对象 | 当前选中 1 仓 | 选中组件集合（默认当前组件）或其 4 仓 |
| 入口 | 底部按钮行 | 批量条 / 卡片 hover / 右键 / 菜单「操作」 |
| 确认 | 常规操作免确认；CHKOUT dirty、COMMIT、发布分支例外 | **>10 组件或 >40 仓强制二次确认**；破坏性操作必确认 |
| 任务 | 进同一任务队列，任务中心可见（单仓 job） | 批量 job，逐仓进度 |
| 并发 | 与同仓写操作互斥 | 与同仓五键互斥；不同仓可并行 |
| 失败 | Toast + 状态栏 + 通知 | 任务中心逐仓明细；组件聚合 `OP_PARTIAL_FAILED` + 重试入口 |
| 不做 | 不对四仓同时发起（用户明确要求） | 不隐式 merge；Pull 默认 `--ff-only` |

### 6.5 Toast / 任务中心入口

- Toast：底部居中，`bottom: calc(var(--layout-status-h) + var(--layout-actions-h) + 12px)`（1440 下约 92px），最大宽 480，最多 3 条堆叠，成功/信息 2.4s、错误 6s（带 `重试`/`详情`），`role="status"` `data-testid="toast"`。
- 任务中心：右侧抽屉 480px（列表：进行中/历史、逐仓进度条、取消、重试失败项、错误码；空态文案沿用"批量操作将在 M2 接入"）；入口 = 菜单「工具→任务中心」/ 标题栏 ▤ / 状态栏「队列」段 / 批量提交后自动打开。
- 每次操作结束写通知（失败）与最近操作（状态栏），不在主界面弹模态（避免打断浏览）。

---

## 7. 文件夹视图

### 7.1 两种候选与裁决

| 候选 | 定义 | 数据可行性 | 交互一致性 | 结论 |
|---|---|---|---|---|
| **A. 工作区目录树视图（推荐）** | 中区由 2×2 换成**单棵树**：工作区根 → 组件虚拟组 → 4 个仓实际目录 → 文件；左栏/右栏/底部按钮/批量条保留 | 需新增 `getWorkspaceTree`（Host 遍历根目录 + `RepoNameParser` 分组 + 与快照状态合并）；工作区是**扁平布局**（根下直接是 `X_Design/X_Impl/...`），组件目录是**虚拟分组**，必须在 Host 侧分组，UI 不解析目录名 | 与工作台共用选中模型：文件节点→右栏文件信息；仓节点→右栏仓详情+底部按钮可用；组件节点→右栏组件概览 | **采用**。真正满足"文件夹视图"的诉求（按磁盘目录浏览），且不改参考图的左/右/底部几何 |
| B. 四仓树另一种排布 | 中区改成"单棵合并树"：根下 4 个分支 Design/Impl/MIL/SIL，复用 `getRepoTree`；或四列并排 | 零新增 Host 接口，最快落地 | 语义仍是"当前组件的四仓"，与工作台重复，很多节点是同一批仓；看不到磁盘上的其他目录/未纳管内容 | **不作为文件夹视图**；降级为工作台 P2 布局选项（`视图→工作台布局：2×2 / 合并树`），本期不实现 |

裁决：**文件夹视图 = 候选 A**，与工作台是同一主界面的两种"视图模式"，不是新页面、不是新窗口；两个模式共享左栏、右栏、底部按钮、批量条、标题栏与状态栏，仅中区容器不同。切换时保留 `selectedComponentId` / `selectedRepoType` / `selectedPath`，工作台侧保存树滚动位置，切回时恢复。

### 7.2 布局与结构（文件夹视图，1440）

```
┌ MenuBar / TitleBar（不变，TitleBar 的 [工作台|文件夹] seg 高亮"文件夹"） ┐
├─ 左栏 20%（组件卡片，不变） ─┬─ 中区：WorkspaceTree 单树 ───────────────┬─ 右栏 18% ─┤
│ 卡片/批量条                  │ ┌ 工作区  D:\Work\MBD      [🔍筛选] [⟳] ┐ │ 依赖节点类型： │
│                             │ │ ▾ CF325F_ChanganSigProcgBascFct  [BEHIND] │ │ 组件概览/CI   │
│                             │ │   ▾ Design  CF…_Design*   ↓ 落后 2      │ │ 仓详情/4 Tab  │
│                             │ │     ▾ src/ …  • main.m  M             │ │ 文件信息      │
│                             │ │   ▸ Impl  CF…_Impl*  ● 未提交           │ │              │
│                             │ │   ▸ MIL / ▸ SIL                        │ │              │
│                             │ │ ▾ CF900A_TractionCtrl  [⇄ 分叉]         │ │              │
│                             │ │ 未纳管（2 个目录）…                     │ │              │
│                             │ └────────────────────────────────────────┘ │              │
├─────────────────────────────┴───────────────────────────────────────────┴──────────────┤
│ [PULL][FETCH][CHKOUT][COMMIT][PUSH]（选中节点可解析为仓时可用，否则禁用+原因）              │
├ StatusBar 不变 ──────────────────────────────────────────────────────────────────────────┤
```

中区树规格：面板 1px 边框 + 8px 圆角；标题 36px（`工作区` + 根路径省略 + 节点计数 + 筛选输入 160 + 刷新）；行高 28；虚拟滚动；无横向滚动。

### 7.3 节点类型、状态标记与交互

| 节点 | 虚拟/真实 | 显示 | 标记 | 单击 | 双击 |
|---|---|---|---|---|---|
| 工作区根 | 真实 | `工作区` + 根路径 | 数据源胶囊、过期 ⏱ | 展开/折叠 | — |
| 组件组 | 虚拟（快照） | 组件 ID + 聚合 chip | 4 仓 mini 点、`*`、`●N` | 选中组件（左栏同步高亮）→ 右栏组件概览 | 展开/折叠 |
| 仓目录 | 真实目录 + 快照状态 | `Design` + 目录名 | 状态圆点、`*`、分支、ahead/behind 简写 | 选中仓 → 右栏仓 Tab + 底部按钮可用 | 打开资源管理器 |
| 目录 | 真实 | 名称 | `⟲` if reparse；不显示 git 状态 | 选中 → 右栏目录/文件信息 | 打开资源管理器 |
| 文件 | 真实 | 名称 + 图标 | `M/A/D/U/!` 字母（token 色）、ignored 灰显 | 选中 → 右栏文件信息 | 白名单直开 / 危险扩展名确认 |
| 未纳管组 | 虚拟 | `未纳管（n）` | warning 图标 | 展开列出未解析目录/仓 | — |

- 展开组件组=本地（用快照数据，零请求）；展开仓/目录=`getWorkspaceTree(path)` 懒加载并缓存 30s。
- 树内筛选（标题栏输入框）：对已加载节点做客户端过滤（名称/路径），不触发新请求；提示"仅过滤已展开内容"。
- 空态：未配置工作区 → `未配置工作区` + `打开设置`；工作区为空 → `工作区为空（无组件目录）` + 重新扫描；根不可读 → 错误码 + 重试。
- 状态色与工作台完全一致（同一 token 映射）；CI 不出现（或仅 CI 槽位在仓右栏）。
- 底部按钮的"选中仓"由节点路径反推：`componentId` + `repoType` 由节点携带（Host 返回），不靠字符串猜测；选中文件/目录时，若其祖先仓可从节点字段得到，则底部按钮按该仓启用并可执行，右栏仍显示文件信息。

### 7.4 文件夹视图数据接口

```json
// UI → Host: getWorkspaceTree
{ "id": "uuid", "method": "getWorkspaceTree", "params": { "path": "", "depth": 1 } }
```

```json
// Host → UI：根层返回组件分组；展开仓/目录后返回物理条目
{ "id": "uuid", "ok": true, "data": {
  "root": "D:\\Work\\MBD", "path": "", "generatedAt": "2026-11-14T02:20:44Z",
  "entries": [
    { "kind": "COMPONENT", "componentId": "CF325F_ChanganSigProcgBascFct",
      "state": "BEHIND", "severityRank": 6, "dirtyCount": 3, "mixedBranch": false,
      "repos": [
        { "kind": "REPO", "repoType": "DESIGN", "name": "CF325F_ChanganSigProcgBascFct_Design",
          "path": "CF325F_ChanganSigProcgBascFct_Design", "state": "BEHIND",
          "branch": "main", "ahead": 0, "behind": 2, "dirtyCount": 1, "hasChildren": true },
        { "kind": "REPO", "repoType": "IMPL", "name": "CF325F_ChanganSigProcgBascFct_Impl", "path": "CF325F_ChanganSigProcgBascFct_Impl", "state": "DIRTY", "branch": "main", "ahead": 0, "behind": 0, "dirtyCount": 3, "hasChildren": true }
      ] },
    { "kind": "UNBOUND_GROUP", "name": "未纳管", "count": 2,
      "children": [ { "kind": "DIR", "name": "X_SIL_Design", "path": "X_SIL_Design", "hasChildren": true } ] }
  ],
  "truncated": false, "error": null
} }
```

展开某仓后（`getWorkspaceTree({path:"CF325F_..._Impl"})`）返回 `kind: "DIR" | "FILE"` 的物理条目，字段与 `getRepoTree` 的 TreeNode 对齐（`path/name/kind/hasChildren/size/mtime/gitStatus/submodule/ignored/children/error`）。

### 7.5 与工作台的关系

| 维度 | 工作台 | 文件夹视图 |
|---|---|---|
| 中区 | 2×2 四仓树（固定 4 面板） | 单棵工作区树（根→组件→仓→文件） |
| 左栏 | 组件卡片列表 | 同左（可折叠，折叠后中区占满） |
| 右栏 | 选中仓 4 Tab | 节点决定内容：组件→概览+CI；仓→4 Tab；文件→文件信息 |
| 底部五键 | 选中仓 | 选中仓（或文件祖先仓） |
| 切换位置 | 标题栏 seg `[工作台][文件夹]` + 视图菜单 + `Ctrl+1/Ctrl+2` | 同左 |
| 持久化 | `localStorage["ui.v3.view"]`，下次启动恢复 | 同 |
| 切换成本 | 保留选中态与滚动位置，不重新请求已缓存树 | 同 |

---

## 8. 保留项（SRS 锚点与全局能力）

### 8.1 CI 10 锚点在新 UI 的落点（全部有位置，testid 保持稳定）

| # | 位置 | 新 UI 落点 | v1 交付物（UI） | `data-testid` |
|---|---|---|---|---|
| CI-1 | 导航"CI 流水线"入口 | 菜单栏 工具 → CI 流水线；状态栏「CI 队列」段点击 | 打开次级弹窗页：骨架 + 空态 + "实验"标记 + 面包屑 | `ci-nav` |
| CI-2 | 组件卡 CI 徽标槽位 | 卡片 L1 右侧，固定 18×18 | 固定槽位 + `∅ 无 CI` 中性空态 | `ci-slot` |
| CI-3 | 组件详情 CI 汇总条 40px | 右栏顶部，固定 40px | 四仓计数（0/0/0/0）+ 最近失败仓（空）+ "未接入" | `ci-strip` |
| CI-4 | 单仓 CI Tab（第 4 位） | 右栏 tablist 第 4 项 | 空态 + `CiLatest` 字段占位表 | `ci-tab` |
| CI-5 | 状态栏 CI 队列 | 状态栏第 5 段 | 图标 + `CI 0`（v1 恒 0） | `ci-queue` |
| CI-6 | 通知中心 CI 事件类目 | 通知抽屉中的独立分组 | 类型枚举 + 空分组 | `ci-notif-category` |
| CI-7 | 看板筛选 CI 状态 facet | 左栏筛选 popover | 禁用项 + tooltip"即将支持" | `ci-facet` |
| CI-8 | 设置 CI 集成分区 | 设置弹窗第 5 分区 | URL/Token/轮询/开关（disabled）+ 实验标记 | `ci-settings` |
| CI-9 | 数据契约 | CI 弹窗内契约块 + `api.ts` 类型 | `CiLatest`/`ComponentCiAggregate`/`ICiProvider` + `CI_UPDATED` 空实现 | `ci-contract` |
| CI-10 | 聚合分离 | 卡片列表容器 + 单测 | 容器 `data-ci-affects-base="false"`；注入 CI FAIL 后卡片底色不变 | `ci-card-base` |

> 唯一性：以上 10 个 testid 在 DOM 中各出现**恰好一次**（CI-9 只在 CI 弹窗打开时存在；CI-8 需设置弹窗打开；CI-7 需筛选 popover 打开；CI-6 需通知抽屉打开；验收脚本按 §10.3 打开对应浮层后断言）。
> 兼容：沿用 M0 已冻结的 testid 名称，旧 CDP 脚本语义不变（位置改变、id 不变）。

### 8.2 状态色 token（唯一来源，禁止新增 hex）

`tokens.css` 现有 `--st-{success,warning,info,purple,cyan,neutral,danger,unknown}` 的 fg/bg/bd 三件套全部保留；新增仅做**派生**：

```css
:root{
  /* 由既有 token 派生，禁止写死颜色 */
  --tint-success: color-mix(in srgb, var(--st-success-bg) 55%, var(--surface));
  --tint-warning: color-mix(in srgb, var(--st-warning-bg) 55%, var(--surface));
  --tint-info:    color-mix(in srgb, var(--st-info-bg)    55%, var(--surface));
  --tint-purple:  color-mix(in srgb, var(--st-purple-bg)  55%, var(--surface));
  --tint-cyan:    color-mix(in srgb, var(--st-cyan-bg)    55%, var(--surface));
  --tint-neutral: color-mix(in srgb, var(--st-neutral-bg) 55%, var(--surface));
  --tint-danger:  color-mix(in srgb, var(--st-danger-bg)  55%, var(--surface));
  --tint-hatched: var(--surface-2);
  /* 变更徽标（非状态色，中性） */
  --badge-change-fg: var(--text); --badge-change-bg: var(--surface-2); --badge-change-bd: var(--border);
  /* CI 通道（独立圆点） */
  --ci-pass: var(--st-success-fg); --ci-fail: var(--st-danger-fg);
  --ci-running: var(--st-info-fg); --ci-idle: var(--st-neutral-bd); --ci-unknown: var(--st-unknown-fg);
}
```

规则：状态 chip/轨道/tint 只能引用上表；CI 形状（圆点/半环/空心）与通道 A 的 chip 形状不同；深色模式由 `prefers-color-scheme` 自动换值；对比度扫描纳入验收（§10.2 AC-32）。

### 8.3 Toast / 任务 / 通知 / 日志 / 设置 / CI 的进入方式

| 能力 | 入口 | 呈现层 | 关闭 | 说明 |
|---|---|---|---|---|
| Toast | 全局事件 | 底部居中堆叠 | 自动 2.4s/6s 或点击 | 成功/失败/复制成功；错误含 `重试`/`详情` |
| 任务中心 | 菜单 工具 / 标题栏 ▤ / 状态栏队列 / 批量提交后自动 | 右侧抽屉 480px | Esc / 关闭按钮 / 点击遮罩 | 进行中/历史、逐仓进度、取消、失败重试；空态标注 M2 |
| 通知中心 | 标题栏 🔔（未读 badge）/ 状态栏通知段 | 右侧抽屉 420px | 同上 | 分组：状态变化/批量/Token/远程；**CI 事件类目（CI-6）**；全部已读 |
| 日志与诊断 | 菜单 工具 → 日志与诊断 | 居中弹窗 960×640 | Esc / 关闭 | 级别过滤（≥所选）、搜索、暂停、导出诊断包（M3 占位） |
| 设置 | 菜单 工具 / 标题栏 ⚙ / `Ctrl+,` | 居中弹窗 760×560 | Esc / 取消 | 六分区（通用/工作区/认证/操作与安全/**CI 集成 CI-8**/实验特性）；未保存变更离开时确认 |
| CI 流水线 | 菜单 工具 / 状态栏 CI 队列段（CI-5） | 居中弹窗 960×640 | Esc / 关闭 | 空态 + 契约（CI-9）+ 实验标记；入口即 CI-1 |
| 未绑定仓库 | 菜单 工具 → 未绑定仓库 | 居中弹窗 800×560 | Esc / 关闭 | 按原因分组、重新解析、忽略（写理由，FR-BOARD-06） |
| 确认对话框 | 破坏性/需确认操作 | 居中 Modal 480px | 取消 / Esc | 展示"将丢失什么"；SYNC/Delete 需输入组件 ID（FR-SAFE-01） |
| 首扫进度 | 标题栏"扫描中"胶囊 + 状态栏 | 胶囊 + 状态栏段 | — | 三阶段 `LOCAL_DISCOVERY→REMOTE_RECONCILE→STATUS_CALC`（图内允许占位，协议字段已冻结） |

浮层打开时底层 `inert`/`aria-hidden`，焦点陷阱（Tab 循环），Esc 关闭，关闭后焦点回到触发元素；浮层不阻塞后台刷新（数据更新继续，关闭后可见）。抽屉/弹窗样式沿用现有 `--surface/--border/--shadow/--radius`。

### 8.4 真实数据保留规则

- 所有计数/状态/分支/Tag/时间来自快照或接口，**禁止 Demo 常量**；无 Host 时展示内置 Mock，且标题栏数据源胶囊必须是"Mock 数据"（不可隐藏）。
- `source=mock` 的 Mock 数据必须能覆盖 7 态（空/加载/错误/离线/过期/部分失败/权限过期）用于视觉测试；真实 Host 数据禁止用 Mock 兜底伪造成功。
- 快照为空/失败时保留上一次数据 + 顶部 notice（不整表闪白），提供重试（沿用 M1 行为）。
- 状态必须区分本地新鲜/远程过期，禁止用旧值冒充当前值。

---

## 9. 组件拆分建议（Vue 3 + TS，零新增运行时依赖）

### 9.1 组件树与文件落点

```
ui/src/
├─ App.vue                        → AppShell 根（组装 + 全局状态 + 浮层宿主）
├─ components/
│  ├─ shell/MenuBar.vue
│  ├─ shell/TitleBar.vue
│  ├─ shell/StatusBar.vue
│  ├─ workbench/WorkbenchView.vue
│  ├─ workbench/ComponentList.vue
│  ├─ workbench/ComponentCard.vue
│  ├─ workbench/RepoGrid.vue
│  ├─ workbench/RepoTreePanel.vue
│  ├─ workbench/FileTree.vue          （树渲染 + 虚拟滚动，被 RepoTreePanel/FolderView 复用）
│  ├─ workbench/InfoPanel.vue
│  ├─ workbench/RepoActionsBar.vue
│  ├─ workbench/BatchBar.vue
│  ├─ folder/FolderView.vue
│  ├─ folder/WorkspaceTree.vue
│  ├─ common/StateChip.vue / CiBadge.vue / ChangeBadge.vue / EmptyState.vue
│  ├─ common/AppMenu.vue（下拉菜单）/ ContextMenu.vue / Popover.vue / VirtualList.vue
│  └─ overlays/DrawerHost.vue / ModalHost.vue / ToastHost.vue / ConfirmDialog.vue
├─ stores/workbench.ts            （组合式 store，单例 reactive，替代全局事件总线）
├─ viewmodel.ts                   （DTO → VM 映射、状态→tone/图标/文案单一映射表）
├─ api.ts                         （扩展 RequestMethod 与新接口，保持 normalize 兼容）
└─ styles/tokens.css              （追加 §9.4 变量，不改既有 --st-*）
```

`App.vue` 从 660 行单文件拆为 AppShell（≤120 行）+ 子组件；旧 7 个整页降级为浮层内的次级页组件（`overlays/pages/{CiPage,TasksPage,NotificationsPage,LogsPage,SettingsPage}.vue`，复用旧内容与 testid）。

### 9.2 组件 props / emits 契约（TypeScript）

```ts
// 共享 VM（viewmodel.ts）
type Tone = 'success'|'warning'|'info'|'purple'|'cyan'|'neutral'|'danger'|'hatched'
type RepoType = 'DESIGN'|'IMPL'|'MIL'|'SIL'
interface RepoVm { repoType: RepoType; repoName: string; state: string; tone: Tone; branch: string; tag?: string;
  headSha?: string; ahead: number; behind: number; dirtyCount: number; untrackedCount: number;
  isLocal: boolean; localPath?: string; flags: string[]; stale: boolean; ci: CiState }
interface ComponentVm { id: string; shortId: string; state: string; tone: Tone; severityRank: number;
  branch: string; tag?: string; mixedBranch: boolean; dirtyTotal: number; starred: boolean;
  repos: RepoVm[]; ci: CiState; lastActivityAt?: string }
interface TreeNodeVm { path: string; name: string; kind: 'DIR'|'FILE'; hasChildren: boolean;
  gitStatus?: 'NONE'|'MODIFIED'|'ADDED'|'DELETED'|'UNTRACKED'|'CONFLICT'; ignored: boolean;
  submodule: boolean; size?: number; mtime?: string; error?: string }
```

| 组件 | props | emits |
|---|---|---|
| `AppShell`（App.vue） | — | — |
| `MenuBar` | `menus: MenuDef[]`；`busyKeys: string[]` | `command(key: string)` |
| `TitleBar` | `product, version, org, counts:{components,repos}, source, sourceTone, scanning, view:'workbench'|'folder', unread:number, taskCount:number, freshness:string` | `search(q: string)`, `switchView(v)`, `refresh()`, `open('notifications'|'tasks'|'settings')` |
| `WorkbenchView` | `component: ComponentVm`, `repo: RepoVm|null`, `treeState: Record<RepoType, TreePanelState>` | `selectRepo(t)`, `treeEvent(e)` |
| `ComponentList` | `components: ComponentVm[]`, `selectedId: string`, `checkedIds: Set<string>`, `selectMode: boolean`, `sortBy: string`, `filters: FilterState`, `loading: boolean`, `error?: string` | `select(id)`, `check(id, shift)`, `toggleSelectMode()`, `sort(key)`, `filter(f)`, `batch(cmd)`, `menu(ev, id)` |
| `ComponentCard` | `component: ComponentVm`, `selected: boolean`, `checked: boolean`, `selectMode: boolean`, `density:'standard'|'compact'` | `select()`, `check(e)`, `quick('SYNC'|'CREATE')`, `contextmenu(ev)` |
| `RepoGrid` | `component: ComponentVm`, `trees: Record<RepoType, TreePanelState>`, `focusedRepo: RepoType` | `focus(t)`, `retry(t)` |
| `RepoTreePanel` | `component: ComponentVm`, `repo: RepoVm|null`, `tree: TreePanelState`, `focused: boolean` | `focus()`, `load(path)`, `toggle(path)`, `selectNode(node)`, `openNode(node)`, `retry()`, `refresh()` |
| `FileTree`（复用） | `nodes: TreeNodeVm[]`, `expanded: Set<string>`, `selectedPath: string|null`, `loading: Set<string>` | `toggle(path)`, `select(node)`, `open(node)`, `contextmenu(ev, node)`, `loadMore()` |
| `InfoPanel` | `component: ComponentVm`, `repo: RepoVm|null`, `tab: 'overview'|'commits'|'diff'|'ci'`, `detail?: RepoDetailVm`, `node?: TreeNodeVm`, `mode:'repo'|'file'|'component'` | `tab(t)`, `copy(text)`, `openPath(node)`, `retry()`, `loadMoreCommits()` |
| `RepoActionsBar` | `repo: RepoVm|null`, `availability: Record<Action, {enabled:boolean, reason?:string}>`, `pending?: Action`, `batchBusy: boolean` | `action(a: Action)` |
| `BatchBar` | `selectedCount: number`, `selectMode: boolean`, `busy: boolean` | `toggleMode()`, `command(cmd)`, `clear()` |
| `StatusBar` | `lastOp: OperationVm`, `freshness:{localAt,remoteAt,localStale,remoteStale}`, `health:{host:boolean,gitworker:boolean,services:ServiceVm[]}`, `queueCount, ciQueue, unread, online` | `open('tasks'|'ci'|'notifications'|'logs')` |
| `FolderView` | `root: string`, `entries: WorkspaceNodeVm[]`, `selectedPath: string`, `expanded: Set<string>`, `filter: string` | `select(node)`, `toggle(path)`, `open(node)`, `contextmenu(ev,node)`, `refresh()` |
| `ToastHost` | `toasts: ToastVm[]` | `dismiss(id)`, `retry(id)` |
| `DrawerHost` | `open: 'tasks'|'notifications'|null`, `payload` | `close()` |
| `ModalHost` | `open: 'settings'|'ci'|'logs'|'unbound'|'confirm'|'commit'|'checkout'|null` | `close()`, `submit(payload)` |
| `StateChip` | `state: string`, `tone: Tone`, `compact?: boolean` | — |
| `CiBadge` | `state: CiState`, `slot?: boolean` | `openCi()` |
| `ChangeBadge` | `dirty: number`, `untracked: number` | — |

### 9.3 状态管理（`stores/workbench.ts`，单例组合式 store）

```ts
state: {
  snapshot: SnapshotDto|null; source: string; scanning: boolean; snapshotError: string;
  selectedComponentId: string; selectedRepoType: RepoType; selectedPath: string|null;
  view: 'workbench'|'folder';
  selectMode: boolean; checkedIds: Set<string>;
  treeCache: Map<`${string}/${RepoType}`, {nodes, expanded:Set<string>, scrollTop, at:number}>;
  repoDetailCache: Map<..., {data, at:number}>;
  action: {pending: Action|null, scope: RepoType|null, lastOp: OperationVm};
  overlays: {drawer: ..., modal: ..., confirm: ..., toasts: ToastVm[]};
}
```

规则：`selectedComponentId` / `selectedRepoType` / `selectedPath` 是唯一选中事实源；树/右栏/按钮全部读它，不各自维护；`refresh()` 只替换快照并清理过期缓存；写操作成功后对受影响 `componentId` 清缓存并 `refresh()`；所有 DTO→VM 映射集中在 `viewmodel.ts`（状态→tone/图标/文案单表，禁止在模板里散落映射）。

### 9.4 CSS 变量（tokens.css 追加，不改既有）

```css
:root{
  --layout-menu-h:30px; --layout-title-h:48px; --layout-actions-h:52px; --layout-status-h:28px;
  --layout-pad-x:16px; --layout-pad-y:12px; --layout-gap:12px;
  --col-left:minmax(248px,20%); --col-right:minmax(224px,18%);
  --card-h:76px; --card-h-compact:60px; --card-pad:7px;
  --tree-row-h:28px; --tree-indent:14px; --tree-indent-max:112px; --tree-head-h:32px;
  --btn-h:40px; --field-h:30px; --chip-h:20px;
  --r-panel:8px; --r-card:8px; --r-chip:6px; --r-btn:6px;
  --z-sticky:10; --z-hover:20; --z-popover:100; --z-menu:110; --z-drawer:200; --z-modal:210; --z-toast:300;
  --tint-success:…; --tint-warning:…; --tint-info:…; --tint-purple:…; --tint-cyan:…; --tint-neutral:…; --tint-danger:…;
}
@media (max-height:799px){ :root{ --layout-menu-h:26px; --layout-title-h:44px; --layout-actions-h:44px;
  --layout-status-h:24px; --layout-pad-y:8px; --card-h:60px; --tree-row-h:26px; --tree-head-h:30px; --btn-h:36px; } }
@media (max-width:1279px){ :root{ --layout-pad-x:8px; --layout-gap:8px; --col-left:232px; --col-right:208px; } }
@media (min-width:1920px){ :root{ --col-left:320px; --col-right:320px; } }
```

### 9.5 响应式规则

1. `≥1920`：左右栏封顶 320，中区变宽（树面板等比增大），不做多列卡片。
2. `1440–1919`：标准比例；1440 为截图基线 A。
3. `1280–1439`：标准纵向；1280 为截图基线 B；右栏 224.6 起，字段标签列 56。
4. `1024–1279`：紧凑档（上下 padding 8、gap 8、卡片 60、状态栏折叠为图标）；仍要求**无横向溢出、所有入口可达**；不做截图基线。
5. `<1024`：单列降级（左栏折叠为 280px 抽屉、右栏变底部 280px 抽屉），仅保证可用；WPF 不允许窗口小于 1024×720。
6. 高度 `720–799`：紧凑纵向（菜单 26/标题 44/按钮 44/状态 24）；树面板最小可用高度 240，低于则内部滚动而非压缩行高。
7. 浮层：宽屏抽屉 480/420，窄屏（<1280）抽屉宽度 `min(92vw, 480)`；弹窗 `min(92vw, 960) × min(90vh, 640)`。

---

## 10. 验收标准（CDP 可自动检查）

### 10.1 `data-testid` 注册表（实现必须逐项落地）

`app-shell, menubar, titlebar, title-product, title-version, title-scope, snapshot-source, title-search, view-switch, view-workbench, view-folder, title-refresh, title-notifications, title-tasks, title-settings, component-list, component-list-count, component-sort, component-filter, batch-bar, batch-select-toggle, component-card[data-component-id], card-ci-slot(ci-slot), card-change-badge, card-repo-chip[data-repo-type], repo-grid, repo-tree-panel[data-repo-type], repo-tree-title, repo-tree-empty, tree-node[data-path], tree-toggle, tree-truncated, info-panel, ci-strip, info-repo-title, info-tab[data-tab], ci-tab, info-branch, info-head, info-ahead-behind, info-dirty, info-last-commit, info-ci, repo-actions, action-pull, action-fetch, action-chkout, action-commit, action-push, statusbar, status-last-op, status-freshness, status-health, status-queue, ci-queue, status-notify, status-network, folder-view, workspace-tree, workspace-node[data-kind], toast-host, toast, drawer, modal, confirm-dialog, context-menu, ci-page, ci-contract, ci-settings, ci-facet, ci-nav, ci-card-base, ci-notif-category`。
`ci-nav/ci-slot/ci-strip/ci-tab/ci-queue/ci-notif-category/ci-facet/ci-settings/ci-contract/ci-card-base` 即 §8.1 的 10 锚点，**各恰好 1 个**。

### 10.2 AC 清单（34 条；方法 = CDP Runtime.evaluate / Input.dispatchMouseEvent / Input.dispatchKeyEvent / Page.captureScreenshot）

| ID | Given/When | Then（可执行断言） |
|---|---|---|
| AC-UI-01 | 启动到工作台 | `[data-testid=menubar|titlebar|component-list|repo-grid|info-panel|repo-actions|statusbar]` 全部存在且可见 |
| AC-UI-02 | 1440×900 | `component-list` 宽 = 281.6±6、`info-panel` 宽 = 253.4±6、`repo-tree-panel` 4 块各 418.5±2 且两两等高、gap=12±1 |
| AC-UI-03 | 1440×900 | 垂直顺序 menubar.top=0、titlebar.top=30、statusbar.bottom=900、`repo-actions` 底边 = `component-list` 底边，且 actions 左边缘 = 中区左边缘（±2） |
| AC-UI-04 | 1280×800 | 同上比例断言（左 249.6±6 / 右 224.6±6 / 树 368.9±2）；`document.scrollingElement.scrollWidth <= innerWidth+1` |
| AC-UI-05 | 任意视口 | `body/#app` computed overflow=hidden；`#app` 高度 = innerHeight；列表/树/右栏内容区的 `overflow-y=auto`、`scrollHeight > clientHeight`（夹具下） |
| AC-UI-06 | 卡片列表 | 可见卡片 ≥6；每卡 4 个 `card-repo-chip`（data-repo-type 覆盖 DESIGN/IMPL/MIL/SIL）；dirty 卡片存在 `card-change-badge` 且标题尾部为 `*` |
| AC-UI-07 | 点击第 2 张卡（CDP Input） | 该卡 `aria-selected=true`、旧卡 false；`info-repo-title` 文本含该组件 ID；4 个 `repo-tree-title` 同步含该组件 ID |
| AC-UI-08 | 卡片聚焦后按 ArrowDown/ArrowUp | 选中在卡片间移动、`document.activeElement` 为卡片、focus ring computed outline-width≥2px |
| AC-UI-09 | 展开树根目录 | `tree-toggle[aria-expanded]` 由 false→true；`tree-node` 数量增加；折叠后数量复原 |
| AC-UI-10 | 双击白名单文件（CDP 双击序列） | 桥收到 `openPath {mode:'open'}` 且路径为相对路径；无页面导航；Toast 出现 |
| AC-UI-11 | 双击目录 | 桥收到 `openPath {mode:'reveal'}`；树展开态不变（不因双击而 toggle） |
| AC-UI-12 | 选中 MISSING 组件/仓 | 对应面板出现 `repo-tree-empty`，文案含"未克隆"，含 Clone 按钮；树体无伪造节点（`tree-node` 数 = 0） |
| AC-UI-13 | 点击 视图→文件夹 | `repo-grid` 不存在、`folder-view` 与 `workspace-tree` 存在；`selectedComponentId` 不变；再点 工作台 恢复 `repo-grid` 且树滚动位置恢复 |
| AC-UI-14 | 文件夹视图 | 根层含 `workspace-node[data-kind=COMPONENT]`；展开组件组不发 `getWorkspaceTree`；展开仓库节点只发 1 次；再次折叠/展开命中缓存不发请求 |
| AC-UI-15 | 选中 Impl 后点 PULL（Mock 桥） | 桥收到 `runRepoAction` 的 `repoType='IMPL'` 且仅一个动作；`data-action-scope` = IMPL；切到 SIL 后 scope 变 SIL |
| AC-UI-16 | 13 个状态夹具组件 | 五键 enabled/disabled 与 §6.2 矩阵逐格一致；禁用键的 `title` 非空且含具体原因；全部禁用时组内无 enabled 按钮 |
| AC-UI-17 | Mock 慢动作（2s） | 进行中键 `aria-busy=true` + spinner；其余 4 键 disabled；状态栏 `status-last-op` 含"执行中"；结束后恢复 |
| AC-UI-18 | Mock 失败动作 | Toast 出现且含错误码；`status-last-op` danger 文案；通知徽标 +1；五键恢复可用 |
| AC-UI-19 | 批量：选 3 个组件点 SYNC | 无确认弹窗；选 11 个组件再点 SYNC → `confirm-dialog` 出现且含"组件 11 / 仓 40+"；取消后桥未收到 job |
| AC-UI-20 | 右键卡片 | `context-menu` 出现且含 SYNC/CREATE/Clone/Delete/复制组件 ID/在资源管理器中打开；Esc 关闭；危险项 computed color = danger token |
| AC-UI-21 | CI 锚点 | 依次打开 CI 弹窗/设置/通知抽屉/筛选 popover 后，10 个 testid 全部命中且 DOM 各 1 个（**10/10**） |
| AC-UI-22 | 注入 CI FAIL（Mock flag） | 卡片 `background-color` 与注入前完全相同（逐字节比较 computed）；`ci-card-base[data-ci-affects-base=false]` 存在 |
| AC-UI-23 | 状态色纪律 | 所有 `.state-chip`/`.ci-badge` 的 computed color/background 值 ⊆ §11.2 token 集合；DOM 中 `style` 属性不含 `#` 十六进制 |
| AC-UI-24 | Ctrl+K → 输入组件 ID 子串 | 搜索框聚焦；列表过滤后 `component-list-count` 数字变小；`compositionstart` 期间不触发查询、`compositionend` 后触发 |
| AC-UI-25 | 点击刷新（Mock 延迟 800ms） | 刷新中按钮 `aria-busy`，卡片数不归零（不闪白）；完成后 `snapshot-source` 与 `status-freshness` 更新 |
| AC-UI-26 | 状态栏 | `statusbar` 内 `[data-seg]` ≥7；`status-last-op` 默认"就绪"；执行一次操作后文案更新；`ci-queue` 文本含 `0` |
| AC-UI-27 | 长路径夹具（90 字符名 + 12 层） | 页面无横向溢出；节点文本 `text-overflow=ellipsis` 且 `title` = 全路径；树体 `scrollWidth <= clientWidth` |
| AC-UI-28 | 大目录夹具（5000 项） | 首次 `getRepoTree` 只发 1 次且返回 2000 项；DOM `tree-node` < 80；滚动到底部渲染最后一项；存在 `tree-truncated` 警告行 |
| AC-UI-29 | 截图基线 | `Page.captureScreenshot` 于 1440×900 与 1280×800 生成 `docs/images/ui-v3/workbench-1440.png`、`workbench-1280.png`、`folder-1440.png`、`action-disabled.png`；与基线图像素差异 ≤0.5%（字体/抗锯齿容差） |
| AC-UI-30 | 控制台 | 全程 `Runtime.consoleAPICalled` error 数 = 0、`Runtime.exceptionThrown` = 0 |
| AC-UI-31 | DPI 缩放 | `Emulation.setDeviceMetricsOverride{1440,900,deviceScaleFactor:1.25}` 后布局尺寸（CSS px）不变且无横向溢出；125% 下不出现半像素错位导致的换行 |
| AC-UI-32 | 深色 + 可访问性 | `Emulation.setEmulatedMedia{prefers-color-scheme:dark}` 后 `--surface` 变化、无白底闪烁；所有按钮有可访问名；`tree` role 正确；Modal 内 Tab 焦点陷阱；Esc 关闭；token 对比度脚本全通过（≥4.5:1） |
| AC-UI-33 | 七态 | 依次注入 空/加载/错误/离线/过期/部分失败/认证过期 Mock：均出现可见说明 + 重试入口，不出现裸空白或裸 TODO |
| AC-UI-34 | 视图切换持久化 | 切到文件夹后 reload，默认视图 = 文件夹；`localStorage["ui.v3.view"]` 对应；切回工作台后同样持久化 |

### 10.3 CDP 执行约定

- 启动：`$env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS='--remote-debugging-port=9222'`，`dotnet run --project src/MbdGitTool.Host`；Node 脚本连 `http://127.0.0.1:9222/json`。
- 尺寸：`Emulation.setDeviceMetricsOverride { width:1440, height:900, deviceScaleFactor:1, mobile:false }`（1280×800 同理）；截图前 `Page.bringToFront` + 等待 2 帧。
- 交互：一律 `Input.dispatchMouseEvent`（`mouseMoved`→`mousePressed`→`mouseReleased`；双击 = 两次 click 间隔 80ms）/ `Input.dispatchKeyEvent`，**禁止 `element.click()`**（与 `docs/ui-review-m0.md` 方法一致）。
- 桥 Mock：通过 `Runtime.addBinding`/`window.__DSH_TEST_BRIDGE__` 注入可控的 Host 应答（含慢动作、失败、CI FAIL、七态），真实 Host 联调另跑一组。
- 浮层锚点：CI-9/CI-8/CI-6 需先打开对应浮层再断言；断言后关闭，避免影响后续用例。
- 产物：截图归档 `docs/images/ui-v3/`；DOM/断言报告 `artifacts/ui-v3/report.json`（运行时产物，gitignored）。

---

## 11. 与旧 UI 的迁移

### 11.1 旧页面 / 锚点保留与移除

| 旧实现（`ui/src/App.vue`） | 新落点 | 处置 |
|---|---|---|
| 自定义 titlebar（品牌/版本/数据源/工作区/新鲜度/刷新） | 标题区（扩展为全局工具栏） | **保留并扩展**；`snapshot-source`、`snapshot-refresh` 语义保留 |
| 左侧 7 图标导航（看板/组件/CI/任务/通知/日志/设置） | 菜单栏 + 标题栏图标 + 状态栏段 + 浮层 | **移除左侧导航**（参考图无此栏）；7 个入口全部保留可达 |
| 看板表格（表格 + 4 仓 chip + CI 槽 + 详情右栏） | 默认改为工作台；表格视图降级为 `视图→表格视图`（P2 备选，不默认暴露） | **降级保留**，FR-BOARD-01 的"表格默认"被裁决 R-01 覆盖；组件/仓数据模型不变 |
| 组件列表页 | 左栏卡片列表（常驻） | **合并**；行点击→卡片点击，语义不变 |
| 组件详情右栏（340px） | 右侧信息面板（224–320px，按 §5） | **替换**；CI 汇总条/四仓 Tab/CI Tab 锚点保留 |
| 单仓详情（隐含在右栏字段） | 右栏 4 Tab（概览/提交/差异/CI） | **扩展**；FR-DETAIL-01/02 落点明确 |
| CI 流水线整页 | 次级弹窗页（960×640） | **保留内容与 testid**；路由由整页改为浮层 |
| 任务中心/通知中心/日志/设置整页 | 抽屉/弹窗（§8.3） | **保留内容与 testid**；导航方式改变 |
| Toast（底部居中） | ToastHost（底部居中，位置随按钮行上移） | **保留并升级**（堆叠/重试/错误码） |
| 状态栏（进程/同步/新鲜度/任务/CI/通知/在线） | 状态栏 7 段（§2.7） | **保留并重排**，新增"最近操作"为第一段（参考图要求） |
| 设置页 6 分区（含 CI 集成） | 设置弹窗 6 分区 | **原样迁移**（CI-8） |
| 首扫 "扫描中" 胶囊、45s 超时、自动重试、空快照回退 | 保留 | **原样保留**（M1 行为不回退） |
| 深色主题（tokens.css） | 保留 | **原样保留**，新面板全部使用 token |
| `data-testid`：`ci-nav / ci-slot / ci-strip / ci-tab / ci-queue / ci-notif-category / ci-facet / ci-settings / ci-contract / ci-card-base` | §8.1 新位置 | **全部保留同名**，CI 锚点 10/10 不回归 |

### 11.2 与 SRS §11 的冲突裁决（以参考图为准，逐条可追溯）

| # | 冲突 | 裁决 | 依据 |
|---|---|---|---|
| R-01 | SRS FR-BOARD-01：默认表格视图 | **默认工作台**；表格降级 P2 备选 | 参考图为工作台布局，用户要求"改成工作台" |
| R-02 | SRS §11.1 L1：左侧图标导航 8 项 | **移除左导航**；入口迁至菜单栏/标题栏/状态栏 + 浮层；7 个功能入口一个不减 | 参考图无左导航；用户"改成工作台" |
| R-03 | SRS §11.3：变更标记用 `●N`，不用 `*` | **两者并存**：`*` 后缀（参考图 3.3 明示）+ `●N` 徽标（SRS 定位置）；`*` 不参与搜索语法，`title` 给分类计数 | 参考图文字规则优先，同时保留 SRS 信息量 |
| R-04 | SRS §11.2："卡片底色永远中性" | **卡片允许 55% 状态色 tint + 4px 轨道**；状态 chip 仍用完整 token；tint 必须过 4.5:1 门禁，不过则降到 40% | 参考图 3.2"卡片颜色由聚合状态决定"；状态 token 仍唯一来源 |
| R-05 | 用户 3.1 旧色语义（红=超前/黄=冲突/灰=MISS） | **按 SRS §11.2 重新映射**：AHEAD=info 蓝、DIVERGED=purple、MISSING/EMPTY=neutral 虚线、UNMERGED=danger 红；保留"卡片按聚合状态着色"的行为 | 用户硬要求"保留 SRS 状态色"；§11.2 为唯一权威色板 |
| R-06 | 参考图右栏 6.7% vs FR-DETAIL 可读性 | 右栏 **18%**（224–320px），差额全部来自右栏 | 参考图右栏是几何示意；plan 已裁决 20/62/18 |
| R-07 | SRS 无"文件夹视图" | **新增视图模式**（候选 A），需补 SRS delta：FR-FS-01（工作区树）、FR-FS-02（懒加载/路径安全）、FR-FS-03（视图切换与持久化）、AC-FS-01..03 | 用户新增硬需求 |
| R-08 | SRS §11.1 L2 的 P2 组件详情页 | **不设独立页**，由右栏 + 4 Tab 承担；下钻仍 1 次点击可达（FR-DETAIL-01） | 参考图无独立详情页；工作台右栏即详情 |
| R-09 | 参考图 6："按钮不同时对四仓发起" | **五键严格单仓**；组件级操作只走批量入口 | 用户明确要求；SRS §9.8 单仓操作语义一致 |
| R-10 | SRS FR-OPS-05 状态栏 6 段 | 7 段：新增"最近操作"为第一段，其余 6 段全保留 | 参考图状态栏只写"最近一次操作的状态"；合并而非替换 |
| R-11 | FR-DETAIL-03 双击白名单 | **完全保留**（白名单直开、危险扩展名二次确认、路径限定根内） | SRS 安全要求不可让位 |
| R-12 | CI-1 原"左侧导航入口" | 位移到菜单栏+状态栏 CI 段（仍为一级入口，1 次点击可达） | 参考图无左导航；锚点语义（入口存在）不变 |
| R-13 | 参考图"双击文件夹=默认程序打开" vs "目录展开/折叠" | 双击=打开资源管理器；箭头/键盘=展开折叠；互不冲突 | 用户文字 rule 4 与常规树习惯的冲突消解 |
| R-14 | 文件夹视图 vs 2×2 树的关系 | 文件夹视图=工作区目录树（A）；四仓合并树=B 降级 P2 | §7.1 |

SRS delta（需在 SRS v0.3 补）：FR-FS-01..03 + AC-FS-01..04；FR-UI-01（工作台区域与比例）、FR-UI-02（左栏卡片信息层级/批量入口）、FR-UI-03（底部单仓按钮矩阵）、FR-UI-04（文件夹视图）、FR-UI-05（右栏 Tab 与构建状态占位）；AC-UI-01..34 对应 §10.2；CI 锚点位置修订（CI-1 位移）。

### 11.3 实现顺序（S2/S3 可照此拆任务）

1. **S2 Host**：`SnapshotDto` 追加可选字段（零破坏）→ `getRepoTree` / `getWorkspaceTree` / `getRepoDetail` / `getCommits` / `openPath`（含路径安全单测）；`runRepoAction` 契约先冻结、返回 `E_NOT_IMPLEMENTED`。
2. **S3 前端（Mock 驱动）**：先后 `viewmodel.ts` + `stores/workbench.ts` → 静态布局（27 个 testid 第一版）→ 卡片列表/批量条 → 四仓树 + 虚拟滚动 → 右栏 + Tab → 底部按钮 + 矩阵 → 文件夹视图 → 浮层（CI/设置/通知/任务/日志/确认）→ 深浅色/紧凑断点 → CDP 34 条自测 + 截图。
3. **S4 联调**：真实快照 + 5 个只读接口；双击打开真实文件；0 console error；真实 RTCRepos 15 仓渲染。
4. **S6 单仓操作**：替换 `runRepoAction` Mock 为真实 job；补 §6.3 进行中/失败/超时/取消；不改布局与 testid。

---

## 12. 风险与未决

| # | 风险 | 影响 | 缓解（已写入规格） |
|---|---|---|---|
| 1 | 4000+ 组件全量快照体积（预估 1.5–3MB JSON）经 WebMessage 传输 + 前端虚拟化 | 首屏慢/内存高 | v1 全量快照 + 虚拟列表；Bridge 实测 >200ms 或 >5MB 时启用分页接口 `getComponentPage{query,filter,sort,offset,limit}`（契约预留，不改 UI） |
| 2 | 大目录/深路径/中文路径的懒加载与路径安全 | 树卡顿、越权打开文件 | 单目录 2000 截断 + 虚拟滚动 + 缓存；Host 归一化/realpath/白名单/危险扩展名二次确认（§4.6） |
| 3 | 卡片 tint 与 SRS 对比度门禁冲突 | 视觉回归/无障碍不达标 | tint 仅由 token 派生、门禁不过降 40%、AC-32 自动扫描；保留一键"中性卡片"样式类兜底 |
| 4 | 10 个 CI 锚点在重写中丢失/重复 | FR-CI-01 验收失败 | testid 注册表 + AC-UI-21 唯一性断言 + 位置表 §8.1 |
| 5 | 双击语义（打开 vs 展开）与 CDP 双击时序 | 误打开文件 | R-13 明确分工；AC-UI-10/11 用 80ms 双击序列；`Enter` 同双击 |
| 6 | 批量操作与单仓五键共用任务队列导致状态互相锁死 | 按钮永远禁用 | 单一 job 状态源 + 同仓互斥/异仓并行 + "该组件有批量任务进行中"明确原因；复用 M1 的刷新节流 |
| 7 | DPI 125%/150% 下 1080p 触发紧凑档甚至单列降级 | 与截图基线不一致 | 验收固定 dpr=1 的 1440/1280；DPI 用 AC-UI-31 单独验证；单列降级只保证可达 |
| 8 | 文件夹视图"虚拟组件组"与实际扁平目录不一致（未纳管/歧义） | 组错/重复 | 分组放 Host（`RepoNameParser` 唯一权威）+ `UNBOUND_GROUP` 分组展示 + 原始目录兜底；UI 不解析目录名 |
| 9 | 真实 Gitea 远程状态缺 refs（M1-b 未完成） | AHEAD/BEHIND 可能 UNKNOWN | UI 支持 `isStale`/`UNKNOWN` 斜纹与"数据过期"文案，不用旧值冒充；不影响新布局 |
| 10 | 底部按钮在 S3 阶段无真实写操作 | 交互不可验收 | Mock 桥可控慢/失败/成功（§10.3），S6 再换真实 job；布局与 testid 冻结避免返工 |

---

## 附录 A：参考图实测数据（复核用）

- 文件：652×441 PNG；有效画布 (35,42)-(619,421)；drawio 源 490×319，缩放 ≈1.19×；图面文字块（drawio 坐标）：菜单栏 (20,25,490×16)、标题 (20,44,490×26)、组件列表 (20,88,120×192.5)、四树各 140×93（170/320, 88/186）、右信息 (470,91,30×181)、按钮 5×55×16（x=170/232/301/369/440, y=292）、状态栏 (20,322,490×22)。
- 实测色值（drawio 默认，仅作识别图面用）：菜单 #DBE8FB、标题/状态栏 #FEE6CE、按钮 #D5E8D5、Component2 #F7CECD、ComponentN #E1D5E6、树面板 #FEE6CE/#F5F5F5、文字 #000000。
- 图注"主界面示意图"与左上角图标不属于 UI；虚线为示意图分隔线，不进入实现。

## 附录 B：SnapshotDto 扩展（保持兼容）与新增接口

B.1 C# 契约（追加可选参数，旧构造调用/旧 JSON 不受影响）

```csharp
public sealed record SnapshotDto(
    string Source, string? WorkspaceRoot, string? Error, string GeneratedAt,
    IReadOnlyList<ComponentDto> Components,
    CountsDto? Counts = null, FreshnessDto? Freshness = null, CapabilitiesDto? Capabilities = null);

public sealed record ComponentDto(
    string ComponentId, string AggregateState, int SeverityRank, string? Branch, string? Tag,
    bool MixedBranch, IReadOnlyList<string> Flags, IReadOnlyList<RepoDto> Repos,
    bool? Incomplete = null, string? BindingCause = null, string? LastActivityAt = null,
    ComponentCiAggregateDto? CiAggregate = null);

public sealed record RepoDto(
    string RepoType, string State, string? SubType, string? Branch, string? HeadSha,
    int Ahead, int Behind, int DirtyCount, int UntrackedCount, bool IsLocal,
    string? LocalPath, IReadOnlyList<string> Flags,
    string? RepoName = null, string? Tag = null, string? UpstreamRef = null, string? CompareRef = null,
    int StashCount = 0, string? CalcAt = null, string? DataSource = null, bool? IsStale = null,
    string? ErrorCode = null, LastCommitDto? LastCommit = null, CiLatestDto? Ci = null);

public sealed record CountsDto(int Components, int Repos, int MissingRepos);
public sealed record FreshnessDto(string? LocalScanAt, string? RemoteFetchAt, bool LocalStale, bool RemoteStale);
public sealed record CapabilitiesDto(bool FileTree, bool WorkspaceTree, bool OpenPath, bool RepoActions, bool Ci);
public sealed record LastCommitDto(string Sha, string ShortSha, string Message, string Author, string At, IReadOnlyList<string> Refs);
```

```ts
// CI 契约（冻结，v1 Mock，ci.enabled=false）
type CiState = 'PASS'|'FAIL'|'RUNNING'|'QUEUED'|'NONE'|'UNKNOWN'
interface CiLatestDto { repoId:number; componentId:string; repoType:'DESIGN'|'IMPL'|'MIL'|'SIL';
  branch:string; commitHash:string; state:CiState; pipelineId?:string; buildNumber?:number;
  failedStage?:string; startedAt?:string; finishedAt?:string; durationMs?:number; url?:string;
  provider:string; fetchedAt:string }
interface ComponentCiAggregateDto { componentId:string; byState:Record<CiState,number>;
  worstState:CiState; lastFailed?:Pick<CiLatestDto,'repoType'|'buildNumber'|'failedStage'|'url'>; dataFreshAt:string }
```

B.2 新增接口一览（信封沿用 `{id, ok, data|error, code}`；全部只读，写操作 M2）

| method | params | data | 错误码（示例） |
|---|---|---|---|
| `getRepoTree` | `{componentId, repoType, path?, depth?}` | 见 §4.4 | `E_REPO_NOT_LOCAL` `E_PATH_INVALID` `E_TOO_MANY_ENTRIES` `E_TIMEOUT` |
| `getWorkspaceTree` | `{path?, depth?}` | 见 §7.4 | `E_WORKSPACE_UNSET` `E_PATH_OUTSIDE_ROOT` `E_REPARSE_POINT` |
| `getRepoDetail` | `{componentId, repoType}` | `{repo:RepoDto, component:ComponentDto, commits:{total,items:LastCommitDto[]}}` | `E_NOT_FOUND` `E_BUSY` |
| `getCommits` | `{componentId, repoType, skip, take}` | `{total, items:LastCommitDto[]}` | `E_GIT_FAILED` `E_TIMEOUT` |
| `openPath` | `{componentId, repoType, path, mode:'open'|'reveal', confirm?:boolean}` | `{opened:boolean, mode, errorCode?:string, message?:string}` | `E_PATH_OUTSIDE_ROOT` `E_DANGEROUS_EXT` `E_CONFIRM_REQUIRED` `E_SUBMODULE` `E_REPARSE_POINT` |
| `runRepoAction`（M2） | `{componentId, repoType, action:'PULL'|'FETCH'|'CHKOUT'|'COMMIT'|'PUSH', args?}` | `{jobId:string, accepted:boolean, errorCode?, message?}`；进度事件 `JOB_UPDATED{jobId,repoType,phase,percent,message}` | `E_ACTION_NOT_ALLOWED` `E_BUSY` `E_NOT_IMPLEMENTED` |
| `getCiStatus`（Mock） | `{componentId}` | `{latest:CiLatestDto[], aggregate:ComponentCiAggregateDto}` | `E_CI_DISABLED` |

`api.ts` 的 `normalizeSnapshot`/`normalizeRepo` 已忽略未知字段，**新增字段不需要改旧解析**；仅需追加 `normalizeRepoTree` / `normalizeWorkspaceTree` / `normalizeRepoDetail` / `normalizeOpenResult` 与对应 mock。
