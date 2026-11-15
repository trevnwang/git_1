# M2b-2a UX / 需求 Gate 复评（提交 `cbdfa78`，结论：不打 tag）

> 角色：独立 UX/需求 Gate 复评（`gate-m2b2-ux`）。对象：`cbdfa78 fix(m2b2a): restore untracked safety, branch/safety-ref hardening, audit isolation and retention floor`（父 `1371f51`，2026-11-14 18:36:54 +0800）。
> 环境：Win11 / .NET 10 / git 2.59；真实 Host PID 67380（18:36:58 启动，Debug 构建 dll 18:34:53）/ workspace=`D:\Workspace\git_1\artifacts\real-test\workspace`（4 组件 12 仓，Gitea `http://192.168.50.20:3000` org=RTCRepos，allowInsecureHttp）/ CDP 9222 `https://app.local`。
> 方法：只读代码/需求对照 + **真实 Host CDP 实测**（真实鼠标事件、真实 DB 查询/导出、1 次 no-op 恢复 + 4 次拒绝恢复）+ 作者自检脚本复跑 + 注入桥复现 Host 源码确定的异常场景（离线/EMPTY）；未改源码/需求/真实仓库内容。
> 证据：`artifacts/m2b2-gate-ux/`（4 份自写脚本 log + 12 张截图 + 2 份作者自检 log）；本报告为唯一源码树新增文档。

---

## 0. 结论（判决）

> **cbdfa78 不满足 M2b-2a Gate 出口：本轮「不打 tag」。**
> **汇总：P0 = 1，P1 = 6，P2 = 10。M2b 仍不完整（M2b-2b 未交付）。**

| 专项判据 | 判定 | 一句话证据 |
|---|---|---|
| 1 日志与诊断 → 操作审计（SQLite/过滤/分页/CSV/JSON/真实 path/错误空态/不伪成功） | **PASS（含 2 个 P1 缺口）** | 真实 523 行（20/页、27 页、翻页/过滤实测可用）；CSV 523 行 BOM+CRLF 真实落盘，JSON 523 项；`打开所在目录` 走 `E_PATH_OUTSIDE_ROOT` 兜底且不伪成功；但 UI 缺「备份 ref」列、导出不受过滤约束、库内 99% 为测试行 |
| 2 组件抽屉 → 备份 ref（列表/sha/时间、恢复确认、Host 拒绝、恢复后刷新、无备份空态、dirty/untracked 拒绝提示） | **PASS（含 P1 缺口）** | 真实 `RTCT_Demo/IMPL` 列 1 条 ref/sha/时间；确认→取消 0 调用；确认→成功 toast+安全 ref+快照/列表刷新+审计行；真实 dirty/untracked 两种拒绝均**不伪成功**并给出明确 toast/抽屉错误；无备份空态正确；但确认框无冲突预览/状态清单、createdAt 非备份时间、ref 无分支身份 |
| 3 EMPTY 首推文案/upstream；离线提示不误报；M2a/M2b-1 selfcheck 不回退 | **FAIL（P0+P1）** | EMPTY 矩阵启用+成功式确认文案，但唯一真实状态（unborn）Host 在联网前必拒 → 「可点必失败」；离线在「仅本地正常模式/本地扫描失败」被误判「离线/远程不可达」，mock 源显示「在线」；M2a 自检 11/11、作者 M2b-2 自检 57/57 通过 |
| 4 M2b-2b 缺口（journal/reconcile/离线完整/全量 AC 证据） | **未交付（按指示列为后续、不阻塞 M2b-2a）** | 全仓 grep 无 journal/reconcile 实现（仅 SQLite `journal_mode=WAL`）；无 AC-OP-31/32；无 `m2b2-ac-report.md`/故障注入报告；离线降级仅全局 pill+FETCH |
| 5 tag 建议 | **仅「不打 m2b-2a tag」** | P0-1 未闭环；另 6 项 P1 未闭环。**M2b 不完整，不得写 M2b/M2 完成** |

> 说明：评审期间并行工作于 18:47:5x 停止本 Host 重建，HEAD 于 18:52:01 前进到 `b967d73`（UI 侧修复了本报告 P0-1、P1-2、P1-3、P1-1 的文案/列缺口）。**本报告全部结论与证据均对应 `cbdfa78` 构建**（18:36:58–18:47 驱动；审计表头无「备份」列即为 cbdfa78 UI 的直接旁证）。`b967d73` 不在本 Gate 对象内，须单独复评。

