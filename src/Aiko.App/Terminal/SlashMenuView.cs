using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// The menu of "/" over the field, as wide as the field: one line per command, the built-in
/// commands of Claude Code first, then the skills, and the keys at the bottom (AIKO-330, way 1
/// in the artifact "Aiko"). The names and descriptions are the ones Claude Code gives, in English.
sealed class SlashMenuView : Border
{
    /// Rows shown before the list scrolls.
    private const double ListHeight = 300;

    private readonly StackPanel _rows = new();
    private readonly ScrollViewer _scroll;
    private readonly List<(Border Row, SlashCommand Command)> _lines = [];
    private int _highlighted;

    public SlashMenuView()
    {
        Background = Tokens.Brush("Surface");
        BorderBrush = Tokens.Brush("Hairline");
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(12);
        Padding = new Thickness(4);
        Effect = Tokens.Get<System.Windows.Media.Effects.Effect>("CardShadow");
        Visibility = Visibility.Collapsed;

        Grid.SetIsSharedSizeScope(_rows, true);
        _scroll = new ScrollViewer { MaxHeight = ListHeight, Focusable = false, Content = _rows };

        var keys = TerminalPanel.Mono(Strings.SlashKeys, "Muted");
        keys.FontSize = 11;
        var foot = new Border
        {
            BorderBrush = Tokens.Brush("Hairline"),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Margin = new Thickness(0, 4, 0, 0),
            Padding = new Thickness(10, 6, 10, 4),
            Child = keys,
        };

        var column = new StackPanel();
        column.Children.Add(_scroll);
        column.Children.Add(foot);
        Child = column;
    }

    /// A line was chosen with the mouse.
    public event Action<SlashCommand>? Picked;

    public bool IsOpen => Visibility == Visibility.Visible;

    public SlashCommand? Highlighted => _lines.Count > 0 ? _lines[_highlighted].Command : null;

    /// Shows the matches, or closes the menu when there are none. The first line is lit.
    public void Show(IReadOnlyList<SlashMatch> matches)
    {
        _rows.Children.Clear();
        _lines.Clear();
        _highlighted = 0;

        AddGroup("Claude Code", matches.Where(match => match.Command.Builtin));
        AddGroup(Strings.SlashGroupSkills, matches.Where(match => !match.Command.Builtin));

        Visibility = _lines.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        Light();
        _scroll.ScrollToTop();
    }

    public void Close() => Visibility = Visibility.Collapsed;

    /// The arrows move the light; at an end it stays there.
    public void Move(int step)
    {
        if (_lines.Count == 0)
        {
            return;
        }

        _highlighted = Math.Clamp(_highlighted + step, 0, _lines.Count - 1);
        Light();
        _lines[_highlighted].Row.BringIntoView();
    }

    private void AddGroup(string name, IEnumerable<SlashMatch> matches)
    {
        var first = true;
        foreach (var match in matches)
        {
            if (first)
            {
                var header = TerminalPanel.Mono(name, "Muted");
                header.FontSize = 11;
                header.Margin = new Thickness(10, 8, 10, 2);
                _rows.Children.Add(header);
                first = false;
            }

            var row = Line(match);
            _lines.Add((row, match.Command));
            _rows.Children.Add(row);
        }
    }

    /// The name with the typed part lit, then the description, cut at the end of the line.
    private Border Line(SlashMatch match)
    {
        var name = TerminalPanel.Mono("", "Muted");
        name.FontSize = 12.5;
        var text = "/" + match.Command.Name;
        var start = match.MatchStart + 1;
        name.Inlines.Add(new Run(text[..start]));
        if (match.MatchLength > 0)
        {
            name.Inlines.Add(new Run(text.Substring(start, match.MatchLength)) { Foreground = Tokens.Brush("Ink"), FontWeight = FontWeights.Medium });
        }

        name.Inlines.Add(new Run(text[(start + match.MatchLength)..]));

        var description = TerminalPanel.Text(match.Command.Description, "TextSmall", "Muted", FontWeights.Normal);
        description.TextTrimming = TextTrimming.CharacterEllipsis;
        description.TextWrapping = TextWrapping.NoWrap;
        description.Margin = new Thickness(14, 0, 0, 0);
        description.VerticalAlignment = VerticalAlignment.Center;

        var grid = new Grid();
        // All names share one width, so the descriptions start in one column.
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "Name" });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.Children.Add(name);
        Grid.SetColumn(description, 1);
        grid.Children.Add(description);

        var row = new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 5, 10, 5),
            Background = System.Windows.Media.Brushes.Transparent,
            Cursor = Cursors.Hand,
            Child = grid,
        };
        var command = match.Command;
        row.MouseEnter += (_, _) =>
        {
            _highlighted = _lines.FindIndex(line => line.Row == row);
            Light();
        };
        row.MouseLeftButtonUp += (_, _) => Picked?.Invoke(command);
        return row;
    }

    private void Light()
    {
        for (var index = 0; index < _lines.Count; index++)
        {
            _lines[index].Row.Background = index == _highlighted ? Tokens.Brush("Raised") : System.Windows.Media.Brushes.Transparent;
        }
    }
}
