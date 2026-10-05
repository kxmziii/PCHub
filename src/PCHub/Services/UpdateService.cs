using Velopack;
using Velopack.Sources;

namespace PCHub.Services;

/// <summary>เช็คและติดตั้งอัปเดตจาก GitHub Releases (ทำงานเฉพาะตอนติดตั้งผ่าน Setup.exe แล้ว)</summary>
public static class UpdateService
{
    /// <summary>
    /// ที่อยู่ GitHub ที่ใช้ปล่อยเวอร์ชันใหม่ เช่น "https://github.com/ชื่อบัญชี/PCHub"
    /// ว่างไว้ = ยังไม่เปิดระบบอัปเดต
    /// </summary>
    public const string GitHubRepo = "https://github.com/kxmziii/PCHub";

    /// <summary>ใช้ระบบอัปเดตได้ไหม (ต้องติดตั้งผ่าน Setup.exe) ถามไม่ได้ = ไม่ได้</summary>
    public static bool IsAvailable
    {
        get
        {
            if (GitHubRepo.Length == 0) return false;
            try
            {
                return Manager.IsInstalled;
            }
            catch (InvalidOperationException)
            {
                return false; // Velopack ยังไม่ได้เริ่มทำงาน (เช่น โปรแกรมทดสอบ)
            }
        }
    }

    private static UpdateManager? _manager;
    private static UpdateManager Manager => _manager ??= new UpdateManager(new GithubSource(GitHubRepo, null, false));

    /// <summary>มีเวอร์ชันใหม่ไหม (null = ไม่มี หรือเช็คไม่ได้)</summary>
    public static async Task<UpdateInfo?> CheckAsync()
    {
        if (!IsAvailable) return null;
        try
        {
            return await Manager.CheckForUpdatesAsync();
        }
        catch (Exception ex) when (ex is System.Net.Http.HttpRequestException or InvalidOperationException)
        {
            AppLog.Warn("Update check failed", ex);
            return null; // ไม่มีเน็ต หรือ GitHub ยังไม่มี release
        }
    }

    /// <summary>
    /// ใช้ตอนเปิดโปรแกรม: เช็ค → ถ้ามีเวอร์ชันใหม่ โหลดเงียบๆ เบื้องหลัง แล้วตั้งให้ติดตั้งตอนปิดโปรแกรม
    /// คืนค่าเวอร์ชันใหม่ที่โหลดไว้แล้ว (null = ไม่มีอัปเดต)
    /// </summary>
    public static async Task<UpdateInfo?> PrepareAsync()
    {
        var update = await CheckAsync();
        if (update == null) return null;
        AppLog.Info($"Update found: {update.TargetFullRelease.Version}");
        try
        {
            await Manager.DownloadUpdatesAsync(update);
        }
        catch (Exception ex) when (ex is System.Net.Http.HttpRequestException or System.IO.IOException)
        {
            AppLog.Warn("Update download failed", ex);
            return null; // โหลดไม่สำเร็จ ไว้ลองใหม่ตอนเปิดครั้งหน้า
        }
        Manager.WaitExitThenApplyUpdates(update.TargetFullRelease, silent: true, restart: false);
        AppLog.Info($"Update {update.TargetFullRelease.Version} downloaded, will apply on exit");
        return update;
    }

    /// <summary>ติดตั้งอัปเดตที่โหลดไว้แล้วรีสตาร์ทเป็นเวอร์ชันใหม่ทันที (args เช่น --tray = เปิดใหม่แบบย่อไว้มุมจอ)</summary>
    public static void RestartNow(UpdateInfo update, string[]? args = null)
    {
        AppLog.Info($"Restarting into {update.TargetFullRelease.Version} args=[{string.Join(' ', args ?? [])}]");
        Manager.ApplyUpdatesAndRestart(update.TargetFullRelease, args);
    }

    /// <summary>โหลดอัปเดตแล้วรีสตาร์ทเป็นเวอร์ชันใหม่</summary>
    public static async Task DownloadAndRestartAsync(UpdateInfo update)
    {
        await Manager.DownloadUpdatesAsync(update);
        Manager.ApplyUpdatesAndRestart(update.TargetFullRelease);
    }
}
