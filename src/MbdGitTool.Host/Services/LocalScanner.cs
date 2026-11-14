using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MbdGitTool.Core.Domain;

namespace MbdGitTool.Host.Services;

/// <summary>本地扫描识别出的一个仓库（仓库全名 + 解析后的组件/类型 + 事实）。</summary>
public sealed record LocalRepoEntry(
    string FullName,
    string ComponentId,
    RepoType RepoType,
    string LocalPath,
    RepoFacts Facts);

/// <summary>本地扫描结果：识别出的仓 + 跳过/告警清单（未绑定仓、reparse point、扫描失败等）。</summary>
public sealed record LocalScanResult(
    IReadOnlyList<LocalRepoEntry> Repos,
    IReadOnlyList<string> Issues)
{
    public static LocalScanResult Empty { get; } = new(Array.Empty<LocalRepoEntry>(), Array.Empty<string>());
}

/// <summary>
/// 本地扫描（SRS §6.3 / §9.3 FR-SCAN-01/02/03）。
/// <para>遍历 <c>workspace_root</c> 直接子目录（深度 1，非递归），识别 <c>.git</c> 目录/文件；</para>
/// <para>采集 §7.2 所需事实（HEAD 形态/分支/upstream/ahead-behind/dirty/untracked/stash/unmerged/进行中/shallow/busy）；</para>
/// <para>跳过 reparse point；单个仓失败记 UNKNOWN + error，不中断整体；所有 git 调用走参数数组（禁止 shell 拼接）。</para>
/// </summary>
public sealed class LocalScanner
{
    private const int MaxParallelism = 8;

    private readonly GitCli _git;

    public LocalScanner(GitCli? git = null) => _git = git ?? new GitCli();

    /// <summary>扫描工作区根目录的直接子目录。取消时抛 <see cref="OperationCanceledException"/>。</summary>
    public async Task<LocalScanResult> ScanAsync(
        string workspaceRoot,
        AppConfig config,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(config);

        if (!Directory.Exists(workspaceRoot))
        {
            return new LocalScanResult(
                Array.Empty<LocalRepoEntry>(),
                new[] { $"工作区根目录不存在：{workspaceRoot}" });
        }

        var issues = new ConcurrentBag<string>();
        var candidates = CollectCandidates(workspaceRoot, issues);
        var giteaBase = BuildGiteaBaseUri(config);

        var entries = new ConcurrentBag<LocalRepoEntry>();
        await Parallel.ForEachAsync(
            candidates,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = MaxParallelism,
                CancellationToken = cancellationToken
            },
            async (directory, token) =>
            {
                var fullName = Path.GetFileName(directory);
                if (!RepoNameParser.TryParse(fullName, out var componentId, out var repoType))
                {
                    issues.Add($"未绑定仓库（命名不符合 SRS §6.1）：{fullName}");
                    return;
                }

                RepoFacts facts;
                try
                {
                    facts = await ScanRepoAsync(directory, config, giteaBase, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // 单个仓失败不中断整体：记 UNKNOWN + error（SRS §7.2 优先级 0）。
                    facts = RepoFacts.Failed($"{ex.GetType().Name}: {ex.Message}", directory);
                }

                entries.Add(new LocalRepoEntry(fullName, componentId, repoType, directory, facts));
            }).ConfigureAwait(false);

        var orderedRepos = entries
            .OrderBy(e => e.FullName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.FullName, StringComparer.Ordinal)
            .ToList();
        var orderedIssues = issues.OrderBy(i => i, StringComparer.Ordinal).ToList();
        return new LocalScanResult(orderedRepos, orderedIssues);
    }

