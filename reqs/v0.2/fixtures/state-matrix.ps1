#!/usr/bin/env pwsh
#Requires -Version 7.0
<#
.SYNOPSIS
    MBD 组件全生命周期 Git 工具 —— 本地 Git 状态矩阵夹具生成器（v0.2.1）。

.DESCRIPTION
    依据 SRS v0.2.1 第 7.1–7.6 节、附录 C 状态矩阵夹具清单，以及
    reqs/review/02-git-domain.md 修正后的状态机，在本地临时目录构造 25 个可复现的
    4 仓组件夹具（Design/Impl/MIL/SIL），并逐个用 git 只读命令做自校验。

    安全约束（硬性）：
      * 全部读写只发生在 -Root 指定目录（默认 $env:TEMP\MbdGitFixtures\<时间戳>）内；
      * 绝不修改真实用户仓库 / 用户 git 全局配置 / 系统目录；
      * 不联网：所有 origin 都是本地 `git init --bare` 的 bare 仓库（file 路径）；
      * 幂等：同名 fixture 目录先删除再重建；-Clean 只删除带标记文件的本脚本产物。

    每个 fixture 生成后写入 manifest.json（全局一份 + 每个 fixture 一份）：
      fixture_id / repo_path / expected_primary_state / expected_sub_type /
      expected_flags / expected_component_aggregate / expected_repos / notes /
      metadata / repos[] / self_check。

    退出码：
      0 = 全部通过（或仅 LFS 被显式跳过）
      2 = 前置条件不满足（git 缺失 / git < 2.40）
      3 = 至少一个 fixture 构造失败
      4 = 至少一个 fixture 自校验结果与期望不一致（含 expected.json 失配）
      5 = 参数 / 目录安全校验失败
      6 = -Clean 失败

.PARAMETER Root
    输出根目录。默认 $env:TEMP\MbdGitFixtures\run-<yyyyMMdd-HHmmss>。
    若显式指定且目录已存在，必须为空或带本脚本的 .mbd-fixtures-root.json 标记。

.PARAMETER Component
    组件 ID（默认 CF325F_ChanganSigProcgBascFct），决定四个仓库目录名：
    <Component>_Design / _Impl / _MIL / _SIL。

.PARAMETER Only
    按 fixture_id 过滤（支持通配符，可逗号分隔或多次指定），例如：
      -Only F-SYNCED,F-TAG*
      -Only F-DETACHED+MIXED

.PARAMETER Clean
    只清理、不生成。带 -Root 时清理该 Root（必须带标记文件）；不带 -Root 时
    清理 $env:TEMP\MbdGitFixtures 下所有带标记的 run-* 目录。

.PARAMETER SkipLfs
    显式跳过 F-LFS-MISSING（manifest 记 skipped 与原因）。环境无 git-lfs 时也会
    自动跳过并记录原因。

.EXAMPLE
    pwsh -NoProfile -File .\state-matrix.ps1
    pwsh -NoProfile -File .\state-matrix.ps1 -Root D:\Temp\mbd-fixtures -Only F-TAG*
    pwsh -NoProfile -File .\state-matrix.ps1 -Clean
