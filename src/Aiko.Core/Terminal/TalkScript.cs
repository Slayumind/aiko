using System.Text.Json;

namespace Aiko.Core.Terminal;

/// One thing the person does in a played conversation.
public sealed record TalkStep(string Do, string Text, string RequestId, CardChoice Choice, PermissionMode Mode);

/// A conversation to play without Claude Code: what a scripted Claude Code answers and what the
/// person does, in order. The cases of the runner and the door --try-conversation read the same
/// shape:
///
///     { "script": [{ "after": "user", "lines": [{...}], "exit": 0 }],
///       "steps":  [{ "do": "send", "text": "..." }] }
public sealed record TalkScript(IReadOnlyList<ScriptedReply> Replies, IReadOnlyList<TalkStep> Steps)
{
    /// Null when the text is not such a script.
    public static TalkScript? Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            return new TalkScript(
                [.. Items(root, "script").Select(ReplyIn)],
                [.. Items(root, "steps").Select(StepIn)]);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// Does every step in order. A step the runner cannot take changes nothing, like in the window.
    public void Play(ConversationRunner runner)
    {
        foreach (var step in Steps)
        {
            switch (step.Do)
            {
                case "send":
                    runner.Send(step.Text, []);
                    break;
                case "choose":
                    runner.Choose(step.RequestId, step.Choice);
                    break;
                case "interrupt":
                    runner.Interrupt();
                    break;
                case "setMode":
                    runner.SetMode(step.Mode);
                    break;
                case "markRead":
                    runner.MarkRead();
                    break;
                case "close":
                    runner.Close();
                    break;
            }
        }
    }

    private static ScriptedReply ReplyIn(JsonElement reply) => new(
        Word(reply, "after"),
        [.. Items(reply, "lines").Select(line => line.GetRawText())],
        reply.TryGetProperty("exit", out var exit) && exit.TryGetInt32(out var code) ? code : null,
        reply.TryGetProperty("fails", out var fails) && fails.ValueKind == JsonValueKind.True);

    private static TalkStep StepIn(JsonElement step) => new(
        Word(step, "do"),
        Word(step, "text"),
        Word(step, "requestId"),
        ChoiceIn(step),
        PermissionModes.FromStream(Word(step, "mode")) ?? PermissionMode.Manual);

    private static CardChoice ChoiceIn(JsonElement step) => Word(step, "choice") switch
    {
        "second" => new CardChoice.Second(),
        "deny" => new CardChoice.Deny(Word(step, "message")),
        "answers" => new CardChoice.Answers(
            [.. Items(step, "picked").Select(picks => (IReadOnlyList<string>)[.. Strings(picks)])]),
        _ => new CardChoice.Allow(),
    };

    private static IEnumerable<string> Strings(JsonElement list) =>
        list.ValueKind == JsonValueKind.Array
            ? list.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString() ?? "")
            : [];

    private static string Word(JsonElement node, string key) =>
        node.ValueKind == JsonValueKind.Object && node.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

    private static IEnumerable<JsonElement> Items(JsonElement node, string key) =>
        node.ValueKind == JsonValueKind.Object && node.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray()
            : [];
}
