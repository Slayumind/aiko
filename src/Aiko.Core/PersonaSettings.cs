using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Aiko.Core;

/// How loud Aiko is in a session (D-196). Guardrails hold on every level (D-195).
public enum Temperament
{
    Quiet,
    Normal,
    Bright,
    Musou,
}

/// What Aiko looks like in the tray, on the island and in the windows (D-216).
public enum FaceStyle
{
    Chibi,
    Emoji,
}

/// Aiko herself: one temperament, one face and one skills switch for all environments (D-196).
/// Whether she talks in an environment is a flag of that environment, not stored here.
public sealed record PersonaSettings
{
    /// See AppSettings.CurrentSchema for why this is written from the start.
    public const int CurrentSchema = 1;

    public static readonly PersonaSettings Default = new();

    public int SchemaVersion { get; init; } = CurrentSchema;

    public Temperament Temperament { get; init; } = Temperament.Normal;

    public FaceStyle Face { get; init; } = FaceStyle.Chibi;

    /// All of Aiko's skills at once: they are one plugin, and it is on or off as a whole (D-237).
    public bool SkillsOn { get; init; } = true;

    /// Same rule as AppSettings: a file we cannot read, or a value from a newer Aiko, means the
    /// defaults for the whole file rather than half of it applied.
    public static PersonaSettings FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Default;
        }

        try
        {
            var settings = JsonSerializer.Deserialize<PersonaSettings>(json, Options) ?? Default;
            if (JsonNode.Parse(json) is JsonObject file && !file.ContainsKey("skillsOn"))
            {
                settings = settings with { SkillsOn = !EveryOldSkillIsOff(file["disabledSkills"]) };
            }

            return settings;
        }
        catch (JsonException)
        {
            return Default;
        }
    }

    public string ToJson() => JsonSerializer.Serialize(this, Options) + Environment.NewLine;

    /// Up to 0.2.1 each skill had its own switch, kept as a list of the skills switched off, first
    /// as aiko-copy and later as copy (D-197, D-234). A file from then keeps the skills off only if
    /// every one of them was off; any skill left on means the person wanted skills.
    private static bool EveryOldSkillIsOff(JsonNode? disabled)
    {
        if (disabled is not JsonArray list)
        {
            return false;
        }

        var off = list
            .Select(item => item is JsonValue value && value.TryGetValue<string>(out var name) ? name : null)
            .OfType<string>()
            .Select(name => name.StartsWith(OldPrefix, StringComparison.Ordinal) ? name[OldPrefix.Length..] : name)
            .ToHashSet(StringComparer.Ordinal);

        return OldSkills.All(off.Contains);
    }

    private const string OldPrefix = "aiko-";

    /// The skills that had a switch of their own, before the one switch.
    private static readonly string[] OldSkills =
        ["copy", "release-gate", "docs-hygiene", "blender-to-unity", "texturing", "glb-for-web", "palette", "gamedesign-research"];

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };
}
