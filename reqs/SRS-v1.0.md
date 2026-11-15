# MBD 组件全生命周期 Git 工具 — 软件需求规格说明书（SRS v1.0）

| 项目 | 内容 |
|---|---|
| 文档版本 | **v1.0**（2026-11-15） |
| 状态 | v1.0 发布基线；历史 0.x 规格与评审记录归档于 `reqs/archive/`、`docs/archive/` |
| 说明 | 每条需求一个二级标题；需求编号即标题前缀。验收标准以归档 Annex A 与 `docs/m2b-ac-op-gap.md` 为准。 |
| 未实现/待确认 | 见根目录 `README.md` 的“v1.0 已知限制与待确认项” |

> 约定：`P0` = v1.0 不可缺；`P1` = v1.0 应完成；`P2` = 可排 v1.1。状态为功能实现状态；最终 Gate 签收状态以 README 为准。

## FR-CFG-01

- **需求编号**：FR-CFG-01
- **需求名称**：首次启动向导
- **优先级**：P0
- **描述**：本工具应在首次启动时以向导形式逐步采集 Gitea BaseURL、组织名、认证方式（PAT）、本地根目录（v1 恰好 1 个，多根留待 v1.1）与代理设置，每一步输入都即时校验：URL 非法、Token 无效、路径不存在或根目录为盘符根/用户目录根时给出字段级错误并阻止继续；向导允许后退修改，全部步骤完成后把配置写入配置文件。
- **验收标准（AC）**：非法 URL/Token/路径/根目录（盘符根/用户目录根）给出字段级错误；可后退；完成后写入配置
- **关联 AC**：AC-CFG-01、AC-CFG-02、AC-CFG-03、AC-CFG-11
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-CFG-02

- **需求编号**：FR-CFG-02
- **需求名称**：“测试连接”返回认证结果、组织可见仓库数、权限范围
- **优先级**：P0
- **描述**：本工具应在用户点击“测试连接”后返回认证结果、组织可见仓库数与权限范围，并在 Token 权限不足时明确列出缺失权限；上述结果在 Mock 模式与真实 Gitea 两种模式下都必须可验证。
- **验收标准（AC）**：Mock/真实 Gitea 两种模式均可验证
- **关联 AC**：AC-CFG-04、AC-CFG-05、AC-CFG-11
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-CFG-03

- **需求编号**：FR-CFG-03
- **需求名称**：Token 存 Windows Credential Manager
- **优先级**：P0
- **描述**：本工具应把 Gitea Token 存入 Windows Credential Manager，配置文件中只保留对该凭据的引用；日志、命令行与仓库的 `.git/config` 中均不得出现明文 Token。
- **验收标准（AC）**：全量日志扫描无 token；remote URL 无凭据
- **关联 AC**：AC-CFG-06、AC-CFG-07
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-CFG-04

- **需求编号**：FR-CFG-04
- **需求名称**：Token 过期/401 时全局提示并在恢复后自动续跑
- **优先级**：P1
- **描述**：本工具应在 Gitea 返回 401 或判定 Token 过期时给出全局提示并提供重新登录入口，重新认证成功后自动继续此前中断的任务，且整个过程不清空已有数据。
- **验收标准（AC）**：注入 401 后 UI 显示重登入口，重登后任务可重试
- **关联 AC**：AC-CFG-05、AC-CFG-08
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-CFG-05

- **需求编号**：FR-CFG-05
- **需求名称**：代理/自签证书配置（系统代理默认、手动覆盖、证书信任选项）
- **优先级**：P1
- **描述**：本工具应提供代理与自签证书配置：默认跟随系统代理，支持手动指定代理以覆盖系统设置，并提供自签证书信任选项，使内网自签 Gitea 可以连通。
- **验收标准（AC）**：内网自签 Gitea 可连通
- **关联 AC**：AC-CFG-09
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-CFG-06

- **需求编号**：FR-CFG-06
- **需求名称**：配置热更新（组织/根目录变更需确认影响范围并重扫）
- **优先级**：P2
- **描述**：本工具应支持配置热更新：修改组织或本地根目录后，先展示该变更影响的仓库与组件范围，待用户确认后重新扫描，并保证变更后工作台计数正确。
- **验收标准（AC）**：变更后工作台计数正确
- **关联 AC**：AC-CFG-02、AC-CFG-10、AC-CFG-12
- **状态**：v1.1（非 v1.0）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-SYNC-01

- **需求编号**：FR-SYNC-01
- **需求名称**：同步组织仓库清单
- **优先级**：P0
- **描述**：本工具应同步组织仓库清单，逐仓记录 `repo_id`、仓库全名、默认分支、URL、是否空仓、创建时间与更新时间；拉取时按 Gitea 单页上限 50 分页直至取全，并在 UI 展示清单覆盖率（4,000 仓规模下可计算并展示）。
- **验收标准（AC）**：4,000 仓清单覆盖率可计算并在 UI 展示
- **关联 AC**：AC-SYNC-01、AC-SYNC-02、AC-SYNC-03、AC-SYNC-14
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-SYNC-02

- **需求编号**：FR-SYNC-02
- **需求名称**：同步已绑定/可见仓的分支与 Tag
- **优先级**：P0
- **描述**：本工具应只对“本地已绑定 + 工作台当前视图可见”的仓库同步分支列表与 Tag 列表（含 tag→commit 映射），不得为 4,000 个仓全量拉取分支与 Tag；用户收藏扩大“关注”范围属于 v1.1 能力，v1 关注范围为本地已绑定与当前视图可见两者。
- **验收标准（AC）**：请求量与时间在 NFR 预算内；覆盖率展示
- **关联 AC**：AC-SYNC-04、AC-SYNC-05
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-SYNC-03

- **需求编号**：FR-SYNC-03
- **需求名称**：首启立即执行一次组织同步
- **优先级**：P0
- **描述**：本工具应在首次启动时立即执行一次组织同步，此后每日执行全量同步、仓库清单水位每 10 分钟刷新、发生变化仓库的 refs 每 30 分钟刷新；同步失败按指数退避最多重试 2 次，失败写入日志且不阻塞其他职责。
- **验收标准（AC）**：断网首启进入降级态并可重试；部分成功显示覆盖率与失败清单
- **关联 AC**：AC-SYNC-01、AC-SYNC-06、AC-SYNC-07
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-SYNC-04

- **需求编号**：FR-SYNC-04
- **需求名称**：每轮同步记录 `sync_run`（总数/成功/失败/覆盖率/耗时）
- **优先级**：P0
- **描述**：本工具应在每轮同步结束后写入一条 `sync_run` 记录，包含仓库总数、成功数、失败数、覆盖率与耗时；覆盖率低于 100% 时把工作台标记为 STALE。
- **验收标准（AC）**：注入部分失败后工作台出现 stale 提示
- **关联 AC**：AC-SYNC-01、AC-SYNC-03、AC-SYNC-08
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-SYNC-05

- **需求编号**：FR-SYNC-05
- **需求名称**：远程仓删除/改名
- **优先级**：P1
- **描述**：本工具应识别远程仓库的删除与改名：删除的仓用 `archived` 标记并提示，改名的仓用 `renamed_from` 记录原全名并提示；改名须生成组件 ID 迁移事件（旧 ID→新 ID）并等待用户确认，不得静默删除数据行；未绑定仓库进入未绑定清单。
- **验收标准（AC）**：改名/删除场景用例；迁移后工作台无重复组件
- **关联 AC**：AC-SYNC-09、AC-SYNC-10、AC-SYNC-13
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-SYNC-06

- **需求编号**：FR-SYNC-06
- **需求名称**：增量策略以仓库 `updated_at` 水位为主
- **优先级**：P1
- **描述**：本工具应在增量同步中以仓库 `updated_at` 水位为主；ETag/If-None-Match 在服务端可用时使用、不可用时不依赖；并发请求上限默认 6；遇到 429/403/5xx 按退避加抖动重试，不得雪崩。
- **验收标准（AC）**：限流注入不雪崩；无 ETag 的 Gitea 版本仍能增量
- **关联 AC**：AC-SYNC-11、AC-SYNC-12
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-SCAN-01

- **需求编号**：FR-SCAN-01
- **需求名称**：遍历 workspace_root 的直接子目录，识别“git 仓 + origin 匹配 Gitea 同名仓”
- **优先级**：P0
- **描述**：本工具应遍历 `workspace_root` 的直接子目录，识别同时满足“是 git 仓库”且“origin 远程匹配 Gitea 同名仓”的目录；非仓库或不匹配的目录跳过并记录跳过结果。
- **验收标准（AC）**：目录夹具下识别/跳过结果与预期一致
- **关联 AC**：AC-SCAN-01、AC-SCAN-02、AC-SCAN-03
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-SCAN-02

- **需求编号**：FR-SCAN-02
- **需求名称**：采集单仓
- **优先级**：P0
- **描述**：本工具应采集单仓状态：HEAD 形态（分支/tag/游离/空）、分支名、HEAD SHA、upstream、ahead/behind、dirty（已跟踪/未跟踪）、stash、unmerged、进行中操作、shallow 克隆与 LFS 缺失情况。
- **验收标准（AC）**：状态矩阵夹具 100% 覆盖
- **关联 AC**：AC-SCAN-04
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-SCAN-03

