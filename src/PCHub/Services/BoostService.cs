using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace PCHub.Services;

/// <summary>แอพที่ปิดไปตอนเริ่มเล่น (จำไว้เพื่อเปิดคืนตอนเลิกเล่น)</summary>
public record ClosedApp(string ProcessName, string? ExePath);

/// <summary>ปิดแอพเบื้องหลังตอนเล่นเกม และเปิดคืนตอนเลิกเล่น (ใช้จาก Task.Run เพราะต้องรอแอพปิด)</summary>
public static class BoostService
{
    /// <summary>launcher และโปรแกรมระบบ ห้ามปิด (ปิดแล้วเกมเปิดไม่ได้หรือ Windows รวน)</summary>
    public static readonly HashSet<string> NeverClose = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer", "PCHub", "dwm", "ApplicationFrameHost", "TextInputHost", "SystemSettings", "ShellExperienceHost",
        "StartMenuExperienceHost", "SearchHost", "LockApp", "steam", "steamwebhelper", "EpicGamesLauncher",
        "EpicWebHelper", "RiotClientServices", "RiotClientUx", "RiotClientUxRender", "EADesktop", "upc",
        "Launcher", "RockstarService", "SocialClubHelper", "TaskMgr",
    };

    public static List<ClosedApp> CloseApps(IEnumerable<string> processNames)
    {
        var closed = new List<ClosedApp>();
        foreach (var name in processNames.Where(n => !NeverClose.Contains(n)))
        {
            var processes = Process.GetProcessesByName(name);
            if (processes.Length == 0) continue;

            var exePath = processes.Select(p => ProcessHelper.GetPath(p.Id)).FirstOrDefault(p => p != null);
            foreach (var p in processes) p.Dispose();

            if (CloseGracefully(name, exePath)) closed.Add(new ClosedApp(name, exePath));
        }
        return closed;
    }

    /// <summary>
    /// ปิดแบบปกติ (เหมือนกดปุ่ม X) แล้วรอ ถ้าแอพยังมีหน้าต่างค้าง (เช่น ถามว่าจะเซฟงานไหม) จะไม่บังคับปิด
    /// เหลือแต่ตัวที่ทำงานเบื้องหลังไม่มีหน้าต่างแล้ว ถึงจะสั่งปิดทิ้ง
    /// </summary>
    private static bool CloseGracefully(string name, string? exePath)
    {
        // OneDrive ไม่มีหน้าต่าง แต่มีคำสั่งปิดตัวเองอย่างปลอดภัย
        if (name.Equals("OneDrive", StringComparison.OrdinalIgnoreCase) && exePath != null)
        {
            TryStart(exePath, "/shutdown");
        }

        for (var attempt = 0; attempt < 8; attempt++)
        {
            var processes = Process.GetProcessesByName(name);
            try
            {
                if (processes.Length == 0) return true;
                foreach (var p in processes.Where(p => p.MainWindowHandle != IntPtr.Zero)) p.CloseMainWindow();
            }
            finally
            {
                foreach (var p in processes) p.Dispose();
            }
            Thread.Sleep(750);
        }

        var remaining = Process.GetProcessesByName(name);
        try
        {
            if (remaining.Length == 0) return true;
            if (remaining.Any(p => p.MainWindowHandle != IntPtr.Zero)) return false; // ยังมีหน้าต่าง ปล่อยไว้

            foreach (var p in remaining)
            {
                try
                {
                    p.Kill();
                }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
                {
                    // ปิดไปเองแล้ว หรือไม่มีสิทธิ์ปิด
                }
            }
            return true;
        }
        finally
        {
            foreach (var p in remaining) p.Dispose();
        }
    }

    public static void Reopen(IEnumerable<ClosedApp> apps)
    {
        foreach (var app in apps.Where(a => a.ExePath != null && File.Exists(a.ExePath)))
        {
            if (Process.GetProcessesByName(app.ProcessName).Length > 0) continue; // เปิดเองไปแล้ว
            var isOneDrive = app.ProcessName.Equals("OneDrive", StringComparison.OrdinalIgnoreCase);
            TryStart(app.ExePath!, isOneDrive ? "/background" : "");
        }
    }

    private static void TryStart(string exePath, string arguments)
    {
        try
        {
            Process.Start(new ProcessStartInfo(exePath, arguments) { UseShellExecute = true })?.Dispose();
        }
        catch (Win32Exception)
        {
            // เปิดไม่ได้ ข้ามไป
        }
    }
}
