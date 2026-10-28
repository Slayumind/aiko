using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// How full the context of the conversation is, as a word at the end of the line under the field:
/// "context 82%", quiet, then orange, then red (ContextGauge, D-345). It is a word on purpose:
/// a ring with a number means the limits everywhere in Aiko.
sealed class ContextWord : TextBlock
{
    private int? _shown = -1;

    public ContextWord()
    {
        FontFamily = Tokens.Get<FontFamily>("Mono");
        FontSize = Tokens.Get<double>("TextTiny");
        VerticalAlignment = VerticalAlignment.Center;
        Visibility = Visibility.Collapsed;
        ToolTip = Strings.TerminalContextTip;
    }

    public void Show(int? percent)
    {
        if (percent == _shown)
        {
            return;
        }

        _shown = percent;
        if (percent is not { } used)
        {
            Visibility = Visibility.Collapsed;
            return;
        }

        Visibility = Visibility.Visible;
        Text = string.Format(CultureInfo.CurrentCulture, Strings.TerminalContextShare, used);
        Foreground = Tokens.Brush(ContextGauge.Level(used) switch
        {
            ContextLevel.Hot => "Destructive",
            ContextLevel.Warm => "Caution",
            _ => "Muted",
        });
    }
}
