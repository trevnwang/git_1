# 02 · Git / 版本控制语义与本地仓库工程可靠性评审

> 评审透镜：**Git/VCS 语义 + Windows 本地仓库工程可靠性**
> 评审对象（只读）：`reqs\_tools\docx_dump.txt`（下称 **docx L\<n\>**）、`reqs\_tools\drawio_dump.txt`（下称 **drawio L\<n\>**）
> 不评审：UI 视觉/线框、.NET 多进程架构、数据库选型、通用需求写法。
> 结论口径：以「第一版能落地、不丢代码、状态不撒谎」为准绳；允许全盘否定草案中的判定算法。

---

## 总体结论

**方向成立，状态机与判定算法必须重做，批量写操作在当前描述下会真实丢代码。**

1. **判定地基不成立（P0）**。草案把 Gitea API 采到的「远程最新 commit」和本地 commit 做祖先关系判断（docx L95-104、L109-110、L118-119；drawio L21-25）。但祖先关系是本地对象图上的运算：**未 fetch 时本地没有远程 commit 对象**，`git merge-base` / `git rev-list` 无从解析；而 Gitea compare API 只返回 `total_commits` 与 `commits`，**没有 merge-base 字段**，且要求两个 SHA 都存在于服务端——**未推送的本地 commit 无解**。所以 docx L110「纯内存比对，无IO阻塞」与正确性不可兼得：必须先 fetch 维护 remote-tracking ref，再在本地做 `git merge-base --is-ancestor`。Gitea compare 只能做兜底（且仅对已推送 commit 有效）。

2. **状态枚举不完备且互不同构（P0）**。drawio 六状态（L19-25）与 docx 五状态（L99-104）不是同一组语义：`NEW_BRANCH` 在 docx 无对应；drawio 的 `CONFLICT` 实为「分叉（diverged）」而非「git 冲突（unmerged）」；两套**都没有** dirty 主状态、detached HEAD、空仓库、无 upstream、swallow/shallow、数据不可用（UNKNOWN）。而 dirty 在 drawio 里只降级成 `*` 装饰（L58 第 3.3 条），不参与聚合、不阻断写操作。

3. **「比较基准分支」没有定义（P0）**。这是所有状态的地基，但 docx 表结构只存单值 `latest_commit`（L118），`branch_list` 不含各分支 head（L119），`tag_list` 不含 tag→commit（L120，与 L68 声称采集 tag hash 矛盾）。进程 1 声称同步「所有分支最新 CommitHash」（L69）却在表里无处落。四仓分支不一致时 drawio 只说「显示 Impl 的分支」（L58 第 3.4 条），没有说以谁为比较基准。结果：本地 feature 分支对远程 main 比较，会全量假 CONFLICT。

4. **组件聚合优先级漏项（P0/P1）**。drawio L30-33 只聚合 CONFLICT > MISS > BEHIND > AHEAD，漏 NEW_BRANCH、dirty、UNMERGED、IN_PROGRESS、TAG_DRIFT、CI、UNKNOWN、批量操作部分失败；docx L105-107 只写「任一仓库异常则整体异常」，无法实现。

5. **批量写操作语义缺失（P0）**。「SYNC = 用远程分支覆盖本地」（drawio L58 第 3.5 条）没有说 fetch+reset 到哪条分支、untracked 怎么处理、本地未推送 commit 和 stash 怎么办、部分失败怎么回写；批量 Tag 切换（docx L146）没说 detach、缺 tag 仓库怎么办；批量删除（docx L148）没有回收站/脏仓保护/占用文件处理。按当前文字实现，`reset --hard` 会真实丢失 MBD 工作区里无法重生成的改动。

6. **Tag / 基线在 LLR 完全断档（P0）**。HLR 把「版本联动、基线管理」列为核心卖点（docx L15、L18、L41-46、L146），LLR 162 行内**没有任何 tag 一致性状态、基线表、tag→commit 快照或漂移检测**。而 MBD 的「基线」恰恰是四仓同 tag。

7. **MBD/Windows 工程约束零覆盖（P1）**。全文无 Git LFS、MAX_PATH/长路径、大小写不敏感、`.git` 为文件（worktree/submodule）、网络盘/OneDrive、磁盘占用、并发写锁、凭证不入库等任何一条；这些不是加分项，是 4000 仓 + 大模型文件场景下的**能否跑通**问题。

8. **MVP 建议收窄**：阶段一 = **只读看板 + 单仓状态机 + Clone/Repair + Tag 检出（detached 可见）**；阶段二 = 受控 SYNC（备份 ref + CAS + journal）；删除走「回收站/软删除」。CI 只做 badge，不进 git 状态机（CI 不是 VCS 语义，混进来是另一个坑）。

---

## 修正后的仓库状态机

### 0. 建模原则：一个主状态 + 若干正交标记，绝不「五状态打天下」

git 仓库的「真实状态」是 5 个互相独立的维度，硬压成 5~6 个互斥枚举必然丢信息、必然误判。修正为：

| 维度 | 含义 | 是否进入组件聚合 |
|---|---|---|
| **A 完整性** | 目录是否存在、是否真仓库、remote 是否还指向该组件仓、是否空仓 | 是 |
| **B HEAD 形态** | 在分支上 / detached@tag / detached@commit | 是（tag 检出是 MBD 常见合法态） |
| **C 同步关系** | 相对 upstream（remote-tracking ref）：同步/落后/超前/分叉/无 upstream | 是（核心） |
| **D 工作区** | 干净 / tracked 改动 / untracked / unmerged（真冲突）/ 操作中间态 | 进主状态 + 阻断写操作 |
| **E 基线/Tag** | 是否检出基线 tag、四仓 tag 是否一致、tag→commit 是否与快照一致 | 组件级独立维度 |
| **X 数据新鲜度** | 上次成功 fetch/scan 是否在 TTL 内 | 是（过期即 UNKNOWN） |

**关键原则**：

- 任何「无法判定」（未 fetch、无权限、repo 损坏、git 不可用、扫描超时）**必须有 UNKNOWN 状态**，禁止用「上次结果」冒充当前状态（docx L122-132 的表 2/表 3 没有成功/失败、新鲜度字段，必然假绿）。
- **detached HEAD 不是错误**：Tag 检出（docx L146）就是 detached。它必须能被表达，否则一律误判为 `NEW_BRANCH`。
- **`CONFLICT` 改名**：git 的 conflict 指 index 里的 unmerged 条目（`git ls-files -u` 非空）；草案的 `CONFLICT`（drawio L23）实际是「双方都有新 commit 的**分叉**」→ 改名 `DIVERGED`。真冲突另设 `UNMERGED`。命名错了，用户就会以为「点一下就能自动合并」。
- **`NEW_BRANCH` 必须拆三态**，回答「指哪一侧」：见下文 Q2 与表 A 的 `NO_UPSTREAM`。

### 1. 两套旧枚举的语义映射（证明其不完备）

