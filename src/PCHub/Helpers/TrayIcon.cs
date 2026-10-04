using System.Drawing;
using System.Windows.Forms;

namespace PCHub.Helpers;

/// <summary>ไอคอน PC Hub ที่มุมจอข้างนาฬิกา (คลิก = เปิดหน้าต่าง, คลิกขวา = เมนู)</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;

    public TrayIcon(Action open, Action exit)
    {
        var menu = new ContextMenuStrip
        {
            Renderer = new ToolStripProfessionalRenderer(new DarkMenuColors()),
            ShowImageMargin = false,
            BackColor = Color.FromArgb(34, 37, 43),
            ForeColor = Color.FromArgb(227, 229, 232),
            Font = new Font("Segoe UI", 10f),
            Padding = new Padding(4),
        };
        menu.Items.Add("เปิด PC Hub", null, (_, _) => open());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("ออกจากโปรแกรม", null, (_, _) => exit());

        using var stream = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico")).Stream;
        _icon = new NotifyIcon
        {
            Icon = new Icon(stream, SystemInformation.SmallIconSize),
            Text = "PC Hub",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) open();
        };
        _icon.BalloonTipClicked += (_, _) => _onNotificationClick?.Invoke();
    }

    private Action? _onNotificationClick;

    /// <summary>กล่องข้อความเล็กๆ ที่เด้งจากมุมจอ (onClick = ทำอะไรเมื่อกดที่ข้อความ)</summary>
    public void ShowHint(string title, string text, Action? onClick = null)
    {
        _onNotificationClick = onClick;
        // Windows รับข้อความได้ไม่เกิน 255 ตัวอักษร
        _icon.ShowBalloonTip(5000, title, text.Length > 250 ? text[..247] + "..." : text, ToolTipIcon.None);
    }

    public void Dispose()
    {
        _icon.Visible = false; // ไม่งั้นไอคอนค้างที่มุมจอจนกว่าจะเอาเมาส์ไปชี้
        _icon.Dispose();
    }

    /// <summary>สีเมนูคลิกขวาแบบมืด ให้เข้ากับธีมของโปรแกรม</summary>
    private sealed class DarkMenuColors : ProfessionalColorTable
    {
        private static readonly Color Background = Color.FromArgb(34, 37, 43);
        private static readonly Color Border = Color.FromArgb(44, 48, 56);
        private static readonly Color Hover = Color.FromArgb(47, 52, 61);

        public override Color ToolStripDropDownBackground => Background;
        public override Color ImageMarginGradientBegin => Background;
        public override Color ImageMarginGradientMiddle => Background;
        public override Color ImageMarginGradientEnd => Background;
        public override Color MenuBorder => Border;
        public override Color MenuItemBorder => Hover;
        public override Color MenuItemSelected => Hover;
        public override Color SeparatorDark => Border;
        public override Color SeparatorLight => Border;
    }
}
