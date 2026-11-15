# §16 / D-028 复评 R3（范围受限）：R2 残余 5 项 P0 核验

| 项目 | 内容 |
|---|---|
| 评审对象 | `reqs/specification.md`（HEAD `78e5a7b`，§16 L1442–1547；§8.1 L385–408；附录 D/D.2 L1389–1437；Annex §1.6 L1668–1679；AC-OP/AC-REPO/AC-SAFE 相关行）+ `reqs/adr/D-028-m2b-schema-enums.md`（全 26 行）+ D-025 §2.3/§4 |
| 范围 | **只核 R2（`docs/review-spec-r2-arch.md`）残余 5 项 P0：A（§8.1/D-028 写者/枚举/INSERT/is_local）、B（CREATE_UNDO journal 载体）、C（REPAIR 备份/回滚/确认清单）、D（G-WIRE 双义/AC-SAFE-04 证据）、E（DoD/D-025 出口/manifest/probe）**；不重评 R1/R2 其余项 |
| 边界 | **只读**：未修改 `reqs/`、代码、配置；本文件是本次唯一新增产物；行号均指 HEAD `78e5a7b` |
| 结论 | **需再修，暂不可冻结（残余 P0=2：A、E；B/C/D 闭环）** |

## 0. 判决

| # | R2 残余 P0 | 本轮判定 | 关键证据（行号） | 残余 |
|---|---|---|---|---|
| A | §8.1/D-028 写者/枚举/INSERT/is_local | **FAIL** | 已修：component 写者 `§8.1:391`、repo is_local 写者 `:392`、job.kind 加 RECLONE/CREATE_UNDO `:397`、job_item.baseline_ref `:398`、INSERT 归属 `D-028:25`、状态转冻结 `D-028:5` | ① `§8.1:387` 仍写“is_local/local_path 由 LOCAL_SCAN 写”，与 `:392` COMPONENT_MGMT 双写者，D-028 未登记 is_local；② `§8.1:399` action 仍“CHECK 同 job.kind 集”，与 `D-028:19` 白名单两套；③ `:398` 新增 `reconcile_status` 未在 D-028 登记 |
| B | CREATE_UNDO journal 载体 | **PASS** | `§16.5:1499` 创建 CREATE_UNDO job、`:1500` 作为 job kind 走 JOB_RUNNER 写 STARTED/DONE/FAILED 与 job_item；`§8.1:397` 含 CREATE_UNDO；`D-028:18`；`AC-OP-25④:2531` 断言 journal+审计 | 无（载体=job/job_item，与 FR-SAFE-04/AC-SAFE-08 同实体） |
| C | REPAIR 备份/回滚/确认清单 | **PASS** | `§16.3:1475` 全部内容→`.trash/<stamp>/<repo>.repair-backup`（30 天，确认框列文件清单/数量）、`:1476` 失败 move back/原文件 hash 不变、`:1477` 不可安全保留默认拒绝、`:1480` RECLONE clone 失败 move back；`AC-REPO-08⑥:2670` 零改动+备份保留+clean 预检 | P1：`E_REPAIR_FAILED/E_RECLONE_FAILED` 未登记于 `D.2:1422-1437`；`:1477`“可安全保留内容”未定义 |
| D | G-WIRE 双义/AC-SAFE-04 证据 | **PASS** | `§16.7.1:1525` 拆 `G-WIRE-REST`/`G-WIRE-GIT`；`Annex §1.6:1672` 总称+强制写子资产；`AC-SAFE-04:2728` 改为“远端 refs/reflog 逐字节一致 + 无 push/无 force（POST 读不算远端写）”；`AC-SAFE-13:2801-2802` 只记 method/url/status、不记 Authorization 值 | P2：`:1672` 单行内含字面 `\n\|`（表格未分行）；`AC-SAFE-13:2799/2801` 仍用总称 G-WIRE，违反 §1.6“必须写子资产” |
| E | DoD/D-025 出口/manifest/probe | **FAIL** | 已修：`§3.3:145` 含 AC-OP-01…35+AC-UI-16；`D-025 §4:152` 含 AC-OP-35；`§16.9:1540` manifest 固定路径/schema；`:1543` DoD 含 AC-OP-35/AC-UI-16；`:1546` SKIP 不计 COVERED；`§13.4:1214/1219` 追溯含 35/AC-UI-16 | ① `§16.7:1515` probe 仍仅匿名 `GET /api/v1/version`，无 token/org；② 未禁止 realhost 条件空断言（`cond ? assert : true`）；③ `D-025 §4:152` 缺 AC-UI-16，而 `§3.3:145`/`§16.9:1543` 含，且 `§3.3:152` 规定“冲突以 ADR 为准”→ 冻结出口可绕过 AC-UI-16；P1：`result.json` schema 两套（`§16.7:1518` vs `AC-OP-34②:2596`） |

