import Foundation
import Testing

@testable import AikoKit

/// A yes to the daily count is a yes to the list that was shown. These tests hold the promise that
/// a value added later is not sent on the strength of an older yes.
struct StatsListTests {
    static let setUp = EnvironmentSettings([AikoEnvironment("Aiko", [#"C:\Users\someone\.claude"#])])

    private static func app(sendStats: Bool, seen: Int) -> AppSettings {
        var app = AppSettings.default
        app.sendStats = sendStats
        app.statsListSeen = seen
        return app
    }

    @Test(arguments: [
        (true, 0, false),
        (true, 1, false),
        (true, 2, true),
        (true, 3, true),
        (false, 2, false),
    ])
    func theNewFlagsGoOnlyAfterTheListThatNamesThemWasSeen(sendStats: Bool, seen: Int, sends: Bool) {
        #expect(StatsList.sendsSkillsAndMemory(Self.app(sendStats: sendStats, seen: seen)) == sends)
    }

    @Test(arguments: [
        (true, 0, true),
        (true, 1, true),
        (true, 2, false),
        (false, 0, false),
    ])
    func onlySomebodyCountedUnderAnOlderListIsAskedToLookAgain(sendStats: Bool, seen: Int, asks: Bool) {
        #expect(StatsList.asksToLookAgain(Self.app(sendStats: sendStats, seen: seen), Self.setUp) == asks)
    }

    @Test
    func nobodyIsAskedBeforeAikoIsSetUp() {
        #expect(!StatsList.asksToLookAgain(Self.app(sendStats: true, seen: 0), .empty))
    }

    @Test
    func seeingTheListIsNotedOnceAndStopsTheQuestion() {
        let before = Self.app(sendStats: true, seen: 0)

        let after = StatsList.seen(before)

        #expect(after.statsListSeen == StatsList.current)
        #expect(after.sendStats)
        #expect(!StatsList.asksToLookAgain(after, Self.setUp))
        #expect(StatsList.seen(after) == after)
    }

    @Test
    func aSettingsFileFromBeforeTheListGrewReadsAsNotSeenAndTheNumberIsKept() {
        let old = AppSettings.fromJson(#"{ "schemaVersion": 3, "sendStats": true, "privacyAsked": true }"#)

        #expect(old.statsListSeen == 0)
        #expect(AppSettings.fromJson(StatsList.seen(old).toJson()).statsListSeen == StatsList.current)
    }
}
