using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace PCHub.Services;

public record PingTarget(string Code, string Name, string Detail, string Host);

public enum PingLevel { Good, Okay, Bad, Failed }

public record PingResult(PingTarget Target, double? AverageMs, double JitterMs, int Lost, int Sent)
{
    public PingLevel Level => AverageMs switch
    {
        null => PingLevel.Failed,
        < 60 when Lost == 0 => PingLevel.Good,
        < 100 => PingLevel.Okay,
        _ => PingLevel.Bad,
    };
}

/// <summary>
/// วัดปิงไปยังศูนย์ข้อมูลในเมืองที่เซิร์ฟเวอร์เกมตั้งอยู่
/// ใช้เวลาเชื่อมต่อ TCP แทน ping ปกติ เพราะเซิร์ฟเวอร์ส่วนใหญ่บล็อก ping (ICMP) ค่าที่ได้ใกล้เคียงกัน
/// </summary>
public static class PingService
{
    public static readonly PingTarget[] Targets =
    [
        new("SG", "สิงคโปร์", "เซิร์ฟ SEA ของเกมส่วนใหญ่ เช่น PUBG, Apex, VALORANT, LoL", "dynamodb.ap-southeast-1.amazonaws.com"),
        new("HK", "ฮ่องกง", "เซิร์ฟเอเชียตะวันออกของบางเกม", "dynamodb.ap-east-1.amazonaws.com"),
        new("JP", "โตเกียว", "เซิร์ฟญี่ปุ่น", "dynamodb.ap-northeast-1.amazonaws.com"),
        new("NET", "เน็ตทั่วไป", "Cloudflare ใกล้บ้านคุณ ถ้าตัวนี้ช้าด้วย แปลว่าเน็ตบ้านมีปัญหา", "1.1.1.1"),
    ];

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    public static async Task<PingResult> MeasureAsync(PingTarget target, int samples = 5)
    {
        IPAddress? address;
        try
        {
            // หา IP ก่อน จะได้ไม่นับเวลาหา DNS รวมเข้าไปในปิง
            address = IPAddress.TryParse(target.Host, out var ip)
                ? ip
                : (await Dns.GetHostAddressesAsync(target.Host)).FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
        }
        catch (SocketException)
        {
            address = null;
        }
        if (address == null) return new PingResult(target, null, 0, samples, samples);

        var times = new List<double>();
        for (var i = 0; i < samples; i++)
        {
            using var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            using var timeout = new CancellationTokenSource(Timeout);
            var stopwatch = Stopwatch.StartNew();
            try
            {
                await socket.ConnectAsync(address, 443, timeout.Token);
                times.Add(stopwatch.Elapsed.TotalMilliseconds);
            }
            catch (Exception ex) when (ex is SocketException or OperationCanceledException)
            {
                // หลุด/หมดเวลา นับเป็น packet loss
            }
            await Task.Delay(150);
        }

        if (times.Count == 0) return new PingResult(target, null, 0, samples, samples);
        var jitter = times.Count > 1 ? times.Zip(times.Skip(1), (a, b) => Math.Abs(a - b)).Average() : 0;
        return new PingResult(target, times.Average(), jitter, samples - times.Count, samples);
    }
}
