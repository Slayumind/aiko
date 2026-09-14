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

    /// Docked means pressed against the edge. In hand the island is a whole thing of its own: every
    /// corner rounded and a hairline all round, with its rings already laid out for the edge below.
    public void Show(IReadOnlyList<CardState> cards, ScreenEdge edge, bool docked = true)
    {
        // Along a top or bottom edge the rings stand in a row; along a side they stand in a
        // column, so the island takes as little of the edge as it can.
        var horizontal = IslandPlacement.IsHorizontal(edge);
        Rings.Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical;

        // Docked, the side against the screen has no corners and no line: it reads as part of the edge.
        Body.CornerRadius = !docked ? new CornerRadius(14) : edge switch
        {
            ScreenEdge.Top => new CornerRadius(0, 0, 14, 14),
            ScreenEdge.Bottom => new CornerRadius(14, 14, 0, 0),
            ScreenEdge.Left => new CornerRadius(0, 14, 14, 0),
            _ => new CornerRadius(14, 0, 0, 14),
        };
        Body.BorderThickness = !docked ? new Thickness(1) : edge switch
        {
            ScreenEdge.Top => new Thickness(1, 0, 1, 1),
            ScreenEdge.Bottom => new Thickness(1, 1, 1, 0),
            ScreenEdge.Left => new Thickness(0, 1, 1, 1),
            _ => new Thickness(1, 1, 0, 1),
        };

        // The missing line goes into the padding, so the rings do not move by a pixel on landing.
        var line = Body.BorderThickness;
        Body.Padding = new Thickness(11 - line.Left, 8 - line.Top, 11 - line.Right, 8 - line.Bottom);

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

        // Every element from the rings up to this panel is marked for measuring. Otherwise a measure
        // of the panel stops at the first element that did not change, and hands back the old size:
        // in hand, a row that turned into a column kept the width and height of the row.
        for (DependencyObject? element = Rings; element is not null && element != this; element = System.Windows.Media.VisualTreeHelper.GetParent(element))
        {
            (element as UIElement)?.InvalidateMeasure();
        }

        InvalidateMeasure();
    }

    private static Thickness Gap(bool horizontal, bool first) => (horizontal, first) switch
    {
        (_, true) => new Thickness(0),
        (true, false) => new Thickness(8, 0, 0, 0),
        _ => new Thickness(0, 8, 0, 0),
    };
}
