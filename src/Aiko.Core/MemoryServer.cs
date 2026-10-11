using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Aiko.Core;

/// What the memory server does with one line: the line to print back, or null when the message
/// needs no answer, and the memory after it. Changed is true when the file must be written.
public sealed record MemoryServerReply(string? Line, MemoryFile Memory, bool Changed);

/// The memory tools as an MCP server, one JSON-RPC message per line.
///
/// Nothing here reads stdin, a file or a clock. The bridge reads a line, reads the memory file,
/// calls Answer, writes the file when it changed and prints the line. So every rule of the
/// protocol can be tested as "this line in, that line out".
public static class MemoryServer
{
    public const string Name = "aiko-memory";

    public const string RememberTool = "remember";

    public const string ForgetTool = "forget";

    /// The protocol we answer with when the client names none.
    public const string DefaultProtocol = "2025-06-18";

    private const int ParseError = -32700;

    private const int MethodNotFound = -32601;

    public static MemoryServerReply Answer(string line, MemoryFile memory, DateOnly today, string version)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(line);
        }
        catch (JsonException)
        {
            return Unchanged(memory, Error("null", ParseError, "Parse error"));
        }

        using (document)
        {
            var request = document.RootElement;
            if (request.ValueKind != JsonValueKind.Object)
            {
                return Unchanged(memory, Error("null", ParseError, "Parse error"));
            }

            // A message without an id is a notification: the protocol forbids an answer.
            if (!request.TryGetProperty("id", out var idElement))
            {
                return Unchanged(memory, null);
            }

            var id = idElement.GetRawText();
            var parameters = request.TryGetProperty("params", out var p) && p.ValueKind == JsonValueKind.Object
                ? p
                : default;
            return Text(request, "method") switch
            {
                "initialize" => Unchanged(memory, Result(id, w => WriteHello(w, parameters, version))),
                "ping" => Unchanged(memory, Result(id, _ => { })),
                "tools/list" => Unchanged(memory, Result(id, WriteTools)),
                "tools/call" => Call(id, parameters, memory, today),
                _ => Unchanged(memory, Error(id, MethodNotFound, "Method not found")),
            };
        }
    }

    /// A tool result that says the call failed, for the request in this line. Null when the line
    /// is not a request, so there is nobody to answer.
    public static string? ToolFailure(string line, string text)
    {
        try
        {
            using var document = JsonDocument.Parse(line);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("id", out var id)
                    ? Result(id.GetRawText(), w => WriteToolText(w, text, isError: true))
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static MemoryServerReply Call(string id, JsonElement parameters, MemoryFile memory, DateOnly today)
    {
        var arguments = parameters.ValueKind == JsonValueKind.Object
            && parameters.TryGetProperty("arguments", out var a) && a.ValueKind == JsonValueKind.Object
            ? a
            : default;
        var tool = Text(parameters, "name");
        MemoryEdit? edit = tool switch
        {
            RememberTool => MemoryEdits.Remember(memory, RequestFrom(arguments), today),
            ForgetTool => MemoryEdits.Forget(memory, Text(arguments, "id")),
            _ => null,
        };
        return edit is null
            ? Unchanged(memory, Result(id, w => WriteToolText(w, $"Unknown tool {tool}.", isError: true)))
            : new MemoryServerReply(
                Result(id, w => WriteToolText(w, edit.Reply, isError: false)), edit.Memory, edit.Changed);
    }

    private static MemoryRequest RequestFrom(JsonElement arguments) => new()
    {
        Id = Text(arguments, "id"),
        Category = Text(arguments, "category"),
        Text = Text(arguments, "fact"),
        Meaning = Text(arguments, "meaning"),
        Pin = Flag(arguments, "pin"),
        Keys = Strings(arguments, "keys"),
    };

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool? Flag(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null,
            }
            : null;

    private static IReadOnlyList<string>? Strings(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString()!)
                .ToList()
            : null;

    private static void WriteHello(Utf8JsonWriter writer, JsonElement parameters, string version)
    {
        writer.WriteString("protocolVersion", Text(parameters, "protocolVersion") ?? DefaultProtocol);
        writer.WriteStartObject("capabilities");
        writer.WriteStartObject("tools");
        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.WriteStartObject("serverInfo");
        writer.WriteString("name", Name);
        writer.WriteString("version", version);
        writer.WriteEndObject();
    }

    private static void WriteTools(Utf8JsonWriter writer)
    {
        writer.WriteStartArray("tools");
        WriteTool(
            writer, RememberTool,
            "Save a new fact about the user to Aiko memory, or refresh an existing one by id.",
            required: "category",
            properties: w =>
            {
                WriteProperty(w, "category", "string", CategoryHelp, enumValues: MemoryRules.Categories.Select(MemoryRules.NameOf));
                WriteProperty(w, "fact", "string", FactHelp);
                WriteProperty(w, "meaning", "string", "Short note in English: what this changes in your answers.");
                WriteProperty(w, "id", "string", "Id of an existing fact (F12) to refresh or reword it.");
                WriteProperty(w, "pin", "boolean", "Pinned facts are never forgotten.");
                WriteProperty(w, "keys", "array", KeysHelp, itemsAreStrings: true);
            });
        WriteTool(
            writer, ForgetTool,
            "Remove a fact from Aiko memory by id.",
            required: "id",
            properties: w => WriteProperty(w, "id", "string", "Id of the fact to forget."));
        writer.WriteEndArray();
    }

    private static void WriteTool(
        Utf8JsonWriter writer, string name, string description, string required, Action<Utf8JsonWriter> properties)
    {
        writer.WriteStartObject();
        writer.WriteString("name", name);
        writer.WriteString("description", description);
        writer.WriteStartObject("inputSchema");
        writer.WriteString("type", "object");
        writer.WriteStartObject("properties");
        properties(writer);
        writer.WriteEndObject();
        writer.WriteStartArray("required");
        writer.WriteStringValue(required);
        writer.WriteEndArray();
        writer.WriteEndObject();

        // Without this Claude Code defers the tool, and the model must search for it before the
        // first call. A memory that costs a search is a memory the model skips.
        writer.WriteStartObject("_meta");
        writer.WriteBoolean("anthropic/alwaysLoad", true);
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static void WriteProperty(
        Utf8JsonWriter writer, string name, string type, string description,
        IEnumerable<string>? enumValues = null, bool itemsAreStrings = false)
    {
        writer.WriteStartObject(name);
        writer.WriteString("type", type);
        writer.WriteString("description", description);
        if (enumValues is not null)
        {
            writer.WriteStartArray("enum");
            foreach (var value in enumValues)
            {
                writer.WriteStringValue(value);
            }

            writer.WriteEndArray();
        }

        if (itemsAreStrings)
        {
            writer.WriteStartObject("items");
            writer.WriteString("type", "string");
            writer.WriteEndObject();
            writer.WriteNumber("maxItems", MemoryRules.MaxKeys);
        }

        writer.WriteEndObject();
    }

    private static void WriteToolText(Utf8JsonWriter writer, string text, bool isError)
    {
        writer.WriteStartArray("content");
        writer.WriteStartObject();
        writer.WriteString("type", "text");
        writer.WriteString("text", text);
        writer.WriteEndObject();
        writer.WriteEndArray();
        writer.WriteBoolean("isError", isError);
    }

    private static string Result(string id, Action<Utf8JsonWriter> body) => Message(id, writer =>
    {
        writer.WriteStartObject("result");
        body(writer);
        writer.WriteEndObject();
    });

    private static string Error(string id, int code, string message) => Message(id, writer =>
    {
        writer.WriteStartObject("error");
        writer.WriteNumber("code", code);
        writer.WriteString("message", message);
        writer.WriteEndObject();
    });

    /// The id goes back exactly as it came: a number stays a number, a string stays a string.
    private static string Message(string id, Action<Utf8JsonWriter> body)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteString("jsonrpc", "2.0");
            writer.WritePropertyName("id");
            writer.WriteRawValue(id);
            body(writer);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static MemoryServerReply Unchanged(MemoryFile memory, string? line) => new(line, memory, Changed: false);

    /// One line per message, and Cyrillic stays readable in the replies the model reads.
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private const string CategoryHelp =
        "you: who the user is, what they know and learn (\"learns Swift\"); "
        + "work: how they like to work with you (\"short answer first\"); "
        + "projects: what they work on, one line each (\"makes the Aiko tray app\"); "
        + "interests: taste and hobbies (\"loves the Souls series\")";

    private const string FactHelp =
        "The fact in English, one short line. Quote the user's own words as they said them. "
        + "The fact, its meaning and keys together are at most 200 characters. Leave out when only refreshing.";

    private const string KeysHelp =
        "1-3 names or terms the user would say when this comes up again, in the user's own language "
        + "and spelling (\"Swift\", \"Ember\", \"C#\"). Not common words. A mention of a key refreshes "
        + "the fact by itself. Pinned facts and work habits take no keys.";
}
