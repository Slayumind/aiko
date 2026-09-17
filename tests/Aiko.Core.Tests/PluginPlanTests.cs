using System.Text.Json;
using Aiko.Core;

namespace Aiko.Core.Tests;

public class PluginPlanTests
{
    private const string LocalAppData = @"C:\Users\Тест Юзер\AppData\Local";
    private const string Marketplace = @"C:\Users\Тест Юзер\AppData\Local\Aiko\marketplace";
    private const string Persona = "aiko-persona@aiko";

    private static AikoEnvironment Env(bool persona) => new("Aiko", [@"C:\Users\someone\.claude"]) { Persona = persona };

    private static PluginState State(string? marketplace = null, string[]? installed = null, string[]? enabled = null) =>
        new(marketplace, (installed ?? []).ToHashSet(), (enabled ?? []).ToHashSet());

    // ---- the command and the marketplace file ----

    [Fact]
    public void The_installed_bridge_is_named_through_LOCALAPPDATA_so_the_command_stays_ASCII()
    {
        var command = AikoMarketplace.PersonaCommand(LocalAppData + @"\Slayumind.Aiko\current\Aiko.Bridge.exe", LocalAppData);

        Assert.Equal("\"%LOCALAPPDATA%\\Slayumind.Aiko\\current\\Aiko.Bridge.exe\" plugin aiko-persona", command);
    }

    [Fact]
    public void A_build_folder_is_named_as_it_is_and_refused_when_it_is_not_ASCII()
    {
        Assert.Equal(
            "\"D:\\src\\Aiko.Bridge.exe\" plugin aiko-persona",
            AikoMarketplace.PersonaCommand(@"D:\src\Aiko.Bridge.exe", LocalAppData));
        Assert.Null(AikoMarketplace.PersonaCommand(LocalAppData + @"\dev\Aiko.Bridge.exe", LocalAppData));
    }

    [Theory]
    [InlineData("\"C:\\x\\a.exe\" plugin p", true)]
    [InlineData("", false)]
    [InlineData("a    b", false)]
    [InlineData("tab\there", false)]
    public void Commands_follow_the_Claude_Code_rules(string command, bool valid)
    {
        Assert.Equal(valid, AikoMarketplace.IsValidCommand(command));
        Assert.False(AikoMarketplace.IsValidCommand(new string('a', 501)));
    }

    [Fact]
    public void The_marketplace_lists_the_persona_as_a_command_source()
    {
        using var json = JsonDocument.Parse(AikoMarketplace.Json("\"x.exe\" plugin aiko-persona"));
        var plugin = json.RootElement.GetProperty("plugins")[0];

        Assert.Equal("aiko", json.RootElement.GetProperty("name").GetString());
        Assert.Equal("aiko-persona", plugin.GetProperty("name").GetString());
        Assert.Equal("command", plugin.GetProperty("source").GetProperty("source").GetString());
        Assert.Equal("\"x.exe\" plugin aiko-persona", plugin.GetProperty("source").GetProperty("command").GetString());
    }

