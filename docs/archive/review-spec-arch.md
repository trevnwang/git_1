# §14（M2b 增量）需求评审：架构兼容性 / 安全倒退 / 证据门禁

| 项目 | 内容 |
|---|---|
| 评审对象 | `reqs/specification.md` **第 14 章「M2b 增量与完成定义（v0.4-draft，待评审）」**（L3130–3211）；对照正文 §6/§7/§8/§9/§13、Annex A（AC-OP/AC-REPO/AC-SAFE）与 ADR D-025/D-026 |
| 代码基线 | `HEAD 61c27b6`（`git status` 干净）；架构事实取自 `src/MbdGitTool.Host`、`src/MbdGitTool.Core`、`ui/scripts`、`tests/`（M2b-2b 候选构建） |
| 方法 | ①逐条把 §14.1–§14.9 与现有实现/数据模型对照；②按“误删/误写/泄露 token/证据失真”四类做安全反推；③核对 §14.7 门禁脚本与现网证据；④区分“需求层应补一句 / 留实现设计 / 需 ADR”，给出 P0/P1/P2 与冻结结论 |
| 边界 | **只读评审**：未修改 `reqs/`、源码、配置、真实仓库/用户数据；本报告是本次唯一新增文件 |
| 结论 | **需先修（不可按现状冻结）**：P0=9、P1=14、P2=7；修复 §9 的 8 条“冻结前最小修复集”后可冻结 |

---

## 0. 结论（判决）

> **§14 的方向是正确的**：它补齐了 `docs/m2b-ac-op-gap.md` 中 AC-OP-23（DELETE_PENDING）、AC-OP-25（CREATE 撤销）、AC-OP-31/32（纳入/移出）、AC-REPO-08（Repair/Re-clone）、AC-OP-11…14（baseline_snapshot）与故障注入报告这些已知 GAP，且 TAG 基线 ref、DELETE 强确认/CAS/`.trash` 等已有可复用实现（`BatchOperationService.cs:1023-1033`、`:456-572`）。
>
> **但 §14 目前有三类冻结阻断：**
>
> 1. **架构/数据层不兼容（§14.1/§14.4）**：§14 依赖 `component`/`repo`/`baseline_snapshot`/`job`/`job_item` 表，而**当前 SQLite 只有 `operation_log` 一张表**（`AuditDatabase.cs:78,89-107`，schema=1）；组件是内存快照（`SnapshotService.cs:127-362`），job 落 `jobs.json`（`JobStore.cs:12-16`），`ComponentDto` 无 `isManaged/isDeleted` 字段（`SnapshotDto.cs:20-32`）。§14.1「写 component/repo 行」与 §14.4「DB 行可查询」不是补一句文案，而是**新持久层 + 表 + 迁移 + 写者 + 扫描合并**的架构变更。
> 2. **安全/数据完整性倒退（§14.1/§14.3/§14.6）**：`LOCAL_DELETE` 未显式绑定 DELETE 已有强确认（`confirmText==componentId`）与四阻断；`RECLONE` 先 move 后 clone 且「失败时仓库零改动」自相矛盾（move 后 clone 失败 → 工作区缺失）；`REPAIR` 的 `git init` 本身已写 `.git`，同样不满足“零改动”；`DELETE_PENDING` 重试若信任持久化的 `localPath` 可误删新内容；`MBD_FAULT_INJECT` 允许运行期开启（release 中存在运行期测试开关先例 `MBD_E2E_OPEN_DRY_RUN`，`OpenPathService.cs:22-34`），且 **Host 内 G-WIRE 观测不到 `git.exe` 网络**（clone/fetch/push 全走 `GitCli`），「远端零写/无 force push」证据链不成立。
> 3. **门禁/证据可被空过（§14.7）**：SKIP=exit 0（`gitea-gate.mjs:48-70`）+ DoD 允许“不可达 SKIP”（L3210）+ 现网 `cdp-m2b2-realhost-check.mjs` 存在 `healthyGitea ? 断言 : true` 与无条件 `true`（`:152,153,161,185`）+ 证据目录 `artifacts/` 被 gitignore（`.gitignore:13`），且**当前 `artifacts/m2b2-realhost/result.json` 实际内容就是一次 SKIP**（`skipped:true, url:http://127.0.0.1:1`），与目录里的旧截图混放 → 真实 Gitea 证据可被“跳过 + 空断言 + 陈旧产物”组合绕过。
>
> **不能以“§14 已写清”冻结后再让实现评审补数据设计**：§14 明确“评审通过并冻结后才可开发”（L3132），而 §14.4 又把 `tag_present` 缺 Tag 行写不写交给“实现评审定稿”（L3171）——这是**把冻结级需求决策下推给实现**，必须先回写到需求。

---

## 1. 现有架构事实（兼容性判断依据）

