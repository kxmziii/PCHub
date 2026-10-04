using System.Globalization;
using System.Windows.Data;
using PCHub.Services;

namespace PCHub.Views;

/// <summary>ใช้ใน XAML: แปลงที่อยู่รูปปกเกม → รูป (ย่อเหลือกว้าง 320px ประหยัดแรม)</summary>
public class CoverImageConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is string path ? ImageLoader.Load(path, 320) : null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
