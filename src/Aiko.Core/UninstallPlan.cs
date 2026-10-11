namespace Aiko.Core;

/// What Aiko deletes when it removes itself, and what it must never touch.
///
/// Removing Aiko deletes folders, and a mistake here costs somebody their accounts: the Claude Code
/// folders sit next to Aiko's own and hold the sign-in and the history Aiko never made. So the list
/// is short, it is written once for both systems, and everything outside it is refused by name.
public static class UninstallPlan
{
    /// Aiko's own two folders, and never a third. Everything Aiko writes lives under them: the
    /// settings and the environments under one, the snapshots, commands, plugins and log under the
    /// other.
    public static IReadOnlyList<string> FoldersToDelete(AikoFolders folders) =>
        [folders.SettingsFolder, folders.LocalFolder];

    /// The one thing in Aiko's folders the person cannot make again is the memory, so it may stay
    /// behind. Only that file: its lock and a ".bad" copy next to it go with the rest, because no
    /// later Aiko reads them and the window promises that one file stays.
    public static IReadOnlyList<string> FilesToKeep(AikoFolders folders, bool keepMemory) =>
        keepMemory ? [folders.MemoryFile] : [];

    /// Whether the text of memory.json holds something to keep: a fact, or a record set aside.
    /// Null is a file that is not there. A file that cannot be read as a memory is worth keeping
    /// too: it may come from a newer Aiko or from a hand edit, and the facts are still in it.
    public static bool MemoryWorthKeeping(string? memoryJson)
    {
        if (string.IsNullOrWhiteSpace(memoryJson))
        {
            return false;
        }

        var read = MemoryFile.FromJson(memoryJson);
        return read.Unreadable || !MemoryPage.From(read.Memory).Empty;
    }

    /// Whether this removal keeps the memory. The answer is the person's own, and null when
    /// nobody could be asked: the installer's hook runs with no window. Then the memory stays,
    /// because a person who was not asked must not lose it. An empty memory is never kept.
    public static bool KeepsMemory(bool? answer, bool worthKeeping) => (answer ?? true) && worthKeeping;

    /// Whether a file stays in this folder. Such a folder is emptied around the file, not deleted.
    public static bool KeepsSomethingIn(AikoFolders folders, string folder, bool keepMemory) =>
        FilesToKeep(folders, keepMemory).Any(file => RealClaude.SameFolder(ParentOf(folders, file), folder));

    /// The guard in front of every delete of a whole folder. Only a folder from the list above
    /// passes: a Claude Code folder, the shell profile, the home folder and the base folders
    /// themselves all answer false, and so does a folder inside ours. A folder that keeps a file
    /// answers false too: it is emptied through MayDeleteInside.
    public static bool MayDelete(AikoFolders folders, string path, bool keepMemory) =>
        !string.IsNullOrWhiteSpace(path)
        && FoldersToDelete(folders).Any(own => RealClaude.SameFolder(own, path))
        && !KeepsSomethingIn(folders, path, keepMemory);

    /// The guard for emptying a folder that keeps a file. Only something that lies directly in
    /// that folder passes, and never the file that stays. "." and ".." are refused by name: they
    /// look like something inside the folder and mean the folder itself and the one above it.
    /// Windows drops dots and spaces at the end of a name, so "memory.json." is the memory too.
    public static bool MayDeleteInside(AikoFolders folders, string path, bool keepMemory)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var (parent, name) = Split(folders, path);
        name = name.TrimEnd('.', ' ');
        return name.Length > 0
            && FoldersToDelete(folders).Any(own => RealClaude.SameFolder(own, parent))
            && KeepsSomethingIn(folders, parent, keepMemory)
            && !FilesToKeep(folders, keepMemory).Any(file =>
                string.Equals(Split(folders, file).Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private static string ParentOf(AikoFolders folders, string path) => Split(folders, path).Parent;

    /// Cuts a path at its last separator. Windows reads '/' as a separator too; on macOS a
    /// backslash is a letter of the name like any other.
    private static (string Parent, string Name) Split(AikoFolders folders, string path)
    {
        char[] separators = folders.Platform.DirectorySeparator == '/' ? ['/'] : ['\\', '/'];
        var trimmed = path.Trim().TrimEnd(separators);
        var cut = trimmed.LastIndexOfAny(separators);
        return cut < 0 ? ("", trimmed) : (trimmed[..cut], trimmed[(cut + 1)..]);
    }
}
