# M2 单仓操作阶段报告（2026-11-14）

> **Gate 状态（2026-11-14 13:25）：三路对抗评审完成，结论「不通过」；`m2-ops` tag 已撤销，本文件不代表已签收。**
> 评审报告：`docs/review-m2-code.md`（P0=1/P1=8）、`docs/review-m2-req.md`（P0=12/P1=10）、`docs/review-m2-ux-fixcheck.md`（P0 闭环 2/8，新 P0=1）。
> 需先修安全 P0（PAT origin 绑定）、确认条/PUSH 确认、焦点、tooltip 错误码、CHKOUT 数据源等并复评通过后才能打 tag。

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

1. 确认条形态（spec FR-ACT-04 的 ≤48px 内联 confirm bar）与 PUSH 发布分支/upstream 文案。
2. CHKOUT 分支选择器接入 remote_ref/Tag 列表、搜索、stash 选项。
3. 禁用 tooltip 错误码、替代动作；状态栏模板/版本号与 Host appVersion 对齐。
4. 批量写操作（SYNC/CREATE/Clone/Delete）、任务中心作业化（job/JOB_UPDATED）。
5. CI 接入（v2）、GitWorker 命名管道迁移、M3 收口。
