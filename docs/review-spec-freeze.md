# 冻结评审（只读）：`reqs/specification.md` v0.4-draft（HEAD `c0f73f3`）

| 项 | 内容 |
|---|---|
| 评审对象 | `reqs/specification.md`：3,264 行 / 406,292 B，SHA256 `FD7B184FC3232BC2F61DECE0846EB6596EB4166428DF0F395EA372CF2C709493`，HEAD `c0f73f3`（工作树 clean，仅未跟踪 review 文档） |
| 本轮基线 | `2f815d0 → c0f73f3`（`git diff --stat`：仅 specification.md，36+/28−；`reqs/adr/*` 与 `.gitignore` 未改） |
| 输入 | `docs/review-spec-r2-complete.md`、`docs/review-spec-r2-testability.md`、`docs/review-spec-r2-arch.md` |
| 方法 | 逐 P0 把三份 r2 的行号断言回落到 `c0f73f3` 实际行号；全文 grep 断链/TBD/枚举/错误码/设置键；以 `git ls-files` + 目录枚举核对证据资产存在性；全量 AC/FR 标题计数；未运行构建/测试 |
| 只读声明 | **未修改任何既有文件**；本文件是本次唯一新增产物 |
| 实际规模（本文件计数） | `#### AC-*` 标题 **176**（174 冻结 + 2 条 v1.1）＋ §13.1.1 表内 AC-UI-01…14 = **188 条冻结 AC**；唯一 FR **104**；AC 域 **12** 个（含 UI）；子字母 AC-UI-10a/10b/10c 已使用 |

---

## 0. 结论（摘要）

> **不可冻结 v0.4，需再修。**
>
> - 三份 r2 报告的 **15 项 P0** 逐条核验：**PASS 2（C-P0-3 baseline、C-P0-4 DELETE_PENDING），FAIL 13**。
> - DoD/证据门禁两项（T-P0-5 与 A-P0-E）同根合并后，**剩余 P0 = 12 项**。
> - 本轮 `c0f73f3` 是**局部修补**而非闭环：已落地 ac-catalog 引用清除、AC-UI-16 新增、AC-REPO-08 失败回滚、§8.1 `RECLONE`/`baseline_ref`、AC-OP-02 标题、R-06 状态声明、DoD manifest 声明、REPAIR 备份改建 `.trash`；但**已删资产引用、规模/编号口径、§8.1 写者/审计枚举、undo journal、G-WIRE 双定义、故障注入点、错误码表、设置键数、DoD 可判定性、M2b 出口**均未闭环。
> - 冻结记录（§5.4）仍为“待填/待评审”，前置条件不可满足。

---

## 1. 三份 r2 报告 P0 逐条核验（评审点 1）

> 判定口径：**PASS** = 该 P0 要求的冻结级修改已同时落在需求文本与可执行 AC 中；**FAIL** = 仍有矛盾/不可判定/已删资产依赖或与冻结文本冲突。行号均为 `c0f73f3`。

### 1.1 `docs/review-spec-r2-complete.md`（C-P0-1…5）

