using Aiko.Core;

namespace Aiko.Core.Tests;

/// The folders the app, the bridge and the shim share. Every path here is on people's disks
/// already, so each one is written out in full: a changed name would lose their settings or split
/// the bridge from the tray.
public class AikoFoldersTests
{
    private static readonly AikoFolders Folders = AikoFolders.Windows(
        @"C:\Users\someone\AppData\Roaming", @"C:\Users\someone\AppData\Local");

    [Fact]
    public void The_persons_choices_live_under_APPDATA_Aiko()
    {
        Assert.Equal(@"C:\Users\someone\AppData\Roaming\Aiko", Folders.SettingsFolder);
        Assert.Equal(@"C:\Users\someone\AppData\Roaming\Aiko\settings.json", Folders.SettingsFile);
        Assert.Equal(@"C:\Users\someone\AppData\Roaming\Aiko\environments.json", Folders.EnvironmentsFile);
        Assert.Equal(@"C:\Users\someone\AppData\Roaming\Aiko\persona.json", Folders.PersonaFile);
        Assert.Equal(@"C:\Users\someone\AppData\Roaming\Aiko\install-id", Folders.InstallIdFile);
        Assert.Equal(@"C:\Users\someone\AppData\Roaming\Aiko\reported.json", Folders.ReportedPeriodsFile);
    }

    [Fact]
    public void What_Aiko_makes_for_itself_lives_under_LOCALAPPDATA_Aiko()
    {
        Assert.Equal(@"C:\Users\someone\AppData\Local\Aiko", Folders.LocalFolder);
        Assert.Equal(@"C:\Users\someone\AppData\Local\Aiko\environments", Folders.SnapshotsFolder);
        Assert.Equal(@"C:\Users\someone\AppData\Local\Aiko\activity", Folders.ActivityFolder);
        Assert.Equal(@"C:\Users\someone\AppData\Local\Aiko\direct", Folders.DirectCacheFolder);
        Assert.Equal(@"C:\Users\someone\AppData\Local\Aiko\bin", Folders.CommandsFolder);
        Assert.Equal(@"C:\Users\someone\AppData\Local\Aiko\marketplace", Folders.MarketplaceFolder);
        Assert.Equal(
            @"C:\Users\someone\AppData\Local\Aiko\marketplace\.claude-plugin\marketplace.json",
            Folders.MarketplaceFile);
        Assert.Equal(@"C:\Users\someone\AppData\Local\Aiko\plugins", Folders.PersonaPluginsFolder);
        Assert.Equal(@"C:\Users\someone\AppData\Local\Aiko\log.txt", Folders.LogFile);
    }

    [Fact]
    public void The_installed_app_is_where_Velopack_puts_it()
    {
        Assert.Equal(@"C:\Users\someone\AppData\Local\Slayumind.Aiko\current", Folders.InstalledAppFolder);
    }

    [Fact]
    public void A_snapshot_file_is_named_after_the_snapshot()
    {
        Assert.Equal(
            @"C:\Users\someone\AppData\Local\Aiko\environments\claude-work.json",
            Folders.SnapshotFile(SnapshotName.For(@"C:\Users\someone\.claude-work\")));
    }

    [Fact]
    public void A_second_environment_folder_sits_beside_claude()
    {
        Assert.Equal(".claude-work", ClaudeConfigFolder.NamedFolderName("work"));
        Assert.Equal(".claude*", ClaudeConfigFolder.SearchPattern);
    }
}
