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

    /// Every face as the tray draws it, on both taskbars, and one whole transition frame by frame:
    /// rings out, face in, face out, rings back. Started with --snapshot-icon out.png faces.
    public static void WriteFaces(string path)
    {
        const int timelineCell = 72;
        var ring = Row(42, LimitTone.Normal);
        var dot = Row(82, LimitTone.Caution);
        var faces = Enum.GetValues<AikoFace>();
        var styles = Enum.GetValues<FaceStyle>();
        (int Size, Color Ground, FaceGround Face)[] rows =
        [
            (16, Dark, FaceGround.Dark),
            (32, Dark, FaceGround.Dark),
            (16, Light, FaceGround.Light),
            (32, Light, FaceGround.Light),
        ];

        var timeline = Enum.GetValues<FacePhase>().SelectMany(TrayFaceMotion.Frames).ToList();
        var width = Math.Max(styles.Length * faces.Length * Cell, timeline.Count * timelineCell);
        var height = (rows.Length * Cell) + timelineCell;

        var visual = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.NearestNeighbor);
        using (var context = visual.RenderOpen())
        {
            for (var row = 0; row < rows.Length; row++)
            {
                var (size, ground, faceGround) = rows[row];
                context.DrawRectangle(new SolidColorBrush(ground), null, new Rect(0, row * Cell, width, Cell));

                var column = 0;
                foreach (var style in styles)
                {
                    foreach (var face in faces)
                    {
                        var png = RingIcon.RenderPng(size, ring, dot, TrayFaceMotion.At(FacePhase.FaceIn, 1), FaceDrawing.For(style, face, faceGround, size));
                        Place(context, png, size, column++ * Cell, row * Cell, Cell);
                    }
                }
            }

            var top = rows.Length * Cell;
            context.DrawRectangle(new SolidColorBrush(Dark), null, new Rect(0, top, width, timelineCell));
            var doneFace = FaceDrawing.For(FaceStyle.Chibi, AikoFace.Done, FaceGround.Dark, 32);
            for (var i = 0; i < timeline.Count; i++)
            {
                Place(context, RingIcon.RenderPng(32, ring, dot, timeline[i], doneFace), 32, i * timelineCell, top, timelineCell);
            }
        }

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path);
        encoder.Save(file);
    }

    private static void Place(DrawingContext context, byte[] png, int size, double left, double top, int cell)
    {
        var drawn = Math.Min(size * (size == 16 ? 6 : 3), cell - 8);
        context.DrawImage(Decode(png), new Rect(left + ((cell - drawn) / 2.0), top + ((cell - drawn) / 2.0), drawn, drawn));
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
