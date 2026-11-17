import Foundation
import Testing

@testable import AikoKit

/// The seam for another agent (D-420): an environment has its list of providers, and a limit window
/// can say how long it lasts and what it is called. Claude says neither, and nothing changes for it.
/// The twin of ProviderTests.cs.
struct ProviderTests {
    static let now = utc(2026, 9, 12, 12)

    @Test
    func anEnvironmentWithoutAListWorksWithClaude() {
        let settings = EnvironmentSettings.fromJson(#"{ "environments": [ { "name": "Work", "configDirectories": ["/c"] } ] }"#)

        #expect(settings.environments[0].providers == [.claude])
    }

    @Test
    func claudeAloneIsNotWrittenAndANameAikoDoesNotKnowIsLeftOut() {
        let plain = EnvironmentSettings([AikoEnvironment("Work", ["/c"])])
        let unknown = EnvironmentSettings.fromJson(
            #"{ "environments": [ { "name": "Work", "configDirectories": ["/c"], "providers": ["somebody"] } ] }"#)

        #expect(!plain.toJson().contains("providers"))
        #expect(unknown.environments[0].providers == [.claude])
    }

    @Test
    func aWindowWithItsOwnLengthIsPacedOnThatLength() {
        // Half of a two hour window is gone and 60% is spent: it runs out before the reset.
        let window = LimitWindow(
            kind: .fiveHour, percent: 60, resetsAt: Self.now.addingTimeInterval(3600), length: 7200, label: "2 hours")
        let snapshot = LimitSnapshot(environment: "other", source: .statusLine, receivedAt: Self.now, windows: [window])

        let row = CardState.from(snapshot, Self.now).rows[0]

        #expect(row.pace.verdict == .runsOutBeforeReset)
        #expect(LimitRow.from(row).name == "2 hours")
    }

    @Test
    func aWindowOfClaudeKeepsItsNameAndLength() {
        let window = LimitWindow(kind: .fiveHour, percent: 10, resetsAt: Self.now.addingTimeInterval(4 * 3600))
        let snapshot = LimitSnapshot(environment: "claude", source: .statusLine, receivedAt: Self.now, windows: [window])

        let row = CardState.from(snapshot, Self.now).rows[0]

        #expect(row.label == nil)
        #expect(LimitRow.from(row).name == CardText.windowName(.fiveHour))
    }

    @Test
    func theLengthAndTheLabelSurviveTheSnapshotFileAndClaudeFilesStayAsTheyWere() {
        let other = LimitSnapshot(
            environment: "other", source: .statusLine, receivedAt: Self.now,
            windows: [LimitWindow(kind: .fiveHour, percent: 5, resetsAt: Self.now.addingTimeInterval(3600), length: 7200, label: "2 hours")])
        let claude = LimitSnapshot(
            environment: "claude", source: .statusLine, receivedAt: Self.now,
            windows: [LimitWindow(kind: .fiveHour, percent: 5, resetsAt: Self.now.addingTimeInterval(3600))])

        let read = SnapshotFile.fromJson(SnapshotFile.toJson(other)).windows[0]

        #expect(read.length == 7200)
        #expect(read.label == "2 hours")
        #expect(!SnapshotFile.toJson(claude).contains("lengthSeconds"))
        #expect(!SnapshotFile.toJson(claude).contains("label"))
    }
}
