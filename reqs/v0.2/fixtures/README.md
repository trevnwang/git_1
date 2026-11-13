# 状态矩阵本地 git 夹具（SRS v0.2.1 §7.2/§7.3/§7.4 + 附录 C）

本目录是 MBD 组件全生命周期 Git 工具的 **M0 测试资产**：一个可在 Windows 上离线、可重复运行的
本地 git 状态矩阵夹具生成器，以及与之配套的期望值静态副本。被测程序（状态计算 / 组件聚合）拿到
同一套 manifest 即可做表驱动断言，不需要真实 Gitea、不需要联网、不接触用户仓库。

> **版本口径（2026-11-14）**：本目录已按 `reqs/review/07-freeze-review.md` 独立冻结复评对齐
> **SRS v0.2.1**：聚合优先级回归 SRS §7.4（**`MISSING=3` 先于 `BLOCKED_LOCAL=4`**，C-03），
> 新增 `F-MISSING+BLOCKED` 顺序锁定夹具，`expected.json.source` 改为 `SRS v0.2.1`。
> **聚合顺序以 SRS v0.2.1 §7.4 为唯一权威**；脚本 `$script:AggregatePriority`、`expected.json`
> 与图集 D3-c/D3-d 必须同改（修改前先跑 `-Only F-MISSING+BLOCKED` 与 `-Only F-SYNCED`）。

| 文件 | 作用 |
|---|---|
| `state-matrix.ps1` | 生成 **25 个** 4 仓组件夹具（附录 C 全覆盖 + C-03 顺序锁定组合），逐仓用只读 `git` 命令自校验，输出 `manifest.json` |
| `expected.json` | 期望值静态副本（`fixture_id → expected_primary_state / expected_sub_type / expected_flags / expected_component_aggregate / expected_repos`），测试直接比对；脚本启动时校验它与内部定义一致 |
| `README.md` | 本文件：用法、输出结构、接入测试方法、已知限制、验证记录 |

状态名严格取自 SRS v0.2.1 §7.2（14 个主状态）、§7.3（11 flag）、§7.4（15 个组件聚合态；**聚合顺序以 SRS v0.2.1 为准**）；`NO_UPSTREAM` 子型
使用 7.2 名称 `LOCAL_ONLY` / `UNSET_BUT_EXISTS` / `UPSTREAM_GONE`（manifest 中
`expected_primary_state: "NO_UPSTREAM"` + `expected_sub_type: "..."`）。

---

## 1. 前置条件

* Windows PowerShell 7（`pwsh`，脚本以 `#Requires -Version 7.0` 声明）；
* Git **≥ 2.40**（SRS 附录 D `git.path` 约束；本机验证用 2.56.1）；
* 可选：`git-lfs` ≥ 3.x。仅 `F-LFS-MISSING` 需要；不可用时该 fixture 被**显式跳过**并在
  manifest 中记录 `status: "skipped"` 与原因，不计为失败。
* 不需要网络；脚本所有 origin 都是本地 `git init --bare` 的 bare 仓库（file 路径）。

脚本会为子进程设置隔离的 `GIT_CONFIG_GLOBAL` / `GIT_CONFIG_SYSTEM`（生成在输出根目录内），
**不会读写用户的全局/系统 git 配置**。

---

## 2. 用法

```powershell
# 全量生成（默认根目录 $env:TEMP\MbdGitFixtures\run-<yyyyMMdd-HHmmss>）
pwsh -NoProfile -File .\state-matrix.ps1

# 指定根目录 + 指定组件 ID
pwsh -NoProfile -File .\state-matrix.ps1 -Root D:\Temp\mbd-fixtures -Component CF325F_ChanganSigProcgBascFct

# 只生成部分 fixture（支持通配符；逗号分隔或多次 -Only）
pwsh -NoProfile -File .\state-matrix.ps1 -Only F-SYNCED
pwsh -NoProfile -File .\state-matrix.ps1 -Only 'F-MISSING+BLOCKED'      # C-03 聚合顺序锁定
pwsh -NoProfile -File .\state-matrix.ps1 -Only 'F-TAG*'
pwsh -NoProfile -File .\state-matrix.ps1 -Only 'F-DETACHED-TAG,F-DETACHED-COMMIT'

# 显式跳过 LFS 夹具（manifest 记 skipped 原因）
pwsh -NoProfile -File .\state-matrix.ps1 -SkipLfs

# 清理（只删除带 .mbd-fixtures-root.json 标记的目录）
pwsh -NoProfile -File .\state-matrix.ps1 -Clean                         # 清空 %TEMP%\MbdGitFixtures 下本脚本的 run-*
pwsh -NoProfile -File .\state-matrix.ps1 -Root D:\Temp\mbd-fixtures -Clean
```

