import Foundation
import Testing

@testable import AikoKit

/// The memory server, one line in and one line out. Claude Code is the client, so the shapes it
/// expects are checked here: the echoed id, the tool list, the text of a tool result.
struct MemoryServerTests {
    static let today = DateOnly(2026, 10, 11)

    static func ask(_ line: String, _ memory: MemoryFile = .empty) -> MemoryServerReply {
        MemoryServer.answer(line, memory, today, "0.4.0")
    }

    static func json(_ reply: MemoryServerReply) -> JsonNode? {
        JsonNode.parse(reply.line)
    }

    static func toolText(_ reply: MemoryServerReply) -> String? {
        json(reply)?["result"]?["content"]?.arrayValue?.first?["text"]?.stringValue
    }

    static let listTools = #"{"jsonrpc":"2.0","id":1,"method":"tools/list"}"#

    @Test
    func initializeAnswersWithTheProtocolTheClientNamed() {
        let reply = Self.ask(
            #"{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-03-26"}}"#)

        let result = Self.json(reply)?["result"]
        #expect(result?["protocolVersion"]?.stringValue == "2025-03-26")
        #expect(result?["serverInfo"]?["name"]?.stringValue == "aiko-memory")
        #expect(result?["serverInfo"]?["version"]?.stringValue == "0.4.0")
        #expect(result?["capabilities"]?["tools"]?.isObject == true)
        #expect(!reply.changed)
    }

    @Test
    func initializeWithoutAProtocolAnswersWithOurs() {
        let reply = Self.ask(#"{"jsonrpc":"2.0","id":1,"method":"initialize"}"#)

        #expect(Self.json(reply)?["result"]?["protocolVersion"]?.stringValue == MemoryServer.defaultProtocol)
    }

    @Test(arguments: [("7", "7"), ("\"abc\"", "\"abc\"")])
    func theIdGoesBackAsItCame(id: String, expected: String) {
        let reply = Self.ask(#"{"jsonrpc":"2.0","id":\#(id),"method":"ping"}"#)

        let written = JsonRawText.members(reply.line ?? "")?.first { $0.key == "id" }?.value
        #expect(written == expected)
        #expect(Self.json(reply)?["result"]?.isObject == true)
    }

    @Test
    func aNotificationGetsNoAnswer() {
        let reply = Self.ask(#"{"jsonrpc":"2.0","method":"notifications/initialized"}"#)

        #expect(reply.line == nil)
        #expect(!reply.changed)
    }

    @Test
    func anUnknownMethodIsAnErrorNotSilence() {
        let reply = Self.ask(#"{"jsonrpc":"2.0","id":3,"method":"resources/list"}"#)

        #expect(Self.json(reply)?["error"]?["code"]?.int64Value == -32601)
    }

    @Test(arguments: ["not json", "[1, 2]"])
    func aLineThatIsNotAMessageIsAParseError(line: String) {
        let reply = Self.ask(line)

        #expect(Self.json(reply)?["error"]?["code"]?.int64Value == -32700)
        #expect(Self.json(reply)?["id"] == .null)
    }

    @Test
    func everyAnswerIsOneLine() {
        let reply = Self.ask(Self.listTools)

        #expect(reply.line?.contains("\n") == false)
    }

    @Test
    func bothToolsAreListedAndAlwaysLoaded() {
        let tools = Self.json(Self.ask(Self.listTools))?["result"]?["tools"]?.arrayValue ?? []

        #expect(tools.map { $0["name"]?.stringValue } == ["remember", "forget"])
        #expect(tools.allSatisfy { $0["_meta"]?["anthropic/alwaysLoad"]?.boolValue == true })
        #expect(tools.allSatisfy { $0["inputSchema"]?["type"]?.stringValue == "object" })
    }

    @Test
    func rememberListsTheFourCategoriesAndNoScope() {
        let properties = Self.json(Self.ask(Self.listTools))?["result"]?["tools"]?.arrayValue?.first?["inputSchema"]?["properties"]

        #expect(properties?["category"]?["enum"]?.arrayValue?.map(\.stringValue)
            == ["you", "work", "projects", "interests"])
        #expect(properties?.objectValue?.contains("scope") == false)
        #expect(properties?["keys"]?["maxItems"]?.int64Value == Int64(MemoryRules.maxKeys))
    }

    @Test
    func rememberSavesAFactAndSaysSo() {
        let reply = Self.ask(
            #"{"jsonrpc":"2.0","id":5,"method":"tools/call","params":{"name":"remember","arguments":{"category":"projects","fact":"Makes Aiko","meaning":"Own product","keys":["Aiko"],"pin":false}}}"#)

        #expect(reply.memory.facts.count == 1)
        let fact = reply.memory.facts.first
        #expect(reply.changed)
        #expect(Self.toolText(reply) == "Saved F1.")
        #expect(Self.json(reply)?["result"]?["isError"]?.boolValue == false)
        #expect(fact?.text == "Makes Aiko")
        #expect(fact?.meaning == "Own product")
        #expect(fact?.keys == ["Aiko"])
        #expect(fact?.seen == Self.today)
    }

    @Test
    func aRefusedSaveChangesNothingAndTheReplySaysWhy() {
        let tooLong = String(repeating: "a", count: MemoryRules.maxLength + 1)

        let reply = Self.ask(
            #"{"jsonrpc":"2.0","id":5,"method":"tools/call","params":{"name":"remember","arguments":{"category":"you","fact":"FACT"}}}"#
                .replacingOccurrences(of: "FACT", with: tooLong))

        #expect(!reply.changed)
        #expect(reply.memory.facts.isEmpty)
        #expect(Self.toolText(reply)?.contains("Too long") == true)
    }

    @Test
    func forgetRemovesTheFact() {
        let memory = MemoryFile(
            2, [MemoryFact("F1", .you, "Learns Swift", seen: Self.today, created: Self.today)], [])

        let reply = Self.ask(
            #"{"jsonrpc":"2.0","id":6,"method":"tools/call","params":{"name":"forget","arguments":{"id":"F1"}}}"#,
            memory)

        #expect(reply.changed)
        #expect(reply.memory.facts.isEmpty)
        #expect(Self.toolText(reply) == "Forgot F1.")
    }

    @Test
    func aReplyKeepsCyrillicReadable() {
        let first = MemoryFact("F1", .work, "Говорит «коротко»", seen: Self.today, created: Self.today)
        let rest = (2...MemoryRules.quota(.work)).map {
            MemoryFact(MemoryFact.idFor($0), .work, "habit \($0)", seen: Self.today, created: Self.today)
        }
        let memory = MemoryFile(20, [first] + rest, [])

        let reply = Self.ask(
            #"{"jsonrpc":"2.0","id":7,"method":"tools/call","params":{"name":"remember","arguments":{"category":"work","fact":"One more habit"}}}"#,
            memory)

        #expect(!reply.changed)
        #expect(reply.line?.contains("Говорит «коротко»") == true)
    }

    @Test
    func anUnknownToolIsAToolError() {
        let reply = Self.ask(
            #"{"jsonrpc":"2.0","id":8,"method":"tools/call","params":{"name":"recall","arguments":{}}}"#)

        #expect(Self.json(reply)?["result"]?["isError"]?.boolValue == true)
        #expect(Self.toolText(reply) == "Unknown tool recall.")
        #expect(!reply.changed)
    }

    @Test
    func argumentsOfAWrongTypeAreReadAsMissing() {
        let reply = Self.ask(
            #"{"jsonrpc":"2.0","id":9,"method":"tools/call","params":{"name":"remember","arguments":{"category":"you","fact":"Learns Swift","keys":"Swift","pin":"yes"}}}"#)

        #expect(reply.memory.facts.count == 1)
        #expect(reply.memory.facts.first?.keys == [])
        #expect(reply.memory.facts.first?.pinned == false)
    }
}
