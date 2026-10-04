using PCHub.Helpers;

namespace PCHub.Models;

public enum CleanupKind
{
    Temp,
    RecycleBin,
    BrowserCache,
    DiscordCache,
    CrashReports,
    GpuCache,
    OldDownloads,
}

public record FoundFile(string Path, long Size);

/// <summary>ผลสแกน 1 หมวด</summary>
public record ScanResult(List<FoundFile> Files, long TotalBytes);

/// <summary>ผลลบ 1 หมวด: ลบได้กี่ไบต์ ข้ามกี่ไฟล์ (ไฟล์ที่โปรแกรมกำลังใช้อยู่)</summary>
public record CleanResult(long FreedBytes, int SkippedFiles);

/// <summary>หมวดขยะ 1 แถวในหน้าลบขยะ</summary>
public class CleanupCategory : ObservableObject
{
    public required CleanupKind Kind { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Icon { get; init; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    private string _sizeText = "—";
    public string SizeText
    {
        get => _sizeText;
        set => SetField(ref _sizeText, value);
    }

    /// <summary>ผลสแกนล่าสุด (null = ยังไม่ได้สแกน)</summary>
    public ScanResult? LastScan { get; set; }
}
