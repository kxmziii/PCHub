using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using PCHub.Helpers;

namespace PCHub.Services;

/// <summary>เซิร์ฟ FiveM 1 เซิร์ฟ (Code = รหัสเข้าเซิร์ฟ แบบ cfx.re/join/xxxxxx)</summary>
public record FiveMServer(
    string Code,
    string Name,
    int Players,
    int MaxPlayers,
    string? IconUrl,
    string? PingHost,
    int PingPort);

/// <summary>
/// ดึงรายชื่อเซิร์ฟ FiveM (กรองเฉพาะเซิร์ฟไทย), กดเข้าเซิร์ฟ, ล้าง cache ของ FiveM
/// ใช้รายชื่อเซิร์ฟชุดเดียวกับที่ตัวเกมใช้ (API สาธารณะของ Cfx.re)
/// </summary>
public static partial class FiveMService
{
    private const string ServerListUrl = "https://frontend.cfx-services.net/api/servers/stream/";

    // รายชื่อเซิร์ฟทั้งโลก ~20 MB ให้เวลาโหลดนานกว่าปกติ
    private static readonly HttpClient Http = CreateClient();

    private static readonly string FiveMFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FiveM");
    private static readonly string DataFolder = Path.Combine(FiveMFolder, "FiveM.app", "data");

    /// <summary>
    /// โฟลเดอร์ cache ที่ลบได้ (เซิร์ฟจะโหลดใหม่เอง) ห้ามรวม game-storage (ไฟล์เกม GTA)
    /// และ nui-storage (ข้อมูลล็อกอินในเซิร์ฟ)
    /// </summary>
    private static readonly string[] CacheFolders = ["cache", "server-cache", "server-cache-priv"];

