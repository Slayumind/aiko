namespace Aiko.Core;

/// How the operating system names programs, lists folders in PATH and runs a shell command.
///
/// The core decides what to do; these small facts decide how it is spelled on one system. Each
/// program passes the description of the system it runs on, so the rules in the core read the
/// same for a port to another system. Only Windows exists today.
public sealed record PlatformConventions
{
    /// Added to a program name to get its file name: ".exe" on Windows.
    public required string ExecutableSuffix { get; init; }

    /// Between the folders in PATH and in other folder lists: ';' on Windows.
    public required char PathListSeparator { get; init; }

    /// How a command line names the user's local data folder, so the command does not have to
    /// spell the path itself. Null when the system has no such variable.
    public required string? LocalDataVariable { get; init; }

    /// The shell Claude Code runs a status line with when Git Bash is not there.
    public required ShellProgram FallbackShell { get; init; }

    public static readonly PlatformConventions Windows = new()
    {
        ExecutableSuffix = ".exe",
        PathListSeparator = ';',
        // cmd.exe expands it when Claude Code runs the command.
        LocalDataVariable = "%LOCALAPPDATA%",
        FallbackShell = new ShellProgram(
            ClaudeShell.PowerShell, "powershell.exe", ["-NoProfile", "-NonInteractive", "-Command"]),
    };

    /// "claude" becomes "claude.exe" on Windows.
    public string ExecutableName(string programName) => programName + ExecutableSuffix;
}

/// A shell program and the arguments that go before the command it should run.
public sealed record ShellProgram(ClaudeShell Kind, string FileName, IReadOnlyList<string> ArgumentsBeforeCommand);
