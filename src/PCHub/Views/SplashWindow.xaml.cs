using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;
using PCHub.Services;

namespace PCHub.Views;

/// <summary>หน้าโหลดตอนเปิดโปรแกรม: โหลดการตั้งค่าและหาเกมไว้ล่วงหน้า เข้าโปรแกรมแล้วจะได้ไม่ต้องรอ</summary>
public partial class SplashWindow : Window
{
    // โชว์อย่างน้อยเท่านี้ ไม่งั้นเครื่องเร็วๆ จะเห็นแค่วูบเดียว
    private static readonly TimeSpan MinimumTime = TimeSpan.FromMilliseconds(1400);
    private static readonly TimeSpan FadeTime = TimeSpan.FromMilliseconds(220);

    public SplashWindow()
    {
        InitializeComponent();
        VersionText.Text = "v" + Assembly.GetExecutingAssembly().GetName().Version?.ToString(3);
    }

    public async Task LoadAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        BeginAnimation(OpacityProperty, new DoubleAnimation(1, FadeTime));

        SetStep("กำลังโหลดการตั้งค่า...", 0.3);
        await Task.Run(() => SettingsService.Current);

        SetStep("กำลังหาเกมในเครื่อง...", 0.85);
        try
        {
            await GameLibraryService.GetAsync();
        }
        catch (Exception)
        {
            // แค่โหลดล่วงหน้า ถ้าพลาด หน้าคลังเกมจะลองหาใหม่เอง ไม่ต้องให้โปรแกรมเปิดไม่ขึ้น
        }

        SetStep("พร้อมแล้ว!", 1);
        var remaining = MinimumTime - stopwatch.Elapsed;
        if (remaining > TimeSpan.Zero) await Task.Delay(remaining);
    }

    public async Task FadeOutAndCloseAsync()
    {
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, FadeTime));
        await Task.Delay(FadeTime);
        Close();
    }

    /// <summary>เปลี่ยนข้อความ และเลื่อนแถบโหลดแบบนุ่มๆ</summary>
    private void SetStep(string status, double progress)
    {
        StatusText.Text = status;
        Progress.BeginAnimation(RangeBase.ValueProperty, new DoubleAnimation(progress, TimeSpan.FromMilliseconds(450))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        });
    }
}
