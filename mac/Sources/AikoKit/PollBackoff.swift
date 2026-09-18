import Foundation

/// How long to wait before asking the usage API again. Direct mode only: the status line
/// costs nothing and is never polled.
///
/// The endpoint answers 429 often and with no retry-after at all, and Anthropic closed those
/// reports as not planned — so a client that keeps knocking only makes it worse.
public struct PollBackoff: Sendable, Equatable {
    public let delay: TimeInterval
    public let failuresInARow: Int

    public init(delay: TimeInterval, failuresInARow: Int) {
        self.delay = delay
        self.failuresInARow = failuresInARow
    }

    public static let normal: TimeInterval = 180
    public static let ceiling: TimeInterval = 900

    public static let start = PollBackoff(delay: normal, failuresInARow: 0)

    public func afterSuccess() -> PollBackoff { PollBackoff.start }

    /// A 429 with a retry-after is obeyed, within reason: never shorter than the normal pace,
    /// never longer than the ceiling. Without a usable header the delay doubles.
    public func afterTooManyRequests(_ retryAfter: TimeInterval?) -> PollBackoff {
        if let wait = retryAfter, wait > 0 {
            return PollBackoff(delay: PollBackoff.clamp(wait), failuresInARow: failuresInARow + 1)
        }
        return afterFailure()
    }

    public func afterFailure() -> PollBackoff {
        let failures = failuresInARow + 1
        let doubled = PollBackoff.normal * pow(2, Double(min(failures, 8)))
        return PollBackoff(delay: PollBackoff.clamp(doubled), failuresInARow: failures)
    }

    public func nextAttemptAfter(_ now: Date) -> Date { now.addingTimeInterval(delay) }

    private static func clamp(_ value: TimeInterval) -> TimeInterval {
        value < normal ? normal : value > ceiling ? ceiling : value
    }
}
