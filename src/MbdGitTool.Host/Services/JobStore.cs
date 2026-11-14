using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace MbdGitTool.Host.Services;

/// <summary>
/// M2b 作业持久化（SRS §9.7 FR-OP-08 “任务持久化，重启后不丢”；§8.1 <c>job</c>/<c>job_item</c>）。
/// <para>落点：<c>%LOCALAPPDATA%\MbdGitTool\jobs\jobs.json</c>（<see cref="AppConfig.JobsRoot"/> 可注入）。</para>
/// <para>形状：<c>{ version:1, jobs:[StoredJob…] }</c>；内存顺序 = 旧→新，落盘保持同序，最多保留最近
/// <see cref="MaxJobs"/> 条（超出淘汰最旧）。写入失败只告警，不影响作业执行（与审计同一策略）。</para>
/// <para>“重启不重复执行”由 <see cref="JobEngine"/> 在加载时把 PENDING/RUNNING 标 <c>INTERRUPTED</c>，不在本类做语义处理。</para>
/// </summary>
public sealed class JobStore
{
    /// <summary>作业文件名（固定）。</summary>
    public const string FileName = "jobs.json";

    /// <summary>最多保留的 job 数（SRS FR-OP-08 任务历史；超出淘汰最旧）。</summary>
    public const int MaxJobs = 200;

    /// <summary>默认作业目录：<c>%LOCALAPPDATA%\MbdGitTool\jobs</c>（SRS 12.7 数据落点）。</summary>
    public static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MbdGitTool",
        "jobs");

    private const int FormatVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    // 同一进程内多实例写同一文件时串行化（与 OperationAuditService 同一策略）。
    private static readonly ConcurrentDictionary<string, object> FileLocks =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly object _sync;

    /// <summary>按配置创建（<see cref="AppConfig.JobsRoot"/> 为空时用默认目录）。</summary>
    public JobStore(AppConfig? config = null)
        : this(string.IsNullOrWhiteSpace(config?.JobsRoot) ? DefaultDirectory : config!.JobsRoot!.Trim())
    {
    }

    /// <summary>按显式目录创建（测试用；目录不存在时在首次写入前自动创建）。</summary>
    public JobStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        DirectoryPath = Path.GetFullPath(directory);
        FilePath = Path.Combine(DirectoryPath, FileName);
        _sync = FileLocks.GetOrAdd(FilePath, _ => new object());
    }

    /// <summary>作业目录（绝对路径）。</summary>
    public string DirectoryPath { get; }

    /// <summary>作业文件（绝对路径）。</summary>
    public string FilePath { get; }

    /// <summary>读取历史作业（旧→新，最多 <see cref="MaxJobs"/> 条）；文件缺失/损坏时返回空表并告警。</summary>
    public IReadOnlyList<StoredJob> Load()
    {
        lock (_sync)
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    return Array.Empty<StoredJob>();
                }

                var json = File.ReadAllText(FilePath, Encoding.UTF8);
                var file = JsonSerializer.Deserialize<StoreFile>(json, JsonOptions);
                if (file?.Jobs is null)
                {
                    return Array.Empty<StoredJob>();
                }

                return Trim(file.Jobs);
            }
            catch (Exception ex) when (ex is IOException
                or UnauthorizedAccessException
                or JsonException
                or NotSupportedException
                or ArgumentException)
            {
                HostLog.Warn($"作业历史读取失败（{ex.GetType().Name}）：{FilePath}");
                return Array.Empty<StoredJob>();
            }
        }
    }

    /// <summary>落盘作业表（旧→新；自动截断到最近 <see cref="MaxJobs"/> 条）。失败仅告警。</summary>
    public void Save(IReadOnlyList<StoredJob> jobs)
    {
        ArgumentNullException.ThrowIfNull(jobs);

        var trimmed = Trim(jobs);
        string json;
        try
        {
            json = JsonSerializer.Serialize(new StoreFile(FormatVersion, trimmed), JsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            HostLog.Warn($"作业历史序列化失败（{ex.GetType().Name}）");
            return;
        }

        lock (_sync)
        {
            string? tempPath = null;
            try
            {
                Directory.CreateDirectory(DirectoryPath);
                tempPath = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                File.WriteAllText(tempPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                File.Move(tempPath, FilePath, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException
                or UnauthorizedAccessException
                or NotSupportedException
                or ArgumentException)
            {
                // 持久化失败不阻断作业；下次状态变化会重试写入。
                HostLog.Warn($"作业历史写入失败（{ex.GetType().Name}）：{FilePath}");
                TryDelete(tempPath);
            }
        }
    }

    private static IReadOnlyList<StoredJob> Trim(IReadOnlyList<StoredJob> jobs)
    {
        if (jobs.Count <= MaxJobs)
        {
            return jobs;
        }

        return jobs.Skip(jobs.Count - MaxJobs).ToList();
    }

    private static void TryDelete(string? path)
    {
        if (path is null)
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 临时文件清理失败可忽略。
        }
    }

    private sealed record StoreFile(int Version, IReadOnlyList<StoredJob> Jobs);
}

/// <summary>
/// 持久化的 job 行（jobs.json）：JobDto 全字段 + 重试所需的 kind/args 元数据。
/// 可空时间/错误码/HEAD 字段保持可空，JSON 中允许 null。
/// <para><c>DryRun/CancelRequested</c> 为可选参数（旧 jobs.json 反序列化兼容；缺省 false）。</para>
/// </summary>
public sealed record StoredJob(
    string Id,
    string Kind,
    string State,
    string CreatedAt,
    string? StartedAt,
    string? FinishedAt,
    IReadOnlyList<StoredJobItem> Items,
    StoredJobArguments? Arguments,
    string? Actor,
    string? RequestId,
    bool DryRun = false,
    bool CancelRequested = false);

/// <summary>持久化的 job_item 行（逐仓结果）；<c>Preview</c> 仅 dryRun 作业为 true。</summary>
public sealed record StoredJobItem(
    string ComponentId,
    string RepoType,
    string? RepoName,
    string State,
    string? StartedAt,
    string? FinishedAt,
    string? ErrorCode,
    string? Message,
    string? PreHead,
    string? PostHead,
    string? BackupRef,
    bool Preview = false,
    string? ExpectHead = null,
    string? BaselineRef = null);

/// <summary>批量作业参数（runBatch.args 的解析结果；持久化以便重启后“仅重试失败项”仍可执行）。</summary>
public sealed record StoredJobArguments(
    string? Branch,
    string? Tag,
    string? Message,
    bool Confirm,
    bool DryRun,
    bool Backup,
    bool SetUpstream,
    bool Stash,
    bool StageAll,
    string? TrashStamp,
    string? ConfirmText = null,
    bool CleanUntracked = false,
    string? Mode = null);
