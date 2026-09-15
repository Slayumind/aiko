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

    /// Relative path with forward slashes → file content.
    public static IReadOnlyDictionary<string, string> Files(Temperament temperament, string bridgeExePath) =>
        new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            [".claude-plugin/plugin.json"] = Manifest(),
            ["output-styles/aiko.md"] = PersonaPrompt.StyleFile(temperament),
            ["hooks/hooks.json"] = Hooks(bridgeExePath),
        };

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
    private static string Hooks(string bridgeExePath)
    {
        var events = new JsonObject();
        foreach (var name in HookEvents)
        {
            events[name] = new JsonArray(
                new JsonObject
                {
                    ["hooks"] = new JsonArray(
                        new JsonObject
                        {
                            ["type"] = "command",
                            ["command"] = bridgeExePath,
                            ["args"] = new JsonArray(HookArgument),
                            ["async"] = true,
                        }),
                });
        }

        return new JsonObject { ["hooks"] = events }.ToJsonString(Indented) + "\n";
    }

    private static readonly System.Text.Json.JsonSerializerOptions Indented = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}
