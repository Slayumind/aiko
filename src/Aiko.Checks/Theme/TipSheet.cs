using System.Windows;
using System.Windows.Controls;
using Aiko.App;
using Aiko.Core;

namespace Aiko.Checks;

/// --snapshot-tip: the tip of the windows in both languages, short and long, in the style of
/// Theme/ToolTip.xaml. A tip opens only under a resting mouse, so this is how it gets looked at.
static class TipSheet
{
    public static void Write(string path)
    {
        var sheet = new StackPanel { Margin = new Thickness(16) };
        sheet.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/Aiko.App;component/Theme/ToolTip.xaml", UriKind.Absolute),
        });

        var before = Strings.Language;
        foreach (var language in new[] { StringLanguage.English, StringLanguage.Russian })
        {
            Strings.Language = language;
            foreach (var words in new[] { Strings.Settings, Strings.TerminalFindConversation, Strings.TerminalFolderGone })
            {
                sheet.Children.Add(new ContentControl { Content = words, Template = (ControlTemplate)sheet.FindResource("TipTemplate"), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 8) });
            }
        }

        Strings.Language = before;
        Snapshot.Write(sheet, path);
    }
}
