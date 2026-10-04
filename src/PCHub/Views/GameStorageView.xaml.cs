using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using PCHub.Helpers;
using PCHub.Models;
using PCHub.Services;

namespace PCHub.Views;

/// <summary>ไดรฟ์ 1 ลูก: ใช้ไปกี่ % เหลือเท่าไหร่</summary>
public record DriveItem(string Name, double UsedRatio, string Detail, bool IsLow);

/// <summary>เกม 1 แถวในหน้าพื้นที่เกม</summary>
public class GameStorageItem : ObservableObject
{
    /// <summary>ไม่ได้เล่นนานกว่านี้ = แนะนำให้ถอน</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromDays(90);

    public required Game Game { get; init; }

    private long? _size;
    /// <summary>null = กำลังนับ, 0 = ไม่รู้ขนาด</summary>
    public long? Size
    {
        get => _size;
        set
        {
            if (SetField(ref _size, value)) OnPropertyChanged(nameof(SizeText));
        }
    }

    public string SizeText => Size switch
    {
        null => "กำลังนับ...",
        0 => "—",
        { } bytes => SizeFormatter.Format(bytes),
    };

    public string Subtitle
    {
        get
        {
            var root = Game.InstallFolder == null ? null : Path.GetPathRoot(Game.InstallFolder)?.TrimEnd('\\');
            return string.IsNullOrEmpty(root) ? Game.SourceName : $"{Game.SourceName}  ·  ไดรฟ์ {root}";
        }
    }

    /// <summary>ไม่ได้เล่นเกิน 3 เดือน หรือ Steam บอกว่ายังไม่เคยเล่น</summary>
    public bool IsStale => Game.LastPlayed is { } last
        ? DateTime.Now - last > StaleAfter
        : Game.Source == GameSource.Steam;

    public string LastPlayedText => Game.LastPlayed is { } last
        ? $"เล่นล่าสุด {TimeFormatter.Ago(last)}"
        : Game.Source == GameSource.Steam ? "ยังไม่เคยเล่น" : "ยังไม่มีข้อมูลการเล่น";
}

public partial class GameStorageView : UserControl
{
    private List<GameStorageItem> _items = [];
    private bool _loaded;
    private bool _loading;

    public GameStorageView()
    {
        InitializeComponent();
        IsVisibleChanged += async (_, e) =>
        {
            if ((bool)e.NewValue && !_loaded) await LoadAsync();
        };
    }

    public async Task LoadAsync()
    {
        if (_loading) return;
        _loading = true;
        _loaded = true;
        RefreshButton.IsEnabled = false;

        DriveList.ItemsSource = StorageService.FixedDrives().Select(ToDriveItem).ToList();
        SummaryTitle.Text = "กำลังดูว่าเกมไหนกินพื้นที่...";
        SummaryDetail.Text = "";

        var games = await Task.Run(GameLibraryService.Scan);
        _items = games
            .Where(g => g.InstallFolder != null && Directory.Exists(g.InstallFolder))
            .Select(g => new GameStorageItem { Game = g, Size = g.SizeOnDisk ?? (IsTooBroad(g.InstallFolder!) ? 0 : null) })
            .ToList();
        ShowSorted();

        // เกมที่ launcher ไม่ได้บอกขนาด นับเองทีละเกม (ใหญ่ๆ อาจใช้เวลาหลายวินาที)
        foreach (var item in _items.Where(i => i.Size == null))
        {
            var folder = item.Game.InstallFolder!;
            item.Size = await Task.Run(() => StorageService.FolderSize(folder));
        }
        ShowSorted();

        RefreshButton.IsEnabled = true;
        _loading = false;
    }

    private void ShowSorted()
    {
        GameList.ItemsSource = _items.OrderByDescending(i => i.Size ?? -1).ToList();

        var known = _items.Where(i => i.Size is > 0).ToList();
        var stale = known.Where(i => i.IsStale).ToList();
        SummaryTitle.Text = $"เกมทั้งหมดใช้พื้นที่ {SizeFormatter.Format(known.Sum(i => i.Size!.Value))}";
        SummaryDetail.Text = stale.Count > 0
            ? $"มี {stale.Count} เกมที่ไม่ได้เล่นเกิน 3 เดือนหรือยังไม่เคยเล่น รวม {SizeFormatter.Format(stale.Sum(i => i.Size!.Value))} ถ้าไม่เล่นแล้วถอนออกได้เลย"
            : "ทุกเกมเพิ่งเล่นไม่นาน ไม่มีเกมที่ควรถอน";
    }

    private static DriveItem ToDriveItem(DriveInfo drive)
    {
        var used = drive.TotalSize - drive.TotalFreeSpace;
        var ratio = drive.TotalSize > 0 ? (double)used / drive.TotalSize : 0;
        var label = string.IsNullOrWhiteSpace(drive.VolumeLabel) ? "" : $"  ({drive.VolumeLabel})";
        return new DriveItem(
            $"ไดรฟ์ {drive.Name.TrimEnd('\\')}{label}",
            ratio,
            $"เหลือ {SizeFormatter.Format(drive.TotalFreeSpace)} จาก {SizeFormatter.Format(drive.TotalSize)}",
            IsLow: ratio > 0.9);
    }

    /// <summary>โฟลเดอร์ใหญ่ที่ไม่ใช่โฟลเดอร์เกมจริง (เช่น เกมที่เพิ่มเองจากไฟล์บน Desktop) ไม่นับ เดี๋ยวช้าและตัวเลขผิด</summary>
    private static bool IsTooBroad(string folder)
    {
        var full = Path.GetFullPath(folder).TrimEnd('\\');
        if (full.Length <= 3) return true; // เช่น C:\
        Environment.SpecialFolder[] broad =
        [
            Environment.SpecialFolder.UserProfile, Environment.SpecialFolder.DesktopDirectory,
            Environment.SpecialFolder.MyDocuments, Environment.SpecialFolder.ProgramFiles,
            Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.Windows,
            Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolder.ApplicationData,
        ];
        var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        return broad.Select(Environment.GetFolderPath).Append(downloads)
            .Any(f => string.Equals(f.TrimEnd('\\'), full, StringComparison.OrdinalIgnoreCase));
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadAsync();

    private void Uninstall_Click(object sender, RoutedEventArgs e)
    {
        var item = (GameStorageItem)((FrameworkElement)sender).DataContext;
        var (target, hint) = StorageService.UninstallTarget(item.Game);
        var size = item.Size is > 0 ? $"จะได้พื้นที่คืนประมาณ {item.SizeText}\n" : "";

        var confirmed = ConfirmDialog.Show(Window.GetWindow(this)!, $"ถอน {item.Game.Name}?",
            $"{size}{hint}\n(PC Hub ไม่ลบไฟล์เกมเอง ให้ launcher ของเกมเป็นคนถอน จะได้ไม่มีไฟล์ค้าง)",
            "ไปหน้าถอนการติดตั้ง", danger: true);
        if (!confirmed) return;

        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true })?.Dispose();
            Toast.Show($"{hint} ถอนเสร็จแล้วกดปุ่มนับใหม่เพื่ออัปเดต");
        }
        catch (Win32Exception)
        {
            Toast.Show($"เปิดหน้าถอนการติดตั้งไม่ได้ ({item.Game.SourceName} อาจยังไม่ได้ลงไว้)", isError: true);
        }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var item = (GameStorageItem)((FrameworkElement)sender).DataContext;
        if (item.Game.InstallFolder is { } folder && Directory.Exists(folder))
            Process.Start("explorer.exe", $"\"{folder}\"")?.Dispose();
    }
}
