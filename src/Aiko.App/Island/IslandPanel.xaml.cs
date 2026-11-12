using System.Windows;
using System.Windows.Controls;
using Aiko.Core;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// The island: one ring per environment. Folded it shows nothing else and stays out of the way.
/// Unfolded, on a hover or for a moment when a limit crosses a threshold, a percentage slides out
/// beside each ring (D-161), so the card is needed less often.
///
/// It does carry the numbers in its tooltip. A ring and a colour say nothing to a screen reader,
/// and nothing at all to somebody who cannot tell the green from the red.
public partial class IslandPanel : UserControl
{
    private const double RingSize = 18;

    private readonly List<UnfoldPanel> _labels = [];
    private bool _unfolded;

    /// Opens or closes the percentages. The window around the panel follows the size while it moves.
    public void Unfold(bool open, bool animate = true)
    {
        _unfolded = open;
        foreach (var label in _labels)
        {
            label.SetOpen(open, animate);
        }
    }

    public bool IsUnfolded => _unfolded;

    private readonly IslandFaceAnimator _faces;

    public IslandPanel()
    {
        InitializeComponent();
        _faces = new IslandFaceAnimator(Rings, Face, FaceFit, () => Rings.Children.OfType<Panel>().SelectMany(p => p.Children.OfType<RingGauge>())
            .Concat(Rings.Children.OfType<RingGauge>()));
    }

    public void ShowFace(System.Windows.Media.ImageSource face) => _faces.Show(face);

    public void HideFace() => _faces.Hide();

    /// For a snapshot: the face fully there, without the way to it.
    public void SetFace(IconFrame frame, System.Windows.Media.ImageSource? face) => _faces.Set(frame, face);

    /// Docked means pressed against the edge. In hand the island is a whole thing of its own: every
    /// corner rounded and a hairline all round, with its rings already laid out for the edge below.
    public void Show(IReadOnlyList<CardState> cards, ScreenEdge edge, bool docked = true)
    {
        // Along a top or bottom edge the rings stand in a row; along a side they stand in a
        // column, so the island takes as little of the edge as it can.
        var horizontal = IslandPlacement.IsHorizontal(edge);
        Rings.Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical;
        Strip.Orientation = Rings.Orientation;
        _horizontal = horizontal;
        DrawSessions();

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

        // Around the face the room is the same on every side: the island closes in on the side where
        // its padding is wider, and the face overlaps that padding.
        var across = Body.Padding.Left + Body.Padding.Right + line.Left + line.Right;
        var along = Body.Padding.Top + Body.Padding.Bottom + line.Top + line.Bottom;
        FaceFit.Target = new Size(
            Math.Max(0, RingSize - Math.Max(0, across - along)),
            Math.Max(0, RingSize - Math.Max(0, along - across)));

        ToolTip = TrayText.Tooltip(cards, null);

        Rings.Children.Clear();
        _labels.Clear();
        foreach (var card in cards)
        {
            var row = card.IconRow;
            var item = new StackPanel
            {
                Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical,
                Margin = Gap(horizontal, Rings.Children.Count == 0),
            };
            item.Children.Add(new RingGauge(row?.Percent, row?.Tone ?? LimitTone.Unknown, RingSize));
            item.Children.Add(Label(row?.Percent, horizontal));
            Rings.Children.Add(item);
        }

        // With nothing set up at all the island still shows one dashed ring, so it is visible and
        // says "waiting" instead of disappearing.
        if (Rings.Children.Count == 0)
        {
            Rings.Children.Add(new RingGauge(null, LimitTone.Unknown, RingSize));
        }

        _faces.Reapply();
        MarkForMeasure();
    }

    /// Every element from the rings and the percentages up to this panel is marked for measuring.
    /// Otherwise a measure of the panel stops at the first element that did not change, and hands
    /// back the old size: in hand, a row that turned into a column kept the size of the row, and an
    /// unfolding percentage would not have widened the window.
    public void MarkForMeasure()
    {
        foreach (var start in _labels.Cast<UIElement>().Append(Rings))
        {
            for (DependencyObject? element = start; element is not null && element != this; element = System.Windows.Media.VisualTreeHelper.GetParent(element))
            {
                (element as UIElement)?.InvalidateMeasure();
            }
        }

        InvalidateMeasure();
    }

