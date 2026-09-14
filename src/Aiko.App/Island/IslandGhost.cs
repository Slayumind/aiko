using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Aiko.Core;

namespace Aiko.App;

/// The landing strip: a pane of liquid glass on the edge where the island in hand will land (D-161,
/// D-183).
///
/// A window of its own, because it sits on the edge while the island is somewhere else. It never
/// takes the focus or a click, and it exists only while the island is in hand.
///
/// The glass follows the owner's reference, the weather cards made of ui-layouts liquid glass, layer
/// by layer: the picture behind it blurred and bent (GlassBands), white at 8 % over it, a hairline
/// lit from inside and a shadow so faint it is felt more than seen (D-185). Being Aiko's own
/// drawing, it can have the flat side against the screen edge that the island has.
sealed class IslandGhost : Window
{
    /// borderRadius 8px, as in the reference, where the island has 14.
    private const double Radius = 8;

    /// How much of the glass itself shows: the rest lets the real, unblurred screen through. The
    /// rim stays at full strength. 0.7 after the owner asked for glass 20 % and then 10 % more clearer.
    private const double GlassOpacity = 0.7;

    /// Room around the pane for the shadow to spread into.
    private const double Spread = 16;

    private readonly GlassBands _glass;
    /// bg-white/8.
    private readonly Border _tint = new()
    {
        Background = new SolidColorBrush(Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF)),
    };

    private readonly Border _pane = new()
    {
        // glowIntensity none: box-shadow 0 4px 4px rgba(0,0,0,.05), 0 0 12px rgba(0,0,0,.05), made one.
        Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 10, ShadowDepth = 2, Direction = 270, Opacity = 0.1 },
    };

    private readonly Border _edgeLight = new()
    {
        // shadowIntensity xs: inset 1px 1px 1px rgba(255,255,255,.3) and the same from the other side.
        BorderBrush = new SolidColorBrush(Color.FromArgb(0x4D, 0xFF, 0xFF, 0xFF)),
        Effect = new BlurEffect { Radius = 1 },
    };

    private readonly Grid _layers = new();
    private ScreenEdge? _edge;
    private Size _size;

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
        RenderOptions.SetBitmapScalingMode(_pane, BitmapScalingMode.Linear);
        _pane.Opacity = GlassOpacity;
        _tint.Opacity = GlassOpacity;

        _layers.Children.Add(_pane);
        _layers.Children.Add(_tint);
        _layers.Children.Add(_edgeLight);
        Content = _layers;
    }

    /// Raised the first time the strip shows. Both windows stay above everything, and the one shown
    /// last lies on top, so the island has to be lifted back over the glass.
    public event Action? Shown;

    /// Moves the strip to where the island would land. A new edge glides there; sliding along the
    /// same edge follows the mouse at once, which is what keeps it feeling attached.
    public void PlaceOn(Box box, ScreenEdge edge)
    {
        var glide = _edge is not null && _edge != edge && Motion.IsOn;
        if (_edge != edge || _size != new Size(box.Width, box.Height))
        {
            Shape(edge, box.Width, box.Height);
        }

        _edge = edge;
        _pane.Background = _glass.BrushFor(edge, box) ?? (Brush)new SolidColorBrush(Color.FromArgb(0x60, 0x17, 0x17, 0x17));

        // The window reaches past the pane on the sides that face into the screen, for the shadow.
        var room = Room(edge);
        var window = new Box(box.X - room.Left, box.Y - room.Top, box.Width + room.Left + room.Right, box.Height + room.Top + room.Bottom);
        Width = window.Width;
        Height = window.Height;

        if (glide)
        {
            BeginAnimation(LeftProperty, Motion.To(window.X, Motion.Expand, Motion.Standard));
            BeginAnimation(TopProperty, Motion.To(window.Y, Motion.Expand, Motion.Standard));
        }
        else
        {
            BeginAnimation(LeftProperty, null);
            BeginAnimation(TopProperty, null);
            Left = window.X;
            Top = window.Y;
        }

        if (!IsVisible)
        {
            Show();
            Shown?.Invoke();
        }
    }

    private static Thickness Room(ScreenEdge edge) => edge switch
    {
        ScreenEdge.Top => new Thickness(Spread, 0, Spread, Spread),
        ScreenEdge.Bottom => new Thickness(Spread, Spread, Spread, 0),
        ScreenEdge.Left => new Thickness(0, Spread, Spread, Spread),
        _ => new Thickness(Spread, Spread, 0, Spread),
    };

    /// The same shape as the island that will land here: flat and unlit on the edge side.
    private void Shape(ScreenEdge edge, double width, double height)
    {
        _size = new Size(width, height);
        var corners = edge switch
        {
            ScreenEdge.Top => new CornerRadius(0, 0, Radius, Radius),
            ScreenEdge.Bottom => new CornerRadius(Radius, Radius, 0, 0),
            ScreenEdge.Left => new CornerRadius(0, Radius, Radius, 0),
            _ => new CornerRadius(Radius, 0, 0, Radius),
        };

        var room = Room(edge);
        foreach (var layer in new[] { _pane, _tint, _edgeLight })
        {
            layer.CornerRadius = corners;
            layer.Margin = room;
            layer.Width = width;
            layer.Height = height;
        }

        _edgeLight.BorderThickness = edge switch
        {
            ScreenEdge.Top => new Thickness(1, 0, 1, 1),
            ScreenEdge.Bottom => new Thickness(1, 1, 1, 0),
            ScreenEdge.Left => new Thickness(0, 1, 1, 1),
            _ => new Thickness(1, 1, 0, 1),
        };

        // Cut to the pane, so the blurred rim glows inwards only, like an inset shadow.
        _edgeLight.Clip = Outline(width, height, corners);
    }

    private static Geometry Outline(double width, double height, CornerRadius corners)
    {
        var outline = new StreamGeometry();
        using (var draw = outline.Open())
        {
            draw.BeginFigure(new Point(corners.TopLeft, 0), isFilled: true, isClosed: true);
            draw.LineTo(new Point(width - corners.TopRight, 0), true, false);
            draw.ArcTo(new Point(width, corners.TopRight), new Size(corners.TopRight, corners.TopRight), 0, false, SweepDirection.Clockwise, true, false);
            draw.LineTo(new Point(width, height - corners.BottomRight), true, false);
            draw.ArcTo(new Point(width - corners.BottomRight, height), new Size(corners.BottomRight, corners.BottomRight), 0, false, SweepDirection.Clockwise, true, false);
            draw.LineTo(new Point(corners.BottomLeft, height), true, false);
            draw.ArcTo(new Point(0, height - corners.BottomLeft), new Size(corners.BottomLeft, corners.BottomLeft), 0, false, SweepDirection.Clockwise, true, false);
            draw.LineTo(new Point(0, corners.TopLeft), true, false);
            draw.ArcTo(new Point(corners.TopLeft, 0), new Size(corners.TopLeft, corners.TopLeft), 0, false, SweepDirection.Clockwise, true, false);
        }

        outline.Freeze();
        return outline;
    }
}
