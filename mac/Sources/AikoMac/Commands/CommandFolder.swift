import AikoKit
import Foundation

/// The folder on PATH that holds the shim and the launch commands: ~/Library/Caches/Aiko/bin.
///
/// It lives outside the app bundle, because an update replaces the bundle and PATH has to point at
/// something that stays. The shim is copied in from the bundle, and each command is a hard link to
/// that copy: one file on disk, many names, and the shim reads the name it was called by.
///
/// Nothing here runs until the person sets up a command or a folder binding (D-158). Removing Aiko
/// takes the block out of ~/.zshrc again.
///
/// The twin of Commands/CommandFolder.cs. Where Windows writes the user PATH in the registry and
/// tells Explorer about it, macOS has no such place at all: a folder under the home directory
/// reaches a shell's PATH only through the shell profile (spike S4, 2026-09-18).
enum CommandFolder {
    static let folder = Store.folders.commandsFolder

    private static let shimName = CommandLinks.shimFileName(.macOS)

    static var profilePath: String {
        (Store.home as NSString).appendingPathComponent(ZshProfile.fileName)
    }

    static var isSetUp: Bool {
        FileManager.default.fileExists(atPath: path(shimName))
    }

    /// The shim shipped with this copy of Aiko, or nil when it is missing, which only happens in a
    /// build that was not put into a bundle.
    static func shimInInstall() -> String? {
        guard let beside = Bundle.main.executablePath.map({ ($0 as NSString).deletingLastPathComponent })
        else {
            return nil
        }

        for name in [shimName, "aiko-shim"] {
            let path = (beside as NSString).appendingPathComponent(name)
            if FileManager.default.isExecutableFile(atPath: path) {
                return path
            }
        }

        return nil
    }

    /// Makes the folder hold the current shim and exactly the commands of these environments.
    /// False when there is no shim to copy.
    @discardableResult
    static func sync(_ settings: EnvironmentSettings) -> Bool {
        guard let source = shimInInstall() else {
            Log.write("commands: no shim beside the app, nothing set up")
            return false
        }

        let manager = FileManager.default
        do {
            try manager.createDirectory(atPath: folder, withIntermediateDirectories: true)

            let shim = path(shimName)
            let shimChanged = try copyIfDifferent(source, shim)

            let present = (try? manager.contentsOfDirectory(atPath: folder)) ?? []
            let plan = CommandLinks.plan(.macOS, settings, present)

            // A new shim means every link still points at the old file. Links are cheap, so all of
            // them are made again.
            let wanted = settings.environments.map { CommandLinks.fileNameFor(.macOS, $0.command) }
            let toRemove = shimChanged ? present.filter { $0 != shimName } : plan.toRemove
            let toAdd = shimChanged ? Array(Set(wanted)) : plan.toAdd

            for name in toRemove {
                try? manager.removeItem(atPath: path(name))
            }

            for name in toAdd {
                link(shim, path(name))
            }

            Log.write("commands: shim \(shimChanged ? "copied" : "current"), "
                + "\(toAdd.count) added, \(toRemove.count) removed")
            return true
        } catch {
            Log.write("commands: could not set up the folder (\(error.localizedDescription))")
            return false
        }
    }

    // ---- the line in ~/.zshrc ----

    /// The line the checklist shows before it is written.
    static var exportLine: String { ZshProfile.exportLine(folder) }

    static func isOnPath() -> Bool {
        guard let text = read(profilePath) else { return false }
        return ZshProfile.holds(text, folder)
    }

    /// Puts the folder first on PATH for every new shell. Terminals that are already open keep the
    /// PATH they started with, as on Windows.
    @discardableResult
    static func addToPath() -> Bool {
        changeProfile { ZshProfile.add($0, folder) }
    }

    @discardableResult
    static func removeFromPath() -> Bool {
        changeProfile { ZshProfile.remove($0) }
    }

    /// Everything Aiko put here goes: the block in the profile and the folder.
    static func remove() {
        removeFromPath()
        try? FileManager.default.removeItem(atPath: folder)
    }

    /// A copy of the profile is made once, before the first change, the same promise Aiko makes
    /// about somebody else's settings.json.
    private static func changeProfile(_ change: (String) -> String?) -> Bool {
        let path = profilePath
        let before = read(path) ?? ""
        guard let after = change(before) else { return false }

        do {
            if FileManager.default.fileExists(atPath: path) {
                let backup = path + ".aiko-backup"
                if !FileManager.default.fileExists(atPath: backup) {
                    try FileManager.default.copyItem(atPath: path, toPath: backup)
                }
            }

            let temporary = path + ".aiko.tmp"
            try after.write(toFile: temporary, atomically: false, encoding: .utf8)
            _ = try FileManager.default.replaceItemAt(
                URL(fileURLWithPath: path), withItemAt: URL(fileURLWithPath: temporary))

            Log.write("commands: \(ZshProfile.fileName) changed")
            return true
        } catch {
            Log.write("commands: could not change \(ZshProfile.fileName) (\(error.localizedDescription))")
            return false
        }
    }

    // ---- the files ----

    private static func path(_ name: String) -> String {
        (folder as NSString).appendingPathComponent(name)
    }

    private static func read(_ path: String) -> String? {
        try? String(contentsOfFile: path, encoding: .utf8)
    }

    private static func copyIfDifferent(_ source: String, _ target: String) throws -> Bool {
        let manager = FileManager.default
        let from = try manager.attributesOfItem(atPath: source)
        if let to = try? manager.attributesOfItem(atPath: target),
           to[.size] as? Int == from[.size] as? Int,
           to[.modificationDate] as? Date == from[.modificationDate] as? Date {
            return false
        }

        try? manager.removeItem(atPath: target)
        try manager.copyItem(atPath: source, toPath: target)
        try? manager.setAttributes([.modificationDate: from[.modificationDate] as Any], ofItemAtPath: target)
        return true
    }

    private static func link(_ shim: String, _ link: String) {
        let manager = FileManager.default
        guard !manager.fileExists(atPath: link) else { return }

        do {
            try manager.linkItem(atPath: shim, toPath: link)
        } catch {
            // A file system without hard links still works, one full copy per command.
            try? manager.copyItem(atPath: shim, toPath: link)
        }
    }
}
