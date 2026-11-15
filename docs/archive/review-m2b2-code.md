# M2b-2a 代码 / 数据安全对抗评审报告（提交 `1371f51`）

| 项目 | 内容 |
|---|---|
| 评审对象 | 提交 `1371f51c1080022cd72c59dd08a385003cba95b3`（`feat(m2b2a): SQLite operation_log, export, backup refs restore, EMPTY first push, retention`，2026-11-14 18:13:44 +0800） |
| 评审范围 | M2b-2a：`operation_log` SQLite 双写/查询/导出、备份 ref 列/恢复、EMPTY 首推、保留清理；**只评审、不改代码** |
| 输入 | `src/MbdGitTool.Host/Services/{AuditDatabase,OperationAuditService,BackupRefService,RetentionService,BatchOperationService,RepoActionService}.cs`、`src/MbdGitTool.Host/Bridge/HostCommandRouter.cs`、`src/MbdGitTool.Core/Contracts/{AuditDto,BackupDto,BridgeParams}.cs`、`tests/MbdGitTool.Host.Tests/M2b2*.cs`、`docs/m2b-status.md`、`docs/images/m2b/audit-log.png`、SRS `reqs/specification.md` §8.1（`:395`）/§8.2（`:406-413`）/§9.7 FR-OP-05/09/11（`:521,525,527`）/§7.6（`:344-373`）/附录 D（`:1366-1368`）、`reqs/specification.md（Annex A）` AC-OP-30（`:1015-1021`）、ADR D-025（`:89,162`） |
| 环境 | Windows / git 2.59 / .NET SDK 10.0.404 / 工作区 `D:\Workspace\git_1`（`HEAD=1371f51`，`git status` 干净）；真实 Host PID 63108（18:12:35 启动，评审期间未杀、未驱动）；评审期间另有并行进程驱动真实 Host/运行测试 |
| 方法 | ① 逐行静态核对（行号均指 `1371f51`）；② `dotnet test` 实跑（Host 全量 266/266、Core 71/71，0 失败/0 跳过）；③ 只读查询真实 `%LOCALAPPDATA%\MbdGitTool\db\mbd.db`（`Mode=ReadOnly`）与 `audit/operations.jsonl`、`host.log`；④ 关键破坏性行为用**工作区外临时 git 仓**做黑盒复现（不修改仓库）；⑤ 与 SRS/AC/ADR 逐条对照 |
| 边界 | 未修改任何源码/配置/真实仓库/真实用户数据；唯一产出为本文件。副作用披露见 §9。 |

---

## 0. 判决（结论）

> **M2b-2a 需先修，本轮不可 tag。**
> **汇总：P0 = 1，P1 = 5，P2 = 12。**

- 测试绿（337/337）与构建 0 warning 成立，但**新的 P0 命中恢复类破坏性操作的数据丢失路径**：`restoreBackupRef` 的脏工作区检查用 `--untracked-files=no` 放行 untracked 文件，而 `git reset --hard` 会静默删除/覆盖“挡路”的 untracked 文件；无冲突预览、确认文案也未提示（FR-OP-11/AC-OP-30 ③④、FR-SAFE-01 全为 P0 要求）。已用黑盒复现。
- P1 集中于：审计库/导出被历史测试数据污染且被当作“真实 521 行”证据（本报告已定位来源与提交内修复）、审计保留下限仅 1 天（< FR-OP-09 ≥90）、备份 ref 丢失分支身份且恢复不校验当前分支（偏离 SRS `<ts>/<branch>`）、SQLite 写失败静默丢行/不降级/无 JSONL→DB 回填、恢复前安全备份 ref 秒级且可被同秒覆盖/产品内不可见不可恢复。
- `docs/m2b-status.md:62` 已自述“审计测试隔离清理”待 M2b-2b，但 `:54-55` 的“521 行真实可查/真实 521 行导出成功”口径不实（详见 §2.2）。
- 建议：修 P0-1 + P1-1…P1-4（P1-5 可随 M2b-2b 的 journal/reconcile 一并收口）后复评；M2b-2b（journal/reconcile、离线、AC-OP 全量证据）未完成，不得写 M2b/M2 完成。

| 汇总口径 | 结果 |
|---|---|
| 全量测试 | Host **266/266**、Core **71/71** 通过（0 失败/0 跳过，1 m 46 s / 64 ms），与状态报告 337/337 一致 |
| 真实库/审计（只读） | operation_log **522→523 行**；JSONL **331 行**；其中真实 Host 行**仅 4→5 行**，其余约 518 行为测试/开发期写入（§2.2） |
| 新 P0 | **1**（P0-1 恢复静默丢 untracked） |
| 新 P1 | **5**（P1-1…P1-5） |
| 新 P2 | **12**（P2-1…P2-12） |
| 是否可 tag | **否（需先修）**；仅当 P0-1 与 P1-1…P1-4 闭环、且状态报告证据口径更正后，可考虑“M2b-2a 阶段 tag” |

---

## 1. 评审基线与可复跑证据

1. **受评对象确定**：`HEAD=1371f51`、工作区干净；所有文件:行号均对应该提交。
2. **测试实跑**（Release，未改源码）：
   ```powershell
   dotnet test tests/MbdGitTool.Host.Tests -c Release --no-restore
   # 已通过! - 失败: 0，通过: 266，已跳过: 0，总计: 266 … 1 m 46 s
   dotnet test tests/MbdGitTool.Core.Tests -c Release --no-restore
   # 已通过! - 失败: 0，通过: 71，已跳过: 0，总计: 71 … 64 ms
   ```
   合计 337/337，与 `docs/m2b-status.md:60` 一致。
3. **真实数据只读取证**（`%LOCALAPPDATA%\MbdGitTool`，评审时刻 18:13–18:28 +0800）：
   - `db/mbd.db`：`PRAGMA quick_check=ok`、`sqlite_version=3.53.3`、`journal_mode=wal`、`user_version=1`；
   - `operation_log` 行数与分布见 §2.2；`audit/operations.jsonl` 331 行；`jobs/jobs.json` 37 个作业；`exports/` 7 个文件；
   - 真实 Host 于 18:12:35 启动（PID 63108），18:15:52 执行了一次 `cleanupRetention`+两次导出（`host.log` 对齐）。
