import Foundation

/// What the colour of the terminal window says about the chosen environment (D-435, D-441).
public enum WindowSignal: Sendable, Equatable {
    /// Nothing needs the person and nothing runs: the window has no colour.
    case quiet
    case working
    case stopped
    case limitNearEnd
    case waiting
}

public enum WindowSignals {
    /// A question has to be answered now, so it goes first. A limit near its end comes next, then a
    /// session that stopped by itself, then work.
    public static func of(_ sessions: [RowState], limit: LimitTone) -> WindowSignal {
        if sessions.contains(.waiting) { return .waiting }
        if limit == .critical { return .limitNearEnd }
        if sessions.contains(.stopped) { return .stopped }
        if sessions.contains(.working) { return .working }
        return .quiet
    }
}
