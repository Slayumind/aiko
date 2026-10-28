using System.Text.Json;

namespace Aiko.Core.Terminal;

/// The file Claude Code keeps of a conversation, read into the feed of the terminal (D-303).
///
/// Aiko keeps no copy of a conversation. When the person goes back into a session, its feed is
/// read from this file into memory. The lines "user" and "assistant" carry a message in the same
/// shape as the stream, so they go through the same Conversation and give the same items.
///
/// The format is not documented, so every line is taken on its own terms: what is not understood
/// is skipped. Only the message of a line is read. The other lines hold the folder, the text of
/// the persona and the last prompt, and none of that is looked at.
public static class Transcript
{
    /// What Claude Code writes in the place of a message when the person stops a turn.
    private const string Interrupted = "[Request interrupted by user";

    /// The feed of a conversation that is over: nothing in it runs or waits.
    public static Conversation Read(IEnumerable<string> lines, PermissionMode mode)
    {
        var state = Conversation.Empty(mode);
        foreach (var line in lines)
        {
            state = Taken(state, line);
        }

        // A step without a result was cut off. A failure that was said back then is not the
        // state of the session now.
        return state with
        {
            Items = [.. state.Items.Select(item => item switch
            {
                FeedItem.Step { State: StepState.Running } step => step with { State = StepState.Failed },
                FeedItem.Shell { State: not ShellState.Done } shell => shell with { State = ShellState.Done },
                _ => item,
            })],
            InTurn = false,
            Tasks = 0,
            Agents = [],
            Unread = false,
            Failure = TalkFailure.None,
            AnsweredInTurn = false,
            FailedInTurn = false,
            Compacting = false,
        };
    }

