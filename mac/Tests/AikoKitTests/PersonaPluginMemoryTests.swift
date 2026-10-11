import Foundation
import Testing

@testable import AikoKit

/// What the persona plugin gains when the memory is on in its environment: two hooks at the start
/// of a session, one beside every message, and the server with the memory tools.
struct PersonaPluginMemoryTests {
    static let bridge = #"C:\Users\Саша Иванов\AppData\Local\Slayumind.Aiko\current\Aiko.Bridge.exe"#

    private var withMemory: [(path: String, content: String)] {
        PersonaPlugin.files(.normal, Self.bridge, memory: true)
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
    func withoutTheMemoryThePluginIsWhatItWas() throws {
        let files = PersonaPlugin.files(.normal, Self.bridge, memory: false)

        #expect(files.map(\.path) == PersonaPlugin.files(.normal, Self.bridge).map(\.path))
        #expect(files.map(\.content) == PersonaPlugin.files(.normal, Self.bridge).map(\.content))
        #expect(!files.contains { $0.path == ".mcp.json" })
        #expect(try hooks(files)["SessionStart"] == nil)
    }

    @Test
    func withTheMemoryThePluginBringsAServerFile() {
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
    func turningTheMemoryOnChangesTheHashSoThePluginIsUpdated() {
        let off = PersonaPlugin.contentHash(PersonaPlugin.files(.normal, Self.bridge))

        #expect(off != PersonaPlugin.contentHash(withMemory))
    }

    @Test
    func thePermissionNamesTheServerTheWayClaudeCodeNamesPluginTools() {
        #expect(PersonaPlugin.memoryPermission == "mcp__plugin_aiko-persona_memory")
    }

    @Test
    func thePluginHasTheMemoryOnlyForAFolderOfAnEnvironmentWithIt() {
        let withMemory = AikoEnvironment("Aiko", [#"C:\Users\someone\.claude"#], persona: true, memory: true)
        let personaOff = AikoEnvironment("Work", [#"C:\Users\someone\.claude-work"#], memory: true)
        let settings = EnvironmentSettings([withMemory, personaOff])

        #expect(PersonaPluginOutput.hasMemory(settings, #"C:\Users\someone\.claude"#))
        #expect(PersonaPluginOutput.hasMemory(settings, #"c:\users\someone\.claude\"#))
        #expect(!PersonaPluginOutput.hasMemory(settings, #"C:\Users\someone\.claude-work"#))
        #expect(!PersonaPluginOutput.hasMemory(settings, #"C:\Users\someone\.claude-other"#))
        #expect(!PersonaPluginOutput.hasMemory(settings, nil))
        #expect(!PersonaPluginOutput.hasMemory(.empty, #"C:\Users\someone\.claude"#))
    }
}