4. **并行声明**：评审期间另有并行进程在驱动真实 Host / 跑测试，`host.log` 为多进程混写。本报告以“评审前后行数差 + 日志时间点 + 进程行为”归因；不归属到本报告结论的行均已标注。
5. **黑盒复现**（工作区外 `%TEMP%` 临时仓）：`git status --porcelain --untracked-files=no` 为空 → `git reset --hard <backup>` 退出码 0 且 untracked 文件内容被 tracked 内容覆盖（§3.2 证据）。

---

## 2. 审计隔离（对应评审要求 1）

### 2.1 当前 HEAD 的测试是否写真实 `%LOCALAPPDATA%\MbdGitTool\db`：**已隔离（PASS），但仅靠约定**

| 核对项 | 证据 | 判定 |
|---|---|---|
| 测试配置统一注入临时落点 | `TestInfrastructure.cs:332-348`：`TestAudit.NewConfig` 同时设置 `AuditRoot`/`JobsRoot`/**`DatabasePath`**/**`ExportsRoot`** | PASS |
| 该隔离是**本提交**才补齐的 | `git show 1371f51 -- tests/MbdGitTool.Host.Tests/TestInfrastructure.cs`：新增 `DatabasePath = NewDatabasePath()`、`ExportsRoot = NewExportsRoot()`（+12 行） | 关键证据（污染根因） |
| 所有构造审计/路由的测试都走隔离 | `grep new HostCommandRouter(` 全部命中 `TestAudit.NewConfig(...)`（`HostCommandRouterTests:143,315,321`、`JobEngineTests:41…1356`、`M2b2RouterApiTests:28,77,112,182,225`、`OperationAuditServiceTests:103,156`、`RealWorkspaceSmokeTests:51`、`RepoActionSecurityTests:395,538,775`）；`JobEngineTests:428,525,1512` 直接 `new AppConfig{...AuditRoot=临时}` 时 DB 解析到 `AuditRoot\mbd.db`（`OperationAuditService.cs:77-87`） | PASS |
| 实跑验证（关键） | 本报告可控复跑：Host 全量前后真实 DB **523→523 行、max(id)=523 不变**；真实 JSONL 331→331 行；exports 7→7；新增的唯一 1 行（id=523，`CLEANUP_RETENTION`，10:15:52.988Z）在 `host.log` 有对应真实 Host `api cleanupRetention … ms=952`（10:15:52.989Z） | **PASS：当前提交测试 0 写入真实库** |
| 残留/旁路证据 | 18:16:49 出现一笔“523 行 → 真实 `exports/`”的导出（`host.log` 上下文为 cloneA/remote 测试夹具 + `E_GITEA_CONFIG_INVALID`），非本报告可控复跑，来源更可能是并行进程/陈旧构建；说明隔离只是 `TestAudit.NewConfig` 约定，任何直接 `new AppConfig()` 的临时 harness 仍会写真实用户目录 | P2-11 |

### 2.2 真实 `operation_log` 的 500+ 测试行：**来源已定位；当前约 99% 为测试数据**

只读取证（评审时刻，`Mode=ReadOnly`，未清理）：

| 维度 | 实测 |
|---|---|
| 总行数 | **523**（18:13 只读首查 522 行；18:15:52 真实 Host cleanup 追加 id=523；18:12:40 导出 521 行可交叉验证） |
| 组件分布 | `CF100A_Demo` **370**、`MOCK_CF1001_ChanganSigProcgBascFct` 16、`CF100B_Slow` 14、`CF999_NotThere` 8、路径穿越夹具 `..\..\evil` 6、**`RTCT_Demo` 2**、NULL（CLEANUP_RETENTION）106（追加后 107） |
| 动作分布 | `CLEANUP_RETENTION` 106→107、COMMIT 92、DELETE 70、FETCH 68、SYNC 58、CREATE 32、CLONE 20、PULL 20、TAG 16、PUSH 12、RESTORE 12、RESET 8、CHKOUT 6、RESTORE_BACKUP 2 |
| actor | `Trevn` 498→499、`tester` 24（`Trevn` 来自 `Environment.UserName`，与真实 Host/路由测试共用） |
| 时间窗 | 09:47:07Z–09:59:19Z（约 12 分钟）+ 10:12:41Z（真实）/10:15:52Z（真实） |
| 真实 Host 行 | 仅 **5 行**：`RTCT_Demo` RESTORE_BACKUP×2（09:52/09:53，`cdp-m2b2-realhost-check.mjs` 的隔离副本恢复）+ CLEANUP_RETENTION×3（09:51:30、10:12:41、10:15:52）；与真实 JSONL 09:40 后仅 5 行一一对应 |
| 测试来源行 | ≈ **518 行**：414 行来自测试夹具组件（370+16+14+8+6）+ 104 行测试期 CLEANUP_RETENTION |
| JSONL 对照 | 331 行（含历史 M2a 测试污染 64 行：`CF100A_Demo` 38 + `MOCK_*` 26，06:36–07:13Z）；09:40 之后仅 5 行（全为真实 Host）→ **同一时刻 DB 522 行 vs JSONL 5 行，双写已分裂** |

**根因定位（可复跑）**：

1. `host.log` 中 `审计库已就绪` 共 863 次，其中 **315 次解析到真实 `C:\Users\Trevn\AppData\Local\MbdGitTool\db\mbd.db`**，集中在 09:47:07Z–09:59:19Z（如 09:47:07.734/.888/.918/.947/.969… 一秒内 10+ 次），与 DB 行时间窗完全重合；这些时刻同时出现 `api getSnapshot source=mock（工作区未配置）`/`git status 失败(… not a git repository)` 等**单测特征**。
2. 同窗口 JSONL 侧没有任何测试行（`AuditRoot` 自 M2a 起已隔离），但 SQLite 侧被写满 → **当时 `AppConfig.DatabasePath` 尚未在测试 helper 中注入**；`1371f51` 才在 `TestInfrastructure.cs` 补齐 `DatabasePath/ExportsRoot`（提交 diff 见上表）。
3. 因此：**09:47–09:59Z 的“M2b-2a 未提交工作树”全量测试（M2b-2a 代码 + 旧 TestAudit）把约 518 行测试/开发数据写进了真实用户审计库**；10:13:44 提交后才隔离，故“当前提交”不再新增，但**历史污染未清理**。
4. **证据失真**：`docs/m2b-status.md:54-55` 把该库的 521 行写成“真实可查/真实 521 行导出成功”；提交内截图 `docs/images/m2b/audit-log.png` 底部直接显示 **“共 522 条”** 且可见 `CF100A_Demo`、`E_CONFIRM_REQUIRED` 等测试夹具行。该证据不能证明真实环境审计链路，只能证明“库可查询/导出”。
5. 同类污染：`jobs/jobs.json` 37 个作业（真实工作区只有 RTCT_* 组件，CLONE/CREATE/DELETE/SYNC 等为夹具场景），`host.log` 5.5 MB 内混入大量测试进程日志；`docs/review-m2a-*` 已记录 JSONL 侧同类 R-3 问题，本次是**新落点（SQLite/导出）上的 R-3 回归**。

