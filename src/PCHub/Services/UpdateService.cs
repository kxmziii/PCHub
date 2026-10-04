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
    public const string GitHubRepo = "";

    public static bool IsAvailable => GitHubRepo.Length > 0 && Manager.IsInstalled;

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
            return null; // ไม่มีเน็ต หรือ GitHub ยังไม่มี release
        }
    }

    /// <summary>โหลดอัปเดตแล้วรีสตาร์ทเป็นเวอร์ชันใหม่</summary>
    public static async Task DownloadAndRestartAsync(UpdateInfo update)
    {
        await Manager.DownloadUpdatesAsync(update);
        Manager.ApplyUpdatesAndRestart(update);
    }
}
