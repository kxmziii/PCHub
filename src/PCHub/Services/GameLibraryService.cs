using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using PCHub.Models;

namespace PCHub.Services;

/// <summary>
/// หาเกมที่ลงไว้ในเครื่องจากแต่ละ launcher (แค่อ่านรายชื่อ ไม่แตะไฟล์เกม)
/// เรียกจาก Task.Run เพราะต้องอ่านไฟล์หลายไฟล์
/// </summary>
public static class GameLibraryService
{
    private static readonly string CommonAppData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

    // ของใน Steam ที่ไม่ใช่เกม
    private static readonly HashSet<string> SteamToolIds =
        ["228980" /* Steamworks Common Redistributables */, "250820" /* SteamVR */];

    private static readonly Dictionary<string, (string Name, string Exe)> RiotProducts = new()
    {
        ["valorant"] = ("VALORANT", "VALORANT.exe"),
        ["league_of_legends"] = ("League of Legends", "LeagueClient.exe"),
        ["bacon"] = ("Legends of Runeterra", "LoR.exe"),
        ["lion"] = ("2XKO", "2XKO.exe"),
    };

    // เกมหรือ launcher ที่ไม่มีระบบให้อ่าน แต่หาได้จาก shortcut ใน Start Menu
    private static readonly string[] KnownGameShortcuts =
    [
        "Roblox Player", "FiveM", "Modrinth App", "Minecraft Launcher", "Garena", "VALORANT", "League of Legends",
        "Genshin Impact", "Honkai Star Rail", "Zenless Zone Zero", "Wuthering Waves", "osu!", "Battle.net",
        "Overwatch", "Marvel Rivals", "Delta Force",
    ];

    private static Task<List<Game>>? _cached;

    /// <summary>
    /// รายชื่อเกม (หาครั้งเดียวตอนหน้าโหลด แล้วทุกหน้าใช้ร่วมกัน)
    /// refresh = true เพื่อหาใหม่ เช่น หลังลงเกมใหม่หรือเพิ่มเกมเอง
    /// </summary>
    public static Task<List<Game>> GetAsync(bool refresh = false)
    {
        if (refresh || _cached == null || _cached.IsFaulted) _cached = Task.Run(Scan);
        return _cached;
    }

    public static List<Game> Scan()
    {
        var games = new List<Game>();
        TryAdd(games, SteamGames);
        TryAdd(games, EpicGames);
        TryAdd(games, RiotGames);
        TryAdd(games, OtherGames);
        games.AddRange(CustomGames());

        // ชื่อซ้ำ (เช่น VALORANT ทั้งจาก Riot และ Start Menu) เก็บอันแรก
        var result = games
            .GroupBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
        PlayTimeService.ApplyTo(result);
        AppLog.Info($"Game scan: {result.Count} games ({string.Join(", ", result.GroupBy(g => g.Source).Select(g => $"{g.Key} {g.Count()}"))})");
        return result;
    }

    /// <summary>launcher ไหนข้อมูลเสียหรืออ่านไม่ได้ ก็ข้ามไป ไม่ให้ทั้งคลังพัง</summary>
    private static void TryAdd(List<Game> games, Func<IEnumerable<Game>> source)
    {
        try
        {
            games.AddRange(source());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException
                                       or InvalidOperationException or System.Security.SecurityException)
        {
            AppLog.Warn($"Game scan step {source.Method.Name} failed", ex); // ข้าม launcher นี้
        }
    }

    // ===== Steam =====

    private static IEnumerable<Game> SteamGames()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
        if (key?.GetValue("SteamPath") is not string steamPath) yield break;
        var steam = Path.GetFullPath(steamPath);

