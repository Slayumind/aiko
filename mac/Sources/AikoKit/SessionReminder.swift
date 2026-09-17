import Foundation

/// Which language the interface speaks. The settings file is not ported yet, so the enum lives
/// here until AppSettings arrives.
public enum AikoLanguage: Sendable, Equatable {
    case system
    case english
    case russian
}

/// The line Claude Code shows at the start of a session when a command overrode a folder binding.
///
/// An explicit command wins over a binding, and the person may mean it (D-159). But limits are
/// money, so Claude Code says whose limits are being spent. The shim leaves AIKO_LAUNCH and
/// AIKO_ENVIRONMENT for the hook to read.
///
/// These two sentences live here and not in tools/strings.json: the bridge prints them, and the
/// bridge has no resources of the app. The copy was agreed on the artifact (D-163).
public enum SessionReminder {
    public static let argument = "--session-start"
    public static let launchVariable = "AIKO_LAUNCH"
    public static let environmentVariable = "AIKO_ENVIRONMENT"
    public static let launchedByCommand = "command"

    /// ProjectBinding is not ported yet, so the environment the folder belongs to comes in as a
    /// name instead of being looked up here.
    public static func messageFor(
        launch: String?,
        runningEnvironment: String?,
        boundEnvironment: String?,
        russian: Bool
    ) -> String? {
        guard launch == launchedByCommand,
              let running = runningEnvironment,
              !running.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else {
            return nil
        }

        guard let bound = boundEnvironment, bound != running else {
            return nil
        }

        return russian
            ? "Aiko: эта папка относится к \(bound), а сессия запущена в \(running). Лимиты тратятся из \(running)."
            : "Aiko: this folder belongs to \(bound), but this session runs in \(running), so the limits of \(running) are used."
    }

    /// The hook answer Claude Code reads. systemMessage is shown to the person; plain output of a
    /// SessionStart hook would go to the model instead.
    public static func hookOutput(_ message: String) -> String {
        JsonNode.object(JsonObject([("systemMessage", .string(message))])).toJsonString()
    }

    /// Russian when chosen in settings, or when settings follow the system and the system speaks
    /// Russian. The Windows bridge gets a language id, because it runs with invariant globalization.
    public static func isRussian(_ language: AikoLanguage, _ systemLanguageId: Int) -> Bool {
        language == .russian || (language == .system && (systemLanguageId & 0x3FF) == 0x19)
    }

    /// The working folder from the JSON Claude Code gives a hook on stdin.
    public static func workingDirectoryIn(_ hookInput: String) -> String? {
        JsonNode.parse(hookInput)?.objectValue?["cwd"]?.stringValue
    }
}
