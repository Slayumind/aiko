namespace Aiko.Core.Terminal;

/// What the part of the window to the right of the list shows.
public enum MainView
{
    /// The environment has no session in the list: one sentence and a button.
    NoSessions,

    /// The real terminal of the chosen session (D-349).
    Terminal,
}

/// What is chosen in the window of the terminal: the tab of an environment and a session of it
/// (D-292, D-293).
public sealed record TerminalWindowModel(string Environment, string? SessionId)
{
    /// The window as it opens: on the session that was open last, or on the first environment.
    /// environments is never empty.
    public static TerminalWindowModel Opened(TerminalSessions sessions, IReadOnlyList<string> environments)
    {
        var last = sessions.LastId is { } id ? sessions.Find(id) : null;
        var environment = last is not null && environments.Contains(last.Environment) ? last.Environment : environments[0];
        return new TerminalWindowModel(environment, sessions.ToOpen(environment)?.Id);
    }

    public MainView View(TerminalSessions sessions) =>
        Session(sessions) is not null ? MainView.Terminal : MainView.NoSessions;

    /// The chosen session, when it is still in the list of the chosen environment.
    public TerminalSession? Session(TerminalSessions sessions) =>
        SessionId is { } id && sessions.Find(id) is { Closed: false } session && session.Environment == Environment
            ? session
            : null;

    /// Another tab.
    public TerminalWindowModel ToEnvironment(string environment, TerminalSessions sessions) =>
        environment == Environment
            ? this
            : new TerminalWindowModel(environment, sessions.ToOpen(environment)?.Id);

    /// A click on a row. A session of another environment, a closed one or an unknown one changes nothing.
    public TerminalWindowModel ToSession(string id, TerminalSessions sessions) =>
        sessions.Find(id) is { Closed: false } session && session.Environment == Environment
            ? this with { SessionId = id }
            : this;

    /// Ctrl+Tab and Ctrl+Shift+Tab (D-353): the next or the previous session of the environment in
    /// the order of the list, round at the ends. With no session chosen the first or the last.
    public TerminalWindowModel ToNeighbour(int step, TerminalSessions sessions)
    {
        var order = sessions.Groups(Environment).SelectMany(group => group.Sessions).Select(session => session.Id).ToList();
        if (order.Count == 0)
        {
            return this;
        }

        var at = SessionId is { } id ? order.IndexOf(id) : -1;
        var next = at < 0
            ? (step > 0 ? 0 : order.Count - 1)
            : ((at + step) % order.Count + order.Count) % order.Count;
        return this with { SessionId = order[next] };
    }

    /// After the list has changed: a session was made, closed or the file was read again. The
    /// window stays on its session when it can; otherwise it takes the one the list opens on.
    public TerminalWindowModel After(TerminalSessions sessions) =>
        Session(sessions) is not null ? this : this with { SessionId = sessions.ToOpen(Environment)?.Id };
}
