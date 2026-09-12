using System.Text.Json;

namespace Aiko.Core;

public enum UpdateState
{
    /// The check has not been made, or it failed. Aiko says so instead of guessing.
    Unknown,

    UpToDate,

    Available,
}

/// What slayumind.org says about the latest version.
///
/// Aiko asks the site rather than GitHub, the same way the other tools of this author do. The
/// files still come from GitHub; only the number comes from the site.
public sealed record UpdateInfo(string? Latest, string? DownloadUrl)
{
    public static readonly UpdateInfo Unknown = new(null, null);

    /// A missing "latest" stays missing.
    ///
    /// The lesson from Sparks: never fill in a fallback version. A client that is told a made up
    /// number decides it is up to date, and every copy in the world goes quiet at once.
    public static UpdateInfo FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Unknown;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return Unknown;
            }

            var latest = Text(root, "latest");
            var download = Text(root, "downloadUrl");

            return latest is null && download is null ? Unknown : new UpdateInfo(latest, download);
        }
        catch (JsonException)
        {
            return Unknown;
        }
    }

    /// Compares as version numbers, not as text: "0.10.0" is newer than "0.9.0", and a string
    /// comparison would say the opposite.
    public UpdateState CompareWith(string currentVersion)
    {
        if (Latest is null
            || !Version.TryParse(Latest, out var latest)
            || !Version.TryParse(currentVersion, out var current))
        {
            return UpdateState.Unknown;
        }

        return latest > current ? UpdateState.Available : UpdateState.UpToDate;
    }

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
