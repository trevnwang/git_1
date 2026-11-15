# §16（M2b 增量）复评 R2：9 项 P0 闭环核验 / 架构不可实现面 / 证据门禁

| 项目 | 内容 |
|---|---|
| 评审对象 | `reqs/specification.md` §16「M2b 增量与完成定义（v0.4-draft）」：`L1414–1517`；关联 §8.1/§8.2（`L385–417`）、附录 D（`L1353–1409`）、Annex A §1.6（`L1638–1649`）、AC-OP-05/11…14/23/25/26/31/32/34/35、AC-REPO-08、AC-SAFE-04/08/13、AC-UI-13；对照 `reqs/adr/D-028-m2b-schema-enums.md`（全 26 行）、`reqs/adr/D-025-m2a-m2b.md` §2.3/§4 |
| 上轮基线 | `docs/review-spec-arch.md`：P0=9（P0-1…P0-9），结论「需先修」 |
| 方法 | ①把 §16 每条“已修订”表述落回具体行号，与 D-028/§8.1/AC 三处对读；②只按“架构不可实现 / 会误删误写 / 泄露 token / 验收不可判定”四类反推；③把 DoD 与已冻结出口（§3.3、D-025 §4）对读；④按冻结级决策与实现设计分级 |
| 边界 | **只读评审**：未改 `reqs/`、代码、配置、真实仓库/用户数据；本文件是本次唯一新增产物 |
| 结论 | **需再修，不可冻结**：9 项中 **4 项闭环、5 项未闭环**；残余 **P0=5**、P1=14、P2=7 |

---

## 0. 判决

> 本轮修订**方向正确且质量明显提升**：§16 已把组件管理写成“管理状态≠聚合态”、LOCAL_DELETE 显式复用 DELETE 强确认、baseline 固定 4 行/同键覆盖、DELETE_PENDING 只落文件并强制重扫+四阻断、故障注入限定 Debug/Test 构建+固定枚举、SKIP 不计 COVERED 并新增 AC-OP-34/35、附录 D 补 6 个 Host 码。上轮“需求把冻结级决策下推实现”的问题大部分已收回。
>
> **但仍不能冻结，原因是 5 类“冻结后即固化”的缺陷：**
>
> 1. **数据模型出现两套口径（P0-A）**：§16 三处声称“已加入 §8.1/由 ADR 修订 §8.1”（`L1424/L1445/L1470`），但 §8.1 实体表**一字未改**（`L391/L397/L398/L399`）；且 `unbind` 要写 `repo.is_local`（`L1428`、AC-OP-32② `L2558`）与 §8.1「repo 本地列由 LOCAL_SCAN 写」（`L392`）、D-028「COMPONENT_MGMT 只写两列」（`D-028:11`）三方冲突；新纳入组件行的 INSERT 写者无人认领（AC-OP-31② `L2550` 要求“component 行写入”）。
> 2. **CREATE 撤销的“写前 journal”没有落点（P0-B）**：§16.5 要求 undo 写 STARTED/DONE/FAILED“复用 FR-SAFE-04”（`L1472`、AC-OP-25④ `L2501`），但 FR-SAFE-04 的 journal 实体是 `job_item.pre/expect/post`（AC-SAFE-08 `L2726–2731`），而 `undoCreate` 不是 job、D-028 新增 kind 只有 REPAIR/RECLONE（`D-028:18`）→ 无表可写，架构不可实现。
> 3. **REPAIR 成功路径会删掉原目录内容（P0-C）**：`L1447` 先把原目录内容移到 `<repo>.repair-backup`、**成功后删除临时目录**，而“可安全保留文件”未定义（`L1447/L1449`）、未要求“将丢失什么”清单/内容等价校验；同时 §16.3 声称验收含“失败回滚”（`L1456`），但 AC-REPO-08（`L2635–2641`）无任何回滚断言。
> 4. **G-WIRE 同名两义，“远端零写”证据仍不成立（P0-D）**：Annex A §1.6 定义 G-WIRE=WireMock.NET **Gitea REST 契约桩**、记录“方法/URL/头”（`L1642`）；§16.6/§16.7.1 定义 G-WIRE=**本地 HTTP 代理**、只记 `{method,url,status}`、不记 Authorization（`L1481/L1497`）——同一冻结代号两种资产、两种字段集（且 §1.6 的“记录头”与“不记 header 值”直接相反）。AC-SAFE-04② 又断言“DELETE/PUT/PATCH/POST 方法数=0”（`L2698`）：若代理真能看到 `git.exe`，`fetch` 的 smart-HTTP 本身就是 POST，且附录 D `fetch.beforeWrite=true`（`L1367`）要求删除前强制 fetch → 断言不可能成立；若 G-WIRE 只是 REST 桩（§1.6 口径），则根本观测不到 git 远端写 → 上轮 P0-7 的证据问题原样保留。
> 5. **DoD 与已冻结出口冲突，证据门禁可被绕开（P0-E，本轮新查）**：§16.9 把 AC-OP-33/34/35 + 证据 manifest 纳入 DoD（`L1513、L1516`），但 §3.3 M2b 出口（`L145`）与**冻结 ADR D-025 §4（`D-025:152`）** 的 M2b 出口 AC 只到 `AC-OP-01…32`，不含 34/35；而 §3.3 明写“本表与 ADR 冲突时以 ADR 的最新决策为准”（`L152`）→ 仍可按旧出口宣称 M2b 完成、跳过证据门禁。

