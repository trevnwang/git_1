using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MbdGitTool.Core.Files;

namespace MbdGitTool.Host.Services;

/// <summary>
/// git 只读注解索引（code-plan §4.4.3/§4.4.4）：
/// <list type="bullet">
/// <item><c>gitStatus</c>：每仓一次 <c>git status --porcelain=v2 -z --untracked-files=all</c>，解析 path→状态，缓存 30s；</item>
/// <item><c>submodule</c>：读仓根 <c>.gitmodules</c> 的 path 列表，缓存 30s；</item>
/// <item><c>ignored</c>：按需 <c>git check-ignore --stdin -z</c> 批探测，缓存 5s（默认关闭，M1 UI 不使用）。</item>
/// </list>
/// <para><b>只读</b>：参数数组、<c>--no-optional-locks</c>、<c>GIT_TERMINAL_PROMPT=0</c>、超时；失败降级为空并记 WARN，不阻塞树。</para>
/// </summary>
public sealed class GitStatusIndex
{
    public const string StatusNone = "NONE";
    public const string StatusModified = "MODIFIED";
    public const string StatusAdded = "ADDED";
    public const string StatusDeleted = "DELETED";
    public const string StatusUntracked = "UNTRACKED";
    public const string StatusConflict = "CONFLICT";

    private static readonly TimeSpan StatusTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan IgnoreTimeout = TimeSpan.FromSeconds(5);
    private static readonly IReadOnlyDictionary<string, string> EmptyStatus =
        new Dictionary<string, string>(StringComparer.Ordinal);
    private static readonly IReadOnlySet<string> EmptySet =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private readonly GitCli _git;
    private readonly TtlCache<IReadOnlyDictionary<string, string>> _statusCache = new(TimeSpan.FromSeconds(30), 16);
    private readonly TtlCache<IReadOnlySet<string>> _submoduleCache = new(TimeSpan.FromSeconds(30), 16);
    private readonly TtlCache<IReadOnlySet<string>> _ignoredCache = new(TimeSpan.FromSeconds(5), 256);

    public GitStatusIndex(GitCli? git = null) => _git = git ?? new GitCli();

    /// <summary>仓相对 posix 路径 → gitStatus；git 失败/超时返回空表（树全部 NONE）。</summary>
    public async Task<IReadOnlyDictionary<string, string>> GetStatusAsync(
        string repoRoot,
        CancellationToken cancellationToken)
    {
        var key = NormalizePath(repoRoot);
        if (key is not null && _statusCache.TryGet(key, out var cached))
        {
            return cached;
        }

        IReadOnlyDictionary<string, string> map;
        var result = await _git
            .RunAsync(
                repoRoot,
                new[] { "status", "--porcelain=v2", "-z", "--untracked-files=all" },
                cancellationToken,
                StatusTimeout)
            .ConfigureAwait(false);

        if (result.Success)
        {
            map = ParseStatus(result.Stdout);
        }
        else
        {
            HostLog.Warn($"git status 失败（gitStatus 降级 NONE）：{SafeGitError(result.Stderr)}");
            map = EmptyStatus;
        }

        if (key is not null)
        {
            _statusCache.Set(key, map);
        }

        return map;
    }

