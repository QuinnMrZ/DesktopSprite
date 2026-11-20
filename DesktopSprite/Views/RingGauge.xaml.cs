using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DesktopSprite.Views;

/// <summary>环形仪表盘：外圈刻度（可选）+ 渐变进度弧 + 发光端点 + 中央数值。</summary>
public partial class RingGauge : UserControl
{
    private double _thickness = 8;
    private Color _color = Color.FromRgb(0x22, 0xD3, 0xEE);
    private bool _showTicks;
    private double _percent;

    private double _cx;
    private double _cy;
    private double _arcR;

    private Ellipse? _track;
    private Path? _ticks;
    private Path? _glow;
    private Path? _arc;
    private Ellipse? _dotHalo;
    private Ellipse? _dot;

    public RingGauge()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Rebuild();
        Loaded += (_, _) => Rebuild();
    }

    public void Configure(double thickness, Color color, bool showTicks,
        double valueFont, double labelFont, double subFont)
    {
        _thickness = thickness;
        _color = color;
        _showTicks = showTicks;

        ValueText.FontSize = valueFont;
        LabelText.FontSize = labelFont;
        SubText.FontSize = subFont;

        Rebuild();
    }

    public void SetValue(double percent, string value, string label, string sub)
    {
        ValueText.Text = value;
        LabelText.Text = label;
        SubText.Text = sub;

        _percent = Math.Clamp(percent, 0, 100);
        UpdateArc();
    }

    private void Rebuild()
    {
        if (ActualWidth < 8 || ActualHeight < 8)
            return;

        double size = Math.Min(ActualWidth, ActualHeight);
        _cx = ActualWidth / 2;
        _cy = ActualHeight / 2;
        _arcR = size / 2 - (_showTicks ? 20 : 8);

        if (_arcR <= 4)
            return;

        if (_track == null)
        {
            _track = new Ellipse { IsHitTestVisible = false };
            _ticks = new Path
            {
                Stroke = Frozen(0x55, Color.FromRgb(0x9A, 0xA6, 0xBF)),
                StrokeThickness = 1.2,
                IsHitTestVisible = false
            };
            _glow = new Path
            {
                Opacity = 0.20,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                IsHitTestVisible = false
            };
            _arc = new Path
            {
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                IsHitTestVisible = false
            };
            _dotHalo = new Ellipse { Width = 18, Height = 18, IsHitTestVisible = false };
            _dot = new Ellipse
            {
                Width = 6.5,
                Height = 6.5,
                Fill = Brushes.White,
                Opacity = 0.95,
                IsHitTestVisible = false
            };

            Layer.Children.Add(_track);
            Layer.Children.Add(_ticks);
            Layer.Children.Add(_glow);
            Layer.Children.Add(_arc);
            Layer.Children.Add(_dotHalo);
            Layer.Children.Add(_dot);
        }

        _track.Width = _arcR * 2;
        _track.Height = _arcR * 2;
        Canvas.SetLeft(_track, _cx - _arcR);
        Canvas.SetTop(_track, _cy - _arcR);
        _track.Stroke = Frozen(0x1E, Colors.White);
        _track.StrokeThickness = _thickness;

        Brush arcBrush = CreateArcBrush();
        _arc!.Stroke = arcBrush;
        _arc.StrokeThickness = _thickness;
        _glow!.Stroke = arcBrush;
        _glow.StrokeThickness = _thickness + 7;
        _dotHalo!.Fill = Frozen(0x38, _color);

        BuildTicks();
        UpdateArc();
    }

    private void BuildTicks()
    {
        if (_ticks == null)
            return;

        if (!_showTicks)
        {
            _ticks.Data = null;
            return;
        }

        const int count = 72;
        double inner = _arcR + _thickness / 2 + 3;

        var geometry = new StreamGeometry();
        using (StreamGeometryContext ctx = geometry.Open())
        {
            for (int i = 0; i < count; i++)
            {
                double angle = -90 + i * (360.0 / count);
                bool major = i % 6 == 0;

                ctx.BeginFigure(Polar(inner, angle), false, false);
                ctx.LineTo(Polar(inner + (major ? 6.5 : 3.2), angle), true, false);
            }
        }

        geometry.Freeze();
        _ticks.Data = geometry;
    }

    private void UpdateArc()
    {
        if (_arc == null || _glow == null || _dot == null || _dotHalo == null || _arcR <= 0)
            return;

        if (_percent < 0.5)
        {
            _arc.Data = null;
            _glow.Data = null;
            _dot.Visibility = Visibility.Collapsed;
            _dotHalo.Visibility = Visibility.Collapsed;
            return;
        }

        double sweep = _percent / 100.0 * 359.4;
        Geometry geometry = BuildArc(sweep);
        _arc.Data = geometry;
        _glow.Data = geometry;

        Point end = Polar(_arcR, -90 + sweep);
        _dot.Visibility = Visibility.Visible;
        _dotHalo.Visibility = Visibility.Visible;
        Canvas.SetLeft(_dot, end.X - _dot.Width / 2);
        Canvas.SetTop(_dot, end.Y - _dot.Height / 2);
        Canvas.SetLeft(_dotHalo, end.X - _dotHalo.Width / 2);
        Canvas.SetTop(_dotHalo, end.Y - _dotHalo.Height / 2);
    }

    private Geometry BuildArc(double sweepDegrees)
    {
        Point start = Polar(_arcR, -90);
        Point end = Polar(_arcR, -90 + sweepDegrees);

        var geometry = new StreamGeometry();
        using (StreamGeometryContext ctx = geometry.Open())
        {
            ctx.BeginFigure(start, false, false);
            ctx.ArcTo(end, new Size(_arcR, _arcR), 0,
                sweepDegrees > 180, SweepDirection.Clockwise, true, false);
        }

        geometry.Freeze();
        return geometry;
    }

    private Point Polar(double radius, double degrees)
    {
        double rad = degrees * Math.PI / 180.0;
        return new Point(_cx + radius * Math.Cos(rad), _cy + radius * Math.Sin(rad));
    }

    private Brush CreateArcBrush()
    {
        var brush = new LinearGradientBrush(
            _color,
            Lighten(_color, 0.45),
            new Point(0.1, 0.1),
            new Point(0.9, 0.9));
        brush.Freeze();
        return brush;
    }

    private static Color Lighten(Color color, double amount)
        => Color.FromRgb(
            (byte)(color.R + (255 - color.R) * amount),
            (byte)(color.G + (255 - color.G) * amount),
            (byte)(color.B + (255 - color.B) * amount));

    private static SolidColorBrush Frozen(byte alpha, Color color)
    {
        var brush = new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
        brush.Freeze();
        return brush;
    }
}
