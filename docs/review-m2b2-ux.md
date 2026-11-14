# M2b-2a UX / 需求对抗评审（提交 `1371f51`）

> **判决：M2b-2a 需先修，本轮不可 tag；M2b 不完整，不得宣称 M2b/M2 完成。**
> 汇总：**P0 = 2，P1 = 8，P2 = 10**。M2b-2a 的功能骨架（审计查询/分页/导出、备份 ref 列表/确认/恢复、恢复后刷新、错误态不伪成功）已接通，其中审计与备份列表/确认/拒绝环节有真实 Host 驱动证据；但**恢复类的破坏面披露不完整（P0）**、**EMPTY「首推」在唯一真实状态（unborn）下必失败却仍启用并给出成功式确认文案（P0）**，且**「真实 521 行」证据实为测试污染数据**、备份时间/分支语义误导、离线判定误报、M2b 出口项（journal/reconcile、AC-OP-31/32、保留清理可视化、AC-OP 全量证据）仍缺。

| 项目 | 内容 |
|---|---|
| 评审对象 | `1371f51c1080022cd72c59dd08a385003cba95b3`（`feat(m2b2a): SQLite operation_log, export, backup refs restore, EMPTY first push, retention`，2026-11-14 18:13:44 +0800），未打 tag |
| 评审范围 | M2b-2a 的 UX / 需求符合度：审计查看过滤分页导出、备份 ref 列表/恢复、EMPTY 首推、离线提示、M2b-2 缺口与 AC 证据 |
| 输入 | `docs/m2b-status.md`；`ui/src/components/overlays/{ModalHost,ComponentDrawer}.vue`；`ui/src/stores/workbench.ts`；SRS `reqs/specification.md` §7.6 / §9.7 FR-OP-09/11 / §13.1.2 AC-OP-33、`reqs/specification.md（Annex A）` AC-OP-30/31/32；ADR `reqs/adr/D-025-m2a-m2b.md`；参考 `artifacts/ui-review/m2b2-api-verify.mjs`、`artifacts/ui-review/m2b2-ui-verify.mjs` |
| 环境 | Windows 11 / git 2.59 / .NET 10；真实 Host PID 63108（18:12:35 启动，workspace=`D:\Workspace\git_1\artifacts\real-test\workspace`，CDP 9222 实测可驱动；**评审中途 Host/CDP 被并行进程关闭（约 18:34），之后改用只读 SQLite + 已入库截图/脚本复跑**）；`%LOCALAPPDATA%\MbdGitTool\db\mbd.db` 评审时刻 523 行 |
| 方法 | ① CDP 真实鼠标事件驱动工作台（审计弹窗、抽屉、恢复确认），取 DOM 文本/行数/分页/导出路径与 toast；② Host 桥只读调用 `getOperationLog/getBackupRefs/exportOperationLog/openPath/cleanupRetention`；③ `sqlite3` 只读统计审计数据构成；④ 工作区外临时 git 仓黑盒复现 `reset --hard` 的 untracked 覆盖；⑤ 对照 `git show 1371f51:…` 复核所有行号 |
| 边界 | **只评审，未改任何源码/需求/真实仓库**；临时脚本与截图在 `%TEMP%\uxrev`。评审开始后 HEAD 为 `1371f51`；评审期间并行修复以提交 **`cbdfa78`**（`fix(m2b2a): restore untracked safety, branch/safety-ref hardening, audit isolation and retention floor`）落库，仅含 Host 侧 4 个文件 + `docs/review-m2b2-code.md`：untracked 阻断、安全 ref 去重、审计测试隔离、审计保留下限 90。**本报告全部结论按 `git show 1371f51` 的 blob 给出**；`cbdfa78` 只关闭 P0-1 的 Host 侧一半与 P1-2 的隔离根因，P0-2/P1-1/P1-3/P1-4/P1-5/P1-6/P1-7/P1-8 均不在该修复内，须作为新对象复评 |

---

## 0. 结论速览

| 维度 | 结论 |
|---|---|
| 审计查看/过滤/分页/导出 | 查看、翻页、动作过滤、CSV/JSON 导出、路径展示在真实 Host 上可用；但**列缺 backupRef/jobItemId**、过滤维度少、导出不受过滤约束、动作过滤为等值匹配、空态文案提到不存在的“时间范围”；「真实 521 行」证据不成立（见 P1-1/P1-2/P1-6） |
| 备份 ref 列表/恢复 | 列表/createdAt/确认/恢复后刷新/Host 拒绝不显示成功 均已接通；但 **确认未披露 untracked/ignored 会被覆盖（1371f51 Host 还只拦 tracked）**、无冲突/当前状态预览、createdAt 显示的是 commit 日期而非备份时间、ref 不含分支身份（P0-1/P1-3/P1-4） |
| EMPTY 首推 | 矩阵把 `EMPTY.PUSH` 设 Y，确认条写“发布分支并建立 upstream/空仓首推”，但 `EMPTY` 在真实状态机中**只有 unborn HEAD**，Host 在联网前即拒绝；真实证据只做到“打开确认条→取消”，从未真正首推（P0-2） |
| 离线提示 | source=local 一律被 UI 判“离线/远程不可达”并禁 FETCH；Host 明确“未配置 Gitea 属正常仅本地模式、不算降级”，真实截图正是**无 Gitea 的本地扫描被判离线**；本地扫描失败也被误标为“远程离线”；source=mock 时状态栏仍“在线”；仅 FETCH 降级、无分区离线态（P1-5/P2-7） |
| M2b-2 缺口 | journal/崩溃 reconcile、AC-OP-31/32（组件纳入/移出）、`.trash` 记录选择/冲突预览、保留清理可视化、AC-OP-01…32 逐条证据/故障注入均未交付；`artifacts/m2b2-realhost` 25/25 结果生成于本提交构建之前且被 gitignore，不能作为 1371f51 门禁证据（P1-7/P1-8） |
| 是否可 tag | **否**。P0 闭环 + P1-1/P1-2/P1-3/P1-4 修复 + 证据口径更正后，可考虑“M2b-2a 阶段 tag”；M2b/M2 仍需 M2b-2b 收口 |

---

## 1. 审计查看 / 过滤 / 分页 / 导出（真实数据）

### 1.1 真实 Host 实测（CDP 驱动，18:16–18:18）

