import Foundation

/// What the diagnostics button copies. Everything a bug report needs and nothing it does not: no
/// tokens, no email addresses, no numbers from the limits, no paths from Claude Code.
public struct DiagnosticsFacts: Sendable, Equatable {
    public var version = ""
    public var system = ""
    public var place = AikoPlace.tray
    public var language = AikoLanguage.system
    public var startsAtLogin = false
    public var checksUpdates = false
    public var sendsStats = false
    public var environments = 0
    public var directMode = 0
    public var boundFolders = 0
    public var commandsSetUp = false
    public var shell = ""
    public var logPath = ""

    public init() {}
}

/// The twin of GeneralPage.OnCopyDiagnostics on Windows. Written for a person to paste into a bug
/// report, so it says yes and no rather than true and false, and it is English in every language.
public enum DiagnosticsText {
    public static func build(_ facts: DiagnosticsFacts) -> String {
        [
            "Aiko \(facts.version)",
            facts.system,
            "shown in: \(facts.place == .island ? "the island" : "the menu bar")",
            "language: \(languageName(facts.language))",
            "starts at login: \(yesNo(facts.startsAtLogin))",
            "checks for updates: \(yesNo(facts.checksUpdates))",
            "sends statistics: \(yesNo(facts.sendsStats))",
            "environments: \(facts.environments)"
                + ", direct mode on for \(facts.directMode)"
                + ", bound folders \(facts.boundFolders)",
            "launch commands set up: \(yesNo(facts.commandsSetUp))",
            "status line shell: \(facts.shell)",
            "log: \(facts.logPath)",
        ].joined(separator: "\n")
    }

    private static func yesNo(_ value: Bool) -> String { value ? "yes" : "no" }

    private static func languageName(_ language: AikoLanguage) -> String {
        switch language {
        case .english: return "English"
        case .russian: return "Русский"
        case .system: return "the one macOS uses"
        }
    }
}
