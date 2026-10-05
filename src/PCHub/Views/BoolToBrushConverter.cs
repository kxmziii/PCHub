using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PCHub.Views;

/// <summary>ใช้ใน XAML: true/false → สีที่กำหนด (เช่น ดาวสีเหลืองเมื่อติดดาว)</summary>
public class BoolToBrushConverter : IValueConverter
{
    public Brush? TrueBrush { get; set; }
    public Brush? FalseBrush { get; set; }

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? TrueBrush : FalseBrush;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
