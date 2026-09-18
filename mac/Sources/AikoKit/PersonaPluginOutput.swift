import Foundation

/// Where the bridge puts the persona plugin it builds, and which old copies it clears (D-201).
///
/// Claude Code runs the bridge with "plugin aiko-persona", copies the folder the bridge prints into
/// its own cache and never reads the folder again. Each content gets its own folder named after
/// its hash, so a changed persona never half-overwrites a folder Claude Code may be copying.
public enum PersonaPluginOutput {
    public static let verb = "plugin"

    /// How long an old folder stays. Another session of the same environment may be copying it
    /// right now; an hour is far longer than any copy takes.
    public static let keepOldFor: TimeInterval = 3600

    /// One folder per environment, named like its limit snapshot, so the two never disagree.
    public static func environmentFolder(_ folders: AikoFolders, _ configDirectory: String) -> String {
        folders.platform.join(folders.personaPluginsFolder, SnapshotName.forConfigDirectory(configDirectory))
    }

    public static func versionFolder(
        _ folders: AikoFolders, _ configDirectory: String, _ contentHash: String
    ) -> String {
        folders.platform.join(environmentFolder(folders, configDirectory), contentHash)
    }

    /// Whether the command asks for a plugin this bridge can build.
    public static func isRequest(_ args: [String]) -> Bool {
        args == [verb, PersonaPlugin.name]
    }

    /// Old folders to delete: every other version older than keepOldFor. Folders that are not a
    /// version at all, such as a temporary folder of a build still running, are left alone.
    public static func staleFolders(
        _ folders: [(name: String, written: Date)], currentHash: String, now: Date
    ) -> [String] {
        folders
            .filter { $0.name != currentHash && isHash($0.name) && now.timeIntervalSince($0.written) > keepOldFor }
            .map(\.name)
    }

    private static func isHash(_ name: String) -> Bool {
        name.count == 12 && name.allSatisfy { $0.isNumber && $0.isASCII || ("a"..."f").contains($0) }
    }
}
