using Aiko.Core;

namespace Aiko.Core.Tests;

public class ClaudeAccountTests
{
    // The shape of oauthAccount on this machine, with made-up names and addresses.
    private const string PersonalMax = """
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
        """;

    private const string TeamSeat = """
        {
          "oauthAccount": {
            "emailAddress": "me@work.example", "organizationName": "Acme",
            "organizationType": "claude_team", "seatTier": "team_tier_1",
            "organizationRateLimitTier": "default_raven", "userRateLimitTier": "default_claude_max_5x"
          }
        }
        """;

    [Fact]
    public void A_personal_max_plan_shows_its_tier()
    {
        var account = ClaudeAccount.FromClaudeJson(PersonalMax);

        Assert.Equal(ClaudePlan.Max, account.Plan);
        Assert.Equal("Max 5x", account.PlanLabel);
        Assert.Equal("me@example.com", account.Email);
    }

    [Fact]
    public void A_personal_plan_has_no_organization_worth_showing()
    {
        Assert.Null(ClaudeAccount.FromClaudeJson(PersonalMax).Organization);
    }

    [Fact]
    public void A_team_seat_is_team_whatever_its_own_tier_says()
    {
        var account = ClaudeAccount.FromClaudeJson(TeamSeat);

        Assert.Equal("Team", account.PlanLabel);
        Assert.Equal("Acme", account.Organization);
    }

    [Theory]
    [InlineData("claude_pro", null, "Pro")]
    [InlineData("claude_max", "default_claude_max_20x", "Max 20x")]
    [InlineData("claude_max", null, "Max")]
    [InlineData("claude_enterprise", null, "Enterprise")]
    [InlineData("something_new", null, "")]
    public void Plan_labels(string type, string? tier, string label)
    {
        var tierJson = tier is null ? "null" : $"\"{tier}\"";
        var json = $$"""{ "oauthAccount": { "organizationType": "{{type}}", "organizationRateLimitTier": {{tierJson}} } }""";

        Assert.Equal(label, ClaudeAccount.FromClaudeJson(json).PlanLabel);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{ "userID": "0000" }""")]
    [InlineData("""{ "oauthAccount": "text" }""")]
    public void Nothing_readable_is_no_account(string? json)
    {
        var account = ClaudeAccount.FromClaudeJson(json);

        Assert.False(account.IsKnown);
        Assert.Equal("", account.PlanLabel);
    }

    [Fact]
    public void The_default_folder_reads_the_home_file_first()
    {
        var home = @"C:\Users\someone";

        Assert.Equal(
            [@"C:\Users\someone\.claude.json", @"C:\Users\someone\.claude\.claude.json"],
            ClaudeConfigFolder.AccountFileCandidates(@"C:\Users\someone\.claude\", home));
    }

    [Fact]
    public void Any_other_folder_reads_its_own_file()
    {
        Assert.Equal(
            [@"C:\Users\someone\.claude-personal\.claude.json"],
            ClaudeConfigFolder.AccountFileCandidates(@"C:\Users\someone\.claude-personal", @"C:\Users\someone"));
    }
}
