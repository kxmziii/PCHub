using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using PCHub.Helpers;
using PCHub.Models;
using PCHub.Services;

namespace PCHub.Views;

/// <summary>แถวที่ติ๊กเลือกได้ในหน้าตั้งค่า Game Boost</summary>
public class ToggleItem : ObservableObject
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public string Detail { get; init; } = "";
    public string? IconSource { get; init; }
    public AppEntry? App { get; init; }
    public long SortValue { get; init; }

    private bool _isChecked;
    public bool IsChecked
    {
        get => _isChecked;
        set => SetField(ref _isChecked, value);
    }
}

public partial class BoostSettingsWindow : Window
{
    // แอพที่ชอบทำงานเงียบๆ อยู่ที่มุมจอ (ไม่มีหน้าต่าง) แต่กินแรม/เน็ต
    private static readonly HashSet<string> BackgroundApps = new(StringComparer.OrdinalIgnoreCase)
    {
        "OneDrive", "Spotify", "Dropbox", "GoogleDriveFS", "ms-teams", "Teams", "Slack", "LINE", "Zoom",
    };

    private static readonly string WindowsFolder = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

    private readonly BoostSettings _boost = SettingsService.Boost;
    private List<ToggleItem>? _closeItems;
    private readonly List<ToggleItem> _companionItems;
    private readonly ListCollectionView _companionView;

    public BoostSettingsWindow()
    {
        InitializeComponent();

        EnabledSwitch.IsChecked = _boost.Enabled;
        PowerSwitch.IsChecked = _boost.HighPerformance;
        RestoreSwitch.IsChecked = _boost.RestoreApps;
        MinimizeSwitch.IsChecked = _boost.MinimizeHub;
        DiscordSwitch.IsChecked = _boost.RestartHeavyDiscord;

        _companionItems = CompanionItems(_boost.CompanionApps);
        _companionView = new ListCollectionView(_companionItems) { Filter = MatchesSearch };
        CompanionList.ItemsSource = _companionView;

        // ดูแอพที่เปิดอยู่ต้องถามทุกโปรเซส ใช้เวลานิดนึง ทำเบื้องหลัง
        Loaded += async (_, _) =>
        {
            var saved = _boost.CloseApps.ToHashSet(StringComparer.OrdinalIgnoreCase);
            _closeItems = await Task.Run(() => RunningApps(saved));
            CloseList.ItemsSource = _closeItems;
        };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        DarkTitleBar.Apply(this);
    }

    /// <summary>แอพที่เปิดอยู่ตอนนี้ (เฉพาะที่มีหน้าต่างหรือรู้ว่าทำงานเบื้องหลัง) + แอพที่เคยเลือกไว้</summary>
    private static List<ToggleItem> RunningApps(HashSet<string> saved)
    {
        var items = new Dictionary<string, ToggleItem>(StringComparer.OrdinalIgnoreCase);
        var processes = Process.GetProcesses();
        try
        {
            foreach (var group in processes.GroupBy(p => p.ProcessName, StringComparer.OrdinalIgnoreCase))
            {
                if (BoostService.NeverClose.Contains(group.Key)) continue;
                if (!group.Any(HasWindow) && !saved.Contains(group.Key) && !BackgroundApps.Contains(group.Key)) continue;

                var path = group.Select(p => ProcessHelper.GetPath(p.Id)).FirstOrDefault(p => p != null);
                if (path == null || path.StartsWith(WindowsFolder, StringComparison.OrdinalIgnoreCase)) continue; // ของ Windows

                var ram = group.Sum(WorkingSet);
                items[group.Key] = new ToggleItem
                {
                    Key = group.Key,
                    Name = DisplayName(path, group.Key),
                    Detail = SizeFormatter.Format(ram),
                    IconSource = path,
                    SortValue = ram,
                    IsChecked = saved.Contains(group.Key),
                };
            }
        }
        finally
        {
            foreach (var p in processes) p.Dispose();
        }

        foreach (var name in saved.Where(n => !items.ContainsKey(n)))
            items[name] = new ToggleItem { Key = name, Name = name, Detail = "ไม่ได้เปิดอยู่", IsChecked = true };

        return items.Values.OrderByDescending(i => i.IsChecked).ThenByDescending(i => i.SortValue).ToList();
    }

    private static List<ToggleItem> CompanionItems(List<AppEntry> companions)
    {
        var chosen = companions.Select(a => a.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var apps = companions.Concat(InstalledAppsService.GetAll())
            .DistinctBy(a => a.Path, StringComparer.OrdinalIgnoreCase);
        return apps
            .Select(a => new ToggleItem
            {
                Key = a.Path,
                Name = a.Name,
                IconSource = a.IconSource,
                App = a,
                IsChecked = chosen.Contains(a.Path),
            })
            .OrderByDescending(i => i.IsChecked)
            .ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static bool HasWindow(Process process)
    {
        try
        {
            return process.MainWindowHandle != IntPtr.Zero;
        }
        catch (InvalidOperationException)
        {
            return false; // ปิดไปแล้ว
        }
    }

    private static long WorkingSet(Process process)
    {
        try
        {
            return process.WorkingSet64;
        }
        catch (InvalidOperationException)
        {
            return 0;
        }
    }

    /// <summary>ชื่อที่อ่านง่าย เช่น "Google Chrome" แทน "chrome"</summary>
    private static string DisplayName(string exePath, string processName)
    {
        try
        {
            var description = FileVersionInfo.GetVersionInfo(exePath).FileDescription;
            return string.IsNullOrWhiteSpace(description) ? processName : description.Trim();
        }
        catch (FileNotFoundException)
        {
            return processName;
        }
    }

    private bool MatchesSearch(object item)
    {
        var text = SearchBox.Text.Trim();
        return text.Length == 0 || ((ToggleItem)item).Name.Contains(text, StringComparison.OrdinalIgnoreCase);
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => _companionView?.Refresh();

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _boost.Enabled = EnabledSwitch.IsChecked == true;
        _boost.HighPerformance = PowerSwitch.IsChecked == true;
        _boost.RestoreApps = RestoreSwitch.IsChecked == true;
        _boost.MinimizeHub = MinimizeSwitch.IsChecked == true;
        _boost.RestartHeavyDiscord = DiscordSwitch.IsChecked == true;
        // ถ้ากดบันทึกก่อนลิสต์แอพโหลดเสร็จ ใช้ค่าเดิมไป
        if (_closeItems != null) _boost.CloseApps = _closeItems.Where(i => i.IsChecked).Select(i => i.Key).ToList();
        _boost.CompanionApps = _companionItems.Where(i => i.IsChecked && i.App != null).Select(i => i.App!.Clone()).ToList();
        SettingsService.Save();
        DialogResult = true;
    }
}
