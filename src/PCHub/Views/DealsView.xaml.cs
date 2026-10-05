using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using PCHub.Models;
using PCHub.Services;

namespace PCHub.Views;

public partial class DealsView : UserControl
{
    private bool _loaded;

    public DealsView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (!_loaded) await LoadAsync();
        };
    }

    public async Task LoadAsync()
    {
        _loaded = true;
        RefreshButton.IsEnabled = false;
        LoadingText.Visibility = Visibility.Visible;
        DealsContent.Visibility = Visibility.Collapsed;
        ErrorCard.Visibility = Visibility.Collapsed;

        // โหลดทุกร้านพร้อมกัน ร้านไหนล่มก็ยังโชว์ส่วนอื่นได้
        var watchTask = TryGetWatchlistAsync();
        var epicTask = DealsService.GetEpicFreeGamesAsync();
        var steamTask = DealsService.GetSteamSpecialsAsync();
        var watch = await watchTask;
        var epic = await TryGet(epicTask);
        var steam = await TryGet(steamTask);

        LoadingText.Visibility = Visibility.Collapsed;
        RefreshButton.IsEnabled = true;

        if (epic == null && steam == null && watch == null)
        {
            ErrorText.Text = "ต่อร้านเกมไม่ได้ ลองเช็คเน็ตแล้วกดปุ่มโหลดใหม่ที่มุมขวาบน";
            ErrorCard.Visibility = Visibility.Visible;
            return;
        }

        ShowWatchlist(watch);
        EpicNowList.ItemsSource = epic?.Now;
        EpicNextList.ItemsSource = epic?.Upcoming;
        SteamList.ItemsSource = steam;
        EpicNowSection.Visibility = epic?.Now.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        EpicNextSection.Visibility = epic?.Upcoming.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        SteamSection.Visibility = steam?.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        DealsContent.Visibility = Visibility.Visible;
    }

    private List<WatchedGame> _watched = [];
    private int? _wishlistCount;

    private void ShowWatchlist((List<WatchedGame> Games, int? WishlistCount)? watch)
    {
        if (watch is not { } result)
        {
            WatchList.ItemsSource = null;
            WatchStatus.Text = "ดึงราคาจาก Steam ไม่ได้ตอนนี้ ลองกดโหลดใหม่";
            return;
        }
        _watched = result.Games;
        _wishlistCount = result.WishlistCount;
        UpdateWatchlist();
    }

    private void UpdateWatchlist()
    {
        WatchList.ItemsSource = _watched;
        var manual = SettingsService.Current.WatchedGames.Count;
        var onSale = _watched.Count(g => g.IsOnSale);
        var saleText = onSale > 0 ? $"  ·  ลดราคาอยู่ {onSale} เกม" : "";

        WatchStatus.Text = _wishlistCount switch
        {
            > 0 => $"Wishlist {_wishlistCount} เกม + เพิ่มเอง {manual} เกม{saleText}  ·  ลดราคาเมื่อไหร่จะแจ้งที่มุมจอ",
            0 when manual == 0 => "ไม่เจอ Wishlist ของ Steam (ว่าง หรือตั้งเป็นส่วนตัวไว้) กด \"เฝ้าราคาเกม\" เพื่อเพิ่มเกมที่อยากได้เองได้เลย",
            _ when manual == 0 => "กด \"เฝ้าราคาเกม\" เพื่อเพิ่มเกมที่อยากได้ ลดราคาเมื่อไหร่ PC Hub จะแจ้งที่มุมจอ",
            _ => $"เฝ้าราคาอยู่ {_watched.Count} เกม{saleText}  ·  ลดราคาเมื่อไหร่จะแจ้งที่มุมจอ",
        };
    }

    private static async Task<(List<WatchedGame> Games, int? WishlistCount)?> TryGetWatchlistAsync()
    {
        try
        {
            return await PriceWatchService.GetWatchlistAsync();
        }
        catch (Exception ex) when (IsNetworkProblem(ex))
        {
            AppLog.Warn("Deals load failed", ex);
            return null;
        }
    }

    private static async Task<T?> TryGet<T>(Task<T> task) where T : class
    {
        try
        {
            return await task;
        }
        catch (Exception ex) when (IsNetworkProblem(ex))
        {
            AppLog.Warn("Deals load failed", ex);
            return null;
        }
    }

    /// <summary>ไม่มีเน็ต หรือร้านเปลี่ยนรูปแบบข้อมูล: ข้ามส่วนนั้นไป ไม่ต้องให้ทั้งหน้าพัง</summary>
    private static bool IsNetworkProblem(Exception ex) =>
        ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException;

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadAsync();

    private async void AddWatch_Click(object sender, RoutedEventArgs e)
    {
        var window = new AddWatchWindow { Owner = Window.GetWindow(this) };
        window.ShowDialog();
        if (!window.Changed) return;

        WatchStatus.Text = "กำลังโหลดราคา...";
        ShowWatchlist(await TryGetWatchlistAsync());
    }

    private void Unwatch_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true; // ไม่ให้การ์ดเปิดหน้าร้านด้วย
        var game = (WatchedGame)((FrameworkElement)sender).DataContext;
        SettingsService.Current.WatchedGames.Remove(game.AppId);
        SettingsService.Save();
        _watched = _watched.Where(g => g.AppId != game.AppId).ToList();
        UpdateWatchlist();
    }

    // เปิดหน้าร้านในแอพ Steam / Epic (ถ้าไม่ได้ลงแอพ จะเปิดเว็บแทน)
    private void Watch_Click(object sender, RoutedEventArgs e)
    {
        var game = (WatchedGame)((FrameworkElement)sender).DataContext;
        StoreLinks.Open(game.AppUrl, game.StoreUrl);
    }

    private void Deal_Click(object sender, RoutedEventArgs e)
    {
        var deal = (Deal)((FrameworkElement)sender).DataContext;
        StoreLinks.Open(deal.AppUrl, deal.StoreUrl);
    }
}
