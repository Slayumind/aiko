using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Aiko.Core;
using Aiko.Core.Terminal;

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
                    FontFamily = Tokens.Mono,
                    FontSize = Tokens.Get<double>("TextSmall"),
                    Foreground = Tokens.Brush(tone),
                    VerticalAlignment = VerticalAlignment.Center,
                },
            },
        };
    }

    public static FrameworkElement WaitingSign() => Marks.Pause();

    public static FrameworkElement DoneSign() => SiteIcons.Draw(SiteIcons.Check, 10, "Muted");

    public static FrameworkElement StoppedSign() => Marks.Line("M0,0 L8,8 M8,0 L0,8", "Destructive");

    public static FrameworkElement WorkingSign() => SiteIcons.Draw(SiteIcons.Scrubber, 8, "Positive");

    /// A sign with a number after it, for counts of sessions on the island and on the tabs.
    public static (StackPanel Row, TextBlock Number) Counted(FrameworkElement sign)
    {
        sign.HorizontalAlignment = HorizontalAlignment.Center;
        sign.VerticalAlignment = VerticalAlignment.Center;
        var number = new TextBlock
        {
            FontFamily = Tokens.Mono,
            FontSize = Tokens.Get<double>("TextTiny"),
            Foreground = Tokens.Brush("Ink"),
            MinWidth = 7,
            Margin = new Thickness(4, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(new Grid { Width = 12, Height = 12, Children = { sign } });
        row.Children.Add(number);
        return (row, number);
    }

    /// The mark of a session; a session the person has seen has none.
    public static FrameworkElement? Of(RowState state) => state switch
    {
        RowState.Working => Create(StatusLook.Working, Strings.TerminalWorking),
        RowState.Waiting => Create(StatusLook.Waiting, Strings.TerminalWaiting),
        RowState.Done => Create(StatusLook.Done, Strings.TerminalDone),
        RowState.Asleep => Create(StatusLook.Asleep, Strings.TerminalAsleep),
        RowState.Stopped => Create(StatusLook.Stopped, Strings.TerminalStopped),
        _ => null,
    };

    private static string Tone(StatusLook look) => look switch
    {
        StatusLook.Working => "Positive",
        StatusLook.Waiting => "Caution",
        StatusLook.Stopped => "Destructive",
        StatusLook.LimitNearEnd => "LimitStrong",
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
        FontFamily = Tokens.Mono,
        FontSize = Tokens.Get<double>("TextSmall"),
        Foreground = Tokens.Brush(tone),
        VerticalAlignment = VerticalAlignment.Center,
    };
}
