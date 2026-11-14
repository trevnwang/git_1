# 需求复评 R2（只读）— 可测试性 / DoD 可判定性

| 项 | 内容 |
|---|---|
| 评审对象 | `reqs/specification.md`（3256 行，v0.4-draft；单文件 = SRS + Annex A AC 目录 + §16 M2b 增量） |
| 基线 | `2f815d0`（`docs(reqs): revise v0.4 spec per three requirements reviews (P0)`）；工作区仅新增评审文档 |
| 上一轮 | `docs/review-spec-testability.md`：P0=8（P0-1 权威链 / P0-2 baseline / P0-3 DELETE_PENDING / P0-4 AC-OP-02 / P0-5 时序注入 / P0-6 外部证据 / P0-7 依赖资产 / P0-8 SKIP+DoD） |
| 本轮范围 | **只评审 `specification.md`**；仅验证：① 8 项修订是否闭环 ② OP-12/23/02、OP-26/27、SAFE-07/09/13 是否仍有矛盾/不可判定 ③ DoD-1…5 可否由第三方按文件复现判定、SKIP 是否仍可冒充 PASS ④ 是否仍有未定义夹具/资产/错误码 |
| 方法 | 逐 AC 解析（175 个 `#### AC-` 头 + AC-UI 表行）、6 字段/枚举/错误码/设置键交叉核对、逐条对拍 §7.6/§8.1/§16/附录 C/D/§13.1.2；仓库资产存在性仅作**辅助核查** |
| 结论 | **需再修（不可冻结）**：P0=5（详见 §5）；修订 8 项中 3 项未闭环（AC-OP-02、确定性故障注入、DoD 判定），2 项部分闭环（F-* 生产口径、单源 AC 残留） |
| 只读声明 | 仅新增本文件；未修改 `specification.md` 与任何其他文件 |

> 判定口径沿用上一轮：**P0** = 第三方按文件仍无法唯一、可复现地判定（规范自相矛盾、期望值不唯一、给定不可构造、证据落点未定义）；**P1** = 断言可判定但缺夹具/命令/文档口径。行号均指 `specification.md`。

---

## 1. 上一轮 8 项 P0 修订闭环验证

