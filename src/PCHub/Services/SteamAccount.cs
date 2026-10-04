using System.IO;
using Microsoft.Win32;

namespace PCHub.Services;

/// <summary>หา Steam ที่ลงไว้ และบัญชี Steam ที่ใช้ล่าสุดในเครื่อง (อ่านจากไฟล์ของ Steam ไม่ต้องล็อกอิน)</summary>
public static class SteamAccount
{
    /// <summary>โฟลเดอร์ที่ลง Steam ไว้ (null = ไม่ได้ลง Steam)</summary>
    public static string? SteamFolder()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
        return key?.GetValue("SteamPath") is string path ? Path.GetFullPath(path) : null;
    }

    /// <summary>ไฟล์ตั้งค่าของบัญชีที่ใช้ล่าสุด (มีเวลาเล่นแต่ละเกม)</summary>
    public static string? LocalConfigPath()
    {
        var userdata = SteamFolder() is { } steam ? Path.Combine(steam, "userdata") : null;
        if (userdata == null || !Directory.Exists(userdata)) return null;

        // เครื่องเดียวอาจล็อกอินหลายบัญชี ใช้บัญชีที่ไฟล์ตั้งค่าถูกแก้ล่าสุด
        return Directory.EnumerateDirectories(userdata)
            .Select(d => Path.Combine(d, "config", "localconfig.vdf"))
            .Where(File.Exists)
            .OrderByDescending(File.GetLastWriteTime)
            .FirstOrDefault();
    }

    /// <summary>SteamID64 ของบัญชีที่ใช้ล่าสุด (null = หาไม่เจอ)</summary>
    public static string? SteamId64()
    {
        // โครงสร้าง: userdata\<account id>\config\localconfig.vdf
        var accountFolder = LocalConfigPath() is { } config ? Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(config))) : null;
        return long.TryParse(accountFolder, out var accountId) ? (76561197960265728L + accountId).ToString() : null;
    }
}
