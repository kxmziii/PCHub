using System.Windows;
#if DEBUG
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
#endif

namespace PCHub;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var window = new MainWindow();
        MainWindow = window;
        window.Show();

#if DEBUG
        // ใช้ตอนพัฒนา: PCHub.exe --snapshot out.png [--page cleaner | mode-editor | boost] [--scan] [--delay ms] [--size 1400x900] [--demo-session]
        // เปิดหน้าที่ต้องการ แคปหน้าจอเป็นรูป แล้วปิดโปรแกรม (--scan = สั่งสแกนขยะก่อนแคป, แค่สแกนไม่ลบ)
        var snapshotPath = GetArg(e.Args, "--snapshot");
        if (snapshotPath != null)
        {
            Window target = window;
            Task ready = Task.CompletedTask;
            if (e.Args.Contains("--demo-session")) window.ShowDemoSessionCard();
            if (GetArg(e.Args, "--size")?.Split('x') is [var w, var h])
            {
                window.Width = double.Parse(w);
                window.Height = double.Parse(h);
            }
            var page = GetArg(e.Args, "--page");
            if (page == "mode-editor")
            {
                var editor = new Views.ModeEditorWindow(Services.SettingsService.Current.Modes[0], isNew: false) { Owner = window };
                editor.Show();
                target = editor;
            }
            else if (page == "boost")
            {
                var boost = new Views.BoostSettingsWindow { Owner = window };
                boost.Show();
                target = boost;
            }
            else if (page != null)
            {
                window.ShowPage(page);
                if (e.Args.Contains("--scan") && window.CurrentPage is Views.CleanerView cleaner)
                    ready = cleaner.ScanAsync();
            }
            // รอให้แอนิเมชันเฟดหน้าเล่นจบก่อนค่อยแคป
            var delay = int.TryParse(GetArg(e.Args, "--delay"), out var ms) ? ms : 500;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(delay) };
            timer.Tick += async (_, _) =>
            {
                timer.Stop();
                await ready;
                await Task.Delay(300);
                SaveSnapshot(target, snapshotPath);
                Shutdown();
            };
            timer.Start();
        }
#endif
    }

#if DEBUG
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
