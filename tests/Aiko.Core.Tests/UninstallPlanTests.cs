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
            Assert.True(UninstallPlan.MayDelete(Windows, folder, keepMemory: false));
        }

        foreach (var folder in UninstallPlan.FoldersToDelete(Mac))
        {
            Assert.True(UninstallPlan.MayDelete(Mac, folder, keepMemory: false));
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
        Assert.False(UninstallPlan.MayDelete(Windows, path, keepMemory: false));
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
        Assert.False(UninstallPlan.MayDelete(Mac, path, keepMemory: false));
    }

    /// Aiko removes its two folders whole. A folder inside one of them has no delete of its own,
    /// so a caller that names it is asking for something the plan does not do.
    [Theory]
    [InlineData(@"C:\Users\someone\AppData\Local\Aiko\bin")]
    [InlineData(@"C:\Users\someone\AppData\Local\Aiko\marketplace")]
    [InlineData(@"C:\Users\someone\AppData\Roaming\Aiko\settings.json")]
    public void A_folder_inside_ours_is_not_deleted_on_its_own(string path)
    {
        Assert.False(UninstallPlan.MayDelete(Windows, path, keepMemory: false));
    }

    /// People type paths with a trailing separator, and Windows does not care about case.
    [Fact]
    public void The_same_folder_written_differently_is_still_ours()
    {
        Assert.True(UninstallPlan.MayDelete(Windows, @"C:\Users\someone\AppData\Local\Aiko\", keepMemory: false));
        Assert.True(UninstallPlan.MayDelete(Windows, @"c:\users\someone\appdata\local\aiko", keepMemory: false));
        Assert.True(UninstallPlan.MayDelete(Mac, "/Users/someone/Library/Caches/Aiko/", keepMemory: false));
    }

    // ---- keeping the memory ----

    /// The memory is the one thing in Aiko's folders the person cannot make again. These cases say
    /// when there is something to keep. A file Aiko cannot read counts: the facts are still in it.
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("  \n", false)]
    [InlineData("{}", false)]
    [InlineData("""{"schema":1,"nextId":1,"facts":[],"setAside":[]}""", false)]
    [InlineData("""{"schema":1,"nextId":2,"facts":[{"id":"F1","category":"you","text":"Learns Swift","seen":"2026-10-05","created":"2026-10-05"}],"setAside":[]}""", true)]
    [InlineData("""{"schema":1,"nextId":1,"facts":[],"setAside":[{"note":"typed by hand"}]}""", true)]
    [InlineData("""{"schema":1,"facts":[{"text":"no id, so it is set aside"}]}""", true)]
    [InlineData("not a memory at all", true)]
    [InlineData("[]", true)]
    [InlineData("""{"schema":99,"facts":[]}""", true)]
    public void A_memory_with_something_in_it_is_worth_keeping(string? memoryJson, bool worth)
    {
        Assert.Equal(worth, UninstallPlan.MemoryWorthKeeping(memoryJson));
    }

    /// The answer is null when nobody could be asked: the installer's hook has no window. Then the
    /// memory stays. An empty memory is never kept, whatever the answer.
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(null, true, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(null, false, false)]
    [InlineData(false, false, false)]
    public void The_memory_stays_unless_the_person_says_no(bool? answer, bool worthKeeping, bool kept)
    {
        Assert.Equal(kept, UninstallPlan.KeepsMemory(answer, worthKeeping));
    }

    [Fact]
    public void Only_the_memory_file_stays_and_only_when_it_is_kept()
    {
        Assert.Equal(
            [@"C:\Users\someone\AppData\Roaming\Aiko\memory.json"],
            UninstallPlan.FilesToKeep(Windows, keepMemory: true));
        Assert.Equal(
            ["/Users/someone/Library/Application Support/Aiko/memory.json"],
            UninstallPlan.FilesToKeep(Mac, keepMemory: true));

        Assert.Empty(UninstallPlan.FilesToKeep(Windows, keepMemory: false));
        Assert.Empty(UninstallPlan.FilesToKeep(Mac, keepMemory: false));
    }

    /// The folder with the memory is emptied around the file, so it does not pass the guard for
    /// whole folders. The other folder goes whole as before.
    [Fact]
    public void The_folder_with_the_kept_memory_does_not_go_whole()
    {
        foreach (var folders in new[] { Windows, Mac })
        {
            Assert.True(UninstallPlan.KeepsSomethingIn(folders, folders.SettingsFolder, keepMemory: true));
            Assert.False(UninstallPlan.MayDelete(folders, folders.SettingsFolder, keepMemory: true));

            Assert.False(UninstallPlan.KeepsSomethingIn(folders, folders.LocalFolder, keepMemory: true));
            Assert.True(UninstallPlan.MayDelete(folders, folders.LocalFolder, keepMemory: true));

            Assert.False(UninstallPlan.KeepsSomethingIn(folders, folders.SettingsFolder, keepMemory: false));
            Assert.False(UninstallPlan.KeepsSomethingIn(folders, folders.LocalFolder, keepMemory: false));
        }
    }

    /// Everything next to the memory goes, the lock and the ".bad" copy too: no later Aiko reads
    /// them, and the window says that one file stays.
    [Theory]
    [InlineData(@"C:\Users\someone\AppData\Roaming\Aiko\settings.json")]
    [InlineData(@"C:\Users\someone\AppData\Roaming\Aiko\environments.json")]
    [InlineData(@"C:\Users\someone\AppData\Roaming\Aiko\persona.json")]
    [InlineData(@"C:\Users\someone\AppData\Roaming\Aiko\install-id")]
    [InlineData(@"C:\Users\someone\AppData\Roaming\Aiko\memory.json.lock")]
    [InlineData(@"C:\Users\someone\AppData\Roaming\Aiko\memory.json.bad")]
    [InlineData(@"C:\Users\someone\AppData\Roaming\Aiko\memory.json.4242.tmp")]
    [InlineData(@"C:\Users\someone\AppData\Roaming\Aiko\a folder")]
    [InlineData(@"c:\users\someone\appdata\roaming\aiko\Settings.json\")]
    public void What_lies_next_to_the_kept_memory_goes_on_Windows(string path)
    {
        Assert.True(UninstallPlan.MayDeleteInside(Windows, path, keepMemory: true));
    }

    /// The memory under every spelling Windows reads as the same file, the folder itself, the
    /// folder above it, anything deeper, and everything outside the one folder that keeps a file.
    [Theory]
    [InlineData(@"C:\Users\someone\AppData\Roaming\Aiko\memory.json")]
    [InlineData(@"C:\Users\someone\AppData\Roaming\Aiko\MEMORY.JSON")]
    [InlineData(@"C:\Users\someone\AppData\Roaming\Aiko\memory.json\")]
    [InlineData(@"C:\Users\someone\AppData\Roaming\Aiko\memory.json.")]
    [InlineData(@"C:\Users\someone\AppData\Roaming\Aiko/memory.json")]
    [InlineData(@"C:\Users\someone\AppData\Roaming\Aiko")]
    [InlineData(@"C:\Users\someone\AppData\Roaming\Aiko\")]
    [InlineData(@"C:\Users\someone\AppData\Roaming\Aiko\.")]
    [InlineData(@"C:\Users\someone\AppData\Roaming\Aiko\..")]
    [InlineData(@"C:\Users\someone\AppData\Roaming\Aiko\a folder\..")]
    [InlineData(@"C:\Users\someone\AppData\Roaming\Aiko\a folder/..")]
    [InlineData(@"C:\Users\someone\AppData\Roaming\Aiko\a folder\settings.json")]
    [InlineData(@"C:\Users\someone\AppData\Roaming")]
    [InlineData(@"C:\Users\someone\AppData\Local\Aiko\log.txt")]
    [InlineData(@"C:\Users\someone\AppData\Local\Aiko\bin")]
    [InlineData(@"C:\Users\someone\.claude\settings.json")]
    [InlineData(@"C:\Users\someone\.claude")]
    [InlineData("memory.json")]
    [InlineData("")]
    [InlineData("   ")]
    public void The_kept_memory_and_everything_outside_stay_on_Windows(string path)
    {
        Assert.False(UninstallPlan.MayDeleteInside(Windows, path, keepMemory: true));
    }

    [Theory]
    [InlineData("/Users/someone/Library/Application Support/Aiko/settings.json")]
    [InlineData("/Users/someone/Library/Application Support/Aiko/memory.json.lock")]
    [InlineData("/Users/someone/Library/Application Support/Aiko/memory.json.bad")]
    [InlineData("/Users/someone/Library/Application Support/Aiko/a folder/")]
    public void What_lies_next_to_the_kept_memory_goes_on_macOS(string path)
    {
        Assert.True(UninstallPlan.MayDeleteInside(Mac, path, keepMemory: true));
    }

    [Theory]
    [InlineData("/Users/someone/Library/Application Support/Aiko/memory.json")]
    [InlineData("/Users/someone/Library/Application Support/Aiko/Memory.json")]
    [InlineData("/Users/someone/Library/Application Support/Aiko/memory.json/")]
    [InlineData("/Users/someone/Library/Application Support/Aiko")]
    [InlineData("/Users/someone/Library/Application Support/Aiko/..")]
    [InlineData("/Users/someone/Library/Application Support/Aiko/a folder/settings.json")]
    [InlineData("/Users/someone/Library/Application Support")]
    [InlineData("/Users/someone/Library/Caches/Aiko/log.txt")]
    [InlineData("/Users/someone/.claude/settings.json")]
    [InlineData("/")]
    public void The_kept_memory_and_everything_outside_stay_on_macOS(string path)
    {
        Assert.False(UninstallPlan.MayDeleteInside(Mac, path, keepMemory: true));
    }

    /// With nothing kept, both folders go whole and nothing is deleted in parts, as before.
    [Fact]
    public void With_no_memory_kept_nothing_is_deleted_in_parts()
    {
        Assert.False(UninstallPlan.MayDeleteInside(
            Windows, @"C:\Users\someone\AppData\Roaming\Aiko\settings.json", keepMemory: false));
        Assert.False(UninstallPlan.MayDeleteInside(
            Windows, @"C:\Users\someone\AppData\Roaming\Aiko\memory.json", keepMemory: false));
        Assert.False(UninstallPlan.MayDeleteInside(
            Mac, "/Users/someone/Library/Application Support/Aiko/settings.json", keepMemory: false));

        Assert.True(UninstallPlan.MayDelete(Windows, Windows.SettingsFolder, keepMemory: false));
        Assert.True(UninstallPlan.MayDelete(Mac, Mac.SettingsFolder, keepMemory: false));
    }
}
