import Foundation

/// Where Claude Code is, and where a new environment's folder goes.
public enum ClaudeInstall {
    /// The PATH a terminal opened right now would get: the machine part, then the user part, with
    /// variables expanded. Aiko itself may have started before Claude Code was installed, and its
    /// own PATH is then out of date: the wizard would wait forever for a Claude Code that is there.
    public static func freshPath(
        _ platform: PlatformConventions,
        machinePath: String?,
        userPath: String?,
        expand: (String) -> String
    ) -> String {
        [machinePath, userPath]
            .compactMap { $0 }
            .filter { !$0.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty }
            .map(expand)
            .joined(separator: String(platform.pathListSeparator))
    }

    /// The real Claude Code on that PATH, never Aiko's own shim, or where the native installer puts
    /// it when PATH has not caught up.
    public static func find(
        _ platform: PlatformConventions,
        freshPath: String,
        commandFolder: String,
        userProfile: String,
        exists: (String) -> Bool
    ) -> String? {
        if let onPath = RealClaude.find(platform, freshPath, [commandFolder], exists) {
            return onPath
        }

        let native = platform.join(userProfile, ".local", "bin", RealClaude.executableName(platform))
        return exists(native) ? native : nil
    }

    /// .claude-work for "Work", .claude-osnovnaya for "Основная". A name already taken gets a
    /// number, so a new environment never lands in somebody's existing account folder.
    public static func newConfigFolder(
        _ platform: PlatformConventions,
        environmentName: String,
        userProfile: String,
        folderExists: (String) -> Bool
    ) -> String {
        let slug = LaunchCommand.slug(environmentName)
        let stem = platform.join(userProfile, ClaudeConfigFolder.namedFolderName(slug.isEmpty ? "env" : slug))

        var candidate = stem
        var n = 2
        while folderExists(candidate) {
            candidate = stem + "-" + String(n)
            n += 1
        }

        return candidate
    }

    public static let credentialsFileName = ".credentials.json"

    /// Signed in means Claude Code wrote its credentials file. Only that the file exists is looked
    /// at; it is never opened here.
    public static func credentialsPathIn(_ platform: PlatformConventions, _ configFolder: String) -> String {
        platform.join(configFolder, credentialsFileName)
    }
}
