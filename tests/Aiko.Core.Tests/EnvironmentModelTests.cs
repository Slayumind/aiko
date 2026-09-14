using Aiko.Core;

namespace Aiko.Core.Tests;

/// Environment 1 in .claude, environment 2 in a folder of its own, commands and project folders.
public class EnvironmentModelTests
{
    private const string Home = @"C:\Users\someone";

    private static AikoEnvironment Env(string name, string folder, params string[] projects) =>
        new(name, [Path.Combine(Home, folder)]) { ProjectFolders = projects };

    // ---- the two environments ----

    [Fact]
    public void Environment_1_is_the_one_in_claude_whatever_the_order()
    {
        var settings = new EnvironmentSettings([Env("Personal", ".claude-personal"), Env("Work", ".claude")]);

        Assert.Equal("Work", settings.First(Home)!.Name);
        Assert.Equal("Personal", settings.Second(Home)!.Name);
        Assert.Empty(settings.Extras(Home));
    }

    [Fact]
    public void A_third_environment_from_an_older_list_is_an_extra_and_the_ring_is_kept()
    {
        // This machine before the two-environment rule: Personal in the ring, Main, and a dead Work.
        var settings = new EnvironmentSettings([
            Env("Personal", ".claude-personal"), Env("Main", ".claude"), Env("Work", ".claude-work"),
        ])
        { RingEnvironment = "Personal" };

        Assert.Equal("Main", settings.First(Home)!.Name);
        Assert.Equal("Personal", settings.Second(Home)!.Name);
        Assert.Equal(["Work"], settings.Extras(Home).Select(e => e.Name));
    }

    [Fact]
    public void Without_a_claude_environment_there_is_no_second_one()
    {
        var settings = new EnvironmentSettings([Env("Personal", ".claude-personal")]);

        Assert.Null(settings.First(Home));
        Assert.Null(settings.Second(Home));
        Assert.Empty(settings.Extras(Home));
    }

    [Fact]
    public void The_default_environment_falls_back_to_environment_1()
    {
        var settings = new EnvironmentSettings([Env("Personal", ".claude-personal"), Env("Work", ".claude")]);

        Assert.Equal("Work", settings.Default(Home)!.Name);
        Assert.Equal("Personal", (settings with { DefaultEnvironment = "Personal" }).Default(Home)!.Name);
        Assert.Equal("Work", (settings with { DefaultEnvironment = "Gone" }).Default(Home)!.Name);
    }

    // ---- commands ----

    [Theory]
    [InlineData("Work", "aiko-work")]
    [InlineData("Personal", "aiko-personal")]
    [InlineData("Основная", "aiko-osnovnaya")]
    [InlineData("Личная щука", "aiko-lichnaya-shchuka")]
    [InlineData("Side project!", "aiko-side-project")]
    [InlineData("  ", "aiko-env")]
    [InlineData("✨", "aiko-env")]
    public void The_command_follows_the_environment_name(string name, string command)
    {
        Assert.Equal(command, LaunchCommand.FromEnvironmentName(name));
    }

    [Fact]
    public void Renaming_changes_the_command_until_the_person_types_their_own()
    {
        var work = new AikoEnvironment("Work", [@"C:\x"]);

        Assert.Equal("aiko-job", (work with { Name = "Job" }).Command);
        Assert.Equal("cc-work", (work with { Name = "Job", CustomCommand = "cc-work" }).Command);
    }

    [Fact]
    public void A_long_name_makes_a_command_within_the_limit()
    {
        var command = LaunchCommand.FromEnvironmentName(new string('a', 80));

        Assert.True(command.Length <= LaunchCommand.MaxLength);
        Assert.Equal(CommandProblem.None, LaunchCommand.Check(command, []));
    }

    [Theory]
    [InlineData("aiko-work", CommandProblem.None)]
    [InlineData("cc_2", CommandProblem.None)]
    [InlineData("", CommandProblem.Empty)]
    [InlineData("Aiko-Work", CommandProblem.BadCharacters)]
    [InlineData("aiko work", CommandProblem.BadCharacters)]
    [InlineData("-work", CommandProblem.BadCharacters)]
    [InlineData("claude", CommandProblem.Reserved)]
    [InlineData("aiko-personal", CommandProblem.Taken)]
    public void Commands_are_checked(string command, CommandProblem problem)
    {
        Assert.Equal(problem, LaunchCommand.Check(command, ["aiko-personal"]));
    }