2026-11-14 18:16 打开「工具 → 日志与诊断」，审计区实测（评审时点共 **523** 行；521 是 18:12:40 导出时点，后续启动/命令清理各追加 1 行）：

| 断言 | 实测值 |
|---|---|
| 数据源标签 `[data-testid=audit-source]` | `Host 审计` |
| 状态 `[data-testid=audit-status]` | `更新于 0s 前`（时间戳取自 `Date.now()`，非 Host 时间） |
| 分页 | `第 1–20 / 共 523 条`、`第 1 / 27 页`、20 行/页 |
| 表头 | 时间 / actor / 动作 / 组件 / 仓 / 结果 / 错误码 / HEAD（前→后）/ job（**无备份 ref、无 jobItemId、无 requestId**） |
| 第一行 | `11-14 18:15 · Trevn · CLEANUP_RETENTION · — · — · ✓ 成功 · — · — → — · —` |
| 下一页 | `第 21–40 / 共 523 条`、`第 2 / 27 页` |
| 末页 | `第 521–523 / 共 523 条`、`第 27 / 27 页`，3 行 |
| 动作过滤 `PUSH` | `第 1–12 / 共 12 条`、`第 1 / 1 页`，12 行全为 PUSH |
| 动作过滤 `PUS`（部分串） | `0 条` + 空态文案（Host 为 `action = $action` 等值匹配，`AuditDatabase.cs:571-574`；UI 未提示“必须完整动作名”） |
| 结果过滤 | UI 有 OK/FAILED/SKIPPED/CANCELED/INTERRUPTED 下拉（`ModalHost.vue:290-299`）；本次 CDP 脚本注入 select 事件不稳定，**未形成通过/失败断言**，复评需用真实鼠标事件复验 |
| 导出 CSV | `已导出 CSV：C:\Users\Trevn\AppData\Local\MbdGitTool\exports\operation-log-20261114101649-76b41b1a.csv（523 条）`，文件真实存在；`getOperationLog` 同时刻 `total=523`，`exportOperationLog` 返回 `count=523` |
| 打开所在目录 | toast：`! 无法直接打开所在目录（E_PATH_OUTSIDE_ROOT：拒绝对路径/UNC/盘符，只允许工作区内相对路径）：导出路径已复制 · …csv` —— **未伪成功** |
| 不可用/失败态 | 代码路径：`status='unavailable'` 文案“Host 未实现审计查询”+`重试`；`status='error'` 文案“审计查询失败”+错误详情+`重试`；导出失败只写 `audit-export-error` 并 toast 失败，不写 `lastExport`（`workbench.ts:2653-2763`、`ModalHost.vue:332-339`）。真实 Host 无缺方法场景，本次仅静态核对 + 仓内自测脚本模拟，**不冒充真实故障验证** |
| Host 拒绝是否显示成功 | 审计导出/查询：否（异常/缺 `path` 即抛 `E_INVALID_RESPONSE`，不伪造路径，`api.ts:1532-1538`） |

### 1.2 发现

**P1-1（需求 FR-OP-09）审计表缺关键列：备份 ref。**
`OperationLogItemVm` 已含 `backupRef/jobItemId/requestId`（`viewmodel.ts:1144-1161`），Host 行也返回（实测 id=520 `backupRef=refs/backup/sync/20261114095915/IMPL`），但表格只渲染 jobId（`ModalHost.vue:343-380`）；底部 hint 反而写“批量作业行含 jobId/jobItemId/backupRef”（`ModalHost.vue:389`），UI 自相矛盾。FR-OP-09 明确要求审计含“备份 ref”；AC-OP-30②还要求可据此追溯恢复。

**P1-2「真实 521 行」证据不成立（门禁证据口径）。**
只读 SQLite 实测（评审时刻，`mode=ro`）：`operation_log` 共 **523** 行，时间窗仅 09:47:07Z–10:15:52Z，其中：

| 维度 | 实测 |
|---|---|
| 组件分布 | `CF100A_Demo` 370、NULL（CLEANUP_RETENTION）107、`MOCK_CF1001_…` 16、`CF100B_Slow` 14、`CF999_NotThere` 8、`..\..\evil` 6、**真实工作区组件 `RTCT_Demo` 仅 2** |
| 动作分布 | CLEANUP_RETENTION 107、COMMIT 92、DELETE 70、FETCH 68、SYNC 58、CREATE 32、CLONE 20、PULL 20、TAG 16、PUSH 12、RESTORE 12、RESET 8、CHKOUT 6、RESTORE_BACKUP 2 |
| 真实 Host 行 | 可归属真实 Host 的仅**个位数**：`RTCT_Demo` 2 条 `RESTORE_BACKUP`（`cdp-m2b2-realhost-check.mjs` 在隔离副本的恢复），外加最多 3 条可对齐真实 Host 启动/命令的 `CLEANUP_RETENTION` |
| 其余 | `CF100A_Demo`/`MOCK_*`/`CF100B_Slow`/`CF999_NotThere`/`..\..\evil` 等**测试夹具组件共 414 行**，加 107 条 `CLEANUP_RETENTION` 中约 104 条测试期行，≈518 行来自测试夹具/测试命令 |
| JSONL 对照 | `audit/operations.jsonl` **331 行**（06:36–10:15Z），含 RTCT_RemoteOnly 85 / RTCT_Demo 83 / RTCT_Synced 72 / RTCT_Incomplete 24 等**真实工作区历史**（FETCH 217、COMMIT 29…）；而 SQLite 中同组件只有 2 行 → **双写分裂且新审计表未回填历史**，UI 只读 SQLite，用户看不到 JSONL 中的真实历史 |

`docs/m2b-status.md:54-55`“521 行真实可查/真实 521 行导出成功”把**测试污染库**当真实证据；导出 302/521/523 条均来自该库。门禁结论不能据此宣称“真实数据验收通过”。（根因是测试 harness 未注入独立 `DatabasePath/ExportsRoot`；后续 `cbdfa78` 已加 `ConfigureTestIsolation`，但只是隔离根因，**未清理既有污染库、未回填 JSONL、未重做 1371f51 的验收证据，状态报告口径也仍待更正**。）

