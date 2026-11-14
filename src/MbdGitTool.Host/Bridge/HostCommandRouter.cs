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
            ["getHostInfo"] = TimeSpan.FromSeconds(2),

            // M2b 作业/批量：runBatch 仅受理入队（10s），查询/取消/重试均为内存操作（5s）。
            ["runBatch"] = TimeSpan.FromSeconds(10),
            ["getJobs"] = TimeSpan.FromSeconds(5),
            ["getJob"] = TimeSpan.FromSeconds(5),
            ["cancelJob"] = TimeSpan.FromSeconds(5),
            ["retryFailed"] = TimeSpan.FromSeconds(5),

            // M2b-2：审计查询/导出、备份 ref 列表/恢复、保留清理。
            ["getOperationLog"] = TimeSpan.FromSeconds(10),
            ["exportOperationLog"] = TimeSpan.FromSeconds(30),
            ["getBackupRefs"] = TimeSpan.FromSeconds(20),
            ["restoreBackupRef"] = TimeSpan.FromSeconds(120),
            ["cleanupRetention"] = TimeSpan.FromSeconds(120)
        };

    private readonly SnapshotService _snapshots;
    private readonly PathPolicy? _paths;
    private readonly BridgeError? _workspaceError;
    private readonly FileTreeReader _reader = new();
    private readonly GitStatusIndex _gitStatus;
    private readonly CommitsService _commits;
    private readonly RepoActionService _actions;
    private readonly OperationAuditService _audit;
    private readonly JobEngine _jobs;
    private readonly BackupRefService _backup;
    private readonly RetentionService _retention;
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
        // M2b-2：同一 OperationAuditService 双写 SQLite operation_log（初始化失败自动降级 JSONL-only）。
        _audit = new OperationAuditService(_snapshots.Config);

        // M2b-2：备份 ref 列表/恢复（refs/backup/sync/*；恢复前写 refs/backup/restore/* 安全备份）。
        _backup = new BackupRefService(_snapshots.Config);

        // M2b-2：本地保留清理（.trash 30 天 / 备份 ref backup.retentionDays / 审计 auditRetentionDays）。
        _retention = new RetentionService(_snapshots.Config, _audit, _backup);

        // 启动保留清理：后台执行，避免阻塞 Host 首屏（命令 cleanupRetention 可同步触发）。
        StartupRetention = Task.Run(async () =>
        {
            try
            {
                await _retention.RunAsync(writeAudit: false).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                HostLog.Warn($"启动保留清理失败（{ex.GetType().Name}）");
            }
        });

        _remoteRefs = new RemoteRefsService();

        // M2b 作业内核：后台单 worker 串行 job；事件经 EventSink（WebViewBridge 设置）推送 JOB_UPDATED。
        // 作业结束后失效派生缓存与“已交付快照”CAS 基线，避免批量写后紧接着的单仓写被误拒。
        _jobs = new JobEngine(
            _snapshots.Config,
            _snapshots,
            actions: _actions,
            audit: _audit,
            onJobFinished: () =>
            {
                _deliveredIndex = null;
                InvalidateCaches();
            });

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

    /// <summary>
    /// M2b-2 启动保留清理任务（构造时后台执行；诊断/测试可 await 其完成，
    /// 结果与 <c>cleanupRetention</c> 命令一致，失败只记日志不抛异常）。
    /// </summary>
    public Task StartupRetention { get; }

    /// <summary>
    /// M2b 作业事件出口（由 <c>WebViewBridge</c> 在初始化后设置）：参数固定为
    /// <c>{ event:"JOB_UPDATED", jobId, current:JobDto, data:JobDto, state, total, cancellable }</c>
    /// 的对象，由桥在 UI 线程序列化后 PostWebMessageAsJson。
    /// </summary>
    public Action<object>? EventSink
    {
        get => _jobs.EventSink;
        set => _jobs.EventSink = value;
    }

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
        _jobs.Dispose();
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

            case "runBatch":
            {
                // 仅受理入队（不等待执行；执行在 JobEngine 后台 worker，事件走 JOB_UPDATED）。
                var parsed = ParseParams<RunBatchParams>(parameters);
                return _jobs.RunBatch(parsed);
            }

            case "getJobs":
            {
                var parsed = ParseParams<GetJobsParams>(parameters);
                return _jobs.GetJobs(parsed);
            }

            case "getJob":
            {
                var parsed = ParseParams<GetJobParams>(parameters);
                return _jobs.GetJob(parsed);
            }

            case "cancelJob":
            {
                var parsed = ParseParams<CancelJobParams>(parameters);
                return _jobs.CancelJob(parsed);
            }

            case "retryFailed":
            {
                var parsed = ParseParams<RetryFailedParams>(parameters);
                return _jobs.RetryFailed(parsed);
            }

            case "getOperationLog":
            {
                // FR-OP-09：operation_log 查询（SQLite；无 token；降级时返回 E_INTERNAL）。
                var parsed = ParseParams<GetOperationLogParams>(parameters);
                return _audit.GetOperationLog(parsed);
            }

            case "exportOperationLog":
            {
                // FR-OP-09：导出 CSV/JSON 到 %LOCALAPPDATA%\MbdGitTool\exports\（返回路径给 UI）。
                var parsed = ParseParams<ExportOperationLogParams>(parameters);
                return await RunIoAsync(
                        () => _audit.ExportOperationLog(parsed),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            case "getBackupRefs":
            {
                // FR-OP-11：列出本地 refs/backup/sync/*（componentId + 可选 repoType；缺省 = 四仓全部）。
                await EnsureSnapshotAsync(cancellationToken).ConfigureAwait(false);
                var parsed = ParseParams<GetBackupRefsParams>(parameters);
                return await RunIoAsync(
                        () => GetBackupRefsAsync(parsed, cancellationToken),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            case "restoreBackupRef":
            {
                // FR-OP-11/AC-OP-30：预检 → 当前 HEAD 备份到 refs/backup/restore/<ts>/<TYPE> → reset --hard <ref>。
                var parsed = ParseParams<RestoreBackupRefParams>(parameters);
                if (parsed.Confirm != true)
                {
                    return CommandResult.Failure(
                        BridgeErrorCodes.ConfirmRequired,
                        "restoreBackupRef 会改写本地 HEAD，需 args.confirm=true 二次确认",
                        new Dictionary<string, object?>
                        {
                            ["action"] = "RESTORE_BACKUP",
                            ["requiresConfirm"] = true
                        });
                }

                var requestId = Guid.NewGuid().ToString("N");
                var actor = Environment.UserName;
                await _snapshots.RefreshAsync(cancellationToken).ConfigureAwait(false);

                var resolveError = TryGetLocalRepoPath(
                    _snapshots.Index,
                    parsed.ComponentId,
                    parsed.RepoType,
                    out var localPath,
                    out var repoRef);
                if (resolveError is not null)
                {
                    return resolveError;
                }

                var repoType = repoRef.Repo.RepoType;

                var stopwatch = Stopwatch.StartNew();
                var (restored, restoreError) = await _backup
                    .RestoreAsync(localPath, repoType, parsed.Ref, cancellationToken)
                    .ConfigureAwait(false);
                stopwatch.Stop();

                if (restoreError is not null || restored is null)
                {
                    _audit.Record(
                        action: "RESTORE_BACKUP",
                        componentId: parsed.ComponentId,
                        repoType: repoType,
                        state: repoRef.Repo.State,
                        preHead: null,
                        postHead: null,
                        ok: false,
                        errorCode: restoreError?.Code ?? BridgeErrorCodes.GitFailed,
                        durationMs: stopwatch.ElapsedMilliseconds,
                        actor: actor,
                        requestId: requestId,
                        backupRef: parsed.Ref);
                    return CommandResult.Failure(restoreError ?? new BridgeError(
                        BridgeErrorCodes.GitFailed,
                        "restoreBackupRef 失败（可重试）"));
                }

                _audit.Record(
                    action: "RESTORE_BACKUP",
                    componentId: parsed.ComponentId,
                    repoType: repoType,
                    state: repoRef.Repo.State,
                    preHead: restored.PreHead,
                    postHead: restored.PostHead,
                    ok: true,
                    errorCode: null,
                    durationMs: stopwatch.ElapsedMilliseconds,
                    actor: actor,
                    requestId: requestId,
                    backupRef: restored.Ref);

                InvalidateCaches();
                await _snapshots.RefreshAsync(cancellationToken).ConfigureAwait(false);
                _deliveredIndex = _snapshots.Index;
                HostLog.Info($"api restoreBackupRef ok ref={restored.Ref} ms={stopwatch.ElapsedMilliseconds}");
                return CommandResult.Success(restored);
            }

            case "cleanupRetention":
            {
                // M2b-2：本地保留清理（.trash 30 天 / 备份 ref 30 天 / 审计 90 天；只删本地，不触远端）。
                var parsed = ParseParams<CleanupRetentionParams>(parameters);
                var summary = await _retention.RunAsync(parsed, writeAudit: true, cancellationToken).ConfigureAwait(false);
                return CommandResult.Success(summary);
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

    /// <summary>
    /// <c>getBackupRefs</c> 实现：componentId 必填；repoType 缺省 = 该组件全部已克隆仓，
    /// 逐仓 <c>git for-each-ref refs/backup/sync</c> 后合并（单仓 git 失败不吞掉错误：全失败才返回错误）。
    /// </summary>
    private async Task<CommandResult> GetBackupRefsAsync(
        GetBackupRefsParams parsed,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(parsed.ComponentId))
        {
            return CommandResult.Failure(
                BridgeErrorCodes.InvalidArgument,
                "缺少必填参数 componentId",
                new Dictionary<string, object?> { ["field"] = "componentId" });
        }

        var index = _snapshots.Index;
        if (!index.TryGetComponent(parsed.ComponentId, out var component))
        {
            return CommandResult.Failure(
                BridgeErrorCodes.NotFound,
                "未找到组件：" + parsed.ComponentId.Trim(),
                new Dictionary<string, object?> { ["componentId"] = parsed.ComponentId });
        }

        var targets = new List<RepoRef>();
        if (!string.IsNullOrWhiteSpace(parsed.RepoType))
        {
            var resolveError = TryGetLocalRepoPath(
                index,
                parsed.ComponentId,
                parsed.RepoType,
                out _,
                out var single);
            if (resolveError is not null)
            {
                return resolveError;
            }

            targets.Add(single);
        }
        else
        {
            foreach (var repo in component.Repos)
            {
                if (!repo.IsLocal || string.IsNullOrWhiteSpace(repo.LocalPath))
                {
                    continue;
                }

                targets.Add(new RepoRef(component.ComponentId, component, repo));
            }

            if (targets.Count == 0)
            {
                return CommandResult.Failure(
                    BridgeErrorCodes.RepoNotLocal,
                    "组件没有已克隆的本地仓，无法列出备份 ref",
                    new Dictionary<string, object?> { ["componentId"] = parsed.ComponentId });
            }
        }

        var refs = new List<BackupRefDto>();
        BridgeError? firstError = null;
        foreach (var target in targets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await _backup
                .GetBackupRefsAsync(target.Repo.LocalPath, target.Repo.RepoType, cancellationToken)
                .ConfigureAwait(false);
            if (result.Ok && result.Data is BackupRefsDto page)
            {
                refs.AddRange(page.Refs);
            }
            else
            {
                firstError ??= result.Error;
            }
        }

        if (refs.Count == 0 && firstError is not null)
        {
            return CommandResult.Failure(firstError);
        }

        refs.Sort((left, right) => string.CompareOrdinal(right.Ref, left.Ref));
        return CommandResult.Success(new BackupRefsDto(refs));
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
