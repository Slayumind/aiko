import Foundation

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
public struct AikoFolders: Sendable, Equatable {
    public let platform: PlatformConventions
    public let settingsBase: String
    public let localBase: String

    public init(_ platform: PlatformConventions, _ settingsBase: String, _ localBase: String) {
        self.platform = platform
        self.settingsBase = settingsBase
        self.localBase = localBase
    }

    public static let appFolderName = "Aiko"

    /// Velopack keeps the installed app in this folder under the local base across updates. Windows
    /// only: on macOS the app is a bundle in /Applications and nothing under the bases points at it.
    public static let installFolderName = "Slayumind.Aiko"

    public static func windows(_ appData: String, _ localAppData: String) -> AikoFolders {
        AikoFolders(.windows, appData, localAppData)
    }

    /// The person's choices go to Application Support, which Time Machine backs up, and what Aiko
    /// makes for itself to Caches, which macOS may empty (D-250).
    public static func macOS(_ applicationSupport: String, _ caches: String) -> AikoFolders {
        AikoFolders(.macOS, applicationSupport, caches)
    }

    /// The folders of the person running this process, as macOS hands them out.
    public static func forThisMac() -> AikoFolders {
        macOS(library(.applicationSupportDirectory), library(.cachesDirectory))
    }

    /// The person's choices: settings, environments, persona. Not a cache.
    public var settingsFolder: String { platform.join(settingsBase, AikoFolders.appFolderName) }

    /// What Aiko makes for itself: snapshots, commands, plugins, the log.
    public var localFolder: String { platform.join(localBase, AikoFolders.appFolderName) }

    public var settingsFile: String { platform.join(settingsFolder, "settings.json") }

    public var environmentsFile: String { platform.join(settingsFolder, "environments.json") }

    /// Its own file: the bridge reads the persona to build the plugin, and the shim does not need it.
    public var personaFile: String { platform.join(settingsFolder, "persona.json") }

    public var installIdFile: String { platform.join(settingsFolder, "install-id") }

    public var reportedPeriodsFile: String { platform.join(settingsFolder, "reported.json") }

    /// The limit snapshots the bridge writes, one file per config folder.
    public var snapshotsFolder: String { platform.join(localFolder, "environments") }

    public func snapshotFile(_ snapshotName: String) -> String {
        platform.join(snapshotsFolder, snapshotName + ".json")
    }

    /// A folder of its own, because the tray reads every file in the snapshots folder as a snapshot.
    public var activityFolder: String { platform.join(localFolder, "activity") }

    /// The last answers of the usage API.
    public var directCacheFolder: String { platform.join(localFolder, "direct") }

    /// The folder in PATH with the shim and the launch commands.
    public var commandsFolder: String { platform.join(localFolder, "bin") }

    public var marketplaceFolder: String { platform.join(localFolder, "marketplace") }

    public var marketplaceFile: String { AikoMarketplace.fileIn(platform, marketplaceFolder) }

    /// The persona plugins the bridge builds, one folder per config folder inside.
    public var personaPluginsFolder: String { platform.join(localFolder, "plugins") }

    public var logFile: String { platform.join(localFolder, "log.txt") }

    /// The installed app itself, as Velopack lays it out on Windows.
    public var installedAppFolder: String {
        platform.join(localBase, AikoFolders.installFolderName, "current")
    }

    private static func library(_ directory: FileManager.SearchPathDirectory) -> String {
        FileManager.default.urls(for: directory, in: .userDomainMask).first?.path
            ?? NSHomeDirectory() + "/Library"
    }
}
