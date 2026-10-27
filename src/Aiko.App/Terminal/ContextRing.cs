using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// How full the context of the conversation is, as a ring of 16 px: quiet, then orange, then red
/// (ContextGauge, D-342). The number is in its tip and in the panel "Tasks".
sealed class ContextRing : Grid
{
    private const double Size = 16;
    private const double Radius = 6;
    private const double Stroke = 2;

    private readonly Path _arc = new() { StrokeThickness = Stroke, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
    private int? _shown = -1;

    public ContextRing()
    {
        Width = Size;
        Height = Size;
        Visibility = Visibility.Collapsed;
        Children.Add(new Ellipse
        {
            Width = Radius * 2 + Stroke,
            Height = Radius * 2 + Stroke,
            Stroke = Tokens.Brush("Raised"),
            StrokeThickness = Stroke,
        });
        Children.Add(_arc);
    }

    public void Show(int? percent)
    {
        if (percent == _shown)
        {
            return;
        }

        _shown = percent;
        if (percent is not { } used)
        {
            Visibility = Visibility.Collapsed;
            return;
        }

        Visibility = Visibility.Visible;
        ToolTip = string.Format(CultureInfo.CurrentCulture, Strings.TerminalContextTip, used);
        _arc.Stroke = Tokens.Brush(ContextGauge.Level(used) switch
        {
            ContextLevel.Hot => "Destructive",
            ContextLevel.Warm => "Caution",
            _ => "Muted",
        });
        _arc.Data = Arc(used);
    }

    /// From the top, clockwise. A full ring is two halves: one arc cannot end where it starts.
    private static Geometry Arc(int percent)
    {
        const double centre = Size / 2;
        var share = Math.Clamp(percent, 1, 100) / 100.0;
        Point At(double part) => new(
            centre + Radius * Math.Sin(part * 2 * Math.PI),
            centre - Radius * Math.Cos(part * 2 * Math.PI));

        var figure = new PathFigure { StartPoint = At(0), IsClosed = false };
        if (share >= 1)
        {
            figure.Segments.Add(new ArcSegment(At(0.5), new Size(Radius, Radius), 0, false, SweepDirection.Clockwise, true));
            figure.Segments.Add(new ArcSegment(At(0.9999), new Size(Radius, Radius), 0, false, SweepDirection.Clockwise, true));
        }
        else
        {
            figure.Segments.Add(new ArcSegment(At(share), new Size(Radius, Radius), 0, share > 0.5, SweepDirection.Clockwise, true));
        }

        var geometry = new PathGeometry([figure]);
        geometry.Freeze();
        return geometry;
    }
}