→ **P1-1**（证据链/数据完整性）+ **P2-11**（无强制守卫、logs/jobs/exports 清理）。

### 2.3 SQLite / JSONL 双写一致性、失败降级：**不一致可永久化，运行期不降级**

| 核对项 | 证据 | 判定 |
|---|---|---|
| 写序 | `OperationAuditService.cs:199-215` 先写 JSONL（失败仅 WARN，方法无返回值）→ `:217-244` 再写 SQLite（失败仅 WARN）| 两步非原子、互不感知 |
| JSONL 失败可继续 | `WriteJsonLine` 内部 catch 后返回 void（`:451-459`），SQLite 仍会写 → DB 有行/JSONL 无行 | 可分裂 |
| SQLite 失败仅告警 | `AuditDatabase.Insert` 返回 false（`:235-277`），`Record`/`JobEngine`（`JobEngine.cs:1049-1079` 先置 `AuditWritten=true`）均忽略返回值；仅 `host.log` WARN | 可静默丢行 |
| 运行期降级 | `_available` 只在 `Initialize` 置位/清零（`AuditDatabase.cs:153-232`）；运行期多次写失败不会置 `DatabaseAvailable=false`，UI 仍显示“可用”，查询继续返回“不完整但成功” | **P1-4** |
| 补偿/回填 | 无重试队列、无 `request_id` 唯一索引/去重、无 JSONL→DB 回填/重建；损坏隔离后也不回放 `.corrupt-*` | P1-4/P2 |
| 查询/导出数据源 | 只读 SQLite（`OperationAuditService.cs:267-280,311-337`）；DB 不可用直接 `E_INTERNAL`（`:875-878`），JSONL 不参与 | 设计声明“JSONL 仍生效”，但用户侧查询/导出缺行 |
| 实证分裂 | 同一时刻 DB 522 行 vs JSONL 331 行（§2.2）；DB 中 09:47 后约 518 行测试数据在 JSONL 完全没有对应行 | 直接证据 |
| 降级路径测试 | `M2b2AuditDatabaseTests.cs:360-375`：无 DB 时查询/导出 E_INTERNAL、JSONL 仍写 | PASS（仅初始化失败场景） |
| 写入失败测试 | 无（见 §6） | P2-12 |

### 2.4 WAL / busy_timeout / 并发 / quick_check / 迁移

| 核对项 | 证据 | 判定 |
|---|---|---|
| 连接参数 | `AuditDatabase.cs:419-449`：初始化连接 `journal_mode=WAL; synchronous=NORMAL; busy_timeout=5000; foreign_keys=ON`，后续连接同样设置（journal_mode 为库级持久设置，只在初始化设置） | PASS |
| 实测 WAL/版本/schema | 真实库 `journal_mode=wal`、`sqlite_version=3.53.3`（≥ SRS §8.2 的 3.51.3）、`user_version=1`；`M2b2AuditDatabaseTests.cs:25-60` 断言 WAL+user_version+15 列 | PASS |
| 并发 | 每次写入/查询新开连接（`Pooling=true`），WAL 允许单写多读 + busy_timeout=5000；但**无进程内写串行化**（`_sync` 只保护 JSONL），也无 SRS §8.2 要求的“生产 1 写连接 + 只读连接池” | 功能可用，架构偏离 → P2-4 |
| 并发测试 | 只有 JSONL 并发 32 线程测试（`OperationAuditServiceTests.cs:81-96`）；**无 SQLite 并发/锁等待/WAL 读写测试** | P2-12 |
| quick_check | 启动每次 `PRAGMA quick_check`（`:197-202,451-469`）；损坏库重命名 `.corrupt-<stamp>`（`:529-552`），测试 `M2b2AuditDatabaseTests.cs:62-76` | PASS（行为） |
| 损坏分类 | `TryInitializeCore` 把**任何 `SqliteException`** 当 `Corrupt`（`:215-221`），包括 `database is locked`、`disk I/O error`；可能把健康库隔离重建（rename 失败则降级） | P2-2 |
| 高版本库 | `user_version > 1` 禁用写、不隔离（`:471-485`），测试 `:78-99` | PASS |
| 迁移 | 当前仅 v0→v1；`TryEnsureSchema` 在事务内建表/索引/置 user_version（`:488-511`），注释说明未来迁移会 `VACUUM INTO`（`:486-487`）但**未实现** | P2-2 |
| 初始化失败降级 | 文档声称“永不抛异常/降级 JSONL-only”，但 `Initialize` 的 catch 过滤器（`:222-231`）不含 `DllNotFoundException`/`TypeInitializationException`/`BadImageFormatException`；`OperationAuditService` 构造函数直接 `Database?.Initialize()`（`:126`）→ 原生 SQLite 加载失败可让 Host 启动崩溃（旁路变主路） | P2-2 |

---

## 3. 恢复安全（对应评审要求 2）

### 3.1 只允许 `refs/backup/sync/`、参数/路径校验

