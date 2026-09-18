import Testing

@testable import AikoKit

struct ClaudeAccountTests {
    // The shape of oauthAccount on this machine, with made-up names and addresses.
    static let personalMax = """
        {
          "userID": "0000",
          "oauthAccount": {
            "accountUuid": "a", "emailAddress": "me@example.com", "organizationUuid": "o",
            "billingType": "stripe_subscription", "seatTier": null, "displayName": "Me",
            "organizationRole": "admin", "organizationName": "me@example.com's Organization",
            "organizationType": "claude_max", "organizationRateLimitTier": "default_claude_max_5x",
            "userRateLimitTier": null
          }
        }
        """

    static let teamSeat = """
        {
          "oauthAccount": {
            "emailAddress": "me@work.example", "organizationName": "Acme",
            "organizationType": "claude_team", "seatTier": "team_tier_1",
            "organizationRateLimitTier": "default_raven", "userRateLimitTier": "default_claude_max_5x"
          }
        }
        """

    @Test
    func aPersonalMaxPlanShowsItsTier() {
        let account = ClaudeAccount.fromClaudeJson(Self.personalMax)

        #expect(account.plan == .max)
        #expect(account.planLabel == "Max 5x")
        #expect(account.email == "me@example.com")
    }

    @Test
    func aPersonalPlanHasNoOrganizationWorthShowing() {
        #expect(ClaudeAccount.fromClaudeJson(Self.personalMax).organization == nil)
    }

    @Test
    func aTeamSeatIsTeamWhateverItsOwnTierSays() {
        let account = ClaudeAccount.fromClaudeJson(Self.teamSeat)

        #expect(account.planLabel == "Team")
        #expect(account.organization == "Acme")
    }

    @Test(arguments: [
        ("claude_pro", nil, "Pro"),
        ("claude_max", "default_claude_max_20x", "Max 20x"),
        ("claude_max", nil, "Max"),
        ("claude_enterprise", nil, "Enterprise"),
        ("something_new", nil, ""),
    ] as [(String, String?, String)])
    func planLabels(type: String, tier: String?, label: String) {
        let tierJson = tier == nil ? "null" : "\"\(tier!)\""
        let json = #"{ "oauthAccount": { "organizationType": "\#(type)", "organizationRateLimitTier": \#(tierJson) } }"#

        #expect(ClaudeAccount.fromClaudeJson(json).planLabel == label)
    }

    @Test(arguments: [nil, "", "not json", "[]", #"{ "userID": "0000" }"#, #"{ "oauthAccount": "text" }"#] as [String?])
    func nothingReadableIsNoAccount(json: String?) {
        let account = ClaudeAccount.fromClaudeJson(json)

        #expect(!account.isKnown)
        #expect(account.planLabel == "")
    }
}
