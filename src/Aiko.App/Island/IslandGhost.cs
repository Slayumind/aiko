using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Aiko.Core;

namespace Aiko.App;

/// The landing strip: a dashed outline on the edge where the island in hand will land (D-161).
///
/// A window of its own, because it sits on the edge while the island is somewhere else. It never
/// takes the focus or a click, and it exists only while the island is in hand.
sealed class IslandGhost : Window
{
    private const double Radius = 14;

    private readonly Path _outline = new()
    {
        Stroke = new SolidColorBrush(Color.FromArgb(0x80, 0xFA, 0xFA, 0xFA)),
        StrokeThickness = 1,
        StrokeDashArray = [4, 3],
    };

    private ScreenEdge? _edge;

    public IslandGhost()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Focusable = false;
        IsHitTestVisible = false;
        Opacity = 0;
        Content = _outline;
    }

    /// Moves the strip to where the island would land. A new edge glides there; sliding along the
    /// same edge follows the mouse at once, which is what keeps it feeling attached.
    public void PlaceOn(Box box, ScreenEdge edge)
    {
        var glide = _edge is not null && _edge != edge && Motion.IsOn;
        _edge = edge;

        _outline.Data = Geometry.Parse(Outline(edge, box.Width, box.Height));
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
            BeginAnimation(OpacityProperty, Motion.To(1, Motion.Hover, Motion.Standard));
        }
    }

    /// Three sides, dashed, with the inner corners rounded. The side on the edge is left out: the
    /// island will have no line there either.
    private static string Outline(ScreenEdge edge, double w, double h)
    {
        var r = Math.Min(Radius, Math.Min(w, h) / 2);
        const double i = 0.5;
        string N(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

        return edge switch
        {
            ScreenEdge.Top =>
                $"M {N(i)},0 L {N(i)},{N(h - r)} A {N(r)},{N(r)} 0 0 0 {N(r)},{N(h - i)} L {N(w - r)},{N(h - i)} A {N(r)},{N(r)} 0 0 0 {N(w - i)},{N(h - r)} L {N(w - i)},0",
            ScreenEdge.Bottom =>
                $"M {N(i)},{N(h)} L {N(i)},{N(r)} A {N(r)},{N(r)} 0 0 1 {N(r)},{N(i)} L {N(w - r)},{N(i)} A {N(r)},{N(r)} 0 0 1 {N(w - i)},{N(r)} L {N(w - i)},{N(h)}",
            ScreenEdge.Left =>
                $"M 0,{N(i)} L {N(w - r)},{N(i)} A {N(r)},{N(r)} 0 0 1 {N(w - i)},{N(r)} L {N(w - i)},{N(h - r)} A {N(r)},{N(r)} 0 0 1 {N(w - r)},{N(h - i)} L 0,{N(h - i)}",
            _ =>
                $"M {N(w)},{N(i)} L {N(r)},{N(i)} A {N(r)},{N(r)} 0 0 0 {N(i)},{N(r)} L {N(i)},{N(h - r)} A {N(r)},{N(r)} 0 0 0 {N(r)},{N(h - i)} L {N(w)},{N(h - i)}",
        };
    }
}