| # | r2 P0 | 判定 | 证据与残留（行号） |
|---|---|---|---|
| **C-P0-1** | 单源化未收尾（外部 AC 目录 + 规模/编号口径） | **FAIL** | 已修：`ac-catalog` 回写口径全部清除（全文 grep `ac-catalog` **0 命中**；L1034/L1068/L1219/L1243 已改为 Annex A 口径）；L1227 单源声明在。**未修**：规模/编号仍是合并前口径——L1310（164 AC/72 FR）、L1540（72 FR/164 AC）、L1563（域固定 11 个）、L1565（164 条“未使用子字母”）、L3034、**L3111（AC=164；OP 34/SAFE 13；无 UI 域）**、L3233（72/72 FR）；而实际为 **188 冻结 AC / 104 FR / 12 域 / 含 AC-UI-10a/b/c 子字母**；§13.1.1 头 L1068 未列 AC-UI-16；§13.1.2 权威范围 L1087/L1089 未含 AC-UI-16。 |
| **C-P0-2** | 已删除验收资产仍被硬依赖 | **FAIL** | 已修：L142（M0 交付）、L146（M3 出口）的 D1–D6 已改为“图集语义并入 §7/§8/§11”；L1311/L1312、L1359 的夹具描述已改。**未修**：L387（“图集 D4 ER…图集侧并行回写”）；L1553（§1.1-5：`fixtures/` 生成器 + `manifest.json` + `expected.json`）；L1652（§1.6 同）；L720（§10.6 M0 交付“状态矩阵夹具生成器”）；L1631/L1899/L1904/L1907/L1987（`expected.json` 表驱动）；L1988（`fixtures/manifest.json`）；**L1998（D3-c/D3-d、`fixtures/state-matrix.ps1`）**；L3236（§5.3-4）；L1281/L1296（`reqs/review-report.md`）、L1302/L1303（`reqs/requirements.md`/review-report）、L1316–L1318（`reqs/archive/*`、docx/drawio）。实际仓库 `reqs/` 仅 `specification.md` + 3 ADR + `fixtures/m2-matrix.json`，无生成器/manifest/expected/D1–D6/state-matrix.ps1；且 L1506“不再依赖已删除的生成脚本”与 L1553/L1652 直接矛盾。 |
| **C-P0-3** | baseline 语义与 AC-OP-12/13、§8.1 主键互斥 | **PASS** | §16.4 L1469–1474（固定 4 行/缺 Tag 行 NULL/同键覆盖/历史落 operation_log）；AC-OP-11③ L2396、AC-OP-12③ L2404、AC-OP-13④ L2412；§8.1 PK L406 未变。仅 P1：L2420⑤ `tag_object_sha` 仍写“可另存”。 |
| **C-P0-4** | DELETE_PENDING 载体/错误码/状态层不一致 | **PASS** | §16.2 L1443–1449（仅文件 `.trash/.pending/*.json`、无 DB 表/列、FAILED+E_DELETE_PENDING、非主状态/聚合态、重扫+四阻断、取消/30 天）；附录 D L1406；AC-OP-23 L2493；FR-OP-06 L526；§7.2/§7.4 未新增状态。 |
| **C-P0-5** | §16 行为缺可执行 AC、DoD 范围错误 | **FAIL** | 已修：**AC-UI-16 已新增 L1165–1172**；AC-REPO-08 补失败回滚 ⑥⑦ L2648；§8.1 `job_item.baseline_ref` L398、`job.kind` RECLONE L397。**未修**：§16.1 L1438 仍写“与 §11.1/§11.6 的入口**需在实现前补 UI AC（AC-UI-16）**”（与已存在自相矛盾）；§11.1 L792 页面清单、§11.6 L925–930 入口/动作清单**无“纳入/移出组件”入口与“已移出”视图**；AC-UI-13 L1084 仍“6/6”；DoD L1521 未列 AC-UI-16；L3084 追溯 FR-OP-12 仅 AC-OP-31/32；§13.4 L1214 FR-OP 仍 `AC-OP-01…32`；**DoD L1521“Annex A 中 AC-OP-01…35”与 AC-OP-33 定义在 §13.1.2（L1173）、Annex A 统计 OP=34（L3111）冲突**；AC-OP-25② L2509 未断言 `baseline_ref` 写入者/迁移口径（P1）。 |

### 1.2 `docs/review-spec-r2-testability.md`（T-P0-1…5）

