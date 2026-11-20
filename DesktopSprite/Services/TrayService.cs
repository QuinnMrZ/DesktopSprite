using System.Runtime.InteropServices;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace DesktopSprite.Services;

/// <summary>系统托盘图标与菜单。</summary>
public sealed class TrayService : IDisposable
{
    private readonly MainWindow _window;
    private readonly AppSettings _settings;

    private Forms.NotifyIcon? _notifyIcon;
    private Forms.ContextMenuStrip? _menu;

    private Forms.ToolStripMenuItem _itemToggle = null!;
    private Forms.ToolStripMenuItem _itemTopmost = null!;
    private Forms.ToolStripMenuItem _itemClickThrough = null!;
    private Forms.ToolStripMenuItem _itemLock = null!;
    private Forms.ToolStripMenuItem _itemAutoStart = null!;

    private IntPtr _iconHandle;
    private bool _disposed;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public TrayService(MainWindow window, AppSettings settings)
    {
        _window = window;
        _settings = settings;
    }

    public void Initialize()
    {
        _menu = new Forms.ContextMenuStrip
        {
            ShowImageMargin = false
        };
        _menu.Opening += (_, _) => SyncState();

        _itemToggle = new Forms.ToolStripMenuItem("隐藏精灵");
        _itemToggle.Click += (_, _) => _window.ToggleVisibility();

        _itemTopmost = new Forms.ToolStripMenuItem("始终置顶");
        _itemTopmost.Click += (_, _) => _window.SetTopmost(!_settings.Topmost);

        _itemClickThrough = new Forms.ToolStripMenuItem("点击穿透");
        _itemClickThrough.Click += (_, _) => _window.SetClickThrough(!_settings.ClickThrough);

        _itemLock = new Forms.ToolStripMenuItem("锁定位置");
        _itemLock.Click += (_, _) => _window.SetLockPosition(!_settings.LockPosition);

        _itemAutoStart = new Forms.ToolStripMenuItem("开机启动");
        _itemAutoStart.Click += (_, _) => _window.SetAutoStart(!_settings.StartWithWindows);

        var itemReset = new Forms.ToolStripMenuItem("重置位置");
        itemReset.Click += (_, _) => _window.ResetPosition();

        var itemExit = new Forms.ToolStripMenuItem("退出");
        itemExit.Click += (_, _) => _window.ExitApplication();

        _menu.Items.AddRange(new Forms.ToolStripItem[]
        {
            _itemToggle,
            new Forms.ToolStripSeparator(),
            _itemTopmost,
            _itemClickThrough,
            _itemLock,
            _itemAutoStart,
            new Forms.ToolStripSeparator(),
            itemReset,
            itemExit
        });

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = CreateIcon(),
            Text = "桌面精灵",
            Visible = true,
            ContextMenuStrip = _menu
        };
        _notifyIcon.DoubleClick += (_, _) => _window.ShowFromTray();
    }

    private void SyncState()
    {
        _itemToggle.Text = _window.IsVisible ? "隐藏精灵" : "显示精灵";
        _itemTopmost.Checked = _settings.Topmost;
        _itemClickThrough.Checked = _settings.ClickThrough;
        _itemLock.Checked = _settings.LockPosition;
        _itemAutoStart.Checked = _settings.StartWithWindows;
    }

    private Drawing.Icon CreateIcon()
    {
        using var bitmap = new Drawing.Bitmap(32, 32);
        using (var g = Drawing.Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Drawing.Color.Transparent);

            using var body = new Drawing.SolidBrush(Drawing.Color.FromArgb(92, 199, 172));
            g.FillEllipse(body, 3, 4, 26, 24);

            using var ear = new Drawing.SolidBrush(Drawing.Color.FromArgb(86, 192, 166));
            g.FillEllipse(ear, 6, 1, 7, 8);
            g.FillEllipse(ear, 19, 1, 7, 8);

            using var eye = new Drawing.SolidBrush(Drawing.Color.White);
            g.FillEllipse(eye, 10, 12, 5, 7);
            g.FillEllipse(eye, 18, 12, 5, 7);
        }

        _iconHandle = bitmap.GetHicon();
        return Drawing.Icon.FromHandle(_iconHandle);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }

        _menu?.Dispose();
        _menu = null;

        if (_iconHandle != IntPtr.Zero)
        {
            DestroyIcon(_iconHandle);
            _iconHandle = IntPtr.Zero;
        }
    }
}