参数一览：

| 参数 | 默认 | 说明 |
|---|---|---|
| `-Root` | `$env:TEMP\MbdGitFixtures\run-<时间戳>` | 输出根目录。若已存在且非空，必须是本脚本创建的（有标记文件），否则拒绝执行 |
| `-Component` | `CF325F_ChanganSigProcgBascFct` | 组件 ID → 四仓目录名 `<Component>_Design/_Impl/_MIL/_SIL` |
| `-Only` | 全部 25 个 | 按 `fixture_id` 过滤，支持 `*` 通配符、逗号分隔、多次指定 |
| `-Clean` | 关 | 只清理，不生成；安全标记见上 |
| `-SkipLfs` | 关 | 跳过 `F-LFS-MISSING` |

退出码：`0` 通过（或仅 LFS 跳过）；`2` 前置不满足（git 缺失/<2.40）；`3` 夹具构造失败；
`4` 自校验与期望不一致或 `expected.json` 与脚本定义失配；`5` 参数/目录安全校验失败；`6` `-Clean` 失败。
失败不会被静默吞掉：构造失败/自校验失败都会在控制台与 manifest 中给出明细。

幂等性：每次运行会先删除同名 fixture 目录再重建；同一 `-Root` 可重复使用。

---

## 3. 输出结构

```
<Root>/
├─ .mbd-fixtures-root.json        # 安全标记（-Clean 只认它）
├─ .gitconfig / .gitconfig-system # 隔离的 git 配置（不触碰用户配置）
├─ manifest.json                  # 全局清单（测试入口）
└─ F-SYNCED/                      # 一个 fixture 一个目录
   ├─ manifest.json               # 该 fixture 的单条记录（同全局清单里的结构）
   ├─ fixture-meta.json           # Gitea/DB 侧事实的离线替身（见 §4）
   ├─ origins/<component>_X.git   # 4 个本地 bare origin（F-REMOTE-GONE 等除外）
   └─ workspaces/<component>/     # 组件目录，内含 <component>_Design/_Impl/_MIL/_SIL
```

`manifest.json` 关键字段：