---

## 1. 9 项 P0 闭环核验（逐项）

> 判定口径：**PASS** = 上轮要求的“冻结级修改”已在需求文本+可判定 AC 中同时落地；**FAIL** = 仍有不可实现/可误删误写/验收不可判定/与冻结文本冲突。行号均指 `reqs/specification.md`。

| # | 上轮 P0 | 本轮判定 | 已落地的证据（行号） | 未闭环残余 |
|---|---|---|---|---|
| P0-1 | 组件管理写者/迁移 | **FAIL** | 管理状态≠聚合态、默认迁移、COMPONENT_MGMT 写者：`L1422–1424`；AC-OP-31③ 默认迁移 `L2550` | ① §8.1 `component` 唯一写者仍只写 `STATUS_CALC`（`L391`），未登记 COMPONENT_MGMT；② `unbind` 要求写 `repo.is_local=0`（`L1428`、AC-OP-32② `L2558`）与 §8.1 `LOCAL_SCAN`（`L392`）、D-028「只写两列」（`D-028:11`）冲突；③ 新纳管（未克隆）组件的 component 行由谁 INSERT 未定义（D-028 把 COMPONENT_MGMT 限定为“只写两列”）；④ 远端已有、本地未发现的组件 `is_managed` 默认值未定义（`L1423` 只覆盖“已由扫描发现”） |
| P0-2 | LOCAL_DELETE 复用 DELETE job | **PASS** | `L1428`：`confirm=true`+`confirmText=componentId`+四阻断+`.trash`、仅全成/已不存在才置位、PARTIAL、不提供强制删除、可重试；AC-OP-32②③⑤ `L2558` | P1：与 AC-OP-21 强制删除路径（`L2468`）的边界（“复用 DELETE job 语义”是否含 force）未写清 |
| P0-3 | REPAIR/RECLONE 回滚 | **FAIL** | REPAIR 先备份原内容再 init/fetch/checkout、失败 move back（`L1446–1449`）；RECLONE 前置（可达/凭据/clean）/stamp 唯一/失败 move back/30 天（`L1450–1453`）；AC-OP-05⑥ `L2341` | ① **成功即删除 `<repo>.repair-backup`**，而“可安全保留文件”未定义（`L1447/L1449`）→ 可误删用户文件（违反 FR-SAFE-01“将丢失什么”原则）；② §16.3 声称“AC-REPO-08 两场景 + 失败回滚”（`L1456`），**AC-REPO-08 无回滚/零改动断言**（`L2635–2641`）→ 验收不可判定（DoD 空过面） |
| P0-4 | baseline 主键裁决 | **PASS** | 固定 4 行、缺 Tag 行 `tag_present=0/commit_sha=NULL`（`L1461–1463`）；PK 不变、同键覆盖、历史由 `operation_log`（`L1464`）；AC-OP-12③ `L2396`、AC-OP-13④ `L2404` 已同步；§8.1 PK `L406` 一致 | P1：`tag_object_sha` 在 AC-OP-14⑤ 写成“可另存”（`L2412`）与 §16.4 强制不符；失败 job 写 0 行语义歧义（见 P1-3） |
| P0-5 | CREATE 撤销基线/journal | **FAIL** | `job_item.baseline_ref` 持久化（`L1470`）；逐仓四校验/`branch -D`/SKIPPED/partial（`L1471`）；审计 CREATE_UNDO、30 天、200 条（`L1473–1474`）；AC-OP-25②③⑤ `L2501` | **journal 无载体**：`L1472`+AC-OP-25④ 要求 undo 写 STARTED/DONE/FAILED“复用 FR-SAFE-04”，但 journal=job_item 三元组（AC-SAFE-08 `L2726–2731`），undo 非 job、D-028 无对应 kind（`D-028:18`）→ 不可实现（需二选一：把 undo 定为 job 或新增 journal 表/ADR） |
| P0-6 | DELETE_PENDING 重试安全 | **PASS** | 只落 `.trash/.pending/*.json`、DB 不新增（`L1436`）；`E_DELETE_PENDING`（`L1437`、附录 D `L1398`）；重试必须重扫+canonicalize+四阻断+边界/reparse 校验、禁止按旧 localPath 移动（`L1438`）；取消/GC（`L1439`）；AC-OP-23②③⑤⑥ `L2485` | P1：“仅重试失败项只重试 pending 项”与 AC-OP-26③（失败项均可重试）冲突；§16.2 说“取消/超期可断言”但 AC-OP-23 无超期用例；pending 30 天与 `delete.retentionDays` 可配（`L1369`）未关联 |
| P0-7 | 故障注入 Debug-only/注入点枚举 + G-WIRE | **FAIL** | 启用的两条件（env ∧ Debug/Test 符号）、Release 忽略（`L1479`）；9 个注入点固定枚举（`L1480`）；确定性时序（`L1482`）；AC-OP-35①②③④⑤ `L2573–2575`；G-WIRE 不记 Authorization 值（`L1481`） | 注入部分 PASS；**G-WIRE 部分 FAIL**：资产定义冲突（§1.6 `L1642` vs `L1481/L1497`）、AC-SAFE-04② 方法计数断言与 git smart-HTTP 读（fetch=POST）+`fetch.beforeWrite=true`（`L1367`）冲突 → 见 P0-D |
| P0-8 | SKIP 分级 | **PASS** | L1 不允许 SKIP、L2 不可达才 SKIP 且 SKIP 不计 DoD 真实证据（`L1488–1491`）；runner 必须区分 PASS/SKIP/FAIL（`L1492`）；DoD-4“SKIP 不计 COVERED”（`L1516`）；AC-OP-34①③⑤（`L2566`） | P1：证据可追溯性（manifest 未定义、artifacts gitignored、probe 匿名）与冻结出口冲突 → 见 P0-E/P1-10/P1-11 |
| P0-9 | 新枚举/错误码/写者 ADR | **FAIL** | D-028 已给出：component 两列写者/迁移、job_item.baseline_ref、baseline 规则、DELETE_PENDING 无表、job.kind REPAIR/RECLONE、audit action 集合、6 个 Host 码（`D-028:11–20`）；附录 D 六码已补（`L1398–1403`） | **§8.1 未回写**：`job.kind` 无 RECLONE（`L397`）、`job_item` 无 `baseline_ref`（`L398`）、`operation_log action` 仍“CHECK 同 job.kind 集”（`L399`）、component 写者未改（`L391`）、`repo.is_local` 写者冲突（`L392`）→ 冻结后两套 schema 口径；D-028 自身仍“草案”（`D-028:5`）、把 REPAIR 说成“新增”（§8.1 本已有） |

