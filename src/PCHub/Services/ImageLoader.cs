using System.IO;
using System.Windows.Media.Imaging;

namespace PCHub.Services;

/// <summary>โหลดรูป (ไฟล์ในเครื่องหรือ https) แบบย่อขนาดตอนโหลด ประหยัดแรม และจำไว้ใช้ซ้ำ</summary>
public static class ImageLoader
{
    private static readonly Dictionary<(string Source, int Width), BitmapImage?> Cache = new();

    public static BitmapImage? Load(string source, int decodeWidth)
    {
        if (Cache.TryGetValue((source, decodeWidth), out var cached)) return cached;

        BitmapImage? image = null;
        var isWeb = source.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        if (isWeb || File.Exists(source))
        {
            try
            {
                image = new BitmapImage();
                image.BeginInit();
                image.UriSource = new Uri(source);
                image.DecodePixelWidth = decodeWidth;
                image.CacheOption = BitmapCacheOption.OnLoad; // ไม่ล็อกไฟล์ไว้
                image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                image.EndInit();
                if (!isWeb) image.Freeze(); // รูปจากเน็ตยังโหลดไม่เสร็จ freeze ไม่ได้
            }
            catch (Exception ex) when (ex is IOException or NotSupportedException or FormatException or UriFormatException)
            {
                image = null; // ไฟล์รูปเสีย
            }
        }

        Cache[(source, decodeWidth)] = image;
        return image;
    }
}
