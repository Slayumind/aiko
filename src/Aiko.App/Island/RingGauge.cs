using System.Windows;
using System.Windows.Media;
using Aiko.Core;

namespace Aiko.App;

/// One ring of the island. The tray has to squeeze two environments into 16 pixels and uses a
/// ring and a dot; the island has room, so every environment gets a ring of its own.
sealed class RingGauge : FrameworkElement
{
    /// How much of the arc is drawn, from 0 to 1. The rings grow their arcs back after a face.
    public static readonly DependencyProperty SweepProperty = DependencyProperty.Register(
        nameof(Sweep), typeof(double), typeof(RingGauge), new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

    private readonly int? _percent;
    private readonly LimitTone _tone;

    public RingGauge(int? percent, LimitTone tone, double size)
    {
        _percent = percent;
        _tone = tone;
        Width = size;
        Height = size;
    }

    public double Sweep
    {
        get => (double)GetValue(SweepProperty);
        set => SetValue(SweepProperty, value);
    }

    protected override void OnRender(DrawingContext context)
    {
        var size = Math.Min(ActualWidth, ActualHeight);
        var thickness = size * 0.18;
        var radius = (size - thickness) / 2;

        int? percent = _percent is { } value ? (int)Math.Round(value * Sweep) : null;
        RingDrawing.Draw(context, new Point(size / 2, size / 2), radius, thickness, percent, _tone);
    }
}
