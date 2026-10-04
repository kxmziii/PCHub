using System.Globalization;
using System.Windows;
using System.Windows.Data;
using PCHub.Services;

namespace PCHub.Views;

/// <summary>ใช้ใน XAML: ระดับปิง → สี (เขียว / เหลือง / แดง)</summary>
public class PingLevelBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        Application.Current.FindResource(value switch
        {
            PingLevel.Good => "Success",
            PingLevel.Okay => "Warning",
            PingLevel.Bad or PingLevel.Failed => "Danger",
            _ => "TextMuted",
        });

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
