using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace MbdGitTool.Host.Services;

/// <summary>
/// <c>operation_log</c> 审计行（SRS §8.1 冻结列；M2b-2 完整表）。
/// <para>字段映射：<c>target_json</c> 投影出 componentId/repoType，<c>before_json</c>/<c>after_json</c>
/// 投影出 head/state（AC-OP-33 要求“物理列为 target_json/before_json/after_json 时须可 JSON 投影出同名字段”）。</para>
/// </summary>
public sealed record OperationLogRecord(
    long Id,
    string Ts,
    string? Actor,
    string Action,
    string? ComponentId,
    string? RepoType,
    string? TargetJson,
    string? BeforeJson,
    string? AfterJson,
    string Result,
    string? ErrorCode,
    string? RequestId,
    string? JobId,
    string? JobItemId,
    string? BackupRef);

/// <summary>待写入 <c>operation_log</c> 的行（<see cref="OperationLogRecord.Id"/> 由 SQLite AUTOINCREMENT 分配）。</summary>
public sealed record OperationLogEntry(
    string Ts,
    string? Actor,
    string Action,
    string? ComponentId,
    string? RepoType,
    string? TargetJson,
    string? BeforeJson,
    string? AfterJson,
    string Result,
    string? ErrorCode,
    string? RequestId,
    string? JobId,
    string? JobItemId,
    string? BackupRef);

/// <summary>
/// 查询过滤（from/to 为已规范化的 UTC ISO-8601 闭区间字符串，null = 不限；limit/offset 由调用方校验）。
/// </summary>
public sealed record OperationLogFilter(
    string? FromUtc = null,
    string? ToUtc = null,
    string? Action = null,
    string? ComponentId = null,
    string? Result = null,
    int Limit = 100,
    int Offset = 0);

/// <summary>查询结果：当前页行 + 过滤条件下的总行数（用于分页）。</summary>
public sealed record OperationLogQueryResult(IReadOnlyList<OperationLogRecord> Rows, int Total);

/// <summary>
/// M2b-2 SQLite 审计库（<c>%LOCALAPPDATA%\MbdGitTool\db\mbd.db</c>）：
/// <para>连接参数：<c>journal_mode=WAL; synchronous=NORMAL; busy_timeout=5000; foreign_keys=ON</c>；
/// 启动建表/迁移由 <c>PRAGMA user_version</c> 判定（当前 schema=1）；启动 <c>PRAGMA quick_check</c>，
/// 损坏时把旧库重命名（<c>.corrupt-&lt;UTC stamp&gt;</c>）后建新库（SRS §8.2；DB 可整库重建）。</para>
/// <para><b>旁路能力</b>：任何初始化/写入/查询失败都只告警并降级（JSONL 审计仍生效），绝不影响动作本身。
/// 写入只含元数据，绝不写 PAT/token/命令 output。</para>
/// </summary>
public sealed class AuditDatabase
{
    /// <summary>固定库文件名（SRS §8.2：DB 固定放 <c>%LOCALAPPDATA%\MbdGitTool\</c>）。</summary>
    public const string DatabaseFileName = "mbd.db";

    /// <summary>当前 schema 版本（<c>PRAGMA user_version</c>）。</summary>
    public const int SchemaVersion = 1;

