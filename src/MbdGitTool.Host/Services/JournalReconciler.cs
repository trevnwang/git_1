using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MbdGitTool.Core.Contracts;
using MbdGitTool.Core.Domain;

namespace MbdGitTool.Host.Services;

/// <summary>
/// 单条 journal 条目的 reconcile 结果（供 HostCommandRouter 映射到任务中心/诊断）。
/// <para><see cref="Status"/> ∈ <see cref="JournalReconciler.NotApplied"/>（HEAD 与写前一致，可安全重试）/
/// <see cref="JournalReconciler.UnknownApplied"/>（HEAD 已变化，不猜测成功，提示人工核对）。</para>
/// </summary>
public sealed record JournalReconcileResult(
    string EntryId,
    string Action,
    string? ComponentId,
    string? RepoType,
    string? JobId,
    string? JobItemId,
    string Status,
    string? PreHead,
    string? CurrentHead);

/// <summary>reconcile 汇总：逐条结果 + 两个分支计数（诊断/测试断言用）。</summary>
public sealed record JournalReconcileReport(IReadOnlyList<JournalReconcileResult> Items)
{
    /// <summary>STARTED 未收口条目总数。</summary>
    public int Total => Items.Count;

    /// <summary>HEAD 与写前一致（未应用，可安全重试）的条目数。</summary>
    public int NotApplied => Count(JournalReconciler.NotApplied);

    /// <summary>HEAD 已变化（无法证明未应用）的条目数。</summary>
    public int UnknownApplied => Count(JournalReconciler.UnknownApplied);

