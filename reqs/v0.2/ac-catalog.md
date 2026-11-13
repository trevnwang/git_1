# 验收标准目录（AC Catalog）— MBD 组件全生命周期 Git 工具

| 项 | 内容 |
|---|---|
| 文档 ID | MBD-GIT-AC |
| 版本 | **v0.2.1（AC 全量，待冻结；按 07 独立冻结复评回写 SRS v0.2.1 口径）** |
| 日期 | 2026-11-14 |
| 依据 | `reqs/requirements.md` SRS **v0.2.1 冻结候选**（本版按 `review/07-freeze-review.md` 回写：`BINDING_ERROR` 严重度按 `binding_cause` 拆分 / SYNC 前置态 / 聚合顺序 / CI 契约 `componentId`+数字 `repoId` / 首启中间态 `UNKNOWN+incomplete=1` / §8.1 补列与枚举；14 主状态 §7.2 / 11 flags §7.3 / 15 聚合态 §7.4 / 状态×操作 §7.6 / 16 表 §8.1 / §8.3 契约↔DDL 映射 / FR §9.1–9.11 / NFR §10 / 色板 §11.2 / CI 锚点 §11.4 / 旧 AC §13.1（仅映射） / 测量方法 §13.3 / 附录 C 夹具 / 附录 D 设置与错误码） |
| 强制输入 | `06-redteam-srs.md` 第 93–115 行（逐 AC 可测试性问题）— 本目录 §1.5 逐条给出处置；`04-ux-ci.md` §5.4 CI“不返工”验收 10 条；`03-architecture.md` §H 测试资产 |
| 文档性质 | **只读派生文档**：不修改 `requirements.md` 与任何既有评审文件；本目录只新增 `reqs/v0.2/ac-catalog.md` |
| 边界 | 只写“可执行的验收断言”，**不做实现设计**：不选库、不写代码、不定进程/IPC/DDL 物理实现；SRS 已冻结的枚举/命名/表名/错误码/状态名/文案一律照抄 |
| 规模 | 覆盖 **72 条 FR（9.1–9.11 全部）**，共 **162 条 AC**（见 §3 末尾统计；FR 优先级 P0=46 / P1=22 / P2=4）；其中 P0 FR 均含成功路径 + 关键失败/异常路径；P2 功能的 v1.1 完整行为单列 §2.12（不冻结、不计入 162 条） |
| 约定 | 与 SRS 不一致处一律显式标 **〔SRS 需修订 R-xx〕**（R 清单与状态见 §4.2），不自行发明枚举/命名；R 清单逐条标“已关闭（SRS v0.2.1 位置）/ 仍开放（M0 前修）” |

---

## 1. 使用说明与 AC 编号规则

### 1.1 使用说明

1. **本目录是 M0 冻结的验收基线**：M0 出口要求“每条 FR ≥1 条 AC、AC 可自动化判定、追溯矩阵 100% 覆盖”（SRS §3.3、§13.4、§10.6）。
2. 每条 AC 的 **Then 必须落在可程序读取的断言源**上，只允许以下 6 类：① SQLite 查询结果（表名/字段名严格按 SRS §8.1）；② Host API/IPC 响应或事件（§12.2 消息契约仅作观测载体，不作验收对象）；③ UI DOM 属性（`data-testid`/`data-state`/`aria-*`/可见文案，文案严格按 §11.2）；④ 日志与错误码（附录 D，前缀与码值照抄）；⑤ 文件系统状态（路径、文件存在性、mtime、`.trash` 目录结构）；⑥ git 命令输出（`git rev-parse`/`for-each-ref`/`status --porcelain=v2`/`cat-file` 等）。
3. **禁止词**（出现即视为该 AC 未通过冻结评审）：正常、流畅、合理、友好、炫酷、丝滑、秒级、实时、尽量、适当、大致、良好、稳定（作为形容词）、性能高、快速（无数字）。
4. 所有时间断言必须用 `IClock` 假时钟或 SRS §13.3 的测量方法（DevTools Performance / BenchmarkDotNet / `sync_run` 计时 / 进程计数），不得用 `sleep` 墙钟。
5. 每条 AC 的 Given 必须引用**夹具 ID**：附录 C `F-*`（本地 git 夹具，由 M0 交付的 `v0.2/fixtures/` 生成器产出，`manifest.json`+`expected.json`）或本目录 §1.6 定义的 `G-WIRE`/`G-E2E`/`C-SYNTH`/`CLK`。
6. 测试层级四选一：**单测 / 集成 / E2E / UI 快照 / 性能**（可叠加，如“集成+UI 快照”）。
7. M0 冻结后 **只允许“只增不改语义”**：新增 AC 用同域下一个未占用序号；修改已冻结 AC 的 Then 必须走 SRS 变更记录 + ADR（见 §5.4 变更规则）。

### 1.2 AC 编号规则

```
AC-<域>-<NN>[.<子字母>]
```

- `<域>` 取值固定 11 个（与 SRS §9 的 FR 分组一一对应，不新增）：`CFG` / `SYNC` / `SCAN` / `STA` / `BOARD` / `DETAIL` / `OP` / `REPO` / `SAFE` / `OPS` / `CI`。
- `<NN>` 为**域内两位流水号**（01–99），按首次冻结时分配；**与 FR 编号无关**，因为一条 AC 可覆盖多条 FR（如状态矩阵同时覆盖 FR-STA-01/02）。
- `<子字母>` 仅用于同一 AC 下必须分别判定、且共享同一 Given 的原子场景（如 `.a`/`.b`），子项单独计入 AC 总数统计；**本版 162 条均为独立主编号，未使用子字母**（留给 M1/M2 只增不改时拆分子场景）。
- 每条 AC 必须显式写 **覆盖 FR**；一个 FR 可被多条 AC 覆盖；**禁止**出现无 FR 归属的 AC、禁止跨域编号、禁止复用已作废编号（作废保留占位并标 `[作废]`）。
- 旧编号 `AC-01…AC-27` 不再使用，仅保留映射（§1.4）。
- **v1.1 草案 AC**：`AC-<域>-<NN>(v1.1)` 使用域内下一个未占用序号（当前 `AC-CFG-12(v1.1)`、`AC-DETAIL-10(v1.1)`），**不复用冻结主编号、不计入 162 条冻结基线统计**，M0/M1/M2 不验收；v1.1 立项时转为正式编号。

### 1.3 单条 AC 模板（固定 6 字段）

```md
#### AC-<域>-<NN> · <一句话标题>
- **覆盖 FR**：<FR-ID（可多条）>
- **测试层级/依赖**：<层级>（<Fake Gitea / WireMock / 真 Gitea / 本地 git 夹具 / IClock / BenchmarkDotNet>）
- **Given**：<前置数据夹具 ID / 环境 / 版本化数据集>
- **When**：<精确操作：命令 / 点击 / 注入事件，含参数>
- **Then**：<可观测断言：DB 字段 / API 响应 / UI 元素与文案 / 日志错误码 / 文件系统状态>
- **关联 NFR/OQ**：<NFR-ID / OQ-ID，无则写 —>
```

字段纪律：
- **Given** 必须能由脚本一键盘起（`fixtures` 生成器 + `G-WIRE` stub + `IClock` 固定时刻）；不得出现“用户手工准备”的数据。
- **When** 必须写清入口（页面/菜单/API）+ 参数值；批量操作写清选中集合与期望 `job` 行。
- **Then** 逐条断言用分号分隔，每条断言必须含“期望值”；涉及列表断言必须给**行数/顺序/键集**。
- **关联 NFR/OQ** 只填 SRS 已存在 ID；OQ 未决时在 §4.1 给出本 AC 采用的暂定口径。

### 1.4 旧 AC-01…AC-27 → 新 AC 映射表（旧编号已作废，仅映射）

> **旧编号已作废，本表仅作映射**：旧 AC 仅为节选（红队原文按当时 65 条 FR 计，直接覆盖约 20 条），映射后旧编号不再引用；同一旧 AC 被拆到多条新 AC。SRS v0.2.1 §3.3/§13.4 已改用本目录新编号（`AC-<域>-<NN>`），旧号仅存于 SRS §13.1 历史映射表；若其他资产仍出现 `AC-01…AC-27`，一律按本表 + §3 追溯矩阵解析为现行编号。M0/M1/M2/M3 出口只允许引用现行编号。本目录正文出现的“旧 AC-xx”“红队 06 AC-xx”均为历史引用，不解析为现行 AC ID。

| 旧 AC | 旧标题 | 新 AC（主） | 补充/拆分 |
|---|---|---|---|
| AC-01 | 组件识别 | AC-SYNC-01 | AC-SCAN-01、AC-SYNC-14、AC-BOARD-01 |
| AC-02 | 缺仓 | AC-SYNC-14 | AC-BOARD-06（筛选“缺仓”） |
| AC-03 | 未绑定 | AC-SYNC-13 | AC-BOARD-13、AC-SCAN-01 |
| AC-04 | 状态矩阵 | AC-STA-01、AC-STA-02 | AC-STA-03、AC-STA-14 |
| AC-05 | 无假绿 | AC-STA-04、AC-STA-09 | AC-STA-08、AC-STA-14、AC-BOARD-12 |
| AC-06 | 关系判定 | AC-STA-05 | AC-STA-06、AC-SCAN-11 |
| AC-07 | SYNC 保护 | AC-OP-15、AC-OP-16 | AC-OP-17、AC-OP-18、AC-SAFE-01/02 |
| AC-08 | 删除可恢复 | AC-OP-20、AC-OP-21 | AC-OP-22、AC-SAFE-04 |
| AC-09 | 部分失败 | AC-OP-03 | AC-OP-04、AC-OP-08、AC-OP-26 |
| AC-10 | 切 Tag | AC-OP-11、AC-OP-12 | AC-OP-13、AC-OP-14、AC-STA-11 |
| AC-11 | 首启三阶段 | AC-OPS-15 | AC-OPS-16 |
| AC-12 | CI 预留 | AC-CI-01…AC-CI-11 | — |
| AC-13 | 性能 | AC-BOARD-03、AC-BOARD-05、AC-BOARD-14 | AC-SCAN-08、AC-SCAN-09、AC-SYNC-12、AC-DETAIL-04 |
| AC-14 | 安全 | AC-SAFE-13、AC-SAFE-12 | AC-SAFE-05/06/07、AC-CFG-06/07 |
| AC-15 | 单实例 | AC-OPS-09 | AC-OPS-10 |
| AC-16 | 状态×操作矩阵 | AC-STA-15 | AC-OP-02、AC-OP-17 |
| AC-17 | CREATE 分支 | AC-OP-24、AC-OP-25 | — |
| AC-18 | 修复类操作 | AC-REPO-08/09/10/11 | AC-OP-05 |
| AC-19 | 恢复 | AC-OP-30 | AC-OP-20、AC-OP-16 |
| AC-20 | 纳入/移出 | AC-OP-31、AC-OP-32 | — |
| AC-21 | 远程未就绪 | AC-OPS-16 | AC-OPS-15、AC-STA-04 |
| AC-22 | 搜索/筛选/排序 | AC-BOARD-04、AC-BOARD-06、AC-BOARD-07 | AC-BOARD-05 |
| AC-23 | 未绑定入口 | AC-BOARD-13 | AC-SYNC-13 |
| AC-24 | 通知/设置/日志 | AC-OPS-01…AC-OPS-08、AC-OPS-17 | AC-OPS-03…AC-OPS-06 |
| AC-25 | 权限/认证过期 | AC-CFG-08 | AC-BOARD-08、AC-SYNC-03 |
| AC-26 | 外部 git 并发 | AC-OP-29 | AC-SCAN-06 |
| AC-27 | 崩溃恢复 | AC-SAFE-08、AC-SAFE-09 | AC-OP-26、AC-OPS-12 |

### 1.5 红队 06 §93–115「可测试性问题」逐条处置索引

> 红队判定 ❌ = 原 AC 不可判定；⚠ = 条件可判定；✅ = 可判定。本表证明每个不可判定项已在 §2 落地为可执行断言。表中 `AC-01…AC-13` 为 **06 红队条目编号**（非本目录现行 ID），映射见 §1.4。

| 红队条目 | 不可判定原因（06 原文要点） | 本目录处置 |
|---|---|---|
| AC-01 组件识别 ✅ | 未定义“同步完成”判据与夹具组织 | **AC-SYNC-01**：`sync_run.coverage=1.0` 且 `repo` 计数=4,000 作为“同步完成”门控；`G-WIRE` 80 页夹具 + `component_id` 精确断言（不出现 `CF325F`） |
| AC-02 缺仓 ⚠ | OQ-03 未决；“提示”形式/位置未定义；批量作用范围未列 | **AC-SYNC-14**：组件 `aggregate_status=BINDING_ERROR`、缺失仓 chip 文案“缺失”、`job.target_json` 只含 3 个存在仓；OQ-03 暂定口径见 §4.1 |
| AC-03 未绑定 ⚠ | `_SIL_Design` 例外/大小写冲突/空仓未覆盖；入口未定义 | **AC-SYNC-13**（`_SIL_Design`/`Y_Design2`/`X_Docs` 全进 `unbound_repo`）；**AC-BOARD-13**（入口=P1 看板“未绑定仓库”抽屉） |
| AC-04 状态矩阵 ❌ | 无夹具清单/预期映射；未覆盖 flags 与 15 聚合态；矩阵非交付物 | **AC-STA-01/02/03**：以附录 C 25 个 `F-*` 为可执行清单，`expected.json` 表驱动 `primary`/`aggregate`/`flags_json` 三断言；property-based 随机提交图断言“恰好 1 主状态 + 必然落 15 态之一 + 未命中落 UNKNOWN” |
| AC-05 无假绿 ❌ | “超 TTL”无值；STALE 非组件态；BINDING_ERROR 遮蔽 UNKNOWN 未裁决；时间格式未定义 | **AC-STA-09**（TTL=90s/1200s/25200s，来源 §7.1）；**AC-STA-04**（任一仓 UNKNOWN ⇒ 组件非 SYNCED）；**AC-STA-08**（时间格式=本地化 `数据 X 前`，源字段 `local_state.last_success_at`/`remote_ref.observed_at`）；遮蔽裁决见 AC-STA-04（R-15 已由 SRS v0.2 §7.4 关闭） |
| AC-06 关系判定 ✅ | 未写明“与 fetch+merge-base 一致”的基准方法 | **AC-STA-05**：先 `fetch` 再对 `compare_ref` 跑 `merge-base --is-ancestor` 的 0/1/>1 三退出码对拍；“未推送不调用 API”由 **AC-STA-06** 用 `G-WIRE` 请求记录断言 |
| AC-07 SYNC 保护 ⚠ | “丢失清单”字段未定义；恢复无 FR/入口；备份/stash 顺序未写 | **AC-OP-16**：预检表行字段 `未推送 commit 列表`/`untracked 清单`/`stash 条数`/`影响仓数` 逐字段断言；恢复入口=任务中心 `refs/backup/sync/<ts>/<branch>`（**AC-OP-30**）；顺序=fetch→backup ref→stash→reset，且外部改动时失败且零改动（**AC-OP-15**） |
| AC-08 删除可恢复 ⚠ | `.trash`/回收站二义；保留期冲突；恢复入口未定义；远端零变更无检查方法 | 统一 `.trash`+30 天（D-017）；**AC-OP-20** 断言 `.trash/<ts>/<full_name>` 目录；**AC-OP-30** 恢复；**AC-SAFE-04** 用 Mock 断言 Gitea 请求记录**无任何写方法** |
| AC-09 部分失败 ✅ | OP_PARTIAL_FAILED 清除条件/入口未定义；重试失败项契约未写 | **AC-OP-03**：`job.succeeded=2/failed=2` → `aggregate_status=OP_PARTIAL_FAILED`；清除需“确认清除”动作并写 `operation_log`；**AC-OP-26** 断言“仅重试失败项”生成的 `job.target_json` 仅含 2 个失败 `repo_id` |
| AC-10 切 Tag ❌ | TAG_DRIFT 依赖未定义基线模式；7.4 无 DETACHED 映射；“1 仓报告缺失”歧义 | 基线模式已作废（D-010）→ **AC-STA-11** 断言“无条件计算”；**AC-OP-12** 明确夹具口径：4 仓均存在、第 4 仓 `main_status=SYNCED` 且无 v1.0 tag → 期望 `TAG_DRIFT`；**AC-OP-14** 断言 `tag_present=0` 行 |
| AC-11 首启 ❌ | 双阶段 vs 三阶段；阶段名/total 来源未定义；跳过降级后看板态未定义 | 统一三阶段（D-016）；**AC-OPS-15** 断言 `progress` 事件序列 `LOCAL_DISCOVERY→REMOTE_RECONCILE→STATUS_CALC` + 跳过远程后看板为只读降级 + 数据截至时间；**AC-OPS-16** 断言每阶段 `{stage,total,current,cancellable}` |
| AC-12 CI 预留 ⚠ | 5 条验收未编号；契约与 DDL 不一致；无 CI 新鲜度锚点 | **AC-CI-01…AC-CI-11**（含 §11.4 的 5 条 + 04 §5.4 的 10 条映射）；新鲜度=**AC-CI-11**；DDL 对齐=**AC-CI-09**（已按 SRS v0.2 契约回写，R-16 关闭） |
| AC-13 性能 ❌ | 无测量方法/工具/门禁；4,000 组件口径与 NFR 冲突 | 全部引用 §13.3：**AC-BOARD-14**（首屏 LCP≤1.5s、冷启动≤5s，两档：正常 1,000 组件/4,000 仓、压测 4,000 组件/16,000 仓）；**AC-BOARD-03**（滚动 10s 采帧 ≥58fps、单帧 ≤33ms、DOM≤120/≤15,000）；**AC-BOARD-05**（防抖 120ms、p95≤300ms）；固定 8C/16G/SSD/Win11 并记录杀软/磁盘型号 |
| AC-14 安全 ❌ | “全量”范围未定义；既有仓 `.git/config` token 不可能清零；诊断包/转储未纳入 | **AC-SAFE-13**（header-only：WireMock 断言仅在 `Authorization: token …` 头、命令行/remote URL 无凭据）；**AC-SAFE-12**（既有仓内嵌 token → `SEC-E001` 告警 + UI 脱敏展示 + **不自动改**）；**AC-SAFE-06/07**（诊断包/转储脱敏）；扫描规则=**AC-SAFE-05** 五类正则样本 |
| AC-15 单实例 ✅ | 崩溃后 stale lock 未覆盖 | **AC-OPS-09**（双开）+ **AC-OPS-10**（PID/时间/进程存活三条件自愈） |

> 06 第 115 行“其他可测试性缺陷”处置：①“kill -9”改为 **Windows 强制终止进程**（验收只断言重启后不猜测成功；具体终止机制不属验收，见 AC-SAFE-09）；②视觉回归“≥12 张”改为 **AC-BOARD-15** 的“6 仓状态 × 6 CI 状态关键组合 ≥12 张 + 15 聚合态全覆盖”；③深色主题=**AC-BOARD-15** 的 `prefers-color-scheme: dark` 快照；④性能测试机=§13.3 写死并在报告注明杀软/磁盘；⑤Fake Gitea 选型冻结为 **`G-WIRE`（WireMock.NET 契约）+ `G-E2E`（真 Gitea 单二进制）** 双资产，见 §1.6。

### 1.6 测试依赖代号（本目录定义，映射 SRS §10.6/§13.2 与 03 §H）

| 代号 | 资产 | 说明 |
|---|---|---|
| `G-WIRE` | WireMock.NET 契约夹具 | 回放 Gitea REST：分页（`Link`/`x-total-count`）、401/403/404/429/5xx、超时、慢响应、无 ETag 两种版本；记录全部请求（方法/URL/头）供断言 |
| `G-E2E` | Gitea 单二进制 fixture | `gitea.exe web` + 内置 SQLite；组织 `MBD`；两档数据：E2E-NORMAL（=1,000 组件 / 4,000 仓）与 E2E-STRESS（=4,000 组件 / 16,000 仓）；可打 Tag/推 commit/删仓/改名 |
| `F-*` | 本地 git 夹具（附录 C） | 由 M0 交付的 `v0.2/fixtures/` 生成器产出；`manifest.json` 给构造步骤，`expected.json` 给 `primary`/`aggregate`/`flags_json` 期望；覆盖 14 主状态 + 11 flags + 15 聚合态 |
| `C-SYNTH` | 合成数据集生成器 | 版本化生成 4,000 组件/16,000 仓元数据 + 400 本地仓（§13.3），每次测量报告生成器版本 |
| `CLK` | `IClock` 假时钟 | 可手动推进；用于 15s/10min/TTL/退避曲线断言 |
| `BENCH` | BenchmarkDotNet 夜间基线门禁 | DB 批写、扫描、聚合纯函数；门禁写入 CI |

环境基线（所有 E2E/性能 AC 共用）：Windows 11 x64、8C/16G/SSD、1440×900/100%、WebView2 Evergreen、.NET 10、Git for Windows ≥2.40、DB 在 `%LOCALAPPDATA%\MbdGitTool\`。

### 1.7 引用与标注约定

- 枚举/状态名/表名/字段名/错误码/设置键/UI 文案：**照抄 SRS**（如 `DETACHED_TAG`、`repo_status.main_status`、`OP-E002`、`delete.retentionDays`、`远程仓库不受影响`）。
- 凡 SRS 未定义而验收必须用到的取值（如 `job_item.state` 取值集），本目录暂用括注名并标 **〔SRS 需修订 R-xx〕**；M0 冻结时以修订后的 SRS 为准同步替换。
- `〔SRS 需修订 R-xx〕` 汇总见 §4.2；未决产品口径（OQ）见 §4.1。
- **需求/方案分层（D-008）**：§12 为设计建议，**不进入验收对象**。AC 只断言可观测行为（DB 查询/UI DOM/FS/git 输出/日志错误码）；CAS 三元组的具体形式、`GIT_ASKPASS` 通道、Job Object 杀进程树等实现机制仅作说明，不写进 Then；实现替换不得改变 AC 语义。

---

## 2. 全量 AC 目录

> 每条 FR 至少 1 条 AC；P0 FR（SRS §9 优先级列）均覆盖成功 + 关键失败/异常路径。测试层级缩写：单测=UT、集成=IT、E2E、UI 快照=VIS、性能=PERF。

### 2.1 配置与认证（FR-CFG）

#### AC-CFG-01 · 向导三必填项的成功路径与字段级校验
- **覆盖 FR**：FR-CFG-01
- **测试层级/依赖**：IT + VIS（`G-WIRE`、`CLK`）
- **Given**：全新 `%LOCALAPPDATA%\MbdGitTool`（无 app.db/config.json）；`G-WIRE` 返回 `GET /api/v1/version` 与 `GET /api/v1/user` 的 200；`CLK=2026-11-14T00:00:00Z`。
- **When**：启动应用 → 向导第 1 步输入 `server.baseUrl=https://gitea.mbd.local`、`server.org=MBD`；第 2 步输入 PAT 并点“测试连接”；第 3 步选择 `workspace.root=D:\MBD\Workspace`，点“完成”。
- **Then**：① 每步“下一步/完成”仅在当前步校验通过后 `enabled`；② 输入 `http://gitea.mbd.local`（非 HTTPS）→ 字段下方 `data-error-code="CFG-E001"` 且“下一步”保持 `disabled`；③ 空 org → `CFG-E001` + 提示含“组织”；④ 完成时 `config.json` 与 `app_setting` 写入 `server.baseUrl`/`server.org`/`workspace.root` 三键；⑤ 无明文 token（见 AC-CFG-06）。
- **关联 NFR/OQ**：NFR-COMPAT-02/03；OQ-19（Win10 不计入）

#### AC-CFG-02 · 根目录专项校验：盘符根/用户目录根/系统目录/不存在目录/多根
- **覆盖 FR**：FR-CFG-01、FR-CFG-06（单根约束，D-013）
- **测试层级/依赖**：IT（本地 FS 夹具）
- **Given**：可写测试目录 `T=`（含中文/空格路径 `D:\MBD 测试\工作区`）、当前用户目录 `C:\Users\<me>`、系统目录 `C:\Windows`；`G-WIRE` 在线。
- **When**：向导第 3 步依次填写下列 5 个值并尝试“完成”：`D:\`、`C:\Users\<me>`、`C:\Windows`、`E:\NotExist`、`D:\MBD 测试\工作区`。
- **Then**：① 前 4 个均被拒：`CFG-E001` + 字段级文案（`data-error-code="CFG-E001"`），且 `app_setting.workspace.root` 未写入；② 第 5 个通过并写入；③ 再次打开设置页，`workspace.root` 控件为**单选**（`data-multi-root="false"`），UI 无“添加第二个根”入口（多根=v1.1，OQ-17 暂定口径）；④ 中文/空格路径可完成，`repo.local_path` 保留原始大小写与空格。
- **关联 NFR/OQ**：NFR-COMPAT-04；OQ-17

#### AC-CFG-03 · 向导可后退且不丢已填值
- **覆盖 FR**：FR-CFG-01
- **测试层级/依赖**：IT（`G-WIRE`）
- **Given**：向导处于第 3 步且前两步已通过校验。
- **When**：点“上一步”两次→在第 1 步修改 `server.org=MBD2`→点“下一步”两次回到第 3 步。
- **Then**：① 第 2 步 PAT 输入框为空（**不回填**任何凭据字符，`type=password` 且 value 为空）；② 第 3 步 `workspace.root` 保留原值；③ 步骤条 `data-step` 依次为 3→2→1→2→3；④ 第 1 步 org 已更新为 `MBD2` 且再测试连接时请求 URL 为 `/api/v1/orgs/MBD2/repos`。
- **关联 NFR/OQ**：NFR-COMPAT-02；—

#### AC-CFG-04 · “测试连接”成功：认证结果/组织可见仓数/权限范围
- **覆盖 FR**：FR-CFG-02
- **测试层级/依赖**：IT（`G-WIRE` 双模式：Mock 与真 Gitea `G-E2E`）
- **Given**：`G-WIRE` 预置：`GET /api/v1/user` → `{login:"dev01"}`；`GET /api/v1/orgs/MBD/repos?limit=50` → 200 且 `x-total-count=4000`；token scope=`read:repository, write:repository`。
- **When**：向导第 2 步点“测试连接”。
- **Then**：① 请求头含 `Authorization: token <PAT>`（`G-WIRE` 请求记录断言，大小写不敏感前缀）；② UI 展示三项且 `data-testid="conn-auth"` 文案含 `dev01`、`data-testid="conn-repo-count"`=`4000`、`data-testid="conn-scope"` 含 `read:repository` 与 `write:repository`；③ 组件状态 `data-state="success"`；④ 同一用例在 `G-E2E` 真实模式下得到相同三项（HTTP 200 + 组织计数一致）。
- **关联 NFR/OQ**：NFR-REL-01；OQ-15（配额不改变本 AC）

#### AC-CFG-05 · “测试连接”失败：401/403 与缺失权限清单
- **覆盖 FR**：FR-CFG-02、FR-CFG-04
- **测试层级/依赖**：IT（`G-WIRE`）
- **Given**：`G-WIRE` 两套 stub：① `GET /api/v1/user` → 401；② 200 但 token scope 仅 `read:user`（无 `read:repository`），`/orgs/MBD/repos` → 403。
- **When**：分别点“测试连接”。
- **Then**：① 401 场景：`data-error-code="CFG-E002"`，UI 含“重新生成最小 scope Token”入口；`app_setting` 不写认证状态；② 403 场景：`CFG-E002` + 缺失权限清单**逐项列出** `read:repository`、`write:repository`（缺失项数=2）；③ 两场景均**不**弹出成功态、不进入下一步；④ 错误码与文案分离：文案本地化字符串变化后错误码不变（`data-error-code` 仍 `CFG-E002`）。
- **关联 NFR/OQ**：NFR-REL-02；—

#### AC-CFG-06 · Token 明文零泄漏（日志/配置/命令行/remote URL/诊断包）
- **覆盖 FR**：FR-CFG-03、FR-SAFE-03、FR-SAFE-06
- **测试层级/依赖**：IT + 安全扫描（`G-WIRE`、真 git 夹具）
- **Given**：全流程跑通 1 次配置 + 1 次 clone（token=`ghp_CANARY_9f8e7d6c5b4a`，唯一哨兵字符串）；开启 Verbose 日志。
- **When**：执行 AC-CFG-01、AC-OP-01 后，对下列产物做全量正则扫描（哨兵 + `token=`/`access_token=`/`Authorization` 值/userinfo）：`%LOCALAPPDATA%\MbdGitTool\**`（config.json/日志/审计/诊断包）、克隆仓 `.git/config`、进程命令行快照（`Get-CimInstance Win32_Process` 采集全部命令行）、网络抓包（仅 `G-WIRE` 记录的头）。
- **Then**：① `config.json` 无哨兵、无 `token` 明文字段，只含引用键与尾 4 位（`…b4a` 之外不出现完整值）；② 日志/审计/诊断包中哨兵出现次数=0（正则扫描输出空）；③ 克隆仓 `.git/config` 的 `[remote "origin"] url` 不含 userinfo 与 token；④ 进程命令行不含哨兵；⑤ 抓包记录中哨兵只出现在 `Authorization` 请求头值；进程 argv、落盘的环境变量/脚本、日志与 `.git/config` 中哨兵出现次数=0（凭据通道实现机制不属验收，见 D-008）。
- **关联 NFR/OQ**：NFR-COMPAT-07、SRS §10.3/D-006；—

#### AC-CFG-07 · 凭据存储契约：只存引用，可撤回
- **覆盖 FR**：FR-CFG-03
- **测试层级/依赖**：IT（Windows 凭据 API 读取器）
- **Given**：已完成 AC-CFG-01 配置，PAT=`ghp_CANARY_9f8e7d6c5b4a`。
- **When**：① 用 Windows Credential Manager 查询 target 形如 `MbdGitTool/Gitea/{host}/{user}` 的条目；② 在设置页点“退出登录/清除凭据”；③ 再次查看配置与凭据。
- **Then**：① 凭据条目存在且值为 PAT（仅测试进程可读）；② `config.json` 仅存引用（target 字符串）+ 尾 4 位；③ 清除后凭据条目不存在，`app_setting` 认证态=`signed_out`，后续请求返回 `SYNC-E001`（401）且 UI 出现重登入口。
- **关联 NFR/OQ**：D-006；—

#### AC-CFG-08 · 401 全局提示 + 重登后自动续跑 + 数据不清空
- **覆盖 FR**：FR-CFG-04
- **测试层级/依赖**：IT + E2E（`G-WIRE` 401 注入、`G-E2E`）
- **Given**：看板已加载 1,000 组件（`repo` 1,000 行，`component` 1,000 行）；`G-WIRE` 令全部 API 返回 401（`SYNC-E001`）。
- **When**：触发一次手动刷新（看板“刷新”按钮）→ 观察状态栏与看板 → 点“重新登录”并输入有效 PAT → 点“重试”。
- **Then**：① 全局横幅出现，`data-error-code="SYNC-E001"`，含“重新登录”按钮；② 看板数据行数不变（1,000 行）、不清空、不显示空态；③ 进行中的 job 的 `job.state` 保持 `RUNNING`（SRS §8.1 合法值；不得置 FAILED/CANCELLED，`job`/`job_item` 行不删除、进度不清零），任务中心该 job 显示“等待认证”且进度不前进（SRS v0.2.1 未新增暂停态或暂停原因列，故不引用不存在的枚举/字段）；④ 重登后同一 job 自动续跑并 `job.state` 终态为成功类，`repo`/`component` 行数与刷新前一致。
- **关联 NFR/OQ**：NFR-REL-01/02；—

#### AC-CFG-09 · 代理与自签证书连通；`sslVerify=false` 被拒
- **覆盖 FR**：FR-CFG-05
- **测试层级/依赖**：IT（`G-WIRE` + 自签证书本地 Gitea 端点）
- **Given**：内网自签 Gitea 端点 `https://gitea.lab.local`（自签 CA `lab-ca.pem`）；代理 `http://proxy.lab:8080` 可转发。
- **When**：① `proxy.mode=manual`、`proxy.url=http://proxy.lab:8080` → 测试连接；② `tls.extraCa=lab-ca.pem` → 测试连接；③ 尝试保存 `sslVerify=false` 或 `git config http.sslVerify=false`。
- **Then**：① 经代理请求成功（`G-WIRE` 记录 `Via`/代理地址），连接测试 200；② 配置 `tls.extraCa` 后 200；③ ③ 被拒：`CFG-E001`（或 `FS-E001` 若为文件问题）+ UI 提示“仅支持显式信任自定义 CA，不支持关闭校验”；④ 应用 `git config --global --get http.sslVerify` 前后均为未设置（工具不改系统级配置）。
- **关联 NFR/OQ**：SRS §12.6（禁止 sslVerify=false 兜底）、NFR-COMPAT-02；—

