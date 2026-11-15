using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace MbdGitTool.Host.Services;

/// <summary>
/// v1.0：Token 本地加密存储（Windows DPAPI，CurrentUser）。
/// 配置文件不落明文 Token；仅保存加密后的 token.bin。
/// </summary>
public static class TokenStore
{
    private const string FileName = "token.bin";

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        public int Size;
        public IntPtr Data;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptProtectData(ref DataBlob dataIn, string? description, IntPtr entropy,
        IntPtr reserved, IntPtr prompt, int flags, out DataBlob dataOut);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptUnprotectData(ref DataBlob dataIn, IntPtr description, IntPtr entropy,
        IntPtr reserved, IntPtr prompt, int flags, out DataBlob dataOut);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LocalFree(IntPtr hMem);

    private static string DirectoryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MbdGitTool");

    private static string FilePath => Path.Combine(DirectoryPath, FileName);

    public static string? Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return null;
            var input = new DataBlob();
            var buffer = File.ReadAllBytes(FilePath);
            input.Size = buffer.Length;
            input.Data = Marshal.AllocHGlobal(buffer.Length);
            try
            {
                Marshal.Copy(buffer, 0, input.Data, buffer.Length);
                if (!CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, out var output))
                    return null;
                try
                {
                    var bytes = new byte[output.Size];
                    Marshal.Copy(output.Data, bytes, 0, output.Size);
                    return Encoding.UTF8.GetString(bytes);
                }
                finally
                {
                    if (output.Data != IntPtr.Zero) LocalFree(output.Data);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(input.Data);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DllNotFoundException)
        {
            return null;
        }
    }

    public static bool Save(string? token)
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            if (string.IsNullOrWhiteSpace(token))
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
                return true;
            }

            var buffer = Encoding.UTF8.GetBytes(token.Trim());
            var input = new DataBlob { Size = buffer.Length, Data = Marshal.AllocHGlobal(buffer.Length) };
            try
            {
                Marshal.Copy(buffer, 0, input.Data, buffer.Length);
                if (!CryptProtectData(ref input, "MbdGitTool PAT", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, out var output))
                    return false;
                try
                {
                    var bytes = new byte[output.Size];
                    Marshal.Copy(output.Data, bytes, 0, output.Size);
                    File.WriteAllBytes(FilePath, bytes);
                    return true;
                }
                finally
                {
                    if (output.Data != IntPtr.Zero) LocalFree(output.Data);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(input.Data);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DllNotFoundException)
        {
            return false;
        }
    }
}
