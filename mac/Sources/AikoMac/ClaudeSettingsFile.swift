import AikoKit
import Foundation

/// The real disk, behind the narrow interface the core asks for. The twin of RealFiles in
/// ClaudeSettingsFile.cs.
struct RealFiles: FileAccess {
    static let shared = RealFiles()

    func exists(_ path: String) throws -> Bool {
        FileManager.default.fileExists(atPath: path)
    }

    func readAllText(_ path: String) throws -> String {
        try String(contentsOfFile: path, encoding: .utf8)
    }

    func writeAllText(_ path: String, _ text: String) throws {
        try text.write(toFile: path, atomically: false, encoding: .utf8)
    }

    func copy(_ from: String, _ to: String) throws {
        try FileManager.default.copyItem(atPath: from, toPath: to)
    }

    func move(_ from: String, _ to: String) throws {
        if FileManager.default.fileExists(atPath: to) {
            try FileManager.default.removeItem(atPath: to)
        }
        try FileManager.default.moveItem(atPath: from, toPath: to)
    }

    func delete(_ path: String) throws {
        try FileManager.default.removeItem(atPath: path)
    }
}

/// Aiko's line in the settings file of one Claude Code environment.
///
/// The rules live in AikoKit.ClaudeSettingsEditor, where a test can reach them. What is left here
/// is the wiring: the real file system, the shell of this platform, and the log.
enum ClaudeSettingsFile {
    private static let patch = SettingsJsonPatch(.macOS)

    private static var editor: ClaudeSettingsEditor {
        ClaudeSettingsEditor(files: RealFiles.shared, patch: patch)
    }

    /// macOS has one shell for this: `ClaudeShellLookup` with no Git Bash path always answers with
    /// the fallback of the platform, /bin/zsh -lc.
    static let shell = ClaudeShellLookup.shellFor(.macOS, nil)

    static func pathIn(_ configDirectory: String) -> String {
        ClaudeSettingsEditor.pathIn(configDirectory)
    }

    /// The exact text Aiko would add. The checklist shows it before asking, so nobody has to take
    /// our word for what we write into their file.
    static func lineFor(_ bridgePath: String) -> String {
        BridgeCommand.forPath(bridgePath, shell)
    }

    /// Whether our line is in this folder's settings. Without it Claude Code reports nothing and no
    /// number can ever arrive, which the card has to be able to say out loud.
    static func hasOurLine(_ configDirectory: String) -> Bool {
        guard let text = read(pathIn(configDirectory)) else { return false }
        return patch.hasOurLine(text)
    }

    /// The person's own output style, which the persona would sit beside. Read for the warning on
    /// the personality page.
    static func userOutputStyle(_ configDirectory: String) -> String? {
        guard let text = read(pathIn(configDirectory)) else { return nil }
        return SettingsJsonPatch.userOutputStyle(text)
    }

    static func addBridge(_ configDirectory: String, _ bridgePath: String) -> PatchOutcome {
        report(configDirectory, editor.add(configDirectory, lineFor(bridgePath)))
    }

    static func addSessionHook(_ configDirectory: String, _ bridgePath: String) -> PatchOutcome {
        report(configDirectory, editor.addSessionHook(configDirectory, BridgeCommand.hookFor(bridgePath, shell)))
    }

    /// Aiko's line, its hook, its plugins and its marketplace (D-205).
    static func removeAiko(_ configDirectory: String) -> PatchOutcome {
        report(configDirectory, editor.remove(configDirectory))
    }

    static func tidyAfterPluginRemoval(_ configDirectory: String) -> PatchOutcome {
        report(configDirectory, editor.tidyAfterPluginRemoval(configDirectory))
    }

    /// The reason in the user's language. The core names the reason; the words live here.
    static func words(_ problem: PatchProblem) -> String {
        switch problem {
        case .bridgeUnknown: return Strings.settingsBridgeUnknown
        case .couldNotWrite: return Strings.settingsWriteFailed
        case .none: return ""
        }
    }

    private static func read(_ path: String) -> String? {
        try? String(contentsOfFile: path, encoding: .utf8)
    }

    private static func report(_ configDirectory: String, _ outcome: PatchOutcome) -> PatchOutcome {
        let folder = (configDirectory as NSString).lastPathComponent
        if outcome.changed {
            Log.write("settings.json changed in \(folder)")
        } else if outcome.problem != .none {
            Log.write("could not change settings.json in \(folder): \(outcome.problem)")
        }

        return outcome
    }
}

/// Where the bridge program is. The checklist writes this path into the Claude Code settings, so it
/// has to be the path that will still work tomorrow.
///
/// In a bundle it sits beside the app binary under the name BridgeCommand.isAiko reads a status
/// line by. A build run straight from .build has it under the name SwiftPM gave it.
enum BridgePath {
    static func current() -> String? {
        let folder = (Bundle.main.executablePath.map { ($0 as NSString).deletingLastPathComponent })
            ?? (CommandLine.arguments.first.map { ($0 as NSString).deletingLastPathComponent })

        guard let folder else { return nil }

        for name in [BridgeCommand.programName, "aiko-bridge"] {
            let path = (folder as NSString).appendingPathComponent(name)
            if FileManager.default.isExecutableFile(atPath: path) {
                return path
            }
        }

        return nil
    }
}
