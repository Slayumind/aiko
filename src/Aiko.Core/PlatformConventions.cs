namespace Aiko.Core;

/// How the operating system names programs, spells a path, lists folders in PATH and runs a shell
/// command.
///
/// The core decides what to do; these small facts decide how it is spelled on one system. Each
/// program passes the description of the system it runs on, so the rules in the core read the
/// same on Windows and on macOS.
public sealed record PlatformConventions
{
    /// Added to a program name to get its file name: ".exe" on Windows, nothing on macOS.
    public required string ExecutableSuffix { get; init; }

    /// Between the folders in PATH and in other folder lists: ';' on Windows, ':' on macOS.
    public required char PathListSeparator { get; init; }

    /// Between the parts of a path. Path.Combine follows the system the code runs on, and the core
    /// has to build paths for the system it was given instead.
    public required char DirectorySeparator { get; init; }

    /// How a command line names the user's local data folder, so the command does not have to
    /// spell the path itself. Null when the system has no such variable.
    public required string? LocalDataVariable { get; init; }

    /// The shell Claude Code runs a status line with when Git Bash is not there.
    public required ShellProgram FallbackShell { get; init; }

    public static readonly PlatformConventions Windows = new()
    {
        ExecutableSuffix = ".exe",
        PathListSeparator = ';',
        DirectorySeparator = '\\',
        // cmd.exe expands it when Claude Code runs the command.
        LocalDataVariable = "%LOCALAPPDATA%",
        FallbackShell = new ShellProgram(
            ClaudeShell.PowerShell, "powershell.exe", ["-NoProfile", "-NonInteractive", "-Command"]),
    };

    /// macOS has no Git Bash and no %VARIABLE% in a command, so a command spells the real path.
    /// zsh is the login shell since Catalina, and -l makes it read the profile the person's own
    /// tools are on.
    public static readonly PlatformConventions MacOS = new()
    {
        ExecutableSuffix = "",
        PathListSeparator = ':',
        DirectorySeparator = '/',
        LocalDataVariable = null,
        FallbackShell = new ShellProgram(ClaudeShell.Zsh, "/bin/zsh", ["-lc"]),
    };

    /// "claude" becomes "claude.exe" on Windows and stays "claude" on macOS.
    public string ExecutableName(string programName) => programName + ExecutableSuffix;

    /// Joins the parts of a path with the separator of this system. A part that already ends with
    /// a separator does not get a second one.
    public string Join(string first, params string[] rest)
    {
        var path = first;
        foreach (var part in rest)
        {
            path = path.TrimEnd(DirectorySeparator) + DirectorySeparator + part;
        }

        return path;
    }
}

/// A shell program and the arguments that go before the command it should run.
public sealed record ShellProgram(ClaudeShell Kind, string FileName, IReadOnlyList<string> ArgumentsBeforeCommand);
