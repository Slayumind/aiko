using System.Text.Json;

namespace Aiko.Core.Terminal;

/// One part of an answer of Claude.
public abstract record AssistantBlock
{
    public sealed record Text(string Value) : AssistantBlock;

    public sealed record ToolUse(string Id, string Name, JsonElement Input) : AssistantBlock;

    /// Claude is thinking. The thoughts themselves are not kept.
    public sealed record Thinking : AssistantBlock;
}

/// A picture as it travels in the stream: its type and its bytes in base64.
public sealed record StreamImage(string MediaType, string Data);

/// What a tool gave back.
public sealed record ToolResult(string ToolUseId, bool IsError, string Text, IReadOnlyList<StreamImage> Images);

/// One line Claude Code wrote to its output, as far as the terminal needs it (D-297).
///
/// Claude Code runs as a child process and speaks JSON lines. Most of a line is of no use here and
/// some of it must not be kept: the start event carries paths, and the hook events carry the text
/// of the persona and of the memory. Those parts are dropped by this parser, so nothing after it
/// can show or log them.
public abstract record StreamEvent
{
    /// Claude Code is ready for a turn. It comes before every turn, not once.
    public sealed record Ready(string SessionId, string Model, PermissionMode? Mode, string Version) : StreamEvent;

    /// A message of Claude, whole. Error is set when Claude Code answered in its place:
    /// "authentication_failed" means the environment is not signed in.
    public sealed record Assistant(string? ParentToolUseId, IReadOnlyList<AssistantBlock> Blocks, string? Error) : StreamEvent;

    /// What came back to Claude: results of tools, or text Claude Code wrote on its own.
    public sealed record User(
        string? ParentToolUseId,
        string Text,
        IReadOnlyList<ToolResult> Results,
        bool IsReplay,
        bool IsSynthetic) : StreamEvent;

    /// A piece of an answer that is still being written.
    public sealed record TextDelta(string? ParentToolUseId, string Text) : StreamEvent;

    /// Claude Code asks whether a tool may run. A question with options and a plan to approve
    /// arrive the same way, with NeedsAnswer set.
    public sealed record Request(
        string RequestId,
        string ToolName,
        string ToolUseId,
        JsonElement Input,
        string Description,
        bool SuggestsAcceptEdits,
        bool NeedsAnswer) : StreamEvent
    {
        /// Set when a subagent asks. Its tool call is not in the feed of the conversation.
        public string? AgentId { get; init; }

        /// The rule Claude Code offers to add so that it never asks about this again. It goes
        /// back as it came (D-306).
        public JsonElement? Rule { get; init; }

        /// The words of that rule, to show on the button: "mkdir build *".
        public string RuleText { get; init; } = "";
    }

    /// How many subagents still run in the background. A turn can end while they work, and the
    /// session is not free until this is zero (D-305).
    public sealed record Tasks(int Count) : StreamEvent;

    /// What a subagent is doing now, in a few words of its own.
    public sealed record TaskProgress(string ToolUseId, string Description) : StreamEvent;

    /// The answer to a request the terminal sent: an interrupt or a change of mode.
    public sealed record Reply(string RequestId, bool Ok, PermissionMode? Mode) : StreamEvent;

    /// Activity is "compacting" or "requesting" while that goes on, null when it is over.
    public sealed record Status(string? Activity, PermissionMode? Mode) : StreamEvent;

    /// The conversation was made shorter. What is above this point Claude remembers as a summary.
    public sealed record Compacted : StreamEvent;

    public sealed record RateLimit(IReadOnlyList<LimitWindow> Windows) : StreamEvent;

    /// The turn is over. Text is the output of a command that ran without the model.
    public sealed record Result(bool IsError, string Subtype, string TerminalReason, string Text) : StreamEvent;

    /// A line the terminal has no use for.
    public sealed record Ignored : StreamEvent;

    /// Not a JSON object.
    public sealed record Unreadable : StreamEvent;

    /// Never throws: one broken line must not end a conversation.
    public static StreamEvent FromLine(string? line)
    {
        var text = line?.Trim().TrimStart('﻿') ?? "";
        if (text.Length == 0)
        {
            return new Ignored();
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            return document.RootElement.ValueKind == JsonValueKind.Object ? Read(document.RootElement) : new Unreadable();
        }
        catch (JsonException)
        {
            return new Unreadable();
        }
    }

