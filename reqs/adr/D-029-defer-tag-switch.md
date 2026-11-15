# D-029：切 Tag（批量 TAG）与 Tag baseline 暂定，不进入 M2b 验收

| 项目 | 内容 |
|---|---|
| 日期 | 2026-11-15 |
| 状态 | **Accepted（用户决策）** |
| 关联 | `reqs/archive/specification-v0.4.1.md` §3.3/§16.4、Annex A AC-OP-11…14；D-025；v0.4 冻结 |
| 决策 | **批量“切 Tag”功能暂不交付、不参与 M2b 验收与代码实现计划**；已有后端实现保留但 UI 禁用；`baseline_snapshot`/TAG_DRIFT 的 Tag 相关验收同暂定 |

## 1. 原因（用户说明）

各版本 Tag 的仓集合并不一致，例如：

- 1.0：DESIGN / IMPL / MIL / SIL；
- 2.0：DESIGN / IMPL / SIL；
- 3.0：DESIGN / IMPL。

因此“四仓同一 Tag”不是通用可操作语义，强制四仓切同一 Tag 会把不存在的仓判为缺 Tag/漂移，产品上不好用。需求层应将 Tag 切换暂定，而不是把“四仓同 Tag”当默认场景。

## 2. 范围

- 暂定：AC-OP-11、AC-OP-12、AC-OP-13、AC-OP-14；§16.4 Tag/baseline_snapshot 的 Tag 写入规则。
- 保留：Tag 作为 CHKOUT 的 ref 选择（单仓检出 Tag）不受影响。
- 已有代码：`BatchOperationService.TagAsync`、`BaselineSnapshotService` 保留但不作为 M2b 验收项；UI 批量 TAG 入口禁用（`implemented=false`，提示“暂不开放：各版本 Tag 仓集合不同”）。
- 不参与：M2b final Gate、AC 台账计数、故障注入报告、测试门禁的必过项。
- 后续：若要恢复，需新需求（Tag 集合按版本配置/按仓可选）并重新评审，不得直接重新启用。

## 3. 影响

- M2b 出口改写为 `AC-OP-01…10、AC-OP-15…35`（排除 11…14）。
- baseline_snapshot 的 Tag 写入不再作为 M2b 完成条件；保留代码不得在 UI 暴露。
- 测试可保留（不阻塞），但 Gate 不引用。
