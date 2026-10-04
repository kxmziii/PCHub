using System.Net.Http;
using System.Text.Json;
using System.Windows;
using PCHub.Helpers;
using PCHub.Services;

namespace PCHub.Views;

/// <summary>ผลค้นหา 1 เกม (ปุ่มเปลี่ยนเป็น "เฝ้าอยู่" เมื่อเพิ่มแล้ว)</summary>
public class SearchRow : ObservableObject
{
    public required int AppId { get; init; }
    public required string Name { get; init; }
    public string? ImageUrl { get; init; }
    public required string PriceText { get; init; }

    private bool _added;
    public bool Added
    {
        get => _added;
        set
        {
            if (!SetField(ref _added, value)) return;
            OnPropertyChanged(nameof(CanAdd));
            OnPropertyChanged(nameof(ButtonText));
        }
    }

    public bool CanAdd => !Added;
    public string ButtonText => Added ? "เฝ้าอยู่" : "เฝ้าราคา";
}

/// <summary>ค้นหาเกมใน Steam แล้วเพิ่มเข้ารายการเฝ้าราคา</summary>
public partial class AddWatchWindow : Window
{
    /// <summary>true = มีการเพิ่มเกม (หน้าของฟรีต้องโหลดรายการใหม่)</summary>
    public bool Changed { get; private set; }

    public AddWatchWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => SearchBox.Focus();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        DarkTitleBar.Apply(this);
    }

#if DEBUG
    /// <summary>ใช้ตอนพัฒนา: ค้นหาให้เลยเพื่อแคปหน้าจอ</summary>
    public Task SearchForTest(string term)
    {
        SearchBox.Text = term;
        return SearchAsync();
    }
#endif

    private async void Search_Click(object sender, RoutedEventArgs e) => await SearchAsync();

    private async Task SearchAsync()
    {
        var term = SearchBox.Text.Trim();
        if (term.Length == 0) return;

        SearchButton.IsEnabled = false;
        HintText.Text = "กำลังค้นหา...";
        HintText.Visibility = Visibility.Visible;
        ResultList.ItemsSource = null;
        try
        {
            var watched = SettingsService.Current.WatchedGames.ToHashSet();
            var rows = (await PriceWatchService.SearchAsync(term))
                .Select(r => new SearchRow
                {
                    AppId = r.AppId, Name = r.Name, ImageUrl = r.ImageUrl, PriceText = r.PriceText,
                    Added = watched.Contains(r.AppId),
                })
                .ToList();
            ResultList.ItemsSource = rows;
            HintText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            HintText.Text = "ไม่เจอเกมชื่อนี้ใน Steam ลองพิมพ์ใหม่ (ภาษาอังกฤษ)";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException)
        {
            HintText.Text = "ค้นหาไม่ได้ ลองเช็คเน็ตแล้วกดค้นหาอีกครั้ง";
        }
        finally
        {
            SearchButton.IsEnabled = true;
        }
    }

    private void Watch_Click(object sender, RoutedEventArgs e)
    {
        var row = (SearchRow)((FrameworkElement)sender).DataContext;
        var watched = SettingsService.Current.WatchedGames;
        if (!watched.Contains(row.AppId)) watched.Add(row.AppId);
        SettingsService.Save();
        row.Added = true;
        Changed = true;
    }
}
