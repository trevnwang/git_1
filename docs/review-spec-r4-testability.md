# 需求复审 R4（只读）— r3 三个 P0 的第二轮范围复核

| 项 | 内容 |
|---|---|
| 评审对象 | `reqs/specification.md`（3289 行，v0.4-draft） |
| 基线 | HEAD `13a20ab`（`docs(reqs): final P0 wording sync before scoped re-review`）；工作区仅新增评审文档 |
| 范围 | 只核 r3 `docs/review-spec-r3-testability.md` 的 3 个 P0：①R-06/错误码；②设置键 38 vs AC-OPS-07 32/32；③DoD-1 的 OP-17/26、REPO-06 双值 + manifest/期望源/命令 |
| 方法 | 只读解析 AC Given/When/Then、§16.9、§4.2、§5.3、附录 D/D.2；行号均指 `specification.md` |
| 结论 | **可冻结（P0 清零）**；遗留 P1/P2 见 §4，建议冻结前顺手清理 |

---

## 1. P0-① R-06 / 错误码 —— **PASS**

| 子项 | 判定 | 证据（行号） |
|---|---|---|
| AC-OPS-17 已统一到 v0.4 冻结表 | PASS | `2940` Given 改为“附录 D 的 srsCode 表 + D.2 Host 映射表（v0.4 冻结）”；`2942` Then ①“四列已冻结…见 D.2”；`2943` 关联写“R-06 已关闭” |
| §5.3 不再把 R-06 列为开放 | PASS | `3270` 第 13 项改为“38 已关闭”（r3 的“仅 R-06 仍开放”已删） |
| §4.2 R-06 状态 | PASS | `3166`“38 条已关闭、R-06 于 v0.4 关闭”；`3175` R-06 行“已关闭（v0.4）” |
| D.2 含 r3 指出的 3 个 Host 码 | PASS | `1438` E_GIT_NON_FF→GIT-E001；`1439` E_REPAIR_FAILED→FS-E001；`1440` E_RECLONE_FAILED→NET-E001；对应 AC：`2389`（AC-OP-07）、`2673`（AC-REPO-08） |
| AC-CFG-05 双码唯一 | PASS | `1736` 401/403 统一 `E_AUTH_FAILED`（`details.srsCode=NET-E001`），不再用 `CFG-E002` |
| AC-SAFE-17 双码唯一 | PASS | `1162` ④ 只保留 `E_ACTION_NOT_ALLOWED`，原“或 `E_INVALID_ARGUMENT`”已删 |

## 2. P0-② 设置键 38 vs AC-OPS-07 32/32 —— **PASS（残留 P2 文字）**

| 子项 | 判定 | 证据（行号） |
|---|---|---|
| AC-OPS-07 标题/断言统一 38 | PASS | `2857` 标题“38 键”；`2862` Then ② 已由 r3 的“**32/32**”改为“**38/38**” |
| AC-OPS-08 一致 | PASS | `2865` 标题“38 键全覆盖”；`2868` Given“38 键”；`2870` ⑤“键数=38” |
| 附录 D 键数 | PASS | `1368–1390` 实测 **38** 个唯一键（脚本核对），与两 AC 的断言基数一致 |
| 残留 P2 | — | `2860` Given 括注仍只枚举 **32** 键（缺 `ui.view`、`ui.panel.leftWidth/rightWidth`、`tree.hideDotGit/defaultExpandDepth/pageSize`）；因同句声明“以附录 D 为唯一清单”且 Then 为 38/38，判定不阻断，建议补齐括号内 6 键或改为“示例” |

## 3. P0-③ DoD-1 双值 + manifest/期望源/命令 —— **PASS（残留 P1）**