```jsonc
{
  "schema_version": "1.0",
  "generated_at": "2026-11-14T00:28:04+08:00",
  "environment": { "pwsh": "...", "git_version": "git version 2.56.1.windows.1", ... },
  "root": "<Root>",
  "component": "CF325F_ChanganSigProcgBascFct",
  "summary": { "fixture_total": 25, "passed": 25, "failed_build": 0, "failed_self_check": 0, "skipped": 0, "elapsed_seconds": 341.64 },
  "fixtures": [
    {
      "fixture_id": "F-TAG-MOVED",
      "repo_path": "<Root>/F-TAG-MOVED/workspaces/CF325F_ChanganSigProcgBascFct",
      "expected_primary_state": "SYNCED",
      "expected_sub_type": null,
      "expected_flags": ["tag_moved"],
      "expected_component_aggregate": "TAG_DRIFT",
      "expected_repos": { "Design": "SYNCED", "Impl": "SYNCED", "MIL": "SYNCED", "SIL": "SYNCED" },
      "notes": "……构造说明……",
      "status": "passed",                      // passed | failed_build | failed_self_check | skipped
      "skip_reason": null,
      "build_error": null,
      "metadata": { "gitea_simulation": { "remote_repo_absent": false, "ttl_expired": false, "job_state": null },
                    "expected_origin_urls": { "Design": "…", "Impl": "…", "MIL": "…", "SIL": "…" } },
      "repos": [                               // 四仓逐仓期望 + 自校验证据
        {
          "repo_type": "Design",
          "path": "…/<component>_Design",
          "expected_origin_url": "…/origins/<component>_Design.git",
          "configured_origin_url": "…",
          "expected_primary_state": "SYNCED",
          "expected_sub_type": null,
          "self_check": {
            "ok": true,
            "derived_primary_state": "SYNCED",
            "derived_sub_type": null,
            "derived_flags": ["tag_moved"],
            "mismatches": [],
            "evidence": {                       // git 只读命令的原始结论
              "exists": true, "is_repo": true, "top_level_matches": true,
              "origin_matches": true, "unborn": false, "branch": "main", "detached": false,
              "tags_at_head": [], "unmerged_count": 0, "in_progress_markers": [],
              "upstream_configured": true, "upstream_ref": "refs/remotes/origin/main",
              "upstream_ref_exists": true, "compare_ref": "refs/remotes/origin/main",
              "relation": "EQUAL", "ahead": 0, "behind": 0,
              "dirty_tracked": false, "untracked_files": [], "stashed": [],
              "shallow": false, "index_lock": false, "remote_reachable": true,
              "local_tags": { "v1.0": "<old sha>" }, "remote_tags": { "v1.0": "<new sha>" },
              "tag_moved": true, "lfs_tracked": false, "lfs_missing": false,
              "status_porcelain_v2": [ "…" ], "ls_files_u": [], "errors": []
            }
          }
        }
      ],
      "self_check": {
        "ok": true,
        "derived_component_aggregate": "TAG_DRIFT",
        "expected_component_aggregate": "TAG_DRIFT",
        "representative_primary_state": "SYNCED",
        "derived_component_flags": ["tag_moved"],
        "expected_flags": ["tag_moved"],
        "mixed_branch": false, "tag_drift": true, "tag_moved": true,
        "tag_sets": ["v1.0", "v1.0", "v1.0", "v1.0"],
        "branch_names": ["main"],
        "aggregate_candidates": ["TAG_DRIFT", "SYNCED"],
        "mismatches": [],
        "commands": [ "git --no-optional-locks -C <repo> status --porcelain=v2 --branch", "…" ]
      }
    }
  ]
}
```

自校验不只看“文件存在”，而是用被测程序同款的只读 git 命令推导状态再与期望比对：
`status --porcelain=v2 --branch`、`rev-parse --is-shallow-repository`、`ls-files -u`、
`MERGE_HEAD/rebase-merge/rebase-apply/CHERRY_PICK_HEAD/BISECT_LOG` 存在性、
`symbolic-ref HEAD`、`tag --points-at HEAD`、`rev-parse @{upstream}`、`merge-base --is-ancestor`
（双向）、`diff-index --quiet HEAD --`、`ls-files --others`、`stash list`、`ls-remote --heads origin`、
`for-each-ref refs/tags` + `rev-parse refs/tags/<t>^{commit}`。原始结论写在 `repos[].self_check.evidence`。

---

## 4. Gitea/DB 侧事实的离线替身

离线夹具无法真的制造“Gitea 已删仓 / 服务端 TTL 过期 / 批量 job 2/4 失败”，因此把这类
**非 git 事实**统一收敛到 `fixture-meta.json`（同时内联在 manifest 的 `metadata` 中），由测试代码
注入远程元数据层 / `IClock` / job journal：

```jsonc
{
  "fixture_id": "F-REMOTE-GONE",
  "fixture_notes": "……",
  "gitea_simulation": { "remote_repo_absent": true, "ttl_expired": false, "job_state": null },
  "expected_origin_urls": { "Design": "…", "Impl": "…", "MIL": "…", "SIL": "…" }
}
```

* `F-UNKNOWN`：origin 被改指到**不存在的本地路径**（模拟 fetch 不可达），`ttl_expired: true`；
* `F-REMOTE-GONE`：直接删除本地 bare origin（模拟 Gitea 端删仓），`remote_repo_absent: true`；
* `F-PARTIAL`：`workspaces/<component>/.mbd-fixture/job.json` 记录 2/4 失败的 clone job 场景，
  `job_state: "partial_failed"`（本夹具**不真实执行**批量操作）；
