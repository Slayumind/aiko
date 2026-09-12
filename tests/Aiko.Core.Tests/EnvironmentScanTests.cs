using Aiko.Core;

namespace Aiko.Core.Tests;

public class EnvironmentScanTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 12, 18, 0, 0, TimeSpan.Zero);

    private static ClaudeFolder Folder(string name, bool credentials = true, double? daysAgo = null) =>
        new($@"C:\Users\someone\{name}", name)
        {
            HasCredentials = credentials,
            LastUsed = daysAgo is { } days ? Now.AddDays(-days) : null,
        };

    [Fact]
    public void A_folder_without_credentials_is_not_an_environment()
    {
        // .claude_pl on this machine: the folder exists and holds nothing at all.
        var found = EnvironmentScan.Pick([Folder(".claude_pl", credentials: false)]);

        Assert.Empty(found);
    }

    [Fact]
    public void Folders_in_recent_use_come_first()
    {
        var found = EnvironmentScan.Pick(
        [
            Folder(".claude-work", daysAgo: 200),
            Folder(".claude-personal", daysAgo: 1),
            Folder(".claude", daysAgo: 30),
        ]);

        Assert.Equal(["Personal", "Main", "Work"], found.Select(f => f.SuggestedName));
    }

    [Fact]
    public void Folders_with_no_known_time_come_last_in_a_steady_order()
    {
        var found = EnvironmentScan.Pick(
        [
            Folder(".claude-zeta"),
            Folder(".claude-alpha"),
            Folder(".claude-personal", daysAgo: 3),
        ]);

        Assert.Equal(["Personal", "Alpha", "Zeta"], found.Select(f => f.SuggestedName));
    }

    [Theory]
    [InlineData(".claude-personal", "Personal")]
    [InlineData(".claude-work", "Work")]
    [InlineData(".claude_pl", "Pl")]
    [InlineData(".claude", "Main")]
    [InlineData("claude", "Main")]
    [InlineData(".claude-", "Main")]
    public void The_name_starts_from_the_folder(string folder, string expected)
    {
        Assert.Equal(expected, EnvironmentScan.SuggestName(folder));
    }

    [Fact]
    public void Two_folders_that_look_the_same_are_both_offered()
    {
        // .claude-work here holds expired tokens and .claude-personal is in daily use, yet both
        // hold the same files. Aiko must not guess which one is real.
        var found = EnvironmentScan.Pick([Folder(".claude-work", daysAgo: 5), Folder(".claude-personal", daysAgo: 5)]);

        Assert.Equal(2, found.Count);
    }

    [Fact]
    public void The_full_path_is_carried_through()
    {
        var found = EnvironmentScan.Pick([Folder(".claude-personal", daysAgo: 1)]);

        Assert.Equal(@"C:\Users\someone\.claude-personal", found[0].FullPath);
    }
}