| 子项 | 判定 | 证据（行号） |
|---|---|---|
| AC-OP-17 双值清除 | PASS | `2469`④ 改为“组件 `OP_PARTIAL_FAILED`（skipped>0 时），清除需显式确认并写审计”，原“或按清除规则”已删 |
| AC-OP-26 双值清除 | PASS | `2543`② 改为“当前项记 `INTERRUPTED`”，原“INTERRUPTED/FAILED”已删；① 未开始 item `QUEUED` 不变 |
| AC-REPO-06 双值清除 | PASS | `2657`① 改为“`main_status` 转为 `AHEAD`（测试不执行 push）”，原“或 `SYNCED`”已删 |
| manifest 可判定 | PASS | `1543` 固定路径/schema：`docs/evidence/m2b-manifest.json` + `{commit,generatedAt,suite:{…},checks:[{ac,status,evidence}]}`；`1539` COVERED 引用该 manifest |
| 期望源可判定 | PASS | `1547` DoD-2 指定唯一期望文件 `reqs/fixtures/m2-matrix.json` + 消费者 `RepoActionAvailabilityMatrixTests`、`cdp-m2a-selfcheck.mjs` + “与 Host/UI 一致”判据 |
| 命令可判定 | PASS（残留 P1） | `1547` 给出了命令形态；但 `vstest ... --Tests:RepoActionAvailabilityMatrix` 仍为带省略号的占位/非标准过滤写法，且 `manifest` 只写“CI/本机 runner 生成”未给生成命令；建议冻结前补完整可复制命令（如 `dotnet test --filter ...` 或 `vstest.console --TestCaseFilter:`）与 manifest 生成脚本名 |

---

## 4. 结论与遗留（不阻断冻结的 P1/P2）

**P0 清零 → 可冻结。** 三项均按 r3 要求闭环：①R-06 在 AC-OPS-17/§4.2/§5.3 统一为“已关闭”，D.2 补齐 3 个缺失 Host 码，AC-CFG-05/SAFE-17 码唯一；②AC-OPS-07/08 与附录 D 均为 38 键；③OP-17/26、REPO-06 双值已清除，manifest/期望源/命令有落点。

建议冻结前清理（不阻断）：

1. **P2** `2860` AC-OPS-07 Given 括注仍列 32 键（缺 6 键）→ 与“38 键”字面不一致，补键或删枚举。
2. **P1** `1547` DoD-2 命令为占位（`vstest ... --Tests:`），manifest 生成命令/脚本未定义；M2b 复验消费者仍用 M2a 命名脚本 `cdp-m2a-selfcheck.mjs`。
3. **P1** `2942` AC-OPS-17 ① 要求“每个错误码均有可重试性/日志级别（见 D.2）”，但 D.2 只映射 Host 码；仅出现在 srsCode 表的 `SYNC-E001/E002/E003`、`SCAN-E001/E002`、`STATE-E001`、`OP-E003`、`FS-E002/E003`、`SEC-E001` 无四列，全表断言仍不完整 → 建议 D.2 补“srsCode-only”行或把 AC 范围限定为 Host 码。
4. **P2** `3166`/`3270` 仍留“（M0 出口条件：附录 D 错误码全表冻结/冻结时回写 AC）”残句，与“已关闭”并置，建议改为“R-06 已关闭（v0.4 附录 D/D.2 冻结）”。
5. **P1（非本轮范围）** `1768` AC-CFG-09 仍“`CFG-E001`（或 `FS-E001` 若为文件问题）”；`2509` AC-OP-22⑥“设置或诊断中查看”、`2633` AC-REPO-03③“按 7.6 阻断（或需 stash 确认）”等既有“或/等”项建议一并清。

---

### 附：核查方式（只读）

- 逐项读取 `2940–2943`（AC-OPS-17）、`3166/3175`（§4.2）、`3270`（§5.3）、`1438–1440`（D.2）、`1736`（AC-CFG-05）、`1162`（AC-SAFE-17）。
- 设置键：抽取附录 D 首列反引号=38 唯一键；计 AC-OPS-07 Given=32、Then=38/38。
- DoD：读取 `1539–1547`；逐条核对 AC-OP-17（`2469`）、AC-OP-26（`2543`）、AC-REPO-06（`2657`）的 Then。
- 未运行构建/测试；未修改任何文件。

