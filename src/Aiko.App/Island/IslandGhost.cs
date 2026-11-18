using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Aiko.Core;

namespace Aiko.App;

/// The landing strip: a pane of glass in the shape of the island, on the edge where the island in
/// hand will land (D-161, D-186, D-456).
///
/// Windows blurs only a whole window, and a window region switches that glass off, so the strip
/// makes its own. The first time it comes to an edge it takes a picture of that edge of the screen
/// without Aiko's island and strip, blurs it once, and shows the part under the strip. Following the
/// mouse only moves the window over that picture. The strip exists only while the island is in hand
/// and never takes the focus or a click.
sealed class IslandGhost : Window
{
    private const double CutSize = 8;

    /// How far from the edge the picture reaches, in layout units: more than any island is deep.
    private const double BandDepth = 160;

    /// The picture is blurred at a quarter of its size, which is also what makes it quick.
    private const double BlurScale = 0.25;
    private const int BlurRadius = 6;
    private const int BlurPasses = 2;

    private readonly Window? _island;
    private readonly ImageBrush _glass = new() { ViewboxUnits = BrushMappingMode.Absolute, Stretch = Stretch.Fill };
    private readonly Path _picture = new();
    private readonly Path _tint = new() { Fill = Tokens.Brush("GlassTint") };
    private readonly Dictionary<(ScreenEdge Edge, Box Work), Band> _bands = [];

    private ScreenEdge? _edge;
    private Band? _band;

    private sealed record Band(Box Area, BitmapSource Picture);

    /// island is left out of the picture, as the strip itself is.
    public IslandGhost(Window? island = null)
    {
        _island = island;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Focusable = false;
        IsHitTestVisible = false;
        _picture.Fill = _glass;
        var face = new System.Windows.Controls.Grid();
        face.Children.Add(_picture);
        face.Children.Add(_tint);
        Content = face;
        LocationChanged += (_, _) => ShowPartUnderStrip();
    }

    /// Raised the first time the strip shows. Both windows stay above everything, and the one shown
    /// last lies on top, so the island has to be lifted back over the glass.
    public event Action? Shown;

    /// Moves the strip to where the island would land. A new edge glides there; sliding along the
    /// same edge follows the mouse at once, which is what keeps it feeling attached.
    public void PlaceOn(Box box, ScreenEdge edge, Box work)
    {
        var glide = _edge is not null && _edge != edge && Motion.IsOn;
        _edge = edge;
        _band = BandOf(edge, work);
        _glass.ImageSource = _band.Picture;
        Width = box.Width;
        Height = box.Height;
        DrawShape(edge, box.Width, box.Height);

        if (glide)
        {
            BeginAnimation(LeftProperty, Motion.To(box.X, Motion.Expand, Motion.Standard));
            BeginAnimation(TopProperty, Motion.To(box.Y, Motion.Expand, Motion.Standard));
        }
        else
        {
            BeginAnimation(LeftProperty, null);
            BeginAnimation(TopProperty, null);
            Left = box.X;
            Top = box.Y;
        }

        ShowPartUnderStrip();
        if (!IsVisible)
        {
            Show();
            TaskbarOrder.PutUnder(this);
            Shown?.Invoke();
        }
    }

    private void ShowPartUnderStrip()
    {
        if (_band is { } band)
        {
            _glass.Viewbox = new Rect(Left - band.Area.X, Top - band.Area.Y, Width, Height);
        }
    }

    /// The outline of the landed island: the two corners against the screen stay whole.
    private void DrawShape(ScreenEdge edge, double width, double height)
    {
        var (top, right, bottom, left) = (edge != ScreenEdge.Top, edge != ScreenEdge.Right, edge != ScreenEdge.Bottom, edge != ScreenEdge.Left);
        var points = new List<Point>();
        void Corner(bool cut, Point first, Point sharp, Point second)
        {
            if (cut)
            {
                points.Add(first);
                points.Add(second);
            }
            else
            {
                points.Add(sharp);
            }
        }

        Corner(left && top, new Point(0, CutSize), new Point(0, 0), new Point(CutSize, 0));
        Corner(top && right, new Point(width - CutSize, 0), new Point(width, 0), new Point(width, CutSize));
        Corner(right && bottom, new Point(width, height - CutSize), new Point(width, height), new Point(width - CutSize, height));
        Corner(bottom && left, new Point(CutSize, height), new Point(0, height), new Point(0, height - CutSize));

        var figure = new PathFigure { StartPoint = points[0], IsClosed = true, IsFilled = true };
        figure.Segments.Add(new PolyLineSegment(points.Skip(1), false));
        var shape = new PathGeometry([figure]);
        _picture.Data = shape;
        _tint.Data = shape;
    }