| # | 上轮 P0 | 本轮修订声明 | 判定 | 证据（行号）与残留 |
|---|---|---|---|---|
| 1 | P0-1 权威链断裂/单源 AC | 单源 AC：Annex A 为唯一 AC 目录 | **PASS**（残留 P1） | 主线已闭合：`1029`“唯一权威 AC 目录 = 本文件 Annex A”；`1518` Annex A 已并入；`1034`/`1582` 旧号仅映射；`1302` Annex A=权威 AC 目录。**残留 P1**：`1034`、`1068`、`1211–1216` 仍出现“ac-catalog 回写/并行角色回写”表述（与 `1029` 的单源声明冲突）；附录 B `1294` 以 `reqs/requirements.md` 为“唯一维护源（本文件）”（该文件不存在，仅本文件存在）；`1532/3026/3225` 仍写“72 FR / 164 AC”，而正文 §9.13–9.17 有 32 条 UI FR、全文件 175 个 AC 头 + 14 条 AC-UI 表行；Annex A `1555` 规定 AC 域仅 11 个、`1557` 称“164 条均无子字母”，但 `1029` 又把含 `AC-UI-10a/b/c` 的 §13.1.2 纳入 Annex A → AC 基线口径与编号规则自相矛盾 |
| 2 | P0-2 baseline 4 行 | baseline 固定 4 行 | **PASS**（残留 P1） | `1461`“每次 TAG job 对组件固定写 4 行”；`1462–1463` 命中/缺 Tag 行的字段；`1464` 同键覆盖 + 历史落 `operation_log`；`AC-OP-12:2396`③（SIL 行 `tag_present=0`/`commit_sha=NULL`/`tag_object_sha=NULL`，收口事务写 4 行）；`AC-OP-11:2388`③；`AC-OP-13:2404`④；`AC-OP-14:2412`④⑤。上轮“写 4 行 vs 失败/跳过不写”的冲突已消除。**残留 P1**：`1465`“失败 job 不写半套”对“整个 TAG job 失败（四仓全未命中）”是写 4 行全 0 还是 0 行未定死；§16.4 要求 `captured_at`，但 OP-11/12/14 均未断言 `captured_at`（OP-14 仅断言 `tag_object_sha`） |
| 3 | P0-3 DELETE_PENDING 冲突/无断言 | DELETE_PENDING 文件记录 + 重试 | **PASS**（残留 P1） | `1436` 唯一载体 `<workspace>/.trash/.pending/<stamp>-<repoName>.json` + 六字段 + 不入 DB；`1437` `job_item.state=FAILED`/`errorCode=E_DELETE_PENDING`（附录 D `1398` 已登记且 `details.srsCode=FS-E001`）；`1438` 重试前重扫/canonicalize/重跑四阻断；`1439` 取消/30 天；`1440` 审计 result；`AC-OP-23:2483–2486` ①–⑦（目录不动、不删锁、不递归、仅重试 pending、取消、审计）。**残留 P1**：`1439` 的“pending 记录 30 天超期只删记录”与 `1436` 的“启动扫描读取 pending 重建待处理视图”在 AC-OP-23 中**无断言**（`AC-OP-23`⑥ 只覆盖显式取消）；`1438` 要求重试校验“仍在工作区**且非 reparse**”，`AC-OP-23`⑤ 只写“工作区边界校验”，漏 reparse；§16.2 `1441` 自称“超期行为可断言”但无对应 Then |
| 4 | P0-4 AC-OP-02 自相矛盾 | AC-OP-02 按仓隔离 | **FAIL（P0，见 §5 P0-1）** | 已修部分：`2316`①“**按仓隔离**：预检失败仓不入队/不建目录，其余仓继续；整体 `PARTIAL` 而非任一失败全部阻断（标题仅指该仓自身）”，期望值唯一。**未闭环**：① 标题 `2311` 仍为“任一失败即阻断”，与 `2316`① 字面相反（仅靠括注解释，属冻结文本内矛盾）；② `2316`⑥ 允许“`MBD_FAULT_INJECT=DISK_WRITE_FAIL` **或**注入磁盘余量模拟”，但 §16.6 `1480` 的固定注入点枚举**没有磁盘余量/磁盘配额点**，`DISK_WRITE_FAIL` 是“写失败”不能产生 `2316`② 所断言的“**预检失败**+`E_DISK_FULL`+无目录创建”；“注入磁盘余量模拟”无参数/无实现落点 → Given① 无法确定性构造，AC 不可复跑 |
| 5 | P0-5 时序/终止断言无确定性钩子 | 确定性故障注入 | **FAIL（P0，见 §5 P0-2）** | 已修部分：`1479` 双开关（`MBD_FAULT_INJECT` + Debug/Test 符号，Release 忽略+告警）；`1480` 9 个固定注入点（含 `CRASH_AT_ITEM_2`/`CRASH_BEFORE_WRITE`/`CRASH_AFTER_WRITE`/`INDEX_LOCK`/`DIR_MOVE_BUSY`）；`1482` 明确取消/崩溃类 AC 用注入点、不以墙钟唯一断言；`AC-OP-26:2507–2508`、`AC-SAFE-09:2737–2738` 已改用 `CRASH_AT_ITEM_2`；`AC-OP-35:2569–2575` 新增注入矩阵 AC。**未闭环**：`AC-OP-27:2515–2516` 的 Given 依赖“**慢 git 桩**”（不在 `1480` 固定枚举内，也无 `delayMs` 适用点定义）才能让第 2 项“已开始且可取消”→ 取消用例仍无法确定性构造；`2518` 使用 `job.state=CANCELED`（§8.1 `397` 的 CHECK 是 `CANCELLED`）、未开始项 `PENDING`（§8.1 `398` `job_item.state` 只有 `QUEUED`，无 `PENDING`）、“二选一固定并在报告声明”；`E_ACTION_NOT_ALLOWED` 未在附录 D（`1383–1408`）登记。另 `AC-OP-26:2510`、`AC-SAFE-09:2740` 仍保留“二选一 + `E_INTERRUPTED`”，`AC-SAFE-09` 的 `NOT_APPLIED/UNKNOWN_APPLIED` 不在 §8.1 任何 CHECK/字段中（无观测落点） |
| 6 | P0-7 依赖资产不存在 | G-E2E/G-WIRE/F-*/C-SYNTH/CLK 定义 | **PASS**（残留 P1） | `1494–1500` 冻结定义：`G-E2E`=本地 hermetic Gitea/WireMock+临时工作区（不依赖固定 IP）；`G-WIRE`=本地 HTTP 代理记录 `{method,url,status}`；`F-*`=按附录 C 在临时目录构造；`C-SYNTH`=测试代码生成；`CLK`=可注入时钟；附录 C `1319–1345` 给出 25 个夹具行与期望。**残留 P1（冻结门禁内部冲突）**：Annex A `1545`（§1.1-5）、`1644`（§1.6）、§5.3 `3228`（M0 签收第 4 项）、§10.6 `720` 仍要求 `F-*` 由 **M0 `fixtures/` 生成器产出 `manifest.json`+`expected.json`**，且 `AC-STA-01:1979–1980` 依赖 `fixtures/manifest.json`、`AC-STA-02:1990`⑤ 以已删 `fixtures/state-matrix.ps1` 的常量为判据；与 `1498`“由测试在临时目录构造、不再依赖已删除的生成脚本”、附录 B `1304`“其余夹具由测试构造”构成**同一资产两套生产口径**（`AC-STA-02`⑤ 的可执行性依赖不存在的脚本） |
| 7 | P0-8 前半：SKIP=pass + COVERED 无定义 | L1/L2 证据分级、COVERED/PARTIAL/GAP | **PASS** | `1489` L1 不允许 SKIP；`1490` L2 不可达 SKIP 且写 `artifacts/<name>/result.json {skipped:true,url,reason,commit}`，**SKIP 不计入 DoD 真实证据**；`1491` M2b final 至少一次 L2/hermetic Gitea 并归档 manifest，仅 SKIP 不能声明 COVERED；`1492` 汇总区分 PASS/SKIP/FAIL；`AC-OP-34:2566` ①–⑤（runner 区分 PASS/FAIL/SKIP、`result.json` 五字段、只把 PASS 计 COVERED、至少一份 L2/hermetic 记录、仅 SKIP 不得 final）；DoD-4 `1516` 同口径；`1508–1510` COVERED/PARTIAL/GAP 定义。→ **SKIP 已不能冒充 PASS**（本条 PASS）；但“COVERED 证据落点”仍是 P0（见下条） |
| 8 | P0-8 后半：DoD 可判定 + AC-OP-34/35 | COVERED 定义、AC-OP-34/35、DoD 可判定 | **FAIL（P0，见 §5 P0-5）** | AC-OP-34/35 已新增且可执行（`2560–2575`）。但：① `1508` COVERED 要求证据“在 `docs/m2b-ac-op-gap.md` 或**证据 manifest** 中可定位”，而“证据 manifest”在 `1491/1508/2563/2565` 四处出现**均无路径/schema/生成者**（AC-OP-34 的 `result.json` 是“每个 gate target”的结果，不是 AC→证据台账）；② DoD-2 `1514` 要求“M2b 构建复验 14×5 矩阵 + `m2-matrix.json` 与 Host/UI 一致”，但 §13.1.2 `1093` 只定义 **M2a 构建**的夹具覆盖，§7.6 `376` 的该条也只讲 M2a 覆盖，**没有 M2b 期望源/消费者脚本/命令**；③ DoD-1 `1513` 又被 §5 P0-1/2/3/4 及 8 条含“或/二选一”的 AC（OP-05:2341、OP-07:2356、OP-17:2436、OP-26:2510、OP-27:2518、REPO-06:2624、REPO-08:2640、SAFE-09:2740）阻塞 → DoD-1 不能由第三方判定 |