**小结：PASS=4（P0-2/4/6/8），FAIL=5（P0-1/3/5/7/9）。**

---

## 2. 专题一：§16 仍存在的「架构不可实现 / 误删误写 / 泄露 token」条款

### 2.1 架构不可实现（冻结即固化）

| ID | 条款 | 问题 | 冻结前处置 |
|---|---|---|---|
| U-1 | §16.5 `L1470–1472`、AC-OP-25④ `L2501` | undo 的写前 journal 无实体（非 job、无 journal 表；FR-SAFE-04 journal=job_item 三元组） | 二选一：①把 `undoCreate` 定为 job（需扩 `job.kind`，ADR）；②新增 journal 表/或明确用 audit 行作 journal 并改 AC-SAFE-08 口径 |
| U-2 | §16.6 `L1481`、§16.7.1 `L1497` vs Annex §1.6 `L1642`、AC-SAFE-13 `L2769–2772` | G-WIRE 同名两义：REST 桩 vs 全量 HTTP 代理；字段集/脱敏口径相反；REST 桩无法观测 `git.exe`，代理又会把 git fetch 的 POST 计入 | 拆成两个资产代号（如 `G-WIRE-REST` / `G-WIRE-GIT`），并回写 §1.6/AC-SAFE-13；“记录头”改为“只记头名白名单、不落值” |
| U-3 | AC-SAFE-04② `L2698` | “DELETE/PUT/PATCH/POST 方法数=0”与 git smart-HTTP 的读 POST、`fetch.beforeWrite=true`（`L1367`）不可区分 → 不可实现（或只测到 REST 侧，测不到 git 写） | 断言改为：①排除 git 传输端点（`info/refs`、`git-upload-pack`/`git-receive-pack`）后的 REST 写=0；②Gitea 服务端/bare 仓 refs/reflog 前后不变；③`GIT_TRACE2_EVENT` argv 无 push/receive-pack |
| U-4 | §16.3 `L1450–1452`、§16.4 `L1465` | RECLONE 失败 move back 未定义半成品目录处置；“失败 job 不写半套”与“job_item 失败仍写 tag_present=0 完整 4 行”互斥；checkout 失败≠缺 Tag 却写 0 → 可误判 `TAG_DRIFT`；缺仓组件（D-015 允许 3 仓）无法“固定写 4 行” | 明确：clone 先写临时目录→成功后原子改名；baseline 失败语义按“是否解析到 tag/commit”而非“是否检出”；缺仓时行集规则写清 |
| U-5 | §16.3 `L1447` | `<repo>.repair-backup` 落在扫描根（深度 1），会被 L0 扫描当作 NON_REPO/组件目录候选；崩溃后成孤儿 | 改用工作区外/`.trash` 下固定暂存区（如 `<root>/.trash/.repair/<stamp>`），或明确扫描排除前缀；补启动孤儿清理 |

