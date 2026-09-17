import Foundation

public enum CountdownUnit: Sendable, Equatable {
    case lessThanMinute
    case minutes
    case hoursAndMinutes
    case daysAndHours
}

/// Time left until a limit window resets, already split the way the card shows it.
/// The core decides what to show; the words come from the UI, so both languages stay out of here.
public struct ResetCountdown: Sendable, Equatable {
    public let isReset: Bool
    public let days: Int
    public let hours: Int
    public let minutes: Int
    public let unit: CountdownUnit

    public init(isReset: Bool, days: Int, hours: Int, minutes: Int, unit: CountdownUnit) {
        self.isReset = isReset
        self.days = days
        self.hours = hours
        self.minutes = minutes
        self.unit = unit
    }

    public static let reset = ResetCountdown(
        isReset: true, days: 0, hours: 0, minutes: 0, unit: .lessThanMinute)

    public static func between(_ now: Date, _ resetsAt: Date) -> ResetCountdown {
        let left = resetsAt.timeIntervalSince(now)
        if left <= 0 {
            return reset
        }

        let seconds = Int(left)
        let days = seconds / 86400
        let hours = (seconds / 3600) % 24
        let minutes = (seconds / 60) % 60

        // A window that ends in 3h 34m 50s reads as "3h 34m": we round down, never promise more.
        let unit: CountdownUnit = days > 0
            ? .daysAndHours
            : hours > 0
                ? .hoursAndMinutes
                : minutes > 0
                    ? .minutes
                    : .lessThanMinute

        return ResetCountdown(isReset: false, days: days, hours: hours, minutes: minutes, unit: unit)
    }
}
