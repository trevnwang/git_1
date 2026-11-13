using MbdGitTool.Core.Contracts;

namespace MbdGitTool.Core.Domain;

/// <summary>组件组装输入：一个已知仓库（本地发现的事实，或远程清单推导出的“缺失仓”事实）。</summary>
public sealed record RepoInput(
    string ComponentId,
    RepoType RepoType,
    RepoFacts Facts);

/// <summary>组件组装的上下文（远程清单是否就绪决定缺仓语义，SRS §7.4 / H-01）。</summary>
public sealed record ComponentAssemblyOptions
{
    /// <summary>
    /// 远程清单是否已就绪：true = 远程缺类型按 <c>BINDING_ERROR(INCOMPLETE)</c>；false = 本地缺失仓按 H-01 记 <c>UNKNOWN + incomplete</c>。
    /// </summary>
    public bool RemoteKnown { get; init; }

    /// <summary>远程存在解析歧义（同组件同类型多于 1 仓）的组件 ID 集合，按 <c>BINDING_ERROR(AMBIGUOUS)</c> 处理。</summary>
    public IReadOnlySet<string>? AmbiguousComponentIds { get; init; }

    public static ComponentAssemblyOptions Default { get; } = new();
}

/// <summary>
/// 组件组装（SRS §7.4 聚合全序 + severity_rank + mixedBranch + 分支/Tag 展示）。
/// <para>复用 <see cref="ComponentAggregation"/> 的 0–14 全序；本类负责：绑定判定、输入去重排序、DTO 映射。</para>
/// <para>空组/解析歧义/INVALID → BINDING_ERROR；远程缺仓 → BINDING_ERROR(INCOMPLETE, severity=12)。</para>
/// </summary>
public static class ComponentAssembler
{
    /// <summary>RepoType 展示顺序（DESIGN → IMPL → MIL → SIL，OTHER 最后）。</summary>
    private static readonly RepoType[] OrderedRepoTypes =
    {
        RepoType.Design,
        RepoType.Impl,
        RepoType.Mil,
        RepoType.Sil,
        RepoType.Other
    };

    /// <summary>仓库事实列表 → 组件 DTO 列表（按 severity_rank 升序、组件 ID 升序，D-020 看板默认排序）。</summary>
    public static IReadOnlyList<ComponentDto> Assemble(
        IEnumerable<RepoInput> repos,
        ComponentAssemblyOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(repos);
        options ??= ComponentAssemblyOptions.Default;

        var groups = new Dictionary<string, List<RepoInput>>(StringComparer.OrdinalIgnoreCase);
        foreach (var repo in repos)
        {
            if (string.IsNullOrWhiteSpace(repo.ComponentId))
            {
                continue;
            }

            if (!groups.TryGetValue(repo.ComponentId, out var list))
            {
                list = new List<RepoInput>();
                groups[repo.ComponentId] = list;
            }

            list.Add(repo);
        }

        // 组内以出现顺序稳定：优先使用远程清单给出的规范大小写（由 SnapshotService 保证先加入），
        // 因此这里直接取首个 ComponentId 作为展示名。
        var components = groups.Values
            .Select(group => AssembleComponent(group[0].ComponentId, group, options))
            .OrderBy(c => c.SeverityRank)
            .ThenBy(c => c.ComponentId, StringComparer.Ordinal)
            .ToList();

        return components;
    }

