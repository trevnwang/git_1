using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MbdGitTool.Core.Contracts;
using MbdGitTool.Core.Security;
using MbdGitTool.Host.Services;
using Xunit;

namespace MbdGitTool.Host.Tests;

/// <summary>AC-OP-02/05：Clone 预检（目标已存在/长路径）在网络前零写阻断。</summary>
public sealed class M2b2bClonePreflightTests
{
    [Fact]
    public async Task Clone_DiskSpaceLowInjection_ReturnsDiskFull()
    {
        using var workspace = new TempWorkspace();
        var config = new AppConfig
        {
            WorkspaceRoot = workspace.Root,
            AuditRoot = TestAudit.NewRoot(),
            JobsRoot = TestAudit.NewJobsRoot(),
            DatabasePath = TestAudit.NewDatabasePath(),
            ExportsRoot = TestAudit.NewExportsRoot(),
            GiteaBaseUrl = "https://example.test",
            GiteaOrg = "RTCRepos",
        };
        var batch = new BatchOperationService(config, new RepoActionService(config: config), paths: new PathPolicy(workspace.Root));
        Environment.SetEnvironmentVariable("MBD_FAULT_INJECT", "DISK_SPACE_LOW");
        try
        {
            var result = await batch.CloneAsync(null, "CF212A_Disk", "IMPL", CancellationToken.None);
            Assert.Equal(JobItemStates.Failed, result.State);
            Assert.Equal(BridgeErrorCodes.DiskFull, result.ErrorCode);
            Assert.False(Directory.Exists(Path.Combine(workspace.Root, "CF212A_Disk_IMPL")));
        }
        finally
        {
            Environment.SetEnvironmentVariable("MBD_FAULT_INJECT", null);
        }
    }

    [Theory]
    [InlineData("GIT_NET_404", "E_REMOTE_NOT_FOUND")]
    [InlineData("GIT_NET_TIMEOUT", "E_NETWORK")]
    public async Task Clone_NetInjection_ClassifiesError(string fault, string expectedCode)
    {
        using var workspace = new TempWorkspace();
        var config = new AppConfig
        {
            WorkspaceRoot = workspace.Root,
            AuditRoot = TestAudit.NewRoot(),
            JobsRoot = TestAudit.NewJobsRoot(),
            DatabasePath = TestAudit.NewDatabasePath(),
            ExportsRoot = TestAudit.NewExportsRoot(),
            GiteaBaseUrl = "https://example.test",
            GiteaOrg = "RTCRepos",
        };
        var batch = new BatchOperationService(config, new RepoActionService(config: config), paths: new PathPolicy(workspace.Root));
        Environment.SetEnvironmentVariable("MBD_FAULT_INJECT", fault);
        try
        {
            var result = await batch.CloneAsync(null, "CF212A_Net", "IMPL", CancellationToken.None);
            Assert.Equal(JobItemStates.Failed, result.State);
            Assert.Equal(expectedCode, result.ErrorCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MBD_FAULT_INJECT", null);
        }
    }
    [Fact]
    public async Task Clone_NetAuth401Injection_ReturnsAuthFailed()
    {
        using var workspace = new TempWorkspace();
        var config = new AppConfig
        {
            WorkspaceRoot = workspace.Root,
            AuditRoot = TestAudit.NewRoot(),
            JobsRoot = TestAudit.NewJobsRoot(),
            DatabasePath = TestAudit.NewDatabasePath(),
            ExportsRoot = TestAudit.NewExportsRoot(),
            GiteaBaseUrl = "https://example.test",
            GiteaOrg = "RTCRepos",
        };
        var batch = new BatchOperationService(config, new RepoActionService(config: config), paths: new PathPolicy(workspace.Root));
        Environment.SetEnvironmentVariable("MBD_FAULT_INJECT", "GIT_NET_AUTH_401");
        try
        {
            var result = await batch.CloneAsync(null, "CF212A_Auth", "IMPL", CancellationToken.None);
            Assert.Equal(JobItemStates.Failed, result.State);
            Assert.Equal(BridgeErrorCodes.AuthFailed, result.ErrorCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MBD_FAULT_INJECT", null);
        }
    }
    [Fact]
    public async Task Clone_TargetDirectoryExists_IsSkippedWithoutOverwrite()
    {
        using var workspace = new TempWorkspace();
        var config = TestAudit.NewConfig(workspace.Root);
        var target = Path.Combine(workspace.Root, "CF900A_Demo_IMPL");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "keep.txt"), "keep");

        var batch = new BatchOperationService(config, new RepoActionService(config: config), paths: new PathPolicy(workspace.Root));
        var result = await batch.CloneAsync(null, "CF900A_Demo", "IMPL", CancellationToken.None);

        Assert.Equal(JobItemStates.Skipped, result.State);
        Assert.Equal("keep", File.ReadAllText(Path.Combine(target, "keep.txt")));
    }

    [Fact]
    public async Task Clone_TooLongTargetPath_IsRejectedBeforeNetwork()
    {
        using var workspace = new TempWorkspace();
        var config = new AppConfig
        {
            WorkspaceRoot = workspace.Root,
            AuditRoot = TestAudit.NewRoot(),
            JobsRoot = TestAudit.NewJobsRoot(),
            DatabasePath = TestAudit.NewDatabasePath(),
            ExportsRoot = TestAudit.NewExportsRoot(),
            GiteaBaseUrl = "http://localhost:1",
            GiteaOrg = "org",
        };
        var componentId = new string('A', 230);
        var batch = new BatchOperationService(config, new RepoActionService(config: config), paths: new PathPolicy(workspace.Root));

        var result = await batch.CloneAsync(null, componentId, "IMPL", CancellationToken.None);

        Assert.Equal(JobItemStates.Failed, result.State);
        Assert.Equal(BridgeErrorCodes.PathTooLong, result.ErrorCode);
        Assert.False(Directory.Exists(Path.Combine(workspace.Root, componentId + "_IMPL")));
    }
}
