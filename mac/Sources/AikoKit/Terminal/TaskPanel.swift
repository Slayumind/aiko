import Foundation

/// A subagent at work: the task it was given and what it says it is doing now.
public struct AgentAtWork: Sendable, Equatable {
    public let task: String
    public let doing: String

    public init(task: String, doing: String) {
        self.task = task
        self.doing = doing
    }
}

/// What the panel "Tasks" above the field shows (D-342): the to-do list of the agent and the
/// subagents at work. With neither, there is no panel.
public struct TaskPanel: Sendable, Equatable {
    public static let empty = TaskPanel(todos: [], agents: [])

    public let todos: [TodoItem]
    public let agents: [AgentAtWork]

    public init(todos: [TodoItem], agents: [AgentAtWork]) {
        self.todos = todos
        self.agents = agents
    }

    public var isEmpty: Bool { todos.isEmpty && agents.isEmpty }

    public var done: Int { AgentTodos.doneCount(todos) }

    /// A subagent is known by the step that started it; a step that left the feed is skipped.
    public static func of(_ conversation: Conversation) -> TaskPanel {
        let steps = conversation.items.compactMap { item -> FeedStep? in
            if case .step(let step) = item.content { return step }
            return nil
        }
        let agents = conversation.agents.compactMap { id -> AgentAtWork? in
            guard let step = steps.first(where: { $0.toolUseId == id }) else { return nil }
            return AgentAtWork(task: step.line.subject.isEmpty ? step.line.label : step.line.subject, doing: step.progress)
        }
        return conversation.todos.isEmpty && agents.isEmpty ? empty : TaskPanel(todos: conversation.todos, agents: agents)
    }
}
