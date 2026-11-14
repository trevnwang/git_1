# 需求复审 R3（只读）— 可测试性/DoD 剩余 P0 复核

| 项 | 内容 |
|---|---|
| 评审对象 | `reqs/specification.md`（3286 行，v0.4-draft；单文件=SRS+Annex A+§13.1.1/§13.1.2+§16） |
| 基线 | HEAD `78e5a7b`（`docs(reqs): third P0 revision and D-025/D-028 sync`）；工作区仅新增评审文档 |
| 范围 | 只核 R2 `docs/review-spec-r2-testability.md` §5.1 的 5 项 P0：T-P0-1…T-P0-5；**只评审 specification.md**，资产仅作辅助 |
| 方法 | 逐项解析 AC Given/When/Then、§16.6 注入点、§8.1 枚举、附录 D/D.2、§16.9 DoD；行号均指 `specification.md` |
| 结论 | **需再修（不可冻结）**：PASS 2（T-P0-1、T-P0-2），**FAIL 3（T-P0-3、T-P0-4、T-P0-5）** |

---

## 1. 逐项判定

### T-P0-1 AC-OP-02 / DISK_SPACE_LOW / 旧标题 —— **PASS**

- **旧标题已改**：`2341` 标题为“Clone 预检：磁盘/长路径/凭据/远程存在，**按仓阻断并汇总**”；`2346`① 明确“按仓隔离…整体 `PARTIAL` 而非‘任一失败即全部阻断’（本 AC 的‘阻断’只针对失败仓自身）”，字面矛盾已消除（全文仅 `2346` 引用该旧短语作反例）。
- **磁盘注入已冻结**：`2343` 依赖列写 `DISK_SPACE_LOW` 注入；`2346`⑥ 明确“磁盘余量通过 `MBD_FAULT_INJECT=DISK_SPACE_LOW`（预检注入点，固定返回 `E_DISK_FULL`）模拟，不用真实占满磁盘”。
- **注入点/码表闭环**：`1508` §16.6 固定注入点枚举含 `DISK_SPACE_LOW`；`2603` AC-OP-35 Given 再次列出该点；`1412`/`1431` 附录 D 登记 `E_DISK_FULL`（srsCode=FS-E001）；`2346` ②–⑤ 对 401/404/路径给出 Host 码 + `details.srsCode`。
- 残留（P1，不阻断）：`1508` 只列注入点名、未给“DISK_SPACE_LOW→Clone 预检固定 E_DISK_FULL”的逐点期望表（该语义只写在 `2346`⑥），建议在 §16.6 增一列“期望码/适用 AC”。

### T-P0-2 OP-26/27、SAFE-09 确定性注入 / CANCELLED·QUEUED / reconcile_status —— **PASS**

- **确定性注入**：`1508` 固定枚举含 `GIT_SLOW`、`CRASH_AT_ITEM_2`、`CRASH_BEFORE_WRITE`、`CRASH_AFTER_WRITE`；`1507` 注入 JSON 含 `point/action/delayMs/errorCode`；`1510` 明确“取消/崩溃类 AC 使用注入点，不以墙钟 `≤2s` 为唯一断言”，UI 改为“注入命中后状态机断言”。
- **OP-26**：`2537` 依赖 `CRASH_AT_ITEM_2`，`2538` Given“不靠墙钟/人工时机”；`2540`① 未开始 item 保持 `QUEUED` 且不重复执行、② 已完成 OK。
- **OP-27**：`2545` 依赖 `GIT_SLOW`+`CRASH_AT_ITEM_2`；`2546` 第 2 项“已开始（`GIT_SLOW` 注入使其可取消）”；`2548` 仅用 `CANCELLED`/`QUEUED`（与 `397` `job.state` CHECK、`398` `job_item.state` CHECK 一致），⑤ 删除了“≤2s”死断言。
- **SAFE-09**：`2767–2768` 用 `CRASH_AT_ITEM_2`（写前 STARTED 已落盘、git 写未发生）；`2770`② 用 `NOT_APPLIED`/`UNKNOWN_APPLIED`，job/item=`INTERRUPTED`，⑤ `CRASH_AFTER_WRITE` 单独覆盖 UNKNOWN_APPLIED。
- **观测量落点**：`398` §8.1 `job_item` 已增 `reconcile_status(CHECK NOT_APPLIED/UNKNOWN_APPLIED，可空)`，`397` `job.state` 含 `CANCELLED/INTERRUPTED/PARTIAL`，`398` `job_item.state` 含 `QUEUED/CANCELLED/INTERRUPTED` → Then 有 DB 落点。
- 残留（P1）：`2537` 依赖“任务 DB/JSON”未定死观测源；`2540`② 仍写“INTERRUPTED/FAILED”，属双值（并入 T-P0-5）。

