using System.Runtime.InteropServices;
using System.Windows;

namespace Aiko.App;

/// The notices on screen (D-377): at most three, the newest next to Aiko. Next to the tray they
/// grow upwards, next to an island at the top of the screen downwards. While "Do not disturb", a
/// presentation or a full-screen app is on, nothing comes up and nothing sounds.
[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.17763")]
sealed class NoticeStack
{
    private const int Most = 3;
    private const double Gap = 6;

    /// Where Aiko is now, in units of WPF: the tray icon or the island; null when neither is known.
    private readonly Func<Rect?> _anchor;
    private readonly List<NoticeWindow> _shown = [];

    public NoticeStack(Func<Rect?> anchor) => _anchor = anchor;

    /// Shows a notice; open runs when it is clicked. False when the system asks for quiet.
    public bool Show(NoticeLine notice, Action open)
    {
        if (!SystemAcceptsNotices())
        {
            return false;
        }

        if (_shown.Count == Most)
        {
            _shown[^1].Go();
        }

        var window = new NoticeWindow(notice);
        window.Clicked += open;
        window.Gone += gone =>
        {
            _shown.Remove(gone);
            Place(moving: true);
        };

        // Shown see through first: the size is known only after layout.
        window.Opacity = 0;
        window.Show();
        window.UpdateLayout();
        _shown.Insert(0, window);
        var upwards = Place(moving: true);
        window.Opacity = 1;
        window.Arrive(upwards ? 1 : -1);
        return true;
    }

    /// Whether the system takes notices now (SHQueryUserNotificationState). Also asked before the
    /// sound of a session the window has in front.
    public static bool SystemAcceptsNotices() =>
        SHQueryUserNotificationState(out var state) != 0 || state == AcceptsNotifications;

    /// Puts every notice in its place, the newest nearest to Aiko. Returns whether they grow upwards.
    private bool Place(bool moving)
    {
        var work = SystemParameters.WorkArea;
        var anchor = _anchor() ?? new Rect(work.Right - 24, work.Bottom, 0, 0);
        var upwards = anchor.Top - work.Top > work.Bottom - anchor.Bottom;
        var edge = upwards ? anchor.Top : anchor.Bottom;

        var offset = 0.0;
        foreach (var window in _shown)
        {
            var width = window.ActualWidth;
            var height = window.ActualHeight;
            var visible = height - (2 * NoticeWindow.Air);
            window.Left = Math.Clamp(anchor.Left + (anchor.Width / 2) - (width / 2), work.Left, Math.Max(work.Left, work.Right - width));
            var top = upwards
                ? edge - Gap - offset - visible - NoticeWindow.Air
                : edge + Gap + offset - NoticeWindow.Air;
            top = Math.Clamp(top, work.Top - NoticeWindow.Air, Math.Max(work.Top, work.Bottom - height + NoticeWindow.Air));
            if (moving && window.IsVisible && window != _shown[0])
            {
                window.MoveTo(top);
            }
            else
            {
                window.Top = top;
            }

            offset += visible + Gap;
        }

        return upwards;
    }

    private const int AcceptsNotifications = 5;

    [DllImport("shell32.dll")]
    private static extern int SHQueryUserNotificationState(out int state);
}
