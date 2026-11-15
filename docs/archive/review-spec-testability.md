# 规格可测试性评审（只读）— MBD 组件全生命周期 Git 工具

| 项 | 内容 |
|---|---|
| 评审对象 | `reqs/specification.md`（3211 行）：SRS v0.4-draft 合并稿 + Annex A（AC Catalog v0.2.1）+ §13.1.1/§13.1.2 + §14 M2b 增量 |
| 基线 | `61c27b6`（工作区 clean，HEAD 即该提交） |
| 评审范围 | **重点**：Annex A §2.7–2.9 的 `AC-OP-01…32`、`AC-REPO-01…11`、`AC-SAFE-01…13`（下称“56 条”）；§14.1–14.9；§13.2/§13.4；可测性门禁 §1.1/§1.3/§1.5/§1.6/§3/§4.2/§5；附录 B/C/D。**辅助**：依赖资产（夹具/脚本/报告）在仓库中的实际存在性（只读 grep + git 历史），用于判断“依赖可用性”，不作为对实现的评审 |
| 方法 | 逐条核对 6 字段（覆盖 FR/测试层级依赖/Given/When/Then/关联 NFR）、断言是否落在可程序读取的 6 类观测源、期望值是否唯一、Given 是否可脚本一键起、依赖资产是否存在、是否与 §14/§8.1/§7.6/附录 C/D 冲突 |
| 结论 | **需先修（不可冻结）**：AC 文本层 P0 8 条、P1 45 条、P2 3 条；**文档/证据层 P0 8 项**（见 §5）。当前既不能作为 M0 冻结基线，也不能作为 M2b DoD 的判定依据 |
| 只读声明 | 仅新增本文件；未修改 `specification.md` 及任何其他文件 |

---

## 1. Annex A（AC-OP / AC-REPO / AC-SAFE）可测试性审计

### 1.1 总览（先说结论好的部分，再说阻断）

**通过项（自动化核对）**

- **6 字段齐全**：56/56 条 AC 均含 `覆盖 FR`、`测试层级/依赖`、`Given`、`When`、`Then`、`关联 NFR/OQ`（脚本核对 Annex A 行 2192–2644）。
- **编号完整**：`AC-OP-01…32`、`AC-REPO-01…11`、`AC-SAFE-01…13` 无缺号、无重复（全文件 173 个 `#### AC-` 头，含 §13.1.2 的 9 条与 2 条 v1.1 草案）。
- **禁止词扫描 0 命中**（§1.1 规则 3 的 15 个词在 56 条内未出现，仅文本层）。
- 部分 AC 断言非常具体可执行（正例）：`AC-SAFE-03`（大小写/首尾空格匹配规则，行 2563）、`AC-SAFE-05`（五类正则样本，行 2577）、`AC-OP-11/14`（`^{commit}` 解引用、`tag_object_sha`，行 2279/2303）、`AC-OP-20`（`.trash` 结构 + realpath，行 2351）、`AC-OP-29`（外部 `index.lock`→`OP-E001` 且不删锁，行 2423）。

**阻断项（量化）**

| 指标 | 数值 | 判定 |
|---|---|---|
| `Given` 未引用任何夹具/依赖代号（违反 §1.1-5、§1.3 字段纪律） | **17/56**：OP-14/23/26/27/29/32、REPO-07、SAFE-02/03/05/06/07/08/09/10/11/12 | 违反冻结纪律 |
| 未引用 `F-*` 夹具 ID（允许 `G-E2E`/`G-WIRE`/`CLK`，但仍缺组件级夹具口径） | 31/56 | 依赖层 P1 |
| 依赖 `G-E2E`（“Gitea 单二进制 + E2E-NORMAL/STRESS 数据集”） | 16/56：OP-01/02/03/04/08/11/13/24/25/31、REPO-01/04/05/11、SAFE-04/13 | **该资产在仓库不存在**（见 §3.3），E2E 级 AC 全部不可复跑 |
| 依赖 `G-WIRE`（WireMock.NET 契约夹具） | 3/56：OP-02、SAFE-04、SAFE-13 | **未实现**（无 WireMock 包/代码，见 §3.2） |
| 引用 `F-*` 但生成器已删 | 25/56 | 附录 C 只剩表格，无 `manifest.json`/`expected.json`/生成器（见 §4） |
| `Then` 含“或/若/按规则/按设计/至少/中对应码”等**二选一或条件期望** | 16 条：OP-02/05/07/17/18/22/25/32、REPO-03/06/08/09、SAFE-07/08/09/13 | 期望值不唯一，第三方无法判定通过 |
| 与 §14 直接冲突 | 2 条：OP-12（vs §14.4）、OP-23（vs §14.2） | **P0** |
| 时间断言但未走 `IClock`（违反 §1.1-4） | 3 条：OP-26（每仓≥2s）、OP-27（取消≤2s/每仓≥3s）、SAFE-09（第 2 仓时终止） | 墙钟/调度依赖，不可复现 |
| 引用未冻结错误码（附录 D R-06 仍开放，行 3014） | OP-04（`GIT` 前缀）、OP-23（`FS-E001` 前缀类）、OP-02（`FS-E001`/`NET-E001` 二选一） | P0/P1（见 1.3.7） |

### 1.2 逐条判定（56 条）

> 判定口径：**P0**=按现有文本第三方无法复现判定（矛盾/期望不唯一/依赖不存在且无法以测试内构造替代）；**P1**=断言可判定但缺夹具/依赖、或存在二义使证据链弱；**P2**=文字/格式问题。
> “依赖状态”：`F`=需已删除的 `F-*` 生成器；`E`=需不存在的 `G-E2E` 数据；`W`=需未实现的 `G-WIRE`；`C`=需 `CLK`/可注入假时钟（当前仅局部 `TimeProvider`，见 §3.3-④）；`—`=无依赖声明。

