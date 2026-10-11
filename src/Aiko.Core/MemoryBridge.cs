using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aiko.Core;

/// The four jobs the bridge does for the memory.
public enum MemoryBridgeMode
{
    /// SessionStart hook: the rules of the memory.
    Rules,

    /// SessionStart hook: the facts. A hook of its own, because each hook output has its own cut.
    Facts,

    /// UserPromptSubmit hook: refresh facts by their keys, and remind the model when the person
    /// talks about themselves.
    Prompt,

    /// The MCP server with the remember and forget tools. It runs as long as the session.
    Serve,
}

/// What a memory job of the bridge decided: the text to print, if any, and the memory file to
/// write, if it changed. BadFile says the file on disk could not be read as a memory, so the
/// bridge must keep it aside before it writes a new one over it.
public sealed record MemoryBridgeStep(string? Output, string? MemoryJson, bool BadFile = false);

/// Every decision the bridge makes for the memory. The bridge itself only reads stdin and the
/// file, calls one function here, and prints and writes what comes back.
public static class MemoryBridge
{
    /// "Aiko.Bridge.exe memory rules|facts|prompt|serve".
    public const string Argument = "memory";

    /// Claude Code cuts a hook output longer than this and gives the model a short preview.
    public const int HookLimit = 10_000;

    public const string WriteFailed = "Aiko could not write the memory file. Nothing was saved.";

    public static MemoryBridgeMode? ModeFor(IReadOnlyList<string> args) =>
        args.Count >= 2 && args[0] == Argument
            ? args[1] switch
            {
                "rules" => MemoryBridgeMode.Rules,
                "facts" => MemoryBridgeMode.Facts,
                "prompt" => MemoryBridgeMode.Prompt,
                "serve" => MemoryBridgeMode.Serve,
                _ => null,
            }
            : null;

    public static string ArgumentFor(MemoryBridgeMode mode) => mode switch
    {
        MemoryBridgeMode.Rules => "rules",
        MemoryBridgeMode.Facts => "facts",
        MemoryBridgeMode.Prompt => "prompt",
        _ => "serve",
    };

    public static string RulesOutput(DateOnly today) => HookOutput("SessionStart", MemoryPrompt.Rules(today));

    /// A file nobody could read gives an empty list, not an error: the session must still start.
    public static string FactsOutput(string? memoryJson) =>
        HookOutput("SessionStart", FactsThatFit(MemoryFile.FromJson(memoryJson).Memory));

    /// Facts saved through the tools always fit. A file edited by hand may not, and a block over
    /// the cut reaches the model with no facts at all. So the longest facts that are not pinned
    /// stay out of the block until it fits; they stay in the file.
    public static string FactsThatFit(MemoryFile memory)
    {
        var text = MemoryPrompt.Facts(memory);
        while (text.Length > HookLimit && LongestToLeaveOut(memory) is { } fact)
        {
            memory = memory with { Facts = memory.Facts.Where(f => f.Id != fact.Id).ToList() };
            text = MemoryPrompt.Facts(memory);
        }

        return text;
    }

    public static MemoryBridgeStep OnPrompt(string hookInput, string? memoryJson, DateOnly today)
    {
        if (PromptIn(hookInput) is not { } prompt)
        {
            return new MemoryBridgeStep(null, null);
        }

        var output = MemoryNudge.Applies(prompt) ? HookOutput("UserPromptSubmit", MemoryNudge.Text) : null;
        var read = MemoryFile.FromJson(memoryJson);
        if (read.Unreadable)
        {
            return new MemoryBridgeStep(output, null);
        }

        var touch = MemoryEdits.TouchByPrompt(read.Memory, prompt, today);
        return new MemoryBridgeStep(output, touch.Touched.Count > 0 ? touch.Memory.ToJson() : null);
    }

    public static MemoryBridgeStep OnServerLine(string line, string? memoryJson, DateOnly today, string version)
    {
        var read = MemoryFile.FromJson(memoryJson);
        var reply = MemoryServer.Answer(line, read.Memory, today, version);
        return reply.Changed
            ? new MemoryBridgeStep(reply.Line, reply.Memory.ToJson(), read.Unreadable)
            : new MemoryBridgeStep(reply.Line, null);
    }

    /// The answer to a tool call whose change could not be written. The model must not read
    /// "Saved" for a fact that is not in the file.
    public static string? WriteFailedReply(string line) => MemoryServer.ToolFailure(line, WriteFailed);

    private static MemoryFact? LongestToLeaveOut(MemoryFile memory) =>
        memory.Facts
            .Where(f => !f.Pinned)
            .OrderByDescending(MemoryRules.LengthOf)
            .ThenBy(f => f.Seen)
            .ThenBy(f => f.Number)
            .FirstOrDefault();

    private static string? PromptIn(string hookInput)
    {
        try
        {
            return JsonNode.Parse(hookInput) is JsonObject root
                && root.TryGetPropertyValue("prompt", out var prompt)
                && prompt is JsonValue value
                && value.TryGetValue<string>(out var text)
                    ? text
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// additionalContext goes to the model; the person does not see it.
    private static string HookOutput(string hookEvent, string context) =>
        new JsonObject
        {
            ["hookSpecificOutput"] = new JsonObject
            {
                ["hookEventName"] = hookEvent,
                ["additionalContext"] = context,
            },
        }.ToJsonString(Readable);

    private static readonly JsonSerializerOptions Readable = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}