| # | 事实 | 证据 |
|---|---|---|
| A1 | 数据层只有 `operation_log` 一张表（schema=1，`PRAGMA user_version` 迁移）；无 `component`/`repo`/`baseline_snapshot`/`job`/`job_item` 表 | `src/MbdGitTool.Host/Services/AuditDatabase.cs:77-107,601-646` |
| A2 | 组件/仓在每次刷新时由本地扫描 + Gitea 清单**内存装配**，不落库；`ComponentDto` 无 `isManaged/isDeleted` | `SnapshotService.cs:127-362`；`src/MbdGitTool.Core/Contracts/SnapshotDto.cs:20-58` |
| A3 | 远程元数据权威、`repo_id`（Gitea 数字 id）可得 | `GiteaClient.cs:10-16`；SRS §7.1（L246） |
| A4 | job/job_item 持久化为 `%LOCALAPPDATA%\MbdGitTool\jobs\jobs.json`，最多 200 条；item 只有 `BackupRef`，无基线/branch 字段 | `JobStore.cs:12-16,24,176-218` |
| A5 | 批量 kind 白名单 11 种（`CHKOUT` 命名，无 `REPAIR`/`RECLONE`）；强写确认 kind 白名单 | `JobEngine.cs:50-53,59` |
| A6 | 路由无 `bindComponent`/`unbindComponent`/`undoCreate`（仅 getSnapshot/runBatch/getJobs/…/openPath 等 17 个方法） | `src/MbdGitTool.Host/Bridge/HostCommandRouter.cs:328-711` |
| A7 | DELETE 已实现强确认（`args.confirm=true` + `confirmText==componentId`）、矩阵预检、dirty/未推送/stash 阻断、CAS、reparse 校验、`.trash` direct-child move | `BatchOperationService.cs:428-592` |
| A8 | SYNC 备份 ref `refs/backup/sync/<ts>/<branch>/<TYPE>`、CREATE 基线 ref `refs/baseline/create/<ts>/<branch>/<TYPE>` 已写入；但 CREATE 基线只写进 `message`，`backupRef` 传 `null`；`refs/baseline/*` 无列表/无清理 | `BatchOperationService.cs:1023-1040`；`BatchOperationService.cs:988-1042` |
| A9 | TAG 已实现 `fetch --force`→`^{commit}`→`checkout --detach`，但**不写任何 DB** | `BatchOperationService.cs:1051-1140` |
| A10 | 审计 `action`/`result` 实际为自由字符串（无 CHECK），但 §8.1 冻结口径是 `action(CHECK 同 job.kind 集)` | `OperationAuditService.cs:232-291`；SRS §8.1 L401 |
| A11 | 错误码表无 `E_DELETE_PENDING`、无组件纳入/移出类码 | `src/MbdGitTool.Core/Contracts/BridgeErrors.cs:7-59` |
| A12 | 真实 Gitea 测试门禁脚本已存在，SKIP=exit 0；聚合 runner 跳过时不写 `result.json`，仍打印 `ALL_RUNNABLE_SELFCHECKS_PASSED` | `ui/scripts/lib/gitea-gate.mjs:48-70`；`ui/scripts/run-selfchecks.mjs:33-57` |
| A13 | realhost 自检存在条件空断言与无条件 true | `ui/scripts/cdp-m2b2-realhost-check.mjs:152,153,161,185` |
| A14 | 证据产物 `artifacts/` 全部 gitignore；现网 `result.json` 为 SKIP | `.gitignore:13`；`artifacts/m2b2-realhost/result.json` |
| A15 | 运行期测试开关有生产先例（`MBD_E2E_OPEN_DRY_RUN`；UI `__DSH_TEST_BRIDGE__`/`__DSH_FORCE_MOCK__`） | `OpenPathService.cs:22-34`；`ui/src/api.ts:616-640` |

---

## 2. 专题一：§14 数据/状态/API 与现有架构的兼容性

### 2.1 §14.1 `component.is_managed/is_deleted` + `bindComponent`/`unbindComponent`

| 期望（§14.1，L3135–3144） | 现状 | 兼容判定 |
|---|---|---|
| `component.is_managed/is_deleted` 为主状态；扫描合并：纳管组件本地缺失仍显示 | 无 `component` 表、无字段；组件每次全量重算 | **不兼容**：需新表 + 迁移 + `SnapshotAssembler` 合并 + 产品语义决策 |
| `bindComponent` 远程只读校验四仓 → **写 component/repo 行** | §8.1 冻结写者：`component` 由 `STATUS_CALC` 写、`repo` 远程列由 `REMOTE_SYNC`/本地列由 `LOCAL_SCAN` 写；§8.2 要求“每字段唯一写者” | **冲突**：新 API 成为第二写者 → 必须 ADR 修订 §8.1 写者或拆 `component_management` 表 |
| `unbindComponent` LOCAL_DELETE：先走 DELETE 预检/`.trash`，再置 `is_deleted=1`、`repo.is_local=0`；失败不改状态 | DELETE 已具备安全语义（A7），但 unbind 是**组件级 4 仓**操作；逐仓 move 非原子 | **部分可复用/状态机缺失**：需定义逐仓结果、部分失败、状态回写顺序与补偿 |
| `is_managed=0` 默认隐藏、进入“已移出”视图 | 现有自动发现模型无“纳管”概念（所有远程四仓组默认显示）；§7.4 聚合状态 15 态无“已移出”；§11.1 二级入口固定 6 个（AC-UI-13 6/6） | **不兼容**：这是产品模型变更（自动发现 → 显式纳管），且触碰 §7.4/§11 冻结 |
| 重新纳入恢复显示、可重新纳管 | 无定义：重纳管时本地目录已有改动/未推送是否保留？`repo` 行被 DELETE 后如何重建？ | **缺设计** |

**核心问题（P0）**：§14.1 把 `is_managed/is_deleted` 称为“主状态”，与 §7.4 的 15 个组件聚合态、§7.2 的 14 个单仓主状态是**两个维度**；`is_managed=0` 后的聚合态、计数（`CountsDto`）、`severity_rank`、排序与“组件计数 +1/-1”均未定义。默认值/迁移（升级用户已发现的组件是否自动 `is_managed=1`）未定义——**不定义就会升级即隐藏全部组件或自动管理全部组件**，两者都是产品事故。

### 2.2 §14.2 DELETE_PENDING

| 期望（L3146–3152） | 现状 | 兼容判定 |
|---|---|---|
| `Directory.Move` 可恢复失败 → 目录保留、写 `.trash/.pending/<stamp>-<repo>.json`、`job_item.state=FAILED, errorCode=E_DELETE_PENDING` | `DeleteAsync` 捕获 IOException 等统一返回 `E_IO_ERROR`，无 pending 记录（`BatchOperationService.cs:574-587`） | **需扩展**：新增错误码、pending 记录、任务中心“待处理”态 |
| 任务中心“重试失败项”**只重试 pending 项** | `retryFailed` 把原 job 的 `FAILED/INTERRUPTED/CANCELED` **全部**重试（`JobEngine.cs:363-373`） | **语义冲突**：需独立 `retryPending` 或按 item 状态过滤；否则 DELETE 的普通失败（dirty/AHEAD）也会被当成 pending 重试 |
| 审计 `action=DELETE`，`result=DELETE_PENDING` | 审计 `result` 直接取 item 终态（`JobEngine.cs:1208-1223`），新 result 值会影响 UI 过滤/导出 | **需定义**：job_item 状态仍 FAILED，但 audit result 特例 = DELETE_PENDING，需显式映射 |
| AC-OP-23 要求“DB 有标记” | job 在 `jobs.json`，不在 DB | **需求/现状不一致**：要么迁移 job 到 DB（大改），要么把 AC 的“DB”改为“作业记录”并走 ADR |
| `.trash/.pending` 与保留清理 | 清理只遍历 14 位时间戳目录（`TrashStampPattern`），`.pending` 不会被删除也不会过期 | **兼容但缺 GC**：陈旧 pending 永久残留 |
| `localPath` 写入记录并用于重试 | 记录含绝对路径；重试若不重扫而按 JSON 路径移动，可能把已被外部改写/替换的目录移走 | **安全风险（P0）**：必须重扫 + 重新 canonical/reparse/IsWithin + 重跑 DELETE 四阻断 + 幂等 |

