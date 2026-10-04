using System.Net.Http;

namespace PCHub.Services;

/// <summary>ตัวต่อเน็ตตัวเดียวใช้ร่วมกันทั้งโปรแกรม (สร้างหลายตัวจะเปลืองการเชื่อมต่อ)</summary>
internal static class Web
{
    public static HttpClient Client { get; } = Create();

    private static HttpClient Create()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("PCHub");
        return client;
    }
}
