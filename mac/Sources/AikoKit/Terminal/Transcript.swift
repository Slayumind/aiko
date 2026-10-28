import Foundation

/// The file Claude Code keeps of a conversation, read into the feed of the terminal (D-303).
///
/// Aiko keeps no copy of a conversation. When the person goes back into a session, its feed is
/// read from this file into memory. The lines "user" and "assistant" carry a message in the same
/// shape as the stream, so they go through the same Conversation and give the same items.
///
/// The format is not documented, so every line is taken on its own terms: what is not understood
/// is skipped. Only the message of a line is read. The other lines hold the folder, the text of
/// the persona and the last prompt, and none of that is looked at.
///
/// The twin of Transcript.cs.
public enum Transcript {
    /// What Claude Code writes in the place of a message when the person stops a turn.
    private static let interrupted = "[Request interrupted by user"

    /// The feed of a conversation that is over: nothing in it runs or waits.
    public static func read(_ lines: some Sequence<String>, mode: PermissionMode) -> Conversation {
        var state = Conversation.empty(mode: mode)
        for line in lines {
            state = taken(state, line)
        }

        // A step without a result was cut off. A failure that was said back then is not the
        // state of the session now.
        for index in state.items.indices {
            if case .step(var step) = state.items[index].content, step.state == .running {
                step.state = .failed
                state.items[index].content = .step(step)
            }
            if case .shell(let command, let shellState, let output, let exitCode) = state.items[index].content, shellState != .done {
                state.items[index].content = .shell(command: command, state: .done, output: output, exitCode: exitCode)
            }
        }
        state.inTurn = false
        state.tasks = 0
        state.agents = []
        state.unread = false
        state.failure = .none
        state.answeredInTurn = false
        state.failedInTurn = false
        state.compacting = false
        return state
    }

    private static func taken(_ state: Conversation, _ line: String) -> Conversation {
        guard let root = JsonNode.parse(line), root.isObject, root["isSidechain"]?.boolValue != true else {
            return state
        }

        switch root["type"]?.stringValue {
        case "assistant" where !isEmptyReply(root):
            return state.on(StreamEvent.fromLine(line)).state
        case "user" where root["isMeta"]?.boolValue != true && root["isCompactSummary"]?.boolValue != true:
            return fromPerson(state, root, line)
        case "system" where root["subtype"]?.stringValue == "compact_boundary":
            return state.on(StreamEvent.fromLine(line)).state
        case "attachment":
            guard let said = queuedByPerson(root["attachment"]) else { return state }
            return fromPerson(state, said, said.toJsonString())
        default:
            return state
        }
    }

    /// A message the person sent while Claude worked. Claude Code writes it into the file as an
    /// attachment when it takes the message, not as a message of the person (2.1.312, RESEARCH).
    /// Notices of tasks and messages of other sessions come the same way and stay out of the feed.
    /// The words are given back as the message they were, so they read like any other.
    private static func queuedByPerson(_ attachment: JsonNode?) -> JsonNode? {
        guard let attachment, attachment["type"]?.stringValue == "queued_command",
              attachment["commandMode"]?.stringValue == "prompt",
              attachment["isMeta"]?.boolValue != true else { return nil }
        let from = attachment["origin"]?.objectValue?["kind"]?.stringValue ?? attachment["origin"]?.stringValue ?? ""
        guard from.isEmpty || from == "human", let prompt = attachment["prompt"],
              prompt.stringValue != nil || prompt.arrayValue != nil else { return nil }
        return .object(JsonObject([
            ("type", .string("user")),
            ("message", .object(JsonObject([("role", .string("user")), ("content", prompt)]))),
        ]))
    }

    /// A "user" line is either what tools gave back, or what the person wrote.
    private static func fromPerson(_ state: Conversation, _ root: JsonNode, _ line: String) -> Conversation {
        let event = StreamEvent.fromLine(line)
        guard case .user(_, let said, let results, _, _) = event else {
            return state
        }

        if !results.isEmpty {
            return state.on(event).state
        }

        let images = pictures(root)
        if said.hasPrefix(interrupted) {
            return added(state, .notice(kind: .interrupted, text: ""))
        }

        // A command of "!". Claude Code writes its output as the next message; Aiko, in the same one.
        if let shell = ShellCommand.read(said) {
            return added(state, .shell(command: shell.command, state: .done, output: shell.output ?? "", exitCode: shell.exitCode))
        }
        if let printed = ShellCommand.output(said), let last = state.items.last,
           case .shell(let command, let shellState, _, let exitCode) = last.content {
            var next = state
            next.items[next.items.count - 1].content = .shell(command: command, state: shellState, output: printed, exitCode: exitCode)
            return next
        }

        // What a command printed without the model. The stream shows it as a notice, so does this.
        let output = tagged(said, "local-command-stdout")
        if !output.isEmpty && said.hasPrefix("<") {
            return added(state, .notice(kind: .output, text: output))
        }

        let text = written(said)
        if text.isEmpty && images.isEmpty {
            return state
        }

        return added(state, .ownerMessage(text: text, queued: false, attachments: images))
    }

    /// The pictures the person sent, as Claude Code wrote them into the file.
    private static func pictures(_ root: JsonNode) -> [AttachedImage] {
        (root["message"]?["content"]?.arrayValue ?? []).compactMap { block in
            guard block["type"]?.stringValue == "image",
                  let source = block["source"]?.objectValue,
                  source["type"]?.stringValue == "base64",
                  let data = source["data"]?.stringValue, !data.isEmpty
            else {
                return nil
            }
            return AttachedImage(mediaType: source["media_type"]?.stringValue ?? "", base64: data)
        }
    }

    private static func added(_ state: Conversation, _ content: FeedItem.Content) -> Conversation {
        var next = state
        next.items.append(FeedItem(id: next.nextId, content: content))
        next.nextId += 1
        return next
    }

    /// The words of the person. Claude Code wraps a command in tags and writes notes of its own
    /// as messages; a command is shown the way it was typed, a note is not shown.
    private static func written(_ text: String) -> String {
        if !text.hasPrefix("<") {
            return text
        }

        let command = tagged(text, "command-name")
        if command.isEmpty {
            return ""
        }

        let arguments = tagged(text, "command-args")
        return arguments.isEmpty ? command : "\(command) \(arguments)"
    }

    private static func tagged(_ text: String, _ tag: String) -> String {
        guard let start = text.range(of: "<\(tag)>", options: .literal),
              let end = text.range(of: "</\(tag)>", options: .literal),
              start.upperBound <= end.lowerBound
        else {
            return ""
        }

        var units = Array(text[start.upperBound..<end.lowerBound].utf16)
        func isEdge(_ unit: UInt16) -> Bool { unit == 32 || unit == 9 || unit == 13 || unit == 10 }
        while let first = units.first, isEdge(first) {
            units.removeFirst()
        }
        while let last = units.last, isEdge(last) {
            units.removeLast()
        }
        return String(decoding: units, as: UTF16.self)
    }

    /// Claude Code fills a turn without an answer with a message of its own, from no model.
    private static func isEmptyReply(_ root: JsonNode) -> Bool {
        root["message"]?["model"]?.stringValue == "<synthetic>" && (root["error"]?.stringValue ?? "").isEmpty
    }
}
