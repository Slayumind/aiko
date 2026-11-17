import Foundation

/// What one environment brings to the open header of the terminal window: its limits and how many
/// of its sessions wait for the person.
public struct HeaderInput: Sendable {
    public let environment: String
    public let limits: LimitSnapshot?
    public let waiting: Int

    public init(environment: String, limits: LimitSnapshot?, waiting: Int) {
        self.environment = environment
        self.limits = limits
        self.waiting = waiting
    }
}

/// One column of the open header. The active environment is shown in full; the others are a quiet
/// column with two numbers. A number is nil when nothing is known about that window.
public struct HeaderColumn: Sendable, Equatable {
    public let environment: String
    public let active: Bool
    public let fiveHour: Int?
    public let sevenDay: Int?
    public let waiting: Int

    public init(environment: String, active: Bool, fiveHour: Int?, sevenDay: Int?, waiting: Int) {
        self.environment = environment
        self.active = active
        self.fiveHour = fiveHour
        self.sevenDay = sevenDay
        self.waiting = waiting
    }
}

/// The open header of the terminal window (D-373, D-376): the active environment always comes
/// first, the others follow in their own order. The pills above keep their order; only the columns
/// move.
public enum HeaderStrip {
    /// When active names no environment of the list, the first one is active: the window always
    /// has a chosen tab.
    public static func columns(_ environments: [HeaderInput], _ active: String, _ now: Date) -> [HeaderColumn] {
        let chosen = environments.contains { $0.environment == active } ? active : environments.first?.environment
        return environments.filter { $0.environment == chosen }.map { column($0, true, now) }
            + environments.filter { $0.environment != chosen }.map { column($0, false, now) }
    }

    private static func column(_ input: HeaderInput, _ active: Bool, _ now: Date) -> HeaderColumn {
        HeaderColumn(
            environment: input.environment,
            active: active,
            fiveHour: input.limits?.statusAt(now, .fiveHour)?.percent,
            sevenDay: input.limits?.statusAt(now, .sevenDay)?.percent,
            waiting: input.waiting)
    }
}
