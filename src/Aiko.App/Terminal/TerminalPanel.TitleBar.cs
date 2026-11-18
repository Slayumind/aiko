using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Aiko.Core;
using Aiko.Core.Terminal;
using static Aiko.App.Widgets;

namespace Aiko.App;

/// The title bar of the window: the list button, the header, the search, the settings and the window buttons.
sealed partial class TerminalPanel
{
    /// The room of the pills: the left column without the two buttons before them.
    private void FitPills()
    {
        var before = _listToggle.ActualWidth + _listToggle.Margin.Left + _listToggle.Margin.Right
            + _chevron.ActualWidth + _chevron.Margin.Left + _chevron.Margin.Right;
        _pills.Fit(_bar.ActualWidth - _right.ActualWidth - before);
    }

    private void ShowBar(TerminalView view)
    {
        var selected = view.Tabs.ToList().FindIndex(tab => tab.Selected);
        _pills.Show([.. view.Tabs.Select(Tab)], selected, view.Environment);

        _right.Children.Clear();
        _right.Children.Add(_search);
        _right.Children.Add(_gear);
        foreach (var button in Buttons)
        {
            _right.Children.Add(button);
        }

        _listHidden = view.Header.ListHidden;
        _chevron.ToolTip = view.Header.Open ? Strings.TerminalHideLimits : Strings.TerminalShowLimits;
        // Only the mark turns: the highlight of the button stays where it is.
        _chevronMark.RenderTransformOrigin = new Point(0.5, 0.5);
        _chevronMark.RenderTransform = _chevronTurn;
        var turn = view.Header.Open ? 180 : 0;
        if (!Motion.IsOn)
        {
            _chevronTurn.BeginAnimation(RotateTransform.AngleProperty, null);
            _chevronTurn.Angle = turn;
        }
        else if (_chevronTurn.Angle != turn)
        {
            // In step with the header that opens or folds.
            _chevronTurn.BeginAnimation(RotateTransform.AngleProperty, Motion.To(turn, view.Header.Open ? Grow : Close, Motion.Standard));
        }
        ShowStrip(view);
    }

    /// The header opens in place and the terminal moves down: nothing covers a real terminal.
    ///
    /// The terminal is a window of its own inside this one: moved on every frame, it lags behind the
    /// software drawing of WPF and jerks. So while the header moves, the terminal is a picture of
    /// itself (TerminalSurface.ShowStill), and the body under the header keeps one height.
    ///
    /// The body stands still while the header moves over it (FollowStrip). Opening: the header grows
    /// over the top of the picture, then the body takes its place and height under it once. Closing:
    /// the body is in its final place and height from the start, and the folding header uncovers it.
    /// The real terminal comes back when the header stands.
    private void ShowStrip(TerminalView view)
    {
        if (!view.Header.Open)
        {
            if (_stripFor is null)
            {
                return;
            }

            _stripFor = null;
            if (!Motion.IsOn || !IsLoaded)
            {
                HideStrip();
                _pills.Hide(false, Close);
                return;
            }

            ShowStills(true);
            HoldBody(_strip.ActualHeight);
            FollowStrip();
            _header.FadeOut(Fold);
            _pills.Hide(false, Close, after: PillsBack);
            var shrink = new System.Windows.Media.Animation.DoubleAnimation(_strip.ActualHeight, 0, Close) { EasingFunction = Motion.Standard };
            shrink.Completed += (_, _) =>
            {
                // Opened again while it was closing: the new header stays.
                if (_stripFor is null)
                {
                    HideStrip();
                    ShowLiveAfterLayout();
                }
            };
            _strip.BeginAnimation(HeightProperty, shrink);
            return;
        }

        var first = _stripFor is null;
        var moved = _stripFor != view.Environment;
        _stripFor = view.Environment;
        var motion = first ? HeaderMotion.Open : moved ? HeaderMotion.Switch : HeaderMotion.None;

        // A window that opens with the header already open shows it at once: it has no width yet
        // to measure the header against, and nothing to grow from.
        var grow = first && Motion.IsOn && IsLoaded && ActualWidth > 0;
        var from = _strip.Visibility == Visibility.Visible ? _strip.ActualHeight : 0;
        _header.Show(view.Header, first && !grow ? HeaderMotion.None : motion, SignalOf(view), view.Tabs.FirstOrDefault(tab => tab.Selected)?.Percent);
        _strip.Visibility = Visibility.Visible;
        if (motion == HeaderMotion.Switch && Motion.IsOn && IsLoaded)
        {
            HoldForSwitch();
        }

        // Pills are the closed header and cards the open one (D-376): the pills give way.
        _pills.Hide(true, Grow);

        if (!grow)
        {
            if (first)
            {
                StopStrip();
                ReleaseBody();
            }

            return;
        }

        // From where it is to its own height: measured once, then left to size itself.
        ShowStills(true);
        StopStrip();
        _strip.Measure(new Size(ActualWidth, double.PositiveInfinity));
        var height = _strip.DesiredSize.Height;
        HoldBody(0);
        _strip.Height = from;
        var growing = new System.Windows.Media.Animation.DoubleAnimation(from, height, Grow) { EasingFunction = Motion.Standard };
        growing.Completed += (_, _) =>
        {
            // Closed again while it was growing: the fold has taken over.
            if (_stripFor is null)
            {
                return;
            }

            StopStrip();
            ReleaseBody();
            ShowLiveAfterLayout();
        };
        _strip.BeginAnimation(HeightProperty, growing);
        FollowStrip();
    }

