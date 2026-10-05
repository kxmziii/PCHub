using System.Windows;
using PCHub.Services;
using PCHub.Views;
#if DEBUG
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
#endif

namespace PCHub;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        HookErrorHandlers();
        AppLog.CleanupOld();
        AppLog.Info($"Start PC Hub {AppLog.Version} args=[{string.Join(' ', e.Args)}] on {System.Runtime.InteropServices.RuntimeInformation.OSDescription}");

#if DEBUG
        if (e.Args.Contains("--snapshot"))
        {
            StartForSnapshot(e.Args);
            return;
        }
#endif

        // ปิดหน้าต่างหลัก = ออกจากโปรแกรม (หน้าโหลดหรือกล่องข้อความอื่นๆ ไม่เกี่ยว)
        ShutdownMode = ShutdownMode.OnMainWindowClose;

        // --tray = เปิดพร้อม Windows หรือหลังอัปเดต: ย่อไว้ที่มุมจอเงียบๆ ไม่ต้องมีหน้าโหลด
        var startInTray = e.Args.Contains("--tray");

        // หน้าโหลด: โหลดการตั้งค่าและหาเกมไว้ล่วงหน้า แล้วค่อยเปิดหน้าต่างหลัก
        SplashWindow? splash = null;
        if (!startInTray)
        {
            splash = new SplashWindow();
            splash.Show();
            await splash.LoadAsync();
        }

        var window = new MainWindow();
        MainWindow = window;
        window.StartBackgroundWork();

        if (splash != null)
        {
            window.Show();
            await splash.FadeOutAndCloseAsync();
        }
    }

    // ===== ดัก error ทั้งโปรแกรม: บันทึกไว้ แทนที่จะเด้งปิดหายไปเฉยๆ =====

    private DateTime _lastErrorDialog = DateTime.MinValue;

    private void HookErrorHandlers()
    {
        DispatcherUnhandledException += (_, e) =>
        {
            AppLog.Error("Unhandled UI exception", e.Exception);
            e.Handled = true; // ส่วนใหญ่ยังใช้ต่อได้ ดีกว่าโปรแกรมหายไปเฉยๆ
            if (DateTime.Now - _lastErrorDialog < TimeSpan.FromSeconds(30)) return; // กันกล่องเด้งรัวๆ
            _lastErrorDialog = DateTime.Now;
            ShowErrorDialog();
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex) AppLog.Error("Unhandled background exception (app is closing)", ex);
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            AppLog.Error("Unobserved task exception", e.Exception);
            e.SetObserved();
        };
    }

    private void ShowErrorDialog()
    {
        // ย่ออยู่มุมจอ ไม่ต้องเด้งกล่องรบกวน (บันทึกไว้แล้ว)
        if (MainWindow is not { IsVisible: true } owner) return;
        var create = ConfirmDialog.Show(owner, "PC Hub เจอปัญหานิดหน่อย",
            "บันทึกรายละเอียดไว้แล้ว ใช้งานต่อได้ตามปกติ\nถ้าเจอบ่อย กดสร้างไฟล์รายงานแล้วส่งให้คนทำ PC Hub ทาง Discord",
            "สร้างไฟล์รายงาน");
        if (create) OpenProblemReport();
    }

    /// <summary>สร้างไฟล์รายงานปัญหาบน Desktop แล้วเปิดโฟลเดอร์ให้เห็นไฟล์</summary>
    public static void OpenProblemReport()
    {
        var path = ProblemReport.Create();
        System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"")?.Dispose();
    }