- **需求编号**：FR-SCAN-03
- **需求名称**：扫描不得写仓库
- **优先级**：P0
- **描述**：本工具应在扫描时保持只读：调用 git 时使用 `--no-optional-locks` 并禁用 auto-gc/auto-maintenance，不写仓库任何文件；当 `.git/index.lock` 存在时把仓库标记 BUSY，不重试也不删除该锁文件。
- **验收标准（AC）**：外部 git 并发时无锁冲突、无仓库改动（`git status` 前后一致）
- **关联 AC**：AC-SCAN-05、AC-SCAN-06
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-SCAN-04

- **需求编号**：FR-SCAN-04
- **需求名称**：分层扫描
- **优先级**：P0
- **描述**：本工具应采用分层扫描：L0 发现每 10 分钟或事件触发执行，L1 指纹每 15 秒读取 6 个文件 stat 且不启动进程，L2 深扫仅针对指纹变化的仓及 FileSystemWatcher 触发，并发 8 路（热仓 5 秒、冷仓 6 小时）；每仓记录 `last_success_at` 与 `fingerprint`。
- **验收标准（AC）**：400 仓指纹轮询 <300ms；变化 5% 时深扫 ≤4s；无全量 30s git 轮询
- **关联 AC**：AC-SCAN-07、AC-SCAN-08、AC-SCAN-09、AC-SCAN-10
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-SCAN-05

- **需求编号**：FR-SCAN-05
- **需求名称**：关系判定前必须保证 remote-tracking ref 新鲜
- **优先级**：P0
- **描述**：本工具应在判定组件与仓库关系前保证 remote-tracking ref 新鲜：远程清单水位 10 分钟、refs 30 分钟或按需刷新、支持手动刷新，并在任何写操作前强制执行 `git fetch`；UI 按来源分别显示数据时间。
- **验收标准（AC）**：远程变更 ≤10min 可见；UI 分源显示数据时间
- **关联 AC**：AC-SCAN-11
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-SCAN-06

- **需求编号**：FR-SCAN-06
- **需求名称**：长路径、大小写、reparse point、网络盘检测与告警
- **优先级**：P1
- **描述**：本工具应检测长路径、大小写敏感性、reparse point 与网络盘并给出告警；用户目录路径含中文或空格时必须能正常扫描。
- **验收标准（AC）**：兼容性夹具
- **关联 AC**：AC-SCAN-03、AC-SCAN-12、AC-SCAN-13
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-STA-01

- **需求编号**：FR-STA-01
- **需求名称**：14 个单仓主状态 + 正交标记 + 组件聚合
- **优先级**：P0
- **描述**：本工具应为每个单仓计算 14 个互斥主状态（UNKNOWN/INVALID/MISSING/EMPTY/UNMERGED/IN_PROGRESS/DETACHED_TAG/DETACHED_COMMIT/DIVERGED/BEHIND/AHEAD/NO_UPSTREAM/DIRTY/SYNCED），叠加正交标记（dirty_tracked/untracked/stashed/shallow/lfs_missing/reparse_point/stale/busy/remote_gone/tag_moved/mixed_branch），并按 `max(组件覆盖态, 四仓主状态)` 聚合出组件状态；任一仓为 UNKNOWN 或无法命中规则时组件兜底为 UNKNOWN，禁止显示假绿。
- **验收标准（AC）**：**附录 C 状态矩阵**逐条通过（14 主状态 × 关键 flag × 15 聚合态）
- **关联 AC**：AC-STA-01、AC-STA-02、AC-STA-03、AC-STA-12、AC-STA-13、AC-STA-15
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-STA-02

- **需求编号**：FR-STA-02
- **需求名称**：数据不可用一律 UNKNOWN/STALE
- **优先级**：P0
- **描述**：本工具应在数据不可用时把仓库或组件状态一律记为 UNKNOWN/STALE；组件内任一仓为 UNKNOWN 时，组件不得显示 SYNCED。
- **验收标准（AC）**：注入扫描/远程失败后组件变 UNKNOWN，不显示绿
- **关联 AC**：AC-STA-01、AC-STA-04、AC-STA-09、AC-STA-14
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-STA-03

- **需求编号**：FR-STA-03
- **需求名称**：关系判定用 compare_ref 与 merge-base
- **优先级**：P0
- **描述**：本工具应使用 `compare_ref` 与 `merge-base --is-ancestor` 判定仓库与远程的关系，并按退出码 0/1/>1 区分 ancestor、非 ancestor 与错误；仅当 commit 已推送到远程时才可调用 Gitea compare API 兜底，未推送 commit 不得调用该 API。
- **验收标准（AC）**：单元测试覆盖 0/1/>1 与 compare_ref 三种来源；未推送 commit 不调用 API
- **关联 AC**：AC-STA-05、AC-STA-06
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-STA-04

- **需求编号**：FR-STA-04
- **需求名称**：状态记录带 `calc_at`/`data_source`/`error_code`/`is_stale`
- **优先级**：P0
- **描述**：本工具应在状态记录中保存 `calc_at`、`data_source`、`error_code`、`is_stale` 字段；过期数据不得参与“绿色（SYNCED）”判定。
- **验收标准（AC）**：查询接口返回新鲜度字段
- **关联 AC**：AC-STA-07、AC-STA-08、AC-STA-09
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-STA-05

- **需求编号**：FR-STA-05
- **需求名称**：操作完成 ≤2s 回写
- **优先级**：P1
- **描述**：本工具应在操作完成后 ≤2 秒回写状态；本地或外部 IDE 变更 ≤5 秒可见（FileSystemWatcher 加指纹兜底），外部 commit/checkout/fetch ≤10 秒可见，远程变更 ≤10 分钟可见。
- **验收标准（AC）**：计时用例覆盖四档；`IClock` 可注入
- **关联 AC**：AC-SCAN-09、AC-STA-10
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-STA-06

- **需求编号**：FR-STA-06
- **需求名称**：组件四仓 Tag 一致性无条件计算（不依赖“基线模式”）
- **优先级**：P1
- **描述**：本工具应无条件计算组件四仓的 Tag 一致性（不依赖“基线模式”）：四仓 tag 集合不一致、缺少 tag 或与快照不符时标记 TAG_DRIFT 并告警；v1 只记录告警，不提供 Tag 管理 UI。
- **验收标准（AC）**：四仓 Tag 不一致时展示 TAG_DRIFT；AC-STA-11
- **关联 AC**：AC-STA-11、AC-DETAIL-02、AC-OP-11、AC-OP-12
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-BOARD-01

- **需求编号**：FR-BOARD-01
- **需求名称**：默认视图 = 工作台左侧组件卡片列表（参考图为准），不再有“表格默认/卡片可选”
- **优先级**：P0
- **描述**：本工具应将工作台默认视图设为左侧组件卡片列表：每个组件渲染一张卡片，卡片字段包含组件 ID、聚合状态徽标、四仓 chip、当前分支、CI 占位与最近活动/新鲜度；默认按 `severity_rank` 升序（数值越小越严重）再按组件 ID 升序排序；旧看板表格视图降级到 v1.1，v1 不提供表格视图入口、不存在 `table` 视图入口。
- **验收标准（AC）**：卡片列表为唯一默认视图；默认排序断言；`table` 视图入口不存在（AC-UI-02/AC-UI-14）
- **关联 AC**：AC-BOARD-01、AC-BOARD-02、AC-BOARD-03、AC-BOARD-14、AC-BOARD-15、AC-BOARD-16
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-BOARD-02

- **需求编号**：FR-BOARD-02
- **需求名称**：工作台搜索与筛选
- **优先级**：P0
- **描述**：本工具应在工作台提供搜索与筛选：搜索组件 ID、仓库全名、当前分支、Tag、HEAD 短 SHA；中文输入法以 `compositionend` 触发搜索，组合输入期间不阻塞；筛选 facet 包括聚合状态、单仓主状态、RepoType、分支、Tag、CI 状态（占位）、有未提交改动、有未推送提交；排序支持严重度、组件 ID、最近活动；搜索结果只过滤左侧卡片列表与文件夹视图的组件节点，不改变中间文件树的当前选中。
- **验收标准（AC）**：搜索/筛选/排序夹具与 AC；IME 用例；1,000 组件滚动 ≥58fps
- **关联 AC**：AC-BOARD-03、AC-BOARD-04、AC-BOARD-05、AC-BOARD-06、AC-BOARD-07
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-BOARD-03

- **需求编号**：FR-BOARD-03
- **需求名称**：工作台七态齐备
- **优先级**：P0
- **描述**：本工具应支持工作台的七种状态：空、加载、错误可重试、离线降级、过期（STALE）、部分失败、权限或认证过期；卡片区、四仓树与信息面板各区域可独立显示自身状态，且每种状态都提供重试入口。
- **验收标准（AC）**：状态注入用例 + 截图（AC-UI-02/11）
- **关联 AC**：AC-BOARD-08
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-BOARD-04

