import Foundation

/// When the island opens by itself (D-161): for a moment, when a session limit crosses 75 % or 90 %
/// on the way up. The numbers on the island are then in front of the person without a hover.
public enum IslandReveal {
    public static let showFor: TimeInterval = 2

    /// True when a ring went from normal to caution, or from normal or caution to critical, between
    /// two sets of numbers. Going down after a reset is good news and says nothing. Numbers that were
    /// not known before, on the first report or after they went stale, do not count as a crossing:
    /// the island would otherwise open every time Claude Code starts talking again.
    public static func toneRose(_ before: [CardState], _ after: [CardState]) -> Bool {
        after.contains { card in
            guard let previous = before.first(where: { $0.environment == card.environment }) else {
                return false
            }
            let was = rank(previous.iconRow?.tone)
            return was > 0 && rank(card.iconRow?.tone) > was
        }
    }

    private static func rank(_ tone: LimitTone?) -> Int {
        switch tone {
        case .normal: return 1
        case .caution: return 2
        case .critical: return 3
        default: return 0
        }
    }
}
