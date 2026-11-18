using System.Windows;
using System.Windows.Media;

namespace Aiko.App;

/// Where an element stands on the pixels of the screen. The window can be zoomed to 115 or 130%, so
/// a width of 1 in layout is not one pixel, and a normal border turns into a blurred line (D-447).
internal readonly record struct PixelGrid(double OriginX, double OriginY, double ScaleX, double ScaleY)
{
    public double OnePixelX => 1 / ScaleX;

    public double OnePixelY => 1 / ScaleY;

    public static PixelGrid Of(Visual visual)
    {
        var top = visual;
        while (VisualTreeHelper.GetParent(top) is Visual parent)
        {
            top = parent;
        }

        var dpi = VisualTreeHelper.GetDpi(visual);
        if (ReferenceEquals(top, visual))
        {
            return new PixelGrid(0, 0, dpi.DpiScaleX, dpi.DpiScaleY);
        }

        var toTop = visual.TransformToAncestor(top);
        var origin = toTop.Transform(new Point(0, 0));
        var right = toTop.Transform(new Point(1, 0));
        var down = toTop.Transform(new Point(0, 1));
        return new PixelGrid(
            origin.X * dpi.DpiScaleX,
            origin.Y * dpi.DpiScaleY,
            (right.X - origin.X) * dpi.DpiScaleX,
            (down.Y - origin.Y) * dpi.DpiScaleY);
    }

    /// The nearest place in layout units that falls exactly on a pixel.
    public double SnapX(double x) => (Math.Round(OriginX + (x * ScaleX)) - OriginX) / ScaleX;

    public double SnapY(double y) => (Math.Round(OriginY + (y * ScaleY)) - OriginY) / ScaleY;

    /// A guideline puts the first edge on a whole pixel, and the size keeps the second edge there.
    public static void Fill(DrawingContext context, Brush? brush, Rect shape)
    {
        context.PushGuidelineSet(new GuidelineSet([shape.Left], [shape.Top]));
        context.DrawRectangle(brush, null, shape);
        context.Pop();
    }
}

/// An element that draws straight lines on whole pixels. When layout moves it, it draws again,
/// because the old lines would no longer fall on the pixels.
internal abstract class PixelElement : FrameworkElement
{
    private PixelGrid _drawnOn;

    protected PixelElement()
    {
        SnapsToDevicePixels = false;
        LayoutUpdated += RedrawWhenMoved;
    }

    protected PixelGrid Grid()
    {
        _drawnOn = PixelGrid.Of(this);
        return _drawnOn;
    }

    protected static void FillOnPixels(DrawingContext context, Brush? brush, Rect shape) =>
        PixelGrid.Fill(context, brush, shape);

    private void RedrawWhenMoved(object? sender, EventArgs e)
    {
        if (PixelGrid.Of(this) != _drawnOn)
        {
            InvalidateVisual();
        }
    }
}