    [Fact]
    public void The_marketplace_file_keeps_quotes_readable()
    {
        var text = AikoMarketplace.Json(@"""%LOCALAPPDATA%\x.exe"" plugin aiko-persona");

        Assert.DoesNotContain(@"\u0022", text);
        Assert.Contains(@"""\""%LOCALAPPDATA%\\x.exe\"" plugin aiko-persona""", text);
    }

    // ---- what a folder has ----

    [Fact]
    public void Only_our_plugins_are_read_and_only_user_installs_count()
    {
        var state = PluginState.Read(
            """{ "enabledPlugins": { "aiko-persona@aiko": true, "aiko@aiko": false, "frontend-design@claude-plugins-official": true } }""",
            """
            { "version": 2, "plugins": {
                "aiko-persona@aiko": [ { "scope": "user" } ],
                "aiko@aiko": [ { "scope": "project", "projectPath": "C:\\x" } ],
                "frontend-design@claude-plugins-official": [ { "scope": "user" } ] } }
            """,
            """{ "aiko": { "installLocation": "C:\\m" }, "claude-plugins-official": { "installLocation": "C:\\o" } }""");

        Assert.Equal(@"C:\m", state.MarketplaceFolder);
        Assert.Equal([Persona], state.Installed);
        Assert.Equal([Persona], state.Enabled);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1, 2]")]
    public void Missing_or_broken_files_read_as_nothing_of_ours(string? json)
    {
        var state = PluginState.Read(json, json, json);

        Assert.Null(state.MarketplaceFolder);
        Assert.Empty(state.Installed);
        Assert.Empty(state.Enabled);
    }

    // ---- what a folder should have ----

    [Fact]
    public void Without_the_persona_a_folder_wants_nothing_not_even_skills()
    {
        Assert.Empty(PluginPlan.Desired(Env(persona: false), PersonaSettings.Default, ["copy"]));
    }

    [Fact]
    public void With_the_persona_a_folder_wants_it_and_the_skills_plugin()
    {
        var desired = PluginPlan.Desired(Env(persona: true), PersonaSettings.Default, ["copy", "palette"]);

        Assert.Equal([Persona, "aiko@aiko"], desired.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void With_the_skills_off_or_none_shipped_a_folder_wants_only_the_persona()
    {
        var skillsOff = PersonaSettings.Default with { SkillsOn = false };

        Assert.Equal([Persona], PluginPlan.Desired(Env(persona: true), skillsOff, ["copy", "palette"]));
        Assert.Equal([Persona], PluginPlan.Desired(Env(persona: true), PersonaSettings.Default, []));
    }

    [Theory]
    [InlineData("aiko-copy@aiko", true)]
    [InlineData("aiko-gamedesign-research@aiko", true)]
    [InlineData("aiko@aiko", false)]
    [InlineData("aiko-persona@aiko", false)]
    [InlineData("aiko-copy@someone-else", false)]
    public void Only_our_plugins_this_version_does_not_ship_are_retired(string id, bool retired)
    {
        Assert.Equal(retired, PluginPlan.IsRetired(id));
    }

    // ---- the steps ----

    [Fact]
    public void Nothing_wanted_and_nothing_there_means_no_steps_and_no_marketplace()
    {
        Assert.Empty(PluginPlan.Steps(new HashSet<string>(), State(), Marketplace));
    }

    [Fact]
    public void A_first_switch_on_adds_the_marketplace_and_installs()
    {
        var steps = PluginPlan.Steps(new HashSet<string> { Persona }, State(), Marketplace);

        Assert.Equal(
            [new PluginStep(PluginStepKind.AddMarketplace, Marketplace), new PluginStep(PluginStepKind.Install, Persona)],
            steps);
    }

    [Fact]
    public void An_installed_but_disabled_plugin_is_enabled_not_installed_again()
    {
        var steps = PluginPlan.Steps(new HashSet<string> { Persona }, State(Marketplace, installed: [Persona]), Marketplace);

        Assert.Equal([new PluginStep(PluginStepKind.Enable, Persona)], steps);
    }

    [Fact]
    public void Switching_off_disables_and_keeps_the_files()
    {
        var steps = PluginPlan.Steps(new HashSet<string>(), State(Marketplace, installed: [Persona, "aiko@aiko"], enabled: [Persona, "aiko@aiko"]), Marketplace);

        Assert.Equal(
            [new PluginStep(PluginStepKind.Disable, Persona), new PluginStep(PluginStepKind.Disable, "aiko@aiko")],
            steps);
    }

    [Fact]
    public void The_one_skill_plugins_of_older_versions_are_uninstalled_while_the_new_one_comes_in()
    {
        var state = State(
            Marketplace,
            installed: [Persona, "aiko-copy@aiko", "aiko-palette@aiko"],
            enabled: [Persona, "aiko-copy@aiko"]);

        var steps = PluginPlan.Steps(new HashSet<string> { Persona, "aiko@aiko" }, state, Marketplace);

        Assert.Equal(
            [
                new PluginStep(PluginStepKind.Install, "aiko@aiko"),
                new PluginStep(PluginStepKind.Uninstall, "aiko-copy@aiko"),
                new PluginStep(PluginStepKind.Uninstall, "aiko-palette@aiko"),
            ],
            steps);
    }

    [Fact]
    public void Old_plugins_go_even_where_the_persona_is_off()
    {
        var steps = PluginPlan.Steps(new HashSet<string>(), State(Marketplace, installed: ["aiko-copy@aiko"]), Marketplace);

        Assert.Equal([new PluginStep(PluginStepKind.Uninstall, "aiko-copy@aiko")], steps);
    }

    [Fact]
    public void A_folder_that_already_matches_needs_nothing()
    {
        var steps = PluginPlan.Steps(new HashSet<string> { Persona }, State(Marketplace + @"\", installed: [Persona], enabled: [Persona]), Marketplace);

        Assert.Empty(steps);
    }

    [Fact]
    public void A_marketplace_of_the_same_name_from_another_place_is_replaced()
    {
        var steps = PluginPlan.Steps(new HashSet<string> { Persona }, State(@"D:\old", installed: [Persona], enabled: [Persona]), Marketplace);

        Assert.Equal(
            [new PluginStep(PluginStepKind.RemoveMarketplace, "aiko"), new PluginStep(PluginStepKind.AddMarketplace, Marketplace)],
            steps);
    }

    [Fact]
    public void Steps_use_user_scope_and_accept_the_command_without_asking()
    {
        Assert.Equal(["plugin", "install", Persona, "-y", "--scope", "user"], new PluginStep(PluginStepKind.Install, Persona).Arguments);
        Assert.Equal(["plugin", "disable", Persona, "--scope", "user"], new PluginStep(PluginStepKind.Disable, Persona).Arguments);
        Assert.Equal(["plugin", "marketplace", "add", Marketplace], new PluginStep(PluginStepKind.AddMarketplace, Marketplace).Arguments);
    }

    // ---- running them ----

    private sealed class ScriptedCli(params int[] exitCodes) : IClaudeCli
    {
        public List<(string Folder, IReadOnlyList<string> Arguments)> Calls { get; } = [];

        public CliResult Run(string configDirectory, IReadOnlyList<string> arguments, TimeSpan timeout)
        {
            Calls.Add((configDirectory, arguments));
            var code = Calls.Count <= exitCodes.Length ? exitCodes[Calls.Count - 1] : 0;
            return new CliResult(code, TimedOut: false);
        }
    }

    [Fact]
    public void Steps_run_in_order_for_the_folder()
    {
        var cli = new ScriptedCli();
        var steps = new[] { new PluginStep(PluginStepKind.AddMarketplace, Marketplace), new PluginStep(PluginStepKind.Install, Persona) };

        var results = PluginReconciler.Apply(cli, @"C:\Users\someone\.claude-work", steps);

        Assert.All(results, r => Assert.True(r.Result.Succeeded));
        Assert.Equal([PluginStepKind.AddMarketplace, PluginStepKind.Install], results.Select(r => r.Step.Kind));
        Assert.All(cli.Calls, c => Assert.Equal(@"C:\Users\someone\.claude-work", c.Folder));
    }

    [Fact]
    public void A_failed_marketplace_step_stops_the_run()
    {
        var cli = new ScriptedCli(1);
        var steps = new[] { new PluginStep(PluginStepKind.AddMarketplace, Marketplace), new PluginStep(PluginStepKind.Install, Persona) };

        var results = PluginReconciler.Apply(cli, @"C:\x", steps);

        Assert.Single(results);
        Assert.Single(cli.Calls);
    }

    [Fact]
    public void One_failed_plugin_does_not_stop_the_others()
    {
        var cli = new ScriptedCli(0, 1, 0);
        var steps = new[]
        {
            new PluginStep(PluginStepKind.Enable, Persona),
            new PluginStep(PluginStepKind.Install, "aiko@aiko"),
            new PluginStep(PluginStepKind.Disable, "aiko-palette@aiko"),
        };

        var results = PluginReconciler.Apply(cli, @"C:\x", steps);

        Assert.Equal([true, false, true], results.Select(r => r.Result.Succeeded));
    }
}
