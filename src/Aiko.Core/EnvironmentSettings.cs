using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aiko.Core;

/// One environment: a name the user chose and the Claude Code config folders inside it.
/// In 0.1 each environment has one folder, but the core keeps a list from the start.
public sealed record AikoEnvironment(string Name, IReadOnlyList<string> ConfigDirectories)
{
    public bool DirectMode { get; init; }

    /// A command name the person typed. Null means the command follows the environment name.
    public string? CustomCommand { get; init; }

    /// Project folders bound to this environment. Claude Code started inside them runs here.
    public IReadOnlyList<string> ProjectFolders { get; init; } = [];

    public string Command => CustomCommand ?? LaunchCommand.FromEnvironmentName(Name);

    public bool Holds(string folder) =>
        ConfigDirectories.Any(d => string.Equals(
            Path.TrimEndingDirectorySeparator(d),
            Path.TrimEndingDirectorySeparator(folder),
            StringComparison.OrdinalIgnoreCase));
}

public sealed record EnvironmentSettings(IReadOnlyList<AikoEnvironment> Environments)
{
    /// See AppSettings.CurrentSchema for why this is written from the start.
    /// 2 added custom commands, project folders and the default environment. A file of schema 1
    /// reads as before, with none of them set.
    public const int CurrentSchema = 2;

    /// The tray has a ring and a dot, so Aiko keeps two environments (D-152).
    public const int MaxEnvironments = 2;

    public static readonly EnvironmentSettings Empty = new([]);

    public bool HasEnvironments => Environments.Count > 0;

    /// Which environment the ring shows. The click on the tray icon swaps it, so it is a name,
    /// not an index: renaming an environment must not silently point at the other one.
    public string? RingEnvironment { get; init; }

    /// Which environment runs in folders bound to none. A name, for the same reason as the ring.
    public string? DefaultEnvironment { get; init; }

    public AikoEnvironment? Ring =>
        Environments.FirstOrDefault(e => e.Name == RingEnvironment) ?? Environments.FirstOrDefault();

    public AikoEnvironment? Dot =>
        Environments.Count < 2 ? null : Environments.FirstOrDefault(e => e.Name != Ring?.Name);

    /// The click swaps the ring and the dot; with one environment there is nothing to swap.
    public EnvironmentSettings SwapRing() =>
        Dot is { } other ? this with { RingEnvironment = other.Name } : this;

    /// Environment 1 is always the one in .claude: the VS Code panel, Claude Desktop and a plain
    /// claude all use that folder, whatever Aiko says (D-156).
    public AikoEnvironment? First(string userProfile) =>
        Environments.FirstOrDefault(e => e.ConfigDirectories.Any(d => ClaudeConfigFolder.IsDefault(d, userProfile)));

    /// The environment beside the one in .claude. Without a .claude environment there is no
    /// second one either: the wizard sets up environment 1 first.
    public AikoEnvironment? Second(string userProfile) =>
        First(userProfile) is null ? null : Kept(userProfile).Skip(1).FirstOrDefault();

    /// Environments beyond the two Aiko keeps. A list from before the two-environment rule can
    /// have them. The core never drops one by itself: removing an environment also takes Aiko's
    /// line out of that folder's settings, and that is the person's call in settings.
    public IReadOnlyList<AikoEnvironment> Extras(string userProfile)
    {
        var kept = Kept(userProfile);
        return Environments.Where(e => !kept.Contains(e)).ToList();
    }

    /// Environment 1 first, then the others with the ring ahead of the rest: the ring was the
    /// person's own choice of what to watch.
    private IReadOnlyList<AikoEnvironment> Kept(string userProfile)
    {
        var first = First(userProfile);
        var others = Environments
            .Where(e => e != first)
            .OrderBy(e => e.Name == RingEnvironment ? 0 : 1);

        return (first is null ? others : others.Prepend(first)).Take(MaxEnvironments).ToList();
    }

    public AikoEnvironment? Default(string userProfile) =>
        Environments.FirstOrDefault(e => e.Name == DefaultEnvironment)
        ?? First(userProfile)
        ?? Environments.FirstOrDefault();

    public static EnvironmentSettings FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Empty;
        }

        try
        {
            var file = JsonSerializer.Deserialize<SettingsFile>(json, Options);
            if (file?.Environments is null)
            {
                return Empty;
            }

            var environments = file.Environments
                .Where(e => !string.IsNullOrWhiteSpace(e.Name) && e.ConfigDirectories is { Count: > 0 })
                .Select(e => new AikoEnvironment(e.Name!, e.ConfigDirectories!)
                {
                    DirectMode = e.DirectMode,
                    CustomCommand = string.IsNullOrWhiteSpace(e.Command) ? null : e.Command,
                    ProjectFolders = e.ProjectFolders?.Where(p => !string.IsNullOrWhiteSpace(p)).ToList() ?? [],
                })
                .ToList();

            return environments.Count > 0
                ? new EnvironmentSettings(environments)
                {
                    RingEnvironment = file.RingEnvironment,
                    DefaultEnvironment = file.DefaultEnvironment,
                }
                : Empty;
        }
        catch (JsonException)
        {
            return Empty;
        }
    }

    public string ToJson() =>
        JsonSerializer.Serialize(
            new SettingsFile
            {
                SchemaVersion = CurrentSchema,
                RingEnvironment = RingEnvironment ?? Ring?.Name,
                DefaultEnvironment = DefaultEnvironment,
                Environments = Environments
                    .Select(e => new EnvironmentFile
                    {
                        Name = e.Name,
                        ConfigDirectories = e.ConfigDirectories,
                        DirectMode = e.DirectMode,
                        Command = e.CustomCommand,
                        ProjectFolders = e.ProjectFolders.Count > 0 ? e.ProjectFolders : null,
                    })
                    .ToList(),
            },
            Options) + Environment.NewLine;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private sealed class SettingsFile
    {
        public int SchemaVersion { get; set; }
        public string? RingEnvironment { get; set; }
        public string? DefaultEnvironment { get; set; }
        public List<EnvironmentFile>? Environments { get; set; }
    }

    private sealed class EnvironmentFile
    {
        public string? Name { get; set; }
        public IReadOnlyList<string>? ConfigDirectories { get; set; }
        public bool DirectMode { get; set; }
        public string? Command { get; set; }
        public IReadOnlyList<string>? ProjectFolders { get; set; }
    }
}
