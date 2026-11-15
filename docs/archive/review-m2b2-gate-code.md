# M2b-2a 数据/安全 Gate 复评报告（提交 `cbdfa78`）

| 项目 | 内容 |
|---|---|
| 评审对象 | 提交 `cbdfa78f35b0dff3003ea06807eb413f900c8bf4`（`fix(m2b2a): restore untracked safety, branch/safety-ref hardening, audit isolation and retention floor`，2026-11-14 18:36:54 +0800，父提交 `1371f51`）；被评 diff 仅 5 个文件：`docs/review-m2b2-code.md`（+329，上一轮评审报告）、`src/MbdGitTool.Host/Services/{AppConfig,BackupRefService,OperationAuditService}.cs`、`tests/MbdGitTool.Host.Tests/M2b2BackupRefTests.cs` |
| 评审范围 | 核对上一轮 `docs/review-m2b2-code.md`（P0=1/P1=5）的闭环：① P0-1 恢复 untracked/ignored 阻断/文件保留/HEAD 不变/tracked dirty 仍 `E_DIRTY_WORKTREE`；② P1 逐项（审计测试隔离、审计保留 ≥90、安全 ref 唯一/可恢复/可见性、备份 ref 分支身份、SQLite/JSONL 分歧/回填）；③ EMPTY 首推、导出/查询、retention 不误删、全量测试 337/337 与 0 warning、M2a/M2b-1 不退化；**只评审，不改代码/真实仓库** |
| 输入 | `docs/review-m2b2-code.md`、`docs/m2b-status.md`、SRS `reqs/specification.md`（FR-OP-05/09/11、FR-SAFE-01、附录 D）、`reqs/specification.md（Annex A）`（AC-OP-30）、ADR `reqs/adr/D-025-m2a-m2b.md`；代码 `BackupRefService/OperationAuditService/AuditDatabase/AppConfig/RetentionService/BatchOperationService/HostCommandRouter`、DTO、`M2b2*Tests.cs`、`TestInfrastructure.cs`、`ui/src/**` |
| 环境 | Windows 11 / git 2.56.1 / .NET SDK 10.0.404 / 工作区 `D:\Workspace\git_1`。评审开始时 `HEAD=cbdfa78`、工作区干净；**评审期间并行工作流将 HEAD 推进到 `b967d73`（cbdfa78 的子提交，仅 `ui/src/**` + `docs/review-m2b2-ux.md`，后端 C# 与 cbdfa78 逐字节一致）**，本报告结论对 cbdfa78 后端成立（§7 披露）。真实 Host PID 67380（Debug，18:36:58 启动）由并行的真实 Host 检查驱动（未由本报告杀/驱动） |
| 方法 | ① `git show cbdfa78` 逐行静态核对（后端行号均对该提交；UI 行号见 §7 注）；② 全量 `dotnet test -c Release` 实跑 + `dotnet build -t:Rebuild` 警告计数；③ **工作区外临时 git 仓 + `ProjectReference` 探针**（`%TEMP%\mbd-gate-restore`）黑盒复现 restore 三阻断/文件保留/HEAD 不变/安全 ref 唯一性/可见性，及 retention 下限与不误删；④ 只读查询真实 `%LOCALAPPDATA%\MbdGitTool\db\mbd.db`（`SqliteOpenMode.ReadOnly`）、JSONL、exports 并比对测试前后行数；⑤ 核对真实 Host `host.log` 与审计行归因；⑥ 与 SRS/AC/ADR 逐条对照 |
| 边界 | 未修改任何源码/配置/`reqs`/`ui`/真实仓库/真实用户数据；构建产物仅落 gitignored `bin/obj`；探针工程与复现仓均在 `%TEMP%`；真实审计库全程只读；未提交任何 commit（HEAD 推进本轮非本报告所为）。副作用与并行归属见 §7 |

---

## 0. 结论（判决）

> **不建议在 `cbdfa78` 打 `m2b-2a` tag。**
> **P0-1 已闭环**（代码 + 单测 + 工作区外黑盒复现 + 真实 Host 现场命中三阻断）；**P1-3 已闭环**；**P1-1 的“测试隔离”子项 PASS、但“污染清理/证据口径更正”未做**；**P1-2（备份 ref 分支身份）、P1-4（SQLite 写失败降级/JSONL→DB 回填）完全未动**；**P1-5 的“同秒覆盖”已闭环，但“产品内可见/可恢复/保留清理”仍未闭环**。
> 与上一轮建议的 tag 前置条件（“P0-1 与 P1-1…P1-4 闭环、且状态报告证据口径更正后可考虑 M2b-2a 阶段 tag”）相比，当前仍差 P1-1（证据口径）、P1-2、P1-4 三项，**故本轮不打 tag**。
> **M2b-2b（journal/reconcile、离线降级完整行为、备份恢复 UI 全链路 CDP 证据、审计测试隔离清理、AC-OP 全量证据）未完成，M2b 不完整、M2/M2b 均不得宣称完成。**

