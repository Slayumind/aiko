import Foundation

/// One part of an answer of Claude.
public enum AssistantBlock: Sendable, Equatable {
    case text(String)

    case toolUse(id: String, name: String, input: JsonObject)

    /// Claude is thinking. The thoughts themselves are not kept.
    case thinking
}

/// A picture as it travels in the stream: its type and its bytes in base64.
public struct StreamImage: Sendable, Equatable {
    public let mediaType: String
    public let data: String

    public init(mediaType: String, data: String) {
        self.mediaType = mediaType
        self.data = data
    }
}

/// What a tool gave back.
/// answers: what the person chose on a card of questions.
public struct ToolResult: Sendable, Equatable {
    public let toolUseId: String
    public let isError: Bool
    public let text: String
    public let images: [StreamImage]
    public var answers: [QuestionAnswer]

    public init(toolUseId: String, isError: Bool, text: String, images: [StreamImage], answers: [QuestionAnswer] = []) {
        self.toolUseId = toolUseId
        self.isError = isError
        self.text = text
        self.images = images
        self.answers = answers
    }
}

/// Claude Code asks whether a tool may run. A question with options and a plan to approve arrive
/// the same way, with needsAnswer set.
public struct ToolRequest: Sendable, Equatable {
    public let requestId: String
    public let toolName: String
    public let toolUseId: String
    public let input: JsonObject
    public let description: String
    public let suggestsAcceptEdits: Bool
    public let needsAnswer: Bool

    /// Set when a subagent asks. Its tool call is not in the feed of the conversation.
    public let agentId: String?

    /// The rule Claude Code offers to add so that it never asks about this again. It goes back
    /// as it came (D-306).
    public let rule: JsonObject?

    /// The words of that rule, to show on the button: "mkdir build *".
    public let ruleText: String

    public init(
        requestId: String,
        toolName: String,
        toolUseId: String,
        input: JsonObject,
        description: String,
        suggestsAcceptEdits: Bool,
        needsAnswer: Bool,
        agentId: String? = nil,
        rule: JsonObject? = nil,
        ruleText: String = ""
    ) {
        self.requestId = requestId
        self.toolName = toolName
        self.toolUseId = toolUseId
        self.input = input
        self.description = description
        self.suggestsAcceptEdits = suggestsAcceptEdits
        self.needsAnswer = needsAnswer
        self.agentId = agentId
        self.rule = rule
        self.ruleText = ruleText
    }
}

/// One line Claude Code wrote to its output, as far as the terminal needs it (D-297).
///
/// Claude Code runs as a child process and speaks JSON lines. Most of a line is of no use here and
/// some of it must not be kept: the start event carries paths, and the hook events carry the text
/// of the persona and of the memory. Those parts are dropped by this parser, so nothing after it
/// can show or log them.
public enum StreamEvent: Sendable, Equatable {
    /// Claude Code is ready for a turn. It comes before every turn, not once.
    case ready(sessionId: String, model: String, mode: PermissionMode?, version: String)

    /// A message of Claude, whole. The error is set when Claude Code answered in its place:
    /// "authentication_failed" means the environment is not signed in.
    case assistant(parentToolUseId: String?, blocks: [AssistantBlock], error: String?)

    /// What came back to Claude: results of tools, or text Claude Code wrote on its own.
    case user(parentToolUseId: String?, text: String, results: [ToolResult], isReplay: Bool, isSynthetic: Bool)

    /// A piece of an answer that is still being written.
    case textDelta(parentToolUseId: String?, text: String)

    case request(ToolRequest)

    /// The answer to a request the terminal sent: an interrupt or a change of mode.
    case reply(requestId: String, ok: Bool, mode: PermissionMode?)

    /// The activity is "compacting" or "requesting" while that goes on, nil when it is over.
    case status(activity: String?, mode: PermissionMode?)

    /// The conversation was made shorter. What is above this point Claude remembers as a summary.
    /// Before and after are its size in tokens, or 0 when Claude Code did not say.
    case compacted(before: Int, after: Int)

    case rateLimit([LimitWindow])

    /// How many subagents still run in the background. A turn can end while they work, and the
    /// session is not free until this is zero (D-305).
    case tasks(Int)

    /// What a subagent is doing now, in a few words of its own.
    case taskProgress(toolUseId: String, description: String)

    /// The turn is over. The text is the output of a command that ran without the model.
    case result(isError: Bool, subtype: String, terminalReason: String, text: String)

    /// A line the terminal has no use for.
    case ignored

    /// Not a JSON object.
    case unreadable

