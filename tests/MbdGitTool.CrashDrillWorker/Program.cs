using System;
using System.IO;
using System.Threading;
using MbdGitTool.Host.Services;

// 崩溃演练 worker：写 journal STARTED → 真实 git commit → 立即 FailFast（不写 DONE），
// 由父进程验证重启 reconcile 判定 UNKNOWN_APPLIED。仅测试用。
if (args.Length < 4)
{
    Console.Error.WriteLine("usage: journalDir repoPath preHead markerFile");
    return 2;
}

var journalDir = args[0];
var repoPath = args[1];
var preHead = args[2];
var marker = args[3];

var journal = new OperationJournal(journalDir);
journal.Begin("CRASH_DRILL", "CF900_Crash", "IMPL", preHead, repoPath, "drill-job", "drill-job:1");

File.WriteAllText(Path.Combine(repoPath, "drill-after-write.txt"), DateTime.UtcNow.ToString("O"));
var git = new GitCli();
var add = await git.RunAsync(repoPath, new[] { "add", "drill-after-write.txt" }, CancellationToken.None).ConfigureAwait(false);
if (!add.Success) return 3;
var commit = await git.RunAsync(repoPath, new[] { "commit", "-m", "crash-drill write" }, CancellationToken.None).ConfigureAwait(false);
if (!commit.Success) return 4;

File.WriteAllText(marker, "written");
// 由父测试进程在观察到 marker 后 Kill 本进程：真实“写后被杀”，不写 DONE/FAILED。
Thread.Sleep(TimeSpan.FromMinutes(5));
return 0;
