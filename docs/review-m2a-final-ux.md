# M2a 最终 UX/需求 Gate 复评（提交 `9d7afd0`）

> 复评角色：独立最终 UX/需求 Gate 复评员
> 复评对象：`9d7afd0 fix(m2): final gate fixes (read-through/CAS, EMPTY.PUSH, audit-lite, single-source matrix, M2b copy, status template)`（2026-11-14 15:08:24 +0800）
> 输入：`docs/review-m2-gate-ux.md`（5 个 Gate-P0）、`docs/m2a-ac-report.md`、`reqs/requirements.md` §13.1.1/§13.1.2（v0.3.1-r2）、`reqs/adr/D-025-m2a-m2b.md`、`reqs/v0.2/fixtures/m2-matrix.json`、`ui/src/**`、`src/**`、`tests/**`、`docs/images/m2/`
> 硬性约束：**只评审**。未修改源码/需求/夹具/真实仓库，未杀掉运行中 Host、未启动替代服务；CDP 交互全部为只读或“注入桥拦截+合成回执”，`runRepoAction` 未向 Host 下发一次（审计文件无 `E_GATE_BLOCKED` 行）。本轮唯一写盘文件 = 本报告。
> 复评时间：2026-11-14 15:08–15:18（本地）。

---

## 0. 结论（放行判决）

| Gate-P0 | 判定 | 一句话依据 |
|---|---|---|
| **G-P0-1 单源矩阵** | **PASS（附 P1）** | `viewmodel.ts` 已无 `MATRIX`；UI 只消费 Host `getComponentDetail.repos[].actions`；真实 Host COMMIT/PUSH `requiresConfirm=true`；tooltip 紧凑格式全匹配；夹具 14×5 与 Host 逐格 0 差异。P1：夹具实际上未被 Host 单测/CDP 消费，且夹具 status 仍为 `SINGLE_SOURCE_PENDING_CODE_ALIGNMENT`。 |
| **G-P0-2 审计-lite** | **FAIL** | 真实动作后 JSONL 确有新增行、无 token、跨重启可查；但缺 `actor`/`request_id` 两个 ADR/SRS 强制字段，且落点是独立 JSONL 而非 `operation_log`，导致“同一 request_id 恰 1 行”不可断言。 |
| **G-P0-3 M2b 边界** | **PASS** | 批量条/菜单/任务中心/卡片快速入口均显示完整“M2b 未接入（批量/作业/审计）”，点击全部有 toast 且 `runRepoAction/getJob/listJobs` 增量 0；无旧“M2 接入”措辞。 |
| **G-P0-4 AC 可验收** | **FAIL** | SRS §13.1.2（9 条 G/W/T）与 §13.4 追溯已补齐；但 `docs/m2a-ac-report.md` 13 条“证据路径自洽”不成立（row 10 写前重扫与代码不符、row 13 指向不存在的“文件末尾两行”、row 8 “CDP 自测”对应脚本仍是 exit 2 占位、row 6 “本轮截图”无归档），且 ADR 要求的三件证据之③不具备可复跑性。 |
| **G-P0-5 状态栏模板** | **PASS** | 真实动作后实测 `最近一次操作：FETCH RTCT_Demo/DESIGN — 成功 · 15:08`，与冻结模板逐字符匹配；失败路径实测 `… — 失败(E_GATE_BLOCKED) · HH:mm` 亦匹配。 |

**最终 Gate 结论：不建议给 M2a 打 tag（不打）。**
**UX/需求 Gate 剩余 P0：2 项**（G-P0-2、G-P0-4）。另有并行安全 Gate 的 R-1（AC-SAFE-15 写前 read-through 未实现，与本报告 G-P0-4 中 row 10 的不实陈述互证）、R-2（审计字段，与本报告 G-P0-2 同源）同向阻断，说明 M2a 出口 13 条并未全绿。

> **一句话**：这是本轮修复质量最高的一次——单源矩阵、确认条、M2b 边界、状态栏、tooltip、CHKOUT 都可复现实测通过；但“审计一条”只实现了自定义 JSONL 的 9 个字段、缺 ADR 冻结的 `actor/request_id`，而“可验收证据链”仍建立在**带不实陈述的报告 + 占位 CDP 脚本 + 未落盘截图**上。这两项属于 M2a 出口 AC 本身，不能用“行为已实现”替代。

---

## 1. 范围、环境、方法与限制

### 1.1 复评环境（真实 Host + 真实 Gitea 夹具）

| 项 | 值 |
|---|---|
| 代码基准 | `git rev-parse HEAD` = `9d7afd0…`（评审基准=提交对象；工作区在复评期间被并行进程改动，见 §1.3） |
| Host | PID 34444，`src\MbdGitTool.Host\bin\Debug\net10.0-windows\MbdGitTool.Host.exe`（15:08:00 启动；该 exe/dll mtime 14:50:52） |
| 页面 | `https://app.local/index.html`，CDP 9222，bundle `assets/index-CjpCjQnj.js`（dist mtime 15:13:20，内容哈希与 15:09 首测一致） |
| 视口 | 1424×861（dpr=1） |
| 数据 | 真实 `RTCRepos`：`RTCT_Demo`（DESIGN/IMPL/MIL=SYNCED，SIL=AHEAD）、`RTCT_Synced`（4×SYNCED）、`RTCT_Incomplete`（2×MISSING）等；本地工作区 `artifacts\real-test\workspace` |
| 数据源标签 | 实测 `snapshot-source = 本地扫描 + Gitea`（非 Mock） |
| 测试量声明 | `docs/m2a-ac-report.md:32` 称 265/265；本轮 Host 运行占 `bin` 无法独立复跑（并行安全 Gate 用 `git archive` 隔离构建复跑得 265/265，本报告不将其作为阻断，但提交内无运行日志/路径） |
| 环境变化（如实披露） | 15:18:28 并行修复进程重建 `bin` 并停止 Host，CDP 9222 随后不可达；本报告全部 CDP 实测值采集于 15:09–15:17（停止前） |

