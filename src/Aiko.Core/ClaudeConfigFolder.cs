namespace Aiko.Core;

/// Which Claude Code folder a run of the bridge belongs to.
///
/// Claude Code started without CLAUDE_CONFIG_DIR works in the .claude folder of the user's home,
/// and that is the most common setup there is: one account, nothing configured. The bridge used to
/// call that case "default" and wrote default.json, while the app looked for the same environment
/// under the folder's own name. The two never met, so a person with the plainest possible setup
/// would never have seen a limit, and their own status line would never have run either. Found on
/// the first live run, before anybody outside had a copy.
public static class ClaudeConfigFolder
{
    public const string DefaultFolderName = ".claude";

    public const string AccountFileName = ".claude.json";

    public static string Resolve(string? fromEnvironment, string userProfile)
    {
        var set = fromEnvironment?.Trim();
        return string.IsNullOrEmpty(set)
            ? Path.Combine(userProfile, DefaultFolderName)
            : set;
    }

    /// Where Claude Code keeps the account facts of a folder.
    ///
    /// Inside the folder, except for the default one: started without CLAUDE_CONFIG_DIR, Claude
    /// Code keeps .claude.json in the home folder beside .claude. Aiko starts environment 1 without
    /// the variable, so that home file is the one that stays current. A copy inside .claude only
    /// exists when somebody once set the variable to it by hand, and it is used only when the home
    /// file is missing.
    public static IReadOnlyList<string> AccountFileCandidates(string folder, string userProfile)
    {
        var inside = Path.Combine(folder, AccountFileName);
        if (!IsDefault(folder, userProfile))
        {
            return [inside];
        }

        return [Path.Combine(userProfile, AccountFileName), inside];
    }

    public static bool IsDefault(string folder, string userProfile) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(folder),
            Path.Combine(userProfile, DefaultFolderName),
            StringComparison.OrdinalIgnoreCase);
}
