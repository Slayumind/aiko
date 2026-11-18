using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Aiko.App;

/// The small parts every code-built surface uses: text in the faces of the tokens, a field, a dot
/// and the buttons, with one hover, press and focus for all of them. They lived in TerminalPanel,
/// and five other files reached into it for them.
internal static class Widgets
{
    internal static void Field(TextBox box)
    {
        box.FontFamily = Tokens.Get<FontFamily>("Mono");
        box.FontSize = Tokens.Get<double>("TextName");
        box.Foreground = Tokens.Brush("Ink");
        box.CaretBrush = Tokens.Brush("Ink");
        box.Background = Tokens.Brush("Background");
        box.BorderBrush = Tokens.Brush("Hairline");
        box.BorderThickness = new Thickness(1);
        box.Padding = new Thickness(9, 6, 9, 6);
        box.Resources[typeof(Border)] = new Style(typeof(Border)) { Setters = { new Setter(Border.CornerRadiusProperty, new CornerRadius(0)) } };
    }

    internal static TextBlock Text(string text, string size, string tone, FontWeight weight) => new()
    {
        Text = text,
        FontFamily = Tokens.Get<FontFamily>("Mono"),
        FontSize = Tokens.Get<double>(size),
        FontWeight = weight,
        Foreground = Tokens.Brush(tone),
        VerticalAlignment = VerticalAlignment.Center,
    };

    internal static TextBlock Mono(string text, string tone) => new()
    {
        Text = text,
        FontFamily = Tokens.Get<FontFamily>("Mono"),
        FontSize = Tokens.Get<double>("TextTiny"),
        Foreground = Tokens.Brush(tone),
        VerticalAlignment = VerticalAlignment.Center,
    };

    internal static FrameworkElement Dot(bool filled) => new Ellipse
    {
        Width = 6,
        Height = 6,
        Fill = filled ? Tokens.Brush("Muted") : Brushes.Transparent,
        Stroke = Tokens.Brush("Muted"),
        StrokeThickness = 1,
        VerticalAlignment = VerticalAlignment.Center,
    };

    /// One button for the whole window: every button is outlined. The main action has the brighter
    /// line, the ghost one has none.
    /// aside stands at the right end of the button, such as the key of its action.
    internal static Button Flat(string text, Action click, bool ghost, bool main = false, bool danger = false, bool plus = false, UIElement? aside = null, string? mark = null)
    {
        var row = new DockPanel();
        var ink = danger ? "Destructive" : ghost ? "Muted" : "Ink";
        if (aside is not null)
        {
            DockPanel.SetDock(aside, Dock.Right);
            row.Children.Add(aside);
        }

        if (plus || mark is not null)
        {
            var sign = Marks.Line(mark ?? "M5,0 L5,10 M0,5 L10,5", ink);
            sign.Margin = new Thickness(0, 0, 8, 0);
            DockPanel.SetDock(sign, Dock.Left);
            row.Children.Add(sign);
        }

        row.Children.Add(Text(text, "TextNumber", ink, main ? FontWeights.Medium : FontWeights.Normal));

        var hover = new Border { CornerRadius = new CornerRadius(0), Background = Tokens.Brush("HoverLayer"), Opacity = 0 };
        var face = new Grid();
        face.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(0),
            Background = Brushes.Transparent,
            BorderBrush = ghost ? Brushes.Transparent : Tokens.Brush(danger ? "Destructive" : main ? "Muted" : "Hairline"),
            BorderThickness = new Thickness(1),
        });
        face.Children.Add(hover);
        face.Children.Add(new Border { Padding = new Thickness(12, 0, 12, 0), Child = row, VerticalAlignment = VerticalAlignment.Center });

        var button = new Button { Content = face, Height = 32, Cursor = Cursors.Hand, Template = Bare(), FocusVisualStyle = FocusRing() };
        button.MouseEnter += (_, _) => hover.BeginAnimation(UIElement.OpacityProperty, Motion.To(1, Motion.Or0(Motion.Hover), Motion.Standard));
        button.MouseLeave += (_, _) => hover.BeginAnimation(UIElement.OpacityProperty, Motion.To(0, Motion.Or0(Motion.Hover), Motion.Standard));
        button.Click += (_, _) => click();
        Motion.SetPress(button, true);
        WindowChromeHit(button);
        return button;
    }

    /// A button that is only an icon: 28 by 28, a light layer on hover, the same press and focus as
    /// the other buttons (D-332).
    internal static Button Icon(UIElement icon)
    {
        var hover = new Border { CornerRadius = new CornerRadius(0), Background = Tokens.Brush("HoverLayer"), Opacity = 0 };
        var face = new Grid { Width = 28, Height = 28 };
        face.Children.Add(hover);
        // In the middle of its hover: a line left to stretch draws from the left edge.
        if (icon is FrameworkElement drawn)
        {
            drawn.HorizontalAlignment = HorizontalAlignment.Center;
            drawn.VerticalAlignment = VerticalAlignment.Center;
        }

        face.Children.Add(icon);

        var button = new Button { Content = face, Cursor = Cursors.Hand, Template = Bare(), FocusVisualStyle = FocusRing() };
        button.MouseEnter += (_, _) => hover.BeginAnimation(UIElement.OpacityProperty, Motion.To(1, Motion.Or0(Motion.Hover), Motion.Standard));
        button.MouseLeave += (_, _) => hover.BeginAnimation(UIElement.OpacityProperty, Motion.To(0, Motion.Or0(Motion.Hover), Motion.Standard));
        Motion.SetPress(button, true);
        WindowChromeHit(button);
        return button;
    }

    internal static ControlTemplate Bare()
    {
        var template = new ControlTemplate(typeof(Button));
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, new TemplateBindingExtension(Control.HorizontalContentAlignmentProperty));
        template.VisualTree = presenter;
        return template;
    }

    /// The focus of the keyboard: an outline of 2 px, as in the settings (D-308).
    internal static Style FocusRing()
    {
        var ring = new FrameworkElementFactory(typeof(Border));
        ring.SetValue(Border.BorderBrushProperty, Tokens.Brush("Ink"));
        ring.SetValue(Border.BorderThicknessProperty, new Thickness(2));
        ring.SetValue(Border.CornerRadiusProperty, new CornerRadius(0));
        ring.SetValue(FrameworkElement.MarginProperty, new Thickness(-3));
        return new Style { Setters = { new Setter(Control.TemplateProperty, new ControlTemplate { VisualTree = ring }) } };
    }

    /// The title bar belongs to the window for dragging; what can be pressed in it says so.
    internal static void WindowChromeHit(IInputElement element) =>
        System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(element, true);
}
