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
    public void Settings_survive_a_trip_through_the_file()
    {
        var settings = new AppSettings
        {
            Place = AikoPlace.Island,
            RunAtStartup = false,
            CheckUpdates = true,
            Language = AikoLanguage.Russian,
            HideIslandInFullScreen = false,
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
}