### 1.2 零真实写保证

- 真实 Host 上采用**内存注入桥**：`window.__DSH_TEST_BRIDGE__` 接管 `runRepoAction` → 只记录 args 并回合成 `{ok:false,code:'E_GATE_BLOCKED'}`，**不下发 Host**；读操作 `getSnapshot/getComponentDetail/getRemoteRefs` 仍转发真实 Host。
- 为验证 COMMIT/PUSH 确认条与文案，仅在**注入快照**上定向 patch（`RTCT_Synced/DESIGN stagedCount=3`、`RTCT_Demo/DESIGN state=NO_UPSTREAM`），未改任何真实文件。
- 事后核对：`%LOCALAPPDATA%\MbdGitTool\audit\operations.jsonl` 中 `E_GATE_BLOCKED` 行数 = **0**；真实仓 `RTCT_Demo_Design` HEAD=`73cee5b`、工作区干净，`RTCT_Demo_SIL` 仍为复评前 `staged 1` 状态；本复评未执行 PULL/FETCH/CHKOUT/COMMIT/PUSH 任一真实写。

### 1.3 并行改动与证据污染（不掩盖）

1. 复评期间并行进程（`gate-fix-backend`）在 15:09–15:20 留下了 **10 个未提交文件**，**不属于 `9d7afd0`**；本报告结论只针对提交对象。其中：15:09 起出现 `src/MbdGitTool.Host/Services/RepoActionService.cs` 可空标注 + `tests/MbdGitTool.Host.Tests/*` 测试补充；15:19 后并行进程进一步在工作区（未提交）改动 `HostCommandRouter.cs`（`runRepoAction` 改调 `_snapshots.RefreshAsync` + 透传 `actor/requestId`）与 `OperationAuditService.cs`（JSONL 增加 `actor/requestId` 字段）——方向与本报告 P0-A/P0-B 的修复建议一致，但**尚未提交、未构建、未复评**，不能作为 `9d7afd0` 的通过依据。若该修复落地，应以新提交重新走 Gate。
2. `audit/operations.jsonl` 在复评期间被并行测试/真实验证继续追加（含 `MOCK_CF1001_*`、`CF100A_Demo` 行）。并行安全 Gate 已判定 Host 测试**非 hermetic**、会向真实审计文件写测试行（其 R-3）。因此本报告对“真实动作新增行”只按 **componentId=RTCT_Demo + 时间戳** 与仓 reflog 交叉取证，不以总行数作证。

### 1.4 证据层

| 层 | 用途 | 证据 |
|---|---|---|
| A 真实 Host 只读 CDP | 状态栏、Host actions、tooltip、CHKOUT refs/搜索/stash、M2b 入口、0 写计数、console | 本轮 `node --input-type=module -` 内联 CDP 脚本 3 次（真实鼠标/键盘事件），原始 JSON 见正文 |
| B 注入桥（真实快照+定向 patch） | COMMIT 两步/确认条、PUSH NO_UPSTREAM、args 校验、零写 | 同上（拦截回执 `E_GATE_BLOCKED`） |
| C 代码/契约只读核对 | 单源矩阵、审计字段、状态栏模板、M2b 文案、report 与代码不一致 | 文中 `文件:行号` |
| D 需求/ADR 核对 | §13.1.2/§13.4/ADR §2.2/§3/§5 | 文中 `文件:行号` |

### 1.5 限制（不掩盖）

1. 无法独立复核 Gitea 远端：`git ls-remote` 返回 `Authentication failed`，Gitea API 404（私有仓），App 未暴露 token。`73cee5b` 的远端一致性只能间接证明：本地 `refs/remotes/origin/main`=73cee5b，`.git/logs/refs/remotes/origin/main` 最后一行 `…037211d 73cee5b … update by push`（LastWriteTime 14:50:13），与审计 PUSH 行 14:50:14 吻合。
2. `stageAll` 接受路径未在本次 Host 停机前重跑（并行进程 15:18 重建 bin 并停 Host）；该路径由代码 `ui/src/stores/workbench.ts:1331` 与上一轮 Gate 的 `mock-03-commit-stageall.png` 覆盖，本轮不将其计为新证据。
3. 真实五键中 PULL/CHKOUT 的真实写证据来自更早轮次（`docs/m2-ops-status.md` §2），本轮审计文件内 `CHKOUT` 行数=0、成功 `PULL` 行数=0；`docs/m2a-ac-report.md` 未给出这两键的本轮可复跑证据路径（不构成新 P0，但削弱“五键写证据齐全”的表述）。
4. 本轮 CDP 采集期间 0 console error / 0 exception；无法回溯挂接前的历史 console。

---

## 2. G-P0-1 单源矩阵：PASS（行为/内容），附 P1

### 2.1 实测/静态断言

