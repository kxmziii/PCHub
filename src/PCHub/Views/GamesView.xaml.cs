using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using PCHub.Models;
using PCHub.Services;

namespace PCHub.Views;

public partial class GamesView : UserControl
{
    private enum SortMode { Recent, Name, PlayTime }

    private List<Game> _games = [];
    private ListCollectionView? _view;
    private string _filter = "all"; // all, Steam, Epic, Riot, Other, hidden
    private SortMode _sort = SortMode.Recent;

    public GamesView()
    {
        InitializeComponent();
        UpdateSortText();
        Loaded += async (_, _) =>
        {
            if (_view == null) await LoadAsync();
        };
        // อัปเดตเวลาเล่นบนการ์ดหลังเลิกเล่น (ทั้งที่เล่นผ่าน PC Hub และที่จับเวลาเองเบื้องหลัง)
        GameSessionService.Instance.SessionEnded += async _ => await RefreshPlayTimesAsync();
        GameWatcher.Instance.SessionRecorded += async _ => await RefreshPlayTimesAsync();
    }

    private async Task RefreshPlayTimesAsync()
    {
        await Task.Run(() => PlayTimeService.ApplyTo(_games));
        RefreshView();
    }

    private static List<string> Hidden => SettingsService.Current.HiddenGames;

    /// <summary>โหลดรายชื่อเกม (refresh = หาในเครื่องใหม่ทั้งหมด)</summary>
    public async Task LoadAsync(bool refresh = false)
    {
        LoadingText.Visibility = Visibility.Visible;
        GameScroll.Visibility = Visibility.Collapsed;
        EmptyState.Visibility = Visibility.Collapsed;

        _games = await GameLibraryService.GetAsync(refresh);
        _view = new ListCollectionView(_games) { Filter = Matches, CustomSort = new GameComparer(_sort) };
        GameList.ItemsSource = _view;

        LoadingText.Visibility = Visibility.Collapsed;
        RefreshView();
    }

    // ===== เรียงลำดับ =====

    private void Sort_Click(object sender, RoutedEventArgs e)
    {
        _sort = _sort switch
        {
            SortMode.Recent => SortMode.Name,
            SortMode.Name => SortMode.PlayTime,
            _ => SortMode.Recent,
        };
        UpdateSortText();
        if (_view != null) _view.CustomSort = new GameComparer(_sort);
    }

    private void UpdateSortText() => SortText.Text = _sort switch
    {
        SortMode.Recent => "เล่นล่าสุด",
        SortMode.Name => "ชื่อ A-Z",
        _ => "เล่นนานสุด",
    };

    private sealed class GameComparer(SortMode mode) : System.Collections.IComparer
    {
        public int Compare(object? x, object? y)
        {
            var a = (Game)x!;
            var b = (Game)y!;
            var result = mode switch
            {
                SortMode.Recent => Nullable.Compare(b.LastPlayed, a.LastPlayed), // ใหม่สุดก่อน เกมที่ไม่เคยเล่นไว้ท้าย
                SortMode.PlayTime => b.PlayTime.CompareTo(a.PlayTime),
                _ => 0,
            };
            return result != 0 ? result : string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
        }
    }

    private void Boost_Click(object sender, RoutedEventArgs e)
    {
        var window = new BoostSettingsWindow { Owner = Window.GetWindow(this) };
        if (window.ShowDialog() == true) Toast.Show("บันทึกการตั้งค่า Game Boost แล้ว");
    }

    private bool Matches(object item)
    {
        var game = (Game)item;
        var isHidden = Hidden.Contains(game.Id);
        if (_filter == "hidden") return isHidden && MatchesSearch(game);
        if (isHidden) return false;

        var sourceMatches = _filter switch
        {
            "all" => true,
            "Other" => game.Source is GameSource.Other or GameSource.Custom,
            _ => game.Source.ToString() == _filter,
        };
        return sourceMatches && MatchesSearch(game);
    }

