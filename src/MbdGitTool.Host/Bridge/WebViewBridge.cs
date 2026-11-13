using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using MbdGitTool.Core.Contracts;
using MbdGitTool.Host.Services;

namespace MbdGitTool.Host.Bridge;

/// <summary>
/// UI ↔ Host 桥（M1，SRS §12.2 同进程 WebView2 WebMessage）。
/// <para>请求：<c>{ id, method: "getSnapshot" | "refresh" }</c>；</para>
/// <para>应答：<c>{ id, ok: true, data: SnapshotDto }</c> 或 <c>{ id, ok: false, error }</c>，camelCase 序列化；</para>
/// <para>安全：只接受来自虚拟主机 <c>app.local</c> 的消息。</para>
/// </summary>
public sealed class WebViewBridge : IDisposable
{
    private const string VirtualHost = "app.local";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly CoreWebView2 _core;
    private readonly SnapshotService _snapshots;
    private bool _attached;
    private bool _disposed;

    public WebViewBridge(CoreWebView2 core, SnapshotService snapshots)
    {
        _core = core ?? throw new ArgumentNullException(nameof(core));
        _snapshots = snapshots ?? throw new ArgumentNullException(nameof(snapshots));
    }

    /// <summary>在 WebView2 初始化完成后挂接消息处理（重复调用幂等）。</summary>
    public void Attach()
    {
        if (_attached)
        {
            return;
        }

        _core.WebMessageReceived += OnWebMessageReceived;
        _attached = true;
        HostLog.Info("WebView 桥已挂接（app.local / getSnapshot / refresh）");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_attached)
        {
            _core.WebMessageReceived -= OnWebMessageReceived;
            _attached = false;
        }
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        // WebView2 事件在 UI 线程；异步处理失败必须自吞并回错误应答，避免 async void 崩溃宿主。
        _ = HandleMessageAsync(e);
    }

    private async Task HandleMessageAsync(CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (!IsAppLocalSource(e.Source))
        {
            HostLog.Warn($"忽略非 app.local 的 WebMessage 来源：{e.Source}");
            return;
        }

        JsonElement? id = null;
        try
        {
            using var document = JsonDocument.Parse(e.WebMessageAsJson);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                await PostErrorAsync(null, "消息必须是 JSON 对象").ConfigureAwait(true);
                return;
            }

            if (root.TryGetProperty("id", out var idElement))
            {
                id = idElement.Clone();
            }

            var method = root.TryGetProperty("method", out var methodElement)
                ? methodElement.GetString()
                : null;

            switch (method)
            {
                case "getSnapshot":
                {
                    HostLog.Info("WebMessage: getSnapshot");
                    var snapshot = await _snapshots.GetSnapshotAsync().ConfigureAwait(true);
                    await PostSnapshotAsync(id, snapshot).ConfigureAwait(true);
                    break;
                }

                case "refresh":
                {
                    HostLog.Info("WebMessage: refresh");
                    var snapshot = await _snapshots.RefreshAsync().ConfigureAwait(true);
                    await PostSnapshotAsync(id, snapshot).ConfigureAwait(true);
                    break;
                }

                default:
                    HostLog.Warn($"WebMessage: 未知方法 {method ?? "<null>"}");
                    await PostErrorAsync(id, $"未知方法：{method ?? "<null>"}").ConfigureAwait(true);
                    break;
            }
        }
        catch (Exception ex)
        {
            HostLog.Error($"WebMessage 处理失败：{ex.GetType().Name}: {ex.Message}");
            await PostErrorAsync(id, $"处理失败（{ex.GetType().Name}）：{ex.Message}").ConfigureAwait(true);
        }
    }

    private Task PostSnapshotAsync(JsonElement? id, SnapshotDto snapshot)
    {
        var response = new BridgeResponse<SnapshotDto>(id, true, snapshot, null);
        return PostJsonAsync(JsonSerializer.Serialize(response, JsonOptions));
    }

    private Task PostErrorAsync(JsonElement? id, string error)
    {
        var response = new BridgeResponse<SnapshotDto>(id, false, null, error);
        return PostJsonAsync(JsonSerializer.Serialize(response, JsonOptions));
    }

    private Task PostJsonAsync(string json)
    {
        try
        {
            _core.PostWebMessageAsJson(json);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            // WebView2 已销毁/导航中：丢弃应答即可，避免影响宿主。
            HostLog.Warn($"WebMessage 回写失败：{ex.GetType().Name}");
        }

        return Task.CompletedTask;
    }

    private static bool IsAppLocalSource(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        return Uri.TryCreate(source, UriKind.Absolute, out var uri)
            && uri.Host.Equals(VirtualHost, StringComparison.OrdinalIgnoreCase)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
    }

    /// <summary>桥应答信封（camelCase：id/ok/data/error）。</summary>
    private sealed record BridgeResponse<T>(JsonElement? Id, bool Ok, T? Data, string? Error);
}