    /// The percentage beside a ring along a top or bottom edge, under it along a side.
    private UnfoldPanel Label(int? percent, bool horizontal)
    {
        var words = new TextBlock
        {
            Text = percent is { } value ? $"{value}%" : "—",
            FontFamily = Tokens.Get<System.Windows.Media.FontFamily>("Mono"),
            FontSize = Tokens.Get<double>("TextTiny"),
            Foreground = Tokens.Brush("Ink"),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = horizontal ? new Thickness(6, 0, 0, 0) : new Thickness(0, 4, 0, 0),
        };

        var label = new UnfoldPanel
        {
            Child = words,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        label.SetOpen(_unfolded, animate: false);
        _labels.Add(label);
        return label;
    }

    private SessionCounts _sessions = SessionCounts.None;
    private bool _horizontal = true;
    private FrameworkElement? _waitingMark;

    /// The sessions after the rings (D-364, D-409). The window around the panel follows the size.
    public void ShowSessions(SessionCounts sessions)
    {
        _sessions = sessions;
        DrawSessions();
        MarkForMeasure();
    }

    /// The waiting session when the point, given by the mouse, is over the mark of "waits"; else null.
    public string? WaitingUnder(System.Windows.Input.MouseEventArgs e)
    {
        if (_waitingMark is not { IsVisible: true } mark || _sessions.FirstWaiting is not { } id)
        {
            return null;
        }

        var at = e.GetPosition(mark);
        return at.X >= -4 && at.Y >= -4 && at.X <= mark.ActualWidth + 4 && at.Y <= mark.ActualHeight + 4 ? id : null;
    }

    /// "Waits" with an orange pause, "done" with a green tick, "stopped" with a red cross and "work"
    /// with a grey dot, as in the list of sessions; each in a field of 12 with its number 4 units after
    /// it; 12 units after the rings, in a row along a top or bottom edge and in a column 6 apart along
    /// a side. With no session to count the island is as it was.
    private void DrawSessions()
    {
        Sessions.Children.Clear();
        _waitingMark = null;
        Sessions.Orientation = _horizontal ? Orientation.Horizontal : Orientation.Vertical;
        Sessions.Margin = _sessions.IsEmpty ? new Thickness(0) : _horizontal ? new Thickness(12, 0, 0, 0) : new Thickness(0, 12, 0, 0);
        if (_sessions.Waiting > 0)
        {
            _waitingMark = AddMark(Marks.Pause(), _sessions.Waiting);
            _waitingMark.Cursor = System.Windows.Input.Cursors.Hand;
        }

        if (_sessions.Done > 0)
        {
            AddMark(SiteIcons.Draw(SiteIcons.Check, 10, "Positive"), _sessions.Done);
        }

        if (_sessions.Stopped > 0)
        {
            AddMark(Marks.Line("M0,0 L8,8 M8,0 L0,8", "Destructive"), _sessions.Stopped);
        }

        if (_sessions.Working > 0)
        {
            AddMark(new System.Windows.Shapes.Ellipse { Width = 8, Height = 8, Fill = Tokens.Brush("Muted") }, _sessions.Working);
        }
    }

    private FrameworkElement AddMark(FrameworkElement sign, int count)
    {
        var mark = SessionMark(sign, count);
        if (Sessions.Children.Count > 0)
        {
            mark.Margin = _horizontal ? new Thickness(10, 0, 0, 0) : new Thickness(0, 6, 0, 0);
        }

        Sessions.Children.Add(mark);
        return mark;
    }

    private static FrameworkElement SessionMark(FrameworkElement sign, int count)
    {
        sign.HorizontalAlignment = HorizontalAlignment.Center;
        sign.VerticalAlignment = VerticalAlignment.Center;
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Background = System.Windows.Media.Brushes.Transparent };
        row.Children.Add(new Grid { Width = 12, Height = 12, Children = { sign } });
        row.Children.Add(new TextBlock
        {
            Text = count.ToString(System.Globalization.CultureInfo.CurrentCulture),
            FontFamily = Tokens.Get<System.Windows.Media.FontFamily>("Mono"),
            FontSize = Tokens.Get<double>("TextTiny"),
            Foreground = Tokens.Brush("Ink"),
            MinWidth = 7,
            Margin = new Thickness(4, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        });
        return row;
    }

    private static Thickness Gap(bool horizontal, bool first) => (horizontal, first) switch
    {
        (_, true) => new Thickness(0),
        (true, false) => new Thickness(8, 0, 0, 0),
        _ => new Thickness(0, 8, 0, 0),
    };
}
