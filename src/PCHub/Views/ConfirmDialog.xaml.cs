using System.Windows;
using PCHub.Helpers;

namespace PCHub.Views;

/// <summary>กล่องถามยืนยันแบบธีมมืด (ใช้แทน MessageBox สีขาว)</summary>
public partial class ConfirmDialog : Window
{
    private ConfirmDialog() => InitializeComponent();

    /// <summary>คืนค่า true ถ้ากดปุ่มยืนยัน</summary>
    public static bool Show(Window owner, string title, string message, string confirmText, bool danger = false) =>
        ShowWithDontAsk(owner, title, message, confirmText, danger, showDontAsk: false).Confirmed;

    /// <summary>
    /// แบบมีช่อง "ไม่ต้องถามอีก" คืนค่าว่ากดยืนยันไหม และติ๊กช่องนั้นไหม
    /// (ติ๊กแล้วกดยกเลิก ไม่นับ จะได้ไม่ปิดการถามโดยไม่ตั้งใจ)
    /// </summary>
    public static (bool Confirmed, bool DontAskAgain) ShowWithDontAsk(
        Window owner, string title, string message, string confirmText, bool danger = false, bool showDontAsk = true)
    {
        var dialog = new ConfirmDialog { Owner = owner, Title = title };
        dialog.TitleText.Text = title;
        dialog.MessageText.Text = message;
        dialog.ConfirmButton.Content = confirmText;
        dialog.DontAskCheck.Visibility = showDontAsk ? Visibility.Visible : Visibility.Collapsed;
        if (danger) dialog.ConfirmButton.Style = (Style)dialog.FindResource("DangerButton");

        var confirmed = dialog.ShowDialog() == true;
        return (confirmed, confirmed && dialog.DontAskCheck.IsChecked == true);
    }

#if DEBUG
    /// <summary>ใช้ตอนพัฒนา: สร้างกล่องไว้แคปหน้าจอ (ไม่รอให้กด)</summary>
    public static ConfirmDialog CreateForTest(string title, string message, string confirmText)
    {
        var dialog = new ConfirmDialog { Title = title };
        dialog.TitleText.Text = title;
        dialog.MessageText.Text = message;
        dialog.ConfirmButton.Content = confirmText;
        dialog.DontAskCheck.Visibility = Visibility.Visible;
        return dialog;
    }
#endif

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        DarkTitleBar.Apply(this);
    }

    private void Confirm_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
