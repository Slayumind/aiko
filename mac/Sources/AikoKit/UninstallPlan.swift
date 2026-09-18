import Foundation

/// What Aiko deletes when it removes itself, and what it must never touch.
///
/// Removing Aiko deletes folders, and a mistake here costs somebody their accounts: the Claude Code
/// folders sit next to Aiko's own and hold the sign-in and the history Aiko never made. So the list
/// is short, it is written once for both systems, and everything outside it is refused by name.
///
/// The twin of UninstallPlan.cs.
public enum UninstallPlan {
    /// Aiko's own two folders, and never a third. Everything Aiko writes lives under them: the
    /// settings and the environments under one, the snapshots, commands, plugins and log under the
    /// other.
    public static func foldersToDelete(_ folders: AikoFolders) -> [String] {
        [folders.settingsFolder, folders.localFolder]
    }

    /// The guard in front of every delete. Only a folder from the list above passes: a Claude Code
    /// folder, the shell profile, the home folder and the base folders themselves all answer false,
    /// and so does a folder inside ours, because Aiko removes the two whole and nothing in parts.
    public static func mayDelete(_ folders: AikoFolders, _ path: String) -> Bool {
        guard !path.trimmingCharacters(in: .whitespaces).isEmpty else { return false }
        return foldersToDelete(folders).contains { RealClaude.sameFolder($0, path) }
    }
}