#### AC-CFG-10 · 配置热更新（组织/根目录变更）：v1 只断言入口不存在/不可用
- **覆盖 FR**：FR-CFG-06（P2，v1.1；v1 只冻结“入口不存在/不可用”，见 §4.1 OQ-24）
- **测试层级/依赖**：IT + VIS（`G-WIRE`、本地 FS 夹具）
- **Given**：已加载组织 `MBD`（1,000 组件）与根 `D:\MBD\Workspace`（100 个本地仓）。
- **When**：① 打开设置页的通用/工作区分区；② 尝试修改 `server.org` 或 `workspace.root`；③ 经 IPC 请求改写 `app_setting` 上述两键；④ 直接改 DB 后重启。
- **Then**：① v1 无“热更新/重同步/重扫”确认对话框与变更生效路径（DOM 无该弹层、IPC 无对应命令，不得出现死按钮）；② 设置页 `server.org`/`workspace.root` 控件为只读或不可见（`data-readonly="true"` 或不存在），并给出 tooltip“v1 仅支持首次配置”；③ 经 IPC 改写被拒（`CFG-E001`），`app_setting` 旧值不变；④ DB 直改后下次启动检测到配置漂移并提示重新配置，不得静默按新组织/新根重扫；⑤ 上述操作后 `component`/`repo` 行数不变、不产生 `sync_run`。
- **关联 NFR/OQ**：OQ-24（P2 版本归属）；完整行为见 §2.12 v1.1 AC（不冻结）

#### AC-CFG-11 · 向导门控：未通过“测试连接”不可完成配置
- **覆盖 FR**：FR-CFG-01、FR-CFG-02
- **测试层级/依赖**：IT（`G-WIRE`）
- **Given**：向导填写合法 BaseURL/org/PAT 但从未点“测试连接”（或测试连接返回 401）。
- **When**：直接点第 3 步“完成”。
- **Then**：① “完成”按钮 `disabled` 且 `aria-disabled=true`；② 提示“请先通过连接测试”（`data-testid="conn-required"`）；③ `app_setting` 未写入、不产生 `sync_run`、看板不加载；④ 补点测试连接成功后按钮 `enabled`，完成后进入首扫三阶段（见 AC-OPS-15）。
- **关联 NFR/OQ**：NFR-COMPAT-02；—

### 2.2 远程元数据同步（FR-SYNC）

#### AC-SYNC-01 · 4,000 仓清单分页拉全、覆盖率 100% 并在 UI 可见
- **覆盖 FR**：FR-SYNC-01、FR-SYNC-03、FR-SYNC-04
- **测试层级/依赖**：IT + E2E（`G-WIRE` 80 页夹具 / `G-E2E` E2E-NORMAL）
- **Given**：`G-WIRE` 提供 4,000 个仓库分页响应（每页 50，`x-total-count=4000`，`Link rel="next"` 链共 80 页），默认分支各不同（含 `main`/`develop`/`master` 混合）；`CLK` 固定。
- **When**：完成 AC-CFG-01 后触发首启同步（或点“立即同步”），等待同步终态。
- **Then**：① 请求页数=80、无重复页（`G-WIRE` 记录去重后 page=1..80）；② `repo` 表行数=4000，`repo_id` 无重复；③ `sync_run` 最近一行 `total=4000`、`ok=4000`、`failed=0`、`coverage=1.0`；④ 看板/状态栏显示“覆盖率 100%”且 `data-testid="sync-coverage"`=`100%`；⑤ 组件数=`component` 行数（按 D-001 规则计算，示例数据集 1,000）。
- **关联 NFR/OQ**：NFR-PERF-06、FR-SYNC-01；OQ-05

#### AC-SYNC-02 · 清单字段落库完整性与空仓标记
- **覆盖 FR**：FR-SYNC-01
- **测试层级/依赖**：IT（`G-WIRE` 单页夹具）
- **Given**：`G-WIRE` 返回 3 仓：`A_Design`（`empty=false`、`default_branch=main`、`archived=false`）、`A_Impl`（`empty=true`、`default_branch=develop`）、`A_Docs`（无生命周期后缀）。
- **When**：执行一次远程清单同步。
- **Then**：① `repo` 行含 `full_name`/`remote_url`/`default_branch`/`archived`/`is_local=0` 且 `repo_id` 等于 Gitea 数字 id；② `A_Impl` 的 `repo` 行标记为空仓（`empty` 语义落到 `local_state`/扫描后 `main_status=EMPTY`，依据 §7.2）；③ `A_Docs` 不进入 `repo`（不参与绑定）而进入 `unbound_repo`；④ 观察时间字段写入（`observed_at`）且非空。
- **关联 NFR/OQ**：FR-SYNC-01；—

#### AC-SYNC-03 · 清单部分失败：覆盖率 <100%、STALE 与失败清单
- **覆盖 FR**：FR-SYNC-01、FR-SYNC-04
- **测试层级/依赖**：IT（`G-WIRE` 第 3 页返回 500；第 4 页起 200）
- **Given**：4,000 仓分页夹具，注入第 3 页 500（重试后仍失败）。
- **When**：执行同步并在弹层查看失败清单。
- **Then**：① `sync_run` 行 `total=4000`、`ok<4000`、`failed>0`、`coverage<1.0`，`error` 含 `SYNC-E003`；② 看板顶部出现 `stale` 提示（`data-stale="true"`），文案含“数据截至 HH:mm”；③ 失败清单可展开，逐条显示页码/仓库/错误码；④ 其余成功页的 `repo` 行保留、不被清空；⑤ 依赖覆盖率的状态标记 STALE（见 AC-STA-09）。
- **关联 NFR/OQ**：SYNC-E003、NFR-REL-01；—

#### AC-SYNC-04 · “关注”范围 = 本地已绑定 ∪ 当前视图可见，不全量拉 4,000 仓分支/Tag
- **覆盖 FR**：FR-SYNC-02
- **测试层级/依赖**：IT（`G-WIRE` 记录请求集合）
- **Given**：4,000 仓清单已同步；本地已绑定 100 仓；当前看板视图可见 50 组件（200 仓）；其余 3,700 仓未绑定不可见。
- **When**：触发 L4 refs 同步（30min 周期或按需）。
- **Then**：① 分支/Tag 请求的 `repo_id` 集合 = 本地已绑定 100 仓 ∪ 可见 50 组件对应 200 仓（去重 ≤300）；② 未绑定且不可见的仓（≥3,400 个）**零请求**；③ `remote_ref` 行只覆盖上述集合；④ UI 覆盖率展示区分“清单覆盖率 100% / refs 覆盖率 n%”（两字段独立）；⑤ **失败路径**：令其中 20 仓 refs 请求返回 500 → refs 覆盖率=(280/300) 且失败仓清单可见，依赖这些仓的关系类状态标 `UNKNOWN`/`STALE`，其余 280 仓 refs 请求成功且 `remote_ref` 行数=280；不回退为“全量拉 4,000 仓”。
- **关联 NFR/OQ**：FR-SYNC-02、NFR-PERF-06；OQ-15

#### AC-SYNC-05 · tag→commit 与分支 head 落 `remote_ref`（含 annotated tag 解引用）
- **覆盖 FR**：FR-SYNC-02、FR-OP-04
- **测试层级/依赖**：IT（`G-E2E` 真 Gitea；annotated tag 夹具）
- **Given**：`G-E2E` 仓 `A_Design`：分支 `main` head=`c1`；轻量 tag `v1.0`→`c2`；annotated tag `v2.0`（tag 对象）指向 `c3`。
- **When**：同步该仓 refs。
- **Then**：① `remote_ref` 行：`(repo_id,'BRANCH','main').commit_hash=c1`；`(repo_id,'TAG','v1.0').commit_hash=c2`；`(repo_id,'TAG','v2.0').commit_hash=c3`（**解引用到 commit**，非 tag 对象 sha）；② 每行 `observed_at` 非空、`synced_at` 非空；③ `ref_type` 仅取 SRS v0.2.1 §8.1 CHECK 集 `BRANCH`/`TAG`（冻结，R-21 关闭）。
- **关联 NFR/OQ**：FR-SYNC-02、§9.7 FR-OP-04；R-21 已关闭（SRS v0.2.1 §8.1 `ref_type CHECK BRANCH/TAG`）

#### AC-SYNC-06 · 首启立即同步 + 三层周期编排（10min 水位 / 30min refs / 每日全量）
- **覆盖 FR**：FR-SYNC-03
- **测试层级/依赖**：IT（`G-WIRE`、`CLK`）
- **Given**：`CLK=2026-11-14T00:00:00Z`；已完成配置。
- **When**：启动应用（首启）→ 连续推进 `CLK`：+10min、+20min、+30min、+24h。
- **Then**：① 首启后立即产生 1 次清单同步（`sync_run` 行 +1，`kind` 取 SRS §8.1 `sync_run.kind`：ORG_LIST/REFS/FULL_RECONCILE）；② `CLK+10min` 触发清单水位同步（请求 `sort=updated&limit=50`，读到水位即停），不触发全量 refs；③ `CLK+30min` 触发变化仓 refs；④ `CLK+24h` 触发全量对账（`sync_run.total=4000`）；⑤ 各职责失败不阻塞其他职责（注入清单失败后 refs 仍运行）。
- **关联 NFR/OQ**：FR-SYNC-03、NFR-PERF-06；OQ-12

#### AC-SYNC-07 · 断网首启降级：退避重试 ≤2、恢复后自动续跑
- **覆盖 FR**：FR-SYNC-03
- **测试层级/依赖**：IT + E2E（`G-WIRE` 断网模拟 / 停 `G-E2E`）
- **Given**：首启时网络不可达（全部请求超时）。
- **When**：观察重试曲线与 UI → 恢复网络 → 点“重试同步”。
- **Then**：① 同一同步轮失败重试次数 ≤2（`G-WIRE` 记录尝试次数=3 含首次，或实现计数断言 ≤2 重试）；② UI 进入离线降级态：`data-degraded="offline"`、`NET-E001`、显示“数据截至 HH:mm”、本地扫描仍运行；③ 恢复后自动续跑，`sync_run.coverage=1.0`；④ 降级期间 `repo` 不被清空、组件不显示 SYNCED（见 AC-STA-04）。
- **关联 NFR/OQ**：NFR-REL-01、NET-E001；—

#### AC-SYNC-08 · `sync_run` 记录字段与覆盖率计算正确
- **覆盖 FR**：FR-SYNC-04
- **测试层级/依赖**：IT（`G-WIRE` 3 仓，1 仓 500）
- **Given**：清单 3 仓，第 2 仓详情请求 500（重试后失败）。
- **When**：完成同步。
- **Then**：① `sync_run` 行 `total=3`、`ok=2`、`failed=1`、`coverage=0.666…`（按 ok/total，保留定义）；② `started_at`/`finished_at` UTC ISO-8601、`duration_ms` ≥0（SRS v0.2.1 补列；旧 `duration` 字段不存在）；③ `error` 为空或含第 2 仓错误；④ 同一轮失败重试不新增 `sync_run` 行（仍为 1 行）。
- **关联 NFR/OQ**：SYNC-E003；—

#### AC-SYNC-09 · 远程改名 → `renamed_from` + 组件 ID 迁移事件（需用户确认，不静默删行）
- **覆盖 FR**：FR-SYNC-05
- **测试层级/依赖**：IT（`G-E2E` 改名 + `G-WIRE` 同名夹具）
- **Given**：`G-E2E` 已有组件 `A`（4 仓 `A_Design`…）；把 `A_Design` 改名为 `B_Design`（其余 3 仓仍 `A_*`）。
- **When**：执行一次清单同步。
- **Then**：① `repo` 行中该仓 `repo_id` 不变、`full_name=B_Design`、`renamed_from=A_Design`；② 生成组件 ID 迁移事件/待确认项，UI 列出旧 ID→新 ID（`data-testid="rename-event"`）；③ 用户点“暂不迁移”时 `component` 行保留旧 ID 且 `is_deleted=0`，无重复组件；④ 点“确认迁移”后 `component` 行数不变（旧 ID 不并存两个），`operation_log` 记迁移；⑤ 全过程无静默删行（前后 `component` 行数差=0）。
- **关联 NFR/OQ**：FR-SYNC-05；R-08 已关闭（SRS v0.2 §8.1 `repo.renamed_from` 已列）

#### AC-SYNC-10 · 远程删除 → `archived` 标记 + `remote_gone` flag + 禁 push
- **覆盖 FR**：FR-SYNC-05
- **测试层级/依赖**：IT + E2E（`G-E2E` 删仓，本地仍有克隆）
- **Given**：本地已克隆 `A_SIL`；在 `G-E2E` 删除该仓。
- **When**：执行一次清单同步 → 打开组件详情 → 尝试对该仓执行 Push。
- **Then**：① `repo.archived=1`（或删除态标记，按 §8.1 `archived`），`repo_status.flags_json` 含 `remote_gone`；② 组件详情该仓显示“远程仓库已删除”告警（`data-flag="remote_gone"`）；③ Push 被禁止并返回明确错误码（`OP-E004` 或 `GIT-*` 前缀），仓库零改动（`git rev-parse HEAD` 前后一致）；④ 提示清理入口存在。
- **关联 NFR/OQ**：§7.3 `remote_gone`；—

#### AC-SYNC-11 · 增量策略：`updated_at` 水位为主；ETag 可用则用、不可用不依赖
- **覆盖 FR**：FR-SYNC-06
- **测试层级/依赖**：IT（`G-WIRE` 两套：有/无 ETag）
- **Given**：`G-WIRE-A` 支持 `ETag/If-None-Match`（304 响应）；`G-WIRE-B` 不返回 ETag。
- **When**：.a 对 A 连续同步两次（第二次数据未变）；.b 对 B 连续同步两次。
- **Then**：.a ① 第二次请求携带 `If-None-Match`；② 304 时 `repo` 行 `updated_at`/`observed_at` 按水位语义处理且不重复写 refs；③ `sync_run` 行 `coverage=1.0`、`failed=0`。  .b ④ 第二次请求不带条件头，仅按 `updated_at` 水位判断变化仓，**仍成功完成增量**（无 304 也不报错）；⑤ 两模式增量后的 `remote_ref` 结果一致。
- **关联 NFR/OQ**：FR-SYNC-06、§12.4；—

#### AC-SYNC-12 · 429/403/5xx 退避 + 抖动，并发上限默认 6，不雪崩
- **覆盖 FR**：FR-SYNC-06
- **测试层级/依赖**：IT（`G-WIRE` 限流注入、`CLK`）
- **Given**：`G-WIRE` 对前 20 个请求返回 429（带 `Retry-After: 2`），其余 200；`CLK` 可控。
- **When**：执行 80 页清单同步。
- **Then**：① 记录 429 后请求间隔按退避序列递增（1s→2s→4s…上限符合实现曲线，断言单调不减且含抖动：同轮重试间隔不完全相同）；② 并发请求峰值 ≤6（`G-WIRE` 记录并发数）；③ `sync_run` 最终 `coverage=1.0`，无请求风暴（总请求数 ≤ 基线请求数×2；基线=同数据集无 429 时的 80 页请求数）；④ 若 `Retry-After` 存在则等待 ≥2s（`CLK` 断言）。
- **关联 NFR/OQ**：SYNC-E002、NFR-PERF-06；OQ-15

#### AC-SYNC-13 · 未绑定仓库落库与原因分组（含 `_SIL_Design` 例外）
- **覆盖 FR**：FR-SYNC-05、SRS §6.1
- **测试层级/依赖**：IT（`G-WIRE` 夹具 6 仓）
- **Given**：远程存在：`X_Docs`（无生命周期后缀）、`Y_Design2`（非法后缀）、`X_SIL_Design`（前缀以生命周期词结尾）、`Z_Design` 与 `z_design`（大小写冲突）、`OK_Impl`（合法）。
- **When**：执行清单同步。
- **Then**：① `unbound_repo` 行数=5（`OK_Impl` 不进入），每条 `full_name` 与夹具一致；② `reason` ∈ `{DOCS,PATTERN,CASE_CONFLICT,UNKNOWN}`（SRS v0.2.1 §8.1 CHECK），逐仓断言 `X_Docs→DOCS`、`Y_Design2→PATTERN`、`X_SIL_Design→PATTERN`、`Z_Design`/`z_design→CASE_CONFLICT`；UI `data-testid="unbound-group"` 恰 3 组（DOCS/PATTERN/CASE_CONFLICT）；③ `X_SIL_Design` **不**被绑定为组件 `X_SIL`（`component` 中不存在该行）；④ 大小写冲突两条均列出且不静默丢弃；⑤ 看板组件数不受未绑定仓影响（只有 `OK_Impl` 所在组件可能缺仓→BINDING_ERROR）。
- **关联 NFR/OQ**：SRS §6.1；R-38 已关闭（SRS v0.2.1 §8.1 `unbound_repo.reason CHECK DOCS/PATTERN/CASE_CONFLICT/UNKNOWN`）

#### AC-SYNC-14 · 远程缺 1–3 仓：`BINDING_ERROR` + `binding_cause=INCOMPLETE`（severity=12）+ 缺失仓可见 + 批量只作用存在仓
- **覆盖 FR**：FR-SYNC-01、SRS §6.2/§7.4、FR-OP-01
- **测试层级/依赖**：IT + UI 快照（`G-WIRE`）
- **Given**：远程仅 `CF325F_ChanganSigProcgBascFct_{Design,Impl,MIL}` 三仓（无 SIL）；本地三仓已克隆；另设对照组件一组：缺 1 仓且其第 4 仓为 `INVALID`（`F-INVALID-REMOTE`）。
- **When**：同步后打开看板行与组件详情 → 选中该组件执行“批量 Fetch”；对照组件做同样操作。
- **Then**：① 仅缺仓组件：`aggregate_status=BINDING_ERROR`、`binding_cause=INCOMPLETE`、`severity_rank=12`（中性低：不进入异常优先最高档，见 AC-BOARD-01；可观测落点=`component.incomplete=1` 且无 INVALID 仓）；② 缺仓+INVALID 对照组件：`binding_cause=INVALID`、`severity_rank=1`（两组参数化断言，**不得写死单一 rank**；`binding_cause∈{INVALID,AMBIGUOUS}`（未绑定/解析歧义归 AMBIGUOUS）→1，`INCOMPLETE`→12；可观测落点=存在 `repo_status.main_status=INVALID` 的仓且 `component.incomplete` 不因缺仓置 1）；③ 卡片文案“绑定异常”+ 缺失仓 chip 显示“缺失”（`data-state="MISSING"`、`data-missing="1"`）；④ 批量 Fetch 的 `job.target_json` 仅含 3 个存在仓 `repo_id`，入队前提示“缺失 1 仓，仅对 3 仓执行”；⑤ 任务结果 3 行 `job_item`，无失败；组件仍可操作其余 3 仓（MISSING 仓不阻塞）。
- **关联 NFR/OQ**：D-015、OQ-20；C-02（SRS v0.2.1 拆分 `binding_cause` 严重度）

### 2.3 本地扫描与发现（FR-SCAN）

#### AC-SCAN-01 · 深度 1 平铺发现：识别/跳过结果与预期一致
- **覆盖 FR**：FR-SCAN-01
- **测试层级/依赖**：IT（本地 FS 夹具 `F-SYNCED` 等）
- **Given**：`workspace_root` 下构造：`A_Design/`（有效 git 仓，origin 匹配）、`A_Impl/`（有效仓）、`A_MIL/`（有效仓）、`A_SIL/`（有效仓）、`Docs/`（普通目录）、`B_Design/`（空目录）、`A_Design/sub_repo/`（嵌套仓，深度 2）、`.trash/20261113/A_Design/`（回收站）。
- **When**：执行 L0 本地发现（启动/手动刷新）。
- **Then**：① 识别为本地仓的条目=`A_Design/A_Impl/A_MIL/A_SIL` 4 条，`repo.is_local=1` 且 `local_path` 为 `<root>\<full_name>`；② 非仓目录 `Docs`/`B_Design` 跳过并写入 `scan_issue`（`issue_type=NON_REPO`）；③ 深度 2 的 `sub_repo` **不**被发现（非递归）；④ `.trash` 下目录**不**被发现（D-017）；⑤ 扫描不产生任何仓库写入（见 AC-SCAN-05）。
- **关联 NFR/OQ**：D-013；—

#### AC-SCAN-02 · 有效仓判定：`rev-parse --show-toplevel` + origin 规范化匹配
- **覆盖 FR**：FR-SCAN-01
- **测试层级/依赖**：IT（`F-INVALID-REMOTE`、`F-INVALID-NOTREPO`、`G-WIRE`）
- **Given**：`C_Design/`（是 git 仓但 origin=`https://other.host/other/C_Design.git`）、`D_Design/`（是 git 仓但 origin 指向同名文档仓 `D_Docs`）、`E_Design/`（有效）。
- **When**：执行发现 + 单仓深扫。
- **Then**：① `C_Design`/`D_Design` 判 `INVALID`，`scan_issue.issue_type=REMOTE_MISMATCH`，`scan_issue.path` 指向目录；② 有效仓 `E_Design` 判 `SYNCED`（`F-SYNCED` 语义）；③ `INVALID` 仓在组件聚合中计入 `BINDING_ERROR`（见 AC-STA-02），不被静默排除；④ `git -C <path> remote get-url origin` 使用参数数组执行（进程命令行不含 shell 拼接；测试断言 argv 为 `git`、`-C`、`<path>`、`remote`、`get-url`、`origin`）。
- **关联 NFR/OQ**：SRS §6.3；R-22 已关闭（SRS v0.2 附录 D `SCAN-E001` 已定义，本 AC 断言该码）

#### AC-SCAN-03 · `scan_issue` 七类问题落库与告警
- **覆盖 FR**：FR-SCAN-01、FR-SCAN-06
- **测试层级/依赖**：IT（本地 FS 夹具：非仓/网络盘/长路径/大小写冲突/junction）
- **Given**：`workspace_root` 下构造 7 类问题各 1：`NON_REPO`（普通目录）、`REMOTE_MISMATCH`（见 AC-SCAN-02）、`NETWORK_DRIVE`（映射为 UNC 的目录）、`REPARSE`（junction 指向外部）、`SAFE_DIRECTORY`（属主不同触发 git 拒绝）、`LONG_PATH`（>260 字符路径）、`CASE_CONFLICT`（`X_Design` 与 `x_design` 并存）。
- **When**：执行 L0+L2 扫描。
- **Then**：① `scan_issue` 行数=7，`issue_type` 覆盖全部 7 个枚举值且与目录一一对应；② `detail` 非空；③ UI 异常清单可按 `issue_type` 分组、每类计数正确；④ 长路径/大小写冲突/网络盘/reparse 均给出“仅只读/禁止写”提示（见 AC-SCAN-12）；⑤ 扫描不因单个问题中断（其余有效仓 `is_local=1` 且 `main_status` 与 `expected.json` 一致）。
- **关联 NFR/OQ**：NFR-COMPAT-04、SCAN-E002；R-10 已关闭（SRS v0.2.1 §6.3 统一“不纳管+`scan_issue` 告警；已纳管后不可写→只读展示”）

#### AC-SCAN-04 · 单仓采集字段全覆盖（附录 C 表驱动）
- **覆盖 FR**：FR-SCAN-02
- **测试层级/依赖**：IT（附录 C 全部 `F-*` 夹具，`expected.json`）
- **Given**：M0 交付的 `v0.2/fixtures/` 生成的 **25 个 `F-*` 夹具**（覆盖 14 主状态 + flags + `F-MISSING+BLOCKED` 组合，N-01）。
- **When**：对每个夹具执行一次 L2 深扫。
- **Then**：逐夹具断言 `local_state` 字段：① `head_type`/`head_ref`/`head_sha` 与 `expected.json` 一致（分支/tag/游离/空仓四形态）；② `upstream_ref`/`ahead`/`behind` 一致；③ `dirty_count`/`untracked_count` 一致；④ `op_in_progress`（MERGE_HEAD/rebase-merge/rebase-apply/CHERRY_PICK_HEAD/BISECT_LOG）一致；⑤ `fingerprint`/`scan_at`/`last_success_at` 非空；⑥ flags（`shallow`/`lfs_missing`/`stashed`/`busy`）与 `expected.json` 的 `flags_json` 相等。**夹具缺失或期望缺失视为未通过**。
- **失败路径补充（同 AC）**：⑦ 对某仓注入 `git` 命令失败（模拟 git 不可用/对象损坏）→ `local_state.scan_error` 写入非空错误码（`GIT-*` 前缀）、`last_success_at` **不**前进、`repo_status.error_code` 非空且主状态按 §7.2 优先级 0 为 `UNKNOWN`；其余仓不受影响（逐仓隔离），失败仓不得沿用旧状态冒充新鲜。
- **关联 NFR/OQ**：FR-SCAN-02、附录 C；R-20 已关闭（SRS v0.2 §8.1 `local_state.head_type` CHECK：BRANCH/DETACHED_TAG/DETACHED_COMMIT/UNBORN）

#### AC-SCAN-05 · 只读扫描零写入：仓库前后状态一致、索引不被触碰
- **覆盖 FR**：FR-SCAN-03
- **测试层级/依赖**：IT（本地 git 夹具、进程监控）
- **Given**：`F-DIRTY`、`F-UNMERGED` 两仓；预采集：`git status --porcelain=v2`、`.git/index` mtime/size、`.git/HEAD` mtime、`git for-each-ref` 输出。
- **When**：执行 L1 指纹 + L2 深扫（含 FileSystemWatcher 触发）。
- **Then**：① 扫描后 `git status --porcelain=v2` 输出与扫描前**逐字节相同**；② `.git/index` mtime/size 不变（未被刷新）；③ `git for-each-ref` 前后相同；④ 扫描进程 argv 含 `--no-optional-locks`（或等价 `GIT_OPTIONAL_LOCKS=0`），且未触发 `gc`/`maintenance`（无 `gc.log`/`gc.pid` 产生）；⑤ 仓库内无新增/修改文件。
- **关联 NFR/OQ**：SRS §7.2/§9.3；—

#### AC-SCAN-06 · `.git/index.lock` 存在 → `busy` flag + BUSY 错误码，不删锁不重试
- **覆盖 FR**：FR-SCAN-03、FR-OP-10
- **测试层级/依赖**：IT（`F-BUSY` 夹具）
- **Given**：`F-BUSY` 仓存在外部持有的 `.git/index.lock`（测试进程创建并保持打开），仓可被只读 git 命令访问。
- **When**：① 执行 L2 扫描；② 对同一仓发起 Fetch 与 Commit。
- **Then**：① 扫描完成且 `repo_status.flags_json` 含 `busy`，锁文件仍存在（测试清理前断言 `Test-Path`=True）；② Fetch/Commit 返回 `OP-E001`、`job_item.state=FAILED`（SRS §8.1 合法值）、仓库零改动；③ 工具**不删除**该锁（前后文件 hash 一致）；④ UI chip 显示“外部 Git 正在操作”（`data-flag="busy"`）；⑤ 不自动重试（`job_item.attempt=1`）。
- **关联 NFR/OQ**：OP-E001、NFR-COMPAT-07；—

#### AC-SCAN-07 · 分层调度：L0/L1/L2 频率与“L1 不起 git 进程”
- **覆盖 FR**：FR-SCAN-04
- **测试层级/依赖**：IT（`CLK`、进程启动计数）
- **Given**：400 个本地仓夹具（`F-SYNCED` 批量生成）；`CLK` 固定；埋点统计 `git.exe` 进程启动次数。
- **When**：推进 `CLK` 15s×4、10min、6h。
- **Then**：① L1 每 15s 一轮，每轮只 `stat` 6 个文件（`.git/HEAD`、`index`、`refs`、`packed-refs`、`logs/HEAD`、`FETCH_HEAD`）——断言 `git.exe` 启动次数=0；② L0 每 10min 触发一次目录发现；③ L2 仅对指纹变化仓 + 热仓启动 git 进程（进程数 ≤ 变化仓数×1，8 路并发上限且并发峰值 ≤8）；④ 无任何“全量 30s git 轮询”（15s 时刻点 git 进程数=0）；⑤ `local_state.fingerprint`/`last_success_at` 每轮更新；⑥ **失败路径**：令 2 个变化仓的 L2 深扫失败 → 这两仓 `scan_error` 写入、状态 `UNKNOWN`，其余 398 仓照常完成（不因单仓失败中断整轮），下一轮仍会重试这 2 仓（失败仓不被永久跳过）。
- **关联 NFR/OQ**：FR-SCAN-04、NFR-PERF-05；—

#### AC-SCAN-08 · L1 指纹性能：400 仓 <300ms（进程数=0）
- **覆盖 FR**：FR-SCAN-04
- **测试层级/依赖**：PERF（400 仓夹具、BenchmarkDotNet/Stopwatch）
- **Given**：400 个本地仓（`F-SYNCED` 批量生成，SSD、杀软状态记录在报告）；预热 3 轮。
- **When**：执行 10 轮 L1 指纹轮询取 P95。
- **Then**：① 单轮 P95 <300ms；② 每轮 `git.exe` 启动计数=0；③ 报告记录测试机规格、杀软、磁盘型号（§13.3）；④ 结果入 `BENCH` 夜间基线，超预算即门禁失败。
- **关联 NFR/OQ**：NFR-PERF-05；—

