using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using PCHub.Models;

namespace PCHub.Services;

public static class AppLauncher
{
    /// <summary>เปิดแอพหรือเกม 1 ตัว คืนค่า false ถ้าเปิดไม่ได้ (เช่น ไฟล์ถูกลบหรือย้ายไปแล้ว)</summary>
    public static bool Launch(AppEntry app)
    {
        // ลิงก์อย่าง steam://rungameid/... ไม่ใช่ไฟล์ เช็คว่ามีไฟล์ไม่ได้
        var isLink = app.Path.Contains("://", StringComparison.Ordinal);
        if (!isLink && !File.Exists(app.Path)) return false;
        try
        {
            Process.Start(new ProcessStartInfo(app.Path)
            {
                UseShellExecute = true,
                Arguments = app.Arguments ?? "",
            });
            return true;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }

    /// <summary>เปิดทุกแอพในโหมด คืนรายชื่อแอพที่เปิดไม่ได้</summary>
    public static async Task<List<AppEntry>> LaunchModeAsync(LaunchMode mode)
    {
        var failed = new List<AppEntry>();
        foreach (var app in mode.Apps)
        {
            if (!Launch(app)) failed.Add(app);
            // เว้นจังหวะนิดนึง เครื่องจะได้ไม่กระตุกตอนเปิดหลายแอพพร้อมกัน
            await Task.Delay(400);
        }
        return failed;
    }
}
