import Foundation

public enum LimitTone: Sendable, Equatable {
    /// No numbers at all: Claude Code has not reported yet.
    case unknown
    case normal
    case caution
    case critical
}

public enum PaceVerdict: Sendable, Equatable {
    /// Nothing to estimate: no data, or the window has just reset.
    case unknown
    case lastsPastReset
    case runsOutBeforeReset
}

/// How much is left at the current pace, and until when.
public struct PaceEstimate: Sendable, Equatable {
    public let verdict: PaceVerdict
    public let timeLeft: TimeInterval

    public init(verdict: PaceVerdict, timeLeft: TimeInterval) {
        self.verdict = verdict
        self.timeLeft = timeLeft
    }
}

/// One row of the card: a window, its colour, its countdown and the estimate.
public struct CardRow: Sendable, Equatable {
    public let kind: LimitKind
    public let percent: Int
    public let tone: LimitTone
    public let isReset: Bool
    public let countdown: ResetCountdown
    public let pace: PaceEstimate

    /// Only for the weekly limit of a heavy model, and only because the server names it. The card
    /// used to say "Fable" whatever came back, which would be wrong the day that changes.
    public let modelName: String?

    public init(
        kind: LimitKind,
        percent: Int,
        tone: LimitTone,
        isReset: Bool,
        countdown: ResetCountdown,
        pace: PaceEstimate,
        modelName: String? = nil
    ) {
        self.kind = kind
        self.percent = percent
        self.tone = tone
        self.isReset = isReset
        self.countdown = countdown
        self.pace = pace
        self.modelName = modelName
    }
}

/// What the card and the icon show for one environment. Everything that decides what to show
/// lives here, so the Windows layer only draws and the macOS layer only draws too.
public struct CardState: Sendable, Equatable {
    public let environment: String
    public let freshness: DataFreshness
    public let updatedAt: Date?
    public let rows: [CardRow]

    /// Until when the card may say "working now". Only the status line counts: a direct mode answer
    /// comes from Aiko asking, not from a session doing anything.
    public var workingUntil: Date?

    public init(
        environment: String,
        freshness: DataFreshness,
        updatedAt: Date?,
        rows: [CardRow],
        workingUntil: Date? = nil
    ) {
        self.environment = environment
        self.freshness = freshness
        self.updatedAt = updatedAt
        self.rows = rows
        self.workingUntil = workingUntil
    }

    public static let cautionFrom = 75
    public static let criticalFrom = 90

    /// The windows Claude Code reports, and how long each one lasts. Needed for the pace estimate:
    /// the share of the window already spent is compared with the share of the limit already used.
    public static let fiveHourWindow: TimeInterval = 5 * 3600
    public static let sevenDayWindow: TimeInterval = 7 * 86400

    /// How long after the last word from the status line a session counts as working. Claude Code
    /// runs the status line as the conversation changes, so an idle or closed session goes quiet;
    /// two minutes rides over a long tool call without calling a closed one "working" for long.
    public static let workingFor: TimeInterval = 120

    public func isWorkingAt(_ now: Date) -> Bool {
        guard let workingUntil else { return false }
        return workingUntil > now
    }

    public static func workingUntilFor(_ snapshot: LimitSnapshot) -> Date? {
        snapshot.source == .statusLine && !snapshot.windows.isEmpty
            ? snapshot.receivedAt.addingTimeInterval(workingFor)
            : nil
    }

    public var hasData: Bool { !rows.isEmpty }

    /// What the ring or the dot shows for this environment: the session window drives the icon,
    /// because it is the one that runs out during a working day.
    public var iconRow: CardRow? {
        if let session = rows.first(where: { $0.kind == .fiveHour }) {
            return session
        }
        return rows.first
    }

    public static func toneFor(_ percent: Int, _ freshness: DataFreshness) -> LimitTone {
        if freshness == .none {
            return .unknown
        }
        // Stale numbers lose their colour: they are still shown, but they no longer shout.
        if freshness == .stale {
            return .unknown
        }
        return percent >= criticalFrom ? .critical
            : percent >= cautionFrom ? .caution
            : .normal
    }

