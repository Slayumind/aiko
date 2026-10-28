import Testing

@testable import AikoKit

/// The twin of TaskPanelTests.cs: the panel Tasks, from spec/cases/task-panel.
struct TaskPanelTests {
    @Test
    func everyPanelShowsTheSameTasks() {
        for row in SpecCases.json("task-panel")["panels"]?.arrayValue ?? [] {
            let name = row["name"]?.stringValue ?? ""
            var state = Conversation.empty().send("go", images: []).state
            for line in row["lines"]?.arrayValue ?? [] {
                state = state.on(StreamEvent.fromLine(line.toJsonString())).state
            }

            let panel = TaskPanel.of(state)
            #expect(panel.isEmpty == (row["empty"]?.boolValue ?? false), "\(name): empty")
            #expect(panel.done == Int(row["done"]?.int64Value ?? -1), "\(name): done")
            let todos = (row["todos"]?.arrayValue ?? []).map { todo in
                [todo["text"]?.stringValue ?? "", todo["doing"]?.stringValue ?? "", todo["state"]?.stringValue ?? ""]
            }
            #expect(panel.todos.map { [$0.text, $0.doing, $0.state.rawValue] } == todos, "\(name): todos")
            let agents = (row["agents"]?.arrayValue ?? []).map { agent in
                AgentAtWork(task: agent["task"]?.stringValue ?? "", doing: agent["doing"]?.stringValue ?? "")
            }
            #expect(panel.agents == agents, "\(name): agents")
        }
    }
}
