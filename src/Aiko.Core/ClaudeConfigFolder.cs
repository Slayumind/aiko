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

    public static string Resolve(string? fromEnvironment, string userProfile)
    {
        var set = fromEnvironment?.Trim();
        return string.IsNullOrEmpty(set)
            ? Path.Combine(userProfile, DefaultFolderName)
            : set;
    }
}
