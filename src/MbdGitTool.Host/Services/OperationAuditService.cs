using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MbdGitTool.Core.Contracts;

namespace MbdGitTool.Host.Services;

/// <summary>
/// 审计（M2a G-P0-2 / AC-OP-33；M2b-1 job 关联；M2b-2 完整表）：
/// 每次 <c>runRepoAction</c> 或作业 item（成功/失败/跳过/取消/中断/预览）落 <b>1 行 JSONL + 1 行 SQLite</b>。
/// <para>JSONL 落点：<c>%LOCALAPPDATA%\MbdGitTool\audit\operations.jsonl</c>
/// （<see cref="AppConfig.AuditRoot"/> 可注入；测试注入临时目录），M2a 行形状保持兼容。</para>
/// <para>SQLite 落点：<c>%LOCALAPPDATA%\MbdGitTool\db\mbd.db</c> 的 <c>operation_log</c> 表
/// （<see cref="AppConfig.DatabasePath"/> 可注入；§8.1 冻结列；WAL/busy_timeout/foreign_keys/user_version 迁移）。</para>
/// <para><b>双写语义</b>：SQLite 是旁路增强——初始化/写入失败只告警（JSONL 仍写），绝不影响动作本身；
/// 查询/导出只读 SQLite（<c>operation_log</c>），导出落 <c>%LOCALAPPDATA%\MbdGitTool\exports\</c>。</para>
/// <para><b>安全</b>：只写元数据（组件/仓类型/HEAD/结果/错误码/requestId/job 关联），绝不写 PAT/token/
/// 命令 output/stderr（FR-SAFE-03）；查询/导出不含 token。</para>
/// </summary>
public sealed class OperationAuditService
{
    /// <summary>审计文件名（固定）。</summary>
    public const string AuditFileName = "operations.jsonl";

    /// <summary>导出文件名前缀（固定，落 <see cref="AppConfig.ExportsRoot"/>）。</summary>
    public const string ExportFilePrefix = "operation-log-";

    /// <summary>导出单次最大行数（防止一次导出耗尽内存；超限返回 E_TOO_MANY_ENTRIES）。</summary>
    public const int MaxExportRows = 100_000;

