using System.Globalization;
using System.Windows.Data;
using PCHub.Services;

namespace PCHub.Views;

/// <summary>ใช้ใน XAML: แปลงที่อยู่ไฟล์ → ไอคอนของไฟล์นั้น</summary>
public class PathToIconConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is string path ? IconService.GetIcon(path) : null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
