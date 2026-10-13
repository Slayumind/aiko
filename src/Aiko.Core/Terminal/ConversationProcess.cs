namespace Aiko.Core.Terminal;

/// What it takes to start one child process: the program, its arguments, what changes in its
/// environment and the folder it works in.
public sealed record ProcessStart(
    string Executable,
    IReadOnlyList<string> Arguments,
    IReadOnlyList<VariableChange> Variables,
    string WorkingDirectory);

/// One run of Claude Code as a child process (D-297). The real one starts claude; a scripted one
/// answers from a list, so a conversation can be played without Claude Code.
///
/// A process is used once: after it has exited, a new one is made for the next start.
public interface IConversationProcess
{
    /// Starts the process. Its lines and its exit come on a thread of its own, the lines in the
    /// order they were written and the exit after the last of them. False when it did not start;
    /// then nothing is ever called.
    bool Start(ProcessStart start, Action<string> onLine, Action<int> onExit);

    /// Writes one line to the process. False when the process is gone.
    bool Write(string line);

    /// Closes the input of the process, which asks Claude Code to leave. A process that stays is
    /// ended a little later, with everything it started.
    void Stop();
}

/// What the terminal writes to the log of Aiko about a conversation (D-298).
///
/// The only text a method takes is the name of the environment. A message, a path, an id or a
/// line of Claude Code cannot be passed here, so it cannot reach the log.
public interface ITerminalLog
{
    void Started(string environment, bool resumed);

    void StartFailed(string environment);

    void Exited(string environment, int exitCode, int linesRead, int linesWritten);

    /// The terminal ended the process itself: the window closed, or the session gave way to another.
    void Stopped(string environment, int linesRead, int linesWritten);
}
