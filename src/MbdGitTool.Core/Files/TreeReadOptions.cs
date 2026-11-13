using MbdGitTool.Core.Contracts;

namespace MbdGitTool.Core.Files;

/// <summary>单目录读取选项（code-plan §4.4.1）。</summary>
/// <param name="Depth">0=只返回元数据（不枚举）；1=目录直接子项；2=子目录再内联一层 children。</param>
/// <param name="Limit">单目录最多返回条目数（1..2000，spec §4.5）。</param>
/// <param name="IncludeGitStatus">是否查 gitStatus（Host 侧注入映射；关闭则全部 NONE）。</param>
/// <param name="IncludeIgnored">是否做 ignored 探测（Host 侧注入；关闭则全部 false）。</param>
/// <param name="IncludeHidden">是否包含隐藏项（OS Hidden/System 与 <c>.</c> 开头；<c>.git</c>/<c>.trash</c> 永远排除）。</param>
public sealed record TreeReadOptions(
    int Depth = 1,
    int Limit = 2000,
    bool IncludeGitStatus = true,
    bool IncludeIgnored = false,
    bool IncludeHidden = false);

/// <summary>单目录读取结果（截断标记 + 实际扫描数 + 告警码）。</summary>
public sealed record DirectoryPage(
    IReadOnlyList<TreeNodeDto> Entries,
    bool Truncated,
    int TotalEntries,
    IReadOnlyList<string> Warnings)
{
    public static DirectoryPage Empty { get; } = new(
        Array.Empty<TreeNodeDto>(),
        false,
        0,
        Array.Empty<string>());
}

/// <summary>树节点注解（gitStatus / submodule）：由 Host 提供查表函数，Core 只做纯枚举/排序。</summary>
public sealed record TreeAnnotationProvider(
    Func<string, string>? GitStatus = null,
    Func<string, bool>? Submodule = null);
