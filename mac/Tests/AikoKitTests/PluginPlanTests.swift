import Foundation
import Testing

@testable import AikoKit

struct PluginPlanTests {
    static let marketplace = #"C:\Users\Тест Юзер\AppData\Local\Aiko\marketplace"#
    static let localAppData = #"C:\Users\Тест Юзер\AppData\Local"#
    static let folders = AikoFolders.windows(#"C:\Users\Тест Юзер\AppData\Roaming"#, localAppData)
    static let persona = "aiko-persona@aiko"

    private func env(persona: Bool) -> AikoEnvironment {
        AikoEnvironment("Aiko", [#"C:\Users\someone\.claude"#], persona: persona)
    }

    // ---- the command and the marketplace file ----

    @Test
    func theInstalledBridgeIsNamedThroughLocalAppDataSoTheCommandStaysAscii() {
        let command = AikoMarketplace.personaCommand(
            .windows, Self.localAppData + #"\Slayumind.Aiko\current\Aiko.Bridge.exe"#, Self.folders)

        #expect(command == #""%LOCALAPPDATA%\Slayumind.Aiko\current\Aiko.Bridge.exe" plugin aiko-persona"#)
    }

    @Test
    func aBuildFolderIsNamedAsItIsAndRefusedWhenItIsNotAscii() {
        #expect(
            AikoMarketplace.personaCommand(.windows, #"D:\src\Aiko.Bridge.exe"#, Self.folders)
                == #""D:\src\Aiko.Bridge.exe" plugin aiko-persona"#)
        #expect(
            AikoMarketplace.personaCommand(.windows, Self.localAppData + #"\dev\Aiko.Bridge.exe"#, Self.folders)
                == nil)
    }

    @Test(arguments: [
        (#""C:\x\a.exe" plugin p"#, true),
        ("", false),
        ("a    b", false),
        ("tab\there", false),
    ] as [(String, Bool)])
    func commandsFollowTheClaudeCodeRules(command: String, valid: Bool) {
        #expect(AikoMarketplace.isValidCommand(command) == valid)
        #expect(!AikoMarketplace.isValidCommand(String(repeating: "a", count: 501)))
    }

    @Test
    func theMarketplaceListsThePersonaAsACommandSource() {
        let root = JsonNode.parse(AikoMarketplace.json(#""x.exe" plugin aiko-persona"#))
        let plugin = root?["plugins"]?.arrayValue?.first

        #expect(root?["name"]?.stringValue == "aiko")
        #expect(plugin?["name"]?.stringValue == "aiko-persona")
        #expect(plugin?["source"]?["source"]?.stringValue == "command")
        #expect(plugin?["source"]?["command"]?.stringValue == #""x.exe" plugin aiko-persona"#)
    }

    @Test
    func theMarketplaceFileKeepsQuotesReadable() {
        let text = AikoMarketplace.json(##""%LOCALAPPDATA%\x.exe" plugin aiko-persona"##)

        #expect(!text.contains(##"\u0022"##))
        #expect(text.contains(##""\"%LOCALAPPDATA%\\x.exe\" plugin aiko-persona""##))
    }

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
        #expect(PluginPlan.desired(env(persona: false), persona: .default, skills: ["copy"]).isEmpty)
    }

    @Test
    func withThePersonaAFolderWantsItAndTheSkillsPlugin() {
        let desired = PluginPlan.desired(env(persona: true), persona: .default, skills: ["copy", "palette"])

        #expect(desired.sorted() == [Self.persona, "aiko@aiko"].sorted())
    }

    @Test
    func withTheSkillsOffOrNoneShippedAFolderWantsOnlyThePersona() {
        var skillsOff = PersonaSettings.default
        skillsOff.skillsOn = false

        #expect(PluginPlan.desired(env(persona: true), persona: skillsOff, skills: ["copy", "palette"]) == [Self.persona])
        #expect(PluginPlan.desired(env(persona: true), persona: .default, skills: []) == [Self.persona])
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