| docx 五状态（L99-104） | drawio 六状态（L19-25） | 差异 / 缺口 |
|---|---|---|
| 同步 | `SYNCED` | 一致，但都没要求 upstream 已刷新 |
| 落后 | `BEHIND`（L22） | drawio 先判「本地历史不含远程最新」再细分，隐含需要远程对象（未 fetch 不成立） |
| 超前 | `AHEAD`（L24） | 一致 |
| 分叉 | `CONFLICT`（L23） | **命名错**：这是 DIVERGED，不是冲突 |
| 缺失 | `MISS`（L19） | 一致 |
| — | `NEW_BRANCH`（L20） | docx 无；且定义含糊（见 Q2） |
| — | — | 缺：UNKNOWN / EMPTY / DETACHED / NO_UPSTREAM / UNMERGED / IN_PROGRESS / DIRTY |

### 2. 表 A：单仓主状态（状态 | 精确触发条件 | 优先级 | 展示语义）

优先级数字越小越优先，**组件聚合 = 取最高优先级**（并叠加组件级态，见表 C）。所有条件默认「在 fetch 成功且数据新鲜」前提下判定；否则落到 `UNKNOWN`。

| 状态 | 精确触发条件 | 优先级 | 展示语义 |
|---|---|---|---|
| `UNKNOWN` | fetch/scan 失败或超 TTL；`git` 不可用；对象库损坏（`git fsck`/rev-parse 报错）；shallow 且 merge-base 不可解且 API 兜底失败；权限拒绝 | 0 | 灰紫「状态未知·请刷新」，**禁用 SYNC/删除等写操作** |
| `INVALID` | 目录存在但非 git 仓库；`rev-parse --show-toplevel` ≠ 组件目录（嵌套仓库）；`origin` URL 规范化后 ≠ 期望仓库；路径仅大小写不同而冲突；reparse point 指向外部 | 1 | 红「仓库绑定异常」，给「重新绑定/隔离」动作 |
| `MISSING` | 组件目录不存在（或为空且未 clone） | 2 | 灰「缺失」，给 Clone 动作 |
| `EMPTY` | 目录是仓库但 `HEAD` 未出生（unborn，`branch.oid (initial)`）；Gitea 端空仓无默认分支 | 3 | 灰「空仓库」，给「初始化/推送首个 commit」说明 |
| `UNMERGED` | `git ls-files -u` 非空（index 有 unmerged 条目，真冲突，未被 `git add` 解决） | 4 | 红「冲突未解决」，**禁止自动 SYNC** |
| `IN_PROGRESS` | 存在 `.git/MERGE_HEAD` / `rebase-merge` / `rebase-apply` / `CHERRY_PICK_HEAD` / `BISECT_LOG` | 5 | 橙「合并/变基未完成」，**禁止写操作** |
| `DETACHED_TAG` | `symbolic-ref -q HEAD` 失败，且 `git tag --points-at HEAD` 非空（可能多个，记录全部） | 6 | 青「已检出基线 tag `v1.2.3`（只读基线态）」，显示 tag 与 commit |
| `DETACHED_COMMIT` | HEAD 分离但无 tag 指向它 | 7 | 橙「游离 commit」，给「创建分支/检出分支」动作 |
| `DIVERGED` | 关系判定：`merge-base --is-ancestor HEAD ref` 与 `--is-ancestor ref HEAD` 均返回 1；或 Gitea 双向 compare 两侧 `total_commits>0` | 8 | 橙「本地与远程分叉（双方都有新提交）」，只允许「查看差异 / 建分支 / 手动合并」，禁止强覆盖 |
| `BEHIND` | `merge-base --is-ancestor HEAD ref` 返回 0 且 HEAD≠ref | 9 | 黄「落后 N 个提交」，主操作「同步」（受表 C 与 CAS 约束） |
| `AHEAD` | `merge-base --is-ancestor ref HEAD` 返回 0 且 HEAD≠ref | 10 | 蓝「本地超前 N 个未推送提交」，主操作「推送」 |
| `NO_UPSTREAM` | 分支无可用 upstream。三子型：`LOCAL_ONLY`（origin 无同名分支）/ `UNSET_BUT_EXISTS`（origin 有同名分支但未设 tracking）/ `UPSTREAM_GONE`（配置的 upstream ref 已不存在，远程删除） | 11 | 青「仅本地分支 / 未跟踪 / 远程分支已删除」，给「修复跟踪 / 推送建立远程分支 / 停止跟踪」动作 |
| `DIRTY` | HEAD 与 upstream 相同（关系为 EQUAL），但 tracked 有改动或（按配置）有 untracked / stash | 12 | 绿底+`*`「已同步但有未提交改动」，**阻断 SYNC 的 hard 覆盖** |
| `SYNCED` | 在分支上、upstream 存在、HEAD==upstream tip、工作区干净（且不在基线模式） | 13 | 绿「完全同步」 |

> `SYNCED` 是唯一的「绿色」；`DETACHED_TAG` 不是绿色也不是异常，它是**基线态**（颜色单列）。

### 3. 表 B：正交标记（不参与主状态优先级，但参与 UI 与阻断逻辑）

| 标记 | 判定 | 影响 | 展示 |
|---|---|---|---|
| `dirty_tracked` | `git --no-optional-locks diff-index --quiet HEAD --` 非 0 | 阻断 reset/SYNC 的 hard 覆盖、阻断 checkout 覆盖 | 名称后 `*`（沿用 drawio L58 第 3.3 条） |
| `untracked` | `git ls-files --others --exclude-standard` 非空 | `reset --hard` 不会删它 → 残留文件；是否清理需用户确认 | `*` + 同步确认清单 |
| `stashed` | `git rev-list --walk-reflogs --count refs/stash` 或 `git stash list` 非空 | 提示「有 stash，SYNC 不会动它」 | 徽标 |
| `shallow` | `git rev-parse --is-shallow-repository` = true | merge-base 可能不可解 → 关系判定降级为 UNKNOWN/API 兜底 | 徽标 + 提示「历史不完整」 |
| `lfs_missing` | 仓库含 LFS（`.gitattributes`/`git lfs ls-files`），但工作区是 pointer 或对象缺失 | 文件即使 `git status` 干净也是**坏的**（MBD 模型打不开） | 红点「LFS 对象缺失」 |
| `reparse_point` | Windows junction/symlink/OneDrive 占位 | 阻止删除、阻止 realpath 校验放行 | 徽标 + 写操作降级 |
| `stale` | 距上次成功 fetch/scan > TTL（建议抓取可见仓库 5 min，其余 30 min，可配） | 主状态强制降为 UNKNOWN | 灰点「数据过期」 |
| `busy` | `.git/index.lock` 存在 / 检测到外部 git 进程持有句柄 / 本工具队列中已有写操作 | 写操作返回 BUSY，不改文件 | 「外部 Git 正在操作」 |
| `remote_repo_absent` | 本地有，但 Gitea 已删该仓库 | 不得自动推送/覆盖；提示「远程仓库已删除」 | 红边 |
| `tag_mismatch` | 本地 tag 的 commit ≠ Gitea 端同名 tag 的 commit（tag 被移动） | 基线完整性告警 | 橙点 |
| `ci_failed` / `ci_pending` / `ci_none` | 按 HEAD commit（detached 时按 tag/commit）查 CI | **不进 git 主状态机**，只做 badge | 独立徽标（见 Q9） |
| `mixed_branch`（组件级） | 四仓当前分支名不一致 | 不显示「Impl 的分支」了事（drawio L58 第 3.4 条），应列出四仓分支 | 组件卡片 `Design:main / Impl:feat-x / …` |

### 4. 表 C：组件级状态与聚合优先级（修正 drawio L30-33）

