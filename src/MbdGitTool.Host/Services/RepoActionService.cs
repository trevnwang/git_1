using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MbdGitTool.Core.Contracts;
using MbdGitTool.Core.Domain;
using MbdGitTool.Core.Security;

namespace MbdGitTool.Host.Services;

/// <summary>
/// 单仓操作（spec §6.2 矩阵 / code-plan §3.9 / SRS §7.6/§9.16 FR-ACT）。
/// <para><b>M2</b>：<c>GetAvailability</c> 仍是矩阵唯一事实源（Enabled = MatrixAllowed，Stage=M2）；</para>
/// <para><c>RunAsync</c> 校验（action → 仓 → 矩阵 → 确认 → 本地仓）后执行真实 git 单仓操作：
/// FETCH / PULL(--ff-only) / CHKOUT / COMMIT / PUSH；同仓串行、异仓并发。</para>
/// <para><b>安全</b>：所有 git 参数走 <c>ArgumentList</c>；PAT 仅经 <c>GIT_CONFIG_*</c> 环境注入，
/// 不进入参数/日志/响应/错误 details（输出统一脱敏 + 截断 ≤2000 字符）。</para>
/// </summary>
public sealed class RepoActionService
{
    private static readonly string[] Actions = { "PULL", "FETCH", "CHKOUT", "COMMIT", "PUSH" };

    /// <summary>响应 output / 错误 stderr 摘要最大字符数（spec 风险预算）。</summary>
    private const int MaxOutputChars = 2000;

    /// <summary>COMMIT message 最大字符数（任务书 M2 口径；SRS FR-ACT-03 的 200 为 UI 约束）。</summary>
    private const int MaxCommitMessageChars = 2000;

    /// <summary>提交信息展示摘要最大字符数。</summary>
    private const int MaxErrorSummaryChars = 500;

    private const string Stage = "M2";

    // 写操作超时：fetch/pull/push 120s；commit/checkout 60s；只读探测沿用 GitCli 默认 20s。
    private static readonly TimeSpan NetworkTimeout = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan WriteTimeout = TimeSpan.FromSeconds(60);

    // 同一仓操作串行（跨服务实例共享）；不同仓各自一把锁，可并发。
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> RepoLocks =
        new(StringComparer.OrdinalIgnoreCase);

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

    private readonly GitCli _git;
    private readonly string? _giteaToken;

    /// <param name="git">git 调用封装（可空，默认 <see cref="GitCli"/>）。</param>
    /// <param name="giteaToken">Gitea PAT（可空）。仅用于网络类动作的 http.extraHeader 注入，禁止日志/响应输出。</param>
    public RepoActionService(GitCli? git = null, string? giteaToken = null)
    {
        _git = git ?? new GitCli();
        _giteaToken = string.IsNullOrWhiteSpace(giteaToken) ? null : giteaToken.Trim();
    }

