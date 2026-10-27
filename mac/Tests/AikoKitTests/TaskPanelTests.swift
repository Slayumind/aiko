import Testing

@testable import AikoKit

/// The twin of TaskPanelTests.cs.
struct TaskPanelTests {
    private static let started = #"{"type":"system","subtype":"init","session_id":"s-1","model":"claude-opus-5-5"}"#

    private static func played(_ lines: String...) -> Conversation {
        var state = Conversation.empty().send("go", images: []).state
        for line in lines {
            state = state.on(StreamEvent.fromLine(line)).state
        }
        return state
    }

    @Test
    func aConversationWithoutAListOrASubagentHasNoPanel() {
        #expect(TaskPanel.of(Self.played(Self.started)).isEmpty)
    }

    @Test
    func aSubagentAtWorkShowsItsTaskAndWhatItDoesNow() {
        let state = Self.played(
            Self.started,
            #"{"type":"assistant","message":{"content":[{"type":"tool_use","id":"toolu_1","name":"Agent","input":{"description":"Check the Swift twin","prompt":"p"}}]},"parent_tool_use_id":null}"#,
            #"{"type":"system","subtype":"task_started","tool_use_id":"toolu_1"}"#,
            #"{"type":"system","subtype":"task_progress","tool_use_id":"toolu_1","description":"Reading TalkInput.swift"}"#)

        let panel = TaskPanel.of(state)

        #expect(panel.agents == [AgentAtWork(task: "Check the Swift twin", doing: "Reading TalkInput.swift")])
        #expect(panel.todos.isEmpty)
    }

    @Test
    func aSubagentThatIsOverLeavesThePanel() {
        let state = Self.played(
            Self.started,
            #"{"type":"assistant","message":{"content":[{"type":"tool_use","id":"toolu_1","name":"Agent","input":{"description":"Check","prompt":"p"}}]},"parent_tool_use_id":null}"#,
            #"{"type":"system","subtype":"task_started","tool_use_id":"toolu_1"}"#,
            #"{"type":"system","subtype":"task_notification","tool_use_id":"toolu_1","status":"completed"}"#)

        #expect(TaskPanel.of(state).isEmpty)
    }

    @Test
    func thePanelCountsTheLinesThatAreDone() {
        let state = Self.played(
            Self.started,
            #"{"type":"assistant","message":{"content":[{"type":"tool_use","id":"toolu_1","name":"TodoWrite","input":{"todos":[{"content":"a","status":"completed"},{"content":"b","status":"in_progress"},{"content":"c","status":"pending"}]}}]},"parent_tool_use_id":null}"#)

        let panel = TaskPanel.of(state)

        #expect(panel.done == 1)
        #expect(panel.todos.count == 3)
    }
}
