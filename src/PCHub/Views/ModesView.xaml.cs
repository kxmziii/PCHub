using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using PCHub.Models;
using PCHub.Services;

namespace PCHub.Views;

public partial class ModesView : UserControl
{
    private readonly ObservableCollection<LaunchMode> _modes;
    private readonly DispatcherTimer _statusTimer = new() { Interval = TimeSpan.FromSeconds(4) };

    public ModesView()
    {
        InitializeComponent();
        _modes = new ObservableCollection<LaunchMode>(SettingsService.Current.Modes);
        ModeList.ItemsSource = _modes;

        _statusTimer.Tick += (_, _) =>
        {
            _statusTimer.Stop();
            StatusBar.Visibility = Visibility.Collapsed;
        };
    }

    private async void LaunchMode_Click(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        var mode = (LaunchMode)button.DataContext;
        if (mode.Apps.Count == 0)
        {
            EditMode(mode);
            return;
        }

        button.IsEnabled = false;
        ShowStatus($"กำลังเปิดโหมด {mode.Name}...");
        var failed = await AppLauncher.LaunchModeAsync(mode);
        button.IsEnabled = true;

        if (failed.Count == 0)
            ShowStatus($"เปิดโหมด {mode.Name} แล้ว ({mode.Apps.Count} แอพ)");
        else
            ShowStatus($"เปิดไม่ได้: {string.Join(", ", failed.Select(a => a.Name))} (ไฟล์อาจถูกลบหรือย้ายไปแล้ว ลองแก้ไขโหมดแล้วเพิ่มใหม่)", isError: true);
    }

    private void LaunchApp_Click(object sender, RoutedEventArgs e)
    {
        var app = (AppEntry)((FrameworkElement)sender).DataContext;
        if (AppLauncher.Launch(app))
            ShowStatus($"เปิด {app.Name} แล้ว");
        else
            ShowStatus($"เปิด {app.Name} ไม่ได้ (ไฟล์อาจถูกลบหรือย้ายไปแล้ว)", isError: true);
    }

    private void EditMode_Click(object sender, RoutedEventArgs e) =>
        EditMode((LaunchMode)((FrameworkElement)sender).DataContext);

    private void AddMode_Click(object sender, RoutedEventArgs e)
    {
        var editor = new ModeEditorWindow(new LaunchMode { Name = "โหมดใหม่" }, isNew: true)
        {
            Owner = Window.GetWindow(this),
        };
        if (editor.ShowDialog() != true || editor.Result == null) return;

        _modes.Add(editor.Result);
        Save();
        ShowStatus($"สร้างโหมด {editor.Result.Name} แล้ว");
    }

    private void EditMode(LaunchMode mode)
    {
        var editor = new ModeEditorWindow(mode, isNew: false) { Owner = Window.GetWindow(this) };
        if (editor.ShowDialog() != true) return;

        var index = _modes.IndexOf(mode);
        if (editor.Deleted)
        {
            _modes.RemoveAt(index);
            ShowStatus($"ลบโหมด {mode.Name} แล้ว");
        }
        else if (editor.Result != null)
        {
            _modes[index] = editor.Result;
            ShowStatus($"บันทึกโหมด {editor.Result.Name} แล้ว");
        }
        Save();
    }

    private void Save()
    {
        SettingsService.Current.Modes = _modes.ToList();
        SettingsService.Save();
    }

    private void ShowStatus(string message, bool isError = false)
    {
        StatusText.Text = message;
        StatusIcon.Text = isError ? "" : ""; // ตกใจ / ติ๊กถูก
        StatusIcon.Foreground = (System.Windows.Media.Brush)FindResource(isError ? "Danger" : "Success");
        StatusBar.Visibility = Visibility.Visible;
        _statusTimer.Stop();
        _statusTimer.Start();
    }
}
