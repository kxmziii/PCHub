using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using PCHub.Models;

namespace PCHub.Services;

/// <summary>
/// ดึงเกมแจกฟรีจาก Epic และเกมลดราคาจาก Steam (ราคาไทย) ผ่าน API สาธารณะของร้าน
/// ไม่ต้องล็อกอิน และไม่ได้ส่งข้อมูลของผู้ใช้ไปไหน
/// </summary>
public static class DealsService
{
    private const string EpicUrl =
        "https://store-site-backend-static.ak.epicgames.com/freeGamesPromotions?locale=en-US&country=TH&allowCountries=TH";

    private const string SteamUrl = "https://store.steampowered.com/api/featuredcategories?cc=th&l=english";

    private static HttpClient Http => Web.Client;
    private static readonly CultureInfo Thai = new("th-TH");

    public record EpicFreeGames(List<Deal> Now, List<Deal> Upcoming);

    public static async Task<EpicFreeGames> GetEpicFreeGamesAsync()
    {
        using var doc = JsonDocument.Parse(await Http.GetStringAsync(EpicUrl));
        var elements = doc.RootElement.GetProperty("data").GetProperty("Catalog").GetProperty("searchStore").GetProperty("elements");

        var now = new List<Deal>();
        var upcoming = new List<Deal>();
        foreach (var game in elements.EnumerateArray())
        {
            if (!game.TryGetProperty("promotions", out var promotions) || promotions.ValueKind != JsonValueKind.Object) continue;

            var title = game.GetProperty("title").GetString() ?? "";
            var slug = EpicSlug(game);
            var url = $"https://store.epicgames.com/p/{slug}";
            var appUrl = StoreLinks.Epic(slug);
            var image = EpicImage(game);
            var fullPrice = game.TryGetProperty("price", out var price)
                ? FormatMoney(price.GetProperty("totalPrice").GetProperty("originalPrice").GetInt64(), "THB")
                : null;

            if (FreeOffer(promotions, "promotionalOffers") is { } current &&
                current.Start <= DateTime.Now && DateTime.Now < current.End)
                now.Add(new Deal(title, image, url, "ฟรี", fullPrice, $"ฟรีถึง {FormatDate(current.End)}", appUrl));
            else if (FreeOffer(promotions, "upcomingPromotionalOffers") is { } next)
                upcoming.Add(new Deal(title, image, url, "ฟรี", fullPrice, $"เริ่ม {FormatDate(next.Start)}", appUrl));
        }
        return new EpicFreeGames(now, upcoming);
    }

    public static async Task<List<Deal>> GetSteamSpecialsAsync()
    {
        using var doc = JsonDocument.Parse(await Http.GetStringAsync(SteamUrl));
        var items = doc.RootElement.GetProperty("specials").GetProperty("items");

        var deals = new List<Deal>();
        var seen = new HashSet<long>();
        foreach (var item in items.EnumerateArray())
        {
            var id = item.GetProperty("id").GetInt64();
            if (!seen.Add(id) || !item.TryGetProperty("final_price", out var final) || final.ValueKind != JsonValueKind.Number) continue;

            var currency = item.TryGetProperty("currency", out var c) ? c.GetString() ?? "THB" : "THB";
            var original = item.TryGetProperty("original_price", out var o) && o.ValueKind == JsonValueKind.Number
                ? FormatMoney(o.GetInt64(), currency)
                : null;
            var image = item.TryGetProperty("large_capsule_image", out var img) ? img.GetString()
                : item.TryGetProperty("header_image", out var header) ? header.GetString() : null;

            deals.Add(new Deal(
                item.GetProperty("name").GetString() ?? "",
                image,
                $"https://store.steampowered.com/app/{id}",
                FormatMoney(final.GetInt64(), currency),
                original,
                $"-{item.GetProperty("discount_percent").GetInt32()}%",
                StoreLinks.Steam((int)id)));
        }
        return deals;
    }

    /// <summary>ช่วงเวลาที่แจกฟรี (ส่วนลด 100%) ถ้ามี</summary>
    private static (DateTime Start, DateTime End)? FreeOffer(JsonElement promotions, string name)
    {
        if (!promotions.TryGetProperty(name, out var groups) || groups.ValueKind != JsonValueKind.Array) return null;
        foreach (var group in groups.EnumerateArray())
        {
            foreach (var offer in group.GetProperty("promotionalOffers").EnumerateArray())
            {
                if (offer.GetProperty("discountSetting").GetProperty("discountPercentage").GetInt32() != 0) continue;
                return (offer.GetProperty("startDate").GetDateTimeOffset().LocalDateTime,
                        offer.GetProperty("endDate").GetDateTimeOffset().LocalDateTime);
            }
        }
        return null;
    }

    private static string EpicSlug(JsonElement game)
    {
        if (game.TryGetProperty("catalogNs", out var ns) && ns.TryGetProperty("mappings", out var mappings) &&
            mappings.ValueKind == JsonValueKind.Array)
        {
            foreach (var mapping in mappings.EnumerateArray())
                if (mapping.GetProperty("pageSlug").GetString() is { Length: > 0 } slug) return slug;
        }
        var productSlug = game.TryGetProperty("productSlug", out var p) ? p.GetString() : null;
        return productSlug?.Replace("/home", "") ?? "";
    }

    /// <summary>รูปแนวตั้งของเกม (ใช้ทำการ์ด)</summary>
    private static string? EpicImage(JsonElement game)
    {
        if (!game.TryGetProperty("keyImages", out var images)) return null;
        string? fallback = null;
        foreach (var image in images.EnumerateArray())
        {
            var type = image.GetProperty("type").GetString();
            var url = image.GetProperty("url").GetString();
            if (type == "OfferImageTall") return url;
            if (type == "Thumbnail") fallback = url;
        }
        return fallback;
    }

    /// <summary>ร้านเก็บราคาเป็นหน่วยสตางค์ เช่น 47475 = ฿474.75</summary>
    private static string FormatMoney(long cents, string currency)
    {
        var value = cents / 100m;
        var number = value == decimal.Truncate(value) ? value.ToString("N0") : value.ToString("N2");
        return currency == "THB" ? $"฿{number}" : $"{number} {currency}";
    }

    private static string FormatDate(DateTime time) => time.ToString("d MMM HH:mm", Thai);
}
