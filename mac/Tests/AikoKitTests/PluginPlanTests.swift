import Foundation
import Testing

@testable import AikoKit

struct PluginPlanTests {
    static let marketplace = #"C:\Users\Тест Юзер\AppData\Local\Aiko\marketplace"#
    static let persona = "aiko-persona@aiko"

    // The command and the marketplace file are written by AikoMarketplace, which is not ported yet.

    private func state(
        _ marketplace: String? = nil, installed: [String] = [], enabled: [String] = []
    ) -> PluginState {
        PluginState(marketplaceFolder: marketplace, installed: Set(installed), enabled: Set(enabled))
    }

    // ---- what a folder has ----

    @Test
    func onlyOurPluginsAreReadAndOnlyUserInstallsCount() {
        let state = PluginState.read(
            settingsJson:
                #"{ "enabledPlugins": { "aiko-persona@aiko": true, "aiko@aiko": false, "frontend-design@claude-plugins-official": true } }"#,
            installedJson: """
                { "version": 2, "plugins": {
                    "aiko-persona@aiko": [ { "scope": "user" } ],
                    "aiko@aiko": [ { "scope": "project", "projectPath": "C:\\\\x" } ],
                    "frontend-design@claude-plugins-official": [ { "scope": "user" } ] } }
                """,
            knownMarketplacesJson:
                #"{ "aiko": { "installLocation": "C:\\m" }, "claude-plugins-official": { "installLocation": "C:\\o" } }"#)

        #expect(state.marketplaceFolder == #"C:\m"#)
        #expect(state.installed == [Self.persona])
        #expect(state.enabled == [Self.persona])
    }

    @Test(arguments: [nil, "", "not json", "[1, 2]"] as [String?])
    func missingOrBrokenFilesReadAsNothingOfOurs(json: String?) {
        let state = PluginState.read(settingsJson: json, installedJson: json, knownMarketplacesJson: json)

        #expect(state.marketplaceFolder == nil)
        #expect(state.installed.isEmpty)
        #expect(state.enabled.isEmpty)
    }

    // ---- what a folder should have ----

    @Test
    func withoutThePersonaAFolderWantsNothingNotEvenSkills() {
        #expect(PluginPlan.desired(personaOn: false, persona: .default, skills: ["copy"]).isEmpty)
    }

    @Test
    func withThePersonaAFolderWantsItAndTheSkillsPlugin() {
        let desired = PluginPlan.desired(personaOn: true, persona: .default, skills: ["copy", "palette"])

        #expect(desired.sorted() == [Self.persona, "aiko@aiko"].sorted())
    }

    @Test
    func withTheSkillsOffOrNoneShippedAFolderWantsOnlyThePersona() {
        var skillsOff = PersonaSettings.default
        skillsOff.skillsOn = false

        #expect(PluginPlan.desired(personaOn: true, persona: skillsOff, skills: ["copy", "palette"]) == [Self.persona])
        #expect(PluginPlan.desired(personaOn: true, persona: .default, skills: []) == [Self.persona])
    }

    @Test(arguments: [
        ("aiko-copy@aiko", true),
        ("aiko-gamedesign-research@aiko", true),
        ("aiko@aiko", false),
        ("aiko-persona@aiko", false),
        ("aiko-copy@someone-else", false),
    ])
    func onlyOurPluginsThisVersionDoesNotShipAreRetired(id: String, retired: Bool) {
        #expect(PluginPlan.isRetired(id) == retired)
    }

    // ---- the steps ----

    @Test
    func nothingWantedAndNothingThereMeansNoStepsAndNoMarketplace() {
        #expect(PluginPlan.steps(desired: [], state: state(), marketplaceFolder: Self.marketplace).isEmpty)
    }

    @Test
    func aFirstSwitchOnAddsTheMarketplaceAndInstalls() {
        let steps = PluginPlan.steps(desired: [Self.persona], state: state(), marketplaceFolder: Self.marketplace)

        #expect(steps == [PluginStep(.addMarketplace, Self.marketplace), PluginStep(.install, Self.persona)])
    }

    @Test
    func anInstalledButDisabledPluginIsEnabledNotInstalledAgain() {
        let steps = PluginPlan.steps(
            desired: [Self.persona],
            state: state(Self.marketplace, installed: [Self.persona]),
            marketplaceFolder: Self.marketplace)

        #expect(steps == [PluginStep(.enable, Self.persona)])
    }

    @Test
    func switchingOffDisablesAndKeepsTheFiles() {
        let steps = PluginPlan.steps(
            desired: [],
            state: state(Self.marketplace, installed: [Self.persona, "aiko@aiko"], enabled: [Self.persona, "aiko@aiko"]),
            marketplaceFolder: Self.marketplace)

        #expect(steps == [PluginStep(.disable, Self.persona), PluginStep(.disable, "aiko@aiko")])
    }

    @Test
    func theOneSkillPluginsOfOlderVersionsAreUninstalledWhileTheNewOneComesIn() {
        let current = state(
            Self.marketplace,
            installed: [Self.persona, "aiko-copy@aiko", "aiko-palette@aiko"],
            enabled: [Self.persona, "aiko-copy@aiko"])

        let steps = PluginPlan.steps(
            desired: [Self.persona, "aiko@aiko"], state: current, marketplaceFolder: Self.marketplace)

        #expect(
            steps == [
                PluginStep(.install, "aiko@aiko"),
                PluginStep(.uninstall, "aiko-copy@aiko"),
                PluginStep(.uninstall, "aiko-palette@aiko"),
            ])
    }