| 断言 | 判定 | 证据 |
|---|---|---|
| `viewmodel.ts` 无 `MATRIX` | ✅ | `ui/src/viewmodel.ts` 全文无矩阵常量；仅保留 tooltip 文案表 `REASON_HINTS`（`viewmodel.ts:502-515`，不参与 enable/confirm）与映射函数 `mapHostActions/hostAvailability`（`:543-606`） |
| UI 只消费 Host actions | ✅ | `ComponentDetailService.cs:79` 把 `_actions.GetAvailability(repoRef)` 塞进 `RepoDto.Actions`；`ui/src/api.ts:55-56,777,783` 解析；`ui/src/stores/workbench.ts:365-379` 仅在 `hostActionsStatus==='ready'` 时调 `hostAvailability`，`:629-685` 从 `getComponentDetail.repos[].actions` 装载（`:671-672`） |
| 真实 Host COMMIT/PUSH `RequiresConfirm=true` | ✅ | 真实 Host 原始 `getComponentDetail` 实测：`RTCT_Demo` 4 仓与 `RTCT_Synced` 4 仓（SYNCED/AHEAD）COMMIT/PUSH 全部 `requiresConfirm=true`、`reasonCode=CONFIRM_REQUIRED`；`RTCT_Incomplete`（MISSING，禁用）同样 `requiresConfirm=true`、`reasonCode=STATE_BLOCKED`。代码 `RepoActionService.cs:121-123`（`cell=='C' || COMMIT || PUSH`） |
| 注入/夹具下确认规则一致 | ✅ | 夹具 14 态每格 `requiresConfirm = cell=='C' || action∈{COMMIT,PUSH}`；UI Mock 规则同（`ui/src/mocks/workbenchMock.ts:716-717`），与 Host 代码逐格 0 差异 |
| tooltip 紧凑冻结格式 | ✅ | 真实 MISSING 仓 5 键 title 全部匹配 `/^.+ \[E_[A-Z_]+\] → .+$/`（例：`MISSING：仓库未克隆 [E_REPO_NOT_LOCAL] → 先 Clone`）；SYNCED+staged0 的 COMMIT 同样匹配（`[E_NOTHING_TO_COMMIT] → …`） |
| 夹具 14×5 与 Host 逐格一致 | ✅ | 脚本对 `RepoActionService.cs:63-80` 的 14 行编码 × 5 键 = 70 格，按冻结规则展开 `enabled/requiresConfirm/reasonCode` 后与 `reqs/v0.2/fixtures/m2-matrix.json` 比对：**0 差异**；含 `DIVERGED.PULL=disabled`、`EMPTY.PUSH=disabled` 两处 M2a 覆盖 |
| 矩阵与 §7.6 关键口径 | ✅ | `EMPTY=NYCYN`、`DIVERGED=NYCNN`、`NO_UPSTREAM=NYNNY`、`SYNCED=YYYYY`，与 ADR §2.5 裁决一致 |

### 2.2 残留 P1（不改变本项 PASS，但影响“单源”工程化）

1. **夹具未被消费**：Host 单测 `tests/MbdGitTool.Host.Tests/RepoActionAndComponentDetailTests.cs:19-36` 仍硬编码 `ExpectedMatrix`（14×5），**不读** `m2-matrix.json`；UI Mock `ui/src/mocks/workbenchMock.ts:643-658` 亦硬编码 `MOCK_HOST_ACTIONS_FIXTURE`；`ui/scripts/cdp-m2a-selfcheck.mjs` 只读夹具校验 14 态数量、不做 UI 断言。即实际存在 **4 份手写矩阵**（夹具/Host 代码/Host 测试/UI Mock），违反 ADR §5（`reqs/adr/D-025-m2a-m2b.md:162`）“Host 单测与 CDP 共用夹具，禁止第三份手写期望”。四份内容当前一致（本轮脚本比对 0 差异），故不判 FAIL，但“单源”尚无机器约束，后续任一改动会静默漂移。
2. **夹具自声明未对齐**：`m2-matrix.json:16` `status=SINGLE_SOURCE_PENDING_CODE_ALIGNMENT`，`:24` 仍写“截至提交 1afa014 Host 仍 requiresConfirm=false / UI 仍持第二套 MATRIX / EMPTY.PUSH 仍启用；代码对齐完成并复跑后才可改状态”。9d7afd0 已完成对齐却未更新该状态，属证据自相矛盾。

---

## 3. G-P0-2 审计-lite：FAIL

### 3.1 已满足部分

| 断言 | 判定 | 实测 |
|---|---|---|
| 真实动作后新增行 | ✅ | 真实 FETCH 15:08:07 后新增行：`{"ts":"2026-11-14T07:08:07.2726710Z","action":"FETCH","componentId":"RTCT_Demo","repoType":"DESIGN",...,"ok":true,"durationMs":704}`；真实 COMMIT/PUSH `73cee5b` 亦各 1 行（14:50:09/14，`preHead=037211d… → postHead=73cee5b…`，与仓 reflog 一致） |
| 成功/失败都写 | ✅ | 文件内既有成功行，也有 `E_CONFIRM_REQUIRED`/`E_REPO_NOT_LOCAL`/`E_INVALID_ARGUMENT` 失败行 |
| 重启后可查 | ✅ | 15:08 重启后的 Host 仍能读到 14:36–14:51 的历史行（文件持久化） |
| 无 token | ✅ | 68 行全文扫描 `token/Authorization/ghp_/Basic ` 命中 0；写入代码只序列化元数据（`OperationAuditService.cs:92-106`） |
| 无重复行 | ✅（弱） | 以 `ts|action|componentId|repoType` 计无重复；但因无 `request_id`，无法按 AC 口径断言“恰 1 行/重试独立” |