    private int Count(string status)
    {
        var count = 0;
        foreach (var item in Items)
        {
            if (string.Equals(item.Status, status, StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }
}

/// <summary>
/// 启动 reconcile（FR-SAFE-04 / NFR-REL-03 / ADR D-026 M2b-2b）：
/// <list type="number">
/// <item>扫描 <see cref="OperationJournal.ReadUnresolved"/>（STARTED 无 DONE/FAILED，说明进程在 git 写期间被杀）；</item>
/// <item>读取该仓当前 HEAD：<c>HEAD == preHead</c> → <see cref="NotApplied"/>（可安全重试）；
/// <c>HEAD != preHead</c>（含仓已不存在）→ <see cref="UnknownApplied"/>（不猜成功，提示核对）；</item>
/// <item>每条写 1 行审计 <c>action=RECONCILE</c>（含 jobId/jobItemId 关联，可在任务中心审计口径下检索），
/// 并给 journal 追加 RECONCILED 防止下次重复判定；</item>
/// <item><b>绝不修改仓库</b>（只读 <c>git rev-parse HEAD</c>）。</item>
/// </list>
/// </summary>
public sealed class JournalReconciler
{
    /// <summary>reconcile 审计动作名（operation_log.action 扩展值）。</summary>
    public const string ReconcileAction = "RECONCILE";

    /// <summary>HEAD 与写前一致：写操作未应用（可安全重试）。</summary>
    public const string NotApplied = "NOT_APPLIED";

    /// <summary>HEAD 已变化：无法证明未应用（不猜测成功，需人工核对）。</summary>
    public const string UnknownApplied = "UNKNOWN_APPLIED";

    private readonly OperationJournal _journal;
    private readonly OperationAuditService _audit;
    private readonly GitCli _git;
    private readonly Func<SnapshotIndex>? _indexProvider;
    private readonly Action<JournalReconcileResult>? _observer;

    /// <param name="journal">写前 journal（读取 STARTED 条目并追加 RECONCILED）。</param>
    /// <param name="audit">审计（每条 reconcile 写 1 行 action=RECONCILE；不写回退队列）。</param>
    /// <param name="git">git 封装（只读 rev-parse；可空 = 默认） 。</param>
    /// <param name="indexProvider">当前快照索引（journal 条目缺 localPath 时按 componentId+repoType 解析路径）。</param>
    /// <param name="observer">reconcile 结果观察者（HostCommandRouter 用于回写任务中心 item 文案）。</param>
    public JournalReconciler(
        OperationJournal journal,
        OperationAuditService audit,
        GitCli? git = null,
        Func<SnapshotIndex>? indexProvider = null,
        Action<JournalReconcileResult>? observer = null)
    {
        _journal = journal ?? throw new ArgumentNullException(nameof(journal));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        _git = git ?? new GitCli();
        _indexProvider = indexProvider;
        _observer = observer;
    }

    /// <summary>执行一次 reconcile（不抛异常：单条失败只告警并继续；返回逐条判定）。</summary>
    public JournalReconcileReport Reconcile()
    {
        IReadOnlyList<JournalEntry> unresolved;
        try
        {
            unresolved = _journal.ReadUnresolved();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            HostLog.Warn($"启动 reconcile 读取 journal 失败（{ex.GetType().Name}）");
            return new JournalReconcileReport(Array.Empty<JournalReconcileResult>());
        }

        if (unresolved.Count == 0)
        {
            return new JournalReconcileReport(Array.Empty<JournalReconcileResult>());
        }

        var results = new List<JournalReconcileResult>(unresolved.Count);
        foreach (var entry in unresolved)
        {
            JournalReconcileResult result;
            try
            {
                result = ReconcileEntry(entry);
            }
            catch (Exception ex) when (ex is IOException
                or UnauthorizedAccessException
                or ArgumentException
                or InvalidOperationException
                or NotSupportedException)
            {
                // 单条失败：不猜测成功，也不阻断其它条目；journal 保持 STARTED 待下次启动重试。
                HostLog.Warn($"启动 reconcile 单条失败（{ex.GetType().Name}）：{entry.Id}");
                continue;
            }

            results.Add(result);
            try
            {
                _observer?.Invoke(result);
            }
            catch (Exception ex)
            {
                HostLog.Warn($"reconcile 结果回调失败（{ex.GetType().Name}）");
            }
        }

        if (results.Count > 0)
        {
            HostLog.Warn(
                $"启动 reconcile 完成：{results.Count} 条未完成 journal（NOT_APPLIED="
                + results.FindAll(item => item.Status == NotApplied).Count
                + "，UNKNOWN_APPLIED="
                + results.FindAll(item => item.Status == UnknownApplied).Count
                + "）；不猜测成功，未修改任何仓库");
        }

        return new JournalReconcileReport(results);
    }

    private JournalReconcileResult ReconcileEntry(JournalEntry entry)
    {
        var localPath = ResolveLocalPath(entry);
        var currentHead = localPath is null ? null : ReadCurrentHead(localPath);

        // 只按“当前 HEAD 是否仍等于写前 HEAD”判定；两者都为 null 也视为未应用（例如 CLONE 未开始）。
        var unchanged = string.Equals(
            currentHead ?? string.Empty,
            entry.PreHead ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
        var status = unchanged ? NotApplied : UnknownApplied;
        var errorCode = unchanged ? null : BridgeErrorCodes.ReconcileUnknown;

        _audit.Record(
            action: ReconcileAction,
            componentId: entry.ComponentId,
            repoType: entry.RepoType,
            state: "UNKNOWN",
            preHead: entry.PreHead,
            postHead: currentHead,
            ok: unchanged,
            errorCode: errorCode,
            durationMs: 0,
            actor: "journal-reconciler",
            requestId: entry.Id,
            jobId: entry.JobId,
            jobItemId: entry.JobItemId,
            result: status);

        _journal.Reconcile(entry, status, currentHead, errorCode);

        return new JournalReconcileResult(
            EntryId: entry.Id,
            Action: entry.Action,
            ComponentId: entry.ComponentId,
            RepoType: entry.RepoType,
            JobId: entry.JobId,
            JobItemId: entry.JobItemId,
            Status: status,
            PreHead: entry.PreHead,
            CurrentHead: currentHead);
    }

    /// <summary>journal 记录的目标路径优先；缺失时按 componentId+repoType 从当前快照解析（只读）。</summary>
    private string? ResolveLocalPath(JournalEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.LocalPath))
        {
            return entry.LocalPath;
        }

        if (_indexProvider is null
            || string.IsNullOrWhiteSpace(entry.ComponentId)
            || string.IsNullOrWhiteSpace(entry.RepoType))
        {
            return null;
        }

        try
        {
            var index = _indexProvider();
            if (index.TryGetRepo(entry.ComponentId, entry.RepoType, out var repo)
                && repo.Repo.IsLocal
                && !string.IsNullOrWhiteSpace(repo.Repo.LocalPath))
            {
                return repo.Repo.LocalPath;
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            HostLog.Warn($"reconcile 解析仓库路径失败（{ex.GetType().Name}）");
        }

        return null;
    }

    /// <summary>只读 rev-parse HEAD（失败返回 null；不写任何仓库/ref）。</summary>
    private string? ReadCurrentHead(string localPath)
    {
        try
        {
            if (!Directory.Exists(localPath))
            {
                // 目录不存在（DELETE 成功/CLONE 未开始等）：按“无 HEAD”处理。
                return null;
            }

            var result = _git
                .RunAsync(
                    localPath,
                    new[] { "rev-parse", "HEAD" },
                    System.Threading.CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            if (!result.Success)
            {
                return null;
            }

            var head = result.TrimmedStdout;
            return head.Length is > 0 and <= 64 ? head : null;
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or InvalidOperationException
            or NotSupportedException)
        {
            HostLog.Warn($"reconcile 读取 HEAD 失败（{ex.GetType().Name}）");
            return null;
        }
    }
}