另：SQLite 与 JSONL **双写分裂且未回填**——`audit/operations.jsonl` 有 331 行真实工作区历史（RTCT_RemoteOnly 85 / RTCT_Demo 83 / RTCT_Synced 72 / RTCT_Incomplete 24，FETCH 217、COMMIT 29，时间自 06:36Z），而 SQLite 中同一工作区只有 2 行；UI 审计页只读 SQLite，用户看到的“全部审计”既含约 518 行测试噪声、又缺 300+ 行真实历史。FR-OP-09 的“操作审计可查询/导出、保留 ≥90 天”在这一口径下不能成立（完整性与可信度均失败）。

**P1-6 审计过滤维度不足 + 导出不受过滤约束。**
- `api.ts:412-425` 与 Host `GetOperationLogParams` 已支持 `from/to/componentId`，UI 只暴露 action/result 两个过滤（`ModalHost.vue:278-322`），**无法按组件/仓/时间范围定位**；523 行 27 页只能全量翻。
- 导出仅传 `{format}`（`workbench.ts:2738-2740`、`api.ts:1850-1852`）；Host `ExportOperationLog` 只认 `from/to`，且把 `action/componentId/result` 硬编码为 null（`OperationAuditService.cs:297-305`）。即使用户已过滤 `PUSH`（12 行），导出仍是全表——本次实测导出的 523 条与当时未过滤的总数一致（当次未在 PUSH 过滤下复测导出，但上述代码路径确定导出不受过滤影响）；UI 导出按钮/提示**没有任何范围说明**，易误以为导出筛选结果。
- 空态文案“可清空过滤条件或调整时间范围后刷新”（`ModalHost.vue:340-342`）提到了 UI 中**不存在的“时间范围”控件**。

**P2-1「打开所在目录」在真实导出路径上必然失败。**
导出固定落 `%LOCALAPPDATA%\MbdGitTool\exports`，而 `openPath` 只允许工作区根内路径（`OpenPathService.cs:138-141`；实测返回 `E_PATH_OUTSIDE_ROOT`）。UI 兜底为复制路径并明确说明（`workbench.ts:2794-2814`），不伪成功；但按钮对真实路径是**半失效**，且 `fallbackCopyExportPath` 在 `navigator.clipboard.writeText` 失败被吞掉后仍提示“导出路径已复制”（`:2807-2813`）——存在**复制失败仍报成功**的伪成功风险。建议：导出落到工作区可打开目录，或 Host 增 `openExportDir` 专用命令，并如实报告复制结果。

**P2-2 过滤交互细节。** 动作过滤是**大小写不敏感等值**（API 侧统一转大写，`api.ts:1843-1846`），不支持“PUSH|SYNC”多值或模糊；无“清除筛选”按钮，只能手工清空；无结果时的空态未回显当前过滤条件。

**P2-3 审计动作枚举漂移。** `RESTORE_BACKUP`、`CLEANUP_RETENTION`、`RESET` 不在 SRS §8.1 `operation_log.action (CHECK 同 job.kind 集)` 的枚举内（`reqs/specification.md:395`），且 `AUDIT_ACTION_LABEL` 未收录，表格原样显示大写（`viewmodel.ts:1122-1141`）；107/523 行是 CLEANUP_RETENTION 维护噪声，审计页无法排除维护行。

**P2-8 分页无水位。** `ORDER BY id DESC LIMIT/OFFSET`（`AuditDatabase.cs:308`）；审计表会因启动/命令清理、其它作业持续追加新行，翻页时可能重复/漏行；UI 无快照水位或“刷新后回到顶部”提示。

**P2-10 导出文件不可辨识范围。** 文件名仅时间戳+随机串，note 只显示条数（523），不记录过滤条件/时间范围；积压多个导出后无法从 UI 判断哪份对应哪次查询。

---

## 2. 备份 ref 列表 / 恢复

### 2.1 真实 Host 实测（CDP 双击组件卡 → 组件详情抽屉）

| 断言 | 实测值 |
|---|---|
| 备份区标题 | `备份 REF（SYNC 备份，可恢复到此处；保留 30 天）`，D/I/M/S 仓 tab + `刷新` |
| 默认加载仓 | 抽屉打开自动按默认聚焦仓加载（RTCT_Demo → Impl）；`RTCT_Demo/IMPL` 列表 1 条 |
| 行内容 | `refs/backup/sync/20261114090055/IMPL · 85dced5 · 11-14 02:10 · 恢复…` |
| 恢复确认 | `从备份 ref 恢复？` / `将把 RTCT_Demo/IMPL 从当前状态恢复到备份 ref refs/backup/sync/20261114090055/IMPL（85dced5）。这会覆盖当前分支状态：恢复后本地 HEAD 指向备份 SHA，未包含在该备份中的未推送改动可能丢失。` / detail=`备份 ref… 备份 SHA… 本次操作会真实写入本地仓库（不写远端）。` / 按钮=`取消`、`确认恢复`（danger） |
| 未确认=0 写 | 取消后无 `restoreBackupRef` 调用、无错误、pending 复位（CDP 桥计数/代码路径一致，`ComponentDrawer.vue:258-265`、`workbench.ts:2866-2888`） |
| Host 拒绝不显示成功 | 代码：`ok=false` → `backupRestore.error='Host 拒绝 restoreBackupRef（ok=false）…'` + error toast + notification，**不刷新成功态、不写成功 toast**（`workbench.ts:2904-2910`）；用桥拦截返回 `restored:false` 实测，抽屉出现 `[data-testid=backup-restore-error]` 错误段落；该分支代码不产生“已从备份 ref 恢复”成功提示 |
| 恢复后刷新 | 成功路径依次 `refreshSnapshot()`→`loadActive()`→`loadComponentDetail()`→`loadBackupRefs()`（`workbench.ts:2924-2928`）；仓内 `artifacts/m2b2-realhost/result.json` 的 `real.backup-restore-success/head-updated/audit-restore-row` 曾据此通过（但见 §2.2 证据时效问题） |
| 无备份空态 | `该仓暂无可恢复的备份 ref（SYNC 成功后会产生 refs/backup/sync/* 备份）`（`ComponentDrawer.vue:243-245`）；接口不可用/失败另有独立红框+重试（`:236-242`） |
| createdAt 展示 | 用 `formatStamp` 显示 **commit committerdate**（`BackupRefService.cs:343,368`；`ComponentDrawer.vue:257`），不是备份 ref 创建时间（ref 名中的 UTC stamp 才是备份时间） |

