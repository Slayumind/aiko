import Foundation
import Testing

@testable import AikoKit

/// When Aiko is allowed to guess how long a limit will last, and when it must keep quiet.
///
/// The guess stretches a single measurement across the whole window. Early in a window that is
/// nonsense, and it was being said as plainly as any other number. Somebody stops working because
/// of a number like that, so the card now says nothing until there is something to go on.
struct PaceEstimateTests {
    static let now = utc(2026, 9, 12, 12)
    static let window: TimeInterval = 5 * 3600

    /// One five hour window, so far along and so much of it spent.
    private func pace(_ percent: Int, _ windowGone: TimeInterval) -> PaceEstimate {
        let resetsAt = Self.now.addingTimeInterval(Self.window - windowGone)
        let snapshot = LimitSnapshot(
            environment: "Personal",
            source: .statusLine,
            receivedAt: Self.now,
            windows: [LimitWindow(kind: .fiveHour, percent: percent, resetsAt: resetsAt)])

        return CardState.from(snapshot, Self.now).rows[0].pace
    }

    @Test
    func twoMinutesIntoAWindowThereIsNothingToGoOn() {
        // One percent in two minutes used to read as "runs out in about three hours".
        #expect(pace(1, 2 * 60).verdict == .unknown)
    }

    @Test
    func aTinyShareSpentSaysNothingEvenLateInTheWindow() {
        #expect(pace(2, 4 * 3600).verdict == .unknown)
    }

    @Test
    func aBigShareSpentSaysNothingWhileTheWindowIsYoung() {
        // Somebody who opened a huge file in the first minutes is not going at that rate all day.
        #expect(pace(40, 10 * 60).verdict == .unknown)
    }

    @Test
    func onceThereIsSomethingToGoOnTheGuessComesBack() {
        let estimate = pace(50, 3600)

        #expect(estimate.verdict == .runsOutBeforeReset)
        // Half the limit in one hour: the rest goes in about another hour.
        #expect(estimate.timeLeft >= 55 * 60 && estimate.timeLeft <= 65 * 60)
    }

    @Test
    func aSlowHourLastsPastTheReset() {
        let estimate = pace(10, 2 * 3600)

        #expect(estimate.verdict == .lastsPastReset)
        #expect(estimate.timeLeft >= 175 * 60 && estimate.timeLeft <= 185 * 60)
    }

    @Test
    func justOverBothThresholdsIsEnough() {
        // Five percent and half an hour of a five hour window: the first point worth a guess.
        #expect(pace(6, 35 * 60).verdict != .unknown)
    }

    @Test
    func aWindowThatJustResetSaysNothing() {
        let snapshot = LimitSnapshot(
            environment: "Personal",
            source: .statusLine,
            receivedAt: Self.now,
            windows: [LimitWindow(kind: .fiveHour, percent: 80, resetsAt: Self.now.adding(minutes: -1))])

        let row = CardState.from(snapshot, Self.now).rows[0]

        #expect(row.isReset)
        #expect(row.pace.verdict == .unknown)
    }

    @Test
    func theWeeklyModelLimitNeverGuesses() throws {
        // Nothing reports how long that window is, so there is no window to reason about.
        let snapshot = LimitSnapshot(
            environment: "Personal",
            source: .directMode,
            receivedAt: Self.now,
            windows: [LimitWindow(kind: .sevenDay, percent: 60, resetsAt: Self.now.adding(days: 3))],
            model: ModelLimit(modelName: "Fable", percent: 70, resetsAt: Self.now.adding(days: 3)))

        let model = try #require(CardState.from(snapshot, Self.now).rows.first { $0.kind == .modelWeek })

        #expect(model.pace.verdict == .unknown)
    }
}
