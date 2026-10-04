using System.Net.Http;
using System.Text.Json;
using PCHub.Models;

namespace PCHub.Services;

/// <summary>
/// เฝ้าราคาเกมบน Steam (ราคาไทย): ดึง Wishlist ของบัญชีที่ใช้ล่าสุด + เกมที่เพิ่มเองใน PC Hub
/// ใช้ API สาธารณะของ Steam ไม่ต้องล็อกอิน
/// </summary>
public static class PriceWatchService
{
    private const string AssetBase = "https://shared.cloudflare.steamstatic.com/store_item_assets/";

    public record SearchResult(int AppId, string Name, string? ImageUrl, string PriceText);

    /// <summary>ผลการดึง Wishlist: null = ไม่ได้ลง Steam, ว่าง = Wishlist ว่างหรือตั้งเป็นส่วนตัว</summary>
    public static async Task<List<int>?> GetWishlistAsync()
    {
        if (SteamAccount.SteamId64() is not { } steamId) return null;
        using var doc = JsonDocument.Parse(await Web.Client.GetStringAsync(
            $"https://api.steampowered.com/IWishlistService/GetWishlist/v1/?steamid={steamId}"));
        if (!doc.RootElement.GetProperty("response").TryGetProperty("items", out var items)) return [];
        return items.EnumerateArray().Select(i => i.GetProperty("appid").GetInt32()).ToList();
    }

    /// <summary>Wishlist + เกมที่เพิ่มเอง พร้อมราคาตอนนี้ (เกมที่ลดราคาอยู่ขึ้นก่อน)</summary>
    public static async Task<(List<WatchedGame> Games, int? WishlistCount)> GetWatchlistAsync()
    {
        List<int>? wishlist = null;
        try
        {
            wishlist = await GetWishlistAsync();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // ดึง Wishlist ไม่ได้ ใช้แค่เกมที่เพิ่มเอง
        }

        var fromWishlist = wishlist?.ToHashSet() ?? [];
        var ids = fromWishlist.Union(SettingsService.Current.WatchedGames).ToList();
        var games = await GetPricesAsync(ids, fromWishlist);
        var sorted = games.OrderByDescending(g => g.DiscountPercent).ThenBy(g => g.Name).ToList();
        return (sorted, wishlist?.Count);
    }

    /// <summary>ราคาไทยของหลายเกมในครั้งเดียว (ทีละไม่เกิน 100 เกม)</summary>
    public static async Task<List<WatchedGame>> GetPricesAsync(IReadOnlyList<int> appIds, ISet<int> wishlist)
    {
        var result = new List<WatchedGame>();
        foreach (var chunk in appIds.Chunk(100))
        {
            var input = JsonSerializer.Serialize(new
            {
                ids = chunk.Select(id => new { appid = id }),
                context = new { language = "english", country_code = "TH" },
                data_request = new { include_basic_info = true, include_assets = true },
            });
            using var doc = JsonDocument.Parse(await Web.Client.GetStringAsync(
                "https://api.steampowered.com/IStoreBrowseService/GetItems/v1/?input_json=" + Uri.EscapeDataString(input)));
            if (!doc.RootElement.GetProperty("response").TryGetProperty("store_items", out var items)) continue;

            foreach (var item in items.EnumerateArray())
            {
                if (!item.TryGetProperty("name", out var name) || !item.TryGetProperty("appid", out var appId)) continue;
                var id = appId.GetInt32();
                var (finalCents, discount, priceText, originalText) = ReadPrice(item);
                result.Add(new WatchedGame(id, name.GetString() ?? "", ReadImage(item), finalCents, discount,
                    priceText, originalText, wishlist.Contains(id)));
            }
        }
        return result;
    }

    /// <summary>ค้นหาเกมใน Steam (ราคาไทย)</summary>
    public static async Task<List<SearchResult>> SearchAsync(string term)
    {
        using var doc = JsonDocument.Parse(await Web.Client.GetStringAsync(
            $"https://store.steampowered.com/api/storesearch/?term={Uri.EscapeDataString(term)}&cc=TH&l=english"));
        var results = new List<SearchResult>();
        foreach (var item in doc.RootElement.GetProperty("items").EnumerateArray())
        {
            if (item.GetProperty("type").GetString() != "app") continue;
            var price = item.TryGetProperty("price", out var p) && p.ValueKind == JsonValueKind.Object
                ? FormatBaht(p.GetProperty("final").GetInt64())
                : "ฟรี";
            results.Add(new SearchResult(item.GetProperty("id").GetInt32(), item.GetProperty("name").GetString() ?? "",
                item.TryGetProperty("tiny_image", out var img) ? img.GetString() : null, price));
        }
        return results;
    }

    private static (long FinalCents, int Discount, string PriceText, string? OriginalText) ReadPrice(JsonElement item)
    {
        if (!item.TryGetProperty("best_purchase_option", out var option)) return (0, 0, "ฟรี / ยังไม่วางขาย", null);

        var finalCents = option.TryGetProperty("final_price_in_cents", out var f) && long.TryParse(f.ToString(), out var cents) ? cents : 0;
        var discount = option.TryGetProperty("discount_pct", out var d) ? d.GetInt32() : 0;
        var priceText = option.TryGetProperty("formatted_final_price", out var fp) ? fp.GetString() ?? FormatBaht(finalCents) : FormatBaht(finalCents);
        var originalText = discount > 0 && option.TryGetProperty("formatted_original_price", out var op) ? op.GetString() : null;
        return (finalCents, discount, priceText, originalText);
    }

    /// <summary>รูปแนวนอนของเกม (ใช้ทำการ์ด)</summary>
    private static string? ReadImage(JsonElement item)
    {
        if (!item.TryGetProperty("assets", out var assets) || !assets.TryGetProperty("asset_url_format", out var format)) return null;
        foreach (var name in new[] { "main_capsule", "header", "small_capsule" })
        {
            if (assets.TryGetProperty(name, out var file) && file.GetString() is { Length: > 0 } fileName)
                return AssetBase + format.GetString()!.Replace("${FILENAME}", fileName);
        }
        return null;
    }

    private static string FormatBaht(long cents)
    {
        var value = cents / 100m;
        return value == decimal.Truncate(value) ? $"฿{value:N0}" : $"฿{value:N2}";
    }
}
