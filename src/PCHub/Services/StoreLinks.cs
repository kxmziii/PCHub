using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Win32;

namespace PCHub.Services;

/// <summary>เปิดหน้าร้านเกมในแอพ Steam / Epic ถ้าลงไว้ ไม่งั้นเปิดเว็บแทน</summary>
public static class StoreLinks
{
    public static string Steam(int appId) => $"steam://store/{appId}";

    public static string Epic(string slug) => $"com.epicgames.launcher://store/p/{slug}";

    public static void Open(string? appUrl, string webUrl)
    {
        if (appUrl != null && IsRegistered(appUrl.Split(':')[0]) && TryStart(appUrl)) return;
        TryStart(webUrl);
    }

    /// <summary>ลิงก์แบบนี้ (เช่น steam://) มีแอพในเครื่องรับเปิดไหม</summary>
    private static bool IsRegistered(string scheme)
    {
        using var key = Registry.ClassesRoot.OpenSubKey($@"{scheme}\shell\open\command");
        return key != null;
    }

    private static bool TryStart(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })?.Dispose();
            return true;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }
}
