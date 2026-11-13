using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MbdGitTool.Core.Contracts;
using MbdGitTool.Core.Domain;
using MbdGitTool.Core.Security;

namespace MbdGitTool.Host.Services;

/// <summary>进程启动抽象（测试注入 fake；生产走 <see cref="ShellProcessLauncher"/>）。</summary>
public interface IProcessLauncher
{
    bool TryStart(ProcessStartInfo startInfo, out string? error);
}

/// <summary>
/// 默认程序/资源管理器启动（code-plan §4.7）：<c>UseShellExecute=true</c>、<c>ArgumentList</c>（禁止 shell 字符串拼接）。
/// <para><c>MBD_E2E_OPEN_DRY_RUN=1</c> 时仅记录日志不启动（CDP E2E 专用，生产默认执行）。</para>
/// </summary>
public sealed class ShellProcessLauncher : IProcessLauncher
{
    private const string DryRunEnvironmentVariable = "MBD_E2E_OPEN_DRY_RUN";

    public bool TryStart(ProcessStartInfo startInfo, out string? error)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        error = null;

        if (string.Equals(
                Environment.GetEnvironmentVariable(DryRunEnvironmentVariable),
                "1",
                StringComparison.Ordinal))
        {
            HostLog.Info($"openPath dry-run（未启动）：{startInfo.FileName}");
            return true;
        }

        try
        {
            using var process = Process.Start(startInfo);
            return true;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException or PlatformNotSupportedException)
        {
            error = ex.Message;
            return false;
        }
    }
}

/// <summary>
/// openPath（spec §3.8 / code-plan §4.7）：白名单直开、危险扩展名二次确认、目录/资源管理器 reveal。
/// <para><b>安全</b>：canonical 后必须在工作区根内；带 componentId+repoType 时二次收口到仓根；<c>.git</c>/submodule/祖先 reparse 拒绝；</para>
/// <para>目录双击 = reveal；文件白名单 = open；危险/未知未确认 = <c>E_CONFIRM_REQUIRED</c>，确认后再调用。只读，不修改任何文件。</para>
/// </summary>
public sealed class OpenPathService
{
    private readonly PathPolicy _paths;
    private readonly Func<SnapshotIndex> _indexProvider;
    private readonly GitStatusIndex _annotations;
    private readonly IProcessLauncher _launcher;

