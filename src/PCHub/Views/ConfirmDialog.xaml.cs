using System.Windows;
using PCHub.Helpers;

namespace PCHub.Views;

/// <summary>กล่องถามยืนยันแบบธีมมืด (ใช้แทน MessageBox สีขาว)</summary>
public partial class ConfirmDialog : Window
{
    private ConfirmDialog() => InitializeComponent();

    /// <summary>คืนค่า true ถ้ากดปุ่มยืนยัน</summary>
    public static bool Show(Window owner, string title, string message, string confirmText, bool danger = false)
    {
        var dialog = new ConfirmDialog { Owner = owner, Title = title };
        dialog.TitleText.Text = title;
        dialog.MessageText.Text = message;
        dialog.ConfirmButton.Content = confirmText;
        if (danger) dialog.ConfirmButton.Style = (Style)dialog.FindResource("DangerButton");
        return dialog.ShowDialog() == true;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        DarkTitleBar.Apply(this);
    }

    private void Confirm_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
