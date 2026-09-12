using System.IO;
using Aiko.Core;

namespace Aiko.App;

/// Which shell Claude Code will run our status line with.
///
/// It uses Git Bash and only falls back to PowerShell when Git Bash is missing, and the two need
/// different text for the same command. Guessing wrong is quiet: the status line simply produces
/// nothing.
static class ShellDetect
{
    public static ClaudeShell Current() => HasGitBash() ? ClaudeShell.GitBash : ClaudeShell.PowerShell;

    private static bool HasGitBash()
    {
        foreach (var path in Places())
        {
            try
            {
                if (File.Exists(path))
                {
                    return true;
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return false;
    }

    /// Git Bash is looked for as part of a Git installation, never as "some bash.exe somewhere".
    ///
    /// Windows 10 and 11 ship C:\Windows\System32\bash.exe, which is the WSL launcher and not Git
    /// Bash at all. Taking it for Git Bash on a machine without Git would write the bash form of
    /// the command while Claude Code ran PowerShell, and the status line would stay silent.
    private static IEnumerable<string> Places()
    {
        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "bin", "bash.exe");
        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Git", "bin", "bash.exe");
        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs", "Git", "bin", "bash.exe");

        // Git installed somewhere else still puts git.exe on the PATH, and bash.exe sits in the
        // bin folder next to it.
        foreach (var folder in SearchPath())
        {
            if (TryCombine(folder, "git.exe") is { } git && SafeExists(git))
            {
                if (TryCombine(folder, Path.Combine("..", "bin", "bash.exe")) is { } bash)
                {
                    yield return bash;
                }
            }
        }
    }

    private static IEnumerable<string> SearchPath()
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var searchPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

        foreach (var folder in searchPath.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            // Anything under the Windows folder is the system's own, not a Git installation.
            if (!folder.StartsWith(windows, StringComparison.OrdinalIgnoreCase))
            {
                yield return folder;
            }
        }
    }

    private static bool SafeExists(string path)
    {
        try
        {
            return File.Exists(path);
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
