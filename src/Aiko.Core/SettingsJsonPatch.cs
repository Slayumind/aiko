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

        // A line kept aside that turns out to be ours is rubbish an older Aiko left behind when it
        // failed to recognise itself after a reinstall. Putting it back on removal would hand the
        // user a status line running a bridge that is gone.
        var cleaned = false;
        if (root!.TryGetPropertyValue(WrappedKey, out var alreadyAside)
            && BridgeCommand.IsAiko(CommandIn(alreadyAside)))
        {
            root.Remove(WrappedKey);
            cleaned = true;
        }

        if (root.TryGetPropertyValue(StatusLineKey, out var existing) && existing is not null)
        {
            var existingCommand = CommandIn(existing);
            if (BridgeCommand.IsAiko(existingCommand))
            {
                // Ours already. Same text means there is nothing to do; different text means the
                // install path or the shell changed, and it is replaced rather than kept aside.
                if (existingCommand == bridgeCommand && !cleaned)
                {
                    return false;
                }
            }
            else
            {
                // Somebody else's status line is kept aside, so removing Aiko can put it back.
                root[WrappedKey] = existing.DeepClone();
            }
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

        // A line kept aside that is ours was never the user's: it is left over from an older Aiko
        // that did not recognise itself. It is dropped, not restored.
        if (root.TryGetPropertyValue(WrappedKey, out var wrapped)
            && wrapped is not null
            && !BridgeCommand.IsAiko(CommandIn(wrapped)))
        {
            root[StatusLineKey] = wrapped.DeepClone();
            root.Remove(WrappedKey);
        }
        else
        {
            root.Remove(WrappedKey);
            root.Remove(StatusLineKey);
        }

        restored = Write(root);
        return true;
    }

    /// Whether the status line in this file is ours, whatever path or shell it names.
    public static bool HasOurLine(string settingsJson) =>
        TryParseObject(settingsJson, out var root)
        && root!.TryGetPropertyValue(StatusLineKey, out var line)
        && BridgeCommand.IsAiko(CommandIn(line));

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

    /// The command a status line object runs, or null when the node is not such an object.
    private static string? CommandIn(JsonNode? statusLine)
    {
        if (statusLine is not JsonObject line
            || !line.TryGetPropertyValue("command", out var command)
            || command is null)
        {
            return null;
        }

        try
        {
            return command.GetValue<string>();
        }
        catch (InvalidOperationException)
        {
            // The key holds something that is not a string. Not ours, and not our business.
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

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
