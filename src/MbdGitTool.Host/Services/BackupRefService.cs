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
/// M2b-2 备份 ref 列表/恢复（SRS §9.7 FR-OP-11、D-017/H-05、AC-OP-30；M2b-2b 分支身份 = ADR D-026 P1-2）：
/// <para>受控 SYNC 在 reset 前创建 <c>refs/backup/sync/&lt;UTC yyyyMMddHHmmss&gt;/&lt;branch-encoded&gt;/&lt;REPO_TYPE&gt;</c>
/// （见 <see cref="BatchOperationService.SyncAsync"/>；分支名经 <see cref="EncodeBranchSegment"/> 安全编码，
/// 无分支身份用 <see cref="DetachedBranchSegment"/> 占位）；本服务负责：</para>
/// <para>① <c>git for-each-ref refs/backup/sync</c> 列出 ref/sha/committerdate/branch（按 ref 倒序 = 新备份在前）；
/// 兼容旧格式 <c>&lt;ts&gt;/&lt;TYPE&gt;</c>（解析为 <c>Branch=null</c> + <c>Format=LEGACY</c>）。</para>
/// <para>② <b>恢复</b>：预检（ref 属于 <c>refs/backup/sync/</c> 且存在、repoType 与 ref 后缀一致、
/// 新格式的来源分支必须等于当前分支（不一致 <c>E_ACTION_NOT_ALLOWED</c> 且零写）、工作区无 tracked/untracked/ignored 改动）
/// → 把当前 HEAD 备份到 <c>refs/backup/restore/&lt;ts&gt;/&lt;TYPE&gt;</c> → <c>reset --hard &lt;ref&gt;</c>；
/// 任一步失败即停止且不 reset，可重试；仅本地 git 写，不触碰远端。</para>
/// <para>③ 保留清理：只删除本地 <c>refs/backup/sync/*</c> 中超期 ref（默认 30 天；<c>backup.retentionDays</c>），
/// 按 ref 时间戳同时覆盖新旧两种格式；<c>update-ref -d</c>，不触碰远端、不删 <c>refs/backup/restore/*</c> 安全备份。</para>
/// <para><b>安全</b>：git 参数一律 <c>ArgumentList</c>；不注入凭据（无网络动作）、错误/响应不含 token；
/// 恢复前加 per-repo 锁（与单仓五键/批量作业互斥）。</para>
/// </summary>
public sealed class BackupRefService
{
    /// <summary>SYNC 备份 ref 前缀（与 <see cref="BatchOperationService.BackupRefPrefix"/> 同一事实源）。</summary>
    public const string SyncRefPrefix = BatchOperationService.BackupRefPrefix;

    /// <summary>恢复前安全备份 ref 前缀（<c>refs/backup/restore/&lt;ts&gt;/&lt;TYPE&gt;</c>）。</summary>
    public const string RestoreRefPrefix = "refs/backup/restore/";

    /// <summary>无法确定来源分支时的占位段（分支名为空/非法；解析回 <c>Branch=DETACHED</c>，恢复即不安全阻断）。</summary>
    public const string DetachedBranchSegment = "DETACHED";

