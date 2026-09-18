import Foundation

/// The name of the file one environment reports into.
///
/// The bridge only knows the config folder it was started for, so the file is named after that
/// folder. The tray has to work out the same name to match a file with the environment the user
/// named, and two copies of this rule would drift apart. So it lives here, and the bridge calls
/// it too.
public enum SnapshotName {
    /// Claude Code started without CLAUDE_CONFIG_DIR uses its own default folder.
    public static let `default` = "default"

    /// `SnapshotName.For` on Windows; `for` is a keyword in Swift.
    public static func forConfigDirectory(_ configDirectory: String?) -> String {
        guard let configDirectory,
              !configDirectory.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else {
            return SnapshotName.default
        }

        return clean(lastFolderName(configDirectory))
    }

    /// The last part of the path, cut by hand. A path helper follows the separators of the computer
    /// it runs on, so the same Windows path would give another name on a Mac. A drive such as "C:"
    /// counts as a separator too, the way Windows reads it.
    private static func lastFolderName(_ path: String) -> String {
        let trimmed = PathText.trimEndSeparators(path)
        guard let index = trimmed.lastIndex(where: { $0 == "\\" || $0 == "/" || $0 == ":" }) else {
            return trimmed
        }
        return String(trimmed[trimmed.index(after: index)...])
    }

    /// A file name, not a folder name: the leading dot goes, and anything that does not belong in
    /// a file name goes with it.
    public static func clean(_ folderName: String) -> String {
        if folderName.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
            return SnapshotName.default
        }

        var kept = String(folderName.filter { $0.isLetter || $0.isNumber || $0 == "-" || $0 == "_" || $0 == "." })
        while kept.first == "." { kept.removeFirst() }
        while kept.last == "." { kept.removeLast() }

        return kept.isEmpty ? SnapshotName.default : kept
    }
}
