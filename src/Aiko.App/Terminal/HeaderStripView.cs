using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Aiko.Core;

namespace Aiko.App;

/// How the open header comes in: with its lines one after another when it opens, sideways when the
/// active environment changes, or at once when only its numbers changed.
enum HeaderMotion
{
    None,
    Open,
    Switch,
}

/// The open header of the terminal window (D-373, D-376): the active environment on the left with
/// the words of the card of limits, the others as narrow quiet columns. When the header is open its
/// cards are the way between environments: a click on a quiet one makes it active. It only draws a
/// HeaderView.
[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.17763")]
static class HeaderStripView
{
    /// How far a card comes from when the environments change places.
    private const double SwitchShift = 40;

    /// The quiet column of another environment.
    public const double QuietWidth = 168;

    /// The bars of the active environment stop here: a bar across a wide window reads as noise.
    private const double BarsMaxWidth = 460;

    /// Each line of the header comes a little after the one above it, like ink in a book.
    private static readonly TimeSpan Step = TimeSpan.FromMilliseconds(30);

    public static FrameworkElement Build(HeaderView header, HeaderMotion motion, Action<string> pick)
    {
        var grid = new Grid();
        var line = 0;
        foreach (var column in header.Columns)
        {
            var at = grid.ColumnDefinitions.Count;
            grid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = column.Column.Active ? new GridLength(1, GridUnitType.Star) : new GridLength(QuietWidth),
            });

            var lines = column.Column.Active ? Full(column) : Quiet(column);
            var stack = new StackPanel { Margin = column.Column.Active ? new Thickness(16, 12, 16, 14) : new Thickness(14, 12, 14, 14) };
            Grid.SetIsSharedSizeScope(stack, true);
            // The new active card comes from the right, where it stood; the one it replaced goes the
            // other way into the quiet column.
            var from = motion == HeaderMotion.Switch ? (column.Column.Active ? SwitchShift : -SwitchShift) : 0;
            foreach (var element in lines)
            {
                stack.Children.Add(element);
                if (motion != HeaderMotion.None)
                {
                    Ink(element, Step * line++, from, motion == HeaderMotion.Open ? 3 : 0);
                }
            }

            var cell = new Border
            {
                Child = stack,
                Background = Brushes.Transparent,
                BorderBrush = Tokens.Brush("Hairline"),
                BorderThickness = new Thickness(at == 0 ? 0 : 1, 0, 0, 0),
            };
            if (!column.Column.Active)
            {
                Pickable(cell, column.Column.Environment, pick);
            }

            Grid.SetColumn(cell, at);
            grid.Children.Add(cell);
        }

        return grid;
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

    private static IEnumerable<FrameworkElement> Full(HeaderColumnView column)
    {
        yield return Head(column, quiet: false);

        if (column.Block is not { } block)
        {
            yield break;
        }

        if (block.Rows.Count == 0)
        {
            var note = TerminalPanel.Text(block.Note, "TextNumber", "Muted", FontWeights.Normal);
            note.TextWrapping = TextWrapping.Wrap;
            note.Margin = new Thickness(0, 6, 0, 0);
            yield return note;
        }

        foreach (var row in block.Rows)
        {
            yield return Bar(row);
            yield return Under(row);
        }

        yield return Profile(column, block);
    }

    private static IEnumerable<FrameworkElement> Quiet(HeaderColumnView column)
    {
        yield return Head(column, quiet: true);

        var numbers = TerminalPanel.Mono(
            string.Format(CultureInfo.CurrentCulture, Strings.TerminalQuietLimits, Percent(column.Column.FiveHour), Percent(column.Column.SevenDay)),
            "Muted");
        numbers.Opacity = 0.55;
        numbers.Margin = new Thickness(0, 6, 0, 0);
        yield return numbers;

        if (column.Column.Waiting > 0)
        {
            // The one bright thing in a quiet column: a session there waits for the person.
            var waiting = TerminalPanel.Mono(WaitingWords(column.Column.Waiting), "Caution");
            waiting.Margin = new Thickness(0, 4, 0, 0);
            yield return waiting;
        }
    }

    private static FrameworkElement Head(HeaderColumnView column, bool quiet)
    {
        var head = new StackPanel { Orientation = Orientation.Horizontal };
        var ring = Ring(column.Column.FiveHour, quiet ? LimitTone.Unknown : ToneOf(column), 14, 2, quiet ? 0.6 : 1);
        ring.Margin = new Thickness(0, 0, 8, 0);
        head.Children.Add(ring);

        var name = TerminalPanel.Text(column.Column.Environment, "TextNumber", quiet ? "Muted" : "Ink", quiet ? FontWeights.Normal : FontWeights.Medium);
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        head.Children.Add(name);

        if (!quiet && column.Column.Waiting > 0)
        {
            var waiting = TerminalPanel.Mono(WaitingWords(column.Column.Waiting), "Caution");
            waiting.Margin = new Thickness(8, 0, 0, 0);
            head.Children.Add(waiting);
        }

        return head;
    }

    /// "5 hours ━━━━━━──── 42%": the name, the bar and the number, no wider than BarsMaxWidth.
    private static FrameworkElement Bar(LimitRow row)
    {
        var grid = new Grid();
        // The names of the windows share one width, so both bars start at the same place.
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "LimitName" });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });

        var name = TerminalPanel.Text(row.Name, "TextSmall", "Muted", FontWeights.Normal);
        name.Margin = new Thickness(0, 0, 12, 0);
        grid.Children.Add(name);

        var track = new Grid { Height = 4, VerticalAlignment = VerticalAlignment.Center };
        track.ColumnDefinitions.Add(new ColumnDefinition { Width = row.Fill });
        track.ColumnDefinitions.Add(new ColumnDefinition { Width = row.Rest });
        var back = new Border { CornerRadius = new CornerRadius(2), Background = Tokens.Brush("Raised") };
        Grid.SetColumnSpan(back, 2);
        track.Children.Add(back);
        track.Children.Add(new Border { CornerRadius = new CornerRadius(2), Background = row.Tone });
        Grid.SetColumn(track, 1);
        grid.Children.Add(track);

        var percent = TerminalPanel.Mono(row.Percent, "Ink");
        percent.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(percent, 2);
        grid.Children.Add(percent);

        // A star column with a ceiling keeps the bar on the left and lets it shrink in a narrow window.
        var room = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        room.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MaxWidth = BarsMaxWidth });
        room.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        room.Children.Add(grid);
        return room;
    }

    /// The reset time under a bar, and the word of its tone or pace beside it.
    private static FrameworkElement Under(LimitRow row)
    {
        var line = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 0) };
        line.Children.Add(TerminalPanel.Mono(row.Resets, "Muted"));
        // Only the word of a tone ("running low"); a guess about the pace is the card's detail.
        if (row.Pace.Length > 0 && !ReferenceEquals(row.PaceTone, Tokens.Brush("Muted")))
        {
            var pace = TerminalPanel.Mono(row.Pace, "Muted");
            pace.Foreground = row.PaceTone;
            pace.Margin = new Thickness(12, 0, 0, 0);
            line.Children.Add(pace);
        }

        return line;
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
        line.Margin = new Thickness(0, 10, 0, 0);
        line.TextTrimming = TextTrimming.CharacterEllipsis;
        return line;
    }

    private static LimitTone ToneOf(HeaderColumnView column) =>
        column.Column.FiveHour is { } percent ? CardState.ToneFor(percent, DataFreshness.Live) : LimitTone.Unknown;

    private static string Percent(int? value) => value is { } known ? known.ToString(CultureInfo.CurrentCulture) + "%" : "—";

    public static string WaitingWords(int count) =>
        string.Format(
            CultureInfo.CurrentCulture,
            count % 10 == 1 && count % 100 != 11 ? Strings.TerminalTabWaitingOne : Strings.TerminalTabWaitingMany,
            count);

    /// A quiet card is a button: a soft fill under the pointer, and a click makes its environment
    /// the active one.
    private static void Pickable(Border cell, string environment, Action<string> pick)
    {
        var hover = new SolidColorBrush(Colors.Transparent);
        var fill = ((SolidColorBrush)Tokens.Brush("HoverLayer")).Color;
        cell.Background = hover;
        cell.Cursor = System.Windows.Input.Cursors.Hand;
        cell.ToolTip = environment;
        cell.MouseEnter += (_, _) => hover.BeginAnimation(SolidColorBrush.ColorProperty, Motion.To(fill, Motion.Or0(Motion.Hover)));
        cell.MouseLeave += (_, _) => hover.BeginAnimation(SolidColorBrush.ColorProperty, Motion.To(Colors.Transparent, Motion.Or0(Motion.Hover)));
        cell.MouseLeftButtonUp += (_, _) => pick(environment);
    }

    /// A line comes in from a soft start, a little after the one above: up from below when the
    /// header opens, sideways when the environments change places.
    private static void Ink(UIElement element, TimeSpan after, double fromX, double fromY)
    {
        if (!Motion.IsOn)
        {
            return;
        }

        var target = element.Opacity;
        var shift = new TranslateTransform(fromX, fromY);
        element.RenderTransform = shift;
        element.Opacity = 0;
        element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, target, Motion.Settle) { BeginTime = after, EasingFunction = Motion.Standard });
        shift.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(fromX, 0, Motion.Settle) { BeginTime = after, EasingFunction = Motion.Standard });
        shift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(fromY, 0, Motion.Settle) { BeginTime = after, EasingFunction = Motion.Standard });
    }
}