---

## 1. 判据 1：日志与诊断 → 操作审计（真实 Host 实测）

### 1.1 实测证据（cbdfa78 构建，2026-11-14 18:41–18:43）

| 断言 | 实测 |
|---|---|
| 入口/来源/状态 | 工具 → 日志与诊断；`Host 审计`；`更新于 0s 前`；`第 1–20 / 共 523 条`、`第 1 / 27 页`；20 行/页 |
| 翻页 | 下一页 → `第 21–40 / 共 523 条`、`第 2 / 27 页`；上一页回第 1 页 |
| 过滤（真实 Host） | 动作=`PUSH` → `第 1–12 / 共 12 条`、`第 1 / 1 页`，12 行全 PUSH；结果=`FAILED` → `共 141 条`，本页全 FAILED；过滤变更自动回第 1 页 |
| 空态 | 动作=`PUS`（部分串，Host 等值匹配）→ `0 条` +「没有匹配的操作审计记录…」空态；**UI 未提示“须完整动作名”** |
| 错误态（真实触发） | 动作=`BAD;ACTION` → Host `E_INVALID_ARGUMENT` → 红框「审计查询失败 … 参数 action 只允许字母/数字/下划线/连字符」+「重试」；清空过滤后恢复 523 条 |
| 导出 CSV | 真实路径 `C:\Users\Trevn\AppData\Local\MbdGitTool\exports\operation-log-20261114104311-3d09fc28.csv`（146,093 B，BOM+CRLF，523 数据行，表头含 `backup_ref/job_item_id`）；toast 含真实 path +「打开所在目录」 |
| 导出 JSON | 真实路径 `…\operation-log-20261114104312-a8f04acc.json`（336,342 B，可解析，523 项） |
| 打开所在目录 | 真实返回 `E_PATH_OUTSIDE_ROOT`（导出固定 `%LOCALAPPDATA%`，openPath 仅工作区内）→ toast「无法直接打开所在目录…导出路径已复制」；**未伪成功** |
| 页面错误 | 0 条 console/page error |

### 1.2 判定与缺口

- **PASS**：查询/过滤/分页/CSV/JSON 导出/真实 path/错误态/空态均真实可用；导出无 path 视为失败（`api.ts:1532-1538`），未发现伪成功。
- **P1-3（审计表缺备份 ref 列）**：表头仅 `时间/actor/动作/组件/仓/结果/错误码/HEAD/job`（`ModalHost.vue:347`），**无备份 ref/jobItemId**；而底部 hint 写「批量作业行含 jobId/jobItemId/backupRef」（`ModalHost.vue:389`）→ UI 自相矛盾。FR-OP-09 明确要求审计含「备份 ref」（`reqs/specification.md:525`），AC-OP-30② 要求可据此追溯恢复。
- **P1-4（导出不受过滤约束）**：在 `PUSH` 过滤（12 条）下点导出 CSV/JSON，实际导出 **523 条**；UI 无任何范围说明 → 用户以为导出筛选结果，实际导出全表（易触 `E_TOO_MANY_ENTRIES` 上限/导出超范围数据）。另 UI 仅暴露 action/result 两个过滤，Host 已支持 `from/to/componentId`，而空态文案却提「调整时间范围」（`ModalHost.vue:340-342`）→ 无该控件。
- **P1-6（数据/证据口径）**：真实库实测 **528 行**（本 Gate 写入前 523）：`CF100A_Demo` 370、`MOCK_*` 16、`CF100B_Slow` 14、`CF999_NotThere` 8、路径穿越夹具 6、NULL（CLEANUP_RETENTION）107、真实组件 `RTCT_Demo` 7（含本 Gate 5 行）；UI 首屏即可见 `CF100A_Demo` 测试夹具行。`docs/m2b-status.md:54-55` 仍称「521 行真实可查 / 真实 521 行导出成功」——与实况不符（cbdfa78 未更正该口径；其 `docs/review-m2b2-code.md` 已指出污染来源）。
- P2：审计 action 枚举/中文标签漂移（`RESTORE_BACKUP/CLEANUP_RETENTION/RESET` 不在 §8.1 `job.kind` 集；107/523 行为清理噪声）；offset 分页无水位（追加行可重复/漏行）；导出文件名不记录过滤范围；安全 ref 不在审计表展示。

---

## 2. 判据 2：组件抽屉 → 备份 ref / 恢复（真实 Host 实测）

