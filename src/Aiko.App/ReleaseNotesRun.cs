using System.Globalization;
using System.Net.Http;
using Aiko.Core;

namespace Aiko.App;

/// What a newer version brings, from the notes of its release on GitHub (D-397). Asked only once a
/// newer version is known and update checks are on, through the same guard of allowed hosts as the
/// files of an update. The request carries nothing about the person.
static class ReleaseNotesRun
{
    private const string Releases = "https://api.github.com/repos/Slayumind/aiko/releases/tags/v";

    /// The titles already read, by version and language: the page opens many times a day.
    private static readonly Dictionary<string, IReadOnlyList<string>> Known = [];

    public static async Task<IReadOnlyList<string>> ForAsync(string version, CancellationToken cancel)
    {
        var russian = (Strings.Culture ?? CultureInfo.CurrentUICulture).TwoLetterISOLanguageName == "ru";
        var key = version + (russian ? "/ru" : "/en");
        if (Known.TryGetValue(key, out var known))
        {
            return known;
        }

        using var http = AikoHttp.Client(TimeSpan.FromSeconds(5));
        try
        {
            using var response = await http.GetAsync(Releases + Uri.EscapeDataString(version), cancel).ConfigureAwait(true);
            if (!response.IsSuccessStatusCode)
            {
                Log.Write($"release notes: GitHub answered {(int)response.StatusCode}");
                return [];
            }

            var titles = ReleaseNotes.FromGitHubJson(await response.Content.ReadAsStringAsync(cancel).ConfigureAwait(true), russian);
            Known[key] = titles;
            return titles;
        }
        catch (Exception failed) when (failed is HttpRequestException or TaskCanceledException)
        {
            // No list is the honest answer: the line "a newer version is out" stands without it.
            Log.Write($"release notes: {failed.GetType().Name}");
            return [];
        }
    }
}
