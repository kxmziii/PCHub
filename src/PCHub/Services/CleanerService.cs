using System.IO;
using PCHub.Models;

namespace PCHub.Services;

/// <summary>สแกนและลบไฟล์ขยะ (ทุกเมธอดในนี้ควรเรียกจาก Task.Run จะได้ไม่ทำให้หน้าจอค้าง)</summary>
public static class CleanerService
{
    private static readonly string LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private static readonly string RoamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    private static readonly string Downloads =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

    // ไฟล์ Temp ที่ใหม่กว่านี้อาจกำลังถูกใช้ (เช่น ตัวติดตั้งที่ยังรันอยู่) เลยไม่แตะ
    private static readonly TimeSpan TempMinAge = TimeSpan.FromDays(1);
    private static readonly TimeSpan DownloadsMinAge = TimeSpan.FromDays(30);

    // ไม่ตามลิงก์/junction ออกไปนอกโฟลเดอร์ และรวมไฟล์ซ่อนด้วย (ค่าเริ่มต้นของ .NET จะข้ามไฟล์ซ่อน)
    private static readonly EnumerationOptions Recursive = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
    };

    private static readonly EnumerationOptions TopLevelOnly = new()
    {
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.System,
    };

    /// <summary>หมวดขยะทั้งหมด เรียงตามลำดับที่จะลบ (ถังขยะต้องมาก่อน Downloads)</summary>
    public static List<CleanupCategory> CreateCategories() =>
    [
        new()
        {
            Kind = CleanupKind.Temp, Icon = "", IsSelected = true,
            Name = "ไฟล์ชั่วคราว (Temp)",
            Description = "ไฟล์ที่โปรแกรมต่างๆ สร้างทิ้งไว้ (ไม่แตะไฟล์ที่เพิ่งสร้างใน 24 ชม.)",
        },
        new()
        {
            Kind = CleanupKind.RecycleBin, Icon = "", IsSelected = true,
            Name = "ถังขยะ",
            Description = "ไฟล์ที่ลบไปแล้ว ล้างตรงนี้จะหายถาวร กู้คืนไม่ได้",
        },
        new()
        {
            Kind = CleanupKind.BrowserCache, Icon = "", IsSelected = true,
            Name = "Cache ของ Chrome / Edge",
            Description = "ลบแค่ไฟล์แคช ไม่ลบรหัสผ่าน ประวัติ หรือการล็อกอิน",
        },
        new()
        {
            Kind = CleanupKind.DiscordCache, Icon = "", IsSelected = true,
            Name = "Cache ของ Discord",
            Description = "รูป วิดีโอ อีโมจิที่ Discord โหลดเก็บไว้ (เปิดดูใหม่ก็โหลดมาเอง)",
        },
        new()
        {
            Kind = CleanupKind.CrashReports, Icon = "", IsSelected = true,
            Name = "รายงาน error ของ Windows",
            Description = "ไฟล์ที่ Windows เก็บไว้ตอนโปรแกรมเด้งหรือค้าง",
        },
        new()
        {
            Kind = CleanupKind.GpuCache, Icon = "", IsSelected = false,
            Name = "Cache การ์ดจอ (NVIDIA / DirectX)",
            Description = "ลบแล้วเกมอาจกระตุกแป๊บนึงตอนเปิดครั้งแรก เพราะต้องสร้างใหม่",
        },
        new()
        {
            Kind = CleanupKind.OldDownloads, Icon = "", IsSelected = false,
            Name = "Downloads ที่เก่ากว่า 30 วัน",
            Description = "ย้ายไปถังขยะ ไม่ได้ลบถาวร กู้คืนได้",
        },
    ];

    public static ScanResult Scan(CleanupKind kind)
    {
        switch (kind)
        {
            case CleanupKind.RecycleBin:
                return new ScanResult([], RecycleBin.GetSize());

            case CleanupKind.OldDownloads:
                var cutoff = DateTime.Now - DownloadsMinAge;
                return Collect(EnumerateFiles(Downloads, TopLevelOnly)
                    .Where(f => f.CreationTime < cutoff && f.LastWriteTime < cutoff));

            case CleanupKind.Temp:
                var tempCutoff = DateTime.Now - TempMinAge;
                return Collect(GetFolders(kind).SelectMany(d => EnumerateFiles(d, Recursive))
                    .Where(f => f.LastWriteTime < tempCutoff));

            default:
                return Collect(GetFolders(kind).SelectMany(d => EnumerateFiles(d, Recursive)));
        }
    }

    /// <summary>ลบไฟล์ตามผลสแกน progress รายงานความคืบหน้า 0.0 - 1.0</summary>
    public static CleanResult Clean(CleanupKind kind, ScanResult scan, IProgress<double>? progress = null)
    {
        switch (kind)
        {
            case CleanupKind.RecycleBin:
                return new CleanResult(RecycleBin.Empty() ? scan.TotalBytes : 0, 0);

            case CleanupKind.OldDownloads:
                RecycleBin.Send(scan.Files.Select(f => f.Path).ToList());
                var moved = scan.Files.Where(f => !File.Exists(f.Path)).ToList();
                return new CleanResult(moved.Sum(f => f.Size), scan.Files.Count - moved.Count);
        }

        long freed = 0;
        int skipped = 0;
        for (var i = 0; i < scan.Files.Count; i++)
        {
            var file = scan.Files[i];
            try
            {
                File.SetAttributes(file.Path, FileAttributes.Normal); // ปลดล็อกไฟล์ read-only
                File.Delete(file.Path);
                freed += file.Size;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                skipped++; // ไฟล์ที่โปรแกรมกำลังใช้อยู่ ข้ามไป
            }

            if (i % 200 == 0) progress?.Report((double)i / scan.Files.Count);
        }

        foreach (var folder in GetFolders(kind)) RemoveEmptySubfolders(folder);
        return new CleanResult(freed, skipped);
    }

    /// <summary>โฟลเดอร์ที่เก็บขยะของแต่ละหมวด (เฉพาะที่มีอยู่จริง)</summary>
    private static IEnumerable<string> GetFolders(CleanupKind kind)
    {
        IEnumerable<string> folders = kind switch
        {
            CleanupKind.Temp =>
            [
                Path.GetTempPath(),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"),
            ],
            CleanupKind.BrowserCache => BrowserCacheFolders(),
            CleanupKind.DiscordCache =>
            [
                Path.Combine(RoamingAppData, "discord", "Cache"),
                Path.Combine(RoamingAppData, "discord", "Code Cache"),
                Path.Combine(RoamingAppData, "discord", "GPUCache"),
            ],
            CleanupKind.CrashReports =>
            [
                Path.Combine(LocalAppData, "CrashDumps"),
                Path.Combine(LocalAppData, "Microsoft", "Windows", "WER", "ReportArchive"),
                Path.Combine(LocalAppData, "Microsoft", "Windows", "WER", "ReportQueue"),
            ],
            CleanupKind.GpuCache =>
            [
                Path.Combine(LocalAppData, "NVIDIA", "DXCache"),
                Path.Combine(LocalAppData, "NVIDIA", "GLCache"),
                Path.Combine(LocalAppData, "D3DSCache"),
                Path.Combine(LocalAppData, "AMD", "DxCache"),
            ],
            _ => [],
        };
        return folders.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>โฟลเดอร์แคชของ Chrome/Edge ทุกโปรไฟล์ (Default, Profile 1, ...)</summary>
    private static IEnumerable<string> BrowserCacheFolders()
    {
        string[] browsers =
        [
            Path.Combine(LocalAppData, "Google", "Chrome", "User Data"),
            Path.Combine(LocalAppData, "Microsoft", "Edge", "User Data"),
        ];

        foreach (var userData in browsers.Where(Directory.Exists))
        {
            yield return Path.Combine(userData, "ShaderCache");
            yield return Path.Combine(userData, "GrShaderCache");

            foreach (var profile in Directory.EnumerateDirectories(userData))
            {
                var name = Path.GetFileName(profile);
                if (name != "Default" && !name.StartsWith("Profile ", StringComparison.Ordinal)) continue;

                yield return Path.Combine(profile, "Cache");
                yield return Path.Combine(profile, "Code Cache");
                yield return Path.Combine(profile, "GPUCache");
            }
        }
    }

    private static IEnumerable<FileInfo> EnumerateFiles(string folder, EnumerationOptions options)
    {
        try
        {
            return new DirectoryInfo(folder).EnumerateFiles("*", options);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static ScanResult Collect(IEnumerable<FileInfo> files)
    {
        var found = new List<FoundFile>();
        long total = 0;
        try
        {
            foreach (var file in files)
            {
                try
                {
                    found.Add(new FoundFile(file.FullName, file.Length));
                    total += file.Length;
                }
                catch (IOException)
                {
                    // ไฟล์หายไประหว่างสแกน ข้ามไป
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // โฟลเดอร์หายไประหว่างสแกน ใช้เท่าที่เจอ
        }
        return new ScanResult(found, total);
    }

    /// <summary>ลบโฟลเดอร์ย่อยที่ว่างแล้ว (ไม่ลบตัวโฟลเดอร์หลัก และไม่แตะโฟลเดอร์ที่เพิ่งถูกแก้ไข)</summary>
    private static void RemoveEmptySubfolders(string root)
    {
        var cutoff = DateTime.Now - TempMinAge;
        List<DirectoryInfo> subfolders;
        try
        {
            subfolders = new DirectoryInfo(root).EnumerateDirectories("*", Recursive)
                .OrderByDescending(d => d.FullName.Length) // ลบจากชั้นในสุดออกมา
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return;
        }

        foreach (var folder in subfolders)
        {
            try
            {
                if (folder.LastWriteTime < cutoff && !folder.EnumerateFileSystemInfos().Any())
                    folder.Delete();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // โฟลเดอร์ที่ใช้งานอยู่ ข้ามไป
            }
        }
    }
}