* 其余 flag（`dirty_tracked/untracked/stashed/shallow/lfs_missing/busy/tag_moved`）均来自真实 git 事实。

---

## 5. 如何接入测试（manifest 驱动表驱动）

推荐把 `expected.json` 作为**测试数据源**（稳定、与脚本一致性由生成器门禁保证），把运行期
`manifest.json` 作为**夹具路径与自校验证据源**：

1. CI/本地先跑生成器（一次约 5–6 分钟）：`pwsh -NoProfile -File state-matrix.ps1 -Root <workspace>`；
   断言退出码为 `0`，或允许 `skipped` 仅来自 LFS。
2. 读 `<workspace>/manifest.json`：按 `fixture_id` 建立索引，记录 `repo_path`（组件目录）与
   四仓 `path`；`fixtures[].status != "passed"` 直接判测试环境错误（不要在被测逻辑里掩盖）。
3. 表驱动用例（xUnit/NUnit `TestCaseSource`、pytest parametrize、Playwright 均可）：
   对 `expected.json.fixtures` 的每一项：
   * 用 `<Root>/<fixture_id>/workspaces/<component>` 作为扫描根，调用被测程序的扫描/聚合入口；
   * 注入 `metadata.gitea_simulation`（`remote_repo_absent` / `ttl_expired` / `job_state`）到
     远程元数据层、`IClock` 与 job journal；
   * 断言四仓主状态 = `expected_repos[RepoType]`，`NO_UPSTREAM` 另断言子型 = `expected_sub_type`；
   * 断言组件聚合 = `expected_component_aggregate`；断言 `expected_flags ⊆ 实际 flags`；
   * 对 `expected_primary_state == null` 的 fixture（`F-PARTIAL`）只断言组件覆盖态。
4. 反向校验：`manifest.fixtures[].self_check.ok` 必须全为 true，否则说明夹具本身被环境破坏
   （例如外部 git 改动了临时目录），应重建夹具而不是放宽断言。
5. 不要硬编码 `<Root>`：默认根目录带时间戳；测试内统一从 `manifest.root` 解析。

最小伪代码：

```csharp
var manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(Path.Combine(root, "manifest.json")));
var expected = JsonSerializer.Deserialize<ExpectedDoc>(File.ReadAllText("reqs/v0.2/fixtures/expected.json"))!;
foreach (var (id, exp) in expected.Fixtures)
{
    var fx = manifest.Fixtures.Single(f => f.FixtureId == id);
    var result = scanner.ScanComponent(fx.RepoPath, Simulate(fx.Metadata.GiteaSimulation));
    Assert.Equal(exp.ExpectedRepos, result.Repos.ToDictionary(r => r.Type, r => r.PrimaryState));
    Assert.Equal(exp.ExpectedComponentAggregate, result.ComponentState);
    Assert.Subset(result.Flags, exp.ExpectedFlags);
}
```

建议覆盖用例：14 主状态各 ≥1（本清单全覆盖）、15 聚合态各 ≥1、**聚合顺序锁定**（`F-MISSING+BLOCKED`
断言组件 = `MISSING`，C-03）、7.6 状态×操作矩阵抽样（用 `F-BUSY` 断言写操作返回 `OP-E001`、
`F-UNMERGED`/`F-IN_PROGRESS` 断言禁止自动 SYNC 等）。

---

## 6. 夹具清单与期望值（与附录 C / SRS 对照）