- **需求编号**：FR-BOARD-04
- **需求名称**：组件级批量操作入口
- **优先级**：P1
- **描述**：本工具应提供组件级批量操作入口，对选中组件执行 Clone/Pull/Fetch/Checkout/Tag/SYNC/Delete/Restore/Create；当选中超过 10 个组件或超过 40 个仓时二次确认，并支持“全选筛选结果”；入口不得新增全宽工具栏或改变 7 区域布局，任务中心须可逐仓查看结果。
- **验收标准（AC）**：选择 11 个组件触发确认；任务中心可逐仓查看；布局 bbox 断言（AC-UI-02）
- **关联 AC**：AC-BOARD-09、AC-BOARD-10、AC-BOARD-11
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-BOARD-05

- **需求编号**：FR-BOARD-05
- **需求名称**：数据新鲜度展示（本地/远程分源）与手动刷新（标题区与状态栏各一处入口）
- **优先级**：P1
- **描述**：本工具应在工作台展示按本地/远程来源区分的数据新鲜度，并在标题区与状态栏各提供一处手动刷新入口；刷新过程中卡片区不得整表闪白，刷新后保留展开状态与滚动位置。
- **验收标准（AC）**：视觉回归 + 计时（AC-UI-01/06）
- **关联 AC**：AC-STA-08、AC-BOARD-05、AC-BOARD-12、AC-BOARD-14
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-BOARD-06

- **需求编号**：FR-BOARD-06
- **需求名称**：未绑定仓库入口（菜单栏/标题区入口 → 抽屉）
- **优先级**：P1
- **描述**：本工具应提供未绑定仓库入口：从菜单栏或标题区打开右侧抽屉，按未绑定原因分组列出仓库，支持重新解析绑定关系，并支持忽略（忽略时必须记录理由）。
- **验收标准（AC）**：未绑定清单抽屉存在；重新解析用例
- **关联 AC**：AC-BOARD-13
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-DETAIL-01

- **需求编号**：FR-DETAIL-01
- **需求名称**：组件详情
- **优先级**：P0
- **描述**：本工具应在双击工作台组件卡片时打开组件详情右侧抽屉（宽 480–720px，覆盖主区右侧且不替换工作台布局，按 ESC 关闭且焦点回到触发控件）；抽屉内容包含四仓状态卡（分支、HEAD、upstream、ahead/behind、dirty）、Tag 对照、未绑定告警与快捷操作。
- **验收标准（AC）**：组件状态下钻到单仓 1 次点击可达；抽屉打开/关闭后主区 bbox 不变（AC-UI-11/13）
- **关联 AC**：AC-DETAIL-01、AC-DETAIL-02
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-DETAIL-02

- **需求编号**：FR-DETAIL-02
- **需求名称**：单仓详情
- **优先级**：P0
- **描述**：本工具应从右侧信息面板的“详情”按钮或文件树仓库根节点打开单仓详情抽屉，Tab 顺序固定为概览、提交历史、差异、文件、CI（占位，第 5 位）；提交历史分页并虚拟滚动，差异只读，二进制差异显示不可预览提示。
- **验收标准（AC）**：10,000 提交仓库：历史首屏 ≤500ms、滚动 ≥58fps、单帧 ≤33ms、提交序列与 `git log` 一致；diff 正确
- **关联 AC**：AC-DETAIL-03、AC-DETAIL-04、AC-DETAIL-05
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-DETAIL-03

- **需求编号**：FR-DETAIL-03
- **需求名称**：单仓详情文件 Tab 复用文件树规则
- **优先级**：P1
- **描述**：本工具应让单仓详情的“文件”Tab 复用工作台文件树的同一只读树组件、同一白名单与同一路径安全规则，禁止两套实现：白名单为 `.m/.c/.h/.slx/.mdl/.json/.xml/.yaml/.yml/.txt/.md/.csv`，白名单外的扩展名与危险扩展名（`.exe/.bat/.cmd/.ps1/.lnk/.msi/.scr`）一律二次确认，且路径必须限定在 `workspace_root` 内。
- **验收标准（AC）**：路径穿越/危险扩展名/白名单用例；与工作台文件树规则一致性断言
- **关联 AC**：AC-DETAIL-06、AC-DETAIL-07、AC-SAFE-10
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-DETAIL-04

- **需求编号**：FR-DETAIL-04
- **需求名称**：四仓分支不一致时（卡片与信息面板）显示 Impl 分支为主 + “分支不一致”角标 + 四仓对照
- **优先级**：P1
- **描述**：本工具应在四仓分支不一致时，在组件卡片与信息面板以 Impl 分支作为主分支显示，并显示“分支不一致”角标与四仓分支对照。
- **验收标准（AC）**：角标必现；对照准确
- **关联 AC**：AC-DETAIL-08
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-DETAIL-05

- **需求编号**：FR-DETAIL-05
- **需求名称**：右键菜单
- **优先级**：P2
- **描述**：本工具应将 v1 的右键菜单限制为“在资源管理器中打开”与“复制路径”两个只读项的最小入口（或两者均不提供），不得包含写操作；复制 commit 与用其他程序打开留待 v1.1 实现。
- **验收标准（AC）**：交互用例
- **关联 AC**：AC-DETAIL-09、AC-DETAIL-10
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-OP-01

- **需求编号**：FR-OP-01
- **需求名称**：批量 Clone
- **优先级**：P0
- **描述**：本工具应支持批量 Clone：执行前预检磁盘空间、长路径、凭据与远程仓库存在性；目标目录已存在时执行 repair 修复而不覆盖；并发默认 2；401/403/404 不重试，网络错误按退避最多重试 2 次；逐仓写 journal，部分失败时组件显示 OP_PARTIAL_FAILED 与明细，重跑保持幂等。
- **验收标准（AC）**：4 仓 2 成功 2 失败时组件显示 OP_PARTIAL_FAILED + 明细；重跑幂等
- **关联 AC**：AC-SYNC-14、AC-OP-01、AC-OP-02、AC-OP-03、AC-OP-04、AC-OP-05
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-OP-02

- **需求编号**：FR-OP-02
- **需求名称**：批量 Pull/Fetch
- **优先级**：P0
- **描述**：本工具应支持对选中组件或单仓批量 Pull/Fetch：失败按仓逐个报告；Pull 默认使用 `--ff-only`，非快进时转手动处理，禁止隐式 merge。
- **验收标准（AC）**：ff-only 失败提示明确；无隐式 merge commit
- **关联 AC**：AC-OP-06、AC-OP-07、AC-OP-08
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-OP-03

- **需求编号**：FR-OP-03
- **需求名称**：批量 Checkout 分支
- **优先级**：P0
- **描述**：本工具应支持批量 Checkout 分支：执行前校验 ref 合法性；dirty 仓库按用户选择阻断或先 stash；部分仓失败时报告逐仓结果。
- **验收标准（AC）**：5 种前置态用例
- **关联 AC**：AC-OP-09、AC-OP-10
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-OP-04

- **需求编号**：FR-OP-04
- **需求名称**：批量切 Tag（暂定 D-029）
- **优先级**：P0
- **描述**：本工具应保留批量切 Tag 能力的实现，但 v1.0 不开放该入口，也不将其纳入验收：若未来开放，四个仓切换到同一 Tag，Tag 不存在于远程则跳过并报告；检出后为 detached HEAD；记录 `baseline_snapshot`（用 `^{commit}` 解引用）；Tag 被移动时以 `git fetch --force` 更新并告警；禁止使用 `--prune-tags`。
- **验收标准（AC）**：恢复时需按版本/仓集合重新定义，缺 Tag/移动 Tag/annotated Tag 用例
- **关联 AC**：AC-SYNC-05、AC-OP-11、AC-OP-12、AC-OP-13、AC-OP-14
- **状态**：暂缓（D-029，不参与 v1.0 验收）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-OP-05

- **需求编号**：FR-OP-05
- **需求名称**：受控 SYNC（“用远程覆盖本地”）
- **优先级**：P0
- **描述**：本工具应提供受控 SYNC（用远程覆盖本地）：fetch 目标为 `<remote>/<同名分支>`，先把当前分支备份到 `refs/backup/sync/<ts>/<branch>`；dirty 时先执行 `stash -u`；CAS 校验通过后执行 `reset --hard <remote-ref>`（禁止 `git pull`）；untracked 默认不删除，需显式勾选且执行前 dry-run；LFS 仓随后执行 `lfs pull`；拒绝覆盖本地独有分支。
- **验收标准（AC）**：未推送 commit 可从备份 ref 恢复；untracked 保留；部分失败报告
- **关联 AC**：AC-OP-15、AC-OP-16、AC-OP-17、AC-OP-18、AC-OP-19
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-OP-06