    // ---- project folders ----

    [Fact]
    public void A_bound_folder_covers_everything_inside_it()
    {
        var settings = new EnvironmentSettings([Env("Work", ".claude"), Env("Personal", ".claude-personal", @"D:\personal")]);

        Assert.Equal("Personal", ProjectBinding.EnvironmentFor(@"D:\personal\aiko\src", settings, Home)!.Name);
        Assert.Equal("Personal", ProjectBinding.EnvironmentFor(@"d:\PERSONAL", settings, Home)!.Name);
    }

    [Fact]
    public void A_folder_whose_name_only_starts_the_same_is_not_inside()
    {
        var settings = new EnvironmentSettings([Env("Work", ".claude"), Env("Personal", ".claude-personal", @"D:\work")]);

        Assert.Equal("Work", ProjectBinding.EnvironmentFor(@"D:\workshop", settings, Home)!.Name);
    }

    [Fact]
    public void The_deeper_binding_wins()
    {
        var settings = new EnvironmentSettings([
            Env("Work", ".claude", @"D:\work"),
            Env("Personal", ".claude-personal", @"D:\work\side-project\"),
        ]);

        Assert.Equal("Personal", ProjectBinding.EnvironmentFor(@"D:\work\side-project\app", settings, Home)!.Name);
        Assert.Equal("Work", ProjectBinding.EnvironmentFor(@"D:\work\client", settings, Home)!.Name);
    }

    [Fact]
    public void An_unbound_folder_runs_in_the_default_environment()
    {
        var settings = new EnvironmentSettings([Env("Work", ".claude"), Env("Personal", ".claude-personal", @"D:\personal")])
        {
            DefaultEnvironment = "Personal",
        };

        Assert.Equal("Personal", ProjectBinding.EnvironmentFor(@"C:\temp", settings, Home)!.Name);
    }

    [Fact]
    public void A_drive_root_binding_covers_the_whole_drive()
    {
        var settings = new EnvironmentSettings([Env("Work", ".claude"), Env("Personal", ".claude-personal", @"E:\")]);

        Assert.Equal("Personal", ProjectBinding.EnvironmentFor(@"E:\anything", settings, Home)!.Name);
    }

    // ---- the file ----

    [Fact]
    public void Commands_folders_and_the_default_survive_a_save()
    {
        var settings = new EnvironmentSettings([
            Env("Work", ".claude"),
            Env("Personal", ".claude-personal", @"D:\personal") with { CustomCommand = "cc" },
        ])
        { DefaultEnvironment = "Personal" };

        var again = EnvironmentSettings.FromJson(settings.ToJson());

        Assert.Equal("Personal", again.DefaultEnvironment);
        Assert.Equal("cc", again.Environments[1].Command);
        Assert.Equal([@"D:\personal"], again.Environments[1].ProjectFolders);
        Assert.Null(again.Environments[0].CustomCommand);
        Assert.Contains("\"schemaVersion\": 2", settings.ToJson());
    }

    [Fact]
    public void A_file_of_schema_1_reads_with_nothing_new_set()
    {
        const string old = """
            {
              "schemaVersion": 1,
              "ringEnvironment": "Personal",
              "environments": [
                { "name": "Personal", "configDirectories": ["C:\\Users\\someone\\.claude-personal"], "directMode": false },
                { "name": "Main", "configDirectories": ["C:\\Users\\someone\\.claude"], "directMode": true }
              ]
            }
            """;

        var settings = EnvironmentSettings.FromJson(old);

        Assert.Equal(2, settings.Environments.Count);
        Assert.All(settings.Environments, e => Assert.Empty(e.ProjectFolders));
        Assert.Equal("aiko-personal", settings.Environments[0].Command);
        Assert.Null(settings.DefaultEnvironment);
        Assert.True(settings.Environments[1].DirectMode);
    }
}