    @Test
    func oldPluginsGoEvenWhereThePersonaIsOff() {
        let steps = PluginPlan.steps(
            desired: [],
            state: state(Self.marketplace, installed: ["aiko-copy@aiko"]),
            marketplaceFolder: Self.marketplace)

        #expect(steps == [PluginStep(.uninstall, "aiko-copy@aiko")])
    }

    @Test
    func aFolderThatAlreadyMatchesNeedsNothing() {
        let steps = PluginPlan.steps(
            desired: [Self.persona],
            state: state(Self.marketplace + #"\"#, installed: [Self.persona], enabled: [Self.persona]),
            marketplaceFolder: Self.marketplace)

        #expect(steps.isEmpty)
    }

    @Test
    func aMarketplaceOfTheSameNameFromAnotherPlaceIsReplaced() {
        let steps = PluginPlan.steps(
            desired: [Self.persona],
            state: state(#"D:\old"#, installed: [Self.persona], enabled: [Self.persona]),
            marketplaceFolder: Self.marketplace)

        #expect(
            steps == [PluginStep(.removeMarketplace, "aiko"), PluginStep(.addMarketplace, Self.marketplace)])
    }

    @Test
    func stepsUseUserScopeAndAcceptTheCommandWithoutAsking() {
        #expect(PluginStep(.install, Self.persona).arguments == ["plugin", "install", Self.persona, "-y", "--scope", "user"])
        #expect(PluginStep(.disable, Self.persona).arguments == ["plugin", "disable", Self.persona, "--scope", "user"])
        #expect(PluginStep(.addMarketplace, Self.marketplace).arguments == ["plugin", "marketplace", "add", Self.marketplace])
    }

    // ---- running them ----

    final class ScriptedCli: ClaudeCli {
        let exitCodes: [Int]
        var calls: [(folder: String, arguments: [String])] = []

        init(_ exitCodes: Int...) {
            self.exitCodes = exitCodes
        }

        func run(configDirectory: String, arguments: [String], timeout: TimeInterval) -> CliResult {
            calls.append((configDirectory, arguments))
            let code = calls.count <= exitCodes.count ? exitCodes[calls.count - 1] : 0
            return CliResult(exitCode: code, timedOut: false)
        }
    }

    @Test
    func stepsRunInOrderForTheFolder() {
        let cli = ScriptedCli()
        let steps = [PluginStep(.addMarketplace, Self.marketplace), PluginStep(.install, Self.persona)]

        let results = PluginReconciler.apply(
            cli: cli, configDirectory: #"C:\Users\someone\.claude-work"#, steps: steps)

        #expect(results.allSatisfy { $0.result.succeeded })
        #expect(results.map(\.step.kind) == [.addMarketplace, .install])
        #expect(cli.calls.allSatisfy { $0.folder == #"C:\Users\someone\.claude-work"# })
    }

    @Test
    func aFailedMarketplaceStepStopsTheRun() {
        let cli = ScriptedCli(1)
        let steps = [PluginStep(.addMarketplace, Self.marketplace), PluginStep(.install, Self.persona)]

        let results = PluginReconciler.apply(cli: cli, configDirectory: #"C:\x"#, steps: steps)

        #expect(results.count == 1)
        #expect(cli.calls.count == 1)
    }

    @Test
    func oneFailedPluginDoesNotStopTheOthers() {
        let cli = ScriptedCli(0, 1, 0)
        let steps = [
            PluginStep(.enable, Self.persona),
            PluginStep(.install, "aiko@aiko"),
            PluginStep(.disable, "aiko-palette@aiko"),
        ]

        let results = PluginReconciler.apply(cli: cli, configDirectory: #"C:\x"#, steps: steps)

        #expect(results.map(\.result.succeeded) == [true, false, true])
    }
}
