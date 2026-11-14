# M2a 最终 UX/需求签收复评（提交 `fe98b95`）

> 复评角色：独立最终 UX/需求（含需求符合度）签收复评员
> 复评对象：`fe98b95 fix(m2): complete final gate evidence (write pre-read-through, audit actor/requestId, fixture parity, M2a CDP selfcheck 10/10)`（2026-11-14 15:25:21 +0800，HEAD）
> 输入：`docs/review-m2a-final-ux.md`（对 `9d7afd0` 的 P0-A/P0-B）、`docs/review-m2a-final-security.md`（R-1/R-2/R-3）、`docs/review-m2-gate-ux.md`、`docs/m2a-ac-report.md`、`reqs/requirements.md` §13.1.1/§13.1.2/§13.4（v0.3.1-r2）、`reqs/adr/D-025-m2a-m2b.md`、`reqs/v0.2/fixtures/m2-matrix.json`、`ui/scripts/cdp-m2a-selfcheck.mjs`、`src/**`、`ui/src/**`、`tests/**`
> 硬性约束：**只评审**。未修改任何源码/需求/夹具/真实 RTCRepos 工作区；未杀掉运行中 Host、未启动替代服务；未执行任何真实写操作。全量测试在 `git archive HEAD` 导出的 `%TEMP%` 不可变树 + `--artifacts-path` 隔离产物中运行，未触碰工作区 `bin/`。本轮唯一写盘文件 = 本报告（另有 `%TEMP%` 下的导出树/探针，均在仓库外）。
> 复评时间：2026-11-14 15:25–15:40（本地）。

---

## 0. 结论（签收判决）

| 核对项 | 判定 | 依据 |
|---|---|---|
| ① `node ui/scripts/cdp-m2a-selfcheck.mjs` | **PASS 10/10，exit 0** | 10 条断言全部 PASS，`{total:10, passed:10, failed:[]}`，`EXIT=0`（§2） |
| ② SRS §13.1.2 的 9 条 AC 定义与 ADR 一致 | **PASS** | 9 条 G/W/T 齐全（`reqs/requirements.md:1098-1168`），与 `reqs/adr/D-025-m2a-m2b.md` §2.2/§3/§5 逐条对应，无冲突（§3） |
| ② `docs/m2a-ac-report.md` 13 条状态/证据与实测自洽 | **基本 PASS（11/13 完全自洽；2 处非阻断 P1 文档残留）** | 13 行状态与代码/测试/真实证据一致；行 13 字段清单未随实现更新、行 6 “本轮截图”未归档，见 §4、§7.1 |
| ② 夹具 `m2-matrix.json` `status=aligned` 且被单测消费 | **PASS** | `status:"aligned"`（夹具 `:16`）；`RepoActionAvailabilityMatrixTests` 直接读取夹具并断言 `status=="aligned"`+14×5 逐格，271/271 实测通过（§5） |
| ③ 状态栏模板 / M2b Toast / tooltip / PUSH 确认条不回归 | **PASS** | 自检 4 项全 PASS；状态栏实测 `最近一次操作：FETCH RTCT_Demo/DESIGN — 成功 · 15:25`；确认条高 44px、bbox 不变、未确认 0 写（§2、§6） |
| ③ COMMIT 确认条不回归 | **PASS（静态 + 前轮实测复核）** | `fe98b95` 未改 `ui/src`（`git diff --stat HEAD~1 HEAD -- ui/src` 为空），COMMIT 对话框→确认条逻辑与上一轮 Gate 实测一致；本轮真实环境无 staged 仓，未重注入桥补测（限制见 §6.3 末条、§8） |
| ③ 真实 `73cee5b` 可复核远端 | **PASS（独立远端取证）** | `git ls-remote` 经 GCM 凭据直连 Gitea：`73cee5be… refs/heads/main`；本地 `RTCT_Demo_Design` HEAD/remote-tracking 均 `73cee5b`、工作区干净（§6.3） |
| 新 P0 | **0** | — |
| 新 P1 | **0** | 本轮未发现新引入的 P1 |
| 遗留 P1（非阻断，证据/文档卫生；上一轮已点出） | **2** | 见 §7.1：m2a-ac-report 行 13 字段清单未随实现更新；行 6 截图证据未归档 |
| **签收结论** | **✅ 建议打 `m2a` tag** | M2a 出口 13 条 AC 行为层已闭环；ADR §5 三件证据齐备且本轮可复跑（自检 exit 0 + 271/271 + 远端 73cee5b）；上一轮 P0-A/P0-B 阻断理由已消除（P0-B 的证据链已成立，仅余 2 处文档表述残留，降为遗留 P1）；遗留 P1/P2 不改变 AC 判定 |

