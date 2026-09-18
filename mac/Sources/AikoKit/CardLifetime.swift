import Foundation

/// When the card opens and when it goes away. The rules of AikoShell.cs, with no window in sight,
/// so they can be tested.
///
/// A card opened by resting the mouse on the icon goes away when the mouse goes away. A card the
/// user clicked for, or clicked on, stays until they close it. Before, every card stayed: running
/// the mouse along the menu bar left a window that had to be dismissed by hand, which is a strange
/// price for a glance.
public enum CardLifetime {
    /// Long enough that running the mouse along the menu bar does not open the card.
    public static let hoverDelay = 1.5

    /// How often the pointer is looked at while an unpinned card is on screen.
    public static let watchInterval = 0.250
}

/// Counts the turns the pointer has been away from both the icon and the card.
///
/// Nothing says plainly that the pointer left the icon, and there is a gap between the icon and
/// the card that the pointer crosses on its way in. So the pointer is looked at instead, and it
/// has to be away for two turns in a row before the card goes.
public struct AwayWatch: Sendable, Equatable {
    public static let turnsToClose = 2

    public private(set) var awayTurns = 0

    public init() {}

    /// One turn of the watch. Answers true when the card should close now.
    public mutating func turn(pointerIsHome: Bool) -> Bool {
        if pointerIsHome {
            awayTurns = 0
            return false
        }

        awayTurns += 1
        return awayTurns >= AwayWatch.turnsToClose
    }
}
