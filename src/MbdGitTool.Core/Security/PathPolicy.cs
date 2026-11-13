using System.Collections.Generic;
using System.IO;
using MbdGitTool.Core.Contracts;

namespace MbdGitTool.Core.Security;

/// <summary>reparse 链检查结果（<see cref="ReparsePath"/> 为链上第一处 reparse point 的绝对路径）。</summary>
public sealed record ReparseCheck(bool HasReparse, string? ReparsePath)
{
    public static ReparseCheck None { get; } = new(false, null);
}

/// <summary>
/// 路径安全核心（code-plan §4.3 / SRS FR-TREE-04 / FR-SAFE-05）。
/// <para><b>规则</b>：只接受工作区根之下的相对路径；canonical 解析后必须仍在根内；拒绝 <c>..</c>、绝对路径、盘符、UNC、ADS、非法字符、保留设备名、尾随空格/点；</para>
/// <para><c>.git</c> 段一律 <c>E_GIT_INTERNAL</c>，<c>.trash</c> 段 <c>E_PATH_INVALID</c>（恢复流程专用，普通读路径不可见）；</para>
/// <para>canonical 用 <see cref="Path.GetFullPath(string)"/>，比较大小写不敏感（Windows），reparse 链逐级检查。</para>
/// </summary>
public sealed class PathPolicy
{
    /// <summary>相对路径长度上限（防止超长请求拖垮桥/日志）。</summary>
    public const int MaxRelativePathLength = 1024;

    private static readonly char[] InvalidNameChars = { '<', '>', '"', '|', '?', '*' };
    private static readonly char[] Separators = { '\\', '/' };

    public PathPolicy(string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);

        var canonical = TryCanonicalize(workspaceRoot)
            ?? throw new ArgumentException("工作区根目录路径非法", nameof(workspaceRoot));

