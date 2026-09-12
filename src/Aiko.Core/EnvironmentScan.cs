namespace Aiko.Core;

/// A Claude Code config folder as the app found it on disk. The core never looks at the disk
/// itself, so the facts come in already gathered.
public sealed record ClaudeFolder(string FullPath, string FolderName)
{
    /// Without credentials there is no account in this folder, so there is nothing to show.
    public bool HasCredentials { get; init; }

    /// When the folder was last touched. Used only to put the folders in a sensible order.
    public DateTimeOffset? LastUsed { get; init; }
}

/// A folder Aiko is ready to offer as an environment, with a name to start from.
public sealed record FoundEnvironment(string SuggestedName, string FullPath)
{
    public DateTimeOffset? LastUsed { get; init; }
}

/// Which of the folders on disk are worth offering as environments, and what to call them.
///
/// Aiko never decides which account is the "real" one. On this machine .claude-work holds expired
/// tokens and .claude-personal is in daily use, and the two folders hold exactly the same files:
/// nothing on disk tells them apart. So the list is offered, and the person names and picks.
public static class EnvironmentScan
{
    /// A folder in daily use has been touched recently. Only used for the order of the list.
    public static readonly TimeSpan RecentlyUsed = TimeSpan.FromDays(30);

    /// The name for a folder with nothing after "claude", when the caller does not give one in the
    /// user's language.
    public const string PlainFolderName = "Main";

    public static IReadOnlyList<FoundEnvironment> Pick(
        IReadOnlyList<ClaudeFolder> folders,
        string plainFolderName = PlainFolderName) =>
        folders
            .Where(folder => folder.HasCredentials)
            .OrderByDescending(folder => folder.LastUsed ?? DateTimeOffset.MinValue)
            .ThenBy(folder => folder.FolderName, StringComparer.OrdinalIgnoreCase)
            .Select(folder => new FoundEnvironment(SuggestName(folder.FolderName, plainFolderName), folder.FullPath)
            {
                LastUsed = folder.LastUsed,
            })
            .ToList();

    /// ".claude-personal" becomes "Personal", ".claude_work" becomes "Work", and a plain ".claude"
    /// gets the plain name, "Main" unless the caller passes it in the user's language. The core
    /// has no language, so the word comes from outside. It is only a starting point: the name
    /// belongs to the user.
    public static string SuggestName(string folderName, string plainFolderName = PlainFolderName)
    {
        var name = folderName.TrimStart('.');
        if (name.StartsWith("claude", StringComparison.OrdinalIgnoreCase))
        {
            name = name["claude".Length..];
        }

        name = name.Trim('-', '_', ' ');
        if (name.Length == 0)
        {
            return plainFolderName;
        }

        return char.ToUpperInvariant(name[0]) + name[1..];
    }
}
