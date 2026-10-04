using System.IO;
using PCHub.Models;

namespace PCHub.Services;

/// <summary>หาแอพที่ติดตั้งในเครื่อง จาก shortcut ใน Start Menu และบน Desktop</summary>
public static class InstalledAppsService
{
    // ตัด shortcut ที่ไม่ใช่ตัวแอพจริง เช่น ตัวถอนการติดตั้ง คู่มือ ลิงก์เว็บ
    private static readonly string[] SkipWords =
    [
        "uninstall", "unins", "setup", "installer", "updater", "help", "documentation", "manual",
        "release notes", "readme", "website", "on the web", "what is new", "safe mode", "error report",
    ];

    private static List<AppEntry>? _cache;

    public static List<AppEntry> GetAll()
    {
        if (_cache != null) return _cache;

        var apps = new Dictionary<string, AppEntry>(StringComparer.OrdinalIgnoreCase);

        // Start Menu มาก่อน (ชื่อเป็นทางการกว่า), ค้นในโฟลเดอร์ย่อยด้วย
        foreach (var folder in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
                     Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                 })
        {
            AddShortcuts(apps, folder, recursive: true);
        }

        // Desktop ค้นแค่ชั้นบนสุด (โฟลเดอร์บน Desktop อาจมีไฟล์เยอะมาก)
        foreach (var folder in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                     Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
                 })
        {
            AddShortcuts(apps, folder, recursive: false);
        }

        _cache = apps.Values.OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        return _cache;
    }

    public static AppEntry? Find(string name) =>
        GetAll().FirstOrDefault(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    private static void AddShortcuts(Dictionary<string, AppEntry> apps, string folder, bool recursive)
    {
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return;

        var options = new EnumerationOptions { RecurseSubdirectories = recursive, IgnoreInaccessible = true };
        foreach (var file in Directory.EnumerateFiles(folder, "*.*", options))
        {
            var extension = Path.GetExtension(file);
            if (!extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase) &&
                !extension.Equals(".url", StringComparison.OrdinalIgnoreCase)) continue;

            var name = Path.GetFileNameWithoutExtension(file);
            if (name.EndsWith(" - Copy", StringComparison.OrdinalIgnoreCase)) name = name[..^" - Copy".Length];
            if (SkipWords.Any(w => name.Contains(w, StringComparison.OrdinalIgnoreCase))) continue;

            apps.TryAdd(name, new AppEntry { Name = name, Path = file });
        }
    }
}