        Root = canonical;
    }

    /// <summary>规范化的工作区根（无尾部分隔符，盘符根保留 <c>D:\</c>）。</summary>
    public string Root { get; }

    /// <summary>
    /// 解析工作区相对路径（<c>""</c>/<c>null</c> = 根）。失败返回 false 并给出稳定错误码。
    /// </summary>
    public bool TryResolveWorkspacePath(string? relative, out string full, out BridgeError? error) =>
        TryResolveRelative(Root, relative, out full, out error);

    /// <summary>
    /// 解析仓内相对路径：除 <paramref name="repoRoot"/> 内收口外，还要求仓根本身位于工作区根内（防跨仓逃逸）。
    /// </summary>
    public bool TryResolveRepoPath(string repoRoot, string? relativeToRepo, out string full, out BridgeError? error)
    {
        full = string.Empty;
        error = null;

        var repoCanonical = TryCanonicalize(repoRoot);
        if (repoCanonical is null)
        {
            error = Error(BridgeErrorCodes.PathInvalid, "仓库路径非法", ("path", relativeToRepo));
            return false;
        }

        if (!IsWithin(Root, repoCanonical))
        {
            error = Error(BridgeErrorCodes.PathOutsideRoot, "仓库路径越出工作区根", ("repoRoot", repoCanonical));
            return false;
        }

        return TryResolveRelative(repoCanonical, relativeToRepo, out full, out error);
    }

    /// <summary><paramref name="candidateCanonical"/> 是否等于 <paramref name="parentCanonical"/> 或位于其下（大小写不敏感）。</summary>
    public bool IsWithin(string parentCanonical, string candidateCanonical)
    {
        if (string.IsNullOrWhiteSpace(parentCanonical) || string.IsNullOrWhiteSpace(candidateCanonical))
        {
            return false;
        }

        var parent = TrimSeparator(parentCanonical);
        var candidate = TrimSeparator(candidateCanonical);

        if (candidate.Equals(parent, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var prefix = parent.EndsWith(Path.DirectorySeparatorChar) || parent.EndsWith(Path.AltDirectorySeparatorChar)
            ? parent
            : parent + Path.DirectorySeparatorChar;
        return candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>逐级检查 reparse point（根自身允许为网络盘/OneDrive，不检查）。</summary>
    public ReparseCheck CheckReparseChain(string fullPath, bool includeTarget = false)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
        {
            return ReparseCheck.None;
        }

        var canonical = TryCanonicalize(fullPath);
        if (canonical is null || !IsWithin(Root, canonical))
        {
            return ReparseCheck.None;
        }

        var relative = Path.GetRelativePath(Root, canonical);
        if (relative is "." or "")
        {
            return ReparseCheck.None;
        }

        var segments = relative.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
        var current = Root;
        for (var index = 0; index < segments.Length; index++)
        {
            current = Path.Combine(current, segments[index]);
            var isTarget = index == segments.Length - 1;
            if (isTarget && !includeTarget)
            {
                break;
            }

            if (IsReparsePoint(current))
            {
                return new ReparseCheck(true, current);
            }
        }

        return ReparseCheck.None;
    }

    /// <summary>单一路径本身是否为 reparse point（不存在/不可读返回 false）。</summary>
    public static bool IsReparsePoint(string fullPath)
    {
        try
        {
            var attributes = File.GetAttributes(fullPath);
            return (attributes & FileAttributes.ReparsePoint) != 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }

    /// <summary>绝对路径 → 工作区相对 posix 路径；越界返回空串。</summary>
    public string ToWorkspaceRelative(string fullPath)
    {
        var canonical = TryCanonicalize(fullPath);
        if (canonical is null || !IsWithin(Root, canonical))
        {
            return string.Empty;
        }

        var relative = Path.GetRelativePath(Root, canonical);
        return relative is "." or "" ? string.Empty : relative.Replace('\\', '/');
    }

    /// <summary>绝对路径 → 仓相对 posix 路径；越界返回空串。</summary>
    public string ToRepoRelative(string repoRoot, string fullPath)
    {
        var repoCanonical = TryCanonicalize(repoRoot);
        var canonical = TryCanonicalize(fullPath);
        if (repoCanonical is null || canonical is null || !IsWithin(repoCanonical, canonical))
        {
            return string.Empty;
        }

        var relative = Path.GetRelativePath(repoCanonical, canonical);
        return relative is "." or "" ? string.Empty : relative.Replace('\\', '/');
    }

    /// <summary>把 posix/反斜杠相对路径规范为 <c>/</c> 分隔、去尾部分隔符（保留前导分隔符以便后续拒绝绝对路径）。</summary>
    public static string NormalizeRelative(string? relative)
    {
        if (string.IsNullOrWhiteSpace(relative))
        {
            return string.Empty;
        }

        return relative.Replace('\\', '/').TrimEnd('/');
    }

    /// <summary><see cref="Path.GetFullPath(string)"/> 的容错版本（非法返回 null，不抛异常）。</summary>
    public static string? TryCanonicalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return TrimSeparator(Path.GetFullPath(path));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or IOException)
        {
            return null;
        }
    }

    private bool TryResolveRelative(string parent, string? relative, out string full, out BridgeError? error)
    {
        full = string.Empty;
        error = null;

        if (string.IsNullOrEmpty(relative))
        {
            full = parent;
            return true;
        }

        if (relative.Length > MaxRelativePathLength)
        {
            error = Error(BridgeErrorCodes.PathInvalid, $"路径过长（上限 {MaxRelativePathLength}）");
            return false;
        }

        if (relative.Contains('\0'))
        {
            error = Error(BridgeErrorCodes.PathInvalid, "路径包含非法字符 \\0");
            return false;
        }

        // 绝对路径/盘符/UNC/根起始分隔符：一律视为越界（UI 只允许传相对路径）。
        if (Path.IsPathFullyQualified(relative)
            || relative.StartsWith(@"\\", StringComparison.Ordinal)
            || relative.StartsWith("//", StringComparison.Ordinal)
            || relative[0] is '\\' or '/')
        {
            error = Error(BridgeErrorCodes.PathOutsideRoot, "拒绝对路径/UNC/盘符，只允许工作区内相对路径", ("path", relative));
            return false;
        }

        var segments = relative.Split(Separators);
        foreach (var segment in segments)
        {
            var segmentError = ValidateSegment(segment);
            if (segmentError is not null)
            {
                error = segmentError;
                return false;
            }
        }

        string combined;
        try
        {
            combined = TrimSeparator(Path.GetFullPath(Path.Combine(parent, relative)));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or IOException)
        {
            error = Error(BridgeErrorCodes.PathInvalid, "路径规范化失败");
            return false;
        }

        if (!IsWithin(parent, combined))
        {
            error = Error(BridgeErrorCodes.PathOutsideRoot, "路径规范化后越出根目录", ("path", relative));
            return false;
        }

        // parent 之下的相对段：.git/.trash 不允许出现在任何读路径请求中。
        var relFromParent = Path.GetRelativePath(parent, combined).Replace('\\', '/');
        foreach (var segment in relFromParent.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment.Equals(".git", StringComparison.OrdinalIgnoreCase))
            {
                error = Error(BridgeErrorCodes.GitInternal, ".git 内部路径禁止访问", ("path", relative));
                return false;
            }

            if (segment.Equals(".trash", StringComparison.OrdinalIgnoreCase))
            {
                error = Error(BridgeErrorCodes.PathInvalid, ".trash 仅恢复流程可见", ("path", relative));
                return false;
            }
        }

        full = combined;
        return true;
    }

    private static BridgeError? ValidateSegment(string segment)
    {
        if (segment.Length == 0 || segment is "." or "..")
        {
            return Error(BridgeErrorCodes.PathInvalid, "路径包含空段/./..", ("segment", segment));
        }

        // ':' 同时覆盖 ADS（file:stream）与盘符/驱动器相对路径。
        if (segment.Contains(':', StringComparison.Ordinal))
        {
            return Error(BridgeErrorCodes.PathInvalid, "路径包含非法字符 ':'（ADS/盘符）", ("segment", segment));
        }

        if (segment.IndexOfAny(InvalidNameChars) >= 0)
        {
            return Error(BridgeErrorCodes.PathInvalid, "路径包含 Windows 非法字符", ("segment", segment));
        }

        if (IsReservedDeviceName(segment))
        {
            return Error(BridgeErrorCodes.PathInvalid, "路径包含 Windows 保留设备名", ("segment", segment));
        }

        if (!string.Equals(segment, segment.Trim(), StringComparison.Ordinal))
        {
            return Error(BridgeErrorCodes.PathInvalid, "路径段不得以空格开头/结尾", ("segment", segment));
        }

        if (segment.EndsWith('.'))
        {
            return Error(BridgeErrorCodes.PathInvalid, "路径段不得以点结尾", ("segment", segment));
        }

        return null;
    }

    private static bool IsReservedDeviceName(string segment)
    {
        var name = segment;
        var dot = name.IndexOf('.', StringComparison.Ordinal);
        if (dot >= 0)
        {
            name = name[..dot];
        }

        name = name.TrimEnd(' ', '.');
        if (name.Length == 0)
        {
            return false;
        }

        if (name.Equals("CON", StringComparison.OrdinalIgnoreCase)
            || name.Equals("PRN", StringComparison.OrdinalIgnoreCase)
            || name.Equals("AUX", StringComparison.OrdinalIgnoreCase)
            || name.Equals("NUL", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (name.Length == 4
            && (name.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("LPT", StringComparison.OrdinalIgnoreCase))
            && name[3] is >= '1' and <= '9')
        {
            return true;
        }

        return false;
    }

    private static string TrimSeparator(string path) => Path.TrimEndingDirectorySeparator(path);

    private static BridgeError Error(
        string code,
        string message,
        params (string Key, object? Value)[] details)
    {
        var dictionary = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (key, value) in details)
        {
            dictionary[key] = value;
        }

        return new BridgeError(code, message, dictionary.Count == 0 ? null : dictionary);
    }
}
