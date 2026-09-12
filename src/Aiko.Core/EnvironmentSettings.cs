using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aiko.Core;

/// One environment: a name the user chose and the Claude Code config folders inside it.
/// In 0.1 the interface shows two, but the core keeps a list from the start.
public sealed record AikoEnvironment(string Name, IReadOnlyList<string> ConfigDirectories)
{
    public bool DirectMode { get; init; }
}

public sealed record EnvironmentSettings(IReadOnlyList<AikoEnvironment> Environments)
{
    /// See AppSettings.CurrentSchema for why this is written from the start.
    public const int CurrentSchema = 1;

    public static readonly EnvironmentSettings Empty = new([]);

    public bool HasEnvironments => Environments.Count > 0;

    /// Which environment the ring shows. The click on the tray icon swaps it, so it is a name,
    /// not an index: renaming an environment must not silently point at the other one.
    public string? RingEnvironment { get; init; }

    public AikoEnvironment? Ring =>
        Environments.FirstOrDefault(e => e.Name == RingEnvironment) ?? Environments.FirstOrDefault();

    public AikoEnvironment? Dot =>
        Environments.Count < 2 ? null : Environments.FirstOrDefault(e => e.Name != Ring?.Name);

    /// The click swaps the ring and the dot; with one environment there is nothing to swap.
    public EnvironmentSettings SwapRing() =>
        Dot is { } other ? this with { RingEnvironment = other.Name } : this;

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
                .Select(e => new AikoEnvironment(e.Name!, e.ConfigDirectories!) { DirectMode = e.DirectMode })
                .ToList();

            return environments.Count > 0
                ? new EnvironmentSettings(environments) { RingEnvironment = file.RingEnvironment }
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
                Environments = Environments
                    .Select(e => new EnvironmentFile
                    {
                        Name = e.Name,
                        ConfigDirectories = e.ConfigDirectories,
                        DirectMode = e.DirectMode,
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
        public List<EnvironmentFile>? Environments { get; set; }
    }

    private sealed class EnvironmentFile
    {
        public string? Name { get; set; }
        public IReadOnlyList<string>? ConfigDirectories { get; set; }
        public bool DirectMode { get; set; }
    }
}
