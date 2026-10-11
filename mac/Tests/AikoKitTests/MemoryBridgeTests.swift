import Foundation
import Testing

@testable import AikoKit

/// What the bridge prints and writes for the memory. The bridge itself only moves bytes, so the
/// hook shapes Claude Code expects and the "write or not" decisions are all checked here.
struct MemoryBridgeTests {
    static let rememberSwift =
        #"{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"remember","arguments":{"category":"you","fact":"Learns Swift"}}}"#

    static let today = DateOnly(2026, 10, 11)
    static let yesterday = DateOnly(2026, 10, 10)

    static func fact(_ number: Int, _ text: String, _ keys: String...) -> MemoryFact {
        MemoryFact(MemoryFact.idFor(number), .projects, text, keys: keys, seen: yesterday, created: yesterday)
    }

    static func hook(_ hookOutput: String?) -> JsonNode? {
        JsonNode.parse(hookOutput)?["hookSpecificOutput"]
    }

    static func context(_ hookOutput: String?) -> String? {
        hook(hookOutput)?["additionalContext"]?.stringValue
    }

    /// Written the way JsonSerializer writes it: every letter outside ASCII as an escape.
    static func promptInput(_ prompt: String) -> String {
        JsonNode.object(JsonObject([
            ("hook_event_name", .string("UserPromptSubmit")),
            ("prompt", .string(prompt)),
        ])).toJsonString()
    }

    @Test(arguments: [
        ("rules", MemoryBridgeMode.rules),
        ("facts", MemoryBridgeMode.facts),
        ("prompt", MemoryBridgeMode.prompt),
        ("serve", MemoryBridgeMode.serve),
    ])
    func theSecondArgumentNamesTheJob(argument: String, mode: MemoryBridgeMode) {
        #expect(MemoryBridge.modeFor(["memory", argument]) == mode)
        #expect(MemoryBridge.argumentFor(mode) == argument)
    }

    @Test(arguments: [
        [],
        ["memory"],
        ["memory", "erase"],
        ["hook", "rules"],
    ] as [[String]])
    func anythingElseIsNotAMemoryJob(arguments: [String]) {
        #expect(MemoryBridge.modeFor(arguments) == nil)
    }

    @Test
    func theRulesGoToTheModelAsSessionStartContext() {
        let output = MemoryBridge.rulesOutput(Self.today)

        #expect(Self.hook(output)?["hookEventName"]?.stringValue == "SessionStart")
        #expect(Self.context(output) == MemoryPrompt.rules(Self.today))
    }

    @Test
    func theFactsGoInABlockOfTheirOwn() {
        let memory = MemoryFile(2, [Self.fact(1, "Makes Aiko", "Aiko")], [])

        #expect(Self.context(MemoryBridge.factsOutput(memory.toJson())) == MemoryPrompt.facts(memory))
    }

    @Test(arguments: [nil, "", "not json"] as [String?])
    func noFileOrABrokenOneGivesAnEmptyListNotAnError(json: String?) {
        #expect(Self.context(MemoryBridge.factsOutput(json)) == MemoryPrompt.facts(.empty))
    }

    @Test
    func aFileGrownByHandStillFitsAndLosesItsLongestFactsFirst() {
        let huge = Self.fact(1, String(repeating: "x", count: 6_000))
        let big = Self.fact(2, String(repeating: "y", count: 5_000))
        let small = Self.fact(3, "Makes Aiko")
        var pinned = Self.fact(4, String(repeating: "z", count: 3_000))
        pinned.pinned = true
        let memory = MemoryFile(5, [huge, big, small, pinned], [])

        let text = MemoryBridge.factsThatFit(memory)

        #expect(text.utf16.count <= MemoryBridge.hookLimit)
        #expect(!text.contains("[F1]"))
        #expect(text.contains("[F2]"))
        #expect(text.contains("[F3]"))
        #expect(text.contains("[F4]"))
    }