- **需求编号**：FR-OP-06
- **需求名称**：本地删除
- **优先级**：P0
- **描述**：本工具应支持本地删除：只删本地、不改动远程；预检到脏工作区、未推送提交、进行中操作或 stash 时默认阻断；删除采用软删除，把仓库移入本地根目录下的 `.trash` 目录（可恢复，默认统一保留 30 天）；删除前做 realpath 校验防误删；文件被占用导致删除失败时保留 DELETE_PENDING 状态。
- **验收标准（AC）**：恢复用例通过；远端零变更；`.trash` 保留期与清理策略可配置且默认 30 天
- **关联 AC**：AC-OP-20、AC-OP-21、AC-OP-22、AC-OP-23、AC-OP-30
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-OP-07

- **需求编号**：FR-OP-07
- **需求名称**：CREATE 分支
- **优先级**：P0
- **描述**：本工具应支持 CREATE 分支：输入分支名后用 `git check-ref-format` 校验，再展示 dry-run 预览表（重名、远程已删、超前/落后、非法、各仓状态、基线 ref），确认后逐仓创建；部分失败时支持“撤销已创建分支”，创建基线 ref 记入 job_item。
- **验收标准（AC）**：1 仓重名时预览明确；撤销成功；创建基线 ref 记录在 job_item
- **关联 AC**：AC-OP-24、AC-OP-25
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-OP-08

- **需求编号**：FR-OP-08
- **需求名称**：任务中心
- **优先级**：P0
- **描述**：本工具应提供任务中心：展示队列/进行中/历史，显示组件级与仓级进度，支持合作式取消、仅重试失败项、逐仓结果与错误码；任务需持久化，重启后状态不丢失。
- **验收标准（AC）**：杀进程重启后任务状态一致（不重复执行）；取消语义符合 7.6
- **关联 AC**：AC-OP-03、AC-OP-26、AC-OP-27、AC-OP-34
- **状态**：部分实现（边界/证据见 README 已知限制）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-OP-09

- **需求编号**：FR-OP-09
- **需求名称**：操作审计
- **优先级**：P1
- **描述**：本工具应记录操作审计：谁、何时、对哪个组件与哪些仓、执行什么操作、操作前后 HEAD、备份 ref 与结果；审计记录保留不少于 90 天且可查询导出。
- **验收标准（AC）**：`operation_log` 可查询导出；日志留存策略生效
- **关联 AC**：AC-OP-33、AC-OP-28、AC-OPS-04
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-OP-10

- **需求编号**：FR-OP-10
- **需求名称**：操作互斥
- **优先级**：P1
- **描述**：本工具应保证操作互斥：同一组件的写操作串行执行；检测到外部 git 占用（`index.lock`）时把仓库置为 BUSY 并拒绝操作，不得删除该锁文件。
- **验收标准（AC）**：并发用例无数据损坏
- **关联 AC**：AC-SCAN-06、AC-OP-29
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-OP-11

- **需求编号**：FR-OP-11
- **需求名称**：恢复操作
- **优先级**：P0
- **描述**：本工具应支持恢复操作：按删除记录从 `.trash` 恢复，或按组件/仓/备份时间从 `refs/backup/*` 恢复；备份 ref `refs/backup/sync/*` 保留不少于 30 天（可通过 `backup.retentionDays` 配置），超期由定期清理任务回收，备份需在任务中心可见并可发起恢复；恢复前展示将被覆盖的当前状态与冲突并要求确认，恢复过程写入审计。
- **验收标准（AC）**：AC-OP-30：删除后可恢复；SYNC 后未推送 commit 可从备份 ref 恢复；保留期内备份 ref 可列出并恢复，超期由清理任务回收
- **关联 AC**：AC-OP-30
- **状态**：部分实现（边界/证据见 README 已知限制）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-OP-12

- **需求编号**：FR-OP-12
- **需求名称**：组件纳入/移出管理
- **优先级**：P1
- **描述**：本工具应支持组件纳入与移出管理：按组件 ID 纳入时校验四仓存在性并展示将要 clone 的仓库；移出可选择本地删除或仅停止管理（数据库行保留并标记 archived）；纳入与移出均写入审计。
- **验收标准（AC）**：AC-OP-31/AC-OP-32：纳入/移出后工作台与 DB 状态正确
- **关联 AC**：AC-UI-16、AC-OP-31、AC-OP-32
- **状态**：部分实现（边界/证据见 README 已知限制）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-REPO-01

- **需求编号**：FR-REPO-01
- **需求名称**：Fetch / Pull（ff-only）/ Checkout 分支或 Tag
- **优先级**：P0
- **描述**：本工具应支持 Fetch、Pull（`--ff-only`）以及 Checkout 分支或 Tag；执行前重新扫描该仓状态；工作区 dirty 时阻断切换类操作并提示先处理本地改动。
- **验收标准（AC）**：与命令行结果一致
- **关联 AC**：AC-SAFE-16、AC-SAFE-17、AC-REPO-01、AC-REPO-02、AC-REPO-03
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-REPO-02

- **需求编号**：FR-REPO-02
- **需求名称**：Push
- **优先级**：P1
- **描述**：本工具应限制 Push 仅针对当前分支且要求该分支有 upstream：无 upstream 时提示“发布分支”，detached HEAD 时禁止 Push 并提示先建分支；服务端分支保护拒绝时原样展示服务端消息；EMPTY 空仓首次推送不受“需 upstream”拦截。
- **验收标准（AC）**：4 类场景用例 + EMPTY 首推用例
- **关联 AC**：AC-REPO-04、AC-REPO-05
- **状态**：部分实现（边界/证据见 README 已知限制）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-REPO-03

- **需求编号**：FR-REPO-03
- **需求名称**：Commit（最小化）
- **优先级**：P1
- **描述**：本工具应提供最小化的 Commit：提交 staged 变更并携带用户填写的 message；存在 unmerged 文件时禁止提交；不提供冲突解决与 rebase 界面。
- **验收标准（AC）**：提交后状态变 DIRTY/SYNCED 正确
- **关联 AC**：AC-REPO-06
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-REPO-04

- **需求编号**：FR-REPO-04
- **需求名称**：Reset（保留/混合/硬）——v1.1 再做，且必须先备份 ref
- **优先级**：P2
- **描述**：本工具应提供 Reset 的保留（soft）、混合（mixed）、硬（hard）三种模式，并排期到 v1.1 实现，且执行前必须先创建备份 ref 以便恢复。
- **验收标准（AC）**：备份可恢复
- **关联 AC**：AC-REPO-07
- **状态**：v1.1（非 v1.0）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-REPO-05

- **需求编号**：FR-REPO-05
- **需求名称**：状态修复类操作
- **优先级**：P0
- **描述**：本工具应提供状态修复类操作并统一走预检、job 与逐仓结果流程：Repair/Re-clone 修复 INVALID，set-upstream 三种子型修复 NO_UPSTREAM，基于 detached HEAD 建分支修复 DETACHED_TAG/DETACHED_COMMIT，以及为 EMPTY 空仓首次推送建立 upstream。
- **验收标准（AC）**：AC-REPO-08/09/10/11：四类修复各 1 个夹具，执行后状态转移正确
- **关联 AC**：AC-UI-10b、AC-OP-05、AC-REPO-08、AC-REPO-09、AC-REPO-10、AC-REPO-11
- **状态**：部分实现（边界/证据见 README 已知限制）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-SAFE-01

- **需求编号**：FR-SAFE-01
- **需求名称**：破坏性操作前展示丢失内容并强确认
- **优先级**：P0
- **描述**：本工具应在删除、SYNC、强制覆盖等破坏性操作前展示“将丢失什么”（待丢弃 commit 列表、untracked 清单、stash、影响仓数），默认阻断并要求显式确认；强制覆盖还需用户输入组件 ID 才能执行。
- **验收标准（AC）**：dry-run 表字段断言；未确认时实际写入操作数=0
- **关联 AC**：AC-OP-16、AC-OP-35、AC-SAFE-01、AC-SAFE-02、AC-SAFE-03
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-SAFE-02

- **需求编号**：FR-SAFE-02
- **需求名称**：删除仅本地
- **优先级**：P0
- **描述**：本工具应保证删除操作仅作用于本地、远程仓库不受影响；UI 固定展示“远程仓库不受影响”文案，且删除过程不得向 Gitea 发起任何写请求。
- **验收标准（AC）**：文案快照；Gitea API 无写请求（Mock 断言）
- **关联 AC**：AC-OP-20、AC-OP-23、AC-SAFE-04
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-SAFE-03

- **需求编号**：FR-SAFE-03
- **需求名称**：Token/日志脱敏
- **优先级**：P0
- **描述**：本工具应对 Token 与日志脱敏、对配置加密，并在导出诊断包前自动脱敏；崩溃转储默认关闭，若开启则导出前必须脱敏，保证日志、诊断包与转储中均无明文 Token。
- **验收标准（AC）**：日志/诊断包/转储扫描无 token；脱敏规则单测
- **关联 AC**：AC-SAFE-15、AC-SAFE-16、AC-SAFE-17、AC-CFG-06、AC-SAFE-05、AC-SAFE-06、AC-SAFE-07、AC-OPS-06
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-SAFE-04