### 2.3 §14.3 REPAIR / RECLONE（job kind）

| 期望（L3154–3163） | 现状 | 兼容判定 |
|---|---|---|
| 新增 job kind `REPAIR`/`RECLONE`；`operation_log.action=REPAIR/RECLONE` | `JobEngine` kind 白名单无二者（`JobEngine.cs:50-53`）；§8.1 `job.kind CHECK` 只有 `REPAIR`、**无 `RECLONE`**（L399） | **需 ADR**：扩白名单 + 改 §8.1 CHECK；且代码 kind 用 `CHKOUT`、SRS 用 `CHECKOUT`，需一并裁定 |
| 任务中心/抽屉对 `INVALID/MISSING/BINDING_ERROR` 提供 Repair 与 Re-clone | §7.6：INVALID=⚠️ Repair/Re-clone、MISSING=Clone、UNKNOWN=重试扫描；BINDING_ERROR 是组件态 | **条款自相矛盾**：入口列表与其下方“判定”表不一致（MISSING→CLONE） |
| REPAIR：`git init`→origin→fetch→安全检出；“不覆盖已有文件，冲突则失败零改动” | 无实现；`git init` 本身先写 `.git` | **“零改动”不可实现**：需临时目录建仓后原子移动，或失败时仅回滚本 job 新建的 `.git` |
| RECLONE：先 move 原目录到 `.trash/<stamp>/<repoName>`，再 clone；“失败时仓库零改动” | 无实现 | **“零改动”不可实现**：move 后 clone 失败 → 工作区缺仓、原目录在 trash；必须定义回滚（move back）与远程可达前置 |
| 失败零改动/审计含 pre/post HEAD | journal 只对白名单 kind 自动写（`JobEngine.cs:770-779`）；`REPAIR/RECLONE` 入白名单后才可复用 | 兼容（前提：入白名单） |
| 凭据与 origin 设置 | 现有 clone/fetch 走 `TryBuildCloneCredentialEnvironment` fail-closed（`BatchOperationService.cs:355-362`），AC-SAFE-14 覆盖 | **§14 未要求复用**：新网络路径若不显式要求将绕过 AC-SAFE-14 → 安全倒退 |

### 2.4 §14.4 Tag 与 `baseline_snapshot`

| 期望（L3165–3174） | 现状 | 兼容判定 |
|---|---|---|
| 每仓写 `baseline_snapshot(component_id, tag, repo_type)`（commit_sha/tag_object_sha/tag_present/captured_at） | 无表；TAG 只做检出+message，不落库（A9） | **不兼容**：新表 + JobRunner 写路径 + 失败补偿 |
| `tag_present` = 是否检出；缺 Tag 仓写 0 **或按设计不写行**（“实现评审定稿”） | AC-OP-12 已冻结“`baseline_snapshot` 写 **4 行**，SIL 行 `tag_present=0`、`commit_sha` 为空”（L2287） | **P0 矛盾**：§14.4 允许 skipped 不写行，与冻结 AC 冲突；且 §14.4 同段又写“失败/跳过不写” |
| 历史行/主键 | §8.1 PK=`(component_id, tag, repo_type)`；AC-OP-13 ④ 要求 tag 移动“记录新 commit 且**不丢历史行**”（旧行保留或新增） | **P0 矛盾**：单 PK 不可能同时保留旧行；需 upsert（覆盖）或版本列，二选一并回写 AC |
| annotated/轻量 tag 区分 | TAG 已用 `^{commit}` 解引用，但未取 tag 对象 SHA，也无 `cat-file -t` 判定轻量 | **需补实现设计**：轻量 tag 的 `tag_object_sha=NULL` 的判定规则 |
| DB 行可查询 | 无 DB 写入路径 | 同上 |

### 2.5 §14.5 CREATE 撤销

| 期望（L3176–3181） | 现状 | 兼容判定 |
|---|---|---|
| `undoCreate { jobId }`；以 `refs/baseline/create/...` **或 job_item 基线**为依据 | 基线 ref 已写入，但**只出现在 message**，`backupRef=null`（`BatchOperationService.cs:1023-1040`） | **缺数据**：`StoredJobItem` 无 branch/baseline 字段（`JobStore.cs:191-203`） |
| 逐仓校验分支仍指向基线、未被检出、无新提交 → `git branch -D` | 无 API/路由；基线 ref 无列表/清理 | **需设计**：branch 名从哪取、基线 ref 从哪取 |
| job 可撤销 | job 仅保留最近 200 条（`JobStore.cs:24`）；`jobId` 淘汰后不可撤销 | **缺定义**：撤销可用窗口 = job 保留期，须写清 |
| 写审计 `CREATE_UNDO`；失败零改动 | 撤销是**写操作**（删分支），但既非 job、未要求 journal、未要求确认 | **违反 FR-SAFE-04**（写操作全部走写前 journal）：需定为 job 或补 journal |

### 2.6 API/枚举/审计契约（跨条款）

| 项 | 现状 | 判定 |
|---|---|---|
| 新审计 action `COMPONENT_BIND`/`COMPONENT_UNBIND`/`CREATE_UNDO` | 实现不校验（A10），但 §8.1 冻结 `action CHECK 同 job.kind 集` | **冻结冲突**：需 ADR 扩 enum（或明确“审计 action 不属 CHECK 集”） |
| `E_DELETE_PENDING` | BridgeErrors 无（A11） | **需补**：Host 错误码 + 附录 D 映射（附录 D 仍是“示例表”，M0 补全时一并） |
| 组件纳入/移出的 API 契约 | §14.1 只给方法名与 `{componentId, displayName?}`/`{componentId, mode}` | **缺 DTO/响应/超时/失败码/审计字段**：属实现设计，可后补，但需声明 |
| 快照/DTO 契约 | `ComponentDto` 无 `isManaged/isDeleted`；新增可选字段可兼容（现有 record 可选参数机制） | 兼容（实现设计） |
| §14.5 撤销与 §14.2 pending 的 UI 入口 | 未定义 testid/入口；任务中心现有结构可扩展 | 实现设计 |

