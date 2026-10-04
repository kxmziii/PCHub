using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using PCHub.Helpers;
using PCHub.Models;
using PCHub.Services;

namespace PCHub.Views;

public partial class CleanerView : UserControl
{
    private readonly List<CleanupCategory> _categories = CleanerService.CreateCategories();
    private bool _busy;

    public CleanerView()
    {
        InitializeComponent();
        CategoryList.ItemsSource = _categories;
        foreach (var category in _categories) category.PropertyChanged += Category_PropertyChanged;

        SummaryTitle.Text = "กดสแกนเพื่อดูว่ามีขยะเท่าไหร่";
        SummaryDetail.Text = "สแกนเฉยๆ ยังไม่ลบอะไรนะ จะลบก็ต่อเมื่อกดปุ่มลบแล้วยืนยันเองเท่านั้น";
    }

    private async void Scan_Click(object sender, RoutedEventArgs e) => await ScanAsync();

    public async Task ScanAsync()
    {
        if (_busy) return;
        SetBusy(true);
        SummaryTitle.Text = "กำลังสแกน...";
        foreach (var category in _categories)
        {
            category.LastScan = null;
            category.SizeText = "รอสแกน";
        }

        for (var i = 0; i < _categories.Count; i++)
        {
            var category = _categories[i];
            SummaryDetail.Text = $"กำลังดู: {category.Name}";
            category.SizeText = "กำลังสแกน...";

            var scan = await Task.Run(() => CleanerService.Scan(category.Kind));
            category.LastScan = scan;
            category.SizeText = scan.TotalBytes > 0 ? SizeFormatter.Format(scan.TotalBytes) : "สะอาดแล้ว";
            Progress.Value = (i + 1.0) / _categories.Count;
        }

        var total = _categories.Sum(c => c.LastScan?.TotalBytes ?? 0);
        SummaryTitle.Text = total > 0 ? $"พบขยะ {SizeFormatter.Format(total)}" : "เครื่องสะอาดแล้ว";
        SetBusy(false);
        UpdateSelectedTotal();
    }

    private async void Clean_Click(object sender, RoutedEventArgs e)
    {
        var selected = SelectedWithJunk();
        if (selected.Count == 0) return;

        var total = selected.Sum(c => c.LastScan!.TotalBytes);
        var list = string.Join("\n", selected.Select(c => $"•  {c.Name}   {SizeFormatter.Format(c.LastScan!.TotalBytes)}"));
        if (selected.Any(c => c.Kind == CleanupKind.RecycleBin))
            list += "\n\nไฟล์ในถังขยะจะหายถาวร กู้คืนไม่ได้";

        if (!ConfirmDialog.Show(Window.GetWindow(this)!, $"ลบขยะ {SizeFormatter.Format(total)}?", list, "ลบเลย")) return;

        SetBusy(true);
        SummaryTitle.Text = "กำลังลบ...";
        long freed = 0;
        var skipped = 0;

        for (var i = 0; i < selected.Count; i++)
        {
            var category = selected[i];
            var scan = category.LastScan!;
            SummaryDetail.Text = $"กำลังลบ: {category.Name}";
            category.SizeText = "กำลังลบ...";

            var start = (double)i / selected.Count;
            var progress = new Progress<double>(p => Progress.Value = start + p / selected.Count);
            var result = await Task.Run(() => CleanerService.Clean(category.Kind, scan, progress));

            freed += result.FreedBytes;
            skipped += result.SkippedFiles;
            var remaining = Math.Max(0, scan.TotalBytes - result.FreedBytes);
            category.SizeText = remaining > 0 ? $"เหลือ {SizeFormatter.Format(remaining)}" : "ลบแล้ว";
            // ลบรอบนี้เสร็จแล้ว จะลบอีกต้องสแกนใหม่ก่อน (รายชื่อไฟล์เดิมใช้ไม่ได้แล้ว)
            category.LastScan = new ScanResult([], 0);
        }

        SetBusy(false);
        CleanButton.IsEnabled = false;
        CleanButtonText.Text = "ลบที่เลือก";
        SummaryTitle.Text = $"ลบไปแล้ว {SizeFormatter.Format(freed)}";
        SummaryDetail.Text = skipped > 0
            ? $"ข้ามไป {skipped:N0} ไฟล์ที่โปรแกรมกำลังใช้อยู่ (ปกติ ไม่ต้องห่วง) ปิดโปรแกรมพวกนั้นแล้วสแกนใหม่ได้"
            : "เรียบร้อย กดสแกนใหม่เพื่อเช็คอีกรอบได้";
    }

    private void Category_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CleanupCategory.IsSelected)) UpdateSelectedTotal();
    }

    /// <summary>อัปเดตปุ่มลบให้ตรงกับหมวดที่ติ๊กไว้</summary>
    private void UpdateSelectedTotal()
    {
        if (_busy || _categories.All(c => c.LastScan == null)) return;

        var bytes = SelectedWithJunk().Sum(c => c.LastScan!.TotalBytes);
        CleanButton.IsEnabled = bytes > 0;
        CleanButtonText.Text = bytes > 0 ? $"ลบที่เลือก ({SizeFormatter.Format(bytes)})" : "ลบที่เลือก";
        SummaryDetail.Text = bytes > 0
            ? $"เลือกไว้ {SizeFormatter.Format(bytes)} ติ๊กเลือกหรือเอาออกได้ตามใจ"
            : "ยังไม่ได้เลือกหมวดที่มีขยะ";
    }

    private List<CleanupCategory> SelectedWithJunk() =>
        _categories.Where(c => c.IsSelected && c.LastScan is { TotalBytes: > 0 }).ToList();

    private void SetBusy(bool busy)
    {
        _busy = busy;
        ScanButton.IsEnabled = !busy;
        CategoryList.IsEnabled = !busy;
        if (busy)
        {
            CleanButton.IsEnabled = false;
            Progress.Value = 0;
        }
        Progress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
    }
}
