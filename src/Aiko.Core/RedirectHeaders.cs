namespace Aiko.Core;

/// Which headers of a request go on to the address a redirect points to (D-420). A token goes only to
/// the host it was meant for (CLAUDE.md, "Лимиты и безопасность токенов"), so the headers that carry
/// a secret stay behind when the host changes. The rest name the caller and travel on.
public static class RedirectHeaders
{
    private static readonly string[] Secret = ["Authorization", "Proxy-Authorization", "Cookie"];

    public static bool Keeps(string header, Uri from, Uri to) =>
        SameHost(from, to) || !Secret.Contains(header, StringComparer.OrdinalIgnoreCase);

    private static bool SameHost(Uri from, Uri to) =>
        string.Equals(from.Scheme, to.Scheme, StringComparison.OrdinalIgnoreCase)
        && string.Equals(from.IdnHost, to.IdnHost, StringComparison.OrdinalIgnoreCase)
        && from.Port == to.Port;
}
