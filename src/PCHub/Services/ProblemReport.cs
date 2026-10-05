using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using PCHub.Helpers;

namespace PCHub.Services;

/// <summary>
/// รวมไฟล์บันทึก + ข้อมูลเครื่องเป็น .zip บน Desktop ให้ผู้ใช้ส่งมาให้คนทำ PC Hub
/// มีแค่ข้อมูลที่ช่วยหาบั๊ก: ไม่มีรหัสผ่าน ไม่มีไฟล์ส่วนตัว ไม่มีไฟล์ตั้งค่าดิบ
/// </summary>
public static class ProblemReport
{
    public static string Create()
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var path = Path.Combine(desktop, $"PCHub-report-{DateTime.Now:yyyyMMdd-HHmm}.zip");
        AppLog.Info("Creating problem report");

        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        if (Directory.Exists(AppLog.FolderPath))
        {
            foreach (var log in Directory.EnumerateFiles(AppLog.FolderPath, "pchub-*.log"))
            {
                // อ่านแบบแชร์ไฟล์ เผื่อโปรแกรมกำลังเขียนบันทึกอยู่
                using var source = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var target = zip.CreateEntry("logs/" + Path.GetFileName(log)).Open();
                source.CopyTo(target);
            }
        }

        using (var writer = new StreamWriter(zip.CreateEntry("system-info.txt").Open(), Encoding.UTF8))
            writer.Write(SystemInfo());
        return path;
    }

    /// <summary>ข้อมูลเครื่อง ถ้าส่วนไหนอ่านไม่ได้ก็จดไว้แล้วข้ามไป ไฟล์รายงานต้องสร้างได้เสมอ</summary>
    private static string SystemInfo()
    {
        var text = new StringBuilder();
        try
        {
            AppendSystemInfo(text);
        }
        catch (Exception ex)
        {
            // ไฟล์รายงานใช้ตอนโปรแกรมมีปัญหา จะพังเพราะอ่านข้อมูลบางส่วนไม่ได้ไม่ได้
            text.AppendLine($"(collecting system info failed: {ex.GetType().Name}: {ex.Message})");
        }
        return text.ToString();
    }

    private static void AppendSystemInfo(StringBuilder text)
    {
        using var self = Process.GetCurrentProcess();
        text.AppendLine($"PC Hub: {AppLog.Version} (installed: {UpdateService.IsAvailable})");
        text.AppendLine($"Windows: {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})");
        text.AppendLine($".NET: {RuntimeInformation.FrameworkDescription}");
        text.AppendLine($"CPU: {CpuName()} ({Environment.ProcessorCount} threads)");
        text.AppendLine($"RAM: {SizeFormatter.Format(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes)}");
        text.AppendLine($"PC Hub memory: working set {SizeFormatter.Format(self.WorkingSet64)}, private {SizeFormatter.Format(self.PrivateMemorySize64)}");
        foreach (var drive in StorageService.FixedDrives())
            text.AppendLine($"Drive {drive.Name}: free {SizeFormatter.Format(drive.TotalFreeSpace)} of {SizeFormatter.Format(drive.TotalSize)}");

        var settings = SettingsService.Current;
        var boost = SettingsService.Boost;
        text.AppendLine();
        text.AppendLine($"Modes: {settings.Modes.Count}, custom games: {settings.CustomGames.Count}, hidden: {settings.HiddenGames.Count}, watched prices: {settings.WatchedGames.Count}");
        text.AppendLine($"Boost: enabled={boost.Enabled} power={boost.HighPerformance} closeApps={boost.CloseApps.Count} companions={boost.CompanionApps.Count} restartDiscord={boost.RestartHeavyDiscord}");
        text.AppendLine($"General: tray={settings.General.MinimizeToTray} autoTrack={settings.General.AutoTrackGames} alerts={settings.General.DealAlerts} confirmPlay={settings.General.ConfirmBeforePlay} startup={StartupService.IsEnabled}");
        text.AppendLine($"Launchers: steam={SteamAccount.SteamFolder() != null} fivem={FiveMService.IsInstalled}");
    }

    private static string CpuName()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
        return (key?.GetValue("ProcessorNameString") as string)?.Trim() ?? "?";
    }
}