    private Band BandOf(ScreenEdge edge, Box work)
    {
        if (_bands.TryGetValue((edge, work), out var known))
        {
            return known;
        }

        var area = edge switch
        {
            ScreenEdge.Top => work with { Height = BandDepth },
            ScreenEdge.Bottom => work with { Y = work.Y + work.Height - BandDepth, Height = BandDepth },
            ScreenEdge.Left => work with { Width = BandDepth },
            _ => work with { X = work.X + work.Width - BandDepth, Width = BandDepth },
        };
        var band = new Band(area, Blurred(TakePicture(area), area));
        _bands[(edge, work)] = band;
        return band;
    }

    /// The screen under the area, without the island and the strip.
    private BitmapSource TakePicture(Box area)
    {
        var scale = VisualTreeHelper.GetDpi(_island ?? this);
        var x = (int)Math.Round(area.X * scale.DpiScaleX);
        var y = (int)Math.Round(area.Y * scale.DpiScaleY);
        var width = Math.Max(1, (int)Math.Round(area.Width * scale.DpiScaleX));
        var height = Math.Max(1, (int)Math.Round(area.Height * scale.DpiScaleY));

        Window?[] own = [_island, this];
        var handles = own.Where(window => window is not null).Select(window => new WindowInteropHelper(window!).Handle).Where(handle => handle != 0).ToList();
        handles.ForEach(handle => SetWindowDisplayAffinity(handle, ExcludeFromCapture));
        var screen = GetDC(0);
        var memory = CreateCompatibleDC(screen);
        var bitmap = CreateCompatibleBitmap(screen, width, height);
        var before = SelectObject(memory, bitmap);
        try
        {
            BitBlt(memory, 0, 0, width, height, screen, x, y, SourceCopy);
            SelectObject(memory, before);
            return Imaging.CreateBitmapSourceFromHBitmap(bitmap, 0, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        }
        finally
        {
            handles.ForEach(handle => SetWindowDisplayAffinity(handle, NoAffinity));
            DeleteObject(bitmap);
            DeleteDC(memory);
            _ = ReleaseDC(0, screen);
        }
    }

    /// A blurred copy that measures exactly the area in layout units, so the brush can pick the part
    /// under the strip by its place on the screen.
    private static BitmapSource Blurred(BitmapSource picture, Box area)
    {
        var plain = new FormatConvertedBitmap(picture, PixelFormats.Bgr32, null, 0);
        var small = new TransformedBitmap(plain, new ScaleTransform(BlurScale, BlurScale));
        var (width, height) = (small.PixelWidth, small.PixelHeight);
        var pixels = new byte[width * height * 4];
        small.CopyPixels(pixels, width * 4, 0);
        for (var pass = 0; pass < BlurPasses; pass++)
        {
            pixels = BoxBlur(pixels, width, height, 4, width * 4);
            pixels = BoxBlur(pixels, height, width, width * 4, 4);
        }

        var result = BitmapSource.Create(width, height, width / area.Width * 96, height / area.Height * 96, PixelFormats.Bgr32, null, pixels, width * 4);
        result.Freeze();
        return result;
    }

    /// One pass of a box blur along lines of the given length. step goes along a line, nextLine to
    /// the same place of the next one.
    private static byte[] BoxBlur(byte[] source, int length, int lines, int step, int nextLine)
    {
        var target = new byte[source.Length];
        var window = (2 * BlurRadius) + 1;
        for (var line = 0; line < lines; line++)
        {
            var start = line * nextLine;
            for (var channel = 0; channel < 3; channel++)
            {
                int At(int i) => source[start + (Math.Clamp(i, 0, length - 1) * step) + channel];
                var sum = 0;
                for (var i = -BlurRadius; i <= BlurRadius; i++)
                {
                    sum += At(i);
                }

                for (var i = 0; i < length; i++)
                {
                    target[start + (i * step) + channel] = (byte)(sum / window);
                    sum += At(i + BlurRadius + 1) - At(i - BlurRadius);
                }
            }
        }

        return target;
    }

    private const uint NoAffinity = 0;
    private const uint ExcludeFromCapture = 0x11;
    private const int SourceCopy = 0x00CC0020;

#pragma warning disable SYSLIB1054
    [DllImport("user32.dll")]
    private static extern bool SetWindowDisplayAffinity(nint window, uint affinity);

    [DllImport("user32.dll")]
    private static extern nint GetDC(nint window);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(nint window, nint context);

    [DllImport("gdi32.dll")]
    private static extern nint CreateCompatibleDC(nint context);

    [DllImport("gdi32.dll")]
    private static extern nint CreateCompatibleBitmap(nint context, int width, int height);

    [DllImport("gdi32.dll")]
    private static extern nint SelectObject(nint context, nint item);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(nint target, int x, int y, int width, int height, nint source, int sourceX, int sourceY, int operation);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(nint item);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(nint context);
#pragma warning restore SYSLIB1054
}
