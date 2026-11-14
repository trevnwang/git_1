# M2 单仓写操作 · 对抗式代码与安全评审报告

| 项目 | 内容 |
|---|---|
| 评审对象 | `src/MbdGitTool.Host/Services/RepoActionService.cs`（核心）、`GitCli.cs`、`src/MbdGitTool.Host/Bridge/HostCommandRouter.cs`、`src/MbdGitTool.Core/Contracts/{BridgeParams,BridgeErrors,SnapshotDto,WorkbenchDto}.cs`、`SnapshotService.cs`、`LocalScanner.cs`、`ComponentAssembler.cs`、`HostInfoProvider.cs`、`AppConfig.cs`、`MainWindow.xaml.cs`、`HostLog.cs`；只读参考 `ui/src/{api.ts,viewmodel.ts,stores/workbench.ts}` |
| 上下文 | M2 已真实 Gitea 验证：COMMIT `e67eb05`、PUSH `267f953`、PULL FF、CHKOUT、FETCH（`docs/m2-ops-status.md`） |
| 基线 | 文档声明 234/234 测试通过（Core 71 + Host 163）；本报告**未重新运行全量测试**（原因见 §1.3） |
| 方法 | 逐行静态核对 + 本机 git 2.56.1 行为实验（12 组，含凭据抓包）+ 真实 Host 只读证据（CDP 9222 仅确认在运行，未发任何写请求；本机 HTTP 抓包仅用于验证 git 凭据注入机制） |
| 边界 | **只新建本报告，未改任何代码/配置/工作区**；未对运行中 Host 执行任何 UI 操作；未执行 `artifacts/real-test/workspace` 的真实写操作 |
| 日期 | 2026-11-14 |

---

## 0. 结论（Gate 判决）

**一句话：命令构造层（ArgumentList/无 shell）与 COMMIT staged-only、PUSH 禁 force、PULL `--ff-only` 三条硬约束实现正确；但凭据外发边界存在 1 个 P0（PAT 注入前不绑定 origin/pushurl，且配置非法时校验 fail-open——已用本机 HTTP 抓包证明 `Authorization: Basic base64(PAT:)` 会发往任意 origin），工作区保护存在“静默覆盖 ignored 文件”“CHKOUT 假成功”“快照门禁 TOCTOU”3 个 P1；当前不适合作为 M2 安全签收，需先修 P0-1 与 P1-1/P1-3（小改动）+ 补安全负向测试。**

| 口径 | 结论 |
|---|---|
| 安全 P0（可致凭据外泄的可利用链） | **1**：P0-1（详见 §3） |
| 功能/数据 P1 | **8**（P1-1…P1-8；其中 5 条已复现，3 条为确定性代码路径/结构性缺口） |
| P2 | **13**（见 §9） |
| 命令注入（本题 1） | **未发现可利用注入**：所有 git 参数均走 `ArgumentList`，无 shell/字符串拼接；branch/tag 校验覆盖 `-` 前缀、空白/控制字符、`..`、`@{`、`~^:?*[\`、`.lock`、≤255；message 经 `-m <argv>` 传递不可当选项。遗留 `+`（force refspec）、`checkout` 路径模式、message 控制字符 3 个边界（P2-3/P1-3/P2-5） |
| 凭据（本题 2） | **不通过**：注入方式本身正确（仅 `GIT_CONFIG_*` 环境、不进 argv），但“会不会发给谁”未校验（P0-1）；http 明文（P1-4）；`GIT_CONFIG_VALUE_0` 被 git 子进程/hook 继承（P1-5）；日志/响应主链路脱敏基本成立，规则有缺口（P2-6） |
| 工作区安全（本题 3） | **部分不通过**：CHKOUT 未防 ignored 文件覆盖（P1-1）、未校验 ref 存在性可假成功（P1-3）；PULL 确实不会非 FF 覆盖（`--ff-only` 实测），但可覆盖 ignored 文件且无本地 clean 复检；COMMIT 确认只提交 staged（无 `add -A` 残留）；PUSH 无 force |
| 并发/竞态（本题 4） | **部分通过**：per-repo 锁覆盖执行全程、跨实例共享、UI 连点单发；但快照永不失效 → 门禁 TOCTOU（P1-2）；锁字典无淘汰、仅进程内（P2-1/P2-2） |
| 契约/错误（本题 5） | **基本可用**：失败不会被当成功（`accepted` 缺省为 false）；E_CONFIRM_REQUIRED 只重试一次（代码确认）；但 stderr 摘要只取首行导致诊断失真（P1-6）、Host/UI 双矩阵漂移（P1-7）、busy/timeout 语义与 120s 竞态（P2-7） |
| 测试缺口（本题 6） | **不通过**：234 用例未覆盖本报告任何一条安全边界；`RealWorkspaceSmokeTests` 会对真实工作区执行真实 PULL（P1-8） |
| **能否提交** | **不能直接提交**。先修 P0-1；P1-1/P1-3 为单行级修复建议同批修；P1-2/P1-4/P1-5/P1-6/P1-7 排入 M2 收口；P1-8 安全测试必补。 |

---

## 1. 评审方法与证据

### 1.1 静态核对
逐行通读 §0 所列文件；交叉核对 UI `executeRepoAction` 与 Host 信封/错误码；用 `grep` 确认全仓**没有** `git add -A`、没有 `ProcessStartInfo.Arguments` 字符串拼接、没有 `cmd.exe`/shell 调用（仅 `ArgumentList`，见 §2）。

### 1.2 本机 git 行为实验（12 组，均为临时目录，不触碰被测仓库）
| # | 实验 | 实测结果 | 用于 |
|---|---|---|---|
| E1 | unborn HEAD：`diff --cached --quiet` / `commit` | exit 1 / root commit 成功 | EMPTY+COMMIT 可用（无缺陷） |
| E2 | `git checkout README.md`（无同名 ref、工作区 clean） | `exit=0`，输出 `Updated 0 paths from the index`，分支未变 | P1-3 假成功 |
| E3 | clean 仓 + ignored `build.log`，目标分支 track 同路径 | `git status --porcelain` 为空；`git checkout other` **exit=0 且覆盖 ignored 文件**；`--no-overwrite-ignore` 时 exit=1 拒绝 | P1-1（CHKOUT） |
| E4 | 同上，`git pull --ff-only` / `merge --ff-only --no-overwrite-ignore` | pull exit=0 **覆盖**；`pull --no-overwrite-ignore` 不存在（exit=129）；`fetch`+`merge --no-overwrite-ignore` exit=1 拒绝 | P1-1（PULL 修复方案） |
| E5 | porcelain v2 冲突仓 | `u UU N...` 行；`status --porcelain` 为 `UU`（非空） | §2.4 stagedCount / dirty 计数 |
| E6 | detached HEAD `git push origin HEAD` | exit=1（`dest…not a full refname`），远端无新 ref | P2-9 安全性确认 |
| E7 | unborn HEAD `git push origin HEAD` | exit=1（`src refspec HEAD does not match any`） | P2-9 矩阵与实现不一致 |
| E8 | `git pull --ff-only origin +main` | exit=0（`+` 被接受为 fetch force refspec） | P2-3 校验缺口 |
| E9 | 凭据抓包：`GIT_CONFIG_COUNT/KEY_0/VALUE_0` 注入 `http.extraHeader`，origin 指向本机 HTTP 服务 | 服务端收到 `Authorization: Basic <base64(PAT:)>`（与 Host `BuildCredentialEnvironment` 完全同构） | P0-1；同时 git/GCM 警告 “use of unencrypted HTTP remote URLs is not recommended” → P1-4 |
| E10 | pre-push hook 打印环境变量 | hook 子进程看到 `GIT_CONFIG_COUNT=1`、`GIT_CONFIG_VALUE_0=Authorization: Basic …`（含 PAT） | P1-5 |
| E11 | 父环境设置 `GIT_DIR` 后 `git -C <另一仓> rev-parse --abbrev-ref HEAD` | 返回 `GIT_DIR` 指向仓的分支（`-C` 被环境覆盖） | P2-10 |
| E12 | `remote.origin.pushurl` 与 fetch url 不同 | `remote get-url origin` 返回 Gitea，`remote get-url --push origin` 返回任意 host；扫描只校验前者 | P0-1 |
| E13 | PUSH 被拒 / PULL 非 FF 的 stderr 首行 | 首行分别是 `To <url>` / `From <url>`；真实原因 `! [rejected]`、`fatal: Not possible to fast-forward` 在其后 | P1-6 |

### 1.3 未运行 234 测试的原因
`tests/MbdGitTool.Host.Tests/RealWorkspaceSmokeTests.cs:162-170` 在真实工作区存在时会对 `artifacts/real-test/workspace` 的仓**执行真实 `runRepoAction PULL`**（该工作区 8 个仓的 origin 为 `http://192.168.50.20:3000/RTCRepos/*`）。为避免测试进程修改真实工作区/访问真实 Gitea，本轮未跑全量；测试覆盖结论来自逐文件阅读全部测试源码。此非 hermetic 行为本身记为 **P1-8**。