> **口径**：打 tag 后文档只能写“**M2a 已通过、M2b 未接入**”，不得写“M2 完成”；`docs/m2-ops-status.md` 的 Gate 状态需同步更新（见 §7.2-P1-3），`m2-ops` tag 不恢复。

---

## 1. 环境、方法与零真实写

| 项 | 值 |
|---|---|
| 代码基准 | `git rev-parse HEAD` = `fe98b95…`；`git status` 干净 |
| Host | PID 32464，`src\MbdGitTool.Host\bin\Debug\net10.0-windows\MbdGitTool.Host.exe`（15:24:45 启动，产物 15:22:09，含 `actor/requestId` 新字段行为，审计行可证） |
| 页面 | CDP 9222，`https://app.local/index.html`（`title=MBD Git Tool`） |
| 真实数据 | `RTCRepos`：4 组件 / 14 仓，`source=local+gitea`（非 Mock） |
| 测试环境 | `git archive HEAD` → `%TEMP%\m2a-signoff-*`；`dotnet test … --artifacts-path %TEMP%\…\artifacts`；未写工作区、未触发真实审计 |
| 零真实写 | `runRepoAction` 未向真实 Host 下发（自检仅“打开确认条→取消”）；真实 `RTCT_Demo_Design` HEAD 仍 `73cee5b`、`git status` 干净；真实审计 `operations.jsonl` 行数运行前后均为 **69** |

## 2. 核对项①：M2a CDP 自检（10/10 PASS，exit 0）

命令：`node ui/scripts/cdp-m2a-selfcheck.mjs`（真实 Host、真实鼠标/键盘事件；只读/开条后取消，零写）

```text
PASS ui-08.host-confirm-commit :: commit=true
PASS ui-08.host-confirm-push :: push=true
PASS ui-08.tooltip-format :: 当前状态 MISSING 不允许 PULL（spec §6.2） [E_REPO_NOT_LOCAL] → 先 Clone
PASS ui-10b.confirm-bar :: ⚠ 将推送 0 个提交（main → origin/main） 取消 确认推送
PASS ui-10b.bar-height :: h=44
PASS ui-10b.bbox-stable
PASS ui-10b.no-write-before-accept :: 最近一次操作：FETCH RTCT_Demo/DESIGN — 成功 · 15:25 => 最近一次操作：FETCH RTCT_Demo/DESIGN — 成功 · 15:25
PASS ui-10.status-template :: 最近一次操作：FETCH RTCT_Demo/DESIGN — 成功 · 15:25
PASS ui-15.m2b-copy :: !批量 SYNC：M2b 未接入（批量/作业/审计）
PASS op-33.audit-actor-requestid :: actor=Trevn requestId=3fc8d23e2c1e419e8c79243e1cd2f1f9
{ "total": 10, "passed": 10, "failed": [] }
EXIT=0
```

说明：脚本已从 `9d7afd0` 时的 `exit 2` 占位实现为真实断言（Host actions/tooltip 正则/确认条高度·bbox·0 写/状态栏模板/M2b 文案/审计字段）。结论：**通过**。

## 3. 核对项②（前半）：SRS §13.1.2 九条 AC 定义与 ADR 一致性

`reqs/requirements.md` v0.3.1-r2 `### 13.1.2`（`:1084`）含 **9 条**完整 Given/When/Then：`AC-UI-10a`(`:1098`)、`AC-UI-10b`(`:1106`)、`AC-UI-10c`(`:1114`，M2b)、`AC-UI-15`(`:1122`)、`AC-SAFE-14`(`:1130`)、`AC-SAFE-15`(`:1138`)、`AC-SAFE-16`(`:1146`)、`AC-SAFE-17`(`:1154`)、`AC-OP-33`(`:1162`)。前置冻结口径齐全：tooltip 格式 `原因 [E_*] → 替代动作`（`:1092`）、COMMIT 两步（`:1094`）、单源夹具（`:1090`）、M2a 出口 13 条（`:1088`）。

