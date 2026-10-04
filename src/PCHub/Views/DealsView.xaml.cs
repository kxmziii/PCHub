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

        // โหลดสองร้านพร้อมกัน ร้านไหนล่มก็ยังโชว์อีกร้านได้
        var epicTask = DealsService.GetEpicFreeGamesAsync();
        var steamTask = DealsService.GetSteamSpecialsAsync();
        var epic = await TryGet(epicTask);
        var steam = await TryGet(steamTask);

        LoadingText.Visibility = Visibility.Collapsed;
        RefreshButton.IsEnabled = true;

        if (epic == null && steam == null)
        {
            ErrorText.Text = "ต่อร้านเกมไม่ได้ ลองเช็คเน็ตแล้วกดปุ่มโหลดใหม่ที่มุมขวาบน";
            ErrorCard.Visibility = Visibility.Visible;
            return;
        }

        EpicNowList.ItemsSource = epic?.Now;
        EpicNextList.ItemsSource = epic?.Upcoming;
        SteamList.ItemsSource = steam;
        EpicNowSection.Visibility = epic?.Now.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        EpicNextSection.Visibility = epic?.Upcoming.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        SteamSection.Visibility = steam?.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        DealsContent.Visibility = Visibility.Visible;
    }

    private static async Task<T?> TryGet<T>(Task<T> task) where T : class
    {
        try
        {
            return await task;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException
                                       or KeyNotFoundException or InvalidOperationException)
        {
            return null; // ไม่มีเน็ต หรือร้านเปลี่ยนรูปแบบข้อมูล
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadAsync();

    private void Deal_Click(object sender, RoutedEventArgs e)
    {
        var deal = (Deal)((FrameworkElement)sender).DataContext;
        try
        {
            Process.Start(new ProcessStartInfo(deal.StoreUrl) { UseShellExecute = true })?.Dispose();
        }
        catch (Win32Exception)
        {
            // ไม่มีเบราว์เซอร์ตั้งค่าไว้ ข้ามไป
        }
    }
}