### 2.2 会误删 / 误写

| ID | 条款 | 风险 | 冻结前处置 |
|---|---|---|---|
| S-1（P0-C） | §16.3 `L1447–1449` | REPAIR 成功后删除 `<repo>.repair-backup`；“可安全保留文件”未定义、无丢失清单/内容等价校验 → 非 git 目录中的用户文件可被静默删除 | 定义“可安全保留”白名单（如仅空目录/仅与远端 HEAD 同内容文件，且逐文件 SHA256 校验）；确认框必须列“将被删除的文件”；否则默认转 RECLONE（`.trash` 可恢复） |
| S-2（P0-A） | §16.1 `L1428`、AC-OP-32② `L2558` vs §8.1 `L392` / D-028 `D-028:11` | `repo.is_local=0` 由谁写三方冲突；component 行 INSERT 无写者 → 同一字段双写/漏写 | 明确：LOCAL_DELETE 只写 `component.is_deleted`，`is_local` 由扫描重算；新纳管 component 行由 COMPONENT_MGMT 以“最小行插入”写或在 D-028 里给出插入归属 |
| S-3 | §16.1 `L1422–1425` | 远端已有、本地未发现的组件默认 `is_managed` 未定义 → 不定义则“升级即隐藏/显示全部远端组件”二选一事故 | 写死：未发现组件的行不存在（不出现在看板）；bind 时才插入 |
| S-4 | §16.4 `L1465` | 失败 job 写 `tag_present=0` 可能把“检出受阻（dirty/unmerged）”误记为“缺 tag”→ 误写 `TAG_DRIFT` | 同 U-4：以 tag 解析结果而非检出结果决定 `tag_present`；失败原因写 `job_item.message` |
| S-5 | §16.1 `L1428` vs AC-OP-21③ `L2468` | “复用 DELETE job 语义”与“不提供强制删除”边界不清：若复用 force 路径则 unbind 可强删，若不复用则脏仓永远 PARTIAL | 明确 unbind 不暴露 force，且 PARTIAL 后“先清理再重试”的路径 |

### 2.3 泄露 token

| 面 | 结论 | 依据 |
|---|---|---|
| G-WIRE 记录 | 语义已写明“不记 Authorization 值”（`L1481`、AC-SAFE-13① `L2772`），**但与 §1.6“记录头”（`L1642`）冲突**，需按 U-2 一并改 | P0-D |
| 命令行快照 | AC-SAFE-13 依赖 `Get-CimInstance Win32_Process` 全量 argv 快照（`L2771`）；WMI 快照可能抓不到短命 `git.exe`，argv 为空时“哨兵=0”空洞通过 → 真泄露也可能漏检 | P1 |
| bind/REPAIR/RECLONE | bind 远程校验失败只要求 fail-closed（`L1425`），未要求复用 `HostLog.Redact`/不回显 git stderr；REPAIR/RECLONE 只写“凭据作用域通过”（`L1451`），未显式要求 origin 去 userinfo、复用 AC-SAFE-14 同一凭据环境 | P1 |
| pending 记录 | `localPath` 落盘（`L1436`）属用户目录信息，非 token；与 AC-OP-28 导出脱敏规则一致即可 | PASS |

