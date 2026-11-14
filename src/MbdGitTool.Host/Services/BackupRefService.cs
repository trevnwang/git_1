using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MbdGitTool.Core.Contracts;
using MbdGitTool.Core.Security;

namespace MbdGitTool.Host.Services;

/// <summary>
/// M2b-2 备份 ref 列表/恢复（SRS §9.7 FR-OP-11、D-017/H-05、AC-OP-30）：
/// <para>受控 SYNC 在 reset 前创建 <c>refs/backup/sync/&lt;UTC yyyyMMddHHmmss&gt;/&lt;REPO_TYPE&gt;</c>
/// （见 <see cref="BatchOperationService.SyncAsync"/>）；本服务负责：</para>
/// <para>① <c>git for-each-ref refs/backup/sync</c> 列出 ref/sha/committerdate（按 ref 倒序 = 新备份在前）；</para>
/// <para>② <b>恢复</b>：预检（ref 属于 <c>refs/backup/sync/</c> 且存在、repoType 与 ref 后缀一致、工作区无跟踪改动）
/// → 把当前 HEAD 备份到 <c>refs/backup/restore/&lt;ts&gt;/&lt;TYPE&gt;</c> → <c>reset --hard &lt;ref&gt;</c>；
/// 任一步失败即停止且不 reset，可重试；仅本地 git 写，不触碰远端。</para>
/// <para>③ 保留清理：只删除本地 <c>refs/backup/sync/*</c> 中超期 ref（默认 30 天；<c>backup.retentionDays</c>），
/// <c>update-ref -d</c>，不触碰远端、不删 <c>refs/backup/restore/*</c> 安全备份。</para>
/// <para><b>安全</b>：git 参数一律 <c>ArgumentList</c>；不注入凭据（无网络动作）、错误/响应不含 token；
/// 恢复前加 per-repo 锁（与单仓五键/批量作业互斥）。</para>
/// </summary>
public sealed class BackupRefService
{
    /// <summary>SYNC 备份 ref 前缀（与 <see cref="BatchOperationService.BackupRefPrefix"/> 同一事实源）。</summary>
    public const string SyncRefPrefix = BatchOperationService.BackupRefPrefix;

    /// <summary>恢复前安全备份 ref 前缀（<c>refs/backup/restore/&lt;ts&gt;/&lt;TYPE&gt;</c>）。</summary>
    public const string RestoreRefPrefix = "refs/backup/restore/";

    private const int MaxErrorSummaryChars = 500;

    private static readonly TimeSpan WriteTimeout = TimeSpan.FromSeconds(60);

    private readonly GitCli _git;
    private readonly string? _workspaceRoot;

    public BackupRefService(AppConfig? config = null, GitCli? git = null)
    {
        _git = git ?? new GitCli();
        _workspaceRoot = config?.WorkspaceRoot;
    }

    /// <summary>工作区根目录（保留清理用；空 = 未配置）。</summary>
    public string? WorkspaceRoot => _workspaceRoot;

    /// <summary>
    /// <c>getBackupRefs</c>：列出本地 <c>refs/backup/sync/*</c>（可选按 repoType 过滤），按 ref 倒序。
    /// <para>返回 <c>BackupRefsDto</c>；git 失败返回 <c>E_GIT_FAILED</c>，参数非法返回 <c>E_INVALID_ARGUMENT</c>。</para>
    /// </summary>
    public async Task<CommandResult> GetBackupRefsAsync(
        string? localPath,
        string? repoType,
        CancellationToken cancellationToken = default)
    {
        var pathFailure = ValidateRepoPath(localPath);
        if (pathFailure is not null)
        {
            return CommandResult.Failure(pathFailure);
        }

        var filterType = string.Empty;
        if (!string.IsNullOrWhiteSpace(repoType))
        {
            if (!RepoResolver.TryNormalizeRepoType(repoType, out filterType))
            {
                return CommandResult.Failure(
                    BridgeErrorCodes.InvalidArgument,
                    "参数 repoType 必须是 DESIGN/IMPL/MIL/SIL",
                    new Dictionary<string, object?> { ["field"] = "repoType", ["repoType"] = repoType });
            }
        }

        var entries = await ListRefsAsync(localPath!, cancellationToken).ConfigureAwait(false);
        if (entries is null)
        {
            return CommandResult.Failure(
                BridgeErrorCodes.GitFailed,
                "读取备份 ref 失败（git for-each-ref；本地仓库可能不可用）",
                new Dictionary<string, object?> { ["retryable"] = true });
        }

        var filtered = string.IsNullOrEmpty(filterType)
            ? entries
            : entries.Where(entry => entry.RepoType == filterType).ToList();

        var refs = filtered
            .OrderByDescending(entry => entry.Ref, StringComparer.Ordinal)
            .Select(entry => new BackupRefDto(entry.Ref, entry.Sha, entry.CreatedAt, entry.RepoType))
            .ToList();
        return CommandResult.Success(new BackupRefsDto(refs));
    }

