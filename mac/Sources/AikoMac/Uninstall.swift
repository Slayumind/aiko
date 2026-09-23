import AikoKit
import Foundation

/// What Aiko does on its way out.
///
/// The promise on the first run was that removing Aiko puts everything back, and this is where that
/// promise is kept. macOS has no uninstaller to keep it for us: dragging an app to the Bin leaves
/// the folders, the login item and the line in the shell profile behind, so Aiko does that work
/// itself while it is still running. Nothing here asks anything: the person already answered.
///
/// The twin of Uninstall.cs, which the Windows installer calls instead.
enum Uninstall {
    /// How long Claude Code is given to forget the plugins. The person is watching a window, and
    /// the record of a plugin that no longer loads is a small thing to leave behind. The same
    /// twenty seconds the Windows uninstaller allows (D-205).
    private static let pluginBudget: TimeInterval = 20

    /// What is left for the person to do, if anything.
    enum Outcome: Sendable, Equatable {
        /// Everything is gone, the app is in the Bin.
        case done

        /// Everything is gone but the app itself: it lives somewhere Aiko cannot move it from.
        case appStays
    }

    /// Whether Aiko can put its own bundle in the Bin. Checked before the person says yes, so the
    /// window can say up front that the app will have to be dragged by hand.
    ///
    /// Two cases say no: a bundle in a folder this user cannot write to, and a copy opened straight
    /// from a disk image or the Downloads folder, which macOS runs from a read-only place of its
    /// own (App Translocation).
    static var canBinTheApp: Bool {
        let bundle = Bundle.main.bundleURL.path
        if bundle.contains("/AppTranslocation/") { return false }

        let manager = FileManager.default
        let parent = (bundle as NSString).deletingLastPathComponent
        return manager.isWritableFile(atPath: bundle) && manager.isWritableFile(atPath: parent)
    }

    /// Takes everything back, in the one order that leaves nothing half done: first what belongs to
    /// Claude Code, then what Aiko put outside itself, then Aiko's own files, and the app last.
    ///
    /// Runs off the window's thread: Claude Code may take seconds to answer.
    static func everything() -> Outcome {
        let deadline = Date().addingTimeInterval(pluginBudget)
        let folders = claudeFoldersAikoMayHaveTouched()

        // Somebody else's file first, while everything Aiko needs is still in place: the status
        // line and the session hook point at a bridge that is about to disappear.
        for folder in folders {
            _ = ClaudeSettingsFile.removeAiko(folder)
        }

        // Before the marketplace folder goes with the rest: Claude Code reads it while it forgets
        // the plugins.
        PluginSync.removeNow(folders, "uninstall", deadline: deadline)

        // The block in the profile and the folder it points at, together. The copy of the profile
        // stays: it is the person's own file (ZshProfile.backupSuffix).
        CommandFolder.remove()

        // Before the bundle moves: macOS knows the login item by where the app is now.
        LoginItem.set(false)

        let outcome = binTheApp() ? Outcome.done : .appStays

        // The last line: after this the folder the log lives in is gone, and writing would make it
        // again.
        Log.write("uninstall: \(folders.count) Claude Code folders put back, "
            + "app \(outcome == .done ? "in the Bin" : "left in place")")
        removeOwnFolders()

        return outcome
    }

    /// Every folder Aiko knows about, and then every Claude Code folder in the home directory.
    ///
    /// The settings may be gone or may never have listed a folder the person added by hand, and a
    /// status line pointing at a program that no longer exists would break their prompt.
    private static func claudeFoldersAikoMayHaveTouched() -> [String] {
        var folders: [String] = []

        func add(_ folder: String) {
            if !folders.contains(where: { RealClaude.sameFolder($0, folder) }) {
                folders.append(folder)
            }
        }

        for folder in Store.environments().environments.flatMap(\.configDirectories) {
            add(folder)
        }

        for folder in ClaudeFolders.find() {
            add(folder.fullPath)
        }

        return folders
    }

    /// Only Aiko's own two folders, and only through the guard that says which they are.
    private static func removeOwnFolders() {
        for folder in UninstallPlan.foldersToDelete(Store.folders)
        where UninstallPlan.mayDelete(Store.folders, folder) {
            try? FileManager.default.removeItem(atPath: folder)
        }
    }

    /// The app moves itself to the Bin. A running program survives that: its files are already
    /// open. If the Bin refuses, the app stays where it is and the page says so — deleting a
    /// program outright, with no way back, is not something Aiko does on somebody's behalf.
    private static func binTheApp() -> Bool {
        guard canBinTheApp else { return false }

        do {
            try FileManager.default.trashItem(at: Bundle.main.bundleURL, resultingItemURL: nil)
            return true
        } catch {
            return false
        }
    }
}