| 汇总口径 | 结果 |
|---|---|
| 全量测试 | **337/337 通过**：Host **266/266**、Core **71/71**（0 失败/0 跳过；Host 2 m 52 s，并行 testhost 拖慢）；与状态报告一致 |
| 构建 | **0 warning / 0 error**（`dotnet build MbdGitTool.sln -c Release -t:Rebuild`） |
| P0（上一轮） | **0 未闭环**：P0-1 闭环（见 §2） |
| P1（上一轮 5 项） | **闭环 1**（P1-3）、**部分闭环 2**（P1-1 隔离子项、P1-5 唯一性子项）、**未闭环 2**（P1-2、P1-4）；另 P1-1 证据口径、P1-5 可见/可恢复为未闭环子项（见 §3） |
| 是否建议 tag | **否（不打 `m2b-2a` tag）**；若产品裁量把 P1-2/P1-4 明确写成 M2b-2b 挂账并更正 `m2b-status.md:54-55` 证据口径，可再复评 |

---

## 1. 评审基线与可复跑证据

1. **受评对象确定**：`git show cbdfa78 --name-status` 仅 5 个文件（见头部表），上一轮 P0/P1 的其余代码（`AuditDatabase.cs`、`BatchOperationService.cs`、`HostCommandRouter.cs`、`ui/**`）在本提交**零改动**——这直接决定 P1-2/P1-4 的“未闭环”判定（见 §3.3/§3.5）。
2. **全量测试实跑**（Release，未改源码）：
   ```powershell
   dotnet test tests/MbdGitTool.Host.Tests -c Release --no-restore -v minimal
   # 已通过! - 失败: 0，通过: 266，已跳过: 0，总计: 266，持续时间: 2 m 52 s
   dotnet test tests/MbdGitTool.Core.Tests -c Release --no-restore -v minimal
   # 已通过! - 失败: 0，通过: 71，已跳过: 0，总计: 71（85 ms）
   dotnet build MbdGitTool.sln -c Release --no-restore -t:Rebuild -v m
   # 已成功生成。0 个警告 0 个错误（7.25 s）
   ```
3. **真实数据只读基线（评审时刻）**：`db/mbd.db` `count=528`、`max(id)=528`、`max(ts)=2026-11-14T10:45:14.7839701Z`；JSONL **336** 行；`exports/` **11** 个文件；`jobs.json` 37 个作业。`operation_log` 仍以测试/开发数据为主：`CF100A_Demo=370`、`NULL(CLEANUP_RETENTION)=107`、`MOCK_CF1001_*`=16、`CF100B_Slow`=14、`CF999_NotThere`=8、路径穿越夹具 `..\..\evil`=6；真实 Host 行共 **7** 行（`RTCT_Demo`：2×RESTORE_BACKUP 旧行 + 5×10:43–10:45Z 新行）。
4. **真实 Host 并行归因**：评审期间真实 Host（PID 67380）被并行 UX 真实 Host 检查驱动，`10:43:54–10:45:14Z` 新增 **5 行**（`524` OK RESTORE_BACKUP/IMPL、`525/527` FAILED `E_DIRTY_WORKTREE`/SIL、`526/528` FAILED `E_WORKTREE_OVERWRITE`/MIL），`host.log` 同一秒有对应 `api restoreBackupRef ok=False code=…`（`10:44:41/44/10:45:11/14Z`）——**这组真实数据同时证明 cbdfa78 的 P0-1 阻断在真实 Host 生效**（§2.2）。
5. **测试隔离实测**：以 18:43 后真实库 528 行为基线，本轮 Host 全量 + Core 全量跑完后真实库 **528→528**（`maxid`/`maxts` 不变）、JSONL **336→336**、exports **11→11**；`host.log` 测试窗口（`10:55–10:59Z`）内 126 条“审计库已就绪”**全部**指向 `%TEMP%\MbdGitToolTests\db|audit|m2b2-audit\…` 或 `%TEMP%\gate-restore-*\db\mbd.db`，**0 条**指向真实 `%LOCALAPPDATA%\MbdGitTool\db`。
6. **黑盒探针**：`%TEMP%\mbd-gate-restore`（仓库外临时工程，`ProjectReference` 引用 `MbdGitTool.Host`）在 `%TEMP%\gate-restore-<guid>\repo` 临时仓复现，未触碰任何真实仓库；命令与输出见 §8。
7. **HEAD 变化披露**：评审中途并行工作流提交 `b967d73`（18:52:01，cbdfa78 的子提交），改动仅 `ui/src/stores/workbench.ts`、`ui/src/components/overlays/ModalHost.vue`、新增 `docs/review-m2b2-ux.md`；后端 C# 无差异，故 §2/§3 的后端行号与结论对 cbdfa78 仍准确；该提交顺手修掉了 cbdfa78 的 UI 确认文案缺口（§5-P2-5）。

