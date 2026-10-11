using Aiko.Core;

namespace Aiko.Core.Tests;

/// The changes the settings window makes, each saved the moment it is made.
public class EnvironmentEditsTests
{
    private const string Home = @"C:\Users\someone";

    private static AikoEnvironment Env(string name, string folder, params string[] projects) =>
        new(name, [Path.Combine(Home, folder)]) { ProjectFolders = projects };

    /// The author's machine on 14 September: Aiko in .claude, Work in .claude-work.
    private static EnvironmentSettings TwoEnvironments() =>
        new EnvironmentSettings([
            Env("Aiko", ".claude", @"C:\Users\someone\Desktop\personal projects") with { CustomCommand = "aiko" },
            Env("Work", ".claude-work", @"C:\Users\someone\Desktop\kodland"),
        ])
        { RingEnvironment = "Aiko", DefaultEnvironment = "Work" };

    // ---- names and commands ----

    [Fact]
    public void A_rename_keeps_an_installed_command_and_suggests_the_new_one()
    {
        var renamed = EnvironmentEdits.Rename(TwoEnvironments(), "Work", "Work · Kodland", commandsInstalled: true);
        var work = renamed.Settings.Environments[1];

        Assert.Equal("Work · Kodland", work.Name);
        Assert.Equal("aiko-work", work.Command);
        Assert.Equal("aiko-work-kodland", renamed.SuggestedCommand);
    }

    [Fact]
    public void Before_the_commands_are_installed_the_command_follows_the_name()
    {
        var renamed = EnvironmentEdits.Rename(TwoEnvironments(), "Work", "Job", commandsInstalled: false);

        Assert.Equal("aiko-job", renamed.Settings.Environments[1].Command);
        Assert.Null(renamed.SuggestedCommand);
    }

    [Fact]
    public void A_rename_that_makes_the_same_command_suggests_nothing()
    {
        var renamed = EnvironmentEdits.Rename(TwoEnvironments(), "Work", "WORK", commandsInstalled: true);

        Assert.Equal("aiko-work", renamed.Settings.Environments[1].Command);
        Assert.Null(renamed.Settings.Environments[1].CustomCommand);
        Assert.Null(renamed.SuggestedCommand);
    }

    [Fact]
    public void The_ring_and_the_default_follow_a_renamed_environment()
    {
        var settings = EnvironmentEdits.Rename(TwoEnvironments(), "Work", "Job", true).Settings;
        settings = EnvironmentEdits.Rename(settings, "Aiko", "Personal", true).Settings;

        Assert.Equal("Personal", settings.RingEnvironment);
        Assert.Equal("Job", settings.DefaultEnvironment);
        Assert.Equal("aiko", settings.Environments[0].Command);
    }

    [Theory]
    [InlineData("", NameProblem.Empty)]
    [InlineData("   ", NameProblem.Empty)]
    [InlineData("aiko", NameProblem.Taken)]
    [InlineData("Work", NameProblem.None)]
    [InlineData("Kodland", NameProblem.None)]
    public void Names_are_checked(string wanted, NameProblem problem)
    {
        Assert.Equal(problem, EnvironmentEdits.CheckName(TwoEnvironments(), "Work", wanted));
    }

    [Fact]
    public void A_name_that_does_not_pass_the_check_changes_nothing()
    {
        var settings = TwoEnvironments();

        Assert.Same(settings, EnvironmentEdits.Rename(settings, "Work", "AIKO", true).Settings);
        Assert.Same(settings, EnvironmentEdits.Rename(settings, "Work", new string('a', 41), true).Settings);
    }

    [Fact]
    public void Taking_the_suggestion_sets_the_command_to_the_name_again()
    {
        var renamed = EnvironmentEdits.Rename(TwoEnvironments(), "Work", "Kodland", true);
        var settings = EnvironmentEdits.SetCommand(renamed.Settings, "Kodland", renamed.SuggestedCommand!);

        Assert.Equal("aiko-kodland", settings.Environments[1].Command);
        Assert.Null(settings.Environments[1].CustomCommand);
    }

    [Fact]
    public void A_command_another_environment_uses_is_refused()
    {
        var settings = TwoEnvironments();

        Assert.Equal(CommandProblem.Taken, EnvironmentEdits.CheckCommand(settings, "Work", "aiko"));
        Assert.Same(settings, EnvironmentEdits.SetCommand(settings, "Work", "aiko"));
        Assert.Equal(CommandProblem.None, EnvironmentEdits.CheckCommand(settings, "Aiko", "aiko"));
    }

