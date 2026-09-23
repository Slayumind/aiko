import Foundation

public enum LimitSource: Sendable, Equatable {
    case statusLine
    case directMode
}

public enum DataFreshness: Sendable, Equatable {
    case none
    case live
    case stale
}

public struct LimitStatus: Sendable, Equatable {
    public let kind: LimitKind
    public let percent: Int
    public let isReset: Bool
    public let countdown: ResetCountdown

    public init(kind: LimitKind, percent: Int, isReset: Bool, countdown: ResetCountdown) {
        self.kind = kind
        self.percent = percent
        self.isReset = isReset
        self.countdown = countdown
    }
}

/// What we know about one environment right now: the windows, where they came from and when.
/// The card and the icon ask this type, not the parsers.
public struct LimitSnapshot: Sendable, Equatable {
    public let environment: String
    public let source: LimitSource
    public let receivedAt: Date
    public let windows: [LimitWindow]

    /// The model limit is only known in direct mode: the status line does not carry it.
    public var model: ModelLimit?

    public init(
        environment: String,
        source: LimitSource,
        receivedAt: Date,
        windows: [LimitWindow],
        model: ModelLimit? = nil
    ) {
        self.environment = environment
        self.source = source
        self.receivedAt = receivedAt
        self.windows = windows
        self.model = model
    }

    /// The status line only speaks while a session is open, so silence is normal, not a failure.
    /// After this long the numbers are shown greyed out and marked with their time.
    public static let defaultStaleAfter: TimeInterval = 15 * 60

    public static func noData(_ environment: String) -> LimitSnapshot {
        LimitSnapshot(environment: environment, source: .statusLine, receivedAt: .distantPast, windows: [])
    }

    public var hasData: Bool { !windows.isEmpty }

    public static func fromStatusLine(
        _ environment: String, _ receivedAt: Date, _ report: StatusLineReport
    ) -> LimitSnapshot {
        report.hasData
            ? LimitSnapshot(environment: environment, source: .statusLine, receivedAt: receivedAt, windows: report.windows)
            : noData(environment)
    }

    public static func fromDirectMode(
        _ environment: String, _ receivedAt: Date, _ report: UsageReport
    ) -> LimitSnapshot {
        report.hasData
            ? LimitSnapshot(
                environment: environment,
                source: .directMode,
                receivedAt: receivedAt,
                windows: report.windows,
                model: report.model)
            : noData(environment)
    }

    /// Two sources for one environment: the fresher answer wins. Direct mode is asked rarely,
    /// the status line speaks on every reply, so neither is always ahead.
    /// Only direct mode knows the model limit. If the status line wins, it keeps the model limit
    /// of the other answer: the limit has its own reset time, so it does not get old with the windows.
    public static func newer(_ first: LimitSnapshot, _ second: LimitSnapshot) -> LimitSnapshot {
        if !first.hasData {
            return second
        }
        if !second.hasData {
            return first
        }
        var (winner, other) = second.receivedAt > first.receivedAt ? (second, first) : (first, second)
        if winner.model == nil {
            winner.model = other.model
        }
        return winner
    }

    public func freshnessAt(_ now: Date, staleAfter: TimeInterval? = nil) -> DataFreshness {
        if !hasData {
            return .none
        }
        return now.timeIntervalSince(receivedAt) > (staleAfter ?? LimitSnapshot.defaultStaleAfter) ? .stale : .live
    }

    /// A window whose reset time has passed reads as zero, without asking anyone.
    /// Usage only grows while you work, and working makes Claude Code report again — so old
    /// numbers stay true until the window turns over, and then they are simply gone.
    /// When this window turns over, as reported. The card needs it for the pace estimate:
    /// how much of the window is already gone decides whether the limit lasts until the reset.
    public func windowResetsAt(_ kind: LimitKind) -> Date? {
        windows.first { $0.kind == kind }?.resetsAt
    }

    public func statusAt(_ now: Date, _ kind: LimitKind) -> LimitStatus? {
        for window in windows where window.kind == kind {
            let countdown = ResetCountdown.between(now, window.resetsAt)
            return countdown.isReset
                ? LimitStatus(kind: kind, percent: 0, isReset: true, countdown: .reset)
                : LimitStatus(kind: kind, percent: window.percent, isReset: false, countdown: countdown)
        }
        return nil
    }
}
