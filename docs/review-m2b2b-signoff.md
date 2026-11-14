# M2b-2b 最终签收 Gate 报告（提交 `1324dd4`，含 `4d4a8b5`）

| 项目 | 内容 |
|---|---|
| 评审对象 | `1324dd4f10ba1950bbdc12ef7467bff059c86a15`（`fix(m2b2b): safety refs visible/restorable, restore journal, export filters, tests`，含 `4d4a8b5`）；diff `4d4a8b5..1324dd4` = 15 文件 +1357/−28（Host 3、Core 契约 2、测试 3、UI 4、docs 2、误入 TestResults 1，无 `reqs/` 变更） |
| 评审范围 | 上一轮 Gate P1 五项逐项闭环：①journal/reconcile+审计回填/repair 自动化用例；②安全 ref `refs/backup/restore/*` 可见/可恢复/保留；③restore reset 接 journal；④审计导出过滤；⑤审计证据口径。并复跑全量测试与 M2b-2/M2b-1/M2a/realhost 自检，给出 phase tag 结论。**只读评审，不改代码/真实仓库** |
| 判据 | `reqs/adr/D-026-m2b2a-deferrals.md`（§3 M2b-2b 必须完成、§4 自动化用例全绿）、`reqs/requirements.md` FR-OP-09/11、FR-SAFE-04、`reqs/v0.2/ac-catalog.md` AC-OP-30/AC-SAFE-08/09；上一轮 `docs/review-m2b2b-gate-code.md`、`docs/m2b2b-status.md` |
| 环境 | Win11 / .NET SDK 10.0.404 / git 2.56.1 / 本机真实 Gitea `192.168.50.20:3000`（评审时 PAT 已失效 401，见 §3）。评审开始时 `HEAD=b48d8e5`（后并行提交 `d4c895e` docs-only）；工作区有**并行工作流的未提交源码改动**（`BridgeErrors.cs`/`BatchOperationService.cs`，非本受评提交），为隔离污染，全量测试在 `1324dd4` 的 `git archive` 临时快照 `%TEMP%\signoff-m2b2b-1324dd4` 中构建执行 |
| 方法 | ①`git show 1324dd4` 逐行静态核对；②`1324dd4` 快照串行全量构建+测试（Core→Host，避免 testhost 并发）；③按官方脚本实跑 4 套 CDP 自检（自建 `1324dd4` Host + CDP 9222）；④只读查询真实库/JSONL/journal/exports 与真实 `refs/backup/*`；⑤与 ADR/AC 逐条对照 |
| 边界 | 未修改任何源码/配置/`reqs`/真实仓库；本报告是唯一新增文件。全量测试与 UI 自检副作用见 §6 |

---

## 0. 结论（判决）

> **不建议在 `1324dd4`（或当前 HEAD `d4c895e`）打 `m2b-2b` tag。**（P0=0，P1=2）
>
> - **已闭环**：①自动化用例（新增 `M2b2bAuditJournalTests` **14** 例 + 安全 ref/restore journal 2 例，全量 **385/385**、0 warning/0 error 实测通过）；②安全 ref 可见/可恢复/保留清理（代码+2 例测试，ADR §3 功能面达成，残留见 P2）；③restore `reset` 前后 STARTED/DONE/FAILED journal 已接入并有成功路径测试（FAILED 路径仅代码覆盖）。
> - **未闭环**：④**审计导出过滤只修了 Host，UI 未接线**——`ui/src/stores/workbench.ts:2743-2750` 的 `exportAudit` 仍调用 `api.exportOperationLog(format)`，不传 `state.audit.filters`；本轮实跑 M2b-2 自检留下直接证据：过滤条件 `action=PUSH` 时导出参数仍是 `{"format":"csv"}`（`artifacts/m2b2-selfcheck/result.json` 的 `audit.export-params-csv`）。文档所称“UI 按当前审计过滤导出”不成立，且无导出过滤自动化用例。⑤**审计证据口径仍过度**：`docs/m2b2b-status.md:26` 仍写“回填真实 JSONL 340 行 + 清理测试污染”、`:35` 仍写“repair 已清理当前真实库”，而真实库当前 451 行中仍含 `CF100A_Demo` 38 行 + `MOCK_CF1001*` 26 行（合计 64 行历史夹具/自检数据，无清理入口可删）；同文档 `:4` 仍写 `369/369`、`:17` 的 3 条能力行被字面 `\n` 拼成一行，表格损坏。
>
> **M2b final 仍未完成**（`m2b-2b` 只代表阶段）：真实库 `action=RECONCILE` 仍为 **0 行**（无“杀进程在写中”真机演练）；`docs/m2b-ac-op-gap.md`（`d4c895e`）中 AC-OP 仍有 GAP/PARTIAL；离线完整语义、AC-OP 全量逐条证据未齐。

