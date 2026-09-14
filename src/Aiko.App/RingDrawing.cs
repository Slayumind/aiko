using System.Windows;
using System.Windows.Media;
using Aiko.Core;

namespace Aiko.App;

/// How a ring is drawn, in one place.
///
/// The tray icon draws it into a PNG for Windows, the island draws it as part of a window. Two
/// copies of the same arc would slowly stop looking alike.
static class RingDrawing
{
    // The taskbar can be dark or light, and the icon is not told which. A see through grey works
    // on both: it settles a step away from whatever is behind it instead of fighting it.
    public static readonly Color TrackColor = Color.FromArgb(0x66, 0x9A, 0x9A, 0x9A);
    public static readonly Color NoDataColor = Color.FromArgb(0x8C, 0x9A, 0x9A, 0x9A);

    public static readonly Color Normal = Color.FromRgb(0x00, 0xBC, 0x7D);
    public static readonly Color Caution = Color.FromRgb(0xFE, 0x9A, 0x00);
    public static readonly Color Critical = Color.FromRgb(0xFF, 0x64, 0x67);
    public static readonly Color Muted = Color.FromRgb(0xA1, 0xA1, 0xA1);

    public static Color ColourFor(LimitTone tone) => tone switch
    {
        LimitTone.Normal => Normal,
        LimitTone.Caution => Caution,
        LimitTone.Critical => Critical,
        _ => Muted,
    };

    /// percent is null when nothing has been reported yet, and then the ring is dashed: a
    /// different state, not zero per cent.
    public static void Draw(
        DrawingContext context,
        Point centre,
        double radius,
        double thickness,
        int? percent,
        LimitTone tone)
    {
        if (percent is null)
        {
            DrawWaiting(context, centre, radius, thickness);
            return;
        }

        DrawTrack(context, centre, radius, thickness);
        DrawArc(context, centre, radius, thickness, percent.Value, tone);
    }

    private static void DrawTrack(DrawingContext context, Point centre, double radius, double thickness) =>
        context.DrawEllipse(null, new Pen(new SolidColorBrush(TrackColor), thickness), centre, radius, radius);

    private static void DrawWaiting(DrawingContext context, Point centre, double radius, double thickness)
    {
        // Five short dashes, worked out from the circle itself so they always close it evenly,
        // whatever the radius. Twelve looked right on paper but came out as a solid ring: the gaps
        // were thinner than the pen, and antialiasing filled them in. Six at 55% still read as
        // busy on the live tray, so the dashes are fewer and the gaps wider than the dashes.
        const int dashes = 5;
        var segment = 2 * Math.PI * radius / dashes;
        var dash = segment * 0.4;

        var pen = new Pen(new SolidColorBrush(NoDataColor), thickness)
        {
            DashCap = PenLineCap.Flat,
            // A dash pattern is measured in pen widths, not in pixels.
            DashStyle = new DashStyle([dash / thickness, (segment - dash) / thickness], 0),
        };
        context.DrawEllipse(null, pen, centre, radius, radius);
    }

    private static void DrawArc(
        DrawingContext context,
        Point centre,
        double radius,
        double thickness,
        int percent,
        LimitTone tone)
    {
        var share = Math.Clamp(percent / 100.0, 0, 1);
        if (share <= 0)
        {
            return;
        }

        var pen = new Pen(new SolidColorBrush(ColourFor(tone)), thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
        };

        if (share >= 1)
        {
            context.DrawEllipse(null, pen, centre, radius, radius);
            return;
        }

        // The arc starts at the top and grows clockwise, the way a clock fills up.
        var angle = share * 2 * Math.PI;
        var start = new Point(centre.X, centre.Y - radius);
        var end = new Point(
            centre.X + (radius * Math.Sin(angle)),
            centre.Y - (radius * Math.Cos(angle)));

        var figure = new PathFigure { StartPoint = start };
        figure.Segments.Add(new ArcSegment(end, new Size(radius, radius), 0, share > 0.5, SweepDirection.Clockwise, true));

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        context.DrawGeometry(null, pen, geometry);
    }
}
