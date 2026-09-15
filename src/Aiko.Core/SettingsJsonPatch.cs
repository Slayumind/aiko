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

    public const string HooksKey = "hooks";
    public const string SessionStartKey = "SessionStart";

    /// Adds the bridge as a SessionStart hook, beside any hooks the person has.
    ///
    /// Hooks are a list of groups, each with its own list of commands. Ours goes in a group of its
    /// own, so a group the person wrote is never edited. A hook of ours with an old path is
    /// replaced, the same way the status line is.
    public static bool TryAddSessionHook(string settingsJson, string hookCommand, out string patched)
    {
        patched = settingsJson;
        if (string.IsNullOrWhiteSpace(hookCommand) || !TryParseObject(settingsJson, out var root))
        {
            return false;
        }

        if (root!.TryGetPropertyValue(HooksKey, out var hooksNode) && hooksNode is not null and not JsonObject)
        {
            // Something that is not an object. Claude Code would not read it either; not ours to fix.
            return false;
        }

        var hooks = hooksNode as JsonObject;
        if (hooks is not null
            && hooks.TryGetPropertyValue(SessionStartKey, out var existing)
            && existing is not null and not JsonArray)
        {
            return false;
        }

        var ours = hooks?[SessionStartKey] is JsonArray present ? OurHookCommands(present).ToList() : [];
        if (ours.Count == 1 && ours[0] == hookCommand)
        {
            return false;
        }

        if (hooks is null)
        {
            hooks = [];
            root[HooksKey] = hooks;
        }

        if (hooks[SessionStartKey] is not JsonArray groups)
        {
            groups = [];
            hooks[SessionStartKey] = groups;
        }

        RemoveOurHooks(groups);
        groups.Add(JsonNode.Parse($$"""
            { "hooks": [ { "type": "command", "command": {{JsonSerializer.Serialize(hookCommand)}} } ] }
            """));

        patched = Write(root);
        return true;
    }

    public static bool HasOurSessionHook(string settingsJson) =>
        TryParseObject(settingsJson, out var root)
        && SessionStartGroups(root!) is { } groups
        && OurHookCommands(groups).Any();

    public static bool TryRemoveBridge(string settingsJson, out string restored)
    {
        restored = settingsJson;
        if (!TryParseObject(settingsJson, out var root))
        {
            return false;
        }

        // The hook goes whenever Aiko leaves a file, with or without a status line of ours in it.
        var hookRemoved = RemoveSessionHook(root!);

        if (!root!.ContainsKey(StatusLineKey) && !root.ContainsKey(WrappedKey))
        {
            if (hookRemoved)
            {
                restored = Write(root);
            }

            return hookRemoved;
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

    /// The output style the person picked for themselves, if any. While the persona is on, its style
    /// wins over this one, and the settings page has to say so (D-207). Aiko never changes the key.
    public static string? UserOutputStyle(string settingsJson) =>
        TryParseObject(settingsJson, out var root)
        && root!.TryGetPropertyValue("outputStyle", out var style)
        && style is JsonValue value
        && value.TryGetValue<string>(out var name)
        && !string.IsNullOrWhiteSpace(name)
        && !name.Equals("default", StringComparison.OrdinalIgnoreCase)
        && !IsPersonaStyle(name)
            ? name
            : null;

    private static bool IsPersonaStyle(string name) =>
        name.Equals(PersonaPrompt.StyleName, StringComparison.OrdinalIgnoreCase)
        || name.Equals($"{PersonaPlugin.Name}:{PersonaPrompt.StyleName}", StringComparison.OrdinalIgnoreCase);

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

    private static JsonArray? SessionStartGroups(JsonObject root) =>
        root.TryGetPropertyValue(HooksKey, out var hooks) && hooks is JsonObject hooksObject
        && hooksObject.TryGetPropertyValue(SessionStartKey, out var groups)
            ? groups as JsonArray
            : null;

    private static IEnumerable<string> OurHookCommands(JsonArray groups) =>
        groups
            .OfType<JsonObject>()
            .SelectMany(group => group.TryGetPropertyValue(HooksKey, out var list) && list is JsonArray array
                ? array.OfType<JsonObject>()
                : [])
            .Select(hook => CommandIn(hook))
            .Where(command => BridgeCommand.IsAiko(command))
            .Select(command => command!);

    /// Takes our commands out of every group, then drops the groups, the SessionStart list and the
    /// hooks object that are left empty because of it. A group the person wrote keeps everything else.
    private static bool RemoveSessionHook(JsonObject root)
    {
        if (SessionStartGroups(root) is not { } groups || !OurHookCommands(groups).Any())
        {
            return false;
        }

        RemoveOurHooks(groups);

        var hooks = (JsonObject)root[HooksKey]!;
        if (groups.Count == 0)
        {
            hooks.Remove(SessionStartKey);
        }

        if (hooks.Count == 0)
        {
            root.Remove(HooksKey);
        }

        return true;
    }

    private static void RemoveOurHooks(JsonArray groups)
    {
        foreach (var group in groups.OfType<JsonObject>().ToList())
        {
            if (!group.TryGetPropertyValue(HooksKey, out var list) || list is not JsonArray commands)
            {
                continue;
            }

            var had = commands.Count;
            foreach (var hook in commands.OfType<JsonObject>().Where(h => BridgeCommand.IsAiko(CommandIn(h))).ToList())
            {
                commands.Remove(hook);
            }

            if (commands.Count == 0 && had > 0)
            {
                groups.Remove(group);
            }
        }
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
