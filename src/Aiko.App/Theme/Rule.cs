using System.Windows.Controls;

namespace Aiko.App;

/// A thin line that divides two parts of a screen: across by default, down when asked.
internal sealed class Rule : PixelBorder
{
    public Rule()
    {
        Background = Tokens.Brush("Hairline");
        Height = 1;
    }

    public Orientation Orientation
    {
        get => double.IsNaN(Width) ? Orientation.Horizontal : Orientation.Vertical;
        set
        {
            Height = value == Orientation.Horizontal ? 1 : double.NaN;
            Width = value == Orientation.Vertical ? 1 : double.NaN;
        }
    }
}
