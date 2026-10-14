using System.IO;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// What the terminal reads from disk besides its own list: the file Claude Code keeps of a
/// conversation, the folders the form of a new session offers, and the settings that say the
/// mode a session starts in.
///
/// Nothing here throws, and nothing here writes. A file that cannot be read is an empty feed.
static class TerminalStore
{
    /// The mode Claude Code would start in for this folder and config folder (D-311): the local
    /// settings of the project, its shared settings, then the settings of the config folder.
    public static PermissionMode DefaultMode(string folder, string configDirectory) =>
        PermissionModes.Default(
        [
            ReadOrNull(Path.Combine(folder, ".claude", "settings.local.json")),
            ReadOrNull(Path.Combine(folder, ".claude", "settings.json")),
            ReadOrNull(Path.Combine(configDirectory, "settings.json")),
        ]);

    private static string? ReadOrNull(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

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

    /// The feed of a conversation as Claude Code has it on disk (D-303). Read into memory and
    /// kept nowhere else.
    public static Conversation History(string configDirectory, string folder, string sessionId, PermissionMode mode)
    {
        if (TranscriptFile(configDirectory, folder, sessionId) is not { } file)
        {
            return Conversation.Empty(mode);
        }

        try
        {
            return Transcript.Read(File.ReadLines(file), mode);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Conversation.Empty(mode);
        }
    }

    /// The folders right inside one, by name, without the hidden ones.
    public static IReadOnlyList<string> FoldersInside(string folder)
    {
        try
        {
            return [.. new DirectoryInfo(folder)
                .EnumerateDirectories()
                .Where(inside => !inside.Name.StartsWith('.') && !inside.Attributes.HasFlag(FileAttributes.Hidden))
                .OrderBy(inside => inside.Name, StringComparer.OrdinalIgnoreCase)
                .Select(inside => inside.FullName)];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return [];
        }
    }
}