| 核对项 | 证据 | 判定 |
|---|---|---|
| ref 白名单 | `BackupRefService.TryParseBackupRef`（`:276-327`）：必须 `refs/backup/sync/` 前缀、恰好 2 段、14 位 UTC 时间戳、段 2 ∈ 四仓（大小写归一）；`RestoreAsync` 再次校验并与 `repoType` 一致（`:107-148`）；`rev-parse --verify …^{commit}` 存在性校验（`:157-167`） | **PASS** |
| 参数注入 | 全部走 `ArgumentList`（`GitCli.RunAsync`），ref 已白名单化，无前导 `-`/`..`/控制字符；`M2b2BackupRefTests.cs:90-115,153-166` 覆盖 `refs/heads/main`、`../evil`、缺失、类型不一致、非法类型 | **PASS** |
| 本地路径校验 | `ValidateRepoPath`（`:496-514`）：canonical + 存在 + 是 git 仓；路径来自快照（router `TryGetLocalRepoPath`），非调用方直传 | PASS（但 `BackupRefService` 本身**不校验工作区包含性**，防纵深受快照/路径策略约束 → P2） |
| 加锁 | 恢复前 `AcquireRepoLockAsync`（`:150-155`），与单仓五键/批量 job 共享 per-repo 锁 | PASS |
| 远端零改动 | 恢复链路仅 `rev-parse`/`status`/`update-ref`/`reset --hard`，无网络/凭据注入；无 push/fetch | **PASS（代码确定）**；测试未显式断言“Gitea 零写请求”（P2-12） |

### 3.2 dirty / untracked / 冲突策略：**P0 数据丢失**

- 脏检查：`git status --porcelain --untracked-files=no`（`BackupRefService.cs:169-189`），有已跟踪改动 → `E_DIRTY_WORKTREE` 阻断，测试 `M2b2BackupRefTests.cs:117-134`。**PASS（已跟踪改动）**。
- **P0-1（未跟踪文件可被静默删除/覆盖）**：
  - 代码注释称“untracked 不受 reset 影响”（`:169`），但 git 语义并非如此：`git reset --hard` 对“挡路”的未跟踪文件/目录会**直接删除/覆盖**（git-reset 文档；本地黑盒复现：`status --untracked-files=no` 为空 → `reset --hard <backup>` 退出 0 → untracked `a.txt`（内容 `PRECIOUS-UNTRACKED`）被备份树里的 `tracked v1` 覆盖，未提示）。
  - 触发场景真实存在：SYNC 丢弃本地提交后用户重新生成同名未跟踪文件/目录（笔记、导出物、本地配置），或备份树包含某路径而工作区该路径是新建未跟踪文件 → 恢复即静默丢失。
  - 违反：FR-OP-11/AC-OP-30 ③④“恢复前展示将覆盖的当前状态与冲突、冲突默认不覆盖、可取消”（`requirements.md:527`、`ac-catalog.md:1020`）、FR-SAFE-01（删除/强覆盖前展示“将丢失什么”）；也与 FR-OP-05“untracked 默认不删”（`:521`）的保护方向相反。
  - UI 确认文案仅写“未包含在该备份中的未推送改动可能丢失”（`ui/src/stores/workbench.ts:2879`），未提示 untracked；而后端已阻断已跟踪改动，文案与实际风险错位。
  - 测试只覆盖**不冲突**的 untracked（`M2b2BackupRefTests.cs:136-151` 的 `keep-me.txt` 不在备份树中 → 必然通过），掩盖该路径。
- 其它冲突未检查：merge/rebase 进行中（无 `MERGE_HEAD`/`rebase-merge` 预检）、ignored 文件挡路、当前分支与备份来源分支不一致（见 P1-2）。

### 3.3 当前 HEAD 安全备份：**P1（秒级 ref 可被同秒覆盖；产品内不可见/不可恢复）**

| 核对项 | 证据 | 判定 |
|---|---|---|
| 备份时机 | 脏检查通过后、reset 之前 `update-ref refs/backup/restore/<stamp>/<TYPE> <preHead>`（`:191-210`）；失败即拒绝 reset | 顺序正确 |
| ref 唯一性 | stamp = `DateTime.UtcNow.ToString("yyyyMMddHHmmss")`（**秒级**），同仓同秒第二次恢复会 `update-ref` **覆盖**同一个安全 ref。`M2b2BackupRefTests.cs:78-88` 自身即演示：第一次 restore 把 `second` 备份到 safetyRef，随即第二次 restore 又把同一 safetyRef 覆盖为 `first` → 原始恢复前 HEAD 的安全备份丢失（尚可由 HEAD reflog 兜底） | **P1-5** |
| 可见/可恢复 | `refs/backup/restore/*` 不在 `getBackupRefs`（`:54-95` 只列 sync 前缀）、也不能作为 `restoreBackupRef` 的 ref（白名单仅 sync）→ **产品内无法列出/恢复到该安全备份**；且无保留清理策略（只增不减） | P1-5 |
| 空仓 | 无 HEAD 时跳过安全备份，消息标注 `<空仓无 HEAD>`（`:195-210,231-238`） | PASS |

### 3.4 失败不误报成功、审计恰 1 行、备份 ref 清理

| 核对项 | 证据 | 判定 |
|---|---|---|
| 失败不误报成功 | `Restored=true` 仅在 `reset --hard` 成功后返回（`:212-238`）；ref 不存在/脏工作区/update-ref 失败均返回错误，测试覆盖前两者 | 基本 PASS；`reset`/`update-ref` 失败分支**无失败注入测试**（`GitCli` sealed 不可注入 → P2-12） |
| 成功被误报失败（反向） | reset 成功后路由还要 `InvalidateCaches` + `_snapshots.RefreshAsync`（`HostCommandRouter.cs:541-545`）；若 refresh 取消/异常，`DispatchAsync` 返回 `E_TIMEOUT/E_INTERNAL`，但仓库已恢复且审计已写 OK → UI 可能提示失败并诱导重试 | P2 |
| 审计恰 1 行 | restore 成功/失败（`restoreError != null`）各写 1 行 `RESTORE_BACKUP`（`HostCommandRouter.cs:509-539`），`requestId` 每次新 GUID；`M2b2RouterApiTests.cs:164-176` 断言 `Assert.Single` | **PASS（尝试级）**；注意 `confirm!=true`、组件解析失败在审计前早退（0 行）；失败行 `preHead/postHead` 恒 null（丢失已知信息）；无失败行审计测试 |
| 审计内容 | `action=RESTORE_BACKUP`、`state` 取快照、`backupRef=ref`、actor/requestId/durationMs；无 token | PASS |
| 备份 ref 清理只删本地 | `CleanupRepositoryAsync` 只 `for-each-ref` 本地 + `update-ref -d`（`:389-441`）；`CleanupExpiredAsync` 只处理 `refs/backup/sync/`（`:246-271`）；测试 `M2b2BackupRefTests.cs:168-193` 断言 31 天删除、1 天保留、**bare 远端零 backup ref** | **PASS（不删远端）** |
| 会否删刚建的 | 以 ref 名内 14 位 UTC stamp 为创建时间，`createdAt > cutoff` 跳过；SYNC 用当前 UTC 创建（`BatchOperationService.cs:210-223`），不会命中 30/7 天前 cutoff；未来 stamp 也保留 | **PASS** |
| 保留期正确性 | `backup.retentionDays` 默认 30、夹取 7–90（`AppConfig.cs:82,153`；`BackupRefService.cs:255`），符合附录 D `:1367`；29/31 天边界无测试（仅 1/31） | 基本 PASS；边界测试 P2-12 |
| 不删 restore ref | 前缀不同 → 安全 ref 不会被清（好），但无上限 | P1-5/P2 |
| 清理加锁 | `CleanupRepositoryAsync` **未持有 per-repo 锁**；会与运行中的 SYNC/恢复并发（只删超期 sync ref，风险低） | P2-10 |

