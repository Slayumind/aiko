import Foundation

public enum ClaudePlan: Sendable, Equatable {
    case unknown
    case pro
    case max
    case team
    case enterprise
}

/// Which account a Claude Code folder belongs to, as Claude Code itself wrote it down.
///
/// The facts come from oauthAccount in .claude.json. That object holds names and plan, and no
/// token: the credentials file is never needed for this. The email address is personal, so it is
/// shown only in settings and the wizard and never goes into the log or the diagnostics.
public struct ClaudeAccount: Sendable, Equatable {
    public let plan: ClaudePlan
    public let rateLimitTier: String?
    public let email: String?
    public let organization: String?

    public init(plan: ClaudePlan, rateLimitTier: String?, email: String?, organization: String?) {
        self.plan = plan
        self.rateLimitTier = rateLimitTier
        self.email = email
        self.organization = organization
    }

    public static let none = ClaudeAccount(plan: .unknown, rateLimitTier: nil, email: nil, organization: nil)

    public var isKnown: Bool { plan != .unknown || email != nil }

    /// The plan the way people call it: "Max 5x", "Team". Product names, the same in every language.
    public var planLabel: String {
        switch plan {
        case .pro:
            return "Pro"
        case .max:
            if rateLimitTier?.contains("max_20x") == true { return "Max 20x" }
            if rateLimitTier?.contains("max_5x") == true { return "Max 5x" }
            return "Max"
        case .team:
            return "Team"
        case .enterprise:
            return "Enterprise"
        case .unknown:
            return ""
        }
    }

    public static func fromClaudeJson(_ json: String?) -> ClaudeAccount {
        guard let json, !json.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty,
              let root = JsonNode.parse(json)?.objectValue,
              let account = root["oauthAccount"]?.objectValue else {
            return none
        }

        let plan = planFrom(text(account, "organizationType"))

        // A personal Max plan keeps its tier on the organization, a Team seat keeps one on the
        // user. Only the Max label uses it, so either place is fine.
        let tier = text(account, "organizationRateLimitTier") ?? text(account, "userRateLimitTier")

        // A personal plan also has an "organization", named after the email. Only a team's
        // name tells the person something.
        let organization = plan == .team || plan == .enterprise ? text(account, "organizationName") : nil

        return ClaudeAccount(
            plan: plan,
            rateLimitTier: tier,
            email: text(account, "emailAddress"),
            organization: organization)
    }

    private static func planFrom(_ organizationType: String?) -> ClaudePlan {
        switch organizationType {
        case "claude_pro": return .pro
        case "claude_max": return .max
        case "claude_team": return .team
        case "claude_enterprise": return .enterprise
        default: return .unknown
        }
    }

    private static func text(_ element: JsonObject, _ name: String) -> String? {
        guard let text = element[name]?.stringValue,
              !text.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else {
            return nil
        }
        return text
    }
}
