using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using DesktopSprite.Models;

namespace DesktopSprite.Views;

/// <summary>
/// 霓虹 HUD 主视图。
/// 收起态：只有中央圆环，轮播 CPU / GPU / 显存 / 内存 / 网络。
/// 展开态：四角圆环从中央圆环处展开。
/// </summary>
public partial class HudView : UserControl
{
    private static readonly Color Cyan = Color.FromRgb(0x22, 0xD3, 0xEE);
    private static readonly Color Magenta = Color.FromRgb(0xE8, 0x79, 0xF9);
    private static readonly Color Amber = Color.FromRgb(0xFB, 0xBF, 0x24);
    private static readonly Color Green = Color.FromRgb(0x4A, 0xDE, 0x80);
    private static readonly Color White = Color.FromRgb(0xF1, 0xF5, 0xF9);

    // 四角圆环中心相对窗口中心 (150,170) 的偏移，收起时按该偏移平移回中央。
    private static readonly double[] CollapsedX = { 98, -98, 98, -98 };
    private static readonly double[] CollapsedY = { 84, 84, -84, -84 };

    private static readonly Grid[] Hosts = new Grid[4];
    private static readonly TranslateTransform[] Translates = new TranslateTransform[4];
    private static readonly ScaleTransform[] Scales = new ScaleTransform[4];

    private readonly ScaleTransform _cpuScale = new(1, 1);

