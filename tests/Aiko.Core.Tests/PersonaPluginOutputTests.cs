using Aiko.Core;

namespace Aiko.Core.Tests;

public class PersonaPluginOutputTests
{
    private static readonly AikoFolders Folders = AikoFolders.Windows(
        @"C:\Users\someone\AppData\Roaming", @"C:\Users\someone\AppData\Local");

    [Fact]
    public void Each_environment_gets_its_own_folder_named_like_its_snapshot()
    {
        Assert.Equal(
            @"C:\Users\someone\AppData\Local\Aiko\plugins\claude",
            PersonaPluginOutput.EnvironmentFolder(Folders, @"C:\Users\someone\.claude"));
        Assert.Equal(
            @"C:\Users\someone\AppData\Local\Aiko\plugins\claude-work\0123456789ab",
            PersonaPluginOutput.VersionFolder(Folders, @"C:\Users\someone\.claude-work\", "0123456789ab"));
    }

    [Theory]
    [InlineData(new[] { "plugin", "aiko-persona" }, true)]
    [InlineData(new[] { "plugin", "aiko-copy" }, false)]
    [InlineData(new[] { "plugin" }, false)]
    [InlineData(new[] { "plugin", "aiko-persona", "extra" }, false)]
    [InlineData(new string[0], false)]
    public void Only_the_persona_plugin_is_built(string[] args, bool expected)
    {
        Assert.Equal(expected, PersonaPluginOutput.IsRequest(args));
    }

    [Fact]
    public void Old_versions_go_after_an_hour_and_the_current_one_stays()
    {
        var now = new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);
        var folders = new[]
        {
            ("aaaaaaaaaaaa", now.AddHours(-3)),
            ("bbbbbbbbbbbb", now.AddMinutes(-10)),
            ("cccccccccccc", now.AddHours(-5)),
            ("cccccccccccc.4711.tmp", now.AddHours(-5)),
            ("notes", now.AddDays(-2)),
        };

        var stale = PersonaPluginOutput.StaleFolders(folders, currentHash: "cccccccccccc", now);

        Assert.Equal(["aaaaaaaaaaaa"], stale);
    }
}