| SRS §13.1.2 AC | ADR D-025 对应 | 一致性结论 |
|---|---|---|
| AC-UI-10a COMMIT 两步/文案/0 写/bbox | §2.2-3、§3 #4、§5 AC-UI-10a | ✅ 一致（对话框→确认条→执行、`将提交 N 个文件`、≤48px） |
| AC-UI-10b PUSH 确认条/Alt+5/NO_UPSTREAM/detached | §2.2-3、§3 #5、§5 AC-UI-10b | ✅ 一致（`将推送 N 个提交（分支 → upstream）`、未确认 0 写） |
| AC-UI-10c 取消/job（M2b，不记 M2a） | §2.3、§4（M2b 出口含 AC-UI-10c） | ✅ 一致（归属 M2b，M2a 仅要求 M2b 提示） |
| AC-UI-15 M2b 边界文案/0 调用/无伪成功 | §2.2-8、§3 #8、§5 AC-UI-15 | ✅ 一致（括号文字不可省） |
| AC-SAFE-14 凭据作用域/fail-closed/E_CREDENTIAL_SCOPE | §2.2-2①、§3 #9、§5 | ✅ 一致（ADR §2.5-⑥ 错误码族被 SRS 细化） |
| AC-SAFE-15 read-through + CAS + scan_at + 零改动 | §2.2-2②、§3 #10、§5 | ✅ 一致（均含写前重扫 + CAS；ADR §5 的“外部改 HEAD → E_CONCURRENT_CHANGE”按层次理解为 service 级 CAS，见 §7.2-P2-1） |
| AC-SAFE-16 ignored 保护（`--no-overwrite-ignore`） | §2.2-2③、§3 #11、§5 | ✅ 一致 |
| AC-SAFE-17 ref 校验（不存在 ref/前缀/PULL 分支/detached/无 force） | §2.2-2④、§3 #12、§5 | ✅ 一致 |
| AC-OP-33 审计-lite（1 行/最小列/成功失败/重启可查） | §2.2-6、§3 #13、§5 | ✅ 语义一致（SRS 写 SQLite `operation_log`，实现为 JSONL；属已披露落点差异，见 §7.2-P2-2） |

结论：**9 条 AC 定义存在且与 ADR 一致，未发现放宽安全要求的口径**；M2a 出口 13 条 = §13.1.1 的 `AC-UI-08/09/10/11/13` + §13.1.2 的 `AC-UI-10a/10b/15` + `AC-SAFE-14…17` + `AC-OP-33`，与 ADR §3 的 13 行逐条对应。

## 4. 核对项②（后半）：`docs/m2a-ac-report.md` 13 条状态/证据 vs 本轮实测

本轮可独立复核的证据：真实审计行（`actor/requestId` 已落）、隔离全量测试 271/271、自检 10/10、远端 `73cee5b`、Host 实桥 actions/tooltip。未重做上一轮已归档的注入桥/截图类证据（见限制）。