    private bool MatchesSearch(Game game)
    {
        var text = SearchBox.Text.Trim();
        return text.Length == 0 || game.Name.Contains(text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>อัปเดตลิสต์ จำนวนบนปุ่มกรอง และข้อความต่างๆ</summary>
    private void RefreshView()
    {
        if (_view == null) return;
        _view.Refresh();

        var visible = _games.Where(g => !Hidden.Contains(g.Id)).ToList();
        SetChip(ChipAll, "ทั้งหมด", visible.Count, alwaysShow: true);
        SetChip(ChipSteam, "Steam", visible.Count(g => g.Source == GameSource.Steam));
        SetChip(ChipEpic, "Epic Games", visible.Count(g => g.Source == GameSource.Epic));
        SetChip(ChipRiot, "Riot Games", visible.Count(g => g.Source == GameSource.Riot));
        SetChip(ChipOther, "อื่นๆ", visible.Count(g => g.Source is GameSource.Other or GameSource.Custom));
        SetChip(ChipHidden, "ซ่อนไว้", _games.Count(g => Hidden.Contains(g.Id)));

        Subtitle.Text = $"เจอ {visible.Count} เกมในเครื่อง  ·  กดการ์ดเพื่อเล่น คลิกขวาเพื่อดูตัวเลือกเพิ่ม";

        var hasItems = !_view.IsEmpty;
        GameScroll.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
        EmptyState.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
        EmptyText.Text = _games.Count == 0
            ? "ยังไม่เจอเกมในเครื่องเลย กดปุ่ม \"เพิ่มเกม\" เพื่อเลือกไฟล์เกมเองได้"
            : "ไม่เจอเกมที่ตรงกับที่ค้นหา";
    }

    private void SetChip(RadioButton chip, string label, int count, bool alwaysShow = false)
    {
        chip.Content = $"{label}  {count}";
        // ซ่อนปุ่มกรองที่ไม่มีเกม (ยกเว้นปุ่มที่กำลังเลือกอยู่)
        chip.Visibility = alwaysShow || count > 0 || chip.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Chip_Checked(object sender, RoutedEventArgs e)
    {
        _filter = (string)((FrameworkElement)sender).Tag;
        RefreshView();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshView();

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadAsync(refresh: true);

    // ===== เล่นเกม / เมนูคลิกขวา =====

    private void Play_Click(object sender, RoutedEventArgs e) => Play((Game)((FrameworkElement)sender).DataContext);

    private async void Play(Game game)
    {
        var failText = $"เปิด {game.Name} ไม่ได้ (เกมอาจถูกลบหรือย้ายไปแล้ว ลองกดหาเกมใหม่)";
        var session = GameSessionService.Instance;

        if (session.IsActive)
        {
            // กำลังเล่นเกมอื่นอยู่: เปิดเกมเฉยๆ ไม่เริ่มเซสชันซ้อน
            if (AppLauncher.Launch(game.ToAppEntry()))
                Toast.Show($"กำลังเปิด {game.Name}... (ไม่จับเวลา เพราะกำลังเล่น {session.CurrentGame?.Name} อยู่)");
            else
                Toast.Show(failText, isError: true);
            return;
        }

        Toast.Show(SettingsService.Boost.Enabled
            ? $"กำลังเตรียมเครื่องและเปิด {game.Name}..."
            : $"กำลังเปิด {game.Name}...");
        if (!await session.PlayAsync(game)) Toast.Show(failText, isError: true);
    }

    private void Card_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        var card = (FrameworkElement)sender;
        var menu = BuildMenu((Game)card.DataContext);
        menu.PlacementTarget = card;
        menu.Placement = PlacementMode.MousePoint;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private ContextMenu BuildMenu(Game game)
    {
        var menu = new ContextMenu();
        menu.Items.Add(NewMenuItem("เล่น", "", () => Play(game)));
        if (game.InstallFolder is { } folder && Directory.Exists(folder))
            menu.Items.Add(NewMenuItem("เปิดโฟลเดอร์เกม", "", () => Process.Start("explorer.exe", $"\"{folder}\"")));

        var modes = SettingsService.Current.Modes;
        if (modes.Count > 0)
        {
            menu.Items.Add(new Separator());
            foreach (var mode in modes)
                menu.Items.Add(NewMenuItem($"เพิ่มเข้าโหมด \"{mode.Name}\"", "", () => AddToMode(game, mode)));
        }

        menu.Items.Add(new Separator());
        if (game.Source == GameSource.Custom)
            menu.Items.Add(NewMenuItem("เอาออกจากคลัง", "", () => RemoveCustom(game)));
        else if (Hidden.Contains(game.Id))
            menu.Items.Add(NewMenuItem("เลิกซ่อน", "", () => SetHidden(game, false)));
        else
            menu.Items.Add(NewMenuItem("ซ่อนเกมนี้", "", () => SetHidden(game, true)));

        return menu;
    }

    private static MenuItem NewMenuItem(string header, string icon, Action onClick)
    {
        var item = new MenuItem { Header = header, Tag = icon };
        item.Click += (_, _) => onClick();
        return item;
    }

    private void AddToMode(Game game, LaunchMode mode)
    {
        var entry = game.ToAppEntry();
        if (mode.Apps.Any(a => a.Path == entry.Path && a.Arguments == entry.Arguments))
        {
            Toast.Show($"{game.Name} อยู่ในโหมด {mode.Name} อยู่แล้ว");
            return;
        }
        mode.Apps.Add(entry);
        SettingsService.Save();
        Toast.Show($"เพิ่ม {game.Name} เข้าโหมด {mode.Name} แล้ว");
    }

    private void SetHidden(Game game, bool hidden)
    {
        if (hidden) Hidden.Add(game.Id);
        else Hidden.Remove(game.Id);
        SettingsService.Save();
        RefreshView();
        Toast.Show(hidden ? $"ซ่อน {game.Name} แล้ว (ดูได้ที่ปุ่ม \"ซ่อนไว้\")" : $"เลิกซ่อน {game.Name} แล้ว");
    }

    // ===== เพิ่ม/ลบเกมเอง =====

    private async void AddGame_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "เลือกไฟล์เกม",
            Filter = "เกม (*.exe, *.lnk, *.url)|*.exe;*.lnk;*.url|ทุกไฟล์ (*.*)|*.*",
        };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;

        var name = Path.GetFileNameWithoutExtension(dialog.FileName);
        var custom = SettingsService.Current.CustomGames;
        if (custom.Any(g => g.Path.Equals(dialog.FileName, StringComparison.OrdinalIgnoreCase)))
        {
            Toast.Show($"{name} อยู่ในคลังแล้ว");
            return;
        }

        custom.Add(new AppEntry { Name = name, Path = dialog.FileName });
        SettingsService.Save();
        await LoadAsync(refresh: true);
        Toast.Show($"เพิ่ม {name} เข้าคลังแล้ว");
    }

    private async void RemoveCustom(Game game)
    {
        SettingsService.Current.CustomGames.RemoveAll(g => $"custom:{g.Path}" == game.Id);
        SettingsService.Save();
        await LoadAsync(refresh: true);
        Toast.Show($"เอา {game.Name} ออกจากคลังแล้ว (ตัวเกมในเครื่องไม่ได้ถูกลบ)");
    }
}