---

## 4. EMPTY 首推（对应评审要求 3）

| 核对项 | 证据 | 判定 |
|---|---|---|
| 矩阵恢复 | `RepoActionService.Matrix`：`EMPTY="NYCYY"`（顺序 PULL/FETCH/CHKOUT/COMMIT/PUSH，`:62-80`）；PUSH 允许且 `RequiresConfirm` 强制 true（`:136-180`）；与 §7.6 EMPTY 行（Fetch ✅/Pull —/Push ✅首推/Commit ✅/Checkout ⚠️，`requirements.md:353`）及单源夹具 `reqs/fixtures/m2-matrix.json`（`EMPTY.PUSH.enabled=true, requiresConfirm=true`）一致；`RepoActionAvailabilityMatrixTests` 逐格断言 | **PASS** |
| 首推建 upstream | `PushAsync`：`firstPush=state=="EMPTY"`，`setUpstream = args.setUpstream || firstPush || NO_UPSTREAM`（`:766-813`）；argv 固定 `push [-u] origin HEAD`，无 force；`M2b2EmptyPushTests.cs:39-68` 断言 `@{upstream}=origin/main` 且 bare 远端 main=HEAD | **PASS** |
| unborn 零远端写 | 首推前 `rev-parse --verify HEAD`，失败返回 `E_REF_NOT_FOUND + requiresCommit`，**在启动网络 git 前**（`:780-803`）；测试 `:70-94` 断言 bare `for-each-ref` 为空 | **PASS** |
| NO_UPSTREAM 不回退 | 同一 `setUpstream` 兜底，NO_UPSTREAM 必带 `-u`（不出现“普通 push 后再补 upstream”的二次写）；`M2b2EmptyPushTests.cs:96-122` 断言 upstream 已建立 | **PASS** |
| token/凭据作用域 | `ExecuteAsync` 对网络动作先 `PrepareCredentialEnvironmentAsync`（`RepoActionService.cs:389-404,857-928`）：token 非空时校验 base/org、`origin` fetch+pushurl 的 scheme/host/port/org 作用域，失败 fail-closed 且**不启动网络 git**；token 仅经 `GIT_CONFIG_* http.<base>/.extraHeader` 注入，不进 argv/日志/响应（`:960-1044`）；单测覆盖作用域不符/明文 http（`RepoActionSecurityTests`）。EMPTY/NO_UPSTREAM 复用同一路径 | **PASS** |
| 测试缺口 | 无 EMPTY 的批量 PUSH/job/审计断言；无“EMPTY + token 作用域失败”优先级断言；无 `push` argv 无 `--force` 的显式采集（EMPTY 场景） | P2-12 |

---

## 5. 导出 / 查询 + 保留清理（对应评审要求 4）

### 5.1 导出/查询

| 核对项 | 证据 | 判定 |
|---|---|---|
| 路径不接受调用方 | `ExportOperationLogParams` 只有 `Format/From/To`（`BridgeParams.cs:99-106`）；导出目录固定 `ExportsRoot`（默认 `%LOCALAPPDATA%\MbdGitTool\exports`，`OperationAuditService.cs:89-100,338-345`）；文件名固定前缀+UTC 戳+GUID | **PASS** |
| CSV 转义 | `Csv()` 对 `,` `"` CR/LF 加引号、`"` 双写（`:818-831`）；BOM+CRLF（`:789-793`） | PASS |
| CSV 公式注入 | 未做 `= + - @`/制表符前缀中和。当前写入字段均为受控值（action 枚举、componentId 来自 Gitea 命名约束、actor 为 Windows 用户名、requestId=GUID、backupRef 白名单），**当前无攻击面**；但防深度不足，Excel 打开时若未来引入自由文本字段即有风险 | P2-5 |
| 脱敏 | DB 只存元数据；导出字段不含 token/Authorization/命令 output；测试 `M2b2AuditDatabaseTests.cs:221-222,282-299`、`M2b2RouterApiTests.cs:101-105` 断言无 token | **PASS** |
| 查询过滤/分页 | `BuildFilter` 校验 limit 1–1000、offset 0–1e6、action/result 白名单字符并大写、componentId ≤128 无控制字符、from/to 支持日期/ISO 且 from≤to（`:555-722`）；参数化 SQL（`AuditDatabase.cs:280-330`）；`total` 为过滤后总数（`:293-301`）；测试覆盖过滤/分页/非法参数（`M2b2AuditDatabaseTests.cs:101-130,225-262`） | **PASS** |
| 查询资源/一致性 | `COUNT(*)` 与 `SELECT` 分两条命令、无事务快照，并发写时 total 与当前页可不一致；`getOperationLog` 为同步 DB 工作，`HostCommandRouter` 的 10 s 超时**无法取消**；`limit=1000` 且字段接近 256 字符上限时响应可超 2 MB（`MaxResponseBytes`，`HostCommandRouter.cs:24-26`/`WebViewBridge.cs:96`）→ `E_RESPONSE_TOO_LARGE` | P2-7/P2-8 |
| 导出过滤不对称 | UI 只传 `format`（`ui/src/api.ts:1850`），Host 只支持 `from/to`；审计页当前 `action/componentId/result` 过滤**不作用于导出** → 用户以为导出“筛选结果”，实际导出全量（易触发 100k 上限 `E_TOO_MANY_ENTRIES`，也可能导出超出意图的数据） | P2-6 |
| 导出目录权限 | `Directory.CreateDirectory(ExportsRoot)` 继承 `%LOCALAPPDATA%` 默认 ACL（本机用户+SYSTEM+管理员），未额外收紧；导出文件无删除/保留策略，7 个文件已含历史测试数据 | P2-5 |
| 大文件/OOM | `QueryAll` 一次装载 ≤100k 行；CSV 流式写出（好）；**JSON 导出 `JsonSerializer.Serialize(envelope)` 生成完整字符串**（100k 行可数百 MB）+ `WriteIndented`，存在 OOM 风险；`PruneJsonLines` 把全部保留行读入 `List<string>` 后重写（JSONL 90 天可很大） | P2-7 |
| 导出审计 | 导出动作本身不写 operation_log（不属强制项） | P2-5 |