| fixture_id | 期望 primary | 期望 aggregate | 关键 flag | 构造要点 |
|---|---|---|---|---|
| F-SYNCED | SYNCED | SYNCED | — | main + upstream，HEAD==upstream，工作区干净 |
| F-BEHIND | BEHIND | BEHIND | — | origin 领先 2 个 commit |
| F-AHEAD | AHEAD | AHEAD | — | 本地领先 2 个未推送 commit |
| F-DIVERGED | DIVERGED | DIVERGED | — | 双方各 1 个新 commit |
| F-DIRTY | DIRTY | DIRTY | `dirty_tracked` | HEAD==upstream，README.md 有改动 |
| F-NO_UPSTREAM-LOCAL | NO_UPSTREAM(LOCAL_ONLY) | NEW_BRANCH | — | 本地 feature-local，origin 无同名 |
| F-NO_UPSTREAM-UNSET | NO_UPSTREAM(UNSET_BUT_EXISTS) | NEW_BRANCH | — | origin 有 feature-unset，未设 tracking |
| F-NO_UPSTREAM-GONE | NO_UPSTREAM(UPSTREAM_GONE) | NEW_BRANCH | — | 远端删分支 + fetch --prune，配置仍在 |
| F-DETACHED-TAG | DETACHED_TAG | DETACHED | — | annotated tag v1.0 + `switch --detach v1.0` |
| F-DETACHED-COMMIT | DETACHED_COMMIT | DETACHED | — | 无 tag 的 detached HEAD |
| F-UNMERGED | UNMERGED | BLOCKED_LOCAL | — | `read-tree -m` 造 index unmerged，无 MERGE_HEAD |
| F-IN_PROGRESS | IN_PROGRESS | BLOCKED_LOCAL | — | `merge --no-commit --no-ff` 留 MERGE_HEAD |
| F-EMPTY | EMPTY | EMPTY | — | `git init` 无首个 commit（unborn） |
| F-INVALID-NOTREPO | INVALID | BINDING_ERROR | — | 目录存在但非 git 仓 |
| F-INVALID-REMOTE | INVALID | BINDING_ERROR | — | origin URL 与期望仓不一致 |
| F-MISSING | MISSING | MISSING | — | 本地 Design 目录不存在 |
| F-MISSING+BLOCKED | MISSING | MISSING | — | Design 缺失 + Impl index 冲突（UNMERGED）；**顺序锁定：MISSING(3) 先于 BLOCKED_LOCAL(4)** |
| F-UNKNOWN | UNKNOWN | UNKNOWN | `stale` | origin 指向不存在路径 + TTL 过期模拟 |
| F-DETACHED+MIXED | DETACHED_TAG | DETACHED | `mixed_branch` | 2 仓 tag 检出、2 仓 main/feature-x |
| F-TAG_DRIFT | DETACHED_TAG | TAG_DRIFT | — | 3/4 仓有 tag v1.0，另 1 仓无 tag |
| F-PARTIAL | null（组件覆盖态） | OP_PARTIAL_FAILED | — | job journal 2/4 失败，Impl/SIL 缺失 |
| F-LFS-MISSING | SYNCED | SYNCED | `lfs_missing` | LFS 对象删除、工作区 pointer，status 干净 |
| F-BUSY | SYNCED | SYNCED | `busy` | `<git-dir>/index.lock` 存在 |
| F-REMOTE-GONE | UNKNOWN | UNKNOWN | `remote_gone` | origin bare 仓被删除（Gitea 删仓模拟） |
| F-TAG-MOVED | SYNCED | TAG_DRIFT | `tag_moved` | 本地 tag 指旧 commit、远端同名 tag 被移动 |

聚合态覆盖：`OP_PARTIAL_FAILED←F-PARTIAL`；`BINDING_ERROR←F-INVALID-*`；`UNKNOWN←F-UNKNOWN/F-REMOTE-GONE`；
`BLOCKED_LOCAL←F-UNMERGED/F-IN_PROGRESS`；`MISSING←F-MISSING/F-MISSING+BLOCKED`；`EMPTY←F-EMPTY`；
`TAG_DRIFT←F-TAG_DRIFT/F-TAG-MOVED`；`DIVERGED/BEHIND/AHEAD/DIRTY/SYNCED←同名 fixture`；
`DETACHED←F-DETACHED-*`；`MIXED_BRANCH←F-DETACHED+MIXED`（其更高优先级 DETACHED 胜出，flag 仍为 mixed_branch）；
`NEW_BRANCH←F-NO_UPSTREAM-*`。15 个聚合态全部有 fixture 覆盖（`MIXED_BRANCH` 以 flag+候选形式出现）。
**聚合优先级顺序以 SRS v0.2.1 §7.4 为准**（`MISSING=3` / `BLOCKED_LOCAL=4`）；`F-MISSING+BLOCKED` 是顺序锁定夹具
（两候选并存时组件必须为 `MISSING`，`manifest.self_check.aggregate_candidates` 应含 `BLOCKED_LOCAL,MISSING,SYNCED`）。

