# M2a 安全/代码 Gate 复评报告（提交 `1afa014`）

| 项目 | 内容 |
|---|---|
| 复评对象 | 提交 `1afa014`（`fix(m2): close security P0 and UX P0, add M2a/M2b ADR`，2026-11-14 14:07:01 +0800） |
| 复评范围 | 原评审 `docs/review-m2-code.md` 的 **P0-1、P1-1、P1-2、P1-3、P1-6、P1-8**（及 AC-SAFE-14/15/16/17 中对应安全面） |
| 输入 | `docs/review-m2-code.md`；`reqs/adr/D-025-m2a-m2b.md`（M2a 出口 AC，权威）；`reqs/specification.md` v0.3.1；`src/MbdGitTool.Host/Services/{RepoActionService,GitCli,LocalScanner,AppConfig,RemoteRefsService,HostLog}.cs`、`src/MbdGitTool.Host/Bridge/HostCommandRouter.cs`、`src/MbdGitTool.Core/Contracts/*`、`tests/MbdGitTool.Host.Tests/{RepoActionSecurityTests,RepoActionGitTests,TestInfrastructure,RealWorkspaceSmokeTests}.cs`、UI 相关只读参考 |
| 环境 | Windows 11 / git 2.56.1.windows.1 / dotnet SDK 10.0.404 / 仓库 `D:\Workspace\git_1`（HEAD=`1afa014`，`git status` 干净） |
| 方法 | ① 逐行代码/契约核对；② 独立服务级黑盒夹具（自建临时控制台程序引用 HEAD 构建产物，真实 git + 回环 TCP 抓包 + `GIT_TRACE2_EVENT` 子进程命令审计）；③ 12 组 git 2.56.1 行为实验（临时目录）；④ 全量 `dotnet test`；⑤ 真实工作区烟测前后只读比对 |
| 边界 | **只新增本报告，未修改任何源码/配置/真实工作区**；未停止运行中的 Host（用 `--artifacts-path` 隔离构建规避文件锁）；全部夹具在 `%TEMP%`；未对真实 Gitea 发起任何写操作 |
| 日期 | 2026-11-14 |

---

## 0. 结论（Gate 判决）

**指定 6 项（P0-1、P1-1/P1-2/P1-3/P1-6/P1-8）全部关闭：6/6 PASS，且未发现新 P0；“token 不外发”与“ignored 文件不被静默覆盖”两条安全边界均有独立黑盒证据。**

**但本次复评发现 1 个新 P1（N-1）：`PULL` 显式传入的 `branch` 未校验“必须等于当前分支”，实测可把当前分支快进到其它远端分支的 tip；该断言是 D-025 冻结的 M2a 出口 AC-SAFE-17 的明文子项（“PULL 显式分支≠当前分支 → 拒绝”），故 AC-SAFE-17 未全绿。**

> **Gate 结论：本次提交 `1afa014` 不建议打 M2a tag（不打）。**
> 修复 N-1（约 10 行 + 1 个负向单测）后即可解除该阻断；另需在 `docs/m2a-ac-report.md` 中显式交代 N-2（写前 read-through 重扫 / `scan_at` 单调前进未实现）。6 项原缺陷“已关闭”的结论不受 N-1 影响。
> 本报告不覆盖 AC-UI-08/09/10/10a/10b/11/13/15 与 AC-OP-33（UI/审计/矩阵），这些需由并行复评给出；按 D-025 §5，当前仓库也尚未见 `docs/m2a-ac-report.md` 与 `reqs/fixtures/m2-matrix.json`，不具备“M2a 全绿”材料。

| 汇总口径 | 结果 |
|---|---|
| 原评审 P0 | 1/1 关闭（P0-1 PASS） |
| 本次指定 P1 | 5/5 关闭（P1-1/2/3/6/8 PASS） |
| 新发现 P0 | 0 |
| 新发现 P1（阻断 M2a 出口） | **1**：N-1（AC-SAFE-17 的 PULL 显式分支子项） |
| 新发现 P2 | 6（N-2…N-7，见 §10） |
| 全量测试 | **262/262 通过**（Core 71 + Host 191；0 跳过；0 警告） |
| 是否建议 M2a gate 通过 | **否（不打 tag）**；修复 N-1 后可复评 |

---

## 1. 验证方法与环境说明（可复跑）

### 1.1 全量测试：隔离构建，避免打断运行中 Host
运行中 Host（PID 9264，`src\MbdGitTool.Host\bin\Debug\...\MbdGitTool.Host.exe`）锁定了 `bin\Debug\net10.0-windows\MbdGitTool.Core.dll`，常规 `dotnet test MbdGitTool.sln` 会因 MSB3027/3021 文件锁失败（首次实测复现：Core 71 通过、Host 项目构建失败）。为不中断 UI 组正在使用的 Host（CDP 9222），改用：

```powershell
dotnet test MbdGitTool.sln --artifacts-path "$env:TEMP\m2gate-artifacts1" -v minimal
```

输出关键行：

