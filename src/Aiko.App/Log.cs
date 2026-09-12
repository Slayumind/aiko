using System.IO;

namespace Aiko.App;

/// A short log of what Aiko did. It feeds the "copy diagnostics" button in settings, and it
/// answers days when the tray stays quiet and nothing on screen says why.
///
/// Only our own events go in here. Never a token, never anything Claude Code handed us.
static class Log
{
    private const long MaxBytes = 256 * 1024;

    private static readonly object Gate = new();

    private static readonly string File = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Aiko",
        "log.txt");

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(File)!);

                // The log is for the last little while, not forever. Past the limit it starts over,
                // which is simpler than rotating files and enough for one bug report.
                if (System.IO.File.Exists(File) && new FileInfo(File).Length > MaxBytes)
                {
                    System.IO.File.Delete(File);
                }

                System.IO.File.AppendAllText(
                    File,
                    $"{DateTimeOffset.Now:HH:mm:ss.fff}  {message}{Environment.NewLine}");
            }
        }
        catch (IOException)
        {
            // A log that cannot be written is not a reason to break the tray.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
