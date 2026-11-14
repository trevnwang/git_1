using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MbdGitTool.Host.Services;

/// <summary>
/// 写前 journal（FR-SAFE-04 / ADR D-026 M2b-2b）：任何 git 写操作在执行前先追加一条
/// <c>status=STARTED</c> 的 JSONL；执行结束后追加 <c>DONE</c>（含 postHead）或 <c>FAILED</c>（含 errorCode）。
/// <para>落点：<c>%LOCALAPPDATA%\MbdGitTool\journal\operations.jsonl</c>
/// （<see cref="AppConfig.JournalRoot"/> 可注入；仅注入 <see cref="AppConfig.AuditRoot"/> 时落在其
/// <c>journal</c> 子目录，保证单测隔离）。</para>
/// <para><b>崩溃语义</b>：STARTED 行先于 git 写落盘；进程被杀时不会补 DONE/FAILED，重启后由
/// <see cref="JournalReconciler"/> 读取当前 HEAD 判定 <c>NOT_APPLIED</c> / <c>UNKNOWN_APPLIED</c>，
/// 绝不猜测成功。reconcile 结果以 <c>status=RECONCILED</c> 追加，避免下一次启动重复 reconcile。</para>
/// <para><b>旁路语义</b>：journal 写入失败只告警，不影响动作；字段只含元数据（动作/组件/仓类型/HEAD/
/// 状态/错误码/job 关联），绝不写 token/output。</para>
/// </summary>
public sealed class OperationJournal
{
    /// <summary>journal 文件名（固定）。</summary>
    public const string JournalFileName = "operations.jsonl";

    /// <summary>默认 journal 子目录名（SRS 12.7 数据落点）。</summary>
    public const string JournalDirectoryName = "journal";

    /// <summary>STARTED：git 写前落盘。</summary>
    public const string StatusStarted = "STARTED";

    /// <summary>DONE：git 写成功结束（含 postHead）。</summary>
    public const string StatusDone = "DONE";

    /// <summary>FAILED：git 写失败/取消/异常结束（含 errorCode）。</summary>
    public const string StatusFailed = "FAILED";

    /// <summary>RECONCILED：启动 reconcile 已判定该 STARTED 条目的结果（不重复判定）。</summary>
    public const string StatusReconciled = "RECONCILED";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // 同一进程内不同实例/并发线程写同一文件时串行化（与 OperationAuditService 同一策略）。
    private static readonly ConcurrentDictionary<string, object> FileLocks =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly object _sync;

    /// <summary>按配置创建（<see cref="AppConfig.JournalRoot"/> 可注入；隔离规则见类注释）。</summary>
    public OperationJournal(AppConfig? config = null)
        : this(ResolveDirectory(config))
    {
    }

    /// <summary>按显式目录创建（测试注入临时目录；目录不存在时首次写入前自动创建）。</summary>
    public OperationJournal(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        DirectoryPath = Path.GetFullPath(directory);
        FilePath = Path.Combine(DirectoryPath, JournalFileName);
        _sync = FileLocks.GetOrAdd(FilePath, _ => new object());
    }

    /// <summary>journal 目录（绝对路径）。</summary>
    public string DirectoryPath { get; }

    /// <summary>journal 文件（绝对路径）。</summary>
    public string FilePath { get; }