```text
已通过! - 失败: 0，通过: 71，已跳过: 0，总计: 71  - MbdGitTool.Core.Tests.dll (net10.0)
已通过! - 失败: 0，通过: 191，已跳过: 0，总计: 191 - MbdGitTool.Host.Tests.dll (net10.0)  (1 m 15 s)
```

即 **262/262 通过**；`TreatWarningsAsErrors=true` 下构建成功 ⇒ 0 警告。Host 191 = 基线 163 + 本提交新增 28 个用例（`RepoActionSecurityTests` 27 个 + `RepoActionGitTests` 新增 1 个）。

> 说明：`--artifacts-path` 使测试 `AppContext.BaseDirectory` 落在临时目录，`RealWorkspaceSmokeTests` 会走“找不到仓库根→跳过”，因此该轮不能替代 P1-8 的真实工作区证据；P1-8 另用仓库内测试二进制单独执行（见 §7）。

### 1.2 独立服务级黑盒夹具（不复用仓库测试代码）
为独立复核（防止“测试自证”），在 `%TEMP%\m2gate-harness\` 新建临时控制台程序，直接引用 **HEAD 源码构建产物**（`%TEMP%\m2gate-artifacts1\...\MbdGitTool.Host.dll` / `MbdGitTool.Core.dll`），对真实 `RepoActionService` 黑盒调用：

- 全部仓为 `%TEMP%` 下真实 git 临时仓（bare remote + cloneA/cloneB），无 Mock；
- 网络侧用自建 `TcpListener` 回环 HTTP 抓包（与仓库测试同机制但独立实现），记录请求头与请求数；
- 用 `GIT_TRACE2_EVENT` 记录 Host 实际启动的**每一条 git 命令 argv**，用于证明“被拒绝时未启动网络 git”与“PUSH 参数无 force/`+`”；
- 结果：37 项断言 36 PASS、1 FAIL（即 N-1），失败项恰为 AC-SAFE-17 的 PULL 子项。

复跑：`cd %TEMP%\m2gate-harness; dotnet run`；证据文件 `%TEMP%\m2gate-h\*\results.tsv`。

### 1.3 git 行为实验（12 组，git 2.56.1，临时目录）
覆盖 `checkout/merge --no-overwrite-ignore` 语义、路径模式 checkout、`rev-parse refs/heads|tags`、PUSH 拒绝 stderr 关键行、URL 作用域 `http.<url>.extraHeader` 正/负例（回环 TCP 抓包）。原始证据目录：`%TEMP%\m2gate-exp-*`、`%TEMP%\m2gate-http-*`。

### 1.4 边界与偶发外部写入
复评期间真实工作区 `artifacts/real-test/workspace/RTCT_Demo_SIL` 在 **14:00:31 / 14:09:16–14:09:49** 出现外部写入（`component.txt`、新 commit 对象、`.git/index`、`FETCH_HEAD`）；时间早于本报告的烟测（14:14:10）且早于/独立于本报告所有测试进程，判为并行 UI/真实 Gitea 复验活动，**非本报告产生**。本报告未对该工作区执行任何写命令（仅 `git --no-optional-locks rev-parse/status` 只读探测）。

---

## 2. P0-1 凭据外发边界 —— **PASS（已关闭）**

### 2.1 实现核对（`RepoActionService.cs`）
网络动作（`FETCH/PULL/PUSH`）在锁内、执行任何网络 git 前调用 `PrepareCredentialEnvironmentAsync`（`:283-298`、`:678-818`）：

1. `token == null` → 不注入、不校验（与修复建议 4 一致，无凭据可外发）：`:685-690`；
2. token 非空且 baseUrl/org 缺失、URL 非法、含 userinfo、org 非法 → `E_GITEA_CONFIG_INVALID` **fail-closed**：`:692-739`；
3. 明文 http 且未显式 `allowInsecureHttp=true` → `E_GITEA_INSECURE_HTTP`：`:741-755`；
4. 只读读取 `git remote get-url origin` **与** `git remote get-url --push origin`：`:757-787`；
5. 两者均须与 `GiteaBaseUrl` 的 scheme/host/port 及 `/<org>/` 路径前缀一致，否则 `E_CREDENTIAL_SCOPE`（不启动网络 git）：`:789-805`、`CheckUrlScope:820-864`；
6. header 改为 **URL 作用域** `http.<canonicalBase>/.extraHeader`，即使 origin 被改也不会发送到非 canonical base：`:807-815`；
7. token 仅进子进程环境（`GIT_CONFIG_*`），不进 argv/响应/日志；错误 details 仅回 scheme/host/port/路径前缀与“是否存在 URL”布尔值，不回显 URL：`CredentialScopeFailure:877-907`。

### 2.2 独立实测（黑盒夹具 + 抓包 + 命令审计）

| # | 场景 | 期望 | 实测（独立夹具） | 结论 |
|---|---|---|---|---|
| 1 | fetch origin host 不匹配（`localhost` vs 配置 `127.0.0.1`），token 存在 | `E_CREDENTIAL_SCOPE`，0 网络请求，不启动 fetch/push | `code=E_CREDENTIAL_SCOPE`；`failedFields=fetch.host,push.host`；回环服务 `RequestCount=0`；trace2 子命令 `[rev-parse,rev-parse,remote,remote]`（**无 fetch/push**） | PASS |
| 2 | `remote.origin.pushurl` 指向另一端口 + `/EvilOrg/`（fetch URL 正常） | `E_CREDENTIAL_SCOPE`，两个服务均 0 请求 | `code=E_CREDENTIAL_SCOPE`；`failedFields=push.port,push.path`；请求数 `0/0`；trace 无 push/fetch | PASS |
| 3 | token 存在但 Gitea baseUrl/org 缺失 | fail-closed，不启动网络 git | `code=E_GITEA_CONFIG_INVALID`；请求 0；trace 仅 `[rev-parse,rev-parse]` | PASS |
| 4 | token 存在但 baseUrl 非法（`not-a-url`/`ftp://`）/org 缺失 | 同上 | 仓库测试 `Token_MissingOrInvalidGiteaConfig_FailsClosedBeforeGit`（5 组 InlineData）随 262 全绿；代码复核 `:709-739` | PASS |
| 5 | base 为 `http://` 且未显式 allow | `E_GITEA_INSECURE_HTTP`，不启动网络 git | `code=E_GITEA_INSECURE_HTTP`；请求 0；无 fetch | PASS |
| 6 | origin 与 base 一致（含 `http://127.0.0.1:P/` 与带路径 base `http://127.0.0.1:P/gitea`） | 仅该 canonical base 收到 `Authorization: Basic base64(token:)` | 两次均抓包到 `Authorization: Basic <base64>`；响应/trace argv 均无 token/base64 | PASS |
| 7 | 作用域 header 只发往 canonical base（git 级独立实验） | 作用域匹配才发 | 回环抓包：scope=`http://127.0.0.1:P`（根）→ 发送；scope=`http://127.0.0.1:P/RTCRepos` → 发送；scope=`http://127.0.0.1:P/OtherOrg` → **不发 Authorization** | PASS |