    /// <summary>默认库目录：<c>%LOCALAPPDATA%\MbdGitTool\db</c>。</summary>
    public static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MbdGitTool",
        "db");

    /// <summary>默认库文件：<c>%LOCALAPPDATA%\MbdGitTool\db\mbd.db</c>。</summary>
    public static string DefaultFilePath => Path.Combine(DefaultDirectory, DatabaseFileName);

    private const string CreateTableSql = """
        CREATE TABLE IF NOT EXISTS operation_log (
            id           INTEGER PRIMARY KEY AUTOINCREMENT,
            ts           TEXT    NOT NULL,
            actor        TEXT    NULL,
            action       TEXT    NOT NULL,
            component_id TEXT    NULL,
            repo_type    TEXT    NULL,
            target_json  TEXT    NULL,
            before_json  TEXT    NULL,
            after_json   TEXT    NULL,
            result       TEXT    NOT NULL,
            error_code   TEXT    NULL,
            request_id   TEXT    NULL,
            job_id       TEXT    NULL,
            job_item_id  TEXT    NULL,
            backup_ref   TEXT    NULL
        );
        """;

    private static readonly string[] CreateIndexSql =
    {
        "CREATE INDEX IF NOT EXISTS idx_operation_log_ts ON operation_log(ts);",
        "CREATE INDEX IF NOT EXISTS idx_operation_log_action ON operation_log(action);",
        "CREATE INDEX IF NOT EXISTS idx_operation_log_component ON operation_log(component_id);",
        "CREATE INDEX IF NOT EXISTS idx_operation_log_result ON operation_log(result);"
    };

    private const string SelectColumns =
        "id, ts, actor, action, component_id, repo_type, target_json, before_json, after_json, "
        + "result, error_code, request_id, job_id, job_item_id, backup_ref";

    private readonly string _connectionString;
    private readonly object _sync = new();
    private volatile bool _available;

    /// <summary>按显式路径创建（目录不存在时在 <see cref="Initialize"/> 中创建）；空 = 默认路径。</summary>
    public AuditDatabase(string? filePath = null)
    {
        FilePath = Path.GetFullPath(
            string.IsNullOrWhiteSpace(filePath) ? DefaultFilePath : filePath!.Trim());
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = FilePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = true
        }.ToString();
    }

    /// <summary>库文件绝对路径。</summary>
    public string FilePath { get; }

    /// <summary>是否已成功初始化并可读写（false 时调用方降级为 JSONL-only）。</summary>
    public bool Available => _available;

    /// <summary>实际加载的 SQLite 版本（启动记录；SRS §8.2 要求 ≥3.51.3）。</summary>
    public string? SqliteVersion { get; private set; }

    /// <summary>
    /// 初始化：建目录 → 打开连接（WAL/quick_check）→ 建表/迁移（user_version）→ 记录 sqlite_version。
    /// 永不抛异常；失败返回 false 并告警（审计旁路）。
    /// <para>损坏库（SQLite 无法读取/quick_check 失败）会把旧库重命名隔离后重建一次；
    /// 高版本 user_version（新程序写的库）不隔离、只禁用写入，避免降级损坏数据。</para>
    /// </summary>
    public bool Initialize()
    {
        lock (_sync)
        {
            if (_available)
            {
                return true;
            }

            var result = TryInitializeCore();
            if (result == InitResult.Ok)
            {
                return true;
            }

            if (result == InitResult.Corrupt)
            {
                // 释放池中仍持有损坏文件的连接句柄，否则隔离重命名会因文件占用失败。
                SqliteConnection.ClearAllPools();
                QuarantineCorruptDatabase();
                return TryInitializeCore() == InitResult.Ok;
            }

            return false;
        }
    }

    private enum InitResult
    {
        Ok,
        Corrupt,
        Failed
    }

    private InitResult TryInitializeCore()
    {
        try
        {
            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var connection = OpenConnection(setJournalMode: true);
            if (!QuickCheck(connection))
            {
                HostLog.Warn($"审计库 quick_check 失败：{FilePath}");
                return InitResult.Corrupt;
            }

            if (!TryEnsureSchema(connection))
            {
                return InitResult.Failed;
            }

            SqliteVersion = ReadSqliteVersion(connection);
            _available = true;
            HostLog.Info(
                $"审计库已就绪：{FilePath}（sqlite={SqliteVersion ?? "<unknown>"}，schema={SchemaVersion}）");
            return InitResult.Ok;
        }
        catch (SqliteException ex)
        {
            _available = false;
            // 只记录异常类型：库路径含用户目录，不输出内容。
            HostLog.Warn($"审计库读取失败（{ex.GetType().Name}）：{FilePath}；尝试隔离重建");
            return InitResult.Corrupt;
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or NotSupportedException
            or ArgumentException)
        {
            _available = false;
            HostLog.Warn($"审计库初始化失败（{ex.GetType().Name}）：{FilePath}；降级为 JSONL-only");
            return InitResult.Failed;
        }
    }

    /// <summary>追加 1 行审计（失败仅告警并返回 false，不影响动作）。</summary>
    public bool Insert(OperationLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (!_available)
        {
            return false;
        }

        try
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO operation_log
                    (ts, actor, action, component_id, repo_type, target_json, before_json, after_json,
                     result, error_code, request_id, job_id, job_item_id, backup_ref)
                VALUES
                    ($ts, $actor, $action, $componentId, $repoType, $targetJson, $beforeJson, $afterJson,
                     $result, $errorCode, $requestId, $jobId, $jobItemId, $backupRef);
                """;
            command.Parameters.AddWithValue("$ts", entry.Ts);
            command.Parameters.AddWithValue("$actor", (object?)entry.Actor ?? DBNull.Value);
            command.Parameters.AddWithValue("$action", entry.Action);
            command.Parameters.AddWithValue("$componentId", (object?)entry.ComponentId ?? DBNull.Value);
            command.Parameters.AddWithValue("$repoType", (object?)entry.RepoType ?? DBNull.Value);
            command.Parameters.AddWithValue("$targetJson", (object?)entry.TargetJson ?? DBNull.Value);
            command.Parameters.AddWithValue("$beforeJson", (object?)entry.BeforeJson ?? DBNull.Value);
            command.Parameters.AddWithValue("$afterJson", (object?)entry.AfterJson ?? DBNull.Value);
            command.Parameters.AddWithValue("$result", entry.Result);
            command.Parameters.AddWithValue("$errorCode", (object?)entry.ErrorCode ?? DBNull.Value);
            command.Parameters.AddWithValue("$requestId", (object?)entry.RequestId ?? DBNull.Value);
            command.Parameters.AddWithValue("$jobId", (object?)entry.JobId ?? DBNull.Value);
            command.Parameters.AddWithValue("$jobItemId", (object?)entry.JobItemId ?? DBNull.Value);
            command.Parameters.AddWithValue("$backupRef", (object?)entry.BackupRef ?? DBNull.Value);
            command.ExecuteNonQuery();
            return true;
        }
        catch (Exception ex) when (ex is SqliteException or InvalidOperationException or IOException)
        {
            HostLog.Warn($"审计库写入失败（{ex.GetType().Name}）：仅保留 JSONL 行");
            return false;
        }
    }

    /// <summary>分页查询（按 id 倒序 = 新行在前）；失败返回 null（调用方映射 E_INTERNAL）。</summary>
    public OperationLogQueryResult? Query(OperationLogFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        if (!_available)
        {
            return null;
        }

        try
        {
            var (where, parameters) = BuildWhere(filter);
            using var connection = OpenConnection();

            int total;
            using (var countCommand = connection.CreateCommand())
            {
                countCommand.CommandText = "SELECT COUNT(*) FROM operation_log" + where;
                AddParameters(countCommand, parameters);
                total = Convert.ToInt32(
                    countCommand.ExecuteScalar() ?? 0,
                    CultureInfo.InvariantCulture);
            }

            var rows = new List<OperationLogRecord>();
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT " + SelectColumns + " FROM operation_log" + where
                    + " ORDER BY id DESC LIMIT $limit OFFSET $offset;";
                AddParameters(command, parameters);
                command.Parameters.AddWithValue("$limit", filter.Limit);
                command.Parameters.AddWithValue("$offset", filter.Offset);
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    rows.Add(ReadRecord(reader));
                }
            }

            return new OperationLogQueryResult(rows, total);
        }
        catch (Exception ex) when (ex is SqliteException
            or InvalidOperationException
            or IOException
            or FormatException
            or OverflowException)
        {
            HostLog.Warn($"审计库查询失败（{ex.GetType().Name}）");
            return null;
        }
    }

    /// <summary>按过滤条件取全部行（导出用；<paramref name="maxRows"/> 上限由调用方校验）。</summary>
    public IReadOnlyList<OperationLogRecord>? QueryAll(OperationLogFilter filter, int maxRows)
    {
        ArgumentNullException.ThrowIfNull(filter);
        if (!_available)
        {
            return null;
        }

        try
        {
            var (where, parameters) = BuildWhere(filter);
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT " + SelectColumns + " FROM operation_log" + where
                + " ORDER BY id ASC LIMIT $limit;";
            AddParameters(command, parameters);
            command.Parameters.AddWithValue("$limit", maxRows);

            var rows = new List<OperationLogRecord>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                rows.Add(ReadRecord(reader));
            }

            return rows;
        }
        catch (Exception ex) when (ex is SqliteException
            or InvalidOperationException
            or IOException
            or FormatException
            or OverflowException)
        {
            HostLog.Warn($"审计库导出查询失败（{ex.GetType().Name}）");
            return null;
        }
    }

    /// <summary>删除 <c>ts &lt; cutoffUtc</c> 的行（90 天保留清理）；失败返回 0。</summary>
    public int DeleteOlderThan(string cutoffUtc)
    {
        if (!_available)
        {
            return 0;
        }

        try
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM operation_log WHERE ts < $cutoff;";
            command.Parameters.AddWithValue("$cutoff", cutoffUtc);
            return command.ExecuteNonQuery();
        }
        catch (Exception ex) when (ex is SqliteException or InvalidOperationException or IOException)
        {
            HostLog.Warn($"审计库保留清理失败（{ex.GetType().Name}）");
            return 0;
        }
    }

    /// <summary>当前总行数（诊断/测试用；不可用返回 -1）。</summary>
    public int Count()
    {
        if (!_available)
        {
            return -1;
        }

        try
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM operation_log;";
            return Convert.ToInt32(command.ExecuteScalar() ?? 0, CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is SqliteException or InvalidOperationException or IOException)
        {
            HostLog.Warn($"审计库计数失败（{ex.GetType().Name}）");
            return -1;
        }
    }

    // ------------------------------------------------------------------ internals

    private SqliteConnection OpenConnection(bool setJournalMode = false)
    {
        var connection = new SqliteConnection(_connectionString);
        try
        {
            connection.Open();
            using var command = connection.CreateCommand();

            // WAL 是库级持久设置（仅初始化时设置）；busy_timeout/synchronous/foreign_keys 为连接级。
            command.CommandText = setJournalMode
                ? """
                  PRAGMA journal_mode=WAL;
                  PRAGMA synchronous=NORMAL;
                  PRAGMA busy_timeout=5000;
                  PRAGMA foreign_keys=ON;
                  """
                : """
                  PRAGMA synchronous=NORMAL;
                  PRAGMA busy_timeout=5000;
                  PRAGMA foreign_keys=ON;
                  """;
            command.ExecuteNonQuery();
            return connection;
        }
        catch
        {
            // 打开/PRAGMA 失败（如损坏库）必须释放连接句柄，否则隔离重命名会被占用而失败。
            connection.Dispose();
            throw;
        }
    }

    private static bool QuickCheck(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA quick_check;";
        using var reader = command.ExecuteReader();
        var ok = false;
        while (reader.Read())
        {
            var value = reader.IsDBNull(0) ? null : reader.GetString(0);
            if (!string.Equals(value, "ok", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            ok = true;
        }

        return ok;
    }

    private static bool TryEnsureSchema(SqliteConnection connection)
    {
        var version = GetUserVersion(connection);
        if (version > SchemaVersion)
        {
            HostLog.Warn(
                $"审计库 user_version={version} 高于本程序支持的 {SchemaVersion}；为避免降级损坏，禁用 SQLite 审计写入");
            return false;
        }

        if (version == SchemaVersion)
        {
            return true;
        }

        // version == 0：首次建库。后续版本迁移（>1）在提升 SchemaVersion 时在此追加，迁移前按 SRS §8.2
        // 使用 VACUUM INTO 备份旧库并在失败时回滚。
        using var transaction = connection.BeginTransaction();
        using (var createTable = connection.CreateCommand())
        {
            createTable.Transaction = transaction;
            createTable.CommandText = CreateTableSql;
            createTable.ExecuteNonQuery();
        }

        foreach (var sql in CreateIndexSql)
        {
            using var createIndex = connection.CreateCommand();
            createIndex.Transaction = transaction;
            createIndex.CommandText = sql;
            createIndex.ExecuteNonQuery();
        }

        using (var setVersion = connection.CreateCommand())
        {
            setVersion.Transaction = transaction;
            setVersion.CommandText = "PRAGMA user_version = " + SchemaVersion.ToString(CultureInfo.InvariantCulture) + ";";
            setVersion.ExecuteNonQuery();
        }

        transaction.Commit();
        return true;
    }

    private static int GetUserVersion(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(command.ExecuteScalar() ?? 0, CultureInfo.InvariantCulture);
    }

    private static string? ReadSqliteVersion(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT sqlite_version();";
        return command.ExecuteScalar() as string;
    }

    private void QuarantineCorruptDatabase()
    {
        var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
        {
            var candidate = FilePath + suffix;
            if (!File.Exists(candidate))
            {
                continue;
            }

            var destination = candidate + ".corrupt-" + stamp;
            try
            {
                File.Move(candidate, destination, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                HostLog.Warn($"审计库损坏隔离失败（{ex.GetType().Name}）：{suffix}");
            }
        }

        HostLog.Warn($"审计库 quick_check 失败：旧库已隔离（{FilePath}.corrupt-{stamp}），将重建新库");
    }

    private static (string Where, Dictionary<string, object> Parameters) BuildWhere(OperationLogFilter filter)
    {
        var clauses = new List<string>(5);
        var parameters = new Dictionary<string, object>(StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(filter.FromUtc))
        {
            clauses.Add("ts >= $from");
            parameters["$from"] = filter.FromUtc;
        }

        if (!string.IsNullOrWhiteSpace(filter.ToUtc))
        {
            clauses.Add("ts <= $to");
            parameters["$to"] = filter.ToUtc;
        }

        if (!string.IsNullOrWhiteSpace(filter.Action))
        {
            clauses.Add("action = $action");
            parameters["$action"] = filter.Action;
        }

        if (!string.IsNullOrWhiteSpace(filter.ComponentId))
        {
            clauses.Add("component_id = $componentId");
            parameters["$componentId"] = filter.ComponentId;
        }

        if (!string.IsNullOrWhiteSpace(filter.Result))
        {
            clauses.Add("result = $result");
            parameters["$result"] = filter.Result;
        }

        var where = clauses.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", clauses);
        return (where, parameters);
    }

    private static void AddParameters(SqliteCommand command, Dictionary<string, object> parameters)
    {
        foreach (var pair in parameters)
        {
            command.Parameters.AddWithValue(pair.Key, pair.Value);
        }
    }

    private static OperationLogRecord ReadRecord(SqliteDataReader reader) => new(
        reader.GetInt64(0),
        reader.GetString(1),
        reader.IsDBNull(2) ? null : reader.GetString(2),
        reader.GetString(3),
        reader.IsDBNull(4) ? null : reader.GetString(4),
        reader.IsDBNull(5) ? null : reader.GetString(5),
        reader.IsDBNull(6) ? null : reader.GetString(6),
        reader.IsDBNull(7) ? null : reader.GetString(7),
        reader.IsDBNull(8) ? null : reader.GetString(8),
        reader.GetString(9),
        reader.IsDBNull(10) ? null : reader.GetString(10),
        reader.IsDBNull(11) ? null : reader.GetString(11),
        reader.IsDBNull(12) ? null : reader.GetString(12),
        reader.IsDBNull(13) ? null : reader.GetString(13),
        reader.IsDBNull(14) ? null : reader.GetString(14));
}
