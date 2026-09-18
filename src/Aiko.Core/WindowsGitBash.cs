namespace Aiko.Core;

/// The Windows folders a Git installation can be under. The app and the bridge read them from
/// Windows and pass them in, so the search below needs no Windows call and can be tested.
public sealed record WindowsSystemFolders(string ProgramFiles, string ProgramFilesX86, string LocalAppData, string Windows);

/// Where Git Bash lives on Windows. Only Windows has Git Bash, so this class is Windows only by name
/// and by content; platform-neutral code does not call it.
///
/// Git Bash is looked for as part of a Git installation, never as "some bash.exe somewhere".
/// Windows 10 and 11 ship C:\Windows\System32\bash.exe, which is the WSL launcher and not Git
/// Bash at all. Taking it for Git Bash on a machine without Git would write the bash form of
/// the command while Claude Code ran PowerShell, and the status line would stay silent.
public static class WindowsGitBash
{
    /// A place to look: the bash.exe itself, and the git.exe that has to sit beside it for the
    /// find to count. Guard is null for the places a Git installer uses, where the folder layout
    /// is proof enough.
    public readonly record struct Place(string Bash, string? Guard);

    public static IEnumerable<Place> Places(WindowsSystemFolders folders, string searchPath)
    {
        yield return new Place(Path.Combine(folders.ProgramFiles, "Git", "bin", "bash.exe"), null);
        yield return new Place(Path.Combine(folders.ProgramFilesX86, "Git", "bin", "bash.exe"), null);
        yield return new Place(Path.Combine(folders.LocalAppData, "Programs", "Git", "bin", "bash.exe"), null);

        // Git installed somewhere else still puts git.exe on the PATH, and bash.exe sits in the
        // bin folder next to it.
        var separator = PlatformConventions.Windows.PathListSeparator;
        foreach (var folder in searchPath.Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            // Anything under the Windows folder is the system's own, not a Git installation.
            if (folders.Windows.Length > 0 && folder.StartsWith(folders.Windows, StringComparison.OrdinalIgnoreCase))
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
    public static string? Find(Func<string, bool> exists, IEnumerable<Place> places)
    {
        foreach (var place in places)
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
