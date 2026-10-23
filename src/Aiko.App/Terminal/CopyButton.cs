using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Aiko.App;

/// The copy button of the terminal (D-332): the copy icon of the site set; after a click the green
/// check for two seconds, and a second click in that time copies nothing again.
static class CopyButton
{
    private static readonly TimeSpan Shown = TimeSpan.FromSeconds(2);

    public static Button For(Func<string> text)
    {
        var copy = SiteIcons.Draw(SiteIcons.Copy, 14, "Muted");
        var done = SiteIcons.Draw(SiteIcons.Check, 14, "Positive");
        done.Visibility = Visibility.Collapsed;

        var face = new Grid();
        face.Children.Add(copy);
        face.Children.Add(done);

        var button = TerminalPanel.Icon(face);
        button.ToolTip = Strings.Copy;
        button.Click += (_, _) =>
        {
            if (done.Visibility == Visibility.Visible)
            {
                return;
            }

            Clipboard.SetText(text());
            copy.Visibility = Visibility.Collapsed;
            done.Visibility = Visibility.Visible;
            button.ToolTip = Strings.Copied;

            var back = new DispatcherTimer { Interval = Shown };
            back.Tick += (_, _) =>
            {
                back.Stop();
                copy.Visibility = Visibility.Visible;
                done.Visibility = Visibility.Collapsed;
                button.ToolTip = Strings.Copy;
            };
            back.Start();
        };
        return button;
    }

    /// The button shows while the mouse is over the part it copies, as in the Office and in Zed;
    /// it stays while its check is on.
    public static void ShowOnHover(FrameworkElement area, Button button)
    {
        button.Opacity = 0;
        area.MouseEnter += (_, _) => button.BeginAnimation(UIElement.OpacityProperty, Motion.To(1, Motion.Hover, Motion.Standard));
        area.MouseLeave += (_, _) => button.BeginAnimation(UIElement.OpacityProperty, Motion.To(0, Motion.Hover, Motion.Standard));
        button.GotKeyboardFocus += (_, _) => button.BeginAnimation(UIElement.OpacityProperty, null);
        button.GotKeyboardFocus += (_, _) => button.Opacity = 1;
    }
}
