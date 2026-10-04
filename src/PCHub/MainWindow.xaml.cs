using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using PCHub.Helpers;
using PCHub.Services;
using PCHub.Views;

namespace PCHub;

public partial class MainWindow : Window
{
    // เก็บหน้าที่เคยเปิดแล้วไว้ สลับกลับมาข้อมูลจะไม่หาย
    private readonly Dictionary<string, UserControl> _pages = new();
    private readonly NavButton[] _navButtons;
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool _closeAfterSession;

    private static GameSessionService Session => GameSessionService.Instance;

    public MainWindow()
    {
        InitializeComponent();
        _navButtons = [NavGames, NavDeals, NavModes, NavCleaner, NavDiscord, NavTools, NavSettings];
        VersionText.Text = "เวอร์ชัน " + Assembly.GetExecutingAssembly().GetName().Version?.ToString(3);
        ShowPage("games");

        _clock.Tick += (_, _) => UpdateSessionClock();
        Session.PropertyChanged += Session_PropertyChanged;
        Session.SessionEnded += Session_Ended;

        _pingTimer.Tick += async (_, _) => await UpdatePingAsync();
        Loaded += async (_, _) =>
        {
            await UpdatePingAsync();
            _pingTimer.Start();
        };
    }

    // ===== ป้ายปิงที่เมนูซ้าย =====

    private readonly DispatcherTimer _pingTimer = new() { Interval = TimeSpan.FromMinutes(1) };

    private async Task UpdatePingAsync()
    {
        var result = await PingService.MeasureAsync(PingService.Targets[0], samples: 3);
        PingText.Text = result.AverageMs is { } ms ? $"ปิงสิงคโปร์  {ms:0} ms" : "ต่อเน็ตไม่ได้";
        PingDot.Fill = (Brush)new PingLevelBrushConverter().Convert(result.Level, typeof(Brush), null!, null!);
    }

    private void PingBadge_Click(object sender, RoutedEventArgs e) => ShowPage("tools");

    /// <summary>หน้าที่เปิดอยู่ตอนนี้</summary>
    public object? CurrentPage => PageHost.Content;

    /// <summary>สลับไปหน้าที่ต้องการ: games, modes, cleaner, discord, tools, settings</summary>
    public void ShowPage(string key)
    {
        var button = _navButtons.FirstOrDefault(b => (string)b.Tag == key) ?? NavGames;
        button.IsChecked = true;
    }

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        var key = (string)((NavButton)sender).Tag;
        if (!_pages.TryGetValue(key, out var page))
        {
            page = key switch
            {
                "deals" => new DealsView(),
                "modes" => new ModesView(),
                "cleaner" => new CleanerView(),
                "discord" => new DiscordView(),
                "tools" => new ToolsView(),
                "settings" => new SettingsView(),
                _ => new GamesView(),
            };
            _pages[key] = page;
        }

        PageHost.Content = page;
        PageHost.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)));
    }

    // ===== การ์ดเซสชันเล่นเกม =====

    private void Session_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(GameSessionService.State)) return;

        switch (Session.State)
        {
            case SessionState.Preparing:
                ShowSessionCard("กำลังเตรียมเครื่อง...", "Warning", "ยกเลิก");
                break;
            case SessionState.WaitingForGame:
                ShowSessionCard("รอเกมเปิด...", "Warning", "ยกเลิก");
                break;
            case SessionState.Playing:
                ShowSessionCard("กำลังเล่น", "Success", "จบเซสชัน");
                if (Session.ManualStopOnly)
                    ShowSessionNote("กด \"จบเซสชัน\" ตอนเลิกเล่น (เกมนี้ PC Hub ดูเองไม่ได้ว่าปิดไปหรือยัง)");
                _clock.Start();
                if (SettingsService.Boost is { Enabled: true, MinimizeHub: true }) WindowState = WindowState.Minimized;
                break;
            case SessionState.Idle:
                _clock.Stop();
                SessionCard.Visibility = Visibility.Collapsed;
                break;
        }
    }

    private void Session_Ended(SessionResult result)
    {
        _clock.Stop();
        if (_closeAfterSession)
        {
            Close();
            return;
        }

        ShowSessionCard("เล่นจบแล้ว", "TextMuted", "ปิด");
        SessionGame.Text = result.Game.Name;
        if (result.Session is { } played)
        {
            SessionTime.Text = TimeFormatter.Long(played.Duration);
            var notes = new List<string>();
            if (played.Duration < TimeSpan.FromMinutes(1)) notes.Add("เล่นไม่ถึงนาที ไม่นับเป็นเวลาเล่น");
            if (result.ReopenedApps > 0) notes.Add($"เปิดแอพที่ปิดไป {result.ReopenedApps} ตัวกลับมาให้แล้ว");
            if (notes.Count > 0) ShowSessionNote(string.Join("\n", notes));
        }
        else
        {
            SessionTime.Text = "—";
            ShowSessionNote("ไม่เห็นเกมเปิดขึ้นมา (อาจกดยกเลิก หรือ launcher กำลังอัปเดตเกมอยู่) คืนค่าเครื่องให้แล้ว");
        }

        // เด้งกลับมาให้เห็นสรุปผล
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
    }

    private void ShowSessionCard(string status, string dotBrush, string buttonText)
    {
        SessionCard.Visibility = Visibility.Visible;
        SessionStatus.Text = status;
        SessionDot.Fill = (Brush)FindResource(dotBrush);
        SessionButton.Content = buttonText;
        SessionGame.Text = Session.CurrentGame?.Name ?? "";
        SessionNote.Visibility = Visibility.Collapsed;
        UpdateSessionClock();
    }

    private void ShowSessionNote(string text)
    {
        SessionNote.Text = text;
        SessionNote.Visibility = Visibility.Visible;
    }

    private void UpdateSessionClock()
    {
        if (Session.State is SessionState.Ended) return;
        SessionTime.Text = Session.StartedAt is { } start ? TimeFormatter.Clock(DateTime.Now - start) : "0:00:00";
    }

    private void SessionButton_Click(object sender, RoutedEventArgs e)
    {
        if (Session.IsActive) Session.Stop();
        else Session.Dismiss();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // กำลังเล่นอยู่: จบเซสชันก่อน (บันทึกเวลา + คืนค่าเครื่อง) แล้วค่อยปิด
        if (Session.IsActive && !_closeAfterSession)
        {
            e.Cancel = true;
            var confirmed = ConfirmDialog.Show(this, "จบเซสชันแล้วปิด PC Hub?",
                $"กำลังเล่น {Session.CurrentGame?.Name} อยู่ PC Hub จะบันทึกเวลาเล่นถึงตอนนี้ และคืนค่าเครื่องก่อนปิด\n(ตัวเกมไม่ถูกปิด)",
                "จบแล้วปิด");
            if (!confirmed) return;
            _closeAfterSession = true;
            Session.Stop();
            return;
        }
        base.OnClosing(e);
    }

#if DEBUG
    /// <summary>ใช้ตอนพัฒนา: โชว์การ์ดเซสชันตัวอย่างโดยไม่ต้องเปิดเกมจริง</summary>
    public void ShowDemoSessionCard()
    {
        ShowSessionCard("กำลังเล่น", "Success", "จบเซสชัน");
        SessionGame.Text = "PUBG: BATTLEGROUNDS";
        SessionTime.Text = "1:23:45";
    }
#endif

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        DarkTitleBar.Apply(this);
    }
}