#### AC-SCAN-09 · L2 时效：本地/IDE 变更 ≤5s、外部 commit/checkout/fetch ≤10s
- **覆盖 FR**：FR-SCAN-04、FR-STA-05
- **测试层级/依赖**：IT（`CLK` + 真实 git 操作）
- **Given**：热仓 `A_Design`（当前详情页打开）；`CLK` 按真实时间推进（用于触发 5s/10s 窗口）。
- **When**：① 外部编辑一个 tracked 文件并保存；② 外部执行 `git commit`；③ 外部执行 `git checkout -b feat`；④ 外部执行 `git fetch`。
- **Then**：① 文件变更后 ≤5s 内 `local_state.dirty_count` 与 `repo_status.main_status`（→`DIRTY`）更新，UI `data-testid="freshness-local"` 时间前进；② commit 后 ≤10s 内 `head_sha` 更新；③ checkout 后 ≤10s 内 `head_ref` 更新；④ fetch 后 ≤10s 内 `behind/ahead` 与 `remote_ref` 更新；⑤ 全部更新仅针对变化仓（未变化仓 `scan_at` 不变）。
- **关联 NFR/OQ**：NFR-PERF-08、FR-STA-05；—

#### AC-SCAN-10 · 热仓/冷仓分层频率（5s / 360min）
- **覆盖 FR**：FR-SCAN-04
- **测试层级/依赖**：IT（`CLK`）
- **Given**：1 个热仓（当前详情页）、1 个冷仓（无界面引用）；`scan.hotIntervalSec=5`、`scan.coldIntervalMin=360`。
- **When**：推进 `CLK` 5s→6s→360min。
- **Then**：① 热仓每 5s 一轮 L2；② 冷仓在 360min 前不触发 L2（git 进程数=0），360min 时触发 1 次；③ 设置项修改（`scan.hotIntervalSec=1`）即时生效（下一轮按新值）；④ 冷仓被用户打开后升级为热仓，下一轮起按 5s 节奏。
- **关联 NFR/OQ**：FR-SCAN-04、附录 D；—

#### AC-SCAN-11 · 关系判定前新鲜度门控 + 写前强制 fetch + UI 分源时间
- **覆盖 FR**：FR-SCAN-05
- **测试层级/依赖**：IT（`G-WIRE`、`CLK`）
- **Given**：本地 `A_Design` 上次 fetch 于 `T0`；`ttl.fetchSec=1200`。
- **When**：① `CLK=T0+19min` 时打开看板；② 对 `A_Design` 发起 SYNC（破坏性，`fetch.beforeWrite=true`）；③ 令 `CLK=T0+21min` 再打开。
- **Then**：① 19min 内关系类状态与 `repo_status` 计算值一致且非 `UNKNOWN`，UI 显示“远程数据 19 分钟前”；② 写前强制 fetch 发生时 `G-WIRE`/真 git 记录一次 fetch，成功后 `remote_ref.observed_at` 更新，后续覆盖以新 HEAD 为基准（外部改动导致操作失败且零改动，见 AC-OP-15）；③ 21min 时关系类状态（BEHIND/AHEAD/DIVERGED/SYNCED）变 `UNKNOWN`（`STATE-E001`），本地事实类状态保持，UI 分别显示“本地数据 X 前 / 远程数据 21 分钟前（已过期）”；④ **失败路径**：写前强制 fetch 失败（断网，`NET-E001`）时，破坏性写操作**被阻断**（不入队、无备份 ref、仓库零改动），UI 提示“远程数据不可用，无法安全执行”，不得跳过 fetch 直接执行覆盖。
- **关联 NFR/OQ**：FR-SCAN-05、NFR-PERF-08、STATE-E001；—

#### AC-SCAN-12 · 长路径/中文空格/大小写冲突/reparse/网络盘检测与告警
- **覆盖 FR**：FR-SCAN-06
- **测试层级/依赖**：IT（本地 FS 夹具、`G-E2E` 长路径仓）
- **Given**：① 中文+空格路径 `D:\MBD 测试\工作区\A_Design`；② 总路径 >260 字符的仓；③ `X_Design`/`x_design` 同目录并存；④ junction 指向外部目录；⑤ UNC 路径根。
- **When**：执行 L0+L2 扫描并尝试对各类仓发起写操作（Fetch/Commit）。
- **Then**：① 中文/空格路径被识别为本地仓（`is_local=1`）且 `main_status=SYNCED`；② 长路径仓：`LongPathsEnabled`/`core.longpaths` 检测结果写入状态栏/诊断，超限写操作返回 `FS-E001`；③ 大小写冲突：两目录均入 `scan_issue(CASE_CONFLICT)`，不静默合并；④ reparse/网络盘/UNC 目标**不纳管**（不进入 `component`，无 `repo.is_local=1`）：记 `scan_issue(REPARSE)`/`scan_issue(NETWORK_DRIVE)` 告警并给只读提示（`SCAN-E002`）；⑤ 若已纳管仓的路径随后变为不可写（网络盘断开/reparse 变更）：写操作被拒（`SCAN-E002`/`OP-E004`）、仅只读展示且仓库零改动；⑥ 已纳管仓如仍置 `reparse_point` flag，则删除入口禁用。
- **关联 NFR/OQ**：NFR-COMPAT-03/04、FS-E001、SCAN-E002；R-10 已关闭（SRS v0.2.1 §6.3）

#### AC-SCAN-13 · submodule 不纳管、仅标记（SRS v0.2.1 §6.3 已冻结）
- **覆盖 FR**：FR-SCAN-06（SRS §6.3“不纳管 submodule（只标记）”）
- **测试层级/依赖**：IT（含 submodule 的夹具）
- **Given**：`A_Design` 内含 1 个 submodule（`.gitmodules` + 子目录）。
- **When**：执行 L2 扫描 → 打开单仓详情。
- **Then**：① 扫描**不**把 submodule 目录当独立仓发现（`repo` 行数不增加）；② 单仓详情/文件树对 submodule 显示“未纳管（submodule）”标记 `data-submodule="true"`（SRS v0.2.1 §6.3 冻结 `submodule` 标记；若 §7.3 后续补列则同时断言 `flags_json` 含 `submodule`）；③ 对 submodule 路径的写操作被拒（`OP-E004`）；④ 主仓状态不受 submodule 内部变更影响（改 submodule 内文件后主仓 `dirty_count` 不含该路径）。
- **关联 NFR/OQ**：SRS §6.3；R-10 已关闭（不纳管 submodule、标 `submodule` 标记、写操作拒绝）

### 2.4 状态计算（FR-STA）

#### AC-STA-01 · 附录 C 状态矩阵：14 主状态逐夹具唯一命中
- **覆盖 FR**：FR-STA-01、FR-STA-02
- **测试层级/依赖**：UT 表驱动（附录 C 全部 `F-*`，`expected.json`）
- **Given**：`v0.2/fixtures/manifest.json` 声明的全部夹具（`F-SYNCED`、`F-BEHIND`、`F-AHEAD`、`F-DIVERGED`、`F-DIRTY`、`F-NO_UPSTREAM-{LOCAL,UNSET,GONE}`、`F-DETACHED-TAG`、`F-DETACHED-COMMIT`、`F-UNMERGED`、`F-IN_PROGRESS`、`F-EMPTY`、`F-INVALID-{NOTREPO,REMOTE}`、`F-MISSING`、`F-UNKNOWN`、`F-DETACHED+MIXED`、`F-TAG_DRIFT`、`F-PARTIAL`、`F-LFS-MISSING`、`F-BUSY`、`F-REMOTE-GONE`、`F-TAG-MOVED`、`F-MISSING+BLOCKED`〔Design=MISSING/Impl=UNMERGED，期望组件聚合=MISSING；SRS v0.2.1 附录 C 第 25 行〕），`expected.json` 给 `primary`/`aggregate`/`flags_json`。
- **When**：对每个夹具执行“扫描→状态计算”纯函数调用。
- **Then**：① 每夹具 `repo_status.main_status` 与 `expected.primary` **完全相等**（14 个状态值全部至少命中 1 次；F-PARTIAL 例外见 ⑤）；② 每夹具 `main_status` 唯一（不得同时命中两态）；③ 任一步骤失败（如 git 不可用）时结果为 `UNKNOWN` 而非沿用旧值；④ 夹具执行数=manifest 声明数，缺失视为未通过（红队 06 AC-04 要求）；⑤ **F-PARTIAL null 例外（M-15）**：该夹具 `expected_primary_state=null`，**不参与主状态相等断言**，只断言组件聚合 `aggregate_status=OP_PARTIAL_FAILED`；其余 24 个夹具主状态逐条相等（本 AC 夹具总数 25 = 附录 C 25 行；`F-MISSING+BLOCKED` 逐仓主状态为 MISSING/UNMERGED，聚合断言见 AC-STA-02）。
- **关联 NFR/OQ**：FR-STA-01、附录 C；R-03/R-04 已关闭（SRS v0.2.1 附录 C：`F-TAG_DRIFT` 第 4 仓固定 `SYNCED`；`F-LFS-MISSING` 定死 SYNCED+flag）

#### AC-STA-02 · 附录 C 聚合矩阵：15 聚合态逐夹具 + 未命中兜底 UNKNOWN（SRS v0.2.1 顺序）
- **覆盖 FR**：FR-STA-01、SRS §7.4
- **测试层级/依赖**：UT 表驱动（附录 C 聚合映射节）
- **Given**：按附录 C“聚合态覆盖”表构造的 15 组夹具（按 SRS v0.2.1 §7.4 聚合优先级 0–14 排列，`MISSING=3` 先于 `BLOCKED_LOCAL=4`）：`OP_PARTIAL_FAILED←F-PARTIAL`(0)、`BINDING_ERROR←F-INVALID-*`(1)、`UNKNOWN←F-UNKNOWN`(2)、`MISSING←F-MISSING`(3)、`BLOCKED_LOCAL←F-UNMERGED/F-IN_PROGRESS`(4)、`EMPTY←F-EMPTY`(5)、`TAG_DRIFT←F-TAG_DRIFT/F-TAG-MOVED`(6)、`DIVERGED←F-DIVERGED`(7)、`BEHIND←F-BEHIND`(8)、`AHEAD←F-AHEAD`(9)、`DETACHED←F-DETACHED-*`(10)、`MIXED_BRANCH←F-DETACHED+MIXED`(11)、`NEW_BRANCH←F-NO_UPSTREAM-*`(12)、`DIRTY←F-DIRTY`(13)、`SYNCED←F-SYNCED`(14)。
- **When**：对每组夹具的四仓主状态执行聚合纯函数；另跑 v0.2.1 新增的 `F-MISSING+BLOCKED` 组合夹具（Design=MISSING/Impl=UNMERGED）。
- **Then**：① `component.aggregate_status` 与期望聚合态**完全相等**（15 个聚合态全部命中）；② 聚合优先级严格按 SRS v0.2.1 §7.4 的 0–14 顺序：`OP_PARTIAL_FAILED=0 / BINDING_ERROR=1 / UNKNOWN=2 / MISSING=3 / BLOCKED_LOCAL=4 / EMPTY=5 / TAG_DRIFT=6 / DIVERGED=7 / BEHIND=8 / AHEAD=9 / DETACHED=10 / MIXED_BRANCH=11 / NEW_BRANCH=12 / DIRTY=13 / SYNCED=14`；`MISSING+UNMERGED` 组合必须得 `MISSING`（不得得 `BLOCKED_LOCAL`）；同时存在 DIVERGED+BEHIND 时为 `DIVERGED`；③ 构造一个“未命中任何显式行”的组合 → 结果必须为 `UNKNOWN`，不得为空白/`SYNCED`；④ 任一仓 `INVALID` ⇒ `BINDING_ERROR`，任一仓 `UNKNOWN/STALE` ⇒ 组件非 `SYNCED`；⑤ **口径唯一性（与图集/夹具已对齐）**：本 AC 的 0–14 常量必须与 D3-c/D3-d、`fixtures/state-matrix.ps1` 的 `AggregatePriority` 一致；任一资产仍为 `BLOCKED_LOCAL=3 / MISSING=4` 即判本 AC 失败并登记为资产未回写（C-03）。
- **关联 NFR/OQ**：FR-STA-01/02；OQ-20；C-03（SRS v0.2.1 已回写，图集/夹具同序）

#### AC-STA-03 · 主状态互斥穷尽（property-based 随机提交图）
- **覆盖 FR**：FR-STA-01
- **测试层级/依赖**：UT + property-based（随机提交图生成器）
- **Given**：随机生成 ≥500 组提交图（分支数 1–5、提交数 0–50、随机 detached/tag/dirty/upstream 缺失/远程删除），每组落成真实 git 仓。
- **When**：对每组执行扫描 + 状态判定纯函数。
- **Then**：① 每组结果恰好 1 个主状态，且 ∈ 14 个合法枚举；② 每组聚合态 ∈ 15 个合法枚举；③ 不存在“空白/None/其他字符串”；④ 同一输入两次判定结果一致（幂等）；⑤ 失败样本最小化后写入报告并作为回归夹具。
- **关联 NFR/OQ**：FR-STA-01；—

#### AC-STA-04 · 假绿防护：任一仓 UNKNOWN/STALE ⇒ 组件不得 SYNCED
- **覆盖 FR**：FR-STA-02
- **测试层级/依赖**：IT（`F-UNKNOWN` + 注入失败）
- **Given**：四仓中 3 仓 `SYNCED`、第 4 仓注入 fetch 失败并超 TTL → 该仓 `UNKNOWN`（`stale` flag）。
- **When**：执行聚合。
- **Then**：① `component.aggregate_status=UNKNOWN`，UI 显示“部分状态未知”+ 指明 1 个仓（`data-unknown-repos="SIL"`）；② 卡片**不**显示绿色/SYNCED（`data-aggregate`≠`SYNCED`）；③ 若第 4 仓同时 `INVALID`（另一组）：按 §7.4 优先级 `BINDING_ERROR`(1) 先于 `UNKNOWN`(2)，结果为 `BINDING_ERROR` 且同时列出 UNKNOWN 仓（SRS v0.2 §7.4 已裁决，R-15 关闭）；④ 数据行不因 UNKNOWN 被隐藏或清空。
- **关联 NFR/OQ**：FR-STA-02、STATE-E001；R-15 已关闭（SRS v0.2 §7.4 裁决 BINDING_ERROR 遮蔽 UNKNOWN）

#### AC-STA-05 · `compare_ref` 三来源 + `merge-base --is-ancestor` 退出码 0/1/>1
- **覆盖 FR**：FR-STA-03
- **测试层级/依赖**：UT + IT（本地 git 夹具、`G-WIRE` 记录 compare 调用）
- **Given**：三组夹具：① 已设 upstream 的分支（`F-BEHIND`）；② 无 upstream 但远程存在同名分支（`F-NO_UPSTREAM-UNSET`）；③ 无 upstream 且远程无同名分支（`F-NO_UPSTREAM-LOCAL`）。
- **When**：先 `git fetch`，再按 D-014 解析 `compare_ref` 并执行 `merge-base --is-ancestor`。
- **Then**：① 来源①`compare_ref=<local_branch>@{upstream}`；来源②`refs/remotes/<remote>/<local_branch>`；来源③无 `compare_ref` → 主状态 `NO_UPSTREAM`；② 退出码 0 ⇒ BEHIND/AHEAD 按方向判定；退出码 1 ⇒ DIVERGED；退出码 >1（如 ref 不存在）⇒ 回退/`UNKNOWN` 并记录错误码，**不得**猜测为 DIVERGED；③ 判定结果与“直接命令行跑 git 命令的真值机”逐用例一致（对拍 ≥20 例）；④ 退出码 >1 的样本有专门单测。
- **关联 NFR/OQ**：FR-STA-03、D-014；—

#### AC-STA-06 · Gitea compare 仅兜底：限已推送 commit，未推送不调用 API
- **覆盖 FR**：FR-STA-03
- **测试层级/依赖**：IT（`G-WIRE` 请求记录 + 本地仓）
- **Given**：① 本地存在未推送 commit（`F-AHEAD`）；② 本地对象库缺失某个已存在于服务端的 commit（shallow 夹具）。
- **When**：执行关系判定。
- **Then**：① 场景① `G-WIRE` 记录中 `/compare` 端点调用次数=0；② 场景② 调用 `/compare` 1 次，结果用于兜底且 `data_source` 标记 compare 来源（SRS §7.1）；③ compare 返回 404/500 时回退 `UNKNOWN` 并写 `error_code`；④ 任何情况下不为未推送 commit 生成“远程关系”。
- **关联 NFR/OQ**：FR-STA-03、§7.1；—

#### AC-STA-07 · `repo_status` 字段完整性（新鲜度四元组）
- **覆盖 FR**：FR-STA-04
- **测试层级/依赖**：IT（`F-SYNCED`、`F-UNKNOWN`、`CLK`）
- **Given**：`F-SYNCED`（成功）与 `F-UNKNOWN`（fetch 失败 + TTL 过期）两仓。
- **When**：分别计算状态后查询 `repo_status`。
- **Then**：① 两行均含 `calc_at`（UTC ISO-8601）、`last_success_at`、`error_code`、`data_source`、`is_stale` 且非空（`error_code` 成功时为 NULL/空、失败时为附录 D 错误码）；② `F-UNKNOWN` 行 `is_stale=1`；③ 查询 API 返回上述字段（响应 JSON key 与表字段同名）；④ 旧值不被沿用：把 fetch 改为失败后 `main_status=UNKNOWN`，`calc_at` 前进。
- **关联 NFR/OQ**：FR-STA-04；—

#### AC-STA-08 · 新鲜度展示：本地/远程分源 + 统一格式 + 过期态
- **覆盖 FR**：FR-STA-04、FR-BOARD-05
- **测试层级/依赖**：IT + VIS（`CLK`）
- **Given**：本地扫描于 `T0`、远程 fetch 于 `T1`、`CLK` 当前为 `T2`。
- **When**：打开看板与组件详情。
- **Then**：① 三处（状态栏/看板行/组件详情）均分源显示“本地数据 X 前 / 远程数据 Y 前”，`data-testid="freshness-local"`/`"freshness-remote"`；② 格式统一为“数据 X 秒前/分钟前”（<60s 用秒、≥60s 用分钟，取整规则固定）；③ 任一源超 TTL ⇒ 对应区 `data-stale="true"` + 斜纹 hatched token；④ 不带时间戳的裸“已同步”文案不得出现（扫描全站文案断言）。
- **关联 NFR/OQ**：NFR-PERF-08、STATE-E001；—

#### AC-STA-09 · TTL 表与超期降级：90s / 1200s / 25200s
- **覆盖 FR**：FR-STA-02、FR-STA-04、SRS §7.1
- **测试层级/依赖**：IT + UT（`CLK`、`F-*`）
- **Given**：`ttl.localSec=90`、`ttl.fetchSec=1200`、`ttl.remoteSec=25200`（附录 D 默认值）。
- **When**：推进 `CLK` 至各 TTL 阈值前 1s / 阈值 / 阈值后 1s，分别触发状态计算。
- **Then**：① 本地指纹 TTL 90s：超期时依赖本地新鲜度的 `is_stale=1`；② fetch TTL 1200s：超期时关系类状态（BEHIND/AHEAD/DIVERGED/SYNCED）→ `UNKNOWN`，错误码 `STATE-E001`；③ 远程元数据 TTL 25200s：超期时元数据区 STALE 且看板标“远程数据已过期”；④ 阈值前 1s 不降级（边界断言）；⑤ 每类超期都有对应 UI 提示，不出现静默假绿。
- **关联 NFR/OQ**：NFR-PERF-08、STATE-E001；—

#### AC-STA-10 · 写操作完成 ≤2s 回写受影响组件
- **覆盖 FR**：FR-STA-05
- **测试层级/依赖**：IT（`G-E2E`、`CLK`）
- **Given**：`A_Design` 处于 `BEHIND`（远程领先 2 提交）；执行一次批量 Pull 成功。
- **When**：记录 Pull `job_item` 完成时刻 `T`，轮询 `component.status_calc_at`/`repo_status.calc_at` 与 UI。
- **Then**：① `T+2s` 内 `repo_status.main_status` 变为 `SYNCED` 且 `calc_at ≥ T`；② UI 该行 `data-aggregate` 同步更新（不整表重渲染，见 AC-BOARD-12）；③ 受影响组件的 dirty-set 重算（其它组件 `calc_at` 不变）；④ 超 2s 视为未通过（P95 断言）。
- **关联 NFR/OQ**：NFR-PERF-08、FR-STA-05；—

#### AC-STA-11 · `TAG_DRIFT` 无条件计算（无“基线模式”）三种触发
- **覆盖 FR**：FR-STA-06、SRS §7.4
- **测试层级/依赖**：UT + IT（`F-TAG_DRIFT`、`F-TAG-MOVED`、`G-E2E`）
- **Given**：① 四仓中 3 仓 HEAD 为 tag `v1.0`、第 4 仓在 `main` 分支且无 `v1.0` tag（`F-TAG_DRIFT`）；② `F-TAG-MOVED`（本地/远程同名 tag 指向不同 commit）；③ `baseline_snapshot` 记录后某仓 tag 被移动。
- **When**：执行组件聚合（不开启任何“基线模式”开关，SRS 已作废该概念）。
- **Then**：① 三组均得 `aggregate_status=TAG_DRIFT`；② 组件卡片文案“基线不一致”（§7.4/§11.2），并逐仓展示 tag 值（缺 tag 仓显示空/`tag_present=0`）；③ 场景② `repo_status.flags_json` 含 `tag_moved`；④ 无任何 UI/配置项名为“基线模式”（全站文案扫描断言）。
- **关联 NFR/OQ**：FR-STA-06、D-010；R-03 已关闭（SRS v0.2.1 附录 C `F-TAG_DRIFT` 第 4 仓固定 `SYNCED`）

#### AC-STA-12 · 组件级 `MIXED_BRANCH` 判定与展示
- **覆盖 FR**：FR-STA-01、SRS §7.3/§7.4
- **测试层级/依赖**：UT + VIS（`F-DETACHED+MIXED`、`G-E2E`）
- **Given**：四仓分别处于 `main`、`develop`、`main`、`feature/x` 分支且无更高聚合态。
- **When**：执行聚合。
- **Then**：① `component.aggregate_status=MIXED_BRANCH`（组件级 `mixed_branch` 为 §7.3 派生标记：SRS §8.1 `component` 无 `flags_json` 列，本 AC **不要求存储落点**，N-03）；② 组件卡片显示“分支不一致”角标（`data-mixed="true"`）；③ 主行分支显示 Impl 仓分支 + 角标（与 AC-DETAIL-08 一致）；④ 若任一仓为 `DETACHED_TAG`，因 `DETACHED`(10) 优先于 `MIXED_BRANCH`(11)，聚合为 `DETACHED`（按 §7.4）。
- **关联 NFR/OQ**：SRS §7.3/§7.4；—

#### AC-STA-13 · `DIRTY` 主状态与 `dirty_tracked`/`untracked`/`stashed` flags
- **覆盖 FR**：FR-STA-01、SRS §7.2/§7.3
- **测试层级/依赖**：UT + IT（`F-DIRTY`、`F-SYNCED` + 参数化）
- **Given**：4 组夹具：① HEAD==upstream + tracked 改动（`F-DIRTY`）；② 仅 untracked 文件；③ 有 stash；④ 干净 `F-SYNCED`。
- **When**：执行状态判定。
- **Then**：① 组①`main_status=DIRTY`、`flags_json` 含 `dirty_tracked`；② 组②仅 untracked：默认 `dirty.includeUntracked=false`（SRS v0.2 附录 D/D-021）→ 断言 `main_status=SYNCED` + `untracked` flag；参数化把该键改为 `true` → 同一夹具 `main_status=DIRTY`；③ 组③ `stashed` flag 存在且不影响主状态；④ 组④`main_status=SYNCED` 且 flags 为空；⑤ `dirty_count`/`untracked_count` 数值与夹具一致。
- **关联 NFR/OQ**：SRS §7.2/§7.3、附录 D `dirty.includeUntracked`、D-021；R-23 已关闭

#### AC-STA-14 · 离线可判定状态集：断网下 7 类本地事实照常判定
- **覆盖 FR**：FR-STA-02、SRS §7.1
- **测试层级/依赖**：IT（断网模拟 + 附录 C 离线子集）
- **Given**：断开全部网络（`G-WIRE`/`G-E2E` 不可达）且 fetch TTL 过期。
- **When**：对 `F-INVALID-NOTREPO`、`F-INVALID-REMOTE`、`F-MISSING`、`F-EMPTY`、`F-UNMERGED`、`F-IN_PROGRESS`、`F-DETACHED-TAG`、`F-DETACHED-COMMIT` 执行本地扫描与状态判定。
- **Then**：① 8 个夹具的主状态分别与附录 C 期望一致（**不**被压成 UNKNOWN）；② 关系类夹具（`F-SYNCED`/`F-BEHIND`/`F-AHEAD`/`F-DIVERGED`）在离线时 → `UNKNOWN` 且 `is_stale=1`；③ UI 显示“远程数据 X 前（离线）”，本地事实状态照常展示；④ 不出现全表灰/全 UNKNOWN（与 NFR-REL-01 一致）。
- **关联 NFR/OQ**：NFR-REL-01、SRS §7.1；—

#### AC-STA-15 · 状态×操作矩阵抽样：禁止操作返回明确错误码且仓库零改动
- **覆盖 FR**：FR-STA-01、SRS §7.6
- **测试层级/依赖**：IT（附录 C 夹具 × 7.6 矩阵抽样）
- **Given**：SRS v0.2.1 §7.6 矩阵 13 行（DETACHED_TAG/DETACHED_COMMIT 合并 1 行）每行至少 1 个夹具（如 UNKNOWN→`F-UNKNOWN`、INVALID→`F-INVALID-REMOTE`、MISSING→`F-MISSING`、EMPTY→`F-EMPTY`、UNMERGED→`F-UNMERGED`、IN_PROGRESS→`F-IN_PROGRESS`、DETACHED→`F-DETACHED-COMMIT`、DIVERGED→`F-DIVERGED`、BEHIND→`F-BEHIND`、AHEAD→`F-AHEAD`、NO_UPSTREAM→`F-NO_UPSTREAM-*`、DIRTY→`F-DIRTY`、SYNCED→`F-SYNCED`）；SRS v0.2.1 的 SYNC 强覆盖列已改为：DIRTY=`⚠️ stash -u + 确认`、DIVERGED=`⚠️ 默认阻断，备份 ref + 输入组件 ID 后允许`、SYNCED=`—（无需；仅清理 untracked 需确认）`。
- **When**：对每夹具按矩阵逐操作发起：Fetch/Pull(ff)/Push/Commit/Checkout 分支/切 Tag/SYNC 强覆盖/Delete/CreateBranch/SetUpstream；SYNC 列按上述 ⚠️/— 逐格抽样。
- **Then**：① 矩阵标 ❌ 的操作全部被拒绝，返回 `OP-E004`，UI 给出替代动作（如“仅修复/先提交”）；② 被拒操作后仓库零改动（`HEAD`、`git status`、`.git` 目录树 hash 前后一致），无 `job` 落库或 `job.state=CANCELLED`（不重复）；③ 标 ✅/⚠️ 的操作至少各成功 1 例，且 SYNC 三格与 AC-OP-15/16/18 无互斥：DIRTY 夹具 stash -u+确认后 SYNC 成功、DIVERGED 夹具备份 ref+输入组件 ID 后 SYNC 成功、SYNCED 夹具 SYNC 无需确认且为 no-op（仅勾选清理 untracked 才需确认）；④ 抽样覆盖矩阵 13 行 × ≥4 列，缺行视为未通过。
- **关联 NFR/OQ**：SRS §7.6、OP-E004；C-04（v0.2.1 已回写；不得再按 DIRTY/DIVERGED/SYNCED=❌ 断言）

### 2.5 看板与检索（FR-BOARD）

#### AC-BOARD-01 · 表格默认视图：列齐全 + 默认排序（`severity_rank` 升序→组件 ID 升序）
- **覆盖 FR**：FR-BOARD-01
- **测试层级/依赖**：IT + VIS（`C-SYNTH` 或 `G-E2E` E2E-NORMAL）
- **Given**：1,000 组件数据，含 15 类聚合态各若干、3 个组件 `severity_rank` 相同；另含两组 `BINDING_ERROR`：仅缺仓（`binding_cause=INCOMPLETE`→`severity_rank=12`）与缺仓+INVALID（`binding_cause=INVALID`→`severity_rank=1`）；当前视图=表格。
- **When**：打开 P1 看板（默认排序）。
- **Then**：① 行内列齐全且顺序固定：多选框、组件 ID+变更徽标、聚合状态徽标、`[D][I][M][S]` 四仓 chip、分支（含不一致角标）、Tag、CI 槽位、新鲜度、操作菜单（`data-col` 顺序断言）；② 默认排序=按 SRS D-020/§7.4 的 `severity_rank` **升序**（数值小=严重度高，排前）→ `component_id` 升序：整表顺序与“`severity_rank` 升序 + `component_id` 升序”独立计算结果逐行相同，**不得使用 OQ-21 旧暂定表**；③ **`BINDING_ERROR` 条件化断言（C-02）**：仅缺仓组件（`binding_cause=INCOMPLETE`，`component.incomplete=1`）必须排在 `severity_rank=12` 的位置，不得排到最高档；缺仓+INVALID 组件（`binding_cause=INVALID`，存在 `repo_status.main_status=INVALID` 仓）按 `severity_rank=1` 排在最前档（同一 `BINDING_ERROR` 不得写死单一 rank）；④ 同 `severity_rank` 行按 `component_id` 升序；⑤ 卡片视图与表格数据行数/组件 ID 集合一致。
- **关联 NFR/OQ**：FR-BOARD-01；D-020/§7.4（R-02 已关闭）；C-02（`binding_cause` 1/12 双档）

#### AC-BOARD-02 · 卡片视图：数据一致 + 几何约束（≥320×104、4px 左轨）
- **覆盖 FR**：FR-BOARD-01、SRS §11.3
- **测试层级/依赖**：VIS + IT
- **Given**：同 AC-BOARD-01 数据集。
- **When**：切换视图为卡片；在 1440×900/100% 下测量。
- **Then**：① 卡片视图组件 ID 集合、聚合态、四仓 chip 状态与表格视图逐项一致；② 卡片宽 ≥320px、高 ≥104px（渲染盒测量）；③ 左轨宽 4px 且颜色=对应状态 token；④ 卡片底色为中性 token（非状态饱和色）；⑤ 卡内仅 1 个 CI 槽位（与 AC-CI-02 一致）。
- **关联 NFR/OQ**：SRS §11.3；—