**结论：本轮未发现新增 P0 级 token 泄露面；token 面收敛为 P1（argv 证据可空过 + 新路径未显式复用凭据规范）。**

---

## 3. 专题二：D-028 与 §8.1 一致性 / 是否还需 ADR

| 项 | D-028 表述 | §8.1 现状（行号） | 判定 |
|---|---|---|---|
| `component.is_managed/is_deleted` 语义 | 管理状态，非聚合态；写者 COMPONENT_MGMT 只写两列+updated_at；迁移默认 1/0（`D-028:11`） | 字段已在（`L391`），但**唯一写者=STATUS_CALC**（`L391`），§8.2 要求每字段唯一写者（`L413`） | **不一致**：字段级写者未体现；需回写 §8.1 + 明确 INSERT 归属 |
| 新纳管（未克隆）组件行 | 未提 | `component` 仅 STATUS_CALC 写（`L391`）；AC-OP-31② 要求 bind 后“component 行写入”（`L2550`） | **缺口**：无 INSERT 写者定义 |
| `repo.is_local` | D-028 只规定 COMPONENT_MGMT 写 component 两列 | §8.1 本地列=LOCAL_SCAN（`L392`）；§16.1 `L1424` 亦写“本地列仍由 LOCAL_SCAN 写”，但 `L1428`/AC-OP-32② 要求置 0 | **三方冲突**（同文件内自相矛盾） |
| `job_item.baseline_ref` | 新增可空列，写者 JOB_RUNNER，不改既有列语义（`D-028:12`） | `job_item` 关键字段**无**该列（`L398`）；§16.5 却写“§8.1 增列”（`L1470`） | **不一致** |
| `job.kind` REPAIR/RECLONE | 均“新增”（`D-028:18`） | 已有 REPAIR、**无 RECLONE**（`L397`）；§16.3 却写“新增到 §8.1 合法集”（`L1445`） | **不一致**（REPAIR 表述亦不准） |
| `operation_log.action` | 语义白名单=job.kind∪{COMPONENT_BIND, COMPONENT_UNBIND, CREATE_UNDO, RECONCILE}；Host 实现本不校验 CHECK（`D-028:19`） | 仍写 `action(CHECK 同 job.kind 集)`（`L399`） | **不一致**（CHECK 口径需二选一：改 DDL 或改 §8.1 为白名单） |
| `baseline_snapshot` | PK 保持；固定 4 行；同键覆盖；history→operation_log；缺 Tag 行 `tag_present=0`（`D-028:13`） | PK/字段一致（`L406`）；AC-OP-12③/13④ 已同步（`L2396/L2404`） | **一致** ✓ |
| `DELETE_PENDING` | 不新增表/列，只文件台账；不改 §7.2/§7.4（`D-028:14`） | §8.1 无新增；§16.2 `L1436–1437` 一致 | **一致** ✓ |
| 错误码 | 新增 6 个 Host 码并映射 `details.srsCode` 前缀（`D-028:20`） | 附录 D 六行已补（`L1398–1403`） | **一致** ✓（但各码到 AC 的映射未逐条写：E_AUTH_FAILED/E_REMOTE_NOT_FOUND/E_NETWORK 无 AC 断言 → P2） |

**冻结所需 ADR 结论：不需要再立新 ADR。** D-028 已覆盖语义；但其状态仍是“草案、随 v0.4 冻结”（`D-028:5`），冻结前必须：
1. **修订 D-028**：补 ①component 行 INSERT 写者（新纳管）②`repo.is_local` 归属（删除写入或改由扫描重算）③“REPAIR 新增”措辞；
2. **回写 §8.1**（或明确“§8.1 对应行以 D-028 为准”的单向声明）：`job.kind` 加 RECLONE、`job_item` 加 `baseline_ref`、`component` 写者列、`operation_log.action` 白名单；
3. D-028 状态改为“随 v0.4 冻结（冻结日期/commit）”，避免“草案 ADR 支撑冻结需求”。

> 一句话：**D-028 已是正确载体，但“ADR 已修订 §8.1”目前只是 §16 的宣称，正文/表未改 → 仍不能冻结（P0-A）。**

---

## 4. 专题三：测试门禁是否仍可空过 DoD

### 4.1 已闭环的部分（P0-8）

