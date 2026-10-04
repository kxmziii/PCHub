using System.Windows;
using System.Windows.Controls;

namespace PCHub.Views;

/// <summary>ปุ่มเมนูด้านซ้าย: เหมือน RadioButton แต่มีไอคอนเพิ่ม (Tag = ชื่อหน้า)</summary>
public class NavButton : RadioButton
{
    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(nameof(Icon), typeof(string), typeof(NavButton), new PropertyMetadata(""));

    public string Icon
    {
        get => (string)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }
}