- **需求编号**：FR-SAFE-04
- **需求名称**：写操作全部走写前 journal + 崩溃恢复 reconcile（pre/expect/post 推断，禁止猜测成功）
- **优先级**：P0
- **描述**：本工具应保证所有写操作在执行前写入 journal，并在崩溃重启后按 pre/expect/post 记录推断、reconcile 实际结果，不得猜测成功。
- **验收标准（AC）**：杀进程重启后 reconcile 用例；不猜测成功
- **关联 AC**：AC-SAFE-08、AC-SAFE-09
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-SAFE-05

- **需求编号**：FR-SAFE-05
- **需求名称**：危险扩展名与路径穿越防护
- **优先级**：P1
- **描述**：本工具应防护危险扩展名与路径穿越：危险扩展名操作需二次确认，含 `..`、绝对路径、UNC/网络盘、Windows 保留名的越界路径直接拒绝；reparse point 目录禁止写入。
- **验收标准（AC）**：路径穿越/危险扩展名/reparse 用例
- **关联 AC**：AC-DETAIL-06、AC-DETAIL-07、AC-SAFE-10、AC-SAFE-11
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-SAFE-06

- **需求编号**：FR-SAFE-06
- **需求名称**：启动时扫描既有仓库 `.git/config`/`insteadOf`/URL 中的凭据并告警（不自动改用户配置）
- **优先级**：P0
- **描述**：本工具应在启动时扫描既有仓库的 `.git/config`、`insteadOf` 配置与远程 URL 中是否内嵌凭据并告警（不自动修改用户配置），UI 中脱敏展示；自身访问 Gitea 时 Token 仅通过 `Authorization: token <PAT>` 请求头发送，绝不写入 URL 或命令行。
- **验收标准（AC）**：夹具仓库内嵌 token → 告警且 UI 脱敏展示；抓包/命令行无 token
- **关联 AC**：AC-SAFE-14、AC-CFG-06、AC-SAFE-12、AC-SAFE-13
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-OPS-01

- **需求编号**：FR-OPS-01
- **需求名称**：应用内通知中心（从菜单栏「帮助/通知」或标题区铃铛进入的右侧抽屉，不占工作台区域）
- **优先级**：P0
- **描述**：本工具应提供应用内通知中心，入口为菜单栏「帮助/通知」或标题区铃铛，打开右侧抽屉且不占用工作台区域；通知覆盖状态恶化、批量完成/部分失败、Token 过期与远程同步失败（CI 事件类目 v1 为空占位）；通知持久化到 `notification` 表，“清除”操作删除通知行。
- **验收标准（AC）**：通知触发/已读/清除用例；`notification` 表有记录；清除后列表为空
- **关联 AC**：AC-OPS-01、AC-OPS-02
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-OPS-02

- **需求编号**：FR-OPS-02
- **需求名称**：日志与诊断（从菜单栏「工具 → 日志与诊断」进入的二级页面/抽屉）
- **优先级**：P0
- **描述**：本工具应提供日志与诊断页面，入口为菜单栏「工具 → 日志与诊断」的二级页面或抽屉：结构化滚动日志单文件 10MB、保留 30 天（审计日志保留 ≥90 天），支持按级别过滤（显示 ≥所选级别）、搜索、暂停与一键生成脱敏诊断包；诊断包包含日志、脱敏配置、DB 快照、schema 版本、最近 job 与 sync_run、版本信息与转储清单。
- **验收标准（AC）**：滚动/留存/脱敏/≥级别断言；诊断包含 §12.5 全部必需项
- **关联 AC**：AC-SAFE-06、AC-OPS-03、AC-OPS-04、AC-OPS-05、AC-OPS-06
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-OPS-03

- **需求编号**：FR-OPS-03
- **需求名称**：设置（从菜单栏「设置」或标题区齿轮进入的抽屉/二级页面）
- **优先级**：P0
- **描述**：本工具应提供设置页面，入口为菜单栏「设置」或标题区齿轮的抽屉/二级页面，分区包括通用、工作区、认证、操作与安全、CI 集成（禁用占位）与实验特性（v1 无可配置键，仅说明/开关占位）；每个设置项须给出键名、默认值、校验规则与生效范围，非法值被拒绝并提示，且设置不得改变工作台 7 区域结构。
- **验收标准（AC）**：设置键表核对；非法值被拒绝并提示；布局 bbox 断言
- **关联 AC**：AC-OPS-07、AC-OPS-08
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-OPS-04

- **需求编号**：FR-OPS-04
- **需求名称**：单实例锁
- **优先级**：P0
- **描述**：本工具应实现单实例锁：第二次启动时提示已运行且不破坏数据；崩溃后遗留的 stale lock 在满足任一条件时自愈——PID 不存在、PID 不属于本应用、或锁文件 mtime 超过 10 分钟且无存活进程。
- **验收标准（AC）**：双开用例 + 强杀后重启可自愈
- **关联 AC**：AC-OPS-09、AC-OPS-10
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-OPS-05

- **需求编号**：FR-OPS-05
- **需求名称**：状态栏双段信息
- **优先级**：P1
- **描述**：本工具应将状态栏分为两段：左段固定显示“最近一次操作：<动作> <组件>/<RepoType> — <成功|失败(错误码)|进行中> · HH:mm”，未操作时显示“无”，失败可展开最近 5 条；右段 6 段分别为数据新鲜度、进程健康、操作队列、CI 队列（v1 恒 0）、通知与网络状态；进程健康以 Host 与 GitWorker 两盏灯表示，RemoteSync/LocalScan/StatusCalc/JOB_RUNNER 等 BackgroundService 以子状态或悬浮明细呈现。
- **验收标准（AC）**：布局快照；灯光数与映射断言；“最近一次操作”文案模板断言（AC-UI-01/10）
- **关联 AC**：AC-OPS-11
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-OPS-06

- **需求编号**：FR-OPS-06
- **需求名称**：数据库 schema 版本 + 迁移 + 迁移前自动备份 + 损坏检测与重建（重建只需重扫）
- **优先级**：P1
- **描述**：本工具应维护数据库 schema 版本并支持迁移，迁移前自动备份；提供损坏检测与重建能力（重建只需重新扫描即可恢复），并以 `PRAGMA user_version` 记录 schema 版本。
- **验收标准（AC）**：迁移/回滚/损坏重建用例；`PRAGMA user_version` 断言
- **关联 AC**：AC-OPS-12、AC-OPS-13
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-OPS-07

- **需求编号**：FR-OPS-07
- **需求名称**：自动更新（v1.1）
- **优先级**：P2
- **描述**：本工具应提供自动更新能力（排期 v1.1）：发现新版本时提示并完成更新，且更新失败可回滚。
- **验收标准（AC）**：更新/回滚用例（v1.1）
- **关联 AC**：AC-OPS-14
- **状态**：v1.1（非 v1.0）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-OPS-08

- **需求编号**：FR-OPS-08
- **需求名称**：首启编排与就绪门控
- **优先级**：P0
- **描述**：本工具应将首启编排为三阶段：`LOCAL_DISCOVERY`（本地发现）→ `REMOTE_RECONCILE`（远程对账）→ `STATUS_CALC`（状态计算），每阶段进度事件包含 `{stage,total,current,cancellable}`；远程阶段可跳过并进入降级只读，远程就绪后自动重判本地仓；远程未就绪时不得判 INVALID/MISSING，正确状态为 `UNKNOWN` 且 `incomplete=1`。
- **验收标准（AC）**：AC-OPS-15/16：分阶段 progress 事件断言（`LOCAL_DISCOVERY→REMOTE_RECONCILE→STATUS_CALC` + `{stage,total,current,cancellable}`）；跳过远程后进入只读，远程恢复后组件由 `UNKNOWN`（`incomplete=1`）自动重判为真实聚合态
- **关联 AC**：AC-OPS-15、AC-OPS-16
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-OPS-09

- **需求编号**：FR-OPS-09
- **需求名称**：错误码表（前缀 CFG/SYNC/SCAN/STATE/OP/FS/NET/GIT/SEC）与用户可读文案
- **优先级**：P1
- **描述**：本工具应建立稳定错误码表，错误码前缀为 CFG/SYNC/SCAN/STATE/OP/FS/NET/GIT/SEC，每个错误码配用户可读中文文案；错误码一经定义不随文案变化，UI 同时展示错误码与文案。
- **验收标准（AC）**：错误码表核对；UI 展示错误码 + 文案
- **关联 AC**：AC-OP-34、AC-OPS-17
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-CI-01

- **需求编号**：FR-CI-01
- **需求名称**：10 个 CI 锚点占位
- **优先级**：P0
- **描述**：本工具应在 v1 于 10 处 CI 锚点预留设计落点（编号 CI-1…CI-10 不变）：菜单栏/标题区 CI 入口、组件卡片 CI 槽位、右侧信息面板 CI 汇总条、单仓详情 CI Tab、状态栏 CI 队列段、通知抽屉 CI 事件类目、标题区筛选面板 CI facet、设置 CI 分区、数据契约、聚合分离；10 处必须在 v1 全部存在，不得因工作台改版删减。
- **验收标准（AC）**：锚点存在性快照测试 10/10（AC-UI-12）；位置变更需同步本表与 AC
- **关联 AC**：AC-DETAIL-03、AC-OPS-02、AC-CI-01、AC-CI-02
- **状态**：占位/预留（v1.0 仅设计落点）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-CI-02

