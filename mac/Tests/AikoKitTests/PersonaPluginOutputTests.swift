import Foundation
import Testing

@testable import AikoKit

struct PersonaPluginOutputTests {
    // The folder tests need SnapshotName, which is not ported yet.

    @Test(arguments: [
        (["plugin", "aiko-persona"], true),
        (["plugin", "aiko-copy"], false),
        (["plugin"], false),
        (["plugin", "aiko-persona", "extra"], false),
        ([], false),
    ] as [([String], Bool)])
    func onlyThePersonaPluginIsBuilt(args: [String], expected: Bool) {
        #expect(PersonaPluginOutput.isRequest(args) == expected)
    }

    @Test
    func oldVersionsGoAfterAnHourAndTheCurrentOneStays() {
        let now = utc(2026, 9, 15, 12)
        let folders: [(name: String, written: Date)] = [
            ("aaaaaaaaaaaa", now.adding(hours: -3)),
            ("bbbbbbbbbbbb", now.adding(minutes: -10)),
            ("cccccccccccc", now.adding(hours: -5)),
            ("cccccccccccc.4711.tmp", now.adding(hours: -5)),
            ("notes", now.adding(days: -2)),
        ]

        let stale = PersonaPluginOutput.staleFolders(folders, currentHash: "cccccccccccc", now: now)

        #expect(stale == ["aaaaaaaaaaaa"])
    }
}
