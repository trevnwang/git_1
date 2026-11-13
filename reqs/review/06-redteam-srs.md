# 红队复审报告 06 — MBD 组件全生命周期 Git 工具 SRS v0.1（对抗式）

- 评审性质：**只读对抗性复审**。未修改 `reqs/requirements.md`、`review-report.md` 及任何被评审文件；本报告仅新建。
- 评审对象快照：`reqs/requirements.md`，**784 行 / 64,211 bytes / SHA256 `BB589B3A…DB6A7EE` / 2026-11-13 23:57:49+08**；`reqs/review-report.md`（142 行）；`reqs/review/01..05`；`reqs/review/03-architecture.md`（复审期间落盘，400 行）；`reqs/archive/`。
- ⚠ 并发修订提示：复审期间 `requirements.md` 已被其他角色改写过一次（53,801→64,211 bytes，新增 D-011/D-012/D-013、7.5 分层 SLO、8.1 schema、12 章架构并入）。本报告以上述哈希快照为准；若最终文件哈希变化，所有行号需复核，但**章节级结论不受影响**。
- 判定口径：**阻断** = 不修正会直接导致返工/数据风险/验收无法判定；**高** = 首版大概率翻车或验收扯皮；**中** = 一致性与维护性问题，可排 v1.1 但应记录。

---

## 红队结论（能否进入开发；3 句内）

1. **不能以当前 SRS 作为 M0/开发验收基线。** 审核报告称 42 个 P0 必须全关（原 33 + 架构 9），红队判定其中**至少 17 个未被可执行地关闭**（原 33 中 9 个假关闭 + 6 个部分关闭；ARC 中 ARC-05/ARC-08 仍有缺口），同时 AC 仅 15 条/65 条 FR 且自认“节选，开发前需补全”，§15 仍有 OQ-02、OQ-03 两个“阻塞=是”未决，文档头却写“可进入开发排期”。
2. **核心三件套仍不闭合：** §7.4 聚合表缺 4 个主状态的映射与兜底、§11.2 色板缺约 16 类状态的视觉定义、§6.2 与 §7.4 对 INVALID 的归属互相矛盾；照此进入开发，状态计算、看板视觉、验收用例三处必然返工。
3. **建议先做一轮“冻结修订”再复审，不要直接进 M0。** 修订内容＝补全 AC 与追溯矩阵、状态×操作矩阵、26 状态色板、数据契约与 DDL 对齐、关闭 OQ-02/03、删除重复附录（约 3–5 人日）；若业务必须启动，只能做“M0 UI 骨架 + Mock”，且不得以本 SRS 作为验收依据。

---

## 假关闭的 P0

> 33 个原 P0 = 01 的 RQ-01…08（8）+ 02 的 G-P0-01…05（5）+ 04 的 UX-01…10（10）+ 05 的 P0-01…10（10）。下表列出**未真关闭的 15 个**（9 个假关闭 + 6 个部分关闭），其余 18 个可接受但受 OQ-02/03 影响。

