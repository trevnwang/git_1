using System;
using System.Collections.Generic;
using System.Linq;
using MbdGitTool.Core.Contracts;
using MbdGitTool.Core.Domain;

namespace MbdGitTool.Host.Services;

/// <summary>
/// 单仓操作可用性（spec §6.2 矩阵 / code-plan §4.8）。
/// <para><b>M1 只读</b>：<c>Enabled</c> 恒 false；<c>MatrixAllowed</c> 按 13 状态 × 5 键真实矩阵；</para>
/// <para><c>runRepoAction</c> 校验后返回 <c>E_NOT_IMPLEMENTED</c>（M2 才冻结 job 入队 + JOB_UPDATED）。</para>
/// </summary>
public sealed class RepoActionService
{
    private static readonly string[] Actions = { "PULL", "FETCH", "CHKOUT", "COMMIT", "PUSH" };

    // spec §6.2 矩阵编码（顺序 PULL/FETCH/CHKOUT/COMMIT/PUSH）：Y=允许 N=禁止 C=允许但需确认。
    private static readonly IReadOnlyDictionary<string, string> Matrix =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["SYNCED"] = "YYYYY",
            ["DIRTY"] = "NYCYY",
            ["AHEAD"] = "NYCNY",
            ["BEHIND"] = "YYCNN",
            ["DIVERGED"] = "NYCNN",
            ["DETACHED_TAG"] = "NYCNN",
            ["DETACHED_COMMIT"] = "NYCNN",
            ["NO_UPSTREAM"] = "NYNNY",
            ["EMPTY"] = "NYCYY",
            ["UNMERGED"] = "NYNNN",
            ["IN_PROGRESS"] = "NNNNN",
            ["INVALID"] = "NNNNN",
            ["MISSING"] = "NNNNN",
            ["UNKNOWN"] = "NYNNN"
        };

    /// <summary>按仓状态给出五键可用性（固定顺序 PULL/FETCH/CHKOUT/COMMIT/PUSH）。</summary>
    public IReadOnlyList<RepoActionAvailabilityDto> GetAvailability(RepoRef repo)
    {
        var state = NormalizeState(repo.Repo.State);
        var row = Matrix.TryGetValue(state, out var encoded) ? encoded : "NNNNN";
        var result = new List<RepoActionAvailabilityDto>(Actions.Length);

        for (var index = 0; index < Actions.Length; index++)
        {
            var action = Actions[index];
            var cell = index < row.Length ? row[index] : 'N';
            var matrixAllowed = cell is 'Y' or 'C';
            var requiresConfirm = cell == 'C';

            string reasonCode;
            string reason;
            if (!matrixAllowed)
            {
                reasonCode = "STATE_BLOCKED";
                reason = DisabledReason(state, action);
            }
            else if (requiresConfirm)
            {
                reasonCode = "M1_READ_ONLY";
                reason = "M1 只读，M2 启用；该操作需要二次确认";
            }
            else
            {
                reasonCode = "M1_READ_ONLY";
                reason = "M1 只读，M2 启用";
            }

            result.Add(new RepoActionAvailabilityDto(
                Action: action,
                Enabled: false, // M1 一律禁用（FR-ACT-02/D-024）
                MatrixAllowed: matrixAllowed,
                ReasonCode: reasonCode,
                Reason: reason,
                RequiresConfirm: requiresConfirm,
                Stage: "M2"));
        }

        return result;
    }

    /// <summary>执行单仓操作（M1：只校验，返回 E_NOT_IMPLEMENTED）。</summary>
    public CommandResult Run(RunRepoActionParams parameters, SnapshotIndex index)
    {
        if (parameters is null)
        {
            return CommandResult.Failure(BridgeErrorCodes.InvalidArgument, "缺少参数");
        }

        var action = parameters.Action?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(action) || !Actions.Contains(action, StringComparer.Ordinal))
        {
            return CommandResult.Failure(
                BridgeErrorCodes.InvalidArgument,
                "参数 action 必须是 PULL/FETCH/CHKOUT/COMMIT/PUSH",
                new Dictionary<string, object?>
                {
                    ["field"] = "action",
                    ["action"] = parameters.Action
                });
        }

        var resolveError = RepoResolver.TryResolve(index, parameters.ComponentId, parameters.RepoType, out var repo);
        if (resolveError is not null)
        {
            return CommandResult.Failure(resolveError);
        }

        var availability = GetAvailability(repo).First(item => item.Action == action);
        if (!availability.MatrixAllowed)
        {
            return CommandResult.Failure(
                BridgeErrorCodes.ActionNotAllowed,
                availability.Reason ?? $"当前状态 {repo.Repo.State} 不允许 {action}",
                new Dictionary<string, object?>
                {
                    ["action"] = action,
                    ["repoType"] = repo.Repo.RepoType,
                    ["state"] = repo.Repo.State,
                    ["reason"] = availability.Reason
                });
        }

        return CommandResult.Failure(
            BridgeErrorCodes.NotImplemented,
            $"M1 为只读模式：{action} 将在 M2 启用（需预检、dry-run/备份、逐仓结果与任务中心）",
            new Dictionary<string, object?>
            {
                ["action"] = action,
                ["repoType"] = repo.Repo.RepoType,
                ["matrixAllowed"] = true,
                ["writeStage"] = "M2"
            });
    }

    private static string NormalizeState(string? state) =>
        string.IsNullOrWhiteSpace(state) ? "UNKNOWN" : state.Trim().ToUpperInvariant();

    private static string DisabledReason(string state, string action) => (state, action) switch
    {
        ("DIRTY", "PULL") => "DIRTY：需先提交或 stash",
        ("DIVERGED", "PULL") => "DIVERGED：需手动 Merge/Rebase",
        ("UNKNOWN", "PULL") => "UNKNOWN：关系数据过期，先 FETCH",
        ("AHEAD", "PULL") => "AHEAD：需先推送或手动处理",
        ("NO_UPSTREAM", "PULL") => "NO_UPSTREAM：未配置 upstream",
        ("EMPTY", "PULL") => "EMPTY：无首个提交",
        ("UNMERGED", "PULL") => "UNMERGED：存在未解决冲突",
        ("AHEAD", "COMMIT") => "AHEAD：需先推送/手动处理",
        ("BEHIND", "COMMIT") => "BEHIND：先 PULL",
        ("UNMERGED", "COMMIT") => "UNMERGED：存在未解决冲突",
        ("IN_PROGRESS", "COMMIT") => "IN_PROGRESS：合并/变基未完成",
        ("BEHIND", "PUSH") => "BEHIND：先 PULL",
        ("DETACHED_TAG", "PUSH") => "DETACHED：请先基于当前 HEAD 创建分支",
        ("DETACHED_COMMIT", "PUSH") => "DETACHED：请先基于当前 HEAD 创建分支",
        ("UNMERGED", "PUSH") => "UNMERGED：存在未解决冲突",
        ("IN_PROGRESS", "FETCH") => "IN_PROGRESS：操作未完成",
        ("INVALID", "FETCH") => "INVALID：仓库绑定异常",
        ("MISSING", "CHKOUT") => "MISSING：仓库未克隆",
        ("IN_PROGRESS", "CHKOUT") => "IN_PROGRESS：操作未完成",
        _ => $"当前状态 {state} 不允许 {action}（spec §6.2）"
    };
}