组件级状态 = **组件覆盖态**（最高优先）+ 四仓主状态取最高优先级 + 组件级基线态 + 标记并集。**任何一仓 UNKNOWN ⇒ 组件不得显示 SYNCED。**

| 优先级 | 组件态 | 触发条件 | 展示 |
|---|---|---|---|
| 0 | `OP_PARTIAL_FAILED` | 批量操作 journal 中有 failed/cancelled/running 未完成项（需用户确认后才清除） | 红「批量操作部分失败 2/4」+ 逐仓错误码 + 「重试失败项」 |
| 1 | `BINDING_ERROR` | 同前缀仓库数 ≠4 / 后缀解析歧义 / 出现未绑定仓库 / 任一仓 `INVALID` | 红「组件绑定异常」+ 未绑定清单 |
| 2 | `UNKNOWN` | 任一仓 `UNKNOWN` 或 `stale` | 灰紫「部分状态未知」+ 哪些仓未知 |
| 3 | `MISSING` | 任一仓 `MISSING`（其余仓照常显示） | 灰「缺失 n/4」 |
| 4 | `BLOCKED_LOCAL` | 任一仓 `UNMERGED` / `IN_PROGRESS` | 红「本地冲突/操作未完成，禁止自动同步」 |
| 5 | `TAG_DRIFT` | 基线模式下四仓检出 tag 不同 / 某仓无该 tag / tag→commit 与基线快照不一致 | 橙「基线不一致」+ 逐仓 tag |
| 6 | `DIVERGED` | 任一仓 `DIVERGED` | 橙 |
| 7 | `BEHIND` | 任一仓 `BEHIND` | 黄「落后」 |
| 8 | `AHEAD` | 任一仓 `AHEAD` | 蓝「超前」 |
| 9 | `MIXED_BRANCH` | 四仓分支名不一致（且无更高级态） | 青「分支不一致」 |
| 10 | `DIRTY` | 任一仓 `DIRTY`（且无更高级态） | 绿+星 |
| 11 | `SYNCED` | 四仓均 `SYNCED` | 绿 |

> 组件「整体高亮」沿用 docx L107 的原则，但**优先级链必须显式枚举**，不能靠 if-else 顺手写（drawio L30-33 就漏了 4 项）。

---

## 判定伪代码（语言无关）

> 所有 `git(...)` 调用都封装为：`git --no-optional-locks -C <path> …`（只读命令默认加 `--no-optional-locks`，避免 status 刷新 index 与外部 IDE 抢 `index.lock`）；超时/退出码非 0 一律转为错误对象，不抛裸异常。

### 3.1 单仓采集：分层，别每 30 秒跑一次全量 `git status`

```pseudo
function collect_repo(r, mode /* cheap | full */) -> RepoState:
    s = RepoState(repo=r)

    # ---- 层 0：文件系统与归属（不依赖网络）----
    if not exists(r.path):                 s.primary = MISSING;      return s
    if not exists(r.path/".git"):          s.primary = INVALID(NOT_A_REPO); return s
    top = git(r, "rev-parse --show-toplevel")          # .git 为文件时也有效(worktree/submodule)
    if top is ERROR:                       s.primary = INVALID(BROKEN_REPO); return s
    if normalize_ci(top) != normalize_ci(r.path):
                                           s.primary = INVALID(NESTED_REPO); return s
    s.reparse = is_reparse_point(r.path)
    urls = git(r, "config --get-all remote.origin.url") | []
    s.origin_ok = any(normalize_url(u) == r.expected_origin_url for u in urls)
    if not s.origin_ok:                    s.primary = INVALID(REMOTE_MISMATCH); return s
    s.shallow = (git(r,"rev-parse --is-shallow-repository") == "true")

    # ---- 层 1：HEAD 形态（不依赖网络）----
    code, branch = git(r, "symbolic-ref --short -q HEAD")
    head = rev_parse(r, "HEAD")
    if head is None:                       s.primary = EMPTY;      return s     # unborn
    s.head = head
    if code == 0:
        s.head_kind = BRANCH; s.branch = branch
    else:
        s.tags_at_head = git(r, "tag --points-at HEAD") | []
        s.head_kind = DETACHED_TAG if s.tags_at_head else DETACHED_COMMIT

    # ---- 层 2：upstream 解析（只读本地配置 + 已 fetch 的 ref）----
    if s.head_kind == BRANCH:
        s.upstream = git(r, "for-each-ref --format=%(upstream) refs/heads/{branch}")
        s.remote   = git(r, "config --get branch.{branch}.remote") or "origin"
        s.remote_ref = "refs/remotes/{s.remote}/{branch}"
        if s.upstream == "":
            s.upstream_state = UNSET_BUT_EXISTS if ref_exists(s.remote_ref) else LOCAL_ONLY
            s.compare_ref    = s.remote_ref
        elif not ref_exists(s.upstream):
            s.upstream_state = UPSTREAM_GONE
            s.compare_ref    = null
        else:
            s.upstream_state = OK
            s.compare_ref    = s.upstream

    # ---- 层 3：同步关系（唯一权威 = 本地对象库 + remote-tracking ref）----
    s.fetched_at = read_fetch_stamp(r)      # 由 fetch 任务写入，不是文件 mtime
    s.fresh = (now() - s.fetched_at) <= FETCH_TTL(r)

    if s.head_kind == BRANCH and s.compare_ref != null:
        rtip = rev_parse(r, s.compare_ref)          # 解析失败 = 数据不完整
        if rtip is None:
            s.relation = UNKNOWN
        elif s.shallow and not merge_base_resolvable(HEAD, rtip):
            s.relation = relation_via_gitea_api(r, s.head, rtip)   # 仅当 head 已推送
        else:
            s.relation = classify_relation(s.head, rtip)
            (s.behind, s.ahead) = parse(git(r, "rev-list --left-right --count {s.compare_ref}...HEAD"))
        if not s.fresh and s.relation != UNKNOWN:
            s.flags += STALE                        # 过期数据只展示为陈旧，不据此执行写操作

    # ---- 层 4：工作区（cheap 层：conflict/中间态；full 层才扫 untracked）----
    s.unmerged    = nonempty(git(r, "ls-files -u -z"))          # 便宜且精确
    s.in_progress = exists(any of .git/MERGE_HEAD, rebase-merge, rebase-apply,
                                   CHERRY_PICK_HEAD, BISECT_LOG)
    if mode == full:
        s.dirty   = git(r, "diff-index --quiet HEAD --") != 0    # 只看 tracked
        s.untracked = nonempty(git(r, "ls-files --others --exclude-standard -z"))
        s.stashed = nonempty(git(r, "stash list"))
        if is_lfs_repo(r): s.lfs_missing = detect_lfs_missing(r)   # pointer 文件/对象缺失
    return project(s)


function classify_relation(a, b):        # a=HEAD, b=remote tip
    if a == b: return EQUAL
    if git_exit("merge-base --is-ancestor {a} {b}") == 0: return BEHIND   # a 是 b 祖先
    if git_exit("merge-base --is-ancestor {b} {a}") == 0: return AHEAD
    return DIVERGED                       # 退出码 >1 是错误，必须与 1 区分！


function project(s):                     # 维度 → 单值主状态（优先级见表 A）
    if s.primary: return s.primary       # MISSING/INVALID/EMPTY 已短路
    if not s.fresh and s.relation == UNKNOWN: return UNKNOWN
    if s.unmerged:    return UNMERGED
    if s.in_progress: return IN_PROGRESS
    if s.head_kind == DETACHED_TAG:    return DETACHED_TAG
    if s.head_kind == DETACHED_COMMIT: return DETACHED_COMMIT
    if s.relation == DIVERGED: return DIVERGED
    if s.relation == BEHIND:   return BEHIND
    if s.relation == AHEAD:    return AHEAD
    if s.upstream_state != OK: return NO_UPSTREAM(sub=s.upstream_state)
    if s.dirty or s.untracked: return DIRTY
    return SYNCED
```