### 5.2 保留清理是否误删保留期内数据

| 核对项 | 证据 | 判定 |
|---|---|---|
| 审计保留默认 90 天 | `AppConfig` 默认 90（`:61,70`）；`CleanupExpired` 删除 `ts < UtcNow - retentionDays` 的 SQLite 行 + JSONL 行（`OperationAuditService.cs:374-388`）；JSONL 对无法解析行保留（`:526-551`）；`M2b2AuditDatabaseTests.cs:306-344` 断言只删 91 天、保留近期 | 默认 PASS |
| **审计保留可被配到 <90 天** | `AppConfig.EffectiveAuditRetentionDays` = `Clamp(…, min:1, max:3650)`（`:79,152`），`OperationAuditService.cs:124` 同样夹取 1–3650；**FR-OP-09 要求“审计保留 ≥90 天”**（`requirements.md:525`），附录 D 明确 `audit.keepDays` 30–365（`:1368`）。用户把 `auditRetentionDays` 设为 30/7/1 即会删除 90 天内审计；测试还把 3650 固化为期望（`M2b2AuditDatabaseTests.cs:386-390`） | **P1-3** |
| 备份 ref 保留 | 默认 30、夹取 7–90（符合附录 D `:1367`）；只删本地、不删远端（测试 PASS，§3.4）；只删 `refs/backup/sync/*`，不删 `restore`（安全但无上限） | PASS（边界测试缺，P2-12） |
| `.trash` 保留 | 固定 `TrashRetentionDays=30`（`RetentionService.cs:22,120`），`CleanupTrash` 只删 `<root>\.trash\<14位戳>` 且 `IsWithin` 校验（`BatchOperationService.cs:1004-1060`）；但**不可配置**（FR-OP-06 验收“保留期与清理策略可配置且默认 30 天”、附录 D `delete.retentionDays` 7–90 均未实现） | P2-10 |
| 不误删新数据 | 三处 cutoff 均 `UtcNow - N days`，新 ref/新行/新 trash 不命中；未来时间戳保留 | PASS |
| 清理动作审计 | 命令路径写 1 行 `CLEANUP_RETENTION`（`RetentionService.cs:85-100`，测试 `M2b2RouterApiTests.cs:266-269`）；**启动后台清理 `writeAudit:false`** → 删除 `.trash`/备份 ref/审计行的启动清理无审计痕迹 | P2-10 |
| `.trash` 清理路径安全 | 仅词法 `Path.GetFullPath` + `IsWithin`（`PathPolicy.TryCanonicalize` 不解析 symlink），未做 `CheckReparseChain`；若 `.trash` 或 `.trash/<stamp>` 是 junction，可能删到工作区外（.NET `Directory.Delete` 只删链接本身，风险有限，仍属防御缺口） | P2-10 |
| 清理并发 | backup ref 清理不持 per-repo 锁；命令 `cleanupRetention` 可与运行中 job 并发（但仅删超期项） | P2-10 |

---

## 6. 测试缺口与失败注入（对应评审要求 5）

| # | 缺口 | 影响 |
|---|---|---|
| 1 | `GitCli` 为 `sealed` 且 `RunAsync` 非 virtual（`GitCli.cs:37,53`），`BackupRefService` 无法注入失败 | **无法测试** `update-ref` 失败、`reset --hard` 失败、postHead 读取失败 → “失败不误报成功/失败零改动”关键分支无证据（P2-12） |
| 2 | 无 DB `busy/locked`、`Insert` 失败、WAL 并发读写、运行期写失败降级测试 | P1-4 的关键失败路径未覆盖 |
| 3 | 无 CSV 公式注入/引号/换行/超大字段测试；无 `>MaxExportRows`（100k）测试；无导出 IO/权限失败注入 | P2-5/P2-7 |
| 4 | 保留期只测 1/31 天，未测 AC-OP-30 明确要求的 29 天保留、31 天回收；未测刚创建 ref 不被删；未测 restore ref 不被删 | AC-OP-30 ⑥ 边界无直接证据 |
| 5 | restore 未测 untracked 冲突（P0-1）、当前分支与备份来源不一致、merge/rebase 进行中、ignored 挡路 | P0/P1 路径无回归保护 |
| 6 | restore 未测“失败恰 1 行审计”（仅测成功路径 `Assert.Single`）；未测得失败行内容/requestId 唯一 | 审计完整性证据不足 |
| 7 | EMPTY 未测批量 PUSH/job/审计；未显式采集 push argv 无 `--force` | 矩阵恢复的真实执行链证据有限 |
| 8 | 无 `getOperationLog` 1000 行响应上限/超时不可取消/COUNT-SELECT 一致性测试；无 `getBackupRefs` 单仓失败部分结果测试 | P2-7/P2-8/P2-9 |

---

## 7. 问题清单（P0/P1/P2）

### P0（阻塞 tag，必须修）

