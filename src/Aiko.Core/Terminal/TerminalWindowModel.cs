namespace Aiko.Core.Terminal;

/// What the part of the window to the right of the list shows.
public enum MainView
{
    /// The environment has no session in the list: one sentence and a button.
    NoSessions,

    /// A new session before its first message: an empty feed with the folder, the model and the
    /// mode in the line under the field (D-342).
    Form,

    Talk,
}

/// What is chosen in the window of the terminal: the tab of an environment, a session of it and
/// whether the form of a new session is open (D-292, D-293).
public sealed record TerminalWindowModel(string Environment, string? SessionId, bool FormOpen)
{
    /// The window as it opens: on the session that was open last, or on the first environment.
    /// environments is never empty.
    public static TerminalWindowModel Opened(TerminalSessions sessions, IReadOnlyList<string> environments)
    {
        var last = sessions.LastId is { } id ? sessions.Find(id) : null;
        var environment = last is not null && environments.Contains(last.Environment) ? last.Environment : environments[0];
        return new TerminalWindowModel(environment, sessions.ToOpen(environment)?.Id, false);
    }

    public MainView View(TerminalSessions sessions) =>
        FormOpen ? MainView.Form
        : Session(sessions) is not null ? MainView.Talk
        : MainView.NoSessions;

    /// The chosen session, when it is still in the list of the chosen environment.
    public TerminalSession? Session(TerminalSessions sessions) =>
        SessionId is { } id && sessions.Find(id) is { Closed: false } session && session.Environment == Environment
            ? session
            : null;

    /// Another tab. The form closes: it belongs to the environment it was opened in.
    public TerminalWindowModel ToEnvironment(string environment, TerminalSessions sessions) =>
        environment == Environment
            ? this
            : new TerminalWindowModel(environment, sessions.ToOpen(environment)?.Id, false);

    /// A click on a row. A session of another environment, a closed one or an unknown one changes nothing.
    public TerminalWindowModel ToSession(string id, TerminalSessions sessions) =>
        sessions.Find(id) is { Closed: false } session && session.Environment == Environment
            ? this with { SessionId = id, FormOpen = false }
            : this;

    public TerminalWindowModel WithForm(bool open) => this with { FormOpen = open };

    /// After the list has changed: a session was made, closed or the file was read again. The
    /// window stays on its session when it can; otherwise it takes the one the list opens on.
    public TerminalWindowModel After(TerminalSessions sessions) =>
        Session(sessions) is not null ? this : this with { SessionId = sessions.ToOpen(Environment)?.Id };
}