| AC | GWT 可判定性 | 主要缺陷/缺口（spec 行号） | 依赖状态 | 级别 |
|---|---|---|---|---|
| OP-01 | 可判定（Then 具体） | Given“组织 MBD / CF325F_…”与真实测试环境 `RTCRepos / RTCT_*` 不符；幂等以 `.git` mtime 断言（行 2199 ⑤） | E | P1 |
| OP-02 | **不可判定** | 标题“任一失败即阻断”与 Then④“其余仓可继续”自相矛盾（行 2202/2207）；磁盘余量“注入”无手段；`FS-E001/NET-E001 中对应码` 二选一（行 2207） | E/W | **P0** |
| OP-03 | 可判定（依赖注入） | 需要 `G-E2E` 按仓注入 403/500；无注入配置契约（行 2213） | E | P1 |
| OP-04 | 部分不可判定 | “错误碼前缀 `GIT`”与附录 D（认证=`SYNC-E001`、`GIT-E001`=空仓）不符；`R-06` 未冻结（行 2224/3014）；“attempt≤2”缺固定期望 | E | P1 |
| OP-05 | 部分不可判定 | ① “转 Repair”**或** `SKIPPED` 二选一（行 2231）；“提供 Repair/Re-clone 入口”未给 UI 断言 | F | P1 |
| OP-06 | 可判定 | Given 4 仓“均为 F-BEHIND”但 `F-*` 为单仓夹具，组件级 4 仓组合未定义 | F/E | P1 |
| OP-07 | 部分不可判定 | ① 码值“`OP-E004` **或** git 前缀”（行 2247） | F | P1 |
| OP-08 | 可判定 | 依赖 G-E2E 部分仓 500 注入契约（行 2253） | E | P1 |
| OP-09 | 可判定 | 5 前置态 × F-* 可组装，但 4 仓/5 仓组件夹具无清单 | F | P1 |
| OP-10 | 可判定 | untracked/stash 夹具在附录 C 无行（行 2270） | F | P1 |
| OP-11 | 可判定 | 四仓 `cD/cI/cM/cS` 未由任何夹具给定；`baseline_snapshot` 列依赖 §8.1（行 2279） | E | P1 |
| OP-12 | **不可判定（冲突）** | ③ 要求 baseline **写 4 行**（含跳过仓 `tag_present=0`），§14.4 行 3173 规定“失败/跳过不写”；§14.4 行 3171 自身又写“写 0 或按设计不写行，须实现评审定稿” | F | **P0** |
| OP-13 | 可判定 | “旧行保留**或**按快照语义新增”（行 2295 ④）；tag 移动需 G-E2E 服务端操作 | E/F | P1 |
| OP-14 | 部分不可判定 | Given“annotated tag 夹具”无 ID/构造；轻量 tag `tag_object_sha` 应为 null 未断言（行 2301–2303） | — | P1 |
| OP-15 | 部分不可判定 | ③ 外部进程改 HEAD/index 的**注入/竞态钩子**未定义；`refs/backup/sync/<ts>/<branch>` 与实现已改成 `…/<branch>/<TYPE>`（见 docs/m2b2b-status.md）不一致；LFS 服务端未定义（行 2311） | E/F | P1 |
| OP-16 | 部分不可判定 | Given 仅 1 仓场景，Then ① 却断言“影响仓数=4”（行 2317/2319）；DIVERGED 四仓夹具未定义 | F | P1 |
| OP-17 | 部分不可判定 | ④ “组件 `OP_PARTIAL_FAILED` **或**按清除规则”（行 2327） | F | P1 |
| OP-18 | 部分不可判定 | ④ “若不可恢复必须明示”为条件断言、无固定期望（行 2335）；untracked/目录夹具未定义 | F | P1 |
| OP-19 | 可判定 | “本地独有分支夹具”无 ID（行 2341）；UPSTREAM_GONE 需服务端删分支 | F | P1 |
| OP-20 | 可判定（正例） | 需本地仓夹具 + `realpath` 恶意路径构造 | F | P1 |
| OP-21 | 可判定 | stash 夹具在附录 C 无行（行 2356） | F | P1 |
| OP-22 | 部分不可判定 | “推进 `CLK` 触发清理任务”但清理触发入口（启动/定时/命令）未定义；“可在设置**或**诊断中查看”二选一（行 2367 ⑥） | C | P1 |
| OP-23 | **不可判定（冲突）** | ② 错误码 `FS-E001` 前缀类 vs §14.2 行 3149 `E_DELETE_PENDING`；未断言 `.trash/.pending/<stamp>-<repo>.json` 及其字段；未断言“仅重试 pending/显式取消 pending/不删锁/不递归强删”（行 2375） | — | **P0** |
| OP-24 | 可判定 | 依赖 G-E2E 四仓与 `check-ref-format`（本地可做） | E/F | P1 |
| OP-25 | 部分不可判定 | 基线存于“`job_item.message`/基线字段，枚举见 §8.1”无确定列；① 确认按钮“禁用**或**要求选择跳过”无规则（行 2391） | E | P1 |
| OP-26 | **不可判定** | “克隆耗时≥2s 夹具”不存在；强制终止**时点**与机制未定义；`INTERRUPTED`/未开始行断言依赖任务中心实现（行 2397–2399） | — | **P0** |
| OP-27 | **不可判定** | “取消后 ≤2s”为墙钟断言，违反 §1.1-4；无 IClock 路径；“每仓耗时≥3s”夹具不存在；“对不支持取消的操作禁用”未给枚举（行 2407 ⑥） | — | **P0** |
| OP-28 | 可判定（较正例） | 需可注入假时钟推进 91 天；导出格式“CSV/JSON 至少 1 种”无固定期望（行 2415 ②） | C | P1 |
| OP-29 | 部分不可判定 | 同组件串行以 `started_at/finished_at` 比较，但两列**精度/时区**未定义（行 2423 ①）；“不同组件可并行”无期望值/并发上限 | — | P1 |
| OP-30 | 部分不可判定 | 备份 ref 路径格式与实现不一致（`<ts>/<branch>` vs `<ts>/<branch>/<TYPE>`）；“备份 ref 已过期”判定条件未定义（行 2431 ③） | C/F/E | P1 |
| OP-31 | 部分不可判定 | §14.1 fail-closed“远程不可用不写库”无断言；“已纳管缺仓仍上板”无断言；看板默认视图过滤口径未给（行 2439） | E | P1 |
| OP-32 | 部分不可判定 | ① “默认视图不显示（**或**移入已移出区）”二选一；§14.1 行 3143 “移出确认必须显示差异与影响”无断言（行 2447） | — | P1 |
| REPO-01 | 可判定 | 需 G-E2E + F-BEHIND；操作前自动重扫断言具体 | E/F | P1 |
| REPO-02 | 可判定 | 需 F-BEHIND/F-DIVERGED | F | P1 |
| REPO-03 | 部分不可判定 | ③ “按 7.6 阻断（**或**需 stash 确认）”条件化（行 2473） | F | P1 |
| REPO-04 | 可判定 | 需真实 Gitea 写 + `write:repository` 权限（测试环境权限不稳，见 §3.1） | E | P1 |
| REPO-05 | 部分不可判定 | 4 类失败面需保护分支/断网等环境；“服务端原文消息”需捕获服务端响应体；无夹具 ID（行 2487） | E | P1 |
| REPO-06 | 部分不可判定 | ① “转为 `AHEAD`（有未推送提交）**或** `SYNCED`”二选一（行 2497） | F | P1 |
| REPO-07 | 可判定 | v1 只冻结“无入口”，可 CDP 断言；② v1.1 口径不验收 | — | P2 |
| REPO-08 | 部分不可判定 | 仅覆盖 INVALID 两场景；`main_status`“→ SYNCED/EMPTY 等真实态”含“等”；§14.3 的 MISSING/BINDING_ERROR、`git init/fetch/默认分支`、强确认、pre/post HEAD、失败零改动均无断言（行 2513） | F | P1 |
| REPO-09 | 可判定 | 三子型夹具 `F-NO_UPSTREAM-*` 已在附录 C；② 主状态“变为关系态（BEHIND/AHEAD/SYNCED）”可接受但非唯一值 | F/E | P1 |
| REPO-10 | 可判定 | 需 F-DETACHED-*；`head_type` 字段口径依赖 §8.1 | F | P1 |
| REPO-11 | 可判定 | 需 F-EMPTY + G-E2E 空仓；R-05 已关闭 | E/F | P1 |
| SAFE-01 | 部分不可判定 | Given 无夹具 ID（“3 未推送/4 untracked/2 stash”需临时构造）；④ “外部修改后点刷新预览数据变化”未定义修改手段；“将覆盖的分支/tag”条数未给（行 2547） | F | P1 |
| SAFE-02 | 部分不可判定 | Given 无夹具；“DB 全表行数与 `.git`/工作区 hash 快照”未定义快照算法（行 2553） | — | P1 |
| SAFE-03 | **可判定（正例）** | 匹配规则已冻结；需控制 `safety.requireTypedConfirm` 设置 | — | P2 |
| SAFE-04 | 部分不可判定 | 需 G-WIRE 记录 `DELETE/PUT/PATCH/POST=0`，但 G-WIRE 未实现；git smart-HTTP 是否经代理未定义（行 2569）；“固定文案逐字断言”缺文案来源（§11? 行 2571 ①） | W/E | P1 |
| SAFE-05 | **可判定（正例）** | UT + 样本串完整；建议补“脱敏函数入口名/日志落盘路径” | — | P2 |
| SAFE-06 | 部分不可判定 | 需“≥50 条最近任务”数据夹具；`VACUUM INTO` 快照、`PRAGMA user_version` 可测；“转储清单”依赖 SAFE-07 策略（行 2587） | — | P1 |
| SAFE-07 | **不可判定** | “若默认开启…最近 N 个”（N 未定）与“若实现默认关闭…”两分支并存→无唯一期望；`dumps\` 根路径未定义；`Environment.FailFast`“等价手段”未定义（行 2593–2595） | — | **P0** |
| SAFE-08 | 部分不可判定 | ① “开始前已落库”为竞态断言，缺注入钩子；② “`expect_head`（**如** CAS 期望值）”可选（行 2603） | — | P1 |
| SAFE-09 | **不可判定** | “执行到第 2 仓时强制终止”→无确定性暂停/注入点；② `INTERRUPTED` **或** `FAILED+错误码` 二选一；终止机制“不属验收”但时点必须可注入（行 2609–2611） | — | **P0** |
| SAFE-10 | 部分不可判定 | ① 7 类扩展名弹确认可测；② “进程监控无新进程”未定义监控方法；“审计‘已打开’”未给 action 值（行 2619） | — | P1 |
| SAFE-11 | 可判定 | 恶意路径可脚本构造；缺“恶意路径夹具”ID（行 2625） | — | P1 |
| SAFE-12 | 可判定 | 需内嵌 token 的 `.git/config` 夹具（可构造）；⑤ 通知持久化依赖 FR-OPS-01 | — | P1 |
| SAFE-13 | **不可判定** | ①“全部 REST 请求头”、②“git 网络请求（服务端日志或本地抓包）”、③“全部进程命令行”、⑥“无 `sslVerify=false`”均无采集方法/工具/产物格式；G-WIRE 未实现；“抓包”不可在 CI 稳定执行（行 2640–2643） | W/E | **P0** |

### 1.3 关键阻断详解

#### 1.3.1 AC-OP-12 与 §14.4 语义冲突（P0）

- AC-OP-12 行 2287 ③：`baseline_snapshot` **写 4 行**，SIL（缺 Tag、被跳过的仓）`tag_present=0`、`commit_sha` 为 NULL。
- §14.4 行 3173：“**baseline 仅在 TAG job 成功项写入，失败/跳过不写**”。
- §14.4 行 3171 自身仍未定死：“缺 Tag 仓写 0 **或**按设计不写行，须在实现评审定稿”。
- 结论：同一行为存在“写 4 行/写 3 行”两个规范口径。任何证据（3 行或 4 行）都可被另一方判为不符合，DoD 无法判定。**必须先定死一种语义并同步 AC-OP-11/12/14。**

#### 1.3.2 AC-OP-23 与 §14.2 错误码/证据冲突（P0）

- AC-OP-23 行 2375 ②：错误码“`FS-E001` 前缀类，见 R-06”；R-06 仍开放（行 3014），附录 D 无 `DELETE_PENDING` 码。
- §14.2 行 3149：`job_item.errorCode=E_DELETE_PENDING`，并要求 `.trash/.pending/<stamp>-<repo>.json` 记录 `{componentId,repoType,repoName,localPath,errorCode,createdAt}`。
- 差距：AC 未断言 pending JSON（路径/字段/原子写）、任务中心“待处理（文件占用）”文案、仅重试 pending、显式取消 pending、不删锁/不递归强删、审计 `result=DELETE_PENDING`。**§14.2 的 6 个可验证点中 5 个无 AC。**

#### 1.3.3 AC-OP-02 内部矛盾 + 不可注入（P0）

- 标题行 2202：“任一失败即阻断”；Then 行 2207 ④：“预检指出 404 仓名，其余仓可继续（部分预检通过）”。两种期望互斥。
- ① “磁盘剩余 < 单仓体积×2”无注入点（§14.6 注入点仅“git 网络/index.lock/Directory.Move/磁盘写失败/进程崩溃”，无磁盘余量伪造）。
- ① 期望码“`FS-E001`/`NET-E001` 中对应码”→ 第三方无法唯一判定。

#### 1.3.4 “只有目标、没有唯一断言”的条目

除上表 16 条二选一外，以下 Then 是目标/方向而非可判定断言：

- REPO-08 ① “`main_status` 脱离 INVALID（→ SYNCED/EMPTY **等**真实态）”——“等”无枚举。
- SAFE-01 ④ “外部修改仓库，点刷新预览数据变化”——“变化”未给字段/行数。
- SAFE-08 ② “记录 `expect_head`（**如** CAS 期望值）”——可选断言。
- OP-30 ③ “备份 ref 已过期均提示”——“过期”判定条件未定义。
- OP-18 ④ “若不可恢复必须在确认框明示”——条件分支无固定期望。
- OP-22 ⑥ / OP-28 ② / OP-32 ① / OP-05 ① / OP-25 ① 均为二选一（已在表内）。

#### 1.3.5 时间/并发/终止类断言无确定性钩子（P0 组）

- OP-26（行 2399）、OP-27（行 2407）、SAFE-09（行 2611）依赖“杀进程”“每仓≥2s/≥3s”“第 2 仓时终止”，但：
  - §1.1 规则 4 要求时间断言必须用 `IClock` 或 §13.3 测量方法；取消≤2s 两者都不属于。
  - 全仓无统一 `IClock` 抽象：仅 `RemoteRefsService` 使用 `TimeProvider`，测试内 `ManualTimeProvider` 仅 1 处（`tests/MbdGitTool.Host.Tests/RepoActionSecurityTests.cs:566,764`）。
  - §14.6 定义了 `MBD_FAULT_INJECT` 但没有“暂停点/在第 N 仓崩溃/慢克隆”的注入点描述，也没有实现（grep 全仓 `MBD_FAULT_INJECT` 0 命中于 `src/ tests/ ui/`）。
- 不得不多次出现的“外部进程改 HEAD/index”（OP-15 ③、SAFE-08 ⑤）同样缺注入钩子。

#### 1.3.6 外部证据采集方法未定义（P0 组）

- SAFE-13 的四类证据（REST 头、git 网络、进程 argv、remote URL）没有指定采集工具、时点、产物与脱敏校验；其中“抓包/服务端日志”不可在 CI 稳定复跑，“进程 argv”需明确 WMI/ETW/快照方式。
- SAFE-10 “进程监控无新进程”、SAFE-02 “`.git`/工作区 hash 快照”均未定义算法；SAFE-07 的转储目录根路径（`dumps\` 相对于什么）未定义。

#### 1.3.7 错误码双体系 + R-06 仍开放

- 附录 D（行 1384–1405）是 `CFG-E001/SYNC-E001/OP-E003…` 体系；§13.1.2（行 1098）与实现/`§14.2` 使用 Host `E_*` 并通过 `details.srsCode` 映射。Annex A §1.1-2 却要求 Then 断言“附录 D 错误码”。
- 结果：`AC-OP-04` 的“`GIT` 前缀”、`AC-OP-23` 的 `FS-E001` 前缀类、`AC-SAFE-13` 的“无 `?token=`”之外码值，都缺少唯一可映射的冻结码表。R-06 是 §4.2 唯一“仍开放”项（行 3014），而 §14.9 DoD 又要求 AC-OP-04/23 全部 COVERED——**未冻结的码表无法支撑 COVERED**。

#### 1.3.8 夹具/依赖代号缺失清单（违反 §1.3 Given 纪律）

- 17 条无任何夹具/依赖代号：`OP-14/23/26/27/29/32`、`REPO-07`、`SAFE-02/03/05/06/07/08/09/10/11/12`。
- 其余依赖 `F-*` 的 AC 也无法执行：附录 C 的 25 个 `F-*` 在仓库中只剩表格，生成器 `v0.2/fixtures/state-matrix.ps1`、`expected.json`、`manifest` 已随 `61c27b6` 删除（见 §4）。
- 组件级（4 仓）夹具未定义：`F-*` 是单仓夹具；`AC-OP-06/09/11/12/15/16/17/21/25/31` 都写“4 仓”，但无组件级夹具 manifest/期望文件。

#### 1.3.9 依赖资产不可用导致“层级写得出、跑不起来”

- 标注 E2E 的 AC 共 16 条（表 1.1），声明依赖 `G-E2E`；仓库无 gitea 二进制 fixture、无 E2E-NORMAL/STRESS 数据集生成器（`C-SYNTH` 也不存在），只有 `192.168.50.20:3000` 上的手工环境（见 §3.1）。
- 标注 IT 的 AC 多依赖 `F-*`/`G-WIRE`，两者均不可用。
- 结论：**56 条 AC 中仅少数（SAFE-03/05/11、REPO-07 等纯本地/单测可构造项）能在当前仓库内按 Given 直接自动化；其余均因夹具/工具/环境缺失而无法“按 Given 一键起、按 Then 逐项断言”完整执行。**

---

## 2. §14 新增要求 → AC/测试证据映射；DoD 可客观判定性

### 2.1 §14 逐节映射矩阵

| §14 | 新增要求（摘要） | 映射到的 AC | 缺口（无对应断言/无 AC） | 判定 |
|---|---|---|---|---|
| 14.1（3135–3144） | `is_managed/is_deleted` 数据、扫描合并（纳管缺仓仍上板）、`bindComponent` 远程只读校验、缺仓按 D-015 纳入、**远程不可用 fail-closed 不写库**、`unbindComponent` 两模式、审计 `COMPONENT_BIND/UNBIND`、重新纳入恢复、远端零写、UI 入口与差异说明 | AC-OP-31（①–⑤）、AC-OP-32（①–⑤） | fail-closed（行 3138）无断言；“纳管缺仓仍出现在看板”（行 3137）无断言；“移出确认显示停止管理/本地删除差异与影响”（行 3143）无断言；`operation_log.details.mode`（行 3142）未断言；“已移出视图”二选一（AC-OP-32①） | **部分映射** |
| 14.2（3146–3152） | `Directory.Move` 失败触发、目录保留原处、`.trash/.pending/<stamp>-<repo>.json` 六字段、`job_item.state=FAILED`+`E_DELETE_PENDING`、任务中心“待处理（文件占用）”、仅重试 pending、显式取消 pending、审计 `DELETE_PENDING`、不删锁/不递归 | AC-OP-23（①–⑤） | 仅“DB 有标记/UI 待删除/重试成功/审计”被部分断言；**pending JSON 与六字段、任务中心文案、仅重试 pending、取消 pending、不删锁/不递归、审计 result 均无断言**；错误码与 §14.2 冲突（见 1.3.2） | **大部分未映射** |
| 14.3（3154–3163） | 入口=任务中心/组件抽屉；目标 `INVALID/MISSING/BINDING_ERROR`；REPAIR=`git init`+origin+fetch+默认分支检出+不覆盖+冲突零改动；RECLONE=移入 `.trash` 再 clone、移动失败不 clone；两操作均确认（RECLONE 输入组件 ID+提示 `.trash`）；审计 pre/post HEAD；失败零改动；状态重算；目标不存在→CLONE | AC-OP-05、AC-REPO-08 | AC-REPO-08 只覆盖 INVALID（NOTREPO/REMOTE）；**MISSING/BINDING_ERROR 的 repair 无 AC**；REPAIR 细节（fetch/默认分支/不覆盖/零改动）无断言；RECLONE“移动失败则不 clone”无断言；强确认无断言；pre/post HEAD 审计无断言（行 3162）；“目标不存在→AC-OP-01”无交叉引用 | **部分映射** |
| 14.4（3165–3174） | baseline_snapshot 四字段（`commit_sha/tag_object_sha/tag_present/captured_at`）、轻量 tag null、四仓同 Tag 不漂移、任一缺 Tag→TAG_DRIFT、仅成功项写入 | AC-OP-11/12/14 | `captured_at` 未断言；轻量 tag `tag_object_sha=null` 未显式断言；`tag_present` 缺失仓语义**未定死且与 §14.4 行 3173 冲突**（见 1.3.1）；“四仓混合 3 检出+1 跳过”无独立证据口径（AC-OP-12 是唯一候选） | **冲突+缺口（P0）** |
| 14.5（3176–3181） | `undoCreate {jobId}`；基线依据 `refs/baseline/create/<ts>/<branch>/<TYPE>`；逐仓校验“仍指向基线/未被检出/无新提交”，否则 SKIPPED+原因；仅本地 `branch -D`；不删远端；审计 `CREATE_UNDO`；失败零改动；部分成功逐仓报告 | AC-OP-25 | ② AC 写“`job_item.message`/基线字段，枚举见 §8.1”，与 §14.5 的 `refs/baseline/create/...` 不一致；撤销的四项守卫、SKIPPED 原因、远端未删、`CREATE_UNDO` 审计值、失败零改动、逐仓部分结果**均无断言** | **仅部分映射** |
| 14.6（3183–3189） | `MBD_FAULT_INJECT` 仅测试启用；6 类注入点；G-WIRE 记录 `{method,url,headers(脱敏),status}` 且禁记 token 明文；报告 `docs/m2b-fault-injection-report.md` 逐 AC-SAFE-01…13 给注入点/期望/实测/证据；报告可复跑 | AC-SAFE-01…13（意图） | **没有一条 AC 引用 `MBD_FAULT_INJECT`**；6 类注入点无 AC/映射；G-WIRE 记录 schema 的断言（AC-SAFE-04 只断言写方法数=0）不含脱敏/字段；报告文件不存在、无格式/schema/复跑命令；生产默认禁用的断言缺失 | **无可用证据** |
| 14.7（3191–3197） | 真实 Gitea 测试先探测 `/api/v1/version`；可达运行、不可达 SKIP(exit 0) 并写 `result.json`；`MBD_GITEA_URL/PROBE_URL` 覆盖；单测用本地 bare remote | 无 AC（门禁本身） | 见 §3.1：SKIP 即 pass；`result.json` 由脚本单测写、聚合器不写；schema/path/优先级未在 spec 冻结；只探版本不验权限/组织/数据；固定测试环境与实际环境不符 | **口径不闭合（P0）** |
| 14.8（3199–3201） | I18N 本轮不做；输入 `docs/i18n-feasibility-input.md` | 无（合理） | 无需求可断言；建议 DoD 明示“不在范围”以免被误判 | 通过 |
| 14.9（3203–3211） | DoD 5 条 | 见 2.2 | 5 条均不可客观判定 | **P0** |

### 2.2 §14.9 DoD 可客观判定性逐条

| DoD | 原文要求（3205–3211） | 是否可客观判定 | 问题 |
|---|---|---|---|
| 1 | “AC-OP-01…32、AC-REPO-01…11、AC-SAFE-01…13、AC-UI-10c 全部 COVERED（不允许 PARTIAL/GAP 计入完成）” | **否** | `COVERED/PARTIAL/GAP` 在 spec 正文无定义（仅 `docs/m2b-ac-op-gap.md:3` 有非规范定义）；无“AC→证据路径→复跑命令→commit”证据模板；台账现状：AC-OP-19/23/31/32=GAP、AC-REPO-01…11=GAP、AC-SAFE-01…13=PARTIAL（`docs/m2b-ac-op-gap.md:26,30,38–41`），但该台账不是规范文件，无法作为 DoD 依据。 |
| 2 | “14 态×5 键矩阵在 M2b 构建复验；单源夹具与 Host/UI 一致” | **否** | 单源夹具 `reqs/fixtures/m2-matrix.json` 自述矛盾：`"status":"aligned"`（:26）与 freezeNotes“本文件状态为 SINGLE_SOURCE_PENDING_CODE_ALIGNMENT…UI 仍持第二套 MATRIX”（:34）；UI 侧确有第二份手写矩阵 `ui/src/mocks/workbenchMock.ts:663`；`"buildVariant":"M2b"`（:6）但 AC-UI-08 期望取自 M2a 构建覆盖（spec 行 1096/1082 的路径 `reqs/v0.2/fixtures/m2-matrix.json` 已失效，实际在 `reqs/fixtures/`）；**未指定 M2b 构建的期望矩阵文件与消费者脚本**，“一致”未定义。 |
| 3 | “故障注入报告通过” | **否** | 报告路径 `docs/m2b-fault-injection-report.md` 不存在；§14.6 未给报告字段 schema/复跑命令/版本；注入能力 `MBD_FAULT_INJECT` 在 `src/ tests/ ui/` 无实现（grep 0 命中）；真实演练仅 `NOT_APPLIED`（`docs/m2b2b-status.md:38` 自述“写中被杀 UNKNOWN_APPLIED 仍由单测覆盖”）。 |
| 4 | “单元/集成全绿 + 真实 Gitea（可达时）自检全绿（不可达按 14.7 SKIP 并记录）” | **否** | “全绿”无门禁命令/阈值/AC 映射；SKIP 即使 DoD 成立（见 §3.1），在不可达环境可零真实 Gitea 证据通过；`docs/m2b2b-status.md:25` 的 388/388 是测试计数，不是 AC 证据；`artifacts/` 被 `.gitignore`（:13）排除，证据不随仓库版本化。 |
| 5 | “独立 Gate（需求/代码/UX）签字；M2=M2a∧M2b” | **否** | 未定义签字产物（文件/字段/commit/时间）、Gate 判据；冻结记录 §5.4 仍“待填”（行 3114–3123）。 |

**小结**：§14 的 9 节中，14.8 可视为已关闭；14.1/14.3/14.4/14.5 为“部分映射”；14.2 大部分未映射；14.6/14.7/14.9 无法形成可判定证据。**§14 新增要求中至少 11 个具体行为点没有任何 AC 断言**（2.1 表“缺口”列）。

---

## 3. 测试环境门禁与故障注入/G-WIRE 的证据口径是否可复现

### 3.1 Gitea 门禁（“不可达→SKIP”）

| 检查点 | 现状（证据） | 结论 |
|---|---|---|
| 门禁规则 | spec 3191–3197；`docs/testing-gitea-gate.md:7–14`；`ui/scripts/lib/gitea-gate.mjs`；`ui/scripts/run-selfchecks.mjs` | 规则存在，但**spec 未定义 `result.json` 路径/字段**（仅 docs 行 14 给了 `{skipped,reason,url,hint}`） |
| SKIP 是否写 result.json | `gitea-gate.mjs:66` 在**单脚本被调用时**写；`run-selfchecks.mjs` 在 Gitea 不可达分支（:40–54）只打印 JSON、**不写 `result.json`**，最后仍输出 `ALL_RUNNABLE_SELFCHECKS_PASSED`（:57） | **口径不一致**：spec/文档要求写，聚合器未写 |
| SKIP 的语义 | exit 0 + “`ALL_RUNNABLE_SELFCHECKS_PASSED`” | **SKIP 与 PASS 不可区分**；在不可达机器上 M2b 可凭 0 条真实 Gitea 证据满足 DoD-4 |
| 探测范围 | 仅 `GET /api/v1/version`（200 即“可达”），2500ms 不重试（`gitea-gate.mjs:26`） | 不验证 token 权限、组织存在、目标数据（4 仓/组件）就绪；token A 仅 `write:repository` 时 `/user`、`/orgs` 403（`docs/real-gitea-test.md` §1） |
| 环境可复现性 | 默认真实地址 `192.168.50.20:3000`（spec 3193）；实际组织 `RTCRepos`、15 个手工仓（`docs/real-gitea-test.md` §2），而 AC Given 写组织 `MBD`、`CF325F_ChanganSigProcgBascFct_*`（如行 2197/2277/2437）；无 seed/reset 脚本，测试仓“保留在服务器供复查” | **AC Given 与唯一真实环境不一致**，且服务端状态会被前序测试改变（tag/push/delete），不可重复 |
| 覆盖优先级 | spec 只列可覆盖变量；`gitea-gate.mjs` 代码 `PROBE_URL || URL`（:19–20），其注释却写 `URL > PROBE_URL`（:8），docs 写 PROBE 优先（:9–10） | 三处口径不一致（注释/代码/文档） |
| 证据是否可审计 | `artifacts/` 整体进入 `.gitignore`（:13）；`git ls-files artifacts` 为空；真实自检 `cdp-m2b2-realhost-check.mjs` 依赖本机 Host+CDP 9222 | 第三方 checkout 后**无法复跑/复核**既有“证据” |

**结论**：§14.7 的 SKIP 门禁在工程上是“让测试不红”的合理做法，但作为 **DoD 证据门禁不可接受**：不可达 = 无证据 = 不得计为通过。需要把“SKIP”降级为 `NOT_RUN` 并在 DoD 中显式阻断/需人工签字，同时定义 `result.json` schema、聚合器写盘、真实环境 seed/reset 与权限预检。

### 3.2 故障注入 / G-WIRE

| 检查点 | 现状 | 结论 |
|---|---|---|
| `MBD_FAULT_INJECT` | spec 3185 要求；`git grep MBD_FAULT_INJECT` 在 `src/ tests/ ui/` **0 命中**；`tests/.../M2b2bAuditJournalTests.cs:719` 只是单测内的“抛异常”替身 | **未实现**，无法按 §14.6 复跑 |
| 注入点覆盖 vs AC 需要 | §14.6 列 6 类（git 网络/index.lock/Directory.Move/磁盘写失败/进程崩溃写前写后）。AC 额外需要：磁盘余量伪造（OP-02）、慢克隆≥2s（OP-26）、取消/杀进程时点（OP-27/SAFE-09）、外部改 HEAD/index 的 CAS 竞态（OP-15③/SAFE-08⑤）、文件占用（OP-23 有 Directory.Move 覆盖但无 API）、旧 tag 移动（OP-13） | **注入点清单不足以支撑 56 条 AC**；且没有“注入点→AC”的映射表 |
| G-WIRE | spec §1.6/3187 定义 WireMock.NET 契约夹具 + 请求记录；`Directory.Packages.props` 无 WireMock 包；`src/ tests/ ui/` 无 G-WIRE 实现；`AC-SAFE-04` 还假设 G-WIRE 能拦截 git smart-HTTP 写请求（远端零写），未定义代理方式 | **资产不存在且语义未定义**（契约 REST 与 git 协议混用） |
| 故障注入报告 | spec 3188 指定 `docs/m2b-fault-injection-report.md`；该文件**不存在**；无模板/字段/复跑命令；`docs/m2b-ac-op-gap.md:41` 自述 AC-SAFE-01…13=PARTIAL、“完整故障注入报告”缺失；`docs/m2b2b-status.md:38` 自述写中杀进程 `UNKNOWN_APPLIED` 仅单测覆盖 | **证据物缺失**；DoD-3 当前不成立 |
| 证据可复现性 | 报告要求“可复跑”，但没有：(a) 构建/开关说明，(b) 注入 JSON 样例，(c) AC→注入点→期望→实测的机器可读台账，(d) 运行命令与版本/commit | 不满足“可复现”最低条件 |

### 3.3 夹具与数据集资产

| 资产（spec 声明） | 声明位置 | 仓库现状 | 影响 |
|---|---|---|---|
| `F-*` 本地 git 夹具（25 个，`manifest.json`+`expected.json`，生成器 `v0.2/fixtures/`） | 行 1436/1466/1535/1788/1871/3100 | **随 61c27b6 删除**：`reqs/v0.2/fixtures/README.md`、`expected.json`、`state-matrix.ps1` 全部删除（git show --stat 可证）；仅剩附录 C 表（行 1318–1354） | 25 条依赖 `F-*` 的 AC 无夹具可起；`AC-STA-01` 要求的“与 state-matrix.ps1 常量一致”（行 1881）无法核验 |
| `G-E2E`（gitea.exe + E2E-NORMAL/STRESS） | 行 1534、§1.6 | **不存在**；无 gitea 二进制、无数据集生成器；只有 `artifacts/m1-fixture/fake-gitea.mjs`（M1 mock）与手工真实环境 | 16 条 E2E AC 无法复跑；容量/性能类断言（1,000/4,000 组件）无数据源 |
| `C-SYNTH` 合成数据集 | 行 1536 | 不存在 | 性能 AC（BOARD/SCAN/SYNC 等，不在本次 56 条内）无数据 |
| `CLK`（`IClock` 假时钟） | 行 1537 | 无全局 `IClock`；仅 `TimeProvider`/单测 `ManualTimeProvider` | OP-22/28/30 的“推进 CLK”不可按口径执行 |
| `m2-matrix.json`（14×5 单源） | 行 1082/1096/1224（旧路径 `reqs/v0.2/fixtures/`） | 实际 `reqs/fixtures/m2-matrix.json`；spec 路径失效；文件 `status:"aligned"` 与 freezeNotes“SINGLE_SOURCE_PENDING_CODE_ALIGNMENT”自相矛盾；UI 另有第二份手写矩阵 `ui/src/mocks/workbenchMock.ts:663` | 影响 AC-UI-08 与 DoD-2；**M2b 构建的期望矩阵未指定** |

---

## 4. 删除旧 AC 目录/评审后的“证据物/夹具/追溯矩阵缺失”

### 4.1 删除事实（commit `61c27b6`，工作区 clean）

被删除且曾作为规范输入的资产：

- `reqs/v0.2/ac-catalog.md`（1715 行，162 AC 的原始唯一权威）
- `reqs/v0.2/fixtures/{README.md,expected.json,state-matrix.ps1}`
- `reqs/v0.2/freeze-checklist.md`、`reqs/v0.2/diagrams.md`
- `reqs/review/01…08`（含 `06-redteam-srs.md`、`07-freeze-review.md`）
- `reqs/review-report.md`、`reqs/requirements.md`、`reqs/archive/*`、原始 docx/drawio
- `reqs/v0.2/fixtures/m2-matrix.json` → 移动为 `reqs/fixtures/m2-matrix.json`

### 4.2 合并稿中仍指向已删资产的引用（合并未完成）

| spec 位置 | 引用内容 | 指向 | 后果 |
|---|---|---|---|
| 10 | “历史评审与原始 docx/drawio 已按要求删除” | — | 与附录 B 自相矛盾（B 仍逐条列这些文件为资产） |
| 1031、1037、1071、1201、1222、1305 | “权威 AC 目录 = `reqs/v0.2/ac-catalog.md`”“冲突时以 AC 目录为准”“映射见 ac-catalog §1.4” | **已删除** | 权威链指向空文件；与本文件行 3/9“唯一需求源=本文件”冲突 |
| 1032/1033 | “待回写 `ac-catalog.md`”“回写完成前以 §13.1.1/§13.1.2 为权威” | 同上 | 回写目标不存在；临时口径变成永久双源 |
| 1419、1420、1507、1527 | 依据/强制输入=`review/07-freeze-review.md`、`06-redteam-srs.md` 第 93–115 行 | **已删除** | §1.5 的 15 行“不可判定→已处置”表、§4.2 的“37 条已关闭”均**不可核验** |
| 1436、1466、1535、1788、1871、3100 | `v0.2/fixtures/` 生成器、`manifest.json`/`expected.json` | **已删除** | AC Given 无法按规则起；§5.3 检查项 4 不可执行 |
| 1881 | 一致性对照 `D3-c/D3-d` + `fixtures/state-matrix.ps1` 的 `AggregatePriority` | **已删除** | AC-STA-01 的“C-03 资产未回写”判据失去对象 |
| 1306、1307、1308、1309、1310、1311、1312、1313 | 附录 B 资产索引逐条列 ac-catalog/diagrams/fixtures/freeze-checklist/review/archive/docx | **已删除** | 资产索引整体失真 |
| 3125 | “与 `v0.2/freeze-checklist.md` … 二者需同时签收” | **已删除** | 冻结签收流程不可执行 |
| 1096、1224、1082 | `reqs/v0.2/fixtures/m2-matrix.json` | 实际在 `reqs/fixtures/` | 路径失效（测试 csproj 已改用新路径，spec 未改） |
| 1276、1298 | `reqs/review-report.md` | **已删除** | 附录 A“详见…”悬空 |

### 4.3 追溯矩阵缺口

1. **Annex A §3（行 2896–2975）只覆盖 72 FR / 162 AC**；合并稿实际有 **104 条 FR**（§9.1–9.11 72 + §9.13–9.17 32）与 **173 个 AC 头**（162 + §2.12 的 2 条 v1.1 + §13.1.2 的 9 条），另有 §13.1.1 表格式 `AC-UI-01…14`（行 1073–1088）。**计数与覆盖范围自相矛盾**（行 1423/2898 仍写“72 FR/162 AC”）。
2. **§14 的 9 节无任何追溯行**：FR→AC 矩阵（§3）与 §13.4 都只到 v0.3.1；§14 新要求散落在 AC 之外的文字里，**没有 AC 编号、没有 FR 映射**。
3. **AC-UI-01…14 无 6 字段 G/W/T 正式条目**（只有表格叙述，无测试层级/依赖、无夹具 ID）；`AC-UI-10c` 有正式条目（§13.1.2），但 Annex A §3 不含 UI 域，DoD-1 却要求 `AC-UI-10c` COVERED → 追溯矩阵与 DoD 口径不一致。
4. `AC-SAFE-14…17`、`AC-OP-33`（§13.1.2）不在 Annex A §2.9/§2.7，也不在 §3 矩阵；§13.4 只在 FR-SAFE/FR-OPS 行以“…17”“AC-OP-33”提及，无逐条映射。
5. §1.5（15 行）/§4.2（38 条 R）以已删评审为证据，属“**结论保留、证据删除**”，不可作为冻结依据。

### 4.4 冻结记录与证据物

- §5.4 冻结记录全部“待填”（行 3114–3123），文件哈希/日期/签收人空白 → 当前文档不可能已完成 M0 冻结。
- 唯一的逐 AC 台账 `docs/m2b-ac-op-gap.md` 不是规范文件，且自述 **M2b 未完成**、多项 GAP（:3–4、:26、:30、:38–41）。
- §14.6 要求的故障注入报告、§13.1.2 要求的 `docs/m2a-ac-report.md`（M2a 存在）之外，**M2b 没有对应 AC 报告**（仅 §14.6 一个报告路径且不存在）。
- `artifacts/` 被 gitignore（:13），真实环境/自检证据不随提交版本化。

**结论**：删除旧 AC 目录/评审后，出现的是**实质性的证据物/夹具/追溯矩阵缺失**，而非仅是“历史归档整理”：
- 权威 AC 目录被删但合并稿仍以其为唯一权威；
- 25 个 `F-*` 夹具只剩表格、生成器/expected 全删；
- §1.5/§4.2 的可测试性处置证据被删，无法复核；
- 追溯矩阵未覆盖 v0.3/v0.3.1/v0.4 新增的 FR/AC/§14 要求；
- DoD 要求的报告多处不存在。

---

## 5. P0 / P1 / P2、结论与最小修改建议

### 5.1 结论

**需先修（不可冻结）**。理由：存在 **8 项 P0 文档/证据级缺陷**（权威链断裂、AC 与 §14 冲突、无注入钩子的时序断言、G-WIRE/G-E2E/F-* 资产缺失、§14 大量要求无 AC、SKIP=pass 门禁漏洞、DoD 不可判定、错误码未冻结），以及 45 条 AC 的夹具/二义问题。按当前文本，第三方无法独立、可复现地判定 DoD-1…5 中任意一条成立。**最小可行路径**：先完成 5.3 的 10 项修改（预计 1–2 天规格编辑），再冻结 AC 并重跑证据。

### 5.2 P0 清单（8 项）

| # | P0 问题 | 证据 | 影响 |
|---|---|---|---|
| P0-1 | 权威链断裂：§13.1 仍指已删除的 `ac-catalog.md` 为唯一权威；§1.5/§1.6/§5.3/附录 B 引用已删评审与夹具生成器 | spec:1031/1037/1201/1222、1419/1420/1507、1436/1535、1305–1313、3125；commit 61c27b6 删除清单 | 无法确定“哪份 AC 是验收基线”，冲突裁决指向空文件 |
| P0-2 | AC-OP-12 与 §14.4 的 baseline 写入语义冲突（写 4 行 vs 失败/跳过不写），§14.4 自身也未定死 | spec:2287 ③ vs 3171/3173 | DoD-1 的 AC-OP-12 无唯一判定 |
| P0-3 | AC-OP-23 与 §14.2 错误码/证据口径冲突（`FS-E001` 前缀 vs `E_DELETE_PENDING`），pending JSON/仅重试 pending/取消 pending 无断言 | spec:2375 vs 3149–3151 | AC-OP-23 无唯一判定；§14.2 大部分未验证 |
| P0-4 | AC-OP-02 自相矛盾（任一失败阻断 vs 其余仓可继续）+ 磁盘余量注入无手段 + 码值二选一 | spec:2202 vs 2207 ①④ | 不可执行 |
| P0-5 | 时序/并发/终止断言无确定性钩子：OP-26/27、SAFE-09；违反 §1.1-4（墙钟）；`MBD_FAULT_INJECT` 未实现、报告缺失 | spec:2397–2399、2407、2609–2611、3185–3189；grep 0 命中；`docs/m2b2b-status.md:38` | 三条 AC 不可复现；DoD-3 不成立 |
| P0-6 | 外部证据采集方法未定义：SAFE-13 抓包/argv/服务端日志；SAFE-07 的 N/`dumps\` 根/两分支并存；SAFE-10 进程监控 | spec:2593–2595、2619、2640–2643 | 4 条 AC 不可复现/无唯一期望 |
| P0-7 | 依赖资产不存在：`G-E2E`（16 AC）、`G-WIRE`（3 AC）、`F-*` 生成器（25 AC）、`C-SYNTH`；`CLK` 无全局实现 | §3.3 | 除少数纯本地可构造项外，56 条 AC 均无法完整一键执行 |
| P0-8 | §14.7 SKIP=pass 漏洞 + DoD 不可判定（COVERED 无定义；result.json 聚合器不写；M2b 矩阵无期望源；签字物缺失；错误码 R-06 未冻结） | spec:3014、3191–3197、3203–3211；`run-selfchecks.mjs:40–57`；`gitea-gate.mjs:8,19–20,66`；`m2-matrix.json:26,34` | M2b 可在无真实证据下“通过”，DoD 失去约束力 |

### 5.3 P1 清单（10 项，按影响排序）

| # | P1 问题 | 涉及 AC |
|---|---|---|
| P1-1 | 16 条 `Then` 含“或/若/按规则/按设计”二选一或条件期望，须定死唯一期望值 | OP-02/05/07/17/18/22/25/32、REPO-03/06/08/09、SAFE-07/08/09/13 |
| P1-2 | 17 条 `Given` 无夹具/依赖代号；违反 §1.1-5、§1.3 | OP-14/23/26/27/29/32、REPO-07、SAFE-02/03/05/06/07/08/09/10/11/12 |
| P1-3 | 错误码双体系（附录 D `*-E0xx` vs Host `E_*`/`details.srsCode`）且 R-06 未冻结；AC-OP-04 “GIT 前缀”不成立 | OP-04、OP-23、OP-02、SAFE-13、DoD-1 |
| P1-4 | 组件级（4 仓）夹具未定义；`F-*` 均为单仓，`expected.json` 已删 | OP-06/09/11/12/15/16/17/21/25/31 |
| P1-5 | 真实环境与 AC Given 不一致（`RTCRepos/RTCT_*` vs `MBD/CF325F_*`），无 seed/reset，服务端状态漂移 | OP-01/03/04/11/12/25/31/32、REPO-04/05 |
| P1-6 | 备份 ref 路径格式与实现不一致（`<ts>/<branch>` vs `<ts>/<branch>/<TYPE>`），AC-OP-15/16/30 断言不可对拍 | OP-15/16/30 |
| P1-7 | 清理/保留触发入口未定义（OP-22/30 的清理任务；audit/backup 保留），CLK 不可全链注入 | OP-22/28/30 |
| P1-8 | §14.1/14.3/14.5 的具体行为无 AC（fail-closed、MISSING/BINDING_ERROR repair、RECLONE 强确认/移动失败不 clone、pre/post HEAD、undo 四守卫、远端未删、CREATE_UNDO 审计、失败零改动） | OP-31/32、REPO-08、OP-25 |
| P1-9 | §14.2 pending 文件/任务中心文案/仅重试 pending/取消 pending/不删锁不递归 无 AC；AC-OP-23 仅覆盖 1/6 行为 | OP-23 |
| P1-10 | 门禁/证据工程口径不一致：`run-selfchecks.mjs` SKIP 不写 `result.json` 且输出 PASSED；probe 优先级三处不一致（spec/注释/代码）；`artifacts/` gitignore；`m2-matrix.json` 自述状态矛盾 + UI 第二份手写矩阵 + 无 M2b 消费者 | AC-UI-08、DoD-2、DoD-4 |

### 5.4 P2 清单（6 项）

1. `# Annex A` 重复标题（行 1409 与 1412）；`# 14` 重复编号（行 1230 风险登记 vs 3130 M2b 增量），单文件出现两个 §14。
2. 计数/范围陈旧：行 1423/2898/1031/1305 的“72 FR/162 AC/14 条 AC-UI”与现状（104 FR/173 AC 头/15 条 UI AC）不符；行 1032 写“AC-UI-01…AC-UI-15（§13.1.1）”而 §13.1.1 只有 01–14。
3. §1.1-6 说“测试层级**四**选一”，随后列了 5 个层级（单测/集成/E2E/UI 快照/性能）。
4. §1.2 行 1448 说“本版 162 条均为独立主编号，未使用子字母”，但 AC-UI-10a/10b/10c 为子字母编号（虽不计入 162）。
5. 行 1411 称 Annex A 含“AC-UI 等逐条 Given/When/Then”，实际 Annex A 无 UI 域；AC-UI 定义分散在 §13.1.1/§13.1.2。
6. 附录 B 资产索引（行 1293–1317）整体未按删除后的仓库更新（同 P0-1 的文档面）。

### 5.5 最小修改建议（按顺序，尽量少改）

1. **收敛权威源**：删除/改写 §13.1、§13.4 中“`reqs/v0.2/ac-catalog.md` 为唯一权威/冲突以其为准/待回写”等语句，明确“Annex A（含 §13.1.1/§13.1.2 引用）+ §14 AC 条款即唯一 AC 基线”；§1.5/§1.6/§5.3/附录 B 的已删资产链接改为“历史资产（已删除，仅归档说明）”，或恢复必要文件（见 4）。
2. **重算并补追溯矩阵**：§3 的统计改为 104 FR / 实际 AC 数；为 §14 的 9 节逐条新增 FR→AC 映射行；为 AC-SAFE-14…17、AC-OP-33、AC-UI-10c 增加追溯行；为 AC-UI-01…15 补“测试层级/依赖 + 夹具 ID”两列（可保留表格式，但必须含这两字段）。
3. **修冲突**：定死 `tag_present` 缺失仓写法（建议：仅成功项写行；缺 Tag 仓不写或写 `tag_present=0` 二选一，并同步 §14.4 与 AC-OP-11/12/14）；统一 DELETE_PENDING 错误码为附录 D 新码（M0 冻结）并让 §14.2/AC-OP-23 一致。
4. **补 §14.6 注入契约**：在 §14.6 增加“注入点→触发参数→受影响 AC”表，至少补：磁盘余量伪造、按仓 401/403/404/500、慢克隆/慢 pull（≥2s/≥3s）、第 N 仓时强制终止/取消、外部改 HEAD/index 的 CAS 竞态、文件占用 Move 失败、旧 tag 移动；并给 `MBD_FAULT_INJECT` JSON 样例与生产禁用断言。
5. **落地时序断言**：把 OP-26/27、SAFE-09 的墙钟/调度断言改为 `IClock`/可控注入点断言（如 `cancel_deadline=2s` 用假时钟推进）；§1.1-4 与 AC 文本同步。
6. **给 SAFE-07/SAFE-13/SAFE-10 定死观测法**：SAFE-07 二选一（建议 v1 默认关闭转储）并定义 `dumps\` 根与 N；SAFE-13 指定采集产物（G-WIRE 请求日志 + 进程 argv 快照 + remote URL 扫描报告）及格式；SAFE-10 指定进程监控实现（快照差分）。
7. **给缺失 AC 的 §14 行为补断言**：优先补 §14.2（pending JSON 六字段/仅重试/取消/不删锁）、§14.1（fail-closed）、§14.3（MISSING/BINDING_ERROR repair、RECLONE 强确认、失败零改动、pre/post HEAD）、§14.5（undo 四守卫、SKIP 原因、远端未删、CREATE_UNDO）。可新增 `AC-OP-34…38` 或扩展现有 AC 的 Then（需按 §5.4 规则走变更）。
8. **锁死错误码**：关闭 R-06（M0 出口条件），附录 D 补全“码/文案/可重试性/日志级别”，并在 Annex A 规定断言一律用 `srsCode`（`details.srsCode` 映射 Host `E_*`）；AC-OP-04 的“GIT 前缀”改为具体码。
9. **修门禁**：`run-selfchecks.mjs` 在 SKIP 时写 `result.json` 且最终输出 `HAS_SKIPPED`（非 PASSED）；DoD 中把 SKIP 记为 `NOT_RUN` 并要求 Gate 签字；统一 probe 优先级（spec/docs/code 三处一致）；真实环境增加 org/token/数据就绪探测与 seed/reset 脚本（或明确“仅人工验收、不计 DoD”）。
10. **给 DoD 加可判定定义**：定义 `COVERED` = “该 AC 每条 Then 有自动化断言且证据文件/命令/commit 可复跑且通过”；给出 `docs/m2b-ac-report.md` 模板（AC/断言/证据路径/复跑命令/commit/状态）；DoD-2 指定 M2b 构建矩阵文件与消费者脚本；DoD-5 定义签字产物（Gate 记录表含人/日期/commit/结论）。

---

## 6. 结论与 Top 8

**结论：需先修，暂不可冻结。** AC 文本结构完整（56/56 六字段、无禁止词），但可测试性被三类问题阻断：**(a) 规范引用链断裂**（唯一权威 AC 目录/夹具生成器/评审证据被删且未清引用）、**(b) AC 与 §14 及实现口径冲突/期望不唯一**（17 条无依赖代号、16 条二选一、2 条直接冲突）、**(c) 证据基础设施缺失**（G-E2E/G-WIRE/F-*/C-SYNTH/故障注入/报告均不可用，SKIP 门禁把“无证据”变成“通过”，DoD 5 条均不可客观判定）。

