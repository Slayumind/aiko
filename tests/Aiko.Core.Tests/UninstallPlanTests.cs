using Aiko.Core;

namespace Aiko.Core.Tests;

/// Removing Aiko is the one place where the app deletes folders. These cases name every folder it
/// may delete and, more important, the ones it may not: the accounts and the history in the Claude
/// Code folders were never Aiko's to remove.
public class UninstallPlanTests
{
    private static readonly AikoFolders Windows = AikoFolders.Windows(
        @"C:\Users\someone\AppData\Roaming", @"C:\Users\someone\AppData\Local");

    private static readonly AikoFolders Mac = AikoFolders.MacOS(
        "/Users/someone/Library/Application Support", "/Users/someone/Library/Caches");

    [Fact]
    public void Two_folders_go_and_no_others()
    {
        Assert.Equal(
            [@"C:\Users\someone\AppData\Roaming\Aiko", @"C:\Users\someone\AppData\Local\Aiko"],
            UninstallPlan.FoldersToDelete(Windows));

        Assert.Equal(
            ["/Users/someone/Library/Application Support/Aiko", "/Users/someone/Library/Caches/Aiko"],
            UninstallPlan.FoldersToDelete(Mac));
    }

    [Fact]
    public void Aikos_own_folders_may_go()
    {
        foreach (var folder in UninstallPlan.FoldersToDelete(Windows))
        {
            Assert.True(UninstallPlan.MayDelete(Windows, folder));
        }

        foreach (var folder in UninstallPlan.FoldersToDelete(Mac))
        {
            Assert.True(UninstallPlan.MayDelete(Mac, folder));
        }
    }

    [Theory]
    [InlineData(@"C:\Users\someone\.claude")]
    [InlineData(@"C:\Users\someone\.claude-work")]
    [InlineData(@"C:\Users\someone\.claude.json")]
    [InlineData(@"C:\Users\someone")]
    [InlineData(@"C:\Users\someone\AppData\Roaming")]
    [InlineData(@"C:\Users\someone\AppData\Local")]
    [InlineData(@"C:\Users\someone\AppData\Local\Slayumind.Aiko")]
    [InlineData(@"C:\")]
    [InlineData("")]
    [InlineData("   ")]
    public void Nothing_of_somebody_elses_may_go_on_Windows(string path)
    {
        Assert.False(UninstallPlan.MayDelete(Windows, path));
    }

    [Theory]
    [InlineData("/Users/someone/.claude")]
    [InlineData("/Users/someone/.claude-aiko")]
    [InlineData("/Users/someone/.zshrc")]
    [InlineData("/Users/someone/.zshrc.aiko-backup")]
    [InlineData("/Users/someone")]
    [InlineData("/Users/someone/Library")]
    [InlineData("/Users/someone/Library/Caches")]
    [InlineData("/Applications")]
    [InlineData("/Applications/Aiko.app")]
    [InlineData("/")]
    public void Nothing_of_somebody_elses_may_go_on_macOS(string path)
    {
        Assert.False(UninstallPlan.MayDelete(Mac, path));
    }

    /// Aiko removes its two folders whole. A folder inside one of them has no delete of its own,
    /// so a caller that names it is asking for something the plan does not do.
    [Theory]
    [InlineData(@"C:\Users\someone\AppData\Local\Aiko\bin")]
    [InlineData(@"C:\Users\someone\AppData\Local\Aiko\marketplace")]
    [InlineData(@"C:\Users\someone\AppData\Roaming\Aiko\settings.json")]
    public void A_folder_inside_ours_is_not_deleted_on_its_own(string path)
    {
        Assert.False(UninstallPlan.MayDelete(Windows, path));
    }

    /// People type paths with a trailing separator, and Windows does not care about case.
    [Fact]
    public void The_same_folder_written_differently_is_still_ours()
    {
        Assert.True(UninstallPlan.MayDelete(Windows, @"C:\Users\someone\AppData\Local\Aiko\"));
        Assert.True(UninstallPlan.MayDelete(Windows, @"c:\users\someone\appdata\local\aiko"));
        Assert.True(UninstallPlan.MayDelete(Mac, "/Users/someone/Library/Caches/Aiko/"));
    }
}