    /// The header is gone, and the terminal takes its place.
    private void HideStrip()
    {
        StopStrip();
        _strip.Visibility = Visibility.Collapsed;
        _header.Clear();
        ReleaseBody();
    }

    /// The open terminal as a picture while the header moves, or live again.
    private void ShowStills(bool still)
    {
        foreach (var terminal in _terminals.Children.OfType<TerminalSurface>())
        {
            if (still)
            {
                terminal.ShowStill();
            }
            else
            {
                terminal.ShowLive();
            }
        }
    }

    /// The real terminal comes back after the layout has put it in its new place and size, so it
    /// does not flash for a frame where the picture was.
    private void ShowLiveAfterLayout() => Dispatcher.BeginInvoke(() =>
    {
        if (!_strip.HasAnimatedProperties)
        {
            ShowStills(false);
        }
    }, System.Windows.Threading.DispatcherPriority.Loaded);

    /// Two environments with a different number of limit rows: the header moves to its new height
    /// with the swap, and the body is held as when the header opens (taller) or closes (lower), so
    /// the terminal changes its size once.
    private void HoldForSwitch()
    {
        _strip.Measure(new Size(ActualWidth, double.PositiveInfinity));
        var change = _header.SwitchHeightChange;
        if (Math.Abs(change) >= 0.5)
        {
            HoldBody(change < 0 ? -change : 0);
        }
    }

    /// The header sizes itself again.
    private void StopStrip()
    {
        _strip.BeginAnimation(HeightProperty, null);
        _strip.Height = double.NaN;
        ShiftBody(0);
    }

    /// The body moves back by this much, and the list of sessions comes forward by as much.
    private void ShiftBody(double back)
    {
        _bodyShift.Y = -back;
        _listShift.Y = back;
    }

    /// While the header grows or folds, the body is moved back by its height on every frame, so the
    /// picture of the terminal does not move: the header passes over it. Opening, the terminal takes
    /// its place under the header once, when the header stands; folding, it is in its place from the
    /// start and the header uncovers it (D-390).
    private void FollowStrip()
    {
        void OnFrame(object? sender, EventArgs e)
        {
            if (!_strip.HasAnimatedProperties)
            {
                CompositionTarget.Rendering -= OnFrame;
                ShiftBody(0);
                return;
            }

            ShiftBody(double.IsNaN(_strip.Height) ? _strip.ActualHeight : _strip.Height);
        }

        CompositionTarget.Rendering -= OnFrame;
        CompositionTarget.Rendering += OnFrame;
    }

    /// The body keeps its height plus extra while the header moves, so it reaches the bottom of the
    /// window while it is moved back under the header. When it is held already it keeps that
    /// height: the one the terminal has now.
    private void HoldBody(double extra)
    {
        if (!double.IsNaN(_body.Height) || _body.ActualHeight <= 0)
        {
            return;
        }

        _body.VerticalAlignment = VerticalAlignment.Top;
        _body.Height = _body.ActualHeight + extra;
    }

    /// The body fills the rest of the window again: on opening, this is the one resize of the
    /// terminal.
    private void ReleaseBody()
    {
        _body.Height = double.NaN;
        _body.VerticalAlignment = VerticalAlignment.Stretch;
    }

    /// Ctrl+Shift+B. True when the wide window should remember a hidden or shown column; a narrow
    /// window only opens or closes its list.
    public bool ToggleListFromKeys()
    {
        if (_narrow)
        {
            PopupZoom.Fit(_listPopup);
            ToggleListPopup();
            return false;
        }

        return true;
    }

