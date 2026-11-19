using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Aiko.App;

/// A Border whose fill and lines stand on whole pixels at every zoom. It lays out like any Border:
/// the same thickness, padding and child. Every frame and hairline of the window is one of these,
/// because a plain Border turns a thin line into a blurred one at 115 and 130% (D-447).
internal class PixelBorder : Border
{
    private PixelGrid _drawnOn;

    public PixelBorder()
    {
        SnapsToDevicePixels = false;
        PixelWatch.Follow(this, RedrawWhenMoved);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var grid = _drawnOn = PixelGrid.Of(this);
        var left = grid.SnapX(0);
        var top = grid.SnapY(0);
        var right = Math.Max(grid.SnapX(ActualWidth), left + grid.OnePixelX);
        var bottom = Math.Max(grid.SnapY(ActualHeight), top + grid.OnePixelY);

        var line = BorderThickness;
        var (lineLeft, lineRight) = (Across(line.Left, grid.ScaleX), Across(line.Right, grid.ScaleX));
        var (lineTop, lineBottom) = (Across(line.Top, grid.ScaleY), Across(line.Bottom, grid.ScaleY));
        var width = right - left;
        var height = bottom - top;

        if (Background is { } fill)
        {
            PixelGrid.Fill(drawingContext, fill, new Rect(
                left + lineLeft,
                top + lineTop,
                Math.Max(0, width - lineLeft - lineRight),
                Math.Max(0, height - lineTop - lineBottom)));
        }

        if (BorderBrush is not { } brush)
        {
            return;
        }

        void Side(double thickness, Rect shape)
        {
            if (thickness > 0)
            {
                PixelGrid.Fill(drawingContext, brush, shape);
            }
        }

        // The sides meet without overlapping, so a see-through line keeps one strength at the corners.
        Side(lineTop, new Rect(left, top, width, lineTop));
        Side(lineBottom, new Rect(left, bottom - lineBottom, width, lineBottom));
        Side(lineLeft, new Rect(left, top + lineTop, lineLeft, Math.Max(0, height - lineTop - lineBottom)));
        Side(lineRight, new Rect(right - lineRight, top + lineTop, lineRight, Math.Max(0, height - lineTop - lineBottom)));
    }

    /// A line is a whole number of pixels, at least one when it is there at all.
    private static double Across(double thickness, double scale) =>
        thickness <= 0 ? 0 : Math.Max(1, Math.Round(thickness * scale)) / scale;

    private void RedrawWhenMoved()
    {
        if (PixelGrid.Of(this) != _drawnOn)
        {
            InvalidateVisual();
        }
    }
}