---

## 2. 判据 1：P0-1 恢复 untracked/ignored 数据丢失 —— **闭环（PASS）**

### 2.1 代码路径

| 核对项 | 证据（cbdfa78） | 判定 |
|---|---|---|
| tracked dirty 仍阻断 | `BackupRefService.cs:171-190`：`git status --porcelain --untracked-files=no` 非空 → `E_DIRTY_WORKTREE`（errors 含 `dirtyFiles` 计数），在 reset 前返回 | **PASS** |
| untracked/ignored 阻断 | `:192-213`：`git status --porcelain --untracked-files=normal --ignored=matching` 非空 → **`E_WORKTREE_OVERWRITE`**（`BridgeErrors.cs:35`），errors 含 `untrackedFiles` 计数、`requiresCommit=false`，文案“请先移走后重试（未改动任何文件）” | **PASS** |
| 阻断发生在任何写操作之前 | 顺序：ref 校验（`:157-167`）→ tracked 检查 → untracked 检查 → 读 preHead/建 `refs/backup/restore/*`（`:215-234`）→ `reset --hard`（`:236-245`）。阻断时**不建安全 ref、不 reset** | **PASS**（黑盒：安全 ref 数 1→1） |
| 错误码/文案可达 UI | `HostCommandRouter.cs:507-524` 失败路径把 `BridgeError` 原样返回并写 1 行 `RESTORE_BACKUP` FAILED 审计（`:507-521`）；UI `executeRestoreBackupRef` 对 `ok=false` 不显示成功（`workbench.ts:2914-2919`） | **PASS** |
| 测试更新 | `M2b2BackupRefTests.cs:137-151` 由 `Restore_UntrackedFileIsPreserved` 改为 `Restore_UntrackedFile_IsBlockedAndPreserved`：断言 `result=null`、`error.Code=E_WORKTREE_OVERWRITE`、文件内容原样；`:117-134` 保留 tracked dirty 断言（HEAD/文件不变） | **PASS** |

### 2.2 动态证据（工作区外临时仓，本报告独立复现）

| 场景 | 结果 |
|---|---|
| 对照：干净工作区 restore | 成功，HEAD→备份 SHA，生成 1 个 `refs/backup/restore/20261114110014540-28fe72c5/IMPL` |
| **P0 原始场景：untracked `a.txt` 与备份树冲突**（备份树含 `a.txt`，当前 HEAD 不含，工作区 `a.txt=PRECIOUS-UNTRACKED`；旧代码会 `reset --hard` 覆盖） | `E_WORKTREE_OVERWRITE`；`restored=null`；**HEAD 不变**；`a.txt` 内容仍为 `PRECIOUS-UNTRACKED`；安全 ref 数 **1→1（未新建）**；errors：`ref=…;untrackedFiles=1;requiresCommit=False` |
| **ignored 文件**（`.gitignore: *.log`，`x.log=IGNORED-KEEP`，`status --ignored=matching` 显示 `!! x.log`） | `E_WORKTREE_OVERWRITE`；HEAD 不变；`x.log` 原样保留 |
| tracked dirty（`README.md` 未提交改动） | `E_DIRTY_WORKTREE`；HEAD 不变；文件内容原样保留 |
| 真实 Host 现场（并行 UX 检查触发） | `operation_log` #525/#527 `E_DIRTY_WORKTREE`（RTCT_Demo/SIL）、#526/#528 `E_WORKTREE_OVERWRITE`（RTCT_Demo/MIL），`host.log` 时间戳一一对应；#524 为成功恢复 IMPL |

**本项小计：上一轮唯一 P0 闭环——阻断、文件保留、HEAD 不变、tracked dirty 语义保持，且阻断不产生副作用（安全 ref/工作区零改动）。** 残留 P2 见 §5-P2-1（策略过宽：所有非冲突 untracked/ignored 一并阻断；错误只回计数不回文件清单，AC-OP-30 ③/FR-SAFE-01 的“untracked 清单/冲突预览”仍未满足）。

---

