using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace PCHub.Services;

/// <summary>
/// ดูโปรเซสที่รันอยู่ แค่ถามที่อยู่ไฟล์ .exe แบบเดียวกับ Task Manager
/// (ไม่อ่านหน่วยความจำของโปรแกรมอื่น ระบบกันโกงของเกมจึงไม่มีปัญหา)
/// </summary>
public static class ProcessHelper
{
    /// <summary>ที่อยู่ไฟล์ .exe ของโปรเซส (null ถ้าถามไม่ได้ เช่น โปรเซสของระบบ)</summary>
    public static string? GetPath(int processId)
    {
        var handle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
        if (handle == IntPtr.Zero) return null;
        try
        {
            var builder = new StringBuilder(1024);
            var size = builder.Capacity;
            return QueryFullProcessImageName(handle, 0, builder, ref size) ? builder.ToString() : null;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    /// <summary>มีโปรแกรมที่ .exe อยู่ในโฟลเดอร์นี้ (หรือโฟลเดอร์ย่อย) กำลังรันอยู่ไหม</summary>
    public static bool AnyRunningIn(string folder)
    {
        var prefix = folder.TrimEnd('\\', '/') + "\\";
        var found = false;
        foreach (var process in Process.GetProcesses())
        {
            if (!found && GetPath(process.Id) is { } path && path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                found = true;
            process.Dispose();
        }
        return found;
    }

    private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder exeName, ref int size);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);
}