#### AC-BOARD-03 · 4,000 组件虚拟滚动：≥58fps、单帧 ≤33ms、DOM 节点预算
- **覆盖 FR**：FR-BOARD-01、FR-BOARD-02
- **测试层级/依赖**：PERF（`C-SYNTH` E2E-STRESS、WebView2 DevTools）
- **Given**：`C-SYNTH` 生成 4,000 组件/16,000 仓（生成器版本记录在报告）；1440×900/100%。
- **When**：按 §13.3 用 DevTools Performance 时间线滚动 10s 采帧；同时统计 DOM。
- **Then**：① 平均帧率 ≥58fps；② 单帧 ≤33ms（不允许出现 >33ms 的帧）；③ DOM 卡片节点 ≤120、总元素 ≤15,000；④ 仅可视 + ≤8 屏 overscan 被渲染（虚拟滚动断言）；⑤ 滚动后滚动锚点与选中项保持（不跳动）；⑥ 报告含测试机规格/杀软/磁盘型号。
- **关联 NFR/OQ**：NFR-PERF-03（旧 AC-13 已作废，仅映射见 §1.4）；OQ-05

#### AC-BOARD-04 · 全局搜索：5 字段命中与匹配规则
- **覆盖 FR**：FR-BOARD-02
- **测试层级/依赖**：IT + UT（搜索服务）
- **Given**：数据集含：组件 `CF325F_ChanganSigProcgBascFct`（分支 `feat/abc`、tag `v1.0`、HEAD `a1b2c3d4`）、仓库 `CF325F_ChanganSigProcgBascFct_Design`。
- **When**：依次搜索：`CF325F`、`ChanganSig`、`CF325F_..._Design`（全名）、`feat/abc`、`v1.0`、`a1b2c3d`（HEAD 短 SHA 前缀）。
- **Then**：① 6 次搜索均命中同一组组件（结果集一致，行数=1）；② 搜索大小写不敏感；③ 搜索**不**匹配变更徽标字符 `●` 与 `*`（搜索 `*` 结果=0，避免通配符冲突）；④ 搜索字段不包含未同步仓的分支/Tag（只覆盖“关注”范围，与 AC-SYNC-04 一致）；⑤ 命中高亮节点存在（`data-match`）。
- **关联 NFR/OQ**：FR-BOARD-02、NFR-PERF-04；—

#### AC-BOARD-05 · 搜索防抖 120ms 与输入到结果 P95 ≤300ms
- **覆盖 FR**：FR-BOARD-02、FR-BOARD-05
- **测试层级/依赖**：PERF（`C-SYNTH`、DevTools）
- **Given**：4,000 组件数据集；输入法中文候选场景（`compositionstart/compositionend`）。
- **When**：连续键入 10 个字符（间隔 50ms）；以及单次输入后测 20 轮取 P95。
- **Then**：① 仅触发 1 次查询（防抖 120ms：`G-WIRE`/查询日志计数=1；输入间隔 <120ms 时查询不在每次击键触发）；② 输入到结果渲染 P95 ≤300ms；③ 中文 IME 组合期间不触发查询（`compositionend` 后触发 1 次）；④ 查询过程不阻塞输入（无 >100ms 长任务）。
- **关联 NFR/OQ**：NFR-PERF-04；R-29 已关闭（SRS v0.2.1 FR-BOARD-02：IME 以 `compositionend` 触发）

#### AC-BOARD-06 · facet 枚举全集与组合筛选
- **覆盖 FR**：FR-BOARD-02
- **测试层级/依赖**：IT + VIS
- **Given**：数据集覆盖 15 聚合态、14 单仓主状态、4 种 RepoType、多分支/多 Tag、CI 占位、dirty/未推送。
- **When**：逐个打开筛选 facet 并断言选项；再做 2 个 facet 组合筛选。
- **Then**：① facet 共 9 组且枚举与 SRS 一致：聚合状态（15 值）、单仓主状态（14 值）、RepoType（`DESIGN/IMPL/MIL/SIL`）、分支、Tag、CI 状态（占位、禁用态）、有未提交改动、有未推送提交（共 8 个可筛组 + 1 个 CI 占位，若实现分组合并则断言键名与 SRS 一一对应）；② 选项计数之和=对应全量计数；③ 组合筛选结果=交集；④ CI facet 为禁用占位 + tooltip“即将上线”（第 7 锚点，见 AC-CI-01）；⑤ 清空筛选恢复全量行数。
- **关联 NFR/OQ**：FR-BOARD-02、CI-7；—

#### AC-BOARD-07 · 排序三键（严重度/组件 ID/最近活动）与升降序
- **覆盖 FR**：FR-BOARD-02
- **测试层级/依赖**：IT + UT（排序服务）
- **Given**：1,000 组件，`component.last_activity_at` 由夹具指定（10 个不同时间；SRS v0.2.1 补列；字段为空时按 ③ 回退）。
- **When**：依次切换排序：严重度↓、组件 ID↑↓、最近活动↓↑。
- **Then**：① 每种排序的整表顺序与独立计算期望逐行一致（“最近活动”排序键=`component.last_activity_at` 降序，为空的行回退 `status_calc_at`）；② 升降序切换后首末行互换；③ 排序键在 URL/状态中保留（刷新后保持）；④ 非法排序键回退默认（严重度↓）并记录日志；⑤ 排序不改变筛选结果集（行数不变）。
- **关联 NFR/OQ**：FR-BOARD-02、§7.4/D-020；C-05（`component.last_activity_at`，SRS v0.2.1 补列）

#### AC-BOARD-08 · 七态齐备（空/加载/错误可重试/离线降级/过期/部分失败/权限过期）
- **覆盖 FR**：FR-BOARD-03
- **测试层级/依赖**：IT + VIS（注入）
- **Given**：分别注入 7 种条件：无数据、请求中、API 500、断网、数据超 TTL、`F-PARTIAL` 部分失败、401（认证过期）。
- **When**：逐一打开 P1 看板。
- **Then**：① 每态有唯一可识别空/错态组件（`data-page-state` 取 `empty/loading/error/offline/stale/partial/permission`）；② 每态均有“重试/重登/刷新”可点击入口且点击后状态推进；③ 错误态显示错误码（附录 D，如 `NET-E001`/`SYNC-E001`）与文案；④ 权限过期态提供“重新登录”入口（与 AC-CFG-08 共用）；⑤ 七态截图入视觉基线。
- **关联 NFR/OQ**：FR-BOARD-03、NFR-REL-01；—

#### AC-BOARD-09 · 批量操作条阈值：>10 组件或 >40 仓触发二次确认
- **覆盖 FR**：FR-BOARD-04
- **测试层级/依赖**：IT + VIS
- **Given**：看板有 ≥12 个可操作组件、对应 ≥48 仓。
- **When**：① 选中 10 个组件（=40 仓）→ 点“批量 Fetch”；② 选中 11 个组件（=44 仓）→ 点“批量 Fetch”。
- **Then**：① 场景① 无二次确认（直接入队，仅预检）；② 场景② 弹出二次确认，文案含“11 个组件 / 44 个仓”，需点“确认执行”才入队；③ 取消确认时 `job` 行数不增；④ 边界值 10 与 40 均为“不触发”（阈值严格 >10 且 >40，按 SRS 原文）；⑤ 确认后 `job.target_json` 与选择集合一致。
- **关联 NFR/OQ**：FR-BOARD-04；—

#### AC-BOARD-10 · 多选与“全选筛选结果”
- **覆盖 FR**：FR-BOARD-04
- **测试层级/依赖**：IT + VIS
- **Given**：筛选“聚合状态=BEHIND”命中 37 个组件。
- **When**：① 手动多选 3 个；② 点“全选筛选结果”；③ 清空筛选。
- **Then**：① 手动多选计数显示 `3`；② “全选筛选结果”后计数=`37`（跨虚拟滚动全量选中，不仅可视行），`data-selected-count="37"`；③ 清空筛选后选择集保持 37（或按实现显式提示“已选 37”）；④ 取消全选后计数=0；⑤ 选中状态在滚动中保持。
- **关联 NFR/OQ**：FR-BOARD-04；—

#### AC-BOARD-11 · 批量操作条操作集合与逐仓进任务中心
- **覆盖 FR**：FR-BOARD-04
- **测试层级/依赖**：IT
- **Given**：选中 2 个组件（8 仓）。
- **When**：打开批量操作条，枚举可见操作；执行其中 1 个（Fetch）。
- **Then**：① 操作条含 9 项且与 SRS 一致：Clone/Pull/Fetch/Checkout/Tag/SYNC/Delete/Restore/Create；② 与状态矩阵不兼容的项为禁用态并给 tooltip（如对缺失组件禁用 SYNC）；③ 执行后跳转/打开任务中心对应 `job`，逐仓 8 行可见（组件/仓级进度）；④ 任务中心返回看板后选中集保留。
- **关联 NFR/OQ**：FR-BOARD-04；—

#### AC-BOARD-12 · 数据新鲜度分源展示 + 手动刷新不整表闪白
- **覆盖 FR**：FR-BOARD-05、SRS §7.5
- **测试层级/依赖**：VIS + PERF
- **Given**：4,000 组件看板已加载，记录当前滚动位置与选中行；`CLK` 固定。
- **When**：点“刷新当前视图”（触发 fetch + 重扫）。
- **Then**：① 刷新中保留旧数据行，行上出现行内加载态（skeleton/半透明），不出现整表白屏/清空（截图无空白帧）；② 刷新后仅变化行更新（keyed diff：未变化行 DOM 节点身份不变）；③ 滚动位置与选中项保持；④ `data-testid="freshness-local"`/`"freshness-remote"` 时间前进；⑤ 刷新失败时保留旧数据 + 错误 Toast + 错误码，不整表报错。
- **关联 NFR/OQ**：NFR-PERF-08、FR-BOARD-05；—

#### AC-BOARD-13 · 未绑定仓库入口：原因分组/重新解析/忽略需记录理由
- **覆盖 FR**：FR-BOARD-06
- **测试层级/依赖**：IT + VIS
- **Given**：`unbound_repo` 有 5 条（原因见 AC-SYNC-13）。
- **When**：打开“未绑定仓库”抽屉/异常清单页；对 1 条点“重新解析”；对 1 条点“忽略”并输入理由。
- **Then**：① 入口存在于 P1 页面（`data-testid="unbound-entry"`），抽屉按 `reason` 分组且恰 3 组（DOCS/PATTERN/CASE_CONFLICT，与 AC-SYNC-13 一致）；② “重新解析”重新执行绑定规则，成功后该仓移出清单（行数 5→4），失败保持并更新 `discovered_at`；③ “忽略”必须填写理由（空理由时确认按钮 `disabled`），忽略后该条不再显示但 `unbound_repo` 行保留忽略记录；④ 操作写审计；⑤ 清单不影响看板组件数。
- **关联 NFR/OQ**：FR-BOARD-06（旧 AC-23 仅映射见 §1.4）；R-38 已关闭（SRS v0.2.1 §8.1 `reason` CHECK 四值）

#### AC-BOARD-14 · 首屏可交互 ≤1.5s、冷启动到首屏 ≤5s（两档数据集）
- **覆盖 FR**：FR-BOARD-01、FR-BOARD-05
- **测试层级/依赖**：PERF（`C-SYNTH`、DevTools Performance）
- **Given**：两档数据集：正常档 1,000 组件/4,000 仓、压测档 4,000 组件/16,000 仓；数据已就绪（不含首扫）；冷启动场景=进程冷启动、DB 已存在。
- **When**：按 §13.3 测量 LCP/首屏可交互时刻；重复 5 次取 P95。
- **Then**：① 两档“数据就绪后首屏可交互”均 ≤1.5s；② 冷启动到首屏 ≤5s（不含首扫）；③ 报告记录数据集生成器版本、测试机 8C/16G/SSD/Win11、杀软/磁盘型号；④ 结果入 `BENCH` 夜间门禁；⑤ 超预算即失败，不允许用“压测档豁免”掩盖。
- **关联 NFR/OQ**：NFR-PERF-02/09（旧 AC-13 已作废，仅映射见 §1.4）；OQ-05

#### AC-BOARD-15 · 状态三重编码 + token 扫描 + 对比度 + 深色主题
- **覆盖 FR**：FR-BOARD-01、SRS §10.5/§11.2
- **测试层级/依赖**：VIS + UT（token 静态扫描 + 对比度计算）
- **Given**：15 聚合态 × 6 关键 CI 态组合矩阵（≥12 张快照）、浅色/深色两主题、`prefers-reduced-motion: reduce`、`prefers-contrast`、`forced-colors`。
- **When**：渲染状态矩阵页并跑：① token 扫描（组件样式禁硬编码 hex，只允许 `--st-*`/`--ci-*`）；② 对比度计算（fg/bg、fg/卡片底）；③ 三重编码检查（图标+中文文案+颜色）；④ 深色/减少动效/高对比模式快照。
- **Then**：① 组件内无硬编码 hex（扫描命中=0）；② 全部组合对比度 ≥4.5:1（非文本 ≥3:1）；③ 每个状态均有图标+中文文案+颜色三通道（缺一即失败）；④ 红色只出现在 SRS §11.2 允许清单（INVALID/BINDING_ERROR/BLOCKED_LOCAL/UNMERGED/OP_PARTIAL_FAILED/REMOTE_GONE/lfs_missing/CI FAIL/破坏性确认按钮），AHEAD/DIVERGED/MISSING 不得为红；⑤ 深色模式 token 换值不换名、快照通过；`reduce-motion` 下动效 ≤1ms（无位移动画）；⑥ 视觉基线 ≥12 张且覆盖 15 聚合态。
- **关联 NFR/OQ**：SRS §10.5、§11.2；R-24 已关闭（§11.2 `DIRTY=success+变更徽标`）；hatched 对比度（H-04）由 SRS v0.2.1 修正，本 AC 断言不降低

#### AC-BOARD-16 · 可访问性：键盘可达、焦点可见、aria-live、点击目标
- **覆盖 FR**：FR-BOARD-01、SRS §10.5
- **测试层级/依赖**：VIS + IT（axe 规则 + 键盘脚本）
- **Given**：看板 + 组件详情 + 任务中心。
- **When**：仅键盘操作（Tab/Shift+Tab/Enter/Space/Esc）；运行 axe 规则集。
- **Then**：① 全部交互元素可 Tab 到达（无 tabindex 陷阱）；② 焦点环 2px 可见（截图断言）；③ 任务完成/失败通过 `aria-live` 播报（DOM 断言 `role=status`/`aria-live=polite` 文案变化）；④ 点击目标 ≥24×24、主按钮 ≥32px（尺寸断言）；⑤ axe 严重级违规=0；⑥ `Esc` 从详情返回看板且焦点回到原行。
- **关联 NFR/OQ**：SRS §10.5；—

### 2.6 组件/单仓详情（FR-DETAIL）

#### AC-DETAIL-01 · 组件详情四仓状态卡字段 + 一次点击可达
- **覆盖 FR**：FR-DETAIL-01
- **测试层级/依赖**：IT + VIS（`G-E2E`）
- **Given**：看板中存在组件 `A`（4 仓 SYNCED/BEHIND/DIRTY/MISSING 混合）。
- **When**：在看板行点击组件 ID。
- **Then**：① 一次点击进入 P2 组件详情（路由/URL 变化，无二次确认）；② 4 张仓状态卡字段齐全：分支、HEAD（短 SHA+全文 tooltip）、upstream、ahead/behind、dirty（`dirty_count`/`untracked_count`）；③ 数据与看板 chip/DB `local_state` 一致；④ `Esc`/面包屑返回看板并保持原滚动位置与选中行；⑤ **失败路径**：令详情数据源查询失败 → 页面显示错误态（`data-page-state="error"`）+ 错误码 + “重试”按钮，旧数据不清空不白屏；重试成功后 `data-page-state` 回到 `ready` 且 4 张卡数据与 DB `local_state` 一致。
- **关联 NFR/OQ**：FR-DETAIL-01；—

#### AC-DETAIL-02 · 组件详情：Tag 对照 + 未绑定告警 + 快捷操作
- **覆盖 FR**：FR-DETAIL-01、FR-STA-06
- **测试层级/依赖**：IT + VIS（`F-TAG_DRIFT`）
- **Given**：`F-TAG_DRIFT` 组件（3 仓 tag `v1.0`、1 仓缺 tag），且该组件有 1 条未绑定仓（`A_Docs` 不进绑定，用组件外部未绑定清单交叉）。
- **When**：打开组件详情。
- **Then**：① Tag 对照表 4 行，逐仓显示 tag 与 commit，缺 tag 仓显示“无”（`data-tag-present="0"`）；② 顶部出现“基线不一致”提示（与 `TAG_DRIFT` 一致）；③ 未绑定告警区存在并列出相关未绑定仓（`data-testid="unbound-alert"`）；④ 快捷操作含 Swap 到详情/任务中心（SYNC/Tag/Create 入口存在且按状态矩阵禁用规则生效）；⑤ 所有数据与 DB 一致（`baseline_snapshot` 行、`remote_ref` tag）。
- **关联 NFR/OQ**：FR-STA-06、D-010；—

#### AC-DETAIL-03 · 单仓详情四 Tab 顺序固定（概览/历史/差异/CI 第 4 位）
- **覆盖 FR**：FR-DETAIL-02、FR-CI-01
- **测试层级/依赖**：VIS + IT
- **Given**：仓 `A_Design`。
- **When**：打开 P3 单仓详情；记录 Tab 顺序与路由。
- **Then**：① Tab 顺序固定为 概览→提交历史→差异→CI（CI 恰为第 4 位），`role=tab` 的 `aria-posinset` 与顺序一致；② 每个 Tab 有独立路由参数（深链可直达，刷新后保持当前 Tab）；③ CI Tab 为占位但可渲染空态（见 AC-CI-08/AC-CI-11）；④ 未接入 CI 时 Tab 顺序不重排（快照断言）；⑤ **失败路径**：深链到不存在的 commit/分支参数 → 显示“内容不存在/已变更”错误态 + 返回入口，不崩溃、不空白。
- **关联 NFR/OQ**：CI-4、SRS §11.4；—

#### AC-DETAIL-04 · 提交历史 1 万提交：虚拟滚动 ≥58fps、首屏 ≤500ms
- **覆盖 FR**：FR-DETAIL-02
- **测试层级/依赖**：PERF（1 万提交夹具、DevTools）
- **Given**：仓含 10,000 个提交（脚本生成）；默认 50 条/页。
- **When**：打开提交历史 Tab；滚动 10s 采帧；测量首屏渲染。
- **Then**：① 首屏（首批 50 条）渲染 ≤500ms；② 滚动平均 ≥58fps、单帧 ≤33ms；③ 分页/滚动加载不重复/不丢行（提交 SHA 序列与 `git log --format=%H` 前 N 条一致）；④ 行内字段：短 SHA、作者、时间、message 首行；⑤ 二进制/超大 commit 不导致卡死（夹具含 1 个 5MB diff 提交）。
- **关联 NFR/OQ**：NFR-PERF-03；R-37 已关闭（SRS v0.2.1 FR-DETAIL-02 已同口径量化：首屏 ≤500ms/≥58fps/单帧 ≤33ms；旧 AC-13 仅映射见 §1.4）

#### AC-DETAIL-05 · 差异只读 + 二进制不可预览提示
- **覆盖 FR**：FR-DETAIL-02
- **测试层级/依赖**：IT + VIS
- **Given**：① 文本文件修改的 commit；② 二进制文件（`.slx`/`.mat`）变更的 commit。
- **When**：打开差异 Tab 并选择两个 commit 对比。
- **Then**：① 文本差异逐行显示、新增/删除着色为 token（非硬编码）；② 二进制文件显示“不可预览”提示（`data-binary="true"`），不尝试渲染乱码；③ 差异区域只读（无编辑控件）；④ 大 diff（>5MB）显示截断提示并提供“在外部程序打开”入口；⑤ 差异内容与 `git diff` 命令输出一致（抽查 3 个 hunk）。
- **关联 NFR/OQ**：FR-DETAIL-02；—

#### AC-DETAIL-06 · 文件树：单击选中/双击白名单/危险扩展名二次确认
- **覆盖 FR**：FR-DETAIL-03、FR-SAFE-05
- **测试层级/依赖**：IT + 安全用例（`G-E2E`）
- **Given**：仓内含 `model.slx`、`readme.txt`、`build.bat`、`tool.ps1`、`setup.exe`、`shortcut.lnk`。
- **When**：① 单击 `readme.txt`；② 双击 `readme.txt`；③ 双击 `build.bat`；④ 双击 `setup.exe`。
- **Then**：① 单击仅选中（`aria-selected=true`），不打开；② 双击 `.txt` 直接打开（白名单内）；③ 双击 `.bat`/`.ps1`/`.exe`/`.lnk`/`.msi`/`.scr` 弹二次确认，点“取消”不启动进程（进程监控无新进程）；④ 确认后由系统默认程序打开，进程命令行不含路径拼接注入；⑤ 打开动作写审计（`action` 取 SRS §8.1 `operation_log.action`，同 `job.kind` 集）。
- **关联 NFR/OQ**：FR-SAFE-05、SRS §12.6；—

#### AC-DETAIL-07 · 路径穿越与 root 限定拒绝（`FS-E001`）
- **覆盖 FR**：FR-DETAIL-03、FR-SAFE-05
- **测试层级/依赖**：IT（安全用例）
- **Given**：文件树节点 + 构造恶意输入：`..\..\Windows\System32\cmd.exe`、绝对路径 `C:\Windows\notepad.exe`、UNC 路径 `\\host\share\x`、reparse 指向 root 外。
- **When**：通过 IPC 请求“打开文件/读取文件”传入上述路径。
- **Then**：① 全部被拒绝并返回 `FS-E001`；② 不发生任何文件读取/进程启动；③ 日志记录拒绝事件（不含敏感路径明文泄漏到普通日志，仅结构字段）；④ reparse 越界同样拒绝；⑤ root 内合法文件读取成功（返回内容与磁盘文件逐字节一致）。
- **关联 NFR/OQ**：FR-SAFE-05、FS-E001；—

#### AC-DETAIL-08 · 四仓分支不一致：Impl 为主 + 角标 + 四仓对照
- **覆盖 FR**：FR-DETAIL-04
- **测试层级/依赖**：VIS + IT（`G-E2E` 四仓不同分支）
- **Given**：四仓分支 `main`/`dev_feature`/`main`/`feature/x`。
- **When**：打开看板行与组件详情。
- **Then**：① 看板主行分支显示 Impl 仓分支（`dev_feature`）；② “分支不一致”角标必现（`data-mixed="true"`）；③ hover/详情展示四仓分支对照表，4 行且值准确；④ 提供“对齐分支”动作入口并跳批量 Checkout（参数预填 4 仓）；⑤ 组件聚合态为 `MIXED_BRANCH`（无更高态时）。
- **关联 NFR/OQ**：SRS §11.3、FR-BOARD-01；—

#### AC-DETAIL-09 · 右键菜单（P2）：v1 只断言入口不存在/不可用
- **覆盖 FR**：FR-DETAIL-05（P2，v1.1；v1 只冻结“入口不存在”，见 §4.1 OQ-24）
- **测试层级/依赖**：IT + VIS
- **Given**：v1 构建；文件树节点、提交行、组件行均可见。
- **When**：对上述三类目标分别右键、按 Shift+F10、并检索命令面板。
- **Then**：① v1 无右键上下文菜单（DOM 无 contextmenu 菜单节点、无相关 IPC 命令类型、命令面板无“复制路径/复制 commit/在资源管理器打开”项），不得出现死按钮或空菜单；② 浏览器默认右键菜单被抑制时不出现“功能未实现”类裸提示（避免把 P2 半成品暴露给用户）；③ 本 AC 在 M0 仅冻结 ①/②；④ 完整四动作行为见 §2.12 v1.1 AC（不冻结）。
- **关联 NFR/OQ**：OQ-24；§2.12 v1.1 AC（不冻结）

### 2.7 批量操作（FR-OP）

#### AC-OP-01 · 批量 Clone 成功路径（4/4）与幂等重跑
- **覆盖 FR**：FR-OP-01
- **测试层级/依赖**：E2E（`G-E2E` + 本地 FS）
- **Given**：组织 `MBD` 存在 4 仓 `CF325F_ChanganSigProcgBascFct_{Design,Impl,MIL,SIL}`；`workspace_root` 为空；磁盘余量充足；PAT 有效。
- **When**：看板选中组件 → 批量操作条点击 “Clone” → 弹层显示将 clone 的 4 仓 → 确认入队。
- **Then**：① `job` 行 `kind=CLONE`（SRS §8.1 `job.kind` 合法值），`job_item` 4 行，`job.succeeded=4`/`failed=0`；② 4 个目录 `<root>\<full_name>` 存在且为 git 仓，`origin` URL 无凭据；③ `repo.is_local=1` 且 `local_path` 相符；④ 首扫后 4 仓 `main_status=SYNCED`，组件 `aggregate_status=SYNCED`；⑤ 重复执行同一操作：4 仓已存在 → 不重新 clone，`job_item` 记“已存在/跳过”（`state=SKIPPED`，SRS §8.1 合法值），磁盘上 `.git` 修改时间不变（幂等）。
- **关联 NFR/OQ**：NFR-REL-05、FR-OP-01；—

#### AC-OP-02 · Clone 预检：磁盘/长路径/凭据/远程存在，任一失败即阻断
- **覆盖 FR**：FR-OP-01、NFR-REL-05
- **测试层级/依赖**：IT（`G-WIRE`/`G-E2E`、磁盘余量注入）
- **Given**：4 组条件：① 磁盘剩余 < 单仓体积×2；② 目标路径超 240 字符；③ PAT 无效（401）；④ 远程某仓 404。
- **When**：分别发起批量 Clone（预检阶段）。
- **Then**：① 场景① 预检阶段阻断（**不产生 `job` 行**；SRS §8.1 `job.state` 无 BLOCKED 枚举），UI 提示磁盘不足 + `FS-E001`/`NET-E001` 中对应码，**无任何目录创建**；② 场景② 超长路径被预检拒绝，提示含具体路径与字符数；③ 场景③ 预检失败，UI 出现重登入口（同 AC-CFG-08），不产生 4 个半成品目录；④ 场景④ 预检指出 404 仓名，其余仓可继续（部分预检通过）；⑤ 预检结果为表：每仓一行（可 clone/失败原因），断言行数=4。
- **关联 NFR/OQ**：NFR-REL-05、NFR-COMPAT-03；—

#### AC-OP-03 · Clone 部分失败（2/4）：`OP_PARTIAL_FAILED` + 明细 + 仅重试失败项 + 清除需确认
- **覆盖 FR**：FR-OP-01、FR-OP-08
- **测试层级/依赖**：E2E（`G-E2E` 2 仓 500/403 注入）
- **Given**：4 仓中 `MIL`/`SIL` 在 clone 时被注入 403/网络错误，`Design`/`Impl` 成功。
- **When**：执行批量 Clone → 打开任务中心 → 点“仅重试失败项” → 再点“清除失败态”。
- **Then**：① `job.succeeded=2`、`failed=2`、`job.state=PARTIAL`（SRS §8.1 CHECK 合法值；不得使用其他 job 态名）；② 组件 `aggregate_status=OP_PARTIAL_FAILED`，卡片文案“部分失败 2/4”；③ 任务中心逐仓 4 行，失败行含错误码（附录 D）与可读文案；④ 重试 job 的 `target_json` 仅含 2 个失败仓 `repo_id`；⑤ “清除失败态”需显式确认（弹确认框），确认后 `aggregate_status` 按真实状态重算并写 `operation_log`；⑥ 重跑幂等（已成功 2 仓不再 clone）。
- **关联 NFR/OQ**：OP-E003、FR-OP-08（旧 AC-09 仅映射见 §1.4）；—

#### AC-OP-04 · Clone 错误分类：401/403/404 不重试，网络错误退避 ≤2
- **覆盖 FR**：FR-OP-01
- **测试层级/依赖**：IT（`G-E2E`/WireMock 代理注入）
- **Given**：4 个远程仓分别注入 401、403、404、瞬时网络错误（首次失败、第二次成功）。
- **When**：执行批量 Clone。
- **Then**：① 401/403/404 仓 `job_item.attempt=1`（不重试），错误码前缀 `GIT`（M0 错误码表冻结，见 §4.2 R-06）；② 网络错误仓 `attempt=2`（≤2 次重试）后成功；③ 无“无限重试”（attempt 上限断言）；④ 任务中心展示每仓分类错误码与文案（非技术堆栈裸露）；⑤ 失败仓不创建残留目录（预检未通过即不 clone）。
- **关联 NFR/OQ**：FR-OP-01；〔SRS 需修订 R-06：git 鉴权/404 错误码未给具体码〕

#### AC-OP-05 · Clone 目标目录已存在：repair 不覆盖
- **覆盖 FR**：FR-OP-01、FR-REPO-05
- **测试层级/依赖**：IT（`F-INVALID-NOTREPO`、`F-INVALID-REMOTE`）
- **Given**：`<root>\A_MIL` 已存在但为非 git 目录；`<root>\A_SIL` 已存在但 origin 不匹配。
- **When**：对组件 A 执行批量 Clone。
- **Then**：① 两目录均**不被覆盖/删除**，`job_item` 标记“已存在→转 Repair”或 `state=SKIPPED`（SRS §8.1 合法值）；② UI 提示“转修复流程”，提供 Repair/Re-clone 入口（见 AC-REPO-08）；③ 原有目录内文件 hash 前后不变；④ 其余 2 仓 clone 成功（`is_local=1`、目录存在且为 git 仓）；⑤ 不产生重复目录（`A_MIL_1` 之类）。
- **关联 NFR/OQ**：FR-OP-01；—

#### AC-OP-06 · 批量 Pull（`--ff-only`）成功且无隐式 merge commit
- **覆盖 FR**：FR-OP-02
- **测试层级/依赖**：E2E（`G-E2E` + `F-BEHIND`）
- **Given**：4 仓均为 `BEHIND`（远程领先 2 提交）；预记录各仓 `git rev-list --merges HEAD` 数量。
- **When**：批量 Pull（ff-only）。
- **Then**：① 4 仓 `main_status=SYNCED`；② `git rev-list --merges HEAD` 数量与 Pull 前相同（无新增 merge commit）；③ `job.succeeded=4`；④ 每组 `job_item.pre_head/post_head` 记录且 `post_head` 为远程 tip 的快进结果；⑤ 组件 `aggregate_status=SYNCED`。
- **关联 NFR/OQ**：FR-OP-02；—

#### AC-OP-07 · Pull 非快进被拒绝并转手动提示
- **覆盖 FR**：FR-OP-02
- **测试层级/依赖**：IT（`F-DIVERGED`）
- **Given**：4 仓中 1 仓为 `F-DIVERGED`。
- **When**：批量 Pull。
- **Then**：① 该仓 Pull 被拒，错误码明确（`OP-E004` 或 git 前缀），UI 提示“非快进，请手动处理（查看差异/建分支）”；② 该仓 HEAD 不变（`pre_head==post_head`）；③ 其余 3 仓成功；④ `job.succeeded=3/failed=1`、组件 `OP_PARTIAL_FAILED`；⑤ 无 merge/rebase 中间态残留（无 `MERGE_HEAD`）。
- **关联 NFR/OQ**：FR-OP-02；—