## 3. 判据 2：上一轮 P1 逐项状态

| 编号 | 上一轮问题摘要 | 本轮状态 | 一句话依据 |
|---|---|---|---|
| P1-1 | 真实审计库/导出/截图 ~99% 为测试数据却当作“真实 521 行”证据 | **PARTIAL**：测试隔离 PASS；污染清理/证据口径 **OPEN** | 本轮实测测试零新增真实行；但 `docs/m2b-status.md:54-55` 未改、真实库/exports 未清理未标注、无 CI 守卫 |
| P1-2 | 备份 ref 丢失分支身份（`<TYPE>` 而非 SRS `<branch>`），恢复不校验当前分支 | **OPEN（未改）** | cbdfa78 未触及 `BatchOperationService.cs`/ref 解析；SRS `requirements.md:521`、AC-OP-30 `ac-catalog.md:1015-1021` 仍为 `<ts>/<branch>`，无 ADR 变更 |
| P1-3 | 审计保留可配到 <90 天（< FR-OP-09 / 附录 D） | **CLOSED（配置路径）+ P2 残留** | `AppConfig.cs:79,152` 下限 90，生产构造均走 `EffectiveAuditRetentionDays`；黑盒 env=1/7/30→90、120→120、5000→3650；残留见 §5-P2-4 |
| P1-4 | SQLite 写失败静默丢行、运行期不降级、无 JSONL→DB 回填/对账 | **OPEN（未改）** | `OperationAuditService.cs:233` 仍忽略 `Insert` 返回值；`AuditDatabase.cs:142,210,217,228` 运行期不置 `Available=false`；全仓无 backfill/reconcile；真实库 528 vs JSONL 336 分歧仍在 |
| P1-5 | 安全 ref 秒级且可被同秒覆盖；产品内不可列出/恢复 | **PARTIAL**：唯一性 CLOSED；可见/可恢复/清理 **OPEN** | `BackupRefService.cs:223-224` 改 `yyyyMMddHHmmssfff-<guid8>`（黑盒 3 次恢复得 3 个不同 ref）；但列表仍只 `refs/backup/sync`（`:357-397`）、restore 白名单仅 sync（`:300-351`），安全 ref 拒绝恢复、无保留清理 |

### 3.1 P1-1 · 审计测试隔离 / 污染与证据（PARTIAL）

**（a）测试隔离：PASS。**
- 机制：`TestAudit.NewConfig` 注入 `DatabasePath`/`ExportsRoot`（`TestInfrastructure.cs:332-348`，该注入在父提交 `1371f51` 已存在）；`ResolveDatabasePath/ResolveExportsRoot` 优先使用显式路径（`OperationAuditService.cs:77-113`）。
- 本轮实测：真实库 **528→528**（`maxid=528`、`maxts=10:45:14.783Z` 不变）、JSONL **336→336**、exports **11→11**；测试窗口 `host.log` 的 126 条“审计库已就绪”全部为 `%TEMP%` 路径（见 §1.5）。**测试不再新增真实审计行成立。**
- 注：cbdfa78 新增的 `OperationAuditService.ConfigureTestIsolation`（`:62-64`）**全仓无任何调用点**（grep 仅命中定义），属**未被接线的死代码**；提交说明“audit test isolation override to temp DB/exports”与实际不符——真正的隔离来自父提交的 `TestAudit.NewConfig`（遗留 P2-3）。

**（b）污染清理 / 证据口径更正：OPEN。**
- `docs/m2b-status.md:54-55` 仍写“**521 行真实可查** / **真实 521 行导出成功**”，未更正为“真实 Host 约 7 行、其余为测试/开发数据”。
- 真实库 528 行中约 **521 行**为测试夹具/清理数据（`CF100A_Demo=370` 等，§1.3）；`exports/` 11 个文件同样含测试数据；上一轮指出的截图 `docs/images/m2b/audit-log.png`（显示“共 522 条”）未被替换/标注。
- 未清理、未标注、无 CI 真实路径零新增守卫；`m2b-status.md:62` 仍把“审计测试隔离清理”挂到 M2b-2b——**即作者自己也承认尚未完成**。
- 结论：上一轮 P1-1 的“证据链/数据完整性”诉求**未闭环**（测试隔离本身已在父提交成立）。

### 3.2 P1-2 · 备份 ref 分支身份（OPEN）

