import Foundation

/// Every address Aiko is allowed to open, and the rule that decides it.
///
/// D-005 promised that anything else is cut off rather than merely absent: Aiko reads tokens and
/// paths, and the only way to promise "it goes nowhere" is to leave nowhere for it to go. The rule
/// lives here so it can be tested without a window, and the one place that opens a connection at
/// all asks it first.
///
/// Every one of them is asked for only with the user's consent: the usage API in direct mode, the
/// site for the version check and the count, GitHub for the files of an update the user asked for.
///
/// GitHub is three hosts because that is how it hands a file over: the API lists what a release
/// has, `github.com` takes the download and sends the reader on to the file store. A redirect is
/// checked again against this list before it is followed.
public enum AllowedHosts {
    public static let anthropic = "api.anthropic.com"

    public static let site = "slayumind.org"

    public static let gitHubApi = "api.github.com"

    public static let gitHub = "github.com"

    /// Where a release file really comes from. The older name is still in use for some releases,
    /// so both are here.
    public static let gitHubFiles = "release-assets.githubusercontent.com"

    public static let gitHubObjects = "objects.githubusercontent.com"

    public static let all = [anthropic, site, gitHubApi, gitHub, gitHubFiles, gitHubObjects]

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
