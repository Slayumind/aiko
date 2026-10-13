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

    /// Claude Code ended in the middle of the work.
    Stopped,

    SignedOut,
}

/// A conversation that is in memory: what it looks like and whether Claude Code runs for it.
public sealed record LiveTalk(TalkGlance Glance, bool Running);

public sealed record SessionRow(string Id, string Name, RowState State, TalkGlance? Glance, long LastUsed);

public sealed record RowGroup(string Folder, string Name, IReadOnlyList<SessionRow> Rows);

/// The list of sessions of one environment as rows, and the count for the tab of the environment.
public static class SessionRows
{
    /// live gives the conversation of a session that is in memory, or null.
    public static IReadOnlyList<RowGroup> For(TerminalSessions sessions, string environment, Func<string, LiveTalk?> live) =>
        [.. sessions.Groups(environment).Select(group => new RowGroup(
            group.Folder,
            group.Name,
            [.. group.Sessions.Select(session => Row(session, live(session.Id)))]))];

    /// How many sessions of the environment wait for the person: the number on its tab.
    public static int Waiting(TerminalSessions sessions, string environment, Func<string, LiveTalk?> live) =>
        sessions.Open(environment).Count(session => live(session.Id)?.Glance.Activity == TalkActivity.Waiting);

    private static SessionRow Row(TerminalSession session, LiveTalk? talk)
    {
        var state = talk is null ? RowState.Asleep : talk.Glance.Activity switch
        {
            TalkActivity.Working => RowState.Working,
            TalkActivity.Waiting => RowState.Waiting,
            TalkActivity.Done => RowState.Done,
            TalkActivity.Failed => RowState.Stopped,
            TalkActivity.SignedOut => RowState.SignedOut,
            _ => talk.Running ? RowState.Read : RowState.Asleep,
        };

        // A sleeping session says when it was used, not what was said last.
        return new SessionRow(session.Id, session.Name, state, state == RowState.Asleep ? null : talk?.Glance, session.LastUsed);
    }
}