| 原 P0 | requirements.md 位置 | 为什么没真关闭 | 必须补什么 |
|---|---|---|---|
| RQ-02（假关闭·聚合侧） | §7.2、§7.4、§6.2 | 7.2 有 14 主状态，但 7.4 只有 12 个组件态：EMPTY、DETACHED_TAG、DETACHED_COMMIT、NO_UPSTREAM 四个主状态**没有聚合映射**，也没有“默认兜底”行（违反 FR-STA-01“有默认兜底”）；§6.2 说 INVALID“不纳入聚合”，§7.4 又说“任一仓 INVALID→BINDING_ERROR”；14≠12 的数量差未解释。 | 7.4 补映射与 `UNKNOWN` 兜底（见“建议修订”2）；统一 6.2；AC 覆盖 26 个状态组合。 |
| RQ-03 / UX-01（假关闭） | §11.2、§10.5、§7.3 | 色板仍只有 8 行，缺 INVALID/EMPTY/UNMERGED/IN_PROGRESS/DETACHED_COMMIT/DIRTY 六个主状态与 BINDING_ERROR/BLOCKED_LOCAL/TAG_DRIFT/MIXED_BRANCH/OP_PARTIAL_FAILED 等聚合态；DETACHED_TAG 只有描边无底色；DIRTY 无颜色且与“SYNCED 唯一绿色”无法自洽；NO_UPSTREAM `#0E7490/#E3F4F7` 对比度约 **4.35:1**、BEHIND 约 **4.49:1**，低于 §10.5 声明的 4.5:1；§11.2“红色只允许 CI FAIL 与破坏性按钮”与 §7.3 `lfs_missing`“红色告警”冲突。 | 补全 26 状态 token 表 + 自动对比度校验；改写红色纪律；深色主题要么补需求/色板，要么从 §13.2 测试项删除。 |
| RQ-04 / P0-04（假关闭） | §8.1、附录 A、§12.4 | 表数仍不实：8.1 为 12 个行项但 `job`/`job_item` 是两张表（**实为 13 张**），附录 A 两处仍称“12 表”；`baseline_snapshot` 行同时写“v2 预留”和“v1 由 JOB_RUNNER 记录”；§12.4 承诺预留 `event_log`，8.1 无此表；notification、scan_issue、binding_override 无表；`repo` 的“唯一写者=REMOTE_SYNC”与 §6.3 local_path/is_local 由本地发现产生矛盾。 | 冻结表清单并逐字段写者；`baseline_snapshot` 定 v1 写；补 notification/event_log/scan_issue/binding_override 或明确不做；local_path 写者改 LOCAL_SCAN 或拆 `repo_local`。 |
| RQ-07（假关闭） | §13.1、§10.6、§9.9、§9.10、§3.3 | AC 仍只有 15 条且标题明写“节选，开发前需补全”；§9.9 FR-SAFE 与 §9.10 FR-OPS 两表**没有“验收”列**（12 条 FR 零 AC）；§10.6 称“HLR→FR→AC 矩阵见第 13 章”，第 13 章无矩阵；65 条 FR 中直接有 AC 的约 20 条；§3.3 M3 出口“第 13 章 AC 全绿”因此是移动靶。 | 出全量 AC + 追溯矩阵 + 门禁（无 AC 不进 M2）；M3 改为“AC 基线冻结版本全绿”。 |
| RQ-06 / UX-06 / ARC-08（部分关闭） | §9.11、§11.4、§8.1、§12.4 | CI 锚点看似完整，但契约与 DDL 不一致：`CiLatest` 有 branch/durationMs/fetchedAt，`ci_status` 表无这些列；`ci_latest` 视图在 CI-9 被提及但 8.1 未定义；通知中心无持久化表（CI-6 自述“通知 schema 后加要迁移”却不在 v1 建）；`event_log` 只在 12.4 口头承诺。 | 把 CiLatest 字段落到 DDL 或写明视图定义；建 notification/event_log 空表；CI 的 token/权限/新鲜度展示纳入 §12.6 与锚点。 |
| RQ-08 / UX-09（部分关闭） | §9.9、§9.7 FR-OP-05/06、§12.6、OQ-04 | FR-SAFE 无验收列；“可恢复”只有一句：删除保留期**三处冲突**（FR-OP-06 7 天 / §12.6 30 天 / OQ-04 30 天）；从 `.trash` 或备份 ref **恢复操作没有 FR/AC**；`.trash` 清理任务、扫描排除、回收站 vs `.trash` 二选一未定；备份 ref 无保留/清理/UI。 | 新增“恢复”与“备份/软删生命周期”FR；统一 7/30 天；定义清理任务与扫描排除；AC-07/08 增加恢复步骤断言。 |
| G-P0-03（假关闭） | §7.2、§15 OQ-02 | DETACHED/BEHIND/AHEAD/DIVERGED/DIRTY 的判定仍使用未定义的“ref”（未绑定到 `refs/remotes/<remote>/<branch>`），比较基准 OQ-02 仍“阻塞=是”；§7.2 前置句“fetch 成功且数据新鲜否则 UNKNOWN”又使离线场景无法给出本地状态。 | 定义 compare_ref 生成规则（默认 upstream 同名分支）并关闭 OQ-02；把“ref”替换为显式 ref；离线可判定状态集写入 7.2。 |
| G-P0-05 / 基线（假关闭） | §7.4 TAG_DRIFT、§8.1、§9.12、AC-10 | TAG_DRIFT 依赖“基线模式”，全文未定义该模式的进入/退出/是否 v1 启用；`baseline_snapshot` 行内 v2/v1 自相矛盾；四仓缺 tag 时快照行（PK 含 repo_type）如何表达“缺失”未定义；AC-10 期望 TAG_DRIFT，但 7.4 无 DETACHED 聚合映射，按现有表**推不出**该结果。 | 定义基线模式或改为无条件 TAG_DRIFT；定义缺失 tag 的快照语义；AC-10 给出可推导的期望（三仓 detached + 一仓缺 tag）。 |
| P0-05（假关闭） | §7.4、§11.2、§15 OQ-03 | 聚合优先级缺 EMPTY/DETACHED/NO_UPSTREAM 与默认兜底；MISSING 优先级 3 高于 DIVERGED(6)/BEHIND(7)，与 §11.2“缺失是中性非错误（4,000 仓多数未克隆）”冲突；OQ-03 未决意味着缺仓可能是常态，BINDING_ERROR 优先级 1 会让大量组件常驻最高级，“异常优先”排序失去意义。 | 补行并以 UNKNOWN 兜底；把“严重度/排序”与“聚合状态”拆开；M0 前拍板 OQ-03。 |
| P0-08 / UX-10（部分关闭） | §9.7、§7.2、§9.8 | 有批量模型，但无“14 主状态 × 8 操作”的适用/阻断矩阵；EMPTY 行“允许初始化/推送首个 commit”与 FR-REPO-02“Push 需有 upstream”**死锁**；DETACHED“允许建分支”、NO_UPSTREAM“修复 tracking”、INVALID“仅修复”均无 FR/AC；FR-OP-08“合作式取消”未定义哪些操作可取消、取消后工作区/任务态；FR-OP-07 CREATE 未定义基线 ref 与撤销条件。 | 补状态×操作矩阵与 repair/set-upstream/create-from-detached 的 FR；定义取消状态机与撤销条件；补 AC-09 延伸用例。 |
| RQ-05 / P0-01 / ARC-02（部分关闭） | §12.1、§7.5、§8.1 | 2 进程模型已并入 D-011，但 §12.1 表写 Host“唯一可发网络请求”，GitWorker 职责却包含 clone/fetch/pull（**全是网络请求**），自相矛盾；§7.5/§9.7 写前强制 fetch 由谁执行、由谁更新 `remote_ref`/DB 未定义；唯一写者规则与 GitWorker 不回写 DB 的衔接缺失。 | 改为“Host 唯一持有凭据并发起 API 请求；git 网络命令由 Host 下发、GitWorker 执行并回传”；定义 fetch 完成→REMOTE_SYNC 更新 remote_ref + generation 的链路。 |
| UX-08 / P0-09（假关闭） | §3.1、§11.1、AC-11、NFR-PERF-09 | 首扫阶段三处不一致：§3.1“双阶段（远程/本地）”、§11.1“远程→本地→计算”三阶段、AC-11“双阶段”；§10.1 又要求“先本地后远程、渐进渲染”；未定义远程元数据未就绪时本地仓如何判定（INVALID/UNBOUND？）以及远程就绪后是否强制重判（P0-09 原问题）。 | 统一为三阶段（本地发现→远程对账→状态计算，可跳过降级），定义就绪门控与“重判”FR，AC-11 按阶段与 total 断言。 |
| RQ-01 / P0-07（部分关闭） | §6.1、§6.2、FR-SYNC-05、§11.4 | 主规则已定，但 6.1 例外“`_SIL_Design` 不参与绑定”与“从最后一个下划线切分”冲突（按规则会绑定为组件 `X_SIL`）；“远程 >4 仓交用户决定收纳”在名称派生 ID 下无法表达（无 binding_override 表/FR）；FR-SYNC-05 的 renamed 无字段，rename 后 component_id 变化无迁移规则；CI 契约仍用 `componentPrefix`，违反 §5 术语表禁用别名与 D-009。 | 给例外判据（前缀不得以生命周期词结尾）；加 binding_override 或砍掉“收纳”；补 rename 迁移事件；契约改用 component_id。 |
| G-P0-04 / RQ-08（部分关闭·SYNC 侧） | FR-OP-05、§7.2、FR-REPO-02 | SYNC 细化后仍缺：detached HEAD 下 SYNC 目标分支未定义；逐目标跳过状态（UNMERGED/IN_PROGRESS/INVALID/EMPTY）未写；备份在 fetch 前/后未写（备份后 fetch 失败会留孤儿 ref）；UPSTREAM_GONE 依赖 `fetch --prune`，但全文无 prune/refspec 要求（不 prune 永远检测不到远程删分支）；恢复入口无 FR。 | 按 02 §3.3 补预检跳过表与顺序（备份→fetch→CAS→reset）；定义 fetch refspec/prune 归属；加“从备份 ref 恢复”FR。 |
| UX-05（假关闭） | §9.5、§13.1 | 搜索/筛选/排序仍只写“facet/排序/多选”三个词：facet 取值域、排序键（含默认“异常优先”的次序）、搜索字段的数据来源（branch/tag 仅同步“已绑定+关注”仓）均未定义；FR-BOARD-04“超阈值”无阈值；无搜索/筛选/排序 AC。 | 列出 facet 枚举与排序键表、定义 4,000 组件与 16,000 仓两种口径、补 AC（搜索字段/排序/多选执行）。 |

**另：03-architecture.md 的 9 个 ARC P0 已于复审期间并入 SRS**（D-011/D-012、§7.5、§8.2、§12.1–12.7、§10.4），其中 **ARC-05（数据模型）与 ARC-08（CI 数据契约）仍有上表所列缺口**；ARC-14（可测试性）的夹具/fake clock/基准门禁未并入（见 RT-15、RT-28）。

---

## 新发现的矛盾/缺口

