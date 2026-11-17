import Foundation

/// What Cmd and a key do in the terminal window (D-353, D-425), with Shift or without. none leaves
/// the key to the program in the terminal. The twin of TerminalKeys.cs, where the key is Ctrl.
public enum TerminalKeyAction: String, Sendable, Equatable {
    case none
    case toggleSettings
    case compose
    case paste
    case copy
    case zoomIn
    case zoomOut
    case zoomReset
    case newSessionQuickly
    case palette
    case find
    case toggleList
    case nextSession
    case previousSession
    case voice
}

/// What the window knows when a key comes.
public struct TerminalKeyState: Sendable, Equatable {
    public let settingsOpen: Bool
    public let claudeSession: Bool
    public let inTerminal: Bool
    public let hasSelection: Bool

    public init(settingsOpen: Bool, claudeSession: Bool, inTerminal: Bool, hasSelection: Bool) {
        self.settingsOpen = settingsOpen
        self.claudeSession = claudeSession
        self.inTerminal = inTerminal
        self.hasSelection = hasSelection
    }
}

/// The table of the keys of the terminal window. Keys are named without the platform: a letter, a
/// digit, or Comma, Plus, Minus, Tab, Space. Rows: spec/cases/terminal-keys.
public enum TerminalKeys {
    public static func action(_ key: String, shift: Bool, _ state: TerminalKeyState) -> TerminalKeyAction {
        // Cmd+, opens the settings and closes them (D-374).
        if key == "Comma" && !shift {
            return .toggleSettings
        }

        // While the settings are open the keys of the sessions wait; zoom still works.
        if state.settingsOpen && !["Plus", "Minus", "0"].contains(key) {
            return .none
        }

        switch (key, shift) {
        // A long text for Claude Code in the editor (D-370).
        case ("G", false) where state.claudeSession: return .compose
        // A picture goes as a picture, a file as its path, text as text (D-370).
        case ("V", false) where state.inTerminal: return .paste
        // With a selection the key copies; without one it goes to the program.
        case ("C", false) where state.inTerminal && state.hasSelection: return .copy
        case ("C", true): return .copy
        case ("Plus", _): return .zoomIn
        case ("Minus", false): return .zoomOut
        case ("0", false): return .zoomReset
        case ("T", true): return .newSessionQuickly
        case ("K", true): return .palette
        // The search in the output of a command line (D-358).
        case ("F", true): return .find
        case ("B", true): return .toggleList
        case ("Tab", false): return .nextSession
        case ("Tab", true): return .previousSession
        case ("Space", true): return .voice
        default: return .none
        }
    }
}
