# UI v3 工作台阶段报告（2026-11-14）

> 视觉基准：`docs/images/reference/main-ui-reference.png`（用户参考图，冲突以图为准）。
> 本阶段完成：新工作台布局 + 文件夹视图 + 真实数据接入 + 三路对抗评审 + P0 修复。

## 1. 已交付

| 能力 | 状态 |
|---|---|
| 参考图布局：菜单栏 / 标题区 / 左组件卡片 / 中 2×2 四仓文件树 / 右信息面板 / 底部 5 键 / 状态栏 | ✅ |
| 组件卡片：聚合状态、四仓 chip、分支/Tag、CI 槽位、变更徽标、选中态、搜索/排序/筛选 | ✅ |
| 四仓文件树：懒加载、展开/折叠、双击打开、MISSING/UNKNOWN 空态、错误态、虚拟行 | ✅ |
| 文件夹视图：工作区根→组件→四仓→文件，两级懒加载，与工作台一键切换 | ✅ |
| 右侧信息：分支/HEAD/对比基准/ahead-behind/dirty/本地路径/最近提交/CI 占位 | ✅ |
| 底部按钮：PULL/FETCH/CHKOUT/COMMIT/PUSH，M1 统一禁用并给出 M2 说明（Host `features.repoActions=false`） | ✅ |
| 菜单/标题入口：工作台/文件夹切换、CI、刷新、通知、任务、设置、搜索 | ✅ |
| 文件夹视图仓节点展开：REPO → 真实文件（示例：`component.txt`/`README.md`） | ✅ |
| 真实数据：RTCRepos（15 仓）+ 本地工作区（`artifacts/real-test/workspace`） | ✅ |
| CI 10 锚点：标题区/卡片/右栏/单仓 Tab/状态栏/通知/筛选/设置/契约/卡片容器 | ✅（跨视图 10/10） |
| 深色主题 / 1280×800 / 1440×900 无横向溢出 | ✅ |

## 2. 本阶段修复（来自对抗评审 P0）

| 评审问题 | 修复 |
|---|---|
| UI-P0：批量条溢出重叠、勾选后仍 0 | 批量条紧凑图标化（276=276 无溢出）；勾选框去掉 click/change 双触发；`1/4` 计数正确 |
| UI-P0：文件夹视图展开仓无子项 | 修复 `children` 未加载语义（normalize 不再把缺失 children 变成空数组），REPO 懒加载生效 |
| UI-P0：卡片状态 tint 过重 | 卡片改中性底色，仅左侧状态轨/状态 chip 着色；选中用轻量 info tint |
| UI-P0：M1 只读边界未生效 | `HostInfo.features.repoActions=false` → 五键全部禁用并给出“M1 只读，M2 接入”原因 |
| 代码 P0：文件夹懒加载无请求 | 同上，修 `loaded` 判定后按 path 发起 `getWorkspaceTree` |
| 组织名硬编码 MBD_Org | `SnapshotDto` 增加 owner/giteaBaseUrl，标题栏/设置显示真实 `RTCRepos` |

## 3. 验证证据

- VS MSBuild：0 warning / 0 error；vstest **216/216**（Core 71 + Host 145）。
- 真实 Gitea + 本地工作区：4 组件 / 14 仓；`RTCT_Demo=BEHIND`、`RTCT_Synced=SYNCED`、`RTCT_RemoteOnly=MISSING`、`RTCT_Incomplete=BINDING_ERROR`。
- CDP（真实鼠标/键盘）：搜索过滤、卡片选中、树展开、文件夹视图、CI/设置/通知弹层、1280 切换、0 console error。
- 截图：`docs/images/ui-v3/`（workbench / folder-view / batch-mode / workbench-1280 / ci-modal / settings）。

## 4. 仍未完成（下一阶段）

1. 组件详情抽屉（双击卡片/`getComponentDetail` 已就绪，UI 未接）。
2. M2 写操作：PULL/FETCH/CHKOUT/COMMIT/PUSH 的实现、确认条、备份/回滚（当前明确禁用）。
3. 树单目录上限 500/2000 对齐、错误态透传（评审 code P1-2/4）。
4. 文件夹视图组件四仓 chip、facet/筛选（评审 req Top7）。
5. 需求文档双源冲突：旧 v0.2 AC 与 v0.3 AC-UI 并存；spec 与 SRS 个别语义（F 通栏/批量条/CI-4/7）需收敛。
6. 快照 2MB/组件规模上限、超大目录性能、CI 真实接入（v2）。
7. 大仓 git status 超时、UI 超时余量、请求字节计数等 code P1。

## 5. 运行

```powershell
npm --prefix ui run build
dotnet build MbdGitTool.sln
$env:MBD_WORKSPACE_ROOT = '<repo>\artifacts\real-test\workspace'
$env:MBD_GITEA_URL = 'http://192.168.50.20:3000'
$env:MBD_GITEA_ORG = 'RTCRepos'
$env:MBD_GITEA_TOKEN = '<token>'
dotnet run --project src\MbdGitTool.Host
```
