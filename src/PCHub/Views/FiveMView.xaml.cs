using System.ComponentModel;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using PCHub.Helpers;
using PCHub.Services;

namespace PCHub.Views;

/// <summary>เซิร์ฟ FiveM 1 แถว (ปิงวัดทีหลัง เลยต้องแจ้งหน้าจอเมื่อได้ค่า)</summary>
public class FiveMRow : ObservableObject
{
    public required FiveMServer Server { get; init; }

    private bool _isFavorite;
    public bool IsFavorite
    {
        get => _isFavorite;
        set
        {
            if (SetField(ref _isFavorite, value)) OnPropertyChanged(nameof(StarGlyph));
        }
    }

    public string StarGlyph => IsFavorite ? "" : ""; // ดาวทึบ / ดาวโปร่ง

    private double? _pingMs;
    private bool _pinged;

    public void SetPing(double? ms)
    {
        _pingMs = ms;
        _pinged = true;
        OnPropertyChanged(nameof(PingText));
        OnPropertyChanged(nameof(PingLevel));
    }

    public string PingText => Server.PingHost == null ? "ซ่อน IP"
        : !_pinged ? "..."
        : _pingMs is { } ms ? $"{ms:0} ms" : "ต่อไม่ได้";

    /// <summary>null = ยังไม่รู้ (สีเทา)</summary>
    public PingLevel? PingLevel => !_pinged || Server.PingHost == null ? null : _pingMs switch
    {
        null => Services.PingLevel.Failed,
        < 60 => Services.PingLevel.Good,
        < 100 => Services.PingLevel.Okay,
        _ => Services.PingLevel.Bad,
    };

    public string PlayersText => $"{Server.Players:N0}/{Server.MaxPlayers:N0} คน";
    public double FillRatio => Server.MaxPlayers > 0 ? (double)Server.Players / Server.MaxPlayers : 0;
}

public partial class FiveMView : UserControl
{
    private List<FiveMRow> _rows = [];
    private bool _loaded;

    public FiveMView()
    {
        InitializeComponent();
        IsVisibleChanged += async (_, e) =>
        {
            if (!(bool)e.NewValue) return;
            if (!_loaded) await LoadAsync();
            else await UpdateCacheInfoAsync(); // อาจเพิ่งเล่นมา cache โตขึ้น
        };
    }

    private static List<string> Favorites => SettingsService.Current.FiveMFavorites;

    public async Task LoadAsync(bool refresh = false)
    {
        _loaded = true;
        RefreshButton.IsEnabled = false;
        LoadingText.Visibility = Visibility.Visible;
        ServerScroll.Visibility = Visibility.Collapsed;
        MessageCard.Visibility = Visibility.Collapsed;
        _ = UpdateCacheInfoAsync();

        try
        {
            var servers = await FiveMService.GetThaiServersAsync(refresh);
            var favorites = Favorites.ToHashSet();
            _rows = servers.Select(s => new FiveMRow { Server = s, IsFavorite = favorites.Contains(s.Code) }).ToList();
            Subtitle.Text = $"เซิร์ฟไทยที่เปิดอยู่ {_rows.Count} เซิร์ฟ  ·  คนเล่นตอนนี้ {servers.Sum(s => s.Players):N0} คน  ·  กดเข้าเซิร์ฟได้เลย";
            ApplyFilter();
            _ = PingAllAsync(_rows);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            ShowMessage("โหลดรายชื่อเซิร์ฟไม่ได้ ลองเช็คเน็ตแล้วกดปุ่มโหลดใหม่ที่มุมขวาบน");
        }
        finally
        {
            LoadingText.Visibility = Visibility.Collapsed;
            RefreshButton.IsEnabled = true;
        }
    }

    /// <summary>วัดปิงทีละ 8 เซิร์ฟพร้อมกัน (พร้อมกันทั้งหมดเน็ตจะกระตุก ค่าเพี้ยน)</summary>
    private static async Task PingAllAsync(List<FiveMRow> rows)
    {
        using var limit = new SemaphoreSlim(8);
        await Task.WhenAll(rows.Where(r => r.Server.PingHost != null).Select(async row =>
        {
            await limit.WaitAsync();
            try
            {
                row.SetPing(await PingService.QuickPingAsync(row.Server.PingHost!, row.Server.PingPort));
            }
            finally
            {
                limit.Release();
            }
        }));
    }

