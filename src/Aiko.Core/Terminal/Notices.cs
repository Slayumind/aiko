namespace Aiko.Core.Terminal;

/// What a notice is about (D-348, D-377).
public enum NoticeKind
{
    /// A session asks the person something.
    Waiting,

    /// A session ended a turn it was working on.
    Done,

    /// A session stopped by itself.
    Stopped,

    /// A limit of an environment went over 90 percent.
    Limit,
}

/// How a notice reaches the person.
public enum NoticeWay
{
    None,

    /// Only the sound: the terminal window is in front, and the list already shows the mark.
    Sound,

    /// The own one-line window of Aiko (D-377).
    Window,
    WindowWithSound,
}

/// Which notices the person wants (D-375): one tick per kind and one for the sound.
public sealed record NoticeChoice(bool Waiting, bool Done, bool Stopped, bool Limit, bool Sound)
{
    public static NoticeChoice From(AppSettings settings) =>
        new(settings.NotifyWaiting, settings.NotifyDone, settings.NotifyStopped, settings.NotifyLimit, settings.NotifySound);

    public bool Wants(NoticeKind kind) => kind switch
    {
        NoticeKind.Waiting => Waiting,
        NoticeKind.Done => Done,
        NoticeKind.Stopped => Stopped,
        _ => Limit,
    };
}

/// A window of a limit that went over the line: which environment, which window, and the percent.
public sealed record LimitCrossing(string Environment, LimitKind Kind, int Percent);

/// When a change brings a notice and how it reaches the person. Rows: spec/cases/notices.
public static class Notices
{
    /// The line of a limit. The tray face gets tired at the same line (TrayMood).
    public const int LimitLine = TrayMood.TiredAt;

    /// The notice a change of a session brings, or null. before is null for a terminal seen for the
    /// first time: a turn that ended before Aiko saw it is not news.
    public static NoticeKind? ForSession(TerminalActivity? before, TerminalActivity now)
    {
        if (before == now)
        {
            return null;
        }

        return now switch
        {
            TerminalActivity.Waiting => NoticeKind.Waiting,
            TerminalActivity.Done when before is TerminalActivity.Working or TerminalActivity.Waiting => NoticeKind.Done,
            TerminalActivity.Exited when before is not null => NoticeKind.Stopped,
            _ => null,
        };
    }

    /// inFront: the terminal window is on screen and has the keyboard. open: the notice is about the
    /// session open in it. A limit is not about a session and shows in front too: nothing else in the
    /// window says it crossed the line.
    public static NoticeWay Route(NoticeKind kind, NoticeChoice choice, bool inFront, bool open)
    {
        if (!choice.Wants(kind))
        {
            return NoticeWay.None;
        }

        var sound = kind == NoticeKind.Waiting && choice.Sound;
        if (kind == NoticeKind.Limit)
        {
            return NoticeWay.Window;
        }

        if (inFront)
        {
            return !open && sound ? NoticeWay.Sound : NoticeWay.None;
        }

        return sound ? NoticeWay.WindowWithSound : NoticeWay.Window;
    }

    /// Whether a limit went over the line. The first number Aiko sees is where the limit already was.
    public static bool CrossedLimit(int? before, int? now) =>
        before is { } was && now is { } percent && was < LimitLine && percent >= LimitLine;
}

/// Follows the windows of every environment's limits and says which ones crossed the line. A window
/// past its reset counts as empty, so the next window can cross again.
public sealed class LimitWatch
{
    private readonly Dictionary<(string Environment, LimitKind Kind), int> _percents = [];

    public IReadOnlyList<LimitCrossing> Next(IEnumerable<LimitSnapshot> snapshots, DateTimeOffset now)
    {
        var crossed = new List<LimitCrossing>();
        foreach (var snapshot in snapshots)
        {
            foreach (var window in snapshot.Windows)
            {
                if (snapshot.StatusAt(now, window.Kind) is not { } status)
                {
                    continue;
                }

                var key = (snapshot.Environment, window.Kind);
                int? before = _percents.TryGetValue(key, out var was) ? was : null;
                _percents[key] = status.Percent;
                if (Notices.CrossedLimit(before, status.Percent))
                {
                    crossed.Add(new LimitCrossing(snapshot.Environment, window.Kind, status.Percent));
                }
            }
        }

        return crossed;
    }
}