    private const int MaxRefNameChars = 255;
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
    /// <para>返回 <c>BackupRefsDto</c>（含 <c>branch</c>/<c>format</c>）；git 失败返回 <c>E_GIT_FAILED</c>，
    /// 参数非法返回 <c>E_INVALID_ARGUMENT</c>。</para>
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
            .Select(entry => new BackupRefDto(
                entry.Ref,
                entry.Sha,
                entry.CreatedAt,
                entry.RepoType,
                entry.Branch,
                entry.Format))
            .ToList();
        return CommandResult.Success(new BackupRefsDto(refs));
    }

    /// <summary>
    /// <c>restoreBackupRef</c>（FR-OP-11/AC-OP-30；M2b-2b ADR D-026 P1-2）：校验 + 安全备份 + <c>reset --hard</c>。
    /// <para>新格式（携带来源分支）：来源分支 ≠ 当前分支 → <c>E_ACTION_NOT_ALLOWED</c>，零写；
    /// 旧格式（<c>Branch=null</c>）：允许恢复，但响应 message 标注“旧备份无分支身份，请确认当前分支”；</para>
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
                "缺少必填参数 ref（必须是 refs/backup/sync/<stamp>/<branch>/<TYPE>；兼容旧格式 <stamp>/<TYPE>）",
                new Dictionary<string, object?> { ["field"] = "ref" }));
        }

        if (!TryParseBackupRef(requestedRef, out var refText, out _, out var refBranch, out var refType, out var refFormat))
        {
            return (null, new BridgeError(
                BridgeErrorCodes.InvalidArgument,
                "参数 ref 必须属于 refs/backup/sync/<UTC yyyyMMddHHmmss>/<branch>/<DESIGN|IMPL|MIL|SIL>"
                + "（或旧格式 <UTC yyyyMMddHHmmss>/<TYPE>）且合法",
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

        // 1b) M2b-2b（ADR D-026 P1-2）：新格式携带来源分支，必须与当前分支一致；
        // 否则恢复会把“另一个分支的备份”reset 到当前分支指针上 → 零写拒绝（不建安全 ref、不 reset）。
        var currentBranch = await ReadCurrentBranchAsync(path, cancellationToken).ConfigureAwait(false);
        if (refBranch is not null
            && !string.Equals(refBranch, currentBranch, StringComparison.Ordinal))
        {
            return (null, new BridgeError(
                BridgeErrorCodes.ActionNotAllowed,
                $"备份 ref 记录的来源分支为 {refBranch}，当前分支为 {currentBranch ?? "<detached HEAD>"}；"
                + "restore 会重置当前分支，已拒绝（HEAD/工作区未改动，未创建安全备份 ref）",
                new Dictionary<string, object?>
                {
                    ["ref"] = refText,
                    ["expected"] = refBranch,
                    ["current"] = currentBranch,
                    ["branch"] = refBranch,
                    ["format"] = refFormat
                }));
        }

        // 2) 脏工作区策略（FR-OP-11/AC-OP-30/FR-SAFE-01）：tracked + untracked + ignored 均阻断。
        // reset --hard 会覆盖/删除挡路的 untracked/ignored 文件；恢复前必须让用户先处理，禁止静默丢失。
        var tracked = await _git
            .RunAsync(path, new[] { "status", "--porcelain", "--untracked-files=no" }, cancellationToken)
            .ConfigureAwait(false);
        if (!tracked.Success)
        {
            return (null, GitFailure("读取工作区状态失败", tracked));
        }

        if (tracked.Stdout.Trim().Length > 0)
        {
            return (null, new BridgeError(
                BridgeErrorCodes.DirtyWorktree,
                "工作区存在未提交的已跟踪改动，restore 会被丢弃；请先 COMMIT 或 STASH 后重试（未改动任何文件）",
                new Dictionary<string, object?>
                {
                    ["ref"] = refText,
                    ["dirtyFiles"] = tracked.Stdout
                        .Split('\n', StringSplitOptions.RemoveEmptyEntries).Length
                }));
        }

        // 2b) untracked/ignored 也可能被 reset --hard 覆盖/删除：默认阻断（FR-OP-11/AC-OP-30）。
        var untracked = await _git
            .RunAsync(path, new[] { "status", "--porcelain", "--untracked-files=normal", "--ignored=matching" }, cancellationToken)
            .ConfigureAwait(false);
        if (!untracked.Success)
        {
            return (null, GitFailure("读取工作区状态失败", untracked));
        }

        if (untracked.Stdout.Trim().Length > 0)
        {
            return (null, new BridgeError(
                BridgeErrorCodes.WorktreeOverwrite,
                "工作区存在未跟踪/忽略文件，restore 可能覆盖或删除它们；请先移走后重试（未改动任何文件）",
                new Dictionary<string, object?>
                {
                    ["ref"] = refText,
                    ["untrackedFiles"] = untracked.Stdout
                        .Split('\n', StringSplitOptions.RemoveEmptyEntries).Length,
                    ["requiresCommit"] = false
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
            var safetyStamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N")[..8];
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
            $"restoreBackupRef 完成：ref={refText} {normalizedType} format={refFormat}"
            + $" branch={refBranch ?? "<legacy>"} current={currentBranch ?? "<detached>"}"
            + $" pre={preHead ?? "<none>"} post={postHead ?? "<none>"}");

        // M2b-2b：旧格式（Branch=null）无分支身份，允许恢复但必须在响应 message 明确提示用户确认当前分支。
        var identityNote = refFormat == BackupRefFormats.Legacy || refBranch is null
            ? "；注意：旧备份无分支身份，请确认当前分支"
                + "（" + (currentBranch ?? "detached HEAD") + "）"
            : "；备份来源分支：" + refBranch + "（与当前分支一致）";

        return (new RestoreBackupRefResultDto(
            Restored: true,
            Ref: refText,
            SafetyRef: safetyRef,
            PreHead: preHead,
            PostHead: postHead,
            Message: "已从备份 ref 恢复（reset --hard）；恢复前 HEAD 已备份到 "
                + (safetyRef ?? "<空仓无 HEAD>") + "，仅本地生效、远端未改动"
                + identityNote), null);
    }

    /// <summary>
    /// 保留清理：删除工作区内各本地仓 <c>refs/backup/sync/*</c> 中超过
    /// <paramref name="retentionDays"/> 天的 ref（只删本地 ref，不触远端）。
    /// <para>按 ref 的 UTC 时间戳判断，同时覆盖旧格式（<c>&lt;ts&gt;/&lt;TYPE&gt;</c>）与新格式
    /// （<c>&lt;ts&gt;/&lt;branch&gt;/&lt;TYPE&gt;</c>）；保留期不变。</para>
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
    /// 解析备份 ref（M2b-2b 兼容新旧两格式；非法/多余段一律拒绝）：
    /// <para>旧格式 <c>refs/backup/sync/&lt;14 位 UTC stamp&gt;/&lt;TYPE&gt;</c> → <c>branch=null</c> + <c>LEGACY</c>；</para>
    /// <para>新格式 <c>refs/backup/sync/&lt;14 位 UTC stamp&gt;/&lt;branch-encoded&gt;/&lt;TYPE&gt;</c> →
    /// <c>branch=Uri.UnescapeDataString(段)</c>（必须为合法分支名且可规范回编码）+ <c>BRANCH</c>；
    /// <c>DETACHED</c> 占位段解析为 <c>branch="DETACHED"</c>（恢复时与任何真实分支不一致 → 安全阻断）。</para>
    /// </summary>
    public static bool TryParseBackupRef(
        string? value,
        out string refText,
        out string stamp,
        out string? branch,
        out string repoType,
        out string format)
    {
        refText = string.Empty;
        stamp = string.Empty;
        branch = null;
        repoType = string.Empty;
        format = BackupRefFormats.Legacy;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > MaxRefNameChars
            || !trimmed.StartsWith(SyncRefPrefix, StringComparison.Ordinal)
            || trimmed.Any(char.IsControl))
        {
            return false;
        }

        var remainder = trimmed[SyncRefPrefix.Length..];
        var segments = remainder.Split('/');

        // 旧格式 2 段 / 新格式 3 段；其余（0/1/4+ 段、空段）= 非法。
        if (segments.Length is < 2 or > 3)
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

        var typeSegment = segments[^1];
        if (!RepoResolver.TryNormalizeRepoType(typeSegment, out var normalizedRepoType))
        {
            return false;
        }

        string? decodedBranch = null;
        var parsedFormat = BackupRefFormats.Legacy;
        if (segments.Length == 3)
        {
            if (!TryDecodeBranchSegment(segments[1], out decodedBranch))
            {
                return false;
            }

            parsedFormat = BackupRefFormats.Branch;
        }

        // 仅在全量校验通过后一次性写出（失败时 out 参数保持初始空值）。
        refText = trimmed;
        stamp = stampSegment;
        branch = decodedBranch;
        repoType = normalizedRepoType;
        format = parsedFormat;
        return true;
    }

    /// <summary>
    /// 兼容旧签名（M2b-2a 调用/测试）：解析并丢弃 branch/format 输出。
    /// </summary>
    public static bool TryParseBackupRef(
        string? value,
        out string refText,
        out string stamp,
        out string repoType) =>
        TryParseBackupRef(value, out refText, out stamp, out _, out repoType, out _);

    /// <summary>
    /// 把分支名编码为备份 ref 单段（<see cref="Uri.EscapeDataString"/>，如 <c>feature/x</c> → <c>feature%2Fx</c>）。
    /// <para>分支为空/非法 → <see cref="DetachedBranchSegment"/>（<c>DETACHED</c>，表示无分支身份）。</para>
    /// </summary>
    public static string EncodeBranchSegment(string? branch)
    {
        if (string.IsNullOrWhiteSpace(branch) || !IsValidBranchIdentity(branch))
        {
            return DetachedBranchSegment;
        }

        return Uri.EscapeDataString(branch);
    }

    /// <summary>
    /// 解码备份 ref 的 branch 段并严格校验：必须是合法分支身份，且“解码 → 重新编码”与原文完全一致
    /// （拒绝非规范编码、<c>%</c> 残留、非法转义、控制字符等）。
    /// </summary>
    private static bool TryDecodeBranchSegment(string segment, out string? branch)
    {
        branch = null;
        if (segment.Length is 0 or > MaxRefNameChars)
        {
            return false;
        }

        string decoded;
        try
        {
            decoded = Uri.UnescapeDataString(segment);
        }
        catch (UriFormatException)
        {
            return false;
        }

        if (!IsValidBranchIdentity(decoded))
        {
            return false;
        }

        if (!string.Equals(Uri.EscapeDataString(decoded), segment, StringComparison.Ordinal))
        {
            return false;
        }

        branch = decoded;
        return true;
    }

    /// <summary>
    /// 分支身份校验（与 <see cref="BatchOperationService.IsSafeRefSegment"/> 同口径，另拒绝 Git 分支不可能出现的
    /// 首字符/空段形态）。仅用于备份 ref 的构造/解析，不做 git 写操作。
    /// </summary>
    private static bool IsValidBranchIdentity(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaxRefNameChars)
        {
            return false;
        }

        if (value is "." or ".." or "@")
        {
            return false;
        }

        if (value[0] is '.' or '/' or '-' or '+')
        {
            return false;
        }

        if (value[^1] is '.' or '/')
        {
            return false;
        }

        if (value.Contains("//", StringComparison.Ordinal))
        {
            return false;
        }

        return BatchOperationService.IsSafeRefSegment(value);
    }

    // ------------------------------------------------------------------ internals

    private sealed record BackupRefEntry(
        string Ref,
        string Sha,
        string CreatedAt,
        string RepoType,
        string? Branch,
        string Format);

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

            if (!TryParseBackupRef(
                    fields[0],
                    out var refText,
                    out _,
                    out var branch,
                    out var repoType,
                    out var format))
            {
                continue;
            }

            var createdAt = NormalizeCommitterDate(fields[2]);
            entries.Add(new BackupRefEntry(refText, fields[1].Trim(), createdAt, repoType, branch, format));
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

    /// <summary>读取当前 HEAD 分支名（detached / 读取失败返回 null；M2b-2b 恢复分支校验用）。</summary>
    private async Task<string?> ReadCurrentBranchAsync(string localPath, CancellationToken cancellationToken)
    {
        var result = await _git
            .RunAsync(localPath, new[] { "rev-parse", "--abbrev-ref", "HEAD" }, cancellationToken)
            .ConfigureAwait(false);
        if (!result.Success)
        {
            return null;
        }

        var branch = result.TrimmedStdout;
        if (branch.Length == 0 || branch.Equals("HEAD", StringComparison.Ordinal))
        {
            return null; // detached HEAD
        }

        return branch;
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
