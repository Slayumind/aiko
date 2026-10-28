using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// The menu over the field, as wide as the field (D-338, D-342): the commands of "/" or the files
/// of "@", one 28 px line each. Commands come in two groups, Claude Code first, then the skills;
/// their names and descriptions are the ones Claude Code gives, in English. The numbers are those of
/// the approved prototype in the artifact "Aiko".
sealed class SlashMenuView : Border
{
    /// The tallest the list gets, and the part of the feed that always stays in view above it.
    public const double MostHeight = 320;
    public const double FeedLeft = 80;

    private const double RowHeight = 28;

    /// PageUp and PageDown move this many lines (Cherry Studio, research of 2026-10-26).
    private const int PageRows = 7;

    private readonly StackPanel _rows = new();
    private readonly ScrollViewer _scroll;
    private readonly List<(Border Row, int Index)> _lines = [];
    private int _highlighted = -1;
    private int _count;

    public SlashMenuView()
    {
        Background = Tokens.Brush("Surface");
        BorderBrush = Tokens.Brush("Hairline");
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(12);
        Padding = new Thickness(6);
        Effect = Tokens.Get<System.Windows.Media.Effects.Effect>("CardShadow");
        Visibility = Visibility.Collapsed;

        Grid.SetIsSharedSizeScope(_rows, true);
        _scroll = new ScrollViewer { MaxHeight = MostHeight, Focusable = false, Content = _rows };
        Child = _scroll;
    }

    /// A line was chosen with the mouse: its place in the list given to Show.
    public event Action<int>? Picked;

    public bool IsOpen => Visibility == Visibility.Visible;

    /// The place of the lit line, or -1 when none is lit and Enter belongs to the field.
    public int Highlighted => _highlighted;

    /// How tall the list may be: the room above the field less the part of the feed that stays.
    public void Fit(double room) => _scroll.MaxHeight = Math.Clamp(room - FeedLeft, RowHeight * 3, MostHeight);

    /// The commands for what was typed after "/". The first line is lit only on a real match.
    public void ShowCommands(IReadOnlyList<SlashMatch> matches)
    {
        Begin();
        AddCommands("Claude Code", matches.Select((match, index) => (match, index)).Where(item => item.match.Command.Builtin));
        AddCommands(Strings.SlashGroupSkills, matches.Select((match, index) => (match, index)).Where(item => !item.match.Command.Builtin));
        End(matches.Count, SlashMenu.LightsFirst(matches) ? FirstShown() : -1);
    }

    /// The files for what was typed after "@": the file name with the match lit, its folder quiet.
    public void ShowFiles(IReadOnlyList<string> files, string query)
    {
        Begin();
        for (var index = 0; index < files.Count; index++)
        {
            var path = files[index];
            var cut = path.LastIndexOf('/');
            var (at, length) = FileMention.Lit(path, query);
            AddLine(index, Lit(FileMention.Name(path), at, length), cut < 0 ? "" : path[..cut]);
        }

        End(files.Count, files.Count > 0 ? 0 : -1);
    }

    public void Close() => Visibility = Visibility.Collapsed;

    /// Up and Down go round; PageUp and PageDown move by seven and stop at the ends.
    public void Move(int step)
    {
        if (_lines.Count == 0)
        {
            return;
        }

        var at = _lines.FindIndex(line => line.Index == _highlighted);
        var n = _lines.Count;
        var to = Math.Abs(step) == 1
            ? (at < 0 ? (step > 0 ? 0 : n - 1) : (at + step + n) % n)
            : Math.Clamp((at < 0 ? 0 : at) + step, 0, n - 1);
        Light(_lines[to].Index, scroll: true);
    }

    public void MovePage(int direction) => Move(direction * PageRows);

    public void MoveToEnd(bool last)
    {
        if (_lines.Count > 0)
        {
            Light(_lines[last ? ^1 : 0].Index, scroll: true);
        }
    }

    private void Begin()
    {
        _rows.Children.Clear();
        _lines.Clear();
    }

    private void End(int count, int lit)
    {
        _count = count;
        var opening = !IsOpen && count > 0;
        Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
        Light(lit, scroll: false);
        _scroll.ScrollToTop();
        if (opening)
        {
            Motion.PopIn(this, above: true);
        }
    }

    private int FirstShown() => _lines.Count > 0 ? _lines[0].Index : -1;

    private void AddCommands(string group, IEnumerable<(SlashMatch Match, int Index)> items)
    {
        var first = true;
        foreach (var (match, index) in items)
        {
            if (first)
            {
                var header = TerminalPanel.Mono(group, "Muted");
                header.FontSize = 11;
                header.Margin = new Thickness(8, 6, 8, 2);
                _rows.Children.Add(header);
                first = false;
            }

            AddLine(index, Lit("/" + match.Command.Name, match.MatchStart + 1, match.MatchLength), match.Command.Description);
        }
    }

    private static TextBlock Lit(string text, int start, int length)
    {
        var name = TerminalPanel.Mono("", "Muted");
        name.FontSize = 12.5;
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        if (start < 0 || length == 0)
        {
            name.Inlines.Add(new Run(text));
            return name;
        }

        name.Inlines.Add(new Run(text[..start]));
        name.Inlines.Add(new Run(text.Substring(start, length)) { Foreground = Tokens.Brush("Ink"), FontWeight = FontWeights.Medium });
        name.Inlines.Add(new Run(text[(start + length)..]));
        return name;
    }

    /// One 28 px line: the name in a column of its own, so the descriptions start in one place,
    /// and the description cut at the end of the line.
    private void AddLine(int index, TextBlock name, string description)
    {
        name.VerticalAlignment = VerticalAlignment.Center;

        var rest = TerminalPanel.Text(description, "TextSmall", "Muted", FontWeights.Normal);
        rest.TextTrimming = TextTrimming.CharacterEllipsis;
        rest.TextWrapping = TextWrapping.NoWrap;
        rest.Margin = new Thickness(14, 0, 0, 0);
        rest.VerticalAlignment = VerticalAlignment.Center;

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "Name" });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.Children.Add(name);
        Grid.SetColumn(rest, 1);
        grid.Children.Add(rest);

        var row = new Border
        {
            Height = RowHeight,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8, 0, 8, 0),
            Background = System.Windows.Media.Brushes.Transparent,
            Cursor = Cursors.Hand,
            Child = grid,
        };

        // Only a pointer that moves lights a line: the list changes under a still pointer as the
        // person types, and the line the keyboard chose must stay.
        row.MouseMove += (_, _) =>
        {
            if (index != _highlighted && PointerTrail.Moved(row))
            {
                Light(index, scroll: false);
            }
        };
        row.MouseLeftButtonUp += (_, _) => Picked?.Invoke(index);
        _lines.Add((row, index));
        _rows.Children.Add(row);
    }

    private void Light(int index, bool scroll)
    {
        _highlighted = index < _count ? index : -1;
        foreach (var (row, at) in _lines)
        {
            row.Background = at == _highlighted ? Tokens.Brush("Raised") : System.Windows.Media.Brushes.Transparent;
            if (scroll && at == _highlighted)
            {
                row.BringIntoView();
            }
        }
    }
}