### 3.2 FAIL 依据

1. **必需字段缺失**：ADR §2.2-6（`reqs/adr/D-025-m2a-m2b.md:67`）要求“至少含 `ts/actor/componentId/repoType/action/before_head/after_head/result/error_code/request_id`”；SRS §8.1 的 `operation_log` 物理列（`reqs/requirements.md:395`）与 AC-OP-33 Then②（`:1167`）同样要求 `actor`、`request_id`。实现（`src/MbdGitTool.Host/Services/OperationAuditService.cs:92-106`）写出字段为 `ts/action/componentId/repoType/state/preHead/postHead/ok/errorCode/durationMs`：**68 行中 `actor` 缺 68、`request_id` 缺 68**；`before_head/after_head/result/error_code` 只是语义近似（`preHead/postHead/ok/errorCode`），`actor`/`request_id` 语义完全缺失。
2. **落点不是 `operation_log`**：SRS §13.1.2 AC-OP-33 的测试层级明确“真实 Host + SQLite `operation_log`”（`:1164`），When 给出 `select ts,actor,target_json,before_json,after_json,action,result,error_code,request_id from operation_log …`（`:1166`）；ADR §5 同一查询（`:180`）。实际为 `%LOCALAPPDATA%\MbdGitTool\audit\operations.jsonl`（自由 JSONL，无表/无 SQL），且没有 `actor/request_id`，无法完成 AC 要求的“同一 request_id 前后行数比对”“重启不重复”。
   - `docs/m2a-ac-report.md:36-43` 把“JSONL audit-lite / 完整 operation_log 归 M2b”列为**已知残留并称不阻塞**，但 ADR §2.3/§2.6 只把**查询/导出/≥90 天保留**归 M2b，最小列写入（含 `actor/request_id`）仍属 M2a；该“降级”未经 ADR/SRS 授权。
3. **证据自洽性**：`docs/m2a-ac-report.md:24`（row 13）把该形态记为 ✅ 并写“真实动作证据见文件末尾两行”——报告正文结束于 §3 已知残留表，**根本没有“文件末尾两行”**；同表列出的字段集也不含 AC 强制的 `actor/request_id`，属“文档说通过但口径不成立”。
4. 旁证：并行安全 Gate 的 R-2 独立判定同一缺陷；其 R-3 另指出 Host 测试会向真实审计文件写行，故本项的“真实动作新增行”证据必须按 componentId/时间过滤（本报告已按此取证）。

### 3.3 修复建议（二选一，须留痕）

- **方案 A（推荐，符合冻结 AC）**：按 SRS §8.1 落 `operation_log`（SQLite，或等价可 SQL 查询的存储）+ 必填 `actor`（可用 OS 用户名/固定 `local-user`）与 `request_id`（桥请求 id，经 `RunRepoActionParams` 透传到审计），补“同 request_id 恰 1 行/重试独立”的 Host 测试与 CDP 审计查询证据；或
- **方案 B（改需求）**：另立 ADR 修订，把 AC-OP-33 的存储形态与字段冻结改为 JSONL 并补齐 `actor/request_id`，同时回写 SRS §13.1.2/§8.1；不得以验收报告“残留”自行降级。

---

## 4. G-P0-3 M2b 边界：PASS

### 4.1 实测（真实 Host，真实鼠标事件 + 桥调用计数）

| 入口 | 可见文案（实测） | 点击结果 | 写调用增量 |
|---|---|---|---|
| 批量条 `[data-testid=batch-bar]` 4 键 | title：`SYNC：M2b 未接入（批量/作业/审计）`、`CREATE：…`、`Clone 缺失仓：…`、`删除 / 恢复 / 纳入 / 移出：…` 均含完整括号文字 | 依次点击 → toast `批量 SYNC/CREATE/CLONE/DELETE：M2b 未接入（批量/作业/审计）` | `runRepoAction/getJob/listJobs` = **0** |
| 菜单「操作」4 项 | `组件 SYNC… — M2b 未接入（批量/作业/审计）`、`组件 CREATE…`、`批量 Clone…`、`批量 Delete…` 全部完整 | 点击“组件 SYNC…” → toast `批量 SYNC：M2b 未接入（批量/作业/审计）` | **0** |
| 任务中心抽屉 `[data-testid=title-tasks]` | 空态描述含 `M2b 未接入（批量/作业/审计）`；`取消 / 重试失败项` 行含完整文案；按钮“了解批量入口” | 点击“了解批量入口” → toast `任务批量提交：M2b 未接入（批量/作业/审计）` | **0** |
| 组件卡片快速入口 | SYNC/CREATE 按钮 title 含完整文案 | 点击 → toast `批量 SYNC：…` | **0** |
| 卡片右键菜单 | 菜单项可见文案为 `SYNC…（四仓，远程覆盖本地）· M2b 未接入`（**括号文字在菜单项本身被省略**） | 点击后 toast 为 `批量 SYNC：M2b 未接入（批量/作业/审计）` | **0** |
| 旧措辞回归 | 全页 `M2 接入` 命中 **0** | — | — |

