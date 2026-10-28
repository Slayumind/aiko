using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace Aiko.App;

/// A short list over the terminal, right above the editor: the files of "@" and the paths of Tab
/// (D-369). It is a popup, a window of its own, because nothing WPF draws in the window can cover a
/// real terminal. The keys stay with the editor, which moves the light and picks.
[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.17763")]
sealed class ChoicePopup
{
    private const int MostRows = 8;

    private readonly Popup _popup = new() { StaysOpen = true, AllowsTransparency = true, Placement = PlacementMode.Top };
    private readonly StackPanel _rows = new();
    private readonly List<Border> _lines = [];
    private IReadOnlyList<string> _choices = [];
    private int _lit;

    public ChoicePopup(UIElement target)
    {
        _popup.PlacementTarget = target;
        _popup.Child = new Border
        {
            MinWidth = 240,
            MaxWidth = 520,
            Margin = new Thickness(12, 12, 12, 4),
            Padding = new Thickness(4),
            CornerRadius = new CornerRadius(10),
            Background = Tokens.Brush("Surface"),
            BorderBrush = Tokens.Brush("InputLine"),
            BorderThickness = new Thickness(1),
            Effect = Tokens.Get<System.Windows.Media.Effects.Effect>("CardShadow"),
            Child = _rows,
        };
    }

    /// A choice was made with the mouse: its index.
    public event Action<int>? Clicked;

    public bool IsOpen => _popup.IsOpen;

    public int Lit => _lit;

    /// The choices, each with the part to mark: start and length, or a length of 0 for none.
    public void Show(IReadOnlyList<(string Text, int Start, int Length)> choices, double left)
    {
        _choices = [.. choices.Select(choice => choice.Text)];
        _rows.Children.Clear();
        _lines.Clear();
        foreach (var (text, start, length) in choices.Take(MostRows))
        {
            var index = _lines.Count;
            var line = TerminalPanel.Text("", "TextNumber", "Muted", FontWeights.Normal);
            line.FontFamily = Tokens.Get<FontFamily>("Mono");
            line.TextTrimming = TextTrimming.CharacterEllipsis;
            line.Inlines.Add(new Run(text[..start]));
            line.Inlines.Add(new Run(text.Substring(start, length)) { Foreground = Tokens.Brush("Ink") });
            line.Inlines.Add(new Run(text[(start + length)..]));
            var row = new Border { CornerRadius = new CornerRadius(6), Padding = new Thickness(8, 5, 8, 5), Cursor = Cursors.Hand, Child = line };
            row.MouseLeftButtonUp += (_, _) => Clicked?.Invoke(index);
            _lines.Add(row);
            _rows.Children.Add(row);
        }

        _popup.HorizontalOffset = left;
        Light(0);
        _popup.IsOpen = _lines.Count > 0;
    }

    public void Close() => _popup.IsOpen = false;

    public void Move(int step) => Light((_lit + step + _lines.Count) % Math.Max(1, _lines.Count));

    private void Light(int index)
    {
        _lit = index;
        for (var at = 0; at < _lines.Count; at++)
        {
            _lines[at].Background = at == index ? Tokens.Brush("Raised") : Brushes.Transparent;
        }
    }
}