| 机制 | 证据 |
|---|---|
| SKIP≠PASS，SKIP 不计 COVERED | §16.7 `L1490`、DoD-4 `L1516`、AC-OP-34①③⑤ `L2566` |
| L1（单元/本地 bare/Mock CDP/单二进制 Gitea/夹具）必跑且**不允许 SKIP** | `L1489` |
| L2 不可达才 SKIP，写 `result.json` | `L1490` |
| runner 必须区分 PASS/SKIP/FAIL、SKIP 单列 | `L1492` |
| 测试资产冻结定义（G-E2E/G-WIRE/F-*/C-SYNTH/CLK） | §16.7.1 `L1494–1500` |

### 4.2 仍可“形式合规空过”的 4 条路径

| # | 路径 | 后果 | 级别 |
|---|---|---|---|
| 1 | **冻结出口未纳入证据门禁**：§3.3 M2b 出口（`L145`）与 D-025 §4（`D-025:152`）只到 AC-OP-01…32，不含 AC-OP-34/35/evidence manifest；§3.3 又规定“与 ADR 冲突以 ADR 为准”（`L152`） | 按旧出口宣称 M2b 完成，AC-OP-34/35 不计入 → 证据门禁形同虚设 | **P0-E** |
| 2 | **证据 manifest 无定义**：只出现“证据 manifest”（`L1508、L1491`），无文件名/路径/schema/“必须入 git”要求；`artifacts/` 仍被 `.gitignore:13` 忽略 | 证据可为本地一次性文件/事后自证，无法按 commit 追溯 | P1 |
| 3 | **probe 口径弱**：可达条件仅匿名 `GET /api/v1/version`（`L1487`），token 缺失/org 错/夹具不可用也“可达”；未禁止 realhost “`cond ? assert : true`”（§16.7 只约束 runner 三态） | L2 可“跑了但没验证真实 Gitea 行为” | P1 |
| 4 | **result.json schema 两处不一致**：§16.7 `{skipped,url,reason,commit}`（`L1490`） vs AC-OP-34② `{target,url,reachable,skipped,commit,timestamp,checks}`（`L2566`）；PASS 态字段未定义 | 机器判定口径随实现漂移；“SKIP 不计 COVERED”可因字段缺失无法核验 | P1（兼 P2） |

### 4.3 其他门禁缺口

- DoD-1 要求 `AC-OP-01…35` 全 COVERED（`L1513`），但 §13.4 追溯矩阵 FR-OP 仍写 `AC-OP-01…32`（`L1206`），FR-SAFE 写 `AC-SAFE-01…17`（`L1208`）→ AC-OP-34/35 无追溯行（P1）。
- DoD-4 “单元/集成全绿（当前 397/397 基线）”（`L1516`）是易腐化硬编码（新增测试即失效/可选择性执行）（P2）。

---

## 5. 残余 P0 / P1 / P2

### P0（冻结前必须修；缺一不可）

| ID | 对应 | 问题 | 冻结前要求 |
|---|---|---|---|
| P0-A | P0-1 + P0-9 | §8.1 未随 D-028/§16 回写；component 行 INSERT 写者、`repo.is_local` 归属未定 → 冻结后两套 schema、同字段双写/漏写 | 修订 D-028（插入写者/is_local/措辞）+ 回写 §8.1 四行；D-028 状态转“随 v0.4 冻结” |
| P0-B | P0-5 | undo 的“写前 journal”无载体（非 job、无 journal 表）→ 架构不可实现 | undo 定为 job（扩 kind）或新增 journal 表并改 AC-SAFE-08/AC-OP-25 口径 |
| P0-C | P0-3 | REPAIR 成功删 `<repo>.repair-backup`、无“可安全保留”定义/丢失清单/内容校验 → 误删；AC-REPO-08 无回滚断言 | 定义白名单+逐文件校验+确认框列失文件；补 AC-REPO-08 失败回滚/原文件 hash 不变断言（或新增 AC） |
| P0-D | P0-7(G-WIRE) | G-WIRE 双定义；AC-SAFE-04② 方法计数不可实现/远端零写证据不成立 | 拆资产代号+回写 §1.6/AC-SAFE-13；AC-SAFE-04 改为服务端 refs/reflog + argv 断言 |
| P0-E | 本轮新查 | §16.9 DoD 与 §3.3/冻结 ADR D-025 §4 的 M2b 出口冲突；证据 manifest 未定义、artifacts gitignored → 证据门禁可绕开 | 修订 D-025 §4/§3.3 纳入 AC-OP-34/35+manifest；定义 manifest 路径/schema/必须入库；probe 加 token+org；禁止条件 true 断言 |