### 2.7 兼容性小结

| 条款 | 兼容性 | 需要什么 |
|---|---|---|
| §14.1 is_managed/is_deleted/bind/unbind | **不兼容** | 新表/迁移/写者 ADR/产品默认语义/UI 视图 |
| §14.2 DELETE_PENDING | **需扩展** | 新错误码/记录/重试语义/审计映射；DB 口径决策 |
| §14.3 REPAIR/RECLONE | **需扩展 + ADR** | 扩枚举、改 §8.1、补状态机/回滚/凭据复用 |
| §14.4 baseline_snapshot | **不兼容 + 需求矛盾** | 新表/写路径/主键语义/与 AC 一致化 |
| §14.5 CREATE undo | **部分基础存在、缺数据与流程** | job_item 基线字段、API/UI、journal/确认、保留期 |
| §14.6 故障注入/G-WIRE | **不存在** | 构建期开关、外部代理、注入点枚举、报告 |
| §14.7 测试门禁 | **已存在但不足** | evidence manifest、SKIP≠PASS、加固 probe |

---

## 3. 专题二：安全倒退评估（误删 / 误写 / token）

### 3.1 组件移出 / 本地删除 — **有倒退风险（P0）**

1. **强确认缺失风险**：DELETE 本身要求 `args.confirm=true` **且** `confirmText==componentId`（`BatchOperationService.cs:456-463`）；§14.1 的 LOCAL_DELETE 只写“先走 DELETE 预检/`.trash`”，未要求复用强确认。若实现成“移出弹窗一次确认 → 4 仓 move”，相对 AC-OP-21（四项阻断 + 输入组件 ID）就是**安全降级**。
2. **4 仓部分失败**：§14.1 “失败不改状态”不可实现——逐仓 move 非原子；2 成功/2 失败时既不能 `is_deleted=1`（2 仓还在），也不能保持 `is_managed=1`（2 仓已移走）。需定义 `PARTIAL`/逐仓结果/是否回滚，否则 UI 会说谎（“已移出”但目录仍占位，或“仍在管理”但仓已缺）。
3. **可删对象边界**：`INVALID/UNKNOWN` 仓按 §7.6 Delete=⚠️ 允许（L356-357），但现有 `DeleteMatrixAllowed` 拒绝 INVALID（`BatchOperationService.cs:478-485`）；unbind 会“移出失败”。若为 unbind 放宽矩阵，必须补 INVALID 目录（可能含用户文件）的 dry-run/确认/`.trash` 语义，不能直接递归删除。
4. **STOP_MANAGE 的隐蔽性**：`is_managed=0` 只隐藏不删除；若 UI 未明确“数据仍在本地”，用户可能以为已删；且“已移出”入口不在 §11.1 固定 6 入口内（AC-UI-13 6/6），需明确入口位置。
5. **输入校验**：`componentId` 来自用户输入，必须复用 §6.1 名字解析 + 单路径段/大小写冲突/保留名校验；否则伪造 ID 可影响本地路径推导与 pending 文件名（见 P2-3）。

### 3.2 Re-clone 备份 — **有数据可用性风险（P0）**

1. **“失败零改动”不成立**：§14.3 先 move 后 clone；move 成功、clone 失败（网络/权限/磁盘）→ 工作区缺仓、原目录只在 `.trash`。必须定义：远程可达预检（`ls-remote`）→ 目标/磁盘/长路径预检 → move → clone 失败则**自动 move back**；否则用户看到的是“仓库消失”。
2. **脏仓直接搬迁**：RECLONE 未要求 clean/未推送/untracked/stash 预检；未提交工作会被搬进 `.trash`（30 天可恢复但用户不知），与 AC-OP-21/FR-SAFE-01「将丢失什么」原则不一致。必须先 dry-run/强确认（§14.3 有 typed confirm，但没列丢失清单）。
3. **与 DELETE 备份混淆**：`.trash/<stamp>/<repoName>` 与 DELETE/RESTORE 共用；RESTORE 按 repoName 取“最近 stamp”（`BatchOperationService.cs:939-949`）可能恢复成 RECLONE 备份而非用户预期的删除前版本。需记录备份类型并让 RESTORE 明确来源。
4. **stamp 碰撞**：现 DELETE 在目标存在时追加 GUID（`:568-572`），§14.3 未要求；RECLONE 应复用同一安全落点/唯一化策略。

### 3.3 DELETE_PENDING 重试 — **有误删风险（P0）**

1. **不得信任持久化 `localPath`**：pending 文件可被本地编辑/陈旧；重试必须重扫、重 canonical、重 reparse/IsWithin 校验，并重跑 DELETE 四阻断 + CAS（HEAD 未变），否则目录被外部替换/提交新内容后重试会删掉新工作。
2. **通用 `retryFailed` 语义冲突**（见 2.2）：普通失败与 pending 混在一起重试；需独立入口/状态过滤。
3. **幂等**：若上次 move 成功后崩溃、pending 未删（or 目录已不存在），重试应识别“已完成”并清理 pending + 审计 OK，而不是报错或重复移走其他目录。
4. **GC/取消**：§14.2 提“用户可显式取消 pending”，但未给 API/审计值/UI；陈旧 pending 无过期策略。

### 3.4 故障注入 / G-WIRE — **有生产误写与证据失真风险（P0）**

