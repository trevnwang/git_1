using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using MbdGitTool.Core.Contracts;
using MbdGitTool.Host.Services;

namespace MbdGitTool.Host.Bridge;

/// <summary>
/// UI ↔ Host 桥（M1，SRS §12.2 同进程 WebView2 WebMessage）——薄适配器：
/// 来源校验（只接受 <c>app.local</c>）→ 请求体上限 → JSON 解析 → <see cref="HostCommandRouter"/> 分发 → 统一信封回写。
/// <para>请求：<c>{ id, method, params? }</c>；成功 <c>{ id, ok:true, data }</c>；</para>
/// <para>失败 <c>{ id, ok:false, error, code, details? }</c>（<c>error</c> 保留人类可读文案，旧前端读取不破）；</para>
/// <para>异步不阻塞 UI 线程；回写必须在 UI 线程（<c>ConfigureAwait(true)</c>）；桥销毁取消全部请求。</para>
/// </summary>
public sealed class WebViewBridge : IDisposable
{
    private const string VirtualHost = "app.local";
    private const int MaxRequestBodyBytes = HostCommandRouter.MaxRequestBodyBytes;
    private const int MaxResponseBytes = HostCommandRouter.MaxResponseBytes;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly CoreWebView2 _core;
    private readonly HostCommandRouter _router;
    private readonly CancellationTokenSource _lifetime = new();
    private bool _attached;
    private bool _disposed;

    public WebViewBridge(CoreWebView2 core, HostCommandRouter router)
    {
        _core = core ?? throw new ArgumentNullException(nameof(core));
        _router = router ?? throw new ArgumentNullException(nameof(router));
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
        HostLog.Info("WebView 桥已挂接（app.local / getSnapshot / refresh / 新工作台只读 API）");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _lifetime.Cancel();

        if (_attached)
        {
            _core.WebMessageReceived -= OnWebMessageReceived;
            _attached = false;
        }
    }

    /// <summary>
    /// M2b：把作业事件（固定形状 <c>{event:"JOB_UPDATED", data:JobDto}</c>）推给 UI。
    /// <para>必须在 UI 线程调用（WebView2 <c>PostWebMessageAsJson</c> 要求）；由
    /// <c>MainWindow</c> 在 <see cref="HostCommandRouter.EventSink"/> 中用 Dispatcher 调度。</para>
    /// <para>序列化失败/超长/桥已销毁均只告警丢弃，不影响后台作业。</para>
    /// </summary>
    public void PostEvent(object payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (_disposed)
        {
            return;
        }

        string json;
        try
        {
            json = JsonSerializer.Serialize(payload, JsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
            HostLog.Warn($"作业事件序列化失败：{ex.GetType().Name}");
            return;
        }

        if (json.Length > MaxResponseBytes)
        {
            HostLog.Warn($"作业事件超过上限（{json.Length} 字节），已丢弃");
            return;
        }

        _ = PostJsonAsync(json);
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

        string rawJson;
        try
        {
            rawJson = e.WebMessageAsJson;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            HostLog.Warn($"WebMessage 读取失败：{ex.GetType().Name}");
            return;
        }

        if (rawJson.Length > MaxRequestBodyBytes)
        {
            await PostResultAsync(
                    null,
                    CommandResult.Failure(
                        BridgeErrorCodes.InvalidRequest,
                        $"请求体超过上限（{MaxRequestBodyBytes} 字节）"))
                .ConfigureAwait(true);
            return;
        }

        JsonElement? id = null;
        string? method = null;
        JsonElement? parameters = null;

        try
        {
            using var document = JsonDocument.Parse(rawJson);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                await PostResultAsync(null, CommandResult.Failure(
                        BridgeErrorCodes.InvalidRequest,
                        "消息必须是 JSON 对象"))
                    .ConfigureAwait(true);
                return;
            }

            if (root.TryGetProperty("id", out var idElement))
            {
                id = idElement.Clone();
            }

            method = root.TryGetProperty("method", out var methodElement)
                && methodElement.ValueKind == JsonValueKind.String
                    ? methodElement.GetString()
                    : null;

            if (root.TryGetProperty("params", out var paramsElement)
                && paramsElement.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
            {
                parameters = paramsElement.Clone();
            }
        }
        catch (JsonException)
        {
            await PostResultAsync(null, CommandResult.Failure(
                    BridgeErrorCodes.InvalidRequest,
                    "请求不是合法 JSON"))
                .ConfigureAwait(true);
            return;
        }

        CommandResult result;
        try
        {
            result = await _router.DispatchAsync(method, parameters, _lifetime.Token).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            // 路由内部已兜底，这里只防御性处理（取消/桥销毁）。
            HostLog.Warn($"WebMessage 分发异常：{ex.GetType().Name}");
            result = CommandResult.Failure(BridgeErrorCodes.Internal, "宿主内部错误");
        }

        await PostResultAsync(id, result).ConfigureAwait(true);
    }

    private Task PostResultAsync(JsonElement? id, CommandResult result)
    {
        var reply = new BridgeReply
        {
            Id = id,
            Ok = result.Ok,
            Data = result.Ok ? result.Data : null,
            Error = result.Ok ? null : result.Error?.Message,
            Code = result.Ok ? null : result.Error?.Code,
            Details = result.Ok ? null : result.Error?.Details
        };

        string json;
        try
        {
            json = JsonSerializer.Serialize(reply, JsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
            HostLog.Error($"WebMessage 应答序列化失败：{ex.GetType().Name}");
            json = JsonSerializer.Serialize(
                new BridgeReply
                {
                    Id = id,
                    Ok = false,
                    Error = "应答序列化失败",
                    Code = BridgeErrorCodes.Internal
                },
                JsonOptions);
        }

        if (json.Length > MaxResponseBytes)
        {
            HostLog.Warn($"WebMessage 应答超过上限：method data 大小 {json.Length} 字节");
            json = JsonSerializer.Serialize(
                new BridgeReply
                {
                    Id = id,
                    Ok = false,
                    Error = "应答超过大小上限（2MB），请降低 limit/分页后重试",
                    Code = BridgeErrorCodes.ResponseTooLarge
                },
                JsonOptions);
        }

        return PostJsonAsync(json);
    }

    private Task PostJsonAsync(string json)
    {
        if (_disposed)
        {
            return Task.CompletedTask;
        }

        try
        {
            _core.PostWebMessageAsJson(json);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or ObjectDisposedException)
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

    /// <summary>桥应答信封（camelCase：id/ok/data/error/code/details；成功时不输出 error/code/details）。</summary>
    private sealed class BridgeReply
    {
        public JsonElement? Id { get; init; }

        public bool Ok { get; init; }

        public object? Data { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Error { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Code { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object? Details { get; init; }
    }
}
