namespace Aiko.Core;

/// Where Aiko keeps its files on this computer.
///
/// The app, the bridge and the shim all read and write the same folders, and each of them used to
/// build the paths on its own. A name changed in one copy would split them quietly: the bridge
/// writing snapshots where the tray never looks. So the layout lives here, and each program only
/// says where the user's two base folders are.
///
/// On Windows the settings base is %APPDATA% and the local base is %LOCALAPPDATA%.
public sealed record AikoFolders(string SettingsBase, string LocalBase)
{
    public const string AppFolderName = "Aiko";

    /// Velopack keeps the installed app in this folder under the local base across updates, so a
    /// command that points into it never has to change. A changed command stops Claude Code from
    /// running it until the person accepts it again.
    public const string InstallFolderName = "Slayumind.Aiko";

    /// The person's choices: settings, environments, persona. Not a cache.
    public string SettingsFolder => Path.Combine(SettingsBase, AppFolderName);

    /// What Aiko makes for itself: snapshots, commands, plugins, the log.
    public string LocalFolder => Path.Combine(LocalBase, AppFolderName);

    public string SettingsFile => Path.Combine(SettingsFolder, "settings.json");

    public string EnvironmentsFile => Path.Combine(SettingsFolder, "environments.json");

    /// Its own file: the bridge reads the persona to build the plugin, and the shim does not need it.
    public string PersonaFile => Path.Combine(SettingsFolder, "persona.json");

    public string InstallIdFile => Path.Combine(SettingsFolder, "install-id");

    public string ReportedPeriodsFile => Path.Combine(SettingsFolder, "reported.json");

    /// The limit snapshots the bridge writes, one file per config folder.
    public string SnapshotsFolder => Path.Combine(LocalFolder, "environments");

    public string SnapshotFile(string snapshotName) => Path.Combine(SnapshotsFolder, snapshotName + ".json");

    /// A folder of its own, because the tray reads every file in the snapshots folder as a limit snapshot.
    public string ActivityFolder => Path.Combine(LocalFolder, "activity");

    /// The last answers of the usage API.
    public string DirectCacheFolder => Path.Combine(LocalFolder, "direct");

    /// The folder in PATH with the shim and the launch commands.
    public string CommandsFolder => Path.Combine(LocalFolder, "bin");

    public string MarketplaceFolder => Path.Combine(LocalFolder, "marketplace");

    public string MarketplaceFile => AikoMarketplace.FileIn(MarketplaceFolder);

    /// The persona plugins the bridge builds, one folder per config folder inside.
    public string PersonaPluginsFolder => Path.Combine(LocalFolder, "plugins");

    public string LogFile => Path.Combine(LocalFolder, "log.txt");

    /// The installed app itself, as Velopack lays it out.
    public string InstalledAppFolder => Path.Combine(LocalBase, InstallFolderName, "current");
}
