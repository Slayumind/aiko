import Foundation

/// How the word of the context looks: quiet up to 75 %, orange up to 90 %, red above (D-345).
public enum ContextLevel: String, Sendable, Equatable {
    case quiet
    case warm
    case hot
}

/// How much of its context window the conversation fills.
///
/// The size comes from the stream, with no request of its own: the tokens of the last message of
/// Claude are what the next call sends again, and the window is in the result of the turn. The
/// spike of 2026-10-27 found this within 0.02 % of what /context says (RESEARCH).
public enum ContextGauge {
    /// Nil until both numbers are known.
    public static func percent(tokens: Int, window: Int) -> Int? {
        guard tokens > 0, window > 0 else { return nil }
        // Rounded half away from zero, as Math.Round with AwayFromZero.
        let share = (Double(tokens) * 100 / Double(window)).rounded(.toNearestOrAwayFromZero)
        return Int(min(max(share, 0), 100))
    }

    public static func level(_ percent: Int) -> ContextLevel {
        percent <= 75 ? .quiet : percent <= 90 ? .warm : .hot
    }

    /// The window of the model in use. Claude Code names the model with a suffix at times
    /// ("claude-opus-5-5[1m]"), so its name only has to start with the key. When no key fits and
    /// there is just one, that one is the model; otherwise the window stays as it was.
    public static func windowFor(model: String, windows: [ModelWindow], known: Int) -> Int {
        if let fit = windows.first(where: { !$0.model.isEmpty && model.hasPrefix($0.model) }) {
            return fit.window
        }
        return windows.count == 1 ? windows[0].window : known
    }
}
