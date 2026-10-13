using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Aiko.Core.Terminal;

/// A picture the person attached to a message.
public sealed record AttachedImage(string MediaType, string Base64);

/// The lines the terminal writes to Claude Code: one JSON object on one line, without the line
/// break. The shapes come from the channel spike (D-297).
public static class HostLine
{
    /// Letters of any language and the plus sign of base64 stay as they are: the default encoder
    /// would write six characters for each of them.
    private static readonly JsonWriterOptions Options = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// Pictures go before the text, the way the API reads them best.
    public static string UserMessage(string text, IReadOnlyList<AttachedImage> images) => Write(writer =>
    {
        writer.WriteString("type", "user");
        writer.WriteStartObject("message");
        writer.WriteString("role", "user");
        writer.WriteStartArray("content");
        foreach (var image in images)
        {
            writer.WriteStartObject();
            writer.WriteString("type", "image");
            writer.WriteStartObject("source");
            writer.WriteString("type", "base64");
            writer.WriteString("media_type", image.MediaType);
            writer.WriteString("data", image.Base64);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }
        if (text.Length > 0)
        {
            writer.WriteStartObject();
            writer.WriteString("type", "text");
            writer.WriteString("text", text);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    });

    /// Claude Code wants the input of the tool back with the yes.
    public static string Allow(string requestId, JsonElement input) =>
        Respond(requestId, writer =>
        {
            writer.WriteString("behavior", "allow");
            writer.WritePropertyName("updatedInput");
            input.WriteTo(writer);
        });

    /// Yes, and no more questions about edits in this session: the mode change travels with the
    /// answer (D-305).
    public static string AllowEdits(string requestId, JsonElement input) =>
        Respond(requestId, writer =>
        {
            writer.WriteString("behavior", "allow");
            writer.WritePropertyName("updatedInput");
            input.WriteTo(writer);
            writer.WriteStartArray("updatedPermissions");
            writer.WriteStartObject();
            writer.WriteString("type", "setMode");
            writer.WriteString("mode", PermissionModes.Name(PermissionMode.AcceptEdits));
            writer.WriteString("destination", "session");
            writer.WriteEndObject();
            writer.WriteEndArray();
        });

    /// Yes, and the rule Claude Code offered goes back as it came. Claude Code writes it into the
    /// settings of the project itself (D-306).
    public static string AllowAlways(string requestId, JsonElement input, JsonElement rule) =>
        Respond(requestId, writer =>
        {
            writer.WriteString("behavior", "allow");
            writer.WritePropertyName("updatedInput");
            input.WriteTo(writer);
            writer.WriteStartArray("updatedPermissions");
            rule.WriteTo(writer);
            writer.WriteEndArray();
        });

    /// The message reaches Claude as the error of the tool.
    public static string Deny(string requestId, string message) =>
        Respond(requestId, writer =>
        {
            writer.WriteString("behavior", "deny");
            writer.WriteString("message", message);
        });

    /// The answer to a question with options: the input of the question with the answers added,
    /// each one under the text of its question.
    public static string Answer(string requestId, JsonElement input, IReadOnlyList<KeyValuePair<string, string>> answers) =>
        Respond(requestId, writer =>
        {
            writer.WriteString("behavior", "allow");
            writer.WriteStartObject("updatedInput");
            foreach (var member in Members(input))
            {
                if (!member.NameEquals("answers"))
                {
                    member.WriteTo(writer);
                }
            }
            writer.WriteStartObject("answers");
            foreach (var answer in answers)
            {
                writer.WriteString(answer.Key, answer.Value);
            }
            writer.WriteEndObject();
            writer.WriteEndObject();
        });

    public static string Interrupt(string requestId) =>
        Ask(requestId, writer => writer.WriteString("subtype", "interrupt"));

    public static string SetMode(string requestId, PermissionMode mode) =>
        Ask(requestId, writer =>
        {
            writer.WriteString("subtype", "set_permission_mode");
            writer.WriteString("mode", PermissionModes.RequestValue(mode));
        });

    private static IEnumerable<JsonProperty> Members(JsonElement input) =>
        input.ValueKind == JsonValueKind.Object ? input.EnumerateObject() : [];

    private static string Respond(string requestId, Action<Utf8JsonWriter> decision) => Write(writer =>
    {
        writer.WriteString("type", "control_response");
        writer.WriteStartObject("response");
        writer.WriteString("subtype", "success");
        writer.WriteString("request_id", requestId);
        writer.WriteStartObject("response");
        decision(writer);
        writer.WriteEndObject();
        writer.WriteEndObject();
    });

    private static string Ask(string requestId, Action<Utf8JsonWriter> request) => Write(writer =>
    {
        writer.WriteString("type", "control_request");
        writer.WriteString("request_id", requestId);
        writer.WriteStartObject("request");
        request(writer);
        writer.WriteEndObject();
    });

    private static string Write(Action<Utf8JsonWriter> body)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, Options))
        {
            writer.WriteStartObject();
            body(writer);
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);
    }
}
