using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace PCHub.Helpers;

/// <summary>ทำแถบหัวหน้าต่างให้มืดเข้ากับธีม (Windows 11)</summary>
public static class DarkTitleBar
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_CAPTION_COLOR = 35;

    // สีเดียวกับเมนูซ้าย #16171B (รูปแบบ COLORREF = 0x00BBGGRR)
    private const int CaptionColor = 0x001B1716;

    /// <summary>เรียกใน OnSourceInitialized ของหน้าต่าง</summary>
    public static void Apply(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;

        int darkMode = 1;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

        int captionColor = CaptionColor;
        DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref captionColor, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