| # | AC | 报告状态 | 本轮/既有证据核对 | 结论 |
|---|---|---|---|---|
| 1 | AC-UI-08 | ✅ | `viewmodel.ts` 无 `MATRIX`（grep 0 命中）；真实 Host `RTCT_Demo` 4 仓 COMMIT/PUSH `requiresConfirm=true`、`reasonCode=CONFIRM_REQUIRED`；tooltip 5/5 匹配紧凑冻结正则；夹具 aligned 且被 Host 单测消费并 271/271 通过 | ✅ 自洽 |
| 2 | AC-UI-09 | ✅ | 自检零写/确认条路径；上一轮 Gate 已逐键实测无“可点无效果”；本轮未发现回归 | ✅ 自洽 |
| 3 | AC-UI-10 | ✅ | 无 staged 禁 COMMIT（真实 UI title 含 `[E_NOTHING_TO_COMMIT]`，本轮实测）；审计行/回写证据在案；真实 PULL/CHKOUT 写证据在 `docs/m2-ops-status.md` §2（报告未逐条引用，P2） | ✅ 基本自洽 |
| 4 | AC-UI-10a | ✅ | 审计真实 COMMIT 行 `037211d→73cee5b`（`:46`）；上一轮 Gate 注入 staged=3 实测对话框→确认条（44px、文案、0 写、ESC、焦点）；`fe98b95` 未改 `ui/src` | ✅ 自洽（前轮实测 + 不回归） |
| 5 | AC-UI-10b | ✅ | 本轮自检：条高 44、文案匹配、bbox 不变、未确认 0 写；真实 PUSH 行（`:47`）；远端 main=73cee5b 已独立复核 | ✅ 自洽 |
| 6 | AC-UI-11 | ✅ | 信息面板逻辑未改；但报告“**本轮截图**”在提交内无对应归档（`docs/images/m2/` 最新仍为 13:18 fixcheck），证据不可审计 | ⚠️ P1（见 §7.1-P1-2） |
| 7 | AC-UI-13 | ✅ | 上一轮 Gate 已实测焦点闭环；`fe98b95` 未改 `ui/src` | ✅ 自洽（前轮实测） |
| 8 | AC-UI-15 | ✅ | 本轮自检 Toast 文案含完整括号文字、0 写；上一轮 Gate 批量条/菜单/任务中心/卡片入口实测通过；卡片右键菜单项标签缺括号文字（P2 残留） | ✅ 自洽 |
| 9 | AC-SAFE-14 | ✅ | 安全 Gate 7 场景 PASS；本轮 271/271（含 7 个 `Token_*` 测试）通过，无回退 | ✅ 自洽（引用并行安全签收） |
| 10 | AC-SAFE-15 | ✅ | `HostCommandRouter.cs:251` 已改 `_snapshots.RefreshAsync`（写前重扫，不再用缓存）；`CasMismatch_WriteAction…` 四动作 `E_CONCURRENT_CHANGE` 测试通过；但**无路由级“写前 refresh”专项测试**（P2 测试缺口） | ✅ 行为自洽（代码 + 测试通过） |
| 11 | AC-SAFE-16 | ✅ | `--no-overwrite-ignore` 代码未改；`Checkout_/Pull_IgnoredFile…` 测试随 271/271 通过 | ✅ 自洽 |
| 12 | AC-SAFE-17 | ✅ | N-1 已修（`Pull_ExplicitBranchDifferentFromCurrent…` 本轮通过；`E_INVALID_ARGUMENT`、HEAD/分支不变）；ref 测试通过 | ✅ 自洽 |
| 13 | AC-OP-33 | ✅ | 实现新增 `actor`/`requestId`（审计尾行 `actor=Trevn requestId=3fc8d23e…`），成功/失败/并发/路由级测试随 271/271 通过；测试隔离生效（真实审计行数 69→69）；但报告行 13 按旧字段清单描述（未含 `actor/requestId`）且“文件末尾两行”指向不明 | ⚠️ P1（见 §7.1-P1-1） |

结论：**13 条状态/证据总体与实测自洽**；2 处（行 13 字段清单、行 6 截图）为文档/证据卫生残留，不改变 AC 行为判定（详见 §7）。

## 5. 夹具与单测消费（核对项②）

- `reqs/v0.2/fixtures/m2-matrix.json:16` `"status": "aligned"`；14 主状态 × 5 键、`requiresConfirm` 规则与 M2a 覆盖（`DIVERGED.PULL`/`EMPTY.PUSH` 禁用）齐全。
- `tests/MbdGitTool.Host.Tests/RepoActionAvailabilityMatrixTests.cs` 直接读取夹具、断言 `status=="aligned"`、14 态逐格比对 Host `GetAvailability` 的 `enabled/requiresConfirm/reasonCode`；本轮隔离全量测试通过 ⇒ **夹具被单测真实消费**。
- 全量测试（提交对象导出树）：`Core 71/71`、`Host 200/200`、**271/271、0 失败/0 跳过**，`TreatWarningsAsErrors=true` 构建成功 ⇒ 0 warning/0 error，`EXIT=0`。
- 残留：`m2-matrix.json:24` freezeNote 仍写“本文件状态为 SINGLE_SOURCE_PENDING_CODE_ALIGNMENT…”与 `status=aligned` 自相矛盾（P2）；CDP 脚本未读取夹具、`ui/src/mocks/workbenchMock.ts:643` 仍硬编码 14×5（P2，见 §7.2）。

