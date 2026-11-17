namespace Aiko.Core.Terminal;

/// Why Aiko is about to stop every program in its window (D-351). Each way out asks first, in one
/// place, so none of them can end a session without a word.
public enum StopReason
{
    Quit,
    Restart,
    Remove,
    StartOver,

    /// One environment goes, and only its sessions stop. Aiko stays.
    RemoveEnvironment,
}

/// The question the window asks before it stops every session: the title, what stops, and the two
/// buttons. The twin of StopQuestion.swift.
public sealed record StopQuestion(string Title, string Body, string Stop, string Keep)
{
    /// Whether the step takes Aiko or all its environments away, so every session ends with it.
    public static bool EndsEverySession(StopReason reason) => reason != StopReason.RemoveEnvironment;

    public static StopQuestion For(StopReason reason) => reason switch
    {
        StopReason.Restart => new(Strings.TerminalQuitTitle, Strings.TerminalRestartBody, Strings.TerminalStopAndRestart, Strings.TerminalDontRestart),
        StopReason.Remove => new(Strings.TerminalQuitTitle, Strings.TerminalRemoveBody, Strings.TerminalStopAndRemove, Strings.TerminalDontRemove),
        StopReason.RemoveEnvironment => new(Strings.TerminalQuitTitle, Strings.TerminalRemoveEnvironmentBody, Strings.TerminalStopAndRemove, Strings.TerminalDontRemove),
        StopReason.StartOver => new(Strings.TerminalQuitTitle, Strings.TerminalStartOverBody, Strings.TerminalStopAndStartOver, Strings.TerminalDontStartOver),
        _ => new(Strings.TerminalQuitTitle, Strings.TerminalQuitBody, Strings.TerminalStopAndQuit, Strings.TerminalDontQuit),
    };
}
