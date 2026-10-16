using System.Text.Json;

namespace Aiko.Core.Terminal;

public enum StepKind
{
    Read,
    Search,
    Edit,
    Run,
    Agent,
    Web,

    /// A skill of Claude Code. The subject is its name.
    Skill,

    /// Any other tool. The label names it.
    Other,
}

/// One step of the work as one line of the feed: what kind it is and what it is about (D-294).
///
/// Label is set for a tool the terminal has no word for: the name of the tool, or the name of the
/// MCP server for a tool that comes from one, written for a person (D-324). Added and Removed count
/// the lines an edit changes.
/// A hidden step is not drawn: a question and a plan come as cards, and two tools are only noise.
public sealed record StepLine(StepKind Kind, string Label, string Subject, int Added, int Removed, bool Hidden)
{
    public static StepLine For(string toolName, JsonElement input)
    {
        switch (toolName)
        {
            case "Read":
                var file = Text(input, "file_path");
                return Line(StepKind.Read, FileName(file)) with { Hidden = IsAttachment(file) };
            case "Glob" or "Grep":
                return Line(StepKind.Search, Text(input, "pattern"));
            case "Write":
                return Line(StepKind.Edit, FileName(Text(input, "file_path"))) with
                {
                    Added = Lines(Text(input, "content")).Count,
                };
            case "Edit":
                var (added, removed) = Changed(Text(input, "old_string"), Text(input, "new_string"));
                return Line(StepKind.Edit, FileName(Text(input, "file_path"))) with { Added = added, Removed = removed };
            case "NotebookEdit":
                return Line(StepKind.Edit, FileName(Text(input, "notebook_path")));
            case "Bash" or "PowerShell":
                return Line(StepKind.Run, FirstLine(Text(input, "command")));
            case "Agent" or "Task":
                var task = Text(input, "description");
                return Line(StepKind.Agent, task.Length > 0 ? task : Text(input, "subagent_type"));
            case "Skill":
                return Line(StepKind.Skill, Text(input, "skill"));
            case "WebFetch":
                return Line(StepKind.Web, Text(input, "url"));
            case "WebSearch":
                return Line(StepKind.Web, Text(input, "query"));
            case "AskUserQuestion" or "ExitPlanMode" or "ToolSearch":
                return new StepLine(StepKind.Other, toolName, "", 0, 0, Hidden: true);
        }

        // mcp__<server>__<tool>: the server may have underscores of its own, the tool comes last.
        if (toolName.StartsWith("mcp__", StringComparison.Ordinal))
        {
            var cut = toolName.LastIndexOf("__", StringComparison.Ordinal);
            if (cut > 5 && cut + 2 < toolName.Length)
            {
                return new StepLine(StepKind.Other, ServerName(toolName[5..cut]), toolName[(cut + 2)..], 0, 0, false);
            }
        }

        return new StepLine(StepKind.Other, toolName, "", 0, 0, false);
    }

    /// The name of an MCP server as a person knows it. A server of a plugin comes as
    /// plugin_<plugin>_<server>, and only the server stays. A connector of claude.ai comes as
    /// claude_ai_Google_Calendar and reads Google Calendar. Plugin names are written with hyphens,
    /// so the first underscore after the plugin is the cut.
    public static string ServerName(string server)
    {
        const string Plugin = "plugin_";
        const string ClaudeAi = "claude_ai_";
        if (server.StartsWith(Plugin, StringComparison.Ordinal))
        {
            var cut = server.IndexOf('_', Plugin.Length);
            if (cut > Plugin.Length && cut + 1 < server.Length)
            {
                return server[(cut + 1)..];
            }
        }
        else if (server.StartsWith(ClaudeAi, StringComparison.Ordinal) && server.Length > ClaudeAi.Length)
        {
            return server[ClaudeAi.Length..].Replace('_', ' ');
        }

        return server;
    }

    /// The lines of an edit that differ, after the equal lines at both ends are set aside.
    public static (int Added, int Removed) Changed(string before, string after)
    {
        var old = Lines(before);
        var now = Lines(after);
        var (start, end) = EqualEnds(old, now);
        return (now.Count - start - end, old.Count - start - end);
    }

    /// How many lines are equal at the start of two texts, and how many at the end after those.
    public static (int Start, int End) EqualEnds(IReadOnlyList<string> old, IReadOnlyList<string> now)
    {
        var start = 0;
        while (start < old.Count && start < now.Count && old[start] == now[start])
        {
            start++;
        }

        var end = 0;
        while (end < old.Count - start && end < now.Count - start && old[old.Count - 1 - end] == now[now.Count - 1 - end])
        {
            end++;
        }

        return (start, end);
    }

    /// The lines of a text. One line break at the very end does not start another line.
    public static IReadOnlyList<string> Lines(string text)
    {
        if (text.Length == 0)
        {
            return [];
        }

        var lines = text.Replace("\r\n", "\n").Split('\n').ToList();
        if (lines.Count > 1 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }
        return lines;
    }

    private static StepLine Line(StepKind kind, string subject) => new(kind, "", subject, 0, 0, false);

    private static string Text(JsonElement input, string key) =>
        input.ValueKind == JsonValueKind.Object
        && input.TryGetProperty(key, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

    /// The name after the last slash of either kind: the path may come from another system.
    private static string FileName(string path)
    {
        var trimmed = path.TrimEnd('/', '\\');
        return trimmed[(trimmed.LastIndexOfAny(['/', '\\']) + 1)..];
    }

    private static string FirstLine(string text)
    {
        var trimmed = text.Trim(' ', '\t', '\r', '\n');
        var end = trimmed.IndexOfAny(['\r', '\n']);
        return end < 0 ? trimmed : trimmed[..end].TrimEnd(' ', '\t');
    }

    /// Older versions of Claude Code save a pasted picture into a temporary folder and read it
    /// back with Read. That step is the picture the person already sees in their own message.
    private static bool IsAttachment(string path)
    {
        var normal = path.Replace('\\', '/');
        return normal.Contains("/claude-", StringComparison.Ordinal) && normal.Contains("/images/", StringComparison.Ordinal);
    }
}