| # | r2 P0 | 判定 | 证据与残留（行号） |
|---|---|---|---|
| **T-P0-1** | AC-OP-02 磁盘预检不可构造/自相矛盾 | **FAIL** | 标题已改“按仓阻断并汇总”L2319，但 Then① L2324 仍引用**已不存在的旧标题**“任一失败即阻断”；（⑥）改用 `MBD_FAULT_INJECT=DISK_SPACE_LOW` L2324，而 §16.6 固定注入点枚举 L1488 与 AC-OP-35 Given L2581 **均无该注入点** → “预检失败 + E_DISK_FULL + 无目录创建”仍不可确定性构造。 |
| **T-P0-2** | 取消/终止用例不可确定性复现、期望值非法 | **FAIL** | AC-OP-27 依赖 `MBD_FAULT_INJECT=GIT_SLOW` L2523（**不在** L1488 枚举）；Given L2524 仍“其余 **PENDING**”；Then① L2526 仍“job 进入 **CANCELED**”（§8.1 L397 为 `CANCELLED`）；`E_ACTION_NOT_ALLOWED`（L2526）未登记附录 D；AC-SAFE-09 L2748 的 `NOT_APPLIED/UNKNOWN_APPLIED` 仍无 §8.1 列/CHECK 观测落点。已修：AC-OP-26 L2518 已唯一化 `INTERRUPTED`/`QUEUED`，`E_INTERRUPTED` 已清除。 |
| **T-P0-3** | 错误码表未冻结且断言与附录 D 冲突 | **FAIL** | R-06 **自相矛盾**：关闭（L1272、L2341、L3150）vs 仍开放（L2917/L2918“M0 需冻结四列/〔SRS 需修订 R-06〕”、L3245“仅 R-06 仍开放”）；附录 D 头部仍“**错误码（示例，M0 补全表…）**”L1389，仅 24 码且**无可重试性/日志级别列**；AC 中使用但附录 D 未登记的 Host 码 **9+1 个**：`E_ACTION_NOT_ALLOWED`（L1162/L2526）、`E_CONCURRENT_CHANGE`（L1146/L1176）、`E_CREDENTIAL_SCOPE`/`E_GITEA_CONFIG_INVALID`/`E_GITEA_INSECURE_HTTP`（L1138）、`E_INVALID_ARGUMENT`（L1162）、`E_REF_NOT_FOUND`（L1162/L1176）、`E_REPO_BUSY`（L1176）、`E_WORKTREE_OVERWRITE`（L1154，且带“或等价明确错误码”）、`E_NOTHING_TO_COMMIT`（L1095 tooltip 示例）；AC-CFG-05 L1711 401→`CFG-E002` 与附录 D `SYNC-E001`=401/403（L1395）冲突；AC-OP-07 L2364“`OP-E004` 或 git 前缀”、AC-OP-08 L2372“`GIT`/`NET`”仍非唯一。 |
| **T-P0-4** | 设置键冻结表矛盾 | **FAIL** | 附录 D 实有 **38 个唯一键**（L1365–1387，含 `ui.view`/`ui.panel.leftWidth`/`ui.panel.rightWidth`/`tree.hideDotGit`/`tree.defaultExpandDepth`/`tree.pageSize`），而 AC-OPS-07 L2835/L2837 冻结“**32 键/32/32**”、AC-OPS-08 L2843/L2845“键数=32” → 两条冻结 AC 与冻结表互斥。已修：AC-SAFE-07 L2731 已删 `diagnostics.dumpEnabled` 设置键，改用环境变量/构建开关（该子项闭环）。 |
| **T-P0-5** | DoD-1/2 不能由第三方判定 | **FAIL** | 已修：manifest 路径/schema 已声明 L1520。**未修**：① L1520 同一行内含**字面 `\n\n`**（渲染/解析缺陷）；② manifest 路径 `artifacts/m2b-evidence/manifest.json` 落在 **`.gitignore:13` 忽略的 `artifacts/`**，无法按 commit 追溯；③ DoD-1 L1521 AC 范围错误（见 C-P0-5）；④ DoD-2 L1522 仍无 M2b 期望源/消费者/命令（§13.1.2 L1093 只冻结 M2a 口径；`reqs/fixtures/m2-matrix.json` 自身 `status:"aligned"` 与 freezeNotes“SINGLE_SOURCE_PENDING_CODE_ALIGNMENT”矛盾）；⑤ DoD-3 L1523“通过/P0-P1 安全缺陷”判据未定义，`docs/m2b-fault-injection-report.md` 不存在；⑥ 双值 AC 仍存：AC-OP-05 L2349、AC-OP-07 L2364、AC-OP-08 L2372、AC-OP-17 L2444、AC-REPO-06 L2632、AC-REPO-08 L2648⑥（“错误码”未给码值）、AC-SAFE-16 L1154、AC-SAFE-17 L1162。 |

### 1.3 `docs/review-spec-r2-arch.md`（A-P0-A…E）

