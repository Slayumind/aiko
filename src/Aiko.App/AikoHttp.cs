using System.Net;
using System.Net.Http;
using Aiko.Core;

namespace Aiko.App;

/// The only place in Aiko that makes an HTTP client.
///
/// D-005 said unwanted hosts are cut off, not just unused. Until 0.2.1 that was not true: both
/// clients were plain `new HttpClient()` reaching hard-coded addresses, so the promise rested on
/// nobody ever adding a third one by accident. Now every client goes through here and carries a
/// handler that refuses anything outside AllowedHosts.
///
/// A handler alone would not be enough — `new HttpClient()` is one line and the next one would be
/// written the same way. What makes this hold is the test beside it: it reads the source of the
/// app and fails when a client is made anywhere but here.
static class AikoHttp
{
    /// For a question with a JSON answer: the version check and the usage API.
    public static HttpClient Client(TimeSpan timeout)
    {
        var client = Plain(timeout);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        return client;
    }

    /// For a file: the parts of an update. The same guard, and no Accept for JSON, because what
    /// comes back is a zip or an installer.
    public static HttpClient Plain(TimeSpan timeout)
    {
        // Redirects are followed by our own handler instead of by this one, so that every hop is
        // checked against the list. GitHub hands a release file over in two hops.
        var inner = new HttpClientHandler { AllowAutoRedirect = false };
        var client = new HttpClient(new OnlyAllowedHosts(inner)) { Timeout = timeout };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"Aiko/{UpdateClient.CurrentVersion()}");
        return client;
    }
}

/// Refuses a request to anywhere Aiko has no business being, and follows redirects itself so that
/// the host of every hop is checked too.
///
/// It throws rather than returning an error status: a blocked address is a mistake in our own
/// code, and a mistake that answers "404" is a mistake somebody debugs for an hour. The message
/// names the host but nothing else — a query string can hold the daily identifier.
sealed class OnlyAllowedHosts(HttpMessageHandler inner) : DelegatingHandler(inner)
{
    /// GitHub takes two. More than this is a loop, not a download.
    private const int MaxHops = 5;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancel)
    {
        var current = request;

        for (var hop = 0; ; hop++)
        {
            if (!AllowedHosts.Allows(current.RequestUri))
            {
                var host = current.RequestUri?.Host ?? "an address with no host";
                throw new InvalidOperationException(
                    $"Aiko does not open {host}. Allowed: {string.Join(", ", AllowedHosts.All)}.");
            }

            var response = await base.SendAsync(current, cancel).ConfigureAwait(false);
            if (hop == MaxHops || !IsRedirect(response.StatusCode) || response.Headers.Location is null)
            {
                return response;
            }

            var next = new Uri(current.RequestUri!, response.Headers.Location);
            response.Dispose();
            current = Follow(current, next);
        }
    }

    private static bool IsRedirect(HttpStatusCode status) =>
        status is HttpStatusCode.MovedPermanently or HttpStatusCode.Found or HttpStatusCode.SeeOther
            or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;

    /// Aiko only ever asks for things, so the next request is the same GET at the new address.
    /// Headers that name the caller travel on; nothing carries a body.
    private static HttpRequestMessage Follow(HttpRequestMessage previous, Uri next)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, next);
        foreach (var header in previous.Headers)
        {
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return request;
    }
}
