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

    /// <summary>SQLite 写失败回退队列文件名（固定，落审计目录；ADR D-026 P1-4）。</summary>
    public const string FallbackFileName = "fallback.jsonl";

    /// <summary><c>repairOperationLog</c> 审计动作名（operation_log.action 扩展值）。</summary>
    public const string RepairOperationLogAction = "REPAIR_OPERATION_LOG";

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
    private readonly object _fallbackSync;
    private readonly int _auditRetentionDays;
    private static string? _testIsolationRoot;
    /// <summary>测试模块初始化调用：把默认审计库/导出目录固定到临时根，避免污染真实 %LOCALAPPDATA%。</summary>
    public static void ConfigureTestIsolation(string root) => _testIsolationRoot = root;

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

        if (_testIsolationRoot is not null)
        {
            return Path.Combine(_testIsolationRoot, "db", AuditDatabase.DatabaseFileName);
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

        if (_testIsolationRoot is not null)
        {
            return Path.Combine(_testIsolationRoot, "exports");
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
        FallbackFilePath = Path.Combine(DirectoryPath, FallbackFileName);
        _sync = FileLocks.GetOrAdd(FilePath, _ => new object());
        _fallbackSync = FileLocks.GetOrAdd(FallbackFilePath, _ => new object());
        ExportsRoot = Path.GetFullPath(
            string.IsNullOrWhiteSpace(exportsRoot) ? DefaultExportsRoot : exportsRoot!.Trim());
        _auditRetentionDays = Math.Clamp(auditRetentionDays, 1, 3650);
        Database = database;
        Database?.Initialize();

        // FR-OP-09 / ADR D-026 P1-4：启动/初始化即把回退队列 + 主 JSONL 中 DB 缺失行回填
        // （按 requestId+jobItemId+ts+action 稳定键去重；成功清空 fallback；失败仅告警，下次再试）。
        if (Database is not null)
        {
            var backfill = BackfillFromJsonlAndFallback();
            if (backfill.Inserted > 0 || backfill.FallbackRemaining > 0)
            {
                HostLog.Info(
                    $"审计启动回填：inserted={backfill.Inserted} skipped={backfill.Skipped} "
                    + $"fallbackRemaining={backfill.FallbackRemaining}");
            }
        }
    }

    /// <summary>审计目录（绝对路径）。</summary>
    public string DirectoryPath { get; }

    /// <summary>审计文件（绝对路径）。</summary>
    public string FilePath { get; }

    /// <summary>SQLite 写失败回退队列文件（绝对路径；成功回填后删除/清空）。</summary>
    public string FallbackFilePath { get; }

    /// <summary>回退队列中尚未回填的行数（诊断/明确降级口径用）。</summary>
    public int PendingFallbackCount => CountFallbackLines();

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

    /// <summary>
    /// 追加一条执行审计（JSONL + SQLite；任一失败仅告警，不影响动作结果）。
    /// <para>M2b-2b（ADR D-026 P1-4）：SQLite 写入失败时把同一行追加到
    /// <see cref="FallbackFilePath"/> 回退队列，下次启动/初始化回填；<paramref name="allowFallback"/>=false
    /// 仅用于 repair 自身审计行（“不写回退”）。</para>
    /// </summary>
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
        string? backupRef = null,
        bool allowFallback = true)
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

        string? line = null;
        try
        {
            line = JsonSerializer.Serialize(
                new AuditLine
                {
                    Ts = ts,
                    Action = actionText,
                    ComponentId = component,
                    RepoType = type,
                    State = stateText,
                    PreHead = pre,
                    PostHead = post,
                    Ok = ok,
                    ErrorCode = error,
                    DurationMs = Math.Max(0, durationMs),
                    Actor = actorText,
                    RequestId = request,
                    Result = resultText,
                    JobId = job,
                    JobItemId = jobItem,
                    BackupRef = backup
                },
                JsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            HostLog.Warn($"审计行序列化失败（{ex.GetType().Name}）：{FilePath}");
        }

        if (line is not null)
        {
            WriteJsonLine(line);
        }

        // SQLite 双写（旁路）：失败进回退队列；列映射见 §8.1（target/before/after JSON 投影）。
        var entry = new OperationLogEntry(
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
            BackupRef: backup);

        var inserted = TryInsertDatabase(entry);
        if (!inserted && allowFallback && Database is not null)
        {
            if (line is null)
            {
                HostLog.Warn($"审计回退队列写入跳过：审计行无法序列化（{FallbackFilePath}）");
            }
            else
            {
                WriteFallbackLine(line);
            }
        }
    }

    /// <summary>SQLite 单行写入（旁路失败返回 false；不抛异常）。</summary>
    private bool TryInsertDatabase(OperationLogEntry entry)
    {
        if (Database is null)
        {
            return true;
        }

        try
        {
            return Database.Insert(entry);
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or ArgumentException
            or System.Security.SecurityException
            or InvalidOperationException)
        {
            HostLog.Warn($"审计 SQLite 双写失败（{ex.GetType().Name}）：{Database.FilePath}");
            return false;
        }
    }

    /// <summary>
    /// 回退队列写入（SQLite 失败时；只追加审计元数据行，不含 token/output）。
    /// 失败仅告警：JSONL 主链路已写入，查询/导出会明确 DB 可用性与 pendingFallback。
    /// </summary>
    private void WriteFallbackLine(string line)
    {
        try
        {
            var bytes = Encoding.UTF8.GetBytes(line + "\n");
            lock (_fallbackSync)
            {
                Directory.CreateDirectory(DirectoryPath);
                using var stream = new FileStream(
                    FallbackFilePath,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.ReadWrite,
                    bufferSize: 4096,
                    FileOptions.None);
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush();
            }

            HostLog.Warn($"审计 SQLite 写入失败：已写入回退队列（下次启动回填）{FallbackFilePath}");
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or ArgumentException
            or System.Security.SecurityException)
        {
            HostLog.Warn($"审计回退队列写入失败（{ex.GetType().Name}）：{FallbackFilePath}");
        }
    }

    /// <summary>读取回退队列行（文件缺失/不可读返回空表）。</summary>
    public IReadOnlyList<string> ReadFallbackLines()
    {
        try
        {
            lock (_fallbackSync)
            {
                if (!File.Exists(FallbackFilePath))
                {
                    return Array.Empty<string>();
                }

                return File.ReadAllLines(FallbackFilePath);
            }
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or ArgumentException)
        {
            HostLog.Warn($"审计回退队列读取失败（{ex.GetType().Name}）：{FallbackFilePath}");
            return Array.Empty<string>();
        }
    }

    private int CountFallbackLines()
    {
        var count = 0;
        foreach (var line in ReadFallbackLines())
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// M2b-2b 启动回填（FR-OP-09 / ADR D-026 P1-4）：把主 JSONL + 回退队列中 SQLite 缺失的行
    /// 按稳定键（<c>requestId+jobItemId+ts+action</c>）回填；回退队列中成功回填的行清除，失败/不可解析的保留。
    /// 失败只告警（返回 <see cref="AuditBackfillResult.Succeeded"/>=false）。
    /// </summary>
    public AuditBackfillResult BackfillFromJsonlAndFallback()
    {
        if (Database is null || !DatabaseAvailable)
        {
            return new AuditBackfillResult(0, 0, CountFallbackLines(), Succeeded: false);
        }

        var keys = Database.QueryStableKeys();
        if (keys is null)
        {
            return new AuditBackfillResult(0, 0, CountFallbackLines(), Succeeded: false);
        }

        var keySet = new HashSet<string>(keys, StringComparer.Ordinal);
        var inserted = 0;
        var skipped = 0;
        var dbHealthy = true;

        // 1) 主 JSONL（M2a 起始终写入；DB 缺行/重建后由此回填）。
        foreach (var line in ReadAuditLines())
        {
            var entry = ParseAuditLine(line);
            if (entry is null)
            {
                continue;
            }

            var key = AuditDatabase.BuildStableKey(entry.RequestId, entry.JobItemId, entry.Ts, entry.Action);
            if (!keySet.Add(key))
            {
                skipped++;
                continue;
            }

            if (Database.Insert(entry))
            {
                inserted++;
                continue;
            }

            // DB 运行期再次失败：停止后续写入（保留回退队列）；键不回滚（下次启动重新计算）。
            dbHealthy = false;
            break;
        }

        // 2) 回退队列（成功回填/DB 已有 → 丢弃；失败/不可解析 → 保留）。
        var fallbackLines = ReadFallbackLines();
        var remaining = new List<string>();
        if (dbHealthy)
        {
            foreach (var line in fallbackLines)
            {
                var entry = ParseAuditLine(line);
                if (entry is null)
                {
                    remaining.Add(line);
                    continue;
                }

                var key = AuditDatabase.BuildStableKey(entry.RequestId, entry.JobItemId, entry.Ts, entry.Action);
                if (!keySet.Add(key))
                {
                    skipped++;
                    continue;
                }

                if (Database.Insert(entry))
                {
                    inserted++;
                    continue;
                }

                dbHealthy = false;
                remaining.Add(line);
            }
        }
        else
        {
            remaining.AddRange(fallbackLines);
        }

        if (dbHealthy && remaining.Count != fallbackLines.Count)
        {
            WriteFallbackRemaining(remaining);
        }

        if (inserted > 0 || remaining.Count > 0)
        {
            HostLog.Info(
                $"审计回填完成：JSONL/回退 → operation_log 插入 {inserted} 行（跳过 {skipped}，"
                + $"回退剩余 {remaining.Count}）");
        }

        return new AuditBackfillResult(inserted, skipped, remaining.Count, dbHealthy);
    }

    /// <summary>重写回退队列（空 = 删除文件）；失败仅告警（下次启动重试）。</summary>
    private void WriteFallbackRemaining(IReadOnlyList<string> remaining)
    {
        try
        {
            lock (_fallbackSync)
            {
                if (remaining.Count == 0)
                {
                    if (File.Exists(FallbackFilePath))
                    {
                        File.Delete(FallbackFilePath);
                    }

                    return;
                }

                Directory.CreateDirectory(DirectoryPath);
                var temporary = FallbackFilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    File.WriteAllLines(
                        temporary,
                        remaining,
                        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                    File.Move(temporary, FallbackFilePath, overwrite: true);
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
                            // 临时文件清理失败不影响回填结果。
                        }
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or ArgumentException)
        {
            HostLog.Warn($"审计回退队列重写失败（{ex.GetType().Name}）：{FallbackFilePath}");
        }
    }

    /// <summary>
    /// <c>repairOperationLog</c>（M2b-2b / ADR D-026 P1-4）：必须 <c>confirm=true</c>；删除并重建
    /// <c>operation_log</c>，再从主 JSONL/回退队列回填（用于清理历史测试污染），最后写 1 条自身审计
    /// （<c>action=REPAIR_OPERATION_LOG</c>，不写回退队列）。返回 <c>{ removed, inserted, total }</c>。
    /// </summary>
    public CommandResult RepairOperationLog(RepairOperationLogParams? parameters)
    {
        if (parameters?.Confirm != true)
        {
            return CommandResult.Failure(
                BridgeErrorCodes.ConfirmRequired,
                "repairOperationLog 会删除并重建 operation_log 表（仅保留 JSONL 可回填行），需 args.confirm=true 二次确认",
                new Dictionary<string, object?>
                {
                    ["action"] = RepairOperationLogAction,
                    ["requiresConfirm"] = true
                });
        }

        if (Database is null || !DatabaseAvailable)
        {
            return DatabaseUnavailable();
        }

        var removed = Database.RecreateOperationLog();
        if (removed is null)
        {
            return CommandResult.Failure(
                BridgeErrorCodes.Internal,
                "审计库 operation_log 重建失败（DB 不可用/IO 失败）；未伪装成功",
                new Dictionary<string, object?> { ["database"] = "unavailable" });
        }

        var keys = new HashSet<string>(StringComparer.Ordinal);
        var inserted = 0;
        var skipped = 0;

        foreach (var line in ReadAuditLines())
        {
            var entry = ParseAuditLine(line);
            if (entry is null)
            {
                continue;
            }

            var key = AuditDatabase.BuildStableKey(entry.RequestId, entry.JobItemId, entry.Ts, entry.Action);
            if (!keys.Add(key))
            {
                skipped++;
                continue;
            }

            if (Database.Insert(entry))
            {
                inserted++;
            }
        }

        // 回退队列同样回填（成功即清空；失败行保留）。
        var fallbackLines = ReadFallbackLines();
        var remaining = new List<string>();
        foreach (var line in fallbackLines)
        {
            var entry = ParseAuditLine(line);
            if (entry is null)
            {
                remaining.Add(line);
                continue;
            }

            var key = AuditDatabase.BuildStableKey(entry.RequestId, entry.JobItemId, entry.Ts, entry.Action);
            if (!keys.Add(key))
            {
                skipped++;
                continue;
            }

            if (Database.Insert(entry))
            {
                inserted++;
            }
            else
            {
                remaining.Add(line);
            }
        }

        if (remaining.Count != fallbackLines.Count)
        {
            WriteFallbackRemaining(remaining);
        }

        // 修复动作自身的审计行（必须 1 行；明确“不写回退”，DB 刚重建应可写）。
        Record(
            action: RepairOperationLogAction,
            componentId: null,
            repoType: null,
            state: "N/A",
            preHead: null,
            postHead: null,
            ok: true,
            errorCode: null,
            durationMs: 0,
            actor: Environment.UserName,
            requestId: Guid.NewGuid().ToString("N"),
            result: "OK",
            allowFallback: false);

        var total = Database.Count();
        if (total < 0)
        {
            total = inserted;
        }

        HostLog.Warn(
            $"repairOperationLog：removed={removed} inserted={inserted} skipped={skipped} total={total} "
            + $"fallbackRemaining={remaining.Count}");
        return CommandResult.Success(new RepairOperationLogResultDto(removed.Value, inserted, total));
    }

    /// <summary>读取主 JSONL 行（文件缺失/不可读返回空表）。</summary>
    private IReadOnlyList<string> ReadAuditLines()
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
            HostLog.Warn($"审计 JSONL 读取失败（{ex.GetType().Name}）：{FilePath}");
            return Array.Empty<string>();
        }
    }

    /// <summary>把 JSONL/回退队列行解析为 SQLite 行（缺字段按 M2a 兼容口径补齐；损坏行返回 null）。</summary>
    private static OperationLogEntry? ParseAuditLine(string line)
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

            var ts = GetString(root, "ts");
            var action = GetString(root, "action");
            if (string.IsNullOrWhiteSpace(ts) || string.IsNullOrWhiteSpace(action))
            {
                return null;
            }

            var component = Sanitize(GetString(root, "componentId"));
            var repoType = Sanitize(GetString(root, "repoType"));
            var state = Sanitize(GetString(root, "state"));
            var preHead = Sanitize(GetString(root, "preHead"));
            var postHead = Sanitize(GetString(root, "postHead"));
            var ok = root.TryGetProperty("ok", out var okElement) && okElement.ValueKind == JsonValueKind.True;
            var result = Sanitize(GetString(root, "result")) ?? (ok ? "OK" : "FAILED");

            return new OperationLogEntry(
                Ts: ts!,
                Actor: Sanitize(GetString(root, "actor")),
                Action: action!,
                ComponentId: component,
                RepoType: repoType,
                TargetJson: BuildTargetJson(component, repoType),
                BeforeJson: BuildHeadJson(preHead, state),
                AfterJson: BuildHeadJson(postHead, null),
                Result: result,
                ErrorCode: Sanitize(GetString(root, "errorCode")),
                RequestId: Sanitize(GetString(root, "requestId")),
                JobId: Sanitize(GetString(root, "jobId")),
                JobItemId: Sanitize(GetString(root, "jobItemId")),
                BackupRef: Sanitize(GetString(root, "backupRef")));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? GetString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>回退队列行 → 审计记录（负 id 表示尚未入 SQLite；供查询/导出合并）。</summary>
    private List<OperationLogRecord> ReadFallbackRecords(OperationLogFilter? filter = null, ISet<string>? excludeKeys = null)
    {
        var records = new List<OperationLogRecord>();
        long nextId = 0;
        foreach (var line in ReadFallbackLines())
        {
            var entry = ParseAuditLine(line);
            if (entry is null)
            {
                continue;
            }

            if (filter is not null && !MatchesFilter(entry, filter))
            {
                continue;
            }

            if (excludeKeys is not null
                && excludeKeys.Contains(AuditDatabase.BuildStableKey(
                    entry.RequestId,
                    entry.JobItemId,
                    entry.Ts,
                    entry.Action)))
            {
                continue;
            }

            nextId--;
            records.Add(new OperationLogRecord(
                nextId,
                entry.Ts,
                entry.Actor,
                entry.Action,
                entry.ComponentId,
                entry.RepoType,
                entry.TargetJson,
                entry.BeforeJson,
                entry.AfterJson,
                entry.Result,
                entry.ErrorCode,
                entry.RequestId,
                entry.JobId,
                entry.JobItemId,
                entry.BackupRef));
        }

        return records;
    }

    private static bool MatchesFilter(OperationLogEntry entry, OperationLogFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.FromUtc)
            && string.CompareOrdinal(entry.Ts, filter.FromUtc) < 0)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(filter.ToUtc)
            && string.CompareOrdinal(entry.Ts, filter.ToUtc) > 0)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(filter.Action)
            && !string.Equals(entry.Action, filter.Action, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(filter.ComponentId)
            && !string.Equals(entry.ComponentId, filter.ComponentId, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(filter.Result)
            && !string.Equals(entry.Result, filter.Result, StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    /// <summary>DB 行 + 回退队列行合并后按 ts 倒序分页（DB 可用但存在 pending 行时使用）。</summary>
    private OperationLogPageDto? BuildMergedPage(
        OperationLogFilter filter,
        IReadOnlyList<OperationLogRecord> fallbackRecords)
    {
        var dbFilter = filter with { Limit = MaxExportRows, Offset = 0 };
        var dbRows = Database!.QueryAll(dbFilter, MaxExportRows);
        if (dbRows is null)
        {
            return null;
        }

        var dbKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in dbRows)
        {
            dbKeys.Add(AuditDatabase.BuildStableKey(row.RequestId, row.JobItemId, row.Ts, row.Action));
        }

        var combined = new List<(string Ts, OperationLogItemDto Dto)>(dbRows.Count + fallbackRecords.Count);
        foreach (var row in dbRows)
        {
            combined.Add((row.Ts, ToDto(row)));
        }

        foreach (var record in fallbackRecords)
        {
            if (dbKeys.Contains(AuditDatabase.BuildStableKey(
                    record.RequestId,
                    record.JobItemId,
                    record.Ts,
                    record.Action)))
            {
                continue;
            }

            combined.Add((record.Ts, ToDto(record)));
        }

        combined.Sort((left, right) => string.CompareOrdinal(right.Ts, left.Ts));
        var total = combined.Count;
        var items = combined
            .Skip(filter.Offset)
            .Take(filter.Limit)
            .Select(entry => entry.Dto)
            .ToList();
        return new OperationLogPageDto(items, total);
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

        // M2b-2b：DB 可用但回退队列仍有 pending 行时，查询结果与回退队列合并（不得把 DB 缺行隐藏成成功）。
        var fallbackRecords = ReadFallbackRecords(filter!);
        if (fallbackRecords.Count > 0)
        {
            var merged = BuildMergedPage(filter!, fallbackRecords);
            if (merged is null)
            {
                return DatabaseUnavailable();
            }

            return CommandResult.Success(merged);
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
        var dbRows = Database.QueryAll(exportFilter, MaxExportRows);
        if (dbRows is null)
        {
            return DatabaseUnavailable();
        }

        // M2b-2b：导出同样合并回退队列（DB 可用但存在未回填行时不得导出“缺行成功”）。
        var mergedRows = new List<OperationLogRecord>(dbRows);
        var fallbackRecords = ReadFallbackRecords(sourceFilter);
        if (fallbackRecords.Count > 0)
        {
            var dbKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in dbRows)
            {
                dbKeys.Add(AuditDatabase.BuildStableKey(row.RequestId, row.JobItemId, row.Ts, row.Action));
            }

            foreach (var record in fallbackRecords)
            {
                if (dbKeys.Contains(AuditDatabase.BuildStableKey(
                        record.RequestId,
                        record.JobItemId,
                        record.Ts,
                        record.Action)))
                {
                    continue;
                }

                mergedRows.Add(record);
            }

            mergedRows.Sort((left, right) => string.CompareOrdinal(left.Ts, right.Ts));
        }

        if (mergedRows.Count > MaxExportRows)
        {
            return CommandResult.Failure(
                BridgeErrorCodes.TooManyEntries,
                $"导出行数超过上限（{MaxExportRows}）：请缩小 from/to 范围",
                new Dictionary<string, object?> { ["field"] = "range", ["total"] = mergedRows.Count });
        }

        IReadOnlyList<OperationLogRecord> rows = mergedRows;
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

    private void WriteJsonLine(string line)
    {
        try
        {
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

    private CommandResult DatabaseUnavailable() => CommandResult.Failure(
        BridgeErrorCodes.Internal,
        "审计数据库不可用（operation_log 查询/导出已降级；JSONL 审计仍生效，失败行在 fallback.jsonl 等待回填）",
        new Dictionary<string, object?>
        {
            ["database"] = "unavailable",
            ["pendingFallback"] = PendingFallbackCount
        });

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
