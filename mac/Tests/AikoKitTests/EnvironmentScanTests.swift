import Foundation
import Testing

@testable import AikoKit

struct EnvironmentScanTests {
    static let now = utc(2026, 9, 12, 18, 0, 0)

    private func folder(_ name: String, credentials: Bool = true, daysAgo: Double? = nil) -> ClaudeFolder {
        ClaudeFolder(
            #"C:\Users\someone\"# + name,
            name,
            hasCredentials: credentials,
            lastUsed: daysAgo.map { Self.now.adding(days: -$0) })
    }

    @Test
    func aFolderWithoutCredentialsIsNotAnEnvironment() {
        // .claude_pl on this machine: the folder exists and holds nothing at all.
        #expect(EnvironmentScan.pick([folder(".claude_pl", credentials: false)]).isEmpty)
    }

    @Test
    func foldersInRecentUseComeFirst() {
        let found = EnvironmentScan.pick([
            folder(".claude-work", daysAgo: 200),
            folder(".claude-personal", daysAgo: 1),
            folder(".claude", daysAgo: 30),
        ])

        #expect(found.map(\.suggestedName) == ["Personal", "Main", "Work"])
    }

    @Test
    func foldersWithNoKnownTimeComeLastInASteadyOrder() {
        let found = EnvironmentScan.pick([
            folder(".claude-zeta"),
            folder(".claude-alpha"),
            folder(".claude-personal", daysAgo: 3),
        ])

        #expect(found.map(\.suggestedName) == ["Personal", "Alpha", "Zeta"])
    }

    @Test(arguments: [
        (".claude-personal", "Personal"),
        (".claude-work", "Work"),
        (".claude_pl", "Pl"),
        (".claude", "Main"),
        ("claude", "Main"),
        (".claude-", "Main"),
    ] as [(String, String)])
    func theNameStartsFromTheFolder(folder: String, expected: String) {
        #expect(EnvironmentScan.suggestName(folder) == expected)
    }

    @Test
    func aPlainFolderGetsTheNameInTheUsersLanguage() {
        // Found on the live run: a Russian wizard offered "Main" beside Russian text.
        let found = EnvironmentScan.pick(
            [folder(".claude", daysAgo: 1), folder(".claude-work", daysAgo: 2)], "Основная")

        #expect(found.map(\.suggestedName) == ["Основная", "Work"])
    }

    @Test
    func twoFoldersThatLookTheSameAreBothOffered() {
        // One may hold expired tokens and the other be in daily use, yet both hold the same files.
        // Aiko must not guess which one is real.
        let found = EnvironmentScan.pick([
            folder(".claude-work", daysAgo: 5),
            folder(".claude-personal", daysAgo: 5),
        ])

        #expect(found.count == 2)
    }

    @Test
    func theFullPathIsCarriedThrough() {
        let found = EnvironmentScan.pick([folder(".claude-personal", daysAgo: 1)])

        #expect(found.first?.fullPath == #"C:\Users\someone\.claude-personal"#)
    }
}