- **需求编号**：FR-CI-02
- **需求名称**：CI 数据契约与 CI_UPDATED 事件
- **优先级**：P0
- **描述**：本工具应定义 CI 数据契约 `CiLatest`、`ComponentCiAggregate`、`ICiProvider` 与 `CI_UPDATED` 事件；v1 由 Mock 提供 CI 数据，配置键 `ci.enabled` 默认 false，启用后各 CI 锚点显示 Mock 数据且工作台布局与区域 bbox 不变。
- **验收标准（AC）**：打开 feature flag 后锚点显示 Mock 数据且工作台布局/区域 bbox 不变
- **关联 AC**：AC-CI-05、AC-CI-06、AC-CI-07、AC-CI-10、AC-CI-11
- **状态**：占位/预留（v1.0 仅设计落点）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-CI-03

- **需求编号**：FR-CI-03
- **需求名称**：CI 独立通道
- **优先级**：P0
- **描述**：本工具应保证 CI 走独立通道：CI 状态永不参与组件聚合与卡片底色（工作台组件卡片底色恒中性）；CI 为空或未知时使用中性样式，禁止用红色表达“没数据”。
- **验收标准（AC）**：注入 CI FAIL 后卡片底色不变（视觉回归）；§7.4 聚合函数输入不含 CI（单测）
- **关联 AC**：AC-CI-03、AC-CI-04
- **状态**：占位/预留（v1.0 仅设计落点）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-CI-04

- **需求编号**：FR-CI-04
- **需求名称**：无 CI 时显示“CI 未接入”统一空态组件，禁止裸 TODO/白块
- **优先级**：P1
- **描述**：本工具应在无 CI 数据时显示统一的“CI 未接入”空态组件，全站禁止出现裸 TODO 或白块。
- **验收标准（AC）**：全站无裸 TODO
- **关联 AC**：AC-CI-08
- **状态**：占位/预留（v1.0 仅设计落点）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-CI-05

- **需求编号**：FR-CI-05
- **需求名称**：`ci_status` 表 + `ci.*` 配置键 + 事件类型在 v1 建好（空数据）
- **优先级**：P0
- **描述**：本工具应在 v1 建好 `ci_status` 表、`ci.*` 配置键与 CI 事件类型（初始为空数据），并纳入 DDL 与迁移。
- **验收标准（AC）**：DDL/迁移中包含
- **关联 AC**：AC-CI-09
- **状态**：占位/预留（v1.0 仅设计落点）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-WS-01

- **需求编号**：FR-WS-01
- **需求名称**：主界面 7 区域固定且顺序不可交换（上→下、左→右）
- **优先级**：P0
- **描述**：本工具应将主界面固定为 7 个区域且顺序不可交换（上→下、左→右）：A 菜单栏（最上）→ B 标题区 → C 左侧组件卡片列表 / D 中间 2×2 四仓文件树 / E 右侧信息面板（C/D/E 同一水平行）→ F 底部单仓按钮条 → G 底部状态栏；任何区域不得被删除或并入其他区域，任务/通知/日志/设置/CI 等二级页面不得替换主界面而丢失区域布局。
- **验收标准（AC）**：CDP 截图 + bbox 断言：7 区域存在、相对顺序、D 为 2×2、C 在左/E 在右；AC-UI-01
- **关联 AC**：见 `docs/m2b-ac-op-gap.md`
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-WS-02

- **需求编号**：FR-WS-02
- **需求名称**：尺寸与缩放
- **优先级**：P0
- **描述**：本工具应按尺寸约束布局主界面：基线视口 1440×900（WebView 内容区，不含系统标题栏），最小 1280×800；宽度分配 C≈20%、D≈62%、E≈18%（C 240–360px、E 220–320px，可拖拽并持久化，D 自适应且 ≥560px）；高度分配 A ≤40px、B ≤64px、F ≤64px、G ≤32px、C/D/E 占剩余（≥600px）；100%–150% 缩放无页面级横向滚动，175%–200% 允许 D 区内部滚动但页面级 `scrollWidth` 等于 `clientWidth`；面板内各自独立滚动，页面整体不横滚。
- **验收标准（AC）**：1440/1280/150%/200% 截图与 `scrollWidth === clientWidth` 断言；拖拽宽度写入 `ui.panel.leftWidth/rightWidth`；AC-UI-01
- **关联 AC**：见 `docs/m2b-ac-op-gap.md`
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-WS-03

- **需求编号**：FR-WS-03
- **需求名称**：视图切换
- **优先级**：P0
- **描述**：本工具应支持工作台与文件夹视图切换：快捷键 `Ctrl+1`/`Ctrl+2`，或点击标题区视图切换按钮、菜单栏「查看」；切换耗时 ≤200ms，切换后保留 `selection`（组件与聚焦仓）与展开状态；两视图共用同一状态快照，禁止各自计算。
- **验收标准（AC）**：切换计时与选择/展开状态保持断言；AC-UI-07
- **关联 AC**：见 `docs/m2b-ac-op-gap.md`
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-WS-04

- **需求编号**：FR-WS-04
- **需求名称**：二级入口全部经菜单栏或标题区进入，不设旧 App Shell 左侧图标导航
- **优先级**：P0
- **描述**：本工具应仅通过菜单栏或标题区提供全部二级入口，不设旧 App Shell 左侧图标导航：任务中心、通知中心、日志与诊断、设置、CI 流水线、未绑定仓库均须可从菜单栏或标题区到达；抽屉打开不改变主区 bbox，按 ESC 关闭且焦点回到触发控件。
- **验收标准（AC）**：7 个入口点击可达；抽屉前后 C/D/E bbox 不变；AC-UI-13
- **关联 AC**：见 `docs/m2b-ac-op-gap.md`
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-WS-05

- **需求编号**：FR-WS-05
- **需求名称**：键盘与可访问性
- **优先级**：P1
- **描述**：本工具应支持键盘操作与可访问性：Tab 顺序为 A→C→D→E→F→G（跳过纯展示的状态栏）；树内用 ↑↓←→ 展开/移动、Enter 打开、空格选中；`F5` 刷新、`Ctrl+F` 搜索、`Ctrl+1/2` 切换视图、`Esc` 关闭抽屉；焦点环 2px 可见；状态变化通过 `aria-live` 播报，reduced-motion 下动效 ≤1ms。
- **验收标准（AC）**：键盘用例；焦点环与 aria 断言；reduced-motion 下动效 ≤1ms
- **关联 AC**：见 `docs/m2b-ac-op-gap.md`
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-TREE-01

- **需求编号**：FR-TREE-01
- **需求名称**：读取目录
- **优先级**：P0
- **描述**：本工具应按需懒加载文件树目录：首次展开 ≤300ms；单目录超过 500 个子项时分页或虚拟化，单次返回不超过 500 条；默认展开仓库根 1 层；排序为目录优先 → 名称升序（大小写不敏感）→ 扩展名；默认隐藏 `.git` 与 `.trash`（`tree.hideDotGit=true` 可关闭）；v1 不提供大小/时间列；读取失败（权限/超长路径/不存在）返回统一错误码并可就地重试。
- **验收标准（AC）**：目录 API 单测；排序夹具；2,000 子项目录计时；AC-UI-03
- **关联 AC**：见 `docs/m2b-ac-op-gap.md`
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-TREE-02

- **需求编号**：FR-TREE-02
- **需求名称**：四仓 2×2 固定
- **优先级**：P0
- **描述**：本工具应将四仓文件树固定为 2×2 布局：左上 Design、右上 Impl、左下 MIL、右下 SIL（顺序不可变）；每格独立展开与独立滚动；展开状态按组件 LRU 缓存（≥100 组件）；切换组件后每格重置为“根展开”；缺失、未克隆或未绑定的仓显示明确空态与原因（“未克隆/绑定异常/读取失败”），禁止伪造树或显示他仓内容。
- **验收标准（AC）**：2×2 顺序截图断言；组件切换后展开状态；缺仓空态；AC-UI-03
- **关联 AC**：见 `docs/m2b-ac-op-gap.md`
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-TREE-03

- **需求编号**：FR-TREE-03
- **需求名称**：双击打开
- **优先级**：P0
- **描述**：本工具应支持文件树的单击与双击：单击选中并把该仓设为“聚焦仓”，右侧信息面板与底部按钮条绑定同一聚焦仓；双击文件用系统默认程序打开、双击文件夹用资源管理器打开；仅白名单扩展名（`.m/.c/.h/.slx/.mdl/.json/.xml/.yaml/.yml/.txt/.md/.csv`）直接打开，白名单外与危险扩展名一律二次确认；打开失败（无关联程序/被占用/超长路径/已删除）给出可读错误码与建议动作；双击不得修改任何文件。
- **验收标准（AC）**：双击夹具；危险扩展名确认；错误码；AC-UI-03/04
- **关联 AC**：见 `docs/m2b-ac-op-gap.md`
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-TREE-04

