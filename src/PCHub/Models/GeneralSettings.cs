namespace PCHub.Models;

/// <summary>การทำงานเบื้องหลังของ PC Hub</summary>
public class GeneralSettings
{
    /// <summary>กด X แล้วย่อไปอยู่มุมจอแทนการปิด</summary>
    public bool MinimizeToTray { get; set; } = true;

    /// <summary>จับเวลาเล่นเองแม้เปิดเกมจาก launcher โดยตรง</summary>
    public bool AutoTrackGames { get; set; } = true;

    /// <summary>เคยบอกผู้ใช้แล้วว่าโปรแกรมยังทำงานอยู่ที่มุมจอ (บอกครั้งเดียว)</summary>
    public bool TrayHintShown { get; set; }
}
