namespace Aiko.Core.Terminal;

/// Which sign the tray icon and the taskbar button show (D-409).
public enum BadgeKind
{
    None,

    /// The number of the sessions that wait for the person.
    Waiting,

    /// The number of the sessions that stopped by themselves and were not opened yet.
    Stopped,

    /// A dot: a session ended a turn the person has not seen yet.
    Done,
}

/// The one sign on the tray icon and the taskbar button. Text is empty for the dot.
public sealed record SessionBadge(BadgeKind Kind, int Count)
{
    public static readonly SessionBadge None = new(BadgeKind.None, 0);

    /// The highest number with its own text: two digits do not fit a 16 px icon.
    public const int Most = 9;

    public string Text => Kind is BadgeKind.None or BadgeKind.Done ? "" : Count > Most ? $"{Most}+" : $"{Count}";
}

/// How many sessions wait, ended a turn, stopped and work (D-364, D-409), and the waiting session a
/// click on the mark of the island opens. A count stays until the person opens the session, like an
/// unread chat in a messenger. Rows: spec/cases/notices.
public sealed record SessionCounts(int Working, int Waiting, int Done, int Stopped, string? FirstWaiting)
{
    public static readonly SessionCounts None = new(0, 0, 0, 0, null);

    public bool IsEmpty => Working == 0 && Waiting == 0 && Done == 0 && Stopped == 0;

    /// What waits has to be answered, what stopped has to be started again, and what is done only
    /// has to be read: the sign shows the most urgent of them.
    public SessionBadge Badge =>
        Waiting > 0 ? new SessionBadge(BadgeKind.Waiting, Waiting)
        : Stopped > 0 ? new SessionBadge(BadgeKind.Stopped, Stopped)
        : Done > 0 ? new SessionBadge(BadgeKind.Done, Done)
        : SessionBadge.None;

    /// The running terminals in the order of the list, with what each one does. A stopped
    /// session the person has seen since it stopped comes here as Idle (StoppedIsNews).
    public static SessionCounts Of(IEnumerable<(string Id, TerminalActivity Activity)> terminals)
    {
        var working = 0;
        var waiting = 0;
        var done = 0;
        var stopped = 0;
        string? first = null;
        foreach (var (id, activity) in terminals)
        {
            switch (activity)
            {
                case TerminalActivity.Working:
                    working++;
                    break;
                case TerminalActivity.Waiting:
                    waiting++;
                    first ??= id;
                    break;
                case TerminalActivity.Done:
                    done++;
                    break;
                case TerminalActivity.Exited:
                    stopped++;
                    break;
            }
        }

        return new SessionCounts(working, waiting, done, stopped, first);
    }

    /// A stop is news until the person sees the session after it. Both times in Unix milliseconds;
    /// a session seen in the same millisecond counts as seen.
    public static bool StoppedIsNews(long stoppedAt, long seenAt) => stoppedAt > seenAt;
}