- 现状（cbdfa78 未改）：SYNC 备份 ref 仍为 `refs/backup/sync/<14 位 UTC>/<REPO_TYPE>`（`BatchOperationService.cs:210-216`，`BackupRefPrefix` + 大写仓类型）；`TryParseBackupRef` 只接受该格式（`BackupRefService.cs:300-351`）；`RestoreAsync` 直接对**当前分支** `reset --hard <ref>`（`:236-245`），不校验当前分支与备份来源分支；`BackupRefDto` 无 branch 字段（`BackupDto.cs`）。
- 需求：FR-OP-05 `requirements.md:521`、AC-OP-30 `ac-catalog.md:1015-1021` 均为 `refs/backup/sync/<ts>/<branch>`，且 AC-OP-30 ② 要求“恢复后本地分支包含那 2 个 commit”；`reqs/adr/` 无格式变更 ADR。
- 风险不变：用户切到分支 B 后恢复分支 A 的备份 → B 指针被重置到 A 的旧提交；审计/UI 无 branch 信息提示。
- 判定：**OPEN**（未做任何代码/文档变更；直接阻断 tag）。

### 3.3 P1-3 · 审计保留 ≥90 天（CLOSED）

- `AppConfig.cs:79` `EffectiveAuditRetentionDays` 与 `:152` `Load` 均为 `ClampDays(..., min: 90, max: 3650)`；`AppConfig.DefaultAuditRetentionDays=90`。
- 生产路径全部使用 Effective：`HostCommandRouter.cs:102`、`JobEngine.cs:101` → `OperationAuditService.cs:66-72` → 清理 cutoff 用 `_auditRetentionDays`（`:389-391`）；`RetentionService.cs:78-84` 的日志/行为同样引用 Effective。
- 黑盒：`MBD_AUDIT_RETENTION_DAYS=1/7/30/90` → `AuditRetentionDays=90/Effective=90`；`120`→120；`5000`→3650。
- 清理不误删：`M2b2AuditDatabaseTests.cs:307-345` 断言仅删 91 天 DB+JSONL 行、保留近期行；本报告探针在隔离临时库插入 91 天旧行 + 近期行后 `CleanupExpired()` → `删除 1 行 / 近期保留 / 剩 1 行`；真实 Host 启动日志 `保留清理完成：… auditDb=0/jsonl=0（90 天）`（18:37:01，无超期可删）。
- P2 残留（§5-P2-4）：`OperationAuditService.cs:137` 构造器仍把参数夹在 `1..3650`（配置路径不可达，但公共构造器可传 <90）；`AppConfig.cs:58` XML 注释仍写“范围 1–3650”（陈旧）；单测只覆盖 0/99999，未断言 30→90。

### 3.4 P1-4 · SQLite 写失败降级 / 双写分歧 / 回填（OPEN）

- 代码零改动（cbdfa78 diff 不含 `AuditDatabase.cs`，`OperationAuditService` 仅加隔离死代码）：
  - 写序 JSONL→SQLite 非原子；`Database?.Insert(...)` **返回值被丢弃**（`OperationAuditService.cs:230-257`；`AuditDatabase.Insert` 失败仅 WARN 返回 false，`:235-277`）。
  - `Available` 仅在 `Initialize` 置位/清零（`AuditDatabase.cs:142,210,217,228`）；运行期写失败不会置不可用，UI 仍显示可用。
  - 查询/导出只读 SQLite（`OperationAuditService.cs:280-292,324-333`），DB 不可用直接 `E_INTERNAL`；无 JSONL→DB 回填/重建/`request_id` 去重/重试队列（全仓 grep 无 backfill/reconcile/重放）。
  - 分歧仍未收敛：真实库 **528** 行 vs JSONL **336** 行（SQLite 多出的主要是 M2b-2a 的测试行，如 `CF100A_Demo` 370 行在 JSONL 无对应）。
- 判定：**OPEN**——按上一轮口径属“复评前应闭环 P1-4”；若决定随 M2b-2b journal/reconcile 收口，必须书面挂账并更正状态报告，否则 tag 前置条件不成立。

### 3.5 P1-5 · 安全 ref 唯一性 / 可恢复 / 可见性（PARTIAL）

