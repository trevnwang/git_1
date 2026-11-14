# M2b-1 阶段报告：作业内核 + 批量操作 + 任务中心（2026-11-14）

> M2b 第一批：**作业内核（job/job_item/JOB_UPDATED/取消/仅重试失败/持久化）** + **批量 PULL/FETCH/SYNC/CLONE/DELETE/RESTORE/CREATE/TAG** + **任务中心 UI**。
> 状态：**M2b-1 候选，待对抗评审；不宣称 M2b 完成**。完整 M2b 仍缺审计完整表/查询导出/90 天、备份 ref 恢复入口、EMPTY 首推、离线降级、journal/reconcile。

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

## 3. 验证

- 构建 0 warning / 0 error；vstest **289/289**（Core 71 + Host 218）。
- 前端 `npm run build` 通过；CDP 自测任务中心/批量/确认/取消/重试。
- 截图：`docs/images/m2b/task-history.png`、`clone-dialog.png`、`clone-job.png`。

## 4. 未完成（M2b-2）

1. **审计完整表**：SQLite `operation_log` 全字段、查询/导出、≥90 天保留（当前 JSONL audit-lite + job 关联）。
2. **备份 ref 恢复**：按 `refs/backup/sync/*` 选择恢复（当前仅展示 backupRef；无 ref 恢复参数）。
3. **RESTORE/TAG/CREATE 的 UI 入口**：BatchBar 目前 4 个批量键（SYNC/CREATE/Clone/Delete）；RESTORE/TAG 仅后端 API。
4. **EMPTY 首推建 upstream**、离线 FETCH 降级、写前 journal/崩溃 reconcile、备份保留清理任务。
5. **AC 覆盖**：AC-OP-01…32、AC-REPO、AC-UI-10c 的逐条证据与故障注入报告。
6. 批量 SYNC 的 dryRun/preview 实际确认条、批量任务取消 UI 全链路复验。
