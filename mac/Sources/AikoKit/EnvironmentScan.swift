import Foundation

/// A Claude Code config folder as the app found it on disk. The core never looks at the disk
/// itself, so the facts come in already gathered.
public struct ClaudeFolder: Sendable, Equatable {
    public let fullPath: String
    public let folderName: String

    /// Without credentials there is no account in this folder, so there is nothing to show.
    public var hasCredentials: Bool

    /// When the folder was last touched. Used only to put the folders in a sensible order.
    public var lastUsed: Date?

    public init(_ fullPath: String, _ folderName: String, hasCredentials: Bool = false, lastUsed: Date? = nil) {
        self.fullPath = fullPath
        self.folderName = folderName
        self.hasCredentials = hasCredentials
        self.lastUsed = lastUsed
    }
}

/// A folder Aiko is ready to offer as an environment, with a name to start from.
public struct FoundEnvironment: Sendable, Equatable {
    public let suggestedName: String
    public let fullPath: String
    public var lastUsed: Date?

    public init(_ suggestedName: String, _ fullPath: String, lastUsed: Date? = nil) {
        self.suggestedName = suggestedName
        self.fullPath = fullPath
        self.lastUsed = lastUsed
    }
}

/// Which of the folders on disk are worth offering as environments, and what to call them.
///
/// Aiko never decides which account is the "real" one. Two folders can hold exactly the same files
/// while one holds expired tokens and the other is in daily use: nothing on disk tells them apart.
/// So the list is offered, and the person names and picks.
public enum EnvironmentScan {
    /// A folder in daily use has been touched recently. Only used for the order of the list.
    public static let recentlyUsed: TimeInterval = 30 * 86400

    /// The name for a folder with nothing after "claude", when the caller does not give one in the
    /// user's language.
    public static let plainFolderName = "Main"

    public static func pick(
        _ folders: [ClaudeFolder], _ plainFolderName: String = EnvironmentScan.plainFolderName
    ) -> [FoundEnvironment] {
        folders
            .filter(\.hasCredentials)
            .enumerated()
            .sorted { left, right in
                let a = left.element.lastUsed ?? Date.distantPast
                let b = right.element.lastUsed ?? Date.distantPast
                if a != b {
                    return a > b
                }
                let byName = left.element.folderName.caseInsensitiveCompare(right.element.folderName)
                return byName == .orderedSame ? left.offset < right.offset : byName == .orderedAscending
            }
            .map { folder in
                FoundEnvironment(
                    suggestName(folder.element.folderName, plainFolderName),
                    folder.element.fullPath,
                    lastUsed: folder.element.lastUsed)
            }
    }

    /// ".claude-personal" becomes "Personal", ".claude_work" becomes "Work", and a plain ".claude"
    /// gets the plain name, "Main" unless the caller passes it in the user's language. The core
    /// has no language, so the word comes from outside. It is only a starting point: the name
    /// belongs to the user.
    public static func suggestName(
        _ folderName: String, _ plainFolderName: String = EnvironmentScan.plainFolderName
    ) -> String {
        var name = folderName
        while name.first == "." { name.removeFirst() }

        if name.lowercased().hasPrefix("claude") {
            name = String(name.dropFirst("claude".count))
        }

        while let first = name.first, first == "-" || first == "_" || first == " " { name.removeFirst() }
        while let last = name.last, last == "-" || last == "_" || last == " " { name.removeLast() }

        if name.isEmpty {
            return plainFolderName
        }

        return name.prefix(1).uppercased() + name.dropFirst()
    }
}
