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

    /// ring: the environment the icon shows. dot: the other one, or null with a single
    /// environment. Both null means Claude Code has not reported yet.
    public static HICON Render(int size, CardRow? ring, CardRow? dot, IconFrame? frame = null, ImageSource? face = null)
    {
        var png = RenderPng(size, ring, dot, frame, face);
        unsafe
        {
            fixed (byte* bytes = png)
            {
                return PInvoke.CreateIconFromResourceEx(
                    bytes, (uint)png.Length, true, PngIconVersion, size, size, IMAGE_FLAGS.LR_DEFAULTCOLOR);
            }
        }
    }

    /// frame: where the icon is between the rings and a face (D-211). Without one, only the rings.
    internal static byte[] RenderPng(int size, CardRow? ring, CardRow? dot, IconFrame? frame = null, ImageSource? face = null)
    {
        // Drawing happens in a 16 unit grid and is scaled to the real size, so 16, 24 and 32 px
        // icons keep the same proportions.
        var scale = size / 16.0;
        var now = frame ?? IconFrame.Rings;
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            var centre = new Point(8 * scale, 8 * scale);

            if (now.RingOpacity > 0)
            {
                context.PushTransform(new ScaleTransform(now.RingScale, now.RingScale, centre.X, centre.Y));
                context.PushOpacity(now.RingOpacity);
                DrawRings(context, centre, scale, ring, dot, now.RingSweep);
                context.Pop();
                context.Pop();
            }

            if (face is not null && now.ShowsFace)
            {
                context.PushTransform(new ScaleTransform(now.FaceScale, now.FaceScale, centre.X, centre.Y));
                context.PushOpacity(now.FaceOpacity);
                context.DrawImage(face, new Rect(0, 0, size, size));
                context.Pop();
                context.Pop();
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

    private static void DrawRings(DrawingContext context, Point centre, double scale, CardRow? ring, CardRow? dot, double sweep)
    {
        var radius = 5.8 * scale;
        var thickness = 2.2 * scale;

        // The arc grows back from zero when the rings return.
        int? percent = ring?.Percent is { } value ? (int)Math.Round(value * sweep) : null;
        RingDrawing.Draw(context, centre, radius, thickness, percent, ring?.Tone ?? LimitTone.Unknown);

        if (ring is not null && dot is { } second)
        {
            context.DrawEllipse(
                new SolidColorBrush(RingDrawing.ColourFor(second.Tone)), null, centre, 1.6 * scale, 1.6 * scale);
        }
    }
}
