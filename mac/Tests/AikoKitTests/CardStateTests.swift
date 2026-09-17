import Foundation
import Testing

@testable import AikoKit

struct CardStateTests {
    static let now = utc(2026, 9, 12, 12)

    private func snapshot(_ receivedAt: Date, _ windows: LimitWindow...) -> LimitSnapshot {
        LimitSnapshot(environment: "Personal", source: .statusLine, receivedAt: receivedAt, windows: windows)
    }

    @Test(arguments: [
        (0, LimitTone.normal),
        (74, LimitTone.normal),
        (75, LimitTone.caution),
        (89, LimitTone.caution),
        (90, LimitTone.critical),
        (100, LimitTone.critical),
    ])
    func colourFollowsTheThresholds(percent: Int, expected: LimitTone) {
        #expect(CardState.toneFor(percent, .live) == expected)
    }

    @Test
    func staleNumbersLoseTheirColourButKeepTheirValue() {
        let snapshot = snapshot(
            Self.now.adding(hours: -1),
            LimitWindow(kind: .fiveHour, percent: 94, resetsAt: Self.now.adding(hours: 2)))

        let card = CardState.from(snapshot, Self.now)

        #expect(card.freshness == .stale)
        #expect(card.rows[0].percent == 94)
        #expect(card.rows[0].tone == .unknown)
    }

    @Test
    func aSessionCountsAsWorkingForTwoMinutesAfterTheStatusLineSpoke() {
        let window = LimitWindow(kind: .fiveHour, percent: 30, resetsAt: Self.now.adding(hours: 2))

        #expect(CardState.from(snapshot(Self.now.adding(seconds: -90), window), Self.now).isWorkingAt(Self.now))
        #expect(!CardState.from(snapshot(Self.now.adding(minutes: -3), window), Self.now).isWorkingAt(Self.now))
        #expect(
            CardState.from(snapshot(Self.now.adding(seconds: -90), window), Self.now).workingUntil
                == Self.now.adding(seconds: 30))
    }

    @Test
    func aDirectModeAnswerOrNoDataNeverCountsAsWorking() {
        let fromApi = LimitSnapshot(
            environment: "Personal",
            source: .directMode,
            receivedAt: Self.now,
            windows: [LimitWindow(kind: .fiveHour, percent: 30, resetsAt: Self.now.adding(hours: 2))])

        #expect(!CardState.from(fromApi, Self.now).isWorkingAt(Self.now))
        #expect(!CardState.from(LimitSnapshot.noData("Work"), Self.now).isWorkingAt(Self.now))
    }

    @Test
    func withoutDataTheCardHasNoRowsAndNoTime() {
        let card = CardState.from(LimitSnapshot.noData("Work"), Self.now)

        #expect(!card.hasData)
        #expect(card.freshness == DataFreshness.none)
        #expect(card.updatedAt == nil)
        #expect(card.iconRow == nil)
    }

    @Test
    func theIconFollowsTheSessionWindow() {
        let snapshot = snapshot(
            Self.now,
            LimitWindow(kind: .sevenDay, percent: 61, resetsAt: Self.now.adding(days: 4)),
            LimitWindow(kind: .fiveHour, percent: 34, resetsAt: Self.now.adding(hours: 3)))

        let card = CardState.from(snapshot, Self.now)

        #expect(card.iconRow?.kind == .fiveHour)
        #expect(card.iconRow?.percent == 34)
    }

    @Test
    func aResetWindowShowsZeroAndNoEstimate() throws {
        let snapshot = snapshot(
            Self.now, LimitWindow(kind: .fiveHour, percent: 88, resetsAt: Self.now.adding(minutes: -5)))

        let row = try #require(CardState.from(snapshot, Self.now).rows.first)

        #expect(CardState.from(snapshot, Self.now).rows.count == 1)
        #expect(row.isReset)
        #expect(row.percent == 0)
        #expect(row.pace.verdict == .unknown)
    }

    @Test
    func aSlowPaceLastsPastTheReset() throws {
        // One hour into a five hour window, ten percent spent: at this rate the window ends first.
        let snapshot = snapshot(
            Self.now, LimitWindow(kind: .fiveHour, percent: 10, resetsAt: Self.now.adding(hours: 4)))

        let row = try #require(CardState.from(snapshot, Self.now).rows.first)

        #expect(row.pace.verdict == .lastsPastReset)
        #expect(row.pace.timeLeft == 4 * 3600)
    }

    @Test
    func aFastPaceSaysHowLongIsLeft() throws {
        // Four hours into a five hour window, eighty percent spent: twenty percent left at the same
        // rate is one more hour, and the window still has an hour to go — it is a close call.
        let snapshot = snapshot(
            Self.now, LimitWindow(kind: .fiveHour, percent: 90, resetsAt: Self.now.adding(hours: 1)))

        let row = try #require(CardState.from(snapshot, Self.now).rows.first)

        #expect(row.pace.verdict == .runsOutBeforeReset)
        #expect(row.pace.timeLeft < 3600)
        #expect(row.pace.timeLeft > 0)
    }

    @Test
    func aWindowThatHasNotStartedYetHasNoEstimate() throws {
        let snapshot = snapshot(
            Self.now, LimitWindow(kind: .fiveHour, percent: 0, resetsAt: Self.now.adding(hours: 5)))

        let row = try #require(CardState.from(snapshot, Self.now).rows.first)

        #expect(row.pace.verdict == .unknown)
    }

    @Test
    func theModelLimitBecomesAThirdRowInDirectMode() throws {
        let report = UsageReport.fromJson("""
            {
              "five_hour": { "utilization": 34.0, "resets_at": "2026-09-12T15:00:00+00:00" },
              "limits": [ { "kind": "weekly_scoped", "percent": 3.0, "resets_at": "2026-09-16T03:00:00+00:00",
                            "scope": { "model": { "display_name": "Fable" } } } ]
            }
            """)
        let snapshot = LimitSnapshot.fromDirectMode("Personal", Self.now, report)

        let card = CardState.from(snapshot, Self.now)

        #expect(card.rows.count == 2)
        let model = try #require(card.rows.first { $0.kind == .modelWeek })
        #expect(model.percent == 3)
        #expect(model.pace.verdict == .unknown)
    }

    @Test
    func theCardKeepsTheTimeTheNumbersArrived() {
        let snapshot = snapshot(
            Self.now.adding(minutes: -2),
            LimitWindow(kind: .fiveHour, percent: 34, resetsAt: Self.now.adding(hours: 3)))

        let card = CardState.from(snapshot, Self.now)

        #expect(card.updatedAt == Self.now.adding(minutes: -2))
        #expect(card.freshness == .live)
    }
}
