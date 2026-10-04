using System.IO;
using PCHub.Models;

namespace PCHub.Services;

/// <summary>ขนาดเกม พื้นที่ไดรฟ์ และการส่งต่อไปถอนการติดตั้งผ่าน launcher</summary>
public static class StorageService
{
    private static readonly EnumerationOptions AllFiles = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint, // ไม่ตามลิงก์ไปนับไฟล์ที่อยู่ที่อื่น
    };

    /// <summary>นับขนาดโฟลเดอร์เอง (ช้า ใช้กับเกมที่ launcher ไม่ได้บอกขนาด) เรียกจาก Task.Run</summary>
    public static long FolderSize(string folder)
    {
        long total = 0;
        try
        {
            foreach (var file in new DirectoryInfo(folder).EnumerateFiles("*", AllFiles))
            {
                try
                {
                    total += file.Length;
                }
                catch (IOException)
                {
                    // ไฟล์หายไประหว่างนับ
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // โฟลเดอร์หายไประหว่างนับ ใช้เท่าที่นับได้
        }
        return total;
    }

    /// <summary>
    /// โฟลเดอร์กว้างๆ ที่ไม่ใช่โฟลเดอร์เกมจริง (เช่น เกมที่เพิ่มเองจากไฟล์บน Desktop)
    /// ไม่ควรนับขนาด หรือใช้ดูว่าเกมเปิดอยู่ไหม เพราะจะช้าและได้ผลผิด
    /// </summary>
    public static bool IsTooBroad(string folder)
    {
        var full = Path.GetFullPath(folder).TrimEnd('\\');
        if (full.Length <= 3) return true; // เช่น C:\
        Environment.SpecialFolder[] broad =
        [
            Environment.SpecialFolder.UserProfile, Environment.SpecialFolder.DesktopDirectory,
            Environment.SpecialFolder.MyDocuments, Environment.SpecialFolder.ProgramFiles,
            Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.Windows,
            Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolder.ApplicationData,
        ];
        var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        return broad.Select(Environment.GetFolderPath).Append(downloads)
            .Any(f => string.Equals(f.TrimEnd('\\'), full, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>ไดรฟ์ในเครื่อง (ฮาร์ดดิสก์/SSD ที่พร้อมใช้)</summary>
    public static List<DriveInfo> FixedDrives() =>
        DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady).ToList();

    /// <summary>
    /// สิ่งที่ใช้เปิดหน้าถอนการติดตั้งของเกม (PC Hub ไม่ลบไฟล์เกมเอง ให้ launcher จัดการ)
    /// คืนค่า (ลิงก์, ข้อความแนะนำผู้ใช้)
    /// </summary>
    public static (string Target, string Hint) UninstallTarget(Game game) => game.Source switch
    {
        GameSource.Steam => ($"steam://uninstall/{game.Id["steam:".Length..]}", "Steam จะถามยืนยันอีกครั้ง"),
        GameSource.Epic => ("com.epicgames.launcher://", "เปิด Epic แล้ว ไปที่ Library → กด ⋯ ที่เกม → Uninstall"),
        _ => ("ms-settings:appsfeatures", "เปิดหน้าแอพของ Windows แล้ว ค้นชื่อเกม → กด ⋯ → Uninstall"),
    };
}
