import Foundation

/// Where Claude Code keeps the file of a conversation (D-303):
/// <config folder>/projects/<name made of the working folder>/<id>.jsonl.
///
/// The name of the folder is the path with every character that is not an English letter or a
/// digit turned into a dash: C:\Users\x\a b gives C--Users-x-a-b. Checked on Claude Code 2.1.296;
/// it is not documented.
///
/// The twin of TranscriptPath.cs. A character is a UTF-16 unit here, as in C# and in Claude Code
/// itself, so one smiling face gives two dashes.
public enum TranscriptPath {
    public static let projectsFolderName = "projects"

    /// A longer name is cut here by Claude Code, which then adds a dash and a few characters of
    /// its own. Those cannot be worked out, so the folder has to be looked for by its start.
    public static let longestName = 200

    public static func folderName(_ workingDirectory: String) -> String {
        let dash: UInt16 = 45
        let units = workingDirectory.utf16.map { unit -> UInt16 in
            let isLetter = (97...122).contains(unit) || (65...90).contains(unit)
            return isLetter || (48...57).contains(unit) ? unit : dash
        }
        return String(decoding: units.prefix(longestName), as: UTF16.self)
    }

    /// Whether the folder on disk has more at its end than folderName gives.
    public static func isCut(_ workingDirectory: String) -> Bool {
        workingDirectory.utf16.count > longestName
    }

    public static func fileName(_ sessionId: String) -> String {
        sessionId + ".jsonl"
    }

    public static func projectsFolder(_ platform: PlatformConventions, _ configDirectory: String) -> String {
        platform.join(configDirectory, projectsFolderName)
    }

    /// The file of a conversation, when the name of its folder was not cut.
    public static func forSession(
        _ platform: PlatformConventions, configDirectory: String, workingDirectory: String, sessionId: String
    ) -> String {
        platform.join(configDirectory, projectsFolderName, folderName(workingDirectory), fileName(sessionId))
    }
}
