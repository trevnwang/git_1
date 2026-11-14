using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using MbdGitTool.Core.Contracts;
using MbdGitTool.Host.Services;
using Xunit;

namespace MbdGitTool.Host.Tests;

/// <summary>
/// M2b-2 审计完整表（SRS §8.1 <c>operation_log</c> / FR-OP-09 / AC-OP-33）：
/// SQLite 建表（WAL/user_version）、查询/分页/JSON 投影、CSV/JSON 导出、90 天保留清理、JSONL 兼容。
/// </summary>
public sealed class M2b2AuditDatabaseTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "MbdGitToolTests",
        "m2b2-audit",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Initialize_CreatesOperationLogSchemaWithWalAndUserVersion2()
    {
        var path = Path.Combine(_root, "db", "mbd.db");
        var database = new AuditDatabase(path);

        Assert.True(database.Initialize());
        Assert.True(database.Available);
        Assert.False(string.IsNullOrWhiteSpace(database.SqliteVersion));

        using var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = path }.ToString());
        connection.Open();

        Assert.Equal((long)AuditDatabase.SchemaVersion, Scalar(connection, "PRAGMA user_version;"));
        Assert.Equal("wal", Scalar(connection, "PRAGMA journal_mode;")?.ToString());

        var columns = new List<string>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA table_info(operation_log);";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                columns.Add(reader.GetString(1));
            }
        }

        Assert.Equal(
            new[]
            {
                "id", "ts", "actor", "action", "component_id", "repo_type", "target_json", "before_json",
                "after_json", "result", "error_code", "request_id", "job_id", "job_item_id", "backup_ref"
            },
            columns);
    }

    [Fact]
    public void Initialize_CorruptFile_QuarantinesAndRecreates()
    {
        var path = Path.Combine(_root, "db", "mbd.db");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "this is definitely not a sqlite database");

        var database = new AuditDatabase(path);

        Assert.True(database.Initialize(), "损坏库应被隔离后重建");
        Assert.True(database.Available);
        Assert.NotEmpty(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.corrupt-*"));
        Assert.True(database.Insert(Entry("PUSH", "CF100A_Demo", "IMPL")));
        Assert.Equal(1, database.Count());
    }

    [Fact]
    public void Initialize_NewerUserVersion_DisablesWritesWithoutQuarantine()
    {
        var path = Path.Combine(_root, "db", "mbd.db");
        var first = new AuditDatabase(path);
        Assert.True(first.Initialize());

        using (var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = path }.ToString()))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version = 99;";
            command.ExecuteNonQuery();
        }

        var second = new AuditDatabase(path);
        Assert.False(second.Initialize());
        Assert.False(second.Available);
        Assert.False(second.Insert(Entry("PUSH", "CF100A_Demo", "IMPL")));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.corrupt-*"));
    }

    [Fact]
    public void Query_FiltersByActionComponentResultAndPaginates_NewestFirst()
    {
        var database = NewDatabase();
        var oldest = Old(1);
        database.Insert(Entry("COMMIT", "C1", "IMPL", ts: oldest));
        database.Insert(Entry("PUSH", "C1", "IMPL", result: "FAILED", errorCode: "E_PUSH_REJECTED"));
        database.Insert(Entry("FETCH", "C2", "DESIGN"));

        var all = database.Query(new OperationLogFilter());
        Assert.NotNull(all);
        Assert.Equal(3, all!.Total);
        Assert.Equal("FETCH", all.Rows[0].Action); // id 倒序
        Assert.Equal("COMMIT", all.Rows[^1].Action);

        var byAction = database.Query(new OperationLogFilter(Action: "COMMIT"));
        Assert.NotNull(byAction);
        Assert.Equal(1, byAction!.Total);
        Assert.Equal(oldest, byAction.Rows[0].Ts);

        Assert.Equal(2, database.Query(new OperationLogFilter(ComponentId: "C1"))!.Total);
        Assert.Equal(1, database.Query(new OperationLogFilter(Result: "FAILED"))!.Total);

        var page = database.Query(new OperationLogFilter(Limit: 2, Offset: 0));
        Assert.Equal(3, page!.Total);
        Assert.Equal(2, page.Rows.Count);

        var lastPage = database.Query(new OperationLogFilter(Limit: 2, Offset: 2));
        Assert.Single(lastPage!.Rows);
    }

    [Fact]
    public void Query_FiltersByTsRange()
    {
        var database = NewDatabase();
        database.Insert(Entry("COMMIT", "C1", "IMPL", ts: "2026-01-01T00:00:00.0000000Z"));
        database.Insert(Entry("COMMIT", "C1", "IMPL", ts: "2026-02-01T00:00:00.0000000Z"));
        database.Insert(Entry("COMMIT", "C1", "IMPL", ts: "2026-03-01T00:00:00.0000000Z"));

        var range = database.Query(new OperationLogFilter(
            FromUtc: "2026-01-15T00:00:00.0000000Z",
            ToUtc: "2026-02-15T00:00:00.0000000Z"));
        Assert.NotNull(range);
        Assert.Equal(1, range!.Total);
        Assert.Equal("2026-02-01T00:00:00.0000000Z", range.Rows[0].Ts);
    }

    [Fact]
    public void DeleteOlderThan_RemovesOnlyExpiredRows()
    {
        var database = NewDatabase();
        var old91 = Old(91);
        var old89 = Old(89);
        database.Insert(Entry("COMMIT", "C1", "IMPL", ts: old91));
        database.Insert(Entry("COMMIT", "C1", "IMPL", ts: old89));

        var removed = database.DeleteOlderThan(Old(90));

        Assert.Equal(1, removed);
        Assert.Equal(1, database.Count());
        Assert.Equal(old89, database.Query(new OperationLogFilter())!.Rows[0].Ts);
    }

    [Fact]
    public void OperationAuditService_DualWritesJsonlAndSqlite_WithM2aCompatibleShape()
    {
        var config = TestAudit.NewConfig();
        var service = new OperationAuditService(config);

        service.Record(
            action: "COMMIT",
            componentId: "CF100A_Demo",
            repoType: "IMPL",
            state: "DIRTY",
            preHead: "1111111111111111111111111111111111111111",
            postHead: "2222222222222222222222222222222222222222",
            ok: true,
            errorCode: null,
            durationMs: 42,
            actor: "tester",
            requestId: "req-1");

        // JSONL（M2a 兼容行：ts/action/componentId/repoType/state/preHead/postHead/ok/errorCode/
        // durationMs/actor/requestId + M2b result；单仓行无 jobId/jobItemId/backupRef 可选键）。
        var lines = File.ReadAllLines(service.FilePath);
        var line = Assert.Single(lines);
        using (var document = JsonDocument.Parse(line))
        {
            var root = document.RootElement;
            Assert.Equal("COMMIT", root.GetProperty("action").GetString());
            Assert.Equal("CF100A_Demo", root.GetProperty("componentId").GetString());
            Assert.Equal("IMPL", root.GetProperty("repoType").GetString());
            Assert.Equal("DIRTY", root.GetProperty("state").GetString());
            Assert.True(root.GetProperty("ok").GetBoolean());
            Assert.Equal("OK", root.GetProperty("result").GetString());
            Assert.True(root.GetProperty("ts").GetString()!.EndsWith('Z'));
            Assert.False(root.TryGetProperty("jobId", out _));
            Assert.False(root.TryGetProperty("jobItemId", out _));
            Assert.False(root.TryGetProperty("backupRef", out _));
        }

        // SQLite 同 1 行（§8.1 冻结列；JSON 投影出 componentId/repoType/head）。
        Assert.True(service.DatabaseAvailable);
        Assert.Equal(1, service.Database!.Count());
        var page = service.GetOperationLog(new GetOperationLogParams());
        Assert.True(page.Ok, page.Error?.Code);
        var data = Assert.IsType<OperationLogPageDto>(page.Data);
        var item = Assert.Single(data.Items);
        Assert.Equal(1, data.Total);
        Assert.Equal("req-1", item.RequestId);
        Assert.Equal("CF100A_Demo", item.ComponentId);
        Assert.Equal("IMPL", item.RepoType);
        Assert.Equal("OK", item.Result);
        Assert.NotNull(item.Target);
        Assert.Equal("CF100A_Demo", item.Target!.Value.GetProperty("componentId").GetString());
        Assert.Equal("IMPL", item.Target!.Value.GetProperty("repoType").GetString());
        Assert.Equal("1111111111111111111111111111111111111111", item.Before!.Value.GetProperty("head").GetString());
        Assert.Equal("DIRTY", item.Before!.Value.GetProperty("state").GetString());
        Assert.Equal("2222222222222222222222222222222222222222", item.After!.Value.GetProperty("head").GetString());

        // 审计不含 token/Authorization（FR-SAFE-03）。
        Assert.DoesNotContain("Authorization", File.ReadAllText(service.FilePath), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetOperationLog_FiltersByActionComponentResultAndRejectsInvalidParams()
    {
        var service = new OperationAuditService(TestAudit.NewConfig());
        service.Record(action: "COMMIT", componentId: "C1", repoType: "IMPL", state: "DIRTY", preHead: null,
            postHead: null, ok: true, errorCode: null, durationMs: 1, requestId: "r1");
        service.Record(action: "PUSH", componentId: "C1", repoType: "IMPL", state: "AHEAD", preHead: null,
            postHead: null, ok: false, errorCode: BridgeErrorCodes.PushRejected, durationMs: 2, requestId: "r2");
        service.Record(action: "FETCH", componentId: "C2", repoType: "DESIGN", state: "SYNCED", preHead: null,
            postHead: null, ok: true, errorCode: null, durationMs: 3, requestId: "r3");

        var page = Assert.IsType<OperationLogPageDto>(
            service.GetOperationLog(new GetOperationLogParams(Action: "commit")).Data);
        Assert.Equal(1, page.Total);
        Assert.Equal("COMMIT", page.Items[0].Action);

        Assert.Equal(2, Assert.IsType<OperationLogPageDto>(
            service.GetOperationLog(new GetOperationLogParams(ComponentId: "C1")).Data).Total);
        var failed = Assert.IsType<OperationLogPageDto>(
            service.GetOperationLog(new GetOperationLogParams(Result: "FAILED")).Data);
        Assert.Single(failed.Items);
        Assert.Equal(BridgeErrorCodes.PushRejected, failed.Items[0].ErrorCode);

        var limited = Assert.IsType<OperationLogPageDto>(
            service.GetOperationLog(new GetOperationLogParams(Limit: 2, Offset: 1)).Data);
        Assert.Equal(3, limited.Total);
        Assert.Equal(2, limited.Items.Count);

        Assert.Equal(
            BridgeErrorCodes.InvalidArgument,
            service.GetOperationLog(new GetOperationLogParams(Limit: 0)).Error!.Code);
        Assert.Equal(
            BridgeErrorCodes.InvalidArgument,
            service.GetOperationLog(new GetOperationLogParams(From: "not-a-date")).Error!.Code);
        Assert.Equal(
            BridgeErrorCodes.InvalidArgument,
            service.GetOperationLog(new GetOperationLogParams(From: "2026-02-01", To: "2026-01-01")).Error!.Code);
    }

    [Fact]
    public void ExportOperationLog_WritesCsvAndJsonUnderExportsRoot_WithoutSecrets()
    {
        var config = TestAudit.NewConfig();
        var service = new OperationAuditService(config);
        service.Record(action: "COMMIT", componentId: "CF100A_Demo", repoType: "IMPL", state: "DIRTY",
            preHead: "1111111111111111111111111111111111111111", postHead: "2222222222222222222222222222222222222222",
            ok: true, errorCode: null, durationMs: 5, requestId: "req-csv");
        service.Record(action: "PUSH", componentId: "CF100A_Demo", repoType: "IMPL", state: "AHEAD",
            preHead: null, postHead: null, ok: false, errorCode: BridgeErrorCodes.PushRejected,
            durationMs: 6, requestId: "req-json");

        var csv = service.ExportOperationLog(new ExportOperationLogParams(Format: "csv"));
        Assert.True(csv.Ok, csv.Error?.Code);
        var csvData = Assert.IsType<OperationLogExportDto>(csv.Data);
        Assert.Equal(2, csvData.Count);
        Assert.StartsWith(Path.GetFullPath(config.ExportsRoot!), csvData.Path, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(csvData.Path));
        var csvText = File.ReadAllText(csvData.Path);
        Assert.Contains("target_json", csvText, StringComparison.Ordinal);
        Assert.Contains("before_json", csvText, StringComparison.Ordinal);
        Assert.Contains("req-csv", csvText, StringComparison.Ordinal);
        Assert.DoesNotContain("Authorization", csvText, StringComparison.OrdinalIgnoreCase);

        var json = service.ExportOperationLog(new ExportOperationLogParams(Format: "JSON"));
        Assert.True(json.Ok, json.Error?.Code);
        var jsonData = Assert.IsType<OperationLogExportDto>(json.Data);
        Assert.Equal(2, jsonData.Count);
        Assert.EndsWith(".json", jsonData.Path, StringComparison.OrdinalIgnoreCase);
        using var document = JsonDocument.Parse(File.ReadAllText(jsonData.Path));
        Assert.Equal(2, document.RootElement.GetProperty("count").GetInt32());
        Assert.Equal(2, document.RootElement.GetProperty("items").GetArrayLength());
        Assert.DoesNotContain(
            "Authorization",
            File.ReadAllText(jsonData.Path),
            StringComparison.OrdinalIgnoreCase);

        Assert.Equal(
            BridgeErrorCodes.InvalidArgument,
            service.ExportOperationLog(new ExportOperationLogParams(Format: "xml")).Error!.Code);
    }

    [Fact]
    public void CleanupExpired_RemovesSqliteAndJsonlRowsOlderThan90Days()
    {
        var config = TestAudit.NewConfig();
        var service = new OperationAuditService(config);

        service.Database!.Insert(Entry("COMMIT", "CF100A_Demo", "IMPL", ts: Old(91)));
        service.Record(action: "PUSH", componentId: "CF100A_Demo", repoType: "IMPL", state: "AHEAD",
            preHead: null, postHead: null, ok: true, errorCode: null, durationMs: 1, requestId: "recent");

        var oldLine = JsonSerializer.Serialize(new
        {
            ts = Old(91),
            action = "COMMIT",
            componentId = "CF100A_Demo",
            repoType = "IMPL",
            state = "DIRTY",
            preHead = (string?)null,
            postHead = (string?)null,
            ok = true,
            errorCode = (string?)null,
            durationMs = 1,
            actor = "tester",
            requestId = "old-jsonl",
            result = "OK"
        });
        File.AppendAllText(service.FilePath, oldLine + Environment.NewLine);

        var (databaseRows, jsonlRows) = service.CleanupExpired();

        Assert.Equal(1, databaseRows);
        Assert.Equal(1, jsonlRows);
        Assert.Equal(1, service.Database!.Count());
        var remaining = service.GetOperationLog(new GetOperationLogParams());
        Assert.Equal("recent", Assert.IsType<OperationLogPageDto>(remaining.Data).Items[0].RequestId);
        var lines = File.ReadAllLines(service.FilePath);
        Assert.Single(lines);
        Assert.DoesNotContain("old-jsonl", lines[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Restart_NewServiceInstance_StillQueriesPersistedRows()
    {
        var config = TestAudit.NewConfig();
        var first = new OperationAuditService(config);
        first.Record(action: "COMMIT", componentId: "CF100A_Demo", repoType: "IMPL", state: "DIRTY",
            preHead: null, postHead: null, ok: true, errorCode: null, durationMs: 1, requestId: "persisted");

        var second = new OperationAuditService(config);
        var page = second.GetOperationLog(new GetOperationLogParams());
        Assert.True(page.Ok, page.Error?.Code);
        Assert.Equal("persisted", Assert.IsType<OperationLogPageDto>(page.Data).Items[0].RequestId);
    }

    [Fact]
    public void WithoutDatabase_SqliteApisDegradeToInternalButJsonlStillWrites()
    {
        var service = new OperationAuditService(Path.Combine(_root, "jsonl-only"));
        service.Record(action: "PUSH", componentId: "CF100A_Demo", repoType: "IMPL", state: "AHEAD",
            preHead: null, postHead: null, ok: true, errorCode: null, durationMs: 1, requestId: "jsonl");

        Assert.False(service.DatabaseAvailable);
        Assert.Single(File.ReadAllLines(service.FilePath));
        Assert.Equal(
            BridgeErrorCodes.Internal,
            service.GetOperationLog(new GetOperationLogParams()).Error!.Code);
        Assert.Equal(
            BridgeErrorCodes.Internal,
            service.ExportOperationLog(new ExportOperationLogParams(Format: "csv")).Error!.Code);
    }

    [Fact]
    public void AppConfig_RetentionDefaults_Are90And30AndClamped()
    {
        var defaults = new AppConfig();
        Assert.Equal(90, defaults.AuditRetentionDays);
        Assert.Equal(30, defaults.BackupRetentionDays);
        Assert.Equal(90, defaults.EffectiveAuditRetentionDays);
        Assert.Equal(30, defaults.EffectiveBackupRetentionDays);

        // 0/负数回落到默认（90），越界夹取到范围内。
        Assert.Equal(90, new AppConfig { AuditRetentionDays = 0 }.EffectiveAuditRetentionDays);
        Assert.Equal(3650, new AppConfig { AuditRetentionDays = 99999 }.EffectiveAuditRetentionDays);
        Assert.Equal(7, new AppConfig { BackupRetentionDays = 1 }.EffectiveBackupRetentionDays);
        Assert.Equal(90, new AppConfig { BackupRetentionDays = 999 }.EffectiveBackupRetentionDays);
    }

    private AuditDatabase NewDatabase()
    {
        var database = new AuditDatabase(Path.Combine(_root, Guid.NewGuid().ToString("N"), "mbd.db"));
        Assert.True(database.Initialize());
        return database;
    }

    private static OperationLogEntry Entry(
        string action,
        string componentId,
        string repoType,
        string? ts = null,
        string result = "OK",
        string? errorCode = null) => new(
        Ts: ts ?? DateTime.UtcNow.ToString("O"),
        Actor: "tester",
        Action: action,
        ComponentId: componentId,
        RepoType: repoType,
        TargetJson: "{\"componentId\":\"" + componentId + "\",\"repoType\":\"" + repoType + "\"}",
        BeforeJson: null,
        AfterJson: null,
        Result: result,
        ErrorCode: errorCode,
        RequestId: "req",
        JobId: null,
        JobItemId: null,
        BackupRef: null);

    private static string Old(int days) => DateTime.UtcNow
        .AddDays(-days)
        .ToString("O", System.Globalization.CultureInfo.InvariantCulture);

    private static object? Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    public void Dispose()
    {
        try
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch
        {
            // 临时目录清理失败不影响测试结论。
        }
    }
}
