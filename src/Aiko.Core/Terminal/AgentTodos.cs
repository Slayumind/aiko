using System.Text.Json;
using System.Text.RegularExpressions;

namespace Aiko.Core.Terminal;

public enum TodoState
{
    Pending,
    Now,
    Done,
}

/// One line of the to-do list of the agent. Doing is how the line reads while it is being done
/// ("Running the tests"); it is empty when Claude did not give it.
public sealed record TodoItem(string Id, string Text, string Doing, TodoState State);

/// The to-do list of the agent, as its tools change it (D-344).
///
/// Claude Code gives these tools to newer models only when the environment asks for them
/// (RESEARCH). TaskCreate adds a line, and its number comes back only in the result: until then
/// the line is known by the id of the tool call. TaskUpdate changes a line by its number.
/// TodoWrite is the older tool: it sends the whole list each time.
public static partial class AgentTodos
{
    private const string Unnumbered = "use:";

    /// The tools of the list. Their steps are not lines of the feed: the panel shows them.
    public static bool Owns(string toolName) =>
        toolName is "TaskCreate" or "TaskUpdate" or "TaskList" or "TaskGet" or "TodoWrite";

    public static IReadOnlyList<TodoItem> Used(IReadOnlyList<TodoItem> list, string toolName, string toolUseId, JsonElement input) =>
        toolName switch
        {
            "TaskCreate" when Text(input, "subject") is { Length: > 0 } subject =>
                [.. list, new TodoItem(Unnumbered + toolUseId, subject, Text(input, "activeForm"), TodoState.Pending)],
            "TaskUpdate" when Text(input, "taskId") is { Length: > 0 } id => Updated(list, id, input),
            "TodoWrite" => Written(input),
            _ => list,
        };

    /// A created line takes the number its result names. A line whose creation failed goes.
    public static IReadOnlyList<TodoItem> Returned(IReadOnlyList<TodoItem> list, ToolResult result)
    {
        var id = Unnumbered + result.ToolUseId;
        if (!list.Any(item => item.Id == id))
        {
            return list;
        }
        if (result.IsError)
        {
            return [.. list.Where(item => item.Id != id)];
        }
        return Number().Match(result.Text) is { Success: true } number
            ? [.. list.Select(item => item.Id == id ? item with { Id = number.Groups[1].Value } : item)]
            : list;
    }

    public static int DoneCount(IReadOnlyList<TodoItem> list) => list.Count(item => item.State == TodoState.Done);

    public static bool AllDone(IReadOnlyList<TodoItem> list) => list.Count > 0 && list.All(item => item.State == TodoState.Done);

    private static IReadOnlyList<TodoItem> Updated(IReadOnlyList<TodoItem> list, string id, JsonElement input)
    {
        var status = Text(input, "status");
        if (status == "deleted")
        {
            return [.. list.Where(item => item.Id != id)];
        }

        return [.. list.Select(item => item.Id != id ? item : item with
        {
            Text = Text(input, "subject") is { Length: > 0 } subject ? subject : item.Text,
            Doing = Text(input, "activeForm") is { Length: > 0 } doing ? doing : item.Doing,
            State = StateOf(status) ?? item.State,
        })];
    }

    private static IReadOnlyList<TodoItem> Written(JsonElement input)
    {
        if (!input.TryGetProperty("todos", out var todos) || todos.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return [.. todos.EnumerateArray()
            .Where(todo => Text(todo, "content").Length > 0)
            .Select((todo, index) => new TodoItem(
                (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                Text(todo, "content"),
                Text(todo, "activeForm"),
                StateOf(Text(todo, "status")) ?? TodoState.Pending))];
    }

    private static TodoState? StateOf(string status) => status switch
    {
        "pending" => TodoState.Pending,
        "in_progress" => TodoState.Now,
        "completed" => TodoState.Done,
        _ => null,
    };

    private static string Text(JsonElement node, string key) =>
        node.ValueKind == JsonValueKind.Object && node.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

    /// "Task #12 created successfully: Write the tests".
    [GeneratedRegex(@"#(\d+)")]
    private static partial Regex Number();
}