| ID | 严重级 | 位置/引用 | 问题 | 后果 | 建议改写 |
|---|---|---|---|---|---|
| RT-01 | **阻断** | §7.2 前置句、§7.1/§7.3 “TTL”、§7.5、NFR-REL-01 | “数据超 TTL”和“fetch 未成功即 UNKNOWN”两处规则都**没有 TTL 数值**；且 7.2 前置句会把离线场景全部压成 UNKNOWN，与 NFR-REL-01“本地扫描仍可用”、§7.5“按数据源分别显示时间”冲突；哪些状态离线可判定（INVALID/MISSING/EMPTY/UNMERGED/IN_PROGRESS/DETACHED）未定义。 | 离线看板要么全灰（违背 REL-01），要么由实现自由决定；AC-05 无法判定；stale 提示与状态计算脱节。 | 增 TTL 表（本地指纹 15s→TTL 90s；fetch 10min→TTL 20min；远程元数据 6h→TTL 7h）；明确“离线可判定状态集”；7.2 前置句改为“关系类状态需 fetch 新鲜，其余按本地事实判定”。 |
| RT-02 | **阻断** | §7.4、§6.2、§11.2 | 聚合表只有 12 行、无 `else/UNKNOWN` 兜底，EMPTY/DETACHED_TAG/DETACHED_COMMIT/NO_UPSTREAM 四类仓同时存在时组件态未定义；6.2 与 7.4 的 INVALID 归属矛盾；MISSING 的“中性”定位与优先级 3 冲突。 | 组件聚合函数无法实现“穷尽互斥”（FR-STA-01 直接不可验收）；看板可能显示空白/假绿；缺仓常态下异常排序失真。 | 7.4 增 3–4 行 + 兜底行（见“建议修订”2）；6.2 INVALID 统一为 BINDING_ERROR；把“展示严重度/排序”独立成列。 |
| RT-03 | **阻断** | §11.2、§10.5、§7.3 | 色板覆盖不足（见假关闭 RQ-03）；对比度两处不达 4.5:1；红色纪律与 LFS 告警冲突；只有浅色值，深色主题却出现在 §13.2 测试项且 OQ-08 未决。 | 视觉回归/截图验收无法建立基线；a11y 声称不成立；实现者自选颜色，后续无法统一。 | 补全 26 状态色板（含深色 token）+ 自动对比度测试；改写红色纪律为“红=阻断/失败，含 LFS 缺失与破坏性确认”；OQ-08 定案。 |
| RT-04 | **阻断** | §7.2“允许写操作”列、§9.8 FR-REPO-02、§9.7 FR-OP-08 | 状态机承诺的写操作在 FR 层无实现路径：EMPTY 要“推送首个 commit”但 Push 要求 upstream；DETACHED“建分支”、NO_UPSTREAM“修复 tracking”、INVALID“仅修复”均无对应 FR/AC；“取消（合作式）”无取消语义。 | 状态机与功能需求互相打脸，开发只能自行发明；AC 无法覆盖。 | 补“从 detached 建分支”“set-upstream”“repair/re-clone”“EMPTY 首推”4 条 FR；定义 cancel 状态机（可取消操作集、取消后的仓态、是否计失败）。 |
| RT-05 | **阻断** | §13.1 标题、§9.9/§9.10、§10.6、§3.3 M3 | AC 自认“节选待补全”；两整张 FR 表无验收列；无 HLR→FR→AC 矩阵；M3 出口绑定一个未冻结的 AC 集。 | 验收无法启动；M3 永远无法“全绿”；范围无法裁剪。 | AC 全量 + 矩阵 + 门禁（见“建议修订”7）。 |
| RT-06 | **阻断** | §8.1、§11.4、§12.4、FR-OPS-01 | 契约/DDL 不一致：CiLatest 的 branch/durationMs/fetchedAt 无表列；`ci_latest` 视图未定义；notification/event_log 无表但被 FR/CI 锚点引用；`baseline_snapshot` 行内 v1/v2 矛盾；`repo.local_path` 写者错配。 | CI/通知/事件 v2 接入必迁移；基线快照 v1 无法落库；字段写者规则不可验证。 | 按“建议修订”5/6 对齐 DDL 与契约；删除重复附录并核对表数；补事件/通知表。 |
| RT-07 | **阻断** | §12.1 进程表、§12.2 | “Host 唯一可发网络请求”与 GitWorker 的 clone/fetch/pull 职责直接矛盾；fetch 结果如何回写 `remote_ref`/触发重算未定义（Host 唯一写 DB，但 GitWorker 是执行者）。 | 两进程通信/数据所有权无法冻结；实现时会出现“谁写 refs/谁更新 DB”的竞态或双写。 | 改写为“Host 唯一持有凭据并发起 Gitea API 请求；网络类 git 命令由 Host 下发、GitWorker 执行并回传结果”；补 fetch→REMOTE_SYNC 的更新链路与 generation 递增要求。 |
| RT-08 | **高** | §7.5 写前 read-through/CAS、§8.1 唯一写者、§9.7 互斥 | 写前强制 fetch 的写者、DB 刷新、generation 递增均未定义；CAS 只比较 HEAD，未覆盖 index/worktree，无法防止“预检后 IDE 改了工作区”。 | 并发保护名义存在、实际可被绕过；AC-07/AC-08 无法覆盖竞态。 | 明确 fetch 归属与 DB 刷新；CAS 增加 `HEAD+index-tree+worktree dirty 摘要`；与 §8.2 单写队列一致。 |
| RT-09 | **高** | §9.7 前言、FR-OP-02、FR-REPO-01/03 | 前言“所有写操作：预检→dry-run 预览→确认”与日常 Pull/Fetch/Commit 不适配；FR-OP-02/FR-REPO 未按该模型描述；互斥写的是“跨进程命名 Mutex”，而 §12.1 已是单 Host + 单 GitWorker。 | 实现者不知哪些操作要确认；日常高频操作被过度打断；互斥层级混乱。 | 区分“常规操作（无确认，仅预检）”与“破坏性操作（dry-run+确认）”；互斥改为“Host 内 per-repo 队列 + GitWorker 每仓锁 + 外部 index.lock 检测”。 |
| RT-10 | **高** | §6.1、§6.2、FR-SYNC-05、§11.4 | `_SIL_Design` 例外与切分规则冲突；>4 仓“收纳”无 binding_override 模型；rename 无字段/迁移；CI 契约 `componentPrefix` 违反术语表与 D-009；`archived/renamed` 只有 archived 字段。 | 组件身份边界不可测（AC-03 未覆盖）；改名场景必返工；CI v2 API 命名污染。 | 例外判据 + binding_override 表 + rename 迁移 FR + 契约统一 component_id。 |
| RT-11 | **高** | §6.3、FR-SCAN-01、§7.3、§12.6 | “非仓/不匹配目录跳过并记录”无落表；网络盘/reparse point 一处“跳过”、一处“只读展示”、7.2 又归 INVALID，三处不一致；submodule“只标记”无 flag；`safe.directory` 提示无验收；保留名/大小写碰撞只在 §12.6 设计层，无 FR。 | 扫描异常不可追溯；网络盘行为三种解释；Windows 保留名路径会让 Clone 失败且无需求依据。 | 建 `scan_issue` 表；明确“跳过=不纳管、只读=可展示禁写、INVALID=纳管后异常”三态；补 submodule flag 与路径合法性 FR/AC。 |
| RT-12 | **高** | §7.2 NO_UPSTREAM/UPSTREAM_GONE、FR-OP-04/05、§7.5 | UPSTREAM_GONE 依赖远程分支删除检测，但全文没有 fetch `--prune` 或 refspec 要求；不 prune 则“远程已删”永远不可见，prune 又可能删用户关心的跟踪 ref；Tag 移动告警却禁止 `--prune-tags`，语义边界未写。 | 状态机里一整个子型不可达；SYNC 的“拒绝覆盖本地独有分支”缺少可靠判据。 | 定义 fetch 策略（显式 refspec + `--prune` 仅对 upstream 分支 + 保留 tag）；补 AC 覆盖“远程分支已删”。 |
| RT-13 | **高** | FR-OP-06、§12.6、OQ-04、AC-07/08 | 删除保留期 7/30 天三处冲突；恢复操作无 FR/AC；`.trash` 清理/扫描排除/与回收站二选一未定；备份 ref 无保留期与恢复入口。 | 数据恢复承诺不可验收；`.trash` 可能被再次扫描或无限膨胀；用户找不到恢复入口。 | 统一 30 天（或 OQ-04 定案）；新增“恢复”FR 与 `.trash` 清理任务；扫描显式排除 `.trash`；备份 ref 保留 ≥30 天并可在任务中心恢复。 |
| RT-14 | **高** | §3.1、§11.1、AC-11、NFR-PERF-09 | 首扫阶段“双阶段/三阶段/先本地后远程”三套说法；无门控与“远程就绪后重判”FR；AC-11 与 11.1 直接冲突。 | 首启行为无法验收；离线/半同步时组件可能被误判为 INVALID/缺失且不自愈（P0-09 原问题）。 | 统一三阶段 + 就绪门控 + 重判 FR/AC（见“建议修订”7）。 |
| RT-15 | **高** | §13.1 AC-04、FR-STA-01、§13.2、§10.6 | “100+ 状态夹具”无清单、无预期映射、未覆盖 12 聚合态与正交标记组合；状态矩阵不是随 SRS 交付的资产；03 §H 建议的 fake clock/对拍/property-based/基准门禁未并入。 | AC-04 不可判定；测试团队无法知道“100+”是哪 100+；时间相关 SLO 无法稳定测试。 | 附录 C 状态矩阵（14 主状态 × 关键 flag × 12 聚合态）作为 M0 交付物；测试策略补 `IClock`、夹具清单、Gitea 单二进制 fixture。 |
| RT-16 | **高** | §10.1、AC-13、NFR-PERF-01 | 性能口径混用：容量写 1,000 组件/4,000 仓，压测写“4× 余量（4,000 组件）”，AC-13 直接写“4,000 组件合成数据”（=16,000 仓）；无测量方法（采样方式、工具、门禁、数据集生成器）。 | 性能验收无法复现，1.5s/58fps 会变成扯皮；开发不知道按哪个规模优化。 | 增加“测量方法”列与数据集生成器交付物；把验收规模（正常/4×）分开写。 |
| RT-17 | **高** | §9.10 FR-OPS 全表、§10.6、§11.1 L3 | FR-OPS-01…06 无“验收”列；设置项未枚举（“每项有默认值”但无键/范围表）；错误码只有前缀无错误码表；页面六态缺“权限不足/认证过期”态（FR-CFG-02、FR-REPO-02 依赖）。 | 通知/日志/设置/DB 迁移都无法验收；权限场景无 UI 落点；任务中心错误码无法定义。 | 出设置项表（键/默认/范围/生效方式）+ 错误码表 + 第七态“权限/认证异常”；每条 FR-OPS 补 AC。 |
| RT-18 | **高** | §9.9 FR-SAFE、§10.3、AC-14、§12.5 | FR-SAFE 无验收列；Token 传输方式未规定（header vs URL query）；pre-existing 本地仓 `.git/config` 里可能已有明文 token，AC-14“`.git/config` 无 token”按此**不可能通过**；§12.5 承诺崩溃转储，但 §10.3/FR-SAFE-03 未把转储纳入脱敏；§10.3“仅 Credential Manager”与 §12.6“DPAPI 或 Credential Manager”冲突。 | 安全验收自相矛盾；崩溃转储/诊断包可能泄漏凭据；实现方案二选一未定。 | 规定 Authorization header-only；启动扫描 pre-existing 凭据并告警/脱敏展示；转储禁用或导出前脱敏；统一 D-006（Credential Manager，DPAPI 作为底层实现细节）。 |
| RT-19 | **高** | FR-SYNC-06、§12.4、FR-SYNC-02 | FR-SYNC-06 要求“使用条件请求（ETag/If-None-Match）”，§12.4 明写“**不假设**列表端点提供可用的 ETag 语义”——需求与架构建议直接冲突；FR-SYNC-02/§12.4 的“用户关注”概念全文未定义（无 FR/字段/设置）；FR-SYNC-05 的 renamed 无字段。 | ETag 是否验收有争议；关注仓机制无法实现；改名检测不完整。 | FR-SYNC-06 改为“以 `updated_at` 水位为主、ETag 可用则用”；定义“关注”的数据与 UI 或删除该词；补 renamed 字段。 |
| RT-20 | **高** | FR-BOARD-02/04、§13.1 | facet 取值域、排序键、默认次序、搜索字段来源均未定义；分支/Tag 搜索只覆盖“已绑定+关注”仓，与“全局搜索”矛盾；“超阈值”无值；无搜索/排序 AC。 | 看板验收无法判定；用户搜不到数据；批量二次确认无依据。 | 列 facet 枚举与排序键；定义阈值（如 >10 组件或 >4 仓）与“全选筛选结果”策略；补 AC。 |
| RT-21 | **中** | NFR-REL-02/REL-04、FR-OPS-04、AC-15 | 5 分钟 3 次熔断无注入测试；重建 ≤5min/100 组件无测量方法；“单实例锁”未要求崩溃后 stale lock 检测（AC-15 只测双开）。 | 可靠性承诺不可验收；崩溃后可能永远无法再启动。 | 补“职责故障注入”用例与重建计时方法；单实例锁定义 stale 判定与自愈。 |
| RT-22 | **中** | NFR-COMPAT-01 | Windows 10 22H2 已于 2025-10-14 结束支持，SRS 日期为 2026-11，仍把 Win10 列为基线，未说明 ESU 或仅 Win11。 | 兼容性/验收环境定义过期，企业机器策略不明。 | 改为“Windows 11 x64 为基线；Windows 10 22H2 仅 ESU 环境（需 OQ 确认）”。 |
| RT-23 | **中** | §3.1、FR-CFG-01、§6.3 D-013、OQ-17 | D-013/OQ-17 已定“v1 单根”，FR-CFG-01（P0）仍写“本地根目录（1..n）”；§6.3 已写单根。 | 首启配置与发现规则不一致，验收标准二义。 | FR-CFG-01 改为“本地根目录（v1 恰好 1 个）”；多根移至 v1.1 预留项。 |
| RT-24 | **高** | 附录 A（两处）、§10.6、§15、§8.1 | 附录 A **完整重复两遍**且处置措辞互相矛盾（一处“12 表（评审 03 §C.3）”，一处旧版“12 表”；一处“2 进程”，一处“职责域架构建议”）；需求状态图例 `[v1]/[v2]/[设计建议]` 几乎未被使用（全用 P0/P1/P2，两套体系无映射）；§10.6 声称矩阵但不存在；§8.1“字段名可调”与 FR-OPS-06 迁移/DDL 冻结冲突；OQ 同时存在于 review-report §7 与 SRS §15。 | 唯一事实源出现两个“附录 A”，团队会引用错误版本；迁移验收无稳定 schema；需求状态不可管理。 | 删除旧附录 A，保留一份；图例与 P0/P1/P2 二选一并给映射；冻结 8.1 字段名；OQ 只在 SRS §15 维护。 |
| RT-25 | **高** | §9.11、§11.4、§8.1、§12.6 | CI 预留返工点：`ci_status` DDL 与 `CiLatest` 契约不一致；`ci_latest` 视图未定义；通知无表；`CI_UPDATED` 单一事件（04 要求还应有队列变化事件）；CI-8 的 URL/Token 未纳入凭据/最小 scope；CI 数据新鲜度无锚点/AC；CI 排序/导出/权限无预留。 | v2 接入仍需迁移 DDL、改 IPC 契约、补凭据与 UI 状态，10 锚点“不返工”承诺只兑现了一半。 | 见“CI 预留返工点”10 条。 |
| RT-26 | **中** | §13.2、OQ-08、§11.2 | 测试策略包含“深色主题”，但需求与色板无深色定义（OQ-08 未决）；“视觉回归 ≥12 张”无法在缺 16 状态色板时建立；Fake Gitea 选型（Docker/WireMock）与 03 §H 推荐的“Gitea 单二进制 fixture”不一致。 | 测试项超出需求，验收无法执行；测试方案二义。 | 深色主题进 v1 并补色板，或删测试项；冻结测试资产选型（Gitea 单二进制 + WireMock 契约）。 |
| RT-27 | **中** | §4 UC-03、§3.1、FR-OP-06 | UC-03“输入组件标识→批量 Clone”无对应 FR（组件如何被“纳入管理”、远程不存在时如何提示、删除目录后如何“移出管理/unbind”未定义）；FR-OP-06 只删目录，DB 行保留为 MISSING 还是移除未定。 | 新组件接入/清理主流程无实现依据；用户无法停止管理某组件。 | 新增“组件纳入/移出管理”FR（含校验、清单、审计），明确删除后 DB 状态。 |
| RT-28 | **中** | §10.3、§12.5、§12.6、§10.4 | 同一主题在需求层与设计层重复且不一致：凭据（仅 Credential Manager vs DPAPI 或 Credential Manager）、日志（30 天/50MB vs 10MB×30 天）、安装签名（“内网分发校验” vs EV/MSIX 签名+更新校验）、Windows 支持（见 RT-22）。 | 两处说法必有一处被违反，验收引哪条都会扯皮。 | 需求层只写可验收约束（如“凭据不得明文落盘，可由 Credential Manager 或 DPAPI 实现”），数值统一。 |
| RT-29 | **中** | §2、FR-DETAIL-02、§10.5 | §10.5 自禁不可测措辞，但 §2“1 屏内回答”、FR-DETAIL-02“1 万提交仓库滚动流畅”仍不可判定；AC-13 也未给“流畅”的测量。 | 验收仍会回到主观判断。 | 改为“组件行在 1440×900、100% 缩放下完整展示 4 仓 chip + 状态 + 新鲜度”“1 万提交滚动 ≥58fps、首屏 ≤500ms”。 |
| RT-30 | **中** | §10.1、§12.2/§12.5、§8.2 | 架构细节与需求重复：NFR-PERF-07 已含 WebView2 200–400MB，但 §10.1 未定义测量口径；§12.2 的 IPC/管道细节属设计层（D-008 要求需求/方案分层），却写在“非需求”章之外被 §13 验收引用风险；§8.2 “每字段唯一写者”与 §12.1 Host 唯一写者/服务级写者未统一。 | 分层失效，需求评审被迫审架构；写者规则冲突会导致实现分歧。 | 需求层保留可验收约束，设计细节统一在 §12；写者规则统一为“Host 单写线程 + 服务级表所有权”。 |

