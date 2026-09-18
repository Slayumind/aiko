import Foundation

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

    public static func messageFor(
        _ platform: PlatformConventions,
        launch: String?,
        runningEnvironment: String?,
        workingDirectory: String,
        settings: EnvironmentSettings,
        russian: Bool
    ) -> String? {
        guard launch == launchedByCommand,
              let running = runningEnvironment,
              !running.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else {
            return nil
        }

        let found = ProjectBinding.boundEnvironmentFor(
            platform, workingDirectory: workingDirectory, settings: settings)
        guard let bound = found?.name, bound != running else {
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

    /// The same answer on macOS, where the system says its language as a tag such as "ru-RU".
    /// Windows hands out a number instead, so the two bridges ask this in their own way.
    public static func isRussian(_ language: AikoLanguage, systemLanguageTag: String?) -> Bool {
        if language == .russian {
            return true
        }

        guard language == .system, let tag = systemLanguageTag else {
            return false
        }

        let base = tag.prefix { $0 != "-" && $0 != "_" }
        return base.caseInsensitiveCompare("ru") == .orderedSame
    }

    /// The working folder from the JSON Claude Code gives a hook on stdin.
    public static func workingDirectoryIn(_ hookInput: String) -> String? {
        JsonNode.parse(hookInput)?.objectValue?["cwd"]?.stringValue
    }
}
