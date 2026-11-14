# M2b-1 阶段报告：作业内核 + 批量操作 + 任务中心（2026-11-14）

> M2b 第一批：**作业内核（job/job_item/JOB_UPDATED/取消/仅重试失败/持久化）** + **批量 PULL/FETCH/SYNC/CLONE/DELETE/RESTORE/CREATE/TAG** + **任务中心 UI**。
> 状态：**M2b-1 Gate 通过（代码 P0/P1=0；UX P0/P1=0；298/298 测试）**，tag `m2b-1`；**不代表完整 M2b/M2 完成**。完整 M2b 仍缺 M2b-2：审计完整表/查询导出/90 天、备份 ref 恢复入口、EMPTY 首推、离线降级、journal/reconcile。

## 1. 已交付

| 能力 | 实现 |
|---|---|
| 作业模型 | `JobDto/JobItemDto` 7 态；`job`/`job_item` 持久化 `%LOCALAPPDATA%\MbdGitTool\jobs\jobs.json`；启动时 PENDING/RUNNING → INTERRUPTED，不重复执行；保留最近 200 job |
| 事件 | `HostCommandRouter.EventSink` → WebViewBridge/MainWindow Dispatcher → `{event:"JOB_UPDATED",data:JobDto}`；UI 自动更新任务中心/状态栏/通知 |
| API | `runBatch/getJobs/getJob/cancelJob/retryFailed`；`componentIds` 必填（禁止隐式全量）；`accepted:boolean`、total/skipped/retried int；`E_JOB_NOT_FOUND` |
| 批量语义 | FETCH/PULL/CHKOUT/COMMIT/PUSH 复用单仓安全语义；SYNC= fetch→备份 ref `refs/backup/sync/<UTC>/<TYPE>`→stash -u→reset --hard origin/branch（备份失败不 reset）；CLONE 缺失仓（Gitea base/org + URL 作用域 PAT）；DELETE 软删 `.trash/<stamp>`（30 天）；RESTORE 最近/指定 stamp；CREATE 分支；TAG 本地 tag；dryRun 预览 |
| 安全 | 路径在 workspace 内；ArgumentList；token 不入 argv/日志；DELETE 不直接递归删除；强写需 `args.confirm=true`；单 worker + 逐仓串行；取消合作式 |
| 审计 | 每个 job item 完成写 1 行 JSONL（复用 M2a schema，新增 `jobId/jobItemId/result/backupRef`），无 token |
| 任务中心 UI | 进行中/历史分段、ok/total、取消、展开逐仓（状态/错误码/backupRef/pre→post）、仅重试失败项、刷新/事件驱动、空态；批量确认对话框（dryRun） |
| 批量入口 UI | BatchBar/卡片右键/菜单 SYNC/CREATE/CLONE/DELETE 先确认/输入再提交 job；提交后打开任务中心；M2a 单仓逻辑未回退 |

## 2. 真实环境实测（RTCRepos + 本地工作区）

| 场景 | 结果 |
|---|---|
| 批量 FETCH `RTCT_Demo` | ✅ job SUCCESS 4/4，任务中心历史出现 |
| 批量 CLONE `RTCT_RemoteOnly` 4 个缺失仓 | ✅ SUCCESS 4/4，本地出现四仓目录（真实 Gitea clone） |
| 批量 DELETE `RTCT_RemoteOnly` | ✅ SUCCESS 4/4 → `.trash/20261114081734/...`，远端未改 |
| 批量 RESTORE | ✅ SUCCESS 4/4，四仓恢复 |
| 批量 SYNC（先造本地未推送 commit） | ✅ 4/4；`refs/backup/sync/20261114081755/DESIGN` = preHead，reset 到 origin/main |
| 取消 job（12 项 FETCH） | ✅ CANCELED 12/12，0 ok/0 failed |
| retryFailed | ✅ 新 job SUCCESS 12/12（retried=12） |
| JOB_UPDATED | ✅ 任务中心历史计数 6→7 自动更新（未手动刷新） |
| 审计 | ✅ 每 item 1 行，含 jobId/jobItemId/backupRef/actor/requestId；无 token |

