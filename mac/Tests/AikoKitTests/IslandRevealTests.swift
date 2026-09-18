import Foundation
import Testing

@testable import AikoKit

struct IslandRevealTests {
    static let now = utc(2026, 9, 14, 12)

    private func card(_ environment: String, _ percent: Int, stale: Bool = false) -> CardState {
        CardState.from(
            LimitSnapshot(
                environment: environment,
                source: .statusLine,
                receivedAt: stale ? Self.now.adding(hours: -2) : Self.now,
                windows: [LimitWindow(kind: .fiveHour, percent: percent, resetsAt: Self.now.adding(hours: 3))]),
            Self.now)
    }

    @Test(arguments: [
        (70, 76, true),
        (80, 91, true),
        (40, 95, true),
        (76, 80, false),
        (91, 20, false),
        (40, 60, false),
    ])
    func theIslandOpensWhenASessionCrossesAThresholdUpwards(before: Int, after: Int, opens: Bool) {
        #expect(IslandReveal.toneRose([card("Work", before)], [card("Work", after)]) == opens)
    }

    @Test
    func theFirstNumbersAndNumbersAfterASilenceDoNotOpenIt() {
        #expect(!IslandReveal.toneRose([], [card("Work", 92)]))
        #expect(!IslandReveal.toneRose([card("Work", 50, stale: true)], [card("Work", 92)]))
    }

    @Test
    func eitherEnvironmentCrossingIsEnough() {
        let before = [card("Aiko", 30), card("Work", 70)]
        let after = [card("Aiko", 31), card("Work", 78)]

        #expect(IslandReveal.toneRose(before, after))
    }
}