## 6. 核对项③：回归项

### 6.1 状态栏模板（PASS）
真实 Host 实测 `最近一次操作：FETCH RTCT_Demo/DESIGN — 成功 · 15:25`，匹配 FR-OPS-05 模板 `最近一次操作：<ACTION> <Component>/<RepoType> — 成功|失败(E_*) · HH:mm`；自检 `ui-10.status-template` PASS。

### 6.2 M2b Toast / tooltip / PUSH 确认条（PASS）
- 批量 SYNC 点击 → Toast 含**完整**“M2b 未接入（批量/作业/审计）”，`runRepoAction/getJob/listJobs` 未新增（自检 `ui-15.m2b-copy` PASS）。
- MISSING 仓 5 键 title 全部匹配 `/^.+ \[E_[A-Z_]+\] → .+$/`（自检 `ui-08.tooltip-format` PASS）。
- PUSH 确认条：高 **44px**（≤48）、文案 `将推送 0 个提交（main → origin/main）`、5 键 bbox 开条前后逐像素一致、未确认状态栏不变（0 写）（自检 4 项 PASS）。

### 6.3 COMMIT/PUSH 与真实 `73cee5b`（PASS）
- 真实审计行（`%LOCALAPPDATA%\MbdGitTool\audit\operations.jsonl`）：COMMIT `preHead=037211dd…→postHead=73cee5b…`（06:50:09Z）、PUSH `73cee5b…`（06:50:14Z）、FETCH `73cee5b…`（07:08:07Z；Host 重启后 15:25 的 FETCH 行 07:25:02Z 另含 `actor=Trevn`/`requestId=…`）。
- 本地仓：`RTCT_Demo_Design` HEAD=`73cee5b feat: gate final staged commit`、`git status` 干净、`refs/remotes/origin/main`=`73cee5b`，reflog `037211d 73cee5b … update by push`（14:50:13）。
- **独立远端取证**：`git ls-remote http://192.168.50.20:3000/RTCRepos/RTCT_Demo_Design.git refs/heads/main` → `73cee5be6973c212f1f7843d750db38b33ac31bb	refs/heads/main`（经 GCM 存储凭据，`GIT_TERMINAL_PROMPT=0`，token 未落盘/未输出）。⇒ 报告“远端 main=73cee5b”成立。
- COMMIT 确认条本轮未重新注入实测：当前聚焦仓 `RTCT_Demo_Impl` 的 COMMIT 因 `stagedCount=0` 禁用（title 含 `[E_NOTHING_TO_COMMIT]`），真实 8 仓无已知“COMMIT 可用且 staged>0”样本（与上一轮 Gate 记录一致）；为避免与并行安全签收共用页面注入桥互相干扰，本轮采用“`ui/src` 零改动 + 上一轮 Gate 实测”复核（限制见 §8）。

### 6.4 审计字段与测试隔离（PASS）
- 新审计行字段齐全：`ts/action/componentId/repoType/state/preHead/postHead/ok/errorCode/durationMs/actor/requestId`；`actor=Environment.UserName`、`requestId=Guid("N")`（`HostCommandRouter.cs`）。
- 测试隔离（上一轮 R-3）：Host 测试统一经 `TestAudit.NewConfig` 注入临时 `AuditRoot`；本轮隔离全量 271 测试前后，真实审计文件行数 **69→69**、mtime 未变；`%TEMP%\MbdGitToolTests\audit\*` 下产生 46 个隔离目录 ⇒ R-3 闭环。

## 7. 缺陷清单（新/残留）

### 7.1 遗留 P1（2 项，非阻断；均为上一轮 Gate 已点出、本轮仍未闭环的证据/文档项）

> 定性说明：2 项都不属于“AC 行为未交付”，而是**验收报告的表述与新实现不同步 / 截图未归档**。M2a 出口 AC 的判定证据本轮已可独立复跑（自检 10/10 exit 0、271/271、远端 73cee5b、审计行），故本签收将其降为遗留 P1，不再作为阻断理由；但建议在 tag 对应的文档提交中顺手修正。