---

## 1. P1 逐项复核

| # | 上一轮 P1 | 本轮证据（`1324dd4`） | 判定 |
|---|---|---|---|
| ① | journal/reconcile + 审计回填/repair 无自动化用例 | `tests/MbdGitTool.Host.Tests/M2b2bAuditJournalTests.cs` **14 个 Fact**（回退队列+启动回填+去重、JSONL 缺行回填、查询/导出合并 pending、`repairOperationLog` confirm/计数/自审计、journal Begin/Complete/Fail、异常收口、reconcile NOT_APPLIED/UNKNOWN_APPLIED+只读+审计恰 1 行+job 回写、单仓/批量 job journal 关联）；`M2b2BackupRefTests.cs:257-297` 新增安全 ref 列表/可恢复、restore journal STARTED+DONE；`TestInfrastructure.cs:331-347` 为测试注入隔离 `JournalRoot`（不再写真实 journal）。`dotnet test --list-tests` 实列 Host 314 例，其中该类 14 例；全量 385/385 通过 | **PASS（闭环）** |
| ② | 安全 ref `refs/backup/restore/*` 可见/可恢复/保留未做 | `BackupRefService.cs:596-597` `for-each-ref` 同时列 sync+restore；`:613-637` 解析 `Kind=SAFETY`、`branch=null`；`:152-163` restore 接受 restore 前缀；`:656-718` 保留清理同时删超期安全 ref；DTO `BackupDto.cs:9-13,34-38` 增 `Kind`；UI `api.ts:1856-1864`、`viewmodel.ts:1169-1172`、`ComponentDrawer.vue:256` 显示“安全备份”。测试：`M2b2BackupRefTests.cs:257-283`（列表 Kind=SAFETY+恢复成功） | **PASS（功能+测试）**，残留 P2-1/P2-2 |
| ③ | restore reset 未接 journal | `BackupRefService.cs:276-277` reset 前 `Begin("RESTORE_BACKUP",…)`；`:288` 安全 ref 失败、`:301` reset 失败均 `Fail`；`:311` 成功 `Complete(postHead)`；`HostCommandRouter.cs:109` 注入 `_journal`。测试：`M2b2BackupRefTests.cs:285-297` 断言 STARTED+DONE 与 postHead | **PASS（代码+成功路径）**，FAILED/取消路径仅代码覆盖（P2-3） |
| ④ | 审计导出忽略过滤 | Host 侧已修：`ExportOperationLogParams` 增 `Action/ComponentId/Result`（`BridgeParams.cs:106-109`），`OperationAuditService.cs:963-971` 把三项传入 `BuildFilter`（`:1220-1290` 实际参与 SQL 过滤）。**但 UI 未接线**：`ui/src/stores/workbench.ts:2743-2750` 仍只传 `format`；实跑证据 `audit.export-params-csv :: {"format":"csv"}`（过滤 PUSH 时）；`tests/**` 无任何导出过滤断言 | **NOT CLOSED → P1-1** |
| ⑤ | 审计证据口径过度 | `docs/m2b2b-status.md:26/35` 措辞未改；真实库只读副本（含 WAL）合计 **451** 行，夹具行 64 行仍在（`CF100A_Demo`=38、`MOCK_CF1001*`=26）；`:4` 仍 `369/369`；`:17` 三行能力被字面 `\n` 合并（表格损坏） | **NOT CLOSED → P1-2** |

---

## 2. 全量测试（隔离快照，实跑）

```powershell
# 快照 = git archive 1324dd4 → %TEMP%\signoff-m2b2b-1324dd4（ui/dist 由主工作区已构建产物复制，源与 1324dd4 ui/ 一致）
dotnet build MbdGitTool.sln -c Debug --nologo
dotnet test tests/MbdGitTool.Core.Tests -c Debug --no-build   # 71/71
dotnet test tests/MbdGitTool.Host.Tests -c Debug --no-build   # 314/314（1m54s，串行）
```