    /// <summary>单个组件组装（空组也返回 BINDING_ERROR，不抛异常、不空白）。</summary>
    public static ComponentDto AssembleComponent(
        string componentId,
        IReadOnlyList<RepoInput> repos,
        ComponentAssemblyOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(componentId);
        ArgumentNullException.ThrowIfNull(repos);
        options ??= ComponentAssemblyOptions.Default;

        var orderedRepos = OrderAndDeduplicate(repos);
        var bindingCause = DetermineBindingCause(componentId, orderedRepos, repos, options);

        var evaluations = new List<(RepoInput Input, RepoEvaluation Evaluation)>(orderedRepos.Count);
        foreach (var input in orderedRepos)
        {
            var evaluation = RepoStateEvaluator.Evaluate(input.Facts);
            evaluations.Add((input, evaluation));
        }

        var snapshots = evaluations
            .Select(pair => new RepoSnapshot(
                RepoId: $"{componentId}_{WireRepoType(pair.Input.RepoType)}",
                ComponentId: componentId,
                RepoType: pair.Input.RepoType,
                State: pair.Evaluation.State,
                NoUpstream: pair.Evaluation.SubType,
                Flags: pair.Evaluation.Flags))
            .ToList();

        var mixedBranch = IsMixedBranch(evaluations);
        var tagsConsistent = AreTagsConsistent(evaluations);

        var componentSnapshot = new ComponentSnapshot(
            ComponentId: componentId,
            Repos: snapshots,
            BindingCause: bindingCause,
            TagsConsistent: tagsConsistent,
            MixedBranch: mixedBranch,
            OperationPartialFailed: false);

        var state = ComponentAggregation.Evaluate(componentSnapshot);
        var componentFlags = new List<string>();

        // H-01：远程未就绪（本地清单不完整）时不得判 MISSING/INVALID，正确态统一为 UNKNOWN + incomplete=1。
        if (bindingCause == BindingCause.None && !options.RemoteKnown && orderedRepos.Count < 4)
        {
            state = ComponentState.Unknown;
            componentFlags.Add("incomplete");
        }

        if (mixedBranch)
        {
            componentFlags.Add("mixed_branch");
        }

        if (evaluations.Any(pair => pair.Evaluation.Flags.HasFlag(RepoFlags.TagMoved)) && !componentFlags.Contains("tag_moved"))
        {
            componentFlags.Add("tag_moved");
        }

        var severityRank = ComponentAggregation.SeverityRank(componentSnapshot, state);

        return new ComponentDto(
            ComponentId: componentId,
            AggregateState: WireComponentState(state),
            SeverityRank: severityRank,
            Branch: SelectDisplayBranch(evaluations),
            Tag: SelectDisplayTag(evaluations),
            MixedBranch: mixedBranch,
            Flags: componentFlags,
            Repos: evaluations.Select(pair => ToRepoDto(pair.Input, pair.Evaluation)).ToList());
    }

    /// <summary>按 RepoType 固定顺序排序并按类型去重（歧义由 BindingCause 表达，DTO 保持四仓形状）。</summary>
    private static List<RepoInput> OrderAndDeduplicate(IReadOnlyList<RepoInput> repos)
    {
        var result = new List<RepoInput>(4);
        foreach (var type in OrderedRepoTypes)
        {
            var match = repos.FirstOrDefault(r => r.RepoType == type);
            if (match is not null)
            {
                result.Add(match);
            }
        }

        return result;
    }

    private static BindingCause DetermineBindingCause(
        string componentId,
        IReadOnlyList<RepoInput> orderedRepos,
        IReadOnlyList<RepoInput> allRepos,
        ComponentAssemblyOptions options)
    {
        // 空组：绑定异常（最高严重度档）。
        if (orderedRepos.Count == 0)
        {
            return BindingCause.InvalidOrUnbound;
        }

        // 解析歧义/未绑定：同组件同类型出现多个仓（allRepos > orderedRepos），或出现无法归类的 OTHER
        // → AMBIGUOUS（severity=1）。DTO 仍按四仓形状去重，歧义由 binding_cause 表达。
        if (orderedRepos.Any(r => r.RepoType == RepoType.Other) || allRepos.Count != orderedRepos.Count)
        {
            return BindingCause.InvalidOrUnbound;
        }

        if (options.AmbiguousComponentIds is not null && options.AmbiguousComponentIds.Contains(componentId))
        {
            return BindingCause.InvalidOrUnbound;
        }

        // 任一仓 INVALID（目录不是 git 仓 / origin 不匹配）→ BINDING_ERROR(binding_cause=INVALID, severity=1)。
        // 注意：扫描失败（Error != null）记 UNKNOWN，不在此处升格为 INVALID。
        if (orderedRepos.Any(r => r.Facts.Exists && r.Facts.Error is null && !r.Facts.IsValidRepo))
        {
            return BindingCause.InvalidOrUnbound;
        }

        // 远程清单已就绪但该组件远程仓数 ≠ 4 → 远程缺仓（INCOMPLETE，severity=12，中性低）。
        if (options.RemoteKnown && orderedRepos.Count < 4)
        {
            return BindingCause.Incomplete;
        }

        return BindingCause.None;
    }

