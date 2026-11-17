using System.IO;

namespace Aiko.Shared;

/// The memory file on the disk, written the same way by the bridge and by the app. Hooks of several
/// sessions, the memory server and the settings window all read the file, change it and write it
/// back; without one writer at a time the slower one would undo the other's change.
static class MemoryDisk
{
    /// The lock file: 80 tries, 25 ms apart, two seconds in all, as the Mac waits on flock.
    public static FileStream TakeLock(string memoryFile)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(memoryFile)!);
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return new FileStream(memoryFile + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (attempt < 80)
            {
                Thread.Sleep(25);
            }
        }
    }

    /// A file that was not a memory is kept as ".bad" before the new one takes its place, so a slip
    /// in a hand edit never costs the person their facts.
    public static void Write(string memoryFile, string json, bool keepBadFile)
    {
        if (keepBadFile && File.Exists(memoryFile))
        {
            File.Move(memoryFile, memoryFile + ".bad", overwrite: true);
        }

        AtomicFile.Replace(memoryFile, json);
    }
}
