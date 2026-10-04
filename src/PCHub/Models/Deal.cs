namespace PCHub.Models;

/// <summary>เกมแจกฟรีหรือลดราคา 1 รายการ</summary>
public record Deal(
    string Title,
    string? ImageUrl,
    string StoreUrl,
    string PriceText,      // เช่น "ฟรี", "฿474.75"
    string? OldPriceText,  // ราคาเต็ม (ขีดฆ่า)
    string Badge);         // เช่น "ฟรีถึง 8 ต.ค. 22:00", "-75%"
