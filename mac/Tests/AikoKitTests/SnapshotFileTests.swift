import Foundation
import Testing

@testable import AikoKit

struct SnapshotFileTests {
    static let now = utc(2026, 9, 12, 12, 0, 0)

    @Test
    func whatTheBridgeWritesTheTrayReadsBack() {
        let snapshot = LimitSnapshot(
            environment: "Personal",
            source: .statusLine,
            receivedAt: Self.now,
            windows: [LimitWindow(kind: .fiveHour, percent: 34, resetsAt: Self.now.adding(hours: 3))])

        let again = SnapshotFile.fromJson(SnapshotFile.toJson(snapshot))

        #expect(again.environment == "Personal")
        #expect(again.source == .statusLine)
        #expect(again.receivedAt == Self.now)
        #expect(again.windows.first?.percent == 34)
        #expect(again.windows.first?.resetsAt == Self.now.adding(hours: 3))
    }

    @Test
    func theModelLimitSurvivesTheRoundTrip() {
        let snapshot = LimitSnapshot(
            environment: "Personal",
            source: .directMode,
            receivedAt: Self.now,
            windows: [LimitWindow(kind: .fiveHour, percent: 10, resetsAt: Self.now.adding(hours: 1))],
            model: ModelLimit(modelName: "Fable", percent: 3, resetsAt: Self.now.adding(days: 4)))

        let again = SnapshotFile.fromJson(SnapshotFile.toJson(snapshot))

        #expect(again.model?.modelName == "Fable")
        #expect(again.model?.percent == 3)
        #expect(again.source == .directMode)
    }

    @Test
    func theFileHoldsNumbersOnlyAndNoTracesOfTheSession() {
        let snapshot = LimitSnapshot(
            environment: "Personal",
            source: .statusLine,
            receivedAt: Self.now,
            windows: [LimitWindow(kind: .fiveHour, percent: 34, resetsAt: Self.now.adding(hours: 3))])

        let json = SnapshotFile.toJson(snapshot).lowercased()

        #expect(!json.contains("session"))
        #expect(!json.contains("transcript"))
        #expect(!json.contains("cwd"))
        #expect(!json.contains("token"))
    }

    @Test
    func enumsAreWrittenAsWordsSoTheFileStaysReadable() {
        let json = SnapshotFile.toJson(
            LimitSnapshot(
                environment: "Work",
                source: .directMode,
                receivedAt: Self.now,
                windows: [LimitWindow(kind: .sevenDay, percent: 61, resetsAt: Self.now.adding(days: 4))]))

        #expect(json.contains("\"DirectMode\""))
        #expect(json.contains("\"SevenDay\""))
    }

    @Test(arguments: ["", "half written {", "{}", #"{ "environment": "Personal", "windows": [] }"#])
    func aFileBeingWrittenRightNowReadsAsNothingNew(json: String) {
        let snapshot = SnapshotFile.fromJson(json, fallbackEnvironment: "Personal")

        #expect(!snapshot.hasData)
        #expect(snapshot.environment == "Personal")
    }

    @Test
    func aTimeIsWrittenTheWayTheWindowsCoreWritesIt() {
        // System.Text.Json writes a DateTimeOffset with the seconds and the offset, and drops a
        // fraction that is zero. Its strict encoder also escapes the plus of the offset, so both
        // cores put the same bytes in the file.
        let json = SnapshotFile.toJson(
            LimitSnapshot(
                environment: "Work",
                source: .statusLine,
                receivedAt: Self.now,
                windows: [LimitWindow(kind: .fiveHour, percent: 1, resetsAt: Self.now)]))

        #expect(json.contains(##""2026-09-12T12:00:00\u002B00:00""##))
    }
}
