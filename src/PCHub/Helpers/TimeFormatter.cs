namespace PCHub.Helpers;

public static class TimeFormatter
{
    /// <summary>เวลาเล่นรวมแบบสั้น เช่น "38 ชม.", "45 นาที"</summary>
    public static string Short(TimeSpan time) => time.TotalMinutes switch
    {
        < 1 => "ไม่ถึงนาที",
        < 60 => $"{(int)time.TotalMinutes} นาที",
        < 600 => $"{time.TotalHours:0.#} ชม.",
        _ => $"{(int)time.TotalHours:N0} ชม.",
    };

    /// <summary>เวลาแบบละเอียด เช่น "1 ชม. 23 นาที"</summary>
    public static string Long(TimeSpan time)
    {
        if (time.TotalMinutes < 1) return "ไม่ถึงนาที";
        var hours = (int)time.TotalHours;
        return hours > 0 ? $"{hours} ชม. {time.Minutes} นาที" : $"{time.Minutes} นาที";
    }

    /// <summary>นาฬิกาจับเวลา เช่น "1:23:45"</summary>
    public static string Clock(TimeSpan time) => $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}";

    /// <summary>ผ่านมานานแค่ไหน เช่น "วันนี้", "3 วันก่อน"</summary>
    public static string Ago(DateTime time)
    {
        var days = (DateTime.Today - time.Date).Days;
        return days switch
        {
            <= 0 => "วันนี้",
            1 => "เมื่อวาน",
            < 7 => $"{days} วันก่อน",
            < 30 => $"{days / 7} สัปดาห์ก่อน",
            < 365 => $"{days / 30} เดือนก่อน",
            _ => $"{days / 365} ปีก่อน",
        };
    }
}
