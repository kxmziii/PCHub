namespace PCHub.Models;

public enum GameSource
{
    Steam,
    Epic,
    Riot,
    Other,
    Custom,
}

/// <summary>เกม 1 เกมในคลังเกม</summary>
public class Game
{
    /// <summary>ไม่ซ้ำกันในคลัง เช่น "steam:578080", "epic:Brill"</summary>
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required GameSource Source { get; init; }

    /// <summary>สิ่งที่ใช้เปิดเกม: ลิงก์ (steam://...) หรือไฟล์ (.exe, .lnk)</summary>
    public required string LaunchTarget { get; init; }
    public string? LaunchArguments { get; init; }

    /// <summary>รูปปกแนวตั้ง (ไฟล์ในเครื่องหรือ https)</summary>
    public string? CoverPath { get; init; }

    /// <summary>ไอคอนเล็ก (exe, lnk หรือรูป) ใช้ตอนไม่มีรูปปก</summary>
    public string? IconPath { get; init; }

    public string? InstallFolder { get; init; }

    public string SourceName => Source switch
    {
        GameSource.Steam => "Steam",
        GameSource.Epic => "Epic Games",
        GameSource.Riot => "Riot Games",
        GameSource.Custom => "เพิ่มเอง",
        _ => "อื่นๆ",
    };

    public AppEntry ToAppEntry() => new()
    {
        Name = Name,
        Path = LaunchTarget,
        Arguments = LaunchArguments,
        IconPath = IconPath ?? CoverPath,
    };
}
