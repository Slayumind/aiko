import Foundation
import Testing

@testable import AikoKit

/// The one permission rule Aiko writes into a person's settings.json for the memory tools (D-279).
/// It is their file: only our rule may move, and what they wrote must come through untouched.
struct MemoryPermissionTests {
    static let folder = #"C:\Users\someone\.claude"#
    static let rule = "mcp__plugin_aiko-persona_memory"

    static var settings: String { ClaudeSettingsEditor.pathIn(folder) }
    static var backup: String { ClaudeSettingsEditor.backupPathIn(folder) }

    private var patch: SettingsJsonPatch { SettingsJsonPatch(.windows) }

    private func set(_ json: String, allowed: Bool) -> String {
        patch.setMemoryPermission(json, allowed: allowed) ?? "(unchanged)"
    }

    private func allow(_ json: String) -> [String]? {
        JsonNode.parse(json)?["permissions"]?["allow"]?.arrayValue?.compactMap(\.stringValue)
    }

    private func editor(_ files: FakeFiles) -> ClaudeSettingsEditor {
        ClaudeSettingsEditor(files: files, patch: patch)
    }

    @Test
    func theRuleIsAddedToAFileWithoutPermissions() {
        let patched = set(#"{ "model": "opus" }"#, allowed: true)

        #expect(allow(patched) == [Self.rule])
        #expect(JsonNode.parse(patched)?["model"]?.stringValue == "opus")
        #expect(patch.hasMemoryPermission(patched))
    }

    @Test
    func theRuleJoinsTheRulesThePersonAlreadyHas() {
        let patched = set(
            #"{ "permissions": { "allow": ["Bash(git status)"], "deny": ["Read(.env)"] } }"#, allowed: true)

        #expect(allow(patched) == ["Bash(git status)", Self.rule])
        #expect(JsonNode.parse(patched)?["permissions"]?["deny"]?.arrayValue?.first?.stringValue == "Read(.env)")
    }

    @Test
    func aRuleThatIsThereIsNotAddedTwice() {
        let once = set("{}", allowed: true)

        #expect(set(once, allowed: true) == "(unchanged)")
    }

    @Test(arguments: [
        #"{ "permissions": "all" }"#,
        #"{ "permissions": { "allow": "Bash" } }"#,
        "not json",
        "[]",
    ])
    func aFileOfAShapeWeDoNotKnowIsLeftAlone(_ json: String) {
        #expect(set(json, allowed: true) == "(unchanged)")
        #expect(set(json, allowed: false) == "(unchanged)")
    }

    @Test
    func takingTheRuleOutKeepsTheOtherRules() {
        let with = set(#"{ "permissions": { "allow": ["Bash(git status)"] } }"#, allowed: true)

        let without = set(with, allowed: false)

        #expect(allow(without) == ["Bash(git status)"])
        #expect(!patch.hasMemoryPermission(without))
    }

    @Test
    func takingTheRuleOutLeavesNoEmptyKeysBehind() throws {
        let without = set(set(#"{ "model": "opus" }"#, allowed: true), allowed: false)

        let root = try #require(JsonNode.parse(without)?.objectValue)
        #expect(!root.contains("permissions"))
        #expect(root["model"]?.stringValue == "opus")
    }

    @Test
    func takingTheRuleOutKeepsPermissionsThatStillSaySomething() throws {
        let with = set(#"{ "permissions": { "deny": ["Read(.env)"] } }"#, allowed: true)

        let permissions = try #require(
            JsonNode.parse(set(with, allowed: false))?["permissions"]?.objectValue)

        #expect(!permissions.contains("allow"))
        #expect(permissions["deny"]?.arrayValue?.count == 1)
    }

    @Test
    func nothingToTakeOutChangesNothing() {
        #expect(set(#"{ "permissions": { "allow": ["Bash(git status)"] } }"#, allowed: false) == "(unchanged)")
    }

    @Test
    func theRuleCountsAsAnEntryOfAiko() {
        let with = set("{}", allowed: true)

        #expect(patch.hasAnyAikoEntries(with))
        #expect(!patch.hasAnyAikoEntries(#"{ "permissions": { "allow": ["Bash"] } }"#))
    }

    @Test
    func theEditorBacksTheFileUpBeforeTheFirstChangeAndWritesOnce() {
        let files = FakeFiles().with(Self.settings, #"{ "model": "opus" }"#)
        let editor = editor(files)

        let first = editor.setMemoryPermission(Self.folder, allowed: true)
        let second = editor.setMemoryPermission(Self.folder, allowed: true)

        #expect(first.changed)
        #expect(!second.changed)
        #expect(files.read(Self.backup) == #"{ "model": "opus" }"#)
        #expect(allow(files.read(Self.settings)) == [Self.rule])
    }

    @Test
    func removingAikoFromAFolderTakesTheRuleWithIt() {
        let files = FakeFiles().with(Self.settings, #"{ "model": "opus" }"#)
        let editor = editor(files)
        _ = editor.setMemoryPermission(Self.folder, allowed: true)

        let outcome = editor.remove(Self.folder)

        #expect(outcome.changed)
        #expect(!patch.hasMemoryPermission(files.read(Self.settings)))
        #expect(!files.has(Self.backup))
    }
}
