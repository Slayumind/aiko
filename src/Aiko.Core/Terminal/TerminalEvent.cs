using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aiko.Core.Terminal;

/// What a session in the terminal did last, as its second line says it (D-352).
public enum TerminalEventKind
{
    /// A message of the person went to the model.
    Thinking,
    Reading,
    Editing,
    Running,
    Searching,

    /// A page or a search on the web.
    Browsing,

    /// An agent of its own was started.
    Delegating,

    /// Any other tool. The detail is its name.
    Using,

    /// A permission prompt: the detail is what is asked for.
    AsksPermission,

    /// A question to the person.
    AsksQuestion,
    Finished,
    Failed,
    OutOfLimit,
}

/// The last hook event of a session of the terminal. The bridge writes it into the file of the
/// session; the window reads it. Detail is short and safe to show: the name of a file without its
/// folder, the start of a command, the name of a tool. The prompt and the answers are never read.
public sealed record TerminalEvent(TerminalEventKind Kind, string Detail, long At)
{
    /// The variable that tells the hook which session of the terminal it runs for (D-360).
    public const string SessionVariable = "AIKO_TERMINAL_SESSION";

    /// The argument the bridge answers to with this hook.
    public const string HookArgument = "terminal-event";

    /// The events the hook of the terminal is called for.
    public static readonly IReadOnlyList<string> HookEvents =
    [
        "UserPromptSubmit", "PreToolUse", "PostToolUse", "PermissionRequest", "Notification", "Stop", "StopFailure",
    ];

    /// Characters of a detail at most; a longer one ends in an ellipsis.
    public const int DetailLength = 40;

    private static readonly HashSet<string> QuestionNotifications = ["elicitation_dialog", "agent_needs_input"];

    public bool IsWaiting => Kind is TerminalEventKind.AsksPermission or TerminalEventKind.AsksQuestion;

    /// The turn is over.
    public bool IsEnd => Kind is TerminalEventKind.Finished or TerminalEventKind.Failed or TerminalEventKind.OutOfLimit;

    /// Whether this event goes into the file over the one written there. Async hooks run side by
    /// side, so the PostToolUse of the last tool can finish after Stop; a turn that ended stays
    /// ended until the person sends a message or Claude Code asks something.
    public bool Replaces(TerminalEvent? written) =>
        written is not { IsEnd: true } || IsEnd || IsWaiting || Kind == TerminalEventKind.Thinking;

    /// The file of a session: its id and ".json". Null for an id that is not one Aiko gives, so a
    /// variable from elsewhere can never point the bridge at another file.
    public static string? FileName(string? sessionId) =>
        sessionId is { Length: > 0 and <= 64 } id && id.All(c => char.IsAsciiLetterOrDigit(c) || c == '-')
            ? id + ".json"
            : null;

    /// Null for an event that changes nothing in the row, or for input that is not a hook call.
    /// now is in milliseconds since 1970.
    public static TerminalEvent? FromHook(string? json, long now)
    {
        if (Parse(json) is not { } root)
        {
            return null;
        }

        var tool = Text(root, "tool_name") ?? "";
        var input = root["tool_input"] as JsonObject;
        (TerminalEventKind Kind, string Detail)? found = Text(root, "hook_event_name") switch
        {
            "UserPromptSubmit" => (TerminalEventKind.Thinking, ""),
            "PreToolUse" or "PostToolUse" => ToolEvent(tool, input),
            "PermissionRequest" => (TerminalEventKind.AsksPermission, ToolEvent(tool, input).Detail),
            // A permission prompt is told by PermissionRequest six seconds earlier (spike 2026-10),
            // and with the detail; the notification would only wipe it.
            "Notification" when QuestionNotifications.Contains(Text(root, "notification_type") ?? "") => (TerminalEventKind.AsksQuestion, ""),
            "Stop" => (TerminalEventKind.Finished, ""),
            "StopFailure" => (Text(root, "error_type") == "rate_limit" ? TerminalEventKind.OutOfLimit : TerminalEventKind.Failed, ""),
            _ => null,
        };

        return found is { } one ? new TerminalEvent(one.Kind, Short(one.Detail), now) : null;
    }

