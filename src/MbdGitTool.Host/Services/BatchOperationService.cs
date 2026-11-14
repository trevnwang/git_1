using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MbdGitTool.Core.Contracts;
using MbdGitTool.Core.Domain;
using MbdGitTool.Core.Security;

namespace MbdGitTool.Host.Services;

/// <summary>批量 item 执行结果（JobEngine 映射为 <c>job_item</c> 状态 + 审计行）。</summary>
public sealed record BatchItemResult(
    string State,
    string? ErrorCode = null,
    string? Message = null,
    string? PreHead = null,
    string? PostHead = null,
    string? BackupRef = null);

/// <summary>
/// M2b 批量“自定义 kind”执行器（SRS §9.7 FR-OP-01…07/11）：SYNC / CLONE / DELETE / RESTORE / CREATE / TAG。
/// <para><b>安全</b>：路径一律经 <see cref="PathPolicy"/> 收口在工作区内；所有 git 参数走 ArgumentList；
/// CLONE 的 PAT 仅经 URL 作用域 <c>http.&lt;base&gt;.extraHeader</c> 环境注入（不进 argv/日志/响应）；</para>
/// <para>DELETE 只做 <c>Directory.Move</c> 到 <c>&lt;workspace&gt;\.trash\&lt;UTC yyyyMMddHHmmss&gt;\&lt;repoName&gt;</c>
/// （跨卷回退复制+删除），禁止直接递归删除工作区仓库；SYNC 必须先成功创建
/// <c>refs/backup/sync/&lt;stamp&gt;/&lt;repoType&gt;</c> 再 <c>reset --hard</c>（备份失败则不 reset）。</para>
/// <para>方法不抛非取消异常：所有失败映射为 <see cref="BatchItemResult"/>；取消时抛
/// <see cref="OperationCanceledException"/>（由 JobEngine 记 CANCELED/INTERRUPTED）。</para>
/// </summary>
public sealed partial class BatchOperationService
{
    /// <summary>SYNC 自动 stash 的固定 message（便于用户按 message 找回）。</summary>
    public const string AutoStashMessage = "MBD: auto stash before SYNC";

    /// <summary>备份 ref / .trash 保留天数（SRS FR-OP-06/11 默认 30 天；清理任务在 JobEngine 启动时执行）。</summary>
    public const int RetentionDays = 30;

    /// <summary>软删除目录名（SRS D-017：统一落点 <c>&lt;workspace&gt;\.trash</c>，扫描器必须排除）。</summary>
    public const string TrashDirectoryName = ".trash";

    /// <summary>备份 ref 前缀（SRS FR-OP-05/11：<c>refs/backup/*</c>，保留 ≥30 天）。</summary>
    public const string BackupRefPrefix = "refs/backup/sync/";

    /// <summary>stderr 摘要最大字符数（日志/审计不回显完整 git 输出）。</summary>
    private const int MaxErrorSummaryChars = 500;

    /// <summary>§7.2 主状态 DIVERGED（SYNC 默认阻断 + 输入组件 ID 后允许，§7.6/AC-OP-16）。</summary>
    private const string RepoStateDiverged = "DIVERGED";

    private static readonly TimeSpan NetworkTimeout = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan WriteTimeout = TimeSpan.FromSeconds(60);

    private readonly AppConfig _config;
    private readonly RepoActionService _actions;
    private readonly GitCli _git;
    private readonly PathPolicy? _paths;

    public BatchOperationService(
        AppConfig config,
        RepoActionService actions,
        GitCli? git = null,
        PathPolicy? paths = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _actions = actions ?? throw new ArgumentNullException(nameof(actions));
        _git = git ?? new GitCli();
        _paths = paths;
    }

    /// <summary>工作区根（<see cref="PathPolicy"/> 规范化；未配置时 null）。</summary>
    public string? WorkspaceRoot => _paths?.Root;

    // ---------------------------------------------------------------- SYNC