### 2.2 发现

**P0-1 恢复确认未披露真实破坏面；1371f51 的 Host 只拦 tracked dirty，untracked/ignored 可被 `reset --hard` 静默覆盖。**
- `BackupRefService.cs:169-190` 仅执行 `status --porcelain --untracked-files=no` 判脏，注释还写“untracked 不受 reset 影响”——**与 git 实际行为不符**。
- 工作区外临时仓黑盒复现（未触碰真实仓库）：备份 ref 指向含 `f.txt=v1` 的提交；当前提交删除 `f.txt`；工作区放 untracked `f.txt=LOCAL-PRECIOUS-UNTRACKED`；Host 同款 tracked-only 检查输出为空 → `git reset --hard <backup>` 成功，`f.txt` 内容变为 `v1`，**untracked 本地数据静默丢失**。
- 该路径在 UI 上只经过一个普通 danger 确认框（`ComponentDrawer.vue:258-265` → `ConfirmDialog.vue`），文案仅“未包含在该备份中的未推送改动可能丢失”，**没有**说明 untracked/ignored 会被覆盖/删除，也没有当前 HEAD、dirty/untracked/stash 计数、远程是否已变动等预览（AC-OP-30③“恢复前展示冲突：目标已存在/远程已变动/备份 ref 已过期”、④“冲突时默认不覆盖、可取消”）。
- 后续提交 `cbdfa78` 把该检查扩到 untracked/ignored（`--untracked-files=normal --ignored=matching`）并改测试，Host 侧阻断已落库；但 `cbdfa78` 未改恢复确认 UI，**确认文案/冲突预览仍需补齐**，且需在 `cbdfa78` 上新对象上复评 P0-1 的关闭证据。

**P1-3 备份 ref 的 `createdAt` 与分支身份误导。**
- 实测两处：`refs/backup/sync/20261114090055/IMPL` 在 UI 显示 `11-14 02:10`（= 其指向 commit 的 committerdate `2026-11-13T18:10:56Z` 的本地时间），而 ref 名的备份时间是 2026-11-14 09:00:55Z（**相差约 15 小时**）；`refs/backup/sync/20261114081755/DESIGN` 显示 `2026-11-14T08:17:53Z`（与 stamp 只差 2s，因其指向的提交刚创建）——恰好证明该字段是 **commit 时间**而非备份时间。用“备份 30 天保留”做判断时，用户会被告知错误的备份年龄（可能误判将过期/刚备份）。
- ref 格式为 `refs/backup/sync/<UTC stamp>/<REPO_TYPE>`（`BackupRefService.cs:274-327`），**不含分支名**；SRS/ADR 期望 `sync/<ts>/<branch>`（AC-OP-30 Given ②、D-017）。恢复是 `reset --hard <ref>`，若用户当前在别的分支，会把**当前分支**指到该备份 SHA；UI 列表/确认框都没有“备份来自哪个分支 / 当前分支是什么”的信息。
- createdAt 只做 `formatStamp`（无年份/秒），跨年 30 天保留场景更难读。

**P1-4 脏仓策略在确认之后才暴露；确认文案与实际策略不一致。**
- Host 对 tracked dirty 仓返回 `E_DIRTY_WORKTREE`（“请先 COMMIT 或 STASH 后重试”），即**用户确认后才被拒绝**；抽屉内没有前置提示/禁用（例如“当前仓 dirty，恢复将被拒绝”），也没有在确认框里展示 dirty/untracked 数。
- 确认文案说“未包含在该备份中的未推送改动可能丢失”（偏“可继续但损失”）；实际策略是“tracked dirty 直接拒绝、untracked 在 1371f51 静默覆盖”。两条路径都与文案不符，属于可理解性/一致性问题（P0-1 的另一半）。

**P2-4 恢复前安全 ref 只在 8 秒 toast 中出现。** 成功 toast 含 `恢复前 HEAD 已备份：refs/backup/restore/<ts>/<TYPE>`（`workbench.ts:2911-2918`），但 `getBackupRefs` 只列 `refs/backup/sync/*`，抽屉不展示、不提供恢复 `refs/backup/restore/*` 的入口；安全网产生后不可在 UI 操作，toast 消失即不可追溯（审计表也不展示该 ref 列，见 P1-1）。

**P2-5 恢复错误可能在切换组件后串仓。** `state.backupRestore.error` 只在下次 `executeRestoreBackupRef` 开始时清空，`loadBackupRefs`/切 tab/关抽屉均不清（`workbench.ts:2823-2863,2898-2899`；`ComponentDrawer.vue:268-270`），一次失败后浏览其它仓仍显示上一仓错误。

**P2-6 备份列表容量/格式。** 列表无分页/上限（30 天保留期内 SYNC 次数多时行数不可控）；`formatStamp` 无年份/秒；ref 无 branch 列。

**附加（证据时效）：** `artifacts/m2b2-realhost/result.json` 显示 25/25 通过（17:54:09），但生成时间早于本提交构建产物（exe 18:00、`ui/dist` 18:10）与提交时间（18:13），且 artifacts 被 gitignore、未在 `docs/m2b-status.md` 引用；**不能作为 1371f51 的门禁证据**，只能算开发期自测。另 `ui/scripts/cdp-m2b2-selfcheck.mjs` 全程使用 `window.__M2B2_TEST__` 测试桥/夹具 ref（如 `refs/backup/sync/20261114173000/IMPL`），并非真实 Host，也不能证明真实恢复链路（`cdp-m2b2-selfcheck.mjs:296-301,455-480`）。

---

## 3. EMPTY 首推 / 离线提示

### 3.1 EMPTY 首推：启用 + 成功式文案，但唯一真实状态必失败