## 3. 对抗评审与 Gate 结果\n\n| 评审 | 结果 |\n|---|---|\n| M2b-1 代码/安全评审 | P0=1/P1=7（发现 SYNC/DELETE 预检、取消语义、审计、事件契约等） |\n| M2b-1 UX 评审 | P0=2/P1=4（任务中心分段劫持、取消误报 E_TIMEOUT 等） |\n| 修复后代码 Gate | **P0=0/P1=0**，298/298 测试、Release 0 warning、M2a 不退化 |\n| 修复后 UX Gate | **P0=0/P1=0**，M2a selfcheck 11/11、任务中心/批量确认/事件实测通过 |\n\n## 4. 验证

- 构建 0 warning / 0 error；vstest **289/289**（Core 71 + Host 218）。
- 前端 `npm run build` 通过；CDP 自测任务中心/批量/确认/取消/重试。
- 截图：`docs/images/m2b/task-history.png`、`clone-dialog.png`、`clone-job.png`。

## 5. 未完成（M2b-2）

1. **审计完整表**：SQLite `operation_log` 全字段、查询/导出、≥90 天保留（当前 JSONL audit-lite + job 关联）。
2. **备份 ref 恢复**：按 `refs/backup/sync/*` 选择恢复（当前仅展示 backupRef；无 ref 恢复参数）。
3. **RESTORE/TAG/CREATE 的 UI 入口**：BatchBar 目前 4 个批量键（SYNC/CREATE/Clone/Delete）；RESTORE/TAG 仅后端 API。
4. **EMPTY 首推建 upstream**、离线 FETCH 降级、写前 journal/崩溃 reconcile、备份保留清理任务。
5. **AC 覆盖**：AC-OP-01…32、AC-REPO、AC-UI-10c 的逐条证据与故障注入报告。
6. 批量 SYNC 的 dryRun/preview 实际确认条、批量任务取消 UI 全链路复验。

---

## 6. M2b-2a（2026-11-14 18:15）

| 能力 | 实现/实测 |
|---|---|
| operation_log SQLite | `%LOCALAPPDATA%\MbdGitTool\db\mbd.db`（WAL）；JSONL 双写兼容；查询/导出实测可用。**更正：早期证据中的“521 行真实”包含预隔离测试行，不作为生产数据证据；测试隔离修复后不再新增真实库行。** |
| 查询/导出 | `getOperationLog`（过滤/分页）；`exportOperationLog` CSV/JSON → `%LOCALAPPDATA%\MbdGitTool\exports\`；导出实测可用（历史行含测试污染，口径已更正） |
| 备份 ref | `getBackupRefs` 列出 `refs/backup/sync/*`（sha/createdAt）；组件抽屉可查看 |
| 恢复 | `restoreBackupRef`（confirm；当前 HEAD 先备份到 `refs/backup/restore/<ts>`；reset --hard；审计 RESTORE_BACKUP）— 单测覆盖，UI 确认弹窗已接 |
| EMPTY 首推 | 矩阵恢复 `EMPTY.PUSH=Y`；`git push -u origin HEAD` 建 upstream；unborn 拒绝且零远端写 |
| 保留清理 | 启动后台 + `cleanupRetention`：`.trash` 30 天 / backup refs 30 天 / 审计 90 天；实测计数 0（无超期） |
| 测试 | 337/337；Debug/Release 0 warning |

> M2b-2a 阶段 Gate：**通过（阶段 tag）**；P0 闭环，P1-2/P1-4 依据 ADR D-026 书面延后 M2b-2b；337/337 测试，M2b-2 自检 53/53。\n> 待 M2b-2b：journal/reconcile、离线降级完整行为、备份分支身份/恢复 UI 全链路、审计回填/历史污染清理、AC-OP 全量证据。\n> 签收补充（`docs/review-m2b2-signoff.md`）：`cdp-m2b2-realhost-check.mjs` 未随 9db68f9 更新/重跑，依 Gate 报告书面延后 M2b-2b；真实审计当前 528 行（含历史测试污染，测试隔离后 528→528 无新增，生产口径以 JSONL/清理后重建为准）。