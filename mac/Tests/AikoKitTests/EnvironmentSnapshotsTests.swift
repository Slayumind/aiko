import Foundation
import Testing

@testable import AikoKit

struct EnvironmentSnapshotsTests {
    static let now = utc(2026, 9, 12, 18, 0, 0)

    private func snapshot(_ file: String, _ percent: Int, minutesAgo: Double) -> LimitSnapshot {
        LimitSnapshot(
            environment: file,
            source: .statusLine,
            receivedAt: Self.now.adding(minutes: -minutesAgo),
            windows: [LimitWindow(kind: .fiveHour, percent: percent, resetsAt: Self.now.adding(hours: 3))])
    }

    @Test
    func theCardShowsTheNameThePersonGaveNotTheFolder() {
        let settings = EnvironmentSettings([
            AikoEnvironment("Личная", [#"C:\Users\someone\.claude-personal"#])
        ])
        let files = ["claude-personal": snapshot("claude-personal", 42, minutesAgo: 1)]

        let combined = EnvironmentSnapshots.combine(settings, files)

        #expect(combined.count == 1)
        #expect(combined.first?.environment == "Личная")
        #expect(combined.first?.windows.first?.percent == 42)
    }

    @Test
    func anEnvironmentOverSeveralFoldersTakesTheFreshestAnswer() {
        let settings = EnvironmentSettings([
            AikoEnvironment("Work", [#"C:\Users\someone\.claude"#, #"C:\Users\someone\.claude-work"#])
        ])
        let files = [
            "claude": snapshot("claude", 10, minutesAgo: 40),
            "claude-work": snapshot("claude-work", 77, minutesAgo: 2),
        ]

        let combined = EnvironmentSnapshots.combine(settings, files)

        #expect(combined.first?.windows.first?.percent == 77)
    }

    @Test
    func anEnvironmentWithNoFileYetIsStillShown() {
        let settings = EnvironmentSettings([AikoEnvironment("Work", [#"C:\Users\someone\.claude-work"#])])

        let combined = EnvironmentSnapshots.combine(settings, [:])

        #expect(combined.count == 1)
        #expect(combined.first?.environment == "Work")
        #expect(combined.first?.hasData == false)
    }

    @Test
    func aFileThatBelongsToNoEnvironmentIsLeftOut() {
        let settings = EnvironmentSettings([
            AikoEnvironment("Personal", [#"C:\Users\someone\.claude-personal"#])
        ])
        let files = [
            "claude-personal": snapshot("claude-personal", 42, minutesAgo: 1),
            "claude-someone-else": snapshot("claude-someone-else", 99, minutesAgo: 1),
        ]

        let combined = EnvironmentSnapshots.combine(settings, files)

        #expect(combined.map(\.environment) == ["Personal"])
    }

    @Test
    func beforeTheWizardHasRunEverythingThatArrivedIsShown() {
        let files = [
            "claude-work": snapshot("claude-work", 82, minutesAgo: 1),
            "claude-personal": snapshot("claude-personal", 42, minutesAgo: 1),
        ]

        let combined = EnvironmentSnapshots.combine(.empty, files)

        #expect(combined.map(\.environment) == ["claude-personal", "claude-work"])
    }

    @Test
    func environmentsKeepTheOrderOfTheSettings() {
        let settings = EnvironmentSettings([
            AikoEnvironment("Work", [#"C:\Users\someone\.claude-work"#]),
            AikoEnvironment("Personal", [#"C:\Users\someone\.claude-personal"#]),
        ])

        let combined = EnvironmentSnapshots.combine(settings, [:])

        #expect(combined.map(\.environment) == ["Work", "Personal"])
    }
}
