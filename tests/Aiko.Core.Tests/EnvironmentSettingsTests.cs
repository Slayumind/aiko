namespace Aiko.Core.Tests;

public class EnvironmentSettingsTests
{
    private const string TwoEnvironments = """
        {
          "ringEnvironment": "Personal",
          "environments": [
            { "name": "Personal", "configDirectories": ["C:\\Users\\slayu\\.claude-personal"], "directMode": false },
            { "name": "Work", "configDirectories": ["C:\\Users\\slayu\\.claude"], "directMode": true }
          ]
        }
        """;

    [Fact]
    public void Reads_environments_with_their_folders_and_direct_mode()
    {
        var settings = EnvironmentSettings.FromJson(TwoEnvironments);

        Assert.Equal(2, settings.Environments.Count);
        Assert.Equal("Personal", settings.Environments[0].Name);
        Assert.False(settings.Environments[0].DirectMode);
        Assert.True(settings.Environments[1].DirectMode);
        Assert.Equal(@"C:\Users\slayu\.claude", settings.Environments[1].ConfigDirectories.Single());
    }

    [Fact]
    public void The_ring_shows_the_chosen_environment_and_the_dot_the_other_one()
    {
        var settings = EnvironmentSettings.FromJson(TwoEnvironments);

        Assert.Equal("Personal", settings.Ring!.Name);
        Assert.Equal("Work", settings.Dot!.Name);
    }

    [Fact]
    public void A_click_swaps_the_ring_and_the_dot()
    {
        var swapped = EnvironmentSettings.FromJson(TwoEnvironments).SwapRing();

        Assert.Equal("Work", swapped.Ring!.Name);
        Assert.Equal("Personal", swapped.Dot!.Name);
    }

    [Fact]
    public void With_one_environment_there_is_no_dot_and_nothing_to_swap()
    {
        var settings = EnvironmentSettings.FromJson("""
            { "environments": [ { "name": "Personal", "configDirectories": ["C:\\x"] } ] }
            """);

        Assert.Equal("Personal", settings.Ring!.Name);
        Assert.Null(settings.Dot);
        Assert.Equal("Personal", settings.SwapRing().Ring!.Name);
    }

    [Fact]
    public void An_unknown_ring_name_falls_back_to_the_first_environment()
    {
        var settings = EnvironmentSettings.FromJson("""
            {
              "ringEnvironment": "Renamed away",
              "environments": [ { "name": "Personal", "configDirectories": ["C:\\x"] } ]
            }
            """);

        Assert.Equal("Personal", settings.Ring!.Name);
    }

    [Fact]
    public void Environments_without_a_name_or_a_folder_are_dropped()
    {
        var settings = EnvironmentSettings.FromJson("""
            {
              "environments": [
                { "name": "", "configDirectories": ["C:\\x"] },
                { "name": "No folders", "configDirectories": [] },
                { "name": "Good", "configDirectories": ["C:\\x"] }
              ]
            }
            """);

        Assert.Single(settings.Environments);
        Assert.Equal("Good", settings.Environments[0].Name);
    }

    [Fact]
    public void What_is_written_can_be_read_back()
    {
        var settings = EnvironmentSettings.FromJson(TwoEnvironments).SwapRing();

        var again = EnvironmentSettings.FromJson(settings.ToJson());

        Assert.Equal("Work", again.Ring!.Name);
        Assert.Equal(2, again.Environments.Count);
        Assert.True(again.Environments.Single(e => e.Name == "Work").DirectMode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("{ \"environments\": [] }")]
    public void Anything_unusable_means_no_environments_yet(string json)
    {
        var settings = EnvironmentSettings.FromJson(json);

        Assert.False(settings.HasEnvironments);
        Assert.Null(settings.Ring);
    }

    [Fact]
    public void A_file_from_before_the_persona_reads_with_the_persona_off()
    {
        var settings = EnvironmentSettings.FromJson(TwoEnvironments);

        Assert.All(settings.Environments, e => Assert.False(e.Persona));
    }

    [Fact]
    public void The_persona_flag_survives_a_trip_through_the_file()
    {
        var settings = EnvironmentSettings.FromJson(TwoEnvironments);
        var withPersona = settings with
        {
            Environments = [settings.Environments[0] with { Persona = true }, settings.Environments[1]],
        };

        var json = withPersona.ToJson();
        var back = EnvironmentSettings.FromJson(json);

        Assert.True(back.Environments[0].Persona);
        Assert.False(back.Environments[1].Persona);
        Assert.Contains($"\"schemaVersion\": {EnvironmentSettings.CurrentSchema}", json);
    }
}