    private static List<string> CollectCandidates(string workspaceRoot, ConcurrentBag<string> issues)
    {
        var candidates = new List<string>();
        IEnumerable<string> directories;
        try
        {
            directories = Directory.EnumerateDirectories(workspaceRoot);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            issues.Add($"读取工作区根目录失败：{ex.Message}");
            return candidates;
        }

        foreach (var directory in directories)
        {
            var name = Path.GetFileName(directory);

            // 隐藏目录一律不纳管（含 .trash / .git 等；D-017 扫描必须排除 .trash）。
            if (name.StartsWith('.'))
            {
                continue;
            }

            FileAttributes attributes;
            try
            {
                attributes = File.GetAttributes(directory);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                issues.Add($"读取目录属性失败：{directory}（{ex.GetType().Name}）");
                continue;
            }

            // 跳过 reparse point（junction/symlink/OneDrive 占位），不纳管（SRS §6.3）。
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                issues.Add($"跳过 reparse point：{directory}");
                continue;
            }

            if (!RepoNameParser.TryParse(name, out _, out _))
            {
                issues.Add($"未绑定仓库（命名不符合 SRS §6.1）：{name}");
                continue;
            }

            candidates.Add(directory);
        }

        return candidates;
    }

    private async Task<RepoFacts> ScanRepoAsync(
        string directory,
        AppConfig config,
        Uri? giteaBase,
        CancellationToken cancellationToken)
    {
        var facts = new RepoFacts { LocalPath = directory };

        var hasGitDirectory = Directory.Exists(Path.Combine(directory, ".git"));
        var hasGitFile = File.Exists(Path.Combine(directory, ".git"));
        if (!hasGitDirectory && !hasGitFile)
        {
            // 目录存在但不是 git 仓 → INVALID（SRS §6.2/附录 C F-INVALID-NOTREPO）。
            return facts with { IsValidRepo = false };
        }

        var gitDirectory = ResolveGitDirectory(directory);
        if (gitDirectory is null)
        {
            return facts with { IsValidRepo = false };
        }

        var inProgress = HasInProgressMarker(gitDirectory);
        var stashExists = await _git
            .RunAsync(directory, new[] { "rev-parse", "--verify", "--quiet", "refs/stash" }, cancellationToken)
            .ConfigureAwait(false);

        facts = facts with
        {
            Busy = File.Exists(Path.Combine(gitDirectory, "index.lock")),
            InProgress = inProgress,
            Shallow = File.Exists(Path.Combine(gitDirectory, "shallow")),
            Stashed = stashExists.Success,
            IsSubmodule = File.Exists(Path.Combine(directory, ".gitmodules"))
        };

        // 仓库归属校验：--show-toplevel 必须等于当前目录（SRS §6.3）。
        var topLevel = await _git
            .RunAsync(directory, new[] { "rev-parse", "--show-toplevel" }, cancellationToken)
            .ConfigureAwait(false);
        if (!topLevel.Success)
        {
            return facts with { Error = $"git rev-parse --show-toplevel 失败：{SafeMessage(topLevel.Stderr)}" };
        }

        if (!PathEquals(topLevel.TrimmedStdout, directory))
        {
            return facts with { IsValidRepo = false };
        }

        // origin 校验：必须存在且（配置 Gitea 时）host/org/name 与 Gitea 同名仓一致（SRS §6.2 FR-SCAN-01）。
        var origin = await _git
            .RunAsync(directory, new[] { "remote", "get-url", "origin" }, cancellationToken)
            .ConfigureAwait(false);
        if (!origin.Success)
        {
            return facts with { IsValidRepo = false };
        }

        var directoryName = Path.GetFileName(directory);
        if (!OriginMatches(origin.TrimmedStdout, directoryName, giteaBase, config))
        {
            return facts with { IsValidRepo = false };
        }

        // 一次 porcelain v2 --branch 拿到 HEAD 形态/分支/upstream/ahead-behind/dirty/untracked/unmerged。
        var statusResult = await _git
            .RunAsync(
                directory,
                new[] { "status", "--porcelain=v2", "--branch" },
                cancellationToken)
            .ConfigureAwait(false);
        if (!statusResult.Success)
        {
            return facts with { Error = $"git status 失败：{SafeMessage(statusResult.Stderr)}" };
        }

        var status = ParsePorcelainStatus(statusResult.Stdout);

        // 本地 tag 清单（组件级 TAG_DRIFT 判定，FR-STA-06；M1 不 fetch，tag_moved 暂不可判）。
        var localTags = await _git
            .RunAsync(directory, new[] { "tag", "--list" }, cancellationToken)
            .ConfigureAwait(false);

        facts = facts with
        {
            Branch = status.Branch,
            HeadSha = status.Unborn ? null : status.HeadSha,
            HeadKind = status.Unborn
                ? HeadKind.Unborn
                : status.Detached ? HeadKind.DetachedCommit : HeadKind.Branch,
            DirtyTracked = status.DirtyTracked,
            DirtyCount = status.DirtyCount,
            StagedCount = status.StagedCount,
            UntrackedCount = status.UntrackedCount,
            Unmerged = status.Unmerged,
            LocalTags = localTags.Success ? SplitLines(localTags.Stdout) : Array.Empty<string>()
        };

        if (status.Detached && facts.HeadSha is not null)
        {
            var tagsAtHead = await _git
                .RunAsync(directory, new[] { "tag", "--points-at", "HEAD" }, cancellationToken)
                .ConfigureAwait(false);
            IReadOnlyList<string> tags = tagsAtHead.Success ? SplitLines(tagsAtHead.Stdout) : Array.Empty<string>();
            facts = facts with
            {
                TagsAtHead = tags,
                HeadKind = tags.Count > 0 ? HeadKind.DetachedTag : HeadKind.DetachedCommit
            };
        }

        if (status.Unborn || status.Detached || status.Branch is null)
        {
            return facts;
        }

        return await ResolveUpstreamAsync(directory, facts, status, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>解析 compare_ref（SRS §7.1 D-014）并计算 ahead/behind。</summary>
    private async Task<RepoFacts> ResolveUpstreamAsync(
        string directory,
        RepoFacts facts,
        PorcelainStatus status,
        CancellationToken cancellationToken)
    {
        if (status.UpstreamRef is not null)
        {
            // porcelain v2 给出短名（origin/main），落事实时规范成完整 ref（§8.1 upstream_ref）。
            var upstreamRef = ToFullUpstreamRef(status.UpstreamRef);
            facts = facts with { UpstreamConfigured = true, UpstreamRef = upstreamRef };

            if (status.HasAheadBehind && status.Ahead is not null && status.Behind is not null)
            {
                return facts with { Ahead = status.Ahead, Behind = status.Behind };
            }

            // 有 upstream 配置但无 ahead/behind：先判断远程跟踪分支是否已被删除（UPSTREAM_GONE）。
            var verify = await _git
                .RunAsync(
                    directory,
                    new[] { "rev-parse", "--verify", "--quiet", upstreamRef },
                    cancellationToken)
                .ConfigureAwait(false);
            if (!verify.Success)
            {
                return facts with { UpstreamGone = true };
            }

            var relation = await CountRelationAsync(directory, upstreamRef, cancellationToken).ConfigureAwait(false);
            return relation is null
                ? facts
                : facts with { Ahead = relation.Value.Ahead, Behind = relation.Value.Behind };
        }

        // 未配置 upstream：compare_ref 回退到 refs/remotes/{remote}/{同名分支}（D-014）。
        var remote = await ResolveRemoteNameAsync(directory, cancellationToken).ConfigureAwait(false);
        if (remote is null || string.IsNullOrWhiteSpace(status.Branch))
        {
            return facts;
        }

        var remoteBranch = $"refs/remotes/{remote}/{status.Branch}";
        var remoteVerify = await _git
            .RunAsync(directory, new[] { "rev-parse", "--verify", "--quiet", remoteBranch }, cancellationToken)
            .ConfigureAwait(false);
        if (!remoteVerify.Success)
        {
            // 远程无同名分支 → NO_UPSTREAM(LOCAL_ONLY)。
            return facts;
        }

        var remoteRelation = await CountRelationAsync(directory, remoteBranch, cancellationToken).ConfigureAwait(false);
        return remoteRelation is null
            ? facts with { RemoteBranchExists = true }
            : facts with
            {
                RemoteBranchExists = true,
                Ahead = remoteRelation.Value.Ahead,
                Behind = remoteRelation.Value.Behind
            };
    }

    private static string ToFullUpstreamRef(string shortRef)
    {
        if (shortRef.StartsWith("refs/", StringComparison.Ordinal))
        {
            return shortRef;
        }

        return shortRef.Contains('/', StringComparison.Ordinal)
            ? "refs/remotes/" + shortRef
            : shortRef;
    }

    private async Task<(int Ahead, int Behind)?> CountRelationAsync(
        string directory,
        string compareRef,
        CancellationToken cancellationToken)
    {
        var result = await _git
            .RunAsync(
                directory,
                new[] { "rev-list", "--left-right", "--count", $"HEAD...{compareRef}" },
                cancellationToken)
            .ConfigureAwait(false);
        if (!result.Success)
        {
            return null;
        }

        var parts = result.TrimmedStdout.Split(
            new[] { '\t', ' ', '\r', '\n' },
            StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2
            || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ahead)
            || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var behind))
        {
            return null;
        }

        return (ahead, behind);
    }

    private async Task<string?> ResolveRemoteNameAsync(string directory, CancellationToken cancellationToken)
    {
        var remotes = await _git.RunAsync(directory, new[] { "remote" }, cancellationToken).ConfigureAwait(false);
        if (!remotes.Success)
        {
            return null;
        }

        var names = SplitLines(remotes.Stdout);
        if (names.Count == 0)
        {
            return null;
        }

        var origin = names.FirstOrDefault(n => n.Equals("origin", StringComparison.OrdinalIgnoreCase));
        return origin ?? names[0];
    }

    private static PorcelainStatus ParsePorcelainStatus(string stdout)
    {
        var status = new PorcelainStatus();
        foreach (var line in stdout.Split('\n'))
        {
            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith("# branch.oid ", StringComparison.Ordinal))
            {
                var value = line["# branch.oid ".Length..].Trim();
                if (value.Equals("(initial)", StringComparison.Ordinal))
                {
                    status = status with { Unborn = true };
                }
                else if (value.Length > 0)
                {
                    status = status with { HeadSha = value };
                }

                continue;
            }

            if (line.StartsWith("# branch.head ", StringComparison.Ordinal))
            {
                var value = line["# branch.head ".Length..].Trim();
                status = value.Equals("(detached)", StringComparison.Ordinal)
                    ? status with { Detached = true }
                    : status with { Branch = value };
                continue;
            }

            if (line.StartsWith("# branch.upstream ", StringComparison.Ordinal))
            {
                var value = line["# branch.upstream ".Length..].Trim();
                if (value.Length > 0)
                {
                    status = status with { UpstreamRef = value };
                }

                continue;
            }

            if (line.StartsWith("# branch.ab ", StringComparison.Ordinal))
            {
                var value = line["# branch.ab ".Length..].Trim();
                var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2
                    && parts[0].StartsWith('+')
                    && parts[1].StartsWith('-')
                    && int.TryParse(parts[0][1..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ahead)
                    && int.TryParse(parts[1][1..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var behind))
                {
                    status = status with { HasAheadBehind = true, Ahead = ahead, Behind = behind };
                }

                continue;
            }

            if (line.StartsWith("1 ", StringComparison.Ordinal) || line.StartsWith("2 ", StringComparison.Ordinal))
            {
                // porcelain v2：`1 XY ...` / `2 XY ...`，X=index（staged）状态，Y=worktree 状态。
                var x = line.Length > 2 ? line[2] : ' ';
                var stagedChange = x != ' ' && x != '.';
                status = status with
                {
                    DirtyTracked = true,
                    DirtyCount = status.DirtyCount + 1,
                    StagedCount = stagedChange ? status.StagedCount + 1 : status.StagedCount
                };
                continue;
            }

            if (line.StartsWith("u ", StringComparison.Ordinal))
            {
                status = status with { Unmerged = true };
                continue;
            }

            if (line.StartsWith("? ", StringComparison.Ordinal))
            {
                status = status with { UntrackedCount = status.UntrackedCount + 1 };
            }
        }

        return status;
    }

    private static string? ResolveGitDirectory(string repositoryDirectory)
    {
        var dotGit = Path.Combine(repositoryDirectory, ".git");
        if (Directory.Exists(dotGit))
        {
            return dotGit;
        }

        if (!File.Exists(dotGit))
        {
            return null;
        }

        try
        {
            var content = File.ReadAllText(dotGit).Trim();
            const string prefix = "gitdir:";
            if (!content.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var raw = content[prefix.Length..].Trim();
            if (raw.Length == 0)
            {
                return null;
            }

            var resolved = Path.IsPathRooted(raw)
                ? Path.GetFullPath(raw)
                : Path.GetFullPath(Path.Combine(repositoryDirectory, raw));
            return Directory.Exists(resolved) ? resolved : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool HasInProgressMarker(string gitDirectory) =>
        File.Exists(Path.Combine(gitDirectory, "MERGE_HEAD"))
        || File.Exists(Path.Combine(gitDirectory, "CHERRY_PICK_HEAD"))
        || File.Exists(Path.Combine(gitDirectory, "REVERT_HEAD"))
        || File.Exists(Path.Combine(gitDirectory, "BISECT_LOG"))
        || Directory.Exists(Path.Combine(gitDirectory, "rebase-merge"))
        || Directory.Exists(Path.Combine(gitDirectory, "rebase-apply"));

    private static bool PathEquals(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        static string Normalize(string value) =>
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(value));

        return Normalize(left).Equals(Normalize(right), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// origin URL 与目录名（Gitea 同名仓）比对（SRS §6.3）：支持 https/ssh/scp-like 形式。
    /// 未配置 Gitea 时只校验仓库名；配置后额外校验 host/org。
    /// </summary>
    private static bool OriginMatches(string originUrl, string directoryName, Uri? giteaBase, AppConfig config)
    {
        var normalized = originUrl.Trim();
        if (normalized.Length == 0)
        {
            return false;
        }

        string? host = null;
        string path;

        if (Uri.TryCreate(normalized, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host))
        {
            host = uri.Host;
            path = uri.AbsolutePath;
        }
        else
        {
            // scp-like：git@host:org/name.git
            var colon = normalized.IndexOf(':', StringComparison.Ordinal);
            var at = normalized.IndexOf('@', StringComparison.Ordinal);
            if (colon > 0 && at > 0 && at < colon)
            {
                host = normalized[(at + 1)..colon];
                path = normalized[(colon + 1)..];
            }
            else
            {
                // 本地路径/UNC 不是 Gitea 克隆；仅当未配置 Gitea（本地夹具）时按仓库名宽松校验。
                path = normalized.Replace('\\', '/');
            }
        }

        path = path.TrimEnd('/');
        if (path.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            path = path[..^4];
        }

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return false;
        }

        var repoName = segments[^1];
        if (!repoName.Equals(directoryName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!config.GiteaEnabled || giteaBase is null)
        {
            return true;
        }

        if (host is null || !host.Equals(giteaBase.Host, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var org = config.GiteaOrg;
        return string.IsNullOrWhiteSpace(org)
            || segments.Any(segment => segment.Equals(org, StringComparison.OrdinalIgnoreCase));
    }

    private static Uri? BuildGiteaBaseUri(AppConfig config)
    {
        if (!config.GiteaEnabled || string.IsNullOrWhiteSpace(config.GiteaBaseUrl))
        {
            return null;
        }

        return Uri.TryCreate(config.GiteaBaseUrl.Trim().TrimEnd('/'), UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            ? uri
            : null;
    }

    private static string SafeMessage(string stderr)
    {
        var message = stderr.Trim();
        if (message.Length == 0)
        {
            return "（无 stderr 输出）";
        }

        var firstLine = message.Split('\n')[0].Trim();
        return firstLine.Length > 300 ? firstLine[..300] : firstLine;
    }

    private static List<string> SplitLines(string text) => text
        .Split('\n')
        .Select(line => line.Trim())
        .Where(line => line.Length > 0)
        .ToList();

    /// <summary>porcelain v2 --branch 的解析中间态。</summary>
    private sealed record PorcelainStatus
    {
        public bool Unborn { get; init; }

        public bool Detached { get; init; }

        public string? Branch { get; init; }

        public string? HeadSha { get; init; }

        public string? UpstreamRef { get; init; }

        public bool HasAheadBehind { get; init; }

        public int? Ahead { get; init; }

        public int? Behind { get; init; }

        public bool DirtyTracked { get; init; }

        public int DirtyCount { get; init; }

    public int StagedCount { get; init; }

        public int UntrackedCount { get; init; }

        public bool Unmerged { get; init; }
    }
}
