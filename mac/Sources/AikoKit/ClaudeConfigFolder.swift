import Foundation

/// Which Claude Code folder a run of the bridge belongs to.
///
/// Claude Code started without CLAUDE_CONFIG_DIR works in the .claude folder of the user's home,
/// and that is the most common setup there is: one account, nothing configured. The bridge used to
/// call that case "default" and wrote default.json, while the app looked for the same environment
/// under the folder's own name. The two never met, so a person with the plainest possible setup
/// would never have seen a limit, and their own status line would never have run either.
///
/// The Windows core builds these paths with Path.Combine, which follows the machine it runs on.
/// Here the platform says how a path is spelled, so a Windows path and a Mac path both come out
/// the way their own system writes them.
public enum ClaudeConfigFolder {
    public static let defaultFolderName = ".claude"

    public static let accountFileName = ".claude.json"

    /// Every Claude Code folder in a home folder: .claude and the .claude-<name> folders beside it.
    public static let searchPattern = defaultFolderName + "*"

    /// The folder of a second environment: .claude-work for "work".
    public static func namedFolderName(_ name: String) -> String { defaultFolderName + "-" + name }

    public static func resolve(
        _ platform: PlatformConventions, _ fromEnvironment: String?, _ userProfile: String
    ) -> String {
        let set = fromEnvironment?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        return set.isEmpty ? platform.join(userProfile, defaultFolderName) : set
    }

    /// Where Claude Code keeps the account facts of a folder.
    ///
    /// Inside the folder, except for the default one: started without CLAUDE_CONFIG_DIR, Claude
    /// Code keeps .claude.json in the home folder beside .claude. Aiko starts environment 1 without
    /// the variable, so that home file is the one that stays current. A copy inside .claude only
    /// exists when somebody once set the variable to it by hand, and it is used only when the home
    /// file is missing.
    public static func accountFileCandidates(
        _ platform: PlatformConventions, _ folder: String, _ userProfile: String
    ) -> [String] {
        let inside = platform.join(folder, accountFileName)
        if !isDefault(platform, folder, userProfile) {
            return [inside]
        }

        return [platform.join(userProfile, accountFileName), inside]
    }

    public static func isDefault(
        _ platform: PlatformConventions, _ folder: String, _ userProfile: String
    ) -> Bool {
        PathText.trimOneEndSeparator(folder)
            .caseInsensitiveCompare(platform.join(userProfile, defaultFolderName)) == .orderedSame
    }
}
