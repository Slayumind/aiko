using System.IO;
using System.Text;
using Aiko.Core;

namespace Aiko.App;

/// The memory file, as the settings window reads and changes it.
///
/// The bridge writes the same file from hooks and from the memory server of every open session,
/// so a change here follows the bridge's rules: take the lock file, read the file again, change
/// what was read, write through a temporary file. Without the lock the window would write back
/// the list it showed a minute ago and undo a fact the model saved since.
static class MemoryStore
{
    private static string File => ThisComputer.Folders.MemoryFile;

    public static string Path => File;

    /// What is in the file now. A file that cannot be read shows as an empty memory; it is kept
    /// aside as ".bad" only when something is written over it.
    public static MemoryFile Load()
    {
        try
        {
            return MemoryFile.FromJson(ReadIfThere()).Memory;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return MemoryFile.Empty;
        }
    }

    /// Reads the file under the lock, applies the change and writes the result when it changed.
    /// Null when the file could not be locked, read or written: then nothing changed.
    public static MemoryHandEdit? Change(Func<MemoryFile, MemoryHandEdit> change)
    {
        try
        {
            using var gate = TakeLock();
            var read = MemoryFile.FromJson(ReadIfThere());
            var edit = change(read.Memory);
            if (edit.Changed)
            {
                Write(edit.Memory.ToJson(), keepBadFile: read.Unreadable);
            }

            return edit;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.Write($"memory: the file could not be changed ({e.GetType().Name})");
            return null;
        }
    }

    /// For "Open file": a memory that was never written has no file to open yet.
    public static bool EnsureFile()
    {
        try
        {
            using var gate = TakeLock();
            if (!System.IO.File.Exists(File))
            {
                Write(MemoryFile.Empty.ToJson(), keepBadFile: false);
            }

            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string ReadIfThere() => System.IO.File.Exists(File) ? System.IO.File.ReadAllText(File) : "";

    /// The same lock file and the same patience as the bridge: 80 tries, 25 ms apart.
    private static FileStream TakeLock()
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(File)!);
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return new FileStream(File + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (attempt < 80)
            {
                Thread.Sleep(25);
            }
        }
    }

    private static void Write(string json, bool keepBadFile)
    {
        if (keepBadFile && System.IO.File.Exists(File))
        {
            System.IO.File.Move(File, File + ".bad", overwrite: true);
        }

        var temporary = File + "." + Environment.ProcessId + ".tmp";
        System.IO.File.WriteAllText(temporary, json, new UTF8Encoding(false));
        System.IO.File.Move(temporary, File, overwrite: true);
    }
}
