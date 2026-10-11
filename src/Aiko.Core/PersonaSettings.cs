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

/// Aiko herself: one temperament and one face for all environments (D-196). Whether she is on in
/// an environment is a flag of that environment, not stored here.
public sealed record PersonaSettings
{
    /// See AppSettings.CurrentSchema for why this is written from the start.
    public const int CurrentSchema = 1;

    public static readonly PersonaSettings Default = new();

    public int SchemaVersion { get; init; } = CurrentSchema;

    public Temperament Temperament { get; init; } = Temperament.Normal;

    public FaceStyle Face { get; init; } = FaceStyle.Chibi;

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
            return JsonSerializer.Deserialize<PersonaSettings>(json, Options) ?? Default;
        }
        catch (JsonException)
        {
            return Default;
        }
    }

    public string ToJson() => JsonSerializer.Serialize(this, Options) + Environment.NewLine;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };
}