    /// <summary>仓内已登记的 submodule 目录（仓相对 posix 路径集合）；无 .gitmodules 返回空集。</summary>
    public async Task<IReadOnlySet<string>> GetSubmodulesAsync(
        string repoRoot,
        CancellationToken cancellationToken)
    {
        var key = NormalizePath(repoRoot);
        if (key is not null && _submoduleCache.TryGet(key, out var cached))
        {
            return cached;
        }

        IReadOnlySet<string> paths = EmptySet;
        try
        {
            var gitModules = Path.Combine(repoRoot, ".gitmodules");
            if (File.Exists(gitModules))
            {
                paths = ParseGitModules(await File.ReadAllTextAsync(gitModules, cancellationToken).ConfigureAwait(false));
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            HostLog.Warn($"读取 .gitmodules 失败：{ex.GetType().Name}");
        }

        if (key is not null)
        {
            _submoduleCache.Set(key, paths);
        }

        return paths;
    }

    /// <summary>对给定仓相对路径批探测 ignored（includeIgnored=true 才调用；失败返回空集）。</summary>
    public async Task<IReadOnlySet<string>> CheckIgnoredAsync(
        string repoRoot,
        IEnumerable<string> relativePaths,
        CancellationToken cancellationToken)
    {
        var items = relativePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (items.Length == 0)
        {
            return EmptySet;
        }

        var key = NormalizePath(repoRoot) + "|" + string.Join('\u001f', items);
        if (_ignoredCache.TryGet(key, out var cached))
        {
            return cached;
        }

        var ignored = await RunCheckIgnoreAsync(repoRoot, items, cancellationToken).ConfigureAwait(false);
        _ignoredCache.Set(key, ignored);
        return ignored;
    }

    /// <summary>清空全部注解缓存（refresh 后）。</summary>
    public void InvalidateAll()
    {
        _statusCache.InvalidateAll();
        _submoduleCache.InvalidateAll();
        _ignoredCache.InvalidateAll();
    }

    private async Task<IReadOnlySet<string>> RunCheckIgnoreAsync(
        string repoRoot,
        IReadOnlyList<string> relativePaths,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _git.GitExecutable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        startInfo.ArgumentList.Add("--no-optional-locks");
        startInfo.ArgumentList.Add("-C");
        startInfo.ArgumentList.Add(repoRoot);
        startInfo.ArgumentList.Add("check-ignore");
        startInfo.ArgumentList.Add("--stdin");
        startInfo.ArgumentList.Add("-z");
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        startInfo.Environment["GIT_OPTIONAL_LOCKS"] = "0";

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                return EmptySet;
            }
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            HostLog.Warn($"git check-ignore 启动失败：{ex.GetType().Name}");
            return EmptySet;
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(IgnoreTimeout);

        try
        {
            var stdin = process.StandardInput;
            await stdin.WriteAsync(string.Join('\0', relativePaths)).ConfigureAwait(false);
            await stdin.WriteAsync('\0').ConfigureAwait(false);
            await stdin.FlushAsync(timeoutSource.Token).ConfigureAwait(false);
            stdin.Close();

            var stdoutTask = process.StandardOutput.ReadToEndAsync(timeoutSource.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(timeoutSource.Token);
            await process.WaitForExitAsync(timeoutSource.Token).ConfigureAwait(false);
            var stdout = await stdoutTask.ConfigureAwait(false);
            _ = await stderrTask.ConfigureAwait(false);

            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var token in stdout.Split('\0', StringSplitOptions.RemoveEmptyEntries))
            {
                set.Add(token);
            }

            return set;
        }
        catch (OperationCanceledException)
        {
            KillProcessTree(process);
            return EmptySet;
        }
        catch (IOException)
        {
            KillProcessTree(process);
            return EmptySet;
        }
    }

    /// <summary>解析 porcelain v2 -z 输出（path→状态）。</summary>
    internal static IReadOnlyDictionary<string, string> ParseStatus(string stdout)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(stdout))
        {
            return map;
        }

        var tokens = stdout.Split('\0');
        for (var index = 0; index < tokens.Length; index++)
        {
            var token = tokens[index];
            if (token.Length == 0)
            {
                continue;
            }

            switch (token[0])
            {
                case '?':
                {
                    var path = PathAfterFields(token, 1);
                    if (path is not null)
                    {
                        map[path] = StatusUntracked;
                    }

                    break;
                }

                case 'u':
                {
                    var path = PathAfterFields(token, 10);
                    if (path is not null)
                    {
                        map[path] = StatusConflict;
                    }

                    break;
                }

                case '1':
                {
                    var path = PathAfterFields(token, 8);
                    if (path is not null)
                    {
                        map[path] = MapXyStatus(token);
                    }

                    break;
                }

                case '2':
                {
                    var path = PathAfterFields(token, 9);
                    if (path is not null)
                    {
                        map[path] = MapXyStatus(token);
                    }

                    // rename/copy：下一条 token 是原路径，跳过。
                    index++;
                    break;
                }

                default:
                    break;
            }
        }

        return map;
    }

    internal static IReadOnlySet<string> ParseGitModules(string content)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith(';') || line.StartsWith('['))
            {
                continue;
            }

            var equals = line.IndexOf('=', StringComparison.Ordinal);
            if (equals <= 0)
            {
                continue;
            }

            var name = line[..equals].Trim();
            if (!name.Equals("path", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = line[(equals + 1)..].Trim().Replace('\\', '/').Trim('/');
            if (value.Length > 0)
            {
                paths.Add(value);
            }
        }

        return paths;
    }

    private static string MapXyStatus(string token)
    {
        if (token.Length < 4)
        {
            return StatusModified;
        }

        var xy = token.AsSpan(2, 2);
        if (xy.Contains('U'))
        {
            return StatusConflict;
        }

        if (xy.Contains('A'))
        {
            return StatusAdded;
        }

        if (xy.Contains('D'))
        {
            return StatusDeleted;
        }

        return StatusModified;
    }

    private static string? PathAfterFields(string token, int fieldCount)
    {
        var index = -1;
        for (var field = 0; field < fieldCount; field++)
        {
            index = token.IndexOf(' ', index + 1);
            if (index < 0)
            {
                return null;
            }
        }

        return index + 1 < token.Length ? token[(index + 1)..] : null;
    }

    private static void KillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // 忽略：进程可能已退出。
        }
    }

    private static string SafeGitError(string stderr)
    {
        var first = stderr.Trim().Split('\n')[0].Trim();
        if (first.Length == 0)
        {
            return "（无 stderr）";
        }

        return HostLog.Redact(first.Length > 300 ? first[..300] : first);
    }

    private static string? NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or IOException)
        {
            return null;
        }
    }
}
