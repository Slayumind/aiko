using System.IO;
using Aiko.Core;

namespace Aiko.App;

/// Finds the Claude Code config folders on this machine and reports plain facts about them.
///
/// Only names and timestamps are read here. The credentials file is never opened: whether it
/// exists is all Aiko needs to know, and its contents are none of our business.
static class ClaudeFolders
{
    private const string CredentialsFile = ".credentials.json";

    /// Files whose age says the folder is in use. Reading their times touches no secrets.
    private static readonly string[] SignsOfUse = [".claude.json", "settings.json", "projects"];

    public static IReadOnlyList<ClaudeFolder> Find()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Find(home);
    }

    public static IReadOnlyList<ClaudeFolder> Find(string home)
    {
        var folders = new List<ClaudeFolder>();

        try
        {
            foreach (var directory in Directory.EnumerateDirectories(home, ".claude*"))
            {
                folders.Add(new ClaudeFolder(directory, Path.GetFileName(directory))
                {
                    HasCredentials = SafeExists(Path.Combine(directory, CredentialsFile)),
                    LastUsed = LastUsed(directory),
                });
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return folders;
    }

    private static DateTimeOffset? LastUsed(string directory)
    {
        DateTimeOffset? newest = null;

        foreach (var name in SignsOfUse)
        {
            var path = Path.Combine(directory, name);
            try
            {
                if (!File.Exists(path) && !Directory.Exists(path))
                {
                    continue;
                }

                var written = new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero);
                if (newest is null || written > newest)
                {
                    newest = written;
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return newest;
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
}
