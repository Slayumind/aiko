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

    /// What Claude Code 2.1.301 named on 2026-10-26, cut down: "default", four short names and
    /// full ids of single versions.
    private static readonly ClaudeModel[] Named =
    [
        new("default", "Default (recommended)"),
        new("opus", "Opus 5.5"),
        new("fable", "Fable 5.1"),
        new("sonnet", "Sonnet 5.5"),
        new("haiku", "Haiku 5.5"),
        new("claude-haiku-4-5-20251001", "Haiku 4.5"),
        new("claude-opus-4-8", "Opus 4.8"),
    ];

    [Fact]
    public void The_form_offers_the_short_names_Claude_Code_named_in_its_order() =>
        Assert.Equal(
            ["Opus 5.5", "Fable 5.1", "Sonnet 5.5", "Haiku 5.5"],
            NewSessionChoices.Offered(Named).Select(model => model.DisplayName));

    [Fact]
    public void Without_models_from_Claude_Code_the_form_keeps_its_own_three() =>
        Assert.Equal(["opus", "sonnet", "haiku"], NewSessionChoices.Offered([]).Select(model => model.Value));

    [Theory]
    [InlineData("opus", "fable")]
    [InlineData("claude-fable-5-1", "sonnet")]
    [InlineData("haiku", "opus")]
    public void A_click_goes_through_the_models_Claude_Code_named(string model, string next) =>
        Assert.Equal(next, NewSessionChoices.NextModel(model, [.. NewSessionChoices.Offered(Named).Select(offered => offered.Value)]));

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