    /// <summary>
    /// 受控 SYNC（FR-OP-05 / §7.6 SYNC 强覆盖列）：
    /// <para><b>逐仓预检</b>（只读，不做任何写）：本地仓/分支/upstream/状态矩阵（仅 BEHIND/DIRTY/SYNCED 允许；
    /// DIVERGED 默认阻断，需 <c>args.confirmText==componentId</c> 强确认；AHEAD/UNMERGED/IN_PROGRESS/INVALID/
    /// MISSING/EMPTY/DETACHED/NO_UPSTREAM/UNKNOWN 拒绝）；</para>
    /// <para><b>执行顺序</b>：CAS（快照 HEAD vs 磁盘 HEAD）→ <c>fetch --prune</c> → 远端分支存在 → fetch 后 HEAD
    /// 未变（外部改动零写拒绝）→ 备份 <c>refs/backup/sync/&lt;stamp&gt;/&lt;repoType&gt;</c> → dirty 时 <c>stash -u</c> →
    /// <c>reset --hard origin/&lt;branch&gt;</c>；任一步失败即停止且不 reset。备份 ref 创建失败 = item FAILED。</para>
    /// </summary>
    public async Task<BatchItemResult> SyncAsync(
        RepoRef? repo,
        string componentId,
        string repoType,
        StoredJobArguments arguments,
        CancellationToken cancellationToken)
    {
        if (repo is null)
        {
            return Skip(BridgeErrorCodes.NotFound, "仓库不在当前快照，跳过 SYNC");
        }

        if (!repo.Repo.IsLocal || string.IsNullOrWhiteSpace(repo.Repo.LocalPath))
        {
            return Skip(BridgeErrorCodes.RepoNotLocal, $"仓库未克隆：{RepoNameFor(repo, repo.ComponentId, repoType)}，跳过 SYNC");
        }

        var localPath = PathPolicy.TryCanonicalize(repo.Repo.LocalPath);
        if (localPath is null || !Directory.Exists(localPath))
        {
            return Skip(BridgeErrorCodes.RepoNotLocal, "本地仓库路径不存在，跳过 SYNC");
        }

        var pathFailure = RequireWithinWorkspace(localPath);
        if (pathFailure is not null)
        {
            return Failed(BridgeErrorCodes.PathOutsideRoot, pathFailure);
        }

        // 1) 状态矩阵预检（§7.6 SYNC 强覆盖列；矩阵是单源，不另写第二套）。
        var state = NormalizeState(repo.Repo.State);
        if (!SyncMatrixAllowed(state, out var blockedReason))
        {
            return Skip(
                BridgeErrorCodes.ActionNotAllowed,
                $"当前状态 {state} 不允许 SYNC：{blockedReason}");
        }

        var requiresTypedConfirm = state == RepoStateDiverged;
        if (requiresTypedConfirm && !MatchesTypedConfirm(arguments.ConfirmText, componentId))
        {
            return Skip(
                BridgeErrorCodes.ConfirmRequired,
                $"DIVERGED 下 SYNC 默认阻断（将丢弃本地未推送提交）；请在确认框输入组件 ID"
                + $"「{componentId.Trim()}」后重试（safety.requireTypedConfirm：精确匹配、区分大小写、忽略首尾空格）");
        }

        if (IsBusyOrInProgress(repo))
        {
            return Skip(BridgeErrorCodes.RepoBusy, "仓库存在进行中操作或外部 git 锁，跳过 SYNC");
        }

        var branch = repo.Repo.Branch;
        if (string.IsNullOrWhiteSpace(branch))
        {
            return Failed(BridgeErrorCodes.ActionNotAllowed, "SYNC 需要当前处于分支状态（detached HEAD 不支持）");
        }

        if (!IsSafeRefSegment(branch))
        {
            return Failed(BridgeErrorCodes.InvalidArgument, "当前分支名非法，拒绝 SYNC");
        }

        // 2) CAS：快照 HEAD（job 开始时 read-through 刷新）必须等于磁盘 HEAD，否则外部改动 → 零写拒绝。
        var preHead = await ReadHeadAsync(localPath, cancellationToken).ConfigureAwait(false);
        if (!SnapshotHeadMatches(repo, preHead))
        {
            return Failed(
                BridgeErrorCodes.ConcurrentChange,
                "快照 HEAD 与仓库当前 HEAD 不一致（执行前存在外部改动）；已拒绝 SYNC，工作区未改动");
        }

        // 3) 网络凭据作用域校验（fail-closed：不匹配/配置非法时不启动网络 git）。
        var (credentialFailure, credentialEnvironment) =
            await _actions.PrepareCredentialEnvironmentAsync(localPath, "SYNC", cancellationToken)
                .ConfigureAwait(false);
        if (credentialFailure is not null)
        {
            return Failed(
                credentialFailure.Error?.Code ?? BridgeErrorCodes.GitFailed,
                credentialFailure.Error?.Message ?? "SYNC 凭据作用域校验失败");
        }

        // 4) fetch --prune。
        var fetch = await _git
            .RunAsync(localPath, new[] { "fetch", "--prune", "origin" }, cancellationToken, NetworkTimeout, credentialEnvironment)
            .ConfigureAwait(false);
        if (!fetch.Success)
        {
            return MapGitFailure("SYNC", fetch, cancellationToken);
        }

        // 5) 远端同名分支必须存在（禁止 reset 到不存在的 ref）。
        var remoteRef = "refs/remotes/origin/" + branch;
        var verify = await _git
            .RunAsync(localPath, new[] { "rev-parse", "--verify", "--quiet", remoteRef }, cancellationToken)
            .ConfigureAwait(false);
        if (!verify.Success || verify.TrimmedStdout.Length == 0)
        {
            return Failed(
                BridgeErrorCodes.RefNotFound,
                $"远端不存在分支 origin/{branch}，已拒绝 reset");
        }

        // 6) 外部改动 CAS：fetch 不应改 HEAD；若 HEAD 已变，在写备份 ref 之前拒绝（零写）。
        var afterFetchHead = await ReadHeadAsync(localPath, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(preHead, afterFetchHead, StringComparison.Ordinal))
        {
            return Failed(
                BridgeErrorCodes.ConcurrentChange,
                "执行期间 HEAD 被外部改动（fetch 前后 HEAD 不一致）；已拒绝 SYNC 且未写备份 ref");
        }

        // 7) 备份 ref 必须先成功（backup 失败 → item FAILED，绝不 reset）。
        preHead = afterFetchHead;
        if (preHead is null)
        {
            return Failed(
                BridgeErrorCodes.GitFailed,
                "仓库无 HEAD（空仓），无法创建备份 ref；已拒绝 reset");
        }

        var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        var backupRef = BackupRefPrefix
            + stamp
            + "/"
            + (string.IsNullOrWhiteSpace(repo.Repo.RepoType) ? repoType : repo.Repo.RepoType).ToUpperInvariant();
        var updateRef = await _git
            .RunAsync(localPath, new[] { "update-ref", backupRef, preHead }, cancellationToken, WriteTimeout)
            .ConfigureAwait(false);
        if (!updateRef.Success)
        {
            return Failed(
                BridgeErrorCodes.GitFailed,
                "创建备份 ref 失败（" + backupRef + "）；已拒绝 reset，工作区未改动");
        }

        // 8) dirty（含未跟踪）先 stash -u；stash 失败则不 reset（避免本地改动丢失）。
        string? stashRef = null;
        var status = await _git
            .RunAsync(localPath, new[] { "status", "--porcelain" }, cancellationToken)
            .ConfigureAwait(false);
        if (!status.Success)
        {
            return MapGitFailure("SYNC", status, cancellationToken);
        }

        if (status.Stdout.Trim().Length > 0)
        {
            var stash = await _git
                .RunAsync(
                    localPath,
                    new[] { "stash", "push", "-u", "-m", AutoStashMessage },
                    cancellationToken,
                    WriteTimeout)
                .ConfigureAwait(false);
            if (!stash.Success)
            {
                return Failed(
                    BridgeErrorCodes.GitFailed,
                    "工作区存在本地改动且 stash -u 失败；已拒绝 reset（备份 ref " + backupRef + " 保留）");
            }

            stashRef = "stash@{0}";
        }

        // 9) reset --hard（不使用 git pull；禁止隐式 merge）。
        var reset = await _git
            .RunAsync(localPath, new[] { "reset", "--hard", "origin/" + branch }, cancellationToken, WriteTimeout)
            .ConfigureAwait(false);
        if (!reset.Success)
        {
            return MapGitFailure("SYNC", reset, cancellationToken);
        }

        var postHead = await ReadHeadAsync(localPath, cancellationToken).ConfigureAwait(false) ?? preHead;
        var message = $"SYNC 完成（{branch} → origin/{branch}）；备份 {backupRef}"
            + (stashRef is null ? string.Empty : "；本地改动已 stash（" + stashRef + "）");
        HostLog.Info($"job SYNC {repo.ComponentId}/{RepoTypeOf(repo, repoType)} ok=true backup={backupRef}");
        return new BatchItemResult(JobItemStates.Ok, null, message, preHead, postHead, backupRef);
    }

    // ---------------------------------------------------------------- CLONE

    /// <summary>
    /// 缺失仓克隆（FR-OP-01）：<c>git clone &lt;baseUrl&gt;/&lt;org&gt;/&lt;repoName&gt;.git &lt;workspace&gt;/&lt;repoName&gt;</c>；
    /// 目标已存在 → SKIPPED（幂等，不覆盖）；PAT 只经 URL 作用域 header 注入（不进 argv）。
    /// </summary>
    public async Task<BatchItemResult> CloneAsync(
        RepoRef? repo,
        string componentId,
        string repoType,
        CancellationToken cancellationToken)
    {
        if (_paths is null)
        {
            return Failed(BridgeErrorCodes.WorkspaceUnset, "未配置工作区根目录，无法 CLONE");
        }

        var repoName = RepoNameFor(repo, componentId, repoType);
        if (!IsSafeDirectoryName(repoName))
        {
            return Failed(
                BridgeErrorCodes.PathInvalid,
                "仓库名非法（必须是工作区内单一路径段），拒绝 CLONE：" + Truncate(repoName));
        }

        if (!_paths.TryResolveWorkspacePath(repoName, out var targetPath, out var pathError))
        {
            return Failed(
                pathError?.Code ?? BridgeErrorCodes.PathInvalid,
                pathError?.Message ?? "CLONE 目标路径非法");
        }

        if (Directory.Exists(targetPath))
        {
            return Skip(
                BridgeErrorCodes.ActionNotAllowed,
                $"目标目录已存在：{repoName}，跳过 CLONE（不覆盖）");
        }

        var baseUrl = _config.GiteaBaseUrl?.Trim().TrimEnd('/');
        var org = _config.GiteaOrg?.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(org))
        {
            return Failed(
                BridgeErrorCodes.GiteaConfigInvalid,
                "未配置 Gitea baseUrl/org，无法构造 CLONE 地址");
        }