补充：`GIT_TRACE2_EVENT` 的完整 argv 未出现 token/base64（token 仅在子进程环境）；`HostLog` 文件与 `HostLog.Redact("Authorization: Basic "+basic)` 均无 token/base64（输出为 `Authorization: ***`）；响应 JSON 无 `Authorization`/`extraHeader`.

### 2.3 残余（非本 P0 阻断项）
- 若 token 为空，网络动作按原设计不对 origin 做 Gitea 作用域校验（公开仓路径），无凭据可外发，可接受；
- 重定向/`url.<base>.insteadOf` 等 git 级 URL 改写属于纵深防御边界：URL 作用域 header 已独立验证只按配置 base 前缀发送，未发现可外发 token 的路径；
- `LocalScanner.OriginMatches` 的 fail-open 仍在（见 N-6），但它只影响扫描/展示分类，**不再参与凭据注入决策**（注入前已 fail-closed + 作用域校验），故 P0-1 关闭不受影响。

**P0-1 判定：PASS（关闭）。**

---

## 3. P1-1 CHKOUT/PULL 静默覆盖 ignored 文件 —— **PASS（已关闭）**

- CHKOUT：`git checkout --no-overwrite-ignore <branch>` / tag 模式 `checkout --no-overwrite-ignore --detach <tag>`（`RepoActionService.cs:538,555`）；git 拒绝时识别 “would be overwritten/overwrite ignored” → `E_WORKTREE_OVERWRITE`（`:1028-1038,1079-1086`）。
- PULL：改为 `git fetch origin <branch>` + `git merge --ff-only --no-overwrite-ignore origin/<branch>`（`:414-434`），不再使用不接受该开关的 `git pull`；失败分别映射 `E_GIT_NON_FF`/`E_WORKTREE_OVERWRITE`。
- git 2.56.1 语义实验：无开关的 `checkout`/`merge` 会覆盖 ignored 文件；加 `--no-overwrite-ignore` 均以 exit 1 拒绝且文件保持不变（A1/A2 实验）。

独立服务级实测（黑盒夹具）：

| 场景 | 实测 | 结论 |
|---|---|---|
| clean 仓 + ignored `build.log`(LOCAL-PRECIOUS) + 目标分支 track 同路径 → CHKOUT | `E_WORKTREE_OVERWRITE`；文件仍为 `LOCAL-PRECIOUS`；HEAD/分支均未变；stderr=“untracked working tree files would be overwritten by checkout” | PASS |
| 同上 → PULL（远端已 track 同路径） | `E_WORKTREE_OVERWRITE`；文件仍为 `LOCAL-PRECIOUS`；HEAD 未变；stderr=“…would be overwritten by merge” | PASS |

**P1-1 判定：PASS（关闭）。**

---

## 4. P1-2 快照 HEAD 与当前 HEAD 不一致（TOCTOU/CAS）—— **PASS（按本次复评口径关闭）**

