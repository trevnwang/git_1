# M2 单仓操作阶段报告（2026-11-14）

> **Gate 状态（2026-11-14 14:25 更新）：M2a 待复评（修复中）**；`m2-ops` tag 已撤销，本文件不代表已签收，也**不得**被引用为“M2a/M2 已通过”。
> Gate 复评报告：`docs/review-m2-gate-ux.md`（5 PASS/1 FAIL；G-P0-1…G-P0-5，UX P0 行为层已闭环但 M2a 出口未满足）、`docs/review-m2-gate-security.md`（原 6 项 PASS，新 P1=N-1：AC-SAFE-17 未全绿；N-2 需在验收报告交代）。
> 当前阻断：N-1（PULL 显式分支）、N-2（read-through 重扫）、G-P0-1（矩阵单源/RequiresConfirm）、G-P0-2（审计-lite）、G-P0-3（M2b 边界文案）、G-P0-5（状态栏模板）、G-P1-1（COMMIT 确认条文案）。
> 本轮（gate-fix-docs，仅文档/夹具，不改代码）已补齐 M2a 出口 AC 的**可验收材料**：SRS v0.3.1-r2 **§13.1.2**（AC-UI-10a/10b/10c/15、AC-SAFE-14…17、AC-OP-33 的完整 Given/When/Then）、单源夹具 `reqs/fixtures/m2-matrix.json`、验收证据模板 `docs/m2a-ac-report.md`、CDP 断言清单占位 `ui/scripts/cdp-m2a-selfcheck.mjs`（旧 `cdp-m2-selfcheck.mjs` 作废）。**材料齐全 ≠ AC 通过**；代码侧修复在并行进行，未合入，复评后再另写签收结论。
> 需先关闭上述阻断并在**真实 Host + 真实 Gitea** 上补五键写证据、复跑 CDP/Host 单测后，才能打 `m2a` tag；M2a 通过也不得宣告“M2 交付完成”（M2b 未接入）。

> 阶段：S6（PULL / FETCH / CHKOUT / COMMIT / PUSH 从 M1 只读升级为真实可用）
> 验证环境：真实 Gitea `RTCRepos` + 本地工作区 `artifacts/real-test/workspace`（15 仓，真实提交/推送）。

## 1. 已交付

| 能力 | 实现 |
|---|---|
| FETCH | `git fetch --prune origin`，免确认，短网络操作 |
| PULL | `git pull --ff-only origin <branch>`；非快进返回 `E_GIT_NON_FF`，不执行 merge |
| CHKOUT | 先校验工作区 clean（含 untracked），再 `checkout <branch>` / `checkout --detach <tag>`；脏工作区 `E_DIRTY_WORKTREE` |
| COMMIT | **只提交 index 中已暂存内容**（不再 `git add -A`）；空 staged → `E_NOTHING_TO_COMMIT`；message 必填（≤2000） |
| PUSH | `git push [ -u ] origin HEAD`，禁止 force；失败保留 exitCode/stderr 摘要（脱敏） |
| 安全 | 所有参数走 ArgumentList；PAT 仅经 `GIT_CONFIG_* http.extraHeader` 注入，不进参数/日志/响应；per-repo 锁串行；输出截断 2000 并脱敏 |
| 能力开关 | HostInfo / Snapshot `repoActions=true`；前端按 Host 能力 + 状态矩阵启用 |
| staged 计数 | Snapshot `RepoDto.stagedCount`（porcelain v2 X 列），信息面板显示“已暂存 N”；无 staged 时 COMMIT 禁用 |
| 交互 | COMMIT 输入框、CHKOUT 分支/Tag 输入、PULL/PUSH 常规免确认、E_CONFIRM_REQUIRED 单次重试、成功 Toast/状态栏 lastOp/刷新 |
| 组件详情抽屉 | 双击卡片打开，四仓表 + 最近提交 + CI 占位 |
| 焦点/可访问性 | ConfirmDialog/ActionDialog 焦点陷阱、Esc、关闭后焦点归还触发控件 |

## 2. 真实环境实测（UI，真实鼠标键盘）

| 动作 | 结果 |
|---|---|
| FETCH `RTCT_Demo_Impl` | ✅ 完成，HEAD 85dced5，633ms |
| COMMIT `RTCT_Demo_SIL` | ✅ 生成 `267f953`（UI 对话框输入消息） |
| PUSH `RTCT_Demo_SIL` | ✅ 真实推送到 Gitea：远端 main = `267f953`，Toast 单 ✓ |
| PULL `RTCT_Demo_MIL` | ✅ `e0732ee..41083ef` fast-forward，behind 1→0 |
| CHKOUT `RTCT_Demo_Design` | ✅ 检出 main，HEAD 037211d |
| staged-only COMMIT | ✅ 外部 `git add` 后 UI 提交 `e67eb05`，未跟踪/未暂存文件不进入提交 |
| 组件详情抽屉 | ✅ 四仓状态/最近提交/CI 占位正常 |
| PULL 免确认 | ✅ 不弹确认框，直接执行并回写状态 |

## 3. 对抗评审与修复（docs/review-m2-ux.md）