    /// <summary>
    /// <c>restoreBackupRef</c>（FR-OP-11/AC-OP-30）：校验 + 安全备份 + <c>reset --hard</c>。
    /// <para>返回结果（<paramref name="error"/> 非 null 表示失败，且失败时保证未执行 reset/未丢数据）。</para>
    /// </summary>
    public async Task<(RestoreBackupRefResultDto? Result, BridgeError? Error)> RestoreAsync(
        string? localPath,
        string? repoType,
        string? requestedRef,
        CancellationToken cancellationToken = default)
    {
        var pathFailure = ValidateRepoPath(localPath);
        if (pathFailure is not null)
        {
            return (null, pathFailure);
        }

        if (!RepoResolver.TryNormalizeRepoType(repoType, out var normalizedType))
        {
            return (null, new BridgeError(
                BridgeErrorCodes.InvalidArgument,
                "参数 repoType 必须是 DESIGN/IMPL/MIL/SIL",
                new Dictionary<string, object?> { ["field"] = "repoType", ["repoType"] = repoType }));
        }

        if (string.IsNullOrWhiteSpace(requestedRef))
        {
            return (null, new BridgeError(
                BridgeErrorCodes.InvalidArgument,
                "缺少必填参数 ref（必须是 refs/backup/sync/&lt;stamp&gt;/&lt;TYPE&gt;）",
                new Dictionary<string, object?> { ["field"] = "ref" }));
        }

        if (!TryParseBackupRef(requestedRef, out var refText, out var stamp, out var refType))
        {
            return (null, new BridgeError(
                BridgeErrorCodes.InvalidArgument,
                "参数 ref 必须属于 refs/backup/sync/<UTC yyyyMMddHHmmss>/<DESIGN|IMPL|MIL|SIL> 且合法",
                new Dictionary<string, object?> { ["field"] = "ref", ["ref"] = requestedRef }));
        }

        if (!string.Equals(refType, normalizedType, StringComparison.Ordinal))
        {
            return (null, new BridgeError(
                BridgeErrorCodes.InvalidArgument,
                $"参数 ref 的仓类型后缀（{refType}）与 repoType（{normalizedType}）不一致",
                new Dictionary<string, object?>
                {
                    ["field"] = "ref",
                    ["refType"] = refType,
                    ["repoType"] = normalizedType
                }));
        }

        var path = localPath!;

        // 同仓串行：与单仓五键/批量作业共享 per-repo 锁（避免恢复与其它写操作并发）。
        using var lease = await RepoActionService
            .AcquireRepoLockAsync(path, cancellationToken)
            .ConfigureAwait(false);

        // 1) ref 必须存在且解析为 commit（禁止“假成功”）。
        var verify = await _git
            .RunAsync(path, new[] { "rev-parse", "--verify", "--quiet", refText + "^{commit}" }, cancellationToken)
            .ConfigureAwait(false);
        if (!verify.Success || verify.TrimmedStdout.Length == 0)
        {
            return (null, new BridgeError(
                BridgeErrorCodes.RefNotFound,
                $"备份 ref 不存在或不是 commit：{refText}",
                new Dictionary<string, object?> { ["ref"] = refText }));
        }

        // 2) 脏工作区策略：有跟踪文件改动时默认阻断（reset --hard 会丢弃）；untracked 不受 reset 影响。
        var status = await _git
            .RunAsync(path, new[] { "status", "--porcelain", "--untracked-files=no" }, cancellationToken)
            .ConfigureAwait(false);
        if (!status.Success)
        {
            return (null, GitFailure("读取工作区状态失败", status));
        }

        if (status.Stdout.Trim().Length > 0)
        {
            return (null, new BridgeError(
                BridgeErrorCodes.DirtyWorktree,
                "工作区存在未提交的已跟踪改动，restore 会被丢弃；请先 COMMIT 或 STASH 后重试（未改动任何文件）",
                new Dictionary<string, object?>
                {
                    ["ref"] = refText,
                    ["dirtyFiles"] = status.Stdout
                        .Split('\n', StringSplitOptions.RemoveEmptyEntries).Length
                }));
        }

        // 3) 当前 HEAD 先备份到 refs/backup/restore/<ts>/<TYPE>（空仓无 HEAD 时跳过）。
        var preHeadResult = await _git
            .RunAsync(path, new[] { "rev-parse", "--verify", "--quiet", "HEAD" }, cancellationToken)
            .ConfigureAwait(false);
        var preHead = preHeadResult.Success ? preHeadResult.TrimmedStdout : null;
        string? safetyRef = null;
        if (preHead is not null)
        {
            var safetyStamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
            safetyRef = RestoreRefPrefix + safetyStamp + "/" + normalizedType;
            var updateSafety = await _git
                .RunAsync(path, new[] { "update-ref", safetyRef, preHead }, cancellationToken, WriteTimeout)
                .ConfigureAwait(false);
            if (!updateSafety.Success)
            {
                return (null, GitFailure(
                    "创建恢复前安全备份 ref 失败；已拒绝 reset，工作区未改动",
                    updateSafety));
            }
        }

        // 4) reset --hard <ref>（不 force 推送/不触远端；失败保留安全备份可重试）。
        var reset = await _git
            .RunAsync(path, new[] { "reset", "--hard", refText }, cancellationToken, WriteTimeout)
            .ConfigureAwait(false);
        if (!reset.Success)
        {
            return (null, GitFailure(
                "reset --hard 失败；未改动远端，恢复前安全备份已保留，可重试",
                reset));
        }

        var postHeadResult = await _git
            .RunAsync(path, new[] { "rev-parse", "--verify", "--quiet", "HEAD" }, cancellationToken)
            .ConfigureAwait(false);
        var postHead = postHeadResult.Success ? postHeadResult.TrimmedStdout : null;

        HostLog.Info(
            $"restoreBackupRef 完成：ref={refText} {normalizedType} pre={preHead ?? "<none>"} post={postHead ?? "<none>"}");

        return (new RestoreBackupRefResultDto(
            Restored: true,
            Ref: refText,
            SafetyRef: safetyRef,
            PreHead: preHead,
            PostHead: postHead,
            Message: "已从备份 ref 恢复（reset --hard）；恢复前 HEAD 已备份到 "
                + (safetyRef ?? "<空仓无 HEAD>") + "，仅本地生效、远端未改动"), null);
    }

