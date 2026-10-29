using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Aiko.App;

/// The pills of the environments in the title bar (D-376), with one soft background that moves to
/// the chosen pill. They are the closed header: when the header opens, the pills fade away and its
/// cards take their place.
[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.17763")]
sealed class TerminalPills
{
    private const double PillHeight = 28;
    private static readonly Duration Fade = new(TimeSpan.FromMilliseconds(160));

    /// The background slides to the chosen pill, and the names change their colour, in this time.
    private static readonly Duration Slide = new(TimeSpan.FromMilliseconds(180));

    /// The names whose colour is still on its way, by environment. The pills are drawn again on
    /// every event; a new name takes the brush that is already moving, so the fade does not jump.
    private readonly Dictionary<string, SolidColorBrush> _fading = [];

    private readonly StackPanel _row = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
    private readonly TranslateTransform _backShift = new();
    private readonly Border _back;
    private readonly TranslateTransform _rootShift = new();
    private string? _chosen;
    private bool _hidden;
    private bool _sliding;

    public TerminalPills()
    {
        _back = new Border
        {
            Height = PillHeight,
            CornerRadius = new CornerRadius(PillHeight / 2),
            Background = Tokens.Brush("Raised"),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            RenderTransform = _backShift,
            Opacity = 0,
        };
        Root.Children.Add(_back);
        Root.Children.Add(_row);
        Root.RenderTransform = _rootShift;
        Root.ClipToBounds = true;

        // The numbers in the pills change their widths: the background follows, without a slide.
        _row.SizeChanged += (_, _) => Place(_selected, slide: false);
    }

    private int _selected = -1;

    public Grid Root { get; } = new() { VerticalAlignment = VerticalAlignment.Center };

    /// The pills, in the order of the environments. chosen is the name of the selected one; when it
    /// changes, the background slides there and the two names trade their colours.
    public void Show(IReadOnlyList<Pill> pills, int selected, string chosen)
    {
        var before = _chosen;
        var moved = before is not null && before != chosen;
        _chosen = chosen;
        _selected = selected;
        _row.Children.Clear();
        foreach (var pill in pills)
        {
            _row.Children.Add(pill.Element);
            if (pill.Label is not { } label)
            {
                continue;
            }

            if (moved && Motion.IsOn && (pill.Environment == chosen || pill.Environment == before))
            {
                Recolour(pill.Environment, label, pill.Environment == chosen ? "Muted" : "Ink");
            }
            else if (_fading.TryGetValue(pill.Environment, out var brush))
            {
                label.Foreground = brush;
            }
        }

        // The pills have their sizes only after layout.
        Root.Dispatcher.BeginInvoke(() => Place(selected, moved), DispatcherPriority.Loaded);
    }

    /// The header opened (hidden) or closed. The pills fade and fold their width. fold is the time
    /// of the header that opens or closes, so both move together.
    public void Hide(bool hidden, Duration fold, TimeSpan after = default)
    {
        if (hidden == _hidden)
        {
            return;
        }

        _hidden = hidden;
        Root.IsHitTestVisible = !hidden;
        if (!Motion.IsOn)
        {
            Root.BeginAnimation(UIElement.OpacityProperty, null);
            Root.BeginAnimation(FrameworkElement.WidthProperty, null);
            Root.Opacity = hidden ? 0 : 1;
            Root.Width = hidden ? 0 : double.NaN;
            return;
        }

        var full = _row.DesiredSize.Width;
        var folding = new DoubleAnimation(hidden ? 0 : full, fold) { BeginTime = after, EasingFunction = Motion.Standard };
        if (!hidden)
        {
            // Back to its own width once open, so new numbers can widen it again.
            folding.Completed += (_, _) =>
            {
                if (!_hidden)
                {
                    Root.BeginAnimation(FrameworkElement.WidthProperty, null);
                    Root.Width = double.NaN;
                }
            };
        }

        if (double.IsNaN(Root.Width))
        {
            Root.Width = Root.ActualWidth;
        }

        Root.BeginAnimation(FrameworkElement.WidthProperty, folding);
        Root.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(hidden ? 0 : 1, Fade) { BeginTime = after, EasingFunction = Motion.Standard });
        _rootShift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(hidden ? 0 : 4, hidden ? -4 : 0, Fade) { BeginTime = after, EasingFunction = Motion.Standard });
    }

    /// The name goes from the colour it had to the one its pill gives it now (Muted and Ink), on a
    /// brush of its own: the brushes of the palette are frozen and cannot change.
    private void Recolour(string environment, TextBlock label, string from)
    {
        var to = ((SolidColorBrush)label.Foreground).Color;
        var brush = new SolidColorBrush(((SolidColorBrush)Tokens.Brush(from)).Color);
        label.Foreground = brush;
        _fading[environment] = brush;
        var fade = new ColorAnimation(to, Slide) { EasingFunction = Motion.Standard };
        fade.Completed += (_, _) =>
        {
            if (_fading.TryGetValue(environment, out var current) && ReferenceEquals(current, brush))
            {
                _fading.Remove(environment);
            }
        };
        brush.BeginAnimation(SolidColorBrush.ColorProperty, fade);
    }

    private void Place(int selected, bool slide)
    {
        if (selected < 0 || selected >= _row.Children.Count || _row.Children[selected] is not FrameworkElement pill || pill.ActualWidth == 0)
        {
            return;
        }

        var x = pill.TranslatePoint(new Point(0, 0), Root).X;
        var width = pill.ActualWidth - pill.Margin.Right;
        if (!slide || !Motion.IsOn || _back.Opacity == 0)
        {
            // A redraw while the background slides lets the slide finish.
            if (_sliding)
            {
                return;
            }

            _backShift.BeginAnimation(TranslateTransform.XProperty, null);
            _back.BeginAnimation(FrameworkElement.WidthProperty, null);
            _backShift.X = x;
            _back.Width = width;
            _back.Opacity = 1;
            return;
        }

        _sliding = true;
        var move = Motion.To(x, Slide, Motion.Standard);
        move.Completed += (_, _) => _sliding = false;
        _backShift.BeginAnimation(TranslateTransform.XProperty, move);
        _back.BeginAnimation(FrameworkElement.WidthProperty, Motion.To(width, Slide, Motion.Standard));
    }
}

/// One pill for TerminalPills: the element, the name in it (null when a narrow window shows only
/// the ring), and its environment.
sealed record Pill(FrameworkElement Element, TextBlock? Label, string Environment);
