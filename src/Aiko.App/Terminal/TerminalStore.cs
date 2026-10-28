using System.IO;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// What the terminal reads from disk besides its own list: where Claude Code keeps the file of a
/// conversation, and the folders inside a bound one.
///
/// Nothing here throws, and nothing here writes.
static class TerminalStore
{
    /// The file of a conversation, or null when Claude Code has none.
    public static string? TranscriptFile(string configDirectory, string folder, string sessionId)
    {
        try
        {
            var exact = TranscriptPath.For(ThisComputer.Platform, configDirectory, folder, sessionId);
            if (File.Exists(exact))
            {
                return exact;
            }

            if (!TranscriptPath.IsCut(folder))
            {
                return null;
            }

            // The name was cut, and Claude Code added characters of its own to it.
            var projects = TranscriptPath.ProjectsFolder(ThisComputer.Platform, configDirectory);
            return Directory.EnumerateDirectories(projects, TranscriptPath.FolderName(folder) + "-*")
                .Select(candidate => Path.Combine(candidate, TranscriptPath.FileName(sessionId)))
                .FirstOrDefault(File.Exists);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    /// The folders right inside one, the ones changed last first, without the hidden ones (D-335).
    public static IReadOnlyList<string> FoldersInside(string folder)
    {
        try
        {
            return [.. new DirectoryInfo(folder)
                .EnumerateDirectories()
                .Where(inside => !inside.Name.StartsWith('.') && !inside.Attributes.HasFlag(FileAttributes.Hidden))
                .OrderByDescending(inside => inside.LastWriteTimeUtc)
                .Select(inside => inside.FullName)];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return [];
        }
    }
}
