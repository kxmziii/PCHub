using Microsoft.Win32;

namespace PCHub.Services;

/// <summary>เปิด PC Hub พร้อม Windows (ใส่ชื่อไว้ในรายการ Run ของผู้ใช้ ไม่ต้องใช้สิทธิ์ admin)</summary>
public static class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "PC Hub";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string;
        }
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
        {
            // --tray = เปิดแบบย่อไว้ที่มุมจอ ไม่เด้งหน้าต่างตอนเปิดเครื่อง
            // ตอนติดตั้งผ่าน Setup.exe ที่อยู่นี้ไม่เปลี่ยนแม้อัปเดตเวอร์ชัน (...\PCHub\current\PCHub.exe)
            key.SetValue(ValueName, $"\"{Environment.ProcessPath}\" --tray");
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