**P0-1 · `restoreBackupRef` 静默删除/覆盖冲突的 untracked 文件（数据丢失）**
- 位置：`src/MbdGitTool.Host/Services/BackupRefService.cs:169-189`（`status --untracked-files=no`）→`:212-221`（`reset --hard ref`）。
- 证据：git-reset 语义 + 本地黑盒复现（untracked 内容被 tracked 内容覆盖，退出码 0）；测试 `M2b2BackupRefTests.cs:136-151` 只覆盖不冲突文件；UI 文案（`ui/src/stores/workbench.ts:2879`）未提示 untracked。
- 违反：FR-OP-11/AC-OP-30 ③④（冲突预览、默认不覆盖、可取消）、FR-SAFE-01、FR-OP-05 的 untracked 保护精神。
- 建议：恢复前用 `--untracked-files=normal`（含 ignored 可选）计算“挡路集合”并展示/阻断；或 `git stash -u` 后再 reset；或改用 `git read-tree`+安全的 checkout 策略；确认文案必须列将丢失文件。

### P1（复评前应闭环）

**P1-1 · 真实审计库/导出/截图 99% 为测试数据，且被当作“真实 521 行”证据**
- 证据：§2.2（523 行中约 518 行为测试来源、真实 5 行；`docs/m2b-status.md:54-55`；`docs/images/m2b/audit-log.png` 显示 522 条 + `CF100A_Demo`）。
- 根因：`1371f51` 才补 `TestAudit.NewConfig` 的 `DatabasePath/ExportsRoot`（提交 diff）；09:47–09:59Z 的未提交工作树测试运行写满真实库（`host.log` 315 次真实 DB 初始化）。
- 建议：清理/标注真实库与 exports 的测试行（保留 JSONL 真实行）；更正状态报告证据口径（区分“真实 Host 行 5 条”与“测试行”）；CI 增加真实路径零新增守卫。

**P1-2 · 备份 ref 丢失分支身份，恢复不校验当前分支（偏离 SRS）**
- 位置：`BatchOperationService.cs:210-223`（`refs/backup/sync/<stamp>/<REPO_TYPE>`）；`BackupRefService.cs:101-239`（restore 直接用当前分支 `reset --hard`）。
- SRS：FR-OP-05/AC-OP-16/AC-OP-30 均为 `refs/backup/sync/<ts>/<branch>`（`requirements.md:521`、`ac-catalog.md:106,900,1018`），无 ADR 变更记录；实现把 branch 换成 repoType，branch 信息永久丢失。
- 风险：用户切到分支 B 后恢复分支 A 的备份 → B 指针被重置为 A 的旧提交；UI/审计无 branch 字段无法提示；AC-OP-30 “恢复后本地分支包含那 2 个 commit”只在同名分支下成立。
- 建议：补 ADR 或改为 `<ts>/<branch>`（兼容旧 ref 迁移）；restore 展示/校验当前分支与备份分支，不一致时二次确认或阻断。

**P1-3 · 审计保留下限 1 天（< FR-OP-09 的 ≥90 天；附录 D 30–365）**
- 位置：`AppConfig.cs:79,152`、`OperationAuditService.cs:124`（`Math.Clamp(…,1,3650)`）；`M2b2AuditDatabaseTests.cs:386-390` 把 3650 固化为期望。
- 后果：合法配置 30/7/1 天会删除 90 天内审计（含 JSONL/DB），可在无提示下销毁保留期内证据；与附录 D `audit.keepDays 30–365`（`requirements.md:1368`）也不一致。
- 建议：审计保留下限按 FR-OP-09 取 ≥90（或至少附录 D 30–365 并记录 SRS 内部冲突），更新测试期望；`delete.retentionDays`（.trash 7–90 可配）一并补齐（见 P2-10）。

**P1-4 · SQLite 写失败静默丢行、运行期不降级、无 JSONL→DB 补偿**
- 位置：`AuditDatabase.cs:235-277`（返回 bool 被忽略）、`:153-232`（`Available` 仅初始化阶段）、`OperationAuditService.cs:199-244`（两次写互不感知，仅 WARN）、`:251-368`（查询/导出只读 DB）。
- 证据：实库 522 行 vs JSONL 331 行分裂（同窗口 DB 约 518 行测试数据/JSONL 0），说明两写落点可永久不一致且无对账；当前仅初始化失败会降级（`M2b2AuditDatabaseTests.cs:360-375`）。
- 建议：`Insert` 失败时置健康标记并暴露 UI/查询“审计降级”；失败重试+`request_id` 去重；提供 JSONL→DB 重建/回填工具；或在 getOperationLog 降级时合并 JSONL（至少提示缺行）。

**P1-5 · 恢复前安全备份 ref 秒级可被同秒覆盖，且产品内不可见/不可恢复**
- 位置：`BackupRefService.cs:199-210`（`yyyyMMddHHmmss` + `update-ref` 覆盖语义）、`:54-95`（列表仅 sync）、`:276-327`（restore 白名单仅 sync）。
- 证据：`M2b2BackupRefTests.cs:78-88` 第二次 restore 后 `SafetyRef` 已指向第一次恢复结果（原始 `second` 的安全 ref 被覆盖）；UI 只展示 sync ref。
- 建议：safety ref 加 GUID/毫秒或“同秒递增”避免覆盖；`getBackupRefs`/restore 支持 `refs/backup/restore/*`（或单独 `getRestorePoints`）；定义 restore ref 的保留/清理策略。

### P2（可与 M2b-2b 一起收口）

