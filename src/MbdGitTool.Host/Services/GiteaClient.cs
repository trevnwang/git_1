using System.Net.Http;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace MbdGitTool.Host.Services;

/// <summary>Gitea 组织/用户仓库（只读清单的最小字段，FR-SYNC-01）。</summary>
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
    public GiteaApiException(int statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }

    public GiteaApiException(int statusCode, string message, Exception inner) : base(message, inner)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
}

/// <summary>
/// Gitea 只读仓库清单客户端（M1）。
/// <para>所有者发现降级链：<c>/orgs/{owner}/repos</c> → <c>/users/{owner}/repos</c> → <c>/repos/search?q={owner}</c>。</para>
/// <para>原因：部分 PAT 只有 <c>write:repository</c> scope，访问 org/user 清单端点会 403；
/// search 端点在同一 scope 下可用（需精确过滤 owner）。所有降级只在 403/404 时发生，401/429/5xx 直接抛错。</para>
/// <para>安全：token 只随 Authorization header 发送，禁止进 URL/日志/命令行。</para>
/// </summary>
public sealed class GiteaClient
{
    private const int PageSize = 50;
    private const int MaxPages = 200;

    private static readonly HttpClient SharedHttp = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    private readonly HttpClient _http;
    private readonly Uri _apiBase;
    private readonly string _owner;
    private readonly string? _token;

    public GiteaClient(string baseUrl, string owner, string? token, HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);

        _owner = owner.Trim();
        _token = string.IsNullOrWhiteSpace(token) ? null : token.Trim();
        _http = httpClient ?? SharedHttp;

        var normalized = baseUrl.Trim().TrimEnd('/');
        if (!Uri.TryCreate(normalized + "/api/v1/", UriKind.Absolute, out var apiBase)
            || (apiBase.Scheme != Uri.UriSchemeHttps && apiBase.Scheme != Uri.UriSchemeHttp))
        {
            throw new ArgumentException("Gitea BaseUrl 不是合法的 HTTP(S) 地址。", nameof(baseUrl));
        }