---

## 2. 命令注入评审（本题 1）

### 2.1 结论：未发现可利用命令注入
- 所有 git 调用唯一入口 `GitCli.RunAsync`（`GitCli.cs:53-140`）：`UseShellExecute=false`、`FileName="git"`、全部参数 `startInfo.ArgumentList.Add`（`GitCli.cs:74-80`）；`-C <path>` 也是独立 argv，路径含空格/引号不会破坏参数边界。
- `RepoActionService` 全部 10 处 git 调用均为字面量数组 + 受校验变量拼接（`RepoActionService.cs:303-534`），无 shell、无字符串命令。
- `branch/tag` 不会紧跟 `-`：`ValidateRefName` 首字符 `-` 直接拒绝（`RepoActionService.cs:870-873`），因此 `git checkout <branch>` / `git pull --ff-only origin <branch>` 的**选项注入不成立**；`:`/`*`/`[`/`\`/空白/控制字符被拒（`:875-886`），`..`/`@{`/`.lock`/尾部 `.` 或 `/` 被拒（`:888-895`）。
- `message` 经 `-m <argv>` 传递（`RepoActionService.cs:459-461`），`--author=/--file=` 等内容不会被解析为选项；长度 ≤2000（`:750-757`），JSON 类型强校验（`:793-837`）。
- `confirm/setUpstream` 只接受 JSON 布尔（`:819-837`），不接受字符串。

### 2.2 残留边界（均已给修复）
| 边界 | 位置 | 状态 | 说明 |
|---|---|---|---|
| branch 前导 `+` 未拒绝 | `RepoActionService.cs:862-898` | **已复现**（E8） | `git pull --ff-only origin +main` 被 git 接受为 force refspec（只影响 remote-tracking ref，`--ff-only` 仍约束 HEAD，故非 P1）。建议：`value[0] is '-' or '+'` 一并拒绝，或统一 `git check-ref-format --branch` 复核。 |
| `checkout <branch>` 路径模式 | `RepoActionService.cs:406-427` | **已复现**（E2） | 不存在的 ref 但同名文件存在时，`git checkout <path>` exit 0 且不改分支 → UI 收到成功。见 P1-3。 |
| message 未拒绝 NUL/控制字符 | `RepoActionService.cs:750-757` | 理论 | `git commit -m` 的 argv 内嵌 `\0` 会被 Win32 命令行截断/产生异常提交消息；建议拒绝 `\0`（保留 `\n` 多行）并定义多行策略。见 P2-5。 |
| 未知 args 静默忽略 | `RepoActionService.cs:691-761` | 代码确定 | `args:{"force":true}`、拼写错误字段均被静默丢弃（不会变成 force，安全，但契约无感知）。见 P2-4。 |
| branch 可解析为 tag/commit | `RepoActionService.cs:406-427` | 理论 | `checkout` 分支名若是 tag/SHA 会进入 detached，而响应 `state` 仍是动作前快照态；不造成破坏，但语义与“检出分支”不符。见 P2-3。 |

---

## 3. 凭据评审（本题 2）

### 3.1 注入方式本身：正确
- PAT 仅进入子进程环境：`GIT_CONFIG_COUNT=1`、`GIT_CONFIG_KEY_0=http.extraHeader`、`GIT_CONFIG_VALUE_0=Authorization: Basic base64(token:)`（`RepoActionService.cs:536-553`）；`extraEnvironment` 仅写入该次 `ProcessStartInfo.Environment`（`GitCli.cs:88-94`）。
- **不进 argv**：代码层确认；实验 E9 进程命令行不含 token（token 只出现在抓包 header 中）。
- 并发串用：`BuildCredentialEnvironment()` 每次新建字典，`GitCli` 每次新建 `ProcessStartInfo`，**不存在跨请求/跨仓共享串用**（两个 `RepoActionService` 实例并发各自独立）。
- 非网络动作（CHKOUT/COMMIT/status/rev-parse）不注入任何凭据环境（`RepoActionService.cs:377-468` 均未传 `extraEnvironment`），正确。

### 3.2 P0-1【已复现-机制链】PAT 可被发送到未绑定/任意 origin（含配置 fail-open）
- **根因**：`RepoActionService` 在注入凭据前**没有任何 origin 校验**；唯一校验发生在扫描期 `LocalScanner.ScanRepoAsync` 的 `git remote get-url origin`（`LocalScanner.cs:210-223`）+ `OriginMatches`（`:557-621`），且该校验：
  1. 只校验 fetch URL，不校验 `remote get-url --push`（`pushurl` 可指向任意 host，实验 E12）；
  2. `if (!config.GiteaEnabled || giteaBase is null) return true;`（`LocalScanner.cs:608-611`）——**fail-open**：Gitea 未配置或 baseUrl 非法（如漏写 `http://`）时，只要 origin 末尾仓名与目录名相同即判合法；
  3. 校验只在快照扫描时发生一次，动作时不复检（快照可能永久不失效，见 P1-2），`git remote set-url/pushurl` 的任何变更都不会被拦截。
- **可复现链（无需攻击者写仓）**：用户 `MBD_GITEA_TOKEN` 已配置（环境变量全局存在），但 `MBD_GITEA_URL`/`MBD_GITEA_ORG` 未配置或写错（`AppConfig.GiteaEnabled` 只看非空，`AppConfig.cs:29-30`；token 单独读取 `AppConfig.cs:77,84`）→ `HostCommandRouter.cs:76` 仍把 token 交给 `RepoActionService` → 工作区中任意 origin（GitHub/内网/攻击者 HTTP 服务）只要仓名匹配即被扫描为合法 → 用户点 FETCH/PULL/PUSH → 实验 E9 已证明 git 会把 `Authorization: Basic base64(PAT:)` 发给该 origin。
- **影响**：PAT 外泄（可被第三方服务器/网络中间人记录），且外发动作由用户正常点击触发，无提示。
- **修复（建议全部做）**：
  1. `RepoActionService` 注入凭据前（锁内）执行 `git remote get-url origin` **与** `git remote get-url --push origin`，要求 scheme/host（及 org 路径）与 `AppConfig.GiteaBaseUrl` 完全匹配；不匹配 → `E_ACTION_NOT_ALLOWED`（新增 `E_CREDENTIAL_SCOPE` 亦可）并**不启动 git**；
  2. 配置非法/缺 Gitea 但存在 token → **fail closed**：网络动作直接拒绝（提示“Gitea 配置不完整，禁止携带 PAT”），而不是继续注入；
  3. 把 header 作用域化：`GIT_CONFIG_KEY_0 = "http.<canonical gitea base>.extraHeader"`（git 支持 URL 作用域），即使 origin 被改也不外发；
  4. token 为空时网络动作照旧（公开仓），保持现状。

### 3.3 P1-4【已复现（环境事实）】PAT 明文 HTTP 传输
- `AppConfig`/`LocalScanner.BuildGiteaBaseUri`/`GiteaClient` 都允许 `http://`（`AppConfig.cs:29`、`LocalScanner.cs:623-634`、`GiteaClient.cs:66-69`）；真实验证环境即 `http://192.168.50.20:3000`（`artifacts/real-test/workspace/RTCT_Demo_Impl` 的 origin，实测）。实验 E9 中 git/GCM 也明确警告 “use of unencrypted HTTP remote URLs is not recommended”。
- 影响：同网段可嗅探/篡改，`Authorization: Basic`（可逆）即 PAT；中间人可回放写操作。
- 修复：token 非空时强制 https（含 `gitea.baseUrl` 与 origin 校验）；确需内网 http 时增加显式配置（如 `gitea.allowInsecure=true`）并在 UI（关于/设置）与日志中明示风险；至少对 `127.0.0.1` 放行。

### 3.4 P1-5【已复现】`GIT_CONFIG_*` 凭据环境被 git 子进程（hooks/helper）继承
- 实验 E10：`pre-push` hook（git 的子进程）可直接读到 `GIT_CONFIG_VALUE_0=Authorization: Basic <base64(PAT:)>`。
- 攻击面：① 工作区仓若是“拷贝目录”而非 `git clone` 得到，`.git/hooks/*` 可被植入，用户一次 PUSH 即让 hook 读取 PAT；② 用户的 `core.hooksPath`/全局配置可让任意脚本作为 hook 运行；③ 若仓库/全局配置 `submodule.recurse=true`，`git pull` 会把同一 `http.extraHeader` 发往**子模块的第三方 host**（理论延伸）。
- 说明：hook 需要本地写权限/来源不可信仓，故不单独评 P0；但结合 P0-1 的“作用域 header”修复可大幅收敛。
- 修复：同 P0-1 第 3 点（`http.<gitea-base>.extraHeader` 作用域）；对网络动作可考虑 `GIT_CONFIG_KEY` 同时设置 `core.hooksPath` 的用户提示；文档明确“会执行仓内 hooks”。

### 3.5 日志/响应/异常 details/脱敏（检查结果）
| 通道 | 结论 | 依据 |
|---|---|---|
| 响应 `output` | ✅ 先脱敏后截断 2000 | `RepoActionService.cs:621-622,657-658` |
| 错误 `message/details.stderr` | ✅ 走 `RedactSecrets`（配置 token 明文与 base64 精确替换 + `HostLog.Redact`） | `:586-596,624-640,660-678` |
| Host 日志 | ✅ 只记 action/组件/code/ms，无输出 | `:275-285`；`HostCommandRouter.cs:141-142` |
| 进程命令行 | ✅ token 不在 argv | §3.1 |
| 继承环境/hook | ❌ 可读（P1-5） | E10 |
| 脱敏规则完整性 | ⚠️ 缺口（P2-6） | 见 §8 P2-6 |

---

## 4. 工作区安全评审（本题 3）

### 4.1 CHKOUT clean 校验
`RepoActionService.cs:383-404`：`git status --porcelain` 输出非空即 `E_DIRTY_WORKTREE`。逐类文件核对：

| 文件类 | 是否计入 status | 结论 |
|---|---|---|
| 已跟踪改动（staged/unstaged） | ✅ | 拦截 |
| untracked 文件/目录 | ✅（默认 `-unormal`） | 拦截 |
| ignored（`.gitignore`） | ❌ 不显示 | **P1-1：不拦截，checkout 会静默覆盖（E3）** |
| 冲突（unmerged） | ✅ 显示 `UU` 等 | 拦截（即便快照过期也不会进 checkout） |
| 进行中 rebase/merge 且工作区 clean | status 可能为空 | git 自身拒绝 checkout（exit≠0），安全兜底 |
| submodule（gitlink/子模块内改动） | 部分（子模块 HEAD 变化会显示，子模块内 untracked 默认不显示） | 无破坏（checkout 不递归更新子模块），P2 提示 |
| assume-unchanged / sparse-checkout 文件 | ❌ 不显示 | 理论：路径模式 checkout 可覆盖（E2 同类，P2） |

### 4.2 P1-1【已复现】CHKOUT/PULL 可静默覆盖 `.gitignore` 忽略的本地文件（数据丢失）
- 位置：`RepoActionService.cs:383-404`（clean 判定不含 `--ignored`）、`:410-416`（checkout 未加 `--no-overwrite-ignore`）、`:343-350`（pull 无任何本地 clean 复检，且 `git pull` 不支持该开关）。
- 复现（E3/E4，临时仓实测）：
  1. 仓 A：`.gitignore` 含 `*.log`，`build.log` 在目标分支被 track；当前分支工作区有 ignored `build.log`（内容 `LOCAL-PRICIOUS`）；
  2. `git status --porcelain` 为空 → Host 判定 clean；
  3. `git checkout other` → **exit 0，`build.log` 被替换为远端内容**；`git pull --ff-only` 同样覆盖。
- 影响：`.env`、本地配置、构建产物、笔记等 ignored 文件在 CHKOUT/PULL 时无声丢失；ignored 文件常被认为是“可丢弃”，但并非全部。
- 修复：
  - CHKOUT：`git checkout --no-overwrite-ignore <branch|--detach tag>`；exit 1 时映射 `E_DIRTY_WORKTREE`（“存在会被覆盖的 ignored 文件，请先备份/移走”）；
  - PULL：改为 `git fetch origin <branch>` + `git merge --ff-only --no-overwrite-ignore FETCH_HEAD`（E4 已验证可拦截；注意 `git pull` 不接受该选项，实测 exit 129）；
  - 可选：clean 判定追加 `git status --porcelain --ignored=matching` 并对与目标树冲突的路径给出可读提示。

### 4.3 P1-3【已复现】CHKOUT 可“假成功”（ref 不存在但同名路径存在）
- 位置：`RepoActionService.cs:406-427`（直接 `git checkout <branch>`，无 `rev-parse --verify`、无执行后目标校验）。
- 复现（E2）：SYNCED 仓、工作区 clean 且有 `README.md`；传 `args.branch="README.md"` → `git checkout README.md` 进入**路径模式**，exit 0，输出 `Updated 0 paths from the index`，分支仍是 `main`；Host 返回 `accepted=true`、`message="CHKOUT 完成（分支 README.md）"`（响应 `branch` 字段为实际 main，UI Toast 因此显示 main，但动作仍被判定成功）。
- 影响：违反“禁止静默失败/失败必须可感知”；用户以为已切分支，后续基于错误分支操作（虽 PUSH 无 force，但可能把提交推到错误分支的语境）。**不会**覆盖改动（clean 前提），故评 P1 而非 P0。
- 修复：动作前 `git rev-parse --verify --quiet refs/heads/<branch>`（tag 用 `refs/tags/<tag>`），不存在 → `E_INVALID_ARGUMENT`“分支/Tag 不存在”；或改用 `git switch <branch>`（不会路径模式）/ `git switch --detach`；动作后校验 `rev-parse --abbrev-ref HEAD` 与请求一致。

### 4.4 PULL 是否可能非 FF 覆盖：否（安全项确认）
- 固定 `git pull --ff-only origin <branch>`（`RepoActionService.cs:343-350`）；非 FF 返回 `E_GIT_NON_FF`（`:354-368`，`IsNonFastForward` 识别 3 种文案，`:613-619`）。实验：dirty 非冲突可 FF（保留本地改动）；dirty 冲突/untracked 冲突均 abort（exit≠0），HEAD 不变。
- 残留：① ignored 文件覆盖（P1-1）；② 未本地复检 dirty（快照过期时 DIRTY 仍可能执行，但 git 兜底）；③ 显式 `branch` 未校验 == 当前分支，可能把当前分支 FF 到**另一个远端分支的 tip**（P2-3）。

### 4.5 COMMIT 是否只提交 staged：是（安全项确认）
- 逐个 grep 确认 `src/` 内无 `git add` / `add -A`；提交路径只有 `git commit -m <message>`（`RepoActionService.cs:459-461`），前置 `git diff --cached --quiet`（`:446-457`，exit 0→`E_NOTHING_TO_COMMIT`，exit 1→继续，>1→失败）。
- unborn HEAD（EMPTY）实测 `diff --cached --quiet` exit 1、`git commit` 可生成 root commit（E1），与矩阵 EMPTY-COMMIT=Y 一致。
- 注意（P2-5）：`pre-commit`/`commit-msg` hook 仍可修改 index/拒绝提交，“staged-only”在 hook 参与下可能包含 hook 新暂存的文件；属 git 语义，建议文档说明。

### 4.6 PUSH 是否可能 force：否（安全项确认）
- 仅构造 `push [ -u ] origin HEAD`（`RepoActionService.cs:476-483`），无 `--force/-f/--force-with-lease`；`setUpstream` 为布尔，只影响 `-u`。
- 实测：detached HEAD `push origin HEAD` 失败且远端无新 ref（E6，安全失败）；unborn HEAD 必然失败（E7）。
- 残留：`pushurl` 不在扫描校验内（P0-1）；EMPTY 行矩阵 PUSH=Y 但必然失败（P2-9）。

### 4.7 stagedCount 解析（porcelain v2 X 列）
- 实现：`LocalScanner.ParsePorcelainStatus` 对 `1 `/`2 ` 行取 `line[2]` 为 X 列，`X ∉ {' ', '.'}` 计数（`LocalScanner.cs:464-476`），映射 `RepoFacts.StagedCount`（`:253`）→ `ComponentAssembler.ToRepoDto`（`ComponentAssembler.cs:297-320`）→ `RepoDto.StagedCount`（`SnapshotDto.cs:58`）→ UI `repo.stagedCount`（`viewmodel.ts:161`，门禁 `:700-707`）。
- 结论：**X 列取位正确**（已按 `1 A. N…` / `2 R. …` 实际输出核对）；rename（`2 `）计入；untracked（`? `）不计；ignored 不计（合理）。
- 缺陷（P2-8）：冲突行 `u UU …` 只置 `Unmerged=true`，**不增加 `DirtyCount`/`StagedCount`**（`LocalScanner.cs:478-482`），出现“UNMERGED 仓 dirtyCount=0、stagedCount=0”的展示误导；状态判定仍正确（`RepoStateEvaluator.cs:78-86` 优先 Unmerged）。
- 契约缺口（P1-7）：`getComponentDetail` 的 `ComponentRepoDto` 不含 `StagedCount`，且 Host `GetAvailability` 不以 stagedCount 收口 → 任何消费 `getComponentDetail.actions` 的客户端会看到“0 staged 时 COMMIT enabled”。

---

## 5. 并发/竞态评审（本题 4）

| 项 | 结论 | 依据 |
|---|---|---|
| per-repo 锁覆盖范围 | ✅ 校验后的执行全程（含前置 readHead、clean/staged 检查、git 写、post read） | `RepoActionService.cs:224-235`，锁内 `ExecuteAsync` |
| 跨实例/快速连点 | ✅ `RepoLocks` 为 static；同仓两个 service 实例串行（`SameRepo_SameGitActions_AreSerialized`）；UI `state.action.pending` 同步置位防连点 | `:45-46`；`RepoActionGitTests.cs:347-368`；`workbench.ts:1046,1190-1191` |
| 不同仓并发 | ✅ 各自一把锁，符合设计；但无全局并发上限（`runRepoAction` 明确绕过 `_ioGate`，`HostCommandRouter.cs:240-247`），多仓连点可并发多个 git 进程（资源放大，P2） | `HostCommandRouter.cs:60,240-247` |
| 锁字典泄漏 | ⚠️ `ConcurrentDictionary<string, SemaphoreSlim>` 只增不减、Semaphore 不 Dispose；量级=仓数，P2 | `:45-46,225` |
| 快照刷新竞态 | ⚠️ `refresh`（LocalScanner，最多 8 并发 git）与写动作**互不互斥**；刷新期间可能采到“动作中间态”并写入 current snapshot（随后被 UI 再次 refresh 覆盖）。写动作完成不自动重建 index，依赖客户端 refresh | `HostCommandRouter.cs:217-221` vs `:245`；`SnapshotService.cs:96-104` |
| **门禁 TOCTOU（P1-2）** | ❌ 矩阵判定基于快照态；快照**没有 TTL/自动刷新**（`SnapshotService.GetSnapshotAsync` 命中缓存即返回，`SnapshotService.cs:61-79`；进程内仅启动时预热一次（`MainWindow.xaml.cs:96`），之后只有 UI 显式 `refresh` 才更新；`Freshness.LocalStale` 恒 false，`:402-406`）；动作前 `RepoActionService.cs:198-222` 注释声称“必须在磁盘/Git 上再收口一次”，实际只查目录存在 + `.git` 存在，**未重算状态**。外部 `git checkout/merge/rebase/remote set-url` 后点写按钮可绕过矩阵 | 代码确定；UI 侧无 `E_CONCURRENT_CHANGE`（`viewmodel.ts:639-643` 有映射，Host 从不返回） |
| 多 Host 实例 | ⚠️ 锁仅进程内；两个 Host 实例同时操作同一工作区无互斥（git 自身 index.lock 只保护部分命令），P2 | `:45-46` |

---

## 6. 契约/错误评审（本题 5）

- **错误码语义**：`E_ACTION_NOT_ALLOWED`（矩阵）、`E_CONFIRM_REQUIRED`、`E_DIRTY_WORKTREE`、`E_NOTHING_TO_COMMIT`、`E_GIT_NON_FF`、`E_GIT_FAILED`、`E_TIMEOUT`、`E_INVALID_ARGUMENT`、`E_REPO_NOT_LOCAL` 都可区分（`BridgeErrors.cs:27-35`）。但 `E_REPO_BUSY` 定义了却从不产生（锁等待/超时变 `E_TIMEOUT`），UI `E_BUSY/E_CONCURRENT_CHANGE` 映射为死代码（`BridgeErrors.cs:19`；`viewmodel.ts:634-643`）。
- **UI 是否可能把失败当成功**：✅ 不会。Host 失败信封 `ok=false` → `settleCall` reject（`api.ts:415-420`）；成功 data 缺 `accepted` 时 `normalizeRepoActionResult` 缺省 `{accepted:false}`（`api.ts:897-902`）→ `executeRepoAction` 抛错（`workbench.ts:1211-1213`）。Host 仅在 `outcome.Failure is null` 才 `Success`（`RepoActionService.cs:273-300`）。
- **E_CONFIRM_REQUIRED 重试只一次**：✅ 代码确认：首次未确认 → 弹确认 → `confirmed=true` 重试；再次 `E_CONFIRM_REQUIRED` 走错误分支不再重试（`workbench.ts:1237-1265`）。Host 侧 confirm 为无状态布尔，仅 UI 语义约束（P2-11）。
- **exitCode/stderr 截断脱敏**：✅ 2000/500 截断且先脱敏；❌ 摘要算法只取**第一行**（`RepoActionService.cs:624-640`）→ PUSH 拒绝时首行是 `To <url>`、PULL 非 FF 时首行是 `From <url>`（E13 实测），真实原因（`! [rejected] … fetch first` / `fatal: Not possible to fast-forward`）丢失；PULL 另走 `E_GIT_NON_FF` 有中文说明，PUSH 只得到 `E_GIT_FAILED`，UI 提示又写“仓库未做破坏性改动”（`viewmodel.ts:608-612`）——对 PUSH/PULL 超时/部分执行而言是过度承诺。→ P1-6。
- **超时预算**：Router 120s（`HostCommandRouter.cs:41`）、GitCli 网络 120s（`RepoActionService.cs:41`）、UI 120s（`api.ts:273`）三者相等：UI 可能先超时并把“可能已成功”的写操作显示为失败，且 Router 在 git 成功后还要做 readHead/readBranch，边界上会被自身 120s 截断 → P2-7。
- **响应 `state`**：返回的是**动作前**快照态（`RepoActionService.cs:293`），UI 当前未使用；若后续消费会误导 → P2（并入 P1-7）。

---

## 7. 测试缺口（本题 6）

现有 234 用例对本报告安全边界**零覆盖**（`RepoActionGitTests.cs` 17 个 + `RepoActionAndComponentDetailTests.cs` 8 个 + `HostCommandRouterTests.cs` 13 个中仅 2 个涉及 runRepoAction；其余为只读/契约测试）。已覆盖：ArgumentList 基础、PULL FF/非 FF、COMMIT staged/unstaged、CHKOUT dirty、PUSH 成功/拒绝、token 不在响应、非法参数表、同仓串行。

### 7.1 必须新增的测试（建议放入 `RepoActionSecurityTests.cs` / 扩展现有文件）
1. **凭据外发边界（P0-1，最高优先）**：`RepoActionService` + 本机 `HttpListener` 作为 origin，仓 origin 改为 `http://127.0.0.1:PORT/RTCT_Demo_Impl.git`，token 配置为假 PAT；断言①请求到达前 Host 即拒绝（修复后）或②若放行则抓包 header 含 PAT（描述现状，用于回归）；同时覆盖 `remote set-url --add --push` 的 pushurl 分支与“token 已配但 Gitea 配置缺失”用例。
2. **ignored 覆盖（P1-1）**：夹具 `.gitignore`+另一分支 track 同路径，本地 ignored 文件；CHKOUT/PULL 后断言文件内容不变且返回 `E_DIRTY_WORKTREE`（修复后）。
3. **CHKOUT 假成功（P1-3）**：`branch="README.md"`（同名文件存在）→ 断言 `E_INVALID_ARGUMENT` 且 HEAD/分支不变；tag 不存在同理。
4. **ref 校验表**：`\n`/`\t`/`\0`/`\u007f`、`@{`、`+main`、`-x`、`a..b`、`a.lock`、`a/`、`a.`、256 字符、`refs/heads/main`、unicode bidi/零宽字符；断言 E_INVALID_ARGUMENT 且无 git 写。
5. **message 边界**：2000 通过 / 2001 拒绝 / 空白拒绝；`-m` 风格值（`--author=x`、`-F`）作为消息原样入库；多行保留；`\0` 处理（修复后拒绝）。
6. **PUSH 无 force**：本地与远端分叉 + `args:{"force":true}` / `args:{"branch":"-f"}` → 远端 ref 不变、返回失败。
7. **PULL 安全**：本地 dirty 非冲突/冲突两种；ignored 冲突；`branch` != 当前分支（修复后拒绝）；`+main`（修复后拒绝）。
8. **stagedCount 解析单测**：porcelain v2 的 `1 A./1 .M/1 AM/2 R.`、unmerged `u UU`、ignored、submodule 行 → 断言 LocalScanner 的 `StagedCount/DirtyCount/Unmerged`。
9. **EMPTY 状态**：unborn + staged → COMMIT 成功（root commit）；EMPTY+PUSH 断言失败路径（或矩阵修正后禁用）。
10. **超时/取消**：假 git（`GitExecutable` 指向 sleep 脚本）→ `E_TIMEOUT`、进程树被杀、锁释放（再次调用可执行）。
11. **token 不落日志**：执行网络动作（fake origin 失败）后读取 `HostLog.LogFilePath`，断言 PAT 明文与 base64 均不出现。
12. **子进程环境（P1-5 现状回归）**：假 git 打印 `GIT_CONFIG_VALUE_0` → 断言网络动作包含、CHKOUT/COMMIT 不包含。
13. **并发**：同仓 PUSH/CHKOUT 并发串行；异仓并发均成功；refresh 与 action 并发不产生不存在的状态；对 `RepoLocks` 无公开 API 时至少验证连续 20 个不同仓路径后行为正常（字典增长可加 internal 可见性断言）。
14. **契约**：`RepoDto.stagedCount` 序列化存在；若补 `getComponentDetail.stagedCount`/Host availability 计 staged，则加契约测试对齐 UI `viewmodel.MATRIX` 与 Host `Matrix`（当前 SYNCED-CHKOUT 不一致，见 P1-7）。
15. **非功能**：`RealWorkspaceSmokeTests` 的 PULL 改为 `MBD_REAL_SMOKE=1` 才执行，或换成 `git rev-parse` 只读探测；默认 CI 不接触真实工作区/Gitea。

### 7.2 文档/事实一致性
- `docs/m2-ops-status.md:15` 称“PAT ... 不进参数/日志/响应”——对日志/响应主链路成立，但未覆盖 P0-1/P1-4/P1-5 的外发与子进程场景，建议加限定；
- 同文 `:4` 称“15 仓”，实际 `artifacts/real-test/workspace` 只有 8 个目录（`RTCT_Demo_*`/`RTCT_Synced_*` 各 4）→ 证据口径需更新；
- `:17` 称“无 staged 时 COMMIT 禁用”，实际只有 UI 按快照禁用，Host `GetAvailability` 仍报 enabled（P1-7）。

---

## 8. P0/P1 清单（文件:行号、复现/攻击、修复）

> 状态标记：**【已复现】**=本机实验或确定性运行证据；**【代码确定】**=静态可证；**【理论】**=需特定夹具/本地写权限。

### P0（1 项，阻断）

**P0-1 PAT 注入前不绑定 origin/pushurl，配置非法时校验 fail-open——PAT 可发往任意 URL【已复现-机制链】**
- 位置：`RepoActionService.cs:536-553`（注入）、`:303-323,325-375,470-501`（网络动作不校验 origin）；`LocalScanner.cs:210-223`（只查 fetch URL）、`:608-611`（fail-open）；`HostCommandRouter.cs:76`（Gitea 配置不完整也交 token）；`AppConfig.cs:29-30,77,84`；实验 E9/E12。
- 攻击/复现：配置 `MBD_GITEA_TOKEN` 但 URL/ORG 缺失或写成非 http(s) → 任意 origin 被扫描为合法 → 用户点击 FETCH/PUSH → 实验同构命令使本机 HTTP 服务收到 `Authorization: Basic base64(PAT:)`；即使配置正确，`remote.origin.pushurl` 或扫描后 `git remote set-url` 也能把 PAT 导向其他 host。
- 修复：§3.2 四步（动作前锁内校验 fetch+push URL 与 Gitea base 一致、fail-closed、`http.<base>.extraHeader` 作用域、token 为空才允许非 Gitea 源）。

### P1（8 项）

**P1-1 CHKOUT/PULL 静默覆盖 ignored 本地文件（数据丢失）【已复现 E3/E4】**
- 位置：`RepoActionService.cs:383-404,410-416,343-350`。
- 复现：见 §4.2；`git status --porcelain` 为空 → checkout/pull exit 0 覆盖 ignored 文件。
- 修复：checkout 加 `--no-overwrite-ignore`；PULL 改 `fetch` + `merge --ff-only --no-overwrite-ignore`；补负向测试。

**P1-2 门禁基于永不失效快照，动作前未本地重算状态（TOCTOU）【代码确定】**
- 位置：`SnapshotService.cs:61-79,96-104,402-406`；`MainWindow.xaml.cs:96`；`RepoActionService.cs:154-174,198-222`；`HostCommandRouter.cs:338-344`。
- 影响：外部 rebase/merge/切分支/改 remote 后，矩阵仍按旧快照允许动作；COMMIT 可在 IN_PROGRESS/detached 下执行（后果多由 git 兜底），违反 FR-ACT 的 CAS/`E_CONCURRENT_CHANGE` 预期。
- 修复：锁内重算最小状态（`status --porcelain=v2 --branch` + upstream + remote URL）后再过矩阵；或快照 TTL + 动作前强制 refresh；失败返回 `E_CONCURRENT_CHANGE`。

**P1-3 CHKOUT ref 不存在时“假成功”【已复现 E2】**
- 位置：`RepoActionService.cs:406-427`。
- 修复：`rev-parse --verify --quiet refs/heads|refs/tags` 预检，或改 `git switch`；动作后校验 HEAD/分支。

**P1-4 PAT 明文 HTTP 传输【已复现（环境事实）】**
- 位置：`AppConfig.cs:29`；`LocalScanner.cs:623-634`；`GiteaClient.cs:66-69`；实测 origin `http://192.168.50.20:3000`。
- 修复：token 非空强制 https / 显式 `allowInsecure` 开关 + 提示。

**P1-5 `GIT_CONFIG_*` 凭据环境被 git 子进程（hook/helper/submodule fetch）继承【已复现 E10】**
- 位置：`GitCli.cs:88-94`；`RepoActionService.cs:546-552`。
- 修复：header 按 URL 作用域（同 P0-1）；文档说明 hooks 风险；必要时清理子进程可见的敏感环境。

**P1-6 错误摘要只取首行 → PUSH 拒绝/PULL 非 FF 诊断失真、UI 提示过度承诺【已复现 E13】**
- 位置：`RepoActionService.cs:624-640,586-596`；`viewmodel.ts:608-612`。
- 修复：摘要优先选 `error:`/`! [rejected]`/`fatal:`/末行；PUSH 增 `E_PUSH_REJECTED` 类错误码；UI 文案改“结果可能已产生副作用，先刷新确认”。

**P1-7 双矩阵漂移 + Host availability 未计 stagedCount + 契约字段缺失【代码确定/已复现（值对比）】**
- 位置：`RepoActionService.cs:49-66,80-122`；`viewmodel.ts:472-554`（SYNCED-CHKOUT：UI=`c`，Host=`Y`）、`:681-720`；`ComponentDetailService.cs:57-80`；`WorkbenchDto.cs:20-43`（`ComponentRepoDto` 无 `StagedCount`）；`SnapshotDto.cs:58`。
- 影响：`getComponentDetail.actions` 对 0 staged 仍报 COMMIT enabled；两处矩阵长期可能再次分叉（UX 报告中 UNKNOWN 已因此出过 P0）。
- 修复：Host `GetAvailability` 增 `CanCommit`/staged 收口并补齐 DTO 字段；UI 改为消费 Host availability；加矩阵契约测试。

**P1-8 安全边界零测试 + `RealWorkspaceSmokeTests` 非 hermetic（真实 PULL）【代码确定】**
- 位置：`RealWorkspaceSmokeTests.cs:162-170`；`RepoActionGitTests.cs` 全文件；`docs/m2-ops-status.md:50-52`。
- 修复：§7.1 的 15 组测试；smoke 改只读或 env 门控。

---

## 9. P2 清单（13 项）

| ID | 问题 | 位置 | 状态 | 修复建议 |
|---|---|---|---|---|
| P2-1 | 锁字典只增不减、Semaphore 不 Dispose；锁仅进程内（多 Host 实例不互斥） | `RepoActionService.cs:45-46,225-234` | 代码确定 | 引用计数淘汰/`ConditionalWeakTable`；跨进程用命名 Mutex 或 M3 GitWorker |
| P2-2 | 锁与 snapshot `refresh`/扫描不互斥，快照可能采到动作中间态；`index.lock` 被误判 Busy | `HostCommandRouter.cs:217-221,240-247`；`LocalScanner.cs:189` | 理论 | 让 refresh 与 per-repo 锁联动，或动作成功后 Host 主动重建 index |
| P2-3 | `+` 未被拒（force refspec，E8）；PULL 显式 branch 未校验==当前分支（可 FF 到别的远端分支 tip）；branch 可解析为 tag/SHA → 意外 detached | `RepoActionService.cs:862-898,325-350,406-427` | 已复现/理论 | 拒绝 `+`；PULL 要求 branch==当前分支（或未指定）；CHKOUT 校验 ref 类型 |
| P2-4 | 未知 args 静默忽略（拼写错误/`force` 不报错）；FETCH 忽略 branch；`setUpstream` 对非 PUSH 无意义 | `RepoActionService.cs:691-761,303-323` | 代码确定 | 未知字段返回 `E_INVALID_ARGUMENT`；按 action 白名单字段 |
| P2-5 | COMMIT message 未拒绝 NUL/控制字符、多行策略未定义；pre-commit hook 可改变“staged-only”最终内容 | `RepoActionService.cs:750-757,459-461` | 理论 | 拒绝 `\0`/其他控制符；文档说明 hook 参与 |
| P2-6 | 脱敏缺口：`Authorization: Basic …`（HostLog 正则只覆盖 token/bearer）、无冒号 userinfo `https://token@host`、query `?token=` 不覆盖；仅精确替换“配置中的 token”，origin 内嵌旧凭据会泄漏 | `HostLog.cs:60-69,104-110`；`RepoActionService.cs:660-678` | 代码确定 | 正则覆盖任意 scheme 的 Authorization、`https://user@`、`token=`；响应统一二次脱敏 |
| P2-7 | `E_REPO_BUSY` 从不产生（锁等待→`E_TIMEOUT`）；cancel/timeout 共用 `E_TIMEOUT`；UI/Host 均 120s 竞态，写已成功可能显示超时 | `BridgeErrors.cs:19,27-35`；`RepoActionService.cs:555-597`；`HostCommandRouter.cs:30-44,151-162`；`api.ts:264-276` | 代码确定 | 锁等待超时返回 `E_REPO_BUSY`；UI 超时 > Host 超时 + 余量；成功回执加 `opId` 便于幂等重试 |
| P2-8 | unmerged 行只置 `Unmerged=true`，不计 `DirtyCount/StagedCount` → UNMERGED 仓显示“已跟踪 0/已暂存 0” | `LocalScanner.cs:478-482` | 已复现（E5） | unmerged 同时计入 dirty（并定义 staged 语义） |
| P2-9 | EMPTY 行矩阵 PUSH=Y，但 `git push origin HEAD` 必然失败（E7）；矩阵与实现不一致 | `RepoActionService.cs:60,470-501` | 已复现 | EMPTY-PUSH 改 N/C，或先要求有提交 |
| P2-10 | GitCli 无输出上限（status/fetch 全量读入内存）；未清理继承的 `GIT_DIR/GIT_WORK_TREE/GIT_INDEX_FILE/GIT_CONFIG_GLOBAL` 等（E11 证明 `-C` 被 `GIT_DIR` 覆盖）；`JsonElement` args 未 Dispose 的缓冲持有 | `GitCli.cs:74-94,112-139`；`BridgeParams.cs:53-57`；`HostCommandRouter.cs:392-423` | 已复现（E11）/理论 | 输出上限+截断；启动时显式清空/覆盖 GIT_* 敏感变量；args 改强类型或 Clone 后 Dispose |
| P2-11 | 生产构建 `AreDevToolsEnabled=true`；`confirm=true` 仅是 UI 语义，任何页面脚本可直接携带（无服务端票据/CSRF） | `MainWindow.xaml.cs:64`；`RepoActionService.cs:183-196` | 代码确定 | Release 关闭 DevTools；确认态用一次性 nonce（Host 生成、UI 回填）或写入审计 |
| P2-12 | 写路径未复用 `PathPolicy` 容器校验（读路径有 `TryResolveRepoPath`），只检查 `.git` 存在；junction/目录替换 TOCTOU 下可能在非预期目录执行 | `RepoActionService.cs:198-222,680-689`；`PathPolicy.cs:49-68` | 理论 | 注入 `PathPolicy`，锁内 `IsWithin(workspaceRoot, localPath)` + reparse 链复检 |
| P2-13 | 文档与事实不一致：15 仓 vs 实际 8；“token 不进日志/响应”未覆盖外发/子进程；“无 staged 时 COMMIT 禁用”仅 UI；测试口径；smoke 非 hermetic 已在 P1-8 | `docs/m2-ops-status.md:4,15,17,50-52`；`RealWorkspaceSmokeTests.cs:162-170` | 已核对 | 更新证据口径与安全限定；smoke env 门控 |

---

## 10. Top8 必须处理（按优先级）

1. **P0-1 凭据外发边界**：动作前锁内校验 fetch/push URL 与 Gitea base 一致 + fail-closed + `http.<base>.extraHeader` 作用域（并处理 P1-4 明文 http：token 非空强制 https 或显式开关）。**立即修。**
2. **P1-1 ignored 文件静默覆盖**：`checkout --no-overwrite-ignore`；PULL 改 `fetch`+`merge --ff-only --no-overwrite-ignore`。**与 P0 同批修（低成本高收益）。**
3. **P1-3 CHKOUT 假成功**：`rev-parse --verify` 预检/改 `git switch` + 动作后目标校验。**同批修。**
4. **P1-2 门禁 TOCTOU**：锁内重算状态再过矩阵（或快照 TTL + 动作前 refresh），失败返回 `E_CONCURRENT_CHANGE`。M2 收口前修。
5. **P1-5 子进程/hook 继承 PAT**：随 P0-1 的 URL 作用域一并收敛；文档声明 hooks 风险。
6. **P1-6 错误摘要与提示**：摘要选关键行/末行；PUSH 拒绝独立错误码；UI 去掉“未做破坏性改动”的过度承诺。
7. **P1-7 双矩阵/availability 契约**：Host `GetAvailability` 计 stagedCount，补 `ComponentRepoDto.StagedCount`，UI 消费 Host 矩阵，加契约测试。
8. **P1-8 安全负向测试**：§7.1 的 15 组测试（尤其凭据外发、ignored、ref 校验、force、message 边界、超时、token 落盘）；smoke 改只读/env 门控。

---

## 11. 明确判定

| 维度 | 判定 |
|---|---|
| 命令注入 | **通过**（无 shell、全 ArgumentList、branch/tag/message 边界充分；遗留 3 个非注入类边界） |
| FETCH/PULL/CHKOUT/COMMIT/PUSH 功能性 | **通过**（真实 Gitea 已验证；`--ff-only`、staged-only、无 force 三条硬约束正确） |
| 凭据安全 | **不通过**（P0-1 + P1-4 + P1-5） |
| 工作区数据安全 | **不通过**（P1-1 + P1-3） |
| 并发/原子性 | **部分通过**（P1-2 + P2-1/2） |
| 测试覆盖 | **不通过**（P1-8） |
| **是否可提交** | **否**。修完 P0-1、P1-1、P1-3 并补齐 §7.1 第 1/2/3/6/8/10 组测试后可提交；P1-2/P1-4/P1-5/P1-6/P1-7 允许作为 M2 收口项但需在计划中闭环。 |

> 说明：本报告只新增本文件，未修改任何代码、配置或工作区；运行中 Host（CDP 9222）未被操作；`artifacts/real-test/workspace` 未被写入。


