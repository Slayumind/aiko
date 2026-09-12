using System.Net.Http;
using System.Reflection;
using Aiko.Core;

namespace Aiko.App;

/// Asks slayumind.org which version is the latest.
///
/// The number comes from the site and the files come from GitHub. Asking the site is a connection
/// to our own server, which sees an address and a version, so it only happens with the user's
/// consent: the switch is off until they turn it on.
sealed class UpdateClient : IDisposable
{
    private static readonly Uri Endpoint = new("https://slayumind.org/api/v1/aiko/version");

    private readonly HttpClient _http;

    public UpdateClient()
    {
        // Short: nobody should wait for a version check, and a silent failure is fine here.
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(2.5) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd($"Aiko/{CurrentVersion()}");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    }

    public async Task<UpdateInfo> AskAsync(CancellationToken cancel)
    {
        try
        {
            using var response = await _http.GetAsync(Endpoint, cancel).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                Log.Write($"update check: the site answered {(int)response.StatusCode}");
                return UpdateInfo.Unknown;
            }

            var body = await response.Content.ReadAsStringAsync(cancel).ConfigureAwait(false);
            return UpdateInfo.FromJson(body);
        }
        catch (TaskCanceledException)
        {
            Log.Write("update check: the site took too long");
            return UpdateInfo.Unknown;
        }
        catch (HttpRequestException)
        {
            Log.Write("update check: no connection");
            return UpdateInfo.Unknown;
        }
    }

    public static string CurrentVersion() =>
        Assembly.GetEntryAssembly()?.GetName().Version is { } version
            ? $"{version.Major}.{version.Minor}.{version.Build}"
            : "0.0.0";

    public void Dispose() => _http.Dispose();
}