    private static List<FiveMServer>? _cached;
    private static DateTime _cachedAt;

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("PCHub");
        return client;
    }

    public static bool IsInstalled => File.Exists(Path.Combine(FiveMFolder, "FiveM.exe"));

    public static bool IsRunning => Process.GetProcesses().Any(p =>
    {
        using (p) return p.ProcessName.StartsWith("FiveM", StringComparison.OrdinalIgnoreCase);
    });

    /// <summary>เซิร์ฟไทยเรียงตามคนเล่นมากสุด (จำไว้ 3 นาที ไม่โหลด 20 MB ซ้ำบ่อย)</summary>
    public static async Task<List<FiveMServer>> GetThaiServersAsync(bool refresh = false)
    {
        if (!refresh && _cached != null && DateTime.Now - _cachedAt < TimeSpan.FromMinutes(3)) return _cached;

        var data = await Http.GetByteArrayAsync(ServerListUrl);
        var servers = await Task.Run(() => ParseThaiServers(data));
        _cached = servers.OrderByDescending(s => s.Players).Take(150).ToList();
        _cachedAt = DateTime.Now;
        return _cached;
    }

    /// <summary>เข้าเซิร์ฟ (เปิด FiveM แล้วต่อเซิร์ฟให้เลย ถ้า FiveM เปิดอยู่แล้วจะย้ายเซิร์ฟให้)</summary>
    public static bool Join(string code) => Launch($"fivem://connect/cfx.re/join/{code}");

    /// <summary>เปิด FiveM เฉยๆ หรือพร้อมคำสั่ง (ส่งลิงก์ fivem:// ให้ตัวเกมตรงๆ เพราะบางเครื่องลิงก์ไม่ได้ผูกไว้)</summary>
    public static bool Launch(string arguments = "")
    {
        var exe = Path.Combine(FiveMFolder, "FiveM.exe");
        if (!File.Exists(exe)) return false;
        Process.Start(new ProcessStartInfo(exe, arguments) { UseShellExecute = true })?.Dispose();
        return true;
    }

    /// <summary>ขนาด cache ของ FiveM ที่ล้างได้ (ไบต์) เรียกจาก Task.Run</summary>
    public static long CacheSize() =>
        CacheFolders.Select(f => Path.Combine(DataFolder, f)).Where(Directory.Exists).Sum(StorageService.FolderSize);

    /// <summary>ล้าง cache (ต้องปิด FiveM ก่อน) คืนค่าขนาดที่ลบได้ เรียกจาก Task.Run</summary>
    public static long ClearCache()
    {
        long freed = 0;
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint, // ไม่ตามลิงก์ออกไปลบไฟล์ที่อื่น
        };

        foreach (var folder in CacheFolders.Select(f => Path.Combine(DataFolder, f)).Where(Directory.Exists))
        {
            foreach (var file in new DirectoryInfo(folder).EnumerateFiles("*", options))
            {
                try
                {
                    var size = file.Length;
                    file.Attributes = FileAttributes.Normal;
                    file.Delete();
                    freed += size;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // ไฟล์ที่ยังถูกใช้ ข้ามไป
                }
            }
        }
        return freed;
    }

    // ===== อ่านรายชื่อเซิร์ฟ =====
    // ข้อมูลเป็นก้อนๆ ต่อกัน: [ความยาว 4 ไบต์][ข้อมูลเซิร์ฟ 1 เซิร์ฟแบบ protobuf]
    // โครงสร้างตาม servers.proto ของ FiveM: Server { 1: รหัสเซิร์ฟ, 2: ServerData }

    private static List<FiveMServer> ParseThaiServers(byte[] data)
    {
        var servers = new List<FiveMServer>();
        var position = 0;
        while (position + 4 <= data.Length)
        {
            var length = BitConverter.ToInt32(data, position);
            position += 4;
            if (length <= 0 || position + length > data.Length) break;

            try
            {
                if (ParseServer(data.AsSpan(position, length)) is { } server) servers.Add(server);
            }
            catch (FormatException)
            {
                // ข้อมูลเซิร์ฟนี้เสีย ข้ามไป
            }
            position += length;
        }
        return servers;
    }

    private static FiveMServer? ParseServer(ReadOnlySpan<byte> bytes)
    {
        var reader = new ProtoReader(bytes);
        string? code = null;
        FiveMServer? server = null;
        while (reader.Next(out var field, out var wire))
        {
            if (field == 1 && wire == ProtoReader.LengthDelimited) code = reader.ReadString();
            else if (field == 2 && wire == ProtoReader.LengthDelimited) server = ParseData(reader.ReadBytes(), code);
            else reader.Skip(wire);
        }
        return server;
    }

    private static FiveMServer? ParseData(ReadOnlySpan<byte> bytes, string? code)
    {
        if (code == null) return null;
        var reader = new ProtoReader(bytes);
        int max = 0, players = 0;
        ulong iconVersion = 0;
        string hostname = "";
        string? endpoint = null;
        var vars = new Dictionary<string, string>();

        while (reader.Next(out var field, out var wire))
        {
            switch (field)
            {
                case 1 when wire == ProtoReader.Varint: max = (int)reader.ReadVarint(); break;
                case 2 when wire == ProtoReader.Varint: players = (int)reader.ReadVarint(); break;
                case 4 when wire == ProtoReader.LengthDelimited: hostname = reader.ReadString(); break;
                case 11 when wire == ProtoReader.Varint: iconVersion = reader.ReadVarint(); break;
                case 12 when wire == ProtoReader.LengthDelimited: ReadVar(reader.ReadBytes(), vars); break;
                case 18 when wire == ProtoReader.LengthDelimited: endpoint ??= reader.ReadString(); break;
                default: reader.Skip(wire); break; // เช่น รายชื่อผู้เล่น, resources
            }
        }

        // เซิร์ฟไทย: ตั้งภาษาเป็นไทย หรือชื่อเซิร์ฟมีตัวอักษรไทย
        var isThai = vars.GetValueOrDefault("locale")?.StartsWith("th", StringComparison.OrdinalIgnoreCase) == true ||
                     ThaiText().IsMatch(hostname);
        if (!isThai || players == 0) return null;

        var name = CleanName(vars.GetValueOrDefault("sv_projectName") is { Length: > 0 } project ? project : hostname);
        var (host, port) = ParseEndpoint(endpoint);
        var icon = iconVersion != 0 ? $"https://frontend.cfx-services.net/api/servers/icon/{code}/{iconVersion}.png" : null;
        return new FiveMServer(code, name, players, max, icon, host, port);
    }

    private static void ReadVar(ReadOnlySpan<byte> bytes, Dictionary<string, string> vars)
    {
        var reader = new ProtoReader(bytes);
        string? key = null, value = null;
        while (reader.Next(out var field, out var wire))
        {
            if (field == 1 && wire == ProtoReader.LengthDelimited) key = reader.ReadString();
            else if (field == 2 && wire == ProtoReader.LengthDelimited) value = reader.ReadString();
            else reader.Skip(wire);
        }
        if (key != null && value != null) vars[key] = value;
    }

    /// <summary>"1.2.3.4:30120" หรือ "https://xxx.users.cfx.re/" → host + port ไว้วัดปิง</summary>
    private static (string? Host, int Port) ParseEndpoint(string? endpoint)
    {
        if (string.IsNullOrEmpty(endpoint)) return (null, 0);
        if (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) && uri.Scheme.StartsWith("http"))
        {
            // เซิร์ฟที่ซ่อน IP ไว้หลังระบบของ Cfx ไม่มีที่อยู่จริงให้วัดปิง
            return uri.Host.StartsWith("private-placeholder", StringComparison.OrdinalIgnoreCase) ? (null, 0) : (uri.Host, uri.Port);
        }
        var colon = endpoint.LastIndexOf(':');
        return colon > 0 && int.TryParse(endpoint[(colon + 1)..], out var port)
            ? (endpoint[..colon], port)
            : (null, 0);
    }

    /// <summary>ลบโค้ดสีของ FiveM (^1, ^7 ...) และช่องว่างซ้ำๆ ออกจากชื่อเซิร์ฟ</summary>
    private static string CleanName(string name)
    {
        var clean = ColorCodes().Replace(name, "");
        clean = Spaces().Replace(clean, " ").Trim();
        return clean.Length > 80 ? clean[..80] + "…" : clean;
    }

    [GeneratedRegex(@"\^[0-9]")]
    private static partial Regex ColorCodes();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"[฀-๿]")]
    private static partial Regex ThaiText();
}