- 实现：写动作（PULL/CHKOUT/COMMIT/PUSH）在 per-repo 锁内先读执行时 HEAD（`preHead`，`RepoActionService.cs:281`），与快照 `repo.Repo.HeadSha` 比较，不一致 → `E_CONCURRENT_CHANGE` 且零改动（`:300-311`、`CheckSnapshotHead:942-962`，details 含 `action/state/snapshotHead/currentHead/sdkCode`）。
- 写成功后 Router 主动失效快照（`HostCommandRouter.cs:244-260` → `SnapshotService.Invalidate():60-68`），下一次请求重建快照，避免“动作后旧 HEAD 触发 CAS 误拒”。
- 独立实测：扫描后外部 commit 改 HEAD，对同一 stale 快照分别执行 CHKOUT/PULL/COMMIT/PUSH → **四个动作全部 `E_CONCURRENT_CHANGE`**；HEAD/分支不变，远端不变。

**P1-2 判定：PASS（指定口径）**。残余说明见 **N-2**：D-025 AC-SAFE-15 的第二断言“动作前 read-through 重扫 / `scan_at` 单调前进”**未实现/无证据**——`runRepoAction` 只在快照为空时重建（`HostCommandRouter.cs:244-260`、`EnsureSnapshotAsync:372-378`），矩阵判定仍基于扫描时快照；仅 HEAD 变化被 CAS 覆盖，dirty/upstream/remote 等状态变化依赖各动作自身的即时检查（安全失败）。建议在 M2a 验收报告中显式交代，或补“动作前强制 refresh”。

---

## 5. P1-3 CHKOUT ref 不存在“假成功” —— **PASS（已关闭）**

- 实现：CHKOUT 先 `rev-parse --verify --quiet refs/heads/<branch>^{commit}`（tag 用 `refs/tags/<tag>^{commit}`），不存在 → `E_REF_NOT_FOUND` 且不 checkout（`RepoActionService.cs:522-557,1054-1063`）；分支名校验拒绝前导 `-`/`+`（`:1393-1396`）。
- git 语义实验：`git checkout README.md`（同名文件、无同名 ref）确实 exit 0 进入路径模式且分支不变 → 旧实现假成功；`rev-parse --verify refs/heads/README.md^{commit}` exit 1、stdout 空 → 新实现可正确拦截。
- 独立实测：SYNCED clean 仓（含 `README.md`）传 `branch="README.md"` → `E_REF_NOT_FOUND`（kind=branch），HEAD/分支不变；传 `tag="v9.9.9"` → `E_REF_NOT_FOUND`（kind=tag）。

**P1-3 判定：PASS（关闭）**。注意：AC-SAFE-17 的“PULL 显式分支必须等于当前分支”子项**未实现**，见 **N-1（新 P1）**；`branch="+main"` 已拒绝（`E_INVALID_ARGUMENT`）、detached PUSH 已被矩阵拒绝（`E_ACTION_NOT_ALLOWED`）、PUSH argv 实测无 `--force/-f/--mirror/--delete/+`。

---

## 6. P1-6 错误摘要只取首行 —— **PASS（Host 侧已关闭）**

- 实现：`SummarizeGitError` 先取关键行（`fatal:`/`error:`/`! [rejected]`/`hint:`/`remote:`，最多末 8 条）再补末 ≤20 行，去重后脱敏并截断 500 字符（`RepoActionService.cs:1100-1143`）；PUSH 拒绝独立为 `E_PUSH_REJECTED`（`BridgeErrors.cs:33`；`PushAsync:644-675`；`IsPushRejected:1088-1095`）。
- 实测（真实非快进拒绝，git 2.56.1）：`code=E_PUSH_REJECTED`；`details.stderr` 为 7 行，包含
  `! [rejected]        HEAD -> main (fetch first)`、`error: failed to push some refs to …`、4 条 `hint:`；
  message 同时包含该摘要；不再是首行 `To <url>`。git 2.56.1 的 PUSH 拒绝输出本身无 `fatal:` 行（既有 `! [rejected]` 关键行，满足“`! [rejected]`/`fatal:` 关键行”口径）。

**P1-6 判定：PASS（Host 侧关闭）**。残余（非本项必须项）：`ui/src/viewmodel.ts:621-692` 未收录本提交新增错误码（`E_PUSH_REJECTED/E_WORKTREE_OVERWRITE/E_REF_NOT_FOUND/E_CREDENTIAL_SCOPE/E_GITEA_CONFIG_INVALID/E_GITEA_INSECURE_HTTP`），UI 走“原始 message 回退”，能显示摘要但无标准 hint/三段式（见 N-5）。

---

## 7. P1-8 测试非 hermetic / 安全边界零覆盖 —— **PASS（已关闭）**

