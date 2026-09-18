namespace Aiko.Core;

/// Where Claude Code is, and where a new environment's folder goes.
public static class ClaudeInstall
{
    /// The PATH a terminal opened right now would get: the machine part, then the user part, with
    /// variables expanded. Aiko itself may have started before Claude Code was installed, and its
    /// own PATH is then out of date: the wizard would wait forever for a Claude Code that is there.
    public static string FreshPath(
        PlatformConventions platform, string? machinePath, string? userPath, Func<string, string> expand) =>
        string.Join(platform.PathListSeparator, new[] { machinePath, userPath }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => expand(part!)));

    /// The real Claude Code on that PATH, never Aiko's own shim, or where the native installer puts
    /// it when PATH has not caught up.
    public static string? Find(
        PlatformConventions platform, string freshPath, string commandFolder, string userProfile, Func<string, bool> exists)
    {
        var onPath = RealClaude.Find(platform, freshPath, [commandFolder], exists);
        if (onPath is not null)
        {
            return onPath;
        }

        var native = platform.Join(userProfile, ".local", "bin", RealClaude.ExecutableName(platform));
        return exists(native) ? native : null;
    }

    /// .claude-work for "Work", .claude-osnovnaya for "Основная". A name already taken gets a
    /// number, so a new environment never lands in somebody's existing account folder.
    public static string NewConfigFolder(
        PlatformConventions platform, string environmentName, string userProfile, Func<string, bool> folderExists)
    {
        var slug = LaunchCommand.Slug(environmentName);
        var stem = platform.Join(userProfile, ClaudeConfigFolder.NamedFolderName(slug.Length > 0 ? slug : "env"));

        var candidate = stem;
        for (var n = 2; folderExists(candidate); n++)
        {
            candidate = stem + "-" + n;
        }

        return candidate;
    }

    public const string CredentialsFileName = ".credentials.json";

    /// Signed in means Claude Code wrote its credentials file. Only that the file exists is looked
    /// at; it is never opened here.
    public static string CredentialsPathIn(PlatformConventions platform, string configFolder) =>
        platform.Join(configFolder, CredentialsFileName);
}