    /// <summary>默认审计目录：<c>%LOCALAPPDATA%\MbdGitTool\audit</c>（SRS 12.7 数据落点）。</summary>
    public static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MbdGitTool",
        "audit");

    /// <summary>默认审计文件：<c>%LOCALAPPDATA%\MbdGitTool\audit\operations.jsonl</c>。</summary>
    public static string DefaultFilePath => Path.Combine(DefaultDirectory, AuditFileName);

    /// <summary>默认导出目录：<c>%LOCALAPPDATA%\MbdGitTool\exports</c>。</summary>
    public static string DefaultExportsRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MbdGitTool",
        "exports");

    private static readonly JsonSerializerOptions JsonOptions = new();

    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    // 同一进程内不同实例/并发线程写同一文件时串行化（跨进程依赖单次 O_APPEND 写入的原子性）。
    private static readonly ConcurrentDictionary<string, object> FileLocks =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly object _sync;
    private readonly int _auditRetentionDays;

    /// <summary>按配置创建（<see cref="AppConfig.AuditRoot"/>/<see cref="AppConfig.DatabasePath"/> 可注入）。</summary>
    public OperationAuditService(AppConfig? config = null)
        : this(
            string.IsNullOrWhiteSpace(config?.AuditRoot) ? DefaultDirectory : config!.AuditRoot!.Trim(),
            new AuditDatabase(ResolveDatabasePath(config)),
            ResolveExportsRoot(config),
            config?.EffectiveAuditRetentionDays ?? AppConfig.DefaultAuditRetentionDays)
    {
    }

    /// <summary>
    /// 审计库路径解析：显式 <see cref="AppConfig.DatabasePath"/> 优先；仅注入 <see cref="AppConfig.AuditRoot"/> 时
    /// 把库放到审计目录旁（单测隔离，避免写真实 <c>%LOCALAPPDATA%</c>）；两者都空 = 默认路径。
    /// </summary>
    private static string? ResolveDatabasePath(AppConfig? config)
    {
        if (!string.IsNullOrWhiteSpace(config?.DatabasePath))
        {
            return config!.DatabasePath;
        }

        return string.IsNullOrWhiteSpace(config?.AuditRoot)
            ? null
            : Path.Combine(config!.AuditRoot!.Trim(), AuditDatabase.DatabaseFileName);
    }

    /// <summary>导出目录解析：显式 <see cref="AppConfig.ExportsRoot"/> 优先；仅注入审计目录时跟随审计目录。</summary>
    private static string? ResolveExportsRoot(AppConfig? config)
    {
        if (!string.IsNullOrWhiteSpace(config?.ExportsRoot))
        {
            return config!.ExportsRoot;
        }

        return string.IsNullOrWhiteSpace(config?.AuditRoot)
            ? null
            : Path.Combine(config!.AuditRoot!.Trim(), "exports");
    }

    /// <summary>按显式目录创建（JSONL-only；M2a 兼容/单测用）。</summary>
    public OperationAuditService(string directory)
        : this(directory, database: null, exportsRoot: null, auditRetentionDays: AppConfig.DefaultAuditRetentionDays)
    {
    }

    /// <summary>
    /// 完整构造：<paramref name="database"/> 非 null 时初始化并启用 SQLite 双写（初始化失败自动降级 JSONL-only）；
    /// 测试注入临时 JSONL 目录/DB 路径/导出目录。
    /// </summary>
    public OperationAuditService(
        string directory,
        AuditDatabase? database,
        string? exportsRoot = null,
        int auditRetentionDays = AppConfig.DefaultAuditRetentionDays)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        DirectoryPath = Path.GetFullPath(directory);
        FilePath = Path.Combine(DirectoryPath, AuditFileName);
        _sync = FileLocks.GetOrAdd(FilePath, _ => new object());
        ExportsRoot = Path.GetFullPath(
            string.IsNullOrWhiteSpace(exportsRoot) ? DefaultExportsRoot : exportsRoot!.Trim());
        _auditRetentionDays = Math.Clamp(auditRetentionDays, 1, 3650);
        Database = database;
        Database?.Initialize();
    }

    /// <summary>审计目录（绝对路径）。</summary>
    public string DirectoryPath { get; }

    /// <summary>审计文件（绝对路径）。</summary>
    public string FilePath { get; }

    /// <summary>导出目录（绝对路径；不存在时在导出时创建）。</summary>
    public string ExportsRoot { get; }

    /// <summary>SQLite 审计库（可为 null = JSONL-only；<see cref="AuditDatabase.Available"/>=false 时同样降级）。</summary>
    public AuditDatabase? Database { get; }

    /// <summary>SQLite 审计查询/导出是否可用。</summary>
    public bool DatabaseAvailable => Database?.Available == true;

    /// <summary>追加一条执行审计（JSONL + SQLite；任一失败仅告警，不影响动作结果）。</summary>
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
            requestId: execution.RequestId,
            jobId: execution.JobId,
            jobItemId: execution.JobItemId,
            result: execution.Result,
            backupRef: execution.BackupRef);
    }

    /// <summary>追加一条执行审计（字段级重载，便于独立测试/诊断；M2b 追加 job 关联可选字段）。</summary>
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
        string? requestId = null,
        string? jobId = null,
        string? jobItemId = null,
        string? result = null,
        string? backupRef = null)
    {
        var ts = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        var actionText = Sanitize(action) ?? "UNKNOWN";
        var component = Sanitize(componentId);
        var type = Sanitize(repoType);
        var stateText = Sanitize(state) ?? "UNKNOWN";
        var pre = Sanitize(preHead);
        var post = Sanitize(postHead);
        var error = Sanitize(errorCode);
        var actorText = Sanitize(actor);
        var request = Sanitize(requestId);
        var job = Sanitize(jobId);
        var jobItem = Sanitize(jobItemId);
        var backup = Sanitize(backupRef);
        var resultText = Sanitize(result) ?? (ok ? "OK" : "FAILED");

        WriteJsonLine(
            actionText,
            component,
            type,
            stateText,
            pre,
            post,
            ok,
            error,
            Math.Max(0, durationMs),
            actorText,
            request,
            resultText,
            job,
            jobItem,
            backup,
            ts);

        // SQLite 双写（旁路）：失败只告警；列映射见 §8.1（target/before/after JSON 投影）。
        try
        {
            Database?.Insert(new OperationLogEntry(
                Ts: ts,
                Actor: actorText,
                Action: actionText,
                ComponentId: component,
                RepoType: type,
                TargetJson: BuildTargetJson(component, type),
                BeforeJson: BuildHeadJson(pre, stateText),
                AfterJson: BuildHeadJson(post, null),
                Result: resultText,
                ErrorCode: error,
                RequestId: request,
                JobId: job,
                JobItemId: jobItem,
                BackupRef: backup));
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or ArgumentException
            or System.Security.SecurityException
            or InvalidOperationException)
        {
            HostLog.Warn($"审计 SQLite 双写失败（{ex.GetType().Name}）：{Database?.FilePath ?? "<none>"}");
        }
    }

    /// <summary>
    /// <c>getOperationLog</c>（FR-OP-09）：按 <c>from/to/action/componentId/result</c> 过滤 + 分页，返回
    /// <c>{ items, total }</c>（<c>total</c> 为过滤条件下总行数）。参数非法返回 <c>E_INVALID_ARGUMENT</c>。
    /// </summary>
    public CommandResult GetOperationLog(GetOperationLogParams? parameters)
    {
        var filter = BuildFilter(
            parameters?.From,
            parameters?.To,
            parameters?.Action,
            parameters?.ComponentId,
            parameters?.Result,
            parameters?.Limit,
            parameters?.Offset,
            out var error);
        if (error is not null)
        {
            return CommandResult.Failure(error);
        }

        if (!DatabaseAvailable)
        {
            return DatabaseUnavailable();
        }

        var page = Database!.Query(filter!);
        if (page is null)
        {
            return DatabaseUnavailable();
        }

        var items = page.Rows.Select(ToDto).ToList();
        return CommandResult.Success(new OperationLogPageDto(items, page.Total));
    }

    /// <summary>
    /// <c>exportOperationLog</c>（FR-OP-09）：把过滤范围内的审计行导出为 CSV/JSON 文件，
    /// 落 <see cref="ExportsRoot"/> 并返回 <c>{ path, count }</c>；内容只含审计元数据（无 token）。
    /// </summary>
    public CommandResult ExportOperationLog(ExportOperationLogParams? parameters)
    {
        var format = parameters?.Format?.Trim().ToLowerInvariant();
        if (format is not ("csv" or "json"))
        {
            return CommandResult.Failure(
                BridgeErrorCodes.InvalidArgument,
                "参数 format 必须是 'csv' 或 'json'",
                new Dictionary<string, object?> { ["field"] = "format", ["format"] = parameters?.Format });
        }

        var filter = BuildFilter(
            parameters?.From,
            parameters?.To,
            action: null,
            componentId: null,
            result: null,
            limit: 1,
            offset: 0,
            out var error);
        if (error is not null)
        {
            return CommandResult.Failure(error);
        }

        if (!DatabaseAvailable)
        {
            return DatabaseUnavailable();
        }

        var totalPage = Database!.Query(filter!);
        if (totalPage is null)
        {
            return DatabaseUnavailable();
        }

        if (totalPage.Total > MaxExportRows)
        {
            return CommandResult.Failure(
                BridgeErrorCodes.TooManyEntries,
                $"导出行数超过上限（{MaxExportRows}）：请缩小 from/to 范围",
                new Dictionary<string, object?> { ["field"] = "range", ["total"] = totalPage.Total });
        }

        var sourceFilter = filter!;
        var exportFilter = sourceFilter with { Limit = MaxExportRows, Offset = 0 };
        var rows = Database.QueryAll(exportFilter, MaxExportRows);
        if (rows is null)
        {
            return DatabaseUnavailable();
        }

        string path;
        try
        {
            Directory.CreateDirectory(ExportsRoot);
            var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
            var fileName = ExportFilePrefix + stamp + "-"
                + Guid.NewGuid().ToString("N")[..8] + "." + format;
            path = Path.Combine(ExportsRoot, fileName);

            if (format == "csv")
            {
                WriteCsv(path, rows);
            }
            else
            {
                WriteJson(path, rows, parameters?.From, parameters?.To);
            }
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or ArgumentException
            or System.Security.SecurityException)
        {
            HostLog.Warn($"审计导出失败（{ex.GetType().Name}）");
            return CommandResult.Failure(BridgeErrorCodes.IoError, "审计导出失败（IO/权限）；请检查数据目录可写");
        }

        HostLog.Info($"审计导出完成：{path}（{rows.Count} 行，format={format}）");
        return CommandResult.Success(new OperationLogExportDto(path, rows.Count));
    }

    /// <summary>
    /// 90 天保留清理（FR-OP-09 / FR-OPS-02）：删除超过 <see cref="AppConfig.EffectiveAuditRetentionDays"/> 的
    /// SQLite 行与 JSONL 行。返回 (SQLite 删除行数, JSONL 删除行数)；失败仅告警。
    /// </summary>
    public (int DatabaseRows, int JsonlRows) CleanupExpired()
    {
        var cutoff = DateTime.UtcNow
            .AddDays(-_auditRetentionDays)
            .ToString("O", CultureInfo.InvariantCulture);
        var dbRemoved = Database?.DeleteOlderThan(cutoff) ?? 0;
        var jsonlRemoved = PruneJsonLines(cutoff);
        if (dbRemoved > 0 || jsonlRemoved > 0)
        {
            HostLog.Info(
                $"审计保留清理（{_auditRetentionDays} 天）：SQLite 删除 {dbRemoved} 行、JSONL 删除 {jsonlRemoved} 行");
        }

        return (dbRemoved, jsonlRemoved);
    }

    // ------------------------------------------------------------------ JSONL

    private void WriteJsonLine(
        string action,
        string? componentId,
        string? repoType,
        string state,
        string? preHead,
        string? postHead,
        bool ok,
        string? errorCode,
        long durationMs,
        string? actor,
        string? requestId,
        string result,
        string? jobId,
        string? jobItemId,
        string? backupRef,
        string ts)
    {
        try
        {
            var line = JsonSerializer.Serialize(
                new AuditLine
                {
                    Ts = ts,
                    Action = action,
                    ComponentId = componentId,
                    RepoType = repoType,
                    State = state,
                    PreHead = preHead,
                    PostHead = postHead,
                    Ok = ok,
                    ErrorCode = errorCode,
                    DurationMs = durationMs,
                    Actor = actor,
                    RequestId = requestId,
                    Result = result,
                    JobId = jobId,
                    JobItemId = jobItemId,
                    BackupRef = backupRef
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

    /// <summary>JSONL 行修剪（保留 ts &gt;= cutoff 与无法解析的行；临时文件 + 原子替换）。</summary>
    private int PruneJsonLines(string cutoffUtc)
    {
        try
        {
            lock (_sync)
            {
                if (!File.Exists(FilePath))
                {
                    return 0;
                }

                var kept = new List<string>();
                var removed = 0;
                foreach (var line in File.ReadLines(FilePath))
                {
                    if (IsExpiredJsonLine(line, cutoffUtc))
                    {
                        removed++;
                        continue;
                    }

                    kept.Add(line);
                }

                if (removed == 0)
                {
                    return 0;
                }

                var temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    File.WriteAllLines(temporary, kept, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                    File.Move(temporary, FilePath, overwrite: true);
                }
                finally
                {
                    if (File.Exists(temporary))
                    {
                        try
                        {
                            File.Delete(temporary);
                        }
                        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                        {
                            // 清理临时文件失败不影响结果。
                        }
                    }
                }

                return removed;
            }
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or ArgumentException)
        {
            HostLog.Warn($"审计 JSONL 保留清理失败（{ex.GetType().Name}）");
            return 0;
        }
    }

    private static bool IsExpiredJsonLine(string line, string cutoffUtc)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(line);
            if (!document.RootElement.TryGetProperty("ts", out var ts)
                || ts.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            var value = ts.GetString();
            return !string.IsNullOrWhiteSpace(value)
                && string.CompareOrdinal(value, cutoffUtc) < 0;
        }
        catch (JsonException)
        {
            // 无法解析的历史行保留（不猜测删除）。
            return false;
        }
    }

    // ------------------------------------------------------------------ 查询/导出 helpers

    private static OperationLogFilter? BuildFilter(
        string? from,
        string? to,
        string? action,
        string? componentId,
        string? result,
        int? limit,
        int? offset,
        out BridgeError? error)
    {
        error = null;

        var normalizedLimit = limit ?? 100;
        if (normalizedLimit < 1 || normalizedLimit > 1000)
        {
            error = new BridgeError(
                BridgeErrorCodes.InvalidArgument,
                "参数 limit 必须在 1–1000 之间",
                new Dictionary<string, object?> { ["field"] = "limit", ["limit"] = limit });
            return null;
        }

        var normalizedOffset = offset ?? 0;
        if (normalizedOffset < 0 || normalizedOffset > 1_000_000)
        {
            error = new BridgeError(
                BridgeErrorCodes.InvalidArgument,
                "参数 offset 必须在 0–1000000 之间",
                new Dictionary<string, object?> { ["field"] = "offset", ["offset"] = offset });
            return null;
        }

        var normalizedAction = NormalizeEnumFilter(action, "action", out var actionError);
        if (actionError is not null)
        {
            error = actionError;
            return null;
        }

        var normalizedResult = NormalizeEnumFilter(result, "result", out var resultError);
        if (resultError is not null)
        {
            error = resultError;
            return null;
        }

        var normalizedComponent = NormalizeComponentFilter(componentId, out var componentError);
        if (componentError is not null)
        {
            error = componentError;
            return null;
        }

        var fromUtc = ParseBoundary(from, endOfDay: false, out var fromError);
        if (fromError is not null)
        {
            error = fromError;
            return null;
        }

        var toUtc = ParseBoundary(to, endOfDay: true, out var toError);
        if (toError is not null)
        {
            error = toError;
            return null;
        }

        if (fromUtc is not null && toUtc is not null
            && string.CompareOrdinal(fromUtc, toUtc) > 0)
        {
            error = new BridgeError(
                BridgeErrorCodes.InvalidArgument,
                "参数 from 不能晚于 to",
                new Dictionary<string, object?> { ["field"] = "from" });
            return null;
        }

        return new OperationLogFilter(
            FromUtc: fromUtc,
            ToUtc: toUtc,
            Action: normalizedAction,
            ComponentId: normalizedComponent,
            Result: normalizedResult,
            Limit: normalizedLimit,
            Offset: normalizedOffset);
    }

    private static string? NormalizeEnumFilter(string? value, string field, out BridgeError? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim().ToUpperInvariant();
        if (trimmed.Length == 0 || trimmed.Length > 64
            || trimmed.Any(ch => !(char.IsAsciiLetterOrDigit(ch) || ch is '_' or '-')))
        {
            error = new BridgeError(
                BridgeErrorCodes.InvalidArgument,
                $"参数 {field} 只允许字母/数字/下划线/连字符",
                new Dictionary<string, object?> { ["field"] = field, [field] = value });
            return null;
        }

        return trimmed;
    }

    private static string? NormalizeComponentFilter(string? value, out BridgeError? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > 128 || trimmed.Any(char.IsControl))
        {
            error = new BridgeError(
                BridgeErrorCodes.InvalidArgument,
                "参数 componentId 非法（长度 ≤128，且不得含控制字符）",
                new Dictionary<string, object?> { ["field"] = "componentId" });
            return null;
        }

        return trimmed;
    }

    /// <summary>解析 from/to：支持 <c>yyyy-MM-dd</c>（整天闭区间）与 ISO-8601 时间；输出 UTC ISO-8601 文本。</summary>
    private static string? ParseBoundary(string? value, bool endOfDay, out BridgeError? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (DateTime.TryParseExact(
                trimmed,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var dateOnly))
        {
            var utc = endOfDay
                ? DateTime.SpecifyKind(dateOnly.Date, DateTimeKind.Utc).AddDays(1).AddTicks(-1)
                : DateTime.SpecifyKind(dateOnly.Date, DateTimeKind.Utc);
            return utc.ToString("O", CultureInfo.InvariantCulture);
        }

        if (DateTimeOffset.TryParse(
                trimmed,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            return parsed.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        }

        error = new BridgeError(
            BridgeErrorCodes.InvalidArgument,
            "参数 from/to 必须是 UTC ISO-8601 日期（yyyy-MM-dd）或时间",
            new Dictionary<string, object?> { ["field"] = endOfDay ? "to" : "from", ["value"] = value });
        return null;
    }

    private static OperationLogItemDto ToDto(OperationLogRecord record) => new(
        Id: record.Id,
        Ts: record.Ts,
        Actor: record.Actor,
        Action: record.Action,
        ComponentId: record.ComponentId,
        RepoType: record.RepoType,
        Target: ParseJson(record.TargetJson),
        Before: ParseJson(record.BeforeJson),
        After: ParseJson(record.AfterJson),
        Result: record.Result,
        ErrorCode: record.ErrorCode,
        RequestId: record.RequestId,
        JobId: record.JobId,
        JobItemId: record.JobItemId,
        BackupRef: record.BackupRef);

    private static JsonElement? ParseJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? BuildTargetJson(string? componentId, string? repoType)
    {
        if (componentId is null && repoType is null)
        {
            return null;
        }

        return JsonSerializer.Serialize(new { componentId, repoType }, WebJson);
    }

    private static string? BuildHeadJson(string? head, string? state)
    {
        if (head is null && state is null)
        {
            return null;
        }

        return state is null
            ? JsonSerializer.Serialize(new { head }, WebJson)
            : JsonSerializer.Serialize(new { head, state }, WebJson);
    }

    private static void WriteCsv(string path, IReadOnlyList<OperationLogRecord> rows)
    {
        var header = string.Join(',', new[]
        {
            "id", "ts", "actor", "action", "component_id", "repo_type", "target_json", "before_json",
            "after_json", "result", "error_code", "request_id", "job_id", "job_item_id", "backup_ref"
        });

        using var writer = new StreamWriter(
            path,
            append: false,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        writer.NewLine = "\r\n";
        writer.WriteLine(header);
        foreach (var row in rows)
        {
            writer.WriteLine(string.Join(',', new[]
            {
                row.Id.ToString(CultureInfo.InvariantCulture),
                Csv(row.Ts),
                Csv(row.Actor),
                Csv(row.Action),
                Csv(row.ComponentId),
                Csv(row.RepoType),
                Csv(row.TargetJson),
                Csv(row.BeforeJson),
                Csv(row.AfterJson),
                Csv(row.Result),
                Csv(row.ErrorCode),
                Csv(row.RequestId),
                Csv(row.JobId),
                Csv(row.JobItemId),
                Csv(row.BackupRef)
            }));
        }
    }

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0)
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    private static void WriteJson(
        string path,
        IReadOnlyList<OperationLogRecord> rows,
        string? from,
        string? to)
    {
        var envelope = new
        {
            schemaVersion = "1.0",
            exportedAt = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            from,
            to,
            count = rows.Count,
            items = rows.Select(record => new
            {
                id = record.Id,
                ts = record.Ts,
                actor = record.Actor,
                action = record.Action,
                componentId = record.ComponentId,
                repoType = record.RepoType,
                target = ParseJson(record.TargetJson),
                before = ParseJson(record.BeforeJson),
                after = ParseJson(record.AfterJson),
                result = record.Result,
                errorCode = record.ErrorCode,
                requestId = record.RequestId,
                jobId = record.JobId,
                jobItemId = record.JobItemId,
                backupRef = record.BackupRef
            }).ToList()
        };

        File.WriteAllText(
            path,
            JsonSerializer.Serialize(envelope, new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                WriteIndented = true
            }),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static CommandResult DatabaseUnavailable() => CommandResult.Failure(
        BridgeErrorCodes.Internal,
        "审计数据库不可用（operation_log 查询/导出已降级；JSONL 审计仍生效）",
        new Dictionary<string, object?> { ["database"] = "unavailable" });

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

    /// <summary>
    /// 审计行序列化形状：M2a 字段名/顺序保持（ts/action/componentId/repoType/state/preHead/postHead/ok/errorCode/
    /// durationMs/actor/requestId），M2b 追加 result/jobId/jobItemId/backupRef；后三者仅在非 null 时输出，
    /// 确保 M2a 单仓行不被新键污染。
    /// </summary>
    private sealed class AuditLine
    {
        [JsonPropertyName("ts")]
        public required string Ts { get; init; }

        [JsonPropertyName("action")]
        public string? Action { get; init; }

        [JsonPropertyName("componentId")]
        public string? ComponentId { get; init; }

        [JsonPropertyName("repoType")]
        public string? RepoType { get; init; }

        [JsonPropertyName("state")]
        public required string State { get; init; }

        [JsonPropertyName("preHead")]
        public string? PreHead { get; init; }

        [JsonPropertyName("postHead")]
        public string? PostHead { get; init; }

        [JsonPropertyName("ok")]
        public bool Ok { get; init; }

        [JsonPropertyName("errorCode")]
        public string? ErrorCode { get; init; }

        [JsonPropertyName("durationMs")]
        public long DurationMs { get; init; }

        [JsonPropertyName("actor")]
        public string? Actor { get; init; }

        [JsonPropertyName("requestId")]
        public string? RequestId { get; init; }

        [JsonPropertyName("result")]
        public required string Result { get; init; }

        [JsonPropertyName("jobId")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? JobId { get; init; }

        [JsonPropertyName("jobItemId")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? JobItemId { get; init; }

        [JsonPropertyName("backupRef")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? BackupRef { get; init; }
    }
}
