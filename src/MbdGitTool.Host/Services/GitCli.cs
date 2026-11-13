using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace MbdGitTool.Host.Services;

/// <summary>git 只读调用结果。</summary>
public sealed record GitResult(int ExitCode, string Stdout, string Stderr)
{
    public bool Success => ExitCode == 0;

    public static GitResult Fail(string error) => new(-1, string.Empty, error);

    public string TrimmedStdout => Stdout.Trim();
}

/// <summary>
/// git 只读调用封装（SRS §12.2 / FR-SCAN-03）。
/// <para><b>硬性约束</b>：参数一律走 <see cref="ProcessStartInfo.ArgumentList"/>（禁止 shell 字符串拼接）；</para>
/// <para>固定加 <c>--no-optional-locks</c> 与 <c>-C &lt;path&gt;</c>，设置 <c>GIT_TERMINAL_PROMPT=0</c>，只读默认超时 20s。</para>
/// </summary>
public sealed class GitCli
{
    /// <summary>只读命令默认超时（M1 只做只读探测，超时即判失败并杀进程树）。</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(20);

    /// <summary>git 可执行文件名（Git for Windows ≥2.40，D-019）。</summary>
    public string GitExecutable { get; init; } = "git";

    /// <summary>
    /// 执行一次 git 只读命令。方法永不抛异常：超时/取消/启动失败均返回失败结果（<see cref="GitResult.Success"/> = false）。
    /// </summary>
    /// <param name="workingDirectory">仓库路径（经 <c>-C</c> 传入，不进入 shell）。</param>
    /// <param name="arguments">子命令及参数（逐个元素加入 ArgumentList）。</param>
    public async Task<GitResult> RunAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentNullException.ThrowIfNull(arguments);

        var startInfo = new ProcessStartInfo
        {
            FileName = GitExecutable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false
        };

        // 全局参数：--no-optional-locks 必须在子命令之前；-C <path> 同样作为全局参数。
        startInfo.ArgumentList.Add("--no-optional-locks");
        startInfo.ArgumentList.Add("-C");
        startInfo.ArgumentList.Add(workingDirectory);
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        // 禁止交互式凭据提示；只读扫描不抢占可选锁。
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        startInfo.Environment["GIT_OPTIONAL_LOCKS"] = "0";
        startInfo.Environment["GCM_INTERACTIVE"] = "never";

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                return GitResult.Fail("git 进程启动失败");
            }
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            return GitResult.Fail($"git 不可用：{ex.Message}");
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout ?? DefaultTimeout);

        var stdoutTask = process.StandardOutput.ReadToEndAsync(timeoutSource.Token);
        var stderrTask = process.StandardError.ReadToEndAsync(timeoutSource.Token);

        try
        {
            await process.WaitForExitAsync(timeoutSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            KillProcessTree(process);
            await DrainAsync(stdoutTask, stderrTask).ConfigureAwait(false);
            return GitResult.Fail(cancellationToken.IsCancellationRequested ? "git 调用已取消" : "git 调用超时");
        }

        string stdout;
        string stderr;
        try
        {
            stdout = await stdoutTask.ConfigureAwait(false);
            stderr = await stderrTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            KillProcessTree(process);
            return GitResult.Fail("git 调用已取消");
        }

        return new GitResult(process.ExitCode, stdout, stderr);
    }

    private static void KillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // 进程可能已退出；杀进程失败不影响返回失败结果。
        }
    }

    private static async Task DrainAsync(Task<string> stdoutTask, Task<string> stderrTask)
    {
        try
        {
            await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
        }
        catch
        {
            // 超时/取消后读取任务可能以 OCE 结束，忽略即可。
        }
    }
}
