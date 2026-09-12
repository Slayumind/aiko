using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Aiko.App;

/// Draws a piece of Aiko into a file. Every surface floats above the desktop, so nothing here is
/// ever seen in a window of its own: this is how it gets looked at after a change.
static class Snapshot
{
    public static void Write(FrameworkElement content, string path, int scale = 2)
    {
        // The panel background goes under it, otherwise the shadow would hang over nothing.
        var ground = new Border { Background = Tokens.Brush("Background"), Child = content };
        ground.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        ground.Arrange(new Rect(ground.DesiredSize));
        ground.UpdateLayout();

        // Twice the size, so the small numbers stay readable on the picture.
        var bitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(ground.DesiredSize.Width * scale),
            (int)Math.Ceiling(ground.DesiredSize.Height * scale),
            96 * scale,
            96 * scale,
            PixelFormats.Pbgra32);
        bitmap.Render(ground);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path);
        encoder.Save(file);
    }
}