### 2.1 列表 / 空态

| 断言 | 实测 |
|---|---|
| 抽屉 | 双击 `RTCT_Demo` 卡片 → 抽屉打开，来源=`Host 详情`；备份区 tab=`Design/Impl/MIL/SIL`，默认聚焦仓 Impl |
| 真实列表 | `refs/backup/sync/20261114090055/IMPL` · `85dced5` · 显示 `11-14 02:10` · 「恢复…」；`getBackupRefs ok` |
| 无备份空态 | 切 SIL → 「该仓暂无可恢复的备份 ref（SYNC 成功后会产生 refs/backup/sync/* 备份）」 |
| 时间语义（P1-5） | ref 名备份时间 = `2026-11-14T09:00:55Z`（本地 17:00:55），UI 显示 `11-14 02:10`（= commit committerdate，差 15h+）。`createdAt` 取 `committerdate`（`BackupRefService.cs:367,392`），非备份时间；ref 为 `<stamp>/<TYPE>`（无 branch），而 AC-OP-30 Given 为 `refs/backup/sync/<ts>/<branch>`（`ac-catalog.md:1018`）；恢复用当前分支 `reset --hard`，不展示/校验备份来源分支 → 用户可能把错误分支指向旧提交 |
| 时间格式 | `formatStamp` 无年份/秒，30 天保留判断易误读（P2） |

### 2.2 恢复确认 / 取消 / 成功（真实写：1 次 no-op）

- 确认框实测：标题「从备份 ref 恢复？」；正文「将把 RTCT_Demo/IMPL …恢复到备份 ref …（85dced5）。这会覆盖当前分支状态：恢复后本地 HEAD 指向备份 SHA，未包含在该备份中的未推送改动可能丢失。」；detail 含 ref 全名/SHA/「本次操作会真实写入本地仓库（不写远端）」；按钮「取消 / 确认恢复(danger)」。
- **取消 = 0 写**：确认前 Host 日志 `api restoreBackupRef` 计数 84→84；无成功 toast；对话框关闭、pending 复位。
- **确认→成功**：toast「✓ 已从备份 ref 恢复 RTCT_Demo/IMPL：85dced5 → 85dced5（refs/backup/sync/20261114090055/IMPL） · 恢复前 HEAD 已备份：refs/backup/restore/20261114104354531-22734110/IMPL」；Host 日志 `restoreBackupRef … pre=85dced5 post=85dced5`；**恢复后刷新**实测：Host 重扫快照（`快照完成 source=local+gitea components=4`）+ 备份列表重新 `getBackupRefs`（10:43:57Z）+ 审计出现 `RESTORE_BACKUP OK 85dced5→85dced5` 行。
- 安全 ref 唯一性已修：`yyyyMMddHHmmssfff-<guid8>`（`BackupRefService.cs:223`），缓解原「同秒覆盖」。
- 限制（诚实）：该仓备份 SHA 恰等于当前 HEAD，恢复为 **no-op**，因此「恢复后 HEAD 变化」的视觉证据未取得（仅取得刷新/审计/安全 ref 证据）；安全 ref 已按评审规范清理（`update-ref -d`），仓库 HEAD/状态恢复基线。

### 2.3 Host 拒绝反馈（真实 Host，两种拒绝）

| 场景 | 结果 |
|---|---|
| tracked dirty（`RTCT_Demo_SIL` 预存 `M component.txt`） | 确认后 Host 返回 `E_DIRTY_WORKTREE`；toast「✕ 恢复失败 · E_DIRTY_WORKTREE：工作区存在未提交改动 · 工作区存在未提交的已跟踪改动，restore 会被丢弃；请先 COMMIT 或 STASH 后重试（未改动任何文件）。请先 COMMIT 或 stash 后再执行」；抽屉红框同文案；**无成功 toast**；仓库状态不变；审计 1 行 FAILED |
| untracked（`RTCT_Demo_MIL` 临时 untracked 文件） | 确认后 Host 返回 `E_WORKTREE_OVERWRITE`；toast「✕ 恢复失败 · E_WORKTREE_OVERWRITE：工作区存在未跟踪/忽略文件，restore 可能覆盖或删除它们；请先移走后重试（未改动任何文件）」；抽屉红框同文案；**无成功 toast**；未发生 reset；审计 1 行 FAILED |

→ cbdfa78 的 untracked/ignored 阻断（`BackupRefService.cs:171-213`，测试 `M2b2BackupRefTests.cs:137-150`）**在真实 Host 上有效**，原代码评审 P0（静默丢 untracked）已闭环；但 UX/需求侧仍缺 AC-OP-30③④ 的「恢复前展示冲突（目标已存在/远程变动/备份过期/当前 HEAD/dirty/untracked）」——用户只能**确认后**才知道被拒，确认文案「未推送改动可能丢失」与新策略「直接拒绝」不一致（P1-1，见 §5）。临时 ref/untracked 文件已全部清理，两仓恢复基线状态。

---

## 3. 判据 3：EMPTY 首推 / 离线提示 / M2a·M2b-1 不回退

### 3.1 EMPTY 首推：**P0（未闭环）**

| 核对 | 证据 |
|---|---|
| 矩阵 | Host `EMPTY="NYCYY"`（PUSH 允许，`RepoActionService.cs:74`）；夹具 `EMPTY.PUSH.enabled=true, requiresConfirm=true`（`reqs/fixtures/m2-matrix.json:150-153`）；cbdfa78 提交说明「fixture normalized」= 把夹具改成与 Host 一致，**非能力修复** |
| 真实状态机 | `EMPTY` 唯一来源是 HEAD unborn（`RepoStateEvaluator.cs:72-76`）；`PushAsync` 对 EMPTY 先 `rev-parse HEAD`，unborn 直接返回 `E_REF_NOT_FOUND`：「EMPTY 空仓还没有首个提交（unborn HEAD），无法首推；请先在按钮条 COMMIT 生成首个提交后重试」（`RepoActionService.cs:777-803`）→ **联网前必失败** |
| UI 文案 | PUSH 确认条 `发布分支并建立 upstream（main → origin/main）`、detail `空仓首推：首次推送将建立 upstream（setUpstream=true）`（`workbench.ts:1546-1572`）——成功式承诺 |
| 复现（注入桥，响应文案按 Host 源码复制） | 选中 EMPTY 仓 → PUSH 可用（data-enabled=true）→ 确认条如上 → 点「确认推送」→ toast「✕ PUSH 失败 · E_REF_NOT_FOUND：EMPTY 空仓还没有首个提交…」、状态栏 `失败(E_REF_NOT_FOUND)`；**无伪造成功**，但按钮可点必失败 |
| 同页矛盾 | 仓树空态主按钮「初始化并首推…」点击提示「当前 Host 未开放该能力」（`RepoTreePanel.vue:45-52,81-93`），与底部 PUSH 可点相反 |
| 需求 | FR-REPO-05（P0）要求「EMPTY 空仓首次推送（建 upstream）」；SRS §7.6 要求 M2b 构建恢复矩阵（`requirements.md:353,372,538`）→ 恢复矩阵 ≠ 交付能力；单测 `EmptyFirstPush_SetsUpstream…` 用**伪造 EMPTY 状态 + 已有 HEAD** 才通过，真实 unborn 场景由 `EmptyPush_UnbornHead_Fails…` 断言失败 |

→ **FR-REPO-05「EMPTY 首推」未交付，却以可用按钮+成功式文案呈现**，违反「禁止可点无效果 / 阶段门禁」口径，P0。

### 3.2 离线提示：**P1（误报，未闭环）**

| 场景（Host 源码可产生的快照形态） | 实测 UI |
|---|---|
| 正常「仅本地」模式：无 Gitea 配置 → `source=local, error=null, remoteStale=true`（`SnapshotService.cs:168-176,416`，注释明确「不算降级错误」） | 顶部「⏻ 离线/远程不可达」、状态栏「离线」、FETCH 禁用 tooltip `…[E_OFFLINE]` → **误报离线**（`workbench.ts:888-910` 把 remoteStale 当离线标记；真实 Host 当前是 local+gitea，故本机未复现该场景，用注入桥按 Host 形态复现） |
| 本地扫描失败：`source=local, error=本地扫描失败…` | 同样标「离线/远程不可达」→ **本地故障误报为远程离线** |
| Mock 数据：`source=mock`（工作区未配置） | 状态栏「在线」、无离线提示 → **伪在线**（`detectRemoteOffline` 对非 local 直接 false，`workbench.ts:946`） |
| 真实在线（local+gitea） | 无离线 pill、状态栏「在线」、FETCH 可用 → 正常场景无误报（PASS） |

→ 「离线完整降级」可按指示归 M2b-2b；但「离线提示不误报」在本轮判据内且**未闭环**：误报会导致正常仅本地用户被禁用 FETCH，属 P1。

### 3.3 M2a / M2b-1 不回退

| 自检 | 结果 | 说明 |
|---|---|---|
| `cdp-m2a-selfcheck.mjs`（真实 Host） | **11/11 PASS** | Host requiresConfirm COMMIT/PUSH、tooltip 冻结格式、确认条 h=44/bbox 稳定/未确认 0 写、状态栏模板、审计 actor+requestId |
| `cdp-m2b2-selfcheck.mjs`（作者脚本，隔离测试桥） | **57/57 PASS** | 含 M2a/M2b-1 回归抽样：任务中心空态、5 键 tooltip、确认条高度、0 写、UI 矩阵单源 |
| `cdp-m2b1-fix-selfcheck.mjs`（68 项） | **未全量复跑** | 18:47:5x Host 被并行工作停止重建，脚本首个 CDP 连接即失败（`selfcheck-m2b1.log`）。旁证：cbdfa78 diff 未触 M2b-1 代码路径（`git show --stat cbdfa78` = AppConfig/BackupRefService/OperationAuditService/测试/文档）；作者 M2b-2 自检已含 M2b-1 回归抽样并全过。记为**证据局限**，建议 M2b-2a tag 前在定版构建上补跑 68/68 |

---

## 4. 判据 4：M2b-2b 缺口（列为后续，不阻塞 M2b-2a）

| 缺口 | 现状 |
|---|---|
| 写前 journal + 崩溃 reconcile（FR-SAFE-04 P0） | **无实现**（`src/**` grep 仅 WAL `journal_mode`；无 journal 表/文件/启动 reconcile） |
| 离线降级完整行为 | 仅全局 pill + FETCH 禁用；无分区七态/恢复通知；有误报（见 §3.2） |
| `.trash` 恢复记录选择/冲突预览（AC-OP-30①） | UI 无 `.trash` 记录选择；批量 RESTORE 仅“按仓名找最近 stamp” |
| AC-OP-31/32 组件纳入/移出 | **未实现**（无 is_managed/纳入/移出 API/UI） |
| AC-OP 全量证据/故障注入/14 态矩阵 M2b 复验 | 无 `m2b2-ac-report.md`、无故障注入报告 |
| FR-REPO-05 其余修复类 | Repair/Re-clone、detached 建分支、真正的 EMPTY 首推未交付 |

> 以上按任务指示**不阻塞 M2b-2a 结论**；但「M2b 不完整」必须在 tag 说明/状态报告中写明。

---

## 5. 问题清单

### P0（1）

**P0-1 · EMPTY 首推「可点必失败」（FR-REPO-05 未交付 + 同页自相矛盾）**
矩阵/夹具启用 PUSH 且 UI 给出成功式「空仓首推/建立 upstream」确认文案，但 EMPTY 的唯一真实形态是 unborn HEAD，Host 在启动网络 git 前固定返回 `E_REF_NOT_FOUND`；仓树空态又提示「Host 未开放该能力」。证据：§3.1；`RepoActionService.cs:74,777-803`；`RepoTreePanel.vue:45-52,81-93`；`workbench.ts:1546-1572`；`m2-matrix.json:150-153`；`M2b2EmptyPushTests.cs`。修复建议：UI 对 `EMPTY && !headSha` 禁用 PUSH 并 tooltip「先 COMMIT 生成首个提交」；要真正满足 FR-REPO-05 则实现「首个提交 + push -u」原子流程，并同步 SRS/ADR/夹具；EMPTY 用例必须断言“点确认后的真实结果”，不能只测到确认条。

### P1（6）

| ID | 问题 | 关键证据 |
|---|---|---|
| **P1-1** | 恢复确认与 Host 策略不一致、**缺冲突/前置状态预览**（AC-OP-30③④）：确认框只写“未推送改动可能丢失”，未展示目标是否存在/远程是否变动/ref 是否过期/当前 HEAD/dirty/untracked/分支；dirty/untracked 只能在确认后由 Host 拒绝（虽然不再丢数据） | `workbench.ts:2866-2889`；实测确认框文本（§2.2）；cbdfa78 Host 新策略 §2.3；`ac-catalog.md:1020` |
| **P1-2** | 离线提示误报：正常「仅本地」模式被判「离线/远程不可达」并禁 FETCH；本地扫描失败误标远程离线；mock 源显示「在线」 | `workbench.ts:888-910,946`；`SnapshotService.cs:168-176,416`；注入桥实测 §3.2 |
| **P1-3** | 审计表缺「备份 ref」（及 jobItemId/requestId）列，hint 却声称包含 → 与 FR-OP-09/AC-OP-30② 追溯要求不符 | `ModalHost.vue:347,389`；真实表头实测 §1.1 |
| **P1-4** | 导出不受当前过滤约束（实测 PUSH 过滤 12 条 → 导出 523 条）、导出范围无提示；UI 过滤维度不足（无 componentId/时间范围，空态文案却提“时间范围”） | `workbench.ts:2733-2763`；`ModalHost.vue:278-342`；实测 §1.1 |
| **P1-5** | 备份 `createdAt` 用 commit 时间而非备份时间（实测差 15h+），ref 无分支身份、恢复不展示/校验来源分支（AC-OP-30 Given `<ts>/<branch>`） → 可能把错误分支指向旧提交 | `BackupRefService.cs:367,392`；`ComponentDrawer.vue:255-257`；实测 §2.1 |
| **P1-6** | 证据/状态报告口径不实：`docs/m2b-status.md:54-55` 仍称“521 行真实可查/真实 521 行导出成功”，实为 523（现 528）行中约 99% 测试夹具行（UI 首屏可见 `CF100A_Demo`）；cbdfa78 未更正 | 只读 SQLite 统计 §1.2；`docs/m2b-status.md:54-55` |

### P2（10）

1. 审计 action 枚举/中文标签漂移（`RESTORE_BACKUP/CLEANUP_RETENTION/RESET` 不在 §8.1 `job.kind`）；107/523 清理噪声行无过滤维度。
2. 审计 offset 分页无水位（追加行可重复/漏行）；「更新于 X 前」用客户端时钟。
3. 导出文件名/内容不含过滤条件/时间范围；`exports/` 无保留清理（已累计 11 个文件，含测试数据）。
4. 「打开所在目录」对真实导出路径必走兜底（`E_PATH_OUTSIDE_ROOT`），按钮半失效；clipboard 失败仍提示“已复制”的潜在伪成功。
5. 备份列表无分页/上限、无 branch 列、时间无年份；安全 ref 仅 8s toast，产品内不可列出/恢复。
6. 恢复错误跨组件/仓残留（`state.backupRestore.error` 切 tab/重载不清空）。
7. EMPTY 仓树空态提示“Host 未开放该能力”的文案残留（与 P0-1 同一功能面）。
8. 首帧/未知态短暂显示「在线」（初始 `online=true`，`workbench.ts:428-429`）。
9. 真实 `%LOCALAPPDATA%` 审计库/导出历史测试污染未清理（测试隔离 cbdfa78 已加，但历史 518 行与 exports 测试文件仍在）——M2b-2b 清理项。
10. 失败注入/回归缺口：`GitCli` sealed 无法注入 `reset/update-ref` 失败；无“失败恰 1 行审计”的失败路径测试；M2b-1 68 项自检未在本轮全量复跑。

---

## 6. 自检结果汇总

| 自检 | 结果 | 环境 |
|---|---|---|
| `node ui/scripts/cdp-m2a-selfcheck.mjs`（真实 Host） | **11/11 PASS** | cbdfa78 构建（18:41） |
| `node ui/scripts/cdp-m2b2-selfcheck.mjs`（作者，隔离测试桥） | **57/57 PASS** | ui/dist（无 Host 写） |
| `audit-ui.mjs`（本 Gate，真实 Host） | 30/31（唯一 FAIL = 导出过滤范围缺陷复现，见 P1-4） | cbdfa78 构建 |
| `drawer-ui.mjs`（本 Gate，真实 Host） | 17/20（3 项为脚本断言口径问题：tab 文案大小写、confirm 预览正则、Host 日志一行/调用计数；真实发现见 P1-1/P1-5） | cbdfa78 构建 |
| `reject-ui.mjs`（本 Gate，真实 Host） | **12/12 PASS** | cbdfa78 构建 |
| `offline-empty.mjs`（本 Gate，注入桥复现 Host 确定形态） | 14/14（全部缺陷按预期复现，且真实页面已恢复） | cbdfa78 构建 |
| `cdp-m2b1-fix-selfcheck.mjs` | 未完成（Host 中途被并行重建） | — |

---

## 7. 边界与副作用披露（诚实记录）

- **未修改**任何源码/需求/夹具/真实分支内容；唯一源码树新增为本报告。评审期间并行工作修改了 `ui/src` 并在 18:52 提交 `b967d73`（非本 Gate 对象）。
- 真实 Host 写操作（均记录在案并在报告对应小节说明）：
  1. `RTCT_Demo/IMPL` 1 次 **no-op 恢复**（备份 SHA=当前 HEAD 85dced5）→ 产生安全 ref `refs/backup/restore/20261114104354531-22734110/IMPL`（已 `update-ref -d` 清理）与 1 行审计；
  2. 4 次**拒绝恢复**（dirty/untracked 各 2 次，含一次重跑校正断言）→ 各 1 行 FAILED 审计；为触发拒绝在 `artifacts/real-test/workspace` 测试工作区临时创建 2 个 `refs/backup/sync/*` ref 与 1 个 untracked 文件，**已全部删除**，SIL/MIL/IMPL 三仓 HEAD 与工作区状态已核对回基线；
  3. 合计真实审计库 **523→528 行**（JSONL 331→336），CSV/JSON 导出新增 4 个文件（`exports/` 7→11）。
- 离线/EMPTY 场景使用 CDP `Page.addScriptToEvaluateOnNewDocument` 注入测试桥复现 Host 源码确定的快照/错误形态（注入脚本已移除并 reload 恢复真实页面：4 组件、`本地扫描 + Gitea`）；未把注入结果冒充实 Host 回执。
- `artifacts/m2b2-gate-ux/`（脚本/log/12 截图）为证据目录，位于 gitignore 的 artifacts 下。
- 未对真实仓库执行任何内容变更/提交/推送；未修改 DB/exports 历史数据（仅上述真实操作追加行/文件）。

---

## 8. 复跑与证据索引

```powershell
# 真实 Host 已启动（CDP 9222）后：
node artifacts/m2b2-gate-ux/audit-ui.mjs        # 审计查询/过滤/分页/导出/错误态（真实 Host）
node artifacts/m2b2-gate-ux/drawer-ui.mjs       # 抽屉备份 ref + 确认取消 + 1 次 no-op 恢复
node artifacts/m2b2-gate-ux/reject-ui.mjs       # dirty / untracked 拒绝（需预置临时 ref/文件）
node artifacts/m2b2-gate-ux/offline-empty.mjs   # 离线误报 / 伪在线 / EMPTY 首推死路（注入桥，结束自动恢复页面）
node ui/scripts/cdp-m2a-selfcheck.mjs           # M2a 回归（11 项，真实 Host）
node ui/scripts/cdp-m2b2-selfcheck.mjs          # 作者自检（57 项，隔离 Mock）
```

| 证据 | 路径 |
|---|---|
| 审计真实列表/错误态 | `artifacts/m2b2-gate-ux/gate1-audit-list.png`、`gate2-audit-error.png`、`audit-ui.log` |
| 备份列表空态/确认框/成功/恢复审计行 | `gate3-drawer-empty-backup.png`、`gate4-restore-confirm.png`、`gate5-restore-success.png`、`gate6-audit-restore-row.png`、`drawer-ui.log` |
| dirty / untracked 拒绝 | `gate7-reject-dirty.png`、`gate8-reject-untracked.png`、`reject-ui.log` |
| 离线误报/伪在线/EMPTY 死路 | `gate9-offline-false-positive-localonly.png`、`gate10-mock-fake-online.png`、`gate11-empty-push-confirm.png`、`gate12-empty-accept-error.png`、`offline-empty.log` |
| 自检 | `selfcheck-m2a.log`（11/11）、`selfcheck-m2b2.log`（57/57）、`selfcheck-m2b1.log`（未完成） |
| 真实审计库统计（只读） | `sqlite3` 只读查询：528 行 / `CF100A_Demo` 370 / `RESTORE_BACKUP` 7（2 历史+5 本 Gate） |

> 需求引用行号：`reqs/specification.md:525`（FR-OP-09）、`:527`（FR-OP-11）、`:538`（FR-REPO-05）、`:549`（FR-SAFE-04）、`:353,372`（§7.6 EMPTY/矩阵）、`reqs/specification.md（Annex A）:1015-1021`（AC-OP-30）、`reqs/fixtures/m2-matrix.json:150-153`（EMPTY.PUSH）。
