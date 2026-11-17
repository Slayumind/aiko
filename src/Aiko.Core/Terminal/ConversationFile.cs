using System.Text.Json;

namespace Aiko.Core.Terminal;

/// Where a conversation of Claude Code was started, as its file says.
public enum ConversationSource
{
    /// A terminal: Aiko, Warp, Windows Terminal or another one.
    Terminal,
    VsCode,
    Desktop,
    Other,
}

/// A conversation of Claude Code on disk (D-353): what the palette needs to find it and to open it
/// again in Aiko. Name is the title Claude Code or the person gave it, or the start of the first
/// message; FirstMessage is that message on one line.
public sealed record Conversation(
    string Id, string Environment, string Folder, string Name, string FirstMessage, long LastUsed, ConversationSource Source);

/// Reads a conversation from the start and the end of its file. The file can be hundreds of
/// megabytes, so only its first and last pieces are read (spike 2026-10: 64 KB and 32 KB). A line cut
/// at the edge of a piece is skipped.
public static class ConversationFile
{
    /// How much of the start and of the end of a file is read.
    public const int HeadBytes = 64 * 1024;
    public const int TailBytes = 32 * 1024;

    /// Null for a conversation of a program (claude -p, the SDK) and for one with no message of the
    /// person and no title: there is nothing to find it by.
    public static Conversation? Read(string id, string environment, string head, string tail, long lastUsed)
    {
        var folder = "";
        var entrypoint = "";
        var message = "";
        foreach (var line in head.Split('\n'))
        {
            if (Parse(line) is not { } root)
            {
                continue;
            }

            if (folder.Length == 0 && Text(root, "cwd") is { Length: > 0 } cwd)
            {
                folder = cwd;
            }

            if (entrypoint.Length == 0 && Text(root, "entrypoint") is { Length: > 0 } entry)
            {
                entrypoint = entry;
            }

            if (message.Length == 0 && Text(root, "type") == "user" && !Flag(root, "isMeta") && UserText(root) is { Length: > 0 } text)
            {
                message = TextMatch.OneLine(text);
            }
        }

        if (entrypoint.StartsWith("sdk", StringComparison.Ordinal))
        {
            return null;
        }

        // The last title wins; a title the person gave wins over the one Claude Code made.
        string? aiTitle = null;
        string? customTitle = null;
        foreach (var line in tail.Split('\n'))
        {
            if (Parse(line) is not { } root)
            {
                continue;
            }

            switch (Text(root, "type"))
            {
                case "ai-title" when Text(root, "aiTitle") is { Length: > 0 } title:
                    aiTitle = title;
                    break;
                case "custom-title" when Text(root, "customTitle") is { Length: > 0 } title:
                    customTitle = title;
                    break;
            }
        }

        var given = SessionName.Clean(customTitle ?? aiTitle);
        if (given.Length == 0 && message.Length == 0)
        {
            return null;
        }

        var place = SessionName.FolderPath(folder);
        var name = given.Length > 0 ? given : SessionName.FromMessage(message, SessionName.FolderName(place));
        return new Conversation(id, environment, place, name, message, lastUsed, SourceOf(entrypoint));
    }

    private static ConversationSource SourceOf(string entrypoint) => entrypoint switch
    {
        "cli" or "" => ConversationSource.Terminal,
        "claude-vscode" => ConversationSource.VsCode,
        "claude-desktop" => ConversationSource.Desktop,
        _ => ConversationSource.Other,
    };

    /// The words of a message of the person. A command, its output and the notes Claude Code adds
    /// come in tags and are not what the person wrote.
    private static string? UserText(JsonElement root)
    {
        if (!root.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object
            || !message.TryGetProperty("content", out var content))
        {
            return null;
        }

        var text = content.ValueKind switch
        {
            JsonValueKind.String => content.GetString(),
            JsonValueKind.Array => content.EnumerateArray()
                .Where(part => part.ValueKind == JsonValueKind.Object && Text(part, "type") == "text")
                .Select(part => Text(part, "text"))
                .FirstOrDefault(part => part is { Length: > 0 }),
            _ => null,
        };

        var trimmed = text?.Trim() ?? "";
        return trimmed.StartsWith('<') ? null : trimmed;
    }

    private static JsonElement? Parse(string line)
    {
        var text = line.Trim();
        if (!text.StartsWith('{') || !text.EndsWith('}'))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            return document.RootElement.ValueKind == JsonValueKind.Object ? document.RootElement.Clone() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Text(JsonElement node, string key) =>
        node.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool Flag(JsonElement node, string key) =>
        node.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.True;
}
