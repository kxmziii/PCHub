using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PCHub.Helpers;
using PCHub.Models;
using PCHub.Services;

namespace PCHub.Views;

/// <summary>เกมอันดับ 2-3 บนการ์ด</summary>
public record WrappedRow(string Label, string TimeText, double BarWidth);

public partial class WrappedView : UserControl
{
    private const double MaxBarWidth = 300;

    private List<Game> _games = [];
    private bool _loaded;

    public WrappedView()
    {
        InitializeComponent();
        IsVisibleChanged += async (_, e) =>
        {
            if (!(bool)e.NewValue) return;
            if (!_loaded) await LoadAsync();
            else ShowSelected(); // กลับมาหน้านี้ อาจมีเวลาเล่นใหม่
        };
    }

    public async Task LoadAsync()
    {
        _loaded = true;
        _games = await GameLibraryService.GetAsync();
        LoadingText.Visibility = Visibility.Collapsed;

        // เลือกช่วงที่มีข้อมูลให้ก่อน (ผู้ใช้ใหม่ยังไม่มีข้อมูลของ PC Hub ใช้ของ Steam ไปก่อน)
        var first = new[] { WrappedPeriod.ThisMonth, WrappedPeriod.LastMonth, WrappedPeriod.SteamTwoWeeks }
            .FirstOrDefault(p => WrappedService.Build(p, _games) != null, WrappedPeriod.ThisMonth);
        Chip(first).IsChecked = true;
        ShowSelected();
    }

    private RadioButton Chip(WrappedPeriod period) => period switch
    {
        WrappedPeriod.LastMonth => ChipLastMonth,
        WrappedPeriod.SteamTwoWeeks => ChipSteam,
        _ => ChipThisMonth,
    };

    private void Period_Checked(object sender, RoutedEventArgs e)
    {
        if (_loaded) ShowSelected();
    }

    private void ShowSelected()
    {
        var period = new[] { WrappedPeriod.ThisMonth, WrappedPeriod.LastMonth, WrappedPeriod.SteamTwoWeeks }
            .First(p => Chip(p).IsChecked == true);
        var summary = WrappedService.Build(period, _games);

        CopyButton.IsEnabled = SaveButton.IsEnabled = summary != null;
        CardHost.Visibility = summary != null ? Visibility.Visible : Visibility.Collapsed;
        EmptyCard.Visibility = summary != null ? Visibility.Collapsed : Visibility.Visible;
        if (summary == null)
        {
            EmptyText.Text = period == WrappedPeriod.SteamTwoWeeks
                ? "ไม่เจอเวลาเล่นช่วง 2 สัปดาห์ล่าสุดจาก Steam"
                : "ช่วงนี้ยังไม่มีข้อมูลการเล่น กดเล่นเกมจากหน้าคลังเกม แล้ว PC Hub จะจับเวลาให้เอง";
            return;
        }
        Fill(summary);
    }

    private void Fill(WrappedSummary summary)
    {
        CardPeriod.Text = summary.Title;
        CardTotal.Text = TimeFormatter.Long(summary.Total);
        CardSub.Text = summary.SessionCount > 0
            ? $"เล่นไป {summary.SessionCount} ครั้ง  ·  {summary.Games.Count} เกม"
            : $"เล่นไป {summary.Games.Count} เกม";

        var extras = new List<string>();
        if (summary.Longest is { } longest) extras.Add($"เล่นยาวสุด {TimeFormatter.Long(longest.Duration)} ({longest.GameName})");
        if (summary.FavoriteDay != null) extras.Add($"ชอบเล่น{summary.FavoriteDay}");
        CardExtra.Text = string.Join("  ·  ", extras);
        CardExtra.Visibility = extras.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        var top = summary.Games[0];
        TopName.Text = top.Name;
        TopTime.Text = TimeFormatter.Long(top.Time);
        var cover = top.CoverPath == null ? null : ImageLoader.Load(top.CoverPath, 320);
        TopCover.Background = cover != null
            ? new ImageBrush(cover) { Stretch = Stretch.UniformToFill }
            : (Brush)FindResource("CardBg");

        RunnerUps.ItemsSource = summary.Games.Skip(1).Take(3)
            .Select((g, i) => new WrappedRow($"{i + 2}. {g.Name}", TimeFormatter.Short(g.Time),
                Math.Max(8, MaxBarWidth * (g.Time / top.Time))))
            .ToList();

        PersonalityText.Text = summary.Personality;
        PersonalityDetail.Text = summary.PersonalityDetail;
        CardNote.Text = summary.SourceNote;
    }

    /// <summary>วาดการ์ดเป็นรูปขนาด 1080x1350 (ความละเอียด 2 เท่าของที่โชว์)</summary>
    private BitmapSource RenderCard()
    {
        var size = new Rect(0, 0, ShareCard.Width, ShareCard.Height);
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            // VisualBrush วาดการ์ดตามขนาดจริง ไม่สนว่าบนจอถูกย่ออยู่
            context.DrawRectangle(new VisualBrush(ShareCard), null, size);
        }
        var bitmap = new RenderTargetBitmap((int)size.Width * 2, (int)size.Height * 2, 192, 192, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

#if DEBUG
    /// <summary>ใช้ตอนพัฒนา: ดูว่ารูปที่บันทึกออกมาหน้าตาถูกต้องไหม</summary>
    public void ExportForTest(string path)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(RenderCard()));
        using var file = File.Create(path);
        encoder.Save(file);
    }
#endif

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetImage(RenderCard());
        Toast.Show("คัดลอกรูปแล้ว ไปกด Ctrl+V ในแชท Discord ได้เลย");
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "PC Hub");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"gaming-wrapped-{DateTime.Now:yyyy-MM-dd-HHmmss}.png");

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(RenderCard()));
        using (var file = File.Create(path)) encoder.Save(file);

        Process.Start("explorer.exe", $"/select,\"{path}\"")?.Dispose();
        Toast.Show($"บันทึกรูปไว้ที่ {path}");
    }
}
