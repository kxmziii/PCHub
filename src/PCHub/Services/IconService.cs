using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PCHub.Services;

/// <summary>
/// ดึงไอคอนของไฟล์ (exe, shortcut) แบบเดียวกับที่ Explorer โชว์
/// ไม่มีลูกศร shortcut และได้ขนาดใหญ่ (48px) ภาพคมบนจอ DPI สูง
/// </summary>
public static class IconService
{
    private const int IconSize = 48;
    private static readonly Dictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static ImageSource? GetIcon(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (Cache.TryGetValue(path, out var cached)) return cached;

        // ถ้าเป็นไฟล์รูปอยู่แล้ว (เช่น ไอคอนเกมจาก Steam) ใช้รูปนั้นเลย
        var image = IsImage(path) ? ImageLoader.Load(path, IconSize) : LoadIcon(path);
        Cache[path] = image;
        return image;
    }

    private static bool IsImage(string path) =>
        path.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".png", StringComparison.OrdinalIgnoreCase);

    private static BitmapSource? LoadIcon(string path)
    {
        IntPtr hBitmap = IntPtr.Zero;
        try
        {
            if (SHCreateItemFromParsingName(path, IntPtr.Zero, typeof(IShellItemImageFactory).GUID, out var factory) != 0)
                return null;
            try
            {
                if (factory.GetImage(new SIZE(IconSize, IconSize), SIIGBF_ICONONLY | SIIGBF_BIGGERSIZEOK, out hBitmap) != 0)
                    return null;
            }
            finally
            {
                Marshal.ReleaseComObject(factory);
            }
            return ToBitmapSource(hBitmap);
        }
        catch (COMException)
        {
            return null;
        }
        finally
        {
            if (hBitmap != IntPtr.Zero) DeleteObject(hBitmap);
        }
    }

    /// <summary>
    /// แปลง HBITMAP เป็นรูปของ WPF โดยเก็บความโปร่งใสไว้
    /// (Imaging.CreateBitmapSourceFromHBitmap ทำพื้นใสกลายเป็นสีดำ เลยต้องอ่านพิกเซลเอง)
    /// </summary>
    private static BitmapSource? ToBitmapSource(IntPtr hBitmap)
    {
        var info = new BITMAP();
        if (GetObject(hBitmap, Marshal.SizeOf<BITMAP>(), ref info) == 0) return null;

        int width = info.bmWidth, height = info.bmHeight, stride = width * 4;
        var header = new BITMAPINFOHEADER
        {
            biSize = Marshal.SizeOf<BITMAPINFOHEADER>(),
            biWidth = width,
            biHeight = -height, // ค่าติดลบ = เรียงพิกเซลจากบนลงล่าง
            biPlanes = 1,
            biBitCount = 32,
        };
        var pixels = new byte[stride * height];

        var hdc = GetDC(IntPtr.Zero);
        try
        {
            if (GetDIBits(hdc, hBitmap, 0, (uint)height, pixels, ref header, 0) == 0) return null;
        }
        finally
        {
            ReleaseDC(IntPtr.Zero, hdc);
        }

        // ไอคอนเก่าๆ บางตัวไม่มีช่อง alpha (เป็น 0 หมด) ให้ถือว่าทึบทั้งรูป
        var hasAlpha = false;
        for (var i = 3; i < pixels.Length; i += 4)
        {
            if (pixels[i] != 0) { hasAlpha = true; break; }
        }
        if (!hasAlpha)
        {
            for (var i = 3; i < pixels.Length; i += 4) pixels[i] = 255;
        }

        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Pbgra32, null, pixels, stride);
        bitmap.Freeze();
        return bitmap;
    }

    // ===== Windows API =====

    private const int SIIGBF_BIGGERSIZEOK = 0x1;
    private const int SIIGBF_ICONONLY = 0x4;

    [ComImport]
    [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(SIZE size, int flags, out IntPtr hBitmap);
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct SIZE(int cx, int cy)
    {
        public readonly int cx = cx;
        public readonly int cy = cy;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAP
    {
        public int bmType;
        public int bmWidth;
        public int bmHeight;
        public int bmWidthBytes;
        public ushort bmPlanes;
        public ushort bmBitsPixel;
        public IntPtr bmBits;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public int biSize;
        public int biWidth;
        public int biHeight;
        public short biPlanes;
        public short biBitCount;
        public int biCompression;
        public int biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public int biClrUsed;
        public int biClrImportant;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHCreateItemFromParsingName(
        string path, IntPtr bindContext, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IShellItemImageFactory factory);

    [DllImport("gdi32.dll")]
    private static extern int GetObject(IntPtr hObject, int size, ref BITMAP bitmap);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(IntPtr hdc, IntPtr hBitmap, uint start, uint lines, byte[] bits, ref BITMAPINFOHEADER info, uint usage);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
}
