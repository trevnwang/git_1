# D-026：M2b-2a 阶段 tag 的两项 P1 延后（P1-2 备份分支身份 / P1-4 审计写失败回填）

| 项目 | 内容 |
|---|---|
| ADR 编号 | D-026 |
| 日期 | 2026-11-14 |
| 状态 | **Accepted（仅用于 M2b-2a 阶段 tag）**；不改变 M2b-2b/M2 出口要求 |
| 关联 | `docs/archive/review-m2b2-code.md` P1-1…P1-5、`docs/archive/review-m2b2-gate-code.md`、`docs/archive/review-m2b2-gate.md`（B1/B2）、`docs/archive/m2b-status.md` |
| 决策 | M2b-2a 阶段 tag 允许在 P1-2（备份 ref 分支身份）与 P1-4（SQLite 写失败降级/JSONL→DB 回填）**已知未闭合**的前提下签发；两项必须在 M2b-2b 完成，不得带入 M2b/M2 完成声明 |

## 1. 背景

M2b-2a 已完成：`operation_log` SQLite + 查询/导出、备份 ref 列表/恢复（含恢复前安全备份与 clean 工作区阻断）、EMPTY 首推、保留清理。独立 Gate 判定 P0 已闭环（恢复不再静默覆盖 untracked/ignored；tracked dirty 仍阻断；文件保留、HEAD 不变、不建安全 ref），但两项 P1 未闭合：

- **P1-2 分支身份**：SYNC 备份 ref 仍为 `refs/backup/sync/<ts>/<TYPE>`，未携带分支；`restoreBackupRef` 不校验“备份来源分支 vs 当前分支”，用户可能在其它分支恢复并重置该分支指针。
- **P1-4 审计回填**：SQLite 写失败仅告警；JSONL 已写入但 DB 可能缺行；无运行时降级队列/JSONL→DB 回填；历史真实库含预隔离测试行，证据口径需更正。

## 2. 决策与理由

M2b-2a 是**阶段 checkpoint**，不是 M2b 完成；两项 P1 属于恢复精度/审计可观测性，不构成本轮 P0 数据丢失路径。为避免无限延期，按 Gate 允许的方式**书面延后到 M2b-2b**，并同时满足以下补偿条件：

1. 恢复前 clean 门禁（tracked/untracked/ignored 全阻断）、恢复前安全 ref（毫秒+GUID 唯一）、审计写 `RESTORE_BACKUP`；
2. JSONL 始终写入（SQLite 旁路失败不影响动作/审计主链路）；UI/API 不把 DB 失败伪装成成功；
3. M2b-2a tag 只标注 M2b-2a；M2b-2b 完成前不得发布 M2b/M2 完成声明；
4. `docs/archive/m2b-status.md` 更正审计行数与测试污染来源，不将历史测试行作为生产证据。

## 3. M2b-2b 必须完成

- 备份 ref 命名改为含分支身份（`refs/backup/sync/<ts>/<branch>/<TYPE>`）；旧 ref 迁移/兼容；`getBackupRefs` 返回 branch/kind；restore 校验当前分支与来源分支（不一致默认阻断或显式二次确认）。
- 审计 outbox/回填：SQLite 写失败进本地回退队列，下次启动回填 JSONL→DB；查询合并回退；修复历史测试污染并提供可审计的清理/重建入口。
- 安全 ref `refs/backup/restore/*` 在 UI/API 可见可恢复、按保留期清理。
- 完成上述后重新跑 M2b-2 Gate，才可打 `m2b-2b` 阶段 tag。

## 4. 验收

- M2b-2a：P0 闭环 + 本 ADR + 更新后的自检脚本/证据可复跑；tag 名 `m2b-2a`。
- M2b-2b：P1-2/P1-4 自动化用例全绿，Gate 报告明确“M2b-2b 通过”，但 M2b 总出口仍需 M2b-2a ∧ M2b-2b ∧ M2b-1 共同满足。
