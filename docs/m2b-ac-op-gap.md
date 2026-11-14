# M2b AC-OP 全量证据矩阵（截至 m2b-2b / 2026-11-14）

> 用途：M2b final 的逐条验收台账。状态：**COVERED**=有自动化/真实证据；**PARTIAL**=有实现/部分用例但缺 AC 指定断言；**GAP**=未实现或无从验证。
> 当前结论：**M2b 未完成**；`m2b-2b` 只是阶段 tag。测试基线 390/390；真机 reconcile NOT_APPLIED 已验证。证据来源：`tests/MbdGitTool.Host.Tests`、`ui/scripts/cdp-*.mjs`、`docs/m2b*.md`。

| AC | 主题 | 状态 | 现有证据 | 缺口/下一步 |
|---|---|---|---|---|
| AC-OP-01 | 批量 Clone 4/4 + 幂等重跑 | PARTIAL | `RunBatch_Clone_MissingRepo_FromLocalBareRemote`、真实 Gitea CLONE 手工 | 4/4 批次、重跑幂等断言 |
| AC-OP-02 | Clone 预检（磁盘/长路径/凭据/远程存在） | PARTIAL | 已实现长路径>230/磁盘<256MB/凭据/`ls-remote` 远端存在预检零写；`M2b2bClonePreflightTests` 覆盖长路径/目标存在 | 远端 401/404 专项与完整预检表证据 |
| AC-OP-03 | Clone 部分失败 2/4 + 仅重试失败 + 清除确认 | PARTIAL | `retryFailed` 通用用例 | Clone 专项 2/4、清除确认审计 |
| AC-OP-04 | Clone 401/403/404 不重试、网络退避≤2 | PARTIAL | 已实现 401/403→E_AUTH_FAILED、404→E_REMOTE_NOT_FOUND、网络→E_NETWORK（仅网络 retryable） | 自动退避≤2 的专项测试 |
| AC-OP-05 | 目标目录已存在 repair 不覆盖 | PARTIAL | Clone 目标存在→SKIPPED 不覆盖（`M2b2bClonePreflightTests`）；REPAIR/Re-clone 入口未实现 | repair 入口与 E_PATH_EXISTS |
| AC-OP-06 | 批量 Pull `--ff-only` 无 merge commit | PARTIAL | 单仓 `Pull_FastForward`；批量 SYNC/PULL 矩阵 | 批量成功专项/无 merge 断言 |
| AC-OP-07 | Pull 非快进拒绝+手动提示 | COVERED | `Pull_NonFastForward_ReturnsNonFfAndDoesNotMerge` | 批量口径复验 |
| AC-OP-08 | 批量 Fetch 成功/失败逐仓 | COVERED | `RunBatch_Fetch_...`、任务中心 realhost | — |
| AC-OP-09 | 批量 Checkout 5 前置态 | PARTIAL | 单仓 CHKOUT 用例、矩阵 | 5 态批量 E2E |
| AC-OP-10 | Checkout dirty 阻断/stash | COVERED | `Checkout_WithStash...`、`Checkout_Dirty...` | 批量口径 |
| AC-OP-11 | 切 Tag 四仓成功/detached/baseline | PARTIAL | 已改为 TAG=检出：`fetch --force`→`^{commit}`→`checkout --detach`；`RunBatch_CreateBranchAndTag` 断言 detached | 四仓 baseline_snapshot 持久化/DB 列 |
| AC-OP-12 | 缺 Tag：3 检出+1 跳过+TAG_DRIFT | PARTIAL | 缺 Tag → SKIPPED + `E_REF_NOT_FOUND` + TAG_DRIFT 文案（`RunBatch_Tag_MissingRemoteTag...`） | 3 检出+1 跳过四仓混合用例 |
| AC-OP-13 | Tag 被移动 fetch --force + 告警 | PARTIAL | 已实现 `fetch --force origin tag`、旧/新 SHA 比较并输出 tag_moved | 专项移动 tag 用例 |
| AC-OP-14 | annotated tag `^{commit}`/tag_present | PARTIAL | `^{commit}` 解引用并 checkout；`RunBatch_Tag_AnnotatedTag...` | tag_present 字段/DB 记录 |
| AC-OP-15 | SYNC BEHIND/DIRTY：备份→stash→确认→reset | COVERED | `RunBatch_Sync_Diverged...`、`DryRun_Sync...` | LFS pull 未覆盖 |
| AC-OP-16 | SYNC DIVERGED 丢失清单/typed confirm/可恢复 | PARTIAL | `Sync_Diverged_DefaultBlocked...`、backup ref 恢复 | dry-run 表四字段逐项断言 |
| AC-OP-17 | SYNC 跳过表 UNMERGED/IN_PROGRESS/… | COVERED | `Sync_Preflight_MatrixBlockedStates...`、单源矩阵 | — |
| AC-OP-18 | SYNC untracked 默认保留/显式删除 dry-run | PARTIAL | stash -u/untracked 保护测试 | 显式删除入口未实现 |
| AC-OP-19 | 拒绝覆盖本地独有分支/UPSTREAM_GONE | GAP | 无 | 分支/上游场景 |
| AC-OP-20 | 删除到 `.trash` 30 天可恢复/realpath | COVERED | `RunBatch_DeleteMovesToTrash_AndRestoreRoundTrips` | — |
| AC-OP-21 | 删除预检四项阻断 | COVERED | `Delete_Preflight_DirtyUntrackedAheadStash...` | — |
| AC-OP-22 | `.trash` 保留可配 + 清理 + 扫描排除 | COVERED | `RetentionService_...`、`CleanupExpired...` | 扫描排除断言 |
| AC-OP-23 | 占用文件 DELETE_PENDING 保留 | GAP | 无 | Windows 占用场景 |
| AC-OP-24 | CREATE dry-run 字段 + check-ref-format | PARTIAL | `RunBatch_ValidationErrors` | dry-run 表逐字段 |
| AC-OP-25 | CREATE 逐仓/重名预览/撤销/基线 ref | PARTIAL | `RunBatch_CreateBranchAndTag...` | 撤销已创建、基线 ref |
| AC-OP-26 | 任务中心队列/持久化/重试/重启不重复 | COVERED | `JobEngineTests`、`M2b-1 selfcheck 68/68` | — |
| AC-OP-27 | 合作式取消/CANCELLED/未开始原状/重扫 | COVERED | `Cancel_*`、M2b-1 realhost | — |
| AC-OP-28 | 审计字段/查询/导出/90 天 | COVERED | `M2b2RouterApiTests`、`M2b2bAuditJournalTests`、realhost | 故障注入证据 |
| AC-OP-29 | 同组件串行/外部 index.lock BUSY | PARTIAL | `RepoLock_IsSharedAndSerializesSameRepository` | 外部 index.lock→BUSY |
| AC-OP-30 | `.trash` 还原 + backup ref 恢复 + 冲突预览 + 审计 | COVERED | `M2b2BackupRefTests` 50/50、M2b-2 selfcheck 53/53 | — |
| AC-OP-31 | 组件纳入：四仓校验/展示将 clone 仓 | GAP | 无（当前仅扫描发现） | 纳入组件产品入口 |
| AC-OP-32 | 组件移出：删除 vs 停止管理 + 看板/DB 一致 | GAP | 无 | 移出入口/审计 |
| AC-REPO-01…11 | 修复类/Repair/Re-clone/detached 建分支等 | GAP | 仅 EMPTY 首推/NO_UPSTREAM | 需逐条实现 |
| AC-SAFE-01…13 | 故障注入/外部并发/崩溃恢复 | PARTIAL | journal/CAS/ignored/credential；真机 reconcile NOT_APPLIED 演练已完成（审计 id=452） | 写中杀进程 UNKNOWN_APPLIED、完整故障注入报告 |
| AC-UI-10c | 取消·job UI | COVERED | M2b-1 selfcheck、任务中心 | — |

## 仍需完成（M2b final 阻断）

1. Clone 预检/错误分类/部分失败/目标存在（AC-OP-01…05）；Pull/Checkout 批量 E2E（06/09）。
2. Tag 检出/TAG_DRIFT/tag_moved/annotated（11…14）。
3. SYNC 丢失清单逐字段、untracked 显式删除、UPSTREAM_GONE（16/18/19）。
4. DELETE_PENDING（23）；CREATE 撤销/基线（24/25）。
5. 外部 index.lock BUSY（29）；组件纳入/移出（31/32）；AC-REPO-01…11 修复类。
6. AC-SAFE 故障注入报告 + 真机 journal 崩溃 reconcile 演练 + 离线完整语义。
7. 上述完成后重新跑 `m2b` Gate；通过才可打 `m2b`，M2 总出口 = M2a ∧ M2b。