    /// <summary>默认 journal 目录：<c>%LOCALAPPDATA%\MbdGitTool\journal</c>。</summary>
    public static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MbdGitTool",
        JournalDirectoryName);

    /// <summary>默认 journal 文件：<c>%LOCALAPPDATA%\MbdGitTool\journal\operations.jsonl</c>。</summary>
    public static string DefaultFilePath => Path.Combine(DefaultDirectory, JournalFileName);

    /// <summary>
    /// 目录解析：显式 <see cref="AppConfig.JournalRoot"/> 优先；仅注入 <see cref="AppConfig.AuditRoot"/> 时
    /// 落在审计目录的 <c>journal</c> 子目录（单测隔离，避免写真实 <c>%LOCALAPPDATA%</c>）；否则默认目录。
    /// </summary>
    public static string ResolveDirectory(AppConfig? config)
    {
        if (!string.IsNullOrWhiteSpace(config?.JournalRoot))
        {
            return config!.JournalRoot!.Trim();
        }

        if (!string.IsNullOrWhiteSpace(config?.AuditRoot))
        {
            return Path.Combine(config!.AuditRoot!.Trim(), JournalDirectoryName);
        }

        return DefaultDirectory;
    }

    /// <summary>
    /// 写前登记一条 STARTED（必须在 git 写之前调用）；写入失败返回 null（动作继续，仅告警）。
    /// <paramref name="localPath"/> 为仓库/目标路径，供崩溃 reconcile 直接读取当前 HEAD。
    /// </summary>
    public JournalEntry? Begin(
        string? action,
        string? componentId,
        string? repoType,
        string? preHead,
        string? localPath = null,
        string? jobId = null,
        string? jobItemId = null)
    {
        var entry = new JournalEntry(
            Id: Guid.NewGuid().ToString("N"),
            Ts: DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            Action: string.IsNullOrWhiteSpace(action) ? "UNKNOWN" : action.Trim().ToUpperInvariant(),
            ComponentId: Sanitize(componentId),
            RepoType: Sanitize(repoType),
            PreHead: Sanitize(preHead),
            Status: StatusStarted,
            LocalPath: SanitizePath(localPath),
            JobId: Sanitize(jobId),
            JobItemId: Sanitize(jobItemId));

        return Append(entry) ? entry : null;
    }

    /// <summary>git 写成功收口：追加 1 行 DONE（含 postHead）。失败仅告警。</summary>
    public void Complete(JournalEntry? entry, string? postHead)
    {
        if (entry is null)
        {
            return;
        }

        Append(entry with { Status = StatusDone, PostHead = Sanitize(postHead) });
    }

    /// <summary>git 写失败/取消/异常收口：追加 1 行 FAILED（含 errorCode）。失败仅告警。</summary>
    public void Fail(JournalEntry? entry, string? errorCode)
    {
        if (entry is null)
        {
            return;
        }

        Append(entry with { Status = StatusFailed, ErrorCode = Sanitize(errorCode) });
    }

    /// <summary>启动 reconcile 收口：追加 1 行 RECONCILED（含判定结果与当前 HEAD）。失败仅告警。</summary>
    public void Reconcile(JournalEntry entry, string reconcileStatus, string? currentHead, string? errorCode = null)
    {
        ArgumentNullException.ThrowIfNull(entry);
        Append(entry with
        {
            Status = StatusReconciled,
            ReconcileStatus = Sanitize(reconcileStatus),
            CurrentHead = Sanitize(currentHead),
            ErrorCode = Sanitize(errorCode)
        });
    }

    /// <summary>
    /// 扫描 journal，返回“STARTED 但尚未收口（无 DONE/FAILED/RECONCILED）”的条目（按首次出现顺序）。
    /// 读取/解析失败的行忽略（不猜测）。
    /// </summary>
    public IReadOnlyList<JournalEntry> ReadUnresolved()
    {
        var entries = new List<JournalEntry>();
        var index = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var line in ReadAllLines())
        {
            var parsed = Parse(line);
            if (parsed is null)
            {
                continue;
            }

            if (parsed.Status == StatusStarted)
            {
                if (!index.ContainsKey(parsed.Id))
                {
                    index[parsed.Id] = entries.Count;
                    entries.Add(parsed);
                }

                continue;
            }

            if (parsed.Status is StatusDone or StatusFailed or StatusReconciled
                && index.Remove(parsed.Id, out var position))
            {
                entries.RemoveAt(position);
                // 重新编号后续索引（journal 很小，线性修复成本可忽略）。
                for (var cursor = position; cursor < entries.Count; cursor++)
                {
                    index[entries[cursor].Id] = cursor;
                }
            }
        }

        return entries;
    }

    /// <summary>未收口的 STARTED 条目数（诊断/测试用）。</summary>
    public int CountUnresolved() => ReadUnresolved().Count;

    /// <summary>journal 总行数（诊断/测试用；文件缺失返回 0）。</summary>
    public int CountLines() => ReadAllLines().Count;

    /// <summary>读取全部可解析的 journal 条目（测试/诊断用；按文件顺序）。</summary>
    public IReadOnlyList<JournalEntry> ReadAll()
    {
        var entries = new List<JournalEntry>();
        foreach (var line in ReadAllLines())
        {
            var parsed = Parse(line);
            if (parsed is not null)
            {
                entries.Add(parsed);
            }
        }

        return entries;
    }

    // ------------------------------------------------------------------ JSONL helpers

    private bool Append(JournalEntry entry)
    {
        string line;
        try
        {
            line = JsonSerializer.Serialize(ToLine(entry), JsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            HostLog.Warn($"写前 journal 序列化失败（{ex.GetType().Name}）：{FilePath}");
            return false;
        }

        try
        {
            lock (_sync)
            {
                Directory.CreateDirectory(DirectoryPath);
                using var stream = new FileStream(
                    FilePath,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.ReadWrite,
                    bufferSize: 4096,
                    FileOptions.None);
                var bytes = Encoding.UTF8.GetBytes(line + "\n");
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush();
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or ArgumentException
            or System.Security.SecurityException)
        {
            HostLog.Warn($"写前 journal 写入失败（{ex.GetType().Name}）：{FilePath}");
            return false;
        }
    }

    private IReadOnlyList<string> ReadAllLines()
    {
        try
        {
            lock (_sync)
            {
                if (!File.Exists(FilePath))
                {
                    return Array.Empty<string>();
                }

                return File.ReadAllLines(FilePath);
            }
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or ArgumentException)
        {
            HostLog.Warn($"写前 journal 读取失败（{ex.GetType().Name}）：{FilePath}");
            return Array.Empty<string>();
        }
    }

    private static JournalLine ToLine(JournalEntry entry) => new()
    {
        Id = entry.Id,
        Ts = entry.Ts,
        Action = entry.Action,
        ComponentId = entry.ComponentId,
        RepoType = entry.RepoType,
        PreHead = entry.PreHead,
        Status = entry.Status,
        LocalPath = entry.LocalPath,
        JobId = entry.JobId,
        JobItemId = entry.JobItemId,
        PostHead = entry.PostHead,
        ErrorCode = entry.ErrorCode,
        ReconcileStatus = entry.ReconcileStatus,
        CurrentHead = entry.CurrentHead
    };

    private static JournalEntry? Parse(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var id = GetString(root, "id");
            var status = GetString(root, "status");
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(status))
            {
                return null;
            }

            return new JournalEntry(
                Id: id!,
                Ts: GetString(root, "ts") ?? string.Empty,
                Action: GetString(root, "action") ?? "UNKNOWN",
                ComponentId: GetString(root, "componentId"),
                RepoType: GetString(root, "repoType"),
                PreHead: GetString(root, "preHead"),
                Status: status!,
                LocalPath: GetString(root, "localPath"),
                JobId: GetString(root, "jobId"),
                JobItemId: GetString(root, "jobItemId"),
                PostHead: GetString(root, "postHead"),
                ErrorCode: GetString(root, "errorCode"),
                ReconcileStatus: GetString(root, "reconcileStatus"),
                CurrentHead: GetString(root, "currentHead"));
        }
        catch (JsonException)
        {
            // 损坏行忽略（不猜测）。
            return null;
        }
    }

    private static string? GetString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>单行字段长度上限（防异常输入膨胀 journal；不改变字段存在性）。</summary>
    private static string? Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= 512 ? trimmed : trimmed[..512];
    }

    /// <summary>路径字段上限更宽（Windows 长路径）；只保留绝对路径字符，避免写入任意文本。</summary>
    private static string? SanitizePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= 1024 ? trimmed : trimmed[..1024];
    }

    /// <summary>
    /// journal 行序列化形状：<c>{id,ts,action,componentId,repoType,preHead,status[,localPath,jobId,jobItemId,
    /// postHead,errorCode,reconcileStatus,currentHead]}</c>；可选键仅在非 null 时输出。
    /// </summary>
    private sealed class JournalLine
    {
        [JsonPropertyName("id")]
        public required string Id { get; init; }

        [JsonPropertyName("ts")]
        public required string Ts { get; init; }

        [JsonPropertyName("action")]
        public required string Action { get; init; }

        [JsonPropertyName("componentId")]
        public string? ComponentId { get; init; }

        [JsonPropertyName("repoType")]
        public string? RepoType { get; init; }

        [JsonPropertyName("preHead")]
        public string? PreHead { get; init; }

        [JsonPropertyName("status")]
        public required string Status { get; init; }

        [JsonPropertyName("localPath")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? LocalPath { get; init; }

        [JsonPropertyName("jobId")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? JobId { get; init; }

        [JsonPropertyName("jobItemId")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? JobItemId { get; init; }

        [JsonPropertyName("postHead")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? PostHead { get; init; }

        [JsonPropertyName("errorCode")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ErrorCode { get; init; }

        [JsonPropertyName("reconcileStatus")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ReconcileStatus { get; init; }

        [JsonPropertyName("currentHead")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? CurrentHead { get; init; }
    }
}

/// <summary>
/// journal 条目（不可变）：STARTED 时 <see cref="PostHead"/>/<see cref="ErrorCode"/> 为 null；
/// DONE 带 <see cref="PostHead"/>；FAILED 带 <see cref="ErrorCode"/>；RECONCILED 带
/// <see cref="ReconcileStatus"/>/<see cref="CurrentHead"/>。
/// </summary>
public sealed record JournalEntry(
    string Id,
    string Ts,
    string Action,
    string? ComponentId,
    string? RepoType,
    string? PreHead,
    string Status,
    string? LocalPath = null,
    string? JobId = null,
    string? JobItemId = null,
    string? PostHead = null,
    string? ErrorCode = null,
    string? ReconcileStatus = null,
    string? CurrentHead = null);