### P1（冻结前应修，最迟随实现评审闭环）

1. AC-UI-16 未建：§16.1 `L1430` 把它推为“实现前补”，违反 §16 自定规则“无法映射的须在冻结前新增 AC”（`L1417`）；且“纳入/移出”入口与 §11.6 六入口/AC-UI-13（`L930、L1084`）落点未定。
2. `tag_object_sha`：§16.4 强制（`L1462`）vs AC-OP-14⑤“可另存”（`L2412`）→ annotated tag 断言不可判定。
3. baseline 失败语义：`L1465` “失败 job 不写半套”与“job_item 失败仍写 tag_present=0 完整 4 行”互斥；checkout 失败≠缺 tag；缺仓组件（D-015）无法固定 4 行。
4. pending 重试范围：“仅重试失败项只重试 pending 项”（AC-OP-23⑤ `L2485`）与 AC-OP-26③（`L2510`）冲突；§16.2 说“取消/超期可断言”但 AC 无超期用例；pending 30 天与 `delete.retentionDays`（`L1369`）未关联。
5. RECLONE 标题写“远程不可用”（`L1450`）与前置“远程可达”（`L1451`）自相矛盾（应为 origin/URL 不匹配）。
6. RECLONE 失败 move back 未定义半成品目录处置；`.trash/<stamp>/<repoName>` 未加备份类型 → AC-OP-30 RESTORE（`L2536–2542`）“最近 stamp”可能取到 RECLONE 备份；进程崩溃（非可控失败）无回滚/reconcile 定义。
7. `<repo>.repair-backup` 在扫描根内（命名冲突/被扫为 NON_REPO）且无孤儿清理。
8. undo：`<branch-encoded>`（`L1470`）vs AC-OP-25② `<branch>`（`L2501`）不一致；未声明 typed confirm（破坏性本地写）；job 200 条淘汰后 undo 行为未定义。
9. 故障注入：AC-OP-27 依赖“慢 git 桩”（`L2515`）不在固定枚举（`L1480`）；注入 JSON 无字段白名单/取值校验。
10. 证据可追溯：manifest 未定义 + `artifacts/` gitignored（`.gitignore:13`）；§16.7/AC-OP-34 的 `result.json` schema 不一致。
11. probe 仅匿名 `/api/v1/version`（`L1487`）；未禁止条件 true 断言；AC-SAFE-13 的 argv 快照对短命进程可空集（`L2771–2772`）。
12. §13.4 追溯矩阵（`L1206/1208`）与 §3.3 M2b 出口（`L145`）未纳入 AC-OP-34/35。
13. 安全：bind 失败文案未要求脱敏（`L1425`）；REPAIR/RECLONE 未显式要求 origin 去 userinfo/复用 AC-SAFE-14 凭据环境（`L1451` 仅“凭据作用域通过”）；LOCAL_DELETE 与 AC-OP-21 force 路径边界（P1-2 条）。
14. LOCAL_DELETE 后重新纳入：`is_deleted=1→0` 且目录已在 `.trash` 时的 clone/恢复路径未定义（AC-OP-32⑥ 只断言“恢复显示/DB 一致”）。

### P2（实现设计/文档治理）

1. pending 文件名 `<stamp>-<repoName>.json` 的 repoName 编码/防路径注入未定义（`L1436`）。
2. 启动扫描读 `.pending` 与 AC-SCAN-01④“.trash 下目录不被发现”（`L1875`）的例外需在 AC 注明。
3. `captured_at` 无 AC 断言（AC-OP-11/12/13 未断言同收口事务/同一时点）。
4. D-028 把 REPAIR 写成“新增”（§8.1 已有）→ 措辞修正。
5. §16.6“Release 忽略并日志告警”与“编译期裁剪”表述可精化（Release 可保留告警代码但不含注入实现）。
6. DoD-4 硬编码“397/397”易腐化；AC-OP-23 依赖 `MBD_FAULT_INJECT` 但未注明须 Debug/Test 构建。
7. E_AUTH_FAILED/E_REMOTE_NOT_FOUND/E_NETWORK 无 AC 断言映射（附录 D 已补码，但 AC 未用）。

---

## 6. 结论与冻结前最小修复集

