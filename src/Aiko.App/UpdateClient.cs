using System.Net.Http;
using System.Reflection;
using Aiko.Core;

namespace Aiko.App;

/// Asks slayumind.org which version is the latest, and says "one copy of Aiko ran today" while it
/// is there.
///
/// The number comes from the site and the files come from GitHub. Asking the site is a connection
/// to our own server, so it only happens with the user's consent: the switch is off until they
/// turn it on, and turning it off turns off the counting too. One switch, because one request
/// does both, and a switch that quietly leaves half of itself running is a lie.
///
/// What goes along with the question is the version, the Windows version, and an identifier that
/// changes every day. Nothing else.
sealed class UpdateClient : IDisposable
{
    private const string Endpoint = "https://slayumind.org/api/v1/aiko/version";

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
            using var response = await _http.GetAsync(Address(), cancel).ConfigureAwait(false);
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

    /// The question, and the three things that ride with it.
    ///
    /// The day is the computer's own: somebody's Tuesday is their Tuesday, and a server that had
    /// to work it out from an address would be looking at the address.
    private static Uri Address()
    {
        var version = Uri.EscapeDataString(CurrentVersion());
        var windows = Uri.EscapeDataString(Environment.OSVersion.Version.ToString());
        var today = Heartbeat.DailyId(InstallId.Current(), DateOnly.FromDateTime(DateTime.Now));

        return today.Length == 0
            ? new Uri($"{Endpoint}?v={version}&os={windows}")
            : new Uri($"{Endpoint}?v={version}&os={windows}&day={today}");
    }

    public static string CurrentVersion() =>
        Assembly.GetEntryAssembly()?.GetName().Version is { } version
            ? $"{version.Major}.{version.Minor}.{version.Build}"
            : "0.0.0";

    public void Dispose() => _http.Dispose();
}