#if DEBUG
    // ใช้ตอนพัฒนา: PCHub.exe --snapshot out.png [--page games | cleaner | storage | mode-editor | boost | splash | ...]
    //   [--scan] [--delay ms] [--size 1400x900] [--demo-session] [--export-wrapped out.png]
    // เปิดหน้าที่ต้องการไว้นอกจอ (ไม่กวนคนที่ใช้คอมอยู่) แคปเป็นรูป แล้วปิดโปรแกรม
    private void StartForSnapshot(string[] args)
    {
        var snapshotPath = GetArg(args, "--snapshot")!;
        var page = GetArg(args, "--page");

        var window = new MainWindow(showTrayIcon: false);
        MainWindow = window;
        HideOffScreen(window);
        if (GetArg(args, "--size")?.Split('x') is [var w, var h])
        {
            window.Width = double.Parse(w);
            window.Height = double.Parse(h);
        }
        window.Show();
        if (args.Contains("--demo-session")) window.ShowDemoSessionCard();
        if (args.Contains("--background")) window.StartBackgroundWork(); // ทดสอบงานเบื้องหลัง (จับเวลาเกม, ปิง)
        if (args.Contains("--crash-test")) // ทดสอบว่า error ถูกบันทึก และโปรแกรมไม่ปิดหายไป
            window.Dispatcher.BeginInvoke(() => throw new InvalidOperationException("crash test"),
                System.Windows.Threading.DispatcherPriority.Background);

        Window target = window;
        Task ready = Task.CompletedTask;
        switch (page)
        {
            case "splash":
                var splash = new SplashWindow();
                HideOffScreen(splash);
                splash.Show();
                _ = splash.LoadAsync();
                target = splash;
                break;
            case "mode-editor":
                target = new ModeEditorWindow(Services.SettingsService.Current.Modes[0], isNew: false) { Owner = window, ShowActivated = false };
                target.Show();
                break;
            case "boost":
                target = new BoostSettingsWindow { Owner = window, ShowActivated = false };
                target.Show();
                break;
            case "confirm-play":
                target = ConfirmDialog.CreateForTest("เล่น PUBG: BATTLEGROUNDS?",
                    "Game Boost จะ:\n•  สลับเป็นโหมดพลังงานแรงสุด\n•  เปิด Discord พร้อมเกม", "เล่นเลย");
                target.Owner = window;
                target.ShowActivated = false;
                target.Show();
                break;
            case "add-watch":
                target = new AddWatchWindow { Owner = window, ShowActivated = false };
                target.Show();
                ready = ((AddWatchWindow)target).SearchForTest(GetArg(args, "--search") ?? "cyberpunk");
                break;
            case "storage":
                window.ShowPage("cleaner");
                ((CleanerView)window.CurrentPage!).ShowTab("storage");
                break;
            case not null:
                window.ShowPage(page);
                if (args.Contains("--scan") && window.CurrentPage is CleanerView cleaner) ready = cleaner.ScanAsync();
                break;
        }

        // รอให้แอนิเมชันและการโหลดเสร็จก่อนค่อยแคป
        var delay = int.TryParse(GetArg(args, "--delay"), out var ms) ? ms : 500;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(delay) };
        timer.Tick += async (_, _) =>
        {
            timer.Stop();
            await ready;
            await Task.Delay(300);
            SaveSnapshot(target, snapshotPath);
            if (GetArg(args, "--export-wrapped") is { } exportPath && window.CurrentPage is WrappedView wrapped)
                wrapped.ExportForTest(exportPath);
            Shutdown();
        };
        timer.Start();
    }

    private static void HideOffScreen(Window window)
    {
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = -30000;
        window.Top = 0;
        window.ShowActivated = false;
        window.ShowInTaskbar = false;
    }

    private static string? GetArg(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    private static void SaveSnapshot(Window window, string path)
    {
        // ชั้นนอกสุดในหน้าต่าง = พื้นที่ภายในทั้งหมด (รวม margin ของเนื้อหา)
        var root = (FrameworkElement)VisualTreeHelper.GetChild(window, 0);
        var bitmap = new RenderTargetBitmap((int)root.ActualWidth, (int)root.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(root);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path);
        encoder.Save(file);
    }
#endif
}