#### AC-OP-08 · 批量 Fetch 成功/失败逐仓报告
- **覆盖 FR**：FR-OP-02
- **测试层级/依赖**：IT（`G-E2E` 部分仓 500）
- **Given**：4 仓中 1 仓 fetch 注入 500，其余 3 仓成功。
- **When**：批量 Fetch。
- **Then**：① `job.succeeded=3/failed=1`；② 失败仓错误码前缀 `GIT`/`NET` + 文案；③ 成功仓 `remote_ref.observed_at` 更新；④ Fetch 不改变工作区（`git status` 前后相同、无 merge）；⑤ 任务中心逐仓可见结果。
- **关联 NFR/OQ**：FR-OP-02；—

#### AC-OP-09 · 批量 Checkout 分支：5 种前置态
- **覆盖 FR**：FR-OP-03
- **测试层级/依赖**：E2E（`F-SYNCED`/`F-BEHIND`/`F-DIRTY`/`F-DETACHED-COMMIT`/`F-UNMERGED`）
- **Given**：5 仓分别为 `SYNCED`、`BEHIND`、`DIRTY`、`DETACHED_COMMIT`、`UNMERGED`；目标分支 `release/1.0` 均存在。
- **When**：批量 Checkout 到 `release/1.0`。
- **Then**：① `SYNCED`/`BEHIND` 仓成功（`head_ref=release/1.0`），BEHIND 仓提示落后；② `DIRTY` 仓阻断（除非用户选 stash，见 AC-OP-10），HEAD 不变；③ `DETACHED_COMMIT` 仓按 7.6 允许但需确认 → 确认后成功；④ `UNMERGED` 仓被拒 `OP-E004`；⑤ `job` 逐仓结果与 7.6 一致；⑥ 分支名非法（`bad..name`）在入队前被 `check-ref-format` 拒绝。
- **关联 NFR/OQ**：FR-OP-03、SRS §7.6；—

#### AC-OP-10 · Checkout 遇 dirty：阻断或按用户选择 stash
- **覆盖 FR**：FR-OP-03、SRS §7.6
- **测试层级/依赖**：IT（`F-DIRTY`）
- **Given**：`F-DIRTY` 仓（1 个 tracked 改动 + 1 个 untracked）。
- **When**：① 预检后选“阻断”（默认）；② 重新发起并选“stash 后切换”；③ 再用 `git stash list` 核对。
- **Then**：① 默认阻断：仓 HEAD 与文件内容不变、无 stash 产生；② stash 选项：切换成功，`stash list` 新增 1 条，切换后工作区干净；③ untracked 文件按选择处理（默认保留）；④ UI 明确显示 stash 名称与恢复入口；⑤ 结果写 `job_item.message` 与审计。
- **关联 NFR/OQ**：FR-OP-03；—

#### AC-OP-11 · 切 Tag：四仓同 Tag 成功、detached 检出、写 `baseline_snapshot`
- **覆盖 FR**：FR-OP-04、FR-STA-06
- **测试层级/依赖**：E2E（`G-E2E` 四仓均打 `v1.0`）
- **Given**：4 仓均有轻量 tag `v1.0`（指向各自 commit `cD/cI/cM/cS`）。
- **When**：对组件执行“切 Tag → v1.0”。
- **Then**：① 4 仓 `HEAD` 分离（`git status` 显示 detached）且 `git tag --points-at HEAD` 含 `v1.0`；② `repo_status.main_status=DETACHED_TAG`（4 仓）；③ `baseline_snapshot` 写入 4 行 `(component_id,'v1.0',repo_type)`，`commit_sha` 分别为 `cD/cI/cM/cS`、`tag_present=1`；④ 组件 `aggregate_status=DETACHED`（按 §7.4，无 TAG_DRIFT 时）；⑤ 任务中心逐仓结果与 tag 一致。
- **关联 NFR/OQ**：FR-OP-04（旧 AC-10 仅映射见 §1.4）；—

#### AC-OP-12 · 切 Tag 缺 Tag：3 仓检出 + 1 仓跳过报告 + `TAG_DRIFT`
- **覆盖 FR**：FR-OP-04、FR-STA-06
- **测试层级/依赖**：E2E（`F-TAG_DRIFT` 口径）
- **Given**：4 仓均存在；仅 3 仓（Design/Impl/MIL）有 tag `v1.0`；SIL 仓在默认分支上、无 `v1.0`（`main_status=SYNCED`）。
- **When**：执行“切 Tag → v1.0”。
- **Then**：① 3 仓 `DETACHED_TAG`，1 仓（SIL）不做 detached 检出、`job_item` 记“缺 Tag 跳过”（`state=SKIPPED`，SRS §8.1 合法值，不置失败也不置成功检出）；② 组件 `aggregate_status=TAG_DRIFT`（TAG_DRIFT 优先于 SYNCED；附录 C `F-TAG_DRIFT` 已冻结第 4 仓为 `SYNCED` 而非 MISSING，R-03 关闭）；③ `baseline_snapshot` 写 4 行，SIL 行 `tag_present=0`、`commit_sha` 为空/NULL；④ 组件详情逐仓展示 tag 对照，缺 tag 仓显示“无”；⑤ 不产生远端 tag 创建/推送。
- **关联 NFR/OQ**：FR-OP-04、FR-STA-06；R-03 已关闭（附录 C `F-TAG_DRIFT` 第 4 仓固定 SYNCED）

#### AC-OP-13 · Tag 被移动：`fetch --force` 更新 + `tag_moved` 告警 + 禁 `--prune-tags`
- **覆盖 FR**：FR-OP-04
- **测试层级/依赖**：E2E（`F-TAG-MOVED`、`G-E2E` 移动 tag）
- **Given**：本地 tag `v1.0`→`c1`；远程同名 tag 被删除重建指向 `c2`。
- **When**：执行切 Tag `v1.0`（或手动刷新）。
- **Then**：① 工具以 `fetch --force`（禁止 `--prune-tags`）更新本地 tag 到 `c2`（断言 `git rev-parse v1.0`=`c2`）；② `repo_status.flags_json` 含 `tag_moved`，UI 警告“tag 被移动”；③ 组件 `aggregate_status=TAG_DRIFT`；④ `baseline_snapshot` 记录新 commit 且不丢历史行（旧行保留或按快照语义新增）；⑤ 其他 tag 未被 prune（本地多余 tag 仍在）。
- **关联 NFR/OQ**：FR-OP-04、§7.3；—

#### AC-OP-14 · annotated tag `^{commit}` 解引用与 `tag_present`
- **覆盖 FR**：FR-OP-04
- **测试层级/依赖**：UT + E2E（annotated tag 夹具）
- **Given**：annotated tag `v2.0`（tag 对象 sha ≠ commit sha `c3`）；轻量 tag `v1.0`→`c2`。
- **When**：切 Tag 并写快照。
- **Then**：① `baseline_snapshot.commit_sha` 均为 **commit sha**（`v2.0`→`c3`，不是 tag 对象 sha）；② 解引用使用 `^{commit}`（断言 git 命令输出与表值一致）；③ 检出后 `git tag --points-at HEAD` 正确；④ `tag_present=1`；⑤ `baseline_snapshot.tag_object_sha` 与 `commit_sha` 区分（SRS v0.2 §8.1 已补该列，可另存 tag 对象 sha；R-08 关闭）。
- **关联 NFR/OQ**：FR-OP-04、§8.1；—

#### AC-OP-15 · 受控 SYNC 成功（BEHIND）与 DIRTY 前置态：备份 ref→stash -u→确认→`reset --hard`→LFS pull
- **覆盖 FR**：FR-OP-05、SRS §7.6
- **测试层级/依赖**：E2E（`G-E2E` + `F-BEHIND`/`F-DIRTY`）+ UI 快照
- **Given**：`A_Design` 本地落后远程 3 提交且无本地独有提交（`BEHIND`）；`A_Impl` 有 1 个 tracked 改动（`DIRTY`）且无未推送 commit；远程分支 `main` 已 fetch 且 `remote_ref` 新鲜。
- **When**：对组件执行 SYNC 强覆盖（dry-run→确认→入队）：① `A_Design`（BEHIND）；② `A_Impl`（DIRTY），确认框勾选 stash 并输入确认。
- **Then**：① BEHIND 仓按可观测顺序完成：备份 ref `refs/backup/sync/<ts>/<branch>` 写入且 `job_item.backup_ref` 落库 → `reset --hard <remote-ref>` → LFS 仓随后 `lfs pull`；SYNC 后 HEAD=远程 tip、`main_status=SYNCED`，`git rev-parse <backup_ref>`=SYNC 前 HEAD，备份 ref 在 `git for-each-ref refs/backup/sync/` 可见，且未产生 merge commit（禁止 `git pull` 语义以“无 merge commit + HEAD=远程 tip”观测）；② DIRTY 仓：§7.6 DIRTY=`⚠️ stash -u + 确认`——未确认时零写入；确认后 `stash -u` 产生 1 条 stash（`git stash list` 可观测，含 untracked），SYNC 成功且 `main_status=SYNCED`，备份 ref 与 stash 均可恢复；③ **外部改动导致操作失败且零改动**：dry-run 期间由外部进程改动 HEAD 或 index → 入队执行失败并返回 `OP-E002`，仓库 `HEAD`/`git status`/`.git` 树 hash 与失败前一致（CAS 具体三元组属实现机制，不属验收，见 D-008）；④ 每仓 `job_item` 与 `operation_log` 可查；⑤ 任一仓失败不回滚另一仓（部分失败逐仓报告）。
- **关联 NFR/OQ**：FR-OP-05、§7.6、D-008；C-04（DIRTY=⚠️ stash -u+确认后成功）

#### AC-OP-16 · SYNC 保护（DIVERGED）：未推送 commit 丢失清单 + 默认阻断 + 备份 ref + 输入组件 ID 后成功且可恢复
- **覆盖 FR**：FR-OP-05、FR-SAFE-01、SRS §7.6
- **测试层级/依赖**：E2E（`F-DIVERGED` + `F-DIRTY`）
- **Given**：`A_Design` 主状态=`DIVERGED`（§7.2 优先级 8：本地有 2 个未推送 commit，同时远程分支前进 1 个提交，双方非祖先关系），另有 1 个 untracked 文件 + 1 条 stash。
- **When**：发起 SYNC → 查看 dry-run 预览 → 不做确认 → 再确认执行并输入正确组件 ID。
- **Then**：① dry-run 表逐字段断言：未推送 commit 列表（2 条，含短 SHA/作者/message）、untracked 清单（1 个路径）、stash 条数=1、影响仓数=4、将覆盖的仓；② 未点确认前**实际写入操作数=0**（`job` 不入队、无 ref 写入、无文件变化）；③ **默认阻断**：DIVERGED 下 SYNC 不自动合并，UI 提示“将丢失”与“可恢复路径”，确认需 `safety.requireTypedConfirm`（输入组件 ID 正确后才 enabled）；④ 确认后备份 ref `refs/backup/sync/<ts>/<branch>` 与 stash 写入，SYNC 完成后可从 `refs/backup/sync/*` 恢复那 2 个 commit（见 AC-OP-30），`main_status=SYNCED`（§7.6 DIVERGED=`⚠️ 默认阻断，备份 ref + 输入组件 ID 后允许`）；⑤ untracked 默认保留（见 AC-OP-18）。
- **关联 NFR/OQ**：FR-SAFE-01、§7.6；C-04（DIVERGED=⚠️ 默认阻断+备份 ref+输入 ID 后成功）

#### AC-OP-17 · SYNC 跳过表：UNMERGED/IN_PROGRESS/INVALID/EMPTY/DETACHED 逐仓跳过并报错码
- **覆盖 FR**：FR-OP-05、SRS §7.6
- **测试层级/依赖**：IT（`F-UNMERGED`、`F-IN_PROGRESS`、`F-INVALID-REMOTE`、`F-EMPTY`、`F-DETACHED-COMMIT`）
- **Given**：5 仓分别处于上述状态，1 仓 `F-BEHIND` 可同步。
- **When**：对组件执行 SYNC（含 dry-run 与确认）。
- **Then**：① 5 仓在预检表中逐行标记“跳过+原因”，SYNC 不执行 `reset --hard`（`.git` 对象/索引 hash 不变）；② 被跳过仓返回 `OP-E004`；③ 仅 `F-BEHIND` 仓完成 SYNC；④ `job.succeeded=1`、`failed=0`、`job.skipped=5`（SRS v0.2.1 §8.1 已补整数计数列），且被跳过 5 仓逐行 `job_item.state=SKIPPED`（SRS §8.1 合法值）；组件 `OP_PARTIAL_FAILED` 或按清除规则；⑤ DETACHED 仓：§7.6 中 SYNC=❌，跳过并提示“先建分支”（R-25 已关闭），不做目标分支猜测。
- **关联 NFR/OQ**：FR-OP-05、SRS §7.6；R-25 已关闭（§7.6 DETACHED SYNC=❌）

#### AC-OP-18 · SYNC 的 untracked（SYNCED 前置态）：默认保留且无需确认；显式勾选删除前必须 dry-run + 确认
- **覆盖 FR**：FR-OP-05、SRS §7.6
- **测试层级/依赖**：IT（`F-DIRTY` + untracked 夹具）
- **Given**：仓内有 3 个 untracked 文件与 1 个 untracked 目录，主状态=`SYNCED`（默认 `dirty.includeUntracked=false`，仅 untracked 不升 DIRTY）。
- **When**：① 默认 SYNC；② 勾选“删除 untracked”再 SYNC（先看 dry-run）。
- **Then**：① 默认：SYNC 属“无需确认”路径（§7.6 SYNCED=`—（无需；仅清理 untracked 需确认）`，不弹确认框、不要求输入组件 ID），4 项 untracked 全部保留，`flags_json` 含 `untracked`；② 勾选“删除 untracked”属需确认路径：dry-run 列出将被删除的 4 个路径；③ 未确认时文件仍在、无写入；④ 确认后 4 项被删除且可从备份/stash 恢复策略中得到说明（若不可恢复必须在确认框明示“不可恢复”）；⑤ 两种路径均写审计。
- **关联 NFR/OQ**：FR-OP-05、§7.6、附录 D `dirty.includeUntracked`；C-04（SYNCED=无需，仅清理 untracked 需确认）

#### AC-OP-19 · SYNC 拒绝覆盖本地独有分支 + `UPSTREAM_GONE` 场景
- **覆盖 FR**：FR-OP-05
- **测试层级/依赖**：IT（`F-NO_UPSTREAM-GONE`、本地独有分支夹具）
- **Given**：① 本地分支 `feat/local-only` 无远程同名分支；② `F-NO_UPSTREAM-GONE`（upstream 对应远程分支已删，需 `fetch --prune` 才能检测到）。
- **When**：对含上述分支的仓执行 SYNC 与 Fetch。
- **Then**：① `feat/local-only` 不被覆盖/删除，SYNC 明确拒绝并提示“本地独有分支”；② 场景② fetch 后 `NO_UPSTREAM(UPSTREAM_GONE)` 可被判定（默认 `remote.prune=true`，SRS v0.2 附录 D 已定，R-26 关闭），主状态=NO_UPSTREAM、聚合=NEW_BRANCH；③ SYNC 对 UPSTREAM_GONE 仓跳过；④ 远程删除的分支不在本地被静默删除；⑤ 结果入 `job_item.message`。
- **关联 NFR/OQ**：FR-OP-05、§7.2、附录 D `remote.prune`；R-26 已关闭

#### AC-OP-20 · 本地删除：软删除到 `.trash`、30 天可恢复、realpath 校验
- **覆盖 FR**：FR-OP-06、FR-SAFE-02
- **测试层级/依赖**：E2E（本地仓夹具 + FS）
- **Given**：`F-SYNCED` 与 `F-DIRTY` 两仓位于 `<root>`。
- **When**：① 删除 `F-SYNCED`（干净仓）；② 删除 `F-DIRTY`（默认应阻断）；③ 强制删除 `F-DIRTY`。
- **Then**：① 场景①：`<root>\<full_name>` 不存在，`<root>\.trash\<ts>\<full_name>` 存在且为完整 git 仓（`git log` 可读）；② 场景②被阻断，目录仍在，提示“有未提交改动/未推送提交”；③ 场景③强制后进入 `.trash`，`operation_log` 记录删除（含 `before_json` 含 HEAD/分支）；④ realpath 校验：删除目标必须在 `<root>` 下（构造 `..`/junction 越界拒绝 `FS-E001`）；⑤ 删除**不触碰** Gitea 远端（见 AC-SAFE-04）。
- **关联 NFR/OQ**：FR-OP-06、D-005/D-017；—

#### AC-OP-21 · 删除预检四项阻断（脏/未推送/进行中/stash）
- **覆盖 FR**：FR-OP-06
- **测试层级/依赖**：IT（`F-DIRTY`、`F-AHEAD`、`F-IN_PROGRESS` + stash 夹具）
- **Given**：4 仓分别为：dirty、AHEAD（未推送）、IN_PROGRESS、有 stash。
- **When**：分别发起删除。
- **Then**：① 4 组均默认阻断（`job` 不入队、不产生 `job` 行；SRS §8.1 `job.state` 无 BLOCKED 枚举），目录均保留；② 阻断弹层列出具体原因与“仍要删除”风险说明；③ 勾选“我已理解”并输入组件 ID 后才可强制；④ 强制删除后 4 目录进 `.trash`；⑤ 仅删除本地，远端无变化。
- **关联 NFR/OQ**：FR-OP-06、FR-SAFE-01；—

#### AC-OP-22 · `.trash` 保留期可配（7–90，默认 30）+ 清理任务 + 扫描排除
- **覆盖 FR**：FR-OP-06、D-017
- **测试层级/依赖**：IT（`CLK`、FS）
- **Given**：`.trash` 内含 3 个不同日期的删除记录（今天、T-29d、T-31d）；`delete.retentionDays=30`。
- **When**：推进 `CLK` 触发清理任务（及设置改为 7 后重跑）。
- **Then**：① 默认 30 天：T-31d 记录被清理、今天与 T-29d 保留；② 设置改为 7 后，T-29d 也被清理；③ 非法值（0/91）被拒（设置校验，见 AC-OPS-08）；④ 清理写审计；⑤ 扫描器**不**扫描 `.trash` 内目录（AC-SCAN-01 ⑤）；⑥ `.trash` 体积/条数可在设置或诊断中查看。
- **关联 NFR/OQ**：FR-OP-06、附录 D；—

#### AC-OP-23 · 占用文件导致的删除失败 → `DELETE_PENDING` 保留
- **覆盖 FR**：FR-OP-06
- **测试层级/依赖**：IT（持有文件句柄的夹具）
- **Given**：仓内某文件被测试进程以独占方式打开。
- **When**：执行强制删除。
- **Then**：① 删除失败但仓进入 `DELETE_PENDING` 态（DB 有标记、UI 显示“待删除”），目录保留；② 错误码明确（`FS-E001` 前缀类，见 R-06）且文案说明“文件被占用”；③ 释放句柄后重试成功（第二次 job 完成删除并进 `.trash`）；④ 无半删除状态（`.git` 未被部分破坏，`git status` 仍可运行）；⑤ 全过程审计。
- **关联 NFR/OQ**：FR-OP-06；—

#### AC-OP-24 · CREATE 分支 dry-run 表字段 + `check-ref-format` 非法输入
- **覆盖 FR**：FR-OP-07
- **测试层级/依赖**：IT（`G-E2E` 四仓）
- **Given**：四仓存在；目标分支名候选：`release/1.0`（合法且不存在）、`bad..name`（非法）、`main`（已存在）、`remote-only`（远程已删分支名）。
- **When**：分别输入 4 个名字并进入 dry-run 预览。
- **Then**：① 非法名 `bad..name` 在输入框即时被 `check-ref-format` 拒绝（按钮禁用 + `FS-E001` 类提示），不发请求；② `main` 行标“已存在/重名”，`release/1.0` 行标可创建；③ dry-run 表字段逐列断言：重名/远程已删/超前落后/非法/各仓状态/基线 ref；④ `remote-only` 行标“远程已删”并给处置选择；⑤ 表行数=4 仓（每仓一行）。
- **关联 NFR/OQ**：FR-OP-07（旧 AC-17 仅映射见 §1.4）；—

#### AC-OP-25 · CREATE 逐仓创建 + 1 仓重名预览 + 撤销已创建 + 基线 ref 记录
- **覆盖 FR**：FR-OP-07
- **测试层级/依赖**：E2E（`G-E2E` + 1 仓预置同名分支）
- **Given**：3 仓无 `release/1.0`，1 仓（MIL）已有同名分支。
- **When**：dry-run 后点“开始创建”。
- **Then**：① dry-run 中 MIL 行高亮“重名”，确认按钮按规则禁用或要求选择“跳过该仓”；② 创建后 3 仓存在 `release/1.0`、`job_item` 记录基线 ref（以创建时 HEAD SHA 作为基线，写入 `job_item.message`/基线字段，枚举见 SRS §8.1）；③ MIL 仓未被强改（原分支 HEAD 不变）；④ 点“撤销已创建分支”后 3 仓新分支被删除且 HEAD 恢复；⑤ 撤销写审计、不可撤销的仓给出说明。
- **关联 NFR/OQ**：FR-OP-07（旧 AC-17 仅映射见 §1.4）；—

#### AC-OP-26 · 任务中心：队列/进度/持久化/失败重试/重启不重复执行
- **覆盖 FR**：FR-OP-08、FR-SAFE-04
- **测试层级/依赖**：E2E（杀进程重启）
- **Given**：发起批量 Clone（4 仓，每仓克隆耗时 ≥2s 夹具）。
- **When**：任务进行到 2/4 时用 Windows 强制终止 Host 进程 → 重启应用 → 打开任务中心。
- **Then**：① 重启后 `job`/`job_item` 行仍在（持久化），已完成 2 行状态保持成功、进行中 1 行 `job.state=INTERRUPTED`（SRS §8.1 合法值）且该 `job_item.state=INTERRUPTED`（SRS v0.2.1 补列；不得标 OK），未开始 1 行保持待执行；② 重启不重复执行已完成 2 仓（目录 mtime 不变）；③ 提供“继续/重试失败项”入口；④ 任务中心按队列/进行中/历史分组；⑤ 进度显示组件级与仓级（`total/current`）且单调不减。
- **关联 NFR/OQ**：FR-OP-08、NFR-REL-03；R-20 已关闭（SRS §8.1 + v0.2.1 补 `job_item` 中断态）

#### AC-OP-27 · 取消语义：合作式取消、`CANCELLED` 逐仓、未开始保持原状、触发重扫
- **覆盖 FR**：FR-OP-08、SRS §7.6
- **测试层级/依赖**：E2E
- **Given**：批量 Checkout 4 仓，每仓操作耗时 ≥3s。
- **When**：点击“取消”任务。
- **Then**：① 取消后 ≤2s 内当前执行仓被终止并记 `CANCELLED`（`job_item.state`）；② 未开始的仓**保持原状**（HEAD/分支与取消前一致），不记失败；③ 已完成的仓不回滚；④ 取消后触发受影响仓重扫（`repo_status.calc_at` 前进）；⑤ `job.state=CANCELLED`、`job.cancel_requested=1`；⑥ 对不支持取消的操作（如 Fetch 瞬时任务）取消按钮禁用并提示。
- **关联 NFR/OQ**：FR-OP-08、SRS §7.6 取消语义；—

#### AC-OP-28 · 操作审计：字段齐全、可查询导出、保留 ≥90 天
- **覆盖 FR**：FR-OP-09
- **测试层级/依赖**：IT（`CLK`、DB 查询）
- **Given**：执行 1 次 SYNC、1 次删除、1 次恢复。
- **When**：查询 `operation_log` 并按时间导出。
- **Then**：① 每次写操作产生记录，字段齐全：`ts`（UTC ISO-8601）、`actor`、`action`、`target_json`（组件/仓集合）、`before_json`/`after_json`（HEAD/分支）、`result`、`error_code`（空或码）、`request_id`；② 审计可查询、可导出（CSV/JSON 至少 1 种），导出文件脱敏；③ 保留策略 `audit.keepDays=90` 生效：`CLK` 推进 91 天后旧记录被清理；④ 审计 sink 与普通日志分离；⑤ 恢复/取消等操作同样入审计。
- **关联 NFR/OQ**：FR-OP-09、NFR-REL-03；—

#### AC-OP-29 · 操作互斥：同组件串行、外部 `index.lock` → BUSY
- **覆盖 FR**：FR-OP-10
- **测试层级/依赖**：IT + E2E
- **Given**：对同一组件同时发起 2 个写操作（Pull 与 Checkout）。
- **When**：并发提交两个 job → 再对 1 仓制造外部 `index.lock` 后发起 Commit。
- **Then**：① 同组件两 job 串行执行（`job_item.started_at`/`finished_at` 为 SRS v0.2.1 补列；第二 job 的 `started_at` ≥ 第一的 `finished_at`），无交错写入；② 外部锁场景返回 `OP-E001`、`job_item.state=FAILED`、仓库零改动、锁文件保留；③ 不同组件可并行（断言可并行，受并发上限约束）；④ 无数据损坏（DB `quick_check`=ok、仓库 `git fsck` 无新增错误）；⑤ 不删锁、不重试。
- **关联 NFR/OQ**：FR-OP-10、OP-E001（旧 AC-26 仅映射见 §1.4）；R-20 已关闭（v0.2.1 补 `job_item` 时间列）

#### AC-OP-30 · 恢复：`.trash` 还原 + `refs/backup/*` 恢复 + 冲突预览 + 审计
- **覆盖 FR**：FR-OP-11、FR-OP-06、SRS §7.6
- **测试层级/依赖**：E2E（AC-OP-20/16 产物）
- **Given**：① 被删除到 `.trash/<ts>/A_Design`；② SYNC 产生 `refs/backup/sync/<ts>/main`（含 2 个未推送 commit）。
- **When**：① 任务中心→恢复→选择 `.trash` 记录→确认；② 组件详情→恢复→选择备份 ref→确认。
- **Then**：① 场景① 目录回到 `<root>\A_Design`，`git log` 与删除前一致，`.trash` 中该记录被移除，`operation_log` 记恢复；② 场景② 恢复后本地分支包含那 2 个 commit（`git rev-parse` 对比备份 ref）；③ 恢复前展示冲突：目标已存在/远程已变动/备份 ref 已过期均提示并需确认；④ 冲突时默认不覆盖、可取消；⑤ 恢复不写远端（Gitea 零写请求）；⑥ **备份 ref 生命周期（D-017/H-05）**：SYNC 产生的 `refs/backup/sync/*` 在保留期内不被清理（默认 30 天：`CLK` 推进 29 天仍存在、推进 31 天由清理任务回收），任务中心可见且可恢复；保留期键=`backup.retentionDays`（SRS v0.2.1 附录 D：默认 30、范围 7–90、即时生效）。
- **关联 NFR/OQ**：FR-OP-11、D-017（旧 AC-19 仅映射见 §1.4）；H-05（备份 ref 保留 ≥30 天）

#### AC-OP-31 · 组件纳入：按组件 ID 校验四仓并展示将 clone 的仓
- **覆盖 FR**：FR-OP-12
- **测试层级/依赖**：E2E（`G-E2E`）
- **Given**：组织有组件 `NEWC` 四仓，本地未克隆。
- **When**：在“纳入组件”输入 `NEWC` → 校验 → 确认纳入。
- **Then**：① 校验显示 4 仓齐全并将 clone 的仓清单（4 行 full_name）；② 纳入后看板出现该组件（组件计数 +1），`component` 行写入；③ 输入不存在的 ID → 明确提示“远程不存在”，不写入 DB；④ 输入缺仓的组件 → 列出缺失仓并提示 `BINDING_ERROR`，仍允许纳入（按 D-015）；⑤ 纳入写审计。
- **关联 NFR/OQ**：FR-OP-12（旧 AC-20 仅映射见 §1.4）；—

#### AC-OP-32 · 组件移出：本地删除 vs 仅停止管理，看板/DB 一致 + 审计
- **覆盖 FR**：FR-OP-12
- **测试层级/依赖**：E2E
- **Given**：组件 `A`（4 仓已克隆）。
- **When**：① 选择“仅停止管理”；② 重新纳入后再选“本地删除”。
- **Then**：① 场景①：`component` 行保留（`is_managed=0`/停止管理标记，SRS v0.2 §8.1 已有 `is_managed`；R-27 关闭），看板默认视图不再显示（或移入“已移出”区），本地目录保留；② 场景②：目录进 `.trash`、`repo.is_local=0`、看板不再显示；③ 两种方式均写审计；④ 重新纳入后可恢复显示；⑤ 远端均零变更。
- **关联 NFR/OQ**：FR-OP-12（旧 AC-20 仅映射见 §1.4）；R-27 已关闭（SRS v0.2 §8.1 `component.is_managed/is_deleted`）

### 2.8 单仓操作（FR-REPO）

#### AC-REPO-01 · 单仓 Fetch：结果与命令行一致
- **覆盖 FR**：FR-REPO-01
- **测试层级/依赖**：IT（`G-E2E` + `F-BEHIND`）
- **Given**：`A_Design` 远程领先 2 提交、本地 `remote_ref` 旧。
- **When**：单仓详情点“Fetch”。
- **Then**：① `remote_ref` 更新到新 tip（`observed_at` 前进）；② 与手工 `git fetch` 后 `git rev-parse origin/main` 结果一致；③ 不改变本地 HEAD 与工作区（`git status` 前后相同）；④ 操作前自动重扫（`scan_at` 前进）；⑤ 结果入任务中心/状态栏（单仓操作即时反馈）。
- **关联 NFR/OQ**：FR-REPO-01、NFR-PERF-08；—

#### AC-REPO-02 · 单仓 Pull（ff-only）：成功后无 merge，非快进拒绝
- **覆盖 FR**：FR-REPO-01
- **测试层级/依赖**：IT（`F-BEHIND`、`F-DIVERGED`）
- **Given**：`F-BEHIND`（可快进）与 `F-DIVERGED`（非快进）两仓。
- **When**：分别点“Pull”。
- **Then**：① 快进仓：HEAD 前进到远程 tip、`main_status=SYNCED`、`git rev-list --merges` 不增、无 merge commit；② 分叉仓：被拒并提示“非快进，请手动处理”，HEAD 不变、无 `MERGE_HEAD`；③ 两场景操作前均自动重扫；④ 失败时错误码与文案可见。
- **关联 NFR/OQ**：FR-REPO-01；—

#### AC-REPO-03 · 单仓 Checkout 分支/Tag：dirty 规则一致
- **覆盖 FR**：FR-REPO-01
- **测试层级/依赖**：IT（`F-DIRTY`、`F-SYNCED`）
- **Given**：干净仓、dirty 仓各 1。
- **When**：① 干净仓 Checkout 分支 `dev`；② 干净仓 Checkout tag `v1.0`；③ dirty 仓 Checkout 分支。
- **Then**：① 分支切换成功 `head_ref=dev`；② tag 检出为 detached 且 `main_status=DETACHED_TAG`；③ dirty 仓按 7.6 阻断（或需 stash 确认，与 AC-OP-10 同规则）；④ 与 §7.6 矩阵一致；⑤ 失败仓库零改动。
- **关联 NFR/OQ**：FR-REPO-01、SRS §7.6；—

