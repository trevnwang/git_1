# §8.1 / D-028 / probe / D-025 复评 R4（范围受限）：R3 残余 2 项 P0 核验

| 项目 | 内容 |
|---|---|
| 评审对象 | `reqs/specification.md`（HEAD `13a20ab`；§8.1 L385–408、§16.7 L1516–1522、§16.9 L1543–1550、AC-OP-32 L2585–2592、AC-OP-34 L2593–2601、AC-DETAIL-06 L2302–2308）；`reqs/adr/D-028-m2b-schema-enums.md`（L11/L19/L25）；`reqs/adr/D-025-m2a-m2b.md`（§2.3 L95、§4 L150–152） |
| 范围 | **只核 R3（`docs/review-spec-r3-arch.md`）残余 2 项 P0**：①§8.1 is_local 双写者（应仅 LOCAL_SCAN；管理操作不写）与 `operation_log.action` 白名单是否与 D-028 一致；②probe 是否要求 version+user+org 三项、禁止恒真断言，以及 D-025 §4 是否已含 AC-UI-16。不重评其余 P1/P2 |
| 边界 | **只读**：未修改 `reqs/`、代码、配置、真实数据；本文件是本次唯一新增产物；行号均指 HEAD `13a20ab` |
| 结论 | **可冻结**（2 项 P0 均闭环；残余 P1/P2 不阻塞冻结） |

## 0. 判决

| # | 核验项 | 判定 | 证据（行号） | 残余（非阻塞） |
|---|---|---|---|---|
| 1 | §8.1 is_local 写者 + `operation_log.action` 白名单 ↔ D-028 | **PASS** | ① is_local：`§8.1:387`“本地字段（is_local/local_path）由 LOCAL_SCAN 单写（管理操作只触发重扫，不直接写 is_local）”；`§8.1:392`“LOCAL_SCAN（is_local/local_path 单写）”；`D-028:11`“repo.is_local/local_path 仍由 LOCAL_SCAN 单写，管理操作不直接写”；`§16.1:1459` LOCAL_DELETE“is_local 由下一次 LOCAL_SCAN 置 0，管理 API 不直接写”；`AC-OP-32②:2591`、RECLONE `§16.3:1484`、`AC-REPO-08②:2673` 同口径。② action：`§8.1:399`“action(CHECK = job.kind ∪ {COMPONENT_BIND, COMPONENT_UNBIND, CREATE_UNDO, RECONCILE}，D-028)”与 `D-028:19` 完全一致；INSERT 归属 `D-028:11/:25` 已明确。 | P1：`AC-DETAIL-06:2307` 仍写“action 取 SRS §8.1 `operation_log.action`，**同 job.kind 集**”，与新白名单不符（应改为“D-028 白名单”）；P2：`D-028:19`“Host 实现原本不校验 CHECK”与 `§8.1:399` 的 DDL `CHECK=白名单` 措辞可对齐 |
| 2 | probe 三项 + 禁止恒真；D-025 §4 含 AC-UI-16 | **PASS** | ① `§16.7:1518`：“GET `<base>/api/v1/version` + `GET /api/v1/user`（带 token，校验身份）+ `GET /api/v1/orgs/{org}/repos?limit=1`（校验 org/权限）…**三项全 2xx 才算 L2 可达**；**禁止条件恒真/空断言**；runner 必须记录每项 HTTP status。”② `D-025 §4:152`：“…+ `AC-UI-16`（组件管理入口）+ …”；`D-025 §2.3:95` 同；`§3.3:145`、`§16.9:1546` 亦含 AC-UI-16。 | P1：`AC-OP-34②:2599` 的 `result.json` 最小字段仍只有 `{target,url,reachable,…}`，未要求三项各自 status/恒真禁令（§16.7 已约束 runner，建议 AC 同步）；`§16.7:1521` SKIP schema 与 AC-OP-34② 字段名仍不一致 |

**小结：PASS=2（第1、2 项均闭环）。**

## 1. 逐项说明

### 第 1 项（PASS）
- **is_local 单写已闭合并四处一致**：`§8.1:387` 与 `:392` 现均为 LOCAL_SCAN 单写；`D-028:11` 显式登记“管理操作不直接写”；`§16.1:1459`（LOCAL_DELETE）、`§16.3:1484`（RECLONE 成功）、`AC-OP-32②:2591`、`AC-REPO-08②:2673` 均改为“由下一次 LOCAL_SCAN 置 0/1”。R3 的 L387 vs L392 双写者冲突已消除，`§8.2` 唯一写者成立。
- **action 白名单已与 ADR 对齐**：`§8.1:399` 直接引用 `job.kind ∪ {COMPONENT_BIND, COMPONENT_UNBIND, CREATE_UNDO, RECONCILE}，D-028`，与 `D-028:19` 语义白名单逐项一致；`CREATE_UNDO` 同时已在 `job.kind`（`§8.1:397`、`D-028:18`），集合内重复但语义无冲突。
- INSERT 归属：`D-028:11`（COMPONENT_MGMT 写两列 + INSERT component 行）与 `D-028:25`（bindComponent INSERT 归属 COMPONENT_MGMT）已明确，`AC-OP-31②:2583`“component 行写入”可落。

### 第 2 项（PASS）
- **probe 加固**：`§16.7:1518` 已由匿名 version 单一探测升级为 **version + user（token 身份）+ org repos（org/权限）三项全 2xx**，且明写“禁止条件恒真/空断言”“记录每项 HTTP status”；R3 的“可达≠可测”与“条件空断言”两残点已在需求层关闭。
- **D-025 出口**：`D-025 §4:152` 增列 `AC-UI-16（组件管理入口）`，与 `D-025 §2.3:95`、`§3.3:145`、`§16.9:1546` 一致，R3 的“按旧出口可跳过 AC-UI-16”冲突已消除。

## 2. 残余（P1/P2，不阻塞本轮冻结）

1. **P1** `AC-DETAIL-06:2307` 仍写“action `同 job.kind 集`”，与 `§8.1:399`/`D-028:19` 白名单不符 → 改为“按 D-028 白名单”。
2. **P1** `AC-OP-34②:2599` 的 `result.json` 字段未覆盖三项 probe status；`§16.7:1521` SKIP schema 与 AC-OP-34② 字段名不一致 → 统一 schema。
3. **P2** `D-028:19`“Host 实现原本不校验 CHECK”与 `§8.1:399` DDL CHECK 措辞对齐。
4. 注：R3 的其它 P1（`E_REPAIR_FAILED/E_RECLONE_FAILED` 未入 D.2、`§16.3:1477`“可安全保留”未定义等）不在本轮范围，未复评；建议随实现评审闭环。

## 3. 结论

> **可冻结。** R3 残余 2 项 P0 均已在 HEAD `13a20ab` 闭环：①is_local 已改为 LOCAL_SCAN 单写（§8.1 L387/L392、D-028:11、§16.1 L1459、AC-OP-32② L2591 一致），`operation_log.action` 白名单与 D-028:19 一致（§8.1 L399）；②probe 已要求 version+user+org 三项 2xx 且禁止恒真/空断言（§16.7 L1518），D-025 §4 L152 已含 AC-UI-16。残余 P1/P2 为文档同步项，不改变验收语义，不阻塞冻结。
