using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using MbdGitTool.Core.Contracts;

namespace MbdGitTool.Host.Services;

/// <summary>DELETE_PENDING 文件记录（AC-OP-23）：列出/取消/30 天 GC；只动 `.trash/.pending`，不删仓库。</summary>
public sealed class DeletePendingService
{
    private readonly AppConfig _config;

    public DeletePendingService(AppConfig config) => _config = config ?? throw new ArgumentNullException(nameof(config));

    public sealed record PendingRecord(string ComponentId, string RepoType, string RepoName, string LocalPath, string ErrorCode, string CreatedAt, string FilePath);

    private string? PendingDirectory =>
        string.IsNullOrWhiteSpace(_config.WorkspaceRoot)
            ? null
            : Path.Combine(_config.WorkspaceRoot!, BatchOperationService.TrashDirectoryName, ".pending");

    public IReadOnlyList<PendingRecord> List()
    {
        var dir = PendingDirectory;
        if (dir is null || !Directory.Exists(dir)) return Array.Empty<PendingRecord>();
        var rows = new List<PendingRecord>();
        foreach (var file in Directory.EnumerateFiles(dir, "*.json"))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file));
                var root = doc.RootElement;
                rows.Add(new PendingRecord(
                    root.TryGetProperty("componentId", out var c) ? c.GetString() ?? string.Empty : string.Empty,
                    root.TryGetProperty("repoType", out var r) ? r.GetString() ?? string.Empty : string.Empty,
                    root.TryGetProperty("repoName", out var n) ? n.GetString() ?? string.Empty : string.Empty,
                    root.TryGetProperty("localPath", out var p) ? p.GetString() ?? string.Empty : string.Empty,
                    root.TryGetProperty("errorCode", out var e) ? e.GetString() ?? string.Empty : string.Empty,
                    root.TryGetProperty("createdAt", out var t) ? t.GetString() ?? string.Empty : string.Empty,
                    file));
            }
            catch (IOException) { /* 忽略不可读记录 */ }
            catch (JsonException) { /* 忽略损坏记录 */ }
        }
        return rows;
    }

    public int Cancel(string componentId, string? repoType)
    {
        var rows = List().Where(row => string.Equals(row.ComponentId, componentId, StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrWhiteSpace(repoType) || string.Equals(row.RepoType, repoType, StringComparison.OrdinalIgnoreCase))).ToList();
        var removed = 0;
        foreach (var row in rows)
        {
            try { File.Delete(row.FilePath); removed++; }
            catch (IOException) { /* 保留可重试 */ }
            catch (UnauthorizedAccessException) { /* 保留 */ }
        }
        return removed;
    }

    public int CleanupExpired(int retentionDays = 30)
    {
        var dir = PendingDirectory;
        if (dir is null || !Directory.Exists(dir)) return 0;
        var cutoff = DateTime.UtcNow.AddDays(-Math.Max(1, retentionDays));
        var removed = 0;
        foreach (var file in Directory.EnumerateFiles(dir, "*.json"))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(file) > cutoff) continue;
                File.Delete(file);
                removed++;
            }
            catch (IOException) { /* ignore */ }
            catch (UnauthorizedAccessException) { /* ignore */ }
        }
        return removed;
    }
}
