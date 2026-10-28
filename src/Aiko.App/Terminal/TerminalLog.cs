namespace Aiko.App;

/// The only place in the terminal that writes to the log of Aiko (D-298).
///
/// A method here takes numbers, yes or no, and the name of an environment. It takes no other
/// text, so a command, a path, an id or a line of the terminal cannot reach the log through it. A
/// test reads this file and the rest of the folder to keep it so.
static class TerminalLog
{
    public static void Started(string environment, bool shell, bool resumed) =>
        Log.Write($"terminal: {(shell ? "a shell" : "Claude Code")} started for {environment}{(shell ? "" : resumed ? ", a resumed conversation" : ", a new conversation")}");

    public static void StartFailed(string environment, bool shell, int error) =>
        Log.Write($"terminal: {(shell ? "a shell" : "Claude Code")} did not start for {environment}, error {error}");

    public static void Exited(string environment, bool shell, int exitCode) =>
        Log.Write($"terminal: {(shell ? "a shell" : "Claude Code")} for {environment} exited with {exitCode}");
}