**Top 8 不可测/缺证据项**

1. **权威 AC 源与夹具生成器已删**：spec 仍指 `reqs/v0.2/ac-catalog.md` 为唯一权威（1031/1037/1201/1222），`v0.2/fixtures/`、`review/06`/`07`、`freeze-checklist` 全被 61c27b6 删除；25 个 `F-*` 只剩附录 C 表。
2. **AC-OP-12 ↔ §14.4 冲突**：baseline 写 4 行（含跳过仓）vs “失败/跳过不写”，且 §14.4 自身“写 0 或不写行”未定死（2287 vs 3171/3173）。
3. **AC-OP-23 ↔ §14.2 冲突**：`FS-E001` vs `E_DELETE_PENDING`；pending JSON/仅重试 pending/取消 pending/不删锁/不递归均无断言（2375 vs 3149–3151）。
4. **时序/终止类不可复现**：OP-26/27、SAFE-09 的“≥2s/≤2s/第 2 仓杀”无注入点、违反 §1.1-4；`MBD_FAULT_INJECT` 无实现、`docs/m2b-fault-injection-report.md` 不存在（3183–3189）。
5. **SAFE-13/07/10 观测法或期望不唯一**：抓包/argv/服务端日志、N/`dumps\` 根、“默认开启/关闭”双分支、进程监控方法均未定义（2593–2595、2619、2640–2643）。
6. **依赖资产不存在**：16 条依赖 `G-E2E`、3 条依赖 `G-WIRE`、25 条依赖 `F-*`、性能依赖 `C-SYNTH`；`G-WIRE`/`G-E2E`/`C-SYNTH` 在仓库 0 实现，`CLK` 无全局假时钟。
7. **§14 大量要求无 AC**：14.1 fail-closed/差异提示、14.2 pending 六字段/仅重试/取消、14.3 MISSING/BINDING_ERROR repair/强确认/零改动、14.5 undo 守卫/远端未删/审计；§14 无追溯行（§3/§13.4 均未覆盖）。
8. **门禁与 DoD 不可判定**：SKIP 即 `ALL_RUNNABLE_SELFCHECKS_PASSED` 且聚合器不写 result.json；真实环境 `RTCRepos/RTCT_*` 与 Given `MBD/CF325F_*` 不符、无 seed/reset；`m2-matrix.json` 自述 `aligned` 与 `PENDING_CODE_ALIGNMENT` 矛盾且无 M2b 消费者；DoD 的 COVERED/全绿/签字均无定义。

---

### 附：评审核对方式（只读）

- AC 字段/编号/禁止词/依赖 token/二选一：脚本解析 `reqs/specification.md` 2192–2644 与 `^#### AC-` 头。
- 资产存在性：`git ls-files`、`git log --diff-filter=D`、`git show --stat 61c27b6`；全仓 grep `MBD_FAULT_INJECT|G-WIRE|FaultInjection|WireMock`。
- 证据状态：`docs/m2b-ac-op-gap.md`、`docs/m2b2b-status.md`、`docs/m2b2-ux.md` 等既有评审记录（仅作旁证，不作为规范）。
- 未运行构建/测试（评审只读）；凡“不可复跑”均指按 spec 声明无法从仓库独立复现。