**为什么不能用「Gitea 的远程最新 commit」直接判定（草案缺口）**：`git merge-base` 的输入必须是**本地对象库中存在的 commit**；Gitea API 返回的 SHA 在本地没有对应对象时，唯一能做的只是 `git cat-file -e` 失败。因此**比较前必须先 fetch**，比较对象是 `refs/remotes/<remote>/<branch>`。Gitea compare 的正确用法见 3.8。

**fetch 规范（正确且可重复）**：

```pseudo
function fetch_repo(r, branch):
    base = "git --no-optional-locks -C {r.path} fetch"
    # 关键点 1：显式 refspec 里的 + 允许强制更新 remote-tracking ref（force-push 场景）
    # 关键点 2：--prune 清掉远程已删分支的陈旧 ref，否则 DELETE 的分支永远显示存在
    # 关键点 3：--no-auto-gc/--no-auto-maintenance，后台轮询绝不触发自动维护（默认是开的）
    # 关键点 4：--no-tags + 单独按需 fetch tag，避免 tag 与 branch 语义混淆
    run(base + " --prune --no-tags --no-recurse-submodules --no-write-fetch-head " +
              "--no-auto-gc --no-auto-maintenance " +
              "origin +refs/heads/{branch}:refs/remotes/origin/{branch}")
    # 注意：`git fetch origin <branch>` 是否更新 refs/remotes/origin/<branch> 取决于
    # remote.origin.fetch 的映射；仓库被改过 refspec 时会只写 FETCH_HEAD。
    # 所以生产代码一律显式 refspec（或 --refmap），不要依赖隐式行为。
    write_fetch_stamp(r, branch, ok/err, remote_tip=rev_parse("refs/remotes/origin/{branch}"))
```

> 依据：git-fetch「CONFIGURED REMOTE-TRACKING BRANCHES」说明命令行 refspec 只决定取什么，`remote.<name>.fetch` 只决定存到哪；`--auto-maintenance` 默认开启；`--prune` 默认不 prune tag。见文末参考 [git-fetch]、[git-status]、[git-merge-base]。

### 3.2 组件聚合

```pseudo
function aggregate(component):
    if journal.has_unfinished_or_failed(component): return OP_PARTIAL_FAILED
    if component.unbound_repos or len(component.repos) != 4: return BINDING_ERROR
    if any(r.primary == UNKNOWN for r in repos):   return UNKNOWN
    if any(r.primary == INVALID for r in repos):   return BINDING_ERROR
    if any(r.primary == MISSING for r in repos):   return MISSING
    if any(r.primary in {UNMERGED, IN_PROGRESS}):  return BLOCKED_LOCAL
    if baseline_mode and not tag_consistent(repos): return TAG_DRIFT
    for st in [DIVERGED, BEHIND, AHEAD]:
        if any(r.primary == st for r in repos): return st
    if distinct(set(r.branch for r in repos if r.head_kind==BRANCH)).size > 1: return MIXED_BRANCH
    if any(r.primary == DIRTY for r in repos): return DIRTY
    if all(r.primary == SYNCED for r in repos): return SYNCED
    return UNKNOWN      # 兜底，禁止静默假绿


function tag_consistent(repos):
    tagsets = [set(r.tags_at_head) for r in repos]
    if any(r.head_kind != DETACHED_TAG for r in repos): return false
    common = intersect(tagsets);  if empty(common): return false
    # 同一 tag 在四仓必须指向各自基线快照记录的 commit（annotated tag 要解引用）
    for r in repos:
        sha = git(r, "rev-parse refs/tags/{tag}^{commit}")
        if sha != baseline_snapshot[r.type, tag]: return false
    return true
```

### 3.3 批量 SYNC（「用远程分支覆盖本地」的正确语义）

```pseudo
function sync_component(c, opts):
    # 阶段 1：现场预检（必须重新 scan，不能读缓存）
    plan = []
    for r in c.repos:
        st = collect_repo(r, full)
        if st.primary in {MISSING, EMPTY, INVALID, UNKNOWN, UNMERGED, IN_PROGRESS}:
            plan += Skip(r, st.primary); continue
        if st.head_kind != BRANCH and not opts.allow_switch_branch:
            plan += Skip(r, NEED_BRANCH); continue
        target_branch = opts.branch or st.branch or st.default_branch
        target_ref    = "refs/remotes/{st.remote}/{target_branch}"
        plan += Step(r,
                     target_ref     = target_ref,
                     expect_head    = st.head,            # CAS 依据
                     backup_needed  = (st.ahead > 0 or st.dirty or st.untracked or st.stashed),
                     dirty          = st.dirty)

    # 阶段 2：必须把「将丢失什么」先给用户看
    show(plan.removed_commits := for each Step: git log --oneline {target_ref}..HEAD)
    show(plan.untracked_files := for each Step: git clean -nd)   # dry-run，绝不默认执行
    if requires_confirm(plan) and not opts.confirmed: return CANCELLED

    # 阶段 3：按规范化路径排序后取跨进程锁（避免 A/B 互锁）；单仓失败不放弃其余仓
    for step in sort_by(normalize_ci(step.repo.path), plan):
        lock = acquire_named_mutex("MBDGit:{sha1(normalize_ci(path))}")   # 跨进程，进程内可重入
        if not lock: step.fail(BUSY); continue
        try:
            if index_lock_exists(path): step.fail(BUSY_EXTERNAL_GIT); continue

            # (1) 先备份，再覆盖（顺序不能反！）
            if step.backup_needed:
                ts = utc_now()
                git(update_ref, "refs/backup/sync/{ts}/{branch}", "HEAD")     # 保住未推送 commit
                if step.dirty: git(stash, "push", "-u", "-m", "mbdgit-pre-sync-" + ts)
                # 可选：git bundle create 作为离线备份（MBD 用户更信这个）

            # (2) fetch 目标 ref（失败则整个仓不动，工作区零改动）
            if not fetch_repo(r, target_branch): step.fail(NETWORK); continue

            # (3) CAS：确认 preflight 之后外部 IDE 没有动过 HEAD
            if rev_parse("HEAD") != step.expect_head: step.fail(CONCURRENT_CHANGE); continue
            if step.dirty and not opts.accept_local_changes_lost: step.fail(REFUSED); continue

            # (4) 移动（绝不使用 git pull —— 会产生 merge commit；绝不 checkout -f）
            if st.head_kind == BRANCH:
                git(reset, "--hard", step.target_ref)          # 只动 tracked，不动 untracked
            else:
                git(switch, "--force-create", target_branch, step.target_ref)

            # (5) untracked 清理：默认不删，用户显式勾选后才执行；不用 -x（会删构建/缓存产物）
            if opts.clean_untracked and opts.confirmed_clean:
                run("git clean -d -f")                          # 执行前必须展示 dry-run 清单

            # (6) LFS：reset 之后补拉，否则工作区是 pointer（git status 仍显示干净）
            if is_lfs_repo(r) and lfs_installed(): run("git lfs pull")

            step.ok(post_head = rev_parse("HEAD"), backup_ref = ...)
        finally:
            release(lock)

    # 阶段 4：逐仓回写 + 重扫；组件不得显示成功
    journal.commit(plan)
    refresh(c)
    return summary(ok=plan.ok, failed=plan.failed, skipped=plan.skipped)   # 组件 → OP_PARTIAL_FAILED
```

