using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Aiko.Core;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// The marks of the states of a session, drawn the same in the list, the island and the notices.
static class Marks
{
    /// The width of the lines of the marks: as thin as the lines of the slayumind icon set, one unit
    /// in twenty-four, at the sizes they are drawn (D-332).
    public const double LineWidth = 0.75;

    /// The sign of "waits for you": two thin bars, drawn as thin as the lines of the slayumind icons
    /// (one in twenty-four), so they read as a quiet mark and not as a pause button (D-299, D-332).
    public static FrameworkElement Pause() => Line("M0.5,0.5 L0.5,9.5 M4.5,0.5 L4.5,9.5", "Caution");

    /// The sound of a question of the agent: a speaker with two waves, in the thin lines of the
    /// other marks. The icon set has no speaker (D-396).
    public static FrameworkElement Sound() =>
        Line("M0.5,3.5 L2.5,3.5 L5,1 L5,9 L2.5,6.5 L0.5,6.5 Z M7,3.3 Q8.1,5 7,6.7 M8.6,1.8 Q10.6,5 8.6,8.2", "Ink");

    /// A mark of thin round lines in a colour of the palette.
    public static Path Line(string data, string tone = "Muted") => new()
    {
        Data = Geometry.Parse(data),
        Stroke = Tokens.Brush(tone),
        StrokeThickness = LineWidth,
        StrokeStartLineCap = PenLineCap.Round,
        StrokeEndLineCap = PenLineCap.Round,
        StrokeLineJoin = PenLineJoin.Round,
        VerticalAlignment = VerticalAlignment.Center,
    };

    /// The track of a ring: the grey of the tray icon's ring. It is see-through, so it reads on the
    /// window, on a card and on the background of the chosen pill alike.
    private static readonly Brush RingTrack = TrackBrush();

    private static Brush TrackBrush()
    {
        var brush = new SolidColorBrush(RingDrawing.TrackColor);
        brush.Freeze();
        return brush;
    }

    /// The ring of a limit: a full grey track and the used part from the top, clockwise.
    public static FrameworkElement Ring(int? percent, LimitTone tone, double size, double thickness, double opacity = 1)
    {
        var ring = new Grid { Width = size, Height = size, VerticalAlignment = VerticalAlignment.Center, Opacity = opacity };
        ring.Children.Add(new Ellipse { Stroke = RingTrack, StrokeThickness = thickness });
        if (percent is { } used && used > 0)
        {
            ring.Children.Add(new Path
            {
                Stroke = Tokens.ToneBrush(tone),
                StrokeThickness = thickness,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Data = Arc(size / 2, (size - thickness) / 2, used / 100.0),
            });
        }

        return ring;
    }

    /// A part of a circle from the top, clockwise.
    public static Geometry Arc(double centre, double radius, double part)
    {
        part = Math.Clamp(part, 0, 0.999);
        var angle = part * 2 * Math.PI;
        var end = new Point(centre + radius * Math.Sin(angle), centre - radius * Math.Cos(angle));
        var figure = new PathFigure { StartPoint = new Point(centre, centre - radius) };
        figure.Segments.Add(new ArcSegment(end, new Size(radius, radius), 0, part > 0.5, SweepDirection.Clockwise, true));
        return new PathGeometry([figure]);
    }

    /// A mark in the square of the notice marks.
    public static FrameworkElement Fit(FrameworkElement mark)
    {
        mark.HorizontalAlignment = HorizontalAlignment.Center;
        mark.VerticalAlignment = VerticalAlignment.Center;
        mark.LayoutTransform = new ScaleTransform(1.25, 1.25);
        return new Grid { Width = 18, Height = 18, VerticalAlignment = VerticalAlignment.Center, Children = { mark } };
    }
}