    /// <summary>组件级 mixed_branch：在分支上的仓当前分支名不一致（detached/unborn 不参与）。</summary>
    private static bool IsMixedBranch(IReadOnlyList<(RepoInput Input, RepoEvaluation Evaluation)> evaluations)
    {
        var branches = evaluations
            .Where(pair => pair.Input.Facts.Exists
                && pair.Input.Facts.IsValidRepo
                && pair.Input.Facts.HeadKind == HeadKind.Branch
                && !string.IsNullOrWhiteSpace(pair.Input.Facts.Branch)
                && pair.Evaluation.State != RepoState.Invalid)
            .Select(pair => pair.Input.Facts.Branch!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return branches.Count > 1;
    }

    /// <summary>
    /// 四仓 tag 一致性（SRS §7.4 / FR-STA-06 无条件计算）：
    /// 有效仓的“本地 tag 名集合”不一致，或任一仓 tag_moved → 不一致。
    /// </summary>
    private static bool AreTagsConsistent(IReadOnlyList<(RepoInput Input, RepoEvaluation Evaluation)> evaluations)
    {
        if (evaluations.Any(pair => pair.Evaluation.Flags.HasFlag(RepoFlags.TagMoved)))
        {
            return false;
        }

        var tagSets = evaluations
            .Where(pair => pair.Input.Facts.Exists
                && pair.Input.Facts.IsValidRepo
                && pair.Input.Facts.Error is null
                && pair.Input.Facts.HeadKind != HeadKind.Unborn)
            .Select(pair => string.Join('\u001f', pair.Input.Facts.LocalTags.OrderBy(t => t, StringComparer.Ordinal)))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return tagSets.Count <= 1;
    }

    /// <summary>聚合展示分支：Impl 优先（SRS §11.3），否则取第一个在分支上的仓。</summary>
    private static string? SelectDisplayBranch(IReadOnlyList<(RepoInput Input, RepoEvaluation Evaluation)> evaluations)
    {
        var impl = evaluations.FirstOrDefault(pair =>
            pair.Input.RepoType == RepoType.Impl
            && pair.Input.Facts.HeadKind == HeadKind.Branch
            && !string.IsNullOrWhiteSpace(pair.Input.Facts.Branch));
        if (impl.Input is not null)
        {
            return impl.Input.Facts.Branch;
        }

        return evaluations
            .Where(pair => pair.Input.Facts.HeadKind == HeadKind.Branch && !string.IsNullOrWhiteSpace(pair.Input.Facts.Branch))
            .Select(pair => pair.Input.Facts.Branch)
            .FirstOrDefault();
    }

    /// <summary>聚合展示 Tag：四仓 HEAD 指向同一个 tag 时展示，否则交由 TAG_DRIFT 逐仓展示。</summary>
    private static string? SelectDisplayTag(IReadOnlyList<(RepoInput Input, RepoEvaluation Evaluation)> evaluations)
    {
        if (evaluations.Count == 0)
        {
            return null;
        }

        var tagLists = evaluations.Select(pair => pair.Input.Facts.TagsAtHead).ToList();
        if (tagLists.Any(tags => tags.Count != 1))
        {
            return null;
        }

        var candidate = tagLists[0][0];
        return tagLists.All(tags => string.Equals(tags[0], candidate, StringComparison.Ordinal))
            ? candidate
            : null;
    }

    private static RepoDto ToRepoDto(RepoInput input, RepoEvaluation evaluation) =>
        new(
            RepoType: WireRepoType(input.RepoType),
            State: WireRepoState(evaluation.State),
            SubType: WireSubType(evaluation.SubType),
            Branch: input.Facts.Branch,
            HeadSha: input.Facts.HeadSha,
            Ahead: evaluation.Ahead,
            Behind: evaluation.Behind,
            DirtyCount: input.Facts.DirtyCount,
            UntrackedCount: input.Facts.UntrackedCount,
            IsLocal: input.Facts.Exists,
            LocalPath: input.Facts.LocalPath,
            Flags: evaluation.Flags.ToWireNames());

    /// <summary>单仓主状态 → SRS §7.2 UPPER_SNAKE 名称。</summary>
    public static string WireRepoState(RepoState state) => state switch
    {
        RepoState.Unknown => "UNKNOWN",
        RepoState.Invalid => "INVALID",
        RepoState.Missing => "MISSING",
        RepoState.Empty => "EMPTY",
        RepoState.Unmerged => "UNMERGED",
        RepoState.InProgress => "IN_PROGRESS",
        RepoState.DetachedTag => "DETACHED_TAG",
        RepoState.DetachedCommit => "DETACHED_COMMIT",
        RepoState.Diverged => "DIVERGED",
        RepoState.Behind => "BEHIND",
        RepoState.Ahead => "AHEAD",
        RepoState.NoUpstream => "NO_UPSTREAM",
        RepoState.Dirty => "DIRTY",
        RepoState.Synced => "SYNCED",
        _ => "UNKNOWN"
    };

    /// <summary>组件聚合态 → SRS §7.4 UPPER_SNAKE 名称。</summary>
    public static string WireComponentState(ComponentState state) => state switch
    {
        ComponentState.OpPartialFailed => "OP_PARTIAL_FAILED",
        ComponentState.BindingError => "BINDING_ERROR",
        ComponentState.Unknown => "UNKNOWN",
        ComponentState.Missing => "MISSING",
        ComponentState.BlockedLocal => "BLOCKED_LOCAL",
        ComponentState.Empty => "EMPTY",
        ComponentState.TagDrift => "TAG_DRIFT",
        ComponentState.Diverged => "DIVERGED",
        ComponentState.Behind => "BEHIND",
        ComponentState.Ahead => "AHEAD",
        ComponentState.Detached => "DETACHED",
        ComponentState.MixedBranch => "MIXED_BRANCH",
        ComponentState.NewBranch => "NEW_BRANCH",
        ComponentState.Dirty => "DIRTY",
        ComponentState.Synced => "SYNCED",
        _ => "UNKNOWN"
    };

    /// <summary>生命周期类型 → SRS UPPER_SNAKE 名称。</summary>
    public static string WireRepoType(RepoType repoType) => repoType switch
    {
        RepoType.Design => "DESIGN",
        RepoType.Impl => "IMPL",
        RepoType.Mil => "MIL",
        RepoType.Sil => "SIL",
        _ => "OTHER"
    };

    /// <summary>NO_UPSTREAM 子型 → SRS §7.2 枚举名；None → null。</summary>
    public static string? WireSubType(NoUpstreamSubType subType) => subType switch
    {
        NoUpstreamSubType.LocalOnly => "LOCAL_ONLY",
        NoUpstreamSubType.UnsetButExists => "UNSET_BUT_EXISTS",
        NoUpstreamSubType.UpstreamGone => "UPSTREAM_GONE",
        _ => null
    };
}
