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

    /// The one thing in Aiko's folders the person cannot make again is the memory, so it may stay
    /// behind. Only that file: its lock and a ".bad" copy next to it go with the rest, because no
    /// later Aiko reads them and the window promises that one file stays.
    public static func filesToKeep(_ folders: AikoFolders, keepMemory: Bool) -> [String] {
        keepMemory ? [folders.memoryFile] : []
    }

    /// Whether the text of memory.json holds something to keep: a fact, or a record set aside.
    /// Nil is a file that is not there. A file that cannot be read as a memory is worth keeping
    /// too: it may come from a newer Aiko or from a hand edit, and the facts are still in it.
    public static func memoryWorthKeeping(_ memoryJson: String?) -> Bool {
        guard let memoryJson, !MemoryText.isBlank(memoryJson) else { return false }

        let read = MemoryFile.fromJson(memoryJson)
        return read.unreadable || !MemoryPage.from(read.memory).empty
    }

    /// Whether this removal keeps the memory. The answer is the person's own, and nil when
    /// nobody could be asked: the Windows installer's hook runs with no window. Then the memory
    /// stays, because a person who was not asked must not lose it. An empty memory is never kept.
    public static func keepsMemory(_ answer: Bool?, worthKeeping: Bool) -> Bool {
        (answer ?? true) && worthKeeping
    }

    /// Whether a file stays in this folder. Such a folder is emptied around the file, not deleted.
    public static func keepsSomethingIn(_ folders: AikoFolders, _ folder: String, keepMemory: Bool) -> Bool {
        filesToKeep(folders, keepMemory: keepMemory).contains {
            RealClaude.sameFolder(split(folders, $0).parent, folder)
        }
    }

    /// The guard in front of every delete of a whole folder. Only a folder from the list above
    /// passes: a Claude Code folder, the shell profile, the home folder and the base folders
    /// themselves all answer false, and so does a folder inside ours. A folder that keeps a file
    /// answers false too: it is emptied through mayDeleteInside.
    public static func mayDelete(_ folders: AikoFolders, _ path: String, keepMemory: Bool) -> Bool {
        guard !path.trimmingCharacters(in: .whitespaces).isEmpty else { return false }
        return foldersToDelete(folders).contains { RealClaude.sameFolder($0, path) }
            && !keepsSomethingIn(folders, path, keepMemory: keepMemory)
    }

    /// The guard for emptying a folder that keeps a file. Only something that lies directly in
    /// that folder passes, and never the file that stays. "." and ".." are refused by name: they
    /// look like something inside the folder and mean the folder itself and the one above it.
    /// Windows drops dots and spaces at the end of a name, so "memory.json." is the memory too.
    public static func mayDeleteInside(_ folders: AikoFolders, _ path: String, keepMemory: Bool) -> Bool {
        guard !path.trimmingCharacters(in: .whitespaces).isEmpty else { return false }

        let (parent, fullName) = split(folders, path)
        var name = fullName
        while let last = name.last, last == "." || last == " " {
            name.removeLast()
        }

        return !name.isEmpty
            && foldersToDelete(folders).contains { RealClaude.sameFolder($0, parent) }
            && keepsSomethingIn(folders, parent, keepMemory: keepMemory)
            && !filesToKeep(folders, keepMemory: keepMemory).contains {
                split(folders, $0).name.caseInsensitiveCompare(name) == .orderedSame
            }
    }

    /// Cuts a path at its last separator. Windows reads '/' as a separator too; on macOS a
    /// backslash is a letter of the name like any other.
    private static func split(_ folders: AikoFolders, _ path: String) -> (parent: String, name: String) {
        let separators: Set<Character> = folders.platform.directorySeparator == "/" ? ["/"] : ["\\", "/"]
        var trimmed = path.trimmingCharacters(in: .whitespaces)
        while let last = trimmed.last, separators.contains(last) {
            trimmed.removeLast()
        }

        guard let cut = trimmed.lastIndex(where: { separators.contains($0) }) else {
            return ("", trimmed)
        }

        return (String(trimmed[..<cut]), String(trimmed[trimmed.index(after: cut)...]))
    }
}
