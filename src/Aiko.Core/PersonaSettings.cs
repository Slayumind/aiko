using System.Text.Json;
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

/// Aiko herself: one temperament, one face and one skill list for all environments (D-196).
/// Whether she talks in an environment is a flag of that environment, not stored here.
public sealed record PersonaSettings
{
    /// See AppSettings.CurrentSchema for why this is written from the start.
    public const int CurrentSchema = 1;

    public static readonly PersonaSettings Default = new();

    public int SchemaVersion { get; init; } = CurrentSchema;

    public Temperament Temperament { get; init; } = Temperament.Normal;

    public FaceStyle Face { get; init; } = FaceStyle.Chibi;

    /// The skills the person switched off. Kept as the off list, so a skill that arrives with an
    /// update is on from the start, like all skills on the first run (D-197).
    public IReadOnlyList<string> DisabledSkills { get; init; } = [];

    public bool IsSkillOn(string skill) => !DisabledSkills.Contains(skill, StringComparer.Ordinal);

    public PersonaSettings WithSkill(string skill, bool on)
    {
        if (IsSkillOn(skill) == on)
        {
            return this;
        }

        return this with
        {
            DisabledSkills = on
                ? DisabledSkills.Where(s => s != skill).ToList()
                : DisabledSkills.Append(skill).Order(StringComparer.Ordinal).ToList(),
        };
    }

    /// A record compares lists by reference, so two equal files would not look equal.
    public bool Equals(PersonaSettings? other) =>
        other is not null
        && SchemaVersion == other.SchemaVersion
        && Temperament == other.Temperament
        && Face == other.Face
        && DisabledSkills.SequenceEqual(other.DisabledSkills);

    public override int GetHashCode() => HashCode.Combine(SchemaVersion, Temperament, Face, DisabledSkills.Count);

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
            return settings with
            {
                DisabledSkills = (settings.DisabledSkills ?? [])
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(WithoutOldPrefix)
                    .Distinct()
                    .Order(StringComparer.Ordinal)
                    .ToList(),
            };
        }
        catch (JsonException)
        {
            return Default;
        }
    }

    public string ToJson() => JsonSerializer.Serialize(this, Options) + Environment.NewLine;

    /// Up to 0.2.1 every skill was a plugin of its own named aiko-copy, aiko-palette and so on
    /// (D-202). A file written then still says so; without this a skill switched off then would
    /// quietly come back on.
    private static string WithoutOldPrefix(string skill) =>
        skill.StartsWith(OldPrefix, StringComparison.Ordinal) ? skill[OldPrefix.Length..] : skill;

    private const string OldPrefix = "aiko-";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };
}