代码锚点：`ui/src/viewmodel.ts:746`（`M2B_BOUNDARY_TEXT`）、`BatchBar.vue:28-31`、`MenuBar.vue:59-62`、`DrawerHost.vue:39-45`、`ComponentCard.vue:121-122`、`stores/workbench.ts:1692-1694`（批量入口统一 toast，不调用 Host）。

### 4.2 残留（P2）

- 卡片右键菜单项标签只写 `· M2b 未接入`（无“（批量/作业/审计）”）；AC-UI-15 的“点击后可见文案”已满足（toast 完整），但与本项“入口自身文案一致”的验收意图有偏差，建议统一补全。
- 本项“C/D/E bbox 不变”（AC-UI-15 Then⑤）本轮未单独测量；改动均为文案替换，上一轮 Gate 的 `mock-*` 布局证据可参照，未发现回归。

---

## 5. G-P0-4 AC 可验收：FAIL

### 5.1 已满足部分

| 断言 | 判定 | 证据 |
|---|---|---|
| SRS §13.1.2 存在且 9 条 G/W/T | ✅ | `reqs/requirements.md:1084` 标题；§13.1.2 含 `AC-UI-10a/10b/10c/15、AC-SAFE-14/15/16/17、AC-OP-33` 共 **9** 条，均含 Given/When/Then（`:1098-1168`）；前言冻结 tooltip 正则 `:1092`、COMMIT 两步 `:1094`、单源夹具与 M2a 13 条 `:1088-1090` |
| §13.4 追溯更新 | ✅ | `:1193-1218`：FR-ACT 行新增 `AC-UI-10a/10b/15`、FR-SAFE 行含 `AC-SAFE-14…17`、FR-OPS 行含 `AC-OP-33`；`:1218` 写明 M2a 13 条门禁与三件证据 |
| 13 条 AC 状态表存在 | ✅ | `docs/m2a-ac-report.md:10-24` 覆盖 13 条，状态均为 ✅ |

### 5.2 FAIL 依据（“13 条状态与证据路径自洽”不成立）

1. **row 10（`docs/m2a-ac-report.md:21`，AC-SAFE-15）与代码不符**：报告称“`runRepoAction` 先 `_snapshots.RefreshAsync`（写前重扫）”，但代码 `src/MbdGitTool.Host/Bridge/HostCommandRouter.cs:248-261` 在 `runRepoAction` 只调 `EnsureSnapshotAsync`；全文件 `RefreshAsync` 仅出现在 `refresh` 方法分支（`:225-229`）。即写前 read-through 重扫并未实现，报告作了与代码不符的 ✅ 陈述（并行安全 Gate R-1 独立得出同一结论）。
2. **row 13（`:24`，AC-OP-33）证据指向不存在的内容**：称“真实动作证据见文件末尾两行”，但报告正文止于 §3 残留表，**无该两行**；且所列字段缺 AC 强制字段（见 §3.2）。
3. **row 8（`:19`，AC-UI-15）称“代码 + CDP 自测”，但仓库 CDP 证据不可复跑**：`ui/scripts/cdp-m2a-selfcheck.mjs:3` 明写“断言清单占位/待实现”，`:14` 写“以退出码 2 结束”，`:252-254` 实际 `process.exit(2)` 并打印 `[PENDING] …不得作为 M2a 通过证据`；旧 `cdp-m2-selfcheck.mjs` 仍在 `ui/scripts/`（未删除、未加作废标记）。
4. **row 6（`:17`，AC-UI-11）称“前轮 CDP + 本轮截图”，但提交未归档任何截图**：`git show --stat 9d7afd0` 无 `docs/images/**`；`git ls-files docs/images/m2` 最新为 13:18 的 fixcheck 截图；`docs/m2a-ac-report.md` 全文 0 处截图/JSON/日志路径。ADR §5 要求“证据归档：`docs/m2a-ac-report.md` + 截图/JSON/Host 日志”，实际最终 Gate 证据只存在于 gitignored 的 `artifacts/ui-review/**`，不可随提交审计。
5. **row 1（`:12`，AC-UI-08）说“夹具已建”，但夹具未被测试/CDP 实际消费且 status 仍 PENDING**（见 §2.2）。
6. **三件证据名义齐全、实质缺一**：`docs/m2a-ac-report.md`、`reqs/v0.2/fixtures/m2-matrix.json`、`ui/scripts/cdp-m2a-selfcheck.mjs` 均存在，但第三件是占位脚本，不满足 ADR §5“复评入口 … 并在 CI/本机复跑”、`docs/m2-ops-status.md` §6 前置③“跑出 exit 0（附输出）”。
7. **测试量声明无提交内路径**：`docs/m2a-ac-report.md:32` 的 265/265 在提交内无 trx/日志；并行安全 Gate 已用隔离树复跑 265/265 通过（可作旁证），故不将测试数量本身作为阻断，但“可复跑证据路径”在提交内仍缺失。

### 5.3 修复建议

1. 把 `ui/scripts/cdp-m2a-selfcheck.mjs` 实现为真实断言脚本（读夹具 14×5、真实鼠标键盘、逐 AC 输出 `{id,ac,ok}`，全绿 exit 0），或按 ADR 修订明确接受“Gate 自备脚本 + 输出归档”，并**附本次运行输出**。
2. 修正 `docs/m2a-ac-report.md` row 10（要么实现 read-through 后重写，要么按安全 Gate 结论改为未通过/待修）、row 13（补真实审计行摘录并修正字段口径）、row 8（写清实际使用的 CDP 脚本与输出路径）、row 6（删除“本轮截图”或补归档）。
3. 将最终 Gate 证据（确认条/状态栏/审计/M2b 文案/0 console）截图与 JSON **归档到 `docs/images/m2/` 或提交内可追踪目录**，并在报告引用。
4. 更新 `m2-matrix.json` status 与 freezeNotes（代码已对齐），并把 Host 单测/CDP 改为**读取夹具**而非硬编码。

