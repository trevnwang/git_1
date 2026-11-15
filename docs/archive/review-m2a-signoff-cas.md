# M2a 签收复评 — AC-SAFE-15 桥层 CAS（P1-1 闭环核验）

> 对象提交：`458e39d`（fix(m2): bridge-level CAS baseline and fixture packaging (final P1-1)，2026-11-14 15:39:38 +0800，17 个本地提交之上）
> 评审方式：只读静态核对 + 定点/全量测试实跑；未改代码、未改真实仓库，唯一产出为本文件。
> 基线口径：SRS v0.3.1-r2 AC-SAFE-15 Then①；ADR D-025 §5；前轮阻断 P1-1（`docs/review-m2a-signoff-security.md` §6.2）。

## 0. 结论摘要

| 项 | 结论 |
|---|---|
| P1-1（桥层 CAS 基线丢失） | **闭环 PASS**：refresh 前以 `_deliveredIndex` 为基线，refresh 后 HEAD 变化在动作前返回 `E_CONCURRENT_CHANGE`，成功后刷新基线 |
| 四动作回归测试 | **PASS**：PULL/CHKOUT/COMMIT/PUSH 全 `E_CONCURRENT_CHANGE`，本地/远端 HEAD 零改动 |
| 全量测试 | **272/272 通过**（Core 71 + Host 201，0 失败/0 跳过），构建 0 warning/0 error |
| 新 P0/P1 | **无**（4 条 P2 见 §3） |
| 是否建议打 `m2a` tag | **建议打**，tag 目标 `458e39d`；P2 记 M2b/后续，不阻断 |

## 1. 核对项①：`runRepoAction` 以 `_deliveredIndex` 作 CAS 基线（静态）

文件：`src/MbdGitTool.Host/Bridge/HostCommandRouter.cs`（HEAD 行号）

| 要求 | 静态证据 | 判定 |
|---|---|---|
| 记录“用户实际看到的快照” | `:65` `private volatile SnapshotIndex? _deliveredIndex;`；`getSnapshot` 成功后 `:227` 赋值；`refresh` 成功后 `:234` 赋值 | PASS |
| 基线在 refresh **之前**捕获 | `runRepoAction`：`:262` `var requestIndex = _deliveredIndex ?? _snapshots.Index;`，`:263-268` 经 `TryGetRepoHead(requestIndex,…)` 取请求时刻 HEAD；`:270` 才 `await _snapshots.RefreshAsync(...)` | PASS |
| refresh 后 HEAD 变化 → **动作前**拒绝 | `:272-283` 用 refresh 后 `_snapshots.Index` 重取 HEAD 并比较（含 `!hasRefreshedHead` 仓消失）；`:285-296` 审计 1 行；`:298-309` 返回 `E_CONCURRENT_CHANGE`（details 含 `snapshotHead/currentHead/requestId`）；`:315` 之后才调 `_actions.RunAsync` → 拒绝路径零写、无网络 git | PASS |
| 成功后刷新基线 | `:329-335`：`InvalidateCaches()` → `RefreshAsync(...)` → `_deliveredIndex = _snapshots.Index`，下一动作不因旧基线被误拒 | PASS |
| 服务层 CAS 未退化 | `RepoActionService.cs:385-396` 写前 CAS + `:1060-1079` `CheckSnapshotHead`；与桥层构成“交付快照→refresh”（桥层）与“refresh→执行”（服务层）双段覆盖 | PASS |
| 语义边界：EMPTY/unborn | `:478-496` `TryGetRepoHead` 对“仓在但 HEAD 为空”返回 true 且 `head=null`，故 EMPTY 态外部首次提交也因 `null != sha` 判并发变化 | PASS |

## 2. 核对项②：回归测试与实跑

测试：`tests/MbdGitTool.Host.Tests/RepoActionSecurityTests.cs::Router_ExternalHeadChange_RejectsAllWritesBeforeAnyAction`（`:771-812`）

