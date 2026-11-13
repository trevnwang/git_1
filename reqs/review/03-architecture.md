# 评审报告 03 — 系统架构 / 进程模型 / 数据持久化 / 性能与安全可行性（对抗式评审）

- 评审对象（只读，未修改）：
  - `reqs/_tools/docx_dump.txt`（草案 docx 转写，下称 **docx L**）
  - `reqs/_tools/drawio_dump.txt`（流程图转写，下称 **drawio L**）
  - 原始文件 `reqs/MBD组件全生命周期Git工具.docx`、`reqs/software_workflow.drawio` 仅作交叉核对
- 评审 lens：**系统架构、进程模型、数据持久化、性能与安全可行性**。不评审 UI 视觉（归 04）、不评审组件状态机语义细节（归 02）、不逐条改写需求措辞（归 01）；只在"它们造成架构成本"处点一句。
- 方法：只读核对两份转写 + 对关键外部事实（Gitea API 分页/端点、SQLite WAL 多进程语义、DuckDB/LiteDB/Realm 并发限制、.NET 支持周期、WebView2 分发）做核查，链接见文末"参考"。
- 判定口径：**阻断（P0）** = 不解决则架构无法冻结/无法开工；**高危（P1）** = 会导致返工、现场事故或验收争议；**中低危（P2）** = 可维护性/交付卫生。
- 结论先行：**方向保持，方案整体否决**。产品方向（组件视角聚合四仓、远程元数据 + 本地 git 双数据源、看板可视化）成立；但"3/4 个常驻进程 + 极简五表 + 未选定嵌入式数据库 + 6h/120s 全量同步 + 30s 全量 git 轮询"这组实现设计，在 4000 仓 / 100 组件 / 单机 Windows 的约束下**定量不成立**，安全与可运维面**接近零设计**。

---

## 总体结论

### 1. 一句话判定

草案的**功能方向可保留**，**架构方案不可执行**：它把"职责名词"当成了"进程边界"，把"愿望形容词"当成了"性能预算"，把"技术栈口号"（高性能嵌入式数据库、优于 SQLite）当成了选型结论，并且在进程间通信、监管者、单实例、首启门控、凭据保护、可观测性六个必需面上完全空白。

### 2. 四条硬理由（每条都可用量算出来）

1. **进程模型不成立**：docx 说 3 个进程（docx L61/L79/L95），drawio 画 4 个（drawio L4-6、L27），且 CI 采集归属两处矛盾（docx L82/L87 进程 2 调 CI API vs drawio L8 进程 1 取 build result 入表 1）；聚合在 docx 是进程 3 内的规则（docx L105-107），在 drawio 是独立"进程 4"（drawio L27-28）。更关键的是：**没有任何一处定义进程间通信、没有定义谁监督谁、没有定义单实例与退出清理**，而 docx L157 却承诺"任意进程崩溃自动重启"——没有监管者，这句话无法实现。
2. **数据层不成立**：docx L114 声明"极简五表"，实际只给了表 1/2/3（L115-132），且 **表 2 连"属于哪个组件/哪个仓库"的列都没有**（docx L122-127），表 3 也没有组件键（L128-132），三张表全部没有主键/外键/索引/版本号/软删除；drawio L14 还要求把"仓库的所有 commit"落表，这与"五表"和规模假设冲突。三/四个进程同时写同一嵌入式库（docx L77/L93/L112），而候选库（SQLite WAL/DuckDB/LiteDB/Realm/LMDB）没有一个支持"多进程并行写"。
3. **性能预算不成立**：4000 仓全量同步若按 docx L66-70 的字段逐项取（分支列表、Tag 列表、Tag→Commit、每分支最新 Commit），最少是 **8000～48000 次 HTTP 请求**，docx L73 的"单次 120s 超时"只在最乐观路径（4000 次 refs 请求、并发 ≥8、平均延迟 ≤80ms）才勉强成立，任何慢仓/429/重试都会整轮超时，且 L74"失败重试 2 次"把负载放大 3 倍；本地侧 100 组件×4 仓 = 400 个仓库 / 30s（docx L89），要求平均每仓 ≤75ms 完成一次 git 状态采集，而 Windows 上 `git status` 在 MBD 大仓（模型/二进制文件多 + 杀软扫描）通常 150ms～2s，**超预算 2～25 倍**；同时 docx L56"状态刷新秒级感知"与 6h/30s/10s 三级轮询自相矛盾。
4. **安全与运维不成立**：drawio L96 把登录认证后的配置"保存为本地 json"（即 Gitea token 明文落盘），全文没有 TLS/代理/自签证书/日志脱敏/破坏性操作审计/安装包签名与更新校验的任何要求（docx L152-158 只有一句"所有状态变更留痕日志"），也没有日志分级/滚动/诊断包/崩溃转储/健康上报协议。

### 3. 建议的架构结论（详见"推荐架构"）

- **进程 = 2 个（v1）**：`MbdGitTool.Host.exe`（编排/调度/唯一 DB 写者/Gitea 客户端/托管 WebView2 UI）+ `MbdGitTool.Git.exe`（唯一执行 `git.exe` 的 worker，不碰 DB，走命名管道 JSON-RPC）。聚合、对比、扫描全部是 Host 内的 `BackgroundService`，不是独立进程；"多进程"的真实收益只有两个：**git 子进程可被整体杀死** 与 **外部库崩溃隔离**，其余都是成本。
- **数据库 = SQLite（WAL）+ 单写者 + Dapper**：数据量（4000 仓、约 12 万条 ref、400 条本地状态、100 条组件状态）不到 50MB，SQLite 完全不是瓶颈；"优于 SQLite"是伪需求，应改为可验收的选型约束（单文件、事务、WAL 并发读、可备份、零运维）。其它候选的排除理由见对比表（DuckDB 单写者且偏 OLAP、LiteDB 跨进程靠命名互斥且写放大、Realm 已停止支持、LMDB 无 SQL/无迁移生态）。
- **采集 = 分层拉模式 + 本地指纹/事件**：远程按"仓库清单水位（80 请求）→ 仅变化仓取 refs（1 请求/仓）→ 每日全量对账（可续传 job，无 120s 硬超时）"三级；本地按"stat 指纹轮询（400 仓×6 个文件，<100ms/轮）→ 仅指纹变化的仓跑 1 次 `git --no-optional-locks status --porcelain=v2 --branch`"两级 + `FileSystemWatcher` 做秒级触发。CI 在 v1 关闭（只留数据契约与 UI 占位），v2 走"commit 变化才查 + pending 退避"。
- **新鲜度必须分档写进 SLO**，不能再用"秒级感知"一个词覆盖本地/远程两种数据源。

### 4. 问题清单统计

P0 阻断 **9 条**、P1 高危 **10 条**、P2 中低危 **9 条**，需用户拍板的决策 **16 条**（Q1–Q16）。其中 **ARC-01/02/03/05/06/07/09** 属于"不决策就不能进入编码"的一票否决项。

### 5. 对其它 lens 的接口（本报告不越界，只留依赖）

- 组件身份规则（01 的 RQ-01/Q1）不冻结，`repo.component_id` 无法建 → 本报告 ARC-05 的数据模型只能给到"可替换的字段形状"。
- 单仓状态枚举（01 RQ-02、02 的表 A/B/C）不定稿，`repo_status.main_status` 的取值域无法冻结 → 本报告只要求"必须带 `data_source` 与 `calc_at`"。
- 批量 SYNC/删除的语义（02 的 3.3/3.6、01 的 RQ-08）不定稿，本报告 ARC-12 的"操作队列 + 部分失败模型"无法落地为具体任务类型。

---

## 草案架构的可行性判定（逐条）

> 判定：**可行** = 可直接进入设计；**需修正** = 方向对但参数/边界必须改；**不可行** = 按原文实现必然失败；**阻断** = 定义缺失到无法开工。量化依据见"推荐架构"内的请求量/耗时预算表。

