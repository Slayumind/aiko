using Aiko.Core;

namespace Aiko.Core.Tests;

/// The bridge and the app have to agree on which folder a run belongs to.
///
/// With CLAUDE_CONFIG_DIR unset the bridge wrote default.json and the app looked for the folder's
/// own name. One account and nothing configured is the commonest setup there is, and it would
/// never have shown a single limit.
public class ClaudeConfigFolderTests
{
    private const string Home = @"C:\Users\someone";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Without_the_variable_it_is_the_folder_Claude_Code_uses_by_default(string? variable)
    {
        Assert.Equal(Path.Combine(Home, ".claude"), ClaudeConfigFolder.Resolve(variable, Home));
    }

    [Fact]
    public void A_set_variable_is_taken_as_it_is()
    {
        Assert.Equal(
            @"C:\Users\someone\.claude-personal",
            ClaudeConfigFolder.Resolve(@"C:\Users\someone\.claude-personal", Home));
    }

    [Fact]
    public void Surrounding_spaces_in_the_variable_do_not_make_another_folder()
    {
        Assert.Equal(@"D:\claude", ClaudeConfigFolder.Resolve("  D:\\claude  ", Home));
    }

    [Fact]
    public void The_bridge_and_the_app_arrive_at_the_same_file_name()
    {
        // The bridge sees no variable. The app knows the environment by the folder the wizard
        // found. Both have to name the same snapshot file, or the card waits forever.
        var fromBridge = SnapshotName.For(ClaudeConfigFolder.Resolve(null, Home));
        var fromApp = SnapshotName.For(Path.Combine(Home, ".claude"));

        Assert.Equal(fromApp, fromBridge);
        Assert.NotEqual(SnapshotName.Default, fromBridge);
    }

    [Fact]
    public void The_app_finds_what_the_bridge_wrote_for_a_default_folder()
    {
        var folder = Path.Combine(Home, ".claude");
        var settings = new EnvironmentSettings([new AikoEnvironment("Work", [folder])]);
        var written = new LimitSnapshot(
            SnapshotName.For(ClaudeConfigFolder.Resolve(null, Home)),
            LimitSource.StatusLine,
            DateTimeOffset.Now,
            [new LimitWindow(LimitKind.FiveHour, 40, DateTimeOffset.Now.AddHours(2))]);

        var combined = EnvironmentSnapshots.Combine(
            settings,
            new Dictionary<string, LimitSnapshot> { [written.Environment] = written });

        Assert.True(combined.Single().HasData);
    }
}
