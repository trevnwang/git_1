# D-028：M2b 增量数据/枚举变更（配合 specification v0.4 §16）

| 项目 | 内容 |
|---|---|
| 状态 | **随 v0.4 需求冻结生效**（D-025 §4 M2b 出口补充 AC-OP-34/35；实现前仍以 specification 为准） |
| 关联 | `reqs/specification.md` §8.1/§16/Annex A；D-025；D-026 |
| 目的 | 明确 §16 新增行为对 §8.1 表/枚举/写者的最小变更，避免实现时绕开冻结语义 |

## 1. 数据与写者

1. `component.is_managed/is_deleted`：语义为管理状态，不是聚合态；新增写者 `COMPONENT_MGMT`，只写该两列、INSERT component 行与 updated_at；其余 component 列仍由 `STATUS_CALC` 写。`repo.is_local/local_path` 仍由 LOCAL_SCAN 单写，管理操作不直接写。迁移：已扫描发现的四仓组件默认 `is_managed=1,is_deleted=0`。
2. `job_item.baseline_ref`：新增可空列（CREATE 基线 ref），写者 JOB_RUNNER；不改既有列语义。
3. `baseline_snapshot`：PK 保持 `(component_id, tag, repo_type)`；TAG 收口固定写 4 行；重复/移动 tag 覆盖同键行，历史由 `operation_log` 保留（before/after 旧/新 SHA）。`tag_present=0` 行由缺 Tag 仓写入。
4. `DELETE_PENDING` 不新增表/列：只使用 `<workspace>/.trash/.pending/*.json` 文件记录；不改变 §7.2/§7.4 状态枚举。

## 2. 枚举扩展

- `job.kind`：新增 `REPAIR`、`RECLONE`、`CREATE_UNDO`（逐仓执行；写 journal/审计）；`REPAIR` 在 §8.1 既有集合中，不重复新增语义。
- 审计 action：在 `job.kind ∪ {COMPONENT_BIND, COMPONENT_UNBIND, CREATE_UNDO, RECONCILE}` 范围内；`operation_log` 的 Host 实现原本不校验 CHECK，本 ADR 明确该集合为语义白名单。
- 错误码：新增 Host 码 `E_DELETE_PENDING`、`E_AUTH_FAILED`、`E_REMOTE_NOT_FOUND`、`E_NETWORK`、`E_DISK_FULL`、`E_PATH_TOO_LONG`；均映射 `details.srsCode` 到附录 D 前缀（FS/NET），附录 D 已补行。

## 3. 约束

- 不改变任何安全顺序：DELETE 四阻断、SYNC 备份→stash→reset、REPAIR/RECLONE 回滚、restore clean 门禁、确认/typed confirm 均保持。
- 新 API：`bindComponent`、`unbindComponent`（INSERT 归属 `COMPONENT_MGMT`）、`undoCreate`（创建 `CREATE_UNDO` job）、`repair`/`reclone`（runBatch kind）；均需参数校验/审计/失败零改动。
- 若实现需要改变本 ADR 范围，先改 `specification.md` + 本 ADR，再开发。
