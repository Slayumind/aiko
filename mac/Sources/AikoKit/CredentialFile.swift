import Foundation

/// What Claude Code keeps in .credentials.json. Parsing only: the file is read by the app,
/// kept in memory for one request and never written anywhere by us.
public struct CredentialFile: Sendable, Equatable, CustomStringConvertible {
    public let accessToken: String
    public let expiresAt: Date
    public let subscriptionType: String

    private init(accessToken: String, expiresAt: Date, subscriptionType: String) {
        self.accessToken = accessToken
        self.expiresAt = expiresAt
        self.subscriptionType = subscriptionType
    }

    public func isExpiredAt(_ now: Date) -> Bool { expiresAt <= now }

    /// Nil when this is not a credential file. `TryParse` with an out parameter on Windows.
    public static func parse(_ json: String?) -> CredentialFile? {
        guard let json, !json.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty,
              let root = JsonNode.parse(json)?.objectValue,
              let oauth = root["claudeAiOauth"]?.objectValue else {
            return nil
        }

        guard let token = oauth["accessToken"]?.stringValue, !token.isEmpty,
              let expires = oauth["expiresAt"]?.int64Value else {
            return nil
        }

        return CredentialFile(
            accessToken: token,
            expiresAt: Date(timeIntervalSince1970: Double(expires) / 1000),
            subscriptionType: oauth["subscriptionType"]?.stringValue ?? "unknown")
    }

    /// Never print the token. Logs, errors and diagnostics all end up calling this.
    public var description: String {
        "Credential(\(subscriptionType), expires \(Iso8601.roundTrip(expiresAt, offsetSeconds: 0)), token hidden)"
    }
}