- **唯一性：CLOSED。** `BackupRefService.cs:223-224` 安全 ref = `refs/backup/restore/<yyyyMMddHHmmssfff>-<guid8>/<TYPE>`，`update-ref` 不再同秒覆盖。黑盒连续 3 次恢复得到 3 个互不相同的安全 ref（`20261114110014540-28fe72c5` / `…110017307-40b61f5a` / `…110017918-de791e0d`），各自指向正确“恢复前 HEAD”；旧测试 `M2b2BackupRefTests.cs:78-88` 只断言第二次 safetyRef 指向第一次恢复结果，**未断言两个 ref 名不同**（旧代码下同样通过）→ 回归测试缺口 P2-2。
- **可见/可恢复：OPEN。** `getBackupRefs` 只列 `refs/backup/sync`（`BackupRefService.cs:357-397`），`restoreBackupRef` 白名单只接受 sync（`:300-351`）。黑盒：`GetBackupRefsAsync` 返回 1 条 sync ref、`containsRestoreRef=false`；用安全 ref 调 `RestoreAsync` → `E_INVALID_ARGUMENT`。安全 ref 也不在任何保留/清理策略内（`CleanupExpiredAsync` 只扫 sync，`:270-295,413-465`）→ 只增不减。
- 判定：**PARTIAL**。唯一性风险（旧 P1-5 的“同秒覆盖”数据丢失）已闭环；可见/可恢复按上一轮结论“可随 M2b-2b 一并收口”，但截至目前仍未收口。

---

## 4. 判据 3：EMPTY 首推 / 导出查询 / retention / 全量测试 / 不退化

| 核对项 | 证据 | 判定 |
|---|---|---|
| EMPTY 首推矩阵 | `RepoActionService.Matrix` `EMPTY="NYCYY"`（PULL/FETCH/CHKOUT/COMMIT/PUSH；PUSH 允许且 `RequiresConfirm=true`）；单源夹具 `reqs/fixtures/m2-matrix.json:128-155`：`EMPTY.PUSH.enabled=true, requiresConfirm=true, reasonCode=CONFIRM_REQUIRED`；`RepoActionAvailabilityMatrixTests` 逐格断言 | **PASS** |
| 首推建 upstream / unborn 零远端写 | `M2b2EmptyPushTests.cs:40-68` 断言 `push -u` 后 `@{upstream}=origin/main` 且 bare 远端 `refs/heads/main`=HEAD；`:71-95` unborn 返回 `E_REF_NOT_FOUND + requiresCommit` 且 bare `for-each-ref` 为空（网络 git 启动前拒绝）；`:97-122` NO_UPSTREAM 同样建 upstream | **PASS**（全量测试内实跑通过） |
| 提交说明“M2b matrix fixture normalized” | `git show cbdfa78 --name-status` **无** `reqs/**` 变更；夹具在 `1371f51` 已正确 | 文案级 P2-3（不影响行为） |
| 导出/查询 | `M2b2AuditDatabaseTests.cs:226-306`（过滤/分页/非法参数、CSV/JSON 落 `ExportsRoot`、无 token）、`M2b2RouterApiTests.cs:73-108,222-281`；参数只有 `format/from/to`（`BridgeParams.cs:103-106`），导出目录固定 `ExportsRoot`，CSV 转义/BOM/JSON 无密钥字段 | **PASS**（未改动，测试通过；既有 P2 不变） |
| retention 不误删 | 见 §3.3（91 天删/近期留、启动日志 0 删除、`.trash`/backup ref 30 天边界单测 `M2b2BackupRefTests.cs:169-253`） | **PASS** |
| 全量测试 / 构建 | Host 266/266 + Core 71/71 = **337/337**，0 失败 0 跳过；Release Rebuild **0 warning / 0 error** | **PASS** |
| M2a / M2b-1 不退化 | cbdfa78 仅改 3 个生产文件：`AppConfig`（审计保留下限，M2a/M2b-1 不读取该值）、`BackupRefService`（M2b-2a 专属）、`OperationAuditService`（仅新增未接线的静态隔离字段/分支）；未改 M2a 单仓五键/安全四件/CAS 相关代码与 M2b-1 job/批量代码；全量套件（含 M2a/M2b-1 全部既有测试）337/337 通过 | **PASS（套件级）**；本轮未复跑 M2a/M2b-1 的真实 Host CDP 全链路，若 Gate 要求真实 E2E 需另跑 |

---

## 5. 本轮发现的 P2 残留（不改变“不打 tag”的判定，但建议随 M2b-2b 收口）

