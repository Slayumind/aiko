import Foundation
import Testing

@testable import AikoKit

struct IconRowsTests {
    static let now = utc(2026, 9, 14, 12)

    private func card(_ environment: String, _ percent: Int?) -> CardState {
        CardState.from(
            LimitSnapshot(
                environment: environment,
                source: .statusLine,
                receivedAt: Self.now,
                windows: percent.map { [LimitWindow(kind: .fiveHour, percent: $0, resetsAt: Self.now.adding(hours: 3))] } ?? []),
            Self.now)
    }

    @Test
    func withNothingReportedTheRingAndTheDotAreEmpty() {
        let none = IconRows.choose([], ringEnvironment: "Work")
        let silent = IconRows.choose([card("Aiko", nil), card("Work", nil)], ringEnvironment: "Work")

        #expect(none.ring == nil && none.dot == nil)
        #expect(silent.ring == nil && silent.dot == nil)
    }

    @Test
    func oneEnvironmentTakesTheRingAndLeavesNoDot() {
        let rows = IconRows.choose([card("Aiko", 42)], ringEnvironment: nil)

        #expect(rows.ring?.percent == 42)
        #expect(rows.dot == nil)
    }

    @Test
    func theChosenEnvironmentIsTheRingAndTheOtherIsTheDot() {
        let rows = IconRows.choose([card("Aiko", 42), card("Work", 82)], ringEnvironment: "Work")

        #expect(rows.ring?.percent == 82)
        #expect(rows.dot?.percent == 42)
    }

    @Test(arguments: [nil, "Gone"] as [String?])
    func withoutAChoiceThatExistsTheFirstEnvironmentIsTheRing(chosen: String?) {
        let rows = IconRows.choose([card("Aiko", 42), card("Work", 82)], ringEnvironment: chosen)

        #expect(rows.ring?.percent == 42)
        #expect(rows.dot?.percent == 82)
    }

    @Test
    func aChosenEnvironmentWithNoNumbersGivesTheRingToOneThatHasThem() {
        let rows = IconRows.choose([card("Aiko", 42), card("Work", nil)], ringEnvironment: "Work")

        #expect(rows.ring?.percent == 42)
        #expect(rows.dot == nil)
    }

    @Test
    func withThreeEnvironmentsTheDotIsTheFirstOtherOneWithNumbers() {
        let rows = IconRows.choose([card("Aiko", 42), card("Side", nil), card("Work", 82)], ringEnvironment: "Aiko")

        #expect(rows.ring?.percent == 42)
        #expect(rows.dot?.percent == 82)
    }
}
