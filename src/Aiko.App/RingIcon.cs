using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Aiko.Core;
using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Aiko.App;

/// The tray icon: a ring for the chosen environment and a dot for the other one.
/// Both take their colour from the same thresholds as the card.
static class RingIcon
{
    private const uint PngIconVersion = 0x00030000;

    // The taskbar can be dark or light, and the icon is not told which. A see through grey works
    // on both: it settles a step away from whatever is behind it instead of fighting it.
    private static readonly Color Track = Color.FromArgb(0x66, 0x9A, 0x9A, 0x9A);
    private static readonly Color NoData = Color.FromArgb(0x8C, 0x9A, 0x9A, 0x9A);

    private static readonly Color Normal = Color.FromRgb(0x00, 0xBC, 0x7D);
    private static readonly Color Caution = Color.FromRgb(0xFE, 0x9A, 0x00);
    private static readonly Color Critical = Color.FromRgb(0xFF, 0x64, 0x67);
    private static readonly Color Muted = Color.FromRgb(0xA1, 0xA1, 0xA1);

    public static Color ColourFor(LimitTone tone) => tone switch
    {
        LimitTone.Normal => Normal,
        LimitTone.Caution => Caution,
        LimitTone.Critical => Critical,
        _ => Muted,
    };

    /// ring: the environment the icon shows. dot: the other one, or null with a single
    /// environment. Both null means Claude Code has not reported yet.
    public static HICON Render(int size, CardRow? ring, CardRow? dot)
    {
        var png = RenderPng(size, ring, dot);
        unsafe
        {
            fixed (byte* bytes = png)
            {
                return PInvoke.CreateIconFromResourceEx(
                    bytes, (uint)png.Length, true, PngIconVersion, size, size, IMAGE_FLAGS.LR_DEFAULTCOLOR);
            }
        }
    }

    internal static byte[] RenderPng(int size, CardRow? ring, CardRow? dot)
    {
        // Drawing happens in a 16 unit grid and is scaled to the real size, so 16, 24 and 32 px
        // icons keep the same proportions.
        var scale = size / 16.0;
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            var centre = new Point(8 * scale, 8 * scale);
            var radius = 5.8 * scale;
            var thickness = 2.2 * scale;

            if (ring is null)
            {
                DrawWaiting(context, centre, radius, thickness);
            }
            else
            {
                DrawTrack(context, centre, radius, thickness);
                DrawArc(context, centre, radius, thickness, ring.Value);
                if (dot is { } second)
                {
                    context.DrawEllipse(
                        new SolidColorBrush(ColourFor(second.Tone)), null, centre, 1.6 * scale, 1.6 * scale);
                }
            }
        }

        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private static void DrawTrack(DrawingContext context, Point centre, double radius, double thickness) =>
        context.DrawEllipse(null, new Pen(new SolidColorBrush(Track), thickness), centre, radius, radius);

    /// No data yet: a dashed ring, so it reads as a different state and not as zero per cent.
    private static void DrawWaiting(DrawingContext context, Point centre, double radius, double thickness)
    {
        // Six dashes, worked out from the circle itself so they always close it evenly, whatever
        // the radius. Twelve looked right on paper but came out as a solid ring: the gaps were
        // thinner than the pen, and antialiasing filled them in.
        const int dashes = 6;
        var segment = 2 * Math.PI * radius / dashes;
        var dash = segment * 0.55;

        var pen = new Pen(new SolidColorBrush(NoData), thickness)
        {
            DashCap = PenLineCap.Flat,
            // A dash pattern is measured in pen widths, not in pixels.
            DashStyle = new DashStyle([dash / thickness, (segment - dash) / thickness], 0),
        };
        context.DrawEllipse(null, pen, centre, radius, radius);
    }

    private static void DrawArc(DrawingContext context, Point centre, double radius, double thickness, CardRow row)
    {
        var share = Math.Clamp(row.Percent / 100.0, 0, 1);
        if (share <= 0)
        {
            return;
        }

        var brush = new SolidColorBrush(ColourFor(row.Tone));
        var pen = new Pen(brush, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };

        if (share >= 1)
        {
            context.DrawEllipse(null, pen, centre, radius, radius);
            return;
        }

        // The arc starts at the top and grows clockwise, the way a clock fills up.
        var angle = share * 2 * Math.PI;
        var start = new Point(centre.X, centre.Y - radius);
        var end = new Point(
            centre.X + radius * Math.Sin(angle),
            centre.Y - radius * Math.Cos(angle));

        var figure = new PathFigure { StartPoint = start };
        figure.Segments.Add(new ArcSegment(
            end,
            new Size(radius, radius),
            0,
            share > 0.5,
            SweepDirection.Clockwise,
            true));

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        context.DrawGeometry(null, pen, geometry);
    }
}
