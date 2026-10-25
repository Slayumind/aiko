import AikoKit
import Foundation

/// What the terminal reads from disk besides its own list: the file Claude Code keeps of a
/// conversation, and the folders the form of a new session offers.
///
/// Nothing here throws, and nothing here writes. A file that cannot be read is an empty feed.
///
/// The twin of Terminal/TerminalStore.cs.
enum TerminalStore {
    /// The mode Claude Code would start in for this folder and config folder (D-311): the local
    /// settings of the project, its shared settings, then the settings of the config folder.
    static func defaultMode(folder: String, configDirectory: String) -> PermissionMode {
        PermissionModes.defaultMode([
            try? String(contentsOfFile: "\(folder)/.claude/settings.local.json", encoding: .utf8),
            try? String(contentsOfFile: "\(folder)/.claude/settings.json", encoding: .utf8),
            try? String(contentsOfFile: "\(configDirectory)/settings.json", encoding: .utf8),
        ])
    }

    /// The file of a conversation, or nil when Claude Code has none.
    static func transcriptFile(configDirectory: String, folder: String, sessionId: String) -> String? {
        let manager = FileManager.default
        let exact = TranscriptPath.forSession(
            .macOS, configDirectory: configDirectory, workingDirectory: folder, sessionId: sessionId)
        if manager.fileExists(atPath: exact) {
            return exact
        }

        guard TranscriptPath.isCut(folder) else {
            return nil
        }

        // The name was cut, and Claude Code added characters of its own to it.
        let projects = TranscriptPath.projectsFolder(.macOS, configDirectory)
        let start = TranscriptPath.folderName(folder) + "-"
        return ((try? manager.contentsOfDirectory(atPath: projects)) ?? [])
            .filter { $0.hasPrefix(start) }
            .sorted()
            .map { "\(projects)/\($0)/\(TranscriptPath.fileName(sessionId))" }
            .first { manager.fileExists(atPath: $0) }
    }

    /// The feed of a conversation as Claude Code has it on disk (D-303). Read into memory and
    /// kept nowhere else.
    static func history(
        configDirectory: String, folder: String, sessionId: String, mode: PermissionMode
    ) -> Conversation {
        guard let file = transcriptFile(configDirectory: configDirectory, folder: folder, sessionId: sessionId),
              let text = try? String(contentsOfFile: file, encoding: .utf8)
        else {
            return Conversation.empty(mode: mode)
        }

        return Transcript.read(text.split(separator: "\n", omittingEmptySubsequences: true).map(String.init), mode: mode)
    }

    /// The folders right inside one, the ones changed last first, without the hidden ones (D-335).
    static func foldersInside(_ folder: String) -> [String] {
        let inside = (try? FileManager.default.contentsOfDirectory(
            at: URL(fileURLWithPath: folder),
            includingPropertiesForKeys: [.isDirectoryKey, .contentModificationDateKey],
            options: [.skipsHiddenFiles])) ?? []

        func changed(_ url: URL) -> Date {
            (try? url.resourceValues(forKeys: [.contentModificationDateKey]).contentModificationDate) ?? .distantPast
        }

        return inside
            .filter { (try? $0.resourceValues(forKeys: [.isDirectoryKey]).isDirectory) == true }
            .sorted { changed($0) > changed($1) }
            .map(\.path)
    }
}
