using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using DesktopSprite.Models;
using DesktopSprite.Services;

namespace DesktopSprite;

public partial class MainWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;

    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;

    private const double CollapsedWidth = 84;
    private const double CollapsedHeight = 84;
    private const double ExpandedWidth = 300;
    private const double ExpandedHeight = 340;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int x, int y, int cx, int cy, uint flags);

    private readonly AppSettings _settings;
    private readonly MetricsService _metrics;

    private IntPtr _hwnd;
    private bool _cleaned;

    private bool _pressed;
    private bool _dragged;
    private Point _dragStartScreen;
    private Point _dragStartWindow;

    public MainWindow(AppSettings settings, MetricsService metrics)
    {
        _settings = settings;
        _metrics = metrics;

        InitializeComponent();

        _settings.StartWithWindows = SettingsService.IsAutoStartEnabled();

        Topmost = _settings.Topmost;

        ApplyViewSize();

        _metrics.Updated += OnMetricsUpdated;
        Hud.Update(MetricSnapshot.Empty);
        Hud.Initialize(_settings.Expanded);
        UpdateMenuState();
    }

    public AppSettings Settings => _settings;

    // ---------- 位置与尺寸 ----------

    private void ApplyViewSize()
    {
        bool expanded = _settings.Expanded;

        LayoutRoot.Width = expanded ? ExpandedWidth : CollapsedWidth;
        LayoutRoot.Height = expanded ? ExpandedHeight : CollapsedHeight;
        Width = LayoutRoot.Width * _settings.Scale;
        Height = LayoutRoot.Height * _settings.Scale;
    }

    private void PositionWindow()
    {
        double scale = GetDpiScale();
        var workArea = System.Windows.Forms.Screen.PrimaryScreen!.WorkingArea;

        if (_settings.Left is double left && _settings.Top is double top)
        {
            Left = left;
            Top = top;
        }
        else
        {
            Left = workArea.Right / scale - Width - 24;
            Top = workArea.Bottom / scale - Height - 24;
        }

        ClampToScreen();
    }

    private double GetDpiScale()
    {
        if (_hwnd != IntPtr.Zero)
        {
            uint dpi = GetDpiForWindow(_hwnd);
            if (dpi > 0)
                return dpi / 96.0;
        }

        DpiScale fallback = VisualTreeHelper.GetDpi(this);
        return fallback.DpiScaleX > 0 ? fallback.DpiScaleX : 1.0;
    }

    private void ClampToScreen()
    {
        // Screen / WorkArea 返回物理像素，Window.Left/Top 是 DIP，需要按 DPI 换算。
        double scale = GetDpiScale();
        var workArea = System.Windows.Forms.Screen.PrimaryScreen!.WorkingArea;

        double minX = workArea.Left / scale;
        double minY = workArea.Top / scale;
        double maxX = Math.Max(minX, workArea.Right / scale - Width);
        double maxY = Math.Max(minY, workArea.Bottom / scale - Height);

        Left = Math.Clamp(Left, minX, maxX);
        Top = Math.Clamp(Top, minY, maxY);
    }

    private void ApplyScale(double scale)
    {
        scale = Math.Clamp(scale, 0.65, 1.8);
        if (Math.Abs(scale - _settings.Scale) < 0.001)
            return;

        double centerX = Left + Width / 2;
        double centerY = Top + Height / 2;

        _settings.Scale = scale;
        ApplyViewSize();

        Left = centerX - Width / 2;
        Top = centerY - Height / 2;

        ClampToScreen();
        SaveSettings();
    }

    /// <summary>点击切换：四角圆环从中央圆环展开 / 收拢。</summary>
    public void ToggleExpanded()
    {
        if (Hud.IsAnimating)
            return;

        double centerX = Left + Width / 2;
        double centerY = Top + Height / 2;

        if (_settings.Expanded)
        {
            // 先播收拢动画，动画结束后再把窗口缩到图标大小。
            Hud.AnimateCollapse(() =>
            {
                _settings.Expanded = false;
                ApplyViewSize();
                Hud.BeginCollapsed();
                Hud.AnimatePop();

                Left = centerX - Width / 2;
                Top = centerY - Height / 2;
                ClampToScreen();
                SaveSettings();
            });
        }
        else
        {
            _settings.Expanded = true;
            ApplyViewSize();
            Hud.BeginExpanded();
            Hud.AnimateExpand();

            Left = centerX - Width / 2;
            Top = centerY - Height / 2;
            ClampToScreen();
            SaveSettings();
        }
    }

    public void ResetPosition()
    {
        double scale = GetDpiScale();
        var workArea = System.Windows.Forms.Screen.PrimaryScreen!.WorkingArea;
        Left = workArea.Right / scale - Width - 24;
        Top = workArea.Bottom / scale - Height - 24;
        ClampToScreen();
        SaveSettings();
    }

    // ---------- 交互 ----------

    private Point ToDip(Point screenPoint)
    {
        PresentationSource? source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget != null)
            return source.CompositionTarget.TransformFromDevice.Transform(screenPoint);
        return screenPoint;
    }

    private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _pressed = true;
        _dragged = false;
        _dragStartScreen = ToDip(PointToScreen(e.GetPosition(this)));
        _dragStartWindow = new Point(Left, Top);
        CaptureMouse();
    }

    private void OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_pressed)
            return;

        Point current = ToDip(PointToScreen(e.GetPosition(this)));
        double dx = current.X - _dragStartScreen.X;
        double dy = current.Y - _dragStartScreen.Y;

        if (!_dragged && (Math.Abs(dx) > 4 || Math.Abs(dy) > 4))
            _dragged = true;

        if (!_dragged || _settings.LockPosition)
            return;

        Left = _dragStartWindow.X + dx;
        Top = _dragStartWindow.Y + dy;
    }

    private void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_pressed)
            return;

        _pressed = false;
        ReleaseMouseCapture();

        if (_dragged)
        {
            ClampToScreen();
            SaveSettings();
        }
        else
        {
            ToggleExpanded();
        }
    }

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        ApplyScale(_settings.Scale + (e.Delta > 0 ? 0.08 : -0.08));
        e.Handled = true;
    }

    // ---------- 外观开关 ----------

    public void SetTopmost(bool value)
    {
        _settings.Topmost = value;
        Topmost = value;
        UpdateMenuState();
        SaveSettings();
    }

    public void SetClickThrough(bool value)
    {
        _settings.ClickThrough = value;
        ApplyClickThrough();
        UpdateMenuState();
        SaveSettings();
    }

    public void SetLockPosition(bool value)
    {
        _settings.LockPosition = value;
        UpdateMenuState();
        SaveSettings();
    }

    public void SetAutoStart(bool value)
    {
        _settings.StartWithWindows = value;
        SettingsService.SetAutoStart(value);
        UpdateMenuState();
        SaveSettings();
    }

    private void ApplyClickThrough()
    {
        if (_hwnd == IntPtr.Zero)
            return;

        int style = GetWindowLong(_hwnd, GwlExStyle);
        style = _settings.ClickThrough
            ? style | WsExTransparent
            : style & ~WsExTransparent;
        SetWindowLong(_hwnd, GwlExStyle, style);
    }

    // ---------- 窗口生命周期 ----------

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _hwnd = new WindowInteropHelper(this).Handle;

        PositionWindow();

        int exStyle = GetWindowLong(_hwnd, GwlExStyle) | WsExToolWindow;
        SetWindowLong(_hwnd, GwlExStyle, exStyle);
        SetWindowPos(_hwnd, IntPtr.Zero, 0, 0, 0, 0,
            SwpNoMove | SwpNoSize | SwpNoZOrder | SwpNoActivate | SwpFrameChanged);

        ApplyClickThrough();
    }

    public void ToggleVisibility()
    {
        if (IsVisible)
            Hide();
        else
            ShowFromTray();
    }

    public void ShowFromTray()
    {
        Show();
        Topmost = _settings.Topmost;
        Activate();
    }

    public void ExitApplication()
    {
        SaveSettings();
        Application.Current.Shutdown();
    }

    public void SaveSettings()
    {
        _settings.Left = Left;
        _settings.Top = Top;
        SettingsService.Save(_settings);
    }

    public void Cleanup()
    {
        if (_cleaned)
            return;
        _cleaned = true;
        _metrics.Updated -= OnMetricsUpdated;
        SaveSettings();
    }

    protected override void OnClosed(EventArgs e)
    {
        Cleanup();
        base.OnClosed(e);
        Dispatcher.BeginInvoke(new Action(() => Application.Current.Shutdown()));
    }

    // ---------- 数据更新 ----------

    private void OnMetricsUpdated(MetricsEventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            Hud.Update(e.Snapshot);
            ToolTip = BuildTooltip(e.Snapshot);
        }));
    }

    private static string BuildTooltip(MetricSnapshot s)
    {
        string gpu = s.GpuAvailable
            ? $"{s.GpuPercent:0}%  {s.GpuTemp:0}°C"
            : "N/A";

        return $"CPU {s.CpuPercent:0}%\n" +
               $"GPU {gpu}\n" +
               $"显存 {s.VramUsedMb:0}/{s.VramTotalMb:0} MB\n" +
               $"内存 {s.MemUsedGb:0.0}/{s.MemTotalGb:0.0} GB\n" +
               $"网络 ↓{s.NetDownBytesPerSec / 1024:0} KB/s ↑{s.NetUpBytesPerSec / 1024:0} KB/s";
    }

    // ---------- 右键菜单 ----------

    private void UpdateMenuState()
    {
        MenuTopmost.IsChecked = _settings.Topmost;
        MenuClickThrough.IsChecked = _settings.ClickThrough;
        MenuLock.IsChecked = _settings.LockPosition;
        MenuAutoStart.IsChecked = _settings.StartWithWindows;
    }

    private void MenuTopmost_Click(object sender, RoutedEventArgs e) => SetTopmost(!_settings.Topmost);

    private void MenuClickThrough_Click(object sender, RoutedEventArgs e) => SetClickThrough(!_settings.ClickThrough);

    private void MenuLock_Click(object sender, RoutedEventArgs e) => SetLockPosition(!_settings.LockPosition);

    private void MenuAutoStart_Click(object sender, RoutedEventArgs e) => SetAutoStart(!_settings.StartWithWindows);

    private void MenuResetPosition_Click(object sender, RoutedEventArgs e) => ResetPosition();

    private void MenuAbout_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "桌面精灵 DesktopSprite\n" +
            "版本：V1.0\n\n" +
            "作者：QuinnZ\n" +
            "AI 工具：opencode\n" +
            "制作地点：中国，上海\n\n" +
            "Copyright (c) 2026 QuinnZ",
            "关于",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void MenuExit_Click(object sender, RoutedEventArgs e) => ExitApplication();
}
