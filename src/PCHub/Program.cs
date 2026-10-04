using PCHub.Services;
using Velopack;

namespace PCHub;

public static class Program
{
    // ตัวที่ build ไว้ทดสอบกับตัวที่ติดตั้งจริงแยกกัน จะได้เปิดพร้อมกันได้
#if DEBUG
    private const string InstanceName = "PCHub-kxmziii-dev";
#else
    private const string InstanceName = "PCHub-kxmziii";
#endif

    [STAThread]
    public static void Main(string[] args)
    {
        // ต้องเรียกเป็นอย่างแรกสุด: ตอนติดตั้ง/อัปเดต/ถอนการติดตั้ง Velopack จะเรียกโปรแกรมพร้อมคำสั่งพิเศษ
        // แล้วจัดการให้เสร็จ (เช่น สร้าง shortcut) โดยไม่ต้องเปิดหน้าต่าง
        VelopackApp.Build()
            .OnBeforeUninstallFastCallback(_ => StartupService.SetEnabled(false)) // ถอนแล้วไม่ต้องเปิดพร้อม Windows อีก
            .Run();

        // เปิดได้ตัวเดียว: ถ้าเปิดอยู่แล้ว (เช่น ย่อไว้มุมจอ) ให้ตัวเดิมเปิดหน้าต่างขึ้นมาแทน
        using var mutex = new Mutex(true, InstanceName, out var isFirstInstance);
        if (!isFirstInstance && !args.Contains("--snapshot"))
        {
            if (EventWaitHandle.TryOpenExisting(InstanceName + "-show", out var existing))
            {
                existing.Set();
                existing.Dispose();
            }
            return;
        }

        // ไม่ dispose: เธรดด้านล่างรอสัญญาณนี้ไปจนกว่าโปรแกรมจะปิด
        var showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, InstanceName + "-show");

        var app = new App();
        app.InitializeComponent();

        var listener = new Thread(() =>
        {
            while (showSignal.WaitOne())
                app.Dispatcher.BeginInvoke(() => (app.MainWindow as MainWindow)?.ShowFromTray());
        })
        {
            IsBackground = true,
        };
        listener.Start();

        app.Run();
    }
}
