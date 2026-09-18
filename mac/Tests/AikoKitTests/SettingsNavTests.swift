import Testing

@testable import AikoKit

struct SettingsNavTests {
    static let home = "/Users/someone"

    static func two() -> EnvironmentSettings {
        EnvironmentSettings([
            AikoEnvironment("Work", ["/Users/someone/.claude-work"], projectFolders: ["/Users/someone/code/site"]),
            AikoEnvironment("Main", ["/Users/someone/.claude"], persona: true),
        ])
    }

    static func rows(
        _ settings: EnvironmentSettings,
        plans: [String: String] = [:],
        sendsStats: Bool = false,
        checklistOpen: Bool = false
    ) -> [SettingsNavEntry] {
        SettingsNav.rows(
            environments: settings,
            plans: plans,
            sendsStats: sendsStats,
            checklistOpen: checklistOpen,
            platform: .macOS,
            userProfile: home)
    }

    static func items(_ rows: [SettingsNavEntry]) -> [SettingsNavItem] {
        rows.compactMap { if case .item(let item) = $0 { return item } else { return nil } }
    }

    @Test
    func aPageKeyIsTheSameStringOnBothSystems() {
        #expect(SettingsPage.general.key == "general")
        #expect(SettingsPage.checklist.key == "setup")
        #expect(SettingsPage.environment("/Users/someone/.claude").key == "env:/Users/someone/.claude")
        #expect(SettingsPage.fromKey("folders") == .folders)
        #expect(SettingsPage.fromKey("env:/Users/someone/.claude") == .environment("/Users/someone/.claude"))
        #expect(SettingsPage.fromKey("env:") == nil)
        #expect(SettingsPage.fromKey("nothing") == nil)
    }

    @Test
    func environmentOneIsAlwaysFirstWhateverOrderTheFileHas() {
        let ordered = SettingsNav.ordered(Self.two(), .macOS, Self.home)

        #expect(ordered.map(\.name) == ["Main", "Work"])
        #expect(SettingsNav.firstPage(Self.two(), .macOS, Self.home)
            == .environment("/Users/someone/.claude"))
    }

    @Test
    func nothingSetUpOpensTheChecklist() {
        #expect(SettingsNav.firstPage(.empty, .macOS, Self.home) == .checklist)
    }

    @Test
    func anEnvironmentRowSaysThePlanAndTheCommandUnderTheName() {
        let rows = Self.rows(Self.two(), plans: ["/Users/someone/.claude": "Max 20x"])
        let items = Self.items(rows)

        #expect(items[0].title == "Main")
        #expect(items[0].note == "Max 20x · aiko-main")
        // No plan read yet: the command alone, with no leading separator.
        #expect(items[1].note == "aiko-work")
    }

    @Test
    func theSecondSlotIsOfferedOnlyWhileThereIsRoomForIt() {
        let one = EnvironmentSettings([AikoEnvironment("Main", ["/Users/someone/.claude"])])

        #expect(Self.rows(one).contains(.addSecondEnvironment))
        #expect(!Self.rows(Self.two()).contains(.addSecondEnvironment))
    }

    @Test
    func anOpenChecklistTakesTheSlotAndAnEmptyMachineHasNothingElse() {
        #expect(!Self.rows(Self.two(), checklistOpen: true).contains(.addSecondEnvironment))
        #expect(Self.items(Self.rows(Self.two(), checklistOpen: true)).contains { $0.page == .checklist })
        #expect(Self.items(Self.rows(.empty)).contains { $0.page == .checklist })
    }

    @Test
    func personalityIsHiddenUntilThereIsAnEnvironmentToTalkIn() {
        #expect(!Self.items(Self.rows(.empty)).contains { $0.page == .personality })
        #expect(Self.items(Self.rows(Self.two())).contains { $0.page == .personality })
    }

    @Test
    func theLowerRowsCountWhatIsBoundAndWhoTalks() {
        let items = Self.items(Self.rows(Self.two(), sendsStats: true))
        let folders = items.first { $0.page == .folders }
        let personality = items.first { $0.page == .personality }
        let privacy = items.first { $0.page == .privacy }

        #expect(folders?.note == Strings.format(Strings.navBound, 1))
        #expect(personality?.note == Strings.format(Strings.navPersonaOn, 1))
        #expect(privacy?.note == Strings.navPrivacyStatsOn)
    }

    @Test
    func theOrderOfTheMenuIsTheOrderOfTheWindowsOne() {
        let rows = Self.rows(Self.two())

        #expect(rows.first == .label(Strings.sectionEnvironments))
        #expect(rows.contains(.separator))
        #expect(Self.items(rows).map(\.page) == [
            .environment("/Users/someone/.claude"),
            .environment("/Users/someone/.claude-work"),
            .folders,
            .personality,
            .privacy,
            .general,
        ])
    }

    @Test
    func theBodyGrowsWithTheScreenAndStopsAtBothEnds() {
        #expect(SettingsLayout.bodyHeight(screenHeight: 800) == 600)
        #expect(SettingsLayout.bodyHeight(screenHeight: 700) == 540)
        #expect(SettingsLayout.bodyHeight(screenHeight: 500) == 420)
    }
}