### 与 03-architecture.md 的一致性核对（03 于复审期间落盘，必须纳入）

| # | 状态 | 内容 |
|---|---|---|
| A1 | ✅ 已并入 | 两 OS 进程 Host+GitWorker（D-011/§12.1）、SQLite WAL 单写者（§8.2/§12.3）、.NET 10 LTS（D-012/§10.4）、分层采集与新鲜度 SLO（§7.5/§12.4）、`quick_check`/`VACUUM INTO`/SQLite ≥3.51.3（§8.2）、安全基线（§12.6）、单用户/RDP 限制（NFR-COMPAT-07）。 |
| A2 | ⚠ 仍冲突 | §12.1“Host 唯一可发网络请求” vs GitWorker 的 clone/fetch/pull（见 RT-07）。 |
| A3 | ⚠ 仍冲突 | FR-SYNC-06 强制 ETag vs §12.4“不假设 ETag 可用”（见 RT-19）。 |
| A4 | ⚠ 仍冲突 | 凭据方案 D-006“Credential Manager” vs §12.6“DPAPI 或 Credential Manager”（见 RT-18/RT-28）。 |
| A5 | ⚠ 未采纳 | 03 §H 的测试资产（Gitea 单二进制 fixture、≥25 状态夹具、IClock、property-based、BenchmarkDotNet 门禁）未进入 §13.2（见 RT-15/RT-26）。 |
| A6 | ⚠ 未采纳 | 03/05 建议的 6 张图集（D1–D6，含验收）未进入 §3.3 里程碑或交付物清单，架构图集仍无 ownership（见“里程碑风险”）。 |
| A7 | ⚠ 部分 | ARC-05 数据模型：SRS 8.1 在 03 §C.3 基础上加 unbound_repo/baseline_snapshot，但 **“12 表”与实际 13 张不符、baseline_snapshot v2/v1 矛盾、ci_status 字段少于契约**（见 RT-06/RT-25）。 |