| # | 草案主张（证据） | 判定 | 依据与量化 |
|---|---|---|---|
| 1 | "多独立单职责常驻进程，完全解耦、异常自恢复"（docx L59-60） | **不可行** | "职责"不是进程边界。三个轮询循环放进一个进程用 `BackgroundService` 即可；拆进程带来 IPC、生命周期、DB 多写、日志聚合、首启编排五类成本，却只换来 git 子进程隔离这一个收益。且"异常自恢复"无监管者（见 #2）。 |
| 2 | 三进程（docx L61/L79/L95）vs 四进程（drawio L4-6/L16/L27） | **阻断** | 数量与归属两套事实源：CI 归属矛盾（docx L82/L87 vs drawio L8）、聚合归属矛盾（docx L105-107 vs drawio L27-28）、对比结果落表矛盾（docx L112 表 3 vs drawio L26 表 2）。架构冻结前必须二选一。 |
| 3 | "任意进程崩溃自动重启，不影响 UI"（docx L157） | **不可行** | 无 supervisor、无重启上限、无重启退避、无崩溃循环熔断；UI 与后台进程的退出关系（关窗后后台是否继续）未定义；无单实例锁，双开将产生两个写者。 |
| 4 | 进程间通信机制 | **阻断** | 全文零字。未定义：传输（命名管道/本地回环/共享内存）、契约与版本协商、超时/取消、背压、错误码、日志关联 ID。 |
| 5 | 进程 1：6h 全量、120s 超时、失败重试 2 次、"仅更新变更仓库"（docx L72-75） | **不可行（需重构成 job）** | 请求量 8000～48000；120s 硬超时使"慢一分钟"变成"整轮失败"，重试 ×3 放大负载；"仅更新变更"缺变更判定手段（Gitea 列表端点不保证返回 ETag 语义，且没有"自上次同步以来"的参数），必须先取仓库清单的 `updated_at` 水位再决定是否取 refs。 |
| 6 | 进程 2：30s 遍历本机已克隆组件（docx L89-91） | **不可行** | 400 仓 / 30s = 13.3 仓/秒；Windows 上每次 `git.exe` 启动 20～60ms，`git status` 于 MBD 大仓 150ms～2s，单进程串行需 60～800s/轮；8 路并发也会被磁盘 IO 与杀软拖到超时，且持续占满 CPU。 |
| 7 | 进程 2 同时"读取本地真实 Git 仓库状态 + 调取 CI API"（docx L82/L87） | **需修正** | 一个进程混两类数据源，且 CI 是远程数据（与 drawio L8 冲突）。CI 若按 400 仓 × 2 次/分钟 = **48000 请求/小时**，不可行；必须先按 commit 变化再查，且 v1 应关闭。 |
| 8 | 进程 3 每 10s"纯内存比对，无 IO 阻塞"（docx L109-110） | **需修正** | 它要读表 1 + 表 2（docx L98），读 DB 就是 IO，与"无 IO"矛盾；100 组件×4 仓 = 400 行的比对是微秒级，10s 周期本身不是问题，问题是**输入数据的新鲜度**（6h/30s）与**每次全量重写 400 行的写放大**。建议改为 dirty-set 触发 + 只写变化行。 |
| 9 | "把相同前缀仓库聚合成组件"（drawio L28、L30-33） | **可行（但不应独立成进程）** | 纯函数，输入 400 行 → 输出 100 行，µs 级；作为 Host 内纯函数或 SQL 视图即可，独立进程（drawio 的"进程 4"）是纯成本。 |
| 10 | "极简五表"（docx L114），实际定义 3 张（L115-132） | **阻断** | 表 2（L122-127）无 repo/component 归属列、无主键 = 无法与表 1/表 3 关联；表 3（L128-132）无组件键；表 1 无仓库全名/URL/默认分支（L116-121）；无操作日志表/任务表/配置表/CI 表；无任何索引、时间戳、软删除。 |
| 11 | drawio L14"记录仓库的所有 commit" | **不可行** | 400 仓 × 数万 commit = 千万级行，且这是 git 的本职（本地 object DB），落表除膨胀无收益；指标只需 ahead/behind 计数与 HEAD sha。 |
| 12 | "高性能嵌入式数据库（替代 Postgres、优于 SQLite）"（docx L163） | **阻断（判定为伪需求）** | 未给候选、未给评价标准、未给结论；且与规模不匹配：4000 仓 + 12 万 ref 对 SQLite 是小数据集。真正瓶颈在 git IO/进程启动/网络，不在 DB。应改为选型约束 + 对比表（见后文）。 |
| 13 | 多进程同时写同一嵌入式库（docx L77/L93/L112 三处写者） | **不可行** | SQLite WAL 允许"多读 + 单写"，写者之间串行；跨进程还需同主机本地磁盘（WAL 不跨网络文件系统，见参考 [1]）。且 3.51.3 之前存在 WAL-reset 竞态导致库损坏的缺陷（多连接/多进程同写是触发条件之一，参考 [1] §11），必须锁版本。 |
| 14 | WebView2 网页级前端（docx L134/L160-163，drawio 全文） | **可行，有前置** | WebView2 Runtime 是运行前置，Win11 自带、Win10 多数已有但仍需检测与离线安装包（参考 [2]）；常驻型应用要注意"运行时更新后需重启才生效"（参考 [2] 的 Evergreen 更新段）。 |
| 15 | 本地发现：遍历根目录直接子文件夹、判断是否 git 仓且 remote 含该组织同名仓（drawio L9-14） | **需修正** | 遍历深度/根目录未定义（选到 `D:\` 会灾难）；每个目录要跑 `git remote` 判定（400 目录 ≈ 400 次进程启动）；`safe.directory`（他人/共享盘仓）会报错（参考 [7]）；remote URL 可能带凭据，需脱敏与 host 白名单；Windows junction/symlink 需拒绝跟随。 |
| 16 | 前端"首次运行需要展示后端进程 2 遍历的进度…需要耗时"（drawio L38） | **需修正** | 只把"遍历"当首启长任务，漏了首轮远程同步、克隆队列、建库/迁移；无进度协议、无门控（表 1 未就绪时看板显示什么）、无可取消；认证在"开始"之后（drawio L35）但后端先起，需定义 degraded 态。 |
| 17 | 双数据源"以远程真实数据 + 本地真实数据双为准"（docx L50-51、L86-87） | **可行，需补规则** | 必须写成可执行规则：DB 永远是缓存不是事实源；任何破坏性操作前 **read-through 重新读取真实状态**；启动时做一次磁盘↔DB 对账（reconciliation）；不变量：DB 行必须带 `observed_at`，超期即标 `stale`。 |
| 18 | "所有状态变更留痕日志，可回溯排查"（docx L158） | **需修正** | 未定义分级、落盘位置、滚动、保留期、脱敏、诊断包导出、操作审计（谁/对哪个仓/前后 commit）。"日志"与"审计"是两套东西。 |
| 19 | 单机单用户假设 | **未声明，有风险** | 全文未定义"同一台机器两人用（RDP 多会话）/ 同一工作区被两个实例打开 / 工作区在共享盘"的行为；这些会直接破坏 DB 与 git 锁模型。 |
| 20 | "进程 1 不读本地文件、不做对比、不操作本地 Git；进程 2 不访问远程全部仓库"（docx L64/L91） | **可行（这条是全文最好的设计）** | 职责隔离方向正确，应保留并加强为"数据所有权"：`remote_*` 表只由 RemoteSync 写，`local_*` 表只由 LocalScan 写，`repo_status` 只由 StatusCalc 写；单进程内同样遵守，避免竞态。 |

---

## 推荐架构（进程 / 通信 / 数据库 / 采集策略 / 技术栈候选对比）

### A. 进程清单（v1 推荐 2 个 OS 进程 + OS 自带的 WebView2 渲染进程）

| 进程 | 数量 | 职责 | 数据权限 | 备注 |
|---|---|---|---|---|
| `MbdGitTool.Host.exe` | 1 | 生命周期/监督、调度（同步/扫描/对比三个 `BackgroundService`）、**唯一 DB 写者**、Gitea 客户端、git 任务编排、日志/健康上报、托管 `WebView2` 窗口 | 唯一可写 DB；唯一可发网络请求 | 常驻；托盘；登录自启（建议 Task Scheduler 用户级） |
| `MbdGitTool.Git.exe` | 1（常驻 worker） | 唯一执行 `git.exe` 的进程：clone/fetch/pull/checkout/tag/commit/push/reset/status/rev-list；每仓互斥；进度回调；取消时杀整棵进程树 | **不碰 DB**；只接收 job、回传结构化事件 | 由 Host 用 Job Object 托管，Host 退出即整树退出；worker 崩溃只影响当前 job |
| `msedgewebview2.exe`（OS 管理） | 若干 | WebView2 渲染 | 无 | **已经是多进程**：所以"多进程架构"的真实成本通常被低估——UI 侧本就有 3～5 个进程 |
| `MbdGitTool.Watchdog.exe` | 0 或 1 | 仅"无人值守常驻"模式需要：拉活 Host | 无 | v1 默认不做；若要求"关掉 UI 后台仍跑"，见决策 Q2 |

**不推荐**的进程：聚合进程、对比进程、每类轮询一个进程。理由：无故障隔离收益（都是托管代码、无原生崩溃面）、纯 CPU 微秒级工作、却引入 IPC 与多写 DB 风险。

**进程内并行模型**：Host 内用 `System.Threading.Channels` + 有限并发调度器；不引入 actor 框架。所有对 DB 的写走 `Channel<WriteCommand>` → 单写线程，按 200ms/500 行批量提交事务。

### B. 通信方式（必须写进设计文档，草案为零）

| 链路 | 传输 | 契约 | 版本/超时/背压 |
|---|---|---|---|
| UI ↔ Host | **同进程**：WebView2 `PostWebMessageAsJson` / `WebMessageReceived`（命令 + 事件推送） | 消息 `{id, type, payload}`；事件 `job.progress / status.changed / health.changed` | UI 侧用 `id` 关联请求；事件可合并（节流 100ms） |
| Host ↔ GitWorker | **命名管道** `\\.\pipe\mbdgit-{SID}-{AppGuid}`，ACL 仅当前用户；长度前缀 JSON（或 `StreamJsonRpc`） | `Hello{protocolVersion,appVersion,capabilities}` → `RunJob{jobId,kind,repos[],opts}` → `Progress{jobId,...}` → `Done{jobId,results[],exitCode}`；`Cancel{jobId}`；`Ping` | 握手版本不匹配即拒绝；控制类调用 10s 超时；job 无硬超时但有"无进度 30s → 判死并杀进程树"；队列满回 `BUSY` |
| Host ↔ 外部（测试/未来拆 UI） | 可选：`127.0.0.1` 随机端口 + 启动令牌（不触发防火墙提示），仅 loopback | REST + SSE | 仅 dev/自动化测试启用；生产默认关闭 |
| Host ↔ Gitea | HTTPS + `Authorization: token`（参考 [3]） | 见"采集策略" | 并发闸门 + 429/403/5xx 退避 + 每请求 15s 超时 |
| Host ↔ git.exe | `Process` + 参数数组（**禁止拼接 shell 字符串**）+ `GIT_OPTIONAL_LOCKS=0`（只读命令） | stdout/stderr/exitCode + 结构化解析 | 只读命令 30s；网络命令 10min；取消杀进程树 |

**监督与重启**：Host 是父进程，创建 GitWorker 时挂 Job Object（`JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`）→ Host 异常退出时 worker 自动退出，避免孤儿。重启策略：1s→2s→4s→…→60s 上限；**5 分钟内失败 3 次即熔断降级**（状态栏红点 + 可操作原因，禁止静默重启风暴，呼应 docx L157）。Host 自身崩溃：写 WER/托管转储 + 下次启动自检（DB 完整性 `PRAGMA quick_check` + 未完成 job 标记为 `INTERRUPTED`）。

**生命周期**：默认"关窗即退出（托盘可最小化）"；若用户要求后台常驻，则改为"关窗 → 托盘常驻"，并由登录项拉起。**不做 Windows 服务**（v1）：服务在 Session 0，拿不到映射网络盘、拿不到用户 DPAPI 凭据、无法弹 UI，反而制造"看起来在跑但同步失败"的隐蔽故障。

### C. 数据库（判定：SQLite WAL，单写者）

#### C.1 候选对比（.NET + 多进程写 + 4000 仓元数据 + 100 组件状态）

| 候选 | .NET 接入 | 多进程并发写 | SQL/迁移生态 | 本场景适配 | 结论 |
|---|---|---|---|---|---|
| **SQLite 3.51.3+（WAL）** | `Microsoft.Data.Sqlite`（官方、自带原生库）+ Dapper | 支持"多读 + 单写"；跨进程需同主机本地盘（WAL 依赖共享内存，**不能放网络盘/UNC**，参考 [1]）；写冲突用 `busy_timeout` + 应用层单写队列解决 | 完整 SQL、索引、事务、`user_version` 迁移、`VACUUM INTO` 备份 | 12 万 ref + 400 状态行 <50MB；单文件零运维；**推荐** | ✅ **采用**，配单写者 |
| LiteDB | 纯托管 | `shared` 模式靠命名互斥，每次操作开关文件，写放大且多进程性能差（参考 [4]） | 无 SQL，自有查询 API，迁移靠手工 | 可存文档，但并发模型与本场景相反 | ❌ 不采用 |
| DuckDB | 官方 .NET | **单写者**：多进程写同一文件直接锁失败；多进程只读可（参考 [5]） | 完整 SQL 但偏 OLAP/分析 | 分析型，写并发模型不符；为 400 行状态引入列存毫无收益 | ❌ 不采用 |
| Realm（Atlas Device SDK） | Realm .NET | 单进程为主 | 对象模型 | **2024-09 起弃用，2025-09-30 生命周期结束**（参考 [6]） | ❌ 直接排除 |
| LMDB（LightningDB 等） | 第三方原生绑定 | 单写者，多读；内存映射 | 无 SQL、无迁移/备份工具、Windows 上 mmap 细节多 | 高性能 KV，但本场景不需要；运维/迁移成本高 | ❌ 不采用 |
| （备选）SQL Server LocalDB / PostgreSQL 嵌入式 | 官方 | 支持 | 强 | 需装实例/服务、许可与运维成本，单机工具属于过度设计 | ❌ 不采用 |

**对"优于 SQLite"的正面反驳**：本场景数据量小、读写比高、单机、要零安装、要能整库备份带走——这四条恰好是 SQLite 的设计目标。应当把需求改写为可验收约束：单文件、ACID、WAL 并发读、schema 版本化迁移、可热备份、崩溃后可 `quick_check` 自愈、无独立服务进程。**唯一要锁的是版本**：确保 bundled SQLite ≥ 3.51.3（修复多连接/WAL 竞态导致损坏的 WAL-reset 缺陷，参考 [1] §11），并在启动时 `SELECT sqlite_version()` 写入日志。

#### C.2 写入模型（解决"多进程写同一嵌入式库"）

- **单写者**：只有 Host 持有可写连接；GitWorker 永不连库；外部工具禁止直连（DB 文件 ACL 仅当前用户）。
- **连接参数**：`journal_mode=WAL; synchronous=NORMAL; busy_timeout=5000; foreign_keys=ON; journal_size_limit=...`；生产写连接 1 个 + 读连接池 N 个（`Mode=ReadOnly`）。
- **批处理**：写命令入 `Channel`，单写线程每 200ms 或 500 行提交一次；状态行"只在值变化时写"（配合 `dirty_set`），把每 10s 全量重写 400 行降为通常 0～5 行。
- **备份/迁移**：`PRAGMA user_version` + 顺序 SQL 迁移脚本 + 迁移前 `VACUUM INTO backup-{version}.db`；迁移失败自动回滚并保留旧库。
- **容错**：启动 `quick_check`；损坏时重命名旧库 → 建新库 → 全量重扫（数据都是可重建的缓存，这是本架构的最大优势，必须在需求里写明"DB 可重建"）。

#### C.3 Schema 建议（替换"极简五表"，字段可随 01/02 的枚举定稿微调，但键/索引/时间戳必须保留）

```sql
-- 事实源是磁盘与 Gitea；本库是可重建缓存，故所有行均带 observed_at
component(component_id TEXT PRIMARY KEY, display_name TEXT, local_root TEXT,
          aggregate_status TEXT, status_calc_at TEXT, incomplete INTEGER DEFAULT 0,
          is_deleted INTEGER DEFAULT 0, created_at TEXT, updated_at TEXT);

