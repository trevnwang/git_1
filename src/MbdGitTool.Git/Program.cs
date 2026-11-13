using System;

namespace MbdGitTool.Git;

/// <summary>
/// GitWorker skeleton (M0). M1+ 将实现命名管道 JSON-RPC：
/// Hello{protocolVersion} / RunJob / Progress / Done / Cancel / Ping。
/// 严禁在此进程直接访问 SQLite（只有 Host 唯一写库）。
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        Console.WriteLine("[MbdGitTool.Git] skeleton started; protocol not implemented yet (M1).");
        Console.WriteLine($"[MbdGitTool.Git] pid={Environment.ProcessId} args={string.Join(' ', args)}");
        return 0;
    }
}
