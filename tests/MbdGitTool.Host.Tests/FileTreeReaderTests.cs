using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using MbdGitTool.Core.Contracts;
using MbdGitTool.Core.Files;

namespace MbdGitTool.Host.Tests;

public sealed class FileTreeReaderTests : IDisposable
{
    private readonly TempWorkspace _ws = new();
    private readonly FileTreeReader _reader = new();

    public void Dispose() => _ws.Dispose();

    [Fact]
    public void Read_DirectoriesFirstThenNameCaseInsensitive()
    {
        _ws.Dir("b");
        _ws.Dir("A");
        _ws.CreateFile("c.txt");
        _ws.CreateFile("a.txt");

        var page = Read(string.Empty);

        Assert.Equal(new[] { "A", "b", "a.txt", "c.txt" }, page.Entries.Select(entry => entry.Name));
        Assert.Equal(new[] { "DIR", "DIR", "FILE", "FILE" }, page.Entries.Select(entry => entry.Kind));
        Assert.False(page.Truncated);
        Assert.Equal(4, page.TotalEntries);
    }

    [Fact]
    public void Read_HiddenOrGitOrTrashPolicy()
    {
        _ws.CreateFile(".gitignore", "bin/");
        _ws.Dir(".git");
        _ws.Dir(".trash");
        _ws.CreateFile("visible.txt");
        var hidden = _ws.CreateFile("hidden.txt");
        File.SetAttributes(hidden, File.GetAttributes(hidden) | FileAttributes.Hidden);

        var defaultPage = Read(string.Empty);
        var names = defaultPage.Entries.Select(entry => entry.Name).ToList();
        Assert.Contains("visible.txt", names);
        Assert.DoesNotContain("hidden.txt", names);
        Assert.DoesNotContain(".gitignore", names);
        Assert.DoesNotContain(".git", names);
        Assert.DoesNotContain(".trash", names);

        var includeHidden = Read(string.Empty, new TreeReadOptions(IncludeHidden: true));
        var hiddenNames = includeHidden.Entries.Select(entry => entry.Name).ToList();
        Assert.Contains("hidden.txt", hiddenNames);
        Assert.Contains(".gitignore", hiddenNames);
        Assert.DoesNotContain(".git", hiddenNames);
        Assert.DoesNotContain(".trash", hiddenNames);
    }

    [Fact]
    public void Read_TruncatesAtLimitAndReportsTotal()
    {
        for (var index = 0; index < 2005; index++)
        {
            _ws.CreateFile("many/f" + index.ToString("D4") + ".txt", string.Empty);
        }

        var page = Read("many");

        Assert.True(page.Truncated);
        Assert.Equal(2005, page.TotalEntries);
        Assert.Equal(FileTreeReader.DefaultLimit, page.Entries.Count);
    }

    [Fact]
    public void Read_LimitOptionRespected()
    {
        _ws.CreateFile("few/a.txt");
        _ws.CreateFile("few/b.txt");
        _ws.CreateFile("few/c.txt");

        var page = Read("few", new TreeReadOptions(Limit: 2));

        Assert.True(page.Truncated);
        Assert.Equal(3, page.TotalEntries);
        Assert.Equal(2, page.Entries.Count);
    }

    [Fact]
    public void Read_DepthTwoInlinesChildrenOneLevel()
    {
        _ws.CreateFile("src/main.m", "int main;");
        _ws.CreateFile("src/sub/deep.c", "x");
        _ws.CreateFile("docs/readme.md", "x");

        var page = Read(string.Empty, new TreeReadOptions(Depth: 2));

        var src = Assert.Single(page.Entries, entry => entry.Name == "src");
        Assert.NotNull(src.Children);
        var main = Assert.Single(src.Children!, entry => entry.Name == "main.m");
        Assert.Equal("src/main.m", main.Path);

        var sub = Assert.Single(src.Children!, entry => entry.Name == "sub");
        Assert.Null(sub.Children); // 只内联一层

        var docs = Assert.Single(page.Entries, entry => entry.Name == "docs");
        Assert.NotNull(docs.Children);
        Assert.Equal("docs/readme.md", Assert.Single(docs.Children!).Path);
    }

    [Fact]
    public void Read_DepthZeroReturnsNoEntries()
    {
        _ws.CreateFile("a.txt");

        var page = Read(string.Empty, new TreeReadOptions(Depth: 0));

        Assert.Empty(page.Entries);
        Assert.Equal(0, page.TotalEntries);
    }

