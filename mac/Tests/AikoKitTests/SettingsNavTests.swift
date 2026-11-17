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
        voiceInput: Bool = false,
        checklistOpen: Bool = false
    ) -> [SettingsNavEntry] {
        SettingsNav.rows(
            environments: settings,
            plans: plans,
            sendsStats: sendsStats,
            voiceInput: voiceInput,
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
        #expect(SettingsPage.terminal.key == "terminal")
        #expect(SettingsPage.fromKey("terminal") == .terminal)
        // The folders live in the page of their environment now (D-374).
        #expect(SettingsPage.fromKey("folders") == nil)
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
        let items = Self.items(rows).filter { if case .environment = $0.page { true } else { false } }

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
    func theLowerRowsSayWhatIsSwitchedOn() {
        let items = Self.items(Self.rows(Self.two(), sendsStats: true, voiceInput: true))
        let window = items.first { $0.page == .terminal }
        let personality = items.first { $0.page == .personality }
        let privacy = items.first { $0.page == .privacy }

        #expect(window?.note == Strings.navVoiceOn)
        #expect(Self.items(Self.rows(Self.two())).first { $0.page == .terminal }?.note == Strings.navVoiceOff)
        #expect(personality?.note == Strings.format(Strings.navPersonaOn, 1))
        #expect(privacy?.note == Strings.navPrivacyStatsOn)
    }

    @Test
    func theOrderOfTheMenuIsTheOrderOfTheWindowsOne() {
        let rows = Self.rows(Self.two())

        // Three groups told apart by room alone (D-404).
        #expect(rows.filter { $0 == .gap }.count == 2)
        #expect(rows[1] == .gap)
        #expect(Self.items(rows).map(\.page) == [
            .general,
            .environment("/Users/someone/.claude"),
            .environment("/Users/someone/.claude-work"),
            .terminal,
            .personality,
            .privacy,
        ])
    }

    @Test
    func theBodyGrowsWithTheScreenAndStopsAtBothEnds() {
        #expect(SettingsLayout.bodyHeight(screenHeight: 800) == 600)
        #expect(SettingsLayout.bodyHeight(screenHeight: 700) == 540)
        #expect(SettingsLayout.bodyHeight(screenHeight: 500) == 420)
    }
}
