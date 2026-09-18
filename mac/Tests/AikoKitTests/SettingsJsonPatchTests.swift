import Foundation
import Testing

@testable import AikoKit

struct SettingsJsonPatchTests {
    static let bridge = #""C:\Users\slayu\AppData\Local\Aiko\current\Aiko.Bridge.exe""#

    static let plainSettings = """
        {
          "$schema": "https://json.schemastore.org/claude-code-settings.json",
          "model": "opusplan",
          "theme": "dark"
        }
        """

    static let settingsWithOwnStatusLine = """
        {
          "model": "opusplan",
          "statusLine": { "type": "command", "command": "~/.claude/my-status.sh" }
        }
        """

    private var patch: SettingsJsonPatch { SettingsJsonPatch(.windows) }

    @Test
    func addsOurStatusLineAndKeepsEverythingElse() throws {
        let patched = try #require(patch.addBridge(Self.plainSettings, Self.bridge))

        let root = try #require(JsonNode.parse(patched)?.objectValue)
        #expect(root["model"]?.stringValue == "opusplan")
        #expect(root["theme"]?.stringValue == "dark")
        #expect(root["statusLine"]?["command"]?.stringValue == Self.bridge)
        #expect(root["statusLine"]?["type"]?.stringValue == "command")
    }

    @Test
    func keepsTheStatusLineTheUserAlreadyHad() throws {
        let patched = try #require(patch.addBridge(Self.settingsWithOwnStatusLine, Self.bridge))

        let root = try #require(JsonNode.parse(patched)?.objectValue)
        #expect(root["statusLine"]?["command"]?.stringValue == Self.bridge)
        #expect(root[SettingsJsonPatch.wrappedKey]?["command"]?.stringValue == "~/.claude/my-status.sh")
    }

    @Test
    func theBridgeCanReadTheCommandItHasToCall() throws {
        let patched = try #require(patch.addBridge(Self.settingsWithOwnStatusLine, Self.bridge))

        #expect(SettingsJsonPatch.readWrappedCommand(patched) == "~/.claude/my-status.sh")
        #expect(SettingsJsonPatch.readWrappedCommand(Self.plainSettings) == nil)
    }

    @Test
    func addingTwiceChangesNothing() throws {
        let once = try #require(patch.addBridge(Self.plainSettings, Self.bridge))

        #expect(patch.addBridge(once, Self.bridge) == nil)
    }

    @Test
    func removingPutsTheFileBackTheWayItWas() throws {
        let patched = try #require(patch.addBridge(Self.plainSettings, Self.bridge))

        let restored = try #require(patch.removeBridge(patched))

        let root = try #require(JsonNode.parse(restored)?.objectValue)
        #expect(!root.contains("statusLine"))
        #expect(!root.contains(SettingsJsonPatch.wrappedKey))
        #expect(root["model"]?.stringValue == "opusplan")
    }

    @Test
    func removingGivesTheUserTheirOwnStatusLineBack() throws {
        let patched = try #require(patch.addBridge(Self.settingsWithOwnStatusLine, Self.bridge))

        let restored = try #require(patch.removeBridge(patched))

        let root = try #require(JsonNode.parse(restored)?.objectValue)
        #expect(root["statusLine"]?["command"]?.stringValue == "~/.claude/my-status.sh")
        #expect(!root.contains(SettingsJsonPatch.wrappedKey))
    }

    @Test
    func removingFromAFileWeNeverTouchedChangesNothing() {
        #expect(patch.removeBridge(Self.plainSettings) == nil)
    }

    @Test(arguments: ["", "not json", "[1, 2]"])
    func aFileWeCannotReadIsLeftAlone(json: String) {
        #expect(patch.addBridge(json, Self.bridge) == nil)
        #expect(patch.removeBridge(json) == nil)
    }

    @Test
    func anEmptyCommandIsRefused() {
        #expect(patch.addBridge(Self.plainSettings, "   ") == nil)
    }