| # | r2 P0 | 判定 | 证据与残留（行号） |
|---|---|---|---|
| **A-P0-A** | §8.1 未随 D-028/§16 回写（写者/枚举） | **FAIL** | 已修：`job.kind` 增 RECLONE（L397）、`job_item` 增 `baseline_ref`（L398）。**未修**：`component` 唯一写者仍 `STATUS_CALC`（L391，无 `COMPONENT_MGMT`）；`repo` 本地列仍 `LOCAL_SCAN`（L392）与 §16.1 L1436、AC-OP-32② L2566 写 `is_local=0` 冲突；`operation_log.action` 仍“CHECK 同 job.kind 集”（L399），未含 D-028 白名单 `COMPONENT_BIND/COMPONENT_UNBIND/CREATE_UNDO/RECONCILE`；AC-OP-31② L2558 要求“`component` 行写入”但**无 INSERT 写者**；D-028 仍“草案”（`D-028:5`）且未解决 INSERT 归属/`repo.is_local`，并仍把 REPAIR 写成“新增”（`D-028:18`，§8.1 本已有 REPAIR）。 |
| **A-P0-B** | CREATE 撤销的“写前 journal”无载体 | **FAIL** | §16.5 L1480 + AC-OP-25④ L2509 要求 undo 写 STARTED/DONE/FAILED“复用 FR-SAFE-04”，但 FR-SAFE-04 的 journal 实体是 `job_item.pre/expect/post`（AC-SAFE-08 L2734–2739）；undo 不是 job、§8.1 `job.kind` 无 CREATE_UNDO 类 job、无 journal 表 → 架构不可实现。 |
| **A-P0-C** | REPAIR 成功删备份/误删 + AC-REPO-08 无回滚 | **FAIL（部分闭环）** | 已修：备份改建 `.trash/<stamp>/<repoName>.repair-backup` 且保留 30 天（L1455）；AC-REPO-08 ⑥⑦ 已补失败回滚/零改动（L2648）。**未修**：L1454/L1457“可安全保留文件”仍未定义、无“将删除文件”清单/逐文件校验（L1462 确认只展示 `.trash` 路径）；REPAIR 备份名未加类型，与 RECLONE 备份（L1460）共用 `.trash/<stamp>/`，AC-OP-30 L2547 按“最近 stamp”恢复可误取；RECLONE 半成品目录处置仍未定义（L1460）。 |
| **A-P0-D** | G-WIRE 同名两义 + AC-SAFE-04② 不可实现 | **FAIL** | §1.6 L1650：`G-WIRE`=**WireMock.NET REST 契约桩**，“记录全部请求（方法/URL/**头**）”；§16.6 L1489 与 §16.7.1 L1505：`G-WIRE`=**本地 HTTP 代理**，只记 `{method,url,status}`、不记 Authorization → 同一冻结代号两种资产、两种字段集（“记录头”与“不记头值”相反）。AC-SAFE-04② L2706 仍断言“DELETE/PUT/PATCH/POST 方法数=0（仅 GET）”，与 git smart-HTTP 读也走 POST + 附录 D `fetch.beforeWrite=true`（L1375）不可区分；若 G-WIRE 为 REST 桩则根本观测不到 `git.exe` 远端写 → “远端零写”证据不成立。 |
| **A-P0-E** | DoD 与冻结出口冲突、证据门禁可绕 | **FAIL** | manifest 缺陷同 T-P0-5；§3.3 M2b 出口 L145 仍为 `AC-OP-01…32…`（未含 34/35）；冻结 ADR `D-025:95` M2b 出口未改；DoD-5 L1525 声称“D-025 §4 的 M2b 出口按 D-028 增补 AC-OP-34/35（ADR 记录）”，但 **D-028/D-025 均无该记录**（本轮 `git diff` 仅 spec 变更）；§13.4 L1214 追溯未含 34/35（L3083–3095 Annex A 追溯亦无 33/34/35 行）；probe L1495 仍仅匿名 `GET /api/v1/version`（无 token/org 校验），且未禁止“条件 true 断言”。 |

**逐项小结：C-P0-3、C-P0-4 = PASS；其余 13 项 = FAIL。**

---

## 2. 六类残留阻断（评审点 2）

### 2.1 外部已删资产引用 —— **仍有（未清零）**
`reqs/review-report.md`（L1281/L1296）、`reqs/requirements.md`（L1302，且自指“唯一维护源”）、`reqs/archive/*.md`/`*.docx`/`*.drawio`（L1316–L1318）、D1–D6 图集/D4 ER（L387）、`fixtures/` 生成器 +`manifest.json`+`expected.json`（L1553/L1652/L720/L1631/L1899/L1904/L1907/L1987/L1988/L3236）、`state-matrix.ps1`/`D3-c/D3-d`（L1998）——上述资产均不存在（`git ls-files reqs` 仅 5 文件）。

### 2.2 TBD / “二选一” —— **仍有**
- 冻结记录：L3250“（待填）”、L3255 SHA256“待填”、L3256 冻结日期“待填”、L3257 签收人空白、L3258 结论未勾选；§16 标题 L1422“（v0.4-draft，**待评审**）”。
- 附录 D L1389“**错误码（示例，M0 补全表；M0 需补…三列）**”。
- 双期望值仍存：L1154（“或等价明确错误码”）、L1162④/⑦（两码/两路径二选一）、L2349（“转 Repair”或 SKIPPED）、L2364（“OP-E004 或 git 前缀”）、L2372（GIT/NET 前缀）、L2444（“OP_PARTIAL_FAILED 或按清除规则”）、L2632（AHEAD 或 SYNCED）、L2648⑥（“错误码”未给值）。

### 2.3 PENDING/CANCELED 枚举冲突 —— **仍有**
AC-OP-27 Given L2524“其余 **PENDING**”与 Then① L2526“job 进入 **CANCELED**”均不是 §8.1 合法值（L397：`QUEUED`/`CANCELLED`）；L2526 同时把 job_item 固定为 QUEUED/CANCELLED（该子项已修）。AC-SAFE-09 L2748 的 `NOT_APPLIED/UNKNOWN_APPLIED` 无 §8.1 列/CHECK 观测落点。

### 2.4 未登记错误码 / 设置键 —— **仍有**
- 错误码：附录 D 仅登记 24 码（L1393–1416）；AC 中 9+1 个 Host 码未登记（清单见 T-P0-3）；附录 D 仍无“可重试性/日志级别”列。
- 设置键：附录 D 38 键 vs AC-OPS-07/08 冻结 32 键（L2832–2845）；`diagnostics.dumpEnabled` 已移除（该点闭环）。

### 2.5 DoD 不可判定 —— **仍有**
见 T-P0-5/A-P0-E：manifest 路径被 gitignore、L1520 字面 `\n\n`、DoD-1 AC 范围错、DoD-2 无 M2b 消费者/命令、DoD-3 判据与报告缺失、DoD-4 “397/397 基线”无出处、双值 AC 未清零。

### 2.6 SKIP 是否可当 PASS —— **否（已关闭，PASS）**
L1497（L1 不得 SKIP）、L1498（L2 SKIP 只写 `result.json` 且不计真实证据）、L1499（仅 SKIP 不得声明 final/COVERED）、L1524（DoD-4 SKIP 不计 COVERED）、AC-OP-34 L2574①③⑤（runner 区分 PASS/FAIL/SKIP、只 PASS 计 COVERED）——该条不再是阻断项。

---

## 3. §8.1 / D-028 同步核验（评审点 3）

| 项 | D-028/§16 要求 | `c0f73f3` §8.1 现状 | 判定 |
|---|---|---|---|
| **新列** `job_item.baseline_ref` | 新增可空列，写者 JOB_RUNNER（`D-028:12`） | L398 已含 `baseline_ref` | ✅ 已同步 |
| **job kind** `RECLONE` | 新增 `REPAIR`、`RECLONE`（`D-028:18`） | L397 已含 `RECLONE`（REPAIR 原已有） | ✅ 已同步（D-028“REPAIR 新增”措辞仍错） |
| **写者** `COMPONENT_MGMT` | component 两列唯一写者（`D-028:11`） | L391 唯一写者仍 `STATUS_CALC`；AC-OP-31② L2558 的 component 行 INSERT 无写者 | ❌ 未同步 |
| **`repo.is_local` 归属** | 只写 component 两列（`D-028:11`） | L392 本地列=`LOCAL_SCAN`；§16.1 L1436/AC-OP-32② L2566 却要求置 0 | ❌ 三方冲突未改 |
| **审计 action** | 白名单=job.kind ∪ `{COMPONENT_BIND, COMPONENT_UNBIND, CREATE_UNDO, RECONCILE}`（`D-028:19`） | L399 仍“action(CHECK 同 job.kind 集)” | ❌ 未同步 |
| **错误码** | 6 个 Host 码映射 `details.srsCode`（`D-028:20`） | L1406–1411 已登记 6 码；但 AC 另用 9+1 个未登记 Host 码 | ⚠️ 部分同步 |
| **D-028 状态** | 随 v0.4 冻结（`D-028:5` 仍“草案”） | 未改；§16 亦未在正文承载 D-028 的 INSERT/is_local 裁决 | ❌ 未转冻结 |

> 结论：**2 项完全同步、1 项部分同步、5 项未同步** → §8.1/D-028 同步 **FAIL**，冻结后仍会形成“两套 schema”。

---

## 4. 结论与冻结前最小修复集（评审点 4）

### 4.1 结论

**需再修，不可冻结 v0.4。**

- 三份 r2 报告 15 项 P0：**PASS = 2（C-P0-3、C-P0-4），FAIL = 13**；
- 去重（T-P0-5 与 A-P0-E 同根）后 **剩余 P0 = 12 项**：C-P0-1、C-P0-2、C-P0-5、T-P0-1…5、A-P0-A…D。
- 本轮 `c0f73f3`（36+/28−）只完成约 1/3 的 r2 修复面，**“HEAD 已按剩余 P0 修订”不成立**；冻结记录 §5.4 前提（§5.3 第 1/4/13/15 项）仍不可勾选。

### 4.2 冻结前最小修复集（按 P0 根因，修完可复评）

1. **C-P0-1**：统一 AC/FR/域/子字母统计与编号规则（188 冻结 AC / 104 FR / 12 域；AC-UI-10a/b/c 子字母例外）——改 L1310/L1540/L1563/L1565/L3034/L3111/L3233、Annex A 元数据 L1533–L1536、§5.3-1/-15、§13.1.1/§13.1.2 范围声明。
2. **C-P0-2**：删除全部已删资产规范性引用（L387/L720/L1553/L1652/L1631/L1899/L1904/L1907/L1987/L1988/L1998/L3236；附录 A L1281/L1296、附录 B L1302/L1303/L1313/L1316–L1318），统一为 §16.7.1“测试临时目录按附录 C 构造”口径。
3. **C-P0-5**：AC-UI-16 接线——§11.1 页面清单、§11.6 入口/“已移出”视图、AC-UI-13 口径（6/6→6+管理入口）、§13.1.1/§13.1.2 范围、§16.1 L1438、DoD L1521、追溯 L1214/L3084、Annex A 统计。
4. **T-P0-1/T-P0-2**：§16.6 固定枚举补 `DISK_SPACE_LOW`、`GIT_SLOW`（或改 AC 为已枚举注入点）；AC-OP-02 L2324 删旧标题指代；AC-OP-27 改 `CANCELED→CANCELLED`、Given `PENDING→QUEUED`；SAFE-09 的 NOT_APPLIED/UNKNOWN_APPLIED 落 `job_item` 可观测字段/枚举。
5. **T-P0-3/T-P0-4**：真正关闭 R-06（统一 L1272/L2341/L2917–2918/L3245 口径；附录 D 删“示例，M0 补全表”并补可重试性/日志级别/用户文案三列，登记 9+1 Host 码与 `details.srsCode`；唯一化 AC-CFG-05/OP-04/07/08/SAFE-16/17 期望码）；统一设置键数（附录 D 38 键 vs AC 32 键取同一冻结数并同步断言）。
6. **A-P0-A/B**：回写 §8.1（component 写者+INSERT 归属、`repo.is_local` 归属、`operation_log.action` 白名单），D-028 转“随 v0.4 冻结”；给 `undoCreate` 的 journal 定载体（job 或 journal 表）并同步 FR-SAFE-04/AC-SAFE-08/AC-OP-25④。
7. **A-P0-C/D**：定义 REPAIR“可安全保留文件”白名单+逐文件校验+确认框“将删除清单”、备份类型标识、RECLONE 半成品处置；拆分 `G-WIRE`（REST 桩 / git 代理两代号），AC-SAFE-04② 改为服务端 refs/reflog + argv 断言。
8. **T-P0-5/A-P0-E**：manifest 移出 `artifacts/`（或改 `docs/evidence/` 并加例外）、修 L1520 字面 `\n\n`、DoD-1 范围、DoD-2 M2b 期望源/消费者脚本、DoD-3 判据与报告；§3.3 L145 与 D-025:95、§13.4/Annex A 追溯纳入 AC-OP-33/34/35；probe 加 token/org 校验并禁止条件 true 断言。
9. **收尾**：§5.4 冻结记录填版本/SHA256/日期/签收（在 1–8 完成后）。

---

## 5. 复跑断言（只读，可逐条核验）

```powershell
cd D:\Workspace\git_1
git status --short                                        # 仅未跟踪 review 文档；specification.md 未被本次评审修改
git diff --stat 2f815d0 c0f73f3                           # 仅 specification.md 36+/28-
git rev-parse --short HEAD                                # c0f73f3

# 规模/编号（应输出 176 标题 / 104 FR / 12 域；与 L1310/1540/1563/1565/3034/3111/3233 不一致）
$c = Get-Content reqs\specification.md -Raw
([regex]::Matches($c,'(?m)^#### (AC-[A-Z]+-\d+[a-z]?)')).Count
([regex]::Matches($c,'(?m)^\| (FR-[A-Z]+-\d+) \|') | % { $_.Groups[1].Value } | Sort-Object -Unique).Count

# 已删资产/断链（命中即残留）
Select-String -Path reqs\specification.md -Pattern 'ac-catalog|review-report|requirements\.md|reqs/archive|\.docx|\.drawio|D1–D6|D4 ER|D3-c|state-matrix|manifest\.json|expected\.json|fixtures/.*生成器'

# TBD/二选一/枚举
Select-String -Path reqs\specification.md -Pattern '待填|待评审|示例，M0 补全表|二选一|或等价|或 git|GIT/NET|CANCELED|PENDING'

# 错误码/设置键（R-06 自相矛盾；38 键 vs 32 键）
Select-String -Path reqs\specification.md -Pattern 'R-06|32/32|键数=32|E_ACTION_NOT_ALLOWED|E_REF_NOT_FOUND|E_CONCURRENT_CHANGE|E_CREDENTIAL_SCOPE|E_WORKTREE_OVERWRITE|E_REPO_BUSY'

# §8.1 / D-028 / G-WIRE / 注入点 / DoD
Select-String -Path reqs\specification.md -Pattern 'COMPONENT_MGMT|CHECK 同 job.kind|baseline_ref|RECLONE|CREATE_UNDO|DISK_SPACE_LOW|GIT_SLOW|WireMock.NET|本地 HTTP 代理|m2b-evidence|\\\\n\\\\n'
```

## 6. 证据索引（本报告引用）

- 规格：`reqs/specification.md`（`c0f73f3`，3,264 行）
- 三份 r2：`docs/review-spec-r2-complete.md`、`docs/review-spec-r2-testability.md`、`docs/review-spec-r2-arch.md`
- ADR：`reqs/adr/D-028-m2b-schema-enums.md`（全 26 行，`status=草案`）、`reqs/adr/D-025-m2a-m2b.md:95`
- 夹具：`reqs/fixtures/m2-matrix.json`（`status:"aligned"` vs freezeNotes `SINGLE_SOURCE_PENDING_CODE_ALIGNMENT`）
- 其它：`.gitignore:13`（`artifacts/` 被忽略）；`docs/images/reference/main-ui-reference.png`、`docs/images/ui-v3/` 存在；`docs/m2b-fault-injection-report.md`、`docs/m2a-ac-report.md` 不存在（DoD/M2a 证据物待交付）