- `RealWorkspaceSmokeTests.cs:174-196`：真实工作区写操作（`runRepoAction PULL`）仅在显式 `MBD_RUN_REAL_WRITE_TESTS=true` 时执行，默认只做只读 API 烟测；其余新增安全测试全部使用 `%TEMP%` 临时 bare remote/临时仓 + 回环 TCP（`RepoActionSecurityTests.cs:26-830`、`TestInfrastructure.cs:14-300`），无真实 Gitea/真实工作区依赖。
- 实测 1（全量）：隔离构建 `262/262` 通过（§1.1）。
- 实测 2（真实工作区 hermetic 专项）：在仓库测试二进制上单跑 `RealWorkspaceSmokeTests`（未设 `MBD_RUN_REAL_WRITE_TESTS`）→ `1 passed, 0 failed, 1s`（说明确实执行了真实工作区只读路径，而非提前返回）；前后对 `artifacts/real-test/workspace` 8 个仓做只读比对（`git --no-optional-locks rev-parse HEAD` + `status --porcelain`）：**WORKSPACE_DIFFS=0**；且该烟测运行时刻（14:14:10）之后该工作区无任何新写入文件（既有写入均在 14:09:49 之前，来自并行活动）。
- 安全边界覆盖：新增 `RepoActionSecurityTests`（Host 191−基线 163=28 个用例）覆盖凭据作用域/fail-closed/http/抓包、ignored、ref 存在性、CAS、摘要、stash/stageAll、token 不落日志；`RepoActionGitTests` 补 PUSH rejected 关键行、token 配置缺失 fail-closed、作用域不匹配等。

**P1-8 判定：PASS（关闭）。**

---

## 8. 回归复核 —— **PASS**

| 项 | 实测 | 结论 |
|---|---|---|
| FETCH 正常 | 本地 bare remote 领先 → `ok=true`，HEAD 不变，`refs/remotes/origin/main` 更新 | PASS |
| PULL(FF) 正常 | `ok=true`，HEAD 与远端一致，无 merge commit（`rev-list --merges=0`） | PASS |
| COMMIT(staged-only) 正常 | 只提交 index 中已暂存文件；`git log -1 --pretty=%s` 与请求 message 一致；无 `add -A`（stageAll 除外，见 N-7） | PASS |
| PUSH 正常 | `ok=true`，bare remote HEAD 更新；trace argv=`git … push origin HEAD`，无 force/`+` | PASS |
| CHKOUT 正常 | `ok=true`，切到目标分支 | PASS |
| token 不出现在响应/日志/异常 | 服务响应 JSON、`HostLog` 文件、`HostLog.Redact` 均无 token/base64；token 仅存在于子进程环境；异常路径均映射为 `CommandResult`（Router 只记异常类型） | PASS |
| 262 测试 | 262/262 通过，0 跳过，0 警告 | PASS |

---

## 9. 逐项结论表

| 原评审项 | 结论 | 关键证据 |
|---|---|---|
| **P0-1** PAT 注入前不绑定 origin/pushurl、配置 fail-open | **PASS** | 独立黑盒：host/pushurl 不匹配 → `E_CREDENTIAL_SCOPE` 且 0 网络请求、trace 无 fetch/push；配置缺失/非法 → `E_GITEA_CONFIG_INVALID`；http 未显式 allow → `E_GITEA_INSECURE_HTTP`；作用域 header 仅 canonical base 收到 `Authorization: Basic base64(PAT:)`；响应/日志/argv 无 token（`RepoActionService.cs:678-907`） |
| **P1-1** CHKOUT/PULL 静默覆盖 ignored 文件 | **PASS** | `--no-overwrite-ignore`（checkout/fetch+merge）实测拒绝且 ignored 文件内容、HEAD、分支均不变；git 2.56.1 对照实验（`RepoActionService.cs:414-434,538,555,1079-1086`） |
| **P1-2** 快照 HEAD ≠ 当前 HEAD → E_CONCURRENT_CHANGE、零改动 | **PASS**（按指定口径） | stale 快照 + 外部 commit：CHKOUT/PULL/COMMIT/PUSH 四个动作均 `E_CONCURRENT_CHANGE`，HEAD/分支/远端不变（`RepoActionService.cs:281,300-311,942-962`；`HostCommandRouter.cs:244-260`；`SnapshotService.cs:60-68`）。残余 read-through 见 N-2 |
| **P1-3** CHKOUT 不存在 ref 假成功 | **PASS** | `branch="README.md"`/`tag="v9.9.9"` → `E_REF_NOT_FOUND`，HEAD/分支不变；`rev-parse --verify` 预检（`RepoActionService.cs:522-557,1054-1063`）。PULL 分支子项缺口见 N-1 |
| **P1-6** 错误摘要只取首行 | **PASS** | PUSH 拒绝 `E_PUSH_REJECTED`，`details.stderr` 含 `! [rejected]`/`error:`/`hint:` 共 7 行，非首行 `To <url>`（`RepoActionService.cs:1100-1143,644-675`） |
| **P1-8** 安全边界零测试 + RealWorkspaceSmokeTests 非 hermetic | **PASS** | 写操作 env 门控（`RealWorkspaceSmokeTests.cs:174-196`）；烟测真实执行且 8 仓 before/after 零差异；262/262 全绿（含新增 28 个安全用例） |