1. **运行期开关**：§14.6 写“仅测试构建/**或**环境变量启用”，措辞允许“release + 环境变量”。同仓已有运行期测试开关先例（A15），所以这不是理论风险；注入点包含“磁盘写失败/Directory.Move 占用/进程崩溃（写前/写后）”，一旦在生产构建可开，就是**数据损坏后门**。必须改为“**编译期测试构建 ∧ 环境变量**”，release 构建不包含代码路径。
2. **注入点必须白名单 + schema 校验**：JSON 不得携带任意路径/命令；崩溃注入不得经 bridge/UI/文件可触发；注入产生的异常/日志不得带 token。
3. **G-WIRE 位置错误**：git 的网络操作（clone/fetch/pull/push）由 `GitCli` 启动 `git.exe` 完成（`BatchOperationService.cs:391-406`、`RepoActionService`），Host 内 HTTP 记录器**看不到**这些请求。因此 §14.6 用 G-WIRE 断言“远端零写/无 force push”以及 AC-SAFE-04/13 的“G-WIRE 记录方法数=0”**当前不可成立**。需定义 G-WIRE 为**外部 HTTP 代理**（Gitea base 指向 recording proxy）或 Gitea 服务端审计日志 + `GIT_TRACE2_EVENT` argv 三件套。
4. **token 泄露面**：G-WIRE 记录 headers；即使“脱敏”也不能保证（Authorization 有 Basic base64 变体）。应“**不记录 header 值**”（只记名称/允许列表），捕获时即丢弃，落盘仅测试目录；报告/审计/journal 一律不含 header 值。此外 bind 的远程校验错误消息不得回显 git stdout/stderr（复用 `HostLog.Redact`），REPAIR/RECLONE 设置 origin 必须剥离 userinfo。

### 3.5 token 泄露面清单（§14 新增路径）

| 新路径 | 泄露方式 | 要求 |
|---|---|---|
| bindComponent 远程校验失败 | 回显 git stderr/URL（含 userinfo） | 只返回分类码 + 脱敏文案；不返回原始 stderr |
| RECLONE/REPAIR 设置 origin | 复制旧 origin 的用户名/token | 重建 URL 且禁止 userinfo；审计 pre/post 不含 URL 凭据 |
| DELETE_PENDING 记录 | `localPath`（非 token，但泄露用户目录结构） | 允许；审计导出可按现有规则 |
| G-WIRE | headers 含 `Authorization` | 不记录值；只测试构建；落盘受限 |
| 故障注入报告 | 注入配置/抓包截图 | 报告入库前脱敏；哨兵扫描 |

---

## 4. 专题三：§14.7 测试门禁与真实证据强度

### 4.1 现状核对

| 项 | 现状 | 证据 |
|---|---|---|
| 探测 `/api/v1/version`，不可达 SKIP(exit 0) | 已实现，`MBD_GITEA_URL/MBD_GITEA_PROBE_URL` 覆盖 | `gitea-gate.mjs:26-70`；`docs/testing-gitea-gate.md:1-36` |
| 不可达写 `result.json` | realhost 脚本传 `outDir` 会写；**聚合 runner 不写**，只打印 JSON | `cdp-m2b2-realhost-check.mjs:20-23`；`run-selfchecks.mjs:33-51` |
| 聚合通过文案 | 跳过真实 Gitea 仍打印 `ALL_RUNNABLE_SELFCHECKS_PASSED`（exit 0） | `run-selfchecks.mjs:53-57` |
| 真实 Host 证据 | 现网 `artifacts/m2b2-realhost/result.json` = SKIP（`url=http://127.0.0.1:1`），同目录存有旧截图 | `artifacts/m2b2-realhost/result.json` |
| 证据入库 | `artifacts/` 被 gitignore（历史 Gate 已因此判 P1-8） | `.gitignore:13`；`docs/review-m2b2-ux.md:221` |
| 断言强度 | `healthyGitea ? 断言 : true`、`check('real.empty-skip-no-fixture', true, …)`、`audit-has-terminal-result` 只要求任意历史行 | `cdp-m2b2-realhost-check.mjs:152,153,161,185,264` |

### 4.2 SKIP 可被滥用的路径（证据强度分析）

- **S1（DoD 空过）**：§14.9-4 明确“不可达按 14.7 SKIP 并记录”（L3210），而 §14.9-1 要求 AC 全 COVERED。只要一次 SKIP 被记为“已记录”，DoD 就可在**零真实 Gitea 证据**下成立；AC-SAFE-04/13、AC-OP-31/32 的真实远端断言随之悬空。
- **S2（runner 语义）**：SKIP 与 PASS 在 exit code/汇总文案上不可区分（`ALL_RUNNABLE_SELFCHECKS_PASSED`）；结果物没有 `{ran, skipped, failed}` 机器可读三态，Gate 报告无法证明“真实运行过”。
- **S3（空断言）**：探测可达但 Host 快照未真正用 Gitea（token 缺失/权限不足/org 错）时，`healthyGitea=false` 让多条 real 断言直接 `true` → 脚本“全绿”但未验证任何真实 Gitea 行为。
- **S4（探测口径）**：`/api/v1/version` 是匿名端点，只能证明“有 HTTP 服务”，不证明 token/org/权限/测试夹具可用；且 `MBD_GITEA_PROBE_URL` 与实际 `MBD_GITEA_URL` 解耦，可“探测可达、测试不可用”。
- **S5（陈旧/混合证据）**：现网证据目录同时有 skip `result.json` 与旧截图；artifacts gitignored → 无 commit 关联、无哈希、无法判断截图归属哪个构建/哪次运行。
- **S6（与冻结测试策略冲突）**：§13.2 已要求“Windows 可跑的 Gitea 单二进制（`gitea.exe web` + 内置 SQLite）做 E2E”（L1180）。既然存在 hermetic Gitea 夹具，§14.7 把 SKIP 作为常规路径就失去了正当性——除“必须用共享测试实例”的少数用例，其余应在本地夹具上跑。

### 4.3 冻结前必须补的规则

1. **SKIP ≠ PASS**：`result.json` 增加 `status ∈ {PASSED, FAILED, SKIPPED}`、`commit`、`buildConfiguration`、`startedAt/finishedAt`、`probe{url,httpStatus,version}`、`tokenScope`、`fixtures`、`assertions{passed,failed,skipped}`。
2. **DoD 绑定证据清单**：M2b 出口要求提交/归档一份 `evidence/m2b-manifest.json`（git 内或带 SHA256 登记），逐 AC 标注 `COVERED(commit, runId)/NOT_VERIFIED(原因)`；**JSON 中任何真实 Gitea AC 为 NOT_VERIFIED 时不得宣布 M2b/M2 完成**。
3. **禁止空断言**：realhost 脚本禁止 `cond ? assert : true`；未满足前置时输出显式 `SKIP` 状态（不是通过），Gate 报告单列 `skippedChecks`。
4. **探测加固**：可达条件 = `/api/v1/version` 200 **且** 带 token `GET /api/v1/orgs/{org}/repos?limit=1` 成功；探测 URL 与测试 URL 必须一致（或显式声明两者关系）；token 缺失/权限不足 → FAILED（不是 SKIP），因为这是配置错误。
5. **证据入库**：`result.json`/关键截图/日志必须能从提交或 `evidence/` 目录复现；至少把 `result.json` 的哈希与运行命令写进 Gate 报告。
6. **本地夹具兜底**：按 §13.2 提供 `gitea.exe` 夹具启动脚本；§14.7 改为“优先本地夹具；仅需共享实例的用例才允许 SKIP”，与 §13.2 一致。

---

## 5. 专题四：需求层可写但缺架构/数据设计的条款

> 分类：**★ 需求层补一句即可冻结**；**▲ 留实现设计（不阻塞冻结，但需在 §14 声明“以下为实现设计”）**；**■ 需 ADR/改 §8.1**。

| # | 条款 | 缺什么 | 处置 | 阻塞 |
|---|---|---|---|---|
| 1 | §14.1 | 现有/升级安装的 `is_managed` 默认值与迁移；自动发现的四仓组件是否自动纳管 | ★ + ■ | P0 |
| 2 | §14.1 | `component`/`repo` 表与唯一写者（§8.1/§8.2） | ■ | P0 |
| 3 | §14.1 | unbind 的 API 响应、`STOP_MANAGE→LOCAL_DELETE` 状态机、4 仓部分失败/回滚、与 DELETE job 的关系 | ★ + ▲ | P0 |
| 4 | §14.1 | “已移出”视图入口（与 §11.1 六入口/AC-UI-13 冲突） | ★ + ▲ | P1 |
| 5 | §14.2 | pending 记录 schema、DB vs `.trash/.pending` 文件（AC-OP-23 写“DB 有标记”）、GC、取消 API、审计 result 映射 | ★ + ▲ | P0 |
| 6 | §14.3 | `RECLONE` 加入 §8.1 `job.kind CHECK`；kind 命名（CHECKOUT/CHKOUT） | ■ | P0 |
| 7 | §14.3 | REPAIR 的零改动实现约束（临时目录/回滚 `.git`）、目标目录非空/INVALID 的处理 | ★ + ▲ | P0 |
| 8 | §14.3 | RECLONE 的远程可达前置、clean/未推送/untracked/stash 预检、clone 失败回滚、trash 唯一化 | ★ + ▲ | P0 |
| 9 | §14.4 | 缺 Tag 仓写不写行、历史行/主键/upsert（与 AC-OP-12/13 一致化） | ★ | P0 |
| 10 | §14.4 | 写路径（JobRunner→DB）与 job 失败/取消时的写序、`tag_object_sha` 轻量判定 | ▲ | P1 |
| 11 | §14.5 | `job_item` 基线字段（当前只有 `backup_ref`/message）、200 job 保留期、undo API/UI、journal/确认、refs/baseline 列表与清理 | ★ + ▲ | P0 |
| 12 | §14.6 | 编译期开关约束、注入点枚举、AC-SAFE-01…13 逐条映射（05/10/11 非注入类，应标 N/A） | ★ + ▲ | P0 |
| 13 | §14.6 | G-WIRE 位置（外部代理/Gitea 日志/argv 三件套）、不记录 header 值 | ★ | P0 |
| 14 | §14.7 | `result.json` schema、evidence manifest、SKIP 与 DoD 的判定规则 | ★ | P0 |
| 15 | §14.7 | probe 认证/org/夹具校验、`PROBE_URL` 与 `URL` 关系 | ★ | P1 |
| 16 | §14.1/§8.1 | 新审计 action 与 `E_DELETE_PENDING` 进入冻结枚举/附录 D | ■ | P1 |
| 17 | §14.9 | DoD 只引用旧 AC：§14 新增行为（REPAIR/RECLONE 回滚、undo 强确认、pending 取消、已移出视图、G-WIRE 位置）无对应可判定断言 | ★ | P1 |
| 18 | §14 全文 | `LOCAL_DELETE` 四仓部分失败、RECLONE 回滚、undo 部分仓 SKIPPED、pending 幂等、bind 错误码 | ▲ | P1 |
| 19 | §14.9 | §14 自述“M2b 完成定义”，但未覆盖既有 M2b 缺口（如 AC-OP-19 GAP、AC-OP-02/04/09/24 PARTIAL，见 `docs/m2b-ac-op-gap.md:9-42`） | ★ | P1 |

**一句话判据**：**凡是会改变冻结语义（表/枚举/AC 断言/DoD）的，必须是 ★ 或 ■ 在冻结前回写**；凡是不改变可判定性的（UI testid、pending 文件字段名、报告模板、错误文案），可以留 ▲ 实现设计。

---

## 6. 内部矛盾与冻结冲突（§7/§8/Annex A）

| # | §14 条款 | 冲突对象 | 内容 |
|---|---|---|---|
| C1 | §14.4 “缺 Tag 仓写 0 **或按设计不写行**”/“失败/跳过不写” | AC-OP-12 ③（L2287） | 冻结 AC 要求写 4 行、SIL 行 `tag_present=0`；§14 允许不写 → 验收不可能同时成立 |
| C2 | §14.4 `baseline_snapshot` 主键 | AC-OP-13 ④（L2295） | 单 PK 不可能“记录新 commit 且不丢历史行” |
| C3 | §14.1 “`is_managed/is_deleted` 为主状态” | §7.2 14 主状态（L266-283）/§7.4 15 聚合态（L307-323） | “主状态”术语与现有状态模型冲突；`is_managed=0` 后聚合态/计数/排序未定义 |
| C4 | §14.3 入口“INVALID、MISSING、BINDING_ERROR 提供 Repair/Re-clone” | §7.6（L356-358）矩阵 | MISSING=Clone、UNKNOWN=重试扫描；BINDING_ERROR 是组件态 |
| C5 | §14.1 LOCAL_DELETE“先走 DELETE 预检” | §7.6 INVALID Delete=⚠️ + 实现拒绝 INVALID（`BatchOperationService.cs:478-485`） | 是否允许删 INVALID/UNKNOWN 目录、怎么防误删用户文件未定义 |
| C6 | §14.1/§14.5 新增审计 action | §8.1 `action(CHECK 同 job.kind 集)`（L401）/§8.2 单写者（L415） | 新 action 不在冻结集；bind 写 `component/repo` 违反唯一写者 |
| C7 | §14.3 RECLONE job kind | §8.1 `job.kind CHECK`（L399） | 无 RECLONE；REPAIR 有但当前白名单未启用 |
| C8 | §14.7 SKIP（L3193-3196） | §13.2 Gitea fixture E2E（L1180） | 已有 hermetic Gitea 夹具要求，SKIP 作为常规路径与冻结测试策略冲突 |
| C9 | §14.9-4 “可达时全绿（不可达 SKIP 并记录）” | §14.9-1 “AC 全 COVERED” | SKIP 未被排除在“完成”之外 → DoD 可空过 |
| C10 | §14 章号 | 正文已有“14. 风险登记”（L1230） | 文档出现两个 §14，引用歧义（评审/实现/AC 追溯均受影响） |

---

## 7. P0 / P1 / P2

### P0（冻结前必须修；否则不得冻结）

| ID | 条款 | 问题 | 冻结前要求 |
|---|---|---|---|
| P0-1 | §14.1 | 组件管理无表/无写者/无默认迁移；“主状态”与 §7.4 冲突 | ★默认语义 + ■表与写者（或拆 `component_management`）+ 迁移方案；声明“已移出”是视图过滤不是聚合态 |
| P0-2 | §14.1 | LOCAL_DELETE 未绑定 DELETE 强确认（`confirm=true`+typed `componentId`）与四阻断；4 仓部分失败“失败不改状态”不可实现 | 明确 LOCAL_DELETE 复用 DELETE job/语义；定义逐仓结果/`PARTIAL`/回滚或补偿；INVALID/UNKNOWN 删除边界 |
| P0-3 | §14.3 | REPAIR/RECLONE “失败零改动”不可实现（`git init` 已写、move 后 clone 失败无回滚） | 补 REPAIR 临时/回滚约束；RECLONE 远程可达前置 + clean 预检 + clone 失败自动 move back |
| P0-4 | §14.4 | baseline 与 AC-OP-12/13 正面矛盾（skipped 行、历史行/主键） | 二选一并在 §14 与 AC 同步（建议：skipped 也写 `tag_present=0` 行；主键加 `captured_at` 或 upsert 后修订 AC-OP-13） |
| P0-5 | §14.5 | undo 依据未持久化（`backupRef=null`、message 文本）、200 job 上限、撤销绕过 journal/确认 | 增 `job_item.baseline_ref`（或等价）与清楚保留期；撤销定为 job 或补 journal；列出部分失败契约 |
| P0-6 | §14.2 | pending 重试信任 `localPath`；`retryFailed` 语义冲突；AC-OP-23 “DB 有标记”与 jobs.json 现状矛盾 | 定义 pending 落点（DB/文件）、幂等重试（重扫+CAS+四阻断）、独立重试入口/状态过滤、GC |
| P0-7 | §14.6 | 运行期 `MBD_FAULT_INJECT` 可在生产构建开启；G-WIRE 在 Host 内看不到 git.exe 网络；“远端零写”证据不成立 | 编译期测试构建 ∧ 环境变量；G-WIRE 改为外部代理/服务端日志 + argv；header 值不记录 |
| P0-8 | §14.7 | SKIP=exit 0 + DoD 允许 SKIP + realhost 空断言 + artifacts gitignore → 真实证据可空过 | SKIP≠PASS；evidence manifest；禁止 `? true` 断言；probe 加认证/org；按 §13.2 本地 Gitea 夹具兜底 |
| P0-9 | §14.1/§14.3/§14.5 | 新枚举/错误码/审计 action 与 §8.1、附录 D 冻结冲突 | 出 ADR 修订 §8.1（RECLONE/action）与附录 D（`E_DELETE_PENDING` 等）；明确 job.kind 与 action 的枚举边界 |

### P1（冻结前应修；最迟在对应实现评审前闭环）

| ID | 条款 | 问题 |
|---|---|---|
| P1-1 | §14.1 | `bindComponent` 输入校验（§6.1 解析/单路径段/大小写冲突/未绑定冲突）与错误码未定义 |
| P1-2 | §14.1 | STOP_MANAGE 可逆性、“已移出”可见性、重纳管不得覆盖本地改动 |
| P1-3 | §14.1 | INVALID/UNKNOWN 仓 unbind 被现实现拒绝（矩阵 ⚠️ 与实现不一致） |
| P1-4 | §14.2 | pending 原子写/GC/取消 API/审计 result 新值与 UI 过滤/导出 |
| P1-5 | §14.3 | 入口列表与 §7.6 判定矛盾（MISSING→CLONE 等） |
| P1-6 | §14.3 | 新网络路径未要求复用 AC-SAFE-14 凭据作用域/`TryBuildCloneCredentialEnvironment`；origin 去 userinfo |
| P1-7 | §14.3 | RECLONE 备份与 DELETE 备份在 `.trash` 无法区分；RESTORE 可能恢复错对象；stamp 唯一化 |
| P1-8 | §14.4 | `tag_object_sha` 轻量/annotated 判定；写路径失败补偿（检出成功但 DB 写失败） |
| P1-9 | §14.5 | undo 部分仓 SKIPPED 契约、typed confirm、`refs/baseline/*` 列表/清理/保留 |
| P1-10 | §14.6 | 注入点↔AC-SAFE-01…13 映射（05/10/11 非注入类应标 N/A）；报告需可复跑（commit/命令/环境） |
| P1-11 | §14.7 | probe 需带 token/org/夹具校验；`result.json` schema、`PROBE_URL` 与 `URL` 关系；证据入库 |
| P1-12 | §14.9 | DoD 只引用旧 AC，§14 新行为无断言；且 §14 未闭合既有 M2b GAP（AC-OP-19 等） |
| P1-13 | §14 全文 | 实现已先行（TAG 检出、CREATE 基线 ref 已交付），§14 未记录“现状即口径/偏差”，存在“需求追认代码”的治理风险 |
| P1-14 | §14.1 | `is_managed/is_deleted` “主状态”与 §7.4 15 态、§11.1 六入口/AC-UI-13 冲突 |

### P2（实现设计或文档治理）

| ID | 条款 | 问题 |
|---|---|---|
| P2-1 | §14.3 | job kind 术语：SRS `CHECKOUT` vs 代码 `CHKOUT`，扩枚举时一并统一 |
| P2-2 | §14.4/§14.5 | `refs/baseline/*` 与 pending 记录无保留期清理（与 `backup.retentionDays` 关系） |
| P2-3 | §14.2 | pending 文件名 `<stamp>-<repo>.json` 的 repoName 编码/路径注入防护 |
| P2-4 | §14.2 | 审计 `result=DELETE_PENDING` 对 UI 过滤/CSV 导出的兼容 |
| P2-5 | §14.6 | 报告模板/路径（`docs/m2b-fault-injection-report.md`）与复跑脚本 |
| P2-6 | §14 章号 | 与正文“14. 风险登记”（L1230）重号，需改名/改号并更新交叉引用 |
| P2-7 | §14.1 | 纳入校验的 Gitea 请求量（4 仓/次）与 §12.4 配额/失败的降级策略 |

---

## 8. 冻结结论与最小修复集

> **结论：需先修，暂不可冻结。** §14 的**范围/优先级判断是对的**（与 `docs/m2b-ac-op-gap.md` 的 GAP 清单一致，P0/P1 与既有 M2b 阻断项对得上），但当前文本把三类“冻结级决策”留成了实现细节或自相矛盾，冻结后会造成：①返工（组件管理层）；②验收不可能通过（baseline 与 AC-OP-12/13）；③安全语义降级（LOCAL_DELETE/RECLONE/故障注入）；④“完成”可被无证据宣布（SKIP）。

**冻结前最小修复集（8 条，缺一不可）**：

1. **§14.4**：与 AC-OP-12/13 一致化（skipped 写行 + `tag_present=0`；历史行/主键二选一），删除“按实现评审定稿”。
2. **§14.1**：补 `is_managed` 默认与迁移、`component/repo` 表与写者 ADR；明确“已移出”是视图过滤而非聚合态。
3. **§14.1**：LOCAL_DELETE 显式复用 DELETE 强确认 + 四阻断 + CAS，并定义 4 仓部分失败状态/回滚；INVALID/UNKNOWN 删除边界。
4. **§14.3**：REPAIR/RECLONE 的零改动/回滚/前置条件 + `RECLONE` 进 §8.1 枚举。
5. **§14.5**：`job_item` 基线字段/保留期/undo 走 job 或 journal + 强确认；partial SKIPPED 契约。
6. **§14.2**：pending 落点（DB/文件）、幂等重试（重扫+CAS+四阻断）、独立重试入口、GC；统一 AC-OP-23 的“DB”口径。
7. **§14.6**：编译期测试构建门禁 + G-WIRE 外部位置/不记录 header 值 + 注入点↔AC 映射表。
8. **§14.7/§14.9**：SKIP≠PASS、evidence manifest、禁止空断言、probe 加认证/org；DoD 绑定“NOT_VERIFIED 不得宣布完成”。

**可留实现设计（不阻塞冻结，但 §14 需加一句“以下为实现设计，不改变验收语义”）**：pending 文件字段名、UI testid、undo 部分失败报告格式、错误文案、G-WIRE 代理具体实现、baseline 写事务细节、REPAIR 临时目录布局。

---

## 9. 证据索引

**规格**
- §14：`reqs/specification.md:3130-3211`（§14.1 3135-3144、§14.2 3146-3152、§14.3 3154-3163、§14.4 3165-3174、§14.5 3176-3181、§14.6 3183-3189、§14.7 3191-3197、§14.9 3203-3211）
- §7.4 聚合态：`:303-330`；§7.6 矩阵：`:350-379`；§8.1 表清单/写者：`:389-410`；§8.2 约束：`:412-419`
- AC-OP-12：`:2282-2288`；AC-OP-13：`:2290-2296`；AC-OP-14：`:2298-2304`；AC-OP-23：`:2370-2376`；AC-OP-25：`:2386-2392`；AC-OP-31/32：`:2434-2448`；AC-REPO-08：`:2508-2514`；AC-SAFE-04：`:2566-2572`；AC-SAFE-13：`:2638-2644`
- §13.2 测试策略（Gitea fixture）：`:1178-1187`；§14. 风险登记（重号）：`:1230-1244`

**代码**
- `src/MbdGitTool.Host/Services/AuditDatabase.cs:77-107,601-646`（唯一表 + schema=1 + 迁移）
- `src/MbdGitTool.Host/Services/SnapshotService.cs:127-362`（内存装配）；`src/MbdGitTool.Core/Contracts/SnapshotDto.cs:20-58`（无管理字段）
- `src/MbdGitTool.Host/Services/JobStore.cs:12-16,24,176-218`（jobs.json/200/无基线字段）
- `src/MbdGitTool.Host/Services/JobEngine.cs:50-53,59,363-373,770-779,975,1208-1223`（kind 白名单/确认/重试/journal/审计映射）
- `src/MbdGitTool.Host/Bridge/HostCommandRouter.cs:328-711`（无 bind/unbind/undo）
- `src/MbdGitTool.Host/Services/BatchOperationService.cs:428-592`（DELETE 安全语义）、`:988-1042`（CREATE 基线）、`:1051-1140`（TAG 不落库）
- `src/MbdGitTool.Host/Services/GitCli.cs:35-41,57-137`（git.exe 网络路径）；`GiteaClient.cs:10-16,39,54-62,178-180`（repo_id/token 仅 header）
- `src/MbdGitTool.Core/Contracts/BridgeErrors.cs:7-59`（无 `E_DELETE_PENDING`）
- `src/MbdGitTool.Host/Services/OpenPathService.cs:22-34`（运行期测试开关先例）

**门禁/证据**
- `ui/scripts/lib/gitea-gate.mjs:26-70`；`ui/scripts/run-selfchecks.mjs:33-57`
- `ui/scripts/cdp-m2b2-realhost-check.mjs:20-23,149-161,165-186,264,271-282`
- `.gitignore:13`；`artifacts/m2b2-realhost/result.json`（SKIP）；`docs/testing-gitea-gate.md`
- `docs/m2b-ac-op-gap.md:9-42`（既有 GAP/PARTIAL 台账，§14 应对齐）；`docs/m2b2b-status.md:33-41`；`docs/review-m2b2-ux.md:221`（证据未入库 P1-8）
- ADR：`reqs/adr/D-025-m2a-m2b.md:83-95`（M2b 范围）、`:150-152`（M2b 出口 AC）；`reqs/adr/D-026-m2b2a-deferrals.md`