**明确回答「覆盖哪个分支」**：覆盖的是**本地当前分支对应的 `<remote>/<同名分支>`**（或用户在对话框显式选定的分支），**不是**远程默认分支。若本地分支在远程没有同名分支（`LOCAL_ONLY`），`SYNC` 必须拒绝（没有可覆盖对象），提示「推送创建远程分支」。

### 3.4 批量切换 Tag（detach 语义 + 基线快照）

```pseudo
function checkout_tag(component, tag):
    for r in component.repos:
        # 1) tag 是否存在于远程：优先 Gitea 元数据 + git ls-remote（不下载对象）
        if not tag_exists_local(r, tag):
            if not tag_exists_remote(r, tag):
                plan += Skip(r, TAG_ABSENT_REMOTE); continue
            # 必须 --force：远端 tag 被移动时普通 fetch 会拒绝覆盖，否则基线永远旧
            run("git -C {r.path} fetch --force --no-tags origin refs/tags/{tag}:refs/tags/{tag}")
        st = collect_repo(r, full)
        if st.unmerged or st.in_progress: plan += Skip(r, BLOCKED); continue
        if st.dirty and not opts.stash_ok: plan += Skip(r, DIRTY); continue
        # 2) detach 检出（git switch --detach；不要 checkout -f 静默丢改动）
        run("git -C {r.path} switch --detach refs/tags/{tag}")
        # 3) 记录基线快照：annotated tag 必须 ^{commit} 解引用，否则比较的是 tag 对象
        sha = git(r, "rev-parse refs/tags/{tag}^{commit}")
        baseline.record(component.prefix, tag, r.type, sha, utc_now())
    refresh(component)
    # 四仓全命中且快照一致 → DETACHED_TAG；否则组件 TAG_DRIFT（列出缺 tag/指向不同的仓）
```

### 3.5 批量 Clone（幂等 / 部分失败 / 超时）

```pseudo
function clone_component(component, opts):
    preflight:
        assert disk_free >= estimate_size(4) * SAFETY        # 4 仓 + .git/lfs，务必预检
        assert long_path_ok(workspace_root)                  # 见 P1-06
        assert creds_valid()                                 # 401 不重试
        assert gitea_repo_exists(component, all 4 types)     # 404 不重试
    for r in component.repos (parallel, maxConcurrent=2):
        if exists(r.path):
            if is_git(r.path):   repair(r)        # 已有仓：fetch + 检出，不覆盖工作区
            elif is_empty(r.path): clone_into(r)
            else: fail(PATH_OCCUPIED)             # 非空非仓库：绝不覆盖/删除
        else:
            env = {GIT_LFS_SKIP_SMUDGE: "1"}      # 先拿指针，Clone 完再后台 lfs pull
            run("git -c core.longpaths=true -c core.autocrlf=false clone " +
                "--origin origin --no-recurse-submodules {url} {path}",
                timeout = per_repo_timeout(30min), env=env)
            if lfs_repo(r): background("git -C {path} lfs pull")
        错误处理：NETWORK/DNS/5xx → 指数退避重试 ≤2；AUTH/403/404/PATH_TOO_LONG → 不重试，直接失败
        journal.record(r, result)                 # 成功仓不回滚；失败仓 → MISSING + error_code
    refresh(component)                            # 组件 → MISSING(部分) 或 OP_PARTIAL_FAILED
```

### 3.6 批量删除（默认可恢复）

```pseudo
function delete_component(component, opts):
    for r in component.repos:
        st = collect_repo(r, full)
        risk = (st.ahead>0 or st.dirty or st.untracked or st.unmerged or st.in_progress or st.stashed)
        if risk and not opts.force: return BLOCKED(r, WARN_UNSAVED_WORK)   # 必须二次确认
        p = realpath(r.path)
        assert under_workspace_root(p) and not is_reparse_point(p) and known_repo(p)  # 防误删
    # 软删除：同卷原子 rename 到 <root>\.trash\<component>-<utc>，延迟 N 天清理；
    # 或调用 Windows Shell API 移入回收站（仅本机卷可用）。不要直接 RemoveDirectory 递归。
    # 删除失败（MATLAB/Simulink/IDE 占用文件）→ 保留 DB 记录为 DELETE_PENDING，绝不先删记录。
    journal.commit(); refresh(component)   # 组件 → MISSING（或 DELETED 审计态，按 Q6 拍板）
```

### 3.7 部分失败回写（草案完全缺失）

```pseudo
OpJournal = { op_id, repo_id, op_type, state: queued|running|ok|failed|busy|cancelled,
              error_code, pre_head, post_head, expect_head, pre_ref, post_ref,
              backup_ref, started_at, finished_at, attempt }

on_startup_recovery():
    for op in journal.where(state in {queued, running}):
        # 不猜「成功/失败」，用 pre/post 事实推断
        cur = rev_parse(op.repo, "HEAD")
        op.state = (cur == op.expect_head) ? failed_not_started
                 : (cur == op.post_ref)    ? ok
                 : partial_unknown                     # 转入人工核对队列，UNKNOWN
    refresh_all()
```

### 3.8 Gitea compare 兜底（只用于「本地无法判定」且「commit 已推送」）

```pseudo
function relation_via_gitea_api(repo, local_sha, remote_sha):
    # 前置：local_sha 必须已存在于服务端，否则接口 404/500 → 直接 UNKNOWN
    a = GET /repos/{org}/{repo}/compare/{remote_sha}...{local_sha}   # head 独有提交
    b = GET /repos/{org}/{repo}/compare/{local_sha}...{remote_sha}   # remote 独有提交
    if a.error or b.error: return UNKNOWN
    if a.total_commits == 0 and b.total_commits == 0: return EQUAL
    if a.total_commits == 0: return BEHIND
    if b.total_commits == 0: return AHEAD
    return DIVERGED
    # 注意：Gitea Compare 结构只有 total_commits + commits，没有 merge_base；
    # 不要试图从 commits 列表反推拓扑，也不要对未推送 commit 调用（必然失败）。
```

---

## 阻断项 P0 / 高危 P1 / 中低危 P2

> 严重级：**P0** = 按草案实现必然产生错误结论或数据丢失，MVP 前必须改；**P1** = 会在真实 MBD/Windows 环境大概率翻车，第一版必须处理；**P2** = 一致性/健壮性缺陷，可排入 v1.1。

