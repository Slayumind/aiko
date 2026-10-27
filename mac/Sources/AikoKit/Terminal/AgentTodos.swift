import Foundation

public enum TodoState: String, Sendable, Equatable {
    case pending
    case now
    case done
}

/// One line of the to-do list of the agent. Doing is how the line reads while it is being done
/// ("Running the tests"); it is empty when Claude did not give it.
public struct TodoItem: Sendable, Equatable {
    public var id: String
    public var text: String
    public var doing: String
    public var state: TodoState

    public init(id: String, text: String, doing: String, state: TodoState) {
        self.id = id
        self.text = text
        self.doing = doing
        self.state = state
    }
}

/// The to-do list of the agent, as its tools change it (D-344).
///
/// Claude Code gives these tools to newer models only when the environment asks for them
/// (RESEARCH). TaskCreate adds a line, and its number comes back only in the result: until then
/// the line is known by the id of the tool call. TaskUpdate changes a line by its number.
/// TodoWrite is the older tool: it sends the whole list each time.
public enum AgentTodos {
    private static let unnumbered = "use:"

    /// The tools of the list. Their steps are not lines of the feed: the panel shows them.
    public static func owns(_ toolName: String) -> Bool {
        ["TaskCreate", "TaskUpdate", "TaskList", "TaskGet", "TodoWrite"].contains(toolName)
    }

    public static func used(_ list: [TodoItem], toolName: String, toolUseId: String, input: JsonObject) -> [TodoItem] {
        switch toolName {
        case "TaskCreate":
            let subject = text(input, "subject")
            guard !subject.isEmpty else { return list }
            return list + [TodoItem(id: unnumbered + toolUseId, text: subject, doing: text(input, "activeForm"), state: .pending)]
        case "TaskUpdate":
            let id = text(input, "taskId")
            return id.isEmpty ? list : updated(list, id, input)
        case "TodoWrite":
            return written(input)
        default:
            return list
        }
    }

    /// A created line takes the number its result names. A line whose creation failed goes.
    public static func returned(_ list: [TodoItem], _ result: ToolResult) -> [TodoItem] {
        let id = unnumbered + result.toolUseId
        guard list.contains(where: { $0.id == id }) else { return list }
        if result.isError {
            return list.filter { $0.id != id }
        }
        guard let number = number(in: result.text) else { return list }
        return list.map { item in
            var item = item
            if item.id == id { item.id = number }
            return item
        }
    }

    public static func doneCount(_ list: [TodoItem]) -> Int {
        list.filter { $0.state == .done }.count
    }

    public static func allDone(_ list: [TodoItem]) -> Bool {
        !list.isEmpty && list.allSatisfy { $0.state == .done }
    }

    private static func updated(_ list: [TodoItem], _ id: String, _ input: JsonObject) -> [TodoItem] {
        let status = text(input, "status")
        if status == "deleted" {
            return list.filter { $0.id != id }
        }

        return list.map { item in
            guard item.id == id else { return item }
            var item = item
            let subject = text(input, "subject")
            let doing = text(input, "activeForm")
            if !subject.isEmpty { item.text = subject }
            if !doing.isEmpty { item.doing = doing }
            item.state = stateOf(status) ?? item.state
            return item
        }
    }

    private static func written(_ input: JsonObject) -> [TodoItem] {
        guard let todos = input["todos"]?.arrayValue else { return [] }
        let lines = todos.compactMap(\.objectValue).filter { !text($0, "content").isEmpty }
        return lines.enumerated().map { index, todo in
            TodoItem(
                id: String(index + 1),
                text: text(todo, "content"),
                doing: text(todo, "activeForm"),
                state: stateOf(text(todo, "status")) ?? .pending)
        }
    }

    private static func stateOf(_ status: String) -> TodoState? {
        switch status {
        case "pending": return .pending
        case "in_progress": return .now
        case "completed": return .done
        default: return nil
        }
    }

    private static func text(_ node: JsonObject, _ key: String) -> String {
        node[key]?.stringValue ?? ""
    }

    /// "Task #12 created successfully: Write the tests".
    private static func number(in text: String) -> String? {
        // The first "#" with digits after it, as the regex #(\d+) of the Windows core finds it.
        var rest = Substring(text)
        while let hash = rest.firstIndex(of: "#") {
            rest = rest[rest.index(after: hash)...]
            let digits = rest.prefix(while: { $0.isASCII && $0.isNumber })
            if !digits.isEmpty {
                return String(digits)
            }
        }
        return nil
    }
}