### T-P0-3 错误码 R-06 / D.2 映射 / AC 双值 —— **FAIL**

1. **R-06 关而不清（同一文件三处矛盾）**：`1391`、`3172` 均写“R-06 于 v0.4 关闭”，但 `2939` AC-OPS-17 Then 仍要求“可重试性/日志级别（**M0 需冻结四列，见 §4.2 R-06**）”，`2940` 仍标“〔**SRS 需修订 R-06**〕”，`3267` §5.3 第 13 项仍写“**仅 R-06 仍开放：M0 出口条件**”。冻结检查项与 AC 仍把 R-06 当未决。
2. **D.2 映射不完整（AC 引用不存在的行）**：D.2 仅 `1422–1437` 15 行，缺 AC 已使用的 3 个 Host 码：
   - `2386` AC-OP-07 断言 `E_GIT_NON_FF`（`details.srsCode=GIT-E001`），D.2 无此行，且 `GIT-E001` 在 `1417` 仅表示“空仓库/unborn” → 码与语义不符；
   - `2670` AC-REPO-08 明确写“`E_REPAIR_FAILED`/`E_RECLONE_FAILED`（**附录 D D.2**）”，D.2 无这两行 → AC 指向不存在的冻结行，DoD-1 必测 AC 不可判定。
3. **AC 双值/同事件两码仍存在**：`1162` AC-SAFE-17④“**`E_ACTION_NOT_ALLOWED` 或 `E_INVALID_ARGUMENT`**”；`1765` AC-CFG-09“`CFG-E001`（**或** `FS-E001` 若为文件问题）”；`1733` AC-CFG-05 把 401 断言为 `CFG-E002`，而 `1397` 附录 D 定“`SYNC-E001`=401/403”、`1428` D.2 定 `E_AUTH_FAILED→NET-E001` → 同一事件三套码口径未裁决。

### T-P0-4 设置键 38 vs AC 32 —— **FAIL**

- 附录 D 实有 **38 个唯一键**（`1365–1387`，脚本核对 38）。
- `2854/2857` AC-OPS-07 标题与 Given 写“**38 键**”，但 `2857` 实际只列 **32 键**（缺 `ui.view`、`ui.panel.leftWidth`、`ui.panel.rightWidth`、`tree.hideDotGit`、`tree.defaultExpandDepth`、`tree.pageSize`）；`2859`② 仍断言“**32/32**”。
- `2862/2867` AC-OPS-08 又写“38 键全覆盖/键数=**38**”。→ 同一文档 38/32 矛盾未修，AC-OPS-07 与 AC-OPS-08 互斥，DoD-1（含 AC-OPS-17 与设置键类 AC）无法判定。

### T-P0-5 DoD manifest / 期望源 / 双值 AC —— **FAIL（部分闭环）**

