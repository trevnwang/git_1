using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace MbdGitTool.Host.Services;

/// <summary>git 调用结果。</summary>
public sealed record GitResult(int ExitCode, string Stdout, string Stderr)
{
    public bool Success => ExitCode == 0;

    /// <summary>超时（由 <see cref="GitCli"/> 杀进程树后返回），调用方映射 <c>E_TIMEOUT</c>。</summary>
    public bool TimedOut { get; init; }

    /// <summary>调用方取消（协作式取消），调用方按取消语义处理。</summary>
    public bool Canceled { get; init; }

    public static GitResult Fail(string error) => new(-1, string.Empty, error);

    public static GitResult Timeout() => new(-1, string.Empty, "git 调用超时") { TimedOut = true };

    public static GitResult Cancelled() => new(-1, string.Empty, "git 调用已取消") { Canceled = true };

    public string TrimmedStdout => Stdout.Trim();
}

/// <summary>
/// git 调用封装（SRS §12.2 / FR-SCAN-03；M2 起同时承载单仓写操作）。
/// <para><b>硬性约束</b>：参数一律走 <see cref="ProcessStartInfo.ArgumentList"/>（禁止 shell 字符串拼接）；</para>
/// <para>固定加 <c>--no-optional-locks</c> 与 <c>-C &lt;path&gt;</c>，设置 <c>GIT_TERMINAL_PROMPT=0</c>；</para>
/// <para>默认超时 20s（只读探测）；写操作由调用方传更长超时（fetch/pull/push 120s，commit/checkout 60s）；</para>
/// <para><b>凭据</b>：可选 <c>extraEnvironment</c> 注入（如 <c>GIT_CONFIG_COUNT/KEY/VALUE</c> 的 http.extraHeader），不对参数或日志输出 token。</para>
/// <para>非 sealed/虚方法：单测可注入故障（如 git 写前抛异常）验证 journal/reconcile 收口，不改变生产调用路径。</para>
/// </summary>
public class GitCli
{
    /// <summary>只读命令默认超时（M1 只做只读探测，超时即判失败并杀进程树）。</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(20);

    /// <summary>git 可执行文件名（Git for Windows ≥2.40，D-019）。</summary>
    public string GitExecutable { get; init; } = "git";

    /// <summary>
    /// 执行一次 git 命令。方法永不抛异常：超时/取消/启动失败均返回失败结果（<see cref="GitResult.Success"/> = false）。
    /// </summary>
    /// <param name="workingDirectory">仓库路径（经 <c>-C</c> 传入，不进入 shell）。</param>
    /// <param name="arguments">子命令及参数（逐个元素加入 ArgumentList）。</param>
    /// <param name="cancellationToken">协作式取消；取消后杀进程树并返回 <see cref="GitResult.Canceled"/>。</param>
    /// <param name="timeout">本次调用超时；null = <see cref="DefaultTimeout"/>（20s）。</param>
    /// <param name="extraEnvironment">额外环境变量（可空；凭据配置用，禁止写入日志/响应）。</param>
    public virtual async Task<GitResult> RunAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null,
        IReadOnlyDictionary<string, string>? extraEnvironment = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentNullException.ThrowIfNull(arguments);

#if DEBUG
        // 故障注入（冻结规格 §16.6）：仅 Debug/Test 构建；Release 忽略。
        var fault = Environment.GetEnvironmentVariable("MBD_FAULT_INJECT");
        if (!string.IsNullOrWhiteSpace(fault))
        {
            var isNetwork = arguments.Any(arg => arg is "fetch" or "push" or "clone" or "ls-remote" or "pull");
            if (isNetwork && fault is "GIT_NET_AUTH_401" or "GIT_NET_404" or "GIT_NET_TIMEOUT")
            {
                var text = fault switch
                {
                    "GIT_NET_AUTH_401" => "fatal: Authentication failed for 'injected' (401)",
                    "GIT_NET_404" => "fatal: repository 'injected' not found (404)",
                    _ => "fatal: unable to access 'injected': Connection timed out"
                };
                return new GitResult(128, string.Empty, text);
            }

            if (fault == "GIT_SLOW" && isNetwork)
            {
                try
                {
                    await Task.Delay(2000, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return GitResult.Cancelled();
                }
            }

            var isWrite = arguments.Any(arg => arg is "commit" or "push" or "branch" or "reset" or "stash"
                or "checkout" or "switch" or "update-ref" or "merge" or "rebase" or "init" or "clone" or "add");
            if (isWrite && fault == "DISK_WRITE_FAIL")
            {
                return new GitResult(128, string.Empty, "fatal: No space left on device (injected DISK_WRITE_FAIL)");
            }
        }
#endif
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

        // 凭据等额外环境（值可能含 token，禁止记录/回显）。
        if (extraEnvironment is not null)
        {
            foreach (var pair in extraEnvironment)
            {
                startInfo.Environment[pair.Key] = pair.Value;
            }
        }

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
            return cancellationToken.IsCancellationRequested ? GitResult.Cancelled() : GitResult.Timeout();
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
            return cancellationToken.IsCancellationRequested ? GitResult.Cancelled() : GitResult.Timeout();
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
