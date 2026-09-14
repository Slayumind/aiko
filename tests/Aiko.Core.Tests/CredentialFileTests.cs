namespace Aiko.Core.Tests;

public class CredentialFileTests
{
    private const string Token = "sk-ant-oat01-example-token-value-for-tests-only";

    private static readonly string Valid = $$"""
        {
          "claudeAiOauth": {
            "accessToken": "{{Token}}",
            "refreshToken": "sk-ant-ort01-refresh-we-never-touch",
            "expiresAt": 1789200000000,
            "subscriptionType": "max"
          },
          "mcpOAuth": { "some-server": { "accessToken": "not ours" } }
        }
        """;

    [Fact]
    public void Reads_the_token_the_expiry_and_the_plan()
    {
        Assert.True(CredentialFile.TryParse(Valid, out var credential));

        Assert.Equal(Token, credential!.AccessToken);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1789200000000), credential.ExpiresAt);
        Assert.Equal("max", credential.SubscriptionType);
    }

    [Fact]
    public void Knows_when_the_token_has_expired()
    {
        CredentialFile.TryParse(Valid, out var credential);

        Assert.True(credential!.IsExpiredAt(credential.ExpiresAt.AddSeconds(1)));
        Assert.False(credential.IsExpiredAt(credential.ExpiresAt.AddSeconds(-1)));
    }

    [Fact]
    public void The_token_never_appears_in_text()
    {
        CredentialFile.TryParse(Valid, out var credential);

        var text = credential!.ToString();

        Assert.DoesNotContain(Token, text);
        Assert.Contains("hidden", text);
    }

    [Fact]
    public void A_missing_plan_reads_as_unknown()
    {
        var json = """
            { "claudeAiOauth": { "accessToken": "abc", "expiresAt": 1789200000000 } }
            """;

        Assert.True(CredentialFile.TryParse(json, out var credential));
        Assert.Equal("unknown", credential!.SubscriptionType);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("{ \"claudeAiOauth\": {} }")]
    [InlineData("{ \"claudeAiOauth\": { \"accessToken\": \"\", \"expiresAt\": 1 } }")]
    [InlineData("{ \"claudeAiOauth\": { \"accessToken\": \"abc\" } }")]
    public void Anything_else_is_simply_not_a_credential_file(string json)
    {
        Assert.False(CredentialFile.TryParse(json, out var credential));
        Assert.Null(credential);
    }
}
