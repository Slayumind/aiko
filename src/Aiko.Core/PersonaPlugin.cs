using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace Aiko.Core;

/// The files of the aiko-persona plugin. The bridge writes them into a folder and prints its path,
/// and Claude Code copies that folder into its plugin cache (D-201). The version Claude Code keeps
/// is a hash of this content, so nothing here counts versions by hand.
public static class PersonaPlugin
{
    public const string Name = "aiko-persona";

    /// The hook argument the bridge answers to (D-206).
    public const string HookArgument = "hook";

    /// The events that change Aiko's face (D-206). Notification is filtered by the bridge, which
    /// keeps only the kinds that mean "waiting for you".
    public static readonly IReadOnlyList<string> HookEvents =
    [
        "UserPromptSubmit", "PostToolUse", "PermissionRequest", "Notification", "Stop", "StopFailure", "SessionEnd",
    ];

    /// The key of the memory server in the plugin. Claude Code builds the tool names from it.
    public const string MemoryServer = "memory";

    /// The permission rule that lets the model call both memory tools without a question (D-279).
    /// Claude Code names a tool of a plugin server mcp__plugin_<plugin>_<server>__<tool>, and a rule
    /// without the tool part covers the whole server.
    public const string MemoryPermission = "mcp__plugin_" + Name + "_" + MemoryServer;

    /// Relative path with forward slashes → file content. With the memory on, the plugin also
    /// brings the memory hooks and the server with the remember and forget tools.
    public static IReadOnlyDictionary<string, string> Files(Temperament temperament, string bridgeExePath, bool memory = false)
    {
        var files = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            [".claude-plugin/plugin.json"] = Manifest(),
            ["output-styles/aiko.md"] = PersonaPrompt.StyleFile(temperament),
            ["hooks/hooks.json"] = Hooks(bridgeExePath, memory),
        };
        if (memory)
        {
            files[".mcp.json"] = Servers(bridgeExePath);
        }

        return files;
    }

    /// Twelve hex characters of SHA-256 over the paths and contents. The bridge names the folder
    /// after it, so a changed persona lands in a new folder and never half-overwrites the old one.
    public static string ContentHash(IReadOnlyDictionary<string, string> files)
    {
        using var sha = SHA256.Create();
        foreach (var (path, content) in files.OrderBy(f => f.Key, StringComparer.Ordinal))
        {
            var bytes = Encoding.UTF8.GetBytes($"{path}\n{content}\n");
            sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
        }

        sha.TransformFinalBlock([], 0, 0);
        return Convert.ToHexStringLower(sha.Hash!)[..12];
    }

    private static string Manifest() =>
        new JsonObject
        {
            ["name"] = Name,
            ["description"] = "Aiko's persona and the hooks behind her face in the tray.",
        }.ToJsonString(Indented) + "\n";

    /// The command and its argument go separately, so no shell reads the path: the install path
    /// has spaces and may have letters that are not ASCII. async keeps the session from waiting.
    ///
    /// The memory hooks are not async: their output is what the model must read, the rules and the
    /// facts at the start of a session and the reminder beside a message. The rules and the facts
    /// are two hooks because Claude Code cuts each hook output on its own.
    private static string Hooks(string bridgeExePath, bool memory)
    {
        var events = new JsonObject();
        foreach (var name in HookEvents)
        {
            var hooks = new JsonArray(FaceHook(bridgeExePath));
            if (memory && name == "UserPromptSubmit")
            {
                hooks.Add(MemoryHook(bridgeExePath, MemoryBridgeMode.Prompt));
            }

            events[name] = new JsonArray(new JsonObject { ["hooks"] = hooks });
        }

        if (memory)
        {
            events["SessionStart"] = new JsonArray(
                new JsonObject
                {
                    ["hooks"] = new JsonArray(
                        MemoryHook(bridgeExePath, MemoryBridgeMode.Rules),
                        MemoryHook(bridgeExePath, MemoryBridgeMode.Facts)),
                });
        }

        return new JsonObject { ["hooks"] = events }.ToJsonString(Indented) + "\n";
    }

    private static JsonObject FaceHook(string bridgeExePath) => new()
    {
        ["type"] = "command",
        ["command"] = bridgeExePath,
        ["args"] = new JsonArray(HookArgument),
        ["async"] = true,
    };

    private static JsonObject MemoryHook(string bridgeExePath, MemoryBridgeMode mode) => new()
    {
        ["type"] = "command",
        ["command"] = bridgeExePath,
        ["args"] = new JsonArray(MemoryBridge.Argument, MemoryBridge.ArgumentFor(mode)),
    };

    private static string Servers(string bridgeExePath) =>
        new JsonObject
        {
            ["mcpServers"] = new JsonObject
            {
                [MemoryServer] = new JsonObject
                {
                    ["command"] = bridgeExePath,
                    ["args"] = new JsonArray(MemoryBridge.Argument, MemoryBridge.ArgumentFor(MemoryBridgeMode.Serve)),
                },
            },
        }.ToJsonString(Indented) + "\n";

    private static readonly System.Text.Json.JsonSerializerOptions Indented = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}
