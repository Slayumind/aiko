namespace Aiko.Core;

/// Every address Aiko is allowed to open, and the rule that decides it.
///
/// D-005 promised that anything else is cut off rather than merely absent: Aiko reads tokens and
/// paths, and the only way to promise "it goes nowhere" is to leave nowhere for it to go. The rule
/// lives here so it can be tested without a window, and AikoHttp is the one place that opens a
/// connection at all.
///
/// Two hosts, and both are asked for only with the user's consent: the usage API in direct mode,
/// the site for the version check and the count. GitHub is opened in a browser, which is the
/// person's own program, not a request Aiko makes.
public static class AllowedHosts
{
    public const string Anthropic = "api.anthropic.com";

    public const string Site = "slayumind.org";

    public static readonly IReadOnlyList<string> All = [Anthropic, Site];

    /// Exact host, https only.
    ///
    /// Exact, because a suffix test would let `api.anthropic.com.example.net` through, and that is
    /// the classic way such a list is defeated. https only, because the token travels on one of
    /// these connections and a downgrade would put it in the open.
    public static bool Allows(Uri? address) =>
        address is not null
        && address.IsAbsoluteUri
        && address.Scheme == Uri.UriSchemeHttps
        && All.Contains(address.Host, StringComparer.OrdinalIgnoreCase);
}
