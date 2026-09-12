using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Aiko.Core;

namespace Aiko.App;

/// Draws every tray icon state into one picture, so the icon can be looked at without digging it
/// out of the Windows overflow area. Started with --snapshot-icon.
static class IconSheet
{
    private const int Cell = 132;

    private static readonly Color Dark = Color.FromRgb(0x0A, 0x0A, 0x0A);
    private static readonly Color Light = Color.FromRgb(0xF2, 0xF2, 0xF2);

    public static void Write(string path)
    {
        (string Name, CardRow? Ring, CardRow? Dot)[] states =
        [
            ("no data", null, null),
            ("42 alone", Row(42, LimitTone.Normal), null),
            ("42 and 82", Row(42, LimitTone.Normal), Row(82, LimitTone.Caution)),
            ("82 and 95", Row(82, LimitTone.Caution), Row(95, LimitTone.Critical)),
            ("95 and stale", Row(95, LimitTone.Critical), Row(30, LimitTone.Unknown)),
            ("100 full", Row(100, LimitTone.Critical), Row(12, LimitTone.Normal)),
        ];

        // Two sizes, because Windows asks for a bigger icon on a scaled screen, and two grounds,
        // because the taskbar can be dark or light.
        (int Size, Color Ground)[] rows =
        [
            (16, Dark),
            (32, Dark),
            (16, Light),
            (32, Light),
        ];

        var visual = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.NearestNeighbor);
        using (var context = visual.RenderOpen())
        {
            for (var row = 0; row < rows.Length; row++)
            {
                var (size, ground) = rows[row];
                context.DrawRectangle(
                    new SolidColorBrush(ground),
                    null,
                    new Rect(0, row * Cell, states.Length * Cell, Cell));

                for (var column = 0; column < states.Length; column++)
                {
                    var (_, ring, dot) = states[column];
                    var image = Decode(RingIcon.RenderPng(size, ring, dot));
                    var drawn = size * (size == 16 ? 6 : 3);
                    context.DrawImage(image, new Rect(
                        column * Cell + (Cell - drawn) / 2.0,
                        row * Cell + (Cell - drawn) / 2.0,
                        drawn,
                        drawn));
                }
            }
        }

        var bitmap = new RenderTargetBitmap(states.Length * Cell, rows.Length * Cell, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path);
        encoder.Save(file);
    }

    private static CardRow Row(int percent, LimitTone tone) =>
        new(LimitKind.FiveHour, percent, tone, false, ResetCountdown.Reset, default);

    private static BitmapImage Decode(byte[] png)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.StreamSource = new MemoryStream(png);
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        return image;
    }
}