- **manifest：PASS**。`1540` 固定路径与 schema：`docs/evidence/m2b-manifest.json`，字段 `{commit,generatedAt,suite:{name,command,startedAt,finishedAt,result,artifact},checks:[{ac,status,evidence}]}`；`1536` COVERED 定义引用该 manifest。P1：未给生成命令/生成者脚本（“CI/本机 runner 生成”），AC-OP-34（`2590–2597`）只断言 `result.json`，未断言 manifest schema。
- **期望源：PASS（附 P1）**。`1544` DoD-2 已给消费者=`RepoActionAvailabilityMatrixTests`+`ui/scripts/cdp-m2a-selfcheck.mjs`、命令、唯一期望文件 `reqs/fixtures/m2-matrix.json`、判据“与 Host/UI 一致”；§13.1.2 `1093` 的 M2a 覆盖语义（DIVERGED.PULL 禁用、EMPTY.PUSH 禁用）已注明“M2b 构建恢复矩阵”。P1：M2b 复验却指定 M2a 命名的 CDP 脚本；`vstest ... --Tests:` 不是 `vstest.console` 合法过滤语法（应为 `--TestCaseFilter:`）；矩阵文件 `status/freezeNotes` 自述“未对齐”属外部资产问题（辅助核查）。
- **双值 AC：FAIL**。DoD-1（`1543`）要求全部 COVERED 的 AC 仍保留二选一/非唯一期望：
  - `2466` AC-OP-17④“组件 `OP_PARTIAL_FAILED` **或**按清除规则”；
  - `2540` AC-OP-26②“INTERRUPTED/FAILED”；
  - `2654` AC-REPO-06①“`AHEAD`（有未推送提交）**或** `SYNCED`”；
  - （另 `2670` AC-REPO-08①“→ SYNCED/EMPTY **等**真实态”、`2506` AC-OP-22⑥“设置**或**诊断中查看”为非唯一期望。）
  → DoD-1 仍不能由第三方唯一判定。

---

## 2. 结论

**需再修（不可冻结）：2 PASS / 3 FAIL。**

- PASS：T-P0-1（AC-OP-02 标题与按仓隔离、`DISK_SPACE_LOW` 已入注入点枚举与 AC-OP-35）、T-P0-2（OP-26/27 与 SAFE-09 已改确定性注入；`CANCELLED`/`QUEUED` 合法；`reconcile_status` 已入 §8.1 且 AC 有落点）。
- FAIL：
  1. **T-P0-3**：R-06 “已关闭”与 AC-OPS-17/§5.3 仍“开放”三处矛盾；D.2 缺 `E_GIT_NON_FF`、`E_REPAIR_FAILED`、`E_RECLONE_FAILED`（其中 AC-REPO-08 明引 D.2）；AC-CFG-05 401 码冲突、AC-SAFE-17④ / AC-CFG-09 仍双值。
  2. **T-P0-4**：附录 D 38 键，但 AC-OPS-07 Given 仅列 32 键、Then 断言 32/32；AC-OPS-08 又断言 38 → 自相矛盾。
  3. **T-P0-5**：manifest 与 M2b 期望源已补齐，但 DoD-1 仍被 AC-OP-17/26、AC-REPO-06 的双值 Then 阻塞。

**最小修复（3 处）**：① 统一 R-06 状态（删 AC-OPS-17 的“M0 需冻结/M 需修订”与 §5.3 第 13 项“仅 R-06 开放”），把 `E_GIT_NON_FF/E_REPAIR_FAILED/E_RECLONE_FAILED` 补进 D.2 并修正 AC-CFG-05/AC-SAFE-17④/AC-CFG-09 为唯一码；② AC-OPS-07 Given/Then 改为 38 键并补 6 个缺失键（或与 AC-OPS-08 统一为 38）；③ 定死 AC-OP-17④、AC-OP-26②、AC-REPO-06① 的唯一期望（必要时把“或”改为表驱动条件）。

---

### 附：核查方式（只读）

- 解析 `^#### (AC-...)` 与 6 字段；逐项对拍 §16.6（`1507–1511`）、§8.1（`397–398`）、附录 D/D.2（`1361–1437`）、§16.9（`1533–1547`）、§4.2（`3163–3172`）、§5.3（`3267`）。
- 设置键：抽取附录 D 首列反引号=38 唯一键；抽取 AC-OPS-07 Given=32，差集 6 键。
- 错误码：全文抽取 Host `E_*` 实际使用集与 D.2 行比对（缺 3 码）；扫描 Then 含“或/二选一/等”的非唯一期望。
- 未运行构建/测试；未修改任何文件。