---

## 7. 已知限制

1. **Gitea/DB 侧事实是离线模拟**：`remote_repo_absent`、`ttl_expired`、`job_state` 由
   `fixture-meta.json` / manifest `metadata` 提供，测试需注入到远程元数据层、`IClock` 与 job journal；
   本生成器只保证本地 git 事实真实。
2. **F-UNKNOWN 的 origin URL 故意不可达**：origin 指向不存在的本地路径以模拟 fetch 失败；
   该 fixture 的 `expected_origin_url` 就是这条不可达路径（绑定比较仍会通过），
   与 `F-INVALID-REMOTE`（URL 与期望仓不一致）语义不同，勿混用。
3. **TAG_DRIFT 判定口径**：SRS 7.4 只写“四仓 tag 不一致 / 缺 tag / 与快照不符”，未给算法。
   本生成器按“四仓**本地 tag 名集合**不一致，或本地/远端同名 tag 指向不同 commit（`tag_moved`）”
   计算；`F-DETACHED+MIXED` 特意让四仓都带 v1.0 以满足附录 C 期望的 `DETACHED`。
4. **BINDING_ERROR 的发现层输入**：本生成器固定“组件 = 4 仓”，未构造“仓数≠4 / 后缀解析歧义 /
   嵌套仓库 / 大小写冲突 / reparse point”的发现层夹具；这些场景需另建工作区级夹具（附录 C 未逐行列）。
5. **F-BUSY 前提**：主状态 SYNCED 依赖被测实现按 SRS 7.5 L2 使用 `--no-optional-locks` 只读扫描；
   若被测实现读取时抢占 `index.lock`，可能得到 UNKNOWN。写操作应返回 `OP-E001`（BUSY）。
6. **未单独建 fixture 的 flag 组合**：`untracked`、`stashed`、`shallow`、`reparse_point` 以及
   长路径/中文路径/大小写冲突等附录 C 提到的组合未单独构造（当前仅作为 flag 出现在实现说明中）；
   需要时按 SRS 13.2 扩展本清单。
7. **不做 SHA 级 golden 比对**：提交时间戳/对象 SHA 每次运行不同，测试只断言状态、子型与 flag；
   需要固定图结构时可另冻结 `git replace`/固定日期的数据集。
8. **性能**：全量 25 fixture 约 5–6 分钟（4 仓 × 数十条 git 命令 + LFS 推送）；CI 建议缓存产物或只跑
   `-Only` 子集（`-Only` 仍会重新构造所选 fixture）。
9. **聚合全序与 severity_rank**：聚合优先级以 **SRS v0.2.1 §7.4** 为准（`MISSING=3` 先于 `BLOCKED_LOCAL=4`，
   见 `F-MISSING+BLOCKED`）；`severity_rank` 是独立的看板排序维度（MISSING=12 中性、BLOCKED_LOCAL=3），
   本生成器只校验主状态/聚合态/flag，不校验看板排序。修改 `$script:AggregatePriority` 必须同步
   `expected.json` 与图集 D3-c/D3-d（C-03）。

---

## 8. 验证记录（真实运行，2026-11-14）

环境：Windows + `pwsh 7.6.7` + `git version 2.56.1.windows.1` + `git-lfs/3.8.1`（LFS 可用，
`F-LFS-MISSING` 未被跳过）。

### 8.1 本次最小验证（v0.2.1 修订后，2026-11-14）