- 结果：**385/385（Core 71 + Host 314）**，失败 0、跳过 0；构建 **0 警告 / 0 错误**（`TreatWarningsAsErrors=true`）。日志：`artifacts/signoff-m2b2b-test.log`（gitignored）。
- 用例数自洽：Host 上半基线 298 + 新文件 14 + 安全 ref/journal 2 = 314。注：提交说明写“M2b2bAuditJournalTests (13)”，实际 **14**（P2-4）。
- 未在含并行未提交改动的主工作树跑全量，避免把他人改动算作受评提交结果。

## 3. CDP 自检复跑（自建 `1324dd4` Host，CDP 9222 启动成功）

| 脚本 | 声称 | 实测 | 说明 |
|---|---|---|---|
| `ui/scripts/cdp-m2b2-selfcheck.mjs`（隔离 Mock） | 53/53 | **53/53 PASS** | `artifacts/m2b2-selfcheck/result.json`；其中 `audit.export-params-csv` 仅 `{"format":"csv"}` 即 P1-④ 证据 |
| `ui/scripts/cdp-m2b1-fix-selfcheck.mjs`（真实 Host） | 68/68 | **68/68 PASS** | 真实 FETCH/取消/重试/dryRun，无失败 |
| `ui/scripts/cdp-m2a-selfcheck.mjs`（真实 Host） | 11/11 | **10/11** | 失败仅 `ui-08.tooltip-format`：脚本第 68 行依赖远程组件 `RTCT_Incomplete` 的 5 键 error tooltip；本机 Gitea PAT 401 → 快照 `source=local`、仅 3 组件，该组件不存在（数据/环境依赖，非 1324dd4 代码路径） |
| `ui/scripts/cdp-m2b2-realhost-check.mjs`（真实 Host） | 19/19 | **18/19** | 失败仅 `real.workspace-components`（要求 `list>=4`，实测 3）；同因 Gitea 401 缺 `RTCT_Incomplete`。此前 19/19 结果文件为健康 Gitea 时产物 |

- Host 启动：`WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--remote-debugging-port=9222` + `MBD_WORKSPACE_ROOT=<real workspace>` + `MBD_GITEA_*`，`1324dd4` 构建产物，CDP `/json/list` 正常（“Host 可启动 CDP 9222”成立）。
- Gitea 401 是**当前环境事实**：`git credential fill` 取到本机已存凭据，但 Host 同步返回 `HTTP 401`（`host.log`：“Gitea 认证失败（401）：PAT 无效或已过期”）。因此声明中的 `M2a 11/11` 与 `realhost 19/19` 在当前环境**不能完整复现**；失败项均为 Gitea 数据依赖检查，非本轮提交改动路径（`1324dd4` 未改 M2a/realhost 相关 UI 行为）。M2b-2 53/53、M2b-1 68/68 可复现。若签收要求四套全绿，需有效 PAT 后重跑入证据。

## 4. 新增发现（P2，不单独改变 P1 判决）