repo(repo_id INTEGER PRIMARY KEY, component_id TEXT NOT NULL REFERENCES component(component_id),
     repo_type TEXT NOT NULL CHECK(repo_type IN ('DESIGN','IMPL','MIL','SIL','OTHER')),
     full_name TEXT NOT NULL UNIQUE, remote_url TEXT, default_branch TEXT,
     is_local INTEGER DEFAULT 0, local_path TEXT, archived INTEGER DEFAULT 0,
     created_at TEXT, updated_at TEXT);
CREATE INDEX ix_repo_component ON repo(component_id, repo_type);

remote_ref(repo_id INTEGER NOT NULL REFERENCES repo(repo_id),
           ref_type TEXT NOT NULL CHECK(ref_type IN ('BRANCH','TAG')), ref_name TEXT NOT NULL,
           commit_hash TEXT NOT NULL, observed_at TEXT NOT NULL,
           PRIMARY KEY(repo_id, ref_type, ref_name));
CREATE INDEX ix_ref_repo ON remote_ref(repo_id);

local_state(repo_id INTEGER PRIMARY KEY REFERENCES repo(repo_id),
            head_type TEXT, head_ref TEXT, head_sha TEXT, upstream_ref TEXT, upstream_sha TEXT,
            ahead INTEGER, behind INTEGER, dirty_count INTEGER, untracked_count INTEGER,
            op_in_progress TEXT, fingerprint TEXT, scan_at TEXT, scan_error TEXT);

repo_status(repo_id INTEGER PRIMARY KEY REFERENCES repo(repo_id),
            main_status TEXT NOT NULL, flags_json TEXT, error_code TEXT,
            data_source TEXT NOT NULL, is_stale INTEGER DEFAULT 0, calc_at TEXT NOT NULL);
CREATE INDEX ix_status_stale ON repo_status(is_stale);

ci_status(repo_id INTEGER, commit_sha TEXT, provider TEXT, state TEXT,
          run_id TEXT, run_url TEXT, updated_at TEXT, PRIMARY KEY(repo_id, commit_sha)); -- v1 建空表

job(job_id TEXT PRIMARY KEY, kind TEXT, target_json TEXT, state TEXT,
    created_at TEXT, started_at TEXT, finished_at TEXT,
    total INTEGER, succeeded INTEGER, failed INTEGER, cancel_requested INTEGER);
job_item(job_id TEXT, repo_id INTEGER, state TEXT, attempt INTEGER, message TEXT,
         updated_at TEXT, PRIMARY KEY(job_id, repo_id));

operation_log(id INTEGER PRIMARY KEY, ts TEXT, actor TEXT, action TEXT, target_json TEXT,
              before_json TEXT, after_json TEXT, result TEXT, error_code TEXT, request_id TEXT);
CREATE INDEX ix_oplog_ts ON operation_log(ts);

sync_run(run_id TEXT PRIMARY KEY, kind TEXT, started_at TEXT, finished_at TEXT,
         coverage REAL, total INTEGER, ok INTEGER, failed INTEGER, error TEXT);
