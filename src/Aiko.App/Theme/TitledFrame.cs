using System.Windows;
using System.Windows.Media;

namespace Aiko.App;

/// A thin frame with a title in a gap of its top line, and an optional second label at the right
/// end of that line. The terminal, the editor and the floating panels use it (D-437, D-441).
internal sealed class TitledFrame : PixelElement
{
    private const double TitleLeft = 14;
    private const double GapAroundLabel = 6;

    private readonly List<UIElement> _parts = [];
    private UIElement? _title;
    private UIElement? _aside;
    private UIElement? _content;

    public Thickness Padding { get; init; } = new(12, 8, 12, 10);

    public Brush Line { get; init; } = Tokens.Brush("Hairline");

    public UIElement? Title
    {
        get => _title;
        set => Replace(ref _title, value);
    }

    public UIElement? Aside
    {
        get => _aside;
        set => Replace(ref _aside, value);
    }

    public UIElement? Content
    {
        get => _content;
        set => Replace(ref _content, value);
    }

    /// Styles and resources of the window reach the content through this list.
    protected override System.Collections.IEnumerator LogicalChildren => _parts.GetEnumerator();

    protected override int VisualChildrenCount => _parts.Count;

    protected override Visual GetVisualChild(int index) => _parts[index];

    protected override Size MeasureOverride(Size availableSize)
    {
        var free = new Size(double.PositiveInfinity, double.PositiveInfinity);
        _aside?.Measure(free);
        var besideTitle = (2 * TitleLeft) + (_aside is null ? 0 : _aside.DesiredSize.Width + (2 * GapAroundLabel));
        _title?.Measure(new Size(Math.Max(0, availableSize.Width - besideTitle), double.PositiveInfinity));
        var top = LabelHeight();
        var sides = Padding.Left + Padding.Right + 2;
        var ends = top + Padding.Top + Padding.Bottom + 1;
        _content?.Measure(new Size(Math.Max(0, availableSize.Width - sides), Math.Max(0, availableSize.Height - ends)));
        var inside = _content?.DesiredSize ?? default;
        return new Size(inside.Width + sides, inside.Height + ends);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var top = LabelHeight();
        if (_title is not null)
        {
            _title.Arrange(new Rect(new Point(TitleLeft, 0), _title.DesiredSize));
        }

        if (_aside is not null)
        {
            var left = Math.Max(0, finalSize.Width - TitleLeft - _aside.DesiredSize.Width);
            _aside.Arrange(new Rect(new Point(left, 0), _aside.DesiredSize));
        }

        _content?.Arrange(new Rect(
            Padding.Left + 1,
            top + Padding.Top,
            Math.Max(0, finalSize.Width - Padding.Left - Padding.Right - 2),
            Math.Max(0, finalSize.Height - top - Padding.Top - Padding.Bottom - 1)));
        return finalSize;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var grid = Grid();
        var left = grid.SnapX(0);
        var right = grid.SnapX(ActualWidth) - grid.OnePixelX;
        var top = grid.SnapY(LabelHeight() / 2);
        var bottom = grid.SnapY(ActualHeight) - grid.OnePixelY;

        void Across(double from, double to, double y)
        {
            if (to > from)
            {
                FillOnPixels(drawingContext, Line, new Rect(from, y, to - from, grid.OnePixelY));
            }
        }

        void Down(double x) => FillOnPixels(drawingContext, Line, new Rect(x, top, grid.OnePixelX, bottom - top + grid.OnePixelY));

        var end = right + grid.OnePixelX;
        var from = left;
        if (_title is not null)
        {
            Across(from, grid.SnapX(TitleLeft - GapAroundLabel), top);
            from = grid.SnapX(TitleLeft + _title.DesiredSize.Width + GapAroundLabel);
        }

        if (_aside is not null)
        {
            var asideLeft = ActualWidth - TitleLeft - _aside.DesiredSize.Width;
            Across(from, grid.SnapX(asideLeft - GapAroundLabel), top);
            from = grid.SnapX(ActualWidth - TitleLeft + GapAroundLabel);
        }

        Across(from, end, top);
        Across(left, end, bottom);
        Down(left);
        Down(right);
    }

    private double LabelHeight() => Math.Max(_title?.DesiredSize.Height ?? 0, _aside?.DesiredSize.Height ?? 0);

    private void Replace(ref UIElement? slot, UIElement? value)
    {
        if (slot is not null)
        {
            RemoveVisualChild(slot);
            RemoveLogicalChild(slot);
            _parts.Remove(slot);
        }

        slot = value;
        if (value is not null)
        {
            _parts.Add(value);
            AddVisualChild(value);
            AddLogicalChild(value);
        }

        // The size of the frame may stay the same, and the gaps in its line must still follow.
        InvalidateMeasure();
        InvalidateVisual();
    }
}
