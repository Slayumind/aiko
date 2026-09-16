using Aiko.Core;

namespace Aiko.Core.Tests;

public class AppSettingsTests
{
    [Fact]
    public void By_default_Aiko_sits_in_the_tray_and_starts_with_Windows()
    {
        var settings = AppSettings.Default;

        Assert.Equal(AikoPlace.Tray, settings.Place);
        Assert.True(settings.RunAtStartup);
        Assert.Equal(AikoLanguage.System, settings.Language);
        Assert.True(settings.HideIslandInFullScreen);
    }

    [Fact]
    public void Update_checks_are_off_until_the_user_agrees()
    {
        Assert.False(AppSettings.Default.CheckUpdates);
    }

    [Fact]
    public void Statistics_are_off_and_unasked_until_the_user_answers()
    {
        Assert.False(AppSettings.Default.SendStats);
        Assert.False(AppSettings.Default.PrivacyAsked);
    }

    /// Somebody on 0.2.0 agreed to a switch that sent three things. 0.2.1 sends six, so the old
    /// yes does not carry over: the file reads as unasked and the question is put again.
    [Fact]
    public void A_file_from_0_2_0_counts_as_never_asked()
    {
        var old = """
            {
              "schemaVersion": 2,
              "place": "Tray",
              "runAtStartup": true,
              "checkUpdates": true,
              "meetAikoShown": true
            }
            """;

        var settings = AppSettings.FromJson(old);

        Assert.True(settings.CheckUpdates);
        Assert.False(settings.SendStats);
        Assert.False(settings.PrivacyAsked);
    }

    /// Found on a live install: a settings.json made by 0.1 still said schema 1 after 0.2.1 had
    /// written new fields into it. The number is meant to tell a future migration what shape the
    /// file is in, and a number that never moves cannot do that.
    [Fact]
    public void Saving_stamps_the_file_with_the_schema_that_wrote_it()
    {
        var old = AppSettings.FromJson("""
            { "schemaVersion": 1, "place": "Tray", "runAtStartup": true }
            """);

        Assert.Equal(1, old.SchemaVersion);
        Assert.Contains($"\"schemaVersion\": {AppSettings.CurrentSchema}", old.ToJson());
        Assert.Equal(AppSettings.CurrentSchema, AppSettings.FromJson(old.ToJson()).SchemaVersion);
    }

    [Fact]
    public void Settings_survive_a_trip_through_the_file()
    {
        var settings = new AppSettings
        {
            Place = AikoPlace.Island,
            RunAtStartup = false,
            CheckUpdates = true,
            SendStats = true,
            PrivacyAsked = true,
            Language = AikoLanguage.Russian,
            HideIslandInFullScreen = false,
            Island = new IslandPosition(ScreenEdge.Right, 0.25),
        };

        var back = AppSettings.FromJson(settings.ToJson());

        Assert.Equal(settings, back);
    }

    [Fact]
    public void Choices_are_written_as_words()
    {
        var json = new AppSettings { Place = AikoPlace.Island, Language = AikoLanguage.English }.ToJson();

        Assert.Contains("\"Island\"", json);
        Assert.Contains("\"English\"", json);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json at all")]
    [InlineData("{ \"place\": ")]
    public void A_broken_file_reads_as_the_defaults(string json)
    {
        Assert.Equal(AppSettings.Default, AppSettings.FromJson(json));
    }

    [Fact]
    public void Fields_we_do_not_know_are_ignored()
    {
        var json = "{ \"place\": \"Island\", \"somethingFromALaterVersion\": 42 }";

        var settings = AppSettings.FromJson(json);

        Assert.Equal(AikoPlace.Island, settings.Place);
        Assert.True(settings.RunAtStartup);
    }

    [Fact]
    public void A_choice_we_do_not_know_reads_as_the_defaults()
    {
        // A newer Aiko may write a place this one has never heard of. Better the whole file falls
        // back than the app starts with half of it applied.
        var settings = AppSettings.FromJson("{ \"place\": \"Wallpaper\" }");

        Assert.Equal(AppSettings.Default, settings);
    }

    [Fact]
    public void Meet_Aiko_has_not_been_shown_in_a_file_from_0_1()
    {
        var settings = AppSettings.FromJson("{ \"schemaVersion\": 1, \"place\": \"Island\" }");

        Assert.False(settings.MeetAikoShown);
        Assert.Equal(AikoPlace.Island, settings.Place);
    }

    [Fact]
    public void Meet_Aiko_shown_survives_a_trip_through_the_file()
    {
        var back = AppSettings.FromJson((AppSettings.Default with { MeetAikoShown = true }).ToJson());

        Assert.True(back.MeetAikoShown);
        Assert.Equal(AppSettings.CurrentSchema, back.SchemaVersion);
    }
}