#>
[CmdletBinding()]
param(
    [string]$Root,
    [string]$Component = 'CF325F_ChanganSigProcgBascFct',
    [string[]]$Only,
    [switch]$Clean,
    [switch]$SkipLfs
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

# ============================================================================
# 0. 常量、状态枚举与退出码
# ============================================================================

$script:ScriptVersion = '1.1.0'
$script:ExitCodeOk = 0
$script:ExitCodePrereq = 2
$script:ExitCodeBuild = 3
$script:ExitCodeSelfCheck = 4
$script:ExitCodeParams = 5
$script:ExitCodeClean = 6

$script:RepoTypes = @('Design', 'Impl', 'MIL', 'SIL')
$script:BaseTempDir = Join-Path $env:TEMP 'MbdGitFixtures'

# SRS 7.2 单仓主状态优先级（0 最高）；数组顺序即优先级顺序。
$script:PrimaryPriority = [ordered]@{
    UNKNOWN         = 0
    INVALID         = 1
    MISSING         = 2
    EMPTY           = 3
    UNMERGED        = 4
    IN_PROGRESS     = 5
    DETACHED_TAG    = 6
    DETACHED_COMMIT = 7
    DIVERGED        = 8
    BEHIND          = 9
    AHEAD           = 10
    NO_UPSTREAM     = 11
    DIRTY           = 12
    SYNCED          = 13
}

# SRS 7.4 组件聚合态优先级（0 最高），共 15 个。
# 顺序以 SRS v0.2.1 §7.4 为准（C-03）：MISSING=3 先于 BLOCKED_LOCAL=4；
# severity_rank 是独立维度（SRS §7.4 第 6 列），不参与本表排序。
$script:AggregatePriority = [ordered]@{
    OP_PARTIAL_FAILED = 0
    BINDING_ERROR     = 1
    UNKNOWN           = 2
    MISSING           = 3
    BLOCKED_LOCAL     = 4
    EMPTY             = 5
    TAG_DRIFT         = 6
    DIVERGED          = 7
    BEHIND            = 8
    AHEAD             = 9
    DETACHED          = 10
    MIXED_BRANCH      = 11
    NEW_BRANCH        = 12
    DIRTY             = 13
    SYNCED            = 14
}

# 单仓主状态 → 组件聚合候选（SRS 7.4「映射完整性」）。
$script:RepoToComponentMap = @{
    UNKNOWN         = 'UNKNOWN'
    INVALID         = 'BINDING_ERROR'
    MISSING         = 'MISSING'
    EMPTY           = 'EMPTY'
    UNMERGED        = 'BLOCKED_LOCAL'
    IN_PROGRESS     = 'BLOCKED_LOCAL'
    DETACHED_TAG    = 'DETACHED'
    DETACHED_COMMIT = 'DETACHED'
    DIVERGED        = 'DIVERGED'
    BEHIND          = 'BEHIND'
    AHEAD           = 'AHEAD'
    NO_UPSTREAM     = 'NEW_BRANCH'
    DIRTY           = 'DIRTY'
    SYNCED          = 'SYNCED'
}

# 自校验时执行的标准 git 只读命令清单（写入 manifest.self_check.commands）。
$script:StandardCheckCommands = @(
    'git --no-optional-locks -C <repo> status --porcelain=v2 --branch',
    'git --no-optional-locks -C <repo> rev-parse --is-shallow-repository',
    'git -C <repo> ls-files -u',
    'Test-Path <git-dir>/MERGE_HEAD | rebase-merge | rebase-apply | CHERRY_PICK_HEAD | BISECT_LOG',
    'git -C <repo> symbolic-ref -q --short HEAD',
    'git -C <repo> tag --points-at HEAD',
    'git -C <repo> rev-parse --symbolic-full-name @{upstream}',
    'git -C <repo> rev-parse --verify --quiet <compare_ref>',
    'git -C <repo> merge-base --is-ancestor HEAD <compare_ref>（及反向）',
    'git -C <repo> rev-list --left-right --count HEAD...<compare_ref>',
    'git --no-optional-locks -C <repo> diff-index --quiet HEAD --',
    'git --no-optional-locks -C <repo> ls-files --others --exclude-standard',
    'git -C <repo> stash list',
    'git --no-optional-locks -C <repo> ls-remote --quiet --heads origin',
    'git -C <repo> for-each-ref --format=%(refname:short) refs/tags + rev-parse refs/tags/<t>^{commit}'
)

# ============================================================================
# 1. 工具函数
# ============================================================================

function Write-Utf8NoBom {
    param([string]$Path, [string]$Text)
    $dir = Split-Path -Parent $Path
    if ($dir -and -not (Test-Path -LiteralPath $dir)) {
        New-Item -ItemType Directory -Force -Path $dir | Out-Null
    }
    [System.IO.File]::WriteAllText($Path, $Text, [System.Text.UTF8Encoding]::new($false))
}

function Write-Info {
    param([string]$Message)
    Write-Host "[INFO ] $Message" -ForegroundColor Cyan
}

function Write-Ok {
    param([string]$Message)
    Write-Host "[ OK  ] $Message" -ForegroundColor Green
}

function Write-Warn2 {
    param([string]$Message)
    Write-Host "[WARN ] $Message" -ForegroundColor Yellow
}

function Write-Fail {
    param([string]$Message)
    Write-Host "[FAIL ] $Message" -ForegroundColor Red
}

# 统一 git 调用：成功返回 @{ ExitCode; Output }，失败抛异常（带命令与输出）。
function Invoke-GitSuccess {
    param(
        [string]$WorkDir,
        [string[]]$Arguments = @(),
        [int[]]$AllowedExitCodes = @(0),
        [string]$What = 'git 命令'
    )
    $gitArgs = @()
    if ($WorkDir) { $gitArgs += @('-C', $WorkDir) }
    $gitArgs += $Arguments
    $output = & git @gitArgs 2>&1
    $code = $LASTEXITCODE
    if ($code -notin $AllowedExitCodes) {
        throw ("{0} 失败：git {1} (exit {2})`n{3}" -f $What, ($Arguments -join ' '), $code, (@($output | ForEach-Object { "$_" }) -join "`n"))
    }
    return [pscustomobject]@{
        ExitCode = $code
        Output   = @($output | ForEach-Object { "$_" })
    }
}

# 自校验用只读 git 调用：默认加 --no-optional-locks，允许非 0 退出码并原样返回。
function Invoke-GitProbe {
    param(
        [string]$WorkDir,
        [string[]]$Arguments = @()
    )
    $gitArgs = @('--no-optional-locks')
    if ($WorkDir) { $gitArgs += @('-C', $WorkDir) }
    $gitArgs += $Arguments
    $output = & git @gitArgs 2>&1
    $code = $LASTEXITCODE
    return [pscustomobject]@{
        ExitCode = $code
        Output   = @($output | ForEach-Object { "$_" })
    }
}

function Set-LocalIdentity {
    param([string]$Path)
    Invoke-GitSuccess -WorkDir $Path -Arguments @('config', 'user.name', 'MBD Git Fixture') | Out-Null
    Invoke-GitSuccess -WorkDir $Path -Arguments @('config', 'user.email', 'fixture@example.invalid') | Out-Null
    Invoke-GitSuccess -WorkDir $Path -Arguments @('config', 'commit.gpgsign', 'false') | Out-Null
    Invoke-GitSuccess -WorkDir $Path -Arguments @('config', 'tag.gpgsign', 'false') | Out-Null
}

function Normalize-Url {
    param([string]$Url)
    if (-not $Url) { return '' }
    return (($Url -replace '\\', '/').TrimEnd('/'))
}

function Normalize-PathForCompare {
    param([string]$Path)
    if (-not $Path) { return '' }
    return (($Path -replace '/', '\').TrimEnd('\'))
}

function Test-UrlEqual {
    param([string]$A, [string]$B)
    return [string]::Equals((Normalize-Url $A), (Normalize-Url $B), [System.StringComparison]::OrdinalIgnoreCase)
}

function Add-CommitFile {
    param(
        [string]$RepoPath,
        [string]$FileName,
        [string]$Content,
        [string]$Message
    )
    $file = Join-Path $RepoPath $FileName
    $dir = Split-Path -Parent $file
    if ($dir -and -not (Test-Path -LiteralPath $dir)) {
        New-Item -ItemType Directory -Force -Path $dir | Out-Null
    }
    if (-not $Content) { $Content = [guid]::NewGuid().ToString() }
    Write-Utf8NoBom -Path $file -Text ($Content + "`n")
    Invoke-GitSuccess -WorkDir $RepoPath -Arguments @('add', '-A') | Out-Null
    Invoke-GitSuccess -WorkDir $RepoPath -Arguments @('commit', '-q', '-m', $Message) | Out-Null
}

function New-RepoRecord {
    param(
        [string]$RepoType,
        [string]$Path,
        [string]$Origin,
        [string]$ExpectedOriginUrl
    )
    return [ordered]@{
        repo_type          = $RepoType
        name               = (Split-Path -Leaf $Path)
        path               = $Path
        origin             = $Origin
        expected_origin_url = $ExpectedOriginUrl
    }
}

function Add-Flag {
    param($List, [string]$Flag)
    if (-not $List.Contains($Flag)) { [void]$List.Add($Flag) }
}

# ============================================================================
# 2. 附录 C 夹具定义（期望值静态源；expected.json 必须与之一致）
# ============================================================================

function New-AllRepos {
    param([string]$State)
    $r = [ordered]@{}
    foreach ($t in $script:RepoTypes) { $r[$t] = $State }
    return $r
}

$script:FixtureDefs = @(
    [ordered]@{
        id = 'F-SYNCED'; primary = 'SYNCED'; sub = $null; flags = @(); aggregate = 'SYNCED'; repos = (New-AllRepos 'SYNCED')
        notes = '四仓均在 main、upstream=origin/main 且 HEAD==upstream，工作区干净。'
    }
    [ordered]@{
        id = 'F-BEHIND'; primary = 'BEHIND'; sub = $null; flags = @(); aggregate = 'BEHIND'; repos = (New-AllRepos 'BEHIND')
        notes = 'origin 侧领先本地 2 个 commit（本地 fetch 后 origin/main 前进 2）。'
    }
    [ordered]@{
        id = 'F-AHEAD'; primary = 'AHEAD'; sub = $null; flags = @(); aggregate = 'AHEAD'; repos = (New-AllRepos 'AHEAD')
        notes = '本地领先 origin/main 2 个未推送 commit。'
    }
    [ordered]@{
        id = 'F-DIVERGED'; primary = 'DIVERGED'; sub = $null; flags = @(); aggregate = 'DIVERGED'; repos = (New-AllRepos 'DIVERGED')
        notes = '本地与 origin 各有 1 个新 commit，互为非祖先。'
    }
    [ordered]@{
        id = 'F-DIRTY'; primary = 'DIRTY'; sub = $null; flags = @('dirty_tracked'); aggregate = 'DIRTY'; repos = (New-AllRepos 'DIRTY')
        notes = 'HEAD==upstream，但 tracked 文件 README.md 有未提交改动。'
    }
    [ordered]@{
        id = 'F-NO_UPSTREAM-LOCAL'; primary = 'NO_UPSTREAM'; sub = 'LOCAL_ONLY'; flags = @(); aggregate = 'NEW_BRANCH'; repos = (New-AllRepos 'NO_UPSTREAM')
        notes = '本地新建 feature-local 分支，origin 无同名分支（LOCAL_ONLY）。'
    }
    [ordered]@{
        id = 'F-NO_UPSTREAM-UNSET'; primary = 'NO_UPSTREAM'; sub = 'UNSET_BUT_EXISTS'; flags = @(); aggregate = 'NEW_BRANCH'; repos = (New-AllRepos 'NO_UPSTREAM')
        notes = 'origin 有同名 feature-unset 分支，但本地未配置 tracking（UNSET_BUT_EXISTS）。附录 C 写作 UNSET，按 7.2 统一为 UNSET_BUT_EXISTS。'
    }
    [ordered]@{
        id = 'F-NO_UPSTREAM-GONE'; primary = 'NO_UPSTREAM'; sub = 'UPSTREAM_GONE'; flags = @(); aggregate = 'NEW_BRANCH'; repos = (New-AllRepos 'NO_UPSTREAM')
        notes = '本地 feature-gone 配置了 upstream，远端分支被删除且已 fetch --prune（UPSTREAM_GONE）。附录 C 写作 GONE，按 7.2 统一为 UPSTREAM_GONE。'
    }
    [ordered]@{
        id = 'F-DETACHED-TAG'; primary = 'DETACHED_TAG'; sub = $null; flags = @(); aggregate = 'DETACHED'; repos = (New-AllRepos 'DETACHED_TAG')
        notes = '四仓均有 annotated tag v1.0 指向 HEAD，并 switch --detach v1.0。'
    }
    [ordered]@{
        id = 'F-DETACHED-COMMIT'; primary = 'DETACHED_COMMIT'; sub = $null; flags = @(); aggregate = 'DETACHED'; repos = (New-AllRepos 'DETACHED_COMMIT')
        notes = '四仓 detached HEAD 且无 tag 指向该 commit。'
    }
    [ordered]@{
        id = 'F-UNMERGED'; primary = 'UNMERGED'; sub = $null; flags = @(); aggregate = 'BLOCKED_LOCAL'; repos = (New-AllRepos 'UNMERGED')
        notes = '用 git read-tree -m <base> <ours> <theirs> 构造 index 未解决冲突（git ls-files -u 非空），无 MERGE_HEAD。'
    }
    [ordered]@{
        id = 'F-IN_PROGRESS'; primary = 'IN_PROGRESS'; sub = $null; flags = @(); aggregate = 'BLOCKED_LOCAL'; repos = (New-AllRepos 'IN_PROGRESS')
        notes = 'git merge --no-commit --no-ff 留下 .git/MERGE_HEAD（合并未完成）。'
    }
    [ordered]@{
        id = 'F-EMPTY'; primary = 'EMPTY'; sub = $null; flags = @(); aggregate = 'EMPTY'; repos = (New-AllRepos 'EMPTY')
        notes = '四仓 git init 后无首个 commit（HEAD unborn），origin 为空 bare 仓。'
    }
    [ordered]@{
        id = 'F-INVALID-NOTREPO'; primary = 'INVALID'; sub = $null; flags = @(); aggregate = 'BINDING_ERROR'
        repos = [ordered]@{ Design = 'INVALID'; Impl = 'SYNCED'; MIL = 'SYNCED'; SIL = 'SYNCED' }
        notes = 'Design 目录存在且有普通文件，但不是 git 仓库（INVALID）。'
    }
    [ordered]@{
        id = 'F-INVALID-REMOTE'; primary = 'INVALID'; sub = $null; flags = @(); aggregate = 'BINDING_ERROR'
        repos = [ordered]@{ Design = 'INVALID'; Impl = 'SYNCED'; MIL = 'SYNCED'; SIL = 'SYNCED' }
        notes = 'Design 是 git 仓库，但 origin URL 与期望仓不一致（INVALID）。'
    }
    [ordered]@{
        id = 'F-MISSING'; primary = 'MISSING'; sub = $null; flags = @(); aggregate = 'MISSING'
        repos = [ordered]@{ Design = 'MISSING'; Impl = 'SYNCED'; MIL = 'SYNCED'; SIL = 'SYNCED' }
        notes = '本地 Design 目录不存在（组件目录下仅 3 仓），origin 侧仍有该仓。'
    }
    [ordered]@{
        id = 'F-MISSING+BLOCKED'; primary = 'MISSING'; sub = $null; flags = @(); aggregate = 'MISSING'
        repos = [ordered]@{ Design = 'MISSING'; Impl = 'UNMERGED'; MIL = 'SYNCED'; SIL = 'SYNCED' }
        notes = '聚合顺序锁定组合（SRS v0.2.1 §7.4，C-03）：Design 目录不存在（MISSING），Impl 有未解决 index 冲突（UNMERGED）。MISSING 聚合优先级 3 先于 BLOCKED_LOCAL 4 → 组件聚合必须为 MISSING（不是 BLOCKED_LOCAL）。'
    }
    [ordered]@{
        id = 'F-UNKNOWN'; primary = 'UNKNOWN'; sub = $null; flags = @('stale'); aggregate = 'UNKNOWN'; repos = (New-AllRepos 'UNKNOWN')
        notes = 'origin 指向不存在的本地路径（模拟 fetch 不可达）+ fixture 元数据标记 TTL 过期（stale）；关系类状态强制 UNKNOWN。'
    }
    [ordered]@{
        id = 'F-DETACHED+MIXED'; primary = 'DETACHED_TAG'; sub = $null; flags = @('mixed_branch'); aggregate = 'DETACHED'
        repos = [ordered]@{ Design = 'DETACHED_TAG'; Impl = 'DETACHED_TAG'; MIL = 'SYNCED'; SIL = 'SYNCED' }
        notes = 'Design/Impl 检出 tag v1.0；MIL 在 main、SIL 在 feature-x（四仓分支不一致 → mixed_branch），聚合取更高优先级 DETACHED。'
    }
    [ordered]@{
        id = 'F-TAG_DRIFT'; primary = 'DETACHED_TAG'; sub = $null; flags = @(); aggregate = 'TAG_DRIFT'
        repos = [ordered]@{ Design = 'DETACHED_TAG'; Impl = 'DETACHED_TAG'; MIL = 'DETACHED_TAG'; SIL = 'SYNCED' }
        notes = '仅 Design/Impl/MIL 有 tag v1.0 并检出，SIL 无 tag → 四仓 tag 集合不一致 → 组件 TAG_DRIFT（覆盖 DETACHED）。附录 C 的关键 flag tag_present 在 SRS 7.3 中不存在，故 expected_flags 为空。'
    }
    [ordered]@{
        id = 'F-PARTIAL'; primary = $null; sub = $null; flags = @(); aggregate = 'OP_PARTIAL_FAILED'
        repos = [ordered]@{ Design = 'SYNCED'; Impl = 'MISSING'; MIL = 'SYNCED'; SIL = 'MISSING' }
        notes = '不真实执行批量操作：以 .mbd-fixture/job.json 记录 clone 2/4 失败（job 场景建议），Design/MIL 已克隆、Impl/SIL 缺失；组件覆盖态 OP_PARTIAL_FAILED 优先于 MISSING。'
    }
    [ordered]@{
        id = 'F-LFS-MISSING'; primary = 'SYNCED'; sub = $null; flags = @('lfs_missing'); aggregate = 'SYNCED'; repos = (New-AllRepos 'SYNCED')
        notes = '四仓含 LFS 模型文件，但本地 .git/lfs/objects 被删、工作区为 pointer；git status 干净 → 主状态 SYNCED + lfs_missing 标记。附录 C 聚合写作「DIRTY? 按 flag」，但 SRS 7.4 无 LFS 聚合态、且主状态为 SYNCED，故期望聚合按 7.4 取 SYNCED（见 README 已知限制）。'
    }
    [ordered]@{
        id = 'F-BUSY'; primary = 'SYNCED'; sub = $null; flags = @('busy'); aggregate = 'SYNCED'; repos = (New-AllRepos 'SYNCED')
        notes = '四仓均正常同步，但在 <git-dir>/index.lock 放了锁文件（外部 git 占用）。SRS 7.5 L2 要求用 --no-optional-locks 只读扫描，故主状态仍可得 SYNCED + busy 标记；写操作应返回 OP-E001。'
    }
    [ordered]@{
        id = 'F-REMOTE-GONE'; primary = 'UNKNOWN'; sub = $null; flags = @('remote_gone'); aggregate = 'UNKNOWN'; repos = (New-AllRepos 'UNKNOWN')
        notes = '本地仓完好，但 origin bare 仓已被删除（模拟 Gitea 端删仓），fixture 元数据标记 remote_repo_absent。fetch 失败 → 关系类状态 UNKNOWN + remote_gone 标记；附录 C 的「REMOTE_GONE 展示」在 SRS 7.4 中不是聚合态，故期望聚合按 7.4 兜底为 UNKNOWN。'
    }
    [ordered]@{
        id = 'F-TAG-MOVED'; primary = 'SYNCED'; sub = $null; flags = @('tag_moved'); aggregate = 'TAG_DRIFT'; repos = (New-AllRepos 'SYNCED')
        notes = '四仓 HEAD==origin/main（SYNCED），但本地 tag v1.0 指向旧 commit A、origin 端同名 tag 已被 force 移动到新 commit B → tag_moved 标记，组件 TAG_DRIFT。'
    }
)

$script:FixtureDefById = @{}
foreach ($def in $script:FixtureDefs) { $script:FixtureDefById[$def.id] = $def }

# ============================================================================
# 3. git 仓库构造辅助函数
# ============================================================================

function New-BareOrigin {
    param($Ctx, [string]$Name)
    $origin = Join-Path $Ctx.OriginsDir ($Name + '.git')
    New-Item -ItemType Directory -Force -Path $Ctx.OriginsDir | Out-Null
    Invoke-GitSuccess -Arguments @('init', '--bare', '-q', '-b', 'main', $origin) | Out-Null
    return $origin
}

function New-SeededOrigin {
    param($Ctx, [string]$Name)
    $origin = New-BareOrigin -Ctx $Ctx -Name $Name
    $seed = Join-Path $Ctx.TmpDir ('seed-' + $Name)
    if (Test-Path -LiteralPath $seed) { Remove-Item -Recurse -Force -LiteralPath $seed }
    New-Item -ItemType Directory -Force -Path $seed | Out-Null
    Invoke-GitSuccess -Arguments @('init', '-q', '-b', 'main', $seed) | Out-Null
    Set-LocalIdentity -Path $seed
    Write-Utf8NoBom -Path (Join-Path $seed 'README.md') -Text ("# " + $Name + "`n")
    Write-Utf8NoBom -Path (Join-Path $seed 'model.txt') -Text ("fixture repo: " + $Name + "`n")
    Invoke-GitSuccess -WorkDir $seed -Arguments @('add', '-A') | Out-Null
    Invoke-GitSuccess -WorkDir $seed -Arguments @('commit', '-q', '-m', ("init: " + $Name)) | Out-Null
    Invoke-GitSuccess -WorkDir $seed -Arguments @('remote', 'add', 'origin', $origin) | Out-Null
    Invoke-GitSuccess -WorkDir $seed -Arguments @('push', '-q', 'origin', 'main') | Out-Null
    return $origin
}

function New-RepoCloned {
    param($Ctx, [string]$RepoType, [string]$Origin)
    $name = '{0}_{1}' -f $Ctx.Component, $RepoType
    $path = Join-Path $Ctx.ComponentDir $name
    Invoke-GitSuccess -Arguments @('clone', '-q', $Origin, $path) | Out-Null
    Set-LocalIdentity -Path $path
    return New-RepoRecord -RepoType $RepoType -Path $path -Origin $Origin -ExpectedOriginUrl $Origin
}

function New-RepoEmpty {
    param($Ctx, [string]$RepoType)
    $name = '{0}_{1}' -f $Ctx.Component, $RepoType
    $origin = New-BareOrigin -Ctx $Ctx -Name $name
    $path = Join-Path $Ctx.ComponentDir $name
    # 克隆空 bare 仓：git 会警告 "empty repository"，但仍生成 unborn main 与 origin
    Invoke-GitSuccess -Arguments @('clone', '-q', $origin, $path) | Out-Null
    Set-LocalIdentity -Path $path
    return New-RepoRecord -RepoType $RepoType -Path $path -Origin $origin -ExpectedOriginUrl $origin
}

function New-MissingRepoRecord {
    param($Ctx, [string]$RepoType, [switch]$SeedOrigin)
    $name = '{0}_{1}' -f $Ctx.Component, $RepoType
    $origin = $null
    if ($SeedOrigin) { $origin = New-SeededOrigin -Ctx $Ctx -Name $name }
    else { $origin = New-BareOrigin -Ctx $Ctx -Name $name }
    $path = Join-Path $Ctx.ComponentDir $name
    return New-RepoRecord -RepoType $RepoType -Path $path -Origin $origin -ExpectedOriginUrl $origin
}

function New-SyncedComponent {
    param($Ctx)
    $repos = [ordered]@{}
    foreach ($t in $script:RepoTypes) {
        $name = '{0}_{1}' -f $Ctx.Component, $t
        $origin = New-SeededOrigin -Ctx $Ctx -Name $name
        $repos[$t] = New-RepoCloned -Ctx $Ctx -RepoType $t -Origin $origin
    }
    return $repos
}

function Add-RemoteCommits {
    param($Ctx, $Repo, [int]$Count = 1, [string]$FileName = 'remote.txt')
    $writer = Join-Path $Ctx.TmpDir ('writer-' + $Repo.name + '-' + [guid]::NewGuid().ToString('N').Substring(0, 6))
    Invoke-GitSuccess -Arguments @('clone', '-q', $Repo.origin, $writer) | Out-Null
    Set-LocalIdentity -Path $writer
    for ($i = 1; $i -le $Count; $i++) {
        Add-CommitFile -RepoPath $writer -FileName $FileName -Content ("remote commit {0} for {1}" -f $i, $Repo.name) -Message ("remote: {0} #{1}" -f $Repo.name, $i)
    }
    Invoke-GitSuccess -WorkDir $writer -Arguments @('push', '-q', 'origin', 'main') | Out-Null
    Remove-Item -Recurse -Force -LiteralPath $writer
    Invoke-GitSuccess -WorkDir $Repo.path -Arguments @('fetch', '-q', '--prune', 'origin') | Out-Null
}

function Add-LocalCommits {
    param($Repo, [int]$Count = 1, [switch]$Push)
    for ($i = 1; $i -le $Count; $i++) {
        Add-CommitFile -RepoPath $Repo.path -FileName 'local.txt' -Content ("local commit {0} for {1}" -f $i, $Repo.name) -Message ("local: {0} #{1}" -f $Repo.name, $i)
    }
    if ($Push) {
        Invoke-GitSuccess -WorkDir $Repo.path -Arguments @('push', '-q', 'origin', 'HEAD') | Out-Null
    }
}

function Set-DirtyTracked {
    param($Repo)
    $readme = Join-Path $Repo.path 'README.md'
    Add-Content -LiteralPath $readme -Value ("local uncommitted edit at " + (Get-Date).ToString('o')) -Encoding utf8
}

function New-LocalOnlyBranch {
    param($Repo)
    Invoke-GitSuccess -WorkDir $Repo.path -Arguments @('switch', '-q', '-c', 'feature-local') | Out-Null
}

function New-UnsetBranch {
    param($Repo)
    Invoke-GitSuccess -WorkDir $Repo.path -Arguments @('switch', '-q', '-c', 'feature-unset') | Out-Null
    Add-CommitFile -RepoPath $Repo.path -FileName 'feature-unset.txt' -Content ('feature-unset work for ' + $Repo.name) -Message 'feature-unset: local work'
    # 推送到 origin 但不建立 tracking（不带 -u）
    Invoke-GitSuccess -WorkDir $Repo.path -Arguments @('push', '-q', 'origin', 'feature-unset') | Out-Null
}

function New-GoneBranch {
    param($Repo)
    Invoke-GitSuccess -WorkDir $Repo.path -Arguments @('switch', '-q', '-c', 'feature-gone') | Out-Null
    Add-CommitFile -RepoPath $Repo.path -FileName 'feature-gone.txt' -Content ('feature-gone work for ' + $Repo.name) -Message 'feature-gone: local work'
    Invoke-GitSuccess -WorkDir $Repo.path -Arguments @('push', '-q', '-u', 'origin', 'feature-gone') | Out-Null
    # 在 bare origin 中删除该分支，并在本地 prune 掉 remote-tracking ref
    Invoke-GitSuccess -WorkDir $Repo.origin -Arguments @('update-ref', '-d', 'refs/heads/feature-gone') | Out-Null
    Invoke-GitSuccess -WorkDir $Repo.path -Arguments @('fetch', '-q', '--prune', 'origin') | Out-Null
}

function Set-DetachedTag {
    param($Repo)
    Invoke-GitSuccess -WorkDir $Repo.path -Arguments @('tag', '-a', 'v1.0', '-m', ('baseline v1.0 for ' + $Repo.name)) | Out-Null
    Invoke-GitSuccess -WorkDir $Repo.path -Arguments @('push', '-q', 'origin', 'refs/tags/v1.0') | Out-Null
    Invoke-GitSuccess -WorkDir $Repo.path -Arguments @('switch', '-q', '--detach', 'v1.0') | Out-Null
}

function Set-DetachedCommit {
    param($Repo)
    $sha = (Invoke-GitSuccess -WorkDir $Repo.path -Arguments @('rev-parse', 'HEAD')).Output[0]
    Invoke-GitSuccess -WorkDir $Repo.path -Arguments @('switch', '-q', '--detach', $sha) | Out-Null
}

function Set-UnmergedRepo {
    param($Repo)
    $p = $Repo.path
    $base = (Invoke-GitSuccess -WorkDir $p -Arguments @('rev-parse', 'HEAD')).Output[0]
    Invoke-GitSuccess -WorkDir $p -Arguments @('switch', '-q', '-c', 'fixture-side', $base) | Out-Null
    Add-CommitFile -RepoPath $p -FileName 'conflict.txt' -Content 'side change' -Message 'side: conflicting content'
    $theirs = (Invoke-GitSuccess -WorkDir $p -Arguments @('rev-parse', 'HEAD')).Output[0]
    Invoke-GitSuccess -WorkDir $p -Arguments @('switch', '-q', 'main') | Out-Null
    Add-CommitFile -RepoPath $p -FileName 'conflict.txt' -Content 'main change' -Message 'main: conflicting content'
    $ours = (Invoke-GitSuccess -WorkDir $p -Arguments @('rev-parse', 'HEAD')).Output[0]
    # read-tree 三路合并：只写 index，产生 unmerged 条目，不产生 MERGE_HEAD
    Invoke-GitSuccess -WorkDir $p -Arguments @('read-tree', '-m', $base, $ours, $theirs) | Out-Null
}

function Set-InProgressRepo {
    param($Repo)
    $p = $Repo.path
    Invoke-GitSuccess -WorkDir $p -Arguments @('switch', '-q', '-c', 'fixture-side') | Out-Null
    Add-CommitFile -RepoPath $p -FileName 'side-only.txt' -Content ('side work for ' + $Repo.name) -Message 'side: non-conflicting change'
    Invoke-GitSuccess -WorkDir $p -Arguments @('switch', '-q', 'main') | Out-Null
    # --no-commit --no-ff：留下 MERGE_HEAD，合并未提交
    Invoke-GitSuccess -WorkDir $p -Arguments @('merge', '-q', '--no-commit', '--no-ff', 'fixture-side') | Out-Null
}

function Set-BusyLock {
    param($Repo)
    $gitDir = (Invoke-GitSuccess -WorkDir $Repo.path -Arguments @('rev-parse', '--absolute-git-dir')).Output[0]
    Write-Utf8NoBom -Path (Join-Path $gitDir 'index.lock') -Text ("fixture: external git process holds index.lock`n")
}

function Convert-RepoToLfsMissing {
    param($Repo)
    $p = $Repo.path
    Invoke-GitSuccess -WorkDir $p -Arguments @('lfs', 'install', '--local') | Out-Null
    Invoke-GitSuccess -WorkDir $p -Arguments @('lfs', 'track', '*.mdl') | Out-Null
    Add-CommitFile -RepoPath $p -FileName 'model.mdl' -Content 'MBD fixture LFS model payload 0123456789' -Message 'lfs: add model.mdl'
    Invoke-GitSuccess -WorkDir $p -Arguments @('push', '-q', 'origin', 'HEAD') | Out-Null
    # 删除本地 LFS 对象，模拟「LFS 对象缺失」
    $gitDir = (Invoke-GitSuccess -WorkDir $p -Arguments @('rev-parse', '--absolute-git-dir')).Output[0]
    $lfsObjects = Join-Path $gitDir 'lfs/objects'
    if (Test-Path -LiteralPath $lfsObjects) { Remove-Item -Recurse -Force -LiteralPath $lfsObjects }
    # 工作区改写为 pointer 文本（与 index blob 一致），并刷新 index stat，使 git status 保持干净
    $blobLines = (Invoke-GitSuccess -WorkDir $p -Arguments @('cat-file', 'blob', 'HEAD:model.mdl')).Output
    $pointer = (($blobLines | Where-Object { $_ -ne $null -and $_ -ne '' }) -join "`n") + "`n"
    Write-Utf8NoBom -Path (Join-Path $p 'model.mdl') -Text $pointer
    Invoke-GitSuccess -WorkDir $p -Arguments @('add', 'model.mdl') | Out-Null
}

# ============================================================================
# 4. 单 fixture 构造调度
# ============================================================================

function Invoke-FixtureBuild {
    param($Ctx, $Def)

    $meta = [ordered]@{
        fixture_id          = $Def.id
        remote_gone         = $false
        ttl_expired         = $false
        job_state           = $null
        expected_origin_urls = [ordered]@{}
    }
    $repos = [ordered]@{}

    switch ($Def.id) {
        'F-SYNCED' { $repos = New-SyncedComponent -Ctx $Ctx }
        'F-BEHIND' {
            $repos = New-SyncedComponent -Ctx $Ctx
            foreach ($t in $script:RepoTypes) { Add-RemoteCommits -Ctx $Ctx -Repo $repos[$t] -Count 2 }
        }
        'F-AHEAD' {
            $repos = New-SyncedComponent -Ctx $Ctx
            foreach ($t in $script:RepoTypes) { Add-LocalCommits -Repo $repos[$t] -Count 2 }
        }
        'F-DIVERGED' {
            $repos = New-SyncedComponent -Ctx $Ctx
            foreach ($t in $script:RepoTypes) {
                Add-LocalCommits -Repo $repos[$t] -Count 1
                Add-RemoteCommits -Ctx $Ctx -Repo $repos[$t] -Count 1
            }
        }
        'F-DIRTY' {
            $repos = New-SyncedComponent -Ctx $Ctx
            foreach ($t in $script:RepoTypes) { Set-DirtyTracked -Repo $repos[$t] }
        }
        'F-NO_UPSTREAM-LOCAL' {
            $repos = New-SyncedComponent -Ctx $Ctx
            foreach ($t in $script:RepoTypes) { New-LocalOnlyBranch -Repo $repos[$t] }
        }
        'F-NO_UPSTREAM-UNSET' {
            $repos = New-SyncedComponent -Ctx $Ctx
            foreach ($t in $script:RepoTypes) { New-UnsetBranch -Repo $repos[$t] }
        }
        'F-NO_UPSTREAM-GONE' {
            $repos = New-SyncedComponent -Ctx $Ctx
            foreach ($t in $script:RepoTypes) { New-GoneBranch -Repo $repos[$t] }
        }
        'F-DETACHED-TAG' {
            $repos = New-SyncedComponent -Ctx $Ctx
            foreach ($t in $script:RepoTypes) { Set-DetachedTag -Repo $repos[$t] }
        }
        'F-DETACHED-COMMIT' {
            $repos = New-SyncedComponent -Ctx $Ctx
            foreach ($t in $script:RepoTypes) { Set-DetachedCommit -Repo $repos[$t] }
        }
        'F-UNMERGED' {
            $repos = New-SyncedComponent -Ctx $Ctx
            foreach ($t in $script:RepoTypes) { Set-UnmergedRepo -Repo $repos[$t] }
        }
        'F-IN_PROGRESS' {
            $repos = New-SyncedComponent -Ctx $Ctx
            foreach ($t in $script:RepoTypes) { Set-InProgressRepo -Repo $repos[$t] }
        }
        'F-EMPTY' {
            foreach ($t in $script:RepoTypes) { $repos[$t] = New-RepoEmpty -Ctx $Ctx -RepoType $t }
        }
        'F-INVALID-NOTREPO' {
            $designName = '{0}_Design' -f $Ctx.Component
            $designOrigin = New-SeededOrigin -Ctx $Ctx -Name $designName
            $designPath = Join-Path $Ctx.ComponentDir $designName
            New-Item -ItemType Directory -Force -Path $designPath | Out-Null
            Write-Utf8NoBom -Path (Join-Path $designPath 'not-a-repo.txt') -Text "此目录存在但并非 git 仓库`n"
            $repos['Design'] = New-RepoRecord -RepoType 'Design' -Path $designPath -Origin $designOrigin -ExpectedOriginUrl $designOrigin
            foreach ($t in @('Impl', 'MIL', 'SIL')) {
                $name = '{0}_{1}' -f $Ctx.Component, $t
                $origin = New-SeededOrigin -Ctx $Ctx -Name $name
                $repos[$t] = New-RepoCloned -Ctx $Ctx -RepoType $t -Origin $origin
            }
        }
        'F-INVALID-REMOTE' {
            $repos = New-SyncedComponent -Ctx $Ctx
            $wrong = $repos['Design'].origin + '.unexpected.git'
            Invoke-GitSuccess -WorkDir $repos['Design'].path -Arguments @('remote', 'set-url', 'origin', $wrong) | Out-Null
        }
        'F-MISSING' {
            $repos['Design'] = New-MissingRepoRecord -Ctx $Ctx -RepoType 'Design' -SeedOrigin
            foreach ($t in @('Impl', 'MIL', 'SIL')) {
                $name = '{0}_{1}' -f $Ctx.Component, $t
                $origin = New-SeededOrigin -Ctx $Ctx -Name $name
                $repos[$t] = New-RepoCloned -Ctx $Ctx -RepoType $t -Origin $origin
            }
        }
        'F-MISSING+BLOCKED' {
            # C-03 顺序锁定：MISSING(3) 与 BLOCKED_LOCAL(4) 并存时组件必须为 MISSING
            $repos['Design'] = New-MissingRepoRecord -Ctx $Ctx -RepoType 'Design' -SeedOrigin
            foreach ($t in @('Impl', 'MIL', 'SIL')) {
                $name = '{0}_{1}' -f $Ctx.Component, $t
                $origin = New-SeededOrigin -Ctx $Ctx -Name $name
                $repos[$t] = New-RepoCloned -Ctx $Ctx -RepoType $t -Origin $origin
            }
            Set-UnmergedRepo -Repo $repos['Impl']
        }
        'F-UNKNOWN' {
            $repos = New-SyncedComponent -Ctx $Ctx
            foreach ($t in $script:RepoTypes) {
                $dead = Join-Path $Ctx.OriginsDir ('_unreachable_' + $repos[$t].name + '.git')
                Invoke-GitSuccess -WorkDir $repos[$t].path -Arguments @('remote', 'set-url', 'origin', $dead) | Out-Null
                $repos[$t].expected_origin_url = $dead
            }
            $meta.ttl_expired = $true
        }
        'F-DETACHED+MIXED' {
            $repos = New-SyncedComponent -Ctx $Ctx
            foreach ($t in $script:RepoTypes) {
                Invoke-GitSuccess -WorkDir $repos[$t].path -Arguments @('tag', '-a', 'v1.0', '-m', ('baseline v1.0 for ' + $repos[$t].name)) | Out-Null
                Invoke-GitSuccess -WorkDir $repos[$t].path -Arguments @('push', '-q', 'origin', 'refs/tags/v1.0') | Out-Null
            }
            foreach ($t in @('Design', 'Impl')) {
                Invoke-GitSuccess -WorkDir $repos[$t].path -Arguments @('switch', '-q', '--detach', 'v1.0') | Out-Null
            }
            # MIL 保持 main；SIL 新建 feature-x 并推送建立 upstream → 四仓分支名不一致
            Invoke-GitSuccess -WorkDir $repos['SIL'].path -Arguments @('switch', '-q', '-c', 'feature-x') | Out-Null
            Invoke-GitSuccess -WorkDir $repos['SIL'].path -Arguments @('push', '-q', '-u', 'origin', 'feature-x') | Out-Null
        }
        'F-TAG_DRIFT' {
            $repos = New-SyncedComponent -Ctx $Ctx
            foreach ($t in @('Design', 'Impl', 'MIL')) {
                Invoke-GitSuccess -WorkDir $repos[$t].path -Arguments @('tag', '-a', 'v1.0', '-m', ('baseline v1.0 for ' + $repos[$t].name)) | Out-Null
                Invoke-GitSuccess -WorkDir $repos[$t].path -Arguments @('push', '-q', 'origin', 'refs/tags/v1.0') | Out-Null
                Invoke-GitSuccess -WorkDir $repos[$t].path -Arguments @('switch', '-q', '--detach', 'v1.0') | Out-Null
            }
            # SIL 不加 tag、留在 main：四仓 tag 集合 3/4 一致 → TAG_DRIFT
        }
        'F-PARTIAL' {
            foreach ($t in @('Design', 'MIL')) {
                $name = '{0}_{1}' -f $Ctx.Component, $t
                $origin = New-SeededOrigin -Ctx $Ctx -Name $name
                $repos[$t] = New-RepoCloned -Ctx $Ctx -RepoType $t -Origin $origin
            }
            $repos['Impl'] = New-MissingRepoRecord -Ctx $Ctx -RepoType 'Impl' -SeedOrigin
            $repos['SIL'] = New-MissingRepoRecord -Ctx $Ctx -RepoType 'SIL' -SeedOrigin
            $meta.job_state = 'partial_failed'
            $jobDir = Join-Path $Ctx.ComponentDir '.mbd-fixture'
            $job = [ordered]@{
                op_id    = 'op-fixture-partial'
                op_type  = 'clone_component'
                state    = 'partial_failed'
                total    = 4
                ok       = 2
                failed   = 2
                items    = @(
                    [ordered]@{ repo = $repos['Impl'].name; state = 'failed'; error_code = 'NET-E001' }
                    [ordered]@{ repo = $repos['SIL'].name; state = 'failed'; error_code = 'NET-E001' }
                    [ordered]@{ repo = $repos['Design'].name; state = 'ok'; error_code = $null }
                    [ordered]@{ repo = $repos['MIL'].name; state = 'ok'; error_code = $null }
                )
            }
            Write-Utf8NoBom -Path (Join-Path $jobDir 'job.json') -Text ($job | ConvertTo-Json -Depth 8)
        }
        'F-LFS-MISSING' {
            $repos = New-SyncedComponent -Ctx $Ctx
            foreach ($t in $script:RepoTypes) { Convert-RepoToLfsMissing -Repo $repos[$t] }
        }
        'F-BUSY' {
            $repos = New-SyncedComponent -Ctx $Ctx
            foreach ($t in $script:RepoTypes) { Set-BusyLock -Repo $repos[$t] }
        }
        'F-REMOTE-GONE' {
            $repos = New-SyncedComponent -Ctx $Ctx
            foreach ($t in $script:RepoTypes) {
                if (Test-Path -LiteralPath $repos[$t].origin) { Remove-Item -Recurse -Force -LiteralPath $repos[$t].origin }
            }
            $meta.remote_gone = $true
        }
        'F-TAG-MOVED' {
            $repos = New-SyncedComponent -Ctx $Ctx
            foreach ($t in $script:RepoTypes) {
                $r = $repos[$t]
                $oldSha = (Invoke-GitSuccess -WorkDir $r.path -Arguments @('rev-parse', 'HEAD')).Output[0]
                Add-RemoteCommits -Ctx $Ctx -Repo $r -Count 1 -FileName 'remote-moved.txt'
                Invoke-GitSuccess -WorkDir $r.path -Arguments @('merge', '-q', '--ff-only', 'origin/main') | Out-Null
                $newSha = (Invoke-GitSuccess -WorkDir $r.path -Arguments @('rev-parse', 'HEAD')).Output[0]
                # 本地 tag v1.0 指向旧 commit；origin 端同名 tag 被 force 移动到新 commit
                Invoke-GitSuccess -WorkDir $r.path -Arguments @('tag', '-f', '-a', 'v1.0', $oldSha, '-m', 'fixture: local tag points to old commit') | Out-Null
                Invoke-GitSuccess -WorkDir $r.origin -Arguments @('tag', '-f', '-a', 'v1.0', $newSha, '-m', 'fixture: remote tag moved') | Out-Null
            }
        }
        default {
            throw ("未知 fixture_id：{0}" -f $Def.id)
        }
    }

    # 期望 origin URL 登记（部分 fixture 会重写为模拟不可达路径）
    foreach ($t in $script:RepoTypes) {
        if ($repos.Contains($t)) { $meta.expected_origin_urls[$t] = $repos[$t].expected_origin_url }
    }

    # 写 fixture 元数据（Gitea/DB 侧事实的离线替身，供测试注入远程元数据层 / IClock）
    $metaObject = [ordered]@{
        fixture_id       = $Def.id
        fixture_notes    = $Def.notes
        gitea_simulation = [ordered]@{
            remote_repo_absent = $meta.remote_gone
            ttl_expired        = $meta.ttl_expired
            job_state          = $meta.job_state
        }
        expected_origin_urls = $meta.expected_origin_urls
    }
    Write-Utf8NoBom -Path (Join-Path $Ctx.FixtureDir 'fixture-meta.json') -Text ($metaObject | ConvertTo-Json -Depth 8)

    return [pscustomobject]@{ repos = $repos; meta = $meta }
}

# ============================================================================
# 5. 参考实现：单仓快照 + 状态推导（自校验用，不依赖被测程序）
# ============================================================================

function Get-RepoSnapshot {
    param([string]$Path, [string]$ExpectedOriginUrl)

    $snap = [ordered]@{
        path                = $Path
        expected_origin_url = $ExpectedOriginUrl
        exists              = $false
        empty_dir           = $false
        is_repo             = $false
        top_level           = $null
        top_level_matches   = $null
        git_dir             = $null
        origin_url          = $null
        origin_matches      = $null
        head_sha            = $null
        unborn              = $false
        branch              = $null
        detached            = $false
        tags_at_head        = @()
        unmerged_count      = 0
        ls_files_u          = @()
        in_progress_markers = @()
        upstream_configured     = $false
        upstream_config_present = $false
        upstream_ref            = $null
        upstream_ref_exists     = $false
        compare_ref         = $null
        relation            = 'UNAVAILABLE'
        ahead               = $null
        behind              = $null
        dirty_tracked       = $false
        untracked           = @()
        stashed             = @()
        shallow             = $false
        index_lock          = $false
        remote_reachable    = $false
        remote_heads        = @()
        remote_has_branch   = $null
        local_tag_map       = [ordered]@{}
        remote_tag_map      = [ordered]@{}
        tag_moved           = $false
        lfs_tracked         = $false
        lfs_missing         = $false
        status_porcelain_v2 = @()
        errors              = @()
    }

    if (-not (Test-Path -LiteralPath $Path)) { return $snap }
    $snap.exists = $true
    $items = @(Get-ChildItem -LiteralPath $Path -Force -ErrorAction SilentlyContinue)
    if ($items.Count -eq 0) { $snap.empty_dir = $true; return $snap }

    $top = Invoke-GitProbe -WorkDir $Path -Arguments @('rev-parse', '--show-toplevel')
    if ($top.ExitCode -ne 0) { return $snap }
    $snap.is_repo = $true
    $snap.top_level = ($top.Output | Where-Object { $_ -ne '' } | Select-Object -First 1)
    # git 在 Windows 上可能返回正斜杠路径，这里统一反斜杠 + 去尾斜杠后做大小写不敏感比较
    $resolved = Normalize-PathForCompare ((Resolve-Path -LiteralPath $Path).Path)
    $topResolved = Normalize-PathForCompare $snap.top_level
    $snap.top_level_matches = [string]::Equals($resolved, $topResolved, [System.StringComparison]::OrdinalIgnoreCase)

    $gitDirProbe = Invoke-GitProbe -WorkDir $Path -Arguments @('rev-parse', '--absolute-git-dir')
    if ($gitDirProbe.ExitCode -eq 0) { $snap.git_dir = ($gitDirProbe.Output | Where-Object { $_ -ne '' } | Select-Object -First 1) }

    $originProbe = Invoke-GitProbe -WorkDir $Path -Arguments @('remote', 'get-url', 'origin')
    if ($originProbe.ExitCode -eq 0) {
        $snap.origin_url = ($originProbe.Output | Where-Object { $_ -ne '' } | Select-Object -First 1)
        $snap.origin_matches = Test-UrlEqual $snap.origin_url $ExpectedOriginUrl
    }
    else {
        $snap.origin_matches = $false
    }

    # 绑定校验不通过时仍采集基础证据，但不再继续关系类探测（自校验会判 INVALID）
    $headProbe = Invoke-GitProbe -WorkDir $Path -Arguments @('rev-parse', '--verify', '--quiet', 'HEAD')
    if ($headProbe.ExitCode -eq 0) {
        $snap.head_sha = ($headProbe.Output | Where-Object { $_ -ne '' } | Select-Object -First 1)
    }
    else {
        $snap.unborn = $true
    }

    $branchProbe = Invoke-GitProbe -WorkDir $Path -Arguments @('symbolic-ref', '-q', '--short', 'HEAD')
    if ($branchProbe.ExitCode -eq 0) {
        $snap.branch = ($branchProbe.Output | Where-Object { $_ -ne '' } | Select-Object -First 1)
        $snap.detached = $false
    }
    else {
        $snap.detached = $true
    }

    $tagsHead = Invoke-GitProbe -WorkDir $Path -Arguments @('tag', '--points-at', 'HEAD')
    if ($tagsHead.ExitCode -eq 0) { $snap.tags_at_head = @($tagsHead.Output | Where-Object { $_ -ne '' }) }

    $statusProbe = Invoke-GitProbe -WorkDir $Path -Arguments @('status', '--porcelain=v2', '--branch')
    if ($statusProbe.ExitCode -eq 0) {
        $snap.status_porcelain_v2 = @($statusProbe.Output | Select-Object -First 16)
    }
    else {
        $snap.errors += ('status 失败: ' + ($statusProbe.Output -join ' '))
    }

    $unmergedProbe = Invoke-GitProbe -WorkDir $Path -Arguments @('ls-files', '-u')
    if ($unmergedProbe.ExitCode -eq 0) {
        $snap.ls_files_u = @($unmergedProbe.Output | Where-Object { $_ -ne '' })
        $snap.unmerged_count = $snap.ls_files_u.Count
    }

    if ($snap.git_dir) {
        foreach ($marker in @('MERGE_HEAD', 'rebase-merge', 'rebase-apply', 'CHERRY_PICK_HEAD', 'BISECT_LOG')) {
            if (Test-Path -LiteralPath (Join-Path $snap.git_dir $marker)) { $snap.in_progress_markers += $marker }
        }
        $snap.index_lock = Test-Path -LiteralPath (Join-Path $snap.git_dir 'index.lock')
    }

    $shallowProbe = Invoke-GitProbe -WorkDir $Path -Arguments @('rev-parse', '--is-shallow-repository')
    if ($shallowProbe.ExitCode -eq 0) { $snap.shallow = (($shallowProbe.Output -join '') -match 'true') }

    if (-not $snap.unborn) {
        $dirtyProbe = Invoke-GitProbe -WorkDir $Path -Arguments @('diff-index', '--quiet', 'HEAD', '--')
        if ($dirtyProbe.ExitCode -eq 0) { $snap.dirty_tracked = $false }
        elseif ($dirtyProbe.ExitCode -eq 1) { $snap.dirty_tracked = $true }
        else { $snap.errors += ('diff-index 失败: ' + ($dirtyProbe.Output -join ' ')) }
    }

    $untrackedProbe = Invoke-GitProbe -WorkDir $Path -Arguments @('ls-files', '--others', '--exclude-standard')
    if ($untrackedProbe.ExitCode -eq 0) { $snap.untracked = @($untrackedProbe.Output | Where-Object { $_ -ne '' }) }

    $stashProbe = Invoke-GitProbe -WorkDir $Path -Arguments @('stash', 'list')
    if ($stashProbe.ExitCode -eq 0) { $snap.stashed = @($stashProbe.Output | Where-Object { $_ -ne '' }) }

    # 上游配置与 ref 存在性。
    # 注意：remote-tracking ref 被 prune 掉之后 @{upstream} 会解析失败，但
    # branch.<name>.remote / branch.<name>.merge 配置仍在 —— 这正是 UPSTREAM_GONE 的特征，
    # 因此必须再查一次配置，不能只看 @{upstream} 是否可解析。
    $upProbe = Invoke-GitProbe -WorkDir $Path -Arguments @('rev-parse', '--symbolic-full-name', '@{upstream}')
    if ($upProbe.ExitCode -eq 0) {
        $snap.upstream_configured = $true
        $snap.upstream_config_present = $true
        $snap.upstream_ref = ($upProbe.Output | Where-Object { $_ -ne '' } | Select-Object -First 1)
        $upExists = Invoke-GitProbe -WorkDir $Path -Arguments @('rev-parse', '--verify', '--quiet', $snap.upstream_ref)
        $snap.upstream_ref_exists = ($upExists.ExitCode -eq 0)
    }
    elseif ($snap.branch) {
        $cfgRemote = Invoke-GitProbe -WorkDir $Path -Arguments @('config', '--get', ('branch.' + $snap.branch + '.remote'))
        $cfgMerge = Invoke-GitProbe -WorkDir $Path -Arguments @('config', '--get', ('branch.' + $snap.branch + '.merge'))
        if ($cfgRemote.ExitCode -eq 0 -and $cfgMerge.ExitCode -eq 0) {
            $snap.upstream_configured = $true
            $snap.upstream_config_present = $true
            $snap.upstream_ref_exists = $false
        }
    }

    # 远端可达性（本地 file 路径，失败即视为 fetch 失败）
    $lsRemote = Invoke-GitProbe -WorkDir $Path -Arguments @('ls-remote', '--quiet', '--heads', 'origin')
    if ($lsRemote.ExitCode -eq 0) {
        $snap.remote_reachable = $true
        foreach ($line in $lsRemote.Output) {
            if ($line -match 'refs/heads/(.+)$') { $snap.remote_heads += $Matches[1] }
        }
    }
    if ($snap.branch) {
        $snap.remote_has_branch = @($snap.remote_heads | Where-Object { $_ -eq $snap.branch }).Count -gt 0
    }

    # 本地 tag → commit（annotated tag 需 ^{commit} 解引用）
    $localTags = Invoke-GitProbe -WorkDir $Path -Arguments @('for-each-ref', '--format=%(refname:short)', 'refs/tags')
    if ($localTags.ExitCode -eq 0) {
        foreach ($t in @($localTags.Output | Where-Object { $_ -ne '' })) {
            $peel = Invoke-GitProbe -WorkDir $Path -Arguments @('rev-parse', '--verify', '--quiet', ('refs/tags/' + $t + '^{commit}'))
            if ($peel.ExitCode -eq 0) { $snap.local_tag_map[$t] = ($peel.Output | Where-Object { $_ -ne '' } | Select-Object -First 1) }
        }
    }

    # 远端 tag → commit（仅当 origin 是本地存在的 bare 仓时；否则跳过，无法验证 tag_moved）
    $originPath = $snap.origin_url
    if ($originPath -and (Test-Path -LiteralPath $originPath) -and (Test-Path -LiteralPath (Join-Path $originPath 'HEAD'))) {
        $remoteTags = Invoke-GitProbe -WorkDir $originPath -Arguments @('for-each-ref', '--format=%(refname:short)', 'refs/tags')
        if ($remoteTags.ExitCode -eq 0) {
            foreach ($t in @($remoteTags.Output | Where-Object { $_ -ne '' })) {
                $peel = Invoke-GitProbe -WorkDir $originPath -Arguments @('rev-parse', '--verify', '--quiet', ('refs/tags/' + $t + '^{commit}'))
                if ($peel.ExitCode -eq 0) { $snap.remote_tag_map[$t] = ($peel.Output | Where-Object { $_ -ne '' } | Select-Object -First 1) }
            }
        }
    }
    foreach ($t in @($snap.local_tag_map.Keys)) {
        if (@($snap.remote_tag_map.Keys) -contains $t -and $snap.local_tag_map[$t] -ne $snap.remote_tag_map[$t]) {
            $snap.tag_moved = $true
        }
    }

    # LFS 检测：.gitattributes 含 filter=lfs，且工作区为 pointer 而本地对象缺失
    $attrsPath = Join-Path $Path '.gitattributes'
    if (Test-Path -LiteralPath $attrsPath) {
        $attrsText = Get-Content -LiteralPath $attrsPath -Raw
        if ($attrsText -match 'filter=lfs') { $snap.lfs_tracked = $true }
    }
    if ($snap.lfs_tracked -and $snap.git_dir) {
        $trackedProbe = Invoke-GitProbe -WorkDir $Path -Arguments @('ls-files')
        if ($trackedProbe.ExitCode -eq 0) {
            foreach ($f in @($trackedProbe.Output | Where-Object { $_ -ne '' })) {
                $fp = Join-Path $Path ($f -replace '/', [System.IO.Path]::DirectorySeparatorChar)
                if (-not (Test-Path -LiteralPath $fp -PathType Leaf)) { continue }
                $first = @(Get-Content -LiteralPath $fp -TotalCount 3 -ErrorAction SilentlyContinue)
                if ($first.Count -ge 1 -and $first[0] -like 'version https://git-lfs.github.com/spec/v1*') {
                    $oid = $null
                    foreach ($line in $first) {
                        if ($line -match '^oid sha256:([0-9a-f]{64})$') { $oid = $Matches[1] }
                    }
                    if ($oid) {
                        $obj = Join-Path $snap.git_dir ('lfs/objects/{0}/{1}/{2}' -f $oid.Substring(0, 2), $oid.Substring(2, 2), $oid)
                        if (-not (Test-Path -LiteralPath $obj)) { $snap.lfs_missing = $true }
                    }
                }
            }
        }
    }

    return $snap
}

function Get-RepoDerivedState {
    param($Snap, $Meta)

    $flags = [System.Collections.ArrayList]::new()
    if ($Snap.exists -and $Snap.is_repo) {
        if ($Snap.dirty_tracked) { Add-Flag $flags 'dirty_tracked' }
        if (@($Snap.untracked).Count -gt 0) { Add-Flag $flags 'untracked' }
        if (@($Snap.stashed).Count -gt 0) { Add-Flag $flags 'stashed' }
        if ($Snap.shallow) { Add-Flag $flags 'shallow' }
        if ($Snap.index_lock) { Add-Flag $flags 'busy' }
        if ($Snap.lfs_missing) { Add-Flag $flags 'lfs_missing' }
    }
    if ($Meta.remote_gone) { Add-Flag $flags 'remote_gone' }
    if ($Snap.tag_moved) { Add-Flag $flags 'tag_moved' }

    $primary = 'UNKNOWN'
    $subType = $null

    if (-not $Snap.exists) { $primary = 'MISSING' }
    elseif ($Snap.empty_dir) { $primary = 'MISSING' }
    elseif (-not $Snap.is_repo) { $primary = 'INVALID' }
    elseif (-not $Snap.top_level_matches) { $primary = 'INVALID' }
    elseif (-not $Snap.origin_matches) { $primary = 'INVALID' }
    elseif ($Snap.unborn) { $primary = 'EMPTY' }
    elseif ($Snap.unmerged_count -gt 0) { $primary = 'UNMERGED' }
    elseif (@($Snap.in_progress_markers).Count -gt 0) { $primary = 'IN_PROGRESS' }
    elseif ($Snap.detached) {
        $Snap.relation = 'DETACHED'
        if (@($Snap.tags_at_head).Count -gt 0) { $primary = 'DETACHED_TAG' } else { $primary = 'DETACHED_COMMIT' }
    }
    else {
        # 在分支上：关系类状态要求 fetch 成功且数据新鲜（SRS 7.1/7.2）
        if (-not $Snap.remote_reachable -or $Meta.ttl_expired) {
            $primary = 'UNKNOWN'
            Add-Flag $flags 'stale'
        }
        elseif (-not $Snap.upstream_configured) {
            if ($Snap.remote_has_branch) { $subType = 'UNSET_BUT_EXISTS' } else { $subType = 'LOCAL_ONLY' }
            $primary = 'NO_UPSTREAM'
            $Snap.relation = 'NO_UPSTREAM'
        }
        elseif (-not $Snap.upstream_ref_exists) {
            if ($Snap.remote_has_branch) { $primary = 'UNKNOWN' }
            else { $subType = 'UPSTREAM_GONE'; $primary = 'NO_UPSTREAM' }
            $Snap.relation = 'NO_UPSTREAM'
        }
        else {
            $compareRef = $Snap.upstream_ref
            $Snap.compare_ref = $compareRef
            $cmpProbe = Invoke-GitProbe -WorkDir $Snap.path -Arguments @('rev-parse', '--verify', '--quiet', $compareRef)
            if ($cmpProbe.ExitCode -ne 0) {
                $primary = 'UNKNOWN'
                Add-Flag $flags 'stale'
            }
            else {
                $cmpSha = ($cmpProbe.Output | Where-Object { $_ -ne '' } | Select-Object -First 1)
                $countProbe = Invoke-GitProbe -WorkDir $Snap.path -Arguments @('rev-list', '--left-right', '--count', ('HEAD...' + $compareRef))
                if ($countProbe.ExitCode -eq 0 -and $countProbe.Output.Count -ge 1) {
                    $parts = ($countProbe.Output[0] -split '\s+') | Where-Object { $_ -ne '' }
                    if ($parts.Count -ge 2) {
                        $Snap.ahead = [int]$parts[0]
                        $Snap.behind = [int]$parts[1]
                    }
                }
                if ($cmpSha -eq $Snap.head_sha) {
                    $Snap.relation = 'EQUAL'
                    if ($Snap.dirty_tracked) { $primary = 'DIRTY' } else { $primary = 'SYNCED' }
                }
                else {
                    $ancHead = Invoke-GitProbe -WorkDir $Snap.path -Arguments @('merge-base', '--is-ancestor', 'HEAD', $compareRef)
                    $ancRef = Invoke-GitProbe -WorkDir $Snap.path -Arguments @('merge-base', '--is-ancestor', $compareRef, 'HEAD')
                    if ($ancHead.ExitCode -eq 0) { $Snap.relation = 'BEHIND'; $primary = 'BEHIND' }
                    elseif ($ancRef.ExitCode -eq 0) { $Snap.relation = 'AHEAD'; $primary = 'AHEAD' }
                    elseif ($ancHead.ExitCode -eq 1 -and $ancRef.ExitCode -eq 1) { $Snap.relation = 'DIVERGED'; $primary = 'DIVERGED' }
                    else { $Snap.relation = 'UNKNOWN'; $primary = 'UNKNOWN'; Add-Flag $flags 'stale' }
                }
            }
        }
    }

    return [ordered]@{
        primary  = $primary
        sub_type = $subType
        flags    = @($flags)
    }
}

function Get-TagDriftInfo {
    param($Snapshots)
    $eligible = @($Snapshots | Where-Object {
            $_.is_repo -and (-not $_.unborn) -and $_.top_level_matches -and $_.origin_matches
        })
    $sets = @()
    foreach ($s in $eligible) {
        $names = @(@($s.local_tag_map.Keys) | Sort-Object)
        $sets += ($names -join ',')
    }
    $distinct = @($sets | Sort-Object -Unique)
    $drift = ($eligible.Count -ge 2 -and $distinct.Count -gt 1)
    $moved = @($eligible | Where-Object { $_.tag_moved }).Count -gt 0
    if ($moved) { $drift = $true }
    return [ordered]@{
        drift     = $drift
        moved     = $moved
        tag_sets  = $sets
        eligible  = $eligible.Count
    }
}

function Get-ComponentDerivedAggregate {
    param($RepoChecks, $Snapshots, $Meta)

    $states = @($RepoChecks | ForEach-Object { $_.self_check.derived_primary_state })
    $candidates = [System.Collections.ArrayList]::new()
    $addCandidate = {
        param([string]$c)
        if (-not $candidates.Contains($c)) { [void]$candidates.Add($c) }
    }

    if ($Meta.job_state -eq 'partial_failed') { & $addCandidate 'OP_PARTIAL_FAILED' }
    if ($states -contains 'INVALID') { & $addCandidate 'BINDING_ERROR' }
    if ($states -contains 'UNKNOWN') { & $addCandidate 'UNKNOWN' }
    if (($states -contains 'UNMERGED') -or ($states -contains 'IN_PROGRESS')) { & $addCandidate 'BLOCKED_LOCAL' }
    if ($states -contains 'MISSING') { & $addCandidate 'MISSING' }
    if ($states -contains 'EMPTY') { & $addCandidate 'EMPTY' }

    $tagInfo = Get-TagDriftInfo -Snapshots $Snapshots
    if ($tagInfo.drift) { & $addCandidate 'TAG_DRIFT' }

    foreach ($st in @('DIVERGED', 'BEHIND', 'AHEAD', 'DETACHED_TAG', 'DETACHED_COMMIT', 'NO_UPSTREAM', 'DIRTY', 'SYNCED')) {
        if ($states -contains $st) {
            $mapped = $script:RepoToComponentMap[$st]
            if ($mapped) { & $addCandidate $mapped }
        }
    }

    # 组件级 mixed_branch：在分支上的四仓当前分支名不一致
    $branchNames = @($Snapshots | Where-Object {
            $_.is_repo -and (-not $_.unborn) -and (-not $_.detached) -and $_.branch
        } | ForEach-Object { $_.branch })
    $mixed = ($branchNames.Count -gt 1) -and (@($branchNames | Sort-Object -Unique).Count -gt 1)
    if ($mixed) { & $addCandidate 'MIXED_BRANCH' }

    $best = $null
    $bestPriority = 999
    foreach ($c in $candidates) {
        $p = $script:AggregatePriority[$c]
        if ($null -ne $p -and $p -lt $bestPriority) { $bestPriority = $p; $best = $c }
    }
    if (-not $best) { $best = 'UNKNOWN' }  # SRS 7.4 兜底

    return [ordered]@{
        aggregate    = $best
        mixed_branch = $mixed
        tag_drift    = $tagInfo.drift
        tag_moved    = $tagInfo.moved
        tag_sets     = $tagInfo.tag_sets
        branch_names = @($branchNames | Sort-Object -Unique)
        candidates   = @($candidates)
        component_flags = @(
            if ($mixed) { 'mixed_branch' }
        )
    }
}

# ============================================================================
# 6. 自校验
# ============================================================================

function New-RepoEvidence {
    param($Snap)
    return [ordered]@{
        exists              = $Snap.exists
        empty_dir           = $Snap.empty_dir
        is_repo             = $Snap.is_repo
        top_level           = $Snap.top_level
        top_level_matches   = $Snap.top_level_matches
        origin_url          = $Snap.origin_url
        origin_matches      = $Snap.origin_matches
        unborn              = $Snap.unborn
        branch              = $Snap.branch
        detached            = $Snap.detached
        head_sha            = $Snap.head_sha
        tags_at_head        = @($Snap.tags_at_head)
        unmerged_count      = $Snap.unmerged_count
        ls_files_u          = @($Snap.ls_files_u | Select-Object -First 5)
        in_progress_markers = @($Snap.in_progress_markers)
        upstream_configured     = $Snap.upstream_configured
        upstream_config_present = $Snap.upstream_config_present
        upstream_ref            = $Snap.upstream_ref
        upstream_ref_exists     = $Snap.upstream_ref_exists
        compare_ref         = $Snap.compare_ref
        relation            = $Snap.relation
        ahead               = $Snap.ahead
        behind              = $Snap.behind
        dirty_tracked       = $Snap.dirty_tracked
        untracked_files     = @($Snap.untracked)
        stashed             = @($Snap.stashed)
        shallow             = $Snap.shallow
        index_lock          = $Snap.index_lock
        remote_reachable    = $Snap.remote_reachable
        remote_has_branch   = $Snap.remote_has_branch
        local_tags          = $Snap.local_tag_map
        remote_tags         = $Snap.remote_tag_map
        tag_moved           = $Snap.tag_moved
        lfs_tracked         = $Snap.lfs_tracked
        lfs_missing         = $Snap.lfs_missing
        status_porcelain_v2 = @($Snap.status_porcelain_v2)
        errors              = @($Snap.errors)
    }
}

function Invoke-FixtureSelfCheck {
    param($Ctx, $Def, $Built)

    $meta = $Built.meta
    $repoChecks = [System.Collections.ArrayList]::new()
    $snapshots = [System.Collections.ArrayList]::new()

    foreach ($t in $script:RepoTypes) {
        $record = $null
        if ($Built.repos.Contains($t)) { $record = $Built.repos[$t] }
        if ($record) {
            $repoPath = $record.path
            $expectedOrigin = $record.expected_origin_url
        }
        else {
            $repoPath = Join-Path $Ctx.ComponentDir ('{0}_{1}' -f $Ctx.Component, $t)
            $expectedOrigin = Join-Path $Ctx.OriginsDir ('{0}_{1}.git' -f $Ctx.Component, $t)
        }

        $snap = Get-RepoSnapshot -Path $repoPath -ExpectedOriginUrl $expectedOrigin
        [void]$snapshots.Add($snap)
        $derived = Get-RepoDerivedState -Snap $snap -Meta $meta

        $expectedState = $Def.repos[$t]
        $expectedSub = $null
        if ($expectedState -eq 'NO_UPSTREAM') { $expectedSub = $Def.sub }

        $mismatches = @()
        if ($derived.primary -ne $expectedState) {
            $mismatches += ('primary_state：期望 {0}，实际 {1}' -f $expectedState, $derived.primary)
        }
        if ($expectedSub -and $derived.sub_type -ne $expectedSub) {
            $mismatches += ('sub_type：期望 {0}，实际 {1}' -f $expectedSub, $derived.sub_type)
        }

        [void]$repoChecks.Add([ordered]@{
                repo_type              = $t
                path                   = $repoPath
                expected_origin_url    = $expectedOrigin
                configured_origin_url  = $snap.origin_url
                expected_primary_state = $expectedState
                expected_sub_type      = $expectedSub
                self_check             = [ordered]@{
                    ok                    = ($mismatches.Count -eq 0)
                    derived_primary_state = $derived.primary
                    derived_sub_type      = $derived.sub_type
                    derived_flags         = @($derived.flags)
                    mismatches            = @($mismatches)
                    evidence              = (New-RepoEvidence -Snap $snap)
                }
            })
    }

    $comp = Get-ComponentDerivedAggregate -RepoChecks $repoChecks -Snapshots $snapshots -Meta $meta
    $fixtureMismatches = [System.Collections.ArrayList]::new()

    foreach ($rc in $repoChecks) {
        foreach ($m in $rc.self_check.mismatches) {
            [void]$fixtureMismatches.Add(('[repo {0}] {1}' -f $rc.repo_type, $m))
        }
    }

    # 代表性主状态 = 四仓主状态中优先级最高者
    $representative = $null
    $bestPriority = 999
    foreach ($rc in $repoChecks) {
        $p = $script:PrimaryPriority[$rc.self_check.derived_primary_state]
        if ($null -ne $p -and $p -lt $bestPriority) { $bestPriority = $p; $representative = $rc.self_check.derived_primary_state }
    }
    if ($null -ne $Def.primary -and $representative -ne $Def.primary) {
        [void]$fixtureMismatches.Add(('representative_primary_state：期望 {0}，实际 {1}' -f $Def.primary, $representative))
    }

    if ($comp.aggregate -ne $Def.aggregate) {
        [void]$fixtureMismatches.Add(('component_aggregate：期望 {0}，实际 {1}' -f $Def.aggregate, $comp.aggregate))
    }

    $derivedFlags = [System.Collections.ArrayList]::new()
    foreach ($rc in $repoChecks) {
        foreach ($f in $rc.self_check.derived_flags) { Add-Flag $derivedFlags $f }
    }
    foreach ($f in $comp.component_flags) { Add-Flag $derivedFlags $f }
    foreach ($expectedFlag in @($Def.flags)) {
        if (-not $derivedFlags.Contains($expectedFlag)) {
            [void]$fixtureMismatches.Add(('flag 缺失：{0}（实际 flags：{1}）' -f $expectedFlag, (@($derivedFlags) -join ', ')))
        }
    }

    $ok = ($fixtureMismatches.Count -eq 0)
    return [ordered]@{
        repos      = @($repoChecks)
        self_check = [ordered]@{
            ok                            = $ok
            derived_component_aggregate   = $comp.aggregate
            expected_component_aggregate  = $Def.aggregate
            representative_primary_state  = $representative
            expected_primary_state        = $Def.primary
            derived_component_flags       = @($derivedFlags)
            expected_flags                = @($Def.flags)
            mixed_branch                  = $comp.mixed_branch
            tag_drift                     = $comp.tag_drift
            tag_moved                     = $comp.tag_moved
            tag_sets                      = @($comp.tag_sets)
            branch_names                  = @($comp.branch_names)
            aggregate_candidates          = @($comp.candidates)
            mismatches                    = @($fixtureMismatches)
            commands                      = @($script:StandardCheckCommands)
        }
    }
}

# ============================================================================
# 7. 目录安全、清理与环境
# ============================================================================

function Resolve-ExecutionRoot {
    param([string]$RootParam)
    if ($RootParam) {
        $root = [System.IO.Path]::GetFullPath($RootParam)
    }
    else {
        $root = Join-Path $script:BaseTempDir ('run-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
    }
    $trimmed = $root.TrimEnd('\', '/')
    if ($trimmed -match '^[A-Za-z]:$') { throw ('拒绝使用盘符根目录：{0}' -f $trimmed) }
    if ($trimmed -match '^[A-Za-z]:\\?$') { throw ('拒绝使用盘符根目录：{0}' -f $trimmed) }
    $userProfile = [System.IO.Path]::GetFullPath($env:USERPROFILE).TrimEnd('\', '/')
    if ([string]::Equals($trimmed, $userProfile, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw ('拒绝使用用户主目录作为 Root：{0}' -f $trimmed)
    }
    if ($env:SystemRoot) {
        $sysRoot = [System.IO.Path]::GetFullPath($env:SystemRoot).TrimEnd('\', '/')
        if ([string]::Equals($trimmed, $sysRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw ('拒绝使用系统目录作为 Root：{0}' -f $trimmed)
        }
    }
    if (Test-Path -LiteralPath (Join-Path $trimmed '.git')) {
        throw ('Root 指向一个已有 git 仓库，拒绝写入：{0}' -f $trimmed)
    }
    return $trimmed
}

function Test-OwnedRoot {
    param([string]$RootPath)
    return (Test-Path -LiteralPath (Join-Path $RootPath '.mbd-fixtures-root.json'))
}

function Initialize-ExecutionRoot {
    param([string]$RootPath)
    if (Test-Path -LiteralPath $RootPath) {
        $children = @(Get-ChildItem -LiteralPath $RootPath -Force -ErrorAction SilentlyContinue)
        if ($children.Count -gt 0 -and -not (Test-OwnedRoot $RootPath)) {
            throw ('Root 已存在且非空，又不是本脚本创建的目录（缺少 .mbd-fixtures-root.json）。' +
                '请指定空目录或先执行 -Clean。Root = {0}' -f $RootPath)
        }
    }
    else {
        New-Item -ItemType Directory -Force -Path $RootPath | Out-Null
    }
    if (-not (Test-OwnedRoot $RootPath)) {
        $marker = [ordered]@{
            generator     = 'state-matrix.ps1'
            script_version = $script:ScriptVersion
            created_at    = (Get-Date).ToString('o')
            component     = $Component
            host          = $env:COMPUTERNAME
            safety        = '本目录由 reqs/v0.2/fixtures/state-matrix.ps1 创建，-Clean 只会删除带本标记的目录。'
        }
        Write-Utf8NoBom -Path (Join-Path $RootPath '.mbd-fixtures-root.json') -Text ($marker | ConvertTo-Json -Depth 5)
    }
}

function Initialize-GitEnvironment {
    param([string]$RootPath)
    $globalCfg = Join-Path $RootPath '.gitconfig'
    $systemCfg = Join-Path $RootPath '.gitconfig-system'
    $globalText = @"
# 由 state-matrix.ps1 生成的隔离 git 配置（不触碰用户全局/系统配置）
[user]
	name = MBD Git Fixture
	email = fixture@example.invalid
[init]
	defaultBranch = main
[commit]
	gpgsign = false
[tag]
	gpgsign = false
[core]
	autocrlf = false
	longpaths = true
[fetch]
	prune = false
"@
    Write-Utf8NoBom -Path $globalCfg -Text ($globalText + "`n")
    Write-Utf8NoBom -Path $systemCfg -Text "# 空系统级配置（隔离宿主环境）`n"
    $env:GIT_CONFIG_GLOBAL = $globalCfg
    $env:GIT_CONFIG_SYSTEM = $systemCfg
    $env:GIT_TERMINAL_PROMPT = '0'
}

function Invoke-Cleanup {
    param([string]$RootParam)
    $removed = @()
    if ($RootParam) {
        $root = [System.IO.Path]::GetFullPath($RootParam).TrimEnd('\', '/')
        if (-not (Test-Path -LiteralPath $root)) {
            Write-Info ('没有需要清理的目录：{0}' -f $root)
            return $removed
        }
        if (-not (Test-OwnedRoot $root)) {
            throw ('拒绝清理：{0} 不是本脚本创建的目录（缺少 .mbd-fixtures-root.json 标记）。' -f $root)
        }
        Remove-Item -Recurse -Force -LiteralPath $root
        $removed += $root
        Write-Ok ('已删除 {0}' -f $root)
        return $removed
    }

    $base = $script:BaseTempDir
    if (-not (Test-Path -LiteralPath $base)) {
        Write-Info ('没有需要清理的目录：{0}' -f $base)
        return $removed
    }
    foreach ($child in @(Get-ChildItem -LiteralPath $base -Force)) {
        if ($child.PSIsContainer -and (Test-OwnedRoot $child.FullName)) {
            Remove-Item -Recurse -Force -LiteralPath $child.FullName
            $removed += $child.FullName
            Write-Ok ('已删除 {0}' -f $child.FullName)
        }
        else {
            Write-Warn2 ('跳过非本脚本创建的条目：{0}' -f $child.FullName)
        }
    }
    if (@(Get-ChildItem -LiteralPath $base -Force -ErrorAction SilentlyContinue).Count -eq 0) {
        Remove-Item -Force -LiteralPath $base
    }
    if ($removed.Count -eq 0) { Write-Info '没有需要清理的本脚本目录。' }
    return $removed
}

# ============================================================================
# 8. 前置条件与 expected.json 一致性
# ============================================================================

function Assert-Prerequisites {
    $gitCmd = Get-Command git -ErrorAction SilentlyContinue
    if (-not $gitCmd) { throw '未找到 git 可执行文件，请先安装 Git ≥ 2.40 并加入 PATH。' }
    $verOut = & git --version 2>&1
    if ($LASTEXITCODE -ne 0) { throw ('git --version 执行失败：{0}' -f (@($verOut) -join ' ')) }
    $verText = (@($verOut | ForEach-Object { "$_" }) -join ' ')
    $m = [regex]::Match($verText, 'git version (\d+)\.(\d+)')
    if (-not $m.Success) { throw ('无法解析 git 版本：{0}' -f $verText) }
    $major = [int]$m.Groups[1].Value
    $minor = [int]$m.Groups[2].Value
    if ($major -lt 2 -or ($major -eq 2 -and $minor -lt 40)) {
        throw ('需要 git ≥ 2.40（SRS 附录 D git.path），当前：{0}' -f $verText)
    }

    $lfsAvailable = $false
    $lfsVersion = $null
    try {
        $lfsOut = & git lfs version 2>&1
        if ($LASTEXITCODE -eq 0) {
            $lfsAvailable = $true
            $lfsVersion = (@($lfsOut | ForEach-Object { "$_" }) -join ' ')
        }
    }
    catch {
        $lfsAvailable = $false
    }

    return [pscustomobject]@{
        git_version   = $verText
        git_major     = $major
        git_minor     = $minor
        lfs_available = $lfsAvailable
        lfs_version   = $lfsVersion
    }
}

function Test-JsonValueEqual {
    param($A, $B)
    if ($null -eq $A -and $null -eq $B) { return $true }
    if ($null -eq $A -or $null -eq $B) { return $false }
    return ([string]$A -eq [string]$B)
}

function Test-JsonArrayEqual {
    param($A, $B)
    $a = @(@($A) | Where-Object { $null -ne $_ } | ForEach-Object { [string]$_ })
    $b = @(@($B) | Where-Object { $null -ne $_ } | ForEach-Object { [string]$_ })
    if ($a.Count -ne $b.Count) { return $false }
    for ($i = 0; $i -lt $a.Count; $i++) {
        if ($a[$i] -ne $b[$i]) { return $false }
    }
    return $true
}

function Assert-ExpectedJsonConsistency {
    $expectedPath = Join-Path $PSScriptRoot 'expected.json'
    if (-not (Test-Path -LiteralPath $expectedPath)) {
        throw ('缺少期望值静态副本：{0}（应与附录 C 一致）' -f $expectedPath)
    }
    $raw = Get-Content -LiteralPath $expectedPath -Raw -Encoding utf8
    $json = $raw | ConvertFrom-Json -AsHashtable
    if (-not $json.ContainsKey('fixtures')) { throw 'expected.json 缺少 fixtures 字段。' }
    $problems = [System.Collections.ArrayList]::new()
    $jsonIds = @($json.fixtures.Keys)
    foreach ($def in $script:FixtureDefs) {
        if (-not $json.fixtures.ContainsKey($def.id)) {
            [void]$problems.Add(('{0}: expected.json 缺失该 fixture' -f $def.id)); continue
        }
        $e = $json.fixtures[$def.id]
        if (-not (Test-JsonValueEqual $e['expected_primary_state'] $def.primary)) {
            [void]$problems.Add(('{0}: expected_primary_state 不一致（expected.json={1} / 脚本={2})' -f $def.id, $e['expected_primary_state'], $def.primary))
        }
        if (-not (Test-JsonValueEqual $e['expected_sub_type'] $def.sub)) {
            [void]$problems.Add(('{0}: expected_sub_type 不一致（expected.json={1} / 脚本={2})' -f $def.id, $e['expected_sub_type'], $def.sub))
        }
        if (-not (Test-JsonArrayEqual $e['expected_flags'] $def.flags)) {
            [void]$problems.Add(('{0}: expected_flags 不一致' -f $def.id))
        }
        if (-not (Test-JsonValueEqual $e['expected_component_aggregate'] $def.aggregate)) {
            [void]$problems.Add(('{0}: expected_component_aggregate 不一致（expected.json={1} / 脚本={2})' -f $def.id, $e['expected_component_aggregate'], $def.aggregate))
        }
        if ($e.ContainsKey('expected_repos')) {
            foreach ($t in $script:RepoTypes) {
                if (-not (Test-JsonValueEqual $e['expected_repos'][$t] $def.repos[$t])) {
                    [void]$problems.Add(('{0}/{1}: expected_repos 不一致' -f $def.id, $t))
                }
            }
        }
    }
    foreach ($id in $jsonIds) {
        if (-not $script:FixtureDefById.ContainsKey($id)) {
            [void]$problems.Add(('{0}: expected.json 含脚本未定义的 fixture' -f $id))
        }
    }
    if ($problems.Count -gt 0) {
        throw ('expected.json 与脚本内夹具定义不一致（共 {0} 处）：`n{1}' -f $problems.Count, (@($problems) -join "`n"))
    }
    Write-Info ('expected.json 一致性检查通过（{0} 个 fixture）' -f $script:FixtureDefs.Count)
}

# ============================================================================
# 9. manifest 输出
# ============================================================================

function New-FixtureRecord {
    param($Ctx, $Def, $Built, $SelfCheckResult, [string]$Status, [string]$BuildError, [string]$SkipReason)
    $record = [ordered]@{
        fixture_id                    = $Def.id
        repo_path                     = $Ctx.ComponentDir
        expected_primary_state        = $Def.primary
        expected_sub_type             = $Def.sub
        expected_flags                = @($Def.flags)
        expected_component_aggregate  = $Def.aggregate
        expected_repos                = $Def.repos
        notes                         = $Def.notes
        status                        = $Status
        skip_reason                   = $SkipReason
        build_error                   = $BuildError
        fixture_dir                   = $Ctx.FixtureDir
        metadata                      = $null
        repos                         = @()
        self_check                    = $null
    }
    if ($Built) {
        $record.metadata = [ordered]@{
            gitea_simulation     = [ordered]@{
                remote_repo_absent = $Built.meta.remote_gone
                ttl_expired        = $Built.meta.ttl_expired
                job_state          = $Built.meta.job_state
            }
            expected_origin_urls = $Built.meta.expected_origin_urls
        }
    }
    if ($SelfCheckResult) {
        $record.repos = @($SelfCheckResult.repos)
        $record.self_check = $SelfCheckResult.self_check
    }
    elseif ($Status -eq 'skipped') {
        $record.self_check = [ordered]@{
            ok          = $true
            skipped     = $true
            skip_reason = $SkipReason
            mismatches  = @()
            commands    = @($script:StandardCheckCommands)
        }
    }
    return $record
}

function Write-Manifests {
    param($RootPath, $FixtureRecords, $Summary, $Environment, $Prereq, $OnlyPatterns)
    $manifest = [ordered]@{
        schema_version = '1.0'
        generated_at   = (Get-Date).ToUniversalTime().ToString('o')
        generator      = [ordered]@{
            name    = 'state-matrix.ps1'
            version = $script:ScriptVersion
            srs     = 'reqs/requirements.md v0.2.1 §7.1–7.6 + §8.1 + 附录 C/附录 D（C-03 聚合顺序按 §7.4）'
            review  = 'reqs/review/02-git-domain.md §2 表 A/表 B/表 C + §3 伪代码'
        }
        environment    = $Environment
        prerequisite   = [ordered]@{
            git_version   = $Prereq.git_version
            lfs_available = $Prereq.lfs_available
            lfs_version   = $Prereq.lfs_version
        }
        root           = $RootPath
        component      = $Component
        only           = @($OnlyPatterns)
        skip_lfs       = [bool]$SkipLfs
        expected_json  = (Join-Path $PSScriptRoot 'expected.json')
        summary        = $Summary
        fixtures       = @($FixtureRecords)
    }

    $manifestPath = Join-Path $RootPath 'manifest.json'
    Write-Utf8NoBom -Path $manifestPath -Text ($manifest | ConvertTo-Json -Depth 24)

    foreach ($record in $FixtureRecords) {
        $single = [ordered]@{
            schema_version = $manifest.schema_version
            generated_at   = $manifest.generated_at
            generator      = $manifest.generator
            root           = $RootPath
            component      = $Component
            fixture        = $record
        }
        Write-Utf8NoBom -Path (Join-Path $record.fixture_dir 'manifest.json') -Text ($single | ConvertTo-Json -Depth 24)
    }
    return $manifestPath
}

# ============================================================================
# 10. 主流程
# ============================================================================

$exitCode = $script:ExitCodeOk
$totalTimer = [System.Diagnostics.Stopwatch]::StartNew()

try {
    # -Clean 只清理，不需要 git
    if ($Clean) {
        $cleanRoot = if ($Root) { $Root } else { $null }
        Invoke-Cleanup -RootParam $cleanRoot | Out-Null
        Write-Ok '清理完成。'
        exit $script:ExitCodeOk
    }

    # 前置条件
    $prereq = Assert-Prerequisites
    Write-Info ('git 版本：{0}' -f $prereq.git_version)
    if ($prereq.lfs_available) { Write-Info ('git-lfs：{0}' -f $prereq.lfs_version) }
    else { Write-Warn2 'git-lfs 不可用：F-LFS-MISSING 将被跳过并记录原因。' }

    # expected.json 静态副本一致性
    Assert-ExpectedJsonConsistency

    # 目录安全
    $execRoot = Resolve-ExecutionRoot -RootParam $Root
    Initialize-ExecutionRoot -RootPath $execRoot
    Initialize-GitEnvironment -RootPath $execRoot
    Write-Info ('输出根目录：{0}' -f $execRoot)

    # 选择 fixture
    $selectedDefs = @()
    if ($Only -and $Only.Count -gt 0) {
        $patterns = @()
        foreach ($o in $Only) {
            foreach ($piece in ($o -split ',')) {
                $trimmedPiece = $piece.Trim()
                if ($trimmedPiece) { $patterns += $trimmedPiece }
            }
        }
        foreach ($def in $script:FixtureDefs) {
            foreach ($p in $patterns) {
                if ($def.id -like $p -or $def.id -eq $p) { $selectedDefs += $def; break }
            }
        }
        if ($selectedDefs.Count -eq 0) {
            throw ('-Only 未匹配任何 fixture：{0}' -f ($patterns -join ', '))
        }
    }
    else {
        $selectedDefs = @($script:FixtureDefs)
    }
    Write-Info ('本次生成 fixture 数量：{0}' -f $selectedDefs.Count)

    $skipLfsEffective = [bool]$SkipLfs -or (-not $prereq.lfs_available)
    $skipLfsReason = $null
    if ($SkipLfs) { $skipLfsReason = '用户显式指定 -SkipLfs' }
    elseif (-not $prereq.lfs_available) { $skipLfsReason = '环境未安装 git-lfs（git lfs version 失败）' }

    $records = @()
    $passed = 0
    $failed = 0
    $skipped = 0
    $buildFailed = 0
    $selfCheckFailed = 0
    $envInfo = [ordered]@{
        pwsh          = $PSVersionTable.PSVersion.ToString()
        os            = [System.Runtime.InteropServices.RuntimeInformation]::OSDescription
        host          = $env:COMPUTERNAME
        git_version   = $prereq.git_version
        lfs_available = $prereq.lfs_available
        lfs_version   = $prereq.lfs_version
    }

    foreach ($def in $selectedDefs) {
        $fixtureTimer = [System.Diagnostics.Stopwatch]::StartNew()
        $fixtureDir = Join-Path $execRoot $def.id
        # 幂等：先清理同名 fixture 目录再重建
        if (Test-Path -LiteralPath $fixtureDir) { Remove-Item -Recurse -Force -LiteralPath $fixtureDir }
        New-Item -ItemType Directory -Force -Path $fixtureDir | Out-Null
        $ctx = [ordered]@{
            Root         = $execRoot
            FixtureId    = $def.id
            FixtureDir   = $fixtureDir
            Component    = $Component
            ComponentDir = (Join-Path $fixtureDir ('workspaces\' + $Component))
            OriginsDir   = (Join-Path $fixtureDir 'origins')
            TmpDir       = (Join-Path $fixtureDir 'tmp')
        }
        New-Item -ItemType Directory -Force -Path $ctx.ComponentDir | Out-Null
        New-Item -ItemType Directory -Force -Path $ctx.OriginsDir | Out-Null
        New-Item -ItemType Directory -Force -Path $ctx.TmpDir | Out-Null

        Write-Host ''
        Write-Info ('▶ 生成 {0} …' -f $def.id)

        if ($def.id -eq 'F-LFS-MISSING' -and $skipLfsEffective) {
            $record = New-FixtureRecord -Ctx $ctx -Def $def -Built $null -SelfCheckResult $null -Status 'skipped' -BuildError $null -SkipReason $skipLfsReason
            $record.self_check.skip_reason = $skipLfsReason
            $records += $record
            $skipped++
            Write-Warn2 ('跳过 {0}：{1}' -f $def.id, $skipLfsReason)
            continue
        }

        try {
            $built = Invoke-FixtureBuild -Ctx $ctx -Def $def
            # 构造用的临时目录（seed/writer）不进入交付物
            if (Test-Path -LiteralPath $ctx.TmpDir) { Remove-Item -Recurse -Force -LiteralPath $ctx.TmpDir }
            $selfCheckResult = Invoke-FixtureSelfCheck -Ctx $ctx -Def $def -Built $built
            $record = New-FixtureRecord -Ctx $ctx -Def $def -Built $built -SelfCheckResult $selfCheckResult -Status 'built' -BuildError $null -SkipReason $null
            if ($selfCheckResult.self_check.ok) {
                $record.status = 'passed'
                $passed++
                Write-Ok ('{0} 自校验通过（primary={1} / aggregate={2}，{3:N2}s）' -f $def.id, $record.self_check.representative_primary_state, $record.self_check.derived_component_aggregate, $fixtureTimer.Elapsed.TotalSeconds)
                foreach ($rc in $record.repos) {
                    Write-Host ('         {0,-7} {1,-16} flags=[{2}]' -f $rc.repo_type, $rc.self_check.derived_primary_state, (@($rc.self_check.derived_flags) -join ',')) -ForegroundColor DarkGray
                }
            }
            else {
                $record.status = 'failed_self_check'
                $selfCheckFailed++
                Write-Fail ('{0} 自校验失败：' -f $def.id)
                foreach ($m in $record.self_check.mismatches) { Write-Host ('         - ' + $m) -ForegroundColor Red }
            }
            $records += $record
        }
        catch {
            $errText = ($_.Exception.GetType().FullName + ': ' + $_.Exception.Message + "`n" + $_.ScriptStackTrace)
            $record = New-FixtureRecord -Ctx $ctx -Def $def -Built $null -SelfCheckResult $null -Status 'failed_build' -BuildError $errText -SkipReason $null
            $records += $record
            $buildFailed++
            Write-Fail ('{0} 构造失败：{1}' -f $def.id, $errText)
        }
    }

    $totalTimer.Stop()
    $summary = [ordered]@{
        fixture_selected   = $selectedDefs.Count
        fixture_total      = $records.Count
        passed             = $passed
        failed_build       = $buildFailed
        failed_self_check  = $selfCheckFailed
        skipped            = $skipped
        elapsed_seconds    = [math]::Round($totalTimer.Elapsed.TotalSeconds, 2)
    }

    $manifestPath = Write-Manifests -RootPath $execRoot -FixtureRecords $records -Summary $summary -Environment $envInfo -Prereq $prereq -OnlyPatterns $Only

    Write-Host ''
    Write-Host '================ 汇总 ================' -ForegroundColor Cyan
    Write-Host ('fixture 总数 : {0}' -f $records.Count)
    Write-Host ('通过         : {0}' -f $passed) -ForegroundColor Green
    Write-Host ('失败(构造)   : {0}' -f $buildFailed) -ForegroundColor Red
    Write-Host ('失败(自校验) : {0}' -f $selfCheckFailed) -ForegroundColor Red
    Write-Host ('跳过         : {0}' -f $skipped) -ForegroundColor Yellow
    Write-Host ('耗时         : {0:N2}s' -f $totalTimer.Elapsed.TotalSeconds)
    Write-Host ('manifest     : {0}' -f $manifestPath)
    Write-Host ('expected     : {0}' -f (Join-Path $PSScriptRoot 'expected.json'))

    if ($buildFailed -gt 0) { $exitCode = $script:ExitCodeBuild }
    elseif ($selfCheckFailed -gt 0) { $exitCode = $script:ExitCodeSelfCheck }
    else { $exitCode = $script:ExitCodeOk }
}
catch {
    $message = $_.Exception.Message
    Write-Host ''
    Write-Fail ('执行失败：{0}' -f $message)
    # 按异常来源选择退出码
    if ($message -match 'expected\.json|缺少期望值静态副本') { $exitCode = $script:ExitCodeSelfCheck }
    elseif ($message -match '拒绝|Root|-Only|盘符|用户主目录|系统目录') { $exitCode = $script:ExitCodeParams }
    else { $exitCode = $script:ExitCodePrereq }
}
finally {
    if ($totalTimer.IsRunning) { $totalTimer.Stop() }
}

exit $exitCode
