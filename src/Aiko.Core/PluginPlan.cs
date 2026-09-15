using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aiko.Core;

public enum PluginStepKind
{
    AddMarketplace,
    RemoveMarketplace,
    Install,
    Enable,
    Disable,
    Update,
    Uninstall,
}

public sealed record PluginStep(PluginStepKind Kind, string Target)
{
    /// The claude command line for this step. User scope, so the plugin follows the account folder.
    public IReadOnlyList<string> Arguments => Kind switch
    {
        PluginStepKind.AddMarketplace => ["plugin", "marketplace", "add", Target],
        PluginStepKind.RemoveMarketplace => ["plugin", "marketplace", "remove", Target],
        PluginStepKind.Install => ["plugin", "install", Target, "-y", "--scope", "user"],
        PluginStepKind.Enable => ["plugin", "enable", Target, "--scope", "user"],
        PluginStepKind.Disable => ["plugin", "disable", Target, "--scope", "user"],
        PluginStepKind.Uninstall => ["plugin", "uninstall", Target, "--scope", "user"],
        _ => ["plugin", "update", Target, "-y", "--scope", "user"],
    };
}

/// What one Claude Code folder has of Aiko's plugins right now. Other plugins are not looked at:
/// they are the person's own and Aiko never touches them.
public sealed record PluginState(
    string? MarketplaceFolder,
    IReadOnlySet<string> Installed,
    IReadOnlySet<string> Enabled)
{
    public static readonly PluginState Nothing = new(null, new HashSet<string>(), new HashSet<string>());

    /// Reads settings.json, plugins/installed_plugins.json and plugins/known_marketplaces.json.
    /// A missing or broken file reads as nothing of ours there.
    public static PluginState Read(string? settingsJson, string? installedJson, string? knownMarketplacesJson) =>
        new(MarketplaceIn(knownMarketplacesJson), InstalledIn(installedJson), EnabledIn(settingsJson));

    private static string? MarketplaceIn(string? json) =>
        Parse(json)?[AikoMarketplace.Name] is JsonObject ours
        && ours["installLocation"] is JsonValue location
        && location.TryGetValue<string>(out var path)
            ? path
            : null;

    private static HashSet<string> InstalledIn(string? json)
    {
        var ours = new HashSet<string>(StringComparer.Ordinal);
        if (Parse(json)?["plugins"] is not JsonObject plugins)
        {
            return ours;
        }

        foreach (var (id, entries) in plugins)
        {
            if (AikoMarketplace.IsOurs(id)
                && entries is JsonArray list
                && list.OfType<JsonObject>().Any(e => e["scope"] is JsonValue scope && scope.TryGetValue<string>(out var s) && s == "user"))
            {
                ours.Add(id);
            }
        }

        return ours;
    }

    private static HashSet<string> EnabledIn(string? json)
    {
        var ours = new HashSet<string>(StringComparer.Ordinal);
        if (Parse(json)?["enabledPlugins"] is not JsonObject enabled)
        {
            return ours;
        }

        foreach (var (id, value) in enabled)
        {
            if (AikoMarketplace.IsOurs(id) && value is JsonValue flag && flag.TryGetValue<bool>(out var on) && on)
            {
                ours.Add(id);
            }
        }

        return ours;
    }

    private static JsonObject? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(json) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// Which of Aiko's plugins a folder should have, and the steps from what it has (D-197, D-202).
public static class PluginPlan
{
    /// The persona and every skill the person left on, only where the persona is on.
    public static IReadOnlySet<string> Desired(AikoEnvironment environment, PersonaSettings persona, IEnumerable<string> skillPlugins)
    {
        if (!environment.Persona)
        {
            return new HashSet<string>();
        }

        return skillPlugins
            .Where(persona.IsSkillOn)
            .Prepend(PersonaPlugin.Name)
            .Select(AikoMarketplace.PluginId)
            .ToHashSet(StringComparer.Ordinal);
    }

    public static IReadOnlyList<PluginStep> Steps(IReadOnlySet<string> desired, PluginState state, string marketplaceFolder)
    {
        var steps = new List<PluginStep>();

        if (desired.Count > 0)
        {
            if (state.MarketplaceFolder is null)
            {
                steps.Add(new(PluginStepKind.AddMarketplace, marketplaceFolder));
            }
            else if (!SameFolder(state.MarketplaceFolder, marketplaceFolder))
            {
                // A marketplace of the same name from another place, for example a build run from
                // the source folder. Claude Code keeps one place per name.
                steps.Add(new(PluginStepKind.RemoveMarketplace, AikoMarketplace.Name));
                steps.Add(new(PluginStepKind.AddMarketplace, marketplaceFolder));
            }
        }

        foreach (var id in desired.Order(StringComparer.Ordinal))
        {
            if (!state.Installed.Contains(id))
            {
                steps.Add(new(PluginStepKind.Install, id));
            }
            else if (!state.Enabled.Contains(id))
            {
                steps.Add(new(PluginStepKind.Enable, id));
            }
        }

        // Turned off, never uninstalled: the files stay, and turning the persona on again is quick.
        foreach (var id in state.Enabled.Where(id => !desired.Contains(id)).Order(StringComparer.Ordinal))
        {
            steps.Add(new(PluginStepKind.Disable, id));
        }

        return steps;
    }

    /// Everything of Aiko's that Claude Code keeps for a folder, for when Aiko or the environment
    /// goes. The plugins first: without the marketplace Claude Code may not know how to remove them.
    public static IReadOnlyList<PluginStep> Removal(PluginState state)
    {
        var steps = state.Installed
            .Order(StringComparer.Ordinal)
            .Select(id => new PluginStep(PluginStepKind.Uninstall, id))
            .ToList();

        if (state.MarketplaceFolder is not null)
        {
            steps.Add(new(PluginStepKind.RemoveMarketplace, AikoMarketplace.Name));
        }

        return steps;
    }

    private static bool SameFolder(string a, string b) =>
        string.Equals(Path.TrimEndingDirectorySeparator(a), Path.TrimEndingDirectorySeparator(b), StringComparison.OrdinalIgnoreCase);
}
