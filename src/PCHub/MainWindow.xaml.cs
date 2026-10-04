using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using PCHub.Helpers;
using PCHub.Views;

namespace PCHub;

public partial class MainWindow : Window
{
    // เก็บหน้าที่เคยเปิดแล้วไว้ สลับกลับมาข้อมูลจะไม่หาย
    private readonly Dictionary<string, UserControl> _pages = new();
    private readonly NavButton[] _navButtons;

    public MainWindow()
    {
        InitializeComponent();
        _navButtons = [NavModes, NavCleaner, NavDiscord, NavTools, NavSettings];
        VersionText.Text = "เวอร์ชัน " + Assembly.GetExecutingAssembly().GetName().Version?.ToString(3);
        ShowPage("modes");
    }

    /// <summary>หน้าที่เปิดอยู่ตอนนี้</summary>
    public object? CurrentPage => PageHost.Content;

    /// <summary>สลับไปหน้าที่ต้องการ: modes, cleaner, discord, tools, settings</summary>
    public void ShowPage(string key)
    {
        var button = _navButtons.FirstOrDefault(b => (string)b.Tag == key) ?? NavModes;
        button.IsChecked = true;
    }

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        var key = (string)((NavButton)sender).Tag;
        if (!_pages.TryGetValue(key, out var page))
        {
            page = key switch
            {
                "cleaner" => new CleanerView(),
                "discord" => new DiscordView(),
                "tools" => new ToolsView(),
                "settings" => new SettingsView(),
                _ => new ModesView(),
            };
            _pages[key] = page;
        }

        PageHost.Content = page;
        PageHost.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)));
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        DarkTitleBar.Apply(this);
    }
}