| ID | 问题 | 证据 | 建议 |
|---|---|---|---|
| **P1-1** | `docs/m2a-ac-report.md:24`（行 13）仍按旧字段清单描述 JSONL（`ts/…/durationMs`，**未含已实现的 `actor`/`requestId`**），并写“真实动作证据见文件末尾两行”（报告/审计文件末尾并非 COMMIT/PUSH 两行，指向不明）；同文件 `:36` 的 §3 标题行被写成字面 `\n\n`，渲染异常 | 实现 `OperationAuditService.cs`、审计行 `07:25:02Z`；报告 `:24`、`:36`；上一轮 `docs/review-m2a-final-ux.md` §5.2-2 已指出 | 更新行 13 字段清单与证据指针（直接引 `operations.jsonl` 的 COMMIT/PUSH/FETCH 三行 `ts`）、修正 `:36` 换行；纯文档修订 |
| **P1-2** | `docs/m2a-ac-report.md:17`（行 6）称“前轮 CDP + **本轮截图**”，但 `fe98b95` 未新增/修改任何 `docs/images/**`（`git diff --stat HEAD~1 HEAD -- docs/images` 为空；`docs/images/m2/` 最新仍为 13:18 的 fixcheck 截图），验收截图未随提交归档，ADR §5“证据归档（截图/JSON/Host 日志）”未完全满足 | 报告 `:17`；`git ls-files docs/images/m2` 14 张均为旧图；上一轮 P1-5 残留 | 补归档确认条/状态栏/审计/M2b 文案截图到 `docs/images/m2/`，或在报告中改引 `artifacts/ui-review/**` 的 JSON/截图并注明 gitignore |

### 7.2 残留 P1/P2（不阻断签收，建议 tag 前后收口）

| ID | 级别 | 问题 | 建议 |
|---|---|---|---|
| P1-3 | P1 | `docs/m2-ops-status.md:3,79` 仍写“Gate 状态：M2a 待复评（修复中）”“复评前置③ `cdp` exit 0 未满足”，与本次签收/自检 exit 0 矛盾 | 签收后按本报告更新为“M2a 已通过（M2b 未接入）”，`m2-ops` tag 不恢复 |
| P2-1 | P2 | AC-SAFE-15 的 ADR §5 断言“外部改 HEAD → RunRepoAction → E_CONCURRENT_CHANGE”与 Then②“写前 read-through 重扫”存在层次歧义：router 层写前 refresh 后，既有的“改 HEAD 再动作”不再稳定复现 `E_CONCURRENT_CHANGE`（CAS 只覆盖 refresh→执行窗口）；报告行 10 的“CAS 实测四动作”未注明层级，当前可由 service 级测试 `CasMismatch_WriteAction…` 复现 | 修订 ADR §5 措辞或在报告注明 CAS 断言层级；可补一条 router 级“写前 refresh/scan_at 前进”测试 |
| P2-2 | P2 | AC-OP-33 的实现落点为 JSONL 而非 SRS §8.1 的 SQLite `operation_log`；字段为 `requestId` 而非 `request_id`（语义等价）。安全 Gate 以 SRS “以实现落点为准”接受，M2b 做完整表 | 在 ADR/SRS §13.1.2 或 M2b 契约中补“M2a JSONL audit-lite 为最小实现、M2b 迁 `operation_log`”的授权说明；不必改代码 |
| P2-3 | P2 | `reqs/v0.2/fixtures/m2-matrix.json:24` freezeNote 与 `status:"aligned"` 自相矛盾（仍写 PENDING）；`ui/src/mocks/workbenchMock.ts:643` 仍硬编码 14×5，CDP 脚本未读取夹具（ADR §5 “Host 单测与 CDP 共用、禁止第三份手写期望”仅 Host 单测侧落地） | 更新 freezeNote；CDP/Mock 改为读夹具或注明 Mock 仅 dev 用途 |
| P2-4 | P2 | `docs/m2a-ac-report.md:3` 仍写“候选代码：工作区最新（待提交）”（已提交为 `fe98b95`）；`docs/m2-ops-status.md:52,59` 单测数 234/234 过期 | 文档同步 |
| P2-5 | P2 | 已知非阻断 UX 债务仍在：COMMIT 对话框无 staged 文件列表、错误 Toast 双 `✕`、CHKOUT 候选含 `origin/HEAD`、卡片右键菜单 M2b 标签缺括号文字 | 归后续修复（M2b/M3） |