    private static Conversation Taken(Conversation state, string line)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(line);
        }
        catch (JsonException)
        {
            return state;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || Flag(root, "isSidechain"))
            {
                return state;
            }

            switch (Word(root, "type"))
            {
                case "assistant" when !IsEmptyReply(root):
                    return state.On(StreamEvent.FromLine(line)).State;
                case "user" when !Flag(root, "isMeta") && !Flag(root, "isCompactSummary"):
                    return FromPerson(state, root, line);
                case "system" when Word(root, "subtype") == "compact_boundary":
                    return state.On(StreamEvent.FromLine(line)).State;
                case "attachment" when QueuedByPerson(Member(root, "attachment")) is { } said:
                    return FromPerson(state, said.RootElement, said.RootElement.GetRawText());
                default:
                    return state;
            }
        }
    }

    /// A message the person sent while Claude worked. Claude Code writes it into the file as an
    /// attachment when it takes the message, not as a message of the person (2.1.312, RESEARCH).
    /// Notices of tasks and messages of other sessions come the same way and stay out of the feed.
    /// The words are given back as the message they were, so they read like any other.
    private static JsonDocument? QueuedByPerson(JsonElement attachment)
    {
        var origin = Member(attachment, "origin");
        var from = origin.ValueKind == JsonValueKind.Object ? Word(origin, "kind") : Word(attachment, "origin");
        if (Word(attachment, "type") != "queued_command" || Word(attachment, "commandMode") != "prompt"
            || Flag(attachment, "isMeta") || from is not ("" or "human"))
        {
            return null;
        }

        var prompt = Member(attachment, "prompt");
        if (prompt.ValueKind is not (JsonValueKind.String or JsonValueKind.Array))
        {
            return null;
        }

        var said = new System.Text.Json.Nodes.JsonObject
        {
            ["type"] = "user",
            ["message"] = new System.Text.Json.Nodes.JsonObject
            {
                ["role"] = "user",
                ["content"] = System.Text.Json.Nodes.JsonNode.Parse(prompt.GetRawText()),
            },
        };
        return JsonDocument.Parse(said.ToJsonString());
    }

    /// A "user" line is either what tools gave back, or what the person wrote.
    private static Conversation FromPerson(Conversation state, JsonElement root, string line)
    {
        if (StreamEvent.FromLine(line) is not StreamEvent.User user)
        {
            return state;
        }

        if (user.Results.Count > 0)
        {
            return state.On(user).State;
        }

        var images = Pictures(root);
        if (user.Text.StartsWith(Interrupted, StringComparison.Ordinal))
        {
            return state with
            {
                Items = [.. state.Items, new FeedItem.Notice(state.NextId, NoticeKind.Interrupted, "")],
                NextId = state.NextId + 1,
            };
        }

        // A command of "!". Claude Code writes its output as the next message; Aiko, in the same one.
        if (ShellCommand.Read(user.Text) is { } shell)
        {
            return state with
            {
                Items = [.. state.Items, new FeedItem.Shell(state.NextId, shell.Command, ShellState.Done, shell.Output ?? "", shell.ExitCode)],
                NextId = state.NextId + 1,
            };
        }

        if (ShellCommand.Output(user.Text) is { } printed && state.Items is [.., FeedItem.Shell last])
        {
            return state with { Items = [.. state.Items.SkipLast(1), last with { Output = printed }] };
        }

        // What a command printed without the model. The stream shows it as a notice, so does this.
        if (Tagged(user.Text, "local-command-stdout") is { Length: > 0 } output && user.Text.StartsWith('<'))
        {
            return state with
            {
                Items = [.. state.Items, new FeedItem.Notice(state.NextId, NoticeKind.Output, output)],
                NextId = state.NextId + 1,
            };
        }

        var text = Written(user.Text);
        if (text.Length == 0 && images.Count == 0)
        {
            return state;
        }

        return state with
        {
            Items = [.. state.Items, new FeedItem.OwnerMessage(state.NextId, text, false, images)],
            NextId = state.NextId + 1,
        };
    }

    /// The words of the person. Claude Code wraps a command in tags and writes notes of its own
    /// as messages; a command is shown the way it was typed, a note is not shown.
    private static string Written(string text)
    {
        if (!text.StartsWith('<'))
        {
            return text;
        }

        var command = Tagged(text, "command-name");
        if (command.Length == 0)
        {
            return "";
        }

        var arguments = Tagged(text, "command-args");
        return arguments.Length > 0 ? $"{command} {arguments}" : command;
    }

    private static string Tagged(string text, string tag)
    {
        var start = text.IndexOf($"<{tag}>", StringComparison.Ordinal);
        var end = text.IndexOf($"</{tag}>", StringComparison.Ordinal);
        return start >= 0 && end > start
            ? text[(start + tag.Length + 2)..end].Trim(' ', '\t', '\r', '\n')
            : "";
    }

    /// Claude Code fills a turn without an answer with a message of its own, from no model.
    private static bool IsEmptyReply(JsonElement root) =>
        Word(Member(root, "message"), "model") == "<synthetic>" && Word(root, "error").Length == 0;

    /// The pictures the person sent, as Claude Code wrote them into the file.
    private static IReadOnlyList<AttachedImage> Pictures(JsonElement root) =>
    [
        .. Blocks(root)
            .Where(block => Word(block, "type") == "image")
            .Select(block => Member(block, "source"))
            .Where(source => Word(source, "type") == "base64" && Word(source, "data").Length > 0)
            .Select(source => new AttachedImage(Word(source, "media_type"), Word(source, "data"))),
    ];

    private static IEnumerable<JsonElement> Blocks(JsonElement root) =>
        Member(Member(root, "message"), "content") is { ValueKind: JsonValueKind.Array } content
            ? content.EnumerateArray().Where(block => block.ValueKind == JsonValueKind.Object)
            : [];

    private static JsonElement Member(JsonElement node, string key) =>
        node.ValueKind == JsonValueKind.Object && node.TryGetProperty(key, out var value) ? value : default;

    private static string Word(JsonElement node, string key) =>
        Member(node, key) is { ValueKind: JsonValueKind.String } value ? StreamEvent.Decoded(value) : "";

    private static bool Flag(JsonElement node, string key) => Member(node, key).ValueKind == JsonValueKind.True;
}