---

## 可测试性问题（逐 AC）

| AC | 判定 | 不可判定/缺口 | 补法 |
|---|---|---|---|
| AC-01 组件识别 | ✅ 可判定 | 需定义“同步完成”的判据（清单覆盖率 100%？）与夹具组织 | 加同步门控前置 + Fake Gitea 夹具 |
| AC-02 缺仓 | ⚠ 条件可判定 | OQ-03 未决；“提示”的形式/位置未定义；批量“只作用于 3 仓”的操作清单未列 | OQ-03 定案；写明确认对话框元素与逐仓结果字段 |
| AC-03 未绑定 | ⚠ 条件可判定 | 未覆盖 `_SIL_Design`（6.1 例外）、大小写冲突、空仓；清单在哪个页面展示未定义（11.1 无该页） | 补夹具；指定“未绑定仓库”入口位置；补 AC |
| AC-04 状态矩阵 | ❌ 不可判定 | 无“100+”夹具清单/预期映射；只覆盖单仓主状态，未覆盖 12 聚合态与 flags 组合；矩阵非交付物 | 附录 C 状态矩阵 + 表驱动测试 + M0 交付物 |
| AC-05 无假绿 | ❌ 不可判定 | “超 TTL”无值；STALE 不是组件态（7.4 无）；若同仓 BINDING_ERROR 会遮蔽 UNKNOWN；“数据时间”格式/来源未定义 | 定义 TTL 表；明确组件态优先级；规定时间戳展示格式 |
| AC-06 关系判定 | ✅ 可判定 | “与 fetch+merge-base 一致”的基准需说明（需先 fetch 才能算） | 写明对比方法和数据集 |
| AC-07 SYNC 保护 | ⚠ 条件可判定 | “丢失清单”字段未定义；恢复操作无 FR/入口；“备份 ref/stash”先后顺序未写 | 定义预览表字段与恢复 FR；补断言 |
| AC-08 删除可恢复 | ⚠ 条件可判定 | `.trash` vs 回收站二义；保留期 7/30 冲突；恢复路径/入口未定义；“远端零变更”无检查方法 | 统一机制/保留期；定义恢复 FR 与远端校验方法 |
| AC-09 部分失败 | ✅ 基本可判定 | OP_PARTIAL_FAILED 的清除条件/入口未定义；“仅重试失败项”的任务中心契约未写 | 定义清除动作；补任务中心字段 |
| AC-10 切 Tag | ❌ 不可判定 | TAG_DRIFT 依赖未定义的“基线模式”；7.4 无 DETACHED 聚合映射； “1 仓报告缺失”指缺 tag 还是缺仓有歧义 | 定义基线模式或无条件 TAG_DRIFT；改写 AC 为三仓 detached + 一仓缺 tag 的期望结构 |
| AC-11 首启 | ❌ 不可判定 | 双阶段 vs 三阶段；阶段名/进度 total 来源未定义；跳过降级后看板状态未定义 | 统一阶段；定义进度协议与降级态 |
| AC-12 CI 预留 | ⚠ 条件可判定 | 5 条验收未编号/无追溯；契约与 DDL 不一致；无 CI 新鲜度锚点；无 CI 空态快照集 | 编号 AC-12.1…12.5；冻结快照集与 DDL |
| AC-13 性能 | ❌ 不可判定 | 无测量方法/工具/数据集生成器/门禁；4,000 组件口径与 NFR 冲突 | 引用测量方法列与 4× 数据集生成器；分正常/压测两档 |
| AC-14 安全 | ❌ 不可判定 | “全量”范围未定义；已有仓 `.git/config` token 不可能清零；诊断包/转储是否纳入未定义；扫描工具/正则未写 | 规定 header-only、启动扫描告警、转储策略、扫描工具与样本 |
| AC-15 单实例 | ✅ 可判定 | 崩溃后 stale lock 未覆盖 | 补 stale lock 自愈用例 |