    private static StreamEvent Read(JsonElement root) => Str(root, "type") switch
    {
        "system" => ReadSystem(root),
        "assistant" => ReadAssistant(root),
        "user" => ReadUser(root),
        "stream_event" => ReadDelta(root),
        "control_request" => ReadRequest(root),
        "control_response" => ReadReply(root),
        "rate_limit_event" => ReadRateLimit(root),
        "result" => new Result(
            Flag(root, "is_error"),
            Str(root, "subtype") ?? "",
            Str(root, "terminal_reason") ?? "",
            Str(root, "result") ?? ""),
        _ => new Ignored(),
    };

    private static StreamEvent ReadSystem(JsonElement root)
    {
        switch (Str(root, "subtype"))
        {
            case "init" when Str(root, "session_id") is { Length: > 0 } session:
                return new Ready(
                    session,
                    Str(root, "model") ?? "",
                    PermissionModes.FromStream(Str(root, "permissionMode")),
                    Str(root, "claude_code_version") ?? "");
            case "status":
                return new Status(Str(root, "status"), PermissionModes.FromStream(Str(root, "permissionMode")));
            case "compact_boundary":
                return new Compacted();
            case "background_tasks_changed":
                return new Tasks(Items(root, "tasks").Count());
            case "task_progress" when Str(root, "tool_use_id") is { Length: > 0 } task:
                return new TaskProgress(task, Str(root, "description") ?? "");
            default:
                return new Ignored();
        }
    }

    private static StreamEvent ReadAssistant(JsonElement root)
    {
        var blocks = new List<AssistantBlock>();
        foreach (var block in Items(Object(root, "message"), "content"))
        {
            switch (Str(block, "type"))
            {
                case "text" when Str(block, "text") is { Length: > 0 } value:
                    blocks.Add(new AssistantBlock.Text(value));
                    break;
                case "tool_use" when Str(block, "id") is { Length: > 0 } id:
                    blocks.Add(new AssistantBlock.ToolUse(id, Str(block, "name") ?? "", Object(block, "input")?.Clone() ?? EmptyObject));
                    break;
                case "thinking":
                    blocks.Add(new AssistantBlock.Thinking());
                    break;
            }
        }

        var error = Str(root, "error");
        return blocks.Count == 0 && error is null
            ? new Ignored()
            : new Assistant(Str(root, "parent_tool_use_id"), blocks, error);
    }

    private static StreamEvent ReadUser(JsonElement root)
    {
        var message = Object(root, "message");
        var texts = new List<string>();
        var results = new List<ToolResult>();

        if (Str(message, "content") is { } plain)
        {
            texts.Add(plain);
        }

        foreach (var block in Items(message, "content"))
        {
            switch (Str(block, "type"))
            {
                case "text" when Str(block, "text") is { } value:
                    texts.Add(value);
                    break;
                case "tool_result" when Str(block, "tool_use_id") is { Length: > 0 } id:
                    results.Add(ReadResult(id, block));
                    break;
            }
        }

        return new User(
            Str(root, "parent_tool_use_id"),
            string.Join('\n', texts),
            results,
            Flag(root, "isReplay"),
            Flag(root, "isSynthetic"));
    }

    private static ToolResult ReadResult(string id, JsonElement block)
    {
        var texts = new List<string>();
        var images = new List<StreamImage>();

        if (Str(block, "content") is { } plain)
        {
            texts.Add(plain);
        }

        foreach (var part in Items(block, "content"))
        {
            switch (Str(part, "type"))
            {
                case "text" when Str(part, "text") is { } value:
                    texts.Add(value);
                    break;
                case "image" when Object(part, "source") is { } source
                    && Str(source, "type") == "base64"
                    && Str(source, "data") is { Length: > 0 } data:
                    images.Add(new StreamImage(Str(source, "media_type") ?? "", data));
                    break;
            }
        }

        return new ToolResult(id, Flag(block, "is_error"), string.Join('\n', texts), images);
    }

    private static StreamEvent ReadDelta(JsonElement root)
    {
        var change = Object(root, "event");
        var delta = Object(change, "delta");

        return Str(change, "type") == "content_block_delta"
            && Str(delta, "type") == "text_delta"
            && Str(delta, "text") is { Length: > 0 } text
            ? new TextDelta(Str(root, "parent_tool_use_id"), text)
            : new Ignored();
    }

