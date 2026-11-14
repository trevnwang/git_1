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
    /// 受控 SYNC（FR-OP-05）：<c>fetch --prune</c> → 备份 <c>refs/backup/sync/&lt;stamp&gt;/&lt;repoType&gt;</c> →
    /// dirty 时 <c>stash -u</c> → <c>reset --hard origin/&lt;branch&gt;</c>；任一步失败即停止且不 reset。
    /// </summary>
    public async Task<BatchItemResult> SyncAsync(
        RepoRef? repo,
        string repoType,
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

        // 1) 网络凭据作用域校验（fail-closed：不匹配/配置非法时不启动网络 git）。
        var (credentialFailure, credentialEnvironment) =
            await _actions.PrepareCredentialEnvironmentAsync(localPath, "SYNC", cancellationToken)
                .ConfigureAwait(false);
        if (credentialFailure is not null)
        {
            return Failed(
                credentialFailure.Error?.Code ?? BridgeErrorCodes.GitFailed,
                credentialFailure.Error?.Message ?? "SYNC 凭据作用域校验失败");
        }

        // 2) fetch --prune。
        var fetch = await _git
            .RunAsync(localPath, new[] { "fetch", "--prune", "origin" }, cancellationToken, NetworkTimeout, credentialEnvironment)
            .ConfigureAwait(false);
        if (!fetch.Success)
        {
            return MapGitFailure("SYNC", fetch, cancellationToken);
        }

        // 3) 远端同名分支必须存在（禁止 reset 到不存在的 ref）。
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

        // 4) 备份 ref 必须先成功（backup 失败 → item FAILED，绝不 reset）。
        var preHead = await ReadHeadAsync(localPath, cancellationToken).ConfigureAwait(false);
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

        // 5) dirty（含未跟踪）先 stash -u；stash 失败则不 reset（避免本地改动丢失）。
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

        // 6) reset --hard（不使用 git pull；禁止隐式 merge）。
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
    /// 本地软删除（FR-OP-06）：仅 <c>Directory.Move</c> 到 <c>&lt;workspace&gt;\.trash\&lt;stamp&gt;\&lt;repoName&gt;</c>；
    /// 不删远端、不直接递归删除工作区；跨卷回退“复制 + 删除源”。
    /// </summary>
    public async Task<BatchItemResult> DeleteAsync(
        RepoRef? repo,
        string repoType,
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

        if (IsBusyOrInProgress(repo))
        {
            return Skip(BridgeErrorCodes.RepoBusy, "仓库存在进行中操作或外部 git 锁，跳过 DELETE");
        }

        var repoName = Path.GetFileName(localPath);
        if (!IsSafeDirectoryName(repoName))
        {
            return Failed(BridgeErrorCodes.PathInvalid, "仓库目录名非法，拒绝 DELETE");
        }

        var preHead = await ReadHeadAsync(localPath, cancellationToken).ConfigureAwait(false);

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
    public int CleanupTrash()
    {
        if (_paths is null)
        {
            return 0;
        }

        var trashRoot = Path.Combine(_paths.Root, TrashDirectoryName);
        if (!Directory.Exists(trashRoot))
        {
            return 0;
        }

        var cutoff = DateTime.UtcNow.AddDays(-RetentionDays);
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
            HostLog.Info($"已清理过期 .trash 目录 {removed} 个（保留 {RetentionDays} 天）");
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
}