        _apiBase = apiBase;
    }

    /// <summary>最近一次成功使用的所有者发现策略（日志/诊断用，不含 token）。</summary>
    public GiteaOwnerDiscovery LastDiscovery { get; private set; } = GiteaOwnerDiscovery.Unknown;

    /// <summary>
    /// 拉全 owner 的仓库清单；按 org → user → search 降级。失败抛 <see cref="GiteaApiException"/> 或网络异常。
    /// </summary>
    public async Task<IReadOnlyList<GiteaRepo>> GetOwnerReposAsync(CancellationToken cancellationToken = default)
    {
        var errors = new List<GiteaApiException>();

        foreach (var strategy in new[] { GiteaOwnerDiscovery.Organization, GiteaOwnerDiscovery.User })
        {
            var prefix = strategy == GiteaOwnerDiscovery.Organization ? "orgs" : "users";
            try
            {
                var repos = await GetPagedAsync(
                    page => $"{prefix}/{Uri.EscapeDataString(_owner)}/repos?page={page}&limit={PageSize}",
                    cancellationToken).ConfigureAwait(false);
                LastDiscovery = strategy;
                return repos;
            }
            catch (GiteaApiException ex) when (ex.StatusCode is 403 or 404)
            {
                errors.Add(ex);
            }
        }

        try
        {
            var repos = await SearchReposAsync(cancellationToken).ConfigureAwait(false);
            LastDiscovery = GiteaOwnerDiscovery.Search;
            return repos;
        }
        catch (GiteaApiException ex)
        {
            errors.Add(ex);
            var detail = string.Join("；", errors.Select(e => e.Message));
            throw new GiteaApiException(
                ex.StatusCode,
                $"无法读取仓库清单（org/user/search 均失败）：{detail}",
                ex);
        }
    }

    /// <summary>兼容旧调用。</summary>
    public Task<IReadOnlyList<GiteaRepo>> GetOrgReposAsync(CancellationToken cancellationToken = default) =>
        GetOwnerReposAsync(cancellationToken);

    private async Task<IReadOnlyList<GiteaRepo>> GetPagedAsync(
        Func<int, string> pathBuilder,
        CancellationToken cancellationToken)
    {
        var repos = new List<GiteaRepo>();
        for (var page = 1; page <= MaxPages; page++)
        {
            var pageRepos = await GetPageAsync(pathBuilder(page), parseSearchEnvelope: false, cancellationToken)
                .ConfigureAwait(false);
            repos.AddRange(pageRepos);

            if (pageRepos.Count < PageSize)
            {
                return repos;
            }
        }

        throw new GiteaApiException(0, $"Gitea 仓库清单分页超过上限（{MaxPages} 页），拒绝继续以防无限循环。");
    }

    private async Task<IReadOnlyList<GiteaRepo>> SearchReposAsync(CancellationToken cancellationToken)
    {
        var repos = new List<GiteaRepo>();
        for (var page = 1; page <= MaxPages; page++)
        {
            var pageRepos = await GetPageAsync(
                $"repos/search?q={Uri.EscapeDataString(_owner)}&sort=updated&order=desc&page={page}&limit={PageSize}",
                parseSearchEnvelope: true,
                cancellationToken).ConfigureAwait(false);

            // search 可能返回其它 owner 的仓库：只接受 owner 精确匹配（大小写不敏感）。
            repos.AddRange(pageRepos.Where(r => MatchesOwner(r.FullName)));
            if (pageRepos.Count < PageSize)
            {
                return repos;
            }
        }

        throw new GiteaApiException(0, $"Gitea 仓库搜索分页超过上限（{MaxPages} 页），拒绝继续以防无限循环。");
    }

    private bool MatchesOwner(string fullName)
    {
        var slash = fullName.IndexOf('/');
        return slash > 0 && string.Equals(fullName[..slash], _owner, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<IReadOnlyList<GiteaRepo>> GetPageAsync(
        string relativePath,
        bool parseSearchEnvelope,
        CancellationToken cancellationToken)
    {
        var uri = new Uri(_apiBase, relativePath);
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (_token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("token", _token);
        }

        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var code = (int)response.StatusCode;
            throw new GiteaApiException(code, DescribeStatus(code));
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var array = document.RootElement;
        if (parseSearchEnvelope)
        {
            if (array.ValueKind != JsonValueKind.Object
                || !array.TryGetProperty("data", out var data)
                || data.ValueKind != JsonValueKind.Array)
            {
                throw new GiteaApiException(0, "Gitea 仓库搜索响应格式非法（期望 { data: [] }）。");
            }

            array = data;
        }
        else if (array.ValueKind != JsonValueKind.Array)
        {
            throw new GiteaApiException(0, "Gitea 仓库清单响应格式非法（期望 JSON 数组）。");
        }

        return ParseRepos(array);
    }

    private List<GiteaRepo> ParseRepos(JsonElement array)
    {
        var result = new List<GiteaRepo>();
        foreach (var element in array.EnumerateArray())
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

            // full_name 形如 `owner/repo`；组件解析必须用不带 owner 前缀的 name（SRS §6.1）。
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
                fullName = $"{_owner}/{name}";
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
        (int)HttpStatusCode.Forbidden => "Gitea 权限不足（403）：PAT scope 不足（需要 read:repository / read:organization 或 write:repository）。",
        (int)HttpStatusCode.TooManyRequests => "Gitea 限流（429）：请求过于频繁，请稍后重试。",
        (int)HttpStatusCode.NotFound => "Gitea 所有者不存在（404）。",
        >= 500 => $"Gitea 服务端错误（{statusCode}）。",
        _ => $"Gitea 请求失败（{statusCode}）。"
    };
}

/// <summary>owner 仓库清单的发现策略（org → user → search 降级）。</summary>
public enum GiteaOwnerDiscovery
{
    Unknown = 0,
    Organization = 1,
    User = 2,
    Search = 3
}