**小结：PASS=3（B/C/D），FAIL=2（A/E）。**

## 1. 逐项说明

### A-P0-A（FAIL）
- 已修部分：`§8.1:391` component 唯一写者拆为 `STATUS_CALC（业务列）/COMPONENT_MGMT（is_managed/is_deleted）`；`:392` repo 写者补 `COMPONENT_MGMT（is_local 的删除置 0）`；`:397` `job.kind` 加 `RECLONE/CREATE_UNDO`；`:398` `job_item.baseline_ref`；`D-028:11/:12/:18/:19/:25` 给出写者/枚举/白名单/INSERT 归属；`AC-OP-31②:2580`、`AC-OP-32②:2588` 与写者一致。
- 未闭环（冻结级）：
  1. `§8.1:387` 首段仍写“`repo` 本地字段（is_local/local_path）由 LOCAL_SCAN 写”，与 `:392` “COMPONENT_MGMT（is_local 的删除置 0）”构成**同字段双写者**，违反 `§8.2:413`；`D-028` 全文未提 `repo.is_local` → 归属不可判。
  2. `§8.1:399` `action(CHECK 同 job.kind 集)` 与 `D-028:19` “job.kind ∪ {COMPONENT_BIND, COMPONENT_UNBIND, CREATE_UNDO, RECONCILE}” 仍是两套口径；`AC-DETAIL-06:2304` 也按“同 job.kind 集”断言 → COMPONENT_BIND/RECONCILE 写审计时无合法枚举。
  3. `§8.1:398` 新增 `reconcile_status` 列未在 D-028 §1 登记（ADR 未覆盖新增列）。

### A-P0-B（PASS）
- `§16.5:1499`：`undoCreate` 创建 `CREATE_UNDO` job；`:1500`：“作为 job kind 走 JOB_RUNNER，写 STARTED/DONE/FAILED 与 `job_item`，不依赖不存在的 journal 表”。
- 载体闭合：`§8.1:397` job.kind 含 CREATE_UNDO；`D-028:18` 同；`AC-OP-25④:2531` 断言 journal+审计。R2 的“非 job 无实体”已消除。

### A-P0-C（PASS）
- `§16.3:1475` 原目录**全部内容**先移 `.trash/<stamp>/<repoName>.repair-backup`（30 天、类型 `repair-backup`），确认框列出将移走文件清单/数量；成功后不立即删、按 `.trash` 保留期清理 → R2“成功即删原内容”误删面消除。
- `:1476` 任一步失败 move back、原文件 hash 不变；`:1477` 不可安全保留默认拒绝并转 RECLONE；`:1480` RECLONE clone 失败 move back；`AC-REPO-08⑥:2670` 有失败回滚+零改动+备份保留 30 天断言；⑦ 补 clean/未推送/untracked/stash 前置。
- P1 残：AC 引用的 `E_REPAIR_FAILED/E_RECLONE_FAILED` 在 `D.2:1422-1437`、`D-028:20` 均未登记；`:1477`“可安全保留内容”无定义（不影响零丢失，因备份在 `.trash` 保留 30 天）。

