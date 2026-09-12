using Aiko.Core;

namespace Aiko.Core.Tests;

public class UpdateInfoTests
{
    [Fact]
    public void A_newer_version_on_the_site_means_an_update_is_available()
    {
        var info = UpdateInfo.FromJson("""
            { "latest": "0.1.1", "downloadUrl": "https://github.com/Slayumind/aiko/releases/latest" }
            """);

        Assert.Equal(UpdateState.Available, info.CompareWith("0.1.0"));
        Assert.Equal("https://github.com/Slayumind/aiko/releases/latest", info.DownloadUrl);
    }

    [Fact]
    public void The_same_version_means_nothing_to_do()
    {
        var info = UpdateInfo.FromJson("""{ "latest": "0.1.0" }""");

        Assert.Equal(UpdateState.UpToDate, info.CompareWith("0.1.0"));
    }

    [Fact]
    public void A_newer_copy_than_the_site_knows_about_is_not_out_of_date()
    {
        var info = UpdateInfo.FromJson("""{ "latest": "0.1.0" }""");

        Assert.Equal(UpdateState.UpToDate, info.CompareWith("0.2.0"));
    }

    [Fact]
    public void Versions_are_compared_as_numbers_not_as_text()
    {
        var info = UpdateInfo.FromJson("""{ "latest": "0.10.0" }""");

        // As text "0.10.0" sorts before "0.9.0", which would hide the update.
        Assert.Equal(UpdateState.Available, info.CompareWith("0.9.0"));
    }

    [Fact]
    public void Without_a_latest_version_Aiko_says_it_does_not_know()
    {
        // The lesson from Sparks: the site leaves the field out when it cannot tell, and a client
        // that invents a fallback would declare every copy up to date.
        var info = UpdateInfo.FromJson("""{ "downloadUrl": "https://example.org" }""");

        Assert.Null(info.Latest);
        Assert.Equal(UpdateState.Unknown, info.CompareWith("0.1.0"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<html>not json</html>")]
    [InlineData("[1, 2, 3]")]
    [InlineData("{ \"latest\": 42 }")]
    [InlineData("{ \"latest\": \"tomorrow\" }")]
    public void An_answer_we_cannot_read_means_we_do_not_know(string json)
    {
        Assert.Equal(UpdateState.Unknown, UpdateInfo.FromJson(json).CompareWith("0.1.0"));
    }

    [Fact]
    public void Fields_from_a_later_site_version_are_ignored()
    {
        var info = UpdateInfo.FromJson("""
            { "latest": "0.2.0", "minSupported": "0.1.0", "installers": { "win-x64": "https://example.org/a.exe" } }
            """);

        Assert.Equal(UpdateState.Available, info.CompareWith("0.1.0"));
    }
}
