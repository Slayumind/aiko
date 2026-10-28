namespace Aiko.Core.Terminal;

/// What the mark of a session in the list says (D-293, D-299).
public enum RowState
{
    Working,

    /// A request waits for the person.
    Waiting,

    /// The turn ended and the person has not looked yet.
    Done,

    /// The turn ended and the person has seen it. The row has no mark.
    Read,

    /// No process runs for the session. The second line says when it was used last.
    Asleep,

    /// The program ended by itself (D-351): Enter brings Claude Code back.
    Stopped,
}

/// How long ago a session was used, as the second line of a sleeping row says it.
public enum UsedDay
{
    /// Only the time is shown.
    Today,
    Yesterday,

    /// The date is shown.
    Earlier,
}

/// What a running terminal is doing, as far as Aiko knows it.
public enum TerminalActivity
{
    /// It runs and waits for nothing in particular.
    Idle,
    Working,

    /// Claude Code asks the person something.
    Waiting,

    /// A turn ended and the person has not looked yet.
    Done,

    /// The program ended by itself.
    Exited,
}

/// A terminal that runs for a session, or ran and ended. Line is the last thing it did, in the
/// words of the second line of the row; empty when nothing is known.
public sealed record LiveTerminal(TerminalActivity Activity, string Line);

public sealed record SessionRow(string Id, string Name, RowState State, string Line, long LastUsed)
{
    public SessionKind Kind { get; init; } = SessionKind.Claude;
}

public sealed record RowGroup(string Folder, string Name, IReadOnlyList<SessionRow> Rows);

/// The list of sessions of one environment as rows, and the count for the tab of the environment.
public static class SessionRows
{
    /// live gives the terminal of a session, or null when none was started.
    public static IReadOnlyList<RowGroup> For(TerminalSessions sessions, string environment, Func<string, LiveTerminal?> live) =>
        [.. sessions.Groups(environment).Select(group => new RowGroup(
            group.Folder,
            group.Name,
            [.. group.Sessions.Select(session => Row(session, live(session.Id)))]))];

    /// How many sessions of the environment wait for the person: the number on its tab.
    public static int Waiting(TerminalSessions sessions, string environment, Func<string, LiveTerminal?> live) =>
        sessions.Open(environment).Count(session => live(session.Id)?.Activity == TerminalActivity.Waiting);

    /// The day a session was used on, counted by the clock of the person: offsetMinutes is how far
    /// that clock is from UTC. A time that is still to come reads as today.
    public static UsedDay DayOf(long usedSeconds, long nowSeconds, int offsetMinutes)
    {
        const long Day = 86400;
        var shift = offsetMinutes * 60L;
        var days = Math.Max(0, FloorDay(nowSeconds + shift, Day) - FloorDay(usedSeconds + shift, Day));
        return days switch
        {
            0 => UsedDay.Today,
            1 => UsedDay.Yesterday,
            _ => UsedDay.Earlier,
        };
    }

    private static long FloorDay(long seconds, long day) => (seconds - (((seconds % day) + day) % day)) / day;

    private static SessionRow Row(TerminalSession session, LiveTerminal? terminal)
    {
        var state = terminal?.Activity switch
        {
            null => RowState.Asleep,
            TerminalActivity.Working => RowState.Working,
            TerminalActivity.Waiting => RowState.Waiting,
            TerminalActivity.Done => RowState.Done,
            TerminalActivity.Exited => RowState.Stopped,
            _ => RowState.Read,
        };

        // A sleeping session says when it was used, not what it did last.
        return new SessionRow(session.Id, session.Name, state, state == RowState.Asleep ? "" : terminal?.Line ?? "", session.LastUsed)
        {
            Kind = session.Kind,
        };
    }
}
