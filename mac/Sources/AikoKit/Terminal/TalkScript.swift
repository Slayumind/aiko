import Foundation

/// One thing the person does in a played conversation.
public struct TalkStep: Sendable, Equatable {
    public let what: String
    public let text: String
    public let requestId: String
    public let choice: CardChoice
    public let mode: PermissionMode
}

/// A conversation to play without Claude Code: what a scripted Claude Code answers and what the
/// person does, in order. The cases of the runner and the door --try-conversation read the same
/// shape:
///
///     { "script": [{ "after": "user", "lines": [{...}], "exit": 0 }],
///       "steps":  [{ "do": "send", "text": "..." }] }
///
/// The twin of TalkScript.cs.
public struct TalkScript: Sendable, Equatable {
    public let replies: [ScriptedReply]
    public let steps: [TalkStep]

    /// Nil when the text is not such a script.
    public static func parse(_ json: String?) -> TalkScript? {
        guard let root = JsonNode.parse(json), root.isObject else {
            return nil
        }

        return TalkScript(
            replies: items(root["script"]).map(replyIn),
            steps: items(root["steps"]).map(stepIn))
    }

    /// Does every step in order. A step the runner cannot take changes nothing, like in the window.
    public func play(_ runner: ConversationRunner) {
        for step in steps {
            switch step.what {
            case "send": runner.send(step.text, images: [])
            case "choose": runner.choose(step.requestId, step.choice)
            case "interrupt": runner.interrupt()
            case "setMode": runner.setMode(step.mode)
            case "markRead": runner.markRead()
            case "close": runner.close()
            default: break
            }
        }
    }

    private static func replyIn(_ reply: JsonNode) -> ScriptedReply {
        ScriptedReply(
            after: word(reply, "after"),
            lines: items(reply["lines"]).map { $0.toJsonString(escaping: .relaxed) },
            exit: reply["exit"]?.int64Value.map { Int($0) },
            fails: reply["fails"]?.boolValue == true)
    }

    private static func stepIn(_ step: JsonNode) -> TalkStep {
        TalkStep(
            what: word(step, "do"),
            text: word(step, "text"),
            requestId: word(step, "requestId"),
            choice: choiceIn(step),
            mode: PermissionModes.fromStream(step["mode"]?.stringValue) ?? .manual)
    }

    private static func choiceIn(_ step: JsonNode) -> CardChoice {
        switch step["choice"]?.stringValue {
        case "second": return .second
        case "deny": return .deny(word(step, "message"))
        case "answers": return .answers(items(step["picked"]).map { items($0).compactMap(\.stringValue) })
        default: return .allow
        }
    }

    private static func word(_ node: JsonNode, _ key: String) -> String {
        node[key]?.stringValue ?? ""
    }

    private static func items(_ node: JsonNode?) -> [JsonNode] {
        node?.arrayValue ?? []
    }
}
