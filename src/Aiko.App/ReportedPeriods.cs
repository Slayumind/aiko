using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aiko.App;

/// Which week and which month this copy has already reported. It never leaves the computer: what
/// travels is one bit, "this is my first run in that period", and the server adds the bits up.
///
/// Written only after the server has answered. A reported flag that was lost on the way would cost
/// a whole week of counting, and the file is cheaper to write twice than to write too early.
static class ReportedPeriods
{
    private static readonly string Path = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Aiko",
        "reported.json");

    public static Reported Read()
    {
        try
        {
            return File.Exists(Path)
                ? JsonSerializer.Deserialize(File.ReadAllText(Path), ReportedJson.Default.Reported) ?? new Reported()
                : new Reported();
        }
        catch (Exception unreadable) when (unreadable is IOException or UnauthorizedAccessException or JsonException)
        {
            // Nothing remembered means every period looks new. That over-counts by one at worst,
            // and only for a machine whose settings folder cannot be read.
            return new Reported();
        }
    }

    public static void Write(Reported reported)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            File.WriteAllText(Path, JsonSerializer.Serialize(reported, ReportedJson.Default.Reported));
        }
        catch (Exception unwritable) when (unwritable is IOException or UnauthorizedAccessException)
        {
            // The count is not worth an error on the user's screen.
        }
    }

    /// Part of "Reset ID": the periods belong to the identifier that reported them.
    public static void Forget()
    {
        try
        {
            File.Delete(Path);
        }
        catch (Exception undeletable) when (undeletable is IOException or UnauthorizedAccessException)
        {
        }
    }
}

sealed record Reported
{
    public string? Week { get; init; }

    public string? Month { get; init; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(Reported))]
partial class ReportedJson : JsonSerializerContext;