---

## 10. 新发现问题（按严重度）

### N-1【新 P1，阻断 M2a 出口 AC-SAFE-17】PULL 显式 branch 未校验“必须等于当前分支”

- **位置**：`src/MbdGitTool.Host/Services/RepoActionService.cs:394-412`（`PullAsync` 仅解析 branch/当前分支，未比较；`:414-434` 直接 `fetch origin <branch>` + `merge --ff-only origin/<branch>`）。
- **对照要求**：D-025 §2.2 安全四件 ④“PULL 显式分支必须等于当前分支”；D-025 §3 AC-SAFE-17“PULL 显式分支≠当前分支 → 拒绝”；SRS FR-ACT-03/FR-REPO-01“PULL 仅当前分支”。
- **实测（独立黑盒夹具，git 2.56.1）**：仓在 `main`，`feature` 为远端新分支且 `main` 是其祖先；`RunRepoAction(PULL, args:{branch:"feature"})` → **`Ok=true`，当前分支 `main` 被快进到 `origin/feature` 的 tip**。即任意调用方可把当前分支悄悄 FF 到另一个远端分支，随后 PUSH 可能把该分支提交发布到当前分支（非 force，但仍产生错误历史），与“只 PULL 当前分支”的安全语义不符。
- **修复建议**（约 10 行）：在 `PullAsync` 解析出 `branch` 后读取当前分支（`ReadCurrentBranchAsync`），若 `arguments.Branch is not null`：
  - 当前为 detached/无法解析 → `E_INVALID_ARGUMENT`；
  - `!string.Equals(arguments.Branch, currentBranch, StringComparison.Ordinal)` → `E_ACTION_NOT_ALLOWED`（或 `E_INVALID_ARGUMENT`），details 给 `expected/current`，**先于任何 git 网络命令**；
  - 补 `RepoActionSecurityTests` 负向用例（PULL branch≠current → 拒绝、HEAD 不变）。
- 复现记录：`%TEMP%\m2gate-h-*\results.tsv` 中 `AC-SAFE-17.pull_explicitBranchNotCurrent`（FAIL：`code=OK mainMoved=True mainNowEqualsOriginFeature=True`）。

### N-2【P2】写前 read-through 重扫 / `scan_at` 单调前进未实现
- 位置：`HostCommandRouter.cs:244-260`（`runRepoAction` 仅 `EnsureSnapshotAsync`，快照非空即用旧快照；`:372-378`）、`RepoActionService.cs:300-311`（仅 HEAD CAS）、`SnapshotService.cs:60-68`（仅写成功后失效）。
- 影响：矩阵判定仍基于扫描时状态；非 HEAD 变化（dirty/upstream/remote）不触发 `E_CONCURRENT_CHANGE`，依赖动作内部即时检查（安全失败但错误码不是 CAS）。与 FR-ACT-03/FR-REPO-01“操作前重新 scan”、AC-SAFE-15“动作前重扫 `scan_at` 单调前进”字面不一致。
- 建议：动作前强制 refresh（或锁内重算 `status --porcelain=v2 --branch`+upstream+remote URL 再过矩阵）；至少在 `docs/m2a-ac-report.md` 中按“偏差说明”口径记录。

### N-3【P2】仅远端存在的分支无法 CHKOUT（P1-3 修复引入的功能回退）
- 位置：`RemoteRefsService.cs:116-128`（返回 `REMOTE_BRANCH`）、`RepoActionService.cs:543-556`（只校验 `refs/heads/<branch>`）、`ui/src/components/overlays/ActionDialog.vue:304`（UI 已提示“需本地已有同名分支”）。
- 实测：本地仅有 `origin/remoteonly` 时 CHKOUT `branch="remoteonly"` → `E_REF_NOT_FOUND`（旧实现会 DWIM 建本地跟踪分支）。ADR 要求 CHKOUT 选择器来自 `remote_ref`，但 M2a 又无“建分支/检出远端分支”能力。
- 建议：对 `refs/remotes/origin/<branch>` 存在且本地无同名分支时用 `git switch --track -c <branch> origin/<branch>`（或 `checkout -b`），或在 UI 侧过滤远端分支并明确文案；二者取其一并在 ADR/验收报告记录。

### N-4【P2】CHKOUT 在 ref 校验前执行 stash，失败动作产生副作用
- 位置：`RepoActionService.cs:484-520`（dirty 且 `stash=true` 先 `git stash push -u`）→ `:522-557`（之后才校验 ref 存在性）。
- 实测：DIRTY 状态 + `branch="README.md"` + `confirm=true, stash=true` → 返回 `E_REF_NOT_FOUND`，但 `git stash list` 已新增 1 条（响应无 `stashRef`，用户不知已 stash）。
- 建议：把 ref 校验前移到 stash 之前；或在失败 details 中返回 `stashRef` 与提示。

