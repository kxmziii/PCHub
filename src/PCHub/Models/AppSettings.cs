namespace PCHub.Models;

/// <summary>ทุกอย่างที่บันทึกลงไฟล์ settings.json</summary>
public class AppSettings
{
    public List<LaunchMode> Modes { get; set; } = [];

    /// <summary>เกมที่เพิ่มเองในคลังเกม</summary>
    public List<AppEntry> CustomGames { get; set; } = [];

    /// <summary>Id ของเกมที่กดซ่อนไว้ (เช่น "steam:431960")</summary>
    public List<string> HiddenGames { get; set; } = [];

    /// <summary>null = ยังไม่เคยตั้งค่า (SettingsService จะใส่ค่าเริ่มต้นให้)</summary>
    public BoostSettings? Boost { get; set; }

    public GeneralSettings General { get; set; } = new();

    /// <summary>Steam appid ของเกมที่กดเฝ้าราคาไว้ใน PC Hub (นอกเหนือจาก Wishlist)</summary>
    public List<int> WatchedGames { get; set; } = [];

    /// <summary>รหัสเซิร์ฟ FiveM ที่ติดดาวไว้</summary>
    public List<string> FiveMFavorites { get; set; } = [];
}
