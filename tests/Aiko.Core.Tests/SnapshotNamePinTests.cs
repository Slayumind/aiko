using Aiko.Core;

namespace Aiko.Core.Tests;

/// The snapshot name is part of the files already on people's disks. The bridge writes a file under
/// this name and the tray looks for the same name, so these cases must never change.
public class SnapshotNamePinTests
{
    [Theory]
    [InlineData(@"C:\Users\someone\.claude", "claude")]
    [InlineData(@"C:\Users\someone\.claude-work", "claude-work")]
    [InlineData(@"C:\Users\Александр Иванов\.claude-osnovnaya", "claude-osnovnaya")]
    [InlineData(@"D:\Claude Accounts\.claude-work-2", "claude-work-2")]
    public void A_typical_Windows_folder_keeps_the_name_it_has_today(string directory, string expected)
    {
        Assert.Equal(expected, SnapshotName.For(directory));
    }

    [Theory]
    [InlineData(@"C:\a\.claude-work")]
    [InlineData("C:/a/.claude-work")]
    [InlineData(@"C:\a\.claude-work\")]
    [InlineData("C:/a/.claude-work/")]
    [InlineData(@"C:\a/.claude-work\\")]
    public void The_same_folder_gets_the_same_name_whatever_separators_it_is_written_with(string directory)
    {
        Assert.Equal("claude-work", SnapshotName.For(directory));
    }

    [Theory]
    [InlineData(@"C:\")]
    [InlineData("C:")]
    [InlineData(@"\")]
    public void A_drive_or_a_root_with_no_folder_name_gets_the_default_name(string directory)
    {
        Assert.Equal(SnapshotName.Default, SnapshotName.For(directory));
    }

    [Fact]
    public void A_folder_on_a_network_share_is_named_after_its_last_part()
    {
        Assert.Equal("claude-team", SnapshotName.For(@"\\server\share\users\someone\.claude-team"));
    }
}