1. **P2-1 表结构不符合 §8.1 冻结语义**：`CreateTableSql`（`AuditDatabase.cs:89-107`）无 `CHECK` 约束；实际写入 `action=RESTORE_BACKUP`/`CLEANUP_RETENTION`（`HostCommandRouter.cs:510,528`、`RetentionService.cs:25`）不在 `job.kind` 集（`requirements.md:393-395`），§8.2 要求的 `action(CHECK 同 job.kind 集)` 未实现。
2. **P2-2 DB 生命周期健壮性**：任何 `SqliteException` 当损坏（`AuditDatabase.cs:215-221`，含 busy/IO error）；损坏隔离后无恢复/回放入口；迁移无 `VACUUM INTO` 备份（`:486-487` 仅注释）；`Initialize` 异常过滤器不含原生加载/类型初始化异常（`:222-231`），DB 旁路失败可阻断 Host 启动。
3. **P2-3 运行期健康度**：`Available` 恒 true、`Insert=false` 被忽略、UI 无“审计写入失败/数据不完整”提示（与 P1-4 同源，独立记录）。
4. **P2-4 连接架构偏离 §8.2**：每次操作新开池连接，无“1 写连接 + 只读连接池”、无写串行化/WAL checkpoint 策略；WAL/busy_timeout 本身正确。
5. **P2-5 CSV 注入/导出治理**：公式前缀未中和（当前无攻击面）；导出动作不写审计；exports 无保留清理、无显式 ACL 收紧（已含 7 个历史文件，含测试数据）。
6. **P2-6 导出与查询过滤不对称**：UI 只传 format、Host 只支持 from/to → 导出全量；超过 100k 后 UI 导出必失败（`E_TOO_MANY_ENTRIES`）。
7. **P2-7 导/清理内存与响应上限**：JSON 导出整串序列化（100k 行 OOM 风险）、`PruneJsonLines` 全量读入、`limit=1000` 最坏超 2 MB 响应上限。
8. **P2-8 查询超时/快照一致性**：`getOperationLog` 同步 DB 工作不响应 10 s 超时；`COUNT` 与 `SELECT` 非同一快照（total 与 items 可短暂不一致）。
9. **P2-9 备份 ref 列表**：`GetBackupRefsAsync` 单仓失败被丢弃（有 refs 时返回部分列表、无 partial 标志，`HostCommandRouter.cs:801-825`）；`CreatedAt` 取 commit 的 `committerdate`（`BackupRefService.cs:341-343,375-387`），不是 ref 创建时间（可能早于备份/异常）。
10. **P2-10 保留清理细节**：`.trash` 保留期不可配（FR-OP-06/附录 D `delete.retentionDays` 未实现）；backup ref 清理不持 per-repo 锁；`.trash` 清理仅词法 `IsWithin`、无 reparse/junction 校验（`BatchOperationService.cs:1037-1045`）；启动清理 `writeAudit:false`（`HostCommandRouter.cs:111-121`）删除动作无审计。
11. **P2-11 隔离仅靠约定、无守卫**：`TestAudit.NewConfig` 是唯一防线；任何直接 `new AppConfig()` 的测试/harness 会写真实 `%LOCALAPPDATA%`（18:16:49 的 523 行真实导出即旁证）；HostLog 未隔离（真实 `host.log` 5.5 MB 混入测试日志）；`jobs.json` 37 个测试/夹具作业残留。
12. **P2-12 失败注入与回归测试缺口**：见 §6（1–8）。

---

## 8. Top 8（按修复优先级）

1. **P0-1** 恢复 `reset --hard` 静默删除/覆盖冲突 untracked 文件（无预览、无阻断）——数据丢失，必须先修。
2. **P1-2** 备份 ref 丢失分支身份（`<TYPE>` vs SRS `<branch>`）+ 恢复不校验当前分支 → 可把错误分支重置到旧提交。
3. **P1-3** 审计保留下限 1 天，可删除 90 天保留期内审计（FR-OP-09/附录 D 不符）。
4. **P1-4** SQLite 写失败静默丢行、运行期不降级、无 JSONL→DB 回填；实库/JSONL 已分裂。
5. **P1-1** 真实审计库/导出/截图 99% 为测试数据，却作为“真实 521 行”tag 证据（污染来源已定位，当前提交已隔离但未清理/未更正口径）。
6. **P1-5** 恢复前 HEAD 安全备份同秒被覆盖，且 `refs/backup/restore/*` 产品内不可列出/恢复。
7. **P2-2/3/4** DB 生命周期与运行期健康度：损坏误判/无迁移备份/原生加载异常可拖垮启动/写失败无 UI 降级/SRS §8.2 连接架构偏离。
8. **P2-5…8** 导出与查询加固：CSV 公式注入面、JSON 大文件 OOM、导出过滤不对称、超时不可取消与 2 MB 响应上限、exports 无保留清理；同步补 §6 失败注入测试。

---

## 9. 边界与副作用披露（诚实记录）

- **未修改**任何源码、配置、`reqs`、`ui` 源码或真实仓库；唯一新增文件为本报告。
- 评审期间执行了两次 Host 测试（132 子集 + 266 全量）与 Core 71 测试：
  - 测试写 `%TEMP%\MbdGitToolTests\{audit,db,exports,jobs}\*`（临时隔离目录）与真实 `%LOCALAPPDATA%\MbdGitTool\logs\host.log`（HostLog 未隔离，属既有非 hermetic 项，P2-11）；
  - **未**向真实 `db/mbd.db`、`audit/operations.jsonl`、`exports/` 写入测试行：以 18:13 首查 522 行为基线，并行真实 Host 在 10:15:52Z 追加 1 行（id=523）后，本报告的两轮测试复跑前后为 **523→523 行 / 331→331 行 / 7→7 个文件**（该唯一新增行在 `host.log` 记录为并行真实 Host `api cleanupRetention`，非测试进程）。
- 破坏性行为仅在 `%TEMP%\git-untracked-collision-*` 临时仓复现（复现后删除），未触碰任何真实仓库。
- 真实数据取证均为只读（`SqliteOpenMode.ReadOnly` + `Get-Content`），未清理/未修改污染行（避免二次篡改真实审计证据）。
- 评审期间有并行进程驱动真实 Host/测试（`host.log` 混写），本报告已按时间戳/进程行为区分归属；18:16:49 的真实 exports 导出未计入本报告证据，仅作为 P2-11 的旁证。
- 副产物：`%TEMP%\mbd-review-query`（只读查询程序）与 `%TEMP%\MbdGitToolTests` 临时目录，均在仓库外。

---

## 10. 附：关键复跑命令

```powershell
# 测试基线（Release）
dotnet test tests/MbdGitTool.Host.Tests -c Release --no-restore
dotnet test tests/MbdGitTool.Core.Tests -c Release --no-restore

# 只读审计库取证（示例；产物在 %TEMP%，不入仓库）
# DataSource=%LOCALAPPDATA%\MbdGitTool\db\mbd.db; Mode=ReadOnly
# SELECT count(*), max(id) FROM operation_log;
# SELECT action, count(*) FROM operation_log GROUP BY action;
# SELECT component_id, count(*) FROM operation_log GROUP BY component_id;

# untracked 冲突复现（工作区外临时仓；评审后已删除）
git init; git commit 首个含 a.txt 的提交; git rm a.txt; git commit
Set-Content a.txt 'PRECIOUS-UNTRACKED'
git status --porcelain --untracked-files=no   # 空（restore 的脏检查放行）
git reset --hard <首个提交>                    # 退出 0，a.txt 内容被覆盖
```
