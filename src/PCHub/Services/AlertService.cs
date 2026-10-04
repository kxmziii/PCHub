using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Threading;
using PCHub.Models;

namespace PCHub.Services;

/// <summary>
/// เช็คของใหม่เบื้องหลังทุก 3 ชั่วโมง แล้วแจ้งที่มุมจอรวมเป็นข้อความเดียว:
/// เกมที่เฝ้าราคาลดราคา, Epic แจกเกมฟรีใหม่, เกมที่เพิ่งเล่นมีแพตช์ใหม่
/// จำไว้ใน %APPDATA%\PCHub\alerts.json ว่าแจ้งอะไรไปแล้ว จะได้ไม่แจ้งซ้ำ
/// </summary>
public sealed class AlertService
{
    // ต้องประกาศก่อน Instance (C# ตั้งค่าตัวแปร static ตามลำดับบรรทัด)
    private static readonly TimeSpan FirstCheckDelay = TimeSpan.FromMinutes(2); // ไม่แย่งเครื่องตอนเพิ่งเปิด
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(3);
    private static readonly TimeSpan RecentlyPlayed = TimeSpan.FromDays(60);
    private static readonly TimeSpan FreshPatch = TimeSpan.FromDays(7);
    private static readonly Regex PatchTitle = new(@"\b(patch|patches|hotfix|update|updates|release notes|changelog)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PCHub", "alerts.json");

    public static AlertService Instance { get; } = new();

    /// <summary>ข้อความแจ้งเตือน 1 ครั้ง (Page = หน้าที่เปิดเมื่อกดข้อความ)</summary>
    public record Alert(string Title, string Message, string Page);

    public event Action<Alert>? AlertRaised;

    private sealed class State
    {
        /// <summary>appid → ราคาที่แจ้งไปแล้ว (สตางค์) ถ้าลดลงกว่านี้อีกจะแจ้งใหม่</summary>
        public Dictionary<int, long> NotifiedSales { get; set; } = [];
        public List<string> SeenEpicFree { get; set; } = [];
        /// <summary>appid → id ของข่าวแพตช์ล่าสุดที่เห็นแล้ว</summary>
        public Dictionary<int, string> SeenPatches { get; set; } = [];
        public bool PatchBaselineDone { get; set; }
    }

    private readonly DispatcherTimer _timer = new() { Interval = FirstCheckDelay };
    private bool _checking;

    private AlertService() =>
        _timer.Tick += async (_, _) =>
        {
            _timer.Interval = CheckInterval;
            await CheckAsync();
        };

    public void Start() => _timer.Start();

    public async Task CheckAsync()
    {
        if (_checking || !SettingsService.General.DealAlerts) return;
        _checking = true;
        try
        {
            var state = LoadState();
            var deals = new List<string>();
            var patches = new List<string>();

            await Try(async () => deals.AddRange(await NewSalesAsync(state)));
            await Try(async () => deals.AddRange(await NewEpicFreeAsync(state)));
            await Try(async () => patches.AddRange(await NewPatchesAsync(state)));
            SaveState(state);

            var lines = deals.Concat(patches).ToList();
            if (lines.Count == 0) return;

            var title = lines.Count == 1 ? "PC Hub" : $"PC Hub: มีของใหม่ {lines.Count} อย่าง";
            var message = string.Join("\n", lines.Take(3)) + (lines.Count > 3 ? $"\nและอีก {lines.Count - 3} อย่าง" : "");
            AlertRaised?.Invoke(new Alert(title, message, deals.Count > 0 ? "deals" : "games"));
        }
        finally
        {
            _checking = false;
        }
    }

