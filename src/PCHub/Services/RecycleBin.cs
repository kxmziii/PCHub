using System.Runtime.InteropServices;

namespace PCHub.Services;

/// <summary>คุยกับถังขยะของ Windows: ดูขนาด, ล้าง, ย้ายไฟล์ลงถัง</summary>
public static class RecycleBin
{
    /// <summary>ขนาดรวมของถังขยะทุกไดรฟ์ (ไบต์)</summary>
    public static long GetSize()
    {
        var info = new SHQUERYRBINFO { cbSize = Marshal.SizeOf<SHQUERYRBINFO>() };
        return SHQueryRecycleBin(null, ref info) == 0 ? info.i64Size : 0;
    }

    /// <summary>ล้างถังขยะ (ลบถาวร) คืนค่า true ถ้าสำเร็จ</summary>
    public static bool Empty() =>
        SHEmptyRecycleBin(IntPtr.Zero, null, SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND) == 0;

    /// <summary>ย้ายไฟล์ลงถังขยะ (กู้คืนได้) โดยไม่มีหน้าต่างถามหรือแจ้ง error เด้งขึ้นมา</summary>
    public static void Send(IReadOnlyCollection<string> paths)
    {
        if (paths.Count == 0) return;
        var operation = new SHFILEOPSTRUCT
        {
            wFunc = FO_DELETE,
            // รายชื่อไฟล์คั่นด้วย \0 และปิดท้ายด้วย \0\0
            pFrom = string.Join('\0', paths) + "\0\0",
            fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI,
        };
        SHFileOperation(ref operation);
    }

    // ===== Windows API =====

    private const uint SHERB_NOCONFIRMATION = 0x1;
    private const uint SHERB_NOPROGRESSUI = 0x2;
    private const uint SHERB_NOSOUND = 0x4;

    private const uint FO_DELETE = 0x3;
    private const ushort FOF_SILENT = 0x4;
    private const ushort FOF_NOCONFIRMATION = 0x10;
    private const ushort FOF_ALLOWUNDO = 0x40;
    private const ushort FOF_NOERRORUI = 0x400;

    [StructLayout(LayoutKind.Sequential)]
    private struct SHQUERYRBINFO
    {
        public int cbSize;
        public long i64Size;
        public long i64NumItems;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        public string pFrom;
        public string? pTo;
        public ushort fFlags;
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string? lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHQueryRecycleBin(string? rootPath, ref SHQUERYRBINFO info);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? rootPath, uint flags);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT operation);
}
