using Aiko.Core.Terminal;

namespace Aiko.Core.Tests;

/// The settings change while the terminal is open. Renaming an environment hid all its sessions:
/// they kept the old name, and the open window wrote that back (review of 2026-10-28).
public class TerminalSessionsFollowTests
{
    private static readonly TerminalSessions Two = TerminalSessions.Empty
        .Add("a1", "Aiko", "/work/aiko", "aiko 1", null, PermissionMode.Manual, 1)
        .Add("w1", "Work", "/work/site", "site 1", null, PermissionMode.Manual, 2);

    private static EnvironmentSettings Settings(params (string Name, string Folder)[] environments) =>
        new([.. environments.Select(environment => new AikoEnvironment(environment.Name, [environment.Folder]))]);

    [Fact]
    public void A_renamed_environment_is_found_by_its_folder_of_Claude_Code()
    {
        var followed = Two.Follow(
            Settings(("Aiko", "C:/u/.claude"), ("Work", "C:/u/.claude-work")),
            Settings(("Aiko", "C:/u/.claude"), ("Job", "C:/u/.claude-work")));

        Assert.Equal("Job", followed.Find("w1")!.Environment);
        Assert.Equal("Aiko", followed.Find("a1")!.Environment);
    }

    [Fact]
    public void Settings_without_a_rename_leave_the_sessions_as_they_are()
    {
        var same = Settings(("Aiko", "C:/u/.claude"), ("Work", "C:/u/.claude-work"));

        Assert.Equal(Two.Sessions, Two.Follow(same, same).Sessions);
    }

    [Fact]
    public void A_removed_environment_takes_nothing_over()
    {
        var followed = Two.Follow(
            Settings(("Aiko", "C:/u/.claude"), ("Work", "C:/u/.claude-work")),
            Settings(("Aiko", "C:/u/.claude")));

        Assert.Equal("Work", followed.Find("w1")!.Environment);
    }
}
