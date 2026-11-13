namespace MbdGitTool.Core.Domain;

/// <summary>
/// 仓库全名解析（SRS §6.1，决策 D-001）。
/// <para>规则：从最后一个下划线切分，末尾必须是四类生命周期后缀之一（大小写不敏感）；</para>
/// <para>剩余前缀若以四类生命周期词结尾（如 <c>X_SIL_Design</c> 的前缀 <c>X_SIL</c>）则解析失败，</para>
/// <para>解析失败 = 不参与组件绑定，由调用方记入“未绑定仓库”清单（禁止静默丢弃）。</para>
/// </summary>
public static class RepoNameParser
{
    /// <summary>四类生命周期后缀（匹配大小写不敏感）。</summary>
    private static readonly (string Suffix, RepoType Type)[] LifecycleSuffixes =
    {
        ("_Design", RepoType.Design),
        ("_Impl", RepoType.Impl),
        ("_MIL", RepoType.Mil),
        ("_SIL", RepoType.Sil)
    };

    /// <summary>
    /// 仓库全名解析为 (componentId, repoType)。
    /// 解析失败返回 false，且 <paramref name="componentId"/> 为空串、<paramref name="repoType"/> 为 <see cref="RepoType.Other"/>。
    /// </summary>
    public static bool TryParse(string? fullName, out string componentId, out RepoType repoType)
    {
        componentId = string.Empty;
        repoType = RepoType.Other;

        if (string.IsNullOrWhiteSpace(fullName))
        {
            return false;
        }

        foreach (var (suffix, type) in LifecycleSuffixes)
        {
            if (!fullName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var prefix = fullName[..^suffix.Length];

            // 前缀不能为空（纯 `_Design` 不算组件标识），也不能再以生命周期词结尾（避免 `X_SIL` 被误当组件）。
            if (prefix.Length == 0 || EndsWithLifecycleWord(prefix))
            {
                return false;
            }

            componentId = prefix;
            repoType = type;
            return true;
        }

        return false;
    }

    /// <summary>解析失败时抛出，供明确需要“必须可解析”的场景使用。</summary>
    public static (string ComponentId, RepoType RepoType) Parse(string fullName)
    {
        if (!TryParse(fullName, out var componentId, out var repoType))
        {
            throw new FormatException($"仓库全名无法解析为组件：{fullName}");
        }

        return (componentId, repoType);
    }

    private static bool EndsWithLifecycleWord(string prefix)
    {
        foreach (var (suffix, _) in LifecycleSuffixes)
        {
            if (prefix.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