    [Fact]
    public void Read_ChineseAndSpaces_Supported()
    {
        _ws.CreateFile("控制 器/主 文件.m", "x");

        var rootPage = Read(string.Empty);
        var dir = Assert.Single(rootPage.Entries);
        Assert.Equal("控制 器", dir.Name);

        var childPage = Read("控制 器");
        var file = Assert.Single(childPage.Entries);
        Assert.Equal("控制 器/主 文件.m", file.Path);
        Assert.Equal(1, file.Size);
    }

    [Fact]
    public void Read_MissingDirectory_ReturnsEmptyWithWarningCode()
    {
        var page = _reader.Read(
            _ws.PathFor("does-not-exist"),
            "does-not-exist",
            new TreeReadOptions(),
            CancellationToken.None);

        Assert.Empty(page.Entries);
        Assert.Contains(BridgeErrorCodes.IoError, page.Warnings);
    }

    [Fact]
    public void Read_MetadataSizeAndMtime()
    {
        var content = new string('x', 1234);
        var mtime = new DateTime(2026, 11, 13, 10, 1, 2, DateTimeKind.Utc);
        _ws.CreateFile("meta/data.bin", content, mtime);

        var page = Read("meta");
        var file = Assert.Single(page.Entries);

        Assert.Equal(1234, file.Size);
        Assert.NotNull(file.Mtime);
        Assert.Equal(mtime, file.Mtime!.Value.UtcDateTime);
        Assert.False(file.HasChildren);
    }

    [Fact]
    public void Read_AppliesGitStatusAndSubmoduleAnnotation()
    {
        _ws.CreateFile("src/main.m");
        _ws.Dir("third_party");

        var annotation = new TreeAnnotationProvider(
            GitStatus: path => path == "src/main.m" ? "MODIFIED" : "NONE",
            Submodule: path => path == "third_party");

        var page = Read(string.Empty, new TreeReadOptions(Depth: 2), annotation);

        var src = Assert.Single(page.Entries, entry => entry.Name == "src");
        Assert.Equal("MODIFIED", Assert.Single(src.Children!, entry => entry.Name == "main.m").GitStatus);
        Assert.True(Assert.Single(page.Entries, entry => entry.Name == "third_party").Submodule);
    }

    [Fact]
    public void Read_ReparsePoint_NotRecursed()
    {
        _ws.CreateFile("target/inner.txt");
        var link = _ws.PathFor("junction");
        if (!TryCreateJunction(link, _ws.PathFor("target")))
        {
            return; // 环境不允许 junction：跳过该用例
        }

        var page = Read(string.Empty, new TreeReadOptions(Depth: 2));
        var node = Assert.Single(page.Entries, entry => entry.Name == "junction");

        Assert.True(node.IsReparsePoint);
        Assert.Null(node.Children);
    }

    [Fact]
    public void Read_LongPath_WorksWhenOsAllows()
    {
        var deep = "longpath";
        for (var index = 0; index < 12; index++)
        {
            deep += "/segment" + index.ToString("D2") + "_abcdefghijklmnopqrstuvwxyz";
        }

        try
        {
            _ws.CreateFile(deep + "/file.txt", "long");
            _ws.CreateFile("longpath/short.txt", "long");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PathTooLongException)
        {
            return; // OS 未启用长路径：跳过
        }

        var page = Read("longpath");

        Assert.Equal(2, page.Entries.Count);
        Assert.Equal(4, Assert.Single(page.Entries, entry => entry.Name == "short.txt").Size);
    }

    private DirectoryPage Read(
        string relative,
        TreeReadOptions? options = null,
        TreeAnnotationProvider? annotation = null)
    {
        var absolute = relative.Length == 0 ? _ws.Root : _ws.PathFor(relative);
        return _reader.Read(absolute, relative, options ?? new TreeReadOptions(), CancellationToken.None, annotation);
    }

    private static bool TryCreateJunction(string linkPath, string targetPath)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            startInfo.ArgumentList.Add("/c");
            startInfo.ArgumentList.Add("mklink");
            startInfo.ArgumentList.Add("/J");
            startInfo.ArgumentList.Add(linkPath);
            startInfo.ArgumentList.Add(targetPath);

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return false;
            }

            process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 && Directory.Exists(linkPath);
        }
        catch
        {
            return false;
        }
    }
}
