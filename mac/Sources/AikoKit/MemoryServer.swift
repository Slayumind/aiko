import Foundation

/// What the memory server does with one line: the line to print back, or nil when the message
/// needs no answer, and the memory after it. `changed` is true when the file must be written.
public struct MemoryServerReply: Sendable, Equatable {
    public let line: String?
    public let memory: MemoryFile
    public let changed: Bool

    public init(line: String?, memory: MemoryFile, changed: Bool) {
        self.line = line
        self.memory = memory
        self.changed = changed
    }
}

/// The memory tools as an MCP server, one JSON-RPC message per line.
///
/// Nothing here reads stdin, a file or a clock. The bridge reads a line, reads the memory file,
/// calls `answer`, writes the file when it changed and prints the line. So every rule of the
/// protocol can be tested as "this line in, that line out".
public enum MemoryServer {
    public static let name = "aiko-memory"

    public static let rememberTool = "remember"

    public static let forgetTool = "forget"

    /// The protocol we answer with when the client names none.
    public static let defaultProtocol = "2025-06-18"

    private static let parseError = -32700

    private static let methodNotFound = -32601

    public static func answer(
        _ line: String, _ memory: MemoryFile, _ today: DateOnly, _ version: String
    ) -> MemoryServerReply {
        guard let request = JsonNode.parse(line)?.objectValue else {
            return unchanged(memory, error("null", parseError, "Parse error"))
        }

        // A message without an id is a notification: the protocol forbids an answer.
        guard let id = idIn(line, request) else {
            return unchanged(memory, nil)
        }

        let parameters = request["params"]?.objectValue
        switch request["method"]?.stringValue {
        case "initialize":
            return unchanged(memory, result(id, hello(parameters, version)))
        case "ping":
            return unchanged(memory, result(id, JsonObject()))
        case "tools/list":
            return unchanged(memory, result(id, tools()))
        case "tools/call":
            return call(id, parameters, memory, today)
        default:
            return unchanged(memory, error(id, methodNotFound, "Method not found"))
        }
    }

    /// A tool result that says the call failed, for the request in this line. Nil when the line
    /// is not a request, so there is nobody to answer.
    public static func toolFailure(_ line: String, _ text: String) -> String? {
        guard let request = JsonNode.parse(line)?.objectValue, let id = idIn(line, request) else {
            return nil
        }

        return result(id, toolText(text, isError: true))
    }

    /// The id as it was written: a number stays a number, a string stays a string, and both keep
    /// the exact text the client sent.
    private static func idIn(_ line: String, _ request: JsonObject) -> String? {
        guard request.contains("id") else {
            return nil
        }

        return JsonRawText.members(line)?.last { $0.key == "id" }?.value ?? "null"
    }

    private static func call(
        _ id: String, _ parameters: JsonObject?, _ memory: MemoryFile, _ today: DateOnly
    ) -> MemoryServerReply {
        let arguments = parameters?["arguments"]?.objectValue
        let tool = parameters?["name"]?.stringValue

        let edit: MemoryEdit
        switch tool {
        case rememberTool:
            edit = MemoryEdits.remember(memory, requestFrom(arguments), today)
        case forgetTool:
            edit = MemoryEdits.forget(memory, arguments?["id"]?.stringValue)
        default:
            return unchanged(memory, result(id, toolText("Unknown tool \(tool ?? "").", isError: true)))
        }

        return MemoryServerReply(
            line: result(id, toolText(edit.reply, isError: false)), memory: edit.memory, changed: edit.changed)
    }

    /// An argument of the wrong type is read as missing: the model gets an answer about the fact
    /// and not about JSON.
    private static func requestFrom(_ arguments: JsonObject?) -> MemoryRequest {
        MemoryRequest(
            id: arguments?["id"]?.stringValue,
            category: arguments?["category"]?.stringValue,
            text: arguments?["fact"]?.stringValue,
            meaning: arguments?["meaning"]?.stringValue,
            pin: arguments?["pin"]?.boolValue,
            keys: arguments?["keys"]?.arrayValue.map { $0.compactMap(\.stringValue) })
    }

