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

    /// <summary>ค่า Game Boost (Load ใส่ค่าเริ่มต้นให้เสมอ ไม่เป็น null)</summary>
    public static BoostSettings Boost => Current.Boost!;

    public static GeneralSettings General => Current.General;

    public static void Save()
    {
        Directory.CreateDirectory(FolderPath);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(Current, JsonOptions));
    }

    private static AppSettings Load()
    {
        AppSettings? settings = null;
        if (File.Exists(FilePath))
        {
            try
            {
                settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOptions);
            }
            catch (JsonException)
            {
                // ไฟล์เสีย: เก็บสำรองไว้ก่อน แล้วเริ่มใหม่
                File.Copy(FilePath, FilePath + ".broken", overwrite: true);
            }
        }

        var changed = settings == null;
        settings ??= new AppSettings { Modes = CreateDefaultModes() };

        // ไฟล์จากเวอร์ชันก่อนยังไม่มีค่า Game Boost
        if (settings.Boost == null)
        {
            settings.Boost = CreateDefaultBoost();
            changed = true;
        }

        if (changed)
        {
            Directory.CreateDirectory(FolderPath);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, JsonOptions));
        }
        return settings;
    }

    /// <summary>Game Boost ตั้งต้น: เปิด Discord พร้อมเกม (ถ้ามี) ยังไม่ปิดแอพไหนจนกว่าผู้ใช้จะเลือกเอง</summary>
    private static BoostSettings CreateDefaultBoost() => new()
    {
        CompanionApps = InstalledAppsService.Find("Discord") is { } discord ? [discord] : [],
    };

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
