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

            RingDrawing.Draw(context, centre, radius, thickness, ring?.Percent, ring?.Tone ?? LimitTone.Unknown);

            if (ring is not null && dot is { } second)
            {
                context.DrawEllipse(
                    new SolidColorBrush(RingDrawing.ColourFor(second.Tone)), null, centre, 1.6 * scale, 1.6 * scale);
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
}
