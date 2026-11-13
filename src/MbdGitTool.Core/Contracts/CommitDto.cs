namespace MbdGitTool.Core.Contracts;

/// <summary>提交（getRepoDetail 最近 5 条 / getCommits 分页 / snapshot repo.lastCommit 共用）。</summary>
public sealed record CommitDto(
    string Sha,
    string ShortSha,
    string Message,
    string Author,
    string At,
    IReadOnlyList<string> Refs);