---

## 6. G-P0-5 状态栏模板：PASS

| 断言 | 判定 | 实测 |
|---|---|---|
| 真实动作后文案匹配冻结模板 | ✅ | 真实 FETCH（审计 15:08:07 行）后状态栏实测：`最近一次操作：FETCH RTCT_Demo/DESIGN — 成功 · 15:08`；模板 `最近一次操作：<ACTION> <Component>/<RepoType> — 成功/失败(E_*) · HH:mm` 逐段匹配 |
| 失败路径含错误码 | ✅ | 注入测试接受后（合成 `E_GATE_BLOCKED`）：`最近一次操作：COMMIT RTCT_Synced/DESIGN — 失败(E_GATE_BLOCKED) · 15:14`；PUSH 同：`最近一次操作：PUSH RTCT_Demo/DESIGN — 失败(E_GATE_BLOCKED) · 15:14`，均匹配 |
| 实现位置 | ✅ | `ui/src/stores/workbench.ts:411-425`（`lastOpText`：idle→“最近一次操作：无”、running→“进行中”、success→“成功”、失败→`失败(${code})`、HH:mm）；实际绑定 `StatusBar.vue:32` |
| 未回归 | ✅ | 其他状态栏段（freshness/health/queue/notify/network）仍正常渲染：`◷本地 2min 前 · 远程 2min 前` / `Host GitWorker` / `队列 0` / `🔔 2` / `在线` |

---

## 7. 回归与证据（第 6 项必须验证）

### 7.1 确认条（COMMIT 两步 / PUSH）

| 子项 | 判定 | 实测 |
|---|---|---|
| COMMIT 第一步只出对话框 | ✅ | 注入 `stagedCount=3` 点击 `action-commit`：`action-dialog` 可见、`confirm-bar` 不可见、`runRepoAction` 计数 0；对话框含 `已暂存 3 个文件 / 将提交 3 个文件` |
| COMMIT 第二步确认条文案/高度/布局 | ✅ | `将提交 3 个文件（已暂存 3）` 匹配 `/^将提交 \d+ 个文件/`（“已暂存”在“个文件”之后括注，符合 §13.1.2:1103）；条高 **44px**；`[data-testid=repo-actions]` 前后 `x=306.39,y=769,w=1101.61,h=52` 完全一致；5 键 `96×30 @ y=780,x=589.19/699.19/809.19/919.19/1029.19` 前后一致 |
| 未确认/ESC 0 写 + 焦点 | ✅ | 开条后 `runRepoAction` 计数 0；焦点=`confirm-bar-accept`；ESC 后条消失、焦点回 `action-commit`、计数仍 0 |
| 接受后 args | ✅ | 恰 1 次 `runRepoAction(COMMIT,{message:'m2a-final-gate',confirm:true})`（未勾选 stageAll 时**无** `stageAll`）；`stageAll` 勾选路径见 `workbench.ts:1320,1331` + 上轮 `mock-03` 证据 |
| PUSH 确认条（真实） | ✅ | 真实 SYNCED 仓点击 `action-push`：条高 44px、文案 `将推送 0 个提交（main → origin/main）` 匹配 `/^将推送 \d+ 个提交（[^（）]+ → [^（）]+）$/`、5 键 bbox 不变、计数 0；焦点 accept；ESC 回 `action-push`、0 写 |
| `Alt+5` 同路径 | ✅ | 真实 `Alt+5` 开条、计数 0（未确认不写） |
| NO_UPSTREAM 发布分支 | ✅ | 注入 NO_UPSTREAM：条文本 `将推送 2 个提交（main → origin/main）`、detail `发布分支：main → origin/main；首次推送将建立 upstream（setUpstream=true）`；接受 args=`{setUpstream:true,confirm:true}` |

### 7.2 焦点/CHKOUT/tooltip/console 回归

| 项 | 判定 | 实测 |
|---|---|---|
| 确认条焦点闭环 | ✅ | 初始焦点在 `confirm-bar`；ESC 归还触发键 |
| CHKOUT 对话框焦点 | ✅ | 初始焦点=`checkout-ref-search`；ESC 后 active=`action-chkout`、0 写 |
| CHKOUT refs/搜索/stash | ✅ | 真实 Host：`共 3 个候选`（本地分支 1 / 远程分支 2），`checkout-ref-group-LOCAL_BRANCH/REMOTE_BRANCH`；搜索 `mai` → 3→2；真实 dirty 仓 `RTCT_Demo_SIL`：stash 复选框存在（默认未勾选）+ hint“未勾选：工作区有 1 处改动时 Host 返回 `E_DIRTY_WORKTREE`…” |
| tooltip 不回归 | ✅ | MISSING 5 键全部匹配冻结正则；staged0 COMMIT 匹配 |
| ConfirmDialog/ComponentDrawer | ✅（无回归） | 两者不在 `9d7afd0` diff 中（提交仅改 `DrawerHost.vue/ModalHost.vue` 的 M2b 文案），上一轮 Gate 已逐项实测通过 |
| console error | ✅ | 3 次 CDP 会话期间 `Runtime.exceptionThrown`/`console.error`/`Log.entryAdded(error)` 合计 **0** |

