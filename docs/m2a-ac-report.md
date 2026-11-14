# M2a 验收证据（13 条出口 AC）— 候选提交状态

> 日期：2026-11-14 15:10（本地）　候选代码：工作区最新（待提交）
> 权威定义：SRS v0.3.1-r2 §13.1.1 + §13.1.2；ADR `reqs/adr/D-025-m2a-m2b.md`。
> 证据层：A=Host 单测/黑盒，B=真实 Gitea E2E，C=WebView2 CDP 真实鼠标键盘，D=静态/截图。
> **签收：独立复评通过（`docs/review-m2a-signoff-ux.md`、`docs/review-m2a-signoff-cas.md`）；tag = `m2a`（目标提交 `458e39d`）。**

## 1. 总表

| # | AC | 状态 | 证据（本轮） |
|---|---|---|---|
| 1 | AC-UI-08 矩阵单源 + tooltip | ✅ | `ui/src/viewmodel.ts` 无 `MATRIX`；UI 消费 Host `getComponentDetail.repos[].actions`（真实 Host：COMMIT/PUSH `requiresConfirm=true`，code `CONFIRM_REQUIRED`）。禁用 title 实测 `MISSING：仓库未克隆 [E_REPO_NOT_LOCAL] → 先 Clone`（紧凑冻结格式）。夹具 `reqs/v0.2/fixtures/m2-matrix.json` 已建 |
| 2 | AC-UI-09 阶段门禁/无静默 | ✅ | M2a 按钮由 Host actions 启用；点击禁用按钮仅 Toast；确认条未确认写调用=0（Gate UX 实测）；真实 COMMIT/PUSH 成功后 Toast/状态栏/审计均变化 |
| 3 | AC-UI-10 前置/失败/进行中/回写/审计 | ✅ | COMMIT 无 staged 禁用 + `E_NOTHING_TO_COMMIT`；PULL 非快进 `E_GIT_NON_FF`（测试）；成功 ≤2s 回写（status 15:08）；每次动作审计一行 |
| 4 | AC-UI-10a COMMIT 确认条 | ✅ | 对话框→确认条 `将提交 1 个文件（已暂存 1）`→确认；真实提交 `73cee5b`；未确认 0 写 |
| 5 | AC-UI-10b PUSH 确认条 | ✅ | 真实 Push：条高 44px，文案 `将推送 1 个提交（main → origin/main）`；确认后 Gitea 远端 `RTCT_Demo_Design/main` = `73cee5b` |
| 6 | AC-UI-11 信息面板 | ✅ | 分支/HEAD/ahead-behind/dirty/已暂存/最近操作/CI 占位；空态/1280 无溢出（前轮 CDP + 本轮截图） |
| 7 | AC-UI-13 焦点闭环 | ✅ | ConfirmDialog 初始焦点在框内、Tab 不逃逸、ESC 归还；ComponentDrawer 关闭回触发卡片；确认条焦点循环 |
| 8 | AC-UI-15 M2b 边界 | ✅ | 批量条/卡片右键/菜单/任务中心统一 `M2b 未接入（批量/作业/审计）`，点击 0 写（代码 + CDP 自测） |
| 9 | AC-SAFE-14 凭据作用域 | ✅ | Gate 安全复评 7 场景 PASS；origin/pushurl 不匹配 `E_CREDENTIAL_SCOPE` 且 0 网络；配置缺失 fail-closed；http 未显式允许 `E_GITEA_INSECURE_HTTP`；URL 作用域 header 仅 canonical base 收到 PAT；token 不出现在 argv/日志/响应 |
| 10 | AC-SAFE-15 read-through + CAS | ✅ | 写前 `RefreshAsync`；**桥层**保留 refresh 前“用户请求快照 HEAD”作为 CAS 基线（`_deliveredIndex`），外部改 HEAD 后四动作均 `E_CONCURRENT_CHANGE`、本地/远端 refs 零改动（`Router_ExternalHeadChange_RejectsAllWritesBeforeAnyAction`）；成功后刷新基线，下一步动作不被误拒 |
| 11 | AC-SAFE-16 ignored 保护 | ✅ | CHKOUT/PULL 使用 `--no-overwrite-ignore`；ignored 文件不被覆盖，拒绝返回 `E_WORKTREE_OVERWRITE`（安全 Gate 实测） |
| 12 | AC-SAFE-17 ref 校验 | ✅ | 不存在 ref `E_REF_NOT_FOUND`；`+main` 拒绝；PULL 显式 branch≠当前分支 `E_INVALID_ARGUMENT`（新增测试 `Pull_ExplicitBranchDifferentFromCurrent_IsRejectedWithoutChanges`） |
| 13 | AC-OP-33 审计-lite | ✅ | `OperationAuditService` 每次动作写 JSONL（`%LOCALAPPDATA%\MbdGitTool\audit\operations.jsonl`）：ts/action/componentId/repoType/state/preHead/postHead/ok/errorCode/durationMs；真实动作证据见文件末尾两行；单测 `OperationAuditServiceTests` 覆盖成功/失败/无 token |

## 2. 关键真实证据

- 真实 Gitea 写：`RTCT_Demo_Design` COMMIT `73cee5b feat: gate final staged commit`，随后 PUSH；远端 `main` = `73cee5b`。
- 审计 JSONL（尾行）：
  - `{"ts":"...07:08:07Z","action":"FETCH","componentId":"RTCT_Demo","repoType":"DESIGN","state":"SYNCED",...,"ok":true,"durationMs":704}`
  - 前一行 COMMIT/PUSH 各一条（成功）。
- 单测：`vstest` **272/272**（Core 71 + Host 201）；构建 0 warning/0 error。
- Gate 安全复评：`docs/review-m2-gate-security.md` 6/6 通过 + N-1 本轮已修。
- Gate UX 复评：`docs/review-m2-gate-ux.md` 的 5 个 Gate-P0（单源矩阵/审计/M2b/AC 定义/状态栏）本轮已逐项修；待最终复评。

## 3. 已知残留（不阻塞 M2a，记录在案）\n\n- `ui/scripts/cdp-m2a-selfcheck.mjs`：10/10 PASS（Host 单源 actions、tooltip、确认条高度/bbox/0 写、状态栏、M2b Toast、审计 actor/requestId）。

| 项 | 说明 |
|---|---|
| staged 文件列表 | COMMIT 显示“已暂存 N/将提交 N”，未列文件名（P1，spec §6.3 建议；不影响安全） |
| 审计落地形态 | M2a 为 JSONL audit-lite；完整 `operation_log` SQLite 表/查询/导出/≥90 天保留归 M2b |
| 取消/job/任务中心/仅重试失败项 | 归 M2b（AC-UI-10c），M2a 仅按钮/M2b 提示 |
| EMPTY 首推 | M2a 按 `EMPTY.PUSH=N` 禁用并提示 M2b；M2b 恢复首推建 upstream |
| `origin/HEAD` 出现在 CHKOUT 候选 | P2 文案/过滤优化 |
| 真实环境 http | 需显式 `MBD_GITEA_ALLOW_INSECURE_HTTP=true`（安全 Gate 要求，默认拒绝） |
