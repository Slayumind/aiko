using System.Text.Json.Nodes;

namespace Aiko.Core.Tests;

/// Taking Aiko's plugins out of a Claude Code folder when Aiko, an environment or the whole setup
/// goes (D-205).
public class PluginRemovalTests
{
    private const string Folder = @"C:\Users\someone\.claude";
    private const string Persona = "aiko-persona@aiko";

    private static string Settings => ClaudeSettingsEditor.PathIn(Folder);

    private static string Backup => ClaudeSettingsEditor.BackupPathIn(Folder);

    private const string WithPlugins = """
        {
          "model": "opus",
          "enabledPlugins": { "aiko-persona@aiko": true, "aiko-copy@aiko": false, "frontend-design@claude-plugins-official": true },
          "extraKnownMarketplaces": {
            "aiko": { "source": { "source": "directory", "path": "C:\\m" } },
            "team": { "source": { "source": "github", "repo": "team/plugins" } }
          },
          "outputStyle": "Explanatory"
        }
        """;

    // ---- the JSON ----

    [Fact]
    public void Only_our_plugins_and_marketplace_leave_the_file()
    {
        Assert.True(SettingsJsonPatch.TryRemovePlugins(WithPlugins, out var restored));

        var root = JsonNode.Parse(restored)!.AsObject();
        Assert.Equal(["frontend-design@claude-plugins-official"], root["enabledPlugins"]!.AsObject().Select(p => p.Key));
        Assert.Equal(["team"], root["extraKnownMarketplaces"]!.AsObject().Select(p => p.Key));
        Assert.Equal("opus", root["model"]!.GetValue<string>());
        Assert.Equal("Explanatory", root["outputStyle"]!.GetValue<string>());
    }

    [Fact]
    public void Keys_left_empty_by_the_removal_go_too()
    {
        var json = """{ "enabledPlugins": { "aiko-persona@aiko": true }, "extraKnownMarketplaces": { "aiko": {} } }""";

        Assert.True(SettingsJsonPatch.TryRemovePlugins(json, out var restored));

        Assert.Empty(JsonNode.Parse(restored)!.AsObject());
    }

    [Theory]
    [InlineData("""{ "enabledPlugins": { "frontend-design@claude-plugins-official": true } }""")]
    [InlineData("""{ "enabledPlugins": {} }""")]
    [InlineData("""{ "enabledPlugins": [1] }""")]
    [InlineData("not json")]
    public void A_file_without_our_plugins_is_left_alone(string json)
    {
        Assert.False(SettingsJsonPatch.TryRemovePlugins(json, out var restored));
        Assert.Equal(json, restored);
    }

    [Fact]
    public void The_empty_keys_the_uninstall_command_writes_back_are_dropped()
    {
        Assert.True(SettingsJsonPatch.TryDropEmptyPluginKeys("""{ "model": "opus", "enabledPlugins": {} }""", out var tidy));
        Assert.Equal(["model"], JsonNode.Parse(tidy)!.AsObject().Select(p => p.Key));

        Assert.False(SettingsJsonPatch.TryDropEmptyPluginKeys("""{ "enabledPlugins": { "x@y": true } }""", out _));
    }

    [Theory]
    [InlineData("""{ "enabledPlugins": { "aiko-copy@aiko": false } }""", true)]
    [InlineData("""{ "extraKnownMarketplaces": { "aiko": {} } }""", true)]
    [InlineData("""{ "aikoWrappedStatusLine": { "command": "mine.sh" } }""", true)]
    [InlineData("""{ "enabledPlugins": { "frontend-design@claude-plugins-official": true } }""", false)]
    [InlineData("""{ "statusLine": { "type": "command", "command": "mine.sh" } }""", false)]
    [InlineData("{}", false)]
    public void Anything_of_ours_counts_as_Aiko_being_in_the_file(string json, bool ours)
    {
        Assert.Equal(ours, SettingsJsonPatch.HasAnyAikoEntries(json));
    }

    // ---- the file and its copy ----

    [Fact]
    public void Removal_takes_the_plugins_out_with_the_status_line()
    {
        var files = new FakeFiles().With(Settings, WithPlugins);
        var editor = new ClaudeSettingsEditor(files);
        editor.Add(Folder, BridgeCommand.For(@"C:\a\Aiko.Bridge.exe"));

        Assert.True(editor.Remove(Folder).Changed);

        var text = files.Read(Settings);
        Assert.DoesNotContain("@aiko", text);
        Assert.DoesNotContain(SettingsJsonPatch.StatusLineKey, text);
        Assert.False(files.Has(Backup));
    }

    [Fact]
    public void A_folder_with_only_plugins_of_ours_is_cleaned_too()
    {
        var files = new FakeFiles().With(Settings, WithPlugins);

        Assert.True(new ClaudeSettingsEditor(files).Remove(Folder).Changed);
        Assert.DoesNotContain("@aiko", files.Read(Settings));
    }

    [Fact]
    public void Tidying_after_the_command_keeps_the_file_and_the_copy()
    {
        var files = new FakeFiles().With(Settings, """{ "enabledPlugins": {} }""").With(Backup, "{}");

        Assert.True(new ClaudeSettingsEditor(files).TidyAfterPluginRemoval(Folder).Changed);

        Assert.True(files.Has(Settings));
        Assert.True(files.Has(Backup));
        Assert.Empty(JsonNode.Parse(files.Read(Settings))!.AsObject());
    }

    // ---- the steps ----

    [Fact]
    public void Removal_uninstalls_every_plugin_of_ours_then_the_marketplace()
    {
        var state = new PluginState(@"C:\m", new HashSet<string> { Persona, "aiko-copy@aiko" }, new HashSet<string>());

        Assert.Equal(
            [
                new PluginStep(PluginStepKind.Uninstall, "aiko-copy@aiko"),
                new PluginStep(PluginStepKind.Uninstall, Persona),
                new PluginStep(PluginStepKind.RemoveMarketplace, "aiko"),
            ],
            PluginPlan.Removal(state));
        Assert.Equal(["plugin", "uninstall", Persona, "--scope", "user"], new PluginStep(PluginStepKind.Uninstall, Persona).Arguments);
    }

    [Fact]
    public void A_folder_with_nothing_of_ours_needs_no_steps()
    {
        Assert.Empty(PluginPlan.Removal(PluginState.Nothing));
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class SlowCli(Clock clock, TimeSpan each) : IClaudeCli
    {
        public List<TimeSpan> Timeouts { get; } = [];

        public CliResult Run(string configDirectory, IReadOnlyList<string> arguments, TimeSpan timeout)
        {
            Timeouts.Add(timeout);
            clock.Now += each;
            return new CliResult(0, TimedOut: false);
        }
    }

    [Fact]
    public void No_step_starts_after_the_deadline_and_none_gets_more_time_than_is_left()
    {
        var clock = new Clock();
        var cli = new SlowCli(clock, TimeSpan.FromSeconds(8));
        var steps = Enumerable.Repeat(new PluginStep(PluginStepKind.Uninstall, Persona), 4).ToList();

        var results = PluginReconciler.Apply(cli, Folder, steps, clock, clock.Now + TimeSpan.FromSeconds(20));

        Assert.Equal(3, results.Count);
        Assert.Equal([TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(12), TimeSpan.FromSeconds(4)], cli.Timeouts);
    }
}
