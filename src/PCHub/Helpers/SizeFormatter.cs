namespace PCHub.Helpers;

public static class SizeFormatter
{
    /// <summary>แปลงจำนวนไบต์เป็นข้อความอ่านง่าย เช่น 950 MB, 1.82 GB</summary>
    public static string Format(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0} KB",
        < 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024):0.#} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):0.##} GB",
    };
}
