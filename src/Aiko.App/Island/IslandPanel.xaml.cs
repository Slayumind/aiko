using System.Windows;
using System.Windows.Controls;
using Aiko.Core;

namespace Aiko.App;

/// The island when it is folded: one ring per environment and nothing else. No percentages on it —
/// the numbers are in the card, and the island has to stay out of the way.
///
/// It does carry the numbers in its tooltip. A ring and a colour say nothing to a screen reader,
/// and nothing at all to somebody who cannot tell the green from the red.
public partial class IslandPanel : UserControl
{
    private const double RingSize = 18;

    public IslandPanel()
    {
        InitializeComponent();
    }

    public void Show(IReadOnlyList<CardState> cards, ScreenEdge edge)
    {
        // Along a top or bottom edge the rings stand in a row; along a side they stand in a
        // column, so the island takes as little of the edge as it can.
        var horizontal = IslandPlacement.IsHorizontal(edge);
        Rings.Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical;

        // Only the corners facing into the screen are rounded: the others are pressed against it.
        Body.CornerRadius = edge switch
        {
            ScreenEdge.Top => new CornerRadius(0, 0, 14, 14),
            ScreenEdge.Bottom => new CornerRadius(14, 14, 0, 0),
            ScreenEdge.Left => new CornerRadius(0, 14, 14, 0),
            _ => new CornerRadius(14, 0, 0, 14),
        };

        ToolTip = TrayText.Tooltip(cards, null);

        Rings.Children.Clear();
        foreach (var card in cards)
        {
            var row = card.IconRow;
            var gauge = new RingGauge(row?.Percent, row?.Tone ?? LimitTone.Unknown, RingSize)
            {
                Margin = Gap(horizontal, Rings.Children.Count == 0),
            };
            Rings.Children.Add(gauge);
        }

        // With nothing set up at all the island still shows one dashed ring, so it is visible and
        // says "waiting" instead of disappearing.
        if (Rings.Children.Count == 0)
        {
            Rings.Children.Add(new RingGauge(null, LimitTone.Unknown, RingSize));
        }
    }

    private static Thickness Gap(bool horizontal, bool first) => (horizontal, first) switch
    {
        (_, true) => new Thickness(0),
        (true, false) => new Thickness(8, 0, 0, 0),
        _ => new Thickness(0, 8, 0, 0),
    };
}
