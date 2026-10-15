using Aiko.Core.Terminal;

namespace Aiko.Core.Tests;

public class ModelSwitchTests
{
    [Theory]
    [InlineData("opus", "sonnet")]
    [InlineData("sonnet", "haiku")]
    [InlineData("haiku", "opus")]
    [InlineData("claude-sonnet-5-5", "haiku")]
    [InlineData("claude-haiku-5-5-20261001", "opus")]
    [InlineData("some-other-model", "opus")]
    [InlineData("", "opus")]
    [InlineData(null, "opus")]
    public void A_click_on_the_model_takes_the_next_one(string? model, string next) =>
        Assert.Equal(next, NewSessionChoices.NextModel(model));

    [Fact]
    public void A_new_model_shows_at_once_and_goes_to_Claude_Code()
    {
        var outcome = Conversation.Empty().SetModel("haiku");

        Assert.Equal("haiku", outcome.State.Model);
        var line = Assert.Single(outcome.Written);
        Assert.Contains("\"subtype\":\"set_model\"", line, StringComparison.Ordinal);
        Assert.Contains("\"model\":\"haiku\"", line, StringComparison.Ordinal);
    }
}
