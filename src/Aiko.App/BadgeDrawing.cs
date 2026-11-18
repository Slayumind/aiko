using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// The one sign of the sessions (D-409): the number of waiting sessions in orange, of stopped ones
/// in red, or a green dot for a finished turn. The tray draws it in the corner of its icon, the
/// taskbar button gets it as an overlay.
static class BadgeDrawing
{
    /// Dark text on the bright grounds, the same as the counts in the mockup.
    private static readonly Color Text = Color.FromRgb(0x17, 0x17, 0x17);

    /// A thin dark edge keeps the sign apart from the ring under it, on a light taskbar too.
    private static readonly Color Edge = Color.FromArgb(0xCC, 0x17, 0x17, 0x17);

    /// The face of the numbers. A narrow bold face reads at seven pixels; the mono font of the
    /// app does not.
    private static readonly Typeface Face = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

    /// The sign in the upper right corner of an icon of size pixels, where messengers put it and
    /// where Windows 11 draws the overlay of the taskbar button; in the 16 unit grid of the tray
    /// icon. Draw it after CutUnder has cleared the place for it.
    public static void DrawInCorner(DrawingContext context, double size, SessionBadge badge)
    {
        if (Shape(size, badge) is not { } shape)
        {
            return;
        }

        var ground = new SolidColorBrush(badge.Kind switch
        {
            BadgeKind.Waiting => RingDrawing.Caution,
            BadgeKind.Stopped => RingDrawing.Critical,
            _ => RingDrawing.Normal,
        });
        context.DrawGeometry(ground, null, shape);
        if (badge.Kind != BadgeKind.Done)
        {
            var text = Number(badge, 7.5 * size / 16.0);
            var box = shape.Bounds;
            context.DrawText(text, new Point(box.Left + ((box.Width - text.WidthIncludingTrailingWhitespace) / 2), box.Top + ((box.Height - text.Height) / 2)));
        }
    }

    /// Everything of the icon but the sign and a gap of one unit around it: the rings and the face
    /// are drawn inside it, so the sign never melts into the arc under it, on any taskbar. Null
    /// when there is no sign.
    public static Geometry? CutUnder(double size, SessionBadge badge)
    {
        if (Shape(size, badge) is not { } shape)
        {
            return null;
        }

        var gap = new Pen(Brushes.Black, 2 * size / 16.0);
        return Geometry.Combine(new RectangleGeometry(new Rect(0, 0, size, size)), shape.GetWidenedPathGeometry(gap), GeometryCombineMode.Exclude, null);
    }

    /// The outline of the sign in the corner: a dot for a finished turn, else a pill around the
    /// number.
    private static Geometry? Shape(double size, SessionBadge badge)
    {
        var scale = size / 16.0;
        switch (badge.Kind)
        {
            case BadgeKind.None:
                return null;
            case BadgeKind.Done:
                var radius = 2.8 * scale;
                return new EllipseGeometry(new Point(size - radius - (0.2 * scale), radius + (0.2 * scale)), radius, radius);
            default:
                var height = 9 * scale;
                var width = Math.Max(height, Number(badge, 7.5 * scale).WidthIncludingTrailingWhitespace + (3 * scale));
                return new RectangleGeometry(new Rect(size - width, 0, width, height), height / 2, height / 2);
        }
    }

    /// The sign alone, filling a square of size pixels: the overlay of the taskbar button. Null for
    /// no sign, which takes the overlay away.
    public static BitmapSource? Render(int size, SessionBadge badge)
    {
        if (badge.Kind == BadgeKind.None)
        {
            return null;
        }

        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            var scale = size / 16.0;
            if (badge.Kind == BadgeKind.Done)
            {
                var radius = 4.5 * scale;
                context.DrawEllipse(new SolidColorBrush(RingDrawing.Normal), new Pen(new SolidColorBrush(Edge), scale), new Point(size / 2.0, size / 2.0), radius, radius);
            }
            else
            {
                var text = Number(badge, 10 * scale);
                var box = new Rect(0, 0, size, size);
                box.Inflate(-0.5 * scale, -0.5 * scale);
                DrawPill(context, box, badge, scale);
                context.DrawText(text, new Point(box.Left + ((box.Width - text.WidthIncludingTrailingWhitespace) / 2), box.Top + ((box.Height - text.Height) / 2)));
            }
        }

        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private static void DrawPill(DrawingContext context, Rect box, SessionBadge badge, double edge)
    {
        var ground = badge.Kind == BadgeKind.Waiting ? RingDrawing.Caution : RingDrawing.Critical;
        var radius = box.Height / 2;
        context.DrawRoundedRectangle(new SolidColorBrush(ground), new Pen(new SolidColorBrush(Edge), edge), box, radius, radius);
    }

    private static FormattedText Number(SessionBadge badge, double size) =>
        new(badge.Text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Face, size, new SolidColorBrush(Text), 1.0);
}