    public string ToJson() =>
        new JsonObject
        {
            ["kind"] = char.ToLowerInvariant(Kind.ToString()[0]) + Kind.ToString()[1..],
            ["detail"] = Detail,
            ["at"] = At,
        }.ToJsonString(new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n";

    /// Never throws: a broken or unknown file is no event.
    public static TerminalEvent? FromJson(string? json)
    {
        if (Parse(json) is not { } root
            || Enum.GetNames<TerminalEventKind>().FirstOrDefault(name => name.Equals(Text(root, "kind"), StringComparison.OrdinalIgnoreCase)) is not { } kind
            || root["at"] is not JsonValue at
            || !at.TryGetValue<long>(out var time))
        {
            return null;
        }

        return new TerminalEvent(Enum.Parse<TerminalEventKind>(kind), Short(Text(root, "detail") ?? ""), time);
    }

    private static (TerminalEventKind Kind, string Detail) ToolEvent(string tool, JsonObject? input) => tool switch
    {
        "Read" or "NotebookRead" => (TerminalEventKind.Reading, FileOf(input)),
        "Edit" or "MultiEdit" or "Write" or "NotebookEdit" => (TerminalEventKind.Editing, FileOf(input)),
        "Bash" or "PowerShell" => (TerminalEventKind.Running, FirstLine(Text(input, "command"))),
        "Grep" or "Glob" => (TerminalEventKind.Searching, FirstLine(Text(input, "pattern"))),
        "WebFetch" => (TerminalEventKind.Browsing, Host(Text(input, "url"))),
        "WebSearch" => (TerminalEventKind.Browsing, FirstLine(Text(input, "query"))),
        "Task" or "Agent" => (TerminalEventKind.Delegating, FirstLine(Text(input, "description"))),
        _ => (TerminalEventKind.Using, ToolName(tool)),
    };

    /// The name of the file only: a folder may hold names the person would not show.
    private static string FileOf(JsonObject? input)
    {
        var path = Text(input, "file_path") ?? Text(input, "notebook_path") ?? "";
        var cut = path.TrimEnd('/', '\\');
        var at = cut.LastIndexOfAny(['/', '\\']);
        return at >= 0 ? cut[(at + 1)..] : cut;
    }

    private static string Host(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : "";

    /// A tool of a server is named mcp__server__tool: the tool part says the most.
    private static string ToolName(string tool)
    {
        var at = tool.LastIndexOf("__", StringComparison.Ordinal);
        return tool.StartsWith("mcp__", StringComparison.Ordinal) && at > 4 ? tool[(at + 2)..] : tool;
    }

    private static string FirstLine(string? text)
    {
        var line = (text ?? "").Trim();
        var end = line.IndexOfAny(['\r', '\n']);
        return end >= 0 ? line[..end] : line;
    }

    /// One line, spaces squeezed, at most DetailLength characters with the ellipsis.
    private static string Short(string text)
    {
        var line = new StringBuilder();
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c) || char.IsControl(c))
            {
                if (line.Length > 0 && line[^1] != ' ')
                {
                    line.Append(' ');
                }
            }
            else
            {
                line.Append(c);
            }
        }

        var clean = line.ToString().TrimEnd();
        return clean.Length <= DetailLength ? clean : clean[..(DetailLength - 1)].TrimEnd() + "…";
    }

    private static JsonObject? Parse(string? json)
    {
        try
        {
            return string.IsNullOrWhiteSpace(json) ? null : JsonNode.Parse(json) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Text(JsonObject? root, string key) =>
        root?[key] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}

/// The settings a session of the terminal starts with, through --settings: the hook of the
/// terminal for every event it needs. Claude Code adds these hooks to the ones of the person and of
/// the plugins, and settings.json is not touched. async keeps the session from waiting, and an
/// async hook decides no permission.
public static class TerminalHooks
{
    public static string SettingsJson(string bridgeExePath)
    {
        var events = new JsonObject();
        foreach (var name in TerminalEvent.HookEvents)
        {
            events[name] = new JsonArray(new JsonObject
            {
                ["hooks"] = new JsonArray(new JsonObject
                {
                    ["type"] = "command",
                    ["command"] = bridgeExePath,
                    ["args"] = new JsonArray(TerminalEvent.HookArgument),
                    ["async"] = true,
                }),
            });
        }

        return new JsonObject { ["hooks"] = events }.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }) + "\n";
    }
}

/// What a running session of Claude Code is doing, from its title and its last hook event (D-352,
/// D-360). Times are in milliseconds. seenAt is when the person last looked at the session, and
/// answeredAt when the person last typed into it: a question that came before is answered.
///
/// After Esc on a permission prompt Claude Code calls no hook (RESEARCH), so "waiting" would stay
/// for ever. Typing into the session is what ends it: that is how the person answers.
public static class TerminalPulse
{
    public static TerminalActivity Of(ClaudeTitle? title, TerminalEvent? last, long seenAt, long answeredAt)
    {
        if (last is { IsWaiting: true } && last.At > answeredAt)
        {
            return TerminalActivity.Waiting;
        }

        if (title is { Working: true })
        {
            return TerminalActivity.Working;
        }

        return last is { IsEnd: true } && last.At > seenAt ? TerminalActivity.Done : TerminalActivity.Idle;
    }

    /// Whether input that went to the program was typed by the person. The terminal also sends
    /// sequences of its own: focus in and out, answers about the cursor. Those are control
    /// sequences (ESC [ ... final) and say nothing. Arrows are such sequences too, but a prompt is
    /// answered with Enter, a digit or Esc, and those count.
    public static bool IsTyped(string input)
    {
        for (var at = 0; at < input.Length; at++)
        {
            if (input[at] != '\u001b' || at + 1 >= input.Length)
            {
                return true;
            }

            if (input[at + 1] == 'O' && at + 2 < input.Length)
            {
                at += 2;
                continue;
            }

            if (input[at + 1] != '[')
            {
                return true;
            }

            // Parameters and intermediates, then the final character.
            var end = at + 2;
            while (end < input.Length && input[end] is >= ' ' and <= '?')
            {
                end++;
            }

            if (end >= input.Length || input[end] is < '@' or > '~')
            {
                return true;
            }

            at = end;
        }

        return false;
    }
}