        if (!IsSafeRefSegment(org) || org.Contains("..", StringComparison.Ordinal))
        {
            return Failed(BridgeErrorCodes.GiteaConfigInvalid, "Gitea org 非法，拒绝 CLONE");
        }

        // 凭据 fail-closed：配置非法时不启动任何网络 git；header 仅对 canonical base 生效。
        var credentialFailure = _actions.TryBuildCloneCredentialEnvironment(out var credentialEnvironment);
        if (credentialFailure is not null)
        {
            return Failed(
                credentialFailure.Error?.Code ?? BridgeErrorCodes.GiteaConfigInvalid,
                credentialFailure.Error?.Message ?? "CLONE 凭据配置校验失败");
        }

        var url = baseUrl + "/" + org + "/" + repoName + ".git";
        var clone = await _git
            .RunAsync(_paths.Root, new[] { "clone", "--", url, targetPath }, cancellationToken, NetworkTimeout, credentialEnvironment)
            .ConfigureAwait(false);
        if (!clone.Success)
        {
            return MapGitFailure("CLONE", clone, cancellationToken);
        }

        var postHead = await ReadHeadAsync(targetPath, cancellationToken).ConfigureAwait(false);
        HostLog.Info($"job CLONE {componentId}/{repoType} ok=true path={repoName}");
        return new BatchItemResult(
            JobItemStates.Ok,
            null,
            $"CLONE 完成：{repoName}",
            null,
            postHead,
            null);
    }

    // ---------------------------------------------------------------- DELETE

    /// <summary>
    /// 本地软删除（FR-OP-06 / AC-OP-21）：<b>必须</b> <c>args.confirm=true</c>（受理门禁）且
    /// <c>args.confirmText == componentId</c>（强确认，精确匹配/区分大小写/忽略首尾空格）；
    /// 逐仓预检 dirty/untracked/AHEAD/stash/IN_PROGRESS/INVALID/MISSING → 默认拒绝（<b>不提供 force 绕过</b>，
    /// 提示先提交/stash）；只把工作区根的直接子目录 <c>Directory.Move</c> 到
    /// <c>&lt;workspace&gt;\.trash\&lt;stamp&gt;\&lt;repoName&gt;</c>，远端零写。
    /// </summary>
    public async Task<BatchItemResult> DeleteAsync(
        RepoRef? repo,
        string componentId,
        string repoType,
        StoredJobArguments arguments,
        CancellationToken cancellationToken)
    {
        if (_paths is null)
        {
            return Failed(BridgeErrorCodes.WorkspaceUnset, "未配置工作区根目录，无法 DELETE");
        }

        if (repo is null)
        {
            return Skip(BridgeErrorCodes.NotFound, "仓库不在当前快照，跳过 DELETE");
        }

        if (!repo.Repo.IsLocal || string.IsNullOrWhiteSpace(repo.Repo.LocalPath))
        {
            return Skip(BridgeErrorCodes.RepoNotLocal, $"仓库未克隆：{RepoNameFor(repo, repo.ComponentId, repoType)}，跳过 DELETE");
        }

        var localPath = PathPolicy.TryCanonicalize(repo.Repo.LocalPath);
        if (localPath is null || !Directory.Exists(localPath))
        {
            return Skip(BridgeErrorCodes.RepoNotLocal, "本地仓库路径不存在，跳过 DELETE");
        }

        // 0) 强确认：DELETE 必须输入组件 ID（与 args.confirm=true 相互独立，防绕过 UI 直接调桥）。
        if (!MatchesTypedConfirm(arguments.ConfirmText, componentId))
        {
            return Failed(
                BridgeErrorCodes.ConfirmRequired,
                $"DELETE 强确认失败：请在确认框输入组件 ID「{componentId.Trim()}」"
                + "（精确匹配、区分大小写、忽略首尾空格）后重试");
        }

        // 只允许删除工作区根的直接子目录（禁止删除根 / 越界路径 / reparse point）。
        if (!string.Equals(Path.GetDirectoryName(localPath), _paths.Root, StringComparison.OrdinalIgnoreCase))
        {
            return Failed(
                BridgeErrorCodes.PathOutsideRoot,
                "DELETE 目标必须是工作区根的直接子目录；已拒绝");
        }

        if (PathPolicy.IsReparsePoint(localPath))
        {
            return Failed(BridgeErrorCodes.ReparsePoint, "DELETE 目标是 reparse point（符号链接/联接），已拒绝");
        }

        // 1) 状态矩阵预检（§7.6 Delete 列：允许 ⚠，但 MISSING/INVALID/IN_PROGRESS/UNKNOWN 拒绝）。
        var state = NormalizeState(repo.Repo.State);
        if (!DeleteMatrixAllowed(state, out var blockedReason))
        {
            return Skip(
                BridgeErrorCodes.ActionNotAllowed,
                $"当前状态 {state} 不允许 DELETE：{blockedReason}");
        }

        if (IsBusyOrInProgress(repo))
        {
            return Failed(BridgeErrorCodes.RepoBusy, "仓库存在进行中操作或外部 git 锁，拒绝 DELETE");
        }

        var repoName = Path.GetFileName(localPath);
        if (!IsSafeDirectoryName(repoName))
        {
            return Failed(BridgeErrorCodes.PathInvalid, "仓库目录名非法，拒绝 DELETE");
        }

        // 2) 工作区 dirty/untracked 拒绝（FR-OP-06 四阻断；先提交/stash 后再删除，不做 force 覆盖）。
        var status = await _git
            .RunAsync(localPath, new[] { "status", "--porcelain" }, cancellationToken)
            .ConfigureAwait(false);
        if (!status.Success)
        {
            return MapGitFailure("DELETE", status, cancellationToken);
        }

        if (status.Stdout.Trim().Length > 0)
        {
            return Failed(
                BridgeErrorCodes.DirtyWorktree,
                "工作区存在未提交改动或未跟踪文件；请先提交或 stash 后再删除（DELETE 不提供强制覆盖）");
        }

        // 3) HEAD 快照（unborn/EMPTY 时为 null：没有提交可丢失，跳过未推送/stash 判定）。
        var preHead = await ReadHeadAsync(localPath, cancellationToken).ConfigureAwait(false);
        if (preHead is not null)
        {
            // 3a) 未推送提交拒绝（AHEAD；无 upstream 且已有提交也视为“无法确认已推送”）。
            var unpushed = await CountUnpushedAsync(localPath, cancellationToken).ConfigureAwait(false);
            if (unpushed is > 0)
            {
                return Failed(
                    BridgeErrorCodes.ActionNotAllowed,
                    $"存在 {unpushed} 个未推送提交；请先 push 后再删除（DELETE 不提供强制覆盖）");
            }

            if (unpushed is null)
            {
                return Failed(
                    BridgeErrorCodes.ActionNotAllowed,
                    "当前分支没有可用的 upstream，无法确认提交已推送；请先发布分支/设置 upstream 后再删除");
            }

            // 3b) stash 记录拒绝（FR-OP-06 四阻断之一）。
            if (await HasStashAsync(localPath, cancellationToken).ConfigureAwait(false))
            {
                return Failed(
                    BridgeErrorCodes.ActionNotAllowed,
                    "仓库存在 stash 记录；请先处理 stash 后再删除（DELETE 不提供强制覆盖）");
            }
        }

        // 4) CAS：快照 HEAD 必须等于磁盘 HEAD（外部改动 → 零写拒绝）。
        if (!SnapshotHeadMatches(repo, preHead))
        {
            return Failed(
                BridgeErrorCodes.ConcurrentChange,
                "快照 HEAD 与仓库当前 HEAD 不一致（执行前存在外部改动）；已拒绝 DELETE，目录保持不变");
        }

        // 目标 trash 路径：全部经 canonical + IsWithin 收口，禁止逃逸。
        var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        var trashRoot = PathPolicy.TryCanonicalize(
            Path.Combine(_paths.Root, TrashDirectoryName, stamp));
        if (trashRoot is null
            || !_paths.IsWithin(_paths.Root, trashRoot)
            || !_paths.IsWithin(_paths.Root, localPath))
        {
            return Failed(BridgeErrorCodes.PathOutsideRoot, "软删除目标路径越出工作区，已拒绝");
        }

        var reparse = _paths.CheckReparseChain(trashRoot);
        if (reparse.HasReparse)
        {
            return Failed(BridgeErrorCodes.ReparsePoint, ".trash 路径含 reparse point，已拒绝 DELETE");
        }

        var destination = Path.Combine(trashRoot, repoName);
        if (Directory.Exists(destination) || File.Exists(destination))
        {
            destination += "-" + Guid.NewGuid().ToString("N")[..8];
        }

        try
        {
            Directory.CreateDirectory(trashRoot);
            MoveDirectory(localPath, destination);
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or ArgumentException)
        {
            return Failed(
                BridgeErrorCodes.IoError,
                $"软删除失败（{ex.GetType().Name}）：{repoName}；源仓库保持不变");
        }

        var relativeTrash = TrashDirectoryName + "/" + stamp + "/" + Path.GetFileName(destination);
        var message = $"已软删除到 .trash（保留 {RetentionDays} 天，可 RESTORE）：{relativeTrash}；远端仓库未改动";
        HostLog.Info($"job DELETE {repo.ComponentId}/{RepoTypeOf(repo, repoType)} ok=true trash={relativeTrash}");
        return new BatchItemResult(JobItemStates.Ok, null, message, preHead, preHead, relativeTrash);
    }

    // ---------------------------------------------------------------- dryRun 预检（只读、零写）

    /// <summary>
    /// dryRun 逐仓预检（AC-OP-16① / P1-6）：只做只读探测（状态矩阵/本地仓/分支/upstream/dirty/untracked/
    /// unpushed/stash/目标路径），返回 <c>state=PREVIEW</c> 的 item；<b>不启动任何 git 写/网络写</b>
    /// （不 fetch、不建 ref、不 stash、不 reset、不移动目录），供任务中心“预览”展示。
    /// </summary>
    public async Task<BatchItemResult> PreviewAsync(
        string kind,
        RepoRef? repo,
        string componentId,
        string repoType,
        StoredJobArguments arguments,
        CancellationToken cancellationToken)
    {
        var notes = new List<string>();
        var repoName = RepoNameFor(repo, componentId, repoType);

        if (repo is null)
        {
            if (kind is "CLONE")
            {
                return await PreviewCloneAsync(componentId, repoType, repoName, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (kind is "RESTORE")
            {
                return PreviewRestore(repoName, arguments);
            }

            return PreviewResult($"{repoName}：仓库不在当前快照，实际执行将 SKIPPED（E_NOT_FOUND）");
        }

        var state = NormalizeState(repo.Repo.State);
        notes.Add($"状态={state}");

        if (kind is not "CLONE" && (!repo.Repo.IsLocal || string.IsNullOrWhiteSpace(repo.Repo.LocalPath)))
        {
            return PreviewResult($"{repoName}：仓库未克隆，实际执行将 SKIPPED（E_REPO_NOT_LOCAL）");
        }

        var localPath = repo.Repo.IsLocal ? PathPolicy.TryCanonicalize(repo.Repo.LocalPath) : null;
        if (localPath is not null && !Directory.Exists(localPath))
        {
            return PreviewResult($"{repoName}：本地仓库路径不存在，实际执行将 SKIPPED");
        }

        switch (kind)
        {
            case "SYNC":
            {
                if (!SyncMatrixAllowed(state, out var blocked))
                {
                    return PreviewResult($"{repoName}：状态 {state} 不允许 SYNC（{blocked}），实际执行将 SKIPPED");
                }

                if (state == RepoStateDiverged)
                {
                    notes.Add(MatchesTypedConfirm(arguments.ConfirmText, componentId)
                        ? "DIVERGED 已输入组件 ID 强确认：将创建备份 ref 后 reset"
                        : $"DIVERGED 默认阻断：确认框需输入组件 ID「{componentId.Trim()}」后才能执行，未输入时零写");
                }
                else
                {
                    notes.Add("将创建备份 ref 后 reset --hard origin/" + (repo.Repo.Branch ?? "<branch>"));
                }

                if (localPath is not null)
                {
                    var (dirty, untracked) = await CountWorkingTreeAsync(localPath, cancellationToken)
                        .ConfigureAwait(false);
                    if (dirty + untracked > 0)
                    {
                        notes.Add($"本地改动 {dirty} 项 / 未跟踪 {untracked} 项将 stash -u（备份 ref 保留）");
                    }

                    var unpushed = await CountUnpushedAsync(localPath, cancellationToken).ConfigureAwait(false);
                    if (unpushed is not null)
                    {
                        notes.Add($"未推送提交 {unpushed} 个");
                    }
                }

                break;
            }

            case "DELETE":
            {
                if (!DeleteMatrixAllowed(state, out var blocked))
                {
                    return PreviewResult($"{repoName}：状态 {state} 不允许 DELETE（{blocked}），实际执行将 SKIPPED");
                }

                if (!MatchesTypedConfirm(arguments.ConfirmText, componentId))
                {
                    notes.Add($"强确认未通过：需输入组件 ID「{componentId.Trim()}」，未输入时零写（默认阻断）");
                }
                else
                {
                    notes.Add("强确认通过（已输入组件 ID）");
                }

                if (localPath is not null)
                {
                    var (dirty, untracked) = await CountWorkingTreeAsync(localPath, cancellationToken)
                        .ConfigureAwait(false);
                    if (dirty + untracked > 0)
                    {
                        notes.Add($"工作区有改动 {dirty} 项 / 未跟踪 {untracked} 项 → 实际执行将被拒绝（先提交/stash）");
                    }

                    var unpushed = await CountUnpushedAsync(localPath, cancellationToken).ConfigureAwait(false);
                    if (unpushed is > 0)
                    {
                        notes.Add($"未推送提交 {unpushed} 个 → 实际执行将被拒绝（先 push）");
                    }
                    else if (unpushed is null)
                    {
                        notes.Add("无可用 upstream，无法确认提交已推送 → 实际执行将被拒绝");
                    }

                    if (await HasStashAsync(localPath, cancellationToken).ConfigureAwait(false))
                    {
                        notes.Add("存在 stash 记录 → 实际执行将被拒绝");
                    }
                }

                notes.Add($".trash 预览路径：{TrashDirectoryName}/<UTC stamp>/{repoName}（远端零写）");
                break;
            }

            case "CLONE":
                notes.Add("目标已存在，实际执行将 SKIPPED（不覆盖）");
                break;

            case "RESTORE":
            {
                var restorePreview = PreviewRestore(repoName, arguments);
                return restorePreview;
            }

            case "CREATE":
            case "TAG":
            {
                var refType = kind == "CREATE" ? "refs/heads/" : "refs/tags/";
                var refName = kind == "CREATE" ? arguments.Branch : arguments.Tag;
                notes.Add(refName is null
                    ? $"缺少 {(kind == "CREATE" ? "branch" : "tag")} 参数"
                    : $"将写入 {refType}{refName}（已存在则 SKIPPED）");
                break;
            }

            default:
            {
                var availability = _actions.GetAvailability(repo).FirstOrDefault(entry => entry.Action == kind);
                if (availability is null || !availability.MatrixAllowed)
                {
                    return PreviewResult(
                        $"{repoName}：当前状态 {state} 不允许 {kind}"
                        + (availability?.Reason is null ? string.Empty : $"（{availability.Reason}）")
                        + "，实际执行将 SKIPPED");
                }

                notes.Add($"将执行 {kind}（矩阵允许{(availability.RequiresConfirm ? "，需确认" : string.Empty)}）");
                break;
            }
        }

        var preHead = localPath is null ? null : await ReadHeadAsync(localPath, cancellationToken).ConfigureAwait(false);
        return PreviewResult("DRY_RUN 预览（只预检、未写入任何仓库/ref）：" + string.Join("；", notes), preHead);
    }

    private async Task<BatchItemResult> PreviewCloneAsync(
        string componentId,
        string repoType,
        string repoName,
        CancellationToken cancellationToken)
    {
        if (_paths is null)
        {
            return PreviewResult("未配置工作区根目录，实际执行将 FAILED（E_WORKSPACE_UNSET）");
        }

        if (!IsSafeDirectoryName(repoName))
        {
            return PreviewResult($"仓库名非法（{Truncate(repoName)}），实际执行将 FAILED（E_PATH_INVALID）");
        }

        if (!_paths.TryResolveWorkspacePath(repoName, out var targetPath, out _))
        {
            return PreviewResult("CLONE 目标路径非法，实际执行将 FAILED（E_PATH_INVALID）");
        }

        if (Directory.Exists(targetPath))
        {
            return PreviewResult($"{repoName}：目标目录已存在，实际执行将 SKIPPED（不覆盖）");
        }

        var baseUrl = _config.GiteaBaseUrl?.Trim().TrimEnd('/');
        var org = _config.GiteaOrg?.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(org))
        {
            return PreviewResult("未配置 Gitea baseUrl/org，实际执行将 FAILED（E_GITEA_CONFIG_INVALID）");
        }

        var credentialFailure = _actions.TryBuildCloneCredentialEnvironment(out _);
        if (credentialFailure is not null)
        {
            return PreviewResult(
                "CLONE 凭据配置校验失败，实际执行将 FAILED（"
                + (credentialFailure.Error?.Code ?? BridgeErrorCodes.GiteaConfigInvalid) + "）");
        }

        return PreviewResult($"DRY_RUN 预览（未克隆）：将 clone {componentId.Trim()}_{repoType} 到工作区 {repoName}；远端零写");
    }

    private BatchItemResult PreviewRestore(string repoName, StoredJobArguments arguments)
    {
        if (_paths is null)
        {
            return PreviewResult("未配置工作区根目录，实际执行将 FAILED（E_WORKSPACE_UNSET）");
        }

        if (!IsSafeDirectoryName(repoName))
        {
            return PreviewResult($"仓库名非法（{Truncate(repoName)}），实际执行将 FAILED（E_PATH_INVALID）");
        }

        if (!_paths.TryResolveWorkspacePath(repoName, out var targetPath, out _))
        {
            return PreviewResult("RESTORE 目标路径非法，实际执行将 FAILED（E_PATH_INVALID）");
        }

        if (Directory.Exists(targetPath) || File.Exists(targetPath))
        {
            return PreviewResult($"{repoName}：目标路径已存在，实际执行将 SKIPPED（不覆盖）");
        }

        var stamp = arguments.TrashStamp?.Trim();
        bool hasTrash;
        if (stamp is not null)
        {
            if (!TrashStampPattern().IsMatch(stamp))
            {
                return PreviewResult("args.trashStamp 必须是 UTC yyyyMMddHHmmss（14 位数字），实际执行将 FAILED（E_INVALID_ARGUMENT）");
            }

            var candidate = PathPolicy.TryCanonicalize(
                Path.Combine(_paths.Root, TrashDirectoryName, stamp, repoName));
            hasTrash = candidate is not null && Directory.Exists(candidate);
        }
        else
        {
            hasTrash = EnumerateTrashStamps()
                .Any(value => Directory.Exists(Path.Combine(_paths.Root, TrashDirectoryName, value, repoName)));
        }

        return PreviewResult(hasTrash
            ? $"DRY_RUN 预览（未移动）：将从 .trash 恢复 {repoName} 到工作区"
            : $"DRY_RUN 预览：未找到可恢复的 .trash 备份，实际执行将 FAILED（E_NOT_FOUND）");
    }

    private static BatchItemResult PreviewResult(string message, string? preHead = null) =>
        new(JobItemStates.Preview, null, message, preHead, null, null);

    // ---------------------------------------------------------------- RESTORE

    /// <summary>
    /// 从 <c>.trash</c> 恢复（FR-OP-11）：按 repoName 找最近一份（或 <c>args.trashStamp</c> 指定）；
    /// 目标已存在 → SKIPPED（不覆盖）；恢复过程由 JobEngine 写审计。
    /// </summary>
    public async Task<BatchItemResult> RestoreAsync(
        RepoRef? repo,
        string componentId,
        string repoType,
        StoredJobArguments arguments,
        CancellationToken cancellationToken)
    {
        if (_paths is null)
        {
            return Failed(BridgeErrorCodes.WorkspaceUnset, "未配置工作区根目录，无法 RESTORE");
        }

        var repoName = RepoNameFor(repo, componentId, repoType);
        if (!IsSafeDirectoryName(repoName))
        {
            return Failed(BridgeErrorCodes.PathInvalid, "仓库名非法，拒绝 RESTORE");
        }

        if (!_paths.TryResolveWorkspacePath(repoName, out var targetPath, out var pathError))
        {
            return Failed(pathError?.Code ?? BridgeErrorCodes.PathInvalid, pathError?.Message ?? "RESTORE 目标路径非法");
        }

        if (Directory.Exists(targetPath) || File.Exists(targetPath))
        {
            return Skip(
                BridgeErrorCodes.ActionNotAllowed,
                $"目标路径已存在：{repoName}，跳过 RESTORE（不覆盖，需先 DELETE）");
        }

        var stamp = arguments.TrashStamp;
        string? trashEntry = null;
        if (!string.IsNullOrWhiteSpace(stamp))
        {
            if (!TrashStampPattern().IsMatch(stamp.Trim()))
            {
                return Failed(
                    BridgeErrorCodes.InvalidArgument,
                    "args.trashStamp 必须是 UTC yyyyMMddHHmmss（14 位数字）");
            }

            trashEntry = PathPolicy.TryCanonicalize(
                Path.Combine(_paths.Root, TrashDirectoryName, stamp.Trim(), repoName));
            if (trashEntry is null || !_paths.IsWithin(_paths.Root, trashEntry) || !Directory.Exists(trashEntry))
            {
                return Failed(
                    BridgeErrorCodes.NotFound,
                    $"未找到指定 .trash 备份：{stamp.Trim()}/{repoName}");
            }
        }
        else
        {
            foreach (var directory in EnumerateTrashStamps())
            {
                var candidate = Path.Combine(_paths.Root, TrashDirectoryName, directory, repoName);
                if (Directory.Exists(candidate))
                {
                    trashEntry = PathPolicy.TryCanonicalize(candidate);
                    break;
                }
            }

            if (trashEntry is null)
            {
                return Failed(BridgeErrorCodes.NotFound, $"未找到可恢复的 .trash 备份：{repoName}");
            }
        }

        if (!_paths.IsWithin(_paths.Root, trashEntry))
        {
            return Failed(BridgeErrorCodes.PathOutsideRoot, "RESTORE 源路径越出工作区，已拒绝");
        }

        if (PathPolicy.IsReparsePoint(trashEntry))
        {
            return Failed(BridgeErrorCodes.ReparsePoint, "RESTORE 源是 reparse point，已拒绝");
        }

        try
        {
            MoveDirectory(trashEntry, targetPath);
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or ArgumentException)
        {
            return Failed(
                BridgeErrorCodes.IoError,
                $"RESTORE 失败（{ex.GetType().Name}）：{repoName}；.trash 备份保持不变");
        }

        var postHead = await ReadHeadAsync(targetPath, cancellationToken).ConfigureAwait(false);
        var message = $"RESTORE 完成：{repoName}（来自 .trash/{Path.GetFileName(Path.GetDirectoryName(trashEntry))}）";
        HostLog.Info($"job RESTORE {componentId}/{repoType} ok=true");
        return new BatchItemResult(JobItemStates.Ok, null, message, null, postHead, null);
    }

    // ---------------------------------------------------------------- CREATE / TAG

    /// <summary>CREATE 分支（FR-OP-07）：四仓从当前 HEAD 建 <c>args.branch</c>（已存在 → SKIPPED；不切换 HEAD）。</summary>
    public async Task<BatchItemResult> CreateBranchAsync(
        RepoRef? repo,
        string repoType,
        StoredJobArguments arguments,
        CancellationToken cancellationToken)
    {
        var branch = arguments.Branch!;
        var local = ResolveLocalRepo(repo, repoType, "CREATE");
        if (local.Result is not null)
        {
            return local.Result;
        }

        var existing = await _git
            .RunAsync(local.Path!, new[] { "rev-parse", "--verify", "--quiet", "refs/heads/" + branch }, cancellationToken)
            .ConfigureAwait(false);
        if (existing.Success && existing.TrimmedStdout.Length > 0)
        {
            return Skip(BridgeErrorCodes.ActionNotAllowed, $"分支已存在：{branch}，跳过 CREATE");
        }

        var preHead = await ReadHeadAsync(local.Path!, cancellationToken).ConfigureAwait(false);
        if (preHead is null)
        {
            return Failed(BridgeErrorCodes.ActionNotAllowed, "空仓无提交（unborn HEAD），无法创建分支");
        }

        var create = await _git
            .RunAsync(local.Path!, new[] { "branch", branch, "HEAD" }, cancellationToken, WriteTimeout)
            .ConfigureAwait(false);
        if (!create.Success)
        {
            return MapGitFailure("CREATE", create, cancellationToken);
        }

        return new BatchItemResult(
            JobItemStates.Ok,
            null,
            $"已从当前 HEAD 创建分支 {branch}（未切换）",
            preHead,
            preHead,
            null);
    }

    /// <summary>TAG（FR-OP-04 简化）：四仓给当前 HEAD 打 <c>args.tag</c>（已存在 → SKIPPED；不自动 push）。</summary>
    public async Task<BatchItemResult> TagAsync(
        RepoRef? repo,
        string repoType,
        StoredJobArguments arguments,
        CancellationToken cancellationToken)
    {
        var tag = arguments.Tag!;
        var local = ResolveLocalRepo(repo, repoType, "TAG");
        if (local.Result is not null)
        {
            return local.Result;
        }

        var existing = await _git
            .RunAsync(local.Path!, new[] { "rev-parse", "--verify", "--quiet", "refs/tags/" + tag }, cancellationToken)
            .ConfigureAwait(false);
        if (existing.Success && existing.TrimmedStdout.Length > 0)
        {
            return Skip(BridgeErrorCodes.ActionNotAllowed, $"tag 已存在：{tag}，跳过 TAG");
        }

        var preHead = await ReadHeadAsync(local.Path!, cancellationToken).ConfigureAwait(false);
        if (preHead is null)
        {
            return Failed(BridgeErrorCodes.ActionNotAllowed, "空仓无提交（unborn HEAD），无法打 tag");
        }

        var create = await _git
            .RunAsync(local.Path!, new[] { "tag", tag }, cancellationToken, WriteTimeout)
            .ConfigureAwait(false);
        if (!create.Success)
        {
            return MapGitFailure("TAG", create, cancellationToken);
        }

        return new BatchItemResult(
            JobItemStates.Ok,
            null,
            $"已在当前 HEAD 打 tag {tag}（未 push）",
            preHead,
            preHead,
            null);
    }

    // ---------------------------------------------------------------- .trash 保留期清理

    /// <summary>
    /// 清理超过 <see cref="RetentionDays"/> 天的 <c>.trash/&lt;stamp&gt;</c> 目录（SRS FR-OP-06 统一保留 30 天）。
    /// 仅删除 <c>&lt;workspace&gt;/.trash</c> 下的 14 位时间戳目录，失败仅告警。
    /// </summary>
    /// <returns>清理的目录数。</returns>
    public int CleanupTrash() => CleanupTrash(RetentionDays);

    /// <summary>
    /// 按指定保留天数清理 <c>.trash/&lt;stamp&gt;</c> 目录（M2b-2 <c>cleanupRetention</c> 复用；
    /// 只删工作区内 <c>.trash</c> 时间戳目录，不触碰其它目录/远端）。
    /// </summary>
    /// <returns>清理的目录数。</returns>
    public int CleanupTrash(int retentionDays)
    {
        if (_paths is null)
        {
            return 0;
        }

        var days = Math.Clamp(retentionDays, 1, 365);
        var trashRoot = Path.Combine(_paths.Root, TrashDirectoryName);
        if (!Directory.Exists(trashRoot))
        {
            return 0;
        }

        var cutoff = DateTime.UtcNow.AddDays(-days);
        var removed = 0;
        foreach (var directory in EnumerateTrashStamps())
        {
            if (!DateTime.TryParseExact(
                    directory,
                    "yyyyMMddHHmmss",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var stamp))
            {
                continue;
            }

            if (stamp > cutoff)
            {
                continue;
            }

            var full = PathPolicy.TryCanonicalize(Path.Combine(trashRoot, directory));
            if (full is null || !_paths.IsWithin(trashRoot, full))
            {
                continue;
            }

            try
            {
                Directory.Delete(full, recursive: true);
                removed++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                HostLog.Warn($"清理过期 .trash 失败（{ex.GetType().Name}）：{directory}");
            }
        }

        if (removed > 0)
        {
            HostLog.Info($"已清理过期 .trash 目录 {removed} 个（保留 {days} 天）");
        }

        return removed;
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>仓库展示名：优先快照 repoName/本地目录名，缺失仓推导 <c>&lt;componentId&gt;_&lt;Design|Impl|MIL|SIL&gt;</c>。</summary>
    public static string RepoNameFor(RepoRef? repo, string componentId, string repoType)
    {
        if (repo is not null)
        {
            return RepoResolver.RepoName(repo);
        }

        return componentId.Trim() + RepoNameSuffix(repoType);
    }

    private static string RepoNameSuffix(string repoType) => repoType.ToUpperInvariant() switch
    {
        "DESIGN" => "_Design",
        "IMPL" => "_Impl",
        "MIL" => "_MIL",
        "SIL" => "_SIL",
        _ => "_" + repoType
    };

    /// <summary>工作区内单一路径段校验（拒绝分隔符/盘符/ADS/控制字符/保留尾点/相对段）。</summary>
    public static bool IsSafeDirectoryName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 255)
        {
            return false;
        }

        if (name is "." or ".." || name.EndsWith('.') || name.EndsWith(' '))
        {
            return false;
        }

        if (name.Contains("..", StringComparison.Ordinal)
            || name.Contains('\\')
            || name.Contains('/')
            || name.Contains(':'))
        {
            return false;
        }

        foreach (var ch in name)
        {
            if (char.IsControl(ch) || ch is '*' or '?' or '"' or '<' or '>' or '|')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>ref 单段名校验（分支/tag/org；拒绝选项注入与非法字符）。</summary>
    public static bool IsSafeRefSegment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 255)
        {
            return false;
        }

        if (value[0] is '-' or '+')
        {
            return false;
        }

        foreach (var ch in value)
        {
            if (char.IsWhiteSpace(ch) || char.IsControl(ch))
            {
                return false;
            }

            if (ch is '~' or '^' or ':' or '?' or '*' or '[' or '\\')
            {
                return false;
            }
        }

        return !value.Contains("..", StringComparison.Ordinal)
            && !value.Contains("@{", StringComparison.Ordinal)
            && !value.EndsWith('.')
            && !value.EndsWith('/')
            && !value.EndsWith(".lock", StringComparison.OrdinalIgnoreCase);
    }

    private IEnumerable<string> EnumerateTrashStamps()
    {
        if (_paths is null)
        {
            yield break;
        }

        var trashRoot = Path.Combine(_paths.Root, TrashDirectoryName);
        if (!Directory.Exists(trashRoot))
        {
            yield break;
        }

        List<string> directories;
        try
        {
            directories = Directory.EnumerateDirectories(trashRoot)
                .Select(Path.GetFileName)
                .Where(name => name is not null && TrashStampPattern().IsMatch(name))
                .Select(name => name!)
                .OrderByDescending(name => name, StringComparer.Ordinal)
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            yield break;
        }

        foreach (var directory in directories)
        {
            yield return directory;
        }
    }

    private string? RequireWithinWorkspace(string localPath)
    {
        if (_paths is null)
        {
            return "未配置工作区根目录";
        }

        return _paths.IsWithin(_paths.Root, localPath) ? null : "仓库路径越出工作区根，已拒绝";
    }

    private static bool IsBusyOrInProgress(RepoRef repo)
    {
        if (string.Equals(repo.Repo.State, "IN_PROGRESS", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return repo.Repo.Flags.Any(flag => string.Equals(flag, "busy", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>状态规范化（缺省 = UNKNOWN，与 SRS §7.2 一致）。</summary>
    private static string NormalizeState(string? state) =>
        string.IsNullOrWhiteSpace(state) ? "UNKNOWN" : state.Trim().ToUpperInvariant();

    /// <summary>§7.6 SYNC 强覆盖列（单源；AHEAD ❌、DIVERGED ⚠默认阻断、BEHIND/DIRTY/SYNCED 允许）。</summary>
    private static bool SyncMatrixAllowed(string state, out string reason)
    {
        switch (state)
        {
            case "SYNCED":
                reason = "无需强覆盖（仅清理 untracked 需确认）";
                return true;
            case "BEHIND":
                reason = "允许（创建备份 ref 后 reset）";
                return true;
            case "DIRTY":
                reason = "允许（stash -u + 已确认）";
                return true;
            case "DIVERGED":
                reason = "默认阻断（需输入组件 ID 强确认）";
                return true;
            case "AHEAD":
                reason = "AHEAD 有未推送提交，SYNC 会丢弃；请先 push/merge（§7.6 AHEAD.SYNC=❌）";
                return false;
            case "IN_PROGRESS":
                reason = "存在进行中操作";
                return false;
            case "UNMERGED":
                reason = "存在未解决的合并冲突（UNMERGED）";
                return false;
            case "INVALID":
                reason = "仓库非法/绑定异常（INVALID）";
                return false;
            case "MISSING":
                reason = "仓库缺失（MISSING）";
                return false;
            case "EMPTY":
                reason = "空仓无 HEAD，无法创建备份 ref（EMPTY）";
                return false;
            case "NO_UPSTREAM":
                reason = "无 upstream（NO_UPSTREAM）";
                return false;
            case "DETACHED_TAG":
            case "DETACHED_COMMIT":
                reason = "detached HEAD（§7.6 DETACHED.SYNC=❌）";
                return false;
            default:
                reason = "状态未知（UNKNOWN），请先刷新扫描";
                return false;
        }
    }

    /// <summary>§7.6 Delete 列（⚠ 允许但需确认；MISSING/INVALID/IN_PROGRESS/UNKNOWN 拒绝）。</summary>
    private static bool DeleteMatrixAllowed(string state, out string reason)
    {
        switch (state)
        {
            case "SYNCED":
            case "BEHIND":
            case "AHEAD":
            case "DIVERGED":
            case "DETACHED_TAG":
            case "DETACHED_COMMIT":
            case "NO_UPSTREAM":
            case "EMPTY":
            case "UNMERGED":
            case "DIRTY":
                reason = "允许（需强确认）";
                return true;
            case "IN_PROGRESS":
                reason = "存在进行中操作";
                return false;
            case "INVALID":
                reason = "仓库非法/绑定异常（INVALID）";
                return false;
            case "MISSING":
                reason = "仓库缺失（MISSING，无本地目录可删）";
                return false;
            default:
                reason = "状态未知（UNKNOWN），请先刷新扫描";
                return false;
        }
    }

    /// <summary>
    /// 强确认文本匹配（SRS 附录 D <c>safety.requireTypedConfirm</c>：精确匹配、区分大小写、忽略首尾空格）。
    /// </summary>
    public static bool MatchesTypedConfirm(string? confirmText, string componentId) =>
        confirmText is not null
        && string.Equals(confirmText.Trim(), componentId.Trim(), StringComparison.Ordinal);

    /// <summary>快照 HEAD 与磁盘 HEAD 的 CAS（快照无 HEAD 信息时不做判定）。</summary>
    private static bool SnapshotHeadMatches(RepoRef repo, string? actualHead)
    {
        var snapshotHead = string.IsNullOrWhiteSpace(repo.Repo.HeadSha) ? null : repo.Repo.HeadSha!.Trim();
        if (snapshotHead is null)
        {
            return true;
        }

        return actualHead is not null
            && string.Equals(snapshotHead, actualHead.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>只读统计工作区改动（N 项 tracked/未跟踪，用于 dry-run 预览）。</summary>
    private async Task<(int Dirty, int Untracked)> CountWorkingTreeAsync(
        string localPath,
        CancellationToken cancellationToken)
    {
        var status = await _git
            .RunAsync(localPath, new[] { "status", "--porcelain" }, cancellationToken)
            .ConfigureAwait(false);
        if (!status.Success)
        {
            return (0, 0);
        }

        var dirty = 0;
        var untracked = 0;
        foreach (var line in status.Stdout.Split('\n'))
        {
            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith("??", StringComparison.Ordinal))
            {
                untracked++;
            }
            else
            {
                dirty++;
            }
        }

        return (dirty, untracked);
    }

    /// <summary>未推送提交数：无可用 upstream 时返回 <c>null</c>（无法确认）。</summary>
    private async Task<int?> CountUnpushedAsync(string localPath, CancellationToken cancellationToken)
    {
        var upstream = await _git
            .RunAsync(localPath, new[] { "rev-parse", "--verify", "--quiet", "@{upstream}" }, cancellationToken)
            .ConfigureAwait(false);
        if (!upstream.Success || upstream.TrimmedStdout.Length == 0)
        {
            return null;
        }

        var count = await _git
            .RunAsync(localPath, new[] { "rev-list", "--count", "@{upstream}..HEAD" }, cancellationToken)
            .ConfigureAwait(false);
        if (!count.Success
            || !int.TryParse(count.TrimmedStdout, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ahead))
        {
            return null;
        }

        return ahead;
    }

    private async Task<bool> HasStashAsync(string localPath, CancellationToken cancellationToken)
    {
        var stash = await _git
            .RunAsync(localPath, new[] { "rev-parse", "--verify", "--quiet", "refs/stash" }, cancellationToken)
            .ConfigureAwait(false);
        return stash.Success && stash.TrimmedStdout.Length > 0;
    }

    private (string? Path, BatchItemResult? Result) ResolveLocalRepo(
        RepoRef? repo,
        string repoType,
        string kind)
    {
        if (repo is null)
        {
            return (null, Skip(BridgeErrorCodes.NotFound, $"仓库不在当前快照，跳过 {kind}"));
        }

        if (!repo.Repo.IsLocal || string.IsNullOrWhiteSpace(repo.Repo.LocalPath))
        {
            return (null, Skip(BridgeErrorCodes.RepoNotLocal, $"仓库未克隆：{RepoNameFor(repo, repo.ComponentId, repoType)}，跳过 {kind}"));
        }

        var localPath = PathPolicy.TryCanonicalize(repo.Repo.LocalPath);
        if (localPath is null || !Directory.Exists(localPath))
        {
            return (null, Skip(BridgeErrorCodes.RepoNotLocal, "本地仓库路径不存在，跳过 " + kind));
        }

        var pathFailure = RequireWithinWorkspace(localPath);
        if (pathFailure is not null)
        {
            return (null, Failed(BridgeErrorCodes.PathOutsideRoot, pathFailure));
        }

        if (IsBusyOrInProgress(repo))
        {
            return (null, Skip(BridgeErrorCodes.RepoBusy, "仓库存在进行中操作或外部 git 锁，跳过 " + kind));
        }

        return (localPath, null);
    }

    private static BatchItemResult Skip(string? errorCode, string message) =>
        new(JobItemStates.Skipped, errorCode, message);

    private static BatchItemResult Failed(string? errorCode, string message) =>
        new(JobItemStates.Failed, errorCode, message);

    /// <summary>.trash 目录名 = UTC <c>yyyyMMddHHmmss</c>（14 位数字，同时用于 RESTORE 与保留期清理）。</summary>
    [GeneratedRegex("^[0-9]{14}$", RegexOptions.CultureInvariant)]
    private static partial Regex TrashStampPattern();

    private BatchItemResult MapGitFailure(string action, GitResult result, CancellationToken cancellationToken)
    {
        if (result.Canceled)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            return Failed(BridgeErrorCodes.GitFailed, action + " 已取消");
        }

        if (result.TimedOut)
        {
            return Failed(BridgeErrorCodes.Timeout, action + " 执行超时（git 进程已终止）");
        }

        var summary = RedactSummary(result.Stderr.Length > 0 ? result.Stderr : result.Stdout);
        return Failed(
            BridgeErrorCodes.GitFailed,
            $"{action} 失败（exit={result.ExitCode}）：{summary}");
    }

    private async Task<string?> ReadHeadAsync(string localPath, CancellationToken cancellationToken)
    {
        var result = await _git
            .RunAsync(localPath, new[] { "rev-parse", "HEAD" }, cancellationToken)
            .ConfigureAwait(false);
        if (!result.Success)
        {
            return null;
        }

        var head = result.TrimmedStdout;
        return head.Length is > 0 and <= 64 ? head : null;
    }

    private static string RedactSummary(string text)
    {
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        var lines = normalized
            .Split('\n')
            .Select(line => line.TrimEnd())
            .Where(line => line.Trim().Length > 0)
            .TakeLast(10)
            .ToList();
        var summary = HostLog.Redact(string.Join("\n", lines));
        return summary.Length <= MaxErrorSummaryChars
            ? summary
            : summary[..(MaxErrorSummaryChars - 1)] + "…";
    }

    /// <summary>跨卷回退：同卷 <see cref="Directory.Move"/>；否则复制后删除源（源与目标都已在工作区内收口）。</summary>
    private static void MoveDirectory(string source, string destination)
    {
        try
        {
            Directory.Move(source, destination);
            return;
        }
        catch (IOException)
        {
            // 跨卷 Move 抛 IOException（ERROR_NOT_SAME_DEVICE）：回退为复制 + 删除源。
        }

        CopyDirectory(source, destination);
        Directory.Delete(source, recursive: true);
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: false);
        }

        foreach (var directory in Directory.EnumerateDirectories(source))
        {
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }
    }

    private static string RepoTypeOf(RepoRef repo, string fallback) =>
        string.IsNullOrWhiteSpace(repo.Repo.RepoType) ? fallback : repo.Repo.RepoType;

    private static string Truncate(string text) =>
        text.Length <= 120 ? text : text[..119] + "…";
}

/// <summary>M2b job/item 状态字符串（SRS §8.1 CHECK 枚举的 UPPER_SNAKE 形式，前后端冻结）。</summary>
public static class JobItemStates
{
    public const string Pending = "PENDING";
    public const string Running = "RUNNING";
    public const string Ok = "OK";
    public const string Failed = "FAILED";
    public const string Skipped = "SKIPPED";
    public const string Canceled = "CANCELED";
    public const string Interrupted = "INTERRUPTED";

    /// <summary>dryRun 预览态（仅 <c>args.dryRun=true</c>）：只预检、不执行、零仓库写。</summary>
    public const string Preview = "PREVIEW";
}

/// <summary>M2b job 状态字符串（SRS §8.1；部分成功统一 PARTIAL）。</summary>
public static class JobStates
{
    public const string Pending = "PENDING";
    public const string Running = "RUNNING";
    public const string Partial = "PARTIAL";
    public const string Success = "SUCCESS";
    public const string Failed = "FAILED";
    public const string Canceled = "CANCELED";
    public const string Interrupted = "INTERRUPTED";

    /// <summary>dryRun 预览作业终态（仅 <c>args.dryRun=true</c>）：表示“已输出预览、未执行任何写”。</summary>
    public const string Preview = "PREVIEW";
}
