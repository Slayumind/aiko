import Foundation
import Testing

@testable import AikoKit

struct LimitSnapshotTests {
    static let now = utc(2026, 9, 12, 12)

    private func snapshotWith(_ windows: LimitWindow...) -> LimitSnapshot {
        LimitSnapshot(environment: "Personal", source: .statusLine, receivedAt: Self.now, windows: windows)
    }

    @Test
    func freshNumbersAreLive() {
        let snapshot = snapshotWith(LimitWindow(kind: .fiveHour, percent: 34, resetsAt: Self.now.adding(hours: 3)))

        #expect(snapshot.freshnessAt(Self.now.adding(minutes: 10)) == .live)
    }

    @Test
    func numbersOlderThanTheLimitAreStale() {
        let snapshot = snapshotWith(LimitWindow(kind: .fiveHour, percent: 34, resetsAt: Self.now.adding(hours: 3)))

        #expect(snapshot.freshnessAt(Self.now.adding(minutes: 16)) == .stale)
    }

    @Test
    func anEmptySnapshotHasNoDataAtAll() {
        let snapshot = LimitSnapshot.noData("Work")

        #expect(!snapshot.hasData)
        #expect(snapshot.freshnessAt(Self.now) == DataFreshness.none)
        #expect(snapshot.statusAt(Self.now, .fiveHour) == nil)
    }

    @Test
    func aWindowReportsItsPercentageAndCountdown() throws {
        let snapshot = snapshotWith(LimitWindow(
            kind: .fiveHour, percent: 34, resetsAt: Self.now.adding(hours: 3).adding(minutes: 34)))

        let status = try #require(snapshot.statusAt(Self.now, .fiveHour))

        #expect(status.percent == 34)
        #expect(!status.isReset)
        #expect(status.countdown.hours == 3)
        #expect(status.countdown.minutes == 34)
    }

    @Test
    func aWindowWhoseResetTimeHasPassedReadsAsZero() throws {
        let snapshot = snapshotWith(LimitWindow(kind: .fiveHour, percent: 88, resetsAt: Self.now.adding(minutes: -1)))

        let status = try #require(snapshot.statusAt(Self.now, .fiveHour))

        #expect(status.isReset)
        #expect(status.percent == 0)
    }

    @Test
    func staleDataStillShowsItsNumbers() throws {
        // Old numbers stay true while nobody works: this is why we show them instead of dashes.
        let snapshot = snapshotWith(LimitWindow(kind: .sevenDay, percent: 61, resetsAt: Self.now.adding(days: 4)))

        let status = try #require(snapshot.statusAt(Self.now.adding(hours: 5), .sevenDay))

        #expect(snapshot.freshnessAt(Self.now.adding(hours: 5)) == .stale)
        #expect(status.percent == 61)
    }

    @Test
    func aReportWithoutDataBecomesAnEmptySnapshot() {
        let snapshot = LimitSnapshot.fromStatusLine("Personal", Self.now, .empty)

        #expect(!snapshot.hasData)
    }

    @Test
    func aReportWithDataKeepsTheSourceAndTheTime() {
        let report = StatusLineReport.fromJson("""
            { "rate_limits": { "five_hour": { "used_percentage": 34, "resets_at": 1789170600 } } }
            """)

        let snapshot = LimitSnapshot.fromStatusLine("Personal", Self.now, report)

        #expect(snapshot.hasData)
        #expect(snapshot.source == .statusLine)
        #expect(snapshot.receivedAt == Self.now)
    }
}
