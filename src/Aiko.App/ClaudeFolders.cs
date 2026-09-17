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

    /// Files only Claude Code writes, and only while somebody uses it: the prompt history and the
    /// session logs. settings.json and .claude.json used to be here, but Aiko edits settings.json
    /// itself and Claude Code touches .claude.json on start, so a folder dead for a month looked
    /// used today. Reading file times touches no secrets.
    private static readonly string[] SignsOfUse = ["history.jsonl", "projects"];

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
            foreach (var directory in Directory.EnumerateDirectories(home, ClaudeConfigFolder.SearchPattern))
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

                // A project folder's time moves when a new session file appears in it; the projects
                // folder's own time only moves when a whole new project does.
                var time = Directory.Exists(path)
                    ? Directory.EnumerateDirectories(path).Select(Directory.GetLastWriteTimeUtc).DefaultIfEmpty(Directory.GetLastWriteTimeUtc(path)).Max()
                    : File.GetLastWriteTimeUtc(path);
                var written = new DateTimeOffset(time, TimeSpan.Zero);
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
