using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MbdGitTool.Core.Contracts;

namespace MbdGitTool.Host.Services;

/// <summary>
/// getRemoteRefs（M2 P0-7）：读取本地/远程分支与 Tag，供 CHKOUT 选择器。
/// <para>只读命令：<c>git for-each-ref refs/heads refs/tags refs/remotes/origin</c>；不访问网络、不注入凭据。</para>
/// <para>上限 500 条（超出标记 <c>truncated</c>）；同一仓路径结果缓存 30s（可注入 <see cref="TimeProvider"/> 便于测试）。</para>
/// </summary>
public sealed class RemoteRefsService
{
    /// <summary>单次返回 ref 上限（spec 风险预算）。</summary>
    public const int MaxRefs = 500;

    /// <summary>结果缓存 TTL（30s）。</summary>
    public static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    private const string Format = "%(refname)\t%(objectname)\t%(objecttype)";

    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(20);

    private readonly GitCli _git;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, CacheEntry> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    public RemoteRefsService(GitCli? git = null, TimeProvider? timeProvider = null)
    {
        _git = git ?? new GitCli();
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>读取指定本地仓的 refs（失败映射 <c>E_GIT_FAILED</c>；不抛非取消异常）。</summary>
    public async Task<CommandResult> GetAsync(string localPath, CancellationToken cancellationToken = default)
    {
        var key = CacheKey(localPath);
        var now = _timeProvider.GetUtcNow();
        if (_cache.TryGetValue(key, out var cached) && cached.ExpiresAt > now)
        {
            return CommandResult.Success(cached.Dto);
        }

        var result = await _git
            .RunAsync(
                localPath,
                new[]
                {
                    "for-each-ref",
                    "--count=" + (MaxRefs + 1),
                    "--format=" + Format,
                    "refs/heads",
                    "refs/tags",
                    "refs/remotes/origin"
                },
                cancellationToken,
                CommandTimeout)
            .ConfigureAwait(false);

        if (!result.Success)
        {
            return CommandResult.Failure(
                BridgeErrorCodes.GitFailed,
                "读取 ref 列表失败：" + Summarize(result),
                new Dictionary<string, object?>
                {
                    ["action"] = "getRemoteRefs",
                    ["exitCode"] = result.ExitCode,
                    ["sdkCode"] = "GIT_REF_LIST_FAILED"
                });
        }

        var parsed = ParseRefs(result.Stdout);
        var truncated = parsed.Count > MaxRefs;
        var refs = truncated ? parsed.Take(MaxRefs).ToList() : parsed;
        var dto = new RemoteRefsDto(
            Refs: refs,
            Truncated: truncated,
            Total: parsed.Count,
            GeneratedAt: now.ToString("O"));

        StoreCache(key, dto, now);
        return CommandResult.Success(dto);
    }

    private static List<RemoteRefDto> ParseRefs(string stdout)
    {
        var refs = new List<RemoteRefDto>();
        foreach (var rawLine in stdout.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.Length == 0)
            {
                continue;
            }

            var parts = line.Split('\t');
            if (parts.Length < 3)
            {
                continue;
            }

            var refName = parts[0].Trim();
            var sha = parts[1].Trim();
            var objectType = parts[2].Trim();
            if (refName.Length == 0 || sha.Length == 0)
            {
                continue;
            }

            var kind = refName switch
            {
                _ when refName.StartsWith("refs/heads/", StringComparison.Ordinal) => "LOCAL_BRANCH",
                _ when refName.StartsWith("refs/tags/", StringComparison.Ordinal) => "TAG",
                _ when refName.StartsWith("refs/remotes/origin/", StringComparison.Ordinal) => "REMOTE_BRANCH",
                _ => null
            };
            if (kind is null)
            {
                continue;
            }

            refs.Add(new RemoteRefDto(refName, sha, objectType, kind));
        }

        return refs;
    }

    private void StoreCache(string key, RemoteRefsDto dto, DateTimeOffset now)
    {
        // 简单容量控制：缓存条目超过 256 时清掉已过期项，避免随仓数/路径无限增长。
        if (_cache.Count >= 256)
        {
            foreach (var pair in _cache)
            {
                if (pair.Value.ExpiresAt <= now)
                {
                    _cache.TryRemove(pair.Key, out _);
                }
            }
        }

        _cache[key] = new CacheEntry(dto, now + CacheTtl);
    }

    private static string CacheKey(string localPath) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(localPath));

    private static string Summarize(GitResult result)
    {
        var text = (result.Stderr + "\n" + result.Stdout).Trim();
        if (text.Length == 0)
        {
            return "（无 stderr 输出）";
        }

        var lines = text.Split('\n');
        return HostLog.Redact(string.Join('\n', lines.TakeLast(10)).Trim());
    }

    private sealed record CacheEntry(RemoteRefsDto Dto, DateTimeOffset ExpiresAt);
}
