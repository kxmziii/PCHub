namespace PCHub.Models;

/// <summary>โหมด = กลุ่มแอพที่เปิดพร้อมกัน เช่น "เล่นเกม" = Discord + Steam</summary>
public class LaunchMode
{
    public string Name { get; set; } = "";

    /// <summary>ไอคอนจากฟอนต์ Segoe Fluent Icons (เก็บเป็นตัวอักษร 1 ตัว)</summary>
    public string Icon { get; set; } = "";

    public List<AppEntry> Apps { get; set; } = [];
}