| # | 问题 | 证据 | 建议 |
|---|---|---|---|
| P2-1 | 安全 ref 清理无自动化用例；真实工作区无任何 `refs/backup/restore/*`，真实环境可见性证据缺 | `CleanupRepositoryAsync` 仅代码；真实 4 仓 `for-each-ref refs/backup/` 只有 sync ref | 补 cleanup 安全 ref 用例；真实 Host 做一次恢复生成安全 ref 并留证 |
| P2-2 | UI 安全 ref 同时显示“安全备份”+“旧格式”；旧格式 tooltip 对安全 ref 语义误导 | `ComponentDrawer.vue:256-258`：SAFETY 分支 `branch=null` → 也落入 `v-else`“旧格式” | SAFETY 单独标签，不再套旧格式文案 |
| P2-3 | restore journal 仅成功路径有测试；取消/异常未在 restore 收口（可能留 STARTED，靠下次 reconcile） | `BackupRefService.cs:276-311` 无 `catch(OperationCanceledException)`；`M2b2BackupRefTests.cs:285-297` 仅成功 | 补失败/取消用例；取消即 `Fail` |
| P2-4 | 证据/文档瑕疵：`m2b2b-status.md:4` 仍 369/369；`:17` 字面 `\n` 破坏 3 行能力表；提交说明测试数 13↔实际 14；`1324dd4` 还带入了误提交的 TestResults XML（`b48d8e5` 已删） | `docs/m2b2b-status.md`、`git show 1324dd4 --stat` | 修文档与提交说明；tag 应指向清理后的提交 |
| P2-5 | 缺 `docs/review-m2b2b-gate-ux.md`（任务引用的 UX Gate 报告在库中不存在），无法核对 UX Gate 原始 P1 表述 | `git log --all -- docs/review-m2b2b-gate-ux.md` 空 | 补交 UX Gate 报告或书面说明其范围并入本报告 |
| P2-6 | realhost 脚本仍为“条件短路/只读”版本（无恢复写链路），且 `real.fetch-available-online` 在 `healthyGitea=false` 时恒真 | `cdp-m2b2-realhost-check.mjs:29-38,154` | 恢复健康 Gitea 后保留写链路断言再入库 |
| P2-7 | 无导出过滤自动化用例（Host 过滤仅代码可见） | `tests/**` 无 `ExportOperationLogParams(Action/Result/ComponentId…)` | 补 service/router 级过滤断言 |

## 5. 与 M2b final 的边界

`m2b-2b` 只是阶段 checkpoint。M2b final 仍缺（不因本 tag 改变）：
- **真机崩溃 reconcile 演练**：真实库/JSONL `action=RECONCILE` 仍 **0 行**（本轮只做单测两分支 + router 注入）；
- **AC-OP 全量逐条证据**：`d4c895e` 的 `docs/m2b-ac-op-gap.md` 仍有多项 GAP/PARTIAL（Clone 预检/错误分类、Tag 检出/漂移、DELETE_PENDING 等）；
- **离线完整语义**（缓存只读/重试队列）与 AC-SAFE-08/09 E2E、审计历史污染的实际清理入口。

## 6. 边界与副作用披露（诚实记录）

- **未修改**源码/配置/`reqs`/`ui`/真实仓库；未 git add/commit；本报告为唯一新增文件。评审期间主工作区出现并行工作流的未提交改动（`BridgeErrors.cs`、`BatchOperationService.cs`）及 docs-only 提交 `d4c895e`，均**不属于**受评提交，全量测试改在 `1324dd4` 快照执行。
- 全量测试：快照内独立 `bin/obj`；测试自身仍会向真实 `%LOCALAPPDATA%\MbdGitTool\logs\host.log` 追加 `HostLog` 行（既有测试隔离缺口，非本轮引入）。
- UI 自检按官方脚本执行，产生真实数据副作用（已披露）：审计导出新增 `%LOCALAPPDATA%\MbdGitTool\exports\operation-log-*.csv`（451 行）；真实 Host 任务中心新增 FETCH/取消/重试/dryRun job 与对应审计行；M2b-1 脚本对真实工作区执行了只读 `FETCH`（更新 `origin/*` 远程跟踪引用，不改工作区/本地分支）；Startup retention 实跑删除 0 行。真实库仅复制到 `%TEMP%\signoff-db` 只读查询。
- 临时产物：`%TEMP%\signoff-m2b2b-1324dd4\*`（快照/构建/测试日志）、`%TEMP%\signoff-db\*`；仓库内 `artifacts/signoff-*.log` 均为 gitignored。

## 7. 复评最小前置（建议）

1. ④ UI 接线：`exportAudit` 传 `state.audit.filters`（action/result，必要时 componentId），并补导出过滤用例（service+router），自检断言 `audit.export-params-*` 含过滤字段；
2. ⑤ 修正 `docs/m2b2b-status.md:4/17/26/35` 与提交说明（385、表格换行、64 行夹具口径），或提供可审计的 JSONL 污染清理入口；
3. 补 P2-1/P2-3（安全 ref 清理、restore FAILED/取消）用例与 P2-2 标签修复；
4. 有效 Gitea PAT 下重跑 M2a 11/11、realhost 19/19 并入库结果；
5. 上述完成后再评估 `m2b-2b` tag；真机 reconcile 演练与 AC-OP 全量证据仍属 **M2b final**，不得以 `m2b-2b` tag 表述 M2b/M2 完成。
