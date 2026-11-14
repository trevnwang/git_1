using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MbdGitTool.Core.Contracts;
using MbdGitTool.Core.Domain;
using MbdGitTool.Core.Files;
using MbdGitTool.Core.Security;
using MbdGitTool.Host.Services;

namespace MbdGitTool.Host.Bridge;

/// <summary>
/// Host 命令路由（code-plan §4.10）：方法白名单 + params 反序列化/校验 + 每方法超时 + FS/git 并发闸 +
/// 缓存失效 + <c>E_*</c> 错误映射。与 WebView2 解耦，可单测。
/// </summary>
public sealed class HostCommandRouter : IDisposable
{
    /// <summary>请求体上限（spec 风险 1）。</summary>
    public const int MaxRequestBodyBytes = 64 * 1024;

    /// <summary>单响应序列化上限（超过返回 E_RESPONSE_TOO_LARGE）。</summary>
    public const int MaxResponseBytes = 2 * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly IReadOnlyDictionary<string, TimeSpan> Timeouts =
        new Dictionary<string, TimeSpan>(StringComparer.Ordinal)
        {
            ["getSnapshot"] = TimeSpan.FromSeconds(45),
            ["refresh"] = TimeSpan.FromSeconds(120),
            ["getComponentDetail"] = TimeSpan.FromSeconds(45),
            ["getRepoDetail"] = TimeSpan.FromSeconds(10),
            ["getCommits"] = TimeSpan.FromSeconds(15),
            ["getRepoTree"] = TimeSpan.FromSeconds(10),
            ["getWorkspaceTree"] = TimeSpan.FromSeconds(10),
            ["openPath"] = TimeSpan.FromSeconds(8),
            ["runRepoAction"] = TimeSpan.FromSeconds(120),
            ["getRemoteRefs"] = TimeSpan.FromSeconds(20),
            ["getCiStatus"] = TimeSpan.FromSeconds(2),
            ["getHostInfo"] = TimeSpan.FromSeconds(2)
        };

    private readonly SnapshotService _snapshots;
    private readonly PathPolicy? _paths;
    private readonly BridgeError? _workspaceError;
    private readonly FileTreeReader _reader = new();
    private readonly GitStatusIndex _gitStatus;
    private readonly CommitsService _commits;
    private readonly RepoActionService _actions;
    private readonly OperationAuditService _audit;
    private readonly RemoteRefsService _remoteRefs;
    private readonly ComponentDetailService _componentDetail;
    private readonly RepoDetailService _repoDetail;
    private readonly CiStatusService _ci;
    private readonly HostInfoProvider _hostInfo;
    private readonly RepoTreeService? _repoTree;
    private readonly WorkspaceTreeService? _workspaceTree;
    private readonly OpenPathService? _openPath;
    private readonly SemaphoreSlim _ioGate = new(4, 4);
    private readonly CancellationTokenSource _lifetime = new();
    private volatile SnapshotIndex? _deliveredIndex;
    private bool _disposed;

    public HostCommandRouter(
        AppConfig config,
        SnapshotService snapshots,
        IProcessLauncher? launcher = null,
        string? webView2Version = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        _snapshots = snapshots ?? throw new ArgumentNullException(nameof(snapshots));
        _gitStatus = new GitStatusIndex();
        _commits = new CommitsService();

        // 单仓操作（M2）：网络类动作凭据由 token 注入（仅环境变量，不进入参数/日志/响应）；
        // P0-1 起 RepoActionService 还会在注入前校验 origin fetch/pushurl 与 Gitea base 的作用域。
        _actions = new RepoActionService(config: _snapshots.Config);

        // G-P0-2 / AC-OP-33：审计-lite（每次 runRepoAction 成功/失败写 1 行 JSONL；不含 token/output）。
        _audit = new OperationAuditService(_snapshots.Config);
        _remoteRefs = new RemoteRefsService();

        Func<SnapshotIndex> indexProvider = () => _snapshots.Index;
        _componentDetail = new ComponentDetailService(indexProvider, _actions);
        _repoDetail = new RepoDetailService(indexProvider, _commits);
        _ci = new CiStatusService(indexProvider);
        _hostInfo = new HostInfoProvider(webView2Version);

        if (!config.HasWorkspace)
        {
            _workspaceError = new BridgeError(
                BridgeErrorCodes.WorkspaceUnset,
                "未配置工作区根目录（config.json 的 workspaceRoot）");
            return;
        }

        try
        {
            _paths = new PathPolicy(config.WorkspaceRoot!);
        }
        catch (ArgumentException ex)
        {
            _workspaceError = new BridgeError(
                BridgeErrorCodes.WorkspaceInvalid,
                "工作区根目录非法：" + ex.Message,
                new Dictionary<string, object?> { ["root"] = config.WorkspaceRoot });
            return;
        }

        _repoTree = new RepoTreeService(indexProvider, _paths, _reader, _gitStatus);
        _workspaceTree = new WorkspaceTreeService(indexProvider, _paths, _reader, _gitStatus);
        _openPath = new OpenPathService(_paths, indexProvider, _gitStatus, launcher);
    }