| ID | 严重级 | 证据 + 行号 | 问题 | 后果 | 修正建议 | 需用户决策 + 可选项 |
|---|---|---|---|---|---|---|
| G-P0-01 | **P0** | docx L95-104、L109-110、L118-119；drawio L21-25 | 用 Gitea API 的「远程最新 commit」与本地 commit 做祖先判定，未 fetch 时本地无远程对象；进程 3 号称「纯内存比对，无 IO」 | BEHIND/AHEAD/DIVERGED 大面积误判；CONFLICT 误报诱导用户强覆盖；「双数据源为准」无法裁决 | 改为「先 fetch 维护 `refs/remotes/<remote>/<branch>`，再本地 `merge-base --is-ancestor`」；Gitea compare 仅兜底（仅已推送 commit）；加 UNKNOWN 与 TTL | 是否允许后台自动 fetch（有网络流量/耗时）？可选项：仅在用户停留的组件上 fetch / 全局定时 fetch / 手动刷新才 fetch |
| G-P0-02 | **P0** | drawio L19-25；docx L99-104；drawio L14、L58(3.3) | 六状态与五状态互不同构，缺 UNKNOWN/EMPTY/DETACHED/NO_UPSTREAM/UNMERGED/IN_PROGRESS；dirty 只是 `*` 装饰，不参与聚合/不阻断 | tag 检出被判 NEW_BRANCH；空仓/无 upstream 崩溃或误判；dirty 仓被一键 SYNC 静默丢弃 | 采用本文表 A（14 态）+ 表 B（正交标记）+ 表 C（聚合链） | `NEW_BRANCH` 拆三个子态是否接受？（见 Q2） |
| G-P0-03 | **P0** | docx L66-70 vs L115-121；drawio L58(3.4) | 比较基准分支未定义；`git_remote_meta` 只有单值 `latest_commit`，`branch_list` 无各分支 head，`tag_list` 无 tag→commit（与 L68 矛盾）；进程 1 声称按分支采集，表放不下 | 本地 feature 分支 vs 远程 main → 全量假 CONFLICT；四仓分支不一致无法表达与聚合 | 显式字段：`compare_branch`、`upstream_ref`、`remote_tip_sha`、`remote_tip_fetched_at`、`remote_api_synced_at`；表按 (repo, branch) 存 head；组件级 `mixed_branch` 标记 | 比较基准默认值：本地当前分支同名远程分支 / 组织约定的集成分支 / 用户选定（三选一） |
| G-P0-04 | **P0** | drawio L58(3.5)；docx L100-104、L150 | 「SYNC=用远程分支覆盖本地」未定义：fetch 哪条、untracked/stash/未推送 commit 怎么办、失败如何回写；`reset --hard` 不删 untracked、`pull` 会造 merge commit | 未推送的 MBD 改动/模型不可重生成；工作区残留 untracked 导致「看起来新、实际混旧」；部分失败后状态显示成功 | 采用 3.3 伪代码：先备份（backup ref + stash + 可选 bundle）→ 显式 refspec fetch → CAS 校验 → `reset --hard <remote-ref>` → 可选 `clean` → `lfs pull` → journal 回写 | 是否允许覆盖未推送 commit？默认否。可选项：仅备份后允许 / 永不 / 逐仓确认 |
| G-P0-05 | **P0** | docx L15、L18、L41-46、L146；LLR 全文（L57-163）无 tag 状态/基线表；docx L120 vs L68 | HLR 的「版本联动/基线管理」在 LLR 断档：没有 tag 一致性状态、没有 tag→commit 快照、没有四仓基线规则 | 核心卖点无法交付；四仓 tag 漂移（基线不一致）在 MBD 交付里是严重问题却不可见；tag 被移动无人知 | 新增组件级 `TAG_DRIFT` 与 `baseline_snapshot(component, tag, repo_type, commit_sha, captured_at)`；annotated tag 用 `^{commit}`；`fetch --force` 更新被移动 tag；不用 `--prune-tags`（会静默删本地基线 tag） | 基线定义与冻结策略见 Q5 |
| G-P1-01 | 高 | docx L89、L109、L152-154、L55-56 | 30 s 轮询 100+ 组件（400+ 仓）跑 git 子进程；`git status` 默认刷新并写 index；`git fetch` 默认跑 auto-maintenance | 与 IDE/MATLAB 抢 `.git/index.lock`；磁盘 IO 风暴；扫描做不完 → 结果过期却照旧展示（假绿） | 分 cheap/full 两层：cheap=rev-parse/for-each-ref/ls-files -u；full=`--no-optional-locks status --porcelain=v2 --branch -z`；可见组件优先 + 抖动 + 自适应退避；`--no-auto-gc --no-auto-maintenance`；记录 `last_success_at/generation` | 是否允许开启 `core.untrackedCache`/`core.fsmonitor`（会改用户仓库配置）？ |
| G-P1-02 | 高 | docx L61-75 | 4000+ 仓 6 h 全量、单次 120 s 超时、每仓取 branches+tags+commits：Gitea 分页单页上限 50（默认 30），仅 repo 列表就 ≥80 页，再加每仓 ≥2 次请求约 8000+ 请求 | 120 s 内不可能完成 → 每次都超时/半量；重试 2 次加剧；状态基准长期过期；与 HLR L56「秒级感知」矛盾 | org repos 分页 + `X-Total-Count` 游标；只对「本地已绑定/关注」的仓抓 branches/tags；条件请求（ETag/If-Modified-Since）；分片 sync + 可恢复断点；部分成功语义 | 是否接受「远端元数据非实时，仅本地 fetch 实时」？可选项：两者都实时（成本高）/ 仅看板实时 |
| G-P1-03 | 高 | docx L32、L145 | 批量 Clone 无并发上限/超时/幂等/预检/部分失败回写 | 目标目录已存在 → clone 失败留下半仓；网络抖动下一次失败拖垮整组件；磁盘打满；凭证错误反复重试 | 采用 3.5 伪代码（并发 2、per-repo 超时、repair 模式、错误分类重试、逐仓 journal） | Clone 是否走 LFS 懒拉取（离线时文件可能打不开）？见 Q8 |
| G-P1-04 | 高 | docx L148 | 「组件一键整体删除（本地四仓全部清空）」无回收站/备份/脏仓保护/占用文件处理 | 误删不可恢复；MATLAB 占用文件导致删一半；DB 记录先删导致状态错乱 | 采用 3.6 伪代码：软删除（.trash 或回收站）、脏仓/AHEAD/IN_PROGRESS 阻断、realpath 校验、失败保留 DELETE_PENDING | 删除是否必须可恢复？默认是；保留期 N 天（默认 7/30） |
| G-P1-05 | 高 | docx L51、L150、L152-154 | 允许用户外部 git 操作，但工具无 per-repo 锁、无 CAS、无 BUSY 态 | 工具 reset 覆盖用户刚在 IDE 里 commit 的内容；index.lock 报错被当成「未知失败」；两个批量操作互相踩 | 跨进程命名 Mutex（key=规范化 realpath）+ BUSY 态 + 写前 CAS（preflight HEAD == 执行时 HEAD）+ generation 版本号丢弃过期扫描结果 | 检测到外部 git 正在操作时：等待 30 s / 立即失败 / 仅提示（三选一） |
| G-P1-06 | 高 | 全文无 LFS / longpaths / 大小写 / 磁盘 / 网络盘 关键词（docx L160-163 仅技术栈） | MBD 工程属性零覆盖：Git LFS（大模型/标定文件）、Windows MAX_PATH、大小写不敏感、`.git` 为文件（worktree/submodule）、OneDrive/网络盘、磁盘占用 | LFS 未安装或 smudge 失败 → 工作区是 pointer，`git status` 却显示干净（假绿）；长路径 clone 中断留半仓；大小写不同仓库名在同一目录冲突；网络盘上 git 锁失效/数据损坏 | LFS 探测与 `lfs fetch/pull`、缺失对象检测；`core.longpaths=true` + 应用 longPathAware manifest；路径长度预检与错误码 `PATH_TOO_LONG`；大小写归一化键 + 碰撞检测；拒绝/警告网络盘与 reparse point；磁盘空间预检 + `.git/lfs` 占用统计 | 是否强制要求安装 Git LFS（缺失即阻断）？见 Q8 |
| G-P1-07 | 高 | drawio L58 第 1 条（配置存本地 json） | 若按常见做法把 token 拼进 remote URL（`https://user:token@gitea/...` 或 `insteadOf`），会写入 `.git/config`、日志、进程命令行 | 凭证泄漏到每个受管仓库与日志文件；重置 token 需清理 400+ 仓 | 用 credential helper / `GIT_ASKPASS` 临时通道；remote URL 存无凭证形式；日志脱敏（禁止打印完整命令行的 URL 参数）；配置加密存储 | 凭证存储方式（Windows Credential Manager / DPAPI 加密文件）由用户拍板 |
| G-P1-08 | 高 | drawio L12；docx L49 | 用 remote URL「包含组织同名仓库」做归属判断（子串匹配，忽略 ssh/https/`.git`/redirect/`insteadOf`/大小写）；docx L49 明确「无需校验前缀合法性」 | 误绑定/漏绑定；Gitea 仓库改名后全部失配；组件前缀解析错（`..._SIL_Design`、大小写、`_Design2`）→ 组件凭空多/少仓 | 以 Gitea 数字 repo id 为主键（name 仅展示）；URL 规范化比较；前缀正则 `^(?<prefix>.+)_(?i)(design|impl|mil|sil)$` 严格绑定并输出「未绑定仓库」清单；改名 = 迁移事件 | 未绑定仓库：静默忽略 / 列「孤儿仓库」告警（建议后者） |
| G-P1-09 | 高 | docx L122-132 | 表 2/表 3 无「上次成功时间/失败原因/generation」，扫描失败时保留旧值 | 进程崩溃/网络断开时看板长期显示旧绿色；用户按过期状态做 SYNC | 增加 `last_success_at`、`last_error_code`、`generation`；超 TTL 强制 UNKNOWN；UI 显示「数据 x 分钟前」 | 新鲜度 TTL 取值（30 s / 5 min / 手动） |
| G-P1-10 | 高 | docx L152-158（兜底/日志），但无操作台账 | 无操作 journal / 崩溃恢复：批量操作进行中进程崩溃后无法判断哪些仓改了、哪些没改 | 状态与磁盘不一致；重复执行导致二次覆盖；用户无法信任「部分成功」 | 引入 `op_journal`（3.7）与启动 reconcile；所有写操作先写 journal 再执行；日志可回溯（docx L158 的落地方式） | 崩溃后自动恢复 vs 仅提示人工核对 |
| G-P2-01 | 中 | docx L139-143 vs drawio L58(3.1) | 状态→颜色两处矛盾：docx 蓝=超前/橙=分叉/红=CI失败+缺失；drawio 红=超前/黄=冲突/灰=MISS/其它=滞后 | 数据模型枚举颜色映射无权威来源，实现与验收必然扯皮 | 状态枚举为唯一权威，颜色由一张集中映射表派生（不写死在 UI 文案里） | 以 docx 还是 drawio 为准（建议以状态枚举为准，两个都重画） |
| G-P2-02 | 中 | docx L50 | 「远程真实数据 + 本地真实数据双为准」没有优先级与新鲜度规则 | Gitea API 与 fetch 后的 remote-tracking ref 不一致时无法裁决 | 单一权威：fetch 后的 remote-tracking ref 用于关系判定；Gitea API 用于发现/存在性/CI；冲突记 drift 告警 | 是否接受「fetch 权威、API 从属」 |
| G-P2-03 | 中 | 全文无 shallow/graft 处理（docx L61-163） | shallow clone（含 `--depth`）下 `merge-base` 可能不可解，`rev-list --count` 也偏小 | 误判 CONFLICT 或落后数量错误；`fetch --unshallow` 可能拉数 GB | 检测 `rev-parse --is-shallow-repository`；不可解时降级 API 兜底或 UNKNOWN；需要时提示「补全历史」 | 是否允许自动 unshallow（大流量） |
| G-P2-04 | 中 | docx L115-132（无 EMPTY 相关字段）；git-status `branch.oid (initial)` | 空仓库/unborn HEAD 未处理：`git rev-parse HEAD` 失败、Gitea 空仓无默认分支 | 进程异常或状态错乱；CREATE 批量建分支/首次推送语义不明 | 新增 EMPTY 态；Create 时显式指定基线 commit（首个 push 的父提交） | 空仓组件是否允许 CREATE 分支 |
| G-P2-05 | 中 | docx L86、L125 | `unpush_commit_count` 依赖 upstream 存在且对象已 fetch；process 2 采集「未推送 commit 列表」但表只有 count | upstream 缺失/ref 过期时计数错误（把已推送算成未推送）；无法展示具体哪些 commit | 改为 `git rev-list --count @{u}..HEAD` 仅在 upstream 有效时计算；存 commit 列表摘要；无 upstream 时显式 `NO_UPSTREAM` | — |
| G-P2-06 | 中 | docx L68、L120、L146；drawio L58(3.6) | annotated tag 与 lightweight tag 混用、tag 被远端移动（force update）、tag 删除均未定义；比较用 tag 对象 sha 会误判不一致 | 基线判等错误；四仓同名 tag 实际指向不同 commit 却显示一致 | `rev-parse refs/tags/<t>^{commit}` 比较提交；记录 tag 对象类型；tag 删除/移动单独告警（不用 `--prune-tags`） | tag 是否允许被移动/删除（基线是否冻结） |
| G-P2-07 | 中 | docx L87、L126、L143 | CI 状态挂在 `git_local_cache` 却以 commit 为键；pull/rebase 后 commit 变化，CI 状态未失效；detached/tag 检出时 CI 归属不明 | 显示「绿色通过」但其实是旧 commit 的 CI；tag 检出后 CI 语义混乱 | CI 单独表 (repo, commit_sha, pipeline, status, updated_at)；查询时以当前 HEAD/tag 对应 commit 为键并显示 commit 短 sha；过期即 NONE | CI 是否进组件聚合/颜色（建议只做 badge，见 Q9） |
| G-P2-08 | 中 | drawio L9、L11-14 | 遍历「直接子文件夹（非递归）」；`.git` 可能是文件（worktree/submodule）；嵌套/子模块仓库未建模 | 漏识别 worktree 仓库；子模块被当作普通目录（MBD 常内嵌第三方模型库）；递归遍历误入 reparse point 死循环 | 用 `rev-parse --show-toplevel` 确认边界；识别 `.git` 文件；子模块单列 `submodule` 标记（只读）；遍历跳过 reparse point | 子模块是否需要纳管（建议 v1 不纳管，只标灰） |
| G-P2-09 | 中 | 全文无（drawio L58 的 CREATE 只提重名 dry-run） | 分支/tag/仓库名的 Windows 非法字符与大小写碰撞：CREATE 分支时用户输入 `a:b`、`..`、`@{}`；两个仓库名仅大小写不同 → 同目录冲突 | 创建失败；clone 时覆盖；路径逃逸风险 | 创建前 `git check-ref-format --branch` 校验；路径用规范化键并检测大小写碰撞；拒绝 `..`/绝对路径 | — |

