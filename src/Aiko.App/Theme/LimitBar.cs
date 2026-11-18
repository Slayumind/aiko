using System.Windows;
using System.Windows.Media;

namespace Aiko.App;

/// A bar of the new look: a tall fill that stands on a thin track, both on one bottom line (D-447).
internal sealed class LimitBar : PixelElement
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value),
        typeof(double),
        typeof(LimitBar),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill),
        typeof(Brush),
        typeof(LimitBar),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// How full the bar is, from 0 to 1.
    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public Brush? Fill
    {
        get => (Brush?)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public double TrackHeight { get; init; } = 5;

    public LimitBar()
    {
        Height = 12;
        Fill = Tokens.Brush("Ink");
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var grid = Grid();
        var left = grid.SnapX(0);
        var right = grid.SnapX(ActualWidth);
        var bottom = grid.SnapY(ActualHeight);
        var trackTop = grid.SnapY(ActualHeight - TrackHeight);
        var fillEnd = grid.SnapX(ActualWidth * Math.Clamp(Value, 0, 1));
        FillOnPixels(drawingContext, Tokens.Brush("LookTrack"), new Rect(left, trackTop, Math.Max(0, right - left), Math.Max(0, bottom - trackTop)));
        FillOnPixels(drawingContext, Fill, new Rect(left, grid.SnapY(0), Math.Max(0, fillEnd - left), Math.Max(0, bottom - grid.SnapY(0))));
    }
}
