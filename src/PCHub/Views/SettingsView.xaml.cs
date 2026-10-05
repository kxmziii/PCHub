using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using PCHub.Services;
using Velopack;

namespace PCHub.Views;

public partial class SettingsView : UserControl
{
    private UpdateInfo? _update;

    private readonly bool _ready;

    public SettingsView()
    {
        InitializeComponent();
        VersionText.Text = "PC Hub เวอร์ชัน " + Assembly.GetExecutingAssembly().GetName().Version?.ToString(3);

        TraySwitch.IsChecked = SettingsService.General.MinimizeToTray;
        StartupSwitch.IsChecked = StartupService.IsEnabled;
        TrackSwitch.IsChecked = SettingsService.General.AutoTrackGames;
        AlertSwitch.IsChecked = SettingsService.General.DealAlerts;
        ConfirmPlaySwitch.IsChecked = SettingsService.General.ConfirmBeforePlay;
        _ready = true; // ตั้งค่าเริ่มต้นเสร็จ ต่อจากนี้กดสวิตช์ถึงจะบันทึก

        // โชว์ว่า PC Hub ใช้แรมเท่าไหร่ ใครก็เช็คได้ว่าเบาจริง
        IsVisibleChanged += (_, e) =>
        {
            if (!(bool)e.NewValue) return;
            using var self = System.Diagnostics.Process.GetCurrentProcess();
            MemoryText.Text = $"ตอนนี้ใช้แรมประมาณ {Helpers.SizeFormatter.Format(self.WorkingSet64)} (ย่อไว้มุมจอจะคืนแรมให้ Windows)";
        };

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

    private void Report_Click(object sender, RoutedEventArgs e)
    {
        App.OpenProblemReport();
        Toast.Show("สร้างไฟล์รายงานไว้บน Desktop แล้ว ส่งไฟล์นี้ให้คนทำ PC Hub ได้เลย");
    }

    private void OpenLogs_Click(object sender, RoutedEventArgs e)
    {
        System.IO.Directory.CreateDirectory(AppLog.FolderPath);
        System.Diagnostics.Process.Start("explorer.exe", $"\"{AppLog.FolderPath}\"")?.Dispose();
    }

    private void Switch_Changed(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;

        SettingsService.General.MinimizeToTray = TraySwitch.IsChecked == true;
        SettingsService.General.AutoTrackGames = TrackSwitch.IsChecked == true;
        SettingsService.General.DealAlerts = AlertSwitch.IsChecked == true;
        SettingsService.General.ConfirmBeforePlay = ConfirmPlaySwitch.IsChecked == true;
        SettingsService.Save();

        if (sender == StartupSwitch)
        {
            var enabled = StartupSwitch.IsChecked == true;
            StartupService.SetEnabled(enabled);
            Toast.Show(enabled ? "เปิดเครื่องครั้งหน้า PC Hub จะรอที่มุมจอให้เลย" : "ไม่เปิด PC Hub พร้อม Windows แล้ว");
        }
        else
        {
            Toast.Show("บันทึกแล้ว");
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
