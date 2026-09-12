using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using Aiko.Core;

namespace Aiko.App;

public enum AskOutcome
{
    Fine,

    /// The server said too many requests. It says so often on this endpoint, with no delay given.
    TooMany,

    /// No answer at all: no network, a timeout, or an error from the server.
    Failed,
}

public readonly record struct UsageAnswer(AskOutcome Outcome, UsageReport Report, TimeSpan? RetryAfter, string? Problem)
{
    public static UsageAnswer Fine(UsageReport report) => new(AskOutcome.Fine, report, null, null);

    public static UsageAnswer TooMany(TimeSpan? retryAfter) => new(AskOutcome.TooMany, UsageReport.Empty, retryAfter, null);

    public static UsageAnswer Failed(string problem) => new(AskOutcome.Failed, UsageReport.Empty, null, problem);
}

/// Asks the usage API for the limits of one account. Direct mode only, and only with the user's
/// consent: this is the one place in Aiko where a token is used.
///
/// The token is passed in for a single request and never kept here. Nothing about it, and nothing
/// from the answer, is written to the log.
sealed class UsageClient : IDisposable
{
    private static readonly Uri Endpoint = new("https://api.anthropic.com/api/oauth/usage");

    private readonly HttpClient _http;

    public UsageClient()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        // An honest name. Other tools send the name of the official client; we do not pretend to
        // be Claude Code, and the spike proved the server is happy with ours.
        _http.DefaultRequestHeaders.UserAgent.ParseAdd($"Aiko/{Version()}");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        _http.DefaultRequestHeaders.Add("anthropic-beta", "oauth-2025-04-20");
    }

    public async Task<UsageAnswer> AskAsync(string accessToken, CancellationToken cancel)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            using var response = await _http.SendAsync(request, cancel).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                return UsageAnswer.TooMany(response.Headers.RetryAfter?.Delta);
            }

            if (!response.IsSuccessStatusCode)
            {
                // The number, not the body: the body can hold anything and belongs to the account.
                return UsageAnswer.Failed($"the server answered {(int)response.StatusCode}");
            }

            var body = await response.Content.ReadAsStringAsync(cancel).ConfigureAwait(false);
            return UsageAnswer.Fine(UsageReport.FromJson(body));
        }
        catch (TaskCanceledException)
        {
            return UsageAnswer.Failed("the request took too long");
        }
        catch (HttpRequestException)
        {
            return UsageAnswer.Failed("no connection");
        }
    }

    private static string Version() =>
        Assembly.GetEntryAssembly()?.GetName().Version is { } version
            ? $"{version.Major}.{version.Minor}.{version.Build}"
            : "0.0.0";

    public void Dispose() => _http.Dispose();
}
