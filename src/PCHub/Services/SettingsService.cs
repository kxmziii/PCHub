using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using PCHub.Models;

namespace PCHub.Services;

/// <summary>อ่าน/บันทึกการตั้งค่าที่ %APPDATA%\PCHub\settings.json</summary>
public static class SettingsService
{
    private static readonly string FolderPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PCHub");

    private static readonly string FilePath = Path.Combine(FolderPath, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        // เก็บภาษาไทยเป็นตัวอักษรปกติ เปิดไฟล์อ่านเองได้
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static AppSettings Current { get; } = Load();

    public static void Save()
    {
        Directory.CreateDirectory(FolderPath);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(Current, JsonOptions));
    }

    private static AppSettings Load()
    {
        if (File.Exists(FilePath))
        {
            try
            {
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOptions) ?? new();
            }
            catch (JsonException)
            {
                // ไฟล์เสีย: เก็บสำรองไว้ก่อน แล้วเริ่มใหม่
                File.Copy(FilePath, FilePath + ".broken", overwrite: true);
            }
        }

        var settings = new AppSettings { Modes = CreateDefaultModes() };
        Directory.CreateDirectory(FolderPath);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, JsonOptions));
        return settings;
    }

    /// <summary>โหมดตั้งต้นตอนเปิดครั้งแรก ใส่เฉพาะแอพที่มีในเครื่อง</summary>
    private static List<LaunchMode> CreateDefaultModes()
    {
        static LaunchMode Make(string name, string icon, params string[] appNames) => new()
        {
            Name = name,
            Icon = icon,
            Apps = appNames.Select(InstalledAppsService.Find).OfType<AppEntry>().ToList(),
        };

        return
        [
            Make("เล่นเกม", "", "Discord", "Steam"),
            Make("สตรีม / ตัดต่อ", "", "OBS Studio", "Discord", "DaVinci Resolve"),
            Make("เขียนโค้ด", "", "Visual Studio Code", "Google Chrome", "Roblox Studio"),
        ];
    }
}