    /// <summary>按仓状态给出五键可用性（固定顺序 PULL/FETCH/CHKOUT/COMMIT/PUSH；Enabled=MatrixAllowed）。</summary>
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
                reasonCode = "CONFIRM_REQUIRED";
                reason = "该操作会改变工作区/HEAD，需二次确认（args.confirm=true）";
            }
            else
            {
                reasonCode = "AVAILABLE";
                reason = "M2 已启用：可直接执行";
            }

            result.Add(new RepoActionAvailabilityDto(
                Action: action,
                Enabled: matrixAllowed, // M2：能力开关 RepoActions=true，按矩阵启用（D-024）
                MatrixAllowed: matrixAllowed,
                ReasonCode: reasonCode,
                Reason: reason,
                RequiresConfirm: requiresConfirm,
                Stage: Stage));
        }

        return result;
    }

    /// <summary>同步包装（兼容旧调用/单测）；消息循环请用 <see cref="RunAsync"/>。</summary>
    public CommandResult Run(RunRepoActionParams parameters, SnapshotIndex index) =>
        RunAsync(parameters, index, CancellationToken.None).GetAwaiter().GetResult();

    /// <summary>执行单仓操作（真实 git；不抛非取消异常：全部映射为 <see cref="CommandResult"/>）。</summary>
    public async Task<CommandResult> RunAsync(
        RunRepoActionParams parameters,
        SnapshotIndex index,
        CancellationToken cancellationToken = default)
    {
        if (parameters is null)
        {
            return CommandResult.Failure(BridgeErrorCodes.InvalidArgument, "缺少参数");
        }

        // 1) action 白名单。
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

        // 2) 仓解析。
        var resolveError = RepoResolver.TryResolve(index, parameters.ComponentId, parameters.RepoType, out var repo);
        if (resolveError is not null)
        {
            return CommandResult.Failure(resolveError);
        }

        // 3) §7.6 矩阵（唯一事实源）。
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
                    ["state"] = NormalizeState(repo.Repo.State),
                    ["reason"] = availability.Reason
                });
        }

        // 4) args 解析/校验。
        var argumentError = TryParseArguments(parameters.Args, action, out var arguments);
        if (argumentError is not null)
        {
            return argumentError;
        }

        // 5) 需确认的 cell：未带 confirm=true 一律不写（FR-ACT-04）。
        if (availability.RequiresConfirm && !arguments.Confirm)
        {
            return CommandResult.Failure(
                BridgeErrorCodes.ConfirmRequired,
                $"{action} 需要二次确认：请在 args 中传入 confirm=true 后重试",
                new Dictionary<string, object?>
                {
                    ["action"] = action,
                    ["repoType"] = repo.Repo.RepoType,
                    ["state"] = NormalizeState(repo.Repo.State),
                    ["requiresConfirm"] = true
                });
        }

        // 6) 本地仓前提（快照可能过期，必须在磁盘/Git 上再收口一次）。
        if (!repo.Repo.IsLocal || string.IsNullOrWhiteSpace(repo.Repo.LocalPath))
        {
            return CommandResult.Failure(
                BridgeErrorCodes.RepoNotLocal,
                $"仓库未克隆：{RepoResolver.RepoName(repo)}",
                new Dictionary<string, object?> { ["repoType"] = repo.Repo.RepoType });
        }

        var localPath = PathPolicy.TryCanonicalize(repo.Repo.LocalPath);
        if (localPath is null || !Directory.Exists(localPath))
        {
            return CommandResult.Failure(
                BridgeErrorCodes.RepoNotLocal,
                "本地仓库路径不存在或不可用（请刷新后重试）",
                new Dictionary<string, object?> { ["repoType"] = repo.Repo.RepoType });
        }

        if (!IsGitRepository(localPath))
        {
            return CommandResult.Failure(
                BridgeErrorCodes.RepoNotLocal,
                "本地路径不是 git 仓库（请刷新/修复后重试）",
                new Dictionary<string, object?> { ["repoType"] = repo.Repo.RepoType });
        }

        // 7) 同仓串行：per-repo 信号量（静态字典，跨实例共享）。
        var gate = RepoLocks.GetOrAdd(localPath, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await ExecuteAsync(action, repo, localPath, arguments, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<CommandResult> ExecuteAsync(
        string action,
        RepoRef repo,
        string localPath,
        ActionArguments arguments,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var preHead = await ReadHeadAsync(localPath, cancellationToken).ConfigureAwait(false);

        (CommandResult? Failure, string Output, string Message) outcome;
        switch (action)
        {
            case "FETCH":
                outcome = await FetchAsync(localPath, cancellationToken).ConfigureAwait(false);
                break;
            case "PULL":
                outcome = await PullAsync(localPath, arguments, cancellationToken).ConfigureAwait(false);
                break;
            case "CHKOUT":
                outcome = await CheckoutAsync(localPath, arguments, cancellationToken).ConfigureAwait(false);
                break;
            case "COMMIT":
                outcome = await CommitAsync(localPath, arguments, cancellationToken).ConfigureAwait(false);
                break;
            case "PUSH":
                outcome = await PushAsync(localPath, arguments, cancellationToken).ConfigureAwait(false);
                break;
            default:
                return CommandResult.Failure(
                    BridgeErrorCodes.InvalidArgument,
                    $"不支持的 action：{action}",
                    new Dictionary<string, object?> { ["field"] = "action", ["action"] = action });
        }

        stopwatch.Stop();
        if (outcome.Failure is not null)
        {
            HostLog.Warn(
                $"runRepoAction {action} {repo.ComponentId}/{repo.Repo.RepoType} ok=false "
                + $"code={outcome.Failure.Error?.Code ?? "<none>"} ms={stopwatch.ElapsedMilliseconds}");
            return outcome.Failure;
        }

        var postHead = await ReadHeadAsync(localPath, cancellationToken).ConfigureAwait(false) ?? preHead;
        var branch = await ReadCurrentBranchAsync(localPath, cancellationToken).ConfigureAwait(false);

        HostLog.Info(
            $"runRepoAction {action} {repo.ComponentId}/{repo.Repo.RepoType} ok=true ms={stopwatch.ElapsedMilliseconds}");

        return CommandResult.Success(new
        {
            accepted = true,
            action,
            componentId = repo.ComponentId,
            repoType = repo.Repo.RepoType,
            state = NormalizeState(repo.Repo.State),
            preHead,
            postHead,
            branch,
            output = outcome.Output,
            durationMs = (long)stopwatch.ElapsedMilliseconds,
            message = outcome.Message
        });
    }

    private async Task<(CommandResult? Failure, string Output, string Message)> FetchAsync(
        string localPath,
        CancellationToken cancellationToken)
    {
        var result = await _git
            .RunAsync(
                localPath,
                new[] { "fetch", "--prune", "origin" },
                cancellationToken,
                NetworkTimeout,
                BuildCredentialEnvironment())
            .ConfigureAwait(false);

        if (!result.Success)
        {
            return (MapGitFailure("FETCH", result, cancellationToken), string.Empty, string.Empty);
        }

        var output = FormatOutput(result);
        return (null, output, "FETCH 完成");
    }

    private async Task<(CommandResult? Failure, string Output, string Message)> PullAsync(
        string localPath,
        ActionArguments arguments,
        CancellationToken cancellationToken)
    {
        var branch = arguments.Branch;
        if (branch is null)
        {
            // 未指定 branch = 当前分支；detached HEAD 无法推断，要求显式参数。
            branch = await ReadCurrentBranchAsync(localPath, cancellationToken).ConfigureAwait(false);
            if (branch is null)
            {
                return (InvalidArgument(
                    "PULL 需要 branch 参数（当前 HEAD 处于 detached 状态或无法解析当前分支）",
                    "branch"), string.Empty, string.Empty);
            }
        }

        var result = await _git
            .RunAsync(
                localPath,
                new[] { "pull", "--ff-only", "origin", branch },
                cancellationToken,
                NetworkTimeout,
                BuildCredentialEnvironment())
            .ConfigureAwait(false);

        if (!result.Success)
        {
            if (IsNonFastForward(result))
            {
                var summary = SummarizeGitError(result);
                return (CommandResult.Failure(
                    BridgeErrorCodes.GitNonFastForward,
                    $"PULL 失败：无法快进（--ff-only），请手动 Merge/Rebase 后重试",
                    new Dictionary<string, object?>
                    {
                        ["action"] = "PULL",
                        ["branch"] = branch,
                        ["exitCode"] = result.ExitCode,
                        ["stderr"] = summary,
                        ["sdkCode"] = "GIT_NON_FAST_FORWARD"
                    }), string.Empty, string.Empty);
            }

            return (MapGitFailure("PULL", result, cancellationToken), string.Empty, string.Empty);
        }

        var output = FormatOutput(result);
        return (null, output, $"PULL 完成（{branch}）");
    }

    private async Task<(CommandResult? Failure, string Output, string Message)> CheckoutAsync(
        string localPath,
        ActionArguments arguments,
        CancellationToken cancellationToken)
    {
        // CHKOUT 前必须工作区 clean（含 untracked）：否则拒绝，避免覆盖本地改动。
        var status = await _git
            .RunAsync(localPath, new[] { "status", "--porcelain" }, cancellationToken)
            .ConfigureAwait(false);
        if (!status.Success)
        {
            return (MapGitFailure("CHKOUT", status, cancellationToken), string.Empty, string.Empty);
        }

        var dirty = status.Stdout.Trim();
        if (dirty.Length > 0)
        {
            var dirtyCount = dirty.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
            return (CommandResult.Failure(
                BridgeErrorCodes.DirtyWorktree,
                "工作区存在未提交变更（含未跟踪文件），CHKOUT 前请先提交或 stash",
                new Dictionary<string, object?>
                {
                    ["action"] = "CHKOUT",
                    ["dirtyCount"] = dirtyCount,
                    ["sdkCode"] = "GIT_DIRTY_WORKTREE"
                }), string.Empty, string.Empty);
        }

        string[] argumentsForGit;
        string message;
        if (arguments.Tag is not null)
        {
            argumentsForGit = new[] { "checkout", "--detach", arguments.Tag };
            message = $"CHKOUT 完成（tag {arguments.Tag}）";
        }
        else
        {
            argumentsForGit = new[] { "checkout", arguments.Branch! };
            message = $"CHKOUT 完成（分支 {arguments.Branch}）";
        }

        var result = await _git
            .RunAsync(localPath, argumentsForGit, cancellationToken, WriteTimeout)
            .ConfigureAwait(false);
        if (!result.Success)
        {
            return (MapGitFailure("CHKOUT", result, cancellationToken), string.Empty, string.Empty);
        }

        return (null, FormatOutput(result), message);
    }

    private async Task<(CommandResult? Failure, string Output, string Message)> CommitAsync(
        string localPath,
        ActionArguments arguments,
        CancellationToken cancellationToken)
    {
        var message = arguments.Message!;

        var status = await _git
            .RunAsync(localPath, new[] { "status", "--porcelain" }, cancellationToken)
            .ConfigureAwait(false);
        if (!status.Success)
        {
            return (MapGitFailure("COMMIT", status, cancellationToken), string.Empty, string.Empty);
        }

        // FR-ACT-03：只提交 index 中已暂存内容，不自动 add（避免误提交未审阅/未跟踪文件）。
        var staged = await _git
            .RunAsync(localPath, new[] { "diff", "--cached", "--quiet" }, cancellationToken)
            .ConfigureAwait(false);
        if (staged.Success)
        {
            return (NothingToCommit(), string.Empty, string.Empty);
        }

        if (staged.ExitCode != 1)
        {
            return (MapGitFailure("COMMIT", staged, cancellationToken), string.Empty, string.Empty);
        }

        var commit = await _git
            .RunAsync(localPath, new[] { "commit", "-m", message }, cancellationToken, WriteTimeout)
            .ConfigureAwait(false);
        if (!commit.Success)
        {
            return (MapGitFailure("COMMIT", commit, cancellationToken), string.Empty, string.Empty);
        }

        return (null, FormatOutput(commit), "COMMIT 完成");
    }

    private async Task<(CommandResult? Failure, string Output, string Message)> PushAsync(
        string localPath,
        ActionArguments arguments,
        CancellationToken cancellationToken)
    {
        // 禁止 force：只允许 origin HEAD（setUpstream=true 时附加 -u）。
        var pushArguments = new List<string> { "push" };
        if (arguments.SetUpstream)
        {
            pushArguments.Add("-u");
        }

        pushArguments.Add("origin");
        pushArguments.Add("HEAD");

        var result = await _git
            .RunAsync(
                localPath,
                pushArguments,
                cancellationToken,
                NetworkTimeout,
                BuildCredentialEnvironment())
            .ConfigureAwait(false);

        if (!result.Success)
        {
            // 失败原样返回 stderr 摘要（脱敏 token），不额外改写 git 语义。
            return (MapGitFailure("PUSH", result, cancellationToken), string.Empty, string.Empty);
        }

        return (null, FormatOutput(result), "PUSH 完成");
    }

    private async Task<string?> ReadHeadAsync(string localPath, CancellationToken cancellationToken)
    {
        var result = await _git
            .RunAsync(localPath, new[] { "rev-parse", "HEAD" }, cancellationToken)
            .ConfigureAwait(false);
        if (!result.Success)
        {
            return null; // EMPTY（unborn HEAD）等：preHead/postHead 允许为空。
        }

        var head = result.TrimmedStdout;
        return head.Length is > 0 and <= 64 ? head : null;
    }

    private async Task<string?> ReadCurrentBranchAsync(string localPath, CancellationToken cancellationToken)
    {
        var result = await _git
            .RunAsync(localPath, new[] { "rev-parse", "--abbrev-ref", "HEAD" }, cancellationToken)
            .ConfigureAwait(false);
        if (!result.Success)
        {
            return null;
        }

        var branch = result.TrimmedStdout;
        if (branch.Length == 0 || branch.Equals("HEAD", StringComparison.Ordinal))
        {
            return null; // detached HEAD
        }

        return branch;
    }

    /// <summary>网络类动作的凭据环境：Gitea PAT → <c>Authorization: Basic base64(token:)</c>（http.extraHeader）。</summary>
    private IReadOnlyDictionary<string, string>? BuildCredentialEnvironment()
    {
        var token = _giteaToken;
        if (token is null)
        {
            // 无 token 仍尝试（公开仓/系统凭据助手已配置）。
            return null;
        }

        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes(token + ":"));
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["GIT_CONFIG_COUNT"] = "1",
            ["GIT_CONFIG_KEY_0"] = "http.extraHeader",
            ["GIT_CONFIG_VALUE_0"] = "Authorization: Basic " + basic
        };
    }

    private CommandResult MapGitFailure(string action, GitResult result, CancellationToken cancellationToken)
    {
        if (result.Canceled)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            return CommandResult.Failure(
                BridgeErrorCodes.GitFailed,
                $"{action} 已取消",
                new Dictionary<string, object?>
                {
                    ["action"] = action,
                    ["sdkCode"] = "GIT_CANCELLED"
                });
        }

        if (result.TimedOut)
        {
            return CommandResult.Failure(
                BridgeErrorCodes.Timeout,
                $"{action} 执行超时（git 进程已终止）",
                new Dictionary<string, object?>
                {
                    ["action"] = action,
                    ["sdkCode"] = "GIT_TIMEOUT"
                });
        }

        var summary = SummarizeGitError(result);
        return CommandResult.Failure(
            BridgeErrorCodes.GitFailed,
            $"{action} 失败（exit={result.ExitCode}）：{summary}",
            new Dictionary<string, object?>
            {
                ["action"] = action,
                ["exitCode"] = result.ExitCode,
                ["stderr"] = summary,
                ["sdkCode"] = "GIT_FAILED"
            });
    }

    private CommandResult NothingToCommit() => CommandResult.Failure(
        BridgeErrorCodes.NothingToCommit,
        "没有可提交的变更",
        new Dictionary<string, object?>
        {
            ["action"] = "COMMIT",
            ["sdkCode"] = "GIT_NOTHING_TO_COMMIT"
        });

    private static CommandResult InvalidArgument(string message, string field) => CommandResult.Failure(
        BridgeErrorCodes.InvalidArgument,
        message,
        new Dictionary<string, object?> { ["field"] = field });

    private static bool IsNonFastForward(GitResult result)
    {
        var text = (result.Stderr + "\n" + result.Stdout).ToLowerInvariant();
        return text.Contains("not possible to fast-forward", StringComparison.Ordinal)
            || text.Contains("non-fast-forward", StringComparison.Ordinal)
            || text.Contains("not a fast forward", StringComparison.Ordinal);
    }

    private string FormatOutput(GitResult result) =>
        Truncate(RedactSecrets(Combine(result.Stdout, result.Stderr)));

    private string SummarizeGitError(GitResult result)
    {
        var text = Combine(result.Stderr, result.Stdout);
        if (text.Length == 0)
        {
            return "（无 stderr 输出）";
        }

        var firstLine = text.Split('\n')[0].Trim();
        if (firstLine.Length == 0)
        {
            firstLine = text.Trim().Split('\n')[0].Trim();
        }

        var redacted = RedactSecrets(firstLine);
        return redacted.Length <= MaxErrorSummaryChars ? redacted : redacted[..MaxErrorSummaryChars] + "…";
    }

    private static string Combine(string stdout, string stderr)
    {
        var left = NormalizeNewLines(stdout).Trim();
        var right = NormalizeNewLines(stderr).Trim();
        if (left.Length == 0)
        {
            return right;
        }

        return right.Length == 0 ? left : left + "\n" + right;
    }

    private static string NormalizeNewLines(string? text) =>
        string.IsNullOrEmpty(text) ? string.Empty : text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private static string Truncate(string text) =>
        text.Length <= MaxOutputChars ? text : text[..(MaxOutputChars - 1)] + "…";

    /// <summary>token/Authorization/URL userinfo 脱敏（响应、日志、错误 details 统一走这里）。</summary>
    private string RedactSecrets(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var redacted = text;
        var token = _giteaToken;
        if (token is not null)
        {
            redacted = redacted.Replace(token, "***", StringComparison.Ordinal);
            var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes(token + ":"));
            redacted = redacted.Replace(basic, "***", StringComparison.Ordinal);
        }

        return HostLog.Redact(redacted);
    }

    private static bool IsGitRepository(string localPath)
    {
        if (Directory.Exists(Path.Combine(localPath, ".git")))
        {
            return true;
        }

        // worktree/submodule 场景 .git 为文件（gitdir: ...）。
        return File.Exists(Path.Combine(localPath, ".git"));
    }

    private static CommandResult? TryParseArguments(
        IReadOnlyDictionary<string, JsonElement>? raw,
        string action,
        out ActionArguments arguments)
    {
        arguments = new ActionArguments(null, null, null, false, false);
        if (raw is null || raw.Count == 0)
        {
            return ValidateRequiredArguments(action, arguments);
        }

        var error = ReadString(raw, "branch", out var branch);
        if (error is not null)
        {
            return error;
        }

        error = ReadString(raw, "tag", out var tag);
        if (error is not null)
        {
            return error;
        }

        error = ReadString(raw, "message", out var message);
        if (error is not null)
        {
            return error;
        }

        error = ReadBool(raw, "confirm", out var confirm);
        if (error is not null)
        {
            return error;
        }

        error = ReadBool(raw, "setUpstream", out var setUpstream);
        if (error is not null)
        {
            return error;
        }

        if (branch is not null)
        {
            var branchError = ValidateRefName(branch, "branch");
            if (branchError is not null)
            {
                return branchError;
            }
        }

        if (tag is not null)
        {
            var tagError = ValidateRefName(tag, "tag");
            if (tagError is not null)
            {
                return tagError;
            }
        }

        if (message is not null)
        {
            message = message.Trim();
            if (message.Length > MaxCommitMessageChars)
            {
                return InvalidArgument($"参数 message 超长（≤{MaxCommitMessageChars} 字符）", "message");
            }
        }

        arguments = new ActionArguments(branch, tag, message, confirm, setUpstream);
        return ValidateRequiredArguments(action, arguments);
    }

    private static CommandResult? ValidateRequiredArguments(string action, ActionArguments arguments)
    {
        switch (action)
        {
            case "CHKOUT":
                if (arguments.Branch is not null && arguments.Tag is not null)
                {
                    return InvalidArgument("CHKOUT 的 branch 与 tag 只能二选一", "branch");
                }

                if (arguments.Branch is null && arguments.Tag is null)
                {
                    return InvalidArgument("CHKOUT 需要 branch 或 tag 参数", "branch");
                }

                return null;

            case "COMMIT":
                if (string.IsNullOrWhiteSpace(arguments.Message))
                {
                    return InvalidArgument("COMMIT 需要非空 message 参数", "message");
                }

                return null;

            default:
                return null;
        }
    }

    private static CommandResult? ReadString(
        IReadOnlyDictionary<string, JsonElement> raw,
        string field,
        out string? value)
    {
        value = null;
        if (!TryGetElement(raw, field, out var element) || element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (element.ValueKind != JsonValueKind.String)
        {
            return InvalidArgument($"参数 {field} 必须是字符串", field);
        }

        var text = element.GetString();
        if (text is not null)
        {
            text = text.Trim();
        }

        value = string.IsNullOrEmpty(text) ? null : text;
        return null;
    }

    private static CommandResult? ReadBool(
        IReadOnlyDictionary<string, JsonElement> raw,
        string field,
        out bool value)
    {
        value = false;
        if (!TryGetElement(raw, field, out var element) || element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (element.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            value = element.GetBoolean();
            return null;
        }

        return InvalidArgument($"参数 {field} 必须是布尔值", field);
    }

    private static bool TryGetElement(
        IReadOnlyDictionary<string, JsonElement> raw,
        string field,
        out JsonElement element)
    {
        if (raw.TryGetValue(field, out element))
        {
            return true;
        }

        foreach (var pair in raw)
        {
            if (pair.Key.Equals(field, StringComparison.OrdinalIgnoreCase))
            {
                element = pair.Value;
                return true;
            }
        }

        element = default;
        return false;
    }

    /// <summary>保守的 ref 名校验（只做安全收口；git 仍是最终裁判）。拒绝控制字符/空白/危险符号/选项注入。</summary>
    private static CommandResult? ValidateRefName(string value, string field)
    {
        if (value.Length > 255)
        {
            return InvalidArgument($"参数 {field} 超长（≤255 字符）", field);
        }

        if (value[0] is '-')
        {
            return InvalidArgument($"参数 {field} 不能以 '-' 开头", field);
        }

        foreach (var ch in value)
        {
            if (char.IsWhiteSpace(ch) || char.IsControl(ch))
            {
                return InvalidArgument($"参数 {field} 不能包含空白/控制字符", field);
            }

            if (ch is '~' or '^' or ':' or '?' or '*' or '[' or '\\')
            {
                return InvalidArgument($"参数 {field} 包含非法字符 {ch}", field);
            }
        }

        if (value.Contains("..", StringComparison.Ordinal)
            || value.Contains("@{", StringComparison.Ordinal)
            || value.EndsWith('.')
            || value.EndsWith('/')
            || value.EndsWith(".lock", StringComparison.OrdinalIgnoreCase))
        {
            return InvalidArgument($"参数 {field} 不是合法的分支/Tag 名", field);
        }

        return null;
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

    /// <summary>解析后的 args（branch/tag/message/confirm/setUpstream）。</summary>
    private sealed record ActionArguments(
        string? Branch,
        string? Tag,
        string? Message,
        bool Confirm,
        bool SetUpstream);
}
