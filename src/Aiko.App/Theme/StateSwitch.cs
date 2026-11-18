using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace Aiko.App;

/// The switch of the new look: both values stand in a row and the chosen one is in brackets,
/// "Off [On]" (D-438, D-445). The brackets of the other value take their place too, so nothing moves.
internal sealed class StateSwitch : ToggleButton
{
    private readonly TextBlock _off;
    private readonly TextBlock _on;

    public StateSwitch(string off, string on, string name)
    {
        _off = Value(off);
        _on = Value(on);
        Content = new StackPanel { Orientation = Orientation.Horizontal, Children = { _off, _on } };
        _on.Margin = new Thickness(8, 0, 0, 0);
        Template = PlainTemplate();
        Cursor = Cursors.Hand;
        FocusVisualStyle = null;
        AutomationName(name);
        Checked += (_, _) => Paint();
        Unchecked += (_, _) => Paint();
        MouseEnter += (_, _) => Paint();
        MouseLeave += (_, _) => Paint();
        Paint();
    }

    private void AutomationName(string name) => System.Windows.Automation.AutomationProperties.SetName(this, name);

    private void Paint()
    {
        var on = IsChecked == true;
        Show(_on, on, "Positive");
        Show(_off, !on, "Ink");
    }

    private void Show(TextBlock value, bool chosen, string chosenTone)
    {
        var brush = Tokens.Brush(chosen ? chosenTone : IsMouseOver ? "Ink" : "LookQuiet");
        value.FontWeight = chosen ? FontWeights.Medium : FontWeights.Normal;
        var runs = value.Inlines.OfType<Run>().ToArray();
        runs[0].Foreground = chosen ? brush : Brushes.Transparent;
        runs[1].Foreground = brush;
        runs[2].Foreground = chosen ? brush : Brushes.Transparent;
    }

    private static TextBlock Value(string word) => new()
    {
        FontFamily = Tokens.Get<FontFamily>("Mono"),
        FontSize = Tokens.Get<double>("TextNumber"),
        VerticalAlignment = VerticalAlignment.Center,
        Inlines = { new Run("["), new Run(word), new Run("]") },
    };

    private static ControlTemplate PlainTemplate()
    {
        var ground = new FrameworkElementFactory(typeof(Border));
        ground.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        ground.SetValue(Border.PaddingProperty, new Thickness(2, 3, 2, 3));
        ground.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));
        return new ControlTemplate(typeof(ToggleButton)) { VisualTree = ground };
    }
}
