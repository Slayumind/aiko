namespace Aiko.Core.Terminal;

/// A subagent at work: the task it was given and what it says it is doing now.
public sealed record AgentAtWork(string Task, string Doing);

/// What the panel "Tasks" above the field shows (D-342): the to-do list of the agent and the
/// subagents at work. With neither, there is no panel.
public sealed record TaskPanel(IReadOnlyList<TodoItem> Todos, IReadOnlyList<AgentAtWork> Agents)
{
    public static readonly TaskPanel Empty = new([], []);

    public bool IsEmpty => Todos.Count == 0 && Agents.Count == 0;

    public int Done => AgentTodos.DoneCount(Todos);

    /// A subagent is known by the step that started it; a step that left the feed is skipped.
    public static TaskPanel Of(Conversation conversation)
    {
        var steps = conversation.Items.OfType<FeedItem.Step>().ToList();
        var agents = conversation.Agents
            .Select(id => steps.FirstOrDefault(step => step.ToolUseId == id))
            .OfType<FeedItem.Step>()
            .Select(step => new AgentAtWork(step.Line.Subject.Length > 0 ? step.Line.Subject : step.Line.Label, step.Progress))
            .ToList();

        return conversation.Todos.Count == 0 && agents.Count == 0 ? Empty : new TaskPanel(conversation.Todos, agents);
    }
}
