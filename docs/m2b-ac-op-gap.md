# M2b AC-OP 全量证据矩阵（截至 m2b-2b / 2026-11-14）

> 用途：M2b final 的逐条验收台账。状态：**COVERED**=有自动化/真实证据；**PARTIAL**=有实现/部分用例但缺 AC 指定断言；**GAP**=未实现或无从验证。
> 当前结论：**M2b 未完成**；`m2b-2b` 只是阶段 tag。测试基线 423/423（v1.0）；真机 reconcile NOT_APPLIED 已验证；故障注入报告见 `docs/m2b-fault-injection-report.md`（未通过）。证据来源：`tests/MbdGitTool.Host.Tests`、`ui/scripts/cdp-*.mjs`、`docs/m2b*.md`。

| AC | 主题 | 状态 | 现有证据 | 缺口/下一步 |
|---|---|---|---|---|
| AC-OP-01 | 批量 Clone 4/4 + 幂等重跑 | COVERED | `RunBatch_Clone_FourRepos_AllSucceed_AndRerunIsIdempotent`：4/4 OK、重跑 4 SKIPPED、`.git` mtime 不变 | — |
| AC-OP-02 | Clone 预检（磁盘/长路径/凭据/远程存在） | COVERED | 长路径/磁盘/凭据/`ls-remote` 零写；`Clone_NetInjection_ClassifiesError` 401/404/timeout；`Clone_DiskSpaceLowInjection` | — |
| AC-OP-03 | Clone 部分失败 2/4 + 仅重试失败 + 清除确认 | PARTIAL | `RunBatch_Clone_PartialFailure_JobPartialAndRetryOnlyFailed`：2 OK/2 FAILED、PARTIAL、retry 仅 2 项 | “清除失败态”显式确认/审计 |
| AC-OP-04 | Clone 401/403/404 不重试、网络退避≤2 | COVERED | 401/403→E_AUTH_FAILED、404→E_REMOTE_NOT_FOUND、timeout→E_NETWORK；`Clone_NetInjection...` | 退避时长未断言（实现无重试，满足“不重试”） |
| AC-OP-05 | 目标目录已存在 repair 不覆盖 | COVERED | Clone 存在→SKIPPED；REPAIR/RECLONE 测试+UI 入口；失败回滚保留备份 | 部分失败专项（后续） |
| AC-OP-06 | 批量 Pull `--ff-only` 无 merge commit | COVERED | `RunBatch_Pull_FastForward_NoMergeCommit` + 批量任务中心 | — |
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
| AC-OP-18 | SYNC untracked 默认保留/显式删除 dry-run | COVERED | 默认保留；`cleanUntracked` 强确认 + 后端测试；SYNC 弹层新增 `clean-untracked` 勾选 + dryRun 提示 | UI CDP 证据（后续） |
| AC-OP-19 | 拒绝覆盖本地独有分支/UPSTREAM_GONE | COVERED | `RunBatch_Sync_RemoteBranchGone...`、`Sync_LocalOnlyBranch_IsRejectedAndKept`（NO_UPSTREAM 本地独有拒绝+保留）、`FixUpstream_Supports...` | UPSTREAM_GONE 的 fetch --prune 状态聚合（后续） |
| AC-OP-20 | 删除到 `.trash` 30 天可恢复/realpath | COVERED | `RunBatch_DeleteMovesToTrash_AndRestoreRoundTrips` | — |
| AC-OP-21 | 删除预检四项阻断 | COVERED | `Delete_Preflight_DirtyUntrackedAheadStash...` | — |
| AC-OP-22 | `.trash` 保留可配 + 清理 + 扫描排除 | COVERED | `RetentionService_...`、`CleanupExpired...` | 扫描排除断言 |
| AC-OP-23 | 占用文件 DELETE_PENDING 保留 | COVERED | `Delete_FileLocked...` + `DeletePendingService` 列表/取消/30 天 GC 测试 + UI 待处理列表 | 真实占用（非注入） |
| AC-OP-24 | CREATE dry-run 字段 + check-ref-format | COVERED | dry-run 输出目标分支/本地重名/远程存在+sha/当前状态/超前落后/基线 HEAD/非法名；`Create_DryRun_ReportsRemoteState...` | “远程已删”处置选择（后续） |
| AC-OP-25 | CREATE 逐仓/重名预览/撤销/基线 ref | COVERED | baseline_ref + `CREATE_UNDO` job（任务中心/取消/journal）+ retention 30 天 + UI 撤销按钮；测试 | CDP 证据（后续） |
| AC-OP-26 | 任务中心队列/持久化/重试/重启不重复 | PARTIAL | `JobEngineTests`、`M2b-1 selfcheck 68/68` | `CRASH_AT_ITEM_2` 作业级确定性终止用例 |
| AC-OP-27 | 合作式取消/CANCELLED/未开始原状/重扫 | PARTIAL | `Cancel_*` 单测；selfcheck 真实取消在快速完成时跳过 | 严格取消 selfcheck / GIT_SLOW 作业级用例 |
| AC-OP-28 | 审计字段/查询/导出/90 天 | COVERED | `M2b2RouterApiTests`、`M2b2bAuditJournalTests`、realhost | 故障注入证据 |
| AC-OP-29 | 同组件串行/外部 index.lock BUSY | COVERED | `RepoLock_IsSharedAndSerializesSameRepository`；`RunBatch_ExternalIndexLock_ReturnsBusyWithoutWriteOrDeletingLock`：E_REPO_BUSY、锁保留、HEAD 不变 | 单仓按钮路径同样校验（可选） |
| AC-OP-30 | `.trash` 还原 + backup ref 恢复 + 冲突预览 + 审计 | PARTIAL | 还原/恢复/脏工作区/untracked 阻断/审计 | 目标已存在/远程已变动/备份过期冲突预览 |
| AC-OP-31 | 组件纳入：四仓校验/展示将 clone 仓 | PARTIAL | `bindComponent` + G-WIRE REST 桩测试（缺仓 2）；UI 弹层 | BINDING_ERROR/组件写入/CDP |
| AC-OP-32 | 组件移出：删除 vs 停止管理 + 看板/DB 一致 | PARTIAL | STOP_MANAGE 过滤；LOCAL_DELETE job + partial 失败（E_DELETE_PARTIAL）；审计；UI | CDP/已移出视图证据 |
| AC-REPO-01…08/11 | 单仓修复/EMPTY 首推等 | PARTIAL | 单仓动作/EMPTY 首推已有实现与部分测试 | 逐条 AC 证据 |\n| AC-REPO-09 | NO_UPSTREAM 三子型修复 | COVERED | `FIX_UPSTREAM` PUBLISH/SET/CLEAR + UI + 测试 | — |\n| AC-REPO-10 | DETACHED 建分支 | COVERED | `FIX_DETACHED` + UI + 测试 | — |
| AC-SAFE-01…04 | 零写/确认/删除零远端 | COVERED | 单测 + 注入矩阵 | — |\n| AC-SAFE-05/06 | 转储/诊断包 | PARTIAL | 默认关闭/脱敏实现 | 真实转储演练 |\n| AC-SAFE-07/08/09 | 脱敏/journal/崩溃 reconcile | COVERED | 真实进程 Kill 写前/写后 + 审计 result | — |\n| AC-SAFE-10/11/12 | 危险扩展/路径穿越/既有 token | PARTIAL | 已有单测 | 安全证据三件套 |\n| AC-SAFE-13 | 凭据 header-only/无 argv | PARTIAL | 测试/G-WIRE 记录 | 归档抓包 |
| AC-UI-10c | 取消·job UI | COVERED | M2b-1 selfcheck、任务中心 | — |\n| AC-UI-16 | 组件纳入/移出入口 | PARTIAL | 组件管理弹层（纳入/停止/本地删除/已移出/pending）、bind 测试 | CDP 证据 |\n| AC-OP-34 | 证据门禁 | PARTIAL | manifest 有 suite/checks/AC 映射；result.json 补齐 target/url/reachable/commit | AC 台账逐条 + 三方签字 |\n| AC-OP-35 | 故障注入可复现 | PARTIAL | 注入矩阵全 PASS；Release 忽略注入未测 | Release 构建验证 |

## 仍需完成（M2b final 阻断）

1. AC 逐条证据/台账：manifest 的 AC 映射与真实产物路径、新 UI（Repair/Reclone/撤销/pending/upstream/detached）CDP；crash drill 脱离预构建 worker。
2. 故障注入报告转 PASS + AC-SAFE-05/06/10/11/12 逐条证据 + `docs/evidence/security` 三件套。
3. AC-OP-26 `CRASH_AT_ITEM_2`、AC-OP-27 严格取消、AC-OP-30 冲突预览。
4. LOCAL_DELETE partial 失败语义/UI、BINDING_ERROR 与组件绑定写入。
5. AC-OP-18 untracked 显式清理、AC-OP-19 三子型 E2E、AC-OP-24 dryRun 字段。
6. 全 AC 台账 → final Gate 签字 → `m2b` tag；M2 = M2a ∧ M2b。