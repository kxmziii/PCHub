using System.Text.Json.Serialization;

namespace PCHub.Models;

/// <summary>แอพหรือเกม 1 ตัว: ชื่อที่โชว์ + สิ่งที่ใช้เปิด (.lnk, .exe, .url หรือลิงก์อย่าง steam://)</summary>
public class AppEntry
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";

    /// <summary>ค่าที่ส่งต่อให้โปรแกรมตอนเปิด (เช่น --launch-product=valorant)</summary>
    public string? Arguments { get; set; }

    /// <summary>ไฟล์ที่ใช้ทำไอคอน ถ้าไม่ใส่จะใช้ไอคอนของ Path</summary>
    public string? IconPath { get; set; }

    [JsonIgnore]
    public string IconSource => IconPath ?? Path;

    public AppEntry Clone() => (AppEntry)MemberwiseClone();
}
