using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Aiko.App;

namespace Aiko.Checks;

/// --snapshot-look out.png [zoom]: every part of the new look on one sheet, at a window zoom of
/// 100, 115 or 130. It is drawn one to one, so the lines on the picture are the lines of the screen.
static class LookSheet
{
    public static void Write(string path, double zoomPercent)
    {
        var sheet = new StackPanel { Width = 520, Margin = new Thickness(16) };
        sheet.Children.Add(new TitledFrame
        {
            Height = 96,
            Title = Label("Голос в редактор", "Ink"),
            Aside = StatusMark.Create(StatusLook.Waiting, "ждёт"),
            Content = Label("> добавь голосовой ввод в редактор", "Ink"),
        });
        sheet.Children.Add(Gap());
        foreach (var (value, tone) in new[] { (0.42, "Ink"), (0.78, "LimitSoft"), (0.93, "LimitStrong") })
        {
            sheet.Children.Add(new LimitBar { Value = value, Fill = Tokens.Brush(tone), Margin = new Thickness(0, 0, 0, 8) });
        }

        sheet.Children.Add(Gap());
        var marks = new WrapPanel();
        foreach (var (look, word) in new[]
                 {
                     (StatusLook.Working, "работает"),
                     (StatusLook.Waiting, "ждёт"),
                     (StatusLook.Done, "готово"),
                     (StatusLook.Asleep, "спит"),
                     (StatusLook.Stopped, "остановилась"),
                     (StatusLook.LimitNearEnd, "лимит 93%"),
                 })
        {
            var mark = StatusMark.Create(look, word);
            mark.Margin = new Thickness(0, 0, 16, 8);
            marks.Children.Add(mark);
        }

        sheet.Children.Add(marks);
        sheet.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/Aiko.App;component/Theme/Controls.xaml", UriKind.Absolute),
        });
        var style = (Style)sheet.FindResource("Switch");
        sheet.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children =
            {
                new ToggleButton { Style = style, IsChecked = true, Margin = new Thickness(0, 0, 24, 0) },
                new ToggleButton { Style = style, IsChecked = false },
            },
        });

        var zoom = zoomPercent / 100;
        Snapshot.Write(new Border { Child = sheet, LayoutTransform = new ScaleTransform(zoom, zoom) }, path, scale: 1);
    }

    private static TextBlock Label(string text, string tone) => new()
    {
        Text = text,
        FontFamily = Tokens.Mono,
        FontSize = Tokens.Get<double>("TextBody"),
        Foreground = Tokens.Brush(tone),
        Background = Brushes.Transparent,
    };

    private static FrameworkElement Gap() => new Border { Height = 12 };
}
