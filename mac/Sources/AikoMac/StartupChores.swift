import AikoKit

import Foundation

/// What Aiko puts right once at every start. None of it draws anything: it repairs what an update
/// or a move of the app left stale. The twin of StartupChores.cs.
@MainActor
enum StartupChores {
    /// Our own line goes stale on its own: the status line names the bridge by its full path, and
    /// a copy dragged from the disk image into Applications, or an update, moves it. A stale line
    /// runs nothing and says nothing about it, so it is put right at startup.
    ///
    /// Only lines that are already ours are touched. Somebody who answered "not now" in the wizard
    /// keeps that answer, and a settings file with no line of ours in it is not ours to write to.
    static func repairOurStatusLines(_ environments: EnvironmentSettings) {
        guard let bridge = BridgePath.current() else { return }

        for environment in environments.environments {
            for folder in environment.configDirectories
            where ClaudeSettingsFile.repairBridge(folder, bridge).changed {
                Log.write("our status line was stale in \((folder as NSString).lastPathComponent) and was put right")
            }
        }
    }

    /// A session that never sent SessionEnd leaves its file behind (D-206). A day later it goes.
    static func clearStaleActivity() {
        let folder = Store.folders.activityFolder
        let manager = FileManager.default
        guard let names = try? manager.contentsOfDirectory(atPath: folder) else { return }

        for name in names {
            let path = (folder as NSString).appendingPathComponent(name)
            guard let written = (try? manager.attributesOfItem(atPath: path))?[.modificationDate] as? Date,
                  ActivityRecord.isStale(written, Date()) else { continue }
            try? manager.removeItem(atPath: path)
        }
    }

    /// What an update of Aiko brings with it and Claude Code does not pick up by itself.
    static func refreshPluginsAndCommands(_ environments: EnvironmentSettings) {
        // An update can bring a new persona text, and on macOS the app itself moves: the marketplace
        // names the bridge by its full path, so a copy dragged from the disk image into Applications
        // leaves Claude Code with a command that points at nothing. When the persona is off
        // everywhere this reads a few files and does nothing else.
        PluginSync.request("startup")

        // An update brings a new shim. The copies in PATH are refreshed, and only when the person
        // set commands up: the folder never appears on its own.
        if CommandFolder.isSetUp {
            CommandFolder.sync(environments)
        }
    }
}