app_setting(key TEXT PRIMARY KEY, value TEXT);
```

对应必须解决的草案缺陷：**表 2 无归属列**（docx L122-127）→ 由 `local_state.repo_id` PK 解决；**表 3 无组件键**（L128-132）→ `repo_status` + `component.aggregate_status` 两级；**"五表"不实**（L114）→ 明确 8 张业务表 + 2 张治理表（`job`/`sync_run`/`operation_log`；`app_setting` 存非敏感配置）；**drawio L14"所有 commit"** → 不落表，只存 head/upstream sha + ahead/behind。

---

### D. 采集策略（替换 6h/120s 全量 + 30s 全量 git 轮询 + 10s 全量比对）

#### D.1 请求量与耗时估算（这是判定"120s 是否现实"的依据）

| 场景 | 请求构成 | 请求量 | 乐观耗时（并发 8、平均 80ms） | 结论 |
|---|---|---|---|---|
| 仓库清单 | `GET /orgs/{org}/repos?page&limit=50` | 4000/50 = **80** | ~1s | 很便宜，应高频做 |
| 每仓 refs（推荐） | 每仓 1 次 `git/refs` 或 `git ls-remote --heads --tags` | **4000**（tag 多时翻页 → 4800～6000） | 40～60s | 可做，但必须可续传 |
| 草案 L66-70 的分支逐条取 | 分支列表 + Tag 列表 + Tag→commit + 每分支 commit | **8000～48000** | 80～480s | **120s 必然失败** |
| 本地全量 400 仓 / 30s | 每仓 ≥1 次 `git.exe`（status 或 rev-parse/rev-list） | 400 次进程启动 | 串行 60～800s | **30s 必然失败** |
| CI 轮询 / 30s | 400 仓 × 2 次/分 | **48000/h** | 每轮 ~20～40s | 过量，v1 关闭 |

#### D.2 分层采集（把"秒级"拆成可验收的三档）

| 层 | 数据 | 触发/频率 | 预算 | 写者 |
|---|---|---|---|---|
| L0 本地发现 | 配置根下 depth≤2 的 `.git` 目录 → `repo`/`component` 行 | 启动、每 10min、`FileSystemWatcher` 事件、手动刷新 | 400 目录 <100ms（纯 stat） | LocalScan |
| L1 本地指纹 | `.git/HEAD`、`.git/index`、`.git/refs/**`、`.git/packed-refs`、`.git/logs/HEAD`、`.git/FETCH_HEAD` 的 `mtime+size` | **每 15s**（只 stat，不起进程） | 400 仓 × ~6 文件 = 2400 次 stat，<100ms | LocalScan |
| L2 本地深扫 | `git --no-optional-locks status --porcelain=v2 --branch` + `rev-list --left-right --count`（参考 [7] 的 `--no-optional-locks`，避免抢 `index.lock`） | **仅指纹变化的仓**；8 路 worker；热仓（当前选中）5s | 变化率 5% → 20 仓 × 0.3～1.5s / 8 ≈ 1～4s | GitWorker→Host |
| L3 远程清单水位 | `GET /orgs/{org}/repos?sort=updated&limit=50`，读到 `updated_at < watermark` 即停 | **每 10min** | 80 请求 ≈ 2～8s | RemoteSync |
| L4 远程 refs | 仅 `updated_at` 变化或本地已有（需精确状态）的仓 → refs 端点/`ls-remote` | 每 30min 或按需 | 稳态通常 ≤20 请求 | RemoteSync |
| L5 远程全量对账 | 4000 仓 refs，**可续传 job**，按 `scan_run` 记录页游标 | 每日 1 次（或用户设定 6h） | 2～5min；无 120s 硬超时 | RemoteSync |
| L6 CI | v1 关闭；v2：HEAD sha 变化才查，PENDING 时 30s→1m→5m 退避 | — | 稳态接近 0 | RemoteSync（v2） |

**关键机制**：①仓库清单 `updated_at` 水位是"增量"的唯一可靠抓手（Gitea 列表端点分页返回 `Link`/`x-total-count`，参考 [3]，但不要假设列表端点提供可用的 `ETag/If-None-Match` 语义，否则要用"每仓 updated_at 比较"兜底）；②任何一轮同步都写 `sync_run`（覆盖率、失败清单），覆盖率 <100% 时 UI 标 `stale` 并显示"数据截至 HH:mm"；③全量对账失败可续传，不做"整轮 120s 一刀切"；④并发闸门 `SemaphoreSlim(6~8)` + 429/403/5xx 指数退避 + 抖动；⑤对同一 Gitea 的 N 个客户端做提示：4000 仓 × N 人 × 每 6h 会成为服务器负担（100 人约 18 请求/秒持续），企业环境应协商限流或后续引入组织级共享缓存/webhook 服务。

**Webhook/事件驱动的可行性结论**：Gitea 支持组织级/实例级 webhook（参考 [8]），但桌面客户端在 NAT/防火墙/终端安全策略下通常**无法接收入站回调**，v1 不能依赖。正确做法是：v1 纯拉模式，但把所有远程变化写进 `remote_ref.observed_at` + 预留 `event_log` 表，使 v2 接入 webhook 或"局域网中继服务"时**只加一个数据源，不改采集主干**。若企业允许 Gitea 回连工作站（同内网 + 放行入站），webhook 可把远程感知从分钟级降到秒级——这是 v2 的可选项（决策 Q6）。

#### D.3 新鲜度 SLO（替代"状态刷新秒级感知"）

| 事件 | 目标（P95） | 实现 |
|---|---|---|
| 用户在本工具执行写操作 | ≤2s 看板变化 | 操作完成事件 → 该组件强制重扫 + dirty-set 重算 |
| 用户/外部 IDE 修改本地工作区 | ≤5s | `FileSystemWatcher` + 指纹兜底 |
| 本地 commit / checkout / fetch（外部工具） | ≤10s | `.git/logs/HEAD` 或 `index`/`refs` 指纹变化 |
| 远程 push / 打 Tag | ≤10min（L3 水位）/ ≤6h（若关掉 L3/L4） | 决策 Q6 |
| UI 首屏（100 组件 / 400 仓） | 冷 ≤3s、热 ≤1s | 只查 SQLite + 增量渲染 |

### E. 技术栈候选对比与推荐

| 层 | 推荐 | 备选 | 决策要点 |
|---|---|---|---|
| 运行时 | **.NET 10 LTS（net10.0，支持至 2028-11）** | .NET 8 | .NET 8 已于 **2026-11-10 终止支持**（参考 [9]），本报告成稿日已过期，`net8.0` 不可作为新项目目标 |
| UI | **WebView2（Evergreen + 启动检测 + 离线 Standalone 安装包）** + TypeScript + React/Vue + Vite | 固定版本 WebView2 | Evergreen 缺失需检测安装（参考 [2]）；固定版 >250MB 且不能从 UNC 运行（参考 [2]），企业离线场景才考虑；常驻应用要处理"Runtime 更新后需重启"（参考 [2]） |
| 宿主 | `Microsoft.Extensions.Hosting` + `BackgroundService` + `System.Threading.Channels` | — | 单进程内调度三个采集器，共享一个 `IClock` 便于测试 |
| DB | **SQLite WAL + `Microsoft.Data.Sqlite` + Dapper + 手写迁移** | EF Core（CRUD 便利但迁移/性能黑盒）、LiteDB | 见 C.1；EF Core 亦可用，但本项目表少、SQL 简单，Dapper 更可控 |
| git 访问 | **`git.exe` 子进程为唯一写/读实现**；参数数组、`--no-optional-locks`、`-c safe.directory` 显式处理、杀进程树 | LibGit2Sharp | 只用 CLI：与用户 IDE/命令行行为一致、支持 LFS/credential helper/GCM、可取消；LibGit2Sharp 仅可作为"读 ref 加速"的可选优化，**禁止混用两种实现做写操作** |
| Git 依赖 | 要求已装 Git for Windows ≥ 2.40（含 Git LFS、GCM） | 捆绑 PortableGit | 捆绑涉及 GPL 分发义务与体积；v1 推荐"检测 + 引导安装"（决策 Q16） |
| HTTP | `HttpClient`（`SocketsHttpHandler`）+ `Polly` 退避 | — | 走系统代理/证书库；自签 CA 用进程内 handler 或 `-c http.sslCAInfo`（只对本次 git 调用生效，**不改用户全局 git config**） |
| IPC | 命名管道 + `StreamJsonRpc`（或长度前缀 `System.Text.Json`） | gRPC over loopback | 管道 ACL 仅当前用户，无端口/防火墙问题 |
| 日志 | Serilog（文件滚动 + 结构化 JSON + 审计独立 sink） | — | 见 F |
| 安装/更新 | MSIX（App Installer，用户级，签名，自动更新） | WiX MSI / Inno Setup | 企业 SCCM/Intune 分发需要 MSI 时再出 MSI 变体；更新前等 job 空闲并备份 DB |
| 崩溃诊断 | `DOTNET_DbgEnableMiniDump=1`（托管）/ WER LocalDumps（原生、WebView2） | `dotnet-dump` | 见 F |
| 测试 | xUnit + Gitea 单二进制 fixture + WireMock.NET + 状态矩阵脚本 + BenchmarkDotNet | — | 见 G |

### F. 安全基线（草案为空白，drawio 只有"本地 json"一句）

| 面 | 要求 |
|---|---|
| 凭据存储 | **禁止** config.json/git config/日志中出现 token。用 **DPAPI（`ProtectedData`，CurrentUser 作用域）** 加密存于 `%LOCALAPPDATA%\MbdGitTool\secrets.dat`（ACL 仅当前用户），或 Windows Credential Manager（`CredWrite`/`CredRead`，target 如 `MbdGitTool/Gitea/{host}/{user}`）；配置文件只存引用与脱敏尾 4 位（参考 [10][11]）。 |
| Token 权限 | 最小作用域：同步用 `read:repository`（Gitea token 支持按 scope 创建，参考 [3]）；仅当允许 push 时才申请 `write:repository`，且**读写 token 分离**；token 轮换/过期 → UI 明确"需重新认证"并进入 degraded 态。 |
| 传输 | 默认仅 HTTPS；支持企业代理（认证代理）与自签 CA；git 侧 `http.sslBackend=schannel`（走 Windows 证书库）或每次调用 `-c http.sslCAInfo=`；禁止 `http.sslVerify=false` 兜底。 |
| 日志脱敏 | 统一过滤器：`Authorization`、`token=`、`access_token=`、URL userinfo（`https://user:pass@`）、`git remote -v` 输出；错误上报前二次清洗（git 会把 remote URL 原样打进 stderr）。 |
| 破坏性操作 | dry-run → 影响清单（将丢弃的 commit/未推送/未提交）→ 二次确认（输入组件名）→ 执行 → 审计。删除只作用于本地克隆并移入 `.trash/`（30 天），v1 禁止操作远程仓库；覆盖类操作前必须 read-through 真实状态（不以 DB 为准）。 |
| 审计 | `operation_log` 追加写 + 独立审计日志文件；记录 actor（Windows 身份）、job_id、目标仓、前后 HEAD sha、结果、耗时；保留 ≥90 天。 |
| 路径安全 | 仓库全名/本地路径做 canonical 校验：拒绝 `..`、绝对路径、保留名（CON/PRN/AUX…）、尾随空格/点、case 冲突；最终路径必须在配置根之下（`Path.GetFullPath` 后前缀比较）；拒绝沿 junction/symlink/reparse point 越界；路径总长按 240 字符预检（Windows MAX_PATH 见 [12]，同时启用 `longPathAware` + `core.longpaths`）。 |
| 安装与更新 | 代码签名（EV 证书，避免 SmartScreen 拦截）、MSIX 签名、更新包签名校验与回滚、降级保护；安装不要求管理员（用户级）。 |
| 权限 | 不以管理员运行；不修改系统级 git config；只对用户自己的仓库执行写操作（`safe.directory` 报错时提示而非静默放宽，参考 [7]）。 |

### G. 可观测性与可运维（草案只有一句"留痕日志"）

- **日志分级**：Serilog，`Verbose/Debug/Information/Warning/Error/Fatal`；按类别分文件（`host`、`remote-sync`、`local-scan`、`git-worker`、`audit`）；滚动 10MB × 30 天；控制台/文件/（可选）ETW。结构化字段：`job_id`、`repo_id`、`component_id`、`request_id`、`duration_ms`、`http_status`、`exit_code`。
- **健康上报协议**（状态栏数据源）：每个子系统发布 `health{name, state: OK|DEGRADED|FAILED, lastSuccessAt, lastError, restarts, queueDepth}`；Host 聚合为快照，UI 通过 WebMessage 事件订阅（≤1Hz 节流）。对应 drawio L107"状态栏显示后台几个进程有没有问题"——**用这份快照实现，而不是靠猜进程是否存活**。
- **诊断包**（一键导出，脱敏）：日志、脱敏配置、`schema_version`、`VACUUM INTO` 的 DB 快照、`git --version`、OS/WebView2 版本、最近 200 条 `job`/`sync_run`、最近 50 条 `operation_log`。用于远程排障，无需远程桌面。
- **崩溃转储**：托管崩溃用 `DOTNET_DbgEnableMiniDump=1` + `DOTNET_DbgMiniDumpType=4` + 固定目录；GitWorker 与 WebView2 的原生崩溃用 WER LocalDumps；转储文件只保留最近 N 个（大小上限），默认不上传。
- **遥测（可选）**：默认关闭；若开启仅上报匿名计数（启动、同步成功率、错误码分布），不含仓库名/路径/用户名；需用户显式同意（决策 Q14）。

### H. 可测试性（草案为零，这是"能不能交付"的关键）

| 层 | 方案 |
|---|---|
| 远程契约测试 | WireMock.NET 回放 Gitea 响应：分页 `Link`/`x-total-count`、401/403/429/5xx、超时、半包、慢响应；断言退避与熔断 |
| 端到端 | 真实 **Gitea 单二进制**（`gitea.exe web`，内置 SQLite）作为测试 fixture：建组织、灌 200～4000 个仓、打 Tag、推 commit；Windows CI 可直接跑（Gitea 有 Windows 发行版） |
| 本地 git 状态矩阵 | 脚本生成 ≥25 种仓态：`SYNCED/AHEAD/BEHIND/DIVERGED`、无 upstream、detached HEAD（切 Tag 后）、dirty/untracked、merge/rebase/cherry-pick 进行中、shallow、LFS、`index.lock` 被占、object 损坏、无 `.git`、submodule、中文/空格/长路径、junction |
| 判定器测试 | 状态判定纯函数 + 表驱动 + 与"真值机"（直接跑 git 命令）对拍；property-based 随机生成提交图 |
| 时间与调度 | `IClock` 注入 + 可手动推进的 fake clock；断言 15s/10min/退避曲线 |
| 性能基线 | 合成 4000 仓元数据 + 400 个本地仓（`git init` + 少量 commit），断言："本地 400 仓指纹轮询 <300ms"、"稳态远程增量 <10s"、"看板首屏 <1s"、"DB 批写 <5ms/次"；夜间跑 |
| CI | `windows-latest` + .NET 10 SDK + `dotnet test` + `dotnet publish` + MSIX 打包；PR 门禁：单测 + 契约测试 + 静态分析（`TreatWarningsAsErrors`、Roslyn analyzers、`dotnet list package --vulnerable`） |
| 手工验收 | 两台机器、一个组织、真 Gitea：断网、代理、token 过期、磁盘满、双开、RDP 双会话、强杀进程 |

### I. 容量与资源预算（建议写入 NFR）

| 项 | 预算 |
|---|---|
| SQLite 库大小 | <100MB（4000 仓 / 12 万 ref / 400 本地状态） |
| Host 常驻内存 | <300MB（不含 WebView2）；WebView2 另计 200～400MB |
| 空闲 CPU | <2%（指纹轮询 + 1 次/10min 水位） |
| 全量对账 | 每日 1 次，P95 ≤5min；可续传；覆盖率 100%，失败清单可见 |
| 首启（400 仓已克隆） | ≤30s 可用看板（先本地、后远程；渐进渲染） |
| 克隆队列 | 并发 2～3 个 `git clone`；每仓前检查磁盘余量（预留 ≥ 单仓最大体积 ×2）；LFS 仓库走 `git lfs` 并显示进度 |
| WebView2 渲染 | 100 张卡片虚拟滚动（4000 远程组件的场景下必须分页/虚拟化；UI 评审已另定，这里只给数据面约束：一次查询 ≤500 行） |

### J. 数据落点与目录布局（避免"装到 Program Files 写不进去"）

```
%LOCALAPPDATA%\MbdGitTool\
  app.db / app.db-wal / app.db-shm   # SQLite，禁止放网络盘/漫游目录
  secrets.dat                        # DPAPI 密文
  config.json                        # 非敏感配置（不含 token）
  logs\  audit\  dumps\  diag\
<用户配置的 workspace 根>\
  CF325F_..._Design\ ...             # 由工具克隆/管理；写 .mbdgit.workspace 标记文件
  .trash\<timestamp>\                # 删除回收站（同卷 rename，秒级）
```

---

## 阻断项 P0 / 高危 P1 / 中低危 P2

> 说明：证据优先给两份转写的行号；`参考[n]` 指向文末外部核查链接。

### P0（阻断：不解决则架构无法冻结）

| ID | 严重级 | 证据+行号 | 问题 | 后果 | 建议 | 需用户决策+可选项 |
|---|---|---|---|---|---|---|
| **ARC-01** | **P0** | docx L59-60（多独立常驻进程/完全解耦）、L61/L79/L95（进程1/2/3）、L105-107（聚合在进程3内）；drawio L4-6（泳道"进程1/2/4"）、L16（边标签"进程3"）、L27-28（独立"聚合→进程4"）、L8（进程1 取 build result 入表1） | 用"职责名词"当进程边界：3 vs 4 个进程两套事实源；CI 归属矛盾（docx L82/L87 进程2 vs drawio L8 进程1）；聚合归属矛盾（进程内规则 vs 独立进程）；对比结果落表矛盾（docx L112 表3 vs drawio L26 表2）。且没有任何故障隔离论证 | 进程边界冻结不了 → IPC/DB 写权/日志/部署全跟着返工；CI 可能双写；聚合进程无输入边（drawio 只有一条来自"后端开始"的边，与 05 号评审 P0-02 同一处）；"常驻多进程"还会叠加 WebView2 自身的多进程，资源与排障成本翻倍 | 进程数按**故障域**而非职责定：v1 用 **Host + GitWorker 两个 OS 进程**；RemoteSync/LocalScan/StatusCalc 作为 Host 内 `BackgroundService`；聚合是纯函数/视图，不设进程；CI 采集唯一归属远程侧，v1 关闭 | **Q1**：A=2 进程（推荐）；B=3 进程（UI 独立）；C=4 进程但必须补 IPC 与聚合输入契约。**Q5**：CI v1 关闭/仅占位（推荐）还是保留只读 |
| **ARC-02** | **P0** | docx L60（异常自恢复）、L157（任意进程崩溃自动重启）；drawio 无监督者/无退出/无单实例/无首启门控（L34-39 只有"开始→认证→后端/前端"）；docx 全文无 IPC 定义 | 进程治理整体缺失：谁监督谁、重启上限/退避/熔断、Host 与 GitWorker 的父子关系、关窗后后台是否继续、单实例互斥、RDP 多会话/同工作区双开、退出清理（孤儿 git 进程）全部未定义。"自动重启"没有主语 | 崩溃后进入重启风暴（无熔断）；双实例双写 DB/双克隆；Host 退出留下半个 clone 与孤儿 `git.exe`；UI 与后台生命周期错位（关了窗数据还在变或再也不变） | Host 作父进程 + Job Object（`KILL_ON_JOB_CLOSE`）；重启 1s→2s→4s→…→60s，5 分钟内 3 次失败熔断降级并上报状态栏；**单实例**：`%LOCALAPPDATA%` 下命名 Mutex + 管道名；worktree 级 `.mbdgit.lock` 文件锁；退出流程：停调度 → 取消 job → 杀进程树 → checkpoint DB | **Q2**：A=托盘随登录自启、关窗退到托盘（推荐）；B=Windows 服务 + UI 客户端（Session 0 拿不到映射盘/DPAPI，需额外设计）；C=仅前台运行 |
| **ARC-03** | **P0** | docx L24-25（本地结构化数据库）、L77（进程1写）、L93（进程2写）、L112（进程3写）；docx L23"高并发"；drawio L8/L14/L26（表1/表2 双写） | 三/四个进程**同时写同一个嵌入式库**，且把"高并发写"当卖点。所有候选库都不支持多进程并行写：SQLite 是"多读+单写"，WAL 还要求所有进程同主机本地盘（网络盘/UNC 直接失效，参考[1]）；写写冲突以 `SQLITE_BUSY` 表现；SQLite < 3.51.3 存在 WAL 多连接/多进程同写触发的损坏缺陷（参考[1] §11）；LiteDB shared 模式每次操作开关文件、靠命名互斥（参考[4]）；DuckDB 多进程写直接锁失败（参考[5]） | 线上典型故障：`database is locked`、偶发"看板数据倒退"、极端情况库损坏只能重建；三处写者让"哪张表谁负责"的规则不可验证；网络盘/漫游配置直接不可用 | 采用**单写者**：只有 Host 持有可写连接，写命令经 `Channel` 串行批提交（200ms/500 行）；GitWorker 永不连库；读用 `Mode=ReadOnly` 连接池；DB 放 `%LOCALAPPDATA%`（禁止网络盘/漫游目录）；启动 `quick_check`，DB 明确定义为"可重建缓存"；锁 bundled SQLite ≥3.51.3 | **Q4**：A=单写者 Host（推荐）；B=允许各进程直写（不推荐，需引入外部锁服务） |
| **ARC-04** | **P0** | docx L163「高性能嵌入式数据库（替代笨重Postgres、优于SQLite）」；docx L114「极简五表」；全文无 DB 候选/评价标准/结论/版本/schema 版本 | 选型未闭环，且结论性口吻（"优于 SQLite"）无依据；同时"五表"只给 3 张（见 ARC-05）。数据量测算：4000 仓 + 约 12 万 ref + 400 本地状态 + 100 组件 ≈ <50MB、写 QPS <10——SQLite 根本不是瓶颈，却要求"替代/优于" | 开发无法选库 → 数据访问层无法开工；若真按"高性能库"引入 DuckDB/LMDB，会得到"单写者 + 无迁移生态 + 运维复杂"的负收益；需求评审无法验收"优于" | 改写为可验收选型约束（单文件、ACID、WAL 并发读、schema 版本化迁移、热备份、零服务、可重建），结论选 **SQLite WAL + Microsoft.Data.Sqlite + Dapper**；对比表见正文 C.1（DuckDB 单写者/偏 OLAP[5]、LiteDB 跨进程弱[4]、Realm 已弃用[6]、LMDB 无 SQL 生态） | **Q3**：A=SQLite WAL（推荐）；B=LiteDB（不推荐）；C=其他（需给出理由与迁移方案） |
| **ARC-05** | **P0** | docx L114-132（"五表"实为 3 表）；表2 L122-127 无 component/repo 归属列、无主键；表3 L128-132 无组件键；表1 L116-121 无仓库全名/URL/默认分支；docx L68-69 要"所有分支最新 commit、Tag→commit"但字段只有单个 `latest_commit`/`tag_list`；drawio L14"记录所有 commit"；drawio L26 对比结果写表2 | 数据模型无法支撑状态计算：表 1 与表 2 无法按仓库关联；"当前分支的远程 commit"无处存；Tag→commit 映射只能塞 JSON；无 CI→commit 关联；无任务/审计/配置/同步运行表；无索引/外键/时间戳/软删除/版本号；drawio 的"所有 commit 落表"会造成千万行膨胀 | 状态判定拿不到输入 → 只能靠"再跑一次 git"补，回到性能死循环；批量操作部分失败无记录 → 无法重试/无法审计；升级无 schema 版本 → 数据迁移不可控 | 按正文 C.3 建 8 张业务表 + 治理表（含 `job/job_item/operation_log/sync_run/app_setting`），补齐 PK/UK/FK/索引/`observed_at`/`is_deleted`/`schema_meta`；commit 历史不落表，只存 head/upstream sha + ahead/behind；每行带 `data_source` 与 `is_stale` | 无独立决策：组件键随 01-Q1 冻结、状态枚举随 01-Q2/02 定稿；ER 草案见正文 C.3 |
| **ARC-06** | **P0** | docx L63（所有 4000+ 仓）、L66-70（分支列表/Tag 列表/Tag→commit/每分支最新 commit）、L72（6h 全量）、L73（单次 120s 超时）、L74（失败重试 2 次）、L75（仅更新变更仓库） | 请求量与超时矛盾：按草案字段逐项取需 8000～48000 请求（见 D.1 表），120s 仅在"每仓 1 次 refs + 并发≥8 + 平均延迟≤80ms"的乐观路径成立；"仅更新变更"缺判定手段（Gitea 列表端点未提供稳定的"自某时刻以来"过滤参数，以实例 Swagger 为准；`ETag/If-None-Match` 不能当通用增量保证，参考[3]）；"整轮 120s 超时"把慢仓/429 变成整轮失败；"失败重试 2 次"把负载放大 3 倍 | 每 6h 一轮大概率整轮失败 → 看板长期 stale 且无提示；重试风暴打到 Gitea；没有续传，一次网络抖动浪费 4000 次请求 | 改成**可续传 job + 三级采集**：仓库清单水位（80 请求/10min）→ 仅 `updated_at` 变化的仓取 refs（1 请求/仓）→ 每日全量对账（2～5min，无 120s 硬超时，per-request 15s + 全局软截止 + 断点续传）；每轮写 `sync_run`（覆盖率/失败清单），覆盖率<100% 标 stale；并发闸门 6～8 + 429/403/5xx 退避抖动；删除"120s 单次超时"需求 | **Q6/Q7**：A=接受"全量对账 P95≤5min、分阶段进度、可续传"（推荐）；B=坚持 120s（需把范围缩到 ≤800 仓或改增量）；C=缩到 1h 水位 + 6h 全量 |
| **ARC-07** | **P0** | docx L79-91（进程2 30s/次、只遍历本机已克隆组件/100+）、L56（状态刷新秒级感知）、L89（30s）、L109（进程3 10s）；drawio L38（首启动展示进程2 遍历进度） | 本地全量轮询不可行：400 仓 / 30s = 13.3 仓/秒；Windows 上 `git.exe` 启动 20～60ms、`git status` 在 MBD 大仓 150ms～2s（模型/二进制文件多 + 杀软实时扫描），串行需 60～800s/轮，8 路并发也撞 IO/AV 瓶颈；且与"秒级感知"直接矛盾。首启把"遍历"当长任务但无进度协议与门控 | 30s 周期永远追不上 → CPU/磁盘持续满载、UI 卡顿（与 docx L54 冲突）、笔记本耗电与发热投诉；首启黑屏数分钟被当崩溃；"秒级感知"无法验收 | 改为**两层**：L1 指纹轮询（15s，只 stat 6 个文件，<100ms）+ L2 仅指纹变化仓跑一次 `git --no-optional-locks status --porcelain=v2 --branch`（参考[7]）+ `FileSystemWatcher`（设 `InternalBufferSize` 并处理溢出，参考[15]）做秒级触发 + 热仓 5s/冷仓 6h 的分档 cadence；"秒级"只承诺本地变更（P95≤5s），远程按 Q6 的 SLO | **Q8**：A=分档 SLO（本地 ≤5s、远程 ≤10min/≤6h，推荐）；B=坚持全链路秒级（需 webhook/中继服务，超出单机工具范围） |
| **ARC-08** | **P0** | docx L82/L87（进程2 读本地 + 调 CI API，采 `Pending/Pass/Fail`）；drawio L8（进程1 取 build result 入表1）；docx L43-45/L87 枚举不一致（Building vs Pending）；docx L126 表2 仅单个 `ci_status`；背景交代 v1 CI 只预留 UI | CI 采集归属冲突（两个"唯一负责"），且 CI 是**远程**数据却塞进本地进程；量级：400 仓 × 2 次/分 = **48000 请求/小时**，不可行；数据模型只有单个 `ci_status` 字段，无法表达"每次 commit 的 CI 结果"（能力 3 要求自动绑定每次 commit）；枚举不一致 | v1 按草案实现 CI 必然拖垮轮询或打到限流；v2 接真 CI 时表结构与调用链全返工；"CI 预警"无法验收 | v1：**关闭 CI 采集**，只建空表 + 前端占位 + 固定数据契约（`ci_status(repo_id, commit_sha, provider, state, run_id, run_url)`）；CI 归属远程侧（v2）。v2 策略：仅当 HEAD sha 变化才查（Gitea commit statuses 端点，参考[13]），PENDING 时 30s→1m→5m 退避，按 sha 缓存 | **Q5**：A=v1 关闭/仅占位（推荐，与背景一致）；B=v1 只读 commit status（需接受额外 400 请求/轮的预算） |
| **ARC-09** | **P0** | drawio L96「登录认证，配置参数，配置完成后保存为本地json」；docx 全文无凭据/加密/TLS/代理/自签证书/日志脱敏/破坏性确认/审计/安装包签名与更新校验（L152-158 仅"留痕日志"） | 安全基线整体缺失：token 明文 json；未定义 HTTPS/企业代理/自签 CA；git 侧可能用 `http.sslVerify=false` 兜底；日志会带 Authorization/remote URL 凭据；破坏性操作（SYNC 覆盖、批量删除、Reset）无 dry-run/确认/回收站/审计；安装包与自动更新无签名校验 → 供应链投毒面 | token 泄露（同机其他用户/备份/排障日志外发）；中间人；误操作丢未推送代码且不可追溯；被替换的更新包可执行任意代码 | 见正文 F：DPAPI/Credential Manager（参考[10][11]）、token 最小 scope + 读写分离、schannel/自定义 CA、日志统一脱敏、破坏性操作 dry-run+二次确认+`.trash`+审计、安装包 EV 签名 + 更新签名校验 | **Q9/Q10**：凭据方案 A=DPAPI（推荐）/B=Credential Manager/C=两者；token A=只读+按需写（推荐）/B=all（禁止） |

### P1（高危：会返工/现场事故/验收争议）

| ID | 严重级 | 证据+行号 | 问题 | 后果 | 建议 | 需用户决策+可选项 |
|---|---|---|---|---|---|---|
| **ARC-10** | P1 | drawio L38（前端首启等"进程2 遍历进度"、明确"需要耗时"）、L34-35 与 L75-77（"开始→认证"后后端/前端才启动）、L96-1（菜单栏登录）、L97（标题栏显示总组件数/仓库数 = 默认数据已就绪）；docx 无首启/门控定义 | 首启时序与门控缺失：只把"遍历"当长任务，漏了建库/迁移、首轮远程同步（可能数分钟）、克隆队列；认证在后端启动之后 → 无 token 时后端应以 degraded 运行；无"表1 未就绪时看板显示什么"的定义 | 首启黑屏/白屏、进度停在 30% 无解释、用户强杀导致半初始化 DB；认证失败时 UI 与服务状态错位 | 首启 = 可取消的分阶段任务：环境检测→建库/迁移→本地发现→远程清单→（可选）全量对账→可用；每阶段有进度与预计剩余；认证失败进入"只读本地"模式并在状态栏常驻提示 | 否（可直接设计），但"首启是否允许只读本地模式"影响 Q2/Q6 |
| **ARC-11** | P1 | drawio L9（遍历本地指定文件夹的直接子文件夹）、L11-12（判断是否 git 仓且 remote 含 Gitea 组织同名仓）、L14（列为管控文件夹建表2） | 本地发现设计粗糙：根目录/深度未定义（选到 `D:\` 或用户目录会遍历爆炸并误纳无关仓库）；每目录一次 `git remote` 判定 = 400 次进程启动；`safe.directory` 在他人/共享盘仓上会拒绝（参考[7]）；remote URL 可能内嵌凭据（需脱敏）；junction/symlink 可越界；只按"同名"匹配不做 host/org 校验会被伪造仓欺骗 | 误把个人仓库纳入管控并执行批量操作（可能 push/reset）；性能差；共享盘/多用户场景报错难解释；安全边界被同机恶意目录突破 | 显式配置 workspace 根（可多个，默认禁止盘符根/用户根），扫描深度 ≤2；只读探测用 `git -C <path> remote get-url origin`（参数数组）；校验 remote host ∈ 白名单且 org 匹配；`-c safe.directory=<path>` 仅对已确认路径、且记录告警；拒绝跟随 reparse point；仓库名与路径做 canonical/正则校验 | **Q11**：A=显式多根 + 深度 2（推荐）；B=单根；C=允许盘符扫描（不推荐） |
| **ARC-12** | P1 | docx L32/L145-150（批量 Clone/Checkout/切 Tag/Pull/删除、单仓 Commit/Push/Reset）；drawio L58-3.5（SYNC 覆盖本地）、L103（CREATE dryrun）；docx 无并发/互斥/部分失败定义 | 无操作队列、无每仓互斥、无部分失败模型、无幂等：双击批量 Pull / 用户 IDE 同时 git / 两个前台操作叠加 → `index.lock` 冲突、半完成状态；SYNC"用远程覆盖本地"会丢未提交/未推送内容（destructive）而无影响预览；批量无 task_id 无法重试/取消 | 一次误点丢代码；批量部分成功但 UI 全绿；同一仓并发操作互相覆盖；无法给出"哪个仓失败、为什么" | 所有写操作进 **job 队列**（`job/job_item`）：幂等键 + 每仓锁（进程内 `SemaphoreSlim` + `.git/index.lock` 冲突重试分类）+ 逐仓结果 + 可取消 + dry-run + 部分失败可见可重试；SYNC/Reset 前 read-through 真实状态并展示将丢弃的 commit；删除进 `.trash` | **Q12**：删除 A=`.trash` 30 天（推荐）/B=永久删除；部分失败建议不整体回滚、只报告（若要求整体回滚需另立决策并定义补偿） |
| **ARC-13** | P1 | docx L158（仅一句"状态变更留痕日志"）；drawio L107（状态栏显示后台进程是否正常） | 可观测性无设计：无日志分级/滚动/保留/脱敏；无诊断包；无健康上报协议（状态栏靠什么数据）；无崩溃转储；无请求/任务关联 ID；无法远程排障 | 现场只能远程桌面；崩溃无堆栈；"进程是否正常"只能靠猜；日志可能泄露 token | 见正文 G：Serilog 分级+滚动 30 天+审计独立 sink；`health` 快照协议（状态字段/最后成功时间/重启次数/队列深度）经 WebMessage 推 UI；一键脱敏诊断包（日志+配置+DB 快照+版本+最近 job）；托管/原生崩溃转储（参考[16]） | 否（可直接设计）；**Q14** 遥测是否可选开启 |
| **ARC-14** | P1 | 全文无测试策略、无 fake 服务、无本地 git 状态矩阵、无性能基线、无 CI 方案 | 不可测：依赖真实 4000 仓 Gitea 才能测；本地 git 状态组合（detached/dirty/merge 中/shallow/LFS/锁冲突）无夹具；"120s/30s/秒级"无测量方法 | 无法回归、无法验收性能、上线即靠人肉试错；缺陷定位以天计 | 见正文 H：WireMock.NET 契约测试 + 真实 Gitea 单二进制 E2E + ≥25 态本地 git 矩阵脚本 + 判定器对拍/property-based + `IClock` 假时钟 + BenchmarkDotNet 夜间基线与预算门禁 | 否；但需要 CI 资源（Gitea fixture/runner）预算 |
| **ARC-15** | P1 | docx L160-163（技术栈：后端 .NET、前端未定、DB 未定）；drawio L96（本地 json）；docx L134（WebView2）；全文无 .NET 版本/WebView2 Runtime/git 版本/LFS/安装签名/自动更新 | 交付链未定义：.NET 版本（.NET 8 已 2026-11-10 停止支持，参考[9]）；WebView2 Runtime 前置与离线安装（参考[2]）；git 版本/LFS/GCM 依赖；自包含 vs 框架依赖；MSIX vs MSI；代码签名与自动更新；长驻应用 WebView2 运行时更新需重启（参考[2]） | 现场装不上/启动即崩；离线企业环境无法补依赖；更新把正在 clone 的任务打断；SmartScreen 拦截 | .NET 10 LTS（参考[9]）+ self-contained x64；WebView2 启动检测 + 随包离线 Standalone 安装器（参考[2]）；git ≥2.40 检测并引导（决策 Q16）；MSIX 用户级 + EV 签名 + 更新前等空闲/备份 DB + 失败回滚；发布物清单写入 NFR | **Q13**：A=MSIX（推荐）/B=MSI/C=绿色 zip；**Q16**：git A=检测引导（推荐）/B=捆绑 PortableGit（GPL 义务） |
| **ARC-16** | P1 | docx L32（批量 Clone）、L55（百级组件）、L148（整体删除）；drawio L38（首启耗时长）；全文无磁盘预算/克隆并发/LFS/进度 | 容量与克隆队列无设计：MBD 仓含 Simulink 模型/生成代码/二进制，单仓可能数百 MB～数 GB；100 组件×4 仓可达数十～数百 GB；克隆并发过高打爆磁盘/网络；LFS 仓库无 `git lfs` 处理；无余量检查 | 磁盘写满后 git 半完成、DB 与磁盘不一致；克隆风暴拖垮同网段 Gitea；LFS 指针文件被当源码 | 克隆并发 2～3 + 队列 + 每仓前磁盘余量检查（≥ 预估体积×2）+ 进度/取消；磁盘预算写进 NFR；检测 `.gitattributes` LFS 并显示"需 Git LFS"；超大仓可评估 partial clone/`--filter=blob:none`（参考[14]，需服务端与用户接受"按需拉取"语义）；提供按组件清理与总占用统计 | 否；**Q6** 需给单组件体积上限假设（建议向用户采集真实仓库体积分布） |
| **ARC-17** | P1 | docx L72（6h/次）× L55（百级组件）→ 单机请求量；全文无限流/退避/多客户端/服务端压力协商；Gitea 开源版默认不强制 API 限流，但限流常由部署方反向代理/网关施加（参考[17]，非官方整理，需以现场为准） | 未考虑"N 个开发者 × 每 6h × 4000 仓"对 Gitea 的聚合压力（100 人 ≈ 18 请求/秒持续），也未设计 429/403 退避、并发闸门、断点续传、失败降级；Gitea 服务器管理员可能禁用/封禁 | 工具在团队规模下反而拖垮代码服务器；被防火墙/WAF 拦截后表现为"工具坏了" | 客户端侧：并发 6～8、令牌桶（如 ≤10 req/s）、429/403/5xx 指数退避、覆盖率上报；产品侧：与 Gitea 管理员约定配额/白名单；中长期用组织级 webhook 或共享缓存服务替代全员轮询（v2） | **Q6**：是否向 IT/Gitea 管理员申请配额与白名单（建议：是） |
| **ARC-18** | P1 | docx 表1 `update_time`（L121）、表2 `scan_time`（L127）、表3 无时间戳（L128-132）、L158 日志；drawio L8"持续不断"、L26"记录对比结果" | 时间语义未定义：全部无时区/UTC 约定（Gitea 返回 RFC3339 UTC、文件 mtime 是本地时间、`git log --date` 可配）；新鲜度/stale 判定、ahead/behind 时间基准、日志排序都依赖时间；无单调时钟保护（系统时间回拨会让"stale"误判） | 跨时区/夏令时/系统改时间导致状态闪烁、同步水位错乱、审计不可信 | 统一 UTC ISO-8601（含 `Z`）落库；每条观测记录 `observed_at` + `monotonic_ticks`；stale 判定用"距上次成功同步的单调时长"；日志与 UI 呈现时再转本地时区 | 否（可直接设计） |
| **ARC-19** | P1 | docx L50（"双为准"）、L26-28（组件双数据源：Gitea/CI API + 本机 git）、drawio L14/L26（先写表2 再对比、对比结果再写表2） | "双数据源"只有口号没有规则：DB 何时是事实源、何时必须回读磁盘/Gitea 未定义；drawio 流程会把"对比结果"写回表2（缓存表），导致缓存与事实混住；无启动对账、无外部改动的失效策略 | 用 DB 旧值做破坏性操作（例如 SYNC/删除判断失误）；手动改文件后看板不重算；DB 与磁盘长期漂移 | 明确不变量：**DB 是缓存，磁盘/Gitea 是事实源**；任何破坏性操作前 read-through；启动与每 N 分钟做磁盘↔DB 对账（发现差异即标 dirty 重扫）；对比结果只写 `repo_status`，绝不写回 `local_state`/`remote_ref` | 否（可直接设计） |

### P2（中低危：可维护性/交付卫生）

| ID | 严重级 | 证据+行号 | 问题 | 后果 | 建议 | 需用户决策+可选项 |
|---|---|---|---|---|---|---|
| **ARC-20** | P2 | docx L11（Windows 桌面）、无路径/盘符/网络盘约束；drawio L9（指定文件夹） | 长路径（`CF325F_ChanganSigProcgBascFct_Design/.git/objects/...` 易超 260）、中文/空格路径、UNC/网络盘（WAL 明确不支持网络文件系统，参考[1]）、漫游目录（DB 不应放 Roaming） | 克隆/checkout 失败且报错难懂；网络盘上 DB 频繁 `SQLITE_BUSY`/损坏 | 启用 long path（`longPathAware`）+ `core.longpaths=true`；路径预检 240 字符；DB 固定 `%LOCALAPPDATA%`；检测到网络盘/漫游目录时拒绝并提示（参考[12]） | 否；Q11 一并确认工作区是否允许网络盘 |
| **ARC-21** | P2 | docx L109-112（10s 全量重算并写表3）、drawio L27-33（独立聚合进程） | 每 10s 全量重算并重写 400 行造成 WAL 写放大与 UI 无用刷新；聚合独立进程纯成本 | 磁盘写放大、SSD 寿命、UI 频繁重排；多一个进程要部署/监控 | dirty-set：只重算受影响组件；只写值变化的行；聚合做纯函数 + 视图/内存快照；删除"聚合进程" | 否 |
| **ARC-22** | P2 | docx L134-150（看板 4000 仓/100 组件）、L55；drawio L58-3（组件卡片列表） | 查询与渲染未按规模设计：4000 远程仓库 + 100 组件卡片需要分页/虚拟滚动；无索引规划（见 C.3）；无慢查询预算 | 首屏卡顿、内存膨胀；"网页级动效"与 4000 行数据叠加恶化（UI 评审另定，但数据面须限流） | 一次查询 ≤500 行 + 键集分页；索引见 C.3；UI 侧虚拟列表；首屏只查 `component` + 聚合状态 | 否 |
| **ARC-23** | P2 | docx L163（DB 未定）、全文无 schema 版本/迁移/备份/损坏重建 | 升级/迁移/备份/重建策略缺失；"极简五表"未来必然扩表 | 升级后数据丢失；库损坏只能全量重扫且无提示；无法回滚版本 | `PRAGMA user_version` + 顺序迁移 + 迁移前 `VACUUM INTO` 备份 + 失败自动回滚 + 启动 `quick_check` + 一键重建（缓存可重建是优势，需写进需求） | **Q13** 一并确认是否 v1 支持自动更新（影响迁移回滚要求） |
| **ARC-24** | P2 | docx L158（留痕日志）；drawio L96（json 配置） | 日志/审计的隐私与留存未定义：可能记录仓库名、本地绝对路径、Windows 用户名、remote URL（含凭据）；无保留期与清理 | 合规风险；日志膨胀；排障包外发泄露 | 脱敏过滤器 + 保留期（运行日志 30 天/审计 90 天）+ 诊断包默认脱敏 + 用户可见"清除日志" | 否 |
| **ARC-25** | P2 | 全文无遥测 | 无产品改进数据通道（可选） | 现场问题只能靠个案反馈 | 可选遥测，默认关闭，仅匿名计数，显式同意（参考 GDPR/企业策略） | **Q14**：A=默认关闭可选开启（推荐）/B=不做 |
| **ARC-26** | P2 | docx L150（单仓 Reset/Commit/Push）、drawio L58-6（单仓操作）；全文无命令构造规则 | git 调用安全：若用字符串拼接命令/未加 `--` 分隔，仓库名或分支名可注入参数；环境变量（`GIT_DIR`/`GIT_WORK_TREE`/`GIT_CONFIG_*`）可能被外部污染导致误操作到别的仓 | 误操作到错误仓库/分支；恶意仓库名触发参数注入 | 只用 `ProcessStartInfo.ArgumentList`；固定 `-C <path>` 且路径 canonical；分隔符 `--`；清理/白名单 `GIT_*` 环境变量；`GIT_TERMINAL_PROMPT=0`（防挂起） | 否 |
| **ARC-27** | P2 | docx L160-163（技术栈） | 分发体积与启动时间未定：self-contained .NET 约 70～100MB + WebView2（Evergreen 共享，固定版 >250MB，参考[2]）；首次启动 JIT/ReadyToRun | 用户嫌大/启动慢；企业分发带宽 | .NET 10 self-contained + ReadyToRun + 裁剪（注意反射/序列化）；WebView2 用 Evergreen（不进包）；给出体积与冷启动预算 | 否 |
| **ARC-28** | P2 | drawio L96（配置）；全文无签名/分发 | 代码签名、SmartScreen、企业分发（SCCM/Intune）、卸载数据保留策略未定义 | 首次运行被 SmartScreen 拦截；企业无法批量部署；卸载误删用户数据 | EV 代码签名 + MSIX 签名；提供 MSI 变体给 SCCM；卸载保留 `%LOCALAPPDATA%` 数据并询问 | **Q13** 一并确认 |

---

## 需用户决策的问题（按优先级）

> 前 6 条不决策则无法进入架构设计/编码；建议全部在 SRS v0.2 前拍板，并写入 ADR（含结论/日期/决策人）。

| # | 问题 | 可选项 | 推荐 | 影响的阻断项 |
|---|---|---|---|---|
| **Q1** | v1 进程模型 | A=Host + GitWorker 两进程（UI 同 Host）；B=三进程（UI 独立，Host+GitWorker+UI）；C=四进程（按 docx/drawio，但必须补 IPC/监督/聚合输入契约） | **A** | ARC-01/02 |
| **Q2** | 常驻形态与自启 | A=托盘应用，登录自启，关窗退到托盘；B=Windows 服务 + UI 客户端；C=仅前台 | **A**（B 在 Session 0 拿不到映射盘/DPAPI，需额外设计） | ARC-02/15 |
| **Q3** | 数据库选型 | A=SQLite WAL；B=LiteDB；C=DuckDB/Realm/LMDB（不建议） | **A**（锁 ≥3.51.3，单写者） | ARC-03/04 |
| **Q4** | DB 写入权 | A=仅 Host 单写者；B=各进程直写 + 外部锁 | **A** | ARC-03 |
| **Q5** | v1 的 CI 边界 | A=关闭采集，仅空表 + UI 占位 + 数据契约；B=只读 commit status；C=完整 CI 联动 | **A**（与"CI 后续完善"背景一致） | ARC-01/08 |
| **Q6** | 远程新鲜度 SLO | A=仓库清单水位 10min + refs 30min + 全量对账每日；B=全量每 6h；C=要求 ≤1min（需 webhook/中继） | **A**；若企业允许 Gitea 回连工作站，再加 webhook 升级到秒级 | ARC-06/17 |
| **Q7** | 全量同步预算与超时 | A=取消 120s 硬超时，改可续传 job（P95≤5min）；B=保留 120s 但缩范围；C=不设全量，只增量 | **A** | ARC-06 |
| **Q8** | "秒级感知"如何验收 | A=本地变更 P95≤5s、远程按 Q6；B=所有变更 ≤5s（需服务端事件）；C=去掉秒级表述 | **A** | ARC-07 |
| **Q9** | 凭据存储 | A=DPAPI（CurrentUser）；B=Windows Credential Manager；C=两者（钥匙串 + 应用级加密） | **A**（B 作为可选增强） | ARC-09 |
| **Q10** | Gitea token 权限 | A=同步用 `read:repository`，push 用独立 `write:repository`；B=单一 `all`；C=OAuth 设备流 | **A**；若企业有 SSO 再评估 C | ARC-09 |
| **Q11** | 工作区根与发现范围 | A=显式多根 + 深度 ≤2；B=单根；C=允许盘符级扫描 | **A** | ARC-11/20 |
| **Q12** | 删除语义 | A=本地 `.trash` 保留 30 天；B=本地永久删除；C=允许删除 Gitea 远程仓（不建议） | **A**，v1 明确禁止 C | ARC-09/12 |
| **Q13** | 安装/更新/分发 | A=MSIX 用户级 + 自动更新；B=MSI（企业 SCCM）；C=绿色 zip 手动解压 | **A**（若企业强制 SCCM 则 A+B 双产物） | ARC-15/23/28 |
| **Q14** | 诊断与遥测 | A=本地诊断包 + 遥测默认关闭可选开启；B=仅本地诊断包；C=全关闭 | **A** | ARC-13/25 |
| **Q15** | 多用户/RDP/共享工作区 | A=单用户单实例（per-user DB + 工作区锁）；B=多会话共享同一工作区（需外部锁服务）；C=不支持共享盘 | **A** | ARC-02/11 |
| **Q16** | git 运行时依赖 | A=检测 Git for Windows ≥2.40 并引导安装（含 LFS/GCM）；B=捆绑 PortableGit（GPL 分发义务）；C=两者（优先 A） | **A**，企业离线场景再评估 B | ARC-15 |

---

## 参考（外部核查，链接为评审时实际读取）

1. SQLite WAL 官方文档（WAL 不适用网络文件系统、单写者、`SQLITE_BUSY`、WAL-reset 缺陷修复版本 3.51.3）：<https://www.sqlite.org/wal.html>
2. Microsoft Learn — WebView2 Runtime 分发（Evergreen/固定版、>250MB、不能从 UNC 运行、常驻应用需重启才用新版本）：<https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution>
3. Gitea API Usage（分页 `page/limit`、`MAX_RESPONSE_ITEMS` 默认 50、`Link`/`x-total-count`、token scope、`Authorization: token`）：<https://docs.gitea.com/development/api-usage/>
4. LiteDB Connection String（Direct vs Shared、命名互斥、多进程性能取舍）：<http://www.litedb.org/docs/connection-string/>
5. DuckDB Concurrency（单进程读写；多进程只读；多进程写需 Quack 服务化）：<https://duckdb.org/docs/current/connect/concurrency>
6. MongoDB Atlas Device SDK（Realm）弃用公告（2024-09 弃用、2025-09-30 生命周期结束）：<https://www.mongodb.com/docs/atlas/device-sdks/deprecation/>
7. Git 文档（`--no-optional-locks` 避免抢占 `index.lock`；`safe.directory`）：<https://git-scm.com/docs/git>、<https://git-scm.com/docs/git-config#Documentation/git-config.txt-safedirectory>
8. Gitea Webhooks（仓库/组织/实例级、事件与投递）：<https://docs.gitea.com/1.27/usage/repository/webhooks>
9. .NET 官方支持策略（.NET 8 于 2026-11-10 终止支持；LTS 3 年 → .NET 10 至 2028-11）：<https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core>
10. Microsoft Learn — DPAPI / `System.Security.Cryptography.ProtectedData`：<https://learn.microsoft.com/en-us/dotnet/standard/security/how-to-use-data-protection>
11. Microsoft Learn — Windows Credential Manager `CredWrite`/`CredRead`：<https://learn.microsoft.com/en-us/windows/win32/api/wincred/nf-wincred-credwritew>
12. Microsoft Learn — Windows 最大路径长度限制：<https://learn.microsoft.com/en-us/windows/win32/fileio/maximum-file-path-limitation>
13. Gitea API — 按 ref 查询 commit statuses（CI 采集端点的 v2 依据）：<https://docs.gitea.com/enterprise/api/23/operations/repo-list-statuses-by-ref/>
14. Gitea — Clone filters / partial clone（大仓/LFS 场景可选）：<https://docs.gitea.com/usage/repository/clone-filters/>
15. Microsoft Learn — `FileSystemWatcher.InternalBufferSize`（事件缓冲溢出需处理）：<https://learn.microsoft.com/en-us/dotnet/api/system.io.filesystemwatcher.internalbuffersize>
16. Microsoft Learn — 托管崩溃时收集转储（`DOTNET_DbgEnableMiniDump` 等）：<https://learn.microsoft.com/en-us/dotnet/core/diagnostics/collect-dumps-crash>
17. Gitea 限流说明（社区整理，非官方：开源版默认不设每用户 API 限流，限流由部署方/网关施加，以现场为准）：<https://raw.githubusercontent.com/api-evangelist/gitea/refs/heads/main/rate-limits/gitea-rate-limits.yml>

---

## 附：本报告与其他评审的边界

- 01（需求质量）已覆盖：组件身份规则、状态枚举、颜色语义、需求可验收性 → 本报告只在"导致数据模型/进程边界无法冻结"处引用，不重复批评。
- 02（git/领域语义）已覆盖：状态机、SYNC/切 Tag/批量删除的 git 语义 → 本报告只在"操作队列/互斥/部分失败"处给架构承载方式。
- 04（UI/CI）已覆盖：界面信息架构、CI 契约 → 本报告只给"数据面限流 ≤500 行/次、WebView2 生命周期"约束。
- 05（drawio 执行方案）已覆盖：图面数据链断裂、首启竞态、图集拆分 → 本报告只在"进程数/表归属/聚合输入"处交叉引用（同一处证据，判定角度不同）。


