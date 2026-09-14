using System.Text.Json;

namespace Aiko.Core;

public enum ClaudePlan
{
    Unknown,
    Pro,
    Max,
    Team,
    Enterprise,
}

/// Which account a Claude Code folder belongs to, as Claude Code itself wrote it down.
///
/// The facts come from oauthAccount in .claude.json. That object holds names and plan, and no
/// token: the credentials file is never needed for this. The email address is personal, so it is
/// shown only in settings and the wizard and never goes into the log or the diagnostics.
public sealed record ClaudeAccount(ClaudePlan Plan, string? RateLimitTier, string? Email, string? Organization)
{
    public static readonly ClaudeAccount None = new(ClaudePlan.Unknown, null, null, null);

    public bool IsKnown => Plan != ClaudePlan.Unknown || Email is not null;

    /// The plan the way people call it: "Max 5x", "Team". Product names, the same in every language.
    public string PlanLabel => Plan switch
    {
        ClaudePlan.Pro => "Pro",
        ClaudePlan.Max when RateLimitTier?.Contains("max_20x", StringComparison.Ordinal) == true => "Max 20x",
        ClaudePlan.Max when RateLimitTier?.Contains("max_5x", StringComparison.Ordinal) == true => "Max 5x",
        ClaudePlan.Max => "Max",
        ClaudePlan.Team => "Team",
        ClaudePlan.Enterprise => "Enterprise",
        _ => "",
    };

    public static ClaudeAccount FromClaudeJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return None;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("oauthAccount", out var account)
                || account.ValueKind != JsonValueKind.Object)
            {
                return None;
            }

            var plan = PlanFrom(Text(account, "organizationType"));

            // A personal Max plan keeps its tier on the organization, a Team seat keeps one on the
            // user. Only the Max label uses it, so either place is fine.
            var tier = Text(account, "organizationRateLimitTier") ?? Text(account, "userRateLimitTier");

            // A personal plan also has an "organization", named after the email. Only a team's
            // name tells the person something.
            var organization = plan is ClaudePlan.Team or ClaudePlan.Enterprise
                ? Text(account, "organizationName")
                : null;

            return new ClaudeAccount(plan, tier, Text(account, "emailAddress"), organization);
        }
        catch (JsonException)
        {
            return None;
        }
    }

    private static ClaudePlan PlanFrom(string? organizationType) => organizationType switch
    {
        "claude_pro" => ClaudePlan.Pro,
        "claude_max" => ClaudePlan.Max,
        "claude_team" => ClaudePlan.Team,
        "claude_enterprise" => ClaudePlan.Enterprise,
        _ => ClaudePlan.Unknown,
    };

    private static string? Text(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = value.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}
