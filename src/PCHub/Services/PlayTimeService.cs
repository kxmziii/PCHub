using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using PCHub.Helpers;
using PCHub.Models;

namespace PCHub.Services;

/// <summary>เก็บประวัติการเล่นที่ %APPDATA%\PCHub\playtime.json และดึงเวลาเล่นที่ Steam เก็บไว้มารวมด้วย</summary>
public static class PlayTimeService
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PCHub", "playtime.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly object Lock = new();
    private static List<PlaySession>? _sessions;

    /// <summary>ทุกครั้งที่เล่น (ใหม่สุดอยู่ท้าย) เป็นสำเนา อ่านจากเธรดไหนก็ได้</summary>
    public static IReadOnlyList<PlaySession> Sessions
    {
        get
        {
            lock (Lock) return (_sessions ??= Load()).ToList();
        }
    }

    public static void Record(PlaySession session)
    {
        lock (Lock)
        {
            _sessions ??= Load();
            _sessions.Add(session);
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(_sessions, JsonOptions));
        }
    }

    /// <summary>ใส่เวลาเล่นรวมและวันที่เล่นล่าสุดให้ทุกเกม</summary>
    public static void ApplyTo(IEnumerable<Game> games)
    {
        var steam = LoadSteamStats();
        var sessions = Sessions;
        foreach (var game in games)
        {
            var own = sessions.Where(s => s.GameId == game.Id).ToList();
            var playTime = TimeSpan.FromTicks(own.Sum(s => s.Duration.Ticks));
            DateTime? lastPlayed = own.Count > 0 ? own.Max(s => s.End) : null;

            // Steam นับเวลาเกม Steam ทุกครั้งอยู่แล้ว (รวมตอนเปิดผ่าน PC Hub ด้วย) เลยใช้ค่าที่มากกว่า
            if (game.Source == GameSource.Steam && steam.TryGetValue(game.Id["steam:".Length..], out var stats))
            {
                if (stats.PlayTime > playTime) playTime = stats.PlayTime;
                if (stats.LastPlayed is { } steamLast && steamLast > (lastPlayed ?? DateTime.MinValue)) lastPlayed = steamLast;
            }

            game.PlayTime = playTime;
            game.LastPlayed = lastPlayed;
        }
    }

    private static List<PlaySession> Load()
    {
        if (!File.Exists(FilePath)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<PlaySession>>(File.ReadAllText(FilePath), JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            File.Copy(FilePath, FilePath + ".broken", overwrite: true);
            return [];
        }
    }

    /// <summary>เวลาเล่นใน 2 สัปดาห์ล่าสุดที่ Steam จำไว้ key = appid (ใช้ทำ Gaming Wrapped ตอนยังไม่มีข้อมูลของ PC Hub)</summary>
    public static Dictionary<string, TimeSpan> SteamTwoWeeks() =>
        LoadSteamStats().Where(s => s.Value.TwoWeeks > TimeSpan.Zero).ToDictionary(s => s.Key, s => s.Value.TwoWeeks);

    private record SteamStat(TimeSpan PlayTime, DateTime? LastPlayed, TimeSpan TwoWeeks);

    /// <summary>เวลาเล่นที่ Steam จำไว้ (จากบัญชี Steam ที่ใช้ล่าสุดในเครื่อง) key = appid</summary>
    private static Dictionary<string, SteamStat> LoadSteamStats()
    {
        var result = new Dictionary<string, SteamStat>();
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            if (key?.GetValue("SteamPath") is not string steamPath) return result;

            var userdata = Path.Combine(Path.GetFullPath(steamPath), "userdata");
            if (!Directory.Exists(userdata)) return result;

            var config = Directory.EnumerateDirectories(userdata)
                .Select(d => Path.Combine(d, "config", "localconfig.vdf"))
                .Where(File.Exists)
                .OrderByDescending(File.GetLastWriteTime)
                .FirstOrDefault();
            if (config == null) return result;

            var root = VdfParser.Parse(File.ReadAllText(config));
            var apps = VdfParser.Find(root, "UserLocalConfigStore", "Software", "Valve", "Steam", "apps");
            if (apps == null) return result;

            foreach (var (appId, value) in apps)
            {
                if (value is not Dictionary<string, object> app) continue;
                DateTime? last = app.TryGetValue("LastPlayed", out var l) && long.TryParse(l as string, out var unix) && unix > 0
                    ? DateTimeOffset.FromUnixTimeSeconds(unix).LocalDateTime
                    : null;
                result[appId] = new SteamStat(Minutes(app, "Playtime"), last, Minutes(app, "Playtime2wks"));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // อ่านข้อมูล Steam ไม่ได้ ใช้แค่ข้อมูลของ PC Hub
        }
        return result;
    }

    private static TimeSpan Minutes(Dictionary<string, object> app, string key) =>
        app.TryGetValue(key, out var value) && long.TryParse(value as string, out var minutes)
            ? TimeSpan.FromMinutes(minutes)
            : TimeSpan.Zero;
}
