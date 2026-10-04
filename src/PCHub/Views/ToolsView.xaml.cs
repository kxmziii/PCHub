using System.Windows;
using System.Windows.Controls;
using PCHub.Helpers;
using PCHub.Services;

namespace PCHub.Views;

/// <summary>แถวผลปิง 1 โซน</summary>
public class PingRow : ObservableObject
{
    public required PingTarget Target { get; init; }

    private PingResult? _result;
    private bool _measuring;

    public PingResult? Result
    {
        get => _result;
        set
        {
            if (!SetField(ref _result, value)) return;
            OnPropertyChanged(nameof(Level));
            OnPropertyChanged(nameof(ValueText));
            OnPropertyChanged(nameof(StatusText));
        }
    }

    public bool Measuring
    {
        get => _measuring;
        set
        {
            if (SetField(ref _measuring, value)) OnPropertyChanged(nameof(ValueText));
        }
    }

    /// <summary>null = ยังไม่ได้วัด (สีเทา)</summary>
    public PingLevel? Level => Result?.Level;

    public string ValueText => Measuring ? "..." : Result switch
    {
        null => "—",
        { AverageMs: { } ms } => $"{ms:0} ms",
        _ => "ต่อไม่ได้",
    };

    public string StatusText
    {
        get
        {
            if (Result == null) return "";
            var parts = new List<string>
            {
                Result.Level switch
                {
                    PingLevel.Good => "ดีมาก",
                    PingLevel.Okay => "พอเล่นได้",
                    PingLevel.Bad => "หน่วง",
                    _ => "ต่อไม่ได้",
                },
            };
            if (Result.Lost > 0 && Result.AverageMs != null) parts.Add($"หลุด {Result.Lost}/{Result.Sent} ครั้ง");
            if (Result.JitterMs > 15) parts.Add("ปิงแกว่ง");
            return string.Join("  ·  ", parts);
        }
    }
}

public partial class ToolsView : UserControl
{
    private readonly List<PingRow> _rows = PingService.Targets.Select(t => new PingRow { Target = t }).ToList();
    private bool _measured;

    public ToolsView()
    {
        InitializeComponent();
        PingList.ItemsSource = _rows;
        IsVisibleChanged += async (_, e) =>
        {
            if ((bool)e.NewValue && !_measured) await RunPingAsync();
        };
    }

    public async Task RunPingAsync()
    {
        _measured = true;
        PingButton.IsEnabled = false;
        PingSummary.Text = "กำลังวัดปิง...";
        foreach (var row in _rows) row.Measuring = true;

        // วัดทุกโซนพร้อมกัน แต่ละแถวอัปเดตทันทีที่วัดเสร็จ
        await Task.WhenAll(_rows.Select(async row =>
        {
            row.Result = await PingService.MeasureAsync(row.Target);
            row.Measuring = false;
        }));

        PingSummary.Text = Verdict();
        PingButton.IsEnabled = true;
    }

    private string Verdict()
    {
        var internet = _rows.First(r => r.Target.Code == "NET").Result;
        var sg = _rows.First(r => r.Target.Code == "SG").Result;
        if (internet?.Level == PingLevel.Failed && sg?.Level == PingLevel.Failed)
            return "ต่อเน็ตไม่ได้ ลองเช็คสาย LAN / Wi-Fi หรือรีสตาร์ทเราเตอร์";

        return sg?.Level switch
        {
            PingLevel.Good => $"เน็ตพร้อมเล่น ปิงไปเซิร์ฟสิงคโปร์ {sg.AverageMs:0} ms",
            PingLevel.Okay => $"พอเล่นได้ ปิงไปสิงคโปร์ {sg.AverageMs:0} ms อาจหน่วงนิดๆ ในเกมที่ต้องไว",
            _ => "ปิงสูง ลองปิดโปรแกรมที่โหลดไฟล์หรือดูสตรีมอยู่ หรือรีสตาร์ทเราเตอร์",
        };
    }

    private async void Ping_Click(object sender, RoutedEventArgs e) => await RunPingAsync();
}
