using System;
using MbdGitTool.Core.Contracts;
using MbdGitTool.Core.Files;

namespace MbdGitTool.Host.Services;

/// <summary>getHostInfo（code-plan §3.2 可选）：版本 / 特性开关 / 请求与树限制。</summary>
public sealed class HostInfoProvider
{
    private readonly string? _webView2Version;
    private readonly string _appVersion;

    public HostInfoProvider(string? webView2Version = null)
    {
        _webView2Version = webView2Version;
        _appVersion = typeof(HostInfoProvider).Assembly.GetName().Version?.ToString() ?? "0.0.0";
    }

    public HostInfoDto Get() => new(
        AppVersion: _appVersion,
        WebView2Version: _webView2Version,
        Features: new HostFeaturesDto(
            FileTree: true,
            WorkspaceTree: true,
            OpenPath: true,
            RepoActions: true,  // M2：单仓操作已接入
            Ci: false,          // v1 Mock 占位
            ComponentDetail: true,
            RemoteRefs: true,       // M2 P0-7：CHKOUT 选择器数据源
            RepoActionStash: true,  // M2：CHKOUT args.stash=true
            CommitStageAll: true),  // M2：COMMIT args.stageAll=true（需 confirm）
        Limits: new HostLimitsDto(
            MaxDirectoryEntries: FileTreeReader.DefaultLimit,
            MaxDepth: 2,
            MaxRequestBodyBytes: 64 * 1024,
            MaxResponseBytes: 2 * 1024 * 1024,
            MaxCommitsPerPage: CommitsService.MaxTake));
}
