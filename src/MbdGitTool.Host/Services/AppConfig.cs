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

    /// <summary>
    /// 审计-lite（G-P0-2/AC-OP-33）目录；为空时使用默认
    /// <c>%LOCALAPPDATA%\MbdGitTool\audit</c>（文件 <c>operations.jsonl</c>）。
    /// <para>仅供测试/诊断注入临时目录，不写入 token 等敏感信息。</para>
    /// </summary>
    public string? AuditRoot { get; init; }

    /// <summary>
    /// M2b 作业持久化目录；为空时使用默认 <c>%LOCALAPPDATA%\MbdGitTool\jobs</c>（文件 <c>jobs.json</c>）。
    /// <para>仅供测试注入临时目录（避免单测污染真实用户数据）。</para>
    /// </summary>
    public string? JobsRoot { get; init; }

    /// <summary>
    /// M2b-2 SQLite 审计库文件；为空时使用默认 <c>%LOCALAPPDATA%\MbdGitTool\db\mbd.db</c>。
    /// <para>仅供测试注入临时路径（避免单测污染真实用户数据）。</para>
    /// </summary>
    public string? DatabasePath { get; init; }

    /// <summary>
    /// M2b-2 审计导出目录；为空时使用默认 <c>%LOCALAPPDATA%\MbdGitTool\exports</c>。
    /// <para>仅供测试注入临时目录（避免单测污染真实用户数据）。</para>
    /// </summary>
    public string? ExportsRoot { get; init; }

    /// <summary>
    /// M2b-2b 写前 journal 目录（FR-SAFE-04；文件 <c>operations.jsonl</c>）；为空时使用默认
    /// <c>%LOCALAPPDATA%\MbdGitTool\journal</c>；仅注入 <see cref="AuditRoot"/> 时落在其 <c>journal</c> 子目录。
    /// <para>仅供测试注入临时目录（避免单测污染真实用户数据）。</para>
    /// </summary>
    public string? JournalRoot { get; init; }

    /// <summary>
    /// 审计保留天数（<c>audit.keepDays</c> / config.json <c>auditRetentionDays</c>，默认 90，范围 1–3650）：
    /// <c>operation_log</c> / JSONL 中早于该天数的行由保留清理任务删除（SRS FR-OP-09 要求 ≥90 天）。
    /// </summary>
    public int AuditRetentionDays { get; init; } = DefaultAuditRetentionDays;

    /// <summary>
    /// 备份 ref 保留天数（<c>backup.retentionDays</c>，默认 30，SRS 附录 D 范围 7–90）：
    /// <c>refs/backup/sync/*</c> 超期由本地保留清理任务回收（仅本地 ref，不触碰远端；D-017/H-05）。
    /// </summary>
    public int BackupRetentionDays { get; init; } = DefaultBackupRetentionDays;

    /// <summary>审计保留天数默认值（SRS FR-OP-09：≥90 天）。</summary>
    public const int DefaultAuditRetentionDays = 90;

    /// <summary>备份 ref 保留天数默认值（SRS 附录 D / D-017：30 天）。</summary>
    public const int DefaultBackupRetentionDays = 30;

    /// <summary>配置加载问题（文件缺失/JSON 非法等）；不影响环境变量覆盖后的可用配置。</summary>
    public string? LoadError { get; init; }

    /// <summary>规范化审计保留天数（缺省/越界回落到默认；读取方也应按范围夹取）。</summary>
    public int EffectiveAuditRetentionDays => ClampDays(AuditRetentionDays, DefaultAuditRetentionDays, min: 90, max: 3650);

    /// <summary>规范化备份 ref 保留天数（附录 D：7–90；缺省/越界回落到默认）。</summary>
    public int EffectiveBackupRetentionDays => ClampDays(BackupRetentionDays, DefaultBackupRetentionDays, min: 7, max: 90);

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
        var auditRetention = ParseInt(Environment.GetEnvironmentVariable("MBD_AUDIT_RETENTION_DAYS"))
            ?? file.AuditRetentionDays
            ?? DefaultAuditRetentionDays;
        var backupRetention = ParseInt(Environment.GetEnvironmentVariable("MBD_BACKUP_RETENTION_DAYS"))
            ?? file.BackupRetentionDays
            ?? DefaultBackupRetentionDays;

        return new AppConfig
        {
            WorkspaceRoot = Normalize(workspaceRoot),
            GiteaBaseUrl = Normalize(baseUrl),
            GiteaOrg = Normalize(org),
            GiteaToken = string.IsNullOrWhiteSpace(token) ? null : token.Trim(),
            AllowInsecureHttp = allowInsecure,
            AuditRetentionDays = ClampDays(auditRetention, DefaultAuditRetentionDays, min: 90, max: 3650),
            BackupRetentionDays = ClampDays(backupRetention, DefaultBackupRetentionDays, min: 7, max: 90),
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

    private static int? ParseInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return int.TryParse(value.Trim(), System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static int ClampDays(int value, int fallback, int min, int max)
    {
        if (value <= 0)
        {
            value = fallback;
        }

        return Math.Clamp(value, min, max);
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

        /// <summary>审计保留天数（SRS <c>audit.keepDays</c>；默认 90）。</summary>
        public int? AuditRetentionDays { get; set; }

        /// <summary>备份 ref 保留天数（SRS <c>backup.retentionDays</c>；默认 30）。</summary>
        public int? BackupRetentionDays { get; set; }
    }

    private sealed class GiteaConfig
    {
        public string? BaseUrl { get; set; }

        public string? Org { get; set; }

        public string? Token { get; set; }

        public bool? AllowInsecureHttp { get; set; }
    }
}
