using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aiko.Core;

/// Aiko's own plugin marketplace: a folder on this computer with one marketplace.json (D-201, D-234).
/// Claude Code reads a local marketplace in place, so nothing is downloaded from anywhere.
public static class AikoMarketplace
{
    public const string Name = "aiko";

    public static string PluginId(string plugin) => $"{plugin}@{Name}";

    public static bool IsOurs(string pluginId) => pluginId.EndsWith("@" + Name, StringComparison.Ordinal);

    /// Where Claude Code looks for the marketplace file inside a marketplace folder.
    public static string FileIn(PlatformConventions platform, string marketplaceFolder) =>
        platform.Join(marketplaceFolder, ".claude-plugin", "marketplace.json");

    /// The command Claude Code runs to get the persona plugin. For the installed app it names the
    /// local data folder through the platform's variable, %LOCALAPPDATA% on Windows, which the
    /// shell expands: the command stays plain ASCII even when the user name has Cyrillic letters or
    /// spaces. Null when no command fits Claude Code's rules.
    public static string? PersonaCommand(PlatformConventions platform, string bridgeExePath, AikoFolders folders)
    {
        // A system without such a variable never needs the installed folder: the command spells
        // the real path, and on macOS the app is not under the local base at all.
        var path = platform.LocalDataVariable is { } variable
            && bridgeExePath.StartsWith(folders.InstalledAppFolder + platform.DirectorySeparator, StringComparison.OrdinalIgnoreCase)
            ? variable + platform.DirectorySeparator + bridgeExePath[(TrimSeparators(platform, folders.LocalBase).Length + 1)..]
            : bridgeExePath;

        var command = $"\"{path}\" {PersonaPluginOutput.Verb} {PersonaPlugin.Name}";
        return IsValidCommand(command) ? command : null;
    }

    private static string TrimSeparators(PlatformConventions platform, string folder) =>
        folder.TrimEnd(platform.DirectorySeparator);

    /// Claude Code's rules for a command source: printable ASCII, at most 500 characters, and no
    /// run of four spaces, so the person can read the whole command they are asked to accept.
    public static bool IsValidCommand(string command) =>
        command.Length is > 0 and <= 500
        && command.All(c => c is >= ' ' and <= '~')
        && !command.Contains("    ", StringComparison.Ordinal);

    /// The persona from the bridge, and the skills plugin from a folder inside the marketplace.
    public static string Json(string personaCommand, SkillPlugin? skills = null)
    {
        var plugins = new JsonArray(
            new JsonObject
            {
                ["name"] = PersonaPlugin.Name,
                ["description"] = "Aiko's persona and the hooks behind her face in the tray.",
                ["source"] = new JsonObject { ["source"] = "command", ["command"] = personaCommand },
            });

        if (skills is not null)
        {
            plugins.Add(new JsonObject
            {
                ["name"] = skills.Name,
                ["description"] = skills.Description,
                ["source"] = skills.Source,
            });
        }

        return new JsonObject
        {
            ["name"] = Name,
            ["owner"] = new JsonObject { ["name"] = "Aiko" },
            ["plugins"] = plugins,
        }.ToJsonString(Formatting) + "\n";
    }

    // The default encoder writes a quote as a six-character escape, which is valid but hard to read
    // for a person who opens the file to see the command.
    private static readonly JsonSerializerOptions Formatting = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}