### N-5【P2】新增错误码未进 UI `ACTION_ERROR_MAP`
- 位置：`ui/src/viewmodel.ts:621-692`（仅旧码）；本提交新增 `E_PUSH_REJECTED/E_WORKTREE_OVERWRITE/E_REF_NOT_FOUND/E_CREDENTIAL_SCOPE/E_GITEA_CONFIG_INVALID/E_GITEA_INSECURE_HTTP`（`src/MbdGitTool.Core/Contracts/BridgeErrors.cs:33-41`）。
- 影响：UI 回退显示 Host 原始 message（含摘要，不会误报成功），但缺标准 hint/三段式；建议补映射与 AC-UI-08 tooltip 口径核对。

### N-6【P2】`LocalScanner.OriginMatches` 仍 fail-open（原 P0-1 根因之扫描层）
- 位置：`src/MbdGitTool.Host/Services/LocalScanner.cs:608-611`（`!GiteaEnabled || giteaBase == null` → `return true`）、`:623-634`（baseUrl 非法 → null）。
- 影响：配置缺失/非法时任意 origin 仅凭仓名匹配即被扫描为合法仓（影响状态/按钮/展示）；**不再影响凭据**（动作层已 fail-closed + 作用域校验）。
- 建议：复用 `RepoActionService.CheckUrlScope`/AppConfig 严格校验，避免两套实现漂移；至少把 fail-open 改为“扫描标记不可操作 + 提示配置缺失”。

### N-7【P2 / 需求范围偏差，非安全回归】COMMIT 新增显式 `stageAll`（`git add -A`）
- 位置：`RepoActionService.cs:591-601`（`args.stageAll=true` 先 `git add -A`；`:220-232` 要求同时 `confirm=true`；UI 在 `ui/src/stores/workbench.ts:1196-1201` 明确提示“暂存全部改动（git add -A）”）。
- 对照：D-025 §2.2 条目 1“COMMIT 仅提交 staged（不做 `add -A`）”。该能力虽为显式勾选+明确文案，非静默行为，但属 M2a 冻结范围外的写能力扩展，建议由需求方裁决（保留则需 ADR 修订；否则移除）。

### 非本复评范围项现状（供 Gate 汇总）
- **P1-4**（http 明文传输 PAT）：**已关闭**——token 非空 + http base 未显式 `allowInsecureHttp` → `E_GITEA_INSECURE_HTTP`（实测，`RepoActionService.cs:741-755`；`AppConfig.cs:24-30,86-88`）。
- **P1-5**（`GIT_CONFIG_*` 被 hooks 子进程继承）：**仍未关闭**（代码确定：token base64 仍在子进程环境 `GIT_CONFIG_VALUE_0`；URL 作用域只限制“发给谁”，不阻止本地 hook 读取）。D-025 M2a 出口未列该项，但若 Gate 口径是“review-m2-code P1 全闭环”，则不能宣称全闭环。
- **P1-7**（双矩阵/Host availability/契约字段）：本报告未复评；本提交的 DTO 未见 `ComponentRepoDto.StagedCount` 等对应改动。

---

## 11. Gate 结论与建议

| 问题 | 结论 |
|---|---|
| 提交 `1afa014` 是否真正关闭 `review-m2-code` 的 P0-1、P1-1/2/3/6/8？ | **是：6/6 已关闭**（本报告独立实测 + 262 全绿 + 真实工作区烟测 hermetic 证据） |
| 是否建议 M2a gate 通过 / 打 tag？ | **否，建议本次不打 M2a tag**。理由：新发现 **N-1（P1）** 使 D-025 冻结的 **AC-SAFE-17 未全绿**（“PULL 显式分支≠当前分支 → 拒绝”未实现，且已实测可把当前分支 FF 到其它远端分支 tip）；另 AC-SAFE-15 的“动作前 read-through 重扫/`scan_at` 前进”无实现与证据（N-2），需在验收报告中交代。 |
| 阻断项修复成本 | N-1：约 10 行 + 1 个负向单测（`RepoActionSecurityTests`），可当天复评；N-2：可选实现 refresh 或在 `docs/m2a-ac-report.md` 按偏差口径记录。 |
| 修复后可打的 tag 口径 | 建议 `m2a`（仅 M2a 单仓安全交付）；**不得**打 `m2-ops`/宣称“M2 交付完成”（M2b 未接入，ADR §2.1/§2.4）。 |
| 其他 M2a 出口材料 | 当前仓库未见 `docs/m2a-ac-report.md`、`reqs/fixtures/m2-matrix.json`、`ui/scripts/cdp-m2a-selfcheck.mjs`（ADR §5 要求），即使安全/代码面闭环，也不具备宣布“M2a 全绿”的材料。 |

---

## 12. 关键复跑命令

