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
    public static HttpClient Client(TimeSpan timeout)
    {
        var client = new HttpClient(new OnlyAllowedHosts(new HttpClientHandler())) { Timeout = timeout };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"Aiko/{UpdateClient.CurrentVersion()}");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        return client;
    }
}

/// Refuses a request to anywhere Aiko has no business being.
///
/// It throws rather than returning an error status: a blocked address is a mistake in our own
/// code, and a mistake that answers "404" is a mistake somebody debugs for an hour. The message
/// names the host but nothing else — a query string can hold the daily identifier.
sealed class OnlyAllowedHosts(HttpMessageHandler inner) : DelegatingHandler(inner)
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancel)
    {
        if (!AllowedHosts.Allows(request.RequestUri))
        {
            var host = request.RequestUri?.Host ?? "an address with no host";
            throw new InvalidOperationException($"Aiko does not open {host}. Allowed: {string.Join(", ", AllowedHosts.All)}.");
        }

        return base.SendAsync(request, cancel);
    }
}
