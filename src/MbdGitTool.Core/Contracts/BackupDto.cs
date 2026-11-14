namespace MbdGitTool.Core.Contracts;

/// <summary>
/// M2b-2b 备份 ref 格式标识（ADR D-026 P1-2 / AC-OP-30）：
/// <para><see cref="Legacy"/>：M2b-2a 及更早的 <c>refs/backup/sync/&lt;ts&gt;/&lt;TYPE&gt;</c>（无分支身份）；</para>
/// <para><see cref="Branch"/>：M2b-2b 的 <c>refs/backup/sync/&lt;ts&gt;/&lt;branch&gt;/&lt;TYPE&gt;</c>
/// （<c>branch</c> 为安全编码后的分支名；无分支身份时用 <c>DETACHED</c> 占位）。</para>
/// </summary>
public static class BackupRefKinds
{
    public const string Sync = "SYNC";
    public const string Safety = "SAFETY";
}

public static class BackupRefFormats
{
    /// <summary>旧格式（无分支身份，解析出的 <see cref="BackupRefDto.Branch"/> = null）。</summary>
    public const string Legacy = "LEGACY";

    /// <summary>新格式（携带分支身份；<see cref="BackupRefDto.Branch"/> 非 null 时为来源分支）。</summary>
    public const string Branch = "BRANCH";
}

/// <summary>
/// M2b-2 备份 ref 契约（SRS §9.7 FR-OP-11 / D-017 / AC-OP-30）。
/// <para>备份 ref 由受控 SYNC 创建：<c>refs/backup/sync/&lt;UTC yyyyMMddHHmmss&gt;/&lt;branch&gt;/&lt;REPO_TYPE&gt;</c>，
/// 保留期默认 30 天（<c>backup.retentionDays</c>，SRS 附录 D 范围 7–90）。</para>
/// <para>M2b-2b 追加 <see cref="Branch"/> / <see cref="Format"/>（仅追加、向后兼容；旧 ref 解析为
/// <c>Branch=null</c> + <c>Format=LEGACY</c>）。</para>
/// </summary>
public sealed record BackupRefDto(
    string Ref,
    string Sha,
    string CreatedAt,
    string RepoType,
    string? Branch = null,
    string Format = BackupRefFormats.Legacy,
    string Kind = "SYNC");

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