    private readonly DispatcherTimer _rotateTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(2800)
    };

    private MetricSnapshot _latest = MetricSnapshot.Empty;
    private int _rotateKey;
    private bool _expanded;
    private bool _animating;

    public HudView()
    {
        InitializeComponent();

        CpuGauge.RenderTransform = _cpuScale;

        GpuGauge.Configure(6.5, Magenta, false, 17, 8.5, 7.5);
        VramGauge.Configure(6.5, Amber, false, 17, 8.5, 7.5);
        RamGauge.Configure(6.5, Green, false, 17, 8.5, 7.5);
        NetGauge.Configure(6.5, White, false, 17, 8.5, 7.5);

        Hosts[0] = GpuHost;
        Hosts[1] = VramHost;
        Hosts[2] = RamHost;
        Hosts[3] = NetHost;
        Translates[0] = GpuTranslate;
        Translates[1] = VramTranslate;
        Translates[2] = RamTranslate;
        Translates[3] = NetTranslate;
        Scales[0] = GpuScale;
        Scales[1] = VramScale;
        Scales[2] = RamScale;
        Scales[3] = NetScale;

        _rotateTimer.Tick += (_, _) => RotateNext();
    }

    public bool IsAnimating => _animating;

    // ---------- 状态 ----------

    public void Initialize(bool expanded)
    {
        if (expanded)
        {
            BeginExpanded();
            LinkCanvas.Opacity = 1;
            ResetHosts();
            _cpuScale.SetScale(1, 1);
            CpuGauge.Opacity = 1;
        }
        else
        {
            BeginCollapsed();
        }
    }

    /// <summary>展开：立即切换布局，然后播放四角展开动画。</summary>
    public void BeginExpanded()
    {
        _expanded = true;
        _rotateTimer.Stop();

        Canvas.SetLeft(CpuGauge, 75);
        Canvas.SetTop(CpuGauge, 95);
        CpuGauge.Width = 150;
        CpuGauge.Height = 150;
        CpuGauge.Configure(9, Cyan, true, 40, 13, 10.5);

        Update(_latest);
        LinkCanvas.Opacity = 0;
        ResetHosts();
    }

    public void AnimateExpand()
    {
        if (_animating)
            return;
        _animating = true;

        var sb = new Storyboard();
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        for (int i = 0; i < Hosts.Length; i++)
        {
            int begin = i * 45;
            Add(sb, Make(CollapsedX[i], 0, begin, 380, ease), Translates[i], TranslateTransform.XProperty);
            Add(sb, Make(CollapsedY[i], 0, begin, 380, ease), Translates[i], TranslateTransform.YProperty);
            Add(sb, Make(0.25, 1, begin, 380, ease), Scales[i], ScaleTransform.ScaleXProperty);
            Add(sb, Make(0.25, 1, begin, 380, ease), Scales[i], ScaleTransform.ScaleYProperty);
            Add(sb, Make(0, 1, begin, 320, ease), Hosts[i], UIElement.OpacityProperty);
        }

        Add(sb, Make(0, 1, 60, 260, ease), LinkCanvas, UIElement.OpacityProperty);
        Add(sb, Make(0.86, 1, 0, 240, ease), _cpuScale, ScaleTransform.ScaleXProperty);
        Add(sb, Make(0.86, 1, 0, 240, ease), _cpuScale, ScaleTransform.ScaleYProperty);
        Add(sb, Make(0.35, 1, 0, 220, ease), CpuGauge, UIElement.OpacityProperty);

        sb.Completed += (_, _) => _animating = false;
        sb.Begin();
    }

    /// <summary>收起：四角先向中央收拢，完成后回调由窗口切换尺寸。</summary>
    public void AnimateCollapse(Action finished)
    {
        if (_animating)
            return;
        _animating = true;

        var sb = new Storyboard();
        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };

        for (int i = 0; i < Hosts.Length; i++)
        {
            int begin = i * 30;
            Add(sb, Make(0, CollapsedX[i], begin, 300, ease), Translates[i], TranslateTransform.XProperty);
            Add(sb, Make(0, CollapsedY[i], begin, 300, ease), Translates[i], TranslateTransform.YProperty);
            Add(sb, Make(1, 0.25, begin, 300, ease), Scales[i], ScaleTransform.ScaleXProperty);
            Add(sb, Make(1, 0.25, begin, 300, ease), Scales[i], ScaleTransform.ScaleYProperty);
            Add(sb, Make(1, 0, begin, 260, ease), Hosts[i], UIElement.OpacityProperty);
        }

        Add(sb, Make(1, 0, 0, 220, ease), LinkCanvas, UIElement.OpacityProperty);
        Add(sb, Make(1, 0.55, 0, 220, ease), CpuGauge, UIElement.OpacityProperty);

        sb.Completed += (_, _) =>
        {
            _animating = false;
            finished();
        };
        sb.Begin();
    }

    public void BeginCollapsed()
    {
        _expanded = false;

        Canvas.SetLeft(CpuGauge, 3);
        Canvas.SetTop(CpuGauge, 3);
        CpuGauge.Width = 78;
        CpuGauge.Height = 78;
        CpuGauge.Configure(7, ColorFor(_rotateKey), false, 22, 9.5, 7.5);

        LinkCanvas.Opacity = 0;
        HideHosts();
        _cpuScale.SetScale(1, 1);
        CpuGauge.Opacity = 1;

        ApplyRotation(_latest);
        _rotateTimer.Start();
    }

    /// <summary>中央圆环的短暂弹性脉冲，用于掩盖尺寸切换。</summary>
    public void AnimatePop()
    {
        var sb = new Storyboard();
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        Add(sb, Make(0.85, 1, 0, 220, ease), _cpuScale, ScaleTransform.ScaleXProperty);
        Add(sb, Make(0.85, 1, 0, 220, ease), _cpuScale, ScaleTransform.ScaleYProperty);
        Add(sb, Make(0.35, 1, 0, 200, ease), CpuGauge, UIElement.OpacityProperty);
        sb.Begin();
    }

    // ---------- 数据 ----------

    public void Update(MetricSnapshot s)
    {
        _latest = s;

        if (s.GpuAvailable)
        {
            string temp = s.GpuTemp > 1 ? $"{s.GpuTemp:0}°C" : "RTX";
            GpuGauge.SetValue(s.GpuPercent, $"{s.GpuPercent:0}%", "GPU", temp);
        }
        else
        {
            GpuGauge.SetValue(0, "N/A", "GPU", "无独显");
        }

        VramGauge.SetValue(
            s.VramPercent,
            $"{s.VramPercent:0}%",
            "VRAM",
            s.VramTotalMb > 0 ? $"{s.VramUsedMb / 1024:0.0}/{s.VramTotalMb / 1024:0.0}GB" : "--");

        RamGauge.SetValue(
            s.MemPercent,
            $"{s.MemPercent:0}%",
            "RAM",
            $"{s.MemUsedGb:0.0}/{s.MemTotalGb:0.0}GB");

        double downMbps = s.NetDownBytesPerSec * 8.0 / 1_000_000.0;
        double upMbps = s.NetUpBytesPerSec * 8.0 / 1_000_000.0;
        double totalMbps = downMbps + upMbps;

        string value = totalMbps >= 100 ? $"{totalMbps:0}"
            : totalMbps >= 10 ? $"{totalMbps:0.0}"
            : $"{totalMbps:0.00}";

        // 对数刻度：几十 Kbps 到 1000 Mbps 都能看出变化。
        double percent = 100.0 * Math.Log10(1 + totalMbps) / Math.Log10(1001);
        NetGauge.SetValue(percent, value, "Mbps", $"↓{downMbps:0} ↑{upMbps:0}");

        if (!_expanded)
            UpdateRotatingValue();
    }

    private void RotateNext()
    {
        _rotateKey = (_rotateKey + 1) % 5;
        ApplyRotation(_latest);
        AnimatePop();
    }

    private void ApplyRotation(MetricSnapshot s)
    {
        CpuGauge.Configure(7, ColorFor(_rotateKey), false, 22, 9.5, 7.5);
        UpdateRotatingValue();
    }

    private void UpdateRotatingValue()
    {
        MetricSnapshot s = _latest;

        switch (_rotateKey)
        {
            case 0:
                CpuGauge.SetValue(s.CpuPercent, $"{s.CpuPercent:0}%", "CPU",
                    $"{Environment.ProcessorCount} 线程");
                break;

            case 1:
                CpuGauge.SetValue(
                    s.GpuAvailable ? s.GpuPercent : 0,
                    s.GpuAvailable ? $"{s.GpuPercent:0}%" : "N/A",
                    "GPU",
                    s.GpuAvailable
                        ? (s.GpuTemp > 1 ? $"{s.GpuTemp:0}°C" : "RTX")
                        : "无独显");
                break;

            case 2:
                CpuGauge.SetValue(
                    s.VramPercent,
                    $"{s.VramPercent:0}%",
                    "VRAM",
                    s.VramTotalMb > 0 ? $"{s.VramUsedMb / 1024:0.0}/{s.VramTotalMb / 1024:0.0}GB" : "--");
                break;

            case 3:
                CpuGauge.SetValue(
                    s.MemPercent,
                    $"{s.MemPercent:0}%",
                    "RAM",
                    $"{s.MemUsedGb:0.0}/{s.MemTotalGb:0.0}GB");
                break;

            default:
                double downMbps = s.NetDownBytesPerSec * 8.0 / 1_000_000.0;
                double upMbps = s.NetUpBytesPerSec * 8.0 / 1_000_000.0;
                double totalMbps = downMbps + upMbps;
                string value = totalMbps >= 100 ? $"{totalMbps:0}"
                    : totalMbps >= 10 ? $"{totalMbps:0.0}"
                    : $"{totalMbps:0.00}";
                double percent = 100.0 * Math.Log10(1 + totalMbps) / Math.Log10(1001);
                CpuGauge.SetValue(percent, value, "Mbps", $"↓{downMbps:0} ↑{upMbps:0}");
                break;
        }
    }

    private static Color ColorFor(int key) => key switch
    {
        0 => Cyan,
        1 => Magenta,
        2 => Amber,
        3 => Green,
        _ => White
    };

    // ---------- 辅助 ----------

    private void ResetHosts()
    {
        for (int i = 0; i < Hosts.Length; i++)
        {
            Hosts[i].Visibility = Visibility.Visible;
            Hosts[i].Opacity = 1;
            Translates[i].X = 0;
            Translates[i].Y = 0;
            Scales[i].SetScale(1, 1);
        }
    }

    private void HideHosts()
    {
        for (int i = 0; i < Hosts.Length; i++)
        {
            Hosts[i].Visibility = Visibility.Collapsed;
            Hosts[i].Opacity = 0;
            Translates[i].X = CollapsedX[i];
            Translates[i].Y = CollapsedY[i];
            Scales[i].SetScale(0.25, 0.25);
        }
    }

    private static void Add(Storyboard sb, DoubleAnimation animation, DependencyObject target,
        DependencyProperty property)
    {
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, new PropertyPath(property));
        sb.Children.Add(animation);
    }

    private static DoubleAnimation Make(double from, double to, double beginMs, double durationMs,
        IEasingFunction? ease = null)
    {
        var animation = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(durationMs))
        {
            BeginTime = TimeSpan.FromMilliseconds(beginMs),
            FillBehavior = FillBehavior.HoldEnd
        };

        if (ease != null)
            animation.EasingFunction = ease;

        Timeline.SetDesiredFrameRate(animation, 24);
        return animation;
    }
}

internal static class ScaleTransformExtensions
{
    public static void SetScale(this ScaleTransform transform, double x, double y)
    {
        transform.ScaleX = x;
        transform.ScaleY = y;
    }
}
