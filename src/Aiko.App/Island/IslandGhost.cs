using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Aiko.Core;

namespace Aiko.App;

/// The landing strip: a pane of frosted glass on the edge where the island in hand will land (D-161,
/// D-182).
///
/// A window of its own, because it sits on the edge while the island is somewhere else. It never
/// takes the focus or a click, and it exists only while the island is in hand.
///
/// Windows gives a window without focus no glass that Aiko can tune (RESEARCH, glass spikes), so the
/// glass is Aiko's own: a frosted picture of the screen taken when the island was picked up, a thin
/// tint and grain over it, and a light rim. Being Aiko's own drawing, it can have the flat side
/// against the edge that the island has.
sealed class IslandGhost : Window
{
    private const double Radius = 14;

    /// White over the frosted picture: just enough to tell the pane from what is behind it.
    private static readonly Color Tint = Color.FromArgb(0x0A, 0xFF, 0xFF, 0xFF);

    /// The light edge that makes a pane read as glass, on the sides that face into the screen.
    private static readonly SolidColorBrush Rim = Frozen(new SolidColorBrush(Color.FromArgb(0x38, 0xFF, 0xFF, 0xFF)));

    /// Grain, the way frosted glass catches light: about two percent, as in Fluent acrylic.
    private static readonly ImageBrush Grain = MakeGrain();

    private readonly GlassBands _glass;
    private readonly Border _picture = new();
    private readonly Border _grain = new() { Background = Grain };
    private readonly Border _rim = new() { BorderBrush = Rim };
    private ScreenEdge? _edge;

    public IslandGhost(GlassBands glass)
    {
        _glass = glass;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Focusable = false;
        IsHitTestVisible = false;

        // The picture is a quarter of the screen's size; smooth scaling keeps the blur soft.
        RenderOptions.SetBitmapScalingMode(_picture, BitmapScalingMode.Linear);

        var layers = new Grid();
        layers.Children.Add(_picture);
        layers.Children.Add(_grain);
        layers.Children.Add(_rim);
        Content = layers;
    }

    /// Raised the first time the strip shows. Both windows stay above everything, and the one shown
    /// last lies on top, so the island has to be lifted back over the glass.
    public event Action? Shown;

    /// Moves the strip to where the island would land. A new edge glides there; sliding along the
    /// same edge follows the mouse at once, which is what keeps it feeling attached.
    public void PlaceOn(Box box, ScreenEdge edge)
    {
        var glide = _edge is not null && _edge != edge && Motion.IsOn;
        if (_edge != edge)
        {
            Shape(edge);
        }

        _edge = edge;
        _picture.Background = _glass.BrushFor(edge, box) ?? (Brush)new SolidColorBrush(Color.FromArgb(0x60, 0x17, 0x17, 0x17));
        Width = box.Width;
        Height = box.Height;

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

        if (!IsVisible)
        {
            Show();
            Shown?.Invoke();
        }
    }

    /// The same shape as the island that will land here: flat and without a rim on the edge side.
    private void Shape(ScreenEdge edge)
    {
        var corners = edge switch
        {
            ScreenEdge.Top => new CornerRadius(0, 0, Radius, Radius),
            ScreenEdge.Bottom => new CornerRadius(Radius, Radius, 0, 0),
            ScreenEdge.Left => new CornerRadius(0, Radius, Radius, 0),
            _ => new CornerRadius(Radius, 0, 0, Radius),
        };

        _picture.CornerRadius = corners;
        _grain.CornerRadius = corners;
        _rim.CornerRadius = corners;
        _rim.BorderThickness = edge switch
        {
            ScreenEdge.Top => new Thickness(1, 0, 1, 1),
            ScreenEdge.Bottom => new Thickness(1, 1, 1, 0),
            ScreenEdge.Left => new Thickness(0, 1, 1, 1),
            _ => new Thickness(1, 1, 0, 1),
        };
    }

    private static ImageBrush MakeGrain()
    {
        const int size = 96;
        var random = new Random(7);
        var pixels = new byte[size * size * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            // Premultiplied BGRA: the tint everywhere, and a light or a darker speck at a few percent.
            var speck = random.Next(0, 6);
            var alpha = (byte)(Tint.A + speck);
            var value = random.Next(3) != 0 ? alpha : (byte)(alpha / 3);
            pixels[i] = value;
            pixels[i + 1] = value;
            pixels[i + 2] = value;
            pixels[i + 3] = alpha;
        }

        var tile = BitmapSource.Create(size, size, 96, 96, PixelFormats.Pbgra32, null, pixels, size * 4);
        tile.Freeze();

        return Frozen(new ImageBrush(tile)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, size, size),
            ViewportUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.None,
        });
    }

    private static T Frozen<T>(T freezable)
        where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