    private static async Task<List<string>> NewSalesAsync(State state)
    {
        var (games, _) = await PriceWatchService.GetWatchlistAsync();
        var lines = new List<string>();
        foreach (var game in games.Where(g => g.IsOnSale))
        {
            if (state.NotifiedSales.TryGetValue(game.AppId, out var notified) && notified <= game.FinalCents) continue;
            state.NotifiedSales[game.AppId] = game.FinalCents;
            lines.Add($"💸 {game.Name} ลด {game.DiscountPercent}% เหลือ {game.PriceText}");
        }

        // หมดโปรแล้วลบออก รอบหน้าลดอีกจะได้แจ้งใหม่
        var onSale = games.Where(g => g.IsOnSale).Select(g => g.AppId).ToHashSet();
        foreach (var id in state.NotifiedSales.Keys.Where(id => !onSale.Contains(id)).ToList()) state.NotifiedSales.Remove(id);
        return lines;
    }

    private static async Task<List<string>> NewEpicFreeAsync(State state)
    {
        var epic = await DealsService.GetEpicFreeGamesAsync();
        var fresh = epic.Now.Where(d => !state.SeenEpicFree.Contains(d.Title)).Select(d => d.Title).ToList();
        state.SeenEpicFree = state.SeenEpicFree.Concat(fresh).TakeLast(50).ToList();
        return fresh.Count > 0 ? [$"🎁 Epic แจกฟรี: {string.Join(", ", fresh)}"] : [];
    }

    /// <summary>แพตช์ใหม่ของเกม Steam ที่เล่นใน 60 วันล่าสุด (ครั้งแรกแค่จำไว้ ไม่แจ้ง)</summary>
    private static async Task<List<string>> NewPatchesAsync(State state)
    {
        var hidden = SettingsService.Current.HiddenGames.ToHashSet();
        var games = (await GameLibraryService.GetAsync())
            .Where(g => g.Source == GameSource.Steam && !hidden.Contains(g.Id) &&
                        g.LastPlayed is { } last && DateTime.Now - last < RecentlyPlayed);

        var lines = new List<string>();
        foreach (var game in games)
        {
            var appId = int.Parse(game.Id["steam:".Length..]);
            if (await LatestPatchAsync(appId) is not { } patch) continue;

            var seenBefore = state.SeenPatches.GetValueOrDefault(appId);
            state.SeenPatches[appId] = patch.Id;
            if (state.PatchBaselineDone && seenBefore != patch.Id && DateTime.Now - patch.Date < FreshPatch)
                lines.Add($"🛠️ {game.Name}: {patch.Title}");
        }
        state.PatchBaselineDone = true;
        return lines;
    }

    /// <summary>ข่าวแพตช์ล่าสุดของเกม (ข้ามข่าวอื่น เช่น ถ่ายทอดสดแข่ง)</summary>
    private static async Task<(string Id, string Title, DateTime Date)?> LatestPatchAsync(int appId)
    {
        using var doc = JsonDocument.Parse(await Web.Client.GetStringAsync(
            $"https://api.steampowered.com/ISteamNews/GetNewsForApp/v2/?appid={appId}&count=10&feeds=steam_community_announcements"));
        foreach (var item in doc.RootElement.GetProperty("appnews").GetProperty("newsitems").EnumerateArray())
        {
            var title = item.GetProperty("title").GetString() ?? "";
            if (!PatchTitle.IsMatch(title)) continue;
            var date = DateTimeOffset.FromUnixTimeSeconds(item.GetProperty("date").GetInt64()).LocalDateTime;
            return (item.GetProperty("gid").GetString() ?? "", title, date);
        }
        return null;
    }

    /// <summary>เช็คแต่ละอย่างแยกกัน อันไหนพัง (เช่น เน็ตหลุด, ร้านเปลี่ยนรูปแบบข้อมูล) ก็ข้ามไป</summary>
    private static async Task Try(Func<Task> check)
    {
        try
        {
            await check();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException
                                       or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            // ข้ามไป รอบหน้าค่อยลองใหม่
        }
    }

    private static State LoadState()
    {
        try
        {
            return File.Exists(FilePath) ? JsonSerializer.Deserialize<State>(File.ReadAllText(FilePath)) ?? new() : new();
        }
        catch (JsonException)
        {
            return new();
        }
    }

    private static void SaveState(State state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(state));
    }
}