1. **P2-1 阻断过宽且缺冲突预览**：untracked/ignored **无条件**阻断（含与备份树无冲突的文件；单测 `M2b2BackupRefTests.cs:137-151` 的 `keep-me.txt` 即被阻断），且错误只回 `untrackedFiles` 计数、不回文件清单；不检查 merge/rebase 进行中；不校验“备份 ref 已过期/远程已变动”。与 AC-OP-30 ③（冲突预览）/FR-SAFE-01（untracked 清单）仍有差距；真实 Host 现场 MIL 仓即被 ignored/untracked 阻断。安全方向正确（fail-closed），但可用性/AC 证据不完整。
2. **P2-2 回归测试缺口**：安全 ref 唯一性无断言（见 §3.5）；安全 ref 可见/可恢复/清理无测试；restore 的 `update-ref`/`reset --hard` 失败注入仍不可测（`GitCli` sealed，旧 P2-12 不变）。
3. **P2-3 提交说明与 diff 不符（证据卫生）**：本次提交说明的“M2b matrix fixture normalized”“audit test isolation override to temp DB/exports”在 diff 中分别不存在/为死代码（§3.1a、§4）；且评审报告 `docs/review-m2b2-code.md` 与修复被混在同一提交。
4. **P2-4 保留下限的防御纵深/文档**：`OperationAuditService.cs:137` 仍 `Math.Clamp(1,3650)`；`AppConfig.cs:58` 注释“范围 1–3650”已陈旧；单测未覆盖 30/7→90。
5. **P2-5 UI 联动不足（cbdfa78 时点）**：cbdfa78 的恢复确认文案（`workbench.ts:2879`，旧文本）未提示“需无已跟踪改动/无未跟踪/忽略文件”，与新的阻断行为错位；`ui/src/viewmodel.ts` `ACTION_ERROR_MAP` 无 `E_WORKTREE_OVERWRITE` 提示（仅通用错误 toast）。**注：该文案缺口已由评审期间落地的子提交 `b967d73` 修复（当前 `workbench.ts:2889`），错误码提示仍未补。**
6. **P2-6 既有导出/查询/DB 生命周期项不变**（不计入本轮回归）：查询/导出仅 SQLite 无 JSONL 合并、JSON 导出整串序列化、count 与 select 非同一快照、DB 损坏分类过宽、无 1 写连接+只读池等——详见上一轮报告 §2.3/§5.1，本轮无代码变化。
7. **P2-7 M2b-2b 未完成**：journal/reconcile、离线降级完整行为、备份恢复 UI 全链路 CDP 证据、审计测试隔离清理、AC-OP 全量证据（`docs/m2b-status.md:62` 自述）——M2b 不完整。

---

## 6. 结论与 tag 建议

**建议：不打 `m2b-2a` tag。**

- **P0 已清零**：上一轮唯一 P0-1 已由代码 + 单测 + 黑盒 + 真实 Host 四重证据闭环，这是本轮最重要的正向结果。
- **但 P1 未清零**：按上一轮报告自身给出的 tag 前置条件（P0-1 与 P1-1…P1-4 闭环、状态报告证据口径更正），当前：
  - P1-3 闭环；P1-1 仅“隔离”子项闭环（污染清理/证据口径未做）；P1-5 仅“唯一性”子项闭环；
  - **P1-2（备份 ref 分支身份）与 P1-4（SQLite 写失败降级/JSONL→DB 回填）零改动**；
  - 故不满足“P1-1…P1-4 闭环”的 tag 门槛。
- **M2b-2b 未完成**（journal/reconcile、离线降级、完整 AC 证据等，见 P2-7），**M2b 不完整；M2 总出口 = M2a ∧ M2b，不得宣称 M2/M2b 完成**。
- 复评前的最小闭环建议（按优先级）：
  1. **P1-4**：`Insert` 失败→健康标记/查询降级提示；`request_id` 去重；JSONL→DB 回填/对账（与 M2b-2b journal/reconcile 合并设计可接受，但需先在 `m2b-status.md` 与本 Gate 明确挂账，而非声称“已修”）。
  2. **P1-2**：补 ADR 把 ref 格式正式改为 `<ts>/<TYPE>` 并在恢复时校验/展示当前分支与备份来源分支（或实现 `<ts>/<branch>` 兼容迁移）。
  3. **P1-1(b)**：清理/标注真实库与 exports 中的测试行，更正 `m2b-status.md:54-55` 证据口径，补 CI 真实路径零新增守卫（或明确以 M2b-2b 清理任务收口）。
  4. **P1-5(b)**：`getBackupRefs`/恢复入口支持 `refs/backup/restore/*`（或单列 restore points）+ 安全 ref 保留清理策略；补唯一性回归测试。
  5. P2-1/P2-4/P2-5 可随 M2b-2b 或后续小修收口（建议至少补 `E_WORKTREE_OVERWRITE` 的 UI 提示与冲突文件清单）。
- 若产品裁量决定先打“阶段 tag”，建议在 tag 说明中逐条列出未闭环 P1-1(b)/P1-2/P1-4/P1-5(b) 及 M2b-2b 清单，并**不得**把 `m2b-2a` 表述为“数据/安全 Gate 通过”。

---

## 7. 边界与副作用披露（诚实记录）