### A-P0-D（PASS）
- `§16.7.1:1525` 冻结 `G-WIRE-REST`（REST 契约桩）与 `G-WIRE-GIT`（git smart-HTTP 代理）两个子资产；`Annex §1.6:1672` 明确 G-WIRE 为总称、引用必须写子资产、两者只记 `{method,url,status}` 且不记 Authorization 值 → R2“同名两义”已拆。
- `AC-SAFE-04:2728` 不再要求“POST 方法数=0”，改为**远端 refs/reflog 逐字节一致** + 无 push/无 force，并明示 git smart-HTTP 的 POST 读不算远端写 → R2“fetch=POST 与 beforeWrite 冲突”已消；`G-WIRE-GIT` 与外部代理/服务端日志（`§16.6:1509`）一致。
- P2 残：`:1672` 一行内含字面 `\n\|`，子资产未拆成独立表格行；`AC-SAFE-13:2799/2801` 仍用总称 `G-WIRE`（应按 §1.6 写子资产）。

### A-P0-E（FAIL）
- 已修部分：`§3.3:145` M2b 出口含 `AC-OP-01…35` + `AC-UI-16`；`D-025:95` 与 `D-025:152` 含 AC-OP-35；`§16.9:1540` manifest 固定路径 `docs/evidence/m2b-manifest.json` 与 schema；`:1543` DoD 含 35/AC-UI-16；`:1546` SKIP 不计 COVERED；`§13.4:1214/1219` 已补 35 与 AC-UI-16。
- 未闭环（冻结级）：
  1. **probe 仍未加固**：`§16.7:1515` 可达条件仅匿名 `GET <base>/api/v1/version`，未要求带 token 校验 org/权限 → “可达≠可测”；R2 P0-E 的“probe 加 token+org”未落。
  2. **条件空断言未禁止**：§16.7/AC-OP-34 均无“禁止 `cond ? assert : true`/空集通过”条款，realhost 仍可“跑了但没验证真实 Gitea”。
  3. **D-025 §4 与 §3.3/§16.9 出口不一致**：`D-025:152` 缺 `AC-UI-16`，而 `§3.3:145`、`§16.9:1543` 含；`§3.3:152` 规定冲突以 ADR 为准 → 冻结后仍可按旧出口宣称 M2b 完成并跳过 AC-UI-16。
  4. P1：`result.json` schema 仍两套（`§16.7:1518` `{skipped,url,reason,commit}` vs `AC-OP-34②:2596` `{target,url,reachable,skipped,commit,timestamp,checks}`）；manifest“必须入库”未写明（路径本身未被 `.gitignore` 忽略，但语义未冻结）。

## 2. 残余问题清单

**P0（2 条，冻结前必须修）**
1. **A-P0-A**：修 `§8.1:387` 与 `:392` 的 is_local 双写者（改首段或在 D-028 补 is_local 归属）；修 `§8.1:399` 与 `D-028:19` 的 action 枚举二选一（建议 §8.1 改“同 D-028 白名单”并同步 `AC-DETAIL-06:2304`）；`D-028` 补登记 `reconcile_status`。
2. **A-P0-E**：probe 加 token+org 校验（`§16.7:1515`）；§16.7/AC-OP-34 增“禁止条件空断言/空集通过”；`D-025:152` 补 AC-UI-16（与 `§3.3:145`、`§16.9:1543` 对齐）。

**P1（随实现评审闭环，不阻冻结）**：`E_REPAIR_FAILED/E_RECLONE_FAILED` 登记 D.2 与 D-028（或在 AC-REPO-08 改用既有码）；`§16.3:1477`“可安全保留内容”定义；`result.json` schema 统一；manifest 入 git 要求；`:1672` 表格分行与 AC-SAFE-13 子资产命名。

## 3. 结论

> **需再修，暂不可冻结（残余 P0=2：A、E）。**
> B（CREATE_UNDO journal 载体）、C（REPAIR 备份/回滚/确认清单）、D（G-WIRE 拆分/AC-SAFE-04 证据）已按 R2 要求闭环；A 仍有 2 处 §8.1 双写者/双枚举口径，E 仍有 probe 未认证、条件空断言未禁、D-025 §4 缺 AC-UI-16 三处。修完 A/E 的 5 个点后可进入冻结。