#### AC-REPO-04 · Push 成功（有 upstream）
- **覆盖 FR**：FR-REPO-02
- **测试层级/依赖**：E2E（`G-E2E` + `F-AHEAD`）
- **Given**：`F-AHEAD` 仓（上游分支存在、本地领先 2 提交、有 `write:repository` 权限）。
- **When**：单仓详情点“Push”。
- **Then**：① Push 成功，远程分支 tip=本地 HEAD；② 本地 `main_status=SYNCED`（重扫后）；③ 组件 `aggregate_status` 按新状态重算；④ 任务/操作记录写审计（`action` 含 push）；⑤ 只推当前分支（其他分支远端不变）。
- **关联 NFR/OQ**：FR-REPO-02；—

#### AC-REPO-05 · Push 四类失败面：无 upstream / detached / 分支保护 / 网络
- **覆盖 FR**：FR-REPO-02
- **测试层级/依赖**：E2E（`G-E2E` 保护分支 + 本地夹具）
- **Given**：① 无 upstream 的分支；② detached HEAD 仓；③ 远程受保护分支（服务端拒绝）；④ 断网。
- **When**：分别点“Push”。
- **Then**：① 无 upstream：提示“发布分支”，提供 set-upstream 引导（FR-REPO-05），不静默推送；② detached：禁止 Push 并提示“基于当前 HEAD 建分支”（FR-REPO-05）；③ 保护分支：展示**服务端原文消息**（断言 UI 含服务端返回字符串），不吞错；④ 断网：`NET-E001`，本地零改动；⑤ 四场景均不产生 force push。
- **关联 NFR/OQ**：FR-REPO-02；—

#### AC-REPO-06 · Commit（最小化）：提交 staged 变更；unmerged 时禁止
- **覆盖 FR**：FR-REPO-03
- **测试层级/依赖**：IT（`F-DIRTY`、`F-UNMERGED`）
- **Given**：① 仓有 1 个已 staged 文件 + 1 个 unstaged 文件；② `F-UNMERGED` 仓。
- **When**：① 输入 message 点 Commit；② 对 unmerged 仓点 Commit。
- **Then**：① 场景① 仅 staged 文件被提交（`git show --stat` 只含 1 文件），成功后 `main_status` 转为 `AHEAD`（有未推送提交）或 `SYNCED`（随后 push 测试）；② message 与输入一致；③ 场景② 被拒并提示“存在未解决冲突”，`git status` 不变；④ 不提供冲突解决/rebase UI（页面无相关控件断言）；⑤ 提交写审计。
- **关联 NFR/OQ**：FR-REPO-03；—

#### AC-REPO-07 · Reset（v1.1）：v1 不暴露入口
- **覆盖 FR**：FR-REPO-04（P2，v1.1）
- **测试层级/依赖**：VIS + IT
- **Given**：v1 构建。
- **When**：在单仓详情与命令面板搜索“Reset”。
- **Then**：① v1 无 Reset 入口/命令（DOM 与 IPC 均无 `RESET` 类型），不得出现死按钮；② 若通过实验开关暴露，必须先备份 ref 再执行（v1.1 验收口径：备份 ref 存在且可恢复，`refs/backup/reset/<ts>/<branch>`）；③ 本 AC 在 M0 仅冻结 ①，② 随 v1.1 冻结。
- **关联 NFR/OQ**：FR-REPO-04；§4.1 OQ-24（版本归属）

#### AC-REPO-08 · INVALID 修复：Repair / Re-clone
- **覆盖 FR**：FR-REPO-05、SRS §7.6
- **测试层级/依赖**：IT（`F-INVALID-NOTREPO`、`F-INVALID-REMOTE`）
- **Given**：① 目录非 git 仓（`F-INVALID-NOTREPO`）；② 是 git 仓但 origin 不匹配（`F-INVALID-REMOTE`）。
- **When**：分别执行 Repair（①）/ Re-clone（②，含备份现有目录到 `.trash`）。
- **Then**：① 场景① Repair 后目录成为合法仓且 origin 匹配，`main_status` 脱离 INVALID（→ SYNCED/EMPTY 等真实态）；② 场景② Re-clone 先备份原目录进 `.trash` 再 clone，成功后 `is_local=1`、`main_status` 正确；③ 两场景均走预检+job+逐仓结果；④ 组件 `BINDING_ERROR` 在修复后按真实状态重算（若四仓齐则脱离 BINDING_ERROR）；⑤ 审计记录 repair/re-clone。
- **关联 NFR/OQ**：FR-REPO-05（旧 AC-18 仅映射见 §1.4）；—

#### AC-REPO-09 · `NO_UPSTREAM` 三子型修复：set-upstream / 发布分支
- **覆盖 FR**：FR-REPO-05
- **测试层级/依赖**：E2E（`F-NO_UPSTREAM-LOCAL/UNSET/GONE`）
- **Given**：三仓分别处于 `LOCAL_ONLY`（远程无同名分支）、`UNSET_BUT_EXISTS`（远程有但未设 tracking）、`UPSTREAM_GONE`（远程已删）。
- **When**：分别执行修复动作。
- **Then**：① `LOCAL_ONLY`：引导“发布分支”（push -u 建 upstream），成功后 `main_status` 重新计算；② `UNSET_BUT_EXISTS`：`git branch --set-upstream-to` 成功，`upstream_ref` 写入，主状态变为关系态（BEHIND/AHEAD/SYNCED）；③ `UPSTREAM_GONE`：引导改名/新建远程分支或清除 upstream，处理后不再显示该子型；④ 三场景均走预检+job+逐仓结果；⑤ 修复失败时仓库零改动。
- **关联 NFR/OQ**：FR-REPO-05、§7.2 NO_UPSTREAM 三子型；—

#### AC-REPO-10 · DETACHED（tag/commit）基于当前 HEAD 建分支
- **覆盖 FR**：FR-REPO-05
- **测试层级/依赖**：IT（`F-DETACHED-TAG`、`F-DETACHED-COMMIT`）
- **Given**：两仓分别 detached 于 tag 与游离 commit；当前 HEAD `h1`。
- **When**：执行“基于当前 HEAD 建分支”（分支名 `baseline/v1.0`）→ 再切回原分支。
- **Then**：① 新分支 `baseline/v1.0` 指向 `h1`（`git rev-parse` 断言）；② 检出新分支后 `head_type`=分支、`main_status` 重新计算（如 `AHEAD`/`NO_UPSTREAM`）；③ 分支名合法性校验同 AC-OP-24；④ 失败时 HEAD 仍为 detached、零改动；⑤ 审计记录。
- **关联 NFR/OQ**：FR-REPO-05（旧 AC-18 仅映射见 §1.4）；—

#### AC-REPO-11 · `EMPTY` 空仓首次推送并建 upstream
- **覆盖 FR**：FR-REPO-05、SRS §7.6
- **测试层级/依赖**：E2E（`F-EMPTY` + `G-E2E` 空仓）
- **Given**：`F-EMPTY`（unborn HEAD）仓，本地有首个 commit 待推。
- **When**：点“初始化/推送首个 commit”。
- **Then**：① 首个 commit 推送成功并建立 upstream（`upstream_ref` 非空）；② `main_status` 由 `EMPTY` 变为 `SYNCED`；③ 远程分支出现且 tip 一致；④ 该操作不被 FR-REPO-02“需有 upstream”误拦（SRS v0.2 FR-REPO-02 已加 EMPTY 首推例外，R-05 关闭）；⑤ 审计记录。
- **关联 NFR/OQ**：FR-REPO-05、GIT-E001；R-05 已关闭（SRS v0.2 FR-REPO-02 例外）

### 2.9 破坏性操作与安全（FR-SAFE）

#### AC-SAFE-01 · dry-run“将丢失什么”字段完整
- **覆盖 FR**：FR-SAFE-01
- **测试层级/依赖**：VIS + IT（`F-AHEAD`+`F-DIRTY`+untracked/stash）
- **Given**：待 SYNC 的组件含 3 个未推送 commit、4 个 untracked、2 条 stash；待删除组件含 dirty 仓。
- **When**：分别打开 SYNC dry-run 与 Delete 预检。
- **Then**：① 预览表逐字段断言：commit 列表（短 SHA+作者+message，条数=3）、untracked 清单（路径数=4）、stash 条数=2、影响仓数、将被覆盖的分支/tag；② 删除预检列出 dirty 仓数与未推送数（与夹具一致）；③ 预览为只读表（无编辑控件）；④ 表数据来自写前 read-through（非 DB 缓存）：在预览打开后外部修改仓库，点“刷新预览”数据变化；⑤ 默认动作=阻断（确认按钮非默认聚焦）。
- **关联 NFR/OQ**：FR-SAFE-01（旧 AC-07 仅映射见 §1.4）；—

#### AC-SAFE-02 · 未确认时写入操作数=0（默认阻断）
- **覆盖 FR**：FR-SAFE-01、SRS §7.6
- **测试层级/依赖**：IT（DB+FS 快照）
- **Given**：预览已展示，记录 DB 全表行数与仓库 `.git`/工作区 hash 快照。
- **When**：① 点“取消”；② 直接关闭弹层；③ 按 Esc。
- **Then**：① 三种退出方式后 `job`/`job_item` 行数不增；② 仓库 `.git` 与工作区快照不变；③ 无 `refs/backup/*` 产生；④ 无审计“已执行”记录（最多记录“取消”）；⑤ 再打开预览结果仍为新快照。
- **关联 NFR/OQ**：FR-SAFE-01；—

#### AC-SAFE-03 · 强制覆盖需输入组件 ID（`safety.requireTypedConfirm=true`）
- **覆盖 FR**：FR-SAFE-01
- **测试层级/依赖**：IT + VIS
- **Given**：`safety.requireTypedConfirm=true`；组件 `CF325F_ChanganSigProcgBascFct` 的 SYNC 强覆盖预览。
- **When**：① 不输入直接点“确认”；② 输入错误 ID；③ 输入正确 ID。
- **Then**：① ①② 确认按钮 `disabled`/点击无效，无任何写入；② ③ 才入队执行；③ 输入框匹配规则（SRS v0.2.1 附录 D 冻结）：**精确匹配、区分大小写、忽略首尾空格**——`cf325f_…`（小写）不通过、` CF325F_ChanganSigProcgBascFct `（首尾空格）通过、多/少字符不通过；④ 设置改为 `false` 后不再要求输入但仍有二次确认；⑤ 确认操作写审计（含确认方式）。
- **关联 NFR/OQ**：FR-SAFE-01、附录 D `safety.requireTypedConfirm`；R-28 已关闭（SRS v0.2.1：精确匹配/区分大小写/忽略首尾空格）

#### AC-SAFE-04 · 删除仅本地：固定文案 + Gitea 无任何写请求
- **覆盖 FR**：FR-SAFE-02
- **测试层级/依赖**：IT（`G-WIRE` 请求记录）
- **Given**：Gitea 端由 `G-WIRE` 代理并记录全部请求；执行 AC-OP-21 的强制删除。
- **When**：删除确认弹层与执行全过程。
- **Then**：① 弹层固定文案含“远程仓库不受影响”（文案快照逐字断言）；② `G-WIRE` 记录中 `DELETE/PUT/PATCH/POST` 方法请求数=0（仅允许 GET 读）；③ 远端仓仍存在（`G-E2E` 侧断言）；④ 本地目录进 `.trash`；⑤ 审计只记本地删除。
- **关联 NFR/OQ**：FR-SAFE-02、D-005（旧 AC-08 仅映射见 §1.4）；—

#### AC-SAFE-05 · 日志脱敏规则单测（五类模式）
- **覆盖 FR**：FR-SAFE-03
- **测试层级/依赖**：UT（脱敏函数）+ IT
- **Given**：样本串：`Authorization: Bearer abc123`、`Authorization: token abc123`、URL `https://user:pass@gitea/x`、`?token=abc123&x=1`、`access_token=abc123`、git stderr 含 `remote: invalid token abc123`、`git remote -v` 输出含凭据 URL。
- **When**：经脱敏函数处理后写日志。
- **Then**：① 五类模式全部被替换（输出不含 `abc123`/`pass`，保留结构占位如 `[REDACTED]`）；② 非敏感字段（URL 主机、用户名、repo 名）保留；③ 脱敏函数幂等（二次处理结果不变）；④ 日志落盘文件扫描无 `abc123`；⑤ 单测覆盖正/反例各 ≥1。
- **关联 NFR/OQ**：FR-SAFE-03、SRS §10.3；—

#### AC-SAFE-06 · 诊断包：必需项齐全 + 导出前自动脱敏
- **覆盖 FR**：FR-SAFE-03、FR-OPS-02
- **测试层级/依赖**：IT（导出包解压检查）
- **Given**：已产生日志、1 次同步、1 次任务、DB 有数据；token 哨兵 `ghp_CANARY_9f8e7d6c5b4a`。
- **When**：一键导出诊断包。
- **Then**：① 包内必需项：脱敏配置、进程状态、最近任务（≥50 条）、DB 版本（`PRAGMA user_version` + `sqlite_version()`）、转储清单、日志、`VACUUM INTO` DB 快照、`git --version`、OS/WebView2 版本（SRS v0.2 FR-OPS-02 已改为引用 §12.5 清单，R-14 关闭）；② 全包扫描哨兵出现次数=0；③ 包可完整解压且文件非空；④ 导出动作写审计；⑤ 导出路径用户可选、默认 `diag\`。
- **关联 NFR/OQ**：FR-SAFE-03、FR-OPS-02；R-14 已关闭（FR-OPS-02 引用 §12.5）

#### AC-SAFE-07 · 崩溃转储：默认关闭或导出前脱敏
- **覆盖 FR**：FR-SAFE-03、SRS §10.3
- **测试层级/依赖**：IT（强制崩溃 + 转储扫描）
- **Given**：触发一次托管崩溃（测试注入 `Environment.FailFast` 类等价手段）；转储目录 `dumps\`。
- **When**：检查转储生成策略与内容。
- **Then**：① 若默认开启：转储目录仅保留最近 N 个且有大小上限；② 转储内**不得**含明文 token（哨兵扫描=0）；若实现默认关闭，断言 `dumps\` 为空且设置项说明“默认关闭”；③ 诊断包中的“转储清单”与实际文件一致；④ 转储文件不在 `%LOCALAPPDATA%` 之外落盘（路径断言）；⑤ 策略写入设置页文案（可见）。
- **关联 NFR/OQ**：FR-SAFE-03、SRS §10.3；—

#### AC-SAFE-08 · 写前 journal：pre/expect/post 三元组落 `job_item`
- **覆盖 FR**：FR-SAFE-04
- **测试层级/依赖**：IT（DB 查询 + 写操作）
- **Given**：对 1 仓执行 Pull（快进场景）。
- **When**：执行前/中/后查询 `job_item`。
- **Then**：① 写操作**开始前** `job_item.pre_head` 已落库（与执行时 HEAD 一致）；② 执行前记录 `expect_head`（如 CAS 期望值）；③ 完成后 `post_head` 落库；④ 三者与 git 命令输出一致；⑤ 若执行中 HEAD 被外部改变，操作失败 `OP-E002`，`job_item.state=FAILED` 且不写“成功”（只断言“外部改动→失败+零改动”，CAS 具体三元组不属验收）。
- **关联 NFR/OQ**：FR-SAFE-04、SRS §7.5 写前 read-through；—

#### AC-SAFE-09 · 崩溃后 reconcile：不猜测成功
- **覆盖 FR**：FR-SAFE-04、NFR-REL-03
- **测试层级/依赖**：E2E（强制终止进程）
- **Given**：批量 SYNC 执行到第 2 仓时强制终止 Host 与 GitWorker 进程（终止实现机制不属验收，只要求进程确实终止）。
- **When**：重启应用触发启动 reconcile。
- **Then**：① 第 1 仓（已完成）`job_item.state=OK` 且 `post_head` 有值；② 第 2 仓（中断）`job.state=INTERRUPTED` 且 `job_item.state=INTERRUPTED`（SRS v0.2.1 补列）或 `FAILED` + 错误码，**不得**标成功；③ 第 3/4 仓未开始保持待执行；④ reconcile 以 git 实际状态为准（对中断仓重扫后 `main_status` 与真实一致）；⑤ 用户可见“上次任务被中断”通知，可重试失败项。
- **关联 NFR/OQ**：FR-SAFE-04、NFR-REL-03；R-20 已关闭（v0.2.1 补 `job_item` 中断态/时间列）

#### AC-SAFE-10 · 危险扩展名二次确认（与文件树联动）
- **覆盖 FR**：FR-SAFE-05、FR-DETAIL-03
- **测试层级/依赖**：IT + 安全用例
- **Given**：文件树含 `.exe/.bat/.cmd/.ps1/.lnk/.msi/.scr` 各 1。
- **When**：逐个双击。
- **Then**：① 7 类全部弹二次确认（`data-danger-ext` 标识扩展名）；② 点“取消”后进程监控无新进程、无新增审计“已打开”；③ 点“确认”后由系统默认打开且审计记录；④ SRS v0.2.1 白名单内的 12 类（`.m/.c/.h/.slx/.mdl/.json/.xml/.yaml/.yml/.txt/.md/.csv`）双击直开、不弹确认；⑤ 白名单外非危险扩展名与危险 7 类（`.exe/.bat/.cmd/.ps1/.lnk/.msi/.scr`）一律二次确认（与 FR-DETAIL-03 逐项一致，R-30 关闭）。
- **关联 NFR/OQ**：FR-SAFE-05；R-30 已关闭（SRS v0.2.1 FR-DETAIL-03 已列白名单 12 类）

#### AC-SAFE-11 · 路径穿越与 reparse 越界拒绝
- **覆盖 FR**：FR-SAFE-05
- **测试层级/依赖**：IT（恶意路径夹具）
- **Given**：恶意输入集：`..\..\x`、`C:\Windows\x`、`\\host\share\x`、保留名 `CON`、尾随点/空格名、大小写冲突路径、junction 指向 root 外。
- **When**：通过 删除/打开/写入 接口传入。
- **Then**：① 全部拒绝并返回 `FS-E001`；② 无文件创建/删除/读取发生（FS 快照不变）；③ 日志记录拒绝且不泄漏完整敏感路径；④ root 内合法路径的读/打开操作成功（返回成功态与文件内容）；⑤ reparse 越界时 `reparse_point` flag 与拒绝码同时出现。
- **关联 NFR/OQ**：FR-SAFE-05、FS-E001；—

#### AC-SAFE-12 · 启动扫描既有仓凭据 → `SEC-E001` 告警 + 脱敏展示 + 不自动改配置
- **覆盖 FR**：FR-SAFE-06
- **测试层级/依赖**：IT（内嵌 token 的夹具仓）
- **Given**：既有仓 `.git/config` 的 origin URL 含 `https://user:ghp_LEAK_123456@host/repo.git`；另一仓含 `insteadOf` 明文凭据；再一仓 clean。
- **When**：启动/首次扫描既有仓库。
- **Then**：① 两个问题仓触发告警，`data-error-code="SEC-E001"`，UI 列表显示仓库名与脱敏后的 URL（`ghp_****3456` 形式）；② **不自动修改**用户 `.git/config`（文件 hash 前后一致）；③ clean 仓无告警；④ 提供“查看处理指引”入口（不提供一键改配置）；⑤ 告警进入通知中心并持久化（FR-OPS-01）。
- **关联 NFR/OQ**：FR-SAFE-06、SEC-E001（旧 AC-14 仅映射见 §1.4）；R-11 已关闭

#### AC-SAFE-13 · Token header-only：抓包/命令行/remote URL 均无凭据
- **覆盖 FR**：FR-SAFE-06、D-006
- **测试层级/依赖**：IT（`G-WIRE` 头部断言 + 进程命令行采集 + 网络抓包）
- **Given**：token=`ghp_CANARY_9f8e7d6c5b4a`；执行 clone/fetch/push 全过程。
- **When**：采集：① 全部 Gitea REST 请求头；② git 网络请求（`G-E2E` 服务端日志或本地抓包）；③ 全部进程命令行；④ 所有 remote URL。
- **Then**：① REST 请求凭据仅出现在 `Authorization: token …` 头；② git 网络操作认证成功（服务端日志可见），但进程命令行/落盘 env/remote URL 中无凭据（凭据通道用 `GIT_ASKPASS` 或凭据助手等实现机制不属验收，见 D-008）；③ 进程 argv 无哨兵；④ `remote_url` 无 userinfo/token；⑤ 无 `?token=`/`access_token=` 出现在任何 URL；⑥ 全过程无 `sslVerify=false`。
- **关联 NFR/OQ**：FR-SAFE-06、D-006（旧 AC-14 仅映射见 §1.4）；SRS v0.2 附录 D 已明确 `ci.token` 只存凭据引用（与 §12.6 同模型）

### 2.10 任务/通知/日志/设置（FR-OPS）

#### AC-OPS-01 · 通知四类触发 + `notification` 持久化 + 已读/清除
- **覆盖 FR**：FR-OPS-01
- **测试层级/依赖**：IT（`G-E2E` 注入 + DB）
- **Given**：分别注入 4 类事件：① 组件状态恶化（SYNCED→DIVERGED）；② 批量完成（成功/部分失败各 1）；③ Token 过期（401）；④ 远程同步失败（覆盖不足）。
- **When**：逐一触发后打开通知抽屉。
- **Then**：① 4 类各产生 ≥1 条 `notification` 行，`type` 取 SRS §8.1 CHECK 集（STATE_CHANGED/JOB_PARTIAL/TOKEN_EXPIRED/SYNC_FAILED/CI_EVENT）、`payload_json` 含上下文（组件/仓/错误码）；② 通知中心计数=未读条数；③ 点“已读”后 `read_at` 非空、计数减 1；④ 点“清除”后对应 `notification` 行被**删除**（SRS v0.2.1 FR-OPS-01：清除=删除通知行），列表为空；⑤ 重启后未读通知仍存在（持久化）；⑥ **失败路径**：注入 `notification` 写库失败 → 降级为 UI Toast + 日志错误码，不阻断触发它的主流程（如任务完成仍写 `job.state` 终态并触发受影响仓重扫），且失败本身写审计。
- **关联 NFR/OQ**：FR-OPS-01；R-20/R-31 已关闭（清除=删除通知行）

#### AC-OPS-02 · 通知中心 CI 事件类目：类型枚举存在 + 空分组（CI-6 锚点）
- **覆盖 FR**：FR-OPS-01、FR-CI-01
- **测试层级/依赖**：VIS + IT
- **Given**：`ci.enabled=false`。
- **When**：打开通知中心。
- **Then**：① 通知中心存在 “CI” 类目/分组（`data-ci-category` 存在），显示空态“CI 未接入”（不得空白/裸 TODO）；② IPC 事件契约中 `CI_UPDATED` 类型已注册（即使 v1 不发送）；③ 其它 4 类通知不受影响；④ 快照测试通过。
- **关联 NFR/OQ**：CI-6、FR-CI-02；—

#### AC-OPS-03 · 日志：10MB×30 天滚动 + 结构化字段 + 级别
- **覆盖 FR**：FR-OPS-02
- **测试层级/依赖**：IT（`CLK` + 写入压测）
- **Given**：日志目录初始为空；`log.keepDays=30`。
- **When**：① 写入 >25MB 日志（脚本快速灌入）；② 推进 `CLK` 31 天；③ 检查结构化字段。
- **Then**：① 单文件滚动上限 10MB（`logs\host-*.log` 每文件 ≤10MB，滚动产生多个文件）；② 31 天后超过保留期的文件被清理；③ 每条结构化日志含 `job_id`/`repo_id`/`component_id`/`request_id`/`duration_ms`/`http_status`/`exit_code` 中适用字段（抽样断言）；④ 级别枚举 `Verbose/Debug/Information/Warning/Error/Fatal` 可按级过滤；⑤ 审计日志不在普通滚动目录（独立 sink）；⑥ **失败路径**：日志目录不可写（只读目录注入）→ 应用不崩溃、状态栏/日志页提示 `FS-E001` 降级、主功能继续可用，并在恢复可写后自动续写。
- **关联 NFR/OQ**：FR-OPS-02、SRS §12.5；—

#### AC-OPS-04 · 审计日志独立 sink，保留 ≥90 天
- **覆盖 FR**：FR-OPS-02、FR-OP-09
- **测试层级/依赖**：IT（`CLK`）
- **Given**：`audit.keepDays=90`；已产生 100 条审计。
- **When**：推进 `CLK` 至 91 天；检查普通日志目录与审计目录。
- **Then**：① 审计文件位于独立 `audit\` 目录，不与普通日志混写；② 90 天内记录保留、超出被清理；③ 审计记录不可被普通日志清理任务删除；④ 审计文件同样脱敏（哨兵扫描=0）；⑤ 90 天阈值可配（30–365）且生效。
- **关联 NFR/OQ**：FR-OPS-02、附录 D；—

#### AC-OPS-05 · 日志级别过滤/搜索/暂停
- **覆盖 FR**：FR-OPS-02
- **测试层级/依赖**：IT + VIS（P7 日志页）
- **Given**：日志含 6 个级别各 ≥2 条，含关键字 `FAILED_SYNC`。
- **When**：① 选级别 `Warning`；② 搜索 `FAILED_SYNC`；③ 点“暂停”后再产生新日志。
- **Then**：① 级别过滤=显示 ≥所选级别（SRS v0.2.1 FR-OPS-02）：选 `Warning` 时显示 Warning/Error/Fatal，不显示 Information 及以下；② 搜索命中行数=关键字实际条数且高亮；③ 暂停后新日志不刷新显示（计数提示“有 N 条新日志”），恢复后补齐；④ 日志内容只读；⑤ 支持复制选中行（不含脱敏前内容）。
- **关联 NFR/OQ**：FR-OPS-02；R-32 已关闭（SRS v0.2.1：显示 ≥所选级别）

#### AC-OPS-06 · 一键诊断包：内容清单与导出流程
- **覆盖 FR**：FR-OPS-02、FR-SAFE-03
- **测试层级/依赖**：IT（与 AC-SAFE-06 共用产物）
- **Given**：运行 ≥10 分钟后导出。
- **When**：点“导出诊断包”→选择路径→导出。
- **Then**：① 导出进度可见、完成后提示路径；② 包内至少含 8 项：配置（脱敏）、进程状态、最近任务、DB 版本、日志、转储清单、DB 快照、环境版本（以 §12.5 清单为准，R-14 关闭）；③ 包大小 ∈[1KB, 500MB]（超出上限报错并给错误码）；④ 导出失败给出错误码与原因（磁盘/权限）；⑤ 导出路径越界（网络盘/系统目录）被拒 `FS-E001`。
- **关联 NFR/OQ**：FR-OPS-02；R-14 已关闭（FR-OPS-02 引用 §12.5）

#### AC-OPS-07 · 设置页六分区 + 附录 D 设置键逐项核对（v0.2.1：32 键）
- **覆盖 FR**：FR-OPS-03
- **测试层级/依赖**：IT + VIS
- **Given**：附录 D 全量设置键表（以 SRS v0.2.1 附录 D 为唯一清单；实测 **32 键**：`workspace.root`、`server.baseUrl`、`server.org`、`auth.mode`、`proxy.mode`、`proxy.url`、`tls.extraCa`、`git.path`、`scan.fingerprintIntervalSec`、`scan.hotIntervalSec`、`scan.coldIntervalMin`、`remote.listWatermarkMin`、`remote.refreshMin`、`remote.fullSweepHours`、`fetch.beforeWrite`、`ttl.localSec`、`ttl.fetchSec`、`ttl.remoteSec`、`delete.retentionDays`、`backup.retentionDays`、`log.keepDays`、`audit.keepDays`、`ui.theme`、`ui.density`、`ui.reduceMotion`、`ci.enabled`、`ci.baseUrl`、`ci.token`、`ci.pollIntervalSec`、`dirty.includeUntracked`、`remote.prune`、`safety.requireTypedConfirm`）。
- **When**：打开 P8 设置页，逐分区核对键。
- **Then**：① 分区固定六组：通用/工作区/认证/操作与安全/CI 集成（禁用占位）/实验特性（v1 无可配置键，仅说明/开关占位，R-18 关闭）；② 附录 D 每个键均可在 UI 找到对应控件（键→控件映射表 100% 命中，**32/32**；含 `backup.retentionDays`、`ci.token`、`dirty.includeUntracked`、`remote.prune`、`safety.requireTypedConfirm`）；③ `ci.enabled/ci.baseUrl/ci.token/ci.pollIntervalSec` 在 CI 分区且为禁用态 + 实验标记（`ci.token` 控件只显示“已存凭据引用/尾 4 位”）；④ 每控件显示当前值与默认值（重置入口存在），缺键即失败（不得出现附录 D 无定义的设置项）；⑤ 快照通过。
- **关联 NFR/OQ**：FR-OPS-03、附录 D；R-13/R-18 已关闭（CI 4 键齐；实验特性 v1 无键）

#### AC-OPS-08 · 设置校验与生效范围（即时/重启）逐键（v0.2.1：32 键全覆盖）
- **覆盖 FR**：FR-OPS-03
- **测试层级/依赖**：IT（`CLK`）
- **Given**：附录 D 32 键逐键非法/边界样本（如 `delete.retentionDays=0`/`91`、`backup.retentionDays=0`/`91`、`scan.fingerprintIntervalSec=4`、`server.baseUrl=http://…`、`ui.theme=blue`、`dirty.includeUntracked=not-bool`、`remote.prune=not-bool`、`safety.requireTypedConfirm=not-bool`、`ci.token` 超长/含空白）。
- **When**：逐键保存非法值；再逐键保存合法值并观察生效时机；对 v0.2 新增键 `dirty.includeUntracked`/`remote.prune`/`safety.requireTypedConfirm`/`ci.token` 各跑 1 组行为断言。
- **Then**：① 非法值被拒并给字段级提示（`CFG-E001`/`FS-E001` 等），`app_setting` 保持旧值；② 标注“即时”的键（扫描/远程/TTL/删除保留/备份保留/日志/UI 主题/`dirty.includeUntracked`/`remote.prune`/`safety.requireTypedConfirm`）保存后下一轮/立即生效（用 `CLK` 或下一轮扫描断言）；③ 标注“重启”的键（`git.path`/`ci.*`/代理）重启后生效；④ 改变 `workspace.root`/`server.org` 在 v1 不生效（入口只读，见 AC-CFG-10）；⑤ 键名与附录 D 完全一致（无大小写/拼写差异），键数=32。
- **关联 NFR/OQ**：FR-OPS-03、附录 D；M-04/M-05（R-23/R-26 已关闭）