- 真实状态机中 `EMPTY` 只在 **HEAD unborn** 时产生（`RepoStateEvaluator.cs:72-76`：“HEAD unborn → EMPTY”）。
- Host 矩阵把 `EMPTY.PUSH` 设为 Y（`RepoActionService.cs:74`），`PushAsync` 对 EMPTY 先做 `rev-parse HEAD`，unborn 直接返回 `E_REF_NOT_FOUND`：“EMPTY 空仓还没有首个提交（unborn HEAD），无法首推；请先在按钮条 COMMIT 生成首个提交后重试”（`:775-803`）——**在启动任何网络 git 之前就必然失败**。
- UI 确认条文案：`text=发布分支并建立 upstream（main → origin/main）`，`detail=空仓首推：首次推送将建立 upstream（setUpstream=true）…`，accept 传 `{setUpstream:true, confirm:true}`（`workbench.ts:1541-1572`）。这是**成功式承诺**，但其实用户即将看到的是拒绝错误。
- **同一页面的另一入口自相矛盾**：EMPTY 仓树空态主按钮“初始化并首推…”点击后提示“当前 Host 未开放该能力”（`RepoTreePanel.vue:45-52,81-93`）；而底部 PUSH 却启用并弹“空仓首推”确认条。两个入口对同一能力给出相反说法，违反 AC-UI-08 单源矩阵/AC-UI-09“禁止可点无效果”的口径。
- 开发期截图 `artifacts/m2b2-realhost/02-real-empty-push-bar.png` 同时显示：卡片“空仓”、四个面板“空仓库（无首个提交）”、底部 PUSH 绿色可用、确认条“发布分支并建立 upstream（master → origin/master）…空仓首推”。真实 Host 自测脚本对该场景**只打开确认条并点取消**（`cdp-m2b2-realhost-check.mjs:159-179`；result.json `real.empty-confirm-no-write-before-accept`），**从未接受/执行过 EMPTY 首推**——即该能力没有被任何证据证明可成功。
- `docs/m2b-status.md:58`“EMPTY 首推｜…git push -u origin HEAD 建 upstream；unborn 拒绝且零远端写”本身已承认唯一真实场景被拒绝；而 SRS §7.6（`reqs/specification.md:353,372`）“EMPTY ✅ 首推建 upstream”与 D-025 §2.5④ 存在**规格与 git 事实冲突**，需需求方裁决，不能既写“恢复 EMPTY.PUSH=Y”又交付一个只能失败的按钮。

**P0-2 判定：** EMPTY 状态下 PUSH 应**禁用**并 tooltip 指向“先 COMMIT 生成首个提交后重试”（或实现真正的“提交+首推”原子流程并由 Host 回写矩阵），同时修正 SRS/ADR/夹具；在此之前不得宣称 FR-REPO-05“EMPTY 首推”已交付。

### 3.2 离线提示：误报 + 伪在线 + 降级不完整

`detectRemoteOffline`（`workbench.ts:877-910`）的判定：`snapshot.source === 'local'` 且命中任一标记 → offline；标记包含 `snapshot.error` 与 `freshness.remoteStale`。Host 侧：

- 未配置 Gitea 属**正常仅本地模式**（`SnapshotService.cs:168-175` 注释“不算降级错误”），此时 `source=local, error=null, remoteStale=true`（`:412-416`）→ **UI 必判离线**：标题区 `⏻ 离线/远程不可达`（`TitleBar.vue:53-59`）、状态栏“离线”（`StatusBar.vue:71-73`）、FETCH 禁用并给出 `E_OFFLINE`。开发期真实截图 `artifacts/m2b2-realhost/01-real-offline.png` 正是此场景：数据源 pill=“本地扫描”、无错误，却被标“离线/远程不可达”。
- 本地扫描失败（`source=local, error='本地扫描失败…'`）同样被标成“离线/远程不可达”，**把本地故障误报为远程离线**。
- **伪在线**：源=Mock（工作区未配置/不存在/非法，Host `BuildMock` 返回 `source=mock,error=...`，`SnapshotService.cs:127-146,428-478`）时 `detectRemoteOffline` 直接 `return {offline:false}`，`state.online=!offline=true`（`:945-946`），状态栏显示“在线”——**加载不到真实工作区、展示 Mock 数据时仍宣称在线**。
- 降级不完整：`availability()` 只把 FETCH 覆写为 disabled（`workbench.ts:522-539`）；PULL（fetch+merge，同为网络操作）仍可点；卡片区/四仓树/信息面板无“离线降级”态（FR-BOARD-03、AC-UI-02 七态要求），只有标题区一个全局 pill；离线开始/恢复无通知，无“远程数据 X 前”的区分。
- 未伪造“在线”的好的一面：真实 Gitea 失败时 Host 返回 local+error，UI 确实不显示 SYNCED/在线，方向正确；问题在“无 Gitea 的正常本地模式”与 mock 被误标。

**P1-5 判定：** source=local 被误报离线（含正常本地模式/本地扫描失败），mock 被伪在线；离线降级只覆盖 FETCH 且无分区态。M2b 的“离线降级完整行为”仍是缺口（与 `docs/m2b-status.md:62` 自述一致）。

**P2-7 补充：** 离线/在线语义未区分“未配置远程”“远程阶段失败”“网络断开”；状态栏初始 `online=true`（`workbench.ts:429`），首帧在快照返回前短暂显示“在线”。

### 3.3 回答“是否伪造在线/离线”

- **伪造离线：是**（source=local 正常本地模式、本地扫描失败均被标“离线/远程不可达”并禁 FETCH）。
- **伪造在线：是**（source=mock 时状态栏“在线”；首屏默认 online=true）。
- **伪造在线于真实 Host 数据：否**（真实 Gitea 失败时 source=local，不会被标在线；这一条做对了）。
- **EMPTY 首推：未伪造成功回执，但伪造了“可首推”的可用性与确认文案**（见 P0-2）。

---

## 4. M2b-2 缺口对照与 AC 状态

### 4.1 缺口对照（对 `docs/m2b-status.md:39-46,50-62`）

