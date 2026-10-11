import Foundation

/// The four jobs the bridge does for the memory.
public enum MemoryBridgeMode: Sendable, Equatable, CaseIterable {
    /// SessionStart hook: the rules of the memory.
    case rules

    /// SessionStart hook: the facts. A hook of its own, because each hook output has its own cut.
    case facts

    /// UserPromptSubmit hook: refresh facts by their keys, and remind the model when the person
    /// talks about themselves.
    case prompt

    /// The MCP server with the remember and forget tools. It runs as long as the session.
    case serve
}

/// What a memory job of the bridge decided: the text to print, if any, and the memory file to
/// write, if it changed. `badFile` says the file on disk could not be read as a memory, so the
/// bridge must keep it aside before it writes a new one over it.
public struct MemoryBridgeStep: Sendable, Equatable {
    public let output: String?
    public let memoryJson: String?
    public let badFile: Bool

    public init(output: String?, memoryJson: String?, badFile: Bool = false) {
        self.output = output
        self.memoryJson = memoryJson
        self.badFile = badFile
    }
}

/// Every decision the bridge makes for the memory. The bridge itself only reads stdin and the
/// file, calls one function here, and prints and writes what comes back.
public enum MemoryBridge {
    /// "Aiko.Bridge memory rules|facts|prompt|serve".
    public static let argument = "memory"

    /// Claude Code cuts a hook output longer than this and gives the model a short preview. It
    /// counts UTF-16 units, so the texts are measured that way here.
    public static let hookLimit = 10_000

    public static let writeFailed = "Aiko could not write the memory file. Nothing was saved."

    public static func modeFor(_ arguments: [String]) -> MemoryBridgeMode? {
        guard arguments.count >= 2, arguments[0] == argument else {
            return nil
        }

        return MemoryBridgeMode.allCases.first { argumentFor($0) == arguments[1] }
    }

    public static func argumentFor(_ mode: MemoryBridgeMode) -> String {
        switch mode {
        case .rules: return "rules"
        case .facts: return "facts"
        case .prompt: return "prompt"
        case .serve: return "serve"
        }
    }

    public static func rulesOutput(_ today: DateOnly) -> String {
        hookOutput("SessionStart", MemoryPrompt.rules(today))
    }

    /// A file nobody could read gives an empty list, not an error: the session must still start.
    public static func factsOutput(_ memoryJson: String?) -> String {
        hookOutput("SessionStart", factsThatFit(MemoryFile.fromJson(memoryJson).memory))
    }

    /// Facts saved through the tools always fit. A file edited by hand may not, and a block over
    /// the cut reaches the model with no facts at all.
    ///
    /// So a fact over the limit stays out of the block, pinned or not: the settings window marks
    /// it "too long" and says the model does not see it, and that must be true. If the block is
    /// still over the cut, for example with more facts than the quotas allow, the longest facts
    /// that are not pinned stay out until it fits. All of them stay in the file.
    public static func factsThatFit(_ memory: MemoryFile) -> String {
        var shown = memory
        shown.facts = memory.facts.filter { MemoryRules.fits(MemoryRules.lengthOf($0)) }
        var text = MemoryPrompt.facts(shown)
        while text.utf16.count > hookLimit, let fact = longestToLeaveOut(shown) {
            shown.facts.removeAll { $0.id == fact.id }
            text = MemoryPrompt.facts(shown)
        }

        return text
    }

    public static func onPrompt(_ hookInput: String, _ memoryJson: String?, _ today: DateOnly) -> MemoryBridgeStep {
        guard let prompt = promptIn(hookInput) else {
            return MemoryBridgeStep(output: nil, memoryJson: nil)
        }

        let output = MemoryNudge.applies(prompt) ? hookOutput("UserPromptSubmit", MemoryNudge.text) : nil
        let read = MemoryFile.fromJson(memoryJson)
        if read.unreadable {
            return MemoryBridgeStep(output: output, memoryJson: nil)
        }

        let touch = MemoryEdits.touchByPrompt(read.memory, prompt, today)
        return MemoryBridgeStep(output: output, memoryJson: touch.touched.isEmpty ? nil : touch.memory.toJson())
    }

    public static func onServerLine(
        _ line: String, _ memoryJson: String?, _ today: DateOnly, _ version: String
    ) -> MemoryBridgeStep {
        let read = MemoryFile.fromJson(memoryJson)
        let reply = MemoryServer.answer(line, read.memory, today, version)
        return reply.changed
            ? MemoryBridgeStep(output: reply.line, memoryJson: reply.memory.toJson(), badFile: read.unreadable)
            : MemoryBridgeStep(output: reply.line, memoryJson: nil)
    }

    /// The answer to a tool call whose change could not be written. The model must not read
    /// "Saved" for a fact that is not in the file.
    public static func writeFailedReply(_ line: String) -> String? {
        MemoryServer.toolFailure(line, writeFailed)
    }

    /// The longest first; of two as long, the one not seen for longer, then the smaller id.
    private static func longestToLeaveOut(_ memory: MemoryFile) -> MemoryFact? {
        memory.facts.filter { !$0.pinned }.min {
            (-MemoryRules.lengthOf($0), $0.seen, $0.number) < (-MemoryRules.lengthOf($1), $1.seen, $1.number)
        }
    }

    private static func promptIn(_ hookInput: String) -> String? {
        JsonNode.parse(hookInput)?.objectValue?["prompt"]?.stringValue
    }

    /// additionalContext goes to the model; the person does not see it.
    private static func hookOutput(_ hookEvent: String, _ context: String) -> String {
        let inner = JsonObject([
            ("hookEventName", .string(hookEvent)),
            ("additionalContext", .string(context)),
        ])
        return JsonNode.object(JsonObject([("hookSpecificOutput", .object(inner))]))
            .toJsonString(escaping: .relaxed)
    }
}
