using Aiko.Core.Terminal;

namespace Aiko.Core.Tests;

public class ModelSwitchTests
{
    private const string Switched =
        """{"type":"user","message":{"role":"user","content":"<local-command-stdout>Set model to `sonnet (claude-sonnet-5-5)`</local-command-stdout>"}}""";

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
    public void A_new_model_is_asked_and_goes_to_Claude_Code()
    {
        var running = Ready("claude-opus-5-5");

        var outcome = running.SetModel("haiku");

        Assert.Equal("haiku", outcome.State.AskedModel);
        Assert.Equal("claude-opus-5-5", outcome.State.Model);
        Assert.Equal("haiku", outcome.State.LatestModel);
        var line = Assert.Single(outcome.Written);
        Assert.Contains("\"subtype\":\"set_model\"", line, StringComparison.Ordinal);
        Assert.Contains("\"model\":\"haiku\"", line, StringComparison.Ordinal);
    }

    [Fact]
    public void Claude_Code_names_the_full_id_after_a_change()
    {
        var asked = Ready("claude-opus-5-5").SetModel("sonnet").State;

        var named = asked.On(StreamEvent.FromLine(Switched)).State;

        Assert.Equal("claude-sonnet-5-5", named.Model);
        Assert.Equal("", named.AskedModel);
        Assert.Equal(asked.Items, named.Items);
    }

    [Fact]
    public void The_next_start_names_the_full_id_of_an_asked_model()
    {
        var asked = Conversation.Empty().SetModel("haiku").State;

        var named = asked.On(new StreamEvent.Ready("s1", "claude-haiku-5-5", null, "")).State;

        Assert.Equal("claude-haiku-5-5", named.Model);
        Assert.Equal("", named.AskedModel);
    }

    [Fact]
    public void A_start_without_a_model_keeps_the_asked_one()
    {
        var asked = Conversation.Empty().SetModel("haiku").State;

        var next = asked.On(new StreamEvent.Ready("s1", "", null, "")).State;

        Assert.Equal("haiku", next.AskedModel);
    }

    [Fact]
    public void The_form_starts_with_the_family_of_a_kept_full_id()
    {
        var sessions = TerminalSessions.Empty.Add("a", "Work", "C:/work", "a", "claude-sonnet-5-5", PermissionMode.Manual, 10);

        Assert.Equal("sonnet", NewSessionChoices.DefaultModel(sessions, "Work"));
    }

    private static Conversation Ready(string model) =>
        Conversation.Empty().On(new StreamEvent.Ready("s1", model, null, "")).State;
}
