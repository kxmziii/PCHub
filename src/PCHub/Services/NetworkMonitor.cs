namespace PCHub.Services;

/// <summary>สรุปเน็ตระหว่างเล่น 1 รอบ</summary>
public record NetworkReport(double? AverageMs, int Spikes, IReadOnlyList<DateTime> SpikeTimes, bool HomeInternetProblem, int Samples)
{
    /// <summary>ข้อความสั้นๆ ไว้โชว์ในการ์ดสรุป เช่น "เน็ต: ปิงเฉลี่ย 45 ms · ลื่นตลอด"</summary>
    public string Summary
    {
        get
        {
            var average = AverageMs is { } ms ? $"ปิงเฉลี่ย {ms:0} ms" : "ต่อเซิร์ฟสิงคโปร์ไม่ได้";
            if (Spikes == 0) return $"เน็ต: {average}  ·  ลื่นตลอด";

            var times = string.Join(", ", SpikeTimes.Take(3).Select(t => t.ToString("HH:mm")));
            var more = SpikeTimes.Count > 3 ? " ..." : "";
            var cause = HomeInternetProblem ? "น่าจะมาจากเน็ตบ้าน/Wi-Fi" : "น่าจะมาจากเส้นทางไปเซิร์ฟ";
            return $"เน็ต: {average}  ·  กระตุก {Spikes} ครั้ง ({times}{more})  ·  {cause}";
        }
    }
}

/// <summary>
/// วัดปิงทุก 10 วินาทีระหว่างเล่น (ไปเซิร์ฟสิงคโปร์ + เน็ตทั่วไป) แล้วสรุปตอนเลิกเล่น
/// วัดสองที่พร้อมกันจะได้แยกออกว่าเน็ตบ้านมีปัญหา หรือแค่เส้นทางไปเซิร์ฟเกมช้า
/// </summary>
public sealed class NetworkMonitor
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);
    private const double SpikeMs = 150;      // ปิงเกินนี้ = รู้สึกกระตุกในเกม
    private const double HomeSlowMs = 100;   // เน็ตทั่วไป (ปกติ ~15 ms) ช้าเกินนี้ = เน็ตบ้านมีปัญหา

    private record Sample(DateTime Time, double? Server, double? Internet);

    private readonly List<Sample> _samples = [];
    private CancellationTokenSource? _stop;

    public void Start()
    {
        _stop = new CancellationTokenSource();
        _ = RunAsync(_stop.Token);
    }

    public NetworkReport Stop()
    {
        _stop?.Cancel();
        List<Sample> samples;
        lock (_samples) samples = _samples.ToList();

        var spikes = samples.Where(s => s.Server is null or > SpikeMs).ToList();
        var homeProblem = spikes.Count > 0 && spikes.Count(s => s.Internet is null or > HomeSlowMs) * 2 >= spikes.Count;
        var times = spikes.Select(s => s.Time)
            // กระตุกติดๆ กันในนาทีเดียวกัน นับเป็นครั้งเดียว
            .GroupBy(t => new DateTime(t.Year, t.Month, t.Day, t.Hour, t.Minute, 0))
            .Select(g => g.Key)
            .ToList();
        var pings = samples.Where(s => s.Server != null).Select(s => s.Server!.Value).ToList();

        return new NetworkReport(pings.Count > 0 ? pings.Average() : null, times.Count, times, homeProblem, samples.Count);
    }

    private async Task RunAsync(CancellationToken token)
    {
        var server = PingService.Targets[0]; // สิงคโปร์: เซิร์ฟ SEA ของเกมส่วนใหญ่
        var internet = PingService.Targets.First(t => t.Code == "NET");
        while (!token.IsCancellationRequested)
        {
            var serverPing = PingService.QuickPingAsync(server.Host, 443, samples: 1);
            var internetPing = PingService.QuickPingAsync(internet.Host, 443, samples: 1);
            await Task.WhenAll(serverPing, internetPing);
            lock (_samples) _samples.Add(new Sample(DateTime.Now, serverPing.Result, internetPing.Result));

            try
            {
                await Task.Delay(Interval, token);
            }
            catch (TaskCanceledException)
            {
                return;
            }
        }
    }
}
