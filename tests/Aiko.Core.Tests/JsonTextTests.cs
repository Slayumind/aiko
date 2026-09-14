using Aiko.Core;

namespace Aiko.Core.Tests;

/// Telling an empty settings file from a broken one.
///
/// Every parser reads a broken file as "no data", and the next save then writes defaults over it.
/// That is how a settings file turns into lost settings without anybody noticing, so the store has
/// to be able to tell the two apart before it writes.
public class JsonTextTests
{
    [Theory]
    [InlineData("{}")]
    [InlineData("""{ "place": "Island" }""")]
    [InlineData("  { }  ")]
    public void A_settings_file_is_an_object(string json)
    {
        Assert.True(JsonText.IsObject(json));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Nothing_is_not_an_object_and_is_not_broken_either(string? json)
    {
        // Empty means a first run. The store checks for length before it asks.
        Assert.False(JsonText.IsObject(json));
    }

    [Theory]
    [InlineData("{ \"place\": ")]
    [InlineData("not json at all")]
    [InlineData("[1, 2, 3]")]
    [InlineData("\"a string\"")]
    [InlineData("\0\0\0\0")]
    public void Rubbish_is_not_an_object(string json)
    {
        Assert.False(JsonText.IsObject(json));
    }

    [Fact]
    public void Half_a_file_from_a_power_cut_is_not_an_object()
    {
        var whole = AppSettings.Default with { Place = AikoPlace.Island };
        var half = whole.ToJson()[..20];

        Assert.False(JsonText.IsObject(half));
    }

    [Fact]
    public void What_we_write_is_always_readable_again()
    {
        Assert.True(JsonText.IsObject(AppSettings.Default.ToJson()));
        Assert.True(JsonText.IsObject(EnvironmentSettings.Empty.ToJson()));
    }

    [Fact]
    public void Both_files_carry_the_shape_they_were_written_in()
    {
        Assert.Contains("schemaVersion", AppSettings.Default.ToJson());
        Assert.Contains("schemaVersion", EnvironmentSettings.Empty.ToJson());
    }

    [Fact]
    public void A_file_written_before_the_version_existed_still_reads()
    {
        var old = """{ "place": "Island", "runAtStartup": false }""";

        var settings = AppSettings.FromJson(old);

        Assert.Equal(AikoPlace.Island, settings.Place);
        Assert.False(settings.RunAtStartup);
    }
}