| 缺口 | 1371f51 状态 | 证据 | 严重度 |
|---|---|---|---|
| 写前 journal + 崩溃 reconcile（FR-SAFE-04 P0） | **完全没有**：全仓 grep `journal/reconcile` 仅命中 SQLite `journal_mode=WAL` 注释，无 journal 表/文件/启动 reconcile | `src/**` grep 结果；`reqs/specification.md:549` | P1-7（M2b 出口 P0 未实现） |
| 离线降级完整行为 | 部分：仅全局 pill + FETCH 禁用；误报/伪在线；无分区七态 | §3.2 | P1-5 |
| 保留清理可视化 | **无**：`cleanupRetention` 只在 `api.ts:1871-1874` 有客户端方法，workbench/组件/设置页均未调用；设置页“删除保留期/备份 ref 保留”是只读写死“30 天”，无 `backup.retentionDays` 配置项、无上次清理时间/计数展示 | `ui/src` grep `cleanupRetention` 仅 api.ts；`ModalHost.vue:228-234` | P1-7 |
| 备份恢复全链路 | 代码+Host 单测+开发期自测存在；但 1371f51 的真实 UI 链路证据缺失（自测工件早于本提交构建且未入库），确认/预检仍有 P0-1/P1-3/P1-4 | §2 | P1-8 |
| AC-OP 全量证据 | **无** `docs/m2b2-ac-report.md`，无 AC-OP-01…32 逐条证据、无故障注入报告；仅有 `docs/m2a-ac-report.md` 与 M2b 状态表 | `docs/` 清单 | P1-8 |
| `.trash` 恢复（AC-OP-30①） | 批量 RESTORE 仅“按仓名找最近 stamp”（或内部 `args.trashStamp`，UI 无选择）；确认文案“目标已存在则跳过”，无 .trash 记录列表/冲突预览/任务中心逐记录选择 | `BatchOperationService.cs:794-894`、`workbench.ts:2609-2624` | P1-7 |
| FR-OP-12 组件纳入/移出（AC-OP-31/32） | **完全未实现**：无 `is_managed`/纳入/移出 API 或 UI；未绑定仓库弹窗仍只读（“Host 后续版本开放”） | `src/**` / `ui/src/**` 对 `is_managed`、`isManaged`、“纳入/移出”的检索无实现；`ModalHost.vue:393-404` | P1-8（M2b 出口 AC 未过） |
| 取消/job/任务中心（AC-UI-10c） | M2b-1 已交付并有截图/自测；本提交未改 JobEngine（提交 stat 无 JobEngine），未见回退；未在本轮复跑真实 Host | `docs/m2b1-gate-ux.md`、`git show --stat 1371f51` | 通过（沿用 M2b-1 证据） |
| AC-OP-33 审计-lite | 单动作恰 1 行、成功/失败都写、重启可查仍成立；但新增 action（RESTORE_BACKUP/CLEANUP_RETENTION/RESET）超出 §8.1 `job.kind` 枚举，属枚举漂移 | §1.2 P2-3 | 通过（有 P2 漂移） |

### 4.2 AC-OP-30/31/33 逐条

| AC | 结论 | 说明 |
|---|---|---|
| **AC-OP-30** 恢复：`.trash` + 备份 ref + 冲突预览 + 审计 | **不通过（部分）** | ②备份 ref 恢复可用（Host 单测 + 开发期自测）；③冲突预览/过期/远程变动提示缺失；④冲突默认不覆盖只在 `.trash` 目标存在时“跳过”，备份恢复是 `reset --hard` 直接覆盖；①`.trash` 无记录选择/任务中心入口；⑥保留期清理有 Host 实现但 UI 不可见/不可配；安全：untracked 可被静默覆盖（P0-1） |
| **AC-OP-31** 组件纳入 | **不通过（未实现）** | 无输入/校验/四仓清单/DB 写入/审计；无 UI 入口 |
| **AC-OP-32** 组件移出 | **不通过（未实现）** | 无“仅停止管理/本地删除”分支、无 `is_managed` 展示、无审计 |
| **AC-OP-33** 审计-lite | 通过（沿用 M2a），有 P2 枚举漂移 | 新查询/导出能力不改变“恰 1 行”语义；但 `CLEANUP_RETENTION` 等新 action 需需求侧登记 |

### 4.3 M2b 是否完整

**不完整。** M2b 出口要求 `AC-OP-01…AC-OP-32`、`AC-REPO-01…11`、`AC-SAFE-01…13`、`AC-UI-10c` + 故障注入 + 14 态矩阵复验（`reqs/specification.md:141`）。截至 1371f51：AC-OP-31/32 未实现；AC-OP-30 不通过；AC-OP 全量逐条证据/故障注入缺失；journal/reconcile 未实现；离线降级不完整；FR-REPO-05 其余修复类（Repair/Re-clone、detached 建分支）未交付；保留清理可视化/配置缺失。**M2b-2a 只是 M2b-2b 的前半段，状态报告 §5/§6 也自述为待办，不能据此宣称 M2b/M2 完成。**

---

## 5. 问题清单

### P0（2）

| ID | 问题 | 证据/位置 | 修复建议 |
|---|---|---|---|
| **P0-1** | 恢复确认未披露 untracked/ignored 会被覆盖；1371f51 Host 只拦 tracked dirty，`reset --hard` 可静默覆盖 untracked（已黑盒复现） | `BackupRefService.cs:169-190,212-221`；`ComponentDrawer.vue:258-265`；`workbench.ts:2877-2882`；复现：临时仓 untracked `f.txt` 被 backup 版本覆盖。**cbdfa78 已落 Host 侧阻断，需复评** | 确认框展示当前 HEAD/dirty/untracked/stash、目标 ref sha/时间、远程是否变动、过期风险；文案改为“将被拒绝/将覆盖哪些未跟踪文件”，与 Host 策略一致；在 cbdfa78 新对象上补恢复预检关闭证据 |
| **P0-2** | EMPTY 首推不可用却启用：EMPTY 仅 unborn，accept 后必失败；确认条写“空仓首推/建立 upstream”，同页另一入口又说“未开放该能力”；无任何真实成功证据 | `RepoStateEvaluator.cs:72-76`；`RepoActionService.cs:74,775-803`；`workbench.ts:1541-1572`；`RepoTreePanel.vue:45-52,81-93`；`cdp-m2b2-realhost-check.mjs:159-179`；`docs/m2b-status.md:58` | UI/矩阵将 EMPTY.PUSH 禁用并 tooltip“空仓无首个提交：先 COMMIT 后推送”；或 Host 实现“建首个提交+push -u”原子能力；同步修正 SRS §7.6/D-025/夹具；EMPTY 测试须覆盖“点确认后的真实结果”，不能只测到确认条 |

### P1（8）

