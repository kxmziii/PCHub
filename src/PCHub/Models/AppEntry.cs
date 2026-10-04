namespace PCHub.Models;

/// <summary>แอพ 1 ตัว: ชื่อที่โชว์ + ที่อยู่ไฟล์ (.lnk, .exe, .url)</summary>
public class AppEntry
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
}
