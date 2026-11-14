# M2b 故障注入 / AC-SAFE 证据报告（草案）

> 依据冻结规格 `reqs/specification.md` §16.6/§16.9；本报告不是“全绿”结论。
> 生成日期：2026-11-15；基线：`spec-v0.4`，HEAD 见提交记录。
> 说明：未列“实测 PASS”的注入点不得在 M2b final 中声明通过。

## 1. 注入点状态

| 注入点 | 实现 | 证据 | 状态 |
|---|---|---|---|
| `DIR_MOVE_BUSY` | Debug 构建 + `MBD_FAULT_INJECT` | `Delete_FileLocked_WritesPendingAndRetrySucceeds`：pending 生成、目录不变、重试成功 | PASS |
| `INDEX_LOCK`（外部 git 占用） | JobEngine 预检 `.git/index.lock` | `RunBatch_ExternalIndexLock_ReturnsBusyWithoutWriteOrDeletingLock` | PASS |
| `CRASH_AT_ITEM_2` | 单测模拟 journal STARTED 无 DONE + 重启 reconcile | `M2b2bAuditJournalTests`（NOT_APPLIED/UNKNOWN_APPLIED，仓库零改动） | PARTIAL（未做真实杀进程） |
| 真机 reconcile（NOT_APPLIED） | 追加 STARTED 无 DONE → 重启 Host | 审计 `RECONCILE result=NOT_APPLIED`（历史 id=452，actor=journal-reconciler） | PASS（历史演练） |
| 真机 reconcile（AFTER_WRITE/UNKNOWN_APPLIED） | 未演练 | — | GAP |
| `GIT_NET_AUTH_401` | Debug 注入 GitCli 网络命令 | `Clone_NetAuth401Injection_ReturnsAuthFailed`：E_AUTH_FAILED | PASS |
| `GIT_NET_404` | 已实现 GitCli 注入 | `Clone_NetInjection_ClassifiesError(404)` → E_REMOTE_NOT_FOUND | PASS |
| `GIT_NET_TIMEOUT` | 已实现 GitCli 注入 | `Clone_NetInjection_ClassifiesError(timeout)` → E_NETWORK | PASS |
| `DISK_SPACE_LOW` | Debug 注入 Clone 预检 | `Clone_DiskSpaceLowInjection_ReturnsDiskFull`：E_DISK_FULL、零目录 | PASS |
| `GIT_SLOW`（取消确定性） | 已实现 2s delay（Debug） | 取消测试仍为通用用例 | PARTIAL |
| `DISK_WRITE_FAIL` | 未实现 | — | GAP |
| `CRASH_BEFORE_WRITE` / `CRASH_AFTER_WRITE` | 未实现真实注入 | — | GAP |
| `G-WIRE-REST` | 测试内 HttpListener Gitea REST 桩 | `Bind_UsesGiteaRestAndReportsMissingRepos`：两仓返回/缺仓 2；只记录 method/url/status | PASS |
| `G-WIRE-GIT` | 测试内 git http-backend 代理 | `GWireGit_CloneThroughProxy...`：clone 成功、记录 GET smart-HTTP、不记录凭据 | PASS |

## 2. AC-SAFE 覆盖

- AC-SAFE-01/02（确认前零写/预览）：已有 Q/A 单测与自检，**PASS**；
- AC-SAFE-03（typed confirm）：DELETE/SYNC/DIVERGED 用例 **PASS**；
- AC-SAFE-04（删除仅本地/远端零写）：DELETE 单测断言无远端操作，但缺 `G-WIRE-GIT` 记录，**PARTIAL**；
- AC-SAFE-05/06/07（脱敏/诊断/转储）：日志脱敏单测存在；转储默认关闭已写规格，真实崩溃转储未演练，**PARTIAL**；
- AC-SAFE-08/09（journal/reconcile）：单测 + 真机 NOT_APPLIED；**AFTER_WRITE/UNKNOWN_GAP**；
- AC-SAFE-10/11（危险扩展名/路径穿越）：已有单测；**PARTIAL**（未纳入本报告注入矩阵）；
- AC-SAFE-12/13（既有仓 token/header-only）：header-only 由安全单测覆盖；抓包/argv 三件套未生成，**PARTIAL**。

## 3. 结论

**故障注入报告尚未通过 M2b final DoD**。剩余阻断：

1. 补 `GIT_NET_404/TIMEOUT`、`GIT_SLOW` 专项用例，实现 `CRASH_AFTER_WRITE` 注入；
2. 实现 `G-WIRE-REST`/`G-WIRE-GIT` 并生成安全证据三件套；
3. 真机演练 `CRASH_AFTER_WRITE` → `UNKNOWN_APPLIED`（只读、不猜成功）；
4. 按 AC-SAFE-01…13 逐条补期望/实测/证据，更新本报告为 PASS 后才可作为 Gate 材料。