**8 项小结**：PASS 5（#1/#2/#3/#6/#7，其中 #1/#2/#3/#6 带 P1 残留）、**FAIL 3（#4 AC-OP-02、#5 确定性注入、#8 DoD）**。

---

## 2. 指定 AC 的残留矛盾/不可判定

| AC | 判定 | 结论与行号 |
|---|---|---|
| **AC-OP-12** 切 Tag 缺 Tag | **通过** | `2394` 明确 SIL 仓 `SYNCED`、`2396`① SKIPPED、② `TAG_DRIFT`、③ **固定 4 行**（`tag_present=0`/NULL）；与 §16.4 `1461–1463`、附录 C `F-TAG_DRIFT:1340`、R-03（`3139`）一致。无阻断性残留 |
| **AC-OP-23** DELETE_PENDING | **通过（残留 P1）** | 文件记录/四阻断重试/取消/审计均已断言（`2485`①–⑦），错误码与附录 D `1398` 一致。P1：pending 30 天超期清理与启动重建视图无断言；重试校验漏“非 reparse”（`1438` vs `2485`⑤） |
| **AC-OP-02** Clone 预检 | **不通过（P0-1）** | 标题 `2311` 与 `2316`① 字面相反；`2316`⑥ 的磁盘注入两选项均不能产生 `2316`② 的“预检失败/E_DISK_FULL/无目录创建”（§16.6 `1479–1480` 无磁盘余量注入点） |
| **AC-OP-26** 任务中心/重启 | **基本通过（残留 P1）** | `2507–2508` 已改为 `CRASH_AT_ITEM_2` 确定性终止、`2510`⑥ 声明无需真实 kill；P1：`2510`① `INTERRUPTED` 或 `FAILED+E_INTERRUPTED` 二选一（`E_INTERRUPTED` 未登记附录 D），报告未指定；`2507` 的“任务 DB/JSON”观测源未定义 |
| **AC-OP-27** 取消语义 | **不通过（P0-2）** | `2515–2516` 依赖未冻结的“慢 git 桩”；`2518` 使用非法枚举 `CANCELED`/`PENDING`、保留“二选一”、`E_ACTION_NOT_ALLOWED` 未登记 → Given 不可构造、Then 不唯一 |
| **AC-SAFE-07** 崩溃转储 | **基本通过（P0-4）** | 双分支已收敛为 v1 唯一口径“默认关闭”，`2721` 定义 `dumps` 根=`%LOCALAPPDATA%\MbdGitTool\dumps\`，`2723`② 限定最近 8 个/≤256MB。**P0**：`2723`① 引入设置 `diagnostics.dumpEnabled`，附录 D（`1355–1379`）无此键，而 `AC-OPS-07:2829`④ 明令“不得出现附录 D 无定义的设置项” → 两条冻结 AC 互斥（DoD-1 要求 AC-SAFE-07 COVERED）；P1：保留策略的触发时点（启动/定时）未定义 |
| **AC-SAFE-09** 崩溃 reconcile | **基本通过（残留 P1）** | `2737–2738` 已改为 `CRASH_AT_ITEM_2`（写前 STARTED 已落盘、git 写未发生），`2740`⑤ `CRASH_AFTER_WRITE` 另测；核心“不得标成功”可判定。P1：`2740`② `INTERRUPTED` 或 `FAILED+E_INTERRUPTED` 二选一（`E_INTERRUPTED` 未登记）；`NOT_APPLIED`/`UNKNOWN_APPLIED` 不在 §8.1 任何 CHECK/列，未给观测落点（违反 Annex A §1.1-2 的 6 类观测源纪律） |
| **AC-SAFE-13** Token header-only | **通过（残留 P1）** | `2769–2772` 已固定采集方法（G-WIRE 只记 `method,url,status` 不记 Authorization 值 + `Get-CimInstance Win32_Process` argv 快照 + remote URL/.git/config 扫描）、产物（`artifacts/security/gwire.jsonl|argv.txt|remotes.txt`）、不要求抓包；上轮“无采集方法/CI 不可复跑”已关闭。P1：`2772`④ “无 `sslVerify=false`”未指定采集源（三件产物中 argv/G-WIRE 不必然覆盖 git config 配置读回）；① 的“布尔认证送达记录”未给字段名/落点 |

---

## 3. DoD-1…5 第三方可复现判定 / SKIP

| DoD | 原文要点（行） | 第三方可否判定 | 问题 |
|---|---|---|---|
| **1** | `1513` 指定 AC 全部 `COVERED`（PARTIAL/GAP 不计） | **否（P0-5）** | ① COVERED 证据落点“证据 manifest”无路径/schema（`1508`，全文仅名词）；② 被 AC-OP-02（P0-1）、AC-OP-27/26/SAFE-09（P0-2）、AC-OP-04（P0-3）、AC-SAFE-07（P0-4）阻塞；③ 另被 8 条含“或/二选一”的必测 AC 阻塞（OP-05:2341、OP-07:2356、OP-17:2436、OP-26:2510、OP-27:2518、REPO-06:2624、REPO-08:2640、SAFE-09:2740） |
| **2** | `1514` M2b 构建复验 14×5；`m2-matrix.json` 与 Host/UI 一致 | **否（P0-5 内）** | 无 M2b 期望源/消费者/命令：§13.1.2 `1093` 只冻结 **M2a 构建**；§7.6 `376` 只规定 M2a 覆盖、M2b“恢复本矩阵”但未给 M2b 期望文件与复跑入口；辅助核查：`reqs/fixtures/m2-matrix.json` 自述 `status:"aligned"` 与 `freezeNotes` “SINGLE_SOURCE_PENDING_CODE_ALIGNMENT…代码对齐完成并复跑后才可改状态”矛盾，且 `buildVariant` 已写 M2b（M2a 覆盖说明仍在） |
| **3** | `1515` 故障注入报告通过、无 P0/P1 安全缺陷 | **部分可以（P1）** | `1483` 定义了报告路径与逐注入点“期望/实测/证据”，可判定；但 §16.6 未定义“通过”判据与“P0/P1 安全缺陷”的定级表，且 `AC-OP-35:2575`⑤ 的 N/A 说明不构成判据 |
| **4** | `1516` 单元/集成全绿（397/397）、L1 必跑、L2/hermetic 证据归档、SKIP 不计 | **部分可以（P1）** | 无门禁命令/范围/产物路径（全绿=跑哪些命令？L2 归档到哪个 manifest？）；SKIP 语义已正确（见下）；“当前 397/397 基线”未给出处/commit |
| **5** | `1517` 独立 Gate（需求/代码/UX）签字：报告文件+结论+日期+commit | **部分可以（P1）** | 字段已给，但未定义三份报告的文件名/模板/签收角色（原“签字物缺失”只部分闭环） |
| **SKIP 是否仍可当 PASS** | — | **否（已关闭）** | `1489–1492` + `AC-OP-34:2566` + DoD-4 `1516` 三重约束：L1 不得 SKIP；L2 SKIP 只写 `result.json` 且不计真实证据；OUTPUT 必须区分 PASS/SKIP/FAIL；只有 SKIP 不得标 M2b final |

---

## 4. 仍未定义的夹具 / 资产 / 错误码 / 枚举

> 按 Annex A `/1.1-2`（Then 必须落在 6 类可程序读取观测源）与 `/1.1-5`（Given 必须引用夹具 ID）核对。

### 4.1 夹具与资产

1. **OP/REPO/SAFE 域 Given 未引用任何冻结夹具代号**（仅依赖列写 `G-E2E`/`F-*`）：Annex A 内 58 条中 **47 条**（如 AC-OP-01/02/03/04/06/08/09/11…、AC-REPO-01/03/05、AC-SAFE-01/02/03…），违反 Annex A `1545` + `1575`“Given 必须能由脚本一键盘起”。
2. **只写描述性夹具、附录 C 无对应行**：`AC-OP-14:2409`“annotated tag 夹具”、`AC-OP-18:2441`“untracked 夹具”、`AC-OP-19:2449`“本地独有分支夹具”、`AC-OP-20:2457`“本地仓夹具”、`AC-OP-21:2465`“stash 夹具”、`AC-SAFE-01:2671`“untracked/stash”、`AC-SAFE-02:2680`“DB+FS 快照”、`AC-STA-03:1995`“随机提交图生成器”。
3. **F-* 生产口径双源**：`1545`/`1644`/`3228`/`720`（M0 `fixtures/` 生成器 + `manifest.json` + `expected.json`）vs `1498`/`1304`（测试临时目录构造、不依赖生成脚本）；`AC-STA-01:1980` 依赖 `fixtures/manifest.json`、`AC-STA-02:1990`⑤ 依赖已删 `fixtures/state-matrix.ps1`。
4. **AC-UI-16 被引用但未定义**：`1430`“需在实现前补 UI AC（AC-UI-16）”；全文无 AC-UI-16 定义（辅助核查也存在其他评审文档，但 spec 内无）。
5. **矩阵夹具自身状态矛盾**（辅助核查，非评审对象）：`reqs/fixtures/m2-matrix.json` `status:"aligned"` vs `freezeNotes`“SINGLE_SOURCE_PENDING_CODE_ALIGNMENT”；另 §13.1.2 `1093` 的 M2a 覆盖（DIVERGED.PULL 禁用、EMPTY.PUSH 禁用）与该文件 `buildVariant:"M2b"` 并存，DoD-2 无 M2b 期望源。

### 4.2 错误码（附录 D `1383–1408` 未冻结）

1. **R-06 仍开放**：`3142`“仍开放（M0 出口条件）：附录 D 错误码全表…于 M0 冻结时补齐并回写 AC-OP-04/23、AC-OPS-17” → 冻结 AC 仍引用未冻结表。
2. **AC-OP-04 期望码不唯一/不成立**：`2332`“错误码前缀 `GIT`（M0 错误码表冻结，见 §4.2 R-06）”；附录 D `1407` `GIT-E001` 仅表示“空仓库/unborn”，`1399` `E_AUTH_FAILED=401/403`、`1400` `E_REMOTE_NOT_FOUND=404`（srsCode=NET-E001）→ 断言“GIT 前缀”与冻结表冲突。`AC-OP-07:2356`“`OP-E004` 或 git 前缀”、`AC-OP-08:2364`“`GIT`/`NET`”同样非唯一。
3. **同一事件两码**：`AC-CFG-05:1703` 401 → `CFG-E002`，而附录 D `1387` `SYNC-E001`=401/403 → 冲突。
4. **Host `E_*` 未登记附录 D（无 `details.srsCode` 映射）**：§13.1.2 使用但附录 D 未列的 9+1 个：`E_CREDENTIAL_SCOPE`（`1138`）、`E_GITEA_CONFIG_INVALID`（`1138`）、`E_GITEA_INSECURE_HTTP`（`1138`）、`E_CONCURRENT_CHANGE`（`1146`）、`E_WORKTREE_OVERWRITE`（`1154`）、`E_REF_NOT_FOUND`（`1162`）、`E_INVALID_ARGUMENT`（`1162`）、`E_ACTION_NOT_ALLOWED`（`1162`/`2518`）、`E_REPO_BUSY`（`1168`）、`E_INTERRUPTED`（`2510`/`2740`）。其中 `AC-SAFE-16:1154` 还写“`E_WORKTREE_OVERWRITE`（**或等价明确错误码**）”、`AC-SAFE-17:1162`④ 写“`E_ACTION_NOT_ALLOWED` **或** `E_INVALID_ARGUMENT`” → M2a 出口 AC 期望值本身不唯一。

### 4.3 设置键/枚举/DDL 未回写

1. **设置键数量矛盾**：附录 D 实有 **38** 个唯一键（`1355–1379`），而 `AC-OPS-07:2827`/`AC-OPS-08:2837` 冻结“**32 键**”并断言 32/32、缺键即失败；AC 列表漏 `ui.view`、`ui.panel.leftWidth`、`ui.panel.rightWidth`、`tree.hideDotGit`、`tree.defaultExpandDepth`、`tree.pageSize` 共 6 键。
2. **`diagnostics.dumpEnabled`**（`2723`）未在附录 D 登记（见 P0-4）。
3. **DDL/枚举未随 §16 回写**：`1445` 要求 `job.kind` 增 `REPAIR`、`RECLONE`，但 §8.1 `397` 的 CHECK 只有 `REPAIR`（无 `RECLONE`，且 §16.3 称“新增 REPAIR”而 §8.1 本有 REPAIR）；`1470`/`AC-OP-25:2501` 断言 `job_item.baseline_ref`，§8.1 `398` `job_item` 无此列；`1424` 的 `COMPONENT_MGMT` 写者不在 §8.1 唯一写者列；`AC-OP-27:2518` 的 `PENDING`、`AC-SAFE-09:2740` 的 `NOT_APPLIED/UNKNOWN_APPLIED` 均不在任何 CHECK 集。
4. **ADR D-028 引用**：`1424/1445/1470` 引“ADR D-028”以改 §8.1，但本文件 §0 ADR 表仅 D-001…D-025（`39–65`），且 `reqs/adr/*` 自述不是需求源（`3`）→ 冻结 AC 所依赖的字段/枚举在需求单源内不可见（`AC-OP-25` 属 DoD-1 必测）。

---

## 5. 结论：需再修（不可冻结），P0 = 5

### 5.1 P0 清单（逐项可复核）

| # | P0 | 证据（行号） | 影响 |
|---|---|---|---|
| **P0-1** | **AC-OP-02 磁盘预检仍不可构造且自相矛盾**：`2316`⑥ 允许 `DISK_WRITE_FAIL` 或“注入磁盘余量模拟”，但 §16.6 `1479–1480` 固定注入点无磁盘余量/配额点，`DISK_WRITE_FAIL` 产生写失败而非“预检失败”，无法得到 `2316`② 的 `E_DISK_FULL`+无目录创建；另标题 `2311` 仍“任一失败即阻断”与 `2316`① 相反 | `2311`、`2316`、`1479–1480` | DoD-1 的 AC-OP-02 不可执行/不可 COVERED |
| **P0-2** | **取消/终止用例仍不可确定性复现且期望值非法**：AC-OP-27 依赖未冻结“慢 git 桩”（`2515–2516` vs `1480`），`2518` 使用 `CANCELED`/`PENDING`（§8.1 `397–398` 为 `CANCELLED`/`QUEUED`）、保留“二选一”、`E_ACTION_NOT_ALLOWED` 未登记；AC-OP-26 `2510`、AC-SAFE-09 `2740` 仍“二选一 + `E_INTERRUPTED`”，SAFE-09 的 `NOT_APPLIED/UNKNOWN_APPLIED` 无观测落点 | `2515–2518`、`2510`、`2740`、`397–398`、`1480` | DoD-1 的 OP-26/27、SAFE-09 不可判定 |
| **P0-3** | **错误码表仍未冻结且断言与附录 D 冲突**：R-06 仍开放（`3142`）；AC-OP-04 `GIT` 前缀（`2332`）与附录 D `1387/1399/1400/1407` 冲突；AC-CFG-05 401→`CFG-E002`（`1703`）vs `SYNC-E001`（`1387`）；9+1 个 Host `E_*` 无附录 D 映射 | `3142`、`2332`、`1703`、`1383–1408`、`1138/1146/1154/1162/1168` | DoD-1 的 AC-OP-04/AC-OPS-17 不可判；M2a 出口 AC 期望码不唯一 |
| **P0-4** | **设置键冻结表矛盾**：附录 D 实有 38 键 vs AC-OPS-07/08 冻结“32 键/32/32”（缺 6 键 `ui.view`、`ui.panel.*`、`tree.*`）；AC-SAFE-07 `2723` 引入附录 D 未登记的 `diagnostics.dumpEnabled`，而 AC-OPS-07 `2829`④ 禁止表外设置项 → 两条冻结 AC 互斥 | `1355–1379`、`2827`、`2837`、`2829`、`2723` | DoD-1 的 AC-SAFE-07 不可 COVERED；M0 设置键冻结不可签收 |
| **P0-5** | **DoD-1/2 不能由第三方判定**：COVERED 依赖未定义“证据 manifest”（`1508`；`1491/1508/2563/2565` 均只有名词）；DoD-2 无 M2b 期望源/消费者/命令（`1514` vs `1093`/`376`）；DoD-1 另被 8 条“或/二选一”AC 与 P0-1…4 阻塞 | `1508`、`1513`、`1514`、`1093`、`376`、`2341/2356/2436/2510/2518/2624/2640/2740` | DoD 不可判定，M2b 出口无客观判据 |

### 5.2 P1 清单（不阻断复评结论，但冻结前应一并修）

1. 单源 AC 残留：`1034/1068/1211–1216` “ac-catalog 回写”；附录 B `1294/1302` 陈旧路径/计数；AC 基线口径 `1532/3026/3225/1557`（72 FR/164 AC/无子字母）vs 实际 104 FR、175 AC 头+14 表行、AC-UI 子字母。
2. baseline 残留：`1465`“失败 job 不写半套”对“整个 TAG job 失败”的 4 行/0 行口径；`captured_at` 未被 OP-11/12/14 断言。
3. DELETE_PENDING 残留：pending 30 天超期与启动重建视图无断言；重试漏“非 reparse”（`1438` vs `2485`⑤）。
4. F-* 生产口径双源（`1545/1644/3228/720` vs `1498/1304`；`AC-STA-02:1990`⑤ 依赖已删 `state-matrix.ps1`）→ §5.3 M0 签收第 4 项与 §16.7.1 冲突。
5. DDL/枚举未回写：§8.1 `397/398/391–406` 缺 `RECLONE`、`job_item.baseline_ref`、`COMPONENT_MGMT` 写者；AC-UI-16 未定义（`1430`）；ADR D-028 未纳入 §0 表。
6. AC-OP-26/SAFE-09 残留二选一与 `E_INTERRUPTED`；SAFE-09 的 `NOT_APPLIED/UNKNOWN_APPLIED` 无观测落点。
7. SAFE-13 采集细节：`sslVerify=false` 的采集源、G-WIRE“布尔认证记录”字段未定义。
8. 双期望值 AC（非 DoD-1 专属）：`AC-SAFE-16:1154`（“或等价明确错误码”）、`AC-SAFE-17:1162`④（两码二选一）、`AC-OP-05:2341`、`AC-OP-07:2356`、`AC-OP-17:2436`、`AC-REPO-06:2624`、`AC-REPO-08:2640`。
9. DoD-3/4/5 判据细化：故障报告“通过”/P0-P1 定级、测试门禁命令与产物路径、三份 Gate 报告文件名/模板。
10. 辅助核查（非 spec 内容）：`docs/m2b-fault-injection-report.md` 当前不存在（DoD-3 交付物）；`reqs/fixtures/m2-matrix.json` 的 `status` 与 `freezeNotes` 自相矛盾；`reqs/fixtures` 仅矩阵夹具；`ui/scripts/run-selfchecks.mjs` 需按 `AC-OP-34` 改造。

### 5.3 建议最小修改（按优先级）

1. 定义磁盘余量注入点（如 `DISK_SPACE_LOW`，或明确“预检使用可注入的磁盘余量 provider”），并删除 `AC-OP-02` 标题与 `2316`① 的字面矛盾（标题改“单仓失败即该仓阻断”）。
2. 给 `AC-OP-27` 增加确定性“慢 git/暂停点”注入点（纳入 §16.6 固定枚举并定义 `delayMs` 语义），把 `CANCELED/PENDING` 改为 §8.1 合法枚举，取消二选一（报告声明改为冻结唯一值）。
3. 关闭 R-06：附录 D 补全 Host `E_*`→`srsCode` 映射与四列（码/文案/可重试性/日志级别），回写 AC-OP-04/07/08、AC-CFG-05、AC-OPS-17、AC-SAFE-14…17、AC-OP-33。
4. 统一设置键表：附录 D 与 AC-OPS-07/08 取同一冻结键数（38/32 二选一），登记 `diagnostics.dumpEnabled`。
5. 定义“证据 manifest”路径/schema 与 `docs/m2b-ac-report.md` 模板；指定 M2b 矩阵期望文件与消费者命令；给 DoD-3/4/5 补执行命令、报告文件名与签收模板。
6. 清理 P1：ac-catalog 残留句、计数/编号口径、F-* 生产口径、§8.1 回写 D-028 新列/枚举、剩余“或/二选一”AC。

---

### 附：核查方式（只读）

- 解析 `^#### (AC-[A-Z]+-[0-9]+[a-z]?)` 得 175 个 AC 头（CFG 12 / SYNC 14 / SCAN 13 / STA 15 / BOARD 16 / DETAIL 10 / OP 35 / REPO 11 / SAFE 17 / OPS 17 / CI 11 / UI 4），另 AC-UI-01…14 以表行存在；OP 编号 01–35 无缺号。
- 逐 AC 校验 Given 夹具代号、Then“或/二选一”、枚举字面与 §8.1 CHECK、错误码与附录 D、设置键与附录 D 全表（38 键）比对。
- 资产存在性为辅助核查（`git ls-files`/目录枚举），不作为本次评审对象；未运行构建/测试。
