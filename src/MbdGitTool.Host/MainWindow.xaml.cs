using System;
using System.IO;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace MbdGitTool.Host;

public partial class MainWindow : Window
{
    private const string VirtualHost = "app.local";

    private static readonly string LogDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MbdGitTool", "logs");

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Log("host loaded");
        try
        {
            var env = await CoreWebView2Environment.CreateAsync();
            await Web.EnsureCoreWebView2Async(env);
            Log($"webview2 initialized: {env.BrowserVersionString}");

            var uiRoot = Path.Combine(AppContext.BaseDirectory, "ui");
            if (!Directory.Exists(uiRoot) || !File.Exists(Path.Combine(uiRoot, "index.html")))
            {
                Log($"ERROR ui root missing: {uiRoot}");
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
            Web.Source = new Uri($"https://{VirtualHost}/index.html");
            Log($"navigate: https://{VirtualHost}/index.html (uiRoot={uiRoot})");
        }
        catch (Exception ex)
        {
            Log("ERROR " + ex);
            RuntimeMissing.Visibility = Visibility.Visible;
            RuntimeMissingDetail.Text =
                "WebView2 初始化失败：\n" + ex.Message + "\n\n请安装 WebView2 Evergreen Runtime 后重试。";
        }
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        Log($"navigation completed: success={e.IsSuccess} status={e.WebErrorStatus}");
    }

    private void OnProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
    {
        Log($"webview2 process failed: {e.ProcessFailedKind} {e.Reason}");
    }

    private static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(LogDir);
            File.AppendAllText(
                Path.Combine(LogDir, "host-m0.log"),
                $"{DateTimeOffset.UtcNow:O} {message}{Environment.NewLine}");
        }
        catch
        {
            // M0：日志失败不阻断启动。
        }
    }
}