    /// <summary>是否配置并解析出合法工作区根（openPath/getRepoTree/getWorkspaceTree 前置）。</summary>
    public bool IsWorkspaceAvailable => _paths is not null;

    /// <summary>路由分发（不抛异常：所有失败映射为 <see cref="CommandResult"/>）。</summary>
    public async Task<CommandResult> DispatchAsync(
        string? method,
        JsonElement? parameters,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(method))
        {
            return CommandResult.Failure(BridgeErrorCodes.InvalidRequest, "缺少 method");
        }

        var methodName = method.Trim();
        if (!Timeouts.TryGetValue(methodName, out var timeout))
        {
            return CommandResult.Failure(
                BridgeErrorCodes.UnknownMethod,
                $"未知方法：{methodName}",
                new Dictionary<string, object?> { ["method"] = methodName });
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, cancellationToken);
        linked.CancelAfter(timeout);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await DispatchCoreAsync(methodName, parameters, linked.Token).ConfigureAwait(false);
            stopwatch.Stop();
            HostLog.Info(
                $"api {methodName} ok={result.Ok} code={result.Error?.Code ?? "<none>"} ms={stopwatch.ElapsedMilliseconds}");
            return result;
        }
        catch (CommandException ex)
        {
            stopwatch.Stop();
            HostLog.Warn($"api {methodName} 失败 code={ex.Error.Code} ms={stopwatch.ElapsedMilliseconds}");
            return CommandResult.Failure(ex.Error);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            var message = _lifetime.IsCancellationRequested || cancellationToken.IsCancellationRequested
                ? "请求已取消"
                : $"请求超时：{methodName}";
            HostLog.Warn($"api {methodName} 取消/超时 ms={stopwatch.ElapsedMilliseconds}");
            return CommandResult.Failure(
                BridgeErrorCodes.Timeout,
                message,
                new Dictionary<string, object?> { ["method"] = methodName });
        }
        catch (JsonException)
        {
            return CommandResult.Failure(BridgeErrorCodes.InvalidArgument, "params 不是合法 JSON");
        }
        catch (UnauthorizedAccessException)
        {
            return CommandResult.Failure(BridgeErrorCodes.AccessDenied, "没有权限访问请求的资源");
        }
        catch (IOException)
        {
            return CommandResult.Failure(BridgeErrorCodes.IoError, "请求的资源读取失败（IO）");
        }
        catch (Exception ex)
        {
            // 不把异常消息/绝对路径透给 UI；只记类型。
            HostLog.Error($"api {methodName} 内部错误：{ex.GetType().Name}");
            return CommandResult.Failure(
                BridgeErrorCodes.Internal,
                $"内部错误（{ex.GetType().Name}）",
                new Dictionary<string, object?> { ["method"] = methodName });
        }
    }

    /// <summary>refresh 后清空树/详情/git 注解/提交缓存。</summary>
    public void InvalidateCaches()
    {
        _repoTree?.InvalidateCaches();
        _workspaceTree?.InvalidateCaches();
        _gitStatus.InvalidateAll();
        _commits.InvalidateAll();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _lifetime.Cancel();
        _ioGate.Dispose();
    }

    private async Task<CommandResult> DispatchCoreAsync(
        string method,
        JsonElement? parameters,
        CancellationToken cancellationToken)
    {
        switch (method)
        {
            case "getSnapshot":
            {
                var snapshot = await _snapshots.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
                // 记录“用户实际看到的快照”，写操作 CAS 以它为基线（AC-SAFE-15）。
                _deliveredIndex = _snapshots.Index;
                return CommandResult.Success(snapshot);
            }

            case "refresh":
            {
                var snapshot = await _snapshots.RefreshAsync(cancellationToken).ConfigureAwait(false);
                _deliveredIndex = _snapshots.Index;
                InvalidateCaches();
                return CommandResult.Success(snapshot);
            }

            case "getHostInfo":
                return CommandResult.Success(_hostInfo.Get());

            case "getComponentDetail":
            {
                await EnsureSnapshotAsync(cancellationToken).ConfigureAwait(false);
                var parsed = ParseParams<GetComponentDetailParams>(parameters);
                return _componentDetail.Get(parsed);
            }

            case "getCiStatus":
            {
                await EnsureSnapshotAsync(cancellationToken).ConfigureAwait(false);
                return _ci.Get(ParseParams<GetCiStatusParams>(parameters));
            }

            case "runRepoAction":
            {
                var parsed = ParseParams<RunRepoActionParams>(parameters);
                var requestId = Guid.NewGuid().ToString("N");
                var actor = Environment.UserName;

                // AC-SAFE-15：先保留“用户请求所依据快照”的 HEAD 作为 CAS 基线，再做写前 read-through。
                var requestIndex = _deliveredIndex ?? _snapshots.Index;
                var hasRequestHead = TryGetRepoHead(
                    requestIndex,
                    parsed.ComponentId,
                    parsed.RepoType,
                    out var requestHead,
                    out _);

                await _snapshots.RefreshAsync(cancellationToken).ConfigureAwait(false);

                if (hasRequestHead)
                {
                    var hasRefreshedHead = TryGetRepoHead(
                        _snapshots.Index,
                        parsed.ComponentId,
                        parsed.RepoType,
                        out var refreshedHead,
                        out var refreshedState);

                    // 外部工具在用户点击后改变 HEAD（或仓消失）：零写、审计 1 行、E_CONCURRENT_CHANGE。
                    if (!hasRefreshedHead
                        || !string.Equals(requestHead, refreshedHead, StringComparison.OrdinalIgnoreCase))
                    {
                        _audit.Record(
                            parsed.Action,
                            parsed.ComponentId,
                            parsed.RepoType,
                            refreshedState ?? "UNKNOWN",
                            requestHead,
                            refreshedHead,
                            ok: false,
                            errorCode: BridgeErrorCodes.ConcurrentChange,
                            durationMs: 0,
                            actor: actor,
                            requestId: requestId);

                        return CommandResult.Failure(
                            BridgeErrorCodes.ConcurrentChange,
                            "仓库状态在用户操作后发生变化（HEAD 已改变）；请刷新后重试",
                            new Dictionary<string, object?>
                            {
                                ["action"] = parsed.Action,
                                ["componentId"] = parsed.ComponentId,
                                ["repoType"] = parsed.RepoType,
                                ["snapshotHead"] = requestHead,
                                ["currentHead"] = refreshedHead,
                                ["requestId"] = requestId
                            });
                    }
                }

                // M2：真实单仓操作（异步执行，不走 _ioGate，避免长任务占满 FS/git 并发闸）。
                // G-P0-2 / AC-OP-33：动作返回前写审计-lite（成功/失败/异常都写 1 行，不含 token/output）。
                var result = await _actions
                    .RunAsync(
                        parsed,
                        _snapshots.Index,
                        cancellationToken,
                        execution =>
                        {
                            execution.Actor = actor;
                            execution.RequestId = requestId;
                            _audit.Record(execution);
                        })
                    .ConfigureAwait(false);

                // M2 P1-2：写操作成功后失效快照缓存/派生缓存，下一次请求按现有 refresh 机制重建（CAS 才能拿到新 HEAD）。
                if (result.Ok)
                {
                    InvalidateCaches();
                    // 成功后重建快照并把它作为下一次动作的“已交付基线”，避免紧接着的下一动作被 CAS 误拒。
                    await _snapshots.RefreshAsync(cancellationToken).ConfigureAwait(false);
                    _deliveredIndex = _snapshots.Index;
                }

                return result;
            }

            case "getRemoteRefs":
            {
                await EnsureSnapshotAsync(cancellationToken).ConfigureAwait(false);
                var parsed = ParseParams<GetRemoteRefsParams>(parameters);
                var resolveError = TryGetLocalRepoPath(
                    _snapshots.Index,
                    parsed.ComponentId,
                    parsed.RepoType,
                    out var localPath,
                    out _);
                if (resolveError is not null)
                {
                    return resolveError;
                }

                return await RunIoAsync(
                        () => _remoteRefs.GetAsync(localPath, cancellationToken),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            case "getRepoDetail":
            {
                await EnsureSnapshotAsync(cancellationToken).ConfigureAwait(false);
                var parsed = ParseParams<GetRepoDetailParams>(parameters);
                return await RunIoAsync(
                        () => _repoDetail.GetAsync(parsed, cancellationToken),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            case "getCommits":
            {
                await EnsureSnapshotAsync(cancellationToken).ConfigureAwait(false);
                var parsed = ParseParams<GetCommitsParams>(parameters);
                var resolveError = TryGetLocalRepoPath(
                    _snapshots.Index,
                    parsed.ComponentId,
                    parsed.RepoType,
                    out var localPath,
                    out _);
                if (resolveError is not null)
                {
                    return resolveError;
                }

                var skip = parsed.Skip ?? 0;
                var take = parsed.Take ?? 100;
                return await RunIoAsync(
                        () => _commits.GetAsync(localPath, skip, take, cancellationToken),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            case "getRepoTree":
            {
                var workspaceFailure = RequireWorkspace();
                if (workspaceFailure is not null)
                {
                    return workspaceFailure;
                }

                await EnsureSnapshotAsync(cancellationToken).ConfigureAwait(false);
                var parsed = ParseParams<GetRepoTreeParams>(parameters);
                return await RunIoAsync(
                        () => _repoTree!.GetTreeAsync(parsed, cancellationToken),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            case "getWorkspaceTree":
            {
                var workspaceFailure = RequireWorkspace();
                if (workspaceFailure is not null)
                {
                    return workspaceFailure;
                }

                await EnsureSnapshotAsync(cancellationToken).ConfigureAwait(false);
                var parsed = ParseParams<GetWorkspaceTreeParams>(parameters);
                return await RunIoAsync(
                        () => _workspaceTree!.GetTreeAsync(parsed, cancellationToken),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            case "openPath":
            {
                var workspaceFailure = RequireWorkspace();
                if (workspaceFailure is not null)
                {
                    return workspaceFailure;
                }

                await EnsureSnapshotAsync(cancellationToken).ConfigureAwait(false);
                var parsed = ParseParams<OpenPathParams>(parameters);
                return await RunIoAsync(
                        () => _openPath!.OpenAsync(parsed, cancellationToken),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            default:
                return CommandResult.Failure(
                    BridgeErrorCodes.UnknownMethod,
                    $"未知方法：{method}",
                    new Dictionary<string, object?> { ["method"] = method });
        }
    }

    private async Task EnsureSnapshotAsync(CancellationToken cancellationToken)
    {
        if (_snapshots.Current is null)
        {
            await _snapshots.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private CommandResult? RequireWorkspace()
    {
        if (_paths is null)
        {
            return CommandResult.Failure(_workspaceError ?? new BridgeError(
                BridgeErrorCodes.WorkspaceUnset,
                "工作区未配置"));
        }

        if (!Directory.Exists(_paths.Root))
        {
            return CommandResult.Failure(
                BridgeErrorCodes.WorkspaceInvalid,
                "工作区根目录不存在或不可读",
                new Dictionary<string, object?> { ["root"] = _paths.Root });
        }

        return null;
    }

    private static bool TryGetRepoHead(
        SnapshotIndex index,
        string? componentId,
        string? repoType,
        out string? head,
        out string? state)
    {
        head = null;
        state = null;
        if (!index.TryGetRepo(componentId, repoType, out var repo))
        {
            return false;
        }

        head = string.IsNullOrWhiteSpace(repo.Repo.HeadSha) ? null : repo.Repo.HeadSha;
        state = repo.Repo.State;
        return true;
    }

    private static CommandResult? TryGetLocalRepoPath(
        SnapshotIndex index,
        string? componentId,
        string? repoType,
        out string localPath,
        out RepoRef repo)
    {
        localPath = string.Empty;
        var resolveError = RepoResolver.TryResolve(index, componentId, repoType, out repo);
        if (resolveError is not null)
        {
            return CommandResult.Failure(resolveError);
        }

        if (!repo.Repo.IsLocal || string.IsNullOrWhiteSpace(repo.Repo.LocalPath))
        {
            return CommandResult.Failure(
                BridgeErrorCodes.RepoNotLocal,
                $"仓库未克隆：{RepoResolver.RepoName(repo)}",
                new Dictionary<string, object?> { ["repoType"] = repo.Repo.RepoType });
        }

        localPath = repo.Repo.LocalPath;
        return null;
    }

    private static T ParseParams<T>(JsonElement? parameters)
        where T : class
    {
        try
        {
            if (parameters is null
                || parameters.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            {
                return JsonSerializer.Deserialize<T>("{}", JsonOptions)
                    ?? throw new CommandException(BridgeErrorCodes.InvalidArgument, "params 不能为空");
            }

            if (parameters.Value.ValueKind != JsonValueKind.Object)
            {
                throw new CommandException(BridgeErrorCodes.InvalidArgument, "params 必须是 JSON 对象");
            }

            return JsonSerializer.Deserialize<T>(parameters.Value.GetRawText(), JsonOptions)
                ?? throw new CommandException(BridgeErrorCodes.InvalidArgument, "params 不能为 null");
        }
        catch (JsonException ex)
        {
            throw new CommandException(
                BridgeErrorCodes.InvalidArgument,
                "params 类型非法：" + ex.Message,
                new Dictionary<string, object?> { ["field"] = ex.Path });
        }
        catch (NotSupportedException)
        {
            throw new CommandException(BridgeErrorCodes.InvalidArgument, "params 包含不支持的 JSON 结构");
        }
    }

    private async Task<CommandResult> RunIoAsync(
        Func<Task<CommandResult>> work,
        CancellationToken cancellationToken)
    {
        await _ioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(work, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ioGate.Release();
        }
    }

    private async Task<CommandResult> RunIoAsync(
        Func<CommandResult> work,
        CancellationToken cancellationToken)
    {
        await _ioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(work, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ioGate.Release();
        }
    }
}
