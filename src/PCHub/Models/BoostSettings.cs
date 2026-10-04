namespace PCHub.Models;

/// <summary>Game Boost: สิ่งที่ PC Hub ทำให้ตอนกดเล่นเกมจากคลังเกม</summary>
public class BoostSettings
{
    public bool Enabled { get; set; } = true;

    /// <summary>สลับเป็นโหมดพลังงานแรงสุดระหว่างเล่น</summary>
    public bool HighPerformance { get; set; } = true;

    /// <summary>เลิกเล่นแล้วเปิดแอพที่ปิดไปกลับมา</summary>
    public bool RestoreApps { get; set; } = true;

    /// <summary>ย่อ PC Hub ตอนเกมเปิด</summary>
    public bool MinimizeHub { get; set; } = true;

    /// <summary>ชื่อโปรเซสของแอพที่จะปิดตอนเล่น เช่น "chrome", "OneDrive"</summary>
    public List<string> CloseApps { get; set; } = [];

    /// <summary>แอพที่เปิดพร้อมเกม เช่น Discord</summary>
    public List<AppEntry> CompanionApps { get; set; } = [];
}
