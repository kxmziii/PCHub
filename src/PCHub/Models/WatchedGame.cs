namespace PCHub.Models;

/// <summary>เกมที่เฝ้าราคาไว้ (จาก Wishlist ของ Steam หรือเพิ่มเองใน PC Hub) พร้อมราคาไทยตอนนี้</summary>
public record WatchedGame(
    int AppId,
    string Name,
    string? ImageUrl,
    long FinalCents,       // ราคาตอนนี้ (สตางค์)
    int DiscountPercent,   // 0 = ไม่ลด
    string PriceText,      // เช่น "฿539.70", "ฟรี"
    string? OriginalPriceText,
    bool FromWishlist)
{
    public bool IsOnSale => DiscountPercent > 0;
    public string StoreUrl => $"https://store.steampowered.com/app/{AppId}";
    public string AppUrl => Services.StoreLinks.Steam(AppId);
    public string Badge => IsOnSale ? $"-{DiscountPercent}%" : FromWishlist ? "Wishlist" : "เฝ้าราคา";
}