    private void ApplyFilter()
    {
        var search = SearchBox.Text.Trim();
        var onlyFavorites = ChipFavorites.IsChecked == true;
        var visible = _rows
            .Where(r => !onlyFavorites || r.IsFavorite)
            .Where(r => search.Length == 0 || r.Server.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.IsFavorite) // เซิร์ฟที่ติดดาวขึ้นก่อน
            .ThenByDescending(r => r.Server.Players)
            .ToList();

        ChipFavorites.Content = $"ติดดาว  {_rows.Count(r => r.IsFavorite)}";
        ServerList.ItemsSource = visible;
        if (visible.Count > 0)
        {
            ServerScroll.Visibility = Visibility.Visible;
            MessageCard.Visibility = Visibility.Collapsed;
        }
        else
        {
            ShowMessage(onlyFavorites && search.Length == 0
                ? "ยังไม่ได้ติดดาวเซิร์ฟไหน กดรูปดาวที่เซิร์ฟประจำ จะได้หาเจอเร็วๆ (เซิร์ฟที่ปิดอยู่จะไม่โชว์)"
                : "ไม่เจอเซิร์ฟที่ตรงกับที่ค้นหา");
        }
    }

    private void ShowMessage(string text)
    {
        MessageText.Text = text;
        MessageCard.Visibility = Visibility.Visible;
        ServerScroll.Visibility = Visibility.Collapsed;
    }

    private async Task UpdateCacheInfoAsync()
    {
        var size = await Task.Run(FiveMService.CacheSize);
        CacheTitle.Text = $"Cache ของ FiveM  ·  {SizeFormatter.Format(size)}";
        ClearCacheButton.IsEnabled = size > 0;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

    private void Chip_Checked(object sender, RoutedEventArgs e)
    {
        if (_loaded) ApplyFilter();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadAsync(refresh: true);

    private void OpenFiveM_Click(object sender, RoutedEventArgs e)
    {
        if (!FiveMService.Launch()) Toast.Show("ไม่เจอ FiveM ในเครื่อง", isError: true);
    }

    private void Join_Click(object sender, RoutedEventArgs e)
    {
        var row = (FiveMRow)((FrameworkElement)sender).DataContext;
        if (FiveMService.Join(row.Server.Code))
            Toast.Show($"กำลังเปิด FiveM และเข้าเซิร์ฟ {row.Server.Name}...");
        else
            Toast.Show("ไม่เจอ FiveM ในเครื่อง", isError: true);
    }

    private void Favorite_Click(object sender, RoutedEventArgs e)
    {
        var row = (FiveMRow)((FrameworkElement)sender).DataContext;
        row.IsFavorite = !row.IsFavorite;
        if (row.IsFavorite) Favorites.Add(row.Server.Code);
        else Favorites.Remove(row.Server.Code);
        SettingsService.Save();
        ApplyFilter();
    }

    private async void ClearCache_Click(object sender, RoutedEventArgs e)
    {
        if (FiveMService.IsRunning)
        {
            Toast.Show("ปิด FiveM ก่อนนะ ถึงจะล้าง cache ได้", isError: true);
            return;
        }

        var size = await Task.Run(FiveMService.CacheSize);
        var confirmed = ConfirmDialog.Show(Window.GetWindow(this)!, $"ล้าง cache ของ FiveM {SizeFormatter.Format(size)}?",
            "ไฟล์ที่เซิร์ฟส่งมา (รถ เสื้อผ้า แมพ) จะถูกลบ เข้าเซิร์ฟครั้งหน้าจะโหลดนานขึ้นนิดนึง\nไม่ลบไฟล์เกม GTA และข้อมูลล็อกอินในเซิร์ฟ",
            "ล้างเลย");
        if (!confirmed) return;

        ClearCacheButton.IsEnabled = false;
        var freed = await Task.Run(FiveMService.ClearCache);
        Toast.Show($"ล้าง cache แล้ว ได้พื้นที่คืน {SizeFormatter.Format(freed)}");
        await UpdateCacheInfoAsync();
    }
}
