using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MbdGitTool.Core.Contracts;
using MbdGitTool.Core.Files;
using MbdGitTool.Core.Security;

namespace MbdGitTool.Host.Services;

/// <summary>
/// 提交历史（getCommits / getRepoDetail 最近 5 条）：只读 <c>git log</c>，参数数组、超时、10s 缓存。
/// <para>格式：<c>-n &lt;take&gt; --skip=&lt;skip&gt; --date=iso-strict --pretty=format:%H%x1f%h%x1f%s%x1f%an%x1f%aI%x1f%D</c>；</para>
/// <para>total 用 <c>git rev-list --count HEAD</c>；空仓（unborn HEAD）返回 total=0/items=[]，其余 git 失败 <c>E_GIT_FAILED</c>。</para>
/// </summary>
public sealed class CommitsService
{
    /// <summary>单页提交上限（spec 100/页，硬上限 200）。</summary>
    public const int MaxTake = 200;

    private static readonly TimeSpan LogTimeout = TimeSpan.FromSeconds(15);
    private static readonly string PrettyFormat =
        "--pretty=format:%H%x1f%h%x1f%s%x1f%an%x1f%aI%x1f%D";

    private readonly GitCli _git;
    private readonly TtlCache<CommitsPageDto> _cache = new(TimeSpan.FromSeconds(10), 64);

    public CommitsService(GitCli? git = null) => _git = git ?? new GitCli();

    /// <summary>读取 <paramref name="skip"/>..<paramref name="skip"/>+<paramref name="take"/> 的提交。</summary>
    public async Task<CommandResult> GetAsync(
        string repoRoot,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(repoRoot) || !Directory.Exists(repoRoot))
        {
            return CommandResult.Failure(
                BridgeErrorCodes.RepoNotLocal,
                "仓库未克隆或本地路径不存在",
                new Dictionary<string, object?> { ["path"] = HostLog.Redact(repoRoot ?? string.Empty) });
        }

        if (skip < 0)
        {
            return CommandResult.Failure(
                BridgeErrorCodes.InvalidArgument,
                "参数 skip 不能为负数",
                new Dictionary<string, object?> { ["field"] = "skip" });
        }

        take = Math.Clamp(take <= 0 ? 100 : take, 1, MaxTake);
        var key = (PathPolicy.TryCanonicalize(repoRoot) ?? repoRoot) + "|" + skip + "|" + take;
        if (_cache.TryGet(key, out var cached))
        {
            return CommandResult.Success(cached);
        }

        var log = await _git
            .RunAsync(
                repoRoot,
                new[]
                {
                    "log",
                    "-n",
                    take.ToString(CultureInfo.InvariantCulture),
                    "--skip=" + skip.ToString(CultureInfo.InvariantCulture),
                    "--date=iso-strict",
                    PrettyFormat
                },
                cancellationToken,
                LogTimeout)
            .ConfigureAwait(false);

        if (!log.Success)
        {
            if (IsUnbornRepository(log))
            {
                var empty = new CommitsPageDto(0, Array.Empty<CommitDto>());
                _cache.Set(key, empty);
                return CommandResult.Success(empty);
            }

            return CommandResult.Failure(
                BridgeErrorCodes.GitFailed,
                "git log 失败：" + SafeGitError(log.Stderr),
                new Dictionary<string, object?> { ["sdkCode"] = "GIT_LOG_FAILED" });
        }

        var items = ParseLog(log.Stdout);
        var total = await CountAsync(repoRoot, items.Count + skip, cancellationToken).ConfigureAwait(false);
        var page = new CommitsPageDto(total, items);
        _cache.Set(key, page);
        return CommandResult.Success(page);
    }

    /// <summary>清空提交缓存（refresh 后）。</summary>
    public void InvalidateAll() => _cache.InvalidateAll();

    internal static IReadOnlyList<CommitDto> ParseLog(string stdout)
    {
        if (string.IsNullOrWhiteSpace(stdout))
        {
            return Array.Empty<CommitDto>();
        }

        var items = new List<CommitDto>();
        foreach (var line in stdout.Split('\n'))
        {
            var trimmed = line.TrimEnd('\r');
            if (trimmed.Length == 0)
            {
                continue;
            }

            var parts = trimmed.Split('\u001f');
            if (parts.Length < 6)
            {
                continue;
            }

            items.Add(new CommitDto(
                Sha: parts[0],
                ShortSha: parts[1],
                Message: parts[2],
                Author: parts[3],
                At: parts[4],
                Refs: ParseRefs(parts[5])));
        }

        return items;
    }

    private async Task<int> CountAsync(string repoRoot, int fallback, CancellationToken cancellationToken)
    {
        var result = await _git
            .RunAsync(repoRoot, new[] { "rev-list", "--count", "HEAD" }, cancellationToken, LogTimeout)
            .ConfigureAwait(false);
        if (!result.Success
            || !int.TryParse(result.TrimmedStdout, NumberStyles.Integer, CultureInfo.InvariantCulture, out var total))
        {
            return fallback;
        }

        return total;
    }

    private static IReadOnlyList<string> ParseRefs(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Array.Empty<string>();
        }

        var refs = new List<string>();
        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var value = part.Trim();
            if (value.Length > 0)
            {
                refs.Add(value);
            }
        }

        return refs;
    }

    private static bool IsUnbornRepository(GitResult result) =>
        string.IsNullOrWhiteSpace(result.Stdout)
        && (result.Stderr.Contains("does not have any commits yet", StringComparison.OrdinalIgnoreCase)
            || result.Stderr.Contains("unknown revision", StringComparison.OrdinalIgnoreCase)
            || result.Stderr.Contains("bad revision", StringComparison.OrdinalIgnoreCase)
            || result.Stderr.Contains("ambiguous argument 'HEAD'", StringComparison.OrdinalIgnoreCase));

    private static string SafeGitError(string stderr)
    {
        var first = stderr.Trim().Split('\n')[0].Trim();
        if (first.Length == 0)
        {
            return "（无 stderr）";
        }

        return HostLog.Redact(first.Length > 300 ? first[..300] : first);
    }
}
