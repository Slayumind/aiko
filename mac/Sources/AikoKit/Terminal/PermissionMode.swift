import Foundation

/// How much Claude Code asks before it acts. The four modes the terminal offers (D-302).
///
/// bypassPermissions and dontAsk exist in Claude Code and are left out on purpose: the first
/// turns every question off, the second refuses in silence.
public enum PermissionMode: Sendable, Equatable {
    case manual
    case acceptEdits
    case plan
    case auto
}

/// The names of the modes. They are the words of Claude Code itself, the same in every language
/// of the interface, so the line under the input, the flag and the documentation say one thing.
public enum PermissionModes {
    /// The order Shift+Tab walks through.
    public static let all: [PermissionMode] = [.manual, .acceptEdits, .plan, .auto]

    public static func name(_ mode: PermissionMode) -> String {
        switch mode {
        case .manual: return "manual"
        case .acceptEdits: return "acceptEdits"
        case .plan: return "plan"
        case .auto: return "auto"
        }
    }

    /// The mode a stream event names. The stream still calls the manual mode "default", the flag
    /// calls it "manual"; both are read. Nil for a mode the terminal does not offer.
    public static func fromStream(_ value: String?) -> PermissionMode? {
        switch value {
        case "default", "manual": return .manual
        case "acceptEdits": return .acceptEdits
        case "plan": return .plan
        case "auto": return .auto
        default: return nil
        }
    }

    /// The value of --permission-mode, or nil when the flag is left out. The manual mode is what
    /// Claude Code starts in, and its name differs between versions, so it is never passed.
    public static func flagValue(_ mode: PermissionMode) -> String? {
        mode == .manual ? nil : name(mode)
    }

    /// The value a set_permission_mode request carries. The stream reports the manual mode as
    /// "default", so that is the word sent back.
    public static func requestValue(_ mode: PermissionMode) -> String {
        mode == .manual ? "default" : name(mode)
    }

    public static func next(_ mode: PermissionMode) -> PermissionMode {
        let index = all.firstIndex(of: mode) ?? 0
        return all[(index + 1) % all.count]
    }
}