**覆盖率结论：** 15 条 AC 对 65 条 FR 的**直接覆盖约 20 条（≈31%）**；FR-SAFE-01…05、FR-OPS-01…07、FR-CFG-05/06、FR-SYNC-05/06、FR-SCAN-05/06、FR-STA-05/06、FR-BOARD-03/04、FR-DETAIL-03、FR-OP-03/07/09/10、FR-REPO-02/03/04、FR-CI-04/05 均无直接 AC。

**其他可测试性缺陷：** §13.2“kill -9”（Windows 措辞）、视觉回归“≥12 张”未给组合矩阵、深色主题无需求（RT-26）、性能测试机未含 Gitea 服务端环境（NFR-PERF-06 无法复现）、§10.6“Fake Gitea（Docker 或 WireMock）”未定选型。

---

## CI 预留返工点

1. **DDL↔契约不一致**：`CiLatest` 的 `branch/durationMs/fetchedAt` 无 `ci_status` 列，`repoId: string` 与 Gitea 数字 id 冲突 → v2 必迁移。
2. **`ci_latest` 视图未定义**：CI-9 提及但 8.1 无表/视图；聚合查询与索引无落点。
3. **通知 schema 缺表**：CI-6 自述“通知 schema 后加要迁移”，但 v1 未建 notification 表 → 与设计意图相反。
4. **`event_log` 缺口**：§12.4 承诺预留 webhook/事件源，但 8.1 无 event_log 表 → v2 加数据源要改主干。
5. **事件类型不足**：只有 `CI_UPDATED`，04 要求的队列/状态变化事件未预留 → 状态栏 CI 队列与通知刷新语义缺失。
6. **CI 设置项与凭据**：CI-8 的 URL/Token 未纳入 §12.6 凭据、target 命名、最小 scope → v2 改存储与权限模型。
7. **CI 数据新鲜度无锚点/AC**：契约要求 `fetchedAt`“UI 必须展示”，但 11.4 十锚点与 AC-12 均未覆盖 → 卡片/详情布局临时加字段。
8. **筛选/排序/导出未预留**：CI-7 只禁用 facet，未定义排序键、导出列、权限过滤 → v2 改查询/索引/报表。
9. **CI 徽标语义未冻结**：未提交改动、detached/tag、多 run 取哪个未写规则（04 明确要求“未提交不显示 CI 徽标”）→ v2 语义返工。
10. **视觉基线未冻结**：11.2 状态色不全，“状态×CI ≥12 快照”无法建立 → v2 接入时样式验收无基线可对。

---

## v1 范围与里程碑风险

1. **规模超载**：65 条 FR + 14 主状态/11 flag/12 聚合态 + 2 进程架构 + 8 页面 + 任务中心 + 10 CI 锚点 + 6 图集，但 §3.3 无工期/人力/依赖；M1 同时承载认证、远程同步、本地发现、状态计算、看板/详情、首扫，是最大风险点。
2. **M0 出口不可达**：“锚点快照通过”依赖看板/详情页面，而页面在 M1 才建；要么 M0 就建页面骨架（需明确），要么 M0 出口改为“Mock 数据 + 锚点组件单测”。
3. **M1 出口无 P95 指标**：“看板 P95 达标”在 NFR 中不存在（NFR-PERF-02 只写 ≤1.5s），指标悬空。
4. **M2 “6 条端到端”未列 ID**：§10.6/§13.2 分别列了 7 条与 11 条，数目对不上；无法判断 M2 完成。
5. **M3 “AC 全绿”是移动靶**：AC 仍自认“节选待补全”，未冻结就无出口。
6. **P1 功能无里程碑归属**：FR-CFG-04/05、FR-SYNC-05/06、FR-SCAN-06、FR-STA-05/06、FR-BOARD-04/05、FR-DETAIL-03/04、FR-OP-09/10、FR-SAFE-05、FR-OPS-05/06、FR-CI-04 共 17 条 P1 无任何里程碑承接。
7. **建议移出 v1（v1.1）**：FR-DETAIL-03（文件树/双击直开，安全面大且非核心）、FR-DETAIL-05（右键菜单，P2）、FR-CFG-06（热更新，P2）、深色主题（OQ-08 未决且色板未覆盖）、CSV 导出（已 v1.1）；FR-REPO-02/03（单仓 Push/Commit）可视排期延后，以保 M2 批量闭环质量。
8. **架构交付物缺口**：03/05 建议的 D1–D6 六张图集没有 owner 与交付节点（M0 只提锚点快照）；03 §H 的测试资产（fixture/IClock/基准）也未被纳入 M0，导致“验收前置”再次落空（review-report §6.5 的原始要求）。
9. **v1.1 边界未定**：Out-of-Scope 表只列 8 项，而 OQ-04/05/08/13/14/16/17/18 的非阻塞决策会改变 v1 边界；建议在 M0 前把所有 `[待确认]` 逐项落到“v1/v1.1/不做”三选一并记录。

