# M2b AC-OP 全量证据矩阵（截至 m2b-2b / 2026-11-14）

> 用途：M2b final 的逐条验收台账。状态：**COVERED**=有自动化/真实证据；**PARTIAL**=有实现/部分用例但缺 AC 指定断言；**GAP**=未实现或无从验证。
> 当前结论：**M2b 未完成**；`m2b-2b` 只是阶段 tag。测试基线 420/420；真机 reconcile NOT_APPLIED 已验证；故障注入报告见 `docs/m2b-fault-injection-report.md`（未通过）。证据来源：`tests/MbdGitTool.Host.Tests`、`ui/scripts/cdp-*.mjs`、`docs/m2b*.md`。

| AC | 主题 | 状态 | 现有证据 | 缺口/下一步 |
|---|---|---|---|---|
| AC-OP-01 | 批量 Clone 4/4 + 幂等重跑 | COVERED | `RunBatch_Clone_FourRepos_AllSucceed_AndRerunIsIdempotent`：4/4 OK、重跑 4 SKIPPED、`.git` mtime 不变 | — |
| AC-OP-02 | Clone 预检（磁盘/长路径/凭据/远程存在） | PARTIAL | 已实现长路径>230/磁盘<256MB/凭据/`ls-remote` 远端存在预检零写；`M2b2bClonePreflightTests` 覆盖长路径/目标存在 | 远端 401/404 专项与完整预检表证据 |
| AC-OP-03 | Clone 部分失败 2/4 + 仅重试失败 + 清除确认 | PARTIAL | `RunBatch_Clone_PartialFailure_JobPartialAndRetryOnlyFailed`：2 OK/2 FAILED、PARTIAL、retry 仅 2 项 | “清除失败态”显式确认/审计 |
| AC-OP-04 | Clone 401/403/404 不重试、网络退避≤2 | PARTIAL | 已实现 401/403→E_AUTH_FAILED、404→E_REMOTE_NOT_FOUND、网络→E_NETWORK（仅网络 retryable） | 自动退避≤2 的专项测试 |
| AC-OP-05 | 目标目录已存在 repair 不覆盖 | PARTIAL | Clone 目标存在→SKIPPED；REPAIR/RECLONE 后端 + 测试（备份/回滚）；UI 入口未接 | CDP/UI 入口 + 部分失败 |
| AC-OP-06 | 批量 Pull `--ff-only` 无 merge commit | PARTIAL | `RunBatch_Pull_FastForward_NoMergeCommit`：HEAD=remote、无 merge commit | 多仓批次证据 |
| AC-OP-07 | Pull 非快进拒绝+手动提示 | COVERED | `Pull_NonFastForward_ReturnsNonFfAndDoesNotMerge` | 批量口径复验 |
| AC-OP-08 | 批量 Fetch 成功/失败逐仓 | COVERED | `RunBatch_Fetch_...`、任务中心 realhost | — |
| AC-OP-09 | 批量 Checkout 5 前置态 | PARTIAL | 支持远端分支建跟踪分支；`RunBatch_Checkout_Branch_Succeeds_AndDirtyRepoBlocked` 覆盖成功+dirty 阻断 | 5 态（SYNCED/BEHIND/DIRTY/DETACHED/UNMERGED）逐用例 |
| AC-OP-10 | Checkout dirty 阻断/stash | COVERED | `Checkout_WithStash...`、`Checkout_Dirty...` | 批量口径 |
| AC-OP-11 | 切 Tag 四仓成功/detached/baseline | **DEFERRED（D-029）** | 检出语义 + `RunBatch_Tag_FourRepos_MixedWritesFourBaselineRows`：3 检出/1 跳过、4 行 baseline、tag_object_sha≠commit | UI baseline 展示（可选） |
| AC-OP-12 | 缺 Tag：3 检出+1 跳过+TAG_DRIFT | **DEFERRED（D-029）** | 四仓混合测试：3 OK/1 SKIPPED、SIL 行 `tag_present=0`、commit=NULL | 组件 TAG_DRIFT 聚合断言（可选） |
| AC-OP-13 | Tag 被移动 fetch --force + 告警 | **DEFERRED（D-029）** | — | — |
| AC-OP-14 | annotated tag `^{commit}`/tag_present | **DEFERRED（D-029）** | annotated 测试 + 四仓测试断言 tag_object_sha≠commit_sha、tag_present=1 | — |
| AC-OP-15 | SYNC BEHIND/DIRTY：备份→stash→确认→reset | COVERED | `RunBatch_Sync_Diverged...`、`DryRun_Sync...` | LFS pull 未覆盖 |
| AC-OP-16 | SYNC DIVERGED 丢失清单/typed confirm/可恢复 | COVERED | `SyncPreview_Diverged_ShowsLossListFields`：未推送 commit 列表/untracked 列表/stash 条数/影响仓；typed confirm、备份 ref 恢复已有用例 | 四仓聚合展示（当前逐仓 message） |
| AC-OP-17 | SYNC 跳过表 UNMERGED/IN_PROGRESS/… | COVERED | `Sync_Preflight_MatrixBlockedStates...`、单源矩阵 | — |
| AC-OP-18 | SYNC untracked 默认保留/显式删除 dry-run | PARTIAL | 默认 stash -u 保留；`cleanUntracked` 显式清理需输入组件 ID 强确认（`Sync_Untracked...`）；dry-run 列出 untracked | UI 勾选项未接 |
| AC-OP-19 | 拒绝覆盖本地独有分支/UPSTREAM_GONE | PARTIAL | `RunBatch_Sync_RemoteBranchGone_RejectedZeroWrite`：远端 main 删除→E_REF_NOT_FOUND、零写/零备份 | 本地独有分支/UPSTREAM_GONE 三子型完整用例 |
| AC-OP-20 | 删除到 `.trash` 30 天可恢复/realpath | COVERED | `RunBatch_DeleteMovesToTrash_AndRestoreRoundTrips` | — |
| AC-OP-21 | 删除预检四项阻断 | COVERED | `Delete_Preflight_DirtyUntrackedAheadStash...` | — |
| AC-OP-22 | `.trash` 保留可配 + 清理 + 扫描排除 | COVERED | `RetentionService_...`、`CleanupExpired...` | 扫描排除断言 |
| AC-OP-23 | 占用文件 DELETE_PENDING 保留 | PARTIAL | `Delete_FileLocked...`：pending JSON/FAILED/E_DELETE_PENDING/重试成功 | 真实占用（非注入）+取消/GC |
| AC-OP-24 | CREATE dry-run 字段 + check-ref-format | PARTIAL | dry-run 输出目标分支/本地重名/远程存在/基线 HEAD/非法名提示；`RunBatch_CreateBranchAndTag...` 断言 | 各仓状态/超前落后完整表 |
| AC-OP-25 | CREATE 逐仓/重名预览/撤销/基线 ref | COVERED | 基线 ref + `undoCreate` 测试（分支删除/零改动） | UI 撤销入口/CDP |
| AC-OP-26 | 任务中心队列/持久化/重试/重启不重复 | COVERED | `JobEngineTests`、`M2b-1 selfcheck 68/68` | — |
| AC-OP-27 | 合作式取消/CANCELLED/未开始原状/重扫 | COVERED | `Cancel_*`、M2b-1 realhost | — |
| AC-OP-28 | 审计字段/查询/导出/90 天 | COVERED | `M2b2RouterApiTests`、`M2b2bAuditJournalTests`、realhost | 故障注入证据 |
| AC-OP-29 | 同组件串行/外部 index.lock BUSY | COVERED | `RepoLock_IsSharedAndSerializesSameRepository`；`RunBatch_ExternalIndexLock_ReturnsBusyWithoutWriteOrDeletingLock`：E_REPO_BUSY、锁保留、HEAD 不变 | 单仓按钮路径同样校验（可选） |
| AC-OP-30 | `.trash` 还原 + backup ref 恢复 + 冲突预览 + 审计 | COVERED | `M2b2BackupRefTests` 50/50、M2b-2 selfcheck 53/53 | — |
| AC-OP-31 | 组件纳入：四仓校验/展示将 clone 仓 | PARTIAL | `bindComponent`（Gitea 四仓校验/缺仓）、UI 组件管理弹层 | 真实 Gitea/CDP 证据、缺仓纳入断言 |
| AC-OP-32 | 组件移出：删除 vs 停止管理 + 看板/DB 一致 | PARTIAL | STOP_MANAGE 过滤、LOCAL_DELETE 串联 DELETE job + is_deleted、UI 入口 | 部分失败/重试/审计/CDP |
| AC-REPO-01…11 | 修复类/Repair/Re-clone/detached 建分支等 | GAP | 仅 EMPTY 首推/NO_UPSTREAM | 需逐条实现 |
| AC-SAFE-01…13 | 故障注入/外部并发/崩溃恢复 | PARTIAL | journal/CAS/ignored/credential；真机 reconcile NOT_APPLIED 演练已完成（审计 id=452） | 写中杀进程 UNKNOWN_APPLIED、完整故障注入报告 |
| AC-UI-10c | 取消·job UI | COVERED | M2b-1 selfcheck、任务中心 | — |

## 仍需完成（M2b final 阻断）

1. AC 逐条证据/台账：manifest 的 AC 映射与真实产物路径、新 UI（Repair/Reclone/撤销/pending/upstream/detached）CDP；crash drill 脱离预构建 worker。
2. 故障注入报告转 PASS + AC-SAFE-05/06/10/11/12 逐条证据 + `docs/evidence/security` 三件套。
3. AC-OP-26 `CRASH_AT_ITEM_2`、AC-OP-27 严格取消、AC-OP-30 冲突预览。
4. LOCAL_DELETE partial 失败语义/UI、BINDING_ERROR 与组件绑定写入。
5. AC-OP-18 untracked 显式清理、AC-OP-19 三子型 E2E、AC-OP-24 dryRun 字段。
6. 全 AC 台账 → final Gate 签字 → `m2b` tag；M2 = M2a ∧ M2b。