#### AC-OPS-09 · 单实例：双开提示退出且不写数据
- **覆盖 FR**：FR-OPS-04
- **测试层级/依赖**：IT + E2E（进程启动）
- **Given**：实例 A 已运行并持有单实例锁（命名 Mutex + 工作区 `.mbdgit.lock`）。
- **When**：启动实例 B。
- **Then**：① 实例 B 显示“已有实例在运行”提示后退出（进程退出码可断言）；② A 不受影响；③ B 不写 DB、不写工作区（DB `-wal` 大小/行数不变）；④ 双开检测覆盖 Mutex 与工作区锁两种路径（分别构造）；⑤ 日志记录双开事件（脱敏）。
- **关联 NFR/OQ**：FR-OPS-04（旧 AC-15 仅映射见 §1.4）；—

#### AC-OPS-10 · stale lock 自愈（PID/时间/进程存活三条件）
- **覆盖 FR**：FR-OPS-04
- **测试层级/依赖**：IT（`CLK` + 残留锁文件）
- **Given**：构造 3 种 stale lock（SRS v0.2.1 FR-OPS-04 三条件）：① PID 不存在；② PID 存在但进程非本应用；③ 锁文件 mtime 超 10min（`CLK` 推进 >10min）且无存活进程；另 1 种：PID 存活且确为上一实例残留（非 stale）。
- **When**：启动应用。
- **Then**：① 前 3 种均自愈（锁被清除且应用进入主界面 `data-ready=true`），写审计/日志说明；② 第 4 种按“单实例占用”处理，不误删活锁；③ 自愈不破坏 DB（`quick_check`=ok）；④ 强杀（TerminateProcess）后立即重启可自愈（真实场景用例）；⑤ 自愈判定字段（PID/时间/存活）可从日志断言。
- **关联 NFR/OQ**：FR-OPS-04（旧 AC-15 仅映射见 §1.4）；R-33 已关闭（SRS v0.2.1 FR-OPS-04 三条件含 mtime>10min）

#### AC-OPS-11 · 状态栏 6 段数据绑定
- **覆盖 FR**：FR-OPS-05
- **测试层级/依赖**：VIS + IT
- **Given**：应用已运行 ≥1 分钟 + 注入 1 次同步失败 + 1 个进行中任务 + 1 条未读通知。
- **When**：观察状态栏。
- **Then**：① 6 段固定顺序：数据新鲜度 / 进程健康 / 操作队列 / CI 队列（v1 恒 0）/ 通知 / 网络；② 各段数据与后端一致（新鲜度时间、队列数、通知计数、网络状态、CI 计数=0）；③ 进程健康恰 **2 灯**=Host + GitWorker（SRS v0.2.1 FR-OPS-05）；各 `BackgroundService`（RemoteSync/LocalScan/StatusCalc/JOB_RUNNER）以子状态/悬浮明细呈现（`health{name,state,…}`），不得把子状态升为独立灯；④ 点击各段跳转对应页面（通知/任务/CI）；⑤ 布局快照 1280/1440/1920 三档不换行。
- **关联 NFR/OQ**：FR-OPS-05、SRS §12.5 健康快照；R-34 已关闭（SRS v0.2.1 FR-OPS-05：2 灯 + `BackgroundService` 子状态）

#### AC-OPS-12 · DB schema `user_version` + 迁移前 `VACUUM INTO` 备份 + 失败回滚
- **覆盖 FR**：FR-OPS-06
- **测试层级/依赖**：IT（旧版本 DB 夹具）
- **Given**：v1 旧 schema DB（`user_version=1`）与目标版本 DB。
- **When**：① 启动执行迁移；② 注入迁移脚本失败。
- **Then**：① 迁移成功：`PRAGMA user_version`=目标值、表结构符合 §8.1（16 表存在，含 `ci_status`/`notification`/`event_log` 空表）；② 迁移前生成 `VACUUM INTO` 备份文件（断言文件存在且可打开）；③ 注入失败时回滚到旧版本（`user_version` 不变、数据行数不变、应用可用或明确提示）；④ 备份文件路径在 `%LOCALAPPDATA%\MbdGitTool\`；⑤ 启动记录 `sqlite_version()` ≥3.51.3。
- **关联 NFR/OQ**：FR-OPS-06、SRS §8.2；—

#### AC-OPS-13 · DB 损坏：`quick_check` 失败 → 重建（≤5min/100 组件）
- **覆盖 FR**：FR-OPS-06、NFR-REL-04
- **测试层级/依赖**：IT（损坏库夹具 + 计时）
- **Given**：人为破坏的 app.db（页损坏）；100 个本地仓夹具。
- **When**：启动应用触发 `PRAGMA quick_check`。
- **Then**：① 检测失败后重命名旧库（保留，不直接删除）→ 建新库 → 全量重扫；② 重建完成后看板可用（100 组件，`repo` 行数=本地仓数）；③ 重建总耗时 ≤5min（P95，按 §13.3 计时）；④ 旧库文件保留在 `MbdGitTool\` 下可导出；⑤ 全过程 UI 有进度提示，不静默。
- **关联 NFR/OQ**：FR-OPS-06、NFR-REL-04；—

#### AC-OPS-14 · 自动更新（v1.1，版本外）
- **覆盖 FR**：FR-OPS-07（P2）
- **测试层级/依赖**：IT（签名包夹具）
- **Given**：v1 构建。
- **When**：检查更新入口/设置项。
- **Then**：① v1 无自动更新入口（不得出现死按钮），设置页无“自动更新”可选项；② v1.1 验收口径（后续冻结）：更新前等 job 空闲并备份 DB、失败可回滚、包签名校验；③ 本 AC 在 M0 仅冻结 ①。
- **关联 NFR/OQ**：FR-OPS-07、NFR-COMPAT-08；§4.1 OQ-24

#### AC-OPS-15 · 首启三阶段：本地发现→远程对账→状态计算（含跳过远程降级）
- **覆盖 FR**：FR-OPS-08、SRS §3.1/§11.1、D-016
- **测试层级/依赖**：E2E（新机器 + `G-E2E` + `CLK`）
- **Given**：全新数据目录；`workspace_root` 下 400 个本地仓夹具；`G-E2E` 在线。
- **When**：首次启动 → 向导完成 → 观察进度页。
- **Then**：① `progress` 事件序列严格为 `LOCAL_DISCOVERY → REMOTE_RECONCILE → STATUS_CALC`（SRS v0.2.1 §11.1 冻结的阶段枚举，逐字一致），不出现“双阶段”旧口径；② 每阶段 `progress` 事件含 `{stage,total,current,cancellable}` 四字段（SRS v0.2.1 冻结），`current` 单调递增至 total；③ 全部完成后看板可用（400 本地仓、组件计数正确）；④ 中途点“跳过远程”后进入只读降级（`data-degraded` 标记、显示“数据截至 HH:mm”），本地仓**不被**永久误判 INVALID/MISSING；⑤ 远程恢复后自动执行重判（见 AC-OPS-16）；⑥ 首启 400 仓场景 ≤30s 可用看板（NFR-PERF-09）。
- **关联 NFR/OQ**：FR-OPS-08（旧 AC-11 仅映射见 §1.4）、NFR-PERF-09；R-35 已关闭（SRS v0.2.1 §11.1 冻结进度协议）

#### AC-OPS-16 · 首扫进度协议 + 远程就绪自动重判（不永久误判）
- **覆盖 FR**：FR-OPS-08
- **测试层级/依赖**：E2E（`G-WIRE` 延迟响应 + `CLK`）
- **Given**：本地 400 仓已克隆；远程阶段被跳过（`G-WIRE` 不可达）；此时观察组件状态。
- **When**：① 跳过远程后打开看板；② 恢复 `G-WIRE` 并触发重连/自动重试。
- **Then**：① 跳过阶段后看板显示只读降级 + “远程未就绪”，本地仓状态按**本地事实**判定（INVALID/MISSING 不被误判：本地存在的仓不得显示 MISSING）；② 未绑定/待对账仓显示 `UNKNOWN` 且 `component.incomplete=1`（SRS v0.2.1 口径，不再使用未定义的中间态命名），不得显示 `INVALID`；③ 远程就绪后自动触发重判：受影响组件 `aggregate_status` 由 UNKNOWN 更新为真实态（断言至少 1 个组件由 UNKNOWN→SYNCED/BEHIND），`incomplete` 清零；④ 重判不需用户操作；⑤ 重判后“远程未就绪”标记消失；⑥ 进度事件含 `{stage,total,current,cancellable}` 四字段且取消可用。
- **关联 NFR/OQ**：FR-OPS-08、D-016（旧 AC-21 仅映射见 §1.4）；R-36 已关闭（SRS v0.2.1：UNKNOWN+incomplete=1）

#### AC-OPS-17 · 错误码表核对：稳定、可读文案、UI 展示错误码+文案
- **覆盖 FR**：FR-OPS-09
- **测试层级/依赖**：UT + IT
- **Given**：附录 D 示例错误码 16 个 + M0 补全后的全表（前缀 `CFG/SYNC/SCAN/STATE/OP/FS/NET/GIT/SEC`）。
- **When**：① 逐码触发或单测注入；② 修改某码的用户文案。
- **Then**：① 每个错误码均有：码值、用户可读文案、可重试性、日志级别（M0 需冻结四列，见 §4.2 R-06）；② UI 展示格式固定为“错误码 + 文案”（`data-error-code` + 文案节点）；③ 文案变化后码值不变（回归断言）；④ 未知错误兜底码存在且前缀合法；⑤ 任务中心/日志/诊断包三处引用同一码表。
- **关联 NFR/OQ**：FR-OPS-09、附录 D；〔SRS 需修订 R-06〕

### 2.11 CI 预留（FR-CI，v1 只占位）

#### AC-CI-01 · 10 锚点存在性（UI 快照）
- **覆盖 FR**：FR-CI-01、SRS §11.4、04 §5.4-1
- **测试层级/依赖**：VIS + IT
- **Given**：v1 构建（`ci.enabled=false`）。
- **When**：遍历应用并断言 10 个锚点。
- **Then**：① CI-1 左侧导航“CI 流水线”入口存在（路由可跳转、页面骨架 + 空态 + 实验标记）；② CI-2 组件行/卡片 CI 徽标槽位存在且定高 16–20px；③ CI-3 组件详情 CI 汇总条 40px 存在；④ CI-4 单仓详情第 4 个 Tab 存在；⑤ CI-5 状态栏 CI 队列段存在且计数=0；⑥ CI-6 通知中心 CI 类目存在（见 AC-OPS-02）；⑦ CI-7 看板筛选 CI facet 存在且禁用；⑧ CI-8 设置“CI 集成”分区存在且 disabled；⑨ CI-9 数据契约（`ci_status` 表/`ci_latest` 视图/`CI_UPDATED`/`ICiProvider`）存在；⑩ CI-10 聚合分离（见 AC-CI-04）。10/10 缺一即失败。
- **关联 NFR/OQ**：FR-CI-01、04 §5.4-1；—

#### AC-CI-02 · CI 槽位定高：1280/1440/1920 三档不换行、不遮挡、卡片高度变化 0px
- **覆盖 FR**：FR-CI-01、04 §5.4-2
- **测试层级/依赖**：VIS（三档分辨率）
- **Given**：同一组件数据在 1280×800、1440×900、1920×1080 三档。
- **When**：渲染并测量 CI 槽位与行/卡片几何。
- **Then**：① 槽位高度 ∈[16,20]px 且三档一致；② 槽位不换行、不与其他信息位遮挡（元素边界不重叠）；③ 卡片/行高度在 CI 数据有/无时变化=0px；④ 每行/卡片恰有 1 个 CI 槽位；⑤ 三档快照通过。
- **关联 NFR/OQ**：CI-2、04 §5.4-2；—

#### AC-CI-03 · 注入 CI FAIL 后卡片底色不变（中性）
- **覆盖 FR**：FR-CI-03
- **测试层级/依赖**：VIS（`ci.enabled=true` + Mock FAIL 注入）
- **Given**：同一组件分别注入 `CiState=PASS`、`FAIL`、`NONE`、`UNKNOWN`。
- **When**：截图对比卡片底色与聚合徽标。
- **Then**：① 4 种注入下卡片底色（计算样式 background-color）=中性 token 且完全相同；② 聚合徽标/左轨颜色完全不变（不因 CI 变化）；③ CI 徽标本身按通道 B 变化（FAIL 红点/`data-ci-state="FAIL"`）；④ 与 SRS §11.2 通道 B 颜色一致。
- **关联 NFR/OQ**：FR-CI-03、CI-10（旧 AC-12 仅映射见 §1.4）；—

#### AC-CI-04 · CI 不参与组件聚合：注入 FAIL 后 `aggregate_status` 不变
- **覆盖 FR**：FR-CI-03、SRS §7.4/D-002
- **测试层级/依赖**：UT + IT
- **Given**：组件四仓均 `SYNCED` → 聚合 `SYNCED`；注入四仓 CI 均 `FAIL`。
- **When**：重新计算聚合。
- **Then**：① `component.aggregate_status` 仍为 `SYNCED`；② 排序位置不变（严重度不变）；③ CI 聚合字段（`byState`/`worstState`）独立存在且 `worstState=FAIL`；④ 单测断言聚合纯函数输入不含 CI 字段（契约隔离）；⑤ 任何 CI 状态组合下聚合结果与“仅仓库状态”计算完全一致。
- **关联 NFR/OQ**：FR-CI-03、D-002、CI-10；—

#### AC-CI-05 · 数据契约：`CiLatest`/`ComponentCiAggregate`/`ICiProvider` 字段与枚举冻结（SRS v0.2 契约）
- **覆盖 FR**：FR-CI-02
- **测试层级/依赖**：UT（契约 schema 校验）
- **Given**：SRS §11.4 冻结契约（SRS v0.2 修订后原文，字段名照抄；契约↔DDL 映射以 SRS §8.3 为唯一判据，AC-CI-09 按此断言）。
- **When**：对 Mock 实现产物做 JSON Schema 校验。
- **Then**：① `CiState` 枚举恰为 `PASS|FAIL|RUNNING|QUEUED|NONE|UNKNOWN` 6 值；② `CiLatest` 含全部字段：`repoId: number`（Gitea 数字 id）、`componentId: string`、`repoType`（`DESIGN|IMPL|MIL|SIL`）、`branch`、`commitHash`（必须绑定 commit）、`state`、可选 `pipelineId`/`buildNumber`/`failedStage`/`startedAt`/`finishedAt`/`durationMs`/`url`、`provider`、`fetchedAt`；**字段名必须为 `componentId` 与数字 `repoId`（不得使用 D-009 禁用别名，也不得用字符串型 repo id）**；③ `ComponentCiAggregate` 含 `componentId`、`byState`（6 键全）、`worstState`、`lastFailed`、`dataFreshAt`；④ `worstState` 优先级为 `FAIL > RUNNING > QUEUED > UNKNOWN > PASS > NONE`（单测排序断言）；⑤ `ICiProvider` 三方法签名存在（`getLatest(componentId)`/`getAggregate(componentId)`/`subscribe`），v1 `subscribe` 为空实现。
- **关联 NFR/OQ**：FR-CI-02、SRS §11.4；R-16 已关闭（SRS v0.2 §11.4 冻结 `componentId`/数字 `repoId`；`ci_status` 列见 AC-CI-09）

#### AC-CI-06 · `CI_UPDATED` 事件：类型注册 + 处理分支 + Mock 触发
- **覆盖 FR**：FR-CI-02
- **测试层级/依赖**：IT（事件注入）
- **Given**：`ci.enabled=true`，Mock provider 可手动发事件。
- **When**：通过 Mock 触发 `CI_UPDATED`（payload 为 1 条 `CiLatest`）。
- **Then**：① IPC 契约中存在 `CI_UPDATED` 类型定义与处理分支（代码/契约断言）；② 收到事件后对应槽位/徽标更新为 payload 的 `state`（DOM 断言）；③ 未知字段/未知 `state` 时使用 `UNKNOWN` 兜底且不崩溃；④ v1 `ci.enabled=false` 时事件不发送（subscribe 为空实现，`getLatest` 返回空数组）；⑤ 事件节流/去重不丢失最终态（连发 3 条取最后一条）。
- **关联 NFR/OQ**：FR-CI-02、04 §5.4-7；—

#### AC-CI-07 · `ci.enabled` 默认 false；开 true 后仅换数据不改结构
- **覆盖 FR**：FR-CI-02、04 §5.4-6
- **测试层级/依赖**：IT + VIS
- **Given**：全新安装。
- **When**：① 读取 `ci.enabled`；② 改为 true 并重启，注入 Mock 数据；③ 对比开/关两态 DOM 结构。
- **Then**：① 默认值=`false`（`app_setting` 断言）；② 开 true 后 CI-2/3/4/5/7 显示 Mock 数据（非空态）；③ 组件结构不变：CI 槽位/汇总条/Tab/状态栏/筛选的 DOM 路径、尺寸、位置与关闭态一致（仅内容变化）；④ 无需改组件代码/重建前端（配置驱动）；⑤ 关闭后恢复空态且无残留 Mock 数据。
- **关联 NFR/OQ**：FR-CI-02、附录 D `ci.enabled`；—

#### AC-CI-08 · 无 CI 时统一 `CiSlot` 空态，禁止裸 TODO/白块
- **覆盖 FR**：FR-CI-04
- **测试层级/依赖**：VIS + 文案扫描
- **Given**：`ci.enabled=false` 全站页面。
- **When**：扫描全站 DOM 文本与截图。
- **Then**：① 全站无裸文本 `TODO`/`敬请期待`/`coming soon`（正则扫描命中=0）；② 所有 CI 位置使用统一 `CiSlot` 空态组件（`data-testid="ci-slot"` + `data-state="NONE"`），文案统一“CI 未接入”/“CI 集成将在后续版本开放”；③ 空态有固定尺寸、骨架不塌陷、tooltip 完整、`aria-label` 存在；④ 5 处以上位置（行/详情/Tab/状态栏/通知）空态一致；⑤ 视觉快照通过。
- **关联 NFR/OQ**：FR-CI-04、04 §5.4-9；—

#### AC-CI-09 · `ci_status` 表 + `ci_latest` 视图 + `ci.*` 配置键在 v1 建好（空数据，SRS v0.2 契约）
- **覆盖 FR**：FR-CI-05、SRS §8.1/§11.4
- **测试层级/依赖**：IT（DDL 检查 + DB 查询）
- **Given**：v1 全新 DB。
- **When**：检查 schema 与数据。
- **Then**：① `ci_status` 表存在，主键 `(repo_id, commit_sha)`，含 SRS v0.2 §8.1 全列：`provider/state/run_id/run_url/branch/build_number/failed_stage/started_at/finished_at/duration_ms/fetched_at/updated_at`（含 v0.2 补的 `build_number/started_at/finished_at`），v1 行数=0；② `ci_latest` 视图存在且可查询（每 `repo_id` 取最新 `commit_sha` 对应行），结果为空集不报错；视图列与契约 `CiLatest` 按 SRS §8.3 映射表断言（`pipelineId↔run_id`、`url↔run_url`、`commitHash↔commit_sha`、`componentId` 经 `repo.component_id` 推导、**`repoType` 由 `ci_status.repo_id → repo.repo_type` 推导（DESIGN/IMPL/MIL/SIL，不冗余存储）**、`byState/worstState` 由视图分组聚合不落表，M-12/N-07）；③ `app_setting` 含 `ci.enabled=false`、`ci.baseUrl=''`、`ci.token`（只存凭据引用，空）、`ci.pollIntervalSec=60` 四键；④ `event_log` 空表存在；⑤ 迁移脚本包含上述对象（M0 冻结）。
- **关联 NFR/OQ**：FR-CI-05、SRS §8.1/§11.4；R-16/R-19 已关闭（旧 AC-12 仅映射见 §1.4）

#### AC-CI-10 · CI 六态视觉（通道 B）与 `worstState` 聚合展示
- **覆盖 FR**：FR-CI-02、SRS §11.2 通道 B
- **测试层级/依赖**：VIS（6 态 × 关键位置快照）
- **Given**：`ci.enabled=true`，分别注入 6 种 `CiState`；另一组注入四仓混合状态。
- **When**：渲染看板/组件详情/单仓 Tab/状态栏/通知。
- **Then**：① 六态视觉与 SRS 通道 B 一致：`PASS`=success 圆点✓、`FAIL`=danger 圆点✕、`RUNNING`=info 半环◐、`QUEUED`=neutral 空心○、`NONE`=neutral ∅、`UNKNOWN`=hatched ?；② 形状/位置与通道 A 明确不同（圆点/pill，不染卡片底色）；③ 四仓混合时 `worstState` 按 `FAIL>RUNNING>QUEUED>UNKNOWN>PASS>NONE` 展示且与契约一致；④ 视觉基线包含 6 态 × ≥2 位置 ≥12 张；⑤ 状态色只来自 `--ci-*` token（无硬编码 hex）。
- **关联 NFR/OQ**：FR-CI-02、SRS §11.2；—

#### AC-CI-11 · CI 数据新鲜度 `fetchedAt` 展示锚点
- **覆盖 FR**：FR-CI-02、SRS §11.4 契约（“UI 必须展示新鲜度”）
- **测试层级/依赖**：VIS + IT
- **Given**：Mock 数据 `fetchedAt=T0`，当前 `CLK=T0+45min`。
- **When**：查看组件行/组件详情汇总条/单仓 CI Tab。
- **Then**：① 3 处均显示 CI 数据时间（“CI 数据 X 分钟前”或 tooltip，`data-testid="ci-freshness"`）；② 超过 §7.1 CI TTL 30min（v2 值，v1 占位）时显示过期态而非伪造当前；③ 无 CI 数据时显示“无 CI”而非“0 秒前”；④ 展示格式与本地/远程新鲜度统一；⑤ 快照通过。
- **关联 NFR/OQ**：FR-CI-02、SRS §11.4、§7.1；—

### 2.12 v1.1 AC（不冻结，不计入 162 条冻结基线）

> 口径（SRS §15 OQ-24 / D-008）：P2 功能（FR-CFG-06 配置热更新、FR-DETAIL-05 右键菜单）在 v1 只冻结“入口不存在/不可用”（AC-CFG-10、AC-DETAIL-09 ①）。以下完整行为为 v1.1 草案，**M0/M1/M2 不验收、不冻结**；编号使用域内**下一个未占用序号**并以 `(v1.1)` 后缀标识（当前 `AC-CFG-12(v1.1)`、`AC-DETAIL-10(v1.1)`），**不占用 162 条冻结编号、不计入统计**（N-09）；v1.1 立项时按 §5.4 变更规则转为正式编号。

#### AC-CFG-12(v1.1) · 配置热更新完整行为（不冻结）
- **覆盖 FR**：FR-CFG-06（P2）
- **测试层级/依赖**：IT（`G-WIRE`、本地 FS 夹具）
- **Given**：已加载组织 `MBD`（1,000 组件）与根 `D:\MBD\Workspace`（100 个本地仓）。
- **When**：设置页把 `server.org` 改为 `MBD2`→确认对话框点“确定并重同步”；再把 `workspace.root` 改为空目录 `D:\MBD\Empty`→确认“重扫”。
- **Then**：① 确认对话框列出影响：组件数 1,000→（MBD2 实际数）、本地仓 100→0；② 确认后 `component` 行数=新组织组件数、`repo` 中 `is_local=1` 行数=0；③ 未确认点“取消”时配置不变、DB 行数不变；④ 变更写 `operation_log.action` 并更新 `app_setting`。
- **关联 NFR/OQ**：OQ-05、OQ-24；v1.1 冻结

#### AC-DETAIL-10(v1.1) · 右键菜单四动作完整行为（不冻结）
- **覆盖 FR**：FR-DETAIL-05（P2）
- **测试层级/依赖**：IT + VIS
- **Given**：文件树节点、提交行、组件行。
- **When**：右键打开上下文菜单。
- **Then**：① 菜单含 4 项：在资源管理器打开、复制路径、复制 commit、用其他程序打开；② 复制动作写入剪贴板并断言内容（路径/完整 SHA）；③ “在资源管理器打开”启动 `explorer.exe` 且路径限定 root 内（越界拒绝 `FS-E001`）；④ 键盘上下文菜单键（Shift+F10）等价可用。
- **关联 NFR/OQ**：OQ-24；v1.1 冻结

---

## 3. FR→AC 追溯矩阵

> 统计：**72 条 FR（9.1–9.11 全量）/ 162 条 AC**；每条 FR ≥1 条 AC，P0 FR 均含成功 + 失败/异常路径。里程碑按 SRS §3.3：M0=骨架与冻结（含 CI 锚点/契约/状态矩阵/夹具/AC 冻结）、M1=只读闭环、M2=写操作、M3=收口；`v1.1` 表示版本外（v1 只冻结“不暴露入口”断言）。

| FR ID | P | AC IDs | 成功路径 | 失败/异常路径 | 测试层级 | 里程碑 |
|---|---|---|---|---|---|---|
| FR-CFG-01 | P0 | AC-CFG-01, AC-CFG-02, AC-CFG-03, AC-CFG-11 | ✓ | ✓ | IT+VIS | M1 |
| FR-CFG-02 | P0 | AC-CFG-04, AC-CFG-05, AC-CFG-11 | ✓ | ✓ | IT（Mock/真 Gitea） | M1 |
| FR-CFG-03 | P0 | AC-CFG-06, AC-CFG-07 | ✓ | ✓ | IT+安全扫描 | M1 |
| FR-CFG-04 | P1 | AC-CFG-05, AC-CFG-08 | ✓ | ✓ | IT+E2E | M1 |
| FR-CFG-05 | P1 | AC-CFG-09 | ✓ | ✓ | IT | M1 |
| FR-CFG-06 | P2 | AC-CFG-02, AC-CFG-10 | ✓ | ✓ | IT | v1.1 |
| FR-SYNC-01 | P0 | AC-SYNC-01, AC-SYNC-02, AC-SYNC-03, AC-SYNC-14 | ✓ | ✓ | IT+E2E | M1 |
| FR-SYNC-02 | P0 | AC-SYNC-04, AC-SYNC-05 | ✓ | ✓（AC-SYNC-04 ⑤ refs 部分失败） | IT | M1 |
| FR-SYNC-03 | P0 | AC-SYNC-01, AC-SYNC-06, AC-SYNC-07 | ✓ | ✓ | IT | M1 |
| FR-SYNC-04 | P0 | AC-SYNC-01, AC-SYNC-03, AC-SYNC-08 | ✓ | ✓ | IT | M1 |
| FR-SYNC-05 | P1 | AC-SYNC-09, AC-SYNC-10, AC-SYNC-13 | ✓ | ✓ | IT+E2E | M1 |
| FR-SYNC-06 | P1 | AC-SYNC-11, AC-SYNC-12 | ✓ | ✓ | IT | M1 |
| FR-SCAN-01 | P0 | AC-SCAN-01, AC-SCAN-02, AC-SCAN-03 | ✓ | ✓ | IT | M1 |
| FR-SCAN-02 | P0 | AC-SCAN-04 | ✓ | ✓（AC-SCAN-04 ⑦ 单仓扫描失败） | IT（表驱动） | M1 |
| FR-SCAN-03 | P0 | AC-SCAN-05, AC-SCAN-06 | ✓ | ✓ | IT | M1 |
| FR-SCAN-04 | P0 | AC-SCAN-07, AC-SCAN-08, AC-SCAN-09, AC-SCAN-10 | ✓ | ✓（AC-SCAN-07 ⑥ 深扫失败隔离） | IT+PERF | M1 |
| FR-SCAN-05 | P0 | AC-SCAN-11 | ✓ | ✓ | IT | M1 |
| FR-SCAN-06 | P1 | AC-SCAN-03, AC-SCAN-12, AC-SCAN-13 | ✓ | ✓ | IT | M1 |
| FR-STA-01 | P0 | AC-STA-01, AC-STA-02, AC-STA-03, AC-STA-12, AC-STA-13, AC-STA-15 | ✓ | ✓ | UT+IT | M1 |
| FR-STA-02 | P0 | AC-STA-01, AC-STA-04, AC-STA-09, AC-STA-14 | ✓ | ✓ | UT+IT | M1 |
| FR-STA-03 | P0 | AC-STA-05, AC-STA-06 | ✓ | ✓ | UT+IT | M1 |
| FR-STA-04 | P0 | AC-STA-07, AC-STA-08, AC-STA-09 | ✓ | ✓（AC-STA-07 ④ fetch 失败 error_code） | IT | M1 |
| FR-STA-05 | P1 | AC-SCAN-09, AC-STA-10 | ✓ | — | IT+PERF | M1 |
| FR-STA-06 | P1 | AC-STA-11, AC-DETAIL-02, AC-OP-11, AC-OP-12 | ✓ | ✓ | UT+IT | M1 |
| FR-BOARD-01 | P0 | AC-BOARD-01, AC-BOARD-02, AC-BOARD-03, AC-BOARD-14, AC-BOARD-15, AC-BOARD-16 | ✓ | ✓*（七态异常由 AC-BOARD-08 覆盖） | IT+VIS+PERF | M1 |
| FR-BOARD-02 | P0 | AC-BOARD-03, AC-BOARD-04, AC-BOARD-05, AC-BOARD-06, AC-BOARD-07 | ✓ | ✓ | IT+PERF | M1 |
| FR-BOARD-03 | P0 | AC-BOARD-08 | ✓ | ✓ | IT+VIS | M1 |
| FR-BOARD-04 | P1 | AC-BOARD-09, AC-BOARD-10, AC-BOARD-11 | ✓ | ✓ | IT+VIS | M2 |
| FR-BOARD-05 | P1 | AC-STA-08, AC-BOARD-05, AC-BOARD-12, AC-BOARD-14 | ✓ | — | VIS+PERF | M1 |
| FR-BOARD-06 | P1 | AC-BOARD-13 | ✓ | ✓ | IT+VIS | M1 |
| FR-DETAIL-01 | P0 | AC-DETAIL-01, AC-DETAIL-02 | ✓ | ✓（AC-DETAIL-01 ⑤ 数据源失败） | IT+VIS | M1 |
| FR-DETAIL-02 | P0 | AC-DETAIL-03, AC-DETAIL-04, AC-DETAIL-05 | ✓ | ✓（AC-DETAIL-03 ⑤ 非法深链参数） | IT+VIS+PERF | M1 |
| FR-DETAIL-03 | P1 | AC-DETAIL-06, AC-DETAIL-07, AC-SAFE-10 | ✓ | ✓ | IT+安全 | M1 |
| FR-DETAIL-04 | P1 | AC-DETAIL-08 | ✓ | — | VIS+IT | M1 |
| FR-DETAIL-05 | P2 | AC-DETAIL-09 | ✓ | — | IT+VIS | v1.1 |
| FR-OP-01 | P0 | AC-SYNC-14, AC-OP-01, AC-OP-02, AC-OP-03, AC-OP-04, AC-OP-05 | ✓ | ✓ | E2E+IT | M2 |
| FR-OP-02 | P0 | AC-OP-06, AC-OP-07, AC-OP-08 | ✓ | ✓ | E2E+IT | M2 |
| FR-OP-03 | P0 | AC-OP-09, AC-OP-10 | ✓ | ✓ | E2E+IT | M2 |
| FR-OP-04 | P0 | AC-SYNC-05, AC-OP-11, AC-OP-12, AC-OP-13, AC-OP-14 | ✓ | ✓ | E2E+UT | M2 |
| FR-OP-05 | P0 | AC-OP-15, AC-OP-16, AC-OP-17, AC-OP-18, AC-OP-19 | ✓ | ✓ | E2E+IT | M2 |
| FR-OP-06 | P0 | AC-OP-20, AC-OP-21, AC-OP-22, AC-OP-23, AC-OP-30 | ✓ | ✓ | E2E+IT | M2 |
| FR-OP-07 | P0 | AC-OP-24, AC-OP-25 | ✓ | ✓ | E2E+IT | M2 |
| FR-OP-08 | P0 | AC-OP-03, AC-OP-26, AC-OP-27 | ✓ | ✓ | E2E | M2 |
| FR-OP-09 | P1 | AC-OP-28, AC-OPS-04 | ✓ | — | IT | M2 |
| FR-OP-10 | P1 | AC-SCAN-06, AC-OP-29 | ✓ | ✓ | IT+E2E | M2 |
| FR-OP-11 | P0 | AC-OP-30 | ✓ | ✓ | E2E | M2 |
| FR-OP-12 | P1 | AC-OP-31, AC-OP-32 | ✓ | ✓ | E2E | M2 |
| FR-REPO-01 | P0 | AC-REPO-01, AC-REPO-02, AC-REPO-03 | ✓ | ✓ | IT | M2 |
| FR-REPO-02 | P1 | AC-REPO-04, AC-REPO-05 | ✓ | ✓ | E2E | M2 |
| FR-REPO-03 | P1 | AC-REPO-06 | ✓ | ✓ | IT | M2 |
| FR-REPO-04 | P2 | AC-REPO-07 | ✓ | — | VIS+IT | v1.1 |
| FR-REPO-05 | P0 | AC-OP-05, AC-REPO-08, AC-REPO-09, AC-REPO-10, AC-REPO-11 | ✓ | ✓ | E2E+IT | M2 |
| FR-SAFE-01 | P0 | AC-OP-16, AC-SAFE-01, AC-SAFE-02, AC-SAFE-03 | ✓ | ✓ | VIS+IT | M2 |
| FR-SAFE-02 | P0 | AC-OP-20, AC-SAFE-04 | ✓ | ✓ | IT | M2 |
| FR-SAFE-03 | P0 | AC-CFG-06, AC-SAFE-05, AC-SAFE-06, AC-SAFE-07, AC-OPS-06 | ✓ | ✓ | UT+IT+安全 | M3 |
| FR-SAFE-04 | P0 | AC-OP-26, AC-SAFE-08, AC-SAFE-09 | ✓ | ✓ | E2E | M2 |
| FR-SAFE-05 | P1 | AC-DETAIL-06, AC-DETAIL-07, AC-SAFE-10, AC-SAFE-11 | ✓ | ✓ | IT+安全 | M1 |
| FR-SAFE-06 | P0 | AC-CFG-06, AC-SAFE-12, AC-SAFE-13 | ✓ | ✓ | IT+安全 | M1 |
| FR-OPS-01 | P0 | AC-OPS-01, AC-OPS-02 | ✓ | ✓（AC-OPS-01 ⑥ 写库失败降级） | IT+VIS | M3 |
| FR-OPS-02 | P0 | AC-SAFE-06, AC-OPS-03, AC-OPS-04, AC-OPS-05, AC-OPS-06 | ✓ | ✓ | IT | M3 |
| FR-OPS-03 | P0 | AC-OPS-07, AC-OPS-08 | ✓ | ✓ | IT+VIS | M3 |
| FR-OPS-04 | P0 | AC-OPS-09, AC-OPS-10 | ✓ | ✓ | IT+E2E | M1 |
| FR-OPS-05 | P1 | AC-OPS-11 | ✓ | — | VIS+IT | M3 |
| FR-OPS-06 | P1 | AC-OPS-12, AC-OPS-13 | ✓ | ✓ | IT | M1（迁移）/M3（重建） |
| FR-OPS-07 | P2 | AC-OPS-14 | ✓ | — | IT | v1.1 |
| FR-OPS-08 | P0 | AC-OPS-15, AC-OPS-16 | ✓ | ✓ | E2E | M1 |
| FR-OPS-09 | P1 | AC-OPS-17 | ✓ | ✓ | UT+IT | M0（码表冻结）/M1 |
| FR-CI-01 | P0 | AC-DETAIL-03, AC-OPS-02, AC-CI-01, AC-CI-02 | ✓ | ✓*（锚点缺失即失败；空态由 AC-CI-08 覆盖） | VIS+IT | M0 |
| FR-CI-02 | P0 | AC-CI-05, AC-CI-06, AC-CI-07, AC-CI-10, AC-CI-11 | ✓ | ✓ | UT+IT+VIS | M0 |
| FR-CI-03 | P0 | AC-CI-03, AC-CI-04 | ✓ | ✓*（AC-CI-03 注入 CI FAIL 本身即异常注入） | VIS+UT | M0 |
| FR-CI-04 | P1 | AC-CI-08 | ✓ | — | VIS | M0 |
| FR-CI-05 | P0 | AC-CI-09 | ✓ | ✓*（AC-CI-09 空集查询不报错） | IT（DDL） | M0 |

**统计**：FR 总数 = 72（P0 = 46 / P1 = 22 / P2 = 4，以 SRS §9 优先级列为准；FR-OPS-06 为 P1）；AC 总数 = 162（均为独立主编号；§2.12 v1.1 草案 AC 不计入）；按域：CFG 11 / SYNC 14 / SCAN 13 / STA 15 / BOARD 16 / DETAIL 9 / OP 32 / REPO 11 / SAFE 13 / OPS 17 / CI 11。**P0 FR 的成功 + 失败/异常双路径**：本表“失败/异常路径”列为 ✓ 者均在该 FR 的主 AC 内直接断言异常分支；标 `✓*` 者为“异常面由同一 P0 域内相邻 AC 覆盖”（如展示类 FR-BOARD-01 的七态异常由 AC-BOARD-08 覆盖、FR-SYNC-02 的 refs 失败由 AC-SYNC-04/12 覆盖、契约类 FR-CI-05 的空集查询不报错由 AC-CI-09 覆盖），冻结评审需逐条确认 `✓*` 的覆盖关系成立。

---

## 4. 未覆盖项与产品决策依赖

### 4.1 产品决策依赖（OQ）与 AC 暂定口径

> 以下 OQ 不阻塞 M0 冻结（SRS §15 判定“是否阻塞=否”者），但 AC 必须先有可执行口径；本列为本目录采用的暂定口径，M0 拍板后同步替换 AC 中的对应断言。

| OQ | 问题 | 本目录 AC 暂定口径 | 影响的 AC |
|---|---|---|---|
| OQ-05 | 容量口径 1,000 组件/4,000 仓 vs 4× 压测 | 分两档：正常档 1,000 组件/4,000 仓；压测档 4,000 组件/16,000 仓，性能预算不豁免 | AC-BOARD-03, AC-BOARD-14, AC-SYNC-01 |
| OQ-06 | 未来 CI 系统 | 只冻结 `provider='mock'`，不冻结真实 provider 名 | AC-CI-05/06/07 |
| OQ-07 | 是否允许后台自动 fetch | 允许、默认开；fetch 周期取附录 D `remote.refreshMin`（SRS v0.2 已定义，R-26 关闭），可关 | AC-SCAN-11, AC-OP-15 |
| OQ-11/16 | 常驻形态/托盘 | 验收只断言“进程存活 + 单实例 + 通知可达”，不冻结托盘/服务形态；形态变更不影响 AC | AC-OPS-09/10 |
| OQ-12 | 远程 SLO（10min/30min/每日） | 按 7.5 分档验收（≤10min 可见、TTL 20min、每日全量 P95≤5min） | AC-SYNC-06, AC-SCAN-11 |
| OQ-13 | 打包 MSIX vs MSI | v1 以 MSIX 为主，签名校验归 M3；AC 不绑定安装器形态 | —（NFR-COMPAT-08 不在本目录 AC 范围） |
| OQ-15 | Gitea 配额/限流 | 验收用 WireMock 注入 429；现场真实配额不计入 AC | AC-SYNC-12 |
| OQ-17 | 多根目录 | v1 恰好 1 个根；多根入口不存在（AC-CFG-02 ③） | AC-CFG-02 |
| OQ-18 | RDP/共享工作区写操作 | 暂定“禁止写、只读展示并提示”；写入返回 `OP-E004`/`SCAN-E002` | AC-SCAN-12, AC-STA-15 |
| OQ-19 | Win10 22H2 ESU | 验收环境固定 Windows 11 x64；Win10 不计入 AC | 全部 E2E/PERF AC |
| OQ-20 | 缺仓是否长期常态 | 按 D-015：缺仓=`BINDING_ERROR` 中性、不参与异常优先最高档 | AC-SYNC-14, AC-BOARD-01 |
| OQ-21 | §7.4 严重度列全序 | **已关闭（D-020 + SRS v0.2 §7.4）**：`severity_rank` 0–14 与聚合优先级分离，看板按 `severity_rank` 升序 + 组件 ID 排序；`BINDING_ERROR` 再按 `binding_cause` 分 1（INVALID/AMBIGUOUS，含未绑定）/12（INCOMPLETE，仅缺仓） | AC-BOARD-01/07, AC-SYNC-14, AC-STA-04 |
| OQ-22 | `untracked` 是否升 `DIRTY` 主状态 | **已关闭（D-021 + 附录 D `dirty.includeUntracked=false`）**：仅 tracked 改动升 DIRTY；改 true 时 untracked 也升 DIRTY（AC-STA-13 参数化断言） | AC-STA-13 |
| **OQ-23**（本目录新提） | 错误码全表（附录 D 仅示例 16 个）与四列（码/文案/可重试性/日志级别） | M0 冻结全表；AC 只断言“码值稳定 + 前缀合法 + UI 同码”，具体新码 M0 补 | AC-OPS-17, AC-OP-04, AC-OP-23, AC-SAFE-13 |
| OQ-24 | P2 FR 的版本归属（FR-CFG-06/DETAIL-05/REPO-04/OPS-07） | v1 只冻结“入口不存在/不可用”（AC-CFG-10/AC-DETAIL-09 已按此重写）；完整行为单列 §2.12 v1.1 AC（不冻结） | AC-CFG-10, AC-DETAIL-09, AC-REPO-07, AC-OPS-14 |

### 4.2 SRS 需修订清单（本目录发现，逐条给出 AC 暂定口径）

> 规则：本目录**不自行发明枚举/命名**；凡 SRS 缺失/矛盾而 AC 必须依赖的，标注 R-xx 并在下表说明暂定口径与建议修订位置。**状态列**按 SRS v0.2.1 逐条核对：已关闭项标 SRS 位置，仍开放项列为 M0 前必修。R-01…R-38 中 **37 条已关闭、仅 R-06 仍开放**（M0 出口条件：附录 D 错误码全表冻结）。夹具总数：**25**（附录 C 25 行，含 v0.2.1 新增 `F-MISSING+BLOCKED` 组合夹具；`F-PARTIAL` 主状态为 null，只断言聚合）。M0 冻结前需由 SRS 修订并回写本目录。

| ID | 位置 | 问题 | AC 暂定口径 / 修订建议 | 状态（SRS v0.2.1） |
|---|---|---|---|---|
| R-01 | §7.2 vs §7.6 | UNKNOWN/INVALID 行 §7.2 的允许写操作与 §7.6 不一致 | 以 §7.6 矩阵为准 | **已关闭**（SRS v0.2.1 §7.2 UNKNOWN/INVALID 行已改为“见 7.6”） |
| R-02 | §7.4 严重度列 | 原严重度非全序，默认排序不可判定 | `severity_rank` 0–14（D-020）；`BINDING_ERROR` 按 `binding_cause` 分 1/12 | **已关闭**（SRS v0.2.1 §7.4 + D-020：`severity_rank` 0–14；`binding_cause` INCOMPLETE=12 / 其余=1） |
| R-03 | 附录 C `F-TAG_DRIFT` | 第 4 仓主状态未定死 | AC-OP-12/AC-STA-01 按附录 C 断言第 4 仓 `SYNCED` | **已关闭**（SRS v0.2.1 附录 C `F-TAG_DRIFT`：第 4 仓固定 `SYNCED`） |
| R-04 | 附录 C `F-LFS-MISSING` | 原写 `DIRTY?`，非确定值 | 主状态 `SYNCED` + `lfs_missing` flag，聚合 SYNCED | **已关闭**（SRS v0.2.1 附录 C `F-LFS-MISSING` 行） |
| R-05 | FR-REPO-02 vs §7.6 | EMPTY 首推与“需 upstream”冲突 | AC-REPO-11 断言不受拦截 | **已关闭**（SRS v0.2.1 FR-REPO-02 已加 EMPTY 例外） |
| R-06 | 附录 D 错误码表 | 仅 16 示例、无“可重试性/日志级别”列 | AC 只断言码前缀/码值稳定/UI 同码；M0 冻结全表 | **仍开放（M0 出口条件）**：附录 D 错误码全表（含可重试性/日志级别/用户文案）于 M0 冻结时补齐并回写 AC-OP-04/23、AC-OPS-17 |
| R-07 | 附录 C `F-NO_UPSTREAM-GONE` | flag 写 `remote_gone?` | 不置 remote_gone，仅主状态 NO_UPSTREAM | **已关闭**（SRS v0.2.1 附录 C `F-NO_UPSTREAM-GONE` 行） |
| R-08 | §8.1 字段缺失 | `repo.renamed_from`、`baseline_snapshot.tag_object_sha`、停止管理字段 | AC-SYNC-09/AC-OP-14/AC-OP-32 已按新字段断言 | **已关闭**（SRS v0.2.1 §8.1 `component.is_managed`/`repo.renamed_from`/`baseline_snapshot.tag_object_sha`） |
| R-09 | §10.3 vs §12.6 | 凭据口径不一 | CM 为对外方案、DPAPI 为实现细节 | **已关闭**（SRS v0.2.1 §10.3/§12.6 已统一） |
| R-10 | §6.3/§7.3/NFR | 网络盘/reparse/submodule 多口径并存 | AC-SCAN-12/13 按“不纳管+`scan_issue` 告警；已纳管后不可写→只读展示；submodule 标记+写拒绝”断言 | **已关闭**（SRS v0.2.1 §6.3 已统一口径） |
| R-11 | §13.1 AC-14 | 既有仓内嵌 token 不可能清零 | 告警+脱敏+不自动改 | **已关闭**（SRS v0.2.1 §13.1 AC-14） |
| R-12 | §13.1 AC-13 vs §13.3 | 容量口径不一 | 两档数据集 | **已关闭**（SRS v0.2.1 §13.1 AC-13 已分两档） |
| R-13 | §11.4 CI-8 vs 附录 D | 缺 `ci.token` | 附录 D 4 键 | **已关闭**（SRS v0.2.1 附录 D `ci.*` 4 键含 `ci.token`） |
| R-14 | FR-OPS-02 vs §12.5 | 诊断包清单不一 | 以 §12.5 为准 | **已关闭**（SRS v0.2.1 FR-OPS-02 引用 §12.5） |
| R-15 | §7.4 优先级 1 vs 2 | BINDING_ERROR/UNKNOWN 遮蔽未裁决 | 加裁决句 | **已关闭**（SRS v0.2.1 §7.4 BINDING_ERROR/UNKNOWN 裁决句） |
| R-16 | §11.4 契约 | 旧字段名与 §8.1 不一致、字符串 repo id、`ci_status` 缺列 | AC-CI-05/AC-CI-09 按 SRS v0.2 回写 | **已关闭**（SRS v0.2.1 §11.4：`componentId`/数字 `repoId`；§8.1 `ci_status`；§8.3 映射表） |
| R-17 | §11.4 CI 契约 | 聚合契约旧字段名、无 repo 级主键、undefined 与 NONE 未区分 | 字段改 `componentId`；`ci_status` PK `(repo_id,commit_sha)`；无数据=NONE | **已关闭**（SRS v0.2.1 §11.4/§8.1） |
| R-18 | §9.10 设置页 | “实验特性”分区无附录 D 键 | AC-OPS-07 断言该分区存在但 v1 无可配置键 | **已关闭**（SRS v0.2.1 FR-OPS-03：实验特性 v1 无键，仅说明/占位） |
| R-19 | §8.1 `ci_status` | 缺 `build_number/started_at/finished_at` | AC-CI-09 按新列断言 | **已关闭**（SRS v0.2.1 §8.1 `ci_status` 全列含 `build_number/started_at/finished_at`） |
| R-20 | §8.1 多个枚举字段 | 无取值集 | 按 §8.1 CHECK 断言 | **已关闭**（SRS v0.2.1 §8.1：全部 CHECK 集已冻结，并补 `job.skipped`、`job_item.started_at/finished_at`、`job_item.state=INTERRUPTED`、`sync_run.duration_ms`、`component.last_activity_at`） |
| R-21 | §8.1 `remote_ref.ref_type` | 无 CHECK/枚举 | AC-SYNC-05 断言 `BRANCH`/`TAG` | **已关闭**（SRS v0.2.1 §8.1 `ref_type CHECK BRANCH/TAG`） |
| R-22 | §6.3 `safe.directory` | 无错误码落点 | 附录 D `SCAN-E001` | **已关闭**（SRS v0.2.1 附录 D `SCAN-E001`） |
| R-23 | §7.2 DIRTY 条件 | 无 `dirty.includeUntracked` 键 | 默认 false；AC-STA-13 参数化 | **已关闭**（SRS v0.2.1 附录 D `dirty.includeUntracked` + D-021） |
| R-24 | §11.2/§10.5 | DIRTY token/对比度未冻结 | `DIRTY=success+变更徽标` | **已关闭**（SRS v0.2.1 §11.2 `DIRTY=success+变更徽标`）；hatched 对比度 H-04 已由 v0.2.1 修正（`#4B5563`） |
| R-25 | FR-OP-05 vs §7.6 | detached 下 SYNC 目标未定义 | §7.6 DETACHED SYNC=❌，AC-OP-17 跳过 | **已关闭**（SRS v0.2.1 §7.6 DETACHED 行 SYNC=❌） |
| R-26 | FR-SYNC-05/SCAN-05 | fetch prune 策略缺失 | `remote.prune=true` | **已关闭**（SRS v0.2.1 附录 D `remote.prune=true`） |
| R-27 | FR-OP-12 vs §8.1 | component 无停止管理字段 | `is_managed/is_deleted` | **已关闭**（SRS v0.2.1 §8.1 `component.is_managed/is_deleted`） |
| R-28 | `safety.requireTypedConfirm` | 输入组件 ID 的大小写/空格未定 | AC-SAFE-03 按冻结规则断言 | **已关闭**（SRS v0.2.1 附录 D：精确匹配、区分大小写、忽略首尾空格） |
| R-29 | FR-BOARD-02 | 中文 IME 规则未写入 | AC-BOARD-05 断言 `compositionend` 触发 | **已关闭**（SRS v0.2.1 FR-BOARD-02：IME 以 `compositionend` 触发） |
| R-30 | FR-DETAIL-03 | 白名单扩展名未列举 | AC-SAFE-10 按完整白名单断言 | **已关闭**（SRS v0.2.1 FR-DETAIL-03：白名单 12 类已列） |
| R-31 | FR-OPS-01 | 通知“清除”语义未定 | AC-OPS-01 ④ 断言删除行 | **已关闭**（SRS v0.2.1 FR-OPS-01：清除=删除通知行） |
| R-32 | FR-OPS-02 | 日志级别过滤语义未定 | AC-OPS-05 ① 断言“≥所选级别” | **已关闭**（SRS v0.2.1 FR-OPS-02：显示 ≥所选级别） |
| R-33 | FR-OPS-04 | stale lock 阈值无数值 | AC-OPS-10 断言三条件（含 mtime>10min） | **已关闭**（SRS v0.2.1 FR-OPS-04：PID 不存在 / PID 非本应用 / mtime>10min 且无存活进程） |
| R-34 | FR-OPS-05 | 进程健康灯映射未定 | AC-OPS-11 断言 Host+GitWorker 2 灯 + 子状态 | **已关闭**（SRS v0.2.1 FR-OPS-05：2 灯 + `BackgroundService` 子状态） |
| R-35 | §3.1/§11.1 | 进度协议字段未定义 | 三阶段 + `{stage,total,current,cancellable}` | **已关闭**（SRS v0.2.1 §11.1 首扫进度协议：阶段枚举 + `{stage,total,current,cancellable}`） |
| R-36 | FR-OPS-08/D-016 | 远程未就绪中间态命名未定 | `UNKNOWN` + `incomplete=1` | **已关闭**（SRS v0.2.1 FR-OPS-08：`UNKNOWN`+`incomplete=1`，不得 INVALID/MISSING） |
| R-37 | FR-DETAIL-02 | “滚动流畅”不可判定 | AC-DETAIL-04 按量化指标断言 | **已关闭**（SRS v0.2.1 FR-DETAIL-02：首屏 ≤500ms/≥58fps/单帧 ≤33ms） |
| R-38 | §8.1 `unbound_repo.reason` | 取值集未定义 | AC-SYNC-13/AC-BOARD-13 按 `DOCS/PATTERN/CASE_CONFLICT/UNKNOWN` 断言 | **已关闭**（SRS v0.2.1 §8.1 `reason CHECK DOCS/PATTERN/CASE_CONFLICT/UNKNOWN`） |

