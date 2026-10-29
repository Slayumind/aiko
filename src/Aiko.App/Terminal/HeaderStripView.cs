using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Aiko.Core;

namespace Aiko.App;

/// How the open header comes in: its cards appear when it opens, they change places when the
/// active environment changes, or only their numbers change.
enum HeaderMotion
{
    None,
    Open,
    Switch,
}

/// The open header of the terminal window (D-373, D-376): one card per environment, the active one
/// on the left and in full, the others as narrow quiet cards. When the header is open its cards are
/// the way between environments: a click on a quiet one makes it active.
///
/// The card of an environment is kept while the header is open, so on a switch the same card moves
/// to its new place and changes its size, and only its contents cross-fade. It draws a HeaderView.
[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.17763")]
sealed class HeaderStripView
{
    /// The quiet card of another environment.
    public const double QuietWidth = 168;

    /// The active card, and the least it shrinks to in a narrow window.
    public const double ActiveWidth = 480;
    public const double ActiveMinWidth = 300;

    /// Under this width the active card names its limit windows short ("5 h", "wk", "Fable"), so
    /// the bars keep a length that can be read.
    public const double CompactBelow = 440;

    /// Between two cards, and around the row of cards.
    public const double Gap = 12;
    private static readonly Thickness StripPadding = new(12, 10, 12, 10);
    private const double CardPadding = 12;
    private const double RowHeight = 24;

    /// Session, week, and the week of the heavy model when the server sends it.
    private const int MostRows = 3;

    // The timings of the owner's choice (polish of the header, round 1).
    private static readonly Duration Appear = new(TimeSpan.FromMilliseconds(320));
    private static readonly TimeSpan QuietLater = TimeSpan.FromMilliseconds(80);
    private static readonly Duration ContentOut = new(TimeSpan.FromMilliseconds(140));
    private static readonly TimeSpan ContentInAt = TimeSpan.FromMilliseconds(220);
    private static readonly Duration ContentIn = new(TimeSpan.FromMilliseconds(160));

    private readonly Action<string> _pick;
    private readonly HeaderCards _row = new();
    private readonly Dictionary<string, Card> _cards = [];

    public HeaderStripView(Action<string> pick)
    {
        _pick = pick;
        Root = new Border { Padding = StripPadding, Child = _row };
    }

    /// The row of cards with the padding of the strip around it.
    public FrameworkElement Root { get; }

    public void Show(HeaderView header, HeaderMotion motion)
    {
        if (motion == HeaderMotion.Open)
        {
            // Opened again while it was closing: the fade out stops here.
            Root.BeginAnimation(UIElement.OpacityProperty, null);
            Root.Opacity = 1;
        }

        var animate = Motion.IsOn && motion != HeaderMotion.None;
        var order = new List<Card>();
        foreach (var column in header.Columns)
        {
            if (!_cards.TryGetValue(column.Column.Environment, out var card))
            {
                card = new Card(column.Column.Environment, _pick);
                _cards[column.Column.Environment] = card;
                _row.Children.Add(card.Root);
            }

            var content = column.Column.Active ? Full(column) : Quiet(column);
            if (motion == HeaderMotion.Switch && animate && card.Active != column.Column.Active)
            {
                card.CrossFade(content, column.Column.Active);
            }
            else
            {
                card.Put(content, column.Column.Active);
            }

            order.Add(card);
        }

        foreach (var gone in _cards.Where(pair => !order.Contains(pair.Value)).ToList())
        {
            _row.Children.Remove(gone.Value.Root);
            _cards.Remove(gone.Key);
        }

        _row.Place([.. order.Select(card => (UIElement)card.Root)], order.FindIndex(card => card.Active), animate && motion == HeaderMotion.Switch);

        if (animate && motion == HeaderMotion.Open)
        {
            for (var at = 0; at < order.Count; at++)
            {
                order[at].Rise(at == 0 ? TimeSpan.Zero : QuietLater);
            }
        }
    }

    /// How much taller the header gets during the switch now running; negative when it gets
    /// lower. Known after a measure.
    public double SwitchHeightChange => _row.HeightChange;

    /// A switch has ended: the cards are in place and the header has its new height.
    public event Action? Settled
    {
        add => _row.Settled += value;
        remove => _row.Settled -= value;
    }

    /// The header closes: both cards go out together, then done runs.
    public void FadeOut(Duration duration, Action done)
    {
        var fade = new DoubleAnimation(0, duration) { EasingFunction = Motion.Standard };
        fade.Completed += (_, _) => done();
        Root.BeginAnimation(UIElement.OpacityProperty, fade);
    }

    /// The header closed: the next open starts from new cards.
    public void Clear()
    {
        _row.Children.Clear();
        _cards.Clear();
        Root.BeginAnimation(UIElement.OpacityProperty, null);
        Root.Opacity = 1;
    }

    /// The ring of a limit: a full grey track and the used part from the top, clockwise.
    public static FrameworkElement Ring(int? percent, LimitTone tone, double size, double thickness, double opacity = 1)
    {
        var ring = new Grid { Width = size, Height = size, VerticalAlignment = VerticalAlignment.Center, Opacity = opacity };
        ring.Children.Add(new Ellipse { Stroke = Tokens.Brush("Raised"), StrokeThickness = thickness });
        if (percent is { } used && used > 0)
        {
            ring.Children.Add(new Path
            {
                Stroke = Tokens.ToneBrush(tone),
                StrokeThickness = thickness,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Data = Arc(size / 2, (size - thickness) / 2, used / 100.0),
            });
        }

        return ring;
    }

    /// A part of a circle from the top, clockwise.
    public static Geometry Arc(double centre, double radius, double part)
    {
        part = Math.Clamp(part, 0, 0.999);
        var angle = part * 2 * Math.PI;
        var end = new Point(centre + radius * Math.Sin(angle), centre - radius * Math.Cos(angle));
        var figure = new PathFigure { StartPoint = new Point(centre, centre - radius) };
        figure.Segments.Add(new ArcSegment(end, new Size(radius, radius), 0, part > 0.5, SweepDirection.Clockwise, true));
        return new PathGeometry([figure]);
    }

    public static string WaitingWords(int count) =>
        string.Format(
            CultureInfo.CurrentCulture,
            count % 10 == 1 && count % 100 != 11 ? Strings.TerminalTabWaitingOne : Strings.TerminalTabWaitingMany,
            count);

    /// The width of the active card: as wide as it wants when there is room, narrower in a narrow
    /// window, never under the minimum.
    public static double ActiveWidthFor(double room, int quietCards) =>
        Math.Clamp(room - quietCards * (QuietWidth + Gap), ActiveMinWidth, ActiveWidth);

    // ---- the contents of a card ----

    /// The active card: the head, one line per limit, and the profile.
    private static FrameworkElement Full(HeaderColumnView column)
    {
        var stack = new StackPanel();
        // The names, resets and tone words of the rows share their widths, so the bars line up.
        Grid.SetIsSharedSizeScope(stack, true);
        stack.Children.Add(Head(column, column.Block));

        if (column.Block is not { } block)
        {
            return stack;
        }

        // Room for the most limit rows an environment has, so every environment gives the header
        // the same height: a switch never moves the terminal under it.
        var rows = new StackPanel { Margin = new Thickness(0, 4, 0, 0), MinHeight = MostRows * RowHeight };
        if (block.Rows.Count == 0)
        {
            var note = TerminalPanel.Text(block.Note, "TextSmall", "Muted", FontWeights.Normal);
            note.TextWrapping = TextWrapping.Wrap;
            note.Margin = new Thickness(0, 4, 0, 0);
            rows.Children.Add(note);
        }

        foreach (var row in block.Rows)
        {
            rows.Children.Add(Row(row));
        }

        stack.Children.Add(rows);
        stack.Children.Add(Profile(column, block));
        return stack;
    }

    /// The quiet card: a dim head, two small bars, and the one bright thing, "N waiting".
    private static FrameworkElement Quiet(HeaderColumnView column)
    {
        var stack = new StackPanel();
        Grid.SetIsSharedSizeScope(stack, true);
        stack.Children.Add(Head(column, null));

        var rows = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        rows.Children.Add(MiniRow(Strings.CardShortSession, column.Column.FiveHour));
        rows.Children.Add(MiniRow(Strings.CardShortWeek, column.Column.SevenDay));
        stack.Children.Add(rows);

        if (column.Column.Waiting > 0)
        {
            var waiting = TerminalPanel.Text(WaitingWords(column.Column.Waiting), "TextSmall", "Caution", FontWeights.Normal);
            waiting.Margin = new Thickness(0, 8, 0, 0);
            stack.Children.Add(waiting);
        }

        return stack;
    }

    /// Ring, name, "N waiting", and at the right end "working now" when the environment works.
    private static FrameworkElement Head(HeaderColumnView column, EnvironmentBlock? block)
    {
        var quiet = block is null;
        var head = new DockPanel { Height = 20, LastChildFill = true };

        if (block is { HaloVisibility: Visibility.Visible })
        {
            var state = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
            var dot = new Grid { Width = 12, Height = 12, Margin = new Thickness(0, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center };
            dot.Children.Add(new Ellipse { Width = 12, Height = 12, Fill = Tokens.Brush("Positive"), Opacity = 0.18 });
            dot.Children.Add(new Ellipse { Width = 6, Height = 6, Fill = block.StateFill, Stroke = block.StateRing, StrokeThickness = 1.5 });
            state.Children.Add(dot);
            var words = TerminalPanel.Text(block.State, "TextTiny", "Ink", FontWeights.Normal);
            words.Foreground = block.StateText;
            state.Children.Add(words);
            DockPanel.SetDock(state, Dock.Right);
            head.Children.Add(state);
        }

        var left = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var ring = Ring(column.Column.FiveHour, quiet ? LimitTone.Unknown : ToneOf(column), 14, 2, quiet ? 0.6 : 1);
        ring.Margin = new Thickness(0, 0, 8, 0);
        left.Children.Add(ring);

        var name = TerminalPanel.Text(column.Column.Environment, "TextNumber", quiet ? "Muted" : "Ink", quiet ? FontWeights.Normal : FontWeights.Medium);
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        left.Children.Add(name);

        if (!quiet && column.Column.Waiting > 0)
        {
            var waiting = TerminalPanel.Text(WaitingWords(column.Column.Waiting), "TextSmall", "Caution", FontWeights.Normal);
            waiting.Margin = new Thickness(8, 0, 0, 0);
            left.Children.Add(waiting);
        }

        head.Children.Add(left);
        return head;
    }

    /// "Session · 5 hours ━━━━━━──── 42%  1h 6m  running low": one line of 24 px per limit.
    private static FrameworkElement Row(LimitRow row)
    {
        var grid = new Grid { Height = RowHeight };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "LimitName" });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
        // The countdown and the tone word have places of their own in every state, so the bar keeps
        // its length when the time gets shorter or a limit starts to run low. Only a word longer
        // than the place ("almost gone" in some languages) widens it.
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, MinWidth = CountdownReserve(), SharedSizeGroup = "LimitReset" });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, MinWidth = ToneReserve(), SharedSizeGroup = "LimitTone" });

        var name = new LimitWindowName(row.Name, row.ShortName);
        name.Margin = new Thickness(0, 0, 12, 0);
        grid.Children.Add(name);

        var track = Track(row.Fill, row.Rest, 4, row.Tone, 1);
        track.Margin = new Thickness(0, 0, 8, 0);
        Grid.SetColumn(track, 1);
        grid.Children.Add(track);

        var percent = TerminalPanel.Mono(row.Percent, "Ink");
        percent.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(percent, 2);
        grid.Children.Add(percent);

        var resets = TerminalPanel.Mono(row.Countdown, "Muted");
        resets.Margin = new Thickness(12, 0, 0, 0);
        Grid.SetColumn(resets, 3);
        grid.Children.Add(resets);

        // Only the word of a tone ("running low"), in its colour; a guess about the pace is the
        // card's detail.
        if (row.Pace.Length > 0 && !ReferenceEquals(row.PaceTone, Tokens.Brush("Muted")))
        {
            var tone = TerminalPanel.Text(row.Pace, "TextSmall", "Muted", FontWeights.Normal);
            tone.Foreground = row.PaceTone;
            tone.Margin = new Thickness(8, 0, 0, 0);
            Grid.SetColumn(tone, 4);
            grid.Children.Add(tone);
        }

        return grid;
    }

    /// "5 h ━━── 82%" in a quiet card: a thin dim bar, nothing coloured.
    private static FrameworkElement MiniRow(string label, int? percent)
    {
        var grid = new Grid { Height = 16 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "QuietName" });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });

        var name = TerminalPanel.Mono(label, "Muted");
        name.Margin = new Thickness(0, 0, 8, 0);
        grid.Children.Add(name);

        var used = Math.Clamp(percent ?? 0, 0, 100);
        var track = Track(new GridLength(used, GridUnitType.Star), new GridLength(100 - used, GridUnitType.Star), 2, Tokens.Brush("Muted"), 0.5);
        track.Margin = new Thickness(0, 0, 4, 0);
        Grid.SetColumn(track, 1);
        grid.Children.Add(track);

        var number = TerminalPanel.Mono(Percent(percent), "Muted");
        number.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(number, 2);
        grid.Children.Add(number);
        return grid;
    }

    /// A bar: a grey track and the used part in two star columns, so the fill follows the
    /// percentage without any measuring in code.
    private static FrameworkElement Track(GridLength fill, GridLength rest, double height, Brush tone, double toneOpacity)
    {
        var track = new Grid { Height = height, VerticalAlignment = VerticalAlignment.Center };
        track.ColumnDefinitions.Add(new ColumnDefinition { Width = fill });
        track.ColumnDefinitions.Add(new ColumnDefinition { Width = rest });
        var back = new Border { CornerRadius = new CornerRadius(height / 2), Background = Tokens.Brush("Raised") };
        Grid.SetColumnSpan(back, 2);
        track.Children.Add(back);
        track.Children.Add(new Border { CornerRadius = new CornerRadius(height / 2), Background = tone, Opacity = toneOpacity });
        return track;
    }

    /// "Max 5x · .claude · direct mode off": who this environment is, on one line.
    private static FrameworkElement Profile(HeaderColumnView column, EnvironmentBlock block)
    {
        var parts = new List<string>();
        if (block.Plan.Length > 0)
        {
            parts.Add(block.Plan);
        }

        parts.Add(column.Folder);
        parts.Add(column.Direct ? Strings.TerminalDirectOn : Strings.TerminalDirectOff);

        var line = TerminalPanel.Text(string.Join("  ·  ", parts), "TextSmall", "Muted", FontWeights.Normal);
        line.Margin = new Thickness(0, 8, 0, 0);
        line.TextTrimming = TextTrimming.CharacterEllipsis;
        return line;
    }

    /// The place of the tone word: the word of "running low" in the current language, and the gap
    /// before it.
    private static double ToneReserve() =>
        Math.Ceiling(TextWidth(Strings.CardToneCaution, Tokens.Get<FontFamily>("Sans"), Tokens.Get<double>("TextSmall"))) + 8;

    /// The place of the countdown: the longest time it can show, and the gap before it.
    private static double CountdownReserve()
    {
        var family = Tokens.Get<FontFamily>("Mono");
        var size = Tokens.Get<double>("TextTiny");
        var longest = new[]
        {
            string.Format(CultureInfo.CurrentCulture, Strings.SpanDaysHours, 6, 23),
            string.Format(CultureInfo.CurrentCulture, Strings.SpanHoursMinutes, 23, 59),
        }.Max(time => TextWidth(time, family, size));
        return Math.Ceiling(longest) + 12;
    }

    private static double TextWidth(string text, FontFamily family, double size) =>
        new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(family, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
            size,
            Brushes.Black,
            1.0).WidthIncludingTrailingWhitespace;

    private static LimitTone ToneOf(HeaderColumnView column) =>
        column.Column.FiveHour is { } percent ? CardState.ToneFor(percent, DataFreshness.Live) : LimitTone.Unknown;

    private static string Percent(int? value) => value is { } known ? known.ToString(CultureInfo.CurrentCulture) + "%" : "—";

    /// One card of the header. It stays the same object while the header is open, whatever its
    /// role: on a switch it changes its contents, not itself.
    private sealed class Card
    {
        private readonly SolidColorBrush _hover = new(Colors.Transparent);
        private readonly CardBody _body = new() { Margin = new Thickness(CardPadding), ClipToBounds = true };
        private readonly TranslateTransform _rise = new();

        /// The contents shown now, and the ones that go out during a switch. The old ones lie on a
        /// canvas, which gives them no say in the height of the card: the card takes the height of
        /// its new contents, and the row can move to it.
        private Border _current = new();
        private readonly Canvas _leaving = new();

        public Card(string environment, Action<string> pick)
        {
            _body.Children.Add(_current);
            _body.Children.Add(_leaving);
            var face = new Grid();
            face.Children.Add(new Border { CornerRadius = new CornerRadius(13), Background = _hover });
            face.Children.Add(_body);

            Root = new Border
            {
                CornerRadius = new CornerRadius(14),
                Background = Tokens.Brush("Surface"),
                BorderBrush = Tokens.Brush("Hairline"),
                BorderThickness = new Thickness(1),
                Child = face,
                RenderTransform = _rise,
            };

            // A quiet card is a button: a soft fill under the pointer, and a click makes its
            // environment the active one. The active card is not.
            var fill = ((SolidColorBrush)Tokens.Brush("HoverLayer")).Color;
            Root.MouseEnter += (_, _) =>
            {
                if (!Active)
                {
                    _hover.BeginAnimation(SolidColorBrush.ColorProperty, Motion.To(fill, Motion.Hover));
                }
            };
            Root.MouseLeave += (_, _) => _hover.BeginAnimation(SolidColorBrush.ColorProperty, Motion.To(Colors.Transparent, Motion.Hover));
            Root.MouseLeftButtonUp += (_, _) =>
            {
                if (!Active)
                {
                    pick(environment);
                }
            };
            Environment = environment;
        }

        public Border Root { get; }

        public string Environment { get; }

        public bool Active { get; private set; } = true;

        /// New contents at once: the numbers changed, or nothing moves.
        public void Put(FrameworkElement content, bool active)
        {
            Role(active);
            _current.Child = content;
        }

        /// The card changes its role: the old contents fade out in the first 140 ms, the new ones
        /// fade in from 220 ms to 380 ms, while the card itself moves and changes its width. Each
        /// layer keeps the width of its own role, so no line reflows while the card resizes.
        public void CrossFade(FrameworkElement content, bool active)
        {
            _leaving.Children.Clear();
            var oldWidth = _body.ActualWidth;
            var old = _current;
            _body.Children.Remove(old);
            _leaving.Children.Add(old);
            old.HorizontalAlignment = HorizontalAlignment.Left;
            old.Width = oldWidth > 0 ? oldWidth : double.NaN;
            old.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, ContentOut) { EasingFunction = Motion.Standard });

            _current = new Border
            {
                Child = content,
                Opacity = 0,
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = InnerWidth(active),
            };
            _body.Children.Insert(0, _current);

            var fadeIn = new DoubleAnimation(0, 1, ContentIn) { BeginTime = ContentInAt, EasingFunction = Motion.Standard };
            var layer = _current;
            fadeIn.Completed += (_, _) =>
            {
                // A newer switch has already taken over.
                if (!ReferenceEquals(layer, _current))
                {
                    return;
                }

                _leaving.Children.Clear();

                _current.BeginAnimation(UIElement.OpacityProperty, null);
                _current.Opacity = 1;
                _current.Width = double.NaN;
                _current.HorizontalAlignment = HorizontalAlignment.Stretch;
            };
            _current.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            Role(active);
        }

        /// Fades in and rises 4 px when the header opens.
        public void Rise(TimeSpan after)
        {
            Root.Opacity = 0;
            Root.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, Appear) { BeginTime = after, EasingFunction = Motion.Standard });
            _rise.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(Motion.AppearShift, 0, Appear) { BeginTime = after, EasingFunction = Motion.Standard });
        }

        private void Role(bool active)
        {
            Active = active;
            Root.Cursor = active ? null : System.Windows.Input.Cursors.Hand;
            Root.ToolTip = active ? null : Environment;
            Panel.SetZIndex(Root, active ? 1 : 0);
            if (active)
            {
                _hover.BeginAnimation(SolidColorBrush.ColorProperty, null);
                _hover.Color = Colors.Transparent;
            }
        }

        /// The width of the contents in a card of this role, inside its border and padding. The
        /// active width is the one the row gave the active card last.
        private double InnerWidth(bool active)
        {
            var row = Root.Parent as HeaderCards;
            var width = active ? row?.ActiveCardWidth ?? ActiveWidth : QuietWidth;
            return Math.Max(0, width - 2 - 2 * CardPadding);
        }
    }
}