> **结论：需再修，不可冻结（残余 P0=5）。**
> 上轮 9 项中 4 项闭环（LOCAL_DELETE、baseline 主键、DELETE_PENDING 重试安全、SKIP 分级），5 项未闭环（组件管理写者、REPAIR/RECLONE、CREATE 撤销 journal、G-WIRE、枚举/§8.1 同步，其中 P0-1 与 P0-9 同根因合并为 P0-A；G-WIRE 单列为 P0-D），另新增 P0-E（DoD 与冻结出口冲突/证据门禁可绕）。§16 的“误删误写”面剩 2 处（REPAIR 删备份、`repo.is_local` 写者冲突），“不可实现”面剩 3 处（undo journal、G-WIRE 代理/AC-SAFE-04 断言、RECLONE 半成品处置）。

**冻结前最小修复集（5 条，缺一不可）**：

1. **P0-A**：修订 D-028（component 行 INSERT 写者、`repo.is_local` 归属、措辞）+ 回写 §8.1（RECLONE / `job_item.baseline_ref` / component 写者 / `operation_log.action` 白名单）+ D-028 状态转“随 v0.4 冻结”。
2. **P0-B**：给 `undoCreate` 的 journal 定载体（job 或 journal 表），同步 FR-SAFE-04/AC-SAFE-08/AC-OP-25④ 口径。
3. **P0-C**：定义 REPAIR 的“可安全保留文件”白名单+逐文件校验+确认框“将删除清单”，并补 AC-REPO-08 失败回滚/原文件 hash 不变断言。
4. **P0-D**：拆分 G-WIRE 资产代号并回写 §1.6/AC-SAFE-13；把 AC-SAFE-04② 改为服务端 refs/reflog + git argv 断言（排除 git 传输端点）。
5. **P0-E**：修订 D-025 §4/§3.3 纳入 AC-OP-34/35+evidence manifest；定义 manifest 路径/schema/必须入 git；probe 加 token+org；禁止条件 true 断言；§13.4 补 AC-OP-34/35 追溯行。

**可留实现设计（不阻塞冻结，但 §16 应加一句“以下为实现设计，不改变验收语义”）**：pending JSON 字段/文件名编码、repair-backup 暂存区布局、RECLONE 半成品清理顺序、注入报告模板、undo 逐仓报告格式、错误文案。

---

## 7. 证据索引

**§16（本次主体）**
- §16 总述：`reqs/specification.md:1414–1418`；§16.1 `:1420–1431`；§16.2 `:1433–1441`；§16.3 `:1443–1456`；§16.4 `:1458–1466`；§16.5 `:1468–1475`；§16.6 `:1477–1483`；§16.7 `:1485–1500`；§16.9 `:1505–1517`

**正文/附录**
- §8.1 表清单/写者：`:385–407`（component `:391`、repo `:392`、job `:397`、job_item `:398`、operation_log `:399`、baseline_snapshot `:406`）；§8.2 `:410–417`
- 附录 D：错误码 `:1353–1408`（6 个 Host 码 `:1398–1403`；`fetch.beforeWrite` `:1367`；`delete.retentionDays` `:1369`）
- Annex §1.6 测试资产：`:1638–1649`（G-WIRE `:1642`、G-E2E `:1643`）
- §3.3 里程碑：`:138–152`（M2b 出口 `:145`、ADR 优先 `:152`）
- §11.6 二级入口：`:923–932`；AC-UI-13：`:1084`
- §13.2 测试策略：`:1175–1184`；§13.4 追溯：`:1196–1223`

**AC**
- AC-OP-05：`:2335–2342`；AC-OP-11 `:2383–2389`；AC-OP-12 `:2391–2397`；AC-OP-13 `:2399–2405`；AC-OP-14 `:2407–2413`
- AC-OP-21（强制删除）：`:2463–2469`；AC-OP-23 `:2479–2486`；AC-OP-25 `:2495–2502`；AC-OP-26 `:2504–2510`；AC-OP-30（RESTORE）`:2536–2542`
- AC-OP-31 `:2544–2551`；AC-OP-32 `:2552–2559`；AC-OP-34 `:2560–2567`；AC-OP-35 `:2569–2576`
- AC-REPO-08 `:2635–2641`；AC-SAFE-04 `:2693–2699`；AC-SAFE-08 `:2726–2732`；AC-SAFE-13 `:2766–2773`
- AC-SCAN-01 `.trash` 排除：`:1875`

**ADR**
- D-028：`reqs/adr/D-028-m2b-schema-enums.md:5,11–14,18–20,25`
- D-025：`reqs/adr/D-025-m2a-m2b.md:95,152`

**其他**
- `.gitignore:13`（`artifacts/` 被忽略）；`reqs/fixtures/m2-matrix.json`（存在，14×5 单源夹具）
