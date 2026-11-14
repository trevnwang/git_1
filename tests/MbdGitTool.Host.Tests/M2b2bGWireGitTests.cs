using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace MbdGitTool.Host.Tests;

/// <summary>G-WIRE-GIT：本地 git smart-HTTP 代理，记录 method/path/status；不记录 Authorization 值。</summary>
public sealed class GWireGitServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _loop;
    private readonly string _projectRoot;
    public string BaseUrl { get; }
    public List<(string Method, string Path, int Status)> Records { get; } = new();

    public GWireGitServer(string projectRoot)
    {
        _projectRoot = projectRoot;
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
            try
            {
                var body = new MemoryStream();
                if (request.HasEntityBody) await request.InputStream.CopyToAsync(body).ConfigureAwait(false);
                var bodyBytes = body.ToArray();
                var (status, headers, output) = RunGitBackend(request, bodyBytes);
                response.StatusCode = status;
                foreach (var (name, value) in headers)
                {
                    if (name.Equals("Status", StringComparison.OrdinalIgnoreCase)) continue;
                    response.Headers[name] = value;
                }
                response.ContentLength64 = output.Length;
                await response.OutputStream.WriteAsync(output).ConfigureAwait(false);
                Records.Add((request.HttpMethod, request.Url?.AbsolutePath ?? string.Empty, status));
            }
            catch (Exception)
            {
                response.StatusCode = 500;
                Records.Add((request.HttpMethod, request.Url?.AbsolutePath ?? string.Empty, 500));
            }
            finally
            {
                response.Close();
            }
        }
    }

    private (int Status, List<(string Name, string Value)> Headers, byte[] Body) RunGitBackend(HttpListenerRequest request, byte[] body)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = "http-backend",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.Environment["GIT_PROJECT_ROOT"] = _projectRoot;
        startInfo.Environment["GIT_HTTP_EXPORT_ALL"] = "1";
        startInfo.Environment["PATH_INFO"] = request.Url?.AbsolutePath ?? "/";
        startInfo.Environment["QUERY_STRING"] = request.Url?.Query.TrimStart('?') ?? string.Empty;
        startInfo.Environment["REQUEST_METHOD"] = request.HttpMethod;
        startInfo.Environment["CONTENT_TYPE"] = request.ContentType ?? string.Empty;
        startInfo.Environment["CONTENT_LENGTH"] = body.Length.ToString(System.Globalization.CultureInfo.InvariantCulture);
        startInfo.Environment["REMOTE_ADDR"] = "127.0.0.1";
        startInfo.Environment["REMOTE_USER"] = string.Empty;

        using var process = Process.Start(startInfo)!;
        using (var stdin = process.StandardInput.BaseStream)
        {
            stdin.Write(body, 0, body.Length);
        }
        var output = new MemoryStream();
        process.StandardOutput.BaseStream.CopyTo(output);
        _ = process.StandardError.ReadToEnd();
        process.WaitForExit(10_000);

        var raw = output.ToArray();
        var separator = IndexOf(raw, new byte[] { 13, 10, 13, 10 });
        var headerLength = separator >= 0 ? separator : IndexOf(raw, new byte[] { 10, 10 });
        var headerText = headerLength > 0 ? Encoding.ASCII.GetString(raw, 0, headerLength) : string.Empty;
        var bodyStart = headerLength >= 0 ? headerLength + (raw[headerLength] == 13 ? 4 : 2) : 0;
        var payload = raw[bodyStart..];
        var headers = new List<(string, string)>();
        var status = 200;
        foreach (var line in headerText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
        {
            var colon = line.IndexOf(':');
            if (colon <= 0) continue;
            var name = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim();
            if (name.Equals("Status", StringComparison.OrdinalIgnoreCase))
            {
                var code = value.Split(' ')[0];
                int.TryParse(code, out status);
            }
            headers.Add((name, value));
        }
        return (status, headers, payload);
    }

    private static int IndexOf(byte[] data, byte[] pattern)
    {
        for (var i = 0; i <= data.Length - pattern.Length; i++)
        {
            var match = true;
            for (var j = 0; j < pattern.Length; j++)
            {
                if (data[i + j] != pattern[j]) { match = false; break; }
            }
            if (match) return i;
        }
        return -1;
    }

    public void Dispose()
    {
        _cts.Cancel();
        _listener.Stop();
        _listener.Close();
        try { _loop.Wait(TimeSpan.FromSeconds(2)); } catch { /* ignore */ }
    }
}

public sealed class M2b2bGWireGitTests
{
    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "MbdGitTool.sln"))) return current.FullName;
            current = current.Parent;
        }
        throw new InvalidOperationException("找不到仓库根");
    }

    [Fact]
    public async Task GWireGit_CloneThroughProxy_RecordsSmartHttpWithoutCredentials()
    {
        using var root = new TempWorkspace();
        var bare = Path.Combine(root.Root, "repo.git");
        TestGit.Run(root.Root, "init", "--bare", "-q", "--initial-branch=main", bare);
        TestGit.Run(bare, "config", "http.receivepack", "true");
        var seedRoot = Path.Combine(Path.GetTempPath(), "MbdGitToolTests", "gwire-seed-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(seedRoot);
        try
        {
            TestGit.Run(seedRoot, "clone", "-q", bare, "seed");
            var seed = Path.Combine(seedRoot, "seed");
            TestGit.ConfigureIdentity(seed);
            File.WriteAllText(Path.Combine(seed, "README.md"), "seed");
            TestGit.CommitAll(seed, "seed");
            TestGit.Run(seed, "push", "-q", "-u", "origin", "main");
        }
        finally
        {
            try { Directory.Delete(seedRoot, recursive: true); } catch { /* ignore */ }
        }

        using var gwire = new GWireGitServer(root.Root);
        var clone = Path.Combine(root.Root, "clone-" + Guid.NewGuid().ToString("N"));
        TestGit.Run(root.Root, "clone", "-q", gwire.BaseUrl + "/repo.git", clone);
        Assert.True(Directory.Exists(Path.Combine(clone, ".git")));
        // 真实 push 也走 G-WIRE（验证远端写请求可被记录，且记录不含凭据）。
        TestGit.ConfigureIdentity(clone);
        File.WriteAllText(Path.Combine(clone, "push.txt"), "x");
        TestGit.Run(clone, "add", "push.txt");
        TestGit.Run(clone, "commit", "-q", "-m", "push");
        TestGit.Run(clone, "push", "-q", "origin", "main");
        Assert.NotEmpty(gwire.Records);
        Assert.Contains(gwire.Records, record => record.Method == "GET");
        Assert.Contains(gwire.Records, record => record.Method == "POST");

        if (Environment.GetEnvironmentVariable("MBD_WRITE_GWIRE_EVIDENCE") == "1")
        {
            var evidenceDir = Path.Combine(FindRepoRoot(), "docs", "evidence", "security");
            Directory.CreateDirectory(evidenceDir);
            File.WriteAllLines(Path.Combine(evidenceDir, "gwire.jsonl"), gwire.Records.Select(record => JsonSerializer.Serialize(new { record.Method, record.Path, record.Status })));
            File.WriteAllText(Path.Combine(evidenceDir, "argv.txt"), $"git clone {gwire.BaseUrl}/repo.git <tmp>\ngit push origin main (via G-WIRE proxy)\n# no token/Authorization recorded\n");
            File.WriteAllText(Path.Combine(evidenceDir, "remotes.txt"), TestGit.Capture(clone, "remote", "-v") + Environment.NewLine);
        }
        await Task.CompletedTask;
    }
}