    @Test(arguments: [
        (#"{ "outputStyle": "Explanatory" }"#, "Explanatory"),
        (#"{ "outputStyle": "my-plugin:Terse" }"#, "my-plugin:Terse"),
        (#"{ "outputStyle": "default" }"#, nil),
        (#"{ "outputStyle": "Aiko" }"#, nil),
        (#"{ "outputStyle": "aiko-persona:Aiko" }"#, nil),
        (#"{ "outputStyle": "" }"#, nil),
        (#"{ "outputStyle": 3 }"#, nil),
        (#"{ "model": "opus" }"#, nil),
        ("not json", nil),
    ] as [(String, String?)])
    func onlyAStyleThePersonPickedCountsAsTheirOwn(json: String, expected: String?) {
        #expect(SettingsJsonPatch.userOutputStyle(json) == expected)
    }

    // ---- the session start hook, from SessionReminderTests ----

    static let hook = #""C:\Program Files\Aiko\Aiko.Bridge.exe" --session-start"#

    @Test
    func theHookIsAddedBesideThePersonsOwnHooks() throws {
        let before = """
            {
              "model": "opus",
              "hooks": {
                "SessionStart": [ { "matcher": "startup", "hooks": [ { "type": "command", "command": "echo hi" } ] } ],
                "Stop": [ { "hooks": [ { "type": "command", "command": "notify" } ] } ]
              }
            }
            """

        let after = try #require(patch.addSessionHook(before, Self.hook))

        let groups = try #require(JsonNode.parse(after)?["hooks"]?["SessionStart"]?.arrayValue)
        #expect(groups.count == 2)
        #expect(groups[0]["hooks"]?.arrayValue?[0]["command"]?.stringValue == "echo hi")
        #expect(groups[1]["hooks"]?.arrayValue?[0]["command"]?.stringValue == Self.hook)
        #expect(JsonNode.parse(after)?["hooks"]?["Stop"] != nil)
        #expect(patch.hasOurSessionHook(after))
    }

    @Test
    func addingTwiceChangesNothingAndAnOldPathIsReplaced() throws {
        let once = try #require(patch.addSessionHook("{}", Self.hook))
        #expect(patch.addSessionHook(once, Self.hook) == nil)

        let moved = #""D:\Aiko\Aiko.Bridge.exe" --session-start"#
        let replaced = try #require(patch.addSessionHook(once, moved))

        let commands = try #require(JsonNode.parse(replaced)?["hooks"]?["SessionStart"]?.arrayValue)
        #expect(commands.count == 1)
        #expect(commands[0]["hooks"]?.arrayValue?[0]["command"]?.stringValue == moved)
    }

    @Test
    func removingAikoTakesTheHookOutAndLeavesTheFileAsItWas() throws {
        let before = #"{ "hooks": { "SessionStart": [ { "hooks": [ { "type": "command", "command": "echo hi" } ] } ] } }"#

        let withLine = try #require(patch.addBridge(before, #""C:\A\Aiko.Bridge.exe""#))
        let withBoth = try #require(patch.addSessionHook(withLine, Self.hook))

        let restored = try #require(patch.removeBridge(withBoth))
        #expect(JsonNode.parse(restored) == JsonNode.parse(before))
    }

    @Test
    func aFileWithOnlyOurHookLosesTheEmptyHooksObject() throws {
        let withHook = try #require(patch.addSessionHook(#"{ "model": "opus" }"#, Self.hook))

        let restored = try #require(patch.removeBridge(withHook))
        #expect(JsonNode.parse(restored) == JsonNode.parse(#"{ "model": "opus" }"#))
    }

    @Test
    func hooksThatAreNotTheExpectedShapeAreNotTouched() {
        #expect(patch.addSessionHook(#"{ "hooks": "oops" }"#, Self.hook) == nil)
        #expect(patch.addSessionHook(#"{ "hooks": { "SessionStart": {} } }"#, Self.hook) == nil)
    }

    @Test
    func theHookCommandIsTheBridgeWithOneArgument() {
        #expect(BridgeCommand.hookFor(#"C:\A\Aiko.Bridge.exe"#) == #""C:\A\Aiko.Bridge.exe" --session-start"#)
        #expect(
            BridgeCommand.hookFor(#"C:\A\Aiko.Bridge.exe"#, .powerShell)
                == #"& "C:\A\Aiko.Bridge.exe" --session-start"#)
        #expect(BridgeCommand.isAiko(.windows, BridgeCommand.hookFor(#"C:\A\Aiko.Bridge.exe"#)))
    }
}
