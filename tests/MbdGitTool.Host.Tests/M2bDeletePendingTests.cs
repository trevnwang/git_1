using System;
using System.IO;
using System.Text;
using MbdGitTool.Host.Services;
using Xunit;

namespace MbdGitTool.Host.Tests;

public sealed class M2bDeletePendingTests
{
    [Fact]
    public void DeletePendingService_ListsCancelsAndCleansExpired()
    {
        using var workspace = new TempWorkspace();
        var config = TestAudit.NewConfig(workspace.Root);
        var pendingDir = Path.Combine(workspace.Root, ".trash", ".pending");
        Directory.CreateDirectory(pendingDir);
        var oldFile = Path.Combine(pendingDir, "20260101000000-CF100A_Demo_IMPL.json");
        var newFile = Path.Combine(pendingDir, "20260102000000-CF100B_Demo_IMPL.json");
        File.WriteAllText(oldFile, "{\"componentId\":\"CF100A_Demo\",\"repoType\":\"IMPL\",\"repoName\":\"CF100A_Demo_IMPL\",\"localPath\":\"x\",\"errorCode\":\"E_DELETE_PENDING\",\"createdAt\":\"2026-01-01T00:00:00Z\"}", new UTF8Encoding(false));
        File.WriteAllText(newFile, "{\"componentId\":\"CF100B_Demo\",\"repoType\":\"IMPL\",\"repoName\":\"CF100B_Demo_IMPL\",\"localPath\":\"x\",\"errorCode\":\"E_DELETE_PENDING\",\"createdAt\":\"2026-01-02T00:00:00Z\"}", new UTF8Encoding(false));
        File.SetLastWriteTimeUtc(oldFile, DateTime.UtcNow.AddDays(-40));
        File.SetLastWriteTimeUtc(newFile, DateTime.UtcNow.AddDays(-1));

        var service = new DeletePendingService(config);
        Assert.Equal(2, service.List().Count);

        Assert.Equal(1, service.Cancel("CF100B_Demo", null));
        Assert.Single(service.List());

        Assert.Equal(1, service.CleanupExpired(30));
        Assert.Empty(service.List());
        Assert.False(File.Exists(oldFile));
    }
}
