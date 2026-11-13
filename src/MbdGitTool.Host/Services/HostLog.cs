using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace MbdGitTool.Host.Services;

/// <summary>
/// 极简宿主日志（M1）：写 <c>%LOCALAPPDATA%\MbdGitTool\logs\host.log</c>，单文件 10MB 滚动由 M3 完善。
/// <para>兼容旧日志：若 <c>host-m0.log</c> 已存在且新日志尚不存在，首次写入前把旧日志复制为 host.log 的基线。</para>
/// <para><b>安全</b>：所有消息先做凭据脱敏（token/Authorization/access_token/URL userinfo），禁止明文 token 落盘。</para>
/// </summary>
public static partial class HostLog
{
    private const string LegacyLogFileName = "host-m0.log";
    private const string LogFileName = "host.log";
    private const long MaxLogBytes = 10L * 1024 * 1024;

    private static readonly object Sync = new();

    /// <summary>日志目录（12.7 数据落点）。</summary>
    public static string LogDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MbdGitTool",
        "logs");

    /// <summary>当前日志文件路径。</summary>
    public static string LogFilePath => Path.Combine(LogDirectory, LogFileName);

    public static void Info(string message) => Write("INFO", message);

    public static void Warn(string message) => Write("WARN", message);

    public static void Error(string message) => Write("ERROR", message);

    /// <summary>写入一行日志；失败不阻断启动（日志自身错误静默）。</summary>
    public static void Write(string level, string message)
    {
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(LogDirectory);
                MigrateLegacyLogIfNeeded();
                RollIfTooLarge();

                var line = string.Create(
                    CultureInfo.InvariantCulture,
                    $"{DateTimeOffset.UtcNow:O} [{level}] {Redact(message)}{Environment.NewLine}");
                File.AppendAllText(LogFilePath, line);
            }
        }
        catch
        {
            // 日志失败不阻断业务（M1）。
        }
    }

    /// <summary>凭据脱敏（FR-SAFE-03）：Authorization 头、token=/access_token=、URL userinfo。</summary>
    public static string Redact(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return string.Empty;
        }

        var redacted = AuthorizationPattern().Replace(message, "$1***");
        return UrlUserInfoPattern().Replace(redacted, "$1***:***@");
    }

    private static void MigrateLegacyLogIfNeeded()
    {
        var legacy = Path.Combine(LogDirectory, LegacyLogFileName);
        if (File.Exists(LogFilePath) || !File.Exists(legacy))
        {
            return;
        }

        try
        {
            File.Copy(legacy, LogFilePath, overwrite: false);
            File.AppendAllText(
                LogFilePath,
                $"{DateTimeOffset.UtcNow:O} [INFO] （已兼容合并 M0 日志 {LegacyLogFileName}）{Environment.NewLine}");
        }
        catch
        {
            // 兼容迁移失败可忽略，继续使用新日志文件。
        }
    }

    private static void RollIfTooLarge()
    {
        var info = new FileInfo(LogFilePath);
        if (!info.Exists || info.Length < MaxLogBytes)
        {
            return;
        }

        var archived = Path.Combine(LogDirectory, $"host.{DateTime.UtcNow:yyyyMMddHHmmss}.log");
        File.Move(LogFilePath, archived, overwrite: true);
    }

    [GeneratedRegex(
        @"(?i)(authorization\s*[:=]\s*token\s+|authorization\s*[:=]\s*bearer\s+|(?<![a-z0-9_])(access_)?token\s*[=:]\s*)\S+",
        RegexOptions.CultureInvariant)]
    private static partial Regex AuthorizationPattern();

    [GeneratedRegex(@"(?i)(https?://)[^/\s:@]+:[^/\s@]+@", RegexOptions.CultureInvariant)]
    private static partial Regex UrlUserInfoPattern();
}
