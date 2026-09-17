import Foundation
import Testing

@testable import AikoKit

/// Taking Aiko's plugins out of a Claude Code folder when Aiko, an environment or the whole setup
/// goes (D-205).
struct PluginRemovalTests {
    static let folder = #"C:\Users\someone\.claude"#
    static let persona = "aiko-persona@aiko"

    static var settings: String { ClaudeSettingsEditor.pathIn(folder) }
    static var backup: String { ClaudeSettingsEditor.backupPathIn(folder) }

    static let withPlugins = """
        {
          "model": "opus",
          "enabledPlugins": { "aiko-persona@aiko": true, "aiko-copy@aiko": false, "frontend-design@claude-plugins-official": true },
          "extraKnownMarketplaces": {
            "aiko": { "source": { "source": "directory", "path": "C:\\\\m" } },
            "team": { "source": { "source": "github", "repo": "team/plugins" } }
          },
          "outputStyle": "Explanatory"
        }
        """

    private var patch: SettingsJsonPatch { TestBridge.patch }

    private func editor(_ files: FakeFiles) -> ClaudeSettingsEditor {
        ClaudeSettingsEditor(files: files, patch: patch)
    }

    // ---- the JSON ----

    @Test
    func onlyOurPluginsAndMarketplaceLeaveTheFile() throws {
        let restored = try #require(patch.removePlugins(Self.withPlugins))

        let root = try #require(JsonNode.parse(restored)?.objectValue)
        #expect(root["enabledPlugins"]?.objectValue?.keys == ["frontend-design@claude-plugins-official"])
        #expect(root["extraKnownMarketplaces"]?.objectValue?.keys == ["team"])
        #expect(root["model"]?.stringValue == "opus")
        #expect(root["outputStyle"]?.stringValue == "Explanatory")
    }

    @Test
    func keysLeftEmptyByTheRemovalGoToo() throws {
        let json = #"{ "enabledPlugins": { "aiko-persona@aiko": true }, "extraKnownMarketplaces": { "aiko": {} } }"#

        let restored = try #require(patch.removePlugins(json))

        #expect(JsonNode.parse(restored)?.objectValue?.isEmpty == true)
    }

    @Test(arguments: [
        #"{ "enabledPlugins": { "frontend-design@claude-plugins-official": true } }"#,
        #"{ "enabledPlugins": {} }"#,
        #"{ "enabledPlugins": [1] }"#,
        "not json",
    ])
    func aFileWithoutOurPluginsIsLeftAlone(json: String) {
        #expect(patch.removePlugins(json) == nil)
    }

    @Test
    func theEmptyKeysTheUninstallCommandWritesBackAreDropped() throws {
        let tidy = try #require(patch.dropEmptyPluginKeys(#"{ "model": "opus", "enabledPlugins": {} }"#))
        #expect(JsonNode.parse(tidy)?.objectValue?.keys == ["model"])

        #expect(patch.dropEmptyPluginKeys(#"{ "enabledPlugins": { "x@y": true } }"#) == nil)
    }

    @Test(arguments: [
        (#"{ "enabledPlugins": { "aiko-copy@aiko": false } }"#, true),
        (#"{ "extraKnownMarketplaces": { "aiko": {} } }"#, true),
        (#"{ "aikoWrappedStatusLine": { "command": "mine.sh" } }"#, true),
        (#"{ "enabledPlugins": { "frontend-design@claude-plugins-official": true } }"#, false),
        (#"{ "statusLine": { "type": "command", "command": "mine.sh" } }"#, false),
        ("{}", false),
    ])
    func anythingOfOursCountsAsAikoBeingInTheFile(json: String, ours: Bool) {
        #expect(patch.hasAnyAikoEntries(json) == ours)
    }

    // ---- the file and its copy ----

    @Test
    func removalTakesThePluginsOutWithTheStatusLine() {
        let files = FakeFiles().with(Self.settings, Self.withPlugins)
        let editor = editor(files)
        _ = editor.add(Self.folder, TestBridge.command(#"C:\a\Aiko.Bridge.exe"#))

        #expect(editor.remove(Self.folder).changed)

        let text = files.read(Self.settings)
        #expect(!text.contains("@aiko"))
        #expect(!text.contains(SettingsJsonPatch.statusLineKey))
        #expect(!files.has(Self.backup))
    }

    @Test
    func aFolderWithOnlyPluginsOfOursIsCleanedToo() {
        let files = FakeFiles().with(Self.settings, Self.withPlugins)

        #expect(editor(files).remove(Self.folder).changed)
        #expect(!files.read(Self.settings).contains("@aiko"))
    }

    @Test
    func tidyingAfterTheCommandKeepsTheFileAndTheCopy() {
        let files = FakeFiles().with(Self.settings, #"{ "enabledPlugins": {} }"#).with(Self.backup, "{}")

        #expect(editor(files).tidyAfterPluginRemoval(Self.folder).changed)

        #expect(files.has(Self.settings))
        #expect(files.has(Self.backup))
        #expect(JsonNode.parse(files.read(Self.settings))?.objectValue?.isEmpty == true)
    }

    // ---- the steps ----

    @Test
    func removalUninstallsEveryPluginOfOursThenTheMarketplace() {
        let state = PluginState(
            marketplaceFolder: #"C:\m"#, installed: [Self.persona, "aiko-copy@aiko"], enabled: [])

        #expect(
            PluginPlan.removal(state) == [
                PluginStep(.uninstall, "aiko-copy@aiko"),
                PluginStep(.uninstall, Self.persona),
                PluginStep(.removeMarketplace, "aiko"),
            ])
        #expect(
            PluginStep(.uninstall, Self.persona).arguments == ["plugin", "uninstall", Self.persona, "--scope", "user"])
    }

    @Test
    func aFolderWithNothingOfOursNeedsNoSteps() {
        #expect(PluginPlan.removal(.nothing).isEmpty)
    }

    final class SlowCli: ClaudeCli {
        var now: Date
        let each: TimeInterval
        var timeouts: [TimeInterval] = []

        init(now: Date, each: TimeInterval) {
            self.now = now
            self.each = each
        }

        func run(configDirectory: String, arguments: [String], timeout: TimeInterval) -> CliResult {
            timeouts.append(timeout)
            now = now.addingTimeInterval(each)
            return CliResult(exitCode: 0, timedOut: false)
        }
    }

    @Test
    func noStepStartsAfterTheDeadlineAndNoneGetsMoreTimeThanIsLeft() {
        let start = utc(2026, 9, 15, 12)
        let cli = SlowCli(now: start, each: 8)
        let steps = Array(repeating: PluginStep(.uninstall, Self.persona), count: 4)

        let results = PluginReconciler.apply(
            cli: cli,
            configDirectory: Self.folder,
            steps: steps,
            now: { cli.now },
            deadline: start.adding(seconds: 20))

        #expect(results.count == 3)
        #expect(cli.timeouts == [20, 12, 4])
    }
}
