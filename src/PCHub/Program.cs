using Velopack;

namespace PCHub;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // ต้องเรียกเป็นอย่างแรกสุด: ตอนติดตั้ง/อัปเดต/ถอนการติดตั้ง Velopack จะเรียกโปรแกรมพร้อมคำสั่งพิเศษ
        // แล้วจัดการให้เสร็จ (เช่น สร้าง shortcut) โดยไม่ต้องเปิดหน้าต่าง
        VelopackApp.Build().Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
