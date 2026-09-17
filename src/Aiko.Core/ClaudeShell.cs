namespace Aiko.Core;

/// The shell Claude Code runs the status line with. On Windows it uses Git Bash, and falls back to
/// PowerShell only when Git Bash is not installed.
public enum ClaudeShell
{
    GitBash,
    PowerShell,
}

/// How to call a status line command with the shell Claude Code uses.
///
/// Both the app and the bridge need this. The app needs it when it writes our line, because the
/// two shells need different text. The bridge needs it when it runs the line the user already had:
/// that line was written for Claude Code's shell, and running it through cmd.exe instead makes a
/// working bash line print nothing at all.
///
/// Where Git Bash is gets found in WindowsGitBash; the shell to fall back on comes from the platform.
public static class ClaudeShellLookup
{
    public static ClaudeShell ShellFor(PlatformConventions platform, string? gitBashPath) =>
        gitBashPath is null ? platform.FallbackShell.Kind : ClaudeShell.GitBash;

    /// How to run a command that was written for Claude Code's shell.
    ///
    /// Arguments are handed over as a list rather than glued into one string: the command is
    /// somebody else's text and may hold quotes, and escaping it by hand is a bug waiting to be
    /// written.
    public static (string FileName, string[] Arguments) CallFor(
        PlatformConventions platform, string? gitBashPath, string command) =>
        gitBashPath is null
            ? (platform.FallbackShell.FileName, [.. platform.FallbackShell.ArgumentsBeforeCommand, command])
            : (gitBashPath, ["-c", command]);
}
