namespace PCHub.Models;

/// <summary>ทุกอย่างที่บันทึกลงไฟล์ settings.json</summary>
public class AppSettings
{
    public List<LaunchMode> Modes { get; set; } = [];

    /// <summary>เกมที่เพิ่มเองในคลังเกม</summary>
    public List<AppEntry> CustomGames { get; set; } = [];

    /// <summary>Id ของเกมที่กดซ่อนไว้ (เช่น "steam:431960")</summary>
    public List<string> HiddenGames { get; set; } = [];
}