        // ไลบรารีเกมอาจอยู่หลายไดรฟ์ (เช่น C:\Program Files (x86)\Steam, D:\SteamLibrary)
        var libraries = new List<string> { steam };
        var libraryFile = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
        if (File.Exists(libraryFile))
        {
            libraries.AddRange(Regex.Matches(File.ReadAllText(libraryFile), "\"path\"\\s+\"(.+?)\"")
                .Select(m => m.Groups[1].Value.Replace(@"\\", @"\")));
        }

        foreach (var library in libraries.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var steamApps = Path.Combine(library, "steamapps");
            if (!Directory.Exists(steamApps)) continue;

            foreach (var manifest in Directory.EnumerateFiles(steamApps, "appmanifest_*.acf"))
            {
                var text = File.ReadAllText(manifest);
                var id = VdfValue(text, "appid");
                var name = VdfValue(text, "name");
                if (id == null || name == null || SteamToolIds.Contains(id) ||
                    name.Contains("Redistributable", StringComparison.OrdinalIgnoreCase)) continue;

                var installDir = VdfValue(text, "installdir");
                var size = long.TryParse(VdfValue(text, "SizeOnDisk"), out var bytes) && bytes > 0 ? bytes : (long?)null;
                var cache = Path.Combine(steam, "appcache", "librarycache", id);
                yield return new Game
                {
                    Id = $"steam:{id}",
                    Name = name,
                    Source = GameSource.Steam,
                    LaunchTarget = $"steam://rungameid/{id}",
                    CoverPath = FindSteamCover(steam, cache, id),
                    IconPath = FindSteamIcon(cache),
                    InstallFolder = installDir == null ? null : Path.Combine(steamApps, "common", installDir),
                    SizeOnDisk = size,
                };
            }
        }
    }

    private static string? VdfValue(string text, string key)
    {
        var match = Regex.Match(text, $"\"{key}\"\\s+\"((?:[^\"\\\\]|\\\\.)*)\"");
        return match.Success ? match.Groups[1].Value.Replace("\\\"", "\"").Replace(@"\\", @"\") : null;
    }

    /// <summary>รูปปกแนวตั้งที่ Steam เก็บไว้ในเครื่อง ถ้าไม่มีใช้ลิงก์จากเซิร์ฟเวอร์ Steam</summary>
    private static string FindSteamCover(string steam, string cacheFolder, string id)
    {
        if (Directory.Exists(cacheFolder))
        {
            foreach (var fileName in new[] { "library_600x900.jpg", "library_capsule.jpg" })
            {
                var found = Directory.EnumerateFiles(cacheFolder, fileName, SearchOption.AllDirectories).FirstOrDefault();
                if (found != null) return found;
            }
        }

        var oldLayout = Path.Combine(steam, "appcache", "librarycache", $"{id}_library_600x900.jpg");
        return File.Exists(oldLayout)
            ? oldLayout
            : $"https://shared.cloudflare.steamstatic.com/store_item_assets/steam/apps/{id}/library_600x900.jpg";
    }

    /// <summary>ไอคอนเกมของ Steam คือไฟล์ .jpg ที่ชื่อเป็นรหัส 40 ตัวอักษร</summary>
    private static string? FindSteamIcon(string cacheFolder) =>
        Directory.Exists(cacheFolder)
            ? Directory.EnumerateFiles(cacheFolder, "*.jpg").FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).Length == 40)
            : null;

    // ===== Epic Games =====

    private record EpicManifest(
        string? DisplayName, string? AppName, string? CatalogNamespace, string? CatalogItemId,
        string? InstallLocation, string? LaunchExecutable, bool bIsIncompleteInstall, List<string>? AppCategories,
        long InstallSize);

    private static IEnumerable<Game> EpicGames()
    {
        var folder = Path.Combine(CommonAppData, "Epic", "EpicGamesLauncher", "Data", "Manifests");
        if (!Directory.Exists(folder)) yield break;

        foreach (var file in Directory.EnumerateFiles(folder, "*.item"))
        {
            EpicManifest? m;
            try
            {
                m = JsonSerializer.Deserialize<EpicManifest>(File.ReadAllText(file));
            }
            catch (JsonException)
            {
                continue;
            }

            if (m?.DisplayName == null || m.AppName == null || m.bIsIncompleteInstall ||
                m.InstallLocation == null || !Directory.Exists(m.InstallLocation)) continue;
            if (m.AppCategories != null && !m.AppCategories.Contains("games")) continue;

            var exe = m.LaunchExecutable == null ? null : Path.Combine(m.InstallLocation, m.LaunchExecutable);
            yield return new Game
            {
                Id = $"epic:{m.AppName}",
                Name = m.DisplayName,
                Source = GameSource.Epic,
                // เปิดผ่าน Epic launcher เกมจะได้อัปเดตและล็อกอินตามปกติ
                LaunchTarget = $"com.epicgames.launcher://apps/{m.CatalogNamespace}%3A{m.CatalogItemId}%3A{m.AppName}?action=launch&silent=true",
                IconPath = exe != null && File.Exists(exe) ? exe : null,
                InstallFolder = m.InstallLocation,
                SizeOnDisk = m.InstallSize > 0 ? m.InstallSize : null,
            };
        }
    }

    // ===== Riot Games =====

    private static IEnumerable<Game> RiotGames()
    {
        var root = Path.Combine(CommonAppData, "Riot Games");
        var installsFile = Path.Combine(root, "RiotClientInstalls.json");
        var metadata = Path.Combine(root, "Metadata");
        if (!File.Exists(installsFile) || !Directory.Exists(metadata)) yield break;

        using var installs = JsonDocument.Parse(File.ReadAllText(installsFile));
        if (!installs.RootElement.TryGetProperty("rc_default", out var clientValue) ||
            clientValue.GetString() is not { } clientPath || !File.Exists(clientPath)) yield break;
        var client = Path.GetFullPath(clientPath);

        // เกมที่ลงแล้วจะมีไฟล์ <เกม>.live.product_settings.yaml ที่บอกที่อยู่เกม
        foreach (var settings in Directory.EnumerateFiles(metadata, "*.live.product_settings.yaml", SearchOption.AllDirectories))
        {
            var product = Path.GetFileName(settings).Split('.')[0];
            if (!RiotProducts.TryGetValue(product, out var info)) continue;

            var match = Regex.Match(File.ReadAllText(settings), "product_install_full_path:\\s*\"?([^\"\\r\\n]+)");
            if (!match.Success || !Directory.Exists(match.Groups[1].Value)) continue;

            var installFolder = Path.GetFullPath(match.Groups[1].Value);
            var exe = Path.Combine(installFolder, info.Exe);
            yield return new Game
            {
                Id = $"riot:{product}",
                Name = info.Name,
                Source = GameSource.Riot,
                LaunchTarget = client,
                LaunchArguments = $"--launch-product={product} --launch-patchline=live",
                IconPath = File.Exists(exe) ? exe : client,
                InstallFolder = installFolder,
            };
        }
    }

    // ===== อื่นๆ (จาก Start Menu) และเกมที่เพิ่มเอง =====

    private static IEnumerable<Game> OtherGames() =>
        InstalledAppsService.GetAll()
            .Where(app => KnownGameShortcuts.Contains(app.Name, StringComparer.OrdinalIgnoreCase))
            .Select(app => new Game
            {
                Id = $"other:{app.Name}",
                Name = app.Name,
                Source = GameSource.Other,
                LaunchTarget = app.Path,
                IconPath = app.Path,
                InstallFolder = TargetFolder(app.Path),
            });

    private static IEnumerable<Game> CustomGames() =>
        SettingsService.Current.CustomGames.Select(app => new Game
        {
            Id = $"custom:{app.Path}",
            Name = app.Name,
            Source = GameSource.Custom,
            LaunchTarget = app.Path,
            LaunchArguments = app.Arguments,
            IconPath = app.IconSource,
            InstallFolder = TargetFolder(app.Path),
        });

    /// <summary>โฟลเดอร์ของโปรแกรมที่ไฟล์นี้ชี้ไป (.lnk จะดูว่า shortcut ชี้ไปที่ไหน)</summary>
    private static string? TargetFolder(string path)
    {
        if (path.Contains("://", StringComparison.Ordinal)) return null;
        if (!path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) return Path.GetDirectoryName(path);

        var shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType == null) return null;
        dynamic? shell = null;
        try
        {
            shell = Activator.CreateInstance(shellType);
            string target = shell!.CreateShortcut(path).TargetPath;
            return string.IsNullOrEmpty(target) ? null : Path.GetDirectoryName(target);
        }
        catch (COMException)
        {
            return null;
        }
        finally
        {
            if (shell != null) Marshal.FinalReleaseComObject(shell);
        }
    }
}
