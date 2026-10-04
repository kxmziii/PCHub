using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace PCHub.Views;

/// <summary>แถบแจ้งผลด้านล่างหน้า โผล่มา 4 วินาทีแล้วหายไปเอง</summary>
public partial class StatusToast : UserControl
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(4) };

    public StatusToast()
    {
        InitializeComponent();
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            Visibility = Visibility.Collapsed;
        };
    }

    public void Show(string message, bool isError = false)
    {
        MessageText.Text = message;
        IconText.Text = isError ? "" : ""; // ตกใจ / ติ๊กถูก
        IconText.Foreground = (Brush)FindResource(isError ? "Danger" : "Success");
        Visibility = Visibility.Visible;
        _timer.Stop();
        _timer.Start();
    }
}