| # | 命令 | 结果 |
|---|---|---|
| 1 | `pwsh -NoProfile -File .\state-matrix.ps1 -Root "$env:TEMP\MbdGitFixtures\v021-check" -Only F-SYNCED` | **exit `0`**；`expected.json` 一致性门禁通过（**25 个 fixture**）；F-SYNCED 自校验通过（primary=SYNCED / aggregate=SYNCED）；fixture 1 / 通过 1 / 失败 0 / 跳过 0；耗时 11.78s |
| 2 | `pwsh -NoProfile -File .\state-matrix.ps1 -Root "$env:TEMP\MbdGitFixtures\v021-check" -Only 'F-MISSING+BLOCKED'` | **exit `0`**；F-MISSING+BLOCKED 自校验通过（primary=MISSING / aggregate=MISSING）；四仓 derived = Design=MISSING / Impl=UNMERGED / MIL=SYNCED / SIL=SYNCED；`aggregate_candidates = BLOCKED_LOCAL,MISSING,SYNCED`；`mismatches=0`；耗时 12.18s |
| 3 | `pwsh -NoProfile -File .\state-matrix.ps1 -Root "$env:TEMP\MbdGitFixtures\v021-check" -Clean` | **exit `0`**；`[ OK ] 已删除 …\v021-check`；清理后目录不存在（`DIR_EXISTS=False`） |

> 结论：C-03 修订后聚合优先级与 SRS v0.2.1 一致（`MISSING` 3 先于 `BLOCKED_LOCAL` 4），
> 新增顺序锁定夹具可复现通过；脚本启动即校验 `expected.json ↔ 脚本内定义`（25/25 一致）。
> 复跑时 `manifest.generator.srs = "reqs/requirements.md v0.2.1 §7.1–7.6 + §8.1 + 附录 C/附录 D（C-03 聚合顺序按 §7.4）"`
> （manifest 实测见 `v021-final` 目录，已 `-Clean`）。

### 8.2 历史全量验证（v0.2 生成器，24 fixture，保留供核对）

| # | 命令 | 结果 |
|---|---|---|
| 1 | `pwsh -NoProfile -File .\state-matrix.ps1` | exit `0`；fixture 24，通过 24，构造失败 0，自校验失败 0，跳过 0；`summary.elapsed_seconds = 341.64`（wall 342.2s） |
| 2 | `pwsh -NoProfile -File .\state-matrix.ps1 -Root "$env:TEMP\MbdGitFixtures\only-check" -Only F-SYNCED` | exit `0`；fixture 1，通过 1，失败 0，跳过 0；耗时 12.69s |
| 3 | `pwsh -NoProfile -File .\state-matrix.ps1 -Root "$env:TEMP\MbdGitFixtures\only-check" -Clean` | exit `0`；`[ OK ] 已删除 …\only-check`；清理后目录不存在 |

全量运行的 manifest 实测路径（v0.2 历史记录，供核对）：

```
C:\Users\Trevn\AppData\Local\Temp\MbdGitFixtures\run-20261114-002804\manifest.json
```

该次 24 个 fixture（v0.2 口径）的 `status` 全部为 `passed`，每个 fixture 的四仓 `repos[].self_check.ok`
均为 true，`self_check.mismatches` 均为空；`expected.json` 一致性门禁通过（24/24）。无环境缺失导致的 skip。
**注意**：该历史 manifest 的聚合顺序是 v0.2 旧口径（`BLOCKED_LOCAL=3 / MISSING=4`），已被本次 v0.2.1
修订取代，不得作为验收基线；请以 §8.1 的 v0.2.1 最小验证与重新生成的全量 manifest 为准。

---

## 9. v0.2.1 复评已关闭项（对应 07 复评 C/H）与剩余限制

### 9.1 已关闭项（07 复评 → 本目录处置）

| 07 条目 | 原问题（v0.2） | v0.2.1 处置 |
|---|---|---|
| **C-03** 聚合顺序 | `state-matrix.ps1` 曾按 `BLOCKED_LOCAL=3 / MISSING=4`，与 SRS §7.4 相反 | 改为 **`MISSING=3 / BLOCKED_LOCAL=4`**（SRS v0.2.1 §7.4）；新增 `F-MISSING+BLOCKED` 组合夹具锁定顺序；`expected.json` 同步 |
| **M-07/M-15** 期望值 | `expected.json.source` 写 v0.1.1；F-PARTIAL 的 null 语义未说明 | `source` 改 **`SRS v0.2.1`**；F-PARTIAL 主状态 null 语义在 notes/README §5 已明确（只断言组件覆盖态） |
| **M-08** README §9 | 5 条"SRS 不一致"中多条已被 SRS v0.2 修复，旧 §9.4 对 §7.4 顺序的描述与 SRS 相反 | 本节重写为"已关闭项 + 剩余限制"；聚合顺序明确**以 SRS v0.2.1 为准** |
| **M-10**（图集侧联动） | 旧 §9 把 `notification` 空表、12.2 Host↔git.exe 等过时项登记为不一致 | 与本目录无关（SRS v0.2 已修）；夹具侧无对应依赖 |

