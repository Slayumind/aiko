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
    /// changes, the background slides there.
    public void Show(IReadOnlyList<FrameworkElement> pills, int selected, string chosen)
    {
        var moved = _chosen is not null && _chosen != chosen;
        _chosen = chosen;
        _selected = selected;
        _row.Children.Clear();
        foreach (var pill in pills)
        {
            _row.Children.Add(pill);
        }

        // The pills have their sizes only after layout.
        Root.Dispatcher.BeginInvoke(() => Place(selected, moved), DispatcherPriority.Loaded);
    }

    /// The header opened (hidden) or closed. The pills fade and fold their width, so the chevron
    /// beside them glides to the list icon and back instead of jumping.
    public void Hide(bool hidden, TimeSpan after = default)
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
        var fold = new DoubleAnimation(hidden ? 0 : full, Motion.Settle) { BeginTime = after, EasingFunction = Motion.Standard };
        if (!hidden)
        {
            // Back to its own width once open, so new numbers can widen it again.
            fold.Completed += (_, _) =>
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

        Root.BeginAnimation(FrameworkElement.WidthProperty, fold);
        Root.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(hidden ? 0 : 1, Fade) { BeginTime = after, EasingFunction = Motion.Standard });
        _rootShift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(hidden ? 0 : 4, hidden ? -4 : 0, Fade) { BeginTime = after, EasingFunction = Motion.Standard });
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
        var move = Motion.To(x, Motion.Settle, Motion.Standard);
        move.Completed += (_, _) => _sliding = false;
        _backShift.BeginAnimation(TranslateTransform.XProperty, move);
        _back.BeginAnimation(FrameworkElement.WidthProperty, Motion.To(width, Motion.Settle, Motion.Standard));
    }
}
