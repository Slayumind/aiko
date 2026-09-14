namespace Aiko.Core;

/// Where Claude Code is, and where a new environment's folder goes.
public static class ClaudeInstall
{
    /// The PATH a terminal opened right now would get: the machine part, then the user part, with
    /// %VARIABLES% expanded. Aiko itself may have started before Claude Code was installed, and
    /// its own PATH is then out of date: the wizard would wait forever for a claude.exe that is there.
    public static string FreshPath(string? machinePath, string? userPath, Func<string, string> expand) =>
        string.Join(';', new[] { machinePath, userPath }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => expand(part!)));

    /// The real claude.exe on that PATH, never Aiko's own shim, or where the native installer puts
    /// it when PATH has not caught up.
    public static string? Find(string freshPath, string commandFolder, string userProfile, Func<string, bool> exists)
    {
        var onPath = RealClaude.Find(freshPath, [commandFolder], exists);
        if (onPath is not null)
        {
            return onPath;
        }

        var native = Path.Combine(userProfile, ".local", "bin", RealClaude.ExecutableName);
        return exists(native) ? native : null;
    }

    /// .claude-work for "Work", .claude-osnovnaya for "Основная". A name already taken gets a
    /// number, so a new environment never lands in somebody's existing account folder.
    public static string NewConfigFolder(string environmentName, string userProfile, Func<string, bool> folderExists)
    {
        var slug = LaunchCommand.Slug(environmentName);
        var stem = Path.Combine(userProfile, ClaudeConfigFolder.DefaultFolderName + "-" + (slug.Length > 0 ? slug : "env"));

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
    public static string CredentialsPathIn(string configFolder) => Path.Combine(configFolder, CredentialsFileName);
}
