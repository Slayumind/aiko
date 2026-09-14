using Aiko.Core;

namespace Aiko.Core.Tests;

public class SnapshotNameTests
{
    [Theory]
    [InlineData(@"C:\Users\someone\.claude-personal", "claude-personal")]
    [InlineData(@"C:\Users\someone\.claude", "claude")]
    [InlineData(@"C:\Users\someone\.claude-work\", "claude-work")]
    [InlineData("/home/someone/.claude", "claude")]
    public void The_file_is_named_after_the_config_folder(string directory, string expected)
    {
        Assert.Equal(expected, SnapshotName.For(directory));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Without_the_variable_the_name_is_the_default_one(string? directory)
    {
        Assert.Equal(SnapshotName.Default, SnapshotName.For(directory));
    }

    [Fact]
    public void Characters_that_do_not_belong_in_a_file_name_are_dropped()
    {
        Assert.Equal("claudework", SnapshotName.Clean("claude:work?"));
    }

    [Fact]
    public void A_folder_named_in_Russian_keeps_its_letters()
    {
        // The user's home folder can be in any language, and the file name follows it.
        Assert.Equal("клод", SnapshotName.Clean(".клод"));
    }

    [Fact]
    public void A_name_left_with_nothing_falls_back()
    {
        Assert.Equal(SnapshotName.Default, SnapshotName.Clean("..."));
        Assert.Equal(SnapshotName.Default, SnapshotName.Clean("???"));
    }
}
