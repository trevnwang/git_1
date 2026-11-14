using System;
using System.Collections.Generic;
using System.Globalization;
using MbdGitTool.Core.Contracts;

namespace MbdGitTool.Host.Services;

/// <summary>AC-OP-11/12/14：TAG 收口写 baseline_snapshot（4 行/组件，同键覆盖）。</summary>
public sealed class BaselineSnapshotService
{
    private readonly AuditDatabase _database;

    public BaselineSnapshotService(AppConfig? config = null)
    {
        _database = new AuditDatabase(config?.DatabasePath);
        _database.Initialize();
    }

    public bool Upsert(string componentId, string tag, string repoType, string? commitSha, string? tagObjectSha, bool present)
    {
        if (string.IsNullOrWhiteSpace(componentId) || string.IsNullOrWhiteSpace(tag)) return false;
        return _database.UpsertBaselineSnapshot(new AuditDatabase.BaselineSnapshotRecord(
            ComponentId: componentId,
            Tag: tag,
            RepoType: repoType.ToUpperInvariant(),
            CommitSha: commitSha,
            TagObjectSha: tagObjectSha,
            TagPresent: present,
            CapturedAt: DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)));
    }

    public IReadOnlyList<AuditDatabase.BaselineSnapshotRecord> List(string componentId) =>
        _database.QueryBaselineSnapshots(componentId);
}