    /// The column of sessions grows or shrinks to its new width; the terminal follows its edge.
    private void SlideList(double width)
    {
        // The window is drawn again on every event: only a new width starts the slide.
        if (width == _listTarget && !double.IsNaN(_list.Width))
        {
            return;
        }

        _listTarget = width;
        _list.ClipToBounds = true;
        var from = double.IsNaN(_list.Width) ? width : _list.ActualWidth;
        if (from == width || !Motion.IsOn || !IsLoaded)
        {
            _list.BeginAnimation(WidthProperty, null);
            _list.Width = width;
            return;
        }

        var slide = new System.Windows.Media.Animation.DoubleAnimation(from, width, Motion.Settle) { EasingFunction = Motion.Standard };
        slide.Completed += (_, _) =>
        {
            _list.BeginAnimation(WidthProperty, null);
            _list.Width = width;
        };
        _list.BeginAnimation(WidthProperty, slide);
    }

    private void ToggleList()
    {
        if (_narrow)
        {
            PopupZoom.Fit(_listPopup);
            ToggleListPopup();
            return;
        }

        ListToggled?.Invoke();
    }

    private readonly Dictionary<string, TabMarks> _tabMarks = [];

    /// The pill of an environment (D-376): the ring and percentage of its limit, its name, and the
    /// sessions that want attention. In a narrow window a pill that is not chosen keeps the ring and
    /// the marks; the name and the number move to the tooltip.
    private Pill Tab(TabView tab)
    {
        TextBlock? label = null;
        var small = _narrow && !tab.Selected;
        var percent = tab.Percent is { } known ? known.ToString(CultureInfo.CurrentCulture) + "%" : null;
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        // The mark is a part of the pill: a click on it picks the environment, as anywhere on the
        // pill. Only the chevron opens the header (D-407): the ring stood right after the chevron,
        // and a click on the first pill opened the header instead.
        row.Children.Add(Marks.Ring(tab.Percent, tab.Tone, 14, 2));

        if (!small)
        {
            // One weight for every pill: the chosen one differs by colour and its background only,
            // so a pill keeps its width when it is chosen and the background lands on it.
            // The name and its numbers are runs of one text, so they stand on one baseline: two
            // texts of 13 and 11 units, each centred, put the smaller one a pixel higher.
            var name = Text(tab.Name, "TextBody", tab.Selected ? "Ink" : "Muted", FontWeights.Medium);
            name.Margin = new Thickness(6, 0, 0, 0);
            if (percent is not null)
            {
                AddNumber(name, percent, "Muted");
            }

            row.Children.Add(name);
            label = name;
        }

        if (!_tabMarks.TryGetValue(tab.Name, out var marks))
        {
            marks = new TabMarks();
            _tabMarks[tab.Name] = marks;
        }

        (marks.Root.Parent as Panel)?.Children.Remove(marks.Root);
        row.Children.Add(marks.Root);
        marks.Show(tab.Waiting, tab.Done, tab.Stopped);

        var back = new Border
        {
            Padding = new Thickness(small ? 8 : 10, 0, small ? 8 : 12, 0),
            Margin = new Thickness(0, 0, 4, 0),
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
            Child = row,
            ToolTip = small ? (percent is null ? tab.Name : tab.Name + " · " + percent) : null,
        };
        back.MouseLeftButtonUp += (_, _) => TabPicked?.Invoke(tab.Name);
        WindowChromeHit(back);
        return new Pill(back, label, tab.Name);
    }

    /// The list icon: a frame with a column on its left.
    private static FrameworkElement ListIcon() => Marks.Line("M1.5,1.5 L12.5,1.5 L12.5,11.5 L1.5,11.5 Z M5,1.5 L5,11.5");

    /// The gear of the project's icon set.
    private static FrameworkElement GearIcon() => new Path
    {
        Data = Tokens.Get<Geometry>("GearPath"),
        Fill = Tokens.Brush("Muted"),
        Stretch = Stretch.Uniform,
        Width = 14,
        Height = 14,
        VerticalAlignment = VerticalAlignment.Center,
    };

    /// The list of a narrow window under its button: opened with the pop-in, closed at once.
    private void ToggleListPopup()
    {
        if (_listPopup.IsOpen || _listPopup.Child is not FrameworkElement list)
        {
            _listPopup.IsOpen = false;
            return;
        }

        Motion.OpenPopup(_listPopup, list, above: false);
    }
}
