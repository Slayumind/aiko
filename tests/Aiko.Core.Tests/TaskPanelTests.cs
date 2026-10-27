using Aiko.Core.Terminal;

namespace Aiko.Core.Tests;

public class TaskPanelTests
{
    private const string Started = """{"type":"system","subtype":"init","session_id":"s-1","model":"claude-opus-5-5"}""";

    private static Conversation Played(params string[] lines)
    {
        var state = Conversation.Empty().Send("go", []).State;
        foreach (var line in lines)
        {
            state = state.On(StreamEvent.FromLine(line)).State;
        }
        return state;
    }

    [Fact]
    public void A_conversation_without_a_list_or_a_subagent_has_no_panel()
    {
        Assert.True(TaskPanel.Of(Played(Started)).IsEmpty);
    }

    [Fact]
    public void A_subagent_at_work_shows_its_task_and_what_it_does_now()
    {
        var state = Played(
            Started,
            """{"type":"assistant","message":{"content":[{"type":"tool_use","id":"toolu_1","name":"Agent","input":{"description":"Check the Swift twin","prompt":"p"}}]},"parent_tool_use_id":null}""",
            """{"type":"system","subtype":"task_started","tool_use_id":"toolu_1"}""",
            """{"type":"system","subtype":"task_progress","tool_use_id":"toolu_1","description":"Reading TalkInput.swift"}""");

        var panel = TaskPanel.Of(state);

        Assert.Equal([new AgentAtWork("Check the Swift twin", "Reading TalkInput.swift")], panel.Agents);
        Assert.Empty(panel.Todos);
    }

    [Fact]
    public void A_subagent_that_is_over_leaves_the_panel()
    {
        var state = Played(
            Started,
            """{"type":"assistant","message":{"content":[{"type":"tool_use","id":"toolu_1","name":"Agent","input":{"description":"Check","prompt":"p"}}]},"parent_tool_use_id":null}""",
            """{"type":"system","subtype":"task_started","tool_use_id":"toolu_1"}""",
            """{"type":"system","subtype":"task_notification","tool_use_id":"toolu_1","status":"completed"}""");

        Assert.True(TaskPanel.Of(state).IsEmpty);
    }

    [Fact]
    public void The_panel_counts_the_lines_that_are_done()
    {
        var state = Played(
            Started,
            """{"type":"assistant","message":{"content":[{"type":"tool_use","id":"toolu_1","name":"TodoWrite","input":{"todos":[{"content":"a","status":"completed"},{"content":"b","status":"in_progress"},{"content":"c","status":"pending"}]}}]},"parent_tool_use_id":null}""");

        var panel = TaskPanel.Of(state);

        Assert.Equal(1, panel.Done);
        Assert.Equal(3, panel.Todos.Count);
    }
}
