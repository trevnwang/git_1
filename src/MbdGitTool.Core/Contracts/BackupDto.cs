namespace MbdGitTool.Core.Contracts;

/// <summary>
/// M2b-2 备份 ref 契约（SRS §9.7 FR-OP-11 / D-017 / AC-OP-30）。
/// <para>备份 ref 由受控 SYNC 创建：<c>refs/backup/sync/&lt;UTC yyyyMMddHHmmss&gt;/&lt;REPO_TYPE&gt;</c>，
/// 保留期默认 30 天（<c>backup.retentionDays</c>，SRS 附录 D 范围 7–90）。</para>
/// </summary>
public sealed record BackupRefDto(
    string Ref,
    string Sha,
    string CreatedAt,
    string RepoType);

/// <summary><c>getBackupRefs</c> 响应：<c>{ refs: BackupRefDto[] }</c>（按创建时间/ref 倒序，新备份在前）。</summary>
public sealed record BackupRefsDto(IReadOnlyList<BackupRefDto> Refs);

/// <summary>
/// <c>restoreBackupRef</c> 响应：<c>{ restored, ref, safetyRef, preHead, postHead, message }</c>。
/// <para><c>safetyRef</c> = 恢复前把当前 HEAD 备份到的 <c>refs/backup/restore/&lt;ts&gt;/&lt;TYPE&gt;</c>
/// （空仓/无 HEAD 时为 null）；失败可重试（恢复前不删除任何本地数据，脏工作区默认阻断）。</para>
/// </summary>
public sealed record RestoreBackupRefResultDto(
    bool Restored,
    string Ref,
    string? SafetyRef,
    string? PreHead,
    string? PostHead,
    string Message);
