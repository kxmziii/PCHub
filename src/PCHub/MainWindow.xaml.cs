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
    private readonly TrayIcon? _tray;
    private bool _closeAfterSession;
    private bool _exitRequested;

    private static GameSessionService Session => GameSessionService.Instance;

    /// <param name="showTrayIcon">false = ไม่ต้องมีไอคอนมุมจอ (ใช้ตอนแคปหน้าจอทดสอบ)</param>
    public MainWindow(bool showTrayIcon = true)
    {
        InitializeComponent();
        _navButtons = [NavGames, NavDeals, NavWrapped, NavModes, NavCleaner, NavDiscord, NavTools, NavSettings];
        VersionText.Text = "เวอร์ชัน " + Assembly.GetExecutingAssembly().GetName().Version?.ToString(3);
        ShowPage("games");

        _clock.Tick += (_, _) => UpdateSessionClock();
        Session.PropertyChanged += Session_PropertyChanged;
        Session.SessionEnded += Session_Ended;
        _pingTimer.Tick += async (_, _) => await UpdatePingAsync();
        if (showTrayIcon) _tray = new TrayIcon(ShowFromTray, ExitApp);
    }

    /// <summary>งานเบื้องหลังที่ทำตลอดแม้หน้าต่างถูกย่อไว้ที่มุมจอ: จับเวลาเกม, ปิง, เช็คอัปเดต</summary>
    public async void StartBackgroundWork()
    {
        GameWatcher.Instance.Start();
        _pingTimer.Start();
        await UpdatePingAsync();
        await PrepareUpdateAsync();
    }

    // ===== ไอคอนมุมจอ =====

    /// <summary>เปิดหน้าต่างขึ้นมา (จากไอคอนมุมจอ หรือตอนดับเบิลคลิก PC Hub ซ้ำ)</summary>
    public void ShowFromTray()
    {
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
    }

    /// <summary>ออกจากโปรแกรมจริงๆ (เมนูคลิกขวาที่ไอคอนมุมจอ)</summary>
    private void ExitApp()
    {
        _exitRequested = true;
        Close();
    }

    // ===== อัปเดตอัตโนมัติ =====

    private Velopack.UpdateInfo? _pendingUpdate;

    /// <summary>เช็คอัปเดตตอนเปิดโปรแกรม ถ้ามีจะโหลดเงียบๆ แล้วโชว์การ์ดแจ้งที่เมนูซ้าย</summary>
    private async Task PrepareUpdateAsync()
    {
        if (!UpdateService.IsAvailable) return;
        await Task.Delay(TimeSpan.FromSeconds(5)); // รอให้โปรแกรมเปิดเสร็จก่อน ไม่แย่งเน็ต/เครื่องตอนเริ่ม
        _pendingUpdate = await UpdateService.PrepareAsync();
        if (_pendingUpdate == null) return;

        // ย่อไว้มุมจออยู่ (เช่น เปิดพร้อม Windows) และไม่ได้เล่นเกม: อัปเดตเงียบๆ เลย ผู้ใช้ไม่ต้องทำอะไร
        if (!IsVisible && !Session.IsActive)
        {
            UpdateService.RestartNow(_pendingUpdate, ["--tray"]);
            return;
        }

        UpdateTitle.Text = $"มีเวอร์ชันใหม่ {_pendingUpdate.TargetFullRelease.Version}";
        UpdateCard.Visibility = Visibility.Visible;
    }

    private void RestartForUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (_pendingUpdate == null) return;
        if (Session.IsActive)
        {
            ConfirmDialog.Show(this, "รอเลิกเล่นก่อนนะ",
                $"กำลังเล่น {Session.CurrentGame?.Name} อยู่ จบเซสชันก่อนแล้วค่อยรีสตาร์ท (หรือปล่อยไว้ จะอัปเดตเองตอนปิดโปรแกรม)",
                "โอเค");
            return;
        }
        UpdateService.RestartNow(_pendingUpdate);
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
                "wrapped" => new WrappedView(),
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
        ShowFromTray();
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
        // กด X = ย่อไปอยู่มุมจอ (ยังจับเวลาเล่นเกมต่อ) ถ้าเปิดตัวเลือกนี้ไว้
        if (!_exitRequested && !_closeAfterSession && SettingsService.General.MinimizeToTray)
        {
            e.Cancel = true;
            Hide();

            // มีอัปเดตโหลดไว้แล้ว: ถือโอกาสอัปเดตตอนนี้เลย ผู้ใช้ไม่เห็นอะไรสะดุด
            if (_pendingUpdate != null && !Session.IsActive)
            {
                UpdateService.RestartNow(_pendingUpdate, ["--tray"]);
                return;
            }

            if (!SettingsService.General.TrayHintShown && _tray != null)
            {
                _tray.ShowHint("PC Hub ยังทำงานอยู่ที่มุมจอ",
                    "ยังจับเวลาเล่นเกมให้ต่อ คลิกไอคอนข้างนาฬิกาเพื่อเปิด หรือคลิกขวาเพื่อออกจากโปรแกรม");
                SettingsService.General.TrayHintShown = true;
                SettingsService.Save();
            }
            return;
        }

        // กำลังเล่นอยู่: จบเซสชันก่อน (บันทึกเวลา + คืนค่าเครื่อง) แล้วค่อยปิด
        if (Session.IsActive && !_closeAfterSession)
        {
            e.Cancel = true;
            ShowFromTray();
            var confirmed = ConfirmDialog.Show(this, "จบเซสชันแล้วปิด PC Hub?",
                $"กำลังเล่น {Session.CurrentGame?.Name} อยู่ PC Hub จะบันทึกเวลาเล่นถึงตอนนี้ และคืนค่าเครื่องก่อนปิด\n(ตัวเกมไม่ถูกปิด)",
                "จบแล้วปิด");
            if (!confirmed)
            {
                _exitRequested = false;
                return;
            }
            _closeAfterSession = true;
            Session.Stop();
            return;
        }
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        GameWatcher.Instance.Stop(); // บันทึกเกมที่ยังเล่นอยู่ก่อนออก
        _tray?.Dispose();
        base.OnClosed(e);
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
