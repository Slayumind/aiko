using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aiko.Core;

/// Adding and removing our one line in the Claude Code settings file. Text in, text out:
/// reading and writing the file, and its backup, belong to the app.
///
/// Two rules the spike taught us. First, find our key by parsing the JSON, never by matching
/// text: a search string assembled in the wrong order silently matches nothing. Second, if the
/// user already has a status line, keep it — the bridge calls it and shows its output.
public static class SettingsJsonPatch
{
    public const string StatusLineKey = "statusLine";
    public const string WrappedKey = "aikoWrappedStatusLine";

    private static readonly JsonSerializerOptions Formatting = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static bool TryAddBridge(string settingsJson, string bridgeCommand, out string patched)
    {
        patched = settingsJson;
        if (string.IsNullOrWhiteSpace(bridgeCommand))
        {
            return false;
        }

        if (!TryParseObject(settingsJson, out var root))
        {
            return false;
        }

        var ours = JsonNode.Parse($$"""
            { "type": "command", "command": {{JsonSerializer.Serialize(bridgeCommand)}} }
            """)!;

        if (root!.TryGetPropertyValue(StatusLineKey, out var existing) && existing is not null)
        {
            // Somebody else's status line is kept aside, so removing Aiko can put it back.
            if (IsOurs(existing, bridgeCommand))
            {
                return false;
            }
            root[WrappedKey] = existing.DeepClone();
        }

        root[StatusLineKey] = ours;
        patched = Write(root);
        return true;
    }

    public static bool TryRemoveBridge(string settingsJson, out string restored)
    {
        restored = settingsJson;
        if (!TryParseObject(settingsJson, out var root))
        {
            return false;
        }

        if (!root!.ContainsKey(StatusLineKey) && !root.ContainsKey(WrappedKey))
        {
            return false;
        }

        if (root.TryGetPropertyValue(WrappedKey, out var wrapped) && wrapped is not null)
        {
            root[StatusLineKey] = wrapped.DeepClone();
            root.Remove(WrappedKey);
        }
        else
        {
            root.Remove(StatusLineKey);
        }

        restored = Write(root);
        return true;
    }

    /// What the bridge should call after doing its own work, if anything.
    public static string? ReadWrappedCommand(string settingsJson)
    {
        if (!TryParseObject(settingsJson, out var root)
            || !root!.TryGetPropertyValue(WrappedKey, out var wrapped)
            || wrapped is not JsonObject wrappedObject)
        {
            return null;
        }

        return wrappedObject.TryGetPropertyValue("command", out var command) ? command?.GetValue<string>() : null;
    }

    private static bool IsOurs(JsonNode statusLine, string bridgeCommand) =>
        statusLine is JsonObject line
        && line.TryGetPropertyValue("command", out var command)
        && command?.GetValue<string>() == bridgeCommand;

    private static bool TryParseObject(string json, out JsonObject? root)
    {
        root = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            root = JsonNode.Parse(json) as JsonObject;
            return root is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string Write(JsonObject root) =>
        root.ToJsonString(Formatting) + Environment.NewLine;
}
