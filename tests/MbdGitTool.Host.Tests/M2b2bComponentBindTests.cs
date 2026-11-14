using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MbdGitTool.Host.Services;
using Xunit;

namespace MbdGitTool.Host.Tests;

/// <summary>G-WIRE-REST：本地 Gitea REST 桩，记录 method/url/status（不记录 Authorization 值）。</summary>
public sealed class GWireRestServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _loop;
    public string BaseUrl { get; }
    public List<(string Method, string Url, int Status)> Records { get; } = new();

    public GWireRestServer()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        BaseUrl = $"http://127.0.0.1:{port}";
        _listener.Prefixes.Add(BaseUrl + "/");
        _listener.Start();
        _loop = Task.Run(LoopAsync);
    }

    private async Task LoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            HttpListenerContext context;
            try { context = await _listener.GetContextAsync().ConfigureAwait(false); }
            catch (HttpListenerException) { return; }
            catch (ObjectDisposedException) { return; }
            var request = context.Request;
            var response = context.Response;
            var url = request.Url?.PathAndQuery ?? string.Empty;
            try
            {
                if (url.StartsWith("/api/v1/orgs/RTCRepos/repos", StringComparison.Ordinal))
                {
                    const string json = """
                        [{"id":1,"name":"CF217A_Bind_DESIGN","full_name":"RTCRepos/CF217A_Bind_DESIGN","default_branch":"main","empty":false,"archived":false},
                         {"id":2,"name":"CF217A_Bind_IMPL","full_name":"RTCRepos/CF217A_Bind_IMPL","default_branch":"main","empty":false,"archived":false}]
                        """;
                    var bytes = Encoding.UTF8.GetBytes(json);
                    response.StatusCode = 200;
                    response.ContentType = "application/json";
                    response.ContentLength64 = bytes.Length;
                    await response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
                    Records.Add(("GET", url, 200));
                }
                else
                {
                    response.StatusCode = 404;
                    Records.Add((request.HttpMethod, url, 404));
                }
            }
            finally
            {
                response.Close();
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _listener.Stop();
        _listener.Close();
        try { _loop.Wait(TimeSpan.FromSeconds(2)); } catch { /* ignore */ }
    }
}

public sealed class M2b2bComponentBindTests
{
    [Fact]
    public async Task Bind_UsesGiteaRestAndReportsMissingRepos()
    {
        using var gwire = new GWireRestServer();
        using var workspace = new TempWorkspace();
        var config = new AppConfig
        {
            WorkspaceRoot = workspace.Root,
            GiteaBaseUrl = gwire.BaseUrl,
            GiteaOrg = "RTCRepos",
            GiteaToken = "test-token",
            AuditRoot = TestAudit.NewRoot(),
            JobsRoot = TestAudit.NewJobsRoot(),
            DatabasePath = TestAudit.NewDatabasePath(),
            ExportsRoot = TestAudit.NewExportsRoot(),
        };
        var service = new ComponentManagementService(config);
        var (ok, error, missing, _) = await service.BindAsync("CF217A_Bind", "Bind 测试", CancellationToken.None);

        Assert.True(ok, error?.Code);
        Assert.Equal(2, missing.Count);
        Assert.Contains("CF217A_Bind_MIL", missing);
        Assert.Contains("CF217A_Bind_SIL", missing);
        var rows = service.List();
        var row = Assert.Single(rows);
        Assert.Equal("CF217A_Bind", row.ComponentId);
        Assert.True(row.IsManaged);
        Assert.False(row.IsDeleted);
        Assert.NotEmpty(gwire.Records);
        Assert.All(gwire.Records, record => Assert.DoesNotContain("Authorization", record.Url, StringComparison.OrdinalIgnoreCase));
    }
}