    @Test
    func aKeyInTheMessageRefreshesTheFactAndTheFileIsWritten() {
        let memory = MemoryFile(3, [Self.fact(1, "Makes Aiko", "Aiko"), Self.fact(2, "Learns Swift", "Swift")], [])

        let step = MemoryBridge.onPrompt(Self.promptInput("поправь отступ в Aiko"), memory.toJson(), Self.today)

        #expect(step.memoryJson != nil)
        let written = MemoryFile.fromJson(step.memoryJson).memory
        #expect(written.find("F1")?.seen == Self.today)
        #expect(written.find("F2")?.seen == Self.yesterday)
        #expect(step.output == nil)
    }

    @Test
    func aMessageThatTouchesNothingWritesNothing() {
        let memory = MemoryFile(2, [Self.fact(1, "Makes Aiko", "Aiko")], [])

        let step = MemoryBridge.onPrompt(Self.promptInput("поправь отступ"), memory.toJson(), Self.today)

        #expect(step.memoryJson == nil)
        #expect(step.output == nil)
    }

    @Test
    func firstPersonBringsTheReminderAsPromptContext() {
        let step = MemoryBridge.onPrompt(
            Self.promptInput("мне нравится, когда кнопки без заливки"), nil, Self.today)

        #expect(Self.hook(step.output)?["hookEventName"]?.stringValue == "UserPromptSubmit")
        #expect(Self.context(step.output) == MemoryNudge.text)
        #expect(step.memoryJson == nil)
    }

    @Test
    func aBrokenFileIsNeverWrittenOverByAMessage() {
        let step = MemoryBridge.onPrompt(Self.promptInput("я работаю над Aiko"), "not json", Self.today)

        #expect(step.memoryJson == nil)
        #expect(step.output != nil)
    }

    @Test(arguments: ["", "not json", #"{"hook_event_name":"UserPromptSubmit"}"#])
    func hookInputWithoutAPromptDoesNothing(input: String) {
        let step = MemoryBridge.onPrompt(input, nil, Self.today)

        #expect(step.output == nil)
        #expect(step.memoryJson == nil)
    }

    @Test
    func aServerCallThatSavesGivesTheFileToWrite() {
        let step = MemoryBridge.onServerLine(Self.rememberSwift, nil, Self.today, "0.4.0")

        #expect(step.output?.contains("Saved F1.") == true)
        #expect(step.memoryJson != nil)
        #expect(MemoryFile.fromJson(step.memoryJson).memory.find("F1")?.text == "Learns Swift")
        #expect(!step.badFile)
    }

    @Test
    func aServerCallThatChangesNothingWritesNothing() {
        let step = MemoryBridge.onServerLine(
            #"{"jsonrpc":"2.0","id":1,"method":"tools/list"}"#, nil, Self.today, "0.4.0")

        #expect(step.output != nil)
        #expect(step.memoryJson == nil)
    }

    @Test
    func aSaveOverABrokenFileAsksToKeepTheOldOneAside() {
        let step = MemoryBridge.onServerLine(Self.rememberSwift, "not json", Self.today, "0.4.0")

        #expect(step.memoryJson != nil)
        #expect(step.badFile)
    }

    @Test
    func aFailedWriteIsAnsweredAsAFailedToolCall() {
        let reply = MemoryBridge.writeFailedReply(
            #"{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"forget"}}"#)

        let message = JsonNode.parse(reply)
        #expect(message?["id"]?.int64Value == 4)
        #expect(message?["result"]?["isError"]?.boolValue == true)
        #expect(message?["result"]?["content"]?.arrayValue?.first?["text"]?.stringValue == MemoryBridge.writeFailed)
    }

    @Test(arguments: ["not json", #"{"jsonrpc":"2.0","method":"notifications/initialized"}"#])
    func aFailedWriteForSomethingThatIsNotARequestHasNoAnswer(line: String) {
        #expect(MemoryBridge.writeFailedReply(line) == nil)
    }
}