| 评审 P0 | 处理 |
|---|---|
| UNKNOWN 误禁 FETCH | ✅ UI 矩阵改为 FETCH 允许，其余写操作禁用（与 Host/SRS §7.6 一致） |
| MISSING 被当“未选择” | ✅ `focusedRepo` 返回 MISSING 仓；信息面板/aria/按钮 scope 正确，tooltip 显示 `MISSING：仓库未克隆` |
| COMMIT 无 staged 仍可用 / Host `add -A` 危险 | ✅ Host 只提交 staged；新增 `stagedCount`；UI 无 staged 禁用并显示“已暂存 N”；新增单测 |
| 确认条缺失、PULL 反向确认 | ⚠️ 部分修复：PULL/PUSH 常规免确认；COMMIT/CHKOUT 用输入对话框（未实现 48px 内联确认条，留 P1） |
| 焦点陷阱/焦点归还 | ✅ ConfirmDialog/ActionDialog 已补焦点陷阱/ESC/归还 |
| 禁用 tooltip 无错误码 | ⏳ P1（待补 reasonCode 前缀/替代动作） |
| CHKOUT 无 remote_ref/搜索/stash | ⏳ P1（LocalScanner 已采集 branch/tag 候选，UI 交互增强留后续） |
| 信息面板缺“最近一次操作” | ✅ 新增 `info-last-op` 字段 |

## 4. 回归

- VS MSBuild（Debug/Release）：0 warning / 0 error。
- vstest：**234/234**（Core 71 + Host 163），新增 `Commit_UnstagedChanges_ReturnsNothingToCommit` 等。
- 真实 Gitea 写操作验证：COMMIT/PUSH/PULL/CHKOUT/FETCH 全部成功；UI 0 console error。
- 截图：`docs/images/m2/`（missing 聚焦、staged 提交、PULL 免确认、组件抽屉）。

## 5. 已知未完成（下一阶段）

1. 确认条形态（spec FR-ACT-04 的 ≤48px 内联 confirm bar）与 PUSH 发布分支/upstream 文案。**（Gate 复评：PUSH/COMMIT 确认条已实现且行为 PASS，但 COMMIT 文案未对齐冻结正则 G-P1-1；见 `docs/review-m2-gate-ux.md` §3.1）**
2. CHKOUT 分支选择器接入 remote_ref/Tag 列表、搜索、stash 选项。**（Gate 复评：已实现并在真实 Host 验证；P2 残留 refs 含 `origin/HEAD`）**
3. 禁用 tooltip 错误码、替代动作；状态栏模板/版本号与 Host appVersion 对齐。**（tooltip 已实现，冻结格式按实现回写为 `原因 [E_*] → 替代动作`；状态栏模板 G-P0-5 未交付）**
4. 批量写操作（SYNC/CREATE/Clone/Delete）、任务中心作业化（job/JOB_UPDATED）。**（归 M2b；M2a 构建须明示“M2b 未接入（批量/作业/审计）”，当前仍未改文案 G-P0-3）**
5. 审计-lite / 矩阵单源 / M2a 出口 AC 证据链：见 §6。
6. CI 接入（v2）、GitWorker 命名管道迁移、M3 收口。

## 6. M2a Gate 复评与修复轮记录（2026-11-14）

| 时间 | 事件 | 结论/产物 |
|---|---|---|
| 14:07–14:20 | `docs/review-m2-gate-ux.md` Gate 复评（UX/需求） | 行为层 UX P0 5/5 闭环；但 M2a 出口 5 项 Gate P0 未满足（G-P0-1…5），SRS §13.1.2 缺失、三件证据全缺 → **不建议打 tag** |
| 14:07–14:20 | `docs/review-m2-gate-security.md` Gate 复评（安全/代码） | 原 P0/P1 6/6 关闭；**新 P1 N-1**（AC-SAFE-17 PULL 显式分支）阻断 M2a；N-2（read-through）需在验收报告交代 → **不建议打 tag** |
| 14:19–14:30 | 文档回写（本文件更新者 gate-fix-docs） | 新增 SRS §13.1.2（9 条 AC）、`reqs/fixtures/m2-matrix.json`、`docs/m2a-ac-report.md`、`ui/scripts/cdp-m2a-selfcheck.mjs`（占位）；ADR §5/§6 同步 tooltip 冻结格式与 AC-UI-10a 两步口径；**未执行代码修复/真实写/CDP** |
| 并行中 | 代码修复（安全 N-1/审计/确认标志、矩阵单源/状态栏/M2b 文案） | 未合入；复评前不得更新本文件 Gate 结论 |

> **复评前置**：① 代码修复合入并通过 262+ 单测；② `docs/m2a-ac-report.md` 13 条 AC 状态更新、真实 Gitea 五键写证据齐全；③ `cdp-m2a-selfcheck.mjs` 实现并跑出 exit 0（附输出）；④ N-1/N-2/G-P0-1…5/G-P1-1 关闭或按 ADR 修订口径记录。四项齐备后由 Gate 复评另写报告，本文件才可更新为“M2a 已通过（M2b 未接入）”。
