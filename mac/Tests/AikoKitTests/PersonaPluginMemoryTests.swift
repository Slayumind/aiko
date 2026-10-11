import Foundation
import Testing

@testable import AikoKit

/// The memory inside the persona plugin: two hooks at the start of a session, one beside every
/// message, and the server with the memory tools. Aiko is not switched on without it (D-285).
struct PersonaPluginMemoryTests {
    static let bridge = #"C:\Users\Саша Иванов\AppData\Local\Slayumind.Aiko\current\Aiko.Bridge.exe"#

    private var withMemory: [(path: String, content: String)] {
        PersonaPlugin.files(.normal, Self.bridge)
    }

    private func hooks(_ files: [(path: String, content: String)]) throws -> JsonObject {
        let file = try #require(files.first { $0.path == "hooks/hooks.json" })
        return try #require(JsonNode.parse(file.content)?["hooks"]?.objectValue)
    }

    private func hooksOf(_ events: JsonObject, _ name: String) -> [JsonNode] {
        events[name]?.arrayValue?.first?["hooks"]?.arrayValue ?? []
    }

    private func args(_ hook: JsonNode?) -> [String] {
        hook?["args"]?.arrayValue?.compactMap(\.stringValue) ?? []
    }

    @Test
    func thePluginBringsAServerFile() {
        #expect(withMemory.map(\.path) == [
            ".claude-plugin/plugin.json", ".mcp.json", "hooks/hooks.json", "output-styles/aiko.md",
        ])
    }

    @Test
    func theServerIsTheBridgeInItsServeMode() throws {
        let file = try #require(withMemory.first { $0.path == ".mcp.json" })
        let server = try #require(JsonNode.parse(file.content)?["mcpServers"]?[PersonaPlugin.memoryServer])

        #expect(server["command"]?.stringValue == Self.bridge)
        #expect(args(server) == ["memory", "serve"])
        #expect(MemoryBridge.modeFor(args(server)) == .serve)
    }

    @Test
    func theRulesAndTheFactsAreTwoHooksAtTheStartOfASession() throws {
        let start = hooksOf(try hooks(withMemory), "SessionStart")

        #expect(start.count == 2)
        #expect(MemoryBridge.modeFor(args(start.first)) == .rules)
        #expect(MemoryBridge.modeFor(args(start.last)) == .facts)
    }

    @Test
    func aMessageRunsTheFaceHookAndThenTheMemoryHook() throws {
        let prompt = hooksOf(try hooks(withMemory), "UserPromptSubmit")

        #expect(prompt.count == 2)
        #expect(args(prompt.first) == ["hook"])
        #expect(MemoryBridge.modeFor(args(prompt.last)) == .prompt)
    }

    @Test
    func memoryHooksAreWaitedForBecauseTheModelReadsTheirOutput() throws {
        let events = try hooks(withMemory)
        let memoryHooks = hooksOf(events, "SessionStart") + hooksOf(events, "UserPromptSubmit").suffix(1)

        #expect(memoryHooks.count == 3)
        for hook in memoryHooks {
            #expect(hook["async"] == nil)
            #expect(hook["type"]?.stringValue == "command")
            #expect(hook["command"]?.stringValue == Self.bridge)
        }
    }

    @Test
    func theOtherEventsKeepOnlyTheFaceHook() throws {
        let events = try hooks(withMemory)

        for name in PersonaPlugin.hookEvents where name != "UserPromptSubmit" {
            #expect(hooksOf(events, name).count == 1)
        }
    }

    @Test
    func thePermissionNamesTheServerTheWayClaudeCodeNamesPluginTools() {
        #expect(PersonaPlugin.memoryPermission == "mcp__plugin_aiko-persona_memory")
    }

    @Test(arguments: [
        (nil, true),
        ("", true),
        ("0123456789ab", false),
        ("0123456789ab\n", false),
        ("ba9876543210", true),
    ] as [(String?, Bool)])
    func thePluginIsUpdatedWhenItsContentIsNotTheOneNoted(_ synced: String?, _ needsUpdate: Bool) {
        #expect(PersonaPluginOutput.needsUpdate(synced, "0123456789ab") == needsUpdate)
    }

    @Test
    func theNoteSitsBesideTheVersionsAndIsNotTakenForOne() {
        let folders = AikoFolders.windows(#"C:\Users\someone\AppData\Roaming"#, #"C:\Users\someone\AppData\Local"#)
        let home = PersonaPluginOutput.environmentFolder(folders, #"C:\Users\someone\.claude"#)
        let note = PersonaPluginOutput.syncedFile(folders, #"C:\Users\someone\.claude"#)
        let name = PersonaPluginOutput.syncedName

        #expect(note == folders.platform.join(home, name))
        #expect(PersonaPluginOutput.staleFolders(
            [(name: name, written: Date.distantPast)], currentHash: "0123456789ab", now: Date()).isEmpty)
    }
}
