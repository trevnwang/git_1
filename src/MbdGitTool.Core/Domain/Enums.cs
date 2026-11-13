namespace MbdGitTool.Core.Domain;

/// <summary>单仓主状态（SRS §7.2，14 态，顺序即优先级 0 最高）。</summary>
public enum RepoState
{
    Unknown = 0,
    Invalid = 1,
    Missing = 2,
    Empty = 3,
    Unmerged = 4,
    InProgress = 5,
    DetachedTag = 6,
    DetachedCommit = 7,
    Diverged = 8,
    Behind = 9,
    Ahead = 10,
    NoUpstream = 11,
    Dirty = 12,
    Synced = 13
}

/// <summary>NO_UPSTREAM 子型（SRS §7.2 / 6.1）。</summary>
public enum NoUpstreamSubType
{
    None = 0,
    LocalOnly = 1,
    UnsetButExists = 2,
    UpstreamGone = 3
}

/// <summary>生命周期类型（SRS §5/§6）。</summary>
public enum RepoType
{
    Design = 0,
    Impl = 1,
    Mil = 2,
    Sil = 3,
    Other = 4
}

/// <summary>正交标记（SRS §7.3，不参与主状态优先级）。</summary>
[Flags]
public enum RepoFlags
{
    None = 0,
    DirtyTracked = 1 << 0,
    Untracked = 1 << 1,
    Stashed = 1 << 2,
    Shallow = 1 << 3,
    LfsMissing = 1 << 4,
    ReparsePoint = 1 << 5,
    Stale = 1 << 6,
    Busy = 1 << 7,
    RemoteGone = 1 << 8,
    TagMoved = 1 << 9,
    Submodule = 1 << 10
}

/// <summary>组件聚合态（SRS §7.4，15 态）。</summary>
public enum ComponentState
{
    OpPartialFailed = 0,
    BindingError = 1,
    Unknown = 2,
    Missing = 3,
    BlockedLocal = 4,
    Empty = 5,
    TagDrift = 6,
    Diverged = 7,
    Behind = 8,
    Ahead = 9,
    Detached = 10,
    MixedBranch = 11,
    NewBranch = 12,
    Dirty = 13,
    Synced = 14
}

/// <summary>BINDING_ERROR 的成因（决定 severity：INVALID/AMBIGUOUS=1，仅缺仓=12）。</summary>
public enum BindingCause
{
    None = 0,
    InvalidOrUnbound = 1,
    Incomplete = 2
}