    private static func hello(_ parameters: JsonObject?, _ version: String) -> JsonObject {
        JsonObject([
            ("protocolVersion", .string(parameters?["protocolVersion"]?.stringValue ?? defaultProtocol)),
            ("capabilities", .object(JsonObject([("tools", .object(JsonObject()))]))),
            ("serverInfo", .object(JsonObject([("name", .string(name)), ("version", .string(version))]))),
        ])
    }

    private static func tools() -> JsonObject {
        let remember = tool(
            rememberTool,
            "Save a new fact about the user to Aiko memory, or refresh an existing one by id.",
            required: "category",
            properties: [
                property("category", "string", categoryHelp, enumValues: MemoryRules.categories.map(MemoryRules.nameOf)),
                property("fact", "string", factHelp),
                property("meaning", "string", "Short note in English: what this changes in your answers."),
                property("id", "string", "Id of an existing fact (F12) to refresh or reword it."),
                property("pin", "boolean", "Pinned facts are never forgotten."),
                property("keys", "array", keysHelp, itemsAreStrings: true),
            ])
        let forget = tool(
            forgetTool,
            "Remove a fact from Aiko memory by id.",
            required: "id",
            properties: [property("id", "string", "Id of the fact to forget.")])

        return JsonObject([("tools", .array([remember, forget]))])
    }

    private static func tool(
        _ name: String, _ description: String, required: String, properties: [(String, JsonNode)]
    ) -> JsonNode {
        .object(JsonObject([
            ("name", .string(name)),
            ("description", .string(description)),
            ("inputSchema", .object(JsonObject([
                ("type", .string("object")),
                ("properties", .object(JsonObject(properties))),
                ("required", .array([.string(required)])),
            ]))),

            // Without this Claude Code defers the tool, and the model must search for it before
            // the first call. A memory that costs a search is a memory the model skips.
            ("_meta", .object(JsonObject([("anthropic/alwaysLoad", .bool(true))]))),
        ]))
    }

    private static func property(
        _ name: String, _ type: String, _ description: String,
        enumValues: [String]? = nil, itemsAreStrings: Bool = false
    ) -> (String, JsonNode) {
        var members: [(String, JsonNode)] = [("type", .string(type)), ("description", .string(description))]
        if let enumValues {
            members.append(("enum", .array(enumValues.map(JsonNode.string))))
        }

        if itemsAreStrings {
            members.append(("items", .object(JsonObject([("type", .string("string"))]))))
            members.append(("maxItems", .number(String(MemoryRules.maxKeys))))
        }

        return (name, .object(JsonObject(members)))
    }

    private static func toolText(_ text: String, isError: Bool) -> JsonObject {
        JsonObject([
            ("content", .array([.object(JsonObject([("type", .string("text")), ("text", .string(text))]))])),
            ("isError", .bool(isError)),
        ])
    }

    private static func result(_ id: String, _ body: JsonObject) -> String {
        message(id, "result", body)
    }

    private static func error(_ id: String, _ code: Int, _ text: String) -> String {
        message(id, "error", JsonObject([("code", .number(String(code))), ("message", .string(text))]))
    }

    /// One line per message, and Cyrillic stays readable in the replies the model reads. The id
    /// is put in as text, because a JsonNode would write it again its own way.
    private static func message(_ id: String, _ name: String, _ body: JsonObject) -> String {
        "{\"jsonrpc\":\"2.0\",\"id\":\(id),\"\(name)\":" + JsonNode.object(body).toJsonString(escaping: .relaxed) + "}"
    }

    private static func unchanged(_ memory: MemoryFile, _ line: String?) -> MemoryServerReply {
        MemoryServerReply(line: line, memory: memory, changed: false)
    }

    private static let categoryHelp =
        "you: who the user is, what they know and learn (\"learns Swift\"); "
        + "work: how they like to work with you (\"short answer first\"); "
        + "projects: what they work on, one line each (\"makes the Aiko tray app\"); "
        + "interests: taste and hobbies (\"loves the Souls series\")"

    private static let factHelp =
        "The fact in English, one short line. Quote the user's own words as they said them. "
        + "The fact, its meaning and keys together are at most 200 characters. Leave out when only refreshing."

    private static let keysHelp =
        "1-3 names or terms the user would say when this comes up again, in the user's own language "
        + "and spelling (\"Swift\", \"Ember\", \"C#\"). Not common words. A mention of a key refreshes "
        + "the fact by itself. Pinned facts and work habits take no keys."
}