- **需求编号**：FR-TREE-04
- **需求名称**：只读与路径安全
- **优先级**：P0
- **描述**：本工具应保证文件树 API 严格只读：不提供新建、重命名、删除、移动、拖放、上传或在树内编辑的入口；所有路径请求先做 canonical 解析，必须位于 `workspace_root` 之下且属于已绑定仓目录，拒绝 `..`、绝对路径、UNC/网络盘、reparse point 越界、Windows 保留名与尾随空格/点；复核目录 realpath，拒绝时返回统一错误码且不泄露越界路径内容。
- **验收标准（AC）**：路径穿越/绝对路径/UNC/reparse/保留名用例 100% 拒绝；树无写入口；AC-UI-04
- **关联 AC**：见 `docs/m2b-ac-op-gap.md`
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-TREE-05

- **需求编号**：FR-TREE-05
- **需求名称**：节点状态标记
- **优先级**：P1
- **描述**：本工具应在每个仓库根节点显示该仓主状态徽标（图标+中文文案+通道 A 状态色，选中行高亮）；已跟踪且未提交改动的文件行显示 `●`、untracked 显示 `○`，数据取自既有 `git status` 快照，不得为文件树额外启动 git 进程；该仓 STALE 时徽标灰化并显示“数据 X 前”。
- **验收标准（AC）**：状态矩阵夹具；进程计数断言（展开文件树不新增 git 进程）；AC-UI-05
- **关联 AC**：见 `docs/m2b-ac-op-gap.md`
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-TREE-06

- **需求编号**：FR-TREE-06
- **需求名称**：文件树右键菜单仅只读项
- **优先级**：P1
- **描述**：本工具应将文件树右键菜单限制为只读项：在资源管理器中打开、复制路径、刷新该仓目录；执行“在资源管理器中打开”前需二次确认路径，再调用系统程序；菜单不得出现任何写操作项。
- **验收标准（AC）**：右键菜单项断言（无写项）；AC-UI-04
- **关联 AC**：见 `docs/m2b-ac-op-gap.md`
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-FOLDER-01

- **需求编号**：FR-FOLDER-01
- **需求名称**：文件夹视图结构
- **优先级**：P0
- **描述**：本工具应将文件夹视图结构定义为：`workspace_root` → 组件目录 → 四仓目录（Design/Impl/MIL/SIL）→ 文件目录树；最多 1 层组件目录、1 层仓库目录，文件层可无限展开；未克隆组件显示“未克隆”淡显节点且不可展开；未纳管目录归入“未纳管”分组（只读展示，不参与聚合）；组件与仓库节点文案与工作台一致。
- **验收标准（AC）**：树结构快照夹具；AC-UI-06
- **关联 AC**：见 `docs/m2b-ac-op-gap.md`
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-FOLDER-02

- **需求编号**：FR-FOLDER-02
- **需求名称**：文件夹视图交互
- **优先级**：P0
- **描述**：本工具应支持文件夹视图的懒加载与展开/折叠，并使用虚拟滚动（400 组件/1,600 仓规模下 ≥58fps）；双击文件用系统默认程序打开、双击文件夹用资源管理器打开（仅白名单扩展名直开，白名单外二次确认）；单击组件或仓库节点同步 `selection`，切回工作台时可定位到同一选中。
- **验收标准（AC）**：性能计时；选中同步断言；AC-UI-06
- **关联 AC**：见 `docs/m2b-ac-op-gap.md`
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-FOLDER-03

- **需求编号**：FR-FOLDER-03
- **需求名称**：文件夹视图状态标记
- **优先级**：P0
- **描述**：本工具应在文件夹视图显示状态标记：组件节点显示聚合状态徽标与四仓 chip，仓库节点显示主状态徽标；数据与工作台来自同一状态快照，同一夹具两视图状态逐字一致；STALE 时显示数据时间与斜纹。
- **验收标准（AC）**：同一夹具两视图状态一致断言；AC-UI-06
- **关联 AC**：见 `docs/m2b-ac-op-gap.md`
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-FOLDER-04

- **需求编号**：FR-FOLDER-04
- **需求名称**：文件夹视图过滤
- **优先级**：P1
- **描述**：本工具应支持文件夹视图过滤：按聚合状态、RepoType、是否有本地改动、是否未克隆过滤；搜索组件 ID、仓名与文件名（防抖 120ms，p95 ≤300ms）；文件夹视图的筛选状态独立保存，不污染工作台筛选。
- **验收标准（AC）**：过滤夹具；筛选状态隔离断言；AC-UI-06
- **关联 AC**：见 `docs/m2b-ac-op-gap.md`
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-FOLDER-05

- **需求编号**：FR-FOLDER-05
- **需求名称**：文件夹视图与工作台切换
- **优先级**：P0
- **描述**：本工具应支持文件夹视图与工作台切换：`Ctrl+1` 切到工作台 / `Ctrl+2` 切到文件夹视图，切换 ≤200ms 并保留选中与展开路径；文件夹视图不等于旧看板表格视图，v1 不提供旧表格视图入口。
- **验收标准（AC）**：切换断言；AC-UI-07/AC-UI-14
- **关联 AC**：见 `docs/m2b-ac-op-gap.md`
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-ACT-01

- **需求编号**：FR-ACT-01
- **需求名称**：按钮固定 5 个且文案/顺序与参考图一致
- **优先级**：P0
- **描述**：本工具应将底部按钮条固定为 5 个按钮，文案与顺序为 `PULL` → `FETCH` → `CHKOUT` → `COMMIT` → `PUSH`（英文大写，不本地化）；未选中聚焦仓时 5 个按钮全部禁用并提示“请先在文件树中选择仓库”；按钮条不因窗口宽度换行或隐藏。
- **验收标准（AC）**：按钮存在/顺序/禁用态快照；未选中点击=0 次写操作；AC-UI-08
- **关联 AC**：见 `docs/m2b-ac-op-gap.md`
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-ACT-02

- **需求编号**：FR-ACT-02
- **需求名称**：底部按钮可用性严格取矩阵
- **优先级**：P0
- **描述**：本工具应按单仓主状态逐格确定底部 5 个按钮的可用性（14 个主状态 × 5 个按钮逐格可验收），映射为：`PULL`=Pull(ff)、`FETCH`=Fetch、`CHKOUT`=Checkout 分支（切 Tag 经 CHKOUT 二级选择）、`COMMIT`=Commit、`PUSH`=Push；禁用时 tooltip 必须给出“原因 + 替代动作 + 错误码”；M1 阶段 5 个按钮全部禁用（含 `FETCH`），只读能力仅通过刷新/重扫/文件树查看体现。
- **验收标准（AC）**：14 主状态 × 5 按钮矩阵逐格断言；M1 全禁用截图与理由断言；AC-UI-08/09
- **关联 AC**：见 `docs/m2b-ac-op-gap.md`
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-ACT-03

- **需求编号**：FR-ACT-03
- **需求名称**：前置条件
- **优先级**：P0
- **描述**：本工具应在每次点击底部按钮前先 read-through 重扫（不接受 DB 缓存）并做 CAS 校验；`FETCH` 需网络可达（离线时禁用并说明）；`PULL` 仅 `--ff-only`（非快进给出手动处理指引）；`CHKOUT` 先弹分支/Tag 选择器（数据来自 `remote_ref`，含搜索）；`COMMIT` 仅提交 staged 变更、message 必填 1–200 字符、无 staged 或 unmerged 时禁用；`PUSH` 需当前分支有 upstream 且非 detached（EMPTY 首推另行处理）。
- **验收标准（AC）**：各类前置夹具；无 staged 时 COMMIT 禁用；PULL 非快进明细；AC-UI-10
- **关联 AC**：AC-UI-10a、AC-UI-10b、AC-SAFE-14、AC-SAFE-15、AC-SAFE-17
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-ACT-04

- **需求编号**：FR-ACT-04
- **需求名称**：确认与安全
- **优先级**：P0
- **描述**：本工具应对 `FETCH`/`PULL`/`CHKOUT` 仅做预检并入队，不弹确认；`COMMIT`/`PUSH` 为写操作，须在按钮条上方显示高度 ≤48px 的确认条（“将提交 N 个文件 / 将推送 N 个提交（分支 → upstream）”），确认后才执行，且确认条不得遮挡或移动 5 个按钮；破坏性覆盖（SYNC 强覆盖/Force Push/Reset）不进入按钮条以防误触；`Alt+5` 等快捷键与按钮走同一路径。
- **验收标准（AC）**：确认条快照；未确认时写操作数=0；按钮 bbox 不变；AC-UI-10a/10b
- **关联 AC**：AC-UI-10a、AC-UI-10b
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-ACT-05

