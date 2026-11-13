using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using MbdGitTool.Core.Contracts;

namespace MbdGitTool.Core.Files;

/// <summary>
/// 单目录文件树读取（code-plan §4.4.2 / spec §4.4）：一次只枚举一个目录，目录优先 + 名称排序，
/// 单目录上限 2000、扫描保护 20000，隐藏/`.git`/`.trash` 策略，权限/不存在错误降级为空 + 告警码。
/// <para>不递归（depth=2 时仅内联一层 children）；reparse point 展示但不递归。</para>
/// </summary>
public sealed class FileTreeReader
{
    /// <summary>目录扫描硬保护：超过后停止枚举并告警（spec §4.5 / code-plan §4.4.2）。</summary>
    public const int MaxScanEntries = 20000;

    /// <summary>默认单目录返回上限（spec：2000）。</summary>
    public const int DefaultLimit = 2000;

    /// <summary>读取一个目录；<paramref name="repoRelativeDirectory"/> 为 posix 相对路径（根为空串）。</summary>
    public DirectoryPage Read(
        string absoluteDirectory,
        string repoRelativeDirectory,
        TreeReadOptions options,
        CancellationToken cancellationToken,
        TreeAnnotationProvider? annotation = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(absoluteDirectory);
        ArgumentNullException.ThrowIfNull(options);

        var relativeDir = NormalizeRelative(repoRelativeDirectory);
        var depth = Math.Clamp(options.Depth, 0, 2);
        var limit = Math.Clamp(options.Limit, 1, DefaultLimit);

        if (depth == 0)
        {
            return DirectoryPage.Empty;
        }

        if (!Directory.Exists(absoluteDirectory))
        {
            return new DirectoryPage(
                Array.Empty<TreeNodeDto>(),
                false,
                0,
                new[] { BridgeErrorCodes.IoError });
        }

        var warnings = new List<string>();
        var raw = new List<RawEntry>(Math.Min(limit + 1, 2048));
        var hitScanLimit = false;

        try
        {
            var enumerable = new FileSystemEnumerable<RawEntry>(
                PathUtil.EnsureExtendedPath(absoluteDirectory),
                static (ref FileSystemEntry entry) => RawEntry.From(ref entry),
                new EnumerationOptions
                {
                    RecurseSubdirectories = false,
                    IgnoreInaccessible = true,
                    AttributesToSkip = 0,
                    ReturnSpecialDirectories = false,
                    MatchType = MatchType.Simple
                })
            {
                ShouldIncludePredicate = (ref FileSystemEntry entry) => ShouldInclude(ref entry, options)
            };

            foreach (var entry in enumerable)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (raw.Count >= MaxScanEntries)
                {
                    hitScanLimit = true;
                    break;
                }

                raw.Add(entry);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (UnauthorizedAccessException)
        {
            warnings.Add(BridgeErrorCodes.AccessDenied);
        }
        catch (IOException)
        {
            warnings.Add(BridgeErrorCodes.IoError);
        }

        raw.Sort(RawEntryComparer.Instance);

        if (hitScanLimit)
        {
            warnings.Add(BridgeErrorCodes.TooManyEntries);
        }

        var totalEntries = raw.Count;
        var truncated = totalEntries > limit;
        var pageEntries = new List<TreeNodeDto>(Math.Min(totalEntries, limit));
        foreach (var entry in raw)
        {
            if (pageEntries.Count >= limit)
            {
                break;
            }

            pageEntries.Add(ToNode(entry, absoluteDirectory, relativeDir, depth, options, annotation, cancellationToken));
        }

        return new DirectoryPage(pageEntries, truncated, totalEntries, warnings);
    }

    private TreeNodeDto ToNode(
        RawEntry entry,
        string absoluteParentDirectory,
        string parentRelativeDir,
        int depth,
        TreeReadOptions options,
        TreeAnnotationProvider? annotation,
        CancellationToken cancellationToken)
    {
        var path = parentRelativeDir.Length == 0 ? entry.Name : parentRelativeDir + "/" + entry.Name;
        var isDirectory = entry.IsDirectory;
        var isReparse = (entry.Attributes & FileAttributes.ReparsePoint) != 0;

        IReadOnlyList<TreeNodeDto>? children = null;
        if (isDirectory && depth >= 2 && !isReparse)
        {
            // 仅内联一层：children 用 depth=1 读取（不再递归）。
            var childPage = Read(
                Path.Combine(absoluteParentDirectory, entry.Name),
                path,
                options with { Depth = 1 },
                cancellationToken,
                annotation);
            children = childPage.Entries;
        }

        var gitStatus = "NONE";
        if (!isDirectory && options.IncludeGitStatus && annotation?.GitStatus is not null)
        {
            gitStatus = annotation.GitStatus(path);
        }

        var submodule = isDirectory && annotation?.Submodule is not null && annotation.Submodule(path);

        return new TreeNodeDto(
            Path: path,
            Name: entry.Name,
            Kind: isDirectory ? "DIR" : "FILE",
            HasChildren: isDirectory,
            Size: isDirectory ? null : entry.Length,
            Mtime: entry.LastWriteTimeUtc,
            GitStatus: gitStatus,
            Submodule: submodule,
            Ignored: false,
            Children: children,
            Error: null,
            IsReparsePoint: isReparse ? true : null);
    }

    private static bool ShouldInclude(ref FileSystemEntry entry, TreeReadOptions options)
    {
        var name = entry.FileName;

        if (name.Equals(".git", StringComparison.OrdinalIgnoreCase)
            || name.Equals(".trash", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (options.IncludeHidden)
        {
            return true;
        }

        if ((entry.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0)
        {
            return false;
        }

        return name.Length == 0 || name[0] != '.';
    }

    private static string NormalizeRelative(string? relative) =>
        string.IsNullOrWhiteSpace(relative) ? string.Empty : relative.Replace('\\', '/').Trim('/');

    private readonly record struct RawEntry(
        string Name,
        bool IsDirectory,
        long Length,
        DateTimeOffset LastWriteTimeUtc,
        FileAttributes Attributes)
    {
        public static RawEntry From(ref FileSystemEntry entry) => new(
            entry.FileName.ToString(),
            entry.IsDirectory,
            entry.IsDirectory ? 0 : entry.Length,
            entry.LastWriteTimeUtc,
            entry.Attributes);
    }

    private sealed class RawEntryComparer : IComparer<RawEntry>
    {
        public static RawEntryComparer Instance { get; } = new();

        public int Compare(RawEntry x, RawEntry y)
        {
            if (x.IsDirectory != y.IsDirectory)
            {
                return x.IsDirectory ? -1 : 1;
            }

            var byName = StringComparer.OrdinalIgnoreCase.Compare(x.Name, y.Name);
            return byName != 0 ? byName : StringComparer.Ordinal.Compare(x.Name, y.Name);
        }
    }
}
