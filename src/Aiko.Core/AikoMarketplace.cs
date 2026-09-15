using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aiko.Core;

/// Aiko's own plugin marketplace: a folder on this computer with one marketplace.json (D-201, D-202).
/// Claude Code reads a local marketplace in place, so nothing is downloaded from anywhere.
public static class AikoMarketplace
{
    public const string Name = "aiko";

    /// Velopack keeps the installed app here across updates, so a command that points into this
    /// folder never has to change. A changed command stops Claude Code from running it until the
    /// person accepts it again.
    public const string InstallFolder = "Slayumind.Aiko";

    public static string PluginId(string plugin) => $"{plugin}@{Name}";

    public static bool IsOurs(string pluginId) => pluginId.EndsWith("@" + Name, StringComparison.Ordinal);

    public static string Folder(string localAppData) => Path.Combine(localAppData, "Aiko", "marketplace");

    public static string FilePath(string localAppData) => Path.Combine(Folder(localAppData), ".claude-plugin", "marketplace.json");

    /// The command Claude Code runs to get the persona plugin. For the installed app it goes
    /// through %LOCALAPPDATA%, which cmd.exe expands: the command stays plain ASCII even when the
    /// user name has Cyrillic letters or spaces. Null when no command fits Claude Code's rules.
    public static string? PersonaCommand(string bridgeExePath, string localAppData)
    {
        var installed = Path.Combine(localAppData, InstallFolder, "current") + Path.DirectorySeparatorChar;
        var path = bridgeExePath.StartsWith(installed, StringComparison.OrdinalIgnoreCase)
            ? "%LOCALAPPDATA%" + Path.DirectorySeparatorChar + bridgeExePath[(localAppData.TrimEnd('\\', '/').Length + 1)..]
            : bridgeExePath;

        var command = $"\"{path}\" {PersonaPluginOutput.Verb} {PersonaPlugin.Name}";
        return IsValidCommand(command) ? command : null;
    }

    /// Claude Code's rules for a command source: printable ASCII, at most 500 characters, and no
    /// run of four spaces, so the person can read the whole command they are asked to accept.
    public static bool IsValidCommand(string command) =>
        command.Length is > 0 and <= 500
        && command.All(c => c is >= ' ' and <= '~')
        && !command.Contains("    ", StringComparison.Ordinal);

    /// The persona from the bridge, and every skill Aiko ships from a folder inside the marketplace.
    public static string Json(string personaCommand, IEnumerable<SkillPlugin>? skills = null)
    {
        var plugins = new JsonArray(
            new JsonObject
            {
                ["name"] = PersonaPlugin.Name,
                ["description"] = "Aiko's persona and the hooks behind her face in the tray.",
                ["source"] = new JsonObject { ["source"] = "command", ["command"] = personaCommand },
            });

        foreach (var skill in (skills ?? []).OrderBy(s => IndexIn(SkillCatalog.All, s.Name)))
        {
            plugins.Add(new JsonObject
            {
                ["name"] = skill.Name,
                ["description"] = skill.Description,
                ["source"] = skill.Source,
            });
        }

        return new JsonObject
        {
            ["name"] = Name,
            ["owner"] = new JsonObject { ["name"] = "Aiko" },
            ["plugins"] = plugins,
        }.ToJsonString(Formatting) + "\n";
    }

    private static int IndexIn(IReadOnlyList<string> list, string name)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i] == name)
            {
                return i;
            }
        }

        return list.Count;
    }

    // The default encoder writes a quote as a six-character escape, which is valid but hard to read
    // for a person who opens the file to see the command.
    private static readonly JsonSerializerOptions Formatting = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}