原 §9 的五条历史不一致，逐条状态：① **NO_UPSTREAM 子型命名**——SRS v0.2/附录 C 已统一为 `LOCAL_ONLY/UNSET_BUT_EXISTS/UPSTREAM_GONE`，本清单沿用 §7.2 全名（**已关闭**）；② **F-DETACHED+MIXED 的 primary**——单仓记 `DETACHED_TAG`、组件聚合记 `DETACHED`，与 §7.2/§7.4 一致（**口径已验证，保留说明**）；③ **flag 命名与 REMOTE_GONE 聚合**——`tag_present` 只作 `baseline_snapshot` 列、`tag_mismatch` 用 `tag_moved`、`REMOTE_GONE` 非聚合态而按 §7.4 兜底 UNKNOWN（**按 SRS 取齐，已关闭**）；④ **聚合优先级顺序**——SRS v0.2.1 已明确 `MISSING=3` 先于 `BLOCKED_LOCAL=4` 并注明与评审 02 表 C 对齐（**C-03 已关闭**）；⑤ **F-LFS-MISSING 聚合期望**——SRS §7.4 无 LFS 聚合态，按主状态 SYNCED + `lfs_missing` flag；TAG_DRIFT 按"四仓 tag 名集合不一致或 tag_moved"判定（**保留为实现口径，见 §7.3**）。

### 9.2 剩余限制

1. **发现层夹具未构造**：仓数≠4、后缀解析歧义、嵌套仓库、大小写冲突、reparse point 等场景需另建工作区级夹具（附录 C 未逐行列）；本生成器固定"组件 = 4 仓"。
2. **flag 组合未单独建 fixture**：`untracked`、`stashed`、`shallow`、`reparse_point` 及长路径/中文路径/大小写冲突仍只作为实现说明，需要时按 SRS §13.2 扩展。
3. **TAG_DRIFT 判定口径**：SRS 7.4 未给算法；本生成器按"四仓本地 tag 名集合不一致，或本地/远端同名 tag 指向不同 commit（`tag_moved`）"计算，F-DETACHED+MIXED 特意四仓都带 v1.0 以符合附录 C 的 `DETACHED` 期望。
4. **Gitea/DB 侧事实是离线模拟**：`remote_repo_absent`、`ttl_expired`、`job_state` 由 `fixture-meta.json`/manifest `metadata` 提供，测试需注入远程元数据层、`IClock` 与 job journal；生成器只保证本地 git 事实真实。
5. **F-UNKNOWN 的 origin URL 故意不可达**，与 F-INVALID-REMOTE（URL 与期望仓不一致）语义不同，勿混用。
6. **不做 SHA 级 golden 比对**：提交时间戳/对象 SHA 每次运行不同，只断言状态、子型与 flag。
7. **性能**：全量 25 fixture 约 5–6 分钟；CI 建议缓存或只跑 `-Only` 子集。
8. **`severity_rank` 不在夹具断言范围**：夹具只校验主状态/聚合态/flag；看板排序（SRS §7.4 severity_rank）由 AC-BOARD-01 覆盖。

> 上述限制只影响"夹具覆盖广度/断言边界"，不改变已冻结的 15 聚合态与 14 主状态语义；
> 任何新增夹具应先在 `expected.json` 登记再跑 `-Only` 验证。

> 上述 1–5 只影响夹具的“命名/期望值选取”，不改变夹具构造本身；建议在 M0 冻结时同步修订附录 C 与 §7.4。
