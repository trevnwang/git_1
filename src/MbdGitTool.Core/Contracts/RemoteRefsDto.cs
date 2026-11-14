namespace MbdGitTool.Core.Contracts;

/// <summary>
/// getRemoteRefs 响应（M2 P0-7）：本地/远程分支 + Tag 快照，供 CHKOUT 选择器（含搜索）使用。
/// <para>数据来自 <c>git for-each-ref refs/heads refs/tags refs/remotes/origin</c>；只读、无网络。</para>
/// <para>总条数截断到 <c>500</c>（<see cref="Truncated"/> 标记是否被截断），结果缓存 30s。</para>
/// </summary>
public sealed record RemoteRefsDto(
    IReadOnlyList<RemoteRefDto> Refs,
    bool Truncated,
    int Total,
    string GeneratedAt);

/// <summary>单个 ref：<see cref="RefName"/> 为完整 ref 名（如 <c>refs/heads/main</c>）。</summary>
/// <param name="Kind">LOCAL_BRANCH / REMOTE_BRANCH / TAG。</param>
public sealed record RemoteRefDto(
    string RefName,
    string Sha,
    string ObjectType,
    string Kind);