- **需求编号**：FR-ACT-05
- **需求名称**：进行中/失败
- **优先级**：P0
- **描述**：本工具应在执行按钮操作时显示进行中状态：执行中该按钮显示 spinner，同仓其余按钮禁用（同仓操作串行）；状态栏左段实时显示进行中；失败在状态栏显示错误码与中文文案并可重试；`BUSY`（外部 git）与 `CONCURRENT_CHANGE`（CAS）按错误码处理，禁止删锁；取消为合作式：`PULL` 等长任务支持取消，`FETCH` 为短网络任务不提供取消（超时中断），`COMMIT` 不可取消，`PUSH` 在服务端接收前可中断、取消点后不可取消，取消后以实际扫描结果为准。
- **验收标准（AC）**：M2a：错误码/BUSY/CAS/进行中/失败可读；M2b：job 状态与取消语义断言；AC-UI-10（M2a）+ AC-UI-10c（M2b）
- **关联 AC**：AC-UI-10c
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-ACT-06

- **需求编号**：FR-ACT-06
- **需求名称**：完成后回写
- **优先级**：P1
- **描述**：本工具应在操作完成后 ≤2 秒刷新对应仓状态、四仓文件树标记、组件卡片与信息面板，并把状态栏左段更新为“最近一次操作”文案；失败项保留在任务中心并支持仅重试失败项；一次操作写入一条审计记录。
- **验收标准（AC）**：M2a：≤2s 回写、状态栏模板、审计一条（AC-OP-33）；M2b：任务中心/仅重试失败项；AC-UI-10 + AC-OP-33 + AC-UI-10c
- **关联 AC**：AC-UI-10a、AC-UI-10c、AC-OP-33
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-ACT-07

- **需求编号**：FR-ACT-07
- **需求名称**：版本门禁（禁止可点但无效果）
- **优先级**：P0
- **描述**：本工具应按版本阶段门禁实现底部按钮，禁止出现可点击但无效果：M0 阶段按钮存在但禁用，点击显示“M0 占位：单仓操作将在 M2 接入”说明；M1 阶段只读，5 个按钮均禁用并给出“M1 只读”理由；M2a 阶段接入按钮前置条件、确认与安全、进行中/失败、完成后回写（取消/job/任务中心除外）；M2b 阶段全量接入；任何阶段点击按钮都不得静默失败（无 toast、无状态变化）。
- **验收标准（AC）**：三阶段截图 + 点击断言；AC-UI-09
- **关联 AC**：AC-UI-15
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-CARD-01

- **需求编号**：FR-CARD-01
- **需求名称**：组件卡片内容（左列 C 区，宽随栏宽 220–360px、高 ≥72px，底色中性）
- **优先级**：P0
- **描述**：本工具应在组件卡片（左侧 C 区，宽随栏宽 220–360px、高 ≥72px、底色中性）上显示：组件 ID（超长省略并提供 tooltip 全名）、聚合状态徽标（图标+中文文案+通道 A 颜色）、四仓 chip `D/I/M/S`（各自主状态，缺失用斜纹）、当前分支（`MIXED_BRANCH` 时显示角标与四仓对照 tooltip）、CI 占位槽位（定高 16–20px，显示“CI 未接入”）、最近活动/新鲜度；卡片不得使用整卡饱和色。
- **验收标准（AC）**：卡片字段快照；底色中性断言；AC-UI-02/12
- **关联 AC**：见 `docs/m2b-ac-op-gap.md`
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-CARD-02

- **需求编号**：FR-CARD-02
- **需求名称**：选中与联动
- **优先级**：P0
- **描述**：本工具应支持组件卡片的选中与联动：单击卡片即选中组件并加载四仓文件树（默认聚焦第一个存在的仓），右侧信息面板切换到该组件；`Ctrl/Shift` 多选进入批量选择态；双击卡片打开组件详情抽屉且不替换工作台；选中态显示 2px 焦点环与 `aria-selected`；唯一选中模型为 `{componentId, focusedRepoType}`，是文件树、信息面板与按钮条的唯一数据源。
- **验收标准（AC）**：联动断言；单一选中模型（改选中后三处同步）；AC-UI-02/11
- **关联 AC**：见 `docs/m2b-ac-op-gap.md`
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-CARD-03

- **需求编号**：FR-CARD-03
- **需求名称**：列表规模与检索
- **优先级**：P0
- **描述**：本工具应保证组件卡片列表满足规模与检索要求：1,000 组件虚拟滚动 ≥58fps 且 DOM 卡片节点 ≤120；默认排序 `severity_rank` 升序 → 组件 ID；按工作台的搜索、筛选与排序结果渲染；可承受 4,000 组件压测档；卡片区独立滚动，纵向滚动条不挤压 D/E 区。
- **验收标准（AC）**：性能计时；滚动条与 bbox 断言；AC-UI-02
- **关联 AC**：见 `docs/m2b-ac-op-gap.md`
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-CARD-04

- **需求编号**：FR-CARD-04
- **需求名称**：批量入口（不破坏参考图布局）
- **优先级**：P0
- **描述**：本工具应提供三处批量入口且不破坏参考图布局：菜单栏「操作」菜单（对当前选中集合生效，未选中时禁用）、组件卡片右键菜单、选中 ≥1 个时在卡片区顶部叠加显示的批量条（高 ≤36px，浮层覆盖在卡片区顶部，不新增全宽工具栏、不改变 C/D/E 区域 bbox）；批量动作支持 Clone/Pull/Fetch/Checkout/Tag/SYNC/Delete/Restore/Create，选中超过 10 个组件或超过 40 个仓时二次确认。
- **验收标准（AC）**：三个入口可达；批量条出现时 C/D/E bbox 不变；AC-UI-02
- **关联 AC**：AC-UI-15
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-CARD-05

- **需求编号**：FR-CARD-05
- **需求名称**：卡片区七态 + 刷新
- **优先级**：P1
- **描述**：本工具应支持卡片区七种状态与刷新：空（“未发现组件，请检查配置/首扫”）、加载骨架、错误可重试、离线降级、过期（STALE 斜纹 + 数据时间）、部分失败（OP_PARTIAL_FAILED 徽标 + 重试）、权限/认证过期（提供重登入口）；刷新不得整区闪白，并保留滚动位置。
- **验收标准（AC）**：七态注入截图；刷新视觉回归；AC-UI-02
- **关联 AC**：见 `docs/m2b-ac-op-gap.md`
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-INFO-01

- **需求编号**：FR-INFO-01
- **需求名称**：右侧信息面板 E 区（宽 220–320px，纵向滚动，字段不得横向溢出）
- **优先级**：P0
- **描述**：本工具应在右侧信息面板（E 区，宽 220–320px，纵向滚动，字段不得横向溢出）显示当前聚焦仓的仓库全名、RepoType、主状态徽标+中文文案+数据时间、分支、HEAD 短 SHA、upstream、ahead/behind/dirty 计数、最近一次操作结果、CI 占位区（“CI 未接入”或 6 态 Mock）以及“详情 / 在资源管理器中打开”快捷按钮；窄栏内字段换行规则固定（标签列 ≤72px）。
- **验收标准（AC）**：字段快照；窄栏 1280/200% 不溢出；AC-UI-11
- **关联 AC**：见 `docs/m2b-ac-op-gap.md`
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-INFO-02

- **需求编号**：FR-INFO-02
- **需求名称**：空态与一致性
- **优先级**：P0
- **描述**：本工具应在未选中聚焦仓时让信息面板显示空态引导“从中间文件树选择仓库”，不得显示上一个仓的残留数据；信息面板与底部按钮条必须绑定同一聚焦仓（同一时刻二者指向不一致即缺陷）；STALE 时显示“数据 X 前”与斜纹。
- **验收标准（AC）**：空态截图；切换仓后按钮条目标同步断言；AC-UI-11
- **关联 AC**：见 `docs/m2b-ac-op-gap.md`
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-INFO-03

- **需求编号**：FR-INFO-03
- **需求名称**：构建状态区（CI 通道 B）
- **优先级**：P1
- **描述**：本工具应在信息面板的构建状态区（CI 通道 B）于 v1 固定显示“CI 未接入”空态；配置 `ci.enabled=true` 时显示 Mock 6 态（PASS/FAIL/RUNNING/QUEUED/NONE/UNKNOWN）与数据新鲜度；该区域永不参与组件聚合与卡片底色。
- **验收标准（AC）**：CI Mock 开关截图；卡片底色不变；AC-UI-11/12
- **关联 AC**：见 `docs/m2b-ac-op-gap.md`
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`

## FR-INFO-04

- **需求编号**：FR-INFO-04
- **需求名称**：信息新鲜度
- **优先级**：P1
- **描述**：本工具应保证信息面板字段在操作后 ≤2 秒、外部 IDE 变更后 ≤10 秒刷新；过期字段灰化并保留最近成功时间；刷新时只更新字段值，不重建面板。
- **验收标准（AC）**：计时与视觉回归；AC-UI-11
- **关联 AC**：见 `docs/m2b-ac-op-gap.md`
- **状态**：已实现（待 M2b final Gate 签收；证据见 README/AC 台账）
- **关联域**：见 `reqs/archive/specification-v0.4.1.md` 功能域/附录 D；AC 台账 `docs/m2b-ac-op-gap.md`