### 4.3 本目录不覆盖项（版本外/非 FR 范围）

| 项 | 原因 | 处理 |
|---|---|---|
| 真实 CI API 接入/告警（v2） | SRS §3.2 Out-of-Scope；v1 只有 Mock/契约 | 仅冻结 AC-CI-* 的 v1 占位与契约；v2 另立 AC |
| 基线管理 UI（v2：列表/冻结/漂移对比/导出） | §9.12 v2 占位 | v1 只覆盖 `baseline_snapshot` 记录与 `TAG_DRIFT`（AC-OP-11/12/14、AC-STA-11） |
| 远端仓库创建/删除/改名（明确不做） | §3.2 | 不设 AC；AC-SYNC-10 只断言“远端已删的本地提示”，不提供远端操作 |
| SSH/OAuth/SSO 认证（v2） | §3.2 | 不设 AC；FR-CFG 只验 PAT |
| 跨组件“全组织”批量写（v2） | §3.2 | 不设 AC；FR-BOARD-04 只验“筛选结果显式多选” |
| 多根目录（v1.1） | D-013/OQ-17 | AC-CFG-02 只断言单根；多根入口不存在 |
| Windows 10 22H2 ESU | D-018/OQ-19 | 验收环境固定 Win11 x64 |
| RDP 多会话/共享工作区写操作 | NFR-COMPAT-07/OQ-18 | 只读提示 + 写拒绝（AC-SCAN-12） |
| 自动更新（v1.1） | FR-OPS-07 P2 | AC-OPS-14 只冻结“v1 无入口” |
| 安装包签名/分发校验 | NFR-COMPAT-08，归 M3 打包验收（非 FR） | 本目录不设 AC，由发布验收承接 |
| 托盘/登录自启形态 | OQ-11 待确认（设计层） | 不设 AC（§4.1 OQ-11 口径） |

---

## 5. AC 冻结门禁

### 5.1 M0 必须冻结的 AC（不允许 M1/M2 再补语义）

M0 = 骨架与冻结（SRS §3.3）。以下 AC 的 Given/When/Then/预期值/夹具/错误码**全部冻结**，M0 退出时须全部可执行（可通过 Mock/夹具跑通或确定性失败）：

| 类别 | 必须冻结的 AC | 理由 |
|---|---|---|
| 状态与聚合（最高优先级） | AC-STA-01…AC-STA-15 | 状态矩阵是全部 UI/写操作的判定基座；附录 C 夹具同批冻结 |
| 配置与认证契约 | AC-CFG-01…AC-CFG-11 | 后续所有 E2E 的前置条件；Token 安全面（CFG-06/07）必须早冻结 |
| 远程同步契约 | AC-SYNC-01…AC-SYNC-14 | 清单/覆盖率/错误码/改名语义是数据模型输入 |
| 本地扫描契约 | AC-SCAN-01…AC-SCAN-13 | 只读零写入、BUSY、分层频率是正确性底线 |
| 状态×操作矩阵 | AC-STA-15、AC-OP-02、AC-OP-17 | §7.6 是写操作放行/禁止的唯一依据 |
| 破坏性安全基线 | AC-SAFE-01…AC-SAFE-13（SAFE-05/06/07 允许 M3 补测试细节，但断言语义 M0 冻结） | 数据丢失风险；审计/journal/备份语义必须先行 |
| CI 预留 | AC-CI-01…AC-CI-11 | 10 锚点/契约/DDL/色板通道 B 是 M0 交付物（SRS §3.3 M0 出口） |
| 首启三阶段 | AC-OPS-15、AC-OPS-16 | D-016 的门控与重判是 M1 出口前提 |
| 性能与 a11y 基线 | AC-BOARD-03、AC-BOARD-05、AC-BOARD-14、AC-BOARD-15、AC-SCAN-08、AC-SCAN-09 | 预算口径/测量方法/数据集生成器需在 M0 冻结，M1 才有可比基线 |
| 错误码与设置键 | AC-OPS-07、AC-OPS-08、AC-OPS-17 | 附录 D 的键/码是跨模块契约 |
| 单实例/DB 基线 | AC-OPS-09、AC-OPS-10、AC-OPS-12 | 数据目录/锁/schema 版本在 M0 定型 |
| 详情/看板骨架断言 | AC-BOARD-01、AC-BOARD-08、AC-DETAIL-03、AC-DETAIL-06、AC-DETAIL-07 | 路由/Tab 顺序/安全边界后加成本高（CI-10 同理） |

### 5.2 M1/M2 允许补充的 AC 边界（只增不改语义）

| 里程碑 | 允许补充 | 禁止改动 |
|---|---|---|
| M1（只读闭环） | ① 视觉回归快照张数扩展（≥12 → 全覆盖 15 聚合态 × 深色）；② 只读交互细节断言（hover/tooltip/键盘顺序）细化；③ 新增夹具变体（同一状态的不同构造）作为**新 AC 编号** | 不得修改已冻结 AC 的 Then 期望值/枚举/错误码；不得降低性能/安全/状态断言 |
| M2（写操作） | ① 故障注入矩阵扩展（更多 4xx/5xx 组合、更多部分失败组合）作为新 AC；② E2E 脚本细化（步骤拆分不改变断言）；③ 任务中心/取消/重试的补充用例 | 不得改动 §7.6 矩阵、错误码映射、备份/stash/CAS 顺序、`.trash` 保留期 |
| M3（收口） | ① 日志/诊断/通知/设置的数据绑定细节；② 安装/发布验收（非 FR） | 不得改动 AC 的判定语义；P0 AC 不得以“收口期优化”为由降级 |

### 5.3 冻结检查清单（M0 签收用）

- [ ] 1. **FR 覆盖**：72/72 FR 均有 ≥1 条 AC，追溯矩阵无悬空 FR（§3）。
- [ ] 2. **P0 路径**：每条 P0 FR 的 AC 同时覆盖成功路径与关键失败/异常路径（§3“失败/异常路径”列）。
- [ ] 3. **可自动化**：全部 Then 落在 6 类观测源（DB/IPC/UI DOM/日志错误码/FS/git 输出），无“正常/流畅/合理/友好”等词（§1.1 禁止词扫描命中=0）。
- [ ] 4. **夹具可执行**：附录 C 全部 `F-*` 由 `v0.2/fixtures/` 生成器产出，`expected.json` 与 AC-STA-01/02 期望逐条一致（红队 06 AC-04 关闭）。
- [ ] 5. **不可判定项关闭**：§1.5 表 15 行（红队 93–115 行）逐条有 AC 编号且 AC 可执行；“其他可测试性缺陷”5 项已处置。
- [ ] 6. **状态模型闭合**：14 主状态/11 flags/15 聚合态/UNKNOWN 兜底/§7.6 矩阵全部有 AC（AC-STA-01/02/03/15）。
- [ ] 7. **TTL/离线**：TTL 数值（90/1200/25200s）与离线可判定状态集有 AC（AC-STA-09/14），且 §7.1 表与附录 D 默认值一致。
- [ ] 8. **首启三阶段**：阶段名/进度字段/跳过降级/自动重判有 AC（AC-OPS-15/16），且与 §3.1/§11.1 口径一致（三阶段）。
- [ ] 9. **安全**：header-only/既有仓 token 扫描/转储与诊断包脱敏/Gitea 无写请求均有 AC（AC-SAFE-04/05/06/07/12/13、AC-CFG-06/07）。
- [ ] 10. **Tag/TAG_DRIFT**：无“基线模式”，无条件计算有 AC（AC-STA-11、AC-OP-11/12/13/14）。
- [ ] 11. **CI 预留**：10 锚点/契约/DDL/底色不变/无裸 TODO/新鲜度均有 AC（AC-CI-01…AC-CI-11）。
- [ ] 12. **性能**：两档数据集、测量方法（§13.3）、门禁（BENCH）写入 AC（AC-BOARD-03/05/14、AC-SCAN-08/09、AC-SYNC-12）。
- [ ] 13. **SRS 修订闭环**：§4.2 R-01…R-38 已逐条标状态（37 已关闭 / 仅 R-06 仍开放：**M0 出口条件**，附录 D 错误码全表冻结时回写 AC）；§4.1 OQ-21/OQ-22 已关闭，OQ-23/OQ-24 仍为 M0/版本归属决策。
- [ ] 14. **版本外项**：v1.1 项（FR-CFG-06/DETAIL-05/REPO-04/OPS-07）的 AC 只冻结“入口不存在”，完整行为移入 §2.12 v1.1 AC（不冻结），不阻塞 M0。
- [ ] 15. **编号与映射**：新编号规则生效、旧 AC-01…27 映射表存在（§1.2/§1.4），无重复/空缺编号。
- [ ] 16. **签收**：产品/QA/架构三方确认；冻结记录（版本号、日期、文件哈希）写入本文件末尾。

### 5.4 冻结记录（待填）

| 项 | 内容 |
|---|---|
| 冻结版本 | 本文件 v0.2.1 |
| 文件哈希（SHA256） | 待填（冻结时由 CI 计算并登记） |
| 冻结日期 | 待填 |
| 签收人 | 产品：____　QA：____　架构：____ |
| 结论 | ☐ 通过　☐ 有条件通过（列条件）　☐ 不通过 |
| 变更规则 | 冻结后只增不改语义；改 Then 期望值/枚举/错误码 → 先改 SRS（修订记录 + ADR）→ 再改 AC 并升版本号 |

> 与 `v0.2/freeze-checklist.md` 的关系：该清单 §2 的 AC 检查项以本文件为证据；本文件 §5.3 是其可执行化展开，二者需同时签收。