    [Fact]
    public void The_checklist_starts_from_the_command_in_use_once_commands_are_installed()
    {
        var work = new AikoEnvironment("Work · Kodland", [@"C:\x"]);

        Assert.Equal("aiko-work-kodland", EnvironmentEdits.StartingCommand(work, commandsInstalled: true));
        Assert.Null(EnvironmentEdits.StartingCommand(work, commandsInstalled: false));
        Assert.Equal("cc", EnvironmentEdits.StartingCommand(work with { CustomCommand = "cc" }, commandsInstalled: false));
        Assert.Null(EnvironmentEdits.StartingCommand(null, commandsInstalled: true));
    }

    // ---- folders ----

    [Fact]
    public void Bindings_are_one_list_sorted_by_folder()
    {
        var bindings = EnvironmentEdits.Bindings(TwoEnvironments());

        Assert.Equal(
            [
                new Binding(@"C:\Users\someone\Desktop\kodland", "Work"),
                new Binding(@"C:\Users\someone\Desktop\personal projects", "Aiko"),
            ],
            bindings);
    }

    [Fact]
    public void Binding_a_folder_to_the_other_environment_moves_it()
    {
        // The live bug: the environment picked for a folder never reached the folder.
        var settings = EnvironmentEdits.Bind(TwoEnvironments(), @"c:\users\someone\desktop\KODLAND\", "Aiko");

        Assert.Empty(settings.Environments[1].ProjectFolders);
        Assert.Equal(2, settings.Environments[0].ProjectFolders.Count);
        Assert.Single(EnvironmentEdits.Bindings(settings), b => b.Environment == "Aiko" && b.Folder.EndsWith(@"KODLAND\"));
    }

    [Fact]
    public void Binding_to_an_unknown_environment_changes_nothing()
    {
        var settings = TwoEnvironments();

        Assert.Same(settings, EnvironmentEdits.Bind(settings, @"D:\x", "Gone"));
    }

    [Fact]
    public void Unbinding_takes_the_folder_away()
    {
        var settings = EnvironmentEdits.Unbind(TwoEnvironments(), @"C:\Users\someone\Desktop\kodland");

        Assert.Single(EnvironmentEdits.Bindings(settings));
    }

    [Fact]
    public void The_default_is_changed_only_to_an_environment_that_exists()
    {
        var settings = TwoEnvironments();

        Assert.Equal("Aiko", EnvironmentEdits.SetDefault(settings, "Aiko").DefaultEnvironment);
        Assert.Same(settings, EnvironmentEdits.SetDefault(settings, "Gone"));
    }

    [Fact]
    public void A_new_folder_goes_to_the_environment_that_is_not_the_default()
    {
        var settings = TwoEnvironments();

        Assert.Equal("Aiko", EnvironmentEdits.ForNewBinding(settings, Home)!.Name);
        Assert.Equal("Work", EnvironmentEdits.ForNewBinding(settings with { DefaultEnvironment = null }, Home)!.Name);
    }

    // ---- direct mode and removing ----

    [Fact]
    public void Direct_mode_changes_for_one_environment()
    {
        var settings = EnvironmentEdits.SetDirectMode(TwoEnvironments(), "Work", true);

        Assert.True(settings.Environments[1].DirectMode);
        Assert.False(settings.Environments[0].DirectMode);
    }

    [Fact]
    public void The_persona_is_switched_in_one_environment_and_kept_through_other_edits()
    {
        var settings = EnvironmentEdits.SetPersona(TwoEnvironments(), "Work", true);
        var renamed = EnvironmentEdits.Rename(settings, "Work", "Kodland", commandsInstalled: false).Settings;

        Assert.True(settings.Environments[1].Persona);
        Assert.False(settings.Environments[0].Persona);
        Assert.True(renamed.Environments[1].Persona);
    }

    [Fact]
    public void An_old_skills_switch_that_was_off_turns_the_skills_off_everywhere_once()
    {
        var settings = WithSkills(WithSkills(TwoEnvironments(), "Aiko"), "Work");
        var oldOff = PersonaSettings.Default with { SkillsOn = false, Temperament = Temperament.Musou };

        var carried = EnvironmentEdits.CarryOldSkillsSwitch(settings, oldOff);
        var again = EnvironmentEdits.CarryOldSkillsSwitch(
            WithSkills(carried.Settings, "Work"), carried.Persona);

        Assert.True(carried.Changed);
        Assert.All(carried.Settings.Environments, e => Assert.False(e.Skills));
        Assert.True(carried.Persona.SkillsOn);
        Assert.Equal(Temperament.Musou, carried.Persona.Temperament);
        Assert.False(again.Changed);
        Assert.True(again.Settings.Environments[1].Skills);
    }

    [Fact]
    public void An_old_skills_switch_that_was_on_changes_nothing()
    {
        var settings = WithSkills(TwoEnvironments(), "Work");

        var carried = EnvironmentEdits.CarryOldSkillsSwitch(settings, PersonaSettings.Default);

        Assert.False(carried.Changed);
        Assert.Same(settings, carried.Settings);
    }

    /// The persona on and then the skills, the only order in which the skills can be turned on.
    private static EnvironmentSettings WithSkills(EnvironmentSettings settings, string environment) =>
        EnvironmentEdits.SetSkills(EnvironmentEdits.SetPersona(settings, environment, true), environment, true);

    [Fact]
    public void The_skills_are_switched_only_where_the_persona_is_on()
    {
        var withPersona = EnvironmentEdits.SetPersona(TwoEnvironments(), "Work", true);

        var on = EnvironmentEdits.SetSkills(withPersona, "Work", true);
        var refused = EnvironmentEdits.SetSkills(withPersona, "Aiko", true);

        Assert.True(on.Environments[1].HasSkills);
        Assert.False(refused.Environments[0].Skills);
    }

    [Fact]
    public void The_persona_may_be_on_without_the_skills()
    {
        var settings = EnvironmentEdits.SetSkills(WithSkills(TwoEnvironments(), "Work"), "Work", false);

        Assert.True(settings.Environments[1].Persona);
        Assert.False(settings.Environments[1].HasSkills);
    }

    [Fact]
    public void Turning_the_persona_off_keeps_the_choice_of_skills_but_the_skills_are_off()
    {
        var off = EnvironmentEdits.SetPersona(WithSkills(TwoEnvironments(), "Work"), "Work", false);

        Assert.True(off.Environments[1].Skills);
        Assert.False(off.Environments[1].HasSkills);
    }

    [Fact]
    public void The_memory_is_switched_only_where_the_persona_is_on()
    {
        var withPersona = EnvironmentEdits.SetPersona(TwoEnvironments(), "Work", true);

        var on = EnvironmentEdits.SetMemory(withPersona, "Work", true);
        var refused = EnvironmentEdits.SetMemory(withPersona, "Aiko", true);

        Assert.True(on.Environments[1].HasMemory);
        Assert.False(refused.Environments[0].Memory);
    }

    [Fact]
    public void Turning_the_persona_off_keeps_the_choice_of_memory_but_the_memory_is_off()
    {
        var on = EnvironmentEdits.SetMemory(EnvironmentEdits.SetPersona(TwoEnvironments(), "Work", true), "Work", true);

        var off = EnvironmentEdits.SetPersona(on, "Work", false);

        Assert.True(off.Environments[1].Memory);
        Assert.False(off.Environments[1].HasMemory);
    }

    [Fact]
    public void Switching_the_persona_of_an_unknown_environment_changes_nothing()
    {
        var settings = TwoEnvironments();

        Assert.Same(settings, EnvironmentEdits.SetPersona(settings, "Nobody", true));
    }

    [Fact]
    public void The_environment_in_claude_cannot_be_removed()
    {
        var settings = TwoEnvironments();

        Assert.False(EnvironmentEdits.CanRemove(settings, "Aiko", Home));
        Assert.Same(settings, EnvironmentEdits.Remove(settings, "Aiko", Home));
    }

    [Fact]
    public void Removing_an_environment_takes_its_bindings_and_the_default_with_it()
    {
        var settings = EnvironmentEdits.Remove(TwoEnvironments() with { RingEnvironment = "Work" }, "Work", Home);

        Assert.Equal(["Aiko"], settings.Environments.Select(e => e.Name));
        Assert.Null(settings.DefaultEnvironment);
        Assert.Null(settings.RingEnvironment);
        Assert.Equal("Aiko", settings.Default(Home)!.Name);
        Assert.Single(EnvironmentEdits.Bindings(settings));
    }
}
