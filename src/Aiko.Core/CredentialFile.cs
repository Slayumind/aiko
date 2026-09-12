using System.Text.Json;

namespace Aiko.Core;

/// What Claude Code keeps in .credentials.json. Parsing only: the file is read by the app,
/// kept in memory for one request and never written anywhere by us.
public sealed class CredentialFile
{
    private CredentialFile(string accessToken, DateTimeOffset expiresAt, string subscriptionType)
    {
        AccessToken = accessToken;
        ExpiresAt = expiresAt;
        SubscriptionType = subscriptionType;
    }

    public string AccessToken { get; }
    public DateTimeOffset ExpiresAt { get; }
    public string SubscriptionType { get; }

    public bool IsExpiredAt(DateTimeOffset now) => ExpiresAt <= now;

    public static bool TryParse(string json, out CredentialFile? credential)
    {
        credential = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("claudeAiOauth", out var oauth)
                || oauth.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!oauth.TryGetProperty("accessToken", out var token) || token.ValueKind != JsonValueKind.String
                || !oauth.TryGetProperty("expiresAt", out var expires) || expires.ValueKind != JsonValueKind.Number)
            {
                return false;
            }

            var accessToken = token.GetString();
            if (string.IsNullOrEmpty(accessToken))
            {
                return false;
            }

            var subscription = oauth.TryGetProperty("subscriptionType", out var type) && type.ValueKind == JsonValueKind.String
                ? type.GetString() ?? "unknown"
                : "unknown";

            credential = new CredentialFile(
                accessToken,
                DateTimeOffset.FromUnixTimeMilliseconds(expires.GetInt64()),
                subscription);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// Never print the token. Logs, exceptions and diagnostics all end up calling this.
    public override string ToString() => $"Credential({SubscriptionType}, expires {ExpiresAt:u}, token hidden)";
}
