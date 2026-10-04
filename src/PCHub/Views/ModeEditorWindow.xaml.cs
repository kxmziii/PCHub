using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using PCHub.Helpers;
using PCHub.Models;
using PCHub.Services;

namespace PCHub.Views;

/// <summary>หน้าต่างสร้าง/แก้ไขโหมด: ตั้งชื่อ เลือกไอคอน เลือกแอพ</summary>
public partial class ModeEditorWindow : Window
{
    // เกม, วิดีโอ, โค้ด, เพลง, เว็บ, เรียน, ทำงาน, บ้าน, ดาว, สายฟ้า
    private static readonly string[] IconChoices =
        ["", "", "", "", "", "", "", "", "", ""];

    private readonly ObservableCollection<AppEntry> _apps;
    private readonly ListCollectionView _installedView;

    /// <summary>โหมดที่แก้เสร็จแล้ว (มีค่าเมื่อกดบันทึก)</summary>
    public LaunchMode? Result { get; private set; }

    /// <summary>true ถ้ากดลบโหมดนี้</summary>
    public bool Deleted { get; private set; }

    public ModeEditorWindow(LaunchMode mode, bool isNew)
    {
        InitializeComponent();

        Title = isNew ? "สร้างโหมดใหม่" : "แก้ไขโหมด";
        HeaderText.Text = Title;
        DeleteButton.Visibility = isNew ? Visibility.Collapsed : Visibility.Visible;
        NameBox.Text = mode.Name;

        IconList.ItemsSource = IconChoices;
        IconList.SelectedItem = IconChoices.Contains(mode.Icon) ? mode.Icon : IconChoices[^1];

        // แก้บนสำเนา กดยกเลิกแล้วโหมดเดิมจะไม่เปลี่ยน
        _apps = new ObservableCollection<AppEntry>(mode.Apps.Select(a => a.Clone()));
        _apps.CollectionChanged += (_, _) => UpdateSelectedHeader();
        SelectedList.ItemsSource = _apps;
        UpdateSelectedHeader();

        _installedView = new ListCollectionView(InstalledAppsService.GetAll()) { Filter = MatchesSearch };
        InstalledList.ItemsSource = _installedView;

        Loaded += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        DarkTitleBar.Apply(this);
    }

    private bool MatchesSearch(object item)
    {
        var text = SearchBox.Text.Trim();
        return text.Length == 0 || ((AppEntry)item).Name.Contains(text, StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateSelectedHeader()
    {
        SelectedHeader.Text = $"แอพในโหมดนี้ ({_apps.Count})";
        EmptyHint.Visibility = _apps.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AddApp(AppEntry app)
    {
        if (_apps.Any(a => a.Path.Equals(app.Path, StringComparison.OrdinalIgnoreCase) && a.Arguments == app.Arguments)) return;
        _apps.Add(app.Clone());
    }

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) =>
        _installedView?.Refresh();

    private void AddApp_Click(object sender, RoutedEventArgs e) =>
        AddApp((AppEntry)((FrameworkElement)sender).DataContext);

    private void InstalledList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (InstalledList.SelectedItem is AppEntry app) AddApp(app);
    }

    private void RemoveApp_Click(object sender, RoutedEventArgs e) =>
        _apps.Remove((AppEntry)((FrameworkElement)sender).DataContext);

    private void BrowseFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "เลือกโปรแกรม",
            Filter = "โปรแกรม (*.exe, *.lnk, *.url, *.bat)|*.exe;*.lnk;*.url;*.bat|ทุกไฟล์ (*.*)|*.*",
        };
        if (dialog.ShowDialog(this) == true)
            AddApp(new AppEntry { Name = Path.GetFileNameWithoutExtension(dialog.FileName), Path = dialog.FileName });
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text.Trim();
        if (name.Length == 0)
        {
            NameError.Visibility = Visibility.Visible;
            NameBox.Focus();
            return;
        }

        Result = new LaunchMode { Name = name, Icon = IconList.SelectedItem as string ?? IconChoices[^1], Apps = _apps.ToList() };
        DialogResult = true;
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        var confirmed = ConfirmDialog.Show(this, "ลบโหมดนี้?",
            $"โหมด \"{NameBox.Text.Trim()}\" จะถูกลบออกจาก PC Hub\n(ตัวแอพในเครื่องไม่ได้ถูกลบนะ)",
            "ลบโหมด", danger: true);
        if (!confirmed) return;

        Deleted = true;
        DialogResult = true;
    }
}
