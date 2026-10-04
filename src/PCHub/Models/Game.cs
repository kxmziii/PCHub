using PCHub.Helpers;

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
public class Game : ObservableObject
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

    /// <summary>โฟลเดอร์ที่ลงเกมไว้ ใช้ดูว่าเกมเปิดอยู่ไหม (โปรเซสที่ .exe อยู่ในโฟลเดอร์นี้)</summary>
    public string? InstallFolder { get; init; }

    /// <summary>ขนาดที่ launcher บอกไว้ (null = launcher ไม่ได้บอก ต้องนับเอง)</summary>
    public long? SizeOnDisk { get; init; }

    private TimeSpan _playTime;
    /// <summary>เวลาเล่นรวม (จาก PC Hub และ Steam)</summary>
    public TimeSpan PlayTime
    {
        get => _playTime;
        set
        {
            if (SetField(ref _playTime, value)) OnPropertyChanged(nameof(Detail));
        }
    }

    private DateTime? _lastPlayed;
    public DateTime? LastPlayed
    {
        get => _lastPlayed;
        set
        {
            if (SetField(ref _lastPlayed, value)) OnPropertyChanged(nameof(Detail));
        }
    }

    public string SourceName => Source switch
    {
        GameSource.Steam => "Steam",
        GameSource.Epic => "Epic Games",
        GameSource.Riot => "Riot Games",
        GameSource.Custom => "เพิ่มเอง",
        _ => "อื่นๆ",
    };

    /// <summary>บรรทัดใต้ชื่อเกม เช่น "Steam · 38 ชม."</summary>
    public string Detail => PlayTime > TimeSpan.Zero
        ? $"{SourceName}  ·  {TimeFormatter.Short(PlayTime)}"
        : SourceName;

    public AppEntry ToAppEntry() => new()
    {
        Name = Name,
        Path = LaunchTarget,
        Arguments = LaunchArguments,
        IconPath = IconPath ?? CoverPath,
    };
}
