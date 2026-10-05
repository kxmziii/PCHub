using System.IO;
using System.Reflection;

namespace PCHub.Services;

/// <summary>
/// บันทึกการทำงานลงไฟล์ %APPDATA%\PCHub\logs\pchub-วันที่.log (เก็บ 7 วัน)
/// ใช้ดูว่าเกิดอะไรขึ้นตอนโปรแกรมมีปัญหาในเครื่องคนอื่น ไม่บันทึกรหัสผ่านหรือข้อมูลส่วนตัว
/// </summary>
public static class AppLog
{
    public static string FolderPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PCHub", "logs");

    private static readonly object Lock = new();

    public static string Version { get; } =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "?";

    public static void Info(string message) => Write("INFO", message);

    public static void Warn(string message, Exception? ex = null) =>
        Write("WARN", ex == null ? message : $"{message}: {ex.GetType().Name}: {ex.Message}");

    public static void Error(string message, Exception ex) => Write("ERROR", $"{message}{Environment.NewLine}{ex}");

    /// <summary>ลบไฟล์บันทึกที่เก่ากว่า 7 วัน</summary>
    public static void CleanupOld()
    {
        try
        {
            if (!Directory.Exists(FolderPath)) return;
            foreach (var file in new DirectoryInfo(FolderPath).EnumerateFiles("pchub-*.log"))
                if (file.LastWriteTime < DateTime.Now.AddDays(-7)) file.Delete();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // ลบไม่ได้ ไว้รอบหน้า
        }
    }

    private static void Write(string level, string message)
    {
        try
        {
            lock (Lock)
            {
                Directory.CreateDirectory(FolderPath);
                File.AppendAllText(Path.Combine(FolderPath, $"pchub-{DateTime.Now:yyyy-MM-dd}.log"),
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // การบันทึกต้องไม่ทำให้โปรแกรมพังเอง
        }
    }
}
