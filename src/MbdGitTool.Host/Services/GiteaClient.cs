using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MbdGitTool.Host.Services;

/// <summary>Gitea 组织仓库（只读清单的最小字段，FR-SYNC-01）。</summary>
public sealed record GiteaRepo(
    long Id,
    string Name,
    string FullName,
    string? DefaultBranch,
    bool Empty,
    bool Archived);

/// <summary>Gitea API 失败（401/403/429 等），携带 HTTP 状态码供 UI/日志分类。</summary>
public sealed class GiteaApiException : Exception
{
    public GiteaApiException(int statusCode, string message)
        : base(message) => StatusCode = statusCode;

    /// <summary>HTTP 状态码；0 表示非 HTTP 错误（如响应格式非法）。</summary>
    public int StatusCode { get; }
}

/// <summary>
/// Gitea 只读客户端（SRS §12.2 / FR-SYNC-01，M1 仅拉组织仓库清单）。
/// <para>仅 GET；<c>Authorization: token &lt;PAT&gt;</c>（D-006 唯一写法）；单请求 15s 超时；</para>
/// <para><b>禁止</b>打印 token / 响应头；异常只暴露状态码与安全文案。</para>
/// <para>无第三方包：<see cref="HttpClient"/> + System.Text.Json。</para>
/// </summary>
public sealed class GiteaClient
{
    private const int PageSize = 50;
    private const int MaxPages = 200;

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    private readonly Uri _apiBase;
    private readonly string _org;
    private readonly string? _token;

    public GiteaClient(string baseUrl, string org, string? token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(org);

        _org = org.Trim();
        _token = string.IsNullOrWhiteSpace(token) ? null : token.Trim();

        var normalized = baseUrl.Trim().TrimEnd('/');
        if (!Uri.TryCreate(normalized + "/api/v1/", UriKind.Absolute, out var apiBase)
            || (apiBase.Scheme != Uri.UriSchemeHttps && apiBase.Scheme != Uri.UriSchemeHttp))
        {
            throw new ArgumentException("Gitea BaseUrl 不是合法的 HTTP(S) 地址。", nameof(baseUrl));
        }

        _apiBase = apiBase;
    }

    /// <summary>
    /// 分页拉全组织仓库清单（每页 50，读到不满一页为止）。失败抛 <see cref="GiteaApiException"/> 或网络异常。
    /// </summary>
    public async Task<IReadOnlyList<GiteaRepo>> GetOrgReposAsync(CancellationToken cancellationToken = default)
    {
        var repos = new List<GiteaRepo>();
        for (var page = 1; page <= MaxPages; page++)
        {
            var pageRepos = await GetReposPageAsync(page, cancellationToken).ConfigureAwait(false);
            repos.AddRange(pageRepos);

            if (pageRepos.Count < PageSize)
            {
                return repos;
            }
        }

        throw new GiteaApiException(0, $"Gitea 仓库清单分页超过上限（{MaxPages} 页），拒绝继续以防无限循环。");
    }

    private async Task<IReadOnlyList<GiteaRepo>> GetReposPageAsync(int page, CancellationToken cancellationToken)
    {
        var uri = new Uri(
            _apiBase,
            string.Create(
                CultureInfo.InvariantCulture,
                $"orgs/{Uri.EscapeDataString(_org)}/repos?page={page}&limit={PageSize}"));

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (_token is not null)
        {
            // D-006：token 仅随 Authorization header 发送，禁止进 URL/命令行。
            request.Headers.Authorization = new AuthenticationHeaderValue("token", _token);
        }

        using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var code = (int)response.StatusCode;
            throw new GiteaApiException(code, DescribeStatus(code));
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new GiteaApiException(0, "Gitea 仓库清单响应格式非法（期望 JSON 数组）。");
        }

        var result = new List<GiteaRepo>();
        foreach (var element in document.RootElement.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var id = element.TryGetProperty("id", out var idElement) && idElement.TryGetInt64(out var idValue)
                ? idValue
                : 0;
            var fullName = element.TryGetProperty("full_name", out var fullNameElement)
                ? fullNameElement.GetString()
                : null;
            var name = element.TryGetProperty("name", out var nameElement)
                ? nameElement.GetString()
                : null;

            // full_name 形如 `org/repo`；组件解析必须用不带 org 前缀的 name（SRS §6.1）。
            if (string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(fullName))
            {
                var slash = fullName.LastIndexOf('/');
                name = slash >= 0 ? fullName[(slash + 1)..] : fullName;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(fullName))
            {
                fullName = $"{_org}/{name}";
            }

            result.Add(new GiteaRepo(
                Id: id,
                Name: name,
                FullName: fullName,
                DefaultBranch: element.TryGetProperty("default_branch", out var branch) ? branch.GetString() : null,
                Empty: element.TryGetProperty("empty", out var empty) && empty.ValueKind == JsonValueKind.True,
                Archived: element.TryGetProperty("archived", out var archived) && archived.ValueKind == JsonValueKind.True));
        }

        return result;
    }

    private static string DescribeStatus(int statusCode) => statusCode switch
    {
        (int)HttpStatusCode.Unauthorized => "Gitea 认证失败（401）：PAT 无效或已过期。",
        (int)HttpStatusCode.Forbidden => "Gitea 权限不足（403）：PAT 缺少 read:repository 权限。",
        (int)HttpStatusCode.TooManyRequests => "Gitea 限流（429）：请求过于频繁，请稍后重试。",
        (int)HttpStatusCode.NotFound => "Gitea 组织不存在（404）。",
        >= 500 => $"Gitea 服务端错误（{statusCode}）。",
        _ => $"Gitea 请求失败（{statusCode}）。"
    };
}
