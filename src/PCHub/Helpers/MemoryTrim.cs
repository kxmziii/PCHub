using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;

namespace PCHub.Helpers;

/// <summary>
/// คืนแรมที่ไม่ได้ใช้แล้วให้ Windows (เรียกตอนย่อไปไว้มุมจอ ตอนนั้นไม่มีใครดูหน้าจอ เลยไม่มีใครรู้สึกสะดุด)
/// เช่น รายชื่อเซิร์ฟ FiveM 20 MB ที่โหลดมาแล้ว หรือรูปที่ไม่ได้โชว์แล้ว
/// </summary>
public static class MemoryTrim
{
    public static void Trim()
    {
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();

        using var self = Process.GetCurrentProcess();
        SetProcessWorkingSetSize(self.Handle, -1, -1);
    }

    [DllImport("kernel32.dll")]
    private static extern bool SetProcessWorkingSetSize(IntPtr process, nint minimum, nint maximum);
}
