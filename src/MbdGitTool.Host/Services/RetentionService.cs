using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using MbdGitTool.Core.Contracts;
using MbdGitTool.Core.Security;

namespace MbdGitTool.Host.Services;

/// <summary>
/// M2b-2 本地保留清理（启动 + <c>cleanupRetention</c> 命令；SRS FR-OP-06/09/11、D-017/H-05）：
/// <para>① <c>&lt;workspace&gt;\.trash\&lt;stamp&gt;</c> 超过 <see cref="TrashRetentionDays"/> 天（D-017 统一 30 天）；</para>
/// <para>② 各本地仓 <c>refs/backup/sync/*</c> 超过 <see cref="AppConfig.EffectiveBackupRetentionDays"/>
/// （默认 30，附录 D 范围 7–90；只删本地 ref，不触碰远端）；</para>
/// <para>③ <c>operation_log</c> SQLite 行 + JSONL 行超过 <see cref="AppConfig.EffectiveAuditRetentionDays"/>
/// （默认 90 天，FR-OP-09/FR-OPS-02）。</para>
/// <para>清理结束后写 1 行审计（action=<c>CLEANUP_RETENTION</c>）并记日志；绝不删除保留期内数据、绝不写远端。</para>
/// </summary>
public sealed class RetentionService
{
    /// <summary><c>.trash</c> 保留天数（D-017 统一 30 天，固定策略）。</summary>
    public const int TrashRetentionDays = 30;

    /// <summary>清理审计动作名（operation_log.action 的本地管理扩展值）。</summary>
    public const string CleanupAction = "CLEANUP_RETENTION";

    private readonly AppConfig _config;
    private readonly OperationAuditService _audit;
    private readonly BackupRefService _backup;

    public RetentionService(
        AppConfig config,
        OperationAuditService audit,
        BackupRefService? backup = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        _backup = backup ?? new BackupRefService(config);
    }

    /// <summary>执行一次保留清理（启动后台任务与 <c>cleanupRetention</c> 命令共用）。</summary>
    public async Task<RetentionCleanupDto> RunAsync(
        CleanupRetentionParams? parameters = null,
        bool writeAudit = true,
        CancellationToken cancellationToken = default)
    {
        _ = parameters;
        var stopwatch = Stopwatch.StartNew();

        var trashRemoved = CleanupTrash();
        var deletePendingRemoved = new DeletePendingService(_config).CleanupExpired(TrashRetentionDays);
        var backupRefsRemoved = 0;
        try
        {
            backupRefsRemoved = await _backup
                .CleanupExpiredAsync(_config.EffectiveBackupRetentionDays, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is System.IO.IOException
            or UnauthorizedAccessException
            or ArgumentException
            or InvalidOperationException)
        {
            HostLog.Warn($"备份 ref 保留清理失败（{ex.GetType().Name}）");
        }

        var (auditRowsRemoved, auditJsonlRowsRemoved) = _audit.CleanupExpired();
        stopwatch.Stop();

        var result = new RetentionCleanupDto(
            TrashRemoved: trashRemoved,
            BackupRefsRemoved: backupRefsRemoved,
            AuditRowsRemoved: auditRowsRemoved,
            AuditJsonlRowsRemoved: auditJsonlRowsRemoved,
            WorkspaceAvailable: _config.HasWorkspace);

        HostLog.Info(
            $"保留清理完成：trash={trashRemoved}/pending={deletePendingRemoved}（{TrashRetentionDays} 天）backupRefs={backupRefsRemoved}"
            + $"（{_config.EffectiveBackupRetentionDays} 天）auditDb={auditRowsRemoved}/jsonl={auditJsonlRowsRemoved}"
            + $"（{_config.EffectiveAuditRetentionDays} 天）ms={stopwatch.ElapsedMilliseconds}");

        if (writeAudit)
        {
            _audit.Record(
                action: CleanupAction,
                componentId: null,
                repoType: null,
                state: "N/A",
                preHead: null,
                postHead: null,
                ok: true,
                errorCode: null,
                durationMs: stopwatch.ElapsedMilliseconds,
                actor: Environment.UserName,
                requestId: Guid.NewGuid().ToString("N"),
                result: "OK");
        }

        return result;
    }

    private int CleanupTrash()
    {
        if (!_config.HasWorkspace)
        {
            return 0;
        }

        try
        {
            var paths = new PathPolicy(_config.WorkspaceRoot!);
            var batch = new BatchOperationService(
                _config,
                new RepoActionService(config: _config),
                new GitCli(),
                paths);
            return batch.CleanupTrash(TrashRetentionDays);
        }
        catch (Exception ex) when (ex is ArgumentException
            or System.IO.IOException
            or UnauthorizedAccessException
            or InvalidOperationException)
        {
            HostLog.Warn($"清理 .trash 失败（{ex.GetType().Name}）");
            return 0;
        }
    }

    /// <summary>审计动作名（供测试/诊断断言）。</summary>
    public static string CleanupActionName => CleanupAction;
}
