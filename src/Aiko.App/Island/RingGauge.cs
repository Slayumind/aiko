using System.Windows;
using System.Windows.Media;
using Aiko.Core;

namespace Aiko.App;

/// One ring of the island. The tray has to squeeze two environments into 16 pixels and uses a
/// ring and a dot; the island has room, so every environment gets a ring of its own.
sealed class RingGauge : FrameworkElement
{
    private readonly int? _percent;
    private readonly LimitTone _tone;

    public RingGauge(int? percent, LimitTone tone, double size)
    {
        _percent = percent;
        _tone = tone;
        Width = size;
        Height = size;
    }

    protected override void OnRender(DrawingContext context)
    {
        var size = Math.Min(ActualWidth, ActualHeight);
        var thickness = size * 0.18;
        var radius = (size - thickness) / 2;

        RingDrawing.Draw(context, new Point(size / 2, size / 2), radius, thickness, _percent, _tone);
    }
}