- `:779-782` 先真实 `getSnapshot` 固定交付快照与 `head0/remote0`；`:785-788` 外部工具 commit 推进本地 HEAD（断言 `external != head0`）。
- `:790-811` PULL/CHKOUT/COMMIT/PUSH 逐个经真实 `HostCommandRouter.DispatchAsync` 执行：断言 `Ok=false`、错误码 == `E_CONCURRENT_CHANGE`、details 含 `snapshotHead/currentHead`、本地 HEAD == `external`、bare 远端 HEAD == `remote0` → **四动作全拒 + 本地/远端零改动** 成立。
- 同一 router 连续 4 次均被拒：隐含“拒绝不回写基线”；若错误推进基线，第 2 个动作起可能被执行。
- 测试敏感性（非恒真）：前轮黑盒在同一场景下旧实现 PULL 返回 `E_ACTION_NOT_ALLOWED`、**PUSH 真执行并推进远端**（`review-m2a-signoff-security.md` §6.2）；该测试对 P1-1 回归敏感。
- 场景与 AC-SAFE-15 Then① 及 ADR D-025 冻结断言一致，且为桥级（走 `DispatchAsync`，非仅服务层）。

实跑（本机：git 2.56.1、dotnet 10.0.404；运行前确认无 MbdGitTool Host 进程，无需杀进程；测试以 `TempWorkspace` + `TestAudit` 临时审计根隔离）：

- 定点：`dotnet test tests\MbdGitTool.Host.Tests\MbdGitTool.Host.Tests.csproj --filter FullyQualifiedName~Router_ExternalHeadChange_RejectsAllWritesBeforeAnyAction` → **1/1 通过**（5s）。
- 全量：`dotnet test MbdGitTool.sln -c Debug` → **272/272 通过**（Core 71 + Host 201，失败 0、跳过 0），退出码 0；`TreatWarningsAsErrors=true` 下构建 0 warning/0 error。
- 评审前后 `git status --short` 为空（本文件除外）；未改动被测代码与真实仓库。

## 3. 核对项③：闭环结论、新问题与 tag 建议

### 3.1 新问题

- P0：无。P1：无。
- P2（不阻断，建议 M2b/后续修）：
  1. **P2-1 CAS 失败开放**：`hasRequestHead=false`（交付快照不含该仓：从未 `getSnapshot`/仅只读命令建索引、或快照因扫描错误丢组件）时跳过桥层 CAS，直接按 refresh 后状态执行。正常 UI 必先 `getSnapshot`，UI 不可触达；但桥是安全边界，建议 fail-closed（无交付基线 → `E_CONCURRENT_CHANGE`/要求 refresh）。
  2. **P2-2 成功路径 refresh 的副作用**：动作成功后在同一 120s 预算内全量重扫；若 `RefreshAsync` 超时/抛异常，已成功写入的应答会变成 `E_TIMEOUT`/`E_INTERNAL`（审计 ok=true），且每次动作多一次全量扫描。建议刷新失败不覆盖成功结果（返回成功 + 标记 stale，由 UI 再取）。
  3. **P2-3 测试证据粒度**：`:809-810` 仅断言本地/远端 HEAD；建议补分支/index/工作区 `status --porcelain`、远端 `for-each-ref`、审计恰 4 行、拒绝路径无写类 git argv（AC 原文为零 refs/index/工作区，实现层“动作前 return”可静态佐证，测试可加强）。
  4. **P2-4 基线语义**：成功后基线 = 动作后服务端快照（`:334`）；若 UI 尚未渲染该快照，严格意义上“用户所见”与基线存在极窄窗口（变化源为用户自身动作，无安全后果），建议验收文档注明。

`docs/m2a-ac-report.md:21` 的 CAS 陈述（桥层 `_deliveredIndex`、四动作 `E_CONCURRENT_CHANGE`、零改动、272/272）与代码/实测一致；“本地/远端 refs 零改动”比测试实际断言（HEAD）更强，见 P2-3。

### 3.2 判定与建议

- **P1-1 闭环**：桥层已以 refresh 前“用户请求快照 HEAD”为 CAS 基线，refresh 后 HEAD 变化在矩阵/动作/网络 git 之前返回 `E_CONCURRENT_CHANGE`，成功动作后刷新基线；AC-SAFE-15 Then① 在桥层重新成立，服务层 CAS 保留。
- **建议打 `m2a` tag**，目标提交 `458e39d`；打 tag 前建议仅提交本评审文件/文档，不改代码。P2-1/P2-2 记入 M2b 待办即可。
- 范围声明：本轮仅复评 CAS 桥层；CDP 自测与真实 Gitea E2E 沿用 Gate 既有证据（未复跑），不影响本项结论。