---

## 必须由用户拍板的语义问题

> 这些问题不是「实现细节」，而是**产品语义**，不定就会在实现层被随意决定，且验收时无法判定对错。每条给默认建议，用户可改。

**Q1. 「比较基准」到底是哪条分支？**
本地当前分支 vs 远程同名分支 / 组织约定的集成分支（如 `main`/`develop`）/ 用户手工选定。
影响：所有 BEHIND/AHEAD/DIVERGED 的定义。默认建议：**本地当前分支的 origin 同名分支**，四仓默认分支不同则组件显示 `MIXED_BRANCH`。

**Q2. `NEW_BRANCH` 指哪一侧？怎么拆？**
草案（drawio L20）「本地仓库的当前分支不存在」字面不成立（当前分支不存在只能是 unborn 或损坏）。建议拆三态：
① `LOCAL_ONLY`：本地新分支，远程没有 → 语义是「未推送的新分支」，**不是错误**；
② `UNSET_BUT_EXISTS`：远程有同名分支但本地没设 tracking → 可自动修复，**不是错误**；
③ `UPSTREAM_GONE`：远程分支已被删除 → 告警，需用户决定是否停止跟踪/删除本地。
默认建议：拆三态；`LOCAL_ONLY` 展示为「仅本地分支」并给「推送创建远程分支」动作。

