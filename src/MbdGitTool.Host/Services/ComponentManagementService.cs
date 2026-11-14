using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MbdGitTool.Core.Contracts;

namespace MbdGitTool.Host.Services;

/// <summary>
/// AC-OP-31/32 / AC-UI-16：组件纳入/移出（component_management 持久化 + 快照过滤）。
/// <para>纳入只读校验 Gitea 四仓；移出 STOP_MANAGE 只改管理状态；LOCAL_DELETE 复用 DELETE job（Router 负责入队）。</para>
/// </summary>
public sealed class ComponentManagementService
{
    private static readonly string[] RepoTypes = { "DESIGN", "IMPL", "MIL", "SIL" };
    private readonly AppConfig _config;
    private readonly AuditDatabase _database;

    public ComponentManagementService(AppConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _database = new AuditDatabase(config.DatabasePath);
        _database.Initialize();
    }

    public AuditDatabase Database => _database;

    public IReadOnlyList<AuditDatabase.ComponentManagementRecord> List() => _database.ListComponentManagement();

    /// <summary>纳入：远程只读校验四仓；缺仓允许纳入但标记 missingRepos。</summary>
    public async Task<(bool Ok, BridgeError? Error, IReadOnlyList<string> Missing, string? DisplayName)> BindAsync(
        string? componentId, string? displayName, CancellationToken cancellationToken)
    {
        var id = componentId?.Trim();
        if (string.IsNullOrWhiteSpace(id))
            return (false, new BridgeError(BridgeErrorCodes.InvalidArgument, "缺少 componentId"), Array.Empty<string>(), null);
        if (!_config.GiteaEnabled)
            return (false, new BridgeError(BridgeErrorCodes.GiteaConfigInvalid, "未配置 Gitea baseUrl/org，无法纳入组件"), Array.Empty<string>(), null);
        try
        {
            var client = new GiteaClient(_config.GiteaBaseUrl!, _config.GiteaOrg!, _config.GiteaToken);
            var repos = await client.GetOrgReposAsync(cancellationToken).ConfigureAwait(false);
            var expected = RepoTypes.ToDictionary(type => id + "_" + type, StringComparer.OrdinalIgnoreCase);
            var found = repos.Where(repo => expected.ContainsKey(repo.Name))
                .Select(repo => repo.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var missing = expected.Keys.Where(name => !found.Contains(name)).OrderBy(name => name, StringComparer.Ordinal).ToList();
            if (found.Count == 0)
                return (false, new BridgeError(BridgeErrorCodes.RemoteNotFound, $"远程不存在组件 {id} 的任何四仓"), missing, displayName);
            var ok = _database.UpsertComponentManagement(new AuditDatabase.ComponentManagementRecord(
                id, displayName?.Trim(), IsManaged: true, IsDeleted: false,
                UpdatedAt: DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)));
            if (!ok)
                return (false, new BridgeError(BridgeErrorCodes.Internal, "组件管理状态写入失败"), missing, displayName);
            return (true, null, missing, displayName);
        }
        catch (GiteaApiException ex)
        {
            var code = ex.StatusCode is 401 or 403 ? BridgeErrorCodes.AuthFailed : BridgeErrorCodes.RemoteNotFound;
            return (false, new BridgeError(code, $"Gitea 校验失败（HTTP {ex.StatusCode}）"), Array.Empty<string>(), displayName);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return (false, new BridgeError(BridgeErrorCodes.Network, $"Gitea 不可达：{ex.GetType().Name}"), Array.Empty<string>(), displayName);
        }
    }

    /// <summary>仅停止管理：保留目录/repo 行，默认视图过滤。</summary>
    public (bool Ok, BridgeError? Error) StopManage(string? componentId)
    {
        var id = componentId?.Trim();
        if (string.IsNullOrWhiteSpace(id))
            return (false, new BridgeError(BridgeErrorCodes.InvalidArgument, "缺少 componentId"));
        var ok = _database.UpsertComponentManagement(new AuditDatabase.ComponentManagementRecord(
            id, null, IsManaged: false, IsDeleted: false,
            UpdatedAt: DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)));
        return ok ? (true, null) : (false, new BridgeError(BridgeErrorCodes.Internal, "组件管理状态写入失败"));
    }

    /// <summary>默认视图：只保留 is_managed=1 且 is_deleted=0；无记录视为默认管理（兼容旧安装）。</summary>
    public IReadOnlyList<string> HiddenComponentIds()
    {
        var rows = _database.ListComponentManagement();
        return rows.Where(row => !row.IsManaged || row.IsDeleted)
            .Select(row => row.ComponentId).ToHashSet(StringComparer.OrdinalIgnoreCase).ToList();
    }
}
