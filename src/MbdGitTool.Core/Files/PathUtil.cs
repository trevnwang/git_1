using System.IO;

namespace MbdGitTool.Core.Files;

/// <summary>长路径工具（code-plan §4.4.2）：枚举/属性读取使用 <c>\\?\</c> 扩展路径，对外输出仍为普通路径。</summary>
public static class PathUtil
{
    private const int MaxPath = 260;

    /// <summary>超过 MAX_PATH 时加 <c>\\?\</c>（UNC 转 <c>\\?\UNC\</c>）；已是扩展路径原样返回。</summary>
    public static string EnsureExtendedPath(string absolutePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(absolutePath);

        if (absolutePath.StartsWith(@"\\?\", StringComparison.Ordinal))
        {
            return absolutePath;
        }

        if (absolutePath.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return absolutePath.Length >= MaxPath ? @"\\?\UNC\" + absolutePath[2..] : absolutePath;
        }

        return absolutePath.Length >= MaxPath ? @"\\?\" + absolutePath : absolutePath;
    }

    /// <summary>去掉 <c>\\?\</c>/<c>\\?\UNC\</c> 前缀，得到普通路径。</summary>
    public static string StripExtendedPrefix(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (path.StartsWith(@"\\?\UNC\", StringComparison.Ordinal))
        {
            return @"\\" + path[8..];
        }

        return path.StartsWith(@"\\?\", StringComparison.Ordinal) ? path[4..] : path;
    }
}
