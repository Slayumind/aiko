using System.Text.Json;

namespace Aiko.Core;

/// What a new version brings, as the General page lists it under "a newer version is out" (D-397).
///
/// The notes of a release on GitHub hold both languages: the English part of CHANGELOG.md, a line
/// that says the Russian part starts, and the part of CHANGELOG.ru.md under the same heading. A
/// point of the notes starts with its title in bold; the list shows the titles only, in order.
public static class ReleaseNotes
{
    /// The line between the two languages in the notes of a release.
    public const string RussianMark = "<!-- ru -->";

    /// More than this reads as a changelog, not as "what is new".
    public const int MostTitles = 8;

    /// The titles from the JSON GitHub gives for a release, in the language asked for. Nothing when
    /// the answer has no notes.
    public static IReadOnlyList<string> FromGitHubJson(string json, bool russian)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("body", out var body)
                && body.ValueKind == JsonValueKind.String
                ? Titles(body.GetString() ?? string.Empty, russian)
                : [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// The titles of the points in one language. Notes without a Russian part give the English
    /// titles in Russian too: an English list is better than none.
    public static IReadOnlyList<string> Titles(string notes, bool russian)
    {
        var at = notes.IndexOf(RussianMark, StringComparison.Ordinal);
        var part = at < 0 ? notes : russian ? notes[(at + RussianMark.Length)..] : notes[..at];

        var titles = new List<string>();
        foreach (var raw in part.Split('\n'))
        {
            var line = raw.Trim();
            if (!line.StartsWith("- **", StringComparison.Ordinal))
            {
                continue;
            }

            var end = line.IndexOf("**", 4, StringComparison.Ordinal);
            if (end < 0)
            {
                continue;
            }

            var title = line[4..end].Trim().TrimEnd('.').Trim();
            if (title.Length > 0)
            {
                titles.Add(title);
            }

            if (titles.Count == MostTitles)
            {
                break;
            }
        }

        return titles;
    }
}
