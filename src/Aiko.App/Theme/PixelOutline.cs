using System.Windows;
using System.Windows.Media;

namespace Aiko.App;

/// A one pixel line around the element, on whole pixels at every zoom. Mouse clicks pass through it.
internal sealed class PixelOutline : PixelElement
{
    public PixelOutline() => IsHitTestVisible = false;

    public Brush? Line { get; init; }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var grid = Grid();
        var left = grid.SnapX(0);
        var top = grid.SnapY(0);
        var right = grid.SnapX(ActualWidth) - grid.OnePixelX;
        var bottom = grid.SnapY(ActualHeight) - grid.OnePixelY;
        var width = right - left + grid.OnePixelX;
        var height = bottom - top + grid.OnePixelY;

        FillOnPixels(drawingContext, Line, new Rect(left, top, width, grid.OnePixelY));
        FillOnPixels(drawingContext, Line, new Rect(left, bottom, width, grid.OnePixelY));
        FillOnPixels(drawingContext, Line, new Rect(left, top, grid.OnePixelX, height));
        FillOnPixels(drawingContext, Line, new Rect(right, top, grid.OnePixelX, height));
    }
}