| ID | 问题 | 证据/位置 |
|---|---|---|
| **P1-1** | 审计表缺备份 ref 列（FR-OP-09 明确要求），hint 却声称有；jobItemId/requestId 不可见 | `ModalHost.vue:343-380,389`；`viewmodel.ts:1144-1161`；实测 id=520 行有 backupRef 但不展示 |
| **P1-2** | “真实 521 行”证据不成立：523 行中真实 Host 仅个位数，≈518 行为测试夹具/测试命令；且 SQLite 未回填 JSONL 中 331 行（含 RTCT 真实历史），双写分裂 → 审计“完整表/90 天保留”不可信 | §1.2；`docs/m2b-status.md:54-55`；SQLite+JSONL 只读统计（DB 523 vs JSONL 331） |
| **P1-3** | 备份 `createdAt` 用 commit committerdate 而非备份时间（ref stamp 与显示差 15h+）；ref 不含分支身份，恢复可能挪错当前分支 | `BackupRefService.cs:343,368`；`ComponentDrawer.vue:255-257`；实测 `20261114090055/IMPL` 显示 `11-14 02:10` |
| **P1-4** | 恢复确认缺冲突/当前状态预览；dirty 策略在 accept 后才报错，文案“可能丢失”与实际“tracked 拒绝/untracked 覆盖”不一致 | `workbench.ts:2877-2882`；`BackupRefService.cs:169-190`；AC-OP-30③④ |
| **P1-5** | 离线误报（source=local 正常本地模式/本地扫描失败 → “离线/远程不可达”+FETCH 禁用）；mock 时状态栏“在线”；仅 FETCH 降级，PULL 仍可点，无分区七态 | `workbench.ts:877-910,522-539,945-946`；`SnapshotService.cs:168-175,412-416`；`artifacts/m2b2-realhost/01-real-offline.png`（无 Gitea 本地扫描被判离线） |
| **P1-6** | 审计过滤仅 action/result（API 已支持 from/to/componentId）；导出恒全表（523 条）且无范围说明；空态文案提“时间范围”但无该控件 | `ModalHost.vue:278-342`；`api.ts:412-425,1850-1852`；`OperationAuditService.cs:297-305` |
| **P1-7** | M2b-2b 缺口确认：journal/崩溃 reconcile 无实现；保留清理无 UI、`backup.retentionDays` 不可配/无上次清理展示；`.trash` 恢复无记录选择/冲突预览；离线降级不完整 | `docs/m2b-status.md:62`；`src/**` grep；`ui/src/api.ts:1871-1874`（未被调用）；`BatchOperationService.cs:794-894` |
| **P1-8** | M2b 出口证据不成立：AC-OP-31/32 未实现；AC-OP-01…32 无逐条证据/故障注入报告；`artifacts/m2b2-realhost` 25/25 早于本提交构建且被 gitignore、状态报告未引用；`cdp-m2b2-selfcheck.mjs` 用测试桥非真实 Host | §4；`docs/` 无 `m2b2-ac-report.md`；`artifacts/m2b2-realhost/result.json`（17:54）vs 构建 18:00/18:10/提交 18:13；`ui/scripts/cdp-m2b2-selfcheck.mjs:296-301,455-480` |

### P2（10）

| ID | 问题 | 位置 |
|---|---|---|
| P2-1 | “打开所在目录”对真实导出路径必失败（openPath 限工作区）；clipboard 失败仍提示“已复制” | `OpenPathService.cs:138-141`；`workbench.ts:2807-2813` |
| P2-2 | 动作过滤等值匹配、无多值/模糊、无清除按钮；空态不回显过滤条件 | `AuditDatabase.cs:571-574`；`ModalHost.vue:278-289` |
| P2-3 | 审计 action 枚举漂移（RESTORE_BACKUP/CLEANUP_RETENTION/RESET 不在 §8.1 job.kind）；无中文标签；维护行占 20% | `viewmodel.ts:1122-1141`；`reqs/specification.md:395` |
| P2-4 | 恢复前安全 ref 只在 8s toast，不列表/不可恢复 | `workbench.ts:2911-2918`；`BackupRefService.cs:31-32` |
| P2-5 | 恢复错误跨仓残留（切 tab/重载不清 `backupRestore.error`） | `workbench.ts:2823-2863,2898-2899`；`ComponentDrawer.vue:268-270` |
| P2-6 | 备份列表无分页；时间格式无年份/秒；无 branch 列 | `ComponentDrawer.vue:246-267`；`viewmodel.ts` formatStamp |
| P2-7 | 离线语义未区分“未配置远程/远程失败/断网”；无离线恢复通知；首帧默认在线 | `TitleBar.vue:53-59`；`workbench.ts:428-429` |
| P2-8 | 审计 offset 分页无水位，新行追加可重复/漏行 | `AuditDatabase.cs:308` |
| P2-9 | 导出文件不记录过滤条件/范围，多份导出难辨识 | `OperationAuditService.cs:338-345`；`ModalHost.vue:324-329` |
| P2-10 | 审计“更新于 X 前”用客户端 `Date.now()`，无 Host 生成时间；跨时区/时钟偏差会误示 | `ModalHost.vue:18-31`；`workbench.ts:2684` |

---

## 6. Top 8（建议修复顺序）

1. **P0-1** 恢复确认/预检：cbdfa78 已落 Host 侧 tracked+untracked+ignored 阻断，需在新对象复评；确认框仍须展示当前 HEAD/dirty/untracked、目标 ref、冲突/过期/远程变动，文案与策略一致。
2. **P0-2** EMPTY 首推：先禁用 + 明确“先 COMMIT”指引（或实现原子首推），同步修正 SRS §7.6 / D-025 / 矩阵夹具；补“确认后真实结果”证据。
3. **P1-1** 审计表补 `备份 ref`（及 jobItemId）列，或删除 hint 中不实声明；补 `RESTORE_BACKUP` 等 action 中文标签。
4. **P1-2** 更正“真实 521 行”口径：cbdfa78 已落测试隔离根因，仍需清理污染库/回填 JSONL/重做证据，并在状态报告给出真实行数与来源；不得以污染库充当验收证据。
5. **P1-3** `createdAt` 改为 ref 创建时间（解析 ref stamp 或记录 `update-ref` 时间），ref 带 branch 身份并在列表/确认中展示；时间格式含年份/秒。
6. **P1-4** 恢复前置预检/预览：dirty/untracked/stash、当前分支/HEAD、目标 ref 分支/时间/过期、远程变动；不满足时在点击阶段即禁用并给原因，而不是 accept 后报错。
7. **P1-5** 离线判定：区分“未配置 Gitea/本地模式”“远程阶段失败”“网络断开”；mock 不显示“在线”；PULL 联动；卡片/树/信息面板补离线态；本地扫描失败不得标“远程离线”。
8. **P1-7 + P1-8** M2b 出口收口：journal/崩溃 reconcile、AC-OP-31/32（FR-OP-12）、`.trash` 记录选择/冲突预览、保留清理可视化与 `backup.retentionDays`、AC-OP-01…32 逐条证据/故障注入；按新提交重跑真实 Host 自检并入库证据。

