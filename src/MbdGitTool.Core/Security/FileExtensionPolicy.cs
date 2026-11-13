namespace MbdGitTool.Core.Security;

/// <summary>openPath 扩展名分级（code-plan §3.8 / spec §4.6）。</summary>
public enum ExtensionClass
{
    Directory = 0,
    SafeText = 1,
    Dangerous = 2,
    Other = 3
}

/// <summary>
/// 打开白名单/危险扩展名策略（FR-DETAIL-03 / FR-SAFE-05）。
/// <para>白名单 = spec 的 <c>.m .c .h .slx .mdl .json .xml .yaml .yml .txt .md .csv</c> + 只读可登记扩展；</para>
/// <para>危险扩展名（可执行/脚本/快捷方式）与未知类型一律需要二次确认（由 Host 返回 <c>E_CONFIRM_REQUIRED</c>）。</para>
/// </summary>
public static class FileExtensionPolicy
{
    // FR-DETAIL-03 白名单 + 只读可登记扩展（code-plan §3.8）。
    private static readonly HashSet<string> SafeTextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".m", ".c", ".h", ".slx", ".mdl", ".json", ".xml", ".yaml", ".yml", ".txt", ".md", ".csv",
        ".hpp", ".cpp", ".log", ".ini", ".gitignore", ".gitattributes", ".editorconfig", ".cs", ".props", ".targets"
    };

    // code-plan §3.8 危险扩展名表。
    private static readonly HashSet<string> DangerousExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".bat", ".cmd", ".ps1", ".vbs", ".js", ".jse", ".wsf", ".msi", ".lnk", ".scr",
        ".reg", ".url", ".com", ".pif", ".cpl", ".jar", ".sh"
    };

    /// <summary>扩展名等级：目录=DIRECTORY；白名单=SAFE_TEXT；危险=DANGEROUS；其余=OTHER。</summary>
    public static ExtensionClass Classify(string? extension, bool isDirectory)
    {
        if (isDirectory)
        {
            return ExtensionClass.Directory;
        }

        if (string.IsNullOrEmpty(extension))
        {
            return ExtensionClass.Other;
        }

        var normalized = Normalize(extension);
        if (SafeTextExtensions.Contains(normalized))
        {
            return ExtensionClass.SafeText;
        }

        return DangerousExtensions.Contains(normalized) ? ExtensionClass.Dangerous : ExtensionClass.Other;
    }

    /// <summary>按文件路径分级（目录由调用方用 <paramref name="isDirectory"/> 传入）。</summary>
    public static ExtensionClass ClassifyPath(string? path, bool isDirectory) =>
        Classify(GetExtension(path), isDirectory);

    /// <summary>形态固定为小写、含前导点的扩展名；无扩展名返回 null。</summary>
    public static string? GetExtension(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var extension = Path.GetExtension(path.TrimEnd(' ', '.'));
        return string.IsNullOrEmpty(extension) ? null : Normalize(extension);
    }

    /// <summary>是否属于“未确认即可直开”的白名单（目录也算直开）。</summary>
    public static bool IsDirectOpenAllowed(string? path, bool isDirectory)
    {
        var kind = ClassifyPath(path, isDirectory);
        return kind is ExtensionClass.Directory or ExtensionClass.SafeText;
    }

    /// <summary>wire 名称：DIRECTORY/SAFE_TEXT/DANGEROUS/OTHER。</summary>
    public static string ToWireName(ExtensionClass extensionClass) => extensionClass switch
    {
        ExtensionClass.Directory => "DIRECTORY",
        ExtensionClass.SafeText => "SAFE_TEXT",
        ExtensionClass.Dangerous => "DANGEROUS",
        _ => "OTHER"
    };

    private static string Normalize(string extension) =>
        extension.StartsWith('.') ? extension.ToLowerInvariant() : "." + extension.ToLowerInvariant();
}