    /// Never throws: one broken line must not end a conversation.
    public static func fromLine(_ line: String?) -> StreamEvent {
        var text = (line ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
        while text.hasPrefix("\u{FEFF}") {
            text.removeFirst()
        }
        if text.isEmpty {
            return .ignored
        }

        guard let root = JsonNode.parse(text)?.objectValue else {
            return .unreadable
        }
        return read(root)
    }

    private static func read(_ root: JsonObject) -> StreamEvent {
        switch root["type"]?.stringValue {
        case "system": return readSystem(root)
        case "assistant": return readAssistant(root)
        case "user": return readUser(root)
        case "stream_event": return readDelta(root)
        case "control_request": return readRequest(root)
        case "control_response": return readReply(root)
        case "rate_limit_event": return readRateLimit(root)
        case "result":
            return .result(
                isError: root["is_error"]?.boolValue ?? false,
                subtype: root["subtype"]?.stringValue ?? "",
                terminalReason: root["terminal_reason"]?.stringValue ?? "",
                text: root["result"]?.stringValue ?? "")
        default: return .ignored
        }
    }

    private static func readSystem(_ root: JsonObject) -> StreamEvent {
        switch root["subtype"]?.stringValue {
        case "init":
            guard let session = root["session_id"]?.stringValue, !session.isEmpty else {
                return .ignored
            }
            return .ready(
                sessionId: session,
                model: root["model"]?.stringValue ?? "",
                mode: PermissionModes.fromStream(root["permissionMode"]?.stringValue),
                version: root["claude_code_version"]?.stringValue ?? "")
        case "status":
            return .status(
                activity: root["status"]?.stringValue,
                mode: PermissionModes.fromStream(root["permissionMode"]?.stringValue))
        case "compact_boundary":
            let size = root["compact_metadata"]?.objectValue
            return .compacted(before: tokens(size?["pre_tokens"]), after: tokens(size?["post_tokens"]))
        case "background_tasks_changed":
            return .tasks(items(root["tasks"]).count)
        case "task_progress":
            guard let task = root["tool_use_id"]?.stringValue, !task.isEmpty else {
                return .ignored
            }
            return .taskProgress(toolUseId: task, description: root["description"]?.stringValue ?? "")
        default:
            return .ignored
        }
    }

    private static func readAssistant(_ root: JsonObject) -> StreamEvent {
        var blocks: [AssistantBlock] = []
        for block in items(root["message"]?["content"]) {
            switch block["type"]?.stringValue {
            case "text":
                if let value = block["text"]?.stringValue, !value.isEmpty {
                    blocks.append(.text(value))
                }
            case "tool_use":
                if let id = block["id"]?.stringValue, !id.isEmpty {
                    blocks.append(.toolUse(
                        id: id,
                        name: block["name"]?.stringValue ?? "",
                        input: block["input"]?.objectValue ?? JsonObject()))
                }
            case "thinking":
                blocks.append(.thinking)
            default:
                break
            }
        }

        let error = root["error"]?.stringValue
        if blocks.isEmpty && error == nil {
            return .ignored
        }
        return .assistant(parentToolUseId: root["parent_tool_use_id"]?.stringValue, blocks: blocks, error: error)
    }

    private static func readUser(_ root: JsonObject) -> StreamEvent {
        let content = root["message"]?["content"]
        var texts: [String] = []
        var results: [ToolResult] = []

        if let plain = content?.stringValue {
            texts.append(plain)
        }

        for block in items(content) {
            switch block["type"]?.stringValue {
            case "text":
                if let value = block["text"]?.stringValue {
                    texts.append(value)
                }
            case "tool_result":
                if let id = block["tool_use_id"]?.stringValue, !id.isEmpty {
                    results.append(readResult(id, block))
                }
            default:
                break
            }
        }

        // Claude Code keeps what a tool returned beside the message, once for it. Only the answers
        // to questions are read from it; the rest holds paths and the contents of files.
        if results.count == 1 {
            let chosen = QuestionAnswers.from(
                root["tool_use_result"]?.objectValue ?? root["toolUseResult"]?.objectValue)
            if !chosen.isEmpty { results[0].answers = chosen }
        }

        return .user(
            parentToolUseId: root["parent_tool_use_id"]?.stringValue,
            text: texts.joined(separator: "\n"),
            results: results,
            isReplay: root["isReplay"]?.boolValue ?? false,
            isSynthetic: root["isSynthetic"]?.boolValue ?? false)
    }

    private static func readResult(_ id: String, _ block: JsonObject) -> ToolResult {
        let content = block["content"]
        var texts: [String] = []
        var images: [StreamImage] = []

        if let plain = content?.stringValue {
            texts.append(plain)
        }

        for part in items(content) {
            switch part["type"]?.stringValue {
            case "text":
                if let value = part["text"]?.stringValue {
                    texts.append(value)
                }
            case "image":
                if let source = part["source"]?.objectValue,
                   source["type"]?.stringValue == "base64",
                   let data = source["data"]?.stringValue, !data.isEmpty {
                    images.append(StreamImage(mediaType: source["media_type"]?.stringValue ?? "", data: data))
                }
            default:
                break
            }
        }

        return ToolResult(
            toolUseId: id,
            isError: block["is_error"]?.boolValue ?? false,
            text: texts.joined(separator: "\n"),
            images: images)
    }

    private static func readDelta(_ root: JsonObject) -> StreamEvent {
        let change = root["event"]?.objectValue
        let delta = change?["delta"]?.objectValue

        guard change?["type"]?.stringValue == "content_block_delta",
              delta?["type"]?.stringValue == "text_delta",
              let text = delta?["text"]?.stringValue, !text.isEmpty else {
            return .ignored
        }
        return .textDelta(parentToolUseId: root["parent_tool_use_id"]?.stringValue, text: text)
    }

    private static func readRequest(_ root: JsonObject) -> StreamEvent {
        guard let request = root["request"]?.objectValue,
              request["subtype"]?.stringValue == "can_use_tool",
              let id = root["request_id"]?.stringValue, !id.isEmpty,
              let tool = request["tool_name"]?.stringValue, !tool.isEmpty else {
            return .ignored
        }

        let suggestsAcceptEdits = items(request["permission_suggestions"]).contains {
            $0["type"]?.stringValue == "setMode" && $0["mode"]?.stringValue == "acceptEdits"
        }

        let rule = items(request["permission_suggestions"]).first {
            $0["type"]?.stringValue == "addRules"
                && $0["behavior"]?.stringValue == "allow"
                && !ruleWords(items($0["rules"]).first, tool: tool).isEmpty
        }

        return .request(ToolRequest(
            requestId: id,
            toolName: tool,
            toolUseId: request["tool_use_id"]?.stringValue ?? "",
            input: request["input"]?.objectValue ?? JsonObject(),
            description: request["description"]?.stringValue ?? "",
            suggestsAcceptEdits: suggestsAcceptEdits,
            needsAnswer: request["requires_user_interaction"]?.boolValue ?? false,
            agentId: request["agent_id"]?.stringValue,
            rule: rule,
            ruleText: rule.map { ruleWords(items($0["rules"]).first, tool: tool) } ?? ""))
    }

    /// The words of a rule that may be offered, or nothing. A rule with words of its own covers
    /// what they say. A rule without them covers every call of its tool, so it is taken only for
    /// the MCP tool that asks: its words are then the name of that tool (D-307).
    private static func ruleWords(_ rule: JsonObject?, tool: String) -> String {
        if let words = rule?["ruleContent"]?.stringValue, !words.isEmpty {
            return words
        }
        return tool.hasPrefix("mcp__") && rule?["toolName"]?.stringValue == tool ? tool : ""
    }

    private static func readReply(_ root: JsonObject) -> StreamEvent {
        guard let response = root["response"]?.objectValue,
              let id = response["request_id"]?.stringValue, !id.isEmpty else {
            return .ignored
        }
        return .reply(
            requestId: id,
            ok: response["subtype"]?.stringValue == "success",
            mode: PermissionModes.fromStream(response["response"]?["mode"]?.stringValue))
    }

    private static func readRateLimit(_ root: JsonObject) -> StreamEvent {
        let limits = root["rate_limit_info"]?["unifiedWindows"]?.objectValue
        var windows: [LimitWindow] = []
        addWindow(&windows, limits, "five_hour", .fiveHour)
        addWindow(&windows, limits, "seven_day", .sevenDay)
        return windows.isEmpty ? .ignored : .rateLimit(windows)
    }

    /// The stream gives a share from 0 to 1 where the status line gives a percentage.
    private static func addWindow(
        _ windows: inout [LimitWindow], _ limits: JsonObject?, _ name: String, _ kind: LimitKind
    ) {
        guard let window = limits?[name]?.objectValue,
              let share = window["utilization"]?.doubleValue,
              let resetsAt = window["resetsAt"]?.doubleValue else {
            return
        }
        windows.append(LimitWindow(
            kind: kind,
            percent: Percentage.fromDouble(share * 100),
            resetsAt: Date(timeIntervalSince1970: resetsAt.rounded(.down))))
    }

    /// The objects of an array. Anything else in it is skipped.
    private static func items(_ node: JsonNode?) -> [JsonObject] {
        (node?.arrayValue ?? []).compactMap(\.objectValue)
    }

    private static func tokens(_ node: JsonNode?) -> Int {
        guard let tokens = node?.doubleValue, tokens >= 0, tokens <= Double(Int32.max) else { return 0 }
        return Int(tokens)
    }
}