> P1-6（审计过滤/导出范围）与 P2-1（打开目录）建议紧随 Top8 后处理。

## 7. 结论与 tag 建议

- **M2b-2a：需先修，本轮不可 tag。** 阻断项：P0-1（恢复可静默覆盖 untracked，且确认未披露）、P0-2（EMPTY 首推为“启用但必失败”的伪能力）。注：`cbdfa78` 已修 P0-1 的 Host 侧阻断（untracked/ignored 不再被放行），但 UI 确认/冲突预览仍未补齐，P0-2 未在该提交内；两者都需在新对象上复评关闭。P1-2 的证据口径不实、P1-1/P1-3/P1-4 的审计列/备份时间/冲突预览是阶段 tag 的最低可接受条件；P1-6/P2 可进入 M2b-2b 或紧随修复。
- **M2b：不完整。** 即使 M2b-2a 修完，M2b 出口仍缺 journal/reconcile（FR-SAFE-04 P0）、AC-OP-31/32、AC-OP-30 全量、离线降级完整行为、保留清理可视化/可配、AC-OP-01…32 逐条证据与故障注入报告；此外 FR-REPO-05 的 Repair/Re-clone、detached 建分支与真正的 EMPTY 首推仍需交付。**任何“M2b 完成/M2 交付”声明都不成立。**
- 建议顺序：① 修 P0-1/P0-2（含工作树并行修复纳入提交）；② 修 P1-1/P1-3/P1-4 与证据口径 P1-2；③ 用**本提交新构建**重跑真实 Host CDP（审计真实数据、备份恢复含 dirty/untracked 场景、EMPTY 取消/接受真实结果、离线两种来源），结果与截图入 `docs/` 或可追踪 artifacts，并在 `docs/m2b-status.md` 引用；④ 再评估“M2b-2a 阶段 tag”；⑤ M2b-2b 收口后按 M2b 出口 AC 逐条签收。

---

## 8. 复现与证据索引

| 证据 | 路径/命令 |
|---|---|
| 真实审计 UI（523 行、分页、过滤、导出、打开目录兜底） | CDP 脚本 `%TEMP%\uxrev\audit-ui.mjs`；截图 `%TEMP%\uxrev\a1-audit.png`…`a5-audit-opendir.png`；导出 `C:\Users\Trevn\AppData\Local\MbdGitTool\exports\operation-log-20261114101649-76b41b1a.csv`（523 行） |
| 备份 ref 列表/确认/拒绝态 | `%TEMP%\uxrev\drawer-ui.mjs`；截图 `b1-backup-refs.png`…`b4-backup-after.png` |
| 审计数据构成（真实/测试行） | `%TEMP%\uxrev\dbcheck.py`（Python 只读 `mode=ro` 查询 `%LOCALAPPDATA%\MbdGitTool\db\mbd.db`） |
| untracked 被 `reset --hard` 覆盖黑盒复现 | `%TEMP%\uxrev\restore-clobber`（临时 git 仓，非仓库/真实仓库） |
| EMPTY/离线开发期截图（**早于 1371f51 构建，仅辅助，不作门禁证据**） | `artifacts/m2b2-realhost/01-real-offline.png`、`02-real-empty-push-bar.png`、`result.json`（25/25，17:54） |
| 真实 Host 自检脚本（需重建后重跑） | `ui/scripts/cdp-m2b2-realhost-check.mjs`（EMPTY 只测到取消，`:159-179`）；`ui/scripts/cdp-m2b2-selfcheck.mjs`（测试桥，非真实 Host） |
| 历史审计截图（提交内） | `docs/images/m2b/audit-log.png`、`docs/images/m2b/backup-refs.png` |
| 关键源码/需求行号 | 见各条“证据/位置”列（均对 `git show 1371f51:…` 复核；UI 文件在 1371f51 与工作树一致） |

**复跑建议命令（需在 1371f51 的新构建上执行）：**

```powershell
# 1) 真实 Host + CDP 9222（隔离工作区，勿指向真实用户仓库）
$env:MBD_WORKSPACE_ROOT = "D:\Workspace\git_1\artifacts\m2b2-realhost\workspace"
# 2) 驱动并归档（脚本要求工作区已有 EMPTY 夹具与备份 ref）
node ui/scripts/cdp-m2b2-realhost-check.mjs 9222
# 3) 审计库只读核对（真实 Host 行 vs 测试污染行；Windows 下可用 %TEMP%\uxrev\dbcheck.py）
python -c "import sqlite3,os;p=os.path.expandvars(r'%LOCALAPPDATA%\\MbdGitTool\\db\\mbd.db');c=sqlite3.connect(f'file:{p}?mode=ro',uri=True);print(c.execute('select count(*),max(id) from operation_log').fetchall());print(c.execute(\"select coalesce(component_id,'<null>'),count(*) from operation_log group by 1 order by 2 desc\").fetchall())"
```

> 说明：评审开始时运行中的 Host（PID 63108，18:12:35 启动）在约 18:34 被并行进程关闭，因此 EMPTY/离线的“当前构建”动态复验未能完成；本报告对这两项以 `git show 1371f51` 源码 + 状态机/矩阵 + 时序合规性判定为主，并把“真实 Host 复跑”列为 tag 前置条件。评审期间并行修复已提交为 `cbdfa78`（仅 Host 侧 untracked 阻断/安全 ref 去重/审计隔离/保留 90 天下限）；若后续决定以 `cbdfa78` 为新评审对象，P0-1 的 Host 侧、P1-2 的隔离根因可重新核对，但 P0-2/P1-1/P1-3/P1-4/P1-5/P1-6/P1-7/P1-8 仍需在本报告指出的 UI/需求/证据层面继续修。
