# M2b-2b 阶段报告（2026-11-14）

> 范围：M2b-2a 延后项 + FR-SAFE-04 + ADR D-026。状态：**候选待 Gate；tag 前不宣称 M2b/M2 完成**。
> 证据环境：真实 RTCRepos + 本地工作区；`388/388` 测试、Debug 0 warning。

## 1. 交付

| 能力 | 实现 |
|---|---|
| 备份 ref 分支身份 | SYNC 备份改为 `refs/backup/sync/<ts>/<branch>/<TYPE>`（branch URL 编码；旧 `<ts>/<TYPE>` 解析为 `LEGACY`/branch=null）；`getBackupRefs` 返回 `branch/format`；UI 组件抽屉显示 `@branch` 或“旧格式” |
| 恢复分支校验 | 新格式备份 branch ≠ 当前分支 → `E_ACTION_NOT_ALLOWED`（details expected/current/branch，零写）；旧格式允许但提示“旧备份无分支身份”；clean/untracked/ignored 阻断不变；安全 ref 毫秒+GUID |
| 审计回填/修复 | SQLite 失败 → `fallback.jsonl`；启动把主 JSONL+fallback 中 DB 缺失行回填（稳定键）；`repairOperationLog {confirm:true}` 重建表并从 JSONL 回填；实测 `removed=858, inserted=340, total=341`（清理历史测试污染） |
| 写前 journal | 单仓五键/批量 item 在 git 写前写 `journal/operations.jsonl` STARTED，成功 DONE(postHead)，失败/取消 FAILED；不阻塞动作主链路 |
| 崩溃 reconcile | 启动扫描 STARTED：HEAD==preHead → `NOT_APPLIED`；HEAD 变化 → `UNKNOWN_APPLIED`（不猜成功）；写 `action=RECONCILE` 审计并回写任务中心 item；只读、不修改仓库 |
| EMPTY 首推 | 有 HEAD 的 EMPTY 首推 `push -u origin HEAD` 建 upstream；unborn UI 禁用并给 `E_REF_NOT_FOUND`（矩阵/单测覆盖） |
| 离线 | 仅显式远程错误/离线标记判离线并禁用 FETCH；健康 `local+gitea` 不误报（realhost 19/19） |
| 保留清理 | `.trash` 30 天 / backup refs 30 天 / 审计 ≥90 天；命令+启动清理；不误删保留期内数据 |
| 安全 ref 可见/可恢复 | `getBackupRefs` 同时列出 `refs/backup/restore/*`（Kind=SAFETY、branch=null）；restore 允许从安全 ref 恢复；cleanup 按时间戳清理安全 ref；UI 显示“安全备份” |
| restore journal | `restoreBackupRef` reset 前写 STARTED，成功 DONE(postHead)，失败/安全 ref 失败 FAILED；测试覆盖 |
| 导出过滤 | `exportOperationLog` 接受 action/componentId/result；UI 已传当前审计过滤（修复自检只发 `{format}` 的问题）；新增 Host 用例断言 result=OK 只导出 1 行且不含 PUSH |
| Clone 预检/分类 | 长路径(>230)/磁盘<256MB/凭据/远端 `ls-remote` 预检零写；401/403→E_AUTH_FAILED、404→E_REMOTE_NOT_FOUND、网络→E_NETWORK（仅网络可重试） |

## 2. 验证

- `vstest` **388/388**（新增安全 ref/journal/回填/导出过滤/Clone 预检用例）；构建 0 warning。
- M2b-2 Mock/CDP 自检 **53/53**；M2a 11/11；M2b-1 68/68。
- realhost 只读自检 **19/19**（真实审计查询/导出、备份 ref 分支/旧格式、无 console error）。
- 真实 SYNC 新 ref：`refs/backup/sync/20261114123736/main/DESIGN` 等 4 条；旧 ref 列表为 LEGACY。
- 真实 journal：SYNC 每仓 STARTED→DONE 成对，含 jobId/jobItemId/preHead/postHead。
- **真机 reconcile 演练**：追加一条无 DONE 的 STARTED（RTCT_RemoteOnly/DESIGN，HEAD 不变）→ 重启 Host → 审计 `RECONCILE result=NOT_APPLIED`（id=452，actor=journal-reconciler），journal 追加 `RECONCILED`，仓库零改动。
- 真实修复：审计表 858→341 行（回填 JSONL 历史；其中仍含 64 行夹具/自检数据，**不作为生产数据证据**）；测试隔离后真实库不再新增。

## 3. 已知风险/未完成

| 项 | 说明 |
|---|---|
| AC-OP 全量证据 | `AC-OP-01…32` 尚未逐条产出 Given/When/Then + 证据；当前为能力/单测/真实环境证据，不是全量 AC 签收 |
| reconcile 真机演练 | 已做“STARTED 无 DONE + 重启”真实演练（NOT_APPLIED）；写中被杀的 UNKNOWN_APPLIED 仍由单测覆盖 |
| 离线降级 | 轻量判定（显式远程错误）；完整离线语义（缓存只读、重试队列）未做 |
| 审计历史污染 | repair 已清理当前真实库；若用户已有旧库仍可手动 repairOperationLog |
| GitWorker 命名管道/CI | 属 M3/v2，不在 M2b |