    private static StreamEvent ReadRequest(JsonElement root)
    {
        var request = Object(root, "request");
        if (Str(request, "subtype") != "can_use_tool"
            || Str(root, "request_id") is not { Length: > 0 } id
            || Str(request, "tool_name") is not { Length: > 0 } tool)
        {
            return new Ignored();
        }

        var suggestsAcceptEdits = Items(request, "permission_suggestions")
            .Any(suggestion => Str(suggestion, "type") == "setMode" && Str(suggestion, "mode") == "acceptEdits");

        JsonElement? rule = Items(request, "permission_suggestions").FirstOrDefault(suggestion =>
            Str(suggestion, "type") == "addRules"
            && Str(suggestion, "behavior") == "allow"
            && RuleWords(Items(suggestion, "rules").FirstOrDefault(), tool).Length > 0);
        if (rule is { ValueKind: JsonValueKind.Undefined })
        {
            rule = null;
        }

        return new Request(
            id,
            tool,
            Str(request, "tool_use_id") ?? "",
            Object(request, "input")?.Clone() ?? EmptyObject,
            Str(request, "description") ?? "",
            suggestsAcceptEdits,
            Flag(request, "requires_user_interaction"))
        {
            AgentId = Str(request, "agent_id"),
            Rule = rule?.Clone(),
            RuleText = RuleWords(Items(rule, "rules").FirstOrDefault(), tool),
        };
    }

    /// The words of a rule that may be offered, or nothing. A rule with words of its own covers
    /// what they say. A rule without them covers every call of its tool, so it is taken only for
    /// the MCP tool that asks: its words are then the name of that tool (D-307).
    private static string RuleWords(JsonElement? rule, string tool)
    {
        if (Str(rule, "ruleContent") is { Length: > 0 } words)
        {
            return words;
        }
        return tool.StartsWith("mcp__", StringComparison.Ordinal) && Str(rule, "toolName") == tool ? tool : "";
    }

    private static StreamEvent ReadReply(JsonElement root)
    {
        var response = Object(root, "response");
        return Str(response, "request_id") is { Length: > 0 } id
            ? new Reply(
                id,
                Str(response, "subtype") == "success",
                PermissionModes.FromStream(Str(Object(response, "response"), "mode")))
            : new Ignored();
    }

    private static StreamEvent ReadRateLimit(JsonElement root)
    {
        var limits = Object(Object(root, "rate_limit_info"), "unifiedWindows");
        var windows = new List<LimitWindow>(2);
        AddWindow(windows, limits, "five_hour", LimitKind.FiveHour);
        AddWindow(windows, limits, "seven_day", LimitKind.SevenDay);
        return windows.Count > 0 ? new RateLimit(windows) : new Ignored();
    }

    /// The stream gives a share from 0 to 1 where the status line gives a percentage.
    private static void AddWindow(List<LimitWindow> windows, JsonElement? limits, string name, LimitKind kind)
    {
        var window = Object(limits, name);
        if (Number(window, "utilization") is { } share && Number(window, "resetsAt") is { } resetsAt)
        {
            windows.Add(new LimitWindow(
                kind,
                Percentage.FromDouble(share * 100),
                DateTimeOffset.FromUnixTimeSeconds((long)Math.Floor(resetsAt))));
        }
    }

    private static readonly JsonElement EmptyObject = JsonDocument.Parse("{}").RootElement.Clone();

    /// A key written twice reads as its last value, the way JsonDocument reads any file.
    private static JsonElement? Member(JsonElement? root, string key) =>
        root is { ValueKind: JsonValueKind.Object } element && element.TryGetProperty(key, out var value) ? value : null;

    private static string? Str(JsonElement? root, string key) =>
        Member(root, key) is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;

    private static bool Flag(JsonElement? root, string key) =>
        Member(root, key) is { ValueKind: JsonValueKind.True };

    private static double? Number(JsonElement? root, string key) =>
        Member(root, key) is { ValueKind: JsonValueKind.Number } value ? value.GetDouble() : null;

    private static JsonElement? Object(JsonElement? root, string key) =>
        Member(root, key) is { ValueKind: JsonValueKind.Object } value ? value : null;

    private static IEnumerable<JsonElement> Items(JsonElement? root, string key) =>
        Member(root, key) is { ValueKind: JsonValueKind.Array } array
            ? array.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.Object)
            : [];
}