    public OpenPathService(
        PathPolicy paths,
        Func<SnapshotIndex> indexProvider,
        GitStatusIndex annotations,
        IProcessLauncher? launcher = null)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _indexProvider = indexProvider ?? throw new ArgumentNullException(nameof(indexProvider));
        _annotations = annotations ?? throw new ArgumentNullException(nameof(annotations));
        _launcher = launcher ?? new ShellProcessLauncher();
    }

    public async Task<CommandResult> OpenAsync(OpenPathParams parameters, CancellationToken cancellationToken)
    {
        parameters ??= new OpenPathParams();

        var mode = string.IsNullOrWhiteSpace(parameters.Mode)
            ? "open"
            : parameters.Mode.Trim().ToLowerInvariant();
        if (mode is not ("open" or "reveal"))
        {
            return CommandResult.Failure(
                BridgeErrorCodes.InvalidArgument,
                "参数 mode 必须是 open 或 reveal",
                new Dictionary<string, object?> { ["field"] = "mode" });
        }

        if (string.IsNullOrWhiteSpace(parameters.Path))
        {
            return CommandResult.Failure(
                BridgeErrorCodes.InvalidArgument,
                "缺少必填参数 path",
                new Dictionary<string, object?> { ["field"] = "path" });
        }

        var index = _indexProvider();
        RepoRef? owner = null;
        string fullPath;

        if (!string.IsNullOrWhiteSpace(parameters.ComponentId) || !string.IsNullOrWhiteSpace(parameters.RepoType))
        {
            var resolveError = RepoResolver.TryResolve(index, parameters.ComponentId, parameters.RepoType, out var repo);
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

            var repoRoot = PathPolicy.TryCanonicalize(repo.Repo.LocalPath);
            if (repoRoot is null || !_paths.IsWithin(_paths.Root, repoRoot))
            {
                return CommandResult.Failure(
                    BridgeErrorCodes.PathOutsideRoot,
                    "仓库路径越出工作区根",
                    new Dictionary<string, object?> { ["repoType"] = repo.Repo.RepoType });
            }

            if (!_paths.TryResolveRepoPath(repoRoot, parameters.Path, out fullPath, out var pathError))
            {
                return CommandResult.Failure(pathError!);
            }

            owner = repo;
        }
        else if (!_paths.TryResolveWorkspacePath(parameters.Path, out fullPath, out var workspaceError))
        {
            return CommandResult.Failure(workspaceError!);
        }

        FileAttributes attributes;
        try
        {
            attributes = File.GetAttributes(fullPath);
        }
        catch (FileNotFoundException)
        {
            return CommandResult.Failure(
                BridgeErrorCodes.PathInvalid,
                "路径不存在",
                new Dictionary<string, object?> { ["path"] = parameters.Path });
        }
        catch (DirectoryNotFoundException)
        {
            return CommandResult.Failure(
                BridgeErrorCodes.PathInvalid,
                "路径不存在",
                new Dictionary<string, object?> { ["path"] = parameters.Path });
        }
        catch (UnauthorizedAccessException)
        {
            return CommandResult.Failure(
                BridgeErrorCodes.AccessDenied,
                "没有权限访问该路径",
                new Dictionary<string, object?> { ["path"] = parameters.Path });
        }
        catch (IOException)
        {
            return CommandResult.Failure(
                BridgeErrorCodes.IoError,
                "路径读取失败（共享冲突/设备移除）",
                new Dictionary<string, object?> { ["path"] = parameters.Path });
        }

        var isDirectory = (attributes & FileAttributes.Directory) != 0;
        var isReparseTarget = (attributes & FileAttributes.ReparsePoint) != 0;

        // 祖先 reparse（junction/symlink）可能把路径指向工作区外：一律拒绝（无法证明仍在内）。
        if (_paths.CheckReparseChain(fullPath).HasReparse)
        {
            return CommandResult.Failure(
                BridgeErrorCodes.ReparsePoint,
                "路径上层含 reparse point/junction，禁止打开",
                new Dictionary<string, object?> { ["path"] = parameters.Path });
        }

        if (isReparseTarget && mode == "open" && !isDirectory)
        {
            return CommandResult.Failure(
                BridgeErrorCodes.ReparsePoint,
                "reparse point 文件不允许直接打开，请使用“打开所在目录”",
                new Dictionary<string, object?> { ["path"] = parameters.Path });
        }

        string? owningRepoRoot = null;
        if (owner is not null)
        {
            owningRepoRoot = PathPolicy.TryCanonicalize(owner.Repo.LocalPath);
        }
        else
        {
            // 未指定仓时，尝试从快照匹配“所在仓”以执行 submodule 收口。
            foreach (var component in index.Components)
            {
                foreach (var repo in component.Repos)
                {
                    if (!repo.IsLocal || string.IsNullOrWhiteSpace(repo.LocalPath))
                    {
                        continue;
                    }

                    var candidateRoot = PathPolicy.TryCanonicalize(repo.LocalPath);
                    if (candidateRoot is not null && _paths.IsWithin(candidateRoot, fullPath))
                    {
                        owningRepoRoot = candidateRoot;
                        owner = new RepoRef(component.ComponentId, component, repo);
                        break;
                    }
                }

                if (owner is not null)
                {
                    break;
                }
            }
        }

        if (owner is not null && owningRepoRoot is not null)
        {
            var submodules = await _annotations
                .GetSubmodulesAsync(owningRepoRoot, cancellationToken)
                .ConfigureAwait(false);
            var repoRelative = _paths.ToRepoRelative(owningRepoRoot, fullPath);
            if (IsInsideSubmodule(submodules, repoRelative))
            {
                return CommandResult.Failure(
                    BridgeErrorCodes.Submodule,
                    "submodule 内部禁止打开",
                    new Dictionary<string, object?> { ["path"] = parameters.Path });
            }
        }

        var extensionClass = FileExtensionPolicy.ClassifyPath(fullPath, isDirectory);
        var extension = isDirectory ? null : FileExtensionPolicy.GetExtension(fullPath);

        if (mode == "open" && extensionClass is not (ExtensionClass.Directory or ExtensionClass.SafeText)
            && parameters.Confirm != true)
        {
            return CommandResult.Failure(
                BridgeErrorCodes.ConfirmRequired,
                $"文件类型 {extension ?? "（无扩展名）"} 可能执行脚本或未知风险，需确认后才能用默认程序打开",
                new Dictionary<string, object?>
                {
                    ["path"] = parameters.Path,
                    ["extension"] = extension,
                    ["extensionClass"] = FileExtensionPolicy.ToWireName(extensionClass)
                });
        }

        var startInfo = BuildStartInfo(fullPath, mode, isDirectory);
        if (!_launcher.TryStart(startInfo, out var launchError))
        {
            HostLog.Warn($"openPath 启动失败：mode={mode} error={launchError ?? "<unknown>"}");
            return CommandResult.Failure(
                BridgeErrorCodes.LaunchFailed,
                "无法用默认程序打开（可能无关联程序/被占用）",
                new Dictionary<string, object?> { ["path"] = parameters.Path });
        }

        var displayPath = owner is not null && owningRepoRoot is not null
            ? _paths.ToRepoRelative(owningRepoRoot, fullPath)
            : _paths.ToWorkspaceRelative(fullPath);

        HostLog.Info(
            $"openPath mode={mode} class={FileExtensionPolicy.ToWireName(extensionClass)} path={displayPath}");

        return CommandResult.Success(new OpenPathResultDto(
            Opened: true,
            Mode: mode,
            Path: displayPath,
            AbsolutePath: fullPath,
            Extension: extension,
            ExtensionClass: FileExtensionPolicy.ToWireName(extensionClass),
            IsReparsePoint: isReparseTarget));
    }

    private static ProcessStartInfo BuildStartInfo(string fullPath, string mode, bool isDirectory)
    {
        if (mode == "open")
        {
            return new ProcessStartInfo
            {
                FileName = fullPath,
                UseShellExecute = true,
                Verb = "open"
            };
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "explorer.exe",
            UseShellExecute = true
        };

        if (isDirectory)
        {
            startInfo.ArgumentList.Add(fullPath);
        }
        else
        {
            // /select 与路径作为一个参数（ArgumentList 由 .NET 负责引号，禁止自行拼接 shell 字符串）。
            startInfo.ArgumentList.Add("/select," + fullPath);
        }

        return startInfo;
    }

    private static bool IsInsideSubmodule(IReadOnlySet<string> submodules, string relativePath)
    {
        if (relativePath.Length == 0)
        {
            return false;
        }

        foreach (var submodule in submodules)
        {
            if (relativePath.Equals(submodule, StringComparison.OrdinalIgnoreCase)
                || relativePath.StartsWith(submodule + "/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