```powershell
# 0) 当前目标提交
git -C D:\Workspace\git_1 rev-parse 1afa014

# 1) 全量测试（隔离构建，避免锁定运行中 Host）
dotnet test D:\Workspace\git_1\MbdGitTool.sln --artifacts-path "$env:TEMP\m2gate-artifacts1" -v minimal
#   期望：Core 71 + Host 191 = 262 通过；0 跳过

# 2) 独立服务级黑盒复评（37 断言；1 个 FAIL 即 N-1）
cd "$env:TEMP\m2gate-harness"; dotnet run

# 3) 真实工作区 hermetic 烟测（只读；不得设 MBD_RUN_REAL_WRITE_TESTS）
dotnet test D:\Workspace\git_1\tests\MbdGitTool.Host.Tests\MbdGitTool.Host.Tests.csproj `
  --no-build --filter "FullyQualifiedName~RealWorkspaceSmokeTests" -v normal
#   运行前后比对 artifacts/real-test/workspace 8 仓 HEAD/status：应无差异

# 4) N-1 最小复现（服务 API）：main 分支上执行 PULL args:{branch:"feature"}（feature 为更靠后的远端分支）
#   期望（AC-SAFE-17）：拒绝；实际 1afa014：Ok=true 且 main 被 FF 到 origin/feature
```

---

## 13. 附：证据摘录

**全量测试**

```text
已通过! - 失败: 0，通过: 71，已跳过: 0，总计: 71  - MbdGitTool.Core.Tests.dll
已通过! - 失败: 0，通过: 191，已跳过: 0，总计: 191 - MbdGitTool.Host.Tests.dll
```

**P0-1（独立夹具）**

```text
[PASS] P0-1b.hostMismatch_rejected: code=E_CREDENTIAL_SCOPE failedFields=fetch.host,push.host
[PASS] P0-1b.no_network_git: reqCount=0 subcommands=[rev-parse,rev-parse,remote,remote]
[PASS] P0-1c.pushurlMismatch_rejected: code=E_CREDENTIAL_SCOPE failedFields=push.port,push.path
[PASS] P0-1c.no_network_git: reqCount=0/0 subcommands=[rev-parse,rev-parse,remote,remote]
[PASS] P0-1d.configMissing_failClosed: code=E_GITEA_CONFIG_INVALID
[PASS] P0-1e.insecureHttp_rejected: code=E_GITEA_INSECURE_HTTP
[PASS] P0-1a.successBase_capture: capturedAuth=True reqCount=1
[PASS] P0-1a.response_no_token: response 无 token/base64/extraHeader
[PASS] P0-1a.argv_no_token: trace argv 不含 token/base64
[PASS] P0-1f.baseWithPath_capture: capturedAuth=True
```

**P1-1 / P1-3 / P1-2 / P1-6**

```text
[PASS] P1-1a.chkout_ignored_protected: code=E_WORKTREE_OVERWRITE file='LOCAL-PRECIOUS' headSame=True branch=main
[PASS] P1-1b.pull_ignored_protected:   code=E_WORKTREE_OVERWRITE file='LOCAL-PRECIOUS' headSame=True
[PASS] P1-3.branch_pathmode: code=E_REF_NOT_FOUND kind=branch headSame=True branch=main
[PASS] P1-3.tag_missing:     code=E_REF_NOT_FOUND kind=tag
[PASS] P1-2.cas_headMismatch: codes=[E_CONCURRENT_CHANGE,E_CONCURRENT_CHANGE,E_CONCURRENT_CHANGE,E_CONCURRENT_CHANGE]
[PASS] P1-2.zero_writes: headUnchanged=True branch=main
[PASS] P1-6.pushSummary_keyLines: code=E_PUSH_REJECTED summaryHasRejected=True
        summary=' ! [rejected]  HEAD -> main (fetch first) / error: failed to push some refs to … / hint: …'
```

**N-1（新 P1）**

```text
[FAIL] AC-SAFE-17.pull_explicitBranchNotCurrent: code=OK mainMoved=True mainNowEqualsOriginFeature=True
       （仓在 main，args:{branch:"feature"}；feature 为远端更新分支且 main 为其祖先）
```

**其他 AC-SAFE-17 子项（通过）**

```text
[PASS] AC-SAFE-17.plusPrefixRejected: CHKOUT '+main' code=E_INVALID_ARGUMENT
[PASS] AC-SAFE-17.pullPlusRejected:   PULL '+main' code=E_INVALID_ARGUMENT
[PASS] AC-SAFE-17.detachedPushBlocked: code=E_ACTION_NOT_ALLOWED headUnchanged=True
[PASS] AC-SAFE-17.pushNoForce: pushArgv=[git.exe --no-optional-locks -C … push origin HEAD] noForce=True
```

**P1-8（真实工作区烟测前后）**

```text
RealWorkspaceSmokeTests.RealWorkspace_NewApis_ReturnTreeData [1 s]  → 测试运行成功
WORKSPACE_DIFFS=0 (8 repos checked, read-only inspection)
```

**token 通道**

```text
[PASS] TOKEN.notInHostLog: logExists=True
[PASS] TOKEN.redact: redacted='Authorization: ***'
```

> 本报告只新增本文件；未修改任何源码、配置、真实工作区或运行中 Host。所有实测证据位于 `%TEMP%`（`m2gate-harness`、`m2gate-h-*`、`m2gate-exp-*`、`m2gate-http-*`、`m2gate-artifacts1`）。
