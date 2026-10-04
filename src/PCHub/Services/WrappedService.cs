using System.Globalization;
using PCHub.Models;

namespace PCHub.Services;

public enum WrappedPeriod
{
    ThisMonth,
    LastMonth,
    SteamTwoWeeks,
}

public record WrappedGame(string Name, TimeSpan Time, string? CoverPath);

/// <summary>สรุปการเล่น 1 ช่วงเวลา</summary>
public record WrappedSummary(
    string Title,             // เช่น "ตุลาคม 2569"
    string SourceNote,        // ข้อมูลมาจากไหน
    TimeSpan Total,
    int SessionCount,         // 0 = ไม่รู้ (ข้อมูลจาก Steam)
    List<WrappedGame> Games,  // เรียงจากเล่นนานสุด
    PlaySession? Longest,
    string? FavoriteDay,
    string Personality,
    string PersonalityDetail);

/// <summary>ทำสรุป Gaming Wrapped จากประวัติการเล่นของ PC Hub (หรือข้อมูล 2 สัปดาห์ของ Steam)</summary>
public static class WrappedService
{
    private static readonly CultureInfo Thai = new("th-TH");

    /// <summary>null = ช่วงนี้ยังไม่มีข้อมูลการเล่น</summary>
    public static WrappedSummary? Build(WrappedPeriod period, IReadOnlyList<Game> games) =>
        period == WrappedPeriod.SteamTwoWeeks ? FromSteam(games) : FromSessions(period, games);

    private static WrappedSummary? FromSessions(WrappedPeriod period, IReadOnlyList<Game> games)
    {
        var thisMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var (start, end) = period == WrappedPeriod.ThisMonth
            ? (thisMonth, DateTime.MaxValue)
            : (thisMonth.AddMonths(-1), thisMonth);

        var sessions = PlayTimeService.Sessions.Where(s => s.Start >= start && s.Start < end).ToList();
        if (sessions.Count == 0) return null;

        var covers = games.ToDictionary(g => g.Id, g => g.CoverPath);
        var perGame = sessions
            .GroupBy(s => s.GameId)
            .Select(g => new WrappedGame(g.Last().GameName, Sum(g.Select(s => s.Duration)), covers.GetValueOrDefault(g.Key)))
            .OrderByDescending(g => g.Time)
            .ToList();

        var total = Sum(sessions.Select(s => s.Duration));
        var favoriteDay = sessions
            .GroupBy(s => s.Start.DayOfWeek)
            .OrderByDescending(g => Sum(g.Select(s => s.Duration)))
            .First().Key;
        var lateNightRatio = (double)sessions.Count(s => s.Start.Hour < 5) / sessions.Count;
        var days = period == WrappedPeriod.ThisMonth ? DateTime.Today.Day : DateTime.DaysInMonth(start.Year, start.Month);
        var (personality, detail) = Personality(total, days, perGame, lateNightRatio);

        return new WrappedSummary(
            start.ToString("MMMM yyyy", Thai),
            "จากเวลาเล่นที่ PC Hub จับไว้",
            total,
            sessions.Count,
            perGame,
            sessions.MaxBy(s => s.Duration),
            Thai.DateTimeFormat.GetDayName(favoriteDay),
            personality,
            detail);
    }

    private static WrappedSummary? FromSteam(IReadOnlyList<Game> games)
    {
        var twoWeeks = PlayTimeService.SteamTwoWeeks();
        var perGame = games
            .Where(g => g.Source == GameSource.Steam)
            .Select(g => (Game: g, Time: twoWeeks.GetValueOrDefault(g.Id["steam:".Length..])))
            .Where(x => x.Time > TimeSpan.Zero)
            .Select(x => new WrappedGame(x.Game.Name, x.Time, x.Game.CoverPath))
            .OrderByDescending(g => g.Time)
            .ToList();
        if (perGame.Count == 0) return null;

        var total = Sum(perGame.Select(g => g.Time));
        var (personality, detail) = Personality(total, 14, perGame, lateNightRatio: 0);
        return new WrappedSummary("2 สัปดาห์ล่าสุด", "จากข้อมูลเวลาเล่นของ Steam", total, 0, perGame, null, null, personality, detail);
    }

    /// <summary>สไตล์การเล่น (ตัวตลกๆ ไว้แชร์)</summary>
    private static (string Name, string Detail) Personality(TimeSpan total, int days, List<WrappedGame> games, double lateNightRatio)
    {
        var hoursPerWeek = total.TotalHours / Math.Max(1, days / 7.0);
        var topShare = total > TimeSpan.Zero ? games[0].Time / total : 0;

        if (lateNightRatio >= 0.3) return ("นกฮูกสายดึก", "ชอบเริ่มเล่นช่วงเที่ยงคืนถึงตีห้า");
        if (hoursPerWeek >= 20) return ("ตัวตึงสายฮาร์ดคอร์", $"เฉลี่ยสัปดาห์ละ {hoursPerWeek:0} ชม.");
        if (topShare >= 0.7) return ("รักเดียวใจเดียว", $"เวลา {topShare:P0} หมดไปกับ {games[0].Name}");
        if (games.Count >= 5) return ("นักสำรวจหลายโลก", $"ลองเล่นไปตั้ง {games.Count} เกม");
        return ("สายชิลเล่นเพลินๆ", "เล่นพอดีๆ ไม่หักโหม");
    }

    private static TimeSpan Sum(IEnumerable<TimeSpan> times) => TimeSpan.FromTicks(times.Sum(t => t.Ticks));
}