---

## 必须修改的 Top 10

1. **修 §7.2/§7.1/§7.3 的新鲜度与离线语义**：给出 TTL 数值表、compare_ref 绑定、离线可判定状态集；关闭 OQ-02。
2. **补全 §7.4 聚合映射**（EMPTY/DETACHED/NO_UPSTREAM + 默认 UNKNOWN 兜底），与 §6.2 统一 INVALID，拆分“聚合态/严重度/排序”。
3. **冻结 §11.2 全量色板**（26 状态 + 深色 + 对比度 ≥4.5:1 + 红色纪律），删除与 §7.3 的冲突。
4. **补全 AC 与追溯矩阵**：65 条 FR 全量 AC、§9.9/§9.10 加验收列、M0/M1/M2/M3 出口绑定 AC ID；否则不进入 M2。
5. **补“14 状态 × 8 操作”矩阵与操作语义**：EMPTY 首推、set-upstream、detached 建分支、repair、CREATE 基线、取消/撤销、禁止操作清单。
6. **统一首扫三阶段 + 就绪门控 + “远程完成后重判” FR/AC**，修 AC-11/§11.1/§3.1/NFR-PERF-09。
7. **对齐数据模型与契约**：表数（13 张）、`baseline_snapshot` v1 写、`ci_status`↔`CiLatest`、`ci_latest` 视图、notification/event_log/scan_issue、`repo.local_path` 写者。
8. **修 §12.1“Host 唯一网络请求”与 GitWorker 职责矛盾**，定义 fetch 后 remote_ref 刷新与 generation；明确 §7.5 写前 fetch/CAS 的数据范围。
9. **补安全与运维可验收细节**：Token header-only、既有仓 token 扫描告警、转储脱敏、`.trash` 生命周期与恢复 FR、备份 ref 保留/恢复、stale lock；统一凭据/日志/安装/Windows 版本口径。
10. **整理文档工程并冻结**：删除重复附录 A、冻结 §8.1 字段名、统一需求状态图例与 P 级、OQ 单一来源并闭环、03 的测试资产与 D1–D6 图集进入里程碑。

---

## 建议的修订（可直接替换到 SRS 的措辞/表格行）

**1）§7.2 前置句（替换）**

> 前置：本地可判定状态（INVALID/MISSING/EMPTY/UNMERGED/IN_PROGRESS/DETACHED_TAG/DETACHED_COMMIT、busy/reparse_point）按本地事实判定，不依赖网络；关系类状态（BEHIND/AHEAD/DIVERGED/DIRTY/SYNCED）必须基于 TTL 内的 remote-tracking ref 判定。任一数据源超 TTL 仅使依赖该数据源的状态降为 `UNKNOWN`，UI 必须同时显示本地部分结果与数据时间。所有 `ref` 均指 `refs/remotes/<remote>/<upstream 分支同名分支>`，比较基准由 OQ-02（默认同名分支）确定后冻结。

**2）§7.4 聚合表（追加行 + 兜底；顺序建议）**

| 优先级 | 组件状态 | 触发 |
|---|---|---|
| 3.5 | `EMPTY` | 任一仓 EMPTY 且无更高态（灰「空仓库」） |
| 5.5 | `DETACHED` | 四仓均 DETACHED_TAG 且同 tag（青「基线 tag 检出」；与快照不符则为 TAG_DRIFT） |
| 5.6 | `NO_UPSTREAM` | 任一仓 NO_UPSTREAM 且无更高态（青「分支缺失/仅本地」） |
| 99 | `UNKNOWN` | 以上都不匹配（兜底，禁止静默显示 SYNCED/空白） |

> 说明：`MISSING` 在聚合优先级中保留但**不参与“异常优先排序”**；排序另用“处理优先级”列（BLOCKED_LOCAL > DIVERGED > BEHIND > OP_PARTIAL_FAILED > BINDING_ERROR > MISSING > DIRTY > …），避免未克隆仓淹没真故障。

**3）§11.2 色板（补行 + 修正）**

| 状态 | 浅色 fg/bg | 图标+文案 | 备注 |
|---|---|---|---|
| DIRTY | `#9A3412` / `#FDEBE3` | ⚠ 有未提交改动 | 琥珀红，非错误 |
| UNMERGED / IN_PROGRESS | `#9F1239` / `#FCE7F3` | ⛔ 冲突/操作未完成 | 阻断写操作 |
| INVALID / BINDING_ERROR | `#7F1D1D` / `#FEE2E2` | ⚠ 仓库绑定异常，仅本地修复 | 仅修复 |
| EMPTY | `#4B5563` / `#F1F2F4` | ∅ 空仓库 | 中性 |
| DETACHED_COMMIT | `#0E7490` / `#E3F4F7` | ⚑ 游离 commit | 允许建分支 |
| TAG_DRIFT / MIXED_BRANCH / OP_PARTIAL_FAILED / BLOCKED_LOCAL | 由 UI token 表补 | 各自中文文案 | 需补对比度验证 |
| BEHIND | `#92400E` / `#FDF1DC` | ↓ 落后 N | 替换现 `#B45309`（4.49:1→6.3:1） |
| NO_UPSTREAM | `#0B5E73` / `#E3F4F7` | ⑂ 分支缺失/仅本地 | 替换现 `#0E7490`（4.35:1→6.5:1） |

> 红色纪律改为：“红色仅用于阻断/失败态（UNMERGED、IN_PROGRESS、OP_PARTIAL_FAILED、BINDING_ERROR、LFS 缺失告警、CI FAIL、破坏性确认按钮）；正常开发态（AHEAD/DIRTY/DIVERGED）不得用红色。”深色主题按同一语义给 token。

**4）§6.2 与 §7.4 的 INVALID 行（替换）**

- §6.2 行改为：`本地目录不是 git 仓 / remote 不匹配 / 嵌套仓库` → **`BINDING_ERROR`（组件态，成员仓 primary=INVALID，不参与状态聚合的数值/时长统计，但组件整体按 BINDING_ERROR 展示，并给出修复动作）**。
- `>4 仓` 行改为：`列出候选，进入「未绑定仓库」清单并可查询；v1 不提供“收纳”写入（无 binding_override 模型），仅提示人工整理；自动合并禁止`。

