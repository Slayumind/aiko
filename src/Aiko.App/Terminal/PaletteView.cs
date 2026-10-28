using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// One line of a palette: a name with its match marked, where it is, and a place in a text when
/// only that matched. A quiet line is drawn dimmer, as a closed session is.
sealed record PaletteRow(string Id, IReadOnlyList<TextPart> Name, string Meta, IReadOnlyList<TextPart> Snippet, bool Quiet = false);

/// A group of lines under a title; an empty title draws no header.
sealed record PaletteGroup(string Title, IReadOnlyList<PaletteRow> Rows);

/// A palette of the terminal window (D-347, D-350): a field, the lines found in groups, and the
/// keys at the bottom. It sits in a popup, a window of its own, because nothing WPF draws in the
/// main window can cover a real terminal. The core finds; this only draws and listens to the keys.
[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.17763")]
sealed class PaletteView : Border
{
    public const double CardWidth = 520;
    private const double MostHeight = 380;

    private readonly Func<string, IReadOnlyList<PaletteGroup>> _find;
    private readonly TextBox _field = new();
    private readonly StackPanel _rows = new();
    private readonly ScrollViewer _scroll;
    private readonly List<(Border Line, PaletteRow Row)> _lines = [];
    private int _lit = -1;

    public PaletteView(string hint, string keys, Func<string, IReadOnlyList<PaletteGroup>> find)
    {
        _find = find;

        var lens = TerminalPanel.Line("M8,8 m-5.5,0 a5.5,5.5 0 1,0 11,0 a5.5,5.5 0 1,0 -11,0 M12,12 L16,16");
        lens.Margin = new Thickness(0, 0, 10, 0);
        DockPanel.SetDock(lens, Dock.Left);

        _field.FontFamily = Tokens.Get<FontFamily>("Sans");
        _field.FontSize = Tokens.Get<double>("TextName");
        _field.Foreground = Tokens.Brush("Ink");
        _field.CaretBrush = Tokens.Brush("Ink");
        _field.Background = Brushes.Transparent;
        _field.BorderThickness = new Thickness(0);
        _field.VerticalContentAlignment = VerticalAlignment.Center;
        var placeholder = TerminalPanel.Text(hint, "TextName", "Muted", FontWeights.Normal);
        placeholder.IsHitTestVisible = false;
        placeholder.Margin = new Thickness(2, 0, 0, 0);
        var typed = new Grid();
        typed.Children.Add(placeholder);
        typed.Children.Add(_field);

        var top = new DockPanel { Margin = new Thickness(14, 10, 14, 10) };
        top.Children.Add(lens);
        top.Children.Add(typed);

        _scroll = new ScrollViewer
        {
            MaxHeight = MostHeight,
            Focusable = false,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(6, 4, 6, 4),
            Content = _rows,
        };

        var keyLine = TerminalPanel.Mono(keys, "Muted");
        keyLine.Margin = new Thickness(14, 8, 14, 9);
        // Russian keys are longer than the card: they go on, never cut at its edge.
        keyLine.TextWrapping = TextWrapping.Wrap;

        var column = new DockPanel();
        DockPanel.SetDock(top, Dock.Top);
        column.Children.Add(top);
        var under = Hairline();
        DockPanel.SetDock(under, Dock.Top);
        column.Children.Add(under);
        var bottom = new StackPanel();
        bottom.Children.Add(Hairline());
        bottom.Children.Add(keyLine);
        DockPanel.SetDock(bottom, Dock.Bottom);
        column.Children.Add(bottom);
        column.Children.Add(_scroll);

        Width = CardWidth;
        // Room for the shadow inside the popup.
        Margin = new Thickness(16, 0, 16, 16);
        Background = Tokens.Brush("Surface");
        BorderBrush = Tokens.Brush("Hairline");
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(12);
        Effect = Tokens.Get<System.Windows.Media.Effects.Effect>("CardShadow");
        Child = column;

        _field.TextChanged += (_, _) =>
        {
            placeholder.Visibility = _field.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            Fill();
        };
        _field.PreviewKeyDown += OnKey;
        Loaded += (_, _) => Keyboard.Focus(_field);
        Fill();
    }

    /// A line was chosen: its id, and whether Ctrl was held.
    public event Action<string, bool>? Picked;

    /// Esc.
    public event Action? Closed;

    public void FocusField() => Keyboard.Focus(_field);

    /// More to find came in, such as conversations read in the background: the same query again.
    public void Refresh() => Fill();

    private void OnKey(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                e.Handled = true;
                Closed?.Invoke();
                break;
            case Key.Down or Key.Up when _lines.Count > 0:
                e.Handled = true;
                Light((_lit + (e.Key == Key.Down ? 1 : -1) + _lines.Count) % _lines.Count, scroll: true);
                break;
            case Key.Enter:
                e.Handled = true;
                Pick(_lit, Keyboard.Modifiers.HasFlag(ModifierKeys.Control));
                break;
        }
    }

    private void Pick(int index, bool control)
    {
        if (index >= 0 && index < _lines.Count)
        {
            Picked?.Invoke(_lines[index].Row.Id, control);
        }
    }

    private void Fill()
    {
        var groups = _find(_field.Text);
        _rows.Children.Clear();
        _lines.Clear();
        foreach (var group in groups.Where(group => group.Rows.Count > 0))
        {
            if (group.Title.Length > 0)
            {
                var header = TerminalPanel.Mono(group.Title, "Muted");
                header.Margin = new Thickness(8, 8, 8, 4);
                _rows.Children.Add(header);
            }

            foreach (var row in group.Rows)
            {
                AddRow(row);
            }
        }

        Light(_lines.Count > 0 ? 0 : -1, scroll: false);
        _scroll.ScrollToTop();
    }

    private void AddRow(PaletteRow row)
    {
        var index = _lines.Count;
        var name = Line(row.Name, "TextName", row.Quiet ? "Muted" : "Ink");
        var top = new DockPanel();
        if (row.Meta.Length > 0)
        {
            var meta = TerminalPanel.Text(row.Meta, "TextSmall", "Muted", FontWeights.Normal);
            meta.Margin = new Thickness(12, 0, 0, 0);
            DockPanel.SetDock(meta, Dock.Right);
            top.Children.Add(meta);
        }

        top.Children.Add(name);

        var lines = new StackPanel();
        lines.Children.Add(top);
        if (row.Snippet.Count > 0)
        {
            var snippet = Line(row.Snippet, "TextSmall", "Muted");
            snippet.Margin = new Thickness(0, 3, 0, 0);
            lines.Children.Add(snippet);
        }

        var line = new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8, 7, 8, 7),
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
            Child = lines,
        };

        // Only a pointer that moves lights a line: the list changes under a still pointer as the
        // person types.
        line.MouseMove += (_, _) =>
        {
            if (index != _lit && PointerTrail.Moved(line))
            {
                Light(index, scroll: false);
            }
        };
        line.MouseLeftButtonUp += (_, _) => Pick(index, Keyboard.Modifiers.HasFlag(ModifierKeys.Control));
        _lines.Add((line, row));
        _rows.Children.Add(line);
    }

    private static TextBlock Line(IReadOnlyList<TextPart> parts, string size, string tone)
    {
        var line = TerminalPanel.Text("", size, tone, FontWeights.Normal);
        line.TextTrimming = TextTrimming.CharacterEllipsis;
        foreach (var part in parts)
        {
            line.Inlines.Add(part.Match
                ? new Run(part.Text) { Foreground = Tokens.Brush("Ink"), TextDecorations = TextDecorations.Underline }
                : new Run(part.Text));
        }

        return line;
    }

    private void Light(int index, bool scroll)
    {
        _lit = index;
        for (var at = 0; at < _lines.Count; at++)
        {
            _lines[at].Line.Background = at == index ? Tokens.Brush("Raised") : Brushes.Transparent;
            if (scroll && at == index)
            {
                _lines[at].Line.BringIntoView();
            }
        }
    }

    private static Border Hairline() => new() { Height = 1, Background = Tokens.Brush("Hairline") };
}
