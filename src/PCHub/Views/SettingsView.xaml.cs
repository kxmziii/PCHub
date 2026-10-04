using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using PCHub.Services;
using Velopack;

namespace PCHub.Views;

public partial class SettingsView : UserControl
{
    private UpdateInfo? _update;

    public SettingsView()
    {
        InitializeComponent();
        VersionText.Text = "PC Hub เวอร์ชัน " + Assembly.GetExecutingAssembly().GetName().Version?.ToString(3);

        if (!UpdateService.IsAvailable)
        {
            UpdateButton.IsEnabled = false;
            UpdateStatus.Text = UpdateService.GitHubRepo.Length == 0
                ? "ระบบอัปเดตอัตโนมัติจะเปิดใช้หลังเชื่อมกับ GitHub"
                : "ระบบอัปเดตใช้ได้เมื่อติดตั้งผ่าน PCHub-win-Setup.exe";
        }
        else
        {
            UpdateStatus.Text = "กดเพื่อดูว่ามีเวอร์ชันใหม่ไหม";
        }
    }

    private async void Update_Click(object sender, RoutedEventArgs e)
    {
        UpdateButton.IsEnabled = false;

        // กดครั้งแรก = เช็ค, ถ้ามีเวอร์ชันใหม่ กดอีกครั้ง = อัปเดตเลย
        if (_update != null)
        {
            UpdateStatus.Text = "กำลังโหลดอัปเดต PC Hub จะเปิดใหม่เองเมื่อเสร็จ...";
            await UpdateService.DownloadAndRestartAsync(_update);
            return;
        }

        UpdateStatus.Text = "กำลังเช็ค...";
        _update = await UpdateService.CheckAsync();
        if (_update == null)
        {
            UpdateStatus.Text = "ใช้เวอร์ชันล่าสุดอยู่แล้ว";
            UpdateButton.IsEnabled = true;
            return;
        }

        UpdateStatus.Text = $"มีเวอร์ชันใหม่ {_update.TargetFullRelease.Version}";
        UpdateButtonText.Text = "อัปเดตเลย";
        UpdateButton.IsEnabled = true;
        Toast.Show($"มี PC Hub เวอร์ชันใหม่ {_update.TargetFullRelease.Version} กด \"อัปเดตเลย\" ได้เลย");
    }
}
