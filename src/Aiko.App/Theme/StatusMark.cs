using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Aiko.App;

/// What a session or a limit is doing, in the words of the new look.
internal enum StatusLook
{
    Working,
    Waiting,
    Done,
    Asleep,
    Stopped,
    LimitNearEnd,
}

/// A state is always a sign and a word, never a colour alone (D-441). Green, orange and red belong
/// to sessions, violet to limits (D-440).
internal static class StatusMark
{
    public static FrameworkElement Create(StatusLook look, string word)
    {
        var tone = Tone(look);
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children =
            {
                Sign(look, tone),
                new TextBlock
                {
                    Text = word,
                    Margin = new Thickness(5, 0, 0, 0),
                    FontFamily = Tokens.Get<FontFamily>("Mono"),
                    FontSize = Tokens.Get<double>("TextSmall"),
                    Foreground = Tokens.Brush(tone),
                    VerticalAlignment = VerticalAlignment.Center,
                },
            },
        };
    }

    private static string Tone(StatusLook look) => look switch
    {
        StatusLook.Working => "Positive",
        StatusLook.Waiting => "Caution",
        StatusLook.Stopped => "Destructive",
        StatusLook.LimitNearEnd => "LookLimit",
        _ => "Muted",
    };

    private static FrameworkElement Sign(StatusLook look, string tone) => look switch
    {
        StatusLook.Working => SiteIcons.Draw(SiteIcons.Scrubber, 9, tone),
        StatusLook.Done => SiteIcons.Draw(SiteIcons.Check, 12, tone),
        StatusLook.Stopped => SiteIcons.Draw(SiteIcons.Cross, 11, tone),
        StatusLook.LimitNearEnd => SiteIcons.Draw(SiteIcons.FireFlame, 13, tone),
        StatusLook.Waiting => Letters("||", tone),
        _ => Letters("–", tone),
    };

    private static TextBlock Letters(string text, string tone) => new()
    {
        Text = text,
        FontFamily = Tokens.Get<FontFamily>("Mono"),
        FontSize = Tokens.Get<double>("TextSmall"),
        Foreground = Tokens.Brush(tone),
        VerticalAlignment = VerticalAlignment.Center,
    };
}