**5）§8.1 关键行（替换）**

| 表 | 主键 | 关键字段 | 唯一写者 |
|---|---|---|---|
| `repo` | repo_id | component_id, repo_type, full_name UK, remote_url_norm, default_branch, is_local, local_path, archived_at, renamed_from, timestamps | REMOTE_SYNC（local_path/is_local 由 LOCAL_SCAN 写，见 `repo_local`） |
| `repo_local` | repo_id | root_id, local_path, is_local, last_seen_at | LOCAL_SCAN |
| `ci_status`（v1 空表） | (repo_id, commit_sha) | branch, provider, state, run_id, run_url, build_number, failed_stage, duration_ms, fetched_at, updated_at | 预留（v2 REMOTE_SYNC） |
| `baseline_snapshot` | (component_id, tag, repo_type) | commit_sha, tag_object_sha, captured_at, job_id | JOB_RUNNER（v1 只增不改） |
| `notification`（v1 空表） | id | category, severity, payload_json, created_at, read_at | ORCHESTRATOR |
| `event_log`（v1 空表） | id | source, type, payload_json, observed_at | REMOTE_SYNC（v2） |
| `scan_issue` | id | root_id, path, kind, reason, discovered_at | LOCAL_SCAN |

> 表数更正：本清单共 **16 张表**（含 job/job_item 分列）；删除“12 表”旧表述与重复附录 A。

**6）§12.1 Host/Worker 行（替换）**

> `Host.exe`：生命周期/监督/调度、**唯一持有凭据并发起 Gitea API 请求**、**唯一 DB 写者**、任务编排、托管 WebView2；`GitWorker.exe`：**唯一执行 git.exe 的常驻 worker**，执行 clone/fetch/pull/checkout/tag/push/reset/status/rev-list 等本地与网络 git 命令（网络凭据由 Host 经命名管道单次下发，不落盘、不出现在命令行/环境变量中），回传结构化事件供 Host 更新 `remote_ref`/`local_state` 并递增 generation。

**7）§13.1/§3.3 验收与里程碑（替换要点）**

> - 增加总则：“AC 总数 ≥ FR 总数；`13.1` 中每条 AC 必须带 FR 追溯号；M0 出口 = 状态矩阵/AC/夹具/色板基线全部冻结并通过评审；M3 出口 = **冻结版本 AC** 全绿。”
> - AC-04 改为：“Given 附录 C 状态矩阵（14 主状态 × flags 组合 × 12 聚合态）全部夹具，When 扫描与聚合，Then 每条夹具命中唯一主状态且聚合结果与期望表一致；夹具缺失视为未通过。”
> - AC-11 改为：“Given 新机器，When 首次启动，Then 依次显示 ①本地发现（total/进度）②远程对账（total/覆盖率）③状态计算；任一阶段失败可重试/跳过降级，远程完成后触发本地重判；降级态显示数据截至时间。”
> - AC-13 改为：“8C/16G/SSD、1440×900、数据就绪后测量：首屏 ≤1.5s（DevTools Performance LCP）、滚动 10s 采帧 ≥58fps 平均且无单帧 >33ms、搜索输入到结果 p95 ≤300ms；数据集由‘4,000 组件合成数据生成器’产出（版本化）；压测另按 4× 余量跑”。
> - M2 用例改为列 ID：“AC-07/08/09/10 + 崩溃恢复（kill 进程后 reconcile）+ 401/断网 + 复制/部分失败”；M1 出口改为“AC-01/02/05/06/11 + 状态矩阵通过”。

**8）§9.10 设置项/错误码（新增表，节选）**

| 键 | 默认 | 范围 | 生效 |
|---|---|---|---|
| `scan.fingerprintIntervalSec` | 15 | 5–60 | 立即 |
| `fetch.intervalMin` | 10 | 5–60 | 下一轮 |
| `sync.fullCronHours` | 24 | 6–168 | 下一轮 |
| `ui.dangerousConfirmThreshold` | 10 | 1–100 | 立即 |
| `delete.retentionDays` | 30 | 7–90 | 立即 |

> 错误码表：`<前缀>-<4 位>`（CFG/SYNC/SCAN/STATE/OP/FS/NET/GIT/SEC），每码给“用户文案、可重试性、日志级别”；任务中心/日志/诊断包统一引用。

**9）§5 术语表（修正行）**

| 术语 | 英文/枚举 | 定义 | 禁用别名 |
|---|---|---|---|
| 组件 | Component | 一个业务组件，ID = 仓库名去掉生命周期后缀后的全名 | 模块、大仓、组件前缀、聚合名（英文 Component 为正式译名，不作禁用别名） |

> §11.4 契约中所有 `componentPrefix` 改为 `componentId`，`repoId: string` 改为 `repo_id: number`。

**10）§15 与文首状态（替换）**

> 文首“版本/状态”改为：“v0.2（Draft，**M0 门禁：OQ-02/OQ-03 关闭 + AC/状态矩阵/色板/契约冻结后放行**）”；§15 增加“决策人、截止日、关闭依据”三列，删除 review-report §7 的重复 OQ 表（唯一来源 = SRS §15）。删除重复的旧附录 A，只保留一份与本 SRS 一致的处置对照。

**11）§9.2 FR-SYNC-06 与 §12.4（替换）**

> FR-SYNC-06：“增量同步以仓库清单 `updated_at` 水位为主判据；若服务端返回可用 ETag/If-None-Match 则作为优化，不作为正确性保证；并发上限默认 6，429/5xx 指数退避+抖动。”
> FR-SYNC-02/§12.4：删除“用户关注”概念（v1 无该功能）；改为“本地已绑定仓 + 当前看板可见组件对应的仓”。

**12）§9.9 新增 FR（替换/追加）**

> FR-SAFE-06（P0）恢复：`.trash` 与 `refs/backup/*` 的恢复入口、恢复前冲突检查（目标已存在/远程已改）、恢复审计；AC：删除后恢复、SYNC 后从备份 ref 恢复各 1 例。
> FR-SAFE-07（P0）备份生命周期：`.trash` 保留 30 天（可配 7–90）、清理任务与扫描排除；`refs/backup/sync/*` 保留 ≥30 天并可在任务中心查看/恢复/清理。

---

## 附：证据与复核入口

- 原 P0 清单：`reqs/review/01-requirements-critic.md` §阻断项、`02-git-domain.md` §G-P0、`04-ux-ci.md` §UX-01…10、`05-drawio-execution.md` §P0-01…10。
- 架构 P0：`reqs/review/03-architecture.md` §ARC-01…09（9 条）与 §C.3/§D.3/§F/§H。
- 审核报告现状：`reqs/review-report.md`（P0 42 条、OQ 双源、§8 已注明本报告将回灌 v0.2）。
- 复核建议：按本报告 Top 10 修订后，用同一状态矩阵/AC/色板/契约四处哈希做“冻结检查”，再以 `13.1` 全量 AC 重新复审；在此之前不建议签发 M0 入场券。
