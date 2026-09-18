import Foundation

/// Every address Aiko is allowed to open, and the rule that decides it.
///
/// D-005 promised that anything else is cut off rather than merely absent: Aiko reads tokens and
/// paths, and the only way to promise "it goes nowhere" is to leave nowhere for it to go. The rule
/// lives here so it can be tested without a window, and the one place that opens a connection at
/// all asks it first.
///
/// Two hosts, and both are asked for only with the user's consent: the usage API in direct mode,
/// the site for the version check and the count. GitHub is opened in a browser, which is the
/// person's own program, not a request Aiko makes.
public enum AllowedHosts {
    public static let anthropic = "api.anthropic.com"

    public static let site = "slayumind.org"

    public static let all = [anthropic, site]

    /// Exact host, https only.
    ///
    /// Exact, because a suffix test would let `api.anthropic.com.example.net` through, and that is
    /// the classic way such a list is defeated. https only, because the token travels on one of
    /// these connections and a downgrade would put it in the open.
    public static func allows(_ address: URL?) -> Bool {
        guard let address,
              let scheme = address.scheme?.lowercased(), scheme == "https",
              let host = address.host else {
            return false
        }

        return all.contains { $0.caseInsensitiveCompare(host) == .orderedSame }
    }
}
