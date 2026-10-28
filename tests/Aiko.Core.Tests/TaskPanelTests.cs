using Aiko.Core.Terminal;

namespace Aiko.Core.Tests;

/// The panel Tasks, read from spec/cases/task-panel, the same table the Swift tests read.
public class TaskPanelTests
{
    [Fact]
    public void Every_panel_shows_the_same_tasks()
    {
        foreach (var row in SpecCases.Json("task-panel").GetProperty("panels").EnumerateArray())
        {
            var name = row.GetProperty("name").GetString();
            var state = Conversation.Empty().Send("go", []).State;
            foreach (var line in row.GetProperty("lines").EnumerateArray())
            {
                state = state.On(StreamEvent.FromLine(line.GetRawText())).State;
            }

            var panel = TaskPanel.Of(state);
            Assert.True(row.GetProperty("empty").GetBoolean() == panel.IsEmpty, $"{name}: empty");
            Assert.True(row.GetProperty("done").GetInt32() == panel.Done, $"{name}: done {panel.Done}");
            Assert.True(
                row.GetProperty("todos").EnumerateArray()
                    .Select(todo => (todo.GetProperty("text").GetString(), todo.GetProperty("doing").GetString(), todo.GetProperty("state").GetString()))
                    .SequenceEqual(panel.Todos.Select(todo => ((string?)todo.Text, (string?)todo.Doing, (string?)State(todo.State)))),
                $"{name}: todos");
            Assert.True(
                row.GetProperty("agents").EnumerateArray()
                    .Select(agent => new AgentAtWork(agent.GetProperty("task").GetString()!, agent.GetProperty("doing").GetString()!))
                    .SequenceEqual(panel.Agents),
                $"{name}: agents");
        }
    }

    private static string State(TodoState state) => state switch
    {
        TodoState.Done => "done",
        TodoState.Now => "now",
        _ => "pending",
    };
}