### 7.3 真实写证据（73cee5b）

- 本地：`RTCT_Demo_Design` HEAD `73cee5b feat: gate final staged commit`（父 `037211d`），`git status` 干净；`refs/remotes/origin/main`=`73cee5b`。
- 推送旁证：`.git/logs/refs/remotes/origin/main` 末行 `037211d 73cee5b RTCTest … update by push`（mtime 14:50:13）；审计 PUSH 行 14:50:14 与之一致。
- **未能独立复核远端**：`git ls-remote` 需要认证（`Authentication failed`），Gitea API 404；`docs/m2a-ac-report.md:28` 的“远端 main=73cee5b”在本轮只能算“本地 remote-tracking+reflog 支持”，不属独立远端取证（如实记录，不据此判通过或失败）。

### 7.4 截图归档

- ❌ 未归档：`9d7afd0` 未新增/修改 `docs/images/**`；`docs/images/m2/` 仍停在 13:18 的 fixcheck 截图（`git ls-files docs/images/m2` 14 张，最新 `fixcheck-*`）；本轮 Gate 用到的确认条/状态栏/审计/M2b 截图与 JSON 只在 gitignored `artifacts/ui-review/**`。该缺口并入 G-P0-4 的 P0 结论，单列为 P1 证据项。

---

## 8. 缺陷清单

### 8.1 P0（UX/需求 Gate 视角，2 项）

| ID | 问题 | 证据 | 修复 |
|---|---|---|---|
| **P0-A（G-P0-2）** | AC-OP-33 审计字段不满足冻结口径：缺 `actor`/`request_id`；落点为自由 JSONL 而非 `operation_log`；无法断言“同 request_id 恰 1 行/重启不重复”；验收报告把该形态记为 ✅ 且指向不存在证据 | §3；`OperationAuditService.cs:92-106`；SRS `:395,:1164-1167`；ADR `:67,:180`；`docs/m2a-ac-report.md:24` | 按 §3.3 方案 A 补齐字段/存储，或另立 ADR 修订需求后回写 SRS |
| **P0-B（G-P0-4）** | M2a 可复跑证据链不成立：`cdp-m2a-selfcheck.mjs` 仍为 exit 2 占位；`m2a-ac-report` row 10 与代码不符、row 13 指向不存在内容、row 8 “CDP 自测”无对应脚本输出、row 6 “本轮截图”未归档；夹具未被单测/CDP 消费 | §5；`ui/scripts/cdp-m2a-selfcheck.mjs:3,14,252-254`；`HostCommandRouter.cs:248-261`；`docs/m2a-ac-report.md:17,19,21,24`；`m2-matrix.json:16,24` | 实现并跑出 CDP 脚本 exit 0（附输出）或按 ADR 修订门禁；修正报告 4 处不实陈述；把夹具接线到 Host 单测与 CDP；归档截图/JSON |

### 8.2 P1（不新增阻断，但应在打 tag 前一并收口）

| ID | 问题 | 证据 |
|---|---|---|
| P1-1 | “单源夹具”未机器化：Host 单测硬编码 `ExpectedMatrix`、UI Mock 硬编码、CDP 占位；4 份手写矩阵（当前内容 0 差异） | §2.2；`RepoActionAndComponentDetailTests.cs:19-36`；`workbenchMock.ts:643-658` |
| P1-2 | `m2-matrix.json` status 仍 `SINGLE_SOURCE_PENDING_CODE_ALIGNMENT`（代码已对齐） | `m2-matrix.json:16,24` |
| P1-3 | COMMIT 对话框仍无 staged 文件列表（G-P1-3 未修） | `workbench.ts:1276-1313`；`review-m2-gate-ux.md` G-P1-3 |
| P1-4 | 错误 toast 双 `✕` 未修：实测 `✕✕ COMMIT 失败 · E_GATE_BLOCKED…`（图标 + 文案自带 `✕`） | `ToastHost.vue:20`；`workbench.ts:1649,1656` |
| P1-5 | 最终 Gate 截图/JSON/日志未归档到 tracked 路径；`m2a-ac-report` 全文无证据路径 | §7.4；ADR §5“证据归档” |
| P1-6 | `docs/m2-ops-status.md` 与提交状态不一致：仍写“代码修复在并行进行，未合入”“复评前置③ cdp 脚本 exit 0”未满足，未随 9d7afd0 更新 | `docs/m2-ops-status.md:13,76-79` |
| P1-7 | 本轮真实写证据只覆盖 COMMIT/PUSH/FETCH；PULL/CHKOUT 真实写来自更早轮次，审计文件 `CHKOUT=0`、成功 `PULL=0`；远端独立复核受认证限制 | §1.5/§7.3 |
| P1-8 | `265/265` 在提交内无运行日志/trx；Host 运行锁定无法独立复跑（并行安全 Gate 隔离复跑通过可旁证） | `docs/m2a-ac-report.md:32` |

### 8.3 P2

1. 卡片右键菜单批量项标签只写“M2b 未接入”（缺括号文字），点击 toast 完整；建议统一。
2. CHKOUT 候选仍含 `origin/HEAD`（上一轮 P2 未处理）。
3. `docs/m2a-ac-report.md:3` 仍写“候选代码：工作区最新（待提交）”，而 9d7afd0 已提交。
4. AC-UI-11 行称“前轮 CDP + 本轮截图”，无路径（并入 P0-B 证据项）。
5. 五键尺寸仍 96×30、`origin/HEAD` 等既有 P2 未变（不属本轮回归）。