/// The inside of a card. It asks for no more height than the row gives it and cuts off what does
/// not fit, so while the row moves to a new height the card keeps its own edge and corners instead
/// of hanging out of the row.
[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.17763")]
sealed class CardBody : Panel
{
    protected override Size MeasureOverride(Size available)
    {
        var size = new Size();
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(new Size(available.Width, double.PositiveInfinity));
            size.Width = Math.Max(size.Width, child.DesiredSize.Width);
            size.Height = Math.Max(size.Height, child.DesiredSize.Height);
        }

        size.Height = Math.Min(size.Height, available.Height);
        size.Width = Math.Min(size.Width, available.Width);
        return size;
    }

    protected override Size ArrangeOverride(Size final)
    {
        foreach (UIElement child in InternalChildren)
        {
            child.Arrange(new Rect(0, 0, final.Width, Math.Max(final.Height, child.DesiredSize.Height)));
        }

        return final;
    }
}

/// The row of cards of the open header. It places the cards side by side from the left: the active
/// one first, as wide as the room allows between its minimum and its full width, the quiet ones
/// after it. On a switch it moves each card from where it stood to its new slot and changes its
/// width at the same time, so the cards really change places.
[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.17763")]
sealed class HeaderCards : Panel
{
    private static readonly Duration Swap = new(TimeSpan.FromMilliseconds(380));

    /// 0 at the start of a switch, 1 at its end. Each frame of it places the cards again.
    private static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
        nameof(Progress), typeof(double), typeof(HeaderCards), new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    private double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    private IReadOnlyList<UIElement> _order = [];
    private int _active;

    /// Where each card stood when the switch started: its left edge and its width.
    private Dictionary<UIElement, (double X, double Width)> _from = [];
    private readonly Dictionary<UIElement, (double X, double Width)> _now = [];

    /// The width the last layout gave the active card.
    public double ActiveCardWidth { get; private set; } = HeaderStripView.ActiveWidth;

    /// True on the cards when the active card is too narrow for the long names of the limit
    /// windows. It passes down to every name inside a card.
    public static readonly DependencyProperty CompactProperty = DependencyProperty.RegisterAttached(
        "Compact", typeof(bool), typeof(HeaderCards), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));

    /// The height of the row when the switch started, and the height it had last.
    private double? _fromHeight;
    private double _lastHeight;

    /// The height the row goes to: the tallest card with its new contents.
    public double TargetHeight { get; private set; }

    /// How much taller the row gets during the switch now running; negative when it gets lower.
    public double HeightChange => _fromHeight is { } from ? TargetHeight - from : 0;

    /// The switch has ended: the cards are in their places and the row has its height.
    public event Action? Settled;

    /// The cards in their order on the row. slide moves them there from where they are now.
    public void Place(IReadOnlyList<UIElement> order, int active, bool slide)
    {
        if (slide)
        {
            _from = new Dictionary<UIElement, (double X, double Width)>(_now);
            _fromHeight = _lastHeight;
            var swap = new DoubleAnimation(0, 1, Swap) { EasingFunction = Motion.Standard };
            swap.Completed += (_, _) =>
            {
                if (Progress >= 1)
                {
                    _fromHeight = null;
                    Settled?.Invoke();
                }
            };
            BeginAnimation(ProgressProperty, swap);
        }

        _order = order;
        _active = Math.Max(0, active);
        InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size available)
    {
        var slots = Slots(available.Width);
        var compact = ActiveCardWidth < HeaderStripView.CompactBelow;
        var height = 0.0;
        foreach (UIElement child in InternalChildren)
        {
            child.SetValue(CompactProperty, compact);
            var width = slots.TryGetValue(child, out var slot) ? slot.Width : 0;
            child.Measure(new Size(width, double.PositiveInfinity));
            height = Math.Max(height, child.DesiredSize.Height);
        }

        // The cards take their new height at once; the row goes there in step with the swap.
        TargetHeight = height;
        var progress = Progress;
        if (progress < 1 && _fromHeight is { } from)
        {
            height = from + (height - from) * progress;
        }

        _lastHeight = height;

        // Measured again within the height of this moment: a card that will be taller is cut off
        // inside its padding, not at its border.
        if (progress < 1)
        {
            foreach (UIElement child in InternalChildren)
            {
                var width = slots.TryGetValue(child, out var slot) ? slot.Width : 0;
                child.Measure(new Size(width, height));
            }
        }

        var right = slots.Count == 0 ? 0 : slots.Values.Max(slot => slot.X + slot.Width);
        return new Size(double.IsInfinity(available.Width) ? right : Math.Min(right, available.Width), height);
    }

    protected override Size ArrangeOverride(Size final)
    {
        var slots = Slots(final.Width);
        _now.Clear();
        foreach (UIElement child in InternalChildren)
        {
            if (slots.TryGetValue(child, out var slot))
            {
                child.Arrange(new Rect(slot.X, 0, slot.Width, final.Height));
                _now[child] = slot;
            }
            else
            {
                child.Arrange(new Rect());
            }
        }

        return final;
    }

    /// The slot of each card at this moment of a switch: between where it stood and where it goes.
    private Dictionary<UIElement, (double X, double Width)> Slots(double room)
    {
        var quiet = Math.Max(0, _order.Count - 1);
        var activeWidth = double.IsInfinity(room) ? HeaderStripView.ActiveWidth : HeaderStripView.ActiveWidthFor(room, quiet);
        ActiveCardWidth = activeWidth;

        var progress = Progress;
        var slots = new Dictionary<UIElement, (double X, double Width)>();
        var x = 0.0;
        for (var at = 0; at < _order.Count; at++)
        {
            var width = at == _active ? activeWidth : HeaderStripView.QuietWidth;
            var target = (X: x, Width: width);
            if (progress < 1 && _from.TryGetValue(_order[at], out var start))
            {
                target = (start.X + (target.X - start.X) * progress, start.Width + (target.Width - start.Width) * progress);
            }

            slots[_order[at]] = target;
            x += width + HeaderStripView.Gap;
        }

        return slots;
    }
}

/// The name of a limit window in a row of the active card: the long name, or the short one when
/// the card is narrow (HeaderCards.Compact). The card decides it in its layout, so the name follows
/// any width of the window and not only the switch between wide and narrow.
[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.17763")]
sealed class LimitWindowName : TextBlock
{
    private readonly string _long;
    private readonly string _short;

    public LimitWindowName(string longName, string shortName)
    {
        _long = longName;
        _short = shortName;
        Text = longName;
        FontFamily = Tokens.Get<FontFamily>("Sans");
        FontSize = Tokens.Get<double>("TextSmall");
        Foreground = Tokens.Brush("Muted");
        VerticalAlignment = VerticalAlignment.Center;
    }

    static LimitWindowName() =>
        HeaderCards.CompactProperty.OverrideMetadata(
            typeof(LimitWindowName),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits, (target, e) =>
            {
                var name = (LimitWindowName)target;
                name.Text = (bool)e.NewValue ? name._short : name._long;
            }));
}
