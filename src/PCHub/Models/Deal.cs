namespace PCHub.Models;

/// <summary>เกมแจกฟรีหรือลดราคา 1 รายการ</summary>
public record Deal(
    string Title,
    string? ImageUrl,
    string StoreUrl,       // หน้าร้านบนเว็บ (ใช้เมื่อไม่ได้ลงแอพของร้าน)
    string PriceText,      // เช่น "ฟรี", "฿474.75"
    string? OldPriceText,  // ราคาเต็ม (ขีดฆ่า)
    string Badge,          // เช่น "ฟรีถึง 8 ต.ค. 22:00", "-75%"
    string? AppUrl = null); // หน้าร้านในแอพ เช่น steam://store/123