    /// <summary>
    /// 保留清理：删除工作区内各本地仓 <c>refs/backup/sync/*</c> 中超过
    /// <paramref name="retentionDays"/> 天的 ref（只删本地 ref，不触远端）。
    /// </summary>
    /// <returns>删除的 ref 数。</returns>
    public async Task<int> CleanupExpiredAsync(
        int retentionDays,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_workspaceRoot) || !Directory.Exists(_workspaceRoot))
        {
            return 0;
        }

        var days = Math.Clamp(retentionDays, 7, 90);
        var cutoff = DateTime.UtcNow.AddDays(-days);
        var removed = 0;

        foreach (var directory in EnumerateWorkspaceRepositories())
        {
            cancellationToken.ThrowIfCancellationRequested();
            removed += await CleanupRepositoryAsync(directory, cutoff, cancellationToken).ConfigureAwait(false);
        }

        if (removed > 0)
        {
            HostLog.Info($"已清理超期备份 ref {removed} 个（backup.retentionDays={days}；仅本地 ref，远端未改动）");
        }

        return removed;
    }

    /// <summary>
    /// 解析 <c>refs/backup/sync/&lt;stamp&gt;/&lt;TYPE&gt;</c>（严格两段：14 位 UTC stamp + 仓类型）。
    /// </summary>
    public static bool TryParseBackupRef(
        string? value,
        out string refText,
        out string stamp,
        out string repoType)
    {
        refText = string.Empty;
        stamp = string.Empty;
        repoType = string.Empty;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > 255
            || !trimmed.StartsWith(SyncRefPrefix, StringComparison.Ordinal)
            || trimmed.Any(char.IsControl))
        {
            return false;
        }

        var remainder = trimmed[SyncRefPrefix.Length..];
        var segments = remainder.Split('/');
        if (segments.Length != 2)
        {
            return false;
        }

        var stampSegment = segments[0];
        if (stampSegment.Length != 14
            || !stampSegment.All(char.IsAsciiDigit)
            || !DateTime.TryParseExact(
                stampSegment,
                "yyyyMMddHHmmss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out _))
        {
            return false;
        }

        if (!RepoResolver.TryNormalizeRepoType(segments[1], out repoType))
        {
            return false;
        }

        refText = trimmed;
        stamp = stampSegment;
        return true;
    }

    // ------------------------------------------------------------------ internals

    private sealed record BackupRefEntry(string Ref, string Sha, string CreatedAt, string RepoType);

    private async Task<IReadOnlyList<BackupRefEntry>?> ListRefsAsync(
        string localPath,
        CancellationToken cancellationToken)
    {
        var result = await _git
            .RunAsync(
                localPath,
                new[]
                {
                    "for-each-ref",
                    "--format=%(refname)%09%(objectname)%09%(committerdate:iso-strict)",
                    SyncRefPrefix
                },
                cancellationToken)
            .ConfigureAwait(false);
        if (!result.Success)
        {
            HostLog.Warn($"读取备份 ref 失败：exit={result.ExitCode}");
            return null;
        }

        var entries = new List<BackupRefEntry>();
        foreach (var line in result.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = line.TrimEnd('\r').Split('\t');
            if (fields.Length < 3)
            {
                continue;
            }

            if (!TryParseBackupRef(fields[0], out var refText, out _, out var repoType))
            {
                continue;
            }

            var createdAt = NormalizeCommitterDate(fields[2]);
            entries.Add(new BackupRefEntry(refText, fields[1].Trim(), createdAt, repoType));
        }

        return entries;
    }

    private static string NormalizeCommitterDate(string raw)
    {
        if (DateTimeOffset.TryParse(
                raw.Trim(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            return parsed.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        }

        return raw.Trim();
    }

    private async Task<int> CleanupRepositoryAsync(
        string localPath,
        DateTime cutoffUtc,
        CancellationToken cancellationToken)
    {
        var result = await _git
            .RunAsync(localPath, new[] { "for-each-ref", "--format=%(refname)", SyncRefPrefix }, cancellationToken)
            .ConfigureAwait(false);
        if (!result.Success)
        {
            return 0;
        }

        var removed = 0;
        foreach (var raw in result.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var refName = raw.Trim();
            if (!TryParseBackupRef(refName, out var refText, out var stamp, out _))
            {
                continue;
            }

            if (!DateTime.TryParseExact(
                    stamp,
                    "yyyyMMddHHmmss",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var createdAt))
            {
                continue;
            }

            if (createdAt > cutoffUtc)
            {
                continue;
            }

            var delete = await _git
                .RunAsync(localPath, new[] { "update-ref", "-d", refText }, cancellationToken, WriteTimeout)
                .ConfigureAwait(false);
            if (delete.Success)
            {
                removed++;
            }
            else
            {
                HostLog.Warn($"删除超期备份 ref 失败：{refText}（exit={delete.ExitCode}）");
            }
        }

        return removed;
    }

    private IEnumerable<string> EnumerateWorkspaceRepositories()
    {
        if (string.IsNullOrWhiteSpace(_workspaceRoot))
        {
            yield break;
        }

        string[] directories;
        try
        {
            directories = Directory.GetDirectories(_workspaceRoot);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            yield break;
        }

        foreach (var directory in directories)
        {
            if (string.Equals(Path.GetFileName(directory), BatchOperationService.TrashDirectoryName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!TryIsGitRepository(directory))
            {
                continue;
            }

            yield return directory;
        }
    }

    private static bool TryIsGitRepository(string path)
    {
        try
        {
            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                // reparse point 不进入（避免误删指向工作区外的仓库 ref）。
                return false;
            }

            return Directory.Exists(Path.Combine(path, ".git"))
                || File.Exists(Path.Combine(path, ".git"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static BridgeError? ValidateRepoPath(string? localPath)
    {
        if (string.IsNullOrWhiteSpace(localPath))
        {
            return new BridgeError(
                BridgeErrorCodes.RepoNotLocal,
                "仓库未克隆或本地路径缺失（请刷新后重试）");
        }

        var canonical = PathPolicy.TryCanonicalize(localPath);
        if (canonical is null || !Directory.Exists(canonical) || !TryIsGitRepository(canonical))
        {
            return new BridgeError(
                BridgeErrorCodes.RepoNotLocal,
                "本地仓库路径不存在或不是 git 仓库（请刷新/修复后重试）");
        }

        return null;
    }

    private BridgeError GitFailure(string message, GitResult result)
    {
        var summary = result.Stderr.Trim();
        if (summary.Length > MaxErrorSummaryChars)
        {
            summary = summary[..MaxErrorSummaryChars];
        }

        return new BridgeError(
            BridgeErrorCodes.GitFailed,
            message,
            new Dictionary<string, object?>
            {
                ["exitCode"] = result.ExitCode,
                ["stderr"] = summary,
                ["retryable"] = true
            });
    }
}