- **未修改**任何源码、配置、`reqs`、`ui` 源码或真实仓库/真实用户数据；唯一新增文件为本报告（`docs/review-m2b2-gate-code.md`）。期间存在并行产物 `docs/review-m2b2-gate-ux.md`（未跟踪，非本报告所为）。
- **HEAD 变化**：评审开始时 `HEAD=cbdfa78`、`git status` 干净；评审中途并行工作流提交 `b967d73`（cbdfa78 子提交，18:52:01，仅 UI 2 文件 + `docs/review-m2b2-ux.md`）。本报告 §2/§3 后端结论以 cbdfa78 diff 为准（后端零差异），UI 文案缺口已注明由 b967d73 修复。**本报告未执行任何 git 写操作（无 add/commit/checkout）**。
- **真实数据只读**：`mbd.db` 全程 `SqliteOpenMode.ReadOnly` 查询（临时查询工具 `%TEMP%\mbd-gate-query`）；JSONL/exports/jobs 仅读取行数/文件名，未清理/未修改。
- **并行归因**：真实库 523→528 的 5 行新增（`10:43:54–10:45:14Z`）来自并行真实 Host 检查（host.log 对应），**不计入**本报告测试副作用；本报告两轮全量测试期间真实库 **528→528**、JSONL **336→336**、exports **11→11**。
- **测试副作用**：测试写 `%TEMP%\MbdGitToolTests\{audit,db,exports,jobs,m2b2-audit}\*` 临时目录；**写真实 `%LOCALAPPDATA%\MbdGitTool\logs\host.log`（HostLog 未隔离，属既有 P2-11 非 hermetic 项，非本轮新增）**；未写真实 `db/mbd.db`、`audit/operations.jsonl`、`exports/`（已以行数/文件数前后比对证明）。
- **探针副作用**：`%TEMP%\mbd-gate-restore`（工程）、`%TEMP%\gate-restore-*`（临时 git 仓，含自建 backup/restore ref）、`%TEMP%\mbd-gate-query`、`%TEMP%\gate-m2b2-*.log`；均在仓库外，评审后保留供复跑，不触碰真实仓库。
- 构建产物写入 gitignored 的 `src/**/bin|obj`、`tests/**/bin|obj`（Release）；未改动任何受版本控制文件。

---

## 8. 附：关键复跑命令

```powershell
# 1) 全量测试 / 构建（Release；本报告实测 266+71=337、0 warning）
dotnet test tests/MbdGitTool.Host.Tests -c Release --no-restore -v minimal
dotnet test tests/MbdGitTool.Core.Tests -c Release --no-restore -v minimal
dotnet build MbdGitTool.sln -c Release --no-restore -t:Rebuild -v m

# 2) 真实审计库只读取证（临时工具 %TEMP%\mbd-gate-query；不写库）
& "$env:TEMP\mbd-gate-query\bin\Release\net11.0\query.exe"
#   -> count=528 maxid=528 maxts=2026-11-14T10:45:14.7839701Z
#   -> action/component 分布；id>=520 的真实 Host 行
(Get-Content "$env:LOCALAPPDATA\MbdGitTool\audit\operations.jsonl" | Measure-Object -Line).Lines   # 336
(Get-ChildItem "$env:LOCALAPPDATA\MbdGitTool\exports" -File).Count                                # 11

# 3) 隔离证明：测试窗口内“审计库已就绪”全部为 %TEMP% 路径（0 条真实路径）
Select-String -Path "$env:LOCALAPPDATA\MbdGitTool\logs\host.log" -Pattern '审计库已就绪' |
  Where-Object { $_.Line -match 'T10:5[5-9]' } | ForEach-Object { $_.Line }

# 4) P0-1 / 安全 ref / 保留下限黑盒探针（仓库外临时工程；创建 %TEMP%\gate-restore-* 临时仓）
Push-Location "$env:TEMP\mbd-gate-restore"; dotnet build -c Release -v q; .\bin\Release\net10.0-windows\gate-restore.exe; Pop-Location
#   关键输出：untracked 冲突/ignored/tracked dirty 三场景 errorCode + head_unchanged=True + 文件保留；
#   clean restore 成功；3 个安全 ref 互异；getBackupRefs 只含 sync ref、restore 安全 ref=E_INVALID_ARGUMENT；
#   env 1/7/30→90、120→120、5000→3650；91 天行删除、近期行保留。

# 5) 上一轮报告关键锚点（cbdfa78）
git show cbdfa78 -- src/MbdGitTool.Host/Services/BackupRefService.cs      # :192-213 E_WORKTREE_OVERWRITE、:223-224 安全 ref 新 stamp
git log --oneline -3                                                       # cbdfa78 父 1371f51；评审期间 HEAD=b967d73
```