**Q3. 一键 SYNC 允许丢弃什么？**
候选：未推送 commit / dirty tracked 改动 / untracked 文件 / stash。
默认建议：**未推送 commit 必须备份后丢弃（默认拒绝，需显式确认）**；dirty 改动默认拒绝覆盖（可 stash）；untracked 默认不删（`reset --hard` 本来就不删，需用户勾选才 `clean -d -f`，禁止 `-x`）。

**Q4. Tag 检出后允许 Commit/Push 吗？推到哪？**
detached HEAD 上 commit 会产生游离 commit，`push` 无上游。
默认建议：**v1 在 detached 下禁止 COMMIT/PUSH**，只允许「基于此 tag 创建分支」；或允许 commit 但强制要求先创建分支。

**Q5. 「组件基线」的定义与冻结策略？**
四仓是否必须同 tag？基线是否要冻结快照（tag→4 个 commit sha）？tag 允许被移动/删除吗？
默认建议：**基线 = 四仓检出同一 tag 且与快照一致**；快照入库（首次检出/首次发现时冻结）；tag 移动/删除单独告警，不自动跟随。

**Q6. 删除的语义与可恢复性？**
回收站 / `.trash` 软删除 / 直接删除？保留期？脏仓/AHEAD 是否一律阻断？
默认建议：软删除 + 保留 7~30 天可配；有未提交/未推送/操作中间态时**默认阻断**，force 后也先导出 bundle。

**Q7. 仓库身份主键：Gitea 数字 id 还是仓库名？**
改名/迁移后如何跟随？
默认建议：**数字 id 为主键，name 仅展示**；改名自动迁移并提示，组件重新绑定需用户确认。

**Q8. Git LFS 策略：**
是否强制安装 git-lfs（缺失即阻断）？Clone 是否 `GIT_LFS_SKIP_SMUDGE=1` 先拿指针、按需/后台 `lfs pull`（离线打开文件会失败）？
默认建议：**检测到 LFS 仓库则要求安装 LFS**；Clone 先跳 smudge，再后台 pull 并显示进度；缺失对象标红（不能显示干净）。

**Q9. CI 状态是否进入组件状态机/颜色？**
docx L143 把「CI 失败」与「仓库缺失」都归红色，drawio L58(3.1) 的红却是「超前」。
默认建议：**CI 只做独立 badge，不参与 git 主状态优先级，不改组件主色**（否则「落后但 CI 通过」会显示成绿色，误导用户）。

**Q10. 未绑定仓库/异常命名怎么处理？**
docx L49 假设「命名全局唯一，无需校验」，但 4000 仓现实中会有 `_Design2`、大小写、改名残留。
默认建议：严格正则绑定 + 输出「未绑定仓库」清单（不静默忽略），组件数 ≠4 时仍展示但标记 `BINDING_ERROR`。

**Q11. 外部 IDE 正在操作 git 时，工具的默认行为？**
等待重试 / 立即失败提示 / 只读展示。
默认建议：**写操作立即返回 BUSY 并提示，不自动删 `.git/index.lock`**（删别人的锁 = 可能损坏并发 git 状态）。

**Q12. 数据新鲜度 TTL 与「过期数据能否操作」？**
默认建议：关系判定依赖 fetch，TTL 内可直接展示；超 TTL 标 STALE 并禁止写操作（写前必须重新 fetch+scan）。

**Q13. 网络盘 / OneDrive / 加密盘上的工作区是否允许？**
默认建议：**允许只读展示，禁止写操作并明确告警**（这些文件系统上 git 锁与 rename 语义不可靠）。

---

## 参考（用于核对上述 git/Gitea 语义）

- [git-status](https://git-scm.com/docs/git-status)：`--porcelain=v2` 分支头（`branch.oid/head/upstream/ab`；`branch.ab` 仅在 upstream 存在且 commit 可见时出现），以及 BACKGROUND REFRESH 明确建议后台调用用 `git --no-optional-locks status`。
- [git-merge-base](https://git-scm.com/docs/git-merge-base)：`--is-ancestor`「exit 0 if true, 1 if not；错误为非 1 的非零」——实现必须区分 1 与 >1。
- [git-fetch](https://git-scm.com/docs/git-fetch)：CONFIGURED REMOTE-TRACKING BRANCHES（命令行 refspec 只决定取什么，`remote.<name>.fetch` 决定存到哪，可用 `--refmap` 覆盖）；`--auto-maintenance` 默认开启；`--prune` 默认不 prune tag；`+` 前缀允许强制更新 remote-tracking ref。
- [Gitea Compare API](https://docs.gitea.com/enterprise/api/operations/repo-compare-diff/)：`GET /repos/{owner}/{repo}/compare/{basehead}`，返回 compare 结果。
- [Gitea Compare 结构体](https://gitea.cn/gitea/gitea/raw/branch/main/modules/structs/repo_compare.go)：只有 `total_commits` + `commits`，**无 merge_base 字段**。
- [Gitea SDK 分页上限](https://gitea.cn/gitea/tea/src/commit/12d13c2d20bc04e70a3a689d858040131aefd223/vendor/code.gitea.io/sdk/gitea/list_options.go)：`maxPageSize = 50`、`defaultPageSize = 10/30`。
- [Git for Windows 长路径](https://gitforwindows.org/git-cannot-create-a-file-or-directory-with-a-long-path.html)：默认不开启长路径，需 `core.longpaths=true`，脚本命令仍可能失败。
- [Windows MAX_PATH 与 LongPathsEnabled](https://support.atlassian.com/bamboo/kb/git-checkouts-fail-on-windows-with-filename-too-long-error-unable-to-create-file-errors/)：Windows 10 1607+ 可启用长路径。