---

## 9. Gate 结论与打 tag 前置

### 9.1 结论

**不建议打 `m2a` tag（M2a Gate 不通过）。**
理由：
1. **G-P0-2 未闭环**：M2a 出口 AC-OP-33 的字段/存储口径不满足冻结的 SRS/ADR，且报告作不实 ✅。
2. **G-P0-4 未闭环**：可复跑证据链仍不成立（占位 CDP 脚本 + 报告不实陈述 + 截图未归档 + 夹具未接线），ADR §5“三件证据”实质缺一，`m2-ops-status` §6 复评前置③未满足。
3. 行为层修复质量高：G-P0-1/G-P0-3/G-P0-5 与确认条/焦点/CHKOUT/tooltip/0 console 均实测通过、零真实写；但**不能用行为子集替代出口 AC**（ADR §2.4-4）。
4. 并行安全 Gate 的 R-1（AC-SAFE-15）与 R-2（审计）同向阻断，M2a 出口 13 条未全绿。

### 9.2 打 `m2a` tag 的最小前置（建议）

1. 修 P0-A：按 SRS 落 `operation_log` 最小列（或经 ADR 修订 JSONL 口径），补齐 `actor/request_id`，补“同 request_id 恰 1 行/重启不重复”的测试与证据。
2. 修 P0-B：实现 `cdp-m2a-selfcheck.mjs` 并在真实 Host 上跑出 exit 0（存档输出）；修正 `m2a-ac-report.md` 的 row 10/13/8/6；夹具接线到 Host 单测与 CDP；归档最终截图/JSON 到 tracked 路径。
3. 并行关闭安全 Gate R-1（AC-SAFE-15 写前 read-through）与 R-3（测试污染真实审计文件）。
4. P1 随附收口（staged 列表、双 ✕、origin/HEAD、ops-status 同步）。
5. 复评通过后只能写“M2a 通过、M2b 未接入”，不得写“M2 完成”。

---

## 附录 A：可复跑命令与关键值

1. **单源夹具 vs Host 逐格比对**（Node，只读）：
   - 读取 `reqs/v0.2/fixtures/m2-matrix.json` 与 `src/MbdGitTool.Host/Services/RepoActionService.cs:63-80` 的 14 行编码；
   - 展开 `enabled = cell∈{Y,C}`、`requiresConfirm = cell==C || action∈{COMMIT,PUSH}`、`reasonCode = !enabled ? STATE_BLOCKED : requiresConfirm ? CONFIRM_REQUIRED : AVAILABLE`；
   - 结果：14 态 × 5 键 = **70 格，0 差异**；Host 测试硬编码矩阵、UI Mock 矩阵亦与 Host 0 差异。
2. **真实 Host 只读探测**（CDP）：`Runtime.evaluate` 读状态栏/按钮/title、`getComponentDetail` 原始 actions；实测 `RTCT_Demo`(SYNCED×3/AHEAD×1) 与 `RTCT_Synced`(SYNCED×4) 的 COMMIT/PUSH `requiresConfirm=true`、`CONFIRM_REQUIRED`；`RTCT_Incomplete`(MISSING) 同 true、`STATE_BLOCKED`。
3. **注入桥确认条/args**：`window.__DSH_TEST_BRIDGE__` patch 快照；点击路径全部走真实 `Input.dispatchMouseEvent`/`dispatchKeyEvent`；`runRepoAction` 拦截返回 `E_GATE_BLOCKED`；审计文件 `E_GATE_BLOCKED` 行数 0，真实仓零改动。
4. **审计证据核对**：`%LOCALAPPDATA%\MbdGitTool\audit\operations.jsonl` — 真实 FETCH 行 `07:08:07Z`（15:08:07 本地），真实 COMMIT/PUSH `73cee5b` 行 `06:50:09Z/06:50:14Z`；全文件 token 关键词 0 命中；`actor/request_id` 0/68 行存在。
5. **关键实测值**：确认条高 44px；`repo-actions` bbox `(306.39,769,1101.61×52)` 开条前后一致；5 键 `96×30 @ y=780`；状态栏 `最近一次操作：FETCH RTCT_Demo/DESIGN — 成功 · 15:08`；console error 0；视口 1424×861。
6. **远端限制**：`git ls-remote origin` 认证失败、Gitea API 404；以本地 `refs/remotes/origin/main` + reflog `update by push` 作旁证。

## 附录 B：本轮证据采集说明

- 因“只评审/不改真实工作区”，本轮**未向 `artifacts/` 或 `docs/images/` 写截图/JSON**；上表数值均来自会话内 CDP 原始 JSON 输出与只读命令，可按附录 A 复跑。
- 采集完成后（15:18:28）并行修复进程重建 `bin` 并停止 Host（CDP 9222 不可达），故页面级证据截止于 15:17；提交对象静态证据不受影响。
- 本报告未修改任何源码、需求、夹具、测试或真实仓库；工作区中 10 个未提交改动（测试补充、可空标注、以及 15:19 后并行进程对 `HostCommandRouter.cs`/`OperationAuditService.cs` 的 `actor/request_id`+read-through 修复）来自并行进程，不属于 `9d7afd0`，亦未被本报告用作通过依据。
