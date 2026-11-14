using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MbdGitTool.Host.Services;

/// <summary>
/// 应用配置（M1，SRS §9.1 / 附录 D）。
/// <para>配置文件：<c>%LOCALAPPDATA%\MbdGitTool\config.json</c>，字段 <c>workspaceRoot</c> / <c>gitea.baseUrl</c> / <c>gitea.org</c> / <c>gitea.token</c>；</para>
/// <para>环境变量覆盖：<c>MBD_WORKSPACE_ROOT</c> / <c>MBD_GITEA_URL</c> / <c>MBD_GITEA_ORG</c> / <c>MBD_GITEA_TOKEN</c>；</para>
/// <para><b>安全约束：token 禁止写入日志</b>（<see cref="ToString"/> 已脱敏，调用方也不得打印原值）。</para>
/// </summary>
public sealed record AppConfig
{
    public string? WorkspaceRoot { get; init; }

    public string? GiteaBaseUrl { get; init; }

    public string? GiteaOrg { get; init; }

    /// <summary>PAT；可为空（Gitea 公开组织只读接口）。禁止日志/诊断包明文输出。</summary>
    public string? GiteaToken { get; init; }

    /// <summary>
    /// 是否显式允许对 <c>http://</c> 的 Gitea 注入 PAT（P1-4 显式配置）。
    /// <para>默认 <c>false</c>：token 非空且 base 为 http 时网络 git 动作拒绝（<c>E_GITEA_INSECURE_HTTP</c>），
    /// 避免 <c>Authorization: Basic</c> 明文外发。</para>
    /// <para>开启方式：config.json <c>gitea.allowInsecureHttp=true</c>，或环境变量 <c>MBD_GITEA_ALLOW_INSECURE_HTTP=true</c>。</para>
    /// </summary>
    public bool AllowInsecureHttp { get; init; }

    /// <summary>配置加载问题（文件缺失/JSON 非法等）；不影响环境变量覆盖后的可用配置。</summary>
    public string? LoadError { get; init; }

    /// <summary>Gitea 只读同步是否可用（baseUrl + org 均非空）。</summary>
    [JsonIgnore]
    public bool GiteaEnabled =>
        !string.IsNullOrWhiteSpace(GiteaBaseUrl) && !string.IsNullOrWhiteSpace(GiteaOrg);

    /// <summary>是否配置了工作区根目录。</summary>
    [JsonIgnore]
    public bool HasWorkspace => !string.IsNullOrWhiteSpace(WorkspaceRoot);

    /// <summary>默认配置文件路径（12.7 数据落点）。</summary>
    public static string DefaultConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MbdGitTool",
        "config.json");

    /// <summary>
    /// 加载配置：先读 JSON 文件（可选），再用环境变量覆盖。任何 IO/解析失败都不抛异常。
    /// </summary>
    public static AppConfig Load(string? configPath = null)
    {
        var path = configPath ?? DefaultConfigPath;
        var file = new ConfigFile();
        string? loadError = null;

        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
                {
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                };
                file = JsonSerializer.Deserialize<ConfigFile>(json, options) ?? new ConfigFile();
            }
            else
            {
                loadError = $"未找到配置文件：{path}";
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // 不记录文件内容；仅记录异常类型（token 不落日志）。
            loadError = $"配置文件读取失败（{ex.GetType().Name}）：{path}";
        }

        var workspaceRoot = Override("MBD_WORKSPACE_ROOT", file.WorkspaceRoot);
        var baseUrl = Override("MBD_GITEA_URL", file.Gitea?.BaseUrl);
        var org = Override("MBD_GITEA_ORG", file.Gitea?.Org);
        var token = Override("MBD_GITEA_TOKEN", file.Gitea?.Token);
        var allowInsecure = ParseBool(Environment.GetEnvironmentVariable("MBD_GITEA_ALLOW_INSECURE_HTTP"))
            ?? file.Gitea?.AllowInsecureHttp
            ?? false;

        return new AppConfig
        {
            WorkspaceRoot = Normalize(workspaceRoot),
            GiteaBaseUrl = Normalize(baseUrl),
            GiteaOrg = Normalize(org),
            GiteaToken = string.IsNullOrWhiteSpace(token) ? null : token.Trim(),
            AllowInsecureHttp = allowInsecure,
            LoadError = loadError
        };
    }

    /// <summary>脱敏描述（禁止包含 token 明文，FR-CFG-03/FR-SAFE-03）。</summary>
    public override string ToString()
    {
        var tokenState = string.IsNullOrEmpty(GiteaToken) ? "<empty>" : "<redacted>";

        return $"AppConfig {{ WorkspaceRoot = {WorkspaceRoot ?? "<unset>"}, GiteaBaseUrl = {GiteaBaseUrl ?? "<unset>"}, "
            + $"GiteaOrg = {GiteaOrg ?? "<unset>"}, GiteaToken = {tokenState}, AllowInsecureHttp = {AllowInsecureHttp} }}";
    }

    private static bool? ParseBool(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "1" or "true" or "yes" or "on" => true,
            "0" or "false" or "no" or "off" => false,
            _ => null
        };
    }

    private static string? Override(string environmentVariable, string? currentValue)
    {
        var value = Environment.GetEnvironmentVariable(environmentVariable);
        return value ?? currentValue;
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>config.json 的原始结构（只用于反序列化）。</summary>
    private sealed class ConfigFile
    {
        public string? WorkspaceRoot { get; set; }

        public GiteaConfig? Gitea { get; set; }
    }

    private sealed class GiteaConfig
    {
        public string? BaseUrl { get; set; }

        public string? Org { get; set; }

        public string? Token { get; set; }

        public bool? AllowInsecureHttp { get; set; }
    }
}