    public static func from(
        _ snapshot: LimitSnapshot, _ now: Date, staleAfter: TimeInterval? = nil
    ) -> CardState {
        let freshness = snapshot.freshnessAt(now, staleAfter: staleAfter)
        if freshness == .none {
            return CardState(environment: snapshot.environment, freshness: .none, updatedAt: nil, rows: [])
        }

        var rows: [CardRow] = []
        addRow(&rows, snapshot, now, freshness, .fiveHour, fiveHourWindow)
        addRow(&rows, snapshot, now, freshness, .sevenDay, sevenDayWindow)

        if let model = snapshot.model {
            let countdown = ResetCountdown.between(now, model.resetsAt)
            rows.append(CardRow(
                kind: .modelWeek,
                percent: countdown.isReset ? 0 : model.percent,
                tone: toneFor(model.percent, freshness),
                isReset: countdown.isReset,
                countdown: countdown,
                // The model window length is not reported, so no honest estimate can be made.
                pace: PaceEstimate(verdict: .unknown, timeLeft: 0),
                modelName: model.modelName))
        }

        return CardState(
            environment: snapshot.environment,
            freshness: freshness,
            updatedAt: snapshot.receivedAt,
            rows: rows,
            workingUntil: workingUntilFor(snapshot))
    }

    private static func addRow(
        _ rows: inout [CardRow],
        _ snapshot: LimitSnapshot,
        _ now: Date,
        _ freshness: DataFreshness,
        _ kind: LimitKind,
        _ windowLength: TimeInterval
    ) {
        guard let status = snapshot.statusAt(now, kind) else { return }

        rows.append(CardRow(
            kind: kind,
            percent: status.percent,
            tone: toneFor(status.percent, freshness),
            isReset: status.isReset,
            countdown: status.countdown,
            pace: estimatePace(status, windowLength, now, snapshot.windowResetsAt(kind))))
    }

    /// How much of the window has to be gone, and how much of the limit spent, before a guess is
    /// worth making.
    ///
    /// The rule below stretches one measurement across the whole window. Two minutes into a five
    /// hour window, one percent spent reads as "runs out in three hours", stated as confidently as
    /// any other answer. A wrong number said plainly is worse than no number: somebody stops
    /// working because of it. Below either line the card says nothing at all.
    static let leastPercentForPace = 5.0

    static let leastShareOfWindowForPace = 0.1

    /// The simple rule the card explains in words: what has been spent over the part of the window
    /// already gone keeps being spent at the same rate. If the limit would run out after the reset,
    /// the window lasts; otherwise we say how long is left.
    static func estimatePace(
        _ status: LimitStatus, _ windowLength: TimeInterval, _ now: Date, _ resetsAt: Date?
    ) -> PaceEstimate {
        guard !status.isReset, let reset = resetsAt, status.percent > 0 else {
            return PaceEstimate(verdict: .unknown, timeLeft: 0)
        }

        let left = reset.timeIntervalSince(now)
        let spent = windowLength - left
        if spent <= 0 {
            return PaceEstimate(verdict: .unknown, timeLeft: 0)
        }

        if Double(status.percent) < leastPercentForPace || spent < windowLength * leastShareOfWindowForPace {
            return PaceEstimate(verdict: .unknown, timeLeft: 0)
        }

        let percentPerSecond = Double(status.percent) / spent
        let secondsToFull = (100 - Double(status.percent)) / percentPerSecond
        // TimeSpan.FromSeconds cuts at a tick, so the two sides round the same way.
        let timeToFull = (secondsToFull * 10_000_000).rounded(.towardZero) / 10_000_000

        return timeToFull >= left
            ? PaceEstimate(verdict: .lastsPastReset, timeLeft: left)
            : PaceEstimate(verdict: .runsOutBeforeReset, timeLeft: timeToFull)
    }
}