### 7.3 新 P0
**无。** 上一轮（`9d7afd0`）的两个 P0 均已实质关闭：

| 上一轮阻断项 | 本轮验证 | 结论 |
|---|---|---|
| P0-A：审计缺 `actor`/`request_id` | `HostCommandRouter` 注入 `actor/requestId`、JSONL 落盘（真实行可见）、`OperationAuditServiceTests` 新增 4 例、测试隔离 | **关闭**（存储落点差异保留为 P2-2，安全 Gate 已接受） |
| P0-B：CDP 脚本占位 + 报告不实 + 夹具未消费 | 自检 10/10 exit 0（本轮复跑）；271/271（本轮复跑）；夹具 aligned 且被单测消费；报告行 10/行 8 已更新 | **实质关闭**：证据链可复跑成立；行 13/行 6 的表述残留降为遗留 P1-1/P1-2（见 §7.1 定性），不构成 AC 阻断 |
| R-1/R-2/R-3（安全 Gate） | 写前 `RefreshAsync`；审计字段；测试隔离（真实审计 69→69） | **关闭**（P2-1 为断言层次说明） |

## 8. 限制（如实披露）

1. COMMIT 两步/`stageAll`/NO_UPSTREAM/CHKOUT refs 等注入桥场景本轮**未重新注入**实测：真实环境无“staged>0 且 COMMIT 可用”仓，且页面与并行安全签收共享，为避免互相干扰，采用“`ui/src` 零改动（HEAD diff 为空）+ 上一轮 Gate 实测”复核；如需本轮独立重测，可在安全签收结束后用内存注入桥补测（不产生真实写）。
2. 未直接调用 Gitea API 核仓（私有组织匿名/无 PAT 走 API 返回 404）；改用 `git ls-remote` 经本机 GCM 已存凭据读取 `refs/heads/main`，只读且未输出 token。
3. 未重跑 UI 视觉/1280/200% 截图类 AC；该类由上一轮 Gate 证据覆盖，且本提交未改 `ui/src`。
4. 本报告未修改源码/需求/夹具/真实仓库；全量测试在 `%TEMP%` 不可变树运行，唯一新增 tracked 文件为本报告。

## 附录 A：复跑命令

```powershell
# 1) M2a CDP 自检（真实 Host，只读；期望 10/10 + exit 0）
node ui/scripts/cdp-m2a-selfcheck.mjs

# 2) 提交对象隔离全量测试（不写工作区/不锁运行中 Host；期望 271/271）
$tmp = Join-Path $env:TEMP ("m2a-signoff-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tmp | Out-Null
git archive HEAD | tar -x -C $tmp
dotnet test "$tmp\MbdGitTool.sln" --artifacts-path "$tmp\artifacts" -v minimal

# 3) 远端 73cee5b 取证（只读；token 由 GCM 提供，不落盘）
$env:GIT_TERMINAL_PROMPT='0'; $env:GCM_INTERACTIVE='Never'
git -c credential.helper=manager ls-remote http://192.168.50.20:3000/RTCRepos/RTCT_Demo_Design.git refs/heads/main

# 4) 审计证据（本机真实 Host 落点）
Get-Content "$env:LOCALAPPDATA\MbdGitTool\audit\operations.jsonl" | Select-Object -Last 3
```

## 附录 B：本轮关键实测值

| 项 | 值 |
|---|---|
| 自检 | 10/10 PASS，exit 0 |
| 全量测试 | Core 71 + Host 200 = **271/271**，0 失败/0 跳过，构建 0 warning（WarningsAsErrors） |
| 远端 `73cee5b` | `refs/heads/main` = `73cee5be6973c212f1f7843d750db38b33ac31bb` |
| 审计尾行 | `07:25:02Z FETCH RTCT_Demo/DESIGN ok=true … actor=Trevn requestId=3fc8d23e…`；真实审计 69 行前后不变 |
| 确认条 | 高 44px；`将推送 0 个提交（main → origin/main）`；5 键 bbox 开条前后一致；未确认 0 写 |
| 状态栏 | `最近一次操作：FETCH RTCT_Demo/DESIGN — 成功 · 15:25` |
| 工具链 | Node v24.19.0；dotnet SDK 10.0.404；真实 Gitea 1.25.5（`http://192.168.50.20:3000`，org `RTCRepos`） |
