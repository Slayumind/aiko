import Foundation

/// A picture the person attached to a message.
public struct AttachedImage: Sendable, Equatable {
    public let mediaType: String
    public let base64: String

    public init(mediaType: String, base64: String) {
        self.mediaType = mediaType
        self.base64 = base64
    }
}

/// The lines the terminal writes to Claude Code: one JSON object on one line, without the line
/// break. The shapes come from the channel spike (D-297).
///
/// Letters of any language and the plus sign of base64 stay as they are: the strict writer would
/// write six characters for each of them.
public enum HostLine {
    /// Pictures go before the text, the way the API reads them best.
    public static func userMessage(_ text: String, images: [AttachedImage]) -> String {
        var content: [JsonNode] = images.map { image in
            object([
                ("type", .string("image")),
                ("source", object([
                    ("type", .string("base64")),
                    ("media_type", .string(image.mediaType)),
                    ("data", .string(image.base64)),
                ])),
            ])
        }
        if !text.isEmpty {
            content.append(object([("type", .string("text")), ("text", .string(text))]))
        }

        return write([
            ("type", .string("user")),
            ("message", object([("role", .string("user")), ("content", .array(content))])),
        ])
    }

    /// Claude Code wants the input of the tool back with the yes.
    public static func allow(_ requestId: String, input: JsonObject) -> String {
        respond(requestId, [("behavior", .string("allow")), ("updatedInput", .object(input))])
    }

    /// Yes, and no more questions about edits in this session: the mode change travels with the
    /// answer (D-305).
    public static func allowEdits(_ requestId: String, input: JsonObject) -> String {
        let mode = object([
            ("type", .string("setMode")),
            ("mode", .string(PermissionModes.name(.acceptEdits))),
            ("destination", .string("session")),
        ])
        return respond(requestId, [
            ("behavior", .string("allow")), ("updatedInput", .object(input)), ("updatedPermissions", .array([mode])),
        ])
    }

    /// Yes, and the rule Claude Code offered goes back as it came. Claude Code writes it into the
    /// settings of the project itself (D-306).
    public static func allowAlways(_ requestId: String, input: JsonObject, rule: JsonObject) -> String {
        respond(requestId, [
            ("behavior", .string("allow")), ("updatedInput", .object(input)),
            ("updatedPermissions", .array([.object(rule)])),
        ])
    }

    /// The message reaches Claude as the error of the tool.
    public static func deny(_ requestId: String, message: String) -> String {
        respond(requestId, [("behavior", .string("deny")), ("message", .string(message))])
    }

    /// The answer to a question with options: the input of the question with the answers added,
    /// each one under the text of its question.
    public static func answer(
        _ requestId: String, input: JsonObject, answers: [(question: String, answer: String)]
    ) -> String {
        var updated = input.members.filter { $0.key != "answers" }
        updated.append(JsonObject.Member(
            key: "answers",
            value: object(answers.map { ($0.question, .string($0.answer)) })))

        return respond(requestId, [("behavior", .string("allow")), ("updatedInput", .object(JsonObject(updated)))])
    }

    public static func interrupt(_ requestId: String) -> String {
        ask(requestId, [("subtype", .string("interrupt"))])
    }

    public static func setMode(_ requestId: String, _ mode: PermissionMode) -> String {
        ask(requestId, [
            ("subtype", .string("set_permission_mode")),
            ("mode", .string(PermissionModes.requestValue(mode))),
        ])
    }

    /// Claude Code takes the new model between turns and names it in the next init (spike of
    /// 2026-10-15, RESEARCH).
    public static func setModel(_ requestId: String, _ model: String) -> String {
        ask(requestId, [
            ("subtype", .string("set_model")),
            ("model", .string(model)),
        ])
    }

    private static func respond(_ requestId: String, _ decision: [(String, JsonNode)]) -> String {
        write([
            ("type", .string("control_response")),
            ("response", object([
                ("subtype", .string("success")),
                ("request_id", .string(requestId)),
                ("response", object(decision)),
            ])),
        ])
    }

    private static func ask(_ requestId: String, _ request: [(String, JsonNode)]) -> String {
        write([
            ("type", .string("control_request")),
            ("request_id", .string(requestId)),
            ("request", object(request)),
        ])
    }

    private static func object(_ pairs: [(String, JsonNode)]) -> JsonNode {
        .object(JsonObject(pairs))
    }

    private static func write(_ pairs: [(String, JsonNode)]) -> String {
        object(pairs).toJsonString(escaping: .relaxed)
    }
}
