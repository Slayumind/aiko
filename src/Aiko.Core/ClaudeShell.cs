namespace Aiko.Core;

/// The shell Claude Code runs the status line with. It uses Git Bash, and falls back to
/// PowerShell only when Git Bash is not installed.
public enum ClaudeShell
{
    GitBash,
    PowerShell,
}

/// Where Git Bash lives, and how to call a status line command with the shell Claude Code uses.
///
/// Both the app and the bridge need this. The app needs it when it writes our line, because the
/// two shells need different text. The bridge needs it when it runs the line the user already had:
/// that line was written for Claude Code's shell, and running it through cmd.exe instead makes a
/// working bash line print nothing at all.
public static class ClaudeShellLookup
{
    /// Git Bash is looked for as part of a Git installation, never as "some bash.exe somewhere".
    ///
    /// Windows 10 and 11 ship C:\Windows\System32\bash.exe, which is the WSL launcher and not Git
    /// Bash at all. Taking it for Git Bash on a machine without Git would write the bash form of
    /// the command while Claude Code ran PowerShell, and the status line would stay silent.
    /// A place to look: the bash.exe itself, and the git.exe that has to sit beside it for the
    /// find to count. Guard is null for the places a Git installer uses, where the folder layout
    /// is proof enough.
    public readonly record struct Place(string Bash, string? Guard);

    public static IEnumerable<Place> Places(string? searchPath = null, string? windowsFolder = null)
    {
        yield return new Place(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "bin", "bash.exe"), null);
        yield return new Place(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Git", "bin", "bash.exe"), null);
        yield return new Place(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs", "Git", "bin", "bash.exe"), null);

        // Git installed somewhere else still puts git.exe on the PATH, and bash.exe sits in the
        // bin folder next to it.
        var path = searchPath ?? Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var windows = windowsFolder ?? Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        foreach (var folder in path.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            // Anything under the Windows folder is the system's own, not a Git installation.
            if (windows.Length > 0 && folder.StartsWith(windows, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var git = TryCombine(folder, "git.exe");
            var bash = TryCombine(folder, Path.Combine("..", "bin", "bash.exe"));
            if (git is not null && bash is not null)
            {
                yield return new Place(bash, git);
            }
        }
    }

    /// The path to Git Bash, or null when Git is not installed. `exists` is passed in so this can
    /// be tested without a Git installation on the machine running the tests.
    public static string? FindGitBash(Func<string, bool> exists, IEnumerable<Place>? places = null)
    {
        foreach (var place in places ?? Places())
        {
            if (place.Guard is not null && !Safe(exists, place.Guard))
            {
                continue;
            }

            if (Safe(exists, place.Bash))
            {
                return place.Bash;
            }
        }

        return null;
    }

    public static ClaudeShell ShellFor(string? gitBashPath) =>
        gitBashPath is null ? ClaudeShell.PowerShell : ClaudeShell.GitBash;

    /// How to run a command that was written for Claude Code's shell.
    ///
    /// Arguments are handed over as a list rather than glued into one string: the command is
    /// somebody else's text and may hold quotes, and escaping it by hand is a bug waiting to be
    /// written.
    public static (string FileName, string[] Arguments) CallFor(string? gitBashPath, string command) =>
        gitBashPath is null
            ? ("powershell.exe", ["-NoProfile", "-NonInteractive", "-Command", command])
            : (gitBashPath, ["-c", command]);

    private static bool Safe(Func<string, bool> exists, string path)
    {
        try
        {
            return exists(path);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// A PATH entry can hold characters no path may hold; such an entry is simply skipped.
    private static string? TryCombine(string folder, string file)
    {
        try
        {
            return Path.Combine(folder, file);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
