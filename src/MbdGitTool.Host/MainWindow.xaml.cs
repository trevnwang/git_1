using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using MbdGitTool.Host.Bridge;
using MbdGitTool.Host.Services;

namespace MbdGitTool.Host;

public partial class MainWindow : Window
{
    private const string VirtualHost = "app.local";

    private WebViewBridge? _bridge;
    private HostCommandRouter? _router;
    private SnapshotService? _snapshots;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        HostLog.Info("host loaded");
        try
        {
            // 1) 配置：config.json + 环境变量覆盖；日志只输出脱敏描述。
            var config = AppConfig.Load();
            if (config.LoadError is not null)
            {
                HostLog.Warn(config.LoadError);
            }

            HostLog.Info($"配置加载完成：{config}");

            // 2) 快照服务（本地扫描 + 可选 Gitea 只读同步）。
            _snapshots = new SnapshotService(config);

            var env = await CoreWebView2Environment.CreateAsync();
            await Web.EnsureCoreWebView2Async(env);
            HostLog.Info($"webview2 initialized: {env.BrowserVersionString}");

            // 3) 命令路由（新工作台只读 API：树/详情/提交/打开/操作占位/CI 占位/HostInfo）。
            _router = new HostCommandRouter(
                config,
                _snapshots,
                webView2Version: env.BrowserVersionString);

            var uiRoot = Path.Combine(AppContext.BaseDirectory, "ui");
            if (!Directory.Exists(uiRoot) || !File.Exists(Path.Combine(uiRoot, "index.html")))
            {
                HostLog.Error($"ui root missing: {uiRoot}");
                RuntimeMissing.Visibility = Visibility.Visible;
                RuntimeMissingDetail.Text = $"未找到前端产物：{uiRoot}\n请先执行 code/ui 下的 npm run build。";
                return;
            }

            Web.CoreWebView2.SetVirtualHostNameToFolderMapping(
                VirtualHost, uiRoot, CoreWebView2HostResourceAccessKind.Allow);
            Web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            Web.CoreWebView2.Settings.AreDevToolsEnabled = true;
            Web.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
            Web.CoreWebView2.ProcessFailed += OnProcessFailed;

            // 3) UI 桥：WebView2 初始化后挂接，路由 getSnapshot/refresh 与工作台只读 API。
            _bridge = new WebViewBridge(Web.CoreWebView2, _router);
            _bridge.Attach();

            // M2b：作业进度事件（JOB_UPDATED）由后台 worker 产生 → Dispatcher 回 UI 线程 → 桥推送。
            var bridge = _bridge;
            _router.EventSink = payload =>
            {
                if (Dispatcher.HasShutdownStarted)
                {
                    return;
                }

                _ = Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (Dispatcher.HasShutdownStarted)
                    {
                        return;
                    }

                    bridge.PostEvent(payload);
                }));
            };

            Web.Source = new Uri($"https://{VirtualHost}/index.html");
            HostLog.Info($"navigate: https://{VirtualHost}/index.html (uiRoot={uiRoot})");

            // UI 首次 getSnapshot 之前预热一次，避免首屏等待 git 扫描（失败不影响 UI，桥会回 error）。
            _ = WarmupSnapshotAsync();
        }
        catch (Exception ex)
        {
            HostLog.Error("ERROR " + ex);
            RuntimeMissing.Visibility = Visibility.Visible;
            RuntimeMissingDetail.Text =
                "WebView2 初始化失败：\n" + ex.Message + "\n\n请安装 WebView2 Evergreen Runtime 后重试。";
        }
    }

    private async Task WarmupSnapshotAsync()
    {
        if (_snapshots is null)
        {
            return;
        }

        try
        {
            await _snapshots.RefreshAsync();
        }
        catch (Exception ex)
        {
            HostLog.Warn($"快照预热失败：{ex.GetType().Name}: {ex.Message}");
        }
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e) =>
        HostLog.Info($"navigation completed: success={e.IsSuccess} status={e.WebErrorStatus}");

    private void OnProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e) =>
        HostLog.Info($"webview2 process failed: {e.ProcessFailedKind} {e.Reason}");

    protected override void OnClosed(EventArgs e)
    {
        _bridge?.Dispose();
        _router?.Dispose();
        _snapshots?.Dispose();
        base.OnClosed(e);
    }
}
