using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace MbdGitTool.Host.Services;

/// <summary>
/// 审计-lite（M2a G-P0-2 / AC-OP-33）：每次 <c>runRepoAction</c>（成功/失败）追加 1 行 JSONL。
/// <para>落点：<c>%LOCALAPPDATA%\MbdGitTool\audit\operations.jsonl</c>（<see cref="AppConfig.AuditRoot"/> 可注入；
/// 测试注入临时目录）。目录不存在时自动创建。</para>
/// <para><b>字段</b>：<c>ts</c>(UTC ISO-8601) / <c>action</c> / <c>componentId</c> / <c>repoType</c> / <c>state</c> /
/// <c>preHead</c> / <c>postHead</c> / <c>ok</c> / <c>errorCode</c> / <c>durationMs</c>。</para>
/// <para><b>安全</b>：只写上述元数据，绝不写 PAT/token/命令 output/stderr（FR-SAFE-03）；写入失败仅告警，
/// 绝不影响动作本身。并发追加由进程内按文件互斥锁 + <see cref="FileShare.ReadWrite"/> 单次写入保证不交错。</para>
/// </summary>
public sealed class OperationAuditService
{
    /// <summary>审计文件名（固定）。</summary>
    public const string AuditFileName = "operations.jsonl";

    /// <summary>默认审计目录：<c>%LOCALAPPDATA%\MbdGitTool\audit</c>（SRS 12.7 数据落点）。</summary>
    public static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MbdGitTool",
        "audit");

    /// <summary>默认审计文件：<c>%LOCALAPPDATA%\MbdGitTool\audit\operations.jsonl</c>。</summary>
    public static string DefaultFilePath => Path.Combine(DefaultDirectory, AuditFileName);

    private static readonly JsonSerializerOptions JsonOptions = new();

    // 同一进程内不同实例/并发线程写同一文件时串行化（跨进程依赖单次 O_APPEND 写入的原子性）。
    private static readonly ConcurrentDictionary<string, object> FileLocks =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly object _sync;

    /// <summary>按配置创建（<see cref="AppConfig.AuditRoot"/> 为空时用默认目录；测试注入临时目录）。</summary>
    public OperationAuditService(AppConfig? config = null)
        : this(string.IsNullOrWhiteSpace(config?.AuditRoot) ? DefaultDirectory : config!.AuditRoot!.Trim())
    {
    }

    /// <summary>按显式目录创建（测试用；目录不存在时在首次写入前自动创建）。</summary>
    public OperationAuditService(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        DirectoryPath = Path.GetFullPath(directory);
        FilePath = Path.Combine(DirectoryPath, AuditFileName);
        _sync = FileLocks.GetOrAdd(FilePath, _ => new object());
    }

    /// <summary>审计目录（绝对路径）。</summary>
    public string DirectoryPath { get; }

    /// <summary>审计文件（绝对路径）。</summary>
    public string FilePath { get; }

    /// <summary>追加一条执行审计（非抛异常：IO/权限失败仅告警，不影响动作结果）。</summary>
    public void Record(RepoActionExecution execution)
    {
        ArgumentNullException.ThrowIfNull(execution);
        Record(
            execution.Action,
            execution.ComponentId,
            execution.RepoType,
            execution.State,
            execution.PreHead,
            execution.PostHead,
            execution.Ok,
            execution.ErrorCode,
            execution.DurationMs,
            actor: execution.Actor,
            requestId: execution.RequestId);
    }

    /// <summary>追加一条执行审计（字段级重载，便于独立测试/诊断）。</summary>
    public void Record(
        string? action,
        string? componentId,
        string? repoType,
        string? state,
        string? preHead,
        string? postHead,
        bool ok,
        string? errorCode,
        long durationMs,
        string? actor = null,
        string? requestId = null)
    {
        try
        {
            var line = JsonSerializer.Serialize(
                new
                {
                    ts = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                    action = Sanitize(action),
                    componentId = Sanitize(componentId),
                    repoType = Sanitize(repoType),
                    state = Sanitize(state) ?? "UNKNOWN",
                    preHead = Sanitize(preHead),
                    postHead = Sanitize(postHead),
                    ok,
                    errorCode = Sanitize(errorCode),
                    durationMs = Math.Max(0, durationMs),
                    actor = Sanitize(actor),
                    requestId = Sanitize(requestId)
                },
                JsonOptions);

            var bytes = Encoding.UTF8.GetBytes(line + "\n");
            lock (_sync)
            {
                Directory.CreateDirectory(DirectoryPath);

                // Append + FileShare.ReadWrite：单行一次写；进程内已加锁，避免多线程交叉写入。
                using var stream = new FileStream(
                    FilePath,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.ReadWrite,
                    bufferSize: 4096,
                    FileOptions.None);
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush();
            }
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or ArgumentException
            or System.Security.SecurityException)
        {
            // 审计失败不影响动作；不记录字段内容（可能含路径/仓库名）。
            HostLog.Warn($"审计写入失败（{ex.GetType().Name}）：{FilePath}");
        }
    }

    /// <summary>单行字段长度上限（防异常输入膨胀 JSONL；不改变字段存在性）。</summary>
    private static string? Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= 256 ? trimmed : trimmed[..256];
    }
}
