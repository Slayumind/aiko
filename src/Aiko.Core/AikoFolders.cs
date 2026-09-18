namespace Aiko.Core;

/// Where Aiko keeps its files on this computer.
///
/// The app, the bridge and the shim all read and write the same folders, and each of them used to
/// build the paths on its own. A name changed in one copy would split them quietly: the bridge
/// writing snapshots where the tray never looks. So the layout lives here, and each program only
/// says which system it runs on and where the user's two base folders are.
///
/// The names under the base folders are the same on both systems, so the Windows tray and the Mac
/// tray read the same file of the same environment (D-250). Only the bases differ:
/// %APPDATA% and %LOCALAPPDATA% on Windows, ~/Library/Application Support and ~/Library/Caches on
/// macOS.
public sealed record AikoFolders(PlatformConventions Platform, string SettingsBase, string LocalBase)
{
    public const string AppFolderName = "Aiko";

    /// Velopack keeps the installed app in this folder under the local base across updates, so a
    /// command that points into it never has to change. A changed command stops Claude Code from
    /// running it until the person accepts it again.
    public const string InstallFolderName = "Slayumind.Aiko";

    public static AikoFolders Windows(string appData, string localAppData) =>
        new(PlatformConventions.Windows, appData, localAppData);

    /// The person's choices go to Application Support, which Time Machine backs up, and what Aiko
    /// makes for itself to Caches, which macOS may empty (D-250).
    public static AikoFolders MacOS(string applicationSupport, string caches) =>
        new(PlatformConventions.MacOS, applicationSupport, caches);

    /// The person's choices: settings, environments, persona. Not a cache.
    public string SettingsFolder => Platform.Join(SettingsBase, AppFolderName);

    /// What Aiko makes for itself: snapshots, commands, plugins, the log.
    public string LocalFolder => Platform.Join(LocalBase, AppFolderName);

    public string SettingsFile => Platform.Join(SettingsFolder, "settings.json");

    public string EnvironmentsFile => Platform.Join(SettingsFolder, "environments.json");

    /// Its own file: the bridge reads the persona to build the plugin, and the shim does not need it.
    public string PersonaFile => Platform.Join(SettingsFolder, "persona.json");

    public string InstallIdFile => Platform.Join(SettingsFolder, "install-id");

    public string ReportedPeriodsFile => Platform.Join(SettingsFolder, "reported.json");

    /// The limit snapshots the bridge writes, one file per config folder.
    public string SnapshotsFolder => Platform.Join(LocalFolder, "environments");

    public string SnapshotFile(string snapshotName) => Platform.Join(SnapshotsFolder, snapshotName + ".json");

    /// A folder of its own, because the tray reads every file in the snapshots folder as a limit snapshot.
    public string ActivityFolder => Platform.Join(LocalFolder, "activity");

    /// The last answers of the usage API.
    public string DirectCacheFolder => Platform.Join(LocalFolder, "direct");

    /// The folder in PATH with the shim and the launch commands.
    public string CommandsFolder => Platform.Join(LocalFolder, "bin");

    public string MarketplaceFolder => Platform.Join(LocalFolder, "marketplace");

    public string MarketplaceFile => AikoMarketplace.FileIn(Platform, MarketplaceFolder);

    /// The persona plugins the bridge builds, one folder per config folder inside.
    public string PersonaPluginsFolder => Platform.Join(LocalFolder, "plugins");

    public string LogFile => Platform.Join(LocalFolder, "log.txt");

    /// The installed app itself, as Velopack lays it out. Windows only: on macOS the app is a
    /// bundle in /Applications and nothing under the base folders points at it.
    public string InstalledAppFolder => Platform.Join(LocalBase, InstallFolderName, "current");
}